using UnityEngine;
using UnityEngine.UI;

public class InfoShower : MonoBehaviour
{
    const float TextPad = 8f;

    private string displayText = "";
    private Vector2 mouseXYinSvg;
    Image background;
    Text uiText;
    RectTransform bgRt;
    RectTransform textRt;

    void Start()
    {
        GameObject canvasObj = new GameObject("InfoCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        CoverUiLayout.ApplySharedCanvasScaler(scaler);

        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvas.transform, false);
        background = bgObj.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.65f);
        background.raycastTarget = false;
        bgRt = background.rectTransform;
        CoverUiLayout.ApplyInfoBand(bgRt);

        GameObject textObj = new GameObject("InfoText");
        textObj.transform.SetParent(bgObj.transform, false);
        uiText = textObj.AddComponent<Text>();
        uiText.color = Color.white;
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiText.fontSize = 14;
        uiText.alignment = TextAnchor.UpperLeft;
        uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
        uiText.verticalOverflow = VerticalWrapMode.Overflow;
        uiText.raycastTarget = false;

        textRt = uiText.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(TextPad, TextPad);
        textRt.offsetMax = new Vector2(-TextPad, -TextPad);
    }

    public void UpdateDisplay()
    {
        displayText = (mouseXYinSvg / 2).ToString() + "\n";
        if (ToolController.inste != null)
        {
            string sel = ToolController.inste.GetSelectionInfoText();
            if (!string.IsNullOrEmpty(sel))
                displayText += sel;
        }
        if (uiText != null)
            uiText.text = displayText;
    }

    void Update()
    {
        if (Camera.main == null) return;
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = 0f;
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
        mouseXYinSvg = MathOfRwrme.U3dPosToSvgPos(worldPos);
        UpdateDisplay();
    }
}
