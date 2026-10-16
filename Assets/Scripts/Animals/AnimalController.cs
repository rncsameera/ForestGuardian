using UnityEngine;

/// <summary>
/// Player-facing wildlife interaction and lightweight animal needs.
/// Animals are part of the world first; care interactions are contextual,
/// not a breeding/simulation system.
/// </summary>
public class AnimalController : Interactable
{
    [Header("Animal Identity")]
    [SerializeField] private string animalName = "Wild Animal";
    [SerializeField] private bool isAdopted;

    [Header("Needs")]
    [Range(0f, 100f)] [SerializeField] private float hunger = 100f;
    [Range(0f, 100f)] [SerializeField] private float thirst = 100f;
    [Range(0f, 100f)] [SerializeField] private float health = 100f;
    [Range(0f, 100f)] [SerializeField] private float happiness = 100f;

    [Header("Need Drain")]
    [SerializeField] private float hungerDrainPerMinute = 2.5f;
    [SerializeField] private float thirstDrainPerMinute = 3.5f;
    [SerializeField] private float happinessDrainPerMinute = 0.6f;

    [Header("Sickness")]
    [SerializeField] private bool isSick;
    [SerializeField] private float sicknessHealthDrainPerMinute = 2.5f;

    private Transform guardian;
    private float interactionDistance = 3.2f;

    public string AnimalName => animalName;
    public bool IsAdopted => isAdopted;
    public float Hunger => hunger;
    public float Thirst => thirst;
    public float Health => health;
    public float Happiness => happiness;
    public bool IsSick => isSick;

    public override string DisplayName => animalName;
    public override string Status => GetNeedMessage();
    public override string Description => isSick
        ? $"{animalName} looks unwell and is keeping still. It may need treatment."
        : $"A wild {animalName}. Observe it quietly and give it room to move.";
    public override string Problem => isSick ? $"{animalName} appears injured or unwell." : string.Empty;

    public override string Prompt
    {
        get
        {
            if (isSick) return "Press E to treat";
            if (thirst < 45f) return "Press E to give water";
            if (hunger < 45f) return "Press E to feed";
            return "Press E to observe";
        }
    }

    public void MarkInjuredForQuest()
    {
        isSick = true;
        health = 42f;
        thirst = 34f;
        hunger = 68f;
    }

    public void SetAnimalName(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) animalName = value;
    }

    private void Start()
    {
        guardian = GameObject.Find("Guardian")?.transform;
    }

    private void Update()
    {
        float minute = Time.deltaTime / 60f;
        hunger = Mathf.Clamp(hunger - hungerDrainPerMinute * minute, 0f, 100f);
        thirst = Mathf.Clamp(thirst - thirstDrainPerMinute * minute, 0f, 100f);
        happiness = Mathf.Clamp(happiness - happinessDrainPerMinute * minute, 0f, 100f);

        if (isSick)
            health = Mathf.Clamp(health - sicknessHealthDrainPerMinute * minute, 0f, 100f);
    }

    public override void Interact(InteractionSystem interaction)
    {
        if (isSick && health < 80f)
        {
            Heal(22f);
            ConservationQuestSystem quests = ConservationQuestSystem.Instance;
            RestorePondQuest quest = quests?.CurrentQuest as RestorePondQuest;
            if (quest != null && quest.Step == 3)
                quests.Apply(QuestSignal.WildlifeChecked);
            GameplayHUD.Instance?.ShowMessage($"{animalName} is responding to treatment. Its breathing is steadier now.", 4f);
            return;
        }

        if (thirst < 65f)
        {
            GiveWater(25f);
            GameplayHUD.Instance?.ShowMessage($"You give {animalName} some water. It settles down.", 3f);
            return;
        }

        if (hunger < 65f)
        {
            Feed(22f);
            GameplayHUD.Instance?.ShowMessage($"{animalName} accepts the food and relaxes.", 3f);
            return;
        }

        base.Interact(interaction);
    }

    public void Feed(float amount)
    {
        hunger = Mathf.Clamp(hunger + amount, 0f, 100f);
        happiness = Mathf.Clamp(happiness + amount * 0.25f, 0f, 100f);
    }

    public void GiveWater(float amount)
    {
        thirst = Mathf.Clamp(thirst + amount, 0f, 100f);
    }

    public void Heal(float amount)
    {
        health = Mathf.Clamp(health + amount, 0f, 100f);
        if (health > 50f) isSick = false;
    }

    public void Adopt()
    {
        isAdopted = true;
        happiness = Mathf.Clamp(happiness + 15f, 0f, 100f);
    }

    public string GetNeedMessage()
    {
        if (isSick) return animalName + " is injured or sick.";
        if (thirst < 30f) return animalName + " is thirsty.";
        if (hunger < 30f) return animalName + " is hungry.";
        if (health < 40f) return animalName + " needs medical care.";
        if (happiness < 30f) return animalName + " needs attention.";
        return animalName + " is doing well.";
    }
}
