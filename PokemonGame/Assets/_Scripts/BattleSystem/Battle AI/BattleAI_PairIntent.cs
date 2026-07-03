using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
using UnityEngine;

public class BattleAI_PairIntent
{
    private readonly BattleAI _ai;

    public BattleAI_PairIntent( BattleAI ai )
    {
        _ai = ai;
    }
    
    public PairIntentResult GetPairIntentResult( ThreatInteractionMatrix tim )
    {
        PairIntentResult pir = new();

        GetPatterns( tim );

        return pir;
    }

    public ThreatInteractionMatrix BuildThreatInteractionMatrix( IBattleAIUnit leftUnit, ThreatIntentResult leftTIRLeft, ThreatIntentResult leftTIRRight, IBattleAIUnit rightUnit, ThreatIntentResult rightTIRLeft, ThreatIntentResult rightTIRRight )
    {
        ThreatInteractionMatrix tim = new()
        {
            UnitLeft = new()
            {
                { leftUnit.Pokemon, leftTIRLeft },
                { leftUnit.Pokemon, leftTIRRight },
            },

            UnitRight = new()
            {
                { rightUnit.Pokemon, rightTIRLeft },
                { rightUnit.Pokemon, rightTIRRight },
            },
        };

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
        if( DetectPattern_OffensivePressure( tim ) is var offensivePressure && offensivePressure.PackFound )
            patterns.Add( PairPattern.OffensivePressure, offensivePressure );

        //--Defensive Reset
        if( DetectPattern_CoveredSwitch( tim ) is var coveredSwitch && coveredSwitch.PackFound )
            patterns.Add( PairPattern.ProtectAndSwitch, coveredSwitch );

        LogPatterns( patterns );

        return patterns;
    }

    private void LogPatterns( Dictionary<PairPattern, PatternIntentPack> patterns )
    {
        CustomLogSession patternLog = new();

        patternLog.Add( $"================================" );
        patternLog.Add( $"=====[Pair Intent Patterns]=====" );
        patternLog.Add( $"================================" );

        foreach ( var kvp in patterns )
        {
            var pattern = kvp.Key;
            var pip = kvp.Value;

            var leftIntent = pip.UnitLeftMatch.MatchingIntent;
            var rightIntent = pip.UnitRightMatch.MatchingIntent;
            //--Get Left Intent Result
            var leftIntentResult = leftIntent.IntentResult;

            //--Get Right Intent Result
            var rightIntentResult = rightIntent.IntentResult;

            var leftUnit = leftIntentResult.Top.Attacker;
            var rightUnit = rightIntentResult.Top.Attacker;

            var leftTarget = leftIntentResult.Top.Opponent;
            var rightTarget = rightIntentResult.Top.Opponent;

            patternLog.Add( $"" );
            patternLog.Add( $"===[{pattern}]===" );
            patternLog.Add( $"Left Unit: {leftUnit.Name}, Intent: {leftIntent.IntentType}, Is Primary: {pip.UnitLeftMatch.IsPrimary}, Evidence: {pip.UnitLeftMatch.Evidence}, Relative Strength: {pip.UnitLeftMatch.RelativeStrength}" );
            patternLog.Add( $"Right Unit: {rightUnit.Name}, Intent: {rightIntent.IntentType}, Is Primary: {pip.UnitRightMatch.IsPrimary}, Evidence: {pip.UnitRightMatch.Evidence}, Relative Strength: {pip.UnitRightMatch.RelativeStrength}" );
            patternLog.Add( $"Left Target: {leftTarget.Name}" );
            patternLog.Add( $"Right Target: {rightTarget.Name}" );

            var leftType = leftIntent.IntentType;
            if( leftType == IntentType.Attack || leftType == IntentType.Setup || leftType == IntentType.OffensiveStatus )
                patternLog.Add( $"Left Unit {leftUnit.Name} attacking with {leftIntentResult.Move.MoveSO.Name}" );
            else
                patternLog.Add( $"Left Unit {leftUnit.Name} is switching into: {leftIntentResult.Candidate.Name}" );

            var rightType = rightIntent.IntentType;
            if( rightType == IntentType.Attack || rightType == IntentType.Setup || rightType == IntentType.OffensiveStatus )
                patternLog.Add( $"Left Unit {rightUnit.Name} attacking with {rightIntentResult.Move.MoveSO.Name}" );
            else
                patternLog.Add( $"Left Unit {rightUnit.Name} is switching into: {rightIntentResult.Candidate.Name}" );

            patternLog.Add( $"" );
        }

        Debug.Log( patternLog.ToString() );
        string path = Application.persistentDataPath + "/Pair Intent Patterns_Log.txt";
        System.IO.File.AppendAllText( path, patternLog.ToString() + "\n" + "\n" + "\n" + "\n" + "\n" );
        patternLog.Clear();
    }

