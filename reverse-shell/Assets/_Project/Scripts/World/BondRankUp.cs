using System.Collections;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The Persona-style moments of a bond (<see cref="Bonds"/>): "BOND RANK UP" with the member's sprite and five rank
    /// pips, and the Soul Card reveal when they join. Both dim the world, pop in, and wait for Enter / Space / F / a click
    /// (or a few seconds). Words come from UI.txt (bond.*).
    /// </summary>
    public static class BondRankUp
    {
        static readonly Color Wash = new Color(0.03f, 0.025f, 0.03f, 0.78f);

        /// <summary>"BOND RANK UP": the member, their rank out of <see cref="Bonds.MaxRank"/>.</summary>
        public static IEnumerator Show(RectTransform canvas, CharacterDefinition member, int rank)
        {
            var rt = Overlay(canvas, "BondRankUp");
            var center = new Vector2(0.5f, 0.5f);
            var accent = member != null ? Color.Lerp(member.color, Color.white, 0.25f) : InkStyle.GoldBright;

            var card = InkStyle.Panel(rt, "Card", new Color(0.07f, 0.06f, 0.07f, 0.96f));
            card.rectTransform.Place(center, center, new Vector2(0f, 10f), new Vector2(760f, 420f));
            InkStyle.Frame(card.rectTransform, InkStyle.GoldBright, 3f);
            var stripe = UIFactory.CreateImage(card.transform, "Stripe", accent);
            stripe.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(10f, 420f));

            var soul = CardArt.SoulSprite(member);
            if (soul != null)
            {
                var face = UIFactory.CreateImage(card.transform, "Sprite", Color.white);
                face.sprite = soul;
                face.preserveAspect = true;
                face.rectTransform.Place(new Vector2(0f, 0.5f), center, new Vector2(170f, 0f), new Vector2(240f, 300f));
            }
            var title = InkStyle.Title(card.transform, "Title", UIText.Get("bond.rank_up"), 64, InkStyle.GoldBright, TextAnchor.MiddleLeft);
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(320f, -50f), new Vector2(420f, 80f));
            var name = InkStyle.Title(card.transform, "Name", member != null ? member.DisplayName : "", 48, InkStyle.Paper, TextAnchor.MiddleLeft);
            name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(320f, -140f), new Vector2(420f, 60f));
            var label = UIFactory.CreateText(card.transform, "Rank", UIText.Get("bond.rank", "n", rank.ToString()), 32, InkStyle.PaperDim, TextAnchor.MiddleLeft);
            label.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(320f, -205f), new Vector2(420f, 44f));

            // five pips, the reached ones lit in the member's colour; the new one pops last
            Image newest = null;
            for (int i = 0; i < Bonds.MaxRank; i++)
            {
                var pip = UIFactory.CreateImage(card.transform, "Pip" + i, i < rank ? accent : new Color(1f, 1f, 1f, 0.12f));
                pip.rectTransform.Place(new Vector2(0f, 0f), center, new Vector2(345f + i * 64f, 110f), new Vector2(40f, 40f));
                pip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                if (i == rank - 1) newest = pip;
            }
            yield return Run(rt, card.rectTransform, newest != null ? newest.rectTransform : null);
        }

        /// <summary>The member's Soul Card, face up, with "&lt;name&gt;'s Soul Card": they join the party.</summary>
        public static IEnumerator SoulCard(RectTransform canvas, CharacterDefinition member)
        {
            var rt = Overlay(canvas, "SoulCard");
            var center = new Vector2(0.5f, 0.5f);
            var holder = UIFactory.CreateRect("Card", rt).Place(center, center, new Vector2(0f, 40f), CardView.Size * 2.2f);
            if (member != null && member.ritual != null)
            {
                CardView.BuildFace(holder, new CardInstance(member.ritual, new BattleUnit(member, Team.Player), 1), 2.2f, true);
                foreach (var g in holder.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            }
            var title = InkStyle.Title(rt, "Title", UIText.Get("bond.soul_card", "name", member != null ? member.DisplayName : ""),
                56, InkStyle.GoldBright, TextAnchor.MiddleCenter);
            title.rectTransform.Place(center, center, new Vector2(0f, -CardView.Size.y * 1.1f - 30f), new Vector2(1400f, 80f));
            var sub = UIFactory.CreateText(rt, "Sub", UIText.Get("bond.joined", "name", member != null ? member.DisplayName : ""), 32,
                InkStyle.Paper, TextAnchor.MiddleCenter);
            sub.rectTransform.Place(center, center, new Vector2(0f, -CardView.Size.y * 1.1f - 90f), new Vector2(1400f, 50f));
            yield return Run(rt, holder, null);
        }

        static RectTransform Overlay(RectTransform canvas, string name)
        {
            var rt = UIFactory.CreateRect(name, canvas).Fill();
            rt.SetAsLastSibling();
            var bg = UIFactory.CreateImage(rt, "Wash", Wash, true);
            bg.rectTransform.Fill();
            rt.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            return rt;
        }

        /// <summary>Fade in, pop the card (and the newest pip), a gold flash, then wait for a press or ~4 s; fade out.</summary>
        static IEnumerator Run(RectTransform rt, RectTransform card, RectTransform pop)
        {
            var group = rt.GetComponent<CanvasGroup>();
            var flash = UIFactory.CreateImage(rt, "Flash", new Color(1f, 0.9f, 0.6f, 0f));
            flash.rectTransform.Fill();
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                float e = Mathf.Clamp01(t / 0.35f);
                group.alpha = e;
                float s = 1f + 0.18f * Mathf.Sin(e * Mathf.PI) * (1f - e) + (1f - e) * 0.2f;
                card.localScale = Vector3.one * s;
                yield return null;
            }
            card.localScale = Vector3.one;
            for (t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                float e = t / 0.3f;
                flash.color = new Color(1f, 0.9f, 0.6f, 0.45f * (1f - e));
                if (pop != null) pop.localScale = Vector3.one * (1f + 0.6f * (1f - e));
                yield return null;
            }
            flash.color = Color.clear;
            if (pop != null) pop.localScale = Vector3.one;
            yield return null;   // the key that opened this mustn't also close it
            for (t = 0f; t < 4f; t += Time.unscaledDeltaTime)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F)
                    || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0)) break;
                yield return null;
            }
            for (t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / 0.25f;
                yield return null;
            }
            Object.Destroy(rt.gameObject);
        }
    }
}
