using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将 MapItem 预制体 Canvas 下的参数控件，从屏幕左下角挪到 cover 下 1/3 信息栏区域。
/// </summary>
public static class MapItemParamUiLayout
{
    const string ParamRootName = "ParamRoot";

    /// <summary>对尚未布局的 Canvas 执行一次：缩放模式 + ParamRoot + 重挂子控件。</summary>
    public static void Ensure(Transform canvasTf)
    {
        if (canvasTf == null) return;
        if (canvasTf.Find(ParamRootName) != null) return;

        Canvas canvas = canvasTf.GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = 11; // 高于 InfoShower(10)，保证可点

        CanvasScaler scaler = canvasTf.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvasTf.gameObject.AddComponent<CanvasScaler>();
        CoverUiLayout.ApplySharedCanvasScaler(scaler);

        GameObject rootGo = new GameObject(ParamRootName, typeof(RectTransform));
        rootGo.transform.SetParent(canvasTf, false);
        RectTransform root = rootGo.GetComponent<RectTransform>();
        CoverUiLayout.ApplyInfoBand(root);

        // 原控件锚在 Canvas 左下；挂到 ParamRoot 后，相对偏移落在信息栏左下角起排
        for (int i = canvasTf.childCount - 1; i >= 0; i--)
        {
            Transform child = canvasTf.GetChild(i);
            if (child == root) continue;
            child.SetParent(root, false);
        }
    }

    /// <summary>在 Canvas 或 ParamRoot 下按名查找（兼容布局前后）。</summary>
    public static Transform Find(Transform canvas, string name)
    {
        if (canvas == null || string.IsNullOrEmpty(name)) return null;
        Transform t = canvas.Find(name);
        if (t != null) return t;
        Transform root = canvas.Find(ParamRootName);
        return root != null ? root.Find(name) : null;
    }
}
