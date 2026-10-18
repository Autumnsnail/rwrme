using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Ogre MeshSerializer_v1.x 二进制 .mesh 解析（移植自 ogre_mesh.py.ref）。
/// 仅覆盖静态几何：positions / normals / uv0 / indices；骨骼、LOD、动画等 chunk 跳过。
/// </summary>
public static class OgreBinaryMeshParser
{
    const ushort M_HEADER = 0x1000;
    const ushort M_MESH = 0x3000;
    const ushort M_SUBMESH = 0x4000;
    const ushort M_SUBMESH_OPERATION = 0x4010;
    const ushort M_SUBMESH_BONE_ASSIGNMENT = 0x4100;
    const ushort M_SUBMESH_TEXTURE_ALIAS = 0x4200;
    const ushort M_GEOMETRY = 0x5000;
    const ushort M_GEOMETRY_VERTEX_DECLARATION = 0x5100;
    const ushort M_GEOMETRY_VERTEX_ELEMENT = 0x5110;
    const ushort M_GEOMETRY_VERTEX_BUFFER = 0x5200;
    const ushort M_GEOMETRY_VERTEX_BUFFER_DATA = 0x5210;
    const ushort M_MESH_BOUNDS = 0x9000;

    const ushort VET_FLOAT1 = 0;
    const ushort VET_FLOAT2 = 1;
    const ushort VET_FLOAT3 = 2;
    const ushort VET_FLOAT4 = 3;

    const ushort VES_POSITION = 1;
    const ushort VES_NORMAL = 4;
    const ushort VES_TEXTURE_COORDINATES = 7;

    public sealed class VertexElement
    {
        public ushort Source;
        public ushort Type;
        public ushort Semantic;
        public ushort Offset;
        public ushort Index;
    }

    public sealed class Geometry
    {
        public int VertexCount;
        public readonly List<VertexElement> Elements = new List<VertexElement>();
        public readonly Dictionary<int, byte[]> Buffers = new Dictionary<int, byte[]>();
        public readonly Dictionary<int, int> VertexSizes = new Dictionary<int, int>();
    }

    public sealed class SubMesh
    {
        public string Material = "";
        public bool UseSharedVertices = true;
        public int[] Indices = Array.Empty<int>();
        public ushort Operation = 4; // OT_TRIANGLE_LIST
        public Geometry Geometry;
    }

    public sealed class OgreMeshData
    {
        public string Version = "";
        public Geometry Shared;
        public readonly List<SubMesh> SubMeshes = new List<SubMesh>();
        public Vector3? BoundsMin;
        public Vector3? BoundsMax;
        public float BoundsRadius;
    }

    public sealed class GeometryArrays
    {
        public int VertexCount;
        public Vector3[] Positions;
        public Vector3[] Normals;
        public Vector2[] Uvs;
    }

