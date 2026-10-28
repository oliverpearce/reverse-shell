using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// CARDS > TILT
    /// Leans a card in 3D (like Pokémon TCG Pocket). The canvases are Screen Space Overlay, which has no perspective, so
    /// every Graphic in the card gets a <see cref="PerspectiveTilt"/> that projects its vertices: the near edge grows
    /// and the far edge shrinks. Two ways to drive it:
    ///   zoomed cards   drag to tilt (a <see cref="CardTiltDrag"/> on the input object) or arrow keys; let go and it
    ///                  springs back flat with a small wobble
    ///   hand cards     while <see cref="HoverWhen"/> is true it leans gently toward the mouse
    /// <see cref="Angles"/> (yaw, pitch in degrees) also drives the shine (<see cref="CardHolo"/>). Unscaled time.
    /// The small icons (<see cref="Layered"/>: the type icon, rank pips, stars) float <see cref="Lift"/> in front of the
    /// card, so they slide against it (parallax). Everything else, words included, lies on the card's surface and turns
    /// with it (no copies).
    /// </summary>
    public class CardTilt : MonoBehaviour
    {
        /// <summary>The card face that tilts (its centre is the pivot).</summary>
        public RectTransform Card;
        /// <summary>A part under the card that stays flat (the zoomed card's text panel).</summary>
        public Transform Exclude;
        /// <summary>Anything under an object with this name stays flat too (for parts rebuilt later, like the
        /// zoomed card's "DetailPanel").</summary>
        public string ExcludeName;
        public float MaxAngle = 25f;
        /// <summary>Arrow keys tilt it (zoomed cards).</summary>
        public bool Keyboard;
        /// <summary>Hover mode: lean toward the mouse while this is true (hand cards).</summary>
        public Func<bool> HoverWhen;
        public float HoverMax = 8f;

        const float Stiffness = 140f, Damping = 11f;   // the spring back to flat (a little wobble)
        /// <summary>How far in front of the card the words and icons float, as a share of the card's height.</summary>
        const float Lift = 0.12f;
        const float DragDegreesPerUnit = 0.12f;          // canvas units of drag per degree

        Vector2 angle, velocity, dragTarget;
        bool held;
        readonly List<PerspectiveTilt> effects = new List<PerspectiveTilt>();
        int scannedChildren = -1;
        Vector2 lastApplied = new Vector2(999f, 999f);

        /// <summary>Yaw (x) and pitch (y) in degrees; (0, 0) is flat.</summary>
        public Vector2 Angles { get { return angle; } }
        /// <summary>How far it's tilted, 0 (flat) to 1 (the max).</summary>
        public float Amount { get { return Mathf.Clamp01(angle.magnitude / Mathf.Max(1f, MaxAngle)); } }
        public bool Tilted { get { return angle.sqrMagnitude > 0.0004f; } }

        public static CardTilt Add(RectTransform card, Transform exclude = null)
        {
            var tilt = card.gameObject.GetComponent<CardTilt>();   // (no ?? on Unity objects: the editor's fake null)
            if (tilt == null) tilt = card.gameObject.AddComponent<CardTilt>();
            tilt.Card = card;
            tilt.Exclude = exclude;
            return tilt;
        }

        public void BeginDrag() { held = true; dragTarget = angle; }

        public void Drag(Vector2 canvasDelta)
        {
            // Dragging right brings the right edge toward you; dragging up brings the top toward you.
            dragTarget += new Vector2(canvasDelta.x, -canvasDelta.y) * DragDegreesPerUnit;
            dragTarget = Vector2.ClampMagnitude(dragTarget, MaxAngle);
        }

        public void EndDrag() { held = false; }

        void LateUpdate()
        {
            if (Card == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Vector2 target = Vector2.zero;
            bool follow = false;
            if (held) { target = dragTarget; follow = true; }
            else if (Keyboard && UIKeys.KeyboardOn && KeyTilt() != Vector2.zero) { target = KeyTilt() * MaxAngle; follow = true; }
            else if (HoverWhen != null && HoverWhen()) { target = MouseLean() * HoverMax; follow = true; }

            if (follow)
            {
                // Follow quickly while held or hovered.
                angle = Vector2.Lerp(angle, target, 1f - Mathf.Exp(-18f * dt));
                velocity = Vector2.zero;
            }
            else
            {
                // Spring back to flat.
                velocity += (-angle * Stiffness - velocity * Damping) * dt;
                angle += velocity * dt;
                if (angle.sqrMagnitude < 0.0004f && velocity.sqrMagnitude < 0.01f) { angle = Vector2.zero; velocity = Vector2.zero; }
            }
            Apply();
        }

        static Vector2 KeyTilt()
        {
            float x = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float y = (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f);
            return new Vector2(x, y) * 0.8f;
        }

        /// <summary>Where the mouse is over the card, -1..1 on each axis (0 = the centre).</summary>
        Vector2 MouseLean()
        {
            var canvas = Card.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Card, Input.mousePosition, cam, out local)) return Vector2.zero;
            var r = Card.rect;
            var lean = new Vector2((local.x - r.center.x) / (r.width / 2f), -(local.y - r.center.y) / (r.height / 2f));
            return Vector2.ClampMagnitude(lean, 1f);
        }

        /// <summary>Makes sure every Graphic in the card has its projection (cards rebuild their faces), then redraws
        /// them if the angle moved.</summary>
        void Apply()
        {
            // Rescan when the card's parts changed: a different count, or a part it was turning is gone (the face was
            // rebuilt, e.g. scrolling to another rank), so the new face always turns too.
            int children = Card.GetComponentsInChildren<Transform>(true).Length;
            bool stale = false;
            foreach (var e in effects) if (e == null) { stale = true; break; }
            if (children != scannedChildren || stale)
            {
                scannedChildren = children;
                effects.RemoveAll(e => e == null);
                foreach (var g in Card.GetComponentsInChildren<Graphic>(true))
                {
                    if (Exclude != null && g.transform.IsChildOf(Exclude)) continue;
                    if (Excluded(g.transform)) continue;
                    var e = g.GetComponent<PerspectiveTilt>();
                    if (e == null) e = g.gameObject.AddComponent<PerspectiveTilt>();
                    e.Source = this;
                    bool floats = Layered(g);
                    e.Lift = floats ? Lift : 0f;
                    e.Ghost = floats;
                    if (!effects.Contains(e)) effects.Add(e);
                }
                lastApplied = new Vector2(999f, 999f);
            }
            if ((angle - lastApplied).sqrMagnitude < 0.0001f) return;
            lastApplied = angle;
            foreach (var e in effects) if (e != null) e.Refresh();
        }

        /// <summary>The parts that float in front of a tilted card: its small icons (the type icon, rank pips, stars).
        /// Words stay on the surface.</summary>
        static bool Layered(Graphic g)
        {
            string n = g.gameObject.name;
            return n == "TypeIcon" || n.StartsWith("Rank") || n == "Pip" || n.StartsWith("Star") || n.StartsWith("Arcana");
        }

        bool Excluded(Transform t)
        {
            if (string.IsNullOrEmpty(ExcludeName)) return false;
            for (; t != null && t != Card; t = t.parent)
                if (t.name == ExcludeName) return true;
            return false;
        }

        /// <summary>A vertex of <paramref name="graphic"/> (in its local space) seen through the tilt.
        /// <paramref name="lift"/>: how far in front of the card it floats (a share of the card's height; the ghosts).</summary>
        public Vector3 Project(RectTransform graphic, Vector3 local, float lift = 0f)
        {
            var r = Card.rect;
            Vector3 c = Card.InverseTransformPoint(graphic.TransformPoint(local)) - (Vector3)r.center;
            Vector3 turned = Quaternion.Euler(angle.y, angle.x, 0f) * new Vector3(c.x, c.y, -lift * r.height);
            float distance = r.height * 3.2f;   // the virtual camera's distance: lower = stronger perspective
            float s = distance / Mathf.Max(1f, distance + turned.z);
            var projected = new Vector3(turned.x * s, turned.y * s, 0f) + (Vector3)r.center;
            return graphic.InverseTransformPoint(Card.TransformPoint(projected));
        }
    }

    /// <summary>CARDS > TILT > PROJECTION. Bends one Graphic's mesh through its card's <see cref="CardTilt"/>.</summary>
    public class PerspectiveTilt : BaseMeshEffect
    {
        public CardTilt Source;
        /// <summary>A ghost layer floats this far in front of the card (share of its height).</summary>
        public float Lift;
        /// <summary>Floating: words keep their shape (they only move and scale), so they stay readable.</summary>
        public bool Ghost;

        public void Refresh()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || Source == null || Source.Card == null || !Source.Tilted) return;
            var rt = (RectTransform)transform;
            var v = new UIVertex();
            if (graphic is Text && Ghost)
            {
                // The ghost of a word never skews: its centre goes where the tilt (and its lift) puts it, and the whole
                // line just moves and grows / shrinks there, so it stays readable while the card turns.
                Vector3 centre = rt.rect.center;
                Vector3 moved = Source.Project(rt, centre, Lift);
                Vector3 side = Source.Project(rt, centre + Vector3.right * Mathf.Max(1f, rt.rect.width * 0.5f), Lift);
                float scale = (side - moved).magnitude / Mathf.Max(1f, rt.rect.width * 0.5f);
                for (int i = 0; i < vh.currentVertCount; i++)
                {
                    vh.PopulateUIVertex(ref v, i);
                    v.position = moved + (v.position - centre) * scale;
                    vh.SetUIVertex(v, i);
                }
                return;
            }
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.position = Source.Project(rt, v.position, Lift);
                vh.SetUIVertex(v, i);
            }
        }
    }

    /// <summary>CARDS > TILT > DRAG. Put on the object that receives the pointer (a zoomed card's holder): dragging
    /// tilts the card.</summary>
    public class CardTiltDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public CardTilt Tilt;

        public static CardTiltDrag Add(GameObject input, CardTilt tilt)
        {
            var drag = input.GetComponent<CardTiltDrag>();
            if (drag == null) drag = input.AddComponent<CardTiltDrag>();
            drag.Tilt = tilt;
            if (input.GetComponent<Graphic>() == null)
            {
                var hit = input.AddComponent<Image>();   // something to grab: an invisible hit area over the card
                hit.color = new Color(0f, 0f, 0f, 0f);
            }
            return drag;
        }

        public void OnBeginDrag(PointerEventData e) { if (Tilt != null) Tilt.BeginDrag(); }

        public void OnDrag(PointerEventData e)
        {
            if (Tilt == null) return;
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            Tilt.Drag(e.delta / Mathf.Max(0.01f, scale));
        }

        public void OnEndDrag(PointerEventData e) { if (Tilt != null) Tilt.EndDrag(); }
    }
}