    private PatternIntentPack Detect_CoveredSetup( ThreatInteractionMatrix tim )
    {
        PatternIntentPack pip = new()
        {
            UnitLeftMatch = default,
            UnitRightMatch = default,
            PackFound = false,
        };

        var unitLeft_SetupIntent = FindSetupIntent( tim.UnitLeft );
        var unitRight_SetupIntent = FindSetupIntent( tim.UnitRight );

        if( !unitLeft_SetupIntent.Found && !unitRight_SetupIntent.Found )
            return pip;

        var unitLeft_CoverIntent = FindCoverAllyIntent( tim.UnitLeft );
        var unitRight_CoverIntent = FindCoverAllyIntent( tim.UnitRight );

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

        var unitLeft_AttackIntent = FindAttackIntent( tim.UnitLeft );

        if( !unitLeft_AttackIntent.Found )
            return pip;

        var unitRight_AttackIntent = FindAttackIntent( tim.UnitRight );

        if( !unitRight_AttackIntent.Found )
            return pip;

        bool leftIsPrimary = false;
        bool rightIsPrimary = false;
        List<( ThreatIntentResult left, ThreatIntentResult right )> attackOverlapTIRs = new();
        foreach( var kvpLeft in tim.UnitLeft )
        {
            var leftTIR = kvpLeft.Value;
            var leftPrimary = leftTIR.PrimaryIntent;
            var leftSecondary = leftTIR.SecondaryIntent;
            MoveThreatResult leftMTR = null;

            if( leftTIR.PrimaryIntent.IntentType == IntentType.Attack )
            {
                leftMTR = (MoveThreatResult)leftTIR.PrimaryIntent.IntentResult;
                leftIsPrimary = true;
            }
            else if( leftTIR.CheckSecondaryIntent && leftTIR.SecondaryIntent.IntentType == IntentType.Attack )
            {
                leftMTR = (MoveThreatResult)leftTIR.SecondaryIntent.IntentResult;
                leftIsPrimary = false;
            }
            else
            {
                leftIsPrimary = false;
                continue;
            }

            var leftTarget = leftMTR.Target.Pokemon;

            foreach( var kvpRight in tim.UnitRight )
            {
                var rightTIR = kvpRight.Value;
                var rightPrimary = rightTIR.PrimaryIntent;
                var rightSecondary = rightTIR.SecondaryIntent;
                MoveThreatResult rightMTR = null;

                if( rightTIR.PrimaryIntent.IntentType == IntentType.Attack )
                {
                    rightMTR = (MoveThreatResult)rightTIR.PrimaryIntent.IntentResult;
                    rightIsPrimary = true;
                }
                else if( rightTIR.CheckSecondaryIntent && rightTIR.SecondaryIntent.IntentType == IntentType.Attack )
                {
                    rightMTR = (MoveThreatResult)rightTIR.SecondaryIntent.IntentResult;
                    rightIsPrimary = false;
                }
                else
                {
                    rightIsPrimary = false;
                    continue;
                }

                var rightTarget = rightMTR.Target.Pokemon;

                if( leftTarget == rightTarget )
                {
                    attackOverlapTIRs.Add( ( leftTIR, rightTIR ) );
                    break; //--Eventually change it so we store all possible overlaps, and then handle the primary bool checks appropriately.
                }
            }
        }

        if( attackOverlapTIRs.Count > 0 )
        {
            PatternIntentMatch leftPim = new()
            {
                Found = true,
                IsPrimary = leftIsPrimary,
                MatchingTIR = attackOverlapTIRs[0].left,
                MatchingIntent = leftIsPrimary ? attackOverlapTIRs[0].left.PrimaryIntent : attackOverlapTIRs[0].left.SecondaryIntent,
                Evidence = leftIsPrimary ? attackOverlapTIRs[0].left.PrimaryIntent.Evidence : attackOverlapTIRs[0].left.SecondaryIntent.Evidence,
            };

            leftPim.RelativeStrength = leftPim.Evidence / leftPim.MatchingTIR.TotalEvidence;

            PatternIntentMatch rightPim = new()
            {
                Found = true,
                IsPrimary = rightIsPrimary,
                MatchingTIR = attackOverlapTIRs[0].right,
                MatchingIntent = rightIsPrimary ? attackOverlapTIRs[0].right.PrimaryIntent : attackOverlapTIRs[0].right.SecondaryIntent,
                Evidence = rightIsPrimary ? attackOverlapTIRs[0].right.PrimaryIntent.Evidence : attackOverlapTIRs[0].right.SecondaryIntent.Evidence,
            };

            rightPim.RelativeStrength = rightPim.Evidence / rightPim.MatchingTIR.TotalEvidence;

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

        var unitLeft_SpeedControlIntent = FindSpeedControlIntent( tim.UnitLeft );
        var unitRight_SpeedControlIntent = FindSpeedControlIntent( tim.UnitRight );

        if( !unitLeft_SpeedControlIntent.Found && !unitRight_SpeedControlIntent.Found )
            return pip;
        else if( unitLeft_SpeedControlIntent.Found && !unitRight_SpeedControlIntent.Found )
        {
            pip.UnitLeftMatch = unitLeft_SpeedControlIntent;
            pip.PackFound = true;
        }
        else if( !unitLeft_SpeedControlIntent.Found && unitRight_SpeedControlIntent.Found )
        {
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

    private PatternIntentPack DetectPattern_OffensivePressure( ThreatInteractionMatrix tim )
    {
        PatternIntentPack pip = new()
        {
            UnitLeftMatch = default,
            UnitRightMatch = default,
            PackFound = false,
        };

        var unitLeft_AttackIntent = FindAttackIntent( tim.UnitLeft );

        if( !unitLeft_AttackIntent.Found )
            return pip;

        var unitRight_AttackIntent = FindAttackIntent( tim.UnitRight );

        if( !unitRight_AttackIntent.Found )
            return pip;

        if( unitLeft_AttackIntent.Found && unitRight_AttackIntent.Found )
        {
            pip.UnitLeftMatch = unitLeft_AttackIntent;
            pip.UnitRightMatch = unitRight_AttackIntent;
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

        var unitLeft_SwitchIntent = FindSwitchIntent( tim.UnitLeft );
        var unitRight_SwitchIntent = FindSwitchIntent( tim.UnitRight );

        if( !unitLeft_SwitchIntent.Found && !unitRight_SwitchIntent.Found )
            return pip;

        var unitLeft_CoverAllyIntent = FindCoverAllyIntent( tim.UnitLeft );
        var unitRight_CoverAllyIntent = FindCoverAllyIntent( tim.UnitRight );

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

    private PatternIntentMatch FindAttackIntent( Dictionary<Pokemon, ThreatIntentResult> threatInteractions )
    {
        PatternIntentMatch pim = new()
        {
            Found = false,
            MatchingTIR = default,
            Evidence = 0,
            RelativeStrength = 0f,
        };

        foreach( var interaction in threatInteractions )
        {
            var tir = interaction.Value;
            bool found = false;
            bool isPrimary = false;

            if( ( tir.PrimaryIntent.IntentType == IntentType.Attack ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.IntentType == IntentType.Attack ) )
            {
                bool primary = tir.PrimaryIntent.IntentType == IntentType.Attack;
                found = true;
                isPrimary = primary;
            }

            if( found )
            {
                pim.Found = true;
                pim.MatchingTIR = tir;
                pim.MatchingIntent = isPrimary ? tir.PrimaryIntent : tir.SecondaryIntent;
                pim.IsPrimary = isPrimary;
                pim.Evidence = pim.MatchingIntent.Evidence;
                pim.RelativeStrength = pim.MatchingIntent.Evidence / (float)tir.TotalEvidence;
                break;
            }
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

        foreach( var interaction in threatInteractions )
        {
            var tir = interaction.Value;
            bool found = false;
            bool isPrimary = false;

            if( ( tir.PrimaryIntent.IntentType == IntentType.Setup ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.IntentType == IntentType.Setup ) )
            {
                bool primary = tir.PrimaryIntent.IntentType == IntentType.Setup;
                found = true;
                isPrimary = primary;
            }

            if( ( tir.PrimaryIntent.IntentType == IntentType.SupportiveStatus ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.IntentType == IntentType.SupportiveStatus ) )
            {
                //--if SupportiveStatus.Type == some battlefield move like reflect or tailwind
                bool primary = tir.PrimaryIntent.IntentType == IntentType.SupportiveStatus;
                found = true;
                isPrimary = primary;
            }

            if( ( tir.PrimaryIntent.IntentType == IntentType.OffensiveStatus ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.IntentType == IntentType.OffensiveStatus ) )
            {
                bool primary = tir.PrimaryIntent.IntentType == IntentType.OffensiveStatus;
                StatusThreatResult offStatus = primary ? (StatusThreatResult)tir.PrimaryIntent.IntentResult : (StatusThreatResult)tir.SecondaryIntent.IntentResult;

                if( offStatus.StatusType == OffensiveStatusType.EntryHazard )
                {
                    found = true;
                    isPrimary = primary;
                }
            }

            if( found )
            {
                pim.Found = true;
                pim.MatchingTIR = tir;
                pim.MatchingIntent = isPrimary ? tir.PrimaryIntent : tir.SecondaryIntent;
                pim.IsPrimary = isPrimary;
                pim.Evidence = pim.MatchingIntent.Evidence;
                pim.RelativeStrength = pim.MatchingIntent.Evidence / (float)tir.TotalEvidence;
                break;
            }
        }

        return pim;
    }

    //--This is "Protection", just named differently to avoid future confusion with a "protect" intent for using the actual moves protect, detect, wide guard, etc.
    private PatternIntentMatch FindCoverAllyIntent( Dictionary<Pokemon, ThreatIntentResult> threatInteractions )
    {
        PatternIntentMatch pim = new()
        {
            Found = false,
            MatchingTIR = default,
            Evidence = 0,
            RelativeStrength = 0f,
        };

        foreach( var interaction in threatInteractions )
        {
            var tir = interaction.Value;
            bool found = false;
            bool isPrimary = false;

            if( tir.PrimaryIntent.IntentType == IntentType.Attack || tir.CheckSecondaryIntent && tir.SecondaryIntent.IntentType == IntentType.Attack )
            {
                bool primary = tir.PrimaryIntent.IntentType == IntentType.Attack;
                MoveThreatResult mtr = primary ? (MoveThreatResult)tir.PrimaryIntent.IntentResult : (MoveThreatResult)tir.SecondaryIntent.IntentResult;
                var move = mtr.Move;
                var name = move.MoveSO.Name;
                var effects = move.MoveSO.MoveEffects;

                if( name == "Fake Out" )
                {
                    found = true;
                    isPrimary = primary;
                }

                if( effects.TransientStatus == TransientConditionID.CenterOfAttention )
                {
                    found = true;
                    isPrimary = primary;
                }
            }

            if( ( tir.PrimaryIntent.IntentType == IntentType.SupportiveStatus ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.IntentType == IntentType.SupportiveStatus ) )
            {
                //--if SupportiveStatus.Type == some battlefield move like reflect or tailwind
                bool primary = tir.PrimaryIntent.IntentType == IntentType.SupportiveStatus;
                StatusThreatResult str = primary ? (StatusThreatResult)tir.PrimaryIntent.IntentResult : (StatusThreatResult)tir.SecondaryIntent.IntentResult;
                var move = str.Move;
                var effects = move.MoveSO.MoveEffects;
                found = true;
                isPrimary = primary;

                if( effects.CourtCondition == CourtConditionID.WideGuard || effects.CourtCondition == CourtConditionID.QuickGuard )
                {
                    found = true;
                    isPrimary = primary;
                }
            }

            if( found )
            {
                pim.Found = true;
                pim.MatchingTIR = tir;
                pim.MatchingIntent = isPrimary ? tir.PrimaryIntent : tir.SecondaryIntent;
                pim.IsPrimary = isPrimary;
                pim.Evidence = pim.MatchingIntent.Evidence;
                pim.RelativeStrength = pim.MatchingIntent.Evidence / (float)tir.TotalEvidence;
                break;
            }

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

        foreach( var interaction in threatInteractions )
        {
            tir = interaction.Value;

            //--Field speed control
            if( ( tir.PrimaryIntent.IntentType == IntentType.SupportiveStatus ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.IntentType == IntentType.SupportiveStatus ) )
            {
                bool primary = tir.PrimaryIntent.IntentType == IntentType.SupportiveStatus;
                var suppStatus = primary ? (StatusThreatResult)tir.PrimaryIntent.IntentResult : (StatusThreatResult)tir.SecondaryIntent.IntentResult;
                var effects = suppStatus.Move.MoveSO.MoveEffects;

                if( effects.CourtCondition == CourtConditionID.Tailwind || effects.FieldCondition == FieldConditionID.TrickRoom )
                {
                    found = true;
                    isPrimary = primary;
                    break;
                }
            }

            //--Direct speed debuff of the opponent
            if( ( tir.PrimaryIntent.IntentType == IntentType.OffensiveStatus ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.IntentType == IntentType.OffensiveStatus ) )
            {
                bool primary = tir.PrimaryIntent.IntentType == IntentType.OffensiveStatus;
                var offStatus = primary ? (StatusThreatResult)tir.PrimaryIntent.IntentResult : (StatusThreatResult)tir.SecondaryIntent.IntentResult;
                var effects = offStatus.Move.MoveSO.MoveEffects;

                if( offStatus.StatusType == OffensiveStatusType.StatDebuff )
                {
                    if( effects.StatChangeList != null && effects.StatChangeList.Count > 0 )
                    {
                        foreach( var sc in effects.StatChangeList )
                        {
                            if( sc.Stat == Stat.Speed && sc.Change < 0 )
                            {
                                found = true;
                                isPrimary = primary;
                                break;
                            }
                        }
                    }
                }
            }

            //--Attack such as icy wind that lowers opponent speed guaranteed
            if( ( tir.PrimaryIntent.IntentType == IntentType.Attack ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.IntentType == IntentType.Attack ) )
            {
                bool primary = tir.PrimaryIntent.IntentType == IntentType.Attack;
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
                            break;
                        }
                    }
                }
            }
        }

        if( found )
        {
            pim.Found = true;
            pim.MatchingTIR = tir;
            pim.MatchingIntent = isPrimary ? tir.PrimaryIntent : tir.SecondaryIntent;
            pim.IsPrimary = isPrimary;
            pim.Evidence = pim.MatchingIntent.Evidence;
            pim.RelativeStrength = pim.MatchingIntent.Evidence / (float)tir.TotalEvidence;
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

        foreach( var interaction in threatIntentInteractions )
        {
            tir = interaction.Value;

            if( tir.PrimaryIntent.IntentType == IntentType.DefensiveSwitch || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.IntentType == IntentType.DefensiveSwitch ) )
            {
                isPrimary = tir.PrimaryIntent.IntentType == IntentType.DefensiveSwitch;
                found = true;
            }

            if( tir.PrimaryIntent.IntentType == IntentType.OffensiveSwitch || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.IntentType == IntentType.OffensiveSwitch ) )
            {
                isPrimary = tir.PrimaryIntent.IntentType == IntentType.OffensiveSwitch;
                found = true;
            }
        }

        if( found )
        {
            pim.Found = true;
            pim.MatchingTIR = tir;
            pim.MatchingIntent = isPrimary ? tir.PrimaryIntent : tir.SecondaryIntent;
            pim.IsPrimary = isPrimary;
            pim.Evidence = pim.MatchingIntent.Evidence;
            pim.RelativeStrength = pim.MatchingIntent.Evidence / (float)tir.TotalEvidence;
        }

        return pim;
    }
}

public struct ThreatInteractionMatrix
{
    public Dictionary<Pokemon, ThreatIntentResult> UnitLeft;
    public Dictionary<Pokemon, ThreatIntentResult> UnitRight;
}

public struct PatternIntentMatch
{
    public bool Found;
    public bool IsPrimary;
    public ThreatIntentResult MatchingTIR;
    public Intent MatchingIntent;
    public int Evidence;
    public float RelativeStrength;
}

public struct PatternIntentPack
{
    public PatternIntentMatch UnitLeftMatch;
    public PatternIntentMatch UnitRightMatch;
    public bool PackFound;
}

public struct PairIntentResult
{
    public ThreatIntentResult TIRLeft;
    public ThreatIntentResult TIRRight;

    public PairStrategy PrimaryStrategy;
    public PairStrategy SecondaryStrategy;

    public float PrimaryConfidence;
    public float SecondaryConfidence;

    public Dictionary<PairPattern, PatternIntentPack> Patterns;
}

public enum PairPattern
{
    CoveredSetup,
    SpeedControl,
    FocusFire,
    SpreadPressure,
    BoardControl,
    WeatherChange,
    TerrainChange,
    DefensiveStall,
    PivotPlay,
    Disruption,
    DoubleSwitch,
    CoveredSwitch,
    ProtectAndSwitch,
    DoubleProtect,
    FakeOutSupport,
    RedirectionSupport,
    OffensivePressure,
    DisruptionPressure,
    TrickRoomSetting, //--Can be used together with Protected Setup for further clarity. Can also imply reversing trick room intent
    TailwindSetting, //--Can be used together with Protected Setup for further clarity. Can also imply matching tailwind intent
}

public enum PairStrategy
{
    
}
