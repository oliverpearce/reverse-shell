using System;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// CARDS > ZOOM
    /// Any card face, big and centred over a dim backdrop: drag (or the arrows) to tilt it in 3D with the holo shine
    /// (<see cref="CardTilt"/>, <see cref="CardHolo"/>); click outside it or Esc closes. Used for cards that aren't
    /// ability cards (Sun / Moon / Star) and for pack reveals; ability and Soul cards in menus use CardDetailPopup,
    /// which has the same tilt plus their text. Unscaled time.
    /// </summary>
    public class CardZoom : MonoBehaviour
    {
        const float Scale = 2f;   // 10x card pixels, like CardDetailPopup

        static CardZoom current;
        public static bool IsOpen { get { return current != null; } }
        /// <summary>The frame it last closed (so the Esc that closed it isn't read again underneath).</summary>
        public static int ClosedFrame { get; private set; }

        Action onClosed;
        int openedFrame;

        /// <param name="buildFace">Draws the card into the rect it's given (CardView.Size * Scale).</param>
        /// <param name="shape">The card's art when it's narrower than a card (Soul tarots, Sun / Moon / Star), so the shine
        /// stays on it; null for ordinary cards.</param>
        /// <param name="type">An ability card's type, for its typed shine (null = the plain one).</param>
        public static CardZoom Open(RectTransform canvasRoot, Action<RectTransform, float> buildFace, CardHolo.Kind holo,
            Action onClosed = null, Sprite shape = null, CardKind? type = null)
        {
            if (current != null) current.Close();
            var rt = UIFactory.CreateRect("CardZoom", canvasRoot).Fill();
            rt.SetAsLastSibling();
            var zoom = rt.gameObject.AddComponent<CardZoom>();
            current = zoom;
            zoom.onClosed = onClosed;
            zoom.openedFrame = Time.frameCount;

            var dim = UIFactory.CreateImage(rt, "Dim", new Color(0.03f, 0.028f, 0.025f, 0.8f), true);
            dim.rectTransform.Fill();
            var close = dim.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(zoom.Close);

            var holder = UIFactory.CreateRect("Card", rt).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), CardView.Size * Scale);
            var face = UIFactory.CreateRect("Face", holder).Fill();
            buildFace(face, Scale);
            foreach (var g in face.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            var tilt = CardTilt.Add(face);
            tilt.ExcludeName = "DetailPanel";   // the card's text panel beside it never turns
            tilt.Keyboard = true;
            CardTiltDrag.Add(holder.gameObject, tilt);
            CardHolo.Add(face, tilt, holo, shape, type);

            var hint = UIFactory.CreateText(rt, "Hint", UIText.Get("card.hint.tilt"), 30, InkStyle.Paper, TextAnchor.MiddleCenter);
            hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1200f, 50f));
            holder.localScale = Vector3.one * 0.6f;
            zoom.StartCoroutine(Unscaled(0.18f, p => holder.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, Tween.OutBack(p))));
            return zoom;
        }

        /// <summary>Runs <paramref name="step"/> from 0 to 1 over real time (menus can sit over a paused world).</summary>
        public static System.Collections.IEnumerator Unscaled(float seconds, Action<float> step)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                step(Mathf.Clamp01(t / seconds));
                yield return null;
            }
            step(1f);
        }

        void Update()
        {
            if (Time.frameCount != openedFrame && UIKeys.Back) Close();
        }

        public void Close()
        {
            if (current == this) current = null;
            ClosedFrame = Time.frameCount;
            var then = onClosed;
            onClosed = null;
            Destroy(gameObject);
            if (then != null) then();
        }
    }

    /// <summary>
    /// CARDS > ARCANA FACES
    /// The Sun, Moon and Star cards in the same card format as every other card (CardView.BuildSimpleFace): an orange
    /// swirl, the card frame, the user's Raven Fantasy Icons pick in the centre window (Resources/Cards/ArcanaIcon_Sun.png
    /// etc., copied by Tools/CardForge/collection_art.py) and the name on the plate.
    /// </summary>
    public static class ArcanaCards
    {
        public static readonly Color Orange = new Color(1f, 0.6f, 0.2f);

        public static Sprite Art(CardBinding kind)
        {
            return kind == CardBinding.None ? null : CardArt.Picture("Cards/ArcanaIcon_" + kind);
        }

        public static string Name(CardBinding kind)
        {
            return UIText.Get("arcana." + kind.ToString().ToLowerInvariant());
        }

        /// <summary>Draws the card into <paramref name="root"/> (sized CardView.Size * s).</summary>
        public static void BuildFace(RectTransform root, CardBinding kind, float s)
        {
            CardView.BuildSimpleFace(root, Art(kind), Orange, Name(kind), s, false);
        }

        /// <summary>The blank card's pearl-silver swirl.</summary>
        public static readonly Color Pearl = new Color(0.86f, 0.9f, 0.98f);

        /// <summary>A blank card: a Sun / Moon / Star card in pearl silver with nothing in the middle (it can become any
        /// card). Give it <see cref="CardHolo.Kind.Blank"/>'s shine.</summary>
        public static void BuildBlankFace(RectTransform root, float s)
        {
            CardView.BuildSimpleFace(root, null, Pearl, UIText.Get("pack.blank_card"), s, false);
        }
    }
}
