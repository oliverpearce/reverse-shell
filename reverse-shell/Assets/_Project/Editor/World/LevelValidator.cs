using System.Collections.Generic;
using Game.World;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// The Level Designer's Validate: finds the problems that would break an area in game. Each problem can point at a
    /// cell (row 0 is the top line of the map), so clicking it in the designer selects that cell.
    /// </summary>
    public static class LevelValidator
    {
        public class Problem
        {
            public string Text;
            public bool Error;              // false = a warning (worth a look, but the area still works)
            public Vector2Int? Cell;
        }

        static readonly Vector2Int[] Steps = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        public static List<Problem> Check(AreaAsset area, IList<AreaAsset> allAreas)
        {
            var problems = new List<Problem>();
            if (string.IsNullOrEmpty(area.id)) Add(problems, true, "The area has no id (saves and warps need one).");
            foreach (var other in allAreas)
                if (other != null && other != area && other.id == area.id)
                    Add(problems, true, "Another area (" + other.name + ") has the same id '" + area.id + "'.");

            var lines = new List<string>((area.map ?? "").Replace("\r", "").TrimEnd('\n').Split('\n'));
            if (lines.Count == 0 || lines[0].Length == 0)
            {
                Add(problems, true, "The map is empty.");
                return problems;
            }
            for (int row = 0; row < lines.Count; row++)
                if (lines[row].Length != lines[0].Length)
                    Add(problems, true, "Row " + row + " is " + lines[row].Length + " wide; the first row is " + lines[0].Length + ".",
                        new Vector2Int(Mathf.Min(lines[row].Length, lines[0].Length), row));

            var map = new OverworldMap(area.map);
            for (int row = 0; row < map.Height; row++)
                for (int x = 0; x < map.Width; x++)
                    if (TileLegend.Find(map.At(x, row)) == null)
                        Add(problems, false, "'" + map.At(x, row) + "' isn't in the tile legend (it builds as plain ground or a block).", new Vector2Int(x, row));

            // Spawn, fights, people, chests.
            var spawns = map.FindAll('S');
            if (spawns.Count != 1) Add(problems, true, "There must be exactly one spawn S (found " + spawns.Count + ").", spawns.Count > 1 ? spawns[1] : (Vector2Int?)null);
            var fights = map.FindAll('E');
            var stands = map.FindAll('X');
            if (fights.Count != stands.Count)
                Add(problems, true, fights.Count + " enemies E but " + stands.Count + " stand squares X: every E needs one X.");
            if (area.encounters.Count > 0 && area.encounters.Count != fights.Count)
                Add(problems, false, area.encounters.Count + " fights are set up for " + fights.Count + " enemies E (they pair up in reading order; "
                    + "missing ones are the practice fight).");
            var npcs = map.FindAll('n');
            if (area.npcs.Count > npcs.Count)
                Add(problems, false, area.npcs.Count + " NPCs are set up but the map only has " + npcs.Count + " n.");
            var chests = map.FindAll('c');
            if (area.chests.Count != chests.Count)
                Add(problems, false, chests.Count + " chests c on the map, " + area.chests.Count + " Parts set up for them.");

            // Warps.
            var warpTiles = new HashSet<char>();
            foreach (var warp in area.warps)
            {
                if (warp == null || string.IsNullOrEmpty(warp.tile)) { Add(problems, true, "A warp has no tile."); continue; }
                char tile = warp.tile[0];
                warpTiles.Add(tile);
                if (map.FindAll(tile).Count == 0)
                    Add(problems, false, "Warp '" + tile + "' isn't painted on the map.");
                AreaAsset target = null;
                foreach (var other in allAreas) if (other != null && other.id == warp.to) target = other;
                if (target == null) { Add(problems, true, "Warp '" + tile + "' leads to '" + warp.to + "', which isn't an area."); continue; }
                bool back = false;
                foreach (var w in target.warps) back |= w != null && w.to == area.id;
                if (!back) Add(problems, false, "Warp '" + tile + "' leads to " + target.id + ", but no warp there leads back (you'd arrive at its spawn).");
            }
            foreach (var tile in new[] { 'W', 'J' })
                if (!warpTiles.Contains(tile) && map.FindAll(tile).Count > 0)
                    Add(problems, true, "'" + tile + "' is painted but no warp says where it leads.", map.FindAll(tile)[0]);

            // Reachability from the spawn (the hidden route V counts as open).
            if (spawns.Count == 1)
            {
                var seen = Reachable(map, spawns[0]);
                foreach (char tile in "WJXZVw")
                    foreach (var cell in map.FindAll(tile))
                        if (!seen.Contains(cell)) Add(problems, true, "'" + tile + "' can't be reached from the spawn.", cell);
                foreach (char tile in "KO1234567UnAcRGC")
                    foreach (var cell in map.FindAll(tile))
                    {
                        bool side = false;
                        foreach (var d in Steps) side |= seen.Contains(cell + d);
                        if (!side) Add(problems, true, "'" + tile + "' has no reachable side to interact from.", cell);
                    }
            }
            return problems;
        }

        /// <summary>Every walkable cell you can walk to from <paramref name="start"/>.</summary>
        public static HashSet<Vector2Int> Reachable(OverworldMap map, Vector2Int start)
        {
            var seen = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in Steps)
                {
                    var n = c + d;
                    if (seen.Contains(n) || !map.InBounds(n.x, n.y)) continue;
                    if (map.IsSolidCell(n.x, n.y) && map.At(n.x, n.y) != 'V') continue;
                    seen.Add(n);
                    queue.Enqueue(n);
                }
            }
            return seen;
        }

        static void Add(List<Problem> list, bool error, string text, Vector2Int? cell = null)
        {
            list.Add(new Problem { Error = error, Text = text, Cell = cell });
        }
    }
}
