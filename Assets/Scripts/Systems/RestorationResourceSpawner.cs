using UnityEngine;

/// <summary>
/// Legacy compatibility component.
/// The opening game no longer spawns repetitive pond-cleaning tasks.
/// Ponds remain world locations and can be used by later missions.
/// </summary>
public sealed class RestorationResourceSpawner : MonoBehaviour
{
    [SerializeField] private ForestEnvironmentGenerator environment;

    private void Start()
    {
        if (environment == null)
            environment = FindAnyObjectByType<ForestEnvironmentGenerator>();

        // Intentionally empty.
        // The opening adventure now uses investigation, wildlife care,
        // the ranger outpost, supplies and saplings instead of five
        // identical pond-waste interactions.
    }
}
