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

    public void SubmitMoveCommand( BattleUnit target )
    {
        Debug.Log( "[AI Scoring] SubmitMoveCommand()" );

        var move = ChooseAMove( target );
        List<BattleUnit> targets = new();
        
        if( move.MoveSO.MoveTarget == MoveTarget.Self )
            targets.Add( _ai.Unit );
        else
            targets.Add( target );

        if( move != null )
        {
            _ai.BattleSystem.SetMoveCommand( _ai.Unit, targets, move, true );
        }
        else
            Debug.LogError( $"{_ai.Unit.Pokemon.NickName} has not chosen a move even though it was supposed to! Battle will now hang!" );
    }

    public int AttackScore( TempoStateResult tempo, ExchangeEvaluation eval, BoardContext context )
    {
        int score = 0;

        var attackerName = eval.AttackerName;
        var targetName = eval.TargetName;

        var myPTKO_onTarget = eval.AttackerPTKO_onTarget;
        var theirPTKO_onMe = eval.TargetPTKO_onAttacker;
        Debug.Log( $"[AI Scoring][Choose Command][Attack Score] Beginning Attack Scoring for {attackerName} vs {targetName}. Tempo: {tempo.TempoState}, My PTKO Them: {myPTKO_onTarget.PotentialKO}, their PTKO on me: {theirPTKO_onMe.PotentialKO}" );

        //--KO Class Advantage
        score += _ai.Get_OffensivePTKOScore( myPTKO_onTarget.Score );
        Debug.Log( $"[AI Scoring][Choose Command][Attack Score] My ({attackerName}) PTKO Score {myPTKO_onTarget.Score}. Score: {score}" );

        score += theirPTKO_onMe.Score;
        Debug.Log( $"[AI Scoring][Choose Command][Attack Score] Their ({targetName}) PTKO Score {theirPTKO_onMe.Score}. Score: {score}" );

        bool iAmFaster = eval.AttackerMovesFirst;
        bool iThreatenKO = eval.AttackerThreatensKO;
        bool theyThreatenKO = eval.TargetThreatensKO;

        if( iThreatenKO )
        {
            if( iAmFaster )
                score += 135; //--Commit hard
            else
                score += 75; //--Probably commit
        }

        Debug.Log( $"[AI Scoring][Choose Command][Attack Score] I Threaten a KO: {iThreatenKO} & I am faster: {iAmFaster}. Score: {score}" );

        if( theyThreatenKO )
        {
            float hp = eval.AttackerHPRatio;
            int deathPenalty;

            if( hp > 0.6f )
                deathPenalty = 120;
            else if( hp > 0.3f )
                deathPenalty = 80;
            else
                deathPenalty = 40;

            if( !iAmFaster )
                score -= deathPenalty;
            else
                score -= deathPenalty / 2;
        }

        Debug.Log( $"[AI Scoring][Choose Command][Attack Score] They Threaten a KO: {theyThreatenKO} & I am faster: {iAmFaster}. Score: {score}" );

        float myHPRatio = eval.AttackerHPRatio;

        if( myHPRatio < 0.2f )
            score -= 20;
        else if( myHPRatio < 0.4f )
            score -= 10;

        Debug.Log( $"[AI Scoring][Choose Command][Attack Score] HP Ratio Check. Score: {score}" );

        score += _ai.TempoAttackModifier( tempo );

        Debug.Log( $"[AI Scoring][Choose Command][Attack Score] Tempo check. Score: {score}" );

        if( context.IsForcedTrade )
            score += 40;

        Debug.Log( $"[AI Scoring][Choose Command][Attack Score] Forced Trade: {context.IsForcedTrade}. Score: {score}" );

        if( context.IsBehind )
            score += 20;

        Debug.Log( $"[AI Scoring][Choose Command][Attack Score] Is Behind: {context.IsBehind}. Score: {score}" );

        return score;
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

    private Move GetRandomMove( BattleUnit target )
    {
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

        Move randMove = null;
        if( usableMoves != null && usableMoves.Count > 0 )
        {
            int r = Random.Range( 0, usableMoves.Count );
            randMove = usableMoves[r];
            usableMoves.Clear();
        }

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

            //--Assign a bonus for a terrain damage boost
            float terrain = _ai.BattleSystem.Field.Terrain?.OnDamageModify?.Invoke( _ai.Unit, target.Pokemon, move ) ?? 1f;
            Debug.Log( $"[AI Scoring][Strongest Attack] Finding {_ai.Unit.Pokemon.NickName}'s Strongest Attacking Move. Terrain Modifier: {terrain}" );

            //--Assign a bonus for held item boost
            float item = _ai.Unit.Pokemon.BattleItemEffect?.OnDamageModify?.Invoke( _ai.Unit, target.Pokemon, move ) ?? 1f;
            Debug.Log( $"[AI Scoring][Strongest Attack] Finding {_ai.Unit.Pokemon.NickName}'s Strongest Attacking Move. Item Modifier: {item}" );

            //--Calculate final score
            float score = power * effectiveness * stat * stab * weather * terrain * item;
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
        
        if( fakeout != null && _ai.CanUseFakeOut( _ai.Unit, target ) )
        {
            if( Random.value < _ai.TrainerSkillModifier )
            {
                return new(){ Score = bestScore * 2f, Move = fakeout };
            }
        }

        return new(){ Score = bestScore, Move = bestMove };
    }
}
