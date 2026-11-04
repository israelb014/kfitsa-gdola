using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Kfitsa
{
    public class RunState { public int score, coins, lives = 3, power, secrets; }

    /// Screens and flow: title, map, pause, scores, settings, credits, and the level itself.
    public class App : MonoBehaviour
    {
        enum Screen { Title, Map, Playing, Paused, Menu }

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
        Bot bot;

        class MenuItem { public Func<string> label; public Action ok; public Action<int> adjust; }

        void Start()
        {
            cam = Camera.main; ppc = cam.GetComponent<PixelPerfectCamera>();
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-feeltest") >= 0) { FeelTest.Run(Arg(args, "-captureDir")); Application.Quit(); return; }

            if (Array.IndexOf(args, "-solve") >= 0) { Solver.Run(Arg(args, "-captureDir")); Application.Quit(); return; }

            save = Save.Load();
            Sound.Init(gameObject);
            Sound.MusicVolume = save.music; Sound.SfxVolume = save.sfx;
            if (!save.fullscreen) UnityEngine.Screen.SetResolution(1280, 720, false);
            hud = new Hud();
            game = gameObject.AddComponent<Game>();
            game.Init(this);
            mapCursor = Mathf.Min(save.unlocked, Levels.All.Length - 1);

            if (Array.IndexOf(args, "-bot") >= 0)
            {
                bot = new Bot(this, Arg(args, "-captureDir"), args);
                game.bot = bot;
                screen = Screen.Playing;
                hud.Show(true);
                bot.Begin();
                return;
            }
            if (Array.IndexOf(args, "-selftest") >= 0) { try { SelfTest.Run(this, Arg(args, "-captureDir")); } catch (Exception e) { Debug.LogError(e); } Application.Quit(); return; }
            if (Array.IndexOf(args, "-inputtest") >= 0) { gameObject.AddComponent<InputTour>().Begin(this, Arg(args, "-captureDir")); ShowTitle(); return; }
            if (Array.IndexOf(args, "-shots") >= 0) { gameObject.AddComponent<ScreenTour>().Begin(this, Arg(args, "-captureDir")); return; }
            ShowTitle();
        }

        static string Arg(string[] a, string name)
        {
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : ".";
        }

        void Update()
        {
            // The pixel camera works only at whole multiples of 1080; elsewhere keep the same 15-row view
            bool whole = UnityEngine.Screen.height % 1080 == 0;
            if (ppc != null && ppc.enabled != whole) ppc.enabled = whole;
            if (!whole) cam.orthographicSize = 7.5f;

            hud.Tick();
            if (bot != null) { bot.Tick(); return; }
            if (screen == Screen.Playing)
            {
                if (GameInput.PausePressed) ShowPause();
                return;
            }
            var m = GameInput.Menu();
            if (screen == Screen.Map) { MapInput(m); return; }
            if (items == null) return;
            if (m.y != 0) { cursor = (cursor - m.y + items.Count) % items.Count; Sound.Play("select", .5f); PaintMenu(); }
            if (m.x != 0 && items[cursor].adjust != null) { items[cursor].adjust(m.x); PaintMenu(); }
            if (m.ok && items[cursor].ok != null) { Sound.Play("select"); items[cursor].ok(); }
            else if (m.back && backAction != null) { Sound.Play("select"); backAction(); }
        }

        // ---- generic menu ----

        void ClearScreen()
        {
            foreach (var o in screenObjects) Destroy(o);
            screenObjects.Clear(); rows.Clear(); items = null; backAction = null; cursor = 0;
        }

        void Backdrop(float dim)
        {
            var b = hud.Picture("bg_hills", new Vector2(.5f, .5f), Vector2.zero, new Vector2(1920, 1920 * 1f));
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
            for (int i = 0; i < list.Count; i++) rows.Add(Line("", 54, top - i * 78));
            PaintMenu();
        }

        void PaintMenu()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i == cursor;
                string s = items[i].label();
                rows[i].text = Rtl.V(on ? "< " + s + " >" : s);
                rows[i].color = on ? new Color(1, .86f, .3f) : Color.white;
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
                Item(save.unlocked > 0 ? "המשך משחק" : "משחק חדש", () => { NewRun(); ShowMap(); }),
                Item("שיאים", () => ShowScores(ShowTitle)),
                Item("הגדרות", () => ShowSettings(ShowTitle)),
                Item("קרדיטים", ShowCredits),
                Item("יציאה", Application.Quit),
            };
            if (save.unlocked > 0) list.Insert(1, Item("משחק חדש", () => { save.unlocked = 0; mapCursor = 0; save.Write(); NewRun(); ShowMap(); }));
            Menu("קפיצה גדולה", list, null, 90, .15f);
            var h = hud.Picture("hero_b_cheer1", new Vector2(.5f, .5f), new Vector2(-640, -60), new Vector2(300, 370));
            screenObjects.Add(h.gameObject);
        }

        void NewRun() => run = new RunState();

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
                new MenuItem { label = () => "מסך מלא  " + (save.fullscreen ? "כן" : "לא"), ok = ToggleFull, adjust = d => ToggleFull() },
                Item("חזרה", () => { save.Write(); back(); }),
            };
            Menu("הגדרות", list, () => { save.Write(); back(); }, 120, .35f, !overGame);
            Line("חצים ימינה ושמאלה משנים ערך", 34, -320, new Color(.85f, .9f, 1));
            Line("זזים: חצים או WASD    קופצים: Space    רצים וזורקים: Shift", 34, -380, new Color(.85f, .9f, 1));
            Line("דלת סודית: חץ למטה", 34, -430, new Color(.85f, .9f, 1));
        }

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
                "גרפיקה וצלילים: Kenney (www.kenney.nl) - CC0",
                "New Platformer Pack, Platformer Characters",
                "Impact, Digital Audio, Music Jingles",
                "מוזיקה: Juhani Junkala - CC0",
                "גופן: Rubik - SIL OFL",
                "נבנה ב-Unity",
            };
            for (int i = 0; i < text.Length; i++) Line(text[i], 44, 230 - i * 80);
        }

        void ShowPause()
        {
            screen = Screen.Paused; game.paused = true; Sound.Duck(true); Sound.Play("select");
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

        // ---- map: the path runs right to left, like the language ----

        static readonly Vector2[] Nodes =
        {
            new Vector2(720, -120), new Vector2(480, 60), new Vector2(240, -120),
            new Vector2(0, 60), new Vector2(-240, -120), new Vector2(-480, 60), new Vector2(-720, -60),
        };

        public void ShowMap()
        {
            ClearScreen();
            hud.Show(false); screen = Screen.Map;
            Sound.Music("m_map");
            Backdrop(.1f);
            Line("מפת המסע", 72, 430);
            for (int i = 0; i < Nodes.Length - 1; i++)
                for (int k = 1; k < 6; k++)
                {
                    var p = Vector2.Lerp(Nodes[i], Nodes[i + 1], k / 6f);
                    var dot = hud.Picture("hud_coin", new Vector2(.5f, .5f), p, new Vector2(20, 20));
                    dot.color = i < save.unlocked ? Color.white : new Color(.3f, .3f, .35f);
                    screenObjects.Add(dot.gameObject);
                }
            for (int i = 0; i < Nodes.Length; i++)
            {
                bool open = i <= save.unlocked, done = i < save.unlocked;
                string icon = !open ? "lock_yellow" : Levels.All[i].boss ? "boss_walk_a" : done ? "block_empty" : "block_coin";
                var n = hud.Picture(icon, new Vector2(.5f, .5f), Nodes[i], new Vector2(100, 100));
                screenObjects.Add(n.gameObject);
                var t = hud.Label(40, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Nodes[i] + new Vector2(0, -92), new Vector2(240, 60));
                t.text = Rtl.V(Levels.All[i].Code); screenObjects.Add(t.gameObject);
            }
            mapHero = hud.Picture(run.power == 0 ? "hero_s_stand" : "hero_b_stand", new Vector2(.5f, .5f), Nodes[mapCursor] + new Vector2(0, 110), new Vector2(100, 120));
            screenObjects.Add(mapHero.gameObject);
            mapInfo = Line("", 52, -320);
            mapStats = Line("", 40, -395);
            Line("רווח - משחקים    Esc - תפריט", 34, -470, new Color(.9f, .95f, 1));
            PaintMap();
        }

        Image mapHero; Text mapInfo, mapStats;

        void PaintMap()
        {
            var d = Levels.All[mapCursor];
            mapHero.rectTransform.anchoredPosition = Nodes[mapCursor] + new Vector2(0, 110);
            mapInfo.text = Rtl.V($"{d.Code}: {d.name}");
            mapStats.text = Rtl.V($"שיא בשלב: {save.best[mapCursor]}    ניקוד: {run.score}    חיים: {run.lives}");
        }

        void MapInput((int x, int y, bool ok, bool back) m)
        {
            // Left moves forward, because the path runs right to left
            int d = -m.x;
            if (d != 0)
            {
                int to = Mathf.Clamp(mapCursor + d, 0, Mathf.Min(save.unlocked, Levels.All.Length - 1));
                if (to != mapCursor) { mapCursor = to; Sound.Play("select", .5f); PaintMap(); }
            }
            if (m.ok) Enter(mapCursor);
            else if (m.back) ShowTitle();
        }

        public string ScreenName => screen.ToString();
        public void ShowCreditsForTour() => ShowCredits();
        public void ShowEndingForTour() { run.score = 45200; run.secrets = 5; ShowEnding(); }
        public void Enter(int level) { ClearScreen(); hud.Show(true); screen = Screen.Playing; game.StartLevel(level); }

        // ---- flow from the level ----

        public void LevelDone()
        {
            int i = game.levelIndex;
            save.best[i] = Mathf.Max(save.best[i], run.score);
            save.unlocked = Mathf.Max(save.unlocked, Mathf.Min(i + 1, Levels.All.Length - 1));
            save.Write();
            if (bot != null) { bot.LevelFinished(); return; }
            hud.Banner("");
            game.Unload();
            if (Levels.All[i].boss) { ShowEnding(); return; }
            mapCursor = Mathf.Min(i + 1, Levels.All.Length - 1);
            ShowMap();
        }

        void ShowEnding()
        {
            hud.Show(false);
            Sound.Music("m_ending");
            int rank = save.AddScore(run.score, "סיים את המשחק");
            Menu("ניצחת!", new List<MenuItem> { Item("לשיאים", () => ShowScores(ShowTitle)) }, () => ShowScores(ShowTitle), -220, .25f, true, 300);
            Line("מלך הג'לי הובס והדרך הביתה פתוחה", 52, 120);
            Line($"ניקוד סופי: {run.score}    סודות שנמצאו: {run.secrets}", 46, 30);
            if (rank >= 0) Line($"מקום {rank + 1} בטבלת השיאים!", 46, -60, new Color(1, .86f, .3f));
            NewRun();
        }

        public void GameOver()
        {
            if (bot != null) { run.lives = 3; game.StartLevel(game.levelIndex, true); return; }
            game.Unload(); hud.Show(false); hud.Fade(0);
            Sound.Music(null); Sound.Play("gameover");
            int rank = save.AddScore(run.score, "הגיע לשלב " + Levels.All[mapCursor].Code);
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
        readonly Text scoreText, coinText, lifeText, bannerText, bossName, hintText;
        float hintT;
        readonly Image fade, bossFill; readonly GameObject bossBar;
        readonly Font font;

        public Hud()
        {
            var o = new GameObject("Hud", typeof(Canvas), typeof(CanvasScaler));
            canvas = o.transform;
            o.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = o.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            font = Resources.Load<Font>("Fonts/Rubik");

            stats = new GameObject("Stats", typeof(RectTransform));
            stats.transform.SetParent(canvas, false);
            var sr = (RectTransform)stats.transform; sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = sr.offsetMax = Vector2.zero;
            // Right to left: score, coins, lives
            scoreText = Label(46, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-40, -24), new Vector2(520, 70), stats.transform, new Vector2(1, 1));
            Picture("hud_coin", new Vector2(1, 1), new Vector2(-700, -54), new Vector2(60, 60), stats.transform);
            coinText = Label(46, TextAnchor.UpperLeft, new Vector2(1, 1), new Vector2(-540, -24), new Vector2(120, 70), stats.transform, new Vector2(1, 1));
            Picture("hud_heart", new Vector2(1, 1), new Vector2(-970, -54), new Vector2(60, 60), stats.transform);
            lifeText = Label(46, TextAnchor.UpperLeft, new Vector2(1, 1), new Vector2(-810, -24), new Vector2(120, 70), stats.transform, new Vector2(1, 1));

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
            fade = Panel(Color.clear);
            fade.gameObject.name = "Fade";
            BossBar(-1);
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

        public void Show(bool on) { stats.SetActive(on); if (!on) { Banner(""); Fade(0); Hint(0, ""); } }
        public void Hint(float seconds, string text) { hintT = seconds; hintText.text = seconds > 0 ? text : ""; }
        public void Tick() { if (hintT > 0 && (hintT -= Time.unscaledDeltaTime) <= 0) hintText.text = ""; }
        public void Banner(string s) { bannerText.text = s; bannerText.transform.SetAsLastSibling(); }
        public void Fade(float a) { fade.color = new Color(0, 0, 0, a); fade.transform.SetAsLastSibling(); }

        public void Stats(int score, int coins, int lives)
        {
            scoreText.text = Rtl.V("ניקוד " + score.ToString("000000"));
            coinText.text = "x" + coins.ToString("00");
            lifeText.text = "x" + lives;
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
