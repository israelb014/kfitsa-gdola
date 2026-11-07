using System.Collections.Generic;
using UnityEngine;

namespace Kfitsa
{
    /// Everything a sandstorm looks and sounds like. It only reads the storm clock and the wind rule,
    /// so what the player sees is the same thing that pushes the hero.
    public class StormView
    {
        const int Grains = 190, LightGrains = 34, Banks = 3;
        static readonly Color SkyWarning = new Color(1, .68f, .32f), SkyStorm = new Color(.86f, .5f, .22f), Tint = new Color(.72f, .4f, .13f), Sand = new Color(.98f, .84f, .56f);

        readonly Grid2 g; readonly List<SpriteRenderer> sky; readonly Hud hud;
        readonly SpriteRenderer tint;
        readonly SpriteRenderer[] grain = new SpriteRenderer[Grains], bank = new SpriteRenderer[Banks * 2];
        readonly float[] gx = new float[Grains], gy = new float[Grains], speed = new float[Grains], bankX = new float[Banks];
        readonly List<(Transform trunk, Transform crown)> palms = new List<(Transform, Transform)>();
        readonly List<(Transform cloth, SpriteRenderer view)> flags = new List<(Transform, SpriteRenderer)>();
        readonly List<(Shelter at, SpriteRenderer glow, SpriteRenderer sign)> shelters = new List<(Shelter, SpriteRenderer, SpriteRenderer)>();
        readonly SpriteRenderer[] scarf = new SpriteRenderer[7];
        float force, skyK, heroK, clock;

        // What is on screen right now, for the tests
        public Weather Shown { get; private set; }
        public float TintAlpha => tint.color.a;
        public int GrainsOn { get; private set; }
        public float PalmLean { get; private set; }
        public float ArrowPush { get; private set; }
        public float HeroTilt { get; private set; }
        /// How hard the storm blows right now, 0 to 1.
        public float Force => force;
        public int HeroOrder { get; private set; } = 10;

        public StormView(Transform root, Grid2 grid, List<SpriteRenderer> skyParts, Hud h)
        {
            g = grid; sky = skyParts; hud = h;
            tint = Art.Spawn("Storm tint", root, Art.Center("px"), 38);
            tint.color = Color.clear;
            for (int i = 0; i < bank.Length; i++) { bank[i] = Art.Spawn("Dust", root, Art.Tile("dust"), 39); bank[i].enabled = false; }
            for (int i = 0; i < Grains; i++)
            {
                bool longOne = i % 3 != 0;
                grain[i] = Art.Spawn("Sand", root, Art.Center(longOne ? "sand_b" : "sand_a"), 41);
                grain[i].color = Sand * (.8f + Random.value * .2f);
                float size = .7f + Random.value * 1.1f;
                grain[i].transform.localScale = new Vector3(size, size, 1);
                grain[i].enabled = false;
                gx[i] = Random.value; gy[i] = Random.value; speed[i] = .7f + Random.value * .6f;
            }
            foreach (var (c, r) in g.Palms)
            {
                var trunk = Art.Spawn("Palm", root, Art.Feet("palm_trunk"), -6).transform;
                trunk.position = new Vector3(c + .5f, r, 0); trunk.localScale = new Vector3(1.25f, 1.1f, 1);
                var crown = Art.Spawn("Palm top", trunk, Art.Center("palm_crown"), -6).transform;
                crown.localPosition = new Vector3(.08f, 2.9f, 0); crown.localScale = new Vector3(.8f, .91f, 1);
                palms.Add((trunk, crown));
            }
            foreach (var (c, r) in g.Flags)
            {
                var pole = Art.Spawn("Pole", root, Art.Feet("storm_pole"), -4).transform;
                pole.position = new Vector3(c + .5f, r, 0);
                var cloth = Art.Spawn("Cloth", pole, Art.LeftMiddle("storm_cloth"), -4);
                cloth.transform.localPosition = new Vector3(0, 1.75f, 0);
                flags.Add((cloth.transform, cloth));
            }
            foreach (var q in g.Shelters)
            {
                var glow = Art.Spawn("Shelter", root, Art.Center("px"), 5);
                glow.transform.position = new Vector3((q.x0 + q.x1) / 2, (q.y0 + q.y1 + 1) / 2, 0);
                glow.transform.localScale = new Vector3((q.x1 - q.x0) * Art.PPU / 4, (q.y1 - q.y0 + 1) * Art.PPU / 4, 1);
                var sign = Art.Spawn("Shelter sign", root, Art.Center("shelter"), 42);
                sign.transform.position = new Vector3((q.x0 + q.x1) / 2, q.y1 + 1.2f, 0);
                shelters.Add((q, glow, sign));
            }
            for (int i = 0; i < scarf.Length; i++) scarf[i] = Art.Spawn("Scarf", root, Art.Center("scarf"), 9);
        }

