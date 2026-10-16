using UnityEngine;

/// <summary>Simple read-only inspection data for generated habitat features.</summary>
public sealed class EnvironmentInspectable : Interactable
{
    [SerializeField] private string displayName = "Forest Habitat";
    [SerializeField] private string status = "Healthy";
    [SerializeField, TextArea] private string description = "This feature supports life in the forest.";
    [SerializeField] private string problem;

    public override string DisplayName => displayName;
    public override string Status => status;
    public override string Description => description;
    public override string Problem => problem;

    public void Configure(string itemName, string itemStatus, string itemDescription, string itemProblem = "")
    {
        displayName = itemName;
        status = itemStatus;
        description = itemDescription;
        problem = itemProblem;
    }
}
