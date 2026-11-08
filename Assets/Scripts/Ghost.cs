using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kfitsa
{
    /// The time trial's ghost (v2.3): where the hero was, 30 times a second, packed small enough for the save file.
    /// Positions rather than button presses, so the ghost never drifts from the run it shows, even where the
    /// hero broke a block or a cloud sank differently.
    public class Ghost
    {
        public const float Rate = 30;
        static readonly string[] Poses = { "stand", "walk1", "walk2", "jump", "fall", "skid", "hurt", "duck", "cheer1", "cheer2", "action1", "climb1", "climb2" };
        static readonly char[] Forms = { 's', 'b', 'p', 'f' };

        public struct Frame { public float x, y; public string pose; public char form; public bool flip; }
        readonly List<Frame> frames = new List<Frame>();
        public int Count => frames.Count;
        public float Length => frames.Count / Rate;

        public void Add(float x, float y, string pose, char form, bool flip) =>
            frames.Add(new Frame { x = x, y = y, pose = pose, form = form, flip = flip });

        /// Where the ghost is at a time since the start, between two recorded frames.
        public Frame At(float t)
        {
            if (frames.Count == 0) return default;
            float f = Mathf.Clamp(t * Rate, 0, frames.Count - 1);
            int i = (int)f; var a = frames[i]; var b = frames[Mathf.Min(i + 1, frames.Count - 1)];
            float k = f - i;
            a.x = Mathf.Lerp(a.x, b.x, k); a.y = Mathf.Lerp(a.y, b.y, k);
            return a;
        }

        // 5 bytes a frame: x and y in fiftieths of a tile, then the pose, the form and the facing
        public string Pack()
        {
            var bytes = new byte[frames.Count * 5];
            for (int i = 0; i < frames.Count; i++)
            {
                var f = frames[i];
                int x = Mathf.Clamp(Mathf.RoundToInt(f.x * 50), 0, 65535), y = Mathf.Clamp(Mathf.RoundToInt((f.y + 20) * 50), 0, 65535);
                int p = Mathf.Max(0, Array.IndexOf(Poses, f.pose)), fm = Mathf.Max(0, Array.IndexOf(Forms, f.form));
                bytes[i * 5] = (byte)x; bytes[i * 5 + 1] = (byte)(x >> 8);
                bytes[i * 5 + 2] = (byte)y; bytes[i * 5 + 3] = (byte)(y >> 8);
                bytes[i * 5 + 4] = (byte)(p | fm << 4 | (f.flip ? 64 : 0));
            }
            return Convert.ToBase64String(bytes);
        }

        public static Ghost Unpack(string s)
        {
            var g = new Ghost();
            if (string.IsNullOrEmpty(s)) return g;
            byte[] b;
            try { b = Convert.FromBase64String(s); } catch (FormatException) { return g; }
            for (int i = 0; i + 4 < b.Length; i += 5)
            {
                int x = b[i] | b[i + 1] << 8, y = b[i + 2] | b[i + 3] << 8, k = b[i + 4];
                g.Add(x / 50f, y / 50f - 20, Poses[Mathf.Min(k & 15, Poses.Length - 1)], Forms[k >> 4 & 3], (k & 64) != 0);
            }
            return g;
        }
    }
}
