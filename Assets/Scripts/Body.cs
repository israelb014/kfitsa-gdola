using UnityEngine;

namespace Kfitsa
{
    /// An axis-aligned box that moves through the tile grid. x is the centre, y is the feet.
    public class Body
    {
        const float Eps = 0.001f;
        public Grid2 grid;
        public float x, y, w, h, vx, vy;
        public bool grounded;
        public Platform on;   // moving platform under the feet, if any

        public float Left => x - w / 2;
        public float Right => x + w / 2;
        public float Top => y + h;

        public bool Overlaps(Body o) => Left < o.Right && Right > o.Left && y < o.Top && Top > o.y;

        protected int RowLo => Mathf.FloorToInt(y + Eps);
        protected int RowHi => Mathf.FloorToInt(Top - Eps);
        protected int ColLo => Mathf.FloorToInt(Left + Eps);
        protected int ColHi => Mathf.FloorToInt(Right - Eps);

        /// Returns true if a wall stopped the move.
        public bool MoveX(float dx)
        {
            x += dx;
            if (dx == 0) return false;
            int col = Mathf.FloorToInt(dx > 0 ? Right : Left);
            for (int r = RowLo; r <= RowHi; r++)
                if (grid.Solid(col, r))
                {
                    x = dx > 0 ? col - w / 2 - Eps : col + 1 + w / 2 + Eps;
                    vx = 0;
                    return true;
                }
            return false;
        }

        /// Falling lands on the floor; rising calls HitHead.
        public void MoveY(float dy)
        {
            y += dy;
            grounded = false;
            on = null;
            if (dy < 0)
            {
                foreach (var p in grid.Platforms)
                    if (y - dy >= p.Top - .05f && y <= p.Top && Right > p.Left && Left < p.Right)
                    {
                        y = p.Top; vy = 0; grounded = true; on = p;
                        return;
                    }
                int row = Mathf.FloorToInt(y);
                for (int c = ColLo; c <= ColHi; c++)
                    if (grid.Solid(c, row))
                    {
                        y = row + 1;
                        vy = 0; grounded = true;
                        return;
                    }
            }
            else if (dy > 0)
            {
                int row = Mathf.FloorToInt(Top);
                int lo = ColLo, hi = ColHi, hit = int.MinValue, hits = 0;
                for (int c = lo; c <= hi; c++)
                    if (grid.SolidFromBelow(c, row)) { hits++; hit = c; }
                if (hits > 0) HitHead(row, hits, hit);
            }
        }

        protected virtual void HitHead(int row, int hits, int col)
        {
            y = row - h - Eps;
            vy = 0;
        }
    }
}
