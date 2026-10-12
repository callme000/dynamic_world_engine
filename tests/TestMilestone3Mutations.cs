using System;
using System.IO;
using DynamicWorldEngine;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Services;

namespace DynamicWorldEngine.Tests
{
    class Milestone3Tester
    {
        static int Main(string[] args)
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine("  MILESTONE 3: LIVE MUTATIONS & DYNAMIC EVENTS TEST      ");
            Console.WriteLine("=========================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_M3_Data");
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            Directory.CreateDirectory(testDir);

            EngineCore engine = new EngineCore(testDir);
            int passed = 0;
            int total = 0;

            // --- TEST 1: SPAWN BASE SCENE ---
            total++;
            Console.WriteLine("[Test 1] Spawning Base Scene (Arms Dealer + Guard)...");
            string spawnJson = @"
            {
              ""action"": ""BUILD_ENVIRONMENT"",
              ""data"": {
                ""environment_id"": ""env_arms_deal"",
                ""name"": ""Alley Arms Deal"",
                ""archetype"": ""criminal_deal"",
                ""description"": ""An illegal weapon sale taking place in the alley"",
                ""props"": [
                  { ""model"": ""prop_table_03b"", ""offset"": { ""x"": 0, ""y"": 3, ""z"": 0 }, ""heading"": 0 }
                ],
                ""npcs"": [
                  {
                    ""entity_id"": ""npc_dealer_01"",
                    ""name"": ""Boris 'The Russian'"",
                    ""occupation"": ""Arms Dealer"",
                    ""public_faction"": ""Fixers"",
                    ""hidden_role"": ""Loyal to the highest bidder"",
                    ""relationships"": { ""Player"": 10, ""Police"": -80 }
                  },
                  {
                    ""entity_id"": ""npc_guard_01"",
                    ""name"": ""Vince 'Iron'"",
                    ""occupation"": ""Bodyguard"",
                    ""public_faction"": ""Fixers"",
                    ""hidden_role"": ""Bodyguard with heavy rifle"",
                    ""relationships"": { ""Player"": 0, ""Police"": -90 }
                  }
                ]
              }
            }";

            EnvironmentRecord env = engine.ExecuteBuildEnvironmentCommand(spawnJson, 10f, 20f, 5f, 0f);
            if (env != null && env.NpcIds.Count == 2)
            {
                Console.WriteLine("  ✅ PASS: Created base environment with Boris (Dealer) and Vince (Guard).");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Base environment creation failed.");
            }

            // --- TEST 2: MUTATE ENTITY STATE (BRIBE & BETRAYAL) ---
            total++;
            Console.WriteLine("\n[Test 2] Testing MUTATE_ENTITY_STATE (Bribe Bodyguard to Betray Boss)...");
            string mutateJson = @"
            {
              ""action"": ""MUTATE_ENTITY_STATE"",
              ""data"": {
                ""target_entity_id"": ""npc_guard_01"",
                ""relationship_delta"": 50,
                ""hostility"": ""friendly_follow"",
                ""new_hidden_role"": ""Bribed by player to eliminate Boris and reveal the stash"",
                ""narrative_reason"": ""Player handed Vince a stack of $10,000 cash to switch sides.""
              }
            }";

            NpcRecord modifiedNpc;
            bool mutateOk = engine.ExecuteMutateEntityCommand(mutateJson, out modifiedNpc);
            bool roleUpdated = modifiedNpc != null && modifiedNpc.HiddenRole.Contains("Bribed by player") && modifiedNpc.Relationships["Player"] == 50;

            if (mutateOk && roleUpdated)
            {
                Console.WriteLine("  ✅ PASS: Guard Vince mutated state -> New Hidden Role: '{0}', Relationship to Player: +{1}", 
                    modifiedNpc.HiddenRole, modifiedNpc.Relationships["Player"]);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Mutation failed.");
            }

