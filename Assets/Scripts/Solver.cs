using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kfitsa
{
    /// Proves a level can be finished (-solve): searches button presses through the real controller,
    /// ignoring enemies. Reports the furthest column reached when it fails.
    public static class Solver
    {
        const float Dt = 1f / Tune.SimHz;
        const int Macro = 10;

        class Node { public Player p; public float t; public bool jumpHeld; public int depth; }

        static readonly (float x, bool run, bool jump)[] Actions =
        {
            (1, true, true), (1, true, false), (1, false, true), (1, false, false), (0, false, false), (0, false, true), (-1, false, false), (-1, false, true),
        };

        public static void Run(string dir)
        {
            Directory.CreateDirectory(dir);
            var w = new StringWriter();
            foreach (var def in Levels.All)
            {
                foreach (bool big in new[] { false, true })
                {
                    var text = Resources.Load<TextAsset>("Levels/" + def.file).text;
                    w.WriteLine($"{def.file} {(big ? "big  " : "small")}: {Solve(Grid2.Parse(text), big)}");
                    File.WriteAllText(Path.Combine(dir, "solve.txt"), w.ToString());
                }
            }
        }

        static bool Dead(Grid2 g, Player h)
        {
            if (h.y < -2.5f) return true;
            int c0 = Mathf.FloorToInt(h.Left + .15f), c1 = Mathf.FloorToInt(h.Right - .15f), r0 = Mathf.FloorToInt(h.y + .1f), r1 = Mathf.FloorToInt(h.Top - .1f);
            for (int r = r0; r <= r1; r++)
                for (int c = c0; c <= c1; c++)
                    if (g.Deadly(c, r) || g.Hazard(c, r)) return true;
            return false;
        }

        static string Solve(Grid2 g, bool big)
        {
            var start = new Player { grid = g, x = g.Start.x, y = g.Start.y };
            start.SetBig(big);
            var buckets = new List<Node>[g.W + 2];
            for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<Node>();
            buckets[(int)start.x].Add(new Node { p = start });
            var seen = new HashSet<long>();
            int top = (int)start.x, furthest = top, expanded = 0;
            bool moving = g.Platforms.Count > 0;
            while (expanded < 1500000)
            {
                while (top >= 0 && buckets[top].Count == 0) top--;
                if (top < 0) return $"NO ROUTE, furthest column {furthest} of gate {g.GateX:F0} ({expanded} states)";
                var list = buckets[top];
                var n = list[list.Count - 1]; list.RemoveAt(list.Count - 1);
                expanded++;
                foreach (var a in Actions)
                {
                    var h = n.p.Copy(); float t = n.t; bool held = n.jumpHeld, alive = true;
                    for (int s = 0; s < Macro && alive; s++)
                    {
                        foreach (var p in g.Platforms)
                        {
                            p.At(t); p.At(t + Dt);
                            if (h.on == p) { h.MoveX(p.dx); h.y += p.dy; }
                        }
                        t += Dt;
                        var pad = new Pad { x = a.x, run = a.run, jumpHeld = a.jump, jumpPressed = a.jump && !held };
                        held = a.jump;
                        h.Step(Dt, pad);
                        foreach (var sp in g.Springs)
                            if (h.vy <= 0 && Mathf.Abs(h.x - (sp.c + .5f)) < .6f && h.y < sp.r + .6f && h.y >= sp.r - .1f) h.Launch(22f);
                        alive = !Dead(g, h);
                    }
                    if (!alive) continue;
                    if (h.x >= g.GateX - .1f && h.grounded && h.x < g.MainW) return $"ok, route about {t:F1}s";
                    if (h.x >= g.MainW) continue;
                    long key = ((long)Mathf.RoundToInt(h.x * 3) << 40) ^ ((long)Mathf.RoundToInt((h.y + 5) * 3) << 28) ^ ((long)Mathf.RoundToInt(h.vx / 2 + 8) << 22)
                        ^ ((long)Mathf.RoundToInt(h.vy / 3 + 12) << 16) ^ (h.grounded ? 1L << 15 : 0) ^ (held ? 1L << 14 : 0)
                        ^ (moving ? (long)Mathf.RoundToInt(Mathf.Repeat(t, 6.98f) * 3) : 0);
                    if (!seen.Add(key)) continue;
                    int b = Mathf.Clamp((int)h.x, 0, g.W + 1);
                    furthest = Mathf.Max(furthest, b);
                    buckets[b].Add(new Node { p = h, t = t, jumpHeld = held });
                    top = Mathf.Max(top, b);
                }
            }
            return $"GAVE UP, furthest column {furthest} of gate {g.GateX:F0}";
        }
    }
}
