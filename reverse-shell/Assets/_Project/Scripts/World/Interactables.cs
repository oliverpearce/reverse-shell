using Combat;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Anything the player can press the interact key on. Subclass to add new kinds
    /// (doors, chests, shrines...): decide when it's in range, what the prompt says, and what happens.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        public abstract string Prompt { get; }

        /// <summary>World position the floating prompt is drawn at.</summary>
        public virtual Vector3 PromptAnchor { get { return transform.position + Vector3.up * 1.9f; } }

        /// <summary>Distance-like score; lower wins when several are in range. Return < 0 when out of range.</summary>
        public abstract float Score(Vector3 playerPosition);

        public abstract void Interact(OverworldBuilder world);

        protected static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }

    /// <summary>Stand on the marked square and press interact to start a battle.</summary>
    public class EncounterInteractable : Interactable
    {
        public string id;
        public string displayName = "Static Thug";
        public BattleSetup setup;
        [Tooltip("Center of the square the player must stand on.")]
        public Vector3 standPoint;
        public float standRadius = 0.55f;

        public override string Prompt { get { return "Fight the " + displayName; } }

        public override float Score(Vector3 playerPosition)
        {
            float d = FlatDistance(playerPosition, standPoint);
            return d <= standRadius ? d : -1f;
        }

        public override void Interact(OverworldBuilder world)
        {
            world.StartEncounter(this);
        }
    }

    /// <summary>NPC with a few lines of dialogue; each press shows the next line.</summary>
    public class TalkInteractable : Interactable
    {
        public string speaker = "Stranger";
        [TextArea] public string[] lines = { "..." };
        public float range = 1.35f;

        int next;

        public override string Prompt { get { return "Talk to " + speaker; } }

        public override float Score(Vector3 playerPosition)
        {
            float d = FlatDistance(playerPosition, transform.position);
            return d <= range ? d : -1f;
        }

        public override void Interact(OverworldBuilder world)
        {
            if (lines == null || lines.Length == 0) return;
            world.UI.ShowMessage(speaker, lines[next % lines.Length]);
            next++;
        }
    }
}
