using System.IO;
using UnityEngine;

namespace Kfitsa
{
    public class LevelDef
    {
        public string file, name, terrain, sky, music, tip, roomTip;
        public string bossMusic, bossTitle;     // v2.5: the boss's own battle music, and the line under its name in its entrance
        public int world, stage;
        public bool boss, hidden;      // hidden: the world on the map's second page (v2.4)
        public string after;           // v2.8, a bonus level: the level that opens it; its stop sits beside that one on the map
        public bool Bonus => after != null;
        public int holiday = -1;      // v2.9: a holiday level, opened by the Hebrew calendar (see Holidays)
        public bool Holiday => holiday >= 0;
        /// Levels off the journey: bonus and holiday levels open nothing and have no time trial.
        public bool Extra => Bonus || Holiday;
        public string Code => Holiday ? Holidays.All[holiday].name : Bonus ? "בונוס" : boss ? "בוס" : world + "-" + stage;
    }

    public static class Levels
    {
        /// Tests can point this at a folder, to try level files without rebuilding the game.
        public static string Folder;

        public static string Text(string file)
        {
            if (Folder != null) return File.ReadAllText(Path.Combine(Folder, file + ".txt"));
            return Resources.Load<TextAsset>("Levels/" + file).text;
        }

        public static readonly LevelDef Test = new LevelDef { file = "test", name = "בדיקה", terrain = "grass", sky = "hills", music = "m_world1", world = 0, stage = 0, tip = "", roomTip = "" };

