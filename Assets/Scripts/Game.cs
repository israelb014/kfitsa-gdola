using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Kfitsa
{
    /// Runs the level: fixed-step simulation, camera, HUD and the win and lose flow.
    public class Game : MonoBehaviour
    {
        enum Phase { Play, Dying, Won, GameOver }

        public Grid2 grid;
        public Player hero;
        public readonly List<Walker> walkers = new List<Walker>();
        public int coins, score, lives = 3, deaths, hits;
        public float levelTime;
        public bool Finished => phase == Phase.Won;
        public float CamLeft => camX - HalfW;

        Phase phase;
        Transform root;
        Camera cam;
        SpriteRenderer heroView, doorTop, doorBottom;
        public readonly List<Crystal> crystals = new List<Crystal>();
        public readonly List<Surprise> surprises = new List<Surprise>();
        readonly List<CoinItem> coinItems = new List<CoinItem>();
        readonly List<Fx> fx = new List<Fx>();
        readonly List<SpriteRenderer> sky = new List<SpriteRenderer>();
        float acc, freeze, phaseT, camX, look, heroAnim, squash, simT, skidDust;
        int stompChain;
        Pad pad;
        bool latchedJump;
        Bot bot;
        Text scoreText, coinText, lifeText, bigText, hintText;
        Image fade;
        float hintT = 7f;

        PixelPerfectCamera ppc;
        float HalfH => ppc != null && ppc.orthographicSize > 0 ? ppc.orthographicSize : cam.orthographicSize;
        float HalfW => HalfH * cam.aspect;

        void Start()
        {
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;
            cam = Camera.main;
            ppc = cam.GetComponent<PixelPerfectCamera>();
            string[] args = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(args, "-feeltest") >= 0) { FeelTest.Run(Arg(args, "-captureDir")); Application.Quit(); return; }
            BuildHud();
            LoadLevel();
            if (System.Array.IndexOf(args, "-bot") >= 0) bot = new Bot(this, Arg(args, "-captureDir"));
        }

        static string Arg(string[] a, string name)
        {
            int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : ".";
        }

        void LoadLevel()
        {
            if (root != null) Destroy(root.gameObject);
            walkers.Clear(); crystals.Clear(); surprises.Clear(); coinItems.Clear(); fx.Clear(); sky.Clear();
            root = new GameObject("Level").transform;
            grid = Grid2.Parse(Resources.Load<TextAsset>("Levels/level1").text);
            grid.BuildTilemaps(root);
            BuildSky();

            hero = new Player { grid = grid, x = grid.Start.x, y = grid.Start.y };
            hero.SetBig(false);
            hero.onBump = Bump;
            hero.onJump = () => { squash = .22f; Puff(hero.x, hero.y, 3); };
            hero.onLand = () => { squash = -.2f; Puff(hero.x, hero.y, 4); };
            heroView = Art.Spawn("Hero", root, Art.Char(Art.HeroA), 10);

            foreach (var e in grid.Enemies)
            {
                var v = Art.Spawn("Walker", root, Art.Char(Art.EnemyA), 5);
                walkers.Add(new Walker { grid = grid, x = e.x, y = e.y, w = .8f, h = .7f, view = v });
            }
            foreach (var c in grid.Coins)
            {
                var v = Art.Spawn("Coin", root, Art.TileC(Art.CoinA), 1);
                v.transform.position = c;
                coinItems.Add(new CoinItem { at = c, view = v });
            }
            foreach (var s in grid.Surprises)
            {
                var v = Art.Spawn("Surprise", root, Art.SurpriseBlock, 2);
                v.transform.position = new Vector3(s.c, s.r, 0);
                surprises.Add(new Surprise { c = s.c, r = s.r, power = s.power, view = v });
            }
            BuildGate();

            look = 0; phase = Phase.Play; phaseT = 0; freeze = 0; levelTime = 0; stompChain = 0; heroGone = 0; growFlash = 0;
            camX = HalfW;
            fade.color = Color.clear;
            bigText.text = "";
        }

        /// A door between two pillars, a wooden beam across the top.
        void BuildGate()
        {
            float gx = grid.GateX - .5f;
            int floor = 0;
            while (grid.Solid((int)gx, floor)) floor++;
            doorBottom = Art.Spawn("Door", root, Art.Tile(Art.DoorBottom), -2);
            doorBottom.transform.position = new Vector3(gx, floor, 0);
            doorTop = Art.Spawn("Door", root, Art.Tile(Art.DoorTop), -2);
            doorTop.transform.position = new Vector3(gx, floor + 1, 0);
            foreach (int side in new[] { -2, 2 })
            {
                int[] col = { Art.PillarBase, Art.PillarMid, Art.PillarMid, Art.PillarTop };
                for (int i = 0; i < 4; i++)
                    Art.Spawn("Pillar", root, Art.Tile(col[i]), -3).transform.position = new Vector3(gx + side, floor + i, 0);
            }
            int[] beam = { 48, 49, 49, 49, 50 };
            for (int i = 0; i < 5; i++)
                Art.Spawn("Beam", root, Art.Tile(beam[i]), -3).transform.position = new Vector3(gx - 2 + i, floor + 4, 0);
        }

        void BuildSky()
        {
            // One strip of background tiles, wrapped by hand as the camera moves
            const float tile = 24f / Art.PPU;
            const int count = 44;   // wide enough for any screen the pixel camera can pick
            for (int i = 0; i < count; i++)
                for (int row = 0; row < 16; row++)
                {
                    int sprite = row == 5 ? 8 + i % 4 : row > 5 ? i % 4 : 16 + i % 4;
                    var v = Art.Spawn("Sky", root, Art.Bg(sprite), -100);
                    v.transform.position = new Vector3(i * tile, row * tile - tile, 0);
                    sky.Add(v);
                }
        }

        void Update()
        {
            if (grid == null) return;
            if (GameInput.QuitPressed) Application.Quit();
            if (GameInput.RestartPressed) { score = 0; coins = 0; lives = 3; LoadLevel(); return; }

            var live = bot != null ? bot.Read() : GameInput.Read();
            latchedJump |= live.jumpPressed;
            pad = live;

            acc += Mathf.Min(Time.unscaledDeltaTime, .1f);
            const float dt = 1f / Tune.SimHz;
            while (acc >= dt)
            {
                acc -= dt;
                Sim(dt);
                pad.jumpPressed = false; latchedJump = false;
            }
            Draw();
            bot?.AfterFrame();
        }

        void Sim(float dt)
        {
            simT += dt;
            var input = pad; input.jumpPressed = latchedJump;

            if (phase == Phase.Won || phase == Phase.GameOver)
            {
                phaseT += dt;
                if (phase == Phase.Won) WalkIntoDoor(dt);
                if (phaseT > 1f && input.jumpPressed)
                {
                    if (phase == Phase.GameOver) { score = 0; coins = 0; lives = 3; }
                    LoadLevel();
                }
                return;
            }
            if (freeze > 0) { freeze -= dt; return; }

            if (phase == Phase.Dying)
            {
                phaseT += dt;
                hero.Step(dt, default);
                if (phaseT > 1.9f) AfterDeath();
                return;
            }

            levelTime += dt;
            hero.Step(dt, input);
            float wall = CamLeft + hero.w / 2;
            if (hero.x < wall) { hero.x = wall; if (hero.vx < 0) hero.vx = 0; }
            if (hero.grounded) stompChain = 0;

            StepCamera(dt);
            StepWalkers(dt);
            StepItems(dt);
            if (hero.y < -2.5f) Die();
            if (hero.x >= grid.GateX - .1f && hero.grounded) Win();
        }

        void StepCamera(float dt)
        {
            float want = hero.vx > 1f ? Tune.CamLookAhead : 0;
            look = Mathf.MoveTowards(look, want, Tune.CamLookSpeed * dt);
            float target = hero.x + look;
            float next = camX + (target - camX) * (1 - Mathf.Exp(-Tune.CamFollow * dt));
            camX = Mathf.Clamp(Mathf.Max(camX, next), HalfW, grid.W - HalfW);   // forward only
        }

        void StepWalkers(float dt)
        {
            float wake = camX + HalfW + 2;
            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                var e = walkers[i];
                if (!e.awake) { e.awake = e.x < wake; if (!e.awake) continue; }
                e.Step(dt);
                if (!e.alive)
                {
                    if (e.flatTime <= 0) { Destroy(e.view.gameObject); walkers.RemoveAt(i); }
                    continue;
                }
                if (e.y < -3) { Destroy(e.view.gameObject); walkers.RemoveAt(i); continue; }
                if (!hero.Overlaps(e)) continue;
                if (hero.vy < 0 && hero.y > e.y + e.h * .35f)
                {
                    e.alive = false; e.flatTime = .45f;
                    hero.Stomped(pad.jumpHeld);
                    stompChain++;
                    score += 100 << Mathf.Min(stompChain, 5);
                    freeze = .035f;
                    Puff(e.x, e.y + .3f, 5);
                }
                else Hurt();
            }
        }

        void StepItems(float dt)
        {
            for (int i = coinItems.Count - 1; i >= 0; i--)
            {
                var c = coinItems[i];
                if (Mathf.Abs(c.at.x - hero.x) < hero.w / 2 + .35f && c.at.y > hero.y - .35f && c.at.y < hero.Top + .35f)
                {
                    AddCoin();
                    Sparkle(c.at);
                    Destroy(c.view.gameObject);
                    coinItems.RemoveAt(i);
                }
            }
            for (int i = crystals.Count - 1; i >= 0; i--)
            {
                var k = crystals[i];
                k.Step(dt);
                if (k.y < -3) { Destroy(k.view.gameObject); crystals.RemoveAt(i); continue; }
                if (k.rise <= 0 && hero.Overlaps(k))
                {
                    Destroy(k.view.gameObject); crystals.RemoveAt(i);
                    score += 1000;
                    if (!hero.big) { hero.SetBig(true); freeze = .5f; growFlash = .5f; }
                }
            }
            foreach (var s in surprises) s.bump = Mathf.Max(0, s.bump - dt);
        }

        float growFlash;

        void AddCoin()
        {
            coins++; score += 100;
            if (coins >= 100) { coins -= 100; lives++; }
        }

        void Bump(int c, int r)
        {
            foreach (var s in surprises)
            {
                if (s.c != c || s.r != r) continue;
                s.bump = .16f;
                if (s.used) return;
                s.used = true;
                s.view.sprite = Art.Tile(Art.UsedBlock);
                if (s.power)
                {
                    var v = Art.Spawn("Crystal", root, Art.TileC(Art.Gem), 1);
                    crystals.Add(new Crystal { grid = grid, x = c + .5f, y = r, w = .8f, h = .8f, view = v });
                }
                else
                {
                    AddCoin();
                    AddFx(Art.TileC(Art.CoinA), new Vector2(c + .5f, r + 1), new Vector2(0, 11), .5f, 34, 3);
                }
                return;
            }
        }

        void Hurt()
        {
            if (hero.invuln > 0) return;
            if (hero.big)
            {
                hero.SetBig(false);
                hero.invuln = Tune.Invulnerable;
                hits++;
                freeze = .3f; growFlash = .3f;
            }
            else Die();
        }

        void Die()
        {
            if (phase != Phase.Play) return;
            phase = Phase.Dying; phaseT = 0; deaths++;
            hero.dead = true; hero.vy = hero.y < 0 ? 17 : 13; hero.vx = 0;
            freeze = .25f;
        }

        void AfterDeath()
        {
            lives--;
            if (lives <= 0)
            {
                phase = Phase.GameOver; phaseT = 0;
                bigText.text = Rtl.V("המשחק נגמר\nרווח - שוב");
                return;
            }
            LoadLevel();
        }

        void Win()
        {
            phase = Phase.Won; phaseT = 0;
            score += 1000 + (hero.big ? 500 : 0);
            bigText.text = Rtl.V("השלב הושלם!\nניקוד " + score + "\nרווח - שוב\n\nאמנות: Kenney (CC0)  גופן: Rubik (OFL)");
        }

        void WalkIntoDoor(float dt)
        {
            hero.vx = 0;
            hero.x = Mathf.MoveTowards(hero.x, grid.GateX, 3f * dt);
            if (Mathf.Abs(hero.x - grid.GateX) < .05f) heroGone = Mathf.Min(1, heroGone + dt * 3);
        }

        float heroGone;

        void Puff(float x, float y, int n)
        {
            for (int i = 0; i < n; i++)
                AddFx(Art.TileC(Art.Dust), new Vector2(x + Random.Range(-.3f, .3f), y + .1f), new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(.4f, 2f)), .28f, 6, 20);
        }

        void Sparkle(Vector2 at) => AddFx(Art.TileC(Art.CoinB), at, new Vector2(0, 3), .16f, 0, 3);

        void AddFx(Sprite s, Vector2 at, Vector2 vel, float life, float gravity, int order)
        {
            var v = Art.Spawn("Fx", root, s, order);
            fx.Add(new Fx { view = v, at = at, vel = vel, life = life, gravity = gravity });
        }

        void Draw()
        {
            float dt = Time.unscaledDeltaTime;
            // Camera sits on whole pixels, so the upscaled image never shimmers
            float px = 1f / Art.PPU;
            float cx = Mathf.Round(camX / px) * px;
            camX = Mathf.Clamp(camX, HalfW, grid.W - HalfW);
            cam.transform.position = new Vector3(cx, HalfH, -10);

            // Sky drifts at a quarter of the camera speed and wraps
            const float tile = 24f / Art.PPU;
            float strip = tile * 4;
            float baseX = cx - HalfW - tile - Mathf.Repeat(cx * .75f, strip);
            int cols = sky.Count / 16;
            for (int i = 0; i < cols; i++)
                for (int row = 0; row < 16; row++)
                {
                    var t = sky[i * 16 + row].transform;
                    t.position = new Vector3(baseX + i * tile, t.position.y, 0);
                }

            DrawHero(dt);
            foreach (var e in walkers) e.Draw();
            foreach (var k in crystals) k.Draw(simT);

            var coinSprite = Art.TileC((int)(simT * 5) % 2 == 0 ? Art.CoinA : Art.CoinB);
            foreach (var c in coinItems) c.view.sprite = coinSprite;
            foreach (var s in surprises)
                s.view.transform.position = new Vector3(s.c, s.r + Mathf.Sin(s.bump / .16f * Mathf.PI) * .28f, 0);

            for (int i = fx.Count - 1; i >= 0; i--)
            {
                var f = fx[i];
                f.life -= dt;
                if (f.life <= 0) { Destroy(f.view.gameObject); fx.RemoveAt(i); continue; }
                f.vel.y -= f.gravity * dt; f.at += f.vel * dt;
                f.view.transform.position = f.at;
            }

            if (hero.skidding && hero.grounded && (skidDust -= dt) <= 0) { skidDust = .06f; Puff(hero.x - hero.facing * .3f, hero.y, 1); }

            scoreText.text = Rtl.V("ניקוד " + score.ToString("000000"));
            coinText.text = "x" + coins.ToString("00");
            lifeText.text = "x" + lives;
            hintT -= dt;
            hintText.text = hintT > 0 ? Rtl.V("חצים או WASD - זזים    Space - קופצים    Shift - רצים    R - מתחילים מחדש    Esc - יציאה") : "";
            if (phase == Phase.Won) fade.color = new Color(0, 0, 0, Mathf.Clamp01((phaseT - .6f) * .9f) * .65f);
            if (phase == Phase.GameOver) fade.color = new Color(0, 0, 0, .75f);
            if (phase == Phase.Dying && phaseT > 1.5f) fade.color = new Color(0, 0, 0, (phaseT - 1.5f) / .4f);
        }

        void DrawHero(float dt)
        {
            float speed = Mathf.Abs(hero.vx);
            heroAnim += dt * (2f + speed * 1.5f);
            int frame = !hero.grounded ? 1 : speed < .2f ? 0 : (int)heroAnim % 2;
            if (hero.skidding) frame = 1;

            growFlash = Mathf.Max(0, growFlash - dt);
            bool showBig = growFlash > 0 ? (int)(growFlash * 16) % 2 == 0 ? hero.big : !hero.big : hero.big;
            heroView.sprite = showBig ? Art.BigHero(frame) : Art.Char(frame == 0 ? Art.HeroA : Art.HeroB);

            // Skidding faces into the turn; dying shows no facing change
            heroView.flipX = (hero.skidding ? -hero.facing : hero.facing) < 0;
            squash = Mathf.MoveTowards(squash, 0, dt * 1.6f);
            float sy = 1 + squash, sx = 1 - squash * .7f;
            float gone = phase == Phase.Won ? 1 - heroGone : 1;
            heroView.transform.localScale = new Vector3(sx * gone, sy * gone, 1);
            heroView.transform.position = new Vector3(hero.x, hero.y, 0);
            heroView.enabled = hero.invuln <= 0 || (int)(simT * 14) % 2 == 0;
        }

        void BuildHud()
        {
            var o = new GameObject("Hud", typeof(Canvas), typeof(CanvasScaler));
            var canvas = o.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = o.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var font = Resources.Load<Font>("Fonts/Rubik");

            var f = new GameObject("Fade", typeof(Image));
            f.transform.SetParent(o.transform, false);
            fade = f.GetComponent<Image>();
            fade.color = Color.clear;
            var fr = fade.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;

            // Right to left: score, coins, lives
            scoreText = Label(o.transform, font, 46, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-40, -28), new Vector2(520, 70));
            Icon(o.transform, Art.Tile(Art.CoinA), new Vector2(-690, -30));
            coinText = Label(o.transform, font, 46, TextAnchor.UpperLeft, new Vector2(1, 1), new Vector2(-560, -28), new Vector2(120, 70));
            Icon(o.transform, Art.Tile(Art.Heart), new Vector2(-900, -30));
            lifeText = Label(o.transform, font, 46, TextAnchor.UpperLeft, new Vector2(1, 1), new Vector2(-770, -28), new Vector2(120, 70));
            hintText = Label(o.transform, font, 32, TextAnchor.LowerCenter, new Vector2(.5f, 0), new Vector2(0, 30), new Vector2(1800, 60));
            hintText.rectTransform.pivot = new Vector2(.5f, 0);
            bigText = Label(o.transform, font, 76, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1700, 700));
            bigText.rectTransform.pivot = new Vector2(.5f, .5f);
        }

        static Text Label(Transform p, Font font, int size, TextAnchor a, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var o = new GameObject("Label", typeof(Text), typeof(Shadow));
            o.transform.SetParent(p, false);
            var t = o.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = a; t.color = Color.white; t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var s = o.GetComponent<Shadow>(); s.effectColor = new Color(.13f, .15f, .22f, 1); s.effectDistance = new Vector2(3, -3);
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(1, 1); r.anchoredPosition = pos; r.sizeDelta = box;
            return t;
        }

        static void Icon(Transform p, Sprite s, Vector2 pos)
        {
            var o = new GameObject("Icon", typeof(Image));
            o.transform.SetParent(p, false);
            var i = o.GetComponent<Image>(); i.sprite = s; i.raycastTarget = false;
            var r = i.rectTransform; r.anchorMin = r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 1);
            r.anchoredPosition = pos; r.sizeDelta = new Vector2(54, 54);
        }
    }
}
