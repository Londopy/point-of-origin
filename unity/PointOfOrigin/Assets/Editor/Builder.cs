using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PointOfOrigin.EditorTools
{
    /// <summary>
    /// Windows build, from the menu or headless:
    ///     unity build unity/PointOfOrigin --editor-version 6000.6.0f1 --target StandaloneWindows64 --execute-method PointOfOrigin.EditorTools.Builder.PerformBuild
    /// </summary>
    public static class Builder
    {
        const string Output = "Build/Windows/PointOfOrigin.exe";

        [MenuItem("Point of Origin/Build Windows (x64)")]
        public static void PerformBuild()
        {
            PlayerSettings.productName = "Point of Origin";
            PlayerSettings.companyName = "Londopy";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = Output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"Point of Origin build: {summary.result}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors -> {Path.GetFullPath(Output)}");
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
