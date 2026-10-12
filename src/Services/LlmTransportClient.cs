using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DynamicWorldEngine.Services
{
    /// <summary>
    /// Pure HTTP / REST Transport Client for LLM generation.
    /// Handles API key discovery, round-robin key rotation, model failover ladders,
    /// HTTP serialization, timeouts, and JSON repair integration.
    /// </summary>
    public class LlmTransportClient
    {
        private readonly List<string> _geminiKeys = new List<string>();
        private readonly List<string> _groqKeys = new List<string>();
        private int _currentGeminiKeyIndex = 0;
        private int _currentGroqKeyIndex = 0;
        private string _preferredGeminiModel = "gemini-3.6-flash";

        public int GeminiKeyCount { get { return _geminiKeys.Count; } }
        public int GroqKeyCount { get { return _groqKeys.Count; } }
        public string PreferredGeminiModel
        {
            get { return _preferredGeminiModel; }
            set { _preferredGeminiModel = value; }
        }

        public IReadOnlyList<string> GeminiKeys { get { return _geminiKeys; } }
        public IReadOnlyList<string> GroqKeys { get { return _groqKeys; } }

        public LlmTransportClient(string customIniPath = null)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            LoadApiKeys(customIniPath);
        }

        public void AddGeminiKey(string key)
        {
            if (!string.IsNullOrEmpty(key) && !_geminiKeys.Contains(key))
            {
                if (key.StartsWith("AIzaSy")) _geminiKeys.Insert(0, key);
                else _geminiKeys.Add(key);
            }
        }

        public void AddGroqKey(string key)
        {
            if (!string.IsNullOrEmpty(key) && !_groqKeys.Contains(key))
            {
                _groqKeys.Add(key);
            }
        }

        public void ClearKeys()
        {
            _geminiKeys.Clear();
            _groqKeys.Clear();
            _currentGeminiKeyIndex = 0;
            _currentGroqKeyIndex = 0;
        }

        public void LoadApiKeys(string customFolderPath)
        {
            string scriptsDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string desktopDir = Path.Combine(userProfile, "Desktop");

            List<string> candidateIniPaths = new List<string>
            {
                Path.Combine(scriptsDirectory, "LivingLSAIs.ini"),
                Path.Combine(scriptsDirectory, "2234fb-LivingLSAIs4.5", "LivingLSAIs.ini"),
                !string.IsNullOrEmpty(customFolderPath) ? Path.Combine(customFolderPath, "LivingLSAIs.ini") : "",
                "C:\\Program Files\\Epic Games\\GTAVEnhanced\\scripts\\LivingLSAIs.ini",
                "C:\\Program Files\\Rockstar Games\\Grand Theft Auto V\\scripts\\LivingLSAIs.ini",
                Path.Combine(desktopDir, "mod backup", "scripts", "LivingLSAIs.ini"),
                Path.Combine(desktopDir, "LivingLSAIs.ini")
            };

            foreach (string path in candidateIniPaths)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    try
                    {
                        string text = File.ReadAllText(path);
                        MatchCollection gMatches = Regex.Matches(text, @"GeminiKey[1-9]\s*=\s*([^\r\n;]+)", RegexOptions.IgnoreCase);
                        foreach (Match m in gMatches)
                        {
                            string key = m.Groups[1].Value.Trim();
                            if (key.Length > 20 && !key.StartsWith("YOUR_") && !key.StartsWith("AQ.") && !_geminiKeys.Contains(key))
                            {
                                if (key.StartsWith("AIzaSy")) _geminiKeys.Insert(0, key);
                                else _geminiKeys.Add(key);
                            }
                        }

                        Match groqMatch = Regex.Match(text, @"GroqKey\s*=\s*([^\r\n;]+)", RegexOptions.IgnoreCase);
                        if (groqMatch.Success)
                        {
                            string gKey = groqMatch.Groups[1].Value.Trim();
                            if (gKey.Length > 15 && !gKey.StartsWith("YOUR_") && !_groqKeys.Contains(gKey))
                            {
                                _groqKeys.Add(gKey);
                            }
                        }

                        Match modelMatch = Regex.Match(text, @"GeminiModel\s*=\s*([^\r\n;]+)", RegexOptions.IgnoreCase);
                        if (modelMatch.Success)
                        {
                            string mName = modelMatch.Groups[1].Value.Trim();
                            if (!string.IsNullOrEmpty(mName) && !mName.StartsWith("YOUR_"))
                            {
                                _preferredGeminiModel = mName;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[LlmTransportClient] Warning reading INI: " + ex.Message);
                    }
                }
            }

            // Also check Desktop direct api text files
            string[] directKeyFiles = new string[]
            {
                Path.Combine(desktopDir, "api.txt"),
                Path.Combine(desktopDir, "groq.txt"),
                Path.Combine(desktopDir, "keys.txt")
            };

            foreach (string f in directKeyFiles)
            {
                if (File.Exists(f))
                {
                    try
                    {
                        string content = File.ReadAllText(f).Trim();
                        string[] lines = content.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string rawLine in lines)
                        {
                            string line = rawLine.Trim();
                            if (line.StartsWith("gsk_") && !_groqKeys.Contains(line))
                            {
                                _groqKeys.Insert(0, line);
                            }
                            else if (line.StartsWith("AIzaSy") && !_geminiKeys.Contains(line))
                            {
                                _geminiKeys.Insert(0, line);
                            }
                        }
                    }
                    catch { }
                }
            }

            Console.WriteLine("[LlmTransportClient] Initialized with {0} Gemini keys, {1} Groq keys.", _geminiKeys.Count, _groqKeys.Count);
        }

        /// <summary>
        /// Attempts LLM generation with key rotation and multi-provider failover ladder.
        /// Returns parsed/repaired JSON response string, or null if all providers/keys fail.
        /// </summary>
        public string ExecuteGenerationWithFailover(string systemPrompt, string userPrompt)
        {
            // 1. Try Gemini Provider
            if (_geminiKeys.Count > 0)
            {
                List<string> geminiModels = new List<string>();
                if (!string.IsNullOrEmpty(_preferredGeminiModel))
                {
                    geminiModels.Add(_preferredGeminiModel);
                }
                string[] standardGemini = new string[] {
                    "gemini-3.6-flash",
                    "gemini-flash-latest",
                    "gemini-3.7-flash",
                    "gemini-flash-lite-latest",
                    "gemini-3.5-flash",
                    "gemini-2.5-flash"
                };
                foreach (string m in standardGemini)
                {
                    if (!geminiModels.Contains(m)) geminiModels.Add(m);
                }

                for (int k = 0; k < _geminiKeys.Count; k++)
                {
                    string apiKey = _geminiKeys[(_currentGeminiKeyIndex + k) % _geminiKeys.Count];
                    foreach (string model in geminiModels)
                    {
                        try
                        {
                            string res = CallGeminiApi(apiKey, model, systemPrompt, userPrompt);
                            if (!string.IsNullOrEmpty(res))
                            {
                                _currentGeminiKeyIndex = (_currentGeminiKeyIndex + k) % _geminiKeys.Count;
                                return JsonRepairUtility.RepairAndExtractJson(res);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[LlmTransportClient] Gemini '{0}' failed: {1}", model, ex.Message);
                        }
                    }
                }
            }

            // 2. Try Groq Provider
            if (_groqKeys.Count > 0)
            {
                string[] groqModels = new string[] {
                    "openai/gpt-oss-120b",
                    "openai/gpt-oss-20b",
                    "groq/compound",
                    "qwen/qwen3.8-27b",
                    "llama-3.3-70b-versatile"
                };

                for (int g = 0; g < _groqKeys.Count; g++)
                {
                    string groqKey = _groqKeys[(_currentGroqKeyIndex + g) % _groqKeys.Count];
                    foreach (string model in groqModels)
                    {
                        try
                        {
                            string res = CallGroqApi(groqKey, model, systemPrompt, userPrompt);
                            if (!string.IsNullOrEmpty(res))
                            {
                                _currentGroqKeyIndex = (_currentGroqKeyIndex + g) % _groqKeys.Count;
                                return JsonRepairUtility.RepairAndExtractJson(res);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[LlmTransportClient] Groq '{0}' failed: {1}", model, ex.Message);
                        }
                    }
                }
            }

            return null;
        }

        public string CallGeminiApi(string apiKey, string modelName, string systemInstruction, string userText)
        {
            string url = string.Format("https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}", modelName, Uri.EscapeDataString(apiKey));

            var requestObj = new
            {
                contents = new[] {
                    new { parts = new[] { new { text = userText } } }
                },
                systemInstruction = new {
                    parts = new[] { new { text = systemInstruction } }
                },
                generationConfig = new {
                    temperature = 0.7,
                    maxOutputTokens = 8192,
                    responseMimeType = "application/json"
                }
            };

            string requestJson = JsonConvert.SerializeObject(requestObj);
            byte[] bytes = Encoding.UTF8.GetBytes(requestJson);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = bytes.Length;
            request.Timeout = 15000;
            request.ReadWriteTimeout = 15000;

            using (Stream reqStream = request.GetRequestStream())
            {
                reqStream.Write(bytes, 0, bytes.Length);
            }

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                string rawResponse = reader.ReadToEnd();
                JObject root = JObject.Parse(rawResponse);
                JToken textToken = root.SelectToken("candidates[0].content.parts[0].text");
                return textToken != null ? textToken.ToString() : null;
            }
        }

        public string CallGroqApi(string apiKey, string modelName, string systemInstruction, string userText)
        {
            string url = "https://api.groq.com/openai/v1/chat/completions";

            var requestObj = new
            {
                model = modelName,
                messages = new[] {
                    new { role = "system", content = systemInstruction },
                    new { role = "user", content = userText }
                },
                temperature = 0.7,
                response_format = new { type = "json_object" }
            };

            string requestJson = JsonConvert.SerializeObject(requestObj);
            byte[] bytes = Encoding.UTF8.GetBytes(requestJson);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.Headers.Add("Authorization", "Bearer " + apiKey);
            request.ContentType = "application/json";
            request.ContentLength = bytes.Length;
            request.Timeout = 15000;
            request.ReadWriteTimeout = 15000;

            using (Stream reqStream = request.GetRequestStream())
            {
                reqStream.Write(bytes, 0, bytes.Length);
            }

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                string rawResponse = reader.ReadToEnd();
                JObject root = JObject.Parse(rawResponse);
                JToken textToken = root.SelectToken("choices[0].message.content");
                return textToken != null ? textToken.ToString() : null;
            }
        }
    }
}
