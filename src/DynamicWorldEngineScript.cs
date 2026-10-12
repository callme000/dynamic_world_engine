using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using DynamicWorldEngine.Archetypes;
using DynamicWorldEngine.Audio;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Observer;
using DynamicWorldEngine.Retrieval;
using DynamicWorldEngine.Runtime;
using DynamicWorldEngine.Services;
using DynamicWorldEngine.Simulation;
using DynamicWorldEngine.Spatial;
using DynamicWorldEngine.UI;
using DynamicWorldEngine.Utils;
using Newtonsoft.Json.Linq;

namespace DynamicWorldEngine
{
    /// <summary>
    /// ScriptHookVDotNet runtime hook.
    /// Acts as a thin coordinator dispatching OnTick, hotkey input, and HUD rendering,
    /// while delegating entity spawning, mutations, and events to dedicated runtime services.
    /// </summary>
    public class DynamicWorldEngineScript : Script
    {
        private readonly EngineCore _engine;
        private bool _isInitialized = false;
        private int _currentArchetypeIndex = 0;

        private readonly EntitySpawnerService _spawner = new EntitySpawnerService();
        private readonly BlipManager _blips = new BlipManager();
        private readonly WorldInspector _inspector = new WorldInspector();
        private readonly NavMeshBlockerManager _navBlockers = new NavMeshBlockerManager();
        private readonly TeleportPocketManager _teleportPockets = new TeleportPocketManager();
        private readonly DirectorTimelineManager _timelineManager = new DirectorTimelineManager();
        private readonly InteriorManager _interiorManager;
        private readonly PartyAudioManager _partyAudio = new PartyAudioManager();
        private readonly NpcActivityDirector _activityDirector = new NpcActivityDirector();
        private readonly RuntimeExecutor _runtimeExecutor;

        private readonly Queue<PendingTask> _taskQueue = new Queue<PendingTask>();
        private readonly object _queueLock = new object();
        private bool _isGenerating = false;

        private DateTime _lastSimulationTick = DateTime.MinValue;
        private readonly TimeSpan _simulationTickInterval = TimeSpan.FromMilliseconds(2000);

        private class PendingTask
        {
            public string JsonPayload { get; set; }
            public string Label { get; set; }
            public string ErrorMessage { get; set; }
            public SpatialOrchestrationResult OrchestrationResult { get; set; }
        }

