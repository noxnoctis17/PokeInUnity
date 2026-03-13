using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ActionType { Attack, OffensiveSwitch, DefensiveSwitch, Setup, Support }
public class BattleAI_ActionEvaluation
{
    private BattleAI _ai;

    public BattleAI_ActionEvaluation( BattleAI ai )
    {
        _ai = ai;
    }

    public ActionEvaluation BuildActionEvaluation( ActionType type, int baseScore, ProjectedBoardState pbs, IBattleAIUnit target, object payload, TurnOutcomeProjection top )
    {
        int pbsScore = _ai.Projection.EvaluatePBS( pbs );
        ActionEvaluation eval = new()
        {
            Type = type,
            Score = baseScore + pbsScore,
            Top = top,
        };

        BattleUnit targetUnit = null;
        if( target != null )
            targetUnit = _ai.GetBattleUnit( target.PID );

        switch( type )
        {
            case ActionType.Attack: //--and--//
            case ActionType.Setup:
                eval.Target = targetUnit;
                eval.MovePayload = (Move)payload;
                break;

            case ActionType.DefensiveSwitch: //--and--//
            case ActionType.OffensiveSwitch:
                eval.SwitchPayload = (Pokemon)payload;
                break;
        }

        _ai.CurrentLog.Add( $"===[Built Action Evaluation for {eval.Type}. Base Score + PBS Score: {eval.Score}]===" );

        return eval;
    }

    public ActionEvaluation EvaluateAction( ActionEvaluation eval )
    {
        return eval.Type switch
        {
            ActionType.Attack           => EvaluateAttackAction(eval),
            ActionType.DefensiveSwitch  => EvaluateDefensiveSwitchAction(eval),
            ActionType.OffensiveSwitch  => EvaluateOffensiveSwitchAction(eval),
            ActionType.Setup            => EvaluateSetupAction(eval),
            _ => eval,
        };
    }

    private ActionEvaluation EvaluateAttackAction( ActionEvaluation eval )
    {
        const int DIE_BEFORE_ACTING_PENALTY = 60;
        const int CLEAN_KO_BONUS = 35;
        const int MUTUAL_KO_PENALTY = 10;

        int score = eval.Score;
        var top = eval.Top;

        _ai.CurrentLog.Add( $"===[Evaluating Attack Action (Score: {score})]===" );

        if( eval.MovePayload == null )
        {
            _ai.CurrentLog.Add( $"No Attacking move was picked! Returning hopefully tanked score! {score}" );
            return eval;
        }

        //--Tactical disaster: we die before acting
        if( top.Attacker_DiesBeforeActing )
        {
            score -= DIE_BEFORE_ACTING_PENALTY;
            _ai.CurrentLog.Add( $"Attacker dies before acting! Score: {score}" );
        }

        //--Tactical perfection: we KO before they act
        if( top.Opponent_DiesBeforeActing )
        {
            score += CLEAN_KO_BONUS;
            _ai.CurrentLog.Add( $"Opponent dies before acting! Score: {score}" );
        }

        //--Mutual KO (small penalty, PBS handles material)
        if( top.MutualKO )
        {
            score -= MUTUAL_KO_PENALTY;
            _ai.CurrentLog.Add( $"Mutual KO! Score: {score}" );
        }

        bool movesFirst = top.Attacker.Speed > top.Opponent.Speed;

        //--We potentially force a switch, punish the switch in!
        if( _ai.UnitSim.PredictForcedSwitch( top.AttackerPTKO, top.OpponentPTKO, movesFirst ) )
        {
            score += 25;
            _ai.CurrentLog.Add( $"We threaten to force a switch! Score: {score}" );
        }

        eval.Score = score;
        _ai.CurrentLog.Add( $"Final Score: {score}" );

        return eval;
    }

