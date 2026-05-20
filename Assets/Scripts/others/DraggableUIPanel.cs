using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 挂在"抓手"（如标题栏）上，拖动时平移 <see cref="target"/> 面板。
/// 用 offsetMin/offsetMax 同步位移，对拉伸锚点 / 点锚点都通用；
/// 位移量经父矩形换算，兼容任意 Canvas renderMode 与 scaleFactor。
/// </summary>
public class DraggableUIPanel : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Tooltip("被拖动的面板根；为空时回退到自身父节点的 RectTransform")]
    public RectTransform target;

    [Tooltip("拖完把面板夹在父矩形内，至少留住可见，防止拖丢")]
    public bool clampToScreen = true;

    public void OnPointerDown(PointerEventData e)
    {
        // 拖动时置顶，避免被其它面板压住
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
        target.offsetMin += d;
        target.offsetMax += d;

        if (clampToScreen) Clamp(parent);
    }

    void Clamp(RectTransform parent)
    {
        Rect p = parent.rect;                 // 父本地坐标系
        Rect t = target.rect;
        Vector3[] corners = new Vector3[4];
        target.GetLocalCorners(corners);      // 相对 target 自身
        // target 左下角在父坐标系下的位置
        Vector2 min = (Vector2)target.localPosition + new Vector2(corners[0].x, corners[0].y);
        Vector2 max = (Vector2)target.localPosition + new Vector2(corners[2].x, corners[2].y);

        Vector2 fix = Vector2.zero;
        if (min.x < p.xMin) fix.x += p.xMin - min.x;
        if (max.x > p.xMax) fix.x -= max.x - p.xMax;
        if (min.y < p.yMin) fix.y += p.yMin - min.y;
        if (max.y > p.yMax) fix.y -= max.y - p.yMax;

        if (fix != Vector2.zero)
        {
            target.offsetMin += fix;
            target.offsetMax += fix;
        }
    }
}
