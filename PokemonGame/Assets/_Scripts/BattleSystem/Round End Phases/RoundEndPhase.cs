using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public class RoundEndPhase
{
    [SerializeField] private RoundEndPhaseType _roundEndPhaseType;
    public RoundEndPhaseType Type => _roundEndPhaseType;
}

public enum RoundEndPhaseType
{
    WeatherDamage,
    WeatherDuration,
    PoisonDamage,
    Leftovers,
    CourtEffect,
    LeechSeed,
    GrassyTerrain,
    TerrainDuration,
    BurnFrostbiteDamage,
    StatusDuration,
    CourtDuration,
    StatusOrbs,
    BindingMoves,
    AquaRing,
    AfterNextRound,
}
