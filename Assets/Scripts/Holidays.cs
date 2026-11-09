using System;
using System.Globalization;
using UnityEngine;

namespace Kfitsa
{
    /// v2.9: the holiday levels open by the Hebrew calendar, worked out on the device with no internet.
    /// A holiday level opens on the days of its holiday (as kept in Israel) and stays open for good once seen open.
    /// The Hebrew day really begins at nightfall; the device's midnight is close enough for a game.
    public static class Holidays
    {
        public struct Def { public string id, name, when, icon, coin; }
        public static readonly Def[] All =
        {
            new Def { id = "hanukkah", name = "חנוכה", when = "בחנוכה", icon = "h_hanukkiah", coin = "h_oil" },
            new Def { id = "purim", name = "פורים", when = "בפורים", icon = "h_mask", coin = "h_mask" },
            new Def { id = "pesach", name = "פסח", when = "בפסח", icon = "h_cup", coin = "h_matzah" },
            new Def { id = "sukkot", name = "סוכות", when = "בסוכות", icon = "h_sukkah", coin = "h_etrog" },
            new Def { id = "roshhashana", name = "ראש השנה", when = "בראש השנה", icon = "h_apple", coin = "h_apple" },
        };

        /// Tests set this to pretend another day.
        public static DateTime? Today;
        static DateTime Now => Today ?? DateTime.Now;

        static readonly HebrewCalendar heb = new HebrewCalendar();

        /// The Hebrew date: year, month (Tishrei = 1, in the order of the year) and day.
        public static (int year, int month, int day) Hebrew(DateTime d)
        {
            int y = heb.GetYear(d), m = heb.GetMonth(d), day = heb.GetDayOfMonth(d);
            return (y, m, day);
        }

        /// The month numbers of Adar (the one with Purim) and Nisan in a Hebrew year: a leap year has a second Adar.
        static int PurimMonth(int year) => heb.IsLeapYear(year) ? 7 : 6;
        static int NisanMonth(int year) => heb.IsLeapYear(year) ? 8 : 7;

        /// Which holiday is today, or -1.
        public static int OnDay(DateTime d)
        {
            var (y, m, day) = Hebrew(d);
            if (m == 1 && (day == 1 || day == 2)) return 4;                     // Rosh Hashana, 1-2 Tishrei
            if (m == 1 && day >= 15 && day <= 22) return 3;                     // Sukkot and Shemini Atzeret, 15-22 Tishrei
            // Hanukkah: eight days from 25 Kislev, into Tevet (Kislev has 29 or 30 days)
            int kislevDays = heb.GetDaysInMonth(y, 3);
            if (m == 3 && day >= 25) return 0;
            if (m == 4 && day <= 8 - (kislevDays - 24)) return 0;
            if (m == PurimMonth(y) && (day == 14 || day == 15)) return 1;       // Purim and Shushan Purim
            if (m == NisanMonth(y) && day >= 15 && day <= 21) return 2;         // Pesach, 15-21 Nisan
            return -1;
        }
        public static int Current => OnDay(Now);
    }
}
