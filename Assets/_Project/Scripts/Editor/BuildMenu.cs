using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace RPG.EditorTools
{
    /// <summary>
    /// Build and run shortcuts.
    /// RPG > Build > Windows Player builds one executable that runs as client, host or server
    /// depending on its command line. RPG > Run starts that build as a local server or client.
    /// RPG > Build > WebGL builds the browser version (also built by GitHub Actions on every push to main).
    /// </summary>
    public static class BuildMenu
    {
        private const string PlayerExe = "Builds/Windows/RPG-s.exe";
        private const string ServerExe = "Builds/WindowsServer/RPG-s-Server.exe";
        public const string WebGLFolder = "Builds/WebGL";

        [MenuItem("RPG/Build/Windows Player")]
        public static void BuildWindowsPlayer()
        {
            Build(PlayerExe, StandaloneBuildSubtarget.Player);
        }

        [MenuItem("RPG/Build/Windows Dedicated Server")]
        public static void BuildWindowsServer()
        {
            if (!IsDedicatedServerModuleInstalled())
            {
                Debug.LogError("[Build] The Windows Dedicated Server module is not installed. " +
                               "Add it in Unity Hub (Installs > 6000.6.4f1 > Add modules > Windows Dedicated Server Build Support), " +
                               "or use RPG > Build > Windows Player and start it with -batchmode -nographics -server.");
                return;
            }
            Build(ServerExe, StandaloneBuildSubtarget.Server);
        }

        [MenuItem("RPG/Build/WebGL (browser)")]
        public static void BuildWebGL()
        {
            ConfigureWebGL();
            Build(WebGLFolder, BuildTarget.WebGL, 0, BuildOptions.None);
        }

        /// <summary>Batch mode / CI entry: Unity.exe -batchmode -projectPath . -executeMethod RPG.EditorTools.BuildMenu.BuildWebGLFromCommandLine -quit</summary>
        public static void BuildWebGLFromCommandLine()
        {
            ConfigureWebGL();
            if (!Build(WebGLFolder, BuildTarget.WebGL, 0, BuildOptions.None))
                EditorApplication.Exit(1);
        }

        private static void ConfigureWebGL()
        {
            // Gzip with a JavaScript fallback works on any static host (GitHub Pages cannot set compression headers).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;

            // Build speed over peak runtime speed: this is a small 2D game and builds run on every push.
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
            SetWasmCodeOptimizationForBuildTimes();
        }

        /// <summary>
        /// WebGL "Code Optimization: Shorter Build Time". It lives in the WebGL module's editor assembly and is
        /// stored per machine, not in the project, so set it before every build (reflection keeps this file
        /// compiling where the WebGL module is not installed).
        /// </summary>
        private static void SetWasmCodeOptimizationForBuildTimes()
        {
            foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type settings = assembly.GetType("UnityEditor.WebGL.UserBuildSettings");
                System.Type optimization = assembly.GetType("UnityEditor.WebGL.WasmCodeOptimization");
                if (settings == null || optimization == null) continue;

                settings.GetProperty("codeOptimization")?.SetValue(null, System.Enum.Parse(optimization, "BuildTimes"));
                return;
            }
            Debug.LogWarning("[Build] WebGL code optimization setting not found; using the default.");
        }

        [MenuItem("RPG/Run/Local Server (last Windows Player build)")]
        public static void RunLocalServer()
        {
            Launch("-batchmode -nographics -server -logFile Builds/Windows/server.log");
        }

        [MenuItem("RPG/Run/Local Client (last Windows Player build)")]
        public static void RunLocalClient()
        {
            Launch("-client -ip 127.0.0.1 -screen-fullscreen 0 -screen-width 1280 -screen-height 720");
        }

        [MenuItem("RPG/Run/Local Bot Client (last Windows Player build)")]
        public static void RunLocalBotClient()
        {
            Launch("-client -ip 127.0.0.1 -bot -screen-fullscreen 0 -screen-width 960 -screen-height 540");
        }

        public static bool Build(string outputPath, StandaloneBuildSubtarget subtarget)
        {
            return Build(outputPath, BuildTarget.StandaloneWindows64, (int)subtarget, BuildOptions.Development);
        }

        public static bool Build(string outputPath, BuildTarget target, int subtarget, BuildOptions buildOptions)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                subtarget = subtarget,
                options = buildOptions,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool succeeded = report.summary.result == BuildResult.Succeeded;
            if (succeeded)
                Debug.Log($"[Build] {target} build finished: {outputPath} ({report.summary.totalTime.TotalSeconds:0}s).");
            else
                Debug.LogError($"[Build] {target} build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            return succeeded;
        }

        /// <summary>Batch mode entry: Unity.exe -batchmode -projectPath . -executeMethod RPG.EditorTools.BuildMenu.BuildWindowsPlayerFromCommandLine -quit</summary>
        public static void BuildWindowsPlayerFromCommandLine()
        {
            if (!Build(PlayerExe, StandaloneBuildSubtarget.Player))
                EditorApplication.Exit(1);
        }

        private static bool IsDedicatedServerModuleInstalled()
        {
            string engine = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.StandaloneWindows64, BuildOptions.None);
            return Directory.Exists(Path.Combine(engine, "Variations", "win64_server_development_mono"))
                   || Directory.Exists(Path.Combine(engine, "Variations", "win64_server_nondevelopment_mono"));
        }

        private static void Launch(string arguments)
        {
            string exe = Path.GetFullPath(PlayerExe);
            if (!File.Exists(exe))
            {
                Debug.LogError($"[Run] {PlayerExe} not found. Build it first with RPG > Build > Windows Player.");
                return;
            }

            Process.Start(new ProcessStartInfo(exe, arguments)
            {
                WorkingDirectory = Path.GetFullPath("."),
                UseShellExecute = false,
            });
            Debug.Log($"[Run] Started {PlayerExe} {arguments}");
        }
    }
}
