using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Game.Entities;
using Game.Input;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MSN2CommunityPatch.Graphics
{
    // Optional CPU/GPU savings found by runtime stats (SceneStats): hundreds of props animate even when off screen, and thousands of
    // renderers cast shadows, many of them tiny. Applied once per Game-scene load (SceneRuntime) and re-applied or reverted when the
    // config value changes; nothing runs per frame. Original values are kept, so turning an option off restores vanilla exactly.
    public static class PerfTweaks
    {
        internal static ConfigEntry<bool> AnimatorCulling, SmallObjectShadows;
        internal static ConfigEntry<float> SmallShadowMaxSize;

        private static readonly Dictionary<Animator, AnimatorCullingMode> CulledAnimators = new Dictionary<Animator, AnimatorCullingMode>();
        private static readonly Dictionary<Renderer, ShadowCastingMode> ShadowlessRenderers = new Dictionary<Renderer, ShadowCastingMode>();

        public static void Bind(ConfigFile c)
        {
            AnimatorCulling = c.Bind("Performance", "AnimatorCulling", true,
                "Props (drawers, cabinets, windows, wardrobe doors) stop updating their animated pose while off screen; their state machines keep running. Characters, the camera, UI and the doors NPCs walk through are never touched.");
            SmallObjectShadows = c.Bind("Performance", "SmallObjectShadows", false,
                "Stop tiny static props (smaller than SmallShadowMaxSize) from casting shadows. Characters and movable items are never touched.");
            SmallShadowMaxSize = c.Bind("Performance", "SmallShadowMaxSize", 0.15f,
                new ConfigDescription("Largest dimension in metres of a prop that SmallObjectShadows may change.", new AcceptableValueRange<float>(0.05f, 0.5f)));
            AnimatorCulling.SettingChanged += (s, e) => ApplyAnimators();
            SmallObjectShadows.SettingChanged += (s, e) => ApplyShadows();
            SmallShadowMaxSize.SettingChanged += (s, e) => { RevertShadows(); ApplyShadows(); };
        }

        // Called by SceneRuntime once per Game-scene load.
        public static void OnGameSceneReady()
        {
            CulledAnimators.Clear();      // previous scene's objects are destroyed
            ShadowlessRenderers.Clear();
            ApplyAnimators();
            ApplyShadows();
        }

        private static bool InGame => SceneManager.GetActiveScene().name == "Game";

        // Characters (NPC actors and the player) and camera rigs must keep exact poses: gameplay reads bone positions (look-at, sight
        // checks, sex/fap scenes). CullUpdateTransforms (never CullCompletely) keeps state machines and animation events running.
        internal static bool IsProtected(Component c) => Protected(c);

        private static bool Protected(Component c)
        {
            if (c.GetComponentInParent<IActorEntity>(true) != null) return true;
            if (c.GetComponentInParent<PlayerEntity>(true) != null) return true;
            if (c.GetComponentInParent<FirstPersonController>(true) != null) return true;
            if (c.GetComponentInParent<Camera>(true) != null) return true;
            // UI animators have no Renderer, so Unity would treat them as always off screen and freeze them.
            if (c.transform is RectTransform || c.GetComponentInParent<Canvas>(true) != null) return true;
            for (var t = c.transform; t != null; t = t.parent)
                foreach (var m in t.GetComponents<MonoBehaviour>())
                    if (m != null && m.GetType().FullName.IndexOf("Cinemachine", System.StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        // Doors carve the NPC nav mesh with a NavMeshObstacle that rides on the animated leaf; a stale off-screen pose would carve the
        // doorway itself and block NPC paths, so any entity with an obstacle keeps animating everywhere.
        internal static bool DrivesNavMesh(Component c)
        {
            var root = (Component)c.GetComponentInParent<Entity>(true) ?? c;
            return root.GetComponentInChildren<UnityEngine.AI.NavMeshObstacle>(true) != null;
        }

        public static void ApplyAnimators()
        {
            if (!InGame) return;
            if (!AnimatorCulling.Value) { RevertAnimators(); return; }
            int n = 0, skipped = 0;
            foreach (var a in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (a.cullingMode != AnimatorCullingMode.AlwaysAnimate || CulledAnimators.ContainsKey(a)) continue;
                if (Protected(a) || DrivesNavMesh(a)) { skipped++; continue; }
                CulledAnimators[a] = a.cullingMode;
                a.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                n++;
            }
            Plugin.Log.LogInfo($"[perf] animators culled={n} (total {CulledAnimators.Count}, characters/camera/UI/nav-mesh doors kept={skipped})");
        }

        private static void RevertAnimators()
        {
            int n = 0;
            foreach (var kv in CulledAnimators) if (kv.Key) { kv.Key.cullingMode = kv.Value; n++; }
            CulledAnimators.Clear();
            if (n > 0) Plugin.Log.LogInfo($"[perf] animators restored={n}");
        }

        public static IEnumerable<MeshRenderer> SmallStaticCasters(float maxSize) =>
            Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(r => r.shadowCastingMode == ShadowCastingMode.On && (r.isPartOfStaticBatch || r.gameObject.isStatic))
                .Where(r => { var s = r.bounds.size; return Mathf.Max(s.x, Mathf.Max(s.y, s.z)) < maxSize; })
                .Where(r => r.GetComponentInParent<Rigidbody>(true) == null && !Protected(r));

        public static void ApplyShadows()
        {
            if (!InGame) return;
            if (!SmallObjectShadows.Value) { RevertShadows(); return; }
            int n = 0;
            foreach (var r in SmallStaticCasters(SmallShadowMaxSize.Value).ToList())
            {
                if (ShadowlessRenderers.ContainsKey(r)) continue;
                ShadowlessRenderers[r] = r.shadowCastingMode;
                r.shadowCastingMode = ShadowCastingMode.Off;
                n++;
            }
            Plugin.Log.LogInfo($"[perf] shadows off={n} (props < {SmallShadowMaxSize.Value:F2} m)");
        }

        private static void RevertShadows()
        {
            int n = 0;
            foreach (var kv in ShadowlessRenderers) if (kv.Key) { kv.Key.shadowCastingMode = kv.Value; n++; }
            ShadowlessRenderers.Clear();
            if (n > 0) Plugin.Log.LogInfo($"[perf] shadows restored={n}");
        }

        public static object Status() => new
        {
            animatorCulling = AnimatorCulling.Value, animatorsCulled = CulledAnimators.Count,
            smallObjectShadows = SmallObjectShadows.Value, shadowsOff = ShadowlessRenderers.Count, smallShadowMaxSize = SmallShadowMaxSize.Value,
        };
    }
}
