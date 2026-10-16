using System.Collections.Generic;
using UnityEngine;

public sealed class AnimalMovement : MonoBehaviour
{
    private static readonly List<AnimalMovement> ActiveAnimals = new List<AnimalMovement>();
    private static Vector3 restoredPondPosition;
    private static bool pondRestored;

    public static void NotifyPondRestored(Vector3 position)
    {
        restoredPondPosition = position;
        pondRestored = true;
    }

    [Header("Animal profile (set by AnimalSpawner)")]
    [SerializeField] private AnimalSpecies species;
    [SerializeField] private AnimalHabitat preferredHabitat;
    [SerializeField] private bool predator;
    [SerializeField] private float movementSpeed = 1.5f;
    [SerializeField] private float fleeSpeed = 4f;
    [SerializeField] private float detectionRadius = 14f;
    [SerializeField] private float wanderRadius = 22f;

    private AnimalProfile profile;
    private ForestEnvironmentGenerator environment;
    private Transform player;
    private AnimalVisualRig visualRig;
    private CharacterController characterController;
    private Vector3 homeAnchor;
    private Vector3 targetPosition;
    private Vector3 lastPosition;
    private Quaternion tailBaseRotation;
    private AnimalMovement preyTarget;
    private WorldTimeManager worldClock;
    private WeatherManager weatherManager;
    private ShelterPoint restShelter;
    private AnimalState state;
    private float stateTimer;
    private float scanTimer;
    private float stuckTimer;
    private float verticalVelocity;
    private float gaitPhase;
    private Vector3 tigerBodyBaseScale;
    private Vector3 tigerBodyBasePosition;
    private Vector3 animalBodyBaseScale;
    private Vector3 animalBodyBasePosition;
    private bool initialized;
    private float routineCheckTimer;
    private float eveningRestThreshold;
    private bool hasHabitatBoundary;
    private Vector2 habitatCenter;
    private float habitatRadius;

    public AnimalSpecies Species => species;
    public AnimalState State => state;
    public bool IsPredator => predator;
    public bool CanBePrey => profile != null && profile.canBePrey;
    public AnimalHabitat PreferredHabitat => preferredHabitat;

    public void SetHabitatBoundary(Vector2 center, float radius)
    {
        habitatCenter = center;
        habitatRadius = Mathf.Max(1f, radius);
        hasHabitatBoundary = true;
    }

    public void Initialize(AnimalProfile animalProfile, ForestEnvironmentGenerator world, Transform playerTransform, Vector3 spawnPosition)
    {
        profile = animalProfile;
        eveningRestThreshold = profile.canFly
            ? Random.Range(0.12f, 0.58f)
            : (profile.isPredator ? Random.Range(0.28f, 0.62f) : Random.Range(0.35f, 0.88f));
        environment = world;
        player = playerTransform;
        worldClock = WorldTimeManager.Instance != null ? WorldTimeManager.Instance : FindAnyObjectByType<WorldTimeManager>();
        weatherManager = WeatherManager.Instance != null ? WeatherManager.Instance : FindAnyObjectByType<WeatherManager>();
        species = profile.species;
        preferredHabitat = profile.preferredHabitat;
        predator = profile.isPredator;
        movementSpeed = profile.movementSpeed;
        fleeSpeed = profile.fleeSpeed;
        detectionRadius = profile.detectionRadius;
        wanderRadius = profile.wanderRadius;
        transform.position = spawnPosition;
        homeAnchor = spawnPosition;
        visualRig = ProceduralAnimalVisual.Build(transform, species, profile.visualScale);
        if (visualRig.articulatedBody != null)
        {
            animalBodyBaseScale = visualRig.articulatedBody.localScale;
            animalBodyBasePosition = visualRig.articulatedBody.localPosition;
            tigerBodyBaseScale = animalBodyBaseScale;
            tigerBodyBasePosition = animalBodyBasePosition;
        }
        if (visualRig.tail != null)
            tailBaseRotation = visualRig.tail.localRotation;

        if (profile.canFly)
        {
            SphereCollider birdCollider = gameObject.AddComponent<SphereCollider>();
            birdCollider.radius = Mathf.Max(0.2f, profile.colliderRadius);
            birdCollider.isTrigger = true;
            transform.position = spawnPosition + Vector3.up * (4f + Random.Range(0f, 3f));
            homeAnchor = transform.position;
            state = AnimalState.Flying;
            stateTimer = Random.Range(5f, 9f);
            if (!TryChooseBirdTarget())
                targetPosition = transform.position + transform.forward * 12f;
        }
        else
        {
            characterController = gameObject.AddComponent<CharacterController>();
            characterController.radius = profile.colliderRadius;
            characterController.height = Mathf.Max(profile.colliderHeight, profile.colliderRadius * 2f);
            characterController.center = Vector3.up * (characterController.height * 0.5f);
            characterController.skinWidth = 0.04f;
            characterController.stepOffset = Mathf.Min(0.25f, characterController.height * 0.2f);
            characterController.slopeLimit = 50f;
            state = Random.value < 0.35f ? AnimalState.Rest : AnimalState.Idle;
            stateTimer = Random.Range(1.2f, 4f);
        }

        lastPosition = transform.position;
        initialized = true;
        ActiveAnimals.Add(this);
    }

