using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Context-sensitive interaction selector.
/// The object under the crosshair wins; large area/task colliders are never
/// allowed to steal an interaction from a smaller visible object.
/// </summary>
public sealed class InteractionSystem : MonoBehaviour
{
    [SerializeField, Min(0.5f)] private float interactionRange = 4f;
    [SerializeField, Range(5f, 80f)] private float focusAngle = 48f;
    [SerializeField, Min(0.01f)] private float scanInterval = 0.06f;
    [SerializeField] private Camera playerCamera;

    private readonly Collider[] nearbyColliders = new Collider[128];
    private Interactable currentTarget;
    private float nextScanTime;

    public Interactable CurrentTarget => currentTarget;

    private void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
    }

    private void Update()
    {
        if (playerCamera == null) playerCamera = Camera.main;

        if (Time.unscaledTime >= nextScanTime)
        {
            nextScanTime = Time.unscaledTime + scanInterval;
            FindFocusedTarget();
        }

        GameplayHUD.Instance?.SetInteractionPrompt(
            currentTarget == null ? string.Empty : "E   " + GetActionLabel(currentTarget));

        bool pressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.E);
#endif

        if (pressed && currentTarget != null)
            currentTarget.Interact(this);
    }

    private string GetActionLabel(Interactable target)
    {
        string name = string.IsNullOrWhiteSpace(target.DisplayName) ? target.gameObject.name : target.DisplayName;

        if (IsWasteTarget(target)) return "PICK UP " + name.ToUpperInvariant();
        if (Contains(name, "deer") || Contains(name, "rabbit") || Contains(name, "animal") ||
            Contains(name, "goat") || Contains(name, "sheep") || Contains(name, "cow") || Contains(name, "dog"))
            return target.Status.IndexOf("sick", System.StringComparison.OrdinalIgnoreCase) >= 0
                ? "TREAT " + name.ToUpperInvariant()
                : "EXAMINE " + name.ToUpperInvariant();
        if (Contains(name, "tree") || Contains(name, "sapling") || Contains(name, "plant"))
            return "INSPECT " + name.ToUpperInvariant();
        if (Contains(name, "pond") || Contains(name, "water") || Contains(name, "stream"))
            return "INVESTIGATE " + name.ToUpperInvariant();
        if (Contains(name, "shelter") || Contains(name, "home") || Contains(name, "house"))
            return "ENTER " + name.ToUpperInvariant();

        string prompt = target.Prompt;
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Equals("Interact", System.StringComparison.OrdinalIgnoreCase))
            return "INTERACT WITH " + name.ToUpperInvariant();
        return prompt.StartsWith("E") ? prompt.Substring(1).Trim().ToUpperInvariant() : prompt.ToUpperInvariant();
    }

    private void FindFocusedTarget()
    {
        currentTarget = null;
        if (playerCamera == null) return;

        Vector3 origin = transform.position + Vector3.up * 1.1f;
        Vector3 forward = playerCamera.transform.forward;

        // 1. Exact crosshair hit. This is the strongest signal.
        if (Physics.Raycast(playerCamera.transform.position, forward, out RaycastHit hit,
            interactionRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            Interactable direct = hit.collider.GetComponent<Interactable>();
            if (direct == null) direct = hit.collider.GetComponentInParent<Interactable>();

            if (IsValid(direct) && !IsTaskArea(direct))
            {
                currentTarget = direct;
                return;
            }
        }

        // 2. Nearby search. Prefer real small objects over task/area colliders.
        int count = Physics.OverlapSphereNonAlloc(origin, interactionRange, nearbyColliders,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

        Interactable best = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider col = nearbyColliders[i];
            if (col == null) continue;

            Interactable candidate = col.GetComponent<Interactable>();
            if (candidate == null) candidate = col.GetComponentInParent<Interactable>();
            if (!IsValid(candidate)) continue;

            Vector3 point = col.ClosestPoint(origin);
            Vector3 toTarget = point - origin;
            float distance = Mathf.Max(0.05f, toTarget.magnitude);
            float angle = Vector3.Angle(forward, toTarget.normalized);
            if (angle > focusAngle) continue;

            float score = angle * 0.65f + distance * 1.8f;
            if (IsTaskArea(candidate)) score += 1000f;

            // Visible waste pieces and animal interactables get a strong preference.
            if (IsWasteTarget(candidate)) score -= 500f;
            if (candidate is AnimalController) score -= 50f;

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        currentTarget = best;
    }

    private static bool IsValid(Interactable target)
    {
        return target != null && target.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(target.Prompt);
    }

    private static bool IsTaskArea(Interactable target)
    {
        return target != null && target.GetType().Name.Equals("ConservationTaskPoint", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWasteTarget(Interactable target)
    {
        if (target == null) return false;
        string typeName = target.GetType().Name;
        string objectName = target.gameObject.name;
        return typeName.Equals("WastePieceInteractable", System.StringComparison.OrdinalIgnoreCase) ||
               Contains(objectName, "bottle") || Contains(objectName, "can") ||
               Contains(objectName, "wrapper") || Contains(objectName, "litter");
    }

    private static bool Contains(string value, string search)
    {
        return !string.IsNullOrEmpty(value) && value.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
