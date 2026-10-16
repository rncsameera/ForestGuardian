using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum ForestWeather
{
    Clear,
    Cloudy,
    Rain
}

/// <summary>Changes ambient weather gradually and owns one lightweight local rain emitter.</summary>
public sealed class WeatherManager : MonoBehaviour
{
    [SerializeField] private ForestWeather startingWeather = ForestWeather.Clear;
    [SerializeField, Min(1f)] private float weatherChangeEveryGameMinutes = 180f;
    [SerializeField, Min(1f)] private float transitionGameMinutes = 20f;
    [SerializeField, Range(0f, 1f)] private float rainChance = 0.28f;
    [SerializeField] private Light sun;

    public static WeatherManager Instance { get; private set; }
    public ForestWeather CurrentWeather { get; private set; }
    public bool IsRaining => CurrentWeather == ForestWeather.Rain;
    public bool IsCloudy => CurrentWeather != ForestWeather.Clear;
    public float MovementMultiplier => CurrentWeather == ForestWeather.Rain ? 0.78f : 1f;
    public event System.Action<ForestWeather> WeatherChanged;

    private ParticleSystem rain;
    private Material rainMaterial;
    private float weatherClock;
    private float weatherBlend;
    private float rainBlend;
    private Transform player;
    private Transform cloudRoot;
    private readonly List<Transform> cloudInstances = new List<Transform>();
    private readonly List<Vector3> cloudBaseScales = new List<Vector3>();
    private bool cloudsCached;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        CurrentWeather = startingWeather;
        if (sun == null)
        {
            Light[] lights = FindObjectsByType<Light>();
            for (int i = 0; i < lights.Length; i++) if (lights[i].type == LightType.Directional) { sun = lights[i]; break; }
        }
        GameObject guardian = GameObject.Find("Guardian");
        if (guardian != null) player = guardian.transform;
        BuildRainEmitter();
        CacheCloudsIfReady();
        ApplyWeatherImmediately();
    }

    private void Update()
    {
        WorldTimeManager clock = WorldTimeManager.Instance;
        float gameMinutesThisFrame = 0f;
        if (clock != null && clock.IsAdvancing)
        {
            gameMinutesThisFrame = Time.deltaTime * clock.TimeScale * 1440f / (Mathf.Max(1f, clock.GetDayLengthMinutes()) * 60f);
            weatherClock += gameMinutesThisFrame;
        }
        if (weatherClock >= weatherChangeEveryGameMinutes)
        {
            weatherClock = Mathf.Repeat(weatherClock, weatherChangeEveryGameMinutes);
            ChooseNextWeather();
        }

        float target = IsRaining ? 1f : IsCloudy ? 0.52f : 0f;
        float transitionStep = gameMinutesThisFrame / Mathf.Max(1f, transitionGameMinutes);
        weatherBlend = Mathf.MoveTowards(weatherBlend, target, transitionStep);
        rainBlend = Mathf.MoveTowards(rainBlend, IsRaining ? 1f : 0f, transitionStep);
        ApplyWeatherBlend();
        CacheCloudsIfReady();
        for (int i = 0; i < cloudInstances.Count; i++)
            if (cloudInstances[i] != null)
                cloudInstances[i].localScale = cloudBaseScales[i] * (1f + weatherBlend * 0.22f);
        if (rain != null && player != null)
        {
            Vector3 p = player.position;
            rain.transform.position = new Vector3(p.x, p.y + 13f, p.z);
        }
    }

    private void CacheCloudsIfReady()
    {
        if (cloudsCached) return;
        GameObject clouds = GameObject.Find("Clouds");
        if (clouds == null) return;
        cloudRoot = clouds.transform;
        for (int i = 0; i < cloudRoot.childCount; i++)
        {
            Transform cloud = cloudRoot.GetChild(i);
            cloudInstances.Add(cloud);
            cloudBaseScales.Add(cloud.localScale);
        }
        cloudsCached = true;
    }

    public void SetWeather(ForestWeather weather)
    {
        // Keep requests on a gradual clear/cloud/rain path.
        if (CurrentWeather == ForestWeather.Clear && weather == ForestWeather.Rain)
            weather = ForestWeather.Cloudy;
        else if (CurrentWeather == ForestWeather.Rain && weather == ForestWeather.Clear)
            weather = ForestWeather.Cloudy;
        if (CurrentWeather == weather) return;
        CurrentWeather = weather;
        WeatherChanged?.Invoke(CurrentWeather);
    }

    private void ChooseNextWeather()
    {
        float roll = Random.value;
        if (CurrentWeather == ForestWeather.Clear)
            SetWeather(roll < 0.68f ? ForestWeather.Cloudy : ForestWeather.Clear);
        else if (CurrentWeather == ForestWeather.Cloudy)
        {
            if (roll < rainChance) SetWeather(ForestWeather.Rain);
            else if (roll < rainChance + 0.35f) SetWeather(ForestWeather.Clear);
        }
        else
            SetWeather(ForestWeather.Cloudy);
    }

    private void BuildRainEmitter()
    {
        GameObject rainObject = new GameObject("Rain Weather Particles");
        rainObject.transform.SetParent(transform, false);
        rain = rainObject.AddComponent<ParticleSystem>();
        var main = rain.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.15f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
        main.startColor = new Color(0.62f, 0.76f, 0.9f, 0.62f);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 900;
        var emission = rain.emission;
        emission.rateOverTime = 0f;
        var shape = rain.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(32f, 0.1f, 32f);
        var velocity = rain.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = -22f;
        var renderer = rain.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 3.2f;
        renderer.velocityScale = 0.08f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        rainMaterial = new Material(shader) { name = "Soft Rain Streaks", color = new Color(0.58f, 0.72f, 0.86f, 0.48f) };
        renderer.sharedMaterial = rainMaterial;
        if (rainMaterial.HasProperty("_Surface")) rainMaterial.SetFloat("_Surface", 1f);
        if (rainMaterial.HasProperty("_SrcBlend")) rainMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (rainMaterial.HasProperty("_DstBlend")) rainMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (rainMaterial.HasProperty("_ZWrite")) rainMaterial.SetFloat("_ZWrite", 0f);
        rainMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        rainMaterial.renderQueue = (int)RenderQueue.Transparent;
    }

    private void ApplyWeatherImmediately()
    {
        if (rain == null) return;
        rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ApplyWeatherBlend()
    {
        WorldTimeManager clock = WorldTimeManager.Instance;
        if (clock != null) clock.SetWeatherVisualFactor(weatherBlend);
        if (rain != null)
        {
            var emission = rain.emission;
            emission.rateOverTime = 560f * rainBlend;
            if (rainBlend > 0.005f && !rain.isPlaying) rain.Play();
            if (rainBlend <= 0.005f && rain.isPlaying) rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (rainMaterial != null) Destroy(rainMaterial);
    }
}
