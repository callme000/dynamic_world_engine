using System;
using System.Collections.Generic;
using System.IO;

namespace DynamicWorldEngine.Archetypes
{
    /// <summary>
    /// Data-driven Environment Catalog.
    ///
    /// Template archetypes are loaded from external JSON files in
    /// DynamicWorldEngineData/environments/, not embedded in C# source code.
    ///
    /// This eliminates hard-coded model names from the codebase. Prop and NPC
    /// model assignments within the templates are controlled by the JSON data files.
    /// To add, modify or remove an archetype, edit or add a JSON file — no recompile required.
    ///
    /// Template files are discovery-driven: GetAvailableArchetypes() lists whatever files
    /// are present in the environments/ directory, including any future additions.
    /// </summary>
    public static class EnvironmentCatalog
    {
        private static string _environmentsDirectory = null;

        /// <summary>
        /// Sets the root directory that contains DynamicWorldEngineData/.
        /// Must be called before any Get* methods. Typically called by EngineCore.
        /// </summary>
        public static void SetDataDirectory(string dataDirectory)
        {
            if (!string.IsNullOrEmpty(dataDirectory))
            {
                _environmentsDirectory = Path.Combine(dataDirectory, "environments");
            }
        }

        // -------------------------------------------------------------------------
        // Discovery
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns all archetype names available in the environments/ directory,
        /// derived from file names (e.g. "gambling_den" from gambling_den.json).
        /// Future archetypes are discovered automatically without source-code changes.
        /// </summary>
        public static IEnumerable<string> GetAvailableArchetypes()
        {
            string dir = ResolveDirectory();
            if (!Directory.Exists(dir)) yield break;

            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                yield return Path.GetFileNameWithoutExtension(file);
            }
        }

        // -------------------------------------------------------------------------
        // Named-archetype convenience accessors (backwards-compatible API)
        // These call GetArchetypeJson() with the matching archetype name.
        // -------------------------------------------------------------------------

        public static string GetGamblingDenJson()     { return GetArchetypeJson("gambling_den");     }
        public static string GetStreetVendorJson()    { return GetArchetypeJson("street_vendor");    }
        public static string GetChopShopJson()        { return GetArchetypeJson("chop_shop");        }
        public static string GetPoliceRoadblockJson() { return GetArchetypeJson("police_checkpoint"); }
        public static string GetBbqPartyJson()        { return GetArchetypeJson("bbq_party");        }

        // -------------------------------------------------------------------------
        // Primary API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Loads an archetype template JSON string from the environments/ data directory.
        /// Returns null if the archetype file does not exist.
        /// Never fabricates or returns embedded hard-coded model strings.
        /// </summary>
        public static string GetArchetypeJson(string archetypeName)
        {
            if (string.IsNullOrEmpty(archetypeName)) return null;

            string dir  = ResolveDirectory();
            string path = Path.Combine(dir, archetypeName + ".json");

            if (!File.Exists(path))
            {
                Console.WriteLine(
                    "[EnvironmentCatalog] Archetype file not found: {0}. " +
                    "To add this archetype, create '{1}'.", archetypeName, path);
                return null;
            }

            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EnvironmentCatalog] Error reading archetype '{0}': {1}", archetypeName, ex.Message);
                return null;
            }
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static string ResolveDirectory()
        {
            if (!string.IsNullOrEmpty(_environmentsDirectory))
                return _environmentsDirectory;

            // Auto-discover relative to the assembly directory
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, "DynamicWorldEngineData", "environments");
        }
    }
}
