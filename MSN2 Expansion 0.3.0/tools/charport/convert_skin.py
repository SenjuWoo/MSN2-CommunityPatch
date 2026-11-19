"""Convert an MSN1 character (default: Lola) into an MSN2 skin pack (msnm meshes + PNG textures + skin.json).

usage:
  python -I convert_skin.py --msn1 "<MSN1>/My Sexy Neighbor_Data" --msn2 "<MSN2>/My Sexy Neighbour 2_Data" --out skins/lola
         [--skin-json skins/lola/skin.json] [--only-extract] [--no-textures]

Pipeline (PLAN.md 0.3.0 "Conversion pipeline"):
 1. extract the source character's SMRs (mesh, weights, bindposes, shapes, materials) from MSN1;
 2. read the target rig (Camila's body SMR bone names + bindposes) from MSN2;
 3. conform by translation-only skinning offsets: v' = v + sum_i w_i (c_map(i) - l_i); Genitals/Anus use the pelvis offset;
 4. rebind onto target bone names with the target bindposes; 5. add blendshape aliases;
 6. export textures (normal maps rebuilt as standard RGB tangent-space normals); 7. same for hair/scalp/outfits;
 8. write manifest.json (sha256 of every output) and print a report.
Raw extraction is cached in <out>/.build so re-runs skip the 2 GB archives. UnityPy rule: the native typetree reader is
disabled (it crashes on this game's MonoBehaviours) and pointers are resolved per assets file.
"""
import argparse
import gc
import hashlib
import json
import os
import pickle
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import msnm  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
MSN2_SCENE_BUNDLE = "StreamingAssets/aa/StandaloneWindows64/839cebfdebea6683d9dd62dba1291e21.bundle"
TARGET_ACTOR = "CamilaAI Variant"
TARGET_BODY_SMR = "Catara Mesh.001"
SOURCE_ACTOR = "Lola"
PELVIS_OVERRIDE = {"Genitals", "Anus"}   # source bones that take the pelvis offset (5 cm joint mismatch tears the crotch)
SOURCE_PELVIS = "DEF-spine"
# alias -> (source shape, scale): genital morphs Camila's animations drive that Lola lacks
SHAPE_ALIASES = {
    "PBMGensPullOutE": ("PBMGensPullOut", 1.0),
    "PBMG8FAnusOpen": ("PBMAnusOpen", 1.0),
    "PBMGensOpen03": ("PBMGensOpen01", 1.0),
    "PBMAnusStyle03": ("PBMAnusOpen", 0.5),
}
# Source SMR -> output file. Outfit groups merge several SMRs into one msnm (each keeps its submeshes).
MESH_GROUPS = {
    "body.msnm": ["Body"],
    "hair.msnm": ["Hair"],
    "scalp.msnm": ["Scalp"],
    "clothes/yoshino_shirt.msnm": ["Yoshino Shirt Mesh"],
    "clothes/yoshino_skirt_belt.msnm": ["Yoshino Skirt Mesh", "Yoshino Belt Mesh"],
    "clothes/yoshino_shoes_socks.msnm": ["Yoshino Shoes Mesh", "Yoshino Socks Mesh"],
    # Lola's MSN1 night look: lace brami + thong + slippers (her "Night Shirt Slippers" SMR is only the slippers)
    "clothes/nightie.msnm": ["Sweet Brami", "GR Thong", "Night Shirt Slippers"],
}
# skin.json written next to the meshes. Body slots name the Camila material they clone ("template"); every
# texture the source material had is listed under its own property name plus the aliases in PROP_ALIASES, and the
# runtime sets the ones the template's shader has. "@garment" = the vanilla garment's own material.
SKIN = {
    "id": "lola", "displayName": "Lola", "replaces": "Camila",
    "body": {"mesh": "body.msnm", "target": "Catara Mesh.001", "templates": {
        "Pupils": "Eyes", "EyeMoisture": None, "Tear": None, "Eyelashes_UP": "Eyelashes", "Luna_Brows": "Eyelashes", "*": "Body"}},
    "hair": {"mesh": "hair.msnm", "target": "TG4 Female Hair 07 (Genesis 8 Female)", "templates": {"*": "Hair"}},
    "extra": [{"mesh": "scalp.msnm", "name": "Lola_Scalp", "templates": {"*": "Hair"}}],
    "clothes": {
        "Camila_Casual_TankTop": "clothes/yoshino_shirt.msnm",
        "Camila_Casual_Shorts": "clothes/yoshino_skirt_belt.msnm",
        "Camila_Casual_Sneakers": "clothes/yoshino_shoes_socks.msnm",
        "Camila_Nightie_Nightie": "clothes/nightie.msnm",
    },
}
FULLRES_TILES = (".1001", ".1002")   # face + head keep up to 4096
NORMAL_PROPS = ("_NormalMap", "_BumpMap", "_Legwear_N", "_DetailNormalMap")
SKIP_PROPS = ("_TranslucencyColorMap", "_SpecularColorMap", "_SpecularStrengthMap", "_MetallicMap", "_RoughnessMap")
PROP_ALIASES = {"_DiffuseMap": ["_BaseMap", "_MainTex"], "_NormalMap": ["_BumpMap"]}


