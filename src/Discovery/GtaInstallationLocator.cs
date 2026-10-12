using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DynamicWorldEngine.Discovery
{
    /// <summary>
    /// Describes the discovered GTA V installation, its edition, content roots, and provenance.
    /// </summary>
    public class GtaInstallationInfo
    {
        public string RootPath { get; set; }
        public string Version { get; set; }
        public string Edition { get; set; }
        public string DlcPacksPath { get; set; }
        public List<string> ContentRoots { get; set; }
        public bool IsInstalled { get; set; }
        public string DiscoveryMethod { get; set; }
        public DateTime DiscoveryTimestamp { get; set; }

        public GtaInstallationInfo()
        {
            ContentRoots = new List<string>();
            DiscoveryTimestamp = DateTime.UtcNow;
            Version = "Unknown";
            Edition = "Unknown";
        }

        public override string ToString()
        {
            return string.Format("GTA V Installation [{0}]: '{1}' (Version: {2}, Discovered via: {3})",
                Edition, RootPath, Version, DiscoveryMethod);
        }
    }

    /// <summary>
    /// Autonomous GTA V Installation Locator.
    /// Discovers GTA V dynamically across running processes, registry hives, launcher manifests,
    /// environment variables, and filesystem hierarchies without relying on hardcoded machine paths.
    /// </summary>
    public static class GtaInstallationLocator
    {
        private static GtaInstallationInfo _cachedInfo;
        private static readonly object _lock = new object();

        public static GtaInstallationInfo LocateInstallation(string explicitOverridePath = null)
        {
            lock (_lock)
            {
                if (_cachedInfo != null && string.IsNullOrEmpty(explicitOverridePath) && _cachedInfo.IsInstalled)
                {
                    return _cachedInfo;
                }

                var info = new GtaInstallationInfo();

                // 1. Explicit user/configuration override
                if (!string.IsNullOrEmpty(explicitOverridePath) && ValidateGtaRoot(explicitOverridePath))
                {
                    PopulateInstallationInfo(info, explicitOverridePath, "ExplicitUserOverride");
                    _cachedInfo = info;
                    return info;
                }

                // 2. Running GTA V Process Inspection
                try
                {
                    var processes = Process.GetProcessesByName("GTA5");
                    if (processes.Length > 0 && !processes[0].HasExited)
                    {
                        string exePath = processes[0].MainModule.FileName;
                        string dir = Path.GetDirectoryName(exePath);
                        if (ValidateGtaRoot(dir))
                        {
                            PopulateInstallationInfo(info, dir, "RunningGta5Process");
                            _cachedInfo = info;
                            return info;
                        }
                    }
                }
                catch { }

                // 3. Environment Variables
                string[] envVars = new string[] { "GTA5_DIR", "GTAV_DIR", "GTA_INSTALL_DIR", "GTAVEnhanced_DIR" };
                foreach (var ev in envVars)
                {
                    string path = Environment.GetEnvironmentVariable(ev);
                    if (!string.IsNullOrEmpty(path) && ValidateGtaRoot(path))
                    {
                        PopulateInstallationInfo(info, path, "EnvironmentVariable:" + ev);
                        _cachedInfo = info;
                        return info;
                    }
                }

                // 4. Base Directory / Relative Scripts Directory Probing
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string candidateBase = ProbeParentDirectoriesForGta(baseDir);
                if (!string.IsNullOrEmpty(candidateBase) && ValidateGtaRoot(candidateBase))
                {
                    PopulateInstallationInfo(info, candidateBase, "PluginBaseDirectoryHierarchy");
                    _cachedInfo = info;
                    return info;
                }

                // 5. Epic Games Store Manifest Inspection
                string epicInstall = ProbeEpicGamesLauncherManifests();
                if (!string.IsNullOrEmpty(epicInstall) && ValidateGtaRoot(epicInstall))
                {
                    PopulateInstallationInfo(info, epicInstall, "EpicGamesStoreManifest");
                    _cachedInfo = info;
                    return info;
                }

                // 6. Windows Registry Hives (Rockstar / Steam)
                string registryPath = ProbeWindowsRegistry();
                if (!string.IsNullOrEmpty(registryPath) && ValidateGtaRoot(registryPath))
                {
                    PopulateInstallationInfo(info, registryPath, "WindowsRegistry");
                    _cachedInfo = info;
                    return info;
                }

                // 7. Steam Library Folders Probing
                string steamInstall = ProbeSteamLibraryFolders();
                if (!string.IsNullOrEmpty(steamInstall) && ValidateGtaRoot(steamInstall))
                {
                    PopulateInstallationInfo(info, steamInstall, "SteamLibraryManifest");
                    _cachedInfo = info;
                    return info;
                }

                // 8. Standard Installation Locations Fallback Probe
                string[] standardLocations = new string[]
                {
                    @"C:\Program Files\Epic Games\GTAVEnhanced",
                    @"C:\Program Files\Rockstar Games\Grand Theft Auto V",
                    @"C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V",
                    @"D:\SteamLibrary\steamapps\common\Grand Theft Auto V",
                    @"E:\SteamLibrary\steamapps\common\Grand Theft Auto V",
                    @"D:\Games\Grand Theft Auto V",
                    @"E:\Games\Grand Theft Auto V"
                };

                foreach (var loc in standardLocations)
                {
                    if (ValidateGtaRoot(loc))
                    {
                        PopulateInstallationInfo(info, loc, "StandardFilesystemProbe");
                        _cachedInfo = info;
                        return info;
                    }
                }

                // Not found
                info.IsInstalled = false;
                info.DiscoveryMethod = "NotFound";
                return info;
            }
        }

        public static bool ValidateGtaRoot(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return false;

            bool hasExe = File.Exists(Path.Combine(path, "GTA5.exe")) || File.Exists(Path.Combine(path, "PlayGTAV.exe"));
            bool hasUpdate = Directory.Exists(Path.Combine(path, "update"));
            bool hasX64 = Directory.Exists(Path.Combine(path, "x64")) || File.Exists(Path.Combine(path, "x64a.rpf"));

            return (hasExe || hasUpdate) && (hasUpdate || hasX64);
        }

        private static void PopulateInstallationInfo(GtaInstallationInfo info, string rootPath, string method)
        {
            info.RootPath = Path.GetFullPath(rootPath);
            info.IsInstalled = true;
            info.DiscoveryMethod = method;

            // Content Roots
            info.ContentRoots.Clear();
            string updateDir = Path.Combine(info.RootPath, "update");
            if (Directory.Exists(updateDir)) info.ContentRoots.Add(updateDir);

            string dlcPath = Path.Combine(info.RootPath, "update", "x64", "dlcpacks");
            if (Directory.Exists(dlcPath))
            {
                info.DlcPacksPath = dlcPath;
                info.ContentRoots.Add(dlcPath);
            }

            string modsDlcPath = Path.Combine(info.RootPath, "mods", "update", "x64", "dlcpacks");
            if (Directory.Exists(modsDlcPath))
            {
                info.ContentRoots.Add(modsDlcPath);
            }

            // Version Detection from GTA5.exe
            string exePath = Path.Combine(info.RootPath, "GTA5.exe");
            if (File.Exists(exePath))
            {
                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(exePath);
                    info.Version = vi.FileVersion ?? vi.ProductVersion ?? "1.0.0.0";
                }
                catch
                {
                    info.Version = "Available";
                }
            }

            // Edition Detection
            if (File.Exists(Path.Combine(info.RootPath, "PlayGTAV.exe")) && Directory.Exists(Path.Combine(info.RootPath, ".egstore")))
            {
                info.Edition = "Epic Games Enhanced";
            }
            else if (info.RootPath.IndexOf("steamapps", StringComparison.OrdinalIgnoreCase) >= 0 || File.Exists(Path.Combine(info.RootPath, "steam_api64.dll")))
            {
                info.Edition = "Steam Edition";
            }
            else if (Directory.Exists(Path.Combine(info.RootPath, "Rockstar Games")))
            {
                info.Edition = "Rockstar Games Launcher";
            }
            else
            {
                info.Edition = "Retail / Enhanced PC";
            }
        }

        private static string ProbeParentDirectoriesForGta(string startDir)
        {
            try
            {
                string current = startDir;
                for (int i = 0; i < 4; i++)
                {
                    if (string.IsNullOrEmpty(current) || !Directory.Exists(current)) break;

                    if (ValidateGtaRoot(current)) return current;

                    var parent = Directory.GetParent(current);
                    if (parent == null) break;
                    current = parent.FullName;
                }
            }
            catch { }
            return null;
        }

        private static string ProbeEpicGamesLauncherManifests()
        {
            try
            {
                string epicDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
                if (Directory.Exists(epicDataDir))
                {
                    string[] itemFiles = Directory.GetFiles(epicDataDir, "*.item");
                    foreach (var itemFile in itemFiles)
                    {
                        string content = File.ReadAllText(itemFile);
                        if (content.IndexOf("GrandTheftAutoV", StringComparison.OrdinalIgnoreCase) >= 0 || content.IndexOf("GTAV", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Match m = Regex.Match(content, @"""InstallLocation""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                            if (m.Success)
                            {
                                string loc = m.Groups[1].Value.Replace(@"\\", @"\");
                                if (ValidateGtaRoot(loc)) return loc;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string ProbeWindowsRegistry()
        {
            string[] registryKeys = new string[]
            {
                @"SOFTWARE\Rockstar Games\Grand Theft Auto V",
                @"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V",
                @"SOFTWARE\Rockstar Games\GTAV",
                @"SOFTWARE\WOW6432Node\Rockstar Games\GTAV"
            };

            foreach (var key in registryKeys)
            {
                try
                {
                    using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(key))
                    {
                        if (rk != null)
                        {
                            object val = rk.GetValue("InstallFolder") ?? rk.GetValue("Install_Path") ?? rk.GetValue("Path");
                            if (val != null && ValidateGtaRoot(val.ToString()))
                            {
                                return val.ToString();
                            }
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        private static string ProbeSteamLibraryFolders()
        {
            try
            {
                string steamPath = null;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("SteamPath");
                        if (val != null) steamPath = val.ToString().Replace("/", @"\");
                    }
                }

                if (string.IsNullOrEmpty(steamPath))
                {
                    steamPath = @"C:\Program Files (x86)\Steam";
                }

                if (Directory.Exists(steamPath))
                {
                    string defaultGta = Path.Combine(steamPath, "steamapps", "common", "Grand Theft Auto V");
                    if (ValidateGtaRoot(defaultGta)) return defaultGta;

                    string vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdfPath))
                    {
                        string vdfText = File.ReadAllText(vdfPath);
                        MatchCollection matches = Regex.Matches(vdfText, @"""path""\s*""([^""]+)""", RegexOptions.IgnoreCase);
                        foreach (Match m in matches)
                        {
                            string libPath = m.Groups[1].Value.Replace(@"\\", @"\");
                            string gtaLib = Path.Combine(libPath, "steamapps", "common", "Grand Theft Auto V");
                            if (ValidateGtaRoot(gtaLib)) return gtaLib;
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
