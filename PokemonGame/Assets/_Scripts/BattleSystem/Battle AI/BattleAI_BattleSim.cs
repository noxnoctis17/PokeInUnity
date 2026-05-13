using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public enum BattleSimRoundType { AttackRound, SwitchRound, SetupRound, }
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

    public BattleSimContext Get_BattleSimContext( PotentialToKO attPTKO, PotentialToKO oppPTKO, SimulatedUnit attacker, SimulatedUnit opponent, SimulatedField field )
    {
        _unitSim.TurnSimLog.Add( $"===[Building Battle Simulation Context ({attacker.Name} vs {opponent.Name})]===" );
        var units = new List<SimulatedUnit> { attacker, opponent };
        units.Sort( ( a, b ) => b.Speed.CompareTo( a.Speed ) );

        bool attMovesFirst = false;
        var attMovePrio = attacker.MTR.Move.Priority;
        var oppMovePrio = opponent.MTR.Move.Priority;

        if( attMovePrio != oppMovePrio )
            attMovesFirst = attMovePrio > oppMovePrio;
        else
            attMovesFirst = attacker.Speed > opponent.Speed;

        _unitSim.TurnSimLog.Add( $"[Turn Simulation] Attacker ({attacker.Name}) Speed: {attacker.Speed}. Opponent ({opponent.Name}) Speed: {opponent.Speed}." );
        _unitSim.TurnSimLog.Add( $"[Turn Simulation] Attacker ({attacker.Name}) Move Priority: {attMovePrio}. Opponent ({opponent.Name}) Move Priority {oppMovePrio}." );
        _unitSim.TurnSimLog.Add( $"[Turn Simulation] Attacker ({attacker.Name}) Moves First: {attMovesFirst}." );

        BattleSimContext ctx = new()
        {
            AttackerPTKO = attPTKO,
            OpponentPTKO = oppPTKO,

            Attacker = attacker,
            Opponent = opponent,
            ActiveUnits = units,

            Field = field,

            AttackerMovesFirst = attMovesFirst,
            AttackerCanAct = _unitSim.CanActOnTurn( attacker ),
            OpponentCanAct = _unitSim.CanActOnTurn( opponent ),
        };

        _unitSim.TurnSimLog.Add( $"Attacker {ctx.Attacker.Name} PTKO: {ctx.AttackerPTKO}" );
        _unitSim.TurnSimLog.Add( $"Opponent {ctx.Opponent.Name} PTKO: {ctx.OpponentPTKO}" );

        return ctx;
    }

    private TurnOutcomeProjection BuildTOP( BattleSimContext ctx )
    {
        TurnOutcomeProjection top = new()
        {
            Attacker = ctx.Attacker,
            Opponent = ctx.Opponent,

            Field = ctx.Field, //--We currently do not make any increments to field. this feature should be expanded on to account for duration tics and such.

            AttackerPTKO = ctx.AttackerPTKO,
            OpponentPTKO = ctx.OpponentPTKO,

            Attacker_EndOfTurnHP = ctx.Attacker.CurrentHPR,
            Opponent_EndOfTurnHP = ctx.Opponent.CurrentHPR,

            Attacker_DiesBeforeActing = ctx.Attacker_DiesBeforeActing,
            Opponent_DiesBeforeActing = ctx.Opponent_DiesBeforeActing,

            AttackerCanAct = ctx.AttackerCanAct,
            OpponentCanAct = ctx.OpponentCanAct,

            MutualKO = ctx.Attacker.CurrentHPR <= 0f && ctx.Opponent.CurrentHPR <= 0f,
            AttackerMovedFirst = ctx.AttackerMovesFirst,
        };

        _unitSim.LogTop( top );
        top.SimulationLog = _unitSim.TurnSimLog.ToString();
        _unitSim.TurnSimLog.Clear();
        _rounds = 0;

        return top;
    }

    public TurnOutcomeProjection SimulateAttackRound( BattleSimContext ctx, string reason = "Attack Simulation Reasons" )
    {
        _rounds++;
        _unitSim.TurnSimLog.Add( $"===[Beginning Round Simulation for ROUND: {_rounds}. (Reason: [{reason}])]===" );
        _unitSim.LogSimUnit( ctx.Attacker );
        _unitSim.LogSimUnit( ctx.Opponent );

        ResolveMovePhase( ctx );
        ResolveRoundEndPhases( ctx );

        return BuildTOP( ctx );
    }

    public TurnOutcomeProjection SimulateSwitchRound( BattleSimContext ctx, bool attackerIsSwitch, bool opponentIsSwitch, string reason = "Switch Simulation Reasons" )
    {
        _rounds++;
        _unitSim.TurnSimLog.Add( $"===[Beginning Round Simulation for ROUND: {_rounds}. (Reason: [{reason}])]===" );
        _unitSim.LogSimUnit( ctx.Attacker );
        _unitSim.LogSimUnit( ctx.Opponent );

        ctx.AttackerIsSwitch = attackerIsSwitch;
        ctx.OpponentIsSwitch = opponentIsSwitch;

        ResolveSwitchPhase( ctx );
        ResolveRoundEndPhases( ctx );

        return BuildTOP( ctx );
    }

    public TurnOutcomeProjection SimulatedSetupRound( BattleSimContext ctx, bool attackerIsSwitch, bool opponentIsSwitch, bool attackerSetup, bool opponentSetup )
    {
        _rounds++;
        _unitSim.TurnSimLog.Add( $"===[Beginning Round Simulation for ROUND: {_rounds}. (Reason: [Setup Round Simulation])]===" );
        _unitSim.LogSimUnit( ctx.Attacker );
        _unitSim.LogSimUnit( ctx.Opponent );

        ctx.AttackerIsSwitch = attackerIsSwitch;
        ctx.OpponentIsSwitch = opponentIsSwitch;

        ctx.AttackerSetup = attackerSetup;
        ctx.OpponentSetup = opponentSetup;

        foreach( var kvp in ctx.Attacker.StatStages )
            Debug.Log( $"[Stat Stage Check] Attacker: {ctx.Attacker.Name}, Stat: {kvp.Key}, Change: {kvp.Value} (Attacker inside SimulatedSetupRound, before Resolving Setup Phase)" );

        ResolveSetupPhase( ctx );
        ResolveRoundEndPhases( ctx );

        return BuildTOP( ctx );
    }

    public TurnOutcomeProjection SimulateOffensiveStatusRound( BattleSimContext ctx, bool attackerStatus, bool opponentStatus, bool attackerSwitch, bool opponentSwitch )
    {
        _rounds++;
        _unitSim.TurnSimLog.Add( $"===[Beginning Round Simulation for ROUND: {_rounds}. (Reason: [Offensive Status Round Simulation])]===" );

        ctx.AttackerStatus = attackerStatus;
        ctx.OpponentStatus = opponentStatus;
        
        ctx.AttackerIsSwitch = attackerSwitch;
        ctx.OpponentIsSwitch = opponentSwitch;

        ResolveStatusPhase( ctx );
        ResolveRoundEndPhases( ctx );

        return BuildTOP( ctx );
    }

    private void ResolveMovePhase( BattleSimContext ctx )
    {
        _unitSim.TurnSimLog.Add( $"===[(Round: {_rounds}) Resolving Move Phase]===" );
        _unitSim.TurnSimLog.Add( $"===[(Round: {_rounds}) Attacker {ctx.Attacker.Name} HPR: {ctx.Attacker.CurrentHPR}. Opponent {ctx.Opponent.Name} HPR: {ctx.Opponent.CurrentHPR}]===" );

        var attMove = ctx.Attacker.MTR.Move;
        var oppMove = ctx.Opponent.MTR.Move;

        int attackerHitCount = _unitSim.Get_ExpectedMoveHits( ctx.Attacker.MTR.Move );
        int opponentHitCount = _unitSim.Get_ExpectedMoveHits( ctx.Opponent.MTR.Move );

        float damageDone = 0f;
        if( ctx.AttackerMovesFirst )
        {
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} moves first!" );
            //--Attacker does damage to opponent
            for( int i = 0; i < attackerHitCount; i++ )
            {
                if( !_unitSim.CanActOnTurn( ctx.Attacker ) )
                    continue;

                damageDone = ApplyAttack( ctx.Opponent, ctx.Attacker.MTR.EstimatedDamage, attackerHitCount );
                // damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attackerHitCount );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} Attacks! Move used: {attMove.MoveSO.Name}, Expected Hits: {attackerHitCount}, Hit: {i+1}. Damage Done: {damageDone}" );
                ResolvePostMoveEffects( ctx.Attacker, ctx.Opponent, damageDone );
                if( ctx.Opponent.CurrentHPR <= 0f )
                    break;
            }

            if( ctx.Opponent.CurrentHPR <= 0f )
            {
                ctx.Opponent_DiesBeforeActing = true;
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} KO'd its opponent before it could act! {ctx.Opponent_DiesBeforeActing}. Damage Done: {damageDone}" );
            }
            else if( _unitSim.CanActOnTurn( ctx.Opponent ) )
            {
                //--Opponent does damage to Attacker
                for( int i = 0; i < opponentHitCount; i++ )
                {
                    damageDone = ApplyAttack( ctx.Attacker, ctx.Opponent.MTR.EstimatedDamage, opponentHitCount );
                    // damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, opponentHitCount );
                    _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} Attacks! Move used: {oppMove.MoveSO.Name}, Expected Hits: {opponentHitCount}, Hit: {i+1}. Damage Done: {damageDone}" );
                    ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
                    if( ctx.Attacker.CurrentHPR <= 0f )
                        break;
                }
            }

        }
        else
        {
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} moves first!" );
            //--Opponent does damage to Attacker
            for( int i = 0; i < opponentHitCount; i++ )
            {
                if( !_unitSim.CanActOnTurn( ctx.Opponent ) )
                    continue;

                damageDone = ApplyAttack( ctx.Attacker, ctx.Opponent.MTR.EstimatedDamage, opponentHitCount );
                // damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, opponentHitCount );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} Attacks! Move used: {oppMove.MoveSO.Name}, Expected Hits: {opponentHitCount}, Hit: {i+1}. Damage Done: {damageDone}" );
                ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
                if( ctx.Attacker.CurrentHPR <= 0f )
                    break;
            }

            if( ctx.Attacker.CurrentHPR <= 0f )
            {
                ctx.Attacker_DiesBeforeActing = true;
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} KO'd its opponent before it could act! {ctx.Attacker_DiesBeforeActing}. Damage Done: {damageDone}" );
            }
            else if( _unitSim.CanActOnTurn( ctx.Attacker ) )
            {
                //--Attacker does damage to opponent
                for( int i = 0; i < attackerHitCount; i++ )
                {
                    damageDone = ApplyAttack( ctx.Opponent, ctx.Attacker.MTR.EstimatedDamage, attackerHitCount );
                    // damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attackerHitCount );
                    _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} Attacks! Move used: {attMove.MoveSO.Name}, Expected Hits: {attackerHitCount}, Hit: {i+1}. Damage Done: {damageDone}" );
                    ResolvePostMoveEffects( ctx.Attacker, ctx.Opponent, damageDone );
                    if( ctx.Opponent.CurrentHPR <= 0f )
                        break;
                }
            }

        }

        _unitSim.TurnSimLog.Add( $"" );
    }

    private void ResolveSwitchPhase( BattleSimContext ctx )
    {
        _unitSim.TurnSimLog.Add( $"===[(Round: {_rounds}) Resolving Switch Phase]===" );
        _unitSim.TurnSimLog.Add( $"===[(Round: {_rounds}) Attacker {ctx.Attacker.Name} HPR: {ctx.Attacker.CurrentHPR}. Opponent {ctx.Opponent.Name} HPR: {ctx.Opponent.CurrentHPR}]===" );

        var attMove = ctx.Attacker.MTR.Move;
        var oppMove = ctx.Opponent.MTR.Move;

        int attackerHitCount = _unitSim.Get_ExpectedMoveHits( ctx.Attacker.MTR.Move );
        int opponentHitCount = _unitSim.Get_ExpectedMoveHits( ctx.Opponent.MTR.Move );

        float damageDone = 0f;
        if( ctx.OpponentIsSwitch && !ctx.AttackerIsSwitch && _unitSim.CanActOnTurn( ctx.Attacker ) )
        {
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} moves first!" );
            //--Attacker does damage to opponent
            for( int i = 0; i < attackerHitCount; i++ )
            {
                damageDone = ApplyAttack( ctx.Opponent, ctx.Attacker.MTR.EstimatedDamage, attackerHitCount );
                // damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attackerHitCount );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} Attacks! Move used: {attMove.MoveSO.Name}, Expected Hits: {attackerHitCount}, Hit: {i+1}. Damage Done: {damageDone}" );
                ResolvePostMoveEffects( ctx.Attacker, ctx.Opponent, damageDone );
            }

            if( ctx.Opponent.CurrentHPR <= 0f )
            {
                ctx.Opponent_DiesBeforeActing = true;
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} KO'd its opponent on entry! {ctx.Opponent_DiesBeforeActing}. Damage Done: {damageDone}" );
            }
        }
        else if( !ctx.OpponentIsSwitch && ctx.AttackerIsSwitch && _unitSim.CanActOnTurn( ctx.Opponent ) )
        {
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} moves first!" );
            //--Opponent does damage to Attacker
            for( int i = 0; i < opponentHitCount; i++ )
            {
                damageDone = ApplyAttack( ctx.Attacker, ctx.Opponent.MTR.EstimatedDamage, opponentHitCount );
                // damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, opponentHitCount );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} Attacks! Move used: {oppMove.MoveSO.Name}, Expected Hits: {opponentHitCount}, Hit: {i+1}. Damage Done: {damageDone}" );
                ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
            }

            if( ctx.Attacker.CurrentHPR <= 0f )
            {
                ctx.Attacker_DiesBeforeActing = true;
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} KO'd its opponent on entry! {ctx.Attacker_DiesBeforeActing}. Damage Done: {damageDone}" );
            }
        }

        ctx.OpponentIsSwitch = false;
        ctx.AttackerIsSwitch = false;

        _unitSim.TurnSimLog.Add( $"" );
    }

    private void ResolveSetupPhase( BattleSimContext ctx )
    {
        var attMove = ctx.Attacker.MTR.Move;
        var oppMove = ctx.Opponent.MTR.Move;

        int attackerHitCount = _unitSim.Get_ExpectedMoveHits( ctx.Attacker.MTR.Move );
        int opponentHitCount = _unitSim.Get_ExpectedMoveHits( ctx.Opponent.MTR.Move );

        float damageDone = 0f;

        if( ctx.AttackerSetup )
        {
            if( ctx.AttackerMovesFirst && _unitSim.CanActOnTurn( ctx.Attacker ) )
            {
                //--Attacker Sets up
                ApplySetupMove( ctx.Attacker, attMove );

                if( !ctx.OpponentIsSwitch && ctx.OpponentCanAct )
                {
                    //--Attacker gets hit by opponent attack
                    damageDone = ApplyAttack( ctx.Attacker, ctx.Opponent.MTR.EstimatedDamage, opponentHitCount );
                    // damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, opponentHitCount );
                    ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
                }
            }
            else
            {
                if( !ctx.OpponentIsSwitch && _unitSim.CanActOnTurn( ctx.Opponent ) )
                {
                    //--Attacker gets hit by opponent attack
                    damageDone = ApplyAttack( ctx.Attacker, ctx.Opponent.MTR.EstimatedDamage, opponentHitCount );
                    // damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, opponentHitCount );
                    ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
                }

                //--Attacker Sets up
                if( ctx.AttackerCanAct )
                    ApplySetupMove( ctx.Attacker, attMove );
            }
        }
        else if( ctx.OpponentSetup )
        {
            if( ctx.AttackerMovesFirst )
            {
                if( !ctx.OpponentIsSwitch && _unitSim.CanActOnTurn( ctx.Attacker ) )
                {
                    //--Opponent gets hit by Attacker attack
                    damageDone = ApplyAttack( ctx.Opponent, ctx.Attacker.MTR.EstimatedDamage, attackerHitCount ); //--Target, attack, attack hit count
                    // damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attackerHitCount ); //--Target, attack, attack hit count
                    ResolvePostMoveEffects( ctx.Attacker, ctx.Opponent, damageDone );
                }

                //--Opponent Sets up
                if( _unitSim.CanActOnTurn( ctx.Opponent ) )
                    ApplySetupMove( ctx.Opponent, oppMove );
            }
            else
            {
                //--Opponent Sets up
                if( _unitSim.CanActOnTurn( ctx.Opponent ) )
                    ApplySetupMove( ctx.Opponent, oppMove );

                if( !ctx.OpponentIsSwitch && _unitSim.CanActOnTurn( ctx.Attacker ) )
                {
                    //--Opponent gets hit by Attacker attack
                    damageDone = ApplyAttack( ctx.Opponent, ctx.Attacker.MTR.EstimatedDamage, attackerHitCount );
                    // damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attackerHitCount );
                    ResolvePostMoveEffects( ctx.Attacker, ctx.Opponent, damageDone );
                }
            }
        }

        _unitSim.TurnSimLog.Add( $"" );
    }

    private void ResolveStatusPhase( BattleSimContext ctx )
    {
        var attMove = ctx.Attacker.MTR.Move;
        var oppMove = ctx.Opponent.MTR.Move;

        int attackerHitCount = _unitSim.Get_ExpectedMoveHits( ctx.Attacker.MTR.Move );
        int opponentHitCount = _unitSim.Get_ExpectedMoveHits( ctx.Opponent.MTR.Move );

        _unitSim.TurnSimLog.Add( $"===[(Round: {_rounds}) Resolving Offensive Status Phase]===" );
        _unitSim.TurnSimLog.Add( $"===[(Round: {_rounds}) Attacker {ctx.Attacker.Name} HPR: {ctx.Attacker.CurrentHPR}. Opponent {ctx.Opponent.Name} HPR: {ctx.Opponent.CurrentHPR}]===" );

        float damageDone = 0f;

        if( ctx.AttackerStatus )
        {
            if( ctx.AttackerMovesFirst )
            {
                //--Attacker Uses Offensive Status
                if( _unitSim.CanActOnTurn( ctx.Attacker ) )
                    ApplyOffensiveStatusMove( ctx.Opponent, attMove, ctx.Field ); //--Target, move used by attacking pokemon, field

                if( !ctx.OpponentIsSwitch && _unitSim.CanActOnTurn( ctx.Opponent ) )
                {
                    //--Attacker gets hit by opponent attack
                    damageDone = ApplyAttack( ctx.Attacker, ctx.Opponent.MTR.EstimatedDamage, opponentHitCount ); //--Target, attacking pokemon PTKO, attacking move hit count
                    // damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, opponentHitCount ); //--Target, attacking pokemon PTKO, attacking move hit count
                    ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
                }
            }
            else
            {
                if( !ctx.OpponentIsSwitch && _unitSim.CanActOnTurn( ctx.Opponent ) )
                {
                    //--Attacker gets hit by opponent attack
                    damageDone = ApplyAttack( ctx.Attacker, ctx.Opponent.MTR.EstimatedDamage, opponentHitCount ); //--Target, attacking pokemon PTKO, attacking move hit count
                    // damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, opponentHitCount ); //--Target, attacking pokemon PTKO, attacking move hit count
                    ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
                }

                //--Attacker Uses Offensive Status
                if( _unitSim.CanActOnTurn( ctx.Attacker ) )
                    ApplyOffensiveStatusMove( ctx.Opponent, attMove, ctx.Field ); //--Target, move used by attacking pokemon, field
            }
        }
        else if( ctx.OpponentStatus )
        {
            if( ctx.AttackerMovesFirst )
            {
                if( !ctx.AttackerIsSwitch && _unitSim.CanActOnTurn( ctx.Attacker ) )
                {
                    //--Opponent gets hit by Attacker attack
                    damageDone = ApplyAttack( ctx.Opponent, ctx.Attacker.MTR.EstimatedDamage, attackerHitCount ); //--Target, attacking pokemon PTKO, attacking move hit count
                    // damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attackerHitCount ); //--Target, attacking pokemon PTKO, attacking move hit count
                    ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
                }

                //--Opponent Uses Offensive Status
                if( _unitSim.CanActOnTurn( ctx.Opponent ) )
                    ApplyOffensiveStatusMove( ctx.Attacker, oppMove, ctx.Field ); //--Target, move used by attacking pokemon, field
            }
            else
            {
                //--Opponent Uses Offensive Status
                if( _unitSim.CanActOnTurn( ctx.Opponent ) )
                    ApplyOffensiveStatusMove( ctx.Attacker, oppMove, ctx.Field ); //--Target, move used by attacking pokemon, field

                if( !ctx.AttackerIsSwitch && _unitSim.CanActOnTurn( ctx.Attacker ) )
                {
                    //--Opponent gets hit by Attacker attack
                    damageDone = ApplyAttack( ctx.Opponent, ctx.Attacker.MTR.EstimatedDamage, attackerHitCount ); //--Target, attacking pokemon PTKO, attacking move hit count
                    // damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attackerHitCount ); //--Target, attacking pokemon PTKO, attacking move hit count
                    ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
                }
            }
        }
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

    private void ResolveRoundEndPhases( BattleSimContext ctx )
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

        unit.StatStages[Stat.Attack]        = unit.StatStages[Stat.Attack]      + delta.Attack;
        unit.StatStages[Stat.Defense]       = unit.StatStages[Stat.Defense]     + delta.Defense;
        unit.StatStages[Stat.SpAttack]      = unit.StatStages[Stat.SpAttack]    + delta.SpAttack;
        unit.StatStages[Stat.SpDefense]     = unit.StatStages[Stat.SpDefense]   + delta.SpDefense;
        unit.StatStages[Stat.Speed]         = unit.StatStages[Stat.Speed]       + delta.Speed;
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

        if( statusEffect )
        {
            if( severe )
                _unitSim.SevereConditions[move.MoveEffects.SevereStatus]?.Invoke( target );
        }
        else if( hazard )
        {
            if( target.CourtLocation == CourtLocation.TopCourt )
                field.TopCourtConditions.Add( move.MoveEffects.CourtCondition, -1 );
            else if( target.CourtLocation == CourtLocation.BottomCourt )
                field.BottomCourtConditions.Add( move.MoveEffects.CourtCondition, -1 );
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

}

public class BattleSimContext
{
    public SimulatedUnit Attacker;
    public SimulatedUnit Opponent;
    public List<SimulatedUnit> ActiveUnits;

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