    private ActionEvaluation EvaluateDefensiveSwitchAction( ActionEvaluation eval )
    {
        const int SWITCH_DIES_PENALTY = 80;
        const int CRITICAL_ENTRY_PENALTY = 30;

        var top = eval.Top;
        int score = eval.Score;

        _ai.CurrentLog.Add( $"===[Evaluating Defensive Switch Action (Score: {score})]===" );
        
        if( eval.SwitchPayload == null )
        {
            _ai.CurrentLog.Add( $"No defensive switch was picked! Returning hopefully tanked score! {score}" );
            return eval;
        }

        if( score == -999 )
        {
            _ai.CurrentLog.Add( $"Score was tanked at the heuristic level! Skipping! Score: {score}" );
            return eval;
        }

        //--Switched mon dies on entry
        if( top.Attacker_EndOfTurnHP <= 0f )
        {
            score -= SWITCH_DIES_PENALTY;
            eval.Score = score;
            _ai.CurrentLog.Add( $"Switch in (attacker) faints on switch in! Score: {score}" );
            return eval;
        }

        //--Critically low after entry. Will have to be careful here, end game switching might be more heavily penalized, which is somewhat reasonable.
        if( top.Attacker_EndOfTurnHP <= 0.2f )
        {
            score -= CRITICAL_ENTRY_PENALTY;
            _ai.CurrentLog.Add( $"Switch in (attacker) takes big damage on entry, leaving it at {top.Attacker_EndOfTurnHP} HP on switch in! Score: {score}" );
        }

        eval.Score = score;
        return eval;
    }

    private ActionEvaluation EvaluateOffensiveSwitchAction( ActionEvaluation eval )
    {
        int score = eval.Score;

        _ai.CurrentLog.Add( $"===[Evaluating Offensive Switch Action (Score: {score})]===" );

        if( eval.SwitchPayload == null )
        {
            _ai.CurrentLog.Add( $"No offensive switch was picked! Returning hopefully tanked score! {score}" );
            return eval;
        }

        if( score == -999 )
        {
            _ai.CurrentLog.Add( $"Score was tanked at the heuristic level! Skipping! Score: {score}" );
            return eval;
        }

        float entryDamage = 1 - eval.Top.Attacker_EndOfTurnHP;

        if( entryDamage > 0.6f )
            score -= 35;

        _ai.CurrentLog.Add( $"Attacker's Entry Damage: {entryDamage}. Score: {score}" );

        if( eval.Top.Attacker_EndOfTurnHP <= 0f )
            score -= 100;
        else if( eval.Top.Attacker_EndOfTurnHP <= 0.2f )
            score -= 60;

        _ai.CurrentLog.Add( $"Attacker end of turn HP: {eval.Top.Attacker_EndOfTurnHP}. Score: {score}" );

        bool opponentThreatenedNextTurn = eval.Top.Opponent_EndOfTurnHP <= 0.5f && eval.Top.Attacker.Speed > eval.Top.Opponent.Speed;
        if( opponentThreatenedNextTurn )
            score += 25;

        _ai.CurrentLog.Add( $"Attacker threatens Opponent next turn: {opponentThreatenedNextTurn}. Score: {score}" );

        eval.Score = score;
        return eval;
    }

