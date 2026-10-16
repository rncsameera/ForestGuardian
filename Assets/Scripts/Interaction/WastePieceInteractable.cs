using UnityEngine;

/// <summary>Direct interaction target for one visible litter item.</summary>
public sealed class WastePieceInteractable : Interactable
{
    private ConservationTaskPoint taskPoint;
    private bool collected;

    public void Initialize(ConservationTaskPoint owner, string itemName)
    {
        taskPoint = owner;
        if (!string.IsNullOrWhiteSpace(itemName)) gameObject.name = itemName;
    }

    public override string DisplayName => collected ? string.Empty : gameObject.name;
    public override string Status => collected ? "Collected" : "Waste item";
    public override string Description => collected ? string.Empty : "Discarded material that can be removed from the habitat.";
    public override string Problem => collected ? string.Empty : "This piece of litter does not belong here.";
    public override string Prompt => collected ? string.Empty : "E  PICK UP " + gameObject.name.ToUpperInvariant();

    public override void Interact(InteractionSystem interaction)
    {
        if (collected) return;

        // Old/legacy waste objects remain usable even if they were spawned by
        // an earlier version of the project. No quest-step gate is used here.
        if (taskPoint != null && taskPoint.IsCompleted) return;

        collected = true;
        EcosystemState.Instance?.RemovePondDebris();

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
            if (collider != null) collider.enabled = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
            if (renderer != null) renderer.enabled = false;

        GameplayHUD.Instance?.ShowMessage(
            "Picked up " + gameObject.name + ".",
            2.5f);
    }
}
