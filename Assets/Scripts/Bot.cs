using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Kfitsa
{
    /// Test driver (-bot): plays each level by simple rules at high speed, proving it can be finished.
    /// Enemies cannot hurt it, so a failure means the route itself is impossible for these rules.
    public class Bot
    {
        readonly App app; readonly string dir; readonly StringWriter log = new StringWriter();
        readonly int only;
        int index, shots;
        float hold, levelClock, nextShot, waitStuck, lastX;
        bool wasAir, done;
        public int StepsPerFrame = 16;
        Game G => app.game;

        public Bot(App a, string captureDir, string[] args)
        {
            app = a; dir = captureDir; Directory.CreateDirectory(dir);
            int i = System.Array.IndexOf(args, "-level");
            only = i >= 0 ? int.Parse(args[i + 1]) : -1;
            if (only >= 0) index = only;
        }

        public void Begin() { QualitySettings.vSyncCount = 0; Application.targetFrameRate = 200; Next(); }

        void Next()
        {
            if (index >= Levels.All.Length || only >= 0 && index != only) { Finish(); return; }
            app.run = new RunState { lives = 99, power = 2 };
            G.god = true;
            G.StartLevel(index);
            levelClock = 0; nextShot = 2; hold = 0; lastX = 0; waitStuck = 0; shots = 0;
        }

        void Log(string line)
        {
            log.WriteLine(line);
            File.WriteAllText(Path.Combine(dir, "bot_levels.txt"), log.ToString());
        }

        void Finish() { Log("end"); done = true; Application.Quit(); }

        public void LevelFinished()
        {
            Log($"{Levels.All[index].file}: finished time={G.levelTime:F1}s deaths={G.deaths} hits={G.hits} coins={G.levelCoins}");
            G.deaths = 0; G.hits = 0; G.levelCoins = 0;
            index++; Next();
        }

        public void Tick()
        {
            if (done || !G.Running) return;
            if (levelClock > nextShot && shots < 3)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"lvl_{Levels.All[index].file}_{shots}.png"));
                shots++; nextShot = levelClock + 10;
            }
        }

        public Pad Read(float dt)
        {
            levelClock += dt;
            var p = new Pad { x = 1, run = true };
            var h = G.hero;
            if (levelClock > 150)
            {
                Log($"{Levels.All[index].file}: STUCK x={h.x:F1} y={h.y:F1} deaths={G.deaths}");
                G.deaths = 0; G.hits = 0; G.levelCoins = 0;
                index++; Next(); return p;
            }
            if (G.Won) { p.jumpPressed = true; return p; }
            if (h.grounded && wasAir) hold = 0;
            wasAir = !h.grounded;
            int feet = Mathf.FloorToInt(h.y + .01f);
            int ahead = Mathf.FloorToInt(h.Right + .5f + Mathf.Abs(h.vx) * .05f);

            bool pit = true;
            for (int r = 0; r < feet; r++) pit &= !G.grid.Solid(ahead, r);
            if (feet == 0) pit = false;
            bool wall = false;
            for (int r = feet; r <= feet + 1; r++) wall |= G.grid.Solid(ahead, r) || G.grid.Hazard(ahead, r);
            for (int r = feet - 1; r <= feet; r++) wall |= G.grid.Deadly(ahead, r);

            // A wide pit: ride a moving platform across if there is one
            int land = Landing(ahead, feet);
            Platform ride = null; float best = 99;
            foreach (var m in G.grid.Platforms)
            {
                float d = m.x - h.x;
                if (d > -1 && d < best && d < 16) { best = d; ride = m; }
            }
            bool jump = pit || wall;
            if (h.on != null)
            {
                // Stay on until the far side is within reach
                int c = Mathf.FloorToInt(h.x);
                int far = Landing(c + 1, feet);
                bool nextRide = false;
                foreach (var m in G.grid.Platforms)
                    if (m != h.on && m.x > h.x && m.Left - h.Right < 5 && m.Top < h.y + 3.5f) nextRide = true;
                p.run = false;
                if (far - c <= 5 || nextRide) { jump = true; }
                else { p.x = Mathf.Clamp(h.on.x - h.x, -1, 1); jump = false; }
            }
            else if (pit && h.grounded && land - ahead > 8 && ride != null)
            {
                // wait at the edge until the ledge comes close
                bool near = ride.Left - h.Right < 4.5f && ride.Top < h.y + 3.5f;
                jump = near; if (!near) { p.x = 0; p.run = false; }
            }

            if (G.boss != null && G.boss.alive && G.boss.awake)
            {
                p.x = Mathf.Sign(G.boss.x - h.x); p.run = false;
                jump = Mathf.Abs(G.boss.x - h.x) < 3.5f;
                p.runPressed = levelClock % 1f < dt;
            }

            // Stuck against something: a full jump usually clears it, then back off and retry
            if (Mathf.Abs(h.x - lastX) < .02f && p.x != 0) waitStuck += dt; else waitStuck = 0;
            lastX = h.x;
            if (waitStuck > .4f) jump = true;
            if (waitStuck > 1.5f) { p.x = -1; }
            if (waitStuck > 2.2f) waitStuck = 0;

            if (h.grounded && jump && hold <= 0) { p.jumpPressed = true; hold = .5f; }
            if (hold > 0) { p.jumpHeld = true; hold -= dt; }
            return p;
        }

        /// First column at or after c with standing room near the given height.
        int Landing(int c, int feet)
        {
            for (int k = 0; k < 30; k++)
                for (int r = feet + 3; r >= Mathf.Max(0, feet - 8); r--)
                    if (G.grid.Solid(c + k, r)) return c + k;
            return c + 30;
        }
    }

    /// Visits every screen once for screenshots (-shots).
    public class ScreenTour : MonoBehaviour
    {
        App app; string dir; int step; float t; bool shot;
        static readonly string[] Names = { "title", "map", "scores", "settings", "credits", "level1", "room", "boss", "desert", "ending", "pause" };

        public void Begin(App a, string captureDir) { app = a; dir = captureDir; Directory.CreateDirectory(dir); app.ShowTitle(); t = 1.5f; }

        void Update()
        {
            t -= Time.unscaledDeltaTime;
            if (t > 0) return;
            t = 1f;
            if (step >= Names.Length) { Application.Quit(); return; }
            if (!shot) { ScreenCapture.CaptureScreenshot(Path.Combine(dir, "screen_" + Names[step] + ".png")); shot = true; t = .2f; return; }
            shot = false;
            step++;
            switch (step)
            {
                case 1: app.save.unlocked = 4; app.run = new RunState(); app.ShowMap(); break;
                case 2: app.save.AddScore(12400, "הגיע לשלב 1-2"); app.save.AddScore(8300, "הגיע לשלב 1-1"); app.ShowScores(app.ShowTitle); break;
                case 3: app.ShowSettings(app.ShowTitle); break;
                case 4: app.ShowCreditsForTour(); break;
                case 5: app.Enter(0); t = 2.6f; break;
                case 6: app.game.hero.x = app.game.grid.Door.x; app.game.hero.y = app.game.grid.Door.y; app.game.UseDoor(); t = .6f; break;
                case 7: app.game.Unload(); app.run.power = 2; app.Enter(6); t = 3; app.game.hero.x = 36; break;
                case 8: app.game.Unload(); app.Enter(3); t = 2.5f; break;
                case 9: app.game.Unload(); app.ShowEndingForTour(); break;
                case 10: app.Enter(0); app.ShowPause(); break;
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
            Check("tap on the first menu row opens the map", app.ScreenName == "Map", app.ScreenName);
            Vector2 node = mid + new Vector2(720, -120) * app.hud.Scale;
            yield return Tap(node);
            if (app.ScreenName == "Map") { Down(9, node); Up(9, node); yield return Wait(.3f); }   // down and up between two frames
            Check("instant tap on the selected level starts it", app.ScreenName == "Playing", app.ScreenName);
            app.game.god = true;
            yield return Wait(2.6f);
            Check("controls visible while playing", Ui.Visible);
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
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "touch_door.png"));
            yield return Wait(.2f);
            yield return Tap(Ui.Spot("door"));
            yield return Wait(1.2f);
            Check("door button enters the room", app.game.inRoom);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "touch_room.png"));
            yield return Wait(.3f);
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
                case 1: Keys(); Note("after Space on title: " + app.ScreenName); break;
                case 2: Keys(Key.Space); break;
                case 3: Keys(); Note("after Space on map: " + app.ScreenName); break;
                case 4: break;
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
    public static class SelfTest
    {
        static StringWriter w; static int fails;
        static void Check(string name, bool ok) { w.WriteLine((ok ? "PASS " : "FAIL ") + name); if (!ok) fails++; }

        static Game Fresh(App app, int level, int power = 0)
        {
            app.run = new RunState { lives = 9, power = power };
            app.game.StartLevel(level);
            return app.game;
        }

        static void Above(Player h, Body e) { h.x = e.x; h.y = e.Top + .4f; h.vy = -6; h.vx = 0; h.grounded = false; }

        public static void Run(App app, string dir)
        {
            Directory.CreateDirectory(dir); w = new StringWriter();
            var g = app.game; g.skipIntro = true; var none = new Pad();

            g = Fresh(app, 0); var e = g.TestEnemy('e', 8, 2); Above(g.hero, e); int s0 = app.run.score; bool bounced = false;
            for (int i = 0; i < 30; i++) { g.TestStep(1, none); bounced |= g.hero.vy > 5; }
            Check("stomp slime squashes it, scores and bounces", !e.alive && app.run.score > s0 && bounced);

            g = Fresh(app, 0); e = g.TestEnemy('n', 12, 2); Above(g.hero, e); g.TestStep(30, none);
            Check("stomp snail hides it in the shell", e.state == 1 && e.alive);
            g.hero.x = e.x - 1.2f; g.hero.y = 2; g.hero.vy = 0; g.hero.invuln = 0; g.TestStep(90, new Pad { x = 1 });
            Check("touching the shell kicks it", e.state == 2);
            var other = g.TestEnemy('e', e.x + 1.5f, 2); other.dir = -1; g.TestStep(60, none);
            Check("sliding shell knocks out another enemy", !other.alive);

            g = Fresh(app, 0, 1); e = g.TestEnemy('p', 8, 2); Above(g.hero, e); g.TestStep(30, none);
            Check("spike slime cannot be stomped and costs the power", e.alive && app.run.power == 0 && g.deaths == 0);
            Check("hero survives one hit when big", g.hero.invuln > 0);

            g = Fresh(app, 0); e = g.TestEnemy('y', 8, 3); g.TestStep(2, none); Above(g.hero, e); g.TestStep(30, none);
            Check("stomp fly drops it", !e.alive);

            g = Fresh(app, 0); e = g.TestEnemy('j', 14, 2); float x0 = e.x; bool leapt = false;
            for (int i = 0; i < 300; i++) { g.TestStep(1, none); leapt |= !e.grounded && e.y > 2.3f; }
            Check("frog hops", leapt && e.x != x0);

            g = Fresh(app, 0, 2); e = g.TestEnemy('p', g.hero.x + 3, 2); g.TestStep(2, none); g.hero.facing = 1; g.TestStep(50, new Pad { runPressed = true });
            Check("thrown disc takes out a spike slime", !e.alive);

            g = Fresh(app, 1, 1); g.hero.x = 13.5f; g.hero.y = 4.2f; g.hero.vy = 9; g.hero.grounded = false; g.TestStep(30, none);
            Check("big hero breaks a brick", !g.grid.Solid(13, 6));
            g = Fresh(app, 1, 0); g.hero.x = 13.5f; g.hero.y = 4.2f; g.hero.vy = 9; g.hero.grounded = false; g.TestStep(30, none);
            Check("small hero only bumps a brick", g.grid.Solid(13, 6));

            g = Fresh(app, 0); Check("hidden block starts open", !g.grid.Solid(63, 10));
            g.hero.x = 63.5f; g.hero.y = 8.4f; g.hero.vy = 9; g.hero.grounded = false; int c0 = app.run.coins; g.TestStep(30, none);
            Check("hidden block appears when hit from below", g.grid.Solid(63, 10) && app.run.coins > c0 && app.run.secrets == 1);

            g = Fresh(app, 0); c0 = app.run.coins; g.hero.x = 17.5f; g.hero.y = 4.2f; g.hero.vy = 13; g.hero.grounded = false; g.TestStep(30, none);
            Check("coin block pays out", app.run.coins == c0 + 1);

            g = Fresh(app, 0); g.hero.x = 60.5f; g.hero.y = 4.2f; g.hero.vy = 13; g.hero.grounded = false; g.TestStep(40, none);
            Check("power block releases a gem", g.pickups.Count == 1);
            for (int i = 0; i < 300 && g.pickups.Count > 0; i++) { var k = g.pickups[0]; g.hero.x = k.x; g.hero.y = k.y; g.hero.vy = 0; g.TestStep(1, none); }
            g.TestStep(90, none);
            Check("gem makes the hero big", app.run.power == 1 && g.hero.big);
            g = Fresh(app, 0, 1); g.hero.x = 60.5f; g.hero.y = 4.2f; g.hero.vy = 9; g.hero.grounded = false; g.TestStep(120, none);
            Check("power block gives the red gem to a big hero", g.pickups.Count == 1 && g.pickups[0].type == "shoot");

            g = Fresh(app, 0); g.hero.x = g.grid.Door.x; g.hero.y = g.grid.Door.y; g.TestStep(4, none); g.TestStep(80, new Pad { down = true });
            Check("secret door leads to the bonus room", g.inRoom);
            g.TestStep(4, none); g.TestStep(80, new Pad { down = true });
            Check("room door leads back", !g.inRoom && Mathf.Abs(g.hero.x - g.grid.Door.x) < .6f);

            g = Fresh(app, 0); g.hero.x = g.grid.Checkpoint.x + .5f; g.hero.y = g.grid.Checkpoint.y; g.TestStep(4, none); g.hero.y = -3; g.TestStep(420, none);
            Check("death returns to the checkpoint", g.Running && Mathf.Abs(g.hero.x - g.grid.Checkpoint.x) < .1f && app.run.lives == 8);

            g = Fresh(app, 1); g.hero.x = 48.5f; g.hero.y = 2; g.TestStep(3, none);
            Check("spring launches", g.hero.vy > 15);

            g = Fresh(app, 4); g.hero.x = 30.5f; g.hero.y = .4f; g.TestStep(3, none);
            Check("lava kills", g.deaths > 0 || g.hero.dead);

            g = Fresh(app, 1); var m = g.grid.Platforms[0]; g.hero.x = m.x; g.hero.y = m.Top + .02f; g.hero.vy = 0; g.TestStep(180, none);
            Check("moving platform carries the hero", Mathf.Abs(g.hero.x - m.x) < .3f && g.hero.on == m);

            g = Fresh(app, 6, 1); g.hero.x = 44; g.TestStep(2, none); g.TestStep(10, none);
            var b = g.boss; Check("boss wakes", b != null && b.awake);
            for (int i = 0; i < 4; i++) { Above(g.hero, b); g.hero.vy = -8; g.hero.invuln = 2; g.TestStep(20, none); g.TestStep(180, none); }
            Check("four stomps defeat the boss", !b.alive);

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
