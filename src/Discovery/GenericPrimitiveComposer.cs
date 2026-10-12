using System;
using System.Collections.Generic;
using GTA.Math;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Discovery
{
    public class ComposedSubComponent
    {
        public GtaAssetDefinition Asset { get; set; }
        public Vector3Offset RelativeOffset { get; set; }
        public float RelativeHeading { get; set; }
        public string FunctionalRole { get; set; }
        public bool SnapToSurface { get; set; }
    }

    public class ComposedPrimitiveGroup
    {
        public string Concept { get; set; }
        public GtaAssetDefinition PrimaryBase { get; set; }
        public List<ComposedSubComponent> SubComponents { get; set; }
        public string CompositionProvenance { get; set; }
        public float TotalWidth { get; set; }
        public float TotalLength { get; set; }
        public float TotalHeight { get; set; }

        public ComposedPrimitiveGroup()
        {
            SubComponents = new List<ComposedSubComponent>();
        }

        public List<PropPlacementData> GeneratePropPlacements(string baseIdPrefix, Vector3Offset rootOffset, float rootHeading)
        {
            var list = new List<PropPlacementData>();
            if (PrimaryBase == null) return list;

            string rootPropId = baseIdPrefix + "_root";
            list.Add(new PropPlacementData
            {
                PropId = rootPropId,
                ParentPropId = "",
                Model = PrimaryBase.ModelName,
                Offset = rootOffset,
                Heading = rootHeading,
                IsDynamic = false,
                IsNavBlocker = PrimaryBase.BlocksNavMesh
            });

            for (int i = 0; i < SubComponents.Count; i++)
            {
                var sub = SubComponents[i];
                string subPropId = string.Format("{0}_sub_{1}", baseIdPrefix, i + 1);

                list.Add(new PropPlacementData
                {
                    PropId = subPropId,
                    ParentPropId = sub.SnapToSurface ? rootPropId : "",
                    Model = sub.Asset.ModelName,
                    Offset = sub.RelativeOffset,
                    Heading = sub.RelativeHeading,
                    IsDynamic = false,
                    IsNavBlocker = sub.Asset.BlocksNavMesh
                });
            }

            return list;
        }
    }

    /// <summary>
    /// Autonomous Generic Primitive Composition Engine.
    /// Composes compound spatial structures from physical primitives (e.g. DJ setups, heist planning tables,
    /// tactical monitoring desks, black-market display vaults) using functional affordance matching.
    /// </summary>
    public class GenericPrimitiveComposer
    {
        private readonly SpatialAssetRegistry _registry;

        public GenericPrimitiveComposer(SpatialAssetRegistry registry = null)
        {
            _registry = registry ?? SpatialAssetRegistry.Instance;
        }

        public ComposedPrimitiveGroup Compose(string functionalConcept, AssetQuery contextQuery = null)
        {
            var group = new ComposedPrimitiveGroup
            {
                Concept = functionalConcept
            };

            string concept = (functionalConcept ?? "").ToLowerInvariant();

            // 1. DJ Performance Setup (Surface + Mixer/Turntables + Left/Right Speakers)
            if (concept.Contains("dj") || concept.Contains("booth") || concept.Contains("turntable"))
            {
                // Surface Stand
                var surfaceQuery = new AssetQuery
                {
                    Type = AssetType.Prop,
                    RequiredAffordances = new List<string> { "can_be_surface", "supports_objects" },
                    RequiredTags = new List<string> { "dj", "nightclub" }
                };
                group.PrimaryBase = _registry.Resolve(surfaceQuery).PrimaryAsset;

                // Decks / Mixer on top
                var deckQuery = new AssetQuery
                {
                    Type = AssetType.Prop,
                    RequiredAffordances = new List<string> { "dj_equipment" }
                };
                var decks = _registry.Resolve(deckQuery).PrimaryAsset;
                if (decks != null)
                {
                    group.SubComponents.Add(new ComposedSubComponent
                    {
                        Asset = decks,
                        RelativeOffset = new Vector3Offset(0f, 0f, group.PrimaryBase != null ? Math.Max(0.76f, group.PrimaryBase.SurfaceElevation) : 0.85f),
                        RelativeHeading = 0f,
                        FunctionalRole = "turntable_mixer",
                        SnapToSurface = true
                    });
                }

                // Audio projection monitors
                var speakerQuery = new AssetQuery
                {
                    Type = AssetType.Prop,
                    RequiredAffordances = new List<string> { "audio_projection" },
                    RequiredTags = new List<string> { "speaker" }
                };
                var speaker = _registry.Resolve(speakerQuery).PrimaryAsset;
                if (speaker != null)
                {
                    group.SubComponents.Add(new ComposedSubComponent
                    {
                        Asset = speaker,
                        RelativeOffset = new Vector3Offset(-2.2f, 0f, 0f),
                        RelativeHeading = 340f,
                        FunctionalRole = "speaker_monitor_left",
                        SnapToSurface = false
                    });
                    group.SubComponents.Add(new ComposedSubComponent
                    {
                        Asset = speaker,
                        RelativeOffset = new Vector3Offset(2.2f, 0f, 0f),
                        RelativeHeading = 20f,
                        FunctionalRole = "speaker_monitor_right",
                        SnapToSurface = false
                    });
                }

                group.CompositionProvenance = "Composed DJ Setup from 3 Primitives: Base Stand + DJ Decks + Flanking Speakers";
                return group;
            }

            // 2. Heist Tactical Planning Command (Conference Table + Blueprints + Cash Stacks + Laptop)
            if (concept.Contains("planning") || concept.Contains("heist") || concept.Contains("briefing"))
            {
                var tableQuery = new AssetQuery
                {
                    Type = AssetType.Prop,
                    RequiredAffordances = new List<string> { "can_be_surface", "supports_objects" },
                    RequiredTags = new List<string> { "table", "planning" }
                };
                group.PrimaryBase = _registry.Resolve(tableQuery).PrimaryAsset;

                float surfaceZ = group.PrimaryBase != null ? Math.Max(0.76f, group.PrimaryBase.SurfaceElevation) : 0.76f;

                // Tactical Blueprints
                var blueprintQuery = new AssetQuery
                {
                    Type = AssetType.Prop,
                    RequiredTags = new List<string> { "blueprint" }
                };
                var blueprint = _registry.Resolve(blueprintQuery).PrimaryAsset;
                if (blueprint != null)
                {
                    group.SubComponents.Add(new ComposedSubComponent
                    {
                        Asset = blueprint,
                        RelativeOffset = new Vector3Offset(0f, 0f, surfaceZ),
                        RelativeHeading = 0f,
                        FunctionalRole = "heist_blueprint",
                        SnapToSurface = true
                    });
                }

                // Tactical Laptop
                var laptopQuery = new AssetQuery
                {
                    Type = AssetType.Prop,
                    RequiredAffordances = new List<string> { "tactical_planning" },
                    RequiredTags = new List<string> { "laptop" }
                };
                var laptop = _registry.Resolve(laptopQuery).PrimaryAsset;
                if (laptop != null)
                {
                    group.SubComponents.Add(new ComposedSubComponent
                    {
                        Asset = laptop,
                        RelativeOffset = new Vector3Offset(-0.4f, 0.1f, surfaceZ),
                        RelativeHeading = 90f,
                        FunctionalRole = "tactical_laptop",
                        SnapToSurface = true
                    });
                }

                // Heist Cash / Loot Stack
                var cashQuery = new AssetQuery
                {
                    Type = AssetType.Prop,
                    RequiredAffordances = new List<string> { "heist_loot" }
                };
                var cash = _registry.Resolve(cashQuery).PrimaryAsset;
                if (cash != null)
                {
                    group.SubComponents.Add(new ComposedSubComponent
                    {
                        Asset = cash,
                        RelativeOffset = new Vector3Offset(0.4f, -0.1f, surfaceZ),
                        RelativeHeading = 0f,
                        FunctionalRole = "heist_reward",
                        SnapToSurface = true
                    });
                }

                group.CompositionProvenance = "Composed Heist Planning Command from 4 Primitives: Table + Blueprints + Laptop + Cash";
                return group;
            }

            // 3. Command & Control Surveillance Overlook (Desk + Monitors + Server Racks)
            if (concept.Contains("control") || concept.Contains("command") || concept.Contains("surveillance") || concept.Contains("doomsday"))
            {
                var deskQuery = new AssetQuery
                {
                    Type = AssetType.Prop,
                    RequiredAffordances = new List<string> { "command_monitoring", "can_be_surface" },
                    RequiredTags = new List<string> { "desk", "command" }
                };
                group.PrimaryBase = _registry.Resolve(deskQuery).PrimaryAsset;

                float surfaceZ = group.PrimaryBase != null ? Math.Max(0.76f, group.PrimaryBase.SurfaceElevation) : 0.85f;

                var screenQuery = new AssetQuery
                {
                    Type = AssetType.Prop,
                    RequiredAffordances = new List<string> { "surveillance" },
                    RequiredTags = new List<string> { "screens", "monitor" }
                };
                var screens = _registry.Resolve(screenQuery).PrimaryAsset;
                if (screens != null)
                {
                    group.SubComponents.Add(new ComposedSubComponent
                    {
                        Asset = screens,
                        RelativeOffset = new Vector3Offset(0f, 0.2f, surfaceZ),
                        RelativeHeading = 0f,
                        FunctionalRole = "surveillance_screens",
                        SnapToSurface = true
                    });
                }

                var serverQuery = new AssetQuery
                {
                    Type = AssetType.Prop,
                    RequiredTags = new List<string> { "server", "rack" }
                };
                var server = _registry.Resolve(serverQuery).PrimaryAsset;
                if (server != null)
                {
                    group.SubComponents.Add(new ComposedSubComponent
                    {
                        Asset = server,
                        RelativeOffset = new Vector3Offset(-3.2f, 0.5f, 0f),
                        RelativeHeading = 90f,
                        FunctionalRole = "server_rack_flank",
                        SnapToSurface = false
                    });
                }

                group.CompositionProvenance = "Composed Command Control Deck from 3 Primitives: Tactical Desk + Multi-Screens + Server Racks";
                return group;
            }

            // Fallback to direct resolution
            group.PrimaryBase = _registry.Resolve(contextQuery ?? new AssetQuery { Type = AssetType.Prop, RawQuery = functionalConcept }).PrimaryAsset;
            group.CompositionProvenance = "Direct Primitive Resolution: " + (group.PrimaryBase != null ? group.PrimaryBase.ModelName : "none");
            return group;
        }
    }
}
