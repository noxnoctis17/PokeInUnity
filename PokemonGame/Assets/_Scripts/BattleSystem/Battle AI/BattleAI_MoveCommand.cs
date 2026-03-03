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

    public void SubmitMoveCommand( BattleUnit target, BoardContext context )
    {
        Debug.Log( "[AI Scoring] SubmitMoveCommand()" );

        var move = ChooseAMove( target, context );
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
        var targetName = eval.OpponentName;

        var myPTKO_onTarget = eval.AttackerPTKOR;
        var theirPTKO_onMe = eval.OpponentPTKOR;
        _ai.CurrentLog.Add( $"===[Beginning Attack Scoring for {attackerName} vs {targetName}. Tempo: {tempo.TempoState}, My PTKO Them: {myPTKO_onTarget.PTKO}, their PTKO on me: {theirPTKO_onMe.PTKO}]===" );

        //--KO Class Advantage
        score += _ai.Get_OffensivePTKOScore( myPTKO_onTarget.Score );
        _ai.CurrentLog.Add( $"My ({attackerName}) PTKO Score {myPTKO_onTarget.Score}. Score: {score}" );

        score += theirPTKO_onMe.Score;
        _ai.CurrentLog.Add( $"Their ({targetName}) PTKO Score {theirPTKO_onMe.Score}. Score: {score}" );

        bool iAmFaster = eval.AttackerMovesFirst;
        bool iThreatenKO = eval.AttackerThreatensKO;
        bool theyThreatenKO = eval.OpponentThreatensKO;

        if( iThreatenKO )
        {
            if( iAmFaster )
                score += 135; //--Commit hard
            else
                score += 75; //--Probably commit
        }

        _ai.CurrentLog.Add( $"I Threaten a KO: {iThreatenKO}, I am faster: {iAmFaster}. Score: {score}" );

        float hp = eval.AttackerHPR;
        if( theyThreatenKO )
        {
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
        else
        {
            if( hp > 0.8f && iAmFaster )
                score += 20;
        }

        _ai.CurrentLog.Add( $"They Threaten a KO: {theyThreatenKO}, I am faster: {iAmFaster}. Score: {score}" );

        float myHPRatio = eval.AttackerHPR;

        if( myHPRatio < 0.2f )
            score -= 20;
        else if( myHPRatio < 0.4f )
            score -= 10;
        else if( myHPRatio >= 0.8f && !theyThreatenKO )
            score += 15;

        _ai.CurrentLog.Add( $"HP Ratio Check {myHPRatio}. Score: {score}" );

        score += _ai.TempoAttackModifier( tempo );

        _ai.CurrentLog.Add( $"Tempo check. Score: {score}" );

        if( context.IsForcedTrade )
            score += 45;

        _ai.CurrentLog.Add( $"Forced Trade: {context.IsForcedTrade}. Score: {score}" );

        if( context.IsBehind )
            score += 20;

        _ai.CurrentLog.Add( $"===[Is Behind: {context.IsBehind}. Final Attack Score: {score}]===" );

        return score;
    }

    private AIDecisionType ChooseAttackStyle()
    {
        return Random.value < _ai.TrainerSkillModifier ? AIDecisionType.StrongestMove : AIDecisionType.RandomMove;
    }

    private Move ChooseAMove( BattleUnit target, BoardContext context )
    {
        Debug.Log( $"[AI Scoring] ChooseAMove()" );
        var decision = ChooseAttackStyle();
        switch( decision )
        {
            case AIDecisionType.StrongestMove:
                var bestMove = Get_BestSimulatedMove( _ai.Unit.Pokemon, target.Pokemon, context ).Move;
                // var bestMove = FindStrongestAttack( target ).Move;
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

        foreach( var move in _ai.Unit.Pokemon.ActiveMoves )
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
        var attacker = _ai.Unit.Pokemon;
        Move bestMove = null;
        Move fakeout = null;
        float bestScore = float.MinValue;

        foreach( var move in _ai.Unit.Pokemon.ActiveMoves )
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
            float effectiveness = TypeChart.GetEffectiveness( move.MoveType, target.Pokemon.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, target.Pokemon.PokeSO.Type2 );

            Debug.Log( $"[AI Scoring][Strongest Attack] Finding {_ai.Unit.Pokemon.NickName}'s Strongest Attacking Move. Effectiveness: {effectiveness}" );

            //--If there a type immunity, skip this move
            if( effectiveness == 0f )
                continue;

            //--Check if move uses preferred attacking stat. In some cases things like move power, stab, effectiveness, or weather damage boost
            //--may make a move more preferable despite not using the higher attacking stat
            float stat = 1f;

            if( move.MoveSO.MoveCategory == MoveCategory.Physical )
                stat = _ai.GetUnitInferredStat( _ai.Unit.Pokemon, Stat.Attack );

            if( move.MoveSO.MoveCategory == MoveCategory.Special )
                stat = _ai.GetUnitInferredStat( _ai.Unit.Pokemon, Stat.SpAttack );

            Debug.Log( $"[AI Scoring][Strongest Attack] Finding {_ai.Unit.Pokemon.NickName}'s Strongest Attacking Move. Inferred Stat: {stat}" );

            int power = move.MovePower;

            //--Multi hit move power expectation
            if( move.MoveSO.HitRange.x >= 2 && move.MoveSO.HitRange.y != 0 )
            {
                int minHits = move.MoveSO.HitRange.x;
                int maxHits = move.MoveSO.HitRange.y;

                int expectedHits = Mathf.FloorToInt( ( minHits + maxHits ) * 0.5f );

                power *= expectedHits;
            }
            
            //--Set-multi hit true power
            if( move.MoveSO.HitRange.x >= 2 && move.MoveSO.HitRange.y == 0 )
            {
                for( int i = 0; i < move.MoveSO.HitRange.x; i++ )
                {
                    power += power;
                }
            }

            float stab              = attacker.CheckTypes( move.MoveType ) ? 1.5f : 1f;
            float weather           = 1f;
            float terrain           = 1f;
            float item              = 1f;

            var field = _ai.BattleSystem.Field;

            if( field.Weather != null )
            {
                if( _ai.UnitSim.WeatherDMGModifiers.TryGetValue( field.Weather.ID, out var mod ) )
                    weather = mod( move );
            }

            if( field.Terrain != null )
            {
                if( _ai.UnitSim.TerrainDMGModifiers.TryGetValue( field.Terrain.ID, out var mod ) )
                    terrain = mod( move );
            }

            if( attacker.BattleItemEffect != null )
            {
                if( _ai.UnitSim.ItemDMGModifiers.TryGetValue( attacker.BattleItemEffect.ID, out var mod ) )
                    item = mod( attacker, target.Pokemon, move );
            }

            Debug.Log( $"[AI Scoring][Strongest Attack][{attacker.NickName}][{move.MoveSO.Name}] Effectiveness: {effectiveness}, STAB: {stab}, Weather: {weather}, Terrain: {terrain}, Item: {item}" );

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

    public MoveThreatResult Get_BestSimulatedMove( Pokemon attacker, Pokemon target, BoardContext boardContext )
    {
        CustomLogSession moveLog = new();
        int bestScore = int.MinValue;
        float bestModifier = 1f;
        Move bestMove = null;

        //--Create Target's PTKO on attacker & target's sim unit once for use in each attacker's move's simulation
        float attHPR                    = _ai.Get_HPRatio( attacker );
        float tarHPR                    = _ai.Get_HPRatio( target );
        var tarMTR                      = _ai.Get_MostThreateningMove( target, attacker ); //--Remember, the order here is attacking unit vs target unit. this is the target's attack on the attacker here.
        var tarMoveMod                  = tarMTR.Modifier;
        var tarWSR                      = _ai.Get_WallingScoreResult( target, attacker, tarMTR );
        PotentialToKOResult tarPTKOR    = _ai.Get_PotentialToKOResult( tarWSR, tarMoveMod, attHPR );
        
        var fieldSim                    = _ai.UnitSim.BuildSimField();
        var targetSimUnit               = _ai.UnitSim.BuildSimUnit( target, tarHPR, tarMTR, fieldSim );

        bool isFaster = _ai.GetUnitInferredStat( attacker, Stat.Speed ) > _ai.GetUnitInferredStat( target, Stat.Speed );

        foreach( var move in attacker.ActiveMoves )
        {
            //--If the move has 0 pp, we can't use it
            if( move.PP == 0 )
                continue;

            //--If the move has 0 power, or is a status move, we skip it. We're looking for damaging moves only!
            if( move.MovePower <= 0 || move.MoveSO.MoveCategory == MoveCategory.Status )
                continue;

            //--Move type effectiveness
            float effectiveness = TypeChart.GetEffectiveness( move.MoveType, target.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, target.PokeSO.Type2 );

            //--If there a type immunity, skip this move
            if( effectiveness == 0f )
                continue;

            float modifier                  = effectiveness * _ai.UnitSim.Get_MoveModifier( attacker, target, move );
            MoveThreatResult mtr            = new(){ Score = 0, Modifier = modifier, Move = move };
            var attWSR                      = _ai.Get_WallingScoreResult( attacker, target, mtr );
            PotentialToKOResult attPTKOR    = _ai.Get_PotentialToKOResult( attWSR, modifier, tarHPR );

            bool movesFirst = isFaster || move.MoveSO.MovePriority > MovePriority.Zero;

            targetSimUnit.CurrentHPR = tarHPR; //--Because we create this sim unit only once, we need to make sure we heal its hp to where it currently is before we run the attack sim!
            var attackerSimUnit     = _ai.UnitSim.BuildSimUnit( attacker, attHPR, mtr, fieldSim );
            var battleSimContext    = _ai.Projection.Get_BattleSimContext( attPTKOR.PTKO, tarPTKOR.PTKO, attackerSimUnit, targetSimUnit, fieldSim, movesFirst );

            moveLog.Clear();
            
            var top                 = _ai.Projection.SimulateRound( battleSimContext, $"Get Best Simulated Move ({attacker.NickName}, {move.MoveSO.Name})" );

            moveLog.Add( $"[AI Scoring][Best Simulated Move][{attacker.NickName}][{move.MoveSO.Name}] Modifier: {modifier}" );
            moveLog.Add( $"[AI Scoring][Best Simulated Move][{attacker.NickName}][{move.MoveSO.Name}] PTKO: {attPTKOR.PTKO}. Walling Score: {attWSR.Score}. Moves first: {movesFirst}." );
            moveLog.Add( $"[AI Scoring][Best Simulated Move][{attacker.NickName}][{move.MoveSO.Name}] Attacker - End of turn HP: {top.Attacker_EndOfTurnHP}." );
            moveLog.Add( $"[AI Scoring][Best Simulated Move][{attacker.NickName}][{move.MoveSO.Name}] Attacker - Dies Before Acting: {top.Attacker_DiesBeforeActing}." );
            moveLog.Add( $"[AI Scoring][Best Simulated Move][{attacker.NickName}][{move.MoveSO.Name}] Opponent - End of turn HP: {top.Opponent_EndOfTurnHP}." );
            moveLog.Add( $"[AI Scoring][Best Simulated Move][{attacker.NickName}][{move.MoveSO.Name}] Opponent - Dies Before Acting: {top.Opponent_DiesBeforeActing}." );
            moveLog.Add( $"[AI Scoring][Best Simulated Move][{attacker.NickName}][{move.MoveSO.Name}] Mutual KO: {top.MutualKO}." );

            moveLog.Add( $"[AI Scoring][Best Simulated Move][{attacker.NickName}][{move.MoveSO.Name}] Beginning Scoring." );

            //--Begin Scoring
            int score = 0;
            if( top.Attacker_DiesBeforeActing )
                score -= 150;

            if( top.Opponent_DiesBeforeActing )
                score += 150;

            if( top.MutualKO )
                score += boardContext.IsBehind ? 40 : -40;

            moveLog.Add( $"[AI Scoring][Best Simulated Move][{attacker.NickName}][{move.MoveSO.Name}] Checked Instant KO Flags. Score: {score}" );

            score += Mathf.FloorToInt( ( 1f - top.Opponent_EndOfTurnHP ) * 90f );
            moveLog.Add( $"[AI Scoring][Best Simulated Move][{attacker.NickName}][{move.MoveSO.Name}] Opponent HP Checked. Score: {score}" );

            score -= Mathf.FloorToInt( ( 1f - top.Attacker_EndOfTurnHP ) * 80f );
            moveLog.Add( $"[AI Scoring][Best Simulated Move][{attacker.NickName}][{move.MoveSO.Name}] Attacker HP Checked. Score: {score}" );

            moveLog.Add( $"[AI Scoring][Best Simulated Move][{attacker.NickName}][{move.MoveSO.Name}] Final Score: {score}" );

            if( score > bestScore )
            {
                bestScore = score;
                bestModifier = modifier;
                bestMove = move;
            }

            moveLog.Add( $"[AI Scoring][Best Simulated Move][{attacker.NickName}][{move.MoveSO.Name}] Best Move: {bestMove.MoveSO.Name}. Best Modifier: {bestModifier}. Best Score: {bestScore}." );
        }

        moveLog.Add( $"[AI Scoring][Best Simulated Move][{attacker.NickName}] Final move chosen. Best Move: {bestMove.MoveSO.Name}. Best Modifier: {bestModifier}. Best Score: {bestScore}." );

        Debug.Log( moveLog.ToString() );
        moveLog.Clear();

        return new()
        {
            Score = bestScore,
            Modifier = bestModifier,
            Move = bestMove
        };
    }
}
