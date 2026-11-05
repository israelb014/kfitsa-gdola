using UnityEngine;

namespace Kfitsa
{
    /// Game's view of the level mechanics: building them, drawing them, and running the bonus room.
    public partial class Game
    {
        void BuildMechanics()
        {
            int starId = 0;
            foreach (var at in grid.Stars)
            {
                int id = starId++;
                if ((gotStars >> id & 1) != 0) continue;     // taken before the last death
                bool had = (SavedStars >> id & 1) != 0;
                var v = Art.Spawn("Star", root, Art.Center("star"), 4);
                v.transform.position = at;
                if (had) v.color = new Color(1, 1, 1, .45f);    // already saved on an earlier visit
                stars.Add(new Star { at = at, id = id, view = v });
            }
            for (int r = 0; r < grid.H; r++)
                for (int c = 0; c < grid.W; c++)
                {
                    char ch = grid.At(c, r);
                    switch (ch)
                    {
                        case 'u':
                            var u = Art.Spawn("Crumble", root, Art.Tile("block_crumble"), 2);
                            u.transform.position = new Vector3(c, r, 0);
                            crumbles.Add(new Crumble { c = c, r = r, view = u });
                            break;
                        case '{': case '}':
                            var b = Art.Spawn("Belt", root, Art.Tile("conveyor_a"), 2);
                            b.transform.position = new Vector3(ch == '{' ? c + 1 : c, r, 0);
                            if (ch == '{') b.transform.localScale = new Vector3(-1, 1, 1);
                            belts.Add(b);
                            break;
                        case 'a': case 'A':
                            var g = Art.Spawn("Gate", root, Art.Tile(ch == 'a' ? "block_red" : "block_blue"), 2);
                            g.transform.position = new Vector3(c, r, 0);
                            gates.Add((g, ch));
                            break;
                    }
                }
            foreach (var (c, r) in grid.Switches)
            {
                var v = Art.Spawn("Switch", root, Art.Tile("switch_red"), 2);
                v.transform.position = new Vector3(c, r, 0);
                switches.Add((v, c, r));
            }
            foreach (var at in grid.RoomLights)
            {
                var v = Art.Spawn("Lamp", root, Art.Tile("torch_off"), -3);
                v.transform.position = at - new Vector2(.5f, .5f);
                lights.Add((at, v, false));
            }
            PaintGates();
            if (grid.DarkRadius > 0) dark = new Darkness(root, grid.DarkRadius);
            if (grid.Tide != null || grid.Flood != null || grid.RoomPool != null) water = new WaterView(root);
            if (grid.Winds.Length > 0) wind = new WindView(root);
            if (grid.Chase != null)
            {
                chaser = Art.Spawn("Chaser", root, Art.Feet("boss_walk_a"), 6);
                chaser.enabled = false;
            }
        }

        void PaintGates()
        {
            bool on = (world.sw & 1) != 0;
            foreach (var (view, kind) in gates)
                view.color = new Color(1, 1, 1, (kind == 'a') != on ? 1 : .22f);
            foreach (var (view, _, _) in switches) view.sprite = Art.Tile(on ? "switch_blue" : "switch_red");
        }

        void StepCrumbles(float dt)
        {
            int under = World.CrumbleUnder(grid, hero);
            foreach (var k in crumbles)
            {
                bool stood = hero.grounded && hero.on == null && Mathf.FloorToInt(hero.y - .05f) == k.r && hero.Left < k.c + 1 && hero.Right > k.c;
                if (k.down)
                {
                    k.t -= dt;
                    bool clear = !(hero.Left < k.c + 1 && hero.Right > k.c && hero.y < k.r + 1 && hero.Top > k.r);
                    if (k.t <= 0 && clear)
                    {
                        k.down = false; k.t = 0; grid.Restore(k.c, k.r, 'u');
                        k.view.enabled = true; k.view.transform.position = new Vector3(k.c, k.r, 0);
                    }
                }
                else if (k.t > 0 || stood)
                {
                    k.t += dt;
                    k.view.transform.position = new Vector3(k.c + Mathf.Sin(simT * 60) * .04f, k.r, 0);
                    if (k.t >= World.CrumbleDelay)
                    {
                        k.down = true; k.t = World.CrumbleBack; grid.Clear(k.c, k.r);
                        k.view.enabled = false;
                        AddFx(Art.Tile("block_crumble"), new Vector2(k.c, k.r), new Vector2(0, -1), .8f, 34, 2);
                        Sound.Play("brick", .6f);
                    }
                }
            }
        }

