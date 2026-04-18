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

    [Tooltip("飞行模式下滚轮调节移动速度；与 Mouse ScrollWheel 相乘后作为 Exp 指数")]
    public float flightScrollWheelIntensity = 3f;
    public float minFlightMoveSpeed = 0.25f;
    public float maxFlightMoveSpeed = 150f;
    public float minFlightFastMoveSpeed = 0.5f;
    public float maxFlightFastMoveSpeed = 450f;

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

        flightRotation.x = transform.eulerAngles.x;
        flightRotation.y = transform.eulerAngles.y;

        topDownPosition = transform.position;
        topDownPosition.y = topDownHeight;

        InitializeCameraMode();

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
        cam.orthographic = true;
        cam.orthographicSize = orthographicSize;

        transform.position = topDownPosition;
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    void SetupFlightCamera()
    {
        cam.orthographic = false;

        transform.rotation = Quaternion.Euler(flightRotation.x, flightRotation.y, 0f);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleModeKey))
        {
            ToggleCameraMode();
        }

        if (Input.GetKeyDown(KeyCode.Escape) && currentMode == CameraMode.FreeFlight)
        {
            ToggleCursorLock();
        }

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
            currentMode = CameraMode.TopDown;

            topDownPosition = transform.position;
            topDownPosition.y = topDownHeight;

            SetupTopDownCamera();

            UnlockCursor();

        }
        else
        {
            currentMode = CameraMode.FreeFlight;

            flightRotation.y = transform.eulerAngles.y;
            flightRotation.x = transform.eulerAngles.x;

            SetupFlightCamera();

            LockCursor();

        }
    }

    void UpdateFlightMode()
    {
        if (isCursorLocked)
        {
            float mouseX = Input.GetAxis("Mouse X") * flightMouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * flightMouseSensitivity * (invertY ? 1f : -1f);

            flightRotation.y += mouseX;
            flightRotation.x += mouseY;
            flightRotation.x = Mathf.Clamp(flightRotation.x, -90f, 90f);

            transform.rotation = Quaternion.Euler(flightRotation.x, flightRotation.y, 0f);
        }

        Vector3 moveDirection = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) moveDirection += transform.forward;
        if (Input.GetKey(KeyCode.S)) moveDirection -= transform.forward;
        if (Input.GetKey(KeyCode.A)) moveDirection -= transform.right;
        if (Input.GetKey(KeyCode.D)) moveDirection += transform.right;

        if (Input.GetKey(KeyCode.Q)) moveDirection -= Vector3.up;
        if (Input.GetKey(KeyCode.E)) moveDirection += Vector3.up;

        if (moveDirection != Vector3.zero)
        {
            moveDirection.Normalize();
            float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? flightFastMoveSpeed : flightMoveSpeed;
            transform.position += moveDirection * currentSpeed * Time.deltaTime;
        }

        ApplyFlightScrollSpeedAdjust();
    }

    void ApplyFlightScrollSpeedAdjust()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 1e-5f) return;

        float speedRatio = flightFastMoveSpeed / Mathf.Max(0.001f, flightMoveSpeed);
        float scale = Mathf.Exp(scroll * flightScrollWheelIntensity);
        flightMoveSpeed = Mathf.Clamp(flightMoveSpeed * scale, minFlightMoveSpeed, maxFlightMoveSpeed);
        flightFastMoveSpeed = Mathf.Clamp(flightMoveSpeed * speedRatio, minFlightFastMoveSpeed, maxFlightFastMoveSpeed);
        if (flightFastMoveSpeed < flightMoveSpeed * 1.01f)
            flightFastMoveSpeed = Mathf.Min(maxFlightFastMoveSpeed, flightMoveSpeed * 1.01f);
    }

    void UpdateTopDownMode()
    {
        HandleMouseDrag();

        HandleKeyboardMovement();

        HandleZoom();

        transform.position = topDownPosition;

        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }


    void HandleMouseDrag()
    {
        if (Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(1))
        {
            isDragging = true;
            lastMousePosition = Input.mousePosition;
        }

        if ((Input.GetMouseButton(2) || Input.GetMouseButton(1)) && isDragging)
        {
            Vector3 currentMousePosition = Input.mousePosition;
            Vector3 mouseDelta = currentMousePosition - lastMousePosition;

            if (mouseDelta.magnitude > 0.1f)
            {
                Vector3 worldDelta = new Vector3(-mouseDelta.x, 0, -mouseDelta.y) * topDownDragSpeed * 0.01f;

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
            cam.orthographicSize -= scroll * zoomSpeed * cam.orthographicSize * 0.005f;
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

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontSize = 16;

        string modeText = currentMode == CameraMode.FreeFlight ? "\u98de\u884c\u6a21\u5f0f\uff08\u900f\u89c6\uff09" : "\u4fef\u89c6\u89d2\u6a21\u5f0f\uff08\u6b63\u4ea4\uff09";
        string cursorText = isCursorLocked ? "\u9f20\u6807\u9501\u5b9a" : "\u9f20\u6807\u81ea\u7531";
        string controlsText = currentMode == CameraMode.TopDown ?
            "\u4e2d\u952e/\u53f3\u952e\u62d6\u52a8 | \u6eda\u8f6e\u7f29\u653e | WASD\u79fb\u52a8" :
            "\u9f20\u6807\u89c6\u89d2 | WASD\u79fb\u52a8 | Q/E\u5347\u964d | \u6eda\u8f6e\u8c03\u901f\u5ea6";

        GUI.Label(new Rect(10, 10, 400, 30), "\u76f8\u673a\u6a21\u5f0f: " + modeText, style);

        if (currentMode == CameraMode.FreeFlight)
        {
            GUI.Label(new Rect(10, 30, 400, 30), "\u72b6\u6001: " + cursorText + " (ESC\u5207\u6362)", style);
            GUI.Label(new Rect(10, 90, 400, 30),
                "\u79fb\u52a8\u901f\u5ea6: " + flightMoveSpeed.ToString("F1") + " / \u52a0\u901f " + flightFastMoveSpeed.ToString("F1"), style);
        }

        GUI.Label(new Rect(10, 50, 600, 30), "\u63a7\u5236: " + controlsText, style);
        GUI.Label(new Rect(10, 70, 400, 30), "\u6309 Tab \u5207\u6362\u6a21\u5f0f", style);

        if (currentMode == CameraMode.TopDown && cam != null)
        {
            GUI.Label(new Rect(10, 110, 400, 30), "\u6b63\u4ea4\u5927\u5c0f: " + cam.orthographicSize.ToString("F1"), style);
        }
    }
}