    private void Update()
    {
        if (!initialized)
            return;

        float deltaTime = Time.deltaTime;
        scanTimer -= deltaTime;
        stateTimer -= deltaTime;
        routineCheckTimer -= deltaTime;
        if (routineCheckTimer <= 0f)
        {
            routineCheckTimer = 1.1f;
            UpdateDailyRoutine();
        }

        if (pondRestored && species != AnimalSpecies.Capybara && (preferredHabitat & AnimalHabitat.NearWater) != 0)
        {
            Vector3 attraction = new Vector3(restoredPondPosition.x, homeAnchor.y, restoredPondPosition.z);
            homeAnchor = Vector3.MoveTowards(homeAnchor, attraction, deltaTime * 0.08f);
        }
        if (scanTimer <= 0f)
        {
            scanTimer = Random.Range(0.35f, 0.65f);
            EvaluateAwareness();
        }

        if (profile.canFly)
            UpdateFlying(deltaTime);
        else
            UpdateGroundMovement(deltaTime);

        Animate(deltaTime);
        lastPosition = transform.position;
    }

    private void UpdateDailyRoutine()
    {
        if (worldClock == null) worldClock = WorldTimeManager.Instance;
        if (weatherManager == null) weatherManager = WeatherManager.Instance;
        bool raining = weatherManager != null && weatherManager.IsRaining;
        bool dog = species == AnimalSpecies.Dog;
        float evening = worldClock != null ? worldClock.EveningFactor : 0f;
        // Individual thresholds spread sheltering and waking across the same
        // in-game evening/dawn window instead of using real-time delays.
        bool wantsShelter = raining || (predator ? evening < eveningRestThreshold : evening >= eveningRestThreshold);
        if (!wantsShelter)
        {
            // Birds may use nests for short daytime rests. Let their flight state
            // machine complete that trip instead of cancelling it on this timer.
            if (profile.canFly && state == AnimalState.ReturnToShelter && restShelter != null)
                return;
            if (state == AnimalState.Sleep || state == AnimalState.ReturnToShelter)
            {
                restShelter = null;
                SetState(AnimalState.Rest, Random.Range(0.8f, 2f));
            }
            return;
        }

        if (state == AnimalState.Sleep || state == AnimalState.ReturnToShelter)
        {
            if (state == AnimalState.Sleep) stateTimer = Mathf.Max(stateTimer, 30f);
            return;
        }
        ShelterPointType shelterType = dog ? ShelterPointType.House : ShelterPoint.BestFor(species, predator);
        restShelter = ShelterPoint.FindNearest(transform.position, shelterType, 150f);
        if (restShelter == null)
        {
            // Keep the animal responsive if the scene has no shelter markers.
            SetState(AnimalState.Sleep, Random.Range(10f, 20f));
            return;
        }
        targetPosition = restShelter.Position;
        SetState(AnimalState.ReturnToShelter, 36000f);
    }

