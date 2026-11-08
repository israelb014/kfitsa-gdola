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

        static Vector2[] nodes;
        /// Evenly spread from right to left, zigzagging up and down; the last stop sits a little lower.
        static Vector2[] Nodes
        {
            get
            {
                if (nodes != null) return nodes;
                int n = Levels.All.Length; nodes = new Vector2[n];
                for (int i = 0; i < n; i++)
                    nodes[i] = new Vector2(Mathf.Lerp(MapRight, MapLeft, i / (n - 1f)), i == n - 1 ? 20 : i % 2 == 0 ? MapLow : MapHigh);
                return nodes;
            }
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
        Image mapHero, mapHat; Text mapInfo, mapMissing, mapStats;
        readonly List<(Image dot, float at)> mapDots = new List<(Image, float)>();
        readonly List<(Image icon, Image glow, int level)> mapNodes = new List<(Image, Image, int)>();
        readonly List<float> mapPop = new List<float>();

        int MapLast => Mathf.Min(save.unlocked, Levels.All.Length - 1);
        public bool MapWalking => Mathf.Abs(mapAt - nodeAt[mapCursor]) > .5f;
        public float MapHeroX => mapHero.rectTransform.anchoredPosition.x;

        void BuildPath()
        {
            pathPts.Clear(); pathLen.Clear(); nodeAt = new float[Nodes.Length];
            pathPts.Add(Nodes[0]); pathLen.Add(0);
            float len = 0;
            const int Steps = 24;
            for (int i = 1; i < Nodes.Length; i++)
            {
                Vector2 p0 = Nodes[Mathf.Max(0, i - 2)], p1 = Nodes[i - 1], p2 = Nodes[i], p3 = Nodes[Mathf.Min(Nodes.Length - 1, i + 1)];
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
            ClearScreen();
            hud.Show(false); screen = Screen.Map;
            Sound.Music("m_map");
            Backdrop(.1f);
            BuildPath();
            mapDots.Clear(); mapNodes.Clear(); mapPop.Clear();
            int last = MapLast;
            // Coming back from a finished level: the hero starts there, and the new part of the path draws itself
            bool walkOn = mapFrom >= 0 && mapFrom < mapCursor;
            mapAt = nodeAt[walkOn ? mapFrom : mapCursor];
            mapReveal = walkOn ? nodeAt[mapFrom] : nodeAt[last];
            mapWait = walkOn ? .6f : 0;
            mapFrom = -1; mapEnter = false;

            Line("מפת המסע", 72, 430);
            // Each world gets its own patch of sky behind its stops
            for (int w = 1; ; w++)
            {
                int a = System.Array.FindIndex(Levels.All, d => d.world == w), b = System.Array.FindLastIndex(Levels.All, d => d.world == w);
                if (a < 0) break;
                int end = Nodes.Length - 1;     // neighbouring worlds meet halfway between their stops
                float x0 = a == 0 ? Nodes[a].x + 120 : (Nodes[a - 1].x + Nodes[a].x) / 2, x1 = b == end ? Nodes[b].x - 120 : (Nodes[b].x + Nodes[b + 1].x) / 2;
                var patch = hud.Picture("bg_" + Levels.All[a].sky, new Vector2(.5f, .5f), new Vector2((x0 + x1) / 2, 60), new Vector2(x0 - x1, 560));
                patch.preserveAspect = false; patch.color = new Color(1, 1, 1, .5f);
                screenObjects.Add(patch.gameObject);
                var name = hud.Label(40, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), new Vector2((x0 + x1) / 2, 335), new Vector2(x0 - x1, 60));
                name.text = Rtl.V("עולם " + w); name.color = new Color(1, .95f, .75f);
                screenObjects.Add(name.gameObject);
            }
            // Dots along the path, spaced evenly, leaving room around each stop
            for (float d = 0; d <= pathLen[pathLen.Count - 1]; d += 30)
            {
                bool nearStop = false;
                foreach (float n in nodeAt) nearStop |= Mathf.Abs(d - n) < 58;
                if (nearStop) continue;
                var dot = hud.Picture("hud_coin", new Vector2(.5f, .5f), PathPoint(d), new Vector2(18, 18));
                screenObjects.Add(dot.gameObject);
                mapDots.Add((dot, d));
            }
            for (int i = 0; i < Nodes.Length; i++)
            {
                var glow = hud.Picture("bubble", new Vector2(.5f, .5f), Nodes[i], new Vector2(150, 150));
                screenObjects.Add(glow.gameObject);
                var n = hud.Picture("lock_yellow", new Vector2(.5f, .5f), Nodes[i], new Vector2(100, 100));
                screenObjects.Add(n.gameObject);
                mapNodes.Add((n, glow, i)); mapPop.Add(0);
                var t = hud.Label(40, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Nodes[i] + new Vector2(0, -92), new Vector2(240, 60));
                t.text = Rtl.V(Levels.All[i].Code); screenObjects.Add(t.gameObject);
                if (i > last) continue;
                for (int k = 0; k < 3; k++)
                {
                    var star = hud.Picture("star", new Vector2(.5f, .5f), Nodes[i] + new Vector2((k - 1) * 40, -142), new Vector2(40, 40));
                    star.color = (save.stars[i] >> k & 1) != 0 ? Color.white : new Color(.12f, .14f, .22f, .75f);
                    screenObjects.Add(star.gameObject);
                }
                int total = SecretTotal(i), got = Mathf.Min(total, Bits(save.found[i]));
                if (total == 0) continue;
                var door = hud.Picture("door_closed", new Vector2(.5f, .5f), Nodes[i] + new Vector2(28, -186), new Vector2(34, 34));
                door.color = got == total ? Color.white : new Color(.6f, .62f, .7f);
                screenObjects.Add(door.gameObject);
                var count = hud.Label(28, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Nodes[i] + new Vector2(-18, -186), new Vector2(80, 40));
                count.text = got + "/" + total; count.color = got == total ? new Color(1, .9f, .4f) : Color.white;
                screenObjects.Add(count.gameObject);
            }
            (mapHero, mapHat) = HeroPicture('s', "stand", Vector2.zero, new Vector2(100, 120));
            if (newOutfit != null) { Line(newOutfit, 40, -245, new Color(1, .86f, .3f)); newOutfit = null; }
            mapInfo = Line("", 52, -300);
            mapMissing = Line("", 38, -362);
            mapStats = Line("", 36, -414);
            Line(TouchUi.Available ? "הקשה על שלב בוחרת אותו, הקשה נוספת משחקת    כפתור חזור - תפריט" : "רווח - משחקים    Esc - תפריט", 34, -470, new Color(.9f, .95f, 1));
            PaintMap();
            MapTick(0);
        }

        void PaintMap()
        {
            var d = Levels.All[mapCursor];
            mapInfo.text = Rtl.V($"{d.Code}: {d.name}");
            var miss = new List<string>();
            for (int k = 0; k < 3; k++) if ((save.stars[mapCursor] >> k & 1) == 0) miss.Add((k + 1).ToString());
            int total = SecretTotal(mapCursor), got = Mathf.Min(total, Bits(save.found[mapCursor]));
            string stars = miss.Count == 0 ? "כל הכוכבים נמצאו" : "חסרים כוכבים: " + string.Join(", ", miss);
            string secrets = total == 0 ? "" : got == total ? "    כל הסודות נמצאו" : $"    סודות: {got}/{total}";
            mapMissing.text = Rtl.V(stars + secrets);
            mapMissing.color = miss.Count == 0 && got == total ? new Color(1, .9f, .4f) : new Color(.9f, .95f, 1);
            int all = 0;
            foreach (int bits in save.stars) all += Bits(bits);
            mapStats.text = Rtl.V($"כוכבים: {all}/{Levels.All.Length * 3}    שיא בשלב: {save.best[mapCursor]}    ניקוד: {run.score}    חיים: {run.lives}" + (Easy ? "    קושי: קל" : ""));
        }

        /// The hero walks toward the chosen stop, and the path lights up as far as it is open.
        void MapTick(float dt)
        {
            if (mapHero == null || nodeAt == null) return;
            float open = nodeAt[MapLast];
            if (mapWait > 0) mapWait -= dt;
            else if (mapReveal < open) mapReveal = Mathf.Min(open, mapReveal + MapReveal * dt);
            float target = nodeAt[mapCursor];
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
                string want = !open2 ? "lock_yellow" : Levels.All[level].boss ? (Levels.All[level].terrain == "snow" ? "giant_walk_a" : "boss_walk_a") : done ? "block_empty" : "block_coin";
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

            // Feet on the path between stops, standing on top of the block at a stop
            float near = float.MaxValue;
            foreach (float n in nodeAt) near = Mathf.Min(near, Mathf.Abs(mapAt - n));
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
        public static float NodeX(int level) => Nodes[level].x;
        public static Vector2 NodePos(int level) => Nodes[level];

        void MapChoose(int to)
        {
            to = Mathf.Clamp(to, 0, MapLast);
            if (to == mapCursor) return;
            mapCursor = to; mapEnter = false; mapWait = 0;
            Sound.Play("select", .5f); PaintMap();
        }

        /// Plays the chosen level, or, if the hero is still on the way, as soon as the hero gets there.
        void MapGo()
        {
            if (!MapWalking && mapWait <= 0) Enter(mapCursor);
            else { mapEnter = true; mapWait = 0; }
        }

        void MapTap(Vector2 at)
        {
            var mid = new Vector2(UnityEngine.Screen.width, UnityEngine.Screen.height) / 2;
            float reach = Mathf.Min(115, (MapRight - MapLeft) / (Nodes.Length - 1) * .55f);
            for (int i = 0; i <= MapLast; i++)
            {
                if (Vector2.Distance(at, mid + Nodes[i] * hud.Scale) > reach * hud.Scale) continue;
                if (i == mapCursor) MapGo(); else MapChoose(i);
                return;
            }
        }

        void MapInput((int x, int y, bool ok, bool back) m)
        {
            // Left moves forward, because the path runs right to left
            if (m.x != 0) MapChoose(mapCursor - m.x);
            if (screen != Screen.Map) return;
            if (m.ok) MapGo();
            else if (m.back) ShowTitle();
        }
    }
}
