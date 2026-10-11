using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using IngameDebugConsole;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSN2CommunityPatch
{
    // Local file command channel for agents/testers: append "id|command args" lines to BepInEx/MSN2/agent/cmd.txt,
    // results land in BepInEx/MSN2/agent/out/<id>.json. Nothing listens on the network.
    public class AgentBridge : MonoBehaviour
    {
        public static readonly string Dir = Path.Combine(Paths.BepInExRootPath, "MSN2", "agent");
        private static readonly string CmdFile = Path.Combine(Dir, "cmd.txt");
        private static readonly string OutDir = Path.Combine(Dir, "out");
        private static readonly Dictionary<string, Func<string, string[], IEnumerator>> Handlers = new Dictionary<string, Func<string, string[], IEnumerator>>(StringComparer.OrdinalIgnoreCase);
        private float _next;

        public static void Register(string name, Func<string, string[], IEnumerator> handler) => Handlers[name] = handler;

        private void Awake()
        {
            Directory.CreateDirectory(OutDir);
            Register("ping", (id, a) => Done(id, new { ok = true, plugin = Plugin.Version, scene = SceneManager.GetActiveScene().name, frame = Time.frameCount, fps = 1f / Time.unscaledDeltaTime, player = Application.version }));
            Register("stats", (id, a) => Done(id, SceneStats.Collect()));
            Register("fps", Fps);
            Register("profile", Profile);
            Register("loop", Loop);
            Register("markers", (id, a) => MarkerRecorder.Record(Arg(a, 0, 10), r => Write(id, r)));
            Register("screenshot", Screenshot);
            Register("console", Console);
            Register("quit", (id, a) => { Write(id, new { ok = true }); Application.Quit(); return null; });
            Register("find", Find);
            Register("ui", (id, a) => Done(id, Buttons().Select(b => $"{SceneStats.Path(b.transform)} | {Label(b)}").ToArray()));
            Register("click", (id, a) =>
            {
                var q = string.Join(" ", a);
                var b = Buttons().FirstOrDefault(x => Label(x).Equals(q, StringComparison.OrdinalIgnoreCase))
                        ?? Buttons().FirstOrDefault(x => (SceneStats.Path(x.transform) + "|" + Label(x)).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
                if (b == null) return Done(id, new { ok = false, error = "no interactable button matches " + q });
                b.onClick.Invoke();
                return Done(id, new { ok = true, clicked = SceneStats.Path(b.transform) });
            });
            Plugin.Log.LogInfo($"[agent] listening on {CmdFile}");
        }

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.25f;
            if (!File.Exists(CmdFile)) return;
            string[] lines;
            try { lines = File.ReadAllLines(CmdFile); File.Delete(CmdFile); }
            catch (IOException) { return; } // writer still holds it; retry next poll
            foreach (var raw in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var bar = raw.IndexOf('|');
                var id = bar > 0 ? raw.Substring(0, bar).Trim() : DateTime.Now.Ticks.ToString();
                var parts = (bar > 0 ? raw.Substring(bar + 1) : raw).Trim().Split(new[] { ' ' }, 2);
                var args = parts.Length > 1 ? parts[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries) : new string[0];
                if (parts.Length > 1 && parts[0].Equals("console", StringComparison.OrdinalIgnoreCase)) args = new[] { parts[1] };
                Plugin.Log.LogInfo($"[agent] {id}: {raw}");
                if (!Handlers.TryGetValue(parts[0], out var h)) { Write(id, new { ok = false, error = "unknown command " + parts[0], commands = Handlers.Keys }); continue; }
                StartCoroutine(Run(id, h, args));
            }
        }

        private IEnumerator Run(string id, Func<string, string[], IEnumerator> h, string[] args)
        {
            IEnumerator it;
            try { it = h(id, args); }
            catch (Exception e) { Write(id, new { ok = false, error = e.ToString() }); yield break; }
            if (it == null) yield break;
            while (true)
            {
                object cur;
                try { if (!it.MoveNext()) break; cur = it.Current; }
                catch (Exception e) { Write(id, new { ok = false, error = e.ToString() }); yield break; }
                yield return cur;
            }
        }

        public static void Write(string id, object result)
        {
            try { File.WriteAllText(Path.Combine(OutDir, id + ".json"), JsonConvert.SerializeObject(result, Formatting.Indented, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore })); }
            catch (Exception e) { Plugin.Log.LogError($"[agent] write {id} failed: {e}"); }
        }

        public static IEnumerator Done(string id, object result) { Write(id, result); yield break; }

        private static float Arg(string[] a, int i, float def) => a.Length > i && float.TryParse(a[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;

        private IEnumerator Fps(string id, string[] a)
        {
            FrameMonitor.Instance.BeginCapture();
            yield return new WaitForSecondsRealtime(Arg(a, 0, 10));
            Write(id, FrameMonitor.Instance.EndCapture());
        }

        private IEnumerator Profile(string id, string[] a)
        {
            bool alloc = a.Contains("alloc");
            int n = ScriptProfiler.Start(alloc);
            FrameMonitor.Instance.BeginCapture();
            yield return new WaitForSecondsRealtime(Arg(a, 0, 10));
            var fps = FrameMonitor.Instance.EndCapture();
            Write(id, new { instrumented = n, fps, top = ScriptProfiler.Stop((int)Arg(a, 1, 40), alloc) });
        }

        private IEnumerator Loop(string id, string[] a)
        {
            PlayerLoopTimer.Install();
            yield return null; yield return null;
            PlayerLoopTimer.Reset();
            FrameMonitor.Instance.BeginCapture();
            yield return new WaitForSecondsRealtime(Arg(a, 0, 10));
            Write(id, new { fps = FrameMonitor.Instance.EndCapture(), loop = PlayerLoopTimer.Report((int)Arg(a, 1, 25)) });
        }

        private IEnumerator Screenshot(string id, string[] a)
        {
            var dir = Path.Combine(Dir, "shots");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, (a.Length > 0 ? a[0] : id) + ".png");
            ScreenCapture.CaptureScreenshot(file);
            yield return null; yield return null; yield return new WaitForSecondsRealtime(0.5f);
            Write(id, new { ok = File.Exists(file), file });
        }

        private IEnumerator Console(string id, string[] a)
        {
            var log = new List<string>();
            Application.LogCallback cb = (c, s, t) => log.Add($"{t}: {c}");
            Application.logMessageReceived += cb;
            try { DebugLogConsole.ExecuteCommand(a.Length > 0 ? a[0] : "help"); }
            catch (Exception e) { log.Add("Exception: " + e.Message); }
            yield return null; yield return null;
            Application.logMessageReceived -= cb;
            Write(id, new { ok = true, log });
        }

        private static IEnumerable<UnityEngine.UI.Button> Buttons() =>
            FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(b => b.IsInteractable());

        private static string Label(Component c)
        {
            var tmp = c.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp) return tmp.text.Trim();
            var t = c.GetComponentInChildren<UnityEngine.UI.Text>();
            return t ? t.text.Trim() : c.name;
        }

        private IEnumerator Find(string id, string[] a)
        {
            var q = a.Length > 0 ? a[0] : "";
            var hits = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(200)
                .Select(t => $"{SceneStats.Path(t)} active={t.gameObject.activeInHierarchy} [{string.Join(",", t.GetComponents<Component>().Select(c => c ? c.GetType().Name : "null"))}]")
                .ToArray();
            Write(id, new { count = hits.Length, hits });
            yield break;
        }
    }
}
