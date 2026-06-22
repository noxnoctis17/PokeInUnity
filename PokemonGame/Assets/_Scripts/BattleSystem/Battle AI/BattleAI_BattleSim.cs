using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum SimModuleType { Attack, Switch, Setup, OffensiveStatus, SupportiveStatus, Heal, Protect }
public class BattleAI_BattleSim
{
    private BattleAI _ai;
    private BattleAI_UnitSim _unitSim;
    private BattleAI_Projection _proj;
    private List<Action<SimulatedUnit, List<SimulatedUnit>, SimulatedField>> _roundEndPhases;
    private int _rounds;
    private const float HP_EPSILON = 0.0009f;
    public Dictionary<string, Func<IBattleAIUnit, IBattleAIUnit, Move, bool>> MoveSuccess { get; private set; }

    public BattleAI_BattleSim( BattleAI ai )
    {
        _ai = ai;
        _unitSim = _ai.UnitSim;
        _proj = _ai.Projection;
        MoveSuccessDicInit();
        BuildRoundEndPhaseList();
        _rounds = 0;
    }

    public SimulationModule BuildSimModule( SimModuleType type, int priority, SimulatedUnit attacker, SimulatedUnit opponent )
    {
        Action<SimulatedUnit, SimulatedUnit, SimulatedField> module = type switch
        {
            SimModuleType.Attack => RunAttackModule,
            SimModuleType.Switch => RunSwitchModule,
            SimModuleType.Setup => RunSetupModule,
            SimModuleType.OffensiveStatus => RunOffensiveStatusModule,
            _ => RunAttackModule,
        };

        SimulationModule sm = new( type, priority, attacker, opponent, module );

        return sm;
    }

    public BattleSimEvent BuildBattleSimEvent( PotentialToKO attPTKO, PotentialToKO oppPTKO, SimulationPackage attackerPack, SimulationPackage opponentPack, SimulatedField field )
    {
        const int priority_offset = 7;

        var attacker = attackerPack.SimUnit;
        var opponent = opponentPack.SimUnit;

        _unitSim.TurnSimLog.Add( $"===[Building Battle Simulation Event ({attacker.Name}'s {attackerPack.ModuleType} vs {opponent.Name}'s {opponentPack.ModuleType})]===" );

        var units = new List<SimulatedUnit> { attacker, opponent };
        units.Sort( ( a, b ) => b.Speed.CompareTo( a.Speed ) );

        bool attMovesFirst = false;
        int attackerPriority = attackerPack.ModuleType == SimModuleType.Switch ? 99 : attacker.MTR?.Move != null ? ( (int)attacker.MTR.Move.Priority - priority_offset ) : ( (int)MovePriority.Zero - priority_offset );
        int opponentPriority = opponentPack.ModuleType == SimModuleType.Switch ? 99 : opponent.MTR?.Move != null ? ( (int)opponent.MTR.Move.Priority - priority_offset ) : ( (int)MovePriority.Zero - priority_offset );

        //--First we check the actual priority systems. if the systems don't equal each other, we let them determine module order
        //--If they are the same, then we have to break the tie by actual unit speed. If speeds are the same, we will assume the opponent goes first.
        if( attackerPriority != opponentPriority )
        {
            attMovesFirst = attackerPriority > opponentPriority;
        }
        else
        {
            attackerPriority = attacker.Speed;
            opponentPriority = opponent.Speed;
            attMovesFirst = attacker.Speed > opponent.Speed;

            if( attacker.Speed == opponent.Speed )
            {
                attackerPriority = 0;
                opponentPriority = 1000;
            }
        }

        //--Build Sim Module
        var attackerModule = BuildSimModule( attackerPack.ModuleType, attackerPriority, attacker, opponent );
        var opponentModule = BuildSimModule( opponentPack.ModuleType, opponentPriority, opponent, attacker );

        List<SimulationModule> modules = new(){ attackerModule, opponentModule };
        modules = modules.OrderByDescending( m => m.Priority ).ToList();

        _unitSim.TurnSimLog.Add( $"[Turn Simulation] Attacker ({attacker.Name}) Speed: {attacker.Speed}. Opponent ({opponent.Name}) Speed: {opponent.Speed}." );
        _unitSim.TurnSimLog.Add( $"[Turn Simulation] Attacker ({attacker.Name}) Move Priority: {attackerPriority}. Opponent ({opponent.Name}) Move Priority {opponentPriority}." );
        _unitSim.TurnSimLog.Add( $"[Turn Simulation] Attacker ({attacker.Name}) Moves First: {attMovesFirst}." );
        _unitSim.TurnSimLog.Add( $"" );

        BattleSimEvent bse = new()
        {
            Attacker = attacker,
            Opponent = opponent,
            ActiveUnits = units,
            SimModules = modules,

            AttackerPTKO = attPTKO,
            OpponentPTKO = oppPTKO,

            Field = field,

            AttackerMovesFirst = attMovesFirst,
            AttackerCanAct = _unitSim.CanActOnTurn( attacker ),
            OpponentCanAct = _unitSim.CanActOnTurn( opponent ),
        };

        _unitSim.TurnSimLog.Add( $"Attacker {bse.Attacker.Name} (HPR: {bse.Attacker.BeginningHPR}), PTKO: {bse.AttackerPTKO}" );
        _unitSim.TurnSimLog.Add( $"Opponent {bse.Opponent.Name} (HPR: {bse.Opponent.BeginningHPR}), PTKO: {bse.OpponentPTKO}" );
        _unitSim.TurnSimLog.Add( $"" );

        return bse;
    }

    private TurnOutcomeProjection BuildTOP( BattleSimEvent bse, bool log = false )
    {
        TurnOutcomeProjection top = new()
        {
            Attacker = bse.Attacker,
            Opponent = bse.Opponent,

            Field = bse.Field, //--We currently do not make any increments to field. this feature should be expanded on to account for duration tics and such.

            AttackerPTKO = bse.AttackerPTKO,
            OpponentPTKO = bse.OpponentPTKO,

            Attacker_EndOfTurnHP = bse.Attacker.CurrentHPR,
            Opponent_EndOfTurnHP = bse.Opponent.CurrentHPR,

            Attacker_DiesBeforeActing = bse.Attacker_DiesBeforeActing,
            Opponent_DiesBeforeActing = bse.Opponent_DiesBeforeActing,

            AttackerCanAct = bse.AttackerCanAct,
            OpponentCanAct = bse.OpponentCanAct,

            MutualKO = bse.Attacker.CurrentHPR <= 0f && bse.Opponent.CurrentHPR <= 0f,
            AttackerMovedFirst = bse.AttackerMovesFirst,
        };

        _unitSim.LogTop( top );
        // top.SimulationLog = _unitSim.TurnSimLog.ToString();

        if( log )
            Debug.Log( _unitSim.TurnSimLog.ToString() );

        _unitSim.TurnSimLog.Clear();

        _rounds = 0;

        return top;
    }