    private void EvaluateAwareness()
    {
        if (profile.canFly)
        {
            bool predatorNearby = FindNearestAnimal(true, detectionRadius) != null;
            bool playerNearby = player != null && Vector3.Distance(transform.position, player.position) < profile.playerNoticeRadius;
            if ((predatorNearby || playerNearby) && (state == AnimalState.Perched || state == AnimalState.Landing))
                SetState(AnimalState.Takeoff, Random.Range(0.25f, 0.55f));
            return;
        }

        if (!predator)
        {
            AnimalMovement threat = FindNearestAnimal(true, detectionRadius);
            if (threat != null)
            {
                Vector3 away = transform.position - threat.transform.position;
                away.y = 0f;
                SetGroundTarget(transform.position + away.normalized * Random.Range(12f, 19f), AnimalState.Flee, Random.Range(3f, 6f));
                return;
            }

            bool domesticDog = species == AnimalSpecies.Dog;
            if (!domesticDog && player != null && Vector3.Distance(transform.position, player.position) < profile.playerNoticeRadius)
            {
                Vector3 away = transform.position - player.position;
                away.y = 0f;
                SetGroundTarget(transform.position + away.normalized * Random.Range(9f, 14f), AnimalState.Flee, Random.Range(2.5f, 4.5f));
            }
            return;
        }

        // This pass keeps the tiger a calm wandering animal; it does not hunt or react to the player.
        if (species == AnimalSpecies.Tiger)
            return;

        if (player != null && Vector3.Distance(transform.position, player.position) < profile.playerNoticeRadius)
        {
            if (Vector3.Distance(transform.position, player.position) < 4f)
            {
                Vector3 away = transform.position - player.position;
                away.y = 0f;
                SetGroundTarget(transform.position + away.normalized * 5f, AnimalState.Alert, 1.8f);
            }
            else
            {
                SetState(AnimalState.Alert, 2f);
            }
            return;
        }

        if (state == AnimalState.Alert || state == AnimalState.Rest || state == AnimalState.Sleep || state == AnimalState.Eat || state == AnimalState.ReturnToShelter)
            return;

        AnimalMovement prey = FindNearestAnimal(false, detectionRadius);
        if (prey != null && Random.value < 0.16f)
        {
            preyTarget = prey;
            targetPosition = prey.transform.position;
            SetState(AnimalState.Chase, Random.Range(3f, 6f));
        }
    }

    private AnimalMovement FindNearestAnimal(bool findPredator, float radius)
    {
        AnimalMovement nearest = null;
        float nearestDistance = radius;
        for (int i = 0; i < ActiveAnimals.Count; i++)
        {
            AnimalMovement candidate = ActiveAnimals[i];
            if (candidate == null || candidate == this || candidate.profile == null)
                continue;
            if (findPredator ? !candidate.predator : (candidate.predator || !candidate.CanBePrey))
                continue;

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }
        return nearest;
    }

