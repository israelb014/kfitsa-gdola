using System;
using UnityEngine;

namespace Kfitsa
{
    /// Achievements (v2.2): one bit each in the save, a toast in the game when one opens, a screen with all of them.
    public static class Achievements
    {
        public struct Def { public string name, goal, icon; }
        public enum Id { First, NoHit, ThreeStars, AllStars, Secrets1, Secrets2, Secrets3, King, Giant, TopWin, Coins, Freeze, Chain, Bubble, Quiet, Hat }

        public static readonly Def[] All =
        {
            new Def { name = "צעד ראשון", goal = "מסיימים שלב", icon = "flag_a" },
            new Def { name = "בלי שריטה", goal = "מסיימים שלב בלי אף פגיעה", icon = "heart" },
            new Def { name = "שלושה כוכבים", goal = "כל שלושת הכוכבים בשלב אחד", icon = "star" },
            new Def { name = "כל הכוכבים", goal = "כל הכוכבים בשלושת העולמות", icon = "gem_red" },
            new Def { name = "חוקר עולם 1", goal = "כל הסודות בעולם 1", icon = "door_closed" },
            new Def { name = "חוקר עולם 2", goal = "כל הסודות בעולם 2", icon = "door_closed" },
            new Def { name = "חוקר עולם 3", goal = "כל הסודות בעולם 3", icon = "door_closed" },
            new Def { name = "מלך הג'לי הובס", goal = "מנצחים את מלך הג'לי", icon = "boss_walk_a" },
            new Def { name = "ענק הכפור הובס", goal = "מנצחים את ענק הכפור", icon = "giant_walk_a" },
            new Def { name = "סביבון מנצח", goal = "מכה אחרונה בבוס בסביבון", icon = "sevivon" },
            new Def { name = "מאה מטבעות", goal = "אוספים 100 מטבעות בסך הכול", icon = "coin_gold" },
            new Def { name = "קפוא!", goal = "מקפיאים אויב בסביבון כפור", icon = "sevivon_frost" },
            new Def { name = "רצף של שלוש", goal = "דורכים על 3 אויבים בלי לנחות", icon = "slime_normal_flat" },
            new Def { name = "בועה מצילה", goal = "בועת המגן עוצרת פגיעה", icon = "bubble" },
            new Def { name = "על קצות האצבעות", goal = "שביל השקט בלי ציוץ של עכבר", icon = "mouse_rest" },
            new Def { name = "מתלבשים", goal = "חובשים כובע מארון הבגדים", icon = "hat_crown" },
        };

        /// Set by the app: where the bits live, and what to show when one opens.
        public static Func<Save> save;
        public static Action<Def> shown;

        public static bool Has(Id id) => save != null && (save().achievements >> (int)id & 1) != 0;
        public static int Count { get { int n = 0; if (save == null) return 0; for (int b = save().achievements; b != 0; b >>= 1) n += b & 1; return n; } }

        public static void Unlock(Id id)
        {
            if (save == null || Has(id)) return;
            var s = save(); s.achievements |= 1 << (int)id; s.Write();
            shown?.Invoke(All[(int)id]);
        }
    }
}
