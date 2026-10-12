using System;
using System.Collections.Generic;

namespace DynamicWorldEngine.Database
{
    // -------------------------------------------------------------------------
    // EVIDENCE HIERARCHY
    // Lower integer = stronger authority.
    // A lower-ranked evidence type must NEVER override contradictory evidence
    // of a higher rank.
    // -------------------------------------------------------------------------
    public enum EvidenceType
    {
        DirectGameMetadata    = 1,  // Hash in CDImage; IS_MODEL_IN_CDIMAGE = true
        GameResourceReference = 2,  // YMAP / YTYP / RPF reference
        RuntimeValidation     = 3,  // Model loaded, spawned, collision confirmed
        ArchiveMetadata       = 4,  // dlc.rpf directory scan
        DependencyReference   = 5,  // Asset referenced by another confirmed asset
        ScriptReference       = 6,  // Appears in known game script data
        DlcSourceContext      = 7,  // DLC pack naming / prefix convention (ba_, h4_, etc.)
        SemanticSimilarity    = 8,  // Tag / affordance / keyword match
        FilenameSimilarity    = 9,  // Model name string contains identity hint
        CuratedManifest       = 10, // Manually authored in characters.json, interiors.json, etc.
        LLMInference          = 11  // LLM proposed this — NEVER used as ground truth inside DWE
    }

    /// <summary>
    /// A single, typed piece of evidence supporting an asset fact or inference.
    /// Every meaningful DWE assertion about an asset must carry at least one EvidenceRecord.
    /// </summary>
    public class EvidenceRecord
    {
        public string EvidenceId      { get; set; }
        public EvidenceType Type      { get; set; }
        public string Source          { get; set; }  // Provider name, scanner class, file path, etc.
        public string SourceLocation  { get; set; }  // Specific file / archive / line
        public string Description     { get; set; }
        /// <summary>
        /// Normalised 0.0–1.0. Reflects how strong this specific piece of evidence is.
        /// Strong runtime evidence = 0.95+. Curated manifest = 0.85. Filename similarity = 0.25.
        /// </summary>
        public float Strength         { get; set; }
        public DateTime RetrievedAt   { get; set; }

        public EvidenceRecord()
        {
            EvidenceId  = Guid.NewGuid().ToString("N").Substring(0, 12);
            RetrievedAt = DateTime.UtcNow;
        }

        // ------------------------------------------------------------------
        // Convenience factories for common evidence production sites
        // ------------------------------------------------------------------

        public static EvidenceRecord FromCuratedManifest(string manifestPath, string description, float strength = 0.85f)
        {
            return new EvidenceRecord
            {
                Type           = EvidenceType.CuratedManifest,
                Source         = "JsonManifestAssetProvider",
                SourceLocation = manifestPath,
                Description    = description,
                Strength       = strength
            };
        }

        public static EvidenceRecord FromRuntimeValidation(string modelName, string detail, float strength = 0.97f)
        {
            return new EvidenceRecord
            {
                Type           = EvidenceType.RuntimeValidation,
                Source         = "GameAssetScanner/RuntimeProbe",
                SourceLocation = modelName,
                Description    = detail,
                Strength       = strength
            };
        }

        public static EvidenceRecord FromDlcSourceContext(string prefix, string dlcPack, float strength = 0.70f)
        {
            return new EvidenceRecord
            {
                Type           = EvidenceType.DlcSourceContext,
                Source         = "SemanticClassificationEngine",
                SourceLocation = "model_name_prefix:" + prefix,
                Description    = string.Format("Model prefix '{0}' conventionally maps to DLC pack '{1}'", prefix, dlcPack),
                Strength       = strength
            };
        }

        public static EvidenceRecord FromSemanticSimilarity(string matchedToken, string property, float strength = 0.45f)
        {
            return new EvidenceRecord
            {
                Type           = EvidenceType.SemanticSimilarity,
                Source         = "SemanticClassificationEngine",
                SourceLocation = "token:" + matchedToken,
                Description    = string.Format("Token '{0}' matched property '{1}'", matchedToken, property),
                Strength       = strength
            };
        }

