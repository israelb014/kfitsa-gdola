using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kfitsa
{
    /// Run by the build on the built player (-stormtest). Proves the storm's promise: what the player sees
    /// and hears and what pushes the hero are the same thing, tick by tick and frame by frame.
    public class StormTest : MonoBehaviour
    {
        App app; string dir; int fails;
        readonly StringWriter log = new StringWriter();
        readonly List<string> started = new List<string>();
        Game G => app.game;
        Grid2 Grid => G.Level;
        Storm Clock => Grid.Storm;

        public void Begin(App a, string d) { app = a; dir = d; StartCoroutine(Tour()); }

        void Note(string s)
        {
            log.WriteLine(s); Debug.Log("STORM " + s);
            if (!Application.isMobilePlatform) File.WriteAllText(Path.Combine(dir, "storm_report.txt"), log.ToString());
        }

        void Check(string what, bool ok, string detail = "") { if (!ok) fails++; Note((ok ? "PASS " : "FAIL ") + what + (detail == "" ? "" : "  (" + detail + ")")); }

        void Put(float x, float y) { G.hero.x = x; G.hero.y = y; G.hero.vx = 0; G.hero.vy = 0; G.TestStep(1, default); G.SnapCamera(); }

        void Enter(int level)
        {
            app.TestPlay(level);
            G.god = true; G.RemoveEnemies();
        }

        /// Exact, in simulation ticks.
        void Ticks()
        {
            G.paused = true;
            var count = new Dictionary<Weather, int>();
            int wrongPush = 0, rightPush = 0;
            int cycle = Mathf.RoundToInt(Clock.Period * Tune.SimHz);
            for (int i = 0; i < cycle * 3; i++)
            {
                if (G.hero.x < 28) Put(38, 2);
                float was = G.hero.x;
                G.TestStep(1, default);
                var w = Clock.At(G.world.t);
                count[w] = count.TryGetValue(w, out int n) ? n + 1 : 1;
                bool moved = Mathf.Abs(G.hero.x - was) > 1e-5f;
                if (moved != (w == Weather.Storm)) wrongPush++; else if (moved) rightPush++;
            }
            float Seconds(Weather w) => count[w] / 3f / Tune.SimHz;
            Check("rhythm is steady", Mathf.Abs(Seconds(Weather.Calm) - Clock.calm) < .02f && Mathf.Abs(Seconds(Weather.Warning) - Clock.warn) < .02f && Mathf.Abs(Seconds(Weather.Storm) - Clock.blow) < .02f,
                $"calm {Seconds(Weather.Calm):F2}s, warning {Seconds(Weather.Warning):F2}s, storm {Seconds(Weather.Storm):F2}s");
            Check("hero is pushed on every storm tick and on no other", wrongPush == 0 && rightPush > 0, $"{rightPush} pushed, {wrongPush} wrong");

            foreach (var q in Grid.Shelters)
            {
                Put((q.x0 + q.x1) / 2, q.y0);
                while (Clock.At(G.world.t) != Weather.Storm) G.TestStep(1, default);
                float was = G.hero.x; int ticks = 0;
                while (Clock.At(G.world.t) == Weather.Storm) { G.TestStep(1, default); ticks++; }
                Check($"shelter at {q.x0:F0} holds through a whole storm", Mathf.Abs(G.hero.x - was) < 1e-4f && World.PushNow(Grid, G.world, G.hero) == 0, $"moved {G.hero.x - was:F3} in {ticks} ticks");
            }
            Check("wind blows left before the turn", World.StormPush(Grid, 60, 5) < 0);
            Check("wind blows right after the turn", World.StormPush(Grid, 140, 5) > 0);
            G.paused = false;
        }

        /// Real frames: the view, the HUD, the sound and the hero, all at once.
        IEnumerator Frames()
        {
            Put(38, 2);
            int buzz = Haptics.Count, storms = 0, frames = 0;
            int bad = 0, hudBad = 0, calmBad = 0, warnBad = 0, stormBad = 0; string first = "";
            bool shotCalm = false, shotWarn = false, shotStorm = false;
            var was = Weather.Calm; float wasX = G.hero.x, wasT = G.world.t;
            float end = G.world.t + Clock.Period * 2 + 1;
            while (G.world.t < end)
            {
                yield return null;
                if (G.hero.x < 28) { Put(38, 2); wasX = G.hero.x; wasT = G.world.t; continue; }
                var view = G.storm; var w = Clock.At(G.world.t); float k = Clock.Left(G.world.t);
                if (w == Weather.Storm && was != Weather.Storm) storms++;
                frames++;
                if (view.Shown != w) { bad++; first = first == "" ? $"shown {view.Shown}, clock {w}" : first; }
                if (app.hud.StormShown != w) hudBad++;
                float length = w == Weather.Calm ? Clock.calm : w == Weather.Warning ? Clock.warn : Clock.blow;
                bool settled = length - k > .5f;   // fades finished
                if (settled)
                {
                    bool same = wasT == G.world.t;
                    bool moved = G.hero.x < wasX - 1e-4f;
                    if (w == Weather.Calm && (view.GrainsOn != 0 || view.TintAlpha != 0 || view.ArrowPush != 0 || moved)) { calmBad++; first = first == "" ? $"calm: grains {view.GrainsOn}, tint {view.TintAlpha:F2}" : first; }
                    if (w == Weather.Warning && (view.GrainsOn == 0 || view.GrainsOn > 60 || view.TintAlpha != 0 || view.ArrowPush != 0 || view.PalmLean < 4 || moved)) { warnBad++; first = first == "" ? $"warning: grains {view.GrainsOn}, tint {view.TintAlpha:F2}, lean {view.PalmLean:F1}" : first; }
                    if (w == Weather.Storm && (view.GrainsOn < 120 || view.TintAlpha < .3f || view.ArrowPush >= 0 || view.PalmLean < 20 || Mathf.Abs(view.HeroTilt) < 5 || !same && !moved || Sound.WindVolume <= 0)) { stormBad++; first = first == "" ? $"storm: grains {view.GrainsOn}, tint {view.TintAlpha:F2}, lean {view.PalmLean:F1}, tilt {view.HeroTilt:F1}, wind {Sound.WindVolume:F2}" : first; }
                    if (!shotCalm && w == Weather.Calm && length - k > 1) { shotCalm = true; yield return Shot("calm"); }
                    if (!shotWarn && w == Weather.Warning && length - k > 1) { shotWarn = true; yield return Shot("warning"); }
                    if (!shotStorm && w == Weather.Storm && length - k > 1.5f) { shotStorm = true; yield return Shot("storm"); }
                }
                was = w; wasX = G.hero.x; wasT = G.world.t;
            }
            Check("view matches the clock on every frame", bad == 0, $"{frames} frames {first}");
            Check("HUD matches the clock on every frame", hudBad == 0);
            Check("calm: no sand, no tint, no arrow, no push", calmBad == 0);
            Check("warning: light sand, palms lean, sky only, no push", warnBad == 0);
            Check("storm: dense sand, tint, palms bent, hero leans, arrow, sound, push", stormBad == 0, first);
            Check("one vibration at the start of each storm", Haptics.Count - buzz == storms && storms >= 2, $"{Haptics.Count - buzz} for {storms} storms");
            Check("wind sounds started in order", started.IndexOf("wind wind_rise") >= 0 && started.IndexOf("wind wind_storm") > started.IndexOf("wind wind_rise"), string.Join(", ", started));
        }

        IEnumerator Shot(string name)
        {
            if (Application.isBatchMode || Application.isMobilePlatform) yield break;
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"storm_{name}_{Screen.width}x{Screen.height}.png"));
            yield return null;
        }

        IEnumerator Tour()
        {
            Note("version " + Application.version + " on " + Application.platform);
            Sound.Started = started.Add;
            int level = Array.FindIndex(Levels.All, l => l.file == "w2l1");
            Enter(level);
            yield return null;
            Check("level has a storm clock", Clock != null);
            Check("level starts in the calm", Clock.At(0) == Weather.Calm);
            Ticks();

            Enter(level);
            yield return Frames();

            // A shelter in a real storm: the panel says so, the arrow goes, the scarf drops
            var q = Grid.Shelters[0];
            Put((q.x0 + q.x1) / 2, q.y0);
            while (Clock.At(G.world.t) != Weather.Storm || Clock.Left(G.world.t) > Clock.blow - 1) { if (Clock.At(G.world.t) != Weather.Storm) Put((q.x0 + q.x1) / 2, q.y0); yield return null; }
            Check("in a shelter there is no push", G.storm.ArrowPush == 0 && Mathf.Abs(G.storm.HeroTilt) < 1);
            yield return Shot("shelter");

            // The bonus room is indoors
            Put(Grid.MainW + 5, Grid.RoomDoor.y);
            while (Clock.At(G.world.t) != Weather.Storm) yield return null;
            yield return null; yield return null;
            Check("no storm in the bonus room", G.storm.Shown == Weather.Calm && app.hud.StormShown == Weather.Calm && G.storm.TintAlpha < .4f);

            // Pausing silences the wind at once, leaving the level stops it for good
            Put(38, 2);
            while (Clock.At(G.world.t) != Weather.Storm) yield return null;
            yield return new WaitForSecondsRealtime(.2f);
            app.Backgrounded();
            yield return null;
            Check("pause silences the wind", Sound.WindVolume == 0);
            G.Unload();
            Check("leaving the level stops the wind", Sound.WindVolume == 0 && app.hud.StormShown == Weather.Calm);

            Sound.Started = null;
            Note(fails == 0 ? "ALL PASS" : fails + " FAILED");
            Application.Quit(fails == 0 ? 0 : 5);
        }
    }
}