            // --- TEST 3: TRIGGER WORLD EVENT (POLICE RAID) ---
            total++;
            Console.WriteLine("\n[Test 3] Testing TRIGGER_WORLD_EVENT (Police Tactical Raid)...");
            string raidJson = @"
            {
              ""action"": ""TRIGGER_WORLD_EVENT"",
              ""data"": {
                ""event_id"": ""event_lspd_raid_01"",
                ""event_type"": ""POLICE_RAID"",
                ""title"": ""LSPD SWAT Breach"",
                ""description"": ""Two police cruisers screech to a halt with sirens blaring"",
                ""vehicle_model"": ""POLICE2"",
                ""sirens"": true,
                ""approach_speed"": 35.0,
                ""occupants"": [
                  {
                    ""name"": ""Officer Vance"",
                    ""ped_model"": ""s_m_y_cop_01"",
                    ""weapon"": ""WEAPON_PUMPSHOTGUN"",
                    ""role"": ""driver"",
                    ""hostility_to_player"": ""attack""
                  },
                  {
                    ""name"": ""Officer Martinez"",
                    ""ped_model"": ""s_m_y_swat_01"",
                    ""weapon"": ""WEAPON_CARBINERIFLE"",
                    ""role"": ""passenger"",
                    ""hostility_to_player"": ""attack""
                  }
                ]
              }
            }";

            TriggerWorldEventPayload raidPayload = engine.ParseTriggerWorldEventCommand(raidJson);
            if (raidPayload != null && raidPayload.Data.Occupants.Count == 2 && raidPayload.Data.Sirens)
            {
                Console.WriteLine("  ✅ PASS: Parsed Tactical Police Raid Event: {0} ({1} with {2} officers, sirens={3})",
                    raidPayload.Data.Title, raidPayload.Data.VehicleModel, raidPayload.Data.Occupants.Count, raidPayload.Data.Sirens);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Police raid event parsing failed.");
            }

            // --- TEST 4: ATTACH QUEST OR STASH ---
            total++;
            Console.WriteLine("\n[Test 4] Testing ATTACH_QUEST_OR_STASH...");
            string stashJson = @"
            {
              ""action"": ""ATTACH_QUEST_OR_STASH"",
              ""data"": {
                ""stash_id"": ""stash_arms_cache_01"",
                ""title"": ""Hidden Contraband Ammo Box"",
                ""description"": ""Contains $5,000 cash and military-grade Micro SMG"",
                ""container_prop"": ""prop_box_ammo04a"",
                ""reward_cash"": 5000,
                ""reward_weapon"": ""WEAPON_MICROSMG"",
                ""offset"": { ""x"": 1.5, ""y"": 4.0, ""z"": 0.0 }
              }
            }";

            CreateStashPayload stashPayload = engine.Parser.ParseCreateStash(stashJson);
            if (stashPayload != null && stashPayload.Data.RewardCash == 5000 && stashPayload.Data.RewardWeapon == "WEAPON_MICROSMG")
            {
                Console.WriteLine("  ✅ PASS: Parsed Secret Stash: {0} ($ {1}, Weapon: {2})",
                    stashPayload.Data.Title, stashPayload.Data.RewardCash, stashPayload.Data.RewardWeapon);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Secret Stash parsing failed.");
            }

            // --- TEST 5: LIVINGLSAIS FACT SYNC AFTER MUTATION ---
            total++;
            Console.WriteLine("\n[Test 5] Verifying LivingLSAIs Fact Hot-Sync for Mutated Guard...");
            string guardFact = engine.GetNpcFactForLivingLSAIs(modifiedNpc.NpcId);
            if (guardFact.Contains("Bribed by player to eliminate Boris"))
            {
                Console.WriteLine("  ✅ PASS: LivingLSAIs fact string updated with betrayal motive:\n     {0}", guardFact);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: LivingLSAIs fact string missing mutated role.");
            }

            Console.WriteLine("\n=========================================================");
            Console.WriteLine("  MILESTONE 3 TEST SUMMARY: {0}/{1} TESTS PASSED", passed, total);
            Console.WriteLine("=========================================================");

            return (passed == total) ? 0 : 1;
        }
    }
}
