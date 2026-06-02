using System;
using System.Collections;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-only settings panel for OGRE runtime import.
/// Attach to your "3rdSettingsManager" canvas/root.
/// </summary>
public sealed class ThirdSettingsManagerPanel : MonoBehaviour
{
    [Header("UI (optional)")]
    public TMP_InputField ogreXmlConverterInput;
    public Button ogreXmlConverterBrowseButton;

    public TMP_InputField meshSearchPathInput;
    public Button meshSearchPathBrowseButton;

    public TMP_InputField texturesPathInput;
    public Button texturesPathBrowseButton;

    [Tooltip("导入 mesh 目录；也可在 Inspector 把 OnClick 指向 OnClickImportAllMeshes")]
    public Button importAllMeshesButton;

    [Tooltip("导入 textures 目录 PNG；也可把 OnClick 指向 OnClickImportAllTextures")]
    public Button importAllTexturesButton;

    [Tooltip("可选。显示批量导入状态")]
    public TextMeshProUGUI importStatusText;

    void Awake()
    {
        ResolvePathRowRefs();

        // Initialize UI from saved values
        if (ogreXmlConverterInput != null)
            ogreXmlConverterInput.SetTextWithoutNotify(OgreRuntimeSettings.OgreXmlConverterPath);
        if (meshSearchPathInput != null)
            meshSearchPathInput.SetTextWithoutNotify(OgreRuntimeSettings.MeshSearchPath);
        if (texturesPathInput != null)
            texturesPathInput.SetTextWithoutNotify(OgreRuntimeSettings.TexturesPath);

        // Wire events
        if (ogreXmlConverterInput != null)
            ogreXmlConverterInput.onEndEdit.AddListener(v => OgreRuntimeSettings.OgreXmlConverterPath = v?.Trim() ?? "");
        if (meshSearchPathInput != null)
            meshSearchPathInput.onEndEdit.AddListener(v => OgreRuntimeSettings.MeshSearchPath = v?.Trim() ?? "");
        if (texturesPathInput != null)
            texturesPathInput.onEndEdit.AddListener(v => OgreRuntimeSettings.TexturesPath = v?.Trim() ?? "");

        if (ogreXmlConverterBrowseButton != null)
            ogreXmlConverterBrowseButton.onClick.AddListener(() =>
            {
                var p = TryBrowseFile("Select OgreXMLConverter.exe", "exe");
                if (!string.IsNullOrEmpty(p))
                {
                    OgreRuntimeSettings.OgreXmlConverterPath = p;
                    ogreXmlConverterInput?.SetTextWithoutNotify(p);
                }
            });

        if (meshSearchPathBrowseButton != null)
            meshSearchPathBrowseButton.onClick.AddListener(() =>
            {
                var p = TryBrowseFolder("Select mesh search folder");
                if (!string.IsNullOrEmpty(p))
                {
                    OgreRuntimeSettings.MeshSearchPath = p;
                    meshSearchPathInput?.SetTextWithoutNotify(p);
                }
            });

        if (texturesPathBrowseButton != null)
            texturesPathBrowseButton.onClick.AddListener(() =>
            {
                var p = TryBrowseFolder("Select textures folder");
                if (!string.IsNullOrEmpty(p))
                {
                    OgreRuntimeSettings.TexturesPath = p;
                    texturesPathInput?.SetTextWithoutNotify(p);
                }
            });

        if (importAllMeshesButton != null)
            importAllMeshesButton.onClick.AddListener(OnClickImportAllMeshes);
        if (importAllTexturesButton == null)
        {
            var texBtn = transform.Find("ImportAllTexturesButton");
            if (texBtn != null)
                importAllTexturesButton = texBtn.GetComponent<Button>();
        }
        if (importAllTexturesButton != null)
            importAllTexturesButton.onClick.AddListener(OnClickImportAllTextures);
    }

    void ResolvePathRowRefs()
    {
        if (meshSearchPathInput == null || meshSearchPathBrowseButton == null)
            BindPathRow(transform, "Mesh files path", ref meshSearchPathInput, ref meshSearchPathBrowseButton);
        if (texturesPathInput == null || texturesPathBrowseButton == null)
            BindPathRow(transform, "texturesPath", ref texturesPathInput, ref texturesPathBrowseButton);
    }

