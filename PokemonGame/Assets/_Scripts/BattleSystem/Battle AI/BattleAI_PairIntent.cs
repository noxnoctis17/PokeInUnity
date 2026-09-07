using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BattleAI_PairIntent
{
    private readonly BattleAI _ai;

    public BattleAI_PairIntent( BattleAI ai )
    {
        _ai = ai;
    }

    //--GamePlanAlignment scoring blocks may want to have ally synergy! don't forget to visit those! --07/02/26 --for clarification, i mean the actual scoring blocks inside of GamePlanAlignment deserve dedicated
    //--ally and opponent ally blocks. --07/13/26
    
    public PairIntentResult GetPairIntentResult( ThreatInteractionMatrix tim )
    {
        PairIntentResult pir = new();

        var patterns = GetPatterns( tim );
        var evidence = GetAllPairObservationEvidence( patterns );
        var tallies = AccumulateObservationEvidence( evidence );
        var availableStrategies = GetAvailableStrategies( patterns, evidence );
        Dictionary<PairStrategy, PairStrategyIntent> strategyScores = new();

        pir.Poe = evidence;

        CustomLogSession stratLog = new();
        stratLog.Add( $"================================" );
        stratLog.Add( $"=====[Available Strategies]=====" );
        stratLog.Add( $"================================" );
        stratLog.Add( $"" );
        foreach( var strat in availableStrategies )
        {
            stratLog.Add( $"Found {strat}" );
        }

        stratLog.Add( $"" );

        foreach( var strat in availableStrategies )
        {
            ( Intent leftIntent, Intent rightIntent ) intents;
            intents.leftIntent = default;
            intents.rightIntent = default;

            PairObservationEvidence poe = default;

            int patternScore = 0;
            int observationScore = 0;
            int opportunityScore = 0;
            int commitmentScore = 0;
            int finalScore = 0;

            stratLog.Add( $"Scoring {strat}" );
            switch( strat )
            {
                case PairStrategy.EstablishTrickRoom:
                    
                    //--Extract Intents
                    tallies.TryGetValue( PairObservation.TrickRoom, out var trickRoomEvidence );
                    poe = trickRoomEvidence.Evidence;
                    intents = GetStrategyIntent( poe, default, patterns );

                    //--Score Strategy
                    foreach( var pattern in patterns )
                    {
                        if( pattern.Key == PairPattern.TrickRoomSetting || pattern.Key == PairPattern.AttackAndSetup || pattern.Key == PairPattern.CoveredSetup )
                        {
                            int leftValue = pattern.Value.UnitLeftMatch.IsPrimary ? 2 : 1;
                            int rightValue = pattern.Value.UnitRightMatch.IsPrimary ? 2 : 1;

                            patternScore += leftValue + rightValue;
                            stratLog.Add( $"Found Relevant Pattern: {pattern.Key}. Value: {leftValue + rightValue}" );
                        }
                    }

                    observationScore += tallies.ContainsKey( PairObservation.TrickRoom ) ? tallies[PairObservation.TrickRoom].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.FakeOutSupport ) ? tallies[PairObservation.FakeOutSupport].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.RedirectionSupport ) ? tallies[PairObservation.RedirectionSupport].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.GuardSupport ) ? tallies[PairObservation.GuardSupport].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.Cover ) ? tallies[PairObservation.Cover].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.Setup ) ? tallies[PairObservation.Setup].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.SpeedControl ) ? tallies[PairObservation.SpeedControl].Tally : 0;

                    opportunityScore += ScoreDenial( strat, evidence ) +
                                        ScoreInitiativeSwing( strat, evidence ) +
                                        ScoreGuaranteedValue( strat, evidence ) +
                                        ScoreBoardControl( strat, evidence );

                    commitmentScore += ScoreEstablishTrickRoomCommitment( evidence );

                    finalScore += patternScore + observationScore + opportunityScore + commitmentScore;

                    stratLog.Add( $"Pattern Score: {patternScore}" );
                    stratLog.Add( $"Observation Score: {observationScore}" );
                    stratLog.Add( $"Opportunity Score: {opportunityScore}" );
                    stratLog.Add( $"Commitment Score: {commitmentScore}" );
                    stratLog.Add( $"" );
                    stratLog.Add( $"Final Score: {finalScore}" );

                break;

                case PairStrategy.EstablishTailwind:

                    //--Extract Intents
                    tallies.TryGetValue( PairObservation.Tailwind, out var tailwindEvidence );
                    poe = tailwindEvidence.Evidence;
                    intents = GetStrategyIntent( poe, default, patterns );

                    //--Score Strategy
                    foreach( var pattern in patterns )
                    {
                        if( pattern.Key == PairPattern.TailwindSetting || pattern.Key == PairPattern.AttackAndSetup || pattern.Key == PairPattern.CoveredSetup )
                        {
                            int leftValue = pattern.Value.UnitLeftMatch.IsPrimary ? 2 : 1;
                            int rightValue = pattern.Value.UnitRightMatch.IsPrimary ? 2 : 1;

                            patternScore += leftValue + rightValue;
                            stratLog.Add( $"Found Relevant Pattern: {pattern.Key}. Value: {leftValue + rightValue}" );
                        }
                    }

                    observationScore += tallies.ContainsKey( PairObservation.Tailwind ) ? tallies[PairObservation.Tailwind].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.FakeOutSupport ) ? tallies[PairObservation.FakeOutSupport].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.RedirectionSupport ) ? tallies[PairObservation.RedirectionSupport].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.GuardSupport ) ? tallies[PairObservation.GuardSupport].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.Cover ) ? tallies[PairObservation.Cover].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.Setup ) ? tallies[PairObservation.Setup].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.SpeedControl ) ? tallies[PairObservation.SpeedControl].Tally : 0;

                    opportunityScore += ScoreMomentum( strat, evidence ) +
                                        ScoreBoardControl( strat, evidence ) +
                                        ScorePressure( strat, evidence ) +
                                        ScoreGuaranteedValue( strat, evidence );

                    commitmentScore += ScoreEstablishTailwindCommitment( evidence );

                    finalScore += patternScore + observationScore + opportunityScore + commitmentScore;

                    stratLog.Add( $"Pattern Score: {patternScore}" );
                    stratLog.Add( $"Observation Score: {observationScore}" );
                    stratLog.Add( $"Opportunity Score: {opportunityScore}" );
                    stratLog.Add( $"Commitment Score: {commitmentScore}" );
                    stratLog.Add( $"" );
                    stratLog.Add( $"Final Score: {finalScore}" );

                break;

                case PairStrategy.WeatherPivot:

                    //--Extract Intents
                    intents = GetStrategyIntent( poe, PairPattern.WeatherChange, patterns, true );

                    //--Score Strategy
                    foreach( var pattern in patterns )
                    {
                        if( pattern.Key == PairPattern.WeatherChange || pattern.Key == PairPattern.CoveredSwitch || pattern.Key == PairPattern.ProtectAndSwitch || pattern.Key == PairPattern.AttackAndSwitch )
                        {
                            int leftValue = pattern.Value.UnitLeftMatch.IsPrimary ? 2 : 1;
                            int rightValue = pattern.Value.UnitRightMatch.IsPrimary ? 2 : 1;

                            patternScore += leftValue + rightValue;
                            stratLog.Add( $"Found Relevant Pattern: {pattern.Key}. Value: {leftValue + rightValue}" );
                        }
                    }

                    observationScore += tallies.ContainsKey( PairObservation.PivotPressure ) ? tallies[PairObservation.PivotPressure].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.Preservation ) ? tallies[PairObservation.Preservation].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.Switch ) ? tallies[PairObservation.Switch].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.WeatherChange ) ? tallies[PairObservation.WeatherChange].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.PivotMove ) ? tallies[PairObservation.PivotMove].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.Cover ) ? tallies[PairObservation.Cover].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.Protect ) ? tallies[PairObservation.Protect].Tally : 0;

                    opportunityScore += ScoreMomentum( strat, evidence ) +
                                        ScoreBoardControl( strat, evidence ) +
                                        ScoreConversion( strat, evidence );

                    commitmentScore += ScoreWeatherPivotCommitment( evidence );

                    finalScore += patternScore + observationScore + opportunityScore + commitmentScore;

                    stratLog.Add( $"Pattern Score: {patternScore}" );
                    stratLog.Add( $"Observation Score: {observationScore}" );
                    stratLog.Add( $"Opportunity Score: {opportunityScore}" );
                    stratLog.Add( $"Commitment Score: {commitmentScore}" );
                    stratLog.Add( $"" );
                    stratLog.Add( $"Final Score: {finalScore}" );

                break;

                case PairStrategy.SecureImmediateKO:

                    //--Extract Intents
                    intents = GetStrategyIntent( poe, PairPattern.FocusFire, patterns, true );

                    //--Score Strategy
                    foreach( var pattern in patterns )
                    {
                        if( pattern.Key == PairPattern.FocusFire || pattern.Key == PairPattern.DoubleAttack )
                        {
                            int leftValue = pattern.Value.UnitLeftMatch.IsPrimary ? 2 : 1;
                            int rightValue = pattern.Value.UnitRightMatch.IsPrimary ? 2 : 1;

                            patternScore += leftValue + rightValue;
                            stratLog.Add( $"Found Relevant Pattern: {pattern.Key}. Value: {leftValue + rightValue}" );
                        }
                    }

                    observationScore += tallies.ContainsKey( PairObservation.Attack ) ? tallies[PairObservation.Attack].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.DoubleAttack ) ? tallies[PairObservation.DoubleAttack].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.RevengeKO ) ? tallies[PairObservation.RevengeKO].Tally : 0;

                    opportunityScore += ScoreImmediateRemoval( strat, evidence ) +
                                        ScorePressure( strat, evidence ) +
                                        ScoreConversion( strat, evidence );

                    commitmentScore += ScoreImmediateKOCommitment( evidence );

                    finalScore += patternScore + observationScore + opportunityScore + commitmentScore;

                    stratLog.Add( $"Pattern Score: {patternScore}" );
                    stratLog.Add( $"Observation Score: {observationScore}" );
                    stratLog.Add( $"Opportunity Score: {opportunityScore}" );
                    stratLog.Add( $"Commitment Score: {commitmentScore}" );
                    stratLog.Add( $"" );
                    stratLog.Add( $"Final Score: {finalScore}" );

                break;

                case PairStrategy.ApplyBoardPressure:

                    //--Extract Intents
                    PairPattern abpPattern;

                    if( patterns.ContainsKey( PairPattern.DoubleAttack ) )
                        abpPattern = PairPattern.DoubleAttack;
                    else if( patterns.ContainsKey( PairPattern.FocusFire ) )
                        abpPattern = PairPattern.FocusFire;
                    else
                        abpPattern = default;

                    if( abpPattern == PairPattern.DoubleAttack || abpPattern == PairPattern.FocusFire ) 
                        intents = GetStrategyIntent( poe, abpPattern, patterns, true );
                    else if( tallies.TryGetValue( PairObservation.Attack, out var apbAttack ) )
                        intents = GetStrategyIntent( apbAttack.Evidence, default, patterns );

                    //--Score Strategy
                    foreach( var pattern in patterns )
                    {
                        if( pattern.Key == PairPattern.FocusFire || pattern.Key == PairPattern.DoubleAttack || pattern.Key == PairPattern.CoveredAttack )
                        {
                            int leftValue = pattern.Value.UnitLeftMatch.IsPrimary ? 2 : 1;
                            int rightValue = pattern.Value.UnitRightMatch.IsPrimary ? 2 : 1;

                            patternScore += leftValue + rightValue;
                            stratLog.Add( $"Found Relevant Pattern: {pattern.Key}. Value: {leftValue + rightValue}" );
                        }
                    }

                    observationScore += tallies.ContainsKey( PairObservation.Attack ) ? tallies[PairObservation.Attack].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.DoubleAttack ) ? tallies[PairObservation.DoubleAttack].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.RevengeKO ) ? tallies[PairObservation.RevengeKO].Tally : 0;

                    opportunityScore += ScoreMomentum( strat, evidence ) +
                                        ScorePressure( strat, evidence ) +
                                        ScoreBoardControl( strat, evidence ) +
                                        ScorePunish( strat, evidence );

                    commitmentScore += ScoreApplyBoardPressureCommitment( evidence );

                    finalScore += patternScore + observationScore + opportunityScore + commitmentScore;

                    stratLog.Add( $"Pattern Score: {patternScore}" );
                    stratLog.Add( $"Observation Score: {observationScore}" );
                    stratLog.Add( $"Opportunity Score: {opportunityScore}" );
                    stratLog.Add( $"Commitment Score: {commitmentScore}" );
                    stratLog.Add( $"" );
                    stratLog.Add( $"Final Score: {finalScore}" );

                break;

                case PairStrategy.PreserveTempo:

                    //--Extract Intents
                    tallies.TryGetValue( PairObservation.Tailwind, out var preserveTempo );
                    poe = preserveTempo.Evidence;
                    intents = GetStrategyIntent( poe, default, patterns );

                    //--Score Strategy
                    foreach( var pattern in patterns )
                    {
                        if( pattern.Key == PairPattern.CoveredSwitch || pattern.Key == PairPattern.ProtectAndSwitch )
                        {
                            int leftValue = pattern.Value.UnitLeftMatch.IsPrimary ? 2 : 1;
                            int rightValue = pattern.Value.UnitRightMatch.IsPrimary ? 2 : 1;

                            patternScore += leftValue + rightValue;
                            stratLog.Add( $"Found Relevant Pattern: {pattern.Key}. Value: {leftValue + rightValue}" );
                        }
                    }

                    observationScore += tallies.ContainsKey( PairObservation.Switch ) ? tallies[PairObservation.Switch].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.PivotMove ) ? tallies[PairObservation.PivotMove].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.Preservation ) ? tallies[PairObservation.Preservation].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.RevengeKO ) ? tallies[PairObservation.RevengeKO].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.PivotPressure ) ? tallies[PairObservation.PivotPressure].Tally : 0;

                    opportunityScore += ScoreMomentum( strat, evidence ) +
                                        ScoreInitiativeSwing( strat, evidence ) +
                                        ScoreGuaranteedValue( strat, evidence );

                    commitmentScore += ScorePreserveTempoCommitment( evidence );

                    finalScore += patternScore + observationScore + opportunityScore + commitmentScore;

                    stratLog.Add( $"Pattern Score: {patternScore}" );
                    stratLog.Add( $"Observation Score: {observationScore}" );
                    stratLog.Add( $"Opportunity Score: {opportunityScore}" );
                    stratLog.Add( $"Commitment Score: {commitmentScore}" );
                    stratLog.Add( $"" );
                    stratLog.Add( $"Final Score: {finalScore}" );

                break;

                case PairStrategy.DenyOpponentSetup:

                    //--Extract Intents
                    if( patterns.TryGetValue( PairPattern.DisruptionPressure, out var disruptionPressure ) )
                    {
                        intents = GetStrategyIntent( poe, PairPattern.DisruptionPressure, patterns, true );
                    }
                    else if( tallies.TryGetValue( PairObservation.Interruption, out var interruption ) )
                    {
                        poe = interruption.Evidence;
                        intents = GetStrategyIntent( poe, default, patterns );
                    }

                    //--Score Strategy
                    foreach( var pattern in patterns )
                    {
                        if( pattern.Key == PairPattern.DisruptionPressure || pattern.Key == PairPattern.CoveredSetup || pattern.Key == PairPattern.FocusFire )
                        {
                            int leftValue = pattern.Value.UnitLeftMatch.IsPrimary ? 2 : 1;
                            int rightValue = pattern.Value.UnitRightMatch.IsPrimary ? 2 : 1;

                            patternScore += leftValue + rightValue;
                            stratLog.Add( $"Found Relevant Pattern: {pattern.Key}. Value: {leftValue + rightValue}" );
                        }
                    }

                    observationScore += tallies.ContainsKey( PairObservation.FakeOutSupport ) ? tallies[PairObservation.FakeOutSupport].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.Interruption ) ? tallies[PairObservation.Interruption].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.Cover ) ? tallies[PairObservation.Cover].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.SpeedControl ) ? tallies[PairObservation.SpeedControl].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.StatusDisruption ) ? tallies[PairObservation.StatusDisruption].Tally : 0;

                    opportunityScore += ScoreInitiativeSwing( strat, evidence ) +
                                        ScorePressure( strat, evidence ) +
                                        ScoreDenial( strat, evidence );

                    commitmentScore += ScoreDenyOpponentSetupCommitment( evidence );

                    finalScore += patternScore + observationScore + opportunityScore + commitmentScore;

                    stratLog.Add( $"Pattern Score: {patternScore}" );
                    stratLog.Add( $"Observation Score: {observationScore}" );
                    stratLog.Add( $"Opportunity Score: {opportunityScore}" );
                    stratLog.Add( $"Commitment Score: {commitmentScore}" );
                    stratLog.Add( $"" );
                    stratLog.Add( $"Final Score: {finalScore}" );

                break;

                case PairStrategy.EstablishDefensivePosition:

                    int bestDefensivePositionPattern = int.MinValue;
                    foreach( var pattern in patterns )
                    {
                        if( pattern.Key == PairPattern.CoveredSetup || pattern.Key == PairPattern.CoveredSwitch || pattern.Key == PairPattern.PivotPlay || pattern.Key == PairPattern.ProtectAndSwitch )
                        {
                            bool leftPrimary = pattern.Value.UnitLeftMatch.IsPrimary;
                            bool rightPrimary = pattern.Value.UnitRightMatch.IsPrimary;

                            int leftValue = leftPrimary ? 2 : 1;
                            int rightValue = rightPrimary ? 2 : 1;

                            patternScore += leftValue + rightValue;
                            stratLog.Add( $"Found Relevant Pattern: {pattern.Key}. Value: {leftValue + rightValue}" );

                            if( leftValue + rightValue > bestDefensivePositionPattern )
                            {
                                bestDefensivePositionPattern = leftValue + rightValue;
                                intents = ( pattern.Value.UnitLeftMatch.MatchingIntent, pattern.Value.UnitRightMatch.MatchingIntent );
                            }
                        }
                    }

                    observationScore += tallies.ContainsKey( PairObservation.Protect ) ? tallies[PairObservation.Protect].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.Cover ) ? tallies[PairObservation.Cover].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.ScreensSupport ) ? tallies[PairObservation.ScreensSupport].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.GuardSupport ) ? tallies[PairObservation.GuardSupport].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.RevengeKO ) ? tallies[PairObservation.RevengeKO].Tally : 0;

                    opportunityScore += ScoreResource( strat, evidence ) +
                                        ScoreMomentum( strat, evidence ) +
                                        ScoreGuaranteedValue( strat, evidence );

                    commitmentScore += ScoreEstablishDefensivePositionCommitment( evidence );

                    finalScore += patternScore + observationScore + opportunityScore + commitmentScore;

                    stratLog.Add( $"Pattern Score: {patternScore}" );
                    stratLog.Add( $"Observation Score: {observationScore}" );
                    stratLog.Add( $"Opportunity Score: {opportunityScore}" );
                    stratLog.Add( $"Commitment Score: {commitmentScore}" );
                    stratLog.Add( $"" );
                    stratLog.Add( $"Final Score: {finalScore}" );

                break;

                case PairStrategy.AfterYouAttack:
                    
                    //--Extract Intents
                    tallies.TryGetValue( PairObservation.AfterYou, out var afterYouEvidence );
                    poe = afterYouEvidence.Evidence;
                    intents = GetStrategyIntent( poe, PairPattern.AfterYouAndAttack, patterns, true );

                    //--Score Strategy
                    foreach( var pattern in patterns )
                    {
                        if( pattern.Key == PairPattern.AfterYouAndAttack )
                        {
                            int leftValue = pattern.Value.UnitLeftMatch.IsPrimary ? 2 : 1;
                            int rightValue = pattern.Value.UnitRightMatch.IsPrimary ? 2 : 1;

                            patternScore += leftValue + rightValue;
                            stratLog.Add( $"Found Relevant Pattern: {pattern.Key}. Value: {leftValue + rightValue}" );
                        }
                    }

                    observationScore += tallies.ContainsKey( PairObservation.AfterYou ) ? tallies[PairObservation.AfterYou].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.Attack ) ? tallies[PairObservation.Attack].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.SpreadAttack ) ? tallies[PairObservation.SpreadAttack].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.StatusMove ) ? tallies[PairObservation.StatusMove].Tally : 0;
                    observationScore += tallies.ContainsKey( PairObservation.SpeedControl ) ? tallies[PairObservation.SpeedControl].Tally : 0;

                    opportunityScore += ScoreInitiativeSwing( strat, evidence ) +
                                        ScorePressure( strat, evidence ) +
                                        ScoreMomentum( strat, evidence ) +
                                        ScoreBoardControl( strat, evidence ) +
                                        ScoreGuaranteedValue( strat, evidence ) +
                                        ScoreImmediateRemoval( strat, evidence );

                    commitmentScore += ScoreAfterYouAttackCommitment( evidence );

                    finalScore += patternScore + observationScore + opportunityScore + commitmentScore;

                    stratLog.Add( $"Pattern Score: {patternScore}" );
                    stratLog.Add( $"Observation Score: {observationScore}" );
                    stratLog.Add( $"Opportunity Score: {opportunityScore}" );
                    stratLog.Add( $"Commitment Score: {commitmentScore}" );
                    stratLog.Add( $"" );
                    stratLog.Add( $"Final Score: {finalScore}" );

                break;
            }

            if( intents.leftIntent.ActionType == ActionType.None )
                Debug.LogError( $"Left Intent for {strat} is ActionType.None!" );

            if( intents.rightIntent.ActionType == ActionType.None )
                Debug.LogError( $"Right Intent for {strat} is ActionType.None!" );

            PairStrategyIntent psi = new()
            {
                LeftIntent = intents.leftIntent,
                RightIntent = intents.rightIntent,

                Strategy = strat,

                PatternScore = patternScore,
                ObservationScore = observationScore,
                OpportunityScore = opportunityScore,
                CommitmentScore = commitmentScore,
                FinalScore = finalScore,
            };

            strategyScores.Add( strat, psi );
            stratLog.Add( $"" );
            stratLog.Add( $"" );
        }

        //--Entropy stuff
        // float totalAllStrategyScores = 0;
        // float activeStrategies = strategyScores.Count( kvp => kvp.Value.FinalScore > 0 );

        // float competitionEntropy = 0f;
        // float competitionConfidence = 0f;

        // foreach( var kvp in strategyScores )
        // {            
        //     totalAllStrategyScores += kvp.Value.FinalScore;
        // }

        // foreach( var kvp in strategyScores )
        // {
        //     if( kvp.Value.FinalScore <= 0 )
        //         continue;

        //     float v = kvp.Value.FinalScore;
        //     float p = v / totalAllStrategyScores;
        //     competitionEntropy -= p * Mathf.Log( p, 2f );
        // }

        // if( activeStrategies <= 1 )
        // {
        //     competitionConfidence = 1f;
        // }
        // else
        // {
        //     competitionEntropy /= Mathf.Log( activeStrategies, 2f );
        //     competitionConfidence = Mathf.Clamp01( 1f - competitionEntropy );
        // }

        // foreach( var kvp in strategyScores )
        // {
        //     var strat = kvp.Value;
        //     float activeSignals = 0;
        //     float totalPositiveSignals = 0;
        //     float ic = 0f;

        //     if( strat.PatternScore > 0 )
        //     {
        //         activeSignals++;
        //         totalPositiveSignals += strat.PatternScore;
        //     }

        //     if( strat.ObservationScore > 0 )
        //     {
        //         activeSignals++;
        //         totalPositiveSignals += strat.ObservationScore;
        //     }

        //     if( strat.OpportunityScore > 0 )
        //     {
        //         activeSignals++;
        //         totalPositiveSignals += strat.OpportunityScore;
        //     }

        //     if( strat.CommitmentScore > 0 )
        //     {
        //         activeSignals++;
        //         totalPositiveSignals += strat.CommitmentScore;
        //     }

        //     if( strat.PatternScore > 0 )
        //     {
        //         float p = strat.PatternScore / totalPositiveSignals;
        //         ic -= p * Mathf.Log( p, 2f );
        //     }

        //     if( strat.ObservationScore > 0 )
        //     {
        //         float p = strat.ObservationScore / totalPositiveSignals;
        //         ic -= p * Mathf.Log( p, 2f );
        //     }

        //     if( strat.OpportunityScore > 0 )
        //     {
        //         float p = strat.OpportunityScore / totalPositiveSignals;
        //         ic -= p * Mathf.Log( p, 2f );
        //     }

        //     if( strat.CommitmentScore > 0 )
        //     {
        //         float p = strat.CommitmentScore / totalPositiveSignals;
        //         ic -= p * Mathf.Log( p, 2f );
        //     }

        //     if( activeSignals <= 1 )
        //     {
        //         strat.InternalConfidence = 1f;
        //     }
        //     else
        //     {
        //         ic /= Mathf.Log( activeSignals, 2f );
        //         strat.InternalConfidence = Mathf.Clamp01( 1f - ic );
        //     }
        // }

        pir.Strategies = strategyScores.OrderByDescending( kvp => kvp.Value.FinalScore ).ToDictionary( kvp => kvp.Key, kvp => kvp.Value );

        pir.PrimaryStrategy = pir.Strategies.Values.First();

        if( pir.Strategies.Count > 1 )
            pir.SecondaryStrategy = pir.Strategies.Values.Skip(1).First();

        stratLog.Add( $"Primary Strategy:.....{pir.PrimaryStrategy.Strategy}, {pir.PrimaryStrategy.FinalScore}" );

        if( pir.Strategies.Count > 1 )
            stratLog.Add( $"Secondary Strategy:.....{pir.SecondaryStrategy.Strategy}, {pir.SecondaryStrategy.FinalScore}" );

        stratLog.Add( $"" );
        stratLog.Add( $"Final Strategy Scores:" );
        foreach( var kvp in pir.Strategies )
        {
            stratLog.Add( $"{kvp.Key}:.....{kvp.Value.FinalScore} ({kvp.Value.InternalConfidence})" );
        }

        // pir.CompetitionConfidence = competitionConfidence;
        // pir.PrimaryConfidence = pir.PrimaryStrategy.InternalConfidence * competitionConfidence;

        // if( pir.Strategies.Count > 1 )
            // pir.SecondaryConfidence = pir.SecondaryStrategy.InternalConfidence * competitionConfidence;
        // else
            // pir.SecondaryConfidence = 0f;

        stratLog.Add( $"Confidence no longer implemented 09/06/26" );
        // stratLog.Add( $"Competition Confidence: {competitionConfidence}" );
        // stratLog.Add( $"Final Primary Confidence: {pir.PrimaryConfidence}" );
        // stratLog.Add( $"Final Secondary Confidence: {pir.SecondaryConfidence}" );

        string path = Application.persistentDataPath + "/Available Strategies_Log.txt";
        System.IO.File.AppendAllText( path, stratLog.ToString() + "\n" + "\n" + "\n" + "\n" + "\n" );
        stratLog.Clear();

        return pir;
    }

    private ( Intent Left, Intent Right ) GetStrategyIntent( PairObservationEvidence poe, PairPattern pattern, Dictionary<PairPattern, PatternIntentPack> patterns, bool checkPattern = false )
    {
        //--Extract Pattern from observation
        var sourcePattern = checkPattern ? pattern : poe.SourcePattern;
        patterns.TryGetValue( sourcePattern, out var pip );

        //--Extract Intents to correct units
        Intent left = pip.UnitLeftMatch.MatchingIntent;
        Intent right = pip.UnitRightMatch.MatchingIntent;

        return ( left, right );
    }

    public ThreatInteractionMatrix BuildThreatInteractionMatrix()
    {
        ThreatInteractionMatrix tim = new()
        {
            EnemyLeft = new(),
            EnemyRight = new(),
        };

        var myUnits = _ai.Blackboard.MyActiveUnits.Keys.ToList();
        var theirUnits = _ai.Blackboard.TheirActiveBattleAIUnits;
        for( int i = 0; i < theirUnits.Count; i++ )
        {
            var enemy = theirUnits[i];
            var ourLeft = _ai.GetPokemonAs_Adapter( myUnits[0].Pokemon );
            var ourRight = _ai.GetPokemonAs_Adapter( myUnits[1].Pokemon );

            //--Enemy vs Our Left
            _ai.SetCurrentUnitAdapter( ourLeft );
            var brainLeft = _ai.ThreatIntent.ReadThreatBrain( enemy, ourLeft );
            var ticLeft = _ai.ThreatIntent.GetThreatCandidates( enemy, ourLeft, brainLeft );
            var tirLeft = _ai.ThreatIntent.GetThreatIntentResult( ticLeft, brainLeft );

            //--Enemy vs Our Right
            _ai.SetCurrentUnitAdapter( ourRight );
            var brainRight = _ai.ThreatIntent.ReadThreatBrain( enemy, ourRight );
            var ticRight = _ai.ThreatIntent.GetThreatCandidates( enemy, ourRight, brainRight );
            var tirRight = _ai.ThreatIntent.GetThreatIntentResult( ticRight, brainRight );

            if( i == 0 )
            {
                tim.EnemyLeft.Add( ourLeft.Pokemon, tirLeft );
                tim.EnemyLeft.Add( ourRight.Pokemon, tirRight );
            }
            else
            {
                tim.EnemyRight.Add( ourLeft.Pokemon, tirLeft );
                tim.EnemyRight.Add( ourRight.Pokemon, tirRight );
            }
        }

        return tim;
    }

    private Dictionary<PairPattern, PatternIntentPack> GetPatterns( ThreatInteractionMatrix tim )
    {
        Dictionary<PairPattern, PatternIntentPack> patterns = new();

        //--Covered Setup
        if( Detect_CoveredSetup( tim ) is var coveredSetup && coveredSetup.PackFound )
            patterns.Add( PairPattern.CoveredSetup, coveredSetup );

        //--Focus Fire
        if( DetectPattern_FocusFire( tim ) is var focusFire && focusFire.PackFound )
            patterns.Add( PairPattern.FocusFire, focusFire );

        //--Speed Control
        if( DetectPattern_SpeedControl( tim ) is var speedControl && speedControl.PackFound )
            patterns.Add( PairPattern.SpeedControl, speedControl );

        //--Offensive Pressure
        if( DetectPattern_DoubleAttack( tim ) is var doubleAttack && doubleAttack.PackFound )
            patterns.Add( PairPattern.DoubleAttack, doubleAttack );

        //--Defensive Reset
        if( DetectPattern_CoveredSwitch( tim ) is var coveredSwitch && coveredSwitch.PackFound )
            patterns.Add( PairPattern.CoveredSwitch, coveredSwitch );

        if( DetectPattern_AttackAndSwitch( tim ) is var attackSwitch && attackSwitch.PackFound )
            patterns.Add( PairPattern.AttackAndSwitch, attackSwitch );

        if( DetectPattern_WeatherChange( tim ) is var weatherChange && weatherChange.PackFound )
            patterns.Add( PairPattern.WeatherChange, weatherChange );

        if( DetectPattern_AfterYouAndAttack( tim ) is var afterYouAttack && afterYouAttack.PackFound )
            patterns.Add( PairPattern.AfterYouAndAttack, afterYouAttack );

        if( patterns.Count > 0 )
            LogPatterns( patterns );

        return patterns;
    }

    private PatternIntentMatch CreatePIM( ThreatIntentResult tir, bool isPrimary, HashSet<PairObservation> observations, bool found = false )
    {
        PatternIntentMatch pim = new()
        {
            Found = false,
            MatchingTIR = default,
            Evidence = 0,
            RelativeStrength = 0f,
        };

        if( found )
            return FinishFoundPIM( ref pim, tir, isPrimary, observations );

        return pim;
    }

    private PatternIntentMatch FinishFoundPIM( ref PatternIntentMatch pim, ThreatIntentResult tir, bool isPrimary, HashSet<PairObservation> observations )
    {
        pim.Found = true;
        pim.MatchingTIR = tir;
        pim.MatchingIntent = isPrimary ? tir.PrimaryIntent : tir.SecondaryIntent;
        pim.IsPrimary = isPrimary;
        pim.Evidence = pim.MatchingIntent.Evidence;
        pim.RelativeStrength = pim.MatchingIntent.Evidence / (float)tir.TotalEvidence;
        pim.Observations = observations.ToList();

        return pim;
    }

    private void LogPatterns( Dictionary<PairPattern, PatternIntentPack> patterns )
    {
        CustomLogSession patternLog = new();
        var ourUnits = _ai.Blackboard.MyActiveUnits.Keys.ToList();
        var ourLeft = ourUnits[0].Pokemon.NickName;
        var ourRight = ourUnits[1].Pokemon.NickName;

        var theirUnits = _ai.Blackboard.TheirActiveBattleAIUnits;
        var theirLeft = theirUnits[0].Name;
        var theirRight = theirUnits[1].Name;

        patternLog.Add( $"================================" );
        patternLog.Add( $"=====[Pair Intent Patterns]=====" );
        patternLog.Add( $"================================" );
        patternLog.Add( $"" );
        patternLog.Add( $"Patterns found for this round: {patterns.Count}" );
        patternLog.Add( $"" );
        patternLog.Add( $"Our Left Unit: {ourLeft}" );
        patternLog.Add( $"Our Right Unit: {ourRight}" );
        patternLog.Add( $"" );
        patternLog.Add( $"Their Left Unit: {theirLeft}" );
        patternLog.Add( $"Their Right Unit: {theirRight}" );
        patternLog.Add( $"" );

        foreach( var kvp in patterns )
        {
            var pattern = kvp.Key;
            var pip = kvp.Value;

            var leftIntent = pip.UnitLeftMatch.MatchingIntent;
            var rightIntent = pip.UnitRightMatch.MatchingIntent;
            //--Get Left Intent Result
            var leftIntentResult = leftIntent.IntentResult;

            //--Get Right Intent Result
            var rightIntentResult = rightIntent.IntentResult;

            //--Attackers are the ai's opponents
            var leftEnemy = leftIntentResult?.Top.Attacker;
            var rightEnemy = rightIntentResult?.Top.Attacker;

            //--Opponents are the ai's units
            var leftEnemyTarget = leftIntentResult?.Top.Opponent;
            var rightEnemyTarget = rightIntentResult?.Top.Opponent;

            patternLog.Add( $"===[{pattern}]===" );
            patternLog.Add( $"Matching pattern for Left had {leftEnemy?.Name} vs {leftEnemyTarget?.Name}, Intent: {leftIntent.ActionType}, Is Primary: {pip.UnitLeftMatch.IsPrimary}, Evidence: {pip.UnitLeftMatch.Evidence}, Relative Strength: {pip.UnitLeftMatch.RelativeStrength}" );
            patternLog.Add( $"Matching pattern for Right had {rightEnemy?.Name} vs {rightEnemyTarget?.Name}, Intent: {rightIntent.ActionType}, Is Primary: {pip.UnitRightMatch.IsPrimary}, Evidence: {pip.UnitRightMatch.Evidence}, Relative Strength: {pip.UnitRightMatch.RelativeStrength}" );

            var leftType = leftIntent.ActionType;
            if( leftType == ActionType.Attack || leftType == ActionType.Setup || leftType == ActionType.OffensiveStatus || leftType == ActionType.SupportiveStatus || leftType == ActionType.Protect )
                patternLog.Add( $"Enemy Unit {leftEnemy?.Name} is attacking {leftEnemyTarget?.Name} with {leftIntentResult?.Move.MoveSO.Name}" );
            else
                patternLog.Add( $"Enemy Unit {leftEnemy?.Name} is switching into: {leftIntentResult?.Candidate?.Name} due to our {leftEnemyTarget?.Name}" );

            var rightType = rightIntent.ActionType;
            if( rightType == ActionType.Attack || rightType == ActionType.Setup || rightType == ActionType.OffensiveStatus || rightType == ActionType.SupportiveStatus || rightType == ActionType.Protect )
                patternLog.Add( $"Enemy Unit {rightEnemy?.Name} is attacking {rightEnemyTarget?.Name} with {rightIntentResult?.Move.MoveSO.Name}" );
            else
                patternLog.Add( $"Enemy Unit {rightEnemy?.Name} is switching into: {rightIntentResult?.Candidate?.Name} due to our {rightEnemyTarget?.Name}" );

            patternLog.Add( $"" );
        }

        // Debug.Log( patternLog.ToString() );
        string path = Application.persistentDataPath + "/Pair Intent Patterns_Log.txt";
        System.IO.File.AppendAllText( path, patternLog.ToString() + "\n" + "\n" + "\n" + "\n" + "\n" );
        patternLog.Clear();
    }

    private PairObservationEvidence EmitPairObservation( PairObservation observation, PairPattern pattern, PatternIntentMatch pim )
    {
        PairObservationEvidence poe = new()
        {
            Type = observation,
            SourcePattern = pattern,
            SourceIntent = pim.MatchingTIR,
            Weight = pim.IsPrimary ? 2 : 1,
            IsPrimary = pim.IsPrimary,
        };

        return poe;
    }

    private List<PairObservationEvidence> GetAllPairObservationEvidence( Dictionary<PairPattern, PatternIntentPack> patterns )
    {
        List<PairObservationEvidence> poe = new();

        foreach( var kvp in patterns )
        {
            var pattern = kvp.Key;

            var leftPIM = kvp.Value.UnitLeftMatch;
            List<PairObservation> leftObservations = leftPIM.Observations ?? new();

            foreach( var observation in leftObservations )
            {
                var evidence = EmitPairObservation( observation, pattern, leftPIM );
                poe.Add( evidence );
            }

            var rightPIM = kvp.Value.UnitRightMatch;
            List<PairObservation> rightObservations = rightPIM.Observations ?? new();

            foreach( var observation in rightObservations )
            {
                var evidence = EmitPairObservation( observation, pattern, rightPIM );
                poe.Add( evidence );
            }
        }

        // LogPairObservationEvidence( poe );

        return poe;
    }

    private void LogPairObservationEvidence( List<PairObservationEvidence> poe )
    {
        CustomLogSession poeLog = new();

        poeLog.Add( $"=====================================" );
        poeLog.Add( $"=====[Pair Observation Evidence]=====" );
        poeLog.Add( $"=====================================" );
        poeLog.Add( $"" );

        foreach( var ev in poe )
        {
            poeLog.Add( $"Pair Observation: {ev.Type}" );
            poeLog.Add( $"Source Pattern: {ev.SourcePattern}" );

            if( ev.Weight == 2 )
                poeLog.Add( $"Source Intent: {ev.SourceIntent.PrimaryIntent.ActionType}" );
            else
                poeLog.Add( $"Source Intent: {ev.SourceIntent.SecondaryIntent.ActionType}" );

            poeLog.Add( $"Weight: {ev.Weight}" );
            poeLog.Add( $"" );
        }

        // Debug.Log( poeLog.ToString() );
        string path = Application.persistentDataPath + "/PairObservationEvidence_Log.txt";
        System.IO.File.AppendAllText( path, poeLog.ToString() + "\n" + "\n" + "\n" + "\n" + "\n" );
        poeLog.Clear();
    }

    private bool TryGetObservationProfile( PairObservationEvidence evidence, PairObservation observation, out PairObservationProfile profile )
    {
        if( evidence.Type == observation )
        {
            profile = ExtractObservationProfile( evidence );
            return true;
        }
        else
        {
            profile = default;
            return false;
        }
    }

    private PairObservationProfile ExtractObservationProfile( PairObservationEvidence evidence )
    {
        var intentResult = evidence.IsPrimary ? evidence.SourceIntent.PrimaryIntent.IntentResult : evidence.SourceIntent.SecondaryIntent.IntentResult;
        var top = intentResult.Top;
        var us = top.Opponent;
        var ourAlly = _ai.GetActiveAllyAs_Adapter( us.Pokemon );
        var them = top.Attacker;
        var theirAlly = _ai.GetActiveAllyAs_Adapter( them.Pokemon );
        var move = intentResult.Move;

        bool weHaveAlly = ourAlly != null;
        bool theyHaveAlly = theirAlly != null;

        return new()
        {
            IntentResult = intentResult,
            Top = top,
            Us = us,
            OurAlly = ourAlly,
            Them = them,
            TheirAlly = theirAlly,
            Move = move,
            WeHaveAlly = weHaveAlly,
            TheyHaveAlly = theyHaveAlly,
        };
    }

    private Dictionary<PairObservation, ( int Tally, PairObservationEvidence Evidence )> AccumulateObservationEvidence( List<PairObservationEvidence> poe )
    {
        Dictionary<PairObservation, ( int Tally, PairObservationEvidence Evidence )> observationTalles = new();

        foreach( var ev in poe )
        {
            if( observationTalles.ContainsKey( ev.Type ) )
            {
                int tally = ev.Weight;
                var obvs = observationTalles[ev.Type];
                obvs.Tally += tally;

                observationTalles[ev.Type] = obvs;
            }
            else
            {
                observationTalles.Add( ev.Type, ( ev.Weight, ev ) );
            }
        }

        // LogObservationTallies( observationTalles );

        return observationTalles;
    }

    private void LogObservationTallies( Dictionary<PairObservation, ( int Tally, PairObservationEvidence Evidence )> observationTalles )
    {
        CustomLogSession otLog = new();

        otLog.Add( $"=====================================" );
        otLog.Add( $"=====[Observation Tally Results]=====" );
        otLog.Add( $"=====================================" );
        otLog.Add( $"" );

        foreach( var kvp in observationTalles )
        {
            otLog.Add( $"Observation: {kvp.Key}, Tally: {kvp.Value.Tally}" );
        }

        // Debug.Log( otLog.ToString() );
        string path = Application.persistentDataPath + "/PairObservationTallies_Log.txt";
        System.IO.File.AppendAllText( path, otLog.ToString() + "\n" + "\n" + "\n" + "\n" + "\n" );
        otLog.Clear();
    }

    private HashSet<PairStrategy> GetAvailableStrategies( Dictionary<PairPattern, PatternIntentPack> patterns, List<PairObservationEvidence> poe )
    {
        HashSet<PairStrategy> strategies = new();
        HashSet<PairObservation> observations = new();

        foreach( var ob in poe )
        {
            if( !observations.Contains( ob.Type ) )
            {
                observations.Add( ob.Type );
            }
        }

        //--Each Strategy requires at least one pattern or one observation in order to be added to the list of available strategies.
        //--most will require a combination of patterns and observations.
        //--since strategy requirements come from patterns and observations, we will simply make a block for each strategy.

        //--Establish Trick Room
        if( observations.Contains( PairObservation.TrickRoom ) )
        {
            strategies.Add( PairStrategy.EstablishTrickRoom );
        }

        //-Establish Tailwind
        if( observations.Contains( PairObservation.Tailwind ) )
        {
            strategies.Add( PairStrategy.EstablishTailwind );
        }

        //--Weather Pivot
        if( observations.Contains( PairObservation.WeatherChange ) && observations.Contains( PairObservation.Switch ) )
        {
            strategies.Add( PairStrategy.WeatherPivot );
        }

        //--Secure Immeidate KO
        if( patterns.ContainsKey( PairPattern.FocusFire ) && patterns.ContainsKey( PairPattern.DoubleAttack ) && observations.Contains( PairObservation.Attack ) )
        {
            strategies.Add( PairStrategy.SecureImmediateKO );
        }

        //--Apply Board Pressure
        if( patterns.ContainsKey( PairPattern.FocusFire ) || patterns.ContainsKey( PairPattern.DoubleAttack ) || observations.Contains( PairObservation.Attack ) )
        {
            strategies.Add( PairStrategy.ApplyBoardPressure );
        }

        //--Preserve Tempo
        if( observations.Contains( PairObservation.Switch ) && observations.Contains( PairObservation.PivotPressure ) )
        {
            strategies.Add( PairStrategy.PreserveTempo );
        }

        //--Deny Opponent Setup
        if( observations.Contains( PairObservation.Cover ) || observations.Contains( PairObservation.StatusDisruption ) )
        {
            strategies.Add( PairStrategy.DenyOpponentSetup );
        }

        //--Establish Defensive Position
        if( patterns.ContainsKey( PairPattern.CoveredSwitch ) || observations.Contains( PairObservation.ScreensSupport ) || observations.Contains( PairObservation.Protect ) )
        {
            strategies.Add( PairStrategy.EstablishDefensivePosition );
        }

        if( patterns.ContainsKey( PairPattern.AfterYouAndAttack ) )
        {
            strategies.Add( PairStrategy.AfterYouAttack );
        }
        

        return strategies;
    }

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private PatternIntentPack Detect_CoveredSetup( ThreatInteractionMatrix tim )
    {
        PatternIntentPack pip = new()
        {
            UnitLeftMatch = default,
            UnitRightMatch = default,
            PackFound = false,
        };

        var unitLeft_SetupIntent = FindSetupIntent( tim.EnemyLeft );
        var unitRight_SetupIntent = FindSetupIntent( tim.EnemyRight );

        if( !unitLeft_SetupIntent.Found && !unitRight_SetupIntent.Found )
            return pip;

        var unitLeft_CoverIntent = FindCoverAllyIntent( tim.EnemyLeft );
        var unitRight_CoverIntent = FindCoverAllyIntent( tim.EnemyRight );

        if( !unitLeft_CoverIntent.Found && !unitRight_CoverIntent.Found )
            return pip;

        if( unitLeft_SetupIntent.Found && unitRight_CoverIntent.Found )
        {
            pip.UnitLeftMatch = unitLeft_SetupIntent;
            pip.UnitRightMatch = unitRight_CoverIntent;
            pip.PackFound = true;
            return pip;
        }

        if( unitLeft_CoverIntent.Found && unitRight_SetupIntent.Found )
        {
            pip.UnitLeftMatch = unitLeft_CoverIntent;
            pip.UnitRightMatch = unitRight_SetupIntent;
            pip.PackFound = true;
            return pip;
        }

        return pip;
    }

    private PatternIntentPack DetectPattern_FocusFire( ThreatInteractionMatrix tim )
    {
        PatternIntentPack pip = new()
        {
            UnitLeftMatch = default,
            UnitRightMatch = default,
            PackFound = false,
        };

        var unitLeft_AttackIntent = FindAttackIntent( tim.EnemyLeft );

        if( !unitLeft_AttackIntent.Found )
            return pip;

        var unitRight_AttackIntent = FindAttackIntent( tim.EnemyRight );

        if( !unitRight_AttackIntent.Found )
            return pip;

        bool leftIsPrimary = false;
        bool rightIsPrimary = false;
        List<( ThreatIntentResult tir, bool primary )> leftMatches = new();
        foreach( var kvpLeft in tim.EnemyLeft )
        {
            var leftTIR = kvpLeft.Value;
            var leftPrimary = leftTIR.PrimaryIntent;
            var leftSecondary = leftTIR.SecondaryIntent;
            MoveThreatResult leftMTR = null;

            if( leftTIR.PrimaryIntent.ActionType == ActionType.Attack )
            {
                leftMTR = (MoveThreatResult)leftTIR.PrimaryIntent.IntentResult;
                leftIsPrimary = true;
                leftMatches.Add( ( leftTIR, leftIsPrimary ) );
                break;
            }
            else if( leftTIR.CheckSecondaryIntent && leftTIR.SecondaryIntent.ActionType == ActionType.Attack )
            {
                leftMTR = (MoveThreatResult)leftTIR.SecondaryIntent.IntentResult;
                leftIsPrimary = false;
                leftMatches.Add( ( leftTIR, leftIsPrimary ) );
                break;
            }
            else
                continue;
        }

        List<( ThreatIntentResult tir, bool primary )> rightMatches = new();
        foreach( var kvpRight in tim.EnemyRight )
        {
            var rightTIR = kvpRight.Value;
            var rightPrimary = rightTIR.PrimaryIntent;
            var rightSecondary = rightTIR.SecondaryIntent;
            MoveThreatResult rightMTR = null;

            if( rightTIR.PrimaryIntent.ActionType == ActionType.Attack )
            {
                rightMTR = (MoveThreatResult)rightTIR.PrimaryIntent.IntentResult;
                rightIsPrimary = true;
                rightMatches.Add( ( rightTIR, rightIsPrimary ) );
                break;
            }
            else if( rightTIR.CheckSecondaryIntent && rightTIR.SecondaryIntent.ActionType == ActionType.Attack )
            {
                rightMTR = (MoveThreatResult)rightTIR.SecondaryIntent.IntentResult;
                rightIsPrimary = false;
                rightMatches.Add( ( rightTIR, rightIsPrimary ) );
                break;
            }
            else
                continue;
        }

        int attackOverlaps = 0;
        ThreatIntentResult leftMatch = default;
        ThreatIntentResult rightMatch = default;
        foreach( var left in leftMatches )
        {
            var leftIntent = left.primary ? (MoveThreatResult)left.tir.PrimaryIntent.IntentResult : (MoveThreatResult)left.tir.SecondaryIntent.IntentResult;
            var leftTarget = leftIntent.Targets[0];

            foreach( var right in rightMatches )
            {
                var rightIntent = right.primary ? (MoveThreatResult)right.tir.PrimaryIntent.IntentResult : (MoveThreatResult)right.tir.SecondaryIntent.IntentResult;
                var rightTarget =  rightIntent.Targets[0];
                
                if( leftTarget == rightTarget )
                {
                    attackOverlaps++;
                    leftMatch = left.tir;
                    rightMatch = right.tir;
                }
            }
        }

        if( attackOverlaps > 0 )
        {
            HashSet<PairObservation> leftObservations = new(){ PairObservation.FocusFire };
            PatternIntentMatch leftPim = CreatePIM( leftMatch, leftIsPrimary, leftObservations, true );

            HashSet<PairObservation> rightObservations = new(){ PairObservation.FocusFire };
            PatternIntentMatch rightPim = CreatePIM( rightMatch, rightIsPrimary, rightObservations, true );

            pip.UnitLeftMatch = leftPim;
            pip.UnitRightMatch = rightPim;
            pip.PackFound = true;
        }

        return pip;
    }

    private PatternIntentPack DetectPattern_SpeedControl( ThreatInteractionMatrix tim )
    {
        PatternIntentPack pip = new()
        {
            UnitLeftMatch = default,
            UnitRightMatch = default,
            PackFound = false,
        };

        var unitLeft_SpeedControlIntent = FindSpeedControlIntent( tim.EnemyLeft );
        var unitRight_SpeedControlIntent = FindSpeedControlIntent( tim.EnemyRight );

        if( !unitLeft_SpeedControlIntent.Found && !unitRight_SpeedControlIntent.Found )
        {
            return pip;
        }
        else if( unitLeft_SpeedControlIntent.Found && !unitRight_SpeedControlIntent.Found )
        {
            pip.UnitLeftMatch = unitLeft_SpeedControlIntent;
            pip.UnitRightMatch = FindHighestIntent( tim.EnemyRight );
            pip.PackFound = true;
        }
        else if( !unitLeft_SpeedControlIntent.Found && unitRight_SpeedControlIntent.Found )
        {
            pip.UnitLeftMatch = FindHighestIntent( tim.EnemyLeft );
            pip.UnitRightMatch = unitRight_SpeedControlIntent;
            pip.PackFound = true;
        }
        else if( unitLeft_SpeedControlIntent.Found && unitRight_SpeedControlIntent.Found )
        {
            pip.UnitLeftMatch = unitLeft_SpeedControlIntent;
            pip.UnitRightMatch = unitRight_SpeedControlIntent;
            pip.PackFound = true;
        }

        return pip;
    }

    private PatternIntentPack DetectPattern_DoubleAttack( ThreatInteractionMatrix tim )
    {
        PatternIntentPack pip = new()
        {
            UnitLeftMatch = default,
            UnitRightMatch = default,
            PackFound = false,
        };

        var unitLeft_AttackIntent = FindAttackIntent( tim.EnemyLeft );

        if( !unitLeft_AttackIntent.Found )
            return pip;

        var unitRight_AttackIntent = FindAttackIntent( tim.EnemyRight );

        if( !unitRight_AttackIntent.Found )
            return pip;

        if( unitLeft_AttackIntent.Found && unitRight_AttackIntent.Found )
        {
            pip.UnitLeftMatch = unitLeft_AttackIntent;
            pip.UnitLeftMatch.Observations = new(){ PairObservation.DoubleAttack };

            pip.UnitRightMatch = unitRight_AttackIntent;
            pip.UnitRightMatch.Observations = new(){ PairObservation.DoubleAttack };

            pip.PackFound = true;
            return pip;
        }

        return pip;
    }

    private PatternIntentPack DetectPattern_CoveredSwitch( ThreatInteractionMatrix tim )
    {
        PatternIntentPack pip = new()
        {
            UnitLeftMatch = default,
            UnitRightMatch = default,
            PackFound = false,
        };

        var unitLeft_SwitchIntent = FindSwitchIntent( tim.EnemyLeft );
        var unitRight_SwitchIntent = FindSwitchIntent( tim.EnemyRight );

        if( !unitLeft_SwitchIntent.Found && !unitRight_SwitchIntent.Found )
            return pip;

        var unitLeft_CoverAllyIntent = FindCoverAllyIntent( tim.EnemyLeft );
        var unitRight_CoverAllyIntent = FindCoverAllyIntent( tim.EnemyRight );

        if( !unitLeft_CoverAllyIntent.Found && !unitRight_CoverAllyIntent.Found )
            return pip;

        if( unitLeft_SwitchIntent.Found && unitRight_CoverAllyIntent.Found )
        {
            pip.UnitLeftMatch = unitLeft_SwitchIntent;
            pip.UnitRightMatch = unitRight_CoverAllyIntent;
            pip.PackFound = true;
            return pip;
        }

        if( unitLeft_CoverAllyIntent.Found && unitRight_SwitchIntent.Found )
        {
            pip.UnitLeftMatch = unitLeft_CoverAllyIntent;
            pip.UnitRightMatch = unitRight_SwitchIntent;
            pip.PackFound = true;
            return pip;
        }

        return pip;
    }

    private PatternIntentPack DetectPattern_AttackAndSwitch( ThreatInteractionMatrix tim )
    {
        PatternIntentPack pip = new()
        {
            UnitLeftMatch = default,
            UnitRightMatch = default,
            PackFound = false,
        };

        var unitLeft_SwitchIntent = FindSwitchIntent( tim.EnemyLeft );
        var unitRight_SwitchIntent = FindSwitchIntent( tim.EnemyRight );

        if( !unitLeft_SwitchIntent.Found && !unitRight_SwitchIntent.Found )
            return pip;

        var unitLeft_AttackIntent = FindAttackIntent( tim.EnemyLeft );
        var unitRight_AttackIntent = FindAttackIntent( tim.EnemyRight );

        if( !unitLeft_AttackIntent.Found && !unitRight_AttackIntent.Found )
            return pip;

        if( unitLeft_SwitchIntent.Found && unitRight_AttackIntent.Found )
        {
            pip.UnitLeftMatch = unitLeft_SwitchIntent;
            pip.UnitRightMatch = unitRight_AttackIntent;
            pip.PackFound = true;
            return pip;
        }

        if( unitLeft_AttackIntent.Found && unitRight_SwitchIntent.Found )
        {
            pip.UnitLeftMatch = unitLeft_AttackIntent;
            pip.UnitRightMatch = unitRight_SwitchIntent;
            pip.PackFound = true;
            return pip;
        }

        return pip;
    }

    private PatternIntentPack DetectPattern_WeatherChange( ThreatInteractionMatrix tim )
    {
        PatternIntentPack pip = new()
        {
            UnitLeftMatch = default,
            UnitRightMatch = default,
            PackFound = false,
        };

        var unitLeft_WeatherChangeIntent = FindWeatherChangeIntent( tim.EnemyLeft );
        var unitRight_WeatherChangeIntent = FindWeatherChangeIntent( tim.EnemyRight );

        if( unitLeft_WeatherChangeIntent.Found )
        {
            pip.UnitLeftMatch = unitLeft_WeatherChangeIntent;
            pip.UnitRightMatch = FindHighestIntent( tim.EnemyRight );
            pip.PackFound = true;
        }

        if( unitRight_WeatherChangeIntent.Found )
        {
            pip.UnitLeftMatch = FindHighestIntent( tim.EnemyLeft );
            pip.UnitRightMatch = unitRight_WeatherChangeIntent;
            pip.PackFound = true;
        }

        return pip;
    }

    private PatternIntentPack DetectPattern_AfterYouAndAttack( ThreatInteractionMatrix tim )
    {
        PatternIntentPack pip = new()
        {
            UnitLeftMatch = default,
            UnitRightMatch = default,
            PackFound = false,
        };

        var unitLeft_AfterYouIntent = FindAfterYouIntent( tim.EnemyLeft );
        var unitRight_AfterYouIntent = FindAfterYouIntent( tim.EnemyRight );

        if( !unitLeft_AfterYouIntent.Found && !unitRight_AfterYouIntent.Found )
            return pip;

        var unitLeft_AttackIntent = FindAttackIntent( tim.EnemyLeft );
        var unitRight_AttackIntent = FindAttackIntent( tim.EnemyRight );

        if( unitLeft_AfterYouIntent.Found && unitRight_AttackIntent.Found )
        {
            pip.UnitLeftMatch = unitLeft_AfterYouIntent;
            pip.UnitRightMatch = unitRight_AttackIntent;
            pip.PackFound = true;
            return pip;
        }

        if( unitRight_AfterYouIntent.Found && unitLeft_AttackIntent.Found )
        {
            pip.UnitLeftMatch = unitLeft_AttackIntent;
            pip.UnitRightMatch = unitRight_AfterYouIntent;
            pip.PackFound = true;
            return pip;
        }

        return pip;
    }

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private PatternIntentMatch FindHighestIntent( Dictionary<Pokemon, ThreatIntentResult> threatInteractions )
    {
        PatternIntentMatch pim = new();

        ThreatIntentResult foundTIR = default;

        int evidence = int.MinValue;
        foreach( var interaction in threatInteractions )
        {
            var tir = interaction.Value;

            if( tir.TotalEvidence > evidence )
            {
                evidence = tir.TotalEvidence;
                foundTIR = tir;
            }
        }

        switch( foundTIR.PrimaryIntent.ActionType )
        {
            case ActionType.Attack:

                pim = FindAttackIntent( threatInteractions );

            break;

            case ActionType.DefensiveSwitch:

                pim = FindSwitchIntent( threatInteractions );

            break;

            case ActionType.OffensiveSwitch:

                pim = FindSwitchIntent( threatInteractions );

            break;

            case ActionType.Setup:
            
                pim = FindSetupIntent( threatInteractions );

            break;

            case ActionType.OffensiveStatus:

                pim = FindCoverAllyIntent( threatInteractions );

                if( !pim.Found )
                    pim = FindSetupIntent( threatInteractions );

                if( !pim.Found )
                    pim = FindSpeedControlIntent( threatInteractions );

            break;

            case ActionType.SupportiveStatus:

                pim = FindSetupIntent( threatInteractions );

                if( !pim.Found )
                    pim = FindCoverAllyIntent( threatInteractions );

                if( !pim.Found )
                    pim = FindSpeedControlIntent( threatInteractions );

                if( !pim.Found )
                    pim = FindAfterYouIntent( threatInteractions );

                if( !pim.Found )
                    pim = FindWeatherChangeIntent( threatInteractions );

            break;

            case ActionType.Protect:
            break;
        }

        return pim;
    }

    private PatternIntentMatch FindAttackIntent( Dictionary<Pokemon, ThreatIntentResult> threatInteractions )
    {
        PatternIntentMatch pim = new()
        {
            Found = false,
            MatchingTIR = default,
            Evidence = 0,
            RelativeStrength = 0f,
        };

        ThreatIntentResult foundTIR = default;
        bool found = false;
        bool isPrimary = false;
        HashSet<PairObservation> observations = new();

        foreach( var interaction in threatInteractions )
        {
            var tir = interaction.Value;

            if( ( tir.PrimaryIntent.ActionType == ActionType.Attack ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.Attack ) )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.Attack;
                found = true;
                isPrimary = primary;
                foundTIR = interaction.Value;
                break;
            }
        }

        if( found )
        {
            observations.Add( PairObservation.Attack );
            pim = FinishFoundPIM( ref pim, foundTIR, isPrimary, observations );
        }

        return pim;
    }

    private PatternIntentMatch FindSetupIntent( Dictionary<Pokemon, ThreatIntentResult> threatInteractions )
    {
        PatternIntentMatch pim = new()
        {
            Found = false,
            MatchingTIR = default,
            Evidence = 0,
            RelativeStrength = 0f,
        };

        ThreatIntentResult tir = default;
        bool found = false;
        bool isPrimary = false;
        HashSet<PairObservation> observations = new();

        foreach( var interaction in threatInteractions )
        {
            tir = interaction.Value;

            if( ( tir.PrimaryIntent.ActionType == ActionType.Setup ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.Setup ) )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.Setup;
                found = true;
                isPrimary = primary;
                observations.Add( PairObservation.StatBoost );
                break;
            }

            if( ( tir.PrimaryIntent.ActionType == ActionType.SupportiveStatus ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.SupportiveStatus ) )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.SupportiveStatus;
                StatusThreatResult suppStatus = primary ? (StatusThreatResult)tir.PrimaryIntent.IntentResult : (StatusThreatResult)tir.SecondaryIntent.IntentResult;

                if( suppStatus.SupportiveStatusType == SupportiveStatusType.ForceMultiplier || suppStatus.SupportiveStatusType == SupportiveStatusType.BattlefieldControl )
                {
                    var effects = suppStatus.Move.MoveSO.MoveEffects;
                    found = true;
                    isPrimary = primary;

                    if( effects.Weather != WeatherConditionID.None )
                        observations.Add( PairObservation.WeatherChange );

                    if( effects.Terrain != TerrainID.None )
                        observations.Add( PairObservation.TerrainChange );

                    if( effects.CourtCondition == CourtConditionID.Tailwind )
                        observations.Add( PairObservation.Tailwind );

                    if( effects.CourtCondition == CourtConditionID.Reflect )
                        observations.Add( PairObservation.ScreensSupport );

                    if( effects.CourtCondition == CourtConditionID.LightScreen )
                        observations.Add( PairObservation.ScreensSupport );

                    if( effects.CourtCondition == CourtConditionID.AuroraVeil )
                        observations.Add( PairObservation.ScreensSupport );

                    if( effects.FieldCondition == FieldConditionID.TrickRoom )
                        observations.Add( PairObservation.TrickRoom );

                    break;
                }
            }

            if( ( tir.PrimaryIntent.ActionType == ActionType.OffensiveStatus ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.OffensiveStatus ) )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.OffensiveStatus;
                StatusThreatResult offStatus = primary ? (StatusThreatResult)tir.PrimaryIntent.IntentResult : (StatusThreatResult)tir.SecondaryIntent.IntentResult;

                if( offStatus.OffensiveStatusType == OffensiveStatusType.EntryHazard )
                {
                    found = true;
                    isPrimary = primary;
                    observations.Add( PairObservation.EntryHazard );
                    break;
                }
            }
        }

        if( found )
        {
            observations.Add( PairObservation.Setup );
            pim = FinishFoundPIM( ref pim, tir, isPrimary, observations );
        }

        return pim;
    }

    private PatternIntentMatch FindCoverAllyIntent( Dictionary<Pokemon, ThreatIntentResult> threatInteractions )
    {
        PatternIntentMatch pim = new()
        {
            Found = false,
            MatchingTIR = default,
            Evidence = 0,
            RelativeStrength = 0f,
        };

        ThreatIntentResult tir = default;
        bool found = false;
        bool isPrimary = false;

        HashSet<PairObservation> observations = new();

        foreach( var interaction in threatInteractions )
        {
            tir = interaction.Value;
        
            if( tir.PrimaryIntent.ActionType == ActionType.Attack || tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.Attack )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.Attack;
                MoveThreatResult mtr = primary ? (MoveThreatResult)tir.PrimaryIntent.IntentResult : (MoveThreatResult)tir.SecondaryIntent.IntentResult;
                var move = mtr.Move;
                var name = move.MoveSO.Name;
                var effects = move.MoveSO.MoveEffects;

                if( name == "Fake Out" )
                {
                    found = true;
                    isPrimary = primary;
                    observations.Add( PairObservation.FakeOutSupport );
                    break;
                }
            }

            if( ( tir.PrimaryIntent.ActionType == ActionType.OffensiveStatus ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.SupportiveStatus ) )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.OffensiveStatus;
                var offStatus = primary ? (StatusThreatResult)tir.PrimaryIntent.IntentResult : (StatusThreatResult)tir.SecondaryIntent.IntentResult;

                if( offStatus.OffensiveStatusType == OffensiveStatusType.Disruption )
                {
                    found = true;
                    isPrimary = primary;
                    observations.Add( PairObservation.StatusDisruption );
                    break;
                }

                if( offStatus.OffensiveStatusType == OffensiveStatusType.StatusEffect )
                {
                    //--This is exactly where unit comparison would be very convenient. i can get the ally, get the target, and check "current ptko" for both and use that to gate
                    //--a wisp/hoarfrost use as cover. using wisp to reduce the attack of an offensive opponent IS a valid and common way to cover your ally. for now i will just bypass the check
                    //--and look for the status + stat alignment
                    var oppBiases = offStatus.Top.Opponent.RoleProfile.Biases;
                    var ss = offStatus.Move.MoveSO.MoveEffects.SevereStatus;
                    if( ( oppBiases.Contains( RoleBias.Physical ) && ss == SevereConditionID.BRN ) || oppBiases.Contains( RoleBias.Special ) && ss == SevereConditionID.FBT )
                    {
                        found = true;
                        isPrimary = primary;
                        observations.Add( PairObservation.StatusDisruption );
                        break;
                    }
                }
            }

            if( ( tir.PrimaryIntent.ActionType == ActionType.SupportiveStatus ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.SupportiveStatus ) )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.SupportiveStatus;
                StatusThreatResult suppStatus = primary ? (StatusThreatResult)tir.PrimaryIntent.IntentResult : (StatusThreatResult)tir.SecondaryIntent.IntentResult;

                var move = suppStatus.Move;
                var effects = move.MoveSO.MoveEffects;

                if( suppStatus.SupportiveStatusType == SupportiveStatusType.AllyProtection && effects.TransientStatus == TransientConditionID.CenterOfAttention )
                {
                    found = true;
                    isPrimary = primary;
                    observations.Add( PairObservation.RedirectionSupport );
                    break;
                }

                if( effects.CourtCondition == CourtConditionID.WideGuard || effects.CourtCondition == CourtConditionID.QuickGuard )
                {
                    found = true;
                    isPrimary = primary;
                    observations.Add( PairObservation.GuardSupport );
                    break;
                }
            }
        }

        if( found )
        {
            observations.Add( PairObservation.Cover );
            pim = FinishFoundPIM( ref pim, tir, isPrimary, observations );
        }

        return pim;
    }

    private PatternIntentMatch FindSpeedControlIntent( Dictionary<Pokemon, ThreatIntentResult> threatInteractions )
    {
        PatternIntentMatch pim = new()
        {
            Found = false,
            MatchingTIR = default,
            Evidence = 0,
            RelativeStrength = 0f,
        };

        ThreatIntentResult tir = default;
        bool found = false;
        bool isPrimary = false;

        HashSet<PairObservation> observations = new();

        foreach( var interaction in threatInteractions )
        {
            tir = interaction.Value;

            //--Field speed control
            if( ( tir.PrimaryIntent.ActionType == ActionType.SupportiveStatus ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.SupportiveStatus ) )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.SupportiveStatus;
                var suppStatus = primary ? (StatusThreatResult)tir.PrimaryIntent.IntentResult : (StatusThreatResult)tir.SecondaryIntent.IntentResult;
                var move = suppStatus.Move;
                var effects = move.MoveSO.MoveEffects;

                if( effects.CourtCondition == CourtConditionID.Tailwind )
                {
                    found = true;
                    isPrimary = primary;
                    observations.Add( PairObservation.Tailwind );
                    break;
                }

                if( effects.FieldCondition == FieldConditionID.TrickRoom )
                {
                    found = true;
                    isPrimary = primary;
                    observations.Add( PairObservation.TrickRoom );
                    break;
                }

                //--Add weather interaction with weather speed ability here
                if( effects.Weather != WeatherConditionID.None )
                {
                    bool moveChangesWeather = _ai.UnitSim.Move_ChangesWeather( move );
                    bool allySpeedFromWeather = false;
                    bool opponentSpeedFromWeather = false;
                    bool opponentAllySpeedFromWeather = false;

                    var movesWeather = effects.Weather;
                    var currentWeather = _ai.Blackboard.CurrentFieldSnapshot.Weather;

                    var ally = suppStatus.Top.Attacker != null ? _ai.GetActiveAllyAs_Adapter( suppStatus.Top.Attacker.Pokemon ) : null;
                    var opp = suppStatus.Top.Opponent;
                    var oppAlly = opp != null ? _ai.GetActiveAllyAs_Adapter( opp.Pokemon ) : null;

                    if( ally != null && moveChangesWeather )
                    {
                        allySpeedFromWeather = _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( ally.Pokemon, movesWeather );
                    }

                    if( opp != null )
                    {
                        opponentSpeedFromWeather = _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( opp.Pokemon, currentWeather );

                        if( oppAlly != null )
                        {
                            opponentAllySpeedFromWeather = _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( oppAlly.Pokemon, currentWeather );
                        }
                    }

                    if( allySpeedFromWeather || ( moveChangesWeather && ( opponentSpeedFromWeather || opponentAllySpeedFromWeather ) ) )
                    {
                        found = true;
                        isPrimary = primary;
                        observations.Add( PairObservation.WeatherChange );
                        break;
                    }
                }
            }

            //--Direct speed debuff of the opponent
            if( ( tir.PrimaryIntent.ActionType == ActionType.OffensiveStatus ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.OffensiveStatus ) )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.OffensiveStatus;
                var offStatus = primary ? (StatusThreatResult)tir.PrimaryIntent.IntentResult : (StatusThreatResult)tir.SecondaryIntent.IntentResult;
                var effects = offStatus.Move.MoveSO.MoveEffects;

                if( offStatus.OffensiveStatusType == OffensiveStatusType.StatDebuff )
                {
                    if( effects.StatChangeList != null && effects.StatChangeList.Count > 0 )
                    {
                        foreach( var sc in effects.StatChangeList )
                        {
                            if( sc.Stat == Stat.Speed && sc.Change < 0 )
                            {
                                found = true;
                                isPrimary = primary;
                                observations.Add( PairObservation.StatDebuff );
                                observations.Add( PairObservation.StatusMove );
                                break;
                            }
                        }
                    }
                }
            }

            //--Attack such as icy wind that lowers opponent speed guaranteed
            if( ( tir.PrimaryIntent.ActionType == ActionType.Attack ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.Attack ) )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.Attack;
                var mtr = primary ? (MoveThreatResult)tir.PrimaryIntent.IntentResult : (MoveThreatResult)tir.SecondaryIntent.IntentResult;
                var effects = mtr.Move.MoveSO.MoveEffects;

                if( effects.StatChangeList != null && effects.StatChangeList.Count > 0 )
                {
                    foreach( var sc in effects.StatChangeList )
                    {
                        if( sc.Stat == Stat.Speed && sc.Change < 0 )
                        {
                            found = true;
                            isPrimary = primary;
                            observations.Add( PairObservation.StatDebuff );
                            observations.Add( PairObservation.Attack );
                            break;
                        }
                    }
                }
            }

            if( tir.PrimaryIntent.ActionType == ActionType.DefensiveSwitch || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.DefensiveSwitch ) )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.DefensiveSwitch;
                var scr = primary ? (SwitchCandidateResult)tir.PrimaryIntent.IntentResult : (SwitchCandidateResult)tir.SecondaryIntent.IntentResult;
                
                bool switchChangesWeather = _ai.UnitSim.Switch_ChangesWeather( scr );
                bool allySpeedFromWeather = false;
                bool opponentSpeedFromWeather = false;
                bool opponentAllySpeedFromWeather = false;

                var ourWeather = _ai.UnitSim.GetWeatherFrom_Ability( scr.Candidate.Pokemon );
                var currentWeather = _ai.Blackboard.CurrentFieldSnapshot.Weather;

                var ally = scr.Top.Attacker != null ? _ai.GetActiveAllyAs_Adapter( scr.Top.Attacker.Pokemon ) : null;
                var opp = scr.Top.Opponent;
                var oppAlly = opp != null ? _ai.GetActiveAllyAs_Adapter( opp.Pokemon ) : null;

                if( ally != null && switchChangesWeather )
                {
                    allySpeedFromWeather = _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( ally.Pokemon, ourWeather );
                }

                if( opp != null )
                {
                    opponentSpeedFromWeather = _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( opp.Pokemon, currentWeather );

                    if( oppAlly != null )
                    {
                        opponentAllySpeedFromWeather = _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( oppAlly.Pokemon, currentWeather );
                    }
                }

                if( allySpeedFromWeather || ( switchChangesWeather && ( opponentSpeedFromWeather || opponentAllySpeedFromWeather ) ) )
                {
                    found = true;
                    isPrimary = primary;
                    observations.Add( PairObservation.WeatherChange );
                    break;
                }
            }

            if( tir.PrimaryIntent.ActionType == ActionType.OffensiveSwitch || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.OffensiveSwitch ) )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.OffensiveSwitch;
                var scr = primary ? (SwitchCandidateResult)tir.PrimaryIntent.IntentResult : (SwitchCandidateResult)tir.SecondaryIntent.IntentResult;

                bool switchChangesWeather = _ai.UnitSim.Switch_ChangesWeather( scr );
                bool allySpeedFromWeather = false;
                bool opponentSpeedFromWeather = false;
                bool opponentAllySpeedFromWeather = false;

                var ourWeather = _ai.UnitSim.GetWeatherFrom_Ability( scr.Candidate.Pokemon );
                var currentWeather = _ai.Blackboard.CurrentFieldSnapshot.Weather;

                var ally = scr.Top.Attacker != null ? _ai.GetActiveAllyAs_Adapter( scr.Top.Attacker.Pokemon ) : null;
                var opp = scr.Top.Opponent;
                var oppAlly = opp != null ? _ai.GetActiveAllyAs_Adapter( opp.Pokemon ) : null;

                if( ally != null && switchChangesWeather )
                {
                    allySpeedFromWeather = _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( ally.Pokemon, ourWeather );
                }

                if( opp != null )
                {
                    opponentSpeedFromWeather = _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( opp.Pokemon, currentWeather );

                    if( oppAlly != null )
                    {
                        opponentAllySpeedFromWeather = _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( oppAlly.Pokemon, currentWeather );
                    }
                }

                if( allySpeedFromWeather || ( switchChangesWeather && ( opponentSpeedFromWeather || opponentAllySpeedFromWeather ) ) )
                {
                    found = true;
                    isPrimary = primary;
                    observations.Add( PairObservation.WeatherChange );
                    break;
                }
            }
        }

        if( found )
        {
            observations.Add( PairObservation.SpeedControl );
            pim = FinishFoundPIM( ref pim, tir, isPrimary, observations );
        }

        return pim;
    }

    private PatternIntentMatch FindSwitchIntent( Dictionary<Pokemon, ThreatIntentResult> threatIntentInteractions )
    {
        PatternIntentMatch pim = new()
        {
            Found = false,
            MatchingTIR = default,
            Evidence = 0,
            RelativeStrength = 0f,
        };

        ThreatIntentResult tir = default;
        bool found = false;
        bool isPrimary = false;

        HashSet<PairObservation> observations = new();

        foreach( var interaction in threatIntentInteractions )
        {
            tir = interaction.Value;

            if( tir.PrimaryIntent.ActionType == ActionType.DefensiveSwitch || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.DefensiveSwitch ) )
            {
                isPrimary = tir.PrimaryIntent.ActionType == ActionType.DefensiveSwitch;
                found = true;
                observations.Add( PairObservation.Preservation );
                break;
            }

            if( tir.PrimaryIntent.ActionType == ActionType.OffensiveSwitch || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.OffensiveSwitch ) )
            {
                isPrimary = tir.PrimaryIntent.ActionType == ActionType.OffensiveSwitch;
                found = true;
                observations.Add( PairObservation.PivotPressure );

                var top = isPrimary ? tir.PrimaryIntent.IntentResult.Top : tir.SecondaryIntent.IntentResult.Top;
                if( top.Attacker != null && top.AttackerPTKO >= PotentialToKO.Dangerous && ( top.AttackerMovedFirst || top.OpponentPTKO <= PotentialToKO.Risky ) )
                    observations.Add( PairObservation.RevengeKO );

                break;
            }

            if( tir.PrimaryIntent.ActionType == ActionType.Attack || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.Attack ) )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.Attack;
                var intentResult = primary ? tir.PrimaryIntent.IntentResult : tir.SecondaryIntent.IntentResult;
                var move = intentResult.Move;

                //--Pivot Move Check
                if( move.MoveEffects.SwitchType == SwitchEffectType.SelfPivot )
                {
                    isPrimary = primary;
                    found = true;
                    observations.Add( PairObservation.PivotPressure );
                    observations.Add( PairObservation.PivotMove );
                    break;
                }
            }
        }

        if( found )
        {
            var scr = isPrimary ? (SwitchCandidateResult)tir.PrimaryIntent.IntentResult : (SwitchCandidateResult)tir.SecondaryIntent.IntentResult;
            bool switchChangesWeather = _ai.UnitSim.Switch_ChangesWeather( scr );

            if( switchChangesWeather )
                observations.Add( PairObservation.WeatherChange );

            var ally = scr.Top.Attacker != null ? _ai.GetActiveAllyAs_Adapter( scr.Top.Attacker.Pokemon ) : null;

            if( ally != null && switchChangesWeather )
            {
                var weather = _ai.UnitSim.GetWeatherFrom_Ability( scr.Candidate.Pokemon );
                var allySpeedFromWeather = _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( ally.Pokemon, weather );
                
                if( allySpeedFromWeather )
                    observations.Add( PairObservation.SpeedControl );
            }

            observations.Add( PairObservation.Switch );
            pim = FinishFoundPIM( ref pim, tir, isPrimary, observations );
        }

        return pim;
    }

    private PatternIntentMatch FindWeatherChangeIntent( Dictionary<Pokemon, ThreatIntentResult> threatIntentInteractions )
    {
        PatternIntentMatch pim = new()
        {
            Found = false,
            MatchingTIR = default,
            Evidence = 0,
            RelativeStrength = 0f,
        };

        ThreatIntentResult tir = default;
        bool found = false;
        bool isPrimary = false;

        HashSet<PairObservation> observations = new();

        foreach( var interaction in threatIntentInteractions )
        {
            tir = interaction.Value;

            if( tir.PrimaryIntent.ActionType == ActionType.DefensiveSwitch || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.DefensiveSwitch ) )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.DefensiveSwitch;
                var scr = primary ? (SwitchCandidateResult)tir.PrimaryIntent.IntentResult : (SwitchCandidateResult)tir.SecondaryIntent.IntentResult;
                bool switchChangesWeather = _ai.UnitSim.Switch_ChangesWeather( scr );

                if( switchChangesWeather )
                {
                    isPrimary = tir.PrimaryIntent.ActionType == ActionType.DefensiveSwitch;
                    found = true;

                    observations.Add( PairObservation.Preservation );

                    var top = scr.Top;
                    if( top.Attacker != null && top.AttackerPTKO >= PotentialToKO.Dangerous && ( top.AttackerMovedFirst || top.OpponentPTKO <= PotentialToKO.Risky ) )
                        observations.Add( PairObservation.RevengeKO );

                    var ally = _ai.GetActiveAllyAs_Adapter( top.Attacker.Pokemon );
                    var candidatesWeather = _ai.UnitSim.GetWeatherFrom_Ability( top.Attacker.Pokemon );

                    if( ally != null && _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( ally.Pokemon, candidatesWeather ) )
                        observations.Add( PairObservation.SpeedControl );
                    
                    break;
                }

                break;
            }

            if( tir.PrimaryIntent.ActionType == ActionType.OffensiveSwitch || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.OffensiveSwitch ) )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.OffensiveSwitch;
                var scr = primary ? (SwitchCandidateResult)tir.PrimaryIntent.IntentResult : (SwitchCandidateResult)tir.SecondaryIntent.IntentResult;
                bool switchChangesWeather = _ai.UnitSim.Switch_ChangesWeather( scr );

                if( switchChangesWeather )
                {
                    isPrimary = tir.PrimaryIntent.ActionType == ActionType.OffensiveSwitch;
                    found = true;

                    observations.Add( PairObservation.PivotPressure );

                    var top = scr.Top;
                    if( top.Attacker != null && top.AttackerPTKO >= PotentialToKO.Dangerous && ( top.AttackerMovedFirst || top.OpponentPTKO <= PotentialToKO.Risky ) )
                        observations.Add( PairObservation.RevengeKO );

                    var ally = _ai.GetActiveAllyAs_Adapter( top.Attacker.Pokemon );
                    var candidatesWeather = _ai.UnitSim.GetWeatherFrom_Ability( top.Attacker.Pokemon );
                    
                    if( ally != null && _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( ally.Pokemon, candidatesWeather ) )
                        observations.Add( PairObservation.SpeedControl );
                    
                    break;
                }
            }

            if( tir.PrimaryIntent.ActionType == ActionType.SupportiveStatus || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.SupportiveStatus ) )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.SupportiveStatus;
                var intentResult = primary ? (StatusThreatResult)tir.PrimaryIntent.IntentResult : (StatusThreatResult)tir.SecondaryIntent.IntentResult;
                bool moveChangesWeather = _ai.UnitSim.Move_ChangesWeather( intentResult.Move );

                if( moveChangesWeather )
                {
                    isPrimary = primary;
                    found = true;

                    observations.Add( PairObservation.Setup );
                    observations.Add( PairObservation.StatusMove );

                    var top = intentResult.Top;
                    var ally = _ai.GetActiveAllyAs_Adapter( top.Attacker.Pokemon );
                    var candidatesWeather = _ai.UnitSim.GetWeatherFrom_Ability( top.Attacker.Pokemon );
                    
                    if( ally != null && _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( ally.Pokemon, candidatesWeather ) )
                        observations.Add( PairObservation.SpeedControl );
                }
            }
        }

        if( found )
        {
            observations.Add( PairObservation.WeatherChange );
            pim = FinishFoundPIM( ref pim, tir, isPrimary, observations );
        }

        return pim;
    }

    private PatternIntentMatch FindAfterYouIntent( Dictionary<Pokemon, ThreatIntentResult> threatIntentInteractions )
    {
        PatternIntentMatch pim = new()
        {
            Found = false,
            MatchingTIR = default,
            Evidence = 0,
            RelativeStrength = 0f,
        };

        ThreatIntentResult foundTIR = default;
        bool found = false;
        bool isPrimary = false;

        HashSet<PairObservation> observations = new();

        foreach( var interaction in threatIntentInteractions )
        {
            var tir = interaction.Value;

            if( tir.PrimaryIntent.ActionType == ActionType.SupportiveStatus || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.ActionType == ActionType.SupportiveStatus ) )
            {
                bool primary = tir.PrimaryIntent.ActionType == ActionType.SupportiveStatus;
                bool secondary = tir.SecondaryIntent.ActionType == ActionType.SupportiveStatus;

                StatusThreatResult primarySTR = default;
                StatusThreatResult secondarySTR = default;

                if( primary )
                    primarySTR = (StatusThreatResult)tir.PrimaryIntent.IntentResult;
                
                if( secondary )
                    secondarySTR = (StatusThreatResult)tir.SecondaryIntent.IntentResult;

                if( primary && primarySTR.Move?.MoveSO.Name == "After You" )
                {
                    isPrimary = true;
                    found = true;
                    foundTIR = tir;
                    break;
                }

                if( secondary && secondarySTR.Move?.MoveSO.Name == "After You" )
                {
                    isPrimary = false;
                    found = true;
                    foundTIR = tir;
                    break;
                }
            }
        }

        if( found )
        {
            observations.Add( PairObservation.AfterYou );
            observations.Add( PairObservation.SpeedControl );
            observations.Add( PairObservation.StatusMove );
            pim = FinishFoundPIM( ref pim, foundTIR, isPrimary, observations );
        }

        return pim;
    }

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private int ScoreInitiativeSwing( PairStrategy strategy, List<PairObservationEvidence> poe )
    {
        int score = 0;

        var activeUnits = _ai.Projection.GetAllActiveUnits_InSpeedOrder();
        var ourUnits = _ai.Blackboard.OurActiveBattleAIUnits;
        var theirUnits = _ai.Blackboard.TheirActiveBattleAIUnits;

        foreach( var e in poe )
        {

            //--Trick Room Success
            if( TryGetObservationProfile( e, PairObservation.TrickRoom, out var trickRoomEvidence ) )
            {
                int slowerThan = 0;
                foreach( var ourUnit in ourUnits )
                {
                    var intentResult = trickRoomEvidence.IntentResult;
                    if( ourUnit.Pokemon == intentResult.Top.Attacker.Pokemon )
                        continue;

                    foreach( var theirUnit in theirUnits )
                    {
                        if( ourUnit.Speed < theirUnit.Speed )
                            slowerThan++;
                        else if( theirUnit.Speed < ourUnit.Speed )
                            slowerThan--;
                    }
                }

                if( slowerThan > 0 )
                {
                    score += slowerThan;
                }
                else if( slowerThan < 0 )
                {
                    score -= slowerThan;
                }
            }

            //--Tailwind Success
            if( strategy == PairStrategy.EstablishTailwind )
            {
                int outSpeeds = 0;

                foreach( var theirUnit in ourUnits )
                {
                    foreach( var ourUnit in theirUnits )
                    {
                        if( theirUnit.Speed * 2 > ourUnit.Speed )
                        {
                            outSpeeds++;
                        }
                    }
                }

                if( outSpeeds > 0 )
                {
                    score += Mathf.Max( ( outSpeeds / 2 ), 1 );
                }
            }

            //--Fake Out Success
            if( TryGetObservationProfile( e, PairObservation.FakeOutSupport, out var fakeOutEvidence ) )
            {
                var target = fakeOutEvidence.IntentResult.Top.Opponent;
                var targetSP = _ai.Projection.GetStrategicProfile( target );
                bool losesToFakeOut = !targetSP.BlocksPriority && ( targetSP.BattlefieldSetter || targetSP.BlocksBattlefieldSetup || targetSP.ProvidesStrongOffense || targetSP.ProvidesSupport );

                if( losesToFakeOut )
                {
                    score += 1;
                }
            }

            //--Speed Control Success (Icy Wind, Weather speed ability, etc.)
            if( TryGetObservationProfile( e, PairObservation.SpeedControl, out var speedControlEvidence ) )
            {
                var intentResult = speedControlEvidence.IntentResult;
                if( intentResult.Move.MoveSO.MoveEffects.FieldCondition != FieldConditionID.TrickRoom )
                {
                    var attackerAfter = intentResult.Top.Attacker;
                    var targetAfter = intentResult.Top.Opponent;
                    var attackerBefore = _ai.UnitSim.CopySimUnit( _ai.GetPokemonAs_Adapter( attackerAfter.Pokemon ), null );
                    var targetBefore = _ai.UnitSim.CopySimUnit( _ai.GetPokemonAs_Adapter( targetAfter.Pokemon ), null );

                    bool theyGetFasterThanUs = attackerAfter.Speed > attackerBefore.Speed && attackerAfter.Speed > targetAfter.Speed;
                    bool weGetSlowerThanThem = targetBefore.Speed > targetAfter.Speed && attackerAfter.Speed > targetAfter.Speed;
                    bool theyAreAlreadyFaster = attackerBefore.Speed > targetBefore.Speed;

                    if( !theyAreAlreadyFaster && ( theyGetFasterThanUs || weGetSlowerThanThem ) )
                    {
                        score += 1;
                    }
                    else if( theyAreAlreadyFaster )//--Handling "They already outspeed us negative check here"
                    {
                        score -= 1;

                        if( intentResult.Move.MoveSO.MoveEffects.CourtCondition == CourtConditionID.Tailwind ) //--Tailwind does not provide meaningful improvement to their speed order.
                        {
                            score -= 1;
                        }
                    }
                }
            }

            //--Weather Speed Success
            if( TryGetObservationProfile( e, PairObservation.WeatherChange, out var weatherChangeEvidence ) )
            {
                var intentResult = speedControlEvidence.IntentResult;
                var newWeather = intentResult.Top.Field.Weather;

                var us = intentResult.Top.Opponent;
                var ourAlly = _ai.GetActiveAllyAs_Adapter( us.Pokemon );
                
                var them = intentResult.Top.Attacker;
                var theirAlly = _ai.GetActiveAllyAs_Adapter( them.Pokemon );
                
                bool weHaveNewWeatherSpeedAbility = _ai.UnitSim.PokemonHasAbility_WeatherSpeed( us.Pokemon ) && _ai.UnitSim.PokemonAbilityMatchesWeather( us.Pokemon, newWeather );
                bool ourAllyHasNewWeatherSpeedAbility = ourAlly != null && _ai.UnitSim.PokemonHasAbility_WeatherSpeed( ourAlly.Pokemon );

                bool theyHaveNewWeatherSpeedAbility = _ai.UnitSim.PokemonHasAbility_WeatherSpeed( them.Pokemon );
                bool theirAllyHasNewWeatherSpeedAbility = theirAlly != null && _ai.UnitSim.PokemonHasAbility_WeatherSpeed( theirAlly.Pokemon );

                bool weLikeNewWeatherSpeed = weHaveNewWeatherSpeedAbility || ourAllyHasNewWeatherSpeedAbility;
                bool theyLikeNewWeatherSpeed = theyHaveNewWeatherSpeedAbility ||  theirAllyHasNewWeatherSpeedAbility;

                if( theyLikeNewWeatherSpeed || !weLikeNewWeatherSpeed )
                {
                    score += 1;
                }
                else if( weLikeNewWeatherSpeed && !theyLikeNewWeatherSpeed )
                {
                    score -= 1;
                }
            }
        }

        return score;
    }

    private int ScoreImmediateRemoval( PairStrategy strategy, List<PairObservationEvidence> poe )
    {
        int score = 0;
        int valuableKOs = 0;
        int safeTWOHKOs = 0;
        int removedFullHPResource = 0;
        int invaluableKOs = 0;
        int koBringsRevenger = 0;

        bool theirLeftVS_OurLeft = false;
        bool theirLeftVS_OurRight = false;
        bool theirRightVS_OurLeft = false;
        bool theirRightVS_OurRight = false;

        var ourUnits = _ai.Blackboard.OurActiveBattleAIUnits;
        var theirUnits = _ai.Blackboard.TheirActiveBattleAIUnits;

        var ourLeft = ourUnits[0];
        var ourRight = ourUnits.Count > 1 ? ourUnits[1] : null;

        var theirLeft = theirUnits[0];
        var theirRight = theirUnits.Count > 1 ? theirUnits[1] : null;

        if( ourRight == null )
        {
            theirLeftVS_OurRight = true;
            if( theirRight == null )
            {
                theirRightVS_OurRight = true;
            }
        }

        if( theirRight == null )
        {
            theirRightVS_OurLeft = true;
        }

        foreach( var e in poe )
        {

            if( theirLeftVS_OurLeft && theirLeftVS_OurRight && theirRightVS_OurLeft && theirRightVS_OurRight )
                break;

            if( TryGetObservationProfile( e, PairObservation.Attack, out var attackEvidence ) )
            {
                //--Make match up check. we should only evaluate 4 unique match ups - them vs us, them vs our ally, their ally vs us, their ally vs our ally

                var intentResult = attackEvidence.IntentResult;
                var them = intentResult.Top.Attacker;
                var us = intentResult.Top.Opponent;
                // Debug.LogError( $"Us, Them inside Score Immediate Removal is: {us.Name}, {them.Name}" ); //--just to really confirm the attacker/opponent perspective is correct lol --07/18/26

                //--Left vs Left
                if( them.Pokemon == theirLeft.Pokemon && us.Pokemon == ourLeft.Pokemon && !theirLeftVS_OurLeft )
                {
                    EvaluateInteraction( them, us );
                    theirLeftVS_OurLeft = true;
                }
                else if(  them.Pokemon == theirLeft.Pokemon && us.Pokemon == ourLeft.Pokemon && theirLeftVS_OurLeft )
                    continue;

                //--Left vs Right
                if( them.Pokemon == theirLeft.Pokemon && us.Pokemon == ourRight.Pokemon && !theirLeftVS_OurRight )
                {
                    EvaluateInteraction( them, us );
                    theirLeftVS_OurRight = true;
                }
                else if(  them.Pokemon == theirLeft.Pokemon && us.Pokemon == ourRight.Pokemon && theirLeftVS_OurRight )
                    continue;

                //--Right vs Left
                if( them.Pokemon == theirRight.Pokemon && us.Pokemon == ourLeft.Pokemon && !theirRightVS_OurLeft )
                {
                    EvaluateInteraction( them, us );
                    theirRightVS_OurLeft = true;
                }
                else if(  them.Pokemon == theirRight.Pokemon && us.Pokemon == ourLeft.Pokemon && theirRightVS_OurLeft )
                    continue;

                //--Right vs Right
                if( them.Pokemon == theirRight.Pokemon && us.Pokemon == ourRight.Pokemon && !theirRightVS_OurRight )
                {
                    EvaluateInteraction( them, us );
                    theirRightVS_OurRight = true;
                }
                else if(  them.Pokemon == theirRight.Pokemon && us.Pokemon == ourRight.Pokemon && theirRightVS_OurRight )
                    continue;
            }
        }

        //--Evaluate Immediate Removal of our unit and its importance
        void EvaluateInteraction( IBattleAIUnit them, IBattleAIUnit us )
        {
            var ourSP = _ai.Projection.GetStrategicProfile( us );
            var ourAlly = _ai.GetActiveAllyAs_Adapter( us.Pokemon );
            var allySP = ourAlly != null ? _ai.Projection.GetStrategicProfile( ourAlly ) : default;
            var theirMove = them.MTR.Move;
            var theirMoveTarget = theirMove.MoveSO.MoveTarget;

            bool importantProvider = ourSP.ProvidesStrongOffense || ourSP.ProvidesSupport || ourSP.BattlefieldSetter || ourSP.BattlefieldFlipper;
            bool importantBlocker = ourSP.BlocksBattlefieldSetup || ourSP.BlocksPriority || ourSP.BlocksSpeedControl || ourSP.BlocksWeatherControl;
            bool importantForAlly = ourAlly != null && allySP.DependsOnFastAlly;
            bool importantCapabilities = ( ourSP.WeatherSetter && ourSP.ChangesCurrentWeather ) || ( ourSP.TerrainSetter && ourSP.ChangesCurrentTerrain ) || ourSP.SpeedController || ourSP.Redirector;

            bool weAreImportant = importantProvider || importantBlocker || importantForAlly || importantCapabilities;

            bool weHaveSash = us.Item == ItemBattleEffectID.FocusSash && us.BeginningHPR == 1f;
            bool weHaveSturdy = us.Ability == AbilityID.Sturdy && us.BeginningHPR == 1f;
            bool weHaveMultiscale = us.Ability == AbilityID.Multiscale && us.BeginningHPR == 1f;
            bool weHaveDisguise = us.Ability == AbilityID.Disguise && us.BeginningHPR == 1f;
            bool weHaveFullHPSave = weHaveSash || weHaveSturdy || weHaveMultiscale || weHaveDisguise;

            bool allyHasSash = ourAlly != null && ourAlly.Item == ItemBattleEffectID.FocusSash && ourAlly.BeginningHPR == 1f;
            bool allyHasSturdy = ourAlly != null && ourAlly.Ability == AbilityID.Sturdy && ourAlly.BeginningHPR == 1f;
            bool allyHasMultiscale = ourAlly != null && ourAlly.Ability == AbilityID.Multiscale && ourAlly.BeginningHPR == 1f;
            bool allyHasDisguise = ourAlly != null && ourAlly.Ability == AbilityID.Disguise && ourAlly.BeginningHPR == 1f;
            bool allyHasFullHPSave = allyHasSash || allyHasSturdy || allyHasMultiscale || allyHasDisguise;

            //--End HPR works because these units are passed directly from TOP, and would be no different than doing top.AttackerPTKO or top.Attacker_EndOfTurnHPR
            if( ( us.EndHPR <= 0f || them.MTR.PTKO == PotentialToKO.OHKO ) && weAreImportant )
                valuableKOs++;

            //--Safe TWOHKOs
            if( them.MTR.PTKO >= PotentialToKO.TwoHKO && them.MTR.PTKO <= PotentialToKO.Dangerous && ( ( us.MTR.PTKO <= PotentialToKO.Risky && them.Speed > us.Speed ) || ( us.MTR.PTKO <= PotentialToKO.TwoHKO ) ) && weAreImportant )
                safeTWOHKOs++;

            //--Removes our sash, multiscale, etc.
            if( ( ( weHaveFullHPSave || allyHasFullHPSave ) && ( theirMoveTarget == MoveTarget.AllAdjacent || theirMoveTarget == MoveTarget.OpposingSide ) || ( weHaveFullHPSave && theirMove.MoveSO.Name == "Fake Out" ) ) )
                removedFullHPResource++;

            //--They remove us but we are not strategical important
            if( ( us.EndHPR <= 0f || them.MTR.PTKO == PotentialToKO.OHKO ) && !weAreImportant )
                invaluableKOs++;

            //--They remove us, and we get to bring in a good revenge killer
            if( us.EndHPR <= 0f || them.MTR.PTKO == PotentialToKO.OHKO )
            {
                var revengeCandidate = _ai.GetSwitch_CurrentPressure( ourUnits );
                var revengerComp_Them = revengeCandidate != null ? _ai.Projection.MakeUnitComparison( revengeCandidate, them ) : default;
                var revenger = revengerComp_Them.Attacker;

                if( revengeCandidate != null && revenger.BestCurrentPTKO >= PotentialToKO.Dangerous && ( revenger.FasterSpeed || revengerComp_Them.Target.BestCurrentPTKO <= PotentialToKO.Risky ) )
                    koBringsRevenger++;
            }
        }

        if( valuableKOs > 0 )
            score += Mathf.Clamp( ( valuableKOs / 2 ), 1, 2 );

        if( safeTWOHKOs > 0 )
            score += Mathf.Max( ( safeTWOHKOs / 2 ), 1, 2 );

        if( removedFullHPResource > 0 )
            score += Mathf.Clamp( ( removedFullHPResource / 2 ), 1, 1 );

        if( invaluableKOs > 0 )
            score -= Mathf.Clamp( ( invaluableKOs / 2 ), 1, 1 );

        if( koBringsRevenger > 0 )
            score -= Mathf.Clamp( ( koBringsRevenger / 2 ), 1, 2 );

        return score;
    }

    private int ScoreBoardControl( PairStrategy strategy, List<PairObservationEvidence> poe )
    {
        int score = 0;

        // Question: Can they establish a persistent board state that favors them?
        // Persistent. That's the keyword.

        bool weCanPreventStatusSetup = false;
        bool ourAllyCanPreventStatusSetup = false;

        bool weBenefitFromTrickRoom = false;
        bool theyBenefitFromTrickRoom = false;
        bool weCanPreventTrickRoom = false;
        bool weCanReverseTrickRoom = false;
        bool trickRoomIsAlreadyUp = false;

        bool weCanPreventTailwind = false;
        bool weAlreadyHaveTailwind = false;
        bool theyAlreadyHaveTailwind = false;
        // bool weCanMatchTailwind = false;
        bool theyCanMatchOurTailwind = false;

        bool theyCanForceWeatherChange = false;
        bool theyCanForceTerrainChange = false;

        // bool weCanSetWeather = false;
        bool weCanInvalidateWeather = false;
        bool weBenefitFromWeatherChange = false;

        // bool weCanSetTerrain = false;
        bool weCanRemoveTerrain = false;
        bool weBenefitFromTerrainChange = false;

        bool theyCanSetScreens = false;
        bool theirScreenAlreadyExists = false;
        bool theyHaveRedirection = false;
        bool theyHaveWideGuard = false;
        bool theyHaveQuickGuard = false;
        bool weHaveSpreadMoves = false;
        bool weHavePriority = false;

        void CheckSetupPrevention( IBattleAIUnit us, IBattleAIUnit them )
        {
            var ourAlly = _ai.GetActiveAllyAs_Adapter( us.Pokemon );
            var theirAlly = _ai.GetActiveAllyAs_Adapter( them.Pokemon );

            var ourSP = _ai.Projection.GetStrategicProfile( us );
            var ourAllySP = ourAlly != null ? _ai.Projection.GetStrategicProfile( us ) : default;

            var theirSP = _ai.Projection.GetStrategicProfile( them );
            var theirAllySP = theirAlly != null ? _ai.Projection.GetStrategicProfile( theirAlly ) : default;

            var field = _ai.Blackboard.CurrentFieldSnapshot;
            var ourCourt = us.CourtLocation == CourtLocation.TopCourt ? field.TopCourtConditions : field.BottomCourtConditions;
            var theirCourt = them.CourtLocation == CourtLocation.TopCourt ? field.TopCourtConditions : field.BottomCourtConditions;

            bool weCanFakeOut = _ai.CanUseFakeOut( us, them );
            bool weCanTaunt = us.RoleProfile.Traits.Contains( RoleTrait.Taunt) && them.Item != ItemBattleEffectID.MentalHerb;
            bool weCanEncore = us.RoleProfile.Traits.Contains( RoleTrait.Encore ) && them.Item != ItemBattleEffectID.MentalHerb;
            bool weHavePrankster = us.Ability == AbilityID.Prankster;
            bool weCanImprison = us.Pokemon.CheckHasActiveMove( "Imprison" );
            bool weHavePranksterDisrupt = weHavePrankster && ( weCanTaunt || weCanEncore || weCanImprison );
            weCanPreventStatusSetup = weCanFakeOut || weCanTaunt || weCanEncore || weHavePranksterDisrupt || weCanImprison || ourSP.BlocksBattlefieldSetup;

            bool ourAllyCanFakeOut = ourAlly != null && _ai.CanUseFakeOut( ourAlly, them );
            bool ourAllyCanTaunt = ourAlly != null && ourAlly.RoleProfile.Traits.Contains( RoleTrait.Taunt ) && them.Item != ItemBattleEffectID.MentalHerb;
            bool ourAllyCanEncore = ourAlly != null && ourAlly.RoleProfile.Traits.Contains( RoleTrait.Encore ) && them.Item != ItemBattleEffectID.MentalHerb;
            bool ourAllyHasPrankster = ourAlly != null && ourAlly.Ability == AbilityID.Prankster;
            bool ourAllyCanImprison = ourAlly != null && ourAlly.Pokemon.CheckHasActiveMove( "Trick Room" );
            bool ourAllyHasPranksterDisrupt = ourAllyHasPrankster && ( ourAllyCanTaunt || ourAllyCanEncore || ourAllyCanImprison );
            ourAllyCanPreventStatusSetup = ourAllyCanFakeOut || ourAllyCanTaunt || ourAllyCanEncore || ourAllyHasPranksterDisrupt || ourAllyCanImprison || ourAllySP.BlocksBattlefieldSetup;

            trickRoomIsAlreadyUp = field.FieldConditions.ContainsKey( FieldConditionID.TrickRoom );
            weAlreadyHaveTailwind = ourCourt.ContainsKey( CourtConditionID.Tailwind );
            theyAlreadyHaveTailwind = theirCourt.ContainsKey( CourtConditionID.Tailwind );

            if( theirSP.DependsOnTrickRoom || ( theirAlly != null && theirAllySP.DependsOnTrickRoom ) )
                theyBenefitFromTrickRoom = true;

            if( ourSP.DependsOnTrickRoom || ( ourAlly != null && ourAllySP.DependsOnTrickRoom ) )
                weBenefitFromTrickRoom = true;

            if( weCanPreventStatusSetup || ourAllyCanPreventStatusSetup )
            {
                weCanPreventTrickRoom = true;
                weCanPreventTailwind = true;
            }

            if( us.Pokemon.CheckHasActiveMove( "Trick Room" ) || ourAlly != null && ourAlly.Pokemon.CheckHasActiveMove( "Trick Room" ) )
                weCanReverseTrickRoom = true;

            // if( !weAlreadyHaveTailwind && ( us.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter ) || ( ourAlly != null && ourAlly.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter ) ) ) )
                // weCanMatchTailwind = true;

            if( !theyAlreadyHaveTailwind && weAlreadyHaveTailwind && them.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter ) || ( theirAlly != null && theirAlly.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter ) ) )
                theyCanMatchOurTailwind = true;

            if( ( _ai.UnitSim.PokemonHasWeatherSetter_Ability( them.Pokemon ) && theirSP.ChangesCurrentWeather ) || ( theirAlly != null && _ai.UnitSim.PokemonHasWeatherSetter_Ability( theirAlly.Pokemon ) && theirAllySP.ChangesCurrentWeather ) )
                theyCanForceWeatherChange = true;

            if( ( _ai.UnitSim.PokemonHasTerrainSetter_Ability( them.Pokemon ) && theirSP.ChangesCurrentTerrain ) || ( theirAlly != null && _ai.UnitSim.PokemonHasTerrainSetter_Ability( theirAlly.Pokemon ) && theirAllySP.ChangesCurrentTerrain ) )
                theyCanForceTerrainChange = true;

            // if( ourSP.WeatherSetter || ourAlly != null && ourAllySP.WeatherSetter )
                // weCanSetWeather = true;

            if( us.Ability == AbilityID.CloudNine || ourAlly != null && ourAlly.Ability == AbilityID.CloudNine )
                weCanInvalidateWeather = true;

            // if( ourSP.TerrainSetter || ourAlly != null && ourAllySP.TerrainSetter )
                // weCanSetTerrain = true;

            // terrain removal check here. i don't believe i have things like ice spinner accounted for yet. will have to look into it in the future. --07/19/26

            bool screenSetter = ( them.RoleProfile.Traits.Contains( RoleTrait.ScreenSetter ) && theirSP.BattlefieldSetter ) || ( theirAlly != null && theirAlly.RoleProfile.Traits.Contains( RoleTrait.ScreenSetter ) && theirAllySP.BattlefieldSetter );
            if( screenSetter )
            {
                bool reflect = !theirCourt.ContainsKey( CourtConditionID.Reflect ) && ( them.Pokemon.CheckHasActiveMove( "Reflect" ) || theirAlly != null && theirAlly.Pokemon.CheckHasActiveMove( "Reflect" ) );
                bool lightScreen = !theirCourt.ContainsKey( CourtConditionID.LightScreen ) && ( them.Pokemon.CheckHasActiveMove( "Light Screen" ) || theirAlly != null && theirAlly.Pokemon.CheckHasActiveMove( "Light Screen" ) );
                bool auroraVeil = !theirCourt.ContainsKey( CourtConditionID.AuroraVeil ) && ( them.Pokemon.CheckHasActiveMove( "Aurora Veil" ) || theirAlly != null && theirAlly.Pokemon.CheckHasActiveMove( "Aurora Veil" ) );

                theyCanSetScreens = reflect || lightScreen || auroraVeil;
                CourtConditionID cc = them.MTR != null && them.MTR.Move != null ? them.MTR.Move.MoveSO.MoveEffects.CourtCondition : CourtConditionID.None;
                CourtConditionID theirScreen = ( cc == CourtConditionID.Reflect || cc == CourtConditionID.LightScreen || cc == CourtConditionID.AuroraVeil ) ? theirScreen = cc : CourtConditionID.None;
                theirScreenAlreadyExists = theirCourt.ContainsKey( theirScreen );
            }

            if( theirSP.Redirector || theirAlly != null && theirAllySP.Redirector )
                theyHaveRedirection = true;

            if( them.Pokemon.CheckHasActiveMove( "Wide Guard" ) || theirAlly != null && theirAlly.Pokemon.CheckHasActiveMove( "Wide Guard" ) )
                theyHaveWideGuard = true;

            if( them.Pokemon.CheckHasActiveMove( "Quick Guard" ) || theirAlly != null && theirAlly.Pokemon.CheckHasActiveMove( "Quick Guard" ) )
                theyHaveQuickGuard = true;

            if( ourSP.SpreadPressure || ourAlly != null && ourAllySP.SpreadPressure )
                weHaveSpreadMoves = true;

            if( ourSP.PriorityAttacks || ourSP.PriorityStatus || ourAlly != null && ( ourAllySP.PriorityAttacks || ourAllySP.PriorityStatus ) )
                weHavePriority = true;
        }

        PairObservationProfile trickRoom = default;
        PairObservationProfile tailwind = default;
        PairObservationProfile weatherChange = default;
        PairObservationProfile terrainChange = default;
        foreach( var e in poe )
        {
            if( TryGetObservationProfile( e, PairObservation.TrickRoom, out trickRoom ) )
            {
                var intentResult = trickRoom.IntentResult;
                var top = intentResult.Top;

                var us = top.Opponent;
                var them = top.Attacker;

                CheckSetupPrevention( us, them );
            }

            if( TryGetObservationProfile( e, PairObservation.Tailwind, out tailwind ) )
            {
                var intentResult = tailwind.IntentResult;
                var top = intentResult.Top;

                var us = top.Opponent;
                var them = top.Attacker;

                CheckSetupPrevention( us, them );
            }

            if( TryGetObservationProfile( e, PairObservation.WeatherChange, out weatherChange ) )
            {
                var intentResult = weatherChange.IntentResult;
                var top = intentResult.Top;

                var us = top.Opponent;
                var them = top.Attacker;

                CheckSetupPrevention( us, them );

                int ourWeatherContext = 0;
                int theirWeatherContext = 0;

                var ourUnits = _ai.Blackboard.OurActiveBattleAIUnits;
                var theirUnits = _ai.Blackboard.TheirActiveBattleAIUnits;

                foreach( var unit in ourUnits )
                {
                    ourWeatherContext += _ai.UnitSim.Get_WeatherContextScore( unit.Pokemon, top.Field.Weather );
                }

                ourWeatherContext /= ourUnits.Count;

                foreach( var unit in theirUnits )
                {
                    theirWeatherContext += _ai.UnitSim.Get_WeatherContextScore( unit.Pokemon, top.Field.Weather );
                }

                theirWeatherContext /= theirUnits.Count;

                weBenefitFromWeatherChange = ourWeatherContext - theirWeatherContext > 0;
            }

            if( TryGetObservationProfile( e, PairObservation.TerrainChange, out terrainChange ) )
            {
                var intentResult = terrainChange.IntentResult;
                var top = intentResult.Top;

                var us = top.Opponent;
                var them = top.Attacker;

                CheckSetupPrevention( us, them );

                int ourTerrainContext = 0;
                int theirTerrainContext = 0;

                var ourUnits = _ai.Blackboard.OurActiveBattleAIUnits;
                var theirUnits = _ai.Blackboard.TheirActiveBattleAIUnits;

                foreach( var unit in ourUnits )
                {
                    ourTerrainContext += _ai.UnitSim.Get_TerrainContextScore( unit.Pokemon, top.Field.Terrain );
                }

                ourTerrainContext /= ourUnits.Count;

                foreach( var unit in theirUnits )
                {
                    theirTerrainContext += _ai.UnitSim.Get_TerrainContextScore( unit.Pokemon, top.Field.Terrain );
                }

                theirTerrainContext /= theirUnits.Count;

                weBenefitFromTerrainChange = ourTerrainContext - theirTerrainContext > 0;
            }
        }

        //--Positive
        //--Trick Room
        if( theyBenefitFromTrickRoom && ( !weCanPreventTrickRoom || !weCanReverseTrickRoom ) )
        {
            score += 1;

            if( !weBenefitFromTrickRoom )
            {
                score += 1;
            }
        }

        //--Tailwind
        if( !weCanPreventTailwind && !theyAlreadyHaveTailwind && !weAlreadyHaveTailwind && theyCanMatchOurTailwind )
            score += 2;

        //--Weather
        if( theyCanForceWeatherChange && !weCanInvalidateWeather && !weBenefitFromWeatherChange )
            score += 2;

        //--Terrain
        if( theyCanForceTerrainChange && !weCanRemoveTerrain && !weBenefitFromTerrainChange )
            score += 2;

        //--Screens
        if( !weCanPreventStatusSetup && !ourAllyCanPreventStatusSetup && theyCanSetScreens )
            score += 1;

        //--Redirection
        if( theyHaveRedirection && !weHaveSpreadMoves )
            score += 1;

        //--Wide Guard
        if( theyHaveWideGuard && weHaveSpreadMoves )
            score += 1;

        //--Quick Guard
        if( theyHaveQuickGuard && !weHavePriority )
            score += 1;

        //--Negative
        //--Board effect already exists
        if( trickRoom.Them != null && trickRoomIsAlreadyUp )
            score -= 1;

        if( tailwind.Them != null && theyAlreadyHaveTailwind && !weAlreadyHaveTailwind )
            score -= 1;

        if( theirScreenAlreadyExists )
            score -= 1;

        //--Board effect benefits opponent
        if( weBenefitFromTrickRoom )
        {
            score -= 1;

            if( !theyBenefitFromTrickRoom )
                score -= 1;
        }

        if( weBenefitFromWeatherChange )
            score -= 2;

        if( weBenefitFromTerrainChange )
            score -= 2;

        return score;
    }

    private int ScoreDenial( PairStrategy strategy, List<PairObservationEvidence> poe )
    {
        int score = 0;

        //--The opponent's ability to deny the ai's current possiblities

        int planStoppingFakeOuts = 0;
        int planStoppingDisruption = 0;
        int planStoppingStatus = 0;
        bool weUsePlanBeforeTheyCanStopUs = false;
        foreach( var e in poe )
        {
            if( e.Type == PairObservation.FakeOutSupport && e is var fakeOutSupport )
            {
                var intentResult = fakeOutSupport.IsPrimary ? fakeOutSupport.SourceIntent.PrimaryIntent.IntentResult : fakeOutSupport.SourceIntent.SecondaryIntent.IntentResult;
                var top = intentResult.Top;

                var them = top.Attacker;
                var us = top.Opponent;

                var ourSP = _ai.Projection.GetStrategicProfile( us );
                var ourTraits = us.RoleProfile.Traits;

                bool weHaveOffensiveSetup = ourTraits.Contains( RoleTrait.PhysicallyOffensiveSetup ) || ourTraits.Contains( RoleTrait.SpeciallyOffensiveSetup );
                bool weHaveDefensiveSetup = ourTraits.Contains( RoleTrait.PhysicallyDefensiveSetup ) || ourTraits.Contains( RoleTrait.SpeciallyDefensiveSetup );
                int ourBoosts = 0;

                foreach( var sc in us.StatStages )
                {
                    if( sc.Value > 0 )
                        ourBoosts += sc.Value;
                }

                bool weHaveSetupPotential = ( weHaveOffensiveSetup || weHaveDefensiveSetup ) && ourBoosts <= 0;

                if( ourSP.BattlefieldFlipper )
                    planStoppingFakeOuts++;

                if( ourSP.BattlefieldSetter )
                    planStoppingFakeOuts++;
                
                if( ourSP.SpreadPressure )
                    planStoppingFakeOuts++;

                if( ourSP.CriticalSupport )
                    planStoppingFakeOuts++;

                if( weHaveSetupPotential )
                    planStoppingFakeOuts++;
            }

            if( e.Type == PairObservation.StatusDisruption && e is var statusDisruption )
            {
                var intentResult = statusDisruption.IsPrimary ? statusDisruption.SourceIntent.PrimaryIntent.IntentResult : statusDisruption.SourceIntent.SecondaryIntent.IntentResult;
                var top = intentResult.Top;
                var moveSO = intentResult.Move.MoveSO;

                var them = top.Attacker;
                var us = top.Opponent;
                var ourBiases = us.RoleProfile.Biases;
                var ourTraits = us.RoleProfile.Traits;

                var ourSP = _ai.Projection.GetStrategicProfile( us );

                bool isTaunt = intentResult.Move.MoveSO.MoveEffects.VolatileStatus == VolatileConditionID.Taunt && !ourTraits.Contains( RoleTrait.TauntImmune );
                bool isEncore = intentResult.Move.MoveSO.MoveEffects.VolatileStatus == VolatileConditionID.Encore;
                bool isWisp = intentResult.Move.MoveSO.MoveEffects.SevereStatus == SevereConditionID.BRN && !ourTraits.Contains( RoleTrait.BurnImmune );
                bool isFrost = intentResult.Move.MoveSO.MoveEffects.SevereStatus == SevereConditionID.FBT && !ourTraits.Contains( RoleTrait.FrostImmune );
                bool isParalysis = intentResult.Move.MoveSO.MoveEffects.SevereStatus == SevereConditionID.PAR && ( ( moveSO.Flags.Contains( MoveFlags.Powder ) && !ourTraits.Contains( RoleTrait.PowderImmune ) ) || ( moveSO.Name == "Thunder Wave" && !ourTraits.Contains( RoleTrait.ThunderWaveImmune ) ) );
                bool isSleep = intentResult.Move.MoveSO.MoveEffects.SevereStatus == SevereConditionID.SLP && !ourTraits.Contains( RoleTrait.SleepImmune );
                bool hasPrankster = them.Ability == AbilityID.Prankster;
                bool theyAreFaster = them.Speed > us.Speed;

                if( !hasPrankster && !theyAreFaster && moveSO.MoveEffects.FieldCondition != FieldConditionID.TrickRoom )
                    weUsePlanBeforeTheyCanStopUs = true;

                if( ourSP.BattlefieldFlipper && isTaunt && ( hasPrankster || theyAreFaster ) && !ourSP.ChangesCurrentWeather && !ourSP.ChangesCurrentTerrain )
                    planStoppingDisruption++;

                if( ourSP.BattlefieldSetter && isTaunt && ( hasPrankster || theyAreFaster || ourTraits.Contains( RoleTrait.TrickRoomSetter ) ) )
                    planStoppingDisruption++;

                if( ( ourSP.CriticalSupport || ourSP.ProvidesSupport ) && isTaunt && ( hasPrankster || theyAreFaster ) )
                    planStoppingDisruption++;

                if( ourSP.ProvidesStrongOffense )
                {
                    if( isWisp && us.RoleProfile.Biases.Contains( RoleBias.Physical ) || isFrost && us.RoleProfile.Biases.Contains( RoleBias.Special ) )
                    {
                        planStoppingStatus++;

                        if( hasPrankster || theyAreFaster )
                        {
                            planStoppingStatus++;
                        }
                    }

                    if( isParalysis || isSleep )
                    {
                        planStoppingStatus++;

                        if( hasPrankster || theyAreFaster )
                        {
                            planStoppingStatus++;

                            if( isSleep )
                            {
                                planStoppingStatus++;
                            }
                        }

                        if( isParalysis && ourTraits.Contains( RoleTrait.ParalysisWeak ) )
                        {
                            planStoppingStatus++;
                        }
                    }
                }

                if( _ai.UnitSim.MoveIsSetup( _ai.GetBattleUnit( us.Pokemon ).LastUsedMove ) && isEncore && ( hasPrankster || theyAreFaster ) )
                {
                    planStoppingStatus++;
                }
            }
        }

        if( planStoppingFakeOuts > 0 )
            score += Mathf.Max( planStoppingFakeOuts, 2 );

        if( planStoppingDisruption > 0 )
            score += Mathf.Max( planStoppingDisruption, 2 );

        if( planStoppingStatus > 0 )
            score += Mathf.Max( planStoppingStatus, 2 );

        if( weUsePlanBeforeTheyCanStopUs )
            score -= 3;

        return score;
    }

    private int ScoreGuaranteedValue( PairStrategy strategy, List<PairObservationEvidence> poe )
    {
        int score = 0;

        int reliableHighValueOptions = 0;
        int reliableLowValueOptions = 0;
        int contingencyRequirements = 0;
        foreach( var e in poe )
        {
            bool isFakeOut = false;
            if( e.Type == PairObservation.FakeOutSupport )
            {
                reliableHighValueOptions++;
                isFakeOut = true;
            }
            
            if( e.Type == PairObservation.Attack && e is var attackEvidence )
            {
                var intentResult = attackEvidence.IsPrimary ? attackEvidence.SourceIntent.PrimaryIntent.IntentResult : attackEvidence.SourceIntent.SecondaryIntent.IntentResult;
                var top = intentResult.Top;
                var them = top.Attacker;
                var us = top.Opponent;
                var ourAlly = _ai.GetActiveAllyAs_Adapter( us.Pokemon );
                bool weHaveAlly = ourAlly != null;
                var move = intentResult.Move;

                bool moveIsSpread = move.MoveSO.MoveTarget == MoveTarget.AllAdjacent || move.MoveSO.MoveTarget == MoveTarget.OpposingSide;

                bool weHaveSash = us.Item == ItemBattleEffectID.FocusSash && us.BeginningHPR == 1f;
                bool weHaveSturdy = us.Ability == AbilityID.Sturdy && us.BeginningHPR == 1f;
                bool weHaveMultiscale = us.Ability == AbilityID.Multiscale && us.BeginningHPR == 1f;
                bool weHaveDisguise = us.Ability == AbilityID.Disguise && us.BeginningHPR == 1f;
                bool weHaveFullHPSave = weHaveSash || weHaveSturdy || weHaveMultiscale || weHaveDisguise;

                bool theyAreUtility = them.RoleProfile.PrimaryRole == RoleClass.UtilitySupport || them.RoleProfile.PrimaryRole == RoleClass.Disrupter || them.RoleProfile.PrimaryRole == RoleClass.FieldControl;
                var ourSP = _ai.Projection.GetStrategicProfile( us );
                var ourAllySP = weHaveAlly ? _ai.Projection.GetStrategicProfile( ourAlly ) : default;
                

                //--Spread Move
                if( moveIsSpread && !ourSP.SpreadProtector && ( !weHaveAlly || weHaveAlly && !ourAllySP.SpreadProtector ) )
                    reliableHighValueOptions++;
                else if( moveIsSpread && ourSP.SpreadProtector && ( !weHaveAlly || weHaveAlly && ourAllySP.SpreadProtector ) )
                    contingencyRequirements++;

                //--Guaranteed KO
                if( top.AttackerPTKO == PotentialToKO.OHKO )
                    reliableHighValueOptions++;

                //--HP Resource Steal
                if( weHaveFullHPSave && ( isFakeOut || moveIsSpread ) )
                    reliableHighValueOptions++;

                //--Chip
                if( top.AttackerPTKO >= PotentialToKO.TwoHKO && ( moveIsSpread || theyAreUtility ) )
                    reliableLowValueOptions++;
                else if( top.AttackerPTKO >= PotentialToKO.TwoHKO && top.OpponentPTKO >= PotentialToKO.Risky )
                    contingencyRequirements++;
            }

            if( e.Type == PairObservation.StatusMove && e is var statusMoveEvidence )
            {
                var intentResult = statusMoveEvidence.IsPrimary ? statusMoveEvidence.SourceIntent.PrimaryIntent.IntentResult : statusMoveEvidence.SourceIntent.SecondaryIntent.IntentResult;
                var top = intentResult.Top;
                var them = top.Attacker;
                var us = top.Opponent;
                var ourSP = _ai.Projection.GetStrategicProfile( us );
                var theyAreFaster = them.Ability == AbilityID.Prankster || them.Speed > us.Speed;

                if( ourSP.ProvidesStrongOffense || ourSP.BlocksBattlefieldSetup )
                {
                    if( theyAreFaster )
                        reliableLowValueOptions++;
                    else
                        contingencyRequirements++;
                }
            }

            if( e.Type == PairObservation.StatusDisruption && e is var statusDisruptionEvidence )
            {
                var intentResult = statusDisruptionEvidence.IsPrimary ? statusDisruptionEvidence.SourceIntent.PrimaryIntent.IntentResult : statusDisruptionEvidence.SourceIntent.SecondaryIntent.IntentResult;
                var top = intentResult.Top;
                var them = top.Attacker;
                var us = top.Opponent;
                var ourSP = _ai.Projection.GetStrategicProfile( us );
                var theyAreFaster = them.Ability == AbilityID.Prankster || them.Speed > us.Speed;

                if( ourSP.ProvidesSupport || ourSP.CriticalSupport || ourSP.BattlefieldSetter || ourSP.BattlefieldFlipper )
                {
                    if( theyAreFaster )
                        reliableLowValueOptions++;
                    else
                        contingencyRequirements++;
                }
            }

            if( e.Type == PairObservation.Switch && e is var switchEvidence )
            {
                var intentResult = switchEvidence.IsPrimary ? switchEvidence.SourceIntent.PrimaryIntent.IntentResult : switchEvidence.SourceIntent.SecondaryIntent.IntentResult;
                var top = intentResult.Top;
                var them = top.Attacker;
                var us = top.Opponent;

                var themComp_Us = _ai.Projection.MakeUnitComparison( them, us );
                var themComp = themComp_Us.Attacker;
                var usComp = themComp_Us.Target;

                if( themComp.BestCurrentPTKO >= PotentialToKO.Risky && ( usComp.BestCurrentPTKO <= PotentialToKO.Risky && !usComp.FasterSpeed ) || ( usComp.BestCurrentPTKO <= PotentialToKO.TwoHKO ) )
                    reliableLowValueOptions++;
                else
                    contingencyRequirements++;
            }

            if( e.Type == PairObservation.Setup )
                contingencyRequirements++;

            if( e.Type == PairObservation.FocusFire )
                contingencyRequirements++;
        }

        if( reliableHighValueOptions > 0 )
            score += Mathf.Max( reliableHighValueOptions, 6 );

        if( reliableLowValueOptions > 0 )
            score += Mathf.Max( reliableLowValueOptions, 3 );

        if( contingencyRequirements > 0 )
            score -= Mathf.Max( contingencyRequirements, 6 );

        return score;
    }

    private int ScoreMomentum( PairStrategy strategy, List<PairObservationEvidence> poe )
    {
        int score = 0;

        int positionGain = 0;
        int switchIsCovered = 0;
        int switchIsSafe = 0;
        int allyIsSafe = 0;
        int damageRisk = 0;
        int positionRisk = 0;
        foreach( var e in poe )
        {
            if( e.SourcePattern == PairPattern.CoveredSwitch || e.SourcePattern == PairPattern.ProtectAndSwitch || e.SourcePattern == PairPattern.DoubleSwitch || e.SourcePattern == PairPattern.WeatherChange )
            {
                if( e.Type == PairObservation.Attack && e is var attackEvidence )
                {
                    var intentResult = attackEvidence.IsPrimary ? attackEvidence.SourceIntent.PrimaryIntent.IntentResult : attackEvidence.SourceIntent.SecondaryIntent.IntentResult;
                    var top = intentResult.Top;
                    var them = top.Attacker;
                    var move = intentResult.Move;

                    //--Pseudo volt switch/u-turn check
                    if( move.MoveEffects.SwitchType == SwitchEffectType.SelfPivot )
                    {
                        positionGain++;

                        if( them.RoleProfile.Traits.Contains( RoleTrait.SlowPivot ) || ( top.Field.FieldConditions.ContainsKey( FieldConditionID.TrickRoom ) && them.RoleProfile.Traits.Contains( RoleTrait.FastPivot ) ) )
                        {
                            positionGain++;
                        }
                    }

                    if( them.BeginningHPR <= 0.1f && them.EndHPR <= 0f && ( !top.AttackerMovedFirst || !top.AttackerCanAct || top.Attacker_DiesBeforeActing ) )
                    {
                        positionGain++;
                    }
                }

                //--Pseudo parting shot check
                if( e.Type == PairObservation.StatDebuff && e is var debuffEvidence )
                {
                    var intentResult = debuffEvidence.IsPrimary ? debuffEvidence.SourceIntent.PrimaryIntent.IntentResult : debuffEvidence.SourceIntent.SecondaryIntent.IntentResult;
                    var top = intentResult.Top;
                    var them = top.Attacker;
                    var move = intentResult.Move;

                    if( move.MoveEffects.SwitchType == SwitchEffectType.SelfPivot )
                    {
                        positionGain++;

                        if( them.RoleProfile.Traits.Contains( RoleTrait.SlowPivot ) || ( top.Field.FieldConditions.ContainsKey( FieldConditionID.TrickRoom ) && them.RoleProfile.Traits.Contains( RoleTrait.FastPivot ) ) )
                        {
                            positionGain++;
                        }
                    }
                }

                if( e.Type == PairObservation.FakeOutSupport )
                    switchIsCovered++;

                if( e.Type == PairObservation.RedirectionSupport )
                    switchIsCovered++;

                if( e.Type == PairObservation.Protect )
                    allyIsSafe++;

                if( e.Type == PairObservation.Switch && e is var switchEvidence )
                {
                    var intentResult = switchEvidence.IsPrimary ? switchEvidence.SourceIntent.PrimaryIntent.IntentResult : switchEvidence.SourceIntent.SecondaryIntent.IntentResult;
                    var top = intentResult.Top;
                    var us = top.Opponent;
                    var ourAlly = _ai.GetActiveAllyAs_Adapter( us.Pokemon );
                    var them = top.Attacker;
                    var theyAreFAster = them.Speed > us.Speed;

                    var themComp_Us = _ai.Projection.MakeUnitComparison( them, us );
                    var themComp = themComp_Us.Attacker;
                    var usComp = themComp_Us.Target;

                    var ourBiases = us.RoleProfile.Biases;
                    var ourAllyBiases = ourAlly?.RoleProfile.Biases;
                    var theirSP = _ai.Projection.GetStrategicProfile( them );
                    bool theyTakeAcceptableDamage = them.BeginningHPR - them.EndHPR < 0.45f;

                    bool weArePhysical = ourBiases.Contains( RoleBias.Physical );
                    bool weAreSpecial = ourBiases.Contains( RoleBias.Special );
                    bool ourAllyIsPhysical = ourAlly != null && ourAllyBiases.Contains( RoleBias.Physical );
                    bool ourAllyIsSpecial = ourAlly != null && ourAllyBiases.Contains( RoleBias.Special );

                    if( theyTakeAcceptableDamage && ( ( top.OpponentPTKO <= PotentialToKO.Risky && theyAreFAster ) || ( top.OpponentPTKO <= PotentialToKO.TwoHKO && top.Attacker.Item != ItemBattleEffectID.FocusSash ) ) )
                    {
                        switchIsSafe++;
                    }
                    else
                        positionRisk++;

                    if( top.OpponentPTKO <= PotentialToKO.Risky )
                    {
                        if( themComp.BestCurrentPTKO >= PotentialToKO.Dangerous || ( themComp.BestCurrentPTKO >= PotentialToKO.Risky && themComp.FasterSpeed ) )
                            positionGain++;
                    }
                    else
                        positionRisk++;

                    if( ( them.Ability == AbilityID.Intimidate && ( weArePhysical || ourAllyIsPhysical ) ) || ( them.Ability == AbilityID.Demoralize && ( weAreSpecial || ourAllyIsSpecial ) ) )
                    {
                        positionGain++;
                    }

                    if( theyTakeAcceptableDamage && ( e.SourcePattern == PairPattern.WeatherChange || e.SourcePattern == PairPattern.TerrainChange ) )
                    {
                        if( theirSP.ChangesCurrentWeather || theirSP.ChangesCurrentTerrain || theirSP.BattlefieldFlipper )
                        {
                            positionGain++;
                        }
                        else
                            positionRisk++;
                    }

                    if( !theyTakeAcceptableDamage )
                        damageRisk++;
                }
            }
        }

        if( positionGain > 0 )
            score += Mathf.Max( positionGain, 4 );

        if( switchIsCovered > 0 )
            score += Mathf.Max( switchIsCovered, 2 );

        if( switchIsSafe > 0 )
            score += Mathf.Max( switchIsSafe, 2 );

        if( allyIsSafe > 0 )
            score += Mathf.Max( allyIsSafe, 2 );

        if( damageRisk > 0 )
            score -= Mathf.Max( damageRisk, 2 );

        if( positionRisk > 0 )
            score -= Mathf.Max( positionRisk, 4 );


        return score;
    }

    private int ScorePressure( PairStrategy strategy, List<PairObservationEvidence> poe )
    {
        int score = 0;

        int doubleForcesProtect = 0;
        int doubleHasKOPressure = 0;

        int tailwindPressure = 0;
        int tailwindKOPressure = 0;

        int trickroomPressure = 0;
        int trickroomKOPressure = 0;

        int pinPressure = 0;
        int spreadPressure = 0;

        int weRelievePressure = 0;
        int theirPressureFails = 0;

        foreach( var e in poe )
        {
            //--Double Attack/Focus Fire pressure
            if( e.SourcePattern == PairPattern.FocusFire || e.SourcePattern == PairPattern.DoubleAttack )
            {
                if( TryGetObservationProfile( e, PairObservation.Attack, out var attack ) )
                {
                    UnitComparison allyComp_Them = default;

                    if( attack.WeHaveAlly )
                    {
                        allyComp_Them = _ai.Projection.MakeUnitComparison( attack.OurAlly, attack.Them );
                    }

                    if( _ai.CanUseProtect( attack.Us.Pokemon ) )
                    {
                        doubleForcesProtect++;
                    }
                    else if( attack.Top.AttackerPTKO >= PotentialToKO.Dangerous || ( attack.WeHaveAlly && allyComp_Them.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous ) )
                    {
                        doubleHasKOPressure++;
                    }
                    else
                    {
                        theirPressureFails++;
                    }
                }

                if( TryGetObservationProfile( e, PairObservation.FakeOutSupport, out var fakeOut ) )
                {
                    UnitComparison allyComp_Them = default;

                    if( fakeOut.WeHaveAlly )
                    {
                        allyComp_Them = _ai.Projection.MakeUnitComparison( fakeOut.OurAlly, fakeOut.Them );
                    }

                    if( _ai.CanUseProtect( fakeOut.Us.Pokemon ) )
                    {
                        doubleForcesProtect++;
                    }
                    else if( fakeOut.WeHaveAlly && allyComp_Them.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous )
                    {
                        doubleHasKOPressure++;
                    }
                    else
                    {
                        theirPressureFails++;
                    }
                }
            }

            //--Tailwind Pressure
            if( TryGetObservationProfile( e, PairObservation.Tailwind, out var tailwind ) )
            {
                //--Checking purely for speed advantage + some big damage/spread damage pressure
                const int tailwind_modifier = 2; //--Tailwind is a 2x direct modifier to a unit's speed stat
                int currentOutSpeeds = 0;
                int afterOutSpeeds = 0;

                var ourUnits = _ai.Blackboard.OurActiveBattleAIUnits;
                var theirUnits = _ai.Blackboard.TheirActiveBattleAIUnits;

                foreach( var ourUnit in ourUnits )
                {
                    foreach( var theirUnit in theirUnits )
                    {
                        if( theirUnit.Speed > ourUnit.Speed )
                        {
                            currentOutSpeeds++;
                        }

                        if( theirUnit.Speed * tailwind_modifier > ourUnit.Speed )
                        {
                            afterOutSpeeds++;
                        }
                    }
                }

                if( afterOutSpeeds > currentOutSpeeds || ( afterOutSpeeds >= currentOutSpeeds && currentOutSpeeds >= 1 ) )
                {
                    tailwindPressure++;

                    if( tailwind.Them.RoleProfile.Traits.Contains( RoleTrait.SpreadAttack ) )
                        tailwindPressure++;
                }
                else
                {
                    theirPressureFails++;
                }

                if( afterOutSpeeds / theirUnits.Count > 0 )
                {
                    if( tailwind.Top.AttackerPTKO >= PotentialToKO.Dangerous )
                    {
                        tailwindKOPressure++;

                        if( tailwind.WeHaveAlly )
                        {
                            var themComp_OurAlly = _ai.Projection.MakeUnitComparison( tailwind.Them, tailwind.Us );

                            //--we use their ally from TOP here because tailwind is a SupportiveStatus, which is currently the only simulation that implements allies within the simulation
                            //--and ally assignment within PairObservationProfile comes from the current ally look up, not from TOP.
                            if( tailwind.Top.AttackerAlly?.Speed > tailwind.OurAlly.Speed && themComp_OurAlly.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous )
                            {
                                tailwindKOPressure++;
                            }
                        }
                    }
                    else
                    {
                        theirPressureFails++;
                    }

                    if( tailwind.TheyHaveAlly )
                    {
                        var theirAllyComp_Us = _ai.Projection.MakeUnitComparison( tailwind.TheirAlly, tailwind.Us );
                        
                        //--we use their ally from TOP here because tailwind is a SupportiveStatus, which is currently the only simulation that implements allies within the simulation
                        //--and ally assignment within PairObservationProfile comes from the current ally look up, not from TOP.
                        if( tailwind.Top.AttackerAlly?.Speed > tailwind.Us.Speed && theirAllyComp_Us.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous )
                        {
                            tailwindKOPressure++;
                        }

                        if( tailwind.WeHaveAlly )
                        {
                            var theirAllyComp_OurAlly = _ai.Projection.MakeUnitComparison( tailwind.TheirAlly, tailwind.Us );

                            if( tailwind.Top.AttackerAlly?.Speed > tailwind.OurAlly.Speed && theirAllyComp_OurAlly.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous )
                            {
                                tailwindKOPressure++;
                            }
                        }

                        if( tailwind.TheirAlly.RoleProfile.Traits.Contains( RoleTrait.SpreadAttack ) )
                        {
                            tailwindPressure++;
                        }
                    }
                    else
                    {
                        theirPressureFails++;
                    }
                }
            }

            //--Trick Room Pressure
            if( TryGetObservationProfile( e, PairObservation.TrickRoom, out var trickroom ) )
            {
                //--Checking purely for in-trick room speed advantage + some big damage/spread damage pressure
                int speedsReversed = 0;

                var ourUnits = _ai.Blackboard.OurActiveBattleAIUnits;
                var theirUnits = _ai.Blackboard.TheirActiveBattleAIUnits;

                foreach( var ourUnit in ourUnits )
                {
                    foreach( var theirUnit in theirUnits )
                    {
                        if( theirUnit.Speed > ourUnit.Speed )
                        {
                            speedsReversed++;
                        }
                    }
                }

                if( speedsReversed > 0 )
                {
                    trickroomPressure++;

                    if( trickroom.Them.RoleProfile.Traits.Contains( RoleTrait.SpreadAttack ) )
                        trickroomPressure++;

                    if( speedsReversed == ourUnits.Count )
                        trickroomPressure++;

                    if( trickroom.Top.AttackerPTKO >= PotentialToKO.Dangerous )
                    {
                        trickroomKOPressure++;

                        if( trickroom.WeHaveAlly )
                        {
                            var themComp_OurAlly = _ai.Projection.MakeUnitComparison( trickroom.Them, trickroom.Us );

                            if( trickroom.TheirAlly?.Speed > trickroom.OurAlly.Speed && themComp_OurAlly.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous )
                            {
                                trickroomKOPressure++;
                            }
                        }
                    }
                    else
                    {
                        theirPressureFails++;
                    }

                    if( trickroom.TheyHaveAlly )
                    {
                        var theirAllyComp_Us = _ai.Projection.MakeUnitComparison( trickroom.TheirAlly, trickroom.Us );
                        
                        if( trickroom.TheirAlly?.Speed > trickroom.Us.Speed && theirAllyComp_Us.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous )
                        {
                            trickroomKOPressure++;
                        }

                        if( trickroom.WeHaveAlly )
                        {
                            var theirAllyComp_OurAlly = _ai.Projection.MakeUnitComparison( tailwind.TheirAlly, trickroom.Us );

                            if( trickroom.TheirAlly?.Speed > trickroom.OurAlly.Speed && theirAllyComp_OurAlly.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous )
                            {
                                trickroomKOPressure++;
                            }
                        }

                        if( trickroom.TheirAlly.RoleProfile.Traits.Contains( RoleTrait.SpreadAttack ) )
                        {
                            trickroomPressure++;
                        }
                    }
                    else
                    {
                        theirPressureFails++;
                    }
                }
                else
                {
                    theirPressureFails++;
                }
            }

            //--Pin possible switch & Spread Damage Pressure
            if( TryGetObservationProfile( e, PairObservation.Attack, out var pinSpreadAttack ) )
            {
                UnitComparison themComp_OurAlly = default;
                UnitComparison theirAllyComp_Us = default;
                UnitComparison theirAllyCom_OurAlly = default;

                if( pinSpreadAttack.Them.RoleProfile.Traits.Contains( RoleTrait.SpreadAttack ) )
                {
                    spreadPressure++;
                }

                if( pinSpreadAttack.WeHaveAlly )
                {
                    themComp_OurAlly = _ai.Projection.MakeUnitComparison( pinSpreadAttack.Them, pinSpreadAttack.OurAlly );

                    if( pinSpreadAttack.TheyHaveAlly )
                    {
                        theirAllyCom_OurAlly = _ai.Projection.MakeUnitComparison( pinSpreadAttack.TheirAlly, pinSpreadAttack.OurAlly );
                    }
                }

                if( pinSpreadAttack.TheyHaveAlly )
                {
                    theirAllyComp_Us = _ai.Projection.MakeUnitComparison( pinSpreadAttack.TheirAlly, pinSpreadAttack.Us );

                    if( pinSpreadAttack.TheirAlly.RoleProfile.Traits.Contains( RoleTrait.SpreadAttack ) )
                    {
                        spreadPressure++;
                    }
                }

                float weSwitchProbability = _ai.UnitSim.PredictSwitchProbability( pinSpreadAttack.Them.Pokemon, pinSpreadAttack.Top.AttackerPTKO, pinSpreadAttack.Top.OpponentPTKO, pinSpreadAttack.Top.AttackerMovedFirst, pinSpreadAttack.Top.Attacker.BeginningHPR, pinSpreadAttack.Top.Opponent.BeginningHPR, pinSpreadAttack.Top.Opponent.Expendability );
                bool weSwitch = weSwitchProbability > 0.85f;

                var ourCandidate = _ai.GetSwitch_CurrentPressure( _ai.Blackboard.TheirActiveBattleAIUnits );
                if( weSwitch && ourCandidate != null )
                {
                    var themComp_OurCand = _ai.Projection.MakeUnitComparison( pinSpreadAttack.Them, ourCandidate );
                    UnitComparison theirAllyComp_OurCand = default;

                    if( e.SourcePattern == PairPattern.FocusFire && pinSpreadAttack.TheyHaveAlly )
                    {
                        theirAllyComp_OurCand = _ai.Projection.MakeUnitComparison( pinSpreadAttack.TheirAlly, ourCandidate );
                    }

                    if( themComp_OurCand.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous || ( pinSpreadAttack.TheyHaveAlly && theirAllyComp_OurCand.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous ) )
                    {
                        pinPressure++;
                    }
                    else
                    {
                        weRelievePressure++;
                        theirPressureFails++;

                        if( pinSpreadAttack.Top.AttackerPTKO >= PotentialToKO.Risky )
                        {
                            weRelievePressure++;
                        }

                        if( pinSpreadAttack.TheyHaveAlly && theirAllyComp_Us.Attacker.BestCurrentPTKO >= PotentialToKO.Risky )
                        {
                            weRelievePressure++;
                        }
                    }
                }
                else
                {
                    theirPressureFails++;
                }
            }
        }

        if( doubleForcesProtect > 0 )   score += Mathf.Max( doubleForcesProtect / 2, 2 );
        if( doubleHasKOPressure > 0 )   score += Mathf.Max( doubleHasKOPressure / 2, 2 );

        if( tailwindPressure > 0 )      score += Mathf.Max( tailwindPressure / 2, 2 );
        if( tailwindKOPressure > 0 )    score += Mathf.Max( tailwindKOPressure / 2, 2 );

        if( trickroomPressure > 0 )     score += Mathf.Max( trickroomPressure / 2, 2 );
        if( trickroomKOPressure > 0 )   score += Mathf.Max( trickroomKOPressure / 2, 2 );

        if( pinPressure > 0 )           score += Mathf.Max( pinPressure / 2, 4 );
        if( spreadPressure > 0 )        score += Mathf.Max( spreadPressure / 2, 2 );

        if( weRelievePressure > 0 )     score -= Mathf.Max( weRelievePressure / 2, 3 );
        if( theirPressureFails > 0 )    score -= Mathf.Max( theirPressureFails / 2, 6 );

        return score;
    }

    private int ScorePunish( PairStrategy strategy, List<PairObservationEvidence> poe )
    {
        int score = 0;

        int weAreLockedDown = 0;
        int weAreMissingAResource = 0;
        int weAreFreeToAct = 0;

        foreach( var e in poe )
        {
            var evidence = ExtractObservationProfile( e );
            var effects = evidence.Move?.MoveSO.MoveEffects;

            var ourBattleUnit = _ai.GetBattleUnit( evidence.Us.Pokemon );
            var ourLastMove = ourBattleUnit.LastUsedMove;
            var usComp_Them = _ai.Projection.MakeUnitComparison( evidence.Us, evidence.Them );

            if( !evidence.Top.OpponentCanAct )
                weAreLockedDown++;

            if( effects?.VolatileStatus == VolatileConditionID.Disabled && evidence.Us.VolatileStatuses.Contains( VolatileConditionID.ChoiceLocked ) )
                weAreLockedDown++;

            if( effects?.VolatileStatus == VolatileConditionID.Encore && evidence.Them.Speed > evidence.Us.Speed )
            {
                if( ourLastMove != null )
                {
                    bool lastWasSetup = _ai.UnitSim.MoveIsSetup( ourLastMove );
                    bool lastWasProtect = ourLastMove.MoveEffects.TransientStatus == TransientConditionID.Protect;
                    bool weOnlyHaveOneAttack = evidence.Us.RoleProfile.Signals.StatusMoveCount >= 3;

                    if( lastWasSetup || lastWasProtect )
                    {
                        weAreLockedDown++;
                    }

                    if( weOnlyHaveOneAttack && usComp_Them.Attacker.BestCurrentPTKO <= PotentialToKO.TwoHKO )
                    {
                        weAreLockedDown++;
                    }
                }
            }

            if( effects?.VolatileStatus == VolatileConditionID.Taunt && evidence.Us.RoleProfile.Signals.StatusMoveCount >= 2 )
            {
                weAreLockedDown++;
                weAreMissingAResource++;

                if( usComp_Them.Attacker.BestCurrentPTKO <= PotentialToKO.TwoHKO )
                    weAreLockedDown++;
            }

            if( ourBattleUnit.Flags[UnitFlags.Recharging].IsActive )
                weAreLockedDown++;

            if( ourBattleUnit.Flags[UnitFlags.SuccessiveProtectUses].Count > 0 )
                weAreMissingAResource++;

            if( !_ai.CanUseFakeOut( evidence.Us, evidence.Them ) )
                weAreMissingAResource++;

            if( weAreLockedDown == 0 && weAreMissingAResource == 0 )
                weAreFreeToAct++;
        }

        if( weAreLockedDown > 0 )           score += Mathf.Max( weAreLockedDown, 4 );
        if( weAreMissingAResource > 0 )     score += Mathf.Max( weAreMissingAResource, 2 );
        if( weAreFreeToAct > 0 )            score -= Mathf.Max( weAreFreeToAct, 2 );

        return score;
    }

    private int ScoreResource( PairStrategy strategy, List<PairObservationEvidence> poe )
    {
        int score = 0;

        int renewableResources = 0;
        int consumableResources = 0;
        int positionalResources = 0;
        int resourceInvestment = 0;
        int resourceCost = 0;

        foreach( var e in poe )
        {
            var generic = ExtractObservationProfile( e );
            var ourPTKO = generic.Top.OpponentPTKO;
            var currentField = _ai.Blackboard.CurrentFieldSnapshot;

            var genericThemBefore = _ai.GetPokemonAs_Adapter( generic.Them.Pokemon );

            if( generic.Them.RoleProfile.Traits.Contains( RoleTrait.RecoveryItem ) && genericThemBefore.Item != generic.Them.Item )
            {
                var themBeforeComp_Us = _ai.Projection.MakeUnitComparison( genericThemBefore, generic.Us );
                var themAfterComp_Us = _ai.Projection.MakeUnitComparison( generic.Them, generic.Us );
                if( themBeforeComp_Us.Attacker.BestCurrentPTKO >= themAfterComp_Us.Attacker.BestCurrentPTKO )
                {
                    resourceInvestment++;

                    if( (int)themBeforeComp_Us.Attacker.BestCurrentPTKO - (int)themAfterComp_Us.Attacker.BestCurrentPTKO > 1 )
                    {
                        resourceCost++;
                    }
                }
            }

            //--TODO: These weather/terrain renew checks probably don't work. i need to add a "last weather" and "last terrain" field to the real Battlefield so it can actually inform SimulatedField, and check last weather/terrain && duration <= 0
            if( TryGetObservationProfile( e, PairObservation.WeatherChange, out var weatherChange ) )
            {
                if( currentField.LastWeather == weatherChange.Top.Field.Weather && currentField.WeatherDuration == 0 && weatherChange.Top.Field.WeatherDuration > 0 )
                {
                    positionalResources++;

                    var theirSP = _ai.Projection.GetStrategicProfile( weatherChange.Them );
                    if( theirSP.BattlefieldFlipper || theirSP.WeatherSetter )
                    {
                        renewableResources++;
                    }
                }
            }

            //--TODO: These weather/terrain renew checks probably don't work. i need to add a "last weather" and "last terrain" field to the real Battlefield so it can actually inform SimulatedField, and check last weather/terrain && duration <= 0
            if( TryGetObservationProfile( e, PairObservation.TerrainChange, out var terrainChange ) )
            {
                if( currentField.LastTerrain == terrainChange.Top.Field.Terrain && currentField.TerrainDuration == 0 && terrainChange.Top.Field.TerrainDuration > 0 )
                {
                    positionalResources++;

                    var theirSP = _ai.Projection.GetStrategicProfile( terrainChange.Them );
                    if( theirSP.BattlefieldFlipper || theirSP.TerrainSetter )
                    {
                        renewableResources++;
                    }
                }
            }

            if( TryGetObservationProfile( e, PairObservation.StatusMove, out var statusEvidence ) )
            {
                StatusThreatResult str = default;

                if( statusEvidence.IntentResult.ActionType == ActionType.SupportiveStatus && ourPTKO <= PotentialToKO.Risky )
                {
                    str = (StatusThreatResult)statusEvidence.IntentResult;

                    if( str.SupportiveStatusType == SupportiveStatusType.Recovery )
                    {
                        var themBefore = _ai.GetPokemonAs_Adapter( statusEvidence.Them.Pokemon );
                        var themAfter = statusEvidence.Them;

                        var usComp_ThemBefore = _ai.Projection.MakeUnitComparison( statusEvidence.Us, themBefore );
                        var usComp_ThemAfter = _ai.Projection.MakeUnitComparison( statusEvidence.Us, themAfter );
                        
                        if( usComp_ThemBefore.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous && usComp_ThemAfter.Attacker.BestCurrentPTKO <= PotentialToKO.Risky )
                            renewableResources++;

                        bool theyHaveSash = themBefore.Item == ItemBattleEffectID.FocusSash;
                        bool theyHaveSturdy = themBefore.Ability == AbilityID.Sturdy;
                        bool theyHaveMultiscale = themBefore.Ability == AbilityID.Multiscale;
                        bool theyHaveDisguise = themBefore.Ability == AbilityID.Disguise;
                        bool theyHaveFullHPResource = theyHaveSash || theyHaveSturdy || theyHaveMultiscale || theyHaveDisguise;

                        if( theyHaveFullHPResource && themAfter.BeginningHPR == 1f )
                            consumableResources++;
                    }

                    if( str.SupportiveStatusType == SupportiveStatusType.AllyProtection && statusEvidence.TheyHaveAlly )
                    {
                        var theirAllyBefore = _ai.GetPokemonAs_Adapter( statusEvidence.TheirAlly.Pokemon );
                        var theirAllyAfter = statusEvidence.TheirAlly;

                        var usComp_TheirAllyBefore = _ai.Projection.MakeUnitComparison( statusEvidence.Us, theirAllyBefore );
                        var UsComp_TheirAllyAfter = _ai.Projection.MakeUnitComparison( statusEvidence.Us, theirAllyAfter );

                        bool theyHaveSash = theirAllyBefore.Item == ItemBattleEffectID.FocusSash;
                        bool theyHaveSturdy = theirAllyBefore.Ability == AbilityID.Sturdy;
                        bool theyHaveMultiscale = theirAllyBefore.Ability == AbilityID.Multiscale;
                        bool theyHaveDisguise = theirAllyBefore.Ability == AbilityID.Disguise;
                        bool theyHaveFullHPResource = theyHaveSash || theyHaveSturdy || theyHaveMultiscale || theyHaveDisguise;

                        if( usComp_TheirAllyBefore.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous && UsComp_TheirAllyAfter.Attacker.BestCurrentPTKO <= PotentialToKO.Risky )
                            renewableResources++;

                        if( theyHaveFullHPResource && theirAllyAfter.BeginningHPR == 1f )
                            consumableResources++;
                    }

                    if( str.SupportiveStatusType == SupportiveStatusType.ForceMultiplier )
                    {
                        var effect = str.Move.MoveSO.MoveEffects.CourtCondition;
                        var theirCourt = statusEvidence.Them.CourtLocation == CourtLocation.TopCourt ? currentField.TopCourtConditions : currentField.BottomCourtConditions;

                        if( !theirCourt.ContainsKey( effect ) )
                            positionalResources++;
                    }
                }
                else if( statusEvidence.IntentResult.ActionType == ActionType.SupportiveStatus && ourPTKO >= PotentialToKO.Dangerous )
                {
                    resourceInvestment++;
                }

                if( statusEvidence.IntentResult.ActionType == ActionType.OffensiveStatus && ourPTKO <= PotentialToKO.Risky )
                {
                    str = (StatusThreatResult)statusEvidence.IntentResult;
                    
                    if( str.OffensiveStatusType == OffensiveStatusType.EntryHazard )
                    {
                        var hazard = str.Move.MoveSO.MoveEffects.CourtCondition;
                        if( hazard == CourtConditionID.StickyWeb )
                            positionalResources += 3;
                        else if( hazard == CourtConditionID.LeechSeed )
                            positionalResources += 2;
                        else
                            positionalResources++;
                    }
                }
                else if( statusEvidence.IntentResult.ActionType == ActionType.OffensiveStatus && ourPTKO >= PotentialToKO.Dangerous )
                {
                    resourceInvestment++;
                }
            }

            if( TryGetObservationProfile( e, PairObservation.Switch, out var switchEvidence ) )
            {
                //--we have to do this because the Them from a switch intent will be the candidate, not the returning pokemon
                var theirAlly = _ai.GetActiveAllyAs_Adapter( switchEvidence.Them.Pokemon );
                var themBefore = _ai.GetActiveAllyAs_Adapter( theirAlly.Pokemon );

                if( themBefore.Ability == AbilityID.Regenerator && themBefore.BeginningHPR <= 0.75f )
                    renewableResources++;

                if( themBefore.Item == ItemBattleEffectID.SitrusBerry || themBefore.Item == ItemBattleEffectID.LumBerry ) //--will need to improve berry detection once more berries are in the game
                    consumableResources++;
            }
        }

        if( renewableResources > 0 )    score += Mathf.Max( renewableResources, 3 );
        if( consumableResources > 0 )   score += Mathf.Max( consumableResources, 3 );
        if( positionalResources > 0 )   score += Mathf.Max( positionalResources, 3 );

        if( resourceInvestment > 0 )    score -= Mathf.Max( resourceInvestment, 3 );
        if( resourceCost > 0 )          score -= Mathf.Max( resourceCost, 6 );

        return score;
    }

    private int ScoreConversion( PairStrategy strategy, List<PairObservationEvidence> poe )
    {
        int score = 0;

        int fieldConversion = 0;
        int materialConversion = 0;
        int actionRisksLead = 0;

        foreach( var e in poe )
        {
            var generic = ExtractObservationProfile( e );
            var leadEval = _ai.Projection.GetLeadState( generic.Them, generic.Us );
            var currentField = _ai.Blackboard.CurrentFieldSnapshot;
            var ourPTKO = generic.Top.OpponentPTKO;

            if( TryGetObservationProfile( e, PairObservation.Tailwind, out var tailwind ) )
            {
                var theirCourt = tailwind.Them.CourtLocation == CourtLocation.TopCourt ? currentField.TopCourtConditions : currentField.BottomCourtConditions;

                if( !theirCourt.ContainsKey( CourtConditionID.Tailwind ) && ourPTKO <= PotentialToKO.Risky )
                {
                    if( leadEval.Lead >= LeadState.Ahead )
                    {
                        fieldConversion += 2;
                    }
                    else if( leadEval.Lead >= LeadState.Even )
                    {
                        fieldConversion += 1;
                    }
                }
                else if( !theirCourt.ContainsKey( CourtConditionID.Tailwind ) && ourPTKO >= PotentialToKO.Dangerous ) 
                {
                    if( leadEval.Lead >= LeadState.Ahead && leadEval.Speed > 0 && leadEval.Pressure > 0 )
                    {
                        actionRisksLead++;
                    }
                }
            }

            if( TryGetObservationProfile( e, PairObservation.TrickRoom, out var trickroom ) )
            {
                var theirSP = _ai.Projection.GetStrategicProfile( trickroom.Them );

                if( theirSP.DependsOnTrickRoom && !currentField.FieldConditions.ContainsKey( FieldConditionID.TrickRoom ) && ourPTKO <= PotentialToKO.Risky )
                {
                    if( leadEval.Lead >= LeadState.Ahead )
                    {
                        fieldConversion += 2;
                    }
                    else if( leadEval.Lead >= LeadState.Even )
                    {
                        fieldConversion += 1;
                    }
                }
                else if( !currentField.FieldConditions.ContainsKey( FieldConditionID.TrickRoom ) && ourPTKO >= PotentialToKO.Dangerous ) 
                {
                    if( leadEval.Lead >= LeadState.Ahead && leadEval.Speed >= -1 && leadEval.Pressure > 0 )
                    {
                        actionRisksLead++;
                    }
                }
            }

            if( TryGetObservationProfile( e, PairObservation.WeatherChange, out var weatherChange ) )
            {
                var theirSP = _ai.Projection.GetStrategicProfile( weatherChange.Them );

                if( currentField.Weather != weatherChange.Top.Field.Weather && ( theirSP.BattlefieldSetter || theirSP.BattlefieldFlipper || theirSP.DependsOnCurrentWeather ) )
                {
                    if( leadEval.Lead >= LeadState.Ahead )
                    {
                        fieldConversion += 2;
                    }
                    else if( leadEval.Lead >= LeadState.Even )
                    {
                        fieldConversion += 1;
                    }
                }
                else if( !currentField.FieldConditions.ContainsKey( FieldConditionID.TrickRoom ) && ourPTKO >= PotentialToKO.Dangerous ) 
                {
                    if( leadEval.Lead >= LeadState.Ahead && ( leadEval.Pressure > 0 || leadEval.Weather >= -1 ) )
                    {
                        actionRisksLead++;
                    }
                }
            }

            if( TryGetObservationProfile( e, PairObservation.TerrainChange, out var terrainChange ) )
            {
                var theirSP = _ai.Projection.GetStrategicProfile( terrainChange.Them );

                if( currentField.Terrain != terrainChange.Top.Field.Terrain && ( theirSP.BattlefieldSetter || theirSP.BattlefieldFlipper || theirSP.DependsOnCurrentTerrain ) )
                {
                    if( leadEval.Lead >= LeadState.Ahead )
                    {
                        fieldConversion += 2;
                    }
                    else if( leadEval.Lead >= LeadState.Even )
                    {
                        fieldConversion += 1;
                    }
                }
                else if( !currentField.FieldConditions.ContainsKey( FieldConditionID.TrickRoom ) && ourPTKO >= PotentialToKO.Dangerous ) 
                {
                    if( leadEval.Lead >= LeadState.Ahead && ( leadEval.Pressure > 0 || leadEval.Terrain >= -1 ) )
                    {
                        actionRisksLead++;
                    }
                }
            }

            if( TryGetObservationProfile( e, PairObservation.Attack, out var attack ) )
            {
                if( attack.Top.AttackerPTKO >= PotentialToKO.Dangerous && ( attack.Top.AttackerMovedFirst || attack.Top.OpponentPTKO <= PotentialToKO.Risky ) )
                {
                    if( leadEval.Lead >= LeadState.Ahead )
                    {
                        materialConversion += 2;
                    }
                    else if( leadEval.Lead >= LeadState.Even )
                    {
                        materialConversion += 1;
                    }
                }
                else if( attack.Top.AttackerPTKO >= PotentialToKO.Dangerous && !attack.Top.OpponentMovedFirst || attack.Top.OpponentPTKO >= PotentialToKO.Dangerous ) 
                {
                    if( leadEval.Lead >= LeadState.Ahead && ( leadEval.Material < 0 || leadEval.HP < 0 ) )
                    {
                        actionRisksLead++;
                    }
                }
            }
        }

        score += Mathf.Max( fieldConversion, 3 );
        score += Mathf.Max( materialConversion, 3 );
        score -= Mathf.Max( actionRisksLead, 5 );

        return score;
    }

    private int ScoreStrategyCommitment( PairStrategy strategy, List<PairObservationEvidence> poe )
    {
        // How much of the board confirms their current investment in a given strategy?
        // is a tailwind setter sitting next to a support that enables it to set up safely? or an attacker that can take advantage immediately?
        // Does a swords dance user have a fake out or redirection partner?
        // is there a weather abuser on the field with a setter in the back as an incoming switch candidate?

        // we filter by strategy and decide whether there is appropriate board investment that supports the strategy out-right.
        // contradictory signals should very slightly penalize.

        // this approach means that each strategy will need its own block that simply adds a point for each relative investment available.
        // which makes this function extremely nuanced.

        // SecureImmediateKO
        // Requires FocusFire and Double Attack and Attack.
        // Patterns: FocusFire, DoubleAttack.
        // Observations: Attack, RevengeKO.
        // Opportunity: Immediate Removal, Conversion, Pressure
        
        
        // ApplyBoardPressure
        // Requires: Double Attack or Focus Fire or Attack.
        // Patterns: DoubleAttack, FocusFire.
        // Observations: Attack. Opportunity: Pressure, Momentum, Board Control, Punish
        
        
        // PreserveTempo
        // Requires: Switch, Pivot Pressure.
        // Patterns: CoveredSwitch.
        // Observations: Switch, PivotPressure, Preservation.
        // Opportunity: Momentum, Initiative, Guaranteed Value
        
        
        // DenyOpponentSetup
        // Requires: Cover, maybe a new Observation called Interruption.
        // Patterns: None currently.
        // Observations: Cover, SpeedControl, FakeOut/Redirection/Guard Support, new observation called StatusDisruption.
        // Opportunity: Denial, Initiative, Pressure 
        
        
        // EstablishDefensivePosition
        //--Requires Screens or Wide Guard or Protect or Switch.
        // Patterns: CoveredSetup, new pattern called DefensivePlay or something.
        // Observations: Protect, Cover, ScreensSupport, GuardSupport.
        // Opportunity: Resource, Momentum, Guaranteed Value
        

        return strategy switch
        {
            PairStrategy.EstablishTrickRoom             => ScoreEstablishTrickRoomCommitment( poe ),
            PairStrategy.EstablishTailwind              => ScoreEstablishTailwindCommitment( poe ),
            PairStrategy.WeatherPivot                   => ScoreWeatherPivotCommitment( poe ),
            PairStrategy.SecureImmediateKO              => ScoreImmediateKOCommitment( poe ),
            PairStrategy.ApplyBoardPressure             => ScoreApplyBoardPressureCommitment( poe ),
            PairStrategy.PreserveTempo                  => ScorePreserveTempoCommitment( poe ),
            PairStrategy.DenyOpponentSetup              => ScoreDenyOpponentSetupCommitment( poe ),
            PairStrategy.EstablishDefensivePosition     => ScoreEstablishDefensivePositionCommitment( poe ),
            _ => 0,
        };
    }

    private int ScoreEstablishTrickRoomCommitment( List<PairObservationEvidence> poe )
    {
        int score = 0;

        var ourActiveUnits = _ai.Blackboard.OurActiveBattleAIUnits;
        var theirActiveUnits = _ai.Blackboard.TheirActiveBattleAIUnits;

        var ourRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( ourActiveUnits[0].Pokemon ).Where( u => !ourActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();
        var theirRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( theirActiveUnits[0].Pokemon ).Where( u => !theirActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();

        var currentField = _ai.Blackboard.CurrentFieldSnapshot;

        bool theirAllyCanFakeOut = false;
        bool theirAllyHasRedirection = false;
        bool theirAllyHasPivotMove = false;
        bool theirAllyIsIntimidateSupport = false;
        bool theirAllyBlocksPriority = false;

        bool theirAllyIsOffensive = false;
        bool theirAllyIsAppropriateSpeedTier = false;
        bool theirAllyHasSpreadMoves = false;
        bool theirAllyHasOneLikelyKO = false;
        bool theirAllyHasTwoLikelyKOs = false;
        bool theirAllyHasSpreadKOAndChip = false;
        bool theirAllyHasDoubleSpreadKO = false;

        bool theyHaveAbusersInBack = false;
        bool currentPairIsTrickRoomCore = false;

        bool setterCanFakeOut = false;
        bool setterCanRedirect = false;
        bool setterBlocksPriority = false;
        bool setterHasOneLikelyKO = false;
        bool setterHasTwoLikelyKOs = false;
        bool setterHasSpreadKOAndChip = false;
        bool setterHasDoubleSpreadKO = false;
        bool setterIsAppropriateSpeedTier = false;
        bool setterHasSideRecovery = false;

        IBattleAIUnit theirAlly = null;
        IBattleAIUnit setter = null;
        PairObservationProfile trickroom = default;
        foreach( var e in poe )
        {
            if( TryGetObservationProfile( e, PairObservation.TrickRoom, out trickroom ) )
            {
                if ( setter == null )
                {
                    setter = trickroom.Them;
                }
                else if( setter.Pokemon != trickroom.Them.Pokemon )
                {
                    Debug.LogError( $"You were right, different setters break this structure! Either evaluate all occurances and pick the better scenario, pick the one that is primary intent > secondary intent, or simply pick the first from the list." );
                }

                var setterRP = setter.RoleProfile;
                setterCanFakeOut = _ai.CanUseFakeOut( setter, trickroom.Us ) || trickroom.WeHaveAlly && _ai.CanUseFakeOut( setter, trickroom.OurAlly );
                setterCanRedirect = setterRP.Traits.Contains( RoleTrait.RedirectionMove );
                setterBlocksPriority = setterRP.Traits.Contains( RoleTrait.PriorityBlocker ) || setter.Ability == AbilityID.PsychicSurge && currentField.Terrain == TerrainID.Psychic;
                setterIsAppropriateSpeedTier = setterRP.Biases.Contains( RoleBias.SlowSpeed ) || setterRP.Biases.Contains( RoleBias.TrickRoomSpeed );
                setterHasSideRecovery = _ai.UnitSim.PokemonHasMove_SideRecovery( setter.Pokemon );

                if( trickroom.TheyHaveAlly )
                {
                    theirAlly = trickroom.TheirAlly;
                    var theirAllyRP = trickroom.TheirAlly.RoleProfile;
                    theirAllyCanFakeOut = _ai.CanUseFakeOut( trickroom.TheirAlly, trickroom.Us ) || ( trickroom.WeHaveAlly && _ai.CanUseFakeOut( trickroom.TheirAlly, trickroom.OurAlly ) );
                    theirAllyHasRedirection = theirAllyRP.Traits.Contains( RoleTrait.RedirectionMove );
                    theirAllyHasPivotMove = theirAllyRP.Traits.Contains( RoleTrait.PivotMove );

                    theirAllyIsOffensive = theirAllyRP.PrimaryRole == RoleClass.Sweeper || theirAllyRP.PrimaryRole == RoleClass.SetupSweeper || theirAllyRP.PrimaryRole == RoleClass.BulkyAttacker || theirAllyRP.PrimaryRole == RoleClass.RevengeKiller || theirAllyRP.PrimaryRole == RoleClass.WallBreaker || theirAllyRP.PrimaryRole == RoleClass.TrickRoomAbuser || theirAllyRP.SecondaryRoles.Contains( RoleClass.TrickRoomAbuser );
                    theirAllyIsAppropriateSpeedTier = theirAllyRP.Biases.Contains( RoleBias.SlowSpeed ) || theirAllyRP.Biases.Contains( RoleBias.TrickRoomSpeed );
                    theirAllyHasSpreadMoves = theirAllyRP.Traits.Contains( RoleTrait.SpreadAttack );

                    theirAllyIsIntimidateSupport = trickroom.TheirAlly.Ability == AbilityID.Intimidate || trickroom.TheirAlly.Ability == AbilityID.Demoralize;

                    theirAllyBlocksPriority = theirAllyRP.Traits.Contains( RoleTrait.PriorityBlocker ) || trickroom.TheirAlly.Ability == AbilityID.PsychicSurge && currentField.Terrain == TerrainID.Psychic;
                }

                //--KO Checks
                var themComp_Us = _ai.Projection.MakeUnitComparison( trickroom.Them, trickroom.Us );
                var themComp_OurAlly = trickroom.WeHaveAlly ? _ai.Projection.MakeUnitComparison( trickroom.Them, trickroom.OurAlly ) : default;
                var theirAllyComp_Us = trickroom.TheyHaveAlly ? _ai.Projection.MakeUnitComparison( trickroom.TheirAlly, trickroom.Us ) : default;
                var theirAllyComp_OurAlly = trickroom.TheyHaveAlly && trickroom.WeHaveAlly ? _ai.Projection.MakeUnitComparison( trickroom.TheirAlly, trickroom.OurAlly ) : default;
                
                var setterPTKO_us = themComp_Us.Attacker.BestCurrentPTKO;
                var setterPTKO_ourAlly = themComp_OurAlly.Attacker.BestCurrentPTKO;
                var theirAllyPTKO_us = theirAllyComp_Us.Attacker.BestCurrentPTKO;
                var theirAllyPTKO_ourAlly = theirAllyComp_OurAlly.Attacker.BestCurrentPTKO;

                if( setterPTKO_us >= PotentialToKO.Dangerous )
                {
                    setterHasOneLikelyKO = true;
                    
                    var moveTarget =  themComp_Us.Attacker.CurrentPTKOs.Keys.First().MoveSO.MoveTarget;
                    if( moveTarget == MoveTarget.AllAdjacent || moveTarget == MoveTarget.OpposingSide )
                    {
                        setterHasSpreadKOAndChip = true;
                    }

                    if( trickroom.OurAlly != null && setterPTKO_ourAlly >= PotentialToKO.Dangerous )
                    {
                        setterHasTwoLikelyKOs = true;
                        
                        if( moveTarget == MoveTarget.AllAdjacent || moveTarget == MoveTarget.OpposingSide )
                        {
                            setterHasDoubleSpreadKO = true;
                        }
                    }
                }

                if( trickroom.OurAlly != null && setterPTKO_ourAlly >= PotentialToKO.Dangerous )
                {
                    setterHasOneLikelyKO = true;

                    var moveTarget =  themComp_OurAlly.Attacker.CurrentPTKOs.Keys.First().MoveSO.MoveTarget;
                    if( moveTarget == MoveTarget.AllAdjacent || moveTarget == MoveTarget.OpposingSide )
                    {
                        setterHasSpreadKOAndChip = true;
                    }

                    if( setterPTKO_us >= PotentialToKO.Dangerous )
                    {
                        setterHasTwoLikelyKOs = true;

                        if( moveTarget == MoveTarget.AllAdjacent || moveTarget == MoveTarget.OpposingSide )
                        {
                            setterHasDoubleSpreadKO = true;
                        }
                    }
                }

                if( trickroom.TheyHaveAlly && theirAllyPTKO_us >= PotentialToKO.Dangerous )
                {
                    theirAllyHasOneLikelyKO = true;

                    var moveTarget = theirAllyComp_Us.Attacker.CurrentPTKOs.Keys.First().MoveSO.MoveTarget;
                    bool isSpreadMove = moveTarget == MoveTarget.AllAdjacent || moveTarget == MoveTarget.OpposingSide;
                    if( isSpreadMove )
                    {
                        theirAllyHasSpreadKOAndChip = true;
                    }

                    if( trickroom.WeHaveAlly && theirAllyPTKO_ourAlly >= PotentialToKO.Dangerous )
                    {
                        theirAllyHasTwoLikelyKOs = true;

                        if( isSpreadMove )
                        {
                            theirAllyHasDoubleSpreadKO = true;
                        }
                    }
                }

                if( trickroom.TheyHaveAlly && theirAllyPTKO_ourAlly >= PotentialToKO.Dangerous )
                {
                    theirAllyHasOneLikelyKO = true;

                    var moveTarget = theirAllyComp_Us.Attacker.CurrentPTKOs.Keys.First().MoveSO.MoveTarget;
                    bool isSpreadMove = moveTarget == MoveTarget.AllAdjacent || moveTarget == MoveTarget.OpposingSide;

                    if( isSpreadMove )
                    {
                        theirAllyHasSpreadKOAndChip = true;
                    } 

                    if( theirAllyPTKO_us >= PotentialToKO.Dangerous )
                    {
                        theirAllyHasTwoLikelyKOs = true;

                        if( isSpreadMove )
                        {
                            theirAllyHasDoubleSpreadKO = true;
                        }
                    }
                }
            }
        }

        //--Setter checks
        bool setterHasFullHPSave = setter.BeginningHPR == 1f && ( setter.Item == ItemBattleEffectID.FocusSash || setter.Ability == AbilityID.Multiscale || setter.Ability == AbilityID.Sturdy || setter.Ability == AbilityID.Disguise );
        bool setterIsTauntProof = setter.RoleProfile.Traits.Contains( RoleTrait.TauntImmune );
        bool setterIsFlinchProof = setter.Item == ItemBattleEffectID.CovertCloak;
        bool setterHasRedirection = setter.RoleProfile.Traits.Contains( RoleTrait.RedirectionMove );
        bool setterContributesAfter = setter.RoleProfile.Traits.Contains( RoleTrait.SpreadAttack ) || setter.Pokemon.CheckHasActiveMove( "Helping Hand" ) || setterHasRedirection || setterHasSideRecovery || setterHasOneLikelyKO;

        currentPairIsTrickRoomCore = setterIsAppropriateSpeedTier && theirAllyIsAppropriateSpeedTier && ( ( ( setterHasRedirection || setterBlocksPriority ) && theirAllyIsOffensive ) || ( setterHasOneLikelyKO && ( theirAllyHasRedirection || theirAllyCanFakeOut || theirAllyHasOneLikelyKO ) ) || ( setterHasSideRecovery && theirAllyHasOneLikelyKO ) );

        //--Back line check
        int abusers = 0;
        foreach( var unit in theirRemainingUnits )
        {
            var unitRP = unit.RoleProfile;
            var primary = unitRP.PrimaryRole;
            var biases = unitRP.Biases;

            bool unitIsAppropriateOffense = primary == RoleClass.Sweeper || primary == RoleClass.SetupSweeper || primary == RoleClass.BulkyAttacker || primary == RoleClass.RevengeKiller || primary == RoleClass.WallBreaker;
            bool unitIsAppropriateSpeedTier = biases.Contains( RoleBias.SlowSpeed ) || biases.Contains( RoleBias.TrickRoomSpeed );

            if( unitIsAppropriateOffense && unitIsAppropriateSpeedTier )
                abusers++; 
        }

        if( abusers > 0 )
            theyHaveAbusersInBack = true;

        //--------------------------------------------------------------------
        //--Investment--------------------------------------------------------
        //--------------------------------------------------------------------
        if( setterIsTauntProof )
            score += 1;

        if( setterIsFlinchProof )
            score += 1;

        if( theirAllyCanFakeOut || theirAllyHasRedirection )
            score += 1;

        if( theirAllyHasPivotMove )
            score += 1;

        if( theirAllyIsIntimidateSupport )
            score += 1;

        if( theirAllyBlocksPriority )
            score += 1;

        if( theyHaveAbusersInBack )
            score += 1;

        if( currentPairIsTrickRoomCore )
            score += 1;

        if( setterHasDoubleSpreadKO )
        {
            score += 3;
        }
        else if( setterHasSpreadKOAndChip )
        {
            score += 2;
        }
        else if( setterHasTwoLikelyKOs || setterHasOneLikelyKO )
        {
            score += 1;
        }

        //--Partner directly abuses tailwind by being an offensive role + not a slow speed tier
        if( theirAllyIsAppropriateSpeedTier )
        {
            score += 1;

            if( theirAllyIsOffensive )
                score += 1;

            if( theirAllyHasSpreadMoves )
                score += 1;

            if( theirAllyHasDoubleSpreadKO )
            {
                score += 3;
            }
            else if( theirAllyHasSpreadKOAndChip )
            {
                score += 2;
            }
            else if( theirAllyHasTwoLikelyKOs || theirAllyHasOneLikelyKO )
            {
                score += 1;
            }
        }
        else //--contradicted by faster speed presence
        {
            score -= 3;
        }

        //--------------------------------------------------------------------
        //--Line Lock---------------------------------------------------------
        //--------------------------------------------------------------------

        //--Trick Room Core on field?
        //-->currentPairIsTrickRoomCore bool already exists

        //--Dedicated Slow Team?
        bool dedicatedSlowMode = abusers >= theirRemainingUnits.Count;

        //--Fast back up, tailwind mode, or weather mode switch?
        int fastBackUps = 0;
        bool backTailwindMode = false;
        bool backWeatherMode = false;
        bool trickRoomSetterInBack = false;

        foreach( var unit in theirRemainingUnits )
        {
            var rp = unit.RoleProfile;
            var traits = rp.Traits;
            var sp = _ai.Projection.GetStrategicProfile( unit );

            if( traits.Contains( RoleTrait.TailwindSetter ) && abusers < theirRemainingUnits.Count )
            {
                foreach( var adapter in _ai.Blackboard.TheirTeamAdapters.Values )
                {
                    if( adapter.Pokemon == unit.Pokemon )
                        continue;

                    if( adapter.RoleProfile.Biases.Contains( RoleBias.MiddlingSpeed ) || adapter.RoleProfile.Biases.Contains( RoleBias.FastSpeed ) )
                    {
                        if( _ai.UnitSim.PokemonIsOffensiveRole( adapter ) )
                        {
                            backTailwindMode = true;
                            break;
                        }
                    }
                }
            }

            if( traits.Contains( RoleTrait.WeatherSetter ) )
            {
                var backWeather = _ai.UnitSim.GetWeatherFrom_Ability( unit.Pokemon );
                foreach( var adapter in _ai.Blackboard.TheirTeamAdapters.Values )
                {
                    if( _ai.UnitSim.PokemonAbilityMatchesWeather( adapter.Pokemon, backWeather ) || _ai.UnitSim.PokemonHasMove_AbusesWeather( adapter.Pokemon, backWeather ) )
                    {
                        backWeatherMode = true;
                        break;
                    }
                }
            }

            if( unit.RoleProfile.Biases.Contains( RoleBias.MiddlingSpeed ) || unit.RoleProfile.Biases.Contains( RoleBias.FastSpeed ) )
            {
                if( _ai.UnitSim.PokemonIsOffensiveRole( unit ) )
                {
                    fastBackUps++;
                }
            }

            if( unit.RoleProfile.Traits.Contains( RoleTrait.TrickRoomSetter ) )
                trickRoomSetterInBack = true;
        }

        //--Other Mode Counting
        int otherModes = 0;
        if( fastBackUps > 0 )
            otherModes++;

        if( backTailwindMode )
            otherModes++;

        if( backWeatherMode )
            otherModes++;

        //--Score Line Lock Signals
        if( currentPairIsTrickRoomCore )
            score += 1;

        if( dedicatedSlowMode )
            score += 1;

        if( otherModes == 0 )
            score += 2;
        else if( otherModes < 2 )
            score += 1;

        //--------------------------------------------------------------------
        //--Irreversibility---------------------------------------------------
        //--------------------------------------------------------------------
        if( currentPairIsTrickRoomCore && ( dedicatedSlowMode || ( abusers >= theirRemainingUnits.Count - 1 && otherModes == 0 ) ) )
            score += 2;
        else if( dedicatedSlowMode || ( abusers >= theirRemainingUnits.Count - 1 && otherModes == 0 ) || ( currentPairIsTrickRoomCore && abusers > 1 && trickRoomSetterInBack ) )
            score += 1;

        if( abusers > 0 && trickRoomSetterInBack )
            score += 1;

        //--------------------------------------------------------------------
        //--Opportunity Cost--------------------------------------------------
        //--------------------------------------------------------------------
        bool currentTurnHasSetupWindow = false;
        bool attackingHasLittleValue = false;
        bool waitingLosesBoardAdvantage = false;
        bool abusersNeedTrickRoom = false;

        bool theirAllyCanRedirect = theirAlly != null && theirAlly.RoleProfile.Traits.Contains( RoleTrait.RedirectionMove );
        if( theirAllyCanFakeOut || setterBlocksPriority || theirAllyBlocksPriority || theirAllyCanRedirect )
            currentTurnHasSetupWindow = true;

        var theirSetterComp_Left = _ai.Projection.MakeUnitComparison( setter, ourActiveUnits[0] );
        var theirSetterComp_Right = ourActiveUnits.Count > 1 ? _ai.Projection.MakeUnitComparison( setter, ourActiveUnits[1] ) : default;
        var theirAllyComp_Left = theirAlly != null ? _ai.Projection.MakeUnitComparison( theirAlly, ourActiveUnits[0] ) : default;
        var theirAllyComp_Right = theirAlly != null && ourActiveUnits.Count > 1 ? _ai.Projection.MakeUnitComparison( theirAlly, ourActiveUnits[1] ) : default;

        bool setterAttacksBadly = false;
        bool theirAllyAttacksBadly = false;
        bool theyLoseSetter = false;

        if( theirSetterComp_Left.Attacker.BestCurrentPTKO <= PotentialToKO.Risky || theirSetterComp_Left.Target.BestCurrentPTKO >= PotentialToKO.Dangerous && theirSetterComp_Left.Target.FasterSpeed )
            setterAttacksBadly = true;

        if( ourActiveUnits.Count > 1  && ( theirSetterComp_Right.Attacker.BestCurrentPTKO <= PotentialToKO.Risky || theirSetterComp_Right.Target.BestCurrentPTKO >= PotentialToKO.Dangerous && theirSetterComp_Right.Target.FasterSpeed ) )
            setterAttacksBadly = true;

        if( theirAlly != null && ( theirAllyComp_Left.Attacker.BestCurrentPTKO <= PotentialToKO.Risky || theirAllyComp_Left.Target.BestCurrentPTKO >= PotentialToKO.Dangerous && theirAllyComp_Left.Target.FasterSpeed ) )
            theirAllyAttacksBadly = true;

        if( ourActiveUnits.Count > 1 && theirAlly != null && ( theirAllyComp_Right.Attacker.BestCurrentPTKO <= PotentialToKO.Risky || theirAllyComp_Right.Target.BestCurrentPTKO >= PotentialToKO.Dangerous && theirAllyComp_Right.Target.FasterSpeed ) )
            theirAllyAttacksBadly = true;

        if( theirSetterComp_Left.Target.BestCurrentPTKO >= PotentialToKO.Dangerous || ourActiveUnits.Count > 1 && theirSetterComp_Right.Target.BestCurrentPTKO >= PotentialToKO.Dangerous )
            theyLoseSetter = true;

        attackingHasLittleValue = setterAttacksBadly || theirAllyAttacksBadly;
        waitingLosesBoardAdvantage = currentTurnHasSetupWindow && attackingHasLittleValue || theyLoseSetter;
        abusersNeedTrickRoom = currentPairIsTrickRoomCore && abusers > 1;

        if( currentTurnHasSetupWindow )
            score += 1;

        if( attackingHasLittleValue )
            score += 1;

        if( waitingLosesBoardAdvantage )
            score += 1;

        if( abusersNeedTrickRoom )
            score += 1;

        return score;
    }

    private int ScoreEstablishTailwindCommitment( List<PairObservationEvidence> poe )
    {
        int score = 0;

        var ourActiveUnits = _ai.Blackboard.OurActiveBattleAIUnits;
        var theirActiveUnits = _ai.Blackboard.TheirActiveBattleAIUnits;

        var ourRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( ourActiveUnits[0].Pokemon ).Where( u => !ourActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();
        var theirRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( theirActiveUnits[0].Pokemon ).Where( u => !theirActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();

        var currentField = _ai.Blackboard.CurrentFieldSnapshot;

        bool theirAllyCanFakeOut = false;
        bool theirAllyHasRedirection = false;
        bool theirAllyHasPivotMove = false;
        bool theirAllyIsIntimidateSupport = false;
        bool theirAllyBlocksPriority = false;

        bool theirAllyIsAppropriateOffense = false;
        bool theirAllyIsAppropriateSpeedTier = false;
        bool theirAllyHasSpreadMoves = false;
        bool theirAllyHasOneLikelyKO = false;
        bool theirAllyHasTwoLikelyKOs = false;
        bool theirAllyHasSpreadKOAndChip = false;
        bool theirAllyHasDoubleSpreadKO = false;

        bool theyHaveAbusersInBack = false;

        IBattleAIUnit theirAlly = null;
        IBattleAIUnit setter = null;
        foreach( var e in poe )
        {
            //--Extract Setter + Ally distinction and information 
            if( TryGetObservationProfile( e, PairObservation.Tailwind, out var tailwind ) )
            {
                if( setter == null )
                    setter = tailwind.Them;
                else
                {
                    if( setter.Pokemon != tailwind.Them.Pokemon )
                    {
                        Debug.LogError( $"You were right, different setters break this structure! Either evaluate both and pick the better scenario, pick the one that is primary intent > secondary intent, or simply pick the first from the list." );
                    }
                }

                if( tailwind.TheyHaveAlly )
                {
                    theirAlly = tailwind.TheirAlly;
                    var theirAllyRP = tailwind.TheirAlly.RoleProfile;
                    theirAllyCanFakeOut = _ai.CanUseFakeOut( tailwind.TheirAlly, tailwind.Us ) || ( tailwind.WeHaveAlly && _ai.CanUseFakeOut( tailwind.TheirAlly, tailwind.OurAlly ) );
                    theirAllyHasRedirection = theirAllyRP.Traits.Contains( RoleTrait.RedirectionMove );
                    theirAllyHasPivotMove = theirAllyRP.Traits.Contains( RoleTrait.PivotMove );

                    theirAllyIsAppropriateOffense = theirAllyRP.PrimaryRole == RoleClass.Sweeper || theirAllyRP.PrimaryRole == RoleClass.SetupSweeper || theirAllyRP.PrimaryRole == RoleClass.BulkyAttacker || theirAllyRP.PrimaryRole == RoleClass.RevengeKiller || theirAllyRP.PrimaryRole == RoleClass.WallBreaker;
                    theirAllyIsAppropriateSpeedTier = theirAllyRP.Biases.Contains( RoleBias.AwkwardSpeed ) || theirAllyRP.Biases.Contains( RoleBias.MiddlingSpeed ) || theirAllyRP.Biases.Contains( RoleBias.FastSpeed );
                    theirAllyHasSpreadMoves = theirAllyRP.Traits.Contains( RoleTrait.SpreadAttack );

                    theirAllyIsIntimidateSupport = tailwind.TheirAlly.Ability == AbilityID.Intimidate || tailwind.TheirAlly.Ability == AbilityID.Demoralize;

                    theirAllyBlocksPriority = theirAllyRP.Traits.Contains( RoleTrait.PriorityBlocker ) || tailwind.TheirAlly.Ability == AbilityID.PsychicSurge && currentField.Terrain == TerrainID.Psychic;
                }
            }
        }

        //--Their Ally checks
        if( theirAlly != null )
        {
            var us = ourActiveUnits[0];
            var ourAlly = ourActiveUnits.Count > 1 ? ourActiveUnits[1] : null;
            var theirAllyComp_Us = _ai.Projection.MakeUnitComparison( theirAlly, us );
            var theirAllyComp_OurAlly = ourAlly != null ? _ai.Projection.MakeUnitComparison( theirAlly, ourAlly ) : default;

            if( theirAllyComp_Us.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous )
                theirAllyHasOneLikelyKO = true;

            if( ourAlly != null )
            {
                if( theirAllyComp_OurAlly.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous )
                    theirAllyHasTwoLikelyKOs = true;

                var bestKOMoveOnUsTarget = theirAllyComp_Us.Attacker.CurrentPTKOs.Keys.First().MoveSO.MoveTarget;
                var bestKOMoveOnUsPTKO = theirAllyComp_Us.Attacker.CurrentPTKOs.Values.First();

                var bestKOMoveOnOurAllyTarget = theirAllyComp_OurAlly.Attacker.CurrentPTKOs.Keys.First().MoveSO.MoveTarget;
                var bestKOMoveOnOurAllyPTKO = theirAllyComp_OurAlly.Attacker.CurrentPTKOs.Values.First();

                bool bestKOMoveIsSpread = ( bestKOMoveOnUsTarget == MoveTarget.AllAdjacent || bestKOMoveOnUsTarget == MoveTarget.OpposingSide ) || ( bestKOMoveOnOurAllyTarget == MoveTarget.AllAdjacent || bestKOMoveOnOurAllyTarget == MoveTarget.OpposingSide );
                if( bestKOMoveIsSpread && bestKOMoveOnUsPTKO >= PotentialToKO.Dangerous && bestKOMoveOnOurAllyPTKO >= PotentialToKO.Dangerous )
                {
                    theirAllyHasDoubleSpreadKO = true;
                }
                else if( bestKOMoveIsSpread && ( bestKOMoveOnUsPTKO >= PotentialToKO.Dangerous || bestKOMoveOnOurAllyPTKO >= PotentialToKO.Dangerous ) )
                {
                    theirAllyHasSpreadKOAndChip = true;
                }
            }
        }

        //--Setter Checks
        bool theirSetterHasFullHPSave = setter.BeginningHPR == 1f && ( setter.Item == ItemBattleEffectID.FocusSash || setter.Ability == AbilityID.Multiscale || setter.Ability == AbilityID.Sturdy || setter.Ability == AbilityID.Disguise );
        bool theirSetterHasPrankster = setter.Ability == AbilityID.Prankster;
        bool theirSetterHasGaleWings = setter.Ability == AbilityID.GaleWings;
        bool theirSetterIsTauntProof = setter.RoleProfile.Traits.Contains( RoleTrait.TauntImmune );
        bool theirSetterIsFlinchProof = setter.Item == ItemBattleEffectID.CovertCloak;

        //--Back line check
        int abusers = 0;
        foreach( var unit in theirRemainingUnits )
        {
            var unitRP = unit.RoleProfile;
            var primary = unitRP.PrimaryRole;
            var biases = unitRP.Biases;

            bool unitIsAppropriateOffense = primary == RoleClass.Sweeper || primary == RoleClass.SetupSweeper || primary == RoleClass.BulkyAttacker || primary == RoleClass.RevengeKiller || primary == RoleClass.WallBreaker;
            bool unitIsAppropriateSpeedTier = biases.Contains( RoleBias.AwkwardSpeed ) || biases.Contains( RoleBias.MiddlingSpeed ) || biases.Contains( RoleBias.FastSpeed );

            if( unitIsAppropriateOffense && unitIsAppropriateSpeedTier )
                abusers++; 
        }

        if( abusers > 0 )
            theyHaveAbusersInBack = true;

        //--Outspeed checks
        int allyCurrentSpeedsBackline = 0;
        int allyCurrentSpeedsActiveLine = 0;
        int allyImprovedSpeedsBackline = 0;
        int allyImprovedSpeedsActiveLine = 0;

        var theirAllyPure = theirAlly != null ? _ai.GetPokemonAs_IBattleAIUnit( theirAlly.Pokemon ) : null;
        foreach( var unit in ourActiveUnits )
        {
            if( theirAllyPure == null )
                break;

            if( theirAllyPure.Speed > unit.Speed )
                allyCurrentSpeedsActiveLine++;

            if( theirAllyPure.Speed * 2 > unit.Speed )
                allyImprovedSpeedsActiveLine++;
        }

        foreach( var unit in ourRemainingUnits )
        {
            if( theirAllyPure == null )
                break;

            if( theirAllyPure.Speed > unit.Speed )
                allyCurrentSpeedsBackline++;

            if( theirAllyPure.Speed * 2 > unit.Speed )
                allyImprovedSpeedsBackline++;
        }

        //--------------------------------------------------------------------
        //--Investment--------------------------------------------------------
        //--------------------------------------------------------------------
        if( theirSetterHasFullHPSave )
            score += 1;

        if( theirSetterHasPrankster || theirSetterHasGaleWings )
            score += 1;

        if( theirSetterIsTauntProof )
            score += 1;

        if( theirSetterIsFlinchProof )
            score += 1;

        if( theirAllyCanFakeOut || theirAllyHasRedirection )
            score += 1;

        if( theirAllyHasPivotMove )
            score += 1;

        if( theirAllyIsIntimidateSupport )
            score += 1;

        if( theirAllyBlocksPriority )
            score += 1;

        if( theyHaveAbusersInBack )
            score += 1;

        //--Partner directly abuses tailwind by being an offensive role + not a slow speed tier
        if( theirAllyIsAppropriateSpeedTier )
        {
            score += 1;

            if( theirAllyIsAppropriateOffense )
                score += 1;

            if( theirAllyHasSpreadMoves )
                score += 1;

            if( theirAllyHasDoubleSpreadKO )
            {
                score += 3;
            }
            else if( theirAllyHasSpreadKOAndChip )
            {
                score += 2;
            }
            else if( theirAllyHasTwoLikelyKOs || theirAllyHasOneLikelyKO )
            {
                score += 1;
            }
        }
        else //--contradicted by trick room speed presence
        {
            score -= 2;
        }

        //--------------------------------------------------------------------
        //--Line Lock (small)-------------------------------------------------
        //--------------------------------------------------------------------
        var setterSP = _ai.Projection.GetStrategicProfile( setter );
        bool setterContributesAfter = setterSP.BlocksPriority || setterSP.HelpingHandSupport || setterSP.ProvidesStrongOffense || setterSP.ProvidesSupport;

        if( setterContributesAfter && theirAllyIsAppropriateOffense )
            score += 1;
        
        if( abusers >= theirRemainingUnits.Count - 1 )
            score += 1;

        //--------------------------------------------------------------------
        //--Irreversibility (small)-------------------------------------------
        //--------------------------------------------------------------------
        if( theirAllyIsAppropriateOffense && theirAllyIsAppropriateSpeedTier && ( setterContributesAfter || abusers > 1 ) )
            score += 1;

        //--------------------------------------------------------------------
        //--Opportunity Cost--------------------------------------------------
        //--------------------------------------------------------------------
        bool weCanMatchTailwind = false;
        bool setterIsHighlyThreatened = false;
        bool currentTurnHasSetupWindow = false;
        bool waitingLosesSpeedAdvantage = false;
        bool tailwindCreatesImmediateOffense = false;

        foreach( var unit in ourActiveUnits )
        {
            if( unit.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter ) )
                weCanMatchTailwind = true;
        }

        var ourLeftPTKO_Setter = _ai.Projection.MakeUnitComparison( ourActiveUnits[0], setter ).Attacker.BestCurrentPTKO;
        var ourRightPTKO_Setter = ourActiveUnits.Count > 1 ? _ai.Projection.MakeUnitComparison( ourActiveUnits[1], setter ).Attacker.BestCurrentPTKO : default;

        if( ourLeftPTKO_Setter > PotentialToKO.Dangerous || ourRightPTKO_Setter >= PotentialToKO.Dangerous || ( setter.Item == ItemBattleEffectID.FocusSash && !theirAllyHasRedirection ) )
            setterIsHighlyThreatened = true;

        if( theirAllyBlocksPriority || theirAllyCanFakeOut || theirAllyHasRedirection )
            currentTurnHasSetupWindow = true;

        int allyTotalSpeedGains = ( allyImprovedSpeedsActiveLine - allyCurrentSpeedsActiveLine ) + ( allyImprovedSpeedsBackline - allyCurrentSpeedsBackline );
        if( allyCurrentSpeedsActiveLine < 2 && allyImprovedSpeedsActiveLine > 1 || allyTotalSpeedGains > 2 || weCanMatchTailwind )
            waitingLosesSpeedAdvantage = true;

        if( allyImprovedSpeedsActiveLine == 2 && theirAllyHasOneLikelyKO )
            tailwindCreatesImmediateOffense = true;

        if( setterIsHighlyThreatened )
            score += 1;

        if( currentTurnHasSetupWindow )
            score += 1;

        if( waitingLosesSpeedAdvantage )
            score += 1;

        if( tailwindCreatesImmediateOffense )
            score += 1;

        return score;
    }

    private int ScoreWeatherPivotCommitment( List<PairObservationEvidence> poe )
    {
        int score = 0;

        //--Blackboard Information
        var ourActiveUnits = _ai.Blackboard.OurActiveBattleAIUnits;
        var theirActiveUnits = _ai.Blackboard.TheirActiveBattleAIUnits;

        var ourRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( ourActiveUnits[0].Pokemon ).Where( u => !ourActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();
        var theirRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( theirActiveUnits[0].Pokemon ).Where( u => !theirActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();

        var currentField = _ai.Blackboard.CurrentFieldSnapshot;

        //--Initialize bools for scoring
        bool theyCanChangeWeather = false;
        bool weBenefitCurrentWeather = false;
        bool theyBenefitIncomingWeather = false;
        bool theyBenefitMoreThanUsFromIncomingWeather = false;

        bool theirAllyAbusesIncomingWeather = false;
        bool theirSetterAbusesIncomingWeather = false;

        bool theirSwitchCandidateIsSafe = false;

        bool weatherChangeGivesTheirAllyOneKO = false;
        bool weatherChangeImprovesTheirAllyPTKOs = false;
        int theirAllysImprovedOutspeeds = 0;

        bool incomingWeatherHindersUs = false;
        bool weRelyOnCurrentWeather = false;

        //--Gather Unit & Evidence References
        PairObservationProfile switchEvidence = default;
        IBattleAIUnit switchCandidate = null;
        IBattleAIUnit theirAlly = null;

        IBattleAIUnit us = null;
        IBattleAIUnit ourAlly = null;

        foreach( var e in poe )
        {
            if( TryGetObservationProfile( e, PairObservation.Switch, out switchEvidence ) )
            {
                if( switchCandidate == null )
                {
                    switchCandidate = switchEvidence.Them;
                }
                else if( switchCandidate.Pokemon != switchEvidence.Them.Pokemon )
                {
                    Debug.LogError( $"that extra bullshit because of the loop" );
                }

                us = switchEvidence.Us;

                if( switchEvidence.TheyHaveAlly )
                {
                    theirAlly = switchEvidence.TheirAlly;
                }

                if( switchEvidence.WeHaveAlly )
                {
                    ourAlly = switchEvidence.OurAlly;
                }
            }
        }

        weBenefitCurrentWeather = _ai.UnitSim.Get_WeatherContextScore( us.Pokemon, currentField.Weather ) > 0 || ourAlly != null && _ai.UnitSim.Get_WeatherContextScore( ourAlly.Pokemon, currentField.Weather ) > 0;
        var candidatesWeather = _ai.UnitSim.GetWeatherFrom_Ability( switchCandidate.Pokemon );

        theyCanChangeWeather = candidatesWeather != currentField.Weather;
        if( !theyCanChangeWeather )
            return -5;

        int ourIncomingWeatherScore = 0;
        ourIncomingWeatherScore += _ai.UnitSim.Get_WeatherContextScore( us.Pokemon, candidatesWeather );
        ourIncomingWeatherScore += ourAlly != null ? _ai.UnitSim.Get_WeatherContextScore( ourAlly.Pokemon, candidatesWeather ) : 0;

        int theirIncomingWeatherScore = 0;
        theirIncomingWeatherScore = theirAlly != null ? _ai.UnitSim.Get_WeatherContextScore( theirAlly.Pokemon, candidatesWeather ) : 0;
        theirIncomingWeatherScore = _ai.UnitSim.Get_WeatherContextScore( switchCandidate.Pokemon, candidatesWeather );

        theyBenefitIncomingWeather = theirIncomingWeatherScore > 0;
        theyBenefitMoreThanUsFromIncomingWeather = theirIncomingWeatherScore - ourIncomingWeatherScore > 0;

        var ourSP = _ai.Projection.GetStrategicProfile( us );
        var theirSP = _ai.Projection.GetStrategicProfile( switchCandidate );
        var ourAllySP = ourAlly != null ? _ai.Projection.GetStrategicProfile( ourAlly ) : default;
        var theirAllySP = theirAlly != null ? _ai.Projection.GetStrategicProfile( theirAlly ) : default;

        theirAllyAbusesIncomingWeather = theirAlly != null && ( _ai.UnitSim.PokemonAbilityMatchesWeather( theirAlly.Pokemon, candidatesWeather ) || _ai.UnitSim.PokemonHasMove_AbusesWeather( theirAlly.Pokemon, candidatesWeather ) );
        theirSetterAbusesIncomingWeather = _ai.UnitSim.PokemonHasMove_AbusesWeather( switchCandidate.Pokemon, candidatesWeather );
        
        theirSwitchCandidateIsSafe = switchEvidence.Top.OpponentPTKO <= PotentialToKO.Risky;

        int theirAllyTotalPTKOImprovement = 0;
        if( theirAlly != null )
        {
            var theirAllyPTKO_Us_CurrentWeather = _ai.GetMove_StrongestAttack( theirAlly, us ).PTKO;
            var theirAllyPTKO_Us_IncomingWeather = _ai.GetMove_StrongestAttack( theirAlly, us, switchEvidence.Top.Field ).PTKO;

            if( theirAllyPTKO_Us_CurrentWeather <= PotentialToKO.Risky && theirAllyPTKO_Us_IncomingWeather >= PotentialToKO.Dangerous )
                weatherChangeGivesTheirAllyOneKO = true;
            
            int theirAllyPTKO_Us_Improvement = (int)theirAllyPTKO_Us_IncomingWeather - (int)theirAllyPTKO_Us_CurrentWeather;
            int theirAllyPTKO_OurAlly_Improvement = 0;

            if( ourAlly != null )
            {
                var theirAllyPTKO_OurAlly_CurrentWeather = _ai.GetMove_StrongestAttack( theirAlly, ourAlly ).PTKO;
                var theirAllyPTKO_OurAlly_IncomingWeather = _ai.GetMove_StrongestAttack( theirAlly, ourAlly, switchEvidence.Top.Field ).PTKO;

                theirAllyPTKO_OurAlly_Improvement = (int)theirAllyPTKO_OurAlly_IncomingWeather - (int)theirAllyPTKO_OurAlly_CurrentWeather;

                if( theirAllyPTKO_OurAlly_CurrentWeather <= PotentialToKO.Risky && theirAllyPTKO_OurAlly_IncomingWeather >= PotentialToKO.Dangerous )
                    weatherChangeGivesTheirAllyOneKO = true;
            }

            theirAllyTotalPTKOImprovement = theirAllyPTKO_OurAlly_Improvement + theirAllyPTKO_OurAlly_Improvement;

            if( _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( theirAlly.Pokemon, candidatesWeather ) )
            {
                int currentOutSpeeds = 0;
                int afterOutSpeeds = 0;
                var theirAllyBase = _ai.GetPokemonAs_Adapter( theirAlly.Pokemon );
                foreach( var unit in ourActiveUnits )
                {
                    if( theirAllyBase.Speed > unit.Speed )
                        currentOutSpeeds++;

                    if( theirAllyBase.Speed * 2 > unit.Speed )
                        afterOutSpeeds++;
                }

                theirAllysImprovedOutspeeds = afterOutSpeeds - currentOutSpeeds;
            }
        }
        
        weatherChangeImprovesTheirAllyPTKOs = theirAllyTotalPTKOImprovement > 0;

        incomingWeatherHindersUs = _ai.UnitSim.Get_WeatherContextScore( us.Pokemon, candidatesWeather ) < 0 || ourAlly != null && _ai.UnitSim.Get_WeatherContextScore( ourAlly.Pokemon, candidatesWeather ) < 0;
        weRelyOnCurrentWeather = ourSP.DependsOnCurrentWeather || ourAlly != null && ourAllySP.DependsOnCurrentWeather;

        //--------------------------------------------------------------------
        //--Investment--------------------------------------------------------
        //--------------------------------------------------------------------
        if( theyCanChangeWeather )
            score += 1;

        if( theirSwitchCandidateIsSafe )
            score += 1;

        if( theyBenefitIncomingWeather )
            score += 1;

        if( theyBenefitMoreThanUsFromIncomingWeather )
            score += 1;

        if( theirAllyAbusesIncomingWeather )
            score += 1;

        if( theirSetterAbusesIncomingWeather )
            score += 1;

        if( weatherChangeGivesTheirAllyOneKO )
            score += 1;

        if( weatherChangeImprovesTheirAllyPTKOs )
            score += 1;

        if( theirAllysImprovedOutspeeds > 0 )
            score += 1;

        if( incomingWeatherHindersUs )
            score += 1;

        if( weRelyOnCurrentWeather )
            score += 1;

        //--------------------------------------------------------------------
        //--Line Lock---------------------------------------------------------
        //--------------------------------------------------------------------
        //--Incoming weather is strategic necessity
        bool incomingWeatherIsBoon = theyCanChangeWeather && theyBenefitIncomingWeather && theirAllyAbusesIncomingWeather && theirSetterAbusesIncomingWeather;

        //--Incoming weather has multiple abusers remaining on bench
        int weatherAbusersInBack = 0;

        //--Tailwind and screens don't currently exist or make sense
        int alternateSupportModes = 0;

        foreach( var unit in theirRemainingUnits )
        {
            if( _ai.UnitSim.PokemonAbilityMatchesWeather( unit.Pokemon, candidatesWeather ) || _ai.UnitSim.PokemonHasMove_AbusesWeather( unit.Pokemon, candidatesWeather ) )
                weatherAbusersInBack++;

            if( unit.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter ) )
                alternateSupportModes++;

            if( unit.RoleProfile.Traits.Contains( RoleTrait.ScreenSetter ) )
                alternateSupportModes++;
        }

        //--Incoming weather obliterates ai's current board
        bool incomingWeatherObliteratesUs = weRelyOnCurrentWeather && incomingWeatherHindersUs && weatherChangeGivesTheirAllyOneKO && theirAllysImprovedOutspeeds > 1;

        if( incomingWeatherIsBoon )
            score += 1;

        if( weatherAbusersInBack > theirRemainingUnits.Count - 1 )
            score += 1;

        if( alternateSupportModes == 0 )
            score += 2;
        else if( alternateSupportModes < 2 )
            score += 1;

        if( incomingWeatherObliteratesUs )
            score += 1;

        if( switchEvidence.Top.OpponentPTKO <= PotentialToKO.Safe )
            score += 1;

        //--------------------------------------------------------------------
        //--Irreversibility (small)-------------------------------------------
        //--------------------------------------------------------------------
        if( weatherAbusersInBack >= theirRemainingUnits.Count && alternateSupportModes == 0 )
            score += 1;

        //--------------------------------------------------------------------
        //--Opportunity Cost--------------------------------------------------
        //--------------------------------------------------------------------
        bool currentWeatherHurtsThem = false;
        bool currentTurnHasGoodWindow = false;
        bool delayingWeatherDelaysPressure = false;
        bool everyTurnTheyDelayBenefitsUs = false;

        int theirCurrentWeatherScore = _ai.UnitSim.Get_WeatherContextScore( theirActiveUnits[0].Pokemon, currentField.Weather ) + theirActiveUnits.Count > 1 ? _ai.UnitSim.Get_WeatherContextScore( theirActiveUnits[1].Pokemon, currentField.Weather ) : 0;
        bool theirAllyCanFakeOut = ( theirAlly != null && _ai.CanUseFakeOut( theirAlly, us ) ) || ( theirAlly != null && ourAlly != null && _ai.CanUseFakeOut( theirAlly, ourAlly ) );
        bool theirAllyHasRedirection = theirAlly != null && theirAlly.RoleProfile.Traits.Contains( RoleTrait.RedirectionMove );
        bool theirAllyBlocksPriority = theirAlly != null && theirAlly.RoleProfile.Traits.Contains( RoleTrait.PriorityBlocker );
        bool theyBlockPriority = theirActiveUnits[0].RoleProfile.Traits.Contains( RoleTrait.PriorityBlocker );

        currentWeatherHurtsThem = theirCurrentWeatherScore < 0;

        if( theirSwitchCandidateIsSafe && ( theirAllyCanFakeOut || theirAllyHasRedirection || theirAllyBlocksPriority || theyBlockPriority ) )
            currentTurnHasGoodWindow = true;

        if( weatherChangeImprovesTheirAllyPTKOs || weatherChangeGivesTheirAllyOneKO || theirAllysImprovedOutspeeds > 0 )
            delayingWeatherDelaysPressure = true;

        if( theyCanChangeWeather && ( weBenefitCurrentWeather || weRelyOnCurrentWeather ) )
            everyTurnTheyDelayBenefitsUs = true;

        if( currentWeatherHurtsThem )
            score += 1;

        if( currentTurnHasGoodWindow )
            score += 1;

        if( delayingWeatherDelaysPressure )
            score += 1;

        if( everyTurnTheyDelayBenefitsUs )
            score += 1;

        return score;
    }

    private int ScoreImmediateKOCommitment( List<PairObservationEvidence> poe )
    {
        int score = 0;

        //--Blackboard Information
        var ourActiveUnits = _ai.Blackboard.OurActiveBattleAIUnits;
        var theirActiveUnits = _ai.Blackboard.TheirActiveBattleAIUnits;

        var ourRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( ourActiveUnits[0].Pokemon ).Where( u => !ourActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();
        var theirRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( theirActiveUnits[0].Pokemon ).Where( u => !theirActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();

        var currentField = _ai.Blackboard.CurrentFieldSnapshot;
        var gp = _ai.Blackboard.GamePlan;

        //--Immediate KO requires focus fire and double attack.
        //--Knowing this, we can skip extracing observation evidence and just make direct checks from the blackboard

        IBattleAIUnit ourLeft = ourActiveUnits[0];
        IBattleAIUnit ourRight = ourActiveUnits.Count > 1 ? ourActiveUnits[1] : null;

        IBattleAIUnit theirLeft = theirActiveUnits[0];
        IBattleAIUnit theirRight = theirActiveUnits.Count > 1 ? theirActiveUnits[1] : null;

        bool weHaveRight = ourRight != null;
        bool theyHaveRight = theirRight != null;

        //--Strategy Profiles
        var ourLeftSP = _ai.Projection.GetStrategicProfile( ourLeft );
        var ourRightSP = weHaveRight ? _ai.Projection.GetStrategicProfile( ourRight ) : default;

        var theirLeftSP = _ai.Projection.GetStrategicProfile( theirLeft );
        var theirRightSP = theyHaveRight ? _ai.Projection.GetStrategicProfile( theirRight ) : default;

        var theirCourt = theirLeft.CourtLocation == CourtLocation.TopCourt ? currentField.TopCourtConditions : currentField.BottomCourtConditions;

        //--Unit comparisons
        var theirLeftComp_OurLeft = _ai.Projection.MakeUnitComparison( theirLeft, ourLeft );
        var theirLeftComp_OurRight = weHaveRight ? _ai.Projection.MakeUnitComparison( theirLeft, ourRight ) : default;
        var theirRightComp_OurLeft = theyHaveRight ? _ai.Projection.MakeUnitComparison( theirRight, ourLeft ) : default;
        var theirRightComp_OurRight = theyHaveRight && weHaveRight ? _ai.Projection.MakeUnitComparison( theirRight, ourRight ) : default;
        
        //--PTKO Gathering
        var theirLeftPTKO_OurLeft = theirLeftComp_OurLeft.Attacker.BestCurrentPTKO;
        var theirLeftPTKO_OurRight = theirLeftComp_OurRight.Attacker.BestCurrentPTKO;
        var theirRightPTKO_OurLeft = theirRightComp_OurLeft.Attacker.BestCurrentPTKO;
        var theirRightPTKO_OurRight = theirRightComp_OurRight.Attacker.BestCurrentPTKO;

        var ourLeftPTKO_TheirLeft = theirLeftComp_OurLeft.Target.BestCurrentPTKO;
        var ourLeftPTKO_TheirRight = theirLeftComp_OurRight.Target.BestCurrentPTKO;
        var ourRightPTKO_TheirLeft = theirRightComp_OurLeft.Target.BestCurrentPTKO;
        var ourRightPTKO_TheirRight = theirRightComp_OurRight.Target.BestCurrentPTKO;

        //--Move References
        var theirLeftMove_Left = theirLeftComp_OurLeft.Attacker.CurrentPTKOs.Keys.First();
        var theirLeftMove_Right = theirLeftComp_OurRight.Attacker.CurrentPTKOs.Keys.First();
        var theirRightMove_Left = theirRightComp_OurLeft.Attacker.CurrentPTKOs.Keys.First();
        var theirRightMove_Right = theirRightComp_OurRight.Attacker.CurrentPTKOs.Keys.First();

        var ourLeftMove_Left = theirLeftComp_OurLeft.Target.CurrentPTKOs.Keys.First();
        var ourLeftMove_Right = theirLeftComp_OurRight.Target.CurrentPTKOs.Keys.First();
        var ourRightMove_Left = theirRightComp_OurLeft.Target.CurrentPTKOs.Keys.First();
        var ourRightMove_Right = theirRightComp_OurRight.Target.CurrentPTKOs.Keys.First();

        //--Speed + Priority Determinations
        bool theirLeftFaster_Left = _ai.UnitSim.AttackerMovesFirst( theirLeft, ourLeft, theirLeftMove_Left, ourLeftMove_Left );
        bool theirLeftFaster_Right = _ai.UnitSim.AttackerMovesFirst( theirLeft, ourRight, theirLeftMove_Left, ourRightMove_Left );
        bool theirRightFaster_Left = _ai.UnitSim.AttackerMovesFirst( theirRight, ourLeft, theirRightMove_Left, ourLeftMove_Left );
        bool theirRightFaster_Right = _ai.UnitSim.AttackerMovesFirst( theirRight, ourRight, theirRightMove_Left, ourRightMove_Left );

        //--Our Unit Importance
        bool ourLeftIsGameplanUnit = gp.OurPrimaryWinCon == ourLeft.Pokemon || gp.OurBlockers.Contains( ourLeft.Pokemon );
        bool ourRightIsGameplanUnit = gp.OurPrimaryWinCon == ourRight.Pokemon || gp.OurBlockers.Contains( ourRight.Pokemon );

        bool ourLeftIsStrategicallyImportant = ourLeftSP.BattlefieldFlipper || ourLeftSP.BattlefieldSetter || ourLeftSP.BlocksBattlefieldSetup || ourLeftSP.BlocksPriority || ourLeftSP.BlocksSpeedControl || ourLeftSP.BlocksWeatherControl || ourRightSP.DependsOnFastAlly;
        bool ourRightIsStrategicallyImportant = ourRightSP.BattlefieldFlipper || ourRightSP.BattlefieldSetter || ourRightSP.BlocksBattlefieldSetup || ourRightSP.BlocksPriority || ourRightSP.BlocksSpeedControl || ourRightSP.BlocksWeatherControl || ourLeftSP.DependsOnFastAlly;

        bool ourLeftIsImportant = ourLeftIsGameplanUnit || ourLeftIsStrategicallyImportant;
        bool ourRightIsImportant = ourRightIsGameplanUnit || ourRightIsStrategicallyImportant;

        //--Immediate KO Checks
        if( ourLeftIsImportant )
        {
            if( theirLeftPTKO_OurLeft >= PotentialToKO.Dangerous && ( theirLeftFaster_Left || ourLeftPTKO_TheirLeft <= PotentialToKO.Risky ) )
            {
                score += 1;
                
                if( theirLeftPTKO_OurLeft >= PotentialToKO.OHKO )
                    score += 1;
            }

            if( theirRightPTKO_OurLeft >= PotentialToKO.Dangerous && ( theirRightFaster_Left || ourLeftPTKO_TheirRight <= PotentialToKO.Risky ) )
            {
                score += 1;
                
                if( theirRightPTKO_OurLeft >= PotentialToKO.OHKO )
                    score += 1;
            }
        }

        if( ourRightIsImportant )
        {
            if( theirLeftPTKO_OurRight >= PotentialToKO.Dangerous && ( theirLeftFaster_Right || ourRightPTKO_TheirLeft <= PotentialToKO.Risky ) )
            {
                score += 1;
                
                if( theirLeftPTKO_OurRight >= PotentialToKO.OHKO )
                    score += 1;
            }

            if( theirRightPTKO_OurRight >= PotentialToKO.Dangerous && ( theirRightFaster_Right || ourRightPTKO_TheirRight <= PotentialToKO.Risky ) )
            {
                score += 1;
                
                if( theirRightPTKO_OurRight >= PotentialToKO.OHKO )
                    score += 1;
            }
        }

        //--Team Strategy Invested Already Checks (Ex: Tailwind is already up, so they obviously now want to attack more)
        bool theyHaveTailwind = theirCourt.ContainsKey( CourtConditionID.Tailwind );
        bool theyHaveScreens = theirCourt.ContainsKey( CourtConditionID.Reflect ) || theirCourt.ContainsKey( CourtConditionID.LightScreen ) || theirCourt.ContainsKey( CourtConditionID.AuroraVeil );
        bool theyBenefitWeather = theirLeftSP.DependsOnCurrentWeather || theyHaveRight && theirRightSP.DependsOnCurrentWeather || _ai.UnitSim.Get_WeatherContextScore( theirLeft.Pokemon ) > 0 || theyHaveRight && _ai.UnitSim.Get_WeatherContextScore( theirRight.Pokemon ) > 0;
        bool theyBenefitTerrain = theirLeftSP.DependsOnCurrentTerrain || theyHaveRight && theirRightSP.DependsOnCurrentTerrain || _ai.UnitSim.Get_TerrainContextScore( theirLeft.Pokemon ) > 0 || theyHaveRight && _ai.UnitSim.Get_TerrainContextScore( theirRight.Pokemon ) > 0;
        bool theyBenefitTrickRoom = theirLeftSP.DependsOnTrickRoom || theyHaveRight && theirRightSP.DependsOnTrickRoom || _ai.UnitSim.Get_TrickRoomContextScore( theirLeft.Pokemon ) > 0 || theyHaveRight && _ai.UnitSim.Get_TrickRoomContextScore( theirRight.Pokemon ) > 0;

        bool theirLeftIsOffensiveSupport = theirLeftSP.Redirector || theirLeftSP.HelpingHandSupport;
        bool theirRightIsOffensiveSupport = theirRightSP.Redirector || theirRightSP.HelpingHandSupport;

        //--------------------------------------------------------------------
        //--Investment--------------------------------------------------------
        //--------------------------------------------------------------------
        if( theyHaveTailwind )
            score += 1;

        if( theyHaveScreens )
            score += 1;

        if( theyBenefitWeather )
            score += 1;

        if( theyBenefitTerrain )
            score += 1;

        if( theyBenefitTrickRoom )
            score += 1;

        if( theirLeftIsOffensiveSupport && ( theirLeftPTKO_OurLeft >= PotentialToKO.Dangerous || theirLeftPTKO_OurRight >= PotentialToKO.Dangerous ) )
            score += 1;

        if( theirRightIsOffensiveSupport && ( theirRightPTKO_OurLeft >= PotentialToKO.Dangerous || theirRightPTKO_OurRight >= PotentialToKO.Dangerous ) )
            score += 1;

        //--------------------------------------------------------------------
        //--Line Lock (small)-------------------------------------------------
        //--------------------------------------------------------------------
        bool theyHaveNoBoardEffects = !theyHaveTailwind && !theyHaveScreens && !theyBenefitWeather && !theyBenefitTerrain && theyBenefitTrickRoom;
        bool theirLeftIsSetter = theirLeftSP.BattlefieldSetter || theirLeftSP.BattlefieldFlipper;
        bool theirRightIsSetter = theirRightSP.BattlefieldSetter || theirRightSP.BattlefieldFlipper;
        bool theyHaveASetter = theirLeftIsSetter || theirRightIsSetter;

        bool ourLeftIsSetter = ourLeftSP.BattlefieldSetter || ourLeftSP.BattlefieldFlipper;
        bool ourRightIsSetter = weHaveRight && ( ourRightSP.BattlefieldSetter || ourRightSP.BattlefieldFlipper );

        bool koingLeftLetsThemSet = ourLeftIsSetter && theyHaveASetter && ( theirLeftPTKO_OurLeft >= PotentialToKO.Dangerous || theirRightPTKO_OurLeft >= PotentialToKO.Dangerous );
        bool koingRightLetsThemSet = ourRightIsSetter && theyHaveASetter && ( theirLeftPTKO_OurRight >= PotentialToKO.Dangerous || theirRightPTKO_OurRight >= PotentialToKO.Dangerous );

        if( !theyHaveNoBoardEffects && !theyHaveASetter )
            score += 1;

        if( !theyHaveNoBoardEffects && ( koingLeftLetsThemSet || koingRightLetsThemSet ) )
            score += 1;

        //--------------------------------------------------------------------
        //--Opportunity Cost--------------------------------------------------
        //--------------------------------------------------------------------
        bool targetBecomesDifficultToRemoveLater = false;
        bool waitingLosesKOWindow = false;
        bool koingSetterPreventsSetup = false;

        bool ourLeftGetsHarderToKO = ourLeft.RoleProfile.Traits.Contains( RoleTrait.ScreenSetter ) || ourLeft.RoleProfile.Traits.Contains( RoleTrait.SpeciallyDefensiveSetup ) || ourLeft.RoleProfile.Traits.Contains( RoleTrait.PhysicallyDefensiveSetup );
        bool ourRightGetsHarderToKO = ourRight.RoleProfile.Traits.Contains( RoleTrait.ScreenSetter ) || ourRight.RoleProfile.Traits.Contains( RoleTrait.SpeciallyDefensiveSetup ) || ourRight.RoleProfile.Traits.Contains( RoleTrait.PhysicallyDefensiveSetup );
        
        bool ourLeftCanGetAway = ourLeft.RoleProfile.Traits.Contains( RoleTrait.PivotMove );
        bool ourRightCanGetAway = ourRight.RoleProfile.Traits.Contains( RoleTrait.PivotMove );

        bool theyHaveKOLeft = theirLeftPTKO_OurLeft >= PotentialToKO.Dangerous || theyHaveRight && theirRightPTKO_OurLeft >= PotentialToKO.Dangerous;
        bool theyHaveKORight = weHaveRight && theirLeftPTKO_OurRight >= PotentialToKO.Dangerous || weHaveRight && theyHaveRight && theirRightPTKO_OurRight >= PotentialToKO.Dangerous;

        targetBecomesDifficultToRemoveLater = theyHaveKOLeft && ourLeftGetsHarderToKO || theyHaveKORight && ourRightGetsHarderToKO;
        waitingLosesKOWindow = theyHaveKOLeft && ourLeftCanGetAway || theyHaveKORight && ourRightCanGetAway || theyHaveKOLeft && koingLeftLetsThemSet || theyHaveKORight && koingRightLetsThemSet;
        koingSetterPreventsSetup = theyHaveKOLeft && ourLeftIsSetter || theyHaveKORight && ourRightIsSetter;

        if( targetBecomesDifficultToRemoveLater )
            score += 1;

        if( waitingLosesKOWindow )
            score += 1;

        if( koingSetterPreventsSetup )
            score += 1;

        return score;
    }

    private int ScoreApplyBoardPressureCommitment( List<PairObservationEvidence> poe )
    {
        int score = 0;

        //--Blackboard Information
        var ourActiveUnits = _ai.Blackboard.OurActiveBattleAIUnits;
        var theirActiveUnits = _ai.Blackboard.TheirActiveBattleAIUnits;

        var ourRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( ourActiveUnits[0].Pokemon ).Where( u => !ourActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();
        var theirRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( theirActiveUnits[0].Pokemon ).Where( u => !theirActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();

        var currentField = _ai.Blackboard.CurrentFieldSnapshot;

        var gp = _ai.Blackboard.GamePlan;

        //--Apply Board Pressure requires focus fire OR double attack OR an attack observation, unlike ImmediateKO which requires all of them.
        //--Knowing this, we can skip extracing observation evidence and just make direct checks from the blackboard

        IBattleAIUnit ourLeft = ourActiveUnits[0];
        IBattleAIUnit ourRight = ourActiveUnits.Count > 1 ? ourActiveUnits[1] : null;

        IBattleAIUnit theirLeft = theirActiveUnits[0];
        IBattleAIUnit theirRight = theirActiveUnits.Count > 1 ? theirActiveUnits[1] : null;

        bool weHaveRight = ourRight != null;
        bool theyHaveRight = theirRight != null;

        //--Strategy Profiles
        var ourLeftSP = _ai.Projection.GetStrategicProfile( ourLeft );
        var ourRightSP = weHaveRight ? _ai.Projection.GetStrategicProfile( ourRight ) : default;

        var theirLeftSP = _ai.Projection.GetStrategicProfile( theirLeft );
        var theirRightSP = theyHaveRight ? _ai.Projection.GetStrategicProfile( theirRight ) : default;

        var theirCourt = theirLeft.CourtLocation == CourtLocation.TopCourt ? currentField.TopCourtConditions : currentField.BottomCourtConditions;

        //--Unit comparisons
        var theirLeftComp_OurLeft = _ai.Projection.MakeUnitComparison( theirLeft, ourLeft );
        var theirLeftComp_OurRight = weHaveRight ? _ai.Projection.MakeUnitComparison( theirLeft, ourRight ) : default;
        var theirRightComp_OurLeft = theyHaveRight ? _ai.Projection.MakeUnitComparison( theirRight, ourLeft ) : default;
        var theirRightComp_OurRight = theyHaveRight && weHaveRight ? _ai.Projection.MakeUnitComparison( theirRight, ourRight ) : default;
        
        //--PTKO Gathering
        var theirLeftPTKO_OurLeft = theirLeftComp_OurLeft.Attacker.BestCurrentPTKO;
        var theirLeftPTKO_OurRight = theirLeftComp_OurRight.Attacker.BestCurrentPTKO;
        var theirRightPTKO_OurLeft = theirRightComp_OurLeft.Attacker.BestCurrentPTKO;
        var theirRightPTKO_OurRight = theirRightComp_OurRight.Attacker.BestCurrentPTKO;

        var ourLeftPTKO_TheirLeft = theirLeftComp_OurLeft.Target.BestCurrentPTKO;
        var ourLeftPTKO_TheirRight = theirLeftComp_OurRight.Target.BestCurrentPTKO;
        var ourRightPTKO_TheirLeft = theirRightComp_OurLeft.Target.BestCurrentPTKO;
        var ourRightPTKO_TheirRight = theirRightComp_OurRight.Target.BestCurrentPTKO;

        //--Move References
        var theirLeftMove_Left = theirLeftComp_OurLeft.Attacker.CurrentPTKOs.Keys.First();
        var theirLeftMove_Right = theirLeftComp_OurRight.Attacker.CurrentPTKOs.Keys.First();
        var theirRightMove_Left = theirRightComp_OurLeft.Attacker.CurrentPTKOs.Keys.First();
        var theirRightMove_Right = theirRightComp_OurRight.Attacker.CurrentPTKOs.Keys.First();

        var ourLeftMove_Left = theirLeftComp_OurLeft.Target.CurrentPTKOs.Keys.First();
        var ourLeftMove_Right = theirLeftComp_OurRight.Target.CurrentPTKOs.Keys.First();
        var ourRightMove_Left = theirRightComp_OurLeft.Target.CurrentPTKOs.Keys.First();
        var ourRightMove_Right = theirRightComp_OurRight.Target.CurrentPTKOs.Keys.First();

        //--Speed + Priority Determinations
        bool theirLeftFaster_Left = _ai.UnitSim.AttackerMovesFirst( theirLeft, ourLeft, theirLeftMove_Left, ourLeftMove_Left );
        bool theirLeftFaster_Right = _ai.UnitSim.AttackerMovesFirst( theirLeft, ourRight, theirLeftMove_Left, ourRightMove_Left );
        bool theirRightFaster_Left = _ai.UnitSim.AttackerMovesFirst( theirRight, ourLeft, theirRightMove_Left, ourLeftMove_Left );
        bool theirRightFaster_Right = _ai.UnitSim.AttackerMovesFirst( theirRight, ourRight, theirRightMove_Left, ourRightMove_Left );

        //--Immediate KO Checks
        if( theirLeftPTKO_OurLeft >= PotentialToKO.Risky )
        {
            score += 1;
            
            if( theirLeftPTKO_OurLeft >= PotentialToKO.OHKO && ( theirLeftFaster_Left || ourLeftPTKO_TheirLeft <= PotentialToKO.Risky ) )
                score += 1;
        }

        if( theirRightPTKO_OurLeft >= PotentialToKO.Risky )
        {
            score += 1;
            
            if( theirRightPTKO_OurLeft >= PotentialToKO.OHKO && ( theirRightFaster_Left || ourLeftPTKO_TheirRight <= PotentialToKO.Risky ) )
                score += 1;
        }

        if( theirLeftPTKO_OurRight >= PotentialToKO.Risky )
        {
            score += 1;
            
            if( theirLeftPTKO_OurRight >= PotentialToKO.OHKO && ( theirLeftFaster_Right || ourRightPTKO_TheirLeft <= PotentialToKO.Risky ) )
                score += 1;
        }

        if( theirRightPTKO_OurRight >= PotentialToKO.Risky )
        {
            score += 1;
            
            if( theirRightPTKO_OurRight >= PotentialToKO.OHKO && ( theirRightFaster_Right || ourRightPTKO_TheirRight <= PotentialToKO.Risky ) )
                score += 1;
        }

        //--Team Strategy Invested Already Checks (Ex: Tailwind is already up, so they obviously now want to attack more)
        bool theyHaveTailwind = theirCourt.ContainsKey( CourtConditionID.Tailwind );
        bool theyHaveScreens = theirCourt.ContainsKey( CourtConditionID.Reflect ) || theirCourt.ContainsKey( CourtConditionID.LightScreen ) || theirCourt.ContainsKey( CourtConditionID.AuroraVeil );
        bool theyBenefitWeather = theirLeftSP.DependsOnCurrentWeather || theyHaveRight && theirRightSP.DependsOnCurrentWeather || _ai.UnitSim.Get_WeatherContextScore( theirLeft.Pokemon ) > 0 || theyHaveRight && _ai.UnitSim.Get_WeatherContextScore( theirRight.Pokemon ) > 0;
        bool theyBenefitTerrain = theirLeftSP.DependsOnCurrentTerrain || theyHaveRight && theirRightSP.DependsOnCurrentTerrain || _ai.UnitSim.Get_TerrainContextScore( theirLeft.Pokemon ) > 0 || theyHaveRight && _ai.UnitSim.Get_TerrainContextScore( theirRight.Pokemon ) > 0;
        bool theyBenefitTrickRoom = theirLeftSP.DependsOnTrickRoom || theyHaveRight && theirRightSP.DependsOnTrickRoom || _ai.UnitSim.Get_TrickRoomContextScore( theirLeft.Pokemon ) > 0 || theyHaveRight && _ai.UnitSim.Get_TrickRoomContextScore( theirRight.Pokemon ) > 0;

        bool theirLeftIsOffensiveSupport = theirLeftSP.Redirector || theirLeftSP.HelpingHandSupport;
        bool theirRightIsOffensiveSupport = theirRightSP.Redirector || theirRightSP.HelpingHandSupport;

        //--------------------------------------------------------------------
        //--Investment--------------------------------------------------------
        //--------------------------------------------------------------------
        if( theyHaveTailwind )
            score += 1;

        if( theyHaveScreens )
            score += 1;

        if( theyBenefitWeather )
            score += 1;

        if( theyBenefitTerrain )
            score += 1;

        if( theyBenefitTrickRoom )
            score += 1;

        if( theirLeftIsOffensiveSupport && ( theirLeftPTKO_OurLeft >= PotentialToKO.Dangerous || theirLeftPTKO_OurRight >= PotentialToKO.Dangerous ) )
            score += 1;

        if( theirRightIsOffensiveSupport && ( theirRightPTKO_OurLeft >= PotentialToKO.Dangerous || theirRightPTKO_OurRight >= PotentialToKO.Dangerous ) )
            score += 1;

        return score;
    }

    private int ScorePreserveTempoCommitment( List<PairObservationEvidence> poe )
    {
        int score = 0;

        //--Blackboard Information
        var ourActiveUnits = _ai.Blackboard.OurActiveBattleAIUnits;
        var theirActiveUnits = _ai.Blackboard.TheirActiveBattleAIUnits;

        var ourRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( ourActiveUnits[0].Pokemon ).Where( u => !ourActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();
        var theirRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( theirActiveUnits[0].Pokemon ).Where( u => !theirActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();

        var currentField = _ai.Blackboard.CurrentFieldSnapshot;
        var gp = _ai.Blackboard.GamePlan;

        IBattleAIUnit ourLeft = ourActiveUnits[0];
        IBattleAIUnit ourRight = ourActiveUnits.Count > 1 ? ourActiveUnits[1] : null;

        IBattleAIUnit theirLeft = theirActiveUnits[0];
        IBattleAIUnit theirRight = theirActiveUnits.Count > 1 ? theirActiveUnits[1] : null;

        bool weHaveRight = ourRight != null;
        bool theyHaveRight = theirRight != null;

        //--Strategy Profiles
        var ourLeftSP = _ai.Projection.GetStrategicProfile( ourLeft );
        var ourRightSP = weHaveRight ? _ai.Projection.GetStrategicProfile( ourRight ) : default;

        var theirLeftSP = _ai.Projection.GetStrategicProfile( theirLeft );
        var theirRightSP = theyHaveRight ? _ai.Projection.GetStrategicProfile( theirRight ) : default;

        var theirCourt = theirLeft.CourtLocation == CourtLocation.TopCourt ? currentField.TopCourtConditions : currentField.BottomCourtConditions;

        PairObservationProfile switchEvidence = default;
        IBattleAIUnit returnUnit = null;
        foreach( var e in poe )
        {
            //--Pivot Moves are currnently only observed as a function of a SwitchIntent, which has the "parent observation" of Switch,
            //--therefore all switch information can be found here, whether it be defensive, offensive, or via pivot move such as volt switch or parting shot
            if( TryGetObservationProfile( e, PairObservation.Switch, out switchEvidence ) )
            {
                if( returnUnit == null )
                {
                    returnUnit = switchEvidence.IntentResult.CurrentActor;
                }
                else if( returnUnit.Pokemon != switchEvidence.IntentResult.CurrentActor.Pokemon )
                {
                    Debug.LogError( $"you know what this is 07/25/26" );
                }
            }
        }

        if( returnUnit == null )
        {
            Debug.LogError( $"Switch Observation returned no return unit (CurrentActor), fix this." );
            return 0;
        }

        var returnUnitSP = _ai.Projection.GetStrategicProfile( returnUnit );

        //--Team Strategy Invested Already Checks. Instead of using these to further attacking, we're checking to see if a support/utility pokemon now needs to pivot out to keep up pressure
        //--and maximize strategy value
        bool theyHaveTailwind = theirCourt.ContainsKey( CourtConditionID.Tailwind );
        bool theyHaveScreens = theirCourt.ContainsKey( CourtConditionID.Reflect ) || theirCourt.ContainsKey( CourtConditionID.LightScreen ) || theirCourt.ContainsKey( CourtConditionID.AuroraVeil );
        bool theyBenefitWeather = theirLeftSP.DependsOnCurrentWeather || theyHaveRight && theirRightSP.DependsOnCurrentWeather || _ai.UnitSim.Get_WeatherContextScore( theirLeft.Pokemon ) > 0 || theyHaveRight && _ai.UnitSim.Get_WeatherContextScore( theirRight.Pokemon ) > 0;
        bool theyBenefitTerrain = theirLeftSP.DependsOnCurrentTerrain || theyHaveRight && theirRightSP.DependsOnCurrentTerrain || _ai.UnitSim.Get_TerrainContextScore( theirLeft.Pokemon ) > 0 || theyHaveRight && _ai.UnitSim.Get_TerrainContextScore( theirRight.Pokemon ) > 0;
        bool theyBenefitTrickRoom = theirLeftSP.DependsOnTrickRoom || theyHaveRight && theirRightSP.DependsOnTrickRoom || _ai.UnitSim.Get_TrickRoomContextScore( theirLeft.Pokemon ) > 0 || theyHaveRight && _ai.UnitSim.Get_TrickRoomContextScore( theirRight.Pokemon ) > 0;

        //--Check to see if the Pokemon switching out was utility and has fulfilled their role.
        bool returnUnitSetExistingFieldEffect = false;
        bool returnUnitProvidedCoverSupport = false;
        bool returnUnitHasExpendedFakeOut = false;
        bool returnUnitHasRedirection = false;
        bool returnUnitHasPivotMove = false;
        bool returnUnitProvidedIntimidate = false;

        //--Incoming Candidate Information
        bool switchCandidateBenefitsImmediately = false;
        bool switchCandidateHasGoodOffensivePressure = false;
        bool switchCandidateEntersSafely = false; //--check incoming PTKOs
        bool returnPokemonPivotsWell = false;   //--check likely turn order to determine how many incoming attacks, if any, the incoming pokemon will have to take.
                                                //--also check if that pivot move out-right removes a target in the process or if it's parting shot, plus the existence of intimidate/demoralize

        bool benchHasBoardStateAbusers = false;

        //--Check to see if returning unit was field effect setter
        if( theyHaveTailwind && returnUnit.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter ) )
            returnUnitSetExistingFieldEffect = true;

        if( theyHaveScreens && returnUnit.RoleProfile.Traits.Contains( RoleTrait.ScreenSetter ) )
            returnUnitSetExistingFieldEffect = true;

        if( theyBenefitWeather && returnUnitSP.WeatherSetter && currentField.Weather != WeatherConditionID.None && ( _ai.UnitSim.GetWeatherFrom_Ability( returnUnit.Pokemon ) == currentField.Weather || _ai.UnitSim.GetWeatherFrom_Moveset( returnUnit.Pokemon ) == currentField.Weather ) )
            returnUnitSetExistingFieldEffect = true;

        if( theyBenefitTerrain && returnUnitSP.TerrainSetter && currentField.Terrain != TerrainID.None && _ai.UnitSim.GetTerrainFrom_Ability( returnUnit.Pokemon ) == currentField.Terrain )
            returnUnitSetExistingFieldEffect = true;

        if( theyBenefitTrickRoom && returnUnit.RoleProfile.Traits.Contains( RoleTrait.TrickRoomSetter ) )
            returnUnitSetExistingFieldEffect = true;

        //--Check to see if returning unit was likely providing cover support
        bool canFakeOutLeft = ourLeft != null && _ai.CanUseFakeOut( returnUnit, ourLeft );
        bool canFakeOutRight = ourRight != null && _ai.CanUseFakeOut( returnUnit, ourRight );
        returnUnitHasExpendedFakeOut = returnUnit.Pokemon.CheckHasActiveMove( "Fake Out" ) && !canFakeOutLeft && !canFakeOutRight;

        returnUnitHasRedirection = returnUnitSP.Redirector;
        returnUnitProvidedCoverSupport = returnUnitHasExpendedFakeOut || returnUnitHasRedirection;

        returnUnitHasPivotMove = returnUnit.RoleProfile.Traits.Contains( RoleTrait.PivotMove );
        returnUnitProvidedIntimidate = returnUnit.Ability == AbilityID.Intimidate || returnUnit.Ability == AbilityID.Demoralize;

        bool returnUnitHasExtractedValue = returnUnitSetExistingFieldEffect || returnUnitProvidedCoverSupport || returnUnitProvidedIntimidate;

        //--Check to see if the pivot process can be executed safely or with useful utility
        if( switchEvidence.Top.OpponentPTKO <= PotentialToKO.Risky )
            switchCandidateEntersSafely = true;

        var switchCandidate = switchEvidence.Top.Attacker;
        var candComp_OurLeft = _ai.Projection.MakeUnitComparison( switchCandidate, ourLeft );
        var candComp_OurRight = ourRight != null ? _ai.Projection.MakeUnitComparison( switchCandidate, ourRight ) : default;

        if( candComp_OurLeft.Attacker.BestCurrentPTKO >= PotentialToKO.Risky || ( weHaveRight && candComp_OurRight.Attacker.BestCurrentPTKO >= PotentialToKO.Risky ) )
            switchCandidateHasGoodOffensivePressure = true;

        bool pivotingSafelyImprovesPressure = switchCandidateHasGoodOffensivePressure && switchCandidateEntersSafely;

        if( returnUnitProvidedIntimidate || ( returnUnitHasPivotMove && returnUnit.Pokemon.CheckHasActiveMove( "Parting Shot" ) ) )
            returnPokemonPivotsWell = true;

        //--------------------------------------------------------------------
        //--Investment--------------------------------------------------------
        //--------------------------------------------------------------------
        //--They have or benefit from existing field effect
        if( theyHaveTailwind )
            score += 1;

        if( theyHaveScreens )
            score += 1;

        if( theyBenefitWeather )
            score += 1;

        if( theyBenefitTerrain )
            score += 1;

        if( theyBenefitTrickRoom )
            score += 1;

        //--Return Unit has fulfilled role
        if( returnUnitHasExtractedValue )
            score += 1;

        //--Return Unit has Pivot Move
        if( returnUnitHasPivotMove )
            score += 1;

        //--Switch Candidate benefits immediately from board (increased speed, weather abuser, trick room abuser, screen or intimidate/demoralize protects weaker defensive stat)
        if( switchCandidateBenefitsImmediately )
            score += 1;

        //--Switch Candidate gets in relatively safely from pivot and can keep up strong pressure
        if( pivotingSafelyImprovesPressure )
            score += 1;

        //--Return Pokemon does good chip damage with pivot move, takes a KO with pivot move, or likely reduced its opponent's stats in the process via intimidate, demoralize, or parting shot
        if( returnPokemonPivotsWell )
            score += 1;

        //--Their bench in general has current board state abusers worth pivoting into
        if( benchHasBoardStateAbusers )
            score += 1;

        //--------------------------------------------------------------------
        //--Line Lock (small)---------------------------------------------------------
        //--------------------------------------------------------------------
        bool pivotIsNecessary = returnUnitHasExtractedValue && switchCandidateBenefitsImmediately && ( pivotingSafelyImprovesPressure || returnPokemonPivotsWell );

        var ourLeftPTKO_ReturnUnit = _ai.Projection.MakeUnitComparison( ourLeft, returnUnit ).Attacker.BestCurrentPTKO;
        var ourRightPTKO_ReturnUnit = weHaveRight ? _ai.Projection.MakeUnitComparison( ourRight, returnUnit ).Attacker.BestCurrentPTKO : default;

        bool returnUnitIsThreatened = ( ourLeftPTKO_ReturnUnit >= PotentialToKO.Dangerous && ourLeft.Speed > returnUnit?.Speed ) || ( ourRightPTKO_ReturnUnit >= PotentialToKO.Dangerous && ourRight?.Speed > returnUnit?.Speed );

        bool theyHaveBoardEffectActive = theyHaveTailwind || theyHaveScreens || theyBenefitWeather || theyBenefitTerrain || theyBenefitTrickRoom;

        if( pivotIsNecessary )
            score += 1;

        if( returnUnitIsThreatened )
            score += 1;

        if( switchCandidateBenefitsImmediately && theyHaveBoardEffectActive )
            score += 1;

        //--------------------------------------------------------------------
        //--Opportunity Cost--------------------------------------------------
        //--------------------------------------------------------------------
        bool returnUnitProvidesNothingElse = returnUnitHasExtractedValue && ( !returnUnitSP.ProvidesStrongOffense || !returnUnitSP.Redirector || returnUnitIsThreatened );
        bool boardStateIsTickingDown = false;
        bool waitingStrandsReturnUnit = false;
        bool delayingRemovesValueForIncoming = false;

        if( theyHaveTailwind && theirCourt[CourtConditionID.Tailwind] <= 3 )
            boardStateIsTickingDown = true;

        bool theyHaveReflect = theirCourt.ContainsKey( CourtConditionID.Reflect );
        bool theyHaveLightScreen = theirCourt.ContainsKey( CourtConditionID.LightScreen );
        bool theyHaveAuroraVeil = theirCourt.ContainsKey( CourtConditionID.AuroraVeil );

        if( theyHaveReflect && theirCourt[CourtConditionID.Reflect] <= 3 || theyHaveLightScreen && theirCourt[CourtConditionID.LightScreen] <= 3 || theyHaveAuroraVeil && theirCourt[CourtConditionID.AuroraVeil] <= 3 )
            boardStateIsTickingDown = true;

        if( theyBenefitWeather && currentField.WeatherDuration <= 3 )
            boardStateIsTickingDown = true;

        if( theyBenefitTerrain && currentField.TerrainDuration <= 3 )
            boardStateIsTickingDown = true;

        if( theyBenefitTrickRoom && currentField.FieldConditions[FieldConditionID.TrickRoom] <= 3 )
            boardStateIsTickingDown = true;

        bool weHaveTaunt = ourLeft.RoleProfile.Traits.Contains( RoleTrait.Taunt ) || weHaveRight && ourRight.RoleProfile.Traits.Contains( RoleTrait.Taunt );
        bool weHaveEncore = ourLeft.RoleProfile.Traits.Contains( RoleTrait.Encore ) || weHaveRight && ourRight.RoleProfile.Traits.Contains( RoleTrait.Encore );
        bool weOutspeed = ourLeft.Speed > returnUnit.Speed || weHaveRight && ourRight.Speed > returnUnit.Speed;
        bool returnUnitLostSash = returnUnit.Item == ItemBattleEffectID.FocusSash && returnUnit.BeginningHPR < 1f || returnUnit.RoleProfile.Traits.Contains( RoleTrait.FocusSash ) && returnUnit.Item != ItemBattleEffectID.FocusSash;
        bool returnUnitLostSitrus = returnUnit.RoleProfile.Traits.Contains( RoleTrait.RecoveryItem ) && returnUnit.Item != ItemBattleEffectID.SitrusBerry && returnUnit.Item != ItemBattleEffectID.Leftovers;

        waitingStrandsReturnUnit = ( returnUnitSP.ProvidesSupport && ( weHaveTaunt || weHaveEncore ) && weOutspeed ) || ( returnUnitIsThreatened && ( weOutspeed || returnUnitLostSash || returnUnitLostSitrus ) );

        delayingRemovesValueForIncoming = boardStateIsTickingDown && ( switchCandidateBenefitsImmediately || pivotingSafelyImprovesPressure || benchHasBoardStateAbusers );

        if( returnUnitProvidesNothingElse )
            score += 1;

        if( boardStateIsTickingDown )
            score += 1;

        if( waitingStrandsReturnUnit )
            score += 1;

        if( delayingRemovesValueForIncoming )
            score += 1;

        return score;
    }

    private int ScoreDenyOpponentSetupCommitment( List<PairObservationEvidence> poe )
    {
        int score = 0;

        //--Blackboard Information
        var ourActiveUnits = _ai.Blackboard.OurActiveBattleAIUnits;
        var theirActiveUnits = _ai.Blackboard.TheirActiveBattleAIUnits;

        var ourRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( ourActiveUnits[0].Pokemon ).Where( u => !ourActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();
        var theirRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( theirActiveUnits[0].Pokemon ).Where( u => !theirActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();

        var currentField = _ai.Blackboard.CurrentFieldSnapshot;

        //--This will check to see if the ai has board setup available to it. does the ai have a trick room core lead?
        //--a prankster tailwind setter next to a support or abuser?
        //--perhaps a prankster screens setter like grimmsnarl?
        //--in these cases, we will check to see how well the ai's opponent can act to deny the ai these setups
        //--by looking for things like taunt, fake out, tailwind matching, using trick room against the ai's trick room to cause it to be immediately reversed
        //--or maybe even access to pivoting directly into a weather setter to change the ai's weather or terrian if their weather is up currently
        //--and the ai has an abuser on the field. this could also look at denying things like sun + choice scarf eruption, or sun + after you eruption

        var ourLeft = ourActiveUnits[0];
        var ourRight = ourActiveUnits.Count > 1 ? ourActiveUnits[1] : null;

        var theirLeft = theirActiveUnits[0];
        var theirRight = theirActiveUnits.Count > 1 ? theirActiveUnits[1] : null;

        bool weHaveRight = ourRight != null;
        bool theyHaveRight = theirRight != null;

        //--Check ai's units for field setters
        bool weHaveTailwindSetter = false;
        bool weHaveTrickRoomSetter = false;

        bool weHaveManualWeather = false;

        bool weHaveReflectSetter = false;
        bool weHaveLightScreenSetter = false;
        bool weHaveAuroraVeilSetter = false;

        bool weSetWeatherWithAbility = false;
        bool weSetTerrainWithAbility = false;

        bool weHavePranksterUser = false;

        bool weHaveFakeOutAvailable = false;
        bool weHaveRedirector = false;
        bool weBlockPriority = false;

        WeatherConditionID manualWeather = WeatherConditionID.None;

        foreach( var unit in ourActiveUnits )
        {
            var rp = unit.RoleProfile;
            var traits = rp.Traits;

            if( traits.Contains( RoleTrait.TailwindSetter ) )
                weHaveTailwindSetter = true;

            if( traits.Contains( RoleTrait.TrickRoomSetter ) )
                weHaveTrickRoomSetter = true;

            if( _ai.UnitSim.GetWeatherFrom_Moveset( unit.Pokemon ) is var weather && weather != WeatherConditionID.None )
            {
                weHaveManualWeather = true;

                if( manualWeather == WeatherConditionID.None )
                    manualWeather = weather;
            }

            if( unit.Pokemon.CheckHasActiveMove( "Reflect" ) )
                weHaveReflectSetter = true;

            if( unit.Pokemon.CheckHasActiveMove( "Light Screen" ) )
                weHaveLightScreenSetter = true;

            if( unit.Pokemon.CheckHasActiveMove( "Aurora Veil" ) )
                weHaveAuroraVeilSetter = true;

            if( currentField.Weather != WeatherConditionID.None && _ai.UnitSim.GetWeatherFrom_Ability( unit.Pokemon ) == currentField.Weather )
                weSetWeatherWithAbility = true;

            if( currentField.Terrain != TerrainID.None && _ai.UnitSim.GetTerrainFrom_Ability( unit.Pokemon ) == currentField.Terrain )
                weSetTerrainWithAbility = true;

            if( unit.Ability == AbilityID.Prankster )
                weHavePranksterUser = true;

            if( !weHaveFakeOutAvailable )
                weHaveFakeOutAvailable = _ai.CanUseFakeOut( unit, theirLeft ) || ( theyHaveRight && _ai.CanUseFakeOut( unit, theirRight ) );

            if( !weHaveRedirector )
                weHaveRedirector = traits.Contains( RoleTrait.RedirectionMove );

            if( !weBlockPriority )
                weBlockPriority = traits.Contains( RoleTrait.PriorityBlocker ) || ( unit.Ability == AbilityID.PsychicSurge && currentField.Terrain == TerrainID.Psychic );
        }

        //--Quick-check ai's possible board state investment for obvious field setting signals
        bool weWantToSetTailwind = false;
        bool weWantToSetTrickRoom = false;
        bool weWantToSetManualWeather = false;
        bool weWantToSetScreens = false;

        bool weWantToSetup = weWantToSetTailwind || weWantToSetTrickRoom || weWantToSetManualWeather || weWantToSetScreens;

        bool weHaveScreensSetter = weHaveReflectSetter || weHaveLightScreenSetter || weHaveAuroraVeilSetter;
        bool weHaveSetupCover = weHaveRedirector || weHaveFakeOutAvailable;

        if( weHaveTailwindSetter )
        {
            bool leftSpeedAppropriate = ourLeft.RoleProfile.Biases.Contains( RoleBias.AwkwardSpeed ) || ourLeft.RoleProfile.Biases.Contains( RoleBias.MiddlingSpeed ) || ourLeft.RoleProfile.Biases.Contains( RoleBias.FastSpeed );
            bool leftOffensive = ourLeft.RoleProfile.PrimaryRole == RoleClass.BulkyAttacker || ourLeft.RoleProfile.PrimaryRole == RoleClass.RevengeKiller || ourLeft.RoleProfile.PrimaryRole == RoleClass.SetupSweeper || ourLeft.RoleProfile.PrimaryRole == RoleClass.Sweeper || ourLeft.RoleProfile.PrimaryRole == RoleClass.WallBreaker;
            bool leftAbuser = leftSpeedAppropriate && ( ourLeft.RoleProfile.Traits.Contains( RoleTrait.SpreadAttack ) || leftOffensive );

            bool rightSpeedAppropriate = weHaveRight && ourRight.RoleProfile.Biases.Contains( RoleBias.AwkwardSpeed ) || ourRight.RoleProfile.Biases.Contains( RoleBias.MiddlingSpeed ) || ourRight.RoleProfile.Biases.Contains( RoleBias.FastSpeed );
            bool rightOffensive = weHaveRight && ourRight.RoleProfile.PrimaryRole == RoleClass.BulkyAttacker || ourRight.RoleProfile.PrimaryRole == RoleClass.RevengeKiller || ourRight.RoleProfile.PrimaryRole == RoleClass.SetupSweeper || ourRight.RoleProfile.PrimaryRole == RoleClass.Sweeper || ourRight.RoleProfile.PrimaryRole == RoleClass.WallBreaker;
            bool rightAbuser = weHaveRight && rightSpeedAppropriate && ( ourRight.RoleProfile.Traits.Contains( RoleTrait.SpreadAttack ) || rightOffensive );

            weWantToSetTailwind = ( weHavePranksterUser && weHaveSetupCover ) || ( ( weHavePranksterUser || weHaveSetupCover ) && ( leftAbuser || rightAbuser ) );
        }

        if( weHaveTrickRoomSetter )
        {
            bool leftSpeedAppropriate = ourLeft.RoleProfile.Biases.Contains( RoleBias.SlowSpeed ) || ourLeft.RoleProfile.Biases.Contains( RoleBias.TrickRoomSpeed );
            bool leftOffensive = ourLeft.RoleProfile.PrimaryRole == RoleClass.BulkyAttacker || ourLeft.RoleProfile.PrimaryRole == RoleClass.RevengeKiller || ourLeft.RoleProfile.PrimaryRole == RoleClass.SetupSweeper || ourLeft.RoleProfile.PrimaryRole == RoleClass.Sweeper || ourLeft.RoleProfile.PrimaryRole == RoleClass.WallBreaker || ourLeft.RoleProfile.PrimaryRole == RoleClass.TrickRoomAbuser || ourLeft.RoleProfile.SecondaryRoles.Contains( RoleClass.TrickRoomAbuser );
            bool leftAbuser = leftSpeedAppropriate && ( ourLeft.RoleProfile.Traits.Contains( RoleTrait.SpreadAttack ) || leftOffensive );

            bool rightSpeedAppropriate = weHaveRight && ourRight.RoleProfile.Biases.Contains( RoleBias.SlowSpeed ) || ourRight.RoleProfile.Biases.Contains( RoleBias.TrickRoomSpeed );
            bool rightOffensive = weHaveRight && ourRight.RoleProfile.PrimaryRole == RoleClass.BulkyAttacker || ourRight.RoleProfile.PrimaryRole == RoleClass.RevengeKiller || ourRight.RoleProfile.PrimaryRole == RoleClass.SetupSweeper || ourRight.RoleProfile.PrimaryRole == RoleClass.Sweeper || ourRight.RoleProfile.PrimaryRole == RoleClass.WallBreaker || ourRight.RoleProfile.PrimaryRole == RoleClass.TrickRoomAbuser || ourRight.RoleProfile.SecondaryRoles.Contains( RoleClass.TrickRoomAbuser );
            bool rightAbuser = weHaveRight && rightSpeedAppropriate && ( ourRight.RoleProfile.Traits.Contains( RoleTrait.SpreadAttack ) || rightOffensive );

            weWantToSetTrickRoom = weHaveSetupCover || leftAbuser || rightAbuser;
        }

        if( weHaveManualWeather )
        {
            bool leftWeatherAbility = _ai.UnitSim.PokemonAbilityMatchesWeather( ourLeft.Pokemon, manualWeather );
            bool rightWeatherAbility = weHaveRight && _ai.UnitSim.PokemonAbilityMatchesWeather( ourRight.Pokemon, manualWeather );

            bool leftMoveAbusesWeather = _ai.UnitSim.PokemonHasMove_AbusesWeather( ourLeft.Pokemon, manualWeather );
            bool rightMoveAbusesWeather = weHaveRight && _ai.UnitSim.PokemonHasMove_AbusesWeather( ourRight.Pokemon, manualWeather );

            bool leftAbuser = leftWeatherAbility || leftMoveAbusesWeather;
            bool rightAbuser = rightWeatherAbility || rightMoveAbusesWeather;

            weWantToSetManualWeather = ( weHavePranksterUser && weHaveSetupCover ) || ( ( weHavePranksterUser || weHaveSetupCover ) && ( leftAbuser || rightAbuser ) );
        }

        if( weHaveScreensSetter )
        {
            bool leftScreensInvestment = false;
            bool rightScreensInvestment = false;

            if( ourLeft.RoleProfile.Traits.Contains( RoleTrait.ScreenSetter ) )
            {
                if( ourLeft.Ability == AbilityID.Prankster || ourLeft.Item == ItemBattleEffectID.LightClay )
                    leftScreensInvestment = true;
            }

            if( ourRight.RoleProfile.Traits.Contains( RoleTrait.ScreenSetter ) )
            {
                if( ourRight.Ability == AbilityID.Prankster || ourRight.Item == ItemBattleEffectID.LightClay )
                    rightScreensInvestment = true;
            }

            bool leftOffensive = ourLeft.RoleProfile.PrimaryRole == RoleClass.BulkyAttacker || ourLeft.RoleProfile.PrimaryRole == RoleClass.RevengeKiller || ourLeft.RoleProfile.PrimaryRole == RoleClass.SetupSweeper || ourLeft.RoleProfile.PrimaryRole == RoleClass.Sweeper || ourLeft.RoleProfile.PrimaryRole == RoleClass.WallBreaker || ourLeft.RoleProfile.PrimaryRole == RoleClass.TrickRoomAbuser || ourLeft.RoleProfile.SecondaryRoles.Contains( RoleClass.TrickRoomAbuser );
            bool rightOffensive = weHaveRight && ourRight.RoleProfile.PrimaryRole == RoleClass.BulkyAttacker || ourRight.RoleProfile.PrimaryRole == RoleClass.RevengeKiller || ourRight.RoleProfile.PrimaryRole == RoleClass.SetupSweeper || ourRight.RoleProfile.PrimaryRole == RoleClass.Sweeper || ourRight.RoleProfile.PrimaryRole == RoleClass.WallBreaker || ourRight.RoleProfile.PrimaryRole == RoleClass.TrickRoomAbuser || ourRight.RoleProfile.SecondaryRoles.Contains( RoleClass.TrickRoomAbuser );

            weWantToSetScreens = ( leftScreensInvestment && rightOffensive ) || ( rightScreensInvestment && leftOffensive );
        }

        //--Check for their (the ai's opponent) setup-stopping resources, such as fake out, encore, and taunt
        var theirLeftSP = _ai.Projection.GetStrategicProfile( theirLeft );
        var theirRightSP = theyHaveRight ? _ai.Projection.GetStrategicProfile( theirRight ) : default;

        bool theirLeftBlocksSetup = theirLeftSP.BlocksBattlefieldSetup;
        bool theirLeftBlocksPriority = theirLeftSP.BlocksPriority && ( weHavePranksterUser || weHaveFakeOutAvailable );

        bool theirRightBlocksSetup = theirRightSP.BlocksBattlefieldSetup;
        bool theirRightBlocksPriority = theirRightSP.BlocksPriority && ( weHavePranksterUser || weHaveFakeOutAvailable );

        bool theirLeftHasTaunt = theirLeft.RoleProfile.Traits.Contains( RoleTrait.Taunt );
        bool theirLeftHasEncore = theirLeft.RoleProfile.Traits.Contains( RoleTrait.Encore );
        bool theirLeftHasPrankster = theirLeft.Ability == AbilityID.Prankster;
        bool theirLeftCanFakeOut = _ai.CanUseFakeOut( theirLeft, ourLeft ) || ( weHaveRight && _ai.CanUseFakeOut( theirLeft, ourRight ) );

        bool theirRightHasTaunt = theyHaveRight && theirRight.RoleProfile.Traits.Contains( RoleTrait.Taunt );
        bool theirRightHasEncore = theyHaveRight && theirRight.RoleProfile.Traits.Contains( RoleTrait.Encore );
        bool theirRightHasPrankster = theyHaveRight && theirRight.Ability == AbilityID.Prankster;
        bool theirRightCanFakeOut = theyHaveRight && ( _ai.CanUseFakeOut( theirRight, ourLeft ) || ( weHaveRight && _ai.CanUseFakeOut( theirRight, ourRight ) ) );

        bool theyCanStopTailwind = false;
        bool theyCanStopTrickRoom = false;
        bool theyCanStopScreens = false;
        bool theyCanStopManualWeather = false;

        bool theyCanChangeWeather = false;
        bool theyCanChangeTerrain = false;

        foreach( var unit in theirRemainingUnits )
        {
            if( _ai.UnitSim.PokemonHasWeatherSetter_Ability( unit.Pokemon ) )
            {
                theyCanChangeWeather = true;
            }

            if( _ai.UnitSim.PokemonHasTerrainSetter_Ability( unit.Pokemon ) )
            {
                theyCanChangeTerrain = true;
            }
        }

        if( weWantToSetTailwind )
        {
            var ourSetter = ourLeft.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter ) ? ourLeft : weHaveRight ? ourRight : null;
            if( ourSetter != null )
            {
                int ourSetterSpeed = ourSetter.Speed;
                bool theirLeftOutSpeedsSetter = theirLeft.Speed > ourSetterSpeed;
                bool theirLeftCanMatchTailwind = theirLeft.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter );
                bool weSetupFasterThanLeft = ( weHavePranksterUser && !theirLeftHasPrankster ) || ( weHavePranksterUser && theirLeftHasPrankster && !theirLeftOutSpeedsSetter ) || ( !weHavePranksterUser && !theirLeftHasPrankster && !theirLeftOutSpeedsSetter );
                bool theirLeftCanStopTailwind = theirLeftCanMatchTailwind || ( !weSetupFasterThanLeft && theirLeftHasTaunt && ourSetter.Item != ItemBattleEffectID.MentalHerb ) || ( theirLeftCanFakeOut && !weBlockPriority );

                bool theirRightOutSpeedsSetter = theyHaveRight && theirRight.Speed > ourSetterSpeed;
                bool theirRightCanMatchTailwind = theyHaveRight && theirRight.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter );
                bool weSetupFasterThanRight = ( weHavePranksterUser && !theirRightHasPrankster ) || ( weHavePranksterUser && theirRightHasPrankster && !theirRightOutSpeedsSetter ) || ( !weHavePranksterUser && !theirRightHasPrankster && !theirRightOutSpeedsSetter );
                bool theirRightCanStopTailwind = theirRightCanMatchTailwind || ( !weSetupFasterThanRight && theirRightHasTaunt && ourSetter.Item != ItemBattleEffectID.MentalHerb ) || ( theirRightCanFakeOut && !weBlockPriority );

                theyCanStopTailwind = theirLeftCanStopTailwind || theirRightCanStopTailwind;
            }
        }

        if( weWantToSetTrickRoom )
        {
            var ourSetter = ourLeft.RoleProfile.Traits.Contains( RoleTrait.TrickRoomSetter ) ? ourLeft : weHaveRight ? ourRight : null;
            if( ourSetter != null )
            {
                bool theirLeftHasTrickRoom = theirLeft.RoleProfile.Traits.Contains( RoleTrait.TrickRoomSetter );
                bool theirLeftHasImprison = theirLeft.Pokemon.CheckHasActiveMove( "Imprison" ) && theirLeftHasTrickRoom;
                bool theirLeftCanStopTrickRoom = theirLeftHasImprison || theirLeftHasTrickRoom || ( theirLeftHasTaunt && ourSetter.Item != ItemBattleEffectID.MentalHerb ) || ( theirLeftCanFakeOut && !weBlockPriority );

                bool theirRightHasTrickRoom = theyHaveRight && theirRight.RoleProfile.Traits.Contains( RoleTrait.TrickRoomSetter );
                bool theirRightHasImprison = theirRight.Pokemon.CheckHasActiveMove( "Imprison" ) && theirRightHasTrickRoom;
                bool theirRightCanStopTrickRoom = theirRightHasImprison || theirRightHasTrickRoom || ( theirRightHasTaunt && ourSetter.Item != ItemBattleEffectID.MentalHerb ) || ( theirRightCanFakeOut && !weBlockPriority );

                theyCanStopTrickRoom = theirLeftCanStopTrickRoom || theirRightCanStopTrickRoom;
            }
        }

        if( weWantToSetScreens )
        {
            var ourSetter = ourLeft.RoleProfile.Traits.Contains( RoleTrait.ScreenSetter ) ? ourLeft : weHaveRight ? ourRight : null;
            if( ourSetter != null )
            {
                int ourSetterSpeed = ourSetter.Speed;
                bool theirLeftOutSpeedsSetter = theirLeft.Speed > ourSetterSpeed;
                bool theirLeftCanMatchScreens = theirLeft.RoleProfile.Traits.Contains( RoleTrait.ScreenSetter );
                bool weSetupFasterThanLeft = ( weHavePranksterUser && !theirLeftHasPrankster ) || ( weHavePranksterUser && theirLeftHasPrankster && !theirLeftOutSpeedsSetter ) || ( !weHavePranksterUser && !theirLeftHasPrankster && !theirLeftOutSpeedsSetter );
                bool theirLeftCanStopScreens = theirLeftCanMatchScreens || ( !weSetupFasterThanLeft && theirLeftHasTaunt && ourSetter.Item != ItemBattleEffectID.MentalHerb ) || ( theirLeftCanFakeOut && !weBlockPriority );

                bool theirRightOutSpeedsSetter = theyHaveRight && theirRight.Speed > ourSetterSpeed;
                bool theirRightCanMatchScreens = theyHaveRight && theirRight.RoleProfile.Traits.Contains( RoleTrait.ScreenSetter );
                bool weSetupFasterThanRight = ( weHavePranksterUser && !theirRightHasPrankster ) || ( weHavePranksterUser && theirRightHasPrankster && !theirRightOutSpeedsSetter ) || ( !weHavePranksterUser && !theirRightHasPrankster && !theirRightOutSpeedsSetter );
                bool theirRightCanStopScreens = theirRightCanMatchScreens || ( !weSetupFasterThanRight && theirRightHasTaunt && ourSetter.Item != ItemBattleEffectID.MentalHerb ) || ( theirRightCanFakeOut && !weBlockPriority );

                theyCanStopScreens = theirLeftCanStopScreens || theirRightCanStopScreens;
            }
        }

        if( weWantToSetManualWeather )
        {
            var ourSetter = _ai.UnitSim.PokemonHasWeatherSetter_Move( ourLeft.Pokemon ) ? ourLeft : weHaveRight ? ourRight : null;
            if( ourSetter != null )
            {
                int ourSetterSpeed = ourSetter.Speed;
                bool theirLeftOutSpeedsSetter = theirLeft.Speed > ourSetterSpeed;
                bool weSetupFasterThanLeft = ( weHavePranksterUser && !theirLeftHasPrankster ) || ( weHavePranksterUser && theirLeftHasPrankster && !theirLeftOutSpeedsSetter ) || ( !weHavePranksterUser && !theirLeftHasPrankster && !theirLeftOutSpeedsSetter );
                bool theirLeftCanStopWeather = ( !weSetupFasterThanLeft && theirLeftHasTaunt && ourSetter.Item != ItemBattleEffectID.MentalHerb ) || ( theirLeftCanFakeOut && !weBlockPriority );

                bool theirRightOutSpeedsSetter = theyHaveRight && theirRight.Speed > ourSetterSpeed;
                bool weSetupFasterThanRight = ( weHavePranksterUser && !theirRightHasPrankster ) || ( weHavePranksterUser && theirRightHasPrankster && !theirRightOutSpeedsSetter ) || ( !weHavePranksterUser && !theirRightHasPrankster && !theirRightOutSpeedsSetter );
                bool theirRightCanStopWeather = ( !weSetupFasterThanRight && theirRightHasTaunt && ourSetter.Item != ItemBattleEffectID.MentalHerb ) || ( theirRightCanFakeOut && !weBlockPriority );

                theyCanStopManualWeather = theirLeftCanStopWeather || theirRightCanStopWeather;
            }
        }

        //--------------------------------------------------------------------
        //--Investment--------------------------------------------------------
        //--------------------------------------------------------------------

        //--If the ai looks like it wants to setup, they will naturally be more inclined to at least work toward managing or preventing it
        if( weWantToSetup )
            score += 1;

        if( theirLeftBlocksSetup || theirRightBlocksSetup )
            score += 1;

        if( theirLeftBlocksPriority || theirRightBlocksPriority )
            score += 1;

        if( theirLeftCanFakeOut || theirRightCanFakeOut )
            score += 1;

        if( weWantToSetTailwind && theyCanStopTailwind )
            score += 2;

        if( weWantToSetTrickRoom && theyCanStopTrickRoom )
            score += 2;

        if( weWantToSetManualWeather && ( theyCanStopManualWeather || theyCanChangeWeather ) )
            score += 2;

        if( weSetWeatherWithAbility && theyCanChangeWeather )
            score += 1;

        if( weSetTerrainWithAbility && theyCanChangeTerrain )
            score += 1;

        if( weWantToSetScreens && theyCanStopScreens )
            score += 2;

        //--------------------------------------------------------------------
        //--Line Lock---------------------------------------------------------
        //--------------------------------------------------------------------

        //--Clear Strategyline for Denial
        int ourAvailableStrategies = 0;
        if( weWantToSetTailwind )
            ourAvailableStrategies++;

        if( weWantToSetTrickRoom )
            ourAvailableStrategies++;

        if( weWantToSetManualWeather )
            ourAvailableStrategies++;

        if( weWantToSetScreens )
            ourAvailableStrategies++;

        var theirLeft_Left = _ai.Projection.MakeUnitComparison( theirLeft, ourLeft );
        var theirLeft_Right = weHaveRight ? _ai.Projection.MakeUnitComparison( theirLeft, ourRight ) : default;
        var theirRight_Left = theyHaveRight ? _ai.Projection.MakeUnitComparison( theirRight, ourLeft ) : default;
        var theirRight_Right = theyHaveRight && weHaveRight ? _ai.Projection.MakeUnitComparison( theirRight, ourRight ) : default;

        bool ourLeftHasKO_Left = theirLeft_Left.Target.BestCurrentPTKO >= PotentialToKO.Dangerous;
        bool ourLeftHasKO_Right = theirLeft_Right.Target.BestCurrentPTKO >= PotentialToKO.Dangerous;
        bool ourRightHasKO_Left = theirRight_Left.Target.BestCurrentPTKO >= PotentialToKO.Dangerous;
        bool ourRightHasKO_Right = theirRight_Right.Target.BestCurrentPTKO >= PotentialToKO.Dangerous;

        bool theirLeftHasKO_Left = theirLeft_Left.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous;
        bool theirLeftHasKO_Right = theirLeft_Right.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous;
        bool theirRightHasKO_Left = theirRight_Left.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous;
        bool theirRightHasKO_Right = theirRight_Right.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous;

        bool theyHaveKO = theirLeftHasKO_Left || theirLeftHasKO_Right || theirRightHasKO_Left || theirRightHasKO_Right;

        //--Denying our strategy directly enables theirs
        WeatherConditionID ourWeather = WeatherConditionID.None;

        if( weSetWeatherWithAbility )
        {
            ourWeather = _ai.UnitSim.PokemonHasWeatherSetter_Ability( ourLeft.Pokemon ) ? _ai.UnitSim.GetWeatherFrom_Ability( ourLeft.Pokemon ) : weHaveRight ? _ai.UnitSim.PokemonHasWeatherSetter_Ability( ourRight.Pokemon ) ? _ai.UnitSim.GetWeatherFrom_Ability( ourRight.Pokemon ) : default : default;
        }
        else if( weWantToSetManualWeather )
        {
            ourWeather = _ai.UnitSim.PokemonHasWeatherSetter_Move( ourLeft.Pokemon ) ? _ai.UnitSim.GetWeatherFrom_Moveset( ourLeft.Pokemon ) : weHaveRight ? _ai.UnitSim.PokemonHasWeatherSetter_Move( ourRight.Pokemon ) ? _ai.UnitSim.GetWeatherFrom_Moveset( ourRight.Pokemon ) : default : default;
        }

        bool weRelyOnWeather = false;
        bool weDesperatelyRelyOnWeather = false;
        if( ourWeather != WeatherConditionID.None )
        {
            var fieldWithWeather = _ai.UnitSim.BuildSimField();

            fieldWithWeather.Weather = ourWeather;
            fieldWithWeather.WeatherDuration = 5;

            var ourLeftWeatherSP = _ai.Projection.GetStrategicProfile( ourLeft, fieldWithWeather );
            var ourRightWeatherSP = weHaveRight ? _ai.Projection.GetStrategicProfile( ourRight, fieldWithWeather ) : default;

            weRelyOnWeather = ourLeftWeatherSP.DependsOnCurrentWeather || ourRightWeatherSP.DependsOnCurrentWeather;
            weDesperatelyRelyOnWeather = ourLeftWeatherSP.DependsOnCurrentWeather && ourRightWeatherSP.DependsOnCurrentWeather;
        }

        bool theyRelyOnWeather = false;
        bool theyDesperatelyRelyOnWeather = false;
        bool theirWeatherLikelyGivesThemAKO = false;

        WeatherConditionID theirWeather = WeatherConditionID.None;
        if( theyCanChangeWeather )
        {
            theirWeather = _ai.UnitSim.PokemonHasWeatherSetter_Ability( theirLeft.Pokemon ) ? _ai.UnitSim.GetWeatherFrom_Ability( theirLeft.Pokemon ) : theyHaveRight ? _ai.UnitSim.PokemonHasWeatherSetter_Ability( theirRight.Pokemon ) ? _ai.UnitSim.GetWeatherFrom_Ability( theirRight.Pokemon ) : default : default;
            
            if( theirWeather == WeatherConditionID.None )
                theirWeather = _ai.UnitSim.PokemonHasWeatherSetter_Move( theirLeft.Pokemon ) ? _ai.UnitSim.GetWeatherFrom_Moveset( theirLeft.Pokemon ) : theyHaveRight ? _ai.UnitSim.PokemonHasWeatherSetter_Move( theirRight.Pokemon ) ? _ai.UnitSim.GetWeatherFrom_Moveset( theirRight.Pokemon ) : default : default;

            var fieldWithWeather = _ai.UnitSim.BuildSimField();
            fieldWithWeather.Weather = theirWeather;
            fieldWithWeather.WeatherDuration = 5;

            var theirLeftWeatherSP = _ai.Projection.GetStrategicProfile( theirLeft, fieldWithWeather );
            var theirRightWeatherSP = theyHaveRight ? _ai.Projection.GetStrategicProfile( theirRight, fieldWithWeather ) : default;

            theyRelyOnWeather = theirLeftWeatherSP.DependsOnCurrentWeather || theirRightWeatherSP.DependsOnCurrentWeather;
            theyDesperatelyRelyOnWeather = theirLeftWeatherSP.DependsOnCurrentWeather && theirRightWeatherSP.DependsOnCurrentWeather;
            theirWeatherLikelyGivesThemAKO = theyRelyOnWeather && !theyHaveKO;
        }

        bool denyingWeatherEnablesWeather = theyCanChangeWeather && weRelyOnWeather && theyRelyOnWeather;
        bool denyingOurWeatherCripplesUs = theyCanChangeWeather && weDesperatelyRelyOnWeather && theyRelyOnWeather;

        bool stoppingTrickRoomEnablesTheirTailwind = false;
        bool stoppingTrickRoomEnablesTheirOffense = false;
        if( weWantToSetTrickRoom )
        {
            bool theyHaveTailwind = theirLeft.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter ) || ( theyHaveRight && theirRight.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter ) );
            stoppingTrickRoomEnablesTheirTailwind = theyHaveTailwind && theyCanStopTrickRoom;
            stoppingTrickRoomEnablesTheirOffense = theyHaveKO && theyCanStopTrickRoom;
        }

        bool stoppingScreensEnablesKO = false;
        bool stoppingScreensGivesThemScreenAdvantage = false;
        if( weWantToSetScreens )
        {
            stoppingScreensEnablesKO = theyHaveKO && theyCanStopScreens;
            stoppingScreensGivesThemScreenAdvantage = theyCanStopScreens && ( theirLeft.RoleProfile.Traits.Contains( RoleTrait.ScreenSetter ) || theyHaveRight && theirRight.RoleProfile.Traits.Contains( RoleTrait.ScreenSetter ) );
        }

        if( ourAvailableStrategies <= 1 )
            score += 1;

        if( denyingWeatherEnablesWeather || denyingOurWeatherCripplesUs )
            score += 2;

        if( theyDesperatelyRelyOnWeather && weDesperatelyRelyOnWeather && theyCanChangeWeather )
            score += 2;

        if( theirWeatherLikelyGivesThemAKO )
            score += 1;

        if( stoppingTrickRoomEnablesTheirTailwind )
            score += 2;

        if( stoppingTrickRoomEnablesTheirOffense )
            score += 1;

        if( stoppingScreensEnablesKO || stoppingScreensGivesThemScreenAdvantage )
            score += 2;

        //--------------------------------------------------------------------
        //--Opportunity Cost--------------------------------------------------
        //--------------------------------------------------------------------
        bool thisTurnIsBestDenialWindow = weWantToSetup && ( weHaveRedirector || weHaveFakeOutAvailable || weBlockPriority );
        bool denialResourceExpires = false;
        bool currentPositionEnablesDenail = false;
        bool taunterHasOneWindow = false;
        bool allowingSetupImmediatelyWeakensTheirBoard = false;

        bool theyHaveFakeOut = theirLeftCanFakeOut || theirRightCanFakeOut;
        bool theyHaveTaunt = theirLeft.RoleProfile.Traits.Contains( RoleTrait.Taunt ) || theyHaveRight && theirRight.RoleProfile.Traits.Contains( RoleTrait.Taunt );
        bool theyHaveEncore = theirLeft.RoleProfile.Traits.Contains( RoleTrait.Encore ) || theyHaveRight && theirRight.RoleProfile.Traits.Contains( RoleTrait.Encore );
        bool ourLeftIsSpeedSetter = ourLeft.RoleProfile.Traits.Contains( RoleTrait.TrickRoomSetter ) || ourLeft.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter );
        bool ourRightIsSpeedSetter = ourRight.RoleProfile.Traits.Contains( RoleTrait.TrickRoomSetter ) || ourRight.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter );
        bool theirLeftFaster_Left = ( theirLeft.Ability == AbilityID.Prankster && ourLeft.Ability != AbilityID.Prankster ) || theirLeft.Speed > ourLeft.Speed;
        bool theirLeftFaster_Right = weHaveRight && ( ( theirLeft.Ability == AbilityID.Prankster && ourRight.Ability != AbilityID.Prankster ) || theirLeft.Speed > ourRight.Speed );
        bool theirRightFaster_Left = theyHaveRight && ( ( theirRight.Ability == AbilityID.Prankster && ourLeft.Ability != AbilityID.Prankster ) || theirRight.Speed > ourLeft.Speed );
        bool theirRightFaster_Right = theyHaveRight && weHaveRight && ( ( theirRight.Ability == AbilityID.Prankster && ourRight.Ability != AbilityID.Prankster ) || theirRight.Speed > ourRight.Speed );
        bool theyImprisonOurLeft = ( theyCanStopTailwind || theyCanStopTrickRoom ) && ( theirLeft.Pokemon.CheckHasActiveMove( "Imprison" ) || theirRight.Pokemon.CheckHasActiveMove( "Imprison" ) );
        bool theyImprisonOurRight = ( theyCanStopTailwind || theyCanStopTrickRoom ) && ( theirLeft.Pokemon.CheckHasActiveMove( "Imprison" ) || theirRight.Pokemon.CheckHasActiveMove( "Imprison" ) );
        bool theyCanMatchTailwind = ( theirLeft.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter ) && ( ourLeftHasKO_Left || ourRightHasKO_Left ) ) || ( theyHaveRight && theirRight.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter ) && ( ourLeftHasKO_Right || ourRightHasKO_Right ) );

        taunterHasOneWindow = theyHaveTaunt && ( ( ourLeftIsSpeedSetter && ourLeft.Item != ItemBattleEffectID.MentalHerb && weHaveRight && !ourRight.RoleProfile.Traits.Contains( RoleTrait.RedirectionMove ) ) || ( weHaveRight && ourRightIsSpeedSetter && ourRight.Item != ItemBattleEffectID.MentalHerb && !ourLeft.RoleProfile.Traits.Contains( RoleTrait.RedirectionMove ) ) ) ;

        denialResourceExpires = theyHaveFakeOut || theyHaveTaunt || theyHaveEncore || ourLeftIsSpeedSetter || ourRightIsSpeedSetter || theirLeftFaster_Left || theirLeftFaster_Right || theirRightFaster_Left || theirRightFaster_Right || theyImprisonOurLeft || theyImprisonOurRight || theyCanMatchTailwind;
        currentPositionEnablesDenail = ourLeftIsSpeedSetter && ( theirLeftFaster_Left || theirRightFaster_Left ) || ourRightIsSpeedSetter && ( theirLeftFaster_Right || theirRightFaster_Right ) || taunterHasOneWindow;

        int ourCurrentSpeeds = 0;
        int ourTailwindSpeeds = 0;
        int ourTrickRoomReversals = 0;
        foreach( var ourUnit in ourActiveUnits )
        {
            foreach( var theirUnit in theirActiveUnits )
            {
                if( ourUnit.Speed > theirUnit.Speed )
                    ourCurrentSpeeds++;
                else
                    ourTrickRoomReversals++;

                if( ourUnit.Speed * 2 > theirUnit.Speed )
                    ourTailwindSpeeds++;
            }
        }

        bool tailwindGivesUsSpeedControl = weWantToSetTailwind && ourTailwindSpeeds - ourCurrentSpeeds > 0;
        bool trickRoomGivesUsBoardControl = weWantToSetTrickRoom && ourTrickRoomReversals > 0;

        allowingSetupImmediatelyWeakensTheirBoard = weDesperatelyRelyOnWeather || stoppingScreensEnablesKO || tailwindGivesUsSpeedControl || trickRoomGivesUsBoardControl;

        if( thisTurnIsBestDenialWindow )
            score += 1;

        if( denialResourceExpires )
            score += 1;

        if( allowingSetupImmediatelyWeakensTheirBoard )
            score += 1;
            
        return score;
    }

    private int ScoreEstablishDefensivePositionCommitment( List<PairObservationEvidence> poe )
    {
        int score = 0;

        //--Blackboard Information
        var ourActiveUnits = _ai.Blackboard.OurActiveBattleAIUnits;
        var theirActiveUnits = _ai.Blackboard.TheirActiveBattleAIUnits;

        var ourRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( ourActiveUnits[0].Pokemon ).Where( u => !ourActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();
        var theirRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( theirActiveUnits[0].Pokemon ).Where( u => !theirActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();

        var currentField = _ai.Blackboard.CurrentFieldSnapshot;
        var gp = _ai.Blackboard.GamePlan;

        var ourLeft = ourActiveUnits[0];
        var ourRight = ourActiveUnits.Count > 1 ? ourActiveUnits[1] : null;

        var theirLeft = theirActiveUnits[0];
        var theirRight = theirActiveUnits.Count > 1 ? theirActiveUnits[1] : null;

        bool weHaveRight = ourRight != null;
        bool theyHaveRight = theirRight != null;

        var ourLeftRP = ourLeft.RoleProfile;
        var ourLeftPR = ourLeftRP.PrimaryRole;
        bool ourLeftIsOffensive = ourLeftPR == RoleClass.BulkyAttacker || ourLeftPR == RoleClass.RevengeKiller || ourLeftPR == RoleClass.WallBreaker || ourLeftPR == RoleClass.SetupSweeper || ourLeftPR == RoleClass.Sweeper || ourLeftPR == RoleClass.TrickRoomAbuser;

        var ourRightRP = weHaveRight ? ourRight.RoleProfile : default;
        var ourRightPR = weHaveRight ? ourRightRP.PrimaryRole : default;
        bool ourRightIsOffensive = weHaveRight && ( ourRightPR == RoleClass.BulkyAttacker || ourRightPR == RoleClass.RevengeKiller || ourRightPR == RoleClass.WallBreaker || ourRightPR == RoleClass.SetupSweeper || ourRightPR == RoleClass.Sweeper || ourRightPR == RoleClass.TrickRoomAbuser );

        var theirLeftRP = theirLeft.RoleProfile;
        var theirLeftPR = theirLeftRP.PrimaryRole;

        var theirRightRP = theyHaveRight ? theirRight.RoleProfile : default;
        var theirRightPR = theyHaveRight ? theirRightRP.PrimaryRole : default;

        IBattleAIUnit defensiveCandidate = null;
        PairObservationProfile preservation = default;
        foreach( var e in poe )
        {
            if( TryGetObservationProfile( e, PairObservation.Preservation, out preservation ) )
            {
                if( defensiveCandidate == null )
                    defensiveCandidate = preservation.Top.Attacker;
                else if( defensiveCandidate.Pokemon != preservation.Top.Attacker.Pokemon )
                    Debug.LogError( $"You know what it is b" );
            }
        }

        //--Initialize Checks
        bool theyCanSetStatAlignedScreen = false;
        bool theyHaveBulkyOrSitrusRedirector = false;
        bool theyHaveWideGuardVsSpreadPressure = false;
        bool theyHaveABlockerVsOurWinCon = false;
        bool theyHaveADefensiveSwitchIncoming = false;
        bool theirDefensiveSwitchHasDefensiveUtility = false;
        bool theirDefensiveSwitchIsABlocker = false;
        bool theirDefensiveSwitchIsABlockerVsOurWincon = false;

        //--They can set a stat-aligned screen this turn
        bool theirLeftCanSetScreens = theirLeft.RoleProfile.Traits.Contains( RoleTrait.ScreenSetter );
        bool theirRightCanSetScreens = theyHaveRight && theirRight.RoleProfile.Traits.Contains( RoleTrait.ScreenSetter );

        if( theirLeftCanSetScreens || theirRightCanSetScreens )
        {
            if( ( ( ourLeftIsOffensive && ourLeftRP.Biases.Contains( RoleBias.Physical ) ) || ( ourRightIsOffensive && ourRightRP.Biases.Contains( RoleBias.Physical ) ) ) && ( theirLeft.Pokemon.CheckHasActiveMove( "Reflect" ) || theirRight.Pokemon.CheckHasActiveMove( "Reflect" ) ) )
            {
                theyCanSetStatAlignedScreen = true;
            }
            else if( ( ( ourLeftIsOffensive && ourLeftRP.Biases.Contains( RoleBias.Special ) ) || ( ourRightIsOffensive && ourRightRP.Biases.Contains( RoleBias.Special ) ) ) && ( theirLeft.Pokemon.CheckHasActiveMove( "Light Screen" ) || theirRight.Pokemon.CheckHasActiveMove( "Light Screen" ) ) )
            {
                theyCanSetStatAlignedScreen = true;
            }
        }
        
        //--They have a bulky or sitrus berry redirector
        bool theirLeftHasRedirection = theirLeft.RoleProfile.Traits.Contains( RoleTrait.RedirectionMove );
        bool theirRightHasRedirection = theirRight.RoleProfile.Traits.Contains( RoleTrait.RedirectionMove );

        if( theirLeftHasRedirection )
        {
            if( theirLeft.Item == ItemBattleEffectID.SitrusBerry || ( theirLeftRP.Biases.Contains( RoleBias.PhysicallyBulky ) && ( ( ourLeftIsOffensive && ourLeftRP.Biases.Contains( RoleBias.Physical ) ) || ( ourRightIsOffensive && ourRightRP.Biases.Contains( RoleBias.Physical ) ) ) ) )
            {
                theyHaveBulkyOrSitrusRedirector = true;
            }
            else if( theirLeft.Item == ItemBattleEffectID.SitrusBerry || ( theirLeftRP.Biases.Contains( RoleBias.SpeciallyBulky ) && ( ( ourLeftIsOffensive && ourLeftRP.Biases.Contains( RoleBias.Special ) ) || ( ourRightIsOffensive && ourRightRP.Biases.Contains( RoleBias.Special ) ) ) ) )
            {
                theyHaveBulkyOrSitrusRedirector = true;
            }
        }
        
        if( theirRightHasRedirection )
        {
            if( theirRight.Item == ItemBattleEffectID.SitrusBerry || ( theirRightRP.Biases.Contains( RoleBias.PhysicallyBulky ) && ( ( ourLeftIsOffensive && ourLeftRP.Biases.Contains( RoleBias.Physical ) ) || ( ourRightIsOffensive && ourRightRP.Biases.Contains( RoleBias.Physical ) ) ) ) )
            {
                theyHaveBulkyOrSitrusRedirector = true;
            }
            else if( theirRight.Item == ItemBattleEffectID.SitrusBerry || ( theirRightRP.Biases.Contains( RoleBias.SpeciallyBulky ) && ( ( ourLeftIsOffensive && ourLeftRP.Biases.Contains( RoleBias.Special ) ) || ( ourRightIsOffensive && ourRightRP.Biases.Contains( RoleBias.Special ) ) ) ) )
            {
                theyHaveBulkyOrSitrusRedirector = true;
            }
        }

        //--They have wide guard and the ai has spread pressure
        bool theirLeftHasWideGuard = theirLeft.Pokemon.CheckHasActiveMove( "Wide Guard" );
        bool theirRightHasWideGuard = theyHaveRight && theirRight.Pokemon.CheckHasActiveMove( "Wide Guard" );
        bool ourLeftHasSpreadPressure = ourLeft.RoleProfile.Traits.Contains( RoleTrait.SpreadAttack );
        bool ourRightHasSpreadPressure = weHaveRight && ourRight.RoleProfile.Traits.Contains( RoleTrait.SpreadAttack );

        if( ( theirLeftHasWideGuard || theirRightHasWideGuard ) && ( ourLeftHasSpreadPressure || ourRightHasSpreadPressure ) )
        {
            theyHaveWideGuardVsSpreadPressure = true;
        }

        //--They have a gameplan blocker on the field in front of our wincon
        bool ourWinConIsOnField = ourLeft.Pokemon == gp.OurPrimaryWinCon || ( weHaveRight && ourRight.Pokemon == gp.OurPrimaryWinCon );
        bool theirLeftIsBlocker = gp.TheirBlockers.Contains( theirLeft.Pokemon );
        bool theirRightIsBlocker = theyHaveRight && gp.TheirBlockers.Contains( theirRight.Pokemon );

        theyHaveABlockerVsOurWinCon = ourWinConIsOnField && ( theirLeftIsBlocker || theirRightIsBlocker );

        //--They have a defensive switch incoming
        if( defensiveCandidate != null )
        {
            theyHaveADefensiveSwitchIncoming = true;

            var candRP = defensiveCandidate.RoleProfile;

            //--Their defensive switch has sitrus berry, side healing, redirection, priority blocking, wide guard, defensive setup, or fake out
            bool ourLeftHasWaterAttack = ourLeft.Pokemon.CheckHasAttackingMoveOfType( PokemonType.Water );
            bool ourRightHasWaterAttack = weHaveRight && ourRight.Pokemon.CheckHasAttackingMoveOfType( PokemonType.Water );
            bool weHaveWaterAttack = ourLeftHasWaterAttack || ourRightHasWaterAttack;

            bool ourLeftHasElectricAttack = ourLeft.Pokemon.CheckHasAttackingMoveOfType( PokemonType.Electric );
            bool ourRightHasElectricAttack = weHaveRight && ourRight.Pokemon.CheckHasAttackingMoveOfType( PokemonType.Electric );
            bool weHaveElectricAttack = ourLeftHasElectricAttack || ourRightHasElectricAttack;

            bool candHasSitrus = defensiveCandidate.Item == ItemBattleEffectID.SitrusBerry;
            bool candHasSideHeal = _ai.UnitSim.PokemonHasMove_SideRecovery( defensiveCandidate.Pokemon );
            bool candHasRedirectionMove = candRP.Traits.Contains( RoleTrait.RedirectionMove );
            bool candHasUsefulRedirectionAbsorb = ( defensiveCandidate.Ability == AbilityID.LightningRod && weHaveElectricAttack ) || ( defensiveCandidate.Ability == AbilityID.StormDrain && weHaveWaterAttack );
            bool candHasWideGuard = defensiveCandidate.Pokemon.CheckHasActiveMove( "Wide Guard" );
            bool candHasDefensiveSetup = candRP.Traits.Contains( RoleTrait.PhysicallyDefensiveSetup ) || candRP.Traits.Contains( RoleTrait.SpeciallyDefensiveSetup );
            bool candHasFakeOut = candRP.Traits.Contains( RoleTrait.FakeOut );

            theirDefensiveSwitchHasDefensiveUtility = candHasSitrus || candHasSideHeal || candHasRedirectionMove || candHasUsefulRedirectionAbsorb || candHasWideGuard || candHasDefensiveSetup || candHasFakeOut;

            //--Their defensive switch is a gameplan blocker and we have our wincon on the field
            theirDefensiveSwitchIsABlocker = gp.TheirBlockers.Contains( defensiveCandidate.Pokemon );
            theirDefensiveSwitchIsABlockerVsOurWincon = theirDefensiveSwitchIsABlocker && ourWinConIsOnField;
        }

        //--------------------------------------------------------------------
        //--Investment--------------------------------------------------------
        //--------------------------------------------------------------------
        if( theyCanSetStatAlignedScreen )
            score += 1;

        if( theyHaveBulkyOrSitrusRedirector )
            score += 1;

        if( theyHaveWideGuardVsSpreadPressure )
            score += 1;

        if( theyHaveABlockerVsOurWinCon )
            score += 1;

        if( theyHaveADefensiveSwitchIncoming )
            score += 1;

        if( theirDefensiveSwitchHasDefensiveUtility )
            score += 1;

        if( theirDefensiveSwitchIsABlocker )
            score += 1;

        if( theirDefensiveSwitchIsABlockerVsOurWincon )
            score += 1;

        //--------------------------------------------------------------------
        //--Line Lock (small)-------------------------------------------------
        //--------------------------------------------------------------------
        bool theirLeftIsOffensive = _ai.UnitSim.PokemonIsOffensiveRole( theirLeft );
        bool theirRightIsOffensive = theyHaveRight && _ai.UnitSim.PokemonIsOffensiveRole( theirRight );

        bool theyDefensivelySupportOffensiveAlly = ( theyCanSetStatAlignedScreen || theyHaveBulkyOrSitrusRedirector ) && ( theirLeftIsOffensive || theirRightIsOffensive );
        bool theyCanDenyOurWincon = ourWinConIsOnField && ( theyHaveABlockerVsOurWinCon && ( theyCanSetStatAlignedScreen || theyHaveBulkyOrSitrusRedirector ) ) || ( theirDefensiveSwitchIsABlocker && theirDefensiveSwitchHasDefensiveUtility );

        if( theyDefensivelySupportOffensiveAlly )
            score += 1;

        if( theyCanDenyOurWincon )
            score += 1;

        //--------------------------------------------------------------------
        //--Opportunity Cost--------------------------------------------------
        //--------------------------------------------------------------------
        var returnUnit = preservation.IntentResult.CurrentActor;
        var returnUnitSP = returnUnit != null ? _ai.Projection.GetStrategicProfile( returnUnit ) : default;

        var ourLeftPTKO_Return = _ai.Projection.MakeUnitComparison( ourLeft, returnUnit ).Attacker.BestCurrentPTKO;
        var ourRightPTKO_Return = weHaveRight ? _ai.Projection.MakeUnitComparison( ourRight, returnUnit ).Attacker.BestCurrentPTKO : default;
        bool weCrippleReturnUnit = returnUnitSP.ProvidesSupport && ( ourLeft.RoleProfile.Traits.Contains( RoleTrait.Taunt ) || ourLeft.RoleProfile.Traits.Contains( RoleTrait.Encore ) || ( weHaveRight && ( ourRight.RoleProfile.Traits.Contains( RoleTrait.Taunt ) || ourRight.RoleProfile.Traits.Contains( RoleTrait.Encore ) ) ) );

        bool returnUnitIsImportant = returnUnit != null && returnUnitSP.BattlefieldFlipper || returnUnitSP.BattlefieldSetter || returnUnitSP.BlocksBattlefieldSetup || returnUnit.Pokemon == gp.TheirPrimaryWinCon || gp.TheirEnablers.Contains( returnUnit.Pokemon );
        
        bool safestDefensiveWindow = theyCanSetStatAlignedScreen || theyHaveBulkyOrSitrusRedirector || preservation.Top.OpponentPTKO <= PotentialToKO.TwoHKO;
        bool waitingExposesImportantUnit = returnUnitIsImportant && ( ourLeftPTKO_Return >= PotentialToKO.Risky || ourRightPTKO_Return >= PotentialToKO.Risky || weCrippleReturnUnit );
        bool ourMomentumSnowballs = ourWinConIsOnField && !theyCanDenyOurWincon;

        bool theirLeftSetsForRight = ( theirRightIsOffensive || theirRightIsBlocker ) && ( theirLeftCanSetScreens || theirLeftHasRedirection || theirLeftHasWideGuard );
        bool theirRightSetsForLeft = ( theirLeftIsOffensive || theirLeftIsBlocker ) && ( theirRightCanSetScreens || theirRightHasRedirection || theirRightHasWideGuard );
        bool theirDefensiveToolsMatter = theirLeftSetsForRight || theirRightSetsForLeft;

        if( safestDefensiveWindow )
            score += 1;

        if( waitingExposesImportantUnit )
            score += 1;

        if( ourMomentumSnowballs )
            score += 1;

        if( theirDefensiveToolsMatter )
            score += 1;

        return score;
    }

    private int ScoreAfterYouAttackCommitment( List<PairObservationEvidence> poe )
    {
        int score = 0;

        //--Blackboard Information
        var ourActiveUnits = _ai.Blackboard.OurActiveBattleAIUnits;
        var theirActiveUnits = _ai.Blackboard.TheirActiveBattleAIUnits;

        var ourRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( ourActiveUnits[0].Pokemon ).Where( u => !ourActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();
        var theirRemainingUnits = _ai.GetRemainingPartyAs_IBattleAIUnits( theirActiveUnits[0].Pokemon ).Where( u => !theirActiveUnits.Any( active => active.Pokemon == u.Pokemon ) ).ToList();

        var currentField = _ai.Blackboard.CurrentFieldSnapshot;
        var gp = _ai.Blackboard.GamePlan;

        var ourLeft = ourActiveUnits[0];
        var ourRight = ourActiveUnits.Count > 1 ? ourActiveUnits[1] : null;

        var theirLeft = theirActiveUnits[0];
        var theirRight = theirActiveUnits.Count > 1 ? theirActiveUnits[1] : null;

        bool weHaveRight = ourRight != null;
        bool theyHaveRight = theirRight != null;

        var ourLeftRP = ourLeft.RoleProfile;
        var ourLeftPR = ourLeftRP.PrimaryRole;
        bool ourLeftIsOffensive = ourLeftPR == RoleClass.BulkyAttacker || ourLeftPR == RoleClass.RevengeKiller || ourLeftPR == RoleClass.WallBreaker || ourLeftPR == RoleClass.SetupSweeper || ourLeftPR == RoleClass.Sweeper || ourLeftPR == RoleClass.TrickRoomAbuser;

        var ourRightRP = weHaveRight ? ourRight.RoleProfile : default;
        var ourRightPR = weHaveRight ? ourRightRP.PrimaryRole : default;
        bool ourRightIsOffensive = weHaveRight && ( ourRightPR == RoleClass.BulkyAttacker || ourRightPR == RoleClass.RevengeKiller || ourRightPR == RoleClass.WallBreaker || ourRightPR == RoleClass.SetupSweeper || ourRightPR == RoleClass.Sweeper || ourRightPR == RoleClass.TrickRoomAbuser );

        var theirLeftRP = theirLeft.RoleProfile;
        var theirLeftPR = theirLeftRP.PrimaryRole;

        var theirRightRP = theyHaveRight ? theirRight.RoleProfile : default;
        var theirRightPR = theyHaveRight ? theirRightRP.PrimaryRole : default;

        var ourCourt = ourLeft.CourtLocation == CourtLocation.TopCourt ? currentField.TopCourtConditions : currentField.BottomCourtConditions;
        var theirCourt = theirLeft.CourtLocation == CourtLocation.TopCourt ? currentField.TopCourtConditions : currentField.BottomCourtConditions;

        bool theirLeftAfterYou = false;
        bool theirRightAfterYou = false;
        PairObservationProfile afterYou = default;
        StatusThreatResult afterYouResult = default;
        MoveThreatResult attackResult = null;

        foreach( var e in poe )
        {
            if( TryGetObservationProfile( e, PairObservation.AfterYou, out afterYou ) )
            {
                if( afterYou.Top.Attacker.Pokemon == theirLeft.Pokemon )
                    theirLeftAfterYou = true;
                else if( afterYou.Top.Attacker.Pokemon == theirRight?.Pokemon )
                    theirRightAfterYou = true;

                
                afterYouResult = (StatusThreatResult)afterYou.IntentResult;
                break;
            }
        }

        foreach( var e in poe )
        {
            if( TryGetObservationProfile( e, PairObservation.Attack, out var attackObservation ) )
            {
                attackResult = (MoveThreatResult)attackObservation.IntentResult;
                break;
            }
        }

        //--Gather Scenario Information
        bool theyHaveAferYouUser = theirLeftAfterYou || theirRightAfterYou;

        if( !theyHaveAferYouUser )
            return -99;

        IBattleAIUnit afterYouUser = theirLeftAfterYou ? theirLeft : theirRight;
        IBattleAIUnit attacker = theirLeftAfterYou ? theirRight : theirLeft;

        var afterYouUserSP = _ai.Projection.GetStrategicProfile( afterYouUser );
        var attackerSP = _ai.Projection.GetStrategicProfile( attacker );

        bool attackerTOPTarget_Left = attackResult.Top.Opponent.Pokemon == ourLeft.Pokemon;
        bool attackerTOPTarget_Right = attackResult.Top.Opponent.Pokemon == ourRight.Pokemon;
        var attackerVs_OurLeft = _ai.Projection.MakeUnitComparison( attacker, ourLeft );
        var attackerVs_OurRight = theyHaveRight ? _ai.Projection.MakeUnitComparison( attacker, ourRight ) : default;
        var ourLeftVs_AfterYouUser = _ai.Projection.MakeUnitComparison( ourLeft, afterYouUser );
        var ourRightVs_AfterYouUser = weHaveRight ? _ai.Projection.MakeUnitComparison( ourRight, afterYouUser ) : default;

        var attackerPTKO_Left = attackerVs_OurLeft.Attacker.BestCurrentPTKO;
        var attackerPTKO_Right = theyHaveRight ? attackerVs_OurRight.Attacker.BestCurrentPTKO : PotentialToKO.Untouchable;
        var ourLeftPTKO_Attacker = attackerVs_OurLeft.Target.BestCurrentPTKO;
        var ourRightPTKO_Attacker = weHaveRight ? attackerVs_OurRight.Target.BestCurrentPTKO : PotentialToKO.Untouchable;

        //--Initialize Checks
        bool attackerHasStrongSpread = false;
        bool attackerBestBothIsSpread = false;
        bool attackerIsSlowest = false;
        bool attackerBenefitsAfterYou = false;

        var moveVs_Left = attackerVs_OurLeft.Attacker.CurrentPTKOs.Keys.First();
        var moveVs_Right = attackerVs_OurRight.Attacker.CurrentPTKOs.Keys.First();

        var leftMoveTarget = moveVs_Left.MoveSO.MoveTarget;
        var rightMoveTarget = theyHaveRight ? moveVs_Right.MoveSO.MoveTarget : MoveTarget.Self;

        if( ( leftMoveTarget == MoveTarget.OpposingSide || leftMoveTarget == MoveTarget.AllAdjacent ) && ( rightMoveTarget == MoveTarget.OpposingSide || rightMoveTarget == MoveTarget.AllAdjacent ) )
        {
            attackerHasStrongSpread = true;

            if( moveVs_Left.MoveSO.Name == moveVs_Right.MoveSO.Name )
                attackerBestBothIsSpread = true;
        }

        List<IBattleAIUnit> speedOrder = new()
        {
            ourLeft,
            theirLeft,
        };

        if( theyHaveRight )
            speedOrder.Add( theirRight );

        if( weHaveRight )
            speedOrder.Add( ourRight );

        speedOrder = speedOrder.OrderByDescending( u => u.Speed ).ThenByDescending( u => u.Pokemon.PokeSO.Speed ).ThenByDescending( u => ( u.Pokemon == theirLeft.Pokemon || u.Pokemon == theirRight?.Pokemon ) ).ToList();

        int order = 0;
        foreach( var unit in speedOrder )
        {
            order++;

            if( unit.Pokemon == attacker.Pokemon )
                break;
        }

        if( order == speedOrder.Count )
            attackerIsSlowest = true;
        else if( order == speedOrder.Count - 1 )
            attackerBenefitsAfterYou = true;

        bool afterYouUserGoesFirst = speedOrder[0].Pokemon == afterYouUser.Pokemon;
        bool afterYouUserGoesSecond = speedOrder[1].Pokemon == afterYouUser.Pokemon;

        bool attackerUsesSpreadMove = attackResult?.Move.MoveSO.MoveTarget == MoveTarget.OpposingSide || attackResult?.Move.MoveSO.MoveTarget == MoveTarget.AllAdjacent;
        bool attackerDoesGoodDamage = attackerPTKO_Left >= PotentialToKO.Dangerous || attackerPTKO_Right >= PotentialToKO.Dangerous;
        bool attackerSpreadHitsHard = attackerHasStrongSpread && ( ( attackerPTKO_Left >= PotentialToKO.Dangerous && attackerPTKO_Right >= PotentialToKO.TwoHKO ) || ( attackerPTKO_Left >= PotentialToKO.TwoHKO && attackerPTKO_Right >= PotentialToKO.Dangerous ) || ( attackerPTKO_Left >= PotentialToKO.Risky && attackerPTKO_Right >= PotentialToKO.Risky ) );
        bool attackerSpreadThreatensSevereDamage = attackerHasStrongSpread && attackerPTKO_Left >= PotentialToKO.Dangerous && attackerPTKO_Right >= PotentialToKO.Dangerous;
        bool attackerIsNaturallySlow = attacker.RoleProfile.Biases.Contains( RoleBias.SlowSpeed ) || attacker.RoleProfile.Biases.Contains( RoleBias.TrickRoomSpeed );
        bool attackerDependsOnAlly = attackerSP.DependsOnFastAlly;

        bool afterYouUserIsFast = afterYouUser.RoleProfile.Biases.Contains( RoleBias.MiddlingSpeed ) || afterYouUser.RoleProfile.Biases.Contains( RoleBias.FastSpeed );
        bool afterYouUsersSpeedIsCurrentlyBoosted = _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( afterYouUser.Pokemon, currentField.Weather ) || theirCourt.ContainsKey( CourtConditionID.Tailwind ) || afterYouUser.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.Unburden ) || afterYouUser.StatStages[Stat.Speed] > 0;

        //--Investment
        if( afterYouUserGoesFirst )
            score += 2;
        else if( afterYouUserGoesSecond )
            score += 1;

        if( attackerBenefitsAfterYou )
            score += 1;

        if( attackerDoesGoodDamage && speedOrder.Count - order <= 1 )
            score += 1;

        if( attackerIsNaturallySlow )
            score += 1;

        if( attackerBestBothIsSpread )
            score += 2;
        else if( attackerHasStrongSpread )
            score += 1;
        
        if( attackerDependsOnAlly )
            score += 1;

        if( attackerUsesSpreadMove && afterYouUserGoesFirst )
        {
            //--Spread move reward
            score += 1;

            if( attackerPTKO_Left >= PotentialToKO.Dangerous )
                score += 1;

            if( attackerPTKO_Right >= PotentialToKO.Dangerous )
                score += 1;
        }
        else if( afterYouUserGoesFirst )
        {
            if( attackerPTKO_Left >= PotentialToKO.Dangerous )
                score += 1;

            if( attackerPTKO_Right >= PotentialToKO.Dangerous )
                score += 1;
        }

        if( afterYouUserIsFast )
            score += 1;

        if( afterYouUsersSpeedIsCurrentlyBoosted )
            score += 2;

        //--Line Lock
        // both members are strongly dependent on each other
        if( attackerDependsOnAlly && ( afterYouUser.RoleProfile.PrimaryArchetype != RoleClassArchetype.Offensive || afterYouUserSP.EnablesAlly ) )
            score += 1;

        // the attacker loses a huge amount of effectiveness without the After You user
        if( attackerIsNaturallySlow )
        {
            if( ourLeftPTKO_Attacker >= PotentialToKO.Risky )
                score += 1;

            if( ourRightPTKO_Attacker >= PotentialToKO.Risky )
                score += 1;

            if( attackerIsSlowest )
                score += 1;
        }

        // the After You user has little independent value
        if( afterYouUser.RoleProfile.Signals.StatusMoveCount >= 2 )
            score += 1;

        // the partner is exceptionally dependent on being moved first
        if( attackerIsSlowest && attackerDependsOnAlly && attacker.RoleProfile.PrimaryArchetype == RoleClassArchetype.Offensive )
            score += 1;

        // their remaining Pokemon don't reproduce the same offensive structure
        int remainingAfterYouAbusers = 0;
        int remainingAbusersWithBigSpread = 0;
        foreach( var mon in theirRemainingUnits )
        {
            var primaryArch = mon.RoleProfile.PrimaryArchetype;
            var biases = mon.RoleProfile.Biases;
            if( ( biases.Contains( RoleBias.SlowSpeed ) || biases.Contains( RoleBias.TrickRoomSpeed ) ) && primaryArch == RoleClassArchetype.Offensive )
            {
                remainingAfterYouAbusers++;

                foreach( var m in mon.ActiveMoves )
                {
                    if( m.MovePower >= 90 && ( m.MoveSO.MoveTarget == MoveTarget.AllAdjacent || m.MoveSO.MoveTarget == MoveTarget.OpposingSide ) )
                        remainingAbusersWithBigSpread++;
                }
            }
        }

        if( remainingAfterYouAbusers <= 0 )
            score += 2;
        else if( remainingAfterYouAbusers == 1 && remainingAbusersWithBigSpread <= 0 )
            score += 1;

        // switching either member substantially reduces their immediate pressure
        if( attackerDoesGoodDamage && attackerIsNaturallySlow && afterYouUserGoesFirst && attacker.RoleProfile.PrimaryArchetype == RoleClassArchetype.Offensive && afterYouUser.RoleProfile.Signals.StatusMoveCount >= 2 )
            score += 1;

        // the opponent has already positioned the pair in a way that makes the line difficult to replace
        if( attackerIsSlowest && afterYouUserGoesFirst && afterYouUsersSpeedIsCurrentlyBoosted )
            score += 1;

        //--Irreversibility
        // After You produces an immediate KO.
        // After You produces a large amount of irreversible damage.
        if( afterYouUserGoesFirst )
        {
            if( attackerPTKO_Left == PotentialToKO.OHKO || attackerPTKO_Right == PotentialToKO.OHKO )
                score += 2;
            else if( attackerPTKO_Left >= PotentialToKO.Risky && attackerPTKO_Right >= PotentialToKO.Risky )
                score += 1;
        }

        // The attacker becomes difficult to stop once it gets the first action.
        if( afterYouUserGoesFirst && attackerHasStrongSpread && attackerBestBothIsSpread )
            score += 1;

        // The After You user becomes vulnerable after spending its turn enabling the attack.
        // The opposing Pokemon that could have disrupted the attacker is removed before it gets to act.
        if( attackerUsesSpreadMove )
        {
            if( attackerPTKO_Left <= PotentialToKO.Dangerous )
            {
                if( ourLeftVs_AfterYouUser.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous )
                {
                    if( afterYouUser.BeginningHPR == 1f && afterYouUser.Item != ItemBattleEffectID.FocusSash )
                        score += 1;
                    else if( afterYouUser.Item != ItemBattleEffectID.FocusSash )
                        score += 1;
                }
            }

            if( attackerPTKO_Right <= PotentialToKO.Dangerous )
            {
                if( ourRightVs_AfterYouUser.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous )
                {
                    if( afterYouUser.BeginningHPR == 1f && afterYouUser.Item != ItemBattleEffectID.FocusSash )
                        score += 1;
                    else if( afterYouUser.Item != ItemBattleEffectID.FocusSash )
                        score += 1;
                }
            }
        }
        else
        {
            if( attackerTOPTarget_Left && attackerPTKO_Left <= PotentialToKO.Dangerous )
            {
                if( ourLeftVs_AfterYouUser.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous )
                {
                    if( afterYouUser.BeginningHPR == 1f && afterYouUser.Item != ItemBattleEffectID.FocusSash )
                        score += 1;
                    else if( afterYouUser.Item != ItemBattleEffectID.FocusSash )
                        score += 1;
                }
            }

            if( attackerTOPTarget_Right && attackerPTKO_Right <= PotentialToKO.Dangerous )
            {
                if( ourRightVs_AfterYouUser.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous )
                {
                    if( afterYouUser.BeginningHPR == 1f && afterYouUser.Item != ItemBattleEffectID.FocusSash )
                        score += 1;
                    else if( afterYouUser.Item != ItemBattleEffectID.FocusSash )
                        score += 1;
                }
            }
        }

        //--Opportunity Cost
        // They lose the speed window

        // If Torkoal is currently slower than both opposing Pokemon, but After You lets it move first, then waiting may mean:

        // opponent attacks Torkoal
        // opponent sets something up
        // opponent removes Lilligant
        // opponent changes the board
        // opponent switches into something that handles Torkoal
        // opponent establishes Trick Room/Tailwind
        // opponent otherwise destroys the condition that made the After You line powerful
        bool theyCanLoseSpeedWindow = false;
        bool attackerTakesBigDamage = false;
        bool attackerIsKOThreatened = false;

        // The target can escape
        bool oneOfOursIsImportant = false;
        bool oneOfOursIsDangerous = false;
        bool weHaveABench = ourRemainingUnits.Count > 0;

        // If the intended target can switch safely now, then waiting may lose the opportunity to exploit After You against it.

        // The attacker can be disabled
        bool weHaveActiveDisable = false;
        bool weHaveBenchedDisable = false;

        // If the opponent has a faster Taunt, Encore, Fake Out, priority, etc., then the window may disappear.
        bool ourLeftPranksterDisruption = ourLeft.Ability == AbilityID.Prankster && ourLeft.RoleProfile.Biases.Contains( RoleBias.Disruptive );
        bool ourRightPranksterDisruption = weHaveRight && ourRight.Ability == AbilityID.Prankster && ourRight.RoleProfile.Biases.Contains( RoleBias.Disruptive );
        bool weHavePriorityDisruption = ourLeftPranksterDisruption || ourRightPranksterDisruption;

        bool ourLeftCanFakeoutAfterYou = _ai.CanUseFakeOut( ourLeft, afterYouUser );
        bool ourLeftCanFakeoutAttacker = _ai.CanUseFakeOut( ourLeft, attacker );

        bool ourRightCanFakeoutAfterYou = weHaveRight && _ai.CanUseFakeOut( ourRight, afterYouUser );
        bool ourRightCanFakeoutAttacker = weHaveRight && _ai.CanUseFakeOut( ourRight, attacker );

        if( afterYouUsersSpeedIsCurrentlyBoosted )
        {
            if( _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( afterYouUser.Pokemon, currentField.Weather ) )
            {
                if( ourLeft.Ability == AbilityID.Prankster || ourLeft.Speed > afterYouUser.Speed )
                {
                    if( _ai.UnitSim.PokemonHasWeatherSetter_Move( ourLeft.Pokemon ) )
                        theyCanLoseSpeedWindow = true;

                    if( weHaveRight && _ai.UnitSim.PokemonHasWeatherSetter_Move( ourRight.Pokemon ) )
                        theyCanLoseSpeedWindow = true;
                }
            }

            if( afterYouUser.StatStages[Stat.Speed] > 0 )
            {
                if( ourLeft.RoleProfile.Traits.Contains( RoleTrait.SpeedDebuffer ) )
                    theyCanLoseSpeedWindow = true;

                if( weHaveRight && ourRight.RoleProfile.Traits.Contains( RoleTrait.SpeedDebuffer ) )
                    theyCanLoseSpeedWindow = true;
            }
        }

        if( theirCourt.TryGetValue( CourtConditionID.Tailwind, out var theirTailwind ) )
        {
            if( theirTailwind <= 2 )
                theyCanLoseSpeedWindow = true;

            if( ourCourt.TryGetValue( CourtConditionID.Tailwind, out var ourTailwind ) )
            {
                if( ourTailwind > theirTailwind )
                    theyCanLoseSpeedWindow = true;
            }
            else
            {
                if( ourLeft.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter ) )
                    theyCanLoseSpeedWindow = true;

                if( weHaveRight && ourRight.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter ) )
                    theyCanLoseSpeedWindow =  true;
            }
        }

        if( ourLeft.Speed > attacker.Speed && ourLeftPTKO_Attacker >= PotentialToKO.Risky )
            attackerTakesBigDamage = true;

        if( ourLeft.Speed > attacker.Speed && ourLeftPTKO_Attacker >= PotentialToKO.Dangerous )
        {
            attackerIsKOThreatened = true;
            oneOfOursIsDangerous = true;
        }

        if( weHaveRight )
        {
            if( ourRight.Speed > attacker.Speed && ourRightPTKO_Attacker >= PotentialToKO.Risky )
                attackerTakesBigDamage = true;

            if( ourRight.Speed > attacker.Speed && ourRightPTKO_Attacker >= PotentialToKO.Dangerous )
            {
                attackerIsKOThreatened = true;
                oneOfOursIsDangerous = true;
            }
        }

        bool weHaveWinconOnField = gp.OurPrimaryWinCon == ourLeft.Pokemon || ourRight?.Pokemon == gp.OurPrimaryWinCon;
        bool weHaveBlockerOnField = gp.OurBlockers.Contains( ourLeft.Pokemon ) || weHaveRight && gp.OurBlockers.Contains( ourRight.Pokemon );
        bool weHaveEnablerOnField = gp.OurEnablers.Contains( ourLeft.Pokemon ) || weHaveRight && gp.OurEnablers.Contains( ourRight.Pokemon );
        oneOfOursIsImportant = weHaveWinconOnField || weHaveBlockerOnField || weHaveEnablerOnField;

        weHaveActiveDisable = ourLeft.Pokemon.CheckHasActiveMove( "Disable" ) || weHaveRight && ourRight.Pokemon.CheckHasActiveMove( "Disable" );
        foreach( var mon in ourRemainingUnits )
        {
            if( mon.Pokemon.CheckHasActiveMove( "Disable" ) )
                weHaveBenchedDisable = true;
        }

        if( theyCanLoseSpeedWindow )
            score += 1;

        if( attackerTakesBigDamage )
        {
            score += 1;

            if( attackerIsKOThreatened )
                score += 1;
        }

        if( oneOfOursIsImportant )
            score += 1;

        if( oneOfOursIsDangerous )
            score += 1;

        if( weHaveABench )
        {
            score += 1;
        }

        if( weHaveActiveDisable || weHaveBenchedDisable )
            score += 1;

        if( weHavePriorityDisruption )
            score += 1;

        if( ourLeftCanFakeoutAfterYou || ourRightCanFakeoutAfterYou )
            score += 1;

        if( ourLeftCanFakeoutAttacker || ourRightCanFakeoutAttacker )
            score += 1;

        if( oneOfOursIsImportant )
            score += 1;

        return score;
    }
}

public struct ThreatInteractionMatrix
{
    public Dictionary<Pokemon, ThreatIntentResult> EnemyLeft;
    public Dictionary<Pokemon, ThreatIntentResult> EnemyRight;
}

public struct PatternIntentMatch
{
    public bool Found;
    public bool IsPrimary;
    public ThreatIntentResult MatchingTIR;
    public Intent MatchingIntent;
    public int Evidence;
    public float RelativeStrength;

    public List<PairObservation> Observations;
}

public struct PatternIntentPack
{
    public PatternIntentMatch UnitLeftMatch;
    public PatternIntentMatch UnitRightMatch;
    public bool PackFound;
}

public struct PairIntentResult
{
    public PairStrategyIntent PrimaryStrategy;
    public PairStrategyIntent SecondaryStrategy;

    // public float CompetitionConfidence;
    // public float PrimaryConfidence;
    // public float SecondaryConfidence;

    public Dictionary<PairStrategy, PairStrategyIntent> Strategies;
    public List<PairObservationEvidence> Poe;
}

public class PairStrategyIntent
{
    public Intent LeftIntent;
    public Intent RightIntent;

    public PairStrategy Strategy;

    public int PatternScore;
    public int ObservationScore;
    public int OpportunityScore;
    public int CommitmentScore;
    public int FinalScore;

    public float InternalConfidence;
}

public enum PairPattern
{
    CoveredAttack,
    CoveredSetup,
    AttackAndSetup,
    AfterYouAndAttack, // <-- do this next before getting started on GetSupportiveStatusResponses()!
    SpeedControl,
    FocusFire,
    SpreadPressure,
    BoardControl,
    WeatherChange,
    TerrainChange,
    DefensivePlay,
    PivotPlay,
    DoubleSwitch,
    CoveredSwitch,
    ProtectAndSwitch,
    AttackAndSwitch,
    DoubleProtect,
    FakeOutSupport,
    RedirectionSupport,
    DoubleAttack,
    DisruptionPressure,
    TrickRoomSetting, //--Can be used together with Protected Setup for further clarity. Can also imply reversing trick room intent
    TailwindSetting, //--Can be used together with Protected Setup for further clarity. Can also imply matching tailwind intent
}

public enum PairStrategy
{
    None, //--Requirements, Signals
    EstablishTrickRoom,
    //--Requires: Trick Room Observation
    //--Patterns: CoveredSetup/Setup
    //--Observations: Fake Out/Redirection/Guard support, cover, setup, trick room
    //--Opportunity Signals: Denial, Initiative, Guaranteed Value, Board Control
    //--Commitment Signals: Investment, Line Lock, Irreversibility, Opportunity Cost

    EstablishTailwind,
    //--Requires: Tailwind Observation
    //--Patterns: CoveredSetup, SetupPlusAttack
    //--Observations: Cover, Redirection, Guard, SpeedControl
    //--Opportunity Signals : Momentum, Board Control, Pressure, Guaranteed Value
    //--Commitment Signals: Investment, Line Lock (small), Irreversibility (small), Opportunity Cost

    WeatherPivot,
    //--Requires: WeatherChange Pattern AND Switch Observation
    //--Patterns: CoveredSwitch, ProtectAndSwitch, AttackAndSwitch
    //--Observations: PivotPressure, Preservation, Switch, WeatherChange, PivotMove, Cover, Protect
    //--Opportunity Signals: Board Control, Conversion, Momentum
    //--Commitment Signals: Investment, Line Lock, Irreversibility (small), Opportunity Cost

    SecureImmediateKO,
    //--Requires FocusFire AND Double Attack AND Attack
    //--Patterns: FocusFire, DoubleAttack
    //--Observations: Attack, RevengeKO
    //--Opportunity: Immediate Removal, Conversion, Pressure
    //--Commitment Signals: Investment, Line Lock (small), Opportunity Cost

    ApplyBoardPressure,
    //--Requires: Double Attack Pattern OR Focus Fire Pattern OR Attack Observation
    //--Patterns: DoubleAttack, FocusFire, CoveredAttack,
    //--Observations: Attack
    //--Opportunity: Pressure, Momentum, Board Control, Punish
    //--Commitment Signals: Investment

    PreserveTempo,
    //--Requires: Switch Observation, Pivot Pressure Observation
    //--Patterns: CoveredSwitch
    //--Observations: Switch, PivotPressure, Preservation
    //--Opportunity: Momentum, Initiative, Guaranteed Value
    //--Commitment Signals: Investment, Line Lock, Opportunity Cost

    DenyOpponentSetup,
    //--Requires: DisruptionPressure Pattern, Interruption Observation
    //--Patterns: FakeOutSupport, FocusFire
    //--Observations: Cover, SpeedControl, FakeOut/Redirection/Guard Support, StatusDisruption
    //--Opportunity: Denial, Initiative, Pressure
    //--Commitment Signals: Investment, Line Lock, Opportunity Cost

    EstablishDefensivePosition,
    //--Requires Screens Observation OR Wide Guard Observation OR Protect Observation OR Switch observation
    //--Patterns: CoveredSetup, DefensivePlay, PivotPlay, ProtectAndSwitch, CoveredSwitch
    //--Observations: Protect, Cover, ScreensSupport, GuardSupport
    //--Opportunity: Resource, Momentum, Guaranteed Value
    //--Commitment Signals: Investment, Line Lock (small), Opportunity Cost


    AfterYouAttack,
    //--Requires: AfterYouAndAttack pattern
    //--Patterns: AfterYouAndAttack
    //--Observations: AfterYou, StatusMove, SpeedControl, Attack, SpreadAttack
    //--Opportunity: Initiative, Pressure, Momentum, BoardControl, GuaranteedValue, ImmediateRemoval
    //--Commitment Signals: Investment, Line Lock, Irreversibility, Opportunity Cost
}

public enum PairObservation
{
    //--Intrinsic Observations. We'll keep these in the same order as the FindXIntent() helpers.
    Attack,
    Setup,
    Cover,
    SpeedControl,
    Switch,
    Protect,

    //--Derived Observations. These are specific observations about why an intrinsic observation was made.
    FakeOutSupport,
    RedirectionSupport,
    GuardSupport, //--Wide Guard, Quick Guard, a switch into a Pokemon with Armor Tail or Psychic Surge perhaps
    TrickRoom,
    Tailwind,
    WeatherChange,
    TerrainChange,
    EntryHazard,
    RevengeKO,
    PivotPressure,
    PivotMove,
    Preservation,
    StatDebuff,
    StatusMove,
    StatBoost,
    ScreensSupport,
    Interruption,
    StatusDisruption,
    AfterYou,
    Quash,
    SpreadAttack,

    //--Coordinated Observations. Some observations can only exist with the existence of two PIMs, such as Double Attack and Focus Fire.
    DoubleAttack,
    FocusFire,
}

public struct PairObservationEvidence
{
    public PairObservation Type;
    public PairPattern SourcePattern;
    public ThreatIntentResult SourceIntent;
    public int Weight;
    public bool IsPrimary;
}

public struct PairObservationProfile
{
    public IActionResult IntentResult;
    public TurnOutcomeProjection Top;
    public IBattleAIUnit Us;
    public IBattleAIUnit OurAlly;
    public IBattleAIUnit Them;
    public IBattleAIUnit TheirAlly;
    public Move Move;
    
    public bool WeHaveAlly;
    public bool TheyHaveAlly;
}