    public static OgreMeshData ParseFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path is null/empty");
        if (!File.Exists(path)) throw new FileNotFoundException("mesh not found", path);
        return Parse(File.ReadAllBytes(path));
    }

    public static OgreMeshData Parse(byte[] data)
    {
        if (data == null || data.Length == 0) throw new ArgumentException("mesh data is empty");
        var r = new MeshReader(data);
        var mesh = new OgreMeshData();

        ushort hid = r.ReadU16();
        if (hid != M_HEADER)
            throw new InvalidDataException($"expected M_HEADER 0x1000, got 0x{hid:X4}");

        mesh.Version = r.ReadStringNl().Trim();
        if (!mesh.Version.StartsWith("[MeshSerializer_v", StringComparison.Ordinal))
            throw new InvalidDataException($"unexpected version: {mesh.Version}");

        while (!r.Eof)
        {
            ushort? cid = r.PeekChunkId();
            if (cid == null) break;
            if (cid.Value != M_MESH) break;

            r.ReadChunkHeader(out _, out _, out int mend);
            if (mend > r.Length) mend = r.Length;
            r.ReadBool(); // skeletal

            while (r.Tell < mend)
            {
                int chunkStart = r.Tell;
                r.ReadChunkHeader(out ushort scid, out uint slength, out int send);
                if (!IsValidChunkSpan(r, chunkStart, slength, send))
                    break;

                if (scid == M_GEOMETRY)
                {
                    mesh.Shared = ParseGeometry(r, send);
                    SkipToChunkEnd(r, send);
                }
                else if (scid == M_SUBMESH)
                {
                    // 声明 end 常伸进下一 mesh 级 chunk，禁止 SkipToChunkEnd(send)
                    mesh.SubMeshes.Add(ParseSubMesh(r, send));
                }
                else if (scid == M_MESH_BOUNDS)
                {
                    float xmin = r.ReadF32(), ymin = r.ReadF32(), zmin = r.ReadF32();
                    float xmax = r.ReadF32(), ymax = r.ReadF32(), zmax = r.ReadF32();
                    mesh.BoundsRadius = r.ReadF32();
                    mesh.BoundsMin = new Vector3(xmin, ymin, zmin);
                    mesh.BoundsMax = new Vector3(xmax, ymax, zmax);
                    SkipToChunkEnd(r, send);
                }
                else
                    SkipToChunkEnd(r, send);
            }
            SkipToChunkEnd(r, mend);
            break;
        }

        return mesh;
    }

    /// <summary>
    /// Ogre chunk length 含 6 字节头。允许 length==6 空 chunk；
    /// 不强制子 chunk 落在父 declared end 内（部分 MeshSerializer_v1.41 父 length 偏短）。
    /// </summary>
    static bool IsValidChunkSpan(MeshReader r, int chunkStart, uint length, int chunkEnd)
    {
        if (length < 6) return false;
        if (chunkEnd < chunkStart + 6) return false; // overflow
        if (chunkEnd > r.Length) return false;
        return true;
    }

    static void SkipToChunkEnd(MeshReader r, int chunkEnd)
    {
        if (chunkEnd > r.Tell)
            r.Seek(chunkEnd);
        // tell > chunkEnd：父 length 偏短或已读完超长子 chunk，禁止 Seek 回去
    }

    public static GeometryArrays GeometryToArrays(Geometry geo)
    {
        if (geo == null) throw new ArgumentNullException(nameof(geo));
        float[] posFlat = ExtractAttr(geo, VES_POSITION, 3);
        if (posFlat == null)
            throw new InvalidDataException("geometry has no positions");

        float[] nrmFlat = ExtractAttr(geo, VES_NORMAL, 3);
        float[] uvFlat = ExtractAttr(geo, VES_TEXTURE_COORDINATES, 2);

        var arrays = new GeometryArrays
        {
            VertexCount = geo.VertexCount,
            Positions = ToVector3(posFlat),
            Normals = nrmFlat != null ? ToVector3(nrmFlat) : null,
            Uvs = uvFlat != null ? ToVector2(uvFlat) : null,
        };
        return arrays;
    }

    static Geometry ParseGeometry(MeshReader r, int end)
    {
        var geo = new Geometry();
        geo.VertexCount = (int)r.ReadU32();
        if (geo.VertexCount < 0)
            throw new InvalidDataException("negative vertex count");

        while (r.Tell < end)
        {
            int chunkStart = r.Tell;
            r.ReadChunkHeader(out ushort cid, out uint length, out int cend);
            if (!IsValidChunkSpan(r, chunkStart, length, cend))
                break;

            if (cid == M_GEOMETRY_VERTEX_DECLARATION)
            {
                while (r.Tell < cend)
                {
                    int elStart = r.Tell;
                    r.ReadChunkHeader(out ushort eid, out uint elength, out int eend);
                    if (!IsValidChunkSpan(r, elStart, elength, eend))
                        break;
                    if (eid == M_GEOMETRY_VERTEX_ELEMENT)
                    {
                        geo.Elements.Add(new VertexElement
                        {
                            Source = r.ReadU16(),
                            Type = r.ReadU16(),
                            Semantic = r.ReadU16(),
                            Offset = r.ReadU16(),
                            Index = r.ReadU16(),
                        });
                    }
                    SkipToChunkEnd(r, eend);
                }
            }
            else if (cid == M_GEOMETRY_VERTEX_BUFFER)
            {
                ushort bind = r.ReadU16();
                ushort vsize = r.ReadU16();
                geo.VertexSizes[bind] = vsize;
                if (r.Tell < cend)
                {
                    int dataStart = r.Tell;
                    r.ReadChunkHeader(out ushort did, out uint dlength, out int dend);
                    if (IsValidChunkSpan(r, dataStart, dlength, dend) && did == M_GEOMETRY_VERTEX_BUFFER_DATA)
                    {
                        long nbytesLong = (long)geo.VertexCount * vsize;
                        if (nbytesLong < 0 || nbytesLong > int.MaxValue)
                            throw new InvalidDataException("vertex buffer size overflow");
                        int nbytes = (int)nbytesLong;
                        if (r.Tell + nbytes > dend)
                            throw new InvalidDataException("vertex buffer data exceeds chunk");
                        geo.Buffers[bind] = r.Read(nbytes);
                    }
                    SkipToChunkEnd(r, IsValidChunkSpan(r, dataStart, dlength, dend) ? dend : cend);
                }
            }
            SkipToChunkEnd(r, cend);
        }
        return geo;
    }

    static SubMesh ParseSubMesh(MeshReader r, int end)
    {
        var sm = new SubMesh();
        sm.Material = r.ReadStringNl();
        sm.UseSharedVertices = r.ReadBool();
        uint indexCount = r.ReadU32();
        if (indexCount > 50_000_000)
            throw new InvalidDataException($"suspicious index count: {indexCount}");
        bool indexes32 = r.ReadBool();
        sm.Indices = new int[indexCount];
        if (indexes32)
        {
            for (int i = 0; i < indexCount; i++)
                sm.Indices[i] = (int)r.ReadU32();
        }
        else
        {
            for (int i = 0; i < indexCount; i++)
                sm.Indices[i] = r.ReadU16();
        }

        // 只消费明确属于 submesh 的子 chunk；其它 id 交还上层（避免吞掉 bounds/edge list）。
        // 父 length 偏短时，geometry/operation 可能落在 declared end 之后，仍继续读。
        while (r.Tell + 6 <= r.Length)
        {
            int chunkStart = r.Tell;
            ushort? peek = r.PeekChunkId();
            if (peek == null) break;

            bool known =
                peek.Value == M_GEOMETRY ||
                peek.Value == M_SUBMESH_OPERATION ||
                peek.Value == M_SUBMESH_BONE_ASSIGNMENT ||
                peek.Value == M_SUBMESH_TEXTURE_ALIAS;

            if (!known)
            {
                // mesh 级 chunk（bounds 等）——即使仍在 declared end 内也交还
                break;
            }
            if (r.Tell >= end && peek.Value != M_GEOMETRY && peek.Value != M_SUBMESH_OPERATION)
                break;

            r.ReadChunkHeader(out ushort cid, out uint length, out int cend);
            if (!IsValidChunkSpan(r, chunkStart, length, cend))
            {
                r.Seek(chunkStart);
                break;
            }
            if (cid == M_GEOMETRY)
                sm.Geometry = ParseGeometry(r, cend);
            else if (cid == M_SUBMESH_OPERATION)
                sm.Operation = r.ReadU16();
            // bone / texture alias：跳过
            SkipToChunkEnd(r, cend);
        }
        return sm;
    }

    static float[] ExtractAttr(Geometry geo, ushort semantic, int components)
    {
        VertexElement el = null;
        for (int i = 0; i < geo.Elements.Count; i++)
        {
            if (geo.Elements[i].Semantic == semantic)
            {
                el = geo.Elements[i];
                break;
            }
        }
        if (el == null) return null;
        if (!geo.Buffers.TryGetValue(el.Source, out byte[] buf)) return null;
        if (!geo.VertexSizes.TryGetValue(el.Source, out int vsize)) return null;

        int nfloat;
        switch (el.Type)
        {
            case VET_FLOAT1: nfloat = 1; break;
            case VET_FLOAT2: nfloat = 2; break;
            case VET_FLOAT3: nfloat = 3; break;
            case VET_FLOAT4: nfloat = 4; break;
            default: return null;
        }

        int take = Math.Min(components, nfloat);
        var outArr = new float[geo.VertexCount * components];
        for (int vi = 0; vi < geo.VertexCount; vi++)
        {
            int baseOff = vi * vsize + el.Offset;
            int dst = vi * components;
            for (int c = 0; c < take; c++)
                outArr[dst + c] = BitConverter.ToSingle(buf, baseOff + c * 4);
            for (int c = take; c < components; c++)
                outArr[dst + c] = 0f;
        }
        return outArr;
    }

    static Vector3[] ToVector3(float[] flat)
    {
        int n = flat.Length / 3;
        var arr = new Vector3[n];
        for (int i = 0; i < n; i++)
            arr[i] = new Vector3(flat[i * 3], flat[i * 3 + 1], flat[i * 3 + 2]);
        return arr;
    }

    static Vector2[] ToVector2(float[] flat)
    {
        int n = flat.Length / 2;
        var arr = new Vector2[n];
        for (int i = 0; i < n; i++)
            arr[i] = new Vector2(flat[i * 2], flat[i * 2 + 1]);
        return arr;
    }

    sealed class MeshReader
    {
        readonly byte[] _data;
        int _pos;

        public MeshReader(byte[] data) { _data = data; _pos = 0; }
        public int Tell => _pos;
        public int Length => _data.Length;
        public bool Eof => _pos >= _data.Length;
        public void Seek(int pos) { _pos = pos; }

        public ushort? PeekChunkId()
        {
            if (_data.Length - _pos < 2) return null;
            return BitConverter.ToUInt16(_data, _pos);
        }

        public void ReadChunkHeader(out ushort id, out uint length, out int dataEnd)
        {
            int start = _pos;
            id = ReadU16();
            length = ReadU32();
            dataEnd = start + (int)length;
        }

        public byte[] Read(int n)
        {
            if (_pos + n > _data.Length)
                throw new EndOfStreamException($"need {n} bytes at {_pos}, have {_data.Length - _pos}");
            var b = new byte[n];
            Buffer.BlockCopy(_data, _pos, b, 0, n);
            _pos += n;
            return b;
        }

        public byte ReadU8() => Read(1)[0];
        public bool ReadBool() => ReadU8() != 0;
        public ushort ReadU16()
        {
            Ensure(2);
            ushort v = BitConverter.ToUInt16(_data, _pos);
            _pos += 2;
            return v;
        }
        public uint ReadU32()
        {
            Ensure(4);
            uint v = BitConverter.ToUInt32(_data, _pos);
            _pos += 4;
            return v;
        }
        public float ReadF32()
        {
            Ensure(4);
            float v = BitConverter.ToSingle(_data, _pos);
            _pos += 4;
            return v;
        }

        void Ensure(int n)
        {
            if (_pos + n > _data.Length)
                throw new EndOfStreamException($"need {n} bytes at {_pos}, have {_data.Length - _pos}");
        }

        public string ReadStringNl()
        {
            int start = _pos;
            while (_pos < _data.Length && _data[_pos] != 0x0A)
                _pos++;
            string s = Encoding.UTF8.GetString(_data, start, _pos - start);
            if (_pos < _data.Length) _pos++; // skip \n
            return s;
        }
    }
}
