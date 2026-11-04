using UnityEngine;

namespace Kfitsa
{
    /// A 3-tile moving ledge you can land on from above.
    public class Platform
    {
        const float HalfW = 1.5f, Range = 3f, Speed = .9f;
        public float x, y, dx, dy;
        readonly float baseX, baseY;
        readonly bool sideways;
        float t;
        public Transform view;

        public float Left => x - HalfW;
        public float Right => x + HalfW;
        public float Top => y;

        public Platform(float cx, float top, bool horizontal) { x = baseX = cx; y = baseY = top; sideways = horizontal; }

        public void Step(float dt) => At(t + dt);

        /// Jumps to a moment in the cycle; dx and dy hold the move from the previous moment.
        public void At(float time)
        {
            t = time;
            float o = Mathf.Sin(t * Speed) * Range;
            float nx = sideways ? baseX + o : baseX, ny = sideways ? baseY : baseY + o;
            dx = nx - x; dy = ny - y; x = nx; y = ny;
        }
    }

    public class Enemy : Body
    {
        public char kind;              // e slime, n snail, y fly, j frog, p spike slime
        public SpriteRenderer view;
        public bool awake, alive = true, flying;
        public int state;              // snail: 0 walking, 1 in shell, 2 sliding
        public float flat, safe, t, shellT;
        public int dir = -1;
        float baseX, baseY, hop = .8f;

        public void Init()
        {
            baseX = x; baseY = y + 2;
            if (kind == 'y') y = baseY;
        }

        public void Step(float dt, float heroX)
        {
            t += dt; safe = Mathf.Max(0, safe - dt);
            if (flying) { vy -= 40f * dt; x += vx * dt; y += vy * dt; return; }
            if (!alive) { flat -= dt; return; }
            switch (kind)
            {
                case 'y':
                    float nx = baseX + Mathf.Sin(t * .9f) * 3.5f;
                    dir = nx > x ? 1 : -1;
                    x = nx; y = baseY + Mathf.Sin(t * 2.2f) * .6f;
                    return;
                case 'j':
                    vy = Mathf.Max(vy - 40f * dt, -Tune.MaxFall);
                    if (grounded)
                    {
                        vx = 0; hop -= dt;
                        if (hop <= 0) { hop = 1.3f; dir = heroX < x ? -1 : 1; vx = dir * 4.2f; vy = 12.5f; }
                    }
                    if (MoveX(vx * dt)) { vx = -vx; dir = -dir; }
                    MoveY(vy * dt);
                    return;
            }
            float speed = Tune.EnemySpeed;
            if (kind == 'n')
            {
                if (state == 1)
                {
                    speed = 0; shellT -= dt;
                    if (shellT <= 0) state = 0;
                }
                else if (state == 2) speed = 9.5f;
                else speed = 1.3f;
            }
            vy = Mathf.Max(vy - Tune.EnemyGravity * dt, -Tune.MaxFall);
            if (speed > 0 && MoveX(dir * speed * dt)) dir = -dir;
            MoveY(vy * dt);
            // Snails and spike slimes are careful; plain slimes walk off edges
            if (grounded && state == 0 && (kind == 'n' || kind == 'p'))
            {
                int ahead = Mathf.FloorToInt(x + dir * (w / 2 + .1f));
                if (!grid.Solid(ahead, Mathf.FloorToInt(y - .1f))) dir = -dir;
            }
        }

        public void Draw()
        {
            view.transform.position = new Vector3(x, y, 0);
            view.flipX = dir > 0;
            view.flipY = flying;
            bool a = (int)(t * 6) % 2 == 0;
            switch (kind)
            {
                case 'e': view.sprite = Art.Feet(!alive && !flying ? "slime_normal_flat" : a ? "slime_normal_walk_a" : "slime_normal_walk_b"); break;
                case 'p': view.sprite = Art.Feet(!alive && !flying ? "slime_spike_flat" : a ? "slime_spike_walk_a" : "slime_spike_walk_b"); break;
                case 'n': view.sprite = Art.Feet(state > 0 || !alive ? "snail_shell" : a ? "snail_walk_a" : "snail_walk_b"); break;
                case 'y': view.sprite = Art.Feet(!alive ? "fly_rest" : (int)(t * 14) % 2 == 0 ? "fly_a" : "fly_b"); break;
                case 'j': view.sprite = Art.Feet(!alive ? "frog_rest" : grounded ? "frog_idle" : "frog_jump"); break;
            }
        }
    }

    /// The boss: hops at the hero, shakes the ground, and calls slimes when it lands.
    public class Boss : Body
    {
        public const int MaxHp = 8;
        public SpriteRenderer view;
        public int hp = MaxHp, landings;
        public bool awake, alive = true;
        public float hurt, wait = 1.5f, t;
        public int dir = -1;
        public System.Action onLand;

        public void Step(float dt, float heroX)
        {
            t += dt; hurt = Mathf.Max(0, hurt - dt);
            if (!alive) { vy -= 30f * dt; y += vy * dt; return; }
            vy = Mathf.Max(vy - 40f * dt, -22f);
            bool air = !grounded;
            if (MoveX(vx * dt)) vx = 0;
            MoveY(vy * dt);
            if (air && grounded) { vx = 0; landings++; onLand?.Invoke(); wait = hp <= 3 ? .55f : 1.05f; }
            if (grounded)
            {
                wait -= dt;
                if (wait <= 0)
                {
                    float air2 = .9f;
                    dir = heroX < x ? -1 : 1;
                    vx = Mathf.Clamp((heroX - x) / air2, -9f, 9f);
                    vy = 18f; grounded = false;
                }
            }
        }

        public void Draw()
        {
            view.transform.position = new Vector3(x, y, 0);
            view.flipX = dir > 0;
            view.flipY = !alive;
            view.sprite = Art.Feet(!grounded ? "boss_rest" : (int)(t * 5) % 2 == 0 ? "boss_walk_a" : "boss_walk_b");
            view.color = hurt > 0 && (int)(t * 16) % 2 == 0 ? new Color(1, .5f, .5f) : Color.white;
        }
    }

    /// The growth gem: rises out of its block, then slides along the ground.
    public class Pickup : Body
    {
        public SpriteRenderer view;
        public string type;     // grow, shoot, life
        public float rise = .55f;
        int dir = 1;

        public void Step(float dt)
        {
            if (rise > 0) { rise -= dt; y += dt / .55f; return; }
            if (type == "shoot") return;     // stays put on its block
            vy = Mathf.Max(vy - 40f * dt, -Tune.MaxFall);
            if (MoveX(dir * 3.2f * dt)) dir = -dir;
            MoveY(vy * dt);
        }

        public void Draw(float t) => view.transform.position = new Vector3(x, y + .5f + Mathf.Sin(t * 8) * .05f, 0);
    }

    /// The shooter's weapon: a spinning disc that flies out and comes back.
    public class Disc
    {
        public SpriteRenderer view;
        public float x, y, vx, t;
        public bool back;

        public bool Hits(Body b) => Mathf.Abs(x - b.x) < b.w / 2 + .25f && y > b.y - .25f && y < b.Top + .25f;
    }

    public class Block
    {
        public int c, r;
        public BlockKind kind;
        public bool used;
        public float bump;
        public SpriteRenderer view;
    }

    public class Spring { public int c, r; public float out_; public SpriteRenderer view; }
    public class CoinItem { public Vector2 at; public SpriteRenderer view; }
    public class Fx { public SpriteRenderer view; public Vector2 at, vel; public float life, gravity, spin; }
}
