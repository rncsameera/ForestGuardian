using UnityEngine;

/// <summary>Inspection and visible restoration interaction for a generated pond.</summary>
public sealed class PondRestorationSite : Interactable
{
    [SerializeField] private string pondName = "Eastern Pond";
    [SerializeField] private bool isPrimaryQuestPond;
    [SerializeField] private bool restored;
    private MeshRenderer waterRenderer;
    private Vector3 pondCenter;
    private bool diagnosisPending;

    public override string DisplayName => pondName;
    public override string Status => restored ? "Healthy - restored" : isPrimaryQuestPond ? "Polluted - poor quality" : IsWestwoodAssessment ? "Damaged - dumping and disturbed refuge" : "Healthy";
    public override string Description => restored
        ? "Clear water ripples along the shore. Plants and wildlife are returning to the habitat."
        : isPrimaryQuestPond ? "Water quality is declining. Plastic waste and debris are affecting the pond habitat."
        : IsWestwoodAssessment ? "Discarded material lies along the shore. Sparse plants leave a wildlife refuge exposed."
        : "A freshwater pond supports plants and wildlife.";
    public override string Problem => restored ? string.Empty : isPrimaryQuestPond ? "Water quality is low and animals are avoiding this area." : IsWestwoodAssessment ? "Dumping has damaged shoreline vegetation and disrupted a wildlife refuge." : string.Empty;
    private bool IsWestwoodAssessment => !isPrimaryQuestPond && ConservationQuestSystem.Instance != null && (ConservationQuestSystem.Instance.CurrentQuest as RestorePondQuest)?.Step == 5;
    public bool IsRestored => restored;
    public bool IsPrimaryQuestPond => isPrimaryQuestPond;

    public void Initialize(string generatedName, bool primary, MeshRenderer renderer, Vector3 center)
    {
        pondName = primary ? "Eastern Pond" : generatedName;
        isPrimaryQuestPond = primary;
        waterRenderer = renderer;
        pondCenter = center;
        restored = false;
        ApplyWaterAppearance();
    }

    public override string Prompt
    {
        get
        {
            RestorePondQuest quest = ConservationQuestSystem.Instance?.CurrentQuest as RestorePondQuest;
            if (quest == null) return "Press E to inspect " + DisplayName;
            if (isPrimaryQuestPond && quest.Step == 1) return "Press E to identify the pond problem";
            if (isPrimaryQuestPond && quest.Step == 3) return "Press E to observe wildlife returning to the pond";
            if (!isPrimaryQuestPond && quest.Step == 5) return "Press E to assess Westwood Pond";
            if (!isPrimaryQuestPond && quest.Step == 7) return "Press E to observe the recovering refuge";
            return "Press E to inspect " + DisplayName;
        }
    }

    public override void Interact(InteractionSystem interaction)
    {
        ConservationQuestSystem quests = ConservationQuestSystem.Instance;
        RestorePondQuest quest = quests?.CurrentQuest as RestorePondQuest;
        if (quest == null) { base.Interact(interaction); return; }
        if (isPrimaryQuestPond && quest.Step == 1)
        {
            if (diagnosisPending) return;
            PythonAIBridge bridge = PythonAIBridge.Instance;
            if (bridge == null) { GameplayHUD.Instance?.ShowMessage("The field kit is not responding. Check your equipment and try the water test again."); return; }
            diagnosisPending = true;
            GameplayHUD.Instance?.ShowMessage("Water sample collected. Analyzing the evidence…", 10f);
            bridge.StartCoroutine(bridge.DiagnosePond(CollectEvidence(), (success, message) =>
            {
                diagnosisPending = false;
                if (!success) { GameplayHUD.Instance?.ShowMessage(message + " Press E to retry the water test.", 9f); return; }
                if (!quests.Apply(QuestSignal.WaterInvestigated)) return;
                GameplayHUD.Instance?.ShowMessage("The evidence points to a water-quality problem. Follow the signs around the shoreline and restore the damaged habitat.", 8f);
            }));
        }
        else if (isPrimaryQuestPond && quest.Step == 3)
        {
            quests.Apply(QuestSignal.RecoveryObserved);
            GameplayHUD.Instance?.ShowMessage("Wildlife activity has increased around the restored pond. The Westwood conservation site is now your next objective.");
        }
        else if (!isPrimaryQuestPond && quest.Step == 5)
        {
            base.Interact(interaction);
            quests.Apply(QuestSignal.DeepAreaInvestigated);
            GameplayHUD.Instance?.ShowMessage("Assessment: dumping has damaged shoreline plants and disrupted a wildlife refuge. Restore the marked refuge.");
        }
        else if (!isPrimaryQuestPond && quest.Step == 7)
        {
            base.Interact(interaction);
            quests.Apply(QuestSignal.RecoveryObserved);
            GameplayHUD.Instance?.ShowMessage("Wildlife has returned to the refuge. The next conservation site is unlocked.");
        }
        else base.Interact(interaction);
    }

    private PythonAIBridge.PondEvidence CollectEvidence()
    {
        EcosystemState ecosystem = EcosystemState.Instance;
        int nearby = 0;
        var animals = AnimalSpawner.Instance != null ? AnimalSpawner.Instance.SpawnedAnimals : null;
        if (animals != null)
            foreach (AnimalMovement animal in animals)
                if (animal != null && (animal.transform.position - pondCenter).sqrMagnitude < 42f * 42f) nearby++;
        float vegetation = ecosystem != null ? ecosystem.VegetationHealth / 100f : .68f;
        bool raining = WeatherManager.Instance != null && WeatherManager.Instance.IsRaining;
        return new PythonAIBridge.PondEvidence
        {
            water_clarity = ecosystem != null ? ecosystem.WaterQuality / 100f : .38f,
            vegetation_health = vegetation,
            wildlife_activity = Mathf.Clamp01(nearby / 5f),
            rainfall = raining ? .75f : .25f,
            dead_vegetation = 1f - vegetation
        };
    }

    public bool Restore()
    {
        if (restored || !isPrimaryQuestPond) return false;
        restored = true;
        ApplyWaterAppearance();
        EcosystemState.Instance?.RestoreMainPond(pondCenter);
        AnimalMovement.NotifyPondRestored(pondCenter);
        AnimalSpawner.Instance?.SpawnRestoredPondCapybaras();
        return true;
    }

    private void ApplyWaterAppearance()
    {
        if (waterRenderer == null) return;
        SetWaterSubmesh(0, restored ? new Color(0.08f, 0.43f, 0.51f, 0.72f) : new Color(0.23f, 0.32f, 0.25f, 0.62f));
        SetWaterSubmesh(1, restored ? new Color(0.23f, 0.59f, 0.59f, 0.58f) : new Color(0.36f, 0.42f, 0.31f, 0.48f));
    }

    private void SetWaterSubmesh(int index, Color color)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        waterRenderer.GetPropertyBlock(block, index);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        waterRenderer.SetPropertyBlock(block, index);
    }
}
