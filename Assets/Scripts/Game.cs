using System.Collections.Generic;
using UnityEngine;

namespace Kfitsa
{
    /// Plays one level: fixed-step simulation, camera, and the win and lose flow.
    public partial class Game : MonoBehaviour
    {
        enum Phase { Intro, Play, Door, Dying, Won }

        public App app;
        public Grid2 grid;
        public Player hero;
        public Boss boss;
        public readonly List<Enemy> enemies = new List<Enemy>();
        public readonly List<Pickup> pickups = new List<Pickup>();
        public readonly List<Block> blocks = new List<Block>();
        public int levelIndex, deaths, hits, levelCoins;
        public float levelTime;
        public bool paused, god, inRoom, skipIntro;
        public bool windless;       // tests only: the level without its storm
        public Vector2 lastFall;    // tests only
        public bool Won => phase == Phase.Won;
        public bool Running => grid != null;
        public LevelDef Def => levelIndex < 0 ? Levels.Test : Levels.All[levelIndex];
        int SavedStars { get => levelIndex < 0 ? 0 : app.save.stars[levelIndex]; set { if (levelIndex >= 0 && !trial) app.save.stars[levelIndex] = value; } }

        // ---- time trial (v2.3): no enemies, no lives, no flags, and a ghost of the best run ----
        public bool trial;
        public Ghost record = new Ghost(), best = new Ghost();
        SpriteRenderer ghostView;
        public bool GhostShown => ghostView != null && ghostView.enabled;
        public Vector2 GhostAt => ghostView != null ? (Vector2)ghostView.transform.position : Vector2.zero;

        // For the tests
        public bool tidy;       // bricks stay and pickups do nothing, so a route replays exactly as the search saw it
        public int GotStars => gotStars;
        public bool CheckpointReached => flagAt >= 0;
        public int TopsInAir => tops.Count;
        public float TopHeight => tops.Count > 0 ? tops[0].y : 0;
        public int TopDir => tops.Count > 0 ? tops[0].dir : 0;
        public int RoomCoinsLeft => coinItems.FindAll(c => c.room && c.view.enabled).Count;
        public bool RoomCoinAt(float x, float y) => coinItems.Exists(c => c.room && c.view.enabled && Mathf.Abs(c.at.x - x) < .1f && Mathf.Abs(c.at.y - y) < .1f);
        public bool RoomSolved => roomDone;
        public bool LampLit(float x, float y) => lights.Exists(l => l.lit && Mathf.Abs(l.at.x - x) < .1f && Mathf.Abs(l.at.y - y) < .1f);
        public void ApplyPowerForTest(int p) => ApplyPower(p);
        public void WinForTest() => Win();
        public void FallForTest() { hero.y = -4; hero.vy = -5; }
        public void AddCoinForTest() => AddCoin();
        public void RemoveEnemies()
        {
            foreach (var e in enemies) Destroy(e.view.gameObject);
            enemies.Clear();
            if (boss != null) { Destroy(boss.view.gameObject); boss = null; }
        }
        public void SnapCamera() { SetCamBounds(); camX = Mathf.Clamp(hero.x, camMin, camMax); camY = Mathf.Clamp(hero.y + 2.5f, HalfH, camTop); }
        public void RestartAtFlag() { flagAt = grid.FlagSpots(app.Easy).IndexOf(grid.Checkpoint); StartLevel(levelIndex, true); RemoveEnemies(); }
        public int FlagCount => flags.Count;
        public void DrawNow() => Draw();     // screenshots: draw a frame the test stepped to

        public int EnemyCount => enemies.Count;
        
        const float HalfH = 7.5f;
        float HalfW => HalfH * cam.aspect;
        float CamLeft => camX - HalfW;

        Phase phase;
        Transform root;
        Camera cam;
        SpriteRenderer heroView, gateDoor, gateTop;
        readonly List<CoinItem> coinItems = new List<CoinItem>();
        readonly List<Spring> springs = new List<Spring>();
        readonly List<Sevivon> tops = new List<Sevivon>();
        readonly List<SnowBall> snowBalls = new List<SnowBall>();
        public int SnowBallCount => snowBalls.Count;
        readonly List<Star> stars = new List<Star>();
        readonly List<Crumble> crumbles = new List<Crumble>();
        readonly List<(SpriteRenderer view, char kind)> gates = new List<(SpriteRenderer, char)>();
        readonly List<(SpriteRenderer view, int c, int r)> switches = new List<(SpriteRenderer, int, int)>();
        readonly List<SpriteRenderer> belts = new List<SpriteRenderer>();
        readonly List<(SpriteRenderer view, Block box)> powerHints = new List<(SpriteRenderer, Block)>();
        readonly List<(Vector2 at, SpriteRenderer view, bool lit)> lights = new List<(Vector2, SpriteRenderer, bool)>();
        Darkness dark; WaterView water; WindView wind;
        public StormView storm;
        public Grid2 Level => grid;
        SpriteRenderer chaser;
        public WorldState world;
        int gotStars; float roomTimer; bool roomOpen, roomDone; bool crumbleShaking;
        readonly List<Fx> fx = new List<Fx>();
        readonly List<SpriteRenderer> sky = new List<SpriteRenderer>(), torches = new List<SpriteRenderer>();
        float acc, freeze, phaseT, camX, camY = HalfH, look, heroAnim, squash, simT, skidDust, growFlash, heroGone, shake, throwT;
        float camMin, camMax, camTop;
        int stompChain, coinStreak;
        int hitsAtStart, squeaks;      // v2.2: for the achievements of this level
        public bool NewBestTime; public float LastTime;
        float coinStreakT;
        SpriteRenderer bubbleView;
        bool bossWarned;
        bool pendingDoor, lastDown;
        int flagAt = -1;            // the furthest checkpoint touched
        readonly List<(Vector2 at, SpriteRenderer view)> flags = new List<(Vector2, SpriteRenderer)>();
        Pad pad; bool latchedJump, latchedRun, latchedThrow;

        public void Init(App a) { app = a; cam = Camera.main; }

