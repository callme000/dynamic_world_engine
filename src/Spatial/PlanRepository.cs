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
                    string tempPath = targetPath + ".tmp." + Guid.NewGuid().ToString().Substring(0, 8);

                    string json = JsonConvert.SerializeObject(plan, Formatting.Indented);
                    File.WriteAllText(tempPath, json);

                    if (File.Exists(targetPath))
                    {
                        File.Delete(targetPath);
                    }
                    File.Move(tempPath, targetPath);
                    return true;
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
                if (!File.Exists(path)) return false;

                try
                {
                    string json = File.ReadAllText(path);
                    plan = JsonConvert.DeserializeObject<SpatialExecutionPlan>(json);
                    return plan != null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[PlanRepository] Error loading plan for " + environmentId + ": " + ex.Message);
                    return false;
                }
            }
        }

        public bool DeletePlan(string environmentId)
        {
            if (string.IsNullOrEmpty(environmentId)) return false;

            lock (_lock)
            {
                string path = GetPlanFilePath(environmentId);
                if (!File.Exists(path)) return false;

                try
                {
                    File.Delete(path);
                    return true;
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
