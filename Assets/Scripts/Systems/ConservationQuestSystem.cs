using System;
using UnityEngine;

public enum QuestSignal
{
    PondInspected, WaterInvestigated, ResourceCollected, PondRestored, DebrisRemoved, HabitatRestored,
    WildlifeChecked, DeepAreaLocated, DeepAreaInvestigated, DeepRestorationAction, RecoveryObserved,
    RidgeReached, RidgeInvestigated, AnimalFound, OutpostFound, OutpostInvestigated, WaterCacheCollected,
    SaplingsWatered
}

[Serializable]
public struct QuestSnapshot { public string Id; public int Step; public int ResourceCount; public int HabitatCount; public bool Complete; }

public interface IConservationQuest
{
    string Id { get; }
    string Title { get; }
    string Description { get; }
    string CurrentObjective { get; }
    bool IsComplete { get; }
    bool Apply(QuestSignal signal);
    QuestSnapshot CaptureSnapshot();
    void ApplySnapshot(QuestSnapshot snapshot);
}

/// <summary>Opening adventure: exploration, investigation, wildlife care and restoration.
/// Ponds are one part of the world, not the entire game.</summary>
public sealed class RestorePondQuest : IConservationQuest
{
    public const int RequiredDebris = 5;
    public const int RequiredHerbs = RequiredDebris;
    private int step;
    private bool complete;

    public string Id => "forest_opening";
    public string Title => "THE QUIET FOREST";
    public string Description => "Explore the reserve, follow what you discover, and help the forest without being told where every answer is.";
    public bool IsComplete => complete;
    public int Step => step;
    public int HerbCount => 0;
    public int DebrisCount => 0;
    public int HabitatCount => 0;
    public int RestorationPercent => step * 100 / 8;

    public string CurrentObjective => step switch
    {
        0 => "Leave home and follow the main trail toward the North Ridge.",
        1 => "Investigate the tracks at the North Ridge. Look closely and press E.",
        2 => "Follow the tracks downhill. Something injured is hiding in the meadow.",
        3 => "Find the injured deer and give it the care it needs. Press E when you are close.",
        4 => "Continue through the meadow and find the abandoned ranger outpost.",
        5 => "Search the outpost for anything that explains what happened here. Press E.",
        6 => "Take the emergency water from the outpost and find the dry saplings nearby.",
        7 => "Water the marked saplings. Then step back and watch the forest respond.",
        _ => "The forest is waking up. New stories and regions are opening around you."
    };

    public bool Apply(QuestSignal signal)
    {
        if (complete) return false;
        switch (signal)
        {
            case QuestSignal.RidgeReached when step == 0: step = 1; return true;
            case QuestSignal.RidgeInvestigated when step == 1: step = 2; return true;
            case QuestSignal.AnimalFound when step == 2: step = 3; return true;
            case QuestSignal.WildlifeChecked when step == 3: step = 4; return true;
            case QuestSignal.OutpostFound when step == 4: step = 5; return true;
            case QuestSignal.OutpostInvestigated when step == 5: step = 6; return true;
            case QuestSignal.WaterCacheCollected when step == 6: step = 7; return true;
            case QuestSignal.SaplingsWatered when step == 7:
                EcosystemState.Instance?.RestoreDeepForest();
                step = 8; complete = true; return true;
            default: return false;
        }
    }

    public QuestSnapshot CaptureSnapshot() => new QuestSnapshot { Id = Id, Step = step, ResourceCount = 0, Complete = complete };
    public void ApplySnapshot(QuestSnapshot snapshot)
    {
        if (snapshot.Id != Id) return;
        step = Mathf.Clamp(snapshot.Step, 0, 8);
        complete = snapshot.Complete || step >= 8;
    }
}

