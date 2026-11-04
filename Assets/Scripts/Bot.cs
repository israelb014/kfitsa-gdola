using System.IO;
using UnityEngine;

namespace Kfitsa
{
    /// Test driver (-bot): plays the level with simple rules, proving it can be finished, and saves screenshots.
    public class Bot
    {
        readonly Game g;
        readonly string dir;
        float hold, shotAt = 1.5f, t;
        int shots;
        bool logged;

        public Bot(Game game, string captureDir) { g = game; dir = captureDir; Directory.CreateDirectory(dir); }

        public Pad Read()
        {
            var p = new Pad { x = 1, run = true };
            var h = g.hero;
            if (h.grounded && !h.jumping && wasAir) hold = 0;
            wasAir = !h.grounded;
            int feet = Mathf.FloorToInt(h.y + .01f);
            int ahead = Mathf.FloorToInt(h.Right + .5f + Mathf.Abs(h.vx) * .05f);
            bool pit = true, wall = false, enemy = false, block = false;
            for (int r = 0; r < feet; r++) pit &= !g.grid.Solid(ahead, r);
            wall = g.grid.Solid(ahead, feet) || g.grid.Solid(ahead, feet + 1);

            // Chase the crystal once it is out
            if (g.crystals.Count > 0)
            {
                p.x = Mathf.Sign(g.crystals[0].x - h.x); p.run = false;
                if (g.crystals[0].rise > 0) p.x = 0;
            }
            foreach (var s in g.surprises)
            {
                float dx = s.c + .5f - h.x;
                if (s.used || s.r <= h.y || dx < -.5f || dx > 5) continue;
                p.run = false;
                if (dx < h.vx * h.vx / (2 * Tune.Friction) + .3f) { p.x = 0; block = Mathf.Abs(h.vx) < 1.5f; }   // brake to stop under it
            }
            // While big, walk into one enemy on purpose to prove a hit is survivable
            foreach (var e in g.walkers)
                if (e.alive && e.x > h.x && e.x - h.x < 3.2f && Mathf.Abs(e.y - h.y) < 2 && !h.big) enemy = true;

            if (h.grounded && (pit || wall || enemy || block) && hold <= 0) { p.jumpPressed = true; hold = .42f; }
            if (hold > 0) { p.jumpHeld = true; hold -= Time.unscaledDeltaTime; }
            if (h.big && !sawBig) { sawBig = true; shotAt = t + .1f; }
            if (g.hits > 0 && !sawHit) { sawHit = true; shotAt = t + .1f; }
            return p;
        }

        bool wasAir, sawBig, sawHit;

        public void AfterFrame()
        {
            t += Time.unscaledDeltaTime;
            if (t > shotAt && shots < 16)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"bot_{shots:00}.png"));
                shots++; shotAt = t + 4f;
            }
            if (g.Finished && !logged)
            {
                logged = true;
                File.WriteAllText(Path.Combine(dir, "bot_result.txt"),
                    $"finished=true time={g.levelTime:F1}s deaths={g.deaths} score={g.score} coins={g.coins} lives={g.lives} big={g.hero.big} sawBig={sawBig} survivedHits={g.hits} blocksUsed={g.surprises.FindAll(s => s.used).Count}\n");
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "bot_win.png"));
                quitAt = t + 2.5f;
            }
            if (quitAt > 0 && t > quitAt) Application.Quit();
            if (t > 150)
            {
                File.WriteAllText(Path.Combine(dir, "bot_result.txt"), $"finished=false x={g.hero.x:F1} deaths={g.deaths} lives={g.lives}\n");
                Application.Quit();
            }
        }

        float quitAt;
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

        static float SkidTime()
        {
            var p = Fresh();
            for (int i = 0; i < 300; i++) p.Step(Dt, new Pad { x = 1, run = true });
            float t = 0;
            while (p.vx > 0 && t < 3) { p.Step(Dt, new Pad { x = -1, run = true }); t += Dt; }
            return t;
        }

        static bool CoyoteWorks(float late)
        {
            var g = Grid2.Flat(60, 2);
            for (int c = 25; c < 60; c++) g.Clear(c, 1);
            for (int c = 25; c < 60; c++) g.Clear(c, 0);
            var p = new Player { grid = g, x = 24, y = 2 }; p.SetBig(false); p.Step(Dt, default);
            while (p.grounded) p.Step(Dt, new Pad { x = 1 });
            for (float t = 0; t < late; t += Dt) p.Step(Dt, new Pad { x = 1 });
            p.Step(Dt, new Pad { x = 1, jumpPressed = true, jumpHeld = true });
            return p.jumping;
        }

        static bool BufferWorks(float early)
        {
            var p = Fresh();
            p.y = 12; p.grounded = false; p.vy = 0;
            for (int i = 0; i < 40; i++) p.Step(Dt, default);   // past the coyote window, near top fall speed
            while (p.y > 2 + 17f * early) p.Step(Dt, default);
            p.Step(Dt, new Pad { jumpPressed = true, jumpHeld = true });
            for (int i = 0; i < 40 && !p.jumping; i++) p.Step(Dt, new Pad { jumpHeld = true });
            return p.jumping;
        }

        public static void Run(string dir)
        {
            Directory.CreateDirectory(dir);
            var w = new StringWriter();
            w.WriteLine($"walk top speed {Speed(false, 3):F2} t/s, run top speed {Speed(true, 3):F2} t/s");
            w.WriteLine($"0.5s to walk speed: {Speed(false, .5f):F2}; 0.5s running: {Speed(true, .5f):F2}");
            w.WriteLine($"skid from run to stop: {SkidTime():F2}s");
            foreach (var (name, run, hold, up) in new[] { ("tap standing", false, 0f, false), ("full standing", false, 9f, false), ("tap running", true, 0f, true), ("full walking", false, 9f, true), ("full running", true, 9f, true) })
            {
                var j = Jump(run, hold, up);
                w.WriteLine($"{name}: height {j.height:F2} tiles, air {j.air:F2}s, distance {j.dist:F2} tiles");
            }
            w.WriteLine($"coyote at 0.05s: {CoyoteWorks(.05f)}, at 0.15s: {CoyoteWorks(.15f)}");
            w.WriteLine($"buffer 0.08s before landing: {BufferWorks(.08f)}, 0.25s before: {BufferWorks(.25f)}");
            File.WriteAllText(Path.Combine(dir, "feel_metrics.txt"), w.ToString());
        }
    }
}
