using System;
using UnityEngine;

[Serializable]
public struct EcosystemSnapshot
{
    public float WaterQuality;
    public float VegetationHealth;
    public float Biodiversity;
    public float HabitatHealth;
    public bool MainPondRestored;
    public Vector3 RestoredPondPosition;
}

/// <summary>Small save-ready set of ecosystem values changed by conservation actions.</summary>
public sealed class EcosystemState : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)] private float waterQuality = 38f;
    [SerializeField, Range(0f, 100f)] private float vegetationHealth = 68f;
    [SerializeField, Range(0f, 100f)] private float biodiversity = 44f;
    [SerializeField, Range(0f, 100f)] private float habitatHealth = 50f;
    [SerializeField] private bool mainPondRestored;
    [SerializeField] private Vector3 restoredPondPosition;

    public static EcosystemState Instance { get; private set; }
    public float WaterQuality => waterQuality;
    public float VegetationHealth => vegetationHealth;
    public float Biodiversity => biodiversity;
    public float HabitatHealth => habitatHealth;
    public float OverallHealth => (waterQuality + vegetationHealth + biodiversity + habitatHealth) * 0.25f;
    public bool MainPondRestored => mainPondRestored;
    public Vector3 RestoredPondPosition => restoredPondPosition;
    public event Action Changed;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void RestoreMainPond(Vector3 pondPosition)
    {
        if (mainPondRestored) return;
        mainPondRestored = true;
        restoredPondPosition = pondPosition;
        waterQuality = Mathf.Clamp(waterQuality + 10f, 0f, 100f);
        habitatHealth = Mathf.Clamp(habitatHealth + 3f, 0f, 100f);
        Changed?.Invoke();
    }

    public void RemovePondDebris() { waterQuality = Mathf.Clamp(waterQuality + 2f, 0f, 100f); habitatHealth = Mathf.Clamp(habitatHealth + 1f, 0f, 100f); Changed?.Invoke(); }
    public void RestorePondVegetation() { vegetationHealth = Mathf.Clamp(vegetationHealth + 12f, 0f, 100f); habitatHealth = Mathf.Clamp(habitatHealth + 7f, 0f, 100f); Changed?.Invoke(); }
    public void ProtectPondWildlife() { biodiversity = Mathf.Clamp(biodiversity + 5f, 0f, 100f); Changed?.Invoke(); }
    public void RestoreDeepForest() { vegetationHealth = Mathf.Clamp(vegetationHealth + 5f, 0f, 100f); habitatHealth = Mathf.Clamp(habitatHealth + 4f, 0f, 100f); biodiversity = Mathf.Clamp(biodiversity + 2f, 0f, 100f); Changed?.Invoke(); }

    public EcosystemSnapshot CaptureSnapshot()
    {
        return new EcosystemSnapshot
        {
            WaterQuality = waterQuality,
            VegetationHealth = vegetationHealth,
            Biodiversity = biodiversity,
            HabitatHealth = habitatHealth,
            MainPondRestored = mainPondRestored,
            RestoredPondPosition = restoredPondPosition
        };
    }

    public void ApplySnapshot(EcosystemSnapshot snapshot)
    {
        waterQuality = Mathf.Clamp(snapshot.WaterQuality, 0f, 100f);
        vegetationHealth = Mathf.Clamp(snapshot.VegetationHealth, 0f, 100f);
        biodiversity = Mathf.Clamp(snapshot.Biodiversity, 0f, 100f);
        habitatHealth = Mathf.Clamp(snapshot.HabitatHealth, 0f, 100f);
        mainPondRestored = snapshot.MainPondRestored;
        restoredPondPosition = snapshot.RestoredPondPosition;
        if (mainPondRestored) AnimalMovement.NotifyPondRestored(restoredPondPosition);
        Changed?.Invoke();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
