using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;


// The module build from the command line, the same as File → Build Profiles → Web - Module - Release → Build:
//   Unity.exe -batchmode -projectPath <repo> -executeMethod ModuleBuild.BuildFromCommandLine -logFile build.log
// Output Builds/ModuleWeb (then Tools/package-module-build.sh). Exits with 0 on success, 1 otherwise.
public static class ModuleBuild {
    private const string ProfilePath = "Assets/Settings/Build Profiles/Web - Module - Release.asset";
    private const string OutputPath = "Builds/ModuleWeb";                       // the folder name gives ModuleWeb.loader.js


    [MenuItem("Module/Build module (Web - Module - Release)")]
    public static void BuildFromMenu() => Build();


    public static void BuildFromCommandLine() {
        EditorApplication.Exit(Build() ? 0 : 1);
    }


    private static bool Build() {
        var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(ProfilePath);
        if (profile == null) {
            Debug.LogError($"ModuleBuild: build profile not found: {ProfilePath}");
            return false;
        }
        BuildProfile.SetActiveBuildProfile(profile);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions {
            buildProfile = profile,
            locationPathName = OutputPath,
            options = BuildOptions.None,
        });
        BuildSummary summary = report.summary;
        Debug.Log($"ModuleBuild: {summary.result}, {summary.totalErrors} errors, {summary.totalSize / (1024 * 1024)} MB, " +
                  $"{summary.totalTime.TotalMinutes:0.0} min → {OutputPath}");
        return summary.result == BuildResult.Succeeded;
    }
}
