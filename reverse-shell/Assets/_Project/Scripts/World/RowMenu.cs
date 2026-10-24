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
    /// </summary>
    public class RowMenu : MonoBehaviour
    {
        public class Row
        {
            public string Label;
            public Action Action;
            public bool Enabled = true;
            public string Help;
        }

        const float RowWidth = 1100f, RowHeight = 74f, RowStep = 88f;

        RectTransform list;
        Text title, help;
        GameObject helpPlate;
        readonly List<Row> rows = new List<Row>();
        readonly List<Image> plates = new List<Image>();
        readonly List<Text> labels = new List<Text>();
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
            var dim = UIFactory.CreateImage(root, "Dim", new Color(0.05f, 0.045f, 0.04f, 0.9f), true);
            dim.rectTransform.Fill();
            InkStyle.Grain(root, new Color(1f, 0.95f, 0.85f, 0.035f));
            title = InkStyle.Title(root, "Title", "", 72, InkStyle.Paper, TextAnchor.MiddleCenter);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1200f, 110f));
            list = UIFactory.CreateRect("Rows", root).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(RowWidth, 600f));

            var plate = UIFactory.CreateImage(root, "Help", new Color(0.08f, 0.075f, 0.07f, 0.95f), true);
            plate.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1300f, 130f));
            InkStyle.Frame(plate.rectTransform, InkStyle.Rule, 2f);
            help = UIFactory.CreateText(plate.transform, "Text", "", 30, InkStyle.Paper, TextAnchor.MiddleCenter);
            help.rectTransform.Fill(18f);
            helpPlate = plate.gameObject;

            var keys = UIFactory.CreateText(root, "Keys",
                "<color=#C2A061>↑ ↓</color>  Choose      <color=#C2A061>Enter</color>  Select      <color=#C2A061>Esc</color>  Back",
                28, InkStyle.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            keys.supportRichText = true;
            keys.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1600f, 50f));
        }

        /// <summary>Opens (or refills) the menu with a page of rows; <paramref name="onBack"/> runs on Esc.</summary>
        public void Show(string heading, List<Row> page, Action onBack, int select = 0)
        {
            if (!IsOpen) openedFrame = Time.frameCount;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            title.text = heading;
            back = onBack;
            UIFactory.ClearChildren(list);
            rows.Clear();
            plates.Clear();
            labels.Clear();
            foreach (var row in page) AddRow(row);
            Select(select);
        }

        public void Hide()
        {
            if (!IsOpen) return;
            ClosedFrame = Time.frameCount;
            gameObject.SetActive(false);
        }

        void AddRow(Row row)
        {
            int i = rows.Count;
            var plate = UIFactory.CreateImage(list, "Row" + i, InkStyle.InkPanel, true);
            plate.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -i * RowStep), new Vector2(RowWidth, RowHeight));
            InkStyle.Frame(plate.rectTransform, InkStyle.Rule, 2f);
            var label = InkStyle.Title(plate.transform, "Label", row.Label, 40, InkStyle.Paper, TextAnchor.MiddleCenter);
            label.supportRichText = true;
            label.rectTransform.Fill(8f);
            rows.Add(row);
            plates.Add(plate);
            labels.Add(label);

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
                // The selected row is a hover: a lighter grey plate that grows a little (GrowSelected), no gold.
                labels[r].color = !usable ? UIFactory.WithAlpha(InkStyle.PaperDim, 0.7f) : InkStyle.Paper;
                plates[r].color = on ? InkStyle.HoverGray : usable ? InkStyle.InkPanel : UIFactory.WithAlpha(InkStyle.InkPanel, 0.5f);
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
            if (Time.frameCount == openedFrame) return;
            if (UIKeys.Pressed(KeyCode.UpArrow) || UIKeys.Pressed(KeyCode.W)) Move(-1);
            if (UIKeys.Pressed(KeyCode.DownArrow) || UIKeys.Pressed(KeyCode.S)) Move(1);
            if (UIKeys.Pressed(KeyCode.Return) || UIKeys.Pressed(KeyCode.KeypadEnter) || UIKeys.Pressed(KeyCode.Space) || UIKeys.Pressed(KeyCode.F))
                Run();
            else if (Input.GetKeyDown(KeyCode.Escape) && back != null) back();
        }
    }
}
