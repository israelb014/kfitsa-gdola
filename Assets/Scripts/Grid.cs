using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Kfitsa
{
    public enum BlockKind { Coin, Grow, Life, Brick, HiddenCoin, HiddenLife, Shield }

    /// The level as data: a cell grid for collision plus everything the level text places.
    /// Row 0 is the bottom. The text file is written top row first.
    public class Grid2
    {
        public int W, H;
        public int MainW;            // columns the camera may show in the main level
        public int RoomStart = -1;   // first column of the bonus room, if there is one
        public Vector2 Start, Door, RoomDoor, Checkpoint, BossSpot;
        public bool HasDoor, HasRoomDoor, HasCheckpoint, HasBoss;
        public float GateX, GateY;
        public int Sw;               // state of the switch blocks; World keeps it in step
        public string Idea = "", RoomMode = "";
        public float RoomSeconds, DarkRadius, CheckpointWater = -1;
        public int RoomH = 15;
        public WindZone[] Winds = new WindZone[0];
        public Storm Storm;
        public readonly List<Shelter> Shelters = new List<Shelter>();
        public readonly List<Vector2> EasyFlags = new List<Vector2>();   // extra checkpoints on easy
        public readonly List<string> Needs = new List<string>();     // what the level's idea is: without it the finish must stay out of reach
        public readonly List<(int c, int r)> Palms = new List<(int, int)>(), Flags = new List<(int, int)>();
        public Tide Tide; public Flood Flood; public Chase Chase; public RoomWater RoomPool;
        public readonly List<Vector2> Stars = new List<Vector2>(), RoomLights = new List<Vector2>();
        public readonly List<(int c, int r)> Switches = new List<(int, int)>();
        public readonly List<Vector2> Coins = new List<Vector2>();
        public readonly List<(Vector2 at, char kind)> Enemies = new List<(Vector2, char)>();
        public readonly List<(int c, int r, BlockKind kind)> Blocks = new List<(int, int, BlockKind)>();
        public readonly List<(int c, int r)> Springs = new List<(int, int)>(), Torches = new List<(int, int)>();
        public bool Tall => H > 15;
        public readonly List<Platform> Platforms = new List<Platform>();
        char[,] cells;

        public static Grid2 Parse(string text)
        {
            var all = text.Replace("\r", "").TrimEnd('\n').Split('\n');
            var rows = new List<string>(); var notes = new List<string>();
            foreach (var line in all) (line.StartsWith("@") ? notes : rows).Add(line);
            var g = new Grid2 { H = rows.Count };
            foreach (var row in rows) g.W = Mathf.Max(g.W, row.Length);
            g.cells = new char[g.W, g.H];
            g.MainW = g.W;
            for (int r = 0; r < g.H; r++)
                for (int c = 0; c < g.W; c++)
                {
                    string line = rows[g.H - 1 - r];
                    char ch = c < line.Length ? line[c] : ' ';
                    var cell = new Vector2(c + .5f, r);
                    switch (ch)
                    {
                        case 'S': g.Start = cell; ch = ' '; break;
                        case 'G': g.GateX = c + .5f; g.GateY = r; ch = ' '; break;
                        case 'C': g.Checkpoint = cell; g.HasCheckpoint = true; ch = ' '; break;
                        case 'D': g.Door = cell; g.HasDoor = true; ch = ' '; break;
                        case 'E': g.RoomDoor = cell; g.HasRoomDoor = true; ch = ' '; break;
                        case 'R': g.RoomStart = c; g.MainW = c; ch = 'g'; break;
                        case 'B': g.BossSpot = cell; g.HasBoss = true; ch = ' '; break;
                        case 'o': g.Coins.Add(new Vector2(c + .5f, r + .5f)); ch = ' '; break;
                        case 'e': case 'n': case 'y': case 'j': case 'p': case 'q': g.Enemies.Add((cell, ch)); ch = ' '; break;
                        case '?': g.Blocks.Add((c, r, BlockKind.Coin)); break;
                        case '!': g.Blocks.Add((c, r, BlockKind.Grow)); break;
                        case '*': g.Blocks.Add((c, r, BlockKind.Life)); break;
                        case '+': g.Blocks.Add((c, r, BlockKind.Shield)); break;
                        case 'b': g.Blocks.Add((c, r, BlockKind.Brick)); break;
                        case 'h': g.Blocks.Add((c, r, BlockKind.HiddenCoin)); break;
                        case 'H': g.Blocks.Add((c, r, BlockKind.HiddenLife)); break;
                        case 'T': g.Springs.Add((c, r)); ch = ' '; break;
                        case '|': g.Torches.Add((c, r)); ch = ' '; break;
                        case 'M': g.Platforms.Add(new Platform(c + .5f, r + 1, true)); ch = ' '; break;
                        case 'V': g.Platforms.Add(new Platform(c + .5f, r + 1, false)); ch = ' '; break;
                        case 's': g.Stars.Add(new Vector2(c + .5f, r + .5f)); ch = ' '; break;
                        case 'L': g.RoomLights.Add(new Vector2(c + .5f, r + .5f)); ch = ' '; break;
                        case '1': g.Switches.Add((c, r)); ch = ' '; break;
                        case 'P': g.Palms.Add((c, r)); ch = ' '; break;
                        case 'F': g.Flags.Add((c, r)); ch = ' '; break;
                    }
                    g.cells[c, r] = ch;
                }
            foreach (var n in notes) g.Note(n.Substring(1).Split(' '));
            return g;
        }

        /// One "@" line of a level file: everything a level does beyond its tiles.
        void Note(string[] a)
        {
            float F(int i) => float.Parse(a[i], System.Globalization.CultureInfo.InvariantCulture);
            switch (a[0])
            {
                case "idea": Idea = a[1]; break;
                case "dark": DarkRadius = F(1); break;
                case "room": RoomMode = a[1]; RoomSeconds = F(2); break;
                case "roomh": RoomH = (int)F(1); break;
                case "plat": Platforms.Add(new Platform(F(1), F(2), F(3), F(4), F(5), F(6), F(7))); break;
                case "wind":
                    Winds = new List<WindZone>(Winds) { new WindZone { x0 = F(1), x1 = F(2), y0 = F(3), y1 = F(4), push = F(5), sw = (int)F(6) } }.ToArray(); break;
                case "storm": Storm = new Storm { calm = F(1), warn = F(2), blow = F(3) }; break;
                case "gust":
                    Winds = new List<WindZone>(Winds) { new WindZone { x0 = F(1), x1 = F(2), y0 = F(3), y1 = F(4), push = F(5), lift = a.Length > 6 ? F(6) : 0, storm = Storm } }.ToArray(); break;
                case "needs": Needs.Add(a[1]); break;
                case "easyflag": EasyFlags.Add(new Vector2(F(1) + .5f, F(2))); break;
                case "shelter": Shelters.Add(new Shelter { x0 = F(1), x1 = F(2), y0 = F(3), y1 = F(4) }); break;
                case "tide": Tide = new Tide { low = F(1), high = F(2), period = F(3), x0 = F(4), x1 = F(5) }; break;
                case "flood": Flood = new Flood { trigger = F(1), y0 = F(2), speed = F(3), top = F(4) }; break;
                case "chase": Chase = new Chase { trigger = F(1), x0 = F(2), speed = F(3), end = F(4) }; break;
                case "pool": RoomPool = new RoomWater { x0 = F(1), x1 = F(2), high = F(3), low = F(4) }; break;
                default: throw new System.Exception("Unknown level note: " + a[0]);
            }
        }

        /// The same level with the storm taken away, to prove nothing depends on it.
        public void Calm()
        {
            Storm = null;
            Winds = System.Array.FindAll(Winds, w => w.storm == null);
        }

        /// Checkpoints in the order the player meets them.
        public List<Vector2> FlagSpots(bool easy)
        {
            var all = new List<Vector2>();
            if (HasCheckpoint) all.Add(Checkpoint);
            if (easy) all.AddRange(EasyFlags);
            all.Sort((a, b) => MainW < 60 ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            return all;
        }

        /// The same level with one mechanic taken out, to prove that it cannot be skipped.
        public void Without(string what)
        {
            if (what == "springs") Springs.Clear();
            else if (what == "platforms") Platforms.Clear();
            else if (what == "ladders") { for (int c = 0; c < W; c++) for (int r = 0; r < H; r++) if (Climbable(c, r)) cells[c, r] = ' '; }
            else if (what == "crumble") { for (int c = 0; c < W; c++) for (int r = 0; r < H; r++) if (cells[c, r] == 'u') cells[c, r] = ' '; }
            else throw new System.Exception("Unknown mechanic: " + what);
        }

        /// A flat test floor, used by the feel measurements.
        public static Grid2 Flat(int w, int floor)
        {
            var g = new Grid2 { W = w, H = 40, MainW = w, cells = new char[w, 40] };   // no notes, no mechanics
            for (int c = 0; c < w; c++) for (int r = 0; r < 40; r++) g.cells[c, r] = r < floor ? '#' : ' ';
            return g;
        }

        public bool AtGate(Body h) => h.grounded && h.x >= GateX - .1f && h.x < MainW && h.y >= GateY - .5f;

        public void Clear(int c, int r) => cells[c, r] = ' ';
        public void Reveal(int c, int r) => cells[c, r] = '?';
        public void Restore(int c, int r, char ch) => cells[c, r] = ch;

        public char At(int c, int r) => c < 0 || c >= W || r < 0 || r >= H ? ' ' : cells[c, r];

        public bool Solid(int c, int r)
        {
            if (c < 0 || c >= W) return true;   // side walls; open above and below
            if (r < 0 || r >= H) return false;
            switch (cells[c, r])
            {
                case '#': case 'g': case 'X': case '?': case '!': case '*': case '+': case 'b': case '{': case '}': case 'u': return true;
                case 'a': return (Sw & 1) == 0;
                case 'A': return (Sw & 1) != 0;
                default: return false;
            }
        }

        /// Hidden blocks only exist for a head coming up from below.
        public bool SolidFromBelow(int c, int r)
        {
            char ch = At(c, r);
            return Solid(c, r) || ch == 'h' || ch == 'H';
        }

        /// Ladders and ropes: the hero can hold on anywhere in them.
        public bool Climbable(int c, int r) { char ch = At(c, r); return ch == 'I' || ch == 't'; }

        /// The top of a ladder is a ledge to stand on, and to climb down through. Ropes have none.
        public bool LadderTop(int c, int r) => At(c, r) == 'I' && !Climbable(c, r + 1) && !Solid(c, r + 1);

        public bool Hazard(int c, int r)
        {
            char ch = At(c, r);
            return ch == '^' || ch == 'l';
        }

        public bool Deadly(int c, int r) => At(c, r) == 'l' || At(c, r) == 'w';

        bool Earth(int c, int r, char kind)
        {
            if (c < 0 || c >= W) return true;
            if (r < 0) return true;
            return r < H && cells[c, r] == kind;
        }

        string GroundTile(int c, int r, char kind)
        {
            bool u = Earth(c, r + 1, kind), d = Earth(c, r - 1, kind), l = Earth(c - 1, r, kind), rt = Earth(c + 1, r, kind);
            if (!l && !rt) return !u && !d ? "block" : !u ? "vertical_top" : !d ? "vertical_bottom" : "vertical_middle";
            if (!u && !d) return !l ? "horizontal_left" : !rt ? "horizontal_right" : "horizontal_middle";
            string side = !l ? "_left" : !rt ? "_right" : "";
            if (!u) return "block_top" + side;
            if (!d) return "block_bottom" + side;
            return side == "" ? "block_center" : "block" + side;
        }

        string Decor(char ch, int c, int r)
        {
            switch (ch)
            {
                case ',': return "grass";
                case '%': return "bush";
                case 'r': return "rock";
                case 'k': return "cactus";
                case 'f': return "fence";
                case '>': return "sign_right";
                case '^': return "spikes";
                case 'l': return At(c, r + 1) == 'l' ? "lava" : "lava_top";
                case 'w': return At(c, r + 1) == 'w' ? "water" : "water_top";
                case 'I': return At(c, r + 1) != 'I' ? "ladder_top" : At(c, r - 1) != 'I' ? "ladder_bottom" : "ladder_middle";
                case 't': return "rope";
                default: return null;
            }
        }

        public Tilemap FalseWalls;
        readonly Dictionary<string, Tile> tiles = new Dictionary<string, Tile>();
        Tile T(string name)
        {
            if (tiles.TryGetValue(name, out var t)) return t;
            t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = Art.Tile(name);
            return tiles[name] = t;
        }

        public void BuildTilemaps(Transform parent, string terrain)
        {
            var go = new GameObject("Grid", typeof(Grid));
            go.transform.SetParent(parent, false);
            var solid = Layer(go.transform, "Solid", 0);
            var decor = Layer(go.transform, "Decor", -5);
            var fake = Layer(go.transform, "False wall", 12);
            FalseWalls = fake;
            for (int r = 0; r < H; r++)
                for (int c = 0; c < W; c++)
                {
                    char ch = cells[c, r];
                    var at = new Vector3Int(c, r, 0);
                    if (ch == '#') solid.SetTile(at, T($"t_{terrain}_{GroundTile(c, r, '#')}"));
                    else if (ch == 'g') solid.SetTile(at, T($"t_stone_{GroundTile(c, r, 'g')}"));
                    else if (ch == 'X') solid.SetTile(at, T("block_planks"));
                    else if (ch == 'Q') fake.SetTile(at, T($"t_{terrain}_{GroundTile(c, r, 'Q')}"));
                    else
                    {
                        var d = Decor(ch, c, r);
                        if (d != null) decor.SetTile(at, T(d));
                    }
                }
        }

        static Tilemap Layer(Transform parent, string name, int order)
        {
            var o = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            o.transform.SetParent(parent, false);
            var map = o.GetComponent<Tilemap>();
            map.tileAnchor = Vector3.zero;
            var rend = o.GetComponent<TilemapRenderer>();
            rend.sharedMaterial = Art.Unlit; rend.sortingOrder = order;
            return map;
        }
    }
}
