using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Discovery;
using DynamicWorldEngine.Spatial;
using GTA.Math;

namespace DynamicWorldEngine.Tests
{
    class TestAssetDiscoveryPipeline
    {
        private static int _passedCount = 0;
        private static int _failedCount = 0;

        static int Main(string[] args)
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("    DYNAMIC WORLD ENGINE - ZERO-HARDCODED ASSET DISCOVERY PIPELINE TEST        ");
            Console.WriteLine("================================================================================\n");

            try
            {
                RunTest("1. Dynamic GTA V Installation Discovery", TestInstallationDiscovery);
                RunTest("2. Dynamic DLC Pack Discovery & Chronological Ordering", TestDlcPackDiscovery);
                RunTest("3. Semantic Classification & Affordance Inference Engine", TestSemanticClassification);
                RunTest("4. Canonical Character & Lore Identity Resolver", TestCanonicalIdentityResolution);
                RunTest("5. Generic Primitive Composition Engine", TestGenericPrimitiveComposition);
                RunTest("6. Autonomous Cache Lifecycle & Invalidation (asset_index.json)", TestCacheLifecycle);
                RunTest("7. Acceptance Prompt 1: Newest Luxury Interior & Vehicles", TestAcceptancePrompt1);
                RunTest("8. Acceptance Prompt 2: Submarine in Luxury Casino Party (Volumetric Feasibility)", TestAcceptancePrompt2);
                RunTest("9. Acceptance Prompt 3: The DLC Collage (5 Cross-DLC Fusion)", TestAcceptancePrompt3_DlcCollage);
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n[CRITICAL FAILURE] Unhandled test exception: " + ex);
                _failedCount++;
            }

            Console.WriteLine("\n================================================================================");
            Console.WriteLine("    TEST SUMMARY: {0} PASSED, {1} FAILED", _passedCount, _failedCount);
            Console.WriteLine("================================================================================");

            return _failedCount == 0 ? 0 : 1;
        }

        private static void RunTest(string testName, Action testAction)
        {
            Console.WriteLine(">>> RUNNING: " + testName);
            try
            {
                testAction();
                Console.WriteLine("    [PASS] " + testName + "\n");
                _passedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine("    [FAIL] " + testName + " - " + ex.Message + "\n");
                _failedCount++;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("Assertion Failed: " + message);
            }
        }

        private static void TestInstallationDiscovery()
        {
            var info = GtaInstallationLocator.LocateInstallation();
            Console.WriteLine("    Discovered Root:       {0}", info.RootPath);
            Console.WriteLine("    Discovered Edition:    {0}", info.Edition);
            Console.WriteLine("    Discovered Version:    {0}", info.Version);
            Console.WriteLine("    Discovery Method:      {0}", info.DiscoveryMethod);
            Console.WriteLine("    DLC Packs Directory:   {0}", info.DlcPacksPath);

            Assert(info.IsInstalled, "GTA V installation was not discovered.");
            Assert(!string.IsNullOrEmpty(info.RootPath), "Discovered RootPath must not be empty.");
            Assert(Directory.Exists(info.RootPath), "Discovered RootPath does not physically exist.");
            Assert(!string.IsNullOrEmpty(info.DlcPacksPath), "DLC packs path must be populated.");
        }

        private static void TestDlcPackDiscovery()
        {
            var install = GtaInstallationLocator.LocateInstallation();
            var dlcEngine = new DlcPackDiscoveryEngine();
            dlcEngine.ScanDlcPacks(install.RootPath);

            Console.WriteLine("    Total DLC Packs Discovered: {0}", dlcEngine.TotalPacksCount);
            Assert(dlcEngine.TotalPacksCount > 0, "No DLC packs were discovered in GTA installation.");

            var newest = dlcEngine.GetNewestDlcPack();
            Assert(newest != null, "Failed to retrieve newest DLC pack.");
            Console.WriteLine("    Newest Installed DLC Pack:  {0} (Order #{1}, Size: {2:N0} MB, Modified: {3:yyyy-MM-dd})",
                newest.PackName, newest.ChronologicalOrderIndex, newest.RpfSizeBytes / (1024 * 1024), newest.LastModifiedUtc);

            Assert(newest.ChronologicalOrderIndex == dlcEngine.TotalPacksCount, "Newest pack order index should match total packs count.");
            Assert(dlcEngine.HasDlc("mpheist4"), "Cayo Perico (mpheist4) pack must be discovered.");
            Assert(dlcEngine.HasDlc("mpvinewood"), "Diamond Casino (mpvinewood) pack must be discovered.");
        }

