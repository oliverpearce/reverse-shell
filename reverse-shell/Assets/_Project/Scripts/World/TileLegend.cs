using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Every map character with a name, a group and a swatch colour, for the Level Designer's palette and legend.
    /// Whether a tile is solid and how tall a block is still come from <see cref="OverworldMap"/> (IsSolidChar,
    /// BlockHeight), so the two can't disagree. Add a line here when a new tile is added to the map builder.
    /// </summary>
    public static class TileLegend
    {
        public class Tile
        {
            public char Char;
            public string Name, Group, Note;
            public Color Color;
            public bool Solid { get { return OverworldMap.IsSolidChar(Char); } }
            public int Height { get { return OverworldMap.BlockHeight(Char); } }
        }

        public static readonly string[] Groups = { "Ground", "Walls", "Nature", "Effects", "Interactables", "Portals", "Story" };

        public static readonly Tile[] All =
        {
            T('.', "Cracked pavement", "Ground", 0.62f, 0.6f, 0.55f),
            T('=', "Overgrown road", "Ground", 0.42f, 0.42f, 0.4f),
            T('P', "Grass plaza", "Ground", 0.45f, 0.62f, 0.35f),
            T(',', "Tall grass", "Ground", 0.36f, 0.55f, 0.28f),
            T('*', "Wildflowers", "Ground", 0.78f, 0.62f, 0.4f),

            T('H', "Tall ruin (5)", "Walls", 0.2f, 0.19f, 0.18f),
            T('#', "Ruin (3)", "Walls", 0.32f, 0.3f, 0.28f),
            T('B', "Mossy brick wall (2)", "Walls", 0.45f, 0.38f, 0.3f),
            T('L', "Low broken wall (1)", "Walls", 0.55f, 0.5f, 0.42f),

            T('T', "Tree", "Nature", 0.18f, 0.42f, 0.2f),
            T('b', "Bush", "Nature", 0.28f, 0.5f, 0.24f),
            T('r', "Rubble pile", "Nature", 0.5f, 0.45f, 0.4f),
            T('p', "Pine", "Nature", 0.15f, 0.34f, 0.22f, "A pine (nature kit)."),
            T('d', "Dead tree", "Nature", 0.42f, 0.36f, 0.3f, "A bare dead tree (nature kit)."),
            T('k', "Twisted tree", "Nature", 0.32f, 0.3f, 0.2f, "A big gnarled tree; its leaves take the world's leaf colour."),
            T('f', "Fern", "Nature", 0.3f, 0.52f, 0.26f, "A big fern or leafy plant."),
            T('o', "Big rock", "Nature", 0.45f, 0.45f, 0.45f, "A boulder."),
            T('m', "Mushrooms", "Nature", 0.75f, 0.55f, 0.4f, "Walkable: a few mushrooms on moss."),
            T('e', "Pebbles", "Nature", 0.6f, 0.58f, 0.52f, "Walkable: scattered pebbles on moss."),
            T('_', "Stone path", "Nature", 0.55f, 0.53f, 0.48f, "Walkable: stepping stones over the pavement."),
            T('~', "Lava", "Effects", 1f, 0.45f, 0.1f, "Glowing, flowing lava with embers (blocks the way)."),
            T('z', "Ooze pool", "Effects", 0.5f, 0.85f, 0.2f, "Glowing toxic ooze with bubbles (blocks the way)."),
            T('i', "Ice crystal", "Effects", 0.6f, 0.9f, 1f, "A sparkling cluster of glowing ice (blocks the way)."),
            T('l', "Light shaft", "Effects", 1f, 0.95f, 0.75f, "Walkable: a soft beam of light falling here."),
            T('y', "Prism shard", "Effects", 0.9f, 0.85f, 1f, "A floating crystal throwing rainbow glints (blocks the way)."),
            T('u', "Specimen capsule", "Effects", 0.45f, 1f, 0.6f, "A glass tube of glowing liquid with something floating inside (blocks the way)."),
            T('h', "Dormant robot", "Effects", 0.6f, 0.6f, 0.58f, "A lab robot slumped where it stopped (blocks the way)."),
            T('g', "Hanging chains", "Effects", 0.5f, 0.48f, 0.45f, "A gantry with chains swaying from it (blocks the way)."),
            T('t', "Fire", "Effects", 1f, 0.55f, 0.15f, "A brazier with a flickering fire, embers and warm light (blocks the way)."),
            T('a', "Office desk", "Effects", 0.85f, 0.78f, 0.62f, "A desk with the Retro PC and a chair (blocks the way). The office's opening uses the desk just north of S."),
            T('v', "Lab bench", "Effects", 0.45f, 0.75f, 0.7f, "A steel bench with glowing test tubes, flasks and a microscope (blocks the way)."),
            T('x', "Server rack", "Effects", 0.2f, 0.3f, 0.25f, "A tall cabinet of blinking indicator lights (blocks the way)."),

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
            T('Y', "Language chip", "Story", 0.45f, 1f, 0.6f, "Chapter One: in the pod room from the start; makes card words readable."),
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
