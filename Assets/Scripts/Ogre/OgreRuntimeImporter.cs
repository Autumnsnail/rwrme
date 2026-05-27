using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class OgreRuntimeImporter : MonoBehaviour
{
    public static OgreRuntimeImporter instance;
    void Awake() => instance = this;

    /// <summary>
    /// 运行时内存中的模型库：键为相对目录的路径（统一使用 '/'），值为该文件的子网格列表。
    /// 由 <see cref="ImportDirectoryAsync"/> 写入；也可自行赋值。
    /// </summary>
    public static readonly Dictionary<string, List<MeshLoader.Result>> RuntimeMeshLibrary =
        new Dictionary<string, List<MeshLoader.Result>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 运行时纹理库：键为相对路径（统一使用 '/'），值为 PNG 等图片加载后的 <see cref="Texture2D"/>。
    /// 由 <see cref="ImportPngDirectoryAsync"/> 写入。
    /// </summary>
    public static readonly Dictionary<string, Texture2D> RuntimeTextureLibrary =
        new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

    public sealed class Options
    {
        /// <summary>
        /// Absolute path to OgreXMLConverter.exe (Windows Standalone only).
        /// If null/empty, runtime can only import already-converted .mesh.xml files.
        /// </summary>
        public string OgreXmlConverterPath;

        /// <summary>
        /// Cache directory under persistentDataPath.
        /// </summary>
        public string CacheFolderName = "OgreMeshCache";

        /// <summary>
        /// If true, keep intermediate .mesh.xml in cache.
        /// If false, importer may delete xml after creating Mesh (but will keep it if needed for debugging).
        /// </summary>
        public bool KeepIntermediateXml = true;

        /// <summary>
        /// Optional directory used when meshOrXmlPath is a relative name.
        /// If empty, falls back to OgreRuntimeSettings.MeshSearchPath.
        /// </summary>
        public string MeshSearchPath;

        /// <summary>
        /// PNG 纹理搜索目录。为空时使用 <see cref="OgreRuntimeSettings.TexturesPath"/>。
        /// </summary>
        public string TexturesPath;

        public bool FlipZ = true;
        public bool FixWindingAfterFlipZ = true;
    }

    /// <summary>
    /// 从目录批量导入 PNG，加载到内存并可选合并进 <see cref="RuntimeTextureLibrary"/>。
    /// 须在主线程调用（内部使用 <see cref="Texture2D.LoadImage"/>）。
    /// </summary>
    public static Task<Dictionary<string, Texture2D>> ImportPngDirectoryAsync(
        string directoryPath,
        bool recursive = true,
        bool mergeIntoRuntimeLibrary = true,
        IProgress<(int current, int total, string relativePath)> progress = null)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("directoryPath is null/empty", nameof(directoryPath));

        var rootFull = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(rootFull))
            throw new DirectoryNotFoundException($"Directory not found: {rootFull}");

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory
            .EnumerateFiles(rootFull, "*.png", searchOption)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        var total = files.Count;

        for (var i = 0; i < files.Count; i++)
        {
            var abs = files[i];
            var relativeKey = NormalizeRelativeKey(Path.GetRelativePath(rootFull, abs));
            progress?.Report((i + 1, total, relativeKey));

            try
            {
                var tex = LoadTextureFromImageFile(abs);
                if (tex == null)
                {
                    Debug.LogWarning($"[OgreRuntimeImporter] Skip PNG (LoadImage failed): {abs}");
                    continue;
                }

                tex.name = Path.GetFileNameWithoutExtension(abs);
                result[relativeKey] = tex;

                if (mergeIntoRuntimeLibrary)
                {
                    if (RuntimeTextureLibrary.TryGetValue(relativeKey, out var old) && old != null && old != tex)
                        UnityEngine.Object.Destroy(old);
                    RuntimeTextureLibrary[relativeKey] = tex;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OgreRuntimeImporter] Skip PNG: {abs}\n{ex.Message}");
            }
        }

        return Task.FromResult(result);
    }

    /// <summary>从内存纹理库按相对路径或文件名查找。</summary>
    public static bool TryGetTextureFromLibrary(string relativeOrFileName, out Texture2D texture)
    {
        texture = null;
        if (string.IsNullOrWhiteSpace(relativeOrFileName))
            return false;

        var key = NormalizeRelativeKey(relativeOrFileName.Replace('\\', '/'));
        if (RuntimeTextureLibrary.TryGetValue(key, out texture) && texture != null)
            return true;

        var fileName = Path.GetFileName(relativeOrFileName.TrimEnd('/', '\\'));
        foreach (var kv in RuntimeTextureLibrary)
        {
            if (string.Equals(Path.GetFileName(kv.Key), fileName, StringComparison.OrdinalIgnoreCase)
                && kv.Value != null)
            {
                texture = kv.Value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 在 <paramref name="searchDir"/> 下按文件名查找 PNG（支持无扩展名）。
    /// </summary>
    public static string ResolveTextureFilePath(string textureName, string searchDir = null)
    {
        if (string.IsNullOrWhiteSpace(textureName))
            return null;

        if (string.IsNullOrWhiteSpace(searchDir))
            searchDir = OgreRuntimeSettings.TexturesPath;
        if (string.IsNullOrWhiteSpace(searchDir) || !Directory.Exists(searchDir))
            return null;

        var name = Path.GetFileName(textureName.Trim().Replace('\\', '/'));
        if (string.IsNullOrEmpty(name))
            return null;

        var direct = Path.Combine(searchDir, name);
        if (File.Exists(direct))
            return Path.GetFullPath(direct);

        if (!Path.HasExtension(name))
        {
            var withPng = Path.Combine(searchDir, name + ".png");
            if (File.Exists(withPng))
                return Path.GetFullPath(withPng);
        }

        return null;
    }

    /// <summary>清空运行时纹理库并销毁已加载的 <see cref="Texture2D"/>。</summary>
    public static void ClearRuntimeTextureLibrary()
    {
        foreach (var tex in RuntimeTextureLibrary.Values)
        {
            if (tex != null)
                UnityEngine.Object.Destroy(tex);
        }
        RuntimeTextureLibrary.Clear();
    }

    static Texture2D LoadTextureFromImageFile(string absolutePath)
    {
        if (!File.Exists(absolutePath))
            return null;

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear: false);
        var bytes = File.ReadAllBytes(absolutePath);
        if (!tex.LoadImage(bytes, markNonReadable: false))
        {
            UnityEngine.Object.Destroy(tex);
            return null;
        }
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        return tex;
    }

    /// <summary>
    /// Runtime import from a file path.
    /// - If path ends with .xml: treated as OgreXMLConverter output and parsed directly.
    /// - If path ends with .mesh: will use OgreXMLConverter (if available on this platform) and cache the .xml.
    /// Returns the converted submeshes (Mesh + material name) in-memory.
    /// </summary>
    public static Task<List<MeshLoader.Result>> ImportAsync(string meshOrXmlPath, Options options = null)
    {
        if (string.IsNullOrWhiteSpace(meshOrXmlPath)) throw new ArgumentException("path is null/empty");
        options ??= new Options();

        // Fill from saved runtime settings if not provided explicitly.
        if (string.IsNullOrWhiteSpace(options.OgreXmlConverterPath))
            options.OgreXmlConverterPath = OgreRuntimeSettings.OgreXmlConverterPath;
        if (string.IsNullOrWhiteSpace(options.MeshSearchPath))
            options.MeshSearchPath = OgreRuntimeSettings.MeshSearchPath;

        var resolved = ResolvePath(meshOrXmlPath, options.MeshSearchPath);
        if (!File.Exists(resolved)) throw new FileNotFoundException("file not found", resolved);

        var ext = Path.GetExtension(resolved).ToLowerInvariant();
        if (ext == ".xml")
        {
            // Unity Mesh 必须在主线程创建，禁止 Task.Run
            return Task.FromResult(ImportFromXml(resolved, options));
        }

        if (ext != ".mesh")
        {
            throw new NotSupportedException($"Unsupported extension '{ext}'. Expected .mesh or .xml (.mesh.xml).");
        }

        // RunConverter 与 ImportFromXml 在同一线程执行，避免 await Task.Run 后延续落到线程池导致 Mesh 非主线程创建。
        var xmlPath = EnsureConvertedXml(resolved, options);
        var results = ImportFromXml(xmlPath, options);

        if (!options.KeepIntermediateXml)
        {
            TryDelete(xmlPath);
        }

        return Task.FromResult(results);
    }
/*
    /// <summary>
    /// Convenience: import and spawn a GameObject hierarchy.
    /// One child GameObject per submesh, named by material name (if present).
    /// </summary>
    public static async Task<GameObject> ImportAndInstantiateAsync(string meshOrXmlPath, Func<string, Material> materialResolver = null, Options options = null)
    {
        var results = await ImportAsync(meshOrXmlPath, options);
        var root = new GameObject(Path.GetFileNameWithoutExtension(meshOrXmlPath));

        for (var i = 0; i < results.Count; i++)
        {
            var sm = results[i];
            if (sm.Mesh == null) continue;

            var name = string.IsNullOrEmpty(sm.MaterialName) ? $"submesh_{i}" : sm.MaterialName;
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, worldPositionStays: false);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = sm.Mesh;

            var mr = go.AddComponent<MeshRenderer>();
            if (materialResolver != null)
            {
                var mat = materialResolver(sm.MaterialName);
                if (mat != null) mr.sharedMaterial = mat;
            }
        }

        return root;
    }
*/
    /// <summary>
    /// 从目录批量读取 OGRE 模型（*.mesh 与 *.mesh.xml），全部保留在内存中。
    /// 若同一目录下同时存在 foo.mesh 与 foo.mesh.xml，只导入 foo.mesh。
    /// </summary>
    /// <param name="directoryPath">根目录绝对路径或相对当前工作目录的路径</param>
    /// <param name="recursive">是否包含子目录</param>
    /// <param name="mergeIntoRuntimeLibrary">为 true 时合并进 <see cref="RuntimeMeshLibrary"/>（同键覆盖）</param>
    /// <param name="options">为空时使用 PlayerPrefs 中的转换器路径等</param>
    /// <param name="progress">可选进度：(当前序号, 总数, 相对路径)</param>
    /// <param name="alsoImportFromMapImporterSvgPath">
    /// 为 true 时，除了 <paramref name="directoryPath"/> 外，还会尝试从 <c>MapImporter</c> 当前使用的
    /// <c>objects.svg</c> 所在目录读取 mesh（即 <c>Application.dataPath + MapImporter.basePath</c>）。
    /// </param>
    /// <returns>本次导入的相对路径 -> 子网格列表（新字典，与库内容相同引用若 merge 为 true）</returns>
    public static async Task<Dictionary<string, List<MeshLoader.Result>>> ImportDirectoryAsync(
        string directoryPath,
        bool recursive = true,
        bool mergeIntoRuntimeLibrary = true,
        Options options = null,
        IProgress<(int current, int total, string relativePath)> progress = null,
        bool alsoImportFromMapImporterSvgPath = true)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("directoryPath is null/empty", nameof(directoryPath));

        // 多根目录导入：先导入用户指定目录，再导入 MapImporter 目录，让 MapImporter 在键冲突时优先覆盖。
        var roots = new List<string>();
        var specifiedRoot = Path.GetFullPath(directoryPath);
        roots.Add(specifiedRoot);

        if (alsoImportFromMapImporterSvgPath)
        {
            var mapRoot = TryGetMapImporterObjectsSvgDirectory();
            if (!string.IsNullOrWhiteSpace(mapRoot))
                roots.Add(mapRoot);
        }

        roots = roots
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var r in roots)
        {
            if (!Directory.Exists(r))
                Debug.LogWarning($"[OgreRuntimeImporter] ImportDirectoryAsync: directory not found, skip: {r}");
        }

        roots = roots.Where(Directory.Exists).ToList();
        if (roots.Count == 0)
            throw new DirectoryNotFoundException($"Directory not found: {specifiedRoot}");

        options ??= new Options();
        if (string.IsNullOrWhiteSpace(options.OgreXmlConverterPath))
            options.OgreXmlConverterPath = OgreRuntimeSettings.OgreXmlConverterPath;
        if (string.IsNullOrWhiteSpace(options.MeshSearchPath))
            options.MeshSearchPath = OgreRuntimeSettings.MeshSearchPath;

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        var result = new Dictionary<string, List<MeshLoader.Result>>(StringComparer.OrdinalIgnoreCase);

        // 预先构建总任务列表，便于 progress 显示总数。
        var jobs = new List<(string rootFull, string abs, string relativeKey)>();
        foreach (var rootFull in roots)
        {
            var meshBinary = new List<string>();
            foreach (var f in Directory.EnumerateFiles(rootFull, "*.mesh", searchOption))
            {
                if (f.EndsWith(".mesh.xml", StringComparison.OrdinalIgnoreCase))
                    continue;
                meshBinary.Add(f);
            }

            var meshXmlOnly = new List<string>();
            foreach (var f in Directory.EnumerateFiles(rootFull, "*.mesh.xml", searchOption))
            {
                var partner = GetPartnerBinaryMeshPath(f);
                if (partner != null && File.Exists(partner))
                    continue;
                meshXmlOnly.Add(f);
            }

            var ordered = meshBinary
                .Concat(meshXmlOnly)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var abs in ordered)
            {
                var relative = NormalizeRelativeKey(Path.GetRelativePath(rootFull, abs));
                jobs.Add((rootFull, abs, relative));
            }
        }

        var total = jobs.Count;
        for (var i = 0; i < jobs.Count; i++)
        {
            var job = jobs[i];
            progress?.Report((i + 1, total, job.relativeKey));

            try
            {
                var submeshes = await ImportAsync(job.abs, options);
                result[job.relativeKey] = submeshes;
                if (mergeIntoRuntimeLibrary)
                    RuntimeMeshLibrary[job.relativeKey] = submeshes;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OgreRuntimeImporter] Skip or fail: {job.abs}\n{ex.Message}");
            }
        }

        return result;
    }

    private static string TryGetMapImporterObjectsSvgDirectory()
    {
        try
        {
            // MapImporter 使用的 objects.svg 路径：Path.Combine(Application.dataPath, basePath, "objects.svg")
            if (MapImporter.instate == null)
                return null;

            var basePath = MapImporter.instate.basePath;
            if (string.IsNullOrWhiteSpace(basePath))
                return null;

            var svgPath = Path.Combine(Application.dataPath, basePath, "objects.svg");
            if (!File.Exists(svgPath))
                return null;

            return Path.GetDirectoryName(svgPath);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>从内存库按相对路径或仅文件名查找（先全键匹配，再文件名匹配）。</summary>
    public static bool TryGetFromLibrary(string relativeOrFileName, out List<MeshLoader.Result> submeshes)
    {
        submeshes = null;
        if (string.IsNullOrWhiteSpace(relativeOrFileName))
            return false;

        var key = NormalizeRelativeKey(relativeOrFileName.Replace('\\', '/'));
        if (RuntimeMeshLibrary.TryGetValue(key, out submeshes))
            return true;

        var fileName = Path.GetFileName(relativeOrFileName.TrimEnd('/', '\\'));
        foreach (var kv in RuntimeMeshLibrary)
        {
            if (string.Equals(Path.GetFileName(kv.Key), fileName, StringComparison.OrdinalIgnoreCase))
            {
                submeshes = kv.Value;
                return true;
            }
        }

        return false;
    }

    /// <summary>清空运行时内存模型库。</summary>
    public static void ClearRuntimeMeshLibrary() => RuntimeMeshLibrary.Clear();

    private static string NormalizeRelativeKey(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return relativePath;
        return relativePath.Replace('\\', '/');
    }

    /// <summary>foo.mesh.xml -> 同目录下的 foo.mesh（若应成对存在）。</summary>
    private static string GetPartnerBinaryMeshPath(string meshXmlPath)
    {
        var dir = Path.GetDirectoryName(meshXmlPath);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(meshXmlPath);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(nameWithoutExt))
            return null;
        if (!nameWithoutExt.EndsWith(".mesh", StringComparison.OrdinalIgnoreCase))
            return null;
        var baseName = nameWithoutExt.Substring(0, nameWithoutExt.Length - ".mesh".Length);
        return Path.Combine(dir, baseName + ".mesh");
    }

    private static List<MeshLoader.Result> ImportFromXml(string xmlPath, Options options)
    {
        var loaderOptions = new MeshLoader.Options
        {
            OgreXmlConverterPath = options.OgreXmlConverterPath,
            FlipZ = options.FlipZ,
            FixWindingAfterFlipZ = options.FixWindingAfterFlipZ,
        };

        return MeshLoader.LoadFromOgreMeshXml(xmlPath, loaderOptions);
    }

    /// <summary>
    /// 同步调用 OgreXMLConverter。故意不在后台线程跑，以便后续 <see cref="ImportFromXml"/> 仍在主线程执行（Unity Mesh 要求）。
    /// 批量导入时可能造成短暂卡顿。
    /// </summary>
    private static string EnsureConvertedXml(string meshPath, Options options)
    {
        // On most platforms (WebGL, iOS, consoles) you can't spawn processes.
        // This runtime converter path is mainly for Windows Standalone builds.
#if !(UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
        throw new PlatformNotSupportedException("Runtime conversion from .mesh requires OgreXMLConverter and a platform that supports starting processes (Windows Standalone). Provide pre-converted .mesh.xml instead.");
#else
        if (string.IsNullOrWhiteSpace(options.OgreXmlConverterPath) || !File.Exists(options.OgreXmlConverterPath))
        {
            throw new FileNotFoundException("OgreXMLConverterPath is not set or invalid; cannot convert .mesh at runtime.", options.OgreXmlConverterPath ?? "(null)");
        }

        var cacheRoot = Path.Combine(Application.persistentDataPath, options.CacheFolderName);
        Directory.CreateDirectory(cacheRoot);

        var sha1 = ComputeSha1(meshPath);
        var dir = Path.Combine(cacheRoot, sha1.Substring(0, 2), sha1.Substring(2, 2));
        Directory.CreateDirectory(dir);

        var xmlPath = Path.Combine(dir, Path.GetFileName(meshPath) + ".xml");

        if (File.Exists(xmlPath) && File.GetLastWriteTimeUtc(xmlPath) >= File.GetLastWriteTimeUtc(meshPath))
        {
            return xmlPath;
        }

        RunConverter(options.OgreXmlConverterPath, meshPath, xmlPath);
        return xmlPath;
#endif
    }

    private static void RunConverter(string converterExe, string inputMeshPath, string outputXmlPath)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = converterExe,
            Arguments = $"\"{inputMeshPath}\" \"{outputXmlPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var p = System.Diagnostics.Process.Start(psi);
        if (p == null) throw new InvalidOperationException("Failed to start OgreXMLConverter process.");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0 || !File.Exists(outputXmlPath))
        {
            throw new InvalidOperationException(
                $"OgreXMLConverter failed (exit {p.ExitCode}).\nstdout:\n{stdout}\n\nstderr:\n{stderr}");
        }
    }

    private static string ComputeSha1(string path)
    {
        using var sha1 = SHA1.Create();
        using var fs = File.OpenRead(path);
        var hash = sha1.ComputeHash(fs);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    private static string ResolvePath(string input, string searchDir)
    {
        // 1) direct path
        if (File.Exists(input)) return Path.GetFullPath(input);

        // 2) if input has no directory, try searchDir + file
        var isRooted = Path.IsPathRooted(input);
        if (!isRooted && !string.IsNullOrWhiteSpace(searchDir))
        {
            var candidate = Path.Combine(searchDir, input);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);

            // Try common extensions
            if (!Path.HasExtension(candidate))
            {
                var c1 = candidate + ".mesh";
                if (File.Exists(c1)) return Path.GetFullPath(c1);
                var c2 = candidate + ".mesh.xml";
                if (File.Exists(c2)) return Path.GetFullPath(c2);
                var c3 = candidate + ".xml";
                if (File.Exists(c3)) return Path.GetFullPath(c3);
            }
        }

        return Path.GetFullPath(input);
    }
}

public static class OgreRuntimeSettings
{
    private const string KeyConverter = "ogre.xmlConverterPath";
    private const string KeyMeshSearch = "ogre.meshSearchPath";
    private const string KeyTextures = "ogre.texturesPath";

    public static string OgreXmlConverterPath
    {
        get => PlayerPrefs.GetString(KeyConverter, string.Empty);
        set
        {
            PlayerPrefs.SetString(KeyConverter, value ?? string.Empty);
            PlayerPrefs.Save();
        }
    }

    public static string MeshSearchPath
    {
        get => PlayerPrefs.GetString(KeyMeshSearch, string.Empty);
        set
        {
            PlayerPrefs.SetString(KeyMeshSearch, value ?? string.Empty);
            PlayerPrefs.Save();
        }
    }

    public static string TexturesPath
    {
        get => PlayerPrefs.GetString(KeyTextures, string.Empty);
        set
        {
            PlayerPrefs.SetString(KeyTextures, value ?? string.Empty);
            PlayerPrefs.Save();
        }
    }
}

