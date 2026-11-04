using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Kfitsa
{
    /// The level as data: a solid-cell grid for collision, plus everything the level text places.
    /// Row 0 is the bottom. The text file is written top row first.
    public class Grid2
    {
        public int W, H;
        public Vector2 Start;
        public float GateX;
        public readonly List<Vector2> Coins = new List<Vector2>(), Enemies = new List<Vector2>();
        public readonly List<(int c, int r, bool power)> Surprises = new List<(int, int, bool)>();
        char[,] cells;

        public static Grid2 Parse(string text)
        {
            var rows = text.Replace("\r", "").TrimEnd('\n').Split('\n');
            var g = new Grid2 { H = rows.Length };
            foreach (var row in rows) g.W = Mathf.Max(g.W, row.Length);
            g.cells = new char[g.W, g.H];
            for (int r = 0; r < g.H; r++)
                for (int c = 0; c < g.W; c++)
                {
                    string line = rows[g.H - 1 - r];
                    char ch = c < line.Length ? line[c] : ' ';
                    switch (ch)
                    {
                        case 'S': g.Start = new Vector2(c + .5f, r); ch = ' '; break;
                        case 'G': g.GateX = c + .5f; ch = ' '; break;
                        case 'o': g.Coins.Add(new Vector2(c + .5f, r + .5f)); ch = ' '; break;
                        case 'e': g.Enemies.Add(new Vector2(c + .5f, r)); ch = ' '; break;
                        case '?': g.Surprises.Add((c, r, false)); break;
                        case '!': g.Surprises.Add((c, r, true)); break;
                    }
                    g.cells[c, r] = ch;
                }
            return g;
        }

        /// A flat test floor, used by the feel measurements.
        public static Grid2 Flat(int w, int floor)
        {
            var g = new Grid2 { W = w, H = 40, cells = new char[w, 40] };
            for (int c = 0; c < w; c++) for (int r = 0; r < 40; r++) g.cells[c, r] = r < floor ? '#' : ' ';
            return g;
        }

        public void Clear(int c, int r) => cells[c, r] = ' ';

        public bool Solid(int c, int r)
        {
            if (c < 0 || c >= W) return true;   // side walls; open above and below
            if (r < 0 || r >= H) return false;
            char ch = cells[c, r];
            return ch == '#' || ch == 'X' || ch == '?' || ch == '!';
        }

        bool Earth(int c, int r)
        {
            if (c < 0 || c >= W || r >= H) return false;
            return r < 0 || cells[c, r] == '#';
        }

        /// Picks the Kenney ground tile from the eight neighbours.
        int GroundTile(int c, int r)
        {
            bool up = Earth(c, r + 1), dn = Earth(c, r - 1), lf = c == 0 || Earth(c - 1, r), rt = c == W - 1 || Earth(c + 1, r);
            int col = lf ? (rt ? 2 : 3) : (rt ? 1 : 0);
            if (!up) return (dn ? 20 : 0) + col;
            if (!dn) return 140 + col;
            if (lf && rt)
            {
                bool Edge(int x) => x >= 0 && x < W;
                if (Edge(c - 1) && !Earth(c - 1, r + 1)) return 25;
                if (Edge(c + 1) && !Earth(c + 1, r + 1)) return 24;
                if (Edge(c - 1) && !Earth(c - 1, r - 1)) return 5;
                if (Edge(c + 1) && !Earth(c + 1, r - 1)) return 4;
            }
            return 120 + col;
        }

        static int Decor(char ch)
        {
            switch (ch)
            {
                case 'a': return 124;  // sprout
                case 'b': return 125;  // tall sprout
                case 't': return 126;  // pine
                case 'k': return 127;  // cactus
                case 'f': return 106;  // fence
                case '>': return 85;   // arrow sign
                default: return -1;
            }
        }

        readonly Dictionary<int, Tile> tileCache = new Dictionary<int, Tile>();
        Tile T(int i)
        {
            if (tileCache.TryGetValue(i, out var t)) return t;
            t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = Art.Tile(i);
            return tileCache[i] = t;
        }

        public void BuildTilemaps(Transform parent)
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
                    if (ch == '#') solid.SetTile(at, T(GroundTile(c, r)));
                    else if (ch == 'X') solid.SetTile(at, T(Art.Crate));
                    else if (Decor(ch) >= 0) decor.SetTile(at, T(Decor(ch)));
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
