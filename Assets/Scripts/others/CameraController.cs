using UnityEngine;

public class DualModeCameraController : MonoBehaviour
{
    [Header("模式设置")]
    public CameraMode currentMode = CameraMode.FreeFlight;

    [Header("飞行模式设置")]
    public float flightMoveSpeed = 5f;
    public float flightFastMoveSpeed = 15f;
    public float flightMouseSensitivity = 2f;
    public bool invertY = false;

    [Header("俯视角模式设置")]
    public float topDownMoveSpeed = 10f;
    public float topDownDragSpeed = 2f;
    public float topDownHeight = 10f;
    public float orthographicSize = 5f;
    public float minOrthoSize = 2f;
    public float maxOrthoSize = 20f;
    public float zoomSpeed = 2f;

    [Header("通用设置")]
    public KeyCode toggleModeKey = KeyCode.Tab;

    // 私有变量
    private Vector3 flightRotation = Vector3.zero;
    private Vector3 topDownPosition;
    private bool isCursorLocked = true;
    private Camera cam;
    private Vector3 dragOrigin;
    private bool isDragging = false;
    private Vector3 lastMousePosition;

    public enum CameraMode
    {
        FreeFlight,
        TopDown
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
            if (cam != null)
            {
                transform.position = cam.transform.position;
                transform.rotation = cam.transform.rotation;
            }
        }

        // 初始化飞行模式
        flightRotation.x = transform.eulerAngles.x;
        flightRotation.y = transform.eulerAngles.y;

        // 初始化俯视角位置
        topDownPosition = transform.position;
        topDownPosition.y = topDownHeight;

        // 根据当前模式初始化相机设置
        InitializeCameraMode();

