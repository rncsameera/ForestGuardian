using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Builds the first lightweight, deterministic ForestGuardian exploration space.</summary>
public sealed class ForestEnvironmentGenerator : MonoBehaviour
{
    private static readonly Color[] StoneTintPalette =
    {
        new Color(0.68f, 0.70f, 0.67f),
        new Color(0.62f, 0.57f, 0.48f),
        new Color(0.56f, 0.59f, 0.56f),
        new Color(0.73f, 0.70f, 0.62f)
    };
    private static readonly float[] ClearingRadii = { 11f, 15f, 17f, 11f, 11f, 13f, 19f, 23f, 19f, 17f, 12f, 12f, 16f };

    [Header("Scene objects")]
    [SerializeField] private GameObject terrainObject;
    [SerializeField] private Transform treesRoot;
    [SerializeField] private Transform vegetationRoot;
    [SerializeField] private Transform rocksRoot;
    [SerializeField] private Transform pathsRoot;
    [SerializeField] private Transform cloudsRoot;
    [SerializeField] private Transform pondsRoot;

    [Header("World dimensions")]
    [SerializeField] private float worldWidth = 520f;
    [SerializeField] private float worldDepth = 560f;
    [SerializeField] private float worldStartZ = -25f;
    [SerializeField] private int terrainCells = 208;
    [SerializeField] private int randomSeed = 61427;

    private readonly List<Mesh> generatedMeshes = new List<Mesh>();
    private readonly List<Transform> cloudInstances = new List<Transform>();
    private readonly List<Vector2> cloudDriftVelocities = new List<Vector2>();
    private readonly List<PondWaterMotion> pondWaterMotions = new List<PondWaterMotion>();
    private Mesh bushClusterMesh;
    private Mesh fernClusterMesh;
    private Material terrainMaterial;
    private Material pathMaterial;
    private Material barkMaterial;
    private Material leafMaterial;
    private Material deepLeafMaterial;
    private Material brightLeafMaterial;
    private Material bushMaterial;
    private Material rockMaterial;
    private Material grassMaterial;
    private Material fernMaterial;
    private Material woodMaterial;
    private Material roofMaterial;
    private Material stoneFoundationMaterial;
    private Material windowMaterial;
    private Material cloudMaterial;
    private Material deepWaterMaterial;
    private Material shallowWaterMaterial;
    private Material originalSkybox;
    private Material forestSkybox;
    private bool replacedSkybox;
    private Transform guardianTransform;
    private Vector2[] mainPath;
    private Vector2[][] secondaryPaths;
    private Vector2[] clearings;
    private PondDefinition[] ponds;
    private int inspectableBushCount;

    public float WorldWidth => worldWidth;
    public float WorldDepth => worldDepth;
    public float WorldStartZ => worldStartZ;
    public int PondCount => ponds != null ? ponds.Length : 0;
    public int ClearingCount => clearings != null ? clearings.Length : 0;
    public Vector2 HouseCenter => new Vector2(34f, 52f);
    public IReadOnlyList<Vector2> MainTrailPoints => mainPath;
    public IReadOnlyList<Vector2[]> SecondaryTrailPoints => secondaryPaths;
    public IReadOnlyList<Vector2> ForestClearingCenters => clearings;

    public void GetLargeRockPositions(List<Vector2> destination, float minimumSize = 1.3f)
    {
        if (destination == null || rocksRoot == null) return;
        for (int i = 0; i < rocksRoot.childCount; i++)
        {
            Transform rock = rocksRoot.GetChild(i);
            if (Mathf.Max(rock.lossyScale.x, rock.lossyScale.z) >= minimumSize)
                destination.Add(new Vector2(rock.position.x, rock.position.z));
        }
    }

    public bool TryFindPondShorePoint(int pondIndex, float angleRadians, float minOffset, float maxOffset, float clearance, out Vector2 point)
    {
        if (ponds == null || pondIndex < 0 || pondIndex >= ponds.Length)
        {
            point = Vector2.zero;
            return false;
        }

        PondDefinition pond = ponds[pondIndex];
        float outlineAngle = angleRadians;
        float outline = PondOutlineFactor(outlineAngle, pond.Phase);
        Vector2 local = new Vector2(Mathf.Cos(angleRadians) * pond.RadiusX * outline,
            Mathf.Sin(angleRadians) * pond.RadiusZ * outline);
        float rotation = pond.Rotation * Mathf.Deg2Rad;
        Vector2 rotated = new Vector2(Mathf.Cos(rotation) * local.x + Mathf.Sin(rotation) * local.y,
            -Mathf.Sin(rotation) * local.x + Mathf.Cos(rotation) * local.y);
        Vector2 center = pond.Center + rotated;
        Vector2 outward = new Vector2(Mathf.Cos(rotation) * Mathf.Cos(angleRadians) * pond.RadiusX + Mathf.Sin(rotation) * Mathf.Sin(angleRadians) * pond.RadiusZ,
            -Mathf.Sin(rotation) * Mathf.Cos(angleRadians) * pond.RadiusX + Mathf.Cos(rotation) * Mathf.Sin(angleRadians) * pond.RadiusZ).normalized;

        const int tries = 8;
        for (int i = 0; i < tries; i++)
        {
            float t = tries == 1 ? 0f : i / (tries - 1f);
            float offset = Mathf.Lerp(minOffset, maxOffset, t);
            Vector2 candidate = center + outward * offset;
            if (IsAnimalGroundLocationClear(candidate, clearance))
            {
                point = candidate;
                return true;
            }
        }

        point = center + outward * maxOffset;
        return false;
    }

    private sealed class PondDefinition
    {
        public string Name;
        public Vector2 Center;
        public float RadiusX;
        public float RadiusZ;
        public float Rotation;
        public float Depth;
        public float Phase;
        public float WaterHeight;
    }

    private sealed class PondWaterMotion
    {
        public Mesh Mesh;
        public Vector3[] BaseVertices;
        public Vector3[] AnimatedVertices;
        public float RadiusX;
        public float RadiusZ;
        public float Phase;
    }

    private void Awake()
    {
        if (!ReferencesAreValid())
            return;

        GameObject guardian = GameObject.Find("Guardian");
        guardianTransform = guardian != null ? guardian.transform : null;
        BuildLayout();
        CreateMaterials();
        BuildTerrain();
        BuildPonds();
        BuildPaths();
        BuildGuardianShelter();
        BuildTrees();
        BuildVegetation();
        BuildRocks();
        BuildForestDetails();
        BuildClouds();
        SetForestAtmosphere();
        PositionGuardianAtShelter();
        Physics.SyncTransforms();

    }

    private bool ReferencesAreValid()
    {
        if (terrainObject != null && treesRoot != null && vegetationRoot != null && rocksRoot != null && pathsRoot != null)
            return true;

        Debug.LogError("ForestEnvironmentGenerator needs terrain and all four environment roots assigned.", this);
        enabled = false;
        return false;
    }

    private void BuildLayout()
    {
        mainPath = new[]
        {
            new Vector2(0f, -20f), new Vector2(0f, 7f), new Vector2(-3f, 24f),
            new Vector2(1f, 47f), new Vector2(7f, 67f), new Vector2(2f, 91f),
            new Vector2(-2f, 115f), new Vector2(5f, 139f), new Vector2(0f, 174f),
            new Vector2(5f, 205f), new Vector2(13f, 235f), new Vector2(4f, 266f),
            new Vector2(-3f, 299f), new Vector2(2f, 333f), new Vector2(0f, 360f)
        };

        secondaryPaths = new[]
        {
            new[] { new Vector2(1f, 47f), new Vector2(20f, 56f), new Vector2(37f, 72f), new Vector2(48f, 91f), new Vector2(54f, 112f), new Vector2(75f, 132f), new Vector2(94f, 151f), new Vector2(92f, 161f) },
            new[] { new Vector2(3f, 88f), new Vector2(-17f, 86f), new Vector2(-34f, 97f), new Vector2(-49f, 116f), new Vector2(-59f, 140f), new Vector2(-82f, 170f), new Vector2(-111f, 193f), new Vector2(-124f, 219f) },
            new[] { new Vector2(-2f, 115f), new Vector2(-19f, 132f), new Vector2(-27f, 153f), new Vector2(-30f, 174f), new Vector2(-54f, 193f), new Vector2(-75f, 218f), new Vector2(-94f, 242f) },
            new[] { new Vector2(4f, 266f), new Vector2(31f, 276f), new Vector2(55f, 288f), new Vector2(68f, 286f) },
            new[] { new Vector2(2f, 333f), new Vector2(-25f, 345f), new Vector2(-48f, 342f), new Vector2(-54f, 330f) },
            new[] { new Vector2(-3f, 299f), new Vector2(-34f, 286f), new Vector2(-62f, 289f), new Vector2(-82f, 297f) },
            new[] { new Vector2(13f, 235f), new Vector2(25f, 224f), new Vector2(42f, 224f), new Vector2(57f, 231f) },
            new[] { new Vector2(1f, 47f), new Vector2(14f, 55f), new Vector2(27f, 55f), new Vector2(32f, 49f) }
        };

        clearings = new[]
        {
            new Vector2(0f, 0f), new Vector2(0f, 43f), new Vector2(1f, 93f),
            new Vector2(49f, 94f), new Vector2(-48f, 116f), new Vector2(-30f, 162f),
            new Vector2(112f, 166f), new Vector2(-132f, 238f), new Vector2(86f, 300f),
            new Vector2(-74f, 337f), new Vector2(45f, 228f), new Vector2(-95f, 300f),
            new Vector2(34f, 52f)
        };

        ponds = new[]
        {
            new PondDefinition { Name = "East Clearing Pond", Center = new Vector2(112f, 166f), RadiusX = 17f, RadiusZ = 12f, Rotation = -18f, Depth = 0.8f, Phase = 0.4f },
            new PondDefinition { Name = "Westwood Pond", Center = new Vector2(-132f, 238f), RadiusX = 19f, RadiusZ = 13f, Rotation = 22f, Depth = 0.9f, Phase = 1.8f },
            new PondDefinition { Name = "Fern Hollow Pond", Center = new Vector2(86f, 300f), RadiusX = 18f, RadiusZ = 12f, Rotation = 31f, Depth = 0.75f, Phase = 3.1f },
            new PondDefinition { Name = "North Glade Pond", Center = new Vector2(-74f, 337f), RadiusX = 15f, RadiusZ = 10f, Rotation = -27f, Depth = 0.75f, Phase = 4.6f }
        };

        foreach (PondDefinition pond in ponds)
            pond.WaterHeight = SampleBaseTerrainHeight(pond.Center.x, pond.Center.y) - 0.22f;
    }