    static void BindPathRow(Transform root, string rowName, ref TMP_InputField input, ref Button browse)
    {
        var row = root.Find(rowName);
        if (row == null) return;

        if (input == null)
            input = row.GetComponentInChildren<TMP_InputField>(true);
        if (browse == null)
        {
            var btn = row.Find("BrowseButton");
            if (btn != null)
                browse = btn.GetComponent<Button>();
        }
    }

    /// <summary>将 mesh 目录导入 <see cref="OgreRuntimeImporter.RuntimeMeshLibrary"/>。</summary>
    public void OnClickImportAllMeshes() => StartCoroutine(ImportMeshesCoroutine());

    /// <summary>将 textures 目录 PNG 导入 <see cref="OgreRuntimeImporter.RuntimeTextureLibrary"/>。</summary>
    public void OnClickImportAllTextures() => StartCoroutine(ImportTexturesCoroutine());

    static string GetTexturesPath(TMP_InputField input)
    {
        var path = input != null ? input.text?.Trim() : null;
        if (string.IsNullOrEmpty(path))
            path = OgreRuntimeSettings.TexturesPath;
        return path;
    }

    private IEnumerator ImportMeshesCoroutine()
    {
        var meshPath = meshSearchPathInput != null ? meshSearchPathInput.text?.Trim() : null;
        if (string.IsNullOrEmpty(meshPath))
            meshPath = OgreRuntimeSettings.MeshSearchPath;
        if (string.IsNullOrWhiteSpace(meshPath) || !Directory.Exists(meshPath))
        {
            SetImportStatus("no mesh path");
            Debug.LogWarning("[ThirdSettingsManagerPanel] mesh path invalid");
            yield break;
        }

        OgreRuntimeSettings.MeshSearchPath = meshPath;

        if (importAllMeshesButton != null)
            importAllMeshesButton.interactable = false;
        SetImportStatus("importing mesh");

        var meshTask = OgreRuntimeImporter.ImportDirectoryAsync(
            meshPath,
            recursive: true,
            mergeIntoRuntimeLibrary: true,
            options: null,
            progress: null);

        while (!meshTask.IsCompleted)
            yield return null;

        if (importAllMeshesButton != null)
            importAllMeshesButton.interactable = true;

        if (meshTask.IsFaulted)
        {
            var msg = meshTask.Exception?.GetBaseException().Message ?? "UNKNOWN ERROR";
            SetImportStatus($"mesh failed: {msg}");
            Debug.LogException(meshTask.Exception);
            yield break;
        }

        var meshBatch = meshTask.Result.Count;
        var meshTotal = OgreRuntimeImporter.RuntimeMeshLibrary.Count;
        SetImportStatus($"mesh +{meshBatch} ({meshTotal})");
        Debug.Log($"[ThirdSettingsManagerPanel] mesh +{meshBatch} total {meshTotal}");
        if (Syncer.instence != null)
            Syncer.instence.updateMap();
    }

    private IEnumerator ImportTexturesCoroutine()
    {
        var texturesPath = GetTexturesPath(texturesPathInput);
        if (string.IsNullOrWhiteSpace(texturesPath) || !Directory.Exists(texturesPath))
        {
            SetImportStatus("no textures path");
            Debug.LogWarning("[ThirdSettingsManagerPanel] textures path invalid");
            yield break;
        }

        OgreRuntimeSettings.TexturesPath = texturesPath;

        if (importAllTexturesButton != null)
            importAllTexturesButton.interactable = false;
        SetImportStatus("importing textures");

        var texTask = OgreRuntimeImporter.ImportPngDirectoryAsync(
            texturesPath,
            recursive: true,
            mergeIntoRuntimeLibrary: true,
            progress: null);

        while (!texTask.IsCompleted)
            yield return null;

        if (importAllTexturesButton != null)
            importAllTexturesButton.interactable = true;

        if (texTask.IsFaulted)
        {
            var msg = texTask.Exception?.GetBaseException().Message ?? "UNKNOWN ERROR";
            SetImportStatus($"tex failed: {msg}");
            Debug.LogException(texTask.Exception);
            yield break;
        }

        var texBatch = texTask.Result.Count;
        var texTotal = OgreRuntimeImporter.RuntimeTextureLibrary.Count;
        SetImportStatus($"tex +{texBatch} ({texTotal})");
        Debug.Log($"[ThirdSettingsManagerPanel] tex +{texBatch} total {texTotal}");
        if (Syncer.instence != null)
            Syncer.instence.updateMap();
    }