        private static void TestSemanticClassification()
        {
            var engine = new SemanticClassificationEngine();

            // Test 1: DJ Desk Prop
            var djProp = new GtaAssetDefinition
            {
                ModelName = "ba_prop_club_dj_desk",
                Type = AssetType.Prop
            };
            engine.Enrich(djProp);
            Console.WriteLine("    ba_prop_club_dj_desk Affordances: {0}", string.Join(", ", djProp.Affordances));
            Assert(djProp.Affordances.Contains("can_be_surface"), "DJ desk should have 'can_be_surface' affordance.");
            Assert(djProp.Affordances.Contains("dj_equipment"), "DJ desk should have 'dj_equipment' affordance.");
            Assert(djProp.Tags.Contains("nightclub"), "DJ desk should have 'nightclub' tag.");
            Assert(djProp.DlcPackOrigin == "mpBattle", "ba_ prefix should map to mpBattle origin.");

            // Test 2: Submarine
            var subProp = new GtaAssetDefinition
            {
                ModelName = "h4_prop_sub_periscope",
                Type = AssetType.Prop
            };
            engine.Enrich(subProp);
            Console.WriteLine("    h4_prop_sub_periscope Affordances: {0}", string.Join(", ", subProp.Affordances));
            Assert(subProp.Affordances.Contains("submersible") || subProp.Tags.Contains("submarine"), "Sub prop should have submarine tag or submersible affordance.");
            Assert(subProp.Tags.Contains("cayo_perico"), "h4_ prefix should map to cayo_perico.");

            // Test 3: Tactical Planning Table
            var tableProp = new GtaAssetDefinition
            {
                ModelName = "xm_prop_x17_table_tactical",
                Type = AssetType.Prop
            };
            engine.Enrich(tableProp);
            Console.WriteLine("    xm_prop_x17_table_tactical Affordances: {0}", string.Join(", ", tableProp.Affordances));
            Assert(tableProp.Affordances.Contains("planning_surface") || tableProp.Affordances.Contains("can_be_surface"), "Tactical table should have planning_surface or can_be_surface affordance.");
            Assert(tableProp.Tags.Contains("doomsday"), "xm_ prefix should map to doomsday.");
        }

