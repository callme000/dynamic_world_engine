using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace DynamicWorldEngine.Spatial
{
    /// <summary>
    /// Authoritative physical blueprint repository.
    /// Stores and retrieves sanitized Canonical Plans (SpatialExecutionPlan)
    /// without relying on or mutating living domain state.
    /// </summary>
    public class PlanRepository
    {
        private readonly string _storageDirectory;
        private readonly object _lock = new object();

        public PlanRepository(string storageDirectory = null)
        {
            if (string.IsNullOrEmpty(storageDirectory))
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string candidate = Path.Combine(baseDir, "DynamicWorldEngineData", "plans");
                if (!Directory.Exists(candidate))
                {
                    string parentCandidate = Path.Combine(baseDir, "..", "DynamicWorldEngineData", "plans");
                    if (Directory.Exists(parentCandidate))
                    {
                        candidate = Path.GetFullPath(parentCandidate);
                    }
                }
                _storageDirectory = candidate;
            }
            else
            {
                _storageDirectory = storageDirectory;
            }

            try
            {
                if (!Directory.Exists(_storageDirectory))
                {
                    Directory.CreateDirectory(_storageDirectory);
                }
            }
            catch { }
        }

        public string StorageDirectory { get { return _storageDirectory; } }

        public string GetPlanFilePath(string environmentId)
        {
            if (string.IsNullOrEmpty(environmentId)) return null;
            string safeName = Path.GetFileName(environmentId) + ".json";
            return Path.Combine(_storageDirectory, safeName);
        }

        public bool HasPlan(string environmentId)
        {
            if (string.IsNullOrEmpty(environmentId)) return false;
            lock (_lock)
            {
                string path = GetPlanFilePath(environmentId);
                return File.Exists(path);
            }
        }

        public bool SavePlan(SpatialExecutionPlan plan)
        {
            if (plan == null) return false;
            return SavePlan(plan.EnvironmentId, plan);
        }

        public bool SavePlan(string environmentId, SpatialExecutionPlan plan)
        {
            if (string.IsNullOrEmpty(environmentId) || plan == null) return false;

            lock (_lock)
            {
                try
                {
                    if (!Directory.Exists(_storageDirectory))
                    {
                        Directory.CreateDirectory(_storageDirectory);
                    }

                    string targetPath = GetPlanFilePath(environmentId);
                    string tempPath = targetPath + ".tmp." + Guid.NewGuid().ToString("N");
                    string backupPath = targetPath + ".bak";
                    string json = JsonConvert.SerializeObject(plan, Formatting.Indented);

                    try
                    {
                        using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
                        {
                            writer.Write(json);
                            writer.Flush();
                            stream.Flush(true);
                        }

                        if (File.Exists(targetPath))
                        {
                            // Atomically replace the primary while retaining its last
                            // committed version as the backup. Never delete the target first.
                            File.Replace(tempPath, targetPath, backupPath, true);
                        }
                        else
                        {
                            File.Move(tempPath, targetPath);
                        }

                        return true;
                    }
                    finally
                    {
                        if (File.Exists(tempPath))
                        {
                            try { File.Delete(tempPath); } catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[PlanRepository] Error saving plan for " + environmentId + ": " + ex.Message);
                    return false;
                }
            }
        }

        public SpatialExecutionPlan GetPlan(string environmentId)
        {
            SpatialExecutionPlan plan;
            TryGetPlan(environmentId, out plan);
            return plan;
        }

        public bool TryGetPlan(string environmentId, out SpatialExecutionPlan plan)
        {
            plan = null;
            if (string.IsNullOrEmpty(environmentId)) return false;

            lock (_lock)
            {
                string path = GetPlanFilePath(environmentId);
                string backupPath = path + ".bak";
                if (!File.Exists(path) && !File.Exists(backupPath)) return false;

                Exception primaryError = null;
                if (File.Exists(path))
                {
                    try
                    {
                        string json = File.ReadAllText(path);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            plan = JsonConvert.DeserializeObject<SpatialExecutionPlan>(json);
                            if (plan != null) return true;
                        }
                        primaryError = new InvalidDataException("Primary plan file was empty or deserialized to null.");
                    }
                    catch (Exception ex)
                    {
                        primaryError = ex;
                    }
                }

                if (File.Exists(backupPath))
                {
                    try
                    {
                        string backupJson = File.ReadAllText(backupPath);
                        if (!string.IsNullOrWhiteSpace(backupJson))
                        {
                            plan = JsonConvert.DeserializeObject<SpatialExecutionPlan>(backupJson);
                            if (plan != null)
                            {
                                Console.WriteLine("[PlanRepository] Recovered plan '" + environmentId + "' from backup.");
                                return true;
                            }
                        }
                    }
                    catch (Exception backupError)
                    {
                        Console.WriteLine("[PlanRepository] Backup for '" + environmentId + "' is unreadable: " + backupError.Message);
                    }
                }

                plan = null;
                if (primaryError != null)
                {
                    Console.WriteLine("[PlanRepository] Error loading plan for " + environmentId + ": " + primaryError.Message);
                }
                return false;
            }
        }

        public bool DeletePlan(string environmentId)
        {
            if (string.IsNullOrEmpty(environmentId)) return false;

            lock (_lock)
            {
                string path = GetPlanFilePath(environmentId);
                string backupPath = path + ".bak";
                if (!File.Exists(path) && !File.Exists(backupPath)) return false;

                try
                {
                    // The backup is part of the same logical plan. Leaving it behind
                    // would allow TryGetPlan to resurrect a deleted plan.
                    if (File.Exists(path)) File.Delete(path);
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    return !File.Exists(path) && !File.Exists(backupPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[PlanRepository] Error deleting plan for " + environmentId + ": " + ex.Message);
                    return false;
                }
            }
        }

        public List<string> GetAllPlanIds()
        {
            var result = new List<string>();
            lock (_lock)
            {
                if (!Directory.Exists(_storageDirectory)) return result;

                try
                {
                    string[] files = Directory.GetFiles(_storageDirectory, "*.json");
                    foreach (var f in files)
                    {
                        string id = Path.GetFileNameWithoutExtension(f);
                        if (!string.IsNullOrEmpty(id))
                        {
                            result.Add(id);
                        }
                    }
                }
                catch { }
            }
            return result;
        }
    }
}
