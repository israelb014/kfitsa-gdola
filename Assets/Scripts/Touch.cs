using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Kfitsa
{
    public enum RunMode { Hold, Auto, Toggle }

    /// On-screen buttons. Each finger is read every frame and goes to the nearest button on its half of
    /// the screen, so a thumb can slide between buttons and never drops off into a dead zone.
    /// In hold mode a finger between jump and run presses both, like rolling a thumb across two pad buttons.
    public class TouchUi
    {
        public static TouchUi Current;

        /// True only when the system reports a touch screen.
        public static bool Available => Touchscreen.current != null;

        public static readonly (string name, Color color)[] Themes =
        {
            ("ירוק", new Color(.25f, .75f, .42f)), ("כחול", new Color(.24f, .55f, .95f)), ("אדום", new Color(.92f, .3f, .3f)),
            ("צהוב", new Color(.98f, .78f, .2f)), ("סגול", new Color(.65f, .4f, .92f)), ("לבן", new Color(.93f, .95f, 1f)),
        };

        const float Size = 190, BothStrip = 34;
        static readonly Color Ink = new Color(.1f, .12f, .2f), Lit = new Color(1, .84f, .2f);

        class Button
        {
            public RectTransform rect; public Image shadow, face, ring; public Graphic mark; public bool held, lit; public float size;
            public Vector2 Center => rect.position;
            public void Paint(Color theme, float alpha)
            {
                var c = lit ? Lit : theme;
                float a = held ? Mathf.Min(1, alpha + .15f) : alpha;
                face.color = new Color(c.r * (held ? 1.15f : 1), c.g * (held ? 1.15f : 1), c.b * (held ? 1.15f : 1), a);
                ring.color = lit ? Color.white : new Color(Ink.r, Ink.g, Ink.b, Mathf.Min(1, a + .15f));
                shadow.color = new Color(0, 0, 0, .45f * Mathf.Min(1, a + .2f));
                var m = mark.color; m.a = Mathf.Min(1, a + .25f); mark.color = m;
                rect.localScale = Vector3.one * (held ? 1.07f : 1);
            }
        }

        readonly GameObject root;
        readonly Button left, right, jump, run, toss, door, climb, pause;
        readonly Button[] all;
        readonly HashSet<int> ignored = new HashSet<int>(), live = new HashSet<int>();
        readonly System.Func<float> scale;
        Save save;
        int polled = -1;
        bool jumpWas, runWas, tossWas, pausePressed, doorShown, throwShown, climbShown, preview, locked;
        Vector2? tap, finger;
        Color theme = Themes[0].color; float alpha = .8f; bool flip;
        readonly Image hand; Button taught; float handT;

        public RunMode Mode => save == null ? RunMode.Hold : (RunMode)save.runMode;
        public bool RunLocked => locked;

        public TouchUi(Hud hud, Transform parent, System.Func<float> canvasScale)
        {
            Current = this; scale = canvasScale;
            EnhancedTouchSupport.Enable();   // keeps a tap that begins and ends between two frames
            root = new GameObject("Touch", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var r = (RectTransform)root.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;

            var disc = Disc(); var circle = Ring(); var soft = Shadow(); var arrow = Arrow();
            left = Make(arrow, 90, null); right = Make(arrow, -90, null);
            run = Make(null, 0, "ריצה"); jump = Make(null, 0, "קפיצה"); toss = Make(null, 0, "זריקה");
            door = Make(arrow, 180, null); climb = Make(arrow, 0, null); pause = Make(null, 0, "II");
            all = new[] { left, right, run, jump, toss, door, climb, pause };
            door.rect.gameObject.SetActive(false); toss.rect.gameObject.SetActive(false); climb.rect.gameObject.SetActive(false);

            // The teaching hand: its fingertip rests on the button it is pointing at
            var h = new GameObject("Hand", typeof(Image)); h.transform.SetParent(root.transform, false);
            hand = h.GetComponent<Image>(); hand.sprite = Art.Center("hand"); hand.raycastTarget = false;
            hand.rectTransform.pivot = new Vector2(.48f, .94f); hand.rectTransform.sizeDelta = new Vector2(130, 130);
            hand.enabled = false;

            Button Make(Sprite icon, float turn, string label)
            {
                var o = new GameObject("Button", typeof(RectTransform));
                o.transform.SetParent(root.transform, false);
                var b = new Button { rect = (RectTransform)o.transform };
                b.shadow = Layer("Shadow", soft, new Vector2(1.22f, 1.22f), new Vector2(0, -.04f));
                b.face = Layer("Face", disc, Vector2.one, Vector2.zero);
                b.ring = Layer("Ring", circle, Vector2.one, Vector2.zero);
                if (icon != null)
                {
                    var i = Layer("Mark", icon, new Vector2(.45f, .45f), Vector2.zero);
                    i.rectTransform.localEulerAngles = new Vector3(0, 0, turn); i.color = Color.white;
                    b.mark = i;
                }
                else
                {
                    var t = hud.Label(44, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Vector2.zero, new Vector2(Size, Size), o.transform);
                    t.text = Rtl.V(label); b.mark = t;
                }
                return b;

                Image Layer(string name, Sprite s, Vector2 size, Vector2 shift)
                {
                    var m = new GameObject(name, typeof(Image)); m.transform.SetParent(o.transform, false);
                    var i = m.GetComponent<Image>(); i.sprite = s; i.raycastTarget = false;
                    var t = i.rectTransform; t.anchorMin = (Vector2.one - size) / 2 + shift; t.anchorMax = (Vector2.one + size) / 2 + shift; t.offsetMin = t.offsetMax = Vector2.zero;
                    return i;
                }
            }
            root.SetActive(false);
        }

        /// Reads the saved look and position; called again on every change, for the live preview.
        public void Configure(Save s)
        {
            save = s;
            alpha = s.buttonAlpha; flip = s.leftHanded;
            theme = Themes[Mathf.Clamp(s.buttonColor, 0, Themes.Length - 1)].color;
            float k = s.buttonSize, R = Size / 2 * k, J = (Size + 20) / 2 * k;
            float gap = 30, move = flip ? 1 : 0, act = 1 - move, sm = flip ? -1 : 1, sa = -sm;
            Place(flip ? right : left, move, sm * (gap + R), gap + R, R * 2);
            Place(flip ? left : right, move, sm * (gap + R * 3 + 40 * k), gap + R, R * 2);
            Place(jump, act, sa * (gap + J), 90 * k + J, J * 2);
            Place(run, act, sa * (gap + J * 2 + 30 * k + R), gap + R, R * 2);
            Place(toss, act, sa * (gap + J * 2 + 30 * k + R), gap + R * 3 + 30 * k, R * 2);
            Place(door, move, sm * (gap + R * 2 + 20 * k), gap + R * 2 + 85 * k + 75 * k, 150 * k);
            Place(climb, move, sm * (gap + R * 2 + 20 * k), gap + R * 2 + 85 * k + 75 * k + 165 * k, 150 * k);
            pause.rect.anchorMin = pause.rect.anchorMax = new Vector2(0, 1); pause.rect.anchoredPosition = new Vector2(75, -70); Resize(pause, 100);
            if (Mode != RunMode.Toggle) locked = false;
            RefreshRun();
            PaintAll();

            void Place(Button b, float side, float x, float y, float size)
            {
                b.rect.anchorMin = b.rect.anchorMax = new Vector2(side, 0);
                b.rect.anchoredPosition = new Vector2(x, y);
                Resize(b, size);
            }
            void Resize(Button b, float size)
            {
                b.rect.sizeDelta = new Vector2(size, size); b.size = size;
                if (b.mark is Text t) { t.fontSize = Mathf.RoundToInt(Mathf.Clamp(size * .23f, 30, 60)); t.rectTransform.sizeDelta = new Vector2(size, size); }
            }
        }

        void RefreshRun()
        {
            run.rect.gameObject.SetActive(Mode != RunMode.Auto);
            run.lit = Mode == RunMode.Toggle && (locked || preview);
            ((Text)run.mark).text = Rtl.V(run.lit ? "ריצה\nנעולה" : "ריצה");
            toss.rect.gameObject.SetActive(throwShown || preview);
        }

        void PaintAll() { foreach (var b in all) b.Paint(theme, alpha); }

        public bool Visible => root.activeSelf && !preview;
        public bool DoorShown => doorShown;
        public bool ClimbShown => climbShown;
        public bool ThrowShown => throwShown;
        public bool RunShown => run.rect.gameObject.activeSelf;
        public float ButtonWidth(string name) => name == "jump" ? jump.size : left.size;
        public Color FaceColor(string name) => (name == "jump" ? jump : run).face.color;

        /// Where a button sits on screen, for the touch test.
        public Vector2 Spot(string name) => name switch
        {
            "left" => left.Center, "right" => right.Center, "jump" => jump.Center, "run" => run.Center, "door" => door.Center, "up" => climb.Center, "throw" => toss.Center, _ => pause.Center,
        };

        /// Points at one button until it has been used: left, right, jump, run, throw or door. Null hides the hand.
        public void Teach(string name)
        {
            var b = name switch { "left" => left, "right" => right, "jump" => jump, "run" => run, "throw" => toss, "door" => door, "up" => climb, _ => null };
            if (b != taught) handT = 0;
            taught = b;
            DrawHand();
        }

        public string Teaching => taught == null ? null : taught == left ? "left" : taught == right ? "right" : taught == jump ? "jump" : taught == run ? "run" : taught == toss ? "throw" : taught == climb ? "up" : "door";
        public bool HandShown => hand.enabled;
        public Vector2 HandTip => hand.rectTransform.position;

        void DrawHand()
        {
            bool show = taught != null && taught.rect.gameObject.activeInHierarchy && Visible;
            hand.enabled = show;
            if (!show) return;
            handT += Time.unscaledDeltaTime;
            // A tap, over and over: the hand dips onto the button, then lifts off
            float tap = Mathf.Abs(Mathf.Sin(handT * 3.2f));
            float px = scale();
            hand.rectTransform.position = taught.Center + new Vector2(18, -10 - 26 * tap) * px;
            hand.rectTransform.localScale = Vector3.one * (taught.size / 190f) * (1 - .08f * (1 - tap));
            hand.color = new Color(1, 1, 1, Mathf.Clamp01(handT * 3));
            hand.transform.SetAsLastSibling();
        }

        public void SetVisible(bool on) { if (!preview && root.activeSelf != on) root.SetActive(on); }

        /// Shows the buttons behind the settings menu, without reacting to fingers.
        public void Preview(bool on)
        {
            preview = on; root.SetActive(on);
            RefreshRun(); PaintAll();
        }

        public void ShowDoor(bool on)
        {
            if (doorShown == on) return;
            doorShown = on; door.rect.gameObject.SetActive(on);
        }

        /// The up arrow, for ladders and ropes.
        public void ShowClimb(bool on)
        {
            if (climbShown == on) return;
            climbShown = on; climb.rect.gameObject.SetActive(on);
        }

        public void ShowThrow(bool on)
        {
            if (throwShown == on) return;
            throwShown = on; RefreshRun();
        }

        /// A finger that came down this frame, for menus.
        public Vector2? Tap { get { Poll(); return tap; } }
        /// Any finger still on the glass, for dragging a slider.
        public Vector2? Finger { get { Poll(); return finger; } }
        public bool PausePressed { get { Poll(); return pausePressed; } }

        public void Merge(ref Pad p)
        {
            Poll();
            if (!Visible) return;
            if (left.held) p.x -= 1;
            if (right.held) p.x += 1;
            p.down |= door.held;
            p.up |= climb.held;
            var mode = Mode;
            if (mode == RunMode.Hold)
            {
                p.run |= run.held;
                p.runPressed |= run.held && !runWas;
            }
            else p.run |= mode == RunMode.Auto || locked;
            p.throwPressed |= toss.held && !tossWas;
            p.jumpHeld |= jump.held;
            p.jumpPressed |= jump.held && !jumpWas;
        }

        void Poll()
        {
            if (polled == Time.frameCount) return;
            polled = Time.frameCount;
            jumpWas = jump.held; runWas = run.held; tossWas = toss.held;
            tap = null; finger = null; pausePressed = false;
            bool l = false, r = false, j = false, u = false, d = false, w = false, c = false;
            live.Clear();
            float px = scale();
            var mode = Mode;
            bool canRun = mode != RunMode.Auto, canThrow = throwShown;
            foreach (var t in ETouch.activeTouches)
            {
                var phase = t.phase;
                if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled) continue;
                bool began = phase == UnityEngine.InputSystem.TouchPhase.Began;
                int id = t.touchId;
                Vector2 at = t.screenPosition;
                live.Add(id);
                finger ??= at;
                if (began)
                {
                    tap ??= at;
                    GameInput.TouchUsed();
                    if (!Visible) ignored.Add(id);   // a menu tap must not land on a button that appears under it
                    else if (Vector2.Distance(at, pause.Center) < 80 * px) { pausePressed = true; ignored.Add(id); }
                }
                if (ignored.Contains(id)) continue;
                bool moveSide = at.x < UnityEngine.Screen.width / 2f != flip;
                if (moveSide)
                {
                    if (climbShown && Vector2.Distance(at, climb.Center) < climb.size * .6f) c = true;
                    else if (doorShown && Vector2.Distance(at, door.Center) < door.size * .63f) d = true;
                    else if ((at - left.Center).sqrMagnitude <= (at - right.Center).sqrMagnitude) l = true;
                    else r = true;
                }
                else
                {
                    float toJump = Vector2.Distance(at, jump.Center);
                    float toRun = canRun ? Vector2.Distance(at, run.Center) : float.MaxValue;
                    float toToss = canThrow ? Vector2.Distance(at, toss.Center) : float.MaxValue;
                    if (toToss < toJump && toToss < toRun) w = true;
                    else if (mode == RunMode.Hold && Mathf.Abs(toJump - toRun) < BothStrip * px) { j = true; u = true; }
                    else if (toJump < toRun) j = true;
                    else u = true;
                }
            }
            ignored.IntersectWith(live);
            if (mode == RunMode.Toggle && u && !run.held) { locked = !locked; Sound.Play("select", .5f); RefreshRun(); }
            bool changed = false;
            Set(left, l); Set(right, r); Set(jump, j); Set(run, u); Set(door, d); Set(toss, w); Set(climb, c);
            if (changed) PaintAll();
            DrawHand();

            void Set(Button b, bool on) { if (b.held != on) { b.held = on; changed = true; } }
        }

        // Generated at runtime, so the buttons stay sharp and need no art.
        static Sprite Round(System.Func<float, float> alphaAt)
        {
            const int n = 256; var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = new Color(1, 1, 1, alphaAt(Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(n / 2f, n / 2f)) / (n / 2f)));
            t.SetPixels32(px); t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
        }

        public static Sprite RoundDisc => Disc();
        static Sprite Disc() => Round(d => Mathf.Clamp01((1 - d) * 128));
        static Sprite Ring() => Round(d => Mathf.Clamp01((1 - d) * 128) * Mathf.Clamp01((d - .91f) * 128));
        static Sprite Shadow() => Round(d => Mathf.Pow(Mathf.Clamp01((1 - d) / .28f), 1.5f));

        /// Points up; the buttons rotate it.
        static Sprite Arrow()
        {
            const int n = 128; var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float half = (n - 8 - y) * .5f;
                    float a = y < 8 ? 0 : Mathf.Clamp01(half - Mathf.Abs(x + .5f - n / 2f) + 1) * Mathf.Clamp01(y - 7);
                    px[y * n + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels32(px); t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
        }
    }
}