    public TurnOutcomeProjection BuildIntentTOP( ActionType action, object ourResult, ThreatIntentResult tir )
    {
        MoveThreatResult ourMTR = null;
        MoveThreatResult theirMTR = null;

        IBattleAIUnit attacker = null;
        IBattleAIUnit opponent = null;

        SimModuleType attackerModule = SimModuleType.Attack;
        SimModuleType opponentModule = SimModuleType.Attack;

        //----------------------------------------------------------------------------
        //--[Our Action]--------------------------------------------------------------
        //----------------------------------------------------------------------------
        switch( action )
        {
            case ActionType.Attack:

                var attack = (MoveThreatResult)ourResult;
                ourMTR = attack;
                attacker = attack.Top.Attacker;
                attackerModule = SimModuleType.Attack;

            break;

            case ActionType.DefensiveSwitch:

                var defSwitch = (SwitchCandidateResult)ourResult;
                attacker = defSwitch.Top.Attacker;
                attackerModule = SimModuleType.Switch;

                ourMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = attacker,
                    TargetBattleUnit = null,
                    Move = null,
                    EstimatedDamage = 0,
                };

            break;

            case ActionType.OffensiveSwitch:

                var offSwitch = (SwitchCandidateResult)ourResult;
                attacker = offSwitch.Top.Attacker;
                attackerModule = SimModuleType.Switch;

                ourMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = attacker,
                    TargetBattleUnit = null,
                    Move = null,
                    EstimatedDamage = 0,
                };

            break;

            case ActionType.Setup:

                var setup = (SetupThreatResult)ourResult;
                attacker = setup.Top.Attacker;
                attackerModule = SimModuleType.Setup;

                ourMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = setup.Target,
                    TargetBattleUnit = setup.TargetBattleUnit,
                    Move = setup.Move,
                    EstimatedDamage = 0f,
                };

            break;

            case ActionType.OffensiveStatus:

                var offStatus = (StatusThreatResult)ourResult;
                attacker = offStatus.Top.Attacker;
                attackerModule = SimModuleType.OffensiveStatus;

                ourMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = offStatus.Target,
                    TargetBattleUnit = offStatus.TargetBattleUnit,
                    Move = offStatus.Move,
                    EstimatedDamage = 0f,
                };

            break;
        }

        //----------------------------------------------------------------------------
        //--[Their Action]------------------------------------------------------------
        //----------------------------------------------------------------------------
        switch( tir.PrimaryIntent )
        {
            case IntentType.Attack:

                var attack = (MoveThreatResult)tir.IntentObject;
                opponent = attack.Top.Attacker;
                opponentModule = SimModuleType.Attack;
                theirMTR = attack;

            break;

            case IntentType.DefensiveSwitch:

                var defSwitch = (SwitchCandidateResult)tir.IntentObject;
                opponent = defSwitch.Top.Attacker;
                opponentModule = SimModuleType.Switch;

                theirMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = opponent,
                    TargetBattleUnit = null,
                    Move = null,
                    EstimatedDamage = 0,
                };

            break;

            case IntentType.OffensiveSwitch:

                var offSwitch = (SwitchCandidateResult)tir.IntentObject;
                opponent = offSwitch.Top.Attacker;
                opponentModule = SimModuleType.Switch;

                theirMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = opponent,
                    TargetBattleUnit = null,
                    Move = null,
                    EstimatedDamage = 0,
                };

            break;

            case IntentType.Setup:

                var setup = (SetupThreatResult)tir.IntentObject;
                opponent = setup.Top.Attacker;
                opponentModule = SimModuleType.Setup;

                theirMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = setup.Target,
                    TargetBattleUnit = setup.TargetBattleUnit,
                    Move = setup.Move,
                    EstimatedDamage = 0f,
                };

            break;

            case IntentType.OffensiveStatus:

                var offStatus = (StatusThreatResult)tir.IntentObject;
                opponent = offStatus.Top.Attacker;
                opponentModule = SimModuleType.OffensiveStatus;

                theirMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = offStatus.Target,
                    TargetBattleUnit = offStatus.TargetBattleUnit,
                    Move = offStatus.Move,
                    EstimatedDamage = 0f,
                };

            break;
        }

        float ourHPR                        = attacker.BeginningHPR;
        float theirHPR                      = opponent.BeginningHPR;
        
        var ourEDR                          = _proj.Get_EstimatedDamageResult( attacker, opponent, ourMTR );
        var theirEDR                        = _proj.Get_EstimatedDamageResult( opponent, attacker, theirMTR );

        PotentialToKOResult ourPTKOR        = _proj.Get_PotentialToKOResult( ourEDR, ourMTR, theirHPR );
        PotentialToKOResult theirPTKOR      = _proj.Get_PotentialToKOResult( theirEDR, theirMTR, ourHPR );

        var fieldSim                        = _ai.UnitSim.BuildSimField();

        var attackerSimUnit                 = _ai.UnitSim.BuildSimUnit( attacker, ourHPR, ourMTR, fieldSim );
        var opponentSimUnit                 = _ai.UnitSim.BuildSimUnit( opponent, theirHPR, theirMTR, fieldSim );

        SimulationPackage attackerPack      = new(){ SimUnit = attackerSimUnit, ModuleType = attackerModule };
        SimulationPackage opponentPack      = new(){ SimUnit = opponentSimUnit, ModuleType = opponentModule };
        
        var bse = BuildBattleSimEvent( ourPTKOR.PTKO, theirPTKOR.PTKO, attackerPack, opponentPack, fieldSim );
        return RunSimulation( bse, true );

        // switch( action )
        // {
        //     case ActionType.Attack:
        //         return SimulateAttackRound( battleSimContext, $"Intent TOP for our Attack Action vs their {tir.PrimaryIntent}" );

        //     case ActionType.DefensiveSwitch:
        //         if( tir.PrimaryIntent == IntentType.DefensiveSwitch || tir.PrimaryIntent == IntentType.OffensiveSwitch )
        //             return SimulateSwitchRound( battleSimContext, true, true, $"Intent TOP for our Defensive Switch vs their {tir.PrimaryIntent}" );
        //         else
        //             return SimulateSwitchRound( battleSimContext, true, false, $"Intent TOP for our Defensive Switch vs their {tir.PrimaryIntent}" );

        //     case ActionType.OffensiveSwitch:
        //         if( tir.PrimaryIntent == IntentType.DefensiveSwitch || tir.PrimaryIntent == IntentType.OffensiveSwitch )
        //             return SimulateSwitchRound( battleSimContext, true, true, $"Intent TOP for our Offensive Switch vs their {tir.PrimaryIntent}" );
        //         else
        //             return SimulateSwitchRound( battleSimContext, true, false, $"Intent TOP for our Offensive Switch vs their {tir.PrimaryIntent}" );

        //     case ActionType.Setup:
        //         if( tir.PrimaryIntent == IntentType.DefensiveSwitch || tir.PrimaryIntent == IntentType.OffensiveSwitch )
        //             return SimulatedSetupRound( battleSimContext, false, true, true, false );
        //         else
        //             return SimulatedSetupRound( battleSimContext, true, true, true, false );

        //     case ActionType.OffensiveStatus:
        //         if( tir.PrimaryIntent == IntentType.DefensiveSwitch || tir.PrimaryIntent == IntentType.OffensiveSwitch )
        //             return SimulateOffensiveStatusRound( battleSimContext, true, false, false, true );
        //         else
        //             return SimulateOffensiveStatusRound( battleSimContext, true, false, false, false );
        // }

        // return ourTOP;
    }

    // public TurnOutcomeProjection SimulateAttackRound( BattleSimEvent ctx, string reason = "Attack Simulation Reasons" )
    // {
    //     _rounds++;
    //     _unitSim.TurnSimLog.Add( $"===[Beginning Round Simulation for ROUND: {_rounds}. (Reason: [{reason}])]===" );
    //     _unitSim.LogSimUnit( ctx.Attacker );
    //     _unitSim.LogSimUnit( ctx.Opponent );

    //     ResolveMovePhase( ctx );
    //     ResolveRoundEndPhases( ctx );

    //     return BuildTOP( ctx );
    // }

    // public TurnOutcomeProjection SimulateSwitchRound( BattleSimEvent ctx, bool attackerIsSwitch, bool opponentIsSwitch, string reason = "Switch Simulation Reasons" )
    // {
    //     _rounds++;
    //     _unitSim.TurnSimLog.Add( $"===[Beginning Round Simulation for ROUND: {_rounds}. (Reason: [{reason}])]===" );
    //     _unitSim.LogSimUnit( ctx.Attacker );
    //     _unitSim.LogSimUnit( ctx.Opponent );

    //     ctx.AttackerIsSwitch = attackerIsSwitch;
    //     ctx.OpponentIsSwitch = opponentIsSwitch;

    //     ResolveSwitchPhase( ctx );
    //     ResolveRoundEndPhases( ctx );

    //     return BuildTOP( ctx );
    // }

    // public TurnOutcomeProjection SimulatedSetupRound( BattleSimEvent ctx, bool attackerIsSwitch, bool opponentIsSwitch, bool attackerSetup, bool opponentSetup )
    // {
    //     _rounds++;
    //     _unitSim.TurnSimLog.Add( $"===[Beginning Round Simulation for ROUND: {_rounds}. (Reason: [Setup Round Simulation])]===" );
    //     _unitSim.LogSimUnit( ctx.Attacker );
    //     _unitSim.LogSimUnit( ctx.Opponent );

    //     ctx.AttackerIsSwitch = attackerIsSwitch;
    //     ctx.OpponentIsSwitch = opponentIsSwitch;

    //     ctx.AttackerSetup = attackerSetup;
    //     ctx.OpponentSetup = opponentSetup;

    //     // foreach( var kvp in ctx.Attacker.StatStages )
    //         // Debug.Log( $"[Stat Stage Check] Attacker: {ctx.Attacker.Name}, Stat: {kvp.Key}, Change: {kvp.Value} (Attacker inside SimulatedSetupRound, before Resolving Setup Phase)" );

    //     ResolveSetupPhase( ctx );
    //     ResolveRoundEndPhases( ctx );

    //     return BuildTOP( ctx );
    // }

    // public TurnOutcomeProjection SimulateOffensiveStatusRound( BattleSimEvent ctx, bool attackerStatus, bool opponentStatus, bool attackerSwitch, bool opponentSwitch )
    // {
    //     _rounds++;
    //     _unitSim.TurnSimLog.Add( $"===[Beginning Round Simulation for ROUND: {_rounds}. (Reason: [Offensive Status Round Simulation])]===" );

    //     ctx.AttackerStatus = attackerStatus;
    //     ctx.OpponentStatus = opponentStatus;
        
    //     ctx.AttackerIsSwitch = attackerSwitch;
    //     ctx.OpponentIsSwitch = opponentSwitch;

    //     ResolveStatusPhase( ctx );
    //     ResolveRoundEndPhases( ctx );

    //     return BuildTOP( ctx );
    // }

    // private void ResolveMovePhase( BattleSimEvent ctx )
    // {
    //     _unitSim.TurnSimLog.Add( $"===[(Round: {_rounds}) Resolving Move Phase]===" );
    //     _unitSim.TurnSimLog.Add( $"===[(Round: {_rounds}) Attacker {ctx.Attacker.Name} HPR: {ctx.Attacker.CurrentHPR}. Opponent {ctx.Opponent.Name} HPR: {ctx.Opponent.CurrentHPR}]===" );

    //     Move attMove = ctx.Attacker.MTR?.Move ?? null;
    //     Move oppMove = ctx.Opponent.MTR?.Move ?? null;

    //     int attackerHitCount = attMove == null ? 0 : _unitSim.Get_ExpectedMoveHits( ctx.Attacker.MTR.Move );
    //     int opponentHitCount = oppMove == null ? 0 : _unitSim.Get_ExpectedMoveHits( ctx.Opponent.MTR.Move );

    //     float damageDone = 0f;
    //     if( ctx.AttackerMovesFirst )
    //     {
    //         _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} moves first!" );
    //         //--Attacker does damage to opponent
    //         for( int i = 0; i < attackerHitCount; i++ )
    //         {
    //             if( !_unitSim.CanActOnTurn( ctx.Attacker ) )
    //                 continue;

    //             damageDone = ApplyAttack( ctx.Opponent, ctx.Attacker.MTR.EstimatedDamage, attackerHitCount );
    //             // damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attackerHitCount );
    //             _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} Attacks! Move used: {attMove.MoveSO.Name}, Expected Hits: {attackerHitCount}, Hit: {i+1}. Damage Done: {damageDone}" );
    //             ResolvePostMoveEffects( ctx.Attacker, ctx.Opponent, damageDone );
    //             if( ctx.Opponent.CurrentHPR <= 0f )
    //                 break;
    //         }

    //         if( ctx.Opponent.CurrentHPR <= 0f )
    //         {
    //             ctx.Opponent_DiesBeforeActing = true;
    //             _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} KO'd its opponent before it could act! {ctx.Opponent_DiesBeforeActing}. Damage Done: {damageDone}" );
    //         }
    //         else if( _unitSim.CanActOnTurn( ctx.Opponent ) )
    //         {
    //             //--Opponent does damage to Attacker
    //             for( int i = 0; i < opponentHitCount; i++ )
    //             {
    //                 damageDone = ApplyAttack( ctx.Attacker, ctx.Opponent.MTR.EstimatedDamage, opponentHitCount );
    //                 // damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, opponentHitCount );
    //                 _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} Attacks! Move used: {oppMove.MoveSO.Name}, Expected Hits: {opponentHitCount}, Hit: {i+1}. Damage Done: {damageDone}" );
    //                 ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
    //                 if( ctx.Attacker.CurrentHPR <= 0f )
    //                     break;
    //             }
    //         }
    //     }
    //     else
    //     {
    //         _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} moves first!" );
    //         //--Opponent does damage to Attacker
    //         for( int i = 0; i < opponentHitCount; i++ )
    //         {
    //             if( !_unitSim.CanActOnTurn( ctx.Opponent ) )
    //                 continue;

    //             damageDone = ApplyAttack( ctx.Attacker, ctx.Opponent.MTR.EstimatedDamage, opponentHitCount );
    //             // damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, opponentHitCount );
    //             _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} Attacks! Move used: {oppMove.MoveSO.Name}, Expected Hits: {opponentHitCount}, Hit: {i+1}. Damage Done: {damageDone}" );
    //             ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
    //             if( ctx.Attacker.CurrentHPR <= 0f )
    //                 break;
    //         }

    //         if( ctx.Attacker.CurrentHPR <= 0f )
    //         {
    //             ctx.Attacker_DiesBeforeActing = true;
    //             _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} KO'd its opponent before it could act! {ctx.Attacker_DiesBeforeActing}. Damage Done: {damageDone}" );
    //         }
    //         else if( _unitSim.CanActOnTurn( ctx.Attacker ) )
    //         {
    //             //--Attacker does damage to opponent
    //             for( int i = 0; i < attackerHitCount; i++ )
    //             {
    //                 damageDone = ApplyAttack( ctx.Opponent, ctx.Attacker.MTR.EstimatedDamage, attackerHitCount );
    //                 // damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attackerHitCount );
    //                 _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} Attacks! Move used: {attMove.MoveSO.Name}, Expected Hits: {attackerHitCount}, Hit: {i+1}. Damage Done: {damageDone}" );
    //                 ResolvePostMoveEffects( ctx.Attacker, ctx.Opponent, damageDone );
    //                 if( ctx.Opponent.CurrentHPR <= 0f )
    //                     break;
    //             }
    //         }

    //     }

    //     _unitSim.TurnSimLog.Add( $"" );
    // }

    // private void ResolveSwitchPhase( BattleSimEvent ctx )
    // {
    //     _unitSim.TurnSimLog.Add( $"===[(Round: {_rounds}) Resolving Switch Phase]===" );
    //     _unitSim.TurnSimLog.Add( $"===[(Round: {_rounds}) Attacker {ctx.Attacker.Name} HPR: {ctx.Attacker.CurrentHPR}. Opponent {ctx.Opponent.Name} HPR: {ctx.Opponent.CurrentHPR}]===" );

    //     Move attMove = ctx.Attacker.MTR?.Move;
    //     Move oppMove = ctx.Opponent.MTR?.Move;

    //     int attackerHitCount = attMove == null ? 0 : _unitSim.Get_ExpectedMoveHits( attMove );
    //     int opponentHitCount = oppMove == null ? 0 : _unitSim.Get_ExpectedMoveHits( oppMove );

    //     float damageDone = 0f;
    //     if( ctx.OpponentIsSwitch && !ctx.AttackerIsSwitch && _unitSim.CanActOnTurn( ctx.Attacker ) )
    //     {
    //         _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} moves first!" );
    //         //--Attacker does damage to opponent
    //         for( int i = 0; i < attackerHitCount; i++ )
    //         {
    //             damageDone = ApplyAttack( ctx.Opponent, ctx.Attacker.MTR.EstimatedDamage, attackerHitCount );
    //             // damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attackerHitCount );
    //             _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} Attacks! Move used: {attMove.MoveSO.Name}, Expected Hits: {attackerHitCount}, Hit: {i+1}. Damage Done: {damageDone}" );
    //             ResolvePostMoveEffects( ctx.Attacker, ctx.Opponent, damageDone );
    //         }

    //         if( ctx.Opponent.CurrentHPR <= 0f )
    //         {
    //             ctx.Opponent_DiesBeforeActing = true;
    //             _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} KO'd its opponent on entry! {ctx.Opponent_DiesBeforeActing}. Damage Done: {damageDone}" );
    //         }
    //     }
    //     else if( !ctx.OpponentIsSwitch && ctx.AttackerIsSwitch && _unitSim.CanActOnTurn( ctx.Opponent ) )
    //     {
    //         _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} moves first!" );
    //         //--Opponent does damage to Attacker
    //         for( int i = 0; i < opponentHitCount; i++ )
    //         {
    //             damageDone = ApplyAttack( ctx.Attacker, ctx.Opponent.MTR.EstimatedDamage, opponentHitCount );
    //             // damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, opponentHitCount );
    //             _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} Attacks! Move used: {oppMove.MoveSO.Name}, Expected Hits: {opponentHitCount}, Hit: {i+1}. Damage Done: {damageDone}" );
    //             ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
    //         }

    //         if( ctx.Attacker.CurrentHPR <= 0f )
    //         {
    //             ctx.Attacker_DiesBeforeActing = true;
    //             _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} KO'd its opponent on entry! {ctx.Attacker_DiesBeforeActing}. Damage Done: {damageDone}" );
    //         }
    //     }

    //     ctx.OpponentIsSwitch = false;
    //     ctx.AttackerIsSwitch = false;

    //     _unitSim.TurnSimLog.Add( $"" );
    // }

    // private void ResolveSetupPhase( BattleSimEvent ctx )
    // {
    //     Move attMove = ctx.Attacker.MTR?.Move ?? null;
    //     Move oppMove = ctx.Opponent.MTR?.Move ?? null;

    //     int attackerHitCount = attMove == null ? 0 : _unitSim.Get_ExpectedMoveHits( ctx.Attacker.MTR.Move );
    //     int opponentHitCount = oppMove == null ? 0 : _unitSim.Get_ExpectedMoveHits( ctx.Opponent.MTR.Move );

    //     float damageDone = 0f;

    //     if( ctx.AttackerSetup )
    //     {
    //         if( ctx.AttackerMovesFirst && _unitSim.CanActOnTurn( ctx.Attacker ) )
    //         {
    //             //--Attacker Sets up
    //             ApplySetupMove( ctx.Attacker, attMove );

    //             if( !ctx.OpponentIsSwitch && ctx.OpponentCanAct )
    //             {
    //                 //--Attacker gets hit by opponent attack
    //                 damageDone = ApplyAttack( ctx.Attacker, ctx.Opponent.MTR.EstimatedDamage, opponentHitCount );
    //                 // damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, opponentHitCount );
    //                 ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
    //             }
    //         }
    //         else
    //         {
    //             if( !ctx.OpponentIsSwitch && _unitSim.CanActOnTurn( ctx.Opponent ) )
    //             {
    //                 //--Attacker gets hit by opponent attack
    //                 damageDone = ApplyAttack( ctx.Attacker, ctx.Opponent.MTR.EstimatedDamage, opponentHitCount );
    //                 // damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, opponentHitCount );
    //                 ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
    //             }

    //             //--Attacker Sets up
    //             if( ctx.AttackerCanAct )
    //                 ApplySetupMove( ctx.Attacker, attMove );
    //         }
    //     }
    //     else if( ctx.OpponentSetup )
    //     {
    //         if( ctx.AttackerMovesFirst )
    //         {
    //             if( !ctx.OpponentIsSwitch && _unitSim.CanActOnTurn( ctx.Attacker ) )
    //             {
    //                 //--Opponent gets hit by Attacker attack
    //                 damageDone = ApplyAttack( ctx.Opponent, ctx.Attacker.MTR.EstimatedDamage, attackerHitCount ); //--Target, attack, attack hit count
    //                 // damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attackerHitCount ); //--Target, attack, attack hit count
    //                 ResolvePostMoveEffects( ctx.Attacker, ctx.Opponent, damageDone );
    //             }

    //             //--Opponent Sets up
    //             if( _unitSim.CanActOnTurn( ctx.Opponent ) )
    //                 ApplySetupMove( ctx.Opponent, oppMove );
    //         }
    //         else
    //         {
    //             //--Opponent Sets up
    //             if( _unitSim.CanActOnTurn( ctx.Opponent ) )
    //                 ApplySetupMove( ctx.Opponent, oppMove );

    //             if( !ctx.OpponentIsSwitch && _unitSim.CanActOnTurn( ctx.Attacker ) )
    //             {
    //                 //--Opponent gets hit by Attacker attack
    //                 damageDone = ApplyAttack( ctx.Opponent, ctx.Attacker.MTR.EstimatedDamage, attackerHitCount );
    //                 // damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attackerHitCount );
    //                 ResolvePostMoveEffects( ctx.Attacker, ctx.Opponent, damageDone );
    //             }
    //         }
    //     }

    //     _unitSim.TurnSimLog.Add( $"" );
    // }

    // private void ResolveStatusPhase( BattleSimEvent ctx )
    // {
    //     Move attMove = ctx.Attacker.MTR?.Move ?? null;
    //     Move oppMove = ctx.Opponent.MTR?.Move ?? null;

    //     int attackerHitCount = attMove == null ? 0 : _unitSim.Get_ExpectedMoveHits( ctx.Attacker.MTR.Move );
    //     int opponentHitCount = oppMove == null ? 0 : _unitSim.Get_ExpectedMoveHits( ctx.Opponent.MTR.Move );

    //     _unitSim.TurnSimLog.Add( $"===[(Round: {_rounds}) Resolving Offensive Status Phase]===" );
    //     _unitSim.TurnSimLog.Add( $"===[(Round: {_rounds}) Attacker {ctx.Attacker.Name} HPR: {ctx.Attacker.CurrentHPR}. Opponent {ctx.Opponent.Name} HPR: {ctx.Opponent.CurrentHPR}]===" );

    //     float damageDone = 0f;

    //     if( ctx.AttackerStatus )
    //     {
    //         if( ctx.AttackerMovesFirst )
    //         {
    //             //--Attacker Uses Offensive Status
    //             if( _unitSim.CanActOnTurn( ctx.Attacker ) )
    //                 ApplyOffensiveStatusMove( ctx.Opponent, attMove, ctx.Field ); //--Target, move used by attacking pokemon, field

    //             if( !ctx.OpponentIsSwitch && _unitSim.CanActOnTurn( ctx.Opponent ) )
    //             {
    //                 //--Attacker gets hit by opponent attack
    //                 damageDone = ApplyAttack( ctx.Attacker, ctx.Opponent.MTR.EstimatedDamage, opponentHitCount ); //--Target, attacking pokemon PTKO, attacking move hit count
    //                 // damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, opponentHitCount ); //--Target, attacking pokemon PTKO, attacking move hit count
    //                 ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
    //             }
    //         }
    //         else
    //         {
    //             if( !ctx.OpponentIsSwitch && _unitSim.CanActOnTurn( ctx.Opponent ) )
    //             {
    //                 //--Attacker gets hit by opponent attack
    //                 damageDone = ApplyAttack( ctx.Attacker, ctx.Opponent.MTR.EstimatedDamage, opponentHitCount ); //--Target, attacking pokemon PTKO, attacking move hit count
    //                 // damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, opponentHitCount ); //--Target, attacking pokemon PTKO, attacking move hit count
    //                 ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
    //             }

    //             //--Attacker Uses Offensive Status
    //             if( _unitSim.CanActOnTurn( ctx.Attacker ) )
    //                 ApplyOffensiveStatusMove( ctx.Opponent, attMove, ctx.Field ); //--Target, move used by attacking pokemon, field
    //         }
    //     }
    //     else if( ctx.OpponentStatus )
    //     {
    //         if( ctx.AttackerMovesFirst )
    //         {
    //             if( !ctx.AttackerIsSwitch && _unitSim.CanActOnTurn( ctx.Attacker ) )
    //             {
    //                 //--Opponent gets hit by Attacker attack
    //                 damageDone = ApplyAttack( ctx.Opponent, ctx.Attacker.MTR.EstimatedDamage, attackerHitCount ); //--Target, attacking pokemon PTKO, attacking move hit count
    //                 // damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attackerHitCount ); //--Target, attacking pokemon PTKO, attacking move hit count
    //                 ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
    //             }

    //             //--Opponent Uses Offensive Status
    //             if( _unitSim.CanActOnTurn( ctx.Opponent ) )
    //                 ApplyOffensiveStatusMove( ctx.Attacker, oppMove, ctx.Field ); //--Target, move used by attacking pokemon, field
    //         }
    //         else
    //         {
    //             //--Opponent Uses Offensive Status
    //             if( _unitSim.CanActOnTurn( ctx.Opponent ) )
    //                 ApplyOffensiveStatusMove( ctx.Attacker, oppMove, ctx.Field ); //--Target, move used by attacking pokemon, field

    //             if( !ctx.AttackerIsSwitch && _unitSim.CanActOnTurn( ctx.Attacker ) )
    //             {
    //                 //--Opponent gets hit by Attacker attack
    //                 damageDone = ApplyAttack( ctx.Opponent, ctx.Attacker.MTR.EstimatedDamage, attackerHitCount ); //--Target, attacking pokemon PTKO, attacking move hit count
    //                 // damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attackerHitCount ); //--Target, attacking pokemon PTKO, attacking move hit count
    //                 ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
    //             }
    //         }
    //     }
    // }

    public TurnOutcomeProjection RunSimulation( BattleSimEvent bsc, bool log = false )
    {
        //--Order modules by priority. maybe we do this in bsc.
        //--run each module's stored action in action -> priority -> speed order as expected
        //--duing each module, appropriately resolve post action effects
        //--after all modules run, run post round effects, make sure ALL effects and their durations tick, appropriately updating each unit and the field.

        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Running a Round Simulation for {bsc.Attacker.Name} vs {bsc.Opponent.Name}!" );
        _unitSim.TurnSimLog.Add( $"" );

        foreach( var module in bsc.SimModules )
        {
            if( module.Type != SimModuleType.SupportiveStatus )
            {
                if( module.Attacker.CurrentHPR <= 0f || module.Opponent.CurrentHPR <= 0f )
                    continue;
            }

            module.Module?.Invoke( module.Attacker, module.Opponent, bsc.Field );
            _unitSim.TurnSimLog.Add( $"" );
        }

        ResolveRoundEndPhases( bsc );

        return BuildTOP( bsc, log );
    }

    private void ResolvePostMoveEffects( SimulatedUnit attacker, SimulatedUnit target, float damageDone )
    {
        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Resolving Post Move Effects for {attacker.Name} (HP {attacker.CurrentHPR}) attacking {target.Name} (HP {target.CurrentHPR})!" );

        bool attackerMakesContact = attacker.MTR.Move.MoveSO.Flags.Contains( MoveFlags.Contact );
        float attackDrainPercent = attacker.MTR.Move.MoveSO.DrainPercentage;
        HealType healType = attacker.MTR.Move.MoveSO.HealType;
        RecoilType recoilType = attacker.MTR.Move.MoveSO.Recoil.RecoilType;
        bool moveChangesStats = attacker.MTR.Move.MoveSO.MoveEffects.StatChangeList != null && attacker.MTR.Move.MoveSO.MoveEffects.StatChangeList.Count > 0;

        //--Contact
        if( attackerMakesContact )
        {
            if( target.Ability == AbilityID.RoughSkin )
                DecreaseHP( attacker, ( 1f/8f ) );

            attacker.CurrentHPR = Mathf.Clamp01( attacker.CurrentHPR );

            if( _unitSim.IsFainted( attacker ) )
                return;

            if( target.Item == BattleItemEffectID.RockyHelmet )
                DecreaseHP( attacker, ( 1f/6f ) );

            if( _unitSim.IsFainted( attacker ) )
                return;

            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} Made contact. HP: {attacker.CurrentHPR}" );
        }

        //--Sitrus Berry
        if( target.Item == BattleItemEffectID.SitrusBerry && target.CurrentHPR <= 0.5f && target.CurrentHPR > HP_EPSILON )
        {
            IncreaseHP( target, 0.25f );
            target.Item = BattleItemEffectID.None; //--eat da berry
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {target.Name} Had a sitrus berry! HP: {target.CurrentHPR}" );
        }

        //--Move Effects such as drain healing and recoil happen after contact/hp change effects.
        if( attackDrainPercent > 0 )
        {
            float drain = attackDrainPercent / 100f;
            IncreaseHP( attacker, drain * damageDone );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} Used a draining move! HP: {attacker.CurrentHPR}" );
        }

        if( healType != HealType.None )
        {
            if( healType == HealType.PercentOfMaxHP )
            {
                float healAmount = attacker.MTR.Move.MoveSO.HealAmount; //--Just in case to avoid integer division resulting in 0 or 100
                float heal = healAmount / 100f;
                IncreaseHP( attacker, heal );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} Used a self-healing move! HP: {attacker.CurrentHPR}" );
            }
        }

        if( recoilType != RecoilType.None )
        {
            float recoilDamage = attacker.MTR.Move.MoveSO.Recoil.RecoilDamage;
            float recoil = recoilDamage / 100f;

            switch( recoilType )
            {
                case RecoilType.RecoilByMaxHP:
                    float maxHP = 1f;
                    DecreaseHP( attacker, maxHP * recoil );
                    break;

                case RecoilType.RecoilByDamage:
                    DecreaseHP( attacker, damageDone * recoil );
                    break;

                case RecoilType.RecoilByCurrentHP:
                    float currentHP = attacker.CurrentHPR;
                    DecreaseHP( attacker, currentHP * recoil );
                    break;

                default:
                    Debug.LogError( "AI Turn Projection: Unknown Recoil Effect!!" );
                    break;
            }

            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} took move recoil! HP: {attacker.CurrentHPR}" );

            if( _unitSim.IsFainted( attacker ) )
                return;
        }

        //--Life Orb
        if( attacker.Item == BattleItemEffectID.LifeOrb && damageDone > 0f )
        {
            DecreaseHP( attacker, ( 1f/10f ) );

            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} took Life Orb recoil! HP: {attacker.CurrentHPR}" );

            if( _unitSim.IsFainted( attacker ) )
                return;
        }

        //--Knock Off
        if( attacker.MTR.Move.MoveSO.Name == "Knock Off" )
        {
            target.Item = BattleItemEffectID.None;
        }

        //--Guaranteed Stat Changes (close combat, trailblaze, scale shot, etc.)
        if( moveChangesStats && attacker.MTR.Move.MoveSO.MoveCategory != MoveCategory.Status )
        {
            ApplySetupMove( attacker, attacker.MTR.Move );
        }

        _unitSim.TurnSimLog.Add( $"" );
    }

    private void ResolveRoundEndPhases( BattleSimEvent ctx )
    {
        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Resolving Round End Phases!" );
        ctx.ActiveUnits.Sort( ( a, b ) => b.Speed.CompareTo( a.Speed ) );

        foreach( var phase in _roundEndPhases )
        {
            foreach( var unit in ctx.ActiveUnits )
            {
                if( _unitSim.IsFainted( unit ) )
                    continue;

                phase( unit, ctx.ActiveUnits, ctx.Field );
            }
        }

        _unitSim.TurnSimLog.Add( $"" );
    }

    private float ApplyAttack( SimulatedUnit target, /*PotentialToKO attackingPTKO*/float baseDamage, int hitCount )
    {
        float previousHPR = target.CurrentHPR;
        float damage = hitCount > 0 ? baseDamage / hitCount : 0f;

        target.CurrentHPR -= damage;
        target.CurrentHPR = Mathf.Clamp01( target.CurrentHPR );
        target.CurrentHPR = Mathf.Floor( target.CurrentHPR * 1000f ) / 1000f;

        if( target.CurrentHPR <= HP_EPSILON )
            target.CurrentHPR = 0f;

        return previousHPR - target.CurrentHPR;
    }

    private void DecreaseHP( SimulatedUnit unit, float delta )
    {
        unit.CurrentHPR -= delta;
        unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
        unit.CurrentHPR = Mathf.Floor( unit.CurrentHPR * 1000f ) / 1000f;

        if( unit.CurrentHPR <= HP_EPSILON )
            unit.CurrentHPR = 0f;
    }

    private void IncreaseHP( SimulatedUnit unit, float delta )
    {
        unit.CurrentHPR += delta;
        unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
        unit.CurrentHPR = Mathf.Floor( unit.CurrentHPR * 1000f ) / 1000f;
    }

    private void ApplySetupMove( SimulatedUnit unit, Move move )
    {
        var delta = _unitSim.BuildStatStageDelta( move );

        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Applying Stat Changes for Unit: {unit.Name}, Move: {move?.MoveSO.Name}." );

        _unitSim.TurnSimLog.Add( $"" );
        _unitSim.TurnSimLog.Add( $"Stat Stages Before:" );
        _unitSim.TurnSimLog.Add( $"Attack: {unit.StatStages[Stat.Attack]}" );
        _unitSim.TurnSimLog.Add( $"Defense: {unit.StatStages[Stat.Defense]}" );
        _unitSim.TurnSimLog.Add( $"SpAttack: {unit.StatStages[Stat.SpAttack]}" );
        _unitSim.TurnSimLog.Add( $"SpDefense: {unit.StatStages[Stat.SpDefense]}" );
        _unitSim.TurnSimLog.Add( $"Speed: {unit.StatStages[Stat.Speed]}" );

        unit.StatStages[Stat.Attack]        = unit.StatStages[Stat.Attack]      + delta.Attack;
        unit.StatStages[Stat.Defense]       = unit.StatStages[Stat.Defense]     + delta.Defense;
        unit.StatStages[Stat.SpAttack]      = unit.StatStages[Stat.SpAttack]    + delta.SpAttack;
        unit.StatStages[Stat.SpDefense]     = unit.StatStages[Stat.SpDefense]   + delta.SpDefense;
        unit.StatStages[Stat.Speed]         = unit.StatStages[Stat.Speed]       + delta.Speed;

        _unitSim.TurnSimLog.Add( $"" );
        _unitSim.TurnSimLog.Add( $"Stat Stages After:" );
        _unitSim.TurnSimLog.Add( $"Attack: {unit.StatStages[Stat.Attack]}" );
        _unitSim.TurnSimLog.Add( $"Defense: {unit.StatStages[Stat.Defense]}" );
        _unitSim.TurnSimLog.Add( $"SpAttack: {unit.StatStages[Stat.SpAttack]}" );
        _unitSim.TurnSimLog.Add( $"SpDefense: {unit.StatStages[Stat.SpDefense]}" );
        _unitSim.TurnSimLog.Add( $"Speed: {unit.StatStages[Stat.Speed]}" );
        _unitSim.TurnSimLog.Add( $"" );
    }

    private void ApplyOffensiveStatusMove( SimulatedUnit target, Move move, SimulatedField field )
    {
        bool severe     = move.MoveEffects.SevereStatus     != SevereConditionID.None ;
        bool vol        = move.MoveEffects.VolatileStatus   != VolatileConditionID.None;
        bool trans      = move.MoveEffects.TransientStatus  != TransientConditionID.None;
        // bool bind       = move.MoveEffects.BindingStatus    != BindingConditionID.None; //--Consider having binding moves be part of this decision line later

        bool statusEffect   =  severe || vol || trans;
        bool hazard         = move.MoveEffects.CourtCondition   != CourtConditionID.None;
        bool debuff         = move.MoveEffects.StatChangeList?.Count > 0 && ( move.MoveSO.MoveEffects.Target == EffectTarget.Enemy || move.MoveSO.MoveEffects.Target == EffectTarget.OpposingSide );

        _unitSim.TurnSimLog.Add( $"Trying to apply an offensive status via {move.MoveSO.Name}!" );
        _unitSim.TurnSimLog.Add( $"" );

        if( statusEffect )
        {
            if( severe )
            {
                if( target.SevereStatus == SevereConditionID.None )
                {
                    _unitSim.SevereConditions[move.MoveEffects.SevereStatus]?.Invoke( target );
                    _unitSim.TurnSimLog.Add( $"Applying {move.MoveEffects.SevereStatus} to {target.Name}!" );
                }
                else
                {
                    _unitSim.TurnSimLog.Add( $"{target.Name} already has the {target.SevereStatus} severe status!" );
                }
            }
        }
        else if( hazard )
        {
            if( target.CourtLocation == CourtLocation.TopCourt )
            {
                field.TopCourtConditions.Add( move.MoveEffects.CourtCondition, -1 );
                _unitSim.TurnSimLog.Add( $"Applying {move.MoveEffects.CourtCondition} to the Top Court!" );
            }
            else if( target.CourtLocation == CourtLocation.BottomCourt )
            {
                field.BottomCourtConditions.Add( move.MoveEffects.CourtCondition, -1 );
                _unitSim.TurnSimLog.Add( $"Applying {move.MoveEffects.CourtCondition} to the Bottom Court!" );
            }
        }
        else if( debuff )
        {
            ApplySetupMove( target, move );
        }
    }

    private void Apply_WeatherDamage( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( field.Weather == WeatherConditionID.None )
            return;

        if( field.Weather == WeatherConditionID.SANDSTORM )
        {
            if( _unitSim.CheckTypes( PokemonType.Rock, unit ) || _unitSim.CheckTypes( PokemonType.Ground, unit ) || _unitSim.CheckTypes( PokemonType.Steel, unit ) )
                return;
            else
                DecreaseHP( unit, ( 1f/16f ) );

            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );

            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} took Sandstorm Damage! HP: {unit.CurrentHPR}" );
        }

        //--Other weathers may heal pokemon with certain abilities
        //--these need to go here
    }

    private void Apply_TerrainChanges( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( field.Terrain == TerrainID.None )
            return;

        if( field.Terrain == TerrainID.Blighted )
        {
            if( !unit.IsUngrounded )
            {
                if( !_unitSim.CheckTypes( PokemonType.Ghost, unit ) && !_unitSim.CheckTypes( PokemonType.Dark, unit ) )
                {
                    DecreaseHP( unit, ( 1f/16f ) );
                    unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
                    _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} took Blighted Terrain Damage! HP: {unit.CurrentHPR}" );
                }
            }
        }

        if( field.Terrain == TerrainID.Grassy )
        {
            if( !unit.IsUngrounded )
            {
                IncreaseHP( unit, ( 1f/16f ) );
                unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was healed by Grassy Terrain! HP: {unit.CurrentHPR}" );
            }
        }
    }
    
    private void Apply_LeftoversBlackSludge( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( unit.Item == BattleItemEffectID.Leftovers && unit.CurrentHPR > HP_EPSILON )
        {
            IncreaseHP( unit, ( 1f/16f ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was healed by Leftovers! HP: {unit.CurrentHPR}" );
        }

        if( unit.Item == BattleItemEffectID.BlackSludge )
        {
            if( _unitSim.CheckTypes( PokemonType.Poison, unit ) && unit.CurrentHPR > HP_EPSILON )
            {
                IncreaseHP( unit, ( 1f/16f ) );
                unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was healed by Black Sludge! HP: {unit.CurrentHPR}" );
            }
            else
            {
                DecreaseHP( unit, ( 1f/16f ) );
                unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Black Sludge! HP: {unit.CurrentHPR}" );
            }
        }
    }

    private void Apply_AquaRing( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( unit.VolatileStatuses.Contains( VolatileConditionID.AquaRing ) )
        {
            IncreaseHP( unit, ( 1f/16f ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was healed by Aqua Ring! HP: {unit.CurrentHPR}" );
        }
    }

    // private void Apply_LeechSeed( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    // {
        
    // }

    private void Apply_SevereStatus( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( unit.SevereStatus == SevereConditionID.PSN )
        {
            DecreaseHP( unit, ( 1f/8f ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Poison! HP: {unit.CurrentHPR}" );
        }

        if( unit.SevereStatus == SevereConditionID.TOX )
        {
            DecreaseHP( unit, ( unit.SevereStatusTime * ( 1f/16f ) ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            unit.SevereStatusTime++;
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Toxic! HP: {unit.CurrentHPR}, Toxic Counter: {unit.SevereStatusTime}" );
        }

        if( unit.SevereStatus == SevereConditionID.BRN || unit.SevereStatus == SevereConditionID.FBT )
        {
            DecreaseHP( unit, ( 1f/16f ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Burn or Frostbite! HP: {unit.CurrentHPR}" );
        }

        if( unit.SevereStatus == SevereConditionID.PAR )
        {
            if( unit.SevereStatusTime > 0 )
                unit.SevereStatusTime--;

            if( unit.SevereStatusTime <= 0 )
                unit.SevereStatus = SevereConditionID.None;
        }

        if( unit.SevereStatus == SevereConditionID.SLP )
        {
            if( unit.SevereStatusTime > 0 )
                unit.SevereStatusTime--;

            if( unit.SevereStatusTime <= 0 )
                unit.SevereStatus = SevereConditionID.None;
        }
    }

    private void Apply_Curse( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( unit.VolatileStatuses.Contains( VolatileConditionID.Cursed ) )
        {
            DecreaseHP( unit, ( 1f/4f ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Curse! HP: {unit.CurrentHPR}" );
        }
    }

    private void Apply_BindingDamage( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( unit.Bindings.Count > 0 )
        {
            foreach( var bind in unit.Bindings )
            {
                float damage = 1f/8f;

                if( bind == BindingConditionID.AcidTrap )
                {
                    float effectiveness = TypeChart.GetEffectiveness( PokemonType.Poison, unit.Type.One ) * TypeChart.GetEffectiveness( PokemonType.Poison, unit.Type.Two );
                    damage *= effectiveness;
                }

                DecreaseHP( unit, damage );
                unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by a Binding Condition! HP: {unit.CurrentHPR}" );
            }
        }
    }

    private void BuildRoundEndPhaseList()
    {
        _roundEndPhases = new()
        {
            { Apply_WeatherDamage },
            { Apply_TerrainChanges },
            { Apply_LeftoversBlackSludge },
            { Apply_AquaRing },
            // { Apply_LeechSeed },
            { Apply_SevereStatus },
            { Apply_Curse },
            { Apply_BindingDamage },
        };
    }

    private void MoveSuccessDicInit()
    {
        MoveSuccess = new()
        {
            {
                "Fake Out", ( attacker, target, move ) =>
                {
                    var attackerUnit = _ai.GetBattleUnit( attacker.Pokemon );

                    if( attackerUnit.Flags[UnitFlags.TurnsTaken].Count > 0 )
                        return false;
                    else
                        return true;
                }
            }
        };
    }

    public void RunAttackModule( SimulatedUnit attacker, SimulatedUnit target, SimulatedField field )
    {
        Move attMove = attacker.MTR?.Move ?? null;
        int attackerHitCount = attMove == null ? 0 : _unitSim.Get_ExpectedMoveHits( attMove );

        float damageDone = 0f;

        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Running an Attack Module! Attacker {attacker?.Name} (HPR: {attacker.BeginningHPR}), Move: {attMove?.MoveSO.Name} (Hits: {attackerHitCount}), Target: {target?.Name} (HPR: {target.BeginningHPR}" );

        for( int i = 0; i < attackerHitCount; i++ )
        {
            if( !_unitSim.CanActOnTurn( attacker ) )
            {
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {attacker.Name} cannot act!" );
                break;
            }

            damageDone = ApplyAttack( target, attacker.MTR.EstimatedDamage, attackerHitCount );
            
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {attacker.Name} Attacks! Move used: {attMove.MoveSO.Name}, Expected Hits: {attackerHitCount}, Hit: {i+1}. Damage Done: {damageDone}" );
            
            ResolvePostMoveEffects( attacker, target, damageDone );

            if( target.CurrentHPR <= 0f )
                break;
        }
    }

    public void RunSwitchModule( SimulatedUnit attacker, SimulatedUnit target, SimulatedField field )
    {
        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Running a Switch Module! Unit Switching In: {attacker.Name}, Opponent: {target.Name}!" );
        //--nothing happens when you switch lol. maybe i can move hazard interactions here
        //--and pull them out of TOP building and stuff.
    }

    public void RunSetupModule( SimulatedUnit attacker, SimulatedUnit target, SimulatedField field )
    {
        Move attMove = attacker.MTR?.Move ?? null;

        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Running a Setup Module! Attacker {attacker.Name}, Move: {attMove.MoveSO.Name}, Opponent: {target.Name}!" );

        if( _unitSim.CanActOnTurn( attacker ) )
        {
            //--Attacker sets up
            ApplySetupMove( attacker, attMove );
        }
    }

    public void RunOffensiveStatusModule( SimulatedUnit attacker, SimulatedUnit target, SimulatedField field )
    {
        Move attMove = attacker.MTR?.Move ?? null;

        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Running an Offensive Status Module! Attacker {attacker.Name}, Move: {attMove.MoveSO.Name}, Opponent: {target.Name}!" );

        if( _unitSim.CanActOnTurn( attacker ) )
        {
            //--Attacker uses offensive status move
            ApplyOffensiveStatusMove( target, attMove, field ); //--Target, move used by attacking pokemon, field
        }
    }

}

public class BattleSimEvent
{
    public SimulatedUnit Attacker;
    public SimulatedUnit Opponent;
    public List<SimulatedUnit> ActiveUnits;
    public List<SimulationModule> SimModules;

    public SimulatedField Field;

    public PotentialToKO AttackerPTKO;
    public PotentialToKO OpponentPTKO;

    public bool AttackerMovesFirst;
    public bool AttackerCanAct;
    public bool OpponentCanAct;

    public bool AttackerIsSwitch;
    public bool OpponentIsSwitch;

    public bool AttackerSetup;
    public bool OpponentSetup;

    public bool AttackerStatus;
    public bool OpponentStatus;

    public bool Attacker_DiesBeforeActing;
    public bool Opponent_DiesBeforeActing;
}

public class SimulationModule
{
    public SimModuleType Type { get; private set; }
    public int Priority { get; private set; }
    public SimulatedUnit Attacker { get; private set; }
    public SimulatedUnit Opponent { get; private set; }
    public Action<SimulatedUnit /*attacker*/, SimulatedUnit /*target*/, SimulatedField /*field*/> Module { get; private set; }

    public SimulationModule( SimModuleType type, int priority, SimulatedUnit attacker, SimulatedUnit opponent, Action< SimulatedUnit, SimulatedUnit, SimulatedField> module )
    {
        Type = type;
        Priority = priority;
        Attacker = attacker;
        Opponent = opponent;
        Module = module;
    }
}

public struct SimulationPackage
{
    public SimulatedUnit SimUnit;
    public SimModuleType ModuleType;
}
