using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using DynamicWorldEngine.Database;
using GTA.Math;

namespace DynamicWorldEngine.Discovery
{
    /// <summary>
    /// Lightweight, portable JSON DTO for Vector3 facts without binary ScriptHookVDotNet coupling.
    /// </summary>
    public class SerializableVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public SerializableVector3() { }

        public SerializableVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public SerializableVector3(Vector3 v)
        {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(X, Y, Z);
        }
    }

    public class SerializableEvidenceRecord
    {
        public string EvidenceId { get; set; }
        public string Type { get; set; }
        public string Source { get; set; }
        public string SourceLocation { get; set; }
        public string Description { get; set; }
        public float Strength { get; set; }
        public DateTime RetrievedAt { get; set; }

        public static SerializableEvidenceRecord FromEvidence(EvidenceRecord e)
        {
            if (e == null) return null;
            return new SerializableEvidenceRecord
            {
                EvidenceId = e.EvidenceId,
                Type = e.Type.ToString(),
                Source = e.Source,
                SourceLocation = e.SourceLocation,
                Description = e.Description,
                Strength = e.Strength,
                RetrievedAt = e.RetrievedAt
            };
        }

        public EvidenceRecord ToEvidence()
        {
            EvidenceType t;
            if (!Enum.TryParse(Type, out t)) t = EvidenceType.SemanticSimilarity;
            return new EvidenceRecord
            {
                EvidenceId = EvidenceId,
                Type = t,
                Source = Source,
                SourceLocation = SourceLocation,
                Description = Description,
                Strength = Strength,
                RetrievedAt = RetrievedAt
            };
        }
    }

    public class SerializableAssetFact
    {
        public string Property { get; set; }
        public string Value { get; set; }
        public SerializableEvidenceRecord Evidence { get; set; }
        public string Status { get; set; }

        public static SerializableAssetFact FromFact(AssetFact f)
        {
            if (f == null) return null;
            return new SerializableAssetFact
            {
                Property = f.Property,
                Value = f.Value,
                Evidence = SerializableEvidenceRecord.FromEvidence(f.Evidence),
                Status = f.Status.ToString()
            };
        }

        public AssetFact ToFact()
        {
            EpistemicStatus st;
            if (!Enum.TryParse(Status, out st)) st = EpistemicStatus.Fact;
            return new AssetFact
            {
                Property = Property,
                Value = Value,
                Evidence = Evidence != null ? Evidence.ToEvidence() : null,
                Status = st
            };
        }
    }

    public class SerializableAssetInference
    {
        public string Property { get; set; }
        public string Value { get; set; }
        public float Confidence { get; set; }
        public string Status { get; set; }
        public List<SerializableEvidenceRecord> SupportingEvidence { get; set; }

        public static SerializableAssetInference FromInference(AssetInference inf)
        {
            if (inf == null) return null;
            var s = new SerializableAssetInference
            {
                Property = inf.Property,
                Value = inf.Value,
                Confidence = inf.Confidence,
                Status = inf.Status.ToString(),
                SupportingEvidence = new List<SerializableEvidenceRecord>()
            };
            if (inf.SupportingEvidence != null)
            {
                foreach (var e in inf.SupportingEvidence)
                {
                    var se = SerializableEvidenceRecord.FromEvidence(e);
                    if (se != null) s.SupportingEvidence.Add(se);
                }
            }
            return s;
        }

        public AssetInference ToInference()
        {
            EpistemicStatus st;
            if (!Enum.TryParse(Status, out st)) st = EpistemicStatus.Candidate;
            var inf = new AssetInference
            {
                Property = Property,
                Value = Value,
                Confidence = Confidence,
                Status = st,
                SupportingEvidence = new List<EvidenceRecord>()
            };
            if (SupportingEvidence != null)
            {
                foreach (var se in SupportingEvidence)
                {
                    if (se != null) inf.SupportingEvidence.Add(se.ToEvidence());
                }
            }
            return inf;
        }
    }

    /// <summary>
    /// Pure JSON DTO for GtaAssetDefinition, ensuring the asset cache is 100% portable
    /// and decoupled from native engine / CLR assembly reflection.
    /// </summary>
    public class SerializableAssetDefinition
    {
        public string ModelName { get; set; }
        public uint ModelHash { get; set; }
        public string Type { get; set; }
        public bool ExistsInGame { get; set; }
        public bool HasCollision { get; set; }
        public SerializableVector3 Dimensions { get; set; }
        public float SurfaceElevation { get; set; }
        public bool Walkable { get; set; }
        public bool BlocksNavMesh { get; set; }
        public bool CanAttachChildren { get; set; }

        public string DlcPackOrigin { get; set; }
        public string ArchetypeName { get; set; }
        public List<string> IplDependencies { get; set; }
        public List<string> RemoveIplDependencies { get; set; }
        public List<string> EntitySets { get; set; }
        public SerializableVector3 DefaultCoordinates { get; set; }
        public float DefaultHeading { get; set; }
        public bool SupportsVehicles { get; set; }
        public int Capacity { get; set; }
        public SerializableVector3 WaterArrivalCoords { get; set; }

        public string Category { get; set; }
        public string Description { get; set; }
        public List<string> Tags { get; set; }
        public List<string> Affordances { get; set; }
        public List<string> Identities { get; set; }
        public List<string> Roles { get; set; }
        public string Confidence { get; set; }
        public float SemanticConfidenceScore { get; set; }
        public List<SerializableAssetFact> Facts { get; set; }
        public List<SerializableAssetInference> Inferences { get; set; }

        public SerializableAssetDefinition()
        {
            Dimensions = new SerializableVector3(1f, 1f, 1f);
            IplDependencies = new List<string>();
            RemoveIplDependencies = new List<string>();
            EntitySets = new List<string>();
            Tags = new List<string>();
            Affordances = new List<string>();
            Identities = new List<string>();
            Roles = new List<string>();
            Confidence = "MachineVerifiedFactual";
            SemanticConfidenceScore = 1.0f;
            Facts = new List<SerializableAssetFact>();
            Inferences = new List<SerializableAssetInference>();
        }

        public static SerializableAssetDefinition FromAsset(GtaAssetDefinition def)
        {
            if (def == null) return null;

            var s = new SerializableAssetDefinition
            {
                ModelName = def.ModelName,
                ModelHash = def.ModelHash,
                Type = def.Type.ToString(),
                ExistsInGame = def.ExistsInGame,
                HasCollision = def.HasCollision,
                Dimensions = new SerializableVector3(def.Dimensions),
                SurfaceElevation = def.SurfaceElevation,
                Walkable = def.Walkable,
                BlocksNavMesh = def.BlocksNavMesh,
                CanAttachChildren = def.CanAttachChildren,
                DlcPackOrigin = def.DlcPackOrigin,
                ArchetypeName = def.ArchetypeName,
                IplDependencies = new List<string>(def.IplDependencies ?? new List<string>()),
                RemoveIplDependencies = new List<string>(def.RemoveIplDependencies ?? new List<string>()),
                EntitySets = new List<string>(def.EntitySets ?? new List<string>()),
                DefaultCoordinates = new SerializableVector3(def.DefaultCoordinates),
                DefaultHeading = def.DefaultHeading,
                SupportsVehicles = def.SupportsVehicles,
                Capacity = def.Capacity,
                WaterArrivalCoords = def.WaterArrivalCoords.HasValue ? new SerializableVector3(def.WaterArrivalCoords.Value) : null,
                Category = def.Category,
                Description = def.Description,
                Tags = new List<string>(def.Tags ?? new List<string>()),
                Affordances = new List<string>(def.Affordances ?? new List<string>()),
                Identities = new List<string>(def.Identities ?? new List<string>()),
                Roles = new List<string>(def.Roles ?? new List<string>()),
                Confidence = def.Confidence.ToString(),
                SemanticConfidenceScore = def.SemanticConfidenceScore,
                Facts = new List<SerializableAssetFact>(),
                Inferences = new List<SerializableAssetInference>()
            };

            if (def.Facts != null)
            {
                foreach (var f in def.Facts)
                {
                    var sf = SerializableAssetFact.FromFact(f);
                    if (sf != null) s.Facts.Add(sf);
                }
            }

            if (def.Inferences != null)
            {
                foreach (var inf in def.Inferences)
                {
                    var si = SerializableAssetInference.FromInference(inf);
                    if (si != null) s.Inferences.Add(si);
                }
            }

            return s;
        }

        public GtaAssetDefinition ToAsset()
        {
            AssetType aType;
            if (!Enum.TryParse(Type, out aType)) aType = AssetType.Prop;

            KnowledgeConfidence conf;
            if (!Enum.TryParse(Confidence, out conf)) conf = KnowledgeConfidence.MachineVerifiedFactual;

            var def = new GtaAssetDefinition
            {
                ModelName = ModelName,
                ModelHash = ModelHash,
                Type = aType,
                ExistsInGame = ExistsInGame,
                HasCollision = HasCollision,
                Dimensions = Dimensions != null ? Dimensions.ToVector3() : new Vector3(1f, 1f, 1f),
                SurfaceElevation = SurfaceElevation,
                Walkable = Walkable,
                BlocksNavMesh = BlocksNavMesh,
                CanAttachChildren = CanAttachChildren,
                DlcPackOrigin = DlcPackOrigin,
                ArchetypeName = ArchetypeName,
                IplDependencies = new List<string>(IplDependencies ?? new List<string>()),
                RemoveIplDependencies = new List<string>(RemoveIplDependencies ?? new List<string>()),
                EntitySets = new List<string>(EntitySets ?? new List<string>()),
                DefaultCoordinates = DefaultCoordinates != null ? DefaultCoordinates.ToVector3() : new Vector3(0f, 0f, 0f),
                DefaultHeading = DefaultHeading,
                SupportsVehicles = SupportsVehicles,
                Capacity = Capacity,
                WaterArrivalCoords = WaterArrivalCoords != null ? (Vector3?)WaterArrivalCoords.ToVector3() : null,
                Category = Category,
                Description = Description,
                Tags = new List<string>(Tags ?? new List<string>()),
                Affordances = new List<string>(Affordances ?? new List<string>()),
                Identities = new List<string>(Identities ?? new List<string>()),
                Roles = new List<string>(Roles ?? new List<string>()),
                Confidence = conf,
                SemanticConfidenceScore = SemanticConfidenceScore,
                Facts = new List<AssetFact>(),
                Inferences = new List<AssetInference>()
            };

            if (Facts != null)
            {
                foreach (var sf in Facts)
                {
                    if (sf != null) def.Facts.Add(sf.ToFact());
                }
            }

            if (Inferences != null)
            {
                foreach (var si in Inferences)
                {
                    if (si != null) def.Inferences.Add(si.ToInference());
                }
            }

            return def;
        }
    }

    public class AssetIndexCacheManifest
    {
        public string ScannerVersion { get; set; }
        public string GtaRootPath { get; set; }
        public string GtaVersion { get; set; }
        public int DlcPacksCount { get; set; }
        public int TotalAssetsCount { get; set; }
        public string DlcContentSignature { get; set; }
        public DateTime GeneratedTimestampUtc { get; set; }
        public List<SerializableAssetDefinition> Assets { get; set; }

        public AssetIndexCacheManifest()
        {
            ScannerVersion = "2.5.0-DynamicZeroHardcoded";
            Assets = new List<SerializableAssetDefinition>();
            GeneratedTimestampUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Autonomous Asset Cache & Rebuild Manager.
    /// Manages the derived asset-index cache, detecting DLC changes, invalidating outdated caches,
    /// and ensuring the GTA V installation remains the single authoritative source of truth.
    /// </summary>
    public class AssetIndexCacheManager
    {
        public const string CacheFileName = "asset_index.json";
        private readonly string _cacheDirectory;
        private readonly DlcPackDiscoveryEngine _dlcEngine;

        public AssetIndexCacheManager(string cacheDirectory, DlcPackDiscoveryEngine dlcEngine)
        {
            _cacheDirectory = !string.IsNullOrEmpty(cacheDirectory) ? cacheDirectory : AppDomain.CurrentDomain.BaseDirectory;
            _dlcEngine = dlcEngine ?? new DlcPackDiscoveryEngine();
        }

        public string CacheFilePath
        {
            get { return Path.Combine(_cacheDirectory, CacheFileName); }
        }

        public bool IsCacheValid(string gtaRootPath)
        {
            string path = CacheFilePath;
            if (!File.Exists(path)) return false;

            try
            {
                using (var fs = File.OpenRead(path))
                using (var reader = new StreamReader(fs))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    var serializer = new JsonSerializer();
                    var manifest = serializer.Deserialize<AssetIndexCacheManifest>(jsonReader);
                    if (manifest == null || manifest.Assets == null || manifest.Assets.Count == 0) return false;

                    // Verify installation path if provided
                    if (!string.IsNullOrEmpty(gtaRootPath) && !string.IsNullOrEmpty(manifest.GtaRootPath) &&
                        !string.Equals(manifest.GtaRootPath, gtaRootPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    // Compute current DLC signature to detect added/removed DLCs
                    string currentSig = ComputeDlcSignature(gtaRootPath);
                    return string.Equals(manifest.DlcContentSignature, currentSig, StringComparison.Ordinal);
                }
            }
            catch
            {
                return false;
            }
        }

        public List<GtaAssetDefinition> LoadCacheAssets()
        {
            var manifest = LoadCacheManifest();
            if (manifest == null || manifest.Assets == null) return null;

            var list = new List<GtaAssetDefinition>();
            foreach (var s in manifest.Assets)
            {
                list.Add(s.ToAsset());
            }
            return list;
        }

        public AssetIndexCacheManifest LoadCacheManifest()
        {
            string path = CacheFilePath;
            if (!File.Exists(path)) return null;

            try
            {
                string text = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<AssetIndexCacheManifest>(text);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[AssetIndexCacheManager] Warning loading cache: " + ex.Message);
                return null;
            }
        }

        public void SaveCache(string gtaRootPath, string gtaVersion, IEnumerable<GtaAssetDefinition> assets)
        {
            try
            {
                if (!Directory.Exists(_cacheDirectory))
                {
                    Directory.CreateDirectory(_cacheDirectory);
                }

                var manifest = new AssetIndexCacheManifest
                {
                    GtaRootPath = gtaRootPath,
                    GtaVersion = gtaVersion,
                    DlcPacksCount = _dlcEngine.TotalPacksCount,
                    DlcContentSignature = ComputeDlcSignature(gtaRootPath),
                    GeneratedTimestampUtc = DateTime.UtcNow
                };

                if (assets != null)
                {
                    foreach (var a in assets)
                    {
                        manifest.Assets.Add(SerializableAssetDefinition.FromAsset(a));
                    }
                    manifest.TotalAssetsCount = manifest.Assets.Count;
                }

                string json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
                File.WriteAllText(CacheFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[AssetIndexCacheManager] Warning saving cache: " + ex.Message);
            }
        }

        public void InvalidateCache()
        {
            try
            {
                string path = CacheFilePath;
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Console.WriteLine("[AssetIndexCacheManager] Asset index cache invalidated.");
                }
            }
            catch { }
        }

        public string ComputeDlcSignature(string gtaRootPath)
        {
            _dlcEngine.ScanDlcPacks(gtaRootPath);
            var sb = new StringBuilder();

            foreach (var pack in _dlcEngine.DiscoveredPacks)
            {
                sb.Append(pack.PackName);
                sb.Append(":");
                sb.Append(pack.RpfSizeBytes);
                sb.Append(":");
                sb.Append(pack.LastModifiedUtc.Ticks);
                sb.Append(";");
            }

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
