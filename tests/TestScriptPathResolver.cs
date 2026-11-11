using System;
using System.IO;
using DynamicWorldEngine.Utils;

namespace DynamicWorldEngine.Tests
{
    internal static class TestScriptPathResolver
    {
        private static int _passed;
        private static int _total;

        private static void AssertEqual(string name, string expected, string actual)
        {
            _total++;
            if (string.Equals(
                Path.GetFullPath(expected).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(actual).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            {
                _passed++;
                Console.WriteLine("  [PASS] " + name);
            }
            else
            {
                Console.WriteLine("  [FAIL] " + name + " Expected=" + expected + " Actual=" + actual);
            }
        }

        private static int Main(string[] args)
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "DWE_PathResolver_" + Guid.NewGuid().ToString("N"));
            string gameRoot = Path.Combine(tempRoot, "GTAVEnhanced");
            string scripts = Path.Combine(gameRoot, "scripts");
            string nestedScripts = Path.Combine(scripts, "scripts", "scripts");
            string noScriptsRoot = Path.Combine(tempRoot, "NoScriptsRoot");

            try
            {
                Directory.CreateDirectory(nestedScripts);
                Directory.CreateDirectory(noScriptsRoot);

                AssertEqual(
                    "Game-root base directory resolves to its scripts child",
                    scripts,
                    ScriptPathResolver.ResolveScriptsDirectory(gameRoot));

                AssertEqual(
                    "Scripts base directory resolves to itself, not scripts/scripts",
                    scripts,
                    ScriptPathResolver.ResolveScriptsDirectory(scripts));

                AssertEqual(
                    "Legacy nested scripts path collapses to the outer scripts directory",
                    scripts,
                    ScriptPathResolver.ResolveScriptsDirectory(nestedScripts));

                AssertEqual(
                    "Nested scripts base resolves to the actual GTA installation root",
                    gameRoot,
                    ScriptPathResolver.ResolveGameRootDirectory(nestedScripts));

                AssertEqual(
                    "Directory without a scripts child remains the scripts fallback",
                    noScriptsRoot,
                    ScriptPathResolver.ResolveScriptsDirectory(noScriptsRoot));

                AssertEqual(
                    "Directory without a scripts suffix remains the game-root fallback",
                    noScriptsRoot,
                    ScriptPathResolver.ResolveGameRootDirectory(noScriptsRoot));
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
                }
                catch { }
            }

            Console.WriteLine(string.Format("Path resolver tests: {0}/{1} passed.", _passed, _total));
            return _passed == _total ? 0 : 1;
        }
    }
}