/// <summary>Dispatches the opening adventure and creates its physical clues/landmarks.</summary>
public sealed class ConservationQuestSystem : MonoBehaviour
{
    public static ConservationQuestSystem Instance { get; private set; }
    private RestorePondQuest restorePondQuest;
    private float proximityCheckAt;
    public IConservationQuest CurrentQuest => restorePondQuest;
    public event Action Updated;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        restorePondQuest = new RestorePondQuest();
    }

    private System.Collections.IEnumerator Start()
    {
        while (FindAnyObjectByType<ForestEnvironmentGenerator>() == null) yield return null;
        yield return null;
        BuildAdventureWorld();
    }

    private void Update()
    {
        if (Time.time < proximityCheckAt || restorePondQuest == null) return;
        proximityCheckAt = Time.time + 0.35f;
        Transform player = GameObject.Find("Guardian")?.transform;
        if (player == null) return;

        Vector2 p = new Vector2(player.position.x, player.position.z);
        if (restorePondQuest.Step == 0 && Vector2.Distance(p, AdventurePositions.NorthRidge) < 18f)
        {
            Apply(QuestSignal.RidgeReached);
            GameplayHUD.Instance?.ShowMessage("North Ridge reached. The tracks are fresh.", 4f);
        }
        else if (restorePondQuest.Step == 2 && Vector2.Distance(p, AdventurePositions.InjuredMeadow) < 18f)
        {
            Apply(QuestSignal.AnimalFound);
            GameplayHUD.Instance?.ShowMessage("You hear an animal struggling in the grass. Find it.", 4f);
        }
        else if (restorePondQuest.Step == 4 && Vector2.Distance(p, AdventurePositions.Outpost) < 18f)
        {
            Apply(QuestSignal.OutpostFound);
            GameplayHUD.Instance?.ShowMessage("Abandoned ranger outpost discovered. Search it for clues.", 5f);
        }
    }

    private void BuildAdventureWorld()
    {
        // Physical clue/landmark objects are generated once, so the opening has tangible places to visit.
        CreatePoint("North Ridge Tracks", AdventurePositions.NorthRidge, "Fresh Tracks", "Animal tracks cross the trail and lead downhill. Something passed through recently.", "Press E to examine the tracks", QuestSignal.RidgeInvestigated, PrimitiveType.Cube, new Vector3(1.2f, .08f, .5f));
        CreatePoint("Emergency Water Cache", AdventurePositions.WaterCache, "Emergency supplies", "A sealed water can is still usable. Take it before leaving the outpost.", "Press E to take the water", QuestSignal.WaterCacheCollected, PrimitiveType.Cylinder, new Vector3(.45f, .55f, .45f), true);
        CreatePoint("Dry Saplings", AdventurePositions.Saplings, "Young trees need water", "Several newly planted saplings are wilting. The soil is dry.", "Press E to water the saplings", QuestSignal.SaplingsWatered, PrimitiveType.Capsule, new Vector3(.9f, 1.2f, .9f), false, true);
        CreatePoint("Ranger Outpost", AdventurePositions.Outpost, "Abandoned ranger station", "The station has been empty for some time. There may be useful notes inside.", "Press E to search the outpost", QuestSignal.OutpostInvestigated, PrimitiveType.Cube, new Vector3(5f, 2.2f, 4f), false, true);
    }

    private void CreatePoint(string name, Vector2 position, string status, string description, string prompt, QuestSignal signal, PrimitiveType shape, Vector3 scale, bool hideAfterUse = false, bool largerLandmark = false)
    {
        ForestEnvironmentGenerator env = FindAnyObjectByType<ForestEnvironmentGenerator>();
        if (env == null) return;
        GameObject root = GameObject.CreatePrimitive(shape);
        root.name = name;
        root.transform.SetParent(env.transform, true);
        float y = env.SampleSurfaceHeight(position.x, position.y);
        root.transform.position = new Vector3(position.x, y + scale.y * 0.5f, position.y);
        root.transform.localScale = scale;
        Renderer renderer = root.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader);
            material.color = largerLandmark ? new Color(.26f, .22f, .16f) : new Color(.34f, .28f, .18f);
            renderer.sharedMaterial = material;
        }
        if (shape == PrimitiveType.Cube && name == "North Ridge Tracks")
            root.transform.rotation = Quaternion.Euler(0f, 28f, 0f);
        StoryDiscoveryPoint point = root.AddComponent<StoryDiscoveryPoint>();
        point.Configure(name, status, description, prompt, signal, hideAfterUse);
        if (largerLandmark)
        {
            BoxCollider collider = root.GetComponent<BoxCollider>();
            if (collider != null) collider.isTrigger = true;
        }
    }

    private static class AdventurePositions
    {
        public static readonly Vector2 NorthRidge = new Vector2(0f, 333f);
        public static readonly Vector2 InjuredMeadow = new Vector2(-74f, 337f);
        public static readonly Vector2 Outpost = new Vector2(-94f, 242f);
        public static readonly Vector2 WaterCache = new Vector2(-89f, 244f);
        public static readonly Vector2 Saplings = new Vector2(-66f, 260f);
    }

    public static Vector2 GetAdventurePosition(int step)
    {
        return step switch
        {
            0 or 1 => AdventurePositions.NorthRidge,
            2 => AdventurePositions.InjuredMeadow,
            3 => AdventurePositions.InjuredMeadow,
            4 or 5 => AdventurePositions.Outpost,
            6 => AdventurePositions.WaterCache,
            7 => AdventurePositions.Saplings,
            _ => new Vector2(34f, 52f)
        };
    }

    public void RegisterQuest(IConservationQuest quest) { Updated?.Invoke(); }
    public bool Apply(QuestSignal signal) { if (!restorePondQuest.Apply(signal)) return false; Updated?.Invoke(); return true; }
    public QuestSnapshot CaptureSnapshot() => restorePondQuest.CaptureSnapshot();
    public void ApplySnapshot(QuestSnapshot snapshot) { restorePondQuest.ApplySnapshot(snapshot); Updated?.Invoke(); }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
