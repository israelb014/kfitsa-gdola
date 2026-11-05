using UnityEngine;

namespace Kfitsa
{
    public class WindZone
    {
        public float x0, x1, y0, y1, push, lift, on, off;
        public int sw = -1;     // when 0 or 1: blows only while the switch is in that state
        public bool Blowing(float t, int state)
        {
            if (sw >= 0 && (state & 1) != sw) return false;
            return off <= 0 || Mathf.Repeat(t, on + off) < on;
        }
        /// 0 to 1: how far through the calm the wind is, for the warning.
        public float Warning(float t) => off <= 0 ? 0 : Mathf.Clamp01((Mathf.Repeat(t, on + off) - on - off + 1f));
    }

    /// Water that comes and goes on a fixed clock, inside a span of columns.
    public class Tide
    {
        public float low, high, period, x0, x1;
        public float At(float t)
        {
            float k = Mathf.Repeat(t, period) / period;     // low for half the cycle, then in, stay, out
            float up = k < .5f ? 0 : k < .62f ? (k - .5f) / .12f : k < .88f ? 1 : (1 - k) / .12f;
            return Mathf.Lerp(low, high, up);
        }
    }

    /// Once the hero passes the trigger, the water climbs for good.
    public class Flood { public float trigger, y0, speed, top; }

    /// Something behind the hero that moves at a fixed speed after the trigger, up to the end column.
    public class Chase { public float trigger, x0, speed, end; }

    /// A pool in the bonus room: the switch drains it.
    public class RoomWater { public float x0, x1, high, low; }

    /// The moving part of a level. A value, so the reachability search can copy it for free.
    public struct WorldState
    {
        public float t, floodAt, chaseAt;
        public int sw;
        public bool onSwitch;
        public static WorldState Fresh => new WorldState { floodAt = -1, chaseAt = -1 };
    }

    /// Every rule that moves the hero or can kill it outright. The game and the reachability
    /// search both run this same code, so a route the search finds is a route the game allows.
    public static class World
    {
        public const float BeltSpeed = 3f, SpringSpeed = 22f, CrumbleDelay = .5f, CrumbleBack = 3.5f;
        public const float WindGroundScale = .55f;

        public static void Step(Grid2 g, ref WorldState s, Player h, float dt, Pad input)
        {
            s.t += dt;
            foreach (var p in g.Platforms)
            {
                p.At(s.t);
                if (h.on == p) { h.MoveX(p.dx); h.y += p.dy; }
            }
            if (h.grounded)
            {
                int belt = Belt(g, h);
                if (belt != 0) h.MoveX(belt * BeltSpeed * dt);
            }
            foreach (var w in g.Winds)
                if (h.x >= w.x0 && h.x <= w.x1 && h.y >= w.y0 && h.y <= w.y1 && w.Blowing(s.t, s.sw))
                {
                    h.MoveX(w.push * (h.grounded ? WindGroundScale : 1) * dt);
                    if (w.lift != 0 && !h.grounded) h.vy = Mathf.Min(h.vy + w.lift * dt, 11f);
                }
            h.Step(dt, input);
            Triggers(g, ref s, h);
        }

        static void Triggers(Grid2 g, ref WorldState s, Player h)
        {
            if (g.Flood != null && s.floodAt < 0 && h.x >= g.Flood.trigger && h.x < g.MainW) s.floodAt = s.t;
            if (g.Chase != null && s.chaseAt < 0 && h.x >= g.Chase.trigger && h.x < g.MainW) s.chaseAt = s.t;
            bool on = false;
            foreach (var (c, r) in g.Switches)
                on |= h.Left < c + .9f && h.Right > c + .1f && h.y < r + .6f && h.Top > r;
            if (on && !s.onSwitch) { s.sw ^= 1; g.Sw = s.sw; }
            s.onSwitch = on;
        }

        /// The spring under the hero this tick, or -1. Launching is up to the caller, as the game also animates it.
        public static int SpringAt(Grid2 g, Player h)
        {
            if (h.vy > 0) return -1;
            for (int i = 0; i < g.Springs.Count; i++)
            {
                var (c, r) = g.Springs[i];
                if (Mathf.Abs(h.x - (c + .5f)) < .6f && h.y < r + .6f && h.y >= r - .1f) return i;
            }
            return -1;
        }

        /// Conveyor direction under the feet: -1, 0 or 1.
        public static int Belt(Grid2 g, Player h)
        {
            int row = Mathf.FloorToInt(h.y - .05f), c0 = Mathf.FloorToInt(h.Left + .002f), c1 = Mathf.FloorToInt(h.Right - .002f);
            for (int c = c0; c <= c1; c++)
            {
                char ch = g.At(c, row);
                if (ch == '{') return -1;
                if (ch == '}') return 1;
            }
            return 0;
        }

        /// Height of the moving water at this column, or -99 if there is none.
        public static float WaterAt(Grid2 g, in WorldState s, float x)
        {
            float y = -99;
            if (x < g.MainW)
            {
                if (g.Tide != null && x >= g.Tide.x0 && x <= g.Tide.x1) y = g.Tide.At(s.t);
                if (g.Flood != null && s.floodAt >= 0) y = Mathf.Max(y, Mathf.Min(g.Flood.top, g.Flood.y0 + g.Flood.speed * (s.t - s.floodAt)));
            }
            else if (g.RoomPool != null && x >= g.RoomPool.x0 && x <= g.RoomPool.x1) y = (s.sw & 1) != 0 ? g.RoomPool.low : g.RoomPool.high;
            return y;
        }

        /// The chaser's front edge, or -99 before it starts.
        public static float ChaseAt(Grid2 g, in WorldState s)
        {
            if (g.Chase == null || s.chaseAt < 0) return -99;
            return Mathf.Min(g.Chase.end, g.Chase.x0 + g.Chase.speed * (s.t - s.chaseAt));
        }

        public static bool Drowned(Grid2 g, in WorldState s, Player h) => h.y + .35f < WaterAt(g, s, h.x);

        public static bool Caught(Grid2 g, in WorldState s, Player h) => g.Chase != null && s.chaseAt >= 0 && h.x < g.Chase.end && h.Right < ChaseAt(g, s) + 1;

        /// Cells that would hurt, as the game checks them.
        public static (bool deadly, bool hazard) Touching(Grid2 g, Player h)
        {
            int c0 = Mathf.FloorToInt(h.Left + .15f), c1 = Mathf.FloorToInt(h.Right - .15f), r0 = Mathf.FloorToInt(h.y + .1f), r1 = Mathf.FloorToInt(h.Top - .1f);
            bool d = false, z = false;
            for (int r = r0; r <= r1; r++)
                for (int c = c0; c <= c1; c++)
                {
                    d |= g.Deadly(c, r);
                    z |= g.Hazard(c, r);
                }
            return (d, z);
        }

        /// The crumbling ledge under the feet, as a key, or -1. A ledge counts as one piece, which is
        /// stricter than the game, where each block falls on its own.
        public static int CrumbleUnder(Grid2 g, Player h)
        {
            if (!h.grounded || h.on != null) return -1;
            int row = Mathf.FloorToInt(h.y - .05f), c0 = Mathf.FloorToInt(h.Left + .002f), c1 = Mathf.FloorToInt(h.Right - .002f);
            int key = -1;
            for (int c = c0; c <= c1; c++)
            {
                if (g.At(c, row) != 'u') { if (g.Solid(c, row)) return -1; continue; }
                int left = c;
                while (g.At(left - 1, row) == 'u') left--;
                key = row * 4096 + left;
            }
            return key;
        }
    }
}
