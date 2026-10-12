using System;
using System.Collections.Generic;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Discovery
{
    public class IdentityResolutionResult
    {
        public string RequestedQuery          { get; set; }
        public string ResolvedPedModel        { get; set; }
        public string ResolvedCharacterName   { get; set; }
        public string CanonicalDlcOrigin      { get; set; }
        public float ConfidenceScore          { get; set; }
        public KnowledgeConfidence ConfidenceLevel { get; set; }
        public bool IsSuccess                 { get; set; }
        public bool IsAmbiguous               { get; set; }
        public string RejectionReason         { get; set; }

        /// <summary>
        /// Typed evidence chain.  Every item explains a scoring contribution.
        /// Consumers can inspect evidence types to understand resolution authority.
        /// </summary>
        public List<EvidenceRecord> EvidenceRecords { get; set; }

        /// <summary>Backwards-compatible string summary built from EvidenceRecords.</summary>
        public List<string> EvidenceChain
        {
            get
            {
                var chain = new List<string>();
                if (EvidenceRecords != null)
                    foreach (var e in EvidenceRecords) chain.Add(e.ToString());
                return chain;
            }
        }

        public List<GtaAssetDefinition> CandidatePeds { get; set; }

        public IdentityResolutionResult()
        {
            EvidenceRecords = new List<EvidenceRecord>();
            CandidatePeds   = new List<GtaAssetDefinition>();
            ConfidenceLevel = KnowledgeConfidence.Unknown;
        }

        public CharacterResolutionAudit ToCharacterAudit()
        {
            return new CharacterResolutionAudit
            {
                RequestedQuery       = RequestedQuery,
                RequestedRole        = CandidatePeds.Count > 0 && CandidatePeds[0].Roles.Count > 0
                                         ? CandidatePeds[0].Roles[0] : "Character",
                ResolvedCharacterName = ResolvedCharacterName ?? "Unresolved Character",
                ResolvedPedModel     = ResolvedPedModel ?? "unknown",
                CanonicalDlcOrigin   = CanonicalDlcOrigin ?? "Standard",
                RejectionReason      = RejectionReason ?? "No canonical identity established.",
                VerificationSummary  = IsSuccess
                    ? "IDENTITY RESOLVED VIA EVIDENCE-GROUNDED ASSET INTELLIGENCE"
                    : "IDENTITY RESOLUTION UNVERIFIED — Insufficient evidence"
            };
        }
    }

    /// <summary>
    /// Autonomous Canonical Identity Resolver.
    ///
    /// Maps freeform natural language identities ("Dr. Dre", "Franklin", "Solomun")
    /// to verified ped models in the active GTA V installation WITHOUT any hard-coded
    /// name-to-model mappings in source code.
    ///
    /// All scoring is driven by AssetInference entries attached to discovered assets.
    /// The identity chain in characters.json is the authoritative external data source;
    /// this class only evaluates evidence, it does not fabricate it.
    ///
    /// EVIDENCE HIERARCHY ENFORCED:
    ///   RuntimeValidation (rank 3) > CuratedManifest (rank 10) > FilenameSimilarity (rank 9)
    /// </summary>
    public class CanonicalIdentityResolver
    {
        private readonly SpatialAssetRegistry _registry;

        public CanonicalIdentityResolver(SpatialAssetRegistry registry = null)
        {
            _registry = registry ?? SpatialAssetRegistry.Instance;
        }

        public IdentityResolutionResult ResolveIdentity(string identityOrRoleQuery, string contextDlc = null)
        {
            var result = new IdentityResolutionResult
            {
                RequestedQuery = identityOrRoleQuery
            };

            if (string.IsNullOrWhiteSpace(identityOrRoleQuery))
            {
                result.IsSuccess      = false;
                result.RejectionReason = "Empty or null identity query.";
                return result;
            }

            string qLower = identityOrRoleQuery.Trim().ToLowerInvariant();

            var allPeds = new List<GtaAssetDefinition>();
            foreach (var a in _registry.AllAssets)
            {
                if (a.Type == AssetType.Ped) allPeds.Add(a);
            }

            if (allPeds.Count == 0)
            {
                result.IsSuccess      = false;
                result.RejectionReason = "No ped models registered in SpatialAssetRegistry.";
                return result;
            }

            var scored = new List<KeyValuePair<GtaAssetDefinition, float>>();

            foreach (var ped in allPeds)
            {
                var pedEvidence = new List<EvidenceRecord>();
                float score = ScorePedIdentityMatch(ped, qLower, contextDlc, pedEvidence);
                if (score > 0f)
                {
                    scored.Add(new KeyValuePair<GtaAssetDefinition, float>(ped, score));
                    // Merge per-asset evidence into the result set (deduplicated by evidenceId later)
                    foreach (var e in pedEvidence) result.EvidenceRecords.Add(e);
                }
            }

            if (scored.Count == 0)
            {
                result.IsSuccess      = false;
                result.RejectionReason = string.Format(
                    "No authentic canonical asset matched query '{0}'. " +
                    "No identity inference in the discovered asset index supports this query. " +
                    "Hallucination prevented.", identityOrRoleQuery);
                return result;
            }

            scored.Sort((a, b) => b.Value.CompareTo(a.Value));

            // Ambiguity: top two candidates within 5% of each other
            if (scored.Count > 1 && Math.Abs(scored[0].Value - scored[1].Value) < scored[0].Value * 0.05f)
            {
                result.IsAmbiguous = true;
            }

            var best = scored[0].Key;

            // Determine resolved identity label from the asset's identity inference
            AssetInference identInf = best.GetPrimaryIdentityInference();
            string resolvedName     = identInf != null ? identInf.Value
                                      : (best.Identities.Count > 0 ? best.Identities[0] : best.ModelName);

            result.ResolvedPedModel      = best.ModelName;
            result.ResolvedCharacterName = resolvedName;
            result.CanonicalDlcOrigin    = !string.IsNullOrEmpty(best.DlcPackOrigin)
                                             ? best.DlcPackOrigin : "GTA V Canonical";
            result.ConfidenceScore       = Math.Min(1.0f, scored[0].Value / 200f);
            result.ConfidenceLevel       = best.Confidence;
            result.IsSuccess             = true;

            // Summary evidence record for this resolution
            result.EvidenceRecords.Add(new EvidenceRecord
            {
                Type           = EvidenceType.SemanticSimilarity,
                Source         = "CanonicalIdentityResolver",
                SourceLocation = "SpatialAssetRegistry.AllAssets",
                Description    = string.Format(
                    "Resolved '{0}' to ped model '{1}' (identity='{2}') with aggregate score {3:F1}. " +
                    "Ambiguous={4}. DLC='{5}'.",
                    identityOrRoleQuery, result.ResolvedPedModel, resolvedName,
                    scored[0].Value, result.IsAmbiguous, result.CanonicalDlcOrigin),
                Strength    = result.ConfidenceScore,
                RetrievedAt = DateTime.UtcNow
            });

            foreach (var pair in scored)
            {
                result.CandidatePeds.Add(pair.Key);
            }

            return result;
        }

        // -------------------------------------------------------------------------
        // SCORING — derived entirely from evidence attached to discovered assets.
        //
        // NO hard-coded name-to-model mappings are permitted here.
        // All identity associations come from AssetInference entries produced by
        // JsonManifestAssetProvider (CuratedManifest) or runtime validation.
        //
        // Evidence hierarchy determines score multipliers:
        //   RuntimeValidation  -> ×1.5
        //   CuratedManifest    -> ×1.0 (baseline)
        //   FilenameSimilarity -> ×0.3
        // -------------------------------------------------------------------------

        private float ScorePedIdentityMatch(
            GtaAssetDefinition ped, string qLower, string contextDlc,
            List<EvidenceRecord> collectedEvidence)
        {
            float score  = 0f;
            string model = (ped.ModelName ?? "").ToLowerInvariant();

            // ------------------------------------------------------------------
            // 1. Score from evidence-backed AssetInference entries
            //    This is the ONLY authoritative scoring path for identity.
            //    Evidence from characters.json (CuratedManifest, strength=0.85)
            //    upgrades to RuntimeValidation once the engine probes the model.
            // ------------------------------------------------------------------
            if (ped.Inferences != null)
            {
                foreach (var inf in ped.Inferences)
                {
                    string infVal = (inf.Value ?? "").ToLowerInvariant();
                    if (string.IsNullOrEmpty(infVal)) continue;

                    // Evidence hierarchy multiplier
                    EvidenceRecord strongest = inf.StrongestEvidence();
                    float hierarchyMult = GetHierarchyMultiplier(strongest);

                    bool isIdentityProp = inf.Property == "identity";
                    bool isKeywordProp  = inf.Property == "keyword";

                    if (isIdentityProp || isKeywordProp)
                    {
                        float baseScore = isIdentityProp ? 120f : 65f;

                        if (infVal.Equals(qLower, StringComparison.Ordinal))
                        {
                            float contribution = baseScore * inf.Confidence * hierarchyMult;
                            score += contribution;
                            if (strongest != null)
                            {
                                collectedEvidence.Add(new EvidenceRecord
                                {
                                    Type           = strongest.Type,
                                    Source         = strongest.Source,
                                    SourceLocation = strongest.SourceLocation,
                                    Description    = string.Format(
                                        "Exact {0} match: '{1}' == query '{2}' on model '{3}' (+{4:F1})",
                                        inf.Property, inf.Value, qLower, ped.ModelName, contribution),
                                    Strength    = strongest.Strength,
                                    RetrievedAt = DateTime.UtcNow
                                });
                            }
                        }
                        else if (qLower.Contains(infVal) || infVal.Contains(qLower))
                        {
                            float contribution = (baseScore * 0.70f) * inf.Confidence * hierarchyMult;
                            score += contribution;
                            if (strongest != null)
                            {
                                collectedEvidence.Add(new EvidenceRecord
                                {
                                    Type           = strongest.Type,
                                    Source         = strongest.Source,
                                    SourceLocation = strongest.SourceLocation,
                                    Description    = string.Format(
                                        "Substring {0} match: '{1}' in query '{2}' on model '{3}' (+{4:F1})",
                                        inf.Property, inf.Value, qLower, ped.ModelName, contribution),
                                    Strength    = strongest.Strength * 0.7f,
                                    RetrievedAt = DateTime.UtcNow
                                });
                            }
                        }
                    }
                    else if (inf.Property == "role")
                    {
                        if (infVal.Equals(qLower) || qLower.Contains(infVal) || infVal.Contains(qLower))
                        {
                            float contribution = 30f * inf.Confidence * hierarchyMult;
                            score += contribution;
                        }
                    }
                }
            }

            // ------------------------------------------------------------------
            // 2. Fallback: model name string similarity (FilenameSimilarity, rank 9)
            //    Only adds a weak signal. Cannot promote a candidate above one with
            //    strong identity inferences.
            // ------------------------------------------------------------------
            if (model.Contains(qLower) && qLower.Length >= 3)
            {
                float contribution = 20f * 0.25f;  // low multiplier — FilenameSimilarity
                score += contribution;
                collectedEvidence.Add(EvidenceRecord.FromFilenameSimilarity(
                    ped.ModelName, qLower, 0.25f));
            }

            // ------------------------------------------------------------------
            // 3. Prefer DLC alignment (bonus only, cannot establish identity alone)
            // ------------------------------------------------------------------
            if (!string.IsNullOrEmpty(contextDlc) && !string.IsNullOrEmpty(ped.DlcPackOrigin))
            {
                if (ped.DlcPackOrigin.IndexOf(contextDlc, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 15f;
                }
            }

            return score;
        }

        private static float GetHierarchyMultiplier(EvidenceRecord evidence)
        {
            if (evidence == null) return 0.20f;  // No evidence: very low weight
            switch (evidence.Type)
            {
                case EvidenceType.DirectGameMetadata:    return 1.80f;
                case EvidenceType.RuntimeValidation:     return 1.50f;
                case EvidenceType.GameResourceReference: return 1.30f;
                case EvidenceType.ArchiveMetadata:       return 1.20f;
                case EvidenceType.DependencyReference:   return 1.10f;
                case EvidenceType.ScriptReference:       return 1.05f;
                case EvidenceType.DlcSourceContext:      return 0.90f;
                case EvidenceType.SemanticSimilarity:    return 0.60f;
                case EvidenceType.FilenameSimilarity:    return 0.30f;
                case EvidenceType.CuratedManifest:       return 1.00f;  // Operator-controlled: baseline
                case EvidenceType.LLMInference:          return 0.00f;  // NEVER accepted
                default:                                 return 0.20f;
            }
        }

    }
}
