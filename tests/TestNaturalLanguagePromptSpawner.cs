using System;
using System.IO;
using DynamicWorldEngine;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Services;

namespace DynamicWorldEngine.Tests
{
    class PromptTestRunner
    {
        static int Main(string[] args)
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine("  TESTING NATURAL LANGUAGE -> WORLD SPAWNER GENERATOR    ");
            Console.WriteLine("=========================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Prompt_Data");
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
            Directory.CreateDirectory(testDir);

            EngineCore engine = new EngineCore(testDir);

            string userPrompt = args.Length > 0 ? string.Join(" ", args) : "Illegal arms dealer sitting behind a table with weapon cases and an armed bodyguard";
            Console.WriteLine("User Natural Language Prompt: \"{0}\"\n", userPrompt);

            try
            {
                Console.WriteLine("1. Calling Gemini REST API to architect custom GTA V scene...");
                string generatedJson = engine.GenerateEnvironmentJsonFromNaturalLanguage(userPrompt);

                Console.WriteLine("2. Received AI-Generated BUILD_ENVIRONMENT JSON:\n");
                Console.WriteLine(generatedJson);
                Console.WriteLine("\n3. Executing Spatial World Placement...");

                var buildPayload = engine.Parser.ParseBuildEnvironment(generatedJson);
                if (buildPayload != null && buildPayload.Data != null && string.IsNullOrEmpty(buildPayload.Data.OriginalPrompt))
                {
                    buildPayload.Data.OriginalPrompt = userPrompt;
                }
                var plan = engine.Planner.PlanEnvironment(buildPayload.Data, new GTA.Math.Vector3(100f, 200f, 10f), 90f);
                EnvironmentRecord env = engine.ExecuteBuildEnvironmentPlan(plan, buildPayload.Data);

                Console.WriteLine("\n✅ SUCCESS! Environment Spawned:");
                Console.WriteLine("   • Environment ID:    {0}", env.EnvironmentId);
                Console.WriteLine("   • Title:             {0}", env.Name);
                Console.WriteLine("   • Archetype:         {0}", env.Archetype);
                Console.WriteLine("   • Matched Interior:  {0} ({1})", plan.MatchedInterior != null ? plan.MatchedInterior.Name : "Open-World Ground", plan.MatchedInterior != null ? plan.MatchedInterior.InteriorId : "none");
                Console.WriteLine("   • Center Origin:     ({0:F1}, {1:F1}, {2:F1})", plan.CenterPosition.X, plan.CenterPosition.Y, plan.CenterPosition.Z);
                Console.WriteLine("   • Total Props:       {0}", env.PropIds.Count);
                Console.WriteLine("   • Total NPCs:        {0}", env.NpcIds.Count);
                Console.WriteLine("   • Total Vehicles:    {0}", plan.ResolvedVehicles.Count);

                if (plan.FeasibilityAudit != null)
                {
                    Console.WriteLine("\n   [Asset Knowledge & Spatial Feasibility Pre-Check]:");
                    Console.WriteLine("     • Requested Asset:       {0}", plan.FeasibilityAudit.RequestedAsset);
                    Console.WriteLine("     • Classification:        {0}", plan.FeasibilityAudit.AssetClassification);
                    Console.WriteLine("     • Physical Dimensions:   {0}", plan.FeasibilityAudit.PhysicalDimensions);
                    Console.WriteLine("     • Target Environment:    {0}", plan.FeasibilityAudit.TargetEnvironment);
                    Console.WriteLine("     • Interior Clearance:    {0}", plan.FeasibilityAudit.EnvironmentClearance);
                    Console.WriteLine("     • Conflict Detected:     {0}", plan.FeasibilityAudit.SpatialConflictDetected ? "YES (Volumetric & Clearance Incompatible)" : "NO");
                    Console.WriteLine("     • Conflict Reason:       {0}", plan.FeasibilityAudit.ConflictReason);
                    Console.WriteLine("     • Resolution Strategy:   {0}", plan.FeasibilityAudit.ResolutionStrategy);
                    Console.WriteLine("     • Suggested Realization: {0}", plan.FeasibilityAudit.SuggestedRealization);
                }

                if (plan.CharacterAudit != null)
                {
                    Console.WriteLine("\n   [Celebrity & Canonical Character Registry Search]:");
                    Console.WriteLine("     • Requested Query:       \"{0}\"", plan.CharacterAudit.RequestedQuery);
                    Console.WriteLine("     • Requested Role:        {0}", plan.CharacterAudit.RequestedRole);
                    Console.WriteLine("     • Discovered Character:  {0}", plan.CharacterAudit.ResolvedCharacterName);
                    Console.WriteLine("     • Canonical Ped Model:   {0}", plan.CharacterAudit.ResolvedPedModel);
                    Console.WriteLine("     • DLC / Canonical Era:   {0}", plan.CharacterAudit.CanonicalDlcOrigin);
                    Console.WriteLine("     • Rejection Logic:       {0}", plan.CharacterAudit.RejectionReason);
                    Console.WriteLine("     • Verification Status:   {0}", plan.CharacterAudit.VerificationSummary);
                }

                if (plan.AssetTransferAudit != null)
                {
                    Console.WriteLine("\n   [Ontological Provenance: Location Ownership vs Asset Origin Audit]:");
                    Console.WriteLine("     • Host Environment (Location Ownership): {0}", plan.AssetTransferAudit.HostEnvironment);
                    Console.WriteLine("     • Donor Asset Origin:                    {0}", plan.AssetTransferAudit.DonorAssetOrigin);
                    Console.WriteLine("     • Requested Asset Family:                {0}", plan.AssetTransferAudit.RequestedAssetFamily);
                    Console.WriteLine("     • Resolved Venue Interior:               {0}", plan.AssetTransferAudit.ResolvedVenueInterior);
                    Console.WriteLine("     • Ontological Distinction:               {0}", plan.AssetTransferAudit.OntologicalDistinction);
                    Console.WriteLine("     • Transferred Asset Models:              {0}", string.Join(", ", plan.AssetTransferAudit.TransferredAssetModels.ToArray()));
                }

                if (plan.ArchitecturalLayers != null && plan.ArchitecturalLayers.Count > 0)
                {
                    Console.WriteLine("\n   [Vertical Architectural Hierarchy Decomposition]:");
                    foreach (var layer in plan.ArchitecturalLayers)
                    {
                        Console.WriteLine("     -> Layer {0}: [{1}] '{2}' -> Resolution: {3} (Interior: {4}) Portal: \"{5}\" at ({6:F1}, {7:F1}, {8:F1})",
                            layer.LayerIndex, layer.LayerName, layer.TargetConcept, layer.ResolutionType, layer.AssignedInteriorId, layer.TransitionPortalTitle,
                            layer.WorldCoordinates.X, layer.WorldCoordinates.Y, layer.WorldCoordinates.Z);
                    }
                }

                if (plan.ConnectedPockets != null && plan.ConnectedPockets.Count > 0)
                {
                    Console.WriteLine("\n   [Connected Teleport Pockets & Portals]:");
                    foreach (var pocket in plan.ConnectedPockets)
                    {
                        Console.WriteLine("     -> Portal ID: {0,-18} Title: \"{1}\" | Entrance: ({2:F1}, {3:F1}, {4:F1}) -> Target: ({5:F1}, {6:F1}, {7:F1}) [Access: {8}]",
                            pocket.PocketId, pocket.EntranceTitle, pocket.StreetCoords.X, pocket.StreetCoords.Y, pocket.StreetCoords.Z,
                            pocket.InteriorCoords.X, pocket.InteriorCoords.Y, pocket.InteriorCoords.Z, pocket.LockReason);
                    }
                }

                if (plan.ResolvedVehicles.Count > 0)
                {
                    Console.WriteLine("\n   [Resolved Vehicles in Exhibition]:");
                    foreach (var veh in plan.ResolvedVehicles)
                    {
                        Console.WriteLine("     -> Model: {0,-12} Pos: ({1:F1}, {2:F1}, {3:F1}) Heading: {4:F1}° Color: {5}",
                            veh.ModelName, veh.WorldPosition.X, veh.WorldPosition.Y, veh.WorldPosition.Z, veh.Heading, veh.PrimaryColor);
                    }
                }

                Console.WriteLine("\n   [Resolved Key Props & Spatial Layering]:");
                foreach (var prop in plan.ResolvedProps)
                {
                    Console.WriteLine("     -> Prop: {0,-28} Model: {1,-32} Pos: ({2:F1}, {3:F1}, {4:F1})",
                        prop.PropId, prop.ModelName, prop.WorldPosition.X, prop.WorldPosition.Y, prop.WorldPosition.Z);
                }

                Console.WriteLine("\n   [Resolved NPCs & Behavioral Roles]:");
                foreach (var npc in plan.ResolvedNpcs)
                {
                    Console.WriteLine("     -> NPC: {0,-18} Model: {1,-20} Pos: ({2:F1}, {3:F1}, {4:F1}) Scenario: {5}",
                        npc.Name, npc.PedModel, npc.WorldPosition.X, npc.WorldPosition.Y, npc.WorldPosition.Z, npc.Scenario);
                }

                foreach (var npcId in env.NpcIds)
                {
                    string fact = engine.GetNpcFactForLivingLSAIs(npcId);
                    Console.WriteLine("\n   [NPC Fact Injected into LivingLSAIs]:\n   {0}", fact);
                }

                Console.WriteLine("\n=========================================================");
                Console.WriteLine("  NATURAL LANGUAGE GENERATION TEST: 100% PASSED!         ");
                Console.WriteLine("=========================================================");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ ERROR during generation: " + ex.Message);
                return 1;
            }
        }
    }
}