        // ---- bonus room: each mode is a small challenge whose prize is the room's star ----

        Star RoomStar => stars.Find(s => s.at.x >= grid.MainW);

        void RoomBegin()
        {
            var star = RoomStar;
            if (star == null) { app.hud.Hint(3f, Rtl.V("את הכוכב של החדר כבר לקחת")); return; }
            roomDone = false; roomOpen = true; roomTimer = grid.RoomSeconds;
            string mode = grid.RoomMode;
            star.hidden = mode == "coins" || mode == "lights" || mode == "stomp";
            star.view.enabled = !star.hidden;
            foreach (var c in coinItems) if (c.room && mode == "coins") c.view.enabled = true;
            if (mode == "stomp" && !enemies.Exists(e => e.alive && e.x >= grid.MainW))
                foreach (var (at, kind) in grid.Enemies)
                    if (at.x >= grid.MainW) AddEnemy(at, kind);
            app.hud.Hint(4f, Rtl.V(Def.roomTip));
            if (roomTimer > 0) app.hud.Timer(roomTimer);
        }

        void StepRoom(float dt)
        {
            if (!inRoom || !roomOpen) return;
            var star = RoomStar;
            if (star == null) { roomOpen = false; return; }
            string mode = grid.RoomMode;

            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                if (l.lit || !(Mathf.Abs(l.at.x - hero.x) < hero.w / 2 + .35f && l.at.y > hero.y - .35f && l.at.y < hero.Top + .35f)) continue;
                l.lit = true; lights[i] = l;
                Sound.Play("magic");
            }

            if (!roomDone && star.hidden)
            {
                bool solved = mode == "coins" ? !coinItems.Exists(c => c.room && c.view.enabled)
                    : mode == "lights" ? lights.TrueForAll(l => l.lit)
                    : !enemies.Exists(e => e.alive && e.x >= grid.MainW);
                if (solved)
                {
                    roomDone = true; star.hidden = false; star.view.enabled = true;
                    app.hud.Timer(-1); roomTimer = 0;
                    Sound.Play("win", .7f);
                    app.hud.Hint(3f, Rtl.V("הכוכב הופיע!"));
                }
            }
            if (roomTimer > 0)
            {
                roomTimer -= dt;
                app.hud.Timer(Mathf.Max(0, roomTimer));
                if (roomTimer <= 0)
                {
                    roomOpen = false; app.hud.Timer(-1);
                    star.hidden = true; star.view.enabled = false;
                    foreach (var c in coinItems) if (c.room) c.view.enabled = false;
                    Sound.Play("lose", .5f);
                    app.hud.Hint(4f, Rtl.V("נגמר הזמן! צאו מהדלת ונסו שוב"));
                }
            }
        }

