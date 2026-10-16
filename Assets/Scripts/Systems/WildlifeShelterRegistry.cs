using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ShelterPointType
{
    ForestRest,
    House,
    Nest
}

/// <summary>A logical, collider-free rest destination that later animal AI can reuse.</summary>
public sealed class ShelterPoint : MonoBehaviour
{
    private static readonly List<ShelterPoint> Active = new List<ShelterPoint>();
    [SerializeField] private ShelterPointType type;
    public ShelterPointType Type => type;
    public Vector3 Position => transform.position;
    public void SetType(ShelterPointType shelterType) => type = shelterType;

    public static ShelterPoint FindNearest(Vector3 from, ShelterPointType preferred, float maxDistance = 120f)
    {
        ShelterPoint nearest = null;
        float best = maxDistance * maxDistance;
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            ShelterPoint point = Active[i];
            if (point == null) { Active.RemoveAt(i); continue; }
            if (point.type != preferred) continue;
            float distance = (point.transform.position - from).sqrMagnitude;
            if (distance < best) { best = distance; nearest = point; }
        }
        return nearest;
    }

    public static ShelterPointType BestFor(AnimalSpecies species, bool predator)
    {
        if (species == AnimalSpecies.Dog) return ShelterPointType.House;
        if (species == AnimalSpecies.Bird || species == AnimalSpecies.RareForestBird || species == AnimalSpecies.PassengerPigeon)
            return ShelterPointType.Nest;
        return ShelterPointType.ForestRest;
    }

    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);
}

/// <summary>Creates a small set of invisible rest/perch markers after the forest has generated.</summary>
public sealed class WildlifeShelterRegistry : MonoBehaviour
{
    [SerializeField] private ForestEnvironmentGenerator environment;
    private readonly List<GameObject> generated = new List<GameObject>();

    private IEnumerator Start()
    {
        if (environment == null) environment = FindAnyObjectByType<ForestEnvironmentGenerator>();
        while (environment == null || environment.transform.Find("Trees") == null || environment.transform.Find("Trees").childCount == 0)
            yield return null;

        CreateMarker("House Rest Point", ShelterPointType.House, new Vector2(environment.HouseCenter.x + 11f, environment.HouseCenter.y));
        Transform trees = environment.transform.Find("Trees");
        int count = Mathf.Min(24, trees.childCount);
        int stride = Mathf.Max(1, trees.childCount / count);
        int placed = 0;
        int nestsPlaced = 0;
        for (int i = 0; i < trees.childCount && placed < count; i += stride)
        {
            Transform tree = trees.GetChild(i);
            Vector3 basePosition = tree.position;
            Vector2 rest = new Vector2(basePosition.x + 6.8f, basePosition.z + 1.2f);
            for (int attempt = 0; attempt < 8 && !environment.IsAnimalGroundLocationClear(rest, 1.8f); attempt++)
            {
                float angle = attempt * 45f * Mathf.Deg2Rad;
                rest = new Vector2(basePosition.x + Mathf.Cos(angle) * 7.5f, basePosition.z + Mathf.Sin(angle) * 7.5f);
            }
            if (environment.IsAnimalGroundLocationClear(rest, 1.8f))
            {
                CreateMarker("Forest Rest Point", ShelterPointType.ForestRest, rest);
                placed++;
            }

            if (nestsPlaced < 24)
            {
                float ground = environment.SampleSurfaceHeight(basePosition.x, basePosition.z);
                float canopyHeight = ground + Mathf.Max(9f, tree.lossyScale.y * 9f);
                CreateMarker("Tree Nest Perch", ShelterPointType.Nest, new Vector2(basePosition.x, basePosition.z), canopyHeight);
                nestsPlaced++;
            }
        }
    }

    private void CreateMarker(string markerName, ShelterPointType type, Vector2 point, float forcedHeight = float.NaN)
    {
        GameObject marker = new GameObject(markerName);
        marker.transform.SetParent(transform, true);
        float y = float.IsNaN(forcedHeight) ? environment.SampleSurfaceHeight(point.x, point.y) : forcedHeight;
        marker.transform.position = new Vector3(point.x, y, point.y);
        marker.AddComponent<ShelterPoint>().SetType(type);
        generated.Add(marker);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < generated.Count; i++)
            if (generated[i] != null) Destroy(generated[i]);
    }
}
