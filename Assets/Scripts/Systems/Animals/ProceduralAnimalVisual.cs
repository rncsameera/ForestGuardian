using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class AnimalVisualRig
{
    public Transform root;
    public readonly List<Transform> legs = new List<Transform>();
    public readonly List<Transform> wings = new List<Transform>();
    public readonly List<AnimalTigerLegRig> tigerLegs = new List<AnimalTigerLegRig>();
    public readonly List<Transform> tigerTailSegments = new List<Transform>();
    public readonly List<AnimalTigerLegRig> articulatedLegs = new List<AnimalTigerLegRig>();
    public readonly List<Transform> articulatedTailSegments = new List<Transform>();
    public Mesh snakeBodyMesh;
    public Vector3[] snakeBodyVertices;
    public Vector3[] snakeAnimatedVertices;
    public Transform tigerBody;
    public Transform tigerHead;
    public Transform articulatedBody;
    public Transform articulatedHead;
    public Transform tail;
}

public sealed class AnimalTigerLegRig
{
    public Transform upper;
    public Transform lower;
}

/// <summary>Builds small low-poly animal silhouettes from shared primitive meshes.</summary>
public static class ProceduralAnimalVisual
{
    private static readonly Dictionary<PrimitiveType, Mesh> PrimitiveMeshes = new Dictionary<PrimitiveType, Mesh>();
    private static Material sharedMaterial;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public static AnimalVisualRig Build(Transform parent, AnimalSpecies species, float visualScale)
    {
        EnsureMaterial();
        AnimalVisualRig rig = new AnimalVisualRig();
        if (sharedMaterial == null)
            return rig;
        GameObject root = new GameObject("Animal Visual");
        root.transform.SetParent(parent, false);
        root.transform.localScale = Vector3.one * visualScale;
        rig.root = root.transform;

        switch (species)
        {
            case AnimalSpecies.Deer: BuildDeer(rig); break;
            case AnimalSpecies.Snake: BuildSnake(rig); break;
            case AnimalSpecies.Bird:
            case AnimalSpecies.RareForestBird:
            case AnimalSpecies.Dodo:
            case AnimalSpecies.PassengerPigeon: BuildBird(rig, species); break;
            default: BuildUnifiedAnimal(rig, species); break;
        }

        return rig;
    }

