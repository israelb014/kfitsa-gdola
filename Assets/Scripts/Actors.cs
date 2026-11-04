using UnityEngine;

namespace Kfitsa
{
    public class Walker : Body
    {
        public SpriteRenderer view;
        public bool awake, alive = true;
        public float flatTime;
        float animT;

        public void Step(float dt)
        {
            if (!alive) { flatTime -= dt; return; }
            vy = Mathf.Max(vy - Tune.EnemyGravity * dt, -Tune.MaxFall);
            if (MoveX(dir * Tune.EnemySpeed * dt)) dir = -dir;
            MoveY(vy * dt);
            animT += dt;
        }

        public int dir = -1;

        public void Draw()
        {
            view.transform.position = new Vector3(x, y, 0);
            view.flipX = dir > 0;
            view.sprite = Art.Char(!alive ? Art.EnemyFlat : (int)(animT * 6) % 2 == 0 ? Art.EnemyA : Art.EnemyB);
        }
    }

    /// The growth crystal: rises out of its block, then slides along the ground.
    public class Crystal : Body
    {
        public SpriteRenderer view;
        public float rise = .55f;
        int dir = 1;

        public void Step(float dt)
        {
            if (rise > 0)
            {
                rise -= dt; y += dt / .55f;
                return;
            }
            vy = Mathf.Max(vy - 40f * dt, -Tune.MaxFall);
            if (MoveX(dir * 3.2f * dt)) dir = -dir;
            MoveY(vy * dt);
        }

        public void Draw(float t) => view.transform.position = new Vector3(x, y + .5f + Mathf.Sin(t * 8) * .05f, 0);
    }

    public class Surprise
    {
        public int c, r;
        public bool power, used;
        public float bump;
        public SpriteRenderer view;
    }

    public class CoinItem
    {
        public Vector2 at;
        public SpriteRenderer view;
    }

    public class Fx
    {
        public SpriteRenderer view;
        public Vector2 at, vel;
        public float life, gravity;
    }
}
