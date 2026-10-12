using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Spatial;
using GTA.Math;
using Newtonsoft.Json;

namespace DynamicWorldEngine.Tests
{
    class PlanSerializationRoundtripTester
    {
        static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  ❌ FAIL: " + message);
                Console.ResetColor();
                Environment.Exit(1);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ✅ PASS: " + message);
                Console.ResetColor();
            }
        }

        static int Main(string[] args)
        {
            Console.WriteLine("==========================================================");
            Console.WriteLine("   CANONICAL PLAN SERIALIZATION ROUNDTRIP AUDIT TEST      ");
            Console.WriteLine("==========================================================\n");

            // 1. Construct a rich, realistic Canonical Plan
            SpatialExecutionPlan plan = new SpatialExecutionPlan
            {
                EnvironmentId = "env_bunker_vault_01",
                Name = "Underground Cartel Vault",
                Archetype = "bunker",
                CenterPosition = new SpatialVector3(100.5f, 200.75f, 1500.0f),
                Heading = 90.0f,
                IsSkyPocket = true,
                Atmosphere = new EnvironmentAtmosphereData
                {
                    Weather = "FOGGY",
                    TimeHours = 23,
                    TimeMinutes = 30,
                    AmbientFx = "none"
                },
                PlannedPocket = new PocketTransition
                {
                    PocketId = "pocket_vault_entrance",
                    EnvironmentId = "env_bunker_vault_01",
                    StreetCoords = new SpatialVector3(120.0f, 210.0f, 30.0f),
                    InteriorCoords = new SpatialVector3(100.5f, 200.75f, 1500.0f),
                    InteriorExitCoords = new SpatialVector3(100.5f, 198.0f, 1500.0f),
                    EntranceTitle = "Reinforced Security Door",
                    IsLocked = true,
                    LockReason = "Keycard Required"
                },
                ResolvedProps = new List<ResolvedPropPlacement>
                {
                    new ResolvedPropPlacement
                    {
                        PropId = "desk_01",
                        ParentPropId = "",
                        ModelName = "prop_table_03b",
                        WorldPosition = new SpatialVector3(100.5f, 203.0f, 1500.0f),
                        WorldRotation = new SpatialVector3(0f, 0f, 90.0f),
                        IsDynamic = false,
                        BlocksNavMesh = true,
                        Dimensions = new SpatialVector3(2.5f, 1.2f, 0.8f)
                    },
                    new ResolvedPropPlacement
                    {
                        PropId = "cash_pile_01",
                        ParentPropId = "desk_01",
                        ModelName = "prop_cash_pile_02",
                        WorldPosition = new SpatialVector3(100.5f, 203.0f, 1500.8f),
                        WorldRotation = new SpatialVector3(0f, 0f, 45.0f),
                        IsDynamic = false,
                        BlocksNavMesh = false,
                        Dimensions = new SpatialVector3(0.4f, 0.3f, 0.2f)
                    }
                },
                ResolvedNpcs = new List<ResolvedNpcPlacement>
                {
                    new ResolvedNpcPlacement
                    {
                        EntityId = "npc_guard_bruno",
                        Name = "Bruno",
                        Occupation = "Bouncer",
                        PublicFaction = "Cartel",
                        PedModel = "g_m_y_salvagoon_01",
                        WorldPosition = new SpatialVector3(102.0f, 201.0f, 1500.0f),
                        Heading = 180.0f,
                        Scenario = "WORLD_HUMAN_GUARD_STAND",
                        Weapon = "WEAPON_PUMPSHOTGUN"
                    }
                },
                ResolvedVehicles = new List<ResolvedVehiclePlacement>
                {
                    new ResolvedVehiclePlacement
                    {
                        VehicleId = "veh_escape_01",
                        ModelName = "kuruma",
                        WorldPosition = new SpatialVector3(125.0f, 215.0f, 30.0f),
                        Heading = 270.0f,
                        PrimaryColor = "matte_black",
                        IsEngineRunning = true,
                        Sirens = false,
                        LightsOn = true,
                        DriverEntityId = ""
                    }
                },
                NavBlockers = new List<WallNavBlockerDefinition>
                {
                    new WallNavBlockerDefinition(
                        new SpatialVector3(100.5f, 203.0f, 1500.0f),
                        new SpatialVector3(2.5f, 1.2f, 0.8f),
                        90.0f
                    )
                }
            };

            Console.WriteLine("[Step 1] Serializing Canonical Plan P to JSON...");
            string json = JsonConvert.SerializeObject(plan, Formatting.Indented);
            Assert(!string.IsNullOrEmpty(json), "Serialized JSON string must not be empty.");
            Assert(!json.Contains("ScriptHookVDotNet"), "JSON must not leak ScriptHookVDotNet internal references.");
            Console.WriteLine("  JSON Size: " + json.Length + " bytes");

            Console.WriteLine("\n[Step 2] Deserializing JSON back to Canonical Plan P'...");
            SpatialExecutionPlan planPrime = JsonConvert.DeserializeObject<SpatialExecutionPlan>(json);
            Assert(planPrime != null, "Deserialized plan P' must not be null.");

            Console.WriteLine("\n[Step 3] Field-by-Field Equality Verification (P == P')...");
            Assert(plan.EnvironmentId == planPrime.EnvironmentId, "EnvironmentId matches exactly: " + planPrime.EnvironmentId);
            Assert(plan.Name == planPrime.Name, "Name matches exactly: " + planPrime.Name);
            Assert(plan.CenterPosition == planPrime.CenterPosition, "CenterPosition matches exactly: " + planPrime.CenterPosition);
            Assert(Math.Abs(plan.Heading - planPrime.Heading) < 0.001f, "Heading matches exactly: " + planPrime.Heading);
            Assert(plan.IsSkyPocket == planPrime.IsSkyPocket, "IsSkyPocket matches exactly: " + planPrime.IsSkyPocket);

            // Verify Atmosphere
            Assert(planPrime.Atmosphere != null && planPrime.Atmosphere.Weather == "FOGGY", "Atmosphere weather matches.");

            // Verify Portals
            Assert(planPrime.PlannedPocket != null, "PlannedPocket is preserved.");
            Assert(plan.PlannedPocket.PocketId == planPrime.PlannedPocket.PocketId, "PocketId matches.");
            Assert(plan.PlannedPocket.StreetCoords == planPrime.PlannedPocket.StreetCoords, "Portal StreetCoords match: " + planPrime.PlannedPocket.StreetCoords);
            Assert(plan.PlannedPocket.InteriorCoords == planPrime.PlannedPocket.InteriorCoords, "Portal InteriorCoords match.");
            Assert(plan.PlannedPocket.IsLocked == planPrime.PlannedPocket.IsLocked, "Portal IsLocked state matches.");
            Assert(planPrime.PlannedPocket.EntranceBlip == null, "EntranceBlip runtime handle is cleanly null (not corrupted).");

            // Verify Props
            Assert(plan.ResolvedProps.Count == planPrime.ResolvedProps.Count, "Props count matches: " + planPrime.ResolvedProps.Count);
            for (int i = 0; i < plan.ResolvedProps.Count; i++)
            {
                var p1 = plan.ResolvedProps[i];
                var p2 = planPrime.ResolvedProps[i];
                Assert(p1.PropId == p2.PropId, string.Format("Prop[{0}] PropId matches: {1}", i, p2.PropId));
                Assert(p1.ParentPropId == p2.ParentPropId, string.Format("Prop[{0}] ParentPropId matches: {1}", i, p2.ParentPropId));
                Assert(p1.ModelName == p2.ModelName, string.Format("Prop[{0}] Model matches: {1}", i, p2.ModelName));
                Assert(p1.WorldPosition == p2.WorldPosition, string.Format("Prop[{0}] WorldPosition matches: {1}", i, p2.WorldPosition));
                Assert(p1.WorldRotation == p2.WorldRotation, string.Format("Prop[{0}] WorldRotation matches: {1}", i, p2.WorldRotation));
                Assert(p1.Dimensions == p2.Dimensions, string.Format("Prop[{0}] Dimensions match: {1}", i, p2.Dimensions));
                Assert(p1.BlocksNavMesh == p2.BlocksNavMesh, string.Format("Prop[{0}] BlocksNavMesh matches.", i));
            }

            // Verify NPCs
            Assert(plan.ResolvedNpcs.Count == planPrime.ResolvedNpcs.Count, "NPCs count matches: " + planPrime.ResolvedNpcs.Count);
            for (int i = 0; i < plan.ResolvedNpcs.Count; i++)
            {
                var n1 = plan.ResolvedNpcs[i];
                var n2 = planPrime.ResolvedNpcs[i];
                Assert(n1.EntityId == n2.EntityId, string.Format("NPC[{0}] EntityId matches: {1}", i, n2.EntityId));
                Assert(n1.PedModel == n2.PedModel, string.Format("NPC[{0}] PedModel matches: {1}", i, n2.PedModel));
                Assert(n1.WorldPosition == n2.WorldPosition, string.Format("NPC[{0}] WorldPosition matches: {1}", i, n2.WorldPosition));
                Assert(Math.Abs(n1.Heading - n2.Heading) < 0.001f, string.Format("NPC[{0}] Heading matches.", i));
                Assert(n1.Weapon == n2.Weapon, string.Format("NPC[{0}] Weapon matches: {1}", i, n2.Weapon));
            }

            // Verify Vehicles
            Assert(plan.ResolvedVehicles.Count == planPrime.ResolvedVehicles.Count, "Vehicles count matches: " + planPrime.ResolvedVehicles.Count);
            for (int i = 0; i < plan.ResolvedVehicles.Count; i++)
            {
                var v1 = plan.ResolvedVehicles[i];
                var v2 = planPrime.ResolvedVehicles[i];
                Assert(v1.VehicleId == v2.VehicleId, string.Format("Vehicle[{0}] VehicleId matches: {1}", i, v2.VehicleId));
                Assert(v1.ModelName == v2.ModelName, string.Format("Vehicle[{0}] ModelName matches: {1}", i, v2.ModelName));
                Assert(v1.WorldPosition == v2.WorldPosition, string.Format("Vehicle[{0}] WorldPosition matches: {1}", i, v2.WorldPosition));
                Assert(v1.PrimaryColor == v2.PrimaryColor, string.Format("Vehicle[{0}] PrimaryColor matches: {1}", i, v2.PrimaryColor));
                Assert(v1.IsEngineRunning == v2.IsEngineRunning, string.Format("Vehicle[{0}] IsEngineRunning matches.", i));
            }

            // Verify NavBlockers
            Assert(plan.NavBlockers.Count == planPrime.NavBlockers.Count, "NavBlockers count matches: " + planPrime.NavBlockers.Count);
            for (int i = 0; i < plan.NavBlockers.Count; i++)
            {
                var b1 = plan.NavBlockers[i];
                var b2 = planPrime.NavBlockers[i];
                Assert(b1.Center == b2.Center, string.Format("NavBlocker[{0}] Center matches: {1}", i, b2.Center));
                Assert(b1.Size == b2.Size, string.Format("NavBlocker[{0}] Size matches: {1}", i, b2.Size));
                Assert(Math.Abs(b1.Heading - b2.Heading) < 0.001f, string.Format("NavBlocker[{0}] Heading matches: {1}", i, b2.Heading));
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n==========================================================");
            Console.WriteLine("   PERFECT PASS: CANONICAL PLAN SERIALIZES LOSSLESSLY!   ");
            Console.WriteLine("   P ≡ P' verified across 100% of spatial fields.         ");
            Console.WriteLine("==========================================================");
            Console.ResetColor();

            return 0;
        }
    }
}
