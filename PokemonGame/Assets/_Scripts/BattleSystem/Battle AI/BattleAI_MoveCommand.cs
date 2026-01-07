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
        Debug.Log( "[AI Scoring] SubmitMoveCommand()" );

        var move = ChooseAMove( target );
        
        if( move.MoveSO.MoveTarget == MoveTarget.Self )
            target = _ai.Unit;

        _ai.BattleSystem.SetMoveCommand( _ai.Unit, target, move, true );
    }

    public bool ShouldAttackInstead( ThreatResult damageThreat )
    {
        var target = damageThreat.Unit.Pokemon;
        var attacker = _ai.Unit.Pokemon;
        
        float targetHPRatio = _ai.Get_HPRatio( target );
        int threatWallingScore = _ai.Get_WallingScore( attacker, target );
        var mostThreateningMoveModifier = _ai.Get_MostThreateningMove( attacker, target ).Modifier;

        PotentialToKOResult potentialToKOTarget  = _ai.Get_PotentialToKOResult( threatWallingScore, mostThreateningMoveModifier, targetHPRatio );

        bool isFaster = _ai.Unit.Pokemon.PokeSO.Speed > damageThreat.Unit.Pokemon.PokeSO.Speed || _ai.Check_UnitHasPriority( attacker ); //--this function needs to consider fake out and whether it can be used! it should also ignore status moves so it doesn't count protect, follow me, etc.
        bool targetIsKillable  = potentialToKOTarget.PotentialKO >= PotentialToKO.Risky;

        Debug.Log( $"[AI Scoring][Should Attack] {attacker.NickName} has chosen whether it will attack {target.NickName}! Target's HP Ratio: {targetHPRatio}, Potential to KO: {potentialToKOTarget.PotentialKO}, Faster: {isFaster}, Target is Kill: {targetIsKillable}" );

        return isFaster && targetIsKillable || _ai.Check_IsLastPokemon();
    }

    private AIDecisionType ChooseAttackStyle()
    {
        return Random.value < _ai.TrainerSkillModifier ? AIDecisionType.StrongestMove : AIDecisionType.RandomMove;
    }

    private Move ChooseAMove( BattleUnit target )
    {
        Debug.Log( $"[AI Scoring] ChooseAMove()" );
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
        Debug.Log( $"[AI Scoring] Getting Random Move vs {target.Pokemon.NickName}" );
        List<Move> usableMoves = new();

        if( _ai.Unit.Flags[UnitFlags.ChoiceItem].IsActive && _ai.Unit.LastUsedMove != null )
            return _ai.Unit.LastUsedMove;

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
        Debug.Log( $"[AI Scoring] Getting Strongest Attack vs {target.Pokemon.NickName}" );

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
            if( move.MovePower <= 0 || move.MoveSO.MoveCategory == MoveCategory.Status )
                continue;

            //--Move type effectiveness
            float effectiveness = TypeChart.GetEffectiveness( move.MoveSO.Type, target.Pokemon.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveSO.Type, target.Pokemon.PokeSO.Type2 );

            Debug.Log( $"[AI Scoring][Strongest Attack] Finding {_ai.Unit.Pokemon.NickName}'s Strongest Attacking Move. Effectiveness: {effectiveness}" );

            //--If there a type immunity, skip this move
            if( effectiveness == 0f )
                continue;

            //--Check if move uses preferred attacking stat. In some cases things like move power, stab, effectiveness, or weather damage boost
            //--may make a move more preferable despite not using the higher attacking stat
            float stat = 1f;

            if( move.MoveSO.MoveCategory == MoveCategory.Physical )
                stat = _ai.Pokemon.PokeSO.Attack;

            if( move.MoveSO.MoveCategory == MoveCategory.Special )
                stat = _ai.Pokemon.PokeSO.SpAttack;

            Debug.Log( $"[AI Scoring][Strongest Attack] Finding {_ai.Unit.Pokemon.NickName}'s Strongest Attacking Move. Base Stat: {stat}" );

            //--Factor in effective move power
            float power = move.MovePower;
            if( move.MoveSO.HitRange.x >= 2 && move.MoveSO.HitRange.y != 0 )
            {
                power *= move.MoveSO.HitRange.y;
            }
            else if( move.MoveSO.HitRange.x >= 2 && move.MoveSO.HitRange.y == 0 )
            {
                power *= move.MoveSO.HitRange.x;
            }

            //--Assign a bonus for stab
            float stab = move.MoveType == _ai.Pokemon.PokeSO.Type1 || move.MoveType == _ai.Pokemon.PokeSO.Type2 ? 1.5f : 1f;
            Debug.Log( $"[AI Scoring][Strongest Attack] Finding {_ai.Unit.Pokemon.NickName}'s Strongest Attacking Move. STAB Modifier: {stab}" );

            //--Assign a bonus for a weather damage boost
            float weather = _ai.BattleSystem.Field.Weather?.OnDamageModify?.Invoke( _ai.Unit.Pokemon, target.Pokemon, move ) ?? 1f;
            Debug.Log( $"[AI Scoring][Strongest Attack] Finding {_ai.Unit.Pokemon.NickName}'s Strongest Attacking Move. Weather Modifier: {weather}" );

            //--Calculate final score
            float score = power * effectiveness * stat * stab * weather;
            Debug.Log( $"[AI Scoring][Strongest Attack] Finding {_ai.Unit.Pokemon.NickName}'s Strongest Attacking Move. Final Score: {score}" );

            //--If this move's score is the highest, set it as the current best score and best move
            if( score > bestScore )
            {
                bestScore = score;
                bestMove = move;
            }

            if( move.MoveSO.Name == "Fake Out" )
                fakeout = move;
        }

        if( _ai.Unit.Flags[UnitFlags.ChoiceItem].IsActive && _ai.Unit.LastUsedMove != null )
        {
            return new(){ Score = bestScore, Move = _ai.Unit.LastUsedMove };
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
        {
            Debug.Log( $"[AI Scoring] Fake Out user {_ai.Unit.Pokemon.NickName}'s Turn Count: {_ai.Unit.Flags[UnitFlags.TurnsTaken].Count}" );
            return false;
        }

        if( target.Pokemon.CheckTypes( PokemonType.Ghost ) )
            return false;

        return true;
    }

}
