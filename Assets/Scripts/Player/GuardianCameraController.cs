using UnityEngine;
using UnityEngine.InputSystem;

public class GuardianCameraController : MonoBehaviour
{
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float lookSensitivity = 0.12f;
    [SerializeField] private float maxLookAngle = 80f;

    private float pitch;

    private void Awake()
    {
        if (cameraPivot == null)
            Debug.LogError("GuardianCameraController requires a CameraPivot reference.", this);
    }

    private void OnEnable()
    {
        if (!GameFlowUI.UIOwnsCursor) LockCursor();
    }

    private void Update()
    {
        if (!GameFlowUI.UIOwnsCursor && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (!GameFlowUI.UIOwnsCursor && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }

        if (Mouse.current == null || cameraPivot == null || Cursor.lockState != CursorLockMode.Locked)
            return;

        pitch = Mathf.Clamp(
            pitch - Mouse.current.delta.ReadValue().y * lookSensitivity,
            -maxLookAngle,
            maxLookAngle
        );
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && !GameFlowUI.UIOwnsCursor)
            LockCursor();
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