    public float SampleSurfaceHeight(float x, float z)
    {
        return SampleHeight(x, z);
    }

    public float GetGroundHeight(float x, float z)
    {
        MeshCollider groundCollider = terrainObject != null ? terrainObject.GetComponent<MeshCollider>() : null;
        if (groundCollider != null && groundCollider.enabled)
        {
            Ray ray = new Ray(new Vector3(x, 1000f, z), Vector3.down);
            RaycastHit hit;
            if (groundCollider.Raycast(ray, out hit, 2000f))
                return hit.point.y;
        }

        // SampleHeight uses the same function that produced the runtime ground mesh.
        return SampleHeight(x, z);
    }

    public bool IsWithinWorldBounds(float x, float z, float margin = 2f)
    {
        return Mathf.Abs(x) <= worldWidth * 0.5f - margin &&
               z >= worldStartZ + margin && z <= worldStartZ + worldDepth - margin;
    }

    public Vector2 GetPondCenter(int index)
    {
        return ponds[index].Center;
    }

    public Vector2 GetPondRadii(int index)
    {
        return new Vector2(ponds[index].RadiusX, ponds[index].RadiusZ);
    }

    public Vector2 GetClearingCenter(int index)
    {
        return clearings[index];
    }

    public bool IsAnimalGroundLocationClear(Vector2 point, float clearanceRadius)
    {
        if (!IsWithinWorldBounds(point.x, point.y, clearanceRadius + 1f))
            return false;
        if (PathDistance(point.x, point.y) < 1.2f || IsNearTree(point.x, point.y, clearanceRadius, treesRoot))
            return false;

        for (int i = 0; i < ponds.Length; i++)
        {
            if (PondNormalizedDistance(point.x, point.y, ponds[i]) < 1.08f)
                return false;
        }

        for (int i = 0; i < rocksRoot.childCount; i++)
        {
            Transform rock = rocksRoot.GetChild(i);
            Vector2 rockPosition = new Vector2(rock.localPosition.x, rock.localPosition.z);
            float rockRadius = Mathf.Max(rock.lossyScale.x, rock.lossyScale.z) * 0.48f;
            if ((point - rockPosition).sqrMagnitude < (clearanceRadius + rockRadius) * (clearanceRadius + rockRadius))
                return false;
        }

        Vector2 houseOffset = point - HouseCenter;
        if (Mathf.Abs(houseOffset.x) < 6f + clearanceRadius && Mathf.Abs(houseOffset.y) < 5.5f + clearanceRadius)
            return false;

        return true;
    }

    private void CreateMaterials()
    {
        terrainMaterial = CreateMaterial("Forest Ground", new Color(0.24f, 0.34f, 0.20f));
        pathMaterial = CreateMaterial("Forest Path", new Color(0.39f, 0.30f, 0.18f));
        barkMaterial = CreateMaterial("Tree Bark", new Color(0.24f, 0.15f, 0.09f));
        leafMaterial = CreateMaterial("Canopy Green", new Color(0.12f, 0.29f, 0.16f));
        deepLeafMaterial = CreateMaterial("Deep Canopy Green", new Color(0.075f, 0.19f, 0.11f));
        brightLeafMaterial = CreateMaterial("Sunlit Canopy Green", new Color(0.18f, 0.32f, 0.14f));
        bushMaterial = CreateMaterial("Understory Green", new Color(0.18f, 0.35f, 0.19f));
        rockMaterial = CreateMaterial("Forest Stone", new Color(0.31f, 0.34f, 0.30f));
        grassMaterial = CreateMaterial("Forest Grass", new Color(0.34f, 0.43f, 0.22f));
        fernMaterial = CreateMaterial("Fern Green", new Color(0.16f, 0.31f, 0.15f));
        woodMaterial = CreateMaterial("Guardian Shelter Wood", new Color(0.34f, 0.20f, 0.11f));
        roofMaterial = CreateMaterial("Guardian Shelter Roof", new Color(0.17f, 0.20f, 0.14f));
        stoneFoundationMaterial = CreateMaterial("Shelter Foundation Stone", new Color(0.30f, 0.31f, 0.27f));
        windowMaterial = CreateMaterial("Shelter Window Glass", new Color(0.30f, 0.48f, 0.53f));
        deepWaterMaterial = CreateMaterial("Pond Deep Water", new Color(0.09f, 0.29f, 0.37f));
        shallowWaterMaterial = CreateMaterial("Pond Shallows", new Color(0.16f, 0.34f, 0.40f));

        ConfigureMatteFoliage(leafMaterial);
        ConfigureMatteFoliage(deepLeafMaterial);
        ConfigureMatteFoliage(brightLeafMaterial);
        ConfigureMatteFoliage(bushMaterial);
        ConfigureMatteFoliage(fernMaterial);
        ConfigureMatteSurface(rockMaterial, 0.16f);
        ConfigureMatteSurface(woodMaterial, 0.18f);
        ConfigureMatteSurface(roofMaterial, 0.08f);
        ConfigureMatteSurface(stoneFoundationMaterial, 0.12f);
        ConfigureMatteSurface(windowMaterial, 0.08f);
        ConfigureWaterSurface(deepWaterMaterial, 0.22f);
        ConfigureWaterSurface(shallowWaterMaterial, 0.18f);
        CreateCloudMaterial();
    }

    private static void ConfigureMatteSurface(Material material, float smoothness)
    {
        if (material == null)
            return;
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_SpecularHighlights"))
        {
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        }
    }