        public static EvidenceRecord FromFilenameSimilarity(string modelName, string matchedIdentity, float strength = 0.25f)
        {
            return new EvidenceRecord
            {
                Type           = EvidenceType.FilenameSimilarity,
                Source         = "CanonicalIdentityResolver",
                SourceLocation = "model_name:" + modelName,
                Description    = string.Format("Model name '{0}' contains substring resembling '{1}'", modelName, matchedIdentity),
                Strength       = strength
            };
        }

        public static EvidenceRecord FromDirectGameMetadata(string modelName, uint hash)
        {
            return new EvidenceRecord
            {
                Type           = EvidenceType.DirectGameMetadata,
                Source         = "DiscoveredModelProvider",
                SourceLocation = string.Format("hash:0x{0:X8}", hash),
                Description    = string.Format("Model '{0}' confirmed present in CDImage (hash 0x{1:X8})", modelName, hash),
                Strength       = 0.99f
            };
        }

        public override string ToString()
        {
            return string.Format("[{0}|{1}|str={2:F2}] {3}", Type, Source, Strength, Description);
        }
    }

    // -------------------------------------------------------------------------
    // EPISTEMIC STATUS
    // -------------------------------------------------------------------------

    /// <summary>
    /// The epistemic certainty of a single asset property value.
    /// Facts are established by strong independent evidence.
    /// Inferences are hypothesis — never silently promoted to facts.
    /// </summary>
    public enum EpistemicStatus
    {
        /// <summary>Machine-verified via runtime CDImage / hash / native probe.</summary>
        Fact,
        /// <summary>
        /// Supported by strong evidence (CuratedManifest or better). The asset resolver
        /// may use this — but the evidence chain must be preserved so consumers know the source.
        /// </summary>
        Resolved,
        /// <summary>A hypothesis — one or more weak supporting evidence items.</summary>
        Candidate,
        /// <summary>Multiple conflicting candidates with similar evidence strength.</summary>
        Ambiguous,
        /// <summary>Insufficient evidence to establish a value.</summary>
        Unknown
    }

    /// <summary>
    /// A machine-verified, evidence-backed property of a GTA asset.
    /// Only properties that can be directly established from game data should be AssetFacts.
    /// </summary>
    public class AssetFact
    {
        public string Property      { get; set; }  // e.g. "model_name", "type", "exists_in_cdimage"
        public string Value         { get; set; }
        public EvidenceRecord Evidence { get; set; }
        public EpistemicStatus Status  { get; set; }

        public AssetFact()
        {
            Status = EpistemicStatus.Fact;
        }
    }

    /// <summary>
    /// A hypothesis about an asset property derived from evidence.
    /// Inferences must NEVER be silently stored as facts.
    /// Consumers must inspect Status before treating an inference as authoritative.
    /// </summary>
    public class AssetInference
    {
        public string Property       { get; set; }  // e.g. "identity", "dlc_origin", "semantic_role"
        public string Value          { get; set; }
        public float Confidence      { get; set; }  // Aggregate; 0.0–1.0
        public EpistemicStatus Status { get; set; }
        public List<EvidenceRecord> SupportingEvidence { get; set; }

        public AssetInference()
        {
            SupportingEvidence = new List<EvidenceRecord>();
            Status             = EpistemicStatus.Candidate;
        }

        /// <summary>
        /// Returns the evidence item with the lowest (strongest) EvidenceType rank.
        /// </summary>
        public EvidenceRecord StrongestEvidence()
        {
            if (SupportingEvidence == null || SupportingEvidence.Count == 0) return null;
            EvidenceRecord best = null;
            foreach (var e in SupportingEvidence)
            {
                if (best == null || (int)e.Type < (int)best.Type) best = e;
            }
            return best;
        }

