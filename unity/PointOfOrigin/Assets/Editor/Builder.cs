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
        const string Version = "1.0.0";
        const string IconPath = "Assets/Icon/icon.png";

        [MenuItem("Point of Origin/Build Windows (x64)")]
        public static void PerformBuild() => PerformBuildTo(Output);

        /// <summary>Build to another folder, for when a running player holds the usual one.</summary>
        public static void PerformBuildTo(string output)
        {
            PlayerSettings.productName = "Point of Origin";
            PlayerSettings.companyName = "Londopy";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            // the exe icon: one texture for every size Windows asks for
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
            {
                var sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Application);
                var icons = new Texture2D[sizes.Length];
                for (int i = 0; i < icons.Length; i++) icons[i] = icon;
                PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Application);
            }
            else Debug.LogWarning($"Point of Origin build: no icon at {IconPath}, the player keeps the default one");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"Point of Origin build: {summary.result}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors -> {Path.GetFullPath(output)}");
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
