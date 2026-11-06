using System.Collections.Generic;
using UnityEngine;

namespace Kfitsa
{
    /// Loads single-image sprites from Resources/Art. 72 pixels = 1 world unit (1 tile).
    public static class Art
    {
        public const int PPU = 72;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Material unlit;

        public static Material Unlit => unlit != null ? unlit : unlit = Resources.Load<Material>("Materials/SpriteUnlit");

        static Sprite Get(string name, Vector2 pivot, string key)
        {
            if (cache.TryGetValue(key, out var s)) return s;
            var t = Resources.Load<Texture2D>("Art/" + name);
            if (t == null) { Debug.LogError("Missing art: " + name); t = Texture2D.whiteTexture; }
            s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), pivot, PPU, 0, SpriteMeshType.FullRect);
            return cache[key] = s;
        }

        /// Pivot at the bottom left, so a sprite at (c, r) fills that cell.
        public static Sprite Tile(string name) => Get(name, Vector2.zero, name);
        public static Sprite Center(string name) => Get(name, new Vector2(.5f, .5f), name + "#c");
        public static Sprite Feet(string name) => Get(name, new Vector2(.5f, 0), name + "#f");
        public static Sprite LeftMiddle(string name) => Get(name, new Vector2(0, .5f), name + "#l");

        /// form: s small, b big, p shooter. The hero canvas keeps a small gap under the feet.
        public static Sprite Hero(char form, string pose) =>
            Get($"hero_{form}_{pose}", new Vector2(.5f, form == 's' ? .0435f : .0286f), $"hero_{form}_{pose}");

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