        public static readonly LevelDef[] All =
        {
            new LevelDef { file = "w1l1", name = "כר הקפיצים", terrain = "grass", sky = "hills", music = "m_world1", world = 1, stage = 1,
                tip = "", roomTip = "אספו את כל המטבעות לפני שהזמן נגמר" },
            new LevelDef { file = "w1l2", name = "יער המעליות", terrain = "grass", sky = "trees", music = "m_world1", world = 1, stage = 2,
                tip = "פלטפורמות נעות: חכו לרגע הנכון", roomTip = "מרוץ! הגיעו לכוכב לפני שהזמן נגמר" },
            new LevelDef { file = "w1l3", name = "מעלה ההר", terrain = "dirt", sky = "hills", music = "m_world1", world = 1, stage = 3,
                tip = "מטפסים למעלה. אבנים עם סימן אזהרה מתמוטטות", roomTip = "יש כאן דרך נסתרת" },
            new LevelDef { file = "w2l1", name = "סופת החול", terrain = "sand", sky = "desert", music = "m_world2", world = 2, stage = 1,
                tip = "שמיים כתומים? סופה בדרך. רוצו למחסה עם המגן הירוק", roomTip = "המתג הופך את כיוון הרוח" },
            new LevelDef { file = "w2l2", name = "ערוץ השיטפון", terrain = "sand", sky = "dusk", music = "m_world2", world = 2, stage = 2,
                tip = "המים עולים ויורדים. עלו למקום גבוה בזמן", roomTip = "המתג מרוקן את הבריכה ומחליף את הקוביות" },
            new LevelDef { file = "w2l3", name = "מצודת הלילה", terrain = "stone", sky = "night", music = "m_world2", world = 2, stage = 3,
                tip = "חשוך כאן. לפידים ומסועים מראים את הדרך", roomTip = "הדליקו את שלושת הלפידים" },
            new LevelDef { file = "boss", name = "מלך הג'לי", terrain = "stone", sky = "ember", music = "m_world2", bossMusic = "m_boss", bossTitle = "שליט הג'לי", world = 2, stage = 4, boss = true,
                tip = "המלך רודף אחריכם! רוצו!", roomTip = "הביסו את כל הג'לי לפני שהזמן נגמר" },
            new LevelDef { file = "w3l1", name = "מגדל הסולמות", terrain = "snow", sky = "snow", music = "m_world3", world = 3, stage = 1,
                tip = "חץ למעלה מטפס בסולם ובחבל, וקפיצה עוזבת", roomTip = "מרוץ טיפוס! הגיעו לכוכב לפני שהזמן נגמר" },
            new LevelDef { file = "w3l2", name = "ענני השלג", terrain = "snow", sky = "snow", music = "m_world3", world = 3, stage = 2,
                tip = "ענן שלג שוקע כשעומדים עליו. אל תתעכבו", roomTip = "דרכו על כל הג'לי-בלוקים והכוכב יופיע" },
            new LevelDef { file = "w3l3", name = "שביל השקט", terrain = "snow", sky = "snow", music = "m_world3", world = 3, stage = 3,
                tip = "עכבר ששומע ריצה מעיר את כולם. לידו הולכים לאט, או דורכים עליו", roomTip = "הכוכב מעבר לעכבר. הולכים, לא רצים" },
            new LevelDef { file = "boss2", name = "ענק הכפור", terrain = "snow", sky = "snow", music = "m_world3", bossMusic = "m_boss2", bossTitle = "שומר הפסגות", world = 3, stage = 4, boss = true,
                tip = "הענק זורק קוביות שלג ואז נתקע לרגע. זה הזמן לקפוץ עליו", roomTip = "אספו את כל המטבעות שעל החבלים לפני שהזמן נגמר" },
            // v2.4: the hidden world, open once every star of worlds 1 to 3 is found
            new LevelDef { file = "w4l1", name = "גשר הצבעים", terrain = "purple", sky = "clouds", music = "m_world4", world = 4, stage = 1, hidden = true,
                tip = "מתג בצבע מוציא את הבלוקים בצבע שלו ומעלים את האחרים", roomTip = "דלקו את כל המנורות. המתגים פותחים את הדרך" },
            new LevelDef { file = "w4l2", name = "כוורת בשמיים", terrain = "purple", sky = "clouds", music = "m_world4", world = 4, stage = 2, hidden = true,
                tip = "הדבורה עפה בשמינייה. החיפושית עפה אחרי הדריכה הראשונה", roomTip = "דרכו על כל החיפושיות. כל אחת צריכה שתי דריכות" },
            new LevelDef { file = "w4l3", name = "אגם הדגים", terrain = "purple", sky = "clouds", music = "m_world4", world = 4, stage = 3, hidden = true,
                tip = "הדגים קופצים מהמים בקצב קבוע. חכו שהם ירדו", roomTip = "מרוץ! ארבעה מתגים בדרך לכוכב" },
            new LevelDef { file = "boss3", name = "מלך הג'לי חוזר", terrain = "purple", sky = "clouds", music = "m_world4", bossMusic = "m_boss3", bossTitle = "עכשיו בכל הצבעים", world = 4, stage = 4, boss = true, hidden = true,
                tip = "המלך חוזר בצבעים! דרכו עליו רק אחרי המתג בצבע שלו", roomTip = "אספו את המטבעות בכל הצבעים" },
            // v2.8: bonus levels, each opened by a level of worlds 1 to 3, off the main path
            new LevelDef { file = "b1", name = "המסך לא מחכה", terrain = "grass", sky = "trees", music = "m_bonus", world = 11, stage = 1, after = "w1l3",
                tip = "המסך זז לבד, והקצה שלו דוחף. אל תישארו מאחור", roomTip = "ספרינט! עלו על המדפים לכוכב לפני שהזמן נגמר" },
            new LevelDef { file = "b2", name = "מתחת לים", terrain = "sand", sky = "sea", music = "m_bonus", world = 12, stage = 1, after = "w2l2",
                tip = "שוחים! כל קפיצה היא תנועת שחייה, וחץ למטה צולל", roomTip = "אספו את כל המטבעות לפני שהזמן נגמר" },
            new LevelDef { file = "b3", name = "מסילת המכרה", terrain = "stone", sky = "mine", music = "m_bonus", world = 13, stage = 1, after = "w3l2",
                tip = "העגלה נוסעת לבד. קופצים מעל בורות ומסורים", roomTip = "הדליקו את שלוש המנורות" },
            // v2.9: holiday levels, each opened on its holiday by the Hebrew calendar, then open for good
            new LevelDef { file = "hanukkah", name = "נס החנוכה", terrain = "stone", sky = "night", music = "m_world3", world = 21, stage = 1, holiday = 0,
                tip = "חשוך! הנרות מאירים את הדרך, ופכי השמן הם המטבעות", roomTip = "הדליקו את כל הנרות" },
            new LevelDef { file = "purim", name = "עדלאידע", terrain = "grass", sky = "hills", music = "m_world1", world = 22, stage = 1, holiday = 1,
                tip = "חג שמח! באים בתחפושת, והקפיצים מעיפים גבוה", roomTip = "אספו את כל המסכות לפני שהזמן נגמר" },
            new LevelDef { file = "pesach", name = "קריעת ים סוף", terrain = "sand", sky = "desert", music = "m_world2", world = 23, stage = 1, holiday = 2,
                tip = "הים נסוג ושב. עוברים כשהמים למטה", roomTip = "מחפשים את האפיקומן! מרוץ אל הכוכב" },
            new LevelDef { file = "sukkot", name = "ענני הכבוד", terrain = "dirt", sky = "trees", music = "m_world4", world = 24, stage = 1, holiday = 3,
                tip = "ענן שוקע כשעומדים עליו. מסוכה לסוכה", roomTip = "מרוץ עם הלולב אל הכוכב" },
            new LevelDef { file = "roshhashana", name = "תפוח בדבש", terrain = "grass", sky = "trees", music = "m_title", world = 25, stage = 1, holiday = 4,
                tip = "שנה טובה ומתוקה! הדבורות שומרות על הכוורות", roomTip = "אספו את כל התפוחים לפני שהזמן נגמר" },
        };
        public static int HolidayFirst => System.Array.FindIndex(All, d => d.Holiday);
        public static int HolidayLevel(int holiday) => System.Array.FindIndex(All, d => d.holiday == holiday);
        /// The levels of the journey and the hidden world; the bonus levels come after them.
        public static int StoryCount => System.Array.FindIndex(All, d => d.Extra) is int k && k >= 0 ? k : All.Length;
        public static int BonusOf(int level) => System.Array.FindIndex(All, d => d.after == All[level].file);
        public static int ParentOf(int level) => All[level].Bonus ? System.Array.FindIndex(All, d => d.file == All[level].after) : -1;
    }
}
