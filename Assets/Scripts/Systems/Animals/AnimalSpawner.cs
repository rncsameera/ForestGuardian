using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Spawns a small habitat-based wildlife population after the forest is built.</summary>
public sealed class AnimalSpawner : MonoBehaviour
{
    public static AnimalSpawner Instance { get; private set; }
    private static readonly AnimalSpecies[] ForestPopulation =
    {
        AnimalSpecies.Deer, AnimalSpecies.Deer, AnimalSpecies.Deer, AnimalSpecies.Deer, AnimalSpecies.Deer,
        AnimalSpecies.Rabbit, AnimalSpecies.Rabbit, AnimalSpecies.Rabbit, AnimalSpecies.Rabbit, AnimalSpecies.Rabbit, AnimalSpecies.Rabbit,
        AnimalSpecies.Dog, AnimalSpecies.Dog,
        AnimalSpecies.Pig, AnimalSpecies.Pig, AnimalSpecies.Pig,
        AnimalSpecies.Lion, AnimalSpecies.Tiger,
        AnimalSpecies.Snake, AnimalSpecies.Snake, AnimalSpecies.Snake, AnimalSpecies.Snake,
        AnimalSpecies.Bird, AnimalSpecies.Bird, AnimalSpecies.Bird, AnimalSpecies.Bird, AnimalSpecies.Bird, AnimalSpecies.Bird, AnimalSpecies.Bird, AnimalSpecies.Bird,
        AnimalSpecies.Dodo, AnimalSpecies.PassengerPigeon,
        AnimalSpecies.Sheep, AnimalSpecies.Sheep, AnimalSpecies.Sheep,
        AnimalSpecies.Goat, AnimalSpecies.Goat, AnimalSpecies.Goat,
        AnimalSpecies.Giraffe,
        AnimalSpecies.Cow, AnimalSpecies.Cow, AnimalSpecies.Cow, AnimalSpecies.Cow,
        AnimalSpecies.Elephant
    };

    private static readonly Vector2[] StartAreaOffsets =
    {
        new Vector2(-4f, 6f), new Vector2(-2f, 8f), new Vector2(0f, 9f), new Vector2(2f, 8f),
        new Vector2(4f, 6f), new Vector2(7f, 12f), new Vector2(11f, 9f), new Vector2(14f, 5f),
        new Vector2(-15f, -1f), new Vector2(-11f, -5f), new Vector2(-7f, -8f), new Vector2(-2f, -10f),
        new Vector2(3f, -10f), new Vector2(8f, -8f), new Vector2(12f, -4f), new Vector2(15f, 0f),
        new Vector2(-16f, 11f), new Vector2(17f, 10f), new Vector2(-17f, -7f), new Vector2(18f, -6f),
        new Vector2(-4f, 20f), new Vector2(4f, 21f), new Vector2(-20f, 3f), new Vector2(20f, 3f), new Vector2(0f, -17f)
    };

    private readonly List<AnimalMovement> spawnedAnimals = new List<AnimalMovement>();
    private ForestEnvironmentGenerator environment;
    private Transform player;
    private Transform animalsRoot;
    private System.Random random;
    private bool initialized;
    private bool restoredPondCapybarasSpawned;

    public IReadOnlyList<AnimalMovement> SpawnedAnimals => spawnedAnimals;

    private void Awake() { Instance = this; }

    private void Start()
    {
        Debug.Log("[AnimalSpawner] INITIALIZED", this);
        StartCoroutine(InitializeWhenWorldIsReady());
    }

    private System.Collections.IEnumerator InitializeWhenWorldIsReady()
    {
        // The environment generator builds its terrain during Awake. Start runs after
        // every active scene object's Awake, so the ground mesh is ready here.
        ForestEnvironmentGenerator world = null;
        for (int attempt = 0; attempt < 120 && world == null; attempt++)
        {
            world = FindAnyObjectByType<ForestEnvironmentGenerator>();
            if (world == null)
                yield return null;
        }

        if (world == null)
        {
            Debug.LogError("[AnimalSpawner] No ForestEnvironmentGenerator exists in the active scene; cannot spawn animals.", this);
            yield break;
        }

        Initialize(world, GameObject.Find("Guardian")?.transform);
    }

