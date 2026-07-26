using UnityEngine;

/// <summary>
/// 发行版窗口化运行时锁定客户区比例为 16:9，窗口仍可拖拽缩放。
/// 编辑器内不启用。全屏 / 无边框全屏模式不干预。
/// </summary>
public sealed class WindowAspectLock : MonoBehaviour
{
    public const float TargetAspect = 16f / 9f;
    const float AspectEpsilon = 0.008f;
    const int MinWidth = 640;
    const int MinHeight = 360;

    int _lastW;
    int _lastH;
    bool _applying;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
#if UNITY_EDITOR
        return;
#else
        if (Object.FindObjectOfType<WindowAspectLock>() != null) return;
        var go = new GameObject(nameof(WindowAspectLock));
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<WindowAspectLock>();
#endif
    }

    void Start()
    {
        _lastW = Screen.width;
        _lastH = Screen.height;
        EnforceAspect(force: true);
    }

    void LateUpdate()
    {
        EnforceAspect(force: false);
    }

    void EnforceAspect(bool force)
    {
        if (Screen.fullScreenMode != FullScreenMode.Windowed)
        {
            _lastW = Screen.width;
            _lastH = Screen.height;
            return;
        }

        int w = Screen.width;
        int h = Screen.height;
        if (w <= 0 || h <= 0) return;
        if (!force && w == _lastW && h == _lastH) return;
        if (_applying) return;

        float aspect = (float)w / h;
        if (!force && Mathf.Abs(aspect - TargetAspect) <= AspectEpsilon)
        {
            _lastW = w;
            _lastH = h;
            return;
        }

        int dw = Mathf.Abs(w - _lastW);
        int dh = Mathf.Abs(h - _lastH);
        // 角点拖拽时两边都变：以变化更大的一边为主驱动
        bool widthDriven = force || dw >= dh;

        int newW;
        int newH;
        if (widthDriven)
        {
            newW = Mathf.Max(MinWidth, w);
            newH = Mathf.Max(MinHeight, Mathf.RoundToInt(newW / TargetAspect));
            newW = Mathf.Max(MinWidth, Mathf.RoundToInt(newH * TargetAspect));
        }
        else
        {
            newH = Mathf.Max(MinHeight, h);
            newW = Mathf.Max(MinWidth, Mathf.RoundToInt(newH * TargetAspect));
            newH = Mathf.Max(MinHeight, Mathf.RoundToInt(newW / TargetAspect));
        }

        if (newW == w && newH == h)
        {
            _lastW = w;
            _lastH = h;
            return;
        }

        _applying = true;
        Screen.SetResolution(newW, newH, FullScreenMode.Windowed);
        _lastW = Screen.width;
        _lastH = Screen.height;
        _applying = false;
    }
}
