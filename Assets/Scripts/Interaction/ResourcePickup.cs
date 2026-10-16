using UnityEngine;

/// <summary>One-shot purifier herb used by Restore the Pond.</summary>
public sealed class ResourcePickup : Interactable
{
    private bool collected;
    private Renderer itemRenderer;
    private Collider itemCollider;
    public override string DisplayName => "Purifier Herb";
    public override string Status => collected ? "Collected" : "Ready to gather";
    public override string Description => "A hardy water-cleansing herb growing in the forest understory.";
    public override string Prompt => collected ? string.Empty : "Press E to gather Purifier Herb";

    private void Awake()
    {
        itemRenderer = GetComponentInChildren<Renderer>();
        itemCollider = GetComponent<Collider>();
    }

    public override void Interact(InteractionSystem interaction)
    {
        if (collected) return;
        ConservationQuestSystem quests = ConservationQuestSystem.Instance;
        RestorePondQuest quest = quests != null ? quests.CurrentQuest as RestorePondQuest : null;
        if (quest == null || quest.Step < 2)
        {
            GameplayHUD.Instance?.ShowMessage("Investigate the pond before gathering restoration herbs.");
            return;
        }
        if (quest.HerbCount >= RestorePondQuest.RequiredHerbs)
        {
            GameplayHUD.Instance?.ShowMessage("You have enough purifier herbs. Return to the Eastern Pond.");
            return;
        }
        collected = quests.Apply(QuestSignal.ResourceCollected);
        if (!collected) return;
        if (itemRenderer != null) itemRenderer.enabled = false;
        if (itemCollider != null) itemCollider.enabled = false;
        GameplayHUD.Instance?.ShowMessage($"Purifier herb gathered ({quest.HerbCount}/{RestorePondQuest.RequiredHerbs}).");
    }
}