    private void SetImportStatus(string message)
    {
        if (importStatusText != null)
            importStatusText.text = message;
    }

    private static string TryBrowseFile(string title, string extensionWithoutDot)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            var filter = $"{extensionWithoutDot.ToUpperInvariant()} files|*.{extensionWithoutDot}|All files|*.*";
            return RuntimeWindowsDialogs.ShowOpenFile(title, filter);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Browse file failed; please input path manually.\n{e.Message}");
            return null;
        }
#else
        return TryBrowseFileWinForms(title, extensionWithoutDot);
#endif
    }

    private static string TryBrowseFileWinForms(string title, string extensionWithoutDot)
    {
        try
        {
            var t = Type.GetType("System.Windows.Forms.OpenFileDialog, System.Windows.Forms");
            if (t == null) { Debug.LogWarning("System.Windows.Forms not available; please input path manually."); return null; }
            var dlg = Activator.CreateInstance(t);
            t.GetProperty("Title")?.SetValue(dlg, title);
            t.GetProperty("Filter")?.SetValue(dlg, $"{extensionWithoutDot.ToUpperInvariant()} files|*.{extensionWithoutDot}|All files|*.*");
            var show = t.GetMethod("ShowDialog", Type.EmptyTypes);
            var result = show?.Invoke(dlg, null);
            var fileName = t.GetProperty("FileName")?.GetValue(dlg) as string;
            if (result != null && Convert.ToInt32(result) == 1 && !string.IsNullOrEmpty(fileName))
                return fileName;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Browse file failed; please input path manually.\n{e.Message}");
        }
        return null;
    }

    private static string TryBrowseFolder(string description)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            return RuntimeWindowsDialogs.ShowBrowseFolder(description);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Browse folder failed; please input path manually.\n{e.Message}");
            return null;
        }
#else
        return TryBrowseFolderFallback(description);
#endif
    }

    private static string TryBrowseFolderFallback(string description)
    {
        try
        {
            var modern = ResolveMicrosoftWin32OpenFolderDialogType();
            if (modern != null)
            {
                var dlg = Activator.CreateInstance(modern);
                modern.GetProperty("Title")?.SetValue(dlg, description);
                var show = modern.GetMethod("ShowDialog", Type.EmptyTypes);
                var result = show?.Invoke(dlg, null);
                if (result is bool accepted && accepted)
                {
                    var folder = modern.GetProperty("FolderName")?.GetValue(dlg) as string;
                    if (!string.IsNullOrEmpty(folder))
                        return folder;
                }

                return null;
            }

            var t = Type.GetType("System.Windows.Forms.FolderBrowserDialog, System.Windows.Forms");
            if (t == null) { Debug.LogWarning("Folder browse not available; please input path manually."); return null; }
            var dlgClassic = Activator.CreateInstance(t);
            t.GetProperty("Description")?.SetValue(dlgClassic, description);
            var showClassic = t.GetMethod("ShowDialog", Type.EmptyTypes);
            var resultClassic = showClassic?.Invoke(dlgClassic, null);
            var selected = t.GetProperty("SelectedPath")?.GetValue(dlgClassic) as string;
            if (resultClassic != null && Convert.ToInt32(resultClassic) == 1 && !string.IsNullOrEmpty(selected))
                return selected;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Browse folder failed; please input path manually.\n{e.Message}");
        }
        return null;
    }

    // WPF .NET 8+ OpenFolderDialog；无 PresentationFramework 时 TryBrowseFolder 会回退到 FolderBrowserDialog。
    private static Type ResolveMicrosoftWin32OpenFolderDialogType()
    {
        var t = Type.GetType("Microsoft.Win32.OpenFolderDialog, PresentationFramework");
        if (t != null)
            return t;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!string.Equals(asm.GetName().Name, "PresentationFramework", StringComparison.OrdinalIgnoreCase))
                continue;
            t = asm.GetType("Microsoft.Win32.OpenFolderDialog");
            if (t != null)
                return t;
        }

        return null;
    }
}