        /// Returns true on the frame the storm hits.
        public bool Draw(in WorldState s, Player hero, float camX, float camY, float halfW, float dt, bool playing)
        {
            clock += dt;
            bool here = hero.x < g.MainW;                        // the bonus room is indoors
            var now = here ? g.Storm.At(s.t) : Weather.Calm;
            bool hit = now == Weather.Storm && Shown != Weather.Storm;
            if (hit) Haptics.Buzz(320, 1);
            Shown = now;

            float target = now == Weather.Storm ? 1 : now == Weather.Warning ? .3f : 0;
            force = Mathf.MoveTowards(force, target, dt * 7);
            skyK = Mathf.MoveTowards(skyK, now == Weather.Calm ? 0 : now == Weather.Warning ? .8f : 1, dt * 5);
            float windSide = Mathf.Sign(World.StormPush(g, Mathf.Min(hero.x, g.MainW - 2), 5));

            // Sky and tint
            var skyColor = skyK < .8f ? Color.Lerp(Color.white, SkyWarning, skyK / .8f) : Color.Lerp(SkyWarning, SkyStorm, (skyK - .8f) / .2f);
            foreach (var part in sky) part.color = skyColor;
            float storming = Mathf.Clamp01((force - .3f) / .7f);
            tint.transform.position = new Vector3(camX, camY, 0);
            tint.transform.localScale = new Vector3((halfW * 2 + 2) * Art.PPU / 4, 17 * Art.PPU / 4, 1);
            tint.color = new Color(Tint.r, Tint.g, Tint.b, .36f * storming);

            DrawDust(camX, camY, halfW, windSide, storming, dt);
            DrawSand(camX, camY, halfW, now, dt);
            DrawPlants(camX, halfW, dt);
            DrawShelters(hero, now, dt);
            DrawScarf(hero, s, now, dt);

            // HUD
            float push = World.PushNow(g, s, hero);
            ArrowPush = push;
            bool safe = World.Sheltered(g, hero.x, hero.y);
            float phaseLength = now == Weather.Warning ? g.Storm.warn : g.Storm.blow;
            float side = World.StormPush(g, Mathf.Min(hero.x, g.MainW - 2), 5);
            hud.StormShow(now, now == Weather.Calm ? 0 : g.Storm.Left(s.t) / phaseLength, side, Mathf.Abs(side) / 6f, safe);

            // Sound: quieter from behind a wall
            if (!playing || !here) Sound.Wind(null, 0, dt);
            else if (now == Weather.Warning) Sound.Wind("wind_rise", .75f, dt * 4);
            else if (now == Weather.Storm) Sound.Wind("wind_storm", safe ? .4f : 1, dt * 4);
            else Sound.Wind(null, 0, dt);
            return hit;
        }

        void DrawDust(float camX, float camY, float halfW, float side, float k, float dt)
        {
            float width = halfW * 2 + 6, height = 7.5f;
            for (int b = 0; b < Banks; b++)
            {
                bankX[b] = Mathf.Repeat(bankX[b] + side * (15 + b * 6) * Mathf.Max(k, .1f) * dt, width);
                for (int half = 0; half < 2; half++)
                {
                    var v = bank[b * 2 + half];
                    v.enabled = k > 0;
                    if (!v.enabled) continue;
                    v.transform.localScale = new Vector3(width / (512f / Art.PPU), height / (256f / Art.PPU), 1);
                    v.transform.position = new Vector3(camX - halfW - 3 + bankX[b] - half * width, camY - 7.5f + b * 3.8f - .5f, 0);
                    v.color = new Color(.86f, .62f, .34f, .5f * k);
                }
            }
        }