        private static void TestCanonicalIdentityResolution()
        {
            var resolver = SpatialAssetRegistry.Instance.IdentityResolver;

            // Query 1: Dr. Dre
            // IMPORTANT: We do NOT assert a specific model name here.
            // The expected model comes from characters.json (external data), not from this test.
            // Asserting a specific substring like "musician" would be circular validation
            // (it would only pass because of the source code that previously put it there).
            var dre = resolver.ResolveIdentity("Dr. Dre");
            Console.WriteLine("    'Dr. Dre' Resolved To: {0} ({1}) - Score: {2}", dre.ResolvedCharacterName, dre.ResolvedPedModel, dre.ConfidenceScore);
            Assert(dre.IsSuccess, "Dr. Dre resolution must succeed (character is in characters.json).");
            // Verify resolution is backed by CuratedManifest evidence, not hard-coded C# logic
            bool dreHasManifestEvidence = false;
            foreach (var e in dre.EvidenceRecords)
                if (e.Type == EvidenceType.CuratedManifest) { dreHasManifestEvidence = true; break; }
            Assert(dreHasManifestEvidence,
                "Dr. Dre resolution must be backed by CuratedManifest evidence from characters.json. " +
                "If this fails, an illegal hard-coded identity mapping exists in source code.");
            Assert(!string.IsNullOrEmpty(dre.ResolvedPedModel),
                "A non-empty model name must be returned for Dr. Dre.");

            // Query 2: Franklin
            var franklin = resolver.ResolveIdentity("Franklin Clinton");
            Console.WriteLine("    'Franklin Clinton' Resolved To: {0} ({1}) - Score: {2}", franklin.ResolvedCharacterName, franklin.ResolvedPedModel, franklin.ConfidenceScore);
            Assert(franklin.IsSuccess, "Franklin resolution must succeed (character is in characters.json).");
            Assert(!string.IsNullOrEmpty(franklin.ResolvedPedModel), "A non-empty model name must be returned for Franklin Clinton.");

            // Query 3: Lamar
            var lamar = resolver.ResolveIdentity("Lamar Davis");
            Console.WriteLine("    'Lamar Davis' Resolved To: {0} ({1}) - Score: {2}", lamar.ResolvedCharacterName, lamar.ResolvedPedModel, lamar.ConfidenceScore);
            Assert(lamar.IsSuccess, "Lamar resolution must succeed (character is in characters.json).");

            // Query 4: Party DJ — should resolve to one of the resident DJs
            var dj = resolver.ResolveIdentity("DJ");
            Console.WriteLine("    'DJ' Resolved To: {0} ({1}) - Score: {2}", dj.ResolvedCharacterName, dj.ResolvedPedModel, dj.ConfidenceScore);
            Assert(dj.IsSuccess, "DJ resolution must succeed.");
            // Verify result is one of the canonical resident DJs from characters.json (not a fabrication)
            var asset = SpatialAssetRegistry.Instance.GetAsset(dj.ResolvedPedModel);
            Assert(asset != null, string.Format("Resolved DJ model '{0}' must be in the registry.", dj.ResolvedPedModel));
            // The resolved character should have a "dj" role inference
            bool hasDjRole = false;
            if (asset.Inferences != null)
                foreach (var inf in asset.Inferences)
                    if (inf.Property == "role" && inf.Value != null && inf.Value.ToLowerInvariant().Contains("dj"))
                    { hasDjRole = true; break; }
            if (!hasDjRole && asset.Roles != null)
                foreach (var r in asset.Roles)
                    if (r.ToLowerInvariant().Contains("dj")) { hasDjRole = true; break; }
            Assert(hasDjRole, string.Format(
                "Resolved DJ model '{0}' must have a 'dj' role (from characters.json), got roles: [{1}]",
                dj.ResolvedPedModel, string.Join(", ", asset.Roles)));
        }

        private static void TestGenericPrimitiveComposition()
        {
            var registry = SpatialAssetRegistry.Instance;

            // 1. Compose DJ Booth
            var djBooth = registry.ComposeComplexEntity("dj_booth");
            Console.WriteLine("    Composed DJ Booth Base: {0}", djBooth.PrimaryAsset != null ? djBooth.PrimaryAsset.ModelName : "null");
            Console.WriteLine("    Sub-Assets Count:       {0}", djBooth.ComposedSubAssets.Count);
            Console.WriteLine("    Provenance:             {0}", djBooth.ResolutionProvenance);
            Assert(djBooth.PrimaryAsset != null, "DJ booth composition must produce a primary surface base.");
            Assert(djBooth.ComposedSubAssets.Count >= 2, "DJ booth composition must include turntables and speakers.");

            // 2. Compose Tactical Heist Table
            var heistTable = registry.ComposeComplexEntity("heist_planning_table");
            Console.WriteLine("    Composed Heist Table:   {0}", heistTable.PrimaryAsset != null ? heistTable.PrimaryAsset.ModelName : "null");
            Console.WriteLine("    Sub-Assets Count:       {0}", heistTable.ComposedSubAssets.Count);
            Console.WriteLine("    Provenance:             {0}", heistTable.ResolutionProvenance);
            Assert(heistTable.PrimaryAsset != null, "Heist table composition must produce a primary conference table.");
            Assert(heistTable.ComposedSubAssets.Count >= 2, "Heist table must include blueprints and cash/loot stacks.");
        }

