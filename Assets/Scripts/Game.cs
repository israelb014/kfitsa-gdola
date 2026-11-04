using System.Collections.Generic;
using UnityEngine;

namespace Kfitsa
{
    /// Plays one level: fixed-step simulation, camera, and the win and lose flow.
    public class Game : MonoBehaviour
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
        public bool Won => phase == Phase.Won;
        public bool Running => grid != null;
        public LevelDef Def => Levels.All[levelIndex];
        public Bot bot;

        const float HalfH = 7.5f;
        float HalfW => HalfH * cam.aspect;
        float CamLeft => camX - HalfW;

        Phase phase;
        Transform root;
        Camera cam;
        SpriteRenderer heroView, gateDoor, gateTop;
        readonly List<CoinItem> coinItems = new List<CoinItem>();
        readonly List<Spring> springs = new List<Spring>();
        readonly List<Disc> discs = new List<Disc>();
        readonly List<Fx> fx = new List<Fx>();
        readonly List<SpriteRenderer> sky = new List<SpriteRenderer>(), torches = new List<SpriteRenderer>();
        SpriteRenderer lever;
        float acc, freeze, phaseT, camX, look, heroAnim, squash, simT, skidDust, growFlash, heroGone, shake, throwT;
        float camMin, camMax;
        int stompChain;
        bool checkpointSet, pendingDoor, lastDown;
        Pad pad; bool latchedJump, latchedRun;

        public void Init(App a) { app = a; cam = Camera.main; }

        public void StartLevel(int index, bool keepCheckpoint = false)
        {
            if (!keepCheckpoint) checkpointSet = false;
            levelIndex = index;
            Unload();
            root = new GameObject("Level").transform;
            grid = Grid2.Parse(Resources.Load<TextAsset>("Levels/" + Def.file).text);
            grid.BuildTilemaps(root, Def.terrain);
            BuildSky();
            BuildRoomBackdrop();

            hero = new Player { grid = grid, x = grid.Start.x, y = grid.Start.y };
            ApplyPower(app.run.power);
            hero.onBump = Bump;
            hero.onJump = () => { squash = .18f; Sound.Play("jump"); };
            hero.onLand = () => squash = -.16f;
            if (checkpointSet) { hero.x = grid.Checkpoint.x; hero.y = grid.Checkpoint.y; }
            heroView = Art.Spawn("Hero", root, Art.Hero('s', "stand"), 10);

            foreach (var (at, kind) in grid.Enemies) AddEnemy(at, kind);
            foreach (var c in grid.Coins)
            {
                var v = Art.Spawn("Coin", root, Art.Center("coin_gold"), 1);
                v.transform.position = c;
                coinItems.Add(new CoinItem { at = c, view = v });
            }
            foreach (var (c, r, kind) in grid.Blocks)
            {
                bool hidden = kind == BlockKind.HiddenCoin || kind == BlockKind.HiddenLife;
                var v = Art.Spawn("Block", root, Art.Tile(kind == BlockKind.Brick ? "bricks_brown" : "block_coin"), 2);
                v.transform.position = new Vector3(c, r, 0);
                v.enabled = !hidden;
                blocks.Add(new Block { c = c, r = r, kind = kind, view = v });
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
                string[] parts = { "horizontal_left", "horizontal_middle", "horizontal_right" };
                for (int i = 0; i < 3; i++)
                {
                    var s = Art.Spawn("Part", o, Art.Tile($"t_{Def.terrain}_{parts[i]}"), 2);
                    s.transform.localPosition = new Vector3(i - 1.5f, -1, 0);
                }
                p.view = o;
            }
            if (grid.HasCheckpoint)
            {
                lever = Art.Spawn("Lever", root, Art.Tile(checkpointSet ? "lever_right" : "lever"), -2);
                lever.transform.position = new Vector3(grid.Checkpoint.x - .5f, grid.Checkpoint.y, 0);
            }
            if (grid.HasDoor) PlaceDoor(grid.Door);
            if (grid.HasRoomDoor) PlaceDoor(grid.RoomDoor);
            BuildGate();
            if (grid.HasBoss)
            {
                var v = Art.Spawn("Boss", root, Art.Feet("boss_walk_a"), 6);
                boss = new Boss { grid = grid, x = grid.BossSpot.x, y = grid.BossSpot.y, w = 2.7f, h = 2.5f, view = v, onLand = BossLanded };
            }

            look = 0; phase = bot != null || skipIntro ? Phase.Play : Phase.Intro; phaseT = 0; freeze = 0; levelTime = 0; stompChain = 0;
            heroGone = 0; growFlash = 0; inRoom = false; shake = 0; pendingDoor = false;
            SetCamBounds();
            camX = Mathf.Clamp(hero.x, camMin, camMax);
            app.hud.Fade(0);
            app.hud.Banner(phase == Phase.Intro ? Rtl.V($"שלב {Def.Code}\n{Def.name}") : "");
            app.hud.BossBar(-1);
            app.hud.Hint(index == 0 && bot == null ? 10f : 0, Rtl.V("חצים או WASD - זזים    Space - קופצים    Shift - רצים    Esc - הפסקה"));
            Sound.Music(Def.music);
            paused = false;
        }

