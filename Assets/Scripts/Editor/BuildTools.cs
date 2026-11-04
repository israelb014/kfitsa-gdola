using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kfitsa.EditorTools
{
    public class PixelImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Art/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.filterMode = FilterMode.Bilinear; t.mipmapEnabled = false; t.alphaIsTransparency = true;
            t.textureCompression = TextureImporterCompression.Uncompressed; t.npotScale = TextureImporterNPOTScale.None;
            t.wrapMode = TextureWrapMode.Clamp;
        }
    }

    public class AudioImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
            var a = (AudioImporter)assetImporter;
            bool music = Path.GetFileName(assetPath).StartsWith("m_");
            var s = a.defaultSampleSettings;
            s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = .6f;
            s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            a.defaultSampleSettings = s;
        }
    }

    public static class BuildTools
    {
        const string Scene = "Assets/Scenes/Main.unity", Settings = "Assets/Settings";

        public static void Setup()
        {
            Directory.CreateDirectory(Settings); Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();

            // URP with the 2D renderer. The creation helper is internal, so reach it the way the menu does.
            string rendererPath = Settings + "/Renderer2D.asset", urpPath = Settings + "/URP.asset";
            var data = AssetDatabase.LoadAssetAtPath<Renderer2DData>(rendererPath);
            if (data == null)
            {
                var menus = typeof(UniversalRenderPipelineAsset).Assembly.GetType("UnityEditor.Rendering.Universal.Renderer2DMenus")
                    ?? Type.GetType("UnityEditor.Rendering.Universal.Renderer2DMenus, Unity.RenderPipelines.Universal.Editor");
                var make = menus.GetMethod("CreateRendererAsset", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                data = (Renderer2DData)make.Invoke(null, new object[] { rendererPath, RendererType._2DRenderer, false, "Renderer" });
            }
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpPath);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(data);
                AssetDatabase.CreateAsset(urp, urpPath);
            }
            GraphicsSettings.defaultRenderPipeline = urp;
            for (int i = 0; i < QualitySettings.count; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }

            Directory.CreateDirectory("Assets/Resources/Materials"); AssetDatabase.Refresh();
            var mat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(mat, "Assets/Resources/Materials/SpriteUnlit.mat");

            // Scene: camera and game object; the level itself builds at runtime from Resources/Levels
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 7.5f; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.84f, .94f, .95f);
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = Art.PPU; ppc.refResolutionX = 1920; ppc.refResolutionY = 1080;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.None;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.None;
            new GameObject("App").AddComponent<App>();
            EditorSceneManager.SaveScene(scene, Scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };

            PlayerSettings.productName = "קפיצה גדולה";
            PlayerSettings.companyName = "Kfitsa";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            Debug.Log("SETUP OK");
        }

        public static void BuildLinux() => Build("Builds/linux/KfitsaGdola.x86_64", BuildTarget.StandaloneLinux64);
        public static void BuildWindows() => Build("Builds/windows/KfitsaGdola.exe", BuildTarget.StandaloneWindows64);

        static void Build(string path, BuildTarget target)
        {
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Scene }, locationPathName = path, target = target, options = BuildOptions.None });
            Debug.Log("BUILD " + r.summary.result + " size=" + r.summary.totalSize);
            if (r.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
