using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum OffensiveStatusType { None, StatusEffect, Disruption, EntryHazard, StatDebuff, Binding, Phaze }
public enum SupportiveStatusType { None, Recovery, ForceMultiplier, BattlefieldControl, AllyProtection }

public class BattleAI_SubmitCommand
{
    private BattleAI _ai;
    private BattleAI_UnitSim _unitSim;
    private BattleAI_BattleSim _battleSim;
    private BattleAI_Projection _proj;

    public BattleAI_SubmitCommand( BattleAI ai )
    {
        _ai = ai;
        _unitSim = _ai.UnitSim;
        _battleSim = _ai.BattleSim;
        _proj = _ai.Projection;
    }

    public void SubmitMoveCommand( ActionEvaluation action )
    {
        _ai.ResetSwitchAmount();
        var attackStyle = ChooseAttackStyle();
        Move move = action.MovePayload;

        switch( attackStyle )
        {
            case AIDecisionType.ChosenMove:
                break;
            
            case AIDecisionType.RandomMove:
                move = GetRandomMove( action.Target );
                break;
        }

        List<BattleUnit> targets = new();
        
        if( attackStyle == AIDecisionType.RandomMove )
        {
            if( move.MoveSO.MoveTarget == MoveTarget.Self || move.MoveSO.MoveTarget == MoveTarget.AllySide )
                targets.Add( _ai.CurrentUnitDeciding );
            else if( move.MoveSO.MoveTarget == MoveTarget.OpposingSide )
            {
                for( int t = 0; t < _ai.Blackboard.TheirActiveBattleAIUnits.Count; t++ )
                {
                    var tar = _ai.GetBattleUnit( _ai.Blackboard.TheirActiveBattleAIUnits[t].Pokemon );
                    targets.Add( tar );
                }
            }
            else if( move.MoveSO.MoveTarget == MoveTarget.AllAdjacent )
            {
                for( int t = 0; t < _ai.Blackboard.TheirActiveBattleAIUnits.Count; t++ )
                {
                    var tar = _ai.GetBattleUnit( _ai.Blackboard.TheirActiveBattleAIUnits[t].Pokemon );
                    targets.Add( tar );
                }

                var allyUnits = _ai.BattleSystem.GetAllyUnits( _ai.CurrentUnitDeciding );
                for( int i = 0; i < allyUnits.Count; i++ )
                {
                    if( allyUnits[i] != _ai.CurrentUnitDeciding )
                        targets.Add( allyUnits[i] );
                    else
                        continue;
                }
            }
            else
                targets.Add( action.Target );
        }
        else
            targets.Add( action.Target );

        if( move != null )
        {
            _ai.BattleSystem.SetMoveCommand( _ai.CurrentUnitDeciding, targets, move, true );
        }
        else
        {
            Debug.LogError( $"{_ai.CurrentUnitDeciding.Pokemon.NickName} has not chosen a move even though it was supposed to! Getting random move!" );
            move = GetRandomMove( action.Target );
            _ai.BattleSystem.SetMoveCommand( _ai.CurrentUnitDeciding, targets, move, true );
        }
    }

    private AIDecisionType ChooseAttackStyle()
    {
        return UnityEngine.Random.value < _ai.TrainerSkillModifier ? AIDecisionType.ChosenMove : AIDecisionType.RandomMove;
    }

    private Move GetRandomMove( BattleUnit target )
    {
        List<Move> usableMoves = new();

        if( _ai.CurrentUnitDeciding.Pokemon.VolatileStatuses.ContainsKey( VolatileConditionID.ChoiceLocked ) && _ai.CurrentUnitDeciding.LastUsedMove != null )
            return _ai.CurrentUnitDeciding.LastUsedMove;

        foreach( var move in _ai.CurrentUnitDeciding.Pokemon.ActiveMoves )
        {
            if( move.PP == 0 )
                continue;

            if( !_ai.BattleSystem.MoveSuccess( _ai.CurrentUnitDeciding, target, move, true ) )
                continue;
            
            usableMoves.Add( move );
        }

        Move randMove = null;
        if( usableMoves != null && usableMoves.Count > 0 )
        {
            int r = UnityEngine.Random.Range( 0, usableMoves.Count );
            randMove = usableMoves[r];
            usableMoves.Clear();
        }

        return randMove;
    }

    public void SubmitSwitchCommand( Pokemon incomingPokemon )
    {
        _ai.IncreaseSwitchAmount();
        _ai.SetLastSentInPokemon( incomingPokemon );
        _ai.SetLastOpposingPokemon( _ai.Blackboard.TheirActiveBattleAIUnits.ToList() );
        _ai.BattleSystem.SetSwitchPokemonCommand( incomingPokemon, _ai.CurrentUnitDeciding, true );
    }
}