        public void StartLevel(int index, bool keepCheckpoint = false)
        {
            if (!keepCheckpoint || trial) { flagAt = -1; gotStars = 0; }
            levelIndex = index;
            Unload();
            root = new GameObject("Level").transform;
            grid = Grid2.Parse(Levels.Text(Def.file));
            if (windless) grid.Calm();
            world = WorldState.Fresh;
            grid.BuildTilemaps(root, Def.terrain);
            BuildSky();
            BuildRoomBackdrop();

            hero = new Player { grid = grid, x = grid.Start.x, y = grid.Start.y };
            ApplyPower(app.run.power);
            hero.onBump = Bump;
            hero.onJump = () => { squash = .18f; Sound.Play("jump"); Dust(2, .9f, 1.2f); };
            hero.onLand = () =>
            {
                // The harder the landing, the deeper the squash and the bigger the cloud
                float k = Mathf.InverseLerp(6, Tune.MaxFall, hero.landSpeed);
                squash = -Mathf.Lerp(.1f, .26f, k);
                Dust(k > .5f ? 5 : 3, 1.4f + k, .8f);
            };
            var spots = grid.FlagSpots(app.Easy);
            if (flagAt >= spots.Count) flagAt = spots.Count - 1;     // difficulty changed since
            if (flagAt >= 0) { hero.x = spots[flagAt].x; hero.y = spots[flagAt].y; }
            heroView = Art.Spawn("Hero", root, Art.Hero('s', "stand"), 10);
            hatView = Art.Spawn("Hat", heroView.transform, null, 11);
            bubbleView = Art.Spawn("Bubble", root, Art.Center("bubble"), 11);
            bubbleView.enabled = false;

            int counted = 0;
            foreach (var (at, kind) in grid.Enemies)
                if (!app.Easy || at.x >= grid.MainW || counted++ % 2 == 0) AddEnemy(at, kind);     // easy: every second one stays home
            if (app.Easy && grid.Chase != null) grid.Chase.speed *= .85f;
            SleepNearMice();
            foreach (var c in grid.Coins)
            {
                var v = Art.Spawn("Coin", root, Art.Center("coin_gold"), 1);
                v.transform.position = c;
                coinItems.Add(new CoinItem { at = c, view = v, room = c.x >= grid.MainW });
            }
            BuildMechanics();
            foreach (var (c, r, kind) in grid.Blocks)
            {
                bool hidden = kind == BlockKind.HiddenCoin || kind == BlockKind.HiddenLife;
                var v = Art.Spawn("Block", root, Art.Tile(kind == BlockKind.Brick ? "bricks_brown" : kind == BlockKind.Grow ? "block_power" : "block_coin"), 2);
                v.transform.position = new Vector3(c, r, 0);
                v.enabled = !hidden;
                var box = new Block { c = c, r = r, kind = kind, view = v };
                blocks.Add(box);
                if (kind == BlockKind.Grow) powerHints.Add((Art.Spawn("Hint", root, Art.Center(GrowHint(app.run.power)), 3), box));
                if (kind == BlockKind.Shield) powerHints.Add((Art.Spawn("Hint", root, Art.Center("bubble"), 3), box));
            }
            foreach (var (c, r) in grid.Springs)
            {
                var v = Art.Spawn("Spring", root, Art.Tile("spring"), 2);
                v.transform.position = new Vector3(c, r, 0);
                springs.Add(new Spring { c = c, r = r, view = v });
            }
            foreach (var (c, r) in grid.Torches)
            {
                var v = Art.Spawn("Torch", root, Art.Tile("torch_on_a"), -3);
                v.transform.position = new Vector3(c, r, 0);
                torches.Add(v);
            }
            foreach (var p in grid.Platforms)
            {
                var o = new GameObject("Platform").transform; o.SetParent(root, false);
                int n = Mathf.RoundToInt(p.halfW * 2);
                for (int i = 0; i < n; i++)
                {
                    string part = n == 1 ? "block" : i == 0 ? "horizontal_left" : i == n - 1 ? "horizontal_right" : "horizontal_middle";
                    string art = !p.cloud ? $"t_{Def.terrain}_{part}" : n == 1 ? "snow_cloud" : i == 0 ? "snow_cloud_left" : i == n - 1 ? "snow_cloud_right" : "snow_cloud_middle";
                    var s = Art.Spawn("Part", o, Art.Tile(art), 2);
                    s.transform.localPosition = new Vector3(i - p.halfW, -1, 0);
                }
                p.view = o;
            }
            flags.Clear();
            for (int i = 0; i < spots.Count; i++)
            {
                var v = Art.Spawn("Flag", root, Art.Tile(i <= flagAt ? "flag_a" : "flag_off"), -2);
                v.transform.position = new Vector3(spots[i].x - .5f, spots[i].y, 0);
                flags.Add((spots[i], v));
            }
            if (grid.HasDoor) PlaceDoor(grid.Door);
            if (grid.HasRoomDoor) PlaceDoor(grid.RoomDoor);
            BuildGate();
            if (grid.HasBoss)
            {
                bool giant = Def.terrain == "snow";
                var v = Art.Spawn("Boss", root, Art.Feet(giant ? "giant_walk_a" : "boss_walk_a"), 6);
                boss = new Boss { grid = grid, x = grid.BossSpot.x, y = grid.BossSpot.y, w = 2.7f, h = 2.5f, view = v, onLand = BossLanded, frost = giant, onThrow = GiantThrow };
                boss.shadow = Art.Spawn("Shadow", root, Art.Center("shadow"), 4);
                boss.shadow.enabled = false;
            }

            look = 0; phase = skipIntro ? Phase.Play : Phase.Intro; phaseT = 0; freeze = 0; levelTime = 0; stompChain = 0; coinStreak = 0; bossWarned = false; hitsAtStart = hits; squeaks = 0;
            heroGone = 0; growFlash = 0; inRoom = false; climbedHere = false; shake = 0; pendingDoor = false;
            SetCamBounds();
            camX = Mathf.Clamp(hero.x, camMin, camMax);
            camY = Mathf.Clamp(hero.y + 3, HalfH, camTop);
            app.hud.Fade(0);
            app.hud.Banner(phase == Phase.Intro ? Rtl.V((index == 0 ? App.HeroName + "\n" : "") + $"שלב {Def.Code}\n{Def.name}") : "");
            app.hud.BossBar(-1);
            app.hud.Timer(-1);
            app.hud.Stars(gotStars | SavedStars, grid.Stars.Count);
            string tip = index == 0 ? (TouchUi.Available ? "משמאל זזים    מימין קופצים ורצים" : "חצים או WASD - זזים    Space - קופצים    Shift - רצים    Esc - הפסקה") : Def.tip;
            app.hud.Hint(skipIntro ? 0 : 8f, Rtl.V(tip));
            TouchUi.Current?.ShowDoor(false); TouchUi.Current?.ShowClimb(false);
            TouchUi.Current?.Teach(null); lessonWait = 0;
            Sound.Music(Def.music);
            if (trial && levelIndex >= 0)
            {
                RemoveEnemies();
                record = new Ghost(); best = Ghost.Unpack(app.save.ghost[levelIndex]);
                ghostView = Art.Spawn("Ghost", root, Art.Hero('s', "stand"), 9);
                ghostView.color = new Color(.7f, .85f, 1, .45f); ghostView.enabled = false;
                int t = app.save.trialTime[levelIndex];
                app.hud.Hint(4f, Rtl.V("מרוץ נגד השעון!  " + (t > 0 ? "השיא: " + Hud.TimeText(t / 10f) + ", ורוח הרפאים רצה איתך" : "עוד אין שיא")));
            }
            paused = false;
        }

        public void Unload()
        {
            if (root != null) Destroy(root.gameObject);
            root = null; grid = null; boss = null; ghostView = null; dark = null; water = null; wind = null; storm = null; chaser = null;
            Sound.WindStop(); app.hud.StormShow(Weather.Calm, 0, 0, 0, false);
            enemies.Clear(); pickups.Clear(); blocks.Clear(); coinItems.Clear(); springs.Clear(); tops.Clear(); snowBalls.Clear(); fx.Clear(); sky.Clear(); torches.Clear();
            stars.Clear(); crumbles.Clear(); gates.Clear(); switches.Clear(); belts.Clear(); powerHints.Clear(); lights.Clear();
        }

        float Pace => app.Easy ? .7f : 1;

        void AddEnemy(Vector2 at, char kind)
        {
            var v = Art.Spawn("Enemy", root, Art.Feet("slime_normal_walk_a"), 5);
            var e = new Enemy { grid = grid, x = at.x, y = at.y, w = kind == 'q' ? .9f : kind == 'm' ? .85f : .8f, h = kind == 'q' ? .9f : kind == 'm' ? .45f : kind == 'z' ? .6f : .7f, view = v, kind = kind };
            e.Init();
            enemies.Add(e);
        }

        void PlaceDoor(Vector2 at)
        {
            Art.Spawn("Door", root, Art.Tile("door_closed"), -2).transform.position = new Vector3(at.x - .5f, at.y, 0);
            Art.Spawn("Door", root, Art.Tile("door_closed_top"), -2).transform.position = new Vector3(at.x - .5f, at.y + 1, 0);
        }

        /// The finish: an open door between two torches, with the exit sign beside it.
        void BuildGate()
        {
            float gx = grid.GateX - .5f;
            int floor = (int)grid.GateY;
            gateDoor = Art.Spawn("Gate", root, Art.Tile("door_open"), -2);
            gateDoor.transform.position = new Vector3(gx, floor, 0);
            gateTop = Art.Spawn("Gate", root, Art.Tile("door_open_top"), -2);
            gateTop.transform.position = new Vector3(gx, floor + 1, 0);
            foreach (int side in new[] { -1, 1 })
            {
                var t = Art.Spawn("Torch", root, Art.Tile("torch_on_a"), -3);
                t.transform.position = new Vector3(gx + side, floor, 0);
                torches.Add(t);
            }
            Art.Spawn("Sign", root, Art.Tile("sign_exit"), -3).transform.position = new Vector3(gx - 2, floor, 0);
            gateDoor.enabled = gateTop.enabled = !grid.HasBoss;
        }

