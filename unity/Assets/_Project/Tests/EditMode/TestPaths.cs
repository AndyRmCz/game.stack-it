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
        public static string SimVectorsDir()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, "shared", "sim-vectors");
                    if (Directory.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
            }
            throw new DirectoryNotFoundException("shared/sim-vectors not found above " + Directory.GetCurrentDirectory());
        }
    }
}
