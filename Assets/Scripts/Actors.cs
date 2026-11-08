using UnityEngine;

namespace Kfitsa
{
    /// A moving ledge you can land on from above. Its position depends only on the clock.
    public class Platform
    {
        public float x, y, dx, dy;
        public readonly float baseX, baseY, halfW, moveX, moveY, period, phase;
        public Transform view;

        public float Left => x - halfW;
        public float Right => x + halfW;
        public float Top => y;

        public Platform(float cx, float top, bool horizontal) : this(cx, top, horizontal ? 3 : 0, horizontal ? 0 : 3, 7, 0, 3) { }

        public Platform(float cx, float top, float rangeX, float rangeY, float seconds, float start, float width)
        {
            x = baseX = cx; y = baseY = top; moveX = rangeX; moveY = rangeY; period = seconds; phase = start; halfW = width / 2;
            At(0); dx = dy = 0;
        }

        /// Jumps to a moment in the cycle; dx and dy hold the move from the previous moment.
        public void At(float time)
        {
            float o = Mathf.Sin((time / period + phase) * 2 * Mathf.PI);
            float nx = baseX + o * moveX, ny = baseY + o * moveY;
            dx = nx - x; dy = ny - y; x = nx; y = ny;
        }
    }

    public class Enemy : Body
    {
        public char kind;              // e slime, n snail, y fly, j frog, p spike slime, q block slime
        public SpriteRenderer view;
        public bool awake, alive = true, flying;
        public int state;              // snail: 0 walking, 1 in shell, 2 sliding
        public float flat, safe, t, shellT;
        public float frozen;           // seconds left inside a block of ice from the frost sevivon
        public Platform ice;           // the ice's top, which the hero can stand on
        public float thawT;            // just out of the ice: harmless for a moment, so the hero on top is not hurt
        public int dir = -1;
        public Platform block;         // a stomped block slime: it stays as a block to stand on
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
            if (grounded && state == 0 && (kind == 'n' || kind == 'p' || kind == 'q'))
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
            // Frozen: ice blue, and blinking for the last second before it thaws
            view.color = frozen <= 0 ? Color.white : frozen < 1.2f && (int)(frozen * 8) % 2 == 0 ? new Color(.85f, .95f, 1) : new Color(.55f, .8f, 1);
            bool a = (int)(t * 6) % 2 == 0;
            switch (kind)
            {
                case 'e': view.sprite = Art.Feet(!alive && !flying ? "slime_normal_flat" : a ? "slime_normal_walk_a" : "slime_normal_walk_b"); break;
                case 'p': view.sprite = Art.Feet(!alive && !flying ? "slime_spike_flat" : a ? "slime_spike_walk_a" : "slime_spike_walk_b"); break;
                case 'n': view.sprite = Art.Feet(state > 0 || !alive ? "snail_shell" : a ? "snail_walk_a" : "snail_walk_b"); break;
                case 'y': view.sprite = Art.Feet(!alive ? "fly_rest" : (int)(t * 14) % 2 == 0 ? "fly_a" : "fly_b"); break;
                case 'j': view.sprite = Art.Feet(!alive ? "frog_rest" : grounded ? "frog_idle" : "frog_jump"); break;
                case 'q': view.sprite = Art.Feet(block != null || !alive ? "slime_block_rest" : !grounded ? "slime_block_jump" : a ? "slime_block_walk_a" : "slime_block_walk_b"); break;
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
        public SpriteRenderer shadow;  // where it will come down
        public float aimX, jumpY;
        public const float WarnTime = .5f;
        /// The crouch before a leap: long enough to see and react to.
        public bool Warning => alive && awake && grounded && wait > 0 && wait < WarnTime;

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
                    aimX = x + vx * air2; jumpY = y;
                }
            }
        }

        public void Draw()
        {
            bool warn = Warning;
            float k = warn ? 1 - wait / WarnTime : 0;      // 0 to 1 through the crouch
            view.transform.position = new Vector3(x + (warn ? Mathf.Sin(t * 60) * .06f : 0), y, 0);
            view.transform.localScale = new Vector3(1 + .12f * k, 1 - .14f * k, 1);
            view.flipX = dir > 0;
            view.flipY = !alive;
            view.sprite = Art.Feet(!grounded ? "boss_rest" : (int)(t * 5) % 2 == 0 ? "boss_walk_a" : "boss_walk_b");
            view.color = hurt > 0 && (int)(t * 16) % 2 == 0 ? new Color(1, .5f, .5f)
                : warn && (int)(t * 12) % 2 == 0 ? new Color(1, .92f, .55f) : Color.white;
            if (shadow == null) return;
            // The shadow marks the landing spot and grows as the king comes down
            shadow.enabled = alive && !grounded;
            float high = Mathf.Clamp01((y - jumpY) / 4);
            shadow.transform.position = new Vector3(aimX, jumpY + .02f, 0);
            shadow.transform.localScale = new Vector3(2.4f - high, 1.2f - high * .4f, 1);
            shadow.color = new Color(1, 1, 1, 1 - high * .3f);
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
            if (type != "grow" && type != "life") return;     // stays put on its box, so it can never be lost
            vy = Mathf.Max(vy - 40f * dt, -Tune.MaxFall);
            if (MoveX(dir * 3.2f * dt)) dir = -dir;
            MoveY(vy * dt);
        }

        public void Draw(float t) => view.transform.position = new Vector3(x, y + .5f + Mathf.Sin(t * 8) * .05f, 0);
    }

    /// The thrown sevivon: hops along the ground, turns back once off a wall, and topples when it runs out of spin.
    public class Sevivon : Body
    {
        public const float Speed = 10.5f, Hop = 8.5f, Gravity = 42f, Life = 2.6f;
        public SpriteRenderer view;
        public float t, spin;
        public bool turned, gone, frost;
        public int dir;

        public void Step(float dt)
        {
            t += dt; spin += dt;
            vy = Mathf.Max(vy - Gravity * dt, -14f);
            if (MoveX(dir * Speed * dt))
            {
                if (turned) gone = true;
                turned = true; dir = -dir;
            }
            MoveY(vy * dt);
            if (grounded) vy = Hop;
            if (t > Life || y < -2) gone = true;
        }

        public bool Hits(Body b) => Left - .1f < b.Right && Right + .1f > b.Left && y - .1f < b.Top && Top + .1f > b.y;
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
    public class CoinItem { public Vector2 at; public SpriteRenderer view; public bool room; }
    public class Star { public Vector2 at; public int id; public SpriteRenderer view; public bool hidden; }
    public class Crumble { public int c, r; public float t; public bool down; public SpriteRenderer view; }
    public class Fx
    {
        public SpriteRenderer view; public Vector2 at, vel; public float life, gravity, spin;
        public float start, grow, alpha, size;    // dust: fades out and swells over its life
    }
}
