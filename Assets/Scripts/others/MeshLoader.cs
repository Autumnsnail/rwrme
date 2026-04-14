using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;

public static class MeshLoader
{
    public sealed class Result
    {
        public Mesh Mesh;
        public string MaterialName;
    }

    public sealed class Options
    {
        /// <summary>
        /// If true, converts from OGRE's typical right-handed Z-forward to Unity's left-handed Z-forward by flipping Z.
        /// This is a common fix but depends on how your content was authored/exported.
        /// </summary>
        public bool FlipZ = true;

        /// <summary>
        /// If true, flips triangle winding when FlipZ is enabled.
        /// </summary>
        public bool FixWindingAfterFlipZ = true;

        /// <summary>
        /// Optional path to OgreXMLConverter executable for converting .mesh -> .mesh.xml.
        /// If null/empty, .mesh loading requires you to provide the .mesh.xml directly.
        /// </summary>
        public string OgreXmlConverterPath = null;
    }

    /// <summary>
    /// Load an OGRE mesh XML (from OgreXMLConverter) and convert to Unity Mesh.
    /// Supports typical static meshes: positions, normals (optional), uv0 (optional), submesh faces.
    /// </summary>
    public static List<Result> LoadFromOgreMeshXml(string meshXmlPath, Options options = null)
    {
        if (string.IsNullOrWhiteSpace(meshXmlPath)) throw new ArgumentException("meshXmlPath is null/empty.");
        if (!File.Exists(meshXmlPath)) throw new FileNotFoundException("mesh xml not found", meshXmlPath);
        options ??= new Options();

        var doc = new XmlDocument();
        using (var fs = File.OpenRead(meshXmlPath))
        {
            doc.Load(fs);
        }

        var meshNode = doc.SelectSingleNode("/mesh");
        if (meshNode == null) throw new InvalidDataException("Not a valid OGRE mesh XML: missing /mesh node.");

        var shared = ParseGeometry(meshNode.SelectSingleNode("sharedgeometry"), options);

        var results = new List<Result>();
        var submeshesNode = meshNode.SelectSingleNode("submeshes");
        if (submeshesNode == null) throw new InvalidDataException("Missing submeshes node.");

        foreach (XmlNode submeshNode in submeshesNode.SelectNodes("submesh") ?? throw new InvalidDataException("Missing submesh nodes."))
        {
            var material = Attr(submeshNode, "material");
            var useShared = AttrBool(submeshNode, "usesharedvertices", defaultValue: false);

            Geometry geom;
            if (useShared)
            {
                if (shared == null) throw new InvalidDataException("Submesh uses shared geometry but sharedgeometry is missing.");
                geom = shared.Value;
            }
            else
            {
                var geometryNode = submeshNode.SelectSingleNode("geometry");
                if (geometryNode == null) throw new InvalidDataException("Submesh has no geometry and does not use shared vertices.");
                geom = ParseGeometry(geometryNode, options) ?? throw new InvalidDataException("Failed to parse submesh geometry.");
            }

            var triangles = ParseFaces(submeshNode.SelectSingleNode("faces"), options);
            var unityMesh = BuildUnityMesh(geom, triangles, options);
            unityMesh.name = Path.GetFileNameWithoutExtension(meshXmlPath) + (string.IsNullOrEmpty(material) ? "" : $"_{material}");

            results.Add(new Result { Mesh = unityMesh, MaterialName = material });
        }

        return results;
    }

    /// <summary>
    /// Convert .mesh -> .mesh.xml with OgreXMLConverter, then load the XML.
    /// This does NOT use OgreMain.dll directly; it relies on OGRE's converter tool output.
    /// </summary>
    public static List<Result> LoadFromOgreMesh(string meshPath, Options options = null)
    {
        if (string.IsNullOrWhiteSpace(meshPath)) throw new ArgumentException("meshPath is null/empty.");
        if (!File.Exists(meshPath)) throw new FileNotFoundException("mesh not found", meshPath);
        options ??= new Options();

        var converter = options.OgreXmlConverterPath;
        if (string.IsNullOrWhiteSpace(converter) || !File.Exists(converter))
        {
            throw new FileNotFoundException(
                "OgreXMLConverter not found. Provide Options.OgreXmlConverterPath, or call LoadFromOgreMeshXml() with a pre-converted .mesh.xml.",
                converter ?? "(null)");
        }

        var tmpDir = Path.Combine(Application.temporaryCachePath, "ogre-mesh-import");
        Directory.CreateDirectory(tmpDir);
        var outXml = Path.Combine(tmpDir, Path.GetFileName(meshPath) + ".xml");

        RunConverter(converter, meshPath, outXml);
        return LoadFromOgreMeshXml(outXml, options);
    }

    private static void RunConverter(string converterExe, string meshPath, string outXml)
    {
        // OgreXMLConverter usage differs slightly by version; most accept:
        //   OgreXMLConverter <input.mesh> <output.mesh.xml>
        var psi = new ProcessStartInfo
        {
            FileName = converterExe,
            Arguments = $"\"{meshPath}\" \"{outXml}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var p = Process.Start(psi);
        if (p == null) throw new InvalidOperationException("Failed to start OgreXMLConverter process.");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0 || !File.Exists(outXml))
        {
            throw new InvalidOperationException(
                $"OgreXMLConverter failed (exit {p.ExitCode}).\n" +
                $"stdout:\n{stdout}\n\nstderr:\n{stderr}");
        }
    }

    private readonly struct Geometry
    {
        public readonly Vector3[] Positions;
        public readonly Vector3[] Normals;
        public readonly Vector2[] Uv0;

        public Geometry(Vector3[] positions, Vector3[] normals, Vector2[] uv0)
        {
            Positions = positions;
            Normals = normals;
            Uv0 = uv0;
        }
    }

