using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Entities;
using HarmonyLib;
using MSN2Characters.Skins;
using UnityEngine;
using UnityEngine.Rendering;
using _Game.Scripts.Evgeny.Clothes;
using _Game.Systems.ClothesController;

namespace MSN2Characters
{
    // Test commands on the Community Patch agent bridge (soft dependency, active only when AgentBridge = true).
    public static class CharacterAgent
    {
        private static MethodInfo _register, _done, _write;

        public static void TryRegister()
        {
            var bridge = Type.GetType("MSN2CommunityPatch.AgentBridge, MSN2CommunityPatch");
            _register = bridge?.GetMethod("Register");
            _done = bridge?.GetMethod("Done");
            _write = bridge?.GetMethod("Write");
            if (_register == null) return;
            Reg("skin", Skin);
            Reg("wear", Wear);
            Reg("blend", Blend);
            Reg("look", Look);
            Reg("portrait", Portrait);
            Reg("actor", ActorCmd);
            Reg("fsm", Fsm);
            Reg("spritedump", SpriteDump);
            Reg("cutaudit", CutNpcs.CutAudit.Run);
            // house open|close <HouseZoneID>: the call the OpenHouse letters make (story progression shortcut for tests)
            Reg("house", (id, a) =>
            {
                var hoc = UnityEngine.Object.FindFirstObjectByType<_Game.Systems.HouseOpenerController.HouseOpenerController>();
                if (!hoc || a.Length < 2 || !Enum.TryParse<Game.Zones.HouseZoneID>(a[1], true, out var z)) return Done(id, new { ok = false, error = "usage: house open|close <ChloeHouse|CamilaHouse|EmmaHouse>" });
                if (a[0] == "open") hoc.OpenHouse(z); else hoc.CloseHouse(z);
                return Done(id, new { ok = true, opened = string.Join(",", hoc.OpenedHouseZoneIDs), neighbours = CutNpcs.Neighbours.Log });
            });
            // beds: BedEntity components in the order the Expansion `sex <Actor> <i>` command indexes them
            Reg("beds", (id, a) => Done(id, UnityEngine.Object.FindObjectsByType<global::BedEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID)
                .Select((b, i) => $"{i}: {b.transform.parent?.parent?.name}/{b.name} type={b.GetType().Name}").ToArray()));
            CharactersPlugin.Log.LogDebug("[agent] character test commands registered");
        }

        private static void Reg(string name, Func<string, string[], IEnumerator> f) => _register.Invoke(null, new object[] { name, f });
        internal static IEnumerator Done(string id, object r) => (IEnumerator)_done.Invoke(null, new[] { id, r });
        internal static void Write(string id, object r) => _write.Invoke(null, new[] { id, r });

        internal static EntitiesController Entities => UnityEngine.Object.FindFirstObjectByType<EntitiesController>();
        internal static IActorEntity Actor(string name) => Enum.TryParse<ActorID>(name, true, out var a) ? Entities?.GetActorByActorID(a) : null;

        private static string Shader(Material m) => m ? $"{m.name}|{m.shader.name}" : "<null>";

