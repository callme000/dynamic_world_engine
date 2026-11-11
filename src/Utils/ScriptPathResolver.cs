using System;
using System.IO;

namespace DynamicWorldEngine.Utils
{
    /// <summary>
    /// Resolves SHVDN's scripts directory without appending another "scripts"
    /// when AppDomain.BaseDirectory already points inside it.
    /// </summary>
    public static class ScriptPathResolver
    {
        public static string ResolveScriptsDirectory(string baseDirectory)
        {
            string fullPath = Normalize(baseDirectory);
            DirectoryInfo current = new DirectoryInfo(fullPath);

            // SHVDN commonly sets AppDomain.BaseDirectory to the configured scripts
            // location itself. Also collapse legacy nested scripts\scripts paths.
            if (IsScriptsDirectory(current))
            {
                while (current.Parent != null && IsScriptsDirectory(current.Parent))
                {
                    current = current.Parent;
                }
                return current.FullName;
            }

            string childScripts = Path.Combine(fullPath, "scripts");
            if (Directory.Exists(childScripts))
            {
                return Path.GetFullPath(childScripts);
            }

            return fullPath;
        }

        public static string ResolveGameRootDirectory(string baseDirectory)
        {
            string fullPath = Normalize(baseDirectory);
            DirectoryInfo current = new DirectoryInfo(fullPath);

            if (!IsScriptsDirectory(current))
            {
                return fullPath;
            }

            // Walk out through any accidental nested scripts directories first.
            while (current.Parent != null && IsScriptsDirectory(current.Parent))
            {
                current = current.Parent;
            }

            return current.Parent != null ? current.Parent.FullName : current.FullName;
        }

        private static bool IsScriptsDirectory(DirectoryInfo directory)
        {
            return directory != null &&
                string.Equals(directory.Name, "scripts", StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string path)
        {
            string candidate = string.IsNullOrWhiteSpace(path)
                ? AppDomain.CurrentDomain.BaseDirectory
                : path;

            string fullPath = Path.GetFullPath(candidate);
            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
