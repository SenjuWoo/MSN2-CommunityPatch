using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MSN2CommunityPatch.Controls
{
    // The vanilla Settings > Controls tab is a static text list (Move WASD, Run Shift, ...). This makes that same page live: click a key, press the
    // new one. Same pixel font and layout as the game; rows the game never listed (selfie camera) and a reset row are cloned from existing ones.
    public class ControlsPage : MonoBehaviour
    {
        private class Row
        {
            public TMP_Text Value;
            public string Orig;                                    // vanilla text, shown while the keys are the defaults
            public (string action, string part, int occ, string name)[] Slots;
            public string Joiner = "";
        }

        private static readonly AccessTools.FieldRef<SettingsView, GameObject> ViewField = AccessTools.FieldRefAccess<SettingsView, GameObject>("_view");
        private readonly List<Row> _rows = new List<Row>();
        private bool _busy;

        public static void Apply(Harmony h)
        {
            var open = AccessTools.Method(typeof(SettingsView), "Open", new[] { typeof(SettingViewArguments) });
            if (open == null) { Plugin.Log.LogWarning("[controls] SettingsView.Open not found; Controls page stays read-only"); return; }
            h.Patch(open, postfix: new HarmonyMethod(typeof(ControlsPage), nameof(Opened)));
            KeyBindings.Changed += HintSync.MarkDirty;
        }

        private static void Opened(SettingsView __instance)
        {
            try
            {
                var view = ViewField(__instance);
                var page = view ? view.transform.Find("Back/Window/Controls") : null;
                if (page && !page.GetComponent<ControlsPage>()) page.gameObject.AddComponent<ControlsPage>().Wire();
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[controls] could not upgrade the Controls page: {e}"); }
        }

        private void OnEnable() => Refresh();

        private static bool IsLine(Transform t) => t.childCount >= 2 && t.GetChild(0).GetComponent<TMP_Text>() && t.GetChild(1).GetComponent<TMP_Text>();

        private void Wire()
        {
            var lines = Enumerable.Range(0, transform.childCount).Select(transform.GetChild).Where(IsLine).ToList();
            if (lines.Count < 9) { Plugin.Log.LogWarning($"[controls] unexpected Controls page ({lines.Count} lines); left as shipped"); return; }

            // vanilla order: Move, Run, Jump, Crouch, Interact, Camera, Phone, Abilities, Tool Selection
            var selfie = CloneLine(lines[5], "Selfie camera", "V");
            selfie.SetSiblingIndex(lines[5].GetSiblingIndex() + 1);
            var reset = CloneLine(lines[5], "Reset all keys", "Reset");
            var hint = CloneLine(lines[5], "Click a key, then press the new one. Esc cancels.", "");
            var hintLabel = hint.GetChild(0).GetComponent<TMP_Text>();
            hintLabel.enableAutoSizing = false; hintLabel.fontSize = 25f; hintLabel.color = new Color(1f, 1f, 1f, 0.7f);
            hint.SetAsLastSibling();

            Add(lines[0], "WASD", "", ("Player/Move", "up", 0, "Forward"), ("Player/Move", "left", 0, "Left"), ("Player/Move", "down", 0, "Back"), ("Player/Move", "right", 0, "Right"));
            Add(lines[1], null, "", ("Player/Sprint", null, 0, "Run"));
            Add(lines[2], null, "", ("Player/Jump", null, 0, "Jump"));
            Add(lines[3], null, "", ("Player/Crouch", null, 0, "Crouch"));
            Add(lines[4], null, "", ("Player/Interact", null, 0, "Interact"));
            Add(lines[5], null, "", ("Player/CameraOnOff", null, 0, "Camera"));
            Add(selfie, null, "", ("Player/CameraBackFront", null, 0, "Selfie"));
            Add(lines[6], null, "", ("Phone/ShowHide", null, 0, "Phone"));
            Add(lines[7], null, " / ", ("Player/ShoutSkill", null, 0, "Shout"), ("Player/EchoSenseSkill", null, 0, "Echo"));
            Add(lines[8], null, "", ("Player/Tools", null, 0, "Tools"));

            Clickable(reset, reset.GetChild(1).GetComponent<TMP_Text>(), () => { if (_busy) return; KeyBindings.ResetAll(); Refresh(); });

            // 12 rows instead of 9: tighten the rows so they still fit the window
            var vlg = GetComponent<VerticalLayoutGroup>();
            if (vlg) { vlg.spacing = 8f; vlg.childForceExpandHeight = false; }
            foreach (var line in Enumerable.Range(0, transform.childCount).Select(transform.GetChild).Where(IsLine))
            {
                var rt = (RectTransform)line;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 58f);
                var le = line.GetComponent<LayoutElement>() ?? line.gameObject.AddComponent<LayoutElement>();
                le.minHeight = le.preferredHeight = 58f;
            }
            Refresh();
            Plugin.Log.LogInfo($"[controls] Controls page is now rebindable ({_rows.Count} rows)");
        }

        private static Transform CloneLine(Transform template, string label, string value)
        {
            var clone = Instantiate(template.gameObject, template.parent).transform;
            clone.name = "MSN2 " + label;
            // the game localises labels through TranslateText; ours are plain English
            foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true).Where(m => m && m.GetType().Name == "TranslateText")) Destroy(mb);
            clone.GetChild(0).GetComponent<TMP_Text>().text = label;
            clone.GetChild(1).GetComponent<TMP_Text>().text = value;
            return clone;
        }

        private void Add(Transform line, string orig, string joiner, params (string action, string part, int occ, string name)[] slots)
        {
            var value = line.GetChild(1).GetComponent<TMP_Text>();
            var row = new Row { Value = value, Orig = orig ?? value.text, Slots = slots, Joiner = joiner };
            _rows.Add(row);
            Clickable(line, value, () => Begin(row));
        }

        private static void Clickable(Transform line, TMP_Text value, Action onClick)
        {
            var img = line.GetComponent<Image>() ?? line.gameObject.AddComponent<Image>();
            img.color = Color.clear; // invisible, but catches the click anywhere on the row
            img.canvasRenderer.cullTransparentMesh = false;
            img.raycastTarget = true;
            var b = line.gameObject.AddComponent<Button>();
            b.targetGraphic = value;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            var cb = b.colors;
            cb.normalColor = cb.selectedColor = Color.white;
            cb.highlightedColor = new Color(1f, 0.55f, 0.8f);
            cb.pressedColor = new Color(1f, 0.35f, 0.65f);
            cb.fadeDuration = 0.05f;
            b.colors = cb;
            b.onClick.AddListener(() => onClick());
        }

        private void Begin(Row row)
        {
            if (_busy) return;
            var slots = row.Slots.Select(s => KeyBindings.Find(s.action, s.part, s.occ)).ToList();
            if (slots.Any(s => s == null)) return;
            _busy = true;
            int i = 0;
            Action step = null;
            step = () =>
            {
                if (i >= slots.Count) { End(); return; }
                row.Value.text = slots.Count > 1 ? row.Slots[i].name + "?" : "Press...";
                KeyBindings.Rebind(slots[i], () => { i++; step(); }, End);
            };
            step();
        }

        private void End() { _busy = false; Refresh(); }

        private void Refresh()
        {
            if (_busy) return;
            foreach (var r in _rows)
            {
                var slots = r.Slots.Select(s => KeyBindings.Find(s.action, s.part, s.occ)).ToList();
                if (slots.Any(s => s == null)) continue;
                var shown = slots.Select(s => s.Display).ToList();
                r.Value.text = slots.All(s => s.IsDefault) ? r.Orig : string.Join(shown.All(d => d.Length == 1) ? r.Joiner : " / ", shown);
            }
        }
    }

    // The HUD hints ("Q", "Tab", "E", "R", "F") are static text in the game's prefabs; keep them showing the real keys.
    internal static class HintSync
    {
        private static readonly (string suffix, string action)[] Hints =
        {
            ("ToolSelectorView/View/SelectedTool/Hint", "Player/Tools"),
            ("ToolSelectorView/View/TabHint/Hotkey Text", "Phone/ShowHide"),
            ("InteractionView/View/TextBack/Icon/Text (TMP)", "Player/Interact"),
            ("SkillsView/View/Panel/Shout SkillUI/Hotkey Text", "Player/ShoutSkill"),
            ("SkillsView/View/Panel/Echo SkillUI/Hotkey Text", "Player/EchoSenseSkill"),
            ("SkillsView/View/Shout SkillUI (1)/Hotkey Text", "Player/WaiterSkill"),
        };

        private static readonly Dictionary<TMP_Text, string> Orig = new Dictionary<TMP_Text, string>();
        private static readonly TMP_Text[] Found = new TMP_Text[Hints.Length];
        private static bool _dirty = true;
        private static float _next, _nextScan;

        public static void MarkDirty() => _dirty = true;

        // Called about once a second while the Game scene is running. Scans for the hint objects only until they are all found (views are
        // created lazily), then just updates the cached ones.
        public static void Tick()
        {
            if (Time.unscaledTime < _next) return;
            if (!_dirty && !KeyBindings.HasCustom) return;
            _next = Time.unscaledTime + 1f;
            if ((_dirty || Time.unscaledTime >= _nextScan) && Found.Any(f => !f)) { Scan(); _nextScan = Time.unscaledTime + 10f; }
            _dirty = false;
            for (int i = 0; i < Hints.Length; i++)
            {
                var t = Found[i];
                if (!t) continue;
                var slot = KeyBindings.Find(Hints[i].action);
                if (slot == null) continue;
                if (!Orig.ContainsKey(t)) Orig[t] = t.text;
                string want = slot.IsDefault ? Orig[t] : slot.Display;
                if (t.text != want) t.text = want;
            }
        }

        private static void Scan()
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (!t || !t.gameObject.scene.IsValid()) continue;
                var path = SceneStats.Path(t.transform);
                for (int i = 0; i < Hints.Length; i++)
                    if (!Found[i] && path.EndsWith(Hints[i].suffix, StringComparison.Ordinal)) { Found[i] = t; break; }
            }
        }
    }
}
