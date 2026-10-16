using UnityEngine;

/// <summary>
/// A story clue that participates in the shared Interactable system.
/// It advances only the quest step that matches its configured signal.
/// </summary>
public sealed class StoryDiscoveryPoint : Interactable
{
    private string displayName = "Story Clue";
    private string status = "Uninvestigated";
    private string description = "There may be something useful here.";
    private string actionPrompt = "INVESTIGATE THE CLUE";
    private QuestSignal signal;

    public override string DisplayName => displayName;
    public override string Status => status;
    public override string Description => description;
    public override string Prompt => actionPrompt;

    public bool HideAfterUse { get; private set; }
    public bool IsUsed { get; private set; }

    public bool IsCurrentObjective
    {
        get
        {
            RestorePondQuest quest = ConservationQuestSystem.Instance?.CurrentQuest as RestorePondQuest;
            if (quest == null || IsUsed) return false;

            return signal switch
            {
                QuestSignal.RidgeInvestigated => quest.Step == 1,
                QuestSignal.WildlifeChecked => quest.Step == 3,
                QuestSignal.OutpostInvestigated => quest.Step == 5,
                QuestSignal.WaterCacheCollected => quest.Step == 6,
                QuestSignal.SaplingsWatered => quest.Step == 7,
                _ => false
            };
        }
    }

    public void Configure(string displayName, string status, string description, string prompt,
        QuestSignal questSignal, bool hideAfterUse)
    {
        this.displayName = string.IsNullOrWhiteSpace(displayName) ? "Story Clue" : displayName;
        this.status = string.IsNullOrWhiteSpace(status) ? "Uninvestigated" : status;
        this.description = string.IsNullOrWhiteSpace(description)
            ? "There may be something useful here." : description;
        actionPrompt = CleanPrompt(prompt);
        signal = questSignal;
        HideAfterUse = hideAfterUse;
        IsUsed = false;
    }

    public override void Interact(InteractionSystem interaction)
    {
        if (IsUsed) return;

        ConservationQuestSystem quests = ConservationQuestSystem.Instance;
        if (quests == null || !IsCurrentObjective)
        {
            GameplayHUD.Instance?.ShowMessage(
                "This is not your current task. Follow the pink objective marker.", 3.5f);
            return;
        }

        if (!quests.Apply(signal))
        {
            GameplayHUD.Instance?.ShowMessage(
                "This clue cannot advance the current task yet. Follow the objective instructions.", 3.5f);
            return;
        }

        IsUsed = true;
        base.Interact(interaction);
        GameplayHUD.Instance?.ShowMessage("Objective complete! Check the updated instructions.", 4f);

        if (HideAfterUse) gameObject.SetActive(false);
    }

    public void ResetPoint()
    {
        IsUsed = false;
        gameObject.SetActive(true);
    }

    private static string CleanPrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return "INVESTIGATE THE CLUE";
        string cleaned = prompt.Trim();

        if (cleaned.StartsWith("Press E to ", System.StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned.Substring("Press E to ".Length);
        else if (cleaned.StartsWith("Press E ", System.StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned.Substring("Press E ".Length);
        else if (cleaned.StartsWith("E ", System.StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned.Substring(2);

        return cleaned.Trim().ToUpperInvariant();
    }
}