def log(*a):
    print(*a, flush=True)


# ---------------------------------------------------------------- UnityPy helpers
def unitypy():
    import UnityPy
    from UnityPy.helpers import TypeTreeHelper
    TypeTreeHelper.read_typetree_boost = None
    return UnityPy


class Files:
    """Objects indexed per assets file, with per-file pointer resolution (FileID n -> externals[n-1])."""

    def __init__(self, env):
        self.byf = {}
        for o in env.objects:
            self.byf.setdefault(o.assets_file, {})[o.path_id] = o
        self.byname = {af.name.lower(): af for af in self.byf}

    def res(self, af, ptr):
        if ptr is None or ptr.m_PathID == 0:
            return None
        if ptr.m_FileID == 0:
            f = af
        else:
            nm = af.externals[ptr.m_FileID - 1].path.replace("\\", "/").split("/")[-1].lower()
            f = self.byname.get(nm)
        return self.byf.get(f, {}).get(ptr.m_PathID) if f is not None else None

    def comps(self, go_reader):
        go = go_reader.read()
        out = []
        for c in go.m_Component:
            p = getattr(c, "component", None) or (c[1] if isinstance(c, tuple) else c)
            r = self.res(go_reader.assets_file, p)
            if r is not None:
                out.append(r)
        return out

    def transform_of(self, go_reader):
        return next(r for r in self.comps(go_reader) if r.type.name in ("Transform", "RectTransform"))

    def find_go(self, name):
        for af, objs in self.byf.items():
            for o in objs.values():
                if o.type.name == "GameObject" and o.peek_name() == name:
                    yield o

    def walk(self, tr_reader):
        """Yield (go_reader, transform_reader) for the subtree."""
        stack = [tr_reader]
        while stack:
            t = stack.pop()
            tr = t.read()
            gor = self.res(t.assets_file, tr.m_GameObject)
            yield gor, t
            for ch in tr.m_Children:
                c = self.res(t.assets_file, ch)
                if c is not None:
                    stack.append(c)

    def go_name_of_transform(self, af, ptr):
        t = self.res(af, ptr)
        if t is None:
            return None
        return self.res(t.assets_file, t.read().m_GameObject).read().m_Name


def mat44(m):
    return np.array([[m.e00, m.e01, m.e02, m.e03], [m.e10, m.e11, m.e12, m.e13],
                     [m.e20, m.e21, m.e22, m.e23], [m.e30, m.e31, m.e32, m.e33]], np.float64)


