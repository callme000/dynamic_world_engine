using System;
using System.Collections.Generic;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Retrieval;
using GTA.Math;
using Newtonsoft.Json;

namespace DynamicWorldEngine.Spatial
{
    public struct SpatialVector3 : IEquatable<SpatialVector3>
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("z")]
        public float Z { get; set; }

        public SpatialVector3(float x, float y, float z) : this()
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static implicit operator Vector3(SpatialVector3 v)
        {
            return new Vector3(v.X, v.Y, v.Z);
        }

        public static implicit operator SpatialVector3(Vector3 v)
        {
            return new SpatialVector3(v.X, v.Y, v.Z);
        }

        public static implicit operator SpatialVector3(Vector3Offset vo)
        {
            if (vo == null) return new SpatialVector3(0f, 0f, 0f);
            return new SpatialVector3(vo.X, vo.Y, vo.Z);
        }

        public static SpatialVector3 operator +(SpatialVector3 a, SpatialVector3 b)
        {
            return new SpatialVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static SpatialVector3 operator +(SpatialVector3 a, Vector3 b)
        {
            return new SpatialVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static SpatialVector3 operator +(Vector3 a, SpatialVector3 b)
        {
            return new SpatialVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static SpatialVector3 operator -(SpatialVector3 a, SpatialVector3 b)
        {
            return new SpatialVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static SpatialVector3 operator -(SpatialVector3 a, Vector3 b)
        {
            return new SpatialVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static SpatialVector3 operator -(Vector3 a, SpatialVector3 b)
        {
            return new SpatialVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public float Length()
        {
            return (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        }

        public float DistanceTo(SpatialVector3 target)
        {
            float dx = X - target.X;
            float dy = Y - target.Y;
            float dz = Z - target.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public float DistanceTo(Vector3 target)
        {
            float dx = X - target.X;
            float dy = Y - target.Y;
            float dz = Z - target.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static bool operator ==(SpatialVector3 a, SpatialVector3 b)
        {
            return Math.Abs(a.X - b.X) < 0.0001f &&
                   Math.Abs(a.Y - b.Y) < 0.0001f &&
                   Math.Abs(a.Z - b.Z) < 0.0001f;
        }

        public static bool operator !=(SpatialVector3 a, SpatialVector3 b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (obj is SpatialVector3) return this == (SpatialVector3)obj;
            if (obj is Vector3) return this == (SpatialVector3)(Vector3)obj;
            return false;
        }

        public bool Equals(SpatialVector3 other)
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ (Y.GetHashCode() << 2) ^ (Z.GetHashCode() >> 2);
        }

        public override string ToString()
        {
            return string.Format("({0:F2}, {1:F2}, {2:F2})", X, Y, Z);
        }
    }

    public class SpatialExecutionPlan
    {
        public string EnvironmentId { get; set; }
        public string PlanId { get { return string.IsNullOrEmpty(_planId) ? EnvironmentId : _planId; } set { _planId = value; } }
        private string _planId;
        public string Name { get; set; }
        public string Archetype { get; set; }
        public SpatialVector3 CenterPosition { get; set; }
        public float Heading { get; set; }
        public bool IsSkyPocket { get; set; }
        public InteriorDefinition MatchedInterior { get; set; }
        public List<ResolvedPropPlacement> ResolvedProps { get; set; }
        public List<ResolvedNpcPlacement> ResolvedNpcs { get; set; }
        public List<ResolvedVehiclePlacement> ResolvedVehicles { get; set; }
        public EnvironmentAtmosphereData Atmosphere { get; set; }
        public List<WallNavBlockerDefinition> NavBlockers { get; set; }
        public PocketTransition PlannedPocket { get; set; }
        public List<PocketTransition> ConnectedPockets { get; set; }
        public List<ArchitecturalLayerDecomposition> ArchitecturalLayers { get; set; }
        public ScenarioDirectorData ScenarioDirector { get; set; }
        public SpatialFeasibilityAudit FeasibilityAudit { get; set; }
        public CharacterResolutionAudit CharacterAudit { get; set; }
        public AssetTransferAudit AssetTransferAudit { get; set; }
        public EngramResolutionReport EngramReport { get; set; }
        [JsonIgnore]
        public SpatialWorldModel WorldModel { get; set; }
        [JsonIgnore]
        public SpatialSceneSolution SceneSolution { get; set; }
        [JsonIgnore]
        public SpatialValidationResult ValidationResult { get; set; }

        public SpatialExecutionPlan()
        {
            ResolvedProps = new List<ResolvedPropPlacement>();
            ResolvedNpcs = new List<ResolvedNpcPlacement>();
            ResolvedVehicles = new List<ResolvedVehiclePlacement>();
            Atmosphere = new EnvironmentAtmosphereData();
            NavBlockers = new List<WallNavBlockerDefinition>();
            ConnectedPockets = new List<PocketTransition>();
            ArchitecturalLayers = new List<ArchitecturalLayerDecomposition>();
            FeasibilityAudit = null;
            CharacterAudit = null;
            AssetTransferAudit = null;
        }
    }

    public class ResolvedVehiclePlacement
    {
        public string VehicleId { get; set; }
        public string ModelName { get; set; }
        public SpatialVector3 WorldPosition { get; set; }
        public float Heading { get; set; }
        public string PrimaryColor { get; set; }
        public bool IsEngineRunning { get; set; }
        public bool Sirens { get; set; }
        public bool LightsOn { get; set; }
        public string DriverEntityId { get; set; }

        public ResolvedVehiclePlacement()
        {
            VehicleId = "";
            ModelName = "police";
            PrimaryColor = "";
            DriverEntityId = "";
        }
    }

    public class ResolvedPropPlacement
    {
        public string PropId { get; set; }
        public string ParentPropId { get; set; }
        public string ModelName { get; set; }
        public SpatialVector3 WorldPosition { get; set; }
        public SpatialVector3 WorldRotation { get; set; } // Pitch, Roll, Heading
        public bool IsDynamic { get; set; }
        public bool BlocksNavMesh { get; set; }
        public SpatialVector3 Dimensions { get; set; }
    }

    public class ResolvedNpcPlacement
    {
        public string EntityId { get; set; }
        public string Name { get; set; }
        public string Occupation { get; set; }
        public string PublicFaction { get; set; }
        public string HiddenRole { get; set; }
        public string PedModel { get; set; }
        public SpatialVector3 WorldPosition { get; set; }
        public float Heading { get; set; }
        public string Scenario { get; set; }
        public string Weapon { get; set; }
        public string InitialTask { get; set; }
        public string InitialState { get; set; }
        public Dictionary<string, int> Relationships { get; set; }
        public List<InitialEventData> InitialEvents { get; set; }

        public ResolvedNpcPlacement()
        {
            Relationships = new Dictionary<string, int>();
            InitialEvents = new List<InitialEventData>();
            InitialTask = "";
            InitialState = "CALM";
        }
    }

    public class WallNavBlockerDefinition
    {
        public SpatialVector3 Center { get; set; }
        public SpatialVector3 Size { get; set; }
        public float Heading { get; set; }

        public WallNavBlockerDefinition()
        {
        }

        public WallNavBlockerDefinition(SpatialVector3 center, SpatialVector3 size, float heading)
        {
            Center = center;
            Size = size;
            Heading = heading;
        }
    }
}
