using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// OnlyUI 布局与工具死区的单一数据源。
/// 设计基准 1920×1080 + CanvasScaler Expand；死区用 Canvas 本地坐标，避免不同分辨率下
/// 「屏幕归一化比例」与参考像素条带错位。
/// </summary>
public static class CoverUiLayout
{
    public const float RefW = 1920f;
    public const float RefH = 1080f;
    public const float CoverWidth = 344f;
    public const float ButtonColWidth = 80f;
    public const float Border = 12f;
    public const float BandFrac = 1f / 3f;

    public static float BandHeight => RefH * BandFrac;
    public static float SubmenuContentWidth => CoverWidth - ButtonColWidth;
    public static float InfoUsableWidth => CoverWidth - ButtonColWidth - Border * 2f;
    public static float InfoUsableHeight => BandHeight - Border * 2f;
    public static Vector2 InfoAnchoredPos => new Vector2(-(ButtonColWidth + Border), Border);

    public static float MiddleBandMinY => BandFrac;
    public static float MiddleBandMaxY => BandFrac * 2f;

    /// <summary>参考分辨率下 cover 左缘归一化（仅兜底；优先用 Canvas 本地判定）。</summary>
    public static float CoverLeftNorm => 1f - CoverWidth / RefW;
    public static float ContentRightNorm => 1f - ButtonColWidth / RefW;

    public const float LeftButtonSize = 80f;
    public const float CenterPopupWidth = 720f;
    public const float CenterPopupHeight = 560f;
    public const float PopupMaxWidthFrac = 0.72f;
    public const float PopupMaxHeightFrac = 0.78f;

    const float PopupPadL = 20f;
    const float PopupPadR = 44f;
    const float PopupPadB = 20f;
    const float PopupPadT = 44f;
    const string ReflowMarkerName = "_ContentReflowed";

    public static float LeftButtonX => Border;

    // -------------------------------------------------------------------------
    // CanvasScaler
    // -------------------------------------------------------------------------

