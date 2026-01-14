using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfoShower : MonoBehaviour
{

    private string displayText = "";
    private GUIStyle style;
    private Vector2 mouseXYinSvg;
    private Texture2D backgroundTex;
    void Start()
    {
        backgroundTex = new Texture2D(1, 1);
        backgroundTex.SetPixel(0, 0, new Color(0, 0, 0, 0.6f)); // 半透明黑色
        backgroundTex.Apply();
        style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.normal.background=backgroundTex;
        style.fontSize = 14;
        style.padding = new RectOffset(10, 10, 10, 10);
    }


    public void UpdateDisplay()
    {
        displayText = (mouseXYinSvg/2).ToString()+"\n";
        if (ToolController.inste.miSelected != null)
        {
            displayText+=    ToolController.inste.miSelected.getInfoText();
        }

    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, Screen.height - 160, 250, 160),
                 displayText, style);
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
