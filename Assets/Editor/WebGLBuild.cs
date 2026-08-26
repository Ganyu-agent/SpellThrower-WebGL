#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SpellThrower.BuildTools
{
    /// <summary>심사용 WebGL 플레이어를 같은 씬 구성으로 반복 빌드한다.</summary>
    public static class WebGLBuild
    {
        static readonly string[] Scenes =
        {
            "Assets/Scenes/MatchmakingScene 1.unity",
            "Assets/Scenes/GameScene.unity"
        };

        public static void Build()
        {
            foreach (var scene in Scenes)
            {
                if (!File.Exists(scene))
                    throw new FileNotFoundException("Required WebGL scene is missing.", scene);
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Could not resolve the Unity project root.");
            var output = Environment.GetEnvironmentVariable("WEBGL_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(projectRoot, "WebGLBuild");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = Path.GetFullPath(output),
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"WebGL build failed: {report.summary.result}, errors={report.summary.totalErrors}");

            File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty);
            Debug.Log($"WebGL build succeeded: {Path.GetFullPath(output)} " +
                      $"({report.summary.totalSize} bytes)");
        }
    }
}
#endif
