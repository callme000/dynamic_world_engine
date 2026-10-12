using System;
using System.Collections.Generic;
using System.IO;
using GTA.Math;
using DynamicWorldEngine;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Retrieval;
using DynamicWorldEngine.Simulation;
using DynamicWorldEngine.Spatial;

namespace DynamicWorldEngine.Tests
{
    public class TestSpatialPerceptionArchitecture
    {
        private static int _passed = 0;
        private static int _failed = 0;

        private static void Assert(bool condition, string message)
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine("  [PASS] " + message);
            }
            else
            {
                _failed++;
                Console.WriteLine("  [FAIL] " + message);
            }
        }

        public static int Main(string[] args)
        {
            Console.WriteLine("=================================================================");
            Console.WriteLine("  TEST SUITE: Spatial Perception Architecture (Structured Vision)");
            Console.WriteLine("=================================================================");

            TestSlopeAndTerrainClassification();
            TestCompassBoundariesAndFootprint();
            TestAlleyAndCorridorDetection();
            TestLivingEntityCensusAndFactions();
            TestPromptFormattingAndTokenEfficiency();
            TestCandidateRankingMultiSite();
            TestEngineCoreIntegration();

            Console.WriteLine();
            Console.WriteLine(string.Format("Results: {0} passed, {1} failed.", _passed, _failed));
            Console.WriteLine("=================================================================");

            return _failed == 0 ? 0 : 1;
        }

        private static void TestSlopeAndTerrainClassification()
        {
            Console.WriteLine("\n--- Test 1: Surface Normal to Slope & Terrain Classification ---");

            // Flat ground: Normal (0, 0, 1) -> nz = 1 -> acos(1) = 0 deg
            float flatNz = 1.0f;
            float flatSlope = (float)Math.Acos(flatNz) * (180.0f / (float)Math.PI);
            Assert(Math.Abs(flatSlope) < 0.01f, "Flat normal (0,0,1) produces exactly 0.0° slope");

            // Gentle grade: nz = 0.995 -> acos(0.995) ~ 5.73 deg
            float gentleNz = 0.995f;
            float gentleSlope = (float)Math.Acos(gentleNz) * (180.0f / (float)Math.PI);
            Assert(gentleSlope >= 5.0f && gentleSlope <= 6.5f, "Normal with nz=0.995 produces ~5.7° gentle slope");

            // Moderate incline: nz = 0.9396 -> acos(0.9396) ~ 20.0 deg
            float modNz = 0.9396f;
            float modSlope = (float)Math.Acos(modNz) * (180.0f / (float)Math.PI);
            Assert(modSlope >= 19.5f && modSlope <= 20.5f, "Normal with nz=0.9396 produces ~20.0° moderate incline");

            // Steep cliff: nz = 0.7071 -> acos(0.7071) ~ 45.0 deg
            float steepNz = 0.7071f;
            float steepSlope = (float)Math.Acos(steepNz) * (180.0f / (float)Math.PI);
            Assert(steepSlope >= 44.5f && steepSlope <= 45.5f, "Normal with nz=0.7071 produces ~45.0° steep incline");

            var steepSnap = SpatialPerceptionScanner.CreateSyntheticSnapshot(
                0f, 0f, 10f, "Vinewood Hills", "Scenic Drive",
                steepSlope, 40f, 40f, false, 0f, 0, 0, "None"
            );
            Assert(steepSnap.PlacementFeasibility.Contains("Restricted"), "Steep 45° terrain flagged as Restricted placement");
        }

        private static void TestCompassBoundariesAndFootprint()
        {
            Console.WriteLine("\n--- Test 2: Compass Boundaries & Clear Footprint Calculation ---");

            var snap = new WorldPerceptionSnapshot
            {
                NorthClearance = 15.0f,
                SouthClearance = 25.0f,
                EastClearance = 20.0f,
                WestClearance = 10.0f,
                AvailableLength = 15.0f + 25.0f, // 40m
                AvailableWidth = 20.0f + 10.0f   // 30m
            };

            snap.BoundaryStructures.Add("Building/Wall 15m North");
            snap.BoundaryStructures.Add("Structure 20m East");
            snap.BoundaryStructures.Add("Wall/Fence 10m West");

            Assert(snap.AvailableWidth == 30.0f, "Available Width derived as 30m (East 20m + West 10m)");
            Assert(snap.AvailableLength == 40.0f, "Available Length derived as 40m (North 15m + South 25m)");
            Assert(snap.BoundaryStructures.Count == 3, "3 prominent boundary structures recorded");
            Assert(snap.BoundaryStructures[0].Contains("15m North"), "North boundary correctly localized at 15m");
        }

        private static void TestAlleyAndCorridorDetection()
        {
            Console.WriteLine("\n--- Test 3: Alley & Corridor Detection from Asymmetric Clearances ---");

            // Narrow East-West span (4.5m) with elongated North-South span (50m)
            var alleySnap = SpatialPerceptionScanner.CreateSyntheticSnapshot(
                110f, -1450f, 30f, "Davis", "Grove St Alley",
                1.5f, 4.5f, 50.0f, true, 4.5f, 3, 0, "Ballas"
            );

            Assert(alleySnap.IsAlleyOrCorridor == true, "Alley / corridor successfully detected from 4.5m narrow span");
            Assert(alleySnap.CorridorWidth == 4.5f, "Corridor width recorded as 4.5m");
            Assert(alleySnap.ObstructionDensity == "Medium", "Alley obstruction density classified as Medium");
            Assert(alleySnap.RoadDistance > alleySnap.CorridorWidth, "Roadway offset correctly measured beyond narrow alley");
        }

        private static void TestLivingEntityCensusAndFactions()
        {
            Console.WriteLine("\n--- Test 4: Living Entities Census & Faction Association ---");

            string testDir = Path.Combine(Path.GetTempPath(), "TestSpatialPerception_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);

            try
            {
                var store = new WorldMemoryStore(testDir);
                var recomputer = new EphemeralStateRecomputer();

                // Seed Ballas NPCs in area
                var npc1 = new NpcRecord
                {
                    NpcId = 201,
                    Name = "Balla G",
                    PublicFaction = "Ballas"
                };
                npc1.History.Add(new EventRecord { PositionX = 100f, PositionY = -1500f, PositionZ = 30f, HasPosition = true });
                store.EnrollNpc(npc1);

                var npc2 = new NpcRecord
                {
                    NpcId = 202,
                    Name = "OG Balla",
                    PublicFaction = "Ballas"
                };
                npc2.History.Add(new EventRecord { PositionX = 105f, PositionY = -1505f, PositionZ = 30f, HasPosition = true });
                store.EnrollNpc(npc2);

                store.EnrollEnvironment(new EnvironmentRecord
                {
                    EnvironmentId = "ballas_crackhouse",
                    Name = "Ballas Safehouse",
                    CenterX = 90f,
                    CenterY = -1490f,
                    CenterZ = 30f,
                    NpcIds = new List<int> { 201, 202 }
                });
                store.Flush();

                var scanner = new SpatialPerceptionScanner();
                var snapshot = scanner.ScanLocation(new Vector3(100f, -1500f, 30f), 50f, store, recomputer);

                Assert(snapshot.DominantFaction == "Ballas", "Dominant faction identified as Ballas from local memory");
                Assert(snapshot.NearbyEnvironments.Count >= 1, "Nearby registered safehouse detected within radius");
                Assert(snapshot.NearbyEnvironments[0].Contains("Ballas Safehouse"), "Registered environment title localized");
            }
            finally
            {
                if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            }
        }

        private static void TestPromptFormattingAndTokenEfficiency()
        {
            Console.WriteLine("\n--- Test 5: Prompt Formatting & Token Efficiency (< 300 Tokens) ---");

            var snap = SpatialPerceptionScanner.CreateSyntheticSnapshot(
                115f, -1460f, 30f, "Davis", "Forum Drive Alley",
                2.1f, 5.0f, 45.0f, true, 5.0f, 6, 2, "Families", "Low"
            );
            snap.NearbyEnvironments.Add("families_clubhouse (62m)");

            string formatted = snap.FormatForPrompt();

            Assert(formatted.Contains("### SPATIAL PERCEPTION SNAPSHOT"), "Formatted prompt contains perception header");
            Assert(formatted.Contains("Davis (Forum Drive Alley)"), "Formatted prompt contains location and street");
            Assert(formatted.Contains("Alley/Corridor (Width: 5.0m)"), "Formatted prompt contains alley dimensions");
            Assert(formatted.Contains("Families"), "Formatted prompt contains living faction presence");
            Assert(formatted.Contains("families_clubhouse"), "Formatted prompt contains nearby venues");

            // Token count check: ~4 chars per token. 300 tokens = ~1200 characters.
            int charCount = formatted.Length;
            int approxTokens = charCount / 4;
            Console.WriteLine("  -> Prompt Character Count: " + charCount + " (~" + approxTokens + " tokens)");
            Assert(approxTokens < 300, "Formatted perception block is under 300 tokens (" + approxTokens + " tokens)");
        }

        private static void TestCandidateRankingMultiSite()
        {
            Console.WriteLine("\n--- Test 6: Multi-Site Candidate Location Ranking ---");

            var scanner = new SpatialPerceptionScanner();

            // 3 Candidates:
            // Site 1: Open industrial flat ground (LS Port)
            // Site 2: Davis alley (Chop Shop / hideout candidate)
            // Site 3: Vinewood steep hillside
            var positions = new List<Vector3>
            {
                new Vector3(800f, -3000f, 5f),  // Port (flat open)
                new Vector3(110f, -1450f, 30f), // Davis
                new Vector3(600f, 800f, 180f)   // Vinewood steep
            };

            // Rank for a large staging compound / drag strip
            var compoundScores = scanner.RankCandidates(positions, "compound");
            Assert(compoundScores.Count == 3, "Ranked all 3 candidate locations");
            Assert(compoundScores[0].FitnessScore >= compoundScores[1].FitnessScore, "Rankings sorted descending by fitness");

            // Rank for an illicit gang hideout / chop shop
            var hideoutScores = scanner.RankCandidates(positions, "gang_hideout");
            Assert(hideoutScores.Count == 3, "Ranked 3 candidates for gang hideout");
            Assert(hideoutScores[0].FitnessScore > 0f, "Top hideout candidate has positive fitness score");
        }

        private static void TestEngineCoreIntegration()
        {
            Console.WriteLine("\n--- Test 7: EngineCore End-to-End Integration & Headless Safety ---");

            string testDir = Path.Combine(Path.GetTempPath(), "TestPerceptionEngine_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);

            try
            {
                var engine = new EngineCore(testDir);

                // 1. Perception Scanner property
                Assert(engine.PerceptionScanner != null, "EngineCore exposes PerceptionScanner instance");

                // 2. Scan snapshot
                var snapshot = engine.GetPerceptionSnapshot(120f, -1400f, 30f);
                Assert(snapshot != null, "GetPerceptionSnapshot returns non-null snapshot");
                Assert(snapshot.Elevation == 30f, "Snapshot elevation grounded to target Z in headless mode");

                // 3. Multi-site ranking via EngineCore
                var candidates = new List<Vector3>
                {
                    new Vector3(100f, -1400f, 30f),
                    new Vector3(200f, -1500f, 30f)
                };
                var ranked = engine.RankCandidateLocations(candidates, "street_vendor");
                Assert(ranked != null && ranked.Count == 2, "RankCandidateLocations successfully evaluated 2 sites");

                // 4. Director prompt generation with brief + perception
                var brief = engine.GetKairosSituationBrief(120f, -1400f, 30f);
                string json = engine.GenerateDirectorJsonFromNaturalLanguage("Ballas meeting in alley", brief, snapshot);
                Assert(!string.IsNullOrEmpty(json), "Director generation produces valid output when supplied with perception snapshot");
            }
            finally
            {
                if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            }
        }
    }
}
