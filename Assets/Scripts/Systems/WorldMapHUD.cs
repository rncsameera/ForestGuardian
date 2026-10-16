using System.Collections.Generic;

using UnityEngine;

using UnityEngine.InputSystem;

using UnityEngine.UI;

/// <summary>

/// GTA-style open-world map HUD: a readable north-up minimap lives in the top-right,

/// with a clear pink objective marker showing the exact current target. M expands it.

/// </summary>

public sealed class WorldMapHUD : MonoBehaviour

{

    [SerializeField] private ForestEnvironmentGenerator environment;

    [SerializeField] private Transform player;

    private RectTransform mapRect;

    private RectTransform playerMarker;

    private RectTransform objectiveMarker;

    private Text objectiveLabel;

    private Text titleText;

    private Text legendText;

    private GameObject panel;

    private bool mapOpen;

    private bool worldBuilt;

    private readonly List<RectTransform> locationMarkers = new List<RectTransform>();

    private readonly List<Text> locationLabels = new List<Text>();

    private readonly List<AnimalMovement> wildlifeAnimals = new List<AnimalMovement>();

    private readonly List<RectTransform> wildlifeMarkers = new List<RectTransform>();

    private static readonly Color PanelColor = new Color(0.025f, 0.045f, 0.035f, 0.94f);

    private static readonly Color PlotColor = new Color(0.17f, 0.27f, 0.20f, 0.97f);

    private static readonly Color TrailColor = new Color(0.66f, 0.52f, 0.34f, 0.95f);

    private static readonly Color PondColor = new Color(0.30f, 0.72f, 0.88f, 1f);

    private static readonly Color PinkObjective = new Color(1f, 0.16f, 0.56f, 1f);

    private static readonly Color ShelterColor = new Color(1f, 0.76f, 0.28f, 1f);

    private void Awake()

    {

        if (environment == null) environment = FindAnyObjectByType<ForestEnvironmentGenerator>();

        if (player == null) player = GameObject.Find("Guardian")?.transform;

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

        yield return null;

        BuildWorldMarkers();

    }

    private void Update()

    {

        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)

        {

            if (GameFlowUI.UIOwnsCursor && !mapOpen) return;

            mapOpen = !mapOpen;

            ApplyMapMode();

            GameFlowUI.SetExternalPanel(mapOpen);

        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && mapOpen)

        {

            mapOpen = false;

            ApplyMapMode();

            GameFlowUI.SetExternalPanel(false);

        }

        if (player == null) player = GameObject.Find("Guardian")?.transform;

        if (environment == null || mapRect == null || player == null) return;

        if (!worldBuilt) BuildWorldMarkers();

        // North-up map: the map itself never rotates. The compass tells the player heading.

        if (playerMarker != null)

        {

            playerMarker.anchoredPosition = WorldToMap(new Vector2(player.position.x, player.position.z));

            playerMarker.localRotation = Quaternion.Euler(0f, 0f, 45f);

        }

        UpdateObjectiveMarker();

