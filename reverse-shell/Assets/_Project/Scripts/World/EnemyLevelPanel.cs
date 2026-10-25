using System;
using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The Sandbox chest: sets how strong the next practice fights are. Three presets (Enemy Lv 5, Boss Lv 20,
    /// Custom Lv 30) and the level itself, which can be tuned from 1 to 99 (buttons, or ←/→ for 1 and ↑/↓ for 10).
    /// Saved in <see cref="WorldSession.SandboxEnemyLevel"/> / <see cref="WorldSession.SandboxBoss"/> and applied to
    /// fights in the Sandbox only. Enter or Esc closes it.
    /// </summary>
    public class EnemyLevelPanel : MonoBehaviour
    {
        Text current, levelText;
        readonly List<Image> presetPlates = new List<Image>();
        int openedFrame;

        static readonly string[] PresetNames = { "Enemy  Lv 5", "Boss  Lv 20", "Custom  Lv 30" };
        static readonly int[] PresetLevels = { 5, 20, 30 };
        static readonly bool[] PresetBoss = { false, true, false };

        public bool IsOpen { get { return gameObject.activeSelf; } }
        public int LastToggleFrame { get; private set; }

        public static EnemyLevelPanel Create(RectTransform parent)
        {
            var rt = UIFactory.CreateRect("EnemyLevelPanel", parent).Fill();
            var panel = rt.gameObject.AddComponent<EnemyLevelPanel>();
            panel.Build(rt);
            rt.gameObject.SetActive(false);
            return panel;
        }

        void Build(RectTransform root)
        {
            var center = new Vector2(0.5f, 0.5f);
            var dim = UIFactory.CreateImage(root, "Dim", new Color(0.05f, 0.045f, 0.04f, 0.8f), true);
            dim.rectTransform.Fill();
            var card = InkStyle.Panel(root, "Card", InkStyle.InkPanel);
            card.rectTransform.Place(center, center, Vector2.zero, new Vector2(900f, 860f));
            InkStyle.HeroFrame(card.rectTransform, InkStyle.Gold, 10f);
            var rt = card.rectTransform;

            var title = InkStyle.Title(rt, "Title", "Sandbox Chest", 64, InkStyle.Paper, TextAnchor.MiddleCenter);
            title.rectTransform.TopStrip(30f, 80f);
            current = UIFactory.CreateText(rt, "Current", "", 32, InkStyle.GoldBright, TextAnchor.MiddleCenter);
            current.supportRichText = true;
            current.rectTransform.TopStrip(116f, 46f);

            // Presets
            for (int i = 0; i < PresetNames.Length; i++)
            {
                int index = i;
                var b = InkStyle.Button(rt, PresetNames[i], PresetNames[i], () => Apply(PresetLevels[index], PresetBoss[index]), false, 40);
                b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f - i * 100f), new Vector2(660f, 92f));
                presetPlates.Add(b.transform.Find("Plate").GetComponent<Image>());
            }

            // Fine-tune the level: -10 -1 [ N ] +1 +10, and boss on/off.
            var label = InkStyle.Label(rt, "LevelLabel", "Enemy Level", 28, InkStyle.Gold, TextAnchor.MiddleCenter);
            label.rectTransform.TopStrip(500f, 40f);
            float y = -560f;
            Step(rt, "-10", -10, new Vector2(-300f, y));
            Step(rt, "-1", -1, new Vector2(-170f, y));
            var box = InkStyle.Panel(rt, "LevelBox", new Color(0f, 0f, 0f, 0.5f));
            box.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(170f, 90f));
            InkStyle.Frame(box.rectTransform, InkStyle.GoldBright, 2f);
            levelText = InkStyle.Title(box.rectTransform, "Level", "", 60, InkStyle.GoldBright, TextAnchor.MiddleCenter);
            levelText.rectTransform.Fill();
            Step(rt, "+1", 1, new Vector2(170f, y));
            Step(rt, "+10", 10, new Vector2(300f, y));

            var bossToggle = InkStyle.Button(rt, "Boss", "Boss: Off", () =>
            {
                WorldSession.SandboxBoss = !WorldSession.SandboxBoss;
                Refresh();
            }, false, 36);
            bossToggle.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-170f, -690f), new Vector2(320f, 92f));
            bossLabel = bossToggle.GetComponentInChildren<Text>();
            var done = InkStyle.Button(rt, "Done", "Done", Close, true, 40);
            done.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(170f, -690f), new Vector2(320f, 92f));

            var keys = UIFactory.CreateText(root, "Keys",
                "<color=#C2A061>← →</color>  Level ±1      <color=#C2A061>↑ ↓</color>  Level ±10      <color=#C2A061>Enter / Esc</color>  Done",
                28, InkStyle.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            keys.supportRichText = true;
            keys.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1600f, 50f));
        }

        Text bossLabel;

        void Step(RectTransform parent, string label, int delta, Vector2 at)
        {
            var b = InkStyle.Button(parent, "Step" + label, label, () => Nudge(delta), false, 36);
            b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), at, new Vector2(124f, 92f));
        }

        void Apply(int level, bool boss)
        {
            WorldSession.SandboxEnemyLevel = level;
            WorldSession.SandboxBoss = boss;
            Refresh();
        }

        void Nudge(int delta)
        {
            WorldSession.SandboxEnemyLevel = Mathf.Clamp(WorldSession.SandboxEnemyLevel + delta, 1, BattleUnit.MaxEnemyLevel);
            Refresh();
        }

        void Refresh()
        {
            int level = WorldSession.SandboxEnemyLevel;
            bool boss = WorldSession.SandboxBoss;
            levelText.text = level.ToString();
            bossLabel.text = "Boss: " + (boss ? "On" : "Off");
            current.text = "Next practice fight:  Lv " + level + (boss ? "  <color=#E05A50>BOSS</color>" : "");
            for (int i = 0; i < presetPlates.Count; i++)
            {
                bool on = PresetLevels[i] == level && PresetBoss[i] == boss;
                presetPlates[i].color = on ? InkStyle.Gold : InkStyle.InkPanel;
                presetPlates[i].transform.parent.GetComponentInChildren<Text>().color = on ? InkStyle.Ink : InkStyle.Paper;
            }
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            openedFrame = LastToggleFrame = Time.frameCount;
            Refresh();
        }

        public void Close()
        {
            LastToggleFrame = Time.frameCount;
            gameObject.SetActive(false);
            SaveGame.Save();
        }

        void Update()
        {
            if (Time.frameCount == openedFrame) return;
            if (UIKeys.Pressed(KeyCode.LeftArrow) || UIKeys.Pressed(KeyCode.A)) Nudge(-1);
            if (UIKeys.Pressed(KeyCode.RightArrow) || UIKeys.Pressed(KeyCode.D)) Nudge(1);
            if (UIKeys.Pressed(KeyCode.DownArrow) || UIKeys.Pressed(KeyCode.S)) Nudge(-10);
            if (UIKeys.Pressed(KeyCode.UpArrow) || UIKeys.Pressed(KeyCode.W)) Nudge(10);
            if (UIKeys.Back || UIKeys.Pressed(KeyCode.Return) || UIKeys.Pressed(KeyCode.KeypadEnter)) Close();
        }
    }
}