        /// <summary>
        /// Re-computes aggregate confidence as a weighted blend of supporting evidence strengths.
        /// Stronger evidence types receive a hierarchy bonus.
        /// </summary>
        public float ComputeAggregateConfidence()
        {
            if (SupportingEvidence == null || SupportingEvidence.Count == 0) return 0f;

            float total = 0f;
            float weight = 0f;
            foreach (var e in SupportingEvidence)
            {
                // Weight by inverse rank (rank 1 = weight 11, rank 11 = weight 1)
                float w = (12f - (int)e.Type) / 11f;
                total  += e.Strength * w;
                weight += w;
            }
            return weight > 0f ? Math.Min(1f, total / weight) : 0f;
        }
    }

    // -------------------------------------------------------------------------
    // CANONICAL ASSET RESOLUTION
    // -------------------------------------------------------------------------

    /// <summary>
    /// The final outcome of the evidence-based asset resolver.
    /// The SpatialWorldPlanner may only consume Exact, Resolved, or Approximate statuses.
    /// Unknown / Unavailable / Invalid must never be silently spawned.
    /// </summary>
    public enum ResolutionStatus
    {
        /// <summary>Direct model hash match; runtime-verified existence confirmed.</summary>
        Exact,
        /// <summary>Fully evidence-backed match; manifest identity confirmed.</summary>
        Resolved,
        /// <summary>Best available match; known to be imperfect / approximate.</summary>
        Approximate,
        /// <summary>Multiple candidates with equal evidence — caller must disambiguate.</summary>
        Ambiguous,
        /// <summary>No matching asset found in discovered index.</summary>
        Unknown,
        /// <summary>Asset not present in user's GTA V installation.</summary>
        Unavailable,
        /// <summary>Requested identifier is fabricated or structurally impossible.</summary>
        Invalid
    }

    /// <summary>
    /// The evidence-grounded result of an asset resolution request.
    /// Every field here has an explicit provenance.
    /// </summary>
    public class CanonicalAssetResolution
    {
        public string Request                       { get; set; }
        public ResolutionStatus Status              { get; set; }
        public GtaAssetDefinition SelectedAsset     { get; set; }
        public List<GtaAssetDefinition> Alternatives { get; set; }
        public List<EvidenceRecord> Evidence        { get; set; }
        public bool IsApproximation                 { get; set; }
        public float Confidence                     { get; set; }
        /// <summary>
        /// Human-readable explanation of why this asset was selected and on what evidence.
        /// Required — never empty for Resolved/Approximate statuses.
        /// </summary>
        public string ExplanationSummary            { get; set; }

        public CanonicalAssetResolution()
        {
            Alternatives = new List<GtaAssetDefinition>();
            Evidence     = new List<EvidenceRecord>();
        }

        // ------------------------------------------------------------------
        // Factory helpers for terminal states
        // ------------------------------------------------------------------

        public static CanonicalAssetResolution CreateUnknown(string request, string reason)
        {
            return new CanonicalAssetResolution
            {
                Request            = request,
                Status             = ResolutionStatus.Unknown,
                Confidence         = 0f,
                ExplanationSummary = reason
            };
        }

        public static CanonicalAssetResolution CreateInvalid(string request, string reason)
        {
            return new CanonicalAssetResolution
            {
                Request            = request,
                Status             = ResolutionStatus.Invalid,
                Confidence         = 0f,
                ExplanationSummary = reason
            };
        }

        /// <summary>
        /// Returns true when the SpatialWorldPlanner is allowed to consume this resolution.
        /// </summary>
        public bool IsPlannerConsumable()
        {
            return Status == ResolutionStatus.Exact
                || Status == ResolutionStatus.Resolved
                || Status == ResolutionStatus.Approximate;
        }

        public override string ToString()
        {
            return string.Format("[{0}|conf={1:F2}|approx={2}] {3} -> {4}",
                Status, Confidence, IsApproximation,
                Request,
                SelectedAsset != null ? SelectedAsset.ModelName : "none");
        }
    }
}
