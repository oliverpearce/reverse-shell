using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// A battlefield unit: animated pixel sprite (or placeholder), HP, Ult pips beside the unit-type diamond, statuses,
    /// and the enemy intent card.
    /// A red lock-on reticle marks the targeted enemy and a white ring marks the unit under the cursor
    /// (mouse or arrow keys). Full details live in the inspect screen (hold, or Space).
    /// </summary>
    public class UnitView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        static readonly Color PanelDark = new Color(0.12f, 0.11f, 0.15f, 0.95f);
        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        public const float Width = 220f;
        /// <summary>Battle sprite size: 64 px frames at 6x (the knight-based sprites are about 38 px tall).</summary>
        const float SpriteSize = 384f;
        /// <summary>A boss is drawn this much bigger.</summary>
        const float BossScale = 1.5f;
        /// <summary>The intent card floats just above the sprite's head: this high for an ordinary enemy, raised for a
        /// boss by the extra height of its head (the figure's head is about 5/8 of the way up its frame).</summary>
        const float IntentY = 64f, HeadShare = 0.625f;
        float intentY = IntentY;
        const float IntentSize = 96f;
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

        /// <summary>Above the head (for enemies, above the intent card): where status names float up.</summary>
        public Vector3 HeadAnchor { get { var n = nameText.rectTransform; return n.TransformPoint(new Vector3(0f, n.rect.yMin, 0f)); } }

        BattleView view;
        CanvasGroup group;
        Image portrait, sprite, flash, intentIcon;
        SlantBar hpFill;
        Text nameText;   // shown on hover only
        Reticle lockOn, focusRing, targetPreview;
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
            if (unit.IsBoss) intentY = IntentY + SpriteSize * (BossScale - 1f) * HeadShare;

            if (unit.Team == Team.Enemy)
            {
                // Intent: a small card above the head showing what the enemy will play: its type icon on a cream,
                // silver or gold card for rank I / II / III (details are in the inspect screen).
                intentIcon = UIFactory.CreateImage(transform, "Intent", Color.white);
                intentIcon.rectTransform.Place(top, new Vector2(0.5f, 0.5f), new Vector2(0f, intentY), new Vector2(IntentSize, IntentSize));
                intentIcon.preserveAspect = true;
                intentIcon.gameObject.SetActive(false);
            }

            Body = UIFactory.CreateRect("Body", transform).Place(top, new Vector2(0.5f, 0.5f), new Vector2(0f, -144f), new Vector2(180f, 180f));
            BodyOrigin = Body.anchoredPosition;

            if (def.animations != null && def.animations.Has(UISpriteAnimator.Idle)) BuildAnimatedBody(unit);
            else BuildPanelBody(unit);
            if (intentIcon != null) intentIcon.transform.SetAsLastSibling();   // never behind the sprite

            // Reticles sit in front of the body, centered on the torso (or the panel's middle).
            var reticleAnchor = animator != null ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
            var reticlePos = animator != null ? new Vector2(0f, 122f) : Vector2.zero;
            var orange = InkStyle.TargetOrange;
            var orangeGlow = orange;
            orangeGlow.a = 0.85f;
            targetPreview = TargetReticle.CreateFocus(Body, reticleAnchor, reticlePos, 236f, orange, orangeGlow, "TargetPreview");
            focusRing = TargetReticle.CreateFocus(Body, reticleAnchor, reticlePos, 210f); // radii are 85% of the texture, leaving room for the glow
            if (unit.Team == Team.Enemy)
                lockOn = TargetReticle.CreateLock(Body, reticleAnchor, reticlePos, 156f);

            // A slim nameplate: the HP bar and Ult pips (and small status icons). The name only shows on hover.
            nameText = InkStyle.Title(transform, "Name", def.DisplayName, 34, InkStyle.Paper, TextAnchor.MiddleCenter).BestFit(18, 34);
            // Above the head (for enemies, above the floating intent card).
            float nameY = unit.Team == Team.Enemy ? intentY + IntentSize / 2f + 24f : 18f;
            nameText.rectTransform.Place(top, new Vector2(0.5f, 0f), new Vector2(0f, nameY), new Vector2(Width + 80f, 42f));
            var nameShadow = nameText.gameObject.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            nameShadow.effectDistance = new Vector2(2f, -2f);
            nameText.enabled = false;

            // Reverse: 1999-style HP bar: a slanted parallelogram (dark backing with a thin rim, coloured fill).
            hpBar = UIFactory.CreateRect("HPBar", transform).Place(top, top, new Vector2(0f, -300f), new Vector2(186f, 14f));
            SlantBar.Create(hpBar, "Back", PanelDark, HpSlant, 2f);
            hpFill = SlantBar.Create(hpBar, "Fill", unit.Team == Team.Player ? new Color(0.35f, 0.8f, 0.45f) : new Color(0.85f, 0.3f, 0.3f), HpSlant);
            if (unit.IsBoss)
            {
                var boss = InkStyle.Title(transform, "Boss", UIText.Get("common.boss"), 34, InkStyle.Crimson, TextAnchor.LowerCenter);
                boss.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
                boss.rectTransform.Place(top, new Vector2(0.5f, 0f), new Vector2(0f, -296f), new Vector2(Width, 48f));
            }

            // Ult points right under the HP bar (enemies with an Ult show theirs too).
            if (unit.Team == Team.Player || (unit.Enemy != null && unit.Enemy.ritual != null))
            {
                // Big pips (no backing): solid dark when empty, gold when earned, orange and pulsing when the Ult is ready.
                const float pipSize = 32f, gap = 4f;
                float rowW = moxieCap * pipSize + (moxieCap - 1) * gap;
                var row = UIFactory.CreateRect("Ult", transform).Place(top, top, new Vector2(0f, -318f), new Vector2(rowW, pipSize));
                for (int i = 0; i < moxieCap; i++)
                {
                    var pip = UIFactory.CreateImage(row, "Pip" + i, Color.white);
                    pip.sprite = CardIcons.PipEmpty;
                    pip.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * (pipSize + gap), 0f), new Vector2(pipSize, pipSize));
                    moxiePips.Add(pip);
                }
            }

            statusRow = UIFactory.CreateRect("Statuses", transform).Place(top, top, new Vector2(0f, -354f), new Vector2(Width + 40f, StatusSize));
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

            // 64 px frames at exactly 6x keep pixels crisp (a boss is drawn at 9x); the feet sit ~2 px above the frame bottom.
            float size = unit.IsBoss ? SpriteSize * BossScale : SpriteSize;
            var images = new List<Image>();
            // A resonance (the protagonist's Electric) glows in its element; Physical keeps the character's own glow.
            Afflatus? resonance = unit.Character != null && unit.Character.resonances.Count > 0 && unit.Afflatus != Afflatus.None
                ? unit.Afflatus : (Afflatus?)null;
            var glowColor = Color.Lerp(unit.Character != null ? AfflatusInfo.GlowFor(unit.Character, resonance) : unit.Definition.color, Color.white, 0.45f);
            glowColor.a = 0.16f;
            foreach (var o in new[] { new Vector2(-6f, 0f), new Vector2(6f, 0f), new Vector2(0f, 6f), new Vector2(0f, -6f),
                                      new Vector2(-4.5f, 4.5f), new Vector2(4.5f, 4.5f), new Vector2(-4.5f, -4.5f), new Vector2(4.5f, -4.5f) })
            {
                var glow = UIFactory.CreateImage(Body, "Glow", glowColor);
                glow.rectTransform.Place(bottom, bottom, new Vector2(0f, -20f) + o, new Vector2(size, size));
                images.Add(glow);
            }
            sprite = UIFactory.CreateImage(Body, "Sprite", Color.white);
            sprite.rectTransform.Place(bottom, bottom, new Vector2(0f, -20f), new Vector2(size, size));
            flash = UIFactory.CreateImage(Body, "Flash", Color.clear);
            flash.material = AdditiveUI();
            flash.rectTransform.Place(bottom, bottom, new Vector2(0f, -20f), new Vector2(size, size));
            images.Add(sprite);
            images.Add(flash);

            animator = Body.gameObject.AddComponent<UISpriteAnimator>();
            animator.Init(unit.Definition.animations, unit.Team == Team.Player ? Facing.Right : Facing.Left, images.ToArray());

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
                intentIcon.rectTransform.anchoredPosition = new Vector2(0f, intentY + Mathf.Round(Mathf.Sin(Time.time * 2.5f) * 1.5f) * 2f);
            if (ultReady)   // a full Ult bar pulses bright
            {
                float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                var c = Color.Lerp(PipReady, new Color(1f, 0.92f, 0.6f), p);
                foreach (var pip in moxiePips)
                {
                    pip.color = c;
                    pip.transform.localScale = Vector3.one * (1f + 0.12f * p);
                }
            }
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
            if (hpRoutine != null) StopCoroutine(hpRoutine);
            if (instant || !isActiveAndEnabled)
            {
                hpFill.Fill = target;
                return;
            }
            float from = hpFill.Fill;
            hpRoutine = StartCoroutine(Tween.Run(0.25f, p => hpFill.Fill = Mathf.Lerp(from, target, p)));
        }

        bool ultReady;
        static readonly Color PipReady = new Color(1f, 0.55f, 0.2f), PipEmptyColor = new Color(0.42f, 0.4f, 0.46f);

        public void SetMoxie(int value, int cap)
        {
            ultReady = value >= cap && cap > 0;
            for (int i = 0; i < moxiePips.Count; i++)
            {
                bool on = i < value;
                if (CardIcons.PipFull != null) moxiePips[i].sprite = on ? CardIcons.PipFull : CardIcons.PipEmpty;
                moxiePips[i].color = on ? (ultReady ? PipReady : Gold) : PipEmptyColor;
                moxiePips[i].transform.localScale = Vector3.one;
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
            if (show) intentIcon.sprite = CardIcons.IntentCard(intent);
        }

        /// <summary>How far the HP bar leans over its height (canvas units).</summary>
        const float HpSlant = 9f;

        /// <summary>Status icon size under the unit (canvas units).</summary>
        const float StatusSize = 76f;

        public void SetStatuses(List<StatusSnapshot> statuses)
        {
            Statuses = new List<StatusSnapshot>(statuses);
            UIFactory.ClearChildren(statusRow);

            // Big icons, no numbers; hover one for what it does and the turns left (StatusChip / StatusTooltip).
            foreach (var s in statuses)
                StatusChip.Create(statusRow, s, StatusSize, false);
        }

        /// <summary>Red lock-on: this enemy is the current target.</summary>
        public void SetSelected(bool selected)
        {
            if (lockOn != null) lockOn.Show(selected && Unit.IsAlive);
        }

        /// <summary>Orange ring: this unit would be hit / buffed by the card being hovered in the hand.</summary>
        public void SetTargetPreview(bool on)
        {
            if (targetPreview != null) targetPreview.Show(on && Unit.IsAlive);
        }

        /// <summary>White ring: the unit under the mouse or the arrow-key cursor.</summary>
        public void SetFocused(bool focused)
        {
            if (focusRing != null) focusRing.Show(focused && Unit.IsAlive);
            if (nameText != null) nameText.enabled = focused && Unit.IsAlive;   // the name shows with the focus ring
        }

        public IEnumerator Die()
        {
            SetSelected(false);
            SetFocused(false);
            SetTargetPreview(false);
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
                case CardKind.Attack: return UIText.Get("card.kind.attack");
                case CardKind.Buff: return UIText.Get("card.kind.buff");
                case CardKind.Debuff: return UIText.Get("card.kind.debuff");
                case CardKind.Counter: return UIText.Get("card.kind.counter");
                default: return UIText.Get("card.kind.heal");
            }
        }

        /// <summary>The kind's color, matching its icon in the CardIcons atlas (red, blue, purple, green, ochre).</summary>
        public static Color KindColor(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Attack: return new Color(0.84f, 0.3f, 0.27f);
                case CardKind.Buff: return new Color(0.3f, 0.5f, 0.82f);
                case CardKind.Debuff: return new Color(0.5f, 0.36f, 0.75f);
                case CardKind.Counter: return new Color(0.76f, 0.58f, 0.2f);
                default: return new Color(0.35f, 0.72f, 0.48f);
            }
        }
    }
}
