using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToolHinter : MonoBehaviour
{
    public string displayText = "Hello World";

    // 文本样式
    public Color textColor = Color.white;
    public int fontSize = 20;

    // 屏幕边距，避免文字太靠近边缘
    public Vector2 padding = new Vector2(10, 10);

    // 字体样式
    public FontStyle fontStyle = FontStyle.Normal;

    // 文字对齐方式
    public TextAnchor textAlignment = TextAnchor.UpperLeft;

    // 存储GUIStyle用于自定义样式
    private GUIStyle guiStyle;
    void Start()
    {
        guiStyle = new GUIStyle();
        guiStyle.normal.textColor = textColor;
        guiStyle.fontSize = fontSize;
        guiStyle.fontStyle = fontStyle;
        guiStyle.alignment = textAlignment;

    }

    // Update is called once per frame
    void Update()
    {
        displayText = ToolController.inste.currentTool.m_name;
        Vector3 mousePos = Input.mousePosition;

    }

    void OnGUI()
    {
        // 获取当前鼠标位置
        Vector3 mousePosition = Input.mousePosition;

        // 转换坐标（Unity的GUI坐标系原点在左上角）
        float guiX = mousePosition.x + padding.x;
        float guiY = Screen.height - mousePosition.y + padding.y;

        // 创建文本位置矩形
        Rect textRect = new Rect(guiX, guiY, 200, 50);

        // 使用GUIStyle绘制文本
        GUI.Label(textRect, displayText, guiStyle);

        // 或者使用简单的GUI.Label（使用默认样式）
        // GUI.Label(textRect, displayText);
    }

    void OnValidate()
    {
        if (guiStyle != null)
        {
            guiStyle.normal.textColor = textColor;
            guiStyle.fontSize = fontSize;
            guiStyle.fontStyle = fontStyle;
            guiStyle.alignment = textAlignment;
        }
    }
}