        private static void TestCacheLifecycle()
        {
            var registry = SpatialAssetRegistry.Instance;
            var cacheManager = registry.CacheManager;
            string cachePath = cacheManager.CacheFilePath;

            Console.WriteLine("    Cache Path: {0}", cachePath);

            // Step 1: Invalidate cache
            registry.InvalidateAndRebuildCache();
            Assert(File.Exists(cachePath), "Cache file asset_index.json must exist after InvalidateAndRebuildCache().");

            // Step 2: Validate cache signature
            bool isValid = cacheManager.IsCacheValid(registry.GtaInstallationRoot);
            Console.WriteLine("    IsCacheValid(): {0}", isValid);
            Assert(isValid, "asset_index.json must be valid for the active GTA V installation.");

            var manifest = cacheManager.LoadCacheManifest();
            Assert(manifest != null, "Failed to load generated cache manifest.");
            Console.WriteLine("    Cached Assets Count:    {0}", manifest.TotalAssetsCount);
            Console.WriteLine("    DLC Content Signature:  {0}", manifest.DlcContentSignature);
            Assert(manifest.TotalAssetsCount > 0, "Cached assets count must be > 0.");
            Assert(!string.IsNullOrEmpty(manifest.DlcContentSignature), "DLC signature must not be empty.");
        }

