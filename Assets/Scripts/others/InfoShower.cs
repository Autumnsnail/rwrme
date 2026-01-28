using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InfoShower : MonoBehaviour
{

    private string displayText = "";
    private GUIStyle style;
    private Vector2 mouseXYinSvg;
    private Texture2D backgroundTex;
    Image background;
    Text uiText;
    void Start()
    {
        Canvas canvas;
        GameObject canvasObj = new GameObject("InfoCanvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvas.transform, false);
        background = bgObj.AddComponent<Image>();
        
        background.rectTransform.anchoredPosition = new Vector2(0       , 0);
        background.rectTransform.anchorMin = new Vector2(0, 0); 
        background.rectTransform.anchorMax = new Vector2(0, 0); 
        background.rectTransform.pivot = new Vector2(0, 0);     
        background.rectTransform.sizeDelta = new Vector2(250, 160);
        background.color = new Color(0, 0, 0, 0.6f); 
        GameObject textObj = new GameObject("InfoText");
        textObj.transform.SetParent(background.transform, false);
        uiText = textObj.AddComponent<Text>();
        uiText.color = Color.white;
        uiText.rectTransform.sizeDelta = new Vector2(250, 160);
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiText.fontSize = 14;
        uiText.alignment = TextAnchor.UpperLeft;
    }


    public void UpdateDisplay()
    {
        displayText = (mouseXYinSvg / 2).ToString() + "\n";
        if (ToolController.inste.miSelected != null)
        {
            displayText += ToolController.inste.miSelected.getInfoText();
        }
        if (uiText != null)
        {
            uiText.text = displayText;
        }
    }


    void Update()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = 0; // 设置Z值为深度
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
        mouseXYinSvg = MathOfRwrme.U3dPosToSvgPos(worldPos);
        UpdateDisplay();
    }
}
