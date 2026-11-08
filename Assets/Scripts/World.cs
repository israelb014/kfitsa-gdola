using UnityEngine;

namespace Kfitsa
{
    public enum Weather { Calm, Warning, Storm }

    /// One clock for a whole level. Everything the player sees and feels asks this same clock, so they cannot disagree.
    public class Storm
    {
        public float calm, warn, blow;
        public float Period => calm + warn + blow;
        public Weather At(float t)
        {
            float k = Mathf.Repeat(t, Period);
            return k < calm ? Weather.Calm : k < calm + warn ? Weather.Warning : Weather.Storm;
        }
        /// Seconds until the current phase ends.
        public float Left(float t)
        {
            float k = Mathf.Repeat(t, Period);
            return (k < calm ? calm : k < calm + warn ? calm + warn : Period) - k;
        }
    }

    /// A safe spot: the storm does not reach a hero whose feet are inside it.
    public class Shelter { public float x0, x1, y0, y1; }

    public class WindZone
    {
        public float x0, x1, y0, y1, push, lift;
        public int sw = -1;     // when 0 or 1: blows only while the switch is in that state
        public Storm storm;     // when set: blows only during the storm
        public bool Blowing(float t, int state)
        {
            if (sw >= 0 && (state & 1) != sw) return false;
            return storm == null || storm.At(t) == Weather.Storm;
        }
        public bool Holds(float x, float y) => x >= x0 && x <= x1 && y >= y0 && y <= y1;
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
        public int cloudA, cloudB;      // the clouds still sunk (id + 1, 0 for none): the one stood on last, and the one before
        public float sinkA, sinkB;
        public bool onSwitch;
        public static WorldState Fresh => new WorldState { floodAt = -1, chaseAt = -1 };
    }

    /// Every rule that moves the hero or can kill it outright. The game and the reachability
    /// search both run this same code, so a route the search finds is a route the game allows.
    public static class World
    {
        public const float BeltSpeed = 3f, SpringSpeed = 22f, CrumbleDelay = .5f, CrumbleBack = 3.5f;
        public const float WindGroundScale = .55f;
        public const float CloudSink = 1.5f, CloudRise = 3f;

        /// Moves every platform to where the clock and the clouds say it is.
        public static void Place(Grid2 g, in WorldState s)
        {
            foreach (var p in g.Platforms) if (p.cloud) PlaceCloud(p, s); else p.At(s.t);
        }

        static void PlaceCloud(Platform p, in WorldState s)
        {
            float ny = p.baseY - SinkOf(p, s);
            p.dx = 0; p.dy = ny - p.y; p.y = ny;
        }

        public static float SinkOf(Platform p, in WorldState s)
        {
            int id = p.cloudId + 1;
            return Mathf.Min(p.maxSink, id == s.cloudA ? s.sinkA : id == s.cloudB ? s.sinkB : 0);
        }

        /// The cloud under the feet sinks; the others float back up. Only two can be down at once, which the
        /// levels respect by spacing clouds so that one is back up before the hero reaches a third.
        static void Clouds(Grid2 g, ref WorldState s, Player h, float dt)
        {
            int on = h.on != null && h.on.cloud ? h.on.cloudId + 1 : 0;
            if (on != 0)
            {
                if (s.cloudA != on)
                {
                    if (s.cloudB == on) { (s.cloudA, s.cloudB) = (s.cloudB, s.cloudA); (s.sinkA, s.sinkB) = (s.sinkB, s.sinkA); }
                    else { s.cloudB = s.cloudA; s.sinkB = s.sinkA; s.cloudA = on; s.sinkA = 0; }
                }
                s.sinkA = Mathf.Min(s.sinkA + CloudSink * dt, h.on.maxSink);
            }
            else if (s.cloudA != 0) s.sinkA = Mathf.Max(0, s.sinkA - CloudRise * dt);
            if (s.cloudB != 0) { s.sinkB = Mathf.Max(0, s.sinkB - CloudRise * dt); if (s.sinkB == 0) s.cloudB = 0; }
            if (on == 0 && s.cloudA != 0 && s.sinkA == 0) { s.cloudA = s.cloudB; s.sinkA = s.sinkB; s.cloudB = 0; s.sinkB = 0; }
        }

        public static void Step(Grid2 g, ref WorldState s, Player h, float dt, Pad input)
        {
            s.t += dt;
            if (g.Clouds.Count > 0) Clouds(g, ref s, h, dt);
            foreach (var p in g.Platforms)
            {
                if (p.cloud) PlaceCloud(p, s); else p.At(s.t);
                if (h.on == p) { h.MoveX(p.dx); h.y += p.dy; }
            }
            if (h.grounded)
            {
                int belt = Belt(g, h);
                if (belt != 0) h.MoveX(belt * BeltSpeed * dt);
            }
            bool safe = Sheltered(g, h.x, h.y);
            foreach (var w in g.Winds)
                if (w.Holds(h.x, h.y) && w.Blowing(s.t, s.sw) && !(safe && w.storm != null))
                {
                    h.MoveX(w.push * (h.grounded ? WindGroundScale : 1) * dt);
                    if (w.lift != 0 && !h.grounded) h.vy = Mathf.Min(h.vy + w.lift * dt, 11f);
                }
            h.Step(dt, input);
            Triggers(g, ref s, h);
        }

        public static bool Sheltered(Grid2 g, float x, float y)
        {
            foreach (var q in g.Shelters)
                if (x >= q.x0 && x <= q.x1 && y >= q.y0 && y <= q.y1) return true;
            return false;
        }

        /// The storm's push at a spot, shelters ignored: which way the sand there flies. 0 outside the storm's zones.
        public static float StormPush(Grid2 g, float x, float y)
        {
            float push = 0;
            foreach (var w in g.Winds) if (w.storm != null && w.Holds(x, y)) push += w.push;
            return push;
        }

        /// The push on the hero right now, for the HUD and the tests; the same rule as Step.
        public static float PushNow(Grid2 g, in WorldState s, Body h)
        {
            if (g.Storm == null || g.Storm.At(s.t) != Weather.Storm || Sheltered(g, h.x, h.y)) return 0;
            return StormPush(g, h.x, h.y);
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

        /// The crumbling ledge under the feet, as a key, or -1. Every three blocks count as one piece, which is
        /// stricter than the game, where each block falls on its own, but still lets a hero run along a bridge.
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
                key = row * 4096 + left + (c - left) / 3 * 3;
            }
            return key;
        }
    }
}