        // 锁定鼠标
        LockCursor();
    }

    void InitializeCameraMode()
    {
        if (cam != null)
        {
            if (currentMode == CameraMode.TopDown)
            {
                SetupTopDownCamera();
            }
            else
            {
                SetupFlightCamera();
            }
        }
    }

    void SetupTopDownCamera()
    {
        // 设置为正交相机
        cam.orthographic = true;
        cam.orthographicSize = orthographicSize;

        // 设置俯视角位置和旋转
        transform.position = topDownPosition;
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    void SetupFlightCamera()
    {
        // 设置为透视相机
        cam.orthographic = false;

        // 恢复飞行模式的旋转
        transform.rotation = Quaternion.Euler(flightRotation.x, flightRotation.y, 0f);
    }

    void Update()
    {
        // 切换模式
        if (Input.GetKeyDown(toggleModeKey))
        {
            ToggleCameraMode();
        }

        // 切换鼠标锁定状态（仅在飞行模式下有效）
        if (Input.GetKeyDown(KeyCode.Escape) && currentMode == CameraMode.FreeFlight)
        {
            ToggleCursorLock();
        }

        // 根据当前模式更新相机
        switch (currentMode)
        {
            case CameraMode.FreeFlight:
                UpdateFlightMode();
                break;
            case CameraMode.TopDown:
                UpdateTopDownMode();
                break;
        }
    }

    void ToggleCameraMode()
    {
        if (currentMode == CameraMode.FreeFlight)
        {
            // 切换到俯视角模式
            currentMode = CameraMode.TopDown;

            // 保存当前位置，但调整高度
            topDownPosition = transform.position;
            topDownPosition.y = topDownHeight;

            SetupTopDownCamera();

            // 在俯视角模式下解锁鼠标以便拖动
            UnlockCursor();

        }
        else
        {
            // 切换到飞行模式
            currentMode = CameraMode.FreeFlight;

            // 重置旋转为当前方向
            flightRotation.y = transform.eulerAngles.y;
            flightRotation.x = transform.eulerAngles.x;

            SetupFlightCamera();

            // 在飞行模式下锁定鼠标
            LockCursor();

        }
    }

    void UpdateFlightMode()
    {
        if (isCursorLocked)
        {
            // 鼠标视角控制
            float mouseX = Input.GetAxis("Mouse X") * flightMouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * flightMouseSensitivity * (invertY ? 1f : -1f);

            flightRotation.y += mouseX;
            flightRotation.x += mouseY;
            flightRotation.x = Mathf.Clamp(flightRotation.x, -90f, 90f);

            transform.rotation = Quaternion.Euler(flightRotation.x, flightRotation.y, 0f);
        }

        // 移动控制
        Vector3 moveDirection = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) moveDirection += transform.forward;
        if (Input.GetKey(KeyCode.S)) moveDirection -= transform.forward;
        if (Input.GetKey(KeyCode.A)) moveDirection -= transform.right;
        if (Input.GetKey(KeyCode.D)) moveDirection += transform.right;

        // 垂直移动 (Q和E键)
        if (Input.GetKey(KeyCode.Q)) moveDirection -= Vector3.up;
        if (Input.GetKey(KeyCode.E)) moveDirection += Vector3.up;

        // 标准化移动方向并应用速度
        if (moveDirection != Vector3.zero)
        {
            moveDirection.Normalize();
            float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? flightFastMoveSpeed : flightMoveSpeed;
            transform.position += moveDirection * currentSpeed * Time.deltaTime;
        }
    }

    void UpdateTopDownMode()
    {
        // 鼠标拖动控制
        HandleMouseDrag();

        // 键盘移动控制（可选）
        HandleKeyboardMovement();

        // 鼠标滚轮缩放
        HandleZoom();

        // 更新相机位置
        transform.position = topDownPosition;

        // 确保相机始终朝下
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    void HandleMouseDrag()
    {
        // 鼠标中键或右键拖动
        if (Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(1))
        {
            isDragging = true;
            lastMousePosition = Input.mousePosition;
        }

        if ((Input.GetMouseButton(2) || Input.GetMouseButton(1)) && isDragging)
        {
            Vector3 currentMousePosition = Input.mousePosition;
            Vector3 mouseDelta = currentMousePosition - lastMousePosition;

            // 使用屏幕像素偏移来计算移动，避免世界坐标计算的抖动
            if (mouseDelta.magnitude > 0.1f) // 添加一个小阈值避免微小移动
            {
                // 将屏幕像素偏移转换为世界空间移动
                Vector3 worldDelta = new Vector3(-mouseDelta.x, 0, -mouseDelta.y) * topDownDragSpeed * 0.01f;

                // 根据正交大小调整移动速度，这样缩放时拖动感觉更自然
                worldDelta *= cam.orthographicSize;

                topDownPosition += worldDelta;
            }

            lastMousePosition = currentMousePosition;
        }

        if (Input.GetMouseButtonUp(2) || Input.GetMouseButtonUp(1))
        {
            isDragging = false;
        }
    }

    void HandleKeyboardMovement()
    {
        // 键盘移动控制（可选功能）
        Vector3 moveDirection = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) moveDirection += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) moveDirection += Vector3.back;
        if (Input.GetKey(KeyCode.A)) moveDirection += Vector3.left;
        if (Input.GetKey(KeyCode.D)) moveDirection += Vector3.right;

        if (moveDirection != Vector3.zero)
        {
            moveDirection.Normalize();
            float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? topDownMoveSpeed * 2f : topDownMoveSpeed;
            topDownPosition += moveDirection * currentSpeed * Time.deltaTime;
        }
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0 && cam != null)
        {
            cam.orthographicSize -= scroll * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minOrthoSize, maxOrthoSize);
            orthographicSize = cam.orthographicSize;
        }
    }

    void ToggleCursorLock()
    {
        isCursorLocked = !isCursorLocked;

        if (isCursorLocked)
        {
            LockCursor();
        }
        else
        {
            UnlockCursor();
        }
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 在Inspector中显示当前模式
    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontSize = 16;

        string modeText = currentMode == CameraMode.FreeFlight ? "飞行模式（透视）" : "俯视角模式（正交）";
        string cursorText = isCursorLocked ? "鼠标锁定" : "鼠标自由";
        string controlsText = currentMode == CameraMode.TopDown ?
            "中键/右键拖动 | 滚轮缩放 | WASD移动" :
            "鼠标视角 | WASD移动 | Q/E升降";

        GUI.Label(new Rect(10, 10, 400, 30), $"相机模式: {modeText}", style);

        if (currentMode == CameraMode.FreeFlight)
        {
            GUI.Label(new Rect(10, 30, 400, 30), $"状态: {cursorText} (ESC切换)", style);
        }

        GUI.Label(new Rect(10, 50, 600, 30), $"控制: {controlsText}", style);
        GUI.Label(new Rect(10, 70, 400, 30), $"按 Tab 切换模式", style);

        if (currentMode == CameraMode.TopDown && cam != null)
        {
            GUI.Label(new Rect(10, 90, 400, 30), $"正交大小: {cam.orthographicSize:F1}", style);
        }
    }
}