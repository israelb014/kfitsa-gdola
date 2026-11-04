using UnityEngine;

namespace Kfitsa
{
    public class Player : Body
    {
        public bool big, skidding, jumping, dead;
        public int facing = 1;
        public float invuln;
        public System.Action<int, int> onBump;   // a block was hit from below
        public System.Action onJump, onLand;

        float coyote, buffer, riseGravity = Tune.RiseGravity, fallStartVy;
        bool wasGrounded = true;

        public void SetBig(bool b)
        {
            big = b;
            w = b ? .95f : .75f;
            h = b ? 1.85f : 1.15f;
        }

        public void Step(float dt, Pad p)
        {
            if (dead)
            {
                vy -= 40f * dt; y += vy * dt;
                return;
            }
            invuln = Mathf.Max(0, invuln - dt);
            coyote -= dt; buffer -= dt;
            if (p.jumpPressed) buffer = Tune.JumpBuffer;

            Horizontal(dt, p);
            TryJump();
            Vertical(dt, p);
        }

        void Horizontal(float dt, Pad p)
        {
            float dir = p.x, max = p.run ? Tune.RunSpeed : Tune.WalkSpeed, rate;
            skidding = false;
            if (grounded)
            {
                if (dir != 0 && vx * dir < -0.2f) { rate = Tune.SkidDecel; skidding = true; }
                else if (dir == 0 || Mathf.Abs(vx) > max) rate = Tune.Friction;
                else rate = p.run ? Tune.RunAccel : Tune.WalkAccel;
                if (dir != 0) facing = (int)dir;
            }
            else
            {
                // In the air there is no friction: you keep your speed and can only steer
                if (dir == 0) rate = 0;
                else if (vx * dir < 0) rate = Tune.AirReverse;
                else if (Mathf.Abs(vx) > max) rate = 0;
                else rate = Tune.AirAccel;
            }
            vx = Mathf.MoveTowards(vx, dir * max, rate * dt);
            MoveX(vx * dt);
        }

        void TryJump()
        {
            if (buffer <= 0 || !(grounded || coyote > 0)) return;
            float t = Mathf.InverseLerp(Tune.WalkSpeed, Tune.RunSpeed, Mathf.Abs(vx));
            vy = Mathf.Lerp(Tune.JumpSpeed, Tune.RunJumpSpeed, t);
            riseGravity = Mathf.Lerp(Tune.RiseGravity, Tune.RunRiseGravity, t);
            jumping = true; grounded = false; wasGrounded = false; buffer = 0; coyote = 0;
            onJump?.Invoke();
        }

        void Vertical(float dt, Pad p)
        {
            float g, maxFall = Tune.MaxFall;
            if (vy > 0) g = jumping && p.jumpHeld ? riseGravity : (jumping ? Tune.CutGravity : Tune.FallGravity);
            else g = Tune.FallGravity;
            if (jumping && p.jumpHeld && Mathf.Abs(vy) < Tune.ApexSpeed) g = riseGravity * Tune.ApexGravityScale;
            if (vy <= 0 && p.down) { g *= Tune.FastFallScale; maxFall = Tune.FastFallMax; }
            vy = Mathf.Max(vy - g * dt, -maxFall);
            if (vy < 0) fallStartVy = vy;

            MoveY(vy * dt);

            if (grounded)
            {
                jumping = false;
                if (!wasGrounded && fallStartVy < -6f) onLand?.Invoke();
                fallStartVy = 0;
            }
            else if (wasGrounded && !jumping) coyote = Tune.Coyote;
            wasGrounded = grounded;
        }

        protected override void HitHead(int row, int hits, int col)
        {
            // Only one corner clipped: slide round it rather than stopping the jump dead
            if (hits == 1)
            {
                bool leftEdge = Right - col <= col + 1 - Left;   // the block is on our right
                float clip = leftEdge ? Right - col : col + 1 - Left;
                if (clip < Tune.CornerNudge)
                {
                    float nx = leftEdge ? col - w / 2 - .002f : col + 1 + w / 2 + .002f;
                    if (!grid.Solid(Mathf.FloorToInt(leftEdge ? nx - w / 2 : nx + w / 2), row)) { x = nx; return; }
                }
            }
            base.HitHead(row, hits, col);
            int c = Mathf.FloorToInt(x);
            onBump?.Invoke(grid.Solid(c, row) ? c : col, row);
        }

        public void Stomped(bool held)
        {
            vy = held || buffer > 0 ? Tune.StompBounceHeld : Tune.StompBounce;
            jumping = true; grounded = false; wasGrounded = false; coyote = 0; buffer = 0;
            riseGravity = Tune.RiseGravity;
        }
    }
}
