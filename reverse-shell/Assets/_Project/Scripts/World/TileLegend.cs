using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Every map character with a name, a group and a swatch colour, for the Level Designer's palette and legend.
    /// Whether a tile is solid and how tall a block is still come from <see cref="OverworldMap"/> (IsSolidChar,
    /// BlockHeight), so the two can't disagree. Add a line here when a new tile is added to the map builder.
    ///
    /// Terrain is filed by chapter / subarea (<see cref="Sections"/>), then by <see cref="Kinds"/>:
    ///   Deco   = decoration: walk-through tiles (grass, pebbles, light) and solid set-dressing props (desks, racks, fire);
    ///   Solids = what shapes the map: walls, trees, rocks, hazards that block the way;
    ///   Misc   = floors and anything else.
    /// A new tile goes in the section of the chapter it was made for, or Shared when every theme uses it (the themes
    /// recolour the shared walls and floors). Interactables, Portals and Story are groups without kinds.
    /// </summary>
    public static class TileLegend
    {
        public class Tile
        {
            public char Char;
            public string Name, Group, Note;
            /// <summary>Deco / Solids / Misc for terrain (palette only); empty for Interactables, Portals and Story.</summary>
            public string Kind = "";
            public Color Color;
            public bool Solid { get { return OverworldMap.IsSolidChar(Char); } }
            public int Height { get { return OverworldMap.BlockHeight(Char); } }
        }

        public const string Shared = "Shared (all chapters)";
        public const string Ch1City = "Chapter 1 · Ruined City";
        public const string Ch1Office = "Chapter 1 · Office";
        public const string Ch1Forest = "Chapter 1 · Forest (intro)";
        public const string Ch2 = "Chapter 2 · Conservatory";
        public const string Ch3 = "Chapter 3 · Frozen";
        public const string Ch4 = "Chapter 4 · Cinderworks";
        public const string Ch5 = "Chapter 5 · Sanctuary";

        public const string Deco = "Deco", Solids = "Solids", Misc = "Misc";
        public static readonly string[] Kinds = { Deco, Solids, Misc };

        /// <summary>The terrain sections, in palette order (each split into <see cref="Kinds"/>).</summary>
        public static readonly string[] Sections = { Shared, Ch1City, Ch1Office, Ch1Forest, Ch2, Ch3, Ch4, Ch5 };

        /// <summary>Every palette group: the terrain sections, then the groups that aren't split by kind.</summary>
        public static readonly string[] Groups = { Shared, Ch1City, Ch1Office, Ch1Forest, Ch2, Ch3, Ch4, Ch5, "Interactables", "Portals", "Story" };

        /// <summary>True for a terrain section (split into Deco / Solids / Misc in the palette).</summary>
        public static bool IsTerrain(string group) { return System.Array.IndexOf(Sections, group) >= 0; }

        public static readonly Tile[] All =
        {
            // Shared: the walls and floors every theme recolours
            T('H', "Tall ruin (5)", Shared, Solids, 0.2f, 0.19f, 0.18f),
            T('#', "Ruin (3)", Shared, Solids, 0.32f, 0.3f, 0.28f),
            T('B', "Mossy brick wall (2)", Shared, Solids, 0.45f, 0.38f, 0.3f),
            T('L', "Low broken wall (1)", Shared, Solids, 0.55f, 0.5f, 0.42f),
            T('.', "Cracked pavement", Shared, Misc, 0.62f, 0.6f, 0.55f),
            T('=', "Overgrown road", Shared, Misc, 0.42f, 0.42f, 0.4f),
            T('P', "Grass plaza", Shared, Misc, 0.45f, 0.62f, 0.35f),

            // Chapter 1: the ruined city (City, Tunnel, Chamber; the Sandbox uses these too)
            T(',', "Tall grass", Ch1City, Deco, 0.36f, 0.55f, 0.28f),
            T('*', "Wildflowers", Ch1City, Deco, 0.78f, 0.62f, 0.4f),
            T('b', "Bush", Ch1City, Deco, 0.28f, 0.5f, 0.24f),
            T('r', "Rubble pile", Ch1City, Deco, 0.5f, 0.45f, 0.4f),
            T('T', "Tree", Ch1City, Solids, 0.18f, 0.42f, 0.2f),

            // Chapter 1: the office
            T('a', "Office desk", Ch1Office, Deco, 0.85f, 0.78f, 0.62f, "A desk with the Retro PC and a chair (blocks the way). The office's opening uses the desk just north of S."),
            T('v', "Lab bench", Ch1Office, Deco, 0.45f, 0.75f, 0.7f, "A steel bench with glowing test tubes, flasks and a microscope (blocks the way)."),
            T('x', "Server rack", Ch1Office, Deco, 0.2f, 0.3f, 0.25f, "A tall cabinet of blinking indicator lights (blocks the way)."),
            T('u', "Specimen capsule", Ch1Office, Deco, 0.45f, 1f, 0.6f, "A glass tube of glowing liquid with something floating inside (blocks the way)."),

            // Chapter 1: the forest intro (Forest, Forest Lab; the nature kit)
            T('f', "Fern", Ch1Forest, Deco, 0.3f, 0.52f, 0.26f, "A big fern or leafy plant."),
            T('m', "Mushrooms", Ch1Forest, Deco, 0.75f, 0.55f, 0.4f, "Walkable: a few mushrooms on moss."),
            T('e', "Pebbles", Ch1Forest, Deco, 0.6f, 0.58f, 0.52f, "Walkable: scattered pebbles on moss."),
            T('_', "Stone path", Ch1Forest, Deco, 0.55f, 0.53f, 0.48f, "Walkable: stepping stones over the pavement."),
            T('l', "Light shaft", Ch1Forest, Deco, 1f, 0.95f, 0.75f, "Walkable: a soft beam of light falling here."),
            T('h', "Dormant robot", Ch1Forest, Deco, 0.6f, 0.6f, 0.58f, "A lab robot slumped where it stopped (blocks the way)."),
            T('p', "Pine", Ch1Forest, Solids, 0.15f, 0.34f, 0.22f, "A pine (nature kit)."),
            T('k', "Twisted tree", Ch1Forest, Solids, 0.32f, 0.3f, 0.2f, "A big gnarled tree; its leaves take the world's leaf colour."),
            T('o', "Big rock", Ch1Forest, Solids, 0.45f, 0.45f, 0.45f, "A boulder."),

            // Chapter 2: the conservatory
            T('g', "Hanging chains", Ch2, Deco, 0.5f, 0.48f, 0.45f, "A gantry with chains swaying from it (blocks the way)."),
            T('z', "Ooze pool", Ch2, Solids, 0.5f, 0.85f, 0.2f, "Glowing toxic ooze with bubbles (blocks the way)."),

            // Chapter 3: frozen
            T('i', "Ice crystal", Ch3, Solids, 0.6f, 0.9f, 1f, "A sparkling cluster of glowing ice (blocks the way)."),
            T('d', "Dead tree", Ch3, Solids, 0.42f, 0.36f, 0.3f, "A bare dead tree (nature kit)."),

            // Chapter 4: the cinderworks
            T('t', "Fire", Ch4, Deco, 1f, 0.55f, 0.15f, "A brazier with a flickering fire, embers and warm light (blocks the way)."),
            T('~', "Lava", Ch4, Solids, 1f, 0.45f, 0.1f, "Glowing, flowing lava with embers (blocks the way)."),

            // Chapter 5: the sanctuary
            T('y', "Prism shard", Ch5, Deco, 0.9f, 0.85f, 1f, "A floating crystal throwing rainbow glints (blocks the way)."),

            T('S', "Player spawn", "Interactables", 0.3f, 0.75f, 1f, "Where you start when you don't arrive through a warp. One per area."),
            T('n', "NPC", "Interactables", 0.95f, 0.75f, 0.35f, "A person or machine; its name and lines are the area's NPC list, in reading order."),
            T('E', "Encounter enemy", "Interactables", 0.9f, 0.3f, 0.28f, "A machine to fight; needs an X next to it. Its fight is the area's encounter list, in reading order."),
            T('X', "Encounter stand square", "Interactables", 1f, 0.55f, 0.5f, "Where you stand to start the fight with the E beside it."),
            T('A', "Access point", "Interactables", 0.65f, 0.4f, 0.95f, "Heals, saves, and brings beaten patrols back."),
            T('R', "Summon beacon", "Interactables", 0.85f, 0.2f, 0.25f, "Brings every beaten machine in the area back."),
            T('W', "Warp square", "Interactables", 0.3f, 0.95f, 0.9f, "Interact to travel. Where it leads is set in the area's warps."),
            T('J', "Second warp", "Interactables", 0.2f, 0.75f, 0.85f, "Another warp tile, so an area can lead to two places."),
            T('j', "Third warp", "Interactables", 0.15f, 0.6f, 0.75f, "A third warp tile (a door or path to a third place)."),
            T('F', "Portal: forest", "Portals", 0.55f, 1f, 0.45f, "The Sandbox's portal to the Chapter One intro (any warp tile works; set it in the area's warps)."),
            T('D', "Portal: conservatory", "Portals", 0.75f, 1f, 0.2f, "The Sandbox's portal to the Chapter Two intro."),
            T('I', "Portal: frozen", "Portals", 0.6f, 0.9f, 1f, "The Sandbox's portal to the Chapter Three intro."),
            T('N', "Portal: cinderworks", "Portals", 1f, 0.5f, 0.15f, "The Sandbox's portal to the Chapter Four intro."),
            T('^', "Portal: sanctuary", "Portals", 1f, 0.9f, 0.6f, "The Sandbox's portal to the Chapter Five intro."),
            T('G', "Rune stone", "Interactables", 0.6f, 0.65f, 0.8f, "Interact to read it."),
            T('C', "Sandbox chest", "Interactables", 0.75f, 0.6f, 0.3f, "Sets the practice fights' enemy level (Sandbox)."),
            T('c', "Loot chest", "Interactables", 0.9f, 0.75f, 0.3f, "Holds one Part, from the area's chest list in reading order; opens once."),

            T('K', "Procedure pod", "Story", 0.8f, 0.85f, 0.9f, "Chapter One: where you wake."),
            T('1', "Memory fragment 1", "Story", 0.55f, 0.85f, 1f),
            T('2', "Memory fragment 2", "Story", 0.55f, 0.85f, 1f),
            T('3', "Memory fragment 3", "Story", 0.55f, 0.85f, 1f),
            T('4', "Record 1", "Story", 0.85f, 0.85f, 0.7f),
            T('5', "Record 2", "Story", 0.85f, 0.85f, 0.7f),
            T('6', "Record 3", "Story", 0.85f, 0.85f, 0.7f),
            T('7', "Record 4", "Story", 0.85f, 0.85f, 0.7f),
            T('O', "Optical scanner", "Story", 0.4f, 0.95f, 1f),
            T('Q', "Colours chip", "Story", 0.8f, 0.5f, 1f, "Chapter One: puts the colours back in card text (gold numbers, coloured statuses)."),
            T('M', "Numbers chip", "Story", 1f, 0.82f, 0.35f, "Chapter One: makes card numbers readable (text, live numbers, status turns)."),
            T('U', "Soul Card pedestal", "Story", 1f, 0.85f, 0.4f),
            T('Z', "Story trigger", "Story", 1f, 0.4f, 0.8f, "Stepping on it runs a story beat (Chapter One's director)."),
            T(':', "Trace", "Story", 0.5f, 0.9f, 0.85f, "Marks the scanner shows."),
            T('V', "Hidden route", "Story", 0.55f, 0.35f, 0.5f, "A cracked wall until it's opened; then a warp."),
            T('w', "Opened route", "Story", 0.4f, 0.8f, 0.75f, "The hidden route once opened (a warp)."),
        };

        static Tile T(char c, string name, string group, float r, float g, float b, string note = "")
        {
            return new Tile { Char = c, Name = name, Group = group, Color = new Color(r, g, b), Note = note };
        }

        /// <summary>A terrain tile: its chapter section and its kind (Deco / Solids / Misc).</summary>
        static Tile T(char c, string name, string section, string kind, float r, float g, float b, string note = "")
        {
            var t = T(c, name, section, r, g, b, note);
            t.Kind = kind;
            return t;
        }

        /// <summary>The palette section to open for an area (by its id and theme, Resources/Themes/Theme_&lt;id&gt;), or null.
        /// The forest intro shares the city's Forest theme, so it is told apart by its id.</summary>
        public static string SectionFor(string areaId, string theme)
        {
            theme = theme ?? "";
            if ((areaId ?? "").StartsWith("intro_forest") || theme.StartsWith("Forest_")) return Ch1Forest;
            if (theme.StartsWith("Office")) return Ch1Office;
            if (theme.StartsWith("Conservatory")) return Ch2;
            if (theme.StartsWith("Frozen")) return Ch3;
            if (theme.StartsWith("Cinderworks")) return Ch4;
            if (theme.StartsWith("Sanctuary")) return Ch5;
            if (theme == "Forest" || theme == "Underground" || theme.Length == 0) return Ch1City;   // the city's look
            return null;
        }

        static Dictionary<char, Tile> byChar;

        /// <summary>The tile for this character, or null if the legend doesn't know it.</summary>
        public static Tile Find(char c)
        {
            if (byChar == null)
            {
                byChar = new Dictionary<char, Tile>();
                foreach (var t in All) byChar[t.Char] = t;
            }
            Tile tile;
            return byChar.TryGetValue(c, out tile) ? tile : null;
        }
    }
}
