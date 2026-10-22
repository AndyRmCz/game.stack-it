using UnityEditor;

namespace StackIt.EditorTools
{
    /// <summary>
    /// Makes com.unity.ide.visualstudio generate SDK-style .csproj files (and unity.slnx) whatever
    /// external script editor is selected. Its legacy format is what Visual Studio 2022 gets by
    /// default, and C# Dev Kit in VS Code refuses to load it ("Old C# projects are not supported").
    /// The package discovers this hook by name on any AssetPostprocessor.
    /// </summary>
    public class ProjectFileStyle : AssetPostprocessor
    {
        private static string OnSelectingCSProjectStyle() => "SDK";
    }
}
