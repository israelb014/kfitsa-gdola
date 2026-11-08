using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kfitsa
{
    /// Hats and pants for the hero (v2.1). Every 3 stars opens the next item, hats and pants in turn.
    /// The pants are baked into the hero pictures; a hat is a second picture placed on the hair in every pose.
    public static class Outfit
    {
        public static readonly string[] Hats = { "", "beanie", "party", "top", "chef", "crown" };
        public static readonly string[] HatNames = { "בלי כובע", "כובע צמר", "כובע מסיבה", "מגבעת", "כובע שף", "כתר" };
        static readonly float[] Sink = { 0, .58f, .36f, .42f, .44f, .32f };   // how far down the hair the hat sits
        public static readonly string[] Pants = { "", "red", "black", "white", "orange", "pink" };
        public static readonly string[] PantsNames = { "מכנסיים כחולים", "מכנסיים אדומים", "מכנסיים שחורים", "מכנסיים לבנים", "מכנסיים כתומים", "מכנסיים ורודים" };
        public const int StarsPerItem = 3;
        public static int ItemCount => Hats.Length - 1 + Pants.Length - 1;

        /// What is worn now; App copies it from the save.
        public static int Hat, Pant;

        /// Stars needed for an item: the first hat at 3, the first pants at 6, the second hat at 9, and so on.
        public static int StarsFor(bool hat, int index) => index == 0 ? 0 : StarsPerItem * (2 * (index - 1) + (hat ? 1 : 2));
        public static bool Open(bool hat, int index, int stars) => stars >= StarsFor(hat, index);
        public static int OpenCount(int stars) => Mathf.Min(ItemCount, stars / StarsPerItem);
        /// The next item still closed, or -1 when everything is open.
        public static int NextAt(int stars) => OpenCount(stars) >= ItemCount ? -1 : (OpenCount(stars) + 1) * StarsPerItem;
        public static string ItemName(int n) => n % 2 == 1 ? HatNames[(n + 1) / 2] : PantsNames[n / 2];   // n counts from 1

        public static string HeroSprite(char form, string pose) => Pant == 0 ? $"hero_{form}_{pose}" : $"hero_{form}_{pose}_{Pants[Pant]}";

        static Dictionary<string, Vector3> head;
        /// Where the hair is in a hero picture: x of its middle, y of its top, its height, all as fractions of the picture.
        static Vector3 Head(char form, string pose)
        {
            if (head == null)
            {
                head = new Dictionary<string, Vector3>();
                var t = Resources.Load<TextAsset>("Art/hat_meta");
                if (t != null)
                    foreach (var line in t.text.Split('\n'))
                    {
                        var a = line.Trim().Split(' ');
                        if (a.Length == 4) head[a[0]] = new Vector3(F(a[1]), F(a[2]), F(a[3]));
                    }
            }
            return head.TryGetValue(form + "_" + pose, out var v) ? v : new Vector3(.5f, .85f, .35f);
        }
        static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

        static string HatArt(char form) => "hat_" + Hats[Hat] + (form == 's' ? "_s" : "");

        /// Puts the hat on a hero in the world. The hat is a child of the hero, so it follows its flip, squash and tilt.
        public static void Place(SpriteRenderer hat, SpriteRenderer hero, char form, string pose)
        {
            hat.enabled = Hat != 0 && hero.enabled;
            if (Hat == 0) return;
            hat.sprite = Art.Feet(HatArt(form));
            var s = hero.sprite; var h = Head(form, pose);
            float w = s.rect.width / s.pixelsPerUnit, hh = s.rect.height / s.pixelsPerUnit;
            float px = s.pivot.x / s.rect.width, py = s.pivot.y / s.rect.height;
            float x = (h.x - px) * w;
            hat.transform.localPosition = new Vector3(hero.flipX ? -x : x, (h.y - Sink[Hat] * h.z - py) * hh, 0);
            hat.transform.localRotation = Quaternion.identity; hat.transform.localScale = Vector3.one;
            hat.flipX = hero.flipX;
            hat.color = hero.color;
            hat.sortingOrder = hero.sortingOrder + 1;
        }

        /// The same for a hero picture in a menu or on the map. The picture keeps its aspect inside its box.
        public static void Place(Image hat, Image hero, char form, string pose)
        {
            hat.enabled = Hat != 0;
            if (Hat == 0) return;
            hat.sprite = Art.Center(HatArt(form)); hat.preserveAspect = true;
            var s = hero.sprite; var box = hero.rectTransform.sizeDelta; var h = Head(form, pose);
            float aspect = s.rect.width / s.rect.height;
            Vector2 shown = box.x / box.y > aspect ? new Vector2(box.y * aspect, box.y) : new Vector2(box.x, box.x / aspect);
            float k = shown.y / s.rect.height;     // screen units per picture pixel
            var r = hat.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.pivot = new Vector2(.5f, 0);
            r.sizeDelta = new Vector2(hat.sprite.rect.width, hat.sprite.rect.height) * k;
            r.anchoredPosition = new Vector2((h.x - .5f) * shown.x, (h.y - Sink[Hat] * h.z - .5f) * shown.y);
        }
    }
}
