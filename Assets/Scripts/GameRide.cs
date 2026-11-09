using UnityEngine;

namespace Kfitsa
{
    /// v2.7: an animal to ride, one for each world, found in a box. A hit while riding loses only the animal.
    public partial class Game
    {
        public static readonly string[] Animals = { "ram", "camel", "penguin", "bird" };
        public static readonly string[] AnimalNames = { "צמרי הכבש", "גמלי הגמל", "פינגי הפינגווין", "נוצי הציפור" };
        static readonly string[] AnimalTips =
        {
            "בריצה הוא נוגח ומעיף אויבים", "הולך על קוצים בלי פגע, וקופץ גבוה יותר", "בריצה מחליק על הבטן ושובר קרח", "מחזיקים קפיצה באוויר, והוא דואה לאט",
        };
        public const float CamelJump = 1.18f;

        int ride = -1;
        SpriteRenderer rideView;
        float rideAnim;
        public int Riding => ride;
        public string RideSpriteName => rideView != null && rideView.sprite != null ? rideView.sprite.name : "";
        /// The animal of this level's world; the test level gets the ram.
        int AnimalOfLevel => Mathf.Clamp(Def.world - 1, 0, Animals.Length - 1);
        public void MountForTest(int kind) => Mount(kind);
        public bool SlidingNow => Sliding;
        bool Sliding => ride == 2 && hero.grounded && Mathf.Abs(hero.vx) > Tune.WalkSpeed + .3f;
        /// Running into enemies knocks them away: the ram always, the penguin while it slides.
        bool Charging => !hero.dead && (ride == 0 && Mathf.Abs(hero.vx) > 2 || Sliding);

        void Mount(int kind)
        {
            ride = kind;
            hero.SetBig(true);
            hero.jumpScale = kind == 1 ? CamelJump : 1;
            hero.glide = kind == 3;
            if (rideView == null) rideView = Art.Spawn("Ride", root, Art.Feet($"ride_{Animals[kind]}_stand"), 12);
            Sound.Play("power"); Sound.Play("jump-high", .5f);
            freeze = .3f; growFlash = 0;
            app.hud.Hint(5f, Rtl.V(AnimalNames[kind] + "!  " + AnimalTips[kind]));
        }

        /// The animal is lost: after a hit it runs off, otherwise it just goes.
        void Dismount(bool runOff)
        {
            if (ride < 0) return;
            if (runOff && root != null)
                AddFx(Art.Center($"ride_{Animals[ride]}_walk_a"), new Vector2(hero.x, hero.y + .6f), new Vector2(-hero.facing * 5, 8), .9f, 30, 12);
            ride = -1;
            hero.jumpScale = 1; hero.glide = false;
            hero.SetBig(app.run.power > 0);
            if (rideView != null) { Destroy(rideView.gameObject); rideView = null; }
        }

        /// Called every step of play: the penguin's slide breaks the ice and bricks it runs into, and nobody climbs on an animal.
        void StepRide()
        {
            if (ride < 0) return;
            if (hero.climbing) { Dismount(true); app.hud.Hint(3f, Rtl.V("החיה לא מטפסת. היא חזרה הביתה")); return; }
            if (!Sliding) return;
            int c = Mathf.FloorToInt(hero.x + hero.facing * (hero.w / 2 + .12f));
            for (int r = Mathf.FloorToInt(hero.y + .1f); r <= Mathf.FloorToInt(hero.Top - .1f); r++)
            {
                var b = blocks.Find(k => k.c == c && k.r == r && k.kind == BlockKind.Brick);
                if (b != null && !tidy) BreakBrick(b);
            }
        }

        static float[] seat;
        /// How high the rider's feet sit above the ground, in tiles.
        float SeatHeight()
        {
            if (seat == null)
            {
                seat = new float[Animals.Length];
                var t = Resources.Load<TextAsset>("Art/ride_meta");
                if (t != null)
                    foreach (var line in t.text.Split('\n'))
                    {
                        var a = line.Trim().Split(' ');
                        int k = System.Array.IndexOf(Animals, a[0]);
                        if (k >= 0 && a.Length >= 5) seat[k] = float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture) * float.Parse(a[4]) / Art.PPU;
                    }
            }
            return seat[ride] > 0 ? seat[ride] : .7f;
        }

        void DrawRide(float dt)
        {
            if (ride < 0 || rideView == null) return;
            rideAnim += dt * (2 + Mathf.Abs(hero.vx) * 1.4f);
            string frame = Sliding ? "slide"
                : !hero.grounded ? (hero.glide && hero.vy < 0 && pad.jumpHeld ? "glide" : "jump")
                : Mathf.Abs(hero.vx) < .2f ? "stand" : (int)rideAnim % 2 == 0 ? "walk_a" : "walk_b";
            if (frame == "slide" && ride != 2 || frame == "glide" && ride != 3) frame = "jump";
            rideView.sprite = Art.Feet($"ride_{Animals[ride]}_{frame}");
            rideView.flipX = hero.facing < 0;
            float gone = phase == Phase.Won ? 1 - heroGone : 1;
            rideView.transform.position = new Vector3(hero.x, hero.y, 0);
            rideView.transform.localScale = new Vector3(gone, gone, 1);
            rideView.enabled = heroView.enabled;
            rideView.sortingOrder = heroView.sortingOrder + 2;      // in front of the rider's legs
        }
    }
}
