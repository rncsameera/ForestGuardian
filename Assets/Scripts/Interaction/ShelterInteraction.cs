using UnityEngine;

/// <summary>Simple first-step interaction test on the Guardian's shelter.</summary>
public sealed class ShelterInteraction : Interactable
{
    public override string DisplayName => "Shelter";
    public override string Status => "Safe";
    public override string Description => "Your home base in the forest. The shelter is a safe place to prepare for conservation work.";
    public override string Prompt => "Press E to Interact";

    public override void Interact(InteractionSystem interaction)
    {
        Debug.Log("[ShelterInteraction] Welcome message shown.", this);
        GameplayHUD.Instance?.ShowMessage("Welcome, Forest Guardian.");
        base.Interact(interaction);
    }
}