    private static Geometry? ParseGeometry(XmlNode geometryNode, Options options)
    {
        if (geometryNode == null) return null;

        var verticesNode = geometryNode.SelectSingleNode("vertexbuffer");
        if (verticesNode == null)
        {
            // Some exports wrap multiple vertexbuffers; pick the first with positions.
            var vbs = geometryNode.SelectNodes("vertexbuffer");
            if (vbs != null)
            {
                foreach (XmlNode vb in vbs)
                {
                    if (AttrBool(vb, "positions", false))
                    {
                        verticesNode = vb;
                        break;
                    }
                }
            }
        }

        var vertexBuffers = geometryNode.SelectNodes("vertexbuffer");
        if (vertexBuffers == null || vertexBuffers.Count == 0) throw new InvalidDataException("geometry has no vertexbuffer.");

        // Merge data across vertexbuffers by vertex index.
        var vertexCount = AttrInt(geometryNode, "vertexcount");
        if (vertexCount <= 0) throw new InvalidDataException("Invalid geometry vertexcount.");

        var positions = new Vector3[vertexCount];
        Vector3[] normals = null;
        Vector2[] uv0 = null;

        foreach (XmlNode vb in vertexBuffers)
        {
            var hasPos = AttrBool(vb, "positions", false);
            var hasNorm = AttrBool(vb, "normals", false);
            var texCoords = AttrInt(vb, "texture_coords", 0);

            if (hasNorm && normals == null) normals = new Vector3[vertexCount];
            if (texCoords > 0 && uv0 == null) uv0 = new Vector2[vertexCount];

            var i = 0;
            var vertices = vb.SelectNodes("vertex");
            if (vertices == null) continue;
            foreach (XmlNode v in vertices)
            {
                if (i >= vertexCount) break;

                if (hasPos)
                {
                    var posNode = v.SelectSingleNode("position");
                    if (posNode != null)
                    {
                        positions[i] = ConvertPosition(
                            AttrFloat(posNode, "x"),
                            AttrFloat(posNode, "y"),
                            AttrFloat(posNode, "z"),
                            options);
                    }
                }

                if (hasNorm && normals != null)
                {
                    var nNode = v.SelectSingleNode("normal");
                    if (nNode != null)
                    {
                        normals[i] = ConvertNormal(
                            AttrFloat(nNode, "x"),
                            AttrFloat(nNode, "y"),
                            AttrFloat(nNode, "z"),
                            options);
                    }
                }

                if (texCoords > 0 && uv0 != null)
                {
                    var tNode = v.SelectSingleNode("texcoord");
                    if (tNode != null)
                    {
                        uv0[i] = new Vector2(AttrFloat(tNode, "u"), AttrFloat(tNode, "v"));
                    }
                }

                i++;
            }
        }

        return new Geometry(positions, normals, uv0);
    }

    private static int[] ParseFaces(XmlNode facesNode, Options options)
    {
        if (facesNode == null) throw new InvalidDataException("Missing faces node.");

        var count = AttrInt(facesNode, "count");
        if (count < 0) throw new InvalidDataException("Invalid faces count.");

        var tris = new int[count * 3];
        var idx = 0;
        var faces = facesNode.SelectNodes("face");
        if (faces == null) return Array.Empty<int>();
        foreach (XmlNode face in faces)
        {
            var v1 = AttrInt(face, "v1");
            var v2 = AttrInt(face, "v2");
            var v3 = AttrInt(face, "v3");

            if (options.FlipZ && options.FixWindingAfterFlipZ)
            {
                // Swap to keep front-face consistent after handedness flip.
                (v2, v3) = (v3, v2);
            }

            tris[idx++] = v1;
            tris[idx++] = v2;
            tris[idx++] = v3;
        }

        if (idx != tris.Length)
        {
            Array.Resize(ref tris, idx);
        }

        return tris;
    }

    private static Mesh BuildUnityMesh(Geometry geom, int[] triangles, Options options)
    {
        var m = new Mesh();
        m.indexFormat = (geom.Positions.Length > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

        m.vertices = geom.Positions;
        if (geom.Normals != null) m.normals = geom.Normals;
        if (geom.Uv0 != null) m.uv = geom.Uv0;
        m.triangles = triangles;

        if (geom.Normals == null || geom.Normals.Length == 0)
        {
            m.RecalculateNormals();
        }

        m.RecalculateBounds();
        return m;
    }

    private static Vector3 ConvertPosition(float x, float y, float z, Options options)
    {
        if (!options.FlipZ) return new Vector3(x, y, z);
        return new Vector3(x, y, -z);
    }

    private static Vector3 ConvertNormal(float x, float y, float z, Options options)
    {
        if (!options.FlipZ) return new Vector3(x, y, z);
        return new Vector3(x, y, -z);
    }

    private static string Attr(XmlNode node, string name)
        => node?.Attributes?[name]?.Value ?? string.Empty;

    private static bool AttrBool(XmlNode node, string name, bool defaultValue)
    {
        var v = Attr(node, name);
        if (string.IsNullOrEmpty(v)) return defaultValue;
        if (bool.TryParse(v, out var b)) return b;
        if (v == "0") return false;
        if (v == "1") return true;
        return defaultValue;
    }

    private static int AttrInt(XmlNode node, string name, int defaultValue = 0)
    {
        var v = Attr(node, name);
        if (string.IsNullOrEmpty(v)) return defaultValue;
        return int.Parse(v, CultureInfo.InvariantCulture);
    }

    private static float AttrFloat(XmlNode node, string name)
    {
        var v = Attr(node, name);
        if (string.IsNullOrEmpty(v)) return 0f;
        return float.Parse(v, CultureInfo.InvariantCulture);
    }
}