        public void Unload()
        {
            if (root != null) Destroy(root.gameObject);
            root = null; grid = null; boss = null; lever = null;
            enemies.Clear(); pickups.Clear(); blocks.Clear(); coinItems.Clear(); springs.Clear(); discs.Clear(); fx.Clear(); sky.Clear(); torches.Clear();
        }

        void AddEnemy(Vector2 at, char kind)
        {
            var v = Art.Spawn("Enemy", root, Art.Feet("slime_normal_walk_a"), 5);
            var e = new Enemy { grid = grid, x = at.x, y = at.y, w = .8f, h = .7f, view = v, kind = kind };
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
            int floor = 0;
            while (grid.Solid((int)gx, floor)) floor++;
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
                for (int r = 0; r < 18; r += 3)
                {
                    var s = Art.Spawn("Room", root, Art.Tile("bg_room"), -50);
                    s.transform.position = new Vector3(c, r, 0);
                    s.transform.localScale = new Vector3(3f * Art.PPU / 256f, 3f * Art.PPU / 256f, 1);
                }
        }

        void SetCamBounds()
        {
            float half = HalfW;
            if (inRoom) { camMin = grid.RoomStart + half; camMax = Mathf.Max(camMin, grid.W - half); }
            else { camMin = half; camMax = Mathf.Max(half, grid.MainW - half); }
        }

        void Update()
        {
            if (grid == null || paused) return;
            var live = bot != null ? default : GameInput.Read();
            latchedJump |= live.jumpPressed;
            latchedRun |= live.runPressed;
            pad = live;

            const float dt = 1f / Tune.SimHz;
            acc += bot != null ? dt * bot.StepsPerFrame : Mathf.Min(Time.unscaledDeltaTime, .1f);
            while (acc >= dt && grid != null)
            {
                acc -= dt;
                if (bot != null) { pad = bot.Read(dt); latchedJump = pad.jumpPressed; latchedRun = pad.runPressed; }
                Sim(dt);
                pad.jumpPressed = false; latchedJump = false; latchedRun = false;
            }
            if (grid != null) Draw();
        }

