using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Linq;

public class MapEnumGenerator : AssetPostprocessor {
    private const string MapsFolder = "Assets/Maps";
    private const string OutputFolder = "Assets/Scripts";
    private const string OutputFile = "MapNameEnum.cs";
    private const string EnumName = "MapName";

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom) {
        Debug.Log("OnPostprocessAllAssets");
        if (imported.Any(p => p.StartsWith(MapsFolder)) ||
            deleted.Any(p => p.StartsWith(MapsFolder)) ||
            moved.Any(p => p.StartsWith(MapsFolder)) ||
            movedFrom.Any(p => p.StartsWith(MapsFolder))) {
            GenerateEnum();
        }
    }

    [MenuItem("Tools/Regenerate MapName Enum")]
    public static void GenerateEnumMenu() => GenerateEnum();

    public static void GenerateEnum() {
        if (!Directory.Exists(MapsFolder)) return;
        var files = Directory.GetFiles(MapsFolder, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        if (files.Count == 0) return;
        if (!Directory.Exists(OutputFolder)) Directory.CreateDirectory(OutputFolder);
        var sb = new StringBuilder();
        sb.AppendLine("// Auto-generated. Do not edit manually.");
        sb.AppendLine("public enum MapName {");
        foreach (var name in files) {
            var safe = ToPascalCase(name);
            sb.AppendLine($"    {safe},");
        }
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(OutputFolder, OutputFile), sb.ToString());
        AssetDatabase.Refresh();
    }

    private static string ToPascalCase(string input) {
        var parts = input.Split(new[]{'_', '-', ' '}, System.StringSplitOptions.RemoveEmptyEntries);
        return string.Join("", parts.Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
    }
} 

// public class MapEnumGenerator : EditorWindow
// {
//     [MenuItem("Tools/Regenerate MapName Enum")]
//     public static void GenerateEnumMenu()
//     {
//         string mapsFolder = "Assets/Maps";
//         string outputFile = "Assets/Scripts/MapNameEnum.cs";
//         if (!Directory.Exists(mapsFolder))
//         {
//             Debug.LogWarning("Maps folder not found: " + mapsFolder);
//             return;
//         }
//         var files = Directory.GetFiles(mapsFolder, "*.json")
//             .Select(f => Path.GetFileNameWithoutExtension(f))
//             .Distinct()
//             .OrderBy(x => x)
//             .ToList();
//         if (files.Count == 0)
//         {
//             Debug.LogWarning("No .json files found in: " + mapsFolder);
//             return;
//         }
//         var sb = new StringBuilder();
//         sb.AppendLine("// Auto-generated. Do not edit manually.");
//         sb.AppendLine("public enum MapName {");
//         foreach (var name in files)
//         {
//             var safe = ToPascalCase(name);
//             sb.AppendLine($"    {safe},");
//         }
//         sb.AppendLine("}");
//         File.WriteAllText(outputFile, sb.ToString());
//         Debug.Log("MapNameEnum.cs generated with: " + string.Join(", ", files));
//         AssetDatabase.Refresh();
//     }

//     private static string ToPascalCase(string input)
//     {
//         var parts = input.Split(new[] { '_', '-', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
//         return string.Join("", parts.Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
//     }
// }