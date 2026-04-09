using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ActionType { Attack, OffensiveSwitch, DefensiveSwitch, Setup, OffensiveStatus, Support }
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
            targetUnit = _ai.GetBattleUnit( target.PID ); //--It's possible that targets are coming back wrong here for attacks?

        _ai.CurrentLog.Add( $"===[Built Action Evaluation for {eval.Type}. Base Score + PBS Score: {eval.Score}]===" );

        switch( type )
        {
            case ActionType.Attack: //--and--//
            case ActionType.Setup:
            case ActionType.OffensiveStatus:
                eval.Target = targetUnit;
                eval.MovePayload = (Move)payload;
                _ai.CurrentLog.Add( $"Attack's Target: (passed) {target.Name}, (battle unit searched) {eval.Target.Pokemon.NickName}" );
                break;

            case ActionType.DefensiveSwitch: //--and--//
            case ActionType.OffensiveSwitch:
                eval.SwitchPayload = (Pokemon)payload;
                _ai.CurrentLog.Add( $"Switch Candidate: {eval.SwitchPayload.NickName}" );
                break;
        }

        return eval;
    }

    public ActionEvaluation EvaluateAction( ActionEvaluation eval )
    {
        return eval.Type switch
        {
            ActionType.Attack           => EvaluateAttackAction( eval ),
            ActionType.DefensiveSwitch  => EvaluateDefensiveSwitchAction( eval ),
            ActionType.OffensiveSwitch  => EvaluateOffensiveSwitchAction( eval ),
            ActionType.Setup            => EvaluateSetupAction( eval ),
            ActionType.OffensiveStatus  => EvaluateOffensiveStatusAction( eval ),
            _ => eval,
        };
    }

    private float NormalizeDamage( float rawDamage, float currentHPR )
    {
        return rawDamage / Mathf.Max( currentHPR, 0.001f );
    }

    private ActionEvaluation EvaluateAttackAction( ActionEvaluation eval )
    {
        int score = eval.Score;
        var top = eval.Top;

        _ai.CurrentLog.Add( $"===[Evaluating Attack Action. ( Base Score: {score})]===" );

        if( eval.MovePayload == null )
        {
            _ai.CurrentLog.Add( $"No Attacking move was picked! Returning hopefully tanked score! {score}" );
            return eval;
        }

        //--Tactical disaster: we die before acting
        if( top.Attacker_DiesBeforeActing )
        {
            score -= 70;
            _ai.CurrentLog.Add( $"Attacker dies before acting! Score: {score}" );
        }

        //--Tactical perfection: we KO before they act
        if( top.Opponent_DiesBeforeActing )
        {
            score += 35;
            _ai.CurrentLog.Add( $"Opponent dies before acting! Score: {score}" );
        }

        //--Mutual KO (small penalty, PBS handles material)
        if( top.MutualKO )
        {
            score -= 10;
            _ai.CurrentLog.Add( $"Mutual KO! Score: {score}" );
        }

        bool movesFirst = top.Attacker.Speed > top.Opponent.Speed;

        //--We potentially force a switch, punish the switch in!
        if( _ai.UnitSim.PredictForcedSwitch( top.AttackerPTKO, top.OpponentPTKO, movesFirst ) )
        {
            score += 25;
            _ai.CurrentLog.Add( $"We threaten to force a switch! Score: {score}" );
        }

        //--Look Ahead Section-------------------------

        var next = _ai.MoveCommand.GetMove_BestAttack( top.Attacker, top.Opponent ).Top;

        bool weKOThem = next.Opponent_DiesBeforeActing || next.Opponent_EndOfTurnHP <= 0f;
        bool weDie = next.Attacker_DiesBeforeActing || next.Attacker_EndOfTurnHP <= 0f;

        if( weKOThem )
            score += 50;

        if( weDie )
            score -= 70;

        bool weMaintainPressure = next.AttackerPTKO >= PotentialToKO.TwoHKO;
        bool theyThreatenUs = next.OpponentPTKO >= PotentialToKO.Dangerous && !next.AttackerMovedFirst;

        if( weMaintainPressure )
            score += 25;

        if( theyThreatenUs )
            score -= 30;

        //--Reward tanks for taking very little damage the turn after switching in.
        float damageTakenRaw = top.Attacker.CurrentHPR - next.Attacker_EndOfTurnHP;
        float damageTaken = NormalizeDamage( damageTakenRaw, top.Attacker.CurrentHPR );
        if( damageTaken >= 0.4f )           score -= 20;
        else if( damageTaken >= 0.2f )      score -= 10;

        //--Reward doing acceptable chip.
        float oppHPLossRaw = top.Opponent_EndOfTurnHP - next.Opponent_EndOfTurnHP;
        float oppHPLoss = NormalizeDamage( oppHPLossRaw, top.Opponent_EndOfTurnHP );
        if( oppHPLoss >= 0.6f )             score += 45;
        else if( oppHPLoss >= 0.45f )       score += 30;
        else if( oppHPLoss >= 0.25f )       score += 15;

        bool badTrade = damageTaken >= 0.45f && oppHPLoss <= 0.25f;

        if( badTrade )
            score -= 40;

        bool weAreForcedOut = _ai.UnitSim.PredictSwitchProbability( next.OpponentPTKO, next.AttackerPTKO, next.AttackerMovedFirst, top.Opponent_EndOfTurnHP, top.Attacker_EndOfTurnHP ) >= 0.8f;
        bool theyAreForcedOut = _ai.UnitSim.PredictSwitchProbability( next.AttackerPTKO, next.OpponentPTKO, next.AttackerMovedFirst, top.Attacker_EndOfTurnHP, top.Opponent_EndOfTurnHP ) >= 0.8f;

        if( theyAreForcedOut )
            score += 25;

        if( weAreForcedOut )
            score -= 30;

        eval.Score = score;
        _ai.CurrentLog.Add( $"Final Score: {score}" );
        return eval;
    }

    private ActionEvaluation EvaluateDefensiveSwitchAction( ActionEvaluation eval )
    {
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
            score = -999;
            eval.Score = score;
            _ai.CurrentLog.Add( $"Switch in (attacker) faints on switch in! Score: {score}" );
            return eval;
        }

        //--Critically low after entry. Will have to be careful here, end game switching might be more heavily penalized, which is somewhat reasonable.
        if( top.Attacker_EndOfTurnHP <= 0.2f )
        {
            score -= 30;
            _ai.CurrentLog.Add( $"Switch in (attacker) takes big damage on entry, leaving it at {top.Attacker_EndOfTurnHP} HP on switch in! Score: {score}" );
        }

        //--Look Ahead Portion-----------------

        //--We need to establish PTKOs and the general attack potential of the following round using the switch candidate.
        var next = _ai.MoveCommand.GetMove_BestAttack( top.Attacker, top.Opponent ).Top;

        //--First we compare threat
        bool weDie = next.Attacker_DiesBeforeActing || next.Attacker_EndOfTurnHP <= 0f;
        bool weKOThem = next.Opponent_DiesBeforeActing || next.Opponent_EndOfTurnHP <= 0f;

        bool theyThreatenUs = next.OpponentPTKO >= PotentialToKO.Dangerous && !next.AttackerMovedFirst;
        bool weThreatenThem = next.AttackerPTKO >= PotentialToKO.TwoHKO && next.AttackerMovedFirst;

        bool weCantThreatenBack = next.AttackerPTKO >= PotentialToKO.TwoHKO && !next.AttackerMovedFirst;

        bool weAreForcedOut = _ai.UnitSim.PredictSwitchProbability( next.OpponentPTKO, next.AttackerPTKO, next.AttackerMovedFirst, top.Opponent_EndOfTurnHP, top.Attacker_EndOfTurnHP ) >= 0.8f;
        bool theyAreForcedOut = _ai.UnitSim.PredictSwitchProbability( next.AttackerPTKO, next.OpponentPTKO, next.AttackerMovedFirst, top.Attacker_EndOfTurnHP, top.Opponent_EndOfTurnHP ) >= 0.8f;

        if( weDie )
        {
            score -= 50;
        }
        else if( theyThreatenUs )
        {
            score -= 35;
        }

        //--Reward tanks for taking very little damage the turn after switching in.
        float damageTakenRaw = top.Attacker.CurrentHPR - next.Attacker_EndOfTurnHP;
        float damageTaken = NormalizeDamage( damageTakenRaw, top.Attacker.CurrentHPR );
        if( damageTaken >= 0.6f )           score -= 30;
        else if( damageTaken >= 0.4f )      score -= 15;
        else if( damageTaken <= 0.15f )     score += 50;
        else if( damageTaken <= 0.3f )      score += 25;

        //--Reward doing acceptable chip.
        float oppHPLossRaw = top.Opponent_EndOfTurnHP - next.Opponent_EndOfTurnHP;
        float oppHPLoss = NormalizeDamage( oppHPLossRaw, top.Opponent_EndOfTurnHP );
        if( oppHPLoss >= 0.3f )             score += 25;
        else if( oppHPLoss >= 0.15f )       score += 10;

        if( weAreForcedOut && weCantThreatenBack )
        {
            score -= 60;
            _ai.CurrentLog.Add( $"Switch creates unstable position (forced out next turn)! Score: {score}" );
        }

        bool reEnteringBadMatchup = false;
        if( _ai.LastSentInPokemon != null )
        {
            bool lastMonStillOnField = false;
            for( int i = 0; i < _ai.LastOpposingPokemon.Count; i++ )
            {
                var lastOpp = _ai.LastOpposingPokemon[i];
                if( lastOpp.PID == top.Opponent.PID )
                {
                    lastMonStillOnField = true;
                    break;
                }
                else
                    continue;
            }

            reEnteringBadMatchup = _ai.LastSentInPokemon.PID == eval.SwitchPayload.PID && lastMonStillOnField;
        }

        if( reEnteringBadMatchup )
        {
            score -= 50;
            _ai.CurrentLog.Add( $"Switch Loop detected, chunking score! Score {score}" );
        }

        if( weThreatenThem || weKOThem || theyAreForcedOut )
        {
            score += 35;
        }

        eval.Score = score;
        return eval;
    }

    private ActionEvaluation EvaluateOffensiveSwitchAction( ActionEvaluation eval )
    {
        int score = eval.Score;
        var top = eval.Top;

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

        float entryDamage = 1 - top.Attacker_EndOfTurnHP;

        if( entryDamage > 0.6f )
            score -= 35;

        _ai.CurrentLog.Add( $"Attacker's Entry Damage: {entryDamage}. Score: {score}" );

        if( top.Attacker_EndOfTurnHP <= 0f )
            score -= 100;
        else if( top.Attacker_EndOfTurnHP <= 0.2f )
            score -= 60;

        _ai.CurrentLog.Add( $"Attacker end of turn HP: {top.Attacker_EndOfTurnHP}. Score: {score}" );

        bool opponentThreatenedNextTurn = top.Opponent_EndOfTurnHP <= 0.5f && top.Attacker.Speed > top.Opponent.Speed;
        bool survives = top.Attacker_EndOfTurnHP >= 0.2f;
        if( opponentThreatenedNextTurn && survives )
            score += 25;

        _ai.CurrentLog.Add( $"Attacker threatens Opponent next turn: {opponentThreatenedNextTurn}. Score: {score}" );

        //--Look Ahead Section-------------------

        var next = _ai.MoveCommand.GetMove_BestAttack( top.Attacker, top.Opponent ).Top;

        bool weKOThem = next.Opponent_DiesBeforeActing || next.Opponent_EndOfTurnHP <= 0f;
        if( weKOThem )
            score += 60;

        bool weThreaten = next.AttackerPTKO >= PotentialToKO.Dangerous;
        if( weThreaten )
            score += 35;

        bool theyAreForcedOut = _ai.UnitSim.PredictSwitchProbability( next.AttackerPTKO, next.OpponentPTKO, next.AttackerMovedFirst, top.Attacker_EndOfTurnHP, top.Opponent_EndOfTurnHP ) >= 0.8f;
        if( theyAreForcedOut )
            score += 40;

        float oppHPLossRaw = top.Opponent_EndOfTurnHP - next.Opponent_EndOfTurnHP;
        float oppHPLoss = NormalizeDamage( oppHPLossRaw, top.Opponent_EndOfTurnHP );
        if( oppHPLoss >= 0.4f )
            score += 30;
        else if( oppHPLoss >= 0.25f )
            score += 20;

        bool weDie = next.Attacker_DiesBeforeActing || next.Attacker_EndOfTurnHP <= 0f;
        if( weDie )
            score -= 80;

        bool weAreForcedOut = _ai.UnitSim.PredictSwitchProbability( next.OpponentPTKO, next.AttackerPTKO, next.AttackerMovedFirst, top.Opponent_EndOfTurnHP, top.Attacker_EndOfTurnHP ) >= 0.8f;
        if( weAreForcedOut )
            score -= 60;

        float damageTakenRaw = top.Attacker.CurrentHPR - next.Attacker_EndOfTurnHP;
        float damageTaken = NormalizeDamage( damageTakenRaw, top.Attacker.CurrentHPR );
        bool noPressure = next.AttackerPTKO < PotentialToKO.TwoHKO;

        if( noPressure && ( damageTaken >= 0.4f || oppHPLoss < 0.2f && damageTaken >= 0.3f ) )
            score -= 40;

        eval.Score = score;
        return eval;
    }

    private ActionEvaluation EvaluateSetupAction( ActionEvaluation eval )
    {
        const int DIE_BEFORE_ACTING_PENALTY        = 150;
        const int SETUP_DIES_AFTER_ACTING_PENALTY  = 100;
        const int HEAVY_SETUP_DAMAGE_PENALTY       = 50;
        const int SETUP_THREATEN_KO_NEXT_TURN      = 30;
        const int SETUP_FORCE_SWITCH_BONUS         = 30;

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

        bool weForceSwitch = _ai.UnitSim.PredictSwitchProbability( top.AttackerPTKO, top.OpponentPTKO, top.AttackerMovedFirst, top.Attacker.CurrentHPR, top.Opponent.CurrentHPR ) >= 0.85f;

        //--We died before the setup completed
        if( top.Attacker_DiesBeforeActing && !weForceSwitch )
        {
            score -= DIE_BEFORE_ACTING_PENALTY;
            eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker dies before setup completes! Score: {score}" );
            return eval;
        }

        //--We get KOd even if we setup
        if( top.Attacker_EndOfTurnHP <= 0 && !weForceSwitch )
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

        // foreach( var kvp in top.Attacker.StatStages )
        //     Debug.Log( $"[Stat Stage Check] Attacker: {top.Attacker.Name}, Stat: {kvp.Key}, Change: {kvp.Value}" );

        // foreach( var kvp in top.Opponent.StatStages )
        //     Debug.Log( $"[Stat Stage Check] Opponent: {top.Opponent.Name}, Stat: {kvp.Key}, Change: {kvp.Value}" );

        //--"Slight Look ahead" //--maybe add fork for switch
        var nextRoundMTR = _ai.MoveCommand.GetMove_BestAttack( top.Attacker, top.Opponent, "Evaluate Setup Action" ); //--stat change issues
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

        float damageTakenRaw = nextTOP.Attacker.CurrentHPR - nextTOP.Attacker_EndOfTurnHP;
        float damageTaken = NormalizeDamage( damageTakenRaw, nextTOP.Attacker.CurrentHPR );
        if( damageTaken <= 0.25f )
        {
            score += 15;
            _ai.CurrentLog.Add( $"Setup takes minimal damage next turn! Score: {score}" );
        }
        else if( damageTaken >= 0.45f )
        {
            score -= 20;
            _ai.CurrentLog.Add( $"Setup takes decent damage next turn! Score: {score}" );
        }

        //--Opponent is now in KO range next turn
        bool movesFirst = nextTOP.Attacker.Speed > nextTOP.Opponent.Speed;
        bool weForceSwitchNextTurn = _ai.UnitSim.PredictSwitchProbability( nextTOP.AttackerPTKO, nextTOP.OpponentPTKO, movesFirst, nextTOP.Attacker.CurrentHPR, nextTOP.Opponent.CurrentHPR ) >= 0.8f;

        if( weForceSwitchNextTurn )
        {
            score += SETUP_FORCE_SWITCH_BONUS;
            _ai.CurrentLog.Add( $"Setup forces opponent to switch! {score}" );
        }

        var oppTeam = _ai.GetRemainingOpposingPokemon( nextTOP.Attacker.PID );
        int fasterBonus = 0;
        bool weKO = nextTOP.Opponent_DiesBeforeActing || nextTOP.Opponent_EndOfTurnHP <= 0f;
        bool sweepBeginning = weKO || weForceSwitchNextTurn;

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

    private ActionEvaluation EvaluateOffensiveStatusAction( ActionEvaluation eval )
    {
        int score = eval.Score;
        var top = eval.Top;

        _ai.CurrentLog.Add( $"===[Evaluating Offensive Status Action (Score: {score})]===" );

        bool weForceSwitch = _ai.UnitSim.PredictSwitchProbability( top.AttackerPTKO, top.OpponentPTKO, top.AttackerMovedFirst, top.Attacker.CurrentHPR, top.Opponent.CurrentHPR ) >= 0.85f;

        if( top.Attacker_DiesBeforeActing && !weForceSwitch )
        {
            score -= 120;
            eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker Dies Before Acting! Score: {score}" );
            return eval;
        }

        if( top.Attacker_EndOfTurnHP <= 0f && !weForceSwitch )
        {
            score -= 90;
            eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker Dies! Score: {score}" );
            return eval;
        }

        if( top.Attacker_EndOfTurnHP <= 0.35f )
        {
            score -= 40;
            _ai.CurrentLog.Add( $"Attacker end of turn hp: {top.Attacker_EndOfTurnHP} Score: {score}" );
        }

        if( !top.OpponentCanAct )
        {
            score += 25;
            _ai.CurrentLog.Add( $"Opponent Can't Act! Score: {score}" );
        }

        var next = _ai.MoveCommand.GetMove_BestAttack( top.Attacker, top.Opponent ).Top;

        bool weNowMoveFirst = next.Attacker.Speed > next.Opponent.Speed;
        if( !top.AttackerMovedFirst && weNowMoveFirst )
        {
            score += 40;
            _ai.CurrentLog.Add( $"We outspeed next turn when we don't currently! Score: {score}" );
        }

        if( next.OpponentPTKO < top.OpponentPTKO || next.AttackerPTKO > top.AttackerPTKO )
        {
            score += 25;
            _ai.CurrentLog.Add( $"Survival or Offense improves next turn! Score: {score}" );
        }

        if( (int)next.OpponentPTKO < (int)top.OpponentPTKO - 1 || (int)next.AttackerPTKO > (int)top.AttackerPTKO + 1 )
        {
            score += 15;
            _ai.CurrentLog.Add( $"Survival or Offense improves next turn dramatically! Score: {score}" );
        }

        if( next.Opponent_DiesBeforeActing )
        {
            score += 45;
            _ai.CurrentLog.Add( $"Opponent dies before acting next turn! Score: {score}" );
        }
        else if( next.Opponent_EndOfTurnHP <= 0f )
        {
            score += 30;
            _ai.CurrentLog.Add( $"Opponent dies next turn! Score: {score}" );
        }

        if( next.AttackerPTKO >= PotentialToKO.TwoHKO && weNowMoveFirst )
        {
            score += 20;
            _ai.CurrentLog.Add( $"We maintain pressure with speed advantage! Score: {score}" );
        }
        else if( next.AttackerPTKO >= PotentialToKO.Risky )
        {
            score += 10;
            _ai.CurrentLog.Add( $"We maintain decent ko pressure! Score: {score}" );
        }

        if( next.Attacker_DiesBeforeActing )
        {
            score -= 60;
            eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker dies before acting! Score: {score}" );
            return eval;
        }

        if( next.Attacker_EndOfTurnHP <= 0f )
        {
            score -= 50;
            eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker dies! Score: {score}" );
            return eval;
        }

        bool weForceSwitchNextTurn = _ai.UnitSim.PredictSwitchProbability( next.AttackerPTKO, next.OpponentPTKO, next.AttackerMovedFirst, top.Attacker_EndOfTurnHP, top.Opponent_EndOfTurnHP ) >= 0.8f;
        if( weForceSwitchNextTurn )
        {
            score += 35;
            _ai.CurrentLog.Add( $"We force a switch! Score: {score}" );
        }

        float damageTakenRaw = next.Attacker.CurrentHPR - next.Attacker_EndOfTurnHP;
        float damageTaken = NormalizeDamage( damageTakenRaw, next.Attacker.CurrentHPR );
        if( damageTaken <= 0.2f )
            score += 10;
        else if( damageTaken >= 0.4f )
            score -= 15;

        _ai.CurrentLog.Add( $"Damage Taken: {damageTaken}. Score: {score}" );

        List<CourtConditionID> courtConditions = new();
        if( top.Opponent.CourtLocation == CourtLocation.TopCourt )
            courtConditions = top.Field.TopCourtConditions;
        else if( top.Opponent.CourtLocation == CourtLocation.BottomCourt )
            courtConditions = top.Field.BottomCourtConditions;

        var oppCourtConditions = courtConditions;
        bool hasHazards = oppCourtConditions.Count > 0;

        _ai.CurrentLog.Add( $"Opposing Court's hazard count: {oppCourtConditions.Count}" );

        if( weForceSwitchNextTurn && hasHazards )
        {
            float hazardDamage = 0f;

            foreach( var condition in oppCourtConditions )
                hazardDamage += _ai.Get_EntryHazardDamage( top.Opponent, condition );

            int hazardScore = Mathf.FloorToInt( hazardDamage * 120f );
            score += hazardScore;

            if( !top.OpponentCanAct || weNowMoveFirst )
                score += 10;

            _ai.CurrentLog.Add( $"We have hazard pressure! Hazard Damage: {hazardDamage}, Hazard Score: {hazardScore} Score: {score}" );
        }

        eval.Score = score;
        return eval;
    }

    public ActionEvaluation EvaluateSacrificeLine( ActionEvaluation eval, DoomedOutcome doomedOutcome )
    {

        var sac = SacrificeScore( eval, doomedOutcome );

        int score = sac.Score;

        switch( sac.Type )
        {
            case ActionType.Attack:
                    score += Mathf.FloorToInt( ( 1 - sac.Top.Opponent_EndOfTurnHP ) * 30 );
                break;

            case ActionType.DefensiveSwitch:
                    if( sac.Top.Attacker_EndOfTurnHP > 0.3f )
                        score += 20;
                break;

            case ActionType.OffensiveSwitch:
                    if( sac.Top.AttackerMovedFirst && sac.Top.AttackerPTKO >= PotentialToKO.Risky )
                        score += 25;
                break;

            case ActionType.Setup:
                score = -999; //--Tank, setup not considered here. it may be possible to defensively setup to gain an advantage, though, but we can add that consideration later.
                break;

            case ActionType.OffensiveStatus:
                score = -999; //--Handle uniquely later, for now we'll tank.
                break;
        };

        sac.Score = score;
        return sac;
    }

    private ActionEvaluation SacrificeScore( ActionEvaluation eval, DoomedOutcome doomedOutcome )
    {
        int score = 0;

        bool weDieThisTurn = eval.Top.Attacker_EndOfTurnHP <= 0 || eval.Top.Attacker_DiesBeforeActing;

        //--Don't want to bias toward accidental survival. I guess.
        if( !weDieThisTurn )
        {
            score -= 25;
        }

        //--Get Next round Pokemon
        BattleAI_PokemonAdapter revengeCandidate = null;
        if( eval.Top.Attacker_DiesBeforeActing || eval.Top.Attacker_EndOfTurnHP <= 0 )
        {
            var switchCandidate = _ai.SwitchCommand.GetSwitch_Revenge( _ai.OpposingUnits ).Pokemon;
            if( switchCandidate != null )
                revengeCandidate = new( switchCandidate, _ai  );
        }
        else if( eval.Top.AttackerPTKO <= PotentialToKO.Safe && eval.Top.OpponentPTKO >= PotentialToKO.TwoHKO )
        {
            var switchCandidate = _ai.SwitchCommand.GetSwitch_Revenge( _ai.OpposingUnits ).Pokemon;
            if( switchCandidate != null )
                revengeCandidate = new( switchCandidate, _ai  );
        }

        IBattleAIUnit nextPokemon;
        if( revengeCandidate != null )
            nextPokemon = revengeCandidate;
        else
            nextPokemon = eval.Top.Attacker;

        //--Look ahead at the next round
        var followUp = _ai.MoveCommand.GetMove_BestAttack( nextPokemon, eval.Top.Opponent ).Top;

        //--Revenge Kill Success
        if( followUp.Opponent_DiesBeforeActing )
        {
            score += 120;
            _ai.CurrentLog.Add( $"Opponent Dies Before Acting! Score: {score}" );
        }
        else if( followUp.Opponent_EndOfTurnHP <= 0 )
        {
            score += 100;
            _ai.CurrentLog.Add( $"Opponent Dies! Score: {score}" );
        }

        //--Strong Pressure/Near KO
        if( followUp.AttackerPTKO >= PotentialToKO.Dangerous )
        {
            score += 60;
            _ai.CurrentLog.Add( $"Attacker has strong pressure! PTKO: {followUp.AttackerPTKO} Score: {score}" );
        }
        else if( followUp.AttackerPTKO >= PotentialToKO.Risky )
        {
            score += 40;
            _ai.CurrentLog.Add( $"Attacker has strong pressure! PTKO: {followUp.AttackerPTKO} Score: {score}" );
        }
        else if( followUp.AttackerPTKO >= PotentialToKO.TwoHKO && followUp.AttackerMovedFirst )
        {
            score += 25;
            _ai.CurrentLog.Add( $"Attacker has strong pressure! PTKO: {followUp.AttackerPTKO} Score: {score}" );
        }

        //--Opponent HP after sac, before next turn starts
        float oppHP_before = eval.Top.Opponent_EndOfTurnHP;
        score += Mathf.FloorToInt( ( 1 - oppHP_before ) * 80 );
        _ai.CurrentLog.Add( $"Opponent's HP After Sacrificing, before next turn starts: {oppHP_before}. Score: {score}" );

        if( followUp.Attacker_EndOfTurnHP > 0.3f )
            score += 30;
        else if( followUp.Attacker_EndOfTurnHP <= 0 )
            score -= 50;

        _ai.CurrentLog.Add( $"Attacker end of look ahead/follow up turn HP: {followUp.Attacker_EndOfTurnHP}. Score: {score}" );

        //--Tempo Recovery from speed control
        if( followUp.AttackerMovedFirst )
            score += 20;

        _ai.CurrentLog.Add( $"Attacker Moves first in follow up round? {followUp.AttackerMovedFirst} Score: {score}" );

        //--Forced Switch check on follow up turn. we use the next pokemon's current hpr and eval TOP opponent's end of turn hpr because that's the hp they will start the follow up round with. we want to know if we force a switch during that round, not after.
        if( _ai.UnitSim.PredictSwitchProbability( followUp.AttackerPTKO, followUp.OpponentPTKO, followUp.AttackerMovedFirst, nextPokemon.CurrentHPR, eval.Top.Opponent_EndOfTurnHP ) >= 0.8 )
        {
            score += 30;
            _ai.CurrentLog.Add( $"Opponent is likely forced to switch in follow up! Score: {score}" );
        }

        //--Dead end penalty to punish bad sacrifice lines
        if( ( followUp.Attacker_EndOfTurnHP <= 0 || followUp.Attacker_DiesBeforeActing ) && followUp.Opponent_EndOfTurnHP >= 0.5f )
        {
            score -= 100;
            _ai.CurrentLog.Add( $"Dead end detected for {eval.Type} decision line! Penalizing... Score: {score}" );
        }

        //--Piece Value death penalty
        if( _ai.TeamPieceValues.TryGetValue( eval.Top.Attacker.PID, out var pieceValue ) )
        {
            int deathValuePenalty = Mathf.FloorToInt( pieceValue.OffensiveValue * 0.5f );
            score -= deathValuePenalty;
            _ai.CurrentLog.Add( $"Piece Value: {pieceValue.OffensiveValue}. Penalty: {deathValuePenalty}. Score: {score}" );
        }

        //--Break No Tempo Recovery State
        if( doomedOutcome.NoTempoRecoveryLine )
        {
            if( followUp.OpponentPTKO <= PotentialToKO.TwoHKO )
            {
                score += 30;
                _ai.CurrentLog.Add( $"Tempo Recovery found for {eval.Type} decision line! Score: {score}" );
            }
        }

        if( nextPokemon != null && _ai.TeamPieceValues.TryGetValue( nextPokemon.PID, out var nextValue ) )
        {
            int reward = Mathf.FloorToInt( nextValue.OffensiveValue * 0.3f );

            if( followUp.Opponent_DiesBeforeActing )
                reward += 20;

            score += reward;

            if( pieceValue.OffensiveValue < 20 && doomedOutcome.DoomedTurn )
            {
                score += 30; // free sack
            }
        }

        //--Final Pressure check. least sure about this feature, remove if wonky.
        float pressureMultiplier = 1f + ( doomedOutcome.PressureScore * 0.15f );
        score = Mathf.FloorToInt( score * pressureMultiplier );

        _ai.CurrentLog.Add( $"Pressure Score: {doomedOutcome.PressureScore}. Multiplier: {pressureMultiplier}. Score: {score}" );
        _ai.CurrentLog.Add( $"Final Sacrifice Evaluation Score for {eval.Type} decision line: {score}" );

        eval.Top = followUp;
        eval.Score = score;
        return eval;
    }


}