        /// Runs the simulation directly, for the self test.
        public void TestStep(int steps, Pad p)
        {
            for (int i = 0; i < steps && grid != null; i++)
            {
                pad = p; latchedJump = p.jumpPressed; latchedRun = p.runPressed;
                Sim(1f / Tune.SimHz);
                p.jumpPressed = false; p.runPressed = false;
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
            var input = pad; input.jumpPressed = latchedJump; input.runPressed = latchedRun;

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
                if (phaseT > 1.2f && input.jumpPressed || phaseT > 7f) app.LevelDone();
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
            foreach (var p in grid.Platforms)
            {
                p.Step(dt);
                if (hero.on == p) { hero.MoveX(p.dx); hero.y += p.dy; }
            }
            hero.Step(dt, input);
            float wall = CamLeft + hero.w / 2;
            if (hero.x < wall) { hero.x = wall; if (hero.vx < 0) hero.vx = 0; }
            if (hero.grounded) stompChain = 0;

            if (input.runPressed && hero.shooter) Throw();
            StepCamera(dt);
            StepEnemies(dt);
            StepBoss(dt);
            StepItems(dt);
            StepDiscs(dt);
            StepCells();
            if (hero.y < -2.5f) Die();
            if (phase != Phase.Play) return;

            if (grid.HasCheckpoint && !checkpointSet && hero.x >= grid.Checkpoint.x && !inRoom)
            {
                checkpointSet = true; lever.sprite = Art.Tile("lever_right"); Sound.Play("magic");
            }
            if (hero.grounded && input.down && !lastDown) TryDoor();
            lastDown = input.down;
            bool nearDoor = !inRoom ? grid.HasDoor && Mathf.Abs(hero.x - grid.Door.x) < 1.5f : grid.HasRoomDoor && Mathf.Abs(hero.x - grid.RoomDoor.x) < 1.5f;
            if (nearDoor) app.hud.Hint(.5f, Rtl.V("חץ למטה - נכנסים לדלת"));
            bool gateOpen = boss == null || !boss.alive;
            if (gateOpen && !inRoom && hero.x >= grid.GateX - .1f && hero.grounded) Win();
        }

        void TryDoor()
        {
            if (grid.HasDoor && !inRoom && Mathf.Abs(hero.x - grid.Door.x) < .6f) EnterDoor();
            else if (grid.HasRoomDoor && inRoom && Mathf.Abs(hero.x - grid.RoomDoor.x) < .6f) EnterDoor();
        }

        void EnterDoor() { phase = Phase.Door; phaseT = 0; pendingDoor = true; Sound.Play("magic"); }

        public void UseDoor()
        {
            inRoom = !inRoom;
            var to = inRoom ? grid.RoomDoor : grid.Door;
            hero.x = to.x; hero.y = to.y; hero.vx = 0; hero.vy = 0;
            SetCamBounds();
            camX = Mathf.Clamp(hero.x, camMin, camMax);
        }

        void StepCamera(float dt)
        {
            float want = hero.vx > 1f ? Tune.CamLookAhead : 0;
            look = Mathf.MoveTowards(look, want, Tune.CamLookSpeed * dt);
            float target = hero.x + look;
            float next = camX + (target - camX) * (1 - Mathf.Exp(-Tune.CamFollow * dt));
            SetCamBounds();
            float free = inRoom ? next : Mathf.Max(camX, next);   // forward only, except in a room
            camX = Mathf.Clamp(free, camMin, camMax);
        }

        void Stomped()
        {
            hero.Stomped(pad.jumpHeld);
            stompChain++;
            app.run.score += 100 << Mathf.Min(stompChain, 5);
            freeze = .035f;
            Sound.Play("stomp");
        }

        void KillFlying(Enemy e, int by)
        {
            if (!e.alive) return;
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
                if (!e.awake) { e.awake = e.x < wake; if (!e.awake) continue; }
                e.Step(dt, hero.x);
                if (e.y < -3 || !e.alive && !e.flying && e.flat <= 0) { Destroy(e.view.gameObject); enemies.RemoveAt(i); continue; }
                if (!e.alive) continue;

                if (e.kind == 'n' && e.state == 2)
                    foreach (var o in enemies)
                        if (o != e && o.alive && e.Overlaps(o)) KillFlying(o, e.dir);

                if (!hero.Overlaps(e)) continue;
                bool stomp = hero.vy < 0 && hero.y > e.y + e.h * .35f;
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
            boss.Step(dt, hero.x);
            if (!boss.alive) return;
            if (hero.Overlaps(boss))
            {
                bool stomp = hero.vy < 0 && hero.y > boss.y + boss.h * .55f;
                if (stomp)
                {
                    hero.Stomped(true);
                    Sound.Play("stomp");
                    if (boss.hurt <= 0) DamageBoss(2);
                }
                else if (boss.hurt <= .9f) Hurt();
            }
        }

        void DamageBoss(int n)
        {
            boss.hp -= n; boss.hurt = 1.2f;
            app.run.score += 500;
            app.hud.BossBar(Mathf.Max(0, boss.hp) / (float)Boss.MaxHp, Rtl.V(Def.name));
            Sound.Play("hurt");
            if (boss.hp <= 0)
            {
                boss.alive = false; boss.vy = 12;
                app.run.score += 5000;
                foreach (var e in enemies) KillFlying(e, 1);
                gateDoor.enabled = gateTop.enabled = true;
                Sound.Music(null); Sound.Play("win");
                app.hud.BossBar(-1);
            }
        }

        void BossLanded()
        {
            shake = .3f;
            Sound.Play("brick");
            if (boss.landings % 2 == 0 && enemies.Count < 3)
            {
                AddEnemy(new Vector2(boss.x + boss.dir * 2f, boss.y), boss.hp <= 3 ? 'p' : 'e');
                var e = enemies[enemies.Count - 1]; e.dir = boss.dir; e.awake = true;
            }
        }

        void Throw()
        {
            if (discs.Count >= 2 || throwT > simT) return;
            throwT = simT + .18f;
            var v = Art.Spawn("Disc", root, Art.Center("saw"), 9);
            v.transform.localScale = new Vector3(.55f, .55f, 1);
            discs.Add(new Disc { view = v, x = hero.x + hero.facing * .5f, y = hero.y + .45f, vx = hero.facing * 14f });
            heroThrow = .2f;
            Sound.Play("throw");
        }

        float heroThrow;

        void StepDiscs(float dt)
        {
            for (int i = discs.Count - 1; i >= 0; i--)
            {
                var d = discs[i];
                d.t += dt;
                if (!d.back && (d.t > .42f || grid.Solid(Mathf.FloorToInt(d.x), Mathf.FloorToInt(d.y)))) d.back = true;
                if (d.back)
                {
                    float toHero = hero.x - d.x;
                    d.vx = Mathf.MoveTowards(d.vx, Mathf.Sign(toHero) * 16f, 70f * dt);
                    d.y = Mathf.MoveTowards(d.y, hero.y + .45f, 8f * dt);
                }
                d.x += d.vx * dt;
                foreach (var e in enemies)
                    if (e.alive && e.awake && d.Hits(e)) { KillFlying(e, d.vx > 0 ? 1 : -1); d.back = true; }
                if (boss != null && boss.alive && boss.awake && boss.hurt <= 0 && d.Hits(boss)) { DamageBoss(1); d.back = true; }
                if (d.back && Mathf.Abs(hero.x - d.x) < .6f || d.t > 1.6f) { Destroy(d.view.gameObject); discs.RemoveAt(i); }
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
                    Destroy(c.view.gameObject);
                    coinItems.RemoveAt(i);
                }
            }
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                var k = pickups[i];
                k.Step(dt);
                if (k.y < -3) { Destroy(k.view.gameObject); pickups.RemoveAt(i); continue; }
                if (k.rise <= 0 && hero.Overlaps(k))
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
            int want = type == "shoot" ? 2 : 1;
            Sound.Play("power");
            if (app.run.power >= want) return;
            ApplyPower(want);
            freeze = .5f; growFlash = .5f;
        }

