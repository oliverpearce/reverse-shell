using System;
using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The overworld Esc menu, played out as physical cards. Esc: like the battle's hold-to-inspect, the view zooms in
    /// quickly (1.5x, eased, about a quarter second) while the HUD fades and the player kneels on one knee (the "sit"
    /// clip). Where the mat (<see cref="PauseMat"/>) goes depends on the ground beside them:
    ///   open to the right   the player ends up on the left of the screen (<see cref="PlayerViewX"/>), facing right; the
    ///                       mat rolls out on the ground to their right from north to south, then the big mat rolls
    ///                       across the screen (left to right) from where the ground mat starts to the right edge.
    ///   a block to the right the player ends up top-centre; the mat rolls out on the ground just south of them, west to
    ///                       east, then the big mat rolls across the screen left to right.
    /// The big mat starts <see cref="MatDelay"/> seconds into the ground mat's roll. Then the menu cards (the same
    /// pixel cards as battle, Tools/CardForge) are dealt onto it, slightly sprawled and overlapping in a rough
    /// horizontal line: Quit, Save, Settings (hovered when it opens), Party, Units, Items, Resonance. There is no Resume card:
    /// Esc resumes. Quit asks first.
    /// Left/Right or A/D (or the mouse) moves the hover; the hovered card straightens, rises and grows. Enter / F /
    /// Space / a click plays it; Esc resumes, which plays it all backwards (cards lift off, the big mat rolls back up
    /// the way it came, then the ground mat, then the player stands and the view eases back out). Esc during the opening skips to the cards.
    /// Party opens the formation, Units the character details, Resonance the protagonist's elements
    /// (<see cref="ResonanceScreen"/>; only after Chapter One or in Sandbox Mode); the view stays zoomed (the player
    /// kneeling) meanwhile, and on the way back the mats roll out and the cards are dealt again. Save asks which slot to
    /// save to; Settings has Fullscreen, Input, Graphics, Tutorials (the guide, <see cref="GuideBook"/>) and Enter / Leave Sandbox
    /// Mode (a <see cref="RowMenu"/> over the cards). Quit
    /// saves and goes back to the title screen.
    /// </summary>
    public class PauseCards : MonoBehaviour
    {
        const float CardScale = 1.3f;              // 6.5x card pixels, so the names read easily (fitted to the mat below)
        const float Gap = -36f;                    // the cards overlap a little, like a hand spread on a table
        const float SelectedScale = 1.12f, SelectedLift = 36f;
        const float RowY = -20f;
        const int DefaultIndex = 2;                // Settings

        // The opening, in seconds (unscaled). The zoom matches the battle inspect's 1.5x (camera distance / 1.5).
        const float ZoomIn = 1f / 1.5f, ZoomTime = 0.28f, WorldRoll = 0.5f, MatRoll = 0.45f, DealStep = 0.05f, DealTime = 0.28f;
        /// <summary>The big mat starts rolling this long after the ground mat does (they overlap).</summary>
        const float MatDelay = 0.2f;
        /// <summary>Where the player ends up across the screen (0 = left edge), like a party unit in the battle inspect.</summary>
        const float PlayerViewX = 0.2f;
        /// <summary>The ground mat's near edge, in world units from the player (right of them, or south of them when a
        /// block is to their right).</summary>
        const float MatGap = 0.35f;
        /// <summary>With the mat south of the player, the camera aims this far south so they stand above the big mat.</summary>
        const float AimSouth = 3f;
        const float MatHeight = 560f, MatRightMargin = 30f, MaxMatRight = 900f;

        // The big mat's span (canvas units from the centre), worked out on opening from where the ground mat lands.
        float matLeft = -700f, matWidth = 1400f;
        Vector3 aim;                               // the camera aim's offset from the player when zoomed
        bool southMat;                             // a block to the player's right: the mat goes south of them instead
        bool steady;                               // the zoomed camera would be inside terrain: don't move or zoom it

        class Entry
        {
            public string Name, Icon;
            public Color Color;
            public Action Play;
            public Func<bool> Shown;               // null = always
            public RectTransform Rect;
            public CanvasGroup Group;
            public Text Label;
            public Vector2 Base;                   // its spot on the mat (with its sprawl)
            public float Tilt, SprawlTilt, Lift, Bright = 0.75f;
            public float Deal;                     // 0 = not on the mat yet, 1 = landed
        }

        OverworldBuilder world;
        CharacterMenu units;
        FormationScreen formation;
        ResonanceScreen resonance;
        ItemsScreen items;
        RowMenu pages;                             // Save slots and Settings, over the cards
        readonly List<Entry> cards = new List<Entry>();
        int index = DefaultIndex;
        int openedFrame;
        bool busy;                                 // an animation is playing, or a card was just played
        bool skip;                                 // Esc during the opening: jump to the end
        float rowFit = 1f;                         // the row's scale so every shown card fits

        Image dim;
        CanvasGroup chrome;                        // the title and the key hint
        RectTransform matMask, rollEnd;
        float matReveal;

        bool zoomed;                               // the view is zoomed in on the player (and the HUD hidden)
        PauseMat worldMat;

        public bool IsOpen { get { return gameObject.activeSelf; } }

        /// <summary>The frame it last opened or closed (so the Esc that closes it doesn't reopen it).</summary>
        public int LastToggleFrame { get; private set; }

        public static PauseCards Create(RectTransform parent, OverworldBuilder world, CharacterMenu units, FormationScreen formation,
            ResonanceScreen resonance, ItemsScreen items)
        {
            var rt = UIFactory.CreateRect("PauseCards", parent).Fill();
            var menu = rt.gameObject.AddComponent<PauseCards>();
            menu.world = world;
            menu.units = units;
            menu.formation = formation;
            menu.resonance = resonance;
            menu.items = items;
            menu.pages = RowMenu.Create(parent);
            menu.Build(rt);
            rt.gameObject.SetActive(false);
            return menu;
        }

        void Build(RectTransform root)
        {
            // A light dim: the zoomed-in world (the player on their mat) stays visible behind the cards.
            dim = UIFactory.CreateImage(root, "Dim", new Color(0.05f, 0.045f, 0.04f, 0f), true);
            dim.rectTransform.Fill();

            BuildMat(root);

            var chromeRect = UIFactory.CreateRect("Chrome", root).Fill();
            chrome = chromeRect.gameObject.AddComponent<CanvasGroup>();
            chrome.blocksRaycasts = false;
            var title = InkStyle.Title(chromeRect, "Title", "PAUSED", 72, InkStyle.Paper, TextAnchor.MiddleCenter);
            title.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            // Top-left (the HUD is hidden while paused), so the zoomed-in player standing top-centre stays clear.
            title.alignment = TextAnchor.MiddleLeft;
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -40f), new Vector2(700f, 110f));
            var keys = UIFactory.CreateText(chromeRect, "Keys",
                "<color=#C2A061>← →</color>  Choose      <color=#C2A061>Enter</color>  Select      <color=#C2A061>Esc</color>  Resume",
                28, InkStyle.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            keys.supportRichText = true;
            keys.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            keys.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1600f, 50f));

            // The order matters: OpenNext takes the card's index (3 Party, 4 Units, 5 Items, 6 Resonance) to come back to it.
            cards.Add(new Entry { Name = "Quit", Icon = "Menu_Quit", Color = new Color(0.86f, 0.36f, 0.32f), Play = ConfirmQuit });
            cards.Add(new Entry { Name = "Save", Icon = "Menu_Save", Color = new Color(0.4f, 0.7f, 0.95f), Play = Save });
            cards.Add(new Entry { Name = "Settings", Icon = "Menu_Settings", Color = new Color(0.62f, 0.66f, 0.72f), Play = ShowSettings });
            cards.Add(new Entry { Name = "Party", Icon = "Menu_Party", Color = new Color(0.72f, 0.62f, 0.95f), Play = () => OpenNext(3) });
            cards.Add(new Entry { Name = "Units", Icon = "Menu_Units", Color = new Color(0.5f, 0.82f, 0.62f), Play = () => OpenNext(4) });
            cards.Add(new Entry { Name = "Items", Icon = "Menu_Items", Color = new Color(0.36f, 0.72f, 0.78f), Play = () => OpenNext(5) });
            cards.Add(new Entry { Name = "Resonance", Icon = "Menu_Resonance", Color = AfflatusInfo.Color(Afflatus.Volt), Play = () => OpenNext(6),
                                  Shown = () => WorldSession.ResonanceMenuUnlocked });

            var size = CardView.Size * CardScale;
            for (int i = 0; i < cards.Count; i++)
            {
                var e = cards[i];
                e.Rect = UIFactory.CreateRect("Card_" + e.Name, root);
                e.Rect.sizeDelta = size;
                e.Group = e.Rect.gameObject.AddComponent<CanvasGroup>();
                e.Label = BuildFace(e.Rect, e, CardScale);
                if (e.Name == "Resonance") CardSparkles.Add(e.Rect, e.Color);   // the Soul Card glitters
                int captured = i;
                var hit = e.Rect.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);   // on the card itself, so it sits behind the face
                var trigger = e.Rect.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => Select(captured));
                trigger.triggers.Add(enter);
                var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                click.callback.AddListener(_ => { Select(captured); Play(); });
                trigger.triggers.Add(click);

                // A stable sprawl per card, as if dropped by hand: tilted a few degrees and a little off the line.
                var rng = new System.Random(e.Name.GetHashCode() & 0x7fffffff);
                e.SprawlTilt = ((float)rng.NextDouble() * 2f - 1f) * 7f;
                e.Tilt = e.SprawlTilt;
            }
        }

        /// <summary>The big mat: the cloth inside a mask that widens from the left as it unrolls, and the rolled-up end
        /// riding the edge.</summary>
        void BuildMat(RectTransform root)
        {
            var center = new Vector2(0.5f, 0.5f);
            matMask = UIFactory.CreateRect("Mat", root);
            matMask.Place(center, new Vector2(0f, 1f), new Vector2(matLeft, RowY + MatHeight / 2f), new Vector2(0f, MatHeight));
            matMask.gameObject.AddComponent<RectMask2D>();
            var shadow = new GameObject("Shadow", typeof(RectTransform)).AddComponent<Image>();
            shadow.rectTransform.SetParent(matMask, false);
            shadow.color = new Color(0f, 0f, 0f, 0.35f);
            shadow.raycastTarget = false;
            matShadow = shadow.rectTransform;
            matCloth = new GameObject("Cloth", typeof(RectTransform)).AddComponent<RawImage>();
            matCloth.rectTransform.SetParent(matMask, false);
            matCloth.raycastTarget = false;

            var roll = new GameObject("Roll", typeof(RectTransform)).AddComponent<RawImage>();
            roll.rectTransform.SetParent(root, false);
            roll.texture = PauseMat.RollTexture();
            roll.raycastTarget = false;
            rollEnd = roll.rectTransform;
            rollEnd.Place(center, center, new Vector2(matLeft, RowY), new Vector2(72f, MatHeight + 36f));
            SizeMat();
        }

        RectTransform matShadow;
        RawImage matCloth;

        /// <summary>Fits the cloth to the mat's span (a texture as many pixels wide as keeps them square).</summary>
        void SizeMat()
        {
            var topLeft = new Vector2(0f, 1f);
            matShadow.Place(topLeft, topLeft, new Vector2(10f, -12f), new Vector2(matWidth, MatHeight));
            matCloth.rectTransform.Place(topLeft, topLeft, Vector2.zero, new Vector2(matWidth, MatHeight));
            matCloth.texture = PauseMat.ClothTexture(Mathf.Max(24, Mathf.RoundToInt(PauseMat.ClothHeight * matWidth / MatHeight)));
            SetMat(matReveal);
        }

        /// <summary>
        /// Where things go for this screen shape: the camera aim (mat to the right: the player at
        /// <see cref="PlayerViewX"/>; mat to the south: the player above the big mat), and the big mat, which always spans
        /// the whole screen so the cards can be large.
        /// </summary>
        void Frame()
        {
            var rect = ((RectTransform)transform).rect;
            var cam = world.Cam;
            var follow = Follow();
            if (cam == null || follow == null || rect.width <= 0f) return;
            float right = Mathf.Min(rect.width / 2f - MatRightMargin, MaxMatRight);
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float d = follow.distance * ZoomIn;
            aim = southMat
                ? new Vector3(0f, 0f, -AimSouth)
                : new Vector3((0.5f - PlayerViewX) * 2f * d * tanV * cam.aspect, 0f, 0f);
            // If that zoomed view would put the camera inside terrain, it stays where it is (no zoom, no shift).
            steady = ViewBlocked(follow, aim);
            if (steady)
            {
                aim = Vector3.zero;
                d = follow.distance;
            }
            // The big mat always spans the whole screen, so the cards can be large and easy to read (it covers the
            // player; the ground mat still rolls out beside them first).
            matLeft = -right;
            matWidth = right - matLeft;
            matMask.anchoredPosition = new Vector2(matLeft, RowY + MatHeight / 2f);
            SizeMat();
        }

        /// <summary>Would the camera, zoomed in and aimed at <paramref name="aimOffset"/>, sit inside terrain (or see the
        /// player through it)? Samples the line from the player's chest to that camera spot against the map's heights.</summary>
        bool ViewBlocked(OverworldCamera follow, Vector3 aimOffset)
        {
            var map = world.Map;
            var sprite = world.PlayerSprite;
            if (map == null || sprite == null) return false;
            var feet = sprite.transform.position;
            var chest = feet + Vector3.up;
            var camAt = feet + aimOffset + Vector3.up * follow.lookHeight
                        + Quaternion.Euler(follow.pitch, 0f, 0f) * new Vector3(0f, 0f, -follow.distance * ZoomIn);
            float length = Vector3.Distance(chest, camAt);
            for (float s = 0.5f; s <= length; s += 0.25f)
            {
                var p = Vector3.Lerp(chest, camAt, s / length);
                if (map.TopAt(p) > p.y) return true;
            }
            return false;
        }

        /// <summary>The ground mat beside the kneeling player (rolled up; unroll it with Set): to their right, north to
        /// south, or just south of them, west to east, when a block is to their right.</summary>
        PauseMat CreateWorldMat(CharacterSprite sprite)
        {
            var at = sprite.transform.position + (southMat
                ? new Vector3(0.3f, 0f, -(MatGap + PauseMat.Depth / 2f))
                : new Vector3(MatGap + PauseMat.Depth / 2f, 0f, 0f));
            return PauseMat.Create(world.transform, at, !southMat);
        }

        /// <summary>Is there anything solid where the north-south mat would lie, to the player's right?</summary>
        bool BlockedRight(Vector3 feet)
        {
            var map = world.Map;
            if (map == null) return false;
            for (float dx = MatGap; dx <= MatGap + PauseMat.Depth + 0.01f; dx += PauseMat.Depth / 2f)
                for (float dz = -PauseMat.Width / 2f; dz <= PauseMat.Width / 2f + 0.01f; dz += PauseMat.Width / 4f)
                    if (map.IsSolidAt(feet + new Vector3(dx, 0f, dz))) return true;
            return false;
        }

        /// <summary>How far the big mat is unrolled (0..1).</summary>
        void SetMat(float t)
        {
            matReveal = Mathf.Clamp01(t);
            // The roll thins as cloth comes off it and is gone once the mat lies flat.
            float w = Mathf.Lerp(72f, 30f, matReveal) * (matReveal > 0.97f ? (1f - matReveal) / 0.03f : 1f);
            // Always left to right (whichever way the ground mat lies).
            matMask.sizeDelta = new Vector2(matWidth * matReveal, MatHeight);
            rollEnd.sizeDelta = new Vector2(w, MatHeight + Mathf.Lerp(36f, 12f, matReveal));
            rollEnd.anchoredPosition = new Vector2(matLeft + matWidth * matReveal, RowY);
            rollEnd.gameObject.SetActive(w > 0.5f);
        }

        /// <summary>A menu card in the battle cards' style: tinted swirl, frame, the icon, the name on the plate.</summary>
        static Text BuildFace(RectTransform root, Entry e, float s)
        {
            float u = CardArt.HandPixel * s;
            var swirl = UIFactory.CreateImage(root, "Swirl", e.Color);
            swirl.sprite = CardArt.Swirl;
            swirl.rectTransform.Fill();
            var frame = UIFactory.CreateImage(root, "Frame", Color.white);
            frame.sprite = CardArt.Frame;
            frame.rectTransform.Fill();
            var icon = UIFactory.CreateImage(root, "Icon", Color.white);
            icon.sprite = CardArt.Icon(e.Icon);
            icon.preserveAspect = true;
            CardArt.PlacePixels(icon.rectTransform, CardArt.ArtX, CardArt.ArtY, CardArt.ArtSize, CardArt.ArtSize, u);
            var plate = CardArt.NamePlate;
            var name = UIFactory.CreateText(root, "Name", e.Name.ToUpperInvariant(), Mathf.RoundToInt(26 * s), InkStyle.Paper, TextAnchor.MiddleCenter)
                .BestFitCardPlate(Mathf.RoundToInt(8 * s), Mathf.RoundToInt(26 * s));
            CardArt.PlacePixels(name.rectTransform, plate.x, plate.y - 1, plate.width, plate.height + 2, u);
            return name;
        }

        // ------------------------------------------------------------------ Open / close

        /// <summary>Esc from the world: zoom in, kneel, roll out the mats, deal the cards (Settings hovered).</summary>
        public void Open() { Open(DefaultIndex); }

        /// <summary>Opens with <paramref name="select"/> hovered (coming back from Party / Units / Resonance).</summary>
        public void Open(int select)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            openedFrame = LastToggleFrame = Time.frameCount;
            foreach (var e in cards) e.Label.text = e.Name.ToUpperInvariant();
            var player = world.PlayerSprite;
            if (!zoomed && player != null) southMat = BlockedRight(player.transform.position);
            Frame();
            Layout();
            busy = false;
            Select(select);
            if (!IsShown(index)) Select(DefaultIndex);
            StopAllCoroutines();
            StartCoroutine(OpenSequence(!zoomed));
        }

        IEnumerator OpenSequence(bool zoomIn)
        {
            busy = true;
            skip = false;
            foreach (var e in cards)
            {
                e.Deal = 0f;
                e.Lift = 0f;
                e.Tilt = e.SprawlTilt;
            }
            SetMat(0f);
            chrome.alpha = 0f;
            dim.color = new Color(dim.color.r, dim.color.g, dim.color.b, zoomIn ? 0f : 0.35f);

            var sprite = world.PlayerSprite;
            if (!zoomIn)
            {
                // Back from Party / Units / Resonance: everything is still laid out, and whoever leads now (the leader
                // may have changed, which respawns the sprite) is already kneeling.
                if (sprite != null)
                {
                    sprite.SetFacing(southMat ? CharacterSprite.Down : CharacterSprite.Right);
                    sprite.PlayPose("sit", true);
                    if (worldMat == null) worldMat = CreateWorldMat(sprite);
                }
                if (worldMat != null) worldMat.Set(1f);
                SetMat(1f);
                chrome.alpha = 1f;
                foreach (var e in cards)
                {
                    e.Deal = 1f;
                    e.Tilt = e.SprawlTilt;
                }
                busy = false;
                yield break;
            }
            // Like the battle inspect: a quick eased zoom (the player to the left, or above the mat) while the HUD fades
            // out; meanwhile they kneel, facing the mat.
            zoomed = true;
            var follow = Follow();
            if (follow != null) follow.Direct = true;
            if (sprite != null)
            {
                sprite.SetFacing(southMat ? CharacterSprite.Down : CharacterSprite.Right);
                sprite.PlayPose("sit");
            }
            for (float t = 0f; t < ZoomTime && !skip; t += Time.unscaledDeltaTime)
            {
                SetZoom(Tween.OutCubic(t / ZoomTime));
                yield return null;
            }
            SetZoom(1f);

            // The mat rolls out on the ground: to the player's right, north to south, or (a block there) just south of
            // them, west to east. MatDelay into that, the big mat starts rolling across the screen (always left to
            // right), so the two overlap.
            if (worldMat != null) Destroy(worldMat.gameObject);
            worldMat = sprite != null ? CreateWorldMat(sprite) : null;
            float rollTotal = Mathf.Max(WorldRoll, MatDelay + MatRoll);
            for (float t = 0f; t < rollTotal && !skip; t += Time.unscaledDeltaTime)
            {
                if (worldMat != null) worldMat.Set(Ease(t / WorldRoll));
                float m = Mathf.Clamp01((t - MatDelay) / MatRoll);
                SetMat(Ease(m));
                chrome.alpha = m;
                yield return null;
            }
            if (worldMat != null) worldMat.Set(1f);
            SetMat(1f);
            chrome.alpha = 1f;

            // The cards are dealt onto it one after another.
            var shown = new List<Entry>();
            for (int i = 0; i < cards.Count; i++) if (IsShown(i)) shown.Add(cards[i]);
            float total = (shown.Count - 1) * DealStep + DealTime;
            for (float t = 0f; t < total && !skip; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < shown.Count; i++) shown[i].Deal = Mathf.Clamp01((t - i * DealStep) / DealTime);
                yield return null;
            }
            foreach (var e in shown) e.Deal = 1f;
            busy = false;
        }

        /// <summary>Resume: everything backwards, then the world carries on.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            LastToggleFrame = Time.frameCount;
            if (pages != null) pages.Hide();
            StopAllCoroutines();
            StartCoroutine(CloseSequence());
        }

        IEnumerator CloseSequence()
        {
            busy = true;
            const float lift = 0.16f, roll = 0.25f, zoomOut = 0.22f;
            var start = new List<float>();
            foreach (var e in cards) start.Add(e.Deal);
            for (float t = 0f; t < lift; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < cards.Count; i++) cards[i].Deal = Mathf.Min(start[i], 1f - t / lift);
                yield return null;
            }
            foreach (var e in cards) e.Deal = 0f;
            // The big mat rolls back up to the left, then the ground mat.
            float from = matReveal, chromeFrom = chrome.alpha;
            for (float t = 0f; t < roll; t += Time.unscaledDeltaTime)
            {
                SetMat(Mathf.Lerp(from, 0f, Ease(t / roll)));
                chrome.alpha = Mathf.Lerp(chromeFrom, 0f, t / roll);
                yield return null;
            }
            SetMat(0f);
            chrome.alpha = 0f;
            if (worldMat != null) yield return worldMat.RollUp(roll);
            worldMat = null;

            // Then the player stands, the view eases back out and the HUD returns.
            var sprite = world.PlayerSprite;
            if (sprite != null) sprite.ClearPose();
            float zoomFrom = zoomed ? 1f : 0f;
            for (float t = 0f; t < zoomOut; t += Time.unscaledDeltaTime)
            {
                SetZoom(zoomFrom * (1f - Tween.OutCubic(t / zoomOut)));
                yield return null;
            }
            ZoomOutNow();
            LastToggleFrame = Time.frameCount;
            busy = false;
            gameObject.SetActive(false);
        }

        /// <summary>How far the view is zoomed in (0 = normal, 1 = the menu's close-up): camera distance and aim, the
        /// HUD's fade and the dim.</summary>
        void SetZoom(float p)
        {
            p = Mathf.Clamp01(p);
            var follow = Follow();
            if (follow != null)
            {
                follow.zoom = steady ? 1f : Mathf.Lerp(1f, ZoomIn, p);
                follow.aimOffset = aim * p;
            }
            if (world.UI != null) world.UI.SetHudAlpha(1f - p);
            dim.color = new Color(dim.color.r, dim.color.g, dim.color.b, 0.35f * p);
        }

        /// <summary>Back to the normal view at once: standing, no mat, no zoom, the HUD shown, the camera following smoothly.</summary>
        void ZoomOutNow()
        {
            zoomed = false;
            var sprite = world.PlayerSprite;
            if (sprite != null) sprite.ClearPose();
            SetZoom(0f);
            var follow = Follow();
            if (follow != null) follow.Direct = false;
            if (worldMat != null) Destroy(worldMat.gameObject);
            worldMat = null;
        }

        /// <summary>Hides the menu at once: another screen is opening over the world (the view stays zoomed and the mat
        /// is put away; both come back with <see cref="Open(int)"/>), or the scene is about to reload.</summary>
        void HideNow(bool zoomOut)
        {
            StopAllCoroutines();
            LastToggleFrame = Time.frameCount;
            if (pages != null) pages.Hide();
            // Opening Party / Units / Resonance keeps the ground mat out (the player stays kneeling on it).
            if (zoomOut) ZoomOutNow();
            busy = false;
            gameObject.SetActive(false);
        }

        OverworldCamera Follow()
        {
            return world != null && world.Cam != null ? world.Cam.GetComponent<OverworldCamera>() : null;
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Lands past its spot and settles back (the card's little bounce).</summary>
        static float EaseOutBack(float t)
        {
            const float c1 = 1.4f, c3 = c1 + 1f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        bool IsShown(int i) { return cards[i].Shown == null || cards[i].Shown(); }

        /// <summary>Hides cards that aren't unlocked yet and spreads the rest across the mat, each a little off the line.</summary>
        void Layout()
        {
            var size = CardView.Size * CardScale;
            int shown = 0;
            for (int i = 0; i < cards.Count; i++) if (IsShown(i)) shown++;
            float total = shown * size.x + (shown - 1) * Gap;
            rowFit = Mathf.Min(1f, (matWidth - 110f) / total);
            float step = (size.x + Gap) * rowFit, x = matLeft + matWidth / 2f - (shown - 1) * step / 2f;
            for (int i = 0; i < cards.Count; i++)
            {
                var e = cards[i];
                bool on = IsShown(i);
                e.Rect.gameObject.SetActive(on);
                if (!on) continue;
                var rng = new System.Random((e.Name.GetHashCode() & 0x7fffffff) + 17);
                float dx = ((float)rng.NextDouble() * 2f - 1f) * 12f, dy = ((float)rng.NextDouble() * 2f - 1f) * 22f;
                e.Base = new Vector2(x + dx * rowFit, RowY + dy * rowFit);
                e.Rect.anchoredPosition = e.Base;
                e.Rect.localScale = Vector3.one * rowFit;
                x += step;
            }
        }

        void Select(int i)
        {
            i = Mathf.Clamp(i, 0, cards.Count - 1);
            if (IsShown(i)) index = i;
        }

        /// <summary>Moves to the next shown card left (-1) or right (+1).</summary>
        void Step(int dir)
        {
            for (int i = index + dir; i >= 0 && i < cards.Count; i += dir)
                if (IsShown(i))
                {
                    Select(i);
                    return;
                }
        }

        void Play()
        {
            if (busy) return;
            cards[index].Play();
        }

        void Update()
        {
            if (Time.frameCount == openedFrame) return;
            if (busy)
            {
                // Esc during the opening skips straight to the dealt cards.
                if (UIKeys.Back) skip = true;
                return;
            }
            if (pages.IsOpen || pages.ClosedFrame == Time.frameCount) return;   // a page is over the cards
            if (UIKeys.Pressed(KeyCode.LeftArrow) || UIKeys.Pressed(KeyCode.A)) Step(-1);
            if (UIKeys.Pressed(KeyCode.RightArrow) || UIKeys.Pressed(KeyCode.D)) Step(1);
            if (UIKeys.Pressed(KeyCode.Return) || UIKeys.Pressed(KeyCode.KeypadEnter) || UIKeys.Pressed(KeyCode.Space) || UIKeys.Pressed(KeyCode.F))
                Play();
            else if (UIKeys.Back) Close();
        }

        void LateUpdate()
        {
            // The hovered card straightens, rises, grows and is fully lit; the others lie where they fell, a little dimmer.
            // Cards being dealt drop in from above, spinning slightly, and land with a small bounce.
            float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            for (int i = 0; i < cards.Count; i++)
            {
                var e = cards[i];
                if (!e.Rect.gameObject.activeSelf) continue;
                bool on = i == index && e.Deal >= 1f;
                float d = EaseOutBack(e.Deal);
                e.Rect.localScale = Vector3.Lerp(e.Rect.localScale, Vector3.one * rowFit * (on ? SelectedScale : 1f), k);
                e.Lift = Mathf.Lerp(e.Lift, on ? SelectedLift * rowFit : 0f, k);
                e.Tilt = Mathf.Lerp(e.Tilt, on ? 0f : e.SprawlTilt, k);
                e.Rect.anchoredPosition = e.Base + new Vector2(0f, e.Lift + (1f - d) * 180f);
                e.Rect.localRotation = Quaternion.Euler(0f, 0f, e.Tilt + (1f - d) * (e.SprawlTilt * 2f + 10f));
                e.Bright = Mathf.Lerp(e.Bright, on ? 1f : 0.78f, k);
                e.Group.alpha = e.Bright * Mathf.Clamp01(e.Deal * 2f);
                e.Group.blocksRaycasts = e.Deal > 0.5f;
                if (on) e.Rect.SetAsLastSibling();
            }
        }

        // ------------------------------------------------------------------ Save: pick a slot

        void Save() { ShowSlots(); }

        void ShowSlots()
        {
            var rows = new List<RowMenu.Row>();
            for (int slot = 1; slot <= SaveGame.SlotCount; slot++)
            {
                int captured = slot;
                string heading, detail, corner;
                SaveGame.SlotCard(slot, true, out heading, out detail, out corner);
                rows.Add(new RowMenu.Row
                {
                    Label = heading,
                    Detail = detail,
                    Corner = corner,
                    Action = () =>
                    {
                        if (captured != SaveGame.Slot && SaveGame.Exists(captured)) ConfirmOverwrite(captured);
                        else SaveTo(captured);
                    },
                });
            }
            rows.Add(new RowMenu.Row { Label = "Back", Action = pages.Hide });
            pages.Show("SAVE", rows, pages.Hide, SaveGame.Slot - 1);
        }

        void ConfirmOverwrite(int slot)
        {
            pages.Show("SAVE", new List<RowMenu.Row>
            {
                new RowMenu.Row { Label = "Overwrite slot " + slot + "?", Enabled = false },
                new RowMenu.Row { Label = "Yes, save over it", Action = () => SaveTo(slot) },
                new RowMenu.Row { Label = "No", Action = ShowSlots },
            }, ShowSlots, 2);
        }

        /// <summary>Saves to <paramref name="slot"/>, which becomes this game's slot (later saves go there too).</summary>
        void SaveTo(int slot)
        {
            SaveGame.Slot = slot;
            SaveGame.Save();
            pages.Hide();
            StartCoroutine(Flash(cards[1], "SAVED " + slot));
        }

        // ------------------------------------------------------------------ Settings

        void ShowSettings(int select = 0)
        {
            bool inSandbox = WorldSession.InSandbox;
            var rows = new List<RowMenu.Row>
            {
                new RowMenu.Row
                {
                    Label = GameSettings.FullscreenLabel,
                    Action = () => { GameSettings.ToggleFullscreen(); ShowSettings(0); },
                    Help = "Play in fullscreen or in a window. Changes right away.",
                },
                new RowMenu.Row
                {
                    Label = GameSettings.ControlsLabel,
                    Action = () => { GameSettings.CycleControls(); ShowSettings(1); },
                    Help = GameSettings.ControlsHelp,
                },
                new RowMenu.Row
                {
                    Label = GameSettings.GraphicsLabel,
                    Action = () => { GameSettings.ToggleGraphics(); ShowSettings(2); },
                    Help = GameSettings.GraphicsHelp,
                },
                new RowMenu.Row
                {
                    Label = GameSettings.GuideLabel,
                    Action = () => GuideBook.Open(pages.transform),
                    Help = GameSettings.GuideHelp,
                },
                new RowMenu.Row
                {
                    Label = inSandbox ? "Leave Sandbox Mode" : "Enter Sandbox Mode",
                    Action = () =>
                    {
                        HideNow(true);   // the area reloads
                        if (inSandbox) world.LeaveSandbox();
                        else world.EnterSandbox();
                    },
                    Help = inSandbox
                        ? "Go back to exactly where you were before you entered the Sandbox."
                        : "Go to the Sandbox: practice fights with any enemy level, and the whole test crew. " +
                          "Leave Sandbox Mode (here) brings you back to where you are now.",
                },
                new RowMenu.Row { Label = "Back", Action = pages.Hide },
            };
            pages.Show("SETTINGS", rows, pages.Hide, select);
        }

        void ShowSettings() { ShowSettings(0); }

        IEnumerator Flash(Entry e, string text)
        {
            e.Label.text = text;
            yield return Tween.Punch(e.Rect, 0.08f, 0.2f);
            yield return new WaitForSecondsRealtime(0.9f);
            if (e.Label != null) e.Label.text = e.Name.ToUpperInvariant();
        }

        /// <summary>Opens the Party (formation), Units or Resonance screen; closing it comes back here (still zoomed in) with
        /// that card hovered and the cards dealt again.</summary>
        void OpenNext(int card)
        {
            // Next frame, so the key that played this card isn't also read by the next screen.
            busy = true;
            StartCoroutine(OpenNextFrame(card));
        }

        IEnumerator OpenNextFrame(int card)
        {
            yield return null;
            HideNow(false);
            System.Action back = () => Open(card);
            if (card == 3 && formation != null) formation.OpenMenu(back);
            else if (card == 4 && units != null) units.Open(back);
            else if (card == 5 && items != null) items.Open(back);
            else if (card == 6 && resonance != null) resonance.Open(back);
        }

        /// <summary>Quit asks first; Yes saves and goes back to the title screen.</summary>
        void ConfirmQuit()
        {
            pages.Show("QUIT", new List<RowMenu.Row>
            {
                new RowMenu.Row { Label = "Are you sure?", Enabled = false },
                new RowMenu.Row { Label = "Yes, quit to the title screen", Action = Quit },
                new RowMenu.Row { Label = "No", Action = pages.Hide },
            }, pages.Hide, 2);
        }

        /// <summary>Save, then back to the title screen (the area reloads under it).</summary>
        void Quit()
        {
            busy = true;
            SaveGame.Save();
            WorldSession.TitleDone = false;
            WorldSession.HasReturnPoint = false;
            ScreenFade.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