    private static void ConfigureWaterSurface(Material material, float smoothness)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_SpecularHighlights"))
        {
            material.SetFloat("_SpecularHighlights", 1f);
            material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
        }
        if (material.HasProperty("_Surface"))
        {
            // Transparent water: the player can see the shallow ground and wildlife below the surface.
            material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_EMISSION");
        }
    }

    private void CreateCloudMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogWarning("No supported lit shader was found; clouds will not be generated.", this);
            return;
        }

        cloudMaterial = new Material(shader)
        {
            name = "Matte Forest Clouds",
            color = new Color(0.78f, 0.81f, 0.84f, 1f),
            renderQueue = (int)RenderQueue.Geometry
        };
        if (cloudMaterial.HasProperty("_Metallic"))
            cloudMaterial.SetFloat("_Metallic", 0f);
        if (cloudMaterial.HasProperty("_Smoothness"))
            cloudMaterial.SetFloat("_Smoothness", 0.12f);
        if (cloudMaterial.HasProperty("_SpecularHighlights"))
        {
            cloudMaterial.SetFloat("_SpecularHighlights", 0f);
            cloudMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        }
        if (cloudMaterial.HasProperty("_Cull"))
            cloudMaterial.SetFloat("_Cull", (float)CullMode.Back);
        if (cloudMaterial.HasProperty("_EmissionColor"))
        {
            cloudMaterial.SetColor("_EmissionColor", Color.black);
            cloudMaterial.DisableKeyword("_EMISSION");
        }
    }

    private void BuildClouds()
    {
        if (cloudMaterial == null)
            return;

        if (cloudsRoot == null)
        {
            GameObject root = new GameObject("Clouds");
            cloudsRoot = root.transform;
            cloudsRoot.SetParent(transform, false);
        }

        Mesh sphere = GetPrimitiveMesh(PrimitiveType.Sphere);
        if (sphere == null)
            return;

        System.Random random = new System.Random(randomSeed + 73);
        const int cloudCount = 42;
        float centerZ = worldStartZ + worldDepth * 0.5f;
        for (int i = 0; i < cloudCount; i++)
        {
            int distanceLayer = i % 10 < 3 ? 0 : (i % 10 < 7 ? 1 : 2);
            float layerSpread = distanceLayer == 0 ? 0.72f : (distanceLayer == 1 ? 1.35f : 2.15f);
            float x = (float)(random.NextDouble() * 2f - 1f) * worldWidth * 0.62f * layerSpread;
            float z = centerZ + (float)(random.NextDouble() * 2f - 1f) * worldDepth * 0.70f * layerSpread;
            float altitude = distanceLayer == 0
                ? 48f + (float)random.NextDouble() * 12f
                : (distanceLayer == 1 ? 61f + (float)random.NextDouble() * 18f : 76f + (float)random.NextDouble() * 23f);
            float size = distanceLayer == 0
                ? 1.25f + (float)random.NextDouble() * 0.55f
                : (distanceLayer == 1 ? 0.82f + (float)random.NextDouble() * 0.43f : 0.38f + (float)random.NextDouble() * 0.28f);
            int variation = random.Next(6);
            Mesh cloudMesh = BuildCloudFormation(sphere, random, distanceLayer, variation);
            GameObject cloud = new GameObject("Drifting Cloud");
            cloud.transform.SetParent(cloudsRoot, false);
            cloud.transform.localPosition = new Vector3(x, altitude, z);
            cloud.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            cloud.transform.localScale = Vector3.one * size;
            cloud.AddComponent<MeshFilter>().sharedMesh = cloudMesh;
            MeshRenderer renderer = cloud.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = cloudMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            MaterialPropertyBlock tint = new MaterialPropertyBlock();
            Color[] palette =
            {
                new Color(0.78f, 0.81f, 0.84f),
                new Color(0.84f, 0.83f, 0.79f),
                new Color(0.81f, 0.85f, 0.88f),
                new Color(0.88f, 0.87f, 0.83f)
            };
            Color cloudColor = Color.Lerp(palette[random.Next(palette.Length)], Color.white, (float)random.NextDouble() * 0.12f);
            int colorProperty = cloudMaterial.HasProperty("_BaseColor") ? Shader.PropertyToID("_BaseColor") : Shader.PropertyToID("_Color");
            tint.SetColor(colorProperty, cloudColor);
            renderer.SetPropertyBlock(tint);
            cloudInstances.Add(cloud.transform);
            float speed = distanceLayer == 0 ? 0.22f : (distanceLayer == 1 ? 0.13f : 0.065f);
            float driftSpeed = speed + (float)random.NextDouble() * speed * 0.18f;
            float driftAngle = ((float)random.NextDouble() * 24f - 12f) * Mathf.Deg2Rad;
            cloudDriftVelocities.Add(new Vector2(Mathf.Cos(driftAngle), Mathf.Sin(driftAngle)) * driftSpeed);
        }
    }

    private Mesh BuildCloudFormation(Mesh puffMesh, System.Random random, int distanceLayer, int variation)
    {
        int upperPuffCount = distanceLayer == 0
            ? 5 + random.Next(5)
            : (distanceLayer == 1 ? 3 + random.Next(4) : 1 + random.Next(3));
        int instanceCount = upperPuffCount + 1;
        Mesh[] sources = new Mesh[instanceCount];
        Matrix4x4[] matrices = new Matrix4x4[instanceCount];
        float width = variation == 2 ? 13f : (variation == 1 ? 8f : 10f);
        float depth = variation == 4 ? 3.1f : (variation == 5 ? 5.2f : 4.2f);
        float baseHeight = variation == 4 ? 1.45f : 2.1f;

        // A broad, flattened lower mass joins the puffs into one readable formation.
        sources[0] = puffMesh;
        matrices[0] = Matrix4x4.TRS(new Vector3(0f, 0f, 0f), Quaternion.identity,
            new Vector3(width, baseHeight, depth));

        for (int i = 0; i < upperPuffCount; i++)
        {
            sources[i + 1] = puffMesh;
            float spread = width * 0.37f;
            float x = (float)(random.NextDouble() * 2f - 1f) * spread;
            float z = (float)(random.NextDouble() * 2f - 1f) * depth * 0.25f;
            float y = 0.55f + (float)random.NextDouble() * (variation == 1 ? 3.1f : 2.2f);
            float puffWidth = (2.8f + (float)random.NextDouble() * 3.2f) * (variation == 2 ? 0.88f : 1f);
            float puffHeight = 2.8f + (float)random.NextDouble() * 2.5f;
            float puffDepth = 2.7f + (float)random.NextDouble() * 2.3f;
            if (variation == 3)
                puffWidth *= 0.82f;
            matrices[i + 1] = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.identity,
                new Vector3(puffWidth, puffHeight, puffDepth));
        }

        return CombineMesh("Cloud Formation Mesh", sources, matrices);
    }

    private void Update()
    {
        float delta = Time.deltaTime;
        float wrapX = worldWidth * 0.62f * 2.15f;
        float wrapZ = worldDepth * 0.70f * 2.15f;
        float centerZ = worldStartZ + worldDepth * 0.5f;
        for (int i = 0; i < cloudInstances.Count; i++)
        {
            Transform cloud = cloudInstances[i];
            if (cloud == null)
                continue;

            Vector3 position = cloud.localPosition;
            position.x += cloudDriftVelocities[i].x * delta;
            position.z += cloudDriftVelocities[i].y * delta;
            if (position.x > wrapX)
                position.x = -wrapX;
            else if (position.x < -wrapX)
                position.x = wrapX;
            if (position.z > centerZ + wrapZ)
                position.z = centerZ - wrapZ;
            else if (position.z < centerZ - wrapZ)
                position.z = centerZ + wrapZ;
            cloud.localPosition = position;
        }

        float time = Time.time;
        for (int i = 0; i < pondWaterMotions.Count; i++)
        {
            PondWaterMotion motion = pondWaterMotions[i];
            for (int vertex = 0; vertex < motion.BaseVertices.Length; vertex++)
            {
                Vector3 baseVertex = motion.BaseVertices[vertex];
                float radial = Mathf.Clamp01(new Vector2(baseVertex.x / motion.RadiusX, baseVertex.z / motion.RadiusZ).magnitude);
                float wave = Mathf.Sin(baseVertex.x * 0.38f + baseVertex.z * 0.22f + time * 0.65f + motion.Phase) * 0.014f;
                wave += Mathf.Sin(-baseVertex.x * 0.19f + baseVertex.z * 0.31f - time * 0.42f + motion.Phase) * 0.009f;
                motion.AnimatedVertices[vertex] = new Vector3(baseVertex.x, baseVertex.y + wave * (1f - radial * 0.65f), baseVertex.z);
            }
            motion.Mesh.vertices = motion.AnimatedVertices;
            motion.Mesh.RecalculateNormals();
            motion.Mesh.RecalculateBounds();
        }
    }

    private void ConfigureSky()
    {
        Shader shader = Shader.Find("Skybox/Procedural");
        if (shader == null)
            return;

        originalSkybox = RenderSettings.skybox;
        forestSkybox = new Material(shader) { name = "ForestGuardian Soft Sky" };
        if (forestSkybox.HasProperty("_SkyTint"))
            forestSkybox.SetColor("_SkyTint", new Color(0.58f, 0.72f, 0.84f));
        if (forestSkybox.HasProperty("_GroundColor"))
            forestSkybox.SetColor("_GroundColor", new Color(0.42f, 0.50f, 0.43f));
        if (forestSkybox.HasProperty("_AtmosphereThickness"))
            forestSkybox.SetFloat("_AtmosphereThickness", 0.82f);
        if (forestSkybox.HasProperty("_Exposure"))
            forestSkybox.SetFloat("_Exposure", 0.85f);
        if (forestSkybox.HasProperty("_SunSize"))
            forestSkybox.SetFloat("_SunSize", 0.025f);
        RenderSettings.skybox = forestSkybox;
        replacedSkybox = true;
    }

    private static void ConfigureMatteFoliage(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.08f);
        if (material.HasProperty("_SpecularHighlights"))
        {
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        }
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);
    }

    private Material CreateMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
        {
            Debug.LogError("No supported shader was found while creating " + materialName + ".", this);
            return null;
        }

        Material material = new Material(shader) { name = materialName, color = color };
        return material;
    }

    private void BuildTerrain()
    {
        Mesh mesh = new Mesh { name = "Forest Rolling Ground" };
        mesh.indexFormat = IndexFormat.UInt32;

        int side = terrainCells + 1;
        Vector3[] vertices = new Vector3[side * side];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[terrainCells * terrainCells * 6];

        for (int z = 0; z < side; z++)
        {
            float worldZ = worldStartZ + worldDepth * z / terrainCells;
            for (int x = 0; x < side; x++)
            {
                float worldX = -worldWidth * 0.5f + worldWidth * x / terrainCells;
                int index = z * side + x;
                vertices[index] = new Vector3(worldX, SampleHeight(worldX, worldZ), worldZ);
                uvs[index] = new Vector2(x / (float)terrainCells * 12f, z / (float)terrainCells * 12f);
            }
        }

        int triangle = 0;
        for (int z = 0; z < terrainCells; z++)
        {
            for (int x = 0; x < terrainCells; x++)
            {
                int bottomLeft = z * side + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + side;
                int topRight = topLeft + 1;
                triangles[triangle++] = bottomLeft;
                triangles[triangle++] = topLeft;
                triangles[triangle++] = bottomRight;
                triangles[triangle++] = bottomRight;
                triangles[triangle++] = topLeft;
                triangles[triangle++] = topRight;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        generatedMeshes.Add(mesh);

        MeshFilter filter = terrainObject.GetComponent<MeshFilter>();
        MeshCollider collider = terrainObject.GetComponent<MeshCollider>();
        MeshRenderer renderer = terrainObject.GetComponent<MeshRenderer>();
        if (filter == null || collider == null || renderer == null)
        {
            Debug.LogError("The Ground object needs its existing MeshFilter, MeshCollider, and MeshRenderer.", terrainObject);
            enabled = false;
            return;
        }

        filter.sharedMesh = mesh;
        collider.sharedMesh = null;
        collider.sharedMesh = mesh;
        renderer.sharedMaterial = terrainMaterial;
        terrainObject.name = "Ground";
    }

    private void BuildPonds()
    {
        if (pondsRoot == null)
        {
            GameObject root = new GameObject("Ponds");
            pondsRoot = root.transform;
            pondsRoot.SetParent(transform, false);
        }

        const int segments = 40;
        float[] ringRadii = { 0.34f, 0.72f, 1f };
        for (int pondIndex = 0; pondIndex < ponds.Length; pondIndex++)
        {
            PondDefinition pond = ponds[pondIndex];
            int vertexCount = 1 + segments * ringRadii.Length;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int ring = 0; ring < ringRadii.Length; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    float angle = segment * Mathf.PI * 2f / segments;
                    float outline = PondOutlineFactor(angle, pond.Phase);
                    float radius = ringRadii[ring] * outline;
                    float localX = Mathf.Cos(angle) * pond.RadiusX * radius;
                    float localZ = Mathf.Sin(angle) * pond.RadiusZ * radius;
                    float rotation = pond.Rotation * Mathf.Deg2Rad;
                    float worldX = Mathf.Cos(rotation) * localX + Mathf.Sin(rotation) * localZ;
                    float worldZ = -Mathf.Sin(rotation) * localX + Mathf.Cos(rotation) * localZ;
                    int index = 1 + ring * segments + segment;
                    vertices[index] = new Vector3(worldX, 0f, worldZ);
                    uvs[index] = new Vector2(worldX / (pond.RadiusX * 2f) + 0.5f, worldZ / (pond.RadiusZ * 2f) + 0.5f);
                }
            }

            List<int> deepTriangles = new List<int>();
            List<int> shallowTriangles = new List<int>();
            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                AddTopTriangle(deepTriangles, 0, 1 + next, 1 + segment);
                for (int ring = 0; ring < ringRadii.Length - 1; ring++)
                {
                    int inner = 1 + ring * segments;
                    int outer = inner + segments;
                    List<int> target = ring < ringRadii.Length - 2 ? deepTriangles : shallowTriangles;
                    AddTopTriangle(target, inner + segment, inner + next, outer + next);
                    AddTopTriangle(target, inner + segment, outer + next, outer + segment);
                }
            }

            Mesh waterMesh = new Mesh { name = pond.Name + " Water Mesh" };
            waterMesh.vertices = vertices;
            waterMesh.uv = uvs;
            waterMesh.subMeshCount = 2;
            waterMesh.SetTriangles(deepTriangles, 0);
            waterMesh.SetTriangles(shallowTriangles, 1);
            waterMesh.RecalculateNormals();
            waterMesh.RecalculateBounds();
            generatedMeshes.Add(waterMesh);

            GameObject water = new GameObject(pond.Name);
            water.transform.SetParent(pondsRoot, false);
            water.transform.localPosition = new Vector3(pond.Center.x, pond.WaterHeight, pond.Center.y);
            water.AddComponent<MeshFilter>().sharedMesh = waterMesh;
            MeshRenderer renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { deepWaterMaterial, shallowWaterMaterial };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            BoxCollider interactionCollider = water.AddComponent<BoxCollider>();
            interactionCollider.isTrigger = true;
            interactionCollider.size = new Vector3(pond.RadiusX * 2f, 0.55f, pond.RadiusZ * 2f);
            PondRestorationSite site = water.AddComponent<PondRestorationSite>();
            site.Initialize(pond.Name, pondIndex == 0, renderer, new Vector3(pond.Center.x, pond.WaterHeight, pond.Center.y));

            pondWaterMotions.Add(new PondWaterMotion
            {
                Mesh = waterMesh,
                BaseVertices = (Vector3[])vertices.Clone(),
                AnimatedVertices = (Vector3[])vertices.Clone(),
                RadiusX = pond.RadiusX,
                RadiusZ = pond.RadiusZ,
                Phase = pond.Phase
            });
        }
    }

    private static void AddTopTriangle(List<int> triangles, int a, int b, int c)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
    }

    private float SampleHeight(float x, float z)
    {
        float baseHeight = SampleBaseTerrainHeight(x, z);
        float height = baseHeight;
        if (ponds == null)
            return height;

        foreach (PondDefinition pond in ponds)
        {
            float normalizedDistance = PondNormalizedDistance(x, z, pond);
            if (normalizedDistance >= 1.12f)
                continue;

            float shoreline = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 1.12f, normalizedDistance));
            float pondFloor = pond.WaterHeight - pond.Depth;
            height = Mathf.Min(height, Mathf.Lerp(pondFloor, baseHeight, shoreline));
        }

        return height;
    }

    private float SampleBaseTerrainHeight(float x, float z)
    {
        float noise = Mathf.PerlinNoise((x + 121f) * 0.008f, (z + 77f) * 0.008f) * 3.1f;
        noise += Mathf.PerlinNoise((x - 19f) * 0.025f, (z + 131f) * 0.025f) * 1.05f;
        noise += Mathf.PerlinNoise((x + 43f) * 0.075f, (z - 28f) * 0.075f) * 0.22f;
        float spawnOffset = Mathf.PerlinNoise(121f * 0.008f, 77f * 0.008f) * 3.1f
                          + Mathf.PerlinNoise(-19f * 0.025f, 131f * 0.025f) * 1.05f
                          + Mathf.PerlinNoise(43f * 0.075f, -28f * 0.075f) * 0.22f;
        float height = noise - spawnOffset;

        float flatten = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(PathDistance(x, z) / 10f));
        foreach (Vector2 clearing in clearings)
        {
            float clearingInfluence = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Vector2.Distance(new Vector2(x, z), clearing) / 15f));
            flatten = Mathf.Min(flatten, clearingInfluence);
        }

        return height * flatten;
    }

    private static float PondNormalizedDistance(float x, float z, PondDefinition pond)
    {
        float dx = x - pond.Center.x;
        float dz = z - pond.Center.y;
        float rotation = pond.Rotation * Mathf.Deg2Rad;
        float localX = Mathf.Cos(rotation) * dx - Mathf.Sin(rotation) * dz;
        float localZ = Mathf.Sin(rotation) * dx + Mathf.Cos(rotation) * dz;
        float normalizedX = localX / pond.RadiusX;
        float normalizedZ = localZ / pond.RadiusZ;
        float angle = Mathf.Atan2(normalizedZ, normalizedX);
        return new Vector2(normalizedX, normalizedZ).magnitude / PondOutlineFactor(angle, pond.Phase);
    }

    private static float PondOutlineFactor(float angle, float phase)
    {
        return 1f + Mathf.Sin(angle * 3f + phase) * 0.065f + Mathf.Sin(angle * 5f - phase * 1.7f) * 0.035f;
    }

    private void BuildPaths()
    {
        CreatePath("Main Forest Trail", mainPath, 4.8f);
        string[] names = { "East Clearing Trail", "West Clearing Trail", "Deep Forest Trail", "Fern Hollow Trail", "North Glade Trail", "Western Ridge Trail", "Eastern Grove Trail", "Guardian Home Trail" };
        for (int i = 0; i < secondaryPaths.Length; i++)
            CreatePath(names[i], secondaryPaths[i], i < 3 ? 3.8f : (i == 7 ? 2.8f : 3.2f));
    }

    private void CreatePath(string pathName, Vector2[] points, float width)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 direction;
            if (i == 0)
                direction = (points[1] - points[0]).normalized;
            else if (i == points.Length - 1)
                direction = (points[i] - points[i - 1]).normalized;
            else
                direction = (points[i + 1] - points[i - 1]).normalized;

            float widthVariation = 0.86f + Mathf.Sin(i * 1.73f + points.Length * 0.41f) * 0.08f + Mathf.Sin(i * 0.67f + points.Length) * 0.05f;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x) * (width * widthVariation * 0.5f);
            Vector2 left = points[i] + perpendicular;
            Vector2 right = points[i] - perpendicular;
            vertices.Add(new Vector3(left.x, SampleHeight(left.x, left.y) + 0.045f, left.y));
            vertices.Add(new Vector3(right.x, SampleHeight(right.x, right.y) + 0.045f, right.y));

            if (i < points.Length - 1)
            {
                int start = i * 2;
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }
        }

        CreateMeshObject(pathName, pathsRoot, vertices.ToArray(), triangles.ToArray(), pathMaterial, false);
    }

    private void BuildGuardianShelter()
    {
        GameObject shelter = new GameObject("Guardian Shelter");
        shelter.transform.SetParent(transform, false);
        shelter.transform.localPosition = new Vector3(34f, SampleHeight(34f, 52f), 52f);
        shelter.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        shelter.AddComponent<ShelterInteraction>();

        CreateBoxPart(shelter.transform, "Stone Foundation", new Vector3(0f, 0.3f, 0f), new Vector3(9f, 0.6f, 8f), Quaternion.identity, stoneFoundationMaterial, true);
        CreateBoxPart(shelter.transform, "Back Wall", new Vector3(0f, 2.15f, -3.55f), new Vector3(7.8f, 3.1f, 0.35f), Quaternion.identity, woodMaterial, true);
        CreateBoxPart(shelter.transform, "Front Wall Left", new Vector3(-2.35f, 2.15f, 3.55f), new Vector3(3.1f, 3.1f, 0.35f), Quaternion.identity, woodMaterial, true);
        CreateBoxPart(shelter.transform, "Front Wall Right", new Vector3(2.35f, 2.15f, 3.55f), new Vector3(3.1f, 3.1f, 0.35f), Quaternion.identity, woodMaterial, true);

        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 3.8f;
            CreateBoxPart(shelter.transform, "Side Wall Lower", new Vector3(x, 1.1f, 0f), new Vector3(0.4f, 1f, 7.1f), Quaternion.identity, woodMaterial, true);
            CreateBoxPart(shelter.transform, "Side Wall Upper", new Vector3(x, 3.05f, 0f), new Vector3(0.4f, 1.2f, 7.1f), Quaternion.identity, woodMaterial, true);
            CreateBoxPart(shelter.transform, "Side Wall Window Front", new Vector3(x, 2.05f, 2.45f), new Vector3(0.4f, 0.9f, 2.1f), Quaternion.identity, woodMaterial, true);
            CreateBoxPart(shelter.transform, "Side Wall Window Back", new Vector3(x, 2.05f, -2.45f), new Vector3(0.4f, 0.9f, 2.1f), Quaternion.identity, woodMaterial, true);
            float glassX = side * 4.02f;
            CreateBoxPart(shelter.transform, "Window Glass", new Vector3(glassX, 2.05f, 0f), new Vector3(0.06f, 0.72f, 1.18f), Quaternion.identity, windowMaterial, false);
            CreateBoxPart(shelter.transform, "Window Frame Vertical", new Vector3(glassX, 2.05f, -0.64f), new Vector3(0.11f, 1f, 0.12f), Quaternion.identity, barkMaterial, false);
            CreateBoxPart(shelter.transform, "Window Frame Vertical", new Vector3(glassX, 2.05f, 0.64f), new Vector3(0.11f, 1f, 0.12f), Quaternion.identity, barkMaterial, false);
            CreateBoxPart(shelter.transform, "Window Frame Crossbar", new Vector3(glassX, 2.05f, 0f), new Vector3(0.12f, 0.1f, 1.3f), Quaternion.identity, barkMaterial, false);
        }

        CreateBoxPart(shelter.transform, "Open Wooden Door", new Vector3(0f, 1.55f, 3.76f), new Vector3(1.38f, 2.5f, 0.14f), Quaternion.Euler(0f, -62f, 0f), barkMaterial, false);
        CreateBoxPart(shelter.transform, "Door Lintel", new Vector3(0f, 3.72f, 3.55f), new Vector3(1.7f, 0.18f, 0.5f), Quaternion.identity, woodMaterial, false);
        CreateBoxPart(shelter.transform, "Front Eave Beam", new Vector3(0f, 3.65f, 3.65f), new Vector3(8.2f, 0.24f, 0.34f), Quaternion.identity, barkMaterial, false);
        CreateBoxPart(shelter.transform, "Rear Eave Beam", new Vector3(0f, 3.65f, -3.65f), new Vector3(8.2f, 0.24f, 0.34f), Quaternion.identity, barkMaterial, false);
        CreateBoxPart(shelter.transform, "Roof Left Slope", new Vector3(-2.15f, 4.55f, 0f), new Vector3(5.1f, 0.34f, 8.5f), Quaternion.Euler(0f, 0f, 25f), roofMaterial, true);
        CreateBoxPart(shelter.transform, "Roof Right Slope", new Vector3(2.15f, 4.55f, 0f), new Vector3(5.1f, 0.34f, 8.5f), Quaternion.Euler(0f, 0f, -25f), roofMaterial, true);
        CreateBoxPart(shelter.transform, "Porch", new Vector3(0f, 0.56f, 4.55f), new Vector3(3.2f, 0.18f, 2.2f), Quaternion.identity, woodMaterial, true);
        CreateBoxPart(shelter.transform, "Porch Step", new Vector3(0f, 0.29f, 5.35f), new Vector3(2.3f, 0.28f, 0.55f), Quaternion.identity, woodMaterial, true);

        // Compact, collider-free home furnishings: a clear bed silhouette, storage,
        // work surface and conservation kit all fit inside the existing shelter.
        CreateBoxPart(shelter.transform, "Bed Frame", new Vector3(-2.1f, 0.72f, -1.6f), new Vector3(2.25f, 0.38f, 3.3f), Quaternion.identity, barkMaterial, false);
        CreateBoxPart(shelter.transform, "Bed Mattress", new Vector3(-2.1f, 1.02f, -1.6f), new Vector3(2.08f, 0.28f, 3.05f), Quaternion.identity, roofMaterial, false);
        CreateBoxPart(shelter.transform, "Bed Blanket", new Vector3(-2.1f, 1.19f, -2.15f), new Vector3(2.1f, 0.1f, 1.75f), Quaternion.identity, woodMaterial, false);
        CreateBoxPart(shelter.transform, "Bed Pillow", new Vector3(-2.1f, 1.22f, -0.43f), new Vector3(1.2f, 0.18f, 0.48f), Quaternion.identity, windowMaterial, false);
        CreateBoxPart(shelter.transform, "Storage Chest", new Vector3(2.25f, 0.86f, -2.25f), new Vector3(1.55f, 0.9f, 1.15f), Quaternion.identity, barkMaterial, false);
        CreateBoxPart(shelter.transform, "Chest Lid", new Vector3(2.25f, 1.34f, -2.25f), new Vector3(1.65f, 0.15f, 1.22f), Quaternion.identity, woodMaterial, false);
        CreateBoxPart(shelter.transform, "Work Table", new Vector3(2.15f, 1.12f, 0.15f), new Vector3(1.9f, 0.16f, 1.35f), Quaternion.identity, woodMaterial, false);
        for (int legX = -1; legX <= 1; legX += 2)
            for (int legZ = -1; legZ <= 1; legZ += 2)
                CreateBoxPart(shelter.transform, "Table Leg", new Vector3(2.15f + legX * 0.72f, 0.65f, 0.15f + legZ * 0.48f), new Vector3(0.14f, 0.95f, 0.14f), Quaternion.identity, barkMaterial, false);
        CreateBoxPart(shelter.transform, "Field Notebook", new Vector3(1.95f, 1.23f, 0.05f), new Vector3(0.48f, 0.06f, 0.62f), Quaternion.Euler(0f, -14f, 0f), windowMaterial, false);
        CreateBoxPart(shelter.transform, "Sample Case", new Vector3(2.55f, 1.27f, 0.28f), new Vector3(0.48f, 0.16f, 0.38f), Quaternion.identity, stoneFoundationMaterial, false);
        CreateBoxPart(shelter.transform, "Lantern Base", new Vector3(2.15f, 1.31f, -0.1f), new Vector3(0.35f, 0.12f, 0.35f), Quaternion.identity, barkMaterial, false);
        GameObject lantern = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lantern.name = "Warm Lantern";
        lantern.transform.SetParent(shelter.transform, false);
        lantern.transform.localPosition = new Vector3(2.15f, 1.58f, -0.1f);
        lantern.transform.localScale = Vector3.one * 0.24f;
        lantern.GetComponent<Renderer>().sharedMaterial = windowMaterial;
        Destroy(lantern.GetComponent<Collider>());
        Light lanternLight = lantern.AddComponent<Light>();
        lanternLight.type = LightType.Point;
        lanternLight.color = new Color(1f, 0.69f, 0.34f);
        lanternLight.intensity = 0.65f;
        lanternLight.range = 4f;
        lanternLight.shadows = LightShadows.None;

        Mesh cylinder = GetPrimitiveMesh(PrimitiveType.Cylinder);
        if (cylinder != null)
        {
            Mesh firewood = CombineMesh("Shelter Firewood Log Mesh", new[] { cylinder }, new[]
            {
                Matrix4x4.TRS(new Vector3(0f, 0.25f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.22f, 1.3f, 0.22f))
            });
            CreateSimpleMeshObject("Firewood", shelter.transform, firewood, barkMaterial, new Vector3(-5.4f, 0.35f, 1.2f), new Vector3(0.8f, 0.8f, 0.8f), -18f, null);
            CreateSimpleMeshObject("Firewood", shelter.transform, firewood, barkMaterial, new Vector3(-5.25f, 0.60f, 1.2f), new Vector3(0.75f, 0.75f, 0.75f), 14f, null);
            CreateSimpleMeshObject("Firewood", shelter.transform, firewood, barkMaterial, new Vector3(-5.45f, 0.85f, 1.2f), new Vector3(0.68f, 0.68f, 0.68f), -8f, null);
            CreateSimpleMeshObject("Chopping Stump", shelter.transform, cylinder, barkMaterial, new Vector3(-5.1f, 0.38f, -1.2f), new Vector3(0.75f, 0.38f, 0.75f), 0f, null);
        }

        CreateBoxPart(shelter.transform, "Trail Sign Post", new Vector3(5.5f, 1.05f, 5.3f), new Vector3(0.18f, 2.1f, 0.18f), Quaternion.identity, barkMaterial, false);
        CreateBoxPart(shelter.transform, "Trail Sign", new Vector3(5.5f, 1.8f, 5.3f), new Vector3(1.5f, 0.58f, 0.16f), Quaternion.identity, woodMaterial, false);

        Mesh stoneMesh = GetPrimitiveMesh(PrimitiveType.Sphere);
        if (stoneMesh != null)
        {
            const int stones = 7;
            for (int i = 0; i < stones; i++)
            {
                float angle = i * Mathf.PI * 2f / stones;
                Vector3 position = new Vector3(5.7f + Mathf.Cos(angle) * 0.78f, 0.12f, -0.45f + Mathf.Sin(angle) * 0.78f);
                CreateSimpleMeshObject("Campfire Ring Stone", shelter.transform, stoneMesh, rockMaterial,
                    position, new Vector3(0.38f, 0.24f, 0.34f), angle * Mathf.Rad2Deg, null);
            }
            CreateSimpleMeshObject("Cold Campfire Bed", shelter.transform, stoneMesh, barkMaterial,
                new Vector3(5.7f, 0.11f, -0.45f), new Vector3(1.1f, 0.12f, 1.1f), 0f, null);
        }
    }

    private static GameObject CreateBoxPart(Transform parent, string name, Vector3 position, Vector3 scale, Quaternion rotation, Material material, bool hasCollider)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.transform.localRotation = rotation;
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
        if (!hasCollider)
        {
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }
        return part;
    }

    private void BuildTrees()
    {
        Mesh[] treeMeshes = { BuildTreeMesh(0), BuildTreeMesh(1), BuildTreeMesh(2) };
        if (treeMeshes[0] == null || treeMeshes[1] == null || treeMeshes[2] == null)
            return;

        Material[][] materials =
        {
            new[] { barkMaterial, leafMaterial },
            new[] { barkMaterial, deepLeafMaterial },
            new[] { barkMaterial, brightLeafMaterial }
        };
        System.Random random = new System.Random(randomSeed);

        // Place groups of trees with clear gaps between groves. The path, pond and clearing
        // exclusions leave recognizable open routes through the otherwise denser woodland.
        List<Vector2> groveCenters = new List<Vector2>();
        for (int attempt = 0; attempt < 900 && groveCenters.Count < 30; attempt++)
        {
            float x = (float)(random.NextDouble() * (worldWidth - 100f) - (worldWidth - 100f) * 0.5f);
            float z = worldStartZ + 50f + (float)random.NextDouble() * (worldDepth - 100f);
            Vector2 candidate = new Vector2(x, z);
            if (PathDistance(x, z) < 24f || IsInClearing(x, z, 28f) || IsNearPond(x, z, 28f))
                continue;

            bool overlapsGrove = false;
            for (int i = 0; i < groveCenters.Count; i++)
            {
                if ((candidate - groveCenters[i]).sqrMagnitude < 48f * 48f)
                {
                    overlapsGrove = true;
                    break;
                }
            }
            if (!overlapsGrove)
                groveCenters.Add(candidate);
        }

        for (int grove = 0; grove < groveCenters.Count; grove++)
        {
            Vector2 center = groveCenters[grove];
            int targetTrees = 42 + random.Next(17);
            int created = 0;
            for (int attempt = 0; attempt < targetTrees * 9 && created < targetTrees; attempt++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Sqrt((float)random.NextDouble()) * (24f + (float)random.NextDouble() * 12f);
                float treeX = center.x + Mathf.Cos(angle) * radius;
                float treeZ = center.y + Mathf.Sin(angle) * radius;
                if (Mathf.Abs(treeX) > worldWidth * 0.5f - 8f || treeZ < worldStartZ + 8f || treeZ > worldStartZ + worldDepth - 8f)
                    continue;
                if (PathDistance(treeX, treeZ) < 8f || IsInClearing(treeX, treeZ, -3f) || IsNearPond(treeX, treeZ, 4f))
                    continue;
                if (IsNearTree(treeX, treeZ, 5.8f, treesRoot))
                    continue;

                CreateVariedTree(treeMeshes, materials, treeX, treeZ, random);
                created++;
            }
        }

        // Light, noise-varied background trees soften the edges between groves without
        // filling paths, pond shores or intentional clearings with a uniform grid.
        for (int attempt = 0, created = 0; attempt < 12000 && created < 360; attempt++)
        {
            float x = (float)(random.NextDouble() * (worldWidth - 20f) - (worldWidth - 20f) * 0.5f);
            float z = worldStartZ + 10f + (float)random.NextDouble() * (worldDepth - 20f);
            float densityNoise = Mathf.PerlinNoise((x + 131f) * 0.019f, (z - 47f) * 0.019f);
            if (densityNoise < 0.38f || PathDistance(x, z) < 9f || IsInClearing(x, z, -4f) || IsNearPond(x, z, 5f))
                continue;
            if (IsNearTree(x, z, 7.6f, treesRoot))
                continue;

            CreateVariedTree(treeMeshes, materials, x, z, random);
            created++;
        }
    }

    private void PositionGuardianAtShelter()
    {
        if (guardianTransform == null)
            return;

        // Start just outside the shelter's south-facing porch, on safe open ground.
        const float spawnX = 34f;
        const float spawnZ = 43.6f;
        guardianTransform.SetPositionAndRotation(
            new Vector3(spawnX, SampleHeight(spawnX, spawnZ), spawnZ),
            Quaternion.Euler(0f, 0f, 0f));
    }

    private void CreateVariedTree(Mesh[] meshes, Material[][] materials, float x, float z, System.Random random)
    {
        int variation = random.Next(meshes.Length);
        float sizeMultiplier = 1f;
        if (guardianTransform != null)
        {
            float distance = Vector2.Distance(new Vector2(x, z), new Vector2(guardianTransform.position.x, guardianTransform.position.z));
            float nearPlayer = 1f - Mathf.InverseLerp(32f, 210f, distance);
            sizeMultiplier = Mathf.Lerp(0.78f, 1.18f, nearPlayer);
        }
        CreateTree(meshes[variation], materials[variation], new Vector3(x, SampleHeight(x, z), z), random, sizeMultiplier);
    }

    private Mesh BuildTreeMesh(int variation)
    {
        Mesh cylinder = GetPrimitiveMesh(PrimitiveType.Cylinder);
        Mesh sphere = GetPrimitiveMesh(PrimitiveType.Sphere);
        if (cylinder == null || sphere == null)
            return null;

        float trunkHeight = variation == 1 ? 3.9f : (variation == 2 ? 2.55f : 2.8f);
        float trunkRadius = variation == 1 ? 0.36f : (variation == 2 ? 0.50f : 0.42f);
        Matrix4x4 trunkMatrix = Matrix4x4.TRS(new Vector3(0f, trunkHeight * 0.5f, 0f), Quaternion.identity,
            new Vector3(trunkRadius, trunkHeight * 0.5f, trunkRadius));
        Mesh trunk = CombineMesh("Tree Trunk", new[] { cylinder }, new[] { trunkMatrix });
        List<Matrix4x4> leafMatrices = new List<Matrix4x4>();
        if (variation == 1)
        {
            leafMatrices.Add(Matrix4x4.TRS(new Vector3(0f, 4.35f, 0f), Quaternion.identity, new Vector3(1.55f, 2.55f, 1.55f)));
            leafMatrices.Add(Matrix4x4.TRS(new Vector3(-0.48f, 3.8f, 0.06f), Quaternion.identity, new Vector3(1.15f, 1.7f, 1.2f)));
            leafMatrices.Add(Matrix4x4.TRS(new Vector3(0.50f, 3.8f, -0.08f), Quaternion.identity, new Vector3(1.2f, 1.75f, 1.15f)));
            leafMatrices.Add(Matrix4x4.TRS(new Vector3(0.05f, 3.45f, 0.55f), Quaternion.identity, new Vector3(1.05f, 1.3f, 1.1f)));
        }
        else if (variation == 2)
        {
            leafMatrices.Add(Matrix4x4.TRS(new Vector3(0f, 2.9f, 0f), Quaternion.identity, new Vector3(2.3f, 1.55f, 2.15f)));
            leafMatrices.Add(Matrix4x4.TRS(new Vector3(-0.85f, 2.55f, 0.20f), Quaternion.identity, new Vector3(1.45f, 1.15f, 1.55f)));
            leafMatrices.Add(Matrix4x4.TRS(new Vector3(0.8f, 2.7f, -0.28f), Quaternion.identity, new Vector3(1.55f, 1.3f, 1.4f)));
            leafMatrices.Add(Matrix4x4.TRS(new Vector3(0.2f, 3.2f, 0.6f), Quaternion.identity, new Vector3(1.25f, 1.25f, 1.2f)));
        }
        else
        {
            leafMatrices.Add(Matrix4x4.TRS(new Vector3(0f, 3.2f, 0f), Quaternion.identity, new Vector3(1.85f, 2.05f, 1.85f)));
            leafMatrices.Add(Matrix4x4.TRS(new Vector3(-0.62f, 2.7f, 0.08f), Quaternion.identity, new Vector3(1.15f, 1.3f, 1.15f)));
            leafMatrices.Add(Matrix4x4.TRS(new Vector3(0.62f, 2.75f, -0.1f), Quaternion.identity, new Vector3(1.15f, 1.35f, 1.15f)));
        }
        Mesh[] leafMeshes = new Mesh[leafMatrices.Count];
        for (int i = 0; i < leafMeshes.Length; i++)
            leafMeshes[i] = sphere;
        Mesh canopy = CombineMesh("Tree Canopy", leafMeshes, leafMatrices.ToArray());

        CombineInstance[] parts =
        {
            new CombineInstance { mesh = trunk, subMeshIndex = 0, transform = Matrix4x4.identity },
            new CombineInstance { mesh = canopy, subMeshIndex = 0, transform = Matrix4x4.identity }
        };
        Mesh tree = new Mesh { name = "Combined Forest Tree" };
        tree.CombineMeshes(parts, false, true);
        tree.RecalculateBounds();
        generatedMeshes.Add(tree);
        generatedMeshes.Add(trunk);
        generatedMeshes.Add(canopy);
        return tree;
    }

    private void CreateTree(Mesh mesh, Material[] materials, Vector3 position, System.Random random, float sizeMultiplier)
    {
        GameObject tree = new GameObject("Forest Tree");
        tree.transform.SetParent(treesRoot, false);
        tree.transform.localPosition = position;
        tree.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
        float sizeRoll = (float)random.NextDouble();
        float scale = sizeRoll < 0.3f
            ? 0.58f + (float)random.NextDouble() * 0.28f
            : (sizeRoll < 0.76f
                ? 0.88f + (float)random.NextDouble() * 0.32f
                : 1.22f + (float)random.NextDouble() * 0.48f);
        scale *= sizeMultiplier;
        tree.transform.localScale = new Vector3(scale, scale * (0.9f + (float)random.NextDouble() * 0.3f), scale);

        MeshFilter filter = tree.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = tree.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = scale > 1.05f ? ShadowCastingMode.On : ShadowCastingMode.Off;
        EnvironmentInspectable inspection = tree.AddComponent<EnvironmentInspectable>();
        inspection.Configure("Forest Tree", "Healthy", "A mature tree that provides shade, food and nesting habitat.");

        CapsuleCollider collider = tree.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 1.5f, 0f);
        collider.radius = 0.34f;
        collider.height = 3f;
    }

    private void BuildVegetation()
    {
        bushClusterMesh = BuildClusterMesh("Bush Cluster", PrimitiveType.Sphere, new[]
        {
            Matrix4x4.TRS(new Vector3(-0.35f, 0.48f, 0f), Quaternion.identity, new Vector3(0.9f, 0.85f, 0.82f)),
            Matrix4x4.TRS(new Vector3(0.35f, 0.44f, 0.08f), Quaternion.identity, new Vector3(0.92f, 0.76f, 0.85f)),
            Matrix4x4.TRS(new Vector3(0f, 0.73f, -0.25f), Quaternion.identity, new Vector3(0.78f, 0.9f, 0.8f))
        });
        Mesh grassMesh = BuildGrassMesh();
        fernClusterMesh = BuildFernMesh();
        if (bushClusterMesh == null || grassMesh == null || fernClusterMesh == null)
            return;

        System.Random random = new System.Random(randomSeed + 19);
        for (int treeIndex = 0; treeIndex < treesRoot.childCount; treeIndex++)
        {
            if (random.NextDouble() > 0.36)
                continue;

            Vector3 treePosition = treesRoot.GetChild(treeIndex).localPosition;
            int bushes = 1 + random.Next(2);
            for (int bush = 0; bush < bushes; bush++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = 3.2f + (float)random.NextDouble() * 2.8f;
                float bushX = treePosition.x + Mathf.Cos(angle) * radius;
                float bushZ = treePosition.z + Mathf.Sin(angle) * radius;
                if (PathDistance(bushX, bushZ) < 6.5f || IsInClearing(bushX, bushZ, -2f) || IsNearPond(bushX, bushZ, 1.8f))
                    continue;

                CreateSimpleMeshObject("Understory Bush", vegetationRoot, bushClusterMesh, bushMaterial,
                    new Vector3(bushX, SampleHeight(bushX, bushZ), bushZ),
                    new Vector3(0.8f + (float)random.NextDouble() * 0.8f, 0.75f + (float)random.NextDouble() * 0.55f, 0.8f + (float)random.NextDouble() * 0.8f),
                    (float)random.NextDouble() * 360f, null);
            }
        }

        // Ground plants grow in loose patches instead of an even scatter across the map.
        for (int patch = 0; patch < 42; patch++)
        {
            float centerX = (float)(random.NextDouble() * (worldWidth - 32f) - (worldWidth - 32f) * 0.5f);
            float centerZ = worldStartZ + 16f + (float)random.NextDouble() * (worldDepth - 32f);
            int grassCount = 6 + random.Next(7);
            int fernCount = 3 + random.Next(5);

            for (int plant = 0; plant < grassCount + fernCount; plant++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Sqrt((float)random.NextDouble()) * (4f + (float)random.NextDouble() * 7f);
                float x = centerX + Mathf.Cos(angle) * radius;
                float z = centerZ + Mathf.Sin(angle) * radius;
                if (PathDistance(x, z) < 4.5f || IsNearPond(x, z, 0.6f))
                    continue;

                bool isGrass = plant < grassCount;
                Mesh mesh = isGrass ? grassMesh : fernClusterMesh;
                Material material = isGrass ? grassMaterial : fernMaterial;
                float size = 0.65f + (float)random.NextDouble() * 0.75f;
                CreateSimpleMeshObject(isGrass ? "Grass Tuft" : "Forest Fern Cluster", vegetationRoot, mesh, material,
                    new Vector3(x, SampleHeight(x, z) + (isGrass ? 0.015f : 0.02f), z), Vector3.one * size,
                    (float)random.NextDouble() * 360f, null);
            }
        }

        for (int pondIndex = 0; pondIndex < ponds.Length; pondIndex++)
        {
            PondDefinition pond = ponds[pondIndex];
            float startAngle = pond.Phase;
            for (int plant = 0; plant < 5; plant++)
            {
                float angle = startAngle + plant * Mathf.PI * 0.4f;
                float outline = PondOutlineFactor(angle, pond.Phase);
                float localX = Mathf.Cos(angle) * pond.RadiusX * outline * 1.12f;
                float localZ = Mathf.Sin(angle) * pond.RadiusZ * outline * 1.12f;
                float rotation = pond.Rotation * Mathf.Deg2Rad;
                float x = pond.Center.x + Mathf.Cos(rotation) * localX + Mathf.Sin(rotation) * localZ;
                float z = pond.Center.y - Mathf.Sin(rotation) * localX + Mathf.Cos(rotation) * localZ;
                if (PathDistance(x, z) < 3.6f)
                    continue;
                Mesh mesh = plant % 2 == 0 ? fernClusterMesh : bushClusterMesh;
                Material material = plant % 2 == 0 ? fernMaterial : bushMaterial;
                float size = plant % 2 == 0 ? 0.75f : 1f;
                CreateSimpleMeshObject("Pond Edge Plants", vegetationRoot, mesh, material,
                    new Vector3(x, SampleHeight(x, z), z), Vector3.one * size,
                    (float)random.NextDouble() * 360f, null);
            }
        }

        Vector2[] homePlants = { new Vector2(26f, 48f), new Vector2(42f, 48f), new Vector2(25f, 59f), new Vector2(43f, 59f) };
        for (int i = 0; i < homePlants.Length; i++)
        {
            Vector2 location = homePlants[i];
            if (PathDistance(location.x, location.y) < 3f)
                continue;
            CreateSimpleMeshObject("Shelter Side Bush", vegetationRoot, bushClusterMesh, bushMaterial,
                new Vector3(location.x, SampleHeight(location.x, location.y), location.y), Vector3.one * 1.05f,
                i * 61f, null);
        }
    }

    private Mesh BuildFernMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        for (int frond = 0; frond < 7; frond++)
        {
            float angle = frond * Mathf.PI * 2f / 7f;
            Vector3 forward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 side = new Vector3(forward.z, 0f, -forward.x);
            for (int leaf = 0; leaf < 4; leaf++)
            {
                float t = 0.28f + leaf * 0.17f;
                Vector3 stem = forward * (t * 0.8f) + Vector3.up * (0.16f + t * 0.45f);
                Vector3 tip = stem + forward * 0.26f + Vector3.up * 0.06f;
                float width = 0.09f + (1f - t) * 0.07f;
                AddDoubleSidedLeaf(vertices, triangles, stem, stem + side * width, tip);
                AddDoubleSidedLeaf(vertices, triangles, stem, stem - side * width, tip);
            }
            Vector3 basePoint = Vector3.zero;
            Vector3 frondTip = forward * 0.9f + Vector3.up * 0.72f;
            AddDoubleSidedLeaf(vertices, triangles, basePoint, basePoint + side * 0.08f, frondTip);
        }

        Mesh mesh = new Mesh { name = "Forest Fern Cluster Mesh" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        generatedMeshes.Add(mesh);
        return mesh;
    }

    private static void AddDoubleSidedLeaf(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 tip)
    {
        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(tip);
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(tip);
        triangles.Add(start + 4);
        triangles.Add(start + 3);
        triangles.Add(start + 5);
    }

    private Mesh BuildGrassMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        for (int blade = 0; blade < 5; blade++)
        {
            float angle = blade * 72f * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 side = new Vector3(forward.z, 0f, -forward.x) * 0.11f;
            Vector3 center = forward * (blade % 2 == 0 ? 0.08f : 0.02f);
            int start = vertices.Count;
            vertices.Add(center - side);
            vertices.Add(center + side);
            vertices.Add(center + forward * 0.08f + Vector3.up * (0.4f + (blade % 3) * 0.12f));
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);

            // Give the reverse-facing side its own vertices so its opposite winding
            // does not cancel the front side's normals during RecalculateNormals.
            int backStart = vertices.Count;
            vertices.Add(center - side);
            vertices.Add(center + side);
            vertices.Add(center + forward * 0.08f + Vector3.up * (0.4f + (blade % 3) * 0.12f));
            triangles.Add(backStart + 1);
            triangles.Add(backStart);
            triangles.Add(backStart + 2);
        }

        Mesh mesh = new Mesh { name = "Combined Grass Tuft" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        generatedMeshes.Add(mesh);
        return mesh;
    }

    private void BuildRocks()
    {
        Mesh rockMesh = BuildClusterMesh("Rock Cluster", PrimitiveType.Sphere, new[]
        {
            Matrix4x4.TRS(new Vector3(-0.28f, 0.25f, 0f), Quaternion.identity, new Vector3(0.7f, 0.55f, 0.65f)),
            Matrix4x4.TRS(new Vector3(0.27f, 0.2f, 0.12f), Quaternion.identity, new Vector3(0.62f, 0.46f, 0.56f)),
            Matrix4x4.TRS(new Vector3(0.02f, 0.43f, -0.18f), Quaternion.identity, new Vector3(0.52f, 0.53f, 0.52f))
        });
        if (rockMesh == null)
            return;

        Mesh[] boulderMeshes = { BuildIrregularBoulderMesh(0), BuildIrregularBoulderMesh(1), BuildIrregularBoulderMesh(2) };

        System.Random random = new System.Random(randomSeed + 41);
        for (int i = 0; i < 50; i++)
        {
            float rockX = (float)(random.NextDouble() * 180f - 90f);
            float rockZ = (float)(random.NextDouble() * 185f - 8f);
            if (PathDistance(rockX, rockZ) < 5.5f || IsInClearing(rockX, rockZ, 1.5f) || IsNearPond(rockX, rockZ, 2f))
                continue;

            CreateRock(rockMesh, rockX, rockZ, random);
        }

        for (int i = 0; i < 55; i++)
        {
            float rockX = (float)(random.NextDouble() * (worldWidth - 30f) - (worldWidth - 30f) * 0.5f);
            float rockZ = worldStartZ + 15f + (float)random.NextDouble() * (worldDepth - 30f);
            if (PathDistance(rockX, rockZ) < 5.5f || IsInClearing(rockX, rockZ, 2f) || IsNearPond(rockX, rockZ, 2f))
                continue;
            CreateRock(rockMesh, rockX, rockZ, random);
        }

        // Sprinkle small stones around woodland groups rather than laying them out in rows.
        for (int i = 0; i < treesRoot.childCount; i += 11)
        {
            Transform nearbyTree = treesRoot.GetChild(i);
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float radius = 2.8f + (float)random.NextDouble() * 4.5f;
            float rockX = nearbyTree.localPosition.x + Mathf.Cos(angle) * radius;
            float rockZ = nearbyTree.localPosition.z + Mathf.Sin(angle) * radius;
            if (PathDistance(rockX, rockZ) < 4.5f || IsInClearing(rockX, rockZ, -2f) || IsNearPond(rockX, rockZ, 1.5f))
                continue;
            CreateRock(rockMesh, rockX, rockZ, random, 0.28f, 0.62f);
        }

        // Small shared rock clusters mark the pond edges while leaving a walkable shore.
        for (int pondIndex = 0; pondIndex < ponds.Length; pondIndex++)
        {
            PondDefinition pond = ponds[pondIndex];
            float startAngle = (float)random.NextDouble() * Mathf.PI * 2f;
            for (int detail = 0; detail < 5; detail++)
            {
                float angle = startAngle + detail * Mathf.PI * 0.4f + (float)(random.NextDouble() * 0.28 - 0.14);
                float outline = PondOutlineFactor(angle, pond.Phase);
                float localX = Mathf.Cos(angle) * pond.RadiusX * outline * 1.13f;
                float localZ = Mathf.Sin(angle) * pond.RadiusZ * outline * 1.13f;
                float rotation = pond.Rotation * Mathf.Deg2Rad;
                float rockX = pond.Center.x + Mathf.Cos(rotation) * localX + Mathf.Sin(rotation) * localZ;
                float rockZ = pond.Center.y - Mathf.Sin(rotation) * localX + Mathf.Cos(rotation) * localZ;
                if (PathDistance(rockX, rockZ) < 4.2f)
                    continue;
                CreateRock(rockMesh, rockX, rockZ, random, 0.42f, 0.72f);
            }
        }

        Vector3[] boulderLocations =
        {
            new Vector3(58f, 0f, 190f), new Vector3(-77f, 0f, 266f), new Vector3(145f, 0f, 306f),
            new Vector3(-154f, 0f, 171f), new Vector3(126f, 0f, 112f), new Vector3(-28f, 0f, 354f),
            new Vector3(155f, 0f, 220f), new Vector3(25f, 0f, 280f),
            new Vector3(-202f, 0f, 405f), new Vector3(207f, 0f, 382f),
            new Vector3(-210f, 0f, 86f), new Vector3(208f, 0f, 255f)
        };
        for (int i = 0; i < boulderLocations.Length; i++)
        {
            Vector3 location = boulderLocations[i];
            int variant = i % boulderMeshes.Length;
            float size = 3.2f + (float)random.NextDouble() * 2.8f;
            if (i == 1 || i == 6)
                size *= 1.25f;
            CreateBoulder(boulderMeshes[variant], location.x, location.z, size, random);

            if (i % 2 == 0)
            {
                for (int companion = 0; companion < 2; companion++)
                {
                    float angle = i * 1.91f + companion * 2.4f + (float)random.NextDouble() * 0.55f;
                    float radius = size * (0.9f + (float)random.NextDouble() * 0.35f);
                    float rockX = location.x + Mathf.Cos(angle) * radius;
                    float rockZ = location.z + Mathf.Sin(angle) * radius;
                    if (PathDistance(rockX, rockZ) < 5f || IsInClearing(rockX, rockZ, 0f) || IsNearPond(rockX, rockZ, 2f))
                        continue;
                    CreateRock(rockMesh, rockX, rockZ, random, 1.1f, 2.2f);
                }
            }
        }
    }

    private Mesh BuildIrregularBoulderMesh(int variation)
    {
        Mesh source = GetPrimitiveMesh(PrimitiveType.Sphere);
        if (source == null)
            return null;
        Mesh mesh = new Mesh { name = "Irregular Boulder Variant " + variation };
        Vector3[] vertices = source.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];
            float angle = Mathf.Atan2(vertex.z, vertex.x);
            float broad = Mathf.Sin(angle * (3f + variation) + variation * 1.7f) * 0.12f;
            float detail = Mathf.Sin(angle * (7f + variation) - vertex.y * 9f) * 0.055f;
            float factor = 1f + broad + detail;
            vertex.x *= factor;
            vertex.z *= factor;
            float crown = 1f + Mathf.Sin(angle * 2.5f + variation) * 0.13f;
            vertex.y *= crown * (variation == 1 ? 0.72f : (variation == 2 ? 1.18f : 0.88f));
            vertices[i] = vertex;
        }
        mesh.vertices = vertices;
        mesh.triangles = source.triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        generatedMeshes.Add(mesh);
        return mesh;
    }

    private void CreateBoulder(Mesh mesh, float x, float z, float size, System.Random random)
    {
        if (mesh == null)
            return;
        GameObject boulder = new GameObject("Large Forest Boulder");
        boulder.transform.SetParent(rocksRoot, false);
        float verticalScale = size * (0.78f + (float)random.NextDouble() * 0.42f);
        boulder.transform.localPosition = new Vector3(x, SampleHeight(x, z) + verticalScale * 0.4f, z);
        boulder.transform.localScale = new Vector3(size * (0.85f + (float)random.NextDouble() * 0.4f), verticalScale, size);
        boulder.transform.localRotation = Quaternion.Euler((float)random.NextDouble() * 10f - 5f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 8f - 4f);
        boulder.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = boulder.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = rockMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        ApplyStoneTint(renderer, random);
        SphereCollider collider = boulder.AddComponent<SphereCollider>();
        collider.center = new Vector3(0f, 0.05f, 0f);
        collider.radius = 0.46f;
    }

    private void BuildForestDetails()
    {
        Mesh cylinder = GetPrimitiveMesh(PrimitiveType.Cylinder);
        if (cylinder == null)
            return;

        Mesh fallenLogMesh = CombineMesh("Fallen Forest Log Mesh", new[] { cylinder }, new[]
        {
            Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.42f, 2.15f, 0.42f))
        });
        Mesh stumpMesh = CombineMesh("Tree Stump Mesh", new[] { cylinder }, new[]
        {
            Matrix4x4.TRS(new Vector3(0f, 0.42f, 0f), Quaternion.identity, new Vector3(0.55f, 0.42f, 0.55f))
        });
        Mesh branchMesh = CombineMesh("Forest Branch Mesh", new[] { cylinder }, new[]
        {
            Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.065f, 0.7f, 0.065f))
        });

        System.Random random = new System.Random(randomSeed + 103);
        int logsCreated = 0;
        for (int attempt = 0; attempt < 110 && logsCreated < 24; attempt++)
        {
            float x = (float)(random.NextDouble() * (worldWidth - 32f) - (worldWidth - 32f) * 0.5f);
            float z = worldStartZ + 16f + (float)random.NextDouble() * (worldDepth - 32f);
            if (PathDistance(x, z) < 8f || IsNearPond(x, z, 4f) || IsInClearing(x, z, -4f))
                continue;
            if (Vector2.Distance(new Vector2(x, z), new Vector2(34f, 52f)) < 22f)
                continue;
            float size = 0.72f + (float)random.NextDouble() * 0.7f;
            CreateSimpleMeshObject("Fallen Log", vegetationRoot, fallenLogMesh, barkMaterial,
                new Vector3(x, SampleHeight(x, z) + 0.34f, z), new Vector3(size, size, size),
                (float)random.NextDouble() * 360f, null);
            logsCreated++;
        }

        int stumpsCreated = 0;
        for (int attempt = 0; attempt < 90 && stumpsCreated < 16; attempt++)
        {
            float x = (float)(random.NextDouble() * (worldWidth - 34f) - (worldWidth - 34f) * 0.5f);
            float z = worldStartZ + 17f + (float)random.NextDouble() * (worldDepth - 34f);
            if (PathDistance(x, z) < 6f || IsNearPond(x, z, 3f) || IsInClearing(x, z, -4f))
                continue;
            if (Vector2.Distance(new Vector2(x, z), new Vector2(34f, 52f)) < 18f)
                continue;
            CreateSimpleMeshObject("Old Tree Stump", vegetationRoot, stumpMesh, barkMaterial,
                new Vector3(x, SampleHeight(x, z), z), Vector3.one * (0.8f + (float)random.NextDouble() * 0.6f),
                (float)random.NextDouble() * 360f, null);
            stumpsCreated++;
        }

        int branchesCreated = 0;
        for (int attempt = 0; attempt < 180 && branchesCreated < 58; attempt++)
        {
            if (treesRoot.childCount == 0)
                break;
            Transform nearbyTree = treesRoot.GetChild(random.Next(treesRoot.childCount));
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float radius = 3.5f + (float)random.NextDouble() * 5.5f;
            float x = nearbyTree.localPosition.x + Mathf.Cos(angle) * radius;
            float z = nearbyTree.localPosition.z + Mathf.Sin(angle) * radius;
            if (PathDistance(x, z) < 5.5f || IsNearPond(x, z, 2f) || IsInClearing(x, z, -3f))
                continue;

            float size = 0.55f + (float)random.NextDouble() * 0.9f;
            CreateSimpleMeshObject("Forest Fallen Branch", vegetationRoot, branchMesh, barkMaterial,
                new Vector3(x, SampleHeight(x, z) + 0.06f, z), Vector3.one * size,
                (float)random.NextDouble() * 360f, null);
            branchesCreated++;
        }

    }

    private void CreateRock(Mesh rockMesh, float x, float z, System.Random random, float minSize = 0.65f, float maxSize = 1.65f)
    {
        SphereCollider collider = new GameObject("Mossy Rock Cluster").AddComponent<SphereCollider>();
        GameObject rock = collider.gameObject;
        rock.transform.SetParent(rocksRoot, false);
        rock.transform.localPosition = new Vector3(x, SampleHeight(x, z), z);
        float size = minSize + (float)random.NextDouble() * (maxSize - minSize);
        rock.transform.localScale = new Vector3(size, size * (0.75f + (float)random.NextDouble() * 0.3f), size);
        rock.transform.localRotation = Quaternion.Euler(
            (float)random.NextDouble() * 16f - 8f,
            (float)random.NextDouble() * 360f,
            (float)random.NextDouble() * 14f - 7f);
        rock.AddComponent<MeshFilter>().sharedMesh = rockMesh;
        MeshRenderer renderer = rock.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = rockMaterial;
        ApplyStoneTint(renderer, random);
        EnvironmentInspectable inspection = rock.AddComponent<EnvironmentInspectable>();
        inspection.Configure("Mossy Rock Habitat", "Stable", "A sheltered rocky patch provides cover for small forest animals.");
        collider.center = new Vector3(0f, 0.35f, 0f);
        collider.radius = 0.48f;
    }

    private void ApplyStoneTint(MeshRenderer renderer, System.Random random)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        int colorProperty = rockMaterial.HasProperty("_BaseColor") ? Shader.PropertyToID("_BaseColor") : Shader.PropertyToID("_Color");
        block.SetColor(colorProperty, StoneTintPalette[random.Next(StoneTintPalette.Length)]);
        renderer.SetPropertyBlock(block);
    }

    private Mesh BuildClusterMesh(string meshName, PrimitiveType primitive, Matrix4x4[] matrices)
    {
        Mesh source = GetPrimitiveMesh(primitive);
        if (source == null)
            return null;

        Mesh[] sources = new Mesh[matrices.Length];
        for (int i = 0; i < sources.Length; i++)
            sources[i] = source;
        return CombineMesh(meshName, sources, matrices);
    }

    private Mesh CombineMesh(string meshName, Mesh[] sources, Matrix4x4[] matrices)
    {
        CombineInstance[] instances = new CombineInstance[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            instances[i] = new CombineInstance
            {
                mesh = sources[i],
                subMeshIndex = 0,
                transform = matrices[i]
            };
        }

        Mesh mesh = new Mesh { name = meshName };
        mesh.CombineMeshes(instances, true, true);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        generatedMeshes.Add(mesh);
        return mesh;
    }

    private Mesh GetPrimitiveMesh(PrimitiveType type)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        MeshFilter filter = primitive.GetComponent<MeshFilter>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        Destroy(primitive);
        return mesh;
    }

    private void CreateSimpleMeshObject(string objectName, Transform parent, Mesh mesh, Material material,
        Vector3 position, Vector3 scale, float yaw, Collider collider)
    {
        GameObject item = new GameObject(objectName);
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        item.transform.localScale = scale;
        item.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        item.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = item.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        if (parent == vegetationRoot)
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        if (objectName == "Understory Bush" && (++inspectableBushCount % 24) == 0)
        {
            SphereCollider bushCollider = item.AddComponent<SphereCollider>();
            bushCollider.center = Vector3.up * 0.45f;
            bushCollider.radius = 0.62f;
            bushCollider.isTrigger = true;
            EnvironmentInspectable inspection = item.AddComponent<EnvironmentInspectable>();
            inspection.Configure("Understory Vegetation", "Healthy", "Native shrubs provide berries, cover and nesting sites for forest wildlife.");
        }
        if (collider != null)
            collider.transform.SetParent(item.transform, false);
    }

    private void CreateMeshObject(string objectName, Transform parent, Vector3[] vertices, int[] triangles,
        Material material, bool addCollider)
    {
        Mesh mesh = new Mesh { name = objectName + " Mesh" };
        mesh.indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        generatedMeshes.Add(mesh);

        GameObject path = new GameObject(objectName);
        path.transform.SetParent(parent, false);
        path.AddComponent<MeshFilter>().sharedMesh = mesh;
        path.AddComponent<MeshRenderer>().sharedMaterial = material;
        if (addCollider)
            path.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private void SetForestAtmosphere()
    {
        ConfigureSky();
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.48f, 0.56f, 0.43f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.62f, 0.70f, 0.59f);
        RenderSettings.fogDensity = 0.0016f;

        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        foreach (Light light in lights)
        {
            if (light.type != LightType.Directional || light.gameObject.name != "Directional Light")
                continue;

            light.color = new Color(1f, 0.91f, 0.76f);
            light.intensity = 1.45f;
            light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            light.shadows = LightShadows.Soft;
            break;
        }
    }

    private float PathDistance(float x, float z)
    {
        Vector2 point = new Vector2(x, z);
        float nearest = DistanceToPolyline(point, mainPath);
        foreach (Vector2[] path in secondaryPaths)
            nearest = Mathf.Min(nearest, DistanceToPolyline(point, path));
        return nearest;
    }

    private bool IsInClearing(float x, float z, float radiusAdjustment)
    {
        Vector2 point = new Vector2(x, z);
        for (int i = 0; i < clearings.Length; i++)
        {
            if (Vector2.Distance(point, clearings[i]) < ClearingRadii[i] + radiusAdjustment)
                return true;
        }
        return false;
    }

    private bool IsNearPond(float x, float z, float margin)
    {
        if (ponds == null)
            return false;
        for (int i = 0; i < ponds.Length; i++)
        {
            PondDefinition pond = ponds[i];
            float normalized = PondNormalizedDistance(x, z, pond);
            if ((normalized - 1f) * Mathf.Min(pond.RadiusX, pond.RadiusZ) < margin)
                return true;
        }
        return false;
    }

    private bool IsNearTree(float x, float z, float distance, Transform parent)
    {
        Vector3 point = new Vector3(x, 0f, z);
        for (int i = 0; i < parent.childCount; i++)
        {
            Vector3 treePosition = parent.GetChild(i).localPosition;
            treePosition.y = 0f;
            if ((treePosition - point).sqrMagnitude < distance * distance)
                return true;
        }
        return false;
    }

    private static float DistanceToPolyline(Vector2 point, Vector2[] points)
    {
        float nearest = float.MaxValue;
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector2 segment = points[i + 1] - points[i];
            float lengthSquared = segment.sqrMagnitude;
            float t = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(point - points[i], segment) / lengthSquared) : 0f;
            nearest = Mathf.Min(nearest, Vector2.Distance(point, points[i] + segment * t));
        }
        return nearest;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < generatedMeshes.Count; i++)
        {
            if (generatedMeshes[i] != null)
                Destroy(generatedMeshes[i]);
        }

        Destroy(terrainMaterial);
        Destroy(pathMaterial);
        Destroy(barkMaterial);
        Destroy(leafMaterial);
        Destroy(deepLeafMaterial);
        Destroy(brightLeafMaterial);
        Destroy(bushMaterial);
        Destroy(rockMaterial);
        Destroy(grassMaterial);
        Destroy(fernMaterial);
        Destroy(woodMaterial);
        Destroy(roofMaterial);
        Destroy(stoneFoundationMaterial);
        Destroy(windowMaterial);
        Destroy(cloudMaterial);
        Destroy(deepWaterMaterial);
        Destroy(shallowWaterMaterial);

        if (replacedSkybox && RenderSettings.skybox == forestSkybox)
            RenderSettings.skybox = originalSkybox;
        Destroy(forestSkybox);
    }
}
