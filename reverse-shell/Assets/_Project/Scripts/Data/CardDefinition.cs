using System;
using System.Collections.Generic;
using Combat.View;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// A card's data. Each rank (1-3 stars) has its own effect list, so upgrading via merge can
    /// change numbers or add whole new effects. Rituals only use rank 1.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Card", fileName = "Card_New")]
    public class CardDefinition : ScriptableObject
    {
        public string displayName;
        public CardKind kind;
        public TargetMode targetMode;
        [Tooltip("Ritual (ultimate) cards never merge and cost all Moxie.")]
        public bool isRitual;
        public Sprite icon;

        public List<CardRank> ranks = new List<CardRank> { new CardRank(), new CardRank(), new CardRank() };

        [Tooltip("Animation played when this card is cast. Leave empty for a default based on kind.")]
        public CastSequence castSequence;

        public string DisplayName
        {
            get { return string.IsNullOrEmpty(displayName) ? name : displayName; }
        }

        public int MaxRank
        {
            get { return isRitual ? 1 : Mathf.Clamp(ranks.Count, 1, 3); }
        }

        public CardRank GetRank(int rank)
        {
            if (ranks.Count == 0) return null;
            return ranks[Mathf.Clamp(rank - 1, 0, ranks.Count - 1)];
        }

        void OnValidate()
        {
            // Adding list elements in the 2019.4 inspector duplicates managed references; split them.
            var seen = new HashSet<object>();
            foreach (var rank in ranks)
                if (rank != null) ManagedReferenceUtility.MakeUnique(rank.effects, seen);
        }
    }

    [Serializable]
    public class CardRank
    {
        [Tooltip("Shown on the card. Leave empty to auto-generate from the effects.")]
        [TextArea(2, 4)] public string description;

        [SerializeReference, SubclassSelector]
        public List<CardEffect> effects = new List<CardEffect>();
    }
}
