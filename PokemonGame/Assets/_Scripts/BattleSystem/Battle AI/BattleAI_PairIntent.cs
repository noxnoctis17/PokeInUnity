using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class BattleAI_PairIntent
{
    private readonly BattleAI _ai;

    public BattleAI_PairIntent( BattleAI ai )
    {
        _ai = ai;
    }

    //--GamePlanAlignment scoring blocks may want to have ally synergy! don't forget to visit those! --07/02/26
    
    public PairIntentResult GetPairIntentResult( ThreatInteractionMatrix tim )
    {
        PairIntentResult pir = new();

        GetPatterns( tim );

        return pir;
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
            patterns.Add( PairPattern.ProtectAndSwitch, coveredSwitch );

        if( patterns.Count > 0 )
            LogPatterns( patterns );

        return patterns;
    }

    private PatternIntentMatch CreatePIM( ThreatIntentResult tir, bool isPrimary, bool found = false )
    {
        PatternIntentMatch pim = new()
        {
            Found = false,
            MatchingTIR = default,
            Evidence = 0,
            RelativeStrength = 0f,
        };

        if( found )
            return FinishFoundPIM( ref pim, tir, isPrimary );

        return pim;
    }

    private PatternIntentMatch FinishFoundPIM( ref PatternIntentMatch pim, ThreatIntentResult tir, bool isPrimary )
    {
        pim.Found = true;
        pim.MatchingTIR = tir;
        pim.MatchingIntent = isPrimary ? tir.PrimaryIntent : tir.SecondaryIntent;
        pim.IsPrimary = isPrimary;
        pim.Evidence = pim.MatchingIntent.Evidence;
        pim.RelativeStrength = pim.MatchingIntent.Evidence / (float)tir.TotalEvidence;

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

            //--Attackers are the ai's opponents
            var leftEnemy = leftIntentResult.Top.Attacker;
            var rightEnemy = rightIntentResult.Top.Attacker;

            //--Opponents are the ai's units
            var leftEnemyTarget = leftIntentResult.Top.Opponent;
            var rightEnemyTarget = rightIntentResult.Top.Opponent;

            patternLog.Add( $"===[{pattern}]===" );
            patternLog.Add( $"Matching pattern for Left had {leftEnemy.Name} vs {leftEnemyTarget.Name}, Intent: {leftIntent.IntentType}, Is Primary: {pip.UnitLeftMatch.IsPrimary}, Evidence: {pip.UnitLeftMatch.Evidence}, Relative Strength: {pip.UnitLeftMatch.RelativeStrength}" );
            patternLog.Add( $"Matching pattern for Right had {rightEnemy.Name} vs {rightEnemyTarget.Name}, Intent: {rightIntent.IntentType}, Is Primary: {pip.UnitRightMatch.IsPrimary}, Evidence: {pip.UnitRightMatch.Evidence}, Relative Strength: {pip.UnitRightMatch.RelativeStrength}" );

            var leftType = leftIntent.IntentType;
            if( leftType == IntentType.Attack || leftType == IntentType.Setup || leftType == IntentType.OffensiveStatus )
                patternLog.Add( $"Enemy Unit {leftEnemy.Name} is attacking {leftEnemyTarget.Name} with {leftIntentResult.Move.MoveSO.Name}" );
            else
                patternLog.Add( $"Enemy Unit {leftEnemy.Name} is switching into: {leftIntentResult.Candidate.Name} due to our {leftEnemyTarget.Name}" );

            var rightType = rightIntent.IntentType;
            if( rightType == IntentType.Attack || rightType == IntentType.Setup || rightType == IntentType.OffensiveStatus )
                patternLog.Add( $"Enemy Unit {rightEnemy.Name} is attacking {rightEnemyTarget.Name} with {rightIntentResult.Move.MoveSO.Name}" );
            else
                patternLog.Add( $"Enemy Unit {rightEnemy.Name} is switching into: {rightIntentResult.Candidate.Name} due to our {rightEnemyTarget.Name}" );

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
        List<( ThreatIntentResult left, ThreatIntentResult right )> attackOverlapTIRs = new();
        foreach( var kvpLeft in tim.EnemyLeft )
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

            foreach( var kvpRight in tim.EnemyRight )
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
            PatternIntentMatch leftPim = CreatePIM( attackOverlapTIRs[0].left, leftIsPrimary, true );
            PatternIntentMatch rightPim = CreatePIM( attackOverlapTIRs[0].right, rightIsPrimary, true );

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

    private PatternIntentMatch FindAttackIntent( Dictionary<Pokemon, ThreatIntentResult> threatInteractions )
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

            if( ( tir.PrimaryIntent.IntentType == IntentType.Attack ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.IntentType == IntentType.Attack ) )
            {
                bool primary = tir.PrimaryIntent.IntentType == IntentType.Attack;
                found = true;
                isPrimary = primary;
                break;
            }
        }

        if( found )
        {
            pim = FinishFoundPIM( ref pim, tir, isPrimary );
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

        foreach( var interaction in threatInteractions )
        {
            tir = interaction.Value;

            if( ( tir.PrimaryIntent.IntentType == IntentType.Setup ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.IntentType == IntentType.Setup ) )
            {
                bool primary = tir.PrimaryIntent.IntentType == IntentType.Setup;
                found = true;
                isPrimary = primary;
                break;
            }

            if( ( tir.PrimaryIntent.IntentType == IntentType.SupportiveStatus ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.IntentType == IntentType.SupportiveStatus ) )
            {
                //--if SupportiveStatus.Type == some battlefield move like reflect or tailwind
                bool primary = tir.PrimaryIntent.IntentType == IntentType.SupportiveStatus;
                found = true;
                isPrimary = primary;
                break;
            }

            if( ( tir.PrimaryIntent.IntentType == IntentType.OffensiveStatus ) || ( tir.CheckSecondaryIntent && tir.SecondaryIntent.IntentType == IntentType.OffensiveStatus ) )
            {
                bool primary = tir.PrimaryIntent.IntentType == IntentType.OffensiveStatus;
                StatusThreatResult offStatus = primary ? (StatusThreatResult)tir.PrimaryIntent.IntentResult : (StatusThreatResult)tir.SecondaryIntent.IntentResult;

                if( offStatus.OffensiveStatusType == OffensiveStatusType.EntryHazard )
                {
                    found = true;
                    isPrimary = primary;
                    break;
                }
            }
        }

        if( found )
        {
            pim = FinishFoundPIM( ref pim, tir, isPrimary );
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

        ThreatIntentResult tir = default;
        bool found = false;
        bool isPrimary = false;

        foreach( var interaction in threatInteractions )
        {
            tir = interaction.Value;
        
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
                    break;
                }

                if( effects.TransientStatus == TransientConditionID.CenterOfAttention )
                {
                    found = true;
                    isPrimary = primary;
                    break;
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
                    break;
                }
            }
        }

        if( found )
        {
            pim = FinishFoundPIM( ref pim, tir, isPrimary );
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
            pim = FinishFoundPIM( ref pim, tir, isPrimary );
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
            pim = FinishFoundPIM( ref pim, tir, isPrimary );
        }

        return pim;
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
    DoubleAttack,
    DisruptionPressure,
    TrickRoomSetting, //--Can be used together with Protected Setup for further clarity. Can also imply reversing trick room intent
    TailwindSetting, //--Can be used together with Protected Setup for further clarity. Can also imply matching tailwind intent
}

public enum PairStrategy
{
    
}
