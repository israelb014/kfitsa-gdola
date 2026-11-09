using UnityEngine;

namespace Kfitsa
{
    public class Player : Body
    {
        public bool big, skidding, jumping, dead, shooter, climbing;
        public const float ClimbSpeed = 4.5f, ClimbSide = 2.5f, GrabWait = .25f;
        float grabWait;     // just jumped off a ladder: no grabbing it again straight away
        public int facing = 1;
        public bool plainJump;      // the search only: a running start adds no height
        public float invuln;
        public System.Action<int, int> onBump;   // a block was hit from below
        public System.Action onJump, onLand;
        public float landSpeed;     // how fast the last landing came down, for the squash
        // v2.7, a ridden animal: the camel jumps higher, the bird glides. Left at 1 and false for the search.
        public float jumpScale = 1; public bool glide;
        public const float GlideFall = 3f;
        // v2.8: under the sea the jump button is a stroke and the hero sinks slowly; in a mine cart the hero rolls on by itself
        public bool swimming;
        public const float SwimGravity = 11f, SwimSink = 3.2f, SwimStroke = 6.8f, SwimSpeed = 4.4f, SwimFast = 5.8f, SwimAccel = 10f, CartSpeed = 8.5f;

        float coyote, buffer, riseGravity = Tune.RiseGravity, fallStartVy;
        bool wasGrounded = true;

        public Player Copy() => (Player)MemberwiseClone();

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
            coyote -= dt; buffer -= dt; grabWait -= dt;
            if (p.jumpPressed) buffer = Tune.JumpBuffer;
            swimming = grid.SwimAt(x);
            if (swimming) { Swim(dt, p); return; }
            if (!climbing && CanGrab(p)) Grab(p);
            if (climbing) { Climb(dt, p); return; }

            Horizontal(dt, p);
            TryJump();
            Vertical(dt, p);
        }

        void Swim(float dt, Pad p)
        {
            float max = p.run ? SwimFast : SwimSpeed;
            vx = Mathf.MoveTowards(vx, p.x * max, SwimAccel * dt);
            if (p.x != 0) facing = (int)p.x;
            skidding = false; climbing = false; jumping = false;
            MoveX(vx * dt);
            if (buffer > 0) { vy = SwimStroke; buffer = 0; onJump?.Invoke(); }
            float sink = p.down ? SwimSink * 1.8f : SwimSink;
            vy = Mathf.Max(vy - SwimGravity * (p.down ? 2 : 1) * dt, -sink);
            MoveY(vy * dt);
            wasGrounded = grounded;
        }

        void Horizontal(float dt, Pad p)
        {
            if (grid.CartAt(x))
            {
                // The cart rolls on to the right at one speed, in the air too; a wall stops it until a jump clears it
                vx = CartSpeed; facing = 1; skidding = false;
                if (MoveX(vx * dt) && !grounded) LedgeStep(1, CartSpeed);
                return;
            }
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
            float speed = vx;
            if (MoveX(vx * dt) && !grounded && dir != 0 && speed * dir > 0) LedgeStep((int)dir, speed);
        }

        /// The feet stopped just short of a ledge's top: lift them onto it, so a nearly good jump is a good jump.
        void LedgeStep(int dir, float speed)
        {
            if (vy > Tune.LedgeMaxRise) return;
            int col = Mathf.FloorToInt(dir > 0 ? Right + .01f : Left - .01f), row = RowLo, top = row + 1;
            if (!grid.Solid(col, row) || top - y > Tune.LedgeAssist) return;
            float nx = x + dir * .08f, left = nx - w / 2, right = nx + w / 2;
            for (int r = top; r <= Mathf.FloorToInt(top + h - .001f); r++)
                for (int c = Mathf.FloorToInt(left + .001f); c <= Mathf.FloorToInt(right - .001f); c++)
                    if (grid.Solid(c, r)) return;
            x = nx; y = top; vy = 0; vx = speed * .7f;
        }

