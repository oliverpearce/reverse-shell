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

    /// <summary>A field terminal: restores the whole party to full HP.</summary>
    public class AccessPointInteractable : Interactable
    {
        public float range = 1.35f;

        public override string Prompt { get { return "Use Access Point"; } }

        public override float Score(Vector3 playerPosition)
        {
            float d = FlatDistance(playerPosition, transform.position);
            return d <= range ? d : -1f;
        }

        public override void Interact(OverworldBuilder world)
        {
            WorldSession.RestoreParty();
            SaveGame.Save();
            world.RefreshPartyHud();
            world.UI.Say("Access Point", "{cyan}SYSTEM RESTORED.{/c}{p} Party HP: {wave}full{/wave}. Progress {green}saved{/c}.", DialogueVoice.System);
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
                world.UI.Say("Summon Beacon", "{gray}No signal to send.{/c}{p} Every machine in the area is still active.", DialogueVoice.Machine);
                return;
            }
            string count = beaten == 1 ? "1 machine" : beaten + " machines";
            world.UI.Dialogue.Ask(new DialogueLine("Summon Beacon",
                    "{red}WARNING:{/c} broadcasting will reactivate {shake}" + count + "{/shake} in this area.{p} Send the signal?", DialogueVoice.Machine),
                new[] { "Resummon", "Cancel" },
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

        public override string Prompt { get { return "Talk to " + speaker; } }

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
}
