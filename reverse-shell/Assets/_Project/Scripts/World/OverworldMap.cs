using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A level drawn as text, one character per 1x1 cell. Row 0 is the north edge (top of the screen).
    ///
    /// Legend
    ///   H  tall ruin (5)          #  ruin (3)           B  mossy brick wall (2)   L  low broken wall (1)
    ///   .  cracked pavement       =  overgrown road     P  grass plaza
    ///   ,  tall grass             *  wildflowers        (both walkable)
    ///   T  tree                   b  bush               r  rubble pile            (all solid)
    ///   S  player spawn           n  NPC                E  encounter enemy        X  encounter stand square
    ///   A  access point (heals the party, solid)
    /// </summary>
    public class OverworldMap
    {
        public readonly int Width;
        public readonly int Height;
        readonly char[,] cells;

        public OverworldMap(string text)
        {
            var rows = new List<string>();
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                var line = raw.TrimEnd();
                if (line.Length > 0) rows.Add(line);
            }

            Height = rows.Count;
            foreach (var r in rows) Width = Mathf.Max(Width, r.Length);
            cells = new char[Width, Height];
            for (int row = 0; row < Height; row++)
                for (int x = 0; x < Width; x++)
                    cells[x, row] = x < rows[row].Length ? rows[row][x] : '#';
        }

        public bool InBounds(int x, int row) { return x >= 0 && x < Width && row >= 0 && row < Height; }

        public char At(int x, int row) { return InBounds(x, row) ? cells[x, row] : '#'; }

        public void Set(int x, int row, char c) { if (InBounds(x, row)) cells[x, row] = c; }

        public static int BlockHeight(char c)
        {
            switch (c)
            {
                case 'H': return 5;
                case '#': return 3;
                case 'B': return 2;
                case 'L': return 1;
                default: return 0;
            }
        }

        /// <summary>Height used when building walls: out-of-bounds counts as open ground so edge walls get faces.</summary>
        public int HeightAt(int x, int row) { return InBounds(x, row) ? BlockHeight(cells[x, row]) : 0; }

        public static bool IsSolidChar(char c)
        {
            return BlockHeight(c) > 0 || c == 'T' || c == 'b' || c == 'r' || c == 'n' || c == 'E' || c == 'A';
        }

        public bool IsSolidCell(int x, int row) { return !InBounds(x, row) || IsSolidChar(cells[x, row]); }

        public Vector3 CellCenter(int x, int row)
        {
            return new Vector3(x + 0.5f, 0f, Height - 1 - row + 0.5f);
        }

        public void WorldToCell(Vector3 p, out int x, out int row)
        {
            x = Mathf.FloorToInt(p.x);
            row = Height - 1 - Mathf.FloorToInt(p.z);
        }

        public bool IsSolidAt(Vector3 p)
        {
            int x, row;
            WorldToCell(p, out x, out row);
            return IsSolidCell(x, row);
        }

        public List<Vector2Int> FindAll(char c)
        {
            var found = new List<Vector2Int>();
            for (int row = 0; row < Height; row++)
                for (int x = 0; x < Width; x++)
                    if (cells[x, row] == c) found.Add(new Vector2Int(x, row));
            return found;
        }

        public static int Hash(int x, int row, int salt = 0)
        {
            unchecked
            {
                int h = x * 73856093 ^ row * 19349663 ^ salt * 83492791;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                return h & 0x7fffffff;
            }
        }
    }
}
