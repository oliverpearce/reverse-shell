using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// A battlefield unit: animated pixel sprite (or placeholder), HP, Moxie pips, statuses, enemy intent.
    /// A red lock-on reticle marks the targeted enemy and a white ring marks the unit under the cursor
    /// (mouse or arrow keys). Full details live in the inspect screen (hold, or Space).
    /// </summary>
    public class UnitView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        static readonly Color PanelDark = new Color(0.12f, 0.11f, 0.15f, 0.95f);
        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        public const float Width = 220f;
        public const float Height = 380f;

        public BattleUnit Unit { get; private set; }
        public RectTransform Rect { get; private set; }
        public RectTransform Body { get; private set; }
        public Vector2 BodyOrigin { get; private set; }
        public Vector3 Center { get { return Body.position; } }

        /// <summary>Latest statuses / intent shown (event-time values, used by the inspect screen).</summary>
        public List<StatusSnapshot> Statuses { get; private set; }
        public CardInstance Intent { get; private set; }
        public int MoxieCap { get; private set; }

        /// <summary>Where big damage numbers appear: just above the HP bar.</summary>
        public Vector3 DamageAnchor { get { return hpBar.TransformPoint(new Vector3(0f, hpBar.rect.yMax + 38f, 0f)); } }

        BattleView view;
        CanvasGroup group;
        Image portrait, sprite, flash, hpFill, intentIcon;
        Reticle lockOn, focusRing;
        UISpriteAnimator animator;
        RectTransform hpBar;
        RectTransform statusRow;
        readonly List<Image> moxiePips = new List<Image>();
        Coroutine hpRoutine, pressRoutine;
        bool longPressed;

        /// <summary>How long to hold the mouse on a unit to open its full character sheet.</summary>
        public const float LongPressSeconds = 0.45f;

        static Material additiveUI;
        static Sprite shadowSprite;

        public static UnitView Create(Transform parent, BattleUnit unit, BattleView view, int moxieCap)
        {
            var rt = UIFactory.CreateRect(unit.Name, parent);
            rt.sizeDelta = new Vector2(Width, Height);
            var uv = rt.gameObject.AddComponent<UnitView>();
            uv.Build(unit, view, moxieCap);
            return uv;
        }

        void Build(BattleUnit unit, BattleView battleView, int moxieCap)
        {
            Unit = unit;
            view = battleView;
            Rect = (RectTransform)transform;
            group = gameObject.AddComponent<CanvasGroup>();
            Statuses = new List<StatusSnapshot>();
            MoxieCap = moxieCap;

            // Transparent hit area so the whole unit is clickable / hoverable.
            var hit = gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            var def = unit.Definition;
            var top = new Vector2(0.5f, 1f);

            if (unit.Team == Team.Enemy)
            {
                // Slay the Spire-style intent: just an icon above the head (details are in the hover menu).
                intentIcon = UIFactory.CreateImage(transform, "Intent", Color.white);
                intentIcon.rectTransform.Place(top, new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(48f, 48f));
                intentIcon.preserveAspect = true;
                intentIcon.gameObject.SetActive(false);
            }

            Body = UIFactory.CreateRect("Body", transform).Place(top, new Vector2(0.5f, 0.5f), new Vector2(0f, -144f), new Vector2(180f, 180f));
            BodyOrigin = Body.anchoredPosition;

            if (def.animations != null && def.animations.Has(UISpriteAnimator.Idle)) BuildAnimatedBody(unit);
            else BuildPanelBody(unit);

            // Reticles sit in front of the body, centered on the torso (or the panel's middle).
            var reticleAnchor = animator != null ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
            var reticlePos = animator != null ? new Vector2(0f, 92f) : Vector2.zero;
            focusRing = TargetReticle.CreateFocus(Body, reticleAnchor, reticlePos, 176f); // radii are 85% of the texture, leaving room for the glow
            if (unit.Team == Team.Enemy)
                lockOn = TargetReticle.CreateLock(Body, reticleAnchor, reticlePos, 130f);

            string label = unit.Team == Team.Player ? def.DisplayName + "  <size=18>Lv " + unit.Level + "</size>" : def.DisplayName;
            var nameText = UIFactory.CreateText(transform, "Name", label, 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).BestFit(18, 34);
            nameText.supportRichText = true;
            nameText.rectTransform.Place(top, top, new Vector2(0f, -232f), new Vector2(Width + 40f, 42f));
            nameText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);

            var character = def as CharacterDefinition;
            string subtitle = def.afflatus + "  ·  " + def.damageType;
            if (character != null && !string.IsNullOrEmpty(character.archetype)) subtitle = character.archetype + "  ·  " + subtitle;
            var sub = UIFactory.CreateText(transform, "Afflatus", subtitle, 16, new Color(0.75f, 0.75f, 0.8f)).BestFit(10, 16);
            sub.rectTransform.Place(top, top, new Vector2(0f, -276f), new Vector2(Width, 22f));

            var hpBg = UIFactory.CreateImage(transform, "HPBar", PanelDark);
            hpBg.rectTransform.Place(top, top, new Vector2(0f, -302f), new Vector2(200f, 22f));
            hpBar = hpBg.rectTransform;
            hpFill = UIFactory.CreateImage(hpBg.transform, "Fill", unit.Team == Team.Player ? new Color(0.35f, 0.8f, 0.45f) : new Color(0.85f, 0.3f, 0.3f));
            var fillRt = hpFill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;

            if (unit.Team == Team.Player)
            {
                var row = UIFactory.CreateRect("Moxie", transform).Place(top, top, new Vector2(0f, -330f), new Vector2(200f, 14f));
                float gap = 6f;
                float w = (200f - gap * (moxieCap - 1)) / moxieCap;
                for (int i = 0; i < moxieCap; i++)
                {
                    var pip = UIFactory.CreateImage(row, "Pip" + i, PanelDark);
                    pip.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * (w + gap), 0f), new Vector2(w, 14f));
                    moxiePips.Add(pip);
                }
            }

            statusRow = UIFactory.CreateRect("Statuses", transform).Place(top, top, new Vector2(0f, -352f), new Vector2(Width, 26f));
            var layout = statusRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 4f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            SetHP(unit.HP, unit.MaxHP, true);
            SetMoxie(unit.Moxie, moxieCap);
            SetIntent(null);
        }

        /// <summary>Octopath-style: the sprite stands on a soft shadow, no frame. Party faces right, enemies face left.</summary>
        void BuildAnimatedBody(BattleUnit unit)
        {
            var bottom = new Vector2(0.5f, 0f);

            var shadow = UIFactory.CreateImage(Body, "Shadow", new Color(0f, 0f, 0f, 0.55f));
            shadow.sprite = ShadowSprite();
            shadow.rectTransform.Place(bottom, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(120f, 30f));

            // 64 px frames at exactly 3x keep pixels crisp; the LPC feet sit ~3px above the frame bottom.
            sprite = UIFactory.CreateImage(Body, "Sprite", Color.white);
            sprite.rectTransform.Place(bottom, bottom, new Vector2(0f, -15f), new Vector2(192f, 192f));
            flash = UIFactory.CreateImage(Body, "Flash", Color.clear);
            flash.material = AdditiveUI();
            flash.rectTransform.Place(bottom, bottom, new Vector2(0f, -15f), new Vector2(192f, 192f));

            animator = Body.gameObject.AddComponent<UISpriteAnimator>();
            animator.Init(unit.Definition.animations, unit.Team == Team.Player ? Facing.Right : Facing.Left, sprite, flash);

        }

        static readonly Vector2 top = new Vector2(0.5f, 1f);

        /// <summary>Fallback when there's no animation set: colored panel with a legacy sheet frame or the name's initial.</summary>
        void BuildPanelBody(BattleUnit unit)
        {
            var def = unit.Definition;
            portrait = UIFactory.CreateImage(Body, "Portrait", def.color);
            portrait.rectTransform.Fill();
            if (def.portrait != null)
            {
                portrait.sprite = def.portrait;
                portrait.color = Color.white;
                portrait.preserveAspect = true;
            }
            else if (def.spriteSheet != null)
            {
                portrait.color = UIFactory.Darken(def.color, 0.75f);
                var frames = SpriteSheets.Slice(def);
                int row = Mathf.Min(unit.Team == Team.Player ? SpriteSheets.Right : SpriteSheets.Left, frames.GetLength(0) - 1);
                sprite = UIFactory.CreateImage(Body, "Sprite", Color.white);
                sprite.sprite = frames[row, Mathf.Min(1, frames.GetLength(1) - 1)];
                sprite.preserveAspect = true;
                sprite.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(192f, 192f));
            }
            else
            {
                string letter = def.DisplayName.Length > 0 ? def.DisplayName.Substring(0, 1) : "?";
                var initial = UIFactory.CreateText(Body, "Initial", letter, 110,
                    UIFactory.WithAlpha(Color.white, 0.45f), TextAnchor.MiddleCenter, FontStyle.Bold);
                initial.rectTransform.Fill();
            }

            flash = UIFactory.CreateImage(Body, "Flash", new Color(1f, 1f, 1f, 0f));
            flash.rectTransform.Fill();
        }

        void Update()
        {
            if (intentIcon != null && intentIcon.gameObject.activeSelf) // gentle bob, in whole pixels
                intentIcon.rectTransform.anchoredPosition = new Vector2(0f, -14f + Mathf.Round(Mathf.Sin(Time.time * 2.5f) * 1.5f) * 2f);
        }

        // ------------------------------------------------------------------ Input

        public void OnPointerClick(PointerEventData eventData)
        {
            if (longPressed)
            {
                longPressed = false; // the press opened the inspect screen; don't also select the target
                return;
            }
            view.OnUnitClicked(Unit);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            longPressed = false;
            CancelPress();
            pressRoutine = StartCoroutine(PressAndHold());
        }

        public void OnPointerUp(PointerEventData eventData) { CancelPress(); }

        /// <summary>Hold to inspect: the unit swells slightly as feedback, then the character sheet opens.</summary>
        IEnumerator PressAndHold()
        {
            float t = 0f;
            while (t < LongPressSeconds)
            {
                t += Time.unscaledDeltaTime;
                Body.localScale = Vector3.one * (1f + 0.08f * Mathf.Clamp01(t / LongPressSeconds));
                yield return null;
            }
            Body.localScale = Vector3.one;
            pressRoutine = null;
            longPressed = true;
            view.OpenInspect(this);
        }

        void CancelPress()
        {
            if (pressRoutine != null) StopCoroutine(pressRoutine);
            pressRoutine = null;
            if (Body != null) Body.localScale = Vector3.one;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            view.OnUnitHover(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CancelPress();
            view.OnUnitHover(this, false);
        }

        /// <summary>Plays the same "hold" feedback as a long press, then opens the inspect screen (Space key).</summary>
        public void Inspect()
        {
            CancelPress();
            pressRoutine = StartCoroutine(PressAndHold());
        }

        // ------------------------------------------------------------------ Animation

        /// <summary>Plays a clip ("slash", "spellcast"...) if this unit has it; returns its length in seconds.</summary>
        public float PlayAnimation(string clip)
        {
            if (animator == null || !Unit.IsAlive) return 0f;
            return animator.Play(clip) ? animator.Duration(clip) : 0f;
        }

        // ------------------------------------------------------------------ State display

        public void SetHP(int hp, int max, bool instant = false)
        {
            float target = max > 0 ? Mathf.Clamp01((float)hp / max) : 0f;
            var rt = hpFill.rectTransform;
            if (hpRoutine != null) StopCoroutine(hpRoutine);
            if (instant || !isActiveAndEnabled)
            {
                rt.anchorMax = new Vector2(target, 1f);
                return;
            }
            float from = rt.anchorMax.x;
            hpRoutine = StartCoroutine(Tween.Run(0.25f, p => rt.anchorMax = new Vector2(Mathf.Lerp(from, target, p), 1f)));
        }

        public void SetMoxie(int value, int cap)
        {
            bool full = value >= cap;
            for (int i = 0; i < moxiePips.Count; i++)
            {
                Color c = i < value ? (full ? new Color(1f, 0.55f, 0.2f) : Gold) : PanelDark;
                moxiePips[i].color = c;
            }
        }

        public IEnumerator PunchMoxie()
        {
            var routines = new List<Coroutine>();
            foreach (var pip in moxiePips) routines.Add(StartCoroutine(Tween.Punch(pip.transform, 0.35f, 0.2f)));
            foreach (var r in routines) yield return r;
        }

        public void SetIntent(CardInstance intent)
        {
            Intent = intent;
            if (intentIcon == null) return;
            bool show = intent != null && Unit.IsAlive;
            intentIcon.gameObject.SetActive(show);
            if (show) intentIcon.sprite = IntentIcons.For(intent.Definition.kind);
        }

        public void SetStatuses(List<StatusSnapshot> statuses)
        {
            Statuses = new List<StatusSnapshot>(statuses);
            UIFactory.ClearChildren(statusRow);

            foreach (var s in statuses)
            {
                var chip = UIFactory.CreateImage(statusRow, s.Definition.DisplayName, UIFactory.Darken(s.Definition.color, 0.35f));
                chip.rectTransform.sizeDelta = new Vector2(58f, 24f);
                string stacks = s.Stacks > 1 ? "x" + s.Stacks : "";
                var label = UIFactory.CreateText(chip.transform, "Label", s.Definition.shortLabel + stacks + " " + s.Remaining, 13, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                label.rectTransform.Fill(1f);
                var outline = chip.gameObject.AddComponent<Outline>();
                outline.effectColor = s.Definition.isDebuff ? new Color(0.9f, 0.25f, 0.25f) : new Color(0.4f, 0.85f, 1f);
            }
        }

        /// <summary>Red lock-on: this enemy is the current target.</summary>
        public void SetSelected(bool selected)
        {
            if (lockOn != null) lockOn.Show(selected && Unit.IsAlive);
        }

        /// <summary>White ring: the unit under the mouse or the arrow-key cursor.</summary>
        public void SetFocused(bool focused)
        {
            if (focusRing != null) focusRing.Show(focused && Unit.IsAlive);
        }

        public IEnumerator Die()
        {
            SetSelected(false);
            SetFocused(false);
            SetIntent(null);
            SetStatuses(new List<StatusSnapshot>());
            if (animator != null)
            {
                // LPC "hurt" is a collapse; hold the last frame so they stay down.
                animator.Play("hurt", true);
                yield return Tween.Wait(Mathf.Max(0.2f, animator.Duration("hurt")));
            }
            if (portrait != null) StartCoroutine(Tween.ColorTo(portrait, UIFactory.Darken(portrait.color, 0.7f), 0.35f));
            if (sprite != null) StartCoroutine(Tween.ColorTo(sprite, new Color(0.45f, 0.42f, 0.55f), 0.35f));
            yield return Tween.Fade(group, 0.45f, 0.35f);
        }

        /// <summary>Bright flash: on animated sprites it's sprite-shaped (additive overlay), otherwise a panel tint.</summary>
        public IEnumerator Flash(Color color, float duration)
        {
            if (animator != null)
            {
                flash.color = color * 0.9f;
                yield return Tween.ColorTo(flash, Color.clear, duration);
                yield break;
            }
            flash.color = UIFactory.WithAlpha(color, 0.85f);
            yield return Tween.ColorTo(flash, UIFactory.WithAlpha(color, 0f), duration);
        }

        public IEnumerator Shake(float strength, float duration)
        {
            yield return Tween.Shake(Body, BodyOrigin, strength, duration);
        }

        // ------------------------------------------------------------------ Shared resources

        static Material AdditiveUI()
        {
            if (additiveUI == null)
            {
                var shader = Shader.Find("Legacy Shaders/Particles/Additive");
                additiveUI = shader != null ? new Material(shader) : new Material(Shader.Find("UI/Default"));
                if (additiveUI.HasProperty("_TintColor")) additiveUI.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            }
            return additiveUI;
        }

        static Sprite ShadowSprite()
        {
            if (shadowSprite != null) return shadowSprite;
            const int w = 32, h = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - w / 2f) / (w / 2f), dy = (y + 0.5f - h / 2f) / (h / 2f);
                    float d = dx * dx + dy * dy;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, d <= 1f ? (d < 0.45f ? 1f : 0.6f) : 0f));
                }
            tex.Apply();
            shadowSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
            return shadowSprite;
        }

        public static string KindLabel(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Attack: return "Attack";
                case CardKind.Buff: return "Buff";
                case CardKind.Debuff: return "Debuff";
                default: return "Heal";
            }
        }

        public static Color KindColor(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Attack: return new Color(0.9f, 0.35f, 0.3f);
                case CardKind.Buff: return new Color(0.95f, 0.75f, 0.3f);
                case CardKind.Debuff: return new Color(0.65f, 0.4f, 0.9f);
                default: return new Color(0.35f, 0.85f, 0.5f);
            }
        }
    }
}