        void DrawMechanics(float dt)
        {
            if (flag != null && checkpointSet) flag.sprite = Art.Tile((int)(simT * 5) % 2 == 0 ? "flag_a" : "flag_b");
            var belt = Art.Tile((int)(simT * 8) % 2 == 0 ? "conveyor_a" : "conveyor_b");
            foreach (var b in belts) b.sprite = belt;
            foreach (var (view, box) in powerHints)
            {
                // The prize floats above its box until the box is used
                view.enabled = !box.used;
                view.transform.position = new Vector3(box.c + .5f, box.r + 1.45f + Mathf.Sin(simT * 4) * .12f, 0);
                view.transform.localScale = Vector3.one * .62f;
                view.color = new Color(1, 1, 1, .85f);
            }
            for (int i = 0; i < lights.Count; i++) if (lights[i].lit) lights[i].view.sprite = Art.Tile((int)(simT * 7) % 2 == 0 ? "torch_on_a" : "torch_on_b");
            if (grid.FalseWalls != null)
            {
                // A false wall fades while the hero is inside it
                int c = Mathf.FloorToInt(hero.x), r = Mathf.FloorToInt(hero.y + .5f);
                bool inside = grid.At(c, r) == 'Q';
                var col = grid.FalseWalls.color;
                col.a = Mathf.MoveTowards(col.a, inside ? .25f : 1, dt * 5);
                grid.FalseWalls.color = col;
            }
            water?.Draw(grid, world, camX, camY, HalfW, simT);
            wind?.Draw(grid, world, camX, camY, HalfW, dt);
            if (chaser != null)
            {
                float front = World.ChaseAt(grid, world);
                chaser.enabled = front > -50 && front < grid.Chase.end;
                chaser.transform.position = new Vector3(front - 1.3f, hero.y > 2 ? Mathf.Max(ChaserFloor(front), 2) : 2, 0);
                chaser.sprite = Art.Feet((int)(simT * 8) % 2 == 0 ? "boss_walk_a" : "boss_walk_b");
                chaser.flipX = true;
                if (chaser.enabled) shake = Mathf.Max(shake, .08f);
            }
            if (dark != null)
            {
                dark.Clear(camX, camY, HalfW, HalfH);
                dark.Add(hero.x, hero.y + hero.h / 2, inRoom ? grid.DarkRadius * .8f : grid.DarkRadius, simT);
                foreach (var t in torches) dark.Add(t.transform.position.x + .5f, t.transform.position.y + .5f, 3.6f, simT);
                foreach (var c in stars) dark.Add(c.at.x, c.at.y, 1.5f, simT);
                foreach (var l in lights) if (l.lit) dark.Add(l.at.x, l.at.y, 4.2f, simT);
                dark.Apply();
            }
        }

