using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Kfitsa
{
    /// On-screen buttons. Each finger is read every frame and goes to the nearest button on its half of
    /// the screen, so a thumb can slide between buttons and never drops off into a dead zone.
    /// Between jump and run a finger presses both, like rolling a thumb across two pad buttons.
    public class TouchUi
    {
        public static TouchUi Current;

        /// True only when the system reports a touch screen.
        public static bool Available => Touchscreen.current != null;

        const float Size = 190, BothStrip = 34, IdleAlpha = .3f, HeldAlpha = .6f;

        class Button
        {
            public RectTransform rect; public Image face; public Graphic mark; public bool held;
            public Vector2 Center => rect.position;
            public void Paint()
            {
                face.color = new Color(1, 1, 1, held ? HeldAlpha : IdleAlpha);
                var c = mark.color; c.a = held ? 1 : .75f; mark.color = c;
            }
        }

        readonly GameObject root;
        readonly Button left, right, jump, run, door, pause;
        readonly HashSet<int> ignored = new HashSet<int>(), live = new HashSet<int>();
        readonly System.Func<float> scale;
        int polled = -1;
        bool jumpWas, runWas, pausePressed, doorShown;
        Vector2? tap;

        public TouchUi(Hud hud, Transform parent, System.Func<float> canvasScale)
        {
            Current = this; scale = canvasScale;
            EnhancedTouchSupport.Enable();   // keeps a tap that begins and ends between two frames
            root = new GameObject("Touch", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var r = (RectTransform)root.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;

            var disc = Disc(); var arrow = Arrow();
            left = Make(hud, new Vector2(0, 0), new Vector2(170, 130), Size, arrow, 90, null);
            right = Make(hud, new Vector2(0, 0), new Vector2(400, 130), Size, arrow, -90, null);
            run = Make(hud, new Vector2(1, 0), new Vector2(-405, 125), Size, null, 0, "ריצה");
            jump = Make(hud, new Vector2(1, 0), new Vector2(-170, 200), Size + 20, null, 0, "קפיצה");
            door = Make(hud, new Vector2(0, 0), new Vector2(285, 335), 150, arrow, 180, null);
            pause = Make(hud, new Vector2(0, 1), new Vector2(75, -70), 100, null, 0, "II");
            door.rect.gameObject.SetActive(false);

            Button Make(Hud h, Vector2 anchor, Vector2 pos, float size, Sprite icon, float turn, string label)
            {
                var o = new GameObject("Button", typeof(Image));
                o.transform.SetParent(root.transform, false);
                var b = new Button { face = o.GetComponent<Image>(), rect = (RectTransform)o.transform };
                b.face.sprite = disc; b.face.raycastTarget = false;
                b.rect.anchorMin = b.rect.anchorMax = anchor; b.rect.anchoredPosition = pos; b.rect.sizeDelta = new Vector2(size, size);
                if (icon != null)
                {
                    var m = new GameObject("Mark", typeof(Image)); m.transform.SetParent(o.transform, false);
                    var i = m.GetComponent<Image>(); i.sprite = icon; i.raycastTarget = false;
                    i.rectTransform.sizeDelta = new Vector2(size, size) * .45f; i.rectTransform.localEulerAngles = new Vector3(0, 0, turn);
                    b.mark = i;
                }
                else
                {
                    var t = h.Label(size > 120 ? 44 : 40, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size), o.transform);
                    t.text = Rtl.V(label); b.mark = t;
                }
                b.Paint();
                return b;
            }
            root.SetActive(false);
        }

        public bool Visible => root.activeSelf;
        public bool DoorShown => doorShown;

        /// Where a button sits on screen, for the touch test.
        public Vector2 Spot(string name) => name switch
        {
            "left" => left.Center, "right" => right.Center, "jump" => jump.Center, "run" => run.Center, "door" => door.Center, _ => pause.Center,
        };

        public void SetVisible(bool on) { if (root.activeSelf != on) root.SetActive(on); }
        public void ShowDoor(bool on)
        {
            if (doorShown == on) return;
            doorShown = on; door.rect.gameObject.SetActive(on);
        }

        /// A finger that came down this frame, for menus.
        public Vector2? Tap { get { Poll(); return tap; } }
        public bool PausePressed { get { Poll(); return pausePressed; } }

        public void Merge(ref Pad p)
        {
            Poll();
            if (left.held) p.x -= 1;
            if (right.held) p.x += 1;
            p.down |= door.held;
            p.run |= run.held;
            p.runPressed |= run.held && !runWas;
            p.jumpHeld |= jump.held;
            p.jumpPressed |= jump.held && !jumpWas;
        }

        void Poll()
        {
            if (polled == Time.frameCount) return;
            polled = Time.frameCount;
            jumpWas = jump.held; runWas = run.held;
            tap = null; pausePressed = false;
            bool l = false, r = false, j = false, u = false, d = false;
            live.Clear();
            float px = scale();
            foreach (var t in ETouch.activeTouches)
            {
                var phase = t.phase;
                if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled) continue;
                bool began = phase == UnityEngine.InputSystem.TouchPhase.Began;
                int id = t.touchId;
                Vector2 at = t.screenPosition;
                live.Add(id);
                if (began)
                {
                    tap ??= at;
                    if (!Visible) ignored.Add(id);   // a menu tap must not land on a button that appears under it
                    else if (Vector2.Distance(at, pause.Center) < 80 * px) { pausePressed = true; ignored.Add(id); }
                }
                if (ignored.Contains(id)) continue;
                if (at.x < UnityEngine.Screen.width / 2f)
                {
                    if (doorShown && Vector2.Distance(at, door.Center) < 95 * px) d = true;
                    else if ((at - left.Center).sqrMagnitude <= (at - right.Center).sqrMagnitude) l = true;
                    else r = true;
                }
                else
                {
                    float toJump = Vector2.Distance(at, jump.Center), toRun = Vector2.Distance(at, run.Center);
                    if (Mathf.Abs(toJump - toRun) < BothStrip * px) { j = true; u = true; }
                    else if (toJump < toRun) j = true;
                    else u = true;
                }
            }
            ignored.IntersectWith(live);
            Set(left, l); Set(right, r); Set(jump, j); Set(run, u); Set(door, d);
        }

        static void Set(Button b, bool on) { if (b.held != on) { b.held = on; b.Paint(); } }

        // Generated at runtime, so the buttons stay sharp and need no art.
        static Sprite Disc()
        {
            const int n = 256; var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(n / 2f, n / 2f));
                    float edge = Mathf.Clamp01(n / 2f - 1 - d);
                    float a = edge * (d > n / 2f - 9 ? 1 : .55f);
                    px[y * n + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels32(px); t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
        }

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
