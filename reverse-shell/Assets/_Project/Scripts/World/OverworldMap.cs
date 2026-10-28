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
    ///   A  access point (heals the party, solid)      R  summon beacon (respawns beaten machines, solid)
    ///   W  warp square (walkable; interact to travel to another area, see WorldAreas)
    ///   G  rune stone (solid; interact to read it)
    ///   C  sandbox chest (solid; sets the practice fights' enemy level, Sandbox)
    ///   c  loot chest (solid; one Part each, WorldAreas.Area.Chests)
    ///   Chapter One story tiles (ChapterOneDirector): K procedure pod, 1-3 memory fragments, 4-7 records, O the
    ///   scanner, U the Soul Card pedestal (all solid props); Z story trigger, : trace (walkable);
    ///   V the hidden route (a cracked wall until opened, then w, a warp); J a second warp tile.
    ///
    /// The Level Designer's palette names every tile (<see cref="TileLegend"/>); add new tiles there too.
    ///
    /// Camera rule (<see cref="ApplyCameraRule"/>, Octopath-style): nothing tall stands just south of a walkable
    /// square, where it would come between the camera and the player.
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
            return BlockHeight(c) > 0 || c == 'T' || c == 'b' || c == 'r' || c == 'n' || c == 'E' || c == 'A' || c == 'R' || c == 'G' || c == 'C' || c == 'c'
                   || c == 'K' || c == 'O' || c == 'U' || c == 'V' || (c >= '1' && c <= '7');
        }

        public bool IsSolidCell(int x, int row) { return !InBounds(x, row) || (IsSolidChar(cells[x, row]) && !passable.Contains(new Vector2Int(x, row))); }

        /// <summary>Items you've used (story props, loot chests, rune stones): their squares stop blocking the player.</summary>
        readonly HashSet<Vector2Int> passable = new HashSet<Vector2Int>();

        public void MakePassable(int x, int row) { if (InBounds(x, row)) passable.Add(new Vector2Int(x, row)); }

        /// <summary>The items that become walkable once used: K the pod, 1-7 fragments and records, O the scanner, U the
        /// pedestal, c loot chests, G rune stones (not the cracked wall V, which opens on its own terms).</summary>
        public static bool IsItemChar(char c)
        {
            return c == 'K' || c == 'O' || c == 'U' || c == 'c' || c == 'G' || (c >= '1' && c <= '7');
        }

        /// <summary>Height of a tree's canopy, for the camera rule (trees are props, not blocks).</summary>
        const float TreeHeight = 3f;

        /// <summary>
        /// Octopath-style readability: the camera looks north from the south, so anything tall a few rows south of
        /// where the player can walk hides them. Each block is lowered (never raised) to the tallest height the camera
        /// sees over from every walkable square north of it in its column: a block <c>k</c> rows south may stand
        /// <c>eye + k * tan(pitch)</c> high. So tall ruins step down toward the street (low wall, brick, ruin, tower)
        /// and trees right below a path become bushes. Solidity is unchanged.
        /// </summary>
        /// <param name="pitch">Camera pitch in degrees (OverworldCamera.pitch).</param>
        /// <param name="eye">How high on the player sprite must stay visible (world units).</param>
        public void ApplyCameraRule(float pitch, float eye = 0.9f)
        {
            float slope = Mathf.Tan(pitch * Mathf.Deg2Rad);
            for (int x = 0; x < Width; x++)
            {
                for (int row = 0; row < Height; row++)
                {
                    char c = cells[x, row];
                    bool tree = c == 'T';
                    if (BlockHeight(c) == 0 && !tree) continue;
                    // The nearest walkable square straight north of this cell (rows decrease northward).
                    float allowed = float.MaxValue;
                    for (int k = 1; k <= 6 && row - k >= 0; k++)
                    {
                        if (IsSolidChar(cells[x, row - k])) continue;
                        allowed = eye + k * slope;
                        break;
                    }
                    if (allowed == float.MaxValue) continue;
                    if (tree)
                    {
                        if (allowed < TreeHeight) cells[x, row] = 'b';
                        continue;
                    }
                    if (BlockHeight(c) <= allowed) continue;
                    cells[x, row] = allowed >= 5f ? 'H' : allowed >= 3f ? '#' : allowed >= 2f ? 'B' : 'L';
                }
            }
        }

        public Vector3 CellCenter(int x, int row)
        {
            return new Vector3(x + 0.5f, 0f, Height - 1 - row + 0.5f);
        }

        public void WorldToCell(Vector3 p, out int x, out int row)
        {
            x = Mathf.FloorToInt(p.x);
            row = Height - 1 - Mathf.FloorToInt(p.z);
        }

        /// <summary>How tall the terrain is at a world point: a block's height, a tree's canopy, else 0.</summary>
        public float TopAt(Vector3 p)
        {
            int x, row;
            WorldToCell(p, out x, out row);
            if (!InBounds(x, row)) return 0f;
            return cells[x, row] == 'T' ? TreeHeight : HeightAt(x, row);
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