        void DrawSand(float camX, float camY, float halfW, Weather now, float dt)
        {
            int count = now == Weather.Storm ? Grains : now == Weather.Warning ? LightGrains : 0;
            float width = halfW * 2 + 2, fly = now == Weather.Storm ? 1.0f : .3f;
            GrainsOn = 0;
            for (int i = 0; i < Grains; i++)
            {
                var v = grain[i];
                if (i >= count) { v.enabled = false; continue; }
                float x = camX - halfW - 1 + gx[i] * width, y = camY - 7.5f + gy[i] * 15;
                float push = World.StormPush(g, Mathf.Min(x, g.MainW - 2), 5);
                gx[i] = Mathf.Repeat(gx[i] + Mathf.Sign(push) * 36 * speed[i] * fly * dt / width, 1);
                gy[i] = Mathf.Repeat(gy[i] - .02f * speed[i] * dt, 1);
                // The sand does not enter a shelter: its air stays clear
                bool inside = x >= g.MainW || now == Weather.Storm && World.Sheltered(g, x, y);
                v.enabled = !inside;
                if (inside) continue;
                GrainsOn++;
                v.transform.position = new Vector3(x, y + Mathf.Sin(clock * 7 + i) * .12f, 0);
            }
        }

        void DrawPlants(float camX, float halfW, float dt)
        {
            float sway = Mathf.Sin(clock * (2 + force * 9));
            PalmLean = 0;
            for (int i = 0; i < palms.Count; i++)
            {
                var (trunk, crown) = palms[i];
                if (Mathf.Abs(trunk.position.x - camX) > halfW + 5) continue;
                float side = Mathf.Sign(World.StormPush(g, trunk.position.x, 5));
                float lean = -side * force * 27 + (1.2f + force * 3.5f) * Mathf.Sin(clock * (2 + force * 9) + i);
                trunk.localEulerAngles = new Vector3(0, 0, lean);
                crown.localEulerAngles = new Vector3(0, 0, lean * .9f);
                PalmLean = Mathf.Max(PalmLean, Mathf.Abs(lean));
            }
            for (int i = 0; i < flags.Count; i++)
            {
                var (cloth, view) = flags[i];
                float side = Mathf.Sign(World.StormPush(g, cloth.position.x, 5));
                // Hangs in the calm and streams out straight in the storm; the flap grows with the wind
                float out_ = Mathf.Clamp01(force * 1.05f);
                float angle = side > 0 ? Mathf.Lerp(-82, 0, out_) : Mathf.Lerp(-98, -180, out_);
                view.flipY = side < 0;
                cloth.localEulerAngles = new Vector3(0, 0, angle + Mathf.Sin(clock * (3 + force * 18) + i) * (2 + force * 9));
                cloth.localScale = new Vector3(1, 1 - .25f * force * (.5f + .5f * sway), 1);
            }
        }

        void DrawShelters(Player hero, Weather now, float dt)
        {
            float pulse = .5f + .5f * Mathf.Sin(clock * 7);
            foreach (var (q, glow, sign) in shelters)
            {
                float a = now == Weather.Calm ? .1f : now == Weather.Warning ? .24f + .24f * pulse : .44f;
                glow.color = new Color(.55f, 1, .68f, a);
                float size = now == Weather.Calm ? 1 : 1.1f + .22f * pulse;
                sign.transform.localScale = new Vector3(size, size, 1);
            }
        }

        void DrawScarf(Player hero, in WorldState s, Weather now, float dt)
        {
            float push = World.PushNow(g, s, hero);
            bool safe = World.Sheltered(g, hero.x, hero.y);
            float side = push != 0 ? Mathf.Sign(push) : Mathf.Sign(World.StormPush(g, Mathf.Min(hero.x, g.MainW - 2), 5));
            float goal = push != 0 ? 1 : now == Weather.Warning && !safe ? .3f : 0;
            heroK = Mathf.MoveTowards(heroK, goal, dt * 8);

            // The hero leans into the wind and is drawn over the dust, so it never gets lost in the storm
            HeroTilt = hero.grounded ? side * heroK * 8 : 0;
            HeroOrder = force > .35f ? 45 : 10;

            bool show = !hero.dead;
            var at = new Vector2(hero.x - hero.facing * .1f, hero.Top - hero.h * .3f);
            for (int i = 0; i < scarf.Length; i++)
            {
                var v = scarf[i];
                v.enabled = show;
                if (!show) continue;
                v.sortingOrder = HeroOrder - 1;
                float wave = Mathf.Sin(clock * (3 + heroK * 17) - i * .9f) * (.12f + heroK * .55f);
                // Hangs down the back in the calm; streams straight out with the wind
                var hang = new Vector2(-hero.facing * .4f, -1).normalized;
                var fly = new Vector2(side, wave).normalized;
                var dir = Vector2.Lerp(hang, fly, heroK).normalized;
                at += dir * (.13f + heroK * .05f);
                v.transform.position = at;
                v.transform.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }
        }
    }
}