    private ActionEvaluation EvaluateSetupAction( ActionEvaluation eval )
    {
        const int DIE_BEFORE_ACTING_PENALTY        = 150;
        const int SETUP_DIES_AFTER_ACTING_PENALTY  = 100;
        const int HEAVY_SETUP_DAMAGE_PENALTY       = 50;
        const int SETUP_THREATEN_KO_NEXT_TURN      = +30;
        const int SETUP_FORCE_SWITCH_BONUS         = +30;

        int score = eval.Score;
        var top = eval.Top;

        _ai.CurrentLog.Add( $"===[Evaluating Setup Action (Score: {score})]===" );

        if( eval.MovePayload == null )
        {
            _ai.CurrentLog.Add( $"No setup move selected! Returning hopefully tanked score! Score: {score}" );
            return eval;
        }

        if( score == -999 )
        {
            _ai.CurrentLog.Add( $"Score was tanked at the heuristic level! Skipping! Score: {score}" );
            return eval;
        }

        //--We died before the setup completed
        if( top.Attacker_DiesBeforeActing )
        {
            score -= DIE_BEFORE_ACTING_PENALTY;
            eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker dies before setup completes! Score: {score}" );
            return eval;
        }

        //--We get KOd even if we setup
        if( top.Attacker_EndOfTurnHP <= 0 )
        {
            score -= SETUP_DIES_AFTER_ACTING_PENALTY;
            eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker faints after setting up! Score: {score}" );
            return eval;
        }

        //--Severe damage taken while setting up
        if( top.Attacker_EndOfTurnHP <= 0.3f )
        {
            score -= HEAVY_SETUP_DAMAGE_PENALTY;
            _ai.CurrentLog.Add( $"Took big damage! Score: {score}" );
        }

        //--"Slight Look ahead"

        var nextRoundMTR = _ai.MoveCommand.Get_BestSimulatedAttack( top.Attacker, top.Opponent, "Evaluate Setup Action" );
        var nextTOP = nextRoundMTR.Top;

        if( nextTOP.Attacker_DiesBeforeActing )
        {
            score -= DIE_BEFORE_ACTING_PENALTY;
            eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker dies before setup completes! Score: {score}" );
            return eval;
        }

        if( nextTOP.Attacker_EndOfTurnHP <= 0f )
        {
            score -= SETUP_DIES_AFTER_ACTING_PENALTY;
            eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker faints after setting up! Score: {score}" );
            return eval;
        }

        if( nextTOP.Opponent_DiesBeforeActing )
        {
            score += SETUP_THREATEN_KO_NEXT_TURN + 15;
            _ai.CurrentLog.Add( $"Setup likely KO without taking damage next turn! Score: {score}" );
        }
        else if( nextTOP.Opponent_EndOfTurnHP <= 0f )
        {
            score += SETUP_THREATEN_KO_NEXT_TURN;
            _ai.CurrentLog.Add( $"Setup likely KO next turn! Score: {score}" );
        }

        if( nextTOP.OpponentPTKO < top.OpponentPTKO )
        {
            score += 15;
            _ai.CurrentLog.Add( $"Setup is more defensive next turn! Score: {score}" );
        }
        
        if( (int)nextTOP.OpponentPTKO - 1 < (int)top.OpponentPTKO )
        {
            score += 10;
            _ai.CurrentLog.Add( $"Setup walls hard next turn! Score: {score}" );
        }

        float damageDone = nextTOP.Attacker.CurrentHPR - nextTOP.Attacker_EndOfTurnHP;
        if( damageDone <= 0.25f )
        {
            score += 15;
            _ai.CurrentLog.Add( $"Setup takes minimal damage next turn! Score: {score}" );
        }
        else if( damageDone >= 0.45f )
        {
            score -= 20;
            _ai.CurrentLog.Add( $"Setup takes decent damage next turn! Score: {score}" );
        }

        //--Opponent is now in KO range next turn
        bool movesFirst = nextTOP.Attacker.Speed > nextTOP.Opponent.Speed;
        bool weForceSwitch = _ai.UnitSim.PredictSwitchProbability( nextTOP.AttackerPTKO, nextTOP.OpponentPTKO, movesFirst, nextTOP.Attacker.CurrentHPR, nextTOP.Opponent.CurrentHPR ) >= 0.8f;

        if( weForceSwitch )
        {
            score += SETUP_FORCE_SWITCH_BONUS;
            _ai.CurrentLog.Add( $"Setup forces opponent to switch! {score}" );
        }

        var oppTeam = _ai.GetRemainingOpposingPokemon( nextTOP.Attacker.PID );
        int fasterBonus = 0;
        bool weKO = nextTOP.Opponent_DiesBeforeActing || nextTOP.Opponent_EndOfTurnHP <= 0f;
        bool sweepBeginning = weKO || weForceSwitch;

        if( sweepBeginning )
        {
            foreach( var opp in oppTeam )
            {
                int oppSpeed = _ai.GetUnitContextualSpeed( opp );

                if( nextTOP.Attacker.Speed > oppSpeed )
                    fasterBonus += 5;
            }

            score += fasterBonus;
            _ai.CurrentLog.Add( $"Outspeeds {fasterBonus / 5} opposing Pokémon after setup! {score}" );
        }

        eval.Score = score;
        return eval;
    }
}
