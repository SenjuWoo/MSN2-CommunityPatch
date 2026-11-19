using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Data;
using Game.Entities;
using HarmonyLib;
using UnityEngine;
using _Game.Systems.ClothesController;

namespace MSN2Characters.Skins
{
    // Applies a SkinPack to one actor, visual only: ActorID, FSMs, saves and animations are untouched.
    // Every step is isolated: a failed step logs "[skin] <step> failed" and leaves that part vanilla.
    public class AppliedSkin
    {
        public IActorEntity Actor;
        public SkinPack Pack;
        public SkinnedMeshRenderer Body;
        public Mesh VanillaBodyMesh;
        public Material[] VanillaBodyMats;
        public Transform[] VanillaBodyBones;
        public SkinnedMeshRenderer VanillaHair;
        public readonly List<GameObject> Created = new List<GameObject>();
        public readonly Dictionary<string, Transform> Skeleton = new Dictionary<string, Transform>();
        public readonly HashSet<string> Ambiguous = new HashSet<string>();
        public readonly Dictionary<string, Material> Templates = new Dictionary<string, Material>();
        public readonly Dictionary<int, (Mesh mesh, Material[] mats, Transform[] bones)> Garments = new Dictionary<int, (Mesh, Material[], Transform[])>();
        public readonly List<string> Log = new List<string>();
        public int MissingBones;
        public float BindposeMaxDiff;
        public bool AnimatorRebound;
    }

    public static class SkinApplier
    {
        public static readonly Dictionary<ActorID, AppliedSkin> Applied = new Dictionary<ActorID, AppliedSkin>();
        private static Regex _nameRx;
        private static string _displayName;
        private static readonly Dictionary<ActorID, (string name, Sprite p1, Sprite p2, Sprite[] cond)> VanillaData = new Dictionary<ActorID, (string, Sprite, Sprite, Sprite[])>();

