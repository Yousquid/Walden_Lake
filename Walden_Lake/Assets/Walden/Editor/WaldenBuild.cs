using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Walden.Editor
{
    [InitializeOnLoad]
    public static class WaldenBuild
    {
        public const string ScenePath = "Assets/Scenes/WaldenDemo.unity";
        private const string ResourcesPath = "Assets/Walden/Resources/";

        static WaldenBuild()
        {
            // An opt-in request also lets an already-open editor build without a second instance.
            EditorApplication.update += CheckBuildRequest;
        }

        private static void CheckBuildRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
            string request = Path.Combine(Application.dataPath, "../Temp/WaldenBuild.request");
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { BuildWindows(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Walden/Open Demo Scene")]
        public static void OpenDemo()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Prepare();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Walden/Play Demo")]
        public static void PlayDemo()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Prepare();
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }

        public static void Prepare()
        {
            Directory.CreateDirectory(ResourcesPath);
            Shader surface = Shader.Find("Universal Render Pipeline/Lit");
            if (!surface) throw new InvalidOperationException("URP Lit shader is missing.");
            CreateMaterial("WaldenSurface", surface, false);
            CreateMaterial("WaldenGlow", surface, true);
            Shader sky = Shader.Find("Skybox/Procedural");
            if (sky) CreateMaterial("WaldenSky", sky, false);
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Walden — lakeside life").AddComponent<WaldenGame>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "WaldenStudio";
            PlayerSettings.productName = "Walden Lake";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            AssetDatabase.SaveAssets();
            Debug.Log("Walden demo scene and runtime materials are ready.");
        }

        private static void CreateMaterial(string name, Shader shader, bool glow)
        {
            string path = ResourcesPath + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) return;
            var material = new Material(shader) { name = name };
            if (glow)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(1.6f, .55f, .1f));
            }
            AssetDatabase.CreateAsset(material, path);
        }

        [MenuItem("Walden/Build Windows Demo")]
        public static void BuildWindows()
        {
            Prepare();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string build = Path.Combine(root, "Builds", "WaldenLakeUpdate02", "WaldenLake.exe");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-waldenBuildPath");
            if (index >= 0 && index + 1 < args.Length) build = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(build));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = build,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4
            });
            string summary = $"Walden build: {report.summary.result}; {report.summary.totalErrors} errors; {report.summary.totalSize / (1024 * 1024)} MB; {report.summary.totalTime}";
            string summaryPath = Path.Combine(root, "Builds", "build-summary.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(summaryPath));
            File.WriteAllText(summaryPath, summary);
            Debug.Log(summary);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(summary);
        }
    }
}
