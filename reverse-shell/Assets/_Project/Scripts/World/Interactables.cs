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
        /// <summary>The area's fight definition (enemies, level, story flag); null = the shared practice fight.</summary>
        public WorldAreas.EncounterDef def;
        public string id;
        public string displayName = "Machine";
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

    /// <summary>A field terminal: restores the whole party to full HP (and saves). Stand on its purple square.</summary>
    public class AccessPointInteractable : Interactable
    {
        [Tooltip("Center of the purple square in front of the terminal.")]
        public Vector3 standPoint;
        public float standRadius = 0.6f;

        public override string Prompt { get { return "Heal"; } }

        public override float Score(Vector3 playerPosition)
        {
            float d = FlatDistance(playerPosition, standPoint);
            return d <= standRadius ? d : -1f;
        }

        public override void Interact(OverworldBuilder world)
        {
            world.RestAtAccessPoint(standPoint);
        }
    }

    /// <summary>Summon beacon: after a yes/no confirmation, brings every beaten machine in this area back (for grinding).</summary>
    public class SummonInteractable : Interactable
    {
        public float range = 1.35f;

        public override string Prompt { get { return "Use Summon Beacon"; } }

        public override float Score(Vector3 playerPosition)
        {
            float d = FlatDistance(playerPosition, transform.position);
            return d <= range ? d : -1f;
        }

        public override void Interact(OverworldBuilder world)
        {
            int beaten = world.BeatenEncountersHere();
            if (beaten == 0)
            {
                world.UI.Say(StoryText.Speaker("beacon.none"), StoryText.Text("beacon.none"), DialogueVoice.Machine);
                return;
            }
            string count = beaten == 1 ? "1 machine" : beaten + " machines";
            world.UI.Dialogue.Ask(new DialogueLine(StoryText.Speaker("beacon.ask"), StoryText.Text("beacon.ask", "count", count), DialogueVoice.Machine),
                StoryText.Texts("beacon.options"),
                choice =>
                {
                    if (choice == 0) world.ResummonEncounters();
                });
        }
    }

    /// <summary>NPC conversation: interact to play all their lines (F / Space / Enter to advance).</summary>
    public class TalkInteractable : Interactable
    {
        public string speaker = "Stranger";
        [Tooltip("Lines with DialogueMarkup, e.g. {wave}hi{/wave}, {shake}, {red}...{/c}, {p}, {slow}...{/slow}.")]
        [TextArea] public string[] lines = { "..." };
        public float range = 1.35f;
        public DialogueVoice voice = DialogueVoice.Default;
        public Sprite portrait;
        [Tooltip("Replaces \"Talk to <speaker>\" (e.g. \"Read the rune\").")]
        public string prompt;

        public override string Prompt { get { return string.IsNullOrEmpty(prompt) ? "Talk to " + speaker : prompt; } }

        public override float Score(Vector3 playerPosition)
        {
            float d = FlatDistance(playerPosition, transform.position);
            return d <= range ? d : -1f;
        }

        public override void Interact(OverworldBuilder world)
        {
            if (lines == null || lines.Length == 0) return;
            var conversation = new System.Collections.Generic.List<DialogueLine>();
            foreach (var line in lines) conversation.Add(new DialogueLine(speaker, line, voice, portrait));
            world.UI.Talk(conversation);
        }
    }

    /// <summary>A story object (memory fragment, record, scanner, pedestal...): what happens is up to the director.</summary>
    public class StoryInteractable : Interactable
    {
        public float range = 1.35f;
        public System.Action<OverworldBuilder> onInteract;

        public override string Prompt { get { return "Interact"; } }

        public override float Score(Vector3 playerPosition)
        {
            float d = FlatDistance(playerPosition, transform.position);
            return d <= range ? d : -1f;
        }

        public override void Interact(OverworldBuilder world)
        {
            if (onInteract != null) onInteract(world);
        }
    }

    /// <summary>A loot chest ('c'): holds one Part, opened once (its story flag remembers). See OverworldBuilder.OpenLootChest.</summary>
    public class LootChestInteractable : Interactable
    {
        public float range = 1.35f;
        public string part, flag;

        public override string Prompt { get { return StoryState.Has(flag) ? "Look inside" : "Open the chest"; } }

        public override float Score(Vector3 playerPosition)
        {
            float d = FlatDistance(playerPosition, transform.position);
            return d <= range ? d : -1f;
        }

        public override void Interact(OverworldBuilder world)
        {
            world.OpenLootChest(this);
        }

        /// <summary>The lid tipped back (an opened chest).</summary>
        public void ShowOpen()
        {
            var lid = transform.Find("Lid");
            if (lid == null) return;
            lid.localPosition = new Vector3(0f, 0.72f, 0.26f);
            lid.localRotation = Quaternion.Euler(-60f, 0f, 0f);
        }
    }

    /// <summary>The Training Center chest: opens the enemy level panel (EnemyLevelPanel).</summary>
    public class ChestInteractable : Interactable
    {
        public float range = 1.35f;

        public override string Prompt { get { return "Open the chest"; } }

        public override float Score(Vector3 playerPosition)
        {
            float d = FlatDistance(playerPosition, transform.position);
            return d <= range ? d : -1f;
        }

        public override void Interact(OverworldBuilder world)
        {
            world.OpenTrainingChest();
        }
    }

    /// <summary>A warp square: stand on it and interact to travel to another area (WorldAreas).</summary>
    public class WarpInteractable : Interactable
    {
        public string destination;
        public string label = "Travel";
        public float radius = 0.6f;

        public override string Prompt { get { return label; } }

        public override Vector3 PromptAnchor { get { return transform.position + Vector3.up * 1.4f; } }

        public override float Score(Vector3 playerPosition)
        {
            float d = FlatDistance(playerPosition, transform.position);
            return d <= radius ? d : -1f;
        }

        public override void Interact(OverworldBuilder world)
        {
            world.TravelTo(destination);
        }
    }
}
