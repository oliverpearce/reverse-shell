using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>A playable character: stats plus the cards they contribute to the hand.</summary>
    [CreateAssetMenu(menuName = "Combat/Character", fileName = "Char_New")]
    public class CharacterDefinition : UnitDefinition
    {
        [Tooltip("The character's class / archetype, e.g. Cantor, Bruiser, Medic. Shown under their name.")]
        public string archetype;

        [Tooltip("Incantation cards dealt into the hand (Reverse: 1999 characters have two). The default deck.")]
        public List<CardDefinition> incantations = new List<CardDefinition>();

        [Tooltip("Extra incantations the player can swap into the deck from the character menu.")]
        public List<CardDefinition> cardPool = new List<CardDefinition>();

        [Tooltip("Ultimate card, spawned into the hand when Moxie reaches the cap.")]
        public CardDefinition ritual;

        [Tooltip("Base HP, ATK and DEF grow by this fraction per level after level 1 (0.08 = +8% per level).")]
        [Range(0f, 0.25f)] public float growthPerLevel = 0.08f;

        [Header("Character card")]
        [Tooltip("Front of the character card: the full robot.")]
        public Sprite portraitFront;
        [Tooltip("Back of the character card: the robot opened up.")]
        public Sprite portraitInternals;
        [Tooltip("White mask of the glowing accents, tinted with the glow color (front and back share it).")]
        public Sprite portraitGlow;
        [Tooltip("White mask of the glowing accents on the internals art.")]
        public Sprite portraitInternalsGlow;

        [Tooltip("Where the WPN / ARM / CHIP / CORE sockets sit on the internals art, in art pixels from its top-left. " +
                 "Empty = the shared robot model's spots.")]
        public List<Vector2> partSockets = new List<Vector2>();

        [Tooltip("Stat bars in the crew menu are scaled so this character's level-cap stats fill 90% (one per game: MC).")]
        public bool statBarReference;

        [Header("Glow / elements")]
        [Tooltip("Elements this character can channel. Empty or one = just their afflatus.")]
        public List<Afflatus> channels = new List<Afflatus>();
        [Tooltip("Glow in Glow Color instead of the afflatus color while no element is channeled (MC).")]
        public bool neutralGlow;
        public Color glowColor = Color.white;

        /// <summary>Every incantation this character could put in their deck: the defaults, then the pool.</summary>
        public List<CardDefinition> AllIncantations()
        {
            var all = new List<CardDefinition>();
            foreach (var c in incantations) if (c != null && !all.Contains(c)) all.Add(c);
            foreach (var c in cardPool) if (c != null && !all.Contains(c)) all.Add(c);
            return all;
        }
    }
}