    private static void BuildDeer(AnimalVisualRig rig)
    {
        Color coat = new Color(0.48f, 0.27f, 0.13f);
        Color cream = new Color(0.83f, 0.72f, 0.53f);
        AddPart(rig, PrimitiveType.Sphere, "Deer Body", new Vector3(0f, 0.92f, 0f), Vector3.zero, new Vector3(0.56f, 0.62f, 1.12f), coat);
        AddPart(rig, PrimitiveType.Capsule, "Deer Neck", new Vector3(0f, 1.2f, 0.42f), new Vector3(-20f, 0f, 0f), new Vector3(0.28f, 0.52f, 0.3f), coat);
        AddHead(rig, new Vector3(0f, 1.55f, 0.68f), 0.31f, coat);
        AddPart(rig, PrimitiveType.Sphere, "Deer Muzzle", new Vector3(0f, 1.42f, 0.94f), Vector3.zero, new Vector3(0.19f, 0.16f, 0.29f), cream);
        AddQuadrupedLegs(rig, 0.26f, 0.68f, 0.39f, 0.28f, new Color(0.31f, 0.19f, 0.12f));
        AddTail(rig, new Vector3(0f, 1.02f, -0.58f), 0.18f, cream);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 basePoint = new Vector3(side * 0.16f, 1.78f, 0.63f);
            AddSegment(rig, "Antler Main", basePoint, basePoint + new Vector3(side * 0.13f, 0.4f, 0.02f), 0.055f, cream);
            AddSegment(rig, "Antler Branch", basePoint + new Vector3(side * 0.07f, 0.22f, 0f), basePoint + new Vector3(side * 0.36f, 0.35f, 0f), 0.04f, cream);
            AddSegment(rig, "Antler Tip", basePoint + new Vector3(side * 0.11f, 0.32f, 0f), basePoint + new Vector3(side * 0.08f, 0.55f, 0f), 0.035f, cream);
            AddPart(rig, PrimitiveType.Sphere, "Deer Ear", new Vector3(side * 0.27f, 1.73f, 0.55f), new Vector3(0f, 0f, side * -23f), new Vector3(0.12f, 0.07f, 0.27f), coat);
        }
    }

    // Non-deer wildlife is generated as one watertight, fused low-poly surface.
    // This avoids the disconnected primitive look and lets coat markings live on the mesh.
    private sealed class FormShape
    {
        public Vector3 A;
        public Vector3 B;
        public Vector3 Radii;
        public float Radius;
        public bool Capsule;
        public int MaterialRole;
        public int Bone;
    }

    private static readonly Dictionary<AnimalSpecies, Mesh> UnifiedMeshes = new Dictionary<AnimalSpecies, Mesh>();
    private static readonly Dictionary<Color, Material> ColorMaterials = new Dictionary<Color, Material>();
    private static readonly int[,] Tetrahedra =
    {
        { 0, 5, 1, 6 }, { 0, 1, 2, 6 }, { 0, 2, 3, 6 },
        { 0, 3, 7, 6 }, { 0, 7, 4, 6 }, { 0, 4, 5, 6 }
    };
    private static readonly int[,] TetraEdges = { { 0, 1 }, { 0, 2 }, { 0, 3 }, { 1, 2 }, { 1, 3 }, { 2, 3 } };
    private static readonly Vector3Int[] CubeCorners =
    {
        new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(1, 1, 0), new Vector3Int(0, 1, 0),
        new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 1), new Vector3Int(1, 1, 1), new Vector3Int(0, 1, 1)
    };

    private static void BuildUnifiedAnimal(AnimalVisualRig rig, AnimalSpecies species)
    {
        List<FormShape> forms = CreateAnimalForms(species);
        Transform[] bones = CreateAnimalSkeleton(rig, species);
        Mesh mesh;
        if (!UnifiedMeshes.TryGetValue(species, out mesh) || mesh == null)
        {
            mesh = BuildUnifiedMesh(species, forms);
            UnifiedMeshes[species] = mesh;
        }

        Material[] materials = GetAnimalMaterials(species);
        SkinnedMeshRenderer skin = rig.root.gameObject.AddComponent<SkinnedMeshRenderer>();
        skin.sharedMesh = mesh;
        skin.sharedMaterials = materials;
        skin.bones = bones;
        skin.rootBone = rig.root;
        skin.localBounds = mesh.bounds;
        skin.updateWhenOffscreen = true;
        skin.shadowCastingMode = ShadowCastingMode.On;
        skin.receiveShadows = true;
        if (mesh.bindposes == null || mesh.bindposes.Length != bones.Length)
        {
            Matrix4x4[] bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                bindposes[i] = bones[i].worldToLocalMatrix * rig.root.localToWorldMatrix;
            mesh.bindposes = bindposes;
        }
    }

    private static void BuildBird(AnimalVisualRig rig, AnimalSpecies species)
    {
        List<FormShape> forms = CreateAnimalForms(species);
        Mesh mesh;
        if (!UnifiedMeshes.TryGetValue(species, out mesh) || mesh == null)
        {
            mesh = BuildUnifiedMesh(species, forms);
            UnifiedMeshes[species] = mesh;
        }

        if (species == AnimalSpecies.Dodo)
        {
            Transform[] bones = CreateAnimalSkeleton(rig, species);
            SkinnedMeshRenderer bodyRenderer = rig.root.gameObject.AddComponent<SkinnedMeshRenderer>();
            bodyRenderer.sharedMesh = mesh;
            bodyRenderer.sharedMaterials = GetAnimalMaterials(species);
            bodyRenderer.bones = bones;
            bodyRenderer.rootBone = rig.root;
            bodyRenderer.localBounds = mesh.bounds;
            bodyRenderer.updateWhenOffscreen = true;
            bodyRenderer.shadowCastingMode = ShadowCastingMode.On;
            bodyRenderer.receiveShadows = true;
            if (mesh.bindposes == null || mesh.bindposes.Length != bones.Length)
            {
                Matrix4x4[] bindposes = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                    bindposes[i] = bones[i].worldToLocalMatrix * rig.root.localToWorldMatrix;
                mesh.bindposes = bindposes;
            }
        }
        else
        {
            GameObject body = new GameObject(species + " Fused Low Poly Body");
            body.transform.SetParent(rig.root, false);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer bodyRenderer = body.AddComponent<MeshRenderer>();
            bodyRenderer.sharedMaterials = GetAnimalMaterials(species);
            bodyRenderer.shadowCastingMode = ShadowCastingMode.On;
            bodyRenderer.receiveShadows = true;
        }

        Material[] birdMaterials = GetAnimalMaterials(species);
        Material wingMaterial = species == AnimalSpecies.PassengerPigeon ? birdMaterials[0] : birdMaterials[1];
        rig.wings.Add(CreateBirdWing(rig.root, -1, wingMaterial, species));
        rig.wings.Add(CreateBirdWing(rig.root, 1, wingMaterial, species));
    }

    private static Transform CreateBirdWing(Transform root, int side, Material material, AnimalSpecies species)
    {
        GameObject wing = new GameObject((side < 0 ? "Left" : "Right") + " Bird Wing");
        Transform pivot = wing.transform;
        pivot.SetParent(root, false);
        pivot.localPosition = new Vector3(side * 0.17f, species == AnimalSpecies.Dodo ? 0.75f : 0.49f, 0.02f);

        // A tapered, feather-notched silhouette, extruded as one mesh so it hinges
        // from the shoulder as an attached wing rather than a flat billboard.
        float wingScale = species == AnimalSpecies.Dodo ? 0.48f : (species == AnimalSpecies.PassengerPigeon ? 1.18f : 1f);
        Vector3[] outline =
        {
            new Vector3(0f, 0f, 0.19f), new Vector3(0.28f, 0f, 0.24f),
            new Vector3(0.76f, 0f, 0.03f), new Vector3(0.68f, 0f, -0.10f),
            new Vector3(0.55f, 0f, -0.06f), new Vector3(0.47f, 0f, -0.25f),
            new Vector3(0.34f, 0f, -0.19f), new Vector3(0.24f, 0f, -0.34f),
            new Vector3(0.12f, 0f, -0.25f), new Vector3(0f, 0f, -0.16f)
        };
        for (int i = 0; i < outline.Length; i++)
        {
            outline[i].x *= side * wingScale;
            outline[i].z *= wingScale;
        }

        Vector3[] vertices = new Vector3[outline.Length * 2 + 2];
        Vector3 center = Vector3.zero;
        for (int i = 0; i < outline.Length; i++)
        {
            vertices[i] = outline[i] + Vector3.up * 0.025f;
            vertices[i + outline.Length] = outline[i] - Vector3.up * 0.025f;
            center += outline[i];
        }
        center /= outline.Length;
        int topCenter = vertices.Length - 2;
        int bottomCenter = vertices.Length - 1;
        vertices[topCenter] = center + Vector3.up * 0.025f;
        vertices[bottomCenter] = center - Vector3.up * 0.025f;

        List<int> triangles = new List<int>(outline.Length * 12);
        for (int i = 0; i < outline.Length; i++)
        {
            int next = (i + 1) % outline.Length;
            triangles.Add(topCenter); triangles.Add(i); triangles.Add(next);
            triangles.Add(bottomCenter); triangles.Add(next + outline.Length); triangles.Add(i + outline.Length);
            triangles.Add(i); triangles.Add(i + outline.Length); triangles.Add(next + outline.Length);
            triangles.Add(i); triangles.Add(next + outline.Length); triangles.Add(next);
        }

        if (side < 0)
        {
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int swap = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = swap;
            }
        }

        Mesh wingMesh = new Mesh { name = species + (side < 0 ? " Left Wing" : " Right Wing") };
        wingMesh.vertices = vertices;
        wingMesh.triangles = triangles.ToArray();
        wingMesh.RecalculateNormals();
        wingMesh.RecalculateBounds();
        wing.AddComponent<MeshFilter>().sharedMesh = wingMesh;
        MeshRenderer renderer = wing.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        return pivot;
    }

    private static List<FormShape> CreateAnimalForms(AnimalSpecies species)
    {
        List<FormShape> f = new List<FormShape>(28);
        switch (species)
        {
            case AnimalSpecies.Rabbit:
                Ellipsoid(f, new Vector3(0f, 0.39f, -0.04f), new Vector3(0.29f, 0.27f, 0.42f));
                Ellipsoid(f, new Vector3(0f, 0.39f, -0.28f), new Vector3(0.29f, 0.26f, 0.3f));
                Ellipsoid(f, new Vector3(0f, 0.43f, 0.24f), new Vector3(0.23f, 0.23f, 0.27f));
                Ellipsoid(f, new Vector3(0f, 0.65f, 0.47f), new Vector3(0.21f, 0.2f, 0.22f), 0, 1);
                Ellipsoid(f, new Vector3(0f, 0.58f, 0.64f), new Vector3(0.13f, 0.095f, 0.16f), 1, 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    Ellipsoid(f, new Vector3(side * 0.12f, 0.94f, 0.46f), new Vector3(0.075f, 0.29f, 0.085f), 0, 1);
                    Ellipsoid(f, new Vector3(side * 0.19f, 0.69f, 0.57f), new Vector3(0.035f, 0.035f, 0.025f), 2, 1);
                    int foreUpper = side < 0 ? 2 : 4;
                    int hindUpper = side < 0 ? 6 : 8;
                    Capsule(f, new Vector3(side * 0.15f, 0.28f, 0.25f), new Vector3(side * 0.16f, 0.08f, 0.28f), 0.075f, 0, foreUpper);
                    Ellipsoid(f, new Vector3(side * 0.19f, 0.28f, -0.31f), new Vector3(0.1f, 0.19f, 0.12f), 0, hindUpper);
                    Capsule(f, new Vector3(side * 0.19f, 0.22f, -0.31f), new Vector3(side * 0.2f, 0.07f, -0.23f), 0.075f, 0, hindUpper + 1);
                }
                Ellipsoid(f, new Vector3(0f, 0.42f, -0.53f), Vector3.one * 0.13f, 1, 10);
                break;

            case AnimalSpecies.Dog:
                // Longer, leaner domestic dog profile with a narrow chest, tucked belly
                // and tapered muzzle. Four slim limbs remain fused to the shoulder/hip.
                Ellipsoid(f, new Vector3(0f, 0.77f, -0.08f), new Vector3(0.33f, 0.29f, 0.76f));
                Ellipsoid(f, new Vector3(0f, 0.79f, -0.51f), new Vector3(0.32f, 0.28f, 0.43f));
                Ellipsoid(f, new Vector3(0f, 0.84f, 0.43f), new Vector3(0.32f, 0.31f, 0.38f));
                Capsule(f, new Vector3(0f, 0.85f, 0.48f), new Vector3(0f, 1.04f, 0.77f), 0.17f, 0, 1);
                Ellipsoid(f, new Vector3(0f, 1.1f, 0.82f), new Vector3(0.25f, 0.24f, 0.29f), 0, 1);
                Ellipsoid(f, new Vector3(0f, 1.0f, 1.04f), new Vector3(0.145f, 0.12f, 0.32f), 1, 1);
                Ellipsoid(f, new Vector3(0f, 1.015f, 1.31f), new Vector3(0.085f, 0.065f, 0.07f), 2, 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    Ellipsoid(f, new Vector3(side * 0.2f, 1.2f, 0.7f), new Vector3(0.12f, 0.17f, 0.13f), 0, 1);
                    Ellipsoid(f, new Vector3(side * 0.2f, 1.12f, 1.04f), new Vector3(0.045f, 0.045f, 0.035f), 2, 1);
                    Ellipsoid(f, new Vector3(side * 0.29f, 0.86f, 0.63f), new Vector3(0.095f, 0.15f, 0.19f), 1);
                    Ellipsoid(f, new Vector3(side * 0.27f, 0.81f, -0.23f), new Vector3(0.09f, 0.15f, 0.19f), 1);
                    int frontUpper = side < 0 ? 2 : 4;
                    int rearUpper = side < 0 ? 6 : 8;
                    Capsule(f, new Vector3(side * 0.27f, 0.67f, 0.49f), new Vector3(side * 0.27f, 0.39f, 0.49f), 0.082f, 0, frontUpper);
                    Capsule(f, new Vector3(side * 0.27f, 0.39f, 0.49f), new Vector3(side * 0.28f, 0.12f, 0.52f), 0.065f, 0, frontUpper + 1);
                    Capsule(f, new Vector3(side * 0.27f, 0.65f, -0.46f), new Vector3(side * 0.27f, 0.38f, -0.46f), 0.086f, 0, rearUpper);
                    Capsule(f, new Vector3(side * 0.27f, 0.38f, -0.46f), new Vector3(side * 0.28f, 0.12f, -0.42f), 0.068f, 0, rearUpper + 1);
                    Ellipsoid(f, new Vector3(side * 0.28f, 0.105f, 0.59f), new Vector3(0.12f, 0.075f, 0.16f), 2);
                    Ellipsoid(f, new Vector3(side * 0.28f, 0.105f, -0.34f), new Vector3(0.12f, 0.075f, 0.15f), 2);
                }
                Capsule(f, new Vector3(0f, 0.83f, -0.71f), new Vector3(0.1f, 1.03f, -1.05f), 0.075f, 0, 10);
                Ellipsoid(f, new Vector3(0.12f, 1.04f, -1.08f), new Vector3(0.07f, 0.08f, 0.085f), 0, 11);
                break;

            case AnimalSpecies.Pig:
                Ellipsoid(f, new Vector3(0f, 0.62f, -0.03f), new Vector3(0.43f, 0.4f, 0.62f));
                Ellipsoid(f, new Vector3(0f, 0.64f, 0.36f), new Vector3(0.4f, 0.36f, 0.42f));
                Ellipsoid(f, new Vector3(0f, 0.72f, 0.65f), new Vector3(0.32f, 0.29f, 0.32f), 0, 1);
                Ellipsoid(f, new Vector3(0f, 0.62f, 0.91f), new Vector3(0.24f, 0.17f, 0.16f), 1, 1);
                Ellipsoid(f, new Vector3(0f, 0.63f, 1.045f), new Vector3(0.12f, 0.055f, 0.025f), 2, 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    Ellipsoid(f, new Vector3(side * 0.23f, 0.89f, 0.68f), new Vector3(0.12f, 0.17f, 0.11f), 0, 1);
                    int frontLeg = side < 0 ? 2 : 4;
                    int rearLeg = side < 0 ? 6 : 8;
                    Capsule(f, new Vector3(side * 0.29f, 0.39f, 0.3f), new Vector3(side * 0.29f, 0.23f, 0.3f), 0.095f, 0, frontLeg);
                    Capsule(f, new Vector3(side * 0.29f, 0.23f, 0.3f), new Vector3(side * 0.3f, 0.1f, 0.3f), 0.073f, 0, frontLeg + 1);
                    Capsule(f, new Vector3(side * 0.3f, 0.37f, -0.39f), new Vector3(side * 0.3f, 0.22f, -0.39f), 0.095f, 0, rearLeg);
                    Capsule(f, new Vector3(side * 0.3f, 0.22f, -0.39f), new Vector3(side * 0.3f, 0.1f, -0.38f), 0.073f, 0, rearLeg + 1);
                    Ellipsoid(f, new Vector3(side * 0.3f, 0.1f, 0.34f), new Vector3(0.13f, 0.07f, 0.16f), 2);
                    Ellipsoid(f, new Vector3(side * 0.3f, 0.1f, -0.34f), new Vector3(0.13f, 0.07f, 0.16f), 2);
                }
                Capsule(f, new Vector3(0f, 0.72f, -0.58f), new Vector3(0.1f, 0.82f, -0.66f), 0.055f, 0, 10);
                Capsule(f, new Vector3(0.1f, 0.82f, -0.66f), new Vector3(0.17f, 0.87f, -0.73f), 0.045f, 0, 11);
                Capsule(f, new Vector3(0.17f, 0.87f, -0.73f), new Vector3(0.11f, 0.82f, -0.76f), 0.04f, 0, 12);
                break;

            case AnimalSpecies.Lion:
            case AnimalSpecies.Tiger:
                bool tiger = species == AnimalSpecies.Tiger;
                int headBone = 1;
                int bodyBone = 14;
                // Athletic feline body: deep chest, narrower waist and hindquarters.
                Ellipsoid(f, new Vector3(0f, 1.04f, -0.03f), new Vector3(0.59f, 0.44f, 1.34f), 0, bodyBone);
                Ellipsoid(f, new Vector3(0f, 1.06f, 0.48f), new Vector3(0.58f, 0.43f, 0.78f), 0, bodyBone);
                Ellipsoid(f, new Vector3(0f, 1.0f, -0.58f), new Vector3(0.55f, 0.41f, 0.73f), 0, bodyBone);
                if (!tiger)
                    Ellipsoid(f, new Vector3(0f, 1.27f, 0.78f), new Vector3(0.48f, 0.47f, 0.46f), 3, 1);
                Ellipsoid(f, new Vector3(0f, 1.39f, 0.98f), new Vector3(0.36f, 0.3f, 0.38f), 0, headBone);
                Ellipsoid(f, new Vector3(0f, 1.25f, 1.2f), new Vector3(0.28f, 0.19f, 0.31f), 1, headBone);
                Ellipsoid(f, new Vector3(0f, 1.29f, 1.45f), new Vector3(0.12f, 0.085f, 0.075f), 2, headBone);
                for (int side = -1; side <= 1; side += 2)
                {
                    Ellipsoid(f, new Vector3(side * 0.27f, 1.59f, 0.83f), new Vector3(0.15f, 0.15f, 0.13f), 0, headBone);
                    Ellipsoid(f, new Vector3(side * 0.25f, 1.41f, 1.18f), new Vector3(0.05f, 0.05f, 0.04f), 2, headBone);
                    int upper = side < 0 ? 2 : 4;
                    int lower = upper + 1;
                    AddCatLeg(f, side, 0.58f, true, upper, lower);
                    upper = side < 0 ? 6 : 8;
                    lower = upper + 1;
                    AddCatLeg(f, side, -0.69f, false, upper, lower);
                }
                AddCatTail(f, tiger);
                break;

            case AnimalSpecies.Sheep:
                Ellipsoid(f, new Vector3(0f, 0.78f, -0.03f), new Vector3(0.55f, 0.58f, 0.7f));
                // Fused wool lobes round the outline without detached tuft objects.
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 3; i++)
                        Ellipsoid(f, new Vector3(side * 0.38f, 0.68f + i * 0.2f, -0.38f + i * 0.32f), new Vector3(0.22f, 0.25f, 0.24f));
                Ellipsoid(f, new Vector3(0f, 0.93f, 0.53f), new Vector3(0.3f, 0.34f, 0.34f), 1, 1);
                Ellipsoid(f, new Vector3(0f, 0.78f, 0.79f), new Vector3(0.16f, 0.13f, 0.2f), 1, 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    Ellipsoid(f, new Vector3(side * 0.24f, 1.16f, 0.47f), new Vector3(0.1f, 0.13f, 0.08f), 0, 1);
                    Ellipsoid(f, new Vector3(side * 0.23f, 0.98f, 0.72f), new Vector3(0.035f, 0.035f, 0.03f), 2, 1);
                }
                AddHoovedLegs(f, 0.52f, 0.28f, 0.22f, -0.38f);
                Capsule(f, new Vector3(0f, 0.82f, -0.58f), new Vector3(0f, 0.66f, -0.68f), 0.065f, 0, 10);
                break;

            case AnimalSpecies.Goat:
                Ellipsoid(f, new Vector3(0f, 0.83f, -0.04f), new Vector3(0.48f, 0.52f, 0.7f));
                Ellipsoid(f, new Vector3(0f, 0.91f, 0.36f), new Vector3(0.39f, 0.48f, 0.4f));
                Capsule(f, new Vector3(0f, 1.03f, 0.48f), new Vector3(0f, 1.38f, 0.73f), 0.21f, 0, 1);
                Ellipsoid(f, new Vector3(0f, 1.52f, 0.8f), new Vector3(0.25f, 0.27f, 0.28f), 0, 1);
                Ellipsoid(f, new Vector3(0f, 1.4f, 1.02f), new Vector3(0.15f, 0.13f, 0.2f), 1, 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    Capsule(f, new Vector3(side * 0.13f, 1.69f, 0.79f), new Vector3(side * 0.18f, 2.03f, 0.72f), 0.055f, 2, 1);
                    Ellipsoid(f, new Vector3(side * 0.32f, 1.54f, 0.72f), new Vector3(0.2f, 0.075f, 0.11f), 0, 1);
                    Ellipsoid(f, new Vector3(side * 0.2f, 1.57f, 0.99f), new Vector3(0.04f, 0.04f, 0.035f), 2, 1);
                }
                Capsule(f, new Vector3(0f, 1.33f, 0.92f), new Vector3(0f, 1.13f, 1.03f), 0.07f, 2, 1);
                AddHoovedLegs(f, 0.67f, 0.24f, 0.35f, -0.4f);
                Capsule(f, new Vector3(0f, 0.95f, -0.65f), new Vector3(0f, 0.72f, -0.79f), 0.06f, 0, 10);
                break;

            case AnimalSpecies.Giraffe:
                Ellipsoid(f, new Vector3(0f, 1.1f, -0.05f), new Vector3(0.48f, 0.5f, 0.82f));
                AddHoovedLegs(f, 1.1f, 0.21f, 0.5f, -0.52f);
                Capsule(f, new Vector3(0f, 1.3f, 0.42f), new Vector3(0f, 2.62f, 0.6f), 0.17f, 0, 1);
                Ellipsoid(f, new Vector3(0f, 2.73f, 0.68f), new Vector3(0.25f, 0.3f, 0.27f), 0, 1);
                Ellipsoid(f, new Vector3(0f, 2.62f, 0.91f), new Vector3(0.18f, 0.15f, 0.25f), 1, 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    Capsule(f, new Vector3(side * 0.13f, 2.94f, 0.65f), new Vector3(side * 0.14f, 3.17f, 0.65f), 0.055f, 2, 1);
                    Ellipsoid(f, new Vector3(side * 0.14f, 3.2f, 0.65f), new Vector3(0.09f, 0.08f, 0.09f), 2, 1);
                    Ellipsoid(f, new Vector3(side * 0.23f, 2.78f, 0.89f), new Vector3(0.04f, 0.04f, 0.035f), 2, 1);
                }
                Capsule(f, new Vector3(0f, 1.45f, -0.84f), new Vector3(0f, 1.12f, -1.02f), 0.055f, 0, 10);
                break;

            case AnimalSpecies.Bird:
            case AnimalSpecies.RareForestBird:
            case AnimalSpecies.PassengerPigeon:
                bool rare = species == AnimalSpecies.RareForestBird;
                bool pigeon = species == AnimalSpecies.PassengerPigeon;
                Ellipsoid(f, new Vector3(0f, 0.42f, -0.02f), new Vector3(pigeon ? 0.28f : 0.31f, 0.3f, pigeon ? 0.58f : 0.48f));
                Ellipsoid(f, new Vector3(0f, 0.62f, 0.3f), new Vector3(0.22f, 0.23f, 0.24f), pigeon ? 1 : 0);
                Ellipsoid(f, new Vector3(0f, 0.53f, 0.52f), new Vector3(pigeon ? 0.09f : 0.1f, 0.075f, 0.24f), 2);
                for (int side = -1; side <= 1; side += 2)
                {
                    Ellipsoid(f, new Vector3(side * 0.12f, 0.15f, -0.45f), new Vector3(0.09f, 0.11f, 0.34f), 1);
                    Capsule(f, new Vector3(side * 0.1f, 0.2f, 0.02f), new Vector3(side * 0.1f, 0.04f, 0.05f), 0.035f, 2);
                    Capsule(f, new Vector3(side * 0.1f, 0.05f, 0.05f), new Vector3(side * 0.14f, 0.035f, 0.13f), 0.025f, 2);
                    Ellipsoid(f, new Vector3(side * 0.15f, 0.64f, 0.39f), new Vector3(0.035f, 0.035f, 0.03f), 2);
                }
                if (rare)
                    Ellipsoid(f, new Vector3(0f, 0.78f, 0.25f), new Vector3(0.13f, 0.2f, 0.12f), 1);
                if (pigeon)
                    Ellipsoid(f, new Vector3(0f, 0.53f, 0.17f), new Vector3(0.2f, 0.2f, 0.18f), 1);
                break;

            case AnimalSpecies.Dodo:
                Ellipsoid(f, new Vector3(0f, 0.69f, -0.04f), new Vector3(0.43f, 0.52f, 0.59f));
                Ellipsoid(f, new Vector3(0f, 0.89f, 0.34f), new Vector3(0.29f, 0.36f, 0.34f), 0, 1);
                Capsule(f, new Vector3(0f, 0.84f, 0.42f), new Vector3(0f, 1.24f, 0.58f), 0.16f, 0, 1);
                Ellipsoid(f, new Vector3(0f, 1.34f, 0.63f), new Vector3(0.24f, 0.23f, 0.25f), 0, 1);
                Ellipsoid(f, new Vector3(0f, 1.25f, 0.89f), new Vector3(0.18f, 0.13f, 0.24f), 1, 1);
                Capsule(f, new Vector3(0f, 1.27f, 1.02f), new Vector3(0f, 1.1f, 1.16f), 0.09f, 2, 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    Ellipsoid(f, new Vector3(side * 0.19f, 1.39f, 0.57f), new Vector3(0.035f, 0.035f, 0.03f), 2, 1);
                    int legBone = side < 0 ? 2 : 4;
                    Capsule(f, new Vector3(side * 0.2f, 0.39f, -0.03f), new Vector3(side * 0.2f, 0.22f, -0.03f), 0.085f, 2, legBone);
                    Capsule(f, new Vector3(side * 0.2f, 0.22f, -0.03f), new Vector3(side * 0.21f, 0.09f, -0.02f), 0.062f, 2, legBone + 1);
                    Capsule(f, new Vector3(side * 0.21f, 0.09f, -0.02f), new Vector3(side * 0.25f, 0.055f, 0.2f), 0.055f, 2, legBone + 1);
                }
                Ellipsoid(f, new Vector3(0f, 0.62f, -0.62f), new Vector3(0.18f, 0.14f, 0.2f), 1, 10);
                break;

            case AnimalSpecies.Capybara:
                Ellipsoid(f, new Vector3(0f, 0.57f, -0.02f), new Vector3(0.43f, 0.34f, 0.69f));
                Ellipsoid(f, new Vector3(0f, 0.62f, 0.48f), new Vector3(0.31f, 0.3f, 0.35f), 0, 1);
                Ellipsoid(f, new Vector3(0f, 0.52f, 0.72f), new Vector3(0.24f, 0.17f, 0.27f), 1, 1);
                Ellipsoid(f, new Vector3(0f, 0.56f, 0.91f), new Vector3(0.16f, 0.09f, 0.11f), 2, 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    Ellipsoid(f, new Vector3(side * 0.26f, 0.86f, 0.58f), new Vector3(0.075f, 0.09f, 0.08f), 0, 1);
                    Ellipsoid(f, new Vector3(side * 0.2f, 0.69f, 0.77f), new Vector3(0.035f, 0.035f, 0.03f), 2, 1);
                    Capsule(f, new Vector3(side * 0.29f, 0.37f, 0.39f), new Vector3(side * 0.3f, 0.11f, 0.4f), 0.085f, 0, side < 0 ? 2 : 4);
                    Capsule(f, new Vector3(side * 0.3f, 0.36f, -0.43f), new Vector3(side * 0.31f, 0.11f, -0.42f), 0.09f, 0, side < 0 ? 6 : 8);
                }
                Ellipsoid(f, new Vector3(0f, 0.42f, -0.66f), new Vector3(0.075f, 0.08f, 0.12f), 0, 10);
                break;

            case AnimalSpecies.Cow:
                Ellipsoid(f, new Vector3(0f, 0.87f, -0.04f), new Vector3(0.52f, 0.48f, 0.84f));
                Ellipsoid(f, new Vector3(0f, 0.9f, 0.54f), new Vector3(0.4f, 0.43f, 0.43f), 0, 1);
                Ellipsoid(f, new Vector3(0f, 1.0f, 0.83f), new Vector3(0.3f, 0.3f, 0.34f), 0, 1);
                Ellipsoid(f, new Vector3(0f, 0.84f, 1.08f), new Vector3(0.2f, 0.15f, 0.2f), 1, 1);
                Ellipsoid(f, new Vector3(0f, 0.65f, -0.05f), new Vector3(0.35f, 0.24f, 0.46f), 1, 14);
                Ellipsoid(f, new Vector3(-0.36f, 1.13f, -0.3f), new Vector3(0.2f, 0.24f, 0.28f), 3, 14);
                Ellipsoid(f, new Vector3(0.34f, 0.96f, 0.15f), new Vector3(0.22f, 0.25f, 0.23f), 3, 14);
                for (int side = -1; side <= 1; side += 2)
                {
                    Ellipsoid(f, new Vector3(side * 0.43f, 1.1f, 0.78f), new Vector3(0.22f, 0.09f, 0.18f), 0, 1);
                    Ellipsoid(f, new Vector3(side * 0.17f, 1.21f, 0.95f), new Vector3(0.07f, 0.06f, 0.07f), 2, 1);
                    int front = side < 0 ? 2 : 4, rear = side < 0 ? 6 : 8;
                    Capsule(f, new Vector3(side * 0.34f, 0.61f, 0.49f), new Vector3(side * 0.35f, 0.11f, 0.49f), 0.11f, 0, front);
                    Capsule(f, new Vector3(side * 0.37f, 0.59f, -0.51f), new Vector3(side * 0.38f, 0.11f, -0.51f), 0.12f, 0, rear);
                }
                Capsule(f, new Vector3(0f, 0.97f, -0.78f), new Vector3(0.02f, 0.62f, -1.0f), 0.06f, 0, 10);
                Ellipsoid(f, new Vector3(0.02f, 0.53f, -1.03f), new Vector3(0.1f, 0.08f, 0.13f), 2, 11);
                break;

            case AnimalSpecies.Elephant:
                Ellipsoid(f, new Vector3(0f, 1.04f, -0.02f), new Vector3(0.76f, 0.72f, 1.0f));
                Ellipsoid(f, new Vector3(0f, 1.25f, 0.72f), new Vector3(0.55f, 0.57f, 0.55f), 0, 1);
                Ellipsoid(f, new Vector3(0f, 1.18f, 1.05f), new Vector3(0.4f, 0.32f, 0.37f), 1, 1);
                Capsule(f, new Vector3(0f, 1.2f, 1.28f), new Vector3(0f, 0.6f, 1.55f), 0.15f, 0, 1);
                Capsule(f, new Vector3(0f, 0.6f, 1.55f), new Vector3(0.05f, 0.31f, 1.48f), 0.12f, 0, 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    Ellipsoid(f, new Vector3(side * 0.56f, 1.32f, 0.81f), new Vector3(0.3f, 0.47f, 0.17f), 0, 1);
                    Ellipsoid(f, new Vector3(side * 0.12f, 1.4f, 1.25f), new Vector3(0.07f, 0.07f, 0.08f), 2, 1);
                    int front = side < 0 ? 2 : 4, rear = side < 0 ? 6 : 8;
                    Capsule(f, new Vector3(side * 0.48f, 0.72f, 0.52f), new Vector3(side * 0.49f, 0.12f, 0.52f), 0.19f, 0, front);
                    Capsule(f, new Vector3(side * 0.5f, 0.72f, -0.55f), new Vector3(side * 0.51f, 0.12f, -0.55f), 0.2f, 0, rear);
                }
                Capsule(f, new Vector3(0f, 0.98f, -0.92f), new Vector3(0f, 0.62f, -1.15f), 0.07f, 0, 10);
                break;
        }
        return f;
    }

    private static void AddCatLeg(List<FormShape> forms, int side, float z, bool front, int upperBone, int lowerBone)
    {
        float x = side * 0.43f;
        float hipY = front ? 0.95f : 0.9f;
        float kneeY = front ? 0.49f : 0.45f;
        Capsule(forms, new Vector3(x, hipY, z), new Vector3(x, kneeY, z + (front ? 0.02f : 0.08f)), 0.135f, 0, upperBone);
        Capsule(forms, new Vector3(x, kneeY + 0.03f, z + (front ? 0.02f : 0.08f)), new Vector3(x, 0.12f, z + 0.12f), 0.1f, 0, lowerBone);
        Ellipsoid(forms, new Vector3(x, 0.105f, z + 0.19f), new Vector3(0.16f, 0.09f, 0.22f), 1, lowerBone);
    }

    private static void AddCatTail(List<FormShape> forms, bool tiger)
    {
        Vector3[] points = { new Vector3(0f, 0.98f, -1.1f), new Vector3(0.02f, 0.91f, -1.43f), new Vector3(0.09f, 0.77f, -1.74f), new Vector3(0.19f, 0.63f, -2.03f), new Vector3(0.27f, 0.55f, -2.28f) };
        for (int i = 0; i < points.Length - 1; i++)
            Capsule(forms, points[i], points[i + 1], i == points.Length - 2 ? 0.065f : 0.075f,
                tiger && i == points.Length - 2 ? 2 : 0, 10 + i);
    }

    private static void AddHoovedLegs(List<FormShape> forms, float hipY, float halfWidth, float frontZ, float backZ)
    {
        foreach (int side in new[] { -1, 1 })
        {
            float[] zPositions = { frontZ, backZ };
            for (int leg = 0; leg < zPositions.Length; leg++)
            {
                float z = zPositions[leg];
                float x = side * halfWidth;
                int upper = leg == 0 ? (side < 0 ? 2 : 4) : (side < 0 ? 6 : 8);
                float kneeY = Mathf.Max(0.28f, hipY * 0.55f);
                float upperRadius = Mathf.Min(0.13f, hipY * 0.12f);
                Capsule(forms, new Vector3(x, hipY, z), new Vector3(x, kneeY, z + 0.01f), upperRadius, 0, upper);
                Capsule(forms, new Vector3(x, kneeY, z + 0.01f), new Vector3(x, 0.12f, z + 0.015f), upperRadius * 0.68f, 0, upper + 1);
                Ellipsoid(forms, new Vector3(x, 0.095f, z + 0.045f), new Vector3(0.13f, 0.09f, 0.18f), 2, upper + 1);
            }
        }
    }

    private static void Ellipsoid(List<FormShape> forms, Vector3 center, Vector3 radii, int role = 0, int bone = 0)
    {
        forms.Add(new FormShape { A = center, B = center, Radii = radii, MaterialRole = role, Bone = bone == 0 ? 14 : bone });
    }

    private static void Capsule(List<FormShape> forms, Vector3 a, Vector3 b, float radius, int role = 0, int bone = 0)
    {
        forms.Add(new FormShape { A = a, B = b, Radius = radius, Capsule = true, MaterialRole = role, Bone = bone == 0 ? 14 : bone });
    }

    private static Transform[] CreateAnimalSkeleton(AnimalVisualRig rig, AnimalSpecies species)
    {
        Transform[] bones = new Transform[15];
        bones[0] = rig.root;
        Vector3 head = new Vector3(0f, 1f, 0.6f);
        Vector3 body = new Vector3(0f, 0.75f, 0f);
        Vector3[] hips = { new Vector3(-0.25f, 0.5f, 0.3f), new Vector3(0.25f, 0.5f, 0.3f),
            new Vector3(-0.25f, 0.5f, -0.35f), new Vector3(0.25f, 0.5f, -0.35f) };
        float frontKnee = 0.25f, rearKnee = 0.25f;
        Vector3[] tailPoints = { new Vector3(0f, 0.7f, -0.5f), new Vector3(0f, 0.65f, -0.75f),
            new Vector3(0f, 0.58f, -1f), new Vector3(0f, 0.5f, -1.2f), new Vector3(0f, 0.44f, -1.35f) };
        switch (species)
        {
            case AnimalSpecies.Dodo:
                head = new Vector3(0f, 1.34f, 0.63f); body = new Vector3(0f, 0.69f, -0.04f);
                hips = new[] { new Vector3(-0.2f, 0.39f, -0.03f), new Vector3(0.2f, 0.39f, -0.03f),
                    new Vector3(-0.2f, 0.39f, -0.03f), new Vector3(0.2f, 0.39f, -0.03f) };
                frontKnee = 0.22f; rearKnee = 0.22f;
                tailPoints = new[] { new Vector3(0f, 0.62f, -0.62f), new Vector3(0f, 0.58f, -0.72f), new Vector3(0f, 0.55f, -0.78f), new Vector3(0f, 0.53f, -0.82f), new Vector3(0f, 0.52f, -0.84f) };
                break;
            case AnimalSpecies.Rabbit:
                head = new Vector3(0f, 0.65f, 0.47f); body = new Vector3(0f, 0.39f, -0.04f);
                hips = new[] { new Vector3(-0.15f, 0.28f, 0.25f), new Vector3(0.15f, 0.28f, 0.25f), new Vector3(-0.19f, 0.28f, -0.31f), new Vector3(0.19f, 0.28f, -0.31f) };
                frontKnee = 0.1f; rearKnee = 0.1f;
                tailPoints = new[] { new Vector3(0f, 0.42f, -0.48f), new Vector3(0f, 0.4f, -0.62f), new Vector3(0f, 0.38f, -0.7f), new Vector3(0f, 0.36f, -0.75f), new Vector3(0f, 0.35f, -0.78f) };
                break;
            case AnimalSpecies.Dog:
                head = new Vector3(0f, 1.1f, 0.82f); body = new Vector3(0f, 0.77f, -0.08f);
                hips = new[] { new Vector3(-0.27f, 0.67f, 0.49f), new Vector3(0.27f, 0.67f, 0.49f), new Vector3(-0.27f, 0.65f, -0.46f), new Vector3(0.27f, 0.65f, -0.46f) };
                frontKnee = 0.39f; rearKnee = 0.38f;
                tailPoints = new[] { new Vector3(0f, 0.83f, -0.71f), new Vector3(0.03f, 0.9f, -0.84f), new Vector3(0.07f, 0.98f, -0.96f), new Vector3(0.1f, 1.03f, -1.05f), new Vector3(0.12f, 1.04f, -1.08f) };
                break;
            case AnimalSpecies.Pig:
                head = new Vector3(0f, 0.72f, 0.65f); body = new Vector3(0f, 0.62f, -0.03f);
                hips = new[] { new Vector3(-0.29f, 0.39f, 0.3f), new Vector3(0.29f, 0.39f, 0.3f), new Vector3(-0.3f, 0.37f, -0.39f), new Vector3(0.3f, 0.37f, -0.39f) };
                frontKnee = 0.23f; rearKnee = 0.22f;
                tailPoints = new[] { new Vector3(0f, 0.72f, -0.58f), new Vector3(0.1f, 0.82f, -0.66f), new Vector3(0.17f, 0.87f, -0.73f), new Vector3(0.11f, 0.82f, -0.76f), new Vector3(0.08f, 0.82f, -0.75f) };
                break;
            case AnimalSpecies.Lion:
            case AnimalSpecies.Tiger:
                head = new Vector3(0f, 1.39f, 0.98f); body = new Vector3(0f, 1.04f, -0.03f);
                hips = new[] { new Vector3(-0.43f, 0.95f, 0.58f), new Vector3(0.43f, 0.95f, 0.58f), new Vector3(-0.43f, 0.9f, -0.69f), new Vector3(0.43f, 0.9f, -0.69f) };
                frontKnee = 0.49f; rearKnee = 0.45f;
                tailPoints = new[] { new Vector3(0f, 0.98f, -1.1f), new Vector3(0.02f, 0.91f, -1.43f), new Vector3(0.09f, 0.77f, -1.74f), new Vector3(0.19f, 0.63f, -2.03f), new Vector3(0.27f, 0.55f, -2.28f) };
                break;
            case AnimalSpecies.Sheep:
                head = new Vector3(0f, 0.93f, 0.53f); body = new Vector3(0f, 0.78f, -0.03f);
                hips = new[] { new Vector3(-0.28f, 0.52f, 0.22f), new Vector3(0.28f, 0.52f, 0.22f), new Vector3(-0.28f, 0.52f, -0.38f), new Vector3(0.28f, 0.52f, -0.38f) };
                frontKnee = 0.286f; rearKnee = 0.286f;
                tailPoints = new[] { new Vector3(0f, 0.85f, -0.56f), new Vector3(0f, 0.72f, -0.66f), new Vector3(0f, 0.62f, -0.71f), new Vector3(0f, 0.58f, -0.74f), new Vector3(0f, 0.56f, -0.76f) };
                break;
            case AnimalSpecies.Goat:
                head = new Vector3(0f, 1.52f, 0.8f); body = new Vector3(0f, 0.83f, -0.04f);
                hips = new[] { new Vector3(-0.24f, 0.67f, 0.35f), new Vector3(0.24f, 0.67f, 0.35f), new Vector3(-0.24f, 0.67f, -0.4f), new Vector3(0.24f, 0.67f, -0.4f) };
                frontKnee = 0.368f; rearKnee = 0.368f;
                tailPoints = new[] { new Vector3(0f, 0.95f, -0.65f), new Vector3(0f, 0.82f, -0.74f), new Vector3(0f, 0.73f, -0.8f), new Vector3(0f, 0.69f, -0.84f), new Vector3(0f, 0.67f, -0.86f) };
                break;
            case AnimalSpecies.Giraffe:
                head = new Vector3(0f, 2.73f, 0.68f); body = new Vector3(0f, 1.12f, -0.05f);
                hips = new[] { new Vector3(-0.25f, 1.12f, 0.56f), new Vector3(0.25f, 1.12f, 0.56f), new Vector3(-0.25f, 1.12f, -0.58f), new Vector3(0.25f, 1.12f, -0.58f) };
                frontKnee = 0.616f; rearKnee = 0.616f;
                tailPoints = new[] { new Vector3(0f, 1.42f, -0.84f), new Vector3(0f, 1.28f, -0.94f), new Vector3(0f, 1.16f, -1.02f), new Vector3(0f, 1.08f, -1.08f), new Vector3(0f, 1.03f, -1.1f) };
                break;
            case AnimalSpecies.Capybara:
                head = new Vector3(0f, 0.62f, 0.48f); body = new Vector3(0f, 0.57f, -0.02f);
                hips = new[] { new Vector3(-0.29f, 0.37f, 0.39f), new Vector3(0.29f, 0.37f, 0.39f), new Vector3(-0.3f, 0.36f, -0.43f), new Vector3(0.3f, 0.36f, -0.43f) };
                frontKnee = 0.23f; rearKnee = 0.22f;
                tailPoints = new[] { new Vector3(0f, 0.42f, -0.56f), new Vector3(0f, 0.4f, -0.62f), new Vector3(0f, 0.39f, -0.66f), new Vector3(0f, 0.38f, -0.69f), new Vector3(0f, 0.37f, -0.7f) };
                break;
            case AnimalSpecies.Cow:
                head = new Vector3(0f, 1.0f, 0.83f); body = new Vector3(0f, 0.87f, -0.04f);
                hips = new[] { new Vector3(-0.34f, 0.61f, 0.49f), new Vector3(0.34f, 0.61f, 0.49f), new Vector3(-0.37f, 0.59f, -0.51f), new Vector3(0.37f, 0.59f, -0.51f) };
                frontKnee = 0.36f; rearKnee = 0.35f;
                tailPoints = new[] { new Vector3(0f, 0.97f, -0.78f), new Vector3(0f, 0.86f, -0.88f), new Vector3(0.02f, 0.73f, -0.96f), new Vector3(0.02f, 0.6f, -1.02f), new Vector3(0.02f, 0.53f, -1.03f) };
                break;
            case AnimalSpecies.Elephant:
                head = new Vector3(0f, 1.25f, 0.72f); body = new Vector3(0f, 1.04f, -0.02f);
                hips = new[] { new Vector3(-0.48f, 0.72f, 0.52f), new Vector3(0.48f, 0.72f, 0.52f), new Vector3(-0.5f, 0.72f, -0.55f), new Vector3(0.5f, 0.72f, -0.55f) };
                frontKnee = 0.4f; rearKnee = 0.4f;
                tailPoints = new[] { new Vector3(0f, 0.98f, -0.92f), new Vector3(0f, 0.86f, -1.0f), new Vector3(0f, 0.74f, -1.07f), new Vector3(0f, 0.64f, -1.12f), new Vector3(0f, 0.59f, -1.15f) };
                break;
        }
        rig.articulatedHead = CreateBone(rig.root, species + " Head Bone", head);
        rig.articulatedBody = CreateBone(rig.root, species + " Body Bone", body);
        if (species == AnimalSpecies.Tiger) { rig.tigerHead = rig.articulatedHead; rig.tigerBody = rig.articulatedBody; }
        bones[1] = rig.articulatedHead;
        bones[14] = rig.articulatedBody;
        for (int i = 0; i < 4; i++)
        {
            Transform upper = CreateBone(rig.root, species + " Leg Bone " + i, hips[i]);
            float knee = i < 2 ? frontKnee : rearKnee;
            Transform lower = CreateBone(upper, species + " Lower Leg Bone " + i, new Vector3(0f, knee - hips[i].y, i < 2 ? 0.02f : 0.08f));
            bones[2 + i * 2] = upper;
            bones[3 + i * 2] = lower;
            AnimalTigerLegRig leg = new AnimalTigerLegRig { upper = upper, lower = lower };
            if (species != AnimalSpecies.Dodo || i < 2) rig.articulatedLegs.Add(leg);
            if (species == AnimalSpecies.Tiger || (species == AnimalSpecies.Dodo && i < 2)) rig.tigerLegs.Add(leg);
        }

        Transform parent = rig.root;
        for (int i = 0; i < 4; i++)
        {
            Transform joint = CreateBone(parent, species + " Tail Bone " + i,
                i == 0 ? tailPoints[0] : tailPoints[i] - tailPoints[i - 1]);
            bones[10 + i] = joint;
            rig.articulatedTailSegments.Add(joint);
            if (species == AnimalSpecies.Tiger) rig.tigerTailSegments.Add(joint);
            parent = joint;
        }
        return bones;
    }

    private static Transform CreateBone(Transform parent, string name, Vector3 localPosition)
    {
        Transform bone = new GameObject(name).transform;
        bone.SetParent(parent, false);
        bone.localPosition = localPosition;
        bone.localRotation = Quaternion.identity;
        return bone;
    }

    private static Material[] GetAnimalMaterials(AnimalSpecies species)
    {
        Color baseColor, accentColor, darkColor;
        switch (species)
        {
            case AnimalSpecies.Rabbit:
                baseColor = new Color(0.54f, 0.44f, 0.34f); accentColor = new Color(0.84f, 0.77f, 0.67f); darkColor = new Color(0.11f, 0.085f, 0.07f); break;
            case AnimalSpecies.Dodo:
                baseColor = new Color(0.48f, 0.39f, 0.29f); accentColor = new Color(0.85f, 0.57f, 0.17f); darkColor = new Color(0.12f, 0.09f, 0.07f); break;
            case AnimalSpecies.PassengerPigeon:
                baseColor = new Color(0.35f, 0.42f, 0.48f); accentColor = new Color(0.68f, 0.34f, 0.3f); darkColor = new Color(0.15f, 0.17f, 0.2f); break;
            case AnimalSpecies.Dog:
                baseColor = new Color(0.56f, 0.34f, 0.2f); accentColor = new Color(0.82f, 0.7f, 0.53f); darkColor = new Color(0.12f, 0.075f, 0.045f); break;
            case AnimalSpecies.Pig:
                baseColor = new Color(0.73f, 0.43f, 0.42f); accentColor = new Color(0.9f, 0.59f, 0.57f); darkColor = new Color(0.2f, 0.11f, 0.12f); break;
            case AnimalSpecies.Lion:
                baseColor = new Color(0.69f, 0.43f, 0.18f); accentColor = new Color(0.9f, 0.73f, 0.48f); darkColor = new Color(0.12f, 0.075f, 0.04f); break;
            case AnimalSpecies.Tiger:
                baseColor = new Color(0.82f, 0.36f, 0.1f); accentColor = new Color(0.88f, 0.79f, 0.63f); darkColor = new Color(0.08f, 0.055f, 0.035f); break;
            case AnimalSpecies.Sheep:
                baseColor = new Color(0.83f, 0.79f, 0.67f); accentColor = new Color(0.28f, 0.24f, 0.21f); darkColor = new Color(0.14f, 0.12f, 0.1f); break;
            case AnimalSpecies.Goat:
                baseColor = new Color(0.63f, 0.52f, 0.37f); accentColor = new Color(0.83f, 0.75f, 0.58f); darkColor = new Color(0.15f, 0.11f, 0.08f); break;
            case AnimalSpecies.Giraffe:
                baseColor = new Color(0.83f, 0.65f, 0.31f); accentColor = new Color(0.95f, 0.79f, 0.43f); darkColor = new Color(0.42f, 0.26f, 0.13f); break;
            case AnimalSpecies.Capybara:
                baseColor = new Color(0.47f, 0.31f, 0.19f); accentColor = new Color(0.65f, 0.46f, 0.29f); darkColor = new Color(0.15f, 0.095f, 0.06f); break;
            case AnimalSpecies.Cow:
                baseColor = new Color(0.83f, 0.79f, 0.68f); accentColor = new Color(0.93f, 0.88f, 0.77f); darkColor = new Color(0.12f, 0.095f, 0.075f); break;
            case AnimalSpecies.Elephant:
                baseColor = new Color(0.43f, 0.47f, 0.46f); accentColor = new Color(0.57f, 0.6f, 0.57f); darkColor = new Color(0.2f, 0.22f, 0.21f); break;
            case AnimalSpecies.RareForestBird:
                baseColor = new Color(0.34f, 0.44f, 0.6f); accentColor = new Color(0.67f, 0.43f, 0.25f); darkColor = new Color(0.09f, 0.075f, 0.055f); break;
            default:
                baseColor = new Color(0.4f, 0.37f, 0.31f); accentColor = new Color(0.26f, 0.23f, 0.19f); darkColor = new Color(0.1f, 0.08f, 0.06f); break;
        }
        Color extraColor = species == AnimalSpecies.Lion ? new Color(0.39f, 0.2f, 0.075f) : darkColor;
        return new[] { GetColorMaterial(baseColor), GetColorMaterial(accentColor), GetColorMaterial(darkColor), GetColorMaterial(extraColor) };
    }

    private static Material GetColorMaterial(Color color)
    {
        Material material;
        if (ColorMaterials.TryGetValue(color, out material) && material != null)
            return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        material = new Material(shader) { name = "Wildlife Coat " + ColorUtility.ToHtmlStringRGB(color) };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.16f);
        ColorMaterials[color] = material;
        return material;
    }

    private static Mesh BuildUnifiedMesh(AnimalSpecies species, List<FormShape> forms)
    {
        Bounds bounds = ShapeBounds(forms, 0.16f);
        const float step = 0.065f;
        Vector3 min = bounds.min;
        int nx = Mathf.CeilToInt(bounds.size.x / step) + 1;
        int ny = Mathf.CeilToInt(bounds.size.y / step) + 1;
        int nz = Mathf.CeilToInt(bounds.size.z / step) + 1;
        int gridCount = nx * ny * nz;
        Vector3[] positions = new Vector3[gridCount];
        float[] field = new float[gridCount];
        for (int z = 0; z < nz; z++)
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    int index = (z * ny + y) * nx + x;
                    Vector3 position = min + new Vector3(x * step, y * step, z * step);
                    positions[index] = position;
                    field[index] = EvaluateField(position, forms);
                }

        List<Vector3> vertices = new List<Vector3>(12000);
        List<int>[] trianglesByMaterial = { new List<int>(30000), new List<int>(6000), new List<int>(6000), new List<int>(5000) };
        Dictionary<ulong, int> edgeVertices = new Dictionary<ulong, int>(18000);
        int[] cube = new int[8];
        int[] tetra = new int[4];
        int[] crossings = new int[4];
        float[] crossingAngles = new float[4];
        for (int z = 0; z < nz - 1; z++)
            for (int y = 0; y < ny - 1; y++)
                for (int x = 0; x < nx - 1; x++)
                {
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3Int offset = CubeCorners[corner];
                        cube[corner] = ((z + offset.z) * ny + y + offset.y) * nx + x + offset.x;
                    }
                    float low = float.MaxValue;
                    float high = float.MinValue;
                    for (int i = 0; i < 8; i++)
                    {
                        low = Mathf.Min(low, field[cube[i]]);
                        high = Mathf.Max(high, field[cube[i]]);
                    }
                    if (low > 0f || high < 0f)
                        continue;

                    for (int t = 0; t < 6; t++)
                    {
                        for (int c = 0; c < 4; c++)
                            tetra[c] = cube[Tetrahedra[t, c]];
                        PolygonizeTetra(tetra, positions, field, forms, species, vertices, trianglesByMaterial, edgeVertices, crossings, crossingAngles);
                    }
                }

        Mesh mesh = new Mesh { name = species + " Fused Low Poly Animal" };
        mesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.subMeshCount = 4;
        for (int material = 0; material < 4; material++)
            mesh.SetTriangles(trianglesByMaterial[material], material, false);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        ApplyAnimalBoneWeights(mesh, forms, vertices);
        return mesh;
    }

    private static Bounds ShapeBounds(List<FormShape> forms, float padding)
    {
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < forms.Count; i++)
        {
            FormShape shape = forms[i];
            Vector3 ext = shape.Capsule ? Vector3.one * shape.Radius : shape.Radii;
            min = Vector3.Min(min, Vector3.Min(shape.A, shape.B) - ext);
            max = Vector3.Max(max, Vector3.Max(shape.A, shape.B) + ext);
        }
        min -= Vector3.one * padding;
        max += Vector3.one * padding;
        return new Bounds((min + max) * 0.5f, max - min);
    }

    private static float EvaluateField(Vector3 point, List<FormShape> forms)
    {
        float value = ShapeDistance(point, forms[0]);
        const float blend = 0.1f;
        for (int i = 1; i < forms.Count; i++)
        {
            float next = ShapeDistance(point, forms[i]);
            float h = Mathf.Clamp01(0.5f + 0.5f * (next - value) / blend);
            value = Mathf.Lerp(next, value, h) - blend * h * (1f - h);
        }
        return value;
    }

    private static float ShapeDistance(Vector3 point, FormShape shape)
    {
        if (shape.Capsule)
        {
            Vector3 ab = shape.B - shape.A;
            float t = Mathf.Clamp01(Vector3.Dot(point - shape.A, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector3.Distance(point, shape.A + ab * t) - shape.Radius;
        }

        Vector3 q = point - shape.A;
        float k0 = new Vector3(q.x / shape.Radii.x, q.y / shape.Radii.y, q.z / shape.Radii.z).magnitude;
        Vector3 scaled = new Vector3(q.x / (shape.Radii.x * shape.Radii.x), q.y / (shape.Radii.y * shape.Radii.y), q.z / (shape.Radii.z * shape.Radii.z));
        float k1 = scaled.magnitude;
        return k1 < 0.0001f ? -Mathf.Min(shape.Radii.x, Mathf.Min(shape.Radii.y, shape.Radii.z)) : k0 * (k0 - 1f) / k1;
    }

    private static void PolygonizeTetra(int[] tetra, Vector3[] positions, float[] field, List<FormShape> forms,
        AnimalSpecies species, List<Vector3> vertices, List<int>[] triangles, Dictionary<ulong, int> edgeVertices,
        int[] crossings, float[] crossingAngles)
    {
        int count = 0;
        for (int edge = 0; edge < 6; edge++)
        {
            int a = tetra[TetraEdges[edge, 0]];
            int b = tetra[TetraEdges[edge, 1]];
            if ((field[a] < 0f) == (field[b] < 0f))
                continue;
            ulong key = ((ulong)(uint)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
            int vertexIndex;
            if (!edgeVertices.TryGetValue(key, out vertexIndex))
            {
                float t = field[a] / (field[a] - field[b]);
                vertexIndex = vertices.Count;
                vertices.Add(Vector3.LerpUnclamped(positions[a], positions[b], t));
                edgeVertices.Add(key, vertexIndex);
            }
            if (count < crossings.Length)
                crossings[count++] = vertexIndex;
        }
        if (count < 3)
            return;

        Vector3 center = Vector3.zero;
        for (int i = 0; i < count; i++) center += vertices[crossings[i]];
        center /= count;
        Vector3 normal = Vector3.zero;
        if (count >= 3)
            normal = Vector3.Cross(vertices[crossings[1]] - vertices[crossings[0]], vertices[crossings[2]] - vertices[crossings[0]]).normalized;
        if (normal.sqrMagnitude < 0.001f) return;
        Vector3 axisX = (vertices[crossings[0]] - center).normalized;
        Vector3 axisY = Vector3.Cross(normal, axisX).normalized;
        for (int i = 0; i < count; i++)
        {
            Vector3 d = vertices[crossings[i]] - center;
            crossingAngles[i] = Mathf.Atan2(Vector3.Dot(d, axisY), Vector3.Dot(d, axisX));
        }
        for (int i = 1; i < count; i++)
        {
            int vertex = crossings[i];
            float angle = crossingAngles[i];
            int j = i - 1;
            while (j >= 0 && crossingAngles[j] > angle)
            {
                crossings[j + 1] = crossings[j];
                crossingAngles[j + 1] = crossingAngles[j];
                j--;
            }
            crossings[j + 1] = vertex;
            crossingAngles[j + 1] = angle;
        }
        AddSurfaceTriangle(crossings[0], crossings[1], crossings[2], vertices, forms, species, triangles);
        if (count == 4)
            AddSurfaceTriangle(crossings[0], crossings[2], crossings[3], vertices, forms, species, triangles);
    }

    private static void AddSurfaceTriangle(int a, int b, int c, List<Vector3> vertices, List<FormShape> forms,
        AnimalSpecies species, List<int>[] triangles)
    {
        Vector3 va = vertices[a], vb = vertices[b], vc = vertices[c];
        Vector3 face = Vector3.Cross(vb - va, vc - va);
        if (face.sqrMagnitude < 0.0000001f) return;
        Vector3 center = (va + vb + vc) / 3f;
        Vector3 normal = face.normalized;
        if (EvaluateField(center + normal * 0.025f, forms) < EvaluateField(center - normal * 0.025f, forms))
        {
            int temp = b; b = c; c = temp;
        }
        int role = SurfaceMaterialRole(species, center, forms);
        triangles[role].Add(a);
        triangles[role].Add(b);
        triangles[role].Add(c);
    }

    private static int SurfaceMaterialRole(AnimalSpecies species, Vector3 point, List<FormShape> forms)
    {
        int best = 0;
        float distance = float.MaxValue;
        for (int i = 0; i < forms.Count; i++)
        {
            float d = ShapeDistance(point, forms[i]);
            if (d < distance) { distance = d; best = i; }
        }
        int role = forms[best].MaterialRole;
        if (species == AnimalSpecies.Tiger && Mathf.Abs(point.x) > 0.28f && point.y > 0.42f && point.y < 1.58f)
        {
            float normalizedY = point.y - 0.98f;
            float stripeCoordinate = point.z + 0.03f - 0.19f * Mathf.Sin(normalizedY * 2.2f);
            int stripe = Mathf.RoundToInt((stripeCoordinate + 0.82f) / 0.31f);
            float stripeCenter = -0.82f + stripe * 0.31f + 0.19f * Mathf.Sin(normalizedY * 2.2f);
            if (stripe >= 0 && stripe <= 5 && Mathf.Abs(point.z - stripeCenter) < 0.065f)
                return 2;
        }
        if (species == AnimalSpecies.Giraffe && role == 0 && point.y > 0.62f && point.y < 2.56f && Mathf.Abs(point.x) > 0.2f)
        {
            float cellX = Mathf.Floor(point.x / 0.34f);
            float cellY = Mathf.Floor(point.y / 0.36f);
            float cellZ = Mathf.Floor(point.z / 0.35f);
            float hash = Mathf.Sin(cellX * 127.1f + cellY * 311.7f + cellZ * 74.7f) * 43758.5453f;
            float jitter = hash - Mathf.Floor(hash);
            if (jitter > 0.24f)
            {
                float centerX = (cellX + 0.5f + (jitter - 0.5f) * 0.35f) * 0.34f;
                float centerY = (cellY + 0.5f + (jitter - 0.5f) * 0.22f) * 0.36f;
                float centerZ = (cellZ + 0.5f + (jitter - 0.5f) * 0.3f) * 0.35f;
                Vector3 d = new Vector3((point.x - centerX) / 0.15f, (point.y - centerY) / 0.16f, (point.z - centerZ) / 0.16f);
                if (d.sqrMagnitude < 1f)
                    return 2;
            }
        }
        return role;
    }

    private static void ApplyAnimalBoneWeights(Mesh mesh, List<FormShape> forms, List<Vector3> vertices)
    {
        BoneWeight[] weights = new BoneWeight[vertices.Count];
        float[] contribution = new float[15];
        for (int v = 0; v < vertices.Count; v++)
        {
            Array.Clear(contribution, 0, contribution.Length);
            Vector3 point = vertices[v];
            for (int s = 0; s < forms.Count; s++)
            {
                FormShape shape = forms[s];
                int bone = Mathf.Clamp(shape.Bone, 0, contribution.Length - 1);
                contribution[bone] += Mathf.Exp(-Mathf.Abs(ShapeDistance(point, shape)) / 0.18f);
            }
            int[] top = { 0, 0, 0, 0 };
            float[] values = { 0f, 0f, 0f, 0f };
            for (int bone = 0; bone < contribution.Length; bone++)
            {
                float score = contribution[bone];
                for (int slot = 0; slot < 4; slot++)
                {
                    if (score <= values[slot]) continue;
                    for (int move = 3; move > slot; move--) { values[move] = values[move - 1]; top[move] = top[move - 1]; }
                    values[slot] = score; top[slot] = bone; break;
                }
            }
            float total = values[0] + values[1] + values[2] + values[3];
            if (total < 0.0001f) { top[0] = 0; values[0] = 1f; total = 1f; }
            weights[v] = new BoneWeight
            {
                boneIndex0 = top[0], boneIndex1 = top[1], boneIndex2 = top[2], boneIndex3 = top[3],
                weight0 = values[0] / total, weight1 = values[1] / total, weight2 = values[2] / total, weight3 = values[3] / total
            };
        }
        mesh.boneWeights = weights;
    }






    private static void BuildSnake(AnimalVisualRig rig)
    {
        Color green = new Color(0.28f, 0.39f, 0.22f);
        const int rings = 28;
        const int sides = 10;
        Vector3[] vertices = new Vector3[rings * sides];
        int[] triangles = new int[(rings - 1) * sides * 6];
        for (int ring = 0; ring < rings; ring++)
        {
            float along = ring / (float)(rings - 1);
            float z = Mathf.Lerp(0.82f, -1.5f, along);
            float radius = Mathf.Lerp(0.145f, 0.045f, along);
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                vertices[ring * sides + side] = new Vector3(Mathf.Cos(angle) * radius, 0.17f + Mathf.Sin(angle) * radius * 0.78f, z);
            }
        }
        int tri = 0;
        for (int ring = 0; ring < rings - 1; ring++)
        {
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                int a = ring * sides + side;
                int b = ring * sides + next;
                int c = (ring + 1) * sides + side;
                int d = (ring + 1) * sides + next;
                triangles[tri++] = a; triangles[tri++] = c; triangles[tri++] = b;
                triangles[tri++] = b; triangles[tri++] = c; triangles[tri++] = d;
            }
        }
        Mesh body = new Mesh { name = "Continuous Slithering Snake Body" };
        body.MarkDynamic();
        body.vertices = vertices;
        body.triangles = triangles;
        body.RecalculateNormals();
        body.RecalculateBounds();
        GameObject bodyObject = new GameObject("Continuous Snake Body");
        bodyObject.transform.SetParent(rig.root, false);
        bodyObject.AddComponent<MeshFilter>().sharedMesh = body;
        MeshRenderer bodyRenderer = bodyObject.AddComponent<MeshRenderer>();
        bodyRenderer.sharedMaterial = sharedMaterial;
        MaterialPropertyBlock bodyBlock = new MaterialPropertyBlock();
        bodyBlock.SetColor(sharedMaterial.HasProperty("_BaseColor") ? BaseColorId : ColorId, green);
        bodyRenderer.SetPropertyBlock(bodyBlock);
        bodyRenderer.shadowCastingMode = ShadowCastingMode.On;
        rig.snakeBodyMesh = body;
        rig.snakeBodyVertices = vertices;
        rig.snakeAnimatedVertices = new Vector3[vertices.Length];
        AddPart(rig, PrimitiveType.Sphere, "Snake Head", new Vector3(0f, 0.2f, 0.83f), Vector3.zero, new Vector3(0.3f, 0.25f, 0.37f), green);
        AddEyes(rig, new Vector3(0f, 0.21f, 0.84f), 0.075f);
        AddPart(rig, PrimitiveType.Sphere, "Snake Tongue", new Vector3(0f, 0.16f, 1.08f), Vector3.zero, new Vector3(0.035f, 0.035f, 0.18f), new Color(0.66f, 0.17f, 0.18f));
    }










    private static void AddHead(AnimalVisualRig rig, Vector3 position, float size, Color color)
    {
        AddPart(rig, PrimitiveType.Sphere, "Animal Head", position, Vector3.zero, Vector3.one * size, color);
        AddEyes(rig, position, size * 0.28f);
    }

    private static void AddEyes(AnimalVisualRig rig, Vector3 center, float size)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            AddPart(rig, PrimitiveType.Sphere, "Animal Eye", center + new Vector3(side * size * 1.35f, size * 0.12f, size * 0.72f), Vector3.zero,
                new Vector3(size * 0.58f, size * 0.62f, size * 0.45f), new Color(0.055f, 0.045f, 0.035f));
        }
    }

    private static void AddTail(AnimalVisualRig rig, Vector3 position, float length, Color color)
    {
        rig.tail = AddPart(rig, PrimitiveType.Capsule, "Animal Tail", position, new Vector3(54f, 0f, 0f), new Vector3(0.09f, length, 0.09f), color);
    }

    private static void AddQuadrupedLegs(AnimalVisualRig rig, float halfWidth, float backZ, float frontZ, float length, Color color)
    {
        float hipY = length + 0.06f;
        foreach (int side in new[] { -1, 1 })
        {
            AddLeg(rig, new Vector3(side * halfWidth, hipY, -backZ), length, color);
            AddLeg(rig, new Vector3(side * halfWidth, hipY, frontZ), length, color);
        }
    }

    private static void AddLeg(AnimalVisualRig rig, Vector3 hip, float length, Color color)
    {
        Transform pivot = new GameObject("Animal Leg Pivot").transform;
        pivot.SetParent(rig.root, false);
        pivot.localPosition = hip;
        AddPartTo(pivot, PrimitiveType.Capsule, "Animal Leg", new Vector3(0f, -length * 0.48f, 0f), Vector3.zero,
            new Vector3(0.14f, length * 0.53f, 0.15f), color);
        rig.legs.Add(pivot);
    }

    private static void AddSegment(AnimalVisualRig rig, string name, Vector3 start, Vector3 end, float radius, Color color)
    {
        Vector3 direction = end - start;
        AddPart(rig, PrimitiveType.Capsule, name, (start + end) * 0.5f,
            Quaternion.FromToRotation(Vector3.up, direction.normalized).eulerAngles,
            new Vector3(radius, direction.magnitude * 0.5f + radius, radius), color);
    }

    private static Transform AddPart(AnimalVisualRig rig, PrimitiveType primitive, string name, Vector3 position, Vector3 rotation, Vector3 scale, Color color)
    {
        return AddPartTo(rig.root, primitive, name, position, rotation, scale, color);
    }

    private static Transform AddPart(AnimalVisualRig rig, PrimitiveType primitive, string name, Vector3 position, Quaternion rotation, Vector3 scale, Color color)
    {
        return AddPartTo(rig.root, primitive, name, position, rotation.eulerAngles, scale, color);
    }

    private static Transform AddPartTo(Transform parent, PrimitiveType primitive, string name, Vector3 position, Vector3 rotation, Vector3 scale, Color color)
    {
        Mesh mesh = GetPrimitiveMesh(primitive);
        GameObject part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(rotation);
        part.transform.localScale = scale;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = sharedMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor(sharedMaterial.HasProperty("_BaseColor") ? BaseColorId : ColorId, color);
        renderer.SetPropertyBlock(block);
        return part.transform;
    }

    private static Mesh GetPrimitiveMesh(PrimitiveType type)
    {
        if (PrimitiveMeshes.TryGetValue(type, out Mesh mesh) && mesh != null)
            return mesh;

        GameObject source = GameObject.CreatePrimitive(type);
        MeshFilter filter = source.GetComponent<MeshFilter>();
        mesh = filter != null ? filter.sharedMesh : null;
        Collider collider = source.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;
        Renderer renderer = source.GetComponent<Renderer>();
        if (renderer != null)
            renderer.enabled = false;
        UnityEngine.Object.Destroy(source);
        PrimitiveMeshes[type] = mesh;
        return mesh;
    }

    private static void EnsureMaterial()
    {
        if (sharedMaterial != null)
            return;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError("No supported shader was found for procedural animal visuals.");
            return;
        }

        sharedMaterial = new Material(shader) { name = "Matte Forest Animal Material" };
        if (sharedMaterial.HasProperty("_Metallic"))
            sharedMaterial.SetFloat("_Metallic", 0f);
        if (sharedMaterial.HasProperty("_Smoothness"))
            sharedMaterial.SetFloat("_Smoothness", 0.12f);
        if (sharedMaterial.HasProperty("_SpecularHighlights"))
        {
            sharedMaterial.SetFloat("_SpecularHighlights", 0f);
            sharedMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        }
        if (sharedMaterial.HasProperty("_EmissionColor"))
        {
            sharedMaterial.SetColor("_EmissionColor", Color.black);
            sharedMaterial.DisableKeyword("_EMISSION");
        }
    }
}
