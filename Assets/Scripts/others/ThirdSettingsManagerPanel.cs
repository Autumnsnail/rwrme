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

    [Tooltip("可选。不填且自动生成 UI 时会创建。也可在 Inspector 里把按钮 OnClick 指向 OnClickImportAllMeshes")]
    public Button importAllMeshesButton;

    [Tooltip("可选。显示批量导入状态")]
    public TextMeshProUGUI importStatusText;

    [Header("Auto build UI if missing")]
    public bool autoBuildIfMissing = true;

    void Awake()
    {
        if (autoBuildIfMissing && (ogreXmlConverterInput == null || meshSearchPathInput == null))
        {
            BuildUi();
        }

        // Initialize UI from saved values
        if (ogreXmlConverterInput != null)
            ogreXmlConverterInput.SetTextWithoutNotify(OgreRuntimeSettings.OgreXmlConverterPath);
        if (meshSearchPathInput != null)
            meshSearchPathInput.SetTextWithoutNotify(OgreRuntimeSettings.MeshSearchPath);

        // Wire events
        if (ogreXmlConverterInput != null)
            ogreXmlConverterInput.onEndEdit.AddListener(v => OgreRuntimeSettings.OgreXmlConverterPath = v?.Trim() ?? "");
        if (meshSearchPathInput != null)
            meshSearchPathInput.onEndEdit.AddListener(v => OgreRuntimeSettings.MeshSearchPath = v?.Trim() ?? "");

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

        if (importAllMeshesButton != null)
            importAllMeshesButton.onClick.AddListener(OnClickImportAllMeshes);
    }

    /// <summary>
    /// 供 UI Button 绑定：将「Mesh 文件目录」下全部 .mesh / .mesh.xml 导入到 <see cref="OgreRuntimeImporter.RuntimeMeshLibrary"/>。
    /// </summary>
    public void OnClickImportAllMeshes() => StartCoroutine(ImportDirectoryCoroutine());

    private IEnumerator ImportDirectoryCoroutine()
    {
        var path = meshSearchPathInput != null ? meshSearchPathInput.text?.Trim() : null;
        if (string.IsNullOrEmpty(path))
            path = OgreRuntimeSettings.MeshSearchPath;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            SetImportStatus("no path");
            Debug.LogWarning("[ThirdSettingsManagerPanel] Mesh path in validate");
            yield break;
        }

        OgreRuntimeSettings.MeshSearchPath = path;

        if (importAllMeshesButton != null)
            importAllMeshesButton.interactable = false;
        SetImportStatus("importing");

        var task = OgreRuntimeImporter.ImportDirectoryAsync(
            path,
            recursive: true,
            mergeIntoRuntimeLibrary: true,
            options: null,
            progress: null);

        while (!task.IsCompleted)
            yield return null;

        if (importAllMeshesButton != null)
            importAllMeshesButton.interactable = true;

        if (task.IsFaulted)
        {
            var msg = task.Exception?.GetBaseException().Message ?? "UNKONWN ERROR";
            SetImportStatus($"failed: {msg}");
            Debug.LogException(task.Exception);
            yield break;
        }

        var batch = task.Result.Count;
        var total = OgreRuntimeImporter.RuntimeMeshLibrary.Count;
        SetImportStatus($"success: {batch} ,total: {total} ");
        Debug.Log($"success: {batch}, total: {total}");
        Syncer.instence.updateMap();
    }

    private void SetImportStatus(string message)
    {
        if (importStatusText != null)
            importStatusText.text = message;
    }

    private void BuildUi()
    {
        // Ensure this object has a RectTransform
        var rt = gameObject.GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        var layout = GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 8;
            layout.padding = new RectOffset(12, 12, 12, 12);
        }

        var fitter = GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        var title = CreateLabel(transform, "OGRE import Setting", 20, FontStyles.Bold);
        title.alignment = TextAlignmentOptions.Center;

        (ogreXmlConverterInput, ogreXmlConverterBrowseButton) =
            CreatePathRow(transform, "OgreXMLConverter.exe Path", "D:\\Tools\\OgreXMLConverter.exe", browseLabel: "Select");

        (meshSearchPathInput, meshSearchPathBrowseButton) =
            CreatePathRow(transform, "Mesh files path", "D:\\assets\\meshes", browseLabel: "select");

        importStatusText = CreateLabel(transform, "ready", 12, FontStyles.Normal);
        importStatusText.alignment = TextAlignmentOptions.Left;

        importAllMeshesButton = CreateFullWidthButton(transform, "load mesh");
    }

    private static Button CreateFullWidthButton(Transform parent, string label)
    {
        var btnGo = new GameObject("ImportAllMeshesButton", typeof(RectTransform));
        btnGo.transform.SetParent(parent, false);
        var img = btnGo.AddComponent<Image>();
        img.color = new Color(0.18f, 0.42f, 0.32f, 1f);
        var btn = btnGo.AddComponent<Button>();
        var le = btnGo.AddComponent<LayoutElement>();
        le.minHeight = 36;
        le.preferredHeight = 36;

        var bt = new GameObject("Text", typeof(RectTransform));
        bt.transform.SetParent(btnGo.transform, false);
        var rect = bt.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var tmp = bt.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 14;
        tmp.color = Color.white;

        return btn;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string text, float fontSize, FontStyles style)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        return tmp;
    }

    private static (TMP_InputField input, Button button) CreatePathRow(Transform parent, string label, string placeholder, string browseLabel)
    {
        var row = new GameObject(label, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandHeight = false;
        h.childForceExpandWidth = true;
        h.spacing = 8;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row.transform, false);
        var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
        labelTmp.text = label;
        labelTmp.fontSize = 14;
        labelTmp.color = Color.white;
        labelTmp.enableWordWrapping = false;
        var labelLayout = labelGo.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 160;

        var inputGo = new GameObject("InputField", typeof(RectTransform));
        inputGo.transform.SetParent(row.transform, false);
        var img = inputGo.AddComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.14f, 0.95f);

        var input = inputGo.AddComponent<TMP_InputField>();
        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(inputGo.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = 14;
        text.color = Color.white;
        text.text = "";
        text.enableWordWrapping = false;
        input.textComponent = text;

        var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
        placeholderGo.transform.SetParent(inputGo.transform, false);
        var ph = placeholderGo.AddComponent<TextMeshProUGUI>();
        ph.fontSize = 14;
        ph.color = new Color(1f, 1f, 1f, 0.35f);
        ph.text = placeholder;
        input.placeholder = ph;

        var inputLayout = inputGo.AddComponent<LayoutElement>();
        inputLayout.minHeight = 32;

        var btnGo = new GameObject("BrowseButton", typeof(RectTransform));
        btnGo.transform.SetParent(row.transform, false);
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.25f, 0.25f, 0.28f, 1f);
        var btn = btnGo.AddComponent<Button>();
        var btnText = btnGo.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText == null)
        {
            var bt = new GameObject("Text", typeof(RectTransform));
            bt.transform.SetParent(btnGo.transform, false);
            btnText = bt.AddComponent<TextMeshProUGUI>();
        }
        btnText.text = browseLabel;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.fontSize = 14;
        btnText.color = Color.white;
        var btnLayout = btnGo.AddComponent<LayoutElement>();
        btnLayout.preferredWidth = 96;
        btnLayout.minHeight = 32;

        return (input, btn);
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