        void ApplyPower(int p)
        {
            app.run.power = p;
            hero.SetBig(p > 0);
            hero.shooter = p == 2;
        }

        void StepCells()
        {
            foreach (var s in springs)
                if (hero.vy <= 0 && Mathf.Abs(hero.x - (s.c + .5f)) < .6f && hero.y < s.r + .6f && hero.y >= s.r - .1f)
                {
                    hero.Launch(22f); s.out_ = .25f; Sound.Play("spring");
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
            Sound.Play("coin", .7f);
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
                    if (hero.big)
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
                if (b.kind == BlockKind.HiddenCoin || b.kind == BlockKind.HiddenLife) { grid.Reveal(c, r); app.run.secrets++; }
                if (b.kind == BlockKind.Grow) Spawn(app.run.power == 0 ? "grow" : "shoot", c, r);
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

        void Spawn(string type, int c, int r)
        {
            string sprite = type == "grow" ? "gem_blue" : type == "shoot" ? "gem_red" : "heart";
            var v = Art.Spawn("Pickup", root, Art.Center(sprite), 1);
            pickups.Add(new Pickup { grid = grid, x = c + .5f, y = r, w = .8f, h = .8f, view = v, type = type });
            Sound.Play("magic");
        }

        void Hurt()
        {
            if (hero.invuln > 0 || phase != Phase.Play) return;
            hits++;
            if (god) { hero.invuln = .3f; return; }
            if (hero.big)
            {
                ApplyPower(0);
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
            phase = Phase.Dying; phaseT = 0; deaths++;
            hero.dead = true; hero.vy = hero.y < 0 ? 17 : 13; hero.vx = 0;
            freeze = .25f;
            app.run.power = 0;
            Sound.Music(null); Sound.Play("lose");
        }

        void AfterDeath()
        {
            app.run.lives--;
            if (app.run.lives <= 0) { app.GameOver(); return; }
            StartLevel(levelIndex, true);
        }

        void Win()
        {
            phase = Phase.Won; phaseT = 0;
            app.run.score += 1000 + (hero.big ? 500 : 0);
            Sound.Music(null); Sound.Play("win");
            app.hud.Banner(Rtl.V($"השלב הושלם!\nניקוד {app.run.score}"));
        }

        void AddFx(Sprite s, Vector2 at, Vector2 vel, float life, float gravity, int order, float scale = 1)
        {
            var v = Art.Spawn("Fx", root, s, order);
            v.transform.localScale = new Vector3(scale, scale, 1);
            fx.Add(new Fx { view = v, at = at, vel = vel, life = life, gravity = gravity });
        }

        void Draw()
        {
            float dt = bot != null ? 1f / 60 : Time.unscaledDeltaTime;
            shake = Mathf.Max(0, shake - dt);
            float wobble = shake > 0 ? Mathf.Sin(simT * 70) * .1f * shake / .3f : 0;
            cam.transform.position = new Vector3(camX, HalfH + wobble, -10);

            // The sky drifts at a quarter of the camera speed and wraps
            const float w = 15f;
            float baseX = camX - HalfW - Mathf.Repeat((camX - HalfW) * .75f, w) - .01f;
            for (int i = 0; i < sky.Count; i++) sky[i].transform.position = new Vector3(baseX + i * w, 0, 0);

            DrawHero(dt);
            foreach (var e in enemies) e.Draw();
            boss?.Draw();
            foreach (var k in pickups) k.Draw(simT);
            foreach (var p in grid.Platforms) p.view.position = new Vector3(p.x, p.y, 0);
            foreach (var d in discs) { d.view.transform.position = new Vector3(d.x, d.y, 0); d.view.transform.Rotate(0, 0, -1100 * dt); }

            var coinSprite = Art.Center((int)(simT * 5) % 2 == 0 ? "coin_gold" : "coin_gold_side");
            foreach (var c in coinItems) c.view.sprite = coinSprite;
            foreach (var b in blocks)
                b.view.transform.position = new Vector3(b.c, b.r + Mathf.Sin(b.bump / .16f * Mathf.PI) * .28f, 0);
            foreach (var s in springs) s.view.sprite = Art.Tile(s.out_ > 0 ? "spring_out" : "spring");
            var torch = Art.Tile((int)(simT * 7) % 2 == 0 ? "torch_on_a" : "torch_on_b");
            foreach (var t in torches) t.sprite = torch;

            for (int i = fx.Count - 1; i >= 0; i--)
            {
                var f = fx[i];
                f.life -= dt;
                if (f.life <= 0) { Destroy(f.view.gameObject); fx.RemoveAt(i); continue; }
                f.vel.y -= f.gravity * dt; f.at += f.vel * dt;
                f.view.transform.position = f.at;
            }
            if (hero.skidding && hero.grounded && (skidDust -= dt) <= 0) { skidDust = .08f; Sound.Play("bump", .12f); }
            app.hud.Stats(app.run.score, app.run.coins, app.run.lives);
        }

        void DrawHero(float dt)
        {
            float speed = Mathf.Abs(hero.vx);
            heroAnim += dt * (3f + speed * 1.6f);
            heroThrow = Mathf.Max(0, heroThrow - dt);

            string pose;
            if (hero.dead) pose = "hurt";
            else if (phase == Phase.Won) pose = "cheer1";
            else if (heroThrow > 0) pose = "action1";
            else if (!hero.grounded) pose = hero.vy > 0 ? "jump" : "fall";
            else if (hero.skidding) pose = "skid";
            else if (speed < .2f) pose = "stand";
            else pose = (int)heroAnim % 2 == 0 ? "walk1" : "walk2";

            growFlash = Mathf.Max(0, growFlash - dt);
            char form = app.run.power == 2 ? 'p' : app.run.power == 1 ? 'b' : 's';
            if (growFlash > 0 && (int)(growFlash * 16) % 2 == 0) form = form == 's' ? 'b' : 's';
            heroView.sprite = Art.Hero(form, pose);

            heroView.flipX = hero.facing < 0;
            squash = Mathf.MoveTowards(squash, 0, dt * 1.6f);
            float sy = 1 + squash, sx = 1 - squash * .7f;
            float gone = phase == Phase.Won ? 1 - heroGone : 1;
            heroView.transform.localScale = new Vector3(sx * gone, sy * gone, 1);
            heroView.transform.position = new Vector3(hero.x, hero.y, 0);
            heroView.enabled = hero.invuln <= 0 || (int)(simT * 14) % 2 == 0;
        }
    }
}
