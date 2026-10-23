using System;
using System.IO;

namespace StackIt.Tests
{
    internal static class TestPaths
    {
        /// <summary>
        /// Finds shared/sim-vectors by walking up from the working directory (Unity: the project
        /// folder) or the test binary (dotnet test: DotNetTests/bin/...).
        /// </summary>
        public static string SimVectorsDir() => FindUp(Path.Combine("shared", "sim-vectors"));

        /// <summary>shared/theme/themes.json, the look tokens.</summary>
        public static string ThemesJson() => Path.Combine(FindUp(Path.Combine("shared", "theme")), "themes.json");

        /// <summary>Assets/_Project/Resources/StackIt, found the same way.</summary>
        public static string ResourcesDir() => FindUp(Path.Combine("Assets", "_Project", "Resources", "StackIt"));

        /// <summary>Assets/_Project/Scripts, found the same way.</summary>
        public static string ScriptsDir() => FindUp(Path.Combine("Assets", "_Project", "Scripts"));

        public static string FindUp(string relative)
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, relative);
                    if (Directory.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
            }
            throw new DirectoryNotFoundException(relative + " not found above " + Directory.GetCurrentDirectory());
        }
    }
}
