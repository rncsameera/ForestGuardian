using UnityEngine;
using UnityEngine.InputSystem;

public class GuardianCameraMode : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private GameObject guardianVisual;
    [SerializeField] private Transform cameraPivot;

    [Header("First Person")]
    [SerializeField] private Vector3 firstPersonPivotOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Third Person")]
    [SerializeField] private Vector3 thirdPersonPivotOffset = new Vector3(0f, 2.5f, 0f);
    [SerializeField] private Vector3 thirdPersonCameraOffset = new Vector3(0f, 0f, -4f);

    [Header("Toggle")]
    [SerializeField] private Key toggleKey = Key.V;

    private bool firstPerson = false;

    private void Start()
    {
        if (playerCamera == null || cameraPivot == null || guardianVisual == null)
        {
            Debug.LogError("GuardianCameraMode requires a camera, CameraPivot, and GuardianVisual reference.", this);
            enabled = false;
            return;
        }

        SetCameraMode();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            firstPerson = !firstPerson;
            SetCameraMode();
        }
    }

    private void SetCameraMode()
    {
        if (playerCamera == null)
            return;

        if (firstPerson)
        {
            cameraPivot.localPosition = firstPersonPivotOffset;
            playerCamera.transform.localPosition = Vector3.zero;
            guardianVisual.SetActive(false);
        }
        else
        {
            cameraPivot.localPosition = thirdPersonPivotOffset;
            playerCamera.transform.localPosition = thirdPersonCameraOffset;
            guardianVisual.SetActive(true);
        }
    }
}
