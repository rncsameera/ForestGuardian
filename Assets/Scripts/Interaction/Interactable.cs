using UnityEngine;

/// <summary>Shared interaction and inspection contract for gameplay objects.</summary>
public abstract class Interactable : MonoBehaviour
{
    public abstract string DisplayName { get; }
    public abstract string Status { get; }
    public abstract string Description { get; }
    public virtual string Problem => string.Empty;
    public virtual string Prompt => "Interact";

    public virtual void Interact(InteractionSystem interaction)
    {
        if (GameplayHUD.Instance != null)
            GameplayHUD.Instance.ShowInspection(DisplayName, Status, Description, Problem);
    }
}
