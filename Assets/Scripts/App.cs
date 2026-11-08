using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Kfitsa
{
    public class RunState { public int score, coins, lives = 3, power, secrets; public bool shield; }

    /// Screens and flow: title, map, pause, scores, settings, credits, and the level itself.
    public partial class App : MonoBehaviour
    {
        enum Screen { Title, Map, Playing, Paused, Menu }

        public const string HeroName = "צוקי ציקי";
        public bool Easy => save != null && save.difficulty == 1;

        public Game game;
        public Hud hud;
        public Save save;
        public RunState run = new RunState();
        Screen screen;
        Camera cam;
        PixelPerfectCamera ppc;
        int mapCursor;
        bool bgVisible;
        readonly List<GameObject> screenObjects = new List<GameObject>();
        readonly List<Text> rows = new List<Text>();
        List<MenuItem> items; int cursor; Action backAction;

        class MenuItem
        {
            public Func<string> label; public Action ok; public Action<int> adjust;
            public Func<float> value; public Action<float> set;       // a slider
            public Color[] chips; public Func<int> chip; public Action<int> pick;   // a row of colours
            public RectTransform fill, knob; public List<Image> marks;
        }


        void Start()
        {
            cam = Camera.main; ppc = cam.GetComponent<PixelPerfectCamera>();
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;
            if (Application.isMobilePlatform) PhoneSetup();
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-touchui") >= 0 || Array.IndexOf(args, "-touchtest") >= 0) InputSystem.AddDevice<Touchscreen>();
            Hud.SafeOverride = ParseRect(Arg(args, "-safearea"));
            if (Array.IndexOf(args, "-feeltest") >= 0) { FeelTest.Run(Arg(args, "-captureDir")); Application.Quit(); return; }

            if (Array.IndexOf(args, "-levelsDir") >= 0) Levels.Folder = Arg(args, "-levelsDir");
            if (Array.IndexOf(args, "-validate") >= 0)
            {
                string one = Array.IndexOf(args, "-level") >= 0 ? Arg(args, "-level") : null;
                if (Array.IndexOf(args, "-seconds") >= 0) Reach.Seconds = double.Parse(Arg(args, "-seconds"));
                bool ok = false;
                try { ok = Reach.ValidateAll(System.IO.Path.Combine(Arg(args, "-captureDir"), "reach_report.txt"), one); }
                catch (Exception e) { Debug.LogError(e); }
                Application.Quit(ok ? 0 : 3); return;
            }

            foreach (string test in new[] { "-playtest", "-selftest", "-inputtest", "-touchtest", "-shots", "-audiotest", "-stormtest" }) Save.Sandbox |= Array.IndexOf(args, test) >= 0;
            save = Save.Load();
            Outfit.Hat = Mathf.Clamp(save.hat, 0, Outfit.Hats.Length - 1); Outfit.Pant = Mathf.Clamp(save.pant, 0, Outfit.Pants.Length - 1);
            Sound.Init(gameObject);
            Sound.MusicVolume = save.music; Sound.SfxVolume = save.sfx;
            if (!save.fullscreen && !Application.isMobilePlatform) UnityEngine.Screen.SetResolution(1280, 720, false);
            Haptics.On = save.haptics;
            hud = new Hud();
            Achievements.save = () => save;
            Achievements.shown = d => { hud.Toast(d.icon, "הישג חדש: " + d.name); Sound.Play("magic"); };
            hud.Touch.Configure(save);
            game = gameObject.AddComponent<Game>();
            game.Init(this);
            mapCursor = Mathf.Min(save.unlocked, Levels.All.Length - 1);

            if (Array.IndexOf(args, "-playtest") >= 0)
            {
                hud.Show(true); screen = Screen.Playing;
                try { PlayTest.Run(this, Arg(args, "-captureDir"), Array.IndexOf(args, "-level") >= 0 ? Arg(args, "-level") : null); } catch (Exception e) { Debug.LogError(e); }
                Application.Quit(); return;
            }
            if (Array.IndexOf(args, "-selftest") >= 0) { try { SelfTest.Run(this, Arg(args, "-captureDir")); } catch (Exception e) { Debug.LogError(e); } Application.Quit(); return; }
            if (Array.IndexOf(args, "-inputtest") >= 0) { gameObject.AddComponent<InputTour>().Begin(this, Arg(args, "-captureDir")); ShowTitle(); return; }
            if (Array.IndexOf(args, "-touchtest") >= 0) { gameObject.AddComponent<TouchTour>().Begin(this, Arg(args, "-captureDir")); ShowTitle(); return; }
            if (Array.IndexOf(args, "-audiotest") >= 0) { ShowTitle(); gameObject.AddComponent<AudioTest>().Begin(this, Arg(args, "-captureDir")); return; }
            if (Array.IndexOf(args, "-stormtest") >= 0) { gameObject.AddComponent<StormTest>().Begin(this, Arg(args, "-captureDir")); return; }
            if (Array.IndexOf(args, "-shots") >= 0) { gameObject.AddComponent<ScreenTour>().Begin(this, Arg(args, "-captureDir")); return; }
            ShowTitle();
        }

        /// 60 fps and nothing the 2D art does not use, to save battery.
        void PhoneSetup()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            QualitySettings.antiAliasing = 0;
            cam.allowHDR = false; cam.allowMSAA = false;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) { urp.supportsHDR = false; urp.msaaSampleCount = 1; urp.renderScale = 1; }
        }

        static Rect? ParseRect(string s)
        {
            var v = s.Split(',');
            if (v.Length != 4) return null;
            return new Rect(float.Parse(v[0]), float.Parse(v[1]), float.Parse(v[2]), float.Parse(v[3]));
        }

        // The phone sends the app to the background: stop the level, keep progress.
        void OnApplicationPause(bool paused) { Away(paused); }
        void OnApplicationFocus(bool focus) { Away(!focus); }

        void Away(bool away)
        {
            if (!Application.isMobilePlatform) return;
            Sound.Background(away);
            if (away) Backgrounded();
        }

        /// Called by name from GameMain.java.
        public void OnSystemBack(string unused) => GameInput.SystemBack();

        public void Backgrounded()
        {
            if (save == null) return;
            save.Write();
            if (screen == Screen.Playing) ShowPause();
        }

        static string Arg(string[] a, string name)
        {
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : ".";
        }

        /// Puts the test straight into a level, with no menus.
        public void TestPlay(int level)
        {
            ClearScreen(); hud.Show(true); screen = Screen.Playing;
            run = new RunState { lives = 99 };
            game.skipIntro = true;
            game.StartLevel(level);
        }

        void Update()
        {
            // The pixel camera works only at whole multiples of 1080; elsewhere keep the same 15-row view
            bool whole = UnityEngine.Screen.height % 1080 == 0;
            if (ppc != null && ppc.enabled != whole) ppc.enabled = whole;
            if (!whole) cam.orthographicSize = 7.5f;

            if (hud == null) return;
            hud.Tick();
            bool playing = screen == Screen.Playing;
            GameInput.Tick();
            hud.Touch.SetVisible(playing && TouchUi.Available && !GameInput.GamepadActive);
            if (Application.isMobilePlatform) UnityEngine.Screen.sleepTimeout = playing ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
            if (screen == Screen.Playing)
            {
                if (GameInput.PausePressed) ShowPause();
                return;
            }
            var m = GameInput.Menu();
            var tap = hud.Touch.Tap;
            if (screen == Screen.Map) { if (!mapHold) MapTick(Time.unscaledDeltaTime); if (tap != null) MapTap(tap.Value); MapInput(m); return; }
            if (items == null) return;
            if (dragging && hud.Touch.Finger is Vector2 held) { Drag(held); return; }
            dragging = false;
            if (tap != null && MenuTap(tap.Value)) return;
            if (m.y != 0) { cursor = (cursor - m.y + items.Count) % items.Count; Sound.Play("select", .5f); PaintMenu(); }
            if (m.x != 0 && items[cursor].adjust != null) { items[cursor].adjust(m.x); PaintMenu(); }
            if (m.ok && items[cursor].ok != null) { Sound.Play("select"); items[cursor].ok(); }
            else if (m.back && backAction != null) { Sound.Play("select"); backAction(); }
        }

        // ---- generic menu ----

        const float TouchRowStep = 100;

        bool MenuTap(Vector2 at)
        {
            float px = hud.Scale;
            for (int i = 0; i < rows.Count; i++)
            {
                Vector2 c = rows[i].rectTransform.position;
                if (items[i].set != null || items[i].pick != null) c.x -= 290 * px;    // these rows sit to one side of the middle
                if (Mathf.Abs(at.y - c.y) > TouchRowStep / 2 * px || Mathf.Abs(at.x - c.x) > 560 * px) continue;
                cursor = i;
                var item = items[i];
                if (item.set != null) { dragging = true; Drag(at); return true; }
                if (item.pick != null)
                {
                    int chip = Mathf.RoundToInt(((at.x - c.x) / px - BarLeft) / ChipStep);
                    if (chip >= 0 && chip < item.chips.Length) { item.pick(chip); PaintMenu(); return true; }
                }
                if (item.ok == null && item.adjust != null) { item.adjust(at.x < c.x ? -1 : 1); PaintMenu(); return true; }
                PaintMenu();
                if (item.ok != null) { Sound.Play("select"); item.ok(); }
                return true;
            }
            return false;
        }

        const float BarLeft = -340, BarRight = 60, ChipStep = 78;
        bool dragging;

        void Drag(Vector2 at)
        {
            var item = items[cursor];
            if (item.set == null) { dragging = false; return; }
            float k = ((at.x - rows[cursor].rectTransform.position.x + 290 * hud.Scale) / hud.Scale - BarLeft) / (BarRight - BarLeft);
            item.set(Mathf.Clamp01(k));
            PaintMenu();
        }

        void ClearScreen()
        {
            hud.Touch.Preview(false); hud.SafeInFront(false); dragging = false;
            foreach (var o in screenObjects) Destroy(o);
            screenObjects.Clear(); rows.Clear(); items = null; backAction = null; cursor = 0;
        }

        void Backdrop(float dim)
        {
            float side = Mathf.Max(1920, hud.Width);
            var b = hud.Picture("bg_hills", new Vector2(.5f, .5f), Vector2.zero, new Vector2(side, side));
            screenObjects.Add(b.gameObject);
            b.transform.SetAsFirstSibling();
            var d = hud.Panel(new Color(.08f, .1f, .18f, dim));
            screenObjects.Add(d.gameObject);
            d.transform.SetSiblingIndex(1);
        }

        Text Line(string text, int size, float y, Color? color = null)
        {
            var t = hud.Label(size, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(1800, size * 1.4f));
            t.text = Rtl.V(text); t.color = color ?? Color.white;
            screenObjects.Add(t.gameObject);
            return t;
        }

        void Menu(string title, List<MenuItem> list, Action back, float top = 120, float dim = .35f, bool withBackdrop = true, float? titleY = null)
        {
            ClearScreen();
            if (withBackdrop) Backdrop(dim); else { var d = hud.Panel(new Color(.05f, .06f, .12f, .7f)); screenObjects.Add(d.gameObject); }
            Line(title, 92, titleY ?? top + 190);
            items = list; backAction = back; if (screen != Screen.Paused) screen = Screen.Menu;
            bool touch = TouchUi.Available;
            for (int i = 0; i < list.Count; i++)
            {
                var row = Line("", touch ? 60 : 54, top - i * (touch ? TouchRowStep : 78));
                rows.Add(row);
                if (list[i].set != null || list[i].pick != null) { row.rectTransform.anchoredPosition += new Vector2(290, 0); row.fontSize = 46; WideRow(list[i], row.rectTransform.anchoredPosition.y); }
            }
            PaintMenu();
        }

        /// Draws the slider or the colour chips beside the label.
        void WideRow(MenuItem m, float y)
        {
            Image Box(Color c, float x, Vector2 size)
            {
                var o = new GameObject("Bar", typeof(Image)); o.transform.SetParent(hud.Canvas, false);
                var i = o.GetComponent<Image>(); i.color = c; i.raycastTarget = false; i.sprite = hud.RoundSprite;
                i.rectTransform.anchorMin = i.rectTransform.anchorMax = new Vector2(.5f, .5f); i.rectTransform.anchoredPosition = new Vector2(x, y); i.rectTransform.sizeDelta = size;
                screenObjects.Add(o);
                return i;
            }
            if (m.set != null)
            {
                float w = BarRight - BarLeft;
                var back = Box(new Color(.12f, .14f, .22f, .95f), (BarLeft + BarRight) / 2, new Vector2(w + 12, 24));
                back.sprite = null;
                var fill = Box(new Color(1, .86f, .3f), BarLeft, new Vector2(w, 12)); fill.sprite = null;
                fill.rectTransform.pivot = new Vector2(0, .5f);
                m.fill = fill.rectTransform;
                m.knob = Box(Color.white, BarLeft, new Vector2(52, 52)).rectTransform;
                return;
            }
            m.marks = new List<Image>();
            for (int i = 0; i < m.chips.Length; i++)
            {
                m.marks.Add(Box(Color.white, BarLeft + i * ChipStep, new Vector2(70, 70)));
                Box(m.chips[i], BarLeft + i * ChipStep, new Vector2(56, 56));
            }
        }

        void PaintMenu()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i == cursor;
                string s = items[i].label();
                rows[i].text = Rtl.V(on ? "< " + s + " >" : s);
                rows[i].color = on ? new Color(1, .86f, .3f) : Color.white;
                var m = items[i];
                if (m.fill != null)
                {
                    float k = m.value(), w = BarRight - BarLeft;
                    m.fill.sizeDelta = new Vector2(w * k, 12);
                    m.knob.anchoredPosition = new Vector2(BarLeft + w * k, m.knob.anchoredPosition.y);
                }
                if (m.marks != null)
                    for (int c = 0; c < m.marks.Count; c++) m.marks[c].color = c == m.chip() ? Color.white : new Color(.1f, .12f, .2f, .9f);
            }
        }

        static MenuItem Item(string label, Action ok) => new MenuItem { label = () => label, ok = ok };

        // ---- screens ----

        public void ShowTitle()
        {
            hud.Show(false); game.Unload();
            Sound.Music("m_title");
            screen = Screen.Title;
            var list = new List<MenuItem>
            {
                save.unlocked > 0 ? Item("המשך משחק", () => { NewRun(); ShowMap(); }) : Item("משחק חדש", () => ShowDifficulty(false)),
                Item("ארון הבגדים", () => ShowWardrobe(ShowTitle)),
                Item("הישגים", () => ShowAchievements(ShowTitle)),
                Item("שיאים", () => ShowScores(ShowTitle)),
                Item("הגדרות", () => ShowSettings(ShowTitle)),
                Item("קרדיטים", ShowCredits),
                Item("יציאה", Application.Quit),
            };
            if (save.unlocked > 0) list.Insert(1, Item("משחק חדש", () => ShowDifficulty(true)));
            Menu("קפיצה גדולה", list, null, 90, .15f);
            HeroPicture('b', "cheer1", new Vector2(-640, -60), new Vector2(300, 370));
            var name = hud.Label(60, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), new Vector2(-640, -285), new Vector2(520, 90));
            name.text = Rtl.V(HeroName); name.color = new Color(1, .86f, .3f);
            screenObjects.Add(name.gameObject);
        }

        void NewRun() => run = new RunState { lives = Easy ? 6 : 3 };

        /// Starting over: normal comes first, so a press straight through keeps the default.
        public void ShowDifficulty(bool wipe)
        {
            void Start(int level)
            {
                save.difficulty = level;
                if (wipe) { save.unlocked = 0; mapCursor = 0; }
                save.Write(); NewRun(); ShowIntro();
            }
            Menu("בחרו קושי", new List<MenuItem> { Item("רגיל", () => Start(0)), Item("קל", () => Start(1)), Item("חזרה", ShowTitle) }, ShowTitle, 140, .35f, true, 360);
            var help = new Color(.85f, .9f, 1);
            Line("רגיל: 3 חיים והאתגר המלא", 40, -220, help);
            Line("קל: 6 חיים, פחות אויבים ואיטיים יותר, ועוד דגלי ביקורת", 40, -290, help);
            Line("אפשר לשנות בכל רגע בהגדרות", 34, -360, help);
        }

        public void ShowIntro()
        {
            Menu(HeroName, new List<MenuItem> { Item("יוצאים לדרך", ShowMap) }, ShowMap, -300, .35f, true, 400);
            HeroPicture('b', "cheer1", new Vector2(0, 120), new Vector2(260, 320));
            Line("הכירו את " + HeroName + "!", 52, -100);
            Line("קפיצה אחר קפיצה, עד ענק הכפור", 40, -180, new Color(.85f, .9f, 1));
        }

        public void ShowScores(Action back)
        {
            var list = new List<MenuItem> { Item("חזרה", back) };
            Menu("שיאים", list, back, -440, .35f, true, 430);
            if (save.scores.Count == 0) Line("עוד אין שיאים. קדימה!", 48, 80);
            for (int i = 0; i < save.scores.Count; i++)
            {
                var r = save.scores[i];
                Line($"{i + 1}.  {r.score:000000}  -  {r.reached}  -  {r.date}", 40, 300 - i * 58);
            }
        }

        public void ShowSettings(Action back)
        {
            bool overGame = screen == Screen.Paused;
            var list = new List<MenuItem>
            {
                new MenuItem { label = () => "מוזיקה  " + Mathf.RoundToInt(save.music * 10), adjust = d => { save.music = Mathf.Clamp01(save.music + d * .1f); Sound.MusicVolume = save.music; Sound.ApplyVolume(); } },
                new MenuItem { label = () => "אפקטים  " + Mathf.RoundToInt(save.sfx * 10), adjust = d => { save.sfx = Mathf.Clamp01(save.sfx + d * .1f); Sound.SfxVolume = save.sfx; Sound.Play("coin"); } },
                new MenuItem { label = () => "קושי  " + (Easy ? "קל" : "רגיל"), ok = FlipDifficulty, adjust = d => FlipDifficulty() },
                new MenuItem { label = () => "מסך מלא  " + (save.fullscreen ? "כן" : "לא"), ok = ToggleFull, adjust = d => ToggleFull() },
                Item("חזרה", () => { save.Write(); back(); }),
            };
            if (Application.isMobilePlatform) list.RemoveAt(3);
            if (TouchUi.Available) list.Insert(list.Count - 1, Item("שליטה", () => { save.Write(); ShowControls(() => ShowSettings(back)); }));
            Menu("הגדרות", list, () => { save.Write(); back(); }, TouchUi.Available ? 210 : 120, .35f, !overGame);
            var help = new Color(.85f, .9f, 1);
            if (TouchUi.Available)
            {
                Line("הקשה בצד ימין של השורה מגבירה, בצד שמאל מחלישה", 34, -250, help);
                Line("משמאל זזים    מימין קופצים, רצים וזורקים", 34, -310, help);
                Line("דלת סודית: כפתור החץ שמופיע ליד הדלת", 34, -370, help);
                return;
            }
            Line("חצים ימינה ושמאלה משנים ערך", 34, -320, help);
            Line("זזים: חצים או WASD    קופצים: Space    רצים וזורקים: Shift", 34, -380, help);
            Line("דלת סודית: חץ למטה    כוכבים: שלושה בכל שלב, אחד בחדר הסודי", 34, -430, help);
        }

        void FlipDifficulty() { save.difficulty = 1 - save.difficulty; save.Write(); }

        static readonly string[] RunModes = { "החזק לריצה", "ריצה אוטומטית", "נגיעה לריצה" };
        static readonly string[] RunHelp =
        {
            "ריצה רק בזמן שמחזיקים את הכפתור", "הגיבור תמיד רץ, ואין כפתור ריצה", "נגיעה נועלת את הריצה וגורמת לכפתור להאיר, נגיעה נוספת משחררת",
        };

        /// Touch settings. Every change shows at once on the buttons around the menu.
        public void ShowControls(Action back)
        {
            bool overGame = screen == Screen.Paused;
            void Live() { hud.Touch.Configure(save); helpLine.text = Rtl.V(RunHelp[save.runMode]); }
            void Cycle(int d) { save.runMode = (save.runMode + d + 3) % 3; Live(); }
            var colors = new Color[TouchUi.Themes.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = TouchUi.Themes[i].color;
            var list = new List<MenuItem>
            {
                new MenuItem { label = () => "שקיפות  " + Mathf.RoundToInt(save.buttonAlpha * 100) + "%", value = () => Mathf.InverseLerp(.25f, 1, save.buttonAlpha),
                    set = k => { save.buttonAlpha = Mathf.Lerp(.25f, 1, k); Live(); }, adjust = d => { save.buttonAlpha = Mathf.Clamp(save.buttonAlpha + d * .05f, .25f, 1); Live(); } },
                new MenuItem { label = () => "גודל  " + Mathf.RoundToInt(save.buttonSize * 100) + "%", value = () => Mathf.InverseLerp(.7f, 1.4f, save.buttonSize),
                    set = k => { save.buttonSize = Mathf.Lerp(.7f, 1.4f, k); Live(); }, adjust = d => { save.buttonSize = Mathf.Clamp(save.buttonSize + d * .05f, .7f, 1.4f); Live(); } },
                new MenuItem { label = () => "צבע  " + TouchUi.Themes[save.buttonColor].name, chips = colors, chip = () => save.buttonColor,
                    pick = c => { save.buttonColor = c; Live(); }, adjust = d => { save.buttonColor = (save.buttonColor + d + colors.Length) % colors.Length; Live(); } },
                new MenuItem { label = () => "מצב יד שמאל  " + (save.leftHanded ? "כן" : "לא"), ok = () => { save.leftHanded = !save.leftHanded; Live(); }, adjust = d => { save.leftHanded = !save.leftHanded; Live(); } },
                new MenuItem { label = () => "ריצה: " + RunModes[save.runMode], ok = () => Cycle(1), adjust = Cycle },
                new MenuItem { label = () => "רטט  " + (save.haptics ? "כן" : "לא"), ok = () => { save.haptics = Haptics.On = !save.haptics; Haptics.Buzz(60, .7f); }, adjust = d => { save.haptics = Haptics.On = !save.haptics; Haptics.Buzz(60, .7f); } },
                Item("חזרה", () => { save.Write(); back(); }),
            };
            Menu("שליטה", list, () => { save.Write(); back(); }, 240, .45f, !overGame, 400);
            helpLine = Line(RunHelp[save.runMode], 34, -470, new Color(.85f, .9f, 1));
            hud.SafeInFront(true);
            hud.Touch.Preview(true);
        }

        Text helpLine;

        void ToggleFull()
        {
            save.fullscreen = !save.fullscreen;
            UnityEngine.Screen.fullScreenMode = save.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (!save.fullscreen) UnityEngine.Screen.SetResolution(1280, 720, false);
            else UnityEngine.Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, true);
        }

        void ShowCredits()
        {
            Menu("קרדיטים", new List<MenuItem> { Item("חזרה", ShowTitle) }, ShowTitle, -420, .35f, true, 410);
            string[] text =
            {
                "גיבור המשחק: " + HeroName,
                "גרפיקה וצלילים: Kenney (www.kenney.nl) - CC0",
                "New Platformer Pack, Platformer Characters",
                "Impact, Digital Audio, Interface, Music Jingles",
                "מוזיקה: Juhani Junkala - CC0",
                "גופן: Rubik - SIL OFL",
                "נבנה ב-Unity",
            };
            for (int i = 0; i < text.Length; i++) Line(text[i], 44, 260 - i * 76);
        }

        public void ShowPause()
        {
            screen = Screen.Paused; game.paused = true; Sound.Duck(true); Sound.Play("select");
            hud.Banner(""); hud.Hint(0, "");
            var list = new List<MenuItem>
            {
                Item("המשך", Resume),
                Item("התחל את השלב מחדש", () => { ClearScreen(); screen = Screen.Playing; run.power = 0; Sound.Duck(false); game.StartLevel(game.levelIndex); }),
                Item("הגדרות", () => ShowSettings(ShowPause)),
                Item("חזרה למפה", () => { ClearScreen(); game.Unload(); Sound.Duck(false); ShowMap(); }),
            };
            Menu("הפסקה", list, Resume, 120, .6f, false);
        }

        void Resume() { ClearScreen(); screen = Screen.Playing; game.paused = false; Sound.Duck(false); }

        public string ScreenName => screen.ToString();
        public int MapCursor => mapCursor;
        public void ShowCreditsForTour() => ShowCredits();
        public void ResumeForTour() => Resume();
        public void ShowEndingForTour() { run.score = 45200; run.secrets = 5; ShowEnding(); }
        public void Enter(int level) { ClearScreen(); hud.Show(true); screen = Screen.Playing; starsBefore = StarCount; game.trial = false; game.StartLevel(level); }

        // ---- time trial (v2.3) ----

        RunState runBeforeTrial;
        public bool TrialOpen(int level) => level >= 0 && level < Levels.All.Length && !Levels.All[level].boss && save.best[level] > 0;

        /// A run of a finished level against the clock, with the game's own run kept aside until it ends.
        public void EnterTrial(int level)
        {
            if (!TrialOpen(level)) return;
            ClearScreen(); hud.Show(true); screen = Screen.Playing;
            if (runBeforeTrial == null) runBeforeTrial = run;
            run = new RunState { lives = 1 };
            game.trial = true; game.StartLevel(level);
        }

        public void TrialDone() { save.Write(); game.Unload(); hud.Banner(""); mapFrom = -1; ShowMap(); }

        /// Back from a time trial in any way: the run as it was before.
        void EndTrial()
        {
            if (runBeforeTrial != null) { run = runBeforeTrial; runBeforeTrial = null; }
            game.trial = false;
        }

        // ---- outfits (v2.1) ----

        int starsBefore = -1;
        string newOutfit;      // shown on the map after a level that opened an item
        public string OutfitNote;   // the same, kept for the self test
        public void WearForTest(bool hat, int d) => WearNext(hat, d);
        public int StarCount { get { int n = 0; foreach (int b in save.stars) for (int k = b; k != 0; k >>= 1) n += k & 1; return n; } }

        /// The hero as a picture on a screen, wearing the chosen pants and hat. Returns the hero and its hat.
        public (Image hero, Image hat) HeroPicture(char form, string pose, Vector2 pos, Vector2 size)
        {
            var h = hud.Picture(Outfit.HeroSprite(form, pose), new Vector2(.5f, .5f), pos, size);
            var o = new GameObject("Hat", typeof(Image)); o.transform.SetParent(h.transform, false);
            var hat = o.GetComponent<Image>(); hat.raycastTarget = false;
            Outfit.Place(hat, h, form, pose);
            screenObjects.Add(h.gameObject);
            return (h, hat);
        }

        void WearNext(bool hat, int d)
        {
            int n = hat ? Outfit.Hats.Length : Outfit.Pants.Length, at = hat ? Outfit.Hat : Outfit.Pant, stars = StarCount;
            for (int i = 1; i <= n; i++)
            {
                int k = ((at + d * i) % n + n) % n;
                if (!Outfit.Open(hat, k, stars)) continue;
                if (hat) Outfit.Hat = save.hat = k; else Outfit.Pant = save.pant = k;
                if (hat && k > 0) Achievements.Unlock(Achievements.Id.Hat);
                break;
            }
            Sound.Play("select");
        }

        /// Achievements that depend on the whole save: every star, every secret of a world.
        void CheckProgressAchievements()
        {
            int stars = 0, all = 0;
            var worldDone = new Dictionary<int, bool>();
            for (int i = 0; i < Levels.All.Length; i++)
            {
                for (int b = save.stars[i]; b != 0; b >>= 1) stars += b & 1;
                all += 3;
                int got = 0; for (int b = save.found[i]; b != 0; b >>= 1) got += b & 1;
                int w = Levels.All[i].world;
                worldDone[w] = (!worldDone.ContainsKey(w) || worldDone[w]) && got >= SecretTotal(i);
            }
            if (stars >= all) Achievements.Unlock(Achievements.Id.AllStars);
            if (worldDone.TryGetValue(1, out bool w1) && w1) Achievements.Unlock(Achievements.Id.Secrets1);
            if (worldDone.TryGetValue(2, out bool w2) && w2) Achievements.Unlock(Achievements.Id.Secrets2);
            if (worldDone.TryGetValue(3, out bool w3) && w3) Achievements.Unlock(Achievements.Id.Secrets3);
        }
        public void CheckProgressForTest() => CheckProgressAchievements();

        /// Every achievement with its icon and goal; the closed ones dark.
        public void ShowAchievements(Action back)
        {
            Menu("הישגים", new List<MenuItem> { Item("חזרה", back) }, back, -455, .35f, true, 440);
            Line($"נפתחו {Achievements.Count} מתוך {Achievements.All.Length}", 38, 355, new Color(1, .9f, .4f));
            for (int i = 0; i < Achievements.All.Length; i++)
            {
                var d = Achievements.All[i]; bool has = Achievements.Has((Achievements.Id)i);
                int col = i % 4, row = i / 4;
                float x = 690 - col * 460, y = 245 - row * 160;       // right to left
                var icon = hud.Picture(d.icon, new Vector2(.5f, .5f), new Vector2(x + 150, y), new Vector2(86, 86));
                icon.color = has ? Color.white : new Color(.2f, .22f, .3f, .85f);
                screenObjects.Add(icon.gameObject);
                var name = hud.Label(32, TextAnchor.MiddleRight, new Vector2(.5f, .5f), new Vector2(x + 95, y + 20), new Vector2(340, 44), null, new Vector2(1, .5f));
                name.text = Rtl.V(d.name); name.color = has ? new Color(1, .9f, .4f) : new Color(.75f, .78f, .85f);
                screenObjects.Add(name.gameObject);
                var goal = hud.Label(24, TextAnchor.MiddleRight, new Vector2(.5f, .5f), new Vector2(x + 95, y - 22), new Vector2(340, 40), null, new Vector2(1, .5f));
                goal.text = Rtl.V(d.goal); goal.color = new Color(.85f, .9f, 1, has ? 1 : .7f);
                screenObjects.Add(goal.gameObject);
            }
        }

        /// The wardrobe: a hat and pants, each chosen among the open ones; every 3 stars opens one more.
        public void ShowWardrobe(Action back)
        {
            (Image hero, Image hat) look = default;
            void Refresh()
            {
                look.hero.sprite = Art.Center(Outfit.HeroSprite('b', "stand"));
                Outfit.Place(look.hat, look.hero, 'b', "stand");
            }
            var list = new List<MenuItem>
            {
                new MenuItem { label = () => "כובע:  " + Outfit.HatNames[Outfit.Hat], ok = () => { WearNext(true, 1); Refresh(); }, adjust = d => { WearNext(true, d); Refresh(); } },
                new MenuItem { label = () => "בגדים:  " + Outfit.PantsNames[Outfit.Pant], ok = () => { WearNext(false, 1); Refresh(); }, adjust = d => { WearNext(false, d); Refresh(); } },
                Item("חזרה", () => { save.Write(); back(); }),
            };
            Menu("ארון הבגדים", list, () => { save.Write(); back(); }, 160, .35f);
            look = HeroPicture('b', "stand", new Vector2(-620, 0), new Vector2(300, 380));
            int stars = StarCount, open = Outfit.OpenCount(stars), next = Outfit.NextAt(stars);
            Line($"כוכבים: {stars}    נפתחו {open} מתוך {Outfit.ItemCount}", 40, -150, new Color(1, .9f, .4f));
            Line(next < 0 ? "כל הבגדים פתוחים!" : $"הבא בתור: {Outfit.ItemName(open + 1)}, ב־{next} כוכבים", 36, -205, new Color(.85f, .9f, 1));
            // Every item in a row, the closed ones dark with a lock
            for (int n = 1; n <= Outfit.ItemCount; n++)
            {
                bool isHat = n % 2 == 1; int k = isHat ? (n + 1) / 2 : n / 2;
                var at = new Vector2(-495 + (n - 1) * 110, -320);
                var pic = hud.Picture(isHat ? "hat_" + Outfit.Hats[k] : "pants_" + Outfit.Pants[k], new Vector2(.5f, .5f), at, new Vector2(84, 84));
                screenObjects.Add(pic.gameObject);
                if (Outfit.Open(isHat, k, stars)) continue;
                pic.color = new Color(.2f, .22f, .3f, .8f);
                var l = hud.Picture("lock_yellow", new Vector2(.5f, .5f), at, new Vector2(44, 44)); screenObjects.Add(l.gameObject);
            }
        }

        // ---- flow from the level ----

        public void LevelDone()
        {
            int i = game.levelIndex;
            if (i < 0) return;
            save.best[i] = Mathf.Max(save.best[i], run.score);
            save.unlocked = Mathf.Max(save.unlocked, Mathf.Min(i + 1, Levels.All.Length - 1));
            save.Write();
            hud.Banner("");
            game.Unload();
            if (i == Levels.All.Length - 1) { ShowEnding(); return; }
            mapCursor = Mathf.Min(i + 1, Levels.All.Length - 1);
            mapFrom = i;      // the map opens on the level just finished, and the hero walks on to the next
            if (starsBefore >= 0 && Outfit.OpenCount(StarCount) > Outfit.OpenCount(starsBefore))
                OutfitNote = newOutfit = "חדש בארון הבגדים: " + Outfit.ItemName(Outfit.OpenCount(StarCount)) + "!";
            starsBefore = -1;
            CheckProgressAchievements();
            if (Levels.All[i].boss) { WorldCleared(i); return; }
            ShowMap();
        }

        /// A boss that is not the last level: a moment of victory, then on along the map to the next world.
        void WorldCleared(int boss)
        {
            hud.Show(false);
            Sound.Music("m_ending");
            Menu(Levels.All[boss].name + " הובס!", new List<MenuItem> { Item("ממשיכים במסע", ShowMap) }, ShowMap, -220, .25f, true, 300);
            Line("הדרך לעולם " + (Levels.All[boss].world + 1) + " נפתחה", 52, 120);
            Line($"ניקוד: {run.score}", 46, 30);
        }
        public void WorldClearedForTour() => WorldCleared(6);

        void ShowEnding()
        {
            hud.Show(false);
            Sound.Music("m_ending");
            int rank = save.AddScore(run.score, "המשחק הושלם");
            Menu("ניצחת!", new List<MenuItem> { Item("לשיאים", () => ShowScores(ShowTitle)) }, () => ShowScores(ShowTitle), -220, .25f, true, 300);
            Line(Levels.All[Levels.All.Length - 1].boss ? Levels.All[Levels.All.Length - 1].name + " הובס והדרך הביתה פתוחה" : "סיימת את כל השלבים שיש כרגע! עוד שלבים בדרך", 52, 120);
            Line($"ניקוד סופי: {run.score}    סודות שנמצאו: {run.secrets}", 46, 30);
            if (rank >= 0) Line($"מקום {rank + 1} בטבלת השיאים!", 46, -60, new Color(1, .86f, .3f));
            NewRun();
        }

        public void GameOver()
        {
            game.Unload(); hud.Show(false); hud.Fade(0);
            Sound.Music(null); Sound.Play("gameover");
            int rank = save.AddScore(run.score, "עד שלב " + Levels.All[mapCursor].Code);
            Menu("המשחק נגמר", new List<MenuItem> { Item("חזרה למפה", () => { NewRun(); ShowMap(); }) }, () => { NewRun(); ShowMap(); }, -200, .6f, true, 240);
            Line($"ניקוד: {run.score}", 56, 60);
            if (rank >= 0) Line($"מקום {rank + 1} בטבלת השיאים!", 46, -40, new Color(1, .86f, .3f));
        }
    }

    /// The canvas: in-level stats plus the building blocks every screen uses.
    public class Hud
    {
        readonly Transform canvas;
        readonly GameObject stats;
        readonly Text scoreText, coinText, lifeText, bannerText, bossName, hintText, timerText, clockText;
        readonly Image[] starIcons = new Image[3], powerIcons = new Image[4];
        Image shieldIcon;
        float hintT;
        readonly Image fade, bossFill; readonly GameObject bossBar;
        readonly Font font;
        readonly CanvasScaler scaler;
        readonly RectTransform safe;
        Rect appliedSafe; Vector2Int appliedSize;

        /// For tests on a PC: pretend the phone reports this safe area, in pixels.
        public static Rect? SafeOverride;
        public TouchUi Touch { get; }
        public Transform Canvas => canvas;
        public Sprite RoundSprite { get; }
        /// Screen pixels per canvas unit.
        public float Scale => UnityEngine.Screen.width * 9 >= UnityEngine.Screen.height * 16 ? UnityEngine.Screen.height / 1080f : UnityEngine.Screen.width / 1920f;
        public float Width => UnityEngine.Screen.width / Scale;

        public Hud()
        {
            var o = new GameObject("Hud", typeof(Canvas), typeof(CanvasScaler));
            canvas = o.transform;
            o.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            scaler = o.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            font = Resources.Load<Font>("Fonts/Rubik");

            // HUD and touch buttons stay clear of the notch; menus are centered and backgrounds run edge to edge
            var so = new GameObject("Safe", typeof(RectTransform));
            so.transform.SetParent(canvas, false);
            safe = (RectTransform)so.transform;
            FitScreen();

            stats = new GameObject("Stats", typeof(RectTransform));
            stats.transform.SetParent(safe, false);
            var sr = (RectTransform)stats.transform; sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = sr.offsetMax = Vector2.zero;
            // Right to left: score, coins, lives
            scoreText = Label(46, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-40, -24), new Vector2(520, 70), stats.transform, new Vector2(1, 1));
            Picture("hud_coin", new Vector2(1, 1), new Vector2(-700, -54), new Vector2(60, 60), stats.transform);
            coinText = Label(46, TextAnchor.UpperLeft, new Vector2(1, 1), new Vector2(-540, -24), new Vector2(120, 70), stats.transform, new Vector2(1, 1));
            Picture("hud_heart", new Vector2(1, 1), new Vector2(-970, -54), new Vector2(60, 60), stats.transform);
            lifeText = Label(46, TextAnchor.UpperLeft, new Vector2(1, 1), new Vector2(-810, -24), new Vector2(120, 70), stats.transform, new Vector2(1, 1));

            for (int i = 0; i < 3; i++) starIcons[i] = Picture("star", new Vector2(1, 1), new Vector2(-1050 - i * 62, -54), new Vector2(56, 56), stats.transform);
            string[] forms = { "hero_s_stand", "hero_b_stand", "sevivon", "sevivon_frost" };
            for (int i = 0; i < 4; i++) powerIcons[i] = Picture(forms[i], new Vector2(0, 1), new Vector2(170 + i * 74, -58), new Vector2(64, 64), stats.transform);
            shieldIcon = Picture("bubble", new Vector2(0, 1), new Vector2(170 + 4 * 74 + 16, -58), new Vector2(60, 60), stats.transform);
            shieldIcon.enabled = false;
            timerText = Label(70, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(0, -130), new Vector2(400, 90), stats.transform);
            clockText = Label(36, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-40, -96), new Vector2(400, 50), stats.transform, new Vector2(1, 1));

            hintText = Label(34, TextAnchor.LowerCenter, new Vector2(.5f, 0), new Vector2(0, 36), new Vector2(1800, 60), stats.transform, new Vector2(.5f, 0));

            bossBar = new GameObject("BossBar", typeof(RectTransform));
            bossBar.transform.SetParent(stats.transform, false);
            var bb = (RectTransform)bossBar.transform; bb.anchorMin = bb.anchorMax = new Vector2(.5f, 1); bb.anchoredPosition = new Vector2(0, -160); bb.sizeDelta = new Vector2(800, 36);
            var back = new GameObject("Back", typeof(Image)); back.transform.SetParent(bossBar.transform, false);
            back.GetComponent<Image>().color = new Color(.13f, .15f, .22f, .9f);
            Stretch(back.GetComponent<Image>().rectTransform);
            var fill = new GameObject("Fill", typeof(Image)); fill.transform.SetParent(bossBar.transform, false);
            bossFill = fill.GetComponent<Image>(); bossFill.color = new Color(.95f, .35f, .2f);
            Stretch(bossFill.rectTransform); bossFill.rectTransform.pivot = new Vector2(1, .5f);
            bossName = Label(34, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), new Vector2(0, 44), new Vector2(800, 50), bossBar.transform, new Vector2(.5f, .5f));

            bannerText = Label(76, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), new Vector2(0, 80), new Vector2(1700, 400), canvas, new Vector2(.5f, .5f));
            // The achievement toast: a dark strip at the top with the icon, over the game and the menus alike
            toast = new GameObject("Toast", typeof(Image));
            toast.transform.SetParent(canvas, false);
            var tr = (RectTransform)toast.transform; tr.anchorMin = tr.anchorMax = new Vector2(.5f, 1); tr.pivot = new Vector2(.5f, 1);
            tr.sizeDelta = new Vector2(760, 120); tr.anchoredPosition = new Vector2(0, -210);
            var tbg = toast.GetComponent<Image>(); tbg.color = new Color(.08f, .1f, .18f, .92f); tbg.raycastTarget = false;
            toastIcon = Picture("star", new Vector2(1, .5f), new Vector2(-70, 0), new Vector2(84, 84), toast.transform);
            toastText = Label(36, TextAnchor.MiddleRight, new Vector2(1, .5f), new Vector2(-130, 0), new Vector2(600, 110), toast.transform, new Vector2(1, .5f));
            toast.SetActive(false);
            fade = Panel(Color.clear);
            fade.gameObject.name = "Fade";
            BossBar(-1);
            Touch = new TouchUi(this, safe, () => Scale);
            RoundSprite = TouchUi.RoundDisc;
        }

        /// The canvas is always at least 1920x1080: wider phones gain width and every layout keeps its height.
        void FitScreen()
        {
            var size = new Vector2Int(UnityEngine.Screen.width, UnityEngine.Screen.height);
            var area = SafeOverride ?? UnityEngine.Screen.safeArea;
            if (size == appliedSize && area == appliedSafe) return;
            appliedSize = size; appliedSafe = area;
            scaler.matchWidthOrHeight = size.x * 9 >= size.y * 16 ? 1 : 0;
            safe.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
            safe.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }

        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

        public Image Panel(Color c)
        {
            var o = new GameObject("Panel", typeof(Image));
            o.transform.SetParent(canvas, false);
            var i = o.GetComponent<Image>(); i.color = c; i.raycastTarget = false;
            Stretch(i.rectTransform);
            return i;
        }

        public Text Label(int size, TextAnchor a, Vector2 anchor, Vector2 pos, Vector2 box, Transform parent = null, Vector2? pivot = null)
        {
            var o = new GameObject("Label", typeof(Text), typeof(Outline));
            o.transform.SetParent(parent != null ? parent : canvas, false);
            var t = o.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = a; t.color = Color.white; t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var s = o.GetComponent<Outline>(); s.effectColor = new Color(.14f, .16f, .24f, 1); s.effectDistance = new Vector2(3, -3);
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = anchor; r.pivot = pivot ?? new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = box;
            return t;
        }

        public Image Picture(string art, Vector2 anchor, Vector2 pos, Vector2 size, Transform parent = null)
        {
            var o = new GameObject("Picture", typeof(Image));
            o.transform.SetParent(parent != null ? parent : canvas, false);
            var i = o.GetComponent<Image>(); i.sprite = Art.Center(art); i.raycastTarget = false; i.preserveAspect = true;
            var r = i.rectTransform; r.anchorMin = r.anchorMax = anchor; r.anchoredPosition = pos; r.sizeDelta = size;
            return i;
        }

        public void Stars(int bits, int count)
        {
            for (int i = 0; i < 3; i++) starIcons[i].color = (bits >> i & 1) != 0 ? Color.white : new Color(.12f, .14f, .22f, .55f);
        }

        /// The steps of the power ladder, with the current one lit.
        public void Power(int level)
        {
            for (int i = 0; i < powerIcons.Length; i++)
            {
                powerIcons[i].color = i == level ? Color.white : new Color(.2f, .22f, .3f, .55f);
                powerIcons[i].rectTransform.localScale = Vector3.one * (i == level ? 1.15f : .85f);
            }
        }

        public void Shield(bool on) { if (shieldIcon.enabled != on) shieldIcon.enabled = on; }
        public bool ShieldShown => shieldIcon.enabled;

        public void Timer(float seconds) => timerText.text = seconds < 0 ? "" : Mathf.CeilToInt(seconds).ToString();

        public void SafeInFront(bool on) { if (on) safe.SetAsLastSibling(); else safe.SetAsFirstSibling(); }

        public void Show(bool on) { stats.SetActive(on); if (!on) { Banner(""); Fade(0); Hint(0, ""); } }
        public void Hint(float seconds, string text) { hintT = seconds; hintText.text = seconds > 0 ? text : ""; }
        public void Tick()
        {
            FitScreen(); if (hintT > 0 && (hintT -= Time.unscaledDeltaTime) <= 0) hintText.text = "";
            if (toastT > 0 && (toastT -= Time.unscaledDeltaTime) <= 0) toast.SetActive(false);
        }

        GameObject toast; Image toastIcon; Text toastText; float toastT;
        public bool ToastShown => toast.activeSelf;
        public string ToastText => toastText.text;
        /// Shows a newly opened achievement for a few seconds.
        public void Toast(string icon, string text)
        {
            toast.SetActive(true); toast.transform.SetAsLastSibling(); toastT = 3.5f;
            toastIcon.sprite = Art.Center(icon); toastText.text = Rtl.V(text);
        }

        /// The level clock under the score; a negative time hides it.
        public void Clock(float seconds) => clockText.text = seconds < 0 ? "" : Rtl.V("זמן " + TimeText(seconds));
        public static string TimeText(float s) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:{1:00.0}", (int)(s / 60), s % 60);
        public void Banner(string s) { bannerText.text = s; bannerText.transform.SetAsLastSibling(); }
        public void Fade(float a) { fade.color = new Color(0, 0, 0, a); fade.transform.SetAsLastSibling(); }

        public void Stats(int score, int coins, int lives)
        {
            scoreText.text = Rtl.V("ניקוד " + score.ToString("000000"));
            coinText.text = "x" + coins.ToString("00");
            lifeText.text = "x" + lives;
        }

        GameObject stormBox; Image stormDisc, stormRing, stormIcon, stormArrow; Text stormText; readonly Image[] stormPips = new Image[3];
        public Weather StormShown { get; private set; }
        public float StormArrowSide { get; private set; }

        /// The storm panel, under the top row. left: 1 to 0 through the phase. side: the wind's push here, sign is its direction.
        public void StormShow(Weather w, float left, float side, float strength, bool safe)
        {
            StormShown = w; StormArrowSide = w == Weather.Storm ? side : 0;
            if (stormBox == null)
            {
                if (w == Weather.Calm) return;
                stormBox = new GameObject("Storm", typeof(RectTransform));
                stormBox.transform.SetParent(stats.transform, false);
                var b = (RectTransform)stormBox.transform; b.anchorMin = b.anchorMax = new Vector2(.5f, 1); b.anchoredPosition = new Vector2(0, -170); b.sizeDelta = new Vector2(10, 10);
                var mid = new Vector2(.5f, .5f);
                stormDisc = Picture("storm_disc", mid, Vector2.zero, new Vector2(190, 190), stormBox.transform);
                stormRing = Picture("storm_ring", mid, Vector2.zero, new Vector2(214, 214), stormBox.transform);
                stormRing.type = Image.Type.Filled; stormRing.fillMethod = Image.FillMethod.Radial360; stormRing.fillOrigin = 2; stormRing.fillClockwise = false;
                stormIcon = Picture("storm_icon", mid, Vector2.zero, new Vector2(112, 112), stormBox.transform);
                stormArrow = Picture("storm_arrow", mid, Vector2.zero, new Vector2(320, 170), stormBox.transform);
                for (int i = 0; i < 3; i++) stormPips[i] = Picture("px", mid, new Vector2(-36 + i * 36, -72), new Vector2(28, 14), stormBox.transform);
                stormText = Label(44, TextAnchor.MiddleCenter, mid, new Vector2(0, -136), new Vector2(600, 60), stormBox.transform);
            }
            stormBox.SetActive(w != Weather.Calm);
            if (w == Weather.Calm) return;

            bool storm = w == Weather.Storm, shield = storm && safe;
            var hot = storm ? new Color(1, .3f, .12f) : new Color(1, .68f, .2f);
            var good = new Color(.35f, .95f, .55f);
            stormRing.fillAmount = Mathf.Clamp01(left);
            stormRing.color = shield ? good : hot;
            stormIcon.gameObject.SetActive(!storm || shield);
            stormIcon.sprite = Art.Center(shield ? "shelter" : "storm_icon");
            stormIcon.color = shield ? Color.white : hot;
            stormArrow.gameObject.SetActive(storm && !shield);
            float size = .62f + strength * .4f;
            stormArrow.rectTransform.localScale = new Vector3(side < 0 ? -size : size, size, 1);
            stormArrow.color = new Color(1, .86f, .3f);
            for (int i = 0; i < 3; i++)
            {
                stormPips[i].gameObject.SetActive(storm && !shield);
                stormPips[i].color = strength * 3 >= i + .5f ? new Color(1, .3f, .12f) : new Color(.2f, .18f, .2f, .8f);
            }
            stormText.text = Rtl.V(shield ? "במחסה" : storm ? "סופה!" : "סופה מתקרבת");
            stormText.color = shield ? good : new Color(1, .93f, .5f);
        }

        /// Fraction below zero hides the bar.
        public void BossBar(float fraction, string name = "")
        {
            bossBar.SetActive(fraction >= 0);
            if (fraction < 0) return;
            bossFill.rectTransform.anchorMin = new Vector2(1 - fraction, 0);
            bossName.text = name;
        }
    }
}
