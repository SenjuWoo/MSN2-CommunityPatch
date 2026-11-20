using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace MSN2Characters.Skins
{
    // skin.json schema 1 (written by tools/charport/convert_skin.py).
    public class SlotDef
    {
        [JsonProperty("template")] public string Template;
        [JsonProperty("hide")] public bool Hide;
        [JsonProperty("tex")] public Dictionary<string, string> Tex = new Dictionary<string, string>();
        // source material values; set only where the template shader has the same property (Daz hair/clothes shaders)
        [JsonProperty("floats")] public Dictionary<string, float> Floats;
        [JsonProperty("colors")] public Dictionary<string, float[]> Colors;
        [JsonProperty("srcShader")] public string SrcShader;
    }

    public class MeshDef
    {
        [JsonProperty("mesh")] public string Mesh;
        [JsonProperty("target")] public string Target;
        [JsonProperty("name")] public string Name;
        [JsonProperty("slots")] public Dictionary<string, SlotDef> Slots = new Dictionary<string, SlotDef>();
    }

    public class SkinDef
    {
        [JsonProperty("schema")] public int Schema;
        [JsonProperty("id")] public string Id;
        [JsonProperty("displayName")] public string DisplayName;
        [JsonProperty("replaces")] public string Replaces;
        [JsonProperty("body")] public MeshDef Body;
        [JsonProperty("hair")] public MeshDef Hair;
        [JsonProperty("extra")] public List<MeshDef> Extra = new List<MeshDef>();
        [JsonProperty("clothes")] public Dictionary<string, MeshDef> Clothes = new Dictionary<string, MeshDef>();
        [JsonProperty("portraits")] public Dictionary<string, string> Portraits = new Dictionary<string, string>();

        public IEnumerable<MeshDef> AllMeshes()
        {
            if (Body != null) yield return Body;
            if (Hair != null) yield return Hair;
            foreach (var e in Extra) yield return e;
            foreach (var c in Clothes.Values) yield return c;
        }
    }

    // One skin folder: definition + meshes + textures, loaded once and cached for the session.
    // Loading is spread over frames: msnm parsing and file reads run on worker threads, PNG decode and mesh
    // creation run one item per frame on the main thread.
    public class SkinPack
    {
        public enum LoadState { Loading, Ready, Failed }

        public readonly string Dir;
        public SkinDef Def;
        public LoadState State = LoadState.Loading;
        public string Error;
        public readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        public readonly Dictionary<string, MsnmData> Data = new Dictionary<string, MsnmData>();
        public readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        public readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        public float LoadSeconds;
        public long TextureBytes;

        public SkinPack(string dir) { Dir = dir; }

        public IEnumerator Load()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                Def = JsonConvert.DeserializeObject<SkinDef>(File.ReadAllText(Path.Combine(Dir, "skin.json")));
                if (Def == null || Def.Schema != 1) throw new InvalidDataException("skin.json: schema must be 1");
                if (Def.Body == null) throw new InvalidDataException("skin.json: body missing");
            }
            catch (Exception e) { Fail("skin.json: " + e.Message); yield break; }

            var meshFiles = Def.AllMeshes().Select(m => m.Mesh).Distinct().ToList();
            var parse = Task.Run(() => meshFiles.ToDictionary(f => f, f => MsnmReader.Parse(Path.Combine(Dir, f))));
            var texFiles = Def.AllMeshes().SelectMany(m => m.Slots.Values).Where(s => s.Tex != null).SelectMany(s => s.Tex.Values).Distinct().ToList();
            var portraits = Def.Portraits.Values.Where(p => File.Exists(Path.Combine(Dir, p))).Distinct().ToList();
            var read = Task.Run(() => texFiles.Concat(portraits).Distinct().ToDictionary(f => f, f => File.ReadAllBytes(Path.Combine(Dir, f))));
            while (!parse.IsCompleted || !read.IsCompleted) yield return null;
            if (parse.IsFaulted) { Fail("mesh: " + parse.Exception.GetBaseException().Message); yield break; }
            if (read.IsFaulted) { Fail("texture file: " + read.Exception.GetBaseException().Message); yield break; }
            foreach (var kv in parse.Result) Data[kv.Key] = kv.Value;

            foreach (var kv in read.Result)
            {
                try
                {
                    // "_n.png" = tangent-space normal map: linear, no sRGB conversion
                    bool normal = kv.Key.EndsWith("_n.png", StringComparison.OrdinalIgnoreCase);
                    var t = new Texture2D(2, 2, TextureFormat.RGBA32, true, normal) { name = Path.GetFileNameWithoutExtension(kv.Key) };
                    if (!t.LoadImage(kv.Value, false)) throw new InvalidDataException("not a PNG");
                    t.wrapMode = TextureWrapMode.Repeat;   // UDIM tiles: body UVs keep their tile offset (u in 0..7)
                    t.anisoLevel = 4;
                    bool isPortrait = portraits.Contains(kv.Key);
                    if (!isPortrait && t.width % 4 == 0 && t.height % 4 == 0) t.Compress(true);
                    t.Apply(true, !isPortrait);
                    Textures[kv.Key] = t;
                    // GPU size estimate (the profiler API reports 0 in release players): DXT1 0.5, DXT5 1, RGBA32 4 bytes/px, +1/3 for mips
                    float bpp = t.format == TextureFormat.DXT1 ? 0.5f : t.format == TextureFormat.DXT5 ? 1f : 4f;
                    TextureBytes += (long)(t.width * t.height * bpp * 4f / 3f);
                    if (isPortrait) Sprites[kv.Key] = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
                }
                catch (Exception e) { Fail($"texture {kv.Key}: {e.Message}"); yield break; }
                yield return null;
            }

            foreach (var def in Def.AllMeshes())
            {
                if (Meshes.ContainsKey(def.Mesh)) continue;
                try
                {
                    var d = Data[def.Mesh];
                    var mesh = MsnmReader.Build(d, $"{Def.Id}_{Path.GetFileNameWithoutExtension(def.Mesh)}");
                    // hidden slots keep their submesh index (materials stay aligned) but draw nothing
                    for (int i = 0; i < d.SlotNames.Length; i++)
                        if (def.Slots.TryGetValue(d.SlotNames[i], out var s) && s.Hide) mesh.SetTriangles(new int[0], i, false);
                    Meshes[def.Mesh] = mesh;
                }
                catch (Exception e) { Fail($"mesh {def.Mesh}: {e.Message}"); yield break; }
                yield return null;
            }
            LoadSeconds = (float)sw.Elapsed.TotalSeconds;
            State = LoadState.Ready;
            CharactersPlugin.Log.LogInfo($"[skin] {Def.Id} loaded in {LoadSeconds:F1}s: {Meshes.Count} meshes ({Data.Values.Sum(d => d.VertexCount):N0} verts), {Textures.Count} textures ({TextureBytes / 1048576f:F0} MB)");
        }

        private void Fail(string msg)
        {
            State = LoadState.Failed;
            Error = msg;
            CharactersPlugin.Log.LogError($"[skin] {Path.GetFileName(Dir)} load failed: {msg}; the actor stays vanilla");
        }

        public void Unload()
        {
            foreach (var m in Meshes.Values) if (m) UnityEngine.Object.Destroy(m);
            foreach (var s in Sprites.Values) if (s) UnityEngine.Object.Destroy(s);
            foreach (var t in Textures.Values) if (t) UnityEngine.Object.Destroy(t);
            Meshes.Clear(); Textures.Clear(); Sprites.Clear(); Data.Clear();
        }
    }
}
