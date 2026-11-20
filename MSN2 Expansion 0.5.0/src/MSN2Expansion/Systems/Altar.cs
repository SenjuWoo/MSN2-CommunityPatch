using System;
using System.Collections.Generic;
using System.Linq;
using Game.Entities;
using UnityEngine;

namespace MSN2Expansion.Systems
{
    // Souvenir altar (MSN1 AltharController): stolen souvenirs stand as trophies on the bookshelf in the player's room.
    // Each trophy is a copy of the stolen item's own mesh, borrowed from any stealable of the same kind in the scene.
    public static class Altar
    {
        // the bookshelf the player faces at the start of a day, in the player's room (measured in game: see TEST_RESULTS 0.5.0)
        public static Vector3 ShelfOrigin = new Vector3(6.56f, 2.45f, 20.74f);
        public static Vector3 ShelfRight = Quaternion.Euler(0, 65f, 0) * Vector3.right;
        public static float Spacing = 0.33f;   // vertical distance between the two shelf boards used
        private static GameObject _root;
        public static int Shown;

        public static float MoneyMultiplier() => SystemsConfig.Altar == null || !SystemsConfig.Altar.Value ? 1f : 1f + Math.Min(0.2f, 0.02f * Kinds().Count);

        public static List<string> Kinds() => Sidecar.State.Trophies.Distinct().ToList();

        public static void Build()
        {
            if (_root) UnityEngine.Object.Destroy(_root);
            Shown = 0;
            if (!SystemsConfig.Altar.Value) return;
            var kinds = Kinds();
            if (kinds.Count == 0) return;
            _root = new GameObject("MSN2Expansion.SouvenirAltar");
            _root.transform.position = ShelfOrigin;
            var prefabs = Prefabs();
            var stealables = UnityEngine.Object.FindObjectsByType<StealableEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int i = 0;
            foreach (var kind in kinds.Take(12))
            {
                // the entity prefab from the game's EntitiesRepositorySO (souvenirs only exist in the scene while a theft task is on)
                prefabs.TryGetValue(kind, out var src);
                if (!src) src = stealables.FirstOrDefault(s => s && s.Data != null && s.Data.GetType().Name.Replace("StealableEntityData", "") == kind)?.gameObject;
                if (!src) continue;
                var copy = CopyVisual(src, kind);
                if (!copy) continue;
                // two rows of six, 10 cm apart, each item scaled to fit a 9 cm box
                int row = i / 6, col = i % 6;
                copy.transform.position = ShelfOrigin + ShelfRight * (col * 0.1f - 0.25f) + Vector3.up * (-row * Spacing);
                copy.name += $"_{row}_{col}";
                copy.transform.rotation = Quaternion.LookRotation(Vector3.Cross(ShelfRight, Vector3.up));
                Fit(copy, 0.11f);
                i++;
            }
            Shown = i;
            ExpansionPlugin.Log.LogInfo($"[altar] {Shown} trophies on the shelf ({kinds.Count} kinds stolen); job money x{MoneyMultiplier():0.00}");
        }

        private static Dictionary<string, GameObject> Prefabs()
        {
            var d = new Dictionary<string, GameObject>();
            var repo = Resources.FindObjectsOfTypeAll<EntitiesRepositorySO>().FirstOrDefault();
            if (!repo) return d;
            foreach (var it in repo)
            {
                var type = it?.EntityDataType?.Type;
                if (it?.Entity == null || type == null || !type.Name.EndsWith("StealableEntityData")) continue;
                d[type.Name.Replace("StealableEntityData", "")] = it.Entity.gameObject;
            }
            return d;
        }

        private static GameObject CopyVisual(GameObject src, string kind)
        {
            var go = new GameObject("Trophy_" + kind);
            go.transform.SetParent(_root.transform, false);
            int parts = 0;
            foreach (var mf in src.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (!mr || !mf.sharedMesh) continue;
                var p = new GameObject(mf.name);
                p.transform.SetParent(go.transform, false);
                // keep the parts' layout relative to the source root
                p.transform.localPosition = src.transform.InverseTransformPoint(mf.transform.position);
                p.transform.localRotation = Quaternion.Inverse(src.transform.rotation) * mf.transform.rotation;
                p.transform.localScale = mf.transform.lossyScale;
                p.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                p.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
                parts++;
            }
            if (parts == 0) { UnityEngine.Object.Destroy(go); return null; }
            return go;
        }

        private static void Fit(GameObject go, float size)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            float m = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (m > 0.0001f) go.transform.localScale *= size / m;
            // stand it on the shelf: bottom of the bounds at the anchor height
            rs = go.GetComponentsInChildren<Renderer>();
            b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            go.transform.position += Vector3.up * (go.transform.position.y - b.min.y);
        }
    }
}