        void BuildSky()
        {
            for (int i = 0; i < 7; i++)
                sky.Add(Art.Spawn("Sky", root, Art.Tile("bg_" + Def.sky), -100));
        }

        void BuildRoomBackdrop()
        {
            if (grid.RoomStart < 0) return;
            for (int c = grid.RoomStart; c < grid.W; c += 3)
                for (int r = 0; r < grid.RoomH + 3; r += 3)
                {
                    var s = Art.Spawn("Room", root, Art.Tile("bg_room"), -50);
                    s.transform.position = new Vector3(c, r, 0);
                    s.transform.localScale = new Vector3(3f * Art.PPU / 256f, 3f * Art.PPU / 256f, 1);
                }
            // A room narrower than a wide phone screen: the wall simply continues past the edge
            int extra = Mathf.CeilToInt(2 * HalfW - (grid.W - grid.RoomStart));
            for (int c = 0; c < extra; c++)
                for (int r = 0; r < grid.RoomH; r++)
                    Art.Spawn("RoomWall", root, Art.Tile("t_stone_block_center"), -40).transform.position = new Vector3(grid.W + c, r, 0);
        }

        void SetCamBounds()
        {
            float half = HalfW;
            if (inRoom) { camMin = grid.RoomStart + half; camMax = Mathf.Max(camMin, grid.W - half); }
            else { camMin = half; camMax = Mathf.Max(half, grid.MainW - half); }
            camTop = Mathf.Max(HalfH, (inRoom ? grid.RoomH : grid.H) - HalfH);
        }

        void Update()
        {
            if (grid == null || paused) return;
            var live = GameInput.Read();
            latchedJump |= live.jumpPressed;
            latchedRun |= live.runPressed;
            latchedThrow |= live.throwPressed;
            pad = live;

            const float dt = 1f / Tune.SimHz;
            acc += Mathf.Min(Time.unscaledDeltaTime, .1f);
            while (acc >= dt && grid != null)
            {
                acc -= dt;
                Sim(dt);
                pad.jumpPressed = false; latchedJump = false; latchedRun = false; latchedThrow = false;
            }
            if (grid != null) Draw();
        }

        /// Runs the simulation directly, for the self test.
        public void TestStep(int steps, Pad p)
        {
            for (int i = 0; i < steps && grid != null; i++)
            {
                pad = p; latchedJump = p.jumpPressed; latchedRun = p.runPressed; latchedThrow = p.throwPressed;
                Sim(1f / Tune.SimHz);
                p.jumpPressed = false; p.runPressed = false; p.throwPressed = false;
            }
        }

        public Enemy TestEnemy(char kind, float x, float y)
        {
            AddEnemy(new Vector2(x, y), kind);
            var e = enemies[enemies.Count - 1]; e.awake = true;
            return e;
        }

        void Sim(float dt)
        {
            simT += dt;
            var input = pad; input.jumpPressed = latchedJump; input.runPressed = latchedRun; input.throwPressed = latchedThrow;

            if (phase == Phase.Intro)
            {
                phaseT += dt;
                if (phaseT > 1.3f) { phase = Phase.Play; app.hud.Banner(""); }
                return;
            }
            if (phase == Phase.Won)
            {
                phaseT += dt;
                hero.vx = 0;
                hero.x = Mathf.MoveTowards(hero.x, grid.GateX, 3f * dt);
                if (Mathf.Abs(hero.x - grid.GateX) < .05f) heroGone = Mathf.Min(1, heroGone + dt * 3);
                if (phaseT > 1.2f && input.jumpPressed || phaseT > 7f) { if (trial) app.TrialDone(); else app.LevelDone(); }
                return;
            }
            if (phase == Phase.Door)
            {
                phaseT += dt;
                app.hud.Fade(1 - Mathf.Abs(phaseT - .25f) / .25f);
                if (phaseT >= .25f && pendingDoor) { pendingDoor = false; UseDoor(); }
                if (phaseT >= .5f) { phase = Phase.Play; app.hud.Fade(0); }
                return;
            }
            if (freeze > 0) { freeze -= dt; return; }

            if (phase == Phase.Dying)
            {
                phaseT += dt;
                hero.Step(dt, default);
                app.hud.Fade(Mathf.Clamp01((phaseT - 1.5f) / .4f));
                if (phaseT > 1.9f) AfterDeath();
                return;
            }

            levelTime += dt;
            if (trial) while (record.Count < levelTime * Ghost.Rate) record.Add(hero.x, hero.y, heroPose, heroForm, hero.facing < 0);
            app.hud.Shield(app.run.shield);
            int switchWas = world.sw;
            World.Step(grid, ref world, hero, dt, input);
            if (world.sw != switchWas) { Sound.Play("bump"); PaintGates(); }
            float wall = CamLeft + hero.w / 2;
            if (hero.x < wall) { hero.x = wall; if (hero.vx < 0) hero.vx = 0; }
            if (hero.grounded) stompChain = 0;

            if (input.runPressed && hero.shooter || input.throwPressed && hero.shooter) Throw();
            StepCamera(dt);
            StepEnemies(dt);
            StepBoss(dt);
            StepSnowBalls(dt);
            StepItems(dt);
            StepTops(dt);
            input0 = input;
            StepCells();
            StepCrumbles(dt);
            StepRoom(dt);
            if (hero.y < -2.5f) Die();
            if (!god && (World.Drowned(grid, world, hero) || World.Caught(grid, world, hero))) ForceDie();
            if (phase != Phase.Play) return;

            for (int i = flags.Count - 1; i > flagAt && !inRoom; i--)
            {
                var spot = flags[i].at;
                if ((grid.MainW >= 60 && hero.x < spot.x) || hero.y < spot.y - 1) continue;
                flagAt = i; flags[i].view.sprite = Art.Tile("flag_a"); Sound.Play("magic");
                app.hud.Hint(2.5f, Rtl.V("נקודת ביקורת!"));
            }
            if (hero.grounded && input.down && !lastDown) TryDoor();
            lastDown = input.down;
            bool nearDoor = !inRoom ? grid.HasDoor && Mathf.Abs(hero.x - grid.Door.x) < 1.5f : grid.HasRoomDoor && Mathf.Abs(hero.x - grid.RoomDoor.x) < 1.5f;
            if (nearDoor) app.hud.Hint(.5f, Rtl.V(TouchUi.Available && !GameInput.GamepadActive ? "כפתור החץ - נכנסים לדלת" : "חץ למטה - נכנסים לדלת"));
            bool nearLadder = NearLadder();
            if (hero.climbing) climbedHere = true;
            if (nearLadder && !hero.climbing && !climbedHere && !nearDoor)
                app.hud.Hint(.5f, Rtl.V(TouchUi.Available && !GameInput.GamepadActive ? "כפתור החץ למעלה - מטפסים" : "חץ למעלה - מטפסים"));
            TouchUi.Current?.ShowDoor(nearDoor || nearLadder && (hero.climbing || OnLadderTop));
            TouchUi.Current?.ShowClimb(nearLadder);
            TouchUi.Current?.ShowThrow(hero.shooter);
            StepLessons(input, nearDoor);
            bool gateOpen = boss == null || !boss.alive;
            if (gateOpen && !inRoom && grid.AtGate(hero)) Win();
        }

        bool climbedHere;
        bool OnLadderTop => hero.grounded && grid.LadderTop(Mathf.FloorToInt(hero.x), Mathf.FloorToInt(hero.y - .05f));
        /// The up button is worth showing: a ladder or rope within reach, or the hero already on one.
        bool NearLadder()
        {
            if (hero.climbing || OnLadderTop) return true;
            int c = Mathf.FloorToInt(hero.x);
            for (int dc = -1; dc <= 1; dc++)
                for (int r = Mathf.FloorToInt(hero.y) - 1; r <= Mathf.FloorToInt(hero.Top) + 2; r++)
                    if (grid.Climbable(c + dc, r)) return true;
            return false;
        }

