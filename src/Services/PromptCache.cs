using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace DynamicWorldEngine.Services
{
    public class PromptCache
    {
        private readonly string _cacheFilePath;
        private readonly Dictionary<string, string> _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();

        public PromptCache(string dataDir)
        {
            if (string.IsNullOrEmpty(dataDir))
            {
                dataDir = AppDomain.CurrentDomain.BaseDirectory;
            }
            _cacheFilePath = Path.Combine(dataDir, "prompt_cache.json");
            LoadCache();
        }

        private void LoadCache()
        {
            lock (_lock)
            {
                if (File.Exists(_cacheFilePath))
                {
                    try
                    {
                        string json = File.ReadAllText(_cacheFilePath);
                        var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                        if (data != null)
                        {
                            foreach (var kvp in data)
                            {
                                _cache[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        public bool TryGetCachedResponse(string prompt, out string cachedJson)
        {
            lock (_lock)
            {
                string key = NormalizeKey(prompt);
                return _cache.TryGetValue(key, out cachedJson);
            }
        }

        public void StoreResponse(string prompt, string jsonResponse)
        {
            if (string.IsNullOrWhiteSpace(prompt) || string.IsNullOrWhiteSpace(jsonResponse)) return;

            lock (_lock)
            {
                string key = NormalizeKey(prompt);
                _cache[key] = jsonResponse;

                // Persist cache asynchronously or debounced
                try
                {
                    string outJson = JsonConvert.SerializeObject(_cache, Formatting.Indented);
                    File.WriteAllText(_cacheFilePath, outJson);
                }
                catch { }
            }
        }

        private string NormalizeKey(string text)
        {
            return (text ?? "").Trim().ToLowerInvariant();
        }
    }
}
