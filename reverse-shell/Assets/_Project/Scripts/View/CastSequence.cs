using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.View
{
    /// <summary>
    /// A card's cast animation, built from reusable steps. Assign one to CardDefinition.castSequence.
    /// Damage numbers and hit shakes play afterwards from the battle events, so a sequence is the
    /// "wind-up": lunges, flashes, projectiles, VFX.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Cast Sequence", fileName = "Seq_New")]
    public class CastSequence : ScriptableObject
    {
        [SerializeReference, SubclassSelector]
        public List<CastStep> steps = new List<CastStep>();

        public IEnumerator Play(CastContext ctx)
        {
            foreach (var step in steps)
            {
                if (step == null) continue;
                if (step.runInParallel) ctx.View.StartCoroutine(step.Play(ctx));
                else yield return ctx.View.StartCoroutine(step.Play(ctx));
            }
        }

        void OnValidate()
        {
            ManagedReferenceUtility.MakeUnique(steps, new HashSet<object>());
        }

        static readonly Dictionary<string, CastSequence> defaults = new Dictionary<string, CastSequence>();

        /// <summary>Fallback used when a card has no sequence assigned.</summary>
        public static CastSequence GetDefault(CardKind kind, bool isRitual)
        {
            string key = kind + (isRitual ? "_ritual" : "");
            CastSequence seq;
            if (defaults.TryGetValue(key, out seq) && seq != null) return seq;

            seq = CreateInstance<CastSequence>();
            seq.name = "DefaultSeq_" + key;
            seq.hideFlags = HideFlags.DontSave;
            if (isRitual)
            {
                seq.steps.Add(new FlashStep { at = StepTarget.Caster, useCasterColor = true, duration = 0.35f });
                seq.steps.Add(new ScreenShakeStep { strength = 12f, duration = 0.3f, runInParallel = true });
            }
            if (kind == CardKind.Attack || kind == CardKind.Debuff)
            {
                seq.steps.Add(new LungeStep());
                seq.steps.Add(new FlashStep { at = StepTarget.Targets, color = Color.white, duration = 0.15f });
            }
            else
            {
                seq.steps.Add(new PulseStep { at = StepTarget.Caster });
                seq.steps.Add(new FlashStep { at = StepTarget.Targets, useCasterColor = true, duration = 0.3f });
            }
            defaults[key] = seq;
            return seq;
        }
    }

    public class CastContext
    {
        public readonly BattleView View;
        public readonly UnitView Caster;
        public readonly List<UnitView> Targets;
        public readonly Color CasterColor;

        public CastContext(BattleView view, UnitView caster, List<UnitView> targets, Color casterColor)
        {
            View = view;
            Caster = caster;
            Targets = targets;
            CasterColor = casterColor;
        }

        public List<UnitView> Resolve(StepTarget at)
        {
            if (at == StepTarget.Caster) return Caster != null ? new List<UnitView> { Caster } : new List<UnitView>();
            return Targets;
        }

        public Vector3 TargetsCenter()
        {
            if (Targets.Count == 0) return Caster != null ? Caster.Center : Vector3.zero;
            Vector3 sum = Vector3.zero;
            foreach (var t in Targets) sum += t.Center;
            return sum / Targets.Count;
        }
    }
}
