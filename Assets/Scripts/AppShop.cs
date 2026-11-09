using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kfitsa
{
    /// The coin shop on the map (v2.6): coins become a currency for an extra life, a power at the start of the
    /// next level, and outfits beyond the ones the stars open.
    public partial class App
    {
        // Prices: the bot's straight run through world 1 picks up 32 coins and every coin there is about 110; a normal run, about 60, buys two things
        public const int PriceLife = 25, PriceBig = 15, PriceSevivon = 35, PriceBubble = 20;

        /// Every coin ever found becomes the first money, once, when a save from before the shop is opened.
        void StartWallet()
        {
            if (!save.walletStarted) { save.wallet = Mathf.Max(save.wallet, save.coinsTotal); save.walletStarted = true; save.Write(); }
            Outfit.Owned = save.shopOwned;
        }

        public void StartWalletForTest() => StartWallet();

        public bool Buy(int price)
        {
            if (save.wallet < price) { Sound.Play("bump", .6f); shopNote = "חסרים " + (price - save.wallet) + " מטבעות"; return false; }
            save.wallet -= price; save.Write();
            Sound.Play("coin"); Sound.Play("power", .6f);
            return true;
        }

        public void BuyLife() { if (Buy(PriceLife)) { run.lives++; shopNote = "עוד חיים! עכשיו " + run.lives; } }
        public void BuyBoost(int power, bool shield, int price)
        {
            if (power > 0 && save.boostPower >= power || shield && save.boostShield) { Sound.Play("bump", .5f); shopNote = "כבר מחכה לשלב הבא"; return; }
            if (!Buy(price)) return;
            if (power > 0) save.boostPower = power;
            if (shield) save.boostShield = true;
            save.Write();
            shopNote = "יחכה בתחילת השלב הבא";
        }

        /// A shop outfit: bought once, then worn from here or from the wardrobe.
        public void BuyOutfit(int n)
        {
            var (hat, index) = Outfit.ShopItem(n);
            if (!Outfit.Open(hat, index, 0))
            {
                if (!Buy(Outfit.Price(hat))) return;
                save.shopOwned |= 1 << Outfit.ShopBit(hat, index); Outfit.Owned = save.shopOwned;
                shopNote = "חדש בארון הבגדים!";
            }
            if (hat) { Outfit.Hat = save.hat = index; Achievements.Unlock(Achievements.Id.Hat); } else Outfit.Pant = save.pant = index;
            save.Write();
        }

        /// The power the shop sold, put on at the start of a level (not in a time trial).
        void UseBoosts()
        {
            if (save.boostPower > run.power) run.power = save.boostPower;
            if (save.boostShield) run.shield = true;
            if (save.boostPower > 0 || save.boostShield) { save.boostPower = 0; save.boostShield = false; save.Write(); }
        }

        string shopNote = "";
        int shopPick;
        public int ShopPick => shopPick;
        public string ShopNote => shopNote;

        public void ShowShop(Action back)
        {
            (Image hero, Image hat) look = default;
            Text wallet = null, note = null, waiting = null;
            string OutfitLabel()
            {
                var (hat, index) = Outfit.ShopItem(shopPick);
                string name = hat ? Outfit.HatNames[index] : Outfit.PantsNames[index];
                return "בגד מיוחד:  " + name + "  ·  " + (Outfit.Open(hat, index, 0) ? "נקנה" : Outfit.Price(hat) + " מטבעות");
            }
            void Refresh()
            {
                // The picture tries on the outfit being looked at
                var (hat, index) = Outfit.ShopItem(shopPick);
                int h = Outfit.Hat, p = Outfit.Pant;
                if (hat) Outfit.Hat = index; else Outfit.Pant = index;
                look.hero.sprite = Art.Center(Outfit.HeroSprite('b', "stand"));
                Outfit.Place(look.hat, look.hero, 'b', "stand");
                Outfit.Hat = h; Outfit.Pant = p;
                wallet.text = Rtl.V("מטבעות: " + save.wallet);
                note.text = Rtl.V(shopNote);
                var w = new List<string>();
                if (save.boostPower == 1) w.Add("גדולים"); if (save.boostPower == 2) w.Add("סביבון"); if (save.boostShield) w.Add("בועת מגן");
                waiting.text = Rtl.V(w.Count == 0 ? "" : "מחכה בתחילת השלב הבא: " + string.Join(", ", w));
            }
            void Pick(int d) { shopPick = (shopPick + d + Outfit.ShopCount) % Outfit.ShopCount; shopNote = ""; Refresh(); }
            void Leave() { save.Write(); shopNote = ""; back(); }
            shopNote = "";
            var list = new List<MenuItem>
            {
                new MenuItem { label = () => "חיים נוספים  ·  " + PriceLife + " מטבעות", ok = () => { BuyLife(); Refresh(); } },
                new MenuItem { label = () => "מתחילים את השלב הבא גדולים  ·  " + PriceBig, ok = () => { BuyBoost(1, false, PriceBig); Refresh(); } },
                new MenuItem { label = () => "מתחילים עם סביבון  ·  " + PriceSevivon, ok = () => { BuyBoost(2, false, PriceSevivon); Refresh(); } },
                new MenuItem { label = () => "מתחילים עם בועת מגן  ·  " + PriceBubble, ok = () => { BuyBoost(0, true, PriceBubble); Refresh(); } },
                new MenuItem { label = OutfitLabel, ok = () => { BuyOutfit(shopPick); Refresh(); }, adjust = Pick },
                Item("חזרה", Leave),
            };
            Menu("חנות", list, Leave, 230, .35f, true, 410);
            look = HeroPicture('b', "stand", new Vector2(-700, 20), new Vector2(260, 330));
            var coin = hud.Picture("hud_coin", new Vector2(.5f, .5f), new Vector2(150, 318), new Vector2(54, 54)); screenObjects.Add(coin.gameObject);
            wallet = Line("", 46, 318, new Color(1, .86f, .3f));
            wallet.rectTransform.anchoredPosition = new Vector2(-40, 318);
            note = Line("", 40, -330, new Color(1, .95f, .6f));
            waiting = Line("", 36, -385, new Color(.85f, .9f, 1));
            Line(TouchUi.Available ? "בבגד המיוחד: הקשה בקצוות מחליפה, באמצע קונה ולובשת" : "בבגד המיוחד: חצים ימינה ושמאלה מחליפים, רווח קונה ולובש", 32, -445, new Color(.8f, .85f, .95f));
            Refresh();
        }
    }
}
