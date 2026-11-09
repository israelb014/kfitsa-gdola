using System.Collections.Generic;
using UnityEngine;

namespace Kfitsa
{
    /// v2.8, the bonus levels: the screen that moves by itself, the sea, and the mine cart.
    public partial class Game
    {
        // ---- auto-scroll: the left edge of the screen moves on at a fixed speed and pushes the hero ----

        float scrollX;
        public bool noScroll;       // tests that replay a route found without the scroll
        public float ScrollLeft { get => scrollX; set => scrollX = value; }
        bool Scrolling => grid.ScrollSpeed > 0 && !noScroll && !inRoom;

        void StartScroll() => scrollX = Mathf.Max(0, hero.x - 4);

        void StepScroll(float dt)
        {
            if (!Scrolling) return;
            scrollX = Mathf.Min(scrollX + grid.ScrollSpeed * dt, Mathf.Max(0, grid.MainW - 2 * HalfW));
        }

        /// Pushed by the edge into a wall: there is nowhere left to go.
        bool Crushed()
        {
            if (!Scrolling || hero.x > CamLeft + hero.w / 2 + .01f) return false;
            for (int r = Mathf.FloorToInt(hero.y + .1f); r <= Mathf.FloorToInt(hero.Top - .1f); r++)
                if (grid.Solid(Mathf.FloorToInt(hero.Right - .02f), r) || grid.Solid(Mathf.FloorToInt(hero.Left + .02f), r)) return true;
            return false;
        }

        // ---- the sea and the mine: bubbles, saws and the cart ----

        readonly List<SpriteRenderer> saws = new List<SpriteRenderer>();
        SpriteRenderer cartView;
        float bubbleT, cartRoll;
        public bool InCart => grid != null && grid.CartAt(hero.x) && !hero.dead;

        void BuildBonus()
        {
            saws.Clear(); cartView = null;
            foreach (var (c, r) in grid.Saws)
            {
                var v = Art.Spawn("Saw", root, Art.Center("saw_a"), 3);
                v.transform.position = new Vector3(c + .5f, r + .45f, 0);
                saws.Add(v);
            }
            if (grid.Cart) cartView = Art.Spawn("Cart", root, Art.Feet("cart_0"), 12);
            StartScroll();
        }

        void DrawBonus(float dt)
        {
            foreach (var s in saws) s.transform.localEulerAngles = new Vector3(0, 0, -simT * 540);
            if (cartView != null)
            {
                cartView.enabled = InCart;
                if (InCart)
                {
                    cartRoll += dt * Mathf.Abs(hero.vx) * 2;
                    cartView.sprite = Art.Feet((int)cartRoll % 2 == 0 ? "cart_0" : "cart_1");
                    cartView.transform.position = new Vector3(hero.x, hero.y, 0);
                    cartView.sortingOrder = heroView.sortingOrder + 2;
                }
            }
            if (grid.SwimAt(hero.x) && !hero.dead && (bubbleT -= dt) <= 0)
            {
                bubbleT = .7f;
                AddFx(Art.Center("bubble"), new Vector2(hero.x + hero.facing * .3f, hero.Top), new Vector2(Random.Range(-.3f, .3f), 1.6f), 1.2f, -1, 11, .12f);
            }
        }

        /// The hero stands in the cart: a little higher, so the cart hides the legs.
        float CartLift => InCart ? .3f : 0;
    }
}
