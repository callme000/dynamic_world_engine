using System;
using System.Collections.Generic;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Discovery
{
    /// <summary>
    /// A single property extracted from a semantic request.
    /// E.g. from "anti-aircraft laser cannon with disco lights":
    ///   ObjectClass=weapon, StyleTag=military, FunctionalTag=anti-aircraft, VisualTag=laser, VisualTag=disco_lights
    /// </summary>
    public class DecomposedProperty
    {
        public string Category  { get; set; }  // ObjectClass, StyleTag, FunctionalTag, VisualTag, ContextTag
        public string Value     { get; set; }
        public float Confidence { get; set; }   // 0.0-1.0: how certain we are about this extraction
    }

    /// <summary>
    /// Result of decomposing a request that may be partially or fully unrealizable.
    /// </summary>
    public class SemanticDecompositionResult
    {
        public string OriginalRequest                      { get; set; }
        public bool   CanBeRealized                        { get; set; }
        public bool   IsApproximation                      { get; set; }
        /// <summary>Properties extracted from the request.</summary>
        public List<DecomposedProperty> ExtractedProperties { get; set; }
        /// <summary>Assets found that partially satisfy the request.</summary>
        public List<CanonicalAssetResolution> PartialRealizations { get; set; }
        /// <summary>Description of what could not be realized.</summary>
        public string UnsupportedRemainder                 { get; set; }
        public float ApproximationConfidence               { get; set; }
        public string ExplanationSummary                   { get; set; }

        public SemanticDecompositionResult()
        {
            ExtractedProperties  = new List<DecomposedProperty>();
            PartialRealizations  = new List<CanonicalAssetResolution>();
        }
    }

    /// <summary>
    /// Semantic Decomposer — handles requests for assets that may not exist, are impossible,
    /// or are ambiguous. Prevents hallucination by refusing to fabricate model names.
    ///
    /// Pipeline:
    ///   Request text
    ///   → ExtractProperties (natural language → typed property set)
    ///   → Search registry for partial matches
    ///   → Compose SemanticDecompositionResult with explicit approximation flag
    ///
    /// The decomposer NEVER invents model names. If no asset matches a property,
    /// that property is recorded in UnsupportedRemainder, not silently dropped.
    /// </summary>
    public class SemanticDecomposer
    {
        private readonly SpatialAssetRegistry _registry;

        public SemanticDecomposer(SpatialAssetRegistry registry = null)
        {
            _registry = registry ?? SpatialAssetRegistry.Instance;
        }

        // ------------------------------------------------------------------
        // Primary entry point
        // ------------------------------------------------------------------

        /// <summary>
        /// Decomposes a potentially impossible or fictional request into realizable parts.
        /// Never fabricates asset identifiers.
        /// </summary>
        public SemanticDecompositionResult Decompose(string request)
        {
            var result = new SemanticDecompositionResult
            {
                OriginalRequest = request
            };

            if (string.IsNullOrWhiteSpace(request))
            {
                result.CanBeRealized    = false;
                result.ExplanationSummary = "Empty request.";
                return result;
            }

            // 1. Extract semantic properties
            result.ExtractedProperties = ExtractProperties(request);

            // 2. Check for identity-type requests (person / character)
            bool isIdentityRequest = false;
            string identityQuery   = null;
            foreach (var p in result.ExtractedProperties)
            {
                if (p.Category == "IdentityQuery")
                {
                    isIdentityRequest = true;
                    identityQuery     = p.Value;
                    break;
                }
            }

            if (isIdentityRequest && !string.IsNullOrEmpty(identityQuery))
            {
                return DecomposeIdentityRequest(request, identityQuery, result);
            }

            // 3. Generic asset property search
            return DecomposeGenericRequest(request, result);
        }

        // ------------------------------------------------------------------
        // Identity decomposition (for "Put Xylo Kane in the world" etc.)
        // ------------------------------------------------------------------

        private SemanticDecompositionResult DecomposeIdentityRequest(
            string request, string identityQuery, SemanticDecompositionResult result)
        {
            var resolver = _registry.IdentityResolver;
            if (resolver == null)
            {
                result.CanBeRealized      = false;
                result.ExplanationSummary  = "Identity resolver not available.";
                return result;
            }

            var resolution = resolver.ResolveIdentity(identityQuery);

            if (!resolution.IsSuccess)
            {
                result.CanBeRealized         = false;
                result.UnsupportedRemainder  = string.Format(
                    "Identity '{0}' is unknown. No asset in the discovered index has an identity " +
                    "inference matching this query. This may be a fictional character or an identity " +
                    "not present in the installed GTA V content. No model will be fabricated.",
                    identityQuery);
                result.ApproximationConfidence = 0f;
                result.ExplanationSummary = string.Format(
                    "Request decomposed. Identity '{0}' not found in evidence-grounded asset index. " +
                    "Rejection reason: {1}", identityQuery, resolution.RejectionReason);
            }
            else
            {
                // Wrap as CanonicalAssetResolution for the planner
                var assetResolution = new CanonicalAssetResolution
                {
                    Request         = identityQuery,
                    Status          = resolution.IsAmbiguous ? ResolutionStatus.Ambiguous : ResolutionStatus.Resolved,
                    SelectedAsset   = _registry.GetAsset(resolution.ResolvedPedModel),
                    Evidence        = resolution.EvidenceRecords,
                    IsApproximation = resolution.IsAmbiguous,
                    Confidence      = resolution.ConfidenceScore,
                    ExplanationSummary = string.Format(
                        "Identity '{0}' resolved to model '{1}' via {2}. Confidence={3:F2}. Ambiguous={4}.",
                        identityQuery, resolution.ResolvedPedModel,
                        resolution.ConfidenceLevel, resolution.ConfidenceScore, resolution.IsAmbiguous)
                };

                result.CanBeRealized    = assetResolution.IsPlannerConsumable();
                result.IsApproximation  = resolution.IsAmbiguous;
                result.ApproximationConfidence = resolution.ConfidenceScore;
                result.PartialRealizations.Add(assetResolution);
                result.ExplanationSummary = string.Format(
                    "Identity '{0}' → '{1}' (model='{2}', conf={3:F2}).",
                    identityQuery, resolution.ResolvedCharacterName,
                    resolution.ResolvedPedModel, resolution.ConfidenceScore);
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Generic / composite concept decomposition
        // ------------------------------------------------------------------

        private SemanticDecompositionResult DecomposeGenericRequest(
            string request, SemanticDecompositionResult result)
        {
            string reqLower = request.ToLowerInvariant();
            var unsupported = new List<string>();
            bool anyRealized = false;

            // For each extracted property, search for assets matching that property
            foreach (var prop in result.ExtractedProperties)
            {
                if (prop.Category == "IdentityQuery") continue;

                var query = new AssetQuery
                {
                    Type        = AssetType.Prop,
                    RawQuery    = prop.Value
                };

                // Map semantic property categories to asset types and query params
                if (prop.Category == "ObjectClass")
                {
                    string v = prop.Value.ToLowerInvariant();
                    if (v.Contains("vehicle") || v.Contains("car") || v.Contains("truck"))
                        query.Type = AssetType.Vehicle;
                    else if (v.Contains("ped") || v.Contains("person") || v.Contains("npc"))
                        query.Type = AssetType.Ped;
                    else
                        query.Type = AssetType.Prop;
                }

                query.RequiredTags = new List<string>();
                if (prop.Category == "FunctionalTag" || prop.Category == "StyleTag")
                    query.RequiredTags.Add(prop.Value);

                var resolution = _registry.ResolveWithEvidence(query);

                if (resolution.IsPlannerConsumable())
                {
                    resolution.IsApproximation = true;  // Decomposed request is always approximate
                    resolution.ExplanationSummary = string.Format(
                        "[Decomposed] Property '{0}' ({1}) → model '{2}' (confidence={3:F2}, approx=true)",
                        prop.Value, prop.Category,
                        resolution.SelectedAsset != null ? resolution.SelectedAsset.ModelName : "none",
                        resolution.Confidence);
                    result.PartialRealizations.Add(resolution);
                    anyRealized = true;
                }
                else
                {
                    unsupported.Add(string.Format("{0}:{1}", prop.Category, prop.Value));
                }
            }

            result.CanBeRealized  = anyRealized;
            result.IsApproximation = anyRealized; // Decomposed = always approximate

            if (unsupported.Count > 0)
            {
                result.UnsupportedRemainder = string.Format(
                    "The following requested properties could not be matched to any discovered asset " +
                    "in the GTA V installation: [{0}]. " +
                    "No model names were fabricated for these properties.",
                    string.Join(", ", unsupported));
            }

            if (anyRealized)
            {
                float avgConf = 0f;
                foreach (var r in result.PartialRealizations) avgConf += r.Confidence;
                avgConf /= result.PartialRealizations.Count;
                result.ApproximationConfidence = avgConf;
            }

            result.ExplanationSummary = string.Format(
                "Request '{0}' decomposed into {1} properties. " +
                "{2} partially realized, {3} unsupported. " +
                "Approximation confidence: {4:F2}. No model names fabricated.",
                request, result.ExtractedProperties.Count,
                result.PartialRealizations.Count, unsupported.Count,
                result.ApproximationConfidence);

            return result;
        }

        // ------------------------------------------------------------------
        // Natural-language → typed property extraction
        //
        // This uses keyword/token rules, NOT LLM output.
        // The extracted properties drive registry searches.
        // If a concept has no matching token rule, it goes into UnsupportedRemainder.
        // ------------------------------------------------------------------

        private List<DecomposedProperty> ExtractProperties(string request)
        {
            var props  = new List<DecomposedProperty>();
            string req = request.ToLowerInvariant();

            // ---- Identity detection (named real-world or fictional person) ----
            // Strategy: check if any word/phrase in the request matches an identity
            // inference in the registry.  Do not assume any identity is valid without evidence.
            ExtractIdentityHints(req, props);

            // ---- Object class ----
            if (Contains(req, "vehicle","car","truck","bike","helicopter","plane","boat","submarine","ship"))
                props.Add(new DecomposedProperty { Category = "ObjectClass", Value = "vehicle", Confidence = 0.90f });
            if (Contains(req, "weapon","gun","cannon","turret","rifle","missile","laser","rocket"))
                props.Add(new DecomposedProperty { Category = "ObjectClass", Value = "weapon", Confidence = 0.85f });
            if (Contains(req, "table","desk","stand","chair","sofa","stool"))
                props.Add(new DecomposedProperty { Category = "ObjectClass", Value = "furniture", Confidence = 0.80f });
            if (Contains(req, "speaker","speaker","subwoofer","boombox","stereo"))
                props.Add(new DecomposedProperty { Category = "ObjectClass", Value = "audio_equipment", Confidence = 0.80f });
            if (Contains(req, "light","lighting","disco","lamp","neon","emissive","glow"))
                props.Add(new DecomposedProperty { Category = "ObjectClass", Value = "lighting", Confidence = 0.75f });

            // ---- Style / Atmosphere ----
            if (Contains(req, "luxury","luxurious","high-end","vip","upscale","premium","penthouse"))
                props.Add(new DecomposedProperty { Category = "StyleTag", Value = "luxury", Confidence = 0.85f });
            if (Contains(req, "military","army","tactical","armored","combat","armour"))
                props.Add(new DecomposedProperty { Category = "StyleTag", Value = "military", Confidence = 0.90f });
            if (Contains(req, "disco","party","nightclub","club","rave","festival"))
                props.Add(new DecomposedProperty { Category = "StyleTag", Value = "party", Confidence = 0.80f });
            if (Contains(req, "tropical","beach","island","cayo","perico"))
                props.Add(new DecomposedProperty { Category = "StyleTag", Value = "tropical", Confidence = 0.75f });

            // ---- Functional tags ----
            if (Contains(req, "anti-aircraft","anti aircraft","aa ","flak","surface-to-air","sam "))
                props.Add(new DecomposedProperty { Category = "FunctionalTag", Value = "anti_aircraft", Confidence = 0.80f });
            if (Contains(req, "dj","turntable","deck","mix","booth"))
                props.Add(new DecomposedProperty { Category = "FunctionalTag", Value = "dj_equipment", Confidence = 0.85f });
            if (Contains(req, "heist","vault","planning","briefing","tactical"))
                props.Add(new DecomposedProperty { Category = "FunctionalTag", Value = "heist", Confidence = 0.75f });
            if (Contains(req, "surveillance","cctv","camera","monitor","screen","command"))
                props.Add(new DecomposedProperty { Category = "FunctionalTag", Value = "surveillance", Confidence = 0.75f });

            // If nothing was extracted, record the whole request as an opaque unknown property
            if (props.Count == 0)
            {
                props.Add(new DecomposedProperty
                {
                    Category   = "Unknown",
                    Value      = request,
                    Confidence = 0.10f
                });
            }

            return props;
        }

        private void ExtractIdentityHints(string req, List<DecomposedProperty> props)
        {
            // Check known identity inferences from the registry index.
            // This avoids hardcoding any names in this class — the registry is the authority.
            if (_registry == null) return;

            foreach (var asset in _registry.AllAssets)
            {
                if (asset.Type != AssetType.Ped) continue;
                if (asset.Inferences == null) continue;

                foreach (var inf in asset.Inferences)
                {
                    if (inf.Property != "identity" && inf.Property != "keyword") continue;
                    string infVal = (inf.Value ?? "").ToLowerInvariant();
                    if (infVal.Length < 3) continue;

                    if (req.Contains(infVal))
                    {
                        // Found a registry-backed identity in the request
                        props.Add(new DecomposedProperty
                        {
                            Category   = "IdentityQuery",
                            Value      = inf.Value,
                            Confidence = inf.Confidence
                        });
                        return; // Take the first (highest-confidence) match per request
                    }
                }
            }
        }

        private static bool Contains(string text, params string[] tokens)
        {
            foreach (var t in tokens)
                if (text.Contains(t)) return true;
            return false;
        }
    }
}
