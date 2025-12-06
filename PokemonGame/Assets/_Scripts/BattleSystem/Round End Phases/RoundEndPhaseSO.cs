using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu( menuName = "Battle/RoundEndPhase" )]
public class RoundEndPhaseSO : ScriptableObject
{
    [SerializeField] private RoundEndPhaseType _roundEndPhaseType;
    public RoundEndPhaseType Type => _roundEndPhaseType;
}

public enum RoundEndPhaseType
{
    WeatherDamage,
    WeatherHealing,
    StatusDamage
}
