using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class GuardianController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4.5f;
    [SerializeField] private float sprintSpeed = 12f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 16f;
    [SerializeField] private float turnSpeed = 18f;
    [SerializeField] private Transform movementCamera;
    [SerializeField] private float jumpHeight = 2.1f;
    [SerializeField] private float gravity = -22f;

    [Header("Look")]
    [SerializeField] private float yawSensitivity = 0.12f;

    private CharacterController controller;
    private float verticalVelocity;
    private float currentSpeed;
    private Vector3 planarVelocity;
    private Vector3 desiredDirection;

    public float WalkSpeed => walkSpeed;
    public float SprintSpeed => sprintSpeed;
    public bool IsSprinting { get; private set; }
    public float CurrentMoveAmount => Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.01f, sprintSpeed));
    public void SetLookSensitivity(float value) => yawSensitivity = Mathf.Clamp(value, 0.02f, 0.5f);

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        currentSpeed = 0f;
        if (movementCamera == null && Camera.main != null) movementCamera = Camera.main.transform;
    }

    private void Update()
    {
        HandleMovement();
        HandleYaw();
    }

    private void HandleMovement()
    {
        Vector2 input = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
        }
        input = Vector2.ClampMagnitude(input, 1f);

        if (movementCamera == null && Camera.main != null) movementCamera = Camera.main.transform;
        Vector3 cameraForward = movementCamera != null ? movementCamera.forward : transform.forward;
        Vector3 cameraRight = movementCamera != null ? movementCamera.right : transform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();
        desiredDirection = (cameraForward * input.y + cameraRight * input.x);
        if (desiredDirection.sqrMagnitude > 0.001f)
        {
            desiredDirection.Normalize();
            float targetAngle = Mathf.Atan2(desiredDirection.x, desiredDirection.z) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        IsSprinting = input.sqrMagnitude > 0.01f && Keyboard.current != null &&
                      (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        float targetSpeed = input.sqrMagnitude > 0.01f ? (IsSprinting ? sprintSpeed : walkSpeed) : 0f;
        float rate = targetSpeed > currentSpeed ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.deltaTime);
        planarVelocity = transform.forward * currentSpeed;

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f;
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = planarVelocity;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleYaw()
    {
        if (Mouse.current == null || Cursor.lockState != CursorLockMode.Locked) return;
        float yaw = Mouse.current.delta.ReadValue().x * yawSensitivity;
        transform.Rotate(Vector3.up * yaw);
    }
}