        void TryJump()
        {
            if (buffer <= 0 || !(grounded || coyote > 0)) return;
            float t = plainJump ? 0 : Mathf.InverseLerp(Tune.WalkSpeed, Tune.RunSpeed, Mathf.Abs(vx));
            vy = Mathf.Lerp(Tune.JumpSpeed, Tune.RunJumpSpeed, t) * (big ? Tune.BigJumpScale : 1) * jumpScale;
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
            else if (glide && vy <= 0 && p.jumpHeld) maxFall = GlideFall;
            vy = Mathf.Max(vy - g * dt, -maxFall);
            if (vy < 0) fallStartVy = vy;

            MoveY(vy * dt);

            if (grounded)
            {
                jumping = false;
                if (!wasGrounded && fallStartVy < -6f) { landSpeed = -fallStartVy; onLand?.Invoke(); }
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
                    int side = Mathf.FloorToInt(leftEdge ? nx - w / 2 : nx + w / 2);
                    bool free = true;
                    for (int r = RowLo; r <= row; r++) free &= !grid.Solid(side, r);
                    if (free) { x = nx; return; }
                }
            }
            base.HitHead(row, hits, col);
            int c = Mathf.FloorToInt(x);
            onBump?.Invoke(grid.Solid(c, row) ? c : col, row);
        }

        // ---- ladders and ropes: up holds on, and the jump button lets go with a normal jump ----

        protected override bool LandsOnLadders => !climbing;

        int Col => Mathf.FloorToInt(x);
        bool Holding => grid.Climbable(Col, Mathf.FloorToInt(y - .05f)) || grid.Climbable(Col, Mathf.FloorToInt(y + .6f));

        bool CanGrab(Pad p)
        {
            if (grabWait > 0) return false;
            if (p.up && Holding && !(grounded && grid.LadderTop(Col, Mathf.FloorToInt(y - .05f)) && !grid.Climbable(Col, Mathf.FloorToInt(y + .05f)))) return true;
            return p.down && grounded && on == null && grid.LadderTop(Col, Mathf.FloorToInt(y - .05f));
        }

        void Grab(Pad p)
        {
            float nx = Col + .5f;
            int lo = Mathf.FloorToInt(y + .001f), hi = Mathf.FloorToInt(y + h - .001f);
            for (int r = lo; r <= hi; r++)
                if (grid.Solid(Mathf.FloorToInt(nx - w / 2 + .001f), r) || grid.Solid(Mathf.FloorToInt(nx + w / 2 - .001f), r)) return;   // no room to line up
            x = nx; vx = 0; vy = 0;
            climbing = true; jumping = false; grounded = false; on = null; coyote = 0;
            if (p.down && !p.up) y -= .06f;      // from the top: step down into the ladder
        }

        void Climb(float dt, Pad p)
        {
            if (buffer > 0)
            {
                // Let go with an ordinary walking jump, in the direction held
                climbing = false; grabWait = GrabWait;
                vx = p.x * Tune.WalkSpeed; if (p.x != 0) facing = (int)p.x;
                vy = Tune.JumpSpeed * (big ? Tune.BigJumpScale : 1);
                riseGravity = Tune.RiseGravity;
                jumping = true; buffer = 0; wasGrounded = false;
                onJump?.Invoke();
                return;
            }
            if (p.x != 0) facing = (int)p.x;
            vx = p.x * ClimbSide;
            MoveX(vx * dt);
            if (!Holding) { climbing = false; vy = 0; wasGrounded = false; return; }   // stepped off the side
            float want = (p.up ? 1 : 0) - (p.down ? 1 : 0);
            vy = want * ClimbSpeed;
            if (vy > 0)
            {
                int top = Mathf.FloorToInt(y - .05f);
                if (!grid.Climbable(Col, top)) top = Mathf.FloorToInt(y + .6f);
                while (grid.Climbable(Col, top + 1)) top++;
                float ny = y + vy * dt;
                if (ny >= top + 1)
                {
                    if (grid.LadderTop(Col, top)) { y = top + 1; vy = 0; climbing = false; grounded = true; wasGrounded = true; return; }   // off the top, onto the ledge
                    ny = top + 1;       // a rope ends where it hangs from
                }
                MoveY(ny - y);
            }
            else if (vy < 0)
            {
                MoveY(vy * dt);
                if (grounded) { climbing = false; vy = 0; wasGrounded = true; return; }
                if (!Holding) { climbing = false; vy = 0; wasGrounded = false; return; }   // slid off the bottom of a rope
            }
            grounded = false; vy = 0;
            wasGrounded = false;
        }

        /// A spring: same rise physics as a jump, just a harder launch.
        public void Launch(float speed)
        {
            vy = speed; climbing = false;
            jumping = true; grounded = false; wasGrounded = false; coyote = 0; buffer = 0;
            riseGravity = Tune.RiseGravity;
        }

        public void Stomped(bool held)
        {
            vy = held || buffer > 0 ? Tune.StompBounceHeld : Tune.StompBounce; climbing = false;
            jumping = true; grounded = false; wasGrounded = false; coyote = 0; buffer = 0;
            riseGravity = Tune.RiseGravity;
        }
    }
}