        // ---- touch lessons: the first time each button is needed, a hand points at it until it is used ----

        [System.Flags] enum Lesson { Move = 1, Jump = 2, Run = 4, Throw = 8, Door = 16, Climb = 32 }
        float lessonWait;

        bool Taught(Lesson l) => (app.save.taught & (int)l) != 0;

        void Learn(Lesson l)
        {
            if (Taught(l)) return;
            app.save.taught |= (int)l;
            app.save.Write();
            lessonWait = 1.2f;      // a breath before the next lesson
            TouchUi.Current?.Teach(null);
        }

        void StepLessons(Pad input, bool nearDoor)
        {
            var t = TouchUi.Current;
            if (t == null) return;
            if (!TouchUi.Available || GameInput.GamepadActive || !t.Visible) { t.Teach(null); return; }
            lessonWait -= 1f / Tune.SimHz;
            string want = null;
            bool first = levelIndex == 0 && !inRoom;
            if (nearDoor && !Taught(Lesson.Door)) want = "door";
            else if (t.ClimbShown && !Taught(Lesson.Climb) && !hero.climbing && !OnLadderTop) want = "up";
            else if (hero.shooter && !Taught(Lesson.Throw)) want = "throw";
            else if (first && !Taught(Lesson.Move))
            {
                want = "right";
                if (hero.x - grid.Start.x > 2.5f) Learn(Lesson.Move);
            }
            else if (first && !Taught(Lesson.Jump))
            {
                want = "jump";
                if (input.jumpPressed) Learn(Lesson.Jump);
            }
            else if (first && !Taught(Lesson.Run) && t.Mode != RunMode.Auto)
            {
                want = "run";
                if (input.run) Learn(Lesson.Run);
            }
            if (hero.climbing) Learn(Lesson.Climb);
            t.Teach(lessonWait > 0 || Taught(Lesson.Move) && want == "right" ? null : want);
        }

        void TryDoor()
        {
            if (grid.HasDoor && !inRoom && Mathf.Abs(hero.x - grid.Door.x) < .6f) EnterDoor();
            else if (grid.HasRoomDoor && inRoom && Mathf.Abs(hero.x - grid.RoomDoor.x) < .6f) EnterDoor();
        }

        void EnterDoor() { Learn(Lesson.Door); phase = Phase.Door; phaseT = 0; pendingDoor = true; Sound.Play("magic"); }

        public void UseDoor()
        {
            inRoom = !inRoom;
            // The room runs on its own clock, so its moving parts always start the same way
            if (inRoom) { mainClock = world.t; world.t = 0; Found(0); } else world.t = mainClock;
            var to = inRoom ? grid.RoomDoor : grid.Door;
            hero.x = to.x; hero.y = to.y; hero.vx = 0; hero.vy = 0;
            SetCamBounds();
            camX = Mathf.Clamp(hero.x, camMin, camMax);
            camY = Mathf.Clamp(hero.y + 3, HalfH, camTop);
            if (inRoom) RoomBegin(); else { app.hud.Timer(-1); roomOpen = false; }
        }

        void StepCamera(float dt)
        {
            float want = hero.vx > 1f ? Tune.CamLookAhead : 0;
            look = Mathf.MoveTowards(look, want, Tune.CamLookSpeed * dt);
            float target = hero.x + look;
            float next = camX + (target - camX) * (1 - Mathf.Exp(-Tune.CamFollow * dt));
            SetCamBounds();
            camX = Mathf.Clamp(next, camMin, camMax);   // free both ways, so a missed star can be gone back for
            // Tall levels: keep the hero a little below the middle, and follow faster on the way down
            float wantY = hero.y + 2.5f;
            camY += (wantY - camY) * (1 - Mathf.Exp(-(wantY < camY ? 9f : 5f) * dt));
            camY = Mathf.Clamp(camY, HalfH, camTop);
        }

        void Stomped()
        {
            hero.Stomped(pad.jumpHeld);
            stompChain++;
            if (stompChain >= 3) Achievements.Unlock(Achievements.Id.Chain);
            app.run.score += 100 << Mathf.Min(stompChain, 5);
            freeze = .07f; shake = Mathf.Max(shake, .12f);      // a beat of stillness and a small kick sells the hit
            Sound.Play("stomp");
            Haptics.Buzz(25, .5f);
        }

        void KillFlying(Enemy e, int by)
        {
            if (!e.alive) return;
            if (e.frozen > 0) { e.frozen = 0; DropIce(e, true); }
            e.alive = false; e.flying = true; e.vy = 9; e.vx = by * 3;
            app.run.score += 200;
            Sound.Play("stomp");
        }

        void StepEnemies(float dt)
        {
            float wake = camX + HalfW + 2;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                if (!e.awake) { e.awake = e.x < wake && Mathf.Abs(e.y - camY) < HalfH + 3; if (!e.awake) continue; }
                if (e.frozen > 0)
                {
                    e.frozen -= dt;
                    if (e.frozen <= 0) Thaw(e);
                    continue;       // in the ice: still, and harmless to touch
                }
                e.thawT = Mathf.Max(0, e.thawT - dt);
                if (e.asleep) { e.t += dt; if ((int)(e.t * 1.2f) != (int)((e.t - dt) * 1.2f)) Snore(e); }
                else e.Step(dt * Pace, hero.x);
                if (e.kind == 'z' && e.alive && !e.scared && MouseHears(e)) Squeak(e);
                if (e.y < -3 || !e.alive && !e.flying && e.flat <= 0) { Destroy(e.view.gameObject); enemies.RemoveAt(i); continue; }
                if (!e.alive) continue;

                if (e.kind == 'n' && e.state == 2)
                    foreach (var o in enemies)
                        if (o != e && o.alive && o.Out && e.Overlaps(o)) KillFlying(o, e.dir);

                if (!e.Out || !hero.Overlaps(e)) continue;       // a worm in the ground is out of reach
                bool stomp = hero.vy < 0 && hero.y > e.y + e.h * .35f;
                if (e.thawT > 0 && !stomp) continue;
                if (e.kind == 'z')
                {
                    // A mouse never hurts: a stomp silences it, a bump makes it squeak
                    if (stomp) { e.alive = false; e.flat = .45f; Stomped(); }
                    else if (!e.scared) Squeak(e);
                    continue;
                }
                if (e.asleep) e.asleep = false;     // touching a sleeper wakes it, whatever happens next
                if (e.kind == 'p') Hurt();
                else if (e.kind == 'n' && e.state == 1)
                {
                    e.state = 2; e.dir = hero.x < e.x ? 1 : -1; e.safe = .25f;
                    if (stomp) Stomped(); else Sound.Play("bump");
                }
                else if (e.kind == 'n' && e.state == 2)
                {
                    if (stomp) { e.state = 1; e.shellT = 6; Stomped(); }
                    else if (e.safe <= 0) Hurt();
                }
                else if (stomp)
                {
                    if (e.kind == 'n') { e.state = 1; e.shellT = 6; }
                    else if (e.kind == 'q') BecomeBlock(e);
                    else if (e.kind == 'y') { e.alive = false; e.flying = true; e.vy = 0; }
                    else { e.alive = false; e.flat = .45f; }
                    Stomped();
                }
                else Hurt();
            }
        }