def decode_mesh(mesh):
    from UnityPy.helpers.MeshHelper import MeshHandler
    h = MeshHandler(mesh)
    h.process()
    V = h.m_VertexCount
    pos = np.asarray(h.m_Vertices, np.float32).reshape(V, -1)[:, :3]
    nrm = np.asarray(h.m_Normals, np.float32).reshape(V, -1)[:, :3] if h.m_Normals else np.zeros((V, 3), np.float32)
    tan = np.asarray(h.m_Tangents, np.float32).reshape(V, 4) if h.m_Tangents else None
    uv = np.asarray(h.m_UV0, np.float32).reshape(V, 2) if h.m_UV0 else np.zeros((V, 2), np.float32)
    bi = np.asarray(h.m_BoneIndices, np.int64).reshape(V, -1) if h.m_BoneIndices else None
    bw = np.asarray(h.m_BoneWeights, np.float32).reshape(V, -1) if h.m_BoneWeights else None
    tris = h.get_triangles()
    subs = [np.asarray(t, np.uint32).reshape(-1) for t in tris]
    shapes = []
    sd = mesh.m_Shapes
    if sd is not None and getattr(sd, "channels", None):
        verts = sd.vertices
        for ch in sd.channels:
            fi = ch.frameIndex + ch.frameCount - 1        # last frame = full weight
            sh = sd.shapes[fi]
            vs = verts[sh.firstVertex: sh.firstVertex + sh.vertexCount]
            idx = np.fromiter((v.index for v in vs), np.uint32, len(vs))
            dv = np.array([(v.vertex.x, v.vertex.y, v.vertex.z) for v in vs], np.float32).reshape(-1, 3)
            dn = np.array([(v.normal.x, v.normal.y, v.normal.z) for v in vs], np.float32).reshape(-1, 3) if sh.hasNormals else np.zeros((len(vs), 3), np.float32)
            shapes.append((ch.name, idx, dv, dn, float(sd.fullWeights[fi])))
    bind = np.stack([mat44(b) for b in mesh.m_BindPose]) if mesh.m_BindPose else np.zeros((0, 4, 4))
    return dict(name=mesh.m_Name, pos=pos, nrm=nrm, tan=tan, uv=uv, bi=bi, bw=bw, subs=subs, shapes=shapes, bind=bind)


# ---------------------------------------------------------------- extraction
def extract_target(msn2_data, cache):
    path = os.path.join(cache, "target.pkl")
    if os.path.exists(path):
        return pickle.load(open(path, "rb"))
    UnityPy = unitypy()
    t0 = time.time()
    env = UnityPy.load(os.path.join(msn2_data, MSN2_SCENE_BUNDLE))
    F = Files(env)
    out = None
    for go in F.find_go(TARGET_ACTOR):
        for gor, tr in F.walk(F.transform_of(go)):
            if gor.read().m_Name != TARGET_BODY_SMR:
                continue
            smr_r = next(r for r in F.comps(gor) if r.type.name == "SkinnedMeshRenderer")
            smr = smr_r.read()
            bones = [F.go_name_of_transform(smr_r.assets_file, b) for b in smr.m_Bones]
            mesh = F.res(smr_r.assets_file, smr.m_Mesh).read()
            bind = np.stack([mat44(b) for b in mesh.m_BindPose])
            out = dict(bones=bones, bind=bind, mesh=mesh.m_Name, shapes=[c.name for c in mesh.m_Shapes.channels])
            break
        if out:
            break
    if out is None:
        raise SystemExit(f"target {TARGET_ACTOR}/{TARGET_BODY_SMR} not found")
    del env, F
    gc.collect()
    pickle.dump(out, open(path, "wb"))
    log(f"[target] {out['mesh']}: {len(out['bones'])} bones, {len(out['shapes'])} shapes ({time.time() - t0:.0f}s)")
    return out


def tex_role(prop):
    return "normal" if prop in NORMAL_PROPS else "color"


