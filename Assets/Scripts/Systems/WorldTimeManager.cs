using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Owns the lightweight world clock, sun arc and night sky visuals.</summary>
public sealed class WorldTimeManager : MonoBehaviour
{
    [Header("Clock")]
    [SerializeField, Range(120f, 300f)] private float dayDurationMinutes = 180f;
    [SerializeField, Min(0f)] private float timeScale = 1f;
    [SerializeField, Range(0f, 24f)] private float startingHour = 8f;
    [SerializeField] private bool advanceTime = true;
    [SerializeField, Range(0f, 24f)] private float dawnStartHour = 4.5f;
    [SerializeField, Range(0f, 24f)] private float sunriseEndHour = 7.5f;
    [SerializeField, Range(0f, 24f)] private float sunsetStartHour = 16.5f;
    [SerializeField, Range(0f, 24f)] private float nightStartHour = 20.5f;

    [Header("Sun and atmosphere")]
    [SerializeField] private Light sun;
    [SerializeField, Min(0f)] private float daytimeSunIntensity = 2f;
    [SerializeField, Min(0f)] private float nightMoonlightIntensity = 0.10f;
    [SerializeField] private Color dayAmbient = new Color(0.57f, 0.65f, 0.59f);
    [SerializeField] private Color nightAmbient = new Color(0.14f, 0.20f, 0.34f);

    [Header("Night sky")]
    [SerializeField, Range(32, 600)] private int starCount = 280;
    [SerializeField] private float skyRadius = 260f;
    [SerializeField] private float lunarCycleDays = 29.53f;

    public static WorldTimeManager Instance { get; private set; }
    public float Hour => minuteOfDay / 60f;
    public float MinuteOfDay => minuteOfDay;
    public int DayNumber => dayNumber;
    public bool IsNight => NightFactor >= 0.85f;
    public float NightFactor { get; private set; }
    public float DaylightFactor { get; private set; }
    public float EveningFactor { get; private set; }
    public float MorningFactor { get; private set; }
    public float DayDurationMinutes => activeDayDurationMinutes;
    public float TimeScale => timeScale;
    public bool IsAdvancing => advanceTime;
    public string MoonPhaseName => GetMoonPhaseName();
    public event Action<int> DayChanged;

    private float minuteOfDay;
    private int dayNumber;
    private float activeDayDurationMinutes;
    private float skyRefreshTimer;
    private float lastMoonTextureAngle = -10f;
    private Transform skyRoot;
    private Transform starRoot;
    private Transform moonRoot;
    private MeshRenderer starRenderer;
    private MeshRenderer moonRenderer;
    private Material starMaterial;
    private Material moonMaterial;
    private Texture2D moonTexture;
    private Camera targetCamera;
    private Light originalSun;
    private Quaternion originalSunRotation;
    private float originalSunIntensity;
    private Color originalSunColor;
    private AmbientMode originalAmbientMode;
    private Color originalAmbientSky;
    private Color originalAmbientEquator;
    private Color originalAmbientGround;
    private bool originalFog;
    private FogMode originalFogMode;
    private Color originalFogColor;
    private float originalFogDensity;
    private bool capturedRenderSettings;
    private float weatherVisualFactor;

