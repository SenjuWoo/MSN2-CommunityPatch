using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MSN2Expansion
{
    // MSN1-style mission UI built at runtime with the game's own TMP font: quest tracker (left),
    // dialogue box (bottom), job journal (center). Keyboard only, on keys the game does not bind.
    public class ExpansionHud : MonoBehaviour
    {
        public static ExpansionHud Instance;
        private static readonly Color Pink = new Color(1f, 0.36f, 0.66f);

        private GameObject _root, _tracker, _dialogue, _journal;
        private TMP_Text _trackerText, _speaker, _line, _journalList, _journalBody, _hint;
        private TMP_FontAsset _font;
        private readonly Queue<Line> _queue = new Queue<Line>();
        private float _lineShownAt, _lineHold;
        private int _selected;
        private bool _dirty = true;

        public bool JournalOpen => _journal && _journal.activeSelf;
        public string CurrentLine => _dialogue && _dialogue.activeSelf ? $"{_speaker.text}: {_line.text}" : null;

        private MissionRunner R => ExpansionPlugin.Runner;

        private void Awake() => Instance = this;

        public void MarkDirty() => _dirty = true;

        private void EnsureBuilt()
        {
            if (_root) return;
            _font = FindGameFont();
            _root = new GameObject("MSN2Expansion.HUD", typeof(Canvas), typeof(CanvasScaler));
            _root.transform.SetParent(transform, false);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            _tracker = Panel("Tracker", new Vector2(0, 0.66f), new Vector2(0, 1), new Vector2(24, 0), new Vector2(460, 10));
            _trackerText = Text(_tracker, "Text", 22, TextAlignmentOptions.TopLeft, new Vector4(14, 10, 14, 10));
            var fit = _tracker.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _tracker.AddComponent<VerticalLayoutGroup>().padding = new RectOffset(14, 14, 10, 10);

            _dialogue = Panel("Dialogue", new Vector2(0.5f, 0.17f), new Vector2(0.5f, 0), Vector2.zero, new Vector2(1100, 170));
            _speaker = Text(_dialogue, "Speaker", 26, TextAlignmentOptions.TopLeft, new Vector4(24, 14, 24, 0));
            _speaker.color = Pink;
            _line = Text(_dialogue, "Line", 24, TextAlignmentOptions.TopLeft, new Vector4(24, 52, 24, 14));
            var hint = Text(_dialogue, "Hint", 16, TextAlignmentOptions.BottomRight, new Vector4(24, 0, 18, 8));
            hint.text = $"[{ExpansionPlugin.AcceptKey.Value}] next";
            hint.color = new Color(1, 1, 1, 0.6f);
            _dialogue.SetActive(false);

            _journal = Panel("Journal", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 680));
            var head = Text(_journal, "Header", 34, TextAlignmentOptions.Top, new Vector4(0, 14, 0, 0));
            head.text = "JOBS";
            head.color = Pink;
            _journalList = Text(_journal, "List", 22, TextAlignmentOptions.TopLeft, new Vector4(28, 70, 800, 60));
            _journalBody = Text(_journal, "Body", 22, TextAlignmentOptions.TopLeft, new Vector4(400, 70, 28, 60));
            _hint = Text(_journal, "Hint", 18, TextAlignmentOptions.Bottom, new Vector4(0, 0, 0, 16));
            _hint.color = new Color(1, 1, 1, 0.7f);
            _journal.SetActive(false);
        }

        private static TMP_FontAsset FindGameFont()
        {
            // HUD clock text uses the game's pixel font; fall back to any loaded TMP font
            var clock = FindFirstObjectByType<Game.Views.GameTimeView>(FindObjectsInactive.Include);
            var t = clock ? clock.GetComponentInChildren<TMP_Text>(true) : null;
            if (t && t.font) return t.font;
            return Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault() ?? TMP_Settings.defaultFontAsset;
        }

        private GameObject Panel(string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.06f, 0.05f, 0.1f, 0.78f);
            go.GetComponent<Image>().raycastTarget = false;
            return go;
        }

        private TMP_Text Text(GameObject parent, string name, float size, TextAlignmentOptions align, Vector4 margins)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margins.x, margins.w); rt.offsetMax = new Vector2(-margins.z, -margins.y);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (_font) t.font = _font;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        public void PlayDialogue(IEnumerable<Line> lines)
        {
            foreach (var l in lines ?? Enumerable.Empty<Line>()) _queue.Enqueue(l);
            if (_dialogue && !_dialogue.activeSelf) NextLine();
        }

        private void NextLine()
        {
            EnsureBuilt();
            if (_queue.Count == 0) { _dialogue.SetActive(false); return; }
            var l = _queue.Dequeue();
            _speaker.text = l.Speaker ?? "";
            _line.text = l.Text ?? "";
            _line.maxVisibleCharacters = 0;
            _lineShownAt = Time.unscaledTime;
            _lineHold = Mathf.Max(4f, (l.Text ?? "").Length * 0.07f);
            _dialogue.SetActive(true);
        }

        private static bool Pressed(string keyName) =>
            Keyboard.current != null && Enum.TryParse<Key>(keyName, true, out var k) && Keyboard.current[k].wasPressedThisFrame;

        private void Update()
        {
            if (R?.Hooks == null)
            {
                if (_root && _root.activeSelf) _root.SetActive(false);
                return;
            }
            EnsureBuilt();
            if (!_root.activeSelf) _root.SetActive(true);

            if (_dialogue.activeSelf)
            {
                int chars = (int)((Time.unscaledTime - _lineShownAt) * 45f);
                _line.maxVisibleCharacters = chars;
                bool typing = chars < _line.text.Length;
                if (Pressed(ExpansionPlugin.AcceptKey.Value) && !JournalOpen) { if (typing) _lineShownAt = -999; else NextLine(); }
                else if (!typing && Time.unscaledTime - _lineShownAt > _lineHold + _line.text.Length / 45f) NextLine();
            }
            else if (_queue.Count > 0) NextLine();

            if (Pressed(ExpansionPlugin.JournalKey.Value)) { _journal.SetActive(!_journal.activeSelf); _selected = 0; _dirty = true; }
            if (JournalOpen)
            {
                var list = Visible();
                if (Pressed(ExpansionPlugin.NextKey.Value)) { _selected++; _dirty = true; }
                if (Pressed(ExpansionPlugin.PrevKey.Value)) { _selected--; _dirty = true; }
                if (list.Count > 0) _selected = (_selected % list.Count + list.Count) % list.Count;
                if (Pressed(ExpansionPlugin.AcceptKey.Value) && list.Count > 0)
                {
                    var m = list[_selected];
                    var st = R.StateOf(m);
                    if (st.Status == MissionStatus.Available) { R.Accept(m.Id); _journal.SetActive(false); }
                    else if (st.Status == MissionStatus.Active && R.MainDone(m)) { R.TurnIn(m.Id); _journal.SetActive(false); }
                    else if (st.Status == MissionStatus.Active) MissionRunner.S.TrackedMission = m.Id;
                    _dirty = true;
                }
            }
            if (_dirty) { _dirty = false; Redraw(); }
        }

        private List<Mission> Visible() => R.ById.Values.Where(m => R.StateOf(m).Status != MissionStatus.Locked)
            .OrderBy(m => R.StateOf(m).Status == MissionStatus.Completed).ThenBy(m => m.PackId).ToList();

        private string ObjectiveLine(Mission m, Objective o, bool bonus)
        {
            var st = R.StateOf(m);
            bool failed = st.BonusFailed.Contains(o.Id), done = R.IsDone(m, o);
            bool constraint = PackLoader.ConstraintTypes.Contains(o.Type);
            string box = failed ? "<color=#ff6060>[x]</color>" : done && !constraint ? "<color=#8be07a>[v]</color>" : "[ ]";
            string count = o.Count > 1 && !constraint ? $" ({R.ProgressOf(m, o)}/{o.Count})" : "";
            string text = (bonus ? "<color=#ffd166>Bonus:</color> " : "") + o.Text + count;
            return done && !constraint ? $"{box} <color=#9a9a9a>{text}</color>" : $"{box} {text}";
        }

        private void Redraw()
        {
            var tracked = MissionRunner.S.TrackedMission != null && R.ById.TryGetValue(MissionRunner.S.TrackedMission, out var tm) && R.StateOf(tm).Status == MissionStatus.Active ? tm : null;
            _tracker.SetActive(tracked != null && ExpansionPlugin.ShowTracker.Value);
            if (tracked != null)
            {
                var sb = new StringBuilder($"<color=#ff5ca8><b>{tracked.Title}</b></color>\n");
                foreach (var o in tracked.Objectives) sb.AppendLine(ObjectiveLine(tracked, o, false));
                foreach (var o in tracked.Bonus) sb.AppendLine(ObjectiveLine(tracked, o, true));
                if (R.MainDone(tracked)) sb.AppendLine($"<color=#8be07a>Ready to turn in: [{ExpansionPlugin.JournalKey.Value}] then [{ExpansionPlugin.AcceptKey.Value}]</color>");
                _trackerText.text = sb.ToString().TrimEnd();
            }

            var list = Visible();
            var ls = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                var st = R.StateOf(list[i]).Status;
                string tag = st == MissionStatus.Available ? "<color=#ffd166>NEW</color>" : st == MissionStatus.Active ? "<color=#5cd0ff>ACTIVE</color>" : "<color=#8be07a>DONE</color>";
                ls.AppendLine($"{(i == _selected ? "<color=#ff5ca8>></color> " : "  ")}{tag} {list[i].Title}");
            }
            _journalList.text = list.Count == 0 ? "No jobs yet. Keep streaming." : ls.ToString();
            if (list.Count == 0) { _journalBody.text = ""; _hint.text = $"[{ExpansionPlugin.JournalKey.Value}] close"; return; }
            var m = list[Mathf.Clamp(_selected, 0, list.Count - 1)];
            var ms = R.StateOf(m);
            var b = new StringBuilder($"<size=30><color=#ff5ca8>{m.Title}</color></size>\n<color=#bbbbbb>From: {m.Giver}</color>\n\n{m.Description}\n\n<b>Objectives</b>\n");
            foreach (var o in m.Objectives) b.AppendLine(ObjectiveLine(m, o, false));
            foreach (var o in m.Bonus) b.AppendLine(ObjectiveLine(m, o, true) + (o.Reward.Money > 0 ? $" <color=#8be07a>+${o.Reward.Money}</color>" : ""));
            b.Append($"\n<b>Pay</b>: ${m.Reward.Money}, +{m.Reward.Subscribers} subscribers");
            foreach (var kv in m.Reward.Items) b.Append($", {kv.Value}x {kv.Key}");
            _journalBody.text = b.ToString();
            string action = ms.Status == MissionStatus.Available ? "accept" : ms.Status == MissionStatus.Active ? (R.MainDone(m) ? "turn in" : "track") : "";
            _hint.text = $"[{ExpansionPlugin.PrevKey.Value}] [{ExpansionPlugin.NextKey.Value}] select    " + (action != "" ? $"[{ExpansionPlugin.AcceptKey.Value}] {action}    " : "") + $"[{ExpansionPlugin.JournalKey.Value}] close";
        }
    }
}