def extract_source(msn1_data, cache, want_textures):
    path = os.path.join(cache, "source.pkl")
    if os.path.exists(path):
        return pickle.load(open(path, "rb"))
    UnityPy = unitypy()
    t0 = time.time()
    files = [os.path.join(msn1_data, n) for n in ("data.unity3d", "sharedassets0.resource", "resources.resource")]
    env = UnityPy.load(*[f for f in files if os.path.exists(f)])
    F = Files(env)
    go = next(F.find_go(SOURCE_ACTOR), None)
    if go is None:
        raise SystemExit(f"source actor {SOURCE_ACTOR} not found")
    wanted = {n for g in MESH_GROUPS.values() for n in g}
    smrs, textures, parents = {}, {}, {}
    for gor, tr in F.walk(F.transform_of(go)):
        name = gor.read().m_Name
        father = tr.read().m_Father
        if father is not None and father.m_PathID:
            parents.setdefault(name, F.go_name_of_transform(tr.assets_file, father))
        if name not in wanted or name in smrs:
            continue
        smr_r = next((r for r in F.comps(gor) if r.type.name == "SkinnedMeshRenderer"), None)
        if smr_r is None:
            continue
        smr = smr_r.read()
        mesh = F.res(smr_r.assets_file, smr.m_Mesh).read()
        d = decode_mesh(mesh)
        d["bones"] = [F.go_name_of_transform(smr_r.assets_file, b) for b in smr.m_Bones]
        d["default_weights"] = [float(w) for w in (smr.m_BlendShapeWeights or [])]
        mats = []
        for mp in smr.m_Materials:
            mr = F.res(smr_r.assets_file, mp)
            if mr is None:
                mats.append({"name": None, "tex": {}})
                continue
            mat = mr.read()
            tex = {}
            for k, te in mat.m_SavedProperties.m_TexEnvs:
                t = F.res(mr.assets_file, te.m_Texture)
                if t is None or k in SKIP_PROPS:
                    continue
                tname = t.peek_name()
                tex[k] = tname
                key = (tname, tex_role(k))
                if want_textures and key not in textures:
                    T = t.read()
                    textures[key] = dict(fmt=str(T.m_TextureFormat), img=T.image.copy())
            sh = F.res(mr.assets_file, mat.m_Shader)
            try:
                shader = sh.read().m_ParsedForm.m_Name if sh is not None else None
            except Exception:
                shader = None
            floats = {k: float(v) for k, v in mat.m_SavedProperties.m_Floats}
            colors = {k: [float(c.r), float(c.g), float(c.b), float(c.a)] for k, c in mat.m_SavedProperties.m_Colors}
            mats.append({"name": mat.m_Name, "tex": tex, "shader": shader, "floats": floats, "colors": colors})
        d["materials"] = mats
        smrs[name] = d
        log(f"[source] {name}: {len(d['pos'])} verts, {len(d['subs'])} subs, {len(d['shapes'])} shapes, {len(d['bones'])} bones")
    missing = wanted - set(smrs)
    if missing:
        log(f"[source] WARNING missing SMRs: {sorted(missing)}")
    out = dict(smrs=smrs, textures=textures, parents=parents)
    del env, F
    gc.collect()
    pickle.dump(out, open(path, "wb"), protocol=4)
    log(f"[source] extracted {len(smrs)} SMRs, {len(textures)} textures ({time.time() - t0:.0f}s)")
    return out


# ---------------------------------------------------------------- conform + rebind
def joints_from_bind(bones, bind):
    return {b: np.linalg.inv(m)[:3, 3] for b, m in zip(bones, bind)}


def build_source_joints(smrs):
    """Joint position per source bone (inverse bindpose translation). Body wins; report disagreement between meshes."""
    J, spread = {}, {}
    order = ["Body"] + [n for n in smrs if n != "Body"]
    for n in order:
        d = smrs[n]
        for b, p in joints_from_bind(d["bones"], d["bind"]).items():
            if b in J:
                spread[b] = max(spread.get(b, 0.0), float(np.linalg.norm(J[b] - p)))
            else:
                J[b] = p
    return J, spread


def effective_map(bone_map, parents, bones):
    """Source bone -> target bone. Bones without a map entry (hair physics, face rig helpers) follow their nearest
    mapped ancestor, so they become rigid with it. Returns (map, fallbacks)."""
    eff, fallback = {}, {}
    for b in bones:
        if b in bone_map:
            eff[b] = bone_map[b]
            continue
        p = parents.get(b)
        while p is not None and p not in bone_map:
            p = parents.get(p)
        if p is not None:
            eff[b] = bone_map[p]
            fallback[b] = p
    return eff, fallback


def canonical_sources(bone_map, src_J, tgt_J):
    """Target bone -> the source bone that is its true counterpart: among the source bones mapped onto it, the one whose
    joint is nearest. Bones merged onto a target (toes -> ToeBase) take the counterpart's offset instead of their own,
    which would otherwise collapse them onto the target joint."""
    best = {}
    for s, t in bone_map.items():
        if s not in src_J or t not in tgt_J:
            continue
        dist = float(np.linalg.norm(src_J[s] - tgt_J[t]))
        if t not in best or dist < best[t][1]:
            best[t] = (s, dist)
    return {t: s for t, (s, _) in best.items()}


