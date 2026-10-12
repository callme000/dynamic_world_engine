using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using GTA.Math;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Discovery
{
    /// <summary>
    /// Autonomous Dynamic Interior Discovery Provider.
    /// Eliminates static interior manifests (e.g. interiors.json) by inspecting the active GTA V
    /// installation directory, installed interior extensions (such as EnableAllinteriors.dll),
    /// and game map data at runtime or startup.
    /// Extracts exact entrance coordinates, interior coordinates, names, and IPL sets dynamically.
    /// </summary>
    public class DynamicInteriorDiscoveryProvider : IAssetDiscoveryProvider
    {
        public string ProviderName { get { return "DynamicInteriorDiscoveryProvider"; } }
        public int Priority { get { return 95; } }

        private readonly List<GtaAssetDefinition> _discoveredInteriors = new List<GtaAssetDefinition>();
        private string _gtaInstallationRoot = "";
        private bool _hasInitialized = false;

        public IReadOnlyList<GtaAssetDefinition> DiscoveredInteriors
        {
            get { return _discoveredInteriors.AsReadOnly(); }
        }

        public void Initialize(SpatialAssetRegistry registry, string gtaRootPath)
        {
            if (_hasInitialized) return;

            _discoveredInteriors.Clear();
            _gtaInstallationRoot = gtaRootPath;

            if (string.IsNullOrEmpty(_gtaInstallationRoot))
            {
                var install = GtaInstallationLocator.LocateInstallation();
                if (install != null && install.IsInstalled)
                {
                    _gtaInstallationRoot = install.RootPath;
                }
            }

            // 1. Discover interiors from installed interior modules (e.g., EnableAllinteriors.dll)
            DiscoverFromInstalledInteriorAssemblies();

            // 2. Discover canonical fallback interiors ensuring essential venue archetypes are registered
            RegisterKnownEngineInteriors();

            _hasInitialized = true;
        }

        public IEnumerable<GtaAssetDefinition> ProvideAssets()
        {
            foreach (var interior in _discoveredInteriors)
            {
                yield return interior;
            }
        }

        #region Assembly Inspection & Extraction

        private void DiscoverFromInstalledInteriorAssemblies()
        {
            List<string> candidatePaths = new List<string>();

            if (!string.IsNullOrEmpty(_gtaInstallationRoot))
            {
                candidatePaths.Add(Path.Combine(_gtaInstallationRoot, "scripts", "EnableAllinteriors.dll"));
                candidatePaths.Add(Path.Combine(_gtaInstallationRoot, "EnableAllinteriors.dll"));
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            candidatePaths.Add(Path.Combine(baseDir, "EnableAllinteriors.dll"));
            candidatePaths.Add(Path.Combine(baseDir, "scripts", "EnableAllinteriors.dll"));
            candidatePaths.Add(Path.Combine(Directory.GetCurrentDirectory(), "EnableAllinteriors.dll"));

            string selectedDll = null;
            foreach (var p in candidatePaths)
            {
                if (File.Exists(p))
                {
                    selectedDll = p;
                    break;
                }
            }

            if (string.IsNullOrEmpty(selectedDll)) return;

            ResolveEventHandler resolveHandler = (s, e) =>
            {
                string shortName = new AssemblyName(e.Name).Name + ".dll";
                string dllDir = Path.GetDirectoryName(selectedDll);
                string[] paths = new string[]
                {
                    !string.IsNullOrEmpty(dllDir) ? Path.Combine(dllDir, shortName) : null,
                    !string.IsNullOrEmpty(_gtaInstallationRoot) ? Path.Combine(_gtaInstallationRoot, "scripts", shortName) : null,
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", shortName),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, shortName)
                };
                foreach (var p in paths)
                {
                    if (!string.IsNullOrEmpty(p) && File.Exists(p))
                    {
                        try { return Assembly.LoadFrom(p); } catch { }
                    }
                }
                return null;
            };

            AppDomain.CurrentDomain.AssemblyResolve += resolveHandler;
            try
            {
                ExtractInteriorsFromDll(selectedDll);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DynamicInteriorDiscoveryProvider] Warning parsing '{0}': {1}", selectedDll, ex.Message);
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= resolveHandler;
            }
        }

        private void ExtractInteriorsFromDll(string dllPath)
        {
            byte[] dllBytes = File.ReadAllBytes(dllPath);
            Assembly asm = Assembly.Load(dllBytes);
            Module mod = asm.ManifestModule;

            Type class1Type = asm.GetType("EnableAllinteriors.Class1");
            if (class1Type == null) return;

            // Extract IPL sets from dedicated methods first
            Dictionary<string, DiscoveredIplBundle> methodIplMap = ExtractIplsFromClassMethods(class1Type, mod);

            // Extract ActiveInterior initializations from .cctor IL via execution-free CIL decoding
            ConstructorInfo cctor = class1Type.TypeInitializer;
            if (cctor == null) return;

            var instructions = CilDecoder.Decode(cctor);
            if (instructions == null || instructions.Count == 0) return;

            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];
                if (inst.OpCode == OpCodes.Newobj)
                {
                    var mb = inst.Operand as MethodBase;
                    if (mb != null && mb.DeclaringType != null && mb.DeclaringType.Name.Contains("ActiveInterior"))
                    {
                        // Locate preceding dup instruction bounding this ActiveInterior instantiation
                        int dupIndex = -1;
                        for (int k = i - 1; k >= Math.Max(0, i - 35); k--)
                        {
                            if (instructions[k].OpCode == OpCodes.Dup)
                            {
                                dupIndex = k;
                                break;
                            }
                        }

                        int startSlice = dupIndex >= 0 ? dupIndex + 1 : Math.Max(0, i - 25);
                        var slice = instructions.GetRange(startSlice, i - startSlice + 1);
                        var extracted = EvaluateActiveInteriorSlice(slice);
                        if (extracted != null)
                        {
                            RegisterExtractedInterior(extracted.Name, extracted.Enter, extracted.Exit, methodIplMap);
                        }
                    }
                }
            }
        }

        private class ExtractedInteriorData
        {
            public string Name;
            public int BlipId;
            public Vector3 MarkerPos;
            public Vector3 Enter;
            public Vector3 Exit;
        }

        private static ExtractedInteriorData EvaluateActiveInteriorSlice(List<CilInstruction> slice)
        {
            Stack<object> stack = new Stack<object>();
            foreach (var inst in slice)
            {
                OpCode op = inst.OpCode;
                if (op == OpCodes.Nop) continue;
                if (op == OpCodes.Ldstr)
                {
                    stack.Push(inst.Operand as string ?? "");
                }
                else if (op == OpCodes.Ldc_R4)
                {
                    stack.Push(inst.Operand != null ? (float)inst.Operand : 0f);
                }
                else if (op == OpCodes.Ldc_I4)
                {
                    stack.Push(inst.Operand != null ? (int)inst.Operand : 0);
                }
                else if (op == OpCodes.Ldc_I4_S)
                {
                    stack.Push(inst.Operand != null ? Convert.ToInt32(inst.Operand) : 0);
                }
                else if (op == OpCodes.Ldc_I4_0) stack.Push(0);
                else if (op == OpCodes.Ldc_I4_1) stack.Push(1);
                else if (op == OpCodes.Ldc_I4_2) stack.Push(2);
                else if (op == OpCodes.Ldc_I4_3) stack.Push(3);
                else if (op == OpCodes.Ldc_I4_4) stack.Push(4);
                else if (op == OpCodes.Ldc_I4_5) stack.Push(5);
                else if (op == OpCodes.Ldc_I4_6) stack.Push(6);
                else if (op == OpCodes.Ldc_I4_7) stack.Push(7);
                else if (op == OpCodes.Ldc_I4_8) stack.Push(8);
                else if (op == OpCodes.Ldc_I4_M1) stack.Push(-1);
                else if (op == OpCodes.Newobj)
                {
                    var mb = inst.Operand as MethodBase;
                    if (mb != null && mb.DeclaringType != null)
                    {
                        string declName = mb.DeclaringType.Name;
                        if (declName == "Vector3" && stack.Count >= 3)
                        {
                            float z = Convert.ToSingle(stack.Pop());
                            float y = Convert.ToSingle(stack.Pop());
                            float x = Convert.ToSingle(stack.Pop());
                            stack.Push(new Vector3(x, y, z));
                        }
                        else if (declName.Contains("ActiveInterior"))
                        {
                            var pars = mb.GetParameters();
                            if (pars.Length == 3 && stack.Count >= 3)
                            {
                                string name = stack.Pop() as string;
                                int blip = Convert.ToInt32(stack.Pop());
                                object posObj = stack.Pop();
                                Vector3 pos = posObj is Vector3 ? (Vector3)posObj : Vector3.Zero;
                                return new ExtractedInteriorData
                                {
                                    Name = name,
                                    BlipId = blip,
                                    MarkerPos = pos,
                                    Enter = pos,
                                    Exit = pos
                                };
                            }
                            else if (pars.Length == 5 && stack.Count >= 5)
                            {
                                object exitObj = stack.Pop();
                                object enterObj = stack.Pop();
                                string name = stack.Pop() as string;
                                int blip = Convert.ToInt32(stack.Pop());
                                object posObj = stack.Pop();

                                Vector3 exit = exitObj is Vector3 ? (Vector3)exitObj : Vector3.Zero;
                                Vector3 enter = enterObj is Vector3 ? (Vector3)enterObj : Vector3.Zero;
                                Vector3 pos = posObj is Vector3 ? (Vector3)posObj : Vector3.Zero;

                                return new ExtractedInteriorData
                                {
                                    Name = name,
                                    BlipId = blip,
                                    MarkerPos = pos,
                                    Enter = enter,
                                    Exit = exit
                                };
                            }
                        }
                    }
                }
            }
            return null;
        }

        private class DiscoveredIplBundle
        {
            public List<string> RequestIpls = new List<string>();
            public List<string> RemoveIpls = new List<string>();
        }

        private void RegisterExtractedInterior(string rawName, Vector3 entrance, Vector3 interior, Dictionary<string, DiscoveredIplBundle> methodIplMap)
        {
            if (string.IsNullOrEmpty(rawName) || rawName.Length < 2) return;

            string cleanName = rawName.Trim();
            string slug = Regex.Replace(cleanName.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
            string interiorId = "int_" + slug;

            string nLower = cleanName.ToLowerInvariant();
            if (nLower.Contains("penthouse") && (nLower.Contains("dc&r") || nLower.Contains("casino") || nLower.Contains("diamond")))
            {
                interiorId = "int_casino_penthouse";
            }
            else if (cleanName.IndexOf("Mansion Art Studio", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                interiorId = "int_mansion_art_studio";
            }
            else if (nLower.Contains("richman") && (nLower.Contains("mansion") || nLower.Contains("villa")))
            {
                interiorId = "int_richman_mansion";
            }
            else if (nLower.Contains("vinewood") && (nLower.Contains("mansion") || nLower.Contains("residence")))
            {
                interiorId = "int_vinewood_mansion";
            }
            else if (nLower.Contains("tongva") && (nLower.Contains("mansion") || nLower.Contains("estate")))
            {
                interiorId = "int_tongva_mansion";
            }
            else if (cleanName.Equals("Bunker", StringComparison.OrdinalIgnoreCase))
            {
                interiorId = "int_subterranean_bunker";
            }
            else if (cleanName.Equals("Facility", StringComparison.OrdinalIgnoreCase))
            {
                interiorId = "int_doomsday_facility";
            }
            else if (nLower.Contains("bahama"))
            {
                interiorId = "int_nightclub_vip";
            }

            // Avoid duplicates
            if (_discoveredInteriors.Exists(d => string.Equals(d.ModelName, interiorId, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var def = new GtaAssetDefinition
            {
                ModelName = interiorId,
                Name = cleanName,
                Type = AssetType.Interior,
                DefaultCoordinates = interior,
                EntranceCoords = entrance,
                Confidence = KnowledgeConfidence.InstallationMetadata,
                SemanticConfidenceScore = 0.95f,
                Capacity = InferDefaultCapacity(cleanName),
                SupportsVehicles = cleanName.IndexOf("garage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   cleanName.IndexOf("hangar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   cleanName.IndexOf("chop shop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   cleanName.IndexOf("salvage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   cleanName.IndexOf("car meet", StringComparison.OrdinalIgnoreCase) >= 0
            };

            // Associate IPLs if discovered for this venue
            if (methodIplMap != null)
            {
                foreach (var kvp in methodIplMap)
                {
                    if (cleanName.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        kvp.Key.IndexOf(cleanName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        interiorId.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        foreach (var ipl in kvp.Value.RequestIpls)
                        {
                            if (!def.IplDependencies.Contains(ipl)) def.IplDependencies.Add(ipl);
                        }
                        foreach (var remIpl in kvp.Value.RemoveIpls)
                        {
                            if (!def.RemoveIplDependencies.Contains(remIpl)) def.RemoveIplDependencies.Add(remIpl);
                        }
                    }
                }
            }

            // Canonical identities and aliases
            def.Identities.Add(cleanName);
            def.Identities.Add(interiorId);

            if (interiorId == "int_mansion_art_studio")
            {
                def.Category = "studio";
                def.Tags.Add("studio");
                def.Tags.Add("art");
                def.Tags.Add("gallery");
                def.Identities.Add("Mansion Art Studio");
                def.Identities.Add("Art Workshop");
                if (!def.IplDependencies.Contains("m26_1_int_placement_interior_int_art_worshop_milo_"))
                {
                    def.IplDependencies.Add("m26_1_int_placement_interior_int_art_worshop_milo_");
                }
            }
            else if (interiorId == "int_richman_mansion")
            {
                def.Category = "mansion";
                def.Tags.AddRange(new[] { "mansion", "party", "luxury", "estate", "residence" });
                def.Identities.Add("Richman Mansion");
                def.Identities.Add("Richman Villa");
                // Relocate scene center from street gate to luxury pool, bar & terrace grounds
                def.DefaultCoordinates = new Vector3(-1644.00f, 476.00f, 128.84f);
            }
            else if (interiorId == "int_vinewood_mansion")
            {
                def.Category = "mansion";
                def.Tags.AddRange(new[] { "mansion", "party", "luxury", "estate", "residence" });
                def.Identities.Add("Vinewood Mansion");
                def.Identities.Add("Vinewood Residence");
                def.Identities.Add("Vinewood Estate");
                def.DefaultCoordinates = new Vector3(545.00f, 728.00f, 201.96f);
            }
            else if (interiorId == "int_tongva_mansion")
            {
                def.Category = "mansion";
                def.Tags.AddRange(new[] { "mansion", "party", "luxury", "estate", "residence" });
                def.Identities.Add("Tongva Hills Mansion");
                def.Identities.Add("Tongva Estate");
                def.DefaultCoordinates = new Vector3(-2593.00f, 1889.00f, 166.99f);
            }
            else if (interiorId == "int_the_kortz_center" || interiorId.Contains("kortz"))
            {
                def.Category = "gallery";
                def.Tags.AddRange(new[] { "kortz", "museum", "gallery", "art", "exhibit", "culture" });
                def.Identities.Add("The Kortz Center");
                def.Identities.Add("Kortz Center");
                def.Identities.Add("Kortz");
                def.Identities.Add("Art Gallery");
                def.Identities.Add("Museum");
                def.Identities.Add("Kortz Art Center");
                def.DefaultCoordinates = new Vector3(-2290.61f, 364.12f, 173.70f);
            }
            else if (interiorId == "int_casino_penthouse")
            {
                def.Identities.Add("int_diamond_penthouse");
                def.Identities.Add("Diamond Casino Penthouse");
                def.Identities.Add("Casino Penthouse");
                def.Identities.Add("DC&R Penthouse");
            }
            else if (interiorId == "int_subterranean_bunker")
            {
                def.Identities.Add("Subterranean Bunker");
            }
            else if (interiorId == "int_doomsday_facility")
            {
                def.Identities.Add("Doomsday Facility");
                def.Identities.Add("Doomsday Command Facility");
            }
            else if (interiorId == "int_nightclub_vip")
            {
                def.Identities.Add("Bahama Mamas");
                def.Identities.Add("Bahama Mamas West");
            }
            else if (interiorId == "int_torture_room" || cleanName.IndexOf("Torture Room", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                def.Category = "interrogation";
                def.Tags.AddRange(new[] { "interrogation", "torture", "safehouse", "basement", "cartel", "questioning", "hostage", "lockup", "underground", "covert" });
                def.Identities.Add("Torture Room");
                def.Identities.Add("Interrogation Room");
                def.Identities.Add("Cartel Safehouse");
                def.Identities.Add("Cartel Interrogation");
                def.Identities.Add("Interrogation Chamber");
                def.DefaultCoordinates = new Vector3(134.40f, -2203.40f, 7.00f);
            }
            else if (interiorId == "int_the_criminal_enterprise_basement" || cleanName.IndexOf("Criminal Enterprise Basement", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                def.Category = "basement";
                def.Tags.AddRange(new[] { "basement", "safehouse", "underground", "cartel", "interrogation", "covert", "warehouse" });
                def.Identities.Add("Criminal Enterprise Basement");
                def.Identities.Add("Underground Safehouse");
                def.Identities.Add("Cartel Hideout");
            }
            else if (interiorId == "int_meat_packing_factory" || cleanName.IndexOf("Meat Packing", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                def.Category = "industrial";
                def.Tags.AddRange(new[] { "slaughterhouse", "meatpacking", "industrial", "cartel", "safehouse", "interrogation", "warehouse" });
                def.Identities.Add("Meat Packing Factory");
                def.Identities.Add("Slaughterhouse");
            }
            else if (interiorId == "int_wrecked_hospital" || cleanName.IndexOf("Hospital", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                def.Category = "hospital";
                def.Tags.AddRange(new[] { "hospital", "medical", "clinic", "emergency", "triage", "paramedic", "doctor", "patient", "surgery" });
                def.Identities.Add("Wrecked Hospital");
                def.Identities.Add("Hospital");
                def.Identities.Add("Emergency Room");
                def.Identities.Add("Triage");
                def.Identities.Add("Hospital Clinic");
                def.Identities.Add("Medical Center");
            }
            else if (interiorId.StartsWith("int_biker_clubhouse") || cleanName.IndexOf("Biker Clubhouse", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                def.Category = "biker_clubhouse";
                def.Tags.AddRange(new[] { "biker", "clubhouse", "motorcycle", "chopper", "lost_mc", "bar", "gang", "biker_gang" });
                def.Identities.Add("Biker Clubhouse");
                def.Identities.Add("Lost MC Clubhouse");
                def.Identities.Add("Biker Bar");
                def.Identities.Add("Motorcycle Club");
                def.Identities.Add("Clubhouse");
            }
            else if (interiorId.StartsWith("int_the_chop_shop") || cleanName.IndexOf("Chop Shop", StringComparison.OrdinalIgnoreCase) >= 0 || cleanName.IndexOf("Salvage Yard", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                def.Category = "chop_shop";
                def.SupportsVehicles = true;
                def.Tags.AddRange(new[] { "chop_shop", "salvage", "mechanic", "autoshop", "workshop", "garage", "stripping", "supercars", "scrapyard" });
                def.Identities.Add("The Chop Shop");
                def.Identities.Add("Chop Shop");
                def.Identities.Add("Salvage Yard");
                def.Identities.Add("Mechanic Workshop");
                def.Identities.Add("Auto Salvage");
            }
            else if (interiorId == "int_meth_lab" || cleanName.IndexOf("Meth Lab", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                def.Category = "drug_lab";
                def.Tags.AddRange(new[] { "meth", "drug_lab", "chemical", "manufacturing", "cooks", "hazmat", "clandestine", "lab" });
                def.Identities.Add("Meth Lab");
                def.Identities.Add("Methamphetamine Lab");
                def.Identities.Add("Clandestine Meth Lab");
                def.Identities.Add("Chemical Lab");
                def.Identities.Add("Illicit Lab");
                if (!def.EntranceCoords.HasValue || def.EntranceCoords.Value.Z < 0f)
                {
                    def.EntranceCoords = new Vector3(1443.0f, -1484.0f, 66.5f);
                }
            }
            else if (interiorId == "int_cocaine_lockup" || cleanName.IndexOf("Cocaine Lockup", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                def.Category = "drug_lab";
                def.Tags.AddRange(new[] { "cocaine", "coke", "drug_lab", "chemical", "manufacturing", "packaging", "clandestine", "lab" });
                def.Identities.Add("Cocaine Lockup");
                def.Identities.Add("Cocaine Processing Lockup");
                def.Identities.Add("Cocaine Lab");
                def.Identities.Add("Illicit Lab");
                if (!def.EntranceCoords.HasValue || def.EntranceCoords.Value.Z < 0f)
                {
                    def.EntranceCoords = new Vector3(-1462.01f, -382.29f, 37.5f);
                }
            }
            else if (interiorId == "int_weed_farm" || cleanName.IndexOf("Weed Farm", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                def.Category = "drug_lab";
                def.Tags.AddRange(new[] { "weed", "cannabis", "farm", "growhouse", "drug_lab", "hydroponics", "clandestine" });
                def.Identities.Add("Weed Farm");
                def.Identities.Add("Cannabis Growhouse");
                def.Identities.Add("Hydroponic Farm");
                def.Identities.Add("Illicit Lab");
            }
            else if (interiorId.StartsWith("int_police_") || cleanName.IndexOf("Police", StringComparison.OrdinalIgnoreCase) >= 0 || cleanName.IndexOf("Precinct", StringComparison.OrdinalIgnoreCase) >= 0 || cleanName.IndexOf("Jail", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                def.Category = "police_station";
                def.Tags.AddRange(new[] { "police", "police_station", "precinct", "cop", "cops", "booking_desk", "jail", "lockup", "law_enforcement", "holding_cell" });
                def.Identities.Add("Police Station");
                def.Identities.Add("Police Precinct");
                def.Identities.Add("Sheriff Station");
                def.Identities.Add("Booking Desk");
                def.Identities.Add("Precinct Lockup");
            }
            else if (interiorId == "int_cargo_ship" || cleanName.IndexOf("Cargo Ship", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                def.Category = "cargo_ship";
                def.Tags.AddRange(new[] { "cargo_ship", "dockyard", "docks", "shipping", "container", "smuggling", "harbor", "port", "freighter" });
                def.Identities.Add("Cargo Ship");
                def.Identities.Add("Freighter");
                def.Identities.Add("Dockyard Cargo");
                def.Identities.Add("Shipping Terminal");
            }
            else if (interiorId == "int_submarine" || cleanName.IndexOf("Submarine", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                def.Category = "submersible";
                def.Tags.Clear();
                def.Tags.AddRange(new[] { "submarine", "submersible", "naval", "underwater", "maritime", "military" });
                def.Identities.Add("Submarine");
                def.Identities.Add("Naval Submarine");
                def.Identities.Add("Submersible Vessel");
            }
            else if (interiorId.Contains("yacht") || cleanName.IndexOf("Yacht", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                def.Category = "yacht";
                def.Tags.Clear();
                def.Tags.AddRange(new[] { "yacht", "luxury", "maritime", "vip", "ocean", "party" });
                def.Identities.Add("Galaxy Super Yacht");
                def.Identities.Add("Luxury Yacht");
                def.Identities.Add("Super Yacht");
                def.Identities.Add("Gunrunning Yacht");
            }
            else if (interiorId.Contains("hangar") || cleanName.IndexOf("Hangar", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                def.Category = "hangar";
                def.SupportsVehicles = true;
                def.Tags.AddRange(new[] { "hangar", "airstrip", "airport", "airfield", "smuggler", "cargo", "plane", "aircraft" });
                def.Identities.Add("Smugglers Run Hangar");
                def.Identities.Add("Smuggler Hangar");
                def.Identities.Add("Airstrip Hangar");
                def.Identities.Add("Airfield Hangar");
            }

            // Identities are strictly canonical venue titles or explicit model aliases,
            // never arbitrary split single tokens which cause false positive identity matches.
            _discoveredInteriors.Add(def);
        }

        private Dictionary<string, DiscoveredIplBundle> ExtractIplsFromClassMethods(Type class1Type, Module mod)
        {
            var map = new Dictionary<string, DiscoveredIplBundle>(StringComparer.OrdinalIgnoreCase);

            try
            {
                MethodInfo[] methods = null;
                try
                {
                    methods = class1Type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                }
                catch (ReflectionTypeLoadException rtle)
                {
                    var validMethods = new List<MethodInfo>();
                    if (rtle.Types != null)
                    {
                        foreach (var t in rtle.Types)
                        {
                            if (t != null)
                            {
                                try { validMethods.AddRange(t.GetMethods()); } catch { }
                            }
                        }
                    }
                    methods = validMethods.ToArray();
                }
                catch { }

                if (methods != null)
                {
                    foreach (MethodInfo m in methods)
                    {
                        if (m == null) continue;
                        string mName = m.Name;
                        if (mName.IndexOf("Mansion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mName.IndexOf("Penthouse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mName.IndexOf("Nightclub", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mName.IndexOf("Kortz", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mName.IndexOf("Hangar", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            MethodBody body = null;
                            try { body = m.GetMethodBody(); } catch { }
                            var instructions = CilDecoder.Decode(m);
                            if (instructions == null || instructions.Count == 0) continue;

                            var bundle = new DiscoveredIplBundle();

                            foreach (var inst in instructions)
                            {
                                if (inst.OpCode == OpCodes.Ldstr)
                                {
                                    string s = inst.Operand as string;
                                    if (!string.IsNullOrEmpty(s) && (s.Contains("_") || s.StartsWith("hei_") || s.StartsWith("apa_") || s.StartsWith("m25_") || s.StartsWith("m26_") || s.StartsWith("ba_")))
                                    {
                                        if (s.EndsWith("_original") || s.Contains("original") || s.Contains("terrain"))
                                        {
                                            if (!bundle.RemoveIpls.Contains(s)) bundle.RemoveIpls.Add(s);
                                        }
                                        else
                                        {
                                            if (!bundle.RequestIpls.Contains(s)) bundle.RequestIpls.Add(s);
                                        }
                                    }
                                }
                            }

                            if (bundle.RequestIpls.Count > 0 || bundle.RemoveIpls.Count > 0)
                            {
                                string key = mName.Replace("RequestMansion_", "").Replace("RefreshInterior", "").Replace("Refresh", "").Replace("Setup_", "").Replace("Load", "").Replace("TheKortzCenterHeist_", "");
                                map[key] = bundle;

                                // Add friendly aliases so name matches work dynamically
                                if (mName.IndexOf("Richman", StringComparison.OrdinalIgnoreCase) >= 0) { map["Richman"] = bundle; map["richman_mansion"] = bundle; }
                                if (mName.IndexOf("Vinewood", StringComparison.OrdinalIgnoreCase) >= 0) { map["Vinewood"] = bundle; map["vinewood_mansion"] = bundle; }
                                if (mName.IndexOf("Tongva", StringComparison.OrdinalIgnoreCase) >= 0) { map["Tongva"] = bundle; map["tongva_mansion"] = bundle; }
                                if (mName.IndexOf("ArtWorkshop", StringComparison.OrdinalIgnoreCase) >= 0) { map["Art Studio"] = bundle; map["ArtWorkshop"] = bundle; map["mansion_art_studio"] = bundle; }
                                if (mName.IndexOf("Kortz", StringComparison.OrdinalIgnoreCase) >= 0) { map["Kortz"] = bundle; map["The Kortz Center"] = bundle; map["int_the_kortz_center"] = bundle; }
                            }
                        }
                    }
                }
            }
            catch { }

            return map;
        }

        private static int InferDefaultCapacity(string name)
        {
            string nLower = name.ToLowerInvariant();
            if (nLower.Contains("nightclub") || nLower.Contains("arena") || nLower.Contains("casino") || nLower.Contains("hangar")) return 35;
            if (nLower.Contains("mansion") || nLower.Contains("villa") || nLower.Contains("estate") || nLower.Contains("penthouse")) return 20;
            if (nLower.Contains("bunker") || nLower.Contains("facility") || nLower.Contains("vault") || nLower.Contains("bank")) return 20;
            if (nLower.Contains("clubhouse") || nLower.Contains("bar") || nLower.Contains("lounge")) return 20;
            if (nLower.Contains("studio") || nLower.Contains("arcade") || nLower.Contains("agency")) return 15;
            return 10;
        }

        #endregion

        #region Canonical Engine Fallback

        private void RegisterKnownEngineInteriors()
        {
            // Core canonical definitions ensuring unhandled DLC gaps have full coverage
            AddFallback("int_biker_clubhouse_a", "Lost MC Biker Clubhouse", new Vector3(982.0f, -102.0f, 74.85f), new Vector3(982.0f, -102.0f, 74.85f), 25, false,
                "biker_clubhouse", new[] { "biker", "clubhouse", "motorcycle", "chopper", "lost_mc", "bar", "gang", "biker_gang" },
                new[] { "Lost MC Clubhouse", "Biker Clubhouse", "Biker Bar", "Motorcycle Club", "Clubhouse" });

            AddFallback("int_biker_clubhouse_b", "Biker Clubhouse 2-Story", new Vector3(1107.0f, -3157.0f, -37.5f), new Vector3(1107.0f, -3157.0f, -37.5f), 30, true,
                "biker_clubhouse", new[] { "biker", "clubhouse", "motorcycle", "chopper", "lost_mc", "bar", "gang" },
                new[] { "Biker Clubhouse", "Motorcycle Clubhouse" },
                new List<string> { "bkr_biker_interior_placement_interior_0_biker_dlc_int_01_milo_" });

            AddFallback("int_police_station", "Mission Row Police Station", new Vector3(432.0f, -981.5f, 30.7f), new Vector3(443.0f, -981.0f, 30.7f), 25, false,
                "police_station", new[] { "police", "police_station", "precinct", "cop", "cops", "booking_desk", "jail", "lockup", "law_enforcement" },
                new[] { "Mission Row Police Station", "Police Station", "Police Precinct", "LSPD Precinct", "Booking Desk" },
                new List<string> { "rc12b_fixed" });

            AddFallback("int_police_jail", "Mission Row Holding Cells", new Vector3(432.0f, -981.5f, 30.7f), new Vector3(459.0f, -993.5f, 24.9f), 15, false,
                "police_station", new[] { "police", "jail", "cells", "holding_cell", "lockup", "precinct" },
                new[] { "Police Lockup", "Holding Cells", "Jail Cells" });

            AddFallback("int_wrecked_hospital", "Wrecked Hospital Trauma Center", new Vector3(356.5f, -590.0f, 43.3f), new Vector3(356.5f, -590.0f, 43.3f), 25, false,
                "hospital", new[] { "hospital", "medical", "clinic", "emergency", "triage", "paramedic", "doctor", "patient", "surgery" },
                new[] { "Wrecked Hospital", "Hospital", "Emergency Room", "Triage", "Trauma Center" });

            AddFallback("int_the_chop_shop_salvage_yard", "The Chop Shop", new Vector3(477.0f, -1310.0f, 29.2f), new Vector3(1088.0f, -2275.0f, -50.0f), 20, true,
                "chop_shop", new[] { "chop_shop", "salvage", "mechanic", "autoshop", "workshop", "garage", "stripping", "supercars", "scrapyard" },
                new[] { "The Chop Shop", "Chop Shop", "Salvage Yard", "Auto Salvage" });

            AddFallback("int_meth_lab", "Clandestine Meth Lab", new Vector3(1443.0f, -1484.0f, 66.5f), new Vector3(1005.7f, -3200.4f, -38.5f), 20, false,
                "drug_lab", new[] { "meth", "drug_lab", "chemical", "manufacturing", "cooks", "hazmat", "clandestine", "lab" },
                new[] { "Meth Lab", "Drug Lab", "Chemical Lab", "Clandestine Meth Lab" });

            AddFallback("int_cocaine_lockup", "Cocaine Processing Lockup", new Vector3(-1462.01f, -382.29f, 37.5f), new Vector3(1088.69f, -3188.02f, -39.8f), 20, false,
                "drug_lab", new[] { "cocaine", "coke", "drug_lab", "chemical", "manufacturing", "packaging", "clandestine", "lab" },
                new[] { "Cocaine Lockup", "Cocaine Processing Lockup", "Cocaine Lab" });

            AddFallback("int_cargo_ship", "Port Cargo Freighter", new Vector3(-166.5f, -2369.0f, 20.0f), new Vector3(-166.5f, -2369.0f, 20.0f), 25, false,
                "cargo_ship", new[] { "cargo_ship", "dockyard", "docks", "shipping", "container", "smuggling", "harbor", "port", "freighter" },
                new[] { "Cargo Ship", "Freighter", "Dockyard Cargo", "Shipping Terminal" });

            AddFallback("int_richman_mansion", "Richman Mansion", new Vector3(-1691.94f, 490.0f, 128.36f), new Vector3(-1644.00f, 476.00f, 128.84f), 20, false,
                "mansion", new[] { "mansion", "party", "luxury", "estate", "residence" }, new[] { "Richman Mansion", "Richman Villa", "Richman Estate" });
            AddFallback("int_vinewood_mansion", "Vinewood Mansion", new Vector3(541.14f, 776.74f, 201.57f), new Vector3(545.00f, 728.00f, 201.96f), 20, false,
                "mansion", new[] { "mansion", "party", "luxury", "estate", "residence" }, new[] { "Vinewood Mansion", "Vinewood Residence", "Vinewood Estate" });
            AddFallback("int_tongva_mansion", "Tongva Hills Mansion", new Vector3(-2555.21f, 1912.11f, 168.26f), new Vector3(-2593.00f, 1889.00f, 166.99f), 20, false,
                "mansion", new[] { "mansion", "party", "luxury", "estate", "residence" }, new[] { "Tongva Hills Mansion", "Tongva Estate", "Tongva Hills" });
            AddFallback("int_diamond_penthouse", "Diamond Casino Penthouse", new Vector3(924.6f, 45.4f, 80.0f), new Vector3(968.6f, 63.3f, 112.5f), 25, false,
                "penthouse", new[] { "penthouse", "casino", "diamond_casino", "luxury", "vip" }, new[] { "Diamond Casino Penthouse", "Casino Penthouse", "Diamond Penthouse" });
            AddFallback("int_doomsday_facility", "Doomsday Command Facility", new Vector3(483.8f, 4809.7f, 30.0f), new Vector3(483.8f, 4809.7f, -59.5f), 25, true,
                "bunker", new[] { "facility", "doomsday", "command", "military", "high_tech", "subterranean", "war_room", "biohazard", "research" }, new[] { "Doomsday Facility", "Doomsday Command Facility", "Mount Chiliad Facility" });
            AddFallback("int_subterranean_bunker", "Subterranean Military Bunker", new Vector3(891.3f, -3245.7f, 20.0f), new Vector3(891.3f, -3245.7f, -99.5f), 25, true,
                "bunker", new[] { "bunker", "military", "command", "tactical", "subterranean", "defense", "war_room", "ordnance" }, new[] { "Subterranean Military Bunker", "Military Bunker", "Subterranean Bunker", "Command Bunker" });
            AddFallback("int_nightclub_vip", "Bahama Mamas West", new Vector3(-1388.9f, -586.11f, 29.5f), new Vector3(-1387.5f, -588.2f, 29.5f), 30, false,
                "nightclub", new[] { "nightclub", "party", "nightlife", "music", "social", "dance", "bahama_mamas", "bar" }, new[] { "Bahama Mamas", "Bahama Mamas West" });
            AddFallback("int_bank_vault", "Pacific Standard Bank Vault", new Vector3(235.4f, 216.8f, 106.3f), new Vector3(255.85f, 217.6f, 101.68f), 20, false,
                "vault", new[] { "bank", "vault", "bank_vault", "pacific_standard", "heist", "deposit", "gold", "cash", "secure" }, new[] { "Pacific Standard Bank Vault", "Pacific Standard Bank", "Bank Vault", "Pacific Standard" });
            AddFallback("int_fib_server_vault", "FIB Server Farm Vault", new Vector3(105.47f, -745.15f, 45.75f), new Vector3(136.1f, -749.16f, 262.85f), 15, false,
                "vault", new[] { "server", "vault", "fib", "mainframe", "data_center", "laboratory", "research", "classified", "biohazard", "science" }, new[] { "FIB Server Farm Vault", "FIB Server Farm", "Server Farm", "FIB Laboratory" });
        }

        private void AddFallback(string id, string name, Vector3 entrance, Vector3 interior, int capacity, bool supportsVehicles, string category = null, string[] tags = null, string[] identities = null, List<string> ipls = null)
        {
            var existing = _discoveredInteriors.Find(d => string.Equals(d.ModelName, id, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                if (!string.IsNullOrEmpty(category)) existing.Category = category;
                if (tags != null)
                {
                    foreach (var t in tags) if (!existing.Tags.Contains(t)) existing.Tags.Add(t);
                }
                if (identities != null)
                {
                    foreach (var idName in identities) if (!existing.Identities.Contains(idName)) existing.Identities.Add(idName);
                }
                if (ipls != null)
                {
                    foreach (var ipl in ipls) if (!existing.IplDependencies.Contains(ipl)) existing.IplDependencies.Add(ipl);
                }
                if (existing.EntranceCoords.HasValue && existing.EntranceCoords.Value.Z < 0f && entrance.Z >= 0f)
                {
                    existing.EntranceCoords = entrance;
                }
                return;
            }

            var def = new GtaAssetDefinition
            {
                ModelName = id,
                Name = name,
                Category = category,
                Type = AssetType.Interior,
                EntranceCoords = entrance,
                DefaultCoordinates = interior,
                Capacity = capacity,
                SupportsVehicles = supportsVehicles,
                Confidence = KnowledgeConfidence.CuratedCanonical,
                SemanticConfidenceScore = 0.95f
            };
            def.Identities.Add(name);
            def.Identities.Add(id);
            if (identities != null)
            {
                foreach (var idName in identities) if (!def.Identities.Contains(idName)) def.Identities.Add(idName);
            }
            if (tags != null)
            {
                foreach (var t in tags) if (!def.Tags.Contains(t)) def.Tags.Add(t);
            }
            if (ipls != null)
            {
                def.IplDependencies.AddRange(ipls);
            }
            _discoveredInteriors.Add(def);
        }

        #endregion
    }

    #region Execution-Free Managed CIL Metadata Reader

    /// <summary>
    /// Decoded Common Intermediate Language (CIL) instruction adhering to ECMA-335 specification.
    /// Provides typed opcode, operand, size, and metadata references without executing target assembly code.
    /// </summary>
    internal class CilInstruction
    {
        public int Offset { get; set; }
        public OpCode OpCode { get; set; }
        public object Operand { get; set; }
        public int Size { get; set; }

        public override string ToString()
        {
            return string.Format("IL_{0:X4}: {1} {2}", Offset, OpCode.Name, Operand != null ? Operand.ToString() : "");
        }
    }

    /// <summary>
    /// Execution-free managed instruction reader according to REA standards.
    /// Decodes both 1-byte and 2-byte opcodes and parses typed operands without invoking CLR constructors or methods.
    /// </summary>
    internal static class CilDecoder
    {
        private static readonly Dictionary<short, OpCode> OneByteOpCodes = new Dictionary<short, OpCode>();
        private static readonly Dictionary<short, OpCode> TwoByteOpCodes = new Dictionary<short, OpCode>();

        static CilDecoder()
        {
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                OpCode opcode = (OpCode)field.GetValue(null);
                if (opcode.OpCodeType != OpCodeType.Nternal)
                {
                    if (opcode.Size == 1)
                        OneByteOpCodes[opcode.Value] = opcode;
                    else
                        TwoByteOpCodes[opcode.Value] = opcode;
                }
            }
        }

        public static List<CilInstruction> Decode(MethodBase method)
        {
            var instructions = new List<CilInstruction>();
            if (method == null) return instructions;

            MethodBody body = null;
            try { body = method.GetMethodBody(); } catch { }
            if (body == null) return instructions;

            byte[] il = body.GetILAsByteArray();
            if (il == null || il.Length == 0) return instructions;

            Module module = method.Module;
            int offset = 0;

            while (offset < il.Length)
            {
                int startOffset = offset;
                byte b = il[offset++];
                OpCode opcode;

                if (b == 0xFE)
                {
                    if (offset >= il.Length) break;
                    byte b2 = il[offset++];
                    short val = unchecked((short)((0xFE << 8) | b2));
                    if (!TwoByteOpCodes.TryGetValue(val, out opcode))
                        continue;
                }
                else
                {
                    if (!OneByteOpCodes.TryGetValue(b, out opcode))
                        continue;
                }

                object operand = null;
                switch (opcode.OperandType)
                {
                    case OperandType.InlineNone:
                        break;
                    case OperandType.ShortInlineI:
                        operand = unchecked((sbyte)il[offset++]);
                        break;
                    case OperandType.ShortInlineVar:
                        operand = il[offset++];
                        break;
                    case OperandType.ShortInlineR:
                        operand = BitConverter.ToSingle(il, offset);
                        offset += 4;
                        break;
                    case OperandType.InlineI:
                        operand = BitConverter.ToInt32(il, offset);
                        offset += 4;
                        break;
                    case OperandType.InlineI8:
                        operand = BitConverter.ToInt64(il, offset);
                        offset += 8;
                        break;
                    case OperandType.InlineR:
                        operand = BitConverter.ToDouble(il, offset);
                        offset += 8;
                        break;
                    case OperandType.InlineString:
                        int strToken = BitConverter.ToInt32(il, offset);
                        offset += 4;
                        try { operand = module.ResolveString(strToken); } catch { operand = null; }
                        break;
                    case OperandType.InlineMethod:
                        int methodToken = BitConverter.ToInt32(il, offset);
                        offset += 4;
                        try { operand = module.ResolveMethod(methodToken); } catch { operand = null; }
                        break;
                    case OperandType.InlineField:
                        int fieldToken = BitConverter.ToInt32(il, offset);
                        offset += 4;
                        try { operand = module.ResolveField(fieldToken); } catch { operand = null; }
                        break;
                    case OperandType.InlineType:
                        int typeToken = BitConverter.ToInt32(il, offset);
                        offset += 4;
                        try { operand = module.ResolveType(typeToken); } catch { operand = null; }
                        break;
                    case OperandType.InlineTok:
                        int memberToken = BitConverter.ToInt32(il, offset);
                        offset += 4;
                        try { operand = module.ResolveMember(memberToken); } catch { operand = null; }
                        break;
                    case OperandType.InlineBrTarget:
                        operand = BitConverter.ToInt32(il, offset);
                        offset += 4;
                        break;
                    case OperandType.ShortInlineBrTarget:
                        operand = unchecked((sbyte)il[offset++]);
                        break;
                    case OperandType.InlineSwitch:
                        int count = BitConverter.ToInt32(il, offset);
                        offset += 4;
                        int[] targets = new int[count];
                        for (int t = 0; t < count; t++)
                        {
                            targets[t] = BitConverter.ToInt32(il, offset);
                            offset += 4;
                        }
                        operand = targets;
                        break;
                    case OperandType.InlineVar:
                        operand = BitConverter.ToUInt16(il, offset);
                        offset += 2;
                        break;
                    case OperandType.InlineSig:
                        operand = BitConverter.ToInt32(il, offset);
                        offset += 4;
                        break;
                    default:
                        break;
                }

                instructions.Add(new CilInstruction
                {
                    Offset = startOffset,
                    OpCode = opcode,
                    Operand = operand,
                    Size = offset - startOffset
                });
            }

            return instructions;
        }
    }

    #endregion
}
