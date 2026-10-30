using System;
using System.Collections.Generic;
using Combat.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// A small overlay list in the title screen's style (ink rows, the selected one gold) used by the Esc menu for its
    /// Settings and Save pages. <see cref="Show"/> fills it with rows; ↑/↓ (or the mouse) pick, Enter / Space / F / a
    /// click runs a row, Esc runs the page's back action. A row with help text shows it underneath while selected.
    /// A row with <see cref="Row.Detail"/> is tall: a heading, a hairline and a few lines (the save slots).
    /// </summary>
    public class RowMenu : MonoBehaviour
    {
        public class Row
        {
            public string Label;
            public Action Action;
            public bool Enabled = true;
            public string Help;
            /// <summary>A tall row (save slots): the label becomes a heading in the top-left and this (rich text, a
            /// few lines) fills the rest. Null = an ordinary one-line row.</summary>
            public string Detail;
            /// <summary>Optional text at a tall row's top-right (the save time).</summary>
            public string Corner;
            /// <summary>A volume row: the label on the left and a slider (<see cref="VolumeSlider"/>) reading and
            /// writing this value; Left / Right move it while the row is selected.</summary>
            public Func<float> SliderGet;
            public Action<float> SliderSet;
        }

        const float RowWidth = 1180f, RowHeight = 86f, RowGap = 14f;
        // Tall rows (the save slots): wider and taller, with a wide margin inside so the text breathes.
        const float TallWidth = 1500f, TallHeight = 220f, TallGap = 12f, TallPadX = 48f, TallPadTop = 28f;

        RectTransform list;
        Text title, help;
        GameObject helpPlate;
        readonly List<Row> rows = new List<Row>();
        readonly List<Image> plates = new List<Image>();
        readonly List<Text> labels = new List<Text>();
        readonly List<Image> markers = new List<Image>();
        CanvasGroup group;
        readonly List<VolumeSlider> sliders = new List<VolumeSlider>();
        Action back;
        int index, openedFrame;

        public bool IsOpen { get { return gameObject.activeSelf; } }

        /// <summary>The frame it last closed (so the Esc that closed it isn't read again by the menu underneath).</summary>
        public int ClosedFrame { get; private set; }

        public static RowMenu Create(RectTransform parent)
        {
            var rt = UIFactory.CreateRect("RowMenu", parent).Fill();
            var menu = rt.gameObject.AddComponent<RowMenu>();
            menu.Build(rt);
            rt.gameObject.SetActive(false);
            return menu;
        }

        void Build(RectTransform root)
        {
            // NieR-style (NierStyle): a dark see-through screen, light type, a slight curved-screen vignette.
            group = root.gameObject.AddComponent<CanvasGroup>();
            var dim = UIFactory.CreateImage(root, "Dim", NierStyle.ScreenWash, true);
            dim.rectTransform.Fill();
            InkStyle.Grain(root, new Color(0.2f, 0.18f, 0.14f, 0.04f));
            NierStyle.Vignette(root);
            title = InkStyle.Title(root, "Title", "", 72, NierStyle.Text, TextAnchor.MiddleCenter);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1200f, 110f));
            list = UIFactory.CreateRect("Rows", root).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(RowWidth, 600f));

            var plate = NierStyle.Panel(root, "Help", NierStyle.RowGray, true);
            plate.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1300f, 130f));
            help = UIFactory.CreateText(plate.transform, "Text", "", 30, NierStyle.Text, TextAnchor.MiddleCenter);
            help.rectTransform.Fill(18f);
            helpPlate = plate.gameObject;

            var keys = UIFactory.CreateText(root, "Keys",
                UIText.Get("menu.keys"),
                28, NierStyle.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            keys.supportRichText = true;
            keys.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1600f, 50f));
        }

        /// <summary>Opens (or refills) the menu with a page of rows; <paramref name="onBack"/> runs on Esc.</summary>
        public void Show(string heading, List<Row> page, Action onBack, int select = 0)
        {
            bool opening = !IsOpen;
            if (opening) openedFrame = Time.frameCount;
            gameObject.SetActive(true);
            if (opening) StartCoroutine(NierStyle.SoftOpen(group, list));   // a soft rise and fade in
            transform.SetAsLastSibling();
            title.text = heading;
            back = onBack;
            UIFactory.ClearChildren(list);
            rows.Clear();
            plates.Clear();
            labels.Clear();
            markers.Clear();
            sliders.Clear();
            nextY = 0f;
            // Pages with tall rows (the save slots) start higher, so they fit above the key hints.
            list.anchoredPosition = new Vector2(0f, page.Exists(r => r.Detail != null) ? -180f : -280f);
            foreach (var row in page) AddRow(row);
            Select(select);
        }

        float nextY;   // where the next row goes, from the list's top

        public void Hide()
        {
            if (!IsOpen) return;
            ClosedFrame = Time.frameCount;
            gameObject.SetActive(false);
        }

        void AddRow(Row row)
        {
            int i = rows.Count;
            bool tall = row.Detail != null;
            float height = tall ? TallHeight : RowHeight, width = tall ? TallWidth : RowWidth;
            var plate = UIFactory.CreateImage(list, "Row" + i, NierStyle.RowGray, true);
            plate.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -nextY), new Vector2(width, height));
            nextY += height + (tall ? TallGap : RowGap);
            // The selection marker: a small accent square left of the row (shown on the selected one).
            var marker = NierStyle.Marker(plate.transform);
            marker.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(14f, 14f));
            markers.Add(marker);
            var label = InkStyle.Title(plate.transform, "Label", row.Label, 44, NierStyle.Text, tall ? TextAnchor.UpperLeft : TextAnchor.MiddleCenter);
            label.supportRichText = true;
            label.rectTransform.Fill(8f);
            if (tall)
            {
                // Heading top-left, a gold hairline under it, then the detail lines; the corner text top-right.
                var tl = new Vector2(0f, 1f);
                float inner = width - TallPadX * 2f;
                label.rectTransform.Place(tl, tl, new Vector2(TallPadX, -TallPadTop), new Vector2(inner, 54f));
                var rule = InkStyle.Line(plate.transform, "Rule", NierStyle.Rule);
                rule.rectTransform.Place(tl, tl, new Vector2(TallPadX, -TallPadTop - 60f), new Vector2(inner, 2f));
                var detail = UIFactory.CreateText(plate.transform, "Detail", row.Detail, 36, NierStyle.Text, TextAnchor.UpperLeft);
                detail.supportRichText = true;
                detail.lineSpacing = 1.05f;
                detail.raycastTarget = false;
                detail.rectTransform.Place(tl, tl, new Vector2(TallPadX, -TallPadTop - 74f), new Vector2(inner, height - TallPadTop - 74f - 20f));
                if (!string.IsNullOrEmpty(row.Corner))
                {
                    var corner = UIFactory.CreateText(plate.transform, "Corner", row.Corner, 36, NierStyle.TextDim, TextAnchor.UpperRight);
                    corner.raycastTarget = false;
                    corner.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-TallPadX, -TallPadTop - 4f), new Vector2(500f, 50f));
                }
            }
            VolumeSlider slider = null;
            if (row.SliderGet != null && row.SliderSet != null)
            {
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(40f, label.rectTransform.offsetMin.y);
                slider = VolumeSlider.Add(plate.rectTransform, row.SliderGet, row.SliderSet);
            }
            rows.Add(row);
            plates.Add(plate);
            labels.Add(label);
            sliders.Add(slider);

            var trigger = plate.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (Usable(i)) Select(i); });
            trigger.triggers.Add(enter);
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(_ => { if (Usable(i)) { Select(i); Run(); } });
            trigger.triggers.Add(click);
        }

        bool Usable(int i) { return i >= 0 && i < rows.Count && rows[i].Enabled && rows[i].Action != null; }

        void Select(int i)
        {
            if (rows.Count == 0) return;
            index = Mathf.Clamp(i, 0, rows.Count - 1);
            if (!Usable(index)) { Move(1); if (!Usable(index)) return; }
            for (int r = 0; r < rows.Count; r++)
            {
                bool on = r == index;
                bool usable = Usable(r);
                // NieR-style: the selected row is a lighter grey bar with the accent square beside it.
                labels[r].color = on ? NierStyle.HudText : !usable ? NierStyle.TextDim : NierStyle.Text;
                plates[r].color = on ? NierStyle.SelectBar : usable ? NierStyle.RowGray : UIFactory.WithAlpha(NierStyle.RowGray, 0.5f);
                markers[r].enabled = on;
            }
            string text = rows[index].Help;
            help.text = text ?? "";
            helpPlate.SetActive(!string.IsNullOrEmpty(text));
        }

        void Move(int step)
        {
            for (int n = 1; n <= rows.Count; n++)
            {
                int next = ((index + step * n) % rows.Count + rows.Count) % rows.Count;
                if (Usable(next))
                {
                    index = next;
                    Select(next);
                    return;
                }
            }
        }

        void Run()
        {
            if (Usable(index)) rows[index].Action();
        }

        const float SelectedScale = 1.04f;

        /// <summary>The selected row grows a little (eased; unscaled, so it works over a paused world).</summary>
        void GrowSelected()
        {
            float k = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
            for (int r = 0; r < plates.Count; r++)
            {
                if (plates[r] == null) continue;
                float target = r == index && Usable(r) ? SelectedScale : 1f;
                var rt = plates[r].rectTransform;
                rt.localScale = Vector3.Lerp(rt.localScale, Vector3.one * target, k);
            }
        }

        void Update()
        {
            GrowSelected();
            if (Time.frameCount == openedFrame || GuideBook.Blocking) return;   // the guide is over it
            if (UIKeys.Pressed(KeyCode.UpArrow) || UIKeys.Pressed(KeyCode.W)) Move(-1);
            if (UIKeys.Pressed(KeyCode.DownArrow) || UIKeys.Pressed(KeyCode.S)) Move(1);
            var slider = index < sliders.Count ? sliders[index] : null;
            if (slider != null && (UIKeys.Pressed(KeyCode.LeftArrow) || UIKeys.Pressed(KeyCode.A))) slider.Step(-1);
            if (slider != null && (UIKeys.Pressed(KeyCode.RightArrow) || UIKeys.Pressed(KeyCode.D))) slider.Step(1);
            if (UIKeys.Pressed(KeyCode.Return) || UIKeys.Pressed(KeyCode.KeypadEnter) || UIKeys.Pressed(KeyCode.Space) || UIKeys.Pressed(KeyCode.F))
                Run();
            else if (UIKeys.Back && back != null) back();
        }
    }
}
