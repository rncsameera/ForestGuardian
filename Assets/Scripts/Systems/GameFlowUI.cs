using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Small in-game front end for the existing playable forest scene.</summary>
public sealed class GameFlowUI : MonoBehaviour
{
    [Serializable] private sealed class SaveData
    {
        public Vector3 position;
        public float yaw;
        public QuestSnapshot quest;
        public EcosystemSnapshot ecosystem;
        public int day, hour, minute, weather;
    }

    private GameObject overlay, card;
    private Text heading, body;
    private Transform player;
    private bool active = true;
    private string savePath;
    private float sensitivity = 0.12f;
    private float volume = 1f;
    private bool completionShown;
    public static GameFlowUI Instance { get; private set; }
    public static bool UIOwnsCursor { get; private set; }
    public static bool ExternalPanelOpen { get; private set; }

    private void Awake()
    {
        Instance = this;
        savePath = Path.Combine(Application.persistentDataPath, "forestguardian-save.json");
        player = GameObject.Find("Guardian")?.transform;
        Build();
        ShowMain();
    }

    private void Update()
    {
        RestorePondQuest quest = ConservationQuestSystem.Instance?.CurrentQuest as RestorePondQuest;
        if (!completionShown && quest != null && quest.IsComplete) { completionShown = true; ShowCompletion(quest); return; }
        if (Keyboard.current == null) return;
        if (ExternalPanelOpen) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (active) Resume(); else ShowPause();
        }
        if (!active && Keyboard.current.jKey.wasPressedThisFrame) ShowJournal();
    }

    private void Build()
    {
        EventSystem[] eventSystems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        EventSystem eventSystem = null;
        foreach (EventSystem candidate in eventSystems)
            if (candidate.isActiveAndEnabled) { eventSystem = candidate; break; }
        if (eventSystem == null && eventSystems.Length > 0) eventSystem = eventSystems[0];
        foreach (EventSystem candidate in eventSystems)
            if (candidate != eventSystem) candidate.enabled = false;
        if (eventSystem == null)
        {
            GameObject events = new GameObject("Game UI Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem = events.GetComponent<EventSystem>();
        }
        eventSystem.gameObject.SetActive(true);
        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        bool createdModule = inputModule == null;
        if (createdModule) inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        inputModule.enabled = true;
        foreach (BaseInputModule module in eventSystem.GetComponents<BaseInputModule>())
            if (module != inputModule) module.enabled = false;
        if (createdModule) inputModule.AssignDefaultActions();
        GameObject canvasGo = new GameObject("Game Flow UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 60;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        overlay = new GameObject("Flow Overlay", typeof(Image)); overlay.transform.SetParent(canvasGo.transform, false);
        RectTransform full = overlay.GetComponent<RectTransform>(); full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one; full.offsetMin = full.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0.025f, 0.055f, 0.045f, 0.86f);
        card = new GameObject("Menu Card", typeof(Image)); card.transform.SetParent(overlay.transform, false);
        RectTransform r = card.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.pivot = new Vector2(.5f, .5f); r.sizeDelta = new Vector2(760, 700);
        card.GetComponent<Image>().color = new Color(.07f, .12f, .095f, .98f);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        heading = TextAt(card.transform, font, 34, new Vector2(0, -34), new Vector2(700, 70), TextAnchor.MiddleCenter);
        body = TextAt(card.transform, font, 21, new Vector2(0, -120), new Vector2(660, 180), TextAnchor.UpperCenter);
    }

    private static Text TextAt(Transform parent, Font font, int size, Vector2 pos, Vector2 dimensions, TextAnchor align)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = pos; rect.sizeDelta = dimensions;
        Text t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.color = new Color(.95f, .93f, .83f); t.alignment = align; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false; return t;
    }

    private void ClearButtons()
    {
        for (int i = card.transform.childCount - 1; i >= 0; i--)
            if (card.transform.GetChild(i).GetComponent<Button>() != null) Destroy(card.transform.GetChild(i).gameObject);
    }
    private void ButtonAt(string label, int index, Action action)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(card.transform, false);
        RectTransform r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(.5f, 1); r.pivot = new Vector2(.5f, 1); r.anchoredPosition = new Vector2(0, -330 - index * 60); r.sizeDelta = new Vector2(420, 48);
        go.GetComponent<Image>().color = new Color(.2f, .34f, .24f); go.GetComponent<Button>().onClick.AddListener(() => action());
        Text t = TextAt(go.transform, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 20, Vector2.zero, new Vector2(400, 45), TextAnchor.MiddleCenter); t.text = label;
    }
    private void Set(string title, string message, bool pauseGame)
    {
        UIOwnsCursor = pauseGame;
        ExternalPanelOpen = false;
        heading.text = title; body.text = message; ClearButtons(); overlay.SetActive(true); active = pauseGame;
        Time.timeScale = pauseGame ? 0 : 1; SetPlayerEnabled(!pauseGame);
        Cursor.visible = pauseGame; Cursor.lockState = pauseGame ? CursorLockMode.None : CursorLockMode.Locked;
    }
    private void SetPlayerEnabled(bool enabled)
    {
        if (player == null) player = GameObject.Find("Guardian")?.transform;
        if (player == null) return;
        foreach (Behaviour b in player.GetComponents<Behaviour>())
            if (b is GuardianController || b is InteractionSystem || b is GuardianCameraController || b is GuardianCameraMode) b.enabled = enabled;
        GuardianCameraController cameraControl = FindAnyObjectByType<GuardianCameraController>();
        if (cameraControl != null) cameraControl.enabled = enabled;
        GuardianCameraMode cameraMode = FindAnyObjectByType<GuardianCameraMode>();
        if (cameraMode != null) cameraMode.enabled = enabled;
    }
    private void ShowMain()
    {
        Set("FOREST GUARDIAN", "Open-world wildlife adventure\n\nExplore freely. Discover what is happening. Your choices change the forest.\n\nWASD move  •  Shift run  •  E interact  •  M map  •  J journal  •  Esc pause", true);
        ButtonAt("New Game", 0, StartNew); if (File.Exists(savePath)) ButtonAt("Continue", 1, LoadGame); ButtonAt("Settings", 2, ShowSettings); ButtonAt("Credits", 3, ShowCredits);
    }
    private void StartNew()
    {
        ForestEnvironmentGenerator env = FindAnyObjectByType<ForestEnvironmentGenerator>();
        if (player == null) player = GameObject.Find("Guardian")?.transform;
        if (env != null && player != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
            player.position = new Vector3(env.HouseCenter.x, env.SampleSurfaceHeight(env.HouseCenter.x, env.HouseCenter.y) + 1f, env.HouseCenter.y);
            if (cc != null) cc.enabled = true;
        }
        Resume(); GameplayHUD.Instance?.ShowMessage("DAY 1  •  The forest is unusually quiet. Leave home and follow the main trail.", 7f);
    }
    private void Resume() { overlay.SetActive(false); active = false; ExternalPanelOpen = false; UIOwnsCursor = false; Time.timeScale = 1; SetPlayerEnabled(true); Cursor.visible = false; Cursor.lockState = CursorLockMode.Locked; }

    public static void SetExternalPanel(bool open)
    {
        if (Instance == null) return;
        ExternalPanelOpen = open;
        UIOwnsCursor = open || Instance.active;
        GuardianCameraController cameraControl = FindAnyObjectByType<GuardianCameraController>();
        if (cameraControl != null) cameraControl.enabled = !UIOwnsCursor;
        GuardianCameraMode cameraMode = FindAnyObjectByType<GuardianCameraMode>();
        if (cameraMode != null) cameraMode.enabled = !UIOwnsCursor;
        Cursor.visible = UIOwnsCursor;
        Cursor.lockState = UIOwnsCursor ? CursorLockMode.None : CursorLockMode.Locked;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null; UIOwnsCursor = false; ExternalPanelOpen = false;
    }
    private void ShowPause()
    {
        Set("PAUSED", "Take a moment to review your field notes or save your progress.", true);
        ButtonAt("Resume", 0, Resume); ButtonAt("Journal", 1, ShowJournal); ButtonAt("Save", 2, SaveGame); ButtonAt("Settings", 3, ShowSettings); ButtonAt("Main Menu", 4, ShowMain);
    }
    private void ShowJournal()
    {
        RestorePondQuest q = ConservationQuestSystem.Instance?.CurrentQuest as RestorePondQuest;
        string objective = q != null ? q.CurrentObjective : "Leave home and follow the current objective.";
        int step = q != null ? q.Step : 0;
        string notes = "FIELD JOURNAL\n\nCURRENT STORY  •  THE QUIET FOREST\n" + objective + "\n\nLOCATIONS\nHome — starting base\nNorth Ridge — tracks and wildlife signs\nWildlife Meadow — injured animal encounter\nRanger Outpost — abandoned field station\n\nDISCOVERIES\n" + (step > 1 ? "Fresh tracks led downhill toward an injured animal.\nThe old ranger station may explain what changed in this area." : "Follow the trail and record what you discover.\nThe map marks your current objective.") + "\n\nTOOLS  •  Field kit: investigate   Supplies: wildlife care\nMap: M   Close journal: Esc";
        Set("FIELD JOURNAL", notes, true); ButtonAt("Return to Game", 0, Resume);
    }
    private void ShowSettings()
    {
        Set("SETTINGS", $"Master volume: {volume:0.0}\nLook sensitivity: {sensitivity:0.00}\nDisplay: {(Screen.fullScreen ? "Fullscreen" : "Windowed")}", true);
        ButtonAt("Volume −", 0, () => { volume = Mathf.Clamp01(volume - .1f); AudioListener.volume = volume; ShowSettings(); });
        ButtonAt("Volume +", 1, () => { volume = Mathf.Clamp01(volume + .1f); AudioListener.volume = volume; ShowSettings(); });
        ButtonAt("Sensitivity −", 2, () => { sensitivity = Mathf.Clamp(sensitivity - .02f, .02f, .5f); ApplySensitivity(); ShowSettings(); });
        ButtonAt("Sensitivity +", 3, () => { sensitivity = Mathf.Clamp(sensitivity + .02f, .02f, .5f); ApplySensitivity(); ShowSettings(); });
        ButtonAt("Toggle Fullscreen", 4, () => { Screen.fullScreen = !Screen.fullScreen; ShowSettings(); }); ButtonAt("Back", 5, ShowPause);
    }
    private void ApplySensitivity() { if (player == null) player = GameObject.Find("Guardian")?.transform; if (player != null) player.GetComponent<GuardianController>()?.SetLookSensitivity(sensitivity); }
    private void ShowCredits() { Set("CREDITS", "ForestGuardian\nA field adventure about noticing what a living ecosystem needs.\n\nExplore • Investigate • Restore • Observe", true); ButtonAt("Back", 0, ShowMain); }
    private void ShowCompletion(RestorePondQuest quest)
    {
        float health = EcosystemState.Instance != null ? EcosystemState.Instance.OverallHealth : 0f;
        Set("THE FOREST REMEMBERS", $"Your first field investigation is complete.\n\nYou followed the tracks, helped wildlife, searched the ranger outpost and restored young trees.\n\nEcosystem health: {health:0}%\n\nNew regions, wildlife events and restoration opportunities are now opening across the reserve.", true);
        ButtonAt("Main Menu", 0, ShowMain);
    }
    private void SaveGame()
    {
        if (player == null) player = GameObject.Find("Guardian")?.transform;
        WorldTimeManager clock = WorldTimeManager.Instance;
        SaveData data = new SaveData { position = player != null ? player.position : Vector3.zero, yaw = player != null ? player.eulerAngles.y : 0,
            quest = ConservationQuestSystem.Instance != null ? ConservationQuestSystem.Instance.CaptureSnapshot() : default,
            ecosystem = EcosystemState.Instance != null ? EcosystemState.Instance.CaptureSnapshot() : default,
            day = clock != null ? clock.DayNumber : 0, hour = clock != null ? Mathf.FloorToInt(clock.Hour) : 8, minute = clock != null ? Mathf.FloorToInt((clock.Hour % 1) * 60) : 0,
            weather = WeatherManager.Instance != null ? (int)WeatherManager.Instance.CurrentWeather : 0 };
        File.WriteAllText(savePath, JsonUtility.ToJson(data)); GameplayHUD.Instance?.ShowMessage("Field notes saved."); ShowPause();
    }
    private void LoadGame()
    {
        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(savePath));
            if (player == null) player = GameObject.Find("Guardian")?.transform;
            if (player != null) { CharacterController cc = player.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false; player.SetPositionAndRotation(data.position, Quaternion.Euler(0, data.yaw, 0)); if (cc != null) cc.enabled = true; }
            ConservationQuestSystem.Instance?.ApplySnapshot(data.quest); EcosystemState.Instance?.ApplySnapshot(data.ecosystem); WorldTimeManager.Instance?.SetTime(data.day, data.hour, data.minute);
            WeatherManager.Instance?.SetWeather((ForestWeather)Mathf.Clamp(data.weather, 0, Enum.GetValues(typeof(ForestWeather)).Length - 1)); Resume();
        }
        catch (Exception e) { Debug.LogWarning("Could not load ForestGuardian save: " + e.Message); ShowMain(); }
    }
}
