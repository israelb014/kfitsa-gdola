using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
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
        /// For trying things out only; never published, so it may skip the level check while levels are being drawn.
        public static void BuildLinuxTest() { skipCheck = true; Build("Builds/test-linux/KfitsaGdola.x86_64", BuildTarget.StandaloneLinux64); }
        static bool skipCheck;

#if UNITY_ANDROID
        /// Unity writes these into the manifest with no setting to change them.
        class AndroidManifestFix : UnityEditor.Android.IPostGenerateGradleAndroidProject
        {
            public int callbackOrder => 0;

            public void OnPostGenerateGradleAndroidProject(string path)
            {
                string file = Path.Combine(path, "src/main/AndroidManifest.xml");
                string xml = File.ReadAllText(file);
                string Need(string text, string from, string to)
                {
                    if (text.Contains(to)) return text;   // an incremental build keeps the patched file
                    if (!text.Contains(from)) throw new BuildFailedException("Manifest has no " + from);
                    return text.Replace(from, to);
                }
                // Predictive back would close the app without ever telling it
                xml = Need(xml, "android:enableOnBackInvokedCallback=\"true\"", "android:enableOnBackInvokedCallback=\"false\"");
                xml = Need(xml, "android:name=\"com.unity3d.player.UnityPlayerGameActivity\"", "android:name=\"com.ibfix.kfitsagdola.GameMain\"");
                xml = Need(xml, "android:name=\"unity.run-without-focus\" android:value=\"true\"", "android:name=\"unity.run-without-focus\" android:value=\"false\"");
                if (!xml.Contains("android.permission.VIBRATE")) xml = xml.Replace("<application", "<uses-permission android:name=\"android.permission.VIBRATE\" />\n  <application");
                File.WriteAllText(file, xml);
            }
        }

        /// Run with -buildTarget Android. The keystore password comes only from the environment, see tools/build_android.sh.
        public static void BuildAndroid()
        {
            var id = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(id, "com.ibfix.kfitsagdola");
            PlayerSettings.bundleVersion = "1.2.0";
            PlayerSettings.Android.bundleVersionCode = 3;
            PlayerSettings.SetScriptingBackend(id, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(id, ManagedStrippingLevel.Minimal);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            // 34 keeps the Back key reaching the game: from 36 the system takes it over for predictive back
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.buildAppBundle = false;
            SetIcons(id);

            string pass = Environment.GetEnvironmentVariable("KFITSA_KEYSTORE_PASS");
            if (string.IsNullOrEmpty(pass)) { Debug.LogError("KFITSA_KEYSTORE_PASS is not set"); EditorApplication.Exit(1); return; }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = "Keystore/kfitsa-gdola.keystore";
            PlayerSettings.Android.keyaliasName = "kfitsagdola";
            PlayerSettings.Android.keystorePass = pass;
            PlayerSettings.Android.keyaliasPass = pass;
            Directory.CreateDirectory("Builds/android");
            AssetDatabase.SaveAssets();
            Build("Builds/android/KfitsaGdola-android.apk", BuildTarget.Android);
            PlayerSettings.Android.keystorePass = ""; PlayerSettings.Android.keyaliasPass = "";
        }

        static void SetIcons(NamedBuildTarget id)
        {
            Texture2D Load(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/" + name + ".png");
            var adaptive = PlayerSettings.GetPlatformIcons(id, UnityEditor.Android.AndroidPlatformIconKind.Adaptive);
            foreach (var i in adaptive) i.SetTextures(Load("icon_bg"), Load("icon_fg"));
            PlayerSettings.SetPlatformIcons(id, UnityEditor.Android.AndroidPlatformIconKind.Adaptive, adaptive);
        }
#endif

        /// Run by every build: a level with anything unreachable stops it.
        public static void Validate()
        {
            // A passed check holds until a level or a rule changes, so the three platform builds run it once
            string stamp = "Library/reach_passed.txt", now = Fingerprint();
            if (File.Exists(stamp) && File.ReadAllText(stamp) == now) { Debug.Log("VALIDATE OK (nothing changed since it last passed)"); return; }
            Levels.Folder = "Assets/Resources/Levels";
            bool ok = Reach.ValidateAll("verification/v1.2/reach_report.txt");
            Levels.Folder = null;
            Debug.Log("VALIDATE " + (ok ? "OK" : "FAILED, see verification/v1.2/reach_report.txt"));
            if (!ok) throw new BuildFailedException("Levels failed the reachability check, see verification/v1.2/reach_report.txt");
            File.WriteAllText(stamp, now);
        }

        static string Fingerprint()
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var all = new System.Text.StringBuilder();
            var files = new System.Collections.Generic.List<string>(Directory.GetFiles("Assets/Resources/Levels", "*.txt"));
            foreach (string name in new[] { "Tune", "Player", "Body", "Grid", "World", "Reach", "Actors" }) files.Add("Assets/Scripts/" + name + ".cs");
            files.Sort(StringComparer.Ordinal);
            foreach (string f in files) all.Append(f).Append(BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(f))));
            return all.ToString();
        }

        static void Build(string path, BuildTarget target)
        {
            try { if (!skipCheck) Validate(); }
            catch (BuildFailedException e) { Debug.LogError(e.Message); EditorApplication.Exit(1); return; }
            PlayerSettings.bundleVersion = "1.2.0";
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Scene }, locationPathName = path, target = target, options = BuildOptions.None });
            Debug.Log("BUILD " + r.summary.result + " size=" + r.summary.totalSize);
            if (r.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