    private void UpdateGroundMovement(float deltaTime)
    {
        if (species == AnimalSpecies.Tiger)
        {
            UpdateTigerMovement(deltaTime);
            return;
        }

        if (state == AnimalState.ReturnToShelter)
        {
            Vector3 shelterDirection = targetPosition - transform.position;
            shelterDirection.y = 0f;
            if (shelterDirection.sqrMagnitude < 1.2f)
            {
                SetState(AnimalState.Sleep, 36000f);
                MoveGround(Vector3.zero, deltaTime);
                return;
            }
            float speedFactor = weatherManager != null ? weatherManager.MovementMultiplier : 1f;
            Vector3 beforeMove = transform.position;
            MoveGround(shelterDirection.normalized * movementSpeed * speedFactor, deltaTime);
            if (Vector3.Distance(beforeMove, transform.position) < 0.002f)
                stuckTimer += deltaTime;
            else
                stuckTimer = 0f;
            if (stuckTimer > 1.5f)
            {
                stuckTimer = 0f;
                restShelter = null;
                SetState(AnimalState.Sleep, 36000f);
            }
            return;
        }

        if (state == AnimalState.Idle || state == AnimalState.Rest || state == AnimalState.Sleep || state == AnimalState.Eat)
        {
            if (stateTimer <= 0f)
            {
                if (Random.value < 0.16f)
                    SetState(Random.value < 0.5f ? AnimalState.Eat : AnimalState.Sleep, Random.Range(2f, 5f));
                else if (TryChooseGroundTarget())
                    SetState(AnimalState.Wander, Random.Range(3f, 8f));
                else
                    SetState(AnimalState.Rest, Random.Range(1f, 2.5f));
            }
            MoveGround(Vector3.zero, deltaTime);
            return;
        }

        if (state == AnimalState.Flee && stateTimer <= 0f)
        {
            AnimalMovement threat = FindNearestAnimal(true, detectionRadius);
            if (threat == null)
            {
                SetState(AnimalState.Idle, Random.Range(1f, 3f));
                MoveGround(Vector3.zero, deltaTime);
                return;
            }
            else
                stateTimer = 1f;
        }

        if (state == AnimalState.Chase)
        {
            if (preyTarget == null || !preyTarget.CanBePrey || stateTimer <= 0f)
            {
                preyTarget = null;
                SetState(AnimalState.Rest, Random.Range(1.5f, 3f));
                MoveGround(Vector3.zero, deltaTime);
                return;
            }

            targetPosition = preyTarget.transform.position;
            if (Vector3.Distance(transform.position, targetPosition) < 2.2f)
            {
                preyTarget = null;
                SetState(AnimalState.Rest, Random.Range(2f, 4f));
                MoveGround(Vector3.zero, deltaTime);
                return;
            }
        }

        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 1f || stateTimer <= 0f)
        {
            SetState(AnimalState.Idle, Random.Range(1f, 3.5f));
            MoveGround(Vector3.zero, deltaTime);
            return;
        }

        float speed = state == AnimalState.Flee ? fleeSpeed : (state == AnimalState.Chase ? fleeSpeed * 0.85f : movementSpeed);
        if (weatherManager != null && state != AnimalState.Flee)
            speed *= weatherManager.MovementMultiplier;
        if (predator && worldClock != null && state == AnimalState.Wander)
            speed *= Mathf.Lerp(1f, 0.78f, worldClock.NightFactor);
        MoveGround(direction.normalized * speed, deltaTime);
        float distance = Vector3.Distance(transform.position, lastPosition);
        if (distance < 0.002f)
            stuckTimer += deltaTime;
        else
            stuckTimer = 0f;

