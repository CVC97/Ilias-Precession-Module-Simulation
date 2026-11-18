using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;


// The module and lecture builds from the command line, the same as File → Build Profiles → <profile> → Build:
//   Unity.exe -batchmode -projectPath <repo> -executeMethod ModuleBuild.BuildFromCommandLine -logFile build.log
//   Unity.exe -batchmode -projectPath <repo> -executeMethod ModuleBuild.BuildLectureFromCommandLine -logFile build.log
// Output Builds/ModuleWeb or Builds/LectureWeb (then Tools/package-module-build.sh [LectureWeb]). Exits with 0 on
// success, 1 otherwise.
public static class ModuleBuild {
    private const string ProfilePath = "Assets/Settings/Build Profiles/Web - Module - Release.asset";
    private const string OutputPath = "Builds/ModuleWeb";                       // the folder name gives ModuleWeb.loader.js
    private const string LectureProfilePath = "Assets/Settings/Build Profiles/Web - Lecture - Release.asset";
    private const string LectureOutputPath = "Builds/LectureWeb";               // LectureWeb.loader.js


    [MenuItem("Module/Build module (Web - Module - Release)")]
    public static void BuildFromMenu() => Build(ProfilePath, OutputPath);

    [MenuItem("Lecture/Build lecture (Web - Lecture - Release)")]
    public static void BuildLectureFromMenu() => Build(LectureProfilePath, LectureOutputPath);


    public static void BuildFromCommandLine() {
        EditorApplication.Exit(Build(ProfilePath, OutputPath) ? 0 : 1);
    }

    public static void BuildLectureFromCommandLine() {
        EditorApplication.Exit(Build(LectureProfilePath, LectureOutputPath) ? 0 : 1);
    }


    private static bool Build(string profilePath, string outputPath) {
        var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
        if (profile == null) {
            Debug.LogError($"ModuleBuild: build profile not found: {profilePath}");
            return false;
        }
        BuildProfile.SetActiveBuildProfile(profile);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions {
            buildProfile = profile,
            locationPathName = outputPath,
            options = BuildOptions.None,
        });
        BuildSummary summary = report.summary;
        Debug.Log($"ModuleBuild: {summary.result}, {summary.totalErrors} errors, {summary.totalSize / (1024 * 1024)} MB, " +
                  $"{summary.totalTime.TotalMinutes:0.0} min → {outputPath}");
        return summary.result == BuildResult.Succeeded;
    }
}
