namespace Kfitsa
{
    public class LevelDef
    {
        public string file, name, terrain, sky, music;
        public int world, stage;
        public bool boss;
        public string Code => boss ? "בוס" : world + "-" + stage;
    }

    public static class Levels
    {
        public static readonly LevelDef[] All =
        {
            new LevelDef { file = "w1l1", name = "כר הדשא", terrain = "grass", sky = "hills", music = "m_world1", world = 1, stage = 1 },
            new LevelDef { file = "w1l2", name = "יער הצמרות", terrain = "grass", sky = "trees", music = "m_world1", world = 1, stage = 2 },
            new LevelDef { file = "w1l3", name = "מעלה ההר", terrain = "dirt", sky = "hills", music = "m_world1", world = 1, stage = 3 },
            new LevelDef { file = "w2l1", name = "חולות הזהב", terrain = "sand", sky = "desert", music = "m_world2", world = 2, stage = 1 },
            new LevelDef { file = "w2l2", name = "ערוץ השקיעה", terrain = "sand", sky = "dusk", music = "m_world2", world = 2, stage = 2 },
            new LevelDef { file = "w2l3", name = "מצודת הלילה", terrain = "stone", sky = "night", music = "m_world2", world = 2, stage = 3 },
            new LevelDef { file = "boss", name = "מלך הג'לי", terrain = "stone", sky = "ember", music = "m_boss", world = 2, stage = 4, boss = true },
        };
    }
}