        void StepBoss(float dt)
        {
            if (boss == null) return;
            if (!boss.awake)
            {
                if (hero.x > boss.x - 16) { boss.awake = true; app.hud.BossBar(1, Rtl.V(Def.name)); }
                return;
            }
            boss.Step(dt * (app.Easy ? .85f : 1), hero.x);
            if (!boss.alive) return;
            if (boss.Warning && !bossWarned) Sound.Play("bump", 1, .55f);     // a low thud as it crouches
            bossWarned = boss.Warning;
            if (hero.Overlaps(boss))
            {
                bool stomp = hero.vy < 0 && hero.y > boss.y + boss.h * .55f;
                if (stomp && !boss.Open)
                {
                    // The giant is not stuck: the hero bounces off its icy head
                    hero.Stomped(true);
                    Sound.Play("bump", 1, .7f);
                    if (!giantTold) { giantTold = true; app.hud.Hint(3f, Rtl.V("רק כשהענק תקוע אחרי זריקה")); }
                }
                else if (stomp)
                {
                    hero.Stomped(true);
                    Sound.Play("stomp");
                    if (boss.hurt <= 0) DamageBoss(2);
                }
                else if (boss.frost && boss.stuck > 0) { }     // stuck in place, it cannot hurt anyone
                else if (boss.hurt <= .9f) Hurt();
            }
        }

        void DamageBoss(int n)
        {
            boss.hp -= n; boss.hurt = 1.2f; boss.stuck = 0;
            freeze = .12f; shake = Mathf.Max(shake, .35f);
            app.run.score += 500;
            app.hud.BossBar(Mathf.Max(0, boss.hp) / (float)Boss.MaxHp, Rtl.V(Def.name));
            Sound.Play("hurt");
            if (boss.hp <= 0)
            {
                boss.alive = false; boss.vy = 12;
                Achievements.Unlock(boss.frost ? Achievements.Id.Giant : Achievements.Id.King);
                if (n == 1) Achievements.Unlock(Achievements.Id.TopWin);      // only a sevivon does 1
                app.run.score += 5000;
                foreach (var e in enemies) KillFlying(e, 1);
                foreach (var b in snowBalls) b.gone = true;
                gateDoor.enabled = gateTop.enabled = true;
                Sound.Music(null); Sound.Play("win");
                app.hud.BossBar(-1);
            }
        }

        void BossLanded()
        {
            shake = .3f;
            Sound.Play("brick");
            if (!boss.frost && boss.landings % 2 == 0 && enemies.Count < 3)
            {
                AddEnemy(new Vector2(boss.x + boss.dir * 2f, boss.y), boss.hp <= 3 ? 'p' : 'e');
                var e = enemies[enemies.Count - 1]; e.dir = boss.dir; e.awake = true;
            }
        }

        bool giantTold;

        /// The frost giant throws one snow block (two when it is nearly beaten) in an arc at the hero.
        void GiantThrow(int count)
        {
            Sound.Play("throw", 1, .6f);
            for (int i = 0; i < count; i++)
            {
                float flight = i == 0 ? 1f : .75f;
                float aim = hero.x + (i == 0 ? 0 : -boss.dir * 2.5f);
                var v = Art.Spawn("SnowBall", root, Art.Center("t_snow_block"), 8);
                v.transform.localScale = Vector3.one * .9f;
                float sx = boss.x + boss.dir * 1.2f, sy = boss.y + boss.h - .4f;
                float drop = sy - (hero.y + .2f);       // it comes down at the hero's height after the flight time
                snowBalls.Add(new SnowBall { grid = grid, view = v, x = sx, y = sy, w = .7f, h = .7f,
                    vx = Mathf.Clamp((aim - sx) / flight, -11f, 11f), vy = (SnowBall.Gravity * flight * flight / 2 - drop) / flight });
            }
        }

        void StepSnowBalls(float dt)
        {
            for (int i = snowBalls.Count - 1; i >= 0; i--)
            {
                var b = snowBalls[i];
                if (!b.gone) b.Step(dt);
                if (!b.gone && hero.Overlaps(b)) { Hurt(); b.gone = true; }
                if (!b.gone) continue;
                for (int k = 0; k < 4; k++)
                    AddFx(Art.Center("t_snow_block"), new Vector2(b.x, b.y + .3f), new Vector2((k - 1.5f) * 2.5f, 5 + k % 2 * 2), .45f, 34, 9, .3f);
                Sound.Play("brick", .5f, 1.4f);
                Destroy(b.view.gameObject); snowBalls.RemoveAt(i);
            }
        }

        void Throw()
        {
            if (tops.Count >= 2 || throwT > simT) return;
            throwT = simT + .22f;
            Learn(Lesson.Throw);
            bool frost = app.run.power >= 3;
            var v = Art.Spawn("Sevivon", root, Art.Center(frost ? "sevivon_frost" : "sevivon"), 9);
            tops.Add(new Sevivon { grid = grid, view = v, x = hero.x + hero.facing * .5f, y = hero.y + Mathf.Min(.5f, hero.h / 2), w = .5f, h = .5f, dir = hero.facing, vy = 3f, frost = frost });
            heroThrow = .2f;
            Sound.Play("throw");
        }

        float heroThrow, mainClock, climbAnim;
        Pad input0;

        void StepTops(float dt)
        {
            for (int i = tops.Count - 1; i >= 0; i--)
            {
                var d = tops[i];
                d.Step(dt);
                foreach (var e in enemies)
                    if (e.alive && e.awake && e.Out && d.Hits(e))
                    {
                        if (d.frost && e.frozen <= 0) Freeze(e); else KillFlying(e, d.dir);
                        d.gone = true; break;
                    }
                if (boss != null && boss.alive && boss.awake && d.Hits(boss))
                {
                    if (boss.hurt <= 0 && boss.Open) DamageBoss(1); else if (boss.frost) Sound.Play("bump", .6f, 1.3f);
                    d.gone = true;
                }
                foreach (var b in snowBalls) if (!b.gone && d.Hits(b)) { b.gone = true; d.gone = true; }
                if (d.gone)
                {
                    AddFx(Art.Center(d.frost ? "sevivon_frost" : "sevivon"), new Vector2(d.x, d.y), new Vector2(-d.dir * 2, 6), .4f, 34, 9);
                    Destroy(d.view.gameObject); tops.RemoveAt(i);
                }
            }
        }

