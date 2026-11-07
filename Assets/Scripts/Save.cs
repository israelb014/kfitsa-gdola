using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kfitsa
{
    [Serializable]
    public class ScoreRow { public int score; public string date; public string reached; }

    /// Progress, high scores and settings, in one JSON file.
    [Serializable]
    public class Save
    {
        public int unlocked;                 // index of the furthest level that can be played
        public int[] best = new int[Levels.All.Length];
        public List<ScoreRow> scores = new List<ScoreRow>();
        public float music = .7f, sfx = .8f;
        public bool fullscreen = true;
        public int difficulty;                               // 0 normal, 1 easy
        public int[] stars = new int[Levels.All.Length];     // one bit per star found

        // Touch controls
        public float buttonAlpha = .8f, buttonSize = 1;
        public bool leftHanded, haptics = true;
        public int buttonColor, runMode;                     // runMode: see TouchUi.RunMode
        public int taught;                                   // touch lessons already shown, one bit each

        /// Tests must never touch the player's real progress.
        public static bool Sandbox;

        static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static Save Load()
        {
            if (Sandbox) return new Save();
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = JsonUtility.FromJson<Save>(File.ReadAllText(FilePath));
                    if (s.best == null || s.best.Length != Levels.All.Length) s.best = new int[Levels.All.Length];
                    if (s.stars == null || s.stars.Length != Levels.All.Length) s.stars = new int[Levels.All.Length];
                    return s;
                }
            }
            catch (Exception e) { Debug.LogWarning("Save unreadable, starting fresh: " + e.Message); }
            return new Save();
        }

        public void Write()
        {
            if (Sandbox) return;
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(this)); }
            catch (Exception e) { Debug.LogWarning("Could not save: " + e.Message); }
        }

        /// Returns the rank (0 based) if the score made the top ten, otherwise -1.
        public int AddScore(int score, string reached)
        {
            if (score <= 0) return -1;
            var row = new ScoreRow { score = score, date = DateTime.Now.ToString("dd.MM.yy"), reached = reached };
            int at = scores.FindIndex(r => r.score < score);
            if (at < 0) at = scores.Count;
            if (at >= 10) return -1;
            scores.Insert(at, row);
            if (scores.Count > 10) scores.RemoveAt(10);
            Write();
            return at;
        }
    }
}
