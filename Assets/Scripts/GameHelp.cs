using System.Collections.Generic;
using UnityEngine;

namespace Kfitsa
{
    /// v2.5: the smart helper, and every boss's entrance.
    public partial class Game
    {
        // ---- the smart helper: three deaths at the same spot, and a help box waits by the restart point ----

        public const int HelpAfter = 3;
        const float SameSpot = 3f;
        readonly List<float> deathXs = new List<float>();
        int deathLevel = -999;
        SpriteRenderer helpView, helpIcon;
        Vector2 helpAt; bool helpOn;
        public bool HelpBoxShown => helpOn;
        public Vector2 HelpBoxAt => helpAt;
        public int HelpGiven { get; private set; }
        public int DeathsHere => deathXs.Count;

        /// In a boss arena the whole fight counts as one spot: the boss moves, so the deaths spread out.
        float SpotOf(float x) => grid.HasBoss && x > grid.BossSpot.x - 18 ? grid.BossSpot.x : x;

        /// Some spot ahead of the restart point where the hero has fallen at least three times.
        bool NeedsHelp()
        {
            foreach (float x in deathXs)
            {
                float spot = SpotOf(x);
                if (spot < hero.x - 1) continue;      // already behind: that part is beaten
                int n = 0;
                foreach (float y in deathXs) if (Mathf.Abs(SpotOf(y) - spot) < SameSpot) n++;
                if (n >= HelpAfter) return true;
            }
            return false;
        }

        void PlaceHelpBox()
        {
            // Just ahead of the hero at chest height, in an open cell; the box is touched, never hit from below
            float y = hero.y + .9f;
            float x = hero.x;
            foreach (float dx in new[] { 1.6f, -1.6f, 2.6f, 0f })
                if (!grid.Solid(Mathf.FloorToInt(hero.x + dx), Mathf.FloorToInt(y)) && !grid.Solid(Mathf.FloorToInt(hero.x + dx), Mathf.FloorToInt(hero.y + .1f))) { x = hero.x + dx; break; }
            helpAt = new Vector2(x, y); helpOn = true;
            helpView = Art.Spawn("HelpBox", root, Art.Center("help_box"), 3);
            helpIcon = Art.Spawn("HelpIcon", root, Art.Center("bubble"), 4);
            helpIcon.transform.localScale = Vector3.one * .4f;
            app.hud.Hint(5f, Rtl.V("קופסת עזרה! גדילה ובועת מגן. אפשר לכבות אותה בהגדרות"));
        }

        void StepHelp(float dt)
        {
            if (!helpOn || tidy) return;
            if (Mathf.Abs(helpAt.x - hero.x) > hero.w / 2 + .45f || helpAt.y < hero.y - .45f || helpAt.y > hero.Top + .45f) return;
            helpOn = false; HelpGiven++;
            Destroy(helpView.gameObject); Destroy(helpIcon.gameObject); helpView = helpIcon = null;
            for (int i = 0; i < 4; i++)
                AddFx(Art.Center("help_box"), helpAt, new Vector2((i % 2 * 2 - 1) * Random.Range(2f, 4f), Random.Range(5f, 9f)), .5f, 34, 3, .35f);
            if (app.run.power == 0) Collect("grow");
            Collect("shield");
        }

        void DrawHelp()
        {
            if (!helpOn || helpView == null) return;
            float bob = Mathf.Sin(simT * 3) * .1f;
            helpView.transform.position = helpAt + new Vector2(0, bob);
            helpIcon.transform.position = helpAt + new Vector2(0, .85f + bob * 1.4f);
        }

        // ---- boss entrances: the camera turns to the boss, it leaps and lands, its name fills the screen ----

        public const float BossIntroTime = 2.6f;
        public bool BossIntroOn => phase == Phase.BossIntro;
        public int BossIntros { get; private set; }
        bool introLanded;

        void StartBossIntro()
        {
            boss.awake = true; phase = Phase.BossIntro; phaseT = 0; introLanded = false; BossIntros++;
            boss.dir = hero.x < boss.x ? -1 : 1;
            hero.vx = 0;
            TouchUi.Current?.Teach(null);
            app.hud.Hint(0, "");
            Sound.Music(Def.bossMusic ?? "m_boss");
            Sound.Play("bump", 1, .45f);        // a deep rumble before it shows itself
        }

        void StepBossIntro(float dt, Pad input)
        {
            phaseT += dt;
            World.Step(grid, ref world, hero, dt, default);     // the hero lands and stands; moving ledges keep moving
            float mid = Mathf.Lerp(hero.x, boss.x, .6f);
            float want = phaseT < BossIntroTime - .55f ? mid : hero.x;
            SetCamBounds();
            camX = Mathf.Clamp(camX + (want - camX) * (1 - Mathf.Exp(-4f * dt)), camMin, camMax);
            if (!introLanded && phaseT >= 1.15f)
            {
                introLanded = true; shake = .45f;
                Sound.Play("brick"); Sound.Play("bump", 1, .6f); Haptics.Buzz(90, .8f);
            }
            float alpha = Mathf.Clamp01((phaseT - .7f) / .3f) * Mathf.Clamp01((BossIntroTime - phaseT) / .4f);
            app.hud.BossTitle(Rtl.V(Def.name), Rtl.V(Def.bossTitle ?? ""), alpha);
            if (phaseT >= BossIntroTime || phaseT > .8f && input.jumpPressed) EndBossIntro();
        }

        void EndBossIntro()
        {
            phase = Phase.Play; phaseT = 0;
            app.hud.BossTitle(null, null, 0);
            app.hud.BossBar(Mathf.Max(0, boss.hp) / (float)Boss.MaxHp, Rtl.V(Def.name));
        }

        /// The leap is only drawn: the boss's own state stays where the fight will start.
        void DrawBossIntro()
        {
            if (boss == null) return;
            float t = phaseT;
            float hop = t > .5f && t < 1.15f ? Mathf.Sin((t - .5f) / .65f * Mathf.PI) * 2.6f : 0;
            float land = t >= 1.15f && t < 1.45f ? 1 - (t - 1.15f) / .3f : 0;
            boss.view.transform.position = new Vector3(boss.x, boss.y + hop, 0);
            boss.view.transform.localScale = new Vector3(1 + .18f * land, 1 - .2f * land, 1);
            boss.view.sprite = Art.Feet((boss.frost ? "giant" : boss.color >= 0 ? "king_" + Grid2.ColorNames[boss.color] : "boss") + (hop > 0 ? "_rest" : "_walk_a"));
        }
    }
}
