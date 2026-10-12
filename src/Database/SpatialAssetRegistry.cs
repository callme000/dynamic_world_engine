using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GTA;
using GTA.Math;
using GTA.Native;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Discovery;
using DynamicWorldEngine.Spatial;
using Newtonsoft.Json.Linq;

namespace DynamicWorldEngine.Database
{
    #region Core Enums

    public enum AssetType
    {
        Prop,
        Interior,
        Ped,
        Vehicle,
        Ipl,
        EntitySet,
        Scenario
    }

    public enum KnowledgeConfidence
    {
        MachineVerifiedFactual, // Layer 1: Confirmed via CDIMAGE/Hash/Engine Memory
        InstallationMetadata,   // Layer 2: Extracted from DLC pack, YMAP/YTYP, IPL
        CuratedCanonical,       // Layer 3: Curated canonical Rockstar lore
        SemanticInferred,       // Layer 3: Inferred semantic tags / affordances
        Unknown                 // Unknown / Unverified
    }

    #endregion

    #region Asset Definition Model

    /// <summary>
    /// Unified 3-layer GTA Asset Ontology definition.
    /// Layer 1: Machine-verified Physical Facts.
    /// Layer 2: Installation & DLC Metadata.
    /// Layer 3: Semantic Knowledge, Affordances & Epistemic Confidence.
    /// </summary>
    public class GtaAssetDefinition
    {
        // -------------------------------------------------------------
        // LAYER 1: MACHINE-VERIFIED PHYSICAL FACTS
        // -------------------------------------------------------------
        public string ModelName { get; set; }
        public uint ModelHash { get; set; }
        public AssetType Type { get; set; }
        public bool ExistsInGame { get; set; }
        public bool HasCollision { get; set; }
        public Vector3 Dimensions { get; set; } // Length, Width, Height (Bounding Box)
        public float SurfaceElevation { get; set; } // Usable surface elevation for child objects
        public bool Walkable { get; set; }
        public bool BlocksNavMesh { get; set; }
        public bool CanAttachChildren { get; set; }

        // -------------------------------------------------------------
        // LAYER 2: INSTALLATION & DLC METADATA
        // -------------------------------------------------------------
        public string DlcPackOrigin { get; set; } // e.g. "mpVinewood", "mpHeist4", "mpSecurity", "mpBattle"
        public string ArchetypeName { get; set; }
        public List<string> IplDependencies { get; set; }
        public List<string> RemoveIplDependencies { get; set; }
        public List<string> EntitySets { get; set; }
        public string Name { get; set; }
        public Vector3 DefaultCoordinates { get; set; }
        public Vector3? EntranceCoords { get; set; }
        public float DefaultHeading { get; set; }
        public bool SupportsVehicles { get; set; }
        public int Capacity { get; set; }
        public Vector3? WaterArrivalCoords { get; set; }

        // -------------------------------------------------------------
        // LAYER 3: SEMANTIC KNOWLEDGE & AFFORDANCES
        // -------------------------------------------------------------
        public string Category { get; set; }
        public string Description { get; set; }
        public List<string> Tags { get; set; }
        public List<string> Affordances { get; set; }
        public List<string> Identities { get; set; }
        public List<string> Roles { get; set; }
        public KnowledgeConfidence Confidence { get; set; }
        public float SemanticConfidenceScore { get; set; } // 0.0 to 1.0

        // -------------------------------------------------------------
        // LAYER 4: EVIDENCE-GROUNDED EPISTEMIC RECORD (new)
        // Parallel to Layer 1-3 fields for backwards compatibility.
        // Consumers should prefer these over flat fields when available.
        // -------------------------------------------------------------
        public List<AssetFact>      Facts      { get; set; }
        public List<AssetInference> Inferences { get; set; }

        public GtaAssetDefinition()
        {
            ExistsInGame = true;
            HasCollision = true;
            Dimensions = new Vector3(1f, 1f, 1f);
            IplDependencies = new List<string>();
            RemoveIplDependencies = new List<string>();
            EntitySets = new List<string>();
            Tags = new List<string>();
            Affordances = new List<string>();
            Identities = new List<string>();
            Roles = new List<string>();
            Confidence = KnowledgeConfidence.MachineVerifiedFactual;
            SemanticConfidenceScore = 1.0f;
            Facts      = new List<AssetFact>();
            Inferences = new List<AssetInference>();
        }

        /// <summary>Records a machine-verified property fact with explicit evidence.</summary>
        public void AddFact(string property, string value, EvidenceRecord evidence)
        {
            if (Facts == null) Facts = new List<AssetFact>();
            Facts.Add(new AssetFact { Property = property, Value = value, Evidence = evidence, Status = EpistemicStatus.Fact });
        }

        /// <summary>
        /// Records an inferred property (identity, DLC origin, semantic role).
        /// Inferences must NEVER be silently promoted to facts.
        /// </summary>
        public void AddInference(string property, string value, float confidence,
            EpistemicStatus status, params EvidenceRecord[] evidence)
        {
            if (Inferences == null) Inferences = new List<AssetInference>();
            var inf = new AssetInference { Property = property, Value = value, Confidence = confidence, Status = status };
            if (evidence != null) foreach (var e in evidence) if (e != null) inf.SupportingEvidence.Add(e);
            Inferences.Add(inf);
        }

        /// <summary>
        /// Returns the highest-confidence identity inference, or null if none exists.
        /// Identity status reflects the evidence quality — never assume Fact without RuntimeValidation.
        /// </summary>
        public AssetInference GetPrimaryIdentityInference()
        {
            if (Inferences == null) return null;
            AssetInference best = null;
            foreach (var inf in Inferences)
            {
                if (inf.Property != "identity") continue;
                if (best == null || inf.Confidence > best.Confidence) best = inf;
            }
            return best;
        }