        UpdateWildlifeMarkers();

    }

    private void BuildUI()

    {

        GameObject canvasObject = new GameObject("World Map HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 25;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        panel = new GameObject("Forest Map Panel", typeof(RectTransform), typeof(Image));

        panel.transform.SetParent(canvasObject.transform, false);

        Image panelImage = panel.GetComponent<Image>();

        panelImage.color = PanelColor;

        panelImage.raycastTarget = false;

        mapRect = CreateRect(panel.transform, "Map Plot", new Vector2(350f, 350f), Vector2.zero, new Vector2(0.5f, 0.5f));

        Image plot = mapRect.gameObject.AddComponent<Image>();

        plot.color = PlotColor;

        plot.raycastTarget = false;

        titleText = MakeText(panel.transform, "Map Title", font, 20, TextAnchor.MiddleLeft,

            new Vector2(18f, -12f), new Vector2(320f, 34f), new Vector2(0f, 1f));

        titleText.color = Color.white;

        titleText.text = "MAP  •  M EXPAND";

        legendText = MakeText(panel.transform, "Map Legend", font, 15, TextAnchor.MiddleLeft,

            new Vector2(18f, 12f), new Vector2(340f, 28f), new Vector2(0f, 0f));

        legendText.color = new Color(1f, 0.86f, 0.92f, 1f);

        legendText.text = "◆ PINK = CURRENT OBJECTIVE";

        panel.SetActive(true);

        ApplyMapMode();

    }

    private void ApplyMapMode()

    {

        if (panel == null || mapRect == null) return;

        RectTransform panelRect = panel.GetComponent<RectTransform>();

        if (mapOpen)

        {

            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);

            panelRect.pivot = new Vector2(0.5f, 0.5f);

            panelRect.anchoredPosition = Vector2.zero;

            panelRect.sizeDelta = new Vector2(760f, 660f);

            mapRect.sizeDelta = new Vector2(640f, 540f);

            titleText.fontSize = 22;

            legendText.fontSize = 13;

            titleText.text = "FOREST MAP  •  M / ESC CLOSE";

            legendText.text = "◆ PINK = CURRENT OBJECTIVE     ● YOU     ■ SHELTER     ● WATER";

        }

        else

        {

            panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 1f);

            panelRect.pivot = new Vector2(1f, 1f);

            panelRect.anchoredPosition = new Vector2(-28f, -28f);

            panelRect.sizeDelta = new Vector2(340f, 340f);

            mapRect.sizeDelta = new Vector2(280f, 280f);

            titleText.fontSize = 16;

            legendText.fontSize = 10;

            titleText.text = "MAP  •  M EXPAND";

            legendText.text = "◆ PINK = CURRENT OBJECTIVE";

        }

        RepositionMapChildren();
        UpdateLabelVisibility();

    }

    private void UpdateLabelVisibility()
    {
        // Compact mode keeps only the active objective label visible.
        foreach (Text label in locationLabels)
        {
            if (label != null)
                label.gameObject.SetActive(mapOpen);
        }

        if (objectiveLabel != null)
            objectiveLabel.gameObject.SetActive(true);
    }


    private void RepositionMapChildren()

    {

        if (mapRect == null) return;

        // Recalculate all world markers against the new plot dimensions.

        foreach (RectTransform marker in locationMarkers)

            if (marker != null) marker.anchoredPosition = MarkerWorldPosition(marker.name);

        UpdateAllLocationPositions();

    }

    private void BuildWorldMarkers()

    {

        if (environment == null || mapRect == null) return;

        for (int i = mapRect.childCount - 1; i>= 0; i--)

        {

            Transform child = mapRect.GetChild(i);

            if (child == playerMarker || child == objectiveMarker) continue;

            Destroy(child.gameObject);

        }

        locationMarkers.Clear();

        locationLabels.Clear();

        wildlifeAnimals.Clear();

        wildlifeMarkers.Clear();

        AddTrail(environment.MainTrailPoints, TrailColor, 4.5f);

        if (environment.SecondaryTrailPoints != null)

            for (int i = 0; i < environment.SecondaryTrailPoints.Count; i++)

                AddTrail(environment.SecondaryTrailPoints[i], new Color(0.58f, 0.45f, 0.30f, 0.8f), 2.5f);

        for (int i = 0; i < environment.PondCount; i++)

        {

            Vector2 pond = environment.GetPondCenter(i);

            RectTransform marker = AddMarker("Pond_" + i, pond, PondColor, 18f, false);

            locationMarkers.Add(marker);

            Text label = MakeText(mapRect, "PondLabel_" + i, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 13,

                TextAnchor.MiddleLeft, WorldToMap(pond) + new Vector2(13f, 0f), new Vector2(125f, 22f), new Vector2(0.5f, 0.5f));

            label.text = i == 0 ? "EASTERN POND" : (i == 1 ? "WESTWOOD POND" : "POND " + (i + 1));

            label.color = Color.white;

            locationLabels.Add(label);

        }

        RectTransform shelter = AddMarker("Shelter", environment.HouseCenter, ShelterColor, 18f, false);

        locationMarkers.Add(shelter);

        Text shelterLabel = MakeText(mapRect, "ShelterLabel", Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 13,

            TextAnchor.MiddleLeft, WorldToMap(environment.HouseCenter) + new Vector2(13f, 0f), new Vector2(100f, 22f), new Vector2(0.5f, 0.5f));

        shelterLabel.text = "HOME";

        shelterLabel.color = Color.white;

        locationLabels.Add(shelterLabel);

        AddNamedLandmark("Ridge", "NORTH RIDGE", new Vector2(0f, 333f), new Color(0.86f, 0.78f, 0.55f, 1f));

        AddNamedLandmark("Outpost", "RANGER OUTPOST", new Vector2(-94f, 242f), new Color(0.78f, 0.68f, 0.48f, 1f));

        AddNamedLandmark("Meadow", "WILDLIFE MEADOW", new Vector2(-74f, 337f), new Color(0.55f, 0.86f, 0.48f, 1f));

        playerMarker = AddMarker("Player", player != null ? new Vector2(player.position.x, player.position.z) : environment.HouseCenter, Color.white, 16f, true);

        objectiveMarker = AddMarker("Objective", environment.HouseCenter, PinkObjective, 22f, false);

        objectiveMarker.localRotation = Quaternion.Euler(0f, 0f, 45f);

        objectiveLabel = MakeText(mapRect, "ObjectiveLabel", Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 13,

            TextAnchor.MiddleLeft, Vector2.zero, new Vector2(230f, 30f), new Vector2(0.5f, 0.5f));

        objectiveLabel.color = new Color(1f, 0.78f, 0.90f, 1f);

        objectiveLabel.fontStyle = FontStyle.Bold;

        worldBuilt = true;

        UpdateAllLocationPositions();

    }

    private void AddNamedLandmark(string key, string label, Vector2 world, Color color)

    {

        RectTransform marker = AddMarker(key, world, color, 15f, false);

        locationMarkers.Add(marker);

        Text text = MakeText(mapRect, key + "Label", Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 13,

            TextAnchor.MiddleLeft, WorldToMap(world) + new Vector2(12f, 0f), new Vector2(130f, 22f), new Vector2(0.5f, 0.5f));

        text.text = label;

        text.color = Color.white;

        locationLabels.Add(text);

    }

    private void UpdateAllLocationPositions()
    {
        for (int i = 0; i < locationMarkers.Count; i++)
        {
            RectTransform marker = locationMarkers[i];
            if (marker == null) continue;

            if (marker.name.StartsWith("Pond_"))
            {
                int index;
                if (!int.TryParse(marker.name.Substring(5), out index)) continue;
                marker.anchoredPosition = WorldToMap(environment.GetPondCenter(index));

                Text pondLabel = mapRect.Find("PondLabel_" + index)?.GetComponent<Text>();
                if (pondLabel != null)
                {
                    float yOffset = (index % 2 == 0) ? 12f : -12f;
                    pondLabel.rectTransform.anchoredPosition = marker.anchoredPosition + new Vector2(12f, yOffset);
                }
            }
            else if (marker.name == "Shelter")
            {
                marker.anchoredPosition = WorldToMap(environment.HouseCenter);
                Text label = mapRect.Find("ShelterLabel")?.GetComponent<Text>();
                if (label != null) label.rectTransform.anchoredPosition = marker.anchoredPosition + new Vector2(12f, -16f);
            }
            else if (marker.name == "Ridge")
            {
                marker.anchoredPosition = WorldToMap(new Vector2(0f, 333f));
                Text label = mapRect.Find("RidgeLabel")?.GetComponent<Text>();
                if (label != null) label.rectTransform.anchoredPosition = marker.anchoredPosition + new Vector2(12f, 20f);
            }
            else if (marker.name == "Outpost")
            {
                marker.anchoredPosition = WorldToMap(new Vector2(-94f, 242f));
                Text label = mapRect.Find("OutpostLabel")?.GetComponent<Text>();
                if (label != null) label.rectTransform.anchoredPosition = marker.anchoredPosition + new Vector2(12f, 0f);
            }
            else if (marker.name == "Meadow")
            {
                marker.anchoredPosition = WorldToMap(new Vector2(-74f, 337f));
                Text label = mapRect.Find("MeadowLabel")?.GetComponent<Text>();
                if (label != null) label.rectTransform.anchoredPosition = marker.anchoredPosition + new Vector2(12f, -22f);
            }
        }
    }


    private void UpdateObjectiveMarker()

    {

        if (objectiveMarker == null || environment == null) return;

        Vector2 target = GetObjectiveWorldPosition(out string label);

        objectiveMarker.anchoredPosition = WorldToMap(target);

        objectiveMarker.localRotation = Quaternion.Euler(0f, 0f, 45f);

        if (objectiveLabel != null)

        {

            objectiveLabel.text = label;

            objectiveLabel.rectTransform.anchoredPosition = objectiveMarker.anchoredPosition + new Vector2(18f, 18f);

        }

    }

    private Vector2 GetObjectiveWorldPosition(out string label)

    {

        label = "OBJECTIVE";

        RestorePondQuest quest = ConservationQuestSystem.Instance?.CurrentQuest as RestorePondQuest;

        if (quest == null) return environment.HouseCenter;

        label = quest.Step switch

        {

            0 => "NORTH RIDGE",

            1 => "EXAMINE TRACKS",

            2 => "INJURED DEER",

            3 => "HELP THE DEER",

            4 => "RANGER OUTPOST",

            5 => "SEARCH OUTPOST",

            6 => "WATER CACHE",

            7 => "DRY SAPLINGS",

            _ => "HOME"

        };

        return ConservationQuestSystem.GetAdventurePosition(quest.Step);

    }

    private void UpdateWildlifeMarkers()

    {

        int count = Mathf.Min(wildlifeAnimals.Count, wildlifeMarkers.Count);

        for (int i = 0; i < count; i++)

        {

            if (wildlifeAnimals[i] == null || wildlifeMarkers[i] == null) continue;

            wildlifeMarkers[i].anchoredPosition = WorldToMap(new Vector2(wildlifeAnimals[i].transform.position.x, wildlifeAnimals[i].transform.position.z));

        }

    }

    private void AddTrail(IReadOnlyList<Vector2> points, Color color, float width)

    {

        if (points == null || points.Count < 2) return;

        for (int i = 0; i < points.Count - 1; i++)

        {

            Vector2 a = WorldToMap(points[i]);

            Vector2 b = WorldToMap(points[i + 1]);

            Vector2 delta = b - a;

            RectTransform segment = CreateRect(mapRect, "Trail", new Vector2(delta.magnitude, width), (a + b) * 0.5f, new Vector2(0.5f, 0.5f));

            segment.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            Image image = segment.gameObject.AddComponent<Image>();

            image.color = color;

            image.raycastTarget = false;

        }

    }

    private RectTransform AddMarker(string name, Vector2 world, Color color, float size, bool playerIcon)

    {

        RectTransform marker = CreateRect(mapRect, name, new Vector2(size, size), WorldToMap(world), new Vector2(0.5f, 0.5f));

        Image image = marker.gameObject.AddComponent<Image>();

        image.color = color;

        image.raycastTarget = false;

        if (playerIcon) marker.localRotation = Quaternion.Euler(0f, 0f, 45f);

        return marker;

    }

    private Vector2 MarkerWorldPosition(string markerName)

    {

        if (markerName == "Shelter") return WorldToMap(environment.HouseCenter);

        if (markerName.StartsWith("Pond_"))

        {

            int index = int.Parse(markerName.Substring(5));

            return WorldToMap(environment.GetPondCenter(index));

        }

        return Vector2.zero;

    }

    private Vector2 WorldToMap(Vector2 world)

    {

        if (environment == null || mapRect == null) return Vector2.zero;

        float x = (world.x + environment.WorldWidth * 0.5f) / environment.WorldWidth;

        float y = (world.y - environment.WorldStartZ) / environment.WorldDepth;

        return new Vector2((x - 0.5f) * mapRect.rect.width, (y - 0.5f) * mapRect.rect.height);

    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 size, Vector2 position, Vector2 anchor)

    {

        GameObject go = new GameObject(name, typeof(RectTransform));

        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();

        rect.anchorMin = rect.anchorMax = anchor;

        rect.pivot = new Vector2(0.5f, 0.5f);

        rect.sizeDelta = size;

        rect.anchoredPosition = position;

        return rect;

    }

    private static Text MakeText(Transform parent, string name, Font font, int size, TextAnchor alignment, Vector2 position, Vector2 dimensions, Vector2 anchor)

    {

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));

        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();

        rect.anchorMin = rect.anchorMax = anchor;

        rect.pivot = anchor;

        rect.anchoredPosition = position;

        rect.sizeDelta = dimensions;

        Text text = go.GetComponent<Text>();

        text.font = font;

        text.fontSize = size;

        text.color = Color.white;

        text.alignment = alignment;

        text.raycastTarget = false;

        text.horizontalOverflow = HorizontalWrapMode.Overflow;

        text.verticalOverflow = VerticalWrapMode.Overflow;

        return text;

    }

}
