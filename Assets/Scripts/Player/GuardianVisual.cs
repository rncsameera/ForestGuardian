using UnityEngine;

public class GuardianVisual : MonoBehaviour
{
    [Header("Character Colors")]
    public Color skinColor = new Color(0.82f, 0.62f, 0.45f);
    public Color shirtColor = new Color(0.18f, 0.42f, 0.25f);
    public Color pantsColor = new Color(0.12f, 0.16f, 0.20f);
    public Color hairColor = new Color(0.08f, 0.05f, 0.03f);

    private Transform visualRoot;
    private Transform leftArmPivot;
    private Transform rightArmPivot;
    private Transform leftLegPivot;
    private Transform rightLegPivot;
    private Transform body;
    private CharacterController characterController;
    private GuardianController guardianController;
    private Vector3 visualRootBasePosition;
    private Vector3 bodyBaseScale;
    private float activity;
    private float gaitPhase;

    private void Start()
    {
        BuildCharacter();
    }

    private void LateUpdate()
    {
        if (visualRoot == null || characterController == null || leftArmPivot == null || rightArmPivot == null || leftLegPivot == null || rightLegPivot == null)
            return;

        Vector3 velocity = characterController.velocity;
        velocity.y = 0f;
        float targetActivity = Mathf.Clamp01(velocity.magnitude / Mathf.Max(guardianController != null ? guardianController.WalkSpeed : 4.5f, 0.1f));
        activity = Mathf.MoveTowards(activity, targetActivity, Time.deltaTime * 4f);

        float sprintThreshold = guardianController != null
            ? (guardianController.WalkSpeed + guardianController.SprintSpeed) * 0.5f
            : 9f;
        bool sprinting = velocity.magnitude > sprintThreshold;
        float cycleRate = sprinting ? 12f : 8f;
        gaitPhase += Time.deltaTime * cycleRate * Mathf.Lerp(0.18f, 1f, activity);

        float swing = Mathf.Sin(gaitPhase) * activity;
        float armAngle = Mathf.Lerp(31f, 52f, sprinting ? 1f : 0f) * swing;
        float legAngle = Mathf.Lerp(39f, 61f, sprinting ? 1f : 0f) * swing;
        leftArmPivot.localRotation = Quaternion.Euler(armAngle, 0f, 0f);
        rightArmPivot.localRotation = Quaternion.Euler(-armAngle, 0f, 0f);
        leftLegPivot.localRotation = Quaternion.Euler(-legAngle, 0f, 0f);
        rightLegPivot.localRotation = Quaternion.Euler(legAngle, 0f, 0f);

        float bob = activity > 0.01f
            ? Mathf.Sin(gaitPhase * 2f) * Mathf.Lerp(0.018f, 0.045f, sprinting ? 1f : 0f) * activity
            : Mathf.Sin(Time.time * 1.8f) * 0.006f;
        visualRoot.localPosition = visualRootBasePosition + Vector3.up * bob;
        if (body != null)
        {
            Vector3 scale = bodyBaseScale;
            scale.y *= 1f + (activity > 0.01f ? Mathf.Sin(gaitPhase * 2f) * 0.012f * activity : Mathf.Sin(Time.time * 1.8f) * 0.006f);
            body.localScale = scale;
        }
    }

