using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kfitsa
{
    /// The world map: a drawn path from level to level that the hero walks along. It runs right to left, like the language.
    /// Each stop shows the stars and secrets still missing, and a level just finished opens the way to the next one.
    public partial class App
    {
        const float MapRight = 720, MapLeft = -720, MapLow = -40, MapHigh = 130, MapWalk = 560, MapReveal = 900;

        /// Evenly spread from right to left, zigzagging up and down; the last stop sits a little lower.
        static Vector2 Layout(int i, int n) => new Vector2(Mathf.Lerp(MapRight, MapLeft, i / (n - 1f)), i == n - 1 ? 20 : i % 2 == 0 ? MapLow : MapHigh);

        // v2.4: two pages. The first holds worlds 1 to 3; the hidden world has a page of its own, open once
        // every star of the first page is found.
        public static int MainCount => System.Array.FindIndex(Levels.All, d => d.hidden) is int k && k >= 0 ? k : Levels.All.Length;
        int pageFirst, pageEnd;          // the levels on the page shown, both ends included
        bool HiddenPage => pageFirst >= MainCount;
        Vector2 Node(int level) => Layout(level - pageFirst, pageEnd - pageFirst + 1);
        public bool HiddenOpen
        {
            get
            {
                if (MainCount >= Levels.All.Length) return false;
                for (int i = 0; i < MainCount; i++) if (Bits(save.stars[i]) < 3) return false;
                return true;
            }
        }
        public int MainStarsMissing { get { int n = 0; for (int i = 0; i < MainCount; i++) n += 3 - Bits(save.stars[i]); return n; } }
        void SetPage()
        {
            if (mapCursor >= MainCount && !Levels.All[mapCursor].Bonus && !HiddenOpen) mapCursor = MainCount - 1;
            bool hidden = mapCursor >= MainCount && !Levels.All[mapCursor].Bonus;
            pageFirst = hidden ? MainCount : 0; pageEnd = hidden ? Levels.StoryCount - 1 : MainCount - 1;
        }

        static int[] secretTotals;
        /// How many secrets each level hides: the bonus room's door, then every hidden block.
        public static int SecretTotal(int level)
        {
            if (secretTotals == null)
            {
                secretTotals = new int[Levels.All.Length];
                for (int i = 0; i < secretTotals.Length; i++)
                {
                    var g = Grid2.Parse(Levels.Text(Levels.All[i].file));
                    secretTotals[i] = (g.HasDoor ? 1 : 0) + g.Blocks.FindAll(b => b.c < g.MainW && (b.kind == BlockKind.HiddenCoin || b.kind == BlockKind.HiddenLife)).Count;
                }
            }
            return secretTotals[level];
        }

        static int Bits(int v) { int n = 0; for (; v != 0; v >>= 1) n += v & 1; return n; }

        // The path as a smooth line through the stops, measured in pixels along its length
        readonly List<Vector2> pathPts = new List<Vector2>();
        readonly List<float> pathLen = new List<float>();
        float[] nodeAt;
        float mapAt, mapReveal, mapWait, mapStep;
        int mapFrom = -1;
        bool mapEnter;
        public bool mapHold;      // screenshots: the map stops on the frame the tour stepped to
        Image mapHero, mapHat, trialBox, pageBox, shopBox; Text trialText; Text mapInfo, mapMissing, mapStats;
        readonly List<(Image dot, float at)> mapDots = new List<(Image, float)>();
        readonly List<(Image icon, Image glow, int level)> mapNodes = new List<(Image, Image, int)>();
        readonly List<float> mapPop = new List<float>();

        // ---- v2.8: bonus stops, above the path between their level and the next ----
        readonly List<(Image icon, Image glow, int level)> bonusNodes = new List<(Image, Image, int)>();
        public bool BonusOpen(int level) => Levels.All[level].Bonus && save.best[Levels.ParentOf(level)] > 0;
        /// Where the hero stands for a level: a bonus stop's hero waits on the path at its parent.
        int PathStop(int level) => Levels.All[level].Bonus ? Levels.ParentOf(level) : level;
        Vector2 BonusNode(int level) { int p = Levels.ParentOf(level); return new Vector2((Node(p).x + Node(Mathf.Min(p + 1, pageEnd)).x) / 2, 262); }
        public static Vector2 BonusNodePos(int level) { int p = Levels.ParentOf(level); return new Vector2((NodePos(p).x + NodePos(p + 1).x) / 2, 262); }

        void BuildBonusStops()
        {
            bonusNodes.Clear();
            if (HiddenPage) return;
            for (int b = Levels.StoryCount; b < Levels.All.Length; b++)
            {
                int p = Levels.ParentOf(b);
                if (p < pageFirst || p > pageEnd) continue;
                var at = BonusNode(b);
                // A short dotted trail up from the path
                for (int k = 1; k <= 3; k++)
                {
                    var dot = hud.Picture("hud_coin", new Vector2(.5f, .5f), Vector2.Lerp(Node(p) + new Vector2(0, 60), at, k / 4f), new Vector2(14, 14));
                    dot.color = BonusOpen(b) ? Color.white : new Color(.3f, .3f, .35f, .8f); screenObjects.Add(dot.gameObject);
                }
                var glow = hud.Picture("bubble", new Vector2(.5f, .5f), at, new Vector2(120, 120)); screenObjects.Add(glow.gameObject);
                var icon = hud.Picture(!BonusOpen(b) ? "lock_yellow" : save.best[b] > 0 ? "block_empty" : "block_strong_coin", new Vector2(.5f, .5f), at, new Vector2(76, 76));
                screenObjects.Add(icon.gameObject);
                bonusNodes.Add((icon, glow, b));
                if (!BonusOpen(b)) continue;
                for (int k = 0; k < 3; k++)
                {
                    var star = hud.Picture("star", new Vector2(.5f, .5f), at + new Vector2(56, 24 - k * 26), new Vector2(26, 26));
                    star.color = (save.stars[b] >> k & 1) != 0 ? Color.white : new Color(.12f, .14f, .22f, .75f);
                    screenObjects.Add(star.gameObject);
                }
            }
        }

        int MapLast => Mathf.Min(HiddenPage ? Mathf.Max(save.unlocked, pageFirst) : save.unlocked, pageEnd);
        public bool MapWalking => Mathf.Abs(mapAt - nodeAt[PathStop(mapCursor)]) > .5f;
        public float MapHeroX => mapHero.rectTransform.anchoredPosition.x;

        void BuildPath()
        {
            pathPts.Clear(); pathLen.Clear(); nodeAt = new float[Levels.All.Length];
            pathPts.Add(Node(pageFirst)); pathLen.Add(0); nodeAt[pageFirst] = 0;
            float len = 0;
            const int Steps = 24;
            for (int i = pageFirst + 1; i <= pageEnd; i++)
            {
                Vector2 p0 = Node(Mathf.Max(pageFirst, i - 2)), p1 = Node(i - 1), p2 = Node(i), p3 = Node(Mathf.Min(pageEnd, i + 1));
                for (int k = 1; k <= Steps; k++)
                {
                    float t = k / (float)Steps, t2 = t * t, t3 = t2 * t;
                    var q = .5f * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (3 * p1 - p0 - 3 * p2 + p3) * t3);
                    q.y += Mathf.Sin(t * Mathf.PI) * 40;      // each leg bows upward a little, like a trail over a hill
                    len += Vector2.Distance(q, pathPts[pathPts.Count - 1]);
                    pathPts.Add(q); pathLen.Add(len);
                }
                nodeAt[i] = len;
            }
        }

        Vector2 PathPoint(float d)
        {
            d = Mathf.Clamp(d, 0, pathLen[pathLen.Count - 1]);
            int lo = 0, hi = pathLen.Count - 1;
            while (hi - lo > 1) { int m = (lo + hi) / 2; if (pathLen[m] <= d) lo = m; else hi = m; }
            float span = pathLen[hi] - pathLen[lo];
            return Vector2.Lerp(pathPts[lo], pathPts[hi], span > 0 ? (d - pathLen[lo]) / span : 0);
        }

        public void ShowMap()
        {
            EndTrial();
            ClearScreen();
            hud.Show(false); screen = Screen.Map;
            Sound.Music("m_map");
            Backdrop(.1f);
            SetPage();
            BuildPath();
            mapDots.Clear(); mapNodes.Clear(); mapPop.Clear();
            int last = MapLast;
            // Coming back from a finished level: the hero starts there, and the new part of the path draws itself
            bool walkOn = mapFrom >= pageFirst && mapFrom < mapCursor;
            mapAt = nodeAt[walkOn ? mapFrom : PathStop(mapCursor)];
            mapReveal = walkOn ? nodeAt[mapFrom] : nodeAt[last];
            mapWait = walkOn ? .6f : 0;
            mapFrom = -1; mapEnter = false;

            Line(HiddenPage ? "ארץ הצבעים" : "מפת המסע", 72, 430);
            // Each world gets its own patch of sky behind its stops
            for (int w = 1; w <= 9; w++)
            {
                int a = System.Array.FindIndex(Levels.All, d => d.world == w), b = System.Array.FindLastIndex(Levels.All, d => d.world == w);
                if (a < pageFirst || a > pageEnd) continue;
                int end = pageEnd;     // neighbouring worlds meet halfway between their stops
                float x0 = a == pageFirst ? Node(a).x + 120 : (Node(a - 1).x + Node(a).x) / 2, x1 = b == end ? Node(b).x - 120 : (Node(b).x + Node(b + 1).x) / 2;
                var patch = hud.Picture("bg_" + Levels.All[a].sky, new Vector2(.5f, .5f), new Vector2((x0 + x1) / 2, 60), new Vector2(x0 - x1, 560));
                patch.preserveAspect = false; patch.color = new Color(1, 1, 1, .5f);
                screenObjects.Add(patch.gameObject);
                var name = hud.Label(40, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), new Vector2((x0 + x1) / 2, 335), new Vector2(x0 - x1, 60));
                name.text = Rtl.V(Levels.All[a].hidden ? "עולם נסתר" : "עולם " + w); name.color = new Color(1, .95f, .75f);
                screenObjects.Add(name.gameObject);
            }
            // Dots along the path, spaced evenly, leaving room around each stop
            for (float d = 0; d <= pathLen[pathLen.Count - 1]; d += 30)
            {
                bool nearStop = false;
                for (int i = pageFirst; i <= pageEnd; i++) nearStop |= Mathf.Abs(d - nodeAt[i]) < 58;
                if (nearStop) continue;
                var dot = hud.Picture("hud_coin", new Vector2(.5f, .5f), PathPoint(d), new Vector2(18, 18));
                screenObjects.Add(dot.gameObject);
                mapDots.Add((dot, d));
            }
            for (int i = pageFirst; i <= pageEnd; i++)
            {
                var glow = hud.Picture("bubble", new Vector2(.5f, .5f), Node(i), new Vector2(150, 150));
                screenObjects.Add(glow.gameObject);
                var n = hud.Picture("lock_yellow", new Vector2(.5f, .5f), Node(i), new Vector2(100, 100));
                screenObjects.Add(n.gameObject);
                mapNodes.Add((n, glow, i)); mapPop.Add(0);
                var t = hud.Label(40, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Node(i) + new Vector2(0, -92), new Vector2(240, 60));
                t.text = Rtl.V(Levels.All[i].Code); screenObjects.Add(t.gameObject);
                if (i > last) continue;
                for (int k = 0; k < 3; k++)
                {
                    var star = hud.Picture("star", new Vector2(.5f, .5f), Node(i) + new Vector2((k - 1) * 40, -142), new Vector2(40, 40));
                    star.color = (save.stars[i] >> k & 1) != 0 ? Color.white : new Color(.12f, .14f, .22f, .75f);
                    screenObjects.Add(star.gameObject);
                }
                int total = SecretTotal(i), got = Mathf.Min(total, Bits(save.found[i]));
                if (total == 0) continue;
                var door = hud.Picture("door_closed", new Vector2(.5f, .5f), Node(i) + new Vector2(28, -186), new Vector2(34, 34));
                door.color = got == total ? Color.white : new Color(.6f, .62f, .7f);
                screenObjects.Add(door.gameObject);
                var count = hud.Label(28, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Node(i) + new Vector2(-18, -186), new Vector2(80, 40));
                count.text = got + "/" + total; count.color = got == total ? new Color(1, .9f, .4f) : Color.white;
                screenObjects.Add(count.gameObject);
            }
            BuildBonusStops();
            (mapHero, mapHat) = HeroPicture('s', "stand", Vector2.zero, new Vector2(100, 120));
            if (newOutfit != null) { Line(newOutfit, 40, -245, new Color(1, .86f, .3f)); newOutfit = null; }
            mapInfo = Line("", 52, -300);
            mapMissing = Line("", 38, -362);
            mapStats = Line("", 36, -414);
            // The time trial button: a dark box at the left, live for finished levels that are not bosses
            trialBox = hud.Picture("star", new Vector2(.5f, .5f), new Vector2(-760, -330), new Vector2(330, 110));
            trialBox.sprite = null; trialBox.preserveAspect = false; trialBox.raycastTarget = false;
            screenObjects.Add(trialBox.gameObject);
            trialText = hud.Label(30, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Vector2.zero, new Vector2(320, 100), trialBox.transform, new Vector2(.5f, .5f));
            // v2.6: the coin shop, at the top left
            shopBox = hud.Picture("star", new Vector2(.5f, .5f), new Vector2(-760, 420), new Vector2(330, 110));
            shopBox.sprite = null; shopBox.preserveAspect = false; shopBox.raycastTarget = false; shopBox.color = new Color(.36f, .24f, .06f, .85f);
            screenObjects.Add(shopBox.gameObject);
            var st = hud.Label(30, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), new Vector2(0, 0), new Vector2(320, 100), shopBox.transform, new Vector2(.5f, .5f));
            st.text = Rtl.V("חנות" + (TouchUi.Available ? "" : "  (B)") + "\nמטבעות: " + save.wallet); st.color = new Color(1, .9f, .5f);
            // The way between the pages: shown once the last boss of the first page is beaten
            pageBox = null;
            if (HiddenPage || MainCount < Levels.All.Length && save.unlocked >= MainCount)
            {
                pageBox = hud.Picture("star", new Vector2(.5f, .5f), new Vector2(760, -330), new Vector2(330, 110));
                pageBox.sprite = null; pageBox.preserveAspect = false; pageBox.raycastTarget = false;
                bool can = HiddenPage || HiddenOpen;
                pageBox.color = can ? new Color(.28f, .12f, .36f, .85f) : new Color(.1f, .12f, .18f, .45f);
                screenObjects.Add(pageBox.gameObject);
                var pt = hud.Label(30, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Vector2.zero, new Vector2(320, 100), pageBox.transform, new Vector2(.5f, .5f));
                pt.text = Rtl.V(HiddenPage ? "חזרה למפת המסע" : HiddenOpen ? "לארץ הצבעים!" : $"עולם נסתר\nחסרים {MainStarsMissing} כוכבים");
                pt.color = can ? new Color(1, .85f, 1) : new Color(.7f, .72f, .8f);
            }
            Line(TouchUi.Available ? "הקשה על שלב בוחרת אותו, הקשה נוספת משחקת    כפתור חזור - תפריט" : "רווח - משחקים    Esc - תפריט", 34, -470, new Color(.9f, .95f, 1));
            PaintMap();
            MapTick(0);
        }

        void PaintMap()
        {
            var d = Levels.All[mapCursor];
            if (trialBox != null)
            {
                bool open = TrialOpen(mapCursor); int t = save.trialTime[mapCursor];
                trialBox.color = open ? new Color(.1f, .14f, .26f, .85f) : new Color(.1f, .12f, .18f, .45f);
                string key = TouchUi.Available ? "" : "  (T)";
                trialText.text = Rtl.V("מרוץ נגד השעון" + key + "\n" + (!open ? (d.boss ? "לא בשלב בוס" : d.Bonus ? "לא בשלב בונוס" : "מסיימים את השלב קודם") : t > 0 ? "שיא " + Hud.TimeText(t / 10f) : "עוד אין שיא"));
                trialText.color = open ? new Color(1, .9f, .45f) : new Color(.7f, .72f, .8f);
            }
            mapInfo.text = Rtl.V($"{d.Code}: {d.name}");
            var miss = new List<string>();
            for (int k = 0; k < 3; k++) if ((save.stars[mapCursor] >> k & 1) == 0) miss.Add((k + 1).ToString());
            int total = SecretTotal(mapCursor), got = Mathf.Min(total, Bits(save.found[mapCursor]));
            string stars = miss.Count == 0 ? "כל הכוכבים נמצאו" : "חסרים כוכבים: " + string.Join(", ", miss);
            string secrets = total == 0 ? "" : got == total ? "    כל הסודות נמצאו" : $"    סודות: {got}/{total}";
            mapMissing.text = Rtl.V(stars + secrets);
            mapMissing.color = miss.Count == 0 && got == total ? new Color(1, .9f, .4f) : new Color(.9f, .95f, 1);
            int all = 0;
            for (int i = pageFirst; i <= pageEnd; i++) all += Bits(save.stars[i]);
            mapStats.text = Rtl.V($"כוכבים: {all}/{(pageEnd - pageFirst + 1) * 3}    שיא בשלב: {save.best[mapCursor]}" + (save.bestTime[mapCursor] > 0 ? "    זמן שיא: " + Hud.TimeText(save.bestTime[mapCursor] / 10f) : "") + $"    ניקוד: {run.score}    חיים: {run.lives}" + (Easy ? "    קושי: קל" : ""));
        }

        /// The hero walks toward the chosen stop, and the path lights up as far as it is open.
        void MapTick(float dt)
        {
            if (mapHero == null || nodeAt == null) return;
            float open = nodeAt[MapLast];
            if (mapWait > 0) mapWait -= dt;
            else if (mapReveal < open) mapReveal = Mathf.Min(open, mapReveal + MapReveal * dt);
            float target = nodeAt[PathStop(mapCursor)];
            bool walking = mapWait <= 0 && Mathf.Abs(mapAt - target) > .5f;
            int dir = 0;
            if (walking)
            {
                float next = Mathf.MoveTowards(mapAt, target > mapAt ? Mathf.Min(target, mapReveal) : target, MapWalk * dt);   // never past the lit path
                dir = next < mapAt ? 1 : -1;      // forward is to the left
                mapAt = next;
                mapStep += dt;
                if (Mathf.Abs(mapAt - target) <= .5f) { mapAt = target; Sound.Play("select", .4f); }
            }
            else mapStep = 0;

            for (int i = 0; i < mapDots.Count; i++)
                mapDots[i].dot.color = mapDots[i].at <= mapReveal ? Color.white : new Color(.3f, .3f, .35f, .8f);
            for (int i = 0; i < mapNodes.Count; i++)
            {
                var (icon, glow, level) = mapNodes[i];
                bool open2 = level <= MapLast && nodeAt[level] <= mapReveal + 1;
                bool done = level < save.unlocked;
                var ld = Levels.All[level];
                string want = !open2 ? "lock_yellow" : ld.boss ? (ld.terrain == "snow" ? "giant_walk_a" : ld.hidden ? "king_blue_walk_a" : "boss_walk_a") : done ? "block_empty" : "block_coin";
                var sprite = Art.Center(want);
                if (icon.sprite != sprite)
                {
                    if (dt > 0 && want != "lock_yellow" && icon.sprite == Art.Center("lock_yellow")) { mapPop[i] = .35f; Sound.Play("magic", .6f); }
                    icon.sprite = sprite;
                }
                mapPop[i] = Mathf.Max(0, mapPop[i] - dt);
                icon.rectTransform.localScale = Vector3.one * (1 + Mathf.Sin(mapPop[i] / .35f * Mathf.PI) * .35f);
                // The chosen stop glows softly
                glow.enabled = level == mapCursor;
                glow.color = new Color(1, .95f, .6f, .35f + .15f * Mathf.Sin(Time.unscaledTime * 4));
            }
            foreach (var (icon, glow, level) in bonusNodes)
            {
                glow.enabled = level == mapCursor;
                glow.color = new Color(.8f, .95f, 1, .4f + .15f * Mathf.Sin(Time.unscaledTime * 4));
            }

            // Feet on the path between stops, standing on top of the block at a stop
            float near = float.MaxValue;
            for (int i = pageFirst; i <= pageEnd; i++) near = Mathf.Min(near, Mathf.Abs(mapAt - nodeAt[i]));
            float lift = Mathf.Lerp(110, 52, Mathf.SmoothStep(0, 1, (near - 30) / 50));
            var at = PathPoint(mapAt) + new Vector2(0, lift);
            char form = run.power == 3 ? 'f' : run.power == 2 ? 'p' : run.power == 1 ? 'b' : 's';
            string pose = walking ? ((int)(mapStep * 8) % 2 == 0 ? "walk1" : "walk2") : "stand";
            mapHero.sprite = Art.Center(Outfit.HeroSprite(form, pose));
            Outfit.Place(mapHat, mapHero, form, pose);
            if (dir != 0) mapHero.rectTransform.localScale = new Vector3(dir > 0 ? -1 : 1, 1, 1);
            mapHero.rectTransform.anchoredPosition = at + new Vector2(0, walking ? Mathf.Abs(Mathf.Sin(mapStep * 12)) * 6 : 0);

            if (!walking && mapWait <= 0 && mapEnter && Mathf.Abs(mapAt - target) <= .5f) { mapEnter = false; Enter(mapCursor); }
        }

        // For the self test: the map as it opens after a level, and the clock and buttons driven by hand
        public void MapTestFinished(int level) { save.unlocked = Mathf.Max(save.unlocked, level + 1); mapCursor = level + 1; mapFrom = level; ShowMap(); }
        public void MapTestTick(float seconds, int x = 0, bool ok = false)
        {
            MapInput((x, 0, ok, false));
            for (float t = 0; t < seconds && screen == Screen.Map; t += 1 / 60f) MapTick(1 / 60f);
        }
        public static float NodeX(int level) => Layout(level, MainCount).x;
        public static Vector2 NodePos(int level) => Layout(level, MainCount);

        /// Up from a stop to its bonus level, down back to the path.
        void MapBonus(int dy)
        {
            if (dy > 0 && !Levels.All[mapCursor].Bonus)
            {
                int b = Levels.BonusOf(mapCursor);
                if (b < 0 || !BonusOpen(b)) return;
                mapCursor = b;
            }
            else if (dy < 0 && Levels.All[mapCursor].Bonus) mapCursor = Levels.ParentOf(mapCursor);
            else return;
            mapEnter = false; Sound.Play("select", .5f); PaintMap();
        }
        public void MapBonusForTest(int dy) => MapBonus(dy);

        void MapChoose(int to)
        {
            // Past either end of a page: over to the other page, when it is open
            if (to > pageEnd && !HiddenPage && HiddenOpen) { MapPage(); return; }
            if (to < pageFirst && HiddenPage) { MapPage(); return; }
            to = Mathf.Clamp(to, pageFirst, MapLast);
            if (to == mapCursor) return;
            mapCursor = to; mapEnter = false; mapWait = 0;
            Sound.Play("select", .5f); PaintMap();
        }

        /// Plays the chosen level, or, if the hero is still on the way, as soon as the hero gets there.
        /// The time trial of the chosen level, once the hero stands on it.
        void MapTrial()
        {
            if (!TrialOpen(mapCursor)) { Sound.Play("bump", .5f); return; }
            if (!MapWalking && mapWait <= 0) EnterTrial(mapCursor);
        }
        public void MapTrialForTest() => MapTrial();

        void MapGo()
        {
            if (!MapWalking && mapWait <= 0) Enter(mapCursor);
            else { mapEnter = true; mapWait = 0; }
        }

        /// Over to the other page of the map.
        void MapPage()
        {
            if (!HiddenPage && !HiddenOpen) { Sound.Play("bump", .5f); return; }
            mapCursor = HiddenPage ? MainCount - 1 : MainCount; mapFrom = -1;
            Sound.Play("magic", .7f);
            ShowMap();
        }
        public void MapPageForTest() => MapPage();
        public int PageFirst => pageFirst;

        void MapTap(Vector2 at)
        {
            if (pageBox != null && RectTransformUtility.RectangleContainsScreenPoint(pageBox.rectTransform, at, null)) { MapPage(); return; }
            if (trialBox != null && RectTransformUtility.RectangleContainsScreenPoint(trialBox.rectTransform, at, null)) { MapTrial(); return; }
            if (shopBox != null && RectTransformUtility.RectangleContainsScreenPoint(shopBox.rectTransform, at, null)) { Sound.Play("select"); ShowShop(ShowMap); return; }
            var mid = new Vector2(UnityEngine.Screen.width, UnityEngine.Screen.height) / 2;
            float reach = Mathf.Min(115, (MapRight - MapLeft) / Mathf.Max(1, pageEnd - pageFirst) * .55f);
            foreach (var (icon, _, level) in bonusNodes)
            {
                if (!BonusOpen(level) || Vector2.Distance(at, mid + BonusNode(level) * hud.Scale) > 60 * hud.Scale) continue;
                if (level == mapCursor) MapGo(); else { mapCursor = level; mapEnter = false; Sound.Play("select", .5f); PaintMap(); }
                return;
            }
            for (int i = pageFirst; i <= MapLast; i++)
            {
                if (Vector2.Distance(at, mid + Node(i) * hud.Scale) > reach * hud.Scale) continue;
                if (i == mapCursor) MapGo(); else MapChoose(i);
                return;
            }
        }

        void MapInput((int x, int y, bool ok, bool back) m)
        {
            // Left moves forward, because the path runs right to left
            if (m.x != 0) MapChoose(PathStop(mapCursor) - m.x);
            if (m.y != 0) MapBonus(m.y);
            if (screen != Screen.Map) return;
            if (m.ok) MapGo();
            else if (GameInput.TrialPressed) MapTrial();
            else if (GameInput.ShopPressed) { Sound.Play("select"); ShowShop(ShowMap); }
            else if (m.back) ShowTitle();
        }
    }
}