        if (stuckTimer > 1.25f)
        {
            stuckTimer = 0f;
            if (state == AnimalState.Flee)
                SetState(AnimalState.Idle, 0.5f);
            else if (TryChooseGroundTarget())
                SetState(AnimalState.Wander, Random.Range(2f, 5f));
        }
    }

    private void UpdateTigerMovement(float deltaTime)
    {
        if (state == AnimalState.ReturnToShelter)
        {
            Vector3 shelterDirection = targetPosition - transform.position;
            shelterDirection.y = 0f;
            if (shelterDirection.sqrMagnitude < 1.2f)
            {
                SetState(AnimalState.Sleep, 36000f);
                MoveGround(Vector3.zero, deltaTime);
            }
            else
            {
                Vector3 beforeMove = transform.position;
                MoveGround(shelterDirection.normalized * movementSpeed, deltaTime);
                if (Vector3.Distance(beforeMove, transform.position) < 0.002f)
                    stuckTimer += deltaTime;
                else
                    stuckTimer = 0f;
                if (stuckTimer > 1.5f)
                {
                    stuckTimer = 0f;
                    restShelter = null;
                    SetState(AnimalState.Sleep, 36000f);
                }
            }
            return;
        }
        if (state == AnimalState.Idle || state == AnimalState.Rest)
        {
            if (stateTimer <= 0f)
            {
                if (Random.value < 0.28f)
                    SetState(AnimalState.Rest, Random.Range(2.2f, 4.8f));
                else if (TryChooseTigerTarget())
                    SetState(AnimalState.Wander, Random.Range(4f, 8f));
                else
                    SetState(AnimalState.Idle, Random.Range(1f, 2f));
            }
            MoveGround(Vector3.zero, deltaTime);
            return;
        }

        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.8f || stateTimer <= 0f)
        {
            SetState(AnimalState.Idle, Random.Range(1.4f, 3.5f));
            MoveGround(Vector3.zero, deltaTime);
            return;
        }

        Vector3 heading = direction.normalized;
        Vector3 lookAhead = transform.position + heading * 1.15f;
        Vector2 nextPoint = new Vector2(lookAhead.x, lookAhead.z);
        if (!environment.IsWithinWorldBounds(nextPoint.x, nextPoint.y, profile.colliderRadius + 1f) ||
            !environment.IsAnimalGroundLocationClear(nextPoint, profile.colliderRadius + 0.7f))
        {
            if (TryChooseTigerTarget())
                SetState(AnimalState.Wander, Random.Range(2.5f, 5f));
            else
                SetState(AnimalState.Rest, Random.Range(1.5f, 2.5f));
            MoveGround(Vector3.zero, deltaTime);
            return;
        }

        MoveGround(heading * movementSpeed, deltaTime);
    }

    private bool TryChooseTigerTarget()
    {
        Vector2 current = new Vector2(transform.position.x, transform.position.z);
        for (int attempt = 0; attempt < 18; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(5f, Mathf.Max(6f, wanderRadius));
            Vector2 candidate = current + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            if (!InsideHabitat(candidate)) continue;
            if ((candidate - new Vector2(homeAnchor.x, homeAnchor.z)).sqrMagnitude > wanderRadius * wanderRadius * 1.7f)
                continue;
            if (!environment.IsWithinWorldBounds(candidate.x, candidate.y, profile.colliderRadius + 1f) ||
                !environment.IsAnimalGroundLocationClear(candidate, profile.colliderRadius + 0.7f))
                continue;

            targetPosition = new Vector3(candidate.x, environment.SampleSurfaceHeight(candidate.x, candidate.y), candidate.y);
            return true;
        }
        return false;
    }

    private void MoveGround(Vector3 horizontalVelocity, float deltaTime)
    {
        if (characterController == null)
            return;

        if (controllerIsGrounded())
            verticalVelocity = -2f;
        else
            verticalVelocity += -22f * deltaTime;

        characterController.Move((horizontalVelocity + Vector3.up * verticalVelocity) * deltaTime);
        if (horizontalVelocity.sqrMagnitude > 0.01f)
        {
            Quaternion rotation = Quaternion.LookRotation(horizontalVelocity.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, deltaTime * 5f);
        }
    }

    private bool controllerIsGrounded()
    {
        return characterController != null && characterController.isGrounded;
    }

    private bool TryChooseGroundTarget()
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(5f, Mathf.Max(6f, wanderRadius));
            Vector2 candidate = new Vector2(homeAnchor.x + Mathf.Cos(angle) * radius, homeAnchor.z + Mathf.Sin(angle) * radius);
            if (!InsideHabitat(candidate)) continue;
            if (!environment.IsAnimalGroundLocationClear(candidate, profile.colliderRadius + 0.7f))
                continue;

            targetPosition = new Vector3(candidate.x, environment.SampleSurfaceHeight(candidate.x, candidate.y), candidate.y);
            return true;
        }
        return false;
    }

    private void SetGroundTarget(Vector3 target, AnimalState newState, float duration)
    {
        Vector2 point = new Vector2(target.x, target.z);
        if (!InsideHabitat(point) || !environment.IsAnimalGroundLocationClear(point, profile.colliderRadius + 0.7f))
        {
            if (!TryChooseGroundTarget())
                return;
        }
        else
        {
            targetPosition = new Vector3(target.x, environment.SampleSurfaceHeight(target.x, target.z), target.z);
        }
        SetState(newState, duration);
    }

    private bool InsideHabitat(Vector2 point)
    {
        return !hasHabitatBoundary || (point - habitatCenter).sqrMagnitude <= habitatRadius * habitatRadius;
    }

    private void UpdateFlying(float deltaTime)
    {
        bool eveningRest = worldClock != null && worldClock.EveningFactor >= eveningRestThreshold;
        bool rainyRest = weatherManager != null && weatherManager.IsRaining;
        if (state == AnimalState.Perched)
        {
            if (eveningRest || rainyRest)
            {
                SetState(AnimalState.Sleep, 36000f);
                return;
            }
            if (stateTimer > 0f) return;
            SetState(AnimalState.Takeoff, 0.7f);
        }
        if (state == AnimalState.Sleep && (worldClock == null || eveningRest || rainyRest))
            return;
        if (state == AnimalState.Sleep)
            SetState(AnimalState.Takeoff, 0.7f);

        bool shouldReturnToNest = eveningRest || rainyRest;
        if (!shouldReturnToNest && state == AnimalState.ReturnToShelter && restShelter != null)
        {
            Vector3 toPerch = restShelter.Position - transform.position;
            if (toPerch.magnitude < 1.4f)
            {
                SetState(AnimalState.Perched, Random.Range(18f, 48f));
                return;
            }
            targetPosition = restShelter.Position;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, movementSpeed * deltaTime);
            if (toPerch.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPerch.normalized, Vector3.up), deltaTime * 1.6f);
            return;
        }
        if (shouldReturnToNest && state != AnimalState.Takeoff)
        {
            if (restShelter == null)
                restShelter = ShelterPoint.FindNearest(transform.position, ShelterPointType.Nest, 500f);
            if (restShelter != null)
            {
                targetPosition = restShelter.Position;
                Vector3 toNest = targetPosition - transform.position;
                if (toNest.magnitude < 1.4f)
                {
                    SetState(eveningRest || rainyRest ? AnimalState.Sleep : AnimalState.Perched,
                        eveningRest || rainyRest ? 36000f : Random.Range(18f, 48f));
                    return;
                }
                SetState(AnimalState.ReturnToShelter, 36000f);
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, movementSpeed * deltaTime);
                if (toNest.sqrMagnitude > 0.01f)
                {
                    Quaternion facing = Quaternion.LookRotation(toNest.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, facing, deltaTime * 1.6f);
                }
                return;
            }
        }

        if (state == AnimalState.ReturnToShelter && restShelter == null)
            SetState(AnimalState.Flying, 3f);
        if (state != AnimalState.Flying && state != AnimalState.Takeoff && state != AnimalState.ReturnToShelter)
            SetState(AnimalState.Takeoff, 0.65f);
        if (state == AnimalState.Takeoff)
        {
            transform.position += Vector3.up * deltaTime * 2.3f;
            if (stateTimer <= 0f)
            {
                if (!TryChooseBirdTarget())
                    targetPosition = transform.position + transform.forward * 12f;
                SetState(AnimalState.Flying, Random.Range(5f, 9f));
            }
            return;
        }

        if (stateTimer <= 0f || Vector3.Distance(transform.position, targetPosition) < 2f)
        {
            if (Random.value < 0.18f)
            {
                restShelter = ShelterPoint.FindNearest(transform.position, ShelterPointType.Nest, 500f);
                if (restShelter != null)
                {
                    targetPosition = restShelter.Position;
                    SetState(AnimalState.ReturnToShelter, Random.Range(12f, 30f));
                    return;
                }
            }
            if (!TryChooseBirdTarget())
                targetPosition = transform.position + transform.forward * 12f;
            SetState(AnimalState.Flying, Random.Range(5f, 9f));
        }

        Vector3 direction = targetPosition - transform.position;
        if (direction.sqrMagnitude > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, movementSpeed * deltaTime);
            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, deltaTime * 1.6f);
        }
    }

    private bool TryChooseBirdTarget()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(8f, Mathf.Max(10f, wanderRadius));
            float x = homeAnchor.x + Mathf.Cos(angle) * radius;
            float z = homeAnchor.z + Mathf.Sin(angle) * radius;
            if (!environment.IsWithinWorldBounds(x, z))
                continue;
            targetPosition = new Vector3(x, environment.SampleSurfaceHeight(x, z) + Random.Range(7f, 15f), z);
            return true;
        }
        return false;
    }

    private void SetState(AnimalState newState, float duration)
    {
        state = newState;
        stateTimer = duration;
    }

    private void Animate(float deltaTime)
    {
        if (visualRig == null || visualRig.root == null)
            return;

        bool moving = state == AnimalState.Wander || state == AnimalState.Flee || state == AnimalState.Chase || state == AnimalState.Flying || state == AnimalState.Takeoff || state == AnimalState.ReturnToShelter;
        float gaitSpeed = species == AnimalSpecies.Tiger
            ? (moving ? 5f : 1.5f)
            : (state == AnimalState.Flee || state == AnimalState.Chase || state == AnimalState.Flying ? 13f : 7f);
        gaitPhase += deltaTime * (moving ? gaitSpeed : 1.5f);
        float activity = moving ? 1f : 0.18f;
        float birdWingPhase = Time.time * 16f;
        float bob = state == AnimalState.Flying || state == AnimalState.Perched
            ? Mathf.Sin(birdWingPhase) * 0.055f
            : Mathf.Sin(gaitPhase * 2f) * (moving ? 0.055f : 0.012f);
        visualRig.root.localPosition = species == AnimalSpecies.Deer || profile.canFly ? Vector3.up * bob : Vector3.zero;

        if (species == AnimalSpecies.Tiger)
            AnimateTiger(moving);
        else if (visualRig.articulatedLegs.Count > 0)
            AnimateQuadruped(moving);

        for (int i = 0; species != AnimalSpecies.Tiger && i < visualRig.legs.Count; i++)
        {
            float phase = gaitPhase + ((i % 2 == 0) ? 0f : Mathf.PI);
            float amplitude = state == AnimalState.Flee || state == AnimalState.Chase ? 36f : 24f;
            visualRig.legs[i].localRotation = Quaternion.Euler(Mathf.Sin(phase) * amplitude * activity, 0f, 0f);
        }

        for (int i = 0; i < visualRig.wings.Count; i++)
        {
            float side = i == 0 ? -1f : 1f;
            float flap = state == AnimalState.Flying || state == AnimalState.Takeoff
                ? Mathf.Sin(birdWingPhase) * 58f
                : Mathf.Sin(Time.time * 2f + i) * 6f;
            visualRig.wings[i].localRotation = Quaternion.Euler(0f, 0f, side * flap);
        }

        if (profile.canFly)
        {
            float bank = 0f;
            if (state == AnimalState.Flying)
            {
                Vector3 desired = targetPosition - transform.position;
                desired.y = 0f;
                if (desired.sqrMagnitude > 0.01f)
                    bank = -Mathf.Clamp(Vector3.SignedAngle(transform.forward, desired.normalized, Vector3.up), -35f, 35f) * 0.45f;
            }
            visualRig.root.localRotation = Quaternion.Slerp(visualRig.root.localRotation,
                Quaternion.Euler(0f, 0f, bank), deltaTime * 3f);
        }

        if (species == AnimalSpecies.Snake && visualRig.snakeBodyMesh != null && visualRig.snakeAnimatedVertices != null)
        {
            Vector3[] baseVertices = visualRig.snakeBodyVertices;
            Vector3[] animated = visualRig.snakeAnimatedVertices;
            float amplitude = moving ? 0.135f : 0.028f;
            for (int i = 0; i < baseVertices.Length; i++)
            {
                Vector3 vertex = baseVertices[i];
                float along = Mathf.Clamp01((0.82f - vertex.z) / 2.32f);
                float phase = Time.time * 2.25f - along * 6.2f;
                vertex.x += Mathf.Sin(phase) * amplitude * (0.12f + along * 0.88f);
                animated[i] = vertex;
            }
            visualRig.snakeBodyMesh.vertices = animated;
            visualRig.snakeBodyMesh.RecalculateNormals();
            visualRig.snakeBodyMesh.RecalculateBounds();
        }

        if (visualRig.tail != null)
            visualRig.tail.localRotation = tailBaseRotation * Quaternion.Euler(0f, Mathf.Sin(Time.time * (moving ? 6f : 1.6f)) * (moving ? 18f : 8f), 0f);
    }

    private void AnimateTiger(bool moving)
    {
        float time = Time.time;
        float breath = Mathf.Sin(time * (moving ? 3.3f : 1.7f));
        if (visualRig.tigerBody != null)
        {
            visualRig.tigerBody.localScale = new Vector3(tigerBodyBaseScale.x,
                tigerBodyBaseScale.y * (1f + breath * (moving ? 0.009f : 0.014f)), tigerBodyBaseScale.z);
            visualRig.tigerBody.localPosition = tigerBodyBasePosition + Vector3.up * (breath * (moving ? 0.008f : 0.014f));
        }

        if (visualRig.tigerHead != null)
        {
            float look = Mathf.Sin(time * (moving ? 1.5f : 0.8f));
            visualRig.tigerHead.localRotation = Quaternion.Euler(Mathf.Sin(time * 1.1f) * 2.2f,
                look * (moving ? 2.5f : 5f), Mathf.Sin(time * 0.9f) * 1.2f);
        }

        for (int i = 0; i < visualRig.tigerLegs.Count; i++)
        {
            AnimalTigerLegRig leg = visualRig.tigerLegs[i];
            bool diagonalPair = i == 0 || i == 3;
            float phase = gaitPhase + (diagonalPair ? 0f : Mathf.PI);
            float swing = moving ? Mathf.Sin(phase) : Mathf.Sin(time * 1.7f + i) * 0.025f;
            leg.upper.localRotation = Quaternion.Euler(swing * (moving ? 20f : 1f), 0f, 0f);
            leg.lower.localRotation = Quaternion.Euler(moving ? Mathf.Max(0f, -swing) * 24f : 0f, 0f, 0f);
        }

        for (int i = 0; i < visualRig.tigerTailSegments.Count; i++)
        {
            float delay = time * (moving ? 2.2f : 1.15f) - i * 0.52f;
            visualRig.tigerTailSegments[i].localRotation = Quaternion.Euler(
                Mathf.Sin(delay) * (moving ? 3f : 2f), Mathf.Sin(delay) * (moving ? 11f : 7f), 0f);
        }
    }

    private void AnimateQuadruped(bool moving)
    {
        float time = Time.time;
        float breath = Mathf.Sin(time * (moving ? 3.1f : 1.6f));
        if (visualRig.articulatedBody != null)
        {
            visualRig.articulatedBody.localScale = new Vector3(animalBodyBaseScale.x,
                animalBodyBaseScale.y * (1f + breath * (moving ? 0.008f : 0.014f)), animalBodyBaseScale.z);
            visualRig.articulatedBody.localPosition = animalBodyBasePosition + Vector3.up * breath * (moving ? 0.006f : 0.012f);
        }

        if (visualRig.articulatedHead != null)
        {
            float look = Mathf.Sin(time * (moving ? 1.4f : 0.72f));
            visualRig.articulatedHead.localRotation = Quaternion.Euler(Mathf.Sin(time * 0.9f) * 1.6f,
                look * (moving ? 2f : 4.5f), Mathf.Sin(time * 0.7f) * 1.1f);
        }

        for (int i = 0; i < visualRig.articulatedLegs.Count; i++)
        {
            AnimalTigerLegRig leg = visualRig.articulatedLegs[i];
            bool diagonalPair = i == 0 || i == 3;
            float phase = gaitPhase + (diagonalPair ? 0f : Mathf.PI);
            float swing = moving ? Mathf.Sin(phase) : Mathf.Sin(time * 1.4f + i) * 0.025f;
            float stride = species == AnimalSpecies.Rabbit ? 26f : 18f;
            leg.upper.localRotation = Quaternion.Euler(swing * stride, 0f, 0f);
            leg.lower.localRotation = Quaternion.Euler(moving ? Mathf.Max(0f, -swing) * 22f : 0f, 0f, 0f);
        }

        for (int i = 0; i < visualRig.articulatedTailSegments.Count; i++)
        {
            float delay = time * (moving ? 2.2f : 1.1f) - i * 0.55f;
            visualRig.articulatedTailSegments[i].localRotation = Quaternion.Euler(
                Mathf.Sin(delay) * (moving ? 2.5f : 1.5f), Mathf.Sin(delay) * (moving ? 8f : 5f), 0f);
        }
    }

    private void OnDestroy()
    {
        ActiveAnimals.Remove(this);
    }
}
