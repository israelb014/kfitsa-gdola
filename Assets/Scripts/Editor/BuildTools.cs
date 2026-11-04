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
            t.filterMode = FilterMode.Point; t.mipmapEnabled = false; t.alphaIsTransparency = true;
            t.textureCompression = TextureImporterCompression.Uncompressed; t.npotScale = TextureImporterNPOTScale.None;
            t.wrapMode = TextureWrapMode.Clamp;
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

            var mat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(mat, "Assets/Resources/Art/SpriteUnlit.mat");

            // Scene: camera and game object; the level itself builds at runtime from Resources/Levels
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 7.5f; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.84f, .94f, .95f);
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = Art.PPU; ppc.refResolutionX = 480; ppc.refResolutionY = 270;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.None;
            new GameObject("Game").AddComponent<Game>();
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

        public static void BuildLinux()
        {
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = "Builds/proto/KfitsaGdola.x86_64",
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            });
            Debug.Log("BUILD " + r.summary.result + " size=" + r.summary.totalSize);
            if (r.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
