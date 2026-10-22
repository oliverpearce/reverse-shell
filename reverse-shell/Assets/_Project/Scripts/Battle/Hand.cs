using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Ordered row of cards. Two adjacent cards with the same definition, owner and rank merge
    /// into one card of the next rank. Ritual cards never merge and don't count toward capacity.
    /// </summary>
    public class Hand
    {
        readonly List<CardInstance> cards = new List<CardInstance>();

        public int Capacity;

        public Hand(int capacity)
        {
            Capacity = capacity;
        }

        public IReadOnlyList<CardInstance> Cards { get { return cards; } }
        public int Count { get { return cards.Count; } }
        public CardInstance this[int index] { get { return cards[index]; } }

        public int IncantationCount
        {
            get
            {
                int n = 0;
                foreach (var c in cards) if (!c.IsRitual) n++;
                return n;
            }
        }

        public bool HasRoomForDraw { get { return IncantationCount < Capacity; } }

        public void Add(CardInstance card) { cards.Add(card); }
        public void Insert(int index, CardInstance card) { cards.Insert(Mathf.Clamp(index, 0, cards.Count), card); }
        public int IndexOf(CardInstance card) { return cards.IndexOf(card); }
        public bool Contains(CardInstance card) { return cards.Contains(card); }
        public bool Remove(CardInstance card) { return cards.Remove(card); }

        public CardInstance RemoveAt(int index)
        {
            var card = cards[index];
            cards.RemoveAt(index);
            return card;
        }

        public void Move(int from, int to)
        {
            var card = cards[from];
            cards.RemoveAt(from);
            cards.Insert(Mathf.Clamp(to, 0, cards.Count), card);
        }

        public int RemoveAllOwnedBy(BattleUnit owner)
        {
            return cards.RemoveAll(c => c.Owner == owner);
        }

        public bool HasRitualFor(BattleUnit owner)
        {
            return cards.Exists(c => c.IsRitual && c.Owner == owner);
        }

        /// <summary>Merges the leftmost mergeable pair, or returns null if none.</summary>
        public MergeResult TryMergeOnce()
        {
            for (int i = 0; i < cards.Count - 1; i++)
            {
                var left = cards[i];
                var right = cards[i + 1];
                if (!left.CanMergeWith(right)) continue;

                var merged = new CardInstance(left.Definition, left.Owner, left.Rank + 1);
                cards[i] = merged;
                cards.RemoveAt(i + 1);
                return new MergeResult(left, right, merged, i);
            }
            return null;
        }

        /// <summary>Merges until stable (chains like 2+1+1 -> 2+2 -> 3).</summary>
        public List<MergeResult> ResolveMerges()
        {
            var results = new List<MergeResult>();
            MergeResult merge;
            while ((merge = TryMergeOnce()) != null) results.Add(merge);
            return results;
        }

        public List<CardInstance> Snapshot() { return new List<CardInstance>(cards); }

        public void Restore(List<CardInstance> snapshot)
        {
            cards.Clear();
            cards.AddRange(snapshot);
        }
    }

    public class MergeResult
    {
        public readonly CardInstance Left;
        public readonly CardInstance Right;
        public readonly CardInstance Result;
        public readonly int Index;

        public MergeResult(CardInstance left, CardInstance right, CardInstance result, int index)
        {
            Left = left;
            Right = right;
            Result = result;
            Index = index;
        }
    }
}
