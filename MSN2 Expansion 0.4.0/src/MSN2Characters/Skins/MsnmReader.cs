using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSN2Characters.Skins
{
    // MSNM v1 reader (format: tools/charport/msnm.py). Parse() is pure C# and runs on a worker thread;
    // Build() creates the UnityEngine.Mesh and must run on the main thread.
    public class MsnmData
    {
        public string Path;
        public string[] Bones;
        public Matrix4x4[] Bindposes;
        public Vector3[] Positions, Normals;
        public Vector4[] Tangents;
        public Vector2[] Uv0;
        public BoneWeight[] Weights;
        public string[] SlotNames;
        public int[][] Indices;
        public List<Shape> Shapes = new List<Shape>();

        public class Shape
        {
            public string Name;
            public float Weight;
            public int[] Index;
            public Vector3[] Dv, Dn;
        }

        public int VertexCount => Positions.Length;
    }

    public static class MsnmReader
    {
        public static MsnmData Parse(string path)
        {
            using (var r = new BinaryReader(File.OpenRead(path), Encoding.UTF8))
            {
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "MSNM") throw new InvalidDataException($"{path}: not an MSNM file");
                uint ver = r.ReadUInt32();
                if (ver != 1) throw new InvalidDataException($"{path}: unsupported MSNM version {ver}");
                int v = checked((int)r.ReadUInt32()), s = (int)r.ReadUInt32(), b = (int)r.ReadUInt32(), k = (int)r.ReadUInt32();
                uint flags = r.ReadUInt32();
                var d = new MsnmData { Path = path, Bones = new string[b], Bindposes = new Matrix4x4[b] };
                for (int i = 0; i < b; i++) d.Bones[i] = Str(r);
                for (int i = 0; i < b; i++)
                {
                    var m = new Matrix4x4();
                    for (int row = 0; row < 4; row++)
                        for (int col = 0; col < 4; col++) m[row, col] = r.ReadSingle();
                    d.Bindposes[i] = m;
                }
                d.Positions = V3(r, v);
                d.Normals = V3(r, v);
                if ((flags & 1) != 0)
                {
                    d.Tangents = new Vector4[v];
                    for (int i = 0; i < v; i++) d.Tangents[i] = new Vector4(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                }
                d.Uv0 = new Vector2[v];
                for (int i = 0; i < v; i++) d.Uv0[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                d.Weights = new BoneWeight[v];
                for (int i = 0; i < v; i++)
                {
                    int i0 = r.ReadUInt16(), i1 = r.ReadUInt16(), i2 = r.ReadUInt16(), i3 = r.ReadUInt16();
                    d.Weights[i] = new BoneWeight
                    {
                        boneIndex0 = i0, boneIndex1 = i1, boneIndex2 = i2, boneIndex3 = i3,
                        weight0 = r.ReadSingle(), weight1 = r.ReadSingle(), weight2 = r.ReadSingle(), weight3 = r.ReadSingle(),
                    };
                    if (i0 >= b || i1 >= b || i2 >= b || i3 >= b) throw new InvalidDataException($"{path}: bone index out of range at vertex {i}");
                }
                d.SlotNames = new string[s];
                d.Indices = new int[s][];
                for (int i = 0; i < s; i++)
                {
                    d.SlotNames[i] = Str(r);
                    int n = checked((int)r.ReadUInt32());
                    var idx = new int[n];
                    for (int j = 0; j < n; j++)
                    {
                        idx[j] = checked((int)r.ReadUInt32());
                        if (idx[j] >= v) throw new InvalidDataException($"{path}: submesh {d.SlotNames[i]} index out of range");
                    }
                    d.Indices[i] = idx;
                }
                for (int i = 0; i < k; i++)
                {
                    var sh = new MsnmData.Shape { Name = Str(r) };
                    uint frames = r.ReadUInt32();
                    if (frames != 1) throw new InvalidDataException($"{path}: shape {sh.Name} has {frames} frames");
                    sh.Weight = r.ReadSingle();
                    int nnz = checked((int)r.ReadUInt32());
                    sh.Index = new int[nnz]; sh.Dv = new Vector3[nnz]; sh.Dn = new Vector3[nnz];
                    for (int j = 0; j < nnz; j++)
                    {
                        sh.Index[j] = checked((int)r.ReadUInt32());
                        if (sh.Index[j] >= v) throw new InvalidDataException($"{path}: shape {sh.Name} index out of range");
                        sh.Dv[j] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        sh.Dn[j] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    }
                    d.Shapes.Add(sh);
                }
                if (r.BaseStream.Position != r.BaseStream.Length) throw new InvalidDataException($"{path}: {r.BaseStream.Length - r.BaseStream.Position} trailing bytes");
                return d;
            }
        }

        public static Mesh Build(MsnmData d, string name)
        {
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            m.SetVertices(d.Positions);
            m.SetNormals(d.Normals);
            if (d.Tangents != null) m.SetTangents(d.Tangents);
            m.SetUVs(0, d.Uv0);
            m.boneWeights = d.Weights;
            m.bindposes = d.Bindposes;
            m.subMeshCount = d.Indices.Length;
            for (int i = 0; i < d.Indices.Length; i++) m.SetTriangles(d.Indices[i], i, false);
            // dense delta buffers are reused across shapes; only touched entries are cleared afterwards
            var dv = new Vector3[d.VertexCount];
            var dn = new Vector3[d.VertexCount];
            foreach (var s in d.Shapes)
            {
                for (int j = 0; j < s.Index.Length; j++) { dv[s.Index[j]] = s.Dv[j]; dn[s.Index[j]] = s.Dn[j]; }
                m.AddBlendShapeFrame(s.Name, s.Weight, dv, dn, null);
                for (int j = 0; j < s.Index.Length; j++) { dv[s.Index[j]] = Vector3.zero; dn[s.Index[j]] = Vector3.zero; }
            }
            m.RecalculateBounds();
            return m;
        }

        private static string Str(BinaryReader r)
        {
            int n = r.ReadUInt16();
            return Encoding.UTF8.GetString(r.ReadBytes(n));
        }

        private static Vector3[] V3(BinaryReader r, int n)
        {
            var a = new Vector3[n];
            for (int i = 0; i < n; i++) a[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            return a;
        }
    }
}