        private static void TestAcceptancePrompt1()
        {
            string prompt = "Throw a massive party in the newest luxury interior with the latest sports cars and high-end guests.";
            Console.WriteLine("    Prompt: \"{0}\"", prompt);

            var newestPack = SpatialAssetRegistry.Instance.DlcDiscoveryEngine.GetNewestDlcPack();
            Console.WriteLine("    Discovered Newest DLC Pack: {0}", newestPack.PackName);

            // Plan via SpatialWorldPlanner
            var engine = new EngineCore(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Discovery_Data"));
            var planner = engine.Planner;

            var payload = new BuildEnvironmentData
            {
                OriginalPrompt = prompt,
                SpatialRequirements = new SpatialRequirementsData
                {
                    Atmosphere = "luxury",
                    Privacy = "exclusive",
                    MinCapacity = 30,
                    RequiredTags = new List<string> { "luxury", "penthouse", "party" }
                },
                Npcs = new List<EnvironmentNpcData>
                {
                    new EnvironmentNpcData { Name = "VIP Guest 1", Occupation = "Celebrity", HiddenRole = "Guest", Scenario = "WORLD_HUMAN_PARTYING" },
                    new EnvironmentNpcData { Name = "DJ", Occupation = "DJ", HiddenRole = "Performer" }
                },
                Vehicles = new List<VehiclePlacementData>
                {
                    new VehiclePlacementData { Model = "italirsx", PrimaryColor = "Gold" }
                }
            };

            var plan = planner.PlanEnvironment(payload, new Vector3(0f, 0f, 0f), 0f);

            Console.WriteLine("    Matched Interior:    {0} ({1})", plan.MatchedInterior != null ? plan.MatchedInterior.Name : "None", plan.MatchedInterior != null ? plan.MatchedInterior.InteriorId : "None");
            Console.WriteLine("    Resolved NPCs Count: {0}", plan.ResolvedNpcs.Count);
            var djNpc = plan.ResolvedNpcs.Find(n => n.Occupation == "DJ" || n.HiddenRole == "Performer");
            Console.WriteLine("    Resolved DJ Model:   {0}", djNpc != null ? djNpc.PedModel : "None");

            Assert(plan.MatchedInterior != null, "Luxury party must resolve to a valid luxury/penthouse interior.");
            Assert(plan.ResolvedNpcs.Count >= 2, "Must resolve placed VIP guests.");
        }

        private static void TestAcceptancePrompt2()
        {
            string prompt = "Put a military submarine inside a luxury casino party.";
            Console.WriteLine("    Prompt: \"{0}\"", prompt);

            var engine = new EngineCore(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Discovery_Data"));
            var planner = engine.Planner;

            var payload = new BuildEnvironmentData
            {
                OriginalPrompt = prompt,
                SpatialRequirements = new SpatialRequirementsData
                {
                    Atmosphere = "luxury",
                    RequiredTags = new List<string> { "casino", "party" }
                },
                Vehicles = new List<VehiclePlacementData>
                {
                    new VehiclePlacementData { Model = "kosatka", PrimaryColor = "Black" }
                }
            };

            var plan = planner.PlanEnvironment(payload, new Vector3(0f, 0f, 0f), 0f);

            Console.WriteLine("    Matched Interior:       {0}", plan.MatchedInterior != null ? plan.MatchedInterior.Name : "Open-World Ground");
            Console.WriteLine("    Feasibility Audit:      {0}", plan.FeasibilityAudit != null ? "TRIGGERED (Pass)" : "NOT TRIGGERED (Fail)");

            if (plan.FeasibilityAudit != null)
            {
                Console.WriteLine("    Requested Asset:        {0}", plan.FeasibilityAudit.RequestedAsset);
                Console.WriteLine("    Classification:         {0}", plan.FeasibilityAudit.AssetClassification);
                Console.WriteLine("    Reason:                 {0}", plan.FeasibilityAudit.ConflictReason);
                Console.WriteLine("    Safe Alternative:       {0}", plan.FeasibilityAudit.ResolutionStrategy);
            }

            Assert(plan.FeasibilityAudit != null, "Volumetric feasibility check must detect oversized submarine in casino interior.");
        }

        private static void TestAcceptancePrompt3_DlcCollage()
        {
            string prompt = "I want a criminal conference where the world's biggest criminals meet. Use the Diamond Casino penthouse as the venue, have the LS Car Meet guys provide the cars outside, use a Doomsday-style command center as the meeting room, have Dr. Dre performing downstairs, and keep a Cayo Perico guard team protecting the building.";
            Console.WriteLine("    Prompt: \"{0}\"", prompt);

            var engine = new EngineCore(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Discovery_Data"));
            var service = engine.Generator;
            var planner = engine.Planner;

            // 1. Synthesize environment dynamically from prompt via WorldGeneratorService
            string generatedJson = service.SynthesizeEnvironmentLocally(prompt);
            Assert(!string.IsNullOrEmpty(generatedJson), "Synthesized JSON must not be empty.");

            var buildPayload = engine.Parser.ParseBuildEnvironment(generatedJson);
            Assert(buildPayload != null, "Action parser must successfully deserialize synthesized JSON.");
            Assert(buildPayload.Data != null, "Deserialized payload must contain environment data.");

            var payload = buildPayload.Data;
            if (string.IsNullOrEmpty(payload.OriginalPrompt))
            {
                payload.OriginalPrompt = prompt;
            }

            // 2. Plan environment execution via SpatialWorldPlanner
            var plan = planner.PlanEnvironment(payload, new Vector3(0f, 0f, 0f), 0f);

            // Print resolution telemetry
            Console.WriteLine("    [1. Venue] Matched Interior:   {0} ({1})",
                plan.MatchedInterior != null ? plan.MatchedInterior.Name : "None",
                plan.MatchedInterior != null ? plan.MatchedInterior.InteriorId : "None");

            Console.WriteLine("    [2. Tuners] Vehicles Staged:    {0}", plan.ResolvedVehicles.Count);
            foreach (var v in plan.ResolvedVehicles)
            {
                Console.WriteLine("        - Model: {0} at Pos: ({1:F1}, {2:F1}, {3:F1})", v.ModelName, v.WorldPosition.X, v.WorldPosition.Y, v.WorldPosition.Z);
            }

            Console.WriteLine("    [3. Doomsday] Props Resolved:   {0}", plan.ResolvedProps.Count);
            foreach (var p in plan.ResolvedProps)
            {
                Console.WriteLine("        - Prop: {0} (Model: {1})", p.PropId, p.ModelName);
            }

            Console.WriteLine("    [4 & 5. Cast] NPCs Resolved:   {0}", plan.ResolvedNpcs.Count);
            foreach (var n in plan.ResolvedNpcs)
            {
                Console.WriteLine("        - NPC: {0} ({1}) | PedModel: {2} | Weapon: {3}", n.Name, n.Occupation, n.PedModel, n.Weapon);
            }

            // --- Assertions verifying cross-DLC fusion ---
            // 1. Diamond Casino Penthouse Interior
            Assert(plan.MatchedInterior != null, "Venue must resolve to an interior.");
            Assert(plan.MatchedInterior.InteriorId == "int_casino_penthouse" || plan.MatchedInterior.InteriorId == "int_diamond_penthouse",
                string.Format("Venue interior must resolve to 'int_casino_penthouse' or 'int_diamond_penthouse', but resolved to '{0}'.", plan.MatchedInterior.InteriorId));

            // 2. LS Car Meet vehicles staged outside (casino entrance valet)
            Assert(plan.ResolvedVehicles.Count >= 2,
                string.Format("Must resolve at least 2 LS Car Meet tuner cars outside, resolved {0}.", plan.ResolvedVehicles.Count));
            bool hasTunerCar = plan.ResolvedVehicles.Exists(v => v.ModelName == "sultanrs" || v.ModelName == "elegy" || v.ModelName == "calico" || v.ModelName == "jesterrr" || v.ModelName == "superd" || v.ModelName.ToLowerInvariant().Contains("banshee"));
            Assert(hasTunerCar, "Resolved vehicles must contain authentic sports/tuner models.");

            // Verify vehicles placed outside at entrance valet since penthouse does not support vehicles inside
            Assert(!plan.MatchedInterior.SupportsVehicles, "Penthouse interior must not support vehicles inside.");
            foreach (var v in plan.ResolvedVehicles)
            {
                float distToEntrance = (v.WorldPosition - plan.MatchedInterior.EntranceCoords).Length();
                Assert(distToEntrance < 50.0f,
                    string.Format("Vehicle {0} must be placed outside near the casino entrance valet.", v.ModelName));
            }

            // 3. Doomsday Command Center Desk & Screens
            bool hasDoomsdayDesk = plan.ResolvedProps.Exists(p => p.ModelName == "xm_prop_x17_desk_cover_01a" || p.PropId.Contains("cmd_desk") || p.PropId.Contains("doomsday"));
            Assert(hasDoomsdayDesk, "Plan must contain Doomsday command center desk.");

            bool hasDoomsdayScreens = plan.ResolvedProps.Exists(p => p.ModelName == "xm_prop_x17_screens_01a" || p.PropId.Contains("screens"));
            Assert(hasDoomsdayScreens, "Plan must contain Doomsday command center surveillance screens.");

            // 4. Dr. Dre canonical character resolution (The Contract / The Cayo Perico Heist)
            var dreNpc = plan.ResolvedNpcs.Find(n => n.PedModel == "ig_musician_00" || (n.Name != null && n.Name.ToLowerInvariant().Contains("dre")));
            Assert(dreNpc != null, "Plan must resolve canonical character Dr. Dre ('ig_musician_00').");
            Assert(dreNpc.PedModel == "ig_musician_00",
                string.Format("Dr. Dre ped model must be 'ig_musician_00', got '{0}'.", dreNpc.PedModel));

            // 5. Cayo Perico Guard Detail / Armed Enforcers
            var guardNpcs = plan.ResolvedNpcs.FindAll(n => n.Weapon == "WEAPON_CARBINERIFLE" || (n.HiddenRole != null && n.HiddenRole.ToLowerInvariant().Contains("guard")) || (n.Occupation != null && n.Occupation.ToLowerInvariant().Contains("security")));
            Assert(guardNpcs.Count >= 2,
                string.Format("Plan must resolve at least 2 armed guard NPCs, found {0}.", guardNpcs.Count));
        }
    }
}
