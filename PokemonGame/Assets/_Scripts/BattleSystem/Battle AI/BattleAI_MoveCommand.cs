using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleAI_MoveCommand
{
    private BattleAI _ai;

    public BattleAI_MoveCommand( BattleAI ai )
    {
        _ai = ai;
    }

    public void SubmitMoveCommand( BattleUnit target ){
        Debug.Log( "AI ChooseMoveCommand()" );

        var move = ChooseAMove( target );
        
        if( move.MoveSO.MoveTarget == MoveTarget.Self )
            target = _ai.Unit;

        _ai.BattleSystem.SetEnemyMoveCommand( _ai.Unit, target, move );
    }

    private AIDecisionType ChooseAttackStyle()
    {
        return Random.value < _ai.TrainerSkillModifier ? AIDecisionType.StrongestMove : AIDecisionType.RandomMove;
    }

    private Move ChooseAMove( BattleUnit target )
    {
        var decision = ChooseAttackStyle();
        switch( decision )
        {
            case AIDecisionType.StrongestMove:
                var bestMove = FindStrongestAttack( target ).Move;
                if( bestMove != null )
                    return bestMove;

                return GetRandomMove( target );

            case AIDecisionType.RandomMove:
            default:
                return GetRandomMove( target );
        }
    }

    private Move GetRandomMove( BattleUnit target ){
        List<Move> usableMoves = new();

        foreach( var move in _ai.Pokemon.ActiveMoves )
        {
            if( move.PP == 0 )
                continue;

            if( !_ai.BattleSystem.MoveSuccess( _ai.Unit, target, move, true ) )
                continue;
            
            usableMoves.Add( move );
        }

        int r = Random.Range( 0, usableMoves.Count );
        var randMove = usableMoves[r];
        usableMoves.Clear();

        return randMove;
    }

    private MoveThreatResult FindStrongestAttack( BattleUnit target )
    {
        Move bestMove = null;
        Move fakeout = null;
        float bestScore = float.MinValue;

        foreach( var move in _ai.Pokemon.ActiveMoves )
        {
            //--If the move has 0 pp, we can't use it
            if( move.PP == 0 )
                continue;

            //--Check if the Move will not have an effect, such as using tailwind while it is already active, or fake out after the first turn, and if so, skip it
            if( !_ai.BattleSystem.MoveSuccess( _ai.Unit, target, move, true ) )
                continue;

            //--If the move has 0 power, or is a status move, we skip it. We're looking for damaging moves only!
            if( move.MoveSO.Power <= 0 || move.MoveSO.MoveCategory == MoveCategory.Status )
                continue;

            //--Move type effectiveness
            float effectiveness = TypeChart.GetEffectiveness( move.MoveSO.Type, target.Pokemon.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveSO.Type, target.Pokemon.PokeSO.Type2 );

            // //--If there's no type advantage, skip this move
            // if( effectiveness <= 1f )
            //     continue;
            //--I don't actually want to skip a move just because it has no type advantage. we will always return the highest damaging move here
            //--Type advantage is already factored in - a move with an advantage will typically have a higher score, assuming the power isn't significantly lower than a non effective move

            //--Check if move uses preferred attacking stat. In some cases things like move power, stab, effectiveness, or weather damage boost
            //--may make a move more preferable despite not using the higher attacking stat
            float stat = 1f;

            if( move.MoveSO.MoveCategory == MoveCategory.Physical )
                stat = _ai.Pokemon.PokeSO.Attack;

            if( move.MoveSO.MoveCategory == MoveCategory.Special )
                stat = _ai.Pokemon.PokeSO.SpAttack;

            //--Assign a bonus for stab
            float stab = move.MoveSO.Type == _ai.Pokemon.PokeSO.Type1 || move.MoveSO.Type == _ai.Pokemon.PokeSO.Type2 ? 1.5f : 1f;

            //--Assign a bonus for a weather damage boost
            float weather = _ai.BattleSystem.Field.Weather?.OnDamageModify?.Invoke( _ai.Unit.Pokemon, target.Pokemon, move ) ?? 1f;

            //--Calculate final score
            float score = move.MoveSO.Power * effectiveness * stat * stab * weather;

            //--If this move's score is the highest, set it as the current best score and best move
            if( score > bestScore )
            {
                bestScore = score;
                bestMove = move;
            }

            if( move.MoveSO.Name == "Fake Out" )
                fakeout = move;
        }

        if( fakeout != null && ShouldUseFakeOut( target ) )
        {
            if( Random.value < _ai.TrainerSkillModifier )
            {
                return new(){ Score = bestScore * 2f, Move = fakeout };
            }
        }

        return new(){ Score = bestScore, Move = bestMove };
    }

    private bool ShouldUseFakeOut( BattleUnit target )
    {
        if( !_ai.Pokemon.CheckHasMove( "Fake Out" ) )
            return false;

        if( _ai.Unit.Flags[UnitFlags.TurnsTaken].Count > 0 )
            return false;

        if( target.Pokemon.CheckTypes( PokemonType.Ghost ) )
            return false;

        return true;
    }

}
