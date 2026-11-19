"""MSNM v1 mesh container: writer, reader and self-test.

Format (little-endian), read by src/MSN2Characters/Skins/MsnmReader.cs:
  char[4] "MSNM"; u32 version=1
  u32 vertexCount, submeshCount, boneCount, blendShapeCount, flags   (flags bit0 = tangents present)
  boneCount x { u16 len; utf8 name }                 target rig bone names
  boneCount x f32[16]                                bindposes, Unity Matrix4x4 row-major (m00 m01 m02 m03 m10 ... m33)
  f32[V*3] positions; f32[V*3] normals; [f32[V*4] tangents if flags&1]; f32[V*2] uv0
  V x { u16 boneIndex[4]; f32 weight[4] }            weights normalised, sorted descending
  submeshCount x { u16 len; utf8 slotName; u32 indexCount; u32 indices[indexCount] }
  blendShapeCount x { u16 len; utf8 name; u32 frameCount(=1); f32 frameWeight(=100); u32 nnz;
                      nnz x { u32 index; f32 dv[3]; f32 dn[3] } }

usage: python msnm.py --selftest | --info <file.msnm>
"""
import struct
import sys
from dataclasses import dataclass, field

import numpy as np

MAGIC = b"MSNM"
VERSION = 1


@dataclass
class BlendShape:
    name: str
    index: np.ndarray            # (nnz,) uint32
    dv: np.ndarray               # (nnz, 3) float32
    dn: np.ndarray               # (nnz, 3) float32
    weight: float = 100.0


@dataclass
class Msnm:
    bones: list                  # target bone names
    bindposes: np.ndarray        # (B, 4, 4) float32, row-major Unity matrices
    positions: np.ndarray        # (V, 3)
    normals: np.ndarray          # (V, 3)
    uv0: np.ndarray              # (V, 2)
    bone_idx: np.ndarray         # (V, 4) uint16
    bone_w: np.ndarray           # (V, 4) float32
    submeshes: list              # [(slotName, indices np.uint32 (N,))]
    shapes: list = field(default_factory=list)
    tangents: np.ndarray = None  # (V, 4) or None


def _str(s):
    b = s.encode("utf-8")
    return struct.pack("<H", len(b)) + b


def write(path, m: Msnm):
    V = len(m.positions)
    flags = 1 if m.tangents is not None else 0
    with open(path, "wb") as f:
        f.write(MAGIC + struct.pack("<I", VERSION))
        f.write(struct.pack("<5I", V, len(m.submeshes), len(m.bones), len(m.shapes), flags))
        for n in m.bones:
            f.write(_str(n))
        f.write(np.ascontiguousarray(m.bindposes, "<f4").reshape(-1, 16).tobytes())
        f.write(np.ascontiguousarray(m.positions, "<f4").tobytes())
        f.write(np.ascontiguousarray(m.normals, "<f4").tobytes())
        if flags & 1:
            f.write(np.ascontiguousarray(m.tangents, "<f4").tobytes())
        f.write(np.ascontiguousarray(m.uv0, "<f4").tobytes())
        rec = np.zeros(V, dtype=[("i", "<u2", 4), ("w", "<f4", 4)])
        rec["i"] = m.bone_idx
        rec["w"] = m.bone_w
        f.write(rec.tobytes())
        for name, idx in m.submeshes:
            idx = np.ascontiguousarray(idx, "<u4")
            f.write(_str(name) + struct.pack("<I", len(idx)) + idx.tobytes())
        for s in m.shapes:
            nnz = len(s.index)
            f.write(_str(s.name) + struct.pack("<IfI", 1, float(s.weight), nnz))
            r = np.zeros(nnz, dtype=[("i", "<u4"), ("v", "<f4", 3), ("n", "<f4", 3)])
            r["i"] = s.index
            r["v"] = s.dv
            r["n"] = s.dn
            f.write(r.tobytes())


class _R:
    def __init__(self, b):
        self.b, self.o = b, 0

    def take(self, n):
        v = self.b[self.o:self.o + n]
        if len(v) != n:
            raise ValueError(f"truncated at {self.o}")
        self.o += n
        return v

    def u(self, fmt):
        sz = struct.calcsize(fmt)
        return struct.unpack(fmt, self.take(sz))

    def s(self):
        (n,) = self.u("<H")
        return self.take(n).decode("utf-8")

    def arr(self, dtype, count):
        dt = np.dtype(dtype)
        return np.frombuffer(self.take(dt.itemsize * count), dt).copy()