    public float GetDayLengthMinutes() => activeDayDurationMinutes;
    public void SetWeatherVisualFactor(float amount) => weatherVisualFactor = Mathf.Clamp01(amount);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        activeDayDurationMinutes = Mathf.Max(1f, dayDurationMinutes);
        minuteOfDay = Mathf.Repeat(startingHour, 24f) * 60f;
        if (sun == null)
        {
            Light[] lights = FindObjectsByType<Light>();
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].type == LightType.Directional) { sun = lights[i]; break; }
        }
        if (sun != null)
        {
            originalSun = sun;
            originalSunRotation = sun.transform.rotation;
            originalSunIntensity = sun.intensity;
            originalSunColor = sun.color;
        }

        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbientSky = RenderSettings.ambientSkyColor;
        originalAmbientEquator = RenderSettings.ambientEquatorColor;
        originalAmbientGround = RenderSettings.ambientGroundColor;
        originalFog = RenderSettings.fog;
        originalFogMode = RenderSettings.fogMode;
        originalFogColor = RenderSettings.fogColor;
        originalFogDensity = RenderSettings.fogDensity;
        capturedRenderSettings = true;
        targetCamera = Camera.main;
        BuildNightSky();
        ApplyLightingAndSun();
    }

    private void Update()
    {
        if (advanceTime && activeDayDurationMinutes > 0f)
        {
            float previousDay = dayNumber;
            minuteOfDay += Time.deltaTime * timeScale * 1440f / (activeDayDurationMinutes * 60f);
            if (minuteOfDay >= 1440f)
            {
                dayNumber += Mathf.FloorToInt(minuteOfDay / 1440f);
                minuteOfDay = Mathf.Repeat(minuteOfDay, 1440f);
                if (dayNumber != previousDay)
                    DayChanged?.Invoke(dayNumber);
            }
        }

        ApplyLightingAndSun();
        UpdateNightSky();
    }

    /// <summary>Sets the clock for testing or a future UI without changing its rate.</summary>
    public void SetTime(int day, int hour, int minute)
    {
        dayNumber = Mathf.Max(0, day);
        minuteOfDay = Mathf.Clamp(hour, 0, 23) * 60f + Mathf.Clamp(minute, 0, 59);
        ApplyLightingAndSun();
    }

    public void SetTimeAdvancing(bool enabled) => advanceTime = enabled;

    private void ApplyLightingAndSun()
    {
        float hour = Hour;
        MorningFactor = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dawnStartHour, sunriseEndHour, hour));
        float duskProgress = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sunsetStartHour, nightStartHour, hour));
        DaylightFactor = Mathf.Clamp01(Mathf.Min(MorningFactor, 1f - duskProgress));
        if (hour < dawnStartHour)
            EveningFactor = 1f;
        else if (hour < sunriseEndHour)
            EveningFactor = 1f - MorningFactor;
        else if (hour < sunsetStartHour)
            EveningFactor = 0f;
        else
            EveningFactor = duskProgress;

        float solarAngle = (hour - 6f) / 24f * Mathf.PI * 2f;
        float elevation = Mathf.Sin(solarAngle) * 74f;
        // The sun moves from the eastern horizon, over the forest, to the west.
        float azimuth = 90f + (hour - 6f) / 24f * 360f;
        Vector3 sunToGround = -new Vector3(Mathf.Cos(elevation * Mathf.Deg2Rad) * Mathf.Sin(azimuth * Mathf.Deg2Rad),
            Mathf.Sin(elevation * Mathf.Deg2Rad), Mathf.Cos(elevation * Mathf.Deg2Rad) * Mathf.Cos(azimuth * Mathf.Deg2Rad));
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.LookRotation(sunToGround.normalized, Vector3.up);
            float sunWeight = DaylightFactor;
            sun.intensity = Mathf.Lerp(nightMoonlightIntensity, daytimeSunIntensity, sunWeight);
            Color naturalSun = Color.Lerp(new Color(0.43f, 0.56f, 0.88f), new Color(1f, 0.91f, 0.76f), sunWeight);
            float goldenHour = Mathf.Max(1f - Mathf.Abs(hour - 6f) / 1.5f, 1f - Mathf.Abs(hour - 18f) / 1.75f);
            sun.color = Color.Lerp(naturalSun, new Color(1f, 0.62f, 0.38f), Mathf.Clamp01(goldenHour) * 0.55f);
        }

        NightFactor = 1f - DaylightFactor;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        Color ambientDay = Color.Lerp(dayAmbient, new Color(0.46f, 0.54f, 0.62f), weatherVisualFactor);
        RenderSettings.ambientSkyColor = Color.Lerp(nightAmbient, ambientDay, DaylightFactor);
        RenderSettings.ambientEquatorColor = Color.Lerp(nightAmbient * 1.18f, ambientDay * 0.88f, DaylightFactor);
        RenderSettings.ambientGroundColor = Color.Lerp(nightAmbient * 0.72f, dayAmbient * 0.60f, DaylightFactor);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = Mathf.Lerp(0.0021f, 0.0011f, DaylightFactor) + weatherVisualFactor * 0.00055f;
        RenderSettings.fogColor = Color.Lerp(new Color(0.10f, 0.16f, 0.29f), Color.Lerp(new Color(0.53f, 0.64f, 0.68f), new Color(0.43f, 0.51f, 0.58f), weatherVisualFactor), DaylightFactor);

        Material skybox = RenderSettings.skybox;
        if (skybox != null)
        {
            Color daySky = Color.Lerp(new Color(0.58f, 0.72f, 0.84f), new Color(0.47f, 0.55f, 0.67f), weatherVisualFactor);
            Color skyTint = Color.Lerp(new Color(0.24f, 0.31f, 0.55f), daySky, DaylightFactor);
            float twilightTint = Mathf.Clamp01(1f - DaylightFactor) * Mathf.Clamp01(1f - NightFactor * 0.35f);
            skyTint = Color.Lerp(skyTint, new Color(0.36f, 0.32f, 0.56f), twilightTint * 0.35f);
            if (skybox.HasProperty("_SkyTint")) skybox.SetColor("_SkyTint", skyTint);
            if (skybox.HasProperty("_GroundColor")) skybox.SetColor("_GroundColor", Color.Lerp(new Color(0.12f, 0.18f, 0.30f), new Color(0.42f, 0.50f, 0.43f), DaylightFactor));
            if (skybox.HasProperty("_Exposure")) skybox.SetFloat("_Exposure", Mathf.Lerp(0.48f, 0.85f, DaylightFactor));
        }
    }

    private void BuildNightSky()
    {
        skyRoot = new GameObject("World Sky Details").transform;
        skyRoot.SetParent(transform, false);
        starRoot = new GameObject("Stars").transform;
        starRoot.SetParent(skyRoot, false);
        starRenderer = starRoot.gameObject.AddComponent<MeshRenderer>();
        starRoot.gameObject.AddComponent<MeshFilter>().sharedMesh = BuildStarMesh();
        starMaterial = CreateUnlitMaterial("World Stars", new Color(0.78f, 0.86f, 1f, 0.88f));
        ConfigureTransparentMaterial(starMaterial);
        starRenderer.sharedMaterial = starMaterial;
        starRenderer.shadowCastingMode = ShadowCastingMode.Off;
        starRenderer.receiveShadows = false;
        starRenderer.lightProbeUsage = LightProbeUsage.Off;
        starRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        moonRoot = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
        moonRoot.name = "Moon";
        moonRoot.SetParent(skyRoot, false);
        Collider collider = moonRoot.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        moonRenderer = moonRoot.GetComponent<MeshRenderer>();
        moonRenderer.shadowCastingMode = ShadowCastingMode.Off;
        moonRenderer.receiveShadows = false;
        moonMaterial = CreateUnlitMaterial("Stylized Moon Phases", Color.white);
        if (moonMaterial.HasProperty("_Surface")) moonMaterial.SetFloat("_Surface", 1f);
        if (moonMaterial.HasProperty("_SrcBlend")) moonMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (moonMaterial.HasProperty("_DstBlend")) moonMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (moonMaterial.HasProperty("_ZWrite")) moonMaterial.SetFloat("_ZWrite", 0f);
        ConfigureTransparentMaterial(moonMaterial);
        moonTexture = new Texture2D(96, 96, TextureFormat.RGBA32, false, true) { name = "Runtime Moon Phase" };
        moonTexture.wrapMode = TextureWrapMode.Clamp;
        moonMaterial.mainTexture = moonTexture;
        moonRenderer.sharedMaterial = moonMaterial;
    }

    private Mesh BuildStarMesh()
    {
        System.Random random = new System.Random(71593);
        Vector3[] vertices = new Vector3[starCount * 4];
        int[] triangles = new int[starCount * 6];
        Vector2[] uv = new Vector2[vertices.Length];
        for (int i = 0; i < starCount; i++)
        {
            float azimuth = (float)random.NextDouble() * Mathf.PI * 2f;
            float elevation = Mathf.Lerp(0.06f, 1.52f, (float)random.NextDouble());
            Vector3 direction = new Vector3(Mathf.Cos(azimuth) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Sin(azimuth) * Mathf.Cos(elevation));
            Vector3 center = direction * skyRadius;
            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            Vector3 up = Vector3.Cross(direction, right).normalized;
            float size = Mathf.Lerp(0.16f, 0.42f, (float)random.NextDouble());
            int v = i * 4;
            vertices[v] = center - right * size - up * size;
            vertices[v + 1] = center - right * size + up * size;
            vertices[v + 2] = center + right * size + up * size;
            vertices[v + 3] = center + right * size - up * size;
            uv[v] = new Vector2(0, 0); uv[v + 1] = new Vector2(0, 1); uv[v + 2] = new Vector2(1, 1); uv[v + 3] = new Vector2(1, 0);
            int t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        Mesh mesh = new Mesh { name = "Combined Night Sky Stars" };
        mesh.vertices = vertices; mesh.triangles = triangles; mesh.uv = uv; mesh.RecalculateBounds();
        return mesh;
    }

    private void UpdateNightSky()
    {
        skyRefreshTimer -= Time.deltaTime;
        if (skyRefreshTimer > 0f) return;
        skyRefreshTimer = 0.2f;
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;
        skyRoot.position = targetCamera.transform.position;
        if (starRenderer != null)
        {
            starRenderer.enabled = true;
            starMaterial.color = new Color(0.78f, 0.86f, 1f, 0.88f * NightFactor);
        }

        float hour = Hour;
        float lunarProgress = Mathf.Repeat((dayNumber + minuteOfDay / 1440f) / Mathf.Max(1f, lunarCycleDays), 1f);
        float lunarAngle = Mathf.PI + lunarProgress * Mathf.PI * 2f;
        if (Mathf.Abs(Mathf.DeltaAngle(lastMoonTextureAngle * Mathf.Rad2Deg, lunarAngle * Mathf.Rad2Deg)) > 0.2f)
        {
            UpdateMoonTexture(lunarAngle);
            lastMoonTextureAngle = lunarAngle;
        }
        float moonElevation = 62f * Mathf.Sin((hour - 18f) / 12f * Mathf.PI);
        float moonAzimuth = 90f + (hour - 6f) / 24f * 360f + lunarAngle * Mathf.Rad2Deg;
        float el = moonElevation * Mathf.Deg2Rad;
        float az = moonAzimuth * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az));
        moonRoot.position = targetCamera.transform.position + direction * (skyRadius * 0.88f);
        moonRoot.rotation = Quaternion.LookRotation(-direction, Vector3.up);
        moonRoot.localScale = Vector3.one * 9f;
        moonRenderer.enabled = NightFactor > 0.005f && moonElevation > -4f;
        float illumination = (1f - Mathf.Cos(lunarAngle)) * 0.5f;
        Color moonTint = Color.Lerp(new Color(0.36f, 0.44f, 0.67f), new Color(0.84f, 0.88f, 1f), illumination);
        moonTint.a = NightFactor;
        moonMaterial.color = moonTint;
    }

    private void UpdateMoonTexture(float phaseAngle)
    {
        Color[] pixels = new Color[96 * 96];
        Vector3 lightDirection = new Vector3(Mathf.Sin(phaseAngle), 0f, Mathf.Cos(phaseAngle));
        for (int y = 0; y < 96; y++)
        for (int x = 0; x < 96; x++)
        {
            float nx = ((x + 0.5f) / 96f) * 2f - 1f;
            float ny = ((y + 0.5f) / 96f) * 2f - 1f;
            float r2 = nx * nx + ny * ny;
            if (r2 > 0.96f) { pixels[y * 96 + x] = Color.clear; continue; }
            float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - r2));
            float lit = Mathf.Clamp01((nx * lightDirection.x + nz * lightDirection.z + 0.03f) * 2.2f);
            pixels[y * 96 + x] = new Color(Mathf.Lerp(0.10f, 1f, lit), Mathf.Lerp(0.16f, 1f, lit), Mathf.Lerp(0.28f, 1f, lit), 1f);
        }
        moonTexture.SetPixels(pixels);
        moonTexture.Apply(false, false);
    }

    private string GetMoonPhaseName()
    {
        float phase = Mathf.Repeat((dayNumber + minuteOfDay / 1440f) / Mathf.Max(1f, lunarCycleDays), 1f);
        if (phase < 0.04f || phase > 0.96f) return "New Moon";
        if (phase < 0.21f) return "Waxing Crescent";
        if (phase < 0.29f) return "First Quarter";
        if (phase < 0.46f) return "Waxing Gibbous";
        if (phase < 0.54f) return "Full Moon";
        if (phase < 0.71f) return "Waning Gibbous";
        if (phase < 0.79f) return "Third Quarter";
        return "Waning Crescent";
    }

    private static Material CreateUnlitMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material material = new Material(shader) { name = materialName, color = color };
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (originalSun != null)
        {
            originalSun.transform.rotation = originalSunRotation;
            originalSun.intensity = originalSunIntensity;
            originalSun.color = originalSunColor;
        }
        if (capturedRenderSettings)
        {
            RenderSettings.ambientMode = originalAmbientMode;
            RenderSettings.ambientSkyColor = originalAmbientSky;
            RenderSettings.ambientEquatorColor = originalAmbientEquator;
            RenderSettings.ambientGroundColor = originalAmbientGround;
            RenderSettings.fog = originalFog;
            RenderSettings.fogMode = originalFogMode;
            RenderSettings.fogColor = originalFogColor;
            RenderSettings.fogDensity = originalFogDensity;
        }
        if (starMaterial != null) Destroy(starMaterial);
        if (moonMaterial != null) Destroy(moonMaterial);
        if (moonTexture != null) Destroy(moonTexture);
    }
}
