using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ActionType { Attack, OffensiveSwitch, DefensiveSwitch, Setup, Support }
public class BattleAI_ActionEvaluation
{
    private const int DIE_BEFORE_ACTING_PENALTY = 60;
    private const int CLEAN_KO_BONUS = 35;
    private const int MUTUAL_KO_PENALTY = 10;
    private const int SWITCH_DIES_PENALTY = 80;
    private const int CRITICAL_ENTRY_PENALTY = 30;
    private BattleAI _ai;
    public ActionType Type { get; set; }
    public Pokemon ActingMon { get; set; }
    public Move SelectedMove { get; set; }
    public Pokemon SwitchTarget { get; set; }
    public int HeuristicScore { get; set; }
    public int PBSScore { get; set; }
    public int FinalScore { get; set; }

    public BattleAI_ActionEvaluation( BattleAI ai )
    {
        _ai = ai;
    }

    public ActionEvaluation BuildActionEvaluation( ActionType type, int baseScore, ProjectedBoardState pbs, object payload, TurnOutcomeProjection top )
    {
        int pbsScore = _ai.Projection.EvaluatePBS( pbs );
        ActionEvaluation eval = new()
        {
            Type = type,
            Score = baseScore + pbsScore,
            Top = top,
        };

        switch( type )
        {
            case ActionType.Attack: eval.MovePayload = (Move)payload;
                break;

            case ActionType.DefensiveSwitch: //--and--//
            case ActionType.OffensiveSwitch: eval.SwitchPayload = (Pokemon)payload;
                break;
        }

        _ai.CurrentLog.Add( $"===[Built Action Evaluation for {eval.Type}. Base Score + PBS Score: {eval.Score}]===" );

        return eval;
    }

    public ActionEvaluation EvaluateAction( ActionEvaluation eval )
    {
        switch( eval.Type )
        {
            case ActionType.Attack:             return EvaluateAttackAction( eval );
            case ActionType.DefensiveSwitch:    return EvaluateDefensiveSwitchAction( eval );
            case ActionType.OffensiveSwitch:     return EvaluateOffensiveSwitchAction( eval );
            default: return eval;
        };
    }

    private ActionEvaluation EvaluateAttackAction( ActionEvaluation eval )
    {
        int score = eval.Score;
        var top = eval.Top;

        _ai.CurrentLog.Add( $"===[Evaluating Attack Action (Score: {score})]===" );

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
        if( _ai.UnitSim.WeForceSwitch( top.AttackerPTKO, top.OpponentPTKO, movesFirst ) )
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
        var top = eval.Top;
        int score = eval.Score;

        _ai.CurrentLog.Add( $"===[Evaluating Defensive Switch Action (Score: {score})]===" );

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
}