def read(path) -> Msnm:
    r = _R(open(path, "rb").read())
    if r.take(4) != MAGIC:
        raise ValueError("not an MSNM file")
    (ver,) = r.u("<I")
    if ver != VERSION:
        raise ValueError(f"unsupported version {ver}")
    V, S, B, K, flags = r.u("<5I")
    bones = [r.s() for _ in range(B)]
    bp = r.arr("<f4", B * 16).reshape(B, 4, 4)
    pos = r.arr("<f4", V * 3).reshape(V, 3)
    nrm = r.arr("<f4", V * 3).reshape(V, 3)
    tan = r.arr("<f4", V * 4).reshape(V, 4) if flags & 1 else None
    uv = r.arr("<f4", V * 2).reshape(V, 2)
    rec = r.arr([("i", "<u2", 4), ("w", "<f4", 4)], V)
    subs = []
    for _ in range(S):
        name = r.s()
        (n,) = r.u("<I")
        subs.append((name, r.arr("<u4", n)))
    shapes = []
    for _ in range(K):
        name = r.s()
        fc, fw, nnz = r.u("<IfI")
        if fc != 1:
            raise ValueError(f"shape {name}: frameCount {fc} != 1")
        d = r.arr([("i", "<u4"), ("v", "<f4", 3), ("n", "<f4", 3)], nnz)
        shapes.append(BlendShape(name, d["i"].copy(), d["v"].copy(), d["n"].copy(), fw))
    if r.o != len(r.b):
        raise ValueError(f"{len(r.b) - r.o} trailing bytes")
    return Msnm(bones, bp, pos, nrm, uv, rec["i"].copy(), rec["w"].copy(), subs, shapes, tan)


def validate(m: Msnm):
    """Structural checks shared by the converter and the self-test. Returns a list of problems."""
    errs = []
    V, B = len(m.positions), len(m.bones)
    if m.bindposes.shape != (B, 4, 4):
        errs.append("bindpose count")
    if m.bone_idx.size and int(m.bone_idx.max()) >= B:
        errs.append("bone index out of range")
    ws = m.bone_w.sum(1)
    if V and np.abs(ws - 1).max() > 1e-4:
        errs.append(f"weight sum error {np.abs(ws - 1).max():.2e}")
    if V and (np.diff(m.bone_w, axis=1) > 1e-7).any():
        errs.append("weights not sorted descending")
    for name, idx in m.submeshes:
        if len(idx) % 3:
            errs.append(f"submesh {name} index count not /3")
        if len(idx) and int(idx.max()) >= V:
            errs.append(f"submesh {name} index out of range")
    seen = set()
    for s in m.shapes:
        if s.name in seen:
            errs.append(f"duplicate shape {s.name}")
        seen.add(s.name)
        if len(s.index) and int(s.index.max()) >= V:
            errs.append(f"shape {s.name} index out of range")
    if len(set(m.bones)) != B:
        errs.append("duplicate bone names")
    return errs


def selftest():
    import os
    import tempfile
    m = Msnm(
        bones=["Hips", "Spine"],
        bindposes=np.stack([np.eye(4, dtype=np.float32), np.eye(4, dtype=np.float32)]),
        positions=np.array([[0, 0, 0], [1, 0, 0], [0, 1, 0]], np.float32),
        normals=np.array([[0, 0, 1]] * 3, np.float32),
        uv0=np.array([[0, 0], [1, 0], [0, 1]], np.float32),
        bone_idx=np.array([[0, 1, 0, 0], [1, 0, 0, 0], [0, 0, 0, 0]], np.uint16),
        bone_w=np.array([[0.75, 0.25, 0, 0], [1, 0, 0, 0], [1, 0, 0, 0]], np.float32),
        submeshes=[("Body", np.array([0, 1, 2], np.uint32))],
        shapes=[BlendShape("Smile", np.array([2], np.uint32), np.array([[0, 0.5, 0]], np.float32), np.zeros((1, 3), np.float32))],
        tangents=np.array([[1, 0, 0, 1]] * 3, np.float32),
    )
    m.bindposes[1, 1, 3] = -0.5  # row-major: m13 is the Y translation
    assert not validate(m), validate(m)
    fd, p = tempfile.mkstemp(suffix=".msnm")
    os.close(fd)
    try:
        write(p, m)
        r = read(p)
    finally:
        os.remove(p)
    assert r.bones == m.bones
    for a in ("bindposes", "positions", "normals", "uv0", "bone_idx", "bone_w", "tangents"):
        assert np.array_equal(getattr(r, a), getattr(m, a)), a
    assert r.submeshes[0][0] == "Body" and np.array_equal(r.submeshes[0][1], m.submeshes[0][1])
    s = r.shapes[0]
    assert s.name == "Smile" and s.weight == 100.0 and np.array_equal(s.index, [2]) and np.allclose(s.dv, [[0, 0.5, 0]])
    assert r.bindposes[1, 1, 3] == np.float32(-0.5)
    assert not validate(r)
    print("msnm selftest OK")


def info(path):
    m = read(path)
    print(f"{path}: verts={len(m.positions)} bones={len(m.bones)} submeshes={[(n, len(i) // 3) for n, i in m.submeshes]} "
          f"shapes={len(m.shapes)} tangents={m.tangents is not None} problems={validate(m) or 'none'}")


if __name__ == "__main__":
    if len(sys.argv) >= 2 and sys.argv[1] == "--selftest":
        selftest()
    elif len(sys.argv) >= 3 and sys.argv[1] == "--info":
        for p in sys.argv[2:]:
            info(p)
    else:
        sys.exit(__doc__)
