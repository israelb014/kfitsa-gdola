using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Kfitsa
{
    /// Plays the real game with the routes the reachability search finds (-playtest), so every promise the
    /// search makes is proved in the game itself. It presses buttons only, through the normal simulation.
    public class PlayTest
    {
        readonly App app; readonly StringWriter log = new StringWriter(); readonly string dir;
        int fails;
        Game G => app.game;

        PlayTest(App a, string d) { app = a; dir = d; }

        public static void Run(App app, string dir, string only)
        {
            Directory.CreateDirectory(dir);
            var t = new PlayTest(app, dir);
            app.game.skipIntro = true;
            foreach (var def in Levels.All)
                if (only == null || def.file == only) t.Level(def);
            t.Note(t.fails == 0 ? "ALL PLAYTESTS PASS" : t.fails + " PLAYTESTS FAILED");
        }

        void Note(string s) { log.WriteLine(s); Console.WriteLine(s); File.WriteAllText(Path.Combine(dir, "playtest.txt"), log.ToString()); }
        void Check(string what, bool ok, string detail = "") { if (!ok) fails++; Note((ok ? "PASS " : "FAIL ") + what + (detail == "" ? "" : "  (" + detail + ")")); }

        void Fresh(int level, int power, bool enemies)
        {
            app.run = new RunState { lives = 99, power = power };
            G.god = !enemies ? false : true;
            G.tidy = !enemies;
            G.noScroll = !enemies;      // v2.8: routes found without the moving screen are replayed without it
            G.StartLevel(level);
            if (!enemies) G.RemoveEnemies();
        }

        void Level(LevelDef def)
        {
            int index = Array.IndexOf(Levels.All, def);
            Note("--- " + def.file + " " + def.Code);
            bool stormy = Grid2.Parse(Levels.Text(def.file)).Storm != null;
            System.Collections.Generic.List<Pad> trialRoute = null; Goal trialGoal = null;
            foreach (var (big, calm) in new[] { (false, false), (true, false), (false, true), (true, true) })
            {
                if (calm && !stormy) continue;
                G.windless = calm;
                string form = (big ? "big" : "small") + (calm ? " without wind" : "");
                var s = Reach.Start(def, big, false, false, calm: calm);
                s.seconds = 120;
                s.stopAfterHits = s.goals.Count(q => q.kind != "coin");
                s.Run();
                foreach (var q in s.goals)
                {
                    if (q.kind != "gate" && q.kind != "star" && q.kind != "box" && q.kind != "secret" && q.kind != "door" && q.kind != "flag" && q.kind != "shelter") continue;
                    Fresh(index, big ? 1 : 0, false);
                    bool done = q.hit && Replay(s.Route(q), q);
                    if (!big && !calm && q.kind == "gate" && q.hit) { trialRoute = s.Route(q); trialGoal = q; }
                    if (!q.hit && def.file != null)
                    {
                        // The wide search ran out of time; the far end gets its own search from the flag, as in the validator
                        var again = Reach.Start(def, big, false, true, calm: calm);
                        again.seconds = 120;
                        foreach (var o in again.goals) if (o.ToString() != q.ToString()) o.hit = true;
                        again.stopAfterHits = 1; again.Run();
                        var same = again.goals.Find(o => o.ToString() == q.ToString() && o.node >= 0);
                        if (same != null) { G.RestartAtFlag(); done = Replay(again.Route(same), same); Check($"{def.file} {form}: {q} is reached in the real game, from the flag", done, $"{G.levelTime:F1}s"); continue; }
                    }
                    Check($"{def.file} {form}: {q} is reached in the real game", done, q.hit ? $"{G.levelTime:F1}s" : "no route");
                }
            }
            G.windless = false;
            Room(def, index);
            Flag(def, index);
            Full(def, index);
            app.save.difficulty = 1; Full(def, index, true); app.save.difficulty = 0;
            Ride(def, index);
            // v2.3: the time trial of the level, on the small hero's way to the gate
            if (!def.boss && !def.Bonus && trialRoute != null)
            {
                app.save.trialTime[index] = 0; app.save.ghost[index] = null;
                app.run = new RunState { lives = 1 }; G.god = false; G.tidy = true; G.trial = true; G.StartLevel(index);
                bool none = G.EnemyCount == 0;
                bool done = Replay(trialRoute, trialGoal);
                for (int i = 0; i < 240 && !G.Won; i++) G.TestStep(1, default);
                Check($"{def.file} time trial: no enemies, finished, best time and ghost saved", none && G.Won && app.save.trialTime[index] > 0 && G.record.Count > 0,
                    $"{G.LastTime:F1}s, ghost {G.record.Count} frames");
                G.trial = false;
            }
        }

        System.Collections.Generic.List<(int tick, float x, float y)> marks;

        /// Plays a route, and asks the game itself whether the goal happened.
        bool Replay(System.Collections.Generic.List<Pad> route, Goal q)
        {
            int deaths = G.deaths;
            int step = 0;
            foreach (var p in route)
            {
                float x = G.hero.x, y = G.hero.y;
                G.TestStep(1, p); step++;
                if (marks != null)
                    foreach (var m in marks)
                        if (m.tick == step && (Mathf.Abs(m.x - G.hero.x) > .05f || Mathf.Abs(m.y - G.hero.y) > .05f))
                        {
                            Note($"     left the route at tick {step}: game {G.hero.x:F2},{G.hero.y:F2} vx {G.hero.vx:F1} on {G.hero.on != null}, search {m.x:F2},{m.y:F2}, was at {x:F2},{y:F2}");
                            marks = null; break;
                        }
                if (G.deaths != deaths) { Note($"     died at tick {step} of {route.Count}, from {x:F2},{y:F2} vy {G.hero.vy:F1}"); return false; }
            }
            var h = G.hero; var g = G.grid;
            if (q.kind == "gate" && !g.AtGate(h)) Note($"     ended at {h.x:F2},{h.y:F2} grounded {h.grounded}, wanted {g.GateX:F1},{g.GateY:F1}, tick {step}");
            switch (q.kind)
            {
                case "gate": return g.AtGate(h) || G.Won;
                case "star": return (G.GotStars & 1 << StarId(g, q)) != 0;
                case "door": return Mathf.Abs(h.x - g.Door.x) < .9f;
                case "flag": return G.CheckpointReached;
                case "shelter": return World.Sheltered(g, h.x, h.y) || Mathf.Abs(h.x - q.x) < 1.5f && Mathf.Abs(h.y - q.y) < 2;
                default: return G.blocks.Exists(b => b.c == q.c && b.r == q.r && b.used);
            }
        }

        static int StarId(Grid2 g, Goal q) => g.Stars.FindIndex(p => Mathf.Abs(p.x - q.x) < .1f && Mathf.Abs(p.y - q.y) < .1f);

        /// Finds a way from where the hero stands now to the first goal the test wants, and plays it.
        bool Drive(LevelDef def, Func<Goal, bool> want, bool room, double seconds = 40, bool walk = false)
        {
            for (int i = 0; i < 240 && G.Running && !G.hero.grounded; i++) G.TestStep(1, default);
            var s = Reach.From(def, G.hero, G.world, room);
            s.seconds = seconds; s.walkOnly = walk;
            foreach (var q in s.goals) if (!want(q)) q.hit = true;
            s.stopAfterHits = 1;
            s.Run();
            var goal = s.goals.Find(q => q.node >= 0);
            if (goal == null) return false;
            int deaths = G.deaths;
            var check = new System.Collections.Generic.Dictionary<int, Vector2>();
            foreach (var m in s.Marks(goal)) check[m.tick] = new Vector2(m.x, m.y);
            int step = 0; bool off = false;
            foreach (var p in s.Route(goal))
            {
                G.TestStep(1, p); step++;
                if (G.deaths != deaths || !G.Running || G.Won) break;
                // An enemy knocked the hero off the route: stop here, so the caller can plan a new one
                if (check.TryGetValue(step, out var at) && (at - new Vector2(G.hero.x, G.hero.y)).sqrMagnitude > .04f)
                {
                    if (!off) Note($"     off the route at step {step}: planned {at.x:F2},{at.y:F2} game {G.hero.x:F2},{G.hero.y:F2} vx {G.hero.vx:F1} vy {G.hero.vy:F1} power {app.run.power}");
                    off = true;
                }
                if (off)
                {
                    // Knocked into the air: let go of the route and brake, as a player would
                    for (int i = 0; i < 240 && G.Running && !G.hero.grounded && G.deaths == deaths; i++) G.TestStep(1, new Pad { x = -Mathf.Sign(G.hero.vx) });
                    break;
                }
            }
            return G.deaths == deaths;
        }

        /// Walks back to the door if the route ended at a run, then presses down.
        void PressDoor()
        {
            var g = G.grid; var at = G.inRoom ? g.RoomDoor : g.Door;
            for (int i = 0; i < 900; i++)
            {
                float d = at.x - G.hero.x;
                if (G.hero.grounded && Mathf.Abs(d) < .4f && Mathf.Abs(G.hero.vx) < 1) break;
                G.TestStep(1, new Pad { x = Mathf.Abs(d) < .3f ? 0 : Mathf.Sign(d) });
            }
            G.TestStep(1, default);
            G.TestStep(1, new Pad { down = true });
            G.TestStep(80, default);
        }

        void Room(LevelDef def, int index)
        {
            Fresh(index, 0, false);
            var s = Reach.Start(def, false, false, false);
            foreach (var q in s.goals) if (q.kind != "door") q.hit = true;
            s.stopAfterHits = 1; s.seconds = 60; s.Run();
            var door = s.goals.Find(q => q.kind == "door");
            bool at = door.node >= 0 && Replay(s.Route(door), door);
            PressDoor();
            Check($"{def.file}: the door opens the bonus room", at && G.inRoom);
            if (!G.inRoom) return;
            var g = G.grid; float clock = G.levelTime; string mode = g.RoomMode; int star = g.Stars.FindIndex(p => p.x >= g.MainW);
            if (g.RoomCoins)
                for (int i = 0; i < 40 && G.RoomCoinsLeft > 0; i++) Drive(def, q => q.kind == "coin" && G.RoomCoinAt(q.x, q.y), true, 10);
            if (g.RoomLamps)
                for (int i = 0; i < 6 && !G.RoomSolved; i++) Drive(def, q => q.kind == "lamp" && !G.LampLit(q.x, q.y), true, 10);
            if (g.RoomStomp)
            {
                app.run.power = 2; G.ApplyPowerForTest(2);
                for (int i = 0; i < 2400 && !G.RoomSolved; i++)
                {
                    var e = G.enemies.Find(x => x.alive && x.x >= g.MainW);
                    int dir = e == null ? 0 : e.x > G.hero.x ? 1 : -1;
                    G.TestStep(1, new Pad { x = dir, run = true, runPressed = i % 40 == 0 });
                }
            }
            if (mode == "sneak") { for (int i = 0; i < 3 && (G.GotStars >> star & 1) == 0 && G.inRoom; i++) Drive(def, q => q.kind == "star", true, 30, true); }     // walking only, so the mouse sleeps on
            else if (mode == "puzzle" || mode == "pool") Drive(def, q => q.kind == "star", true, 60);
            else Drive(def, q => q.kind == "star", true, 20);
            for (int i = 0; i < 240; i++) if ((G.GotStars >> star & 1) == 0) G.TestStep(1, default);
            float took = G.levelTime - clock;
            Check($"{def.file}: bonus room ({mode}) is solved and pays its star", (G.GotStars >> star & 1) != 0, $"{took:F1}s of {g.RoomSeconds:F0}s");
            bool back = Drive(def, q => q.kind == "room exit", true, 20);
            PressDoor();
            Check($"{def.file}: the way out of the room works", !G.inRoom);
        }

        void Flag(LevelDef def, int index)
        {
            foreach (bool big in new[] { false, true })
            {
                Fresh(index, big ? 1 : 0, false);
                var s = Reach.Start(def, big, false, true);
                foreach (var q in s.goals) if (q.kind != "gate") q.hit = true;
                s.stopAfterHits = 1; s.seconds = 60; s.Run();
                var gate = s.goals.Find(q => q.kind == "gate");
                G.RestartAtFlag();
                marks = gate.node >= 0 ? s.Marks(gate) : null;
                bool ok = gate.node >= 0 && Replay(s.Route(gate), gate);
                marks = null;
                Check($"{def.file} {(big ? "big" : "small")}: finish from the checkpoint in the real game", ok, $"{G.levelTime:F1}s");
            }
        }

        /// The whole level with enemies on. They cannot hurt the bot, but they bump it off its route, so it replans.
        void Full(LevelDef def, int index, bool easy = false)
        {
            Fresh(index, 0, true);
            int wallet0 = app.save.wallet;
            if (easy)
            {
                int total = G.grid.Enemies.FindAll(e => e.at.x < G.grid.MainW).Count, room = G.grid.Enemies.Count - total;
                Check($"{def.file} easy: half the enemies and two more flags", (total == 0 || G.EnemyCount - room <= (total + 1) / 2) && G.FlagCount == G.grid.FlagSpots(false).Count + G.grid.EasyFlags.Count,
                    $"{G.EnemyCount - room} of {total}, {G.FlagCount} flags");
            }
            int plans = 0;
            while (G.Running && !G.Won && G.levelTime < 400 && plans < (G.grid.ScrollSpeed > 0 ? 80 : 10))     // the moving screen bumps the bot off its plan often
            {
                plans++;
                if (G.boss != null && G.boss.alive && G.hero.x > G.boss.x - 24) { FightBoss(); continue; }
                int falls = G.deaths;
                bool found = Drive(def, q => q.kind == "gate", false, 60);
                if (G.deaths != falls) Note($"     fell at {G.lastFall.x:F1},{G.lastFall.y:F1} clock {G.levelTime:F1}");
                // After a fall, set off a little later, so the same enemy is not met at the same moment again
                if (G.deaths != falls) { G.TestStep(280, default); G.TestStep(190 * G.deaths, default); continue; }
                if (!found) Note($"     route {plans}: stopped at {G.hero.x:F1},{G.hero.y:F1} clock {G.world.t:F1}, falls {G.deaths}");
                if (G.grid != null && G.grid.AtGate(G.hero) && G.boss != null && G.boss.alive) { FightBoss(); }
            }
            Check($"{def.file}{(easy ? " easy" : "")}: played start to finish with enemies", G.Won, $"{G.levelTime:F1}s, {G.deaths} falls, {G.hits} hits, {plans} routes, {app.save.wallet - wallet0} coins");
        }

        /// v2.7: the level's animal is let out of its box, ridden, and taken to the finish.
        void Ride(LevelDef def, int index)
        {
            var g0 = Grid2.Parse(Levels.Text(def.file));
            if (!g0.Blocks.Exists(b => b.kind == BlockKind.Animal)) return;
            var box = g0.Blocks.Find(b => b.kind == BlockKind.Animal);
            Fresh(index, 0, false); G.tidy = false; G.god = true;
            Drive(def, q => q.kind == "box" && q.c == box.c && q.r == box.r, false, 40);
            G.TestStep(120, default);
            Drive(def, q => q.kind == "power-up" && q.c == box.c && q.r == box.r, false, 40);
            for (int i = 0; i < 90 && G.Riding < 0; i++) G.TestStep(1, default);
            int animal = G.Riding, plans = 0;
            while (G.Running && !G.Won && plans < 12) { plans++; Drive(def, q => q.kind == "gate", false, 60); }
            Check($"{def.file}: its animal comes out of the box, is ridden, and reaches the finish", animal >= 0 && G.Won,
                $"{(animal >= 0 ? Game.Animals[animal] : "no animal")}, still riding {G.Riding >= 0}, {G.levelTime:F1}s, {plans} routes");
            G.god = false;
        }

        void FightBoss()
        {
            app.run.power = 2; G.ApplyPowerForTest(2);
            for (int i = 0; i < 12000 && G.boss.alive; i++)
            {
                var b = G.boss;
                float dx = b.x - G.hero.x;
                if (b.color >= 0)
                {
                    // The king in colours: first the switch of his colour, then on top of him
                    bool soft = b.Open && b.hurt <= 0;
                    float target = b.x;
                    if (!b.Open)
                    {
                        // The nearest switch of his colour: the level may have another one far back, past water
                        var sw = G.grid.ColorSwitches.Where(c => c.color == b.color).OrderBy(c => Mathf.Abs(c.c + .5f - G.hero.x)).First();
                        target = sw.c + .5f;
                    }
                    float go = target - G.hero.x;
                    G.TestStep(1, new Pad { x = Mathf.Abs(go) > .3f ? Mathf.Sign(go) : 0, run = Mathf.Abs(go) > 3,
                        jumpPressed = soft && G.hero.grounded && Mathf.Abs(dx) < 4, jumpHeld = soft });
                    continue;
                }
                if (b.frost)
                {
                    // Keep away while it throws; when it is stuck, run in and jump on it
                    bool open = b.Open && b.hurt <= 0;
                    float side = dx > 0 ? -1 : 1, target = open ? b.x : b.x + side * 6;
                    float go = target - G.hero.x;
                    G.TestStep(1, new Pad { x = Mathf.Abs(go) > .5f ? Mathf.Sign(go) : 0, run = true, runPressed = open && i % 20 == 0,
                        jumpPressed = open && G.hero.grounded && Mathf.Abs(dx) < 4, jumpHeld = open });
                    continue;
                }
                G.TestStep(1, new Pad { x = Mathf.Abs(dx) > 3 ? Mathf.Sign(dx) : -Mathf.Sign(dx), run = true, runPressed = i % 30 == 0, jumpPressed = i % 70 == 0, jumpHeld = i % 70 < 30 });
            }
        }
    }

    /// Visits every screen once for screenshots (-shots).
    public class ScreenTour : MonoBehaviour
    {
        App app; string dir; int step; float t; bool shot;
        static readonly string[] Names = { "title", "map", "scores", "settings", "credits", "ending", "pause", "l1", "l1room", "l2", "l3", "l3top", "l4", "l5", "l5flood", "l6", "l6belts", "l7", "l7arena", "power", "controls", "difficulty", "intro",
            "flag_storm", "frost", "dust_shield", "boss_warn", "boss_shadow", "hand", "map_walk", "map_full", "w3l1", "w3l1_ladder", "w3l1_rope", "world_cleared", "w3l2_clouds", "w3l2_worm", "w3l3_sleepers", "w3l3_squeak", "boss2_throw", "boss2_stuck", "wardrobe", "outfit_game", "outfit_map", "achievements", "toast", "win_time", "trial_map", "trial_ghost", "w4l1", "w4l2", "w4l3", "boss3", "map_hidden", "boss_intro", "help_box", "shop", "wardrobe_shop", "ride_ram", "ride_camel", "ride_penguin", "ride_bird", "bonus_scroll", "bonus_sea", "bonus_cart", "map_bonus", "h_hanukkah", "h_purim", "h_pesach", "h_sukkot", "h_roshhashana", "map_holidays" };
        Func<bool> freezeWhen;     // stops the game on the exact frame worth capturing

        public void Begin(App a, string captureDir) { app = a; dir = captureDir; Directory.CreateDirectory(dir); app.ShowTitle(); t = 1.5f; }

        void Update()
        {
            if (freezeWhen != null && app.game.Running && freezeWhen()) { app.game.paused = true; freezeWhen = null; }
            t -= Time.unscaledDeltaTime;
            if (t > 0) return;
            t = 1f;
            if (step >= Names.Length) { Application.Quit(); return; }
            if (!shot) { ScreenCapture.CaptureScreenshot(Path.Combine(dir, "screen_" + Names[step] + ".png")); shot = true; t = .2f; return; }
            shot = false;
            step++;
            var G = app.game;
            void At(int level, float x, float y, int power = 0)
            {
                if (G.Running) G.Unload();
                freezeWhen = null;
                G.skipIntro = true; G.skipBossIntro = true; app.run = new RunState { power = power };
                app.Enter(level);
                G.paused = false; G.god = true;
                G.hero.x = x; G.hero.y = y; G.TestStep(1, default);
                G.SnapCamera();
            }
            switch (step)
            {
                case 29:
                {
                    if (G.Running) G.Unload();
                    app.hud.Show(false); app.run = new RunState { power = 1 }; app.save.stars[3] = 5; app.save.found[0] = 1; app.save.found[2] = 1; app.save.found[3] = 1;
                    app.MapTestFinished(3); app.mapHold = true; app.MapTestTick(.85f);
                    break;
                }
                case 31: app.mapHold = false; At(7, 30, 2, 1); break;
                case 32: At(7, 39.4f, 2, 1); G.paused = true; G.TestStep(1, new Pad { up = true }); G.TestStep(80, new Pad { up = true }); G.SnapCamera(); G.DrawNow(); break;
                case 33: At(7, 146.5f, 15, 2); G.paused = true; G.TestStep(1, new Pad { up = true }); G.TestStep(20, new Pad { up = true }); G.SnapCamera(); G.DrawNow(); break;
                case 34: if (G.Running) G.Unload(); app.hud.Show(false); app.WorldClearedForTour(); break;
                case 37: At(9, 117, 2, 1); G.paused = true; G.TestStep(60, default); G.SnapCamera(); G.DrawNow(); break;
                case 38:
                {
                    At(9, 20, 2, 1); G.paused = true; var mouse = G.enemies.Find(x => x.kind == 'z');
                    for (int i = 0; i < 300 && mouse != null && !mouse.scared; i++) G.TestStep(1, new Pad { x = 1, run = true });
                    G.TestStep(14, default); G.SnapCamera(); G.DrawNow();
                    break;
                }
                case 49: app.hud.Show(true); At(11, 19, 2, 1); G.paused = true; G.TestStep(30, new Pad { x = 1 }); G.SnapCamera(); G.DrawNow(); app.hud.Hint(0, ""); break;
                case 50: At(12, 37.5f, 2, 1); G.paused = true; G.TestStep(240, default); G.SnapCamera(); G.DrawNow(); break;
                case 51:
                {
                    At(13, 52.5f, 2, 1); G.paused = true; G.TestStep(10, default);
                    var f = G.enemies.Find(x => x.kind == 'N' && x.x > 59 && x.x < 61);
                    for (int i = 0; i < 900 && f != null && !(f.Out && f.y > 4); i++) G.TestStep(1, default);
                    G.SnapCamera(); G.DrawNow();
                    break;
                }
                case 52:
                {
                    At(14, 133.5f, 2, 1); G.paused = true; G.boss.awake = true; app.hud.BossBar(1, Rtl.V(Levels.All[14].name));
                    G.boss.hp = 6; G.boss.color = 1; G.TestStep(60, default); G.SnapCamera(); G.DrawNow();
                    break;
                }
                case 53:
                {
                    if (G.Running) G.Unload();
                    app.hud.Show(false); for (int i = 0; i < app.save.stars.Length; i++) app.save.stars[i] = i < App.MainCount ? 7 : i == App.MainCount ? 5 : 0;
                    app.save.unlocked = App.MainCount + 1; app.mapHold = false; app.ShowMap(); app.MapPageForTest();
                    break;
                }
                case 54:
                {
                    app.hud.Show(true); At(10, 140, 2, 1); G.paused = true; G.skipBossIntro = false;
                    G.hero.x = G.boss.x - 15.9f; G.hero.y = G.boss.y + .5f; G.TestStep(1, default); G.TestStep(160, default); G.DrawNow();
                    break;
                }
                case 55:
                {
                    At(0, 20, 2); G.paused = true; app.save.helper = true; app.run.lives = 9;
                    for (int k = 0; k < 3; k++)
                    {
                        int d = G.deaths; G.hero.x = 26; G.hero.y = 2; G.TestStep(1, default); G.FallForTest();
                        for (int i = 0; i < 900 && !(G.deaths > d && !G.hero.dead); i++) G.TestStep(1, default);
                    }
                    G.TestStep(30, default); G.SnapCamera(); G.DrawNow();
                    break;
                }
                case 56:
                {
                    if (G.Running) G.Unload();
                    app.hud.Show(false); app.save.wallet = 137; app.save.shopOwned = Outfit.Owned = 0b001001; app.save.boostShield = true; app.ShowShop(app.ShowTitle);
                    break;
                }
                case 57:
                {
                    app.save.boostShield = false; Outfit.Hat = 6; Outfit.Pant = 6; for (int i = 0; i < app.save.stars.Length; i++) app.save.stars[i] = i < 5 ? 7 : 0;
                    app.ShowWardrobe(app.ShowTitle);
                    break;
                }
                case 58: case 59: case 60: case 61:
                {
                    app.hud.Show(true); app.hud.Hint(0, "");
                    int k = step - 58; string[] file = { "w1l2", "w2l3", "w3l2", "w4l2" }; float[] x = { 20, 49, 50, 30 }, y = { 2, 2, 3, 7 };
                    At(Array.FindIndex(Levels.All, l => l.file == file[k]), x[k], y[k]); G.paused = true; G.MountForTest(k); G.TestStep(40, default);
                    if (k == 1) G.TestStep(30, default);
                    if (k == 2) G.TestStep(70, new Pad { x = 1, run = true });
                    if (k == 3) { G.hero.y = 9; G.hero.vy = 0; G.hero.grounded = false; G.TestStep(30, new Pad { x = 1, jumpHeld = true }); }
                    if (k == 0) G.TestStep(30, new Pad { x = 1 });
                    G.SnapCamera(); G.DrawNow(); app.hud.Hint(0, "");
                    break;
                }
                case 62: case 63: case 64:
                {
                    app.hud.Show(true); Outfit.Hat = Outfit.Pant = 0;
                    string[] file = { "b1", "b2", "b3" }; float[] x = { 46, 40, 112 }, y = { 2, 6, 2 };
                    At(Array.FindIndex(Levels.All, l => l.file == file[step - 62]), x[step - 62], y[step - 62]); G.paused = true;
                    if (step == 62) { G.ScrollLeft = 38; G.TestStep(60, new Pad { x = 1 }); }
                    if (step == 63) G.TestStep(20, new Pad { x = 1, jumpPressed = true });
                    if (step == 64) G.TestStep(20, default);
                    G.SnapCamera(); G.TestStep(1, default); G.DrawNow(); app.hud.Hint(0, "");
                    break;
                }
                case 65:
                {
                    if (G.Running) G.Unload();
                    app.hud.Show(false); app.mapHold = false; app.save.unlocked = 9;
                    for (int i = 0; i < 10; i++) { app.save.best[i] = 900; app.save.stars[i] = 7; }
                    int bb = Array.FindIndex(Levels.All, l => l.file == "b2"); app.save.best[Array.FindIndex(Levels.All, l => l.file == "b1")] = 500; app.save.stars[bb - 1] = 3;
                    app.MapTestFinished(3); app.MapTestTick(3); app.MapBonusForTest(1); app.MapTestTick(.2f);
                    break;
                }
                case 66: case 67: case 68: case 69: case 70:
                {
                    app.hud.Show(true); int k = step - 66; float[] x = { 46, 22, 30, 36, 26 }, y = { 2, 2, 2, 3, 2 };
                    At(Levels.HolidayLevel(k), x[k], y[k]); G.paused = true; G.TestStep(k == 2 ? 300 : 30, default); G.SnapCamera(); G.DrawNow(); app.hud.Hint(0, "");
                    break;
                }
                case 71:
                {
                    if (G.Running) G.Unload();
                    app.hud.Show(false); app.save.holidays = 0b10011; Holidays.Today = new DateTime(2026, 12, 8); app.ShowMap(); app.MapHolidaysForTest(); app.MapTestTick(.3f);
                    Holidays.Today = null;
                    break;
                }
                case 47:
                {
                    if (G.Running) G.Unload();
                    app.save.best[2] = 900; app.save.trialTime[2] = 287; app.hud.Show(false); app.run = new RunState(); app.mapHold = true;
                    app.MapTestFinished(1); app.MapTestTick(3);
                    break;
                }
                case 48:
                {
                    app.mapHold = false; app.save.best[0] = 900; app.save.trialTime[0] = 0; app.save.ghost[0] = null;
                    G.skipIntro = true; app.EnterTrial(0); G.god = true;
                    G.TestStep(420, new Pad { x = 1, run = true }); G.WinForTest(); app.TrialDone();
                    app.EnterTrial(0); G.paused = true; G.TestStep(300, new Pad { x = 1 }); G.SnapCamera(); G.DrawNow(); app.hud.Hint(0, "");
                    break;
                }
                case 44:
                {
                    if (G.Running) G.Unload();
                    Outfit.Hat = Outfit.Pant = 0; app.hud.Show(false); app.save.achievements = 0b1010_0101_1001_0111; app.ShowAchievements(app.ShowTitle);
                    break;
                }
                case 45: app.save.achievements = 0; app.hud.Show(true); At(0, 20, 2, 1); G.paused = true; Achievements.Unlock(Achievements.Id.Chain); G.DrawNow(); break;
                case 46:
                {
                    app.hud.Show(true); At(0, 20, 2, 1); G.paused = true; app.save.bestTime[0] = 341; G.TestStep(90, default); G.WinForTest(); G.TestStep(20, default); G.DrawNow();
                    break;
                }
                case 41:
                {
                    if (G.Running) G.Unload();
                    app.hud.Show(false); for (int i = 0; i < app.save.stars.Length; i++) app.save.stars[i] = i < 7 ? 7 : 0;
                    Outfit.Hat = 5; Outfit.Pant = 1; app.ShowWardrobe(app.ShowTitle);
                    break;
                }
                case 42: Outfit.Hat = 1; Outfit.Pant = 2; app.hud.Show(true); At(0, 20, 2, 1); G.paused = true; G.TestStep(1, new Pad { x = 1 }); G.DrawNow(); break;
                case 43:
                {
                    if (G.Running) G.Unload();
                    Outfit.Hat = 2; Outfit.Pant = 4; app.hud.Show(false); app.run = new RunState { power = 0 }; app.mapHold = false; app.ShowMap();
                    break;
                }
                case 39:
                case 40:
                {
                    At(10, 157, 2, 1); G.paused = true;
                    for (int i = 0; i < 900 && G.SnowBallCount == 0; i++) G.TestStep(1, default);
                    G.TestStep(step == 39 ? 14 : 40, default); G.SnapCamera(); G.DrawNow();
                    break;
                }
                case 35: app.hud.Show(true); At(8, 135, 10, 1); G.paused = true; G.TestStep(90, default); G.SnapCamera(); G.DrawNow(); break;
                case 36:
                {
                    At(8, 93, 3, 1); G.paused = true;
                    var w = G.enemies.Find(x => x.kind == 'm' && x.x > 94);
                    for (int i = 0; i < 600 && w != null && !(w.Out && w.Rise >= 1); i++) G.TestStep(1, default);
                    G.SnapCamera(); G.DrawNow();
                    break;
                }
                case 30: app.mapHold = false; app.save.unlocked = Levels.All.Length - 1; for (int i = 0; i < Levels.All.Length; i++) { app.save.stars[i] = i % 3 == 0 ? 7 : 3; app.save.found[i] = 7; } app.run = new RunState { power = 3 }; app.ShowMap(); break;
                case 1: app.save.unlocked = 4; app.save.stars[0] = 7; app.save.stars[1] = 3; app.save.stars[2] = 1; app.run = new RunState(); app.ShowMap(); break;
                case 2: app.save.AddScore(12400, "עד שלב 1-2"); app.save.AddScore(8300, "עד שלב 1-1"); app.ShowScores(app.ShowTitle); break;
                case 3: app.ShowSettings(app.ShowTitle); break;
                case 4: app.ShowCreditsForTour(); break;
                case 5: app.ShowEndingForTour(); break;
                case 6: app.Enter(0); app.ShowPause(); break;
                case 7: G.paused = false; app.ResumeForTour(); At(0, 30, 2); break;
                case 8: At(0, 112.5f, 2); G.UseDoor(); G.SnapCamera(); break;
                case 9: At(1, 42, 6); break;
                case 10: At(2, 10, 15); break;
                case 11: At(2, 22, 58); break;
                case 12: At(3, 34, 2); break;
                case 13: At(4, 34, 2); G.world.t = 7.5f; break;
                case 14: At(4, 168, 12); G.world.t = 20; G.world.floodAt = 8; break;
                case 15: At(5, 22, 2); break;
                case 16: At(5, 151, 4, 1); break;
                case 17: At(6, 40, 3); G.world.t = 20; G.world.chaseAt = 14.2f; break;
                case 18: At(6, 168, 2, 2); G.TestStep(30, new Pad { x = 1, run = true }); G.TestStep(1, new Pad { runPressed = true }); G.TestStep(18, default); break;
                case 19: At(1, 11, 2, 1); break;
                case 20: G.Unload(); app.ShowTitle(); if (TouchUi.Available) app.ShowControls(app.ShowTitle); break;
                case 21: if (G.Running) G.Unload(); app.hud.Show(false); app.ShowDifficulty(false); break;
                case 22: app.ShowIntro(); break;
                // Version 1.4
                case 23: At(3, 90, 2); G.world.t = 9.5f; break;
                case 24:
                {
                    At(0, 30, 2, 3);
                    var e = G.TestEnemy('e', 34, 2); G.TestStep(1, new Pad { throwPressed = true }); G.TestStep(40, default);
                    G.hero.x = e.x; G.hero.y = e.y + 1.05f; G.hero.vy = 0; G.TestStep(6, default);
                    break;
                }
                case 25:
                {
                    At(0, 24, 7, 1); app.run.shield = true; G.paused = true; G.hero.vy = -16; G.hero.grounded = false;
                    for (int i = 0; i < 240 && !G.hero.grounded; i++) G.TestStep(1, default);
                    G.TestStep(3, default); G.DrawNow();
                    break;
                }
                case 26:
                {
                    At(6, 173, 2); G.paused = true; G.boss.awake = true; G.boss.wait = .4f;
                    for (int i = 0; i < 240 && !(G.boss.Warning && G.boss.wait < .15f); i++) G.TestStep(1, default);
                    G.DrawNow();
                    break;
                }
                case 27:
                {
                    At(6, 171, 2); G.paused = true; G.boss.awake = true; G.boss.wait = .05f;
                    for (int i = 0; i < 400 && !(!G.boss.grounded && G.boss.vy < -6); i++) G.TestStep(1, default);
                    G.DrawNow();
                    break;
                }
                case 28: if (TouchUi.Available) { At(0, 4, 2); app.save.taught = 0; } break;
            }
        }
    }

    /// Feeds real touch events through the Input System and compares them with the keyboard (-touchtest).
    public class TouchTour : MonoBehaviour
    {
        App app; string dir; Touchscreen ts; readonly StringWriter w = new StringWriter();
        TouchUi Ui => app.hud.Touch;
        Player H => app.game.hero;

        public void Begin(App a, string d) { app = a; dir = d; Directory.CreateDirectory(d); ts = Touchscreen.current; StartCoroutine(Run()); }

        void Note(string s) { w.WriteLine(s); File.WriteAllText(Path.Combine(dir, "touch_test.txt"), w.ToString()); }
        void Check(string what, bool ok, string detail = "") => Note((ok ? "PASS " : "FAIL ") + what + (detail == "" ? "" : "  (" + detail + ")"));
        void Finger(int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 at) =>
            InputSystem.QueueStateEvent(ts, new TouchState { touchId = id, phase = phase, position = at });
        void Down(int id, Vector2 at) => Finger(id, UnityEngine.InputSystem.TouchPhase.Began, at);
        void Move(int id, Vector2 at) => Finger(id, UnityEngine.InputSystem.TouchPhase.Moved, at);
        void Up(int id, Vector2 at) => Finger(id, UnityEngine.InputSystem.TouchPhase.Ended, at);
        static WaitForSecondsRealtime Wait(float s) => new WaitForSecondsRealtime(s);

        IEnumerator Tap(Vector2 at) { Down(9, at); yield return Wait(.1f); Up(9, at); yield return Wait(.25f); }

        float peak;
        IEnumerator Jump(Action press, Action release, float hold)
        {
            float start = H.y; peak = 0;
            press();
            for (float t = 0; t < 1.4f; t += Time.unscaledDeltaTime)
            {
                if (t >= hold && release != null) { release(); release = null; }
                peak = Mathf.Max(peak, H.y - start);
                yield return null;
            }
        }

        IEnumerator Run()
        {
            yield return Wait(1f);
            var mid = new Vector2(Screen.width, Screen.height) / 2;
            Check("controls hidden on the title screen", !Ui.Visible);
            Vector2 first = mid + new Vector2(0, (90 + 0) * app.hud.Scale * 1) ;
            yield return Tap(first);
            Check("new game asks for the difficulty first", app.ScreenName == "Menu", app.ScreenName);
            yield return Tap(mid + new Vector2(0, 140) * app.hud.Scale);
            Check("normal is the first choice and gives 3 lives", app.save.difficulty == 0 && app.run.lives == 3, $"difficulty {app.save.difficulty}, lives {app.run.lives}");
            yield return Tap(mid + new Vector2(0, -300) * app.hud.Scale);
            Check("the intro leads to the map", app.ScreenName == "Map", app.ScreenName);
            Vector2 node = mid + App.NodePos(0) * app.hud.Scale;
            yield return Tap(node);
            if (app.ScreenName == "Map") { Down(9, node); Up(9, node); yield return Wait(.3f); }   // down and up between two frames
            Check("instant tap on the selected level starts it", app.ScreenName == "Playing", app.ScreenName);
            app.game.god = true;
            yield return Wait(2.6f);
            Check("controls visible while playing", Ui.Visible);
            Check("first time: a hand points at the move button", Ui.HandShown && Ui.Teaching == "right" && Vector2.Distance(Ui.HandTip, Ui.Spot("right")) < Ui.ButtonWidth("right") * .45f,
                $"shown {Ui.HandShown}, teaching {Ui.Teaching}, tip {Ui.HandTip}, button {Ui.Spot("right")}");
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "touch_idle.png"));
            yield return Wait(.2f);

            Vector2 jump = Ui.Spot("jump"), run = Ui.Spot("run"), right = Ui.Spot("right"), left = Ui.Spot("left");

            var kb = Keyboard.current;
            yield return Jump(() => InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Space)), () => InputSystem.QueueStateEvent(kb, new KeyboardState()), .08f);
            float keyShort = peak;
            yield return Jump(() => InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Space)), () => InputSystem.QueueStateEvent(kb, new KeyboardState()), .7f);
            float keyLong = peak;
            yield return Jump(() => Down(1, jump), () => Up(1, jump), .08f);
            float touchShort = peak;
            yield return Jump(() => Down(1, jump), () => Up(1, jump), .7f);
            float touchLong = peak;
            Check("short tap jumps lower than a long hold", touchShort < touchLong - 1, $"{touchShort:F2} / {touchLong:F2} tiles");
            Check("short touch jump matches the keyboard", Mathf.Abs(touchShort - keyShort) < .25f, $"touch {touchShort:F2}, keyboard {keyShort:F2}");
            Check("full touch jump matches the keyboard", Mathf.Abs(touchLong - keyLong) < .05f, $"touch {touchLong:F2}, keyboard {keyLong:F2}");

            Down(1, right); yield return Wait(.25f); Down(2, run);
            yield return Wait(1.1f);
            Check("two fingers: move and run reach run speed", Mathf.Abs(H.vx - Tune.RunSpeed) < .2f, $"vx {H.vx:F2}");
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "touch_held.png"));
            float y0 = H.y; Down(3, jump);
            yield return Wait(.25f);
            Check("three fingers: move, run and jump together", H.jumping || H.y > y0 + .5f, $"height {H.y - y0:F2}, vx {H.vx:F2}");
            Check("run speed kept during the jump", H.vx > Tune.RunSpeed - .5f, $"vx {H.vx:F2}");
            Up(3, jump); Up(2, run); yield return Wait(.9f);
            Move(1, left); yield return Wait(.7f);
            Check("sliding a finger across switches direction", H.vx < -1, $"vx {H.vx:F2}");
            Up(1, left); yield return Wait(.8f);
            Check("all fingers up stops the hero", Mathf.Abs(H.vx) < .1f, $"vx {H.vx:F2}");

            Vector2 between = (jump + run) / 2;
            Down(1, between); yield return Wait(.15f);
            var both = GameInput.Read();
            Check("thumb between jump and run presses both", both.jumpHeld && both.run);
            Up(1, between); yield return Wait(1.2f);

            yield return Tap(Ui.Spot("pause"));
            Check("pause button opens the pause menu", app.ScreenName == "Paused", app.ScreenName);
            Check("controls hidden in the pause menu", !Ui.Visible);
            float y1 = H.y;
            Vector2 resume = mid + new Vector2(30, 120 * app.hud.Scale);
            Down(9, resume); yield return Wait(.5f);
            Check("tap on Resume continues", app.ScreenName == "Playing", app.ScreenName);
            Check("the finger left on Resume does not jump", Mathf.Abs(H.y - y1) < .01f, $"moved {H.y - y1:F3}");
            Up(9, resume); yield return Wait(.2f);

            app.Backgrounded(); yield return Wait(.2f);
            Check("going to the background pauses", app.ScreenName == "Paused", app.ScreenName);
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Escape)); yield return Wait(.15f);
            InputSystem.QueueStateEvent(kb, new KeyboardState()); yield return Wait(.2f);
            Check("back (Esc) resumes from the pause menu", app.ScreenName == "Playing", app.ScreenName);
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Escape)); yield return Wait(.15f);
            InputSystem.QueueStateEvent(kb, new KeyboardState()); yield return Wait(.2f);
            Check("back (Esc) pauses during play", app.ScreenName == "Paused", app.ScreenName);
            yield return Tap(resume);

            var door = app.game.grid.Door; H.x = door.x; H.y = door.y; H.vx = 0;
            yield return Wait(.3f);
            Check("door button appears next to a door", Ui.DoorShown);
            Check("first time at a door: the hand points at the door button", Ui.HandShown && Ui.Teaching == "door", $"teaching {Ui.Teaching}");
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "touch_door.png"));
            yield return Wait(.2f);
            yield return Tap(Ui.Spot("door"));
            yield return Wait(1.2f);
            Check("door button enters the room", app.game.inRoom);
            Check("each lesson ends once its button is used: move, jump, run, door", (app.save.taught & 23) == 23 && !Ui.HandShown, $"taught {app.save.taught}, hand {Ui.HandShown}");
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "touch_room.png"));
            yield return Wait(.3f);

            // Back on open ground for the settings
            app.game.skipIntro = true; app.game.StartLevel(-1); yield return Wait(.2f); app.game.god = true;
            // Version 1.6: the up arrow appears beside a ladder and climbs it
            H.x = 62.4f; H.y = 2; H.vx = 0; yield return Wait(.3f);
            Check("up button appears next to a ladder", Ui.ClimbShown);
            Check("first time at a ladder: the hand points at the up button", Ui.HandShown && Ui.Teaching == "up", $"teaching {Ui.Teaching}");
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "touch_ladder.png"));
            yield return Wait(.2f);
            Vector2 upSpot = Ui.Spot("up"); Down(3, upSpot); yield return Wait(.5f);
            Check("holding the up button climbs the ladder", H.climbing && H.y > 3, $"climbing {H.climbing}, y {H.y:F2}");
            Up(3, upSpot); yield return Wait(.2f);
            Check("on a ladder the down button shows too, and the lesson is learned", Ui.DoorShown && (app.save.taught & 32) != 0 && !Ui.HandShown);
            H.climbing = false; H.x = 5; H.y = 2; app.game.SnapCamera(); yield return Wait(.3f);
            Check("away from ladders the up button hides", !Ui.ClimbShown);
            var save = app.save;
            void Settings() => Ui.Configure(save);
            IEnumerator Run(float seconds) { H.x = 5; H.y = 2; H.vx = 0; right = Ui.Spot("right"); Down(1, right); yield return Wait(seconds); }

            // Run modes
            save.runMode = (int)RunMode.Auto; Settings();
            Check("automatic run hides the run button", !Ui.RunShown);
            yield return Run(1.1f);
            Check("automatic run: one finger reaches run speed", Mathf.Abs(H.vx - Tune.RunSpeed) < .2f, $"vx {H.vx:F2}");
            Up(1, right); yield return Wait(.6f);

            save.runMode = (int)RunMode.Toggle; Settings(); yield return Wait(.1f);
            run = Ui.Spot("run");
            yield return Run(1f);
            Check("toggle run: walks until locked", Mathf.Abs(H.vx - Tune.WalkSpeed) < .2f, $"vx {H.vx:F2}");
            yield return Tap(run);
            H.x = 5; yield return Wait(.9f);
            Check("toggle run: one tap locks running on", Ui.RunLocked && Mathf.Abs(H.vx - Tune.RunSpeed) < .2f, $"locked {Ui.RunLocked}, vx {H.vx:F2}");
            var lit = Ui.FaceColor("run");
            Check("toggle run: locked button is lit", lit.r > .9f && lit.g > .7f && lit.b < .4f, lit.ToString());
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "touch_locked.png"));
            yield return Tap(run);
            H.x = 5; yield return Wait(1f);
            Check("toggle run: a second tap turns it off", !Ui.RunLocked && Mathf.Abs(H.vx - Tune.WalkSpeed) < .2f, $"locked {Ui.RunLocked}, vx {H.vx:F2}");
            Up(1, right); yield return Wait(.6f);

            save.runMode = (int)RunMode.Hold; Settings(); yield return Wait(.1f);
            run = Ui.Spot("run");
            yield return Run(.3f); Down(2, run); yield return Wait(1f);
            Check("hold run: fast while held", Mathf.Abs(H.vx - Tune.RunSpeed) < .2f, $"vx {H.vx:F2}");
            Up(2, run); H.x = 5; yield return Wait(.9f);
            Check("hold run: back to walking when released", Mathf.Abs(H.vx - Tune.WalkSpeed) < .3f, $"vx {H.vx:F2}");
            Up(1, right); yield return Wait(.6f);

            // Look of the buttons
            float wide = Ui.ButtonWidth("jump"); save.buttonSize = 1.4f; Settings();
            Check("size setting grows the buttons", Ui.ButtonWidth("jump") > wide * 1.3f);
            save.buttonSize = 1; save.buttonAlpha = .3f; Settings(); float faint = Ui.FaceColor("jump").a;
            save.buttonAlpha = 1; Settings();
            Check("opacity setting changes the buttons", faint < .35f && Ui.FaceColor("jump").a > .95f);
            save.buttonAlpha = .8f; var before = Ui.FaceColor("jump"); save.buttonColor = 2; Settings();
            Check("colour setting changes the buttons", Ui.FaceColor("jump") != before);
            save.buttonColor = 0; save.leftHanded = true; Settings(); yield return Wait(.1f);
            Check("left hand mode swaps the sides", Ui.Spot("jump").x < Screen.width / 2f && Ui.Spot("left").x > Screen.width / 2f);
            float gy = H.y; Down(1, Ui.Spot("jump")); yield return Wait(.3f);
            Check("left hand mode: the jump button works where it now is", H.y > gy + .5f, $"height {H.y - gy:F2}");
            Up(1, Ui.Spot("jump")); yield return Wait(.9f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "touch_left.png"));
            yield return Wait(.2f);
            save.leftHanded = false; Settings(); yield return Wait(.1f);

            // Weapon
            app.game.ApplyPowerForTest(2); yield return Wait(.2f);
            Check("throw button appears with the sevivon", Ui.ThrowShown);
            yield return Tap(Ui.Spot("throw"));
            Check("throw button throws", app.game.TopsInAir > 0);
            app.game.ApplyPowerForTest(0); yield return Wait(.2f);

            // Settings screen with a real finger
            app.ShowPause(); yield return Wait(.2f);
            app.ShowControls(app.ShowPause); yield return Wait(.2f);
            Check("buttons show behind the settings as a preview", !Ui.Visible);
            save.buttonAlpha = .8f;
            float rowY = mid.y + 240 * app.hud.Scale;
            yield return Tap(new Vector2(mid.x - 330 * app.hud.Scale, rowY));
            Check("tapping the left end of the opacity slider lowers it", save.buttonAlpha < .35f, $"{save.buttonAlpha:F2}");
            Down(9, new Vector2(mid.x - 330 * app.hud.Scale, rowY)); yield return Wait(.1f); Move(9, new Vector2(mid.x + 50 * app.hud.Scale, rowY)); yield return Wait(.15f); Up(9, new Vector2(mid.x + 50 * app.hud.Scale, rowY)); yield return Wait(.2f);
            Check("dragging the slider raises it", save.buttonAlpha > .9f, $"{save.buttonAlpha:F2}");
            yield return Tap(new Vector2(mid.x + (-340 + 2 * 78) * app.hud.Scale, mid.y + 40 * app.hud.Scale));
            Check("tapping a colour chooses it", save.buttonColor == 2, $"{save.buttonColor}");
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "touch_settings.png"));
            yield return Wait(.3f);
            save.buttonColor = 0; save.buttonAlpha = .8f; Settings();
            app.ShowPause(); yield return Wait(.1f);
            yield return Tap(resume); yield return Wait(.2f);

            // A gamepad takes over from the touch buttons
            var gp = InputSystem.AddDevice<Gamepad>(); yield return Wait(.1f);
            Check("a connected but unused gamepad keeps the buttons", Ui.Visible);
            H.x = 5; float px = H.x; InputSystem.QueueStateEvent(gp, new GamepadState { leftStick = new Vector2(1, 0) }); yield return Wait(.5f);
            Check("using the gamepad hides the buttons", !Ui.Visible);
            Check("the gamepad moves the hero", H.x > px + .5f, $"moved {H.x - px:F2}");
            InputSystem.QueueStateEvent(gp, new GamepadState()); yield return Wait(.3f);
            yield return Tap(mid); yield return Wait(.2f);
            Check("touching the screen brings the buttons back", Ui.Visible);
            InputSystem.RemoveDevice(gp);
            Note("end");
            Application.Quit();
        }
    }

    /// Feeds real keyboard and gamepad events through the Input System (-inputtest).
    public class InputTour : MonoBehaviour
    {
        App app; string dir; float t; int step; readonly StringWriter w = new StringWriter(); Gamepad pad; float startX, startY;

        public void Begin(App a, string d) { app = a; dir = d; Directory.CreateDirectory(d); pad = InputSystem.AddDevice<Gamepad>(); }

        static void Keys(params Key[] k) { InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k)); }
        void Note(string s) { w.WriteLine(s); File.WriteAllText(Path.Combine(dir, "input_test.txt"), w.ToString()); }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (t < .9f) return;
            t = 0;
            var h = app.game.hero;
            switch (step++)
            {
                case 0: Keys(Key.Space); break;
                case 1: Keys(); Note("after Space on title: " + app.ScreenName); if (app.ScreenName == "Menu") step = 0; break;   // difficulty, then the intro
                case 2: Keys(Key.Space); break;
                case 3: Keys(); Note("after Space on map: " + app.ScreenName); break;
                case 4: app.game.god = true; break;      // the slime down the road must not end the test
                case 5: startX = h.x; startY = h.y; InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(1, 0) }); break;
                case 6: Note($"gamepad stick right: moved {h.x - startX:F2} tiles"); InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(1, 0) }.WithButton(GamepadButton.South)); t = .6f; break;
                case 7: Note($"gamepad South: height {h.y - startY:F2}, jumping {h.jumping}"); InputSystem.QueueStateEvent(pad, new GamepadState()); break;
                case 8: InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Start)); break;
                case 9: InputSystem.QueueStateEvent(pad, new GamepadState()); Note("gamepad Start: " + app.ScreenName); break;
                case 10: Keys(Key.Escape); break;
                case 11: Keys(); Note("after Esc: " + app.ScreenName); break;
                case 12: startX = h.x; startY = h.y; Keys(Key.RightArrow, Key.LeftShift); t = .5f; break;
                case 13: Note($"held Right+Shift: moved {h.x - startX:F2} tiles, vx {h.vx:F2}"); Keys(Key.RightArrow, Key.LeftShift, Key.Space); t = .6f; break;
                case 14: Note($"pressed Space: height {h.y - startY:F2}, jumping {h.jumping}"); Keys(); break;
                case 15: Keys(Key.Escape); break;
                case 16: Keys(); Note("after Esc: " + app.ScreenName); Application.Quit(); break;
            }
        }
    }

    /// Exercises every mechanic through the real simulation (-selftest) and writes PASS or FAIL lines.
    /// Runs on the "test" level, so a redesign of the real levels cannot break it.
    public static class SelfTest
    {
        static StringWriter w; static int fails;
        static void Check(string name, bool ok) { w.WriteLine((ok ? "PASS " : "FAIL ") + name); if (!ok) fails++; }

        static Game Fresh(App app, int power = 0)
        {
            app.run = new RunState { lives = 9, power = power };
            app.game.StartLevel(-1);
            return app.game;
        }

        static void Above(Player h, Body e) { h.x = e.x; h.y = e.Top + .4f; h.vy = -6; h.vx = 0; h.grounded = false; }
        static void Under(Game g, float x, float y) { g.hero.x = x; g.hero.y = y; g.hero.vy = 16; g.hero.vx = 0; g.hero.grounded = false; }

        public static void Run(App app, string dir)
        {
            Directory.CreateDirectory(dir); w = new StringWriter();
            var g = app.game; g.skipIntro = true; g.skipBossIntro = true; var none = new Pad();

            g = Fresh(app); var e = g.TestEnemy('e', 8, 2); Above(g.hero, e); int s0 = app.run.score; bool bounced = false;
            for (int i = 0; i < 30; i++) { g.TestStep(1, none); bounced |= g.hero.vy > 5; }
            Check("stomp slime squashes it, scores and bounces", !e.alive && app.run.score > s0 && bounced);

            g = Fresh(app); e = g.TestEnemy('n', 12, 2); Above(g.hero, e); g.TestStep(30, none);
            Check("stomp snail hides it in the shell", e.state == 1 && e.alive);
            g.hero.x = e.x - 1.2f; g.hero.y = 2; g.hero.vy = 0; g.hero.invuln = 0; g.TestStep(90, new Pad { x = 1 });
            Check("touching the shell kicks it", e.state == 2);
            var other = g.TestEnemy('e', e.x + 1.5f, 2); other.dir = -1; g.TestStep(60, none);
            Check("sliding shell knocks out another enemy", !other.alive);

            g = Fresh(app, 1); e = g.TestEnemy('p', 8, 2); Above(g.hero, e); g.TestStep(30, none);
            Check("spike slime cannot be stomped and costs the power", e.alive && app.run.power == 0 && g.deaths == 0);
            Check("hero survives one hit when big", g.hero.invuln > 0);

            g = Fresh(app, 2); e = g.TestEnemy('p', 8, 2); Above(g.hero, e); g.TestStep(30, none);
            Check("a hit takes the sevivon first and leaves the hero big", app.run.power == 1 && g.hero.big && !g.hero.shooter);

            g = Fresh(app); e = g.TestEnemy('y', 8, 3); g.TestStep(2, none); Above(g.hero, e); g.TestStep(30, none);
            Check("stomp fly drops it", !e.alive);

            g = Fresh(app); e = g.TestEnemy('j', 14, 2); float x0 = e.x; bool leapt = false;
            for (int i = 0; i < 300; i++) { g.TestStep(1, none); leapt |= !e.grounded && e.y > 2.3f; }
            Check("frog hops", leapt && e.x != x0);

            g = Fresh(app, 2); e = g.TestEnemy('p', g.hero.x + 6, 2); g.TestStep(2, none); g.hero.facing = 1; float high = 0; g.TestStep(1, new Pad { runPressed = true });
            for (int i = 0; i < 200 && g.TopsInAir > 0; i++) { g.TestStep(1, none); high = Mathf.Max(high, g.TopHeight); }
            Check("thrown sevivon hops along the ground", high > 2.4f);
            Check("thrown sevivon takes out a spike slime", !e.alive);
            g = Fresh(app, 2); g.hero.x = 62; g.hero.facing = 1; g.TestStep(2, none); g.TestStep(1, new Pad { throwPressed = true }); bool cameBack = false;
            for (int i = 0; i < 300 && g.TopsInAir > 0; i++) { g.TestStep(1, none); cameBack |= g.TopDir < 0; }
            Check("sevivon turns back off a wall", cameBack);

            g = Fresh(app, 1); Under(g, 20.5f, 3.5f); g.TestStep(30, none);
            Check("big hero breaks a brick", !g.grid.Solid(20, 6));
            g = Fresh(app, 0); Under(g, 20.5f, 3.5f); g.TestStep(30, none);
            Check("small hero only bumps a brick", g.grid.Solid(20, 6));

            g = Fresh(app); Check("hidden block starts open", !g.grid.Solid(32, 6));
            Under(g, 32.5f, 3.5f); int c0 = app.run.coins; g.TestStep(30, none);
            Check("hidden block appears when hit from below", g.grid.Solid(32, 6) && app.run.coins > c0 && app.run.secrets == 1);

            g = Fresh(app); c0 = app.run.coins; Under(g, 24.5f, 3.5f); g.TestStep(30, none);
            Check("coin block pays out", app.run.coins == c0 + 1);

            g = Fresh(app); Under(g, 28.5f, 3.5f); g.TestStep(40, none);
            Check("power box releases a gem", g.pickups.Count == 1 && g.pickups[0].type == "grow");
            for (int i = 0; i < 300 && g.pickups.Count > 0; i++) { var k = g.pickups[0]; g.hero.x = k.x; g.hero.y = k.y; g.hero.vy = 0; g.TestStep(1, none); }
            g.TestStep(90, none);
            Check("first power-up makes the hero big", app.run.power == 1 && g.hero.big);
            g = Fresh(app, 1); Under(g, 28.5f, 3.5f); g.TestStep(120, none);
            Check("second power-up, taken while big, is the sevivon", g.pickups.Count == 1 && g.pickups[0].type == "shoot");
            for (int i = 0; i < 300 && g.pickups.Count > 0; i++) { var k = g.pickups[0]; g.hero.x = k.x; g.hero.y = k.y; g.hero.vy = 0; g.TestStep(1, none); }
            g.TestStep(90, none);
            Check("sevivon power-up gives the throw", app.run.power == 2 && g.hero.shooter);

            // Version 1.4: the frost sevivon is the next step after the sevivon
            g = Fresh(app, 2); Under(g, 28.5f, 3.5f); g.TestStep(120, none);
            Check("third power-up, taken with the sevivon, is the frost sevivon", g.pickups.Count == 1 && g.pickups[0].type == "frost");
            for (int i = 0; i < 300 && g.pickups.Count > 0; i++) { var k = g.pickups[0]; g.hero.x = k.x; g.hero.y = k.y; g.hero.vy = 0; g.TestStep(1, none); }
            g.TestStep(90, none);
            Check("frost power-up keeps the throw", app.run.power == 3 && g.hero.shooter && g.hero.big);

            g = Fresh(app, 3); e = g.TestEnemy('e', g.hero.x + 6, 2); g.TestStep(2, none); g.hero.facing = 1; int plats = g.grid.Platforms.Count;
            g.TestStep(1, new Pad { throwPressed = true });
            for (int i = 0; i < 200 && g.TopsInAir > 0; i++) g.TestStep(1, none);
            Check("frost sevivon freezes an enemy instead of knocking it out", e.alive && e.frozen > 0 && e.ice != null && g.grid.Platforms.Count == plats + 1);
            float ex = e.x; g.hero.x = e.x; g.hero.y = e.y + 1.6f; g.hero.vy = 0; g.hero.vx = 0; g.hero.invuln = 0; g.TestStep(30, none);
            Check("the hero can stand on the frozen enemy", g.hero.grounded && e.ice != null && g.hero.on == e.ice && Mathf.Abs(g.hero.y - (e.y + 1)) < .01f);
            Check("a frozen enemy does not move and does no harm", e.x == ex && app.run.power == 3 && g.deaths == 0);
            for (int i = 0; i < 120 * 6 && e.frozen > 0; i++) g.TestStep(1, none);
            g.TestStep(10, none);
            Check("the ice melts after five seconds and the ledge goes with it", e.frozen <= 0 && e.ice == null && g.grid.Platforms.Count == plats);
            Check("the thaw does not hurt the hero standing on the ice", app.run.power == 3 && g.deaths == 0);

            g = Fresh(app, 3); e = g.TestEnemy('e', g.hero.x + 6, 2); g.TestStep(2, none); g.hero.facing = 1;
            g.TestStep(1, new Pad { throwPressed = true }); for (int i = 0; i < 200 && g.TopsInAir > 0; i++) g.TestStep(1, none);
            g.TestStep(30, none); g.TestStep(1, new Pad { throwPressed = true }); for (int i = 0; i < 200 && g.TopsInAir > 0; i++) g.TestStep(1, none);
            Check("a second sevivon shatters the ice and knocks the enemy out", !e.alive && e.ice == null && g.grid.Platforms.Count == plats);

            g = Fresh(app, 3); e = g.TestEnemy('p', 8, 2); Above(g.hero, e); g.TestStep(30, none);
            Check("a hit takes the frost first and leaves the plain sevivon", app.run.power == 2 && g.hero.shooter);

            g = Fresh(app); Under(g, 12.5f, 3.5f); g.TestStep(120, none);
            Check("shield box releases a bubble that stays on the box", g.pickups.Count == 1 && g.pickups[0].type == "shield" && Mathf.Abs(g.pickups[0].x - 12.5f) < .01f);
            for (int i = 0; i < 300 && g.pickups.Count > 0; i++) { var k = g.pickups[0]; g.hero.x = k.x; g.hero.y = k.y; g.hero.vy = 0; g.TestStep(1, none); }
            g.TestStep(60, none);
            Check("the bubble shields the hero and shows on the HUD", app.run.shield && app.run.power == 0 && app.hud.ShieldShown);
            e = g.TestEnemy('p', g.hero.x + 3, 2); g.hero.invuln = 0; Above(g.hero, e); g.TestStep(30, none);
            Check("the bubble takes one hit: no death, no power lost", !app.run.shield && g.deaths == 0 && app.run.power == 0 && g.hero.invuln > 0);
            g.hero.x = 5; g.hero.y = 2; g.TestStep(300, none); e = g.TestEnemy('p', g.hero.x + 3, 2); g.hero.invuln = 0; Above(g.hero, e); g.TestStep(30, none);
            Check("without the bubble the next hit counts", g.deaths == 1);
            g = Fresh(app); app.run.shield = true; int died = g.deaths; g.hero.y = -3; g.TestStep(40, none);
            Check("a fall still kills through the bubble, and the bubble is lost", g.deaths == died + 1 && !app.run.shield);

            g = Fresh(app); g.hero.x = g.grid.Door.x; g.hero.y = g.grid.Door.y; g.TestStep(4, none); g.TestStep(80, new Pad { down = true });
            Check("secret door leads to the bonus room", g.inRoom);
            g.TestStep(4, none); g.TestStep(80, new Pad { down = true });
            Check("room door leads back", !g.inRoom && Mathf.Abs(g.hero.x - g.grid.Door.x) < .6f);

            g = Fresh(app); g.hero.x = g.grid.Checkpoint.x + .5f; g.hero.y = g.grid.Checkpoint.y; g.TestStep(4, none); g.hero.y = -3; g.TestStep(420, none);
            Check("death returns to the checkpoint", g.Running && Mathf.Abs(g.hero.x - g.grid.Checkpoint.x) < .1f && app.run.lives == 8);

            g = Fresh(app); g.hero.x = 36.5f; g.hero.y = 2; g.TestStep(3, none);
            Check("spring launches", g.hero.vy > 15);

            g = Fresh(app); g.hero.x = 41.5f; g.hero.y = 3; g.hero.vy = 0; g.TestStep(20, none); bool solid = g.grid.Solid(41, 2);
            g.TestStep(90, none);
            Check("crumbling block holds, then gives way", solid && !g.grid.Solid(41, 2));
            g.hero.x = 10; g.hero.y = 2; g.TestStep(120 * 5, none);
            Check("crumbling block comes back", g.grid.Solid(41, 2));

            g = Fresh(app); g.hero.x = 48; g.hero.y = 2; float bx = g.hero.x; g.TestStep(60, none);
            Check("conveyor carries the hero", g.hero.x > bx + 1);

            g = Fresh(app); g.hero.x = 54.5f; g.hero.y = 2; g.TestStep(4, none); g.hero.x = 56.5f; g.TestStep(4, none); g.hero.x = 58.5f; g.TestStep(4, none);
            Check("three stars are saved for the level map", g.GotStars == 7);

            int buzz = Haptics.Count; Haptics.On = true;
            g = Fresh(app); e = g.TestEnemy('e', 8, 2); Above(g.hero, e); g.TestStep(30, none); int afterStomp = Haptics.Count;
            g = Fresh(app, 1); e = g.TestEnemy('p', 8, 2); Above(g.hero, e); g.TestStep(30, none); int afterHit = Haptics.Count;
            g.hero.y = -3; g.TestStep(40, none);
            Check("vibration on stomp, hit and death", afterStomp > buzz && afterHit > afterStomp && Haptics.Count > afterHit);
            Haptics.On = false; int off = Haptics.Count; g = Fresh(app); e = g.TestEnemy('e', 8, 2); Above(g.hero, e); g.TestStep(30, none);
            Check("vibration can be turned off", Haptics.Count == off);
            Haptics.On = true;

            // Version 1.6: ladders, ropes and the block slime
            {
                var up = new Pad { up = true }; var down = new Pad { down = true };
                g = Fresh(app); g.hero.x = 62.4f; g.hero.y = 2; g.TestStep(2, none); g.TestStep(1, up);
                Check("up at the foot of a ladder holds on and lines up with it", g.hero.climbing && Mathf.Abs(g.hero.x - 62.5f) < .01f);
                g.TestStep(150, up);
                Check("climbing to the top steps off onto the ledge", !g.hero.climbing && g.hero.grounded && Mathf.Abs(g.hero.y - 7) < .01f);
                g.TestStep(30, none);
                Check("the top of a ladder holds the hero up", g.hero.grounded && Mathf.Abs(g.hero.y - 7) < .01f);
                g.TestStep(3, down);
                Check("down on top of a ladder climbs down into it", g.hero.climbing && g.hero.y < 7);
                g.TestStep(200, down);
                Check("climbing down to the floor lets go", !g.hero.climbing && g.hero.grounded && Mathf.Abs(g.hero.y - 2) < .01f);
                g.TestStep(1, up); g.TestStep(40, up); float midY = g.hero.y;
                Check("the hero can stop halfway up a ladder", g.hero.climbing && midY > 3 && midY < 6);
                g.TestStep(30, none);
                Check("letting go of up stays put on the ladder", g.hero.climbing && Mathf.Abs(g.hero.y - midY) < .01f);
                g.TestStep(1, new Pad { jumpPressed = true, jumpHeld = true, x = -1 });
                Check("jump lets go of the ladder with a normal jump", !g.hero.climbing && g.hero.vy > 10 && g.hero.vx < 0);
                g = Fresh(app); g.hero.x = 44.5f; g.hero.y = 2; g.TestStep(2, none);
                g.TestStep(1, new Pad { jumpPressed = true, jumpHeld = true, up = true }); g.TestStep(60, new Pad { jumpHeld = true, up = true });
                Check("up in the air catches a rope", g.hero.climbing);
                g.TestStep(240, up);
                Check("a rope ends where it hangs", g.hero.climbing && g.hero.y <= 10.01f && g.hero.y > 8);
                g.TestStep(400, down);
                Check("climbing off the bottom of a rope drops to the floor", !g.hero.climbing && g.hero.grounded && Mathf.Abs(g.hero.y - 2) < .01f);
                g = Fresh(app); int d0 = g.deaths; e = g.TestEnemy('q', 8, 2); g.TestStep(2, none); Above(g.hero, e); g.TestStep(30, none);
                Check("a stomped block slime turns into a block", !e.alive && e.block != null && app.run.power == 0 && g.deaths == d0);
                g.hero.x = e.x; g.hero.y = e.y + 1.5f; g.hero.vy = 0; g.hero.vx = 0; g.TestStep(40, none);
                Check("the hero can stand on the block", g.hero.grounded && g.hero.on == e.block && Mathf.Abs(g.hero.y - (e.y + 1)) < .01f);
                g.TestStep(600, none);
                Check("the block stays", e.block != null && g.EnemyCount >= 1);
                g = Fresh(app); d0 = g.deaths; e = g.TestEnemy('q', 8, 2); g.hero.x = 6; g.hero.y = 2; g.TestStep(60, new Pad { x = 1 });
                Check("walking into a block slime hurts", g.deaths == d0 + 1);
            }

            // Version 1.7: sinking clouds and the worm
            {
                g = Fresh(app); var cl = g.grid.Clouds[0];
                g.hero.x = 7.5f; g.hero.y = 11.3f; g.hero.vy = 0; g.TestStep(30, none);
                Check("the hero can stand on a snow cloud", g.hero.on == cl && Mathf.Abs(g.hero.y - cl.y) < .01f);
                g.TestStep(120, none);
                Check("a cloud sinks while stood on, carrying the hero", cl.y < 10 && cl.y > 9 && g.hero.on == cl && Mathf.Abs(g.hero.y - cl.y) < .01f);
                g.hero.x = 20; g.hero.y = 2; g.hero.on = null; g.TestStep(120, none);
                Check("left alone, the cloud floats back up", Mathf.Abs(cl.y - 11) < .01f);
                g.hero.x = 7.5f; g.hero.y = 11.3f; g.hero.vy = 0; g.SnapCamera(); g.TestStep(120 * 8, none);
                Check($"over ground, a cloud stops when it reaches the floor (cloud {cl.y:F2}, hero {g.hero.y:F2} {g.hero.grounded})", Mathf.Abs(cl.y - 2) < .01f && g.hero.grounded);
                g = Fresh(app); int d1 = g.deaths; e = g.TestEnemy('m', 20, 2); g.hero.x = 30;
                for (int i = 0; i < 1000 && e.Out; i++) g.TestStep(1, none);
                g.hero.x = 20; g.hero.y = 2; g.hero.vx = 0; g.hero.invuln = 0; g.TestStep(2, none);
                Check("a worm in the ground does no harm", !e.Out && g.deaths == d1);
                for (int i = 0; i < 1000 && !e.Out; i++) g.TestStep(1, none);
                g.TestStep(3, none);
                Check("a worm out of the ground hurts", g.deaths == d1 + 1);
                g = Fresh(app); e = g.TestEnemy('m', 20, 2); g.hero.x = 30;
                for (int i = 0; i < 1000 && !e.Out; i++) g.TestStep(1, none);
                Above(g.hero, e); g.TestStep(20, none);
                Check("a worm that is out can be stomped", !e.alive);
            }

            // Version 1.8: the mouse and its sleepers
            {
                g = Fresh(app); var m = g.TestEnemy('z', 34, 2); var sl = g.TestEnemy('e', 40, 2); g.SleepNearMice(); float sx = sl.x;
                g.TestStep(120, none);
                Check("an enemy near a mouse sleeps and stays still", sl.asleep && sl.x == sx && !m.scared);
                g.hero.x = 26; g.hero.y = 2; g.hero.vx = 0; g.SnapCamera(); g.TestStep(120, new Pad { x = 1 });
                Check("walking up to a mouse does not wake it", !m.scared && sl.asleep && g.hero.x > 28);
                g.hero.x = 22; g.hero.vx = 0; g.SnapCamera(); g.TestStep(150, new Pad { x = 1, run = true });
                Check("running near a mouse makes it squeak and run away", m.scared && m.dir == 1);
                Check("the squeak wakes the enemies near it", !sl.asleep);
                g.TestStep(60, none);
                Check("woken, the enemy moves again", sl.x != sx);
                g = Fresh(app); int d2 = g.deaths; m = g.TestEnemy('z', 20, 2); sl = g.TestEnemy('e', 26, 2); g.SleepNearMice(); g.TestStep(2, none);
                Above(g.hero, m); g.TestStep(20, none);
                Check("a stomp silences the mouse: the sleepers sleep on", !m.alive && !m.scared && sl.asleep);
                g = Fresh(app); d2 = g.deaths; m = g.TestEnemy('z', 12, 2); g.hero.x = 10; g.hero.y = 2; g.TestStep(150, new Pad { x = 1 });
                Check("bumping into a mouse does no harm, but it squeaks", m.scared && g.deaths == d2);
            }

            // Version 2.4: the land of colours
            {
                int c1 = Array.FindIndex(Levels.All, l => l.file == "w4l1"), k3 = Array.FindIndex(Levels.All, l => l.file == "boss3");
                Check("colours: four hidden levels after the frost giant", Levels.StoryCount == App.MainCount + 4 && Levels.All[App.MainCount - 1].file == "boss2" && c1 == App.MainCount && Levels.All[k3].hidden);
                app.run = new RunState { lives = 9 }; app.game.skipIntro = true; app.game.StartLevel(c1); g = app.game; g.god = true;
                Check("colours: a level starts red, so red blocks are solid and blue ones open", g.world.sw == 0 && g.grid.Solid(24, 5) && !g.grid.Solid(45, 1));
                g.hero.x = 18.5f; g.hero.y = 2; g.hero.vx = 0; g.SnapCamera(); g.TestStep(10, none);
                Check("colours: the blue switch opens the red wall and brings out the blue bridge", g.world.sw == 1 && !g.grid.Solid(24, 5) && g.grid.Solid(45, 1));
                g.TestStep(30, none);
                Check("colours: standing on it again changes nothing", g.world.sw == 1);
                g.hero.x = 20.5f; g.TestStep(240, new Pad { x = 1 });
                Check($"colours: the hero walks through the open red wall ({g.hero.x:F1})", g.hero.x > 25);
                g.god = false;

                g = Fresh(app); var bee = g.TestEnemy('W', 30, 3); g.hero.x = 3;
                float bx0 = 99, bx1 = -99, by0 = 99, by1 = -99;
                for (int i = 0; i < 120 * 8; i++) { g.TestStep(1, none); bx0 = Mathf.Min(bx0, bee.x); bx1 = Mathf.Max(bx1, bee.x); by0 = Mathf.Min(by0, bee.y); by1 = Mathf.Max(by1, bee.y); }
                Check($"bee: flies a figure of eight ({bx1 - bx0:F1} wide, {by1 - by0:F1} high)", bx1 - bx0 > 4.5f && bx1 - bx0 < 5.5f && by1 - by0 > 2 && by1 - by0 < 2.8f);
                g = Fresh(app); var bug = g.TestEnemy('K', 20, 2); g.TestStep(2, none); float gy = bug.y;
                Above(g.hero, bug); g.TestStep(20, none);
                Check("ladybug: the first stomp sends it flying", bug.alive && bug.state == 1);
                g.TestStep(120, none);
                Check($"ladybug: then it hovers above the ground ({bug.y:F1})", bug.y > gy + .8f);
                g.hero.invuln = 0; Above(g.hero, bug); g.TestStep(20, none);
                Check("ladybug: the second stomp ends it", !bug.alive);
                g = Fresh(app); var fish = g.TestEnemy('N', 30, 0); g.hero.x = 3; float fy = -9; int outT = 0;
                for (int i = 0; i < 120 * 6; i++) { g.TestStep(1, none); fy = Mathf.Max(fy, fish.y); if (fish.Out) outT++; }
                Check($"fish: leaps about 6 tiles out of the water and back (top {fy:F1}, out {outT / 120f:F1} of 6 s)", fy > 5.5f && fy < 6.5f && outT > 120 * 2 && outT < 120 * 4.5f);

                app.run = new RunState { lives = 9 }; app.game.StartLevel(k3); g = app.game; g.god = true; var king = g.boss;
                Check("returning king: starts red, so the red of the start makes him soft", king != null && king.color == 0 && king.Open);
                king.awake = true; Above(g.hero, king); g.TestStep(20, none);
                Check("returning king: a stomp hurts him and turns him blue", king.hp == Boss.MaxHp - 2 && king.color == 1 && !king.Open);
                king.hurt = 0; g.hero.invuln = 0; Above(g.hero, king); g.TestStep(12, none);
                Check("returning king: blue, he cannot be stomped while red is on", king.hp == Boss.MaxHp - 2);
                g.hero.x = 126.5f; g.hero.y = 2; g.hero.vx = 0; g.hero.vy = 0; g.SnapCamera(); g.TestStep(10, none);
                Check("returning king: the blue switch makes him soft again", g.world.sw == 1 && king.Open);
                g.god = false; g.Unload();

                var keepS = (int[])app.save.stars.Clone(); int keepU = app.save.unlocked;
                for (int i = 0; i < app.save.stars.Length; i++) app.save.stars[i] = i < App.MainCount ? 7 : 0;
                app.save.stars[3] = 3;
                Check("map: the hidden world stays shut while a star is missing", !app.HiddenOpen && app.MainStarsMissing == 1);
                app.save.stars[3] = 7; app.save.unlocked = App.MainCount; app.save.achievements = 0;
                app.CheckProgressForTest();
                Check("map: every star of worlds 1 to 3 opens it, and the achievement does not wait for the hidden stars", app.HiddenOpen && Achievements.Has(Achievements.Id.AllStars));
                app.mapHold = true; app.ShowMap(); app.MapPageForTest();
                Check("map: the hidden world has its own page", app.PageFirst == App.MainCount && app.MapCursor == App.MainCount);
                app.MapPageForTest();
                Check("map: and back", app.PageFirst == 0 && app.MapCursor == App.MainCount - 1);
                app.mapHold = false; app.save.stars = keepS; app.save.unlocked = keepU; app.save.achievements = 0;
            }

            // Version 2.3: the time trial and its ghost
            {
                var gh = new Ghost(); for (int i = 0; i < 90; i++) gh.Add(3 + i * .1f, 2 + (i % 7) * .3f, i % 2 == 0 ? "walk1" : "jump", i < 45 ? 's' : 'p', i % 3 == 0);
                var back = Ghost.Unpack(gh.Pack()); var f1 = back.At(1); var f0 = gh.At(1);
                Check($"ghost: packs small and comes back the same ({gh.Pack().Length} chars for 3 s)", back.Count == 90 && Mathf.Abs(f1.x - f0.x) < .03f && Mathf.Abs(f1.y - f0.y) < .03f
                    && f1.pose == f0.pose && f1.form == f0.form && f1.flip == f0.flip);
                int boss = Array.FindIndex(Levels.All, l => l.boss);
                var keepBest = (int[])app.save.best.Clone(); app.save.best[0] = 100; app.save.trialTime[0] = 0; app.save.ghost[0] = null;
                Check("time trial: only finished levels that are not bosses", app.TrialOpen(0) && !app.TrialOpen(1) && !app.TrialOpen(boss));
                app.run = new RunState { lives = 3, score = 4321 }; app.game.skipIntro = true; app.EnterTrial(0); g = app.game;
                Check("time trial: no enemies and no lives to lose", g.trial && g.EnemyCount == 0 && app.run.lives == 1);
                g.TestStep(240, new Pad { x = 1 }); float rx = g.hero.x; g.WinForTest();
                Check($"time trial: the first finish is the record, with its ghost ({app.save.trialTime[0]}, {g.record.Count} frames)", g.NewBestTime && app.save.trialTime[0] == Mathf.RoundToInt(g.LastTime * 10)
                    && !string.IsNullOrEmpty(app.save.ghost[0]) && g.record.Count >= 59);
                app.TrialDone();
                Check("time trial: back on the map, the run as it was", app.ScreenName == "Map" && app.run.score == 4321 && app.run.lives == 3 && !app.game.trial);
                app.EnterTrial(0); g = app.game; g.TestStep(120, none); g.DrawNow();
                var want = Ghost.Unpack(app.save.ghost[0]).At(1);
                Check($"time trial: the ghost runs the best run (at {g.GhostAt.x:F2}, recorded {want.x:F2})", g.GhostShown && Mathf.Abs(g.GhostAt.x - want.x) < .05f);
                g.TestStep(60, none); g.FallForTest();
                for (int i = 0; i < 600 && g.levelTime > .4f; i++) g.TestStep(1, none);
                Check($"time trial: a fall starts the run over, no life lost (clock {g.levelTime:F1})", g.trial && g.levelTime < .5f && app.run.lives == 1 && Mathf.Abs(g.hero.x - rx) > 1);
                int rec = app.save.trialTime[0]; string recGhost = app.save.ghost[0];
                g.TestStep(360, none); g.WinForTest();
                Check("time trial: a slower run keeps the record and its ghost", !g.NewBestTime && app.save.trialTime[0] == rec && app.save.ghost[0] == recGhost);
                app.TrialDone();
                app.save.best = keepBest; app.save.trialTime[0] = 0; app.save.ghost[0] = null; g.Unload();
            }

            // Version 2.2: achievements and the level clock
            {
                Check("achievements: 16, each with an icon", Achievements.All.Length == 16 && Achievements.All.All(d => Resources.Load<Texture2D>("Art/" + d.icon) != null));
                app.save.achievements = 0; app.save.coinsTotal = 0;
                Achievements.Unlock(Achievements.Id.Freeze);
                Check($"achievements: opening one shows a toast ({app.hud.ToastText})", Achievements.Has(Achievements.Id.Freeze) && app.hud.ToastShown && Achievements.Count == 1);
                app.save.achievements = 0;
                app.run = new RunState { lives = 9 }; app.game.skipIntro = true; app.game.StartLevel(0); g = app.game;
                for (int i = 0; i < app.save.bestTime.Length; i++) app.save.bestTime[i] = 0;
                g.TestStep(120, none); g.WinForTest();
                Check($"clock: the first finish is the best time ({g.LastTime:F1}s, saved {app.save.bestTime[0]})", g.NewBestTime && app.save.bestTime[0] == Mathf.RoundToInt(g.LastTime * 10));
                int b0 = app.save.bestTime[0];
                Check("achievements: a finished level without a hit opens two", Achievements.Has(Achievements.Id.First) && Achievements.Has(Achievements.Id.NoHit));
                g.StartLevel(0); g.TestStep(240, none); g.WinForTest();
                Check("clock: a slower finish keeps the old best", !g.NewBestTime && app.save.bestTime[0] == b0);
                g.StartLevel(0); g.TestStep(60, none); g.hero.invuln = 0; g.god = true; g.TestEnemy('e', g.hero.x + .2f, g.hero.y); g.TestStep(30, none); g.god = false; g.WinForTest();
                Check($"clock: a faster finish is the new best ({app.save.bestTime[0]})", g.NewBestTime && app.save.bestTime[0] < b0);
                app.save.achievements = 0; g.StartLevel(0); g.hero.invuln = 0; g.god = true; g.TestEnemy('e', g.hero.x + .2f, g.hero.y); g.TestStep(30, none); g.god = false; g.WinForTest();
                Check("achievements: a hit in the level, no 'without a hit'", Achievements.Has(Achievements.Id.First) && !Achievements.Has(Achievements.Id.NoHit));
                g = Fresh(app); app.save.coinsTotal = 98;
                g.AddCoinForTest(); g.AddCoinForTest();
                Check($"achievements: the 100th coin over all games ({app.save.coinsTotal})", Achievements.Has(Achievements.Id.Coins));
                var keepStars = (int[])app.save.stars.Clone(); var keepFound = (int[])app.save.found.Clone();
                for (int i = 0; i < Levels.All.Length; i++) { app.save.stars[i] = 7; app.save.found[i] = Levels.All[i].world == 1 ? (1 << 10) - 1 : 0; }
                app.CheckProgressForTest();
                Check("achievements: every star and the secrets of world 1 only", Achievements.Has(Achievements.Id.AllStars) && Achievements.Has(Achievements.Id.Secrets1) && !Achievements.Has(Achievements.Id.Secrets2));
                app.save.stars = keepStars; app.save.found = keepFound; app.save.achievements = 0; app.save.coinsTotal = 0;
                for (int i = 0; i < app.save.bestTime.Length; i++) app.save.bestTime[i] = 0;
                g.Unload();
            }

            // Version 2.1: outfits
            {
                Check("outfits: one item every 3 stars, hats and pants in turn", Outfit.StarsFor(true, 1) == 3 && Outfit.StarsFor(false, 1) == 6 && Outfit.StarsFor(true, 5) == 27
                    && Outfit.StarsFor(false, 5) == 30 && Outfit.OpenCount(33) == 10 && Outfit.NextAt(4) == 6 && Outfit.NextAt(30) == -1);
                var keep = (int[])app.save.stars.Clone();
                for (int i = 0; i < app.save.stars.Length; i++) app.save.stars[i] = 0;
                Outfit.Hat = Outfit.Pant = 0; app.WearForTest(true, 1); app.WearForTest(false, 1);
                Check("outfits: with no stars nothing can be worn", Outfit.Hat == 0 && Outfit.Pant == 0);
                app.save.stars[0] = 7; app.save.stars[1] = 7; app.save.stars[2] = 7;      // 9 stars: two hats and one pair of pants
                app.WearForTest(true, 1); int h1 = Outfit.Hat; app.WearForTest(true, 1); int h2 = Outfit.Hat; app.WearForTest(true, 1);
                app.WearForTest(false, 1); app.WearForTest(false, 1); app.WearForTest(false, 1);
                Check($"outfits: the wardrobe offers only what is open (hats {h1},{h2},{Outfit.Hat}, pants {Outfit.Pant})", h1 == 1 && h2 == 2 && Outfit.Hat == 0 && Outfit.Pant == 1);
                Check("outfits: the choice is saved", app.save.hat == 0 && app.save.pant == 1);
                Outfit.Hat = 5; Outfit.Pant = 1;
                g = Fresh(app); g.TestStep(2, none); g.DrawNow();
                Check($"outfits: the hero wears the pants and the hat in the game ({g.HeroSpriteName})", g.HeroSpriteName.StartsWith("hero_s_") && g.HeroSpriteName.EndsWith("_red") && g.HatShown);
                Outfit.Hat = 0; g.DrawNow();
                Check("outfits: no hat chosen, no hat shown", !g.HatShown);
                g.Unload();
                for (int i = 0; i < app.save.stars.Length; i++) app.save.stars[i] = 0;
                app.save.stars[0] = 3;     // 2 stars, and the level is finished with a third
                app.game.skipIntro = true; app.Enter(0); app.save.stars[0] = 7; app.LevelDone();
                Check($"outfits: a level that brings the stars to 3 opens the first hat ({app.OutfitNote})", app.OutfitNote != null && app.OutfitNote.Contains(Outfit.HatNames[1]));
                app.save.stars = keep; Outfit.Hat = Outfit.Pant = 0; app.save.hat = app.save.pant = 0; app.OutfitNote = null;
            }

            // Version 2.0: the frost giant
            {
                int gi = Array.FindIndex(Levels.All, l => l.file == "boss2");
                app.run = new RunState { lives = 9 }; app.game.skipIntro = true; app.game.StartLevel(gi); g = app.game; g.god = true;
                var b = g.boss;
                Check("the frost giant is the last boss of worlds 1 to 3", b != null && b.frost && gi == App.MainCount - 1);
                g.hero.x = b.x - 10; g.hero.y = 2; g.SnapCamera(); g.TestStep(2, none);
                Check("the giant wakes when the hero comes near", b.awake);
                bool warned = false;
                for (int i = 0; i < 900 && g.SnowBallCount == 0; i++) { warned |= b.Warning; g.TestStep(1, none); }
                Check("the giant crouches, then throws a snow block and is stuck", warned && g.SnowBallCount == 1 && b.stuck > 0 && !b.Warning);
                Check("the giant calls no slimes", !g.enemies.Exists(e => e.alive && Mathf.Abs(e.x - b.x) < 30));
                Above(g.hero, b); g.TestStep(20, none);
                Check("stuck, the giant can be stomped", b.hp == Boss.MaxHp - 2);
                for (int i = 0; i < 900 && !(b.grounded && !b.Open && b.hurt <= 0 && b.wait > .3f && b.thrown == false); i++) g.TestStep(1, none);
                g.hero.invuln = 0; int h0 = g.hits; Above(g.hero, b); g.TestStep(12, none);
                Check("not stuck, a stomp bounces off without harm to either", b.hp == Boss.MaxHp - 2 && g.hits == h0 && g.hero.vy > 0);
                g.hero.x = b.x - 9; g.hero.y = 2; g.hero.vx = 0; g.SnapCamera();
                for (int i = 0; i < 900 && g.SnowBallCount == 0; i++) g.TestStep(1, none);
                h0 = g.hits; g.hero.invuln = 0;
                for (int i = 0; i < 200 && g.SnowBallCount > 0; i++) g.TestStep(1, none);
                Check("a snow block hurts the hero it lands on", g.hits == h0 + 1);
                g.ApplyPowerForTest(2);
                for (int i = 0; i < 900 && !b.Open; i++) g.TestStep(1, none);
                g.hero.x = b.x - 4; g.hero.y = b.y; g.hero.facing = 1; g.hero.vx = 0; int hp0 = b.hp;
                g.TestStep(1, new Pad { runPressed = true }); g.TestStep(40, none);
                Check("stuck, a sevivon hurts the giant", b.hp == hp0 - 1);
                b.hp = 2; b.hurt = 0;
                for (int i = 0; i < 900 && !b.Open; i++) g.TestStep(1, none);
                bool flying = g.SnowBallCount > 0;
                Above(g.hero, b); g.TestStep(20, none);
                Check("the last stomp beats the giant and its snow blocks break", !b.alive && flying && g.SnowBallCount == 0);
                g.god = false;
            }

            // Version 1.5: the live world map
            {
                g.Unload(); app.run = new RunState(); app.save.unlocked = 1;
                app.MapTestFinished(1);
                Check("map: after a level the hero starts on the level just finished", Mathf.Abs(app.MapHeroX - App.NodeX(1)) < 2 && app.MapCursor == 2 && app.MapWalking);
                app.MapTestTick(.3f);
                Check("map: the hero waits a moment before walking on", Mathf.Abs(app.MapHeroX - App.NodeX(1)) < 2);
                app.MapTestTick(3);
                Check("map: the hero walks along the path to the next level", Mathf.Abs(app.MapHeroX - App.NodeX(2)) < 2 && !app.MapWalking);
                app.MapTestTick(.1f, -1);
                Check("map: cannot walk past the last open level", app.MapCursor == 2);
                app.MapTestTick(.05f, 1);
                Check("map: going back starts a walk", app.MapCursor == 1 && app.MapWalking);
                app.MapTestTick(.05f, 0, true);
                Check("map: play pressed on the way waits for the hero to arrive", app.ScreenName == "Map");
                app.MapTestTick(3);
                Check("map: then the level starts", app.ScreenName == "Playing" && app.game.levelIndex == 1);
                Check("map: secrets are counted per level", App.SecretTotal(0) >= 1 && App.SecretTotal(5) >= 2);
                int had = app.save.found[app.game.levelIndex];
                app.game.UseDoor();
                Check("map: entering the bonus room is saved as a found secret", (app.save.found[1] & 1) == 1 && had == 0);
                var kept = Save.Fit(new[] { 7, 3 });
                Check("an older save keeps its stars when levels are added", kept.Length == Levels.All.Length && kept[0] == 7 && kept[1] == 3 && kept[2] == 0);
                app.game.Unload();
            }

            // Version 2.5: the smart helper and the boss entrances
            {
                void DieAt(float x)
                {
                    var gg = app.game; int d = gg.deaths;
                    gg.hero.x = x; gg.hero.y = 2; gg.hero.vx = 0; gg.TestStep(1, none); gg.FallForTest();
                    for (int i = 0; i < 900 && !(gg.deaths > d && !gg.hero.dead); i++) gg.TestStep(1, none);
                }
                g = Fresh(app); app.save.helper = true;
                DieAt(14); DieAt(15);
                Check("helper: two deaths at one spot bring no help box", !app.game.HelpBoxShown && app.game.DeathsHere == 2);
                DieAt(14.5f); g = app.game;
                Check("helper: the third death at the same spot puts a help box by the restart point", g.HelpBoxShown && Mathf.Abs(g.HelpBoxAt.x - g.hero.x) < 3 && app.run.power == 0);
                g.hero.x = g.HelpBoxAt.x; g.hero.y = g.HelpBoxAt.y - .5f; g.TestStep(2, none);
                Check("helper: touching the box grows the hero and gives a bubble shield", !g.HelpBoxShown && app.run.power == 1 && app.run.shield && g.HelpGiven == 1);
                g = Fresh(app); DieAt(12); DieAt(20); DieAt(28);
                Check("helper: deaths at three different spots bring no help box", !app.game.HelpBoxShown && app.game.DeathsHere == 3);
                g = Fresh(app); app.save.helper = false; DieAt(14); DieAt(14); DieAt(14);
                Check("helper: turned off in the settings, no box", !app.game.HelpBoxShown && app.game.DeathsHere == 3);
                app.save.helper = true; app.game.Unload();

                var bosses = Levels.All.Where(l => l.boss).ToArray();
                Check("every boss has its own battle music, apart from the level's music", bosses.Length == 3 && bosses.Select(l => l.bossMusic).Distinct().Count() == 3
                    && bosses.All(l => Array.IndexOf(Sound.Tracks, l.bossMusic) >= 0 && l.music != l.bossMusic && !string.IsNullOrEmpty(l.bossTitle)));
                foreach (var def in bosses)
                {
                    int bi = Array.IndexOf(Levels.All, def);
                    app.run = new RunState { lives = 9 }; app.game.skipBossIntro = false; app.game.StartLevel(bi); g = app.game; g.god = true;
                    var b = g.boss; float bx0 = b.x, by0 = b.y;
                    Check($"{def.file}: the level starts with its world's music", Sound.Current == def.music);
                    g.hero.x = b.x - 15.9f; g.hero.y = b.y + .5f; g.hero.vx = 0; g.TestStep(1, none);
                    Check($"{def.file}: coming near starts the boss's entrance, with its own music", g.BossIntroOn && b.awake && Sound.Current == def.bossMusic);
                    float hx = g.hero.x; g.TestStep(150, new Pad { x = 1, run = true });
                    Check($"{def.file}: during the entrance the name fills the screen, and nobody moves", g.BossIntroOn && app.hud.BossTitleText == Rtl.V(def.name)
                        && Mathf.Abs(g.hero.x - hx) < .3f && b.x == bx0 && b.y == by0 && b.hp == Boss.MaxHp);
                    g.TestStep(Mathf.CeilToInt((Game.BossIntroTime - 1.2f) * Tune.SimHz), none);
                    Check($"{def.file}: then the fight begins and the name goes away", !g.BossIntroOn && app.hud.BossTitleText == "");
                }
                app.run = new RunState { lives = 9 }; app.game.StartLevel(Array.IndexOf(Levels.All, bosses[1])); g = app.game; g.god = true;
                g.hero.x = g.boss.x - 15.9f; g.hero.y = g.boss.y + .5f; g.TestStep(1, none); g.TestStep(110, none);
                g.TestStep(1, new Pad { jumpPressed = true, jumpHeld = true }); g.TestStep(1, none);
                Check("a jump skips the rest of the entrance", !g.BossIntroOn);
                g.god = false; g.skipBossIntro = true; g.Unload();
            }

            // Version 2.9: the holiday levels
            {
                var days = new (int y, int m, int d, int h)[] { (2026, 12, 5, 0), (2026, 12, 12, 0), (2026, 12, 13, -1), (2027, 3, 23, 1), (2027, 3, 24, 1), (2027, 3, 25, -1),
                    (2027, 4, 22, 2), (2027, 4, 28, 2), (2027, 4, 29, -1), (2027, 10, 2, 4), (2027, 10, 3, 4), (2027, 10, 4, -1), (2027, 10, 16, 3), (2027, 10, 23, 3), (2027, 10, 24, -1), (2026, 11, 9, -1) };
                var wrong = days.Where(t => Holidays.OnDay(new DateTime(t.y, t.m, t.d)) != t.h).Select(t => $"{t.y}-{t.m}-{t.d}").ToArray();
                Check($"calendar: 16 days checked against Hebcal, from Hanukkah 5787 to Sukkot 5788 {string.Join(" ", wrong)}", wrong.Length == 0);
                var hd = Holidays.Hebrew(new DateTime(2026, 12, 5));
                Check("calendar: 5 December 2026 is 25 Kislev 5787", hd.year == 5787 && hd.month == 3 && hd.day == 25);
                int hk = Levels.HolidayLevel(0), pu = Levels.HolidayLevel(1);
                Check("holidays: five levels, one for each holiday, each with its pictures", Levels.All.Count(l => l.Holiday) == 5 && hk == Levels.HolidayFirst
                    && Levels.All.Where(l => l.Holiday).All(l => Grid2.Parse(Levels.Text(l.file)).Decos.Count > 0 && Resources.Load<Texture2D>("Art/" + Holidays.All[l.holiday].coin) != null));
                var hKeep = app.save; app.save = new Save(); app.save.unlocked = 3;
                Holidays.Today = new DateTime(2026, 11, 9); app.RefreshHolidays();
                Check("holidays: on an ordinary day nothing opens", app.save.holidays == 0 && !app.HolidayOpen(hk));
                Holidays.Today = new DateTime(2026, 12, 6); app.RefreshHolidays();
                Holidays.Today = new DateTime(2026, 12, 20); app.RefreshHolidays();
                Check("holidays: Hanukkah opens its level, and it stays open after the holiday", app.HolidayOpen(hk) && !app.HolidayOpen(pu));
                Holidays.Today = new DateTime(2026, 12, 8); app.mapHold = true; app.ShowMap();
                Check("holidays: during the holiday the map shows its badge", app.HolidayBadgeShown);
                app.MapHolidaysForTest();
                Check("holidays: the box opens the holiday page on today's holiday", app.OnHolidayPage && app.MapCursor == hk);
                app.MapTestTick(.1f, -1); app.MapTestTick(.1f, 0, true); app.MapTestTick(.5f);
                Check("holidays: a holiday still shut cannot be played", app.ScreenName == "Map" && app.MapCursor == pu);
                app.MapTestTick(.1f, 1); app.MapTestTick(.1f, 0, true); app.MapTestTick(1);
                Check("holidays: an open one can", app.ScreenName == "Playing" && app.game.levelIndex == hk);
                app.game.WinForTest(); app.LevelDone();
                Check("holidays: finishing it goes back to the holiday page", app.ScreenName == "Map" && app.OnHolidayPage && app.MapCursor == hk && app.save.unlocked == 3);
                app.MapHolidaysForTest();
                Check("holidays: and the box goes back to the journey", !app.OnHolidayPage);
                Holidays.Today = new DateTime(2027, 3, 23); app.RefreshHolidays(); int hat0 = Outfit.Hat;
                app.Enter(pu);
                Check("purim: the hero comes in costume", app.game.InCostume && Outfit.Hat > Outfit.StarHats);
                app.game.Unload();
                Check("purim: and takes it off after the level", Outfit.Hat == hat0 && !app.game.InCostume);
                Holidays.Today = null; app.mapHold = false; app.save = hKeep;
            }

            // Version 2.8: the bonus levels
            {
                int b1 = Array.FindIndex(Levels.All, l => l.file == "b1"), b2 = Array.FindIndex(Levels.All, l => l.file == "b2"), b3 = Array.FindIndex(Levels.All, l => l.file == "b3");
                Check("bonus: three levels after the story, each opened by a level of worlds 1 to 3", b1 == Levels.StoryCount && b2 == b1 + 1 && b3 == b2 + 1
                    && Levels.ParentOf(b1) == 2 && Levels.ParentOf(b2) == 4 && Levels.ParentOf(b3) == 8 && Levels.BonusOf(2) == b1);
                app.run = new RunState { lives = 9 }; app.game.StartLevel(b2); g = app.game; g.RemoveEnemies();
                int seaDeaths = g.deaths; g.hero.x = 10; g.hero.y = 6; g.hero.vy = 0; g.TestStep(120, none); float sank = 6 - g.hero.y;
                Check($"sea: the hero sinks slowly in the water ({sank:F1} tiles in a second)", g.hero.swimming && sank > 1.5f && sank < 4 && g.deaths == seaDeaths);
                float by0 = g.hero.y; g.TestStep(1, new Pad { jumpPressed = true, jumpHeld = true }); g.TestStep(30, none);
                Check("sea: the jump button is a stroke up", g.hero.y > by0 + .8f);
                g.hero.x = 18; g.hero.y = 10; g.TestStep(1, none); float bx0 = g.hero.x; g.TestStep(120, new Pad { x = 1 });
                Check($"sea: swimming is slower than walking ({g.hero.x - bx0:F1} tiles in a second)", g.hero.x - bx0 > 2.5f && g.hero.x - bx0 < Tune.WalkSpeed);
                g.Unload(); app.run = new RunState { lives = 9 }; app.game.StartLevel(b3); g = app.game; g.RemoveEnemies(); g.god = true;
                g.hero.x = 80; g.hero.y = 2; g.TestStep(1, none); bx0 = g.hero.x; g.TestStep(60, none);
                Check($"cart: rolls on by itself ({(g.hero.x - bx0) * 2:F1} tiles a second)", Mathf.Abs((g.hero.x - bx0) * 2 - Player.CartSpeed) < .3f && g.InCart);
                g.TestStep(300, none);
                Check($"cart: a wall stops it at the station ({g.hero.x:F1})", g.hero.x > 103 && g.hero.x < 104 && g.hero.vx >= 0);
                g.TestStep(1, new Pad { jumpPressed = true, jumpHeld = true }); g.TestStep(90, new Pad { jumpHeld = true });
                Check("cart: a jump clears the wall and it rolls on", g.hero.x > 106);
                g.god = false; g.Unload(); app.run = new RunState { lives = 9 }; app.game.StartLevel(b1); g = app.game; g.RemoveEnemies();
                g.hero.x = 20; g.hero.y = 2; g.TestStep(1, none); float bleft0 = g.ScrollLeft; g.TestStep(240, none);
                Check($"scroll: the screen moves on by itself ({g.ScrollLeft - bleft0:F1} tiles in 2 seconds)", Mathf.Abs(g.ScrollLeft - bleft0 - 6.4f) < .3f);
                g.TestStep(600, none);
                Check("scroll: its edge pushes a hero who stands still", g.hero.x >= g.ScrollLeft + g.hero.w / 2 - .05f && g.hero.x > 21);
                int bd0 = g.deaths; g.hero.x = 62.4f; g.hero.y = 2; g.hero.vx = 0; g.ScrollLeft = 59; g.TestStep(240, none);
                Check("scroll: pushed into a wall, the hero is lost", g.deaths == bd0 + 1);
                g.Unload();
                var bkeepS = app.save; app.save = new Save(); app.save.unlocked = 4;
                Check("bonus: shut until its level is finished", !app.BonusOpen(b1));
                app.save.best[2] = 500;
                Check("bonus: open once it is", app.BonusOpen(b1) && !app.BonusOpen(b2) && !app.TrialOpen(b1));
                app.mapHold = true; app.MapTestFinished(1); app.MapTestTick(3);
                app.MapBonusForTest(1);
                Check("bonus: up on the map from its level chooses it", app.MapCursor == b1);
                app.MapTestTick(.1f, 0, true); app.MapTestTick(1);
                Check("bonus: and plays it", app.ScreenName == "Playing" && app.game.levelIndex == b1);
                app.game.WinForTest(); app.LevelDone();
                Check($"bonus: finishing it opens nothing new and goes back to its stop ({app.save.unlocked} {app.MapCursor} {app.ScreenName} {app.save.best[b1]})", app.save.unlocked == 4 && app.MapCursor == b1 && app.ScreenName == "Map" && app.save.best[b1] > 0);
                app.MapBonusForTest(-1);
                Check("bonus: down goes back to the path", app.MapCursor == 2);
                app.mapHold = false; app.save = bkeepS;
            }

            // Version 2.7: animals to ride
            {
                g = Fresh(app); Under(g, 16.5f, 3.5f); g.TestStep(120, none);
                Check("ride: the animal box lets out an animal that waits on the box", g.pickups.Count == 1 && g.pickups[0].type == "ride");
                for (int i = 0; i < 300 && g.pickups.Count > 0; i++) { var k = g.pickups[0]; g.hero.x = k.x; g.hero.y = k.y; g.hero.vy = 0; g.TestStep(1, none); }
                g.hero.x = 8; g.hero.y = 2; g.TestStep(60, none);
                Check("ride: touching it mounts the ram, as tall as the big hero", g.Riding == 0 && g.hero.big && app.run.power == 0 && g.RideSpriteName.StartsWith("ride_ram"));
                int d0 = g.deaths, l0 = app.run.lives; var en = g.TestEnemy('p', g.hero.x + 3, 2); g.hero.invuln = 0; Above(g.hero, en); g.TestStep(30, none);
                Check("ride: a hit costs only the animal, not a life or the power", g.Riding == -1 && g.deaths == d0 && app.run.lives == l0 && !g.hero.big && g.hero.invuln > 0);

                g = Fresh(app); g.MountForTest(0); g.hero.x = 6; g.hero.y = 2; en = g.TestEnemy('e', 12, 2); int h0 = g.hits;
                g.TestStep(150, new Pad { x = 1, run = true });
                Check("ram: running into an enemy knocks it away, unhurt", !en.alive && g.hits == h0 && g.Riding == 0);

                float Height(int kind)
                {
                    var gg = Fresh(app); if (kind >= 0) gg.MountForTest(kind); else gg.ApplyPowerForTest(1);
                    gg.hero.x = 8; gg.hero.y = 2; gg.TestStep(40, none); float y0 = gg.hero.y, top = y0;
                    gg.TestStep(1, new Pad { jumpPressed = true, jumpHeld = true });
                    for (int i = 0; i < 150; i++) { gg.TestStep(1, new Pad { jumpHeld = true }); top = Mathf.Max(top, gg.hero.y); }
                    return top - y0;
                }
                float bigH = Height(-1), camelH = Height(1);
                Check($"camel: jumps higher than the big hero ({camelH:F1} against {bigH:F1} tiles)", camelH > bigH * 1.25f);
                int w23 = Array.FindIndex(Levels.All, l => l.file == "w2l3"), w32 = Array.FindIndex(Levels.All, l => l.file == "w3l2");
                app.run = new RunState { lives = 9 }; app.game.StartLevel(w23); g = app.game; g.RemoveEnemies();
                g.MountForTest(1); g.hero.x = 50.5f; g.hero.y = 2; g.hero.invuln = 0; h0 = g.hits; g.TestStep(60, none);
                Check("camel: stands on spikes unhurt", g.hits == h0 && g.Riding == 1 && g.grid.Spike(50, 2));
                g.Unload(); app.run = new RunState { lives = 9 }; app.game.StartLevel(w23); g = app.game; g.RemoveEnemies();
                g.hero.x = 50.5f; g.hero.y = 2; g.hero.invuln = 0; h0 = g.hits; g.TestStep(60, none);
                Check("spikes still hurt without the camel", g.hits > h0);

                g.Unload(); app.run = new RunState { lives = 9 }; app.game.StartLevel(w32); g = app.game; g.RemoveEnemies(); g.god = true;
                g.MountForTest(2); g.hero.x = 48; g.hero.y = 3; int bricks = g.blocks.Count(b => b.c == 55); h0 = g.hits; bool slid = false;
                g.TestStep(40, none); for (int i = 0; i < 260; i++) { g.TestStep(1, new Pad { x = 1, run = true }); slid |= g.SlidingNow; }
                Check($"penguin: slides when running, and the slide breaks the wall of ice ({bricks} blocks)", bricks == 2 && g.blocks.Count(b => b.c == 55) == 0 && slid && g.hero.x > 56);
                g.god = false; g.Unload();

                float Fall(bool glide)
                {
                    var gg = Fresh(app); gg.MountForTest(3); gg.TestStep(40, none); gg.hero.x = 50; gg.hero.y = 13; gg.hero.vy = 0; gg.hero.grounded = false;
                    gg.TestStep(60, new Pad { jumpHeld = glide }); return gg.hero.vy;
                }
                Check($"bird: holding jump glides down slowly ({Fall(true):F1} against {Fall(false):F1} tiles a second)", Fall(true) >= -Player.GlideFall - .01f && Fall(false) < -10);

                g = Fresh(app); g.MountForTest(1); g.TestStep(40, none); g.hero.x = 62.5f; g.hero.y = 2; g.TestStep(30, new Pad { up = true });
                Check("ride: the animal stays behind at a ladder", g.Riding == -1 && g.hero.climbing);
                g = Fresh(app); g.MountForTest(0); g.TestStep(40, none); g.FallForTest(); g.TestStep(10, none);
                Check("ride: a fall loses the animal too", g.Riding == -1);

                var withBox = Levels.All.Where(l => Grid2.Parse(Levels.Text(l.file)).Blocks.Exists(b => b.kind == BlockKind.Animal)).Select(l => l.file).ToArray();
                Check("ride: one animal box in a level of each world: " + string.Join(" ", withBox), withBox.SequenceEqual(new[] { "w1l2", "w2l3", "w3l2", "w4l2" })
                    && Levels.All.All(l => Grid2.Parse(Levels.Text(l.file)).Blocks.Count(b => b.kind == BlockKind.Animal) <= 1));
                app.game.Unload();
            }

            // Version 2.6: the coin shop
            {
                var keep = app.save;
                app.save = new Save { coinsTotal = 137 }; app.StartWalletForTest();
                Check("shop: the coins found before the shop became the first money", app.save.wallet == 137 && app.save.walletStarted);
                app.save.coinsTotal = 200; app.StartWalletForTest();
                Check("shop: that happens only once", app.save.wallet == 137);
                app.save = keep;
                g = Fresh(app); int w0 = app.save.wallet; g.AddCoinForTest();
                Check("shop: a coin in a level goes into the wallet", app.save.wallet == w0 + 1);
                g.trial = true; g.AddCoinForTest(); g.trial = false;
                Check("shop: a coin in a time trial does not", app.save.wallet == w0 + 1);
                g.Unload();
                app.save.wallet = 10; app.run = new RunState { lives = 3 }; app.BuyLife();
                Check("shop: too few coins buy nothing", app.run.lives == 3 && app.save.wallet == 10);
                app.save.wallet = App.PriceLife + App.PriceSevivon + App.PriceBubble; app.BuyLife();
                Check("shop: an extra life", app.run.lives == 4 && app.save.wallet == App.PriceSevivon + App.PriceBubble);
                app.BuyBoost(2, false, App.PriceSevivon); app.BuyBoost(0, true, App.PriceBubble); app.BuyBoost(0, true, App.PriceBubble);
                Check("shop: a sevivon and a bubble wait for the next level, and one of each is enough", app.save.wallet == 0 && app.save.boostPower == 2 && app.save.boostShield);
                app.Enter(0); g = app.game;
                Check("shop: the next level starts with them, and they are used up", app.run.power == 2 && app.run.shield && g.hero.shooter && app.save.boostPower == 0 && !app.save.boostShield);
                g.Unload();
                Check("shop: its outfits do not open with stars", !Outfit.Open(true, Outfit.StarHats + 1, 999) && !Outfit.Open(false, Outfit.StarPants + 1, 999) && Outfit.ItemCount == 10 && Outfit.ShopCount == 6);
                app.save.wallet = 1000; app.BuyOutfit(0);
                Check("shop: a hat bought is owned and worn", Outfit.Open(true, Outfit.StarHats + 1, 0) && Outfit.Hat == Outfit.StarHats + 1 && app.save.hat == Outfit.Hat && app.save.wallet == 1000 - Outfit.Price(true));
                app.BuyOutfit(0);
                Check("shop: wearing it again costs nothing", app.save.wallet == 1000 - Outfit.Price(true));
                app.BuyOutfit(3);
                Check("shop: pants bought are worn, in every picture", Outfit.Pant == Outfit.StarPants + 1 && Art.Hero('f', "climb2") != null && Art.Hero('f', "climb2").name.EndsWith("_gold")
                    && Resources.Load<Texture2D>("Art/hat_cowboy_s") != null && Resources.Load<Texture2D>("Art/pants_gray") != null);
                app.WearForTest(true, 1);
                Check("shop: the wardrobe goes on from a bought hat to the next open one", Outfit.Hat != Outfit.StarHats + 1);
                int avg = (App.PriceLife + App.PriceBig + App.PriceSevivon + App.PriceBubble + Outfit.Price(true) * 3 + Outfit.Price(false) * 3) / 10;
                Check($"shop: two things cost about {2 * avg} coins on average", 2 * avg >= 50 && 2 * avg <= 75);
                Outfit.Hat = Outfit.Pant = 0; app.save.hat = app.save.pant = 0; app.save.shopOwned = Outfit.Owned = 0; app.save.wallet = 0;
            }

            w.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
            File.WriteAllText(Path.Combine(dir, "selftest.txt"), w.ToString());
        }
    }

    /// Measures the controller on a flat floor (-feeltest), with no rendering involved.
    public static class FeelTest
    {
        const float Dt = 1f / Tune.SimHz;

        static Player Fresh()
        {
            var p = new Player { grid = Grid2.Flat(3000, 2), x = 20, y = 2 };
            p.SetBig(false);
            p.Step(Dt, default);
            return p;
        }

        static (float height, float air, float dist) Jump(bool run, float holdFor, bool runUp)
        {
            var p = Fresh();
            if (runUp) for (int i = 0; i < 300; i++) p.Step(Dt, new Pad { x = 1, run = run });
            float y0 = p.y, x0 = p.x, top = y0, t = 0;
            p.Step(Dt, new Pad { x = runUp ? 1 : 0, run = run, jumpPressed = true, jumpHeld = true });
            while (!p.grounded && t < 5)
            {
                t += Dt;
                p.Step(Dt, new Pad { x = runUp ? 1 : 0, run = run, jumpHeld = t < holdFor });
                top = Mathf.Max(top, p.y);
            }
            return (top - y0, t, p.x - x0);
        }

        static float Speed(bool run, float seconds)
        {
            var p = Fresh();
            for (float t = 0; t < seconds; t += Dt) p.Step(Dt, new Pad { x = 1, run = run });
            return p.vx;
        }

        public static void Run(string dir)
        {
            Directory.CreateDirectory(dir);
            var w = new StringWriter();
            w.WriteLine($"walk top speed {Speed(false, 3):F2} t/s, run top speed {Speed(true, 3):F2} t/s");
            foreach (var (name, run, hold, up) in new[] { ("tap standing", false, 0f, false), ("full standing", false, 9f, false), ("full walking", false, 9f, true), ("full running", true, 9f, true) })
            {
                var j = Jump(run, hold, up);
                w.WriteLine($"{name}: height {j.height:F2} tiles, air {j.air:F2}s, distance {j.dist:F2} tiles");
            }
            File.WriteAllText(Path.Combine(dir, "feel_metrics.txt"), w.ToString());
        }
    }
}
