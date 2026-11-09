using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
            PlayerSettings.bundleVersion = "2.8.0";
            PlayerSettings.Android.bundleVersionCode = 20;
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
            bool ok = Reach.ValidateAll("verification/v2.8/reach_report.txt");
            Levels.Folder = null;
            Debug.Log("VALIDATE " + (ok ? "OK" : "FAILED, see verification/v2.8/reach_report.txt"));
            if (!ok) throw new BuildFailedException("Levels failed the reachability check, see verification/v2.8/reach_report.txt");
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
            try { if (!skipCheck) { Validate(); AudioBefore(); } }
            catch (BuildFailedException e) { Debug.LogError(e.Message); EditorApplication.Exit(1); return; }
            PlayerSettings.bundleVersion = "2.8.0";
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Scene }, locationPathName = path, target = target, options = BuildOptions.None });
            Debug.Log("BUILD " + r.summary.result + " size=" + r.summary.totalSize);
            if (r.summary.result != BuildResult.Succeeded) { EditorApplication.Exit(1); return; }
            try { if (!skipCheck) AudioAfter(path, target); }
            catch (BuildFailedException e) { Debug.LogError("AUDIO FAILED: " + e.Message); EditorApplication.Exit(1); }
        }

        const string AudioDir = "Assets/Resources/Audio", AudioStamp = "Library/audio_passed.txt", AudioReport = "verification/v2.8";

        static string[] AudioFiles() => Directory.GetFiles(AudioDir).Where(f => !f.EndsWith(".meta")).Select(f => f.Replace('\\', '/')).OrderBy(f => f, StringComparer.Ordinal).ToArray();

        /// Checks that need no player: the sound list, the files, the project settings and the default volumes.
        static void AudioBefore()
        {
            var listed = new HashSet<string>(Sound.Effects.Concat(Sound.Tracks));
            var files = AudioFiles();
            var names = new HashSet<string>(files.Select(Path.GetFileNameWithoutExtension));
            foreach (string name in listed.Except(names)) throw new BuildFailedException("Sound listed but no file: " + name);
            foreach (string name in names.Except(listed)) throw new BuildFailedException("Sound file not in Sound.cs: " + name);

            foreach (string f in files)
            {
                using (var read = File.OpenRead(f))
                {
                    var head = new byte[24];
                    read.Read(head, 0, head.Length);
                    if (System.Text.Encoding.ASCII.GetString(head).StartsWith("version https://git-lfs")) throw new BuildFailedException("Sound is only a Git LFS pointer: " + f);
                }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(f);
                if (clip == null || clip.length <= 0) throw new BuildFailedException("Sound does not import: " + f);
            }

            var asked = new HashSet<string>(Levels.All.Select(l => l.music));
            foreach (string code in Directory.GetFiles("Assets/Scripts", "*.cs"))
                foreach (Match m in Regex.Matches(File.ReadAllText(code), "Sound\\.(?:Play|Music)\\(\"([^\"]+)\""))
                    asked.Add(m.Groups[1].Value);
            foreach (string name in asked.Except(listed)) throw new BuildFailedException("Code plays a sound that does not exist: " + name);

            var fresh = new Save();
            if (fresh.music <= 0 || fresh.sfx <= 0) throw new BuildFailedException("Default volume is 0");

            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0]);
            if (settings.FindProperty("m_DisableAudio").boolValue) throw new BuildFailedException("Audio is disabled in the project settings");
            if (settings.FindProperty("m_Volume").floatValue <= 0) throw new BuildFailedException("Project audio volume is 0");
            Debug.Log("AUDIO sound list, files, settings and default volumes OK");
        }

        /// Checks on the player just built. Linux runs here, so its test proves the code and the sounds;
        /// the other two must carry the same files, and Android is played on the emulator by tools/build_android.sh.
        static void AudioAfter(string path, BuildTarget target)
        {
            CheckPacked(path, target);
            Debug.Log("AUDIO all " + AudioFiles().Length + " sounds are in the player");

            string now = AudioFingerprint();
            if (target != BuildTarget.StandaloneLinux64)
            {
                if (!File.Exists(AudioStamp) || File.ReadAllText(AudioStamp) != now) throw new BuildFailedException("The audio play test has not passed on this code: build Linux first");
                Debug.Log("AUDIO play test passed on the Linux build of the same code");
                return;
            }

            Directory.CreateDirectory(AudioReport);
            PlayerTest(path, "-audiotest", "audio_report.txt", "music started", "sfx started", "ALL PASS");
            PlayerTest(path, "-stormtest", "storm_report.txt", "ALL PASS");
            File.WriteAllText(AudioStamp, now);
            Debug.Log("AUDIO play test passed");
        }

        static void PlayerTest(string path, string flag, string reportName, params string[] needs)
        {
            string dir = Path.GetFullPath(AudioReport), report_ = Path.Combine(dir, reportName);
            File.Delete(report_);
            var run = new System.Diagnostics.ProcessStartInfo(Path.GetFullPath(path), $"-batchmode -nographics {flag} -captureDir \"{dir}\" -logFile \"{dir}/{flag.Substring(1)}_player.log\"") { UseShellExecute = false };
            run.Environment["PULSE_SERVER"] = "/nonexistent";   // the measurement is inside the player, so the build need not play through the speakers
            using (var player = System.Diagnostics.Process.Start(run))
            {
                if (!player.WaitForExit(120000)) { player.Kill(); throw new BuildFailedException(flag + " did not finish"); }
                string text = File.Exists(report_) ? File.ReadAllText(report_) : "";
                foreach (string need in needs)
                    if (!text.Contains(need)) throw new BuildFailedException(flag + " did not log \"" + need + "\", see " + report_);
                if (player.ExitCode != 0) throw new BuildFailedException(flag + " failed, see " + report_);
            }
        }

        /// Opens the built files, because this Unity leaves the build report's asset list empty. On desktop a clip's
        /// name sits in resources.assets as a length and then its letters; the samples sit in resources.resource.
        static void CheckPacked(string path, BuildTarget target)
        {
            if (target == BuildTarget.Android)
            {
                // The APK stores each clip's samples in its own file with a hashed name; tools/build_android.sh then loads every clip by name
                using var apk = System.IO.Compression.ZipFile.OpenRead(path);
                var parts = apk.Entries.Where(e => e.FullName.StartsWith("assets/bin/Data/") && e.FullName.EndsWith(".resource")).ToArray();
                if (parts.Length != AudioFiles().Length || parts.Any(e => e.Length == 0)) throw new BuildFailedException($"The APK has {parts.Length} sound files, not {AudioFiles().Length}");
                if (parts.Sum(e => e.Length) < 1000000) throw new BuildFailedException("The APK has almost no sound data");
                return;
            }
            string data = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "_Data");
            byte[] index = File.ReadAllBytes(Path.Combine(data, "resources.assets"));
            long samples = new FileInfo(Path.Combine(data, "resources.resource")).Length;
            if (samples < 1000000) throw new BuildFailedException("The player has almost no sound data: " + samples + " bytes");
            foreach (string f in AudioFiles())
            {
                string name = Path.GetFileNameWithoutExtension(f);
                var key = BitConverter.GetBytes(name.Length).Concat(System.Text.Encoding.ASCII.GetBytes(name)).ToArray();
                if (!Contains(index, key)) throw new BuildFailedException("Sound is not in the built player: " + name);
            }
        }

        static bool Contains(byte[] all, byte[] key)
        {
            for (int i = 0; i + key.Length <= all.Length; i++)
            {
                int k = 0;
                while (k < key.Length && all[i + k] == key[k]) k++;
                if (k == key.Length) return true;
            }
            return false;
        }

        static string AudioFingerprint()
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var all = new System.Text.StringBuilder();
            var files = new List<string>(AudioFiles()) { "ProjectSettings/AudioManager.asset", Scene };
            foreach (string name in new[] { "Sound", "AudioTest", "App", "Save", "StormView", "StormTest", "World", "Grid", "Game", "Mechanics" }) files.Add("Assets/Scripts/" + name + ".cs");
            foreach (string f in files) all.Append(f).Append(BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(f))));
            return all.ToString();
        }
    }
}