    private void BuildCharacter()
    {
        // Hide the original capsule renderer.
        MeshRenderer oldRenderer = GetComponent<MeshRenderer>();
        if (oldRenderer != null)
            oldRenderer.enabled = false;

        // Keep the scene's GuardianVisual child and populate it only once.
        visualRoot = transform.Find("GuardianVisual");
        if (visualRoot == null)
        {
            GameObject root = new GameObject("GuardianVisual");
            visualRoot = root.transform;
            visualRoot.SetParent(transform, false);
        }

        if (visualRoot.childCount > 0)
        {
            BindAnimationParts();
            return;
        }

        // Body
        body = CreatePrimitive(
            PrimitiveType.Capsule,
            "Body",
            visualRoot,
            new Vector3(0f, 1.05f, 0f),
            new Vector3(0.65f, 0.75f, 0.42f),
            shirtColor
        ).transform;

        // Head
        CreatePrimitive(
            PrimitiveType.Sphere,
            "Head",
            visualRoot,
            new Vector3(0f, 2.05f, 0f),
            new Vector3(0.48f, 0.48f, 0.48f),
            skinColor
        );

        // Hair
        CreatePrimitive(
            PrimitiveType.Sphere,
            "Hair",
            visualRoot,
            new Vector3(0f, 2.27f, -0.02f),
            new Vector3(0.50f, 0.25f, 0.50f),
            hairColor
        );

        leftArmPivot = CreateLimbPivot("LeftArmPivot", visualRoot, new Vector3(-0.48f, 1.65f, 0f), "LeftArm", new Vector3(0f, -0.55f, 0f), new Vector3(0.16f, 0.55f, 0.16f), shirtColor);
        rightArmPivot = CreateLimbPivot("RightArmPivot", visualRoot, new Vector3(0.48f, 1.65f, 0f), "RightArm", new Vector3(0f, -0.55f, 0f), new Vector3(0.16f, 0.55f, 0.16f), shirtColor);
        leftLegPivot = CreateLimbPivot("LeftLegPivot", visualRoot, new Vector3(-0.20f, 0.92f, 0f), "LeftLeg", new Vector3(0f, -0.46f, 0f), new Vector3(0.18f, 0.46f, 0.18f), pantsColor);
        rightLegPivot = CreateLimbPivot("RightLegPivot", visualRoot, new Vector3(0.20f, 0.92f, 0f), "RightLeg", new Vector3(0f, -0.46f, 0f), new Vector3(0.18f, 0.46f, 0.18f), pantsColor);
        characterController = GetComponent<CharacterController>();
        guardianController = GetComponent<GuardianController>();
        visualRootBasePosition = visualRoot.localPosition;
        bodyBaseScale = body != null ? body.localScale : Vector3.one;
    }

    private Transform CreateLimbPivot(string pivotName, Transform parent, Vector3 pivotPosition, string meshName, Vector3 meshPosition, Vector3 meshScale, Color color)
    {
        GameObject pivotObject = new GameObject(pivotName);
        pivotObject.transform.SetParent(parent, false);
        pivotObject.transform.localPosition = pivotPosition;
        CreatePrimitive(PrimitiveType.Capsule, meshName, pivotObject.transform, meshPosition, meshScale, color);
        return pivotObject.transform;
    }

    private void BindAnimationParts()
    {
        leftArmPivot = visualRoot.Find("LeftArmPivot");
        rightArmPivot = visualRoot.Find("RightArmPivot");
        leftLegPivot = visualRoot.Find("LeftLegPivot");
        rightLegPivot = visualRoot.Find("RightLegPivot");
        body = visualRoot.Find("Body");
        characterController = GetComponent<CharacterController>();
        guardianController = GetComponent<GuardianController>();
        visualRootBasePosition = visualRoot.localPosition;
        bodyBaseScale = body != null ? body.localScale : Vector3.one;
    }

    private GameObject CreatePrimitive(
        PrimitiveType type,
        string objectName,
        Transform parent,
        Vector3 position,
        Vector3 scale,
        Color color)
    {
        GameObject obj = GameObject.CreatePrimitive(type);

        obj.name = objectName;
        obj.transform.SetParent(parent);
        obj.transform.localPosition = position;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = scale;

        // Visual only — the Guardian's main Character Controller handles collision.
        Collider collider = obj.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = obj.GetComponent<Renderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
        {
            Debug.LogError("No supported shader was found for GuardianVisual.", this);
            Destroy(obj);
            return null;
        }

        Material material = new Material(shader);
        material.color = color;
        renderer.material = material;

        return obj;
    }
}
