using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace DynamicWorldEngine.Database
{
    public class CanonicalCharacterDefinition
    {
        public string CharacterId { get; set; }
        public string FullName { get; set; }
        public string PedModel { get; set; }
        public string DefaultOccupation { get; set; }
        public string PublicFaction { get; set; }
        public string CanonDescription { get; set; }
        public string DlcOrigin { get; set; }
        public List<string> Roles { get; set; }
        public List<string> AssociatedInteriors { get; set; }
        public List<string> Keywords { get; set; }
        public string DefaultScenario { get; set; }
        public string DefaultInitialTask { get; set; }

        public CanonicalCharacterDefinition()
        {
            Roles = new List<string>();
            AssociatedInteriors = new List<string>();
            Keywords = new List<string>();
            DefaultScenario = "WORLD_HUMAN_STAND";
            DefaultInitialTask = "";
        }

        public CanonicalCharacterDefinition(
            string id, string fullName, string pedModel, string defaultOccupation,
            string publicFaction, string canonDescription, string dlcOrigin,
            string[] roles, string[] associatedInteriors, string[] keywords,
            string defaultScenario = "WORLD_HUMAN_STAND", string defaultInitialTask = "")
        {
            CharacterId = id;
            FullName = fullName;
            PedModel = pedModel;
            DefaultOccupation = defaultOccupation;
            PublicFaction = publicFaction;
            CanonDescription = canonDescription;
            DlcOrigin = dlcOrigin;
            Roles = new List<string>(roles ?? new string[0]);
            AssociatedInteriors = new List<string>(associatedInteriors ?? new string[0]);
            Keywords = new List<string>(keywords ?? new string[0]);
            DefaultScenario = defaultScenario;
            DefaultInitialTask = defaultInitialTask;
        }
    }

    /// <summary>
    /// Data-driven Character Provider. Loads lore and celebrity characters from external JSON
    /// manifests (DynamicWorldEngineData/characters.json), ensuring zero hardcoded characters in C# code.
    /// </summary>
    public static class CanonicalCharacterRegistry
    {
        private static readonly List<CanonicalCharacterDefinition> _characters = new List<CanonicalCharacterDefinition>();

        static CanonicalCharacterRegistry()
        {
            LoadManifest();
        }

        public static void LoadManifest()
        {
            _characters.Clear();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var install = DynamicWorldEngine.Discovery.GtaInstallationLocator.LocateInstallation();

            List<string> candidatePaths = new List<string>
            {
                Path.Combine(baseDir, "DynamicWorldEngineData", "characters.json"),
                Path.Combine(baseDir, "scripts", "DynamicWorldEngineData", "characters.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "DynamicWorldEngineData", "characters.json")
            };

            if (install != null && !string.IsNullOrEmpty(install.RootPath))
            {
                candidatePaths.Add(Path.Combine(install.RootPath, "scripts", "DynamicWorldEngineData", "characters.json"));
                candidatePaths.Add(Path.Combine(install.RootPath, "DynamicWorldEngineData", "characters.json"));
            }

            string manifestPath = candidatePaths.Find(File.Exists);
            if (manifestPath == null) return;

            try
            {
                string json = File.ReadAllText(manifestPath);
                JArray arr = JArray.Parse(json);
                foreach (JObject item in arr)
                {
                    var def = new CanonicalCharacterDefinition
                    {
                        CharacterId = (string)item["CharacterId"],
                        FullName = (string)item["FullName"],
                        PedModel = (string)item["PedModel"],
                        DefaultOccupation = (string)item["DefaultOccupation"],
                        PublicFaction = (string)item["PublicFaction"],
                        CanonDescription = (string)item["CanonDescription"],
                        DlcOrigin = (string)item["DlcOrigin"],
                        DefaultScenario = item["DefaultScenario"] != null ? (string)item["DefaultScenario"] : "WORLD_HUMAN_STAND",
                        DefaultInitialTask = item["DefaultInitialTask"] != null ? (string)item["DefaultInitialTask"] : ""
                    };

                    if (item["Roles"] != null)
                    {
                        foreach (var token in (JArray)item["Roles"]) def.Roles.Add((string)token);
                    }

                    if (item["AssociatedInteriors"] != null)
                    {
                        foreach (var token in (JArray)item["AssociatedInteriors"]) def.AssociatedInteriors.Add((string)token);
                    }

                    if (item["Keywords"] != null)
                    {
                        foreach (var token in (JArray)item["Keywords"]) def.Keywords.Add((string)token);
                    }

                    _characters.Add(def);
                }
            }
            catch { }
        }

        public static List<CanonicalCharacterDefinition> GetAllCharacters()
        {
            if (_characters.Count == 0) LoadManifest();
            return new List<CanonicalCharacterDefinition>(_characters);
        }

        public static CanonicalCharacterDefinition FindBestMatch(string query, string roleFilter = null)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;
            if (_characters.Count == 0) LoadManifest();

            string q = query.ToLowerInvariant();
            string rFilter = (roleFilter ?? "").ToLowerInvariant();

            int bestScore = 0;
            CanonicalCharacterDefinition bestMatch = null;

            foreach (var ch in _characters)
            {
                int score = 0;

                // Role filter requirement
                if (!string.IsNullOrEmpty(rFilter))
                {
                    if (ch.Roles.Exists(r => r.ToLowerInvariant().Contains(rFilter)))
                    {
                        score += 30;
                    }
                    else if (ch.DefaultOccupation.ToLowerInvariant().Contains(rFilter))
                    {
                        score += 20;
                    }
                }

                // Full name match
                if (q.Contains(ch.FullName.ToLowerInvariant()))
                {
                    score += 50;
                }

                // Ped model direct match
                if (q.Contains(ch.PedModel.ToLowerInvariant()))
                {
                    score += 40;
                }

                // Keyword match
                foreach (var kw in ch.Keywords)
                {
                    if (q.Contains(kw.ToLowerInvariant()))
                    {
                        score += 15;
                    }
                }

                // Associated Interior match
                foreach (var intId in ch.AssociatedInteriors)
                {
                    if (q.Contains(intId.ToLowerInvariant()))
                    {
                        score += 10;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = ch;
                }
            }

            return (bestScore >= 20) ? bestMatch : null;
        }
    }
}