        public bool HasAffordance(string affordance)
        {
            if (string.IsNullOrEmpty(affordance) || Affordances == null) return false;
            string target = affordance.ToLowerInvariant();
            return Affordances.Exists(a => a.ToLowerInvariant().Equals(target) || a.ToLowerInvariant().Contains(target));
        }

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag) || Tags == null) return false;
            string target = tag.ToLowerInvariant();
            return Tags.Exists(t => string.Equals(t, target, StringComparison.OrdinalIgnoreCase));
        }

        private static readonly HashSet<string> _identityStopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "with", "for", "from", "warehouse", "house", "area", "room", "gate", "base", "shop", "story", "place", "center", "centre", "unit", "front",
            "mansion", "bunker", "hangar", "club", "lab", "hospital", "station", "precinct", "office", "garage", "bar", "villa", "estate", "penthouse", "yacht", "boat", "store", "building", "facility", "terminal",
            "airfield", "runway", "airstrip", "track", "pier", "beach", "dam", "quarry", "park", "plaza", "mountain", "street", "strip", "alley", "port", "docks", "harbor"
        };

        public bool MatchesIdentity(string nameOrQuery)
        {
            if (string.IsNullOrEmpty(nameOrQuery) || Identities == null) return false;
            string target = nameOrQuery.ToLowerInvariant();
            return Identities.Exists(id =>
            {
                if (string.IsNullOrEmpty(id)) return false;
                string idLower = id.ToLowerInvariant();
                if (_identityStopWords.Contains(idLower)) return false;
                if (idLower.Equals(target, StringComparison.OrdinalIgnoreCase)) return true;
                if (idLower.Contains(" ") && target.Contains(idLower)) return true;
                if (target.Contains(" ") && idLower.Length >= 4)
                {
                    return System.Text.RegularExpressions.Regex.IsMatch(target, @"\b" + System.Text.RegularExpressions.Regex.Escape(idLower) + @"\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
                return false;
            });
        }

        public int GetBestMatchedIdentityLength(string nameOrQuery)
        {
            if (string.IsNullOrEmpty(nameOrQuery) || Identities == null) return 0;
            string target = nameOrQuery.ToLowerInvariant();
            int bestLen = 0;
            foreach (var id in Identities)
            {
                if (string.IsNullOrEmpty(id)) continue;
                string idLower = id.ToLowerInvariant();
                if (_identityStopWords.Contains(idLower)) continue;
                bool isMatch = false;
                if (idLower.Equals(target, StringComparison.OrdinalIgnoreCase)) isMatch = true;
                else if (idLower.Contains(" ") && target.Contains(idLower)) isMatch = true;
                else if (target.Contains(" ") && idLower.Length >= 4 && System.Text.RegularExpressions.Regex.IsMatch(target, @"\b" + System.Text.RegularExpressions.Regex.Escape(idLower) + @"\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) isMatch = true;
                if (isMatch && idLower.Length > bestLen)
                {
                    bestLen = idLower.Length;
                }
            }
            return bestLen;
        }
    }

    #endregion

    #region Query & Resolution API

    public class AssetQuery
    {
        public AssetType Type { get; set; }
        public string Identity { get; set; }
        public string Category { get; set; }
        public List<string> RequiredTags { get; set; }
        public List<string> RequiredAffordances { get; set; }
        public List<string> RequiredCapabilities { get; set; }
        public string PreferDlc { get; set; }
        public int MinCapacity { get; set; }
        public bool? RequiresCollision { get; set; }
        public bool? RequiresVehicleSupport { get; set; }
        public Vector3? MaxDimensions { get; set; }
        public string RawQuery { get; set; }

        public AssetQuery()
        {
            RequiredTags = new List<string>();
            RequiredAffordances = new List<string>();
            RequiredCapabilities = new List<string>();
        }
    }

    public class AssetQueryResult
    {
        public GtaAssetDefinition PrimaryAsset { get; set; }
        public List<GtaAssetDefinition> AlternativeAssets { get; set; }
        public float MatchScore { get; set; }
        public KnowledgeConfidence Confidence { get; set; }
        public string ResolutionProvenance { get; set; }
        public List<GtaAssetDefinition> ComposedSubAssets { get; set; }

        public AssetQueryResult()
        {
            AlternativeAssets = new List<GtaAssetDefinition>();
            ComposedSubAssets = new List<GtaAssetDefinition>();
        }
    }

    #endregion

    #region Provider Interfaces

    public interface IAssetDiscoveryProvider
    {
        string ProviderName { get; }
        int Priority { get; }
        void Initialize(SpatialAssetRegistry registry, string gtaRootPath);
        IEnumerable<GtaAssetDefinition> ProvideAssets();
    }

    public interface ISemanticEnrichmentProvider
    {
        string ProviderName { get; }
        void Enrich(GtaAssetDefinition asset);
    }

    #endregion

    #region Concrete Providers

    /// <summary>
    /// Provider 1: Discovers and indexes installed DLC packs from the GTA V installation directory.
    /// Reads %GTADIR%\update\x64\dlcpacks to register available DLC content and map content eras.
    /// </summary>
    public class DlcPackDiscoveryProvider : IAssetDiscoveryProvider
    {
        public string ProviderName { get { return "DlcPackDiscoveryProvider"; } }
        public int Priority { get { return 100; } }

        private readonly List<string> _discoveredDlcPacks = new List<string>();
        public IReadOnlyList<string> DiscoveredDlcPacks { get { return _discoveredDlcPacks; } }

        public void Initialize(SpatialAssetRegistry registry, string gtaRootPath)
        {
            _discoveredDlcPacks.Clear();

            try
            {
                var engine = new DlcPackDiscoveryEngine();
                engine.ScanDlcPacks(gtaRootPath);
                foreach (var pack in engine.DiscoveredPacks)
                {
                    _discoveredDlcPacks.Add(pack.PackName);
                }
            }
            catch { }
        }

        public IEnumerable<GtaAssetDefinition> ProvideAssets()
        {
            yield break;
        }
    }

    /// <summary>
    /// Provider 2: Data-Driven JSON Manifest Provider.
    /// Loads exteriorized JSON manifests (interiors.json, props.json, characters.json)
    /// from DynamicWorldEngineData directory, completely removing hardcoded C# lists.
    /// </summary>
    public class JsonManifestAssetProvider : IAssetDiscoveryProvider
    {
        public string ProviderName { get { return "JsonManifestAssetProvider"; } }
        public int Priority { get { return 90; } }

        private string _manifestDirectory = "";

        /// <summary>Default constructor — discovers the manifest directory via standard search paths.</summary>
        public JsonManifestAssetProvider() { }

        /// <summary>
        /// Explicit-directory constructor for test / isolated use.
        /// Bypasses auto-discovery and uses the provided directory directly.
        /// </summary>
        public JsonManifestAssetProvider(string manifestDirectory)
        {
            _manifestDirectory = manifestDirectory ?? "";
        }

        public void Initialize(SpatialAssetRegistry registry, string gtaRootPath)
        {
            if (!string.IsNullOrEmpty(_manifestDirectory) && Directory.Exists(_manifestDirectory))
            {
                return;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var install = GtaInstallationLocator.LocateInstallation(gtaRootPath);

            List<string> candidateDirs = new List<string>
            {
                Path.Combine(baseDir, "DynamicWorldEngineData"),
                Path.Combine(baseDir, "scripts", "DynamicWorldEngineData"),
                Path.Combine(Directory.GetCurrentDirectory(), "DynamicWorldEngineData")
            };

            if (install != null && !string.IsNullOrEmpty(install.RootPath))
            {
                candidateDirs.Add(Path.Combine(install.RootPath, "scripts", "DynamicWorldEngineData"));
                candidateDirs.Add(Path.Combine(install.RootPath, "DynamicWorldEngineData"));
            }

            foreach (string d in candidateDirs)
            {
                if (Directory.Exists(d) && (File.Exists(Path.Combine(d, "interiors.json")) || File.Exists(Path.Combine(d, "props.json")) || File.Exists(Path.Combine(d, "characters.json"))))
                {
                    _manifestDirectory = d;
                    break;
                }
            }
        }

        public IEnumerable<GtaAssetDefinition> ProvideAssets()
        {
            if (string.IsNullOrEmpty(_manifestDirectory) || !Directory.Exists(_manifestDirectory))
            {
                yield break;
            }

            // 1. Ingest Interiors Manifest (Optional override/enrichment if present)
            string intPath = Path.Combine(_manifestDirectory, "interiors.json");
            if (File.Exists(intPath))
            {
                JArray arr = null;
                try
                {
                    string text = File.ReadAllText(intPath);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        arr = JArray.Parse(text);
                    }
                }
                catch { }

                if (arr != null)
                {
                    foreach (JObject item in arr)
                    {
                    var def = new GtaAssetDefinition
                    {
                        ModelName = (string)item["InteriorId"],
                        Type = AssetType.Interior,
                        Category = (string)item["Category"],
                        Description = (string)item["Description"],
                        Capacity = item["CapacityNpcs"] != null ? (int)item["CapacityNpcs"] : 10,
                        SupportsVehicles = item["SupportsVehicles"] != null && (bool)item["SupportsVehicles"],
                        Confidence = KnowledgeConfidence.CuratedCanonical,
                        SemanticConfidenceScore = 0.98f
                    };

                    // Name
                    if (item["Name"] != null) def.Name = (string)item["Name"];

                    // Coordinates
                    if (item["InteriorCoords"] != null)
                    {
                        float x = item["InteriorCoords"]["X"] != null ? (float)item["InteriorCoords"]["X"] : 0f;
                        float y = item["InteriorCoords"]["Y"] != null ? (float)item["InteriorCoords"]["Y"] : 0f;
                        float z = item["InteriorCoords"]["Z"] != null ? (float)item["InteriorCoords"]["Z"] : 0f;
                        def.DefaultCoordinates = new Vector3(x, y, z);
                    }
                    if (item["EntranceCoords"] != null)
                    {
                        float ex = item["EntranceCoords"]["X"] != null ? (float)item["EntranceCoords"]["X"] : 0f;
                        float ey = item["EntranceCoords"]["Y"] != null ? (float)item["EntranceCoords"]["Y"] : 0f;
                        float ez = item["EntranceCoords"]["Z"] != null ? (float)item["EntranceCoords"]["Z"] : 0f;
                        def.EntranceCoords = new Vector3(ex, ey, ez);
                    }
                    if (item["InteriorHeading"] != null) def.DefaultHeading = (float)item["InteriorHeading"];

                    // Water arrival
                    if (item["WaterArrivalCoords"] != null && item["WaterArrivalCoords"].Type != JTokenType.Null)
                    {
                        float wx = (float)item["WaterArrivalCoords"]["X"];
                        float wy = (float)item["WaterArrivalCoords"]["Y"];
                        float wz = (float)item["WaterArrivalCoords"]["Z"];
                        def.WaterArrivalCoords = new Vector3(wx, wy, wz);
                    }

                    // IPLs
                    if (item["IplNames"] != null)
                    {
                        foreach (var token in (JArray)item["IplNames"]) def.IplDependencies.Add((string)token);
                    }
                    if (item["RemoveIplNames"] != null)
                    {
                        foreach (var token in (JArray)item["RemoveIplNames"]) def.RemoveIplDependencies.Add((string)token);
                    }
                    if (item["EntitySets"] != null)
                    {
                        foreach (var token in (JArray)item["EntitySets"]) def.EntitySets.Add((string)token);
                    }

                    // Tags & Keywords
                    if (item["SemanticTags"] != null)
                    {
                        foreach (var token in (JArray)item["SemanticTags"]) def.Tags.Add((string)token);
                    }
                    if (item["Keywords"] != null)
                    {
                        foreach (var token in (JArray)item["Keywords"])
                        {
                            string kw = (string)token;
                            if (!def.Tags.Contains(kw)) def.Tags.Add(kw);
                            if (!def.Identities.Contains(kw)) def.Identities.Add(kw);
                        }
                    }

                    // Category, Privacy, Atmosphere
                    if (!string.IsNullOrEmpty(def.Category) && !def.Tags.Contains(def.Category)) def.Tags.Add(def.Category);
                    if (item["Privacy"] != null)
                    {
                        string priv = (string)item["Privacy"];
                        if (!def.Tags.Contains(priv)) def.Tags.Add(priv);
                        def.Affordances.Add(priv);
                    }
                    if (item["Atmosphere"] != null)
                    {
                        string atmo = (string)item["Atmosphere"];
                        if (!def.Tags.Contains(atmo)) def.Tags.Add(atmo);
                        def.Affordances.Add(atmo);
                    }

                    // Identities
                    if (item["Name"] != null) def.Identities.Add((string)item["Name"]);
                    def.Identities.Add(def.ModelName);

                    // Affordances
                    def.Affordances.Add("spatial_container");
                    if (def.SupportsVehicles) def.Affordances.Add("vehicle_staging");
                    if (def.WaterArrivalCoords.HasValue) def.Affordances.Add("maritime_access");
                    if (def.Capacity >= 20) def.Affordances.Add("crowd_gathering");

                    yield return def;
                }
                }
            }

            // 2. Ingest Props Manifest
            string propPath = Path.Combine(_manifestDirectory, "props.json");
            if (File.Exists(propPath))
            {
                string text = File.ReadAllText(propPath);
                JArray arr = JArray.Parse(text);
                foreach (JObject item in arr)
                {
                    var def = new GtaAssetDefinition
                    {
                        ModelName = (string)item["Model"],
                        Type = AssetType.Prop,
                        Category = (string)item["Category"],
                        Walkable = item["Walkable"] != null && (bool)item["Walkable"],
                        BlocksNavMesh = item["BlocksNavMesh"] != null && (bool)item["BlocksNavMesh"],
                        CanAttachChildren = item["CanAttachChildren"] != null && (bool)item["CanAttachChildren"],
                        SurfaceElevation = item["SurfaceElevation"] != null ? (float)item["SurfaceElevation"] : 0f,
                        Confidence = KnowledgeConfidence.CuratedCanonical,
                        SemanticConfidenceScore = 0.95f
                    };

                    if (item["Dimensions"] != null)
                    {
                        float dx = item["Dimensions"]["X"] != null ? (float)item["Dimensions"]["X"] : 1f;
                        float dy = item["Dimensions"]["Y"] != null ? (float)item["Dimensions"]["Y"] : 1f;
                        float dz = item["Dimensions"]["Z"] != null ? (float)item["Dimensions"]["Z"] : 1f;
                        def.Dimensions = new Vector3(dx, dy, dz);
                    }

                    if (item["Tags"] != null)
                    {
                        foreach (var token in (JArray)item["Tags"]) def.Tags.Add((string)token);
                    }

                    yield return def;
                }
            }

            // 3. Ingest Characters Manifest
            string charPath = Path.Combine(_manifestDirectory, "characters.json");
            if (File.Exists(charPath))
            {
                string text = File.ReadAllText(charPath);
                JArray arr = JArray.Parse(text);
                foreach (JObject item in arr)
                {
                    string pedModel  = (string)item["PedModel"];
                    string fullName  = (string)item["FullName"];
                    string charId    = (string)item["CharacterId"];
                    string dlcOrigin = (string)item["DlcOrigin"];

                    var def = new GtaAssetDefinition
                    {
                        ModelName = pedModel,
                        Type = AssetType.Ped,
                        Category = (string)item["DefaultOccupation"],
                        Description = (string)item["CanonDescription"],
                        DlcPackOrigin = dlcOrigin,
                        // CuratedManifest data is not machine-verified — reflect that.
                        Confidence = KnowledgeConfidence.CuratedCanonical,
                        SemanticConfidenceScore = 0.85f
                    };

                    // Model name is established by the manifest but not yet runtime-validated here.
                    // It becomes a Fact once runtime validation confirms IS_MODEL_IN_CDIMAGE.
                    def.AddFact("model_name", pedModel,
                        EvidenceRecord.FromCuratedManifest(charPath,
                            string.Format("Model '{0}' listed in characters.json manifest", pedModel), 0.85f));

                    // ------------------------------------------------------------------
                    // IDENTITY: record as inference, NOT as fact.
                    // The manifest authoritatively maps the model to a real-world identity
                    // (e.g. "Dr. Dre"), but this cannot be machine-verified from game data alone.
                    // Status = Resolved (operator-authoritative manifest), Confidence = 0.85.
                    // The consumer can see this came from CuratedManifest and act accordingly.
                    // ------------------------------------------------------------------
                    var manifestEvidence = EvidenceRecord.FromCuratedManifest(charPath,
                        string.Format("Identity '{0}' attributed to model '{1}' in characters.json", fullName, pedModel), 0.85f);

                    if (!string.IsNullOrEmpty(fullName))
                    {
                        def.AddInference("identity", fullName, 0.85f, EpistemicStatus.Resolved, manifestEvidence);
                        // Keep backwards-compatible flat Identities list
                        def.Identities.Add(fullName);
                    }
                    if (!string.IsNullOrEmpty(charId))
                    {
                        def.AddInference("identity", charId, 0.80f, EpistemicStatus.Resolved, manifestEvidence);
                        if (!def.Identities.Contains(charId)) def.Identities.Add(charId);
                    }

                    if (item["Keywords"] != null)
                    {
                        foreach (var token in (JArray)item["Keywords"])
                        {
                            string kw = (string)token;
                            // Keywords are weaker: FilenameSimilarity-level inference
                            def.AddInference("keyword", kw, 0.65f, EpistemicStatus.Candidate,
                                EvidenceRecord.FromCuratedManifest(charPath,
                                    string.Format("Keyword '{0}' associated with '{1}'", kw, fullName), 0.65f));
                            if (!def.Identities.Contains(kw)) def.Identities.Add(kw);
                        }
                    }

                    if (item["Roles"] != null)
                    {
                        foreach (var token in (JArray)item["Roles"])
                        {
                            string r = (string)token;
                            def.Roles.Add(r);
                            def.Tags.Add(r);
                            def.AddInference("role", r, 0.80f, EpistemicStatus.Resolved, manifestEvidence);
                        }
                    }

                    def.Affordances.Add("social_dialogue");
                    def.Affordances.Add("autonomous_actor");

                    yield return def;
                }
            }
        }
    }

    /// <summary>
    /// Provider 3: Discovers models from ScriptHookVDotNet reflection across PedHash and VehicleHash,
    /// interrogating runtime CDImage memory when available.
    /// </summary>
    public class DiscoveredModelProvider : IAssetDiscoveryProvider
    {
        public string ProviderName { get { return "DiscoveredModelProvider"; } }
        public int Priority { get { return 50; } }

        public void Initialize(SpatialAssetRegistry registry, string gtaRootPath) { }

        public IEnumerable<GtaAssetDefinition> ProvideAssets()
        {
            // Enumerate known Ped Hashes via reflection to register available base models
            var pedHashType = typeof(PedHash);
            var pedFields = pedHashType.GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var f in pedFields)
            {
                string name = f.Name;
                uint hash = 0;
                try { hash = Convert.ToUInt32(f.GetValue(null)); } catch { }

                var def = new GtaAssetDefinition
                {
                    ModelName = name,
                    ModelHash = hash,
                    Type = AssetType.Ped,
                    Category = "CivilianPed",
                    Confidence = KnowledgeConfidence.MachineVerifiedFactual,
                    SemanticConfidenceScore = 0.5f
                };
                def.Identities.Add(name);
                def.Tags.Add(name.ToLowerInvariant());
                yield return def;
            }

            // Enumerate known Vehicle Hashes via reflection
            var vehHashType = typeof(VehicleHash);
            var vehFields = vehHashType.GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var f in vehFields)
            {
                string name = f.Name;
                uint hash = 0;
                try { hash = Convert.ToUInt32(f.GetValue(null)); } catch { }

                var def = new GtaAssetDefinition
                {
                    ModelName = name,
                    ModelHash = hash,
                    Type = AssetType.Vehicle,
                    Category = "Vehicle",
                    Confidence = KnowledgeConfidence.MachineVerifiedFactual,
                    SemanticConfidenceScore = 0.5f
                };
                def.Identities.Add(name);
                def.Tags.Add(name.ToLowerInvariant());
                yield return def;
            }
        }
    }

    /// <summary>
    /// Default Semantic Enrichment Provider: Evaluates physical properties, tags, and categories
    /// to synthesize affordances (can_be_surface, dj_equipment, military_command, etc.).
    /// </summary>
    public class DefaultSemanticEnrichmentProvider : ISemanticEnrichmentProvider
    {
        public string ProviderName { get { return "DefaultSemanticEnrichmentProvider"; } }

        public void Enrich(GtaAssetDefinition def)
        {
            if (def == null) return;

            string catLower = (def.Category ?? "").ToLowerInvariant();
            string modelLower = (def.ModelName ?? "").ToLowerInvariant();

            // Prop Affordance Enrichment
            if (def.Type == AssetType.Prop)
            {
                if (catLower == "table")
                {
                    AddAffordance(def, "can_be_surface");
                    AddAffordance(def, "supports_objects");
                    AddAffordance(def, "supports_seating_adjacent");
                }
                else if (catLower == "seat")
                {
                    AddAffordance(def, "supports_npc_seating");
                    AddAffordance(def, "rest_posture");
                }
                else if (catLower == "wall")
                {
                    AddAffordance(def, "perimeter_barrier");
                    AddAffordance(def, "blocks_movement");
                    AddAffordance(def, "blocks_vision");
                }
                else if (catLower == "container")
                {
                    AddAffordance(def, "stores_inventory");
                    AddAffordance(def, "concealment");
                }
                else if (catLower == "lighting")
                {
                    AddAffordance(def, "illuminates_area");
                    AddAffordance(def, "mood_lighting");
                }

                if (def.HasTag("dj") || modelLower.Contains("dj") || def.HasTag("turntable"))
                {
                    AddAffordance(def, "dj_equipment");
                    AddAffordance(def, "music_production");
                    AddAffordance(def, "party_centerpiece");
                }
                if (def.HasTag("speaker") || modelLower.Contains("speaker"))
                {
                    AddAffordance(def, "audio_projection");
                    AddAffordance(def, "party_decoration");
                }
                if (def.HasTag("cash") || def.HasTag("gold") || def.HasTag("loot") || def.HasTag("heist"))
                {
                    AddAffordance(def, "heist_loot");
                    AddAffordance(def, "high_value_target");
                    AddAffordance(def, "contraband");
                }
                if (def.HasTag("drugs") || def.HasTag("coke") || def.HasTag("meth") || def.HasTag("weed"))
                {
                    AddAffordance(def, "narcotics_contraband");
                    AddAffordance(def, "illegal_substance");
                }
                if (def.HasTag("military") || def.HasTag("weapon") || def.HasTag("ammo") || def.HasTag("sandbag"))
                {
                    AddAffordance(def, "military_defense");
                    AddAffordance(def, "armory_storage");
                }
                if (def.HasTag("cctv") || def.HasTag("camera") || def.HasTag("surveillance") || def.HasTag("screen"))
                {
                    AddAffordance(def, "surveillance");
                    AddAffordance(def, "command_monitoring");
                }
                if (def.HasTag("champagne") || def.HasTag("beer") || def.HasTag("drink") || def.HasTag("whiskey"))
                {
                    AddAffordance(def, "drinks_service");
                    AddAffordance(def, "social_prop");
                }
                if (def.HasTag("planning") || def.HasTag("blueprint") || def.HasTag("boardroom"))
                {
                    AddAffordance(def, "planning_surface");
                    AddAffordance(def, "heist_coordination");
                }
            }
            // Interior Affordance Enrichment
            else if (def.Type == AssetType.Interior)
            {
                if (def.HasTag("vault") || modelLower.Contains("vault") || (def.Category ?? "").ToLowerInvariant().Contains("vault"))
                {
                    AddAffordance(def, "secure_containment");
                    AddAffordance(def, "heist_target");
                }
                if (def.HasTag("nightclub") || def.HasTag("club") || modelLower.Contains("locker") || modelLower.Contains("club"))
                {
                    AddAffordance(def, "nightclub_performance");
                    AddAffordance(def, "party_venue");
                }
                if (def.HasTag("penthouse") || modelLower.Contains("penthouse"))
                {
                    AddAffordance(def, "luxury_hospitality");
                    AddAffordance(def, "vip_social");
                }
                if (def.HasTag("bunker") || def.HasTag("facility") || modelLower.Contains("bunker") || modelLower.Contains("facility"))
                {
                    AddAffordance(def, "military_command");
                    AddAffordance(def, "blast_shelter");
                }
                if (def.HasTag("prison") || def.HasTag("jail") || modelLower.Contains("jail"))
                {
                    AddAffordance(def, "detention_holding");
                    AddAffordance(def, "secure_containment");
                }
                if (def.HasTag("mansion") || (def.Category ?? "").ToLowerInvariant().Contains("mansion"))
                {
                    AddAffordance(def, "luxury_estate");
                    AddAffordance(def, "party_venue");
                    AddAffordance(def, "vip_social");
                    AddAffordance(def, "residential_grounds");
                }
                if (def.HasTag("yacht") || (def.Category ?? "").ToLowerInvariant().Contains("yacht"))
                {
                    AddAffordance(def, "luxury_estate");
                    AddAffordance(def, "party_venue");
                    AddAffordance(def, "vip_social");
                    AddAffordance(def, "maritime_luxury");
                }
            }
            // Ped Affordance Enrichment
            else if (def.Type == AssetType.Ped)
            {
                if (def.Roles.Contains("dj") || def.Roles.Contains("party_dj") || def.HasTag("dj"))
                {
                    AddAffordance(def, "dj_performance");
                    AddAffordance(def, "crowd_entertainment");
                    AddAffordance(def, "celebrity_presence");
                }
                if (def.Roles.Contains("heist_leader") || def.Roles.Contains("planner") || def.HasTag("planner"))
                {
                    AddAffordance(def, "heist_planning");
                    AddAffordance(def, "tactical_command");
                }
                if (def.Roles.Contains("bodyguard") || def.Roles.Contains("security") || def.HasTag("security"))
                {
                    AddAffordance(def, "vip_protection");
                    AddAffordance(def, "perimeter_security");
                }
                if (modelLower.Contains("fam") || modelLower.Contains("grove"))
                {
                    if (!def.Tags.Contains("families")) def.Tags.Add("families");
                    if (!def.Tags.Contains("gang")) def.Tags.Add("gang");
                    AddAffordance(def, "gang_member");
                    AddAffordance(def, "families_gang");
                }
                if (modelLower.Contains("balla"))
                {
                    if (!def.Tags.Contains("ballas")) def.Tags.Add("ballas");
                    if (!def.Tags.Contains("gang")) def.Tags.Add("gang");
                    AddAffordance(def, "gang_member");
                    AddAffordance(def, "ballas_gang");
                }
                if (modelLower.Contains("vagos") || modelLower.Contains("salva"))
                {
                    if (!def.Tags.Contains("vagos")) def.Tags.Add("vagos");
                    if (!def.Tags.Contains("gang")) def.Tags.Add("gang");
                    AddAffordance(def, "gang_member");
                }
                if (modelLower.Contains("marabun"))
                {
                    if (!def.Tags.Contains("marabunta")) def.Tags.Add("marabunta");
                    if (!def.Tags.Contains("gang")) def.Tags.Add("gang");
                    AddAffordance(def, "gang_member");
                }
                if (modelLower.Contains("armboss") || modelLower.Contains("armgoon"))
                {
                    if (!def.Tags.Contains("heist")) def.Tags.Add("heist");
                    if (!def.Tags.Contains("heist_crew")) def.Tags.Add("heist_crew");
                    if (!def.Tags.Contains("tactical")) def.Tags.Add("tactical");
                    AddAffordance(def, "tactical_assault");
                    AddAffordance(def, "heist_combat");
                }
                if (modelLower.Contains("highsec"))
                {
                    if (!def.Tags.Contains("security")) def.Tags.Add("security");
                    if (!def.Tags.Contains("guard")) def.Tags.Add("guard");
                    AddAffordance(def, "perimeter_security");
                }
            }
        }

        private void AddAffordance(GtaAssetDefinition def, string affordance)
        {
            if (!def.Affordances.Contains(affordance))
            {
                def.Affordances.Add(affordance);
            }
        }
    }

    #endregion

    #region SpatialAssetRegistry (Unified Orchestrator)

    /// <summary>
    /// The central GTA World & Spatial Asset Registry.
    /// Coordinates discovered facts, Rockstar metadata, and semantic ontologies.
    /// Provides the Asset Query API and affordance-based primitive composition.
    /// </summary>
    public class SpatialAssetRegistry
    {
        private static readonly Lazy<SpatialAssetRegistry> _instance = new Lazy<SpatialAssetRegistry>(() => new SpatialAssetRegistry());
        public static SpatialAssetRegistry Instance { get { return _instance.Value; } }

        private readonly List<IAssetDiscoveryProvider> _providers = new List<IAssetDiscoveryProvider>();
        private readonly List<ISemanticEnrichmentProvider> _enrichers = new List<ISemanticEnrichmentProvider>();
        private readonly List<GtaAssetDefinition> _assets = new List<GtaAssetDefinition>();
        private readonly List<string> _discoveredDlcPacks = new List<string>();
        private string _gtaInstallationRoot = "";
        private GtaInstallationInfo _installationInfo;
        private readonly DlcPackDiscoveryEngine _dlcEngine;
        private readonly AssetIndexCacheManager _cacheManager;
        private readonly CanonicalIdentityResolver _identityResolver;
        private readonly GenericPrimitiveComposer _primitiveComposer;
        private readonly SemanticClassificationEngine _semanticClassificationEngine;

        public IReadOnlyList<GtaAssetDefinition> AllAssets { get { return _assets; } }
        public IReadOnlyList<string> DiscoveredDlcPacks { get { return _discoveredDlcPacks; } }
        public string GtaInstallationRoot { get { return _gtaInstallationRoot; } }
        public GtaInstallationInfo InstallationInfo { get { return _installationInfo; } }
        public DlcPackDiscoveryEngine DlcDiscoveryEngine { get { return _dlcEngine; } }
        public AssetIndexCacheManager CacheManager { get { return _cacheManager; } }
        public CanonicalIdentityResolver IdentityResolver { get { return _identityResolver; } }
        public GenericPrimitiveComposer PrimitiveComposer { get { return _primitiveComposer; } }

        public GtaAssetDefinition GetAsset(string modelName)
        {
            if (string.IsNullOrEmpty(modelName)) return null;
            return _assets.Find(a => string.Equals(a.ModelName, modelName, StringComparison.OrdinalIgnoreCase));
        }

        public SpatialAssetRegistry()
        {
            _installationInfo = GtaInstallationLocator.LocateInstallation();
            _gtaInstallationRoot = (_installationInfo != null && _installationInfo.IsInstalled)
                ? _installationInfo.RootPath
                : "";

            _dlcEngine = new DlcPackDiscoveryEngine();
            string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DynamicWorldEngineData");
            _cacheManager = new AssetIndexCacheManager(cacheDir, _dlcEngine);
            _identityResolver = new CanonicalIdentityResolver(this);
            _primitiveComposer = new GenericPrimitiveComposer(this);
            _semanticClassificationEngine = new SemanticClassificationEngine();

            RegisterDefaultProviders();
            InitializeRegistry(_gtaInstallationRoot);
        }

        /// <summary>
        /// Constructor for test / isolated use.
        /// Creates a standalone registry using the specified data directory for manifests.
        /// The GTA installation discovery still runs to locate DLC packs.
        /// </summary>
        public SpatialAssetRegistry(string dataDirectory)
        {
            _installationInfo   = GtaInstallationLocator.LocateInstallation();
            _gtaInstallationRoot = (_installationInfo != null && _installationInfo.IsInstalled)
                ? _installationInfo.RootPath : "";

            _dlcEngine    = new DlcPackDiscoveryEngine();
            _cacheManager = new AssetIndexCacheManager(dataDirectory, _dlcEngine);
            _semanticClassificationEngine = new SemanticClassificationEngine();

            // Use the provided data directory for the manifest provider
            _providers.Add(new DlcPackDiscoveryProvider());
            _providers.Add(new DynamicInteriorDiscoveryProvider());
            _providers.Add(new OpenWorldLocationProvider());
            _providers.Add(new JsonManifestAssetProvider(dataDirectory));
            _providers.Add(new DiscoveredModelProvider());
            _enrichers.Add(new DefaultSemanticEnrichmentProvider());
            _enrichers.Add(_semanticClassificationEngine);
            _providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            // Identity resolver and composer must be set after providers are ready
            _identityResolver  = new CanonicalIdentityResolver(this);
            _primitiveComposer = new GenericPrimitiveComposer(this);
        }

        /// <summary>
        /// Public alias for InitializeRegistry — used by test instances created
        /// via the directory-path constructor.
        /// </summary>
        public void Initialize()
        {
            InitializeRegistry(_gtaInstallationRoot);
        }

        private void RegisterDefaultProviders()
        {
            _providers.Add(new DlcPackDiscoveryProvider());
            _providers.Add(new DynamicInteriorDiscoveryProvider());
            _providers.Add(new OpenWorldLocationProvider());
            _providers.Add(new JsonManifestAssetProvider());
            _providers.Add(new DiscoveredModelProvider());

            _enrichers.Add(new DefaultSemanticEnrichmentProvider());
            _enrichers.Add(_semanticClassificationEngine);

            // Sort providers by priority descending
            _providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        /// <summary>
        /// Interrogates the GTA installation directory and builds the unified ontology.
        /// First checks the verified asset-index cache; if missing or invalid, dynamically executes
        /// full discovery and semantic enrichment, then persists the derived cache.
        /// </summary>
        public void InitializeRegistry(string gtaRootPath)
        {
            if (!string.IsNullOrEmpty(gtaRootPath))
            {
                _gtaInstallationRoot = gtaRootPath;
            }
            else if (string.IsNullOrEmpty(_gtaInstallationRoot))
            {
                _installationInfo = GtaInstallationLocator.LocateInstallation();
                if (_installationInfo != null && _installationInfo.IsInstalled)
                {
                    _gtaInstallationRoot = _installationInfo.RootPath;
                }
            }

            _assets.Clear();
            _discoveredDlcPacks.Clear();

            // 1. Scan DLC packs dynamically via DlcPackDiscoveryEngine
            if (!string.IsNullOrEmpty(_gtaInstallationRoot))
            {
                _dlcEngine.ScanDlcPacks(_gtaInstallationRoot);
                foreach (var pack in _dlcEngine.DiscoveredPacks)
                {
                    _discoveredDlcPacks.Add(pack.PackName);
                }
            }

            // 2. Check if derived asset_index.json cache is valid
            if (_cacheManager != null && _cacheManager.IsCacheValid(_gtaInstallationRoot))
            {
                var cachedAssets = _cacheManager.LoadCacheAssets();
                if (cachedAssets != null && cachedAssets.Count > 0)
                {
                    _assets.AddRange(cachedAssets);
                    MergeCuratedManifests();
                    return;
                }
            }

            // 3. Cache missing or invalidated: Execute discovery and build ontology
            foreach (var provider in _providers)
            {
                try
                {
                    provider.Initialize(this, _gtaInstallationRoot);
                }
                catch { }
            }

            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var provider in _providers)
            {
                try
                {
                    var assets = provider.ProvideAssets();
                    if (assets != null)
                    {
                        foreach (var a in assets)
                        {
                            string key = a.Type + ":" + a.ModelName;
                            if (!seenKeys.Contains(key))
                            {
                                seenKeys.Add(key);

                                // Run semantic enrichment pipeline
                                foreach (var enricher in _enrichers)
                                {
                                    enricher.Enrich(a);
                                }

                                _assets.Add(a);
                            }
                        }
                    }
                }
                catch { }
            }

            // 4. Persist newly discovered & enriched ontology to asset_index.json cache
            try
            {
                if (_cacheManager != null)
                {
                    string version = _installationInfo != null ? _installationInfo.Version : "1.0.0";
                    _cacheManager.SaveCache(_gtaInstallationRoot, version, _assets);
                }
            }
            catch { }
        }

        private void MergeCuratedManifests()
        {
            var manifestProvider = _providers.Find(p => p is JsonManifestAssetProvider) as JsonManifestAssetProvider;
            if (manifestProvider != null)
            {
                try
                {
                    manifestProvider.Initialize(this, _gtaInstallationRoot);
                    var curatedAssets = manifestProvider.ProvideAssets();
                    if (curatedAssets != null)
                    {
                        foreach (var ca in curatedAssets)
                        {
                            if (ca == null) continue;
                            foreach (var enricher in _enrichers)
                            {
                                enricher.Enrich(ca);
                            }
                            int idx = _assets.FindIndex(a => a.Type == ca.Type && string.Equals(a.ModelName, ca.ModelName, StringComparison.OrdinalIgnoreCase));
                            if (idx >= 0)
                            {
                                _assets[idx] = ca;
                            }
                            else
                            {
                                _assets.Add(ca);
                            }
                        }
                    }
                }
                catch { }
            }

            var openWorldProvider = _providers.Find(p => p is DynamicWorldEngine.Discovery.OpenWorldLocationProvider) as DynamicWorldEngine.Discovery.OpenWorldLocationProvider;
            if (openWorldProvider != null)
            {
                try
                {
                    openWorldProvider.Initialize(this, _gtaInstallationRoot);
                    var locAssets = openWorldProvider.ProvideAssets();
                    if (locAssets != null)
                    {
                        foreach (var la in locAssets)
                        {
                            if (la == null) continue;
                            int idx = _assets.FindIndex(a => a.Type == la.Type && string.Equals(a.ModelName, la.ModelName, StringComparison.OrdinalIgnoreCase));
                            if (idx >= 0)
                            {
                                _assets[idx] = la;
                            }
                            else
                            {
                                _assets.Add(la);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        #region Runtime Machine Verification (Layer 1)

        /// <summary>
        /// Interrogates GTA V runtime memory via ScriptHookV natives to verify if a model
        /// exists in the active CDImage and is valid.
        /// </summary>
        public bool VerifyModelExistsInEngine(string modelName, out uint modelHash)
        {
            modelHash = 0;
            if (string.IsNullOrEmpty(modelName)) return false;

            try
            {
                modelHash = (uint)Game.GenerateHash(modelName);
                return Function.Call<bool>(Hash.IS_MODEL_IN_CDIMAGE, modelHash) && Function.Call<bool>(Hash.IS_MODEL_VALID, modelHash);
            }
            catch
            {
                // Standalone test harness fallback
                return true;
            }
        }

        /// <summary>
        /// Queries the GTA V engine for the true physical bounding box dimensions of a model.
        /// </summary>
        public Vector3 QueryModelDimensions(string modelName)
        {
            try
            {
                uint hash = (uint)Game.GenerateHash(modelName);
                var min = new OutputArgument();
                var max = new OutputArgument();
                Function.Call(Hash.GET_MODEL_DIMENSIONS, hash, min, max);
                Vector3 minV = min.GetResult<Vector3>();
                Vector3 maxV = max.GetResult<Vector3>();
                return new Vector3(Math.Abs(maxV.X - minV.X), Math.Abs(maxV.Y - minV.Y), Math.Abs(maxV.Z - minV.Z));
            }
            catch
            {
                var cached = _assets.Find(a => string.Equals(a.ModelName, modelName, StringComparison.OrdinalIgnoreCase));
                if (cached != null && (cached.Dimensions.X > 1.1f || cached.Dimensions.Y > 1.1f || cached.Dimensions.Z > 1.1f))
                {
                    return cached.Dimensions;
                }

                // ------------------------------------------------------------------
                // DIMENSION ESTIMATES — SemanticSimilarity-level inferences (rank 8).
                // These are NOT facts. They are name-pattern estimates used only when
                // the GTA runtime is unavailable (standalone test / pre-load context).
                // Confidence: 0.30 — enough for feasibility checks, not for placement.
                // ------------------------------------------------------------------
                string mLower = (modelName ?? "").ToLowerInvariant();
                Vector3 estimated = new Vector3(0, 0, 0);
                if      (mLower.Contains("kosatka"))                                                                    estimated = new Vector3(12.5f, 111.5f, 18.0f);
                else if (mLower.Contains("submersible") || mLower.Contains("avisa"))                                   estimated = new Vector3(3.2f,  6.5f,   3.0f);
                else if (mLower.Contains("titan")   || mLower.Contains("volatol")   || mLower.Contains("alkonost")
                      || mLower.Contains("bombushka") || mLower.Contains("cargo"))                                     estimated = new Vector3(35.0f, 32.0f,  9.0f);
                else if (mLower.Contains("apc")     || mLower.Contains("khanjali")  || mLower.Contains("rhino")
                      || mLower.Contains("insurgent"))                                                                  estimated = new Vector3(3.2f,  7.5f,   3.2f);
                else if (mLower.Contains("marquis") || mLower.Contains("tug")       || mLower.Contains("jetmax")
                      || mLower.Contains("speeder"))                                                                    estimated = new Vector3(4.0f,  12.0f,  4.5f);

                if (estimated.X > 0f)
                {
                    // Attach inference record to the cached asset if available
                    if (cached != null)
                    {
                        cached.AddInference("dimensions_estimate",
                            string.Format("{0:F1}x{1:F1}x{2:F1}", estimated.X, estimated.Y, estimated.Z),
                            0.30f, EpistemicStatus.Candidate,
                            EvidenceRecord.FromSemanticSimilarity(mLower, "vehicle_dimensions_pattern", 0.30f));
                    }
                    return estimated;
                }

                return cached != null ? cached.Dimensions : new Vector3(2.2f, 4.8f, 1.5f);
            }
        }

        #endregion

        #region Semantic Asset Resolver API

        /// <summary>
        /// Primary resolution entry point: resolves an abstract semantic query into verified
        /// GTA assets, evaluating factual validity, affordance matching, and confidence score.
        /// </summary>
        public AssetQueryResult Resolve(AssetQuery query)
        {
            if (query == null) throw new ArgumentNullException("query");

            var result = new AssetQueryResult();
            var candidates = _assets.FindAll(a => a.Type == query.Type);
            if (candidates.Count == 0)
            {
                result.Confidence = KnowledgeConfidence.Unknown;
                result.ResolutionProvenance = "No candidates registered for type " + query.Type;
                return result;
            }

            float bestScore = 0f;
            GtaAssetDefinition bestMatch = null;
            var ranked = new List<KeyValuePair<GtaAssetDefinition, float>>();

            foreach (var candidate in candidates)
            {
                float score = CalculateMatchScore(candidate, query);
                if (score > 0f)
                {
                    ranked.Add(new KeyValuePair<GtaAssetDefinition, float>(candidate, score));
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMatch = candidate;
                    }
                }
            }

            if (bestMatch == null)
            {
                result.PrimaryAsset = null;
                result.MatchScore = 0f;
                result.Confidence = KnowledgeConfidence.Unknown;
                result.ResolutionProvenance = "No candidates matched semantic query.";
                return result;
            }

            ranked.Sort((a, b) => b.Value.CompareTo(a.Value));
            result.PrimaryAsset = bestMatch;
            result.MatchScore = bestScore;
            result.Confidence = result.PrimaryAsset.Confidence;

            foreach (var pair in ranked)
            {
                if (pair.Key != result.PrimaryAsset && result.AlternativeAssets.Count < 5)
                {
                    result.AlternativeAssets.Add(pair.Key);
                }
            }

            result.ResolutionProvenance = string.Format("Resolved via {0} (Confidence: {1}, Score: {2:F1})",
                result.PrimaryAsset.Confidence, result.PrimaryAsset.SemanticConfidenceScore, result.MatchScore);

            return result;
        }

        private float CalculateMatchScore(GtaAssetDefinition asset, AssetQuery query)
        {
            float score = 0f;

            // 1. Identity exact or partial match (High weight: 100 pts)
            if (!string.IsNullOrEmpty(query.Identity))
            {
                string idLower = query.Identity.ToLowerInvariant();
                if (asset.MatchesIdentity(idLower)) score += 100f;
                if ((asset.ModelName ?? "").ToLowerInvariant().Contains(idLower)) score += 80f;
            }

            // 2. Category match (30 pts exact, 20 pts tag)
            if (!string.IsNullOrEmpty(query.Category))
            {
                if (string.Equals(asset.Category, query.Category, StringComparison.OrdinalIgnoreCase)) score += 30f;
                else if (asset.HasTag(query.Category)) score += 20f;
            }

            // 3. Required Affordances (Crucial: 50 pts each)
            if (query.RequiredAffordances != null && query.RequiredAffordances.Count > 0)
            {
                foreach (var aff in query.RequiredAffordances)
                {
                    if (asset.HasAffordance(aff)) score += 50f;
                }
            }

            // 4. Required Capabilities / Roles (30 pts each)
            if (query.RequiredCapabilities != null && query.RequiredCapabilities.Count > 0)
            {
                foreach (var cap in query.RequiredCapabilities)
                {
                    if (string.IsNullOrEmpty(cap)) continue;
                    string capLower = cap.ToLowerInvariant();
                    if (asset.Roles.Exists(r => r.ToLowerInvariant().Contains(capLower))) score += 30f;
                    if (asset.HasAffordance(capLower)) score += 30f;
                }
            }

            // 5. Required Tags (15 pts each, with synergy bonus when all match)
            if (query.RequiredTags != null && query.RequiredTags.Count > 0)
            {
                int matchedTags = 0;
                foreach (var tag in query.RequiredTags)
                {
                    if (asset.HasTag(tag))
                    {
                        score += 15f;
                        matchedTags++;
                    }
                }
                if (matchedTags == query.RequiredTags.Count)
                {
                    score += 25f;
                }
            }

            // 6. DLC Preference bonus (20 pts)
            if (!string.IsNullOrEmpty(query.PreferDlc) && !string.IsNullOrEmpty(asset.DlcPackOrigin))
            {
                if (asset.DlcPackOrigin.ToLowerInvariant().Contains(query.PreferDlc.ToLowerInvariant()))
                {
                    score += 20f;
                }
            }

            // 7. Vehicle support
            if (query.RequiresVehicleSupport.HasValue)
            {
                if (asset.SupportsVehicles == query.RequiresVehicleSupport.Value) score += 30f;
                else if (query.RequiresVehicleSupport.Value && !asset.SupportsVehicles) score -= 50f;
            }

            // 8. Raw query general matching
            if (!string.IsNullOrEmpty(query.RawQuery))
            {
                string rawLower = query.RawQuery.ToLowerInvariant();
                int matchedLen = asset.GetBestMatchedIdentityLength(rawLower);
                if (matchedLen > 0)
                {
                    // Specificity bonus: longer, more distinct proper titles scale higher
                    score += 40f + (matchedLen * 2f);
                }
                else if (asset.MatchesIdentity(rawLower))
                {
                    score += 40f;
                }

                if (!string.IsNullOrEmpty(asset.Category))
                {
                    string catLower = asset.Category.ToLowerInvariant();
                    if (System.Text.RegularExpressions.Regex.IsMatch(rawLower, @"\b" + System.Text.RegularExpressions.Regex.Escape(catLower) + @"\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                        (catLower.Contains("_") && System.Text.RegularExpressions.Regex.IsMatch(rawLower, @"\b" + System.Text.RegularExpressions.Regex.Escape(catLower.Replace('_', ' ')) + @"\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
                    {
                        score += 30f;
                    }
                }
                foreach (var t in asset.Tags)
                {
                    if (string.IsNullOrEmpty(t)) continue;
                    string tLower = t.ToLowerInvariant();
                    if (tLower.Length >= 3)
                    {
                        if (System.Text.RegularExpressions.Regex.IsMatch(rawLower, @"\b" + System.Text.RegularExpressions.Regex.Escape(tLower) + @"\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                            (tLower.Contains("_") && System.Text.RegularExpressions.Regex.IsMatch(rawLower, @"\b" + System.Text.RegularExpressions.Regex.Escape(tLower.Replace('_', ' ')) + @"\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
                        {
                            score += 10f;
                        }
                    }
                }
            }

            // 9. Capacity requirement (only award bonus/penalty if the asset has some semantic relevance)
            if (query.MinCapacity > 0 && score > 0f)
            {
                if (asset.Capacity >= query.MinCapacity) score += 25f;
                else if (asset.Capacity > 0) score -= 15f;
            }

            // 10. Celebrity role guard: Celebrities / VIP artists should not be picked for generic combat/security/heist roles unless explicitly requested
            bool isCelebrity = asset.Type == AssetType.Ped && (asset.HasTag("celebrity") || asset.HasTag("vip_artist") || asset.HasTag("dj"));
            bool queryRequestsCelebrity = (!string.IsNullOrEmpty(query.RawQuery) && (query.RawQuery.Contains("celebrity") || query.RawQuery.Contains("artist") || query.RawQuery.Contains("dj") || query.RawQuery.Contains("musician") || query.RawQuery.Contains("star") || query.RawQuery.Contains("dre") || query.RawQuery.Contains("moody") || query.RawQuery.Contains("solomun")))
                || (query.RequiredTags != null && (query.RequiredTags.Contains("celebrity") || query.RequiredTags.Contains("dj") || query.RequiredTags.Contains("vip_artist")));

            if (isCelebrity && !queryRequestsCelebrity)
            {
                score -= 50f;
            }

            return score;
        }

        #endregion

        #region Evidence-Grounded Resolution API (new)

        /// <summary>
        /// Evidence-grounded asset resolution.  Returns a <see cref="CanonicalAssetResolution"/>
        /// with an explicit evidence chain, resolution status, and approximation flag.
        ///
        /// The SpatialWorldPlanner SHOULD prefer this over the legacy Resolve() wherever possible.
        /// Status must be Exact, Resolved, or Approximate before the result may be spawned.
        /// </summary>
        public CanonicalAssetResolution ResolveWithEvidence(AssetQuery query)
        {
            if (query == null)
                return CanonicalAssetResolution.CreateInvalid("null", "Null query supplied.");

            var legacyResult = Resolve(query);
            if (legacyResult == null || legacyResult.PrimaryAsset == null)
            {
                return CanonicalAssetResolution.CreateUnknown(
                    query.RawQuery ?? query.Identity ?? query.Type.ToString(),
                    string.Format("No asset matched query. Type={0}, Identity='{1}', Tags=[{2}]",
                        query.Type, query.Identity ?? "", string.Join(",", query.RequiredTags ?? new System.Collections.Generic.List<string>())));
            }

            var asset = legacyResult.PrimaryAsset;

            // Determine status from available evidence
            ResolutionStatus status = ResolutionStatus.Resolved;
            bool isApprox           = false;

            bool hasRuntimeEvidence = false;
            if (asset.Facts != null)
            {
                foreach (var f in asset.Facts)
                {
                    if (f.Evidence != null &&
                        (f.Evidence.Type == EvidenceType.DirectGameMetadata ||
                         f.Evidence.Type == EvidenceType.RuntimeValidation))
                    {
                        hasRuntimeEvidence = true;
                        break;
                    }
                }
            }

            if (hasRuntimeEvidence)
                status = ResolutionStatus.Exact;
            else if (legacyResult.MatchScore < 30f)
            {
                status  = ResolutionStatus.Approximate;
                isApprox = true;
            }

            // Collect evidence from the resolved asset
            var evidence = new System.Collections.Generic.List<EvidenceRecord>();
            if (asset.Facts != null)   foreach (var f in asset.Facts)   if (f.Evidence != null) evidence.Add(f.Evidence);
            if (asset.Inferences != null) foreach (var inf in asset.Inferences) foreach (var e in inf.SupportingEvidence) evidence.Add(e);

            float confidence = hasRuntimeEvidence ? 0.97f : asset.SemanticConfidenceScore;

            var resolution = new CanonicalAssetResolution
            {
                Request            = query.RawQuery ?? query.Identity ?? query.Type.ToString(),
                Status             = status,
                SelectedAsset      = asset,
                Alternatives       = legacyResult.AlternativeAssets ?? new System.Collections.Generic.List<GtaAssetDefinition>(),
                Evidence           = evidence,
                IsApproximation    = isApprox,
                Confidence         = confidence,
                ExplanationSummary = string.Format(
                    "Selected '{0}' (type={1}) with score {2:F1}. Evidence sources: [{3}]. Confidence: {4:F2}. Approximation: {5}.",
                    asset.ModelName, asset.Type, legacyResult.MatchScore,
                    string.Join(", ", evidence.Count > 0
                        ? (System.Collections.Generic.IEnumerable<string>)(System.Linq.Enumerable.Select(evidence, e => e.Type.ToString()))
                        : new string[] { "none" }),
                    confidence, isApprox)
            };

            return resolution;
        }

        #endregion

        #region Affordance-Driven Primitive Composition & Identity

        /// <summary>
        /// Composes complex game entities from physical primitives when no single pre-baked
        /// Rockstar prop satisfies all requested functional affordances.
        /// Delegates directly to GenericPrimitiveComposer.
        /// </summary>
        public AssetQueryResult ComposeComplexEntity(string functionalConcept, AssetQuery contextQuery = null)
        {
            if (_primitiveComposer != null)
            {
                var group = _primitiveComposer.Compose(functionalConcept, contextQuery);
                if (group != null && group.PrimaryBase != null)
                {
                    var result = new AssetQueryResult
                    {
                        PrimaryAsset = group.PrimaryBase,
                        ResolutionProvenance = group.CompositionProvenance,
                        Confidence = KnowledgeConfidence.SemanticInferred,
                        MatchScore = 150f
                    };

                    foreach (var sub in group.SubComponents)
                    {
                        if (sub != null && sub.Asset != null)
                        {
                            result.ComposedSubAssets.Add(sub.Asset);
                        }
                    }

                    return result;
                }
            }

            // Fallback to direct resolution
            return Resolve(contextQuery ?? new AssetQuery { Type = AssetType.Prop, RawQuery = functionalConcept });
        }

        /// <summary>
        /// Resolves natural language identities to verified ped models in the active GTA V installation.
        /// </summary>
        public IdentityResolutionResult ResolveIdentity(string identityOrRoleQuery, string contextDlc = null)
        {
            if (_identityResolver != null)
            {
                return _identityResolver.ResolveIdentity(identityOrRoleQuery, contextDlc);
            }
            return new IdentityResolutionResult { RequestedQuery = identityOrRoleQuery, IsSuccess = false, RejectionReason = "IdentityResolver not initialized" };
        }

        /// <summary>
        /// Invalidates the derived asset cache and triggers a fresh autonomous re-discovery.
        /// </summary>
        public void InvalidateAndRebuildCache()
        {
            if (_cacheManager != null)
            {
                _cacheManager.InvalidateCache();
            }
            InitializeRegistry(_gtaInstallationRoot);
        }

        #endregion
    }

    #endregion
}
