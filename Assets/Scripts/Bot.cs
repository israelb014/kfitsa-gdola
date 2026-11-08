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
            G.StartLevel(level);
            if (!enemies) G.RemoveEnemies();
        }

        void Level(LevelDef def)
        {
            int index = Array.IndexOf(Levels.All, def);
            Note("--- " + def.file + " " + def.Code);
            bool stormy = Grid2.Parse(Levels.Text(def.file)).Storm != null;
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
            if (mode == "lights")
                for (int i = 0; i < 6 && !G.RoomSolved; i++) Drive(def, q => q.kind == "lamp" && !G.LampLit(q.x, q.y), true, 10);
            if (mode == "stomp" || mode == "blocks")
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
            if (easy)
            {
                int total = G.grid.Enemies.FindAll(e => e.at.x < G.grid.MainW).Count, room = G.grid.Enemies.Count - total;
                Check($"{def.file} easy: half the enemies and two more flags", (total == 0 || G.EnemyCount - room <= (total + 1) / 2) && G.FlagCount == G.grid.FlagSpots(false).Count + G.grid.EasyFlags.Count,
                    $"{G.EnemyCount - room} of {total}, {G.FlagCount} flags");
            }
            int plans = 0;
            while (G.Running && !G.Won && G.levelTime < 400 && plans < 10)
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
            Check($"{def.file}{(easy ? " easy" : "")}: played start to finish with enemies", G.Won, $"{G.levelTime:F1}s, {G.deaths} falls, {G.hits} hits, {plans} routes");
        }

        void FightBoss()
        {
            app.run.power = 2; G.ApplyPowerForTest(2);
            for (int i = 0; i < 12000 && G.boss.alive; i++)
            {
                var b = G.boss;
                float dx = b.x - G.hero.x;
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
            "flag_storm", "frost", "dust_shield", "boss_warn", "boss_shadow", "hand", "map_walk", "map_full", "w3l1", "w3l1_ladder", "w3l1_rope", "world_cleared", "w3l2_clouds", "w3l2_worm", "w3l3_sleepers", "w3l3_squeak", "boss2_throw", "boss2_stuck" };
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
                G.skipIntro = true; app.run = new RunState { power = power };
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
            var g = app.game; g.skipIntro = true; var none = new Pad();

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

            // Version 2.0: the frost giant
            {
                int gi = Array.FindIndex(Levels.All, l => l.file == "boss2");
                app.run = new RunState { lives = 9 }; app.game.skipIntro = true; app.game.StartLevel(gi); g = app.game; g.god = true;
                var b = g.boss;
                Check("the frost giant is the last level's boss", b != null && b.frost && gi == Levels.All.Length - 1);
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