        // skin info | skin reload | skin off | skin on | skin mats
        private static IEnumerator Skin(string id, string[] a)
        {
            var sub = a.Length > 0 ? a[0].ToLowerInvariant() : "info";
            if (sub == "reload" || sub == "on")
            {
                CharactersRuntime.Instance.ReloadAll();
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 120f && CharactersRuntime.Packs.Values.Any(p => p.State == SkinPack.LoadState.Loading)) yield return null;
                yield return new WaitForSecondsRealtime(0.5f);
                Write(id, Info());
                yield break;
            }
            if (sub == "off")
            {
                foreach (var st in SkinApplier.Applied.Values.ToList()) SkinApplier.Revert(st);
                foreach (var p in CharactersRuntime.Packs.Values) p.Unload();
                CharactersRuntime.Packs.Clear();
                yield return null;
                Write(id, new { ok = true, applied = SkinApplier.Applied.Count });
                yield break;
            }
            if (sub == "mats")
            {
                var actor = Actor(a.Length > 1 ? a[1] : "Camila") as MonoBehaviour;
                if (!actor) { Write(id, new { ok = false, error = "actor not found" }); yield break; }
                Write(id, actor.GetComponentsInChildren<Renderer>(true).Select(r => new
                {
                    renderer = r.name, enabled = r.enabled, active = r.gameObject.activeInHierarchy,
                    mats = r.sharedMaterials.Select(m => m ? new
                    {
                        name = m.name, shader = m.shader.name, keywords = m.shaderKeywords, queue = m.renderQueue,
                        tex = m.GetTexturePropertyNames().Select(p => $"{p}={(m.GetTexture(p) ? m.GetTexture(p).name + " " + m.GetTexture(p).width : "-")}").ToArray(),
                        floats = Enumerable.Range(0, m.shader.GetPropertyCount()).Where(i => m.shader.GetPropertyType(i) == ShaderPropertyType.Float || m.shader.GetPropertyType(i) == ShaderPropertyType.Range)
                            .Select(i => $"{m.shader.GetPropertyName(i)}={m.GetFloat(m.shader.GetPropertyName(i)):0.###}").ToArray(),
                        colors = Enumerable.Range(0, m.shader.GetPropertyCount()).Where(i => m.shader.GetPropertyType(i) == ShaderPropertyType.Color)
                            .Select(i => $"{m.shader.GetPropertyName(i)}={m.GetColor(m.shader.GetPropertyName(i))}").ToArray(),
                    } : null).ToArray()
                }).ToArray());
                yield break;
            }
            Write(id, Info());
        }

        private static object Info() => new
        {
            config = CharactersPlugin.SkinFor.ToDictionary(k => k.Key.ToString(), k => k.Value.Value),
            packs = CharactersRuntime.Packs.Select(p => new
            {
                id = p.Key, state = p.Value.State.ToString(), error = p.Value.Error, seconds = p.Value.LoadSeconds,
                meshes = p.Value.Meshes.Keys.ToArray(), textures = p.Value.Textures.Count, textureMB = p.Value.TextureBytes / 1048576f,
            }).ToArray(),
            applied = SkinApplier.Applied.Values.Select(st => new
            {
                actor = st.Actor.ActorID.ToString(),
                active = (st.Actor as MonoBehaviour) && (st.Actor as MonoBehaviour).gameObject.activeInHierarchy,
                bodyMesh = st.Body && st.Body.sharedMesh ? st.Body.sharedMesh.name : null,
                bones = st.Body ? st.Body.bones.Length : 0, missingBones = st.MissingBones, ambiguous = st.Ambiguous.ToArray(),
                bindposeMaxDiff = st.BindposeMaxDiff, animatorRebound = st.AnimatorRebound,
                bodyMats = st.Body ? st.Body.sharedMaterials.Select(Shader).ToArray() : null,
                created = st.Created.Where(g => g).Select(g => g.name).ToArray(),
                garments = st.Garments.Count,
                dickShape = DickShape(st.Actor),
                log = st.Log,
            }).ToArray(),
            name = Resources.FindObjectsOfTypeAll<Game.Data.ActorDataSO>().Where(x => x.ID == ActorID.Camila).Select(x => x.Name).FirstOrDefault(),
            displayNames = SkinApplier.DisplayNames,
            portrait = Resources.FindObjectsOfTypeAll<Game.Data.ActorDataSO>().Where(x => x.ID == ActorID.Camila).Select(x => x.Portrait ? x.Portrait.name : null).FirstOrDefault(),
        };

        private static object DickShape(IActorEntity actor)
        {
            if (!(actor is AIActorEntity ai) || !ai.BlendShapeSync) return null;
            var name = Traverse.Create(ai.BlendShapeSync).Field("dickBlendShapeName").GetValue<string>();
            var src = Traverse.Create(ai.BlendShapeSync).Field("sourceRenderer").GetValue<SkinnedMeshRenderer>();
            return new { name, sourceIsBody = src && src == actor.ActorMeshRenderer.ActorSkinnedMeshRenderer, indexOnBody = src && src.sharedMesh ? src.sharedMesh.GetBlendShapeIndex(name ?? "") : -2 };
        }

        // wear <Actor> <ClothingPackID>
        private static IEnumerator Wear(string id, string[] a)
        {
            if (a.Length < 2 || !Enum.TryParse<ActorID>(a[0], true, out var actor)) { Write(id, new { ok = false, error = "usage: wear <Actor> <ClothingPackID>" }); yield break; }
            var cc = UnityEngine.Object.FindFirstObjectByType<ClothesController>();
            Cysharp.Threading.Tasks.UniTaskExtensions.Forget(cc.WearClothingPack(actor, new ClothingPackID(a[1])));
            yield return new WaitForSecondsRealtime(1.5f);
            var e = Entities.GetActorByActorID(actor);
            Write(id, new
            {
                ok = true, pack = e.WearingClothingPack?.ToString(), wearing = e.WearingClothes.Select(c => c.ToString()).ToArray(),
                garments = cc.LoadedAssets.Where(x => x.ActorID == actor && x.SkinnedObject).Select(x => $"{x.ClothID}:{(x.SkinnedObject.gameObject.activeSelf ? "on" : "off")}:{(x.SkinnedObject.sharedMesh ? x.SkinnedObject.sharedMesh.name : "-")}").ToArray(),
                dick = DickShape(e),
            });
        }

        // blend <Actor> [filter]: non-zero blendshape weights on the body renderer (proves animation drives the skin's shapes)
        private static IEnumerator Blend(string id, string[] a)
        {
            var e = Actor(a.Length > 0 ? a[0] : "Camila");
            var r = e?.ActorMeshRenderer?.ActorSkinnedMeshRenderer;
            if (!r || !r.sharedMesh) { Write(id, new { ok = false, error = "no body renderer" }); yield break; }
            var m = r.sharedMesh;
            var f = a.Length > 1 ? a[1] : "";
            Write(id, new
            {
                mesh = m.name, count = m.blendShapeCount,
                nonZero = Enumerable.Range(0, m.blendShapeCount).Where(i => r.GetBlendShapeWeight(i) > 0.01f && m.GetBlendShapeName(i).Contains(f)).Select(i => $"{m.GetBlendShapeName(i)}={r.GetBlendShapeWeight(i):0.#}").ToArray(),
                anim = r.GetComponentInParent<Animator>() is Animator an && an ? $"{an.name} enabled={an.enabled} init={an.isInitialized}" : null,
            });
        }

        // look <Actor> [yaw=0] [dist=1.6] [height=1.2] [fov=40] [size=1024] [name]: render the actor from a temporary camera to agent/shots/<name>.png
        private static IEnumerator Look(string id, string[] a)
        {
            var actor = Actor(a.Length > 0 ? a[0] : "Camila") as MonoBehaviour;
            if (!actor) { Write(id, new { ok = false, error = "actor not found" }); yield break; }
            float yaw = F(a, 1, 0), dist = F(a, 2, 1.6f), h = F(a, 3, 1.2f), fov = F(a, 4, 40f);
            int size = (int)F(a, 5, 1024);
            string name = a.Length > 6 ? a[6] : $"look_{a[0]}_{yaw:0}";
            // optional bone: aim relative to that bone (Hips/Head follow the animation; the root may not)
            Transform anchor = actor.transform;
            if (a.Length > 7)
            {
                var b = actor.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == a[7]);
                if (b) anchor = b;
            }
            yield return RenderShot(id, actor.transform, yaw, dist, h, fov, size, size, name, h, null, anchor);
        }

        // portrait <Actor> [file]: 512x512 face shot; with the skin on, writes it into the skin folder as the HUD portrait
        private static IEnumerator Portrait(string id, string[] a)
        {
            var e = Actor(a.Length > 0 ? a[0] : "Camila");
            var mono = e as MonoBehaviour;
            if (!mono) { Write(id, new { ok = false, error = "actor not found" }); yield break; }
            Transform head = null;
            SkinApplier.Applied.TryGetValue(e.ActorID, out var st);
            if (st != null) st.Skeleton.TryGetValue("Head", out head);
            if (!head) head = mono.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Head");
            // Portrait: upper body on a transparent background, the vanilla HUD sprite's 150:256 aspect.
            // Portrait2: 490x512 framed shot with the room behind her, like the vanilla phone/timetable picture.
            var dir = a.Length > 1 ? a[1] : (st != null ? st.Pack.Dir : Path.Combine(BepInEx.Paths.BepInExRootPath, "MSN2", "agent", "shots"));
            float yaw = F(a, 2, 15);
            yield return RenderShot(id + "_1", mono.transform, yaw, 1.75f, -0.16f, 30f, 300, 512, "portrait", -0.27f, Path.Combine(dir, "portrait.png"), head, cutout: true, keyLight: true);
            yield return RenderShot(id + "_2", mono.transform, yaw, 1.4f, -0.08f, 34f, 490, 512, "portrait2", -0.17f, Path.Combine(dir, "portrait2.png"), head, keyLight: true);
            // house HUD portraits: one per alert level (calm -> shocked), expressions from the face rig's FACS shapes
            var body = e.ActorMeshRenderer.ActorSkinnedMeshRenderer;
            for (int lvl = 0; lvl < Expressions.Length; lvl++)
            {
                var expr = Expressions[lvl];
                var saved = new System.Collections.Generic.List<(int, float)>();
                Action set = () =>
                {
                    foreach (var kv in expr)
                        foreach (var side in kv.Key.Contains("{s}") ? new[] { "Left", "Right" } : new[] { "" })
                        {
                            int bi = body.sharedMesh.GetBlendShapeIndex(kv.Key.Replace("{s}", side));
                            if (bi < 0) continue;
                            saved.Add((bi, body.GetBlendShapeWeight(bi)));
                            body.SetBlendShapeWeight(bi, kv.Value);
                        }
                };
                Action reset = () => { foreach (var (bi, w) in saved) body.SetBlendShapeWeight(bi, w); };
                yield return RenderShot(lvl == Expressions.Length - 1 ? id : id + "_c" + lvl, mono.transform, yaw + CondYaw[lvl], 1.75f, -0.16f, 30f, 300, 512, "portrait_cond" + lvl, -0.27f, Path.Combine(dir, $"portrait_cond{lvl}.png"), head, cutout: true, keyLight: true, before: set, after: reset);
            }
        }

        private static readonly float[] CondYaw = { 0, 10, -8, 18, -14, 4 };
        private static readonly System.Collections.Generic.Dictionary<string, float>[] Expressions =
        {
            new System.Collections.Generic.Dictionary<string, float> { ["facs_bs_MouthSmile{s}_div2"] = 35 },
            new System.Collections.Generic.Dictionary<string, float> { },
            new System.Collections.Generic.Dictionary<string, float> { ["facs_BrowDown{s}"] = 50, ["facs_bs_EyeSquint{s}_div2"] = 45, ["facs_bs_MouthPress{s}_div2"] = 50 },
            new System.Collections.Generic.Dictionary<string, float> { ["facs_BrowDown{s}"] = 85, ["facs_bs_MouthFrown{s}_div2"] = 70, ["facs_bs_NoseSneer{s}_div2"] = 40 },
            new System.Collections.Generic.Dictionary<string, float> { ["facs_bs_BrowInnerUp{s}_div2"] = 100, ["facs_cbs_EyesWide{s}_div2"] = 60, ["facs_bs_MouthStretch{s}_div2"] = 50 },
            new System.Collections.Generic.Dictionary<string, float> { ["facs_bs_BrowInnerUp{s}_div2"] = 100, ["facs_BrowOuterUp{s}"] = 100, ["facs_cbs_EyesWide{s}_div2"] = 100, ["facs_cbs_JawOpen_div2"] = 80, ["facs_bs_MouthFunnel_div2"] = 30 },
        };

        private static float F(string[] a, int i, float d) => a.Length > i && float.TryParse(a[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;

        internal static IEnumerator RenderShot(string id, Transform target, float yaw, float dist, float camH, float fov, int w, int h, string name, float lookH, string outFile = null, Transform anchor = null, bool cutout = false, bool keyLight = false, Action before = null, Action after = null)
        {
            yield return new WaitForEndOfFrame();
            before?.Invoke();
            var go = new GameObject("MSN2Characters_LookCam");
            var cam = go.AddComponent<Camera>();
            var main = Camera.main;
            if (main) { cam.CopyFrom(main); }
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 60f;
            // cut-out: the actor's renderers move to an otherwise unused layer for this one render; the camera draws only
            // that layer on transparent black, like the vanilla HUD portraits
            const int ShotLayer = 31;
            var moved = new System.Collections.Generic.List<(GameObject go, int layer)>();
            if (cutout)
            {
                foreach (var r in target.GetComponentsInChildren<Renderer>()) { moved.Add((r.gameObject, r.gameObject.layer)); r.gameObject.layer = ShotLayer; }
                cam.cullingMask = 1 << ShotLayer;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
            }
            GameObject lightGo = null;
            var dir = Quaternion.Euler(0, yaw, 0) * target.forward;
            var basePos = anchor ? anchor.position : target.position;
            var aim = basePos + Vector3.up * lookH;
            var camPos = basePos + dir * dist + Vector3.up * camH;
            // pull the camera in front of walls/furniture between it and the actor
            foreach (var hit in Physics.RaycastAll(aim, camPos - aim, Vector3.Distance(aim, camPos), ~0, QueryTriggerInteraction.Ignore).OrderBy(x => x.distance))
            {
                if (hit.transform.IsChildOf(target)) continue;
                camPos = aim + (camPos - aim).normalized * Mathf.Max(0.25f, hit.distance - 0.08f);
                break;
            }
            go.transform.position = camPos;
            go.transform.LookAt(aim);
            if (keyLight)
            {
                // soft key light from the camera side so face shots do not depend on the room's lamps
                lightGo = new GameObject("MSN2Characters_KeyLight");
                lightGo.transform.position = camPos + Vector3.up * 0.4f + go.transform.right * 0.3f;
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point; l.range = 4f; l.intensity = 1.6f; l.color = new Color(1f, 0.96f, 0.9f);
                l.cullingMask = cutout ? 1 << ShotLayer : ~0;
            }
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.enabled = false;
            string err = null;
            try
            {
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else cam.Render();
            }
            catch (Exception ex) { err = ex.Message; }
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, cutout ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var dirOut = Path.Combine(BepInEx.Paths.BepInExRootPath, "MSN2", "agent", "shots");
            Directory.CreateDirectory(dirOut);
            var file = outFile ?? Path.Combine(dirOut, name + ".png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            if (outFile != null) File.Copy(outFile, Path.Combine(dirOut, Path.GetFileName(outFile)), true);
            UnityEngine.Object.Destroy(tex);
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(go);
            if (lightGo) UnityEngine.Object.Destroy(lightGo);
            after?.Invoke();
            foreach (var (mgo, layer) in moved) if (mgo) mgo.layer = layer;
            Write(id, new { ok = err == null, error = err, file });
        }

        // spritedump <Actor>: write the actor's ActorDataSO Portrait/Portrait2 to agent/shots (GPU copy; sprites are not CPU-readable)
        private static IEnumerator SpriteDump(string id, string[] a)
        {
            if (a.Length < 1 || !Enum.TryParse<ActorID>(a[0], true, out var actor)) { Write(id, new { ok = false, error = "usage: spritedump <Actor>" }); yield break; }
            var so = Resources.FindObjectsOfTypeAll<Game.Data.ActorDataSO>().FirstOrDefault(x => x.ID == actor);
            var outs = new System.Collections.Generic.List<string>();
            var dirOut = Path.Combine(BepInEx.Paths.BepInExRootPath, "MSN2", "agent", "shots");
            Directory.CreateDirectory(dirOut);
            var all = new System.Collections.Generic.List<(string, Sprite)> { ("Portrait", so ? so.Portrait : null), ("Portrait2", so ? so.Portrait2 : null) };
            if (so && so.ConditionalPortraits != null) for (int i = 0; i < so.ConditionalPortraits.Length; i++) all.Add(($"Cond{i}", so.ConditionalPortraits[i]));
            foreach (var (label, sp) in all)
            {
                if (!sp) { outs.Add(label + ": none"); continue; }
                var r = sp.textureRect;
                var rt = RenderTexture.GetTemporary(sp.texture.width, sp.texture.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(sp.texture, rt);
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var t = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
                t.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0); t.Apply();
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                var f = Path.Combine(dirOut, $"sprite_{actor}_{label}.png");
                File.WriteAllBytes(f, t.EncodeToPNG());
                UnityEngine.Object.Destroy(t);
                outs.Add($"{label}: {sp.name} {r.width}x{r.height} -> {f}");
            }
            yield return null;
            Write(id, new { ok = true, sprites = outs });
        }

        // fsm <Actor> <FSMID> | fsm <Actor> timetable | fsm <Actor> stop: start one NodeCanvas FSM the way the timetable does
        private static IEnumerator Fsm(string id, string[] a)
        {
            var ai = Actor(a.Length > 0 ? a[0] : "") as AIActorEntity;
            if (!ai || a.Length < 2) { Write(id, new { ok = false, error = "usage: fsm <Actor> <FSMID|timetable|stop>" }); yield break; }
            string err = null;
            try
            {
                if (a[1] == "stop") ai.AiFsmController.Stop(true);
                else if (a[1] == "timetable") Cysharp.Threading.Tasks.UniTaskExtensions.Forget(ai.TimeTableController.RunAndSwitch(ai.destroyCancellationToken));
                else ai.AiFsmController.StartNewFSM(new FSMID(a[1]));
            }
            catch (Exception e) { err = e.Message; }
            yield return new WaitForSecondsRealtime(2f);
            Write(id, new { ok = err == null, error = err, fsm = ai.AiFsmController.FsmOwner && ai.AiFsmController.FsmOwner.behaviour ? ai.AiFsmController.FsmOwner.behaviour.name : null, interval = ai.TimeTableController.CurrentInterval?.Name });
        }

        // actor <Name> info|activate|tp
        private static IEnumerator ActorCmd(string id, string[] a)
        {
            var e = Actor(a.Length > 0 ? a[0] : "");
            var mono = e as MonoBehaviour;
            if (!mono) { Write(id, new { ok = false, error = "actor not found: " + (a.Length > 0 ? a[0] : "") }); yield break; }
            var sub = a.Length > 1 ? a[1].ToLowerInvariant() : "info";
            if (sub == "activate") mono.gameObject.SetActive(true);
            yield return null;
            var ai = e as AIActorEntity;
            Write(id, new
            {
                actor = e.ActorID.ToString(), go = mono.name, active = mono.gameObject.activeInHierarchy,
                pos = $"{mono.transform.position.x:0.00} {mono.transform.position.y:0.00} {mono.transform.position.z:0.00}",
                pack = e.WearingClothingPack?.ToString(), wearing = e.WearingClothes.Select(c => c.ToString()).ToArray(),
                fsm = ai && ai.AiFsmController ? Traverse.Create(ai.Data).Field("CurrentFsm").GetValue()?.ToString() : null,
                renderers = mono.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => $"{r.name}:{(r.enabled && r.gameObject.activeInHierarchy ? "on" : "off")}:{(r.sharedMesh ? r.sharedMesh.name + "/" + r.sharedMesh.vertexCount : "-")}").ToArray(),
            });
        }
    }
}
