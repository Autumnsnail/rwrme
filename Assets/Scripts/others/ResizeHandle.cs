using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 挂在面板右下角的小手柄：拖动改变 <see cref="target"/> 的宽高（窗口式 resize）。
/// 保持左上角不动——只动右边界(offsetMax.x)与下边界(offsetMin.y)，对拉伸/点锚点都通用。
/// </summary>
public class ResizeHandle : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public RectTransform target;
    public Vector2 minSize = new Vector2(180f, 140f);

    public void OnPointerDown(PointerEventData e)
    {
        if (target != null) target.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData e)
    {
        if (target == null) return;
        RectTransform parent = target.parent as RectTransform;
        if (parent == null) return;

        Vector2 prev, cur;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, e.position - e.delta, e.pressEventCamera, out prev))
            return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, e.position, e.pressEventCamera, out cur))
            return;

        Vector2 d = cur - prev;

        // 右边界：offsetMax.x 增大 → 变宽；下边界：offsetMin.y 减小 → 变高
        Vector2 newMax = target.offsetMax + new Vector2(d.x, 0f);
        Vector2 newMin = target.offsetMin + new Vector2(0f, d.y);

        // 尺寸下限夹取（用锚点跨度 + 偏移推回当前像素尺寸）
        float ph = parent.rect.height;
        float pw = parent.rect.width;
        float spanW = (target.anchorMax.x - target.anchorMin.x) * pw;
        float spanH = (target.anchorMax.y - target.anchorMin.y) * ph;
        float w = spanW + newMax.x - newMin.x;
        float h = spanH + newMax.y - newMin.y;

        if (w >= minSize.x) target.offsetMax = new Vector2(newMax.x, target.offsetMax.y);
        if (h >= minSize.y) target.offsetMin = new Vector2(target.offsetMin.x, newMin.y);
    }
}