def bake_default_shapes(d):
    """MSN1 keeps some shapes switched on in the renderer (Lola: "Armature SkinBinding" and "hairClipFix" at 100), so
    her real look includes them. MSN2 animations never drive them, so bake them into the base mesh and drop them."""
    w = d.get("default_weights") or []
    keep, baked = [], []
    pos, nrm = d["pos"].astype(np.float64), d["nrm"].astype(np.float64)
    for i, s in enumerate(d["shapes"]):
        name, idx, dv, dn, fw = s
        wt = w[i] if i < len(w) else 0.0
        if wt > 0:
            k = wt / (fw or 100.0)
            pos[idx] += dv * k
            nrm[idx] += dn * k
            baked.append(f"{name}@{wt:g}")
        else:
            keep.append(s)
    if baked:
        n = np.linalg.norm(nrm, axis=1, keepdims=True)
        n[n == 0] = 1
        d["pos"], d["nrm"], d["shapes"] = pos.astype(np.float32), (nrm / n).astype(np.float32), keep
    return baked


def conform_mesh(d, bone_map, src_J, tgt_J, tgt_index, tgt_bind, parents=None, canon=None):
    eff, fallback = effective_map(bone_map, parents or {}, d["bones"])
    bones = d["bones"]
    unmapped = sorted({b for b in bones if b not in eff})
    if unmapped:
        return None, unmapped
    pelvis_off = tgt_J[bone_map[SOURCE_PELVIS]] - src_J[SOURCE_PELVIS] if SOURCE_PELVIS in src_J else None
    bone_map = eff
    off = np.zeros((len(bones), 3))
    for i, b in enumerate(bones):
        t = bone_map[b]
        if b in PELVIS_OVERRIDE and pelvis_off is not None:
            off[i] = pelvis_off
        else:
            off[i] = tgt_J[t] - src_J[canon.get(t, b) if canon else b]
    bi, bw = d["bi"], d["bw"].astype(np.float64)
    V = len(d["pos"])
    vo = np.einsum("vk,vkc->vc", bw, off[bi])
    pos = (d["pos"] + vo).astype(np.float32)
    # rebind: source bone index -> target bone name; merge duplicates, keep top 4, renormalise
    tnames = [bone_map[b] for b in bones]
    used = sorted(set(tnames[i] for i in np.unique(bi[bw > 0])), key=lambda n: tgt_index[n])
    local = {n: i for i, n in enumerate(used)}
    remap = np.array([local.get(n, 0) for n in tnames])
    ti = remap[bi]
    K = len(used)
    dense = np.zeros((V, K))
    for k in range(bi.shape[1]):
        np.add.at(dense, (np.arange(V), ti[:, k]), bw[:, k])
    top = np.argsort(-dense, axis=1, kind="stable")[:, :4]
    tw = np.take_along_axis(dense, top, axis=1)
    s = tw.sum(1, keepdims=True)
    s[s == 0] = 1
    tw = tw / s
    top[tw == 0] = 0
    bind = np.stack([tgt_bind[tgt_index[n]] for n in used]).astype(np.float32)
    per_bone = {}
    for i, b in enumerate(bones):
        per_bone[f"{b}->{bone_map[b]}"] = float(np.linalg.norm(off[i]))
    return dict(pos=pos, bones=used, bind=bind, bi=top.astype(np.uint16), bw=tw.astype(np.float32),
                max_vertex_offset=float(np.linalg.norm(vo, axis=1).max()), per_bone=per_bone, fallback=fallback), []


