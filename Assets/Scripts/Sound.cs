using System.Collections.Generic;
using UnityEngine;

namespace Kfitsa
{
    /// One music channel and a small pool of effect voices.
    public static class Sound
    {
        static AudioSource music;
        static AudioSource[] voices;
        static int next;
        static string current;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        public static float MusicVolume = .7f, SfxVolume = .8f;

        public static void Init(GameObject host)
        {
            music = host.AddComponent<AudioSource>();
            music.loop = true; music.playOnAwake = false;
            voices = new AudioSource[10];
            for (int i = 0; i < voices.Length; i++) { voices[i] = host.AddComponent<AudioSource>(); voices[i].playOnAwake = false; }
        }

        static AudioClip Clip(string name)
        {
            if (!clips.TryGetValue(name, out var c)) clips[name] = c = Resources.Load<AudioClip>("Audio/" + name);
            return c;
        }

        public static void Play(string name, float volume = 1)
        {
            var c = Clip(name);
            if (c == null || voices == null) return;
            var v = voices[next = (next + 1) % voices.Length];
            v.pitch = 1; v.volume = volume * SfxVolume;
            v.PlayOneShot(c);
        }

        public static void Music(string name)
        {
            if (music == null) return;
            if (name == current && music.isPlaying) return;
            current = name;
            music.clip = name == null ? null : Clip(name);
            music.volume = MusicVolume * .6f;
            if (music.clip != null) music.Play(); else music.Stop();
        }

        public static void Duck(bool on) { if (music != null) music.volume = MusicVolume * (on ? .2f : .6f); }
        public static void ApplyVolume() { if (music != null) music.volume = MusicVolume * .6f; }
    }
}