        public DynamicWorldEngineScript()
        {
            try
            {
                int currentThreadId = Thread.CurrentThread.ManagedThreadId;
                Log.MainThreadId = currentThreadId;
                PlayerTeleportCoordinator.MainThreadId = currentThreadId;
                SpatialPerceptionScanner.MainThreadId = currentThreadId;
                SpatialGridSampler.MainThreadId = currentThreadId;

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string scriptsFolder = Directory.Exists(Path.Combine(baseDir, "scripts"))
                    ? Path.Combine(baseDir, "scripts")
                    : baseDir;

                Log.Initialize(scriptsFolder);
                Log.Info("Script", "DynamicWorldEngineScript starting up... MainThreadId={0}", currentThreadId);
                Log.Info("Script", "Paths: BaseDir=\"{0}\", ScriptsFolder=\"{1}\"", baseDir, scriptsFolder);

                string dataFolder = Path.Combine(scriptsFolder, "DynamicWorldEngineData");
                if (!Directory.Exists(dataFolder) && Directory.Exists(Path.Combine(baseDir, "DynamicWorldEngineData")))
                {
                    dataFolder = Path.Combine(baseDir, "DynamicWorldEngineData");
                }

                string llaiFolder = scriptsFolder;
                if (Directory.Exists(Path.Combine(scriptsFolder, "LLAI SERVER")))
                {
                    llaiFolder = scriptsFolder;
                }
                else if (Directory.Exists(Path.Combine(scriptsFolder, "2234fb-LivingLSAIs4.5")))
                {
                    llaiFolder = Path.Combine(scriptsFolder, "2234fb-LivingLSAIs4.5");
                }

                _interiorManager = new InteriorManager(_teleportPockets, _blips);
                _runtimeExecutor = new RuntimeExecutor(
                    _spawner,
                    _navBlockers,
                    _blips,
                    _teleportPockets,
                    _interiorManager,
                    _partyAudio,
                    _activityDirector,
                    _timelineManager
                );

                // Pass live runtime managers to EngineCore to establish a single authoritative runtime context
                _engine = new EngineCore(dataFolder, llaiFolder, _navBlockers, _blips, _teleportPockets, _interiorManager, _runtimeExecutor);
                _isInitialized = true;

                // Interrogate GTA V installation & discover active DLC ontology
                try
                {
                    SpatialAssetRegistry.Instance.InitializeRegistry(baseDir);
                    Log.Info("Script", "SpatialAssetRegistry initialized successfully.");
                }
                catch (Exception ex)
                {
                    Log.Warn("Script", "Failed initializing SpatialAssetRegistry: {0}", ex.Message);
                }

                // Enable Online Multiplayer DLC Map Assets in Singleplayer
                try
                {
                    Function.Call(Hash.ON_ENTER_MP);
                    Log.Debug("Script", "ON_ENTER_MP called successfully.");
                }
                catch (Exception ex)
                {
                    Log.Warn("Script", "Failed calling ON_ENTER_MP: {0}", ex.Message);
                }

                KeyDown += OnKeyDown;
                Tick += OnTick;
                Interval = 20; // 20ms for ultra-responsive 0ms observer loop & HUD drawing

                Log.Info("Script", "Dynamic World Engine v8.0 loaded cleanly. Ready for player prompts.");
                Notification.Show("~g~Dynamic World Engine v8.0 (GPT-6 Astra Edition)~w~ Loaded!~n~~y~F7:~w~ Direct Story/Events (World Director) | ~y~F8:~w~ Generate Scene (World Architect) | ~y~F9:~w~ Inspector HUD | ~y~F10:~w~ Presets | ~y~F11:~w~ Clear");
            }
            catch (Exception ex)
            {
                Log.Fatal("Script", ex, "Fatal exception during DynamicWorldEngineScript constructor");
                Notification.Show("~r~Dynamic World Engine Error:~w~ " + ex.Message);
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!_isInitialized) return;

            try
            {
                Ped playerPed = null;
                try
                {
                    if (Game.Player != null && Game.Player.Character != null && Game.Player.Character.Exists())
                    {
                        playerPed = Game.Player.Character;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("OnTick", "Failed accessing Player.Character: {0}", ex.Message);
                }

                if (playerPed == null) return;

                DateTime now = DateTime.UtcNow;

                // 1. Instant 0ms Closed-Loop World Observer
                try
                {
                    _engine.Observer.ObserveWorld(playerPed, _spawner.ActiveNpcContexts);
                }
                catch (Exception ex)
                {
                    Log.Error("Observer", ex, "Error in ObserveWorld");
                }

                // 2. Draw World Inspector HUD if enabled (F9)
                try
                {
                    _inspector.DrawInspectorHud(_engine.Store, _spawner.ActiveNpcContexts);
                }
                catch (Exception ex)
                {
                    Log.Error("HUD", ex, "Error in DrawInspectorHud");
                }

                // 3. Process Sky / Void Teleport Pockets & Interior Entrances
                try
                {
                    _teleportPockets.ProcessPocketTransitions(playerPed);
                }
                catch (Exception ex)
                {
                    Log.Error("Pockets", ex, "Error in ProcessPocketTransitions");
                }

                // 4. Process Scenario Director Timeline Beats (Timers, Proximity, Weapons, Combat)
                try
                {
                    _timelineManager.ProcessTimeline(playerPed, ExecuteWorldEventDirect, ExecuteEntityMutationDirect);
                }
                catch (Exception ex)
                {
                    Log.Error("Timeline", ex, "Error in ProcessTimeline");
                }

                // 5. Suppress unwanted ambient GTA wanted levels when player is an innocent bystander
                try
                {
                    if (Game.Player.WantedLevel > 0)
                    {
                        bool playerAttackedPolice = false;
                        foreach (var ctx in _spawner.ActiveNpcContexts)
                        {
                            if (ctx != null && ctx.IsValid && ctx.Record.PublicFaction == "Police" && ctx.PedInstance.HasBeenDamagedBy(playerPed))
                            {
                                playerAttackedPolice = true;
                                break;
                            }
                        }

                        if (!playerAttackedPolice)
                        {
                            Game.Player.WantedLevel = 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("WantedLevel", "Error checking wanted level: {0}", ex.Message);
                }

                // 5b. Autonomous Multi-Stop NPC Activity Director Tick
                try
                {
                    _activityDirector.OnTick();
                }
                catch (Exception ex)
                {
                    Log.Error("ActivityDirector", ex, "Error in ActivityDirector.OnTick");
                }

                // 6. Autonomous World Simulation Tick (every 2 seconds)
                if ((now - _lastSimulationTick) >= _simulationTickInterval)
                {
                    _lastSimulationTick = now;

                    try
                    {
                        List<EventRecord> newEvents = _engine.Simulation.PerformAutonomousWorldTick();

                        if (newEvents != null && newEvents.Count > 0)
                        {
                            foreach (var evt in newEvents)
                            {
                                if (evt.Description.Contains("dispatch") || evt.Description.Contains("police"))
                                {
                                    Notification.Show(string.Format("~r~[Simulation Emergence]~w~~n~{0}", evt.Description));
                                }
                            }
                        }

                        foreach (var ctx in _spawner.ActiveNpcContexts)
                        {
                            if (ctx != null && ctx.IsValid)
                            {
                                NpcStateMachine.ApplyPhysicalBehavior(ctx.PedInstance, ctx.Record, playerPed, ctx);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Simulation", ex, "Error in autonomous simulation tick");
                    }
                }

                // 7. Process Background Generation Tasks on Main Game Thread
                lock (_queueLock)
                {
                    if (_taskQueue.Count > 0)
                    {
                        PendingTask task = _taskQueue.Dequeue();
                        Log.Breadcrumb("Processing dequeued task: {0}", task.Label);
                        if (!string.IsNullOrEmpty(task.ErrorMessage))
                        {
                            Log.Error("TaskQueue", null, "Task reported error: {0}", task.ErrorMessage);
                            Notification.Show(string.Format("~r~[AI Generation Error]:~w~ {0}", task.ErrorMessage));
                        }
                        else if (task.OrchestrationResult != null && task.OrchestrationResult.Success && task.OrchestrationResult.Environment != null)
                        {
                            try
                            {
                                var targetLoc = task.OrchestrationResult.LocationResult != null ? task.OrchestrationResult.LocationResult.SelectedLocation : null;
                                if (targetLoc != null && playerPed != null && playerPed.Exists())
                                {
                                    Log.Info("Realize", "Anchoring player at {0} (Heading {1})", targetLoc.Coordinates, targetLoc.Heading);
                                    _engine.AnchorPhysicalContext(targetLoc.Coordinates, targetLoc.Heading, waitCollision: false, fadeScreen: false);
                                }

                                Log.Info("Realize", "Realizing environment \"{0}\" (PlanId={1})...", 
                                    task.OrchestrationResult.Environment.EnvironmentId, 
                                    task.OrchestrationResult.Environment.PlanId);

                                _engine.Realize(task.OrchestrationResult.Environment.EnvironmentId, playerPed);
                                int propsCount = _engine.Runtime.GetPropsForEnvironment(task.OrchestrationResult.Environment.EnvironmentId).Count;
                                int npcsCount = _engine.Runtime.GetNpcsForEnvironment(task.OrchestrationResult.Environment.EnvironmentId).Count;
                                int vehsCount = _engine.Runtime.GetVehiclesForEnvironment(task.OrchestrationResult.Environment.EnvironmentId).Count;

                                string locName = targetLoc != null
                                    ? targetLoc.DisplayName
                                    : task.Label;

                                Log.Info("Realize", "Realization complete! Props={0}, Vehicles={1}, NPCs={2}", propsCount, vehsCount, npcsCount);
                                
                                Notification.Show(string.Format("~g~[Spatial Orchestration Realized]~w~ {0}!~n~~y~Props:~w~ {1} | ~y~Vehicles:~w~ {2} | ~y~Cast:~w~ {3}",
                                    locName, propsCount, vehsCount, npcsCount));
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Realize", ex, "Exception during environment realization in OnTick");
                                Notification.Show("~r~[Realization Error]:~w~ " + ex.Message);
                            }
                        }
                        else if (!string.IsNullOrEmpty(task.JsonPayload))
                        {
                            DispatchRawJsonAction(task.JsonPayload, task.Label);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Fatal("OnTick", ex, "Fatal exception caught in outer OnTick frame");
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isInitialized) return;

            try
            {
                // F7: AI World Director (Direct Multi-Beat Storylines, Ambience, Raids, or Character Mutations)
                // Shift + F7: Instantly direct Storyline / Event / Mutation from Windows Clipboard
                if (e.KeyCode == Keys.F7)
                {
                    Log.Breadcrumb("KeyDown: F7 (Shift={0})", e.Shift);
                    if (e.Shift)
                    {
                        TriggerEventOrMutationFromClipboard();
                    }
                    else
                    {
                        OpenEventOrMutationPromptBox();
                    }
                }
                // F8: AI World Architect (Pure Spatial Scene Generation at t = 0)
                // Shift + F8: Instantly generate scene directly from Windows Clipboard
                else if (e.KeyCode == Keys.F8)
                {
                    Log.Breadcrumb("KeyDown: F8 (Shift={0})", e.Shift);
                    if (e.Shift)
                    {
                        GenerateSceneFromClipboard();
                    }
                    else
                    {
                        OpenNaturalLanguagePromptBox();
                    }
                }
                // F9: Toggle Epistemic Memory & Live Scenario Inspector HUD
                else if (e.KeyCode == Keys.F9)
                {
                    Log.Breadcrumb("KeyDown: F9 (Toggle Inspector HUD)");
                    _inspector.Toggle();
                }
                // F10: Cycle Zero-Cost Built-in Presets
                else if (e.KeyCode == Keys.F10)
                {
                    Log.Breadcrumb("KeyDown: F10 (Cycle Presets)");
                    CycleBuiltInPresets();
                }
                // F11: Clean Up Active World Entities & Reset Scenarios
                else if (e.KeyCode == Keys.F11)
                {
                    Log.Breadcrumb("KeyDown: F11 (Clear Entities)");
                    ClearSpawnedWorldEntities();
                    Notification.Show("~y~[Dynamic World Engine]~w~ Cleared all active props, NPCs, scenario timelines, and pockets!");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Input", ex, "Unhandled exception in OnKeyDown for key {0}", e.KeyCode);
            }
        }

        private void CycleBuiltInPresets()
        {
            try
            {
                string[] presetNames = new string[] {
                    "Illegal Backroom Gambling Den",
                    "Underground Chop Shop & Mechanic",
                    "Vespucci Beach Street Food Vendor",
                    "LSPD Tactical Highway Roadblock",
                    "Grove Street Barbecue Cookout"
                };

                string[] presetJsons = new string[] {
                    EnvironmentCatalog.GetGamblingDenJson(),
                    EnvironmentCatalog.GetChopShopJson(),
                    EnvironmentCatalog.GetStreetVendorJson(),
                    EnvironmentCatalog.GetPoliceRoadblockJson(),
                    EnvironmentCatalog.GetBbqPartyJson()
                };

                _currentArchetypeIndex = (_currentArchetypeIndex + 1) % presetJsons.Length;
                string selectedName = presetNames[_currentArchetypeIndex];
                string selectedJson = presetJsons[_currentArchetypeIndex];

                Log.Info("Presets", "Cycling to preset: \"{0}\"", selectedName);
                Notification.Show(string.Format("~b~[Preset Selected]:~w~ {0}", selectedName));
                SpawnEnvironmentFromJson(selectedJson, selectedName);
            }
            catch (Exception ex)
            {
                Log.Error("Presets", ex, "Exception cycling built-in presets");
                Notification.Show("~r~[Preset Error]:~w~ " + ex.Message);
            }
        }

        private string GetCustomUserInput(string defaultText = "", int maxChars = 255)
        {
            try
            {
                Log.Breadcrumb("Opening onscreen keyboard dialog...");
                Function.Call(Hash.DISPLAY_ONSCREEN_KEYBOARD, 6, "FMMC_KEY_TIP8", "", defaultText ?? "", "", "", "", maxChars);
                int status = 0;
                while ((status = Function.Call<int>(Hash.UPDATE_ONSCREEN_KEYBOARD)) == 0)
                {
                    // Suppress game control actions every frame so typing 'E', 'T', space, etc.
                    // does not trigger LivingLSAIs NPC interactions, text chat, attacks, or movement
                    Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 0);
                    Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 1);
                    Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 2);
                    Script.Wait(0);
                }

                if (status == 1)
                {
                    string text = Function.Call<string>(Hash.GET_ONSCREEN_KEYBOARD_RESULT);
                    Log.Breadcrumb("Onscreen keyboard confirmed: \"{0}\"", text);
                    return text;
                }

                Log.Breadcrumb("Onscreen keyboard closed without submission (status={0})", status);
                return string.Empty;
            }
            catch (Exception ex)
            {
                Log.Error("UI", ex, "Exception during onscreen keyboard input");
                return string.Empty;
            }
        }

        private void GenerateSceneFromClipboard()
        {
            if (_isGenerating)
            {
                Log.Warn("Prompt", "GenerateSceneFromClipboard ignored - already generating a scene.");
                Notification.Show("~y~[AI World Architect]~w~ Already generating a scene. Please wait.");
                return;
            }

            string clipboardText = ClipboardHelper.GetClipboardText();
            if (string.IsNullOrEmpty(clipboardText) || clipboardText.Trim().Length < 3)
            {
                Log.Warn("Prompt", "GenerateSceneFromClipboard ignored - clipboard is empty or too short.");
                Notification.Show("~y~[AI World Architect]~w~ Clipboard is empty! Copy a prompt with ~b~Ctrl+C~w~ in Windows first.");
                return;
            }

            string userPrompt = clipboardText.Trim();
            Log.Breadcrumb("GenerateSceneFromClipboard: \"{0}\"", userPrompt);
            Log.Info("Prompt", "Generating scene from clipboard: \"{0}\"", userPrompt);
            string preview = userPrompt.Length > 60 ? (userPrompt.Substring(0, 57) + "...") : userPrompt;
            Notification.Show(string.Format("~g~[AI World Architect]~w~ Generating scene from clipboard:~n~\"~b~{0}~w~\"...", preview));
            _isGenerating = true;

            Vector3 playerPos = Vector3.Zero;
            float playerHeading = 0f;
            try
            {
                if (Game.Player != null && Game.Player.Character != null)
                {
                    playerPos = Game.Player.Character.Position;
                    playerHeading = Game.Player.Character.Heading;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Prompt", "Failed to retrieve player position: {0}", ex.Message);
            }

            // Stage 1 & 2: Macro-location discovery & Spatial Perception ON MAIN THREAD
            LocationSelectionResult locResult = null;
            SpatialWorldModel worldModel = null;
            try
            {
                Log.Breadcrumb("Discovering location for prompt: \"{0}\"", userPrompt);
                locResult = _engine.DiscoverAndSelectLocation(userPrompt, playerPos, playerHeading);
                var selectedLoc = locResult != null ? locResult.SelectedLocation : null;
                if (selectedLoc != null)
                {
                    Log.Info("Prompt", "Anchoring location candidate: {0} ({1})", selectedLoc.DisplayName, selectedLoc.Coordinates);
                    Notification.Show(string.Format("~b~[Spatial Pipeline]~w~ Anchoring ~y~{0}~w~...", selectedLoc.DisplayName));
                    worldModel = _engine.PerceptionScanner.ScanSpatialWorldModel(selectedLoc.Coordinates, selectedLoc.DefaultRadius, _engine.Store, _engine.Simulation.Recomputer);
                }
                else
                {
                    Log.Info("Prompt", "No macro-location candidate resolved. Scanning around player origin: {0}", playerPos);
                    worldModel = _engine.PerceptionScanner.ScanSpatialWorldModel(playerPos, 50.0f, _engine.Store, _engine.Simulation.Recomputer);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Prompt", ex, "Exception during main-thread location discovery/perception");
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Log.Breadcrumb("Background scene generation started for prompt: \"{0}\"", userPrompt);
                    var orchResult = _engine.ExecuteOrchestrationPipeline(
                        userPrompt,
                        playerPos,
                        playerHeading,
                        autoRealize: false,
                        preScannedModel: worldModel,
                        preResolvedLocation: locResult);

                    lock (_queueLock)
                    {
                        if (orchResult != null && orchResult.Plan != null)
                        {
                            Log.Info("Prompt", "Orchestration pipeline succeeded! Enqueuing task with plan \"{0}\"", orchResult.Plan.Name);
                            _taskQueue.Enqueue(new PendingTask { 
                                JsonPayload = orchResult.GeneratedSceneJson, 
                                Label = userPrompt,
                                OrchestrationResult = orchResult
                            });
                        }
                        else
                        {
                            Log.Warn("Prompt", "Orchestration plan was null. Falling back to direct JSON generation.");
                            string generatedJson = _engine.GenerateSceneJsonFromNaturalLanguage(userPrompt);
                            _taskQueue.Enqueue(new PendingTask { JsonPayload = generatedJson, Label = userPrompt });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Prompt", ex, "Exception during background scene generation");
                    lock (_queueLock)
                    {
                        _taskQueue.Enqueue(new PendingTask { ErrorMessage = ex.Message });
                    }
                }
                finally
                {
                    _isGenerating = false;
                    Log.Breadcrumb("Background scene generation finished (Prompt: \"{0}\")", userPrompt);
                }
            });
        }

        private void TriggerEventOrMutationFromClipboard()
        {
            if (_isGenerating)
            {
                Log.Warn("Director", "TriggerEventOrMutationFromClipboard ignored - already processing an event.");
                Notification.Show("~y~[AI World Director]~w~ Already processing an event. Please wait.");
                return;
            }

            string clipboardText = ClipboardHelper.GetClipboardText();
            if (string.IsNullOrEmpty(clipboardText) || clipboardText.Trim().Length < 3)
            {
                Log.Warn("Director", "TriggerEventOrMutationFromClipboard ignored - clipboard is empty or too short.");
                Notification.Show("~y~[AI World Director]~w~ Clipboard is empty! Copy an event prompt with ~b~Ctrl+C~w~ in Windows first.");
                return;
            }

            string userPrompt = clipboardText.Trim();
            Log.Breadcrumb("TriggerEventOrMutationFromClipboard: \"{0}\"", userPrompt);
            Log.Info("Director", "Directing storyline/event from clipboard: \"{0}\"", userPrompt);
            string preview = userPrompt.Length > 60 ? (userPrompt.Substring(0, 57) + "...") : userPrompt;
            Notification.Show(string.Format("~g~[AI World Director]~w~ Directing story/event from clipboard:~n~\"~b~{0}~w~\"...", preview));
            _isGenerating = true;

            GTA.Math.Vector3? playerPos = null;
            try { if (Game.Player != null && Game.Player.Character != null) playerPos = Game.Player.Character.Position; } catch {}
            string worldContext = _engine.GetScopedWorldFactSummary(userPrompt, null, playerPos);
            try
            {
                if (playerPos.HasValue)
                {
                    var perception = _engine.GetPerceptionSnapshot(playerPos.Value.X, playerPos.Value.Y, playerPos.Value.Z);
                    if (perception != null)
                    {
                        string pText = perception.FormatForPrompt();
                        if (!string.IsNullOrEmpty(pText))
                        {
                            worldContext = string.IsNullOrEmpty(worldContext)
                                ? pText
                                : (pText + "\n\n" + worldContext);
                        }
                    }

                    var brief = _engine.GetKairosSituationBrief(playerPos.Value.X, playerPos.Value.Y, playerPos.Value.Z);
                    if (brief != null)
                    {
                        string briefText = brief.FormatForPrompt();
                        if (!string.IsNullOrEmpty(briefText))
                        {
                            worldContext = string.IsNullOrEmpty(worldContext)
                                ? briefText
                                : (briefText + "\n\n" + worldContext);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Director", "Failed retrieving situation brief: {0}", ex.Message);
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Log.Breadcrumb("Background director generation started for: \"{0}\"", userPrompt);
                    string json = _engine.GenerateDirectorJsonFromNaturalLanguage(userPrompt, worldContext);
                    lock (_queueLock)
                    {
                        Log.Info("Director", "Director JSON generated successfully for: \"{0}\"", userPrompt);
                        _taskQueue.Enqueue(new PendingTask { JsonPayload = json, Label = userPrompt });
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Director", ex, "Exception during background director generation");
                    lock (_queueLock)
                    {
                        _taskQueue.Enqueue(new PendingTask { ErrorMessage = ex.Message });
                    }
                }
                finally
                {
                    _isGenerating = false;
                    Log.Breadcrumb("Background director generation finished (Prompt: \"{0}\")", userPrompt);
                }
            });
        }

        private void OpenEventOrMutationPromptBox()
        {
            if (_isGenerating)
            {
                Log.Warn("Director", "OpenEventOrMutationPromptBox ignored - already processing an event.");
                Notification.Show("~y~[AI World Director]~w~ Already processing an event. Please wait.");
                return;
            }

            try
            {
                Log.Breadcrumb("Opening event/mutation prompt box...");
                string clipText = ClipboardHelper.GetClipboardText();
                string initialText = (!string.IsNullOrEmpty(clipText) && clipText.Length <= 250) ? clipText : "";
                string userPrompt = GetCustomUserInput(initialText, 255);
                if (string.IsNullOrEmpty(userPrompt) || userPrompt.Trim().Length < 3)
                {
                    Log.Breadcrumb("Event/mutation prompt was empty or cancelled.");
                    return;
                }

                Log.Breadcrumb("OpenEventOrMutationPromptBox submitted: \"{0}\"", userPrompt);
                Log.Info("Director", "Directing story/event: \"{0}\"", userPrompt);
                Notification.Show(string.Format("~y~[AI World Director]~w~ Directing story/event:~n~\"~b~{0}~w~\"...", userPrompt));
                _isGenerating = true;

                GTA.Math.Vector3? playerPos = null;
                try { if (Game.Player != null && Game.Player.Character != null) playerPos = Game.Player.Character.Position; } catch {}
                string worldContext = _engine.GetScopedWorldFactSummary(userPrompt, null, playerPos);
                try
                {
                    if (playerPos.HasValue)
                    {
                        var perception = _engine.GetPerceptionSnapshot(playerPos.Value.X, playerPos.Value.Y, playerPos.Value.Z);
                        if (perception != null)
                        {
                            string pText = perception.FormatForPrompt();
                            if (!string.IsNullOrEmpty(pText))
                            {
                                worldContext = string.IsNullOrEmpty(worldContext)
                                    ? pText
                                    : (pText + "\n\n" + worldContext);
                            }
                        }

                        var brief = _engine.GetKairosSituationBrief(playerPos.Value.X, playerPos.Value.Y, playerPos.Value.Z);
                        if (brief != null)
                        {
                            string briefText = brief.FormatForPrompt();
                            if (!string.IsNullOrEmpty(briefText))
                            {
                                worldContext = string.IsNullOrEmpty(worldContext)
                                    ? briefText
                                    : (briefText + "\n\n" + worldContext);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("Director", "Failed retrieving situational perception: {0}", ex.Message);
                }

                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        Log.Breadcrumb("Background director generation started for: \"{0}\"", userPrompt);
                        string json = _engine.GenerateDirectorJsonFromNaturalLanguage(userPrompt, worldContext);
                        lock (_queueLock)
                        {
                            Log.Info("Director", "Director JSON generated successfully for: \"{0}\"", userPrompt);
                            _taskQueue.Enqueue(new PendingTask { JsonPayload = json, Label = userPrompt });
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Director", ex, "Exception during background director generation");
                        lock (_queueLock)
                        {
                            _taskQueue.Enqueue(new PendingTask { ErrorMessage = ex.Message });
                        }
                    }
                    finally
                    {
                        _isGenerating = false;
                        Log.Breadcrumb("Background director generation finished (Prompt: \"{0}\")", userPrompt);
                    }
                });
            }
            catch (Exception ex)
            {
                _isGenerating = false;
                Log.Error("Director", ex, "Exception opening event/mutation prompt box");
                Notification.Show("~r~[AI World Director Error]:~w~ " + ex.Message);
            }
        }

        private void OpenNaturalLanguagePromptBox()
        {
            if (_isGenerating)
            {
                Log.Warn("Prompt", "OpenNaturalLanguagePromptBox ignored - already generating a scene.");
                Notification.Show("~y~[AI World Architect]~w~ Already generating a scene. Please wait.");
                return;
            }

            try
            {
                Log.Breadcrumb("Opening scene architect prompt box...");
                string clipText = ClipboardHelper.GetClipboardText();
                string initialText = (!string.IsNullOrEmpty(clipText) && clipText.Length <= 250) ? clipText : "";
                string userPrompt = GetCustomUserInput(initialText, 255);
                if (string.IsNullOrEmpty(userPrompt) || userPrompt.Trim().Length < 3)
                {
                    Log.Breadcrumb("Scene architect prompt was empty or cancelled.");
                    return;
                }

                Log.Breadcrumb("OpenNaturalLanguagePromptBox submitted: \"{0}\"", userPrompt);
                Log.Info("Prompt", "Generating scene for prompt: \"{0}\"", userPrompt);
                Notification.Show(string.Format("~y~[AI World Architect]~w~ Generating scene for:~n~\"~b~{0}~w~\"...", userPrompt));
                _isGenerating = true;

                Vector3 playerPos = Vector3.Zero;
                float playerHeading = 0f;
                try
                {
                    if (Game.Player != null && Game.Player.Character != null)
                    {
                        playerPos = Game.Player.Character.Position;
                        playerHeading = Game.Player.Character.Heading;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("Prompt", "Failed to get player position: {0}", ex.Message);
                }

                // Stage 1 & 2: Macro-location discovery & Spatial Perception ON MAIN THREAD
                LocationSelectionResult locResult = null;
                SpatialWorldModel worldModel = null;
                try
                {
                    Log.Breadcrumb("Discovering location for prompt: \"{0}\"", userPrompt);
                    locResult = _engine.DiscoverAndSelectLocation(userPrompt, playerPos, playerHeading);
                    var selectedLoc = locResult != null ? locResult.SelectedLocation : null;
                    if (selectedLoc != null)
                    {
                        Log.Info("Prompt", "Anchoring location candidate: {0} ({1})", selectedLoc.DisplayName, selectedLoc.Coordinates);
                        Notification.Show(string.Format("~b~[Spatial Pipeline]~w~ Anchoring ~y~{0}~w~...", selectedLoc.DisplayName));
                        worldModel = _engine.PerceptionScanner.ScanSpatialWorldModel(selectedLoc.Coordinates, selectedLoc.DefaultRadius, _engine.Store, _engine.Simulation.Recomputer);
                    }
                    else
                    {
                        Log.Info("Prompt", "No macro-location candidate resolved. Scanning around player origin: {0}", playerPos);
                        worldModel = _engine.PerceptionScanner.ScanSpatialWorldModel(playerPos, 50.0f, _engine.Store, _engine.Simulation.Recomputer);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Prompt", ex, "Exception during main-thread location discovery/perception");
                }

                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        Log.Breadcrumb("Background scene generation started for prompt: \"{0}\"", userPrompt);
                        var orchResult = _engine.ExecuteOrchestrationPipeline(
                            userPrompt,
                            playerPos,
                            playerHeading,
                            autoRealize: false,
                            preScannedModel: worldModel,
                            preResolvedLocation: locResult);

                        lock (_queueLock)
                        {
                            if (orchResult != null && orchResult.Plan != null)
                            {
                                Log.Info("Prompt", "Orchestration pipeline succeeded! Enqueuing task with plan \"{0}\"", orchResult.Plan.Name);
                                _taskQueue.Enqueue(new PendingTask { 
                                    JsonPayload = orchResult.GeneratedSceneJson, 
                                    Label = userPrompt,
                                    OrchestrationResult = orchResult
                                });
                            }
                            else
                            {
                                Log.Warn("Prompt", "Orchestration plan was null. Falling back to direct JSON generation.");
                                string generatedJson = _engine.GenerateSceneJsonFromNaturalLanguage(userPrompt);
                                _taskQueue.Enqueue(new PendingTask { JsonPayload = generatedJson, Label = userPrompt });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Prompt", ex, "Exception during background scene generation");
                        lock (_queueLock)
                        {
                            _taskQueue.Enqueue(new PendingTask { ErrorMessage = ex.Message });
                        }
                    }
                    finally
                    {
                        _isGenerating = false;
                        Log.Breadcrumb("Background scene generation finished (Prompt: \"{0}\")", userPrompt);
                    }
                });
            }
            catch (Exception ex)
            {
                _isGenerating = false;
                Log.Error("Prompt", ex, "Exception in OpenNaturalLanguagePromptBox");
                Notification.Show("~r~[AI World Architect Error]:~w~ " + ex.Message);
            }
        }

        private void DispatchRawJsonAction(string jsonPayload, string label)
        {
            try
            {
                Log.Breadcrumb("DispatchRawJsonAction: label=\"{0}\"", label);
                jsonPayload = JsonRepairUtility.RepairAndExtractJson(jsonPayload);
                JObject root = JObject.Parse(jsonPayload);
                string action = root.Value<string>("action") ?? "";
                Log.Info("Action", "Dispatching action \"{0}\" for label \"{1}\"", action, label);

                if (string.Equals(action, "DIRECT_SCENARIO", StringComparison.OrdinalIgnoreCase))
                {
                    ExecuteDirectScenario(jsonPayload);
                }
                else if (string.Equals(action, "TRIGGER_WORLD_EVENT", StringComparison.OrdinalIgnoreCase))
                {
                    ExecuteWorldEvent(jsonPayload);
                }
                else if (string.Equals(action, "MUTATE_ENTITY_STATE", StringComparison.OrdinalIgnoreCase))
                {
                    ExecuteEntityMutation(jsonPayload);
                }
                else if (string.Equals(action, "ATTACH_QUEST_OR_STASH", StringComparison.OrdinalIgnoreCase))
                {
                    ExecuteCreateStash(jsonPayload);
                }
                else
                {
                    SpawnEnvironmentFromJson(jsonPayload, label);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Action", ex, "Error dispatching action for label \"{0}\"", label);
                Notification.Show("~r~[Action Dispatch Error]:~w~ " + ex.Message);
            }
        }

        private void ExecuteDirectScenario(string jsonPayload)
        {
            try
            {
                Log.Breadcrumb("Executing DIRECT_SCENARIO...");
                DirectScenarioPayload payload = _engine.Parser.ParseDirectScenario(jsonPayload);
                var data = payload.Data;
                if (data == null) return;
                _engine.DirectScenario(data);
                Log.Info("Director", "DIRECT_SCENARIO activated: {0} ({1} beats loaded)",
                    string.IsNullOrEmpty(data.ScenarioTitle) ? "Active Scenario" : data.ScenarioTitle,
                    data.Beats != null ? data.Beats.Count : 0);
                Notification.Show(string.Format("~y~[World Director]~w~ Scenario: {0} ({1} beats loaded)",
                    string.IsNullOrEmpty(data.ScenarioTitle) ? "Active Scenario" : data.ScenarioTitle,
                    data.Beats != null ? data.Beats.Count : 0));
            }
            catch (Exception ex)
            {
                Log.Error("Director", ex, "Exception executing direct scenario");
            }
        }

        private void ExecuteWorldEvent(string jsonPayload)
        {
            try
            {
                Log.Breadcrumb("Executing TRIGGER_WORLD_EVENT...");
                TriggerWorldEventPayload payload = _engine.Parser.ParseTriggerWorldEvent(jsonPayload);
                var data = payload.Data;
                if (data == null) return;
                ExecuteWorldEventDirect(data);
            }
            catch (Exception ex)
            {
                Log.Error("Event", ex, "Exception executing world event");
            }
        }

        public void ExecuteWorldEventDirect(TriggerWorldEventData data)
        {
            try
            {
                Log.Info("Event", "Executing world event: {0}", data != null ? data.EventTitle : "Unknown");
                WorldEventDispatcher.ExecuteWorldEventDirect(data, _spawner, _blips);
            }
            catch (Exception ex)
            {
                Log.Error("Event", ex, "Exception in ExecuteWorldEventDirect");
            }
        }

        private void ExecuteEntityMutation(string jsonPayload)
        {
            try
            {
                Log.Breadcrumb("Executing MUTATE_ENTITY_STATE...");
                NpcRecord modifiedNpc;
                bool ok = _engine.ExecuteMutateEntityCommand(jsonPayload, out modifiedNpc);

                MutateEntityPayload payload = _engine.Parser.ParseMutateEntity(jsonPayload);
                var data = payload.Data;
                if (data == null) return;
                ExecuteEntityMutationDirect(data);
            }
            catch (Exception ex)
            {
                Log.Error("Mutation", ex, "Exception executing entity mutation");
            }
        }

        public void ExecuteEntityMutationDirect(MutateEntityData data)
        {
            try
            {
                Log.Info("Mutation", "Applying entity mutation to target: {0}", data != null ? data.TargetEntityId : "Unknown");
                EntityMutationHandler.ExecuteEntityMutationDirect(data, _spawner.ActiveNpcContexts);
            }
            catch (Exception ex)
            {
                Log.Error("Mutation", ex, "Exception in ExecuteEntityMutationDirect");
            }
        }

        private void ExecuteCreateStash(string jsonPayload)
        {
            try
            {
                Log.Breadcrumb("Executing ATTACH_QUEST_OR_STASH...");
                CreateStashPayload payload = _engine.Parser.ParseCreateStash(jsonPayload);
                var data = payload.Data;
                if (data == null) return;
                _runtimeExecutor.RealizeStash(data, Game.Player.Character, _engine.Store);
            }
            catch (Exception ex)
            {
                Log.Error("Stash", ex, "Exception executing create stash");
            }
        }

        public void SpawnEnvironmentFromJson(string jsonPayload, string label = "Custom Environment")
        {
            try
            {
                Log.Breadcrumb("SpawnEnvironmentFromJson: \"{0}\"", label);
                Ped playerPed = Game.Player.Character;
                Vector3 playerPos = playerPed.Position;
                float playerHeading = playerPed.Heading;

                BuildEnvironmentPayload payload = _engine.Parser.ParseBuildEnvironment(jsonPayload);
                var data = payload.Data;

                // 1. Unified SpatialWorldPlanner resolves location, clearance, assets, hierarchy & nav blockers
                Log.Info("Spawner", "Planning environment from JSON for \"{0}\"...", label);
                SpatialExecutionPlan plan = _engine.Planner.PlanEnvironment(data, playerPos, playerHeading);

                // 2. Commission environment: persist physical plan in PlanRepository & enroll world state
                Log.Info("Spawner", "Commissioning environment \"{0}\"...", plan.PlanId);
                EnvironmentRecord envRecord = _engine.CommissionEnvironment(plan, data);

                // 3. Authoritatively realize environment from CanonicalPlan and persist REALIZED lifecycle state
                Log.Info("Spawner", "Realizing environment \"{0}\"...", envRecord.EnvironmentId);
                envRecord = _engine.Realize(envRecord.EnvironmentId, playerPed, data);

                int propsCount = _engine.Runtime.GetPropsForEnvironment(envRecord.EnvironmentId).Count;
                int npcsCount = _engine.Runtime.GetNpcsForEnvironment(envRecord.EnvironmentId).Count;
                int vehsCount = _engine.Runtime.GetVehiclesForEnvironment(envRecord.EnvironmentId).Count;

                string sceneName = plan.MatchedInterior != null ? plan.MatchedInterior.Name : plan.Name;
                string atmoInfo = (plan.Atmosphere != null && !string.IsNullOrEmpty(plan.Atmosphere.Weather))
                    ? string.Format(" | ~c~{0}~w~", plan.Atmosphere.Weather)
                    : "";
                Log.Info("Spawner", "SpawnEnvironmentFromJson completed: {0} props, {1} npcs, {2} vehs", propsCount, npcsCount, vehsCount);
                Notification.Show(string.Format("~g~[Scene Directed] {0}!~w~~n~~y~Props:~w~ {1} | ~y~Vehicles:~w~ {2} | ~y~Cast:~w~ {3}{4}~n~~b~Fact profiles hot-synced to LivingLSAIs!",
                    sceneName, propsCount, vehsCount, npcsCount, atmoInfo));
            }
            catch (Exception ex)
            {
                Log.Error("Spawner", ex, "Exception during SpawnEnvironmentFromJson for \"{0}\"", label);
                Notification.Show("~r~[DWE Spatial Error]:~w~ " + ex.Message);
            }
        }

        private void ClearSpawnedWorldEntities()
        {
            try
            {
                Log.Breadcrumb("Clearing all spawned world entities...");
                _runtimeExecutor.ClearAll();
                _engine.Observer.ClearTracking();
                Log.Info("Cleaner", "Successfully cleared all spawned props, NPCs, vehicles, and observer tracking.");
            }
            catch (Exception ex)
            {
                Log.Error("Cleaner", ex, "Exception during ClearSpawnedWorldEntities");
                Notification.Show("~r~[Clear Entities Error]:~w~ " + ex.Message);
            }
        }
    }
}
