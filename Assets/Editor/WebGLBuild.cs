#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
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

            ConfigureFixedFhdCanvas(output);
            File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty);
            Debug.Log($"WebGL build succeeded: {Path.GetFullPath(output)} " +
                      $"({report.summary.totalSize} bytes)");
        }

        static void ConfigureFixedFhdCanvas(string output)
        {
            var indexPath = Path.Combine(output, "index.html");
            var html = File.ReadAllText(indexPath);
            html = Regex.Replace(html,
                "<canvas id=\"unity-canvas\" width=\\d+ height=\\d+",
                "<canvas id=\"unity-canvas\" width=1920 height=1080");
            html = html.Replace(
                "// config.matchWebGLToCanvasSize = false;",
                "config.matchWebGLToCanvasSize = false;");
            html = Regex.Replace(html,
                "canvas\\.style\\.width = \"\\d+px\";\\s*canvas\\.style\\.height = \"\\d+px\";",
                "canvas.style.width = \"100%\";\n        canvas.style.height = \"100%\";");
            File.WriteAllText(indexPath, html);

            var stylePath = Path.Combine(output, "TemplateData", "style.css");
            const string fhdMarker = "/* Fixed 1920x1080 render target";
            var style = File.ReadAllText(stylePath);
            if (!style.Contains(fhdMarker))
                File.AppendAllText(stylePath,
                "\n" + fhdMarker + ", scaled down without changing aspect ratio. */\n" +
                "html, body { width: 100%; height: 100%; overflow: hidden; background: #000; }\n" +
                "#unity-container.unity-desktop { width: min(100vw, calc((100vh - 38px) * 16 / 9)); }\n" +
                "#unity-container.unity-desktop #unity-canvas { width: 100%; height: auto; aspect-ratio: 16 / 9; display: block; }\n" +
                "#unity-container.unity-desktop #unity-footer { width: 100%; }\n" +
                "#unity-container.unity-mobile { left: 50%; top: 50%; transform: translate(-50%, -50%); width: min(100vw, calc(100vh * 16 / 9)); height: min(100vh, calc(100vw * 9 / 16)); }\n" +
                ".unity-mobile #unity-canvas { width: 100%; height: 100%; aspect-ratio: 16 / 9; display: block; }\n");
        }
    }
}
#endif