        float ChaserFloor(float x)
        {
            int c = Mathf.Clamp(Mathf.FloorToInt(x), 0, grid.W - 1);
            for (int r = grid.H - 1; r >= 0; r--) if (grid.Solid(c, r)) return r + 1;
            return 2;
        }
    }

    /// Low-resolution darkness that follows the camera; the bilinear filter makes the light edge soft.
    public class Darkness
    {
        const int W = 64, H = 36;
        readonly Texture2D tex; readonly SpriteRenderer view; readonly Color32[] px = new Color32[W * H]; readonly float[] light = new float[W * H];
        float left, bottom, cellX, cellY;

        public Darkness(Transform parent, float radius)
        {
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(.5f, .5f), 1, 0, SpriteMeshType.FullRect);
            view = Art.Spawn("Darkness", parent, s, 50);
        }

        public void Clear(float camX, float camY, float halfW, float halfH)
        {
            left = camX - halfW - 1; bottom = camY - halfH - 1;
            cellX = (halfW * 2 + 2) / W; cellY = (halfH * 2 + 2) / H;
            view.transform.position = new Vector3(camX, camY, 0);
            view.transform.localScale = new Vector3(cellX, cellY, 1);
            System.Array.Clear(light, 0, light.Length);
        }

        public void Add(float x, float y, float radius, float time)
        {
            radius *= 1 + Mathf.Sin(time * 9 + x) * .025f;
            int x0 = Mathf.Max(0, (int)((x - radius - left) / cellX)), x1 = Mathf.Min(W - 1, (int)((x + radius - left) / cellX) + 1);
            int y0 = Mathf.Max(0, (int)((y - radius - bottom) / cellY)), y1 = Mathf.Min(H - 1, (int)((y + radius - bottom) / cellY) + 1);
            for (int j = y0; j <= y1; j++)
                for (int i = x0; i <= x1; i++)
                {
                    float dx = left + (i + .5f) * cellX - x, dy = bottom + (j + .5f) * cellY - y;
                    float k = 1 - Mathf.Sqrt(dx * dx + dy * dy) / radius;
                    if (k <= 0) continue;
                    k = Mathf.Min(1, k * 2.2f);
                    light[j * W + i] = Mathf.Max(light[j * W + i], k);
                }
        }

        public void Apply()
        {
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(6, 6, 18, (byte)(250 * (1 - light[i])));
            tex.SetPixels32(px); tex.Apply(false);
        }
    }

    /// The moving surface of tide, flood and room pool, tiled across the screen.
    public class WaterView
    {
        readonly SpriteRenderer[] top = new SpriteRenderer[48]; readonly SpriteRenderer body;

        public WaterView(Transform parent)
        {
            for (int i = 0; i < top.Length; i++) { top[i] = Art.Spawn("Wave", parent, Art.Tile("water_top"), 20); top[i].color = new Color(1, 1, 1, .92f); }
            body = Art.Spawn("Water", parent, Art.Tile("water"), 20); body.color = new Color(1, 1, 1, .92f);
        }

        public void Draw(Grid2 g, in WorldState s, float camX, float camY, float halfW, float time)
        {
            int first = Mathf.FloorToInt(camX - halfW) - 1;
            float bottom = camY - 9;
            float lo = 99, sway = Mathf.Sin(time * 2) * .06f;
            for (int i = 0; i < top.Length; i++)
            {
                float y = World.WaterAt(g, s, first + i + .5f);
                top[i].enabled = y > -50;
                if (!top[i].enabled) continue;
                top[i].transform.position = new Vector3(first + i, y - 1 + sway, 0);
                top[i].transform.localScale = new Vector3(1, 1, 1);
            }
            // The body is one stretched tile per column pair, so a span of water can have ends
            body.enabled = false;
            for (int i = 0; i < top.Length; i++) if (top[i].enabled) lo = Mathf.Min(lo, top[i].transform.position.y);
            if (lo < 50 && lo > bottom)
            {
                int a = 0, b = top.Length - 1;
                while (a < b && !top[a].enabled) a++;
                while (b > a && !top[b].enabled) b--;
                body.enabled = true;
                body.transform.position = new Vector3(first + a, bottom, 0);
                body.transform.localScale = new Vector3(b - a + 1, lo - bottom, 1);
            }
        }
    }

    /// Streaks that show where the wind blows and which way, plus a warning shortly before it starts.
    public class WindView
    {
        readonly SpriteRenderer[] streak = new SpriteRenderer[26]; readonly float[] ox = new float[26], oy = new float[26];

        public WindView(Transform parent)
        {
            for (int i = 0; i < streak.Length; i++)
            {
                streak[i] = Art.Spawn("Streak", parent, Art.Center("streak"), 30);
                ox[i] = Random.value * 40; oy[i] = Random.value;
            }
        }

        public void Draw(Grid2 g, in WorldState s, float camX, float camY, float halfW, float dt)
        {
            for (int i = 0; i < streak.Length; i++)
            {
                WindZone w = null; float seen = 0;
                foreach (var z in g.Winds)
                    if (z.x1 > camX - halfW && z.x0 < camX + halfW && (camY + 8 > z.y0 && camY - 8 < z.y1))
                    {
                        float a = z.Blowing(s.t, s.sw) ? 1 : z.Warning(s.t) * .35f;
                        if (a > seen) { seen = a; w = z; }
                    }
                var v = streak[i];
                v.enabled = w != null && seen > 0;
                if (!v.enabled) continue;
                ox[i] = Mathf.Repeat(ox[i] + w.push * 3.2f * seen * dt, 40);
                float x = Mathf.Max(w.x0, camX - halfW) + Mathf.Repeat(ox[i], Mathf.Max(2, Mathf.Min(w.x1, camX + halfW) - Mathf.Max(w.x0, camX - halfW)));
                float y0 = Mathf.Max(w.y0, camY - 8), y1 = Mathf.Min(w.y1, camY + 8);
                if (w.lift != 0) { x = w.x0 + oy[i] * (w.x1 - w.x0); v.transform.position = new Vector3(x, y0 + Mathf.Repeat(ox[i], y1 - y0), 0); v.transform.localEulerAngles = new Vector3(0, 0, 90); }
                else { v.transform.position = new Vector3(x, y0 + oy[i] * (y1 - y0), 0); v.transform.localEulerAngles = Vector3.zero; }
                v.color = new Color(1, 1, 1, .55f * seen);
            }
        }
    }

    /// Short vibrations on the phone. Does nothing on a computer or when turned off in the settings.
    public static class Haptics
    {
        public static bool On = true;
        public static int Count;       // for the tests
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaClass main;
#endif
        public static void Buzz(int ms, float strength)
        {
            if (!On) return;
            Count++;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                main ??= new AndroidJavaClass("com.ibfix.kfitsagdola.GameMain");
                main.CallStatic("buzz", ms, Mathf.RoundToInt(strength * 255));
            }
            catch (System.Exception e) { Debug.LogWarning("Vibration failed: " + e.Message); On = false; }
#endif
        }
    }
}
