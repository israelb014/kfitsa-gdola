using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Kfitsa
{
    /// One thing a level promises the player can get to.
    public class Goal
    {
        public string kind, label;
        public int c, r;
        public float x, y;        // overlap goals
        public bool hit;
        public int node = -1, plan, tick;  // how it was reached
        public override string ToString() => $"{kind} {label}";
    }

    public struct Plan
    {
        public sbyte d1, d2; public bool run1, run2, jump, heldAll; public short hold, split, max;
        public Pad At(int tick)
        {
            bool first = tick < split;
            return new Pad
            {
                x = first ? d1 : d2, run = first ? run1 : run2,
                jumpPressed = jump && tick == 0, jumpHeld = heldAll || jump && tick < hold,
            };
        }
    }

    /// Searches real button presses through the real controller and World rules. It moves between
    /// standing spots using human-sized inputs (hold times and direction changes a person can repeat),
    /// and keeps only the earliest arrival at each spot, so a timed hazard is also handled correctly.
    public class Search
    {
        public const float Dt = 1f / Tune.SimHz;
        const int MaxAir = 420;

        /// Crumbling ledges already touched on the way here. They keep falling after the hero leaves.
        public class Trail { public int key; public float at; public Trail older; }

        public class Node
        {
            public Player p; public WorldState s; public int parent, planIndex, surface; public Trail trail;
        }

        public readonly Grid2 g;
        public readonly List<Node> nodes = new List<Node>();
        public readonly List<Plan> plans = new List<Plan>();
        public readonly List<Goal> goals = new List<Goal>();
        public double seconds = 45;
        public bool exhausted;          // every reachable spot was explored
        public int stopAfterHits = int.MaxValue;
        public float hazardScale = 1;
        readonly Dictionary<long, int> seen = new Dictionary<long, int>();
        // One queue per place, always serving the least explored place first: the search spreads through
        // the whole level instead of trying every phase of the first platform. Each queue is first come, first served.
        class Place { public readonly List<int> waiting = new List<int>(); public int served; public float x, y, far; }
        readonly Dictionary<int, Place> places = new Dictionary<int, Place>();
        int queued, hits;
        public readonly int[,] surfaceAt; public int surfaces;
        public readonly List<(int row, int c0, int c1)> surfaceList = new List<(int, int, int)>();
        public readonly HashSet<int> stood = new HashSet<int>();
        public readonly HashSet<long> links = new HashSet<long>();
        readonly Dictionary<int, Goal> boxGoals = new Dictionary<int, Goal>(), cellGoals = new Dictionary<int, Goal>();
        readonly List<Goal> platformGoals = new List<Goal>(); readonly List<Goal>[] overlapByColumn;
        Goal gate, door, checkpoint, roomExit;
        readonly float cycle;
        readonly bool room;
        readonly float maxHold;

        static readonly short[] Holds = { 8, 24, 45, 72 };
        static readonly short[] Splits = { 24, 56 };

        public Search(Grid2 grid, bool inRoom, float holdCap = 9)
        {
            g = grid; room = inRoom; maxHold = holdCap;
            surfaceAt = new int[g.W, g.H];
            overlapByColumn = new List<Goal>[g.W];
            cycle = FindCycle();
            BuildPlans();
            FindSurfaces();
        }

        float FindCycle()
        {
            var periods = new List<float>();
            foreach (var p in g.Platforms) periods.Add(p.period);
            foreach (var w in g.Winds) if (w.off > 0) periods.Add(w.on + w.off);
            if (g.Tide != null) periods.Add(g.Tide.period);
            int l = 1;
            foreach (float f in periods)
            {
                int n = Mathf.RoundToInt(f * 2);
                int a = l, b = n;
                while (b != 0) { int t = a % b; a = b; b = t; }
                l = l / a * n;
            }
            return periods.Count == 0 ? 0 : l / 2f;
        }

        /// Time only matters close to something that moves with the clock.
        bool Near(float x, float y)
        {
            foreach (var p in g.Platforms)
                if (Mathf.Abs(x - p.baseX) < Mathf.Abs(p.moveX) + 10 && Mathf.Abs(y - p.baseY) < Mathf.Abs(p.moveY) + 6) return true;
            foreach (var w in g.Winds)
                if (w.off > 0 && x > w.x0 - 8 && x < w.x1 + 4 && y > w.y0 - 2 && y < w.y1 + 2) return true;
            return g.Tide != null && x > g.Tide.x0 - 6 && x < g.Tide.x1 + 2;
        }

        void BuildPlans()
        {
            // On the ground: short steps
            foreach (sbyte d in new sbyte[] { -1, 1 })
                foreach (bool run in new[] { false, true })
                    foreach (bool held in new[] { false, true })
                        plans.Add(new Plan { d1 = d, d2 = d, run1 = run, run2 = run, heldAll = held, split = 999, max = 14 });
            plans.Add(new Plan { max = 18, split = 999 });       // wait, and let the speed die down
            ground = plans.Count;
            // Jumps: a hold time, a direction, and a change of direction in the air
            foreach (short hold in Holds)
            {
                if (hold * Dt > maxHold) continue;
                foreach (sbyte d1 in new sbyte[] { 1, -1, 0 })
                {
                    plans.Add(new Plan { jump = true, hold = hold, d1 = d1, d2 = d1, run1 = true, run2 = true, split = 999, max = MaxAir });
                    foreach (short split in Splits)
                        foreach (sbyte d2 in new sbyte[] { 1, -1, 0 })
                            if (d2 != d1) plans.Add(new Plan { jump = true, hold = hold, d1 = d1, d2 = d2, run1 = true, run2 = true, split = split, max = MaxAir });
                }
            }
            // Holding jump through a spring bounce
            foreach (sbyte d1 in new sbyte[] { 1, -1, 0 })
                foreach (sbyte d2 in new sbyte[] { 1, -1, 0 })
                    foreach (short split in Splits)
                        plans.Add(new Plan { jump = true, hold = 999, d1 = d1, d2 = d2, run1 = true, run2 = true, split = split, max = MaxAir });
        }
        int ground;

        void FindSurfaces()
        {
            for (int r = 0; r < g.H; r++)
                for (int c = 0; c < g.W; c++)
                    surfaceAt[c, r] = -1;
            for (int r = 0; r < g.H - 1; r++)
            {
                int c0 = room ? g.MainW : 0, c1 = room ? g.W : g.MainW;
                for (int c = c0; c < c1; c++)
                {
                    if (!Standable(c, r) || room && r >= g.RoomH - 2) continue;
                    int id = c > c0 && surfaceAt[c - 1, r] >= 0 ? surfaceAt[c - 1, r] : surfaces++;
                    surfaceAt[c, r] = id;
                    if (id == surfaceList.Count) surfaceList.Add((r, c, c));
                    else { var s = surfaceList[id]; s.c1 = c; surfaceList[id] = s; }
                }
            }
        }

        bool Standable(int c, int r)
        {
            bool sw = g.At(c, r) == 'a' || g.At(c, r) == 'A';
            if (!(g.Solid(c, r) || sw)) return false;
            if (g.Deadly(c, r + 1) || g.Hazard(c, r + 1)) return false;
            return !(g.Solid(c, r + 1) || g.At(c, r + 1) == 'a' || g.At(c, r + 1) == 'A');
        }

        public Goal Add(string kind, string label, int c = 0, int r = 0, float x = 0, float y = 0)
        {
            var q = new Goal { kind = kind, label = label, c = c, r = r, x = x, y = y };
            goals.Add(q);
            switch (kind)
            {
                case "box": case "secret": boxGoals[r * 4096 + c] = q; break;
                case "star": case "coin": case "power-up": case "lamp": case "switch": case "spring":
                    int at = Mathf.Clamp((int)x, 0, g.W - 1);
                    (overlapByColumn[at] ??= new List<Goal>()).Add(q); break;
                case "ledge": cellGoals[c] = q; break;      // c is the surface id
                case "platform": platformGoals.Add(q); break;
                case "gate": gate = q; break;
                case "door": door = q; break;
                case "room exit": roomExit = q; break;
                case "flag": checkpoint = q; break;
            }
            return q;
        }

        void Hit(Goal q, int parent, int plan, int tick)
        {
            if (q == null || q.hit) return;
            q.hit = true; q.node = parent; q.plan = plan; q.tick = tick; if (q.kind != "coin" || room) hits++;
        }

        long Key(Player p, in WorldState s)
        {
            int on = p.on != null ? g.Platforms.IndexOf(p.on) + 1 : 0;
            float rx = on > 0 ? p.x - p.on.x : p.x;
            long k = Mathf.RoundToInt(rx * 3) + 4096L;
            k = k * 1024 + Mathf.RoundToInt(on > 0 ? 0 : p.y * 2) + 8;
            k = k * 32 + Mathf.RoundToInt(p.vx / 3f) + 8;
            k = k * 64 + on;
            k = k * 2 + (s.sw & 1);
            k = k * 2 + (s.floodAt >= 0 || s.chaseAt >= 0 ? 1 : 0);
            int phase = cycle > 0 && Near(p.x, p.y) ? Mathf.FloorToInt(Mathf.Repeat(s.t, cycle) * 2) : 0;
            return k * 256 + phase;
        }

        public void Begin(Player start, WorldState s)
        {
            start.grid = g;
            g.Sw = s.sw;
            foreach (var p in g.Platforms) { p.At(s.t); p.At(s.t); }
            Push(new Node { p = start, s = s, parent = -1, planIndex = -1, surface = -1 });
        }

        void Push(Node n)
        {
            if (!seen.TryAdd(Key(n.p, n.s), nodes.Count)) return;
            nodes.Add(n);
            int where = n.p.on != null ? -2 - g.Platforms.IndexOf(n.p.on) : SurfaceUnder(n.p);
            if (!places.TryGetValue(where, out var q)) { places[where] = q = new Place { x = n.p.x, y = n.p.y }; q.far = Distance(q.x, q.y); }
            q.waiting.Add(nodes.Count - 1); queued++;
        }

        public void Run()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (queued > 0 && hits < stopAfterHits && clock.Elapsed.TotalSeconds < seconds)
            {
                if (++turn % 64 == 0) foreach (var q in places.Values) q.far = Distance(q.x, q.y);
                Place next = null;
                foreach (var q in places.Values)
                    if (q.waiting.Count > 0 && (next == null || q.served + q.far * 3 < next.served + next.far * 3)) next = q;
                next.served++; queued--;
                Expand(Take(next));
            }
            exhausted = queued == 0;
        }

        int turn;

        /// Of the oldest few spots waiting in a place, the one closest to something unreached.
        int Take(Place q)
        {
            int best = 0, n = Mathf.Min(q.waiting.Count, 24); float near = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var p = nodes[q.waiting[i]].p;
                float d = Distance(p.x, p.y);
                if (d < near) { near = d; best = i; }
            }
            int id = q.waiting[best];
            q.waiting.RemoveAt(best);
            return id;
        }

        /// Tiles from a place to the nearest thing still unreached, so effort goes where it can still pay off.
        float Distance(float x, float y)
        {
            float best = 400;
            foreach (var q in goals)
            {
                if (q.hit || q.kind == "coin" && !room) continue;
                float gx = q.x, gy = q.y;
                if (q.kind == "ledge") { var l = surfaceList[q.c]; gx = (l.c0 + l.c1) / 2f; gy = l.row; }
                else if (q.kind == "box" || q.kind == "secret") { gx = q.c; gy = q.r; }
                else if (q.kind == "platform") { gx = g.Platforms[q.c].baseX; gy = g.Platforms[q.c].baseY; }
                else if (q.kind == "gate") { gx = g.GateX; gy = g.GateY; }
                else if (q.kind == "door") { gx = g.Door.x; gy = g.Door.y; }
                else if (q.kind == "flag") { gx = g.Checkpoint.x; gy = g.Checkpoint.y; }
                else if (q.kind == "room exit") { gx = g.RoomDoor.x; gy = g.RoomDoor.y; }
                best = Mathf.Min(best, Mathf.Abs(gx - x) + Mathf.Abs(gy - y));
            }
            return best;
        }

        void Expand(int id)
        {
            var n = nodes[id];
            bool springNear = false;
            foreach (var (c, r) in g.Springs) springNear |= Mathf.Abs(c + .5f - n.p.x) < 7 && Mathf.Abs(r - n.p.y) < 3;
            foreach (var w in g.Winds) springNear |= w.lift != 0 && n.p.x > w.x0 - 6 && n.p.x < w.x1 + 6;
            for (int i = 0; i < plans.Count; i++)
            {
                var pl = plans[i];
                if ((pl.heldAll || pl.hold == 999) && !springNear) continue;
                Simulate(id, n, i, pl);
                if (hits >= stopAfterHits) return;
            }
        }

        void Simulate(int id, Node n, int planIndex, in Plan pl)
        {
            var h = n.p.Copy(); var s = n.s; g.Sw = s.sw;
            foreach (var p in g.Platforms) { p.At(s.t); }
            var trail = n.trail;
            int tick = 0, pi = planIndex;
            var plan = pl;
            int bumpTick = 0;
            h.onBump = (c, r) => { if (boxGoals.TryGetValue(r * 4096 + c, out var q)) Hit(q, id, pi, bumpTick); };
            h.onJump = null; h.onLand = null;
            while (true)
            {
                bumpTick = tick;
                var pad = plan.At(tick);
                World.Step(g, ref s, h, Dt, pad);
                if (World.SpringAt(g, h) >= 0) h.Launch(World.SpringSpeed);
                tick++;
                if (Dead(h, s)) return;
                Touch(h, s, id, pi, tick);
                if (h.grounded)
                {
                    int k = World.CrumbleUnder(g, h);
                    if (k >= 0)
                    {
                        var seen = trail;
                        while (seen != null && seen.key != k) seen = seen.older;
                        float since = seen == null ? 99 : s.t - seen.at;
                        if (since > World.CrumbleDelay - .06f && since < World.CrumbleDelay + World.CrumbleBack + 1) return;
                        if (since >= 99 || since >= World.CrumbleDelay + World.CrumbleBack + 1) trail = new Trail { key = k, at = s.t, older = trail };
                    }
                }
                if (tick >= plan.max && h.grounded) break;
                if (plan.jump && h.grounded && tick > 3) break;
                if (tick >= plan.max && !h.grounded && !plan.jump)
                {
                    // Walked off an edge: the input carries on until the landing
                    plan.max = MaxAir; plan.jump = false;
                }
                if (tick > MaxAir) return;
            }
            int surf = SurfaceUnder(h);
            if (surf >= 0 && n.surface >= 0 && surf != n.surface) links.Add((long)n.surface << 32 | (uint)surf);
            Push(new Node { p = h, s = s, parent = id, planIndex = planIndex, trail = trail, surface = surf >= 0 ? surf : n.surface });
        }

        int SurfaceUnder(Player h)
        {
            if (h.on != null) return -1;
            int row = Mathf.FloorToInt(h.y - .05f), c = Mathf.Clamp(Mathf.FloorToInt(h.x), 0, g.W - 1);
            if (row < 0 || row >= g.H) return -1;
            int id = surfaceAt[c, row];
            if (id >= 0) return id;
            c = Mathf.Clamp(Mathf.FloorToInt(h.Left + .05f), 0, g.W - 1); id = surfaceAt[c, row];
            if (id >= 0) return id;
            c = Mathf.Clamp(Mathf.FloorToInt(h.Right - .05f), 0, g.W - 1);
            return surfaceAt[c, row];
        }

        bool Dead(Player h, in WorldState s)
        {
            if (h.y < -2.5f) return true;
            if (!room && h.x >= g.MainW) return true;
            var (deadly, hazard) = World.Touching(g, h);
            if (deadly || hazard) return true;
            if (hazardScale != 1)
            {
                var fast = s;      // the same hazard with less time left, for the safety margin
                if (fast.floodAt >= 0) fast.floodAt = fast.t - (fast.t - fast.floodAt) * hazardScale;
                if (fast.chaseAt >= 0) fast.chaseAt = fast.t - (fast.t - fast.chaseAt) * hazardScale;
                return World.Drowned(g, fast, h) || World.Caught(g, fast, h);
            }
            return World.Drowned(g, s, h) || World.Caught(g, s, h);
        }

        void Touch(Player h, in WorldState s, int id, int plan, int tick)
        {
            int col = Mathf.Clamp(Mathf.FloorToInt(h.x), 1, g.W - 2);
            for (int c = col - 1; c <= col + 1; c++)
                if (overlapByColumn[c] != null)
                    foreach (var q in overlapByColumn[c])
                        if (!q.hit && Mathf.Abs(q.x - h.x) < h.w / 2 + .35f && q.y > h.y - .35f && q.y < h.Top + .35f) Hit(q, id, plan, tick);
            if (!h.grounded)
                return;
            if (gate != null && g.AtGate(h)) Hit(gate, id, plan, tick);
            if (door != null && Mathf.Abs(h.x - g.Door.x) < .6f && Mathf.Abs(h.y - g.Door.y) < .6f) Hit(door, id, plan, tick);
            if (roomExit != null && Mathf.Abs(h.x - g.RoomDoor.x) < .6f && Mathf.Abs(h.y - g.RoomDoor.y) < .6f) Hit(roomExit, id, plan, tick);
            if (checkpoint != null && h.x >= g.Checkpoint.x && h.y >= g.Checkpoint.y - 1) Hit(checkpoint, id, plan, tick);
            if (h.on != null) { int k = g.Platforms.IndexOf(h.on); foreach (var q in platformGoals) if (q.c == k) Hit(q, id, plan, tick); return; }
            int surf = SurfaceUnder(h);
            if (surf >= 0 && stood.Add(surf) && cellGoals.TryGetValue(surf, out var ledge)) Hit(ledge, id, plan, tick);
        }

        /// The button presses that reach a goal, tick by tick.
        public List<Pad> Route(Goal q)
        {
            var chain = new List<int>();
            for (int i = q.node; i >= 0; i = nodes[i].parent) chain.Add(i);
            chain.Reverse();
            var pads = new List<Pad>();
            for (int i = 1; i < chain.Count; i++) Replay(plans[nodes[chain[i]].planIndex], nodes[chain[i]], pads, int.MaxValue);
            Replay(plans[q.plan], null, pads, q.tick + 1);
            return pads;
        }

        /// Where the search says the hero stands after each step of a route, for comparing with the game.
        public List<(int tick, float x, float y)> Marks(Goal q)
        {
            var chain = new List<int>();
            for (int i = q.node; i >= 0; i = nodes[i].parent) chain.Add(i);
            chain.Reverse();
            var marks = new List<(int, float, float)>();
            int tick = 0;
            for (int i = 1; i < chain.Count; i++)
            {
                var n = nodes[chain[i]];
                tick += (int)Mathf.Round((n.s.t - nodes[n.parent].s.t) / Dt);
                marks.Add((tick, n.p.x, n.p.y));
            }
            return marks;
        }

        void Replay(Plan pl, Node end, List<Pad> pads, int upTo)
        {
            // The node remembers where the plan ended, not how long it took, so rerun it to find that out
            int ticks = upTo;
            if (end != null) ticks = (int)Mathf.Round((end.s.t - nodes[end.parent].s.t) / Dt);
            for (int t = 0; t < ticks; t++) pads.Add(pl.At(t));
        }
    }

    public static class Reach
    {
        public class Job { public LevelDef def; public string name; public bool big, room, flag, strict; public Search search; public double seconds; public List<Goal> coinsLeft; }

        static Goal[] AddGoals(Search s, bool room)
        {
            var g = s.g; bool In(float x) => room ? x >= g.MainW : x < g.MainW;
            for (int i = 0; i < s.surfaces; i++) { var (row, c0, c1) = s.surfaceList[i]; s.Add("ledge", $"row {row} columns {c0}-{c1}", i); }
            foreach (var (c, r, kind) in g.Blocks)
            {
                if (!In(c)) continue;
                bool hidden = kind == BlockKind.HiddenCoin || kind == BlockKind.HiddenLife;
                if (kind == BlockKind.Brick) continue;
                s.Add(hidden ? "secret" : "box", $"{kind} at {c},{r}", c, r);
                if (kind == BlockKind.Grow) s.Add("power-up", $"above the box at {c},{r}", c, r, c + .5f, r + 1.4f);
            }
            int star = 0; foreach (var q in g.Stars) { star++; if (In(q.x)) s.Add("star", $"{star} at {q.x:F0},{q.y:F0}", x: q.x, y: q.y); }
            foreach (var q in g.Coins) if (In(q.x)) s.Add("coin", $"at {q.x:F0},{q.y:F0}", x: q.x, y: q.y);
            foreach (var q in g.RoomLights) if (In(q.x)) s.Add("lamp", $"at {q.x:F0},{q.y:F0}", x: q.x, y: q.y);
            foreach (var (c, r) in g.Switches) if (In(c)) s.Add("switch", $"at {c},{r}", x: c + .5f, y: r + .4f);
            foreach (var (c, r) in g.Springs) if (In(c)) s.Add("spring", $"at {c},{r}", x: c + .5f, y: r + .3f);
            for (int i = 0; i < g.Platforms.Count; i++) if (In(g.Platforms[i].baseX)) s.Add("platform", $"{i + 1} at {g.Platforms[i].baseX:F0}", i);
            if (room) s.Add("room exit", "the door back");
            else
            {
                s.Add("gate", "the finish");
                if (g.HasDoor) s.Add("door", "to the bonus room");
                if (g.HasCheckpoint) s.Add("flag", "checkpoint");
            }
            return s.goals.ToArray();
        }

        static Player Hero(Grid2 g, bool big, Vector2 at)
        {
            var p = new Player { grid = g, x = at.x, y = at.y };
            p.SetBig(big);
            return p;
        }

        /// A running search for one level, form and start. The grid is private to it, so jobs can run in parallel.
        public static Search Start(LevelDef def, bool big, bool room, bool flag, float holdCap = 9, float hazard = 1)
        {
            var g = Grid2.Parse(Levels.Text(def.file));
            var s = new Search(g, room, holdCap) { hazardScale = hazard };
            AddGoals(s, room);
            var at = room ? g.RoomDoor : flag ? g.Checkpoint : g.Start;
            s.Begin(Hero(g, big, at), WorldState.Fresh);
            return s;
        }

        /// A search from wherever the hero is in a running game.
        public static Search From(LevelDef def, Player hero, WorldState state, bool room)
        {
            var g = Grid2.Parse(Levels.Text(def.file));
            var s = new Search(g, room);
            AddGoals(s, room);
            var p = hero.Copy();
            p.grid = g; p.on = hero.on == null ? null : g.Platforms[hero.grid.Platforms.IndexOf(hero.on)];
            s.Begin(p, state);
            return s;
        }

        /// Measured from the real controller, for the report.
        public static string Physics()
        {
            var w = new StringBuilder();
            foreach (bool big in new[] { false, true })
                foreach (var (name, run, up) in new[] { ("standing", false, false), ("walking", false, true), ("running", true, true) })
                {
                    var p = new Player { grid = Grid2.Flat(3000, 2), x = 20, y = 2 };
                    p.SetBig(big);
                    p.Step(Search.Dt, default);
                    if (up) for (int i = 0; i < 300; i++) p.Step(Search.Dt, new Pad { x = 1, run = run });
                    float x0 = p.x, y0 = p.y, top = y0, t = 0;
                    p.Step(Search.Dt, new Pad { x = up ? 1 : 0, run = run, jumpPressed = true, jumpHeld = true });
                    while (!p.grounded && t < 5) { t += Search.Dt; p.Step(Search.Dt, new Pad { x = up ? 1 : 0, run = run, jumpHeld = true }); top = Mathf.Max(top, p.y); }
                    w.AppendLine((big ? "big   " : "small ") + name + " jump: height " + (top - y0).ToString("F2") + " tiles, distance " + (p.x - x0).ToString("F2") + " tiles, air " + t.ToString("F2") + "s");
                }
            return w.ToString();
        }

        public static double Seconds = 90;
        const float StrictHold = .38f;     // the finish must be reachable without holding the jump to its very top

        public static bool ValidateAll(string reportPath, string only = null)
        {
            var jobs = new List<Job>();
            foreach (var def in Levels.All)
            {
                if (only != null && def.file != only) continue;
                var grid = Grid2.Parse(Levels.Text(def.file));
                foreach (bool big in new[] { false, true })
                {
                    string form = big ? "big" : "small";
                    jobs.Add(new Job { def = def, big = big, name = $"{def.file} {form} from the start" });
                    if (grid.HasCheckpoint) jobs.Add(new Job { def = def, big = big, flag = true, name = $"{def.file} {form} from the flag" });
                    if (grid.HasRoomDoor) jobs.Add(new Job { def = def, big = big, room = true, name = $"{def.file} {form} in the bonus room" });
                    jobs.Add(new Job { def = def, big = big, strict = true, name = $"{def.file} {form} finish with a margin" });
                }
            }
            Parallel.ForEach(jobs.OrderByDescending(j => j.def.file).ToList(), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, j =>
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                j.search = Start(j.def, j.big, j.room, j.flag, j.strict ? StrictHold : 9, j.strict ? 1.15f : 1);
                j.search.seconds = Seconds;
                if (j.strict) { j.search.stopAfterHits = 1; foreach (var q in j.search.goals) if (q.kind != "gate") q.hit = true; }
                else if (j.flag) { j.search.stopAfterHits = 1; foreach (var q in j.search.goals) if (q.kind != "gate") q.hit = true; }
                else
                {
                    j.search.stopAfterHits = j.search.goals.Count(q => q.kind != "coin" || j.room);
                    if (!j.room) j.coinsLeft = j.search.goals.Where(q => q.kind == "coin").ToList();
                }
                j.search.Run();
                j.seconds = clock.Elapsed.TotalSeconds;
                Console.WriteLine($"{j.name}: {j.search.goals.Count(q => !q.hit)} missing, {j.search.nodes.Count} spots, {j.seconds:F1}s");
            });

            var w = new StringBuilder();
            w.AppendLine("Reachability report, measured from the real controller").AppendLine().Append(Physics()).AppendLine();
            int fails = 0;
            foreach (var j in jobs)
            {
                var s = j.search;
                var missing = s.goals.Where(q => !q.hit && (q.kind != "coin" || j.room)).ToList();
                fails += missing.Count;
                w.AppendLine($"{(missing.Count == 0 ? "ok  " : "FAIL")} {j.name}: {s.goals.Count} goals, {s.nodes.Count} spots, {j.seconds:F1}s{(s.exhausted ? ", searched everything" : "")}");
                foreach (var q in missing.Take(40)) w.AppendLine($"       cannot reach {q}");
            }
            foreach (var line in Lint(only)) { fails++; w.AppendLine("FAIL " + line); }
            w.AppendLine(fails == 0 ? "ALL REACHABLE" : fails + " PROBLEMS");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
            File.WriteAllText(reportPath, w.ToString());
            return fails == 0;
        }

        /// The design promises that are not about physics.
        static IEnumerable<string> Lint(string only)
        {
            var ideas = new Dictionary<string, string>(); var rooms = new Dictionary<string, string>();
            foreach (var def in Levels.All)
            {
                if (only != null && def.file != only) continue;
                var g = Grid2.Parse(Levels.Text(def.file));
                if (g.Stars.Count != 3) yield return $"{def.file}: has {g.Stars.Count} stars, needs 3";
                if (!g.HasCheckpoint) yield return $"{def.file}: no checkpoint";
                else
                {
                    float at = g.MainW < 60 ? g.Checkpoint.y / g.GateY : (g.Checkpoint.x - g.Start.x) / (g.GateX - g.Start.x);
                    if (at < .3f || at > .7f) yield return $"{def.file}: checkpoint is not near the middle ({at:P0})";
                }
                if (g.Idea == "") yield return $"{def.file}: no main idea";
                else if (ideas.TryGetValue(g.Idea, out var o)) yield return $"{def.file}: same main idea as {o}";
                else ideas[g.Idea] = def.file;
                string roomKey = g.RoomMode + g.RoomSeconds;
                if (!g.HasDoor || !g.HasRoomDoor) yield return $"{def.file}: no bonus room";
                else if (rooms.TryGetValue(g.RoomMode, out var r)) yield return $"{def.file}: same room type as {r}";
                else rooms[g.RoomMode] = def.file;
                if (g.Stars.Count(q => q.x >= g.MainW) != 1) yield return $"{def.file}: the room must hold exactly one star";
                int power = g.Blocks.Count(b => b.kind == BlockKind.Grow);
                if (def != Levels.All[0] && power < 3) yield return $"{def.file}: {power} power boxes, needs 3 (two early, one after the flag)";
                if (def == Levels.All[0] && power < 1) yield return $"{def.file}: no power box";
            }
        }
    }
}