    public void Initialize(ForestEnvironmentGenerator world, Transform playerTransform)
    {
        if (initialized || world == null)
            return;

        initialized = true;
        environment = world;
        player = playerTransform;
        random = new System.Random(98217);
        GameObject root = GameObject.Find("Animals");
        if (root == null)
            root = new GameObject("Animals");
        root.transform.SetParent(environment.transform, false);
        animalsRoot = root.transform;

        Dictionary<AnimalSpecies, Transform> categoryRoots = new Dictionary<AnimalSpecies, Transform>();
        Debug.Log("[AnimalSpawner] Spawning animals...", this);
        Dictionary<AnimalSpecies, int> speciesIndexes = new Dictionary<AnimalSpecies, int>();
        for (int i = 0; i < ForestPopulation.Length; i++)
        {
            AnimalSpecies species = ForestPopulation[i];
            Transform category;
            if (!categoryRoots.TryGetValue(species, out category))
            {
                GameObject categoryObject = new GameObject(CategoryName(species));
                categoryObject.transform.SetParent(animalsRoot, false);
                category = categoryObject.transform;
                categoryRoots.Add(species, category);
            }

            AnimalProfile profile = ProfileFor(species);
            int speciesIndex = speciesIndexes.TryGetValue(species, out int nextIndex) ? nextIndex : 0;
            speciesIndexes[species] = speciesIndex + 1;
            Vector2 point = FindRegionalSpawnPoint(species, speciesIndex, profile);
            float ground = environment.GetGroundHeight(point.x, point.y);
            Vector3 position = new Vector3(point.x, ground, point.y);
            if (profile.canFly)
                position.y += 5.5f + (i % 3) * 1.2f;

            GameObject animal = new GameObject(species.ToString());
            animal.transform.SetParent(category, true);
            animal.transform.position = position;
            animal.transform.rotation = Quaternion.Euler(0f, (i * 47) % 360, 0f);
            AnimalController interaction = animal.AddComponent<AnimalController>();
            interaction.SetAnimalName(species.ToString());
            AnimalMovement movement = animal.AddComponent<AnimalMovement>();
            movement.Initialize(profile, environment, player, position);
            ValidateGeneratedRenderer(animal.transform, species);
            spawnedAnimals.Add(movement);
            Debug.Log($"[AnimalSpawner] Spawned {species} at {animal.transform.position}", animal);
        }

        SpawnCapybaraGroups();
        Debug.Log($"[AnimalSpawner] Spawn complete: {spawnedAnimals.Count} animals across {environment.PondCount} pond habitats.", this);
    }

    private Vector2 FindRegionalSpawnPoint(AnimalSpecies species, int speciesIndex, AnimalProfile profile)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            Vector2 candidate;
            switch (species)
            {
                case AnimalSpecies.Dog:
                    candidate = AroundHouse(speciesIndex);
                    break;
                case AnimalSpecies.Lion:
                case AnimalSpecies.Tiger:
                    candidate = RandomWorldPoint();
                    if (Vector2.Distance(candidate, environment.HouseCenter) < 115f) continue;
                    break;
                case AnimalSpecies.Sheep:
                case AnimalSpecies.Goat:
                case AnimalSpecies.Giraffe:
                case AnimalSpecies.Cow:
                    candidate = AroundClearing(speciesIndex + (int)species * 3);
                    break;
                case AnimalSpecies.Deer:
                    candidate = speciesIndex % 3 == 0
                        ? AroundClearing(speciesIndex + 2)
                        : speciesIndex % 3 == 1 ? AroundPond(speciesIndex, profile.colliderRadius) : RandomWorldPoint();
                    break;
                case AnimalSpecies.Elephant:
                case AnimalSpecies.Snake:
                    candidate = AroundPond(speciesIndex + (int)species, profile.colliderRadius + 1f);
                    break;
                default:
                    candidate = RandomWorldPoint();
                    break;
            }

