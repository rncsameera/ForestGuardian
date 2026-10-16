using UnityEngine;

/// <summary>
/// Generic world restoration point. It is deliberately NOT tied to a pond quest.
/// Individual litter objects should be the actual E targets; this component is only
/// a fallback/area description for legacy content.
/// </summary>
public sealed class ConservationTaskPoint : Interactable
{
    [SerializeField] private bool habitatWork;
    [SerializeField] private string taskName = "Restoration Site";
    private bool completed;

    public bool IsCompleted => completed;
    public bool IsHabitatWork => habitatWork;
    public override string DisplayName => taskName;
    public override string Status => completed ? "Complete" : habitatWork ? "Habitat restoration" : "Restoration site";
    public override string Description => completed
        ? "This part of the habitat has been restored."
        : habitatWork
            ? "A damaged part of the habitat needs attention."
            : "There is something here that needs to be investigated or restored.";
    public override string Problem => completed ? string.Empty : habitatWork ? "The habitat needs restoration." : "The site needs attention.";
    public override string Prompt => completed ? string.Empty : habitatWork ? "Press E to restore" : "Press E to inspect";

    public void Initialize(bool vegetation, string label)
    {
        habitatWork = vegetation;
        taskName = string.IsNullOrWhiteSpace(label) ? "Restoration Site" : label;
    }

    public bool RemoveWastePiece()
    {
        // Legacy compatibility only. New gameplay should use a direct waste piece.
        return false;
    }

    public void Complete()
    {
        completed = true;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            if (renderer != null) renderer.enabled = false;
        foreach (Collider collider in GetComponentsInChildren<Collider>())
            if (collider != null) collider.enabled = false;
    }

    public override void Interact(InteractionSystem interaction)
    {
        if (completed) return;
        base.Interact(interaction);
    }
}
