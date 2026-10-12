using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Retrieval;
using DynamicWorldEngine.Spatial;
using GTA.Math;

namespace DynamicWorldEngine.Tests
{
    class TestEngramResolutionArchitecture
    {
        private static int passed = 0;
        private static int total = 0;

        static void AssertTrue(string name, bool condition)
        {
            total++;
            if (condition)
            {
                Console.WriteLine(string.Format("  [PASS] {0}", name));
                passed++;
            }
            else
            {
                Console.WriteLine(string.Format("  [FAIL] {0}", name));
            }
        }

        static int Main(string[] args)
        {
            Console.WriteLine("==========================================================");
            Console.WriteLine("  ENGRAM LAYER ARCHITECTURE TEST SUITE (Law 3: Engram)   ");
            Console.WriteLine("==========================================================\n");

            try
            {
                // ========================================================
                // TEST 1: Semantic Prop Resolution
                // ========================================================
                Console.WriteLine("--- TEST 1: Semantic Prop Resolution ---");
                {
                    var resTable = EngramResolver.ResolvePropModel("folding_table");
                    AssertTrue("folding_table resolves to non-empty model", !string.IsNullOrEmpty(resTable.ResolvedModelName));
                    AssertTrue("folding_table is NOT an exact passthrough", !resTable.IsExactMatch);
                    AssertTrue("folding_table resolves to a table or surface model",
                        resTable.ResolvedModelName.Contains("table") || resTable.ResolvedModelName.StartsWith("prop_") || resTable.ResolvedModelName.StartsWith("bkr_"));

                    var resChair = EngramResolver.ResolvePropModel("office_chair");
                    AssertTrue("office_chair resolves to chair/seat model",
                        resChair.ResolvedModelName.Contains("chair") || resChair.ResolvedModelName.Contains("seat") || resChair.ResolvedModelName.StartsWith("prop_"));

                    var resAmmo = EngramResolver.ResolvePropModel("ammo_crate");
                    AssertTrue("ammo_crate resolves to ammo/box model",
                        resAmmo.ResolvedModelName.Contains("ammo") || resAmmo.ResolvedModelName.Contains("box") || resAmmo.ResolvedModelName.Contains("crate"));

                    var resCash = EngramResolver.ResolvePropModel("cash_briefcase");
                    AssertTrue("cash_briefcase resolves to cash/heist loot model",
                        resCash.ResolvedModelName.Contains("cash") || resCash.ResolvedModelName.Contains("case") || resCash.ResolvedModelName.Contains("vault"));

                    var resLight = EngramResolver.ResolvePropModel("worklight");
                    AssertTrue("worklight resolves to worklight/light/lantern model",
                        resLight.ResolvedModelName.Contains("light") || resLight.ResolvedModelName.Contains("worklight") || resLight.ResolvedModelName.Contains("lantern") || resLight.ResolvedModelName.Contains("lamp"));
                }

                // ========================================================
                // TEST 2: Exact Passthrough for Valid GTA Models
                // ========================================================
                Console.WriteLine("\n--- TEST 2: Exact Passthrough for Valid GTA Models ---");
                {
                    var resProp = EngramResolver.ResolvePropModel("prop_table_03b");
                    AssertTrue("prop_table_03b is preserved exactly", resProp.ResolvedModelName == "prop_table_03b");
                    AssertTrue("prop_table_03b is marked exact match", resProp.IsExactMatch);
                    AssertTrue("prop_table_03b confidence is 1.0", resProp.Confidence >= 0.99f);

                    var resAmmoBox = EngramResolver.ResolvePropModel("prop_box_ammo04a");
                    AssertTrue("prop_box_ammo04a is preserved exactly", resAmmoBox.ResolvedModelName == "prop_box_ammo04a");
                    AssertTrue("prop_box_ammo04a is marked exact match", resAmmoBox.IsExactMatch);

                    var resVeh = EngramResolver.ResolveVehicleModel("police");
                    AssertTrue("police vehicle is preserved exactly", resVeh.ResolvedModelName == "police");
                    AssertTrue("police vehicle is marked exact match", resVeh.IsExactMatch);

                    var resBuzzard = EngramResolver.ResolveVehicleModel("buzzard");
                    AssertTrue("buzzard vehicle is preserved exactly", resBuzzard.ResolvedModelName == "buzzard");
                    AssertTrue("buzzard vehicle is marked exact match", resBuzzard.IsExactMatch);
                }

                // ========================================================
                // TEST 3: Hallucinated / Fictional Model Resolution
                // ========================================================
                Console.WriteLine("\n--- TEST 3: Hallucinated / Fictional Model Resolution ---");
                {
                    // Case A: Hallucinated model code with recognizable semantic root
                    var resHallucinatedTable = EngramResolver.ResolvePropModel("prop_super_folding_table");
                    AssertTrue("prop_super_folding_table recognized and resolved to authentic table",
                        resHallucinatedTable.ResolvedModelName.Contains("table") || resHallucinatedTable.ResolvedModelName.StartsWith("prop_"));
                    AssertTrue("prop_super_folding_table is NOT marked exact match", !resHallucinatedTable.IsExactMatch);

                    // Case B: Completely fictional sci-fi prop code (plasma disintegrator)
                    var resFictional = EngramResolver.ResolvePropModel("prop_quantum_plasma_disintegrator_99");
                    AssertTrue("Fictional prop does not throw exception", resFictional != null);
                    AssertTrue("Fictional prop resolves to safe generic fallback", !string.IsNullOrEmpty(resFictional.ResolvedModelName));
                    AssertTrue("Fictional prop confidence is low (< 0.5)", resFictional.Confidence < 0.5f);
                }

                // ========================================================
                // TEST 4: Semantic Vehicle Resolution
                // ========================================================
                Console.WriteLine("\n--- TEST 4: Semantic Vehicle Resolution ---");
                {
                    var vPolice = EngramResolver.ResolveVehicleModel("police_cruiser");
                    AssertTrue("police_cruiser resolves to police", vPolice.ResolvedModelName == "police");

                    var vAmbulance = EngramResolver.ResolveVehicleModel("emergency_ambulance");
                    AssertTrue("emergency_ambulance resolves to ambulance", vAmbulance.ResolvedModelName == "ambulance");

                    var vFire = EngramResolver.ResolveVehicleModel("firetruck");
                    AssertTrue("firetruck resolves to firetruk or firetruck",
                        vFire.ResolvedModelName.Equals("firetruk", StringComparison.OrdinalIgnoreCase) || 
                        vFire.ResolvedModelName.Equals("firetruck", StringComparison.OrdinalIgnoreCase));

                    var vHeli = EngramResolver.ResolveVehicleModel("attack_helicopter");
                    AssertTrue("attack_helicopter resolves to buzzard or savage",
                        vHeli.ResolvedModelName == "buzzard" || vHeli.ResolvedModelName == "savage");

                    var vLimo = EngramResolver.ResolveVehicleModel("luxury_limo");
                    AssertTrue("luxury_limo resolves to cognoscenti or luxury sedan",
                        vLimo.ResolvedModelName == "cognoscenti" || vLimo.ResolvedModelName == "windsor");

                    var vTank = EngramResolver.ResolveVehicleModel("military_tank");
                    AssertTrue("military_tank resolves to rhino", vTank.ResolvedModelName == "rhino");

                    var vSports = EngramResolver.ResolveVehicleModel("street_racing_sports_car");
                    AssertTrue("street_racing_sports_car resolves to comet2", vSports.ResolvedModelName == "comet2");

                    var vSub = EngramResolver.ResolveVehicleModel("submarine");
                    AssertTrue("submarine resolves to submersible or kosatka",
                        vSub.ResolvedModelName == "submersible" || vSub.ResolvedModelName == "kosatka");
                }

                // ========================================================
                // TEST 5: Engram Resolution Report Tracking
                // ========================================================
                Console.WriteLine("\n--- TEST 5: Engram Resolution Report Tracking ---");
                {
                    var data = new BuildEnvironmentData
                    {
                        EnvironmentId = "env_engram_test",
                        Name = "Engram Test Suite",
                        Archetype = "test_lab",
                        Props = new List<PropPlacementData>
                        {
                            new PropPlacementData { PropId = "p1", Model = "folding_table" },
                            new PropPlacementData { PropId = "p2", Model = "prop_table_03b" }, // exact
                            new PropPlacementData { PropId = "p3", Model = "prop_quantum_plasma_disintegrator_99" } // unresolvable
                        },
                        Vehicles = new List<VehiclePlacementData>
                        {
                            new VehiclePlacementData { VehicleId = "v1", Model = "police_cruiser" },
                            new VehiclePlacementData { VehicleId = "v2", Model = "buzzard" } // exact
                        }
                    };

                    var report = EngramResolver.ResolveAllAssets(data, "Test lab environment");

                    AssertTrue("Report processed 5 items total", report.TotalProcessed == 5);
                    AssertTrue("Report tracked 2 exact passthroughs", report.PassthroughCount == 2);
                    AssertTrue("Report tracked 2 high-confidence semantic resolutions", report.ResolvedCount == 2);
                    AssertTrue("Report tracked 1 fallback/unresolved item", report.FallbackCount == 1);
                    AssertTrue("Unresolved list contains fictional alien disruptor",
                        report.UnresolvedAssets.Exists(u => u.Contains("prop_quantum_plasma_disintegrator_99")));

                    // Verify mutation on data object
                    AssertTrue("p1.Model was updated from folding_table", data.Props[0].Model != "folding_table");
                    AssertTrue("p2.Model remained prop_table_03b", data.Props[1].Model == "prop_table_03b");
                    AssertTrue("v1.Model was updated from police_cruiser to police", data.Vehicles[0].Model == "police");
                    AssertTrue("v2.Model remained buzzard", data.Vehicles[1].Model == "buzzard");
                }

                // ========================================================
                // TEST 6: SpatialWorldPlanner Pipeline Integration
                // ========================================================
                Console.WriteLine("\n--- TEST 6: SpatialWorldPlanner Pipeline Integration ---");
                {
                    var planner = new SpatialWorldPlanner();
                    var data = new BuildEnvironmentData
                    {
                        EnvironmentId = "env_deal_engram",
                        Name = "Cartel Safehouse Deal",
                        Archetype = "arms_deal",
                        Props = new List<PropPlacementData>
                        {
                            new PropPlacementData { PropId = "deal_table", Model = "folding_table" },
                            new PropPlacementData { PropId = "chair_1", Model = "office_chair" },
                            new PropPlacementData { PropId = "loot_box", Model = "ammo_crate" }
                        },
                        Vehicles = new List<VehiclePlacementData>
                        {
                            new VehiclePlacementData { VehicleId = "getaway_car", Model = "sports_car" }
                        }
                    };

                    Vector3 playerPos = new Vector3(100f, 200f, 30f);
                    float heading = 90f;

                    var plan = planner.PlanEnvironment(data, playerPos, heading);

                    AssertTrue("Plan has EngramReport attached", plan.EngramReport != null);
                    AssertTrue("EngramReport recorded 4 assets resolved", plan.EngramReport.TotalProcessed == 4);

                    // Verify props in plan have authentic GTA models, NOT raw semantic descriptors!
                    var resolvedTable = plan.ResolvedProps.Find(p => p.PropId == "deal_table");
                    AssertTrue("Resolved table exists in plan", resolvedTable != null);
                    AssertTrue("Resolved table does NOT have semantic descriptor 'folding_table'",
                        resolvedTable.ModelName != "folding_table");
                    AssertTrue("Resolved table has valid GTA prop name",
                        resolvedTable.ModelName.StartsWith("prop_") || resolvedTable.ModelName.StartsWith("bkr_"));

                    var resolvedVeh = plan.ResolvedVehicles.Find(v => v.VehicleId == "getaway_car");
                    AssertTrue("Resolved vehicle exists in plan", resolvedVeh != null);
                    AssertTrue("Resolved vehicle does NOT have semantic class 'sports_car'",
                        resolvedVeh.ModelName != "sports_car");
                    AssertTrue("Resolved vehicle has valid GTA vehicle model name",
                        resolvedVeh.ModelName == "comet2" || resolvedVeh.ModelName == "police");
                }

                // ========================================================
                // TEST 7: Null & Edge Case Safety
                // ========================================================
                Console.WriteLine("\n--- TEST 7: Null & Edge Case Safety ---");
                {
                    var repNull = EngramResolver.ResolveAllAssets(null);
                    AssertTrue("Null data does not throw", repNull != null && repNull.TotalProcessed == 0);

                    var resNullProp = EngramResolver.ResolvePropModel(null);
                    AssertTrue("Null prop model returns safe default", !string.IsNullOrEmpty(resNullProp.ResolvedModelName));

                    var resEmptyVeh = EngramResolver.ResolveVehicleModel("");
                    AssertTrue("Empty vehicle model returns safe default", !string.IsNullOrEmpty(resEmptyVeh.ResolvedModelName));

                    var resWhitespace = EngramResolver.ResolvePropModel("   ");
                    AssertTrue("Whitespace prop returns safe default", !string.IsNullOrEmpty(resWhitespace.ResolvedModelName));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("\n  [FATAL] Unhandled exception: {0}", ex));
            }

            Console.WriteLine(string.Format("\n=========================================================="));
            Console.WriteLine(string.Format("  RESULTS: {0}/{1} PASSED", passed, total));
            Console.WriteLine(string.Format("=========================================================="));

            return (passed == total) ? 0 : 1;
        }
    }
}
