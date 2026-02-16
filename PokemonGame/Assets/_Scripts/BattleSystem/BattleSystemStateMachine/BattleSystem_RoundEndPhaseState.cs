using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class BattleSystem_RoundEndPhaseState : State<BattleSystem>
{
    private BattleSystem _battleSystem;
    public Dictionary<RoundEndPhaseType, IRoundEndPhaseHandler> RoundEndPhaseDictionary { get; private set; }
    [SerializeField] private List<RoundEndPhase> _roundEndPhases;
    public List<RoundEndPhase> RoundEndPhases => _roundEndPhases;

    public override void EnterState( BattleSystem owner )
    {
        _battleSystem = owner;
    }

    public void Init()
    {
        RoundEndPhaseDictionary = new()
        {
            { RoundEndPhaseType.WeatherDuration,        new RoundEndPhase_WeatherDuration() },
            { RoundEndPhaseType.WeatherDamage,          new RoundEndPhase_WeatherDamage() },
            { RoundEndPhaseType.AfterNextRound,         new RoundEndPhase_AfterNextRoundActivate() },
            { RoundEndPhaseType.GrassyTerrain,          new RoundEndPhase_GrassyTerrain() },
            { RoundEndPhaseType.Leftovers,              new RoundEndPhase_Leftovers() },
            { RoundEndPhaseType.AquaRing,               new RoundEndPhase_AquaRing() },
            { RoundEndPhaseType.LeechSeed,              new RoundEndPhase_LeechSeed() },
            { RoundEndPhaseType.StatusDuration,         new RoundEndPhase_StatusDuration() },
            { RoundEndPhaseType.PoisonDamage,           new RoundEndPhase_PoisonDamage() },
            { RoundEndPhaseType.BurnFrostbiteDamage,    new RoundEndPhase_BurnFrostbiteDamage() },
            { RoundEndPhaseType.BindingMoves,           new RoundEndPhase_BindingMoves() },
            { RoundEndPhaseType.CourtEffect,            new RoundEndPhase_CourtEffect() },
            { RoundEndPhaseType.CourtDuration,          new RoundEndPhase_CourtDuration() },
            { RoundEndPhaseType.TerrainDuration,        new RoundEndPhase_TerrainDuration() },
            { RoundEndPhaseType.StatusOrbs,             new RoundEndPhase_StatusOrbs() },
        };
    }

    public void Clear()
    {
        RoundEndPhaseDictionary = null;
    }
    
}
