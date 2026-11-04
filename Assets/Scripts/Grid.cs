using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Kfitsa
{
    public enum BlockKind { Coin, Grow, Life, Brick, HiddenCoin, HiddenLife }

    /// The level as data: a cell grid for collision plus everything the level text places.
    /// Row 0 is the bottom. The text file is written top row first.
    public class Grid2
    {
        public int W, H;
        public int MainW;            // columns the camera may show in the main level
        public int RoomStart = -1;   // first column of the bonus room, if there is one
        public Vector2 Start, Door, RoomDoor, Checkpoint, BossSpot;
        public bool HasDoor, HasRoomDoor, HasCheckpoint, HasBoss;
        public float GateX;
        public readonly List<Vector2> Coins = new List<Vector2>();
        public readonly List<(Vector2 at, char kind)> Enemies = new List<(Vector2, char)>();
        public readonly List<(int c, int r, BlockKind kind)> Blocks = new List<(int, int, BlockKind)>();
        public readonly List<(int c, int r)> Springs = new List<(int, int)>(), Torches = new List<(int, int)>();
        public readonly List<Platform> Platforms = new List<Platform>();
        char[,] cells;

        public static Grid2 Parse(string text)
        {
            var rows = text.Replace("\r", "").TrimEnd('\n').Split('\n');
            var g = new Grid2 { H = rows.Length };
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
                        case 'G': g.GateX = c + .5f; ch = ' '; break;
                        case 'C': g.Checkpoint = cell; g.HasCheckpoint = true; ch = ' '; break;
                        case 'D': g.Door = cell; g.HasDoor = true; ch = ' '; break;
                        case 'E': g.RoomDoor = cell; g.HasRoomDoor = true; ch = ' '; break;
                        case 'R': g.RoomStart = c; g.MainW = c; ch = 'g'; break;
                        case 'B': g.BossSpot = cell; g.HasBoss = true; ch = ' '; break;
                        case 'o': g.Coins.Add(new Vector2(c + .5f, r + .5f)); ch = ' '; break;
                        case 'e': case 'n': case 'y': case 'j': case 'p': g.Enemies.Add((cell, ch)); ch = ' '; break;
                        case '?': g.Blocks.Add((c, r, BlockKind.Coin)); break;
                        case '!': g.Blocks.Add((c, r, BlockKind.Grow)); break;
                        case '*': g.Blocks.Add((c, r, BlockKind.Life)); break;
                        case 'b': g.Blocks.Add((c, r, BlockKind.Brick)); break;
                        case 'h': g.Blocks.Add((c, r, BlockKind.HiddenCoin)); break;
                        case 'H': g.Blocks.Add((c, r, BlockKind.HiddenLife)); break;
                        case 'T': g.Springs.Add((c, r)); ch = ' '; break;
                        case '|': g.Torches.Add((c, r)); ch = ' '; break;
                        case 'M': g.Platforms.Add(new Platform(c + .5f, r + 1, true)); ch = ' '; break;
                        case 'V': g.Platforms.Add(new Platform(c + .5f, r + 1, false)); ch = ' '; break;
                    }
                    g.cells[c, r] = ch;
                }
            return g;
        }

        /// A flat test floor, used by the feel measurements.
        public static Grid2 Flat(int w, int floor)
        {
            var g = new Grid2 { W = w, H = 40, MainW = w, cells = new char[w, 40] };
            for (int c = 0; c < w; c++) for (int r = 0; r < 40; r++) g.cells[c, r] = r < floor ? '#' : ' ';
            return g;
        }

        public void Clear(int c, int r) => cells[c, r] = ' ';
        public void Reveal(int c, int r) => cells[c, r] = '?';

        char At(int c, int r) => c < 0 || c >= W || r < 0 || r >= H ? ' ' : cells[c, r];

        public bool Solid(int c, int r)
        {
            if (c < 0 || c >= W) return true;   // side walls; open above and below
            if (r < 0 || r >= H) return false;
            switch (cells[c, r])
            {
                case '#': case 'g': case 'X': case '?': case '!': case '*': case 'b': return true;
                default: return false;
            }
        }

        /// Hidden blocks only exist for a head coming up from below.
        public bool SolidFromBelow(int c, int r)
        {
            char ch = At(c, r);
            return Solid(c, r) || ch == 'h' || ch == 'H';
        }

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
                default: return null;
            }
        }

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
            for (int r = 0; r < H; r++)
                for (int c = 0; c < W; c++)
                {
                    char ch = cells[c, r];
                    var at = new Vector3Int(c, r, 0);
                    if (ch == '#') solid.SetTile(at, T($"t_{terrain}_{GroundTile(c, r, '#')}"));
                    else if (ch == 'g') solid.SetTile(at, T($"t_stone_{GroundTile(c, r, 'g')}"));
                    else if (ch == 'X') solid.SetTile(at, T("block_planks"));
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
