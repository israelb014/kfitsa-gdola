using System.Collections.Generic;
using UnityEngine;

namespace Kfitsa
{
    /// Slices the Kenney sheets at runtime. 18 pixels = 1 world unit.
    public static class Art
    {
        public const int PPU = 18;
        // Tile indices in tiles.png (20 columns)
        public const int Crate = 26, UsedBlock = 29, Gem = 67, CoinA = 151, CoinB = 152, Heart = 44, Dust = 157;
        public const int DoorTop = 130, DoorBottom = 150, PillarTop = 32, PillarMid = 52, PillarBase = 72;
        // Character indices in chars.png (9 columns)
        public const int HeroA = 6, HeroB = 7, EnemyA = 18, EnemyB = 19, EnemyFlat = 20;

        static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();
        static Texture2D tiles, chars, bgs, extras;
        static Material unlit;

        public static Material Unlit => unlit != null ? unlit : unlit = Resources.Load<Material>("Art/SpriteUnlit");

        static Texture2D Tex(ref Texture2D t, string name) => t != null ? t : t = Resources.Load<Texture2D>("Art/" + name);

        static Sprite Cut(Texture2D t, int px, int py, int size, Vector2 pivot) =>
            Sprite.Create(t, new Rect(px, t.height - py - size, size, size), pivot, PPU, 0, SpriteMeshType.FullRect);

        static Sprite Cached(int key, System.Func<Sprite> make)
        {
            if (!cache.TryGetValue(key, out var s)) cache[key] = s = make();
            return s;
        }

        /// Pivot at the bottom left, so a sprite at (c, r) fills that cell.
        public static Sprite Tile(int i) => Cached(i, () => Cut(Tex(ref tiles, "tiles"), i % 20 * 18, i / 20 * 18, 18, Vector2.zero));

        /// Same tile with a centred pivot, for things that spin or fly.
        public static Sprite TileC(int i) => Cached(1000 + i, () => Cut(Tex(ref tiles, "tiles"), i % 20 * 18, i / 20 * 18, 18, new Vector2(.5f, .5f)));

        /// Pivot at the feet.
        public static Sprite Char(int i) => Cached(2000 + i, () => Cut(Tex(ref chars, "chars"), i % 9 * 24, i / 9 * 24, 24, new Vector2(.5f, 0)));

        public static Sprite Bg(int i) => Cached(3000 + i, () => Cut(Tex(ref bgs, "bgs"), i % 8 * 24, i / 8 * 24, 24, Vector2.zero));

        public static Sprite BigHero(int frame) => Cached(4000 + frame, () => Cut(Tex(ref extras, "extras"), frame * 36, 0, 36, new Vector2(.5f, 0)));

        public static Sprite SurpriseBlock => Cached(5000, () =>
        {
            var t = Tex(ref extras, "extras");
            return Sprite.Create(t, new Rect(72, t.height - 18, 18, 18), Vector2.zero, PPU, 0, SpriteMeshType.FullRect);
        });

        public static SpriteRenderer Spawn(string name, Transform parent, Sprite s, int order)
        {
            var o = new GameObject(name);
            o.transform.SetParent(parent, false);
            var r = o.AddComponent<SpriteRenderer>();
            r.sharedMaterial = Unlit; r.sprite = s; r.sortingOrder = order;
            return r;
        }
    }
}