        void StepItems(float dt)
        {
            for (int i = coinItems.Count - 1; i >= 0; i--)
            {
                var c = coinItems[i];
                if (!c.view.enabled) continue;
                if (Mathf.Abs(c.at.x - hero.x) < hero.w / 2 + .35f && c.at.y > hero.y - .35f && c.at.y < hero.Top + .35f)
                {
                    AddCoin();
                    if (c.room && grid.RoomCoins) c.view.enabled = false;   // comes back if the clock runs out
                    else { Destroy(c.view.gameObject); coinItems.RemoveAt(i); }
                }
            }
            for (int i = stars.Count - 1; i >= 0; i--)
            {
                var c = stars[i];
                if (c.hidden || !(Mathf.Abs(c.at.x - hero.x) < hero.w / 2 + .35f && c.at.y > hero.y - .35f && c.at.y < hero.Top + .35f)) continue;
                gotStars |= 1 << c.id;
                SavedStars |= 1 << c.id;
                app.save.Write();
                app.run.score += 2000;
                Sound.Play("gem"); Haptics.Buzz(40, .6f);
                AddFx(Art.Center("star"), c.at, new Vector2(0, 8), .6f, 20, 8);
                Destroy(c.view.gameObject); stars.RemoveAt(i);
                app.hud.Stars(gotStars | SavedStars, grid.Stars.Count);
                if (roomOpen && c.at.x >= grid.MainW) { roomOpen = false; app.hud.Timer(-1); }
            }
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                var k = pickups[i];
                k.Step(dt);
                if (k.y < -3) { Destroy(k.view.gameObject); pickups.RemoveAt(i); continue; }
                if (k.rise <= 0 && hero.Overlaps(k) && !tidy)
                {
                    Destroy(k.view.gameObject); pickups.RemoveAt(i);
                    Collect(k.type);
                }
            }
            foreach (var b in blocks) b.bump = Mathf.Max(0, b.bump - dt);
            foreach (var s in springs) s.out_ = Mathf.Max(0, s.out_ - dt);
        }

        void Collect(string type)
        {
            app.run.score += 1000;
            if (type == "life") { app.run.lives++; Sound.Play("gem"); return; }
            Sound.Play("power"); Haptics.Buzz(60, .7f);
            if (type == "shield")
            {
                if (!app.run.shield) app.hud.Hint(4f, Rtl.V("בועת מגן! היא תספוג פגיעה אחת"));
                app.run.shield = true;
                return;
            }
            int want = type == "frost" ? 3 : type == "shoot" ? 2 : 1;
            if (app.run.power >= want) return;
            ApplyPower(want);
            freeze = .5f; growFlash = .5f;
            string how = !TouchUi.Available || GameInput.GamepadActive ? "כפתור הריצה זורק סביבון" : "כפתור הזריקה זורק סביבון";
            app.hud.Hint(5f, Rtl.V(want == 1 ? "גדלת! קופסת קריאה נוספת תיתן סביבון"
                : want == 2 ? "יש סביבון! " + how
                : "סביבון כפור! אויב שנפגע קופא, ואפשר לעמוד עליו"));
        }

        void ApplyPower(int p)
        {
            app.run.power = p;
            hero.SetBig(p > 0);
            hero.shooter = p >= 2;
            app.hud.Power(p);
            foreach (var (view, box) in powerHints) if (box.kind == BlockKind.Grow) view.sprite = Art.Center(GrowHint(p));
        }

        /// What a power box gives at each step of the ladder: grow, then the sevivon, then the frost sevivon.
        static string GrowHint(int power) => power == 0 ? "gem_blue" : power == 1 ? "sevivon" : "sevivon_frost";
        static string GrowGift(int power) => power == 0 ? "grow" : power == 1 ? "shoot" : "frost";

        void StepCells()
        {
            int hit = World.SpringAt(grid, hero);
            if (hit >= 0)
            {
                hero.Launch(World.SpringSpeed); springs[hit].out_ = .25f; Sound.Play("spring");
                if (levelIndex == 0 && !input0.jumpHeld) app.hud.Hint(3f, Rtl.V("החזיקו קפיצה על הקפיץ כדי לעוף גבוה"));
            }
            int c0 = Mathf.FloorToInt(hero.Left + .15f), c1 = Mathf.FloorToInt(hero.Right - .15f), r0 = Mathf.FloorToInt(hero.y + .1f), r1 = Mathf.FloorToInt(hero.Top - .1f);
            for (int r = r0; r <= r1; r++)
                for (int c = c0; c <= c1; c++)
                {
                    if (grid.Deadly(c, r)) { if (!god) { ForceDie(); return; } }
                    else if (grid.Hazard(c, r)) Hurt();
                }
        }

        void AddCoin()
        {
            app.run.coins++; app.run.score += 100; levelCoins++;
            if (++app.save.coinsTotal >= 100) Achievements.Unlock(Achievements.Id.Coins);
            coinStreak = simT - coinStreakT < .5f ? Mathf.Min(coinStreak + 1, 12) : 0;
            coinStreakT = simT;
            Sound.Play("coin", .7f, Mathf.Pow(2, coinStreak / 12f));      // one semitone per coin, up to an octave
            if (app.run.coins >= 100) { app.run.coins -= 100; app.run.lives++; Sound.Play("gem"); }
        }

        void Bump(int c, int r)
        {
            foreach (var e in enemies)
                if (e.alive && e.grounded && Mathf.Abs(e.x - (c + .5f)) < .9f && Mathf.Abs(e.y - (r + 1)) < .1f) KillFlying(e, 1);
            foreach (var b in blocks)
            {
                if (b.c != c || b.r != r) continue;
                if (b.kind == BlockKind.Brick)
                {
                    if (hero.big && !tidy)
                    {
                        grid.Clear(c, r);
                        Destroy(b.view.gameObject); blocks.Remove(b);
                        app.run.score += 50;
                        for (int i = 0; i < 4; i++)
                            AddFx(Art.Center("bricks_brown"), new Vector2(c + .5f, r + .5f), new Vector2((i % 2 * 2 - 1) * Random.Range(2f, 4f), Random.Range(6f, 11f)), .7f, 34, 3, .35f);
                        Sound.Play("brick");
                    }
                    else { b.bump = .16f; Sound.Play("bump"); }
                    return;
                }
                b.bump = .16f;
                if (b.used) { Sound.Play("bump"); return; }
                b.used = true;
                b.view.enabled = true;
                b.view.sprite = Art.Tile("block_empty");
                if (b.kind == BlockKind.HiddenCoin || b.kind == BlockKind.HiddenLife) { grid.Reveal(c, r); app.run.secrets++; Found(1 + grid.Blocks.FindAll(k => k.kind == BlockKind.HiddenCoin || k.kind == BlockKind.HiddenLife).FindIndex(k => k.c == c && k.r == r)); }
                if (b.kind == BlockKind.Grow) Spawn(GrowGift(app.run.power), c, r);
                else if (b.kind == BlockKind.Shield) Spawn("shield", c, r);
                else if (b.kind == BlockKind.Life || b.kind == BlockKind.HiddenLife) Spawn("life", c, r);
                else
                {
                    AddCoin();
                    AddFx(Art.Center("coin_gold"), new Vector2(c + .5f, r + 1), new Vector2(0, 11), .5f, 34, 3);
                }
                return;
            }
            Sound.Play("bump", .5f);
        }

        /// A secret for the map: bit 0 is the bonus room, then one bit per hidden block in the level's order.
        void Found(int bit)
        {
            if (levelIndex < 0 || bit < 0 || (app.save.found[levelIndex] >> bit & 1) != 0) return;
            app.save.found[levelIndex] |= 1 << bit;
            app.save.Write();
        }
        public int FoundBits => levelIndex < 0 ? 0 : app.save.found[levelIndex];

        void Spawn(string type, int c, int r)
        {
            string sprite = type == "grow" ? "gem_blue" : type == "shoot" ? "gem_red" : type == "frost" ? "sevivon_frost" : type == "shield" ? "bubble" : "heart";
            var v = Art.Spawn("Pickup", root, Art.Center(sprite), 1);
            if (type == "shield") v.transform.localScale = Vector3.one * .55f;
            pickups.Add(new Pickup { grid = grid, x = c + .5f, y = r, w = .8f, h = .8f, view = v, type = type });
            Sound.Play("magic");
        }

        void Hurt()
        {
            if (hero.invuln > 0 || phase != Phase.Play) return;
            hits++;
            if (god) { hero.invuln = .3f; return; }
            Haptics.Buzz(120, .9f);
            if (app.run.shield)
            {
                app.run.shield = false;
                Achievements.Unlock(Achievements.Id.Bubble);
                hero.invuln = Tune.Invulnerable;
                freeze = .15f; shake = Mathf.Max(shake, .2f);
                Sound.Play("pop");
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3;
                    AddFx(Art.Center("bubble"), new Vector2(hero.x, hero.y + hero.h / 2), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5, .3f, 0, 11, .18f);
                }
                return;
            }
            if (hero.big)
            {
                ApplyPower(app.run.power - 1);    // one step at a time: sevivon, then big, then small
                hero.invuln = Tune.Invulnerable;
                freeze = .3f; growFlash = .3f;
                Sound.Play("shrink");
            }
            else Die();
        }

        void Die() => ForceDie();

        void ForceDie()
        {
            if (phase != Phase.Play) return;
            phase = Phase.Dying; phaseT = 0; deaths++; lastFall = new Vector2(hero.x, hero.y);
            TouchUi.Current?.Teach(null);
            hero.dead = true; hero.vy = hero.y < 0 ? 17 : 13; hero.vx = 0;
            freeze = .25f;
            app.run.power = 0; app.run.shield = false;
            Haptics.Buzz(250, 1);
            app.hud.Timer(-1);
            Sound.Music(null); Sound.Play("lose");
        }

        void AfterDeath()
        {
            if (trial) { app.run.power = 0; StartLevel(levelIndex); return; }    // a fall starts the run over, with no lives to lose
            app.run.lives--;
            if (app.run.lives <= 0) { app.GameOver(); return; }
            StartLevel(levelIndex, true);
        }

        void Win()
        {
            phase = Phase.Won; phaseT = 0;
            TouchUi.Current?.Teach(null);
            if (trial && levelIndex >= 0)
            {
                Sound.Music(null); Sound.Play("win");
                LastTime = levelTime;
                int tenths = Mathf.Max(1, Mathf.RoundToInt(levelTime * 10)), was = app.save.trialTime[levelIndex];
                NewBestTime = was == 0 || tenths < was;
                if (NewBestTime) { app.save.trialTime[levelIndex] = tenths; app.save.ghost[levelIndex] = record.Pack(); app.save.Write(); }
                app.hud.Banner(Rtl.V("מרוץ נגד השעון\nזמן " + Hud.TimeText(levelTime) + (NewBestTime ? (was == 0 ? "" : "    שיא חדש!") : "    השיא " + Hud.TimeText(was / 10f))));
                return;
            }
            app.run.score += 1000 + (hero.big ? 500 : 0);
            Sound.Music(null); Sound.Play("win");
            int found = 0;
            for (int b = gotStars | SavedStars; b != 0; b >>= 1) found += b & 1;
            // The clock: a new best time is kept per level
            LastTime = levelTime; NewBestTime = false;
            string time = "";
            if (levelIndex >= 0)
            {
                int tenths = Mathf.Max(1, Mathf.RoundToInt(levelTime * 10)), best = app.save.bestTime[levelIndex];
                NewBestTime = best == 0 || tenths < best;
                if (NewBestTime) app.save.bestTime[levelIndex] = tenths;
                time = "\nזמן " + Hud.TimeText(levelTime) + (NewBestTime ? (best == 0 ? "" : "    שיא חדש!") : "    שיא " + Hud.TimeText(best / 10f));
                Achievements.Unlock(Achievements.Id.First);
                if (hits == hitsAtStart) Achievements.Unlock(Achievements.Id.NoHit);
                if (found == 3) Achievements.Unlock(Achievements.Id.ThreeStars);
                if (Def.file == "w3l3" && squeaks == 0) Achievements.Unlock(Achievements.Id.Quiet);
            }
            app.hud.Banner(Rtl.V($"השלב הושלם!\nניקוד {app.run.score}    כוכבים {found}/3" + time));
        }

        void AddFx(Sprite s, Vector2 at, Vector2 vel, float life, float gravity, int order, float scale = 1)
        {
            var v = Art.Spawn("Fx", root, s, order);
            v.transform.localScale = new Vector3(scale, scale, 1);
            fx.Add(new Fx { view = v, at = at, vel = vel, life = life, gravity = gravity });
        }

        void Draw()
        {
            float dt = Time.unscaledDeltaTime;
            shake = Mathf.Max(0, shake - dt);
            float wobble = shake > 0 ? Mathf.Sin(simT * 70) * .1f * shake / .3f : 0;
            cam.transform.position = new Vector3(camX, camY + wobble, -10);

            // The sky drifts at a quarter of the camera speed and wraps
            const float w = 15f;
            float baseX = camX - HalfW - Mathf.Repeat((camX - HalfW) * .75f, w) - .01f;
            for (int i = 0; i < sky.Count; i++) sky[i].transform.position = new Vector3(baseX + i * w, camY - HalfH, 0);

            DrawHero(dt);
            foreach (var e in enemies) e.Draw();
            boss?.Draw();
            foreach (var b in snowBalls) { b.view.transform.position = new Vector3(b.x, b.y + b.h / 2, 0); b.view.transform.rotation = Quaternion.Euler(0, 0, b.spin * -b.vx * 40); }
            foreach (var k in pickups) k.Draw(simT);
            foreach (var p in grid.Platforms) p.view.position = new Vector3(p.x, p.y, 0);
            foreach (var d in tops)
            {
                d.view.transform.position = new Vector3(d.x, d.y + .25f, 0);
                d.view.transform.localEulerAngles = new Vector3(0, 0, Mathf.Sin(d.spin * 26) * 14);    // wobbles upright as it spins
                d.view.flipX = (int)(d.spin * 18) % 2 == 0;
            }
            DrawMechanics(dt);

            var coinSprite = Art.Center((int)(simT * 5) % 2 == 0 ? "coin_gold" : "coin_gold_side");
            foreach (var c in coinItems) c.view.sprite = coinSprite;
            foreach (var b in blocks)
                b.view.transform.position = new Vector3(b.c, b.r + Mathf.Sin(b.bump / .16f * Mathf.PI) * .28f, 0);
            foreach (var s in springs) s.view.sprite = Art.Tile(s.out_ > 0 ? "spring_out" : "spring");
            foreach (var c in stars) c.view.transform.position = c.at + new Vector2(0, Mathf.Sin(simT * 3 + c.id) * .08f);
            var torch = Art.Tile((int)(simT * 7) % 2 == 0 ? "torch_on_a" : "torch_on_b");
            foreach (var t in torches) t.sprite = torch;

            for (int i = fx.Count - 1; i >= 0; i--)
            {
                var f = fx[i];
                f.life -= dt;
                if (f.life <= 0) { Destroy(f.view.gameObject); fx.RemoveAt(i); continue; }
                f.vel.y -= f.gravity * dt; f.at += f.vel * dt;
                f.view.transform.position = f.at;
                if (f.start > 0)
                {
                    float k = 1 - f.life / f.start;
                    f.view.transform.localScale = Vector3.one * f.size * (1 + f.grow * k);
                    var col = f.view.color; col.a = f.alpha * (1 - k); f.view.color = col;
                }
            }
            if (hero.skidding && hero.grounded && (skidDust -= dt) <= 0)
            {
                skidDust = .08f; Sound.Play("bump", .12f);
                Puff(new Vector2(hero.x - hero.facing * .1f, hero.y + .1f), new Vector2(-hero.facing * 1.5f, .8f), .9f);
            }
            DrawBubble();
            app.hud.Stats(app.run.score, app.run.coins, app.run.lives);
            app.hud.Clock(levelIndex >= 0 ? levelTime : -1);
        }

        static readonly Color DustGrass = new Color(.88f, .84f, .7f), DustSand = new Color(.97f, .86f, .62f), DustStone = new Color(.86f, .86f, .9f), DustDirt = new Color(.86f, .75f, .6f);

        /// A small cloud at the hero's feet. Only on ground the hero is really standing on, not on a moving ledge.
        void Dust(int n, float spread, float rise)
        {
            if (hero.on != null || inRoom && grid.RoomPool != null) return;
            int fc = Mathf.FloorToInt(hero.x), fr = Mathf.FloorToInt(hero.y - .05f);
            if (!grid.Solid(fc, fr) && !grid.Solid(Mathf.FloorToInt(hero.Left + .05f), fr) && !grid.Solid(Mathf.FloorToInt(hero.Right - .05f), fr)) return;   // jumping off a ladder
            for (int i = 0; i < n; i++)
            {
                float side = n == 1 ? 0 : i / (n - 1f) * 2 - 1;
                Puff(new Vector2(hero.x + side * hero.w * .75f, hero.y + .1f), new Vector2(side * spread * 2.2f, rise * Random.Range(.6f, 1f)), Random.Range(.8f, 1.1f));
            }
        }

        void Puff(Vector2 at, Vector2 vel, float size)
        {
            var v = Art.Spawn("Dust", root, Art.Center("puff"), 11);
            var tint = Def.terrain == "snow" && !inRoom ? Color.white : Def.terrain == "sand" ? DustSand : Def.terrain == "stone" || inRoom ? DustStone : Def.terrain == "dirt" ? DustDirt : DustGrass;
            v.color = tint;
            float s = size * (hero.big ? 1.7f : 1.35f);
            v.transform.localScale = Vector3.one * s;
            fx.Add(new Fx { view = v, at = at, vel = vel, life = .38f, gravity = -1.5f, start = .38f, grow = 1.2f, alpha = .85f, size = s });
        }

        void DrawBubble()
        {
            bubbleView.enabled = app.run.shield && !hero.dead && phase != Phase.Won;
            if (!bubbleView.enabled) return;
            float size = (Mathf.Max(hero.h, hero.w) + .55f) / (128f / Art.PPU);
            float wob = Mathf.Sin(simT * 5) * .04f;
            bubbleView.transform.position = new Vector3(hero.x, hero.y + hero.h / 2, 0);
            bubbleView.transform.localScale = new Vector3(size * (1 + wob), size * (1 - wob), 1);
            bubbleView.sortingOrder = heroView.sortingOrder + 1;
        }

        /// The frost sevivon locks an enemy in ice for a few seconds; the ice's top is a ledge.
        void Freeze(Enemy e)
        {
            Achievements.Unlock(Achievements.Id.Freeze);
            e.frozen = FrozenSeconds; e.vx = 0; e.vy = 0;
            var o = new GameObject("Ice").transform; o.SetParent(root, false);
            var cube = Art.Spawn("IceCube", o, Art.Tile("ice"), 6);
            cube.transform.localPosition = new Vector3(-.5f, -1, 0);
            e.ice = new Platform(e.x, e.y + 1, 0, 0, 1, 0, 1) { view = o };
            grid.Platforms.Add(e.ice);
            app.run.score += 200;
            Sound.Play("freeze");
        }

        public const float FrozenSeconds = 5;

        /// Every enemy close to a mouse starts asleep, and stays still until the mouse squeaks.
        public void SleepNearMice()
        {
            foreach (var m in enemies)
                if (m.kind == 'z' && !m.scared)
                    foreach (var e in enemies)
                        if (e.kind != 'z' && e.kind != 'm' && e.kind != 'y' && Mathf.Abs(e.x - m.x) < Enemy.MouseWakes && Mathf.Abs(e.y - m.y) < 6 && (e.x >= grid.MainW) == (m.x >= grid.MainW)) e.asleep = true;
        }

        /// The mouse hears running, not walking, and only on its own side of the level.
        bool MouseHears(Enemy m)
        {
            if (!m.awake || phase != Phase.Play) return false;
            bool running = Mathf.Abs(hero.vx) > Tune.WalkSpeed + .4f;
            return running && Mathf.Abs(hero.x - m.x) < Enemy.MouseHears && Mathf.Abs(hero.y - m.y) < 4 && (hero.x >= grid.MainW) == (m.x >= grid.MainW);
        }

        void Squeak(Enemy m)
        {
            squeaks++;
            m.scared = true; m.dir = hero.x < m.x ? 1 : -1;
            Sound.Play("select", 1, 1.9f);
            AddFx(Art.Center("bubble"), new Vector2(m.x, m.y + 1.1f), new Vector2(0, 2.5f), .5f, 0, 9, .25f);
            int woke = 0;
            foreach (var e in enemies)
                if (e.asleep && Mathf.Abs(e.x - m.x) < Enemy.MouseWakes && Mathf.Abs(e.y - m.y) < 6) { e.asleep = false; e.awake = true; woke++; }
            if (!squeakTold) { squeakTold = true; app.hud.Hint(3f, Rtl.V(woke > 0 ? "העכבר צייץ והעיר את כולם!" : "העכבר צייץ וברח")); }
        }
        bool squeakTold;

        void Snore(Enemy e) => AddFx(Art.Center("bubble"), new Vector2(e.x + .3f, e.y + e.h + .2f), new Vector2(.4f, 1.2f), .9f, 0, 4, .12f);

        /// A stomped block slime turns into a block for good: it stops, and its top is a ledge.
        void BecomeBlock(Enemy e)
        {
            e.alive = false; e.flat = float.MaxValue; e.vx = 0;
            var o = new GameObject("Block slime").transform; o.SetParent(root, false);
            e.block = new Platform(e.x, e.y + 1, 0, 0, 1, 0, 1) { view = o };
            grid.Platforms.Add(e.block);
            Sound.Play("brick", .7f);
        }

        void Thaw(Enemy e)
        {
            e.frozen = 0; e.thawT = .6f;
            DropIce(e, false);
        }

        void DropIce(Enemy e, bool loud)
        {
            if (e.ice == null) return;
            grid.Platforms.Remove(e.ice);
            if (hero.on == e.ice) hero.on = null;
            for (int i = 0; i < 4; i++)
                AddFx(Art.Center("ice"), new Vector2(e.x, e.y + .5f), new Vector2((i % 2 * 2 - 1) * Random.Range(2f, 4f), Random.Range(5f, 9f)), .5f, 34, 7, .3f);
            Destroy(e.ice.view.gameObject); e.ice = null;
            Sound.Play("shatter", loud ? 1 : .6f);
        }

        void DrawHero(float dt)
        {
            float speed = Mathf.Abs(hero.vx);
            heroAnim += dt * (3f + speed * 1.6f);
            heroThrow = Mathf.Max(0, heroThrow - dt);

            string pose;
            if (hero.dead) pose = "hurt";
            else if (phase == Phase.Won) pose = "cheer1";
            else if (hero.climbing) { climbAnim += dt * Mathf.Abs(hero.vy) * 1.6f + dt * Mathf.Abs(hero.vx); pose = (int)climbAnim % 2 == 0 ? "climb1" : "climb2"; }
            else if (heroThrow > 0) pose = "action1";
            else if (!hero.grounded) pose = hero.vy > 0 ? "jump" : "fall";
            else if (hero.skidding) pose = "skid";
            else if (speed < .2f) pose = "stand";
            else pose = (int)heroAnim % 2 == 0 ? "walk1" : "walk2";

            growFlash = Mathf.Max(0, growFlash - dt);
            char form = app.run.power == 3 ? 'f' : app.run.power == 2 ? 'p' : app.run.power == 1 ? 'b' : 's';
            if (growFlash > 0 && (int)(growFlash * 16) % 2 == 0) form = form == 's' ? 'b' : 's';
            heroView.sprite = Art.Hero(form, pose);

            heroView.flipX = hero.facing < 0;
            squash = Mathf.MoveTowards(squash, 0, dt * 1.6f);
            float stretch = hero.grounded || hero.dead ? 0 : Mathf.Clamp(Mathf.Abs(hero.vy) / 30f, 0, .08f);
            float sy = 1 + squash + stretch, sx = 1 - (squash + stretch) * .7f;
            float gone = phase == Phase.Won ? 1 - heroGone : 1;
            heroView.transform.localScale = new Vector3(sx * gone, sy * gone, 1);
            heroView.transform.position = new Vector3(hero.x, hero.y, 0);
            heroView.transform.localEulerAngles = new Vector3(0, 0, storm != null ? storm.HeroTilt : 0);
            heroView.sortingOrder = storm != null ? storm.HeroOrder : 10;
            heroView.enabled = hero.invuln <= 0 || (int)(simT * 14) % 2 == 0;
            Outfit.Place(hatView, heroView, form, pose);
            if (ghostView != null)
            {
                bool on = trial && best.Count > 0 && (phase == Phase.Play || phase == Phase.Won) && levelTime <= best.Length + .3f;
                ghostView.enabled = on;
                if (on)
                {
                    var f = best.At(levelTime);
                    ghostView.sprite = Art.Hero(f.form, f.pose); ghostView.flipX = f.flip;
                    ghostView.transform.position = new Vector3(f.x, f.y, 0);
                }
            }
            heroPose = pose; heroForm = form;
        }
        SpriteRenderer hatView;
        public string heroPose = "stand"; public char heroForm = 's';
        public bool HatShown => hatView != null && hatView.enabled && hatView.sprite != null;
        public string HeroSpriteName => heroView != null && heroView.sprite != null ? heroView.sprite.name : "";
    }
}
