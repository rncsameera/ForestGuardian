using System;
using UnityEngine;

[Flags]
public enum AnimalHabitat
{
    None = 0,
    Forest = 1 << 0,
    NearWater = 1 << 1,
    House = 1 << 2,
    OpenGrassland = 1 << 3,
    Sky = 1 << 4
}

public enum AnimalSpecies
{
    Deer,
    Rabbit,
    Dog,
    Pig,
    Lion,
    Tiger,
    Snake,
    Bird,
    RareForestBird,
    Dodo,
    PassengerPigeon,
    Sheep,
    Goat,
    Giraffe,
    Cow,
    Elephant,
    Capybara
}

public enum AnimalState
{
    Idle,
    Wander,
    Flee,
    Chase,
    Alert,
    Rest,
    Eat,
    Sleep,
    ReturnToShelter,
    Takeoff,
    Flying,
    Landing,
    Perched
}

[Serializable]
public sealed class AnimalProfile
{
    public AnimalSpecies species;
    public AnimalHabitat preferredHabitat;
    public bool isPredator;
    public bool canBePrey = true;
    public bool canFly;
    public float movementSpeed = 1.5f;
    public float fleeSpeed = 4f;
    public float detectionRadius = 14f;
    public float playerNoticeRadius = 6f;
    public float wanderRadius = 22f;
    public float visualScale = 1f;
    public float colliderRadius = 0.35f;
    public float colliderHeight = 1.2f;
    [Range(0f, 1f)] public float spawnProbability = 1f;
}