    public static void ApplySharedCanvasScaler(CanvasScaler scaler)
    {
        if (scaler == null) return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefW, RefH);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight = 0.5f;
    }

    public static Canvas FindOnlyUiCanvas()
    {
        if (UIManager.instance != null)
        {
            Canvas c = UIManager.instance.GetComponent<Canvas>();
            if (c != null) return c;
            c = UIManager.instance.GetComponentInParent<Canvas>();
            if (c != null) return c;
        }
        GameObject go = GameObject.Find("OnlyUI");
        return go != null ? go.GetComponent<Canvas>() : null;
    }

    public static bool TryGetCanvasRect(out Rect canvasRect)
    {
        canvasRect = default;
        Canvas c = FindOnlyUiCanvas();
        if (c == null) return false;
        RectTransform crt = c.transform as RectTransform;
        if (crt == null) return false;
        canvasRect = crt.rect;
        return canvasRect.width > 1f && canvasRect.height > 1f;
    }

    public static bool TryScreenToCanvasLocal(Vector2 screenPos, out Vector2 local, out Rect canvasRect)
    {
        local = default;
        canvasRect = default;
        Canvas c = FindOnlyUiCanvas();
        if (c == null) return false;
        RectTransform crt = c.transform as RectTransform;
        if (crt == null) return false;
        canvasRect = crt.rect;
        Camera cam = c.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.worldCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(crt, screenPos, cam, out local);
    }

    // -------------------------------------------------------------------------
    // 死区（工具射线 / 粘贴 / 相机滚轮等）
    // -------------------------------------------------------------------------

    /// <summary>
    /// 固定 UI 带：右侧 cover 全高 + 左下三按钮列。
    /// 用 Canvas 本地像素，Expand 下超宽/超高分辨率时仍与 cover 对齐。
    /// </summary>
    public static bool IsInStaticUiDeadZone(Vector2 screenPos)
    {
        if (TryScreenToCanvasLocal(screenPos, out Vector2 local, out Rect cr))
        {
            if (local.x >= cr.xMax - CoverWidth)
                return true;

            float btnRight = cr.xMin + LeftButtonX + LeftButtonSize + Border;
            float btnTop = cr.yMin + Border + LeftButtonSize * 3f + Border;
            if (local.x >= cr.xMin && local.x <= btnRight
                && local.y >= cr.yMin && local.y <= btnTop)
                return true;

            return false;
        }

        // Canvas 未就绪时的参考分辨率兜底
        if (Screen.width <= 0 || Screen.height <= 0) return false;
        float nx = screenPos.x / Screen.width;
        float ny = screenPos.y / Screen.height;
        if (nx >= CoverLeftNorm) return true;
        float maxX = (LeftButtonX + LeftButtonSize + Border) / RefW;
        float maxY = (Border + LeftButtonSize * 3f + Border) / RefH;
        return nx <= maxX && ny <= maxY;
    }

    /// <summary>是否应屏蔽地图工具输入（固定带 + 动态面板）。</summary>
    public static bool BlocksMapToolInput(Vector2 screenPos)
    {
        if (UIManager.PointerOverDraggablePanel()) return true;
        if (UIManager.PointerOverCenterPopup()) return true;
        return IsInStaticUiDeadZone(screenPos);
    }

    public static bool BlocksMapToolInput() => BlocksMapToolInput(Input.mousePosition);

    // -------------------------------------------------------------------------
    // 布局 Apply*
    // -------------------------------------------------------------------------

    public static void ApplyMiddleBandPanel(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(CoverLeftNorm, MiddleBandMinY);
        rt.anchorMax = new Vector2(ContentRightNorm, MiddleBandMaxY);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(Border, Border);
        rt.offsetMax = new Vector2(-Border, -Border);
    }

    public static Vector2 GetClampedPopupSize()
    {
        float w = CenterPopupWidth;
        float h = CenterPopupHeight;
        if (TryGetCanvasRect(out Rect cr))
        {
            w = Mathf.Min(w, cr.width * PopupMaxWidthFrac);
            h = Mathf.Min(h, cr.height * PopupMaxHeightFrac);
        }
        return new Vector2(Mathf.Max(320f, w), Mathf.Max(280f, h));
    }

    public static void ApplyCenterPopupPanel(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = GetClampedPopupSize();
    }

    public static void ApplyLeftBottomButton(RectTransform rt, int indexFromBottom)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(LeftButtonX, Border + indexFromBottom * LeftButtonSize);
        rt.sizeDelta = new Vector2(LeftButtonSize, LeftButtonSize);
    }

    /// <summary>右侧工具子菜单：cover 上 1/3、避开按钮列。</summary>
    public static void ApplyRightToolSubmenu(RectTransform rt, float idSearchBarHeight)
    {
        if (rt == null) return;
        float panelH = BandHeight - idSearchBarHeight;
        float panelW = SubmenuContentWidth;
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-(ButtonColWidth + panelW * 0.5f), -(idSearchBarHeight + panelH * 0.5f));
        rt.sizeDelta = new Vector2(panelW, panelH);
    }

    public static void ApplyIdSearchBar(RectTransform rt, float barHeight)
    {
        if (rt == null) return;
        float panelW = SubmenuContentWidth;
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-(ButtonColWidth + panelW * 0.5f), -barHeight * 0.5f);
        rt.sizeDelta = new Vector2(panelW, barHeight);
    }

    public static void ApplyInfoBand(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = InfoAnchoredPos;
        rt.sizeDelta = new Vector2(InfoUsableWidth, InfoUsableHeight);
    }

    // -------------------------------------------------------------------------
    // 中央弹窗内容 reflow
    // -------------------------------------------------------------------------

    public static void ReflowCenterPopupContent(GameObject panel)
    {
        if (panel == null) return;
        if (panel.transform.Find(ReflowMarkerName) != null) return;

        string name = panel.name;
        if (name == "SettingsManager")
            StretchChildToContent(panel.transform, "innerText");
        else if (name == "RefManager")
            ScaleSubtreeFromDesign(panel.transform, new Vector2(250f, 320f));
        else if (name == "3rdSettingsManager")
            ScaleSubtreeFromDesign(panel.transform, new Vector2(250f, 228.16f));

        var marker = new GameObject(ReflowMarkerName);
        marker.transform.SetParent(panel.transform, false);
        marker.hideFlags = HideFlags.HideAndDontSave;
    }

    static void StretchChildToContent(Transform panel, string childName)
    {
        Transform t = panel.Find(childName);
        if (t == null) return;
        RectTransform rt = t as RectTransform;
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = new Vector2(PopupPadL, PopupPadB);
        rt.offsetMax = new Vector2(-PopupPadR, -PopupPadT);
    }

    static void ScaleSubtreeFromDesign(Transform panel, Vector2 oldDesignSize)
    {
        RectTransform panelRt = panel as RectTransform;
        float popupW = panelRt != null ? panelRt.sizeDelta.x : CenterPopupWidth;
        float popupH = panelRt != null ? panelRt.sizeDelta.y : CenterPopupHeight;
        float contentW = popupW - PopupPadL - PopupPadR;
        float contentH = popupH - PopupPadT - PopupPadB;
        float sx = contentW / Mathf.Max(1f, oldDesignSize.x);
        float sy = contentH / Mathf.Max(1f, oldDesignSize.y);
        float contentCx = (PopupPadL - PopupPadR) * 0.5f;
        float contentCy = (PopupPadB - PopupPadT) * 0.5f;

        for (int i = 0; i < panel.childCount; i++)
        {
            Transform child = panel.GetChild(i);
            if (child.name == "PopupCloseBtn" || child.name == ReflowMarkerName) continue;
            RectTransform rt = child as RectTransform;
            if (rt == null) continue;

            bool topLeft = Approx(rt.anchorMin, Vector2.up) && Approx(rt.anchorMax, Vector2.up);
            Vector2 oldSize = rt.sizeDelta;

            if (topLeft)
            {
                rt.anchoredPosition = new Vector2(
                    PopupPadL + rt.anchoredPosition.x * sx,
                    -PopupPadT + rt.anchoredPosition.y * sy);
                rt.sizeDelta = new Vector2(oldSize.x * sx, Mathf.Max(oldSize.y * sy, oldSize.y));
            }
            else
            {
                rt.anchoredPosition = new Vector2(
                    contentCx + rt.anchoredPosition.x * sx,
                    contentCy + rt.anchoredPosition.y * sy);
                rt.sizeDelta = new Vector2(oldSize.x * sx, Mathf.Max(oldSize.y * sy, oldSize.y));
            }

            ScaleNestedChildren(rt, oldSize, rt.sizeDelta);
        }
    }

    static void ScaleNestedChildren(RectTransform parent, Vector2 oldParentSize, Vector2 newParentSize)
    {
        if (parent.childCount == 0) return;
        float sx = newParentSize.x / Mathf.Max(1f, oldParentSize.x);
        float sy = newParentSize.y / Mathf.Max(1f, oldParentSize.y);
        if (Mathf.Abs(sx - 1f) < 0.001f && Mathf.Abs(sy - 1f) < 0.001f) return;

        for (int i = 0; i < parent.childCount; i++)
        {
            RectTransform rt = parent.GetChild(i) as RectTransform;
            if (rt == null) continue;
            Vector2 oldSize = rt.sizeDelta;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x * sx, rt.anchoredPosition.y * sy);
            bool stretched = (rt.anchorMax - rt.anchorMin).sqrMagnitude > 0.0001f;
            if (!stretched)
                rt.sizeDelta = new Vector2(oldSize.x * sx, Mathf.Max(oldSize.y * sy, oldSize.y));
            ScaleNestedChildren(rt, oldSize, rt.sizeDelta);
        }
    }

    static bool Approx(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 0.0001f;
}