            if (environment.IsWithinWorldBounds(candidate.x, candidate.y, profile.colliderRadius + 2f) &&
                environment.IsAnimalGroundLocationClear(candidate, profile.colliderRadius + 0.25f))
                return candidate;
        }

        return FindStartAreaPoint(speciesIndex, profile);
    }

    private Vector2 AroundHouse(int index)
    {
        float angle = (index * 137.5f + 35f) * Mathf.Deg2Rad;
        float radius = 11f + index * 4.2f;
        return environment.HouseCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private Vector2 AroundClearing(int index)
    {
        int count = environment.ClearingCount;
        Vector2 center = environment.GetClearingCenter((index % Mathf.Max(count, 1)));
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        float radius = (float)Math.Sqrt(random.NextDouble()) * 9f;
        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private Vector2 AroundPond(int index, float clearance)
    {
        if (environment.PondCount > 0)
        {
            int pond = (index + random.Next(environment.PondCount)) % environment.PondCount;
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            if (environment.TryFindPondShorePoint(pond, angle, 3.2f, 9f, clearance, out Vector2 shore))
                return shore;
        }
        return RandomWorldPoint();
    }

    private Vector2 RandomWorldPoint()
    {
        float x = (float)(random.NextDouble() * (environment.WorldWidth - 36f) - (environment.WorldWidth - 36f) * 0.5f);
        float z = environment.WorldStartZ + 18f + (float)random.NextDouble() * (environment.WorldDepth - 36f);
        return new Vector2(x, z);
    }

    private void SpawnCapybaraGroups()
    {
        Transform category = new GameObject("Capybaras").transform;
        category.SetParent(animalsRoot, false);
        AnimalProfile profile = CapybaraProfile();
        for (int pond = 0; pond < environment.PondCount; pond++)
        {
            Vector2 center = environment.GetPondCenter(pond);
            Vector2 radii = environment.GetPondRadii(pond);
            float habitatRadius = Mathf.Max(radii.x, radii.y) + 12f;
            for (int member = 0; member < (pond == 0 ? 1 : 2); member++)
            {
                SpawnCapybaraAtPond(pond, member, category, profile, center, habitatRadius);
            }
        }
    }

    public void SpawnRestoredPondCapybaras()
    {
        if (!initialized || restoredPondCapybarasSpawned || environment == null || animalsRoot == null) return;
        restoredPondCapybarasSpawned = true;
        Transform category = animalsRoot.Find("Capybaras");
        if (category == null) return;
        Vector2 center = environment.GetPondCenter(0);
        Vector2 radii = environment.GetPondRadii(0);
        for (int member = 1; member <= 2; member++) SpawnCapybaraAtPond(0, member, category, CapybaraProfile(), center, Mathf.Max(radii.x, radii.y) + 12f);
    }

    private void SpawnCapybaraAtPond(int pond, int member, Transform category, AnimalProfile profile, Vector2 center, float habitatRadius)
    {
        Vector2 shore = center;
        bool found = false;
        for (int attempt = 0; attempt < 24 && !found; attempt++)
        {
            float angle = (member * 137f + attempt * 37.5f + (float)random.NextDouble() * 14f) * Mathf.Deg2Rad;
            found = environment.TryFindPondShorePoint(pond, angle, 3.4f, 10f, profile.colliderRadius + 0.4f, out shore);
        }
        if (!found) { Debug.LogWarning($"[AnimalSpawner] No valid shoreline position found for Capybara at pond {pond}.", this); return; }
        Vector3 position = new Vector3(shore.x, environment.GetGroundHeight(shore.x, shore.y), shore.y);
        GameObject animal = new GameObject("Capybara"); animal.transform.SetParent(category, true); animal.transform.position = position;
        animal.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
        animal.AddComponent<AnimalController>().SetAnimalName("Capybara");
        AnimalMovement movement = animal.AddComponent<AnimalMovement>(); movement.Initialize(profile, environment, player, position); movement.SetHabitatBoundary(center, habitatRadius);
        ValidateGeneratedRenderer(animal.transform, AnimalSpecies.Capybara); spawnedAnimals.Add(movement);
        Debug.Log($"[AnimalSpawner] Spawned Capybara at pond {pond + 1}: {animal.transform.position}", animal);
    }

    private Vector2 FindStartAreaPoint(int index, AnimalProfile profile)
    {
        Vector2 start = player != null ? new Vector2(player.position.x, player.position.z) : environment.HouseCenter;
        Vector2 preferred = start + StartAreaOffsets[index % StartAreaOffsets.Length];
        if (profile.canFly || environment.IsAnimalGroundLocationClear(preferred, profile.colliderRadius + 0.25f))
            return preferred;

        for (int ring = 1; ring <= 10; ring++)
        {
            for (int step = 0; step < 16; step++)
            {
                float angle = (step + index * 0.37f) * Mathf.PI * 2f / 16f;
                Vector2 candidate = preferred + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring * 1.35f;
                if (environment.IsWithinWorldBounds(candidate.x, candidate.y, 3f) &&
                    environment.IsAnimalGroundLocationClear(candidate, profile.colliderRadius + 0.25f))
                    return candidate;
            }
        }

        Debug.LogWarning($"[AnimalSpawner] Using nearest safe fallback position for {profile.species}.", this);
        return preferred;
    }

    private static AnimalProfile ProfileFor(AnimalSpecies species)
    {
        switch (species)
        {
            case AnimalSpecies.Deer: return DeerProfile();
            case AnimalSpecies.Rabbit: return RabbitProfile();
            case AnimalSpecies.Dog: return DogProfile();
            case AnimalSpecies.Pig: return PigProfile();
            case AnimalSpecies.Lion: return LionProfile();
            case AnimalSpecies.Tiger: return TigerProfile();
            case AnimalSpecies.Snake: return SnakeProfile();
            case AnimalSpecies.Bird: return BirdProfile(false);
            case AnimalSpecies.RareForestBird: return BirdProfile(true);
            case AnimalSpecies.Dodo: return DodoProfile();
            case AnimalSpecies.PassengerPigeon: return PassengerPigeonProfile();
            case AnimalSpecies.Sheep: return SheepProfile();
            case AnimalSpecies.Goat: return GoatProfile();
            case AnimalSpecies.Cow: return CowProfile();
            case AnimalSpecies.Elephant: return ElephantProfile();
            case AnimalSpecies.Capybara: return CapybaraProfile();
            default: return GiraffeProfile();
        }
    }

    private static string CategoryName(AnimalSpecies species)
    {
        switch (species)
        {
            case AnimalSpecies.Deer: return "Deer";
            case AnimalSpecies.Rabbit: return "Rabbits";
            case AnimalSpecies.Dog: return "Dogs";
            case AnimalSpecies.Pig: return "Pigs";
            case AnimalSpecies.Lion: return "Lions";
            case AnimalSpecies.Tiger: return "Tigers";
            case AnimalSpecies.Snake: return "Snakes";
            case AnimalSpecies.Bird: return "Birds";
            case AnimalSpecies.RareForestBird: return "Birds";
            case AnimalSpecies.Dodo:
            case AnimalSpecies.PassengerPigeon: return "Birds";
            case AnimalSpecies.Sheep: return "Sheep";
            case AnimalSpecies.Goat: return "Goats";
            case AnimalSpecies.Cow: return "Cows";
            case AnimalSpecies.Elephant: return "Elephants";
            case AnimalSpecies.Capybara: return "Capybaras";
            default: return "Giraffe";
        }
    }

    private static void ValidateGeneratedRenderer(Transform animal, AnimalSpecies species)
    {
        if (animal.GetComponentInChildren<Renderer>() == null)
            Debug.LogError($"[AnimalSpawner] {species} was spawned without a generated visual renderer.", animal);
    }

    private bool IsSpawnSeparated(Vector2 point, AnimalProfile profile)
    {
        float requiredDistance = profile.canFly ? 3f : profile.colliderRadius + 0.55f;
        for (int i = 0; i < spawnedAnimals.Count; i++)
        {
            AnimalMovement other = spawnedAnimals[i];
            if (other == null)
                continue;
            Vector2 otherPoint = new Vector2(other.transform.position.x, other.transform.position.z);
            if ((point - otherPoint).sqrMagnitude < requiredDistance * requiredDistance)
                return false;
        }
        return true;
    }

    private AnimalHabitat ChooseHabitat(AnimalHabitat options)
    {
        List<AnimalHabitat> candidates = new List<AnimalHabitat>(5);
        if ((options & AnimalHabitat.Forest) != 0) candidates.Add(AnimalHabitat.Forest);
        if ((options & AnimalHabitat.NearWater) != 0) candidates.Add(AnimalHabitat.NearWater);
        if ((options & AnimalHabitat.House) != 0) candidates.Add(AnimalHabitat.House);
        if ((options & AnimalHabitat.OpenGrassland) != 0) candidates.Add(AnimalHabitat.OpenGrassland);
        if ((options & AnimalHabitat.Sky) != 0) candidates.Add(AnimalHabitat.Sky);
        return candidates.Count == 0 ? AnimalHabitat.Forest : candidates[random.Next(candidates.Count)];
    }

    private Vector2 ChooseCandidate(AnimalHabitat habitat)
    {
        if (habitat == AnimalHabitat.NearWater && environment.PondCount > 0)
        {
            int pond = random.Next(environment.PondCount);
            Vector2 center = environment.GetPondCenter(pond);
            Vector2 radii = environment.GetPondRadii(pond);
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float shoreOffset = 2.5f + (float)random.NextDouble() * 7f;
            return center + new Vector2(Mathf.Cos(angle) * (radii.x + shoreOffset), Mathf.Sin(angle) * (radii.y + shoreOffset));
        }

        if (habitat == AnimalHabitat.House)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float radius = 8f + (float)random.NextDouble() * 16f;
            Vector2 house = environment.HouseCenter;
            return house + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        if (habitat == AnimalHabitat.OpenGrassland && environment.ClearingCount > 0)
        {
            Vector2 center = environment.GetClearingCenter(random.Next(environment.ClearingCount));
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float radius = (float)Math.Sqrt(random.NextDouble()) * 12f;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        float x = (float)(random.NextDouble() * (environment.WorldWidth - 30f) - (environment.WorldWidth - 30f) * 0.5f);
        float z = environment.WorldStartZ + 15f + (float)random.NextDouble() * (environment.WorldDepth - 30f);
        return new Vector2(x, z);
    }

    private static AnimalProfile DeerProfile()
    {
        return new AnimalProfile { species = AnimalSpecies.Deer, preferredHabitat = AnimalHabitat.Forest | AnimalHabitat.NearWater | AnimalHabitat.OpenGrassland, movementSpeed = 2.4f, fleeSpeed = 6.2f, detectionRadius = 20f, playerNoticeRadius = 8f, wanderRadius = 38f, visualScale = 1.1f, colliderRadius = 0.38f, colliderHeight = 1.45f };
    }

    private static AnimalProfile RabbitProfile()
    {
        return new AnimalProfile { species = AnimalSpecies.Rabbit, preferredHabitat = AnimalHabitat.Forest | AnimalHabitat.NearWater | AnimalHabitat.House | AnimalHabitat.OpenGrassland, movementSpeed = 1.8f, fleeSpeed = 7f, detectionRadius = 13f, playerNoticeRadius = 8f, wanderRadius = 22f, visualScale = 0.62f, colliderRadius = 0.22f, colliderHeight = 0.66f };
    }

    private static AnimalProfile DogProfile()
    {
        return new AnimalProfile { species = AnimalSpecies.Dog, preferredHabitat = AnimalHabitat.House, movementSpeed = 2f, fleeSpeed = 3f, detectionRadius = 14f, playerNoticeRadius = 7f, wanderRadius = 28f, visualScale = 0.8f, colliderRadius = 0.28f, colliderHeight = 0.95f, canBePrey = false };
    }

    private static AnimalProfile PigProfile()
    {
        return new AnimalProfile { species = AnimalSpecies.Pig, preferredHabitat = AnimalHabitat.Forest | AnimalHabitat.OpenGrassland, movementSpeed = 1.15f, fleeSpeed = 3.8f, detectionRadius = 12f, playerNoticeRadius = 6f, wanderRadius = 24f, visualScale = 0.92f, colliderRadius = 0.36f, colliderHeight = 1.05f };
    }

    private static AnimalProfile LionProfile()
    {
        return new AnimalProfile { species = AnimalSpecies.Lion, preferredHabitat = AnimalHabitat.Forest, isPredator = true, canBePrey = false, movementSpeed = 2f, fleeSpeed = 4.7f, detectionRadius = 21f, playerNoticeRadius = 12f, wanderRadius = 42f, visualScale = 1.18f, colliderRadius = 0.44f, colliderHeight = 1.55f };
    }

    private static AnimalProfile TigerProfile()
    {
        return new AnimalProfile { species = AnimalSpecies.Tiger, preferredHabitat = AnimalHabitat.Forest, isPredator = true, canBePrey = false, movementSpeed = 1.15f, fleeSpeed = 2.4f, detectionRadius = 23f, playerNoticeRadius = 12f, wanderRadius = 14f, visualScale = 1.12f, colliderRadius = 0.42f, colliderHeight = 1.5f };
    }

    private static AnimalProfile SnakeProfile()
    {
        return new AnimalProfile { species = AnimalSpecies.Snake, preferredHabitat = AnimalHabitat.Forest | AnimalHabitat.NearWater, movementSpeed = 0.72f, fleeSpeed = 1.7f, detectionRadius = 8f, playerNoticeRadius = 5f, wanderRadius = 12f, visualScale = 0.7f, colliderRadius = 0.18f, colliderHeight = 0.4f };
    }

    private static AnimalProfile BirdProfile(bool rare)
    {
        return new AnimalProfile
        {
            species = rare ? AnimalSpecies.RareForestBird : AnimalSpecies.Bird,
            preferredHabitat = AnimalHabitat.Forest | AnimalHabitat.NearWater | AnimalHabitat.OpenGrassland | AnimalHabitat.Sky,
            canFly = true,
            canBePrey = true,
            movementSpeed = rare ? 5.2f : 4.2f,
            fleeSpeed = rare ? 7f : 6f,
            detectionRadius = 12f,
            playerNoticeRadius = 9f,
            wanderRadius = rare ? 45f : 32f,
            visualScale = 0.72f,
            colliderRadius = 0.2f,
            colliderHeight = 0.5f,
            spawnProbability = rare ? 0.55f : 1f
        };
    }

    private static AnimalProfile DodoProfile()
    {
        return new AnimalProfile
        {
            species = AnimalSpecies.Dodo,
            preferredHabitat = AnimalHabitat.Forest | AnimalHabitat.OpenGrassland,
            canBePrey = true,
            movementSpeed = 1.15f,
            fleeSpeed = 2.6f,
            detectionRadius = 12f,
            playerNoticeRadius = 7f,
            wanderRadius = 20f,
            visualScale = 0.8f,
            colliderRadius = 0.28f,
            colliderHeight = 1.1f
        };
    }

    private static AnimalProfile PassengerPigeonProfile()
    {
        AnimalProfile profile = BirdProfile(false);
        profile.species = AnimalSpecies.PassengerPigeon;
        profile.visualScale = 0.78f;
        profile.movementSpeed = 5.1f;
        profile.wanderRadius = 42f;
        return profile;
    }

    private static AnimalProfile SheepProfile()
    {
        return new AnimalProfile { species = AnimalSpecies.Sheep, preferredHabitat = AnimalHabitat.House | AnimalHabitat.OpenGrassland | AnimalHabitat.Forest, movementSpeed = 1.05f, fleeSpeed = 3.8f, detectionRadius = 13f, playerNoticeRadius = 6f, wanderRadius = 22f, visualScale = 0.92f, colliderRadius = 0.36f, colliderHeight = 1.15f };
    }

    private static AnimalProfile GoatProfile()
    {
        return new AnimalProfile { species = AnimalSpecies.Goat, preferredHabitat = AnimalHabitat.Forest | AnimalHabitat.NearWater | AnimalHabitat.House | AnimalHabitat.OpenGrassland, movementSpeed = 1.65f, fleeSpeed = 4.8f, detectionRadius = 15f, playerNoticeRadius = 7f, wanderRadius = 30f, visualScale = 0.92f, colliderRadius = 0.32f, colliderHeight = 1.35f };
    }

    private static AnimalProfile GiraffeProfile()
    {
        return new AnimalProfile { species = AnimalSpecies.Giraffe, preferredHabitat = AnimalHabitat.OpenGrassland, movementSpeed = 1.45f, fleeSpeed = 4.1f, detectionRadius = 18f, playerNoticeRadius = 8f, wanderRadius = 26f, visualScale = 1.18f, colliderRadius = 0.48f, colliderHeight = 3.7f };
    }

    private static AnimalProfile CowProfile()
    {
        return new AnimalProfile { species = AnimalSpecies.Cow, preferredHabitat = AnimalHabitat.OpenGrassland, movementSpeed = 1.05f, fleeSpeed = 2.8f, detectionRadius = 13f, playerNoticeRadius = 5f, wanderRadius = 28f, visualScale = 1f, colliderRadius = 0.52f, colliderHeight = 1.65f };
    }

    private static AnimalProfile ElephantProfile()
    {
        return new AnimalProfile { species = AnimalSpecies.Elephant, preferredHabitat = AnimalHabitat.NearWater | AnimalHabitat.OpenGrassland, movementSpeed = 0.8f, fleeSpeed = 1.8f, detectionRadius = 18f, playerNoticeRadius = 0f, wanderRadius = 22f, visualScale = 1.25f, colliderRadius = 0.9f, colliderHeight = 2.7f, canBePrey = false };
    }

    private static AnimalProfile CapybaraProfile()
    {
        return new AnimalProfile { species = AnimalSpecies.Capybara, preferredHabitat = AnimalHabitat.NearWater, movementSpeed = 0.68f, fleeSpeed = 1.2f, detectionRadius = 6f, playerNoticeRadius = 0f, wanderRadius = 6f, visualScale = 0.78f, colliderRadius = 0.36f, colliderHeight = 0.8f, canBePrey = false };
    }
}
