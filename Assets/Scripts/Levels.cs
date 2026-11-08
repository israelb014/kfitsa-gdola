using System.IO;
using UnityEngine;

namespace Kfitsa
{
    public class LevelDef
    {
        public string file, name, terrain, sky, music, tip, roomTip;
        public int world, stage;
        public bool boss;
        public string Code => boss ? "בוס" : world + "-" + stage;
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
            new LevelDef { file = "boss", name = "מלך הג'לי", terrain = "stone", sky = "ember", music = "m_boss", world = 2, stage = 4, boss = true,
                tip = "המלך רודף אחריכם! רוצו!", roomTip = "הביסו את כל הג'לי לפני שהזמן נגמר" },
            new LevelDef { file = "w3l1", name = "מגדל הסולמות", terrain = "snow", sky = "snow", music = "m_world3", world = 3, stage = 1,
                tip = "חץ למעלה מטפס בסולם ובחבל, וקפיצה עוזבת", roomTip = "מרוץ טיפוס! הגיעו לכוכב לפני שהזמן נגמר" },
            new LevelDef { file = "w3l2", name = "ענני השלג", terrain = "snow", sky = "snow", music = "m_world3", world = 3, stage = 2,
                tip = "ענן שלג שוקע כשעומדים עליו. אל תתעכבו", roomTip = "דרכו על כל הג'לי-בלוקים והכוכב יופיע" },
            new LevelDef { file = "w3l3", name = "שביל השקט", terrain = "snow", sky = "snow", music = "m_world3", world = 3, stage = 3,
                tip = "עכבר ששומע ריצה מעיר את כולם. לידו הולכים לאט, או דורכים עליו", roomTip = "הכוכב מעבר לעכבר. הולכים, לא רצים" },
            new LevelDef { file = "boss2", name = "ענק הכפור", terrain = "snow", sky = "snow", music = "m_boss", world = 3, stage = 4, boss = true,
                tip = "הענק זורק קוביות שלג ואז נתקע לרגע. זה הזמן לקפוץ עליו", roomTip = "אספו את כל המטבעות שעל החבלים לפני שהזמן נגמר" },
        };
    }
}