def merge_group(parts):
    """Concatenate several conformed SMRs (outfit group) into one mesh, unifying bone lists."""
    if len(parts) == 1:
        return parts[0]
    bones = []
    for c, _ in parts:
        for b in c["bones"]:
            if b not in bones:
                bones.append(b)
    bind = {}
    for c, _ in parts:
        for b, m in zip(c["bones"], c["bind"]):
            bind[b] = m
    out = dict(pos=[], nrm=[], tan=[], uv=[], bi=[], bw=[], subs=[], shapes={}, mats=[])
    base = 0
    for c, d in parts:
        remap = np.array([bones.index(b) for b in c["bones"]], np.uint16)
        out["pos"].append(c["pos"]); out["nrm"].append(d["nrm"]); out["uv"].append(d["uv"])
        out["tan"].append(d["tan"] if d["tan"] is not None else np.tile([1, 0, 0, 1], (len(d["pos"]), 1)).astype(np.float32))
        out["bi"].append(remap[c["bi"]]); out["bw"].append(c["bw"])
        out["subs"] += [(n, idx + base) for n, idx in c["subs"]]
        out["mats"] += c["mats"]
        for s in c["shapes"]:
            k = out["shapes"].setdefault(s.name, [])
            k.append(msnm.BlendShape(s.name, s.index + base, s.dv, s.dn, s.weight))
        base += len(d["pos"])
    shapes = [msnm.BlendShape(n, np.concatenate([x.index for x in v]), np.concatenate([x.dv for x in v]),
                              np.concatenate([x.dn for x in v]), v[0].weight) for n, v in out["shapes"].items()]
    return dict(pos=np.concatenate(out["pos"]), nrm=np.concatenate(out["nrm"]), tan=np.concatenate(out["tan"]),
                uv=np.concatenate(out["uv"]), bi=np.concatenate(out["bi"]), bw=np.concatenate(out["bw"]),
                subs=out["subs"], shapes=shapes, mats=out["mats"], bones=bones,
                bind=np.stack([bind[b] for b in bones]).astype(np.float32)), None


def slot_names(d):
    names, seen = [], {}
    for i, m in enumerate(d["materials"][:len(d["subs"])]):
        n = m["name"] or f"sub{i}"
        if n in seen:
            seen[n] += 1
            n = f"{n}#{seen[n]}"
        else:
            seen[n] = 0
        names.append(n)
    while len(names) < len(d["subs"]):
        names.append(f"sub{len(names)}")
    return names


def add_aliases(shapes):
    have = {s.name: s for s in shapes}
    added = []
    for alias, (src, scale) in SHAPE_ALIASES.items():
        if alias in have or src not in have:
            continue
        s = have[src]
        shapes.append(msnm.BlendShape(alias, s.index.copy(), (s.dv * scale).astype(np.float32), (s.dn * scale).astype(np.float32), s.weight))
        added.append(f"{alias}<-{src}x{scale}")
    return added


# ---------------------------------------------------------------- textures
def tex_file(tname, role):
    safe = "".join(ch if ch.isalnum() or ch in "._-" else "_" for ch in tname)
    return f"tex/{safe}{'_n' if role == 'normal' else ''}.png"


