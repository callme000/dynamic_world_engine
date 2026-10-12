using System;
using System.Collections.Generic;
using System.IO;

namespace DynamicWorldEngine.Discovery
{
    /// <summary>
    /// Metadata record for a single discovered DLC pack within the user's GTA V installation.
    /// </summary>
    public class DiscoveredDlcPack
    {
        public string PackName { get; set; }
        public string FileSystemPath { get; set; }
        public string RpfPath { get; set; }
        public bool Exists { get; set; }
        public long RpfSizeBytes { get; set; }
        public DateTime LastModifiedUtc { get; set; }
        public int ChronologicalOrderIndex { get; set; }
        public bool IsCustomOrMod { get; set; }
        public DateTime DiscoveryTimestamp { get; set; }

        public DiscoveredDlcPack()
        {
            DiscoveryTimestamp = DateTime.UtcNow;
            Exists = true;
        }

        public override string ToString()
        {
            return string.Format("DLC [{0}] (Order #{1}, Size: {2:N0} MB, Modified: {3:yyyy-MM-dd})",
                PackName, ChronologicalOrderIndex, RpfSizeBytes / (1024 * 1024), LastModifiedUtc);
        }
    }

    /// <summary>
    /// Autonomous DLC Discovery Engine.
    /// Enumerate DLC pack directories dynamically directly from the filesystem without hardcoded DLC name arrays.
    /// Supports hypothetical future DLCs (e.g. mpfuture2027) with zero source-code changes.
    /// </summary>
    public class DlcPackDiscoveryEngine
    {
        private readonly List<DiscoveredDlcPack> _discoveredPacks = new List<DiscoveredDlcPack>();
        private readonly object _lock = new object();
        private DateTime _lastScanTime = DateTime.MinValue;

        public IReadOnlyList<DiscoveredDlcPack> DiscoveredPacks
        {
            get
            {
                lock (_lock) { return _discoveredPacks.AsReadOnly(); }
            }
        }

        public DateTime LastScanTime { get { return _lastScanTime; } }

        public int TotalPacksCount
        {
            get
            {
                lock (_lock) { return _discoveredPacks.Count; }
            }
        }

        public void ScanDlcPacks(string gtaRootPath)
        {
            lock (_lock)
            {
                _discoveredPacks.Clear();

                if (string.IsNullOrEmpty(gtaRootPath) || !Directory.Exists(gtaRootPath))
                {
                    return;
                }

                // 1. Scan standard update/x64/dlcpacks
                string standardDlcDir = Path.Combine(gtaRootPath, "update", "x64", "dlcpacks");
                ScanDirectoryForDlcPacks(standardDlcDir, false);

                // 2. Scan mods/update/x64/dlcpacks if present
                string modsDlcDir = Path.Combine(gtaRootPath, "mods", "update", "x64", "dlcpacks");
                ScanDirectoryForDlcPacks(modsDlcDir, true);

                // 3. Sort chronologically by file write time to establish newest -> oldest order
                _discoveredPacks.Sort((a, b) => a.LastModifiedUtc.CompareTo(b.LastModifiedUtc));

                for (int i = 0; i < _discoveredPacks.Count; i++)
                {
                    _discoveredPacks[i].ChronologicalOrderIndex = i + 1;
                }

                _lastScanTime = DateTime.UtcNow;
            }
        }

        private void ScanDirectoryForDlcPacks(string directoryPath, bool isModDirectory)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath)) return;

            try
            {
                string[] subDirs = Directory.GetDirectories(directoryPath);
                foreach (var dir in subDirs)
                {
                    string packName = Path.GetFileName(dir);
                    string rpfPath = Path.Combine(dir, "dlc.rpf");

                    var pack = new DiscoveredDlcPack
                    {
                        PackName = packName,
                        FileSystemPath = dir,
                        RpfPath = rpfPath,
                        Exists = true,
                        IsCustomOrMod = isModDirectory
                    };

                    if (File.Exists(rpfPath))
                    {
                        var fi = new FileInfo(rpfPath);
                        pack.RpfSizeBytes = fi.Length;
                        pack.LastModifiedUtc = fi.LastWriteTimeUtc;
                    }
                    else
                    {
                        pack.LastModifiedUtc = Directory.GetLastWriteTimeUtc(dir);
                    }

                    // Avoid duplicate if pack already discovered from standard dir
                    if (!_discoveredPacks.Exists(p => string.Equals(p.PackName, packName, StringComparison.OrdinalIgnoreCase)))
                    {
                        _discoveredPacks.Add(pack);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DlcPackDiscoveryEngine] Warning scanning '{0}': {1}", directoryPath, ex.Message);
            }
        }

        /// <summary>
        /// Retrieves the latest / newest DLC pack installed in the user's GTA V installation.
        /// Useful for prompts requesting "the newest luxury interior / newest vehicles".
        /// </summary>
        public DiscoveredDlcPack GetNewestDlcPack()
        {
            lock (_lock)
            {
                if (_discoveredPacks.Count == 0) return null;
                return _discoveredPacks[_discoveredPacks.Count - 1];
            }
        }

        public bool HasDlc(string packName)
        {
            if (string.IsNullOrEmpty(packName)) return false;
            lock (_lock)
            {
                return _discoveredPacks.Exists(p => string.Equals(p.PackName, packName, StringComparison.OrdinalIgnoreCase) || p.PackName.IndexOf(packName, StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }
    }
}
