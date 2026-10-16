using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Always-visible compact world minimap. The map rotates with the player like a modern open-world HUD.</summary>
public sealed class MiniMapHUD : MonoBehaviour
{
    private ForestEnvironmentGenerator environment;
    private Transform player;
    private RectTransform worldLayer;
    private RectTransform playerMarker, objectiveMarker;
    private readonly List<RectTransform> staticMarkers = new List<RectTransform>();
    private GameObject root;
    private const float MapSize = 270f;
    private const float WorldScale = 0.55f;

    private void Awake()
    {
        environment = FindAnyObjectByType<ForestEnvironmentGenerator>();
        player = GameObject.Find("Guardian")?.transform;
        BuildUI();
    }

    private System.Collections.IEnumerator Start()
    {
        while (environment == null)
        {
            environment = FindAnyObjectByType<ForestEnvironmentGenerator>();
            yield return null;
        }
        if (player == null) player = GameObject.Find("Guardian")?.transform;
        BuildWorld();
    }

    private void Update()
    {
        if (player == null) player = GameObject.Find("Guardian")?.transform;
        if (player == null || worldLayer == null) return;

        worldLayer.anchoredPosition = -WorldToMap(player.position);
        worldLayer.localRotation = Quaternion.Euler(0f, 0f, player.eulerAngles.y);
        if (playerMarker != null) playerMarker.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
        UpdateObjectiveMarker();
    }

    private void UpdateObjectiveMarker()
    {
        if (objectiveMarker == null || environment == null) return;
        RestorePondQuest quest = ConservationQuestSystem.Instance?.CurrentQuest as RestorePondQuest;
        Vector2 target = environment.HouseCenter;
        if (quest != null && quest.Step <= 3) target = environment.GetPondCenter(0);
        else if (quest != null && quest.Step >= 4) target = environment.PondCount > 1 ? environment.GetPondCenter(1) : environment.HouseCenter;
        objectiveMarker.anchoredPosition = WorldToMap(new Vector3(target.x, 0f, target.y));
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("Mini Map HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 24;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        root = new GameObject("Minimap", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvasObject.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0f, 0f);
        rootRect.pivot = new Vector2(0f, 0f);
        rootRect.anchoredPosition = new Vector2(24f, 24f);
        rootRect.sizeDelta = new Vector2(MapSize, MapSize);
        root.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.02f, 0.88f);
        root.GetComponent<Image>().raycastTarget = false;

        GameObject plot = new GameObject("Map Plot", typeof(RectTransform), typeof(Image));
        plot.transform.SetParent(root.transform, false);
        RectTransform plotRect = plot.GetComponent<RectTransform>();
        plotRect.anchorMin = plotRect.anchorMax = new Vector2(0.5f, 0.5f);
        plotRect.pivot = new Vector2(0.5f, 0.5f);
        plotRect.anchoredPosition = Vector2.zero;
        plotRect.sizeDelta = new Vector2(MapSize - 8f, MapSize - 8f);
        plot.GetComponent<Image>().color = new Color(0.15f, 0.24f, 0.18f, 0.94f);
        plot.GetComponent<Image>().raycastTarget = false;
        worldLayer = new GameObject("World Layer", typeof(RectTransform)).GetComponent<RectTransform>();
        worldLayer.SetParent(plot.transform, false);
        worldLayer.anchorMin = worldLayer.anchorMax = new Vector2(0.5f, 0.5f);
        worldLayer.pivot = new Vector2(0.5f, 0.5f);
        worldLayer.sizeDelta = new Vector2(Mathf.Max(600f, environment != null ? environment.WorldWidth * WorldScale : 600f), Mathf.Max(600f, environment != null ? environment.WorldDepth * WorldScale : 600f));

        playerMarker = Marker(worldLayer, Color.white, 13f, true);
        objectiveMarker = Marker(worldLayer, new Color(1f, 0.78f, 0.25f), 12f, false);
        Text n = TextAt(root.transform, "N", 13, new Vector2(0f, -7f), new Vector2(24f, 20f));
        n.color = new Color(1f, 0.95f, 0.8f, 0.9f);
    }

    private void BuildWorld()
    {
        if (environment == null || worldLayer == null) return;
        foreach (Transform child in worldLayer) if (child != playerMarker && child != objectiveMarker) Destroy(child.gameObject);
        staticMarkers.Clear();

        if (environment.MainTrailPoints != null)
            for (int i = 0; i < environment.MainTrailPoints.Count - 1; i++)
                Trail(environment.MainTrailPoints[i], environment.MainTrailPoints[i + 1]);

        for (int i = 0; i < environment.PondCount; i++)
            staticMarkers.Add(Marker(worldLayer, i == 0 ? new Color(0.25f, 0.72f, 0.9f) : new Color(0.22f, 0.55f, 0.72f), i == 0 ? 10f : 7f, false, environment.GetPondCenter(i)));
        staticMarkers.Add(Marker(worldLayer, new Color(0.98f, 0.72f, 0.28f), 10f, false, environment.HouseCenter));
    }

    private void Trail(Vector2 a, Vector2 b)
    {
        Vector2 pa = WorldToMap(new Vector3(a.x, 0f, a.y));
        Vector2 pb = WorldToMap(new Vector3(b.x, 0f, b.y));
        Vector2 delta = pb - pa;
        RectTransform segment = new GameObject("Trail", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        segment.SetParent(worldLayer, false);
        segment.anchorMin = segment.anchorMax = new Vector2(0.5f, 0.5f);
        segment.pivot = new Vector2(0.5f, 0.5f);
        segment.anchoredPosition = (pa + pb) * 0.5f;
        segment.sizeDelta = new Vector2(delta.magnitude, 2.5f);
        segment.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        Image image = segment.GetComponent<Image>();
        image.color = new Color(0.67f, 0.52f, 0.32f, 0.75f);
        image.raycastTarget = false;
    }

    private RectTransform Marker(Transform parent, Color color, float size, bool isPlayer, Vector2 world = default)
    {
        return Marker(parent, color, size, isPlayer, new Vector3(world.x, 0f, world.y));
    }

    private RectTransform Marker(Transform parent, Color color, float size, bool isPlayer, Vector3 world)
    {
        GameObject go = new GameObject(isPlayer ? "Player" : "Location", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = WorldToMap(world);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (isPlayer) rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        return rect;
    }

    private Vector2 WorldToMap(Vector3 world)
    {
        float x = (world.x + environment.WorldWidth * 0.5f) * WorldScale;
        float y = (world.z - environment.WorldStartZ) * WorldScale;
        return new Vector2(x, y);
    }

    private static Text TextAt(Transform parent, string value, int size, Vector2 pos, Vector2 dimensions)
    {
        GameObject go = new GameObject(value, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = dimensions;
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }
}