        public static void Patch(Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(ClothesController), "AttachClothing"), postfix: new HarmonyMethod(typeof(SkinApplier), nameof(AttachClothingPostfix)));
            h.Patch(AccessTools.Method(typeof(LocalizationManager), nameof(LocalizationManager.Translate)), postfix: new HarmonyMethod(typeof(SkinApplier), nameof(TranslatePostfix)));
            h.Patch(AccessTools.Method(typeof(Game.Views.ActorInHousePortrait), nameof(Game.Views.ActorInHousePortrait.Setup)), postfix: new HarmonyMethod(typeof(SkinApplier), nameof(SweepPostfix)));
        }

        private static string Clean(string matName) => matName.Replace(" (Instance)", "").Trim();

        public static AppliedSkin Apply(IActorEntity actor, SkinPack pack)
        {
            var st = new AppliedSkin { Actor = actor, Pack = pack };
            var mono = actor as MonoBehaviour;
            var amr = actor.ActorMeshRenderer;
            st.Body = amr ? amr.ActorSkinnedMeshRenderer : null;
            if (!mono || !st.Body) { CharactersPlugin.Log.LogError($"[skin] {actor.ActorID}: no ActorMeshRenderer, skin not applied"); return null; }

            var animator = st.Body.GetComponentInParent<Animator>();
            var skelRoot = animator ? animator.transform : mono.transform;
            foreach (var t in skelRoot.GetComponentsInChildren<Transform>(true))
            {
                if (st.Skeleton.ContainsKey(t.name)) st.Ambiguous.Add(t.name);
                else st.Skeleton[t.name] = t;
            }
            foreach (var m in st.Body.sharedMaterials) if (m) st.Templates[Clean(m.name)] = m;
            var hairDef = pack.Def.Hair;
            if (hairDef != null)
            {
                st.VanillaHair = mono.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == hairDef.Target);
                if (st.VanillaHair && st.VanillaHair.sharedMaterials.Length > 0 && st.VanillaHair.sharedMaterials[0]) st.Templates["Hair"] = st.VanillaHair.sharedMaterials[0];
            }

            // body (required: without it nothing else is applied)
            if (!Step(st, "body", () => ApplyBody(st))) { Revert(st); return null; }
            Step(st, "hair", () =>
            {
                if (hairDef == null) return;
                var go = AddRenderer(st, hairDef, $"{pack.Def.Id}_Hair");
                if (st.VanillaHair) st.VanillaHair.enabled = false;
                st.Log.Add($"hair {go.name} replaces {(st.VanillaHair ? st.VanillaHair.name : "<none>")}");
            });
            foreach (var e in pack.Def.Extra) Step(st, "extra " + e.Name, () => AddRenderer(st, e, e.Name ?? e.Mesh));
            Applied[actor.ActorID] = st;
            Step(st, "garments", () =>
            {
                var cc = UnityEngine.Object.FindFirstObjectByType<ClothesController>();
                if (!cc) return;
                foreach (var a in cc.LoadedAssets.Where(x => x.ActorID == actor.ActorID && x.SkinnedObject)) ConvertGarment(st, a.SkinnedObject);
            });
            Step(st, "rebind", () =>
            {
                // the Animator resolved blendShape.<name> curves against the vanilla mesh; rebind if it is already running
                if (animator && animator.isActiveAndEnabled && animator.isInitialized) { animator.Rebind(); animator.Update(0f); st.AnimatorRebound = true; }
            });
            Step(st, "outline", () => { if (mono.TryGetComponent<OutlineHelper>(out var oh)) oh.UpdateOutlineRenderers(); });
            Step(st, "identity", () => { ApplyIdentity(actor.ActorID, pack); st.Log.Add($"texts renamed {SweepTexts()}"); });
            CharactersPlugin.Log.LogInfo($"[skin] {pack.Def.Id} applied to {actor.ActorID}: bones resolved {st.Body.bones.Length - st.MissingBones}/{st.Body.bones.Length}, ambiguous {st.Ambiguous.Count}, bindpose max diff {st.BindposeMaxDiff:E1}, animator rebound {st.AnimatorRebound}; {string.Join("; ", st.Log)}");
            return st;
        }

        private static bool Step(AppliedSkin st, string name, Action a)
        {
            try { a(); return true; }
            catch (Exception e) { CharactersPlugin.Log.LogError($"[skin] {name} failed: {e.Message}\n{e.StackTrace}"); st.Log.Add(name + " FAILED"); return false; }
        }

        private static Transform[] ResolveBones(AppliedSkin st, string[] names, string what)
        {
            var bones = new Transform[names.Length];
            var missing = new List<string>();
            for (int i = 0; i < names.Length; i++)
                if (!st.Skeleton.TryGetValue(names[i], out bones[i])) missing.Add(names[i]);
            if (missing.Count > 0) throw new Exception($"{what}: bones missing on {st.Actor.ActorID}: {string.Join(", ", missing)}");
            return bones;
        }

        private static Material[] Materials(AppliedSkin st, MeshDef def, MsnmData d, Material garmentTemplate)
        {
            var mats = new Material[d.SlotNames.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                def.Slots.TryGetValue(d.SlotNames[i], out var slot);
                var tname = slot?.Template ?? "@garment";
                Material template = tname == "@garment" ? garmentTemplate : (st.Templates.TryGetValue(tname, out var t) ? t : null);
                if (slot != null && slot.Hide) template = template ? template : (garmentTemplate ? garmentTemplate : st.Templates.Values.FirstOrDefault());   // draws nothing
                if (!template) throw new Exception($"slot {d.SlotNames[i]}: template material '{tname}' not found (have {string.Join(", ", st.Templates.Keys)})");
                var m = new Material(template) { name = $"{st.Pack.Def.Id}_{d.SlotNames[i]}" };
                if (slot?.Tex != null)
                    foreach (var kv in slot.Tex)
                        if (m.HasProperty(kv.Key) && st.Pack.Textures.TryGetValue(kv.Value, out var tex)) m.SetTexture(kv.Key, tex);
                if (slot?.Floats != null)
                    foreach (var kv in slot.Floats)
                        if (!kv.Key.StartsWith("_Queue", StringComparison.Ordinal) && m.HasProperty(kv.Key)) m.SetFloat(kv.Key, kv.Value);
                if (slot?.Colors != null)
                    foreach (var kv in slot.Colors)
                        if (kv.Value != null && kv.Value.Length == 4 && m.HasProperty(kv.Key)) m.SetColor(kv.Key, new Color(kv.Value[0], kv.Value[1], kv.Value[2], kv.Value[3]));
                // Camila's body shader blends a baked legwear layer over the skin; a transparent texture switches it off
                foreach (var p in m.GetTexturePropertyNames())
                    if (p.StartsWith("_Legwear", StringComparison.Ordinal)) m.SetTexture(p, Texture2D.blackTexture);
                mats[i] = m;
            }
            return mats;
        }

        private static void ApplyBody(AppliedSkin st)
        {
            var def = st.Pack.Def.Body;
            var d = st.Pack.Data[def.Mesh];
            var bones = ResolveBones(st, d.Bones, "body");
            // consistency check: the file's bindposes are Camila's; compare with the live vanilla mesh by bone name
            var vm = st.Body.sharedMesh;
            if (vm)
            {
                var vb = st.Body.bones;
                var vp = vm.bindposes;
                for (int i = 0; i < d.Bones.Length; i++)
                {
                    int j = Array.FindIndex(vb, b => b && b.name == d.Bones[i]);
                    if (j < 0 || j >= vp.Length) continue;
                    for (int k = 0; k < 16; k++) st.BindposeMaxDiff = Math.Max(st.BindposeMaxDiff, Math.Abs(vp[j][k] - d.Bindposes[i][k]));
                }
            }
            var mats = Materials(st, def, d, null);
            st.VanillaBodyMesh = st.Body.sharedMesh;
            st.VanillaBodyMats = st.Body.sharedMaterials;
            st.VanillaBodyBones = st.Body.bones;
            st.Body.sharedMesh = st.Pack.Meshes[def.Mesh];
            st.Body.bones = bones;
            st.Body.sharedMaterials = mats;
            st.Log.Add($"body {d.VertexCount} verts {d.SlotNames.Length} slots {d.Shapes.Count} shapes");
        }

        private static GameObject AddRenderer(AppliedSkin st, MeshDef def, string name)
        {
            var d = st.Pack.Data[def.Mesh];
            var bones = ResolveBones(st, d.Bones, name);
            var mats = Materials(st, def, d, null);
            var go = new GameObject(name);
            go.layer = st.Body.gameObject.layer;
            go.transform.SetParent(st.Body.transform.parent, false);
            go.transform.localPosition = st.Body.transform.localPosition;
            go.transform.localRotation = st.Body.transform.localRotation;
            go.transform.localScale = st.Body.transform.localScale;
            var r = go.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = st.Pack.Meshes[def.Mesh];
            r.bones = bones;
            r.rootBone = st.Body.rootBone;
            r.localBounds = st.Body.localBounds;
            r.sharedMaterials = mats;
            r.shadowCastingMode = st.Body.shadowCastingMode;
            r.receiveShadows = st.Body.receiveShadows;
            r.updateWhenOffscreen = st.Body.updateWhenOffscreen;
            r.quality = st.Body.quality;
            r.renderingLayerMask = st.Body.renderingLayerMask;
            st.Created.Add(go);
            return go;
        }

        // ClothesController.AttachClothing(characterRenderer, clothingRenderer) runs every time a pack is worn.
        private static void AttachClothingPostfix(SkinnedMeshRenderer characterRenderer, SkinnedMeshRenderer clothingRenderer)
        {
            if (!characterRenderer || !clothingRenderer) return;
            foreach (var st in Applied.Values)
                if (st.Body == characterRenderer)
                {
                    try { ConvertGarment(st, clothingRenderer); }
                    catch (Exception e) { CharactersPlugin.Log.LogError($"[skin] garment {clothingRenderer.name} failed: {e.Message}"); }
                    return;
                }
        }

        public static void ConvertGarment(AppliedSkin st, SkinnedMeshRenderer r)
        {
            var clothId = r.name.EndsWith("_SkinnedPart") ? r.name.Substring(0, r.name.Length - "_SkinnedPart".Length) : r.name;
            if (!st.Pack.Def.Clothes.TryGetValue(clothId, out var def)) return;
            var mesh = st.Pack.Meshes[def.Mesh];
            if (r.sharedMesh == mesh) return;
            var d = st.Pack.Data[def.Mesh];
            var bones = ResolveBones(st, d.Bones, clothId);
            var template = r.sharedMaterials.FirstOrDefault(m => m);
            var mats = Materials(st, def, d, template);
            if (!st.Garments.ContainsKey(r.GetInstanceID())) st.Garments[r.GetInstanceID()] = (r.sharedMesh, r.sharedMaterials, r.bones);
            r.sharedMesh = mesh;
            r.bones = bones;
            r.rootBone = st.Body.rootBone;
            r.localBounds = st.Body.localBounds;
            r.sharedMaterials = mats;
            CharactersPlugin.Log.LogInfo($"[skin] garment {clothId} -> {def.Mesh} ({d.VertexCount} verts, {mats.Length} slots, template {(template ? template.shader.name : "-")})");
        }

        public static void Revert(AppliedSkin st)
        {
            if (st == null) return;
            if (st.Body && st.VanillaBodyMesh)
            {
                st.Body.sharedMesh = st.VanillaBodyMesh;
                st.Body.bones = st.VanillaBodyBones;
                st.Body.sharedMaterials = st.VanillaBodyMats;
            }
            if (st.VanillaHair) st.VanillaHair.enabled = true;
            foreach (var go in st.Created) if (go) UnityEngine.Object.Destroy(go);
            var cc = UnityEngine.Object.FindFirstObjectByType<ClothesController>();
            if (cc)
                foreach (var a in cc.LoadedAssets.Where(x => x.SkinnedObject))
                    if (st.Garments.TryGetValue(a.SkinnedObject.GetInstanceID(), out var g)) { a.SkinnedObject.sharedMesh = g.mesh; a.SkinnedObject.sharedMaterials = g.mats; a.SkinnedObject.bones = g.bones; }
            if (st.Actor != null)
            {
                Applied.Remove(st.Actor.ActorID);
                RestoreIdentity(st.Actor.ActorID);
                var anim = st.Body ? st.Body.GetComponentInParent<Animator>() : null;
                if (anim && anim.isActiveAndEnabled && anim.isInitialized) { anim.Rebind(); anim.Update(0f); }
            }
        }

        // ---- identity: name + portraits in ActorDataSO, and whole-word name replacement in translated text
        private static ActorDataSO DataOf(ActorID id) => Resources.FindObjectsOfTypeAll<ActorDataSO>().FirstOrDefault(x => x.ID == id);

        public static void ApplyIdentity(ActorID id, SkinPack pack)
        {
            var so = DataOf(id);
            if (so && !VanillaData.ContainsKey(id)) VanillaData[id] = (so.Name, so.Portrait, so.Portrait2, so.ConditionalPortraits);
            if (so)
            {
                if (pack.Def.Portraits.TryGetValue("Portrait", out var p1) && pack.Sprites.TryGetValue(p1, out var s1)) Traverse.Create(so).Field("<Portrait>k__BackingField").SetValue(s1);
                if (pack.Def.Portraits.TryGetValue("Portrait2", out var p2) && pack.Sprites.TryGetValue(p2, out var s2)) Traverse.Create(so).Field("<Portrait2>k__BackingField").SetValue(s2);
                // house HUD: one portrait per alert level; keep the vanilla array length, repeat the last skin portrait if short
                var vc = VanillaData[id].cond;
                var cond = new List<Sprite>();
                for (int i = 0; pack.Def.Portraits.TryGetValue("Cond" + i, out var pc) && pack.Sprites.TryGetValue(pc, out var sc); i++) cond.Add(sc);
                if (cond.Count > 0 && vc != null)
                    Traverse.Create(so).Field("<ConditionalPortraits>k__BackingField").SetValue(Enumerable.Range(0, vc.Length).Select(i => cond[Math.Min(i, cond.Count - 1)]).ToArray());
            }
            // ActorDataSO.Name is a localization key (Russian, e.g. "Камила"); the player sees its translation ("Camila").
            // Every spelling is rewritten: the key, its current translation and the enum name. ActorDataSO.Name itself stays,
            // so lookups keep working; translated output is rewritten instead (TranslatePostfix).
            var vanillaName = VanillaData.TryGetValue(id, out var v) && !string.IsNullOrEmpty(v.name) ? v.name : id.ToString();
            _nameRx = null;
            var names = new HashSet<string> { vanillaName, id.ToString() };
            try
            {
                var lm = UnityEngine.Object.FindFirstObjectByType<LocalizationManager>();
                if (lm && lm.IsInitialized) names.Add(lm.Translate(vanillaName));
            }
            catch { /* translation optional */ }
            names.RemoveWhere(string.IsNullOrEmpty);
            _nameRx = new Regex(@"\b(" + string.Join("|", names.OrderByDescending(n => n.Length).Select(Regex.Escape)) + @")\b", RegexOptions.CultureInvariant);
            _displayName = pack.Def.DisplayName;
            // Other plugins (the Expansion journal) read this map by reflection to show the same name.
            foreach (var n in names) DisplayNames[n] = pack.Def.DisplayName;
        }

        public static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>();

        public static void RestoreIdentity(ActorID id)
        {
            var so = DataOf(id);
            if (so && VanillaData.TryGetValue(id, out var v))
            {
                Traverse.Create(so).Field("<Portrait>k__BackingField").SetValue(v.p1);
                Traverse.Create(so).Field("<Portrait2>k__BackingField").SetValue(v.p2);
                Traverse.Create(so).Field("<ConditionalPortraits>k__BackingField").SetValue(v.cond);
            }
            // put the vanilla name back into texts already on screen
            if (_nameRx != null && VanillaData.TryGetValue(id, out var vd) && !string.IsNullOrEmpty(_displayName))
            {
                _nameRx = new Regex($@"\b{Regex.Escape(_displayName)}\b", RegexOptions.CultureInvariant);
                var keep = _displayName;
                var back = string.IsNullOrEmpty(vd.name) ? id.ToString() : vd.name;
                var lm = UnityEngine.Object.FindFirstObjectByType<LocalizationManager>();
                var rx = _nameRx;
                _nameRx = null;   // Translate must not rewrite while we look the vanilla name up
                try { if (lm && lm.IsInitialized) back = lm.Translate(back); } catch { }
                _nameRx = rx;
                _displayName = back;
                SweepTexts();
                _displayName = keep;
            }
            _nameRx = null;
            DisplayNames.Clear();
        }

        public static string ReplaceName(string s) => _nameRx != null && !string.IsNullOrEmpty(s) ? _nameRx.Replace(s, _displayName) : s;

        private static void TranslatePostfix(ref string __result) { if (_nameRx != null) __result = ReplaceName(__result); }

        private static void SweepPostfix(MonoBehaviour __instance) { if (_nameRx != null) SweepTexts(__instance.transform); }

        // Texts created before the skin was applied (house HUD portraits are built at scene load) keep the vanilla name;
        // rewrite them once. root = null sweeps every loaded TMP text.
        public static int SweepTexts(Transform root = null)
        {
            if (_nameRx == null) return 0;
            int n = 0;
            var texts = root ? root.GetComponentsInChildren<TMPro.TMP_Text>(true) : Resources.FindObjectsOfTypeAll<TMPro.TMP_Text>();
            foreach (var t in texts)
            {
                if (!t || string.IsNullOrEmpty(t.text) || !t.gameObject.scene.IsValid()) continue;
                var r = ReplaceName(t.text);
                if (r != t.text) { t.text = r; n++; }
            }
            return n;
        }
    }
}
