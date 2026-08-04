using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleAI_CoordinationIntent
{
    private readonly BattleAI _ai;

    public BattleAI_CoordinationIntent( BattleAI ai )
    {
        _ai = ai;
    }

    public void GetCoordinationIntentResult( PairIntentResult pir )
    {
        
    }

    private HashSet<StrategyPressure> ExtractPressures( PairStrategy strat, List<PairObservationEvidence> poe )
    {
        HashSet<StrategyPressure> pressures = new();

        return pressures;
    }

    private HashSet<StrategyPressure> ExtractPressures_EstablishTrickRoom( List<PairObservationEvidence> poe )
    {
        HashSet<StrategyPressure> pressures = new();

        return pressures;
    }

    private HashSet<StrategyPressure> ExtractPressures_EstablishTailwind( List<PairObservationEvidence> poe )
    {
        HashSet<StrategyPressure> pressures = new();

        return pressures;
    }

    private HashSet<StrategyPressure> ExtractPressures_WeatherPivot( List<PairObservationEvidence> poe )
    {
        HashSet<StrategyPressure> pressures = new();

        return pressures;
    }

    private HashSet<StrategyPressure> ExtractPressures_SecureImmediateKO( List<PairObservationEvidence> poe )
    {
        HashSet<StrategyPressure> pressures = new();

        return pressures;
    }

    private HashSet<StrategyPressure> ExtractPressures_ApplyBoardPressure( List<PairObservationEvidence> poe )
    {
        HashSet<StrategyPressure> pressures = new();

        return pressures;
    }

    private HashSet<StrategyPressure> ExtractPressures_PreserveTempo( List<PairObservationEvidence> poe )
    {
        HashSet<StrategyPressure> pressures = new();

        return pressures;
    }

    private HashSet<StrategyPressure> ExtractPressures_DenyOpponentSetup( List<PairObservationEvidence> poe )
    {
        HashSet<StrategyPressure> pressures = new();

        return pressures;
    }

    private HashSet<StrategyPressure> ExtractPressures_EstablishDefensivePosition( List<PairObservationEvidence> poe )
    {
        HashSet<StrategyPressure> pressures = new();

        return pressures;
    }
}

public struct CoordinationIntentResult
{
    
}

public enum StrategyPressure
{
    
}

public enum PressureResponse
{
    
}

public enum CoordinationOpportunity
{
    
}

public enum CoordinationJob
{
    
}
