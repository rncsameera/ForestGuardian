using UnityEngine;
using UnityEngine.UI;

/// <summary>Minimal open-world HUD inspired by modern third-person games: objective, contextual action and world status.</summary>
public sealed class GameplayHUD : MonoBehaviour
{
    public static GameplayHUD Instance { get; private set; }

    private Text objectiveText, promptText, healthText, messageText, locationText, compassText, stepText;
    private GameObject inspectionPanel;
    private Text inspectionText;
    private float messageUntil;
    private Canvas canvas;
    private static readonly string[] Directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
        if (GetComponent<GameFlowUI>() == null) gameObject.AddComponent<GameFlowUI>();
        if (GetComponent<PythonAIBridge>() == null) gameObject.AddComponent<PythonAIBridge>();
    }

    private void Update()
    {
        ConservationQuestSystem quests = ConservationQuestSystem.Instance;
        RestorePondQuest quest = quests != null ? quests.CurrentQuest as RestorePondQuest : null;
        if (objectiveText != null)
        {
            string title = quests != null && quests.CurrentQuest != null ? quests.CurrentQuest.Title : "Explore the Forest";
            string objective = quests != null && quests.CurrentQuest != null ? quests.CurrentQuest.CurrentObjective : "Explore the forest and discover what is happening.";
            objectiveText.text = title.ToUpperInvariant() + "\n<size=20>" + objective + "</size>";
            if (stepText != null && quest != null) stepText.text = GetQuestProgress(quest);
        }

        UpdateCompass();

        EcosystemState ecosystem = EcosystemState.Instance;
        if (healthText != null)
            healthText.text = ecosystem != null ? $"ECOSYSTEM  {ecosystem.OverallHealth:0}%" : "ECOSYSTEM  —";

        if (messageText != null && Time.unscaledTime > messageUntil)
            messageText.text = string.Empty;
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("Gameplay HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Compact objective card, deliberately much smaller than the previous debug-style block.
        GameObject objectiveCard = MakePanel(canvasObject.transform, "Objective Card", new Vector2(620f, 122f), new Vector2(34f, -34f), new Vector2(0f, 1f), new Color(0.02f, 0.03f, 0.025f, 0.78f));
        objectiveText = MakeText(objectiveCard.transform, "Objective", font, 26, TextAnchor.UpperLeft,
            new Vector2(18f, -13f), new Vector2(580f, 94f), new Vector2(0f, 1f));
        objectiveText.color = new Color(0.96f, 0.92f, 0.78f);
        objectiveText.supportRichText = true;

        GameObject compassCard = MakePanel(canvasObject.transform, "Compass Card", new Vector2(560f, 52f), new Vector2(0f, -22f), new Vector2(0.5f, 1f), new Color(0.015f, 0.02f, 0.018f, 0.58f));
        compassText = MakeText(compassCard.transform, "Compass", font, 24, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(540f, 46f), new Vector2(0.5f, 0.5f));
        compassText.color = Color.white; compassText.supportRichText = true;

        locationText = MakeText(canvasObject.transform, "Location", font, 16, TextAnchor.MiddleCenter,
            new Vector2(0f, -78f), new Vector2(520f, 24f), new Vector2(0.5f, 1f));
        locationText.text = "FOREST WILDLANDS"; locationText.color = new Color(1f, 1f, 1f, 0.78f);

        stepText = MakeText(canvasObject.transform, "Quest Progress", font, 17, TextAnchor.UpperLeft,
            new Vector2(52f, -151f), new Vector2(540f, 28f), new Vector2(0f, 1f));
        stepText.color = new Color(0.86f, 0.92f, 0.82f, 0.82f);

        GameObject promptCard = MakePanel(canvasObject.transform, "Interaction Card", new Vector2(560f, 72f), new Vector2(0f, 82f), new Vector2(0.5f, 0f), new Color(0.015f, 0.02f, 0.018f, 0.9f));
        promptText = MakeText(promptCard.transform, "Interaction Prompt", font, 24, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(540f, 66f), new Vector2(0.5f, 0.5f));
        promptText.color = Color.white;

        GameObject statusCard = MakePanel(canvasObject.transform, "Status Card", new Vector2(250f, 54f), new Vector2(-28f, 28f), new Vector2(1f, 0f), new Color(0.02f, 0.03f, 0.025f, 0.72f));
        healthText = MakeText(statusCard.transform, "Ecosystem", font, 19, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(230f, 48f), new Vector2(0.5f, 0.5f));
        healthText.color = new Color(0.78f, 0.92f, 0.72f);

        messageText = MakeText(canvasObject.transform, "Feedback", font, 22, TextAnchor.MiddleCenter,
            new Vector2(0f, 156f), new Vector2(1100f, 58f), new Vector2(0.5f, 0f));
        messageText.color = new Color(1f, 0.95f, 0.82f);

        inspectionPanel = MakePanel(canvasObject.transform, "Inspection Panel", new Vector2(460f, 270f), new Vector2(-26f, 0f), new Vector2(1f, 0.5f), new Color(0.025f, 0.035f, 0.03f, 0.92f));
        inspectionText = MakeText(inspectionPanel.transform, "Inspection", font, 20, TextAnchor.UpperLeft,
            new Vector2(18f, -16f), new Vector2(420f, 235f), new Vector2(0f, 1f));
        inspectionPanel.SetActive(false);
    }

    private static GameObject MakePanel(Transform parent, string name, Vector2 size, Vector2 position, Vector2 anchor, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    private static Text MakeText(Transform parent, string name, Font font, int size, TextAnchor alignment,
        Vector2 position, Vector2 dimensions, Vector2 anchor)
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
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    public void SetInteractionPrompt(string prompt)
    {
        if (promptText == null) return;
        promptText.text = prompt;
        promptText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(prompt));
    }

    public void ShowInspection(string name, string status, string description, string problem)
    {
        if (inspectionPanel == null) return;
        inspectionPanel.SetActive(true);
        inspectionText.text = name + "\n<size=16>" + status + "</size>\n\n" + description +
            (string.IsNullOrWhiteSpace(problem) ? string.Empty : "\n\n<color=#e9c46a>" + problem + "</color>");
        inspectionText.supportRichText = true;
    }

    public void ShowMessage(string message, float seconds = 4f)
    {
        if (messageText == null) return;
        messageText.text = message;
        messageUntil = Time.unscaledTime + seconds;
    }

    private void UpdateCompass()
    {
        if (compassText == null) return;
        Transform player = GameObject.Find("Guardian")?.transform;
        if (player == null) return;
        int index = Mathf.RoundToInt(player.eulerAngles.y / 45f) & 7;
        string[] strip = new string[5];
        for (int i = -2; i <= 2; i++) strip[i + 2] = Directions[(index + i + 8) & 7];
        compassText.text = $"{strip[0]}    {strip[1]}    <b><size=28>{strip[2]}</size></b>    {strip[3]}    {strip[4]}";
    }

    private string GetQuestProgress(RestorePondQuest quest)
    {
        return quest.Step switch
        {
            0 => "DAY 1  •  EXPLORE",
            1 => "STEP 2/8  •  EXAMINE TRACKS  [E]",
            2 => "STEP 3/8  •  FIND THE INJURED DEER",
            3 => "STEP 4/8  •  TREAT THE DEER  [E]",
            4 => "STEP 5/8  •  FIND THE RANGER OUTPOST",
            5 => "STEP 6/8  •  SEARCH THE OUTPOST  [E]",
            6 => "STEP 7/8  •  TAKE THE WATER CACHE  [E]",
            7 => "STEP 8/8  •  WATER THE SAPLINGS  [E]",
            _ => "STORY COMPLETE"
        };
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
}