def save_texture(entry, tname, role, out_dir):
    from PIL import Image
    img = entry["img"]
    limit = 4096 if any(t in tname for t in FULLRES_TILES) else 2048
    if max(img.size) > limit:
        f = limit / max(img.size)
        img = img.resize((max(1, int(img.size[0] * f)), max(1, int(img.size[1] * f))), Image.LANCZOS)
    if role == "normal":
        a = np.asarray(img.convert("RGBA"), np.float32) / 255.0
        fmt = entry["fmt"]
        # DXT5nm stores X in alpha and Y in green; BC5/RGB store X in red. Rebuild a plain RGB normal (A=1).
        r_const = a[..., 0].std() < 0.02 and a[..., 0].mean() > 0.9
        if ("DXT5" in fmt or "BC3" in fmt) or r_const:
            x, y = a[..., 3], a[..., 1]
        else:
            x, y = a[..., 0], a[..., 1]
        nx, ny = x * 2 - 1, y * 2 - 1
        nz = np.sqrt(np.clip(1 - nx * nx - ny * ny, 0, 1))
        rgb = np.stack([x, y, (nz + 1) / 2, np.ones_like(x)], -1)
        img = Image.fromarray((np.clip(rgb, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA")
    rel = tex_file(tname, role)
    p = os.path.join(out_dir, rel)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    img.save(p, optimize=False, compress_level=6)
    return rel, img.size


# ---------------------------------------------------------------- main
def sha256(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def _slots(outputs, mesh, templates):
    slots = {}
    for sname, src in outputs[mesh]["slots"].items():
        base = sname.split("#")[0]
        t = templates.get(base, templates.get("*", "@garment"))
        # source float/colour properties travel only to templates of the same shader family (Daz hair/clothes);
        # the runtime sets a property only when the template shader has it
        copy = t in ("Hair", "@garment")
        slots[sname] = {"hide": True} if t is None else dict(
            {"template": t, "tex": src["tex"]},
            **({"floats": src["floats"], "colors": src["colors"], "srcShader": src["srcShader"]} if copy else {}))
    return slots


def write_skin_json(out, outputs, path=None):
    """skin.json schema 1 (read by src/MSN2Characters/Skins/SkinPack.cs). Portraits are added by the runtime renderer."""
    s = SKIN
    skin = {"schema": 1, "id": s["id"], "displayName": s["displayName"], "replaces": s["replaces"],
            "body": {"mesh": s["body"]["mesh"], "target": s["body"]["target"], "slots": _slots(outputs, s["body"]["mesh"], s["body"]["templates"])},
            "hair": {"mesh": s["hair"]["mesh"], "target": s["hair"]["target"], "slots": _slots(outputs, s["hair"]["mesh"], s["hair"]["templates"])},
            "extra": [{"mesh": e["mesh"], "name": e["name"], "slots": _slots(outputs, e["mesh"], e["templates"])} for e in s["extra"]],
            "clothes": {cid: {"mesh": m, "slots": _slots(outputs, m, {"*": "@garment"})} for cid, m in s["clothes"].items()},
            "blendshapeAliases": {}, "portraits": dict({"Portrait": "portrait.png", "Portrait2": "portrait2.png"},
                                           **{f"Cond{i}": f"portrait_cond{i}.png" for i in range(6)})}
    p = path or os.path.join(out, "skin.json")
    with open(p, "w", newline="\n") as f:
        json.dump(skin, f, indent=1)
        f.write("\n")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--msn1", required=True)
    ap.add_argument("--msn2", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--only-extract", action="store_true")
    ap.add_argument("--no-textures", action="store_true")
    ap.add_argument("--skin-json", default=None, help="where to write skin.json (default <out>/skin.json)")
    a = ap.parse_args()
    out = os.path.abspath(a.out)
    cache = os.path.join(out, ".build")
    os.makedirs(cache, exist_ok=True)
    bone_map = json.load(open(os.path.join(HERE, "bone_map_msn1_to_msn2.json")))["map"]

    tgt = extract_target(a.msn2, cache)
    src = extract_source(a.msn1, cache, not a.no_textures)
    if a.only_extract:
        return 0
    tgt_index = {b: i for i, b in enumerate(tgt["bones"])}
    tgt_J = joints_from_bind(tgt["bones"], tgt["bind"])
    src_J, spread = build_source_joints(src["smrs"])
    missing_tgt = sorted({v for v in bone_map.values() if v not in tgt_index})
    report = dict(target=tgt["mesh"], target_bones=len(tgt["bones"]), bone_map_targets_missing_in_rig=missing_tgt,
                  source_joint_spread_max=max(spread.values()) if spread else 0.0, meshes={}, unmapped={}, textures={})
    canon = canonical_sources(bone_map, src_J, tgt_J)
    report["fallback_bones"] = {}
    problems = list(missing_tgt and [f"bone map targets missing in rig: {missing_tgt}"] or [])
    tex_needed = {}
    outputs = {}
    for rel, names in MESH_GROUPS.items():
        parts = []
        for n in names:
            d = src["smrs"].get(n)
            if d is None:
                continue
            baked = bake_default_shapes(d)
            if baked:
                report.setdefault("baked_shapes", {})[n] = baked
            c, unm = conform_mesh(d, bone_map, src_J, tgt_J, tgt_index, tgt["bind"], src.get("parents", {}), canon)
            if c is not None and c["fallback"]:
                report["fallback_bones"][n] = c["fallback"]
            if unm:
                report["unmapped"][n] = unm
                problems.append(f"{n}: unmapped bones {unm}")
                continue
            sn = slot_names(d)
            c["subs"] = list(zip(sn, d["subs"]))
            c["shapes"] = [msnm.BlendShape(*s[:4], s[4]) for s in d["shapes"]]
            c["mats"] = [(sn[i], d["materials"][i] if i < len(d["materials"]) else {"tex": {}}) for i in range(len(sn))]
            parts.append((c, d))
        if not parts:
            continue
        g, _ = merge_group(parts)
        if len(parts) == 1:
            c, d = parts[0]
            g = dict(c, nrm=d["nrm"], tan=d["tan"], uv=d["uv"])
        added = add_aliases(g["shapes"]) if rel == "body.msnm" else []
        m = msnm.Msnm(bones=g["bones"], bindposes=g["bind"], positions=g["pos"], normals=g["nrm"], uv0=g["uv"],
                      bone_idx=g["bi"], bone_w=g["bw"], submeshes=g["subs"], shapes=g["shapes"], tangents=g["tan"])
        errs = msnm.validate(m)
        if errs:
            problems.append(f"{rel}: {errs}")
        p = os.path.join(out, rel)
        os.makedirs(os.path.dirname(p), exist_ok=True)
        msnm.write(p, m)
        back = msnm.read(p)
        if len(back.positions) != len(m.positions):
            problems.append(f"{rel}: round trip mismatch")
        slots = {}
        for sname, srcmat in g["mats"]:
            tex = srcmat.get("tex", {})
            st = {}
            for prop, tname in tex.items():
                role = tex_role(prop)
                st[prop] = tex_file(tname, role)
                tex_needed[(tname, role)] = st[prop]
                for al in PROP_ALIASES.get(prop, []):
                    st.setdefault(al, st[prop])
            slots[sname] = {"tex": st, "srcShader": srcmat.get("shader"), "floats": srcmat.get("floats", {}), "colors": srcmat.get("colors", {})}
        uv = g["uv"]
        outputs[rel] = dict(slots=slots, sources=names)
        report["meshes"][rel] = dict(
            verts=len(m.positions), submeshes=[(n, len(i) // 3) for n, i in m.submeshes], shapes=len(m.shapes),
            bones=len(m.bones), weight_sum_err=float(np.abs(m.bone_w.sum(1) - 1).max()) if len(m.bone_w) else 0.0,
            uv_range=[float(uv[:, 0].min()), float(uv[:, 0].max()), float(uv[:, 1].min()), float(uv[:, 1].max())],
            tangents=m.tangents is not None, aliases=added,
            max_vertex_offset_m=max(c["max_vertex_offset"] for c, _ in parts),
            max_bone_offset_m=max(max(c["per_bone"].values()) for c, _ in parts),
            biggest_bone_offsets=sorted(((v, k) for c, _ in parts for k, v in c["per_bone"].items()), reverse=True)[:6])
    if not a.no_textures:
        for (tname, role), rel in sorted(tex_needed.items()):
            e = src["textures"].get((tname, role))
            if e is None:
                problems.append(f"texture missing: {tname}")
                continue
            _, size = save_texture(e, tname, role, out)
            report["textures"][rel] = dict(src_format=e["fmt"], size=list(size))
    json.dump(outputs, open(os.path.join(cache, "slots.json"), "w"), indent=1)
    write_skin_json(out, outputs, a.skin_json)
    # manifest of every shipped file (everything except .build)
    files = {}
    for root, dirs, fs in os.walk(out):
        dirs[:] = [d for d in dirs if d != ".build"]
        for f in fs:
            if f == "manifest.json":
                continue
            p = os.path.join(root, f)
            files[os.path.relpath(p, out).replace("\\", "/")] = dict(sha256=sha256(p), bytes=os.path.getsize(p))
    json.dump(dict(schema=1, generator="convert_skin.py", files=dict(sorted(files.items()))),
              open(os.path.join(out, "manifest.json"), "w"), indent=1)
    report["problems"] = problems
    json.dump(report, open(os.path.join(cache, "report.json"), "w"), indent=1)
    for rel, r in report["meshes"].items():
        log(f"{rel:36s} verts={r['verts']:6d} subs={len(r['submeshes']):2d} shapes={r['shapes']:3d} bones={r['bones']:3d} "
            f"wErr={r['weight_sum_err']:.1e} maxVOff={r['max_vertex_offset_m'] * 100:.2f}cm maxBoneOff={r['max_bone_offset_m'] * 100:.2f}cm uv={['%.2f' % x for x in r['uv_range']]}")
    log(f"unmapped bones: {sum(len(v) for v in report['unmapped'].values())}; textures: {len(report['textures'])}; "
        f"source joint spread max {report['source_joint_spread_max'] * 100:.3f}cm; problems: {problems or 'none'}")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
