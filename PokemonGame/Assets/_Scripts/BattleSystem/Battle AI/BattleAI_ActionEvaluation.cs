using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum ActionType { Attack, OffensiveSwitch, DefensiveSwitch, Setup, OffensiveStatus, Support }
public class BattleAI_ActionEvaluation
{
    private BattleAI _ai;

    public BattleAI_ActionEvaluation( BattleAI ai )
    {
        _ai = ai;
    }

    public ActionEvaluation BuildActionEvaluation( ActionType type, int baseScore, IBattleAIUnit target, BattleUnit targetBattleUnit, object payload, TurnOutcomeProjection top, ExchangeEvaluation exchangeEval )
    {
        ActionEvaluation eval = new()
        {
            Type = type,
            Score = baseScore,
            Top1 = top,
            ExchangeEvaluation = exchangeEval,
            ActorPID = top.Attacker.PID,
        };

        BattleUnit targetUnit = null;
        if( target != null )
        {
            // targetUnit = _ai.GetBattleUnit( target.Pokemon ); //--It's possible that targets are coming back wrong here for attacks? -- yes, yes they are. we're somehow getting targets passed into this function that aren't even on the field... --5/2/26 @ 2:20am
            targetUnit = targetBattleUnit;
            _ai.CurrentLog.Add( $"Intended Target: {target.Name}" );
            _ai.CurrentLog.Add( $"Battle Unit Pokemon: {targetUnit.Pokemon.NickName}" );
        }
        else if( type != ActionType.OffensiveSwitch && type != ActionType.DefensiveSwitch )
            Debug.LogError( $"Target is null for a move action!" );

        _ai.CurrentLog.Add( $"===[Built Action Evaluation for {eval.Type}. ActionScore: {eval.Score}]===" );

        switch( type )
        {
            case ActionType.Attack: //--and--//
            case ActionType.Setup:
            case ActionType.OffensiveStatus:
                eval.Target = targetUnit;
                _ai.CurrentLog.Add( $"" );
                _ai.CurrentLog.Add( $"Attack's Target: (passed) {target.Name}" );
                eval.MovePayload = (Move)payload;
                _ai.CurrentLog.Add( $"(battle unit searched) {eval.Target.Pokemon.NickName}" );
                break;

            case ActionType.DefensiveSwitch: //--and--//
            case ActionType.OffensiveSwitch:
                eval.SwitchPayload = (Pokemon)payload;
                _ai.CurrentLog.Add( $"Switch Candidate: {eval.SwitchPayload.NickName}" );
                break;
        }

        _ai.CurrentLog.Add( $"================================================================" );
        _ai.CurrentLog.Add( $"" );

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
        var top = eval.Top1;

        _ai.CurrentLog.Add( $"===[Evaluating Attack Action. (Base Score: {score})]===" );

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

        //--If force a switch, punish the switch in!
        float theySwitchProbability = eval.ExchangeEvaluation.OpponentSwitchProbability;
        score += Mathf.FloorToInt( 25f * theySwitchProbability );
        _ai.CurrentLog.Add( $"Probability the opponent switches: {theySwitchProbability}. Score: {score}" );

        //--Risky survival push
        var ee = eval.ExchangeEvaluation;
        bool weMightSurvive = top.OpponentPTKO != PotentialToKO.OHKO && ee.OpponentPTKOR.PTKO >= PotentialToKO.Risky;
        bool weFaintInSim = top.Attacker_EndOfTurnHP <= 0f;

        if( weMightSurvive && weFaintInSim )
        {
            int comebackPotential = 0;
            bool opponentSelfDebuffs = _ai.UnitSim.CheckHasSelfDebuffMove( top.Opponent.ActiveMoves ) && !top.AttackerMovedFirst;
            bool opponentChipsSelf = ( _ai.UnitSim.CheckHasRecoilMove( top.Opponent.ActiveMoves ) || top.Opponent.Item == BattleItemEffectID.LifeOrb ) && !top.AttackerMovedFirst;

            if( ee.AttackerThreatensKO )
                comebackPotential += 2;

            if( opponentSelfDebuffs )
                comebackPotential += 2;

            if( opponentChipsSelf )
                comebackPotential += 2;

            score += comebackPotential * 25;
        }

        //--Look Ahead Section-------------------------
        bool weForceSwitch = UnityEngine.Random.value <= theySwitchProbability;
        
        var ourActiveAdapters = _ai.GetActiveAllyUnits_AsBattleAIUnits( _ai.Unit.Pokemon );
        
        var offensiveSwitch = _ai.SwitchCommand.GetSwitch_Revenge( ourActiveAdapters ).Pokemon;
        var defensiveSwitch = _ai.SwitchCommand.GetSwitch_Defensive( top.Opponent ).Top.Attacker;

        SimulatedUnit nextOpponent;
        MoveThreatResult nextOpponentMTR;

        if( top.Opponent_EndOfTurnHP <= 0f && offensiveSwitch != null )
        {
            BattleAI_PokemonAdapter opponentOffensiveSwitchAdapter = _ai.GetPokemonAs_Adapter( offensiveSwitch );
            nextOpponentMTR = _ai.MoveCommand.GetMove_BestAttack( opponentOffensiveSwitchAdapter, top.Attacker );
            nextOpponent = _ai.UnitSim.BuildSimUnit( opponentOffensiveSwitchAdapter, opponentOffensiveSwitchAdapter.BeginningHPR, nextOpponentMTR, top.Field );
        }
        else if( weForceSwitch && defensiveSwitch != null )
        {
            SimulatedUnit opponentDefensiveSwitchAdapter = defensiveSwitch;
            nextOpponentMTR = _ai.MoveCommand.GetMove_BestAttack( opponentDefensiveSwitchAdapter, top.Attacker );
            nextOpponent = _ai.UnitSim.BuildSimUnit( opponentDefensiveSwitchAdapter, opponentDefensiveSwitchAdapter.CurrentHPR, nextOpponentMTR, top.Field );
        }
        else
        {
            nextOpponentMTR = _ai.MoveCommand.GetMove_BestAttack( top.Opponent, top.Attacker );
            nextOpponent = _ai.UnitSim.BuildSimUnit( top.Opponent, top.Opponent_EndOfTurnHP, nextOpponentMTR, top.Field );
        }

        var next = _ai.MoveCommand.GetMove_BestAttack( top.Attacker, nextOpponent ).Top;

        bool weKOThem = next.Opponent_DiesBeforeActing || next.Opponent_EndOfTurnHP <= 0f;
        bool weDie = next.Attacker_DiesBeforeActing || next.Attacker_EndOfTurnHP <= 0f;

        if( weKOThem )
            score += 50;

        _ai.CurrentLog.Add( $"We KO them in the look ahead round! Score: {score}" );

        if( weDie )
            score -= 70;

        _ai.CurrentLog.Add( $"They KO us in the look ahead round! Score: {score}" );

        bool weMaintainPressure = next.AttackerPTKO >= PotentialToKO.TwoHKO;
        bool theyThreatenUs = next.OpponentPTKO >= PotentialToKO.Dangerous && !next.AttackerMovedFirst;

        if( weMaintainPressure )
            score += 25;

        _ai.CurrentLog.Add( $"We maintain pressure in the look ahead round! Score: {score}" );

        if( theyThreatenUs )
            score -= 30;

        _ai.CurrentLog.Add( $"They threaten us in the look ahead round! Score: {score}" );

        //--Reward tanks for taking very little damage the turn after switching in.
        float damageTakenRaw = top.Attacker.CurrentHPR - next.Attacker_EndOfTurnHP;
        float damageTaken = NormalizeDamage( damageTakenRaw, top.Attacker.CurrentHPR );
        if( damageTaken >= 0.4f )
        {
            score -= 20;
            _ai.CurrentLog.Add( $"We take more than 40% of our current hp in damage next round! Score: {score}" );
        }
        else if( damageTaken >= 0.2f )
        {
            score -= 10;
            _ai.CurrentLog.Add( $"We take more than 20% of our current hp in damage next round! Score: {score}" );
        }

        //--Reward doing acceptable chip.
        float oppHPLossRaw = top.Opponent_EndOfTurnHP - next.Opponent_EndOfTurnHP;
        float oppHPLoss = NormalizeDamage( oppHPLossRaw, top.Opponent_EndOfTurnHP );
        if( oppHPLoss >= 0.6f )
        {
            score += 45;
            _ai.CurrentLog.Add( $"We do 60% or more damage in the next round! Score: {score}" );
        }
        else if( oppHPLoss >= 0.45f )
        {
            score += 30;
            _ai.CurrentLog.Add( $"We do 45% or more damage in the next round! Score: {score}" );
        }
        else if( oppHPLoss >= 0.25f )
        {
            score += 15;
            _ai.CurrentLog.Add( $"We do 25% or more damage in the next round! Score: {score}" );
        }

        bool badTrade = damageTaken >= 0.45f && oppHPLoss <= 0.25f;

        if( badTrade )
        {
            score -= 40;
            _ai.CurrentLog.Add( $"Bad Trade detected. Score: {score}" );
        }

        //--Switch Check
        float weAreForcedOutProb = _ai.UnitSim.PredictSwitchProbability( next.OpponentPTKO, next.AttackerPTKO, next.AttackerMovedFirst, top.Opponent_EndOfTurnHP, top.Attacker_EndOfTurnHP, next.Attacker.Expendability );
        float theyAreForcedOutProb = _ai.UnitSim.PredictSwitchProbability( next.AttackerPTKO, next.OpponentPTKO, next.AttackerMovedFirst, top.Attacker_EndOfTurnHP, top.Opponent_EndOfTurnHP, next.Opponent.Expendability );

        score += Mathf.FloorToInt( 25f * weAreForcedOutProb );
        _ai.CurrentLog.Add( $"We force them to switch next round! Score: {score}" );

        score -= Mathf.FloorToInt( 30f * theyAreForcedOutProb );
        _ai.CurrentLog.Add( $"They force us to switch next round! Score: {score}" );

        eval.NextTurn_WeAreForcedOut = weAreForcedOutProb >= 0.7f;
        eval.NextTurn_TheyAreForcedOut = theyAreForcedOutProb >= 0.7f;

        eval.Top2 = next;
        eval.Score = score;
        _ai.CurrentLog.Add( $"Final Score: {score}" );
        return eval;
    }

    private ActionEvaluation EvaluateDefensiveSwitchAction( ActionEvaluation eval )
    {
        var top = eval.Top1;
        int score = eval.Score;

        _ai.CurrentLog.Add( $"===[Evaluating Defensive Switch Action (Score: {score})]===" );

        //--Switched mon dies on entry
        if( top.Attacker_EndOfTurnHP <= 0f )
        {
            score = -999;
            // eval.Score = score;
            _ai.CurrentLog.Add( $"Switch in (attacker) faints on switch in! Score: {score}" );
            // return eval;
        }

        //--Critically low after entry. Will have to be careful here, end game switching might be more heavily penalized, which is somewhat reasonable.
        if( top.Attacker_EndOfTurnHP <= 0.2f )
        {
            score -= 30;
            _ai.CurrentLog.Add( $"Switch in (attacker) takes big damage on entry, leaving it at {top.Attacker_EndOfTurnHP} HP on switch in! Score: {score}" );
        }

        //--Risky survival push
        var ee = eval.ExchangeEvaluation;
        bool weMightSurvive = ee.OpponentPTKOR.PTKO != PotentialToKO.OHKO && ee.OpponentPTKOR.PTKO >= PotentialToKO.Risky;
        bool weFaintInEval = !ee.AttackerSurvives;

        if( weMightSurvive && weFaintInEval )
        {
            int comebackPotential = 0;

            bool opponentSelfDebuffs = _ai.UnitSim.CheckHasSelfDebuffMove( top.Opponent.ActiveMoves ) && !top.AttackerMovedFirst;
            bool opponentChipsSelf = ( _ai.UnitSim.CheckHasRecoilMove( top.Opponent.ActiveMoves ) || top.Opponent.Item == BattleItemEffectID.LifeOrb ) && !top.AttackerMovedFirst;

            if( ee.AttackerThreatensKO )
                comebackPotential += 2;

            if( opponentSelfDebuffs )
                comebackPotential += 2;

            if( opponentChipsSelf )
                comebackPotential += 2;

            score -= comebackPotential * 10;
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

        float weAreForcedOut = _ai.UnitSim.PredictSwitchProbability( next.OpponentPTKO, next.AttackerPTKO, next.AttackerMovedFirst, top.Opponent_EndOfTurnHP, top.Attacker_EndOfTurnHP, next.Attacker.Expendability );
        float theyAreForcedOut = _ai.UnitSim.PredictSwitchProbability( next.AttackerPTKO, next.OpponentPTKO, next.AttackerMovedFirst, top.Attacker_EndOfTurnHP, top.Opponent_EndOfTurnHP, next.Opponent.Expendability );



        if( weDie )
        {
            score -= 60;
        }
        else if( theyThreatenUs )
        {
            score -= 45;
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

        if( weCantThreatenBack )
        {
            score -= Mathf.FloorToInt( 60f * weAreForcedOut);
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
            score -= 75;
            _ai.CurrentLog.Add( $"Switch Loop detected, chunking score! Score {score}" );
        }

        if( weThreatenThem || weKOThem )
        {
            score += 35;
            score += Mathf.FloorToInt( 30f * theyAreForcedOut );
        }

        eval.Top2 = next;
        eval.Score = score;
        return eval;
    }

    private ActionEvaluation EvaluateOffensiveSwitchAction( ActionEvaluation eval )
    {
        int score = eval.Score;
        var top = eval.Top1;

        _ai.CurrentLog.Add( $"===[Evaluating Offensive Switch Action (Score: {score})]===" );

        float entryDamage = 1 - top.Attacker_EndOfTurnHP;

        if( entryDamage > 0.4f )
            score -= 45;

        _ai.CurrentLog.Add( $"Attacker's Entry Damage: {entryDamage}. Score: {score}" );

        if( top.Attacker_EndOfTurnHP <= 0f )
            score -= 150;
        else if( top.Attacker_EndOfTurnHP <= 0.2f )
            score -= 75;

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

        float theyAreForcedOut = _ai.UnitSim.PredictSwitchProbability( next.AttackerPTKO, next.OpponentPTKO, next.AttackerMovedFirst, top.Attacker_EndOfTurnHP, top.Opponent_EndOfTurnHP, next.Opponent.Expendability );
        score += Mathf.FloorToInt( 40f * theyAreForcedOut );

        float oppHPLossRaw = top.Opponent_EndOfTurnHP - next.Opponent_EndOfTurnHP;
        float oppHPLoss = NormalizeDamage( oppHPLossRaw, top.Opponent_EndOfTurnHP );
        if( oppHPLoss >= 0.4f )
            score += 30;
        else if( oppHPLoss >= 0.25f )
            score += 20;

        bool weDie = next.Attacker_DiesBeforeActing || next.Attacker_EndOfTurnHP <= 0f;
        if( weDie )
            score -= 100;

        float weAreForcedOut = _ai.UnitSim.PredictSwitchProbability( next.OpponentPTKO, next.AttackerPTKO, next.AttackerMovedFirst, top.Opponent_EndOfTurnHP, top.Attacker_EndOfTurnHP, next.Attacker.Expendability );
        score -= Mathf.FloorToInt( 75f * weAreForcedOut );

        float damageTakenRaw = top.Attacker.CurrentHPR - next.Attacker_EndOfTurnHP;
        float damageTaken = NormalizeDamage( damageTakenRaw, top.Attacker.CurrentHPR );
        bool noPressure = next.AttackerPTKO < PotentialToKO.TwoHKO;

        if( noPressure && ( damageTaken >= 0.4f || oppHPLoss < 0.2f && damageTaken >= 0.3f ) )
            score -= 50;

        eval.Top2 = next;
        eval.Score = score;
        return eval;
    }

    private ActionEvaluation EvaluateSetupAction( ActionEvaluation eval )
    {
        const int DIE_BEFORE_ACTING_PENALTY         = 150;
        const int SETUP_DIES_AFTER_ACTING_PENALTY   = 175;
        const int HEAVY_SETUP_DAMAGE_PENALTY        = 50;
        const int SETUP_THREATEN_KO_NEXT_TURN       = 30;
        const int OPPONENT_SWITCH_WEIGHT            = 50;
        const int WE_SWITCH_WEIGHT                  = 75;

        int score = eval.Score;
        var top = eval.Top1;

        _ai.CurrentLog.Add( $"===[Evaluating Setup Action (Score: {score})]===" );

        float weForceSwitch = eval.ExchangeEvaluation.OpponentSwitchProbability;
        score += Mathf.FloorToInt( OPPONENT_SWITCH_WEIGHT * weForceSwitch );

        //--We died before the setup completed
        if( top.Attacker_DiesBeforeActing )
        {
            score -= DIE_BEFORE_ACTING_PENALTY;
            // eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker dies before setup completes! Score: {score}" );
            // return eval;
        }

        //--We get KOd even if we setup
        if( top.Attacker_EndOfTurnHP <= 0 )
        {
            score -= SETUP_DIES_AFTER_ACTING_PENALTY;
            // eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker faints after setting up! Score: {score}" );
            // return eval;
        }

        //--Low HP after setting up
        if( top.Attacker_EndOfTurnHP <= 0.3f )
        {
            score -= HEAVY_SETUP_DAMAGE_PENALTY;
            _ai.CurrentLog.Add( $"Took big damage! Score: {score}" );
        }

        //--------------------------------
        //----------Look Ahead------------
        //--------------------------------

        var next = _ai.MoveCommand.GetMove_BestAttack( top.Attacker, top.Opponent, false, "Evaluate Setup Action (Look Ahead)" ).Top;

        if( next.Attacker_DiesBeforeActing )
        {
            score -= DIE_BEFORE_ACTING_PENALTY;
            // eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker dies before setup completes! Score: {score}" );
            // return eval;
        }

        if( next.Attacker_EndOfTurnHP <= 0f )
        {
            score -= SETUP_DIES_AFTER_ACTING_PENALTY;
            // eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker faints after setting up! Score: {score}" );
            // return eval;
        }

        if( next.Opponent_DiesBeforeActing )
        {
            score += SETUP_THREATEN_KO_NEXT_TURN + 15;
            _ai.CurrentLog.Add( $"Setup likely KO without taking damage next turn! Score: {score}" );
        }
        else if( next.Opponent_EndOfTurnHP <= 0f )
        {
            score += SETUP_THREATEN_KO_NEXT_TURN;
            _ai.CurrentLog.Add( $"Setup likely KO next turn! Score: {score}" );
        }

        if( next.OpponentPTKO < top.OpponentPTKO )
        {
            score += 15;
            _ai.CurrentLog.Add( $"Setup is more defensive next turn! Score: {score}" );
        }
        
        if( (int)next.OpponentPTKO - 1 < (int)top.OpponentPTKO )
        {
            score += 10;
            _ai.CurrentLog.Add( $"Setup walls hard next turn! Score: {score}" );
        }

        float damageTakenRaw = next.Attacker.CurrentHPR - next.Attacker_EndOfTurnHP;
        float damageTaken = NormalizeDamage( damageTakenRaw, next.Attacker.CurrentHPR );
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
        bool movesFirst = next.AttackerMovedFirst;

        float weForceSwitchNextTurnProbability = _ai.UnitSim.PredictSwitchProbability( next.AttackerPTKO, next.OpponentPTKO, movesFirst, next.Attacker.CurrentHPR, next.Opponent.CurrentHPR, next.Opponent.Expendability, true, $"{next.Opponent.Name} (Setup Look Ahead)" );
        float theyForceUsToSwitchNextTurnProbability = _ai.UnitSim.PredictSwitchProbability( next.OpponentPTKO, next.AttackerPTKO, movesFirst, next.Opponent.CurrentHPR, next.Attacker.CurrentHPR, next.Attacker.Expendability, true, $"{next.Attacker.Name} (Setup Look Ahead)" );

        float dangerWeight =
            next.OpponentPTKO >= PotentialToKO.OHKO ? 1.5f :
            next.OpponentPTKO >= PotentialToKO.Dangerous ? 1.25f :
            next.OpponentPTKO >= PotentialToKO.Risky ? 1f :
            next.OpponentPTKO >= PotentialToKO.TwoHKO ? 0.5f : 0.25f;

        float penalty = WE_SWITCH_WEIGHT * dangerWeight;

        score += Mathf.FloorToInt( OPPONENT_SWITCH_WEIGHT * weForceSwitchNextTurnProbability );
        score -= Mathf.FloorToInt( ( 1f - theyForceUsToSwitchNextTurnProbability ) * penalty );

        var oppTeam = _ai.GetRemainingOpposingPokemon( next.Attacker.PID );
        int fasterBonus = 0;
        bool weKO = next.Opponent_DiesBeforeActing || next.Opponent_EndOfTurnHP <= 0f;
        bool weForceSwitchNextTurn = weForceSwitchNextTurnProbability >= 0.7f;
        bool sweepBeginning = weKO || weForceSwitchNextTurn;

        if( sweepBeginning )
        {
            foreach( var opp in oppTeam )
            {
                int oppSpeed = _ai.GetUnitContextualSpeed( opp );

                if( next.Attacker.Speed > oppSpeed )
                    fasterBonus += 5;
            }

            score += fasterBonus;
            _ai.CurrentLog.Add( $"Outspeeds {fasterBonus / 5} opposing Pokémon after setup! {score}" );
        }

        eval.Top2 = next;
        eval.Top2.AttackerHasSweepHorizon = sweepBeginning;
        eval.Score = score;
        return eval;
    }

    private ActionEvaluation EvaluateOffensiveStatusAction( ActionEvaluation eval )
    {
        int score = eval.Score;
        var top = eval.Top1;

        _ai.CurrentLog.Add( $"===[Evaluating Offensive Status Action (Score: {score})]===" );

        if( top.Attacker_DiesBeforeActing )
        {
            score -= 120;
            // eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker Dies Before Acting! Score: {score}" );
            // return eval;
        }

        if( top.Attacker_EndOfTurnHP <= 0f )
        {
            score -= 90;
            // eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker Dies! Score: {score}" );
            // return eval;
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

        bool weNowMoveFirst = next.AttackerMovedFirst;
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
            // eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker dies before acting! Score: {score}" );
            // return eval;
        }

        if( next.Attacker_EndOfTurnHP <= 0f )
        {
            score -= 50;
            // eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker dies! Score: {score}" );
            // return eval;
        }

        float weForceSwitchNextTurnProb = _ai.UnitSim.PredictSwitchProbability( next.AttackerPTKO, next.OpponentPTKO, next.AttackerMovedFirst, next.Attacker.BeginningHPR, next.Opponent.BeginningHPR, top.Opponent.Expendability );
        score += Mathf.FloorToInt( 50f * weForceSwitchNextTurnProb );
        _ai.CurrentLog.Add( $"We force a switch next turn probability: {weForceSwitchNextTurnProb} * 50f. Score: {score}" );

        float damageTakenRaw = next.Attacker.CurrentHPR - next.Attacker_EndOfTurnHP;
        float damageTaken = NormalizeDamage( damageTakenRaw, next.Attacker.CurrentHPR );
        if( damageTaken <= 0.2f )
            score += 10;
        else if( damageTaken >= 0.4f )
            score -= 15;

        _ai.CurrentLog.Add( $"Damage Taken: {damageTaken}. Score: {score}" );

        Dictionary<CourtConditionID, int> courtConditions = new();
        if( top.Opponent.CourtLocation == CourtLocation.TopCourt )
            courtConditions = top.Field.TopCourtConditions;
        else if( top.Opponent.CourtLocation == CourtLocation.BottomCourt )
            courtConditions = top.Field.BottomCourtConditions;

        var oppCourtConditions = courtConditions;
        bool hasHazards = oppCourtConditions.Count > 0;

        _ai.CurrentLog.Add( $"Opposing Court's hazard count: {oppCourtConditions.Count}" );

        if( hasHazards )
        {
            float hazardDamage = 0f;

            foreach( var condition in oppCourtConditions )
                hazardDamage += _ai.Get_EntryHazardDamage( top.Opponent, condition.Key );

            int hazardScore = Mathf.FloorToInt( hazardDamage * 120f );
            score += Mathf.FloorToInt( 25f * weForceSwitchNextTurnProb * hazardScore );

            if( !top.OpponentCanAct || weNowMoveFirst )
                score += 25;

            _ai.CurrentLog.Add( $"We have hazard pressure! Hazard Damage: {hazardDamage}, Hazard Score: {hazardScore} Score: {score}" );
        }

        eval.Top2 = next;
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
                    score += Mathf.FloorToInt( ( 1 - sac.Top1.Opponent_EndOfTurnHP ) * 30 );
                break;

            case ActionType.DefensiveSwitch:
                    if( sac.Top1.Attacker_EndOfTurnHP > 0.3f )
                        score += 20;
                break;

            case ActionType.OffensiveSwitch:
                    if( sac.Top1.AttackerMovedFirst && sac.Top1.AttackerPTKO >= PotentialToKO.Risky )
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

        bool weDieThisTurn = eval.Top1.Attacker_EndOfTurnHP <= 0 || eval.Top1.Attacker_DiesBeforeActing;

        //--Don't want to bias toward accidental survival. I guess.
        if( !weDieThisTurn )
        {
            score -= 25;
        }

        //--Get Next round Pokemon
        BattleAI_PokemonAdapter revengeCandidate = null;
        if( eval.Top1.Attacker_DiesBeforeActing || eval.Top1.Attacker_EndOfTurnHP <= 0 )
        {
            var switchCandidate = _ai.SwitchCommand.GetSwitch_Revenge( _ai.TheirBattleAIUnits ).Pokemon;
            if( switchCandidate != null )
                revengeCandidate = _ai.GetPokemonAs_Adapter( switchCandidate );
        }
        else if( eval.Top1.AttackerPTKO <= PotentialToKO.Safe && eval.Top1.OpponentPTKO >= PotentialToKO.TwoHKO )
        {
            var switchCandidate = _ai.SwitchCommand.GetSwitch_Revenge( _ai.TheirBattleAIUnits ).Pokemon;
            if( switchCandidate != null )
                revengeCandidate = _ai.GetPokemonAs_Adapter( switchCandidate );
        }

        IBattleAIUnit nextPokemon;
        if( revengeCandidate != null )
            nextPokemon = revengeCandidate;
        else
            nextPokemon = eval.Top1.Attacker;

        //--Look ahead at the next round
        var followUp = _ai.MoveCommand.GetMove_BestAttack( nextPokemon, eval.Top1.Opponent ).Top;

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
        float oppHP_before = eval.Top1.Opponent_EndOfTurnHP;
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
        float weForceSwitchNextTurnProb = _ai.UnitSim.PredictSwitchProbability( followUp.AttackerPTKO, followUp.OpponentPTKO, followUp.AttackerMovedFirst, nextPokemon.CurrentHPR, eval.Top1.Opponent_EndOfTurnHP, followUp.Opponent.Expendability );
        score += Mathf.FloorToInt( 30f * weForceSwitchNextTurnProb );
        _ai.CurrentLog.Add( $"Opponent's switch probability {weForceSwitchNextTurnProb} * 30f. Score: {score}" );

        //--Dead end penalty to punish bad sacrifice lines
        if( ( followUp.Attacker_EndOfTurnHP <= 0 || followUp.Attacker_DiesBeforeActing ) && followUp.Opponent_EndOfTurnHP >= 0.5f )
        {
            score -= 100;
            _ai.CurrentLog.Add( $"Dead end detected for {eval.Type} decision line! Penalizing... Score: {score}" );
        }

        //--Piece Value death penalty
        if( _ai.OurTeamPieceValues.TryGetValue( eval.Top1.Attacker.Pokemon, out var pieceValue ) )
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

        if( nextPokemon != null && _ai.OurTeamPieceValues.TryGetValue( nextPokemon.Pokemon, out var nextValue ) )
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

        eval.Top1 = followUp;
        eval.Score = score;
        return eval;
    }

    public int EvaluateThreatResponse( ActionEvaluation action, ThreatProfile threat, DoomedOutcome doomed, BoardContext bc, SurvivalClass sc )
    {
        int score = 0;
        float sackScalar = 0.7f;
        var expendability = bc.MyExpendability;
        float sackModifier = ( 1 - expendability * sackScalar );

        var top1 = action.Top1;
        var top2 = action.Top2;

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===================================================" );
        _ai.CurrentLog.Add( $"=====[Evaluating Threat Response for {action.Type}]=====" );
        _ai.CurrentLog.Add( $"===================================================" );
        _ai.CurrentLog.Add( $"" );

        _ai.CurrentLog.Add( $"Threat Type is {threat.Type}." );

        float damageDealt = top1.Opponent.BeginningHPR - top1.Opponent_EndOfTurnHP;
        _ai.CurrentLog.Add( $"Damage Dealt to threat: {damageDealt}. Score: {score}" );

        switch( threat.Type )
        {
            case ThreatType.BurstDamage:

                if( top1.Attacker_EndOfTurnHP <= 0f )
                {
                    score -= Mathf.RoundToInt( 70 * sackModifier );
                    _ai.CurrentLog.Add( $"Attacker doesn't survive burst damage threat from opponent. Penalizing. Score: {score}" );
                }
                else
                {
                    if( damageDealt >= 0.33f )
                    {
                        score += 25;
                        _ai.CurrentLog.Add( $"Attacker survives the round and does 33% damage or more. Score: {score}" );
                    }
                }

                if( top1.Attacker_EndOfTurnHP > 0 && ( top1.AttackerPTKO >= PotentialToKO.Risky && top1.AttackerMovedFirst || top1.AttackerPTKO >= PotentialToKO.Dangerous ) )
                {
                    score += 40;
                    _ai.CurrentLog.Add( $"Attacker survives and threatens big damage on burst damage threat opponent. Score: {score}" );
                }

                if( top1.Opponent_EndOfTurnHP <= 0f )
                {
                    score += 50;
                    _ai.CurrentLog.Add( $"Opponent is KO'd this round! Score: {score}" );
                }

                if( top1.OpponentPTKO >= PotentialToKO.Risky && top2.OpponentPTKO < PotentialToKO.Risky )
                {
                    score += 25;
                    _ai.CurrentLog.Add( $"Opponent's PTKO {top1.OpponentPTKO} during this round is lessened to {top2.OpponentPTKO} next round! Score: {score}" );
                }

                if( threat.ThreatensImmediateKO && action.Type == ActionType.DefensiveSwitch )
                {
                    score += 25;
                    _ai.CurrentLog.Add( $"Opponent threatens an immediate KO, pushing defensive switching. Score: {score}" );
                }

                if( action.Type == ActionType.DefensiveSwitch && action.Top2.Attacker_EndOfTurnHP > 0 )
                {
                    score += 40;
                    _ai.CurrentLog.Add( $"Defensive switch candidate survives the burst damage on incoming and next turn! Score: {score}" );
                }

                if( action.Type == ActionType.OffensiveSwitch )
                {
                    score += 15;
                    _ai.CurrentLog.Add( $"Flat reward for an offensive switch that may neutralize the current burst damage threat. Score: {score}" );

                    if( action.Top2.Attacker_EndOfTurnHP > 0 && action.Top2.AttackerPTKO >= PotentialToKO.Dangerous )
                    {
                        score += 25;
                        _ai.CurrentLog.Add( $"Offensive switch candidate survives next round and threatens big damage! Score: {score}" );
                    }
                }

                if( threat.ConstraintPressure >= 2f && action.Type == ActionType.DefensiveSwitch )
                {
                    score += 15;
                    _ai.CurrentLog.Add( $"Constraint Pressure: {threat.ConstraintPressure}, rewarding defensive switch lightly. Score: {score}" );
                }

            break;

            case ThreatType.Pressure:

                score += Mathf.FloorToInt( damageDealt * 60 );
                _ai.CurrentLog.Add( $"Flat damage dealt reward for general pressure, * 60( {damageDealt * 60}). Score: {score}" );

                if( top2.AttackerPTKO >= PotentialToKO.Risky )
                {
                    score += 15;
                    _ai.CurrentLog.Add( $"Attacker threatens good damage next round. Score: {score}" );
                }

                if( action.Type == ActionType.OffensiveSwitch && top2.AttackerPTKO >= PotentialToKO.Dangerous && top2.Attacker_EndOfTurnHP > 0 )
                {
                    score += 25;
                    _ai.CurrentLog.Add( $"Offensive switch candidate survives next round and threatens big damage. Score: {score}" );
                }

            break;

            case ThreatType.Setup:

                score += Mathf.FloorToInt( damageDealt * 75 );
                _ai.CurrentLog.Add( $"Flat damage dealt reward on a threat that might setup, * 75( {damageDealt * 75}). Score: {score}" );

                if( top1.AttackerPTKO >= PotentialToKO.Risky )
                {
                    score += 20;
                    _ai.CurrentLog.Add( $"We threaten decent damage to the setup mon. Score: {score}" );
                }

                if( top1.AttackerMovedFirst )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"We're faster than the setup threat. Score: {score}" );
                }

                if( top1.Attacker_EndOfTurnHP > 0 && top2.Opponent_EndOfTurnHP <= 0 )
                {
                    score += 30;
                    _ai.CurrentLog.Add( $"We survive this round and KO the setup threat next round. Score: {score}" );

                    if( top2.AttackerMovedFirst )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"We're faster than the setup threat next round. Score: {score}" );
                    }
                }

            break;

            case ThreatType.Tank:

                if( damageDealt < 0.2f )
                {
                    score -= 40;
                    _ai.CurrentLog.Add( $"We don't do meaningful chip to the tank threat. Penalizing. Score: {score}" );
                }

                if( damageDealt >= 0.33f )
                {
                    score += 40;
                    _ai.CurrentLog.Add( $"We do 33% damage or more to a tank. Score: {score}" );
                }
                else if( damageDealt >= 0.2f )
                {
                    score += 20;
                    _ai.CurrentLog.Add( $"We do 20% or more to a tank. Score: {score}" );
                }

                if( top2.AttackerPTKO >= PotentialToKO.Risky || top2.AttackerPTKO > top1.AttackerPTKO  )
                {
                    score += 25; //--Future breaking potential
                    _ai.CurrentLog.Add( $"We threaten good damage next round, or we improve our PTKO from current round into next round. This is good break potential. Score: {score}" );
                }

                if( action.Type == ActionType.Setup )
                {
                    if( action.Top2.Attacker_EndOfTurnHP > 0 && action.Top2.AttackerPTKO >= PotentialToKO.Dangerous )
                    {
                        score += 50;
                        _ai.CurrentLog.Add( $"Attacker survives setting up on the opposing tank this round and threatens big damage next round. Score: {score}" );
                    }
                    else if( action.Top2.Attacker_EndOfTurnHP > 0 )
                    {
                        score += 25; //--Setup is good vs tanks
                        _ai.CurrentLog.Add( $"Attacker survives setting up on the opposing tank this round and survives next round. Score: {score}" );
                    }
                    else
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Setting up on tanks is usually good. We may not survive or threaten significant damage, but still giving a small reward for tje scenario. Score: {score}" );
                    }
                }

                if( action.Type == ActionType.OffensiveStatus )
                {
                    if( !top1.OpponentCanAct || !top2.OpponentCanAct && top2.Attacker_EndOfTurnHP > 0 )
                    {
                        score += 25;
                        _ai.CurrentLog.Add( $"We prevent the tank from acting this round, or next round and we survive next round. Rewarding. Score: {score}" );
                    }

                    if( top1.Opponent.SevereStatus == SevereConditionID.None && top2.Opponent.SevereStatus != SevereConditionID.None )
                    {
                        score += 25;
                        _ai.CurrentLog.Add( $"We apply a status effect to the tank, likely crippling it or allowing for guaranteed residual chip damage. Score: {score}" );
                    }

                    if( bc.BattlefieldState.EntryHazardsOn_TheirSide <= 0 && _ai.UnitSim.MoveIsEntryHazard( action.MovePayload ) && top1.Attacker_EndOfTurnHP > 0f )
                    {
                        score += 25;
                        _ai.CurrentLog.Add( $"We don't have hazards setup yet, and we survive the turn. We should take advantage of the tank and seize some field control. Score: {score}" );
                    }
                }

                if( action.Type == ActionType.DefensiveSwitch )
                {
                    score -= 30;
                    _ai.CurrentLog.Add( $"Defensively switching against the tank is likely not the best idea. Score: {score}" );
                }

                if( action.Type == ActionType.OffensiveSwitch )
                {
                    score += 20;
                    _ai.CurrentLog.Add( $"Offensively switching against a tank is likely a safe tempo grab. Score: {score}" );

                    if( action.Top2.Attacker_EndOfTurnHP > 0 && action.Top2.AttackerPTKO >= PotentialToKO.Dangerous )
                    {
                        score += 25;
                        _ai.CurrentLog.Add( $"We survive switching in, survive next turn, and threaten big damage next turn. Score: {score}" );
                    }
                }

                if( threat.ConstraintPressure >= 2f )
                {
                    score -= 20;
                    _ai.CurrentLog.Add( $"Constraint Pressure {threat.ConstraintPressure} > 2. Score: {score}" );
                }

            break;

            case ThreatType.Utility:

                if( action.Type == ActionType.Setup )
                {
                    score -= 20;
                    _ai.CurrentLog.Add( $"Setting up against a utility mon could cripple us. Score: {score}" );
                }

                if( action.Type == ActionType.OffensiveStatus && !_ai.UnitSim.MoveIsEntryHazard( action.MovePayload ) )
                {
                    score += 25;
                    _ai.CurrentLog.Add( $"We could potentially cripple the utility threat. Score: {score}" );
                }

                if( damageDealt >= 0.5f )
                {
                    score += 30;
                    _ai.CurrentLog.Add( $"We deal 50% or more damage to a utility threat. Score: {score}" );
                }
                else if( damageDealt >= 0.3f )
                {
                    score += 20;
                    _ai.CurrentLog.Add( $"We deal 30% or more damage to a utility threat. Score: {score}" );
                }

                if( action.Type == ActionType.DefensiveSwitch )
                {
                    score -= 30;
                    _ai.CurrentLog.Add( $"Defensively switching might result in a crippled tank. Score: {score}" );
                }

                if( action.Type == ActionType.OffensiveSwitch )
                {
                    score += 20;
                    _ai.CurrentLog.Add( $"Offensively switching might cripple us, but it may also give us a tempo grab. Score: {score}" );

                    if( action.Top2.Attacker_EndOfTurnHP > 0 && action.Top2.AttackerPTKO >= PotentialToKO.Dangerous )
                    {
                        score += 25;
                        _ai.CurrentLog.Add( $"We survive next round and threaten big damage. Score: {score}" );
                    }
                }

                if( threat.ConstraintPressure >= 2f )
                {
                    score -= 30;
                    _ai.CurrentLog.Add( $"Constraint Pressure: {threat.ConstraintPressure} > 2. Score: {score}" );
                }

            break;
        }

        //--------------------
        //--Universal Scores--
        //--------------------

        if( top1.Opponent_DiesBeforeActing )
        {
            score += 50; //--Outright removes threat
            _ai.CurrentLog.Add( $"Current simulation detects we out-right remove the threat. Score: {score}" );
        }

        if( action.Top2.AttackerMovedFirst && threat.OutspeedsCurrent )
        {
            score += 40;
            _ai.CurrentLog.Add( $"This action changes speed dynamic in our favor. Score: {score}" );
        }

        float theySwitchProbability = _ai.UnitSim.PredictSwitchProbability( top1.AttackerPTKO, top1.OpponentPTKO, top1.AttackerMovedFirst, top1.Attacker.BeginningHPR, top1.Opponent.BeginningHPR, top1.Opponent.Expendability );
        score += Mathf.FloorToInt( 50f * theySwitchProbability );
        _ai.CurrentLog.Add( $"Switch Probability: {theySwitchProbability}. Score: {score}" );

        bool canKillNow = top1.AttackerPTKO >= PotentialToKO.Dangerous && top1.AttackerMovedFirst;
        if( canKillNow && action.Type != ActionType.Attack )
        {
            score -= 75;
            _ai.CurrentLog.Add( $"Current action is: {action.Type}. The attack line very likely to get an immediate KO. Penalizing. Score: {score}" );
        }

        float urgencyMultiplier = 1f;
        if( threat.Urgency >= ThreatUrgency.High )
        {
            if( top1.Opponent_EndOfTurnHP <= 0f )
            {
                score += 50;
                _ai.CurrentLog.Add( $"Threat Urgency is: {threat.Urgency}. Opponent ends round at 0 hp. Rewarding. Score: {score}" );
            }

            if( top1.Attacker_EndOfTurnHP <= 0f )
            {
                score -= Mathf.RoundToInt( 50 * sackModifier );
                _ai.CurrentLog.Add( $"Threat Urgency is: {threat.Urgency}. We end the round at 0 hp. Penalizing. Score: {score}" );
            }
        }

        switch( threat.Urgency )
        {
            case ThreatUrgency.Medium:      urgencyMultiplier = 1.1f; break;
            case ThreatUrgency.High:        urgencyMultiplier = 1.25f; break;
            case ThreatUrgency.Critical:    urgencyMultiplier = 1.5f; break;
        }

        score = Mathf.FloorToInt( score * urgencyMultiplier );
        _ai.CurrentLog.Add( $"Threat Urgency Multiplier: {urgencyMultiplier}. Score: {score}" );

        //--Doomed potential
        //--Sweep check
        if( doomed.SweepIncoming && ( top1.Opponent_EndOfTurnHP < 0.55f || top2.Opponent_EndOfTurnHP <= 0f ) )
        {
            score += 25;
            _ai.CurrentLog.Add( $"Doomed Turn Sweep Detected. This action threatens to shut it down! Score: {score}" );
        }

        if( doomed.NoTempoRecoveryLine && top2.AttackerPTKO >= PotentialToKO.Risky && top2.Attacker_EndOfTurnHP > 0 )
        {
            score += 20;
            _ai.CurrentLog.Add( $"Doomed turn No Tempo Recovery detected. This Action appears to break opponent tempo! Score: {score}" );
        }

        //--Strategic Sacrifice to regain control\
        if( top1.Attacker_EndOfTurnHP <= 0f && ( top2.AttackerPTKO >= PotentialToKO.Risky && top2.AttackerMovedFirst || top2.AttackerPTKO >= PotentialToKO.Dangerous && top2.Attacker_EndOfTurnHP > 0f ) )
        {
            score += Mathf.RoundToInt( 50 * sackModifier );
            _ai.CurrentLog.Add( $"Strategic sacrifice here results in revenge/tempo next turn! Score: {score}" );
        }

        _ai.CurrentLog.Add( $"{action.Type}'s Final Threat Response Score: {score}" );
        _ai.CurrentLog.Add( $"===================================================" );
        _ai.CurrentLog.Add( $"" );

        return score;
    }

    public int EvaluateBattlefieldState( ActionEvaluation action, BoardContext boardContext )
    {
        return action.Type switch
        {
            ActionType.Attack               => EvaluateBattlefieldFor_Attack( action, boardContext ),
            ActionType.DefensiveSwitch      => EvaluateBattlefieldFor_DefensiveSwitch( action, boardContext ),
            ActionType.OffensiveSwitch      => EvaluateBattlefieldFor_OffensiveSwitch( action, boardContext ),
            ActionType.Setup                => EvaluateBattlefieldFor_Setup( action, boardContext ),
            ActionType.OffensiveStatus      => EvaluateBattlefieldFor_OffensiveStatus( action, boardContext ),
            _ => 0,
        };
    }

    private int EvaluateBattlefieldFor_Attack( ActionEvaluation action, BoardContext boardContext )
    {
        int score = 0;
        var bfs = boardContext.BattlefieldState;
        var top1 = action.Top1;

        var attackerMon = top1.Attacker.Pokemon;
        var opponentMon = top1.Opponent.Pokemon;

        bool isMidGame = bfs.Round > 6 && bfs.Round < 16;
        bool isLateGame = bfs.Round > 15;

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"============================================" );
        _ai.CurrentLog.Add( $"===[Evaluating Battlefield for Attacking]===" );
        _ai.CurrentLog.Add( $"============================================" );
        _ai.CurrentLog.Add( $"" );

        if( bfs.IsEarlyGame && top1.AttackerPTKO != PotentialToKO.OHKO )
        {
            if( top1.AttackerPTKO <= PotentialToKO.Dangerous )
                score -= 15;
            else if( top1.AttackerPTKO <= PotentialToKO.TwoHKO )
                score -= 25;

            _ai.CurrentLog.Add( $"It's still early game and we don't have a guaranteed OHKO! Penalizing. Score: {score}" );
        }

        int hazardDelta = bfs.EntryHazardsOn_TheirSide - bfs.EntryHazardsOn_MySide;
        if( bfs.IsEarlyGame && hazardDelta <= 0 )
        {
            score -= 20;
            _ai.CurrentLog.Add( $"It's still early game and we are behind on hazard pressure! Penalizing. Score: {score}" );
        }

        if( isLateGame )
        {
            score += 10;
            _ai.CurrentLog.Add( $"Late Game Attack bonus. Score: {score}" );
        }
        
        int weatherContext = _ai.UnitSim.Get_WeatherContextScore( attackerMon );
        int terrainContext = _ai.UnitSim.Get_TerrainContextScore( attackerMon );
        int trickRoomContext = _ai.UnitSim.Get_TrickRoomContextScore( attackerMon );
        int contextScore = weatherContext + terrainContext + trickRoomContext;

        score += contextScore;
        _ai.CurrentLog.Add( $"[Attacker's Battlefield Context] Weather: {weatherContext}, Terrian: {terrainContext}, TRoom: {trickRoomContext}. Total Context Score: {contextScore}. Score: {score}" );

        int oppWeatherContext = _ai.UnitSim.Get_WeatherContextScore( opponentMon );
        int oppTerrainContext = _ai.UnitSim.Get_TerrainContextScore( opponentMon );
        int oppTrickRoomContext = _ai.UnitSim.Get_TrickRoomContextScore( opponentMon );
        int oppContextScore = oppWeatherContext + oppTerrainContext + oppTrickRoomContext;

        score -= oppContextScore;
        _ai.CurrentLog.Add( $"[Opponent's Battlefield Context] Weather: {oppWeatherContext}, Terrian: {oppTerrainContext}, TRoom: {oppTrickRoomContext}. Total Context Score: {oppContextScore}. Score: {score}" );

        if( bfs.WeHave_Tailwind )
        {
            score += bfs.OurTailwindDuration * 2;
            _ai.CurrentLog.Add( $"We're benefiting from tailwind! Score: {score}" );

            if( bfs.OurTailwindDuration == 1 )
            {
                score += 5;
                _ai.CurrentLog.Add( $"Last round of tailwind! Extra bump for attacking with speed advantage! Score: {score}" );
            }
        }

        if( bfs.TheyHave_Tailwind )
        {
            score -= bfs.TheirTailwindDuration * 2;
            _ai.CurrentLog.Add( $"They have speed control. Should we try to disrupt, match, or stall out? Score: {score}" );

            if( bfs.TheirTailwindDuration == 1 )
            {
                score -= 5;
                _ai.CurrentLog.Add( $"Opponent's last round of tailwind! A small penalty to encourage stalling it out for a potential advantage next round. Score: {score}" );
            }
        }

        if( bfs.TrickRoomActive )
        {
            if( bfs.WeHave_TrickRoomAdvantage )
            {
                score += bfs.TrickRoomDuration * 2;
                _ai.CurrentLog.Add( $"We're benefiting from Trick Room! Score: {score}" );
            }
            else if( bfs.TheyHave_TrickRoomAdvantage )
            {
                score -= bfs.TrickRoomDuration * 2;
                _ai.CurrentLog.Add( $"They have advantage in Trick Room. Should we try to disrupt or stall out? Score: {score}" );
            }
        }

        if( bfs.TheyHave_Reflect && action.MovePayload.MoveSO.MoveCategory == MoveCategory.Physical )
        {
            score -= bfs.TheirReflectDuration * 3;
            _ai.CurrentLog.Add( $"The opponent's reflect weakens our attack. Score: {score}" );
        }

        if( bfs.TheyHave_LightScreen && action.MovePayload.MoveSO.MoveCategory == MoveCategory.Special )
        {
            score -= bfs.TheirLightScreenDuration * 3;
            _ai.CurrentLog.Add( $"The opponent's light screen weakens our attack. Score: {score}" );
        }

        if( bfs.TheyHave_AuroraVeil )
        {
            score -= bfs.TheirAuroraVeilDuration * 4;
            _ai.CurrentLog.Add( $"The opponent's aurora veil weakens our attack. Score: {score}" );
        }

        if( bfs.WeHave_Reflect )
        {
            score += bfs.OurReflectDuration * 2;
            _ai.CurrentLog.Add( $"We're likely protected via Reflect! Let's take advantage and attack! Score: {score}" );
        }

        if( bfs.WeHave_LightScreen )
        {
            score += bfs.OurLightScreenDuration * 2;
            _ai.CurrentLog.Add( $"We're likely protected via Light Screen! Let's take advantage and attack! Score: {score}" );
        }

        if( bfs.WeHave_AuroraVeil )
        {
            score += bfs.OurAuroraVeilDuration * 3;
            _ai.CurrentLog.Add( $"We're likely protected via Aurora Veil! Let's take advantage and attack! Score: {score}" );
        }

        score += Mathf.Clamp( bfs.FieldControlDelta, -5, 5 );
        _ai.CurrentLog.Add( $"Adding field control delta (clamped). Delta: {bfs.FieldControlDelta}. Score: {score}" );

        if( bfs.TheyHave_FieldControl && contextScore < 5 )
        {
            score -= 5;
            _ai.CurrentLog.Add( $"They're in control of the field and we have minimal benefit from field effects. Minor penalty to encourage disruption. Score: {score}" );
        }

        _ai.CurrentLog.Add( $"Final Battlefield State for Attacking Score: {score}" );
        _ai.CurrentLog.Add( $"" );

        return score;
    }

    private int EvaluateBattlefieldFor_DefensiveSwitch( ActionEvaluation action, BoardContext boardContext )
    {
        int score = 0;

        var bfs = boardContext.BattlefieldState;
        var top1 = action.Top1;

        var attackerMon = top1.Attacker.Pokemon; //--This unit should actually be the switch candidate, but i will get the candidate directly just in case. I will confirm this 100% soon. --04/22/26
        var opponentMon = top1.Opponent.Pokemon;
        var switchCandidate = action.SwitchPayload;

        bool isMidGame = bfs.Round > 6 && bfs.Round < 16;
        bool isLateGame = bfs.Round > 15;

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"======================================================" );
        _ai.CurrentLog.Add( $"===[Evaluating Battlefield for Defensive Switching]===" );
        _ai.CurrentLog.Add( $"======================================================" );
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"TOP1 Attacker: {top1.Attacker.Name}" );
        _ai.CurrentLog.Add( $"Switch Candidate: {switchCandidate.NickName}" );

        if( bfs.Round <= 1 && top1.OpponentPTKO != PotentialToKO.OHKO && !top1.AttackerMovedFirst )
        {
            score -= 30;
            _ai.CurrentLog.Add( $"It's first round and we're not immediately threatened with death. Can we do something else other than switch? Score: {score}" );
        }
        else if( bfs.IsEarlyGame && top1.OpponentPTKO < PotentialToKO.Dangerous )
        {
            score -= 15;
            _ai.CurrentLog.Add( $"It's early game and we're not in immediate danger. Should we try something else? Score: {score}" );
        }

        if( bfs.EntryHazardsOn_MySide > 0 )
        {
            if( bfs.IsEarlyGame || isMidGame )
            {
                score -= 20;
                _ai.CurrentLog.Add( $"Entry hazards detected on our side. Let's make sure it's worth switching into them. Score: {score}" );
            }
            else if( isLateGame )
            {
                score -= 5;
                _ai.CurrentLog.Add( $"Entry hazards detected on our side, but it's late game. We can probably tolerate switching into them. Score: {score}" );
            }

            if( bfs.EntryHazardsOn_MySide > 1 )
                score -= 5;
        }

        int weatherContext = _ai.UnitSim.Get_WeatherContextScore( switchCandidate );
        int terrainContext = _ai.UnitSim.Get_TerrainContextScore( switchCandidate );
        int trickRoomContext = _ai.UnitSim.Get_TrickRoomContextScore( switchCandidate );
        int contextScore = weatherContext + terrainContext + trickRoomContext;

        score += contextScore;
        _ai.CurrentLog.Add( $"[Switch Candidate's Battlefield Context] Weather: {weatherContext}, Terrian: {terrainContext}, TRoom: {trickRoomContext}. Total Context Score: {contextScore}. Score: {score}" );

        bool switchSetsWeather = _ai.UnitSim.PokemonHasWeatherSetter_Ability( switchCandidate );
        bool switchChangesWeather = false;
        WeatherConditionID candidatesWeather = WeatherConditionID.None;

        if( switchSetsWeather )
        {
            switch( switchCandidate.AbilityID )
            {
                case AbilityID.Drought: candidatesWeather = WeatherConditionID.SUNNY; break;
                case AbilityID.Drizzle: candidatesWeather = WeatherConditionID.RAIN; break;
                case AbilityID.Sandstream: candidatesWeather = WeatherConditionID.SANDSTORM; break;
                case AbilityID.SnowWarning: candidatesWeather = WeatherConditionID.SNOW; break;
            }

            if( candidatesWeather != WeatherConditionID.None && candidatesWeather != bfs.Weather )
                switchChangesWeather = true;
        }

        if( !bfs.WeHave_WeatherControl && switchChangesWeather )
        {
            int myNewWeatherContext = _ai.UnitSim.Get_WeatherContextScore( switchCandidate, candidatesWeather );
            int theirNewWeatherContext = _ai.UnitSim.Get_WeatherContextScore( opponentMon, candidatesWeather );

            if( myNewWeatherContext > theirNewWeatherContext )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Switch candidate can swing the weather in our favor! Score: {score}" );
            }
            else
            {
                score -= 5;
                _ai.CurrentLog.Add( $"Switch candidate changes weather in our opponent's favor! Penalizing slightly. Score: {score}" );
            }
        }

        if( bfs.WeHave_Tailwind && bfs.OurTailwindDuration >= 2 )
        {
            score += 5;
            _ai.CurrentLog.Add( $"We may be able to take advantage of our tailwind. Score {score}" );
        }

        var oppMoveCat = top1.Opponent.MTR.Move.MoveSO.MoveCategory;
        if( bfs.WeHave_Reflect && bfs.OurReflectDuration >= 2 && oppMoveCat == MoveCategory.Physical )
        {
            score += 5;
            _ai.CurrentLog.Add( $"We're protected on incoming by Reflect. Score {score}" );
        }

        if( bfs.WeHave_LightScreen && bfs.OurLightScreenDuration >= 2 && oppMoveCat == MoveCategory.Special )
        {
            score += 5;
            _ai.CurrentLog.Add( $"We're protected on incoming by Light Screen. Score {score}" );
        }

        if( bfs.WeHave_AuroraVeil && bfs.OurAuroraVeilDuration >= 2 )
        {
            score += 10;
            _ai.CurrentLog.Add( $"We're protected on incoming by Aurora Veil. Score {score}" );
        }

        _ai.CurrentLog.Add( $"Final Battlefield State for Defensive Switching Score: {score}" );
        _ai.CurrentLog.Add( $"" );

        return score;
    }

    private int EvaluateBattlefieldFor_OffensiveSwitch( ActionEvaluation action, BoardContext boardContext )
    {
        int score = 0;

        var bfs = boardContext.BattlefieldState;
        var top1 = action.Top1;
        var top2 = action.Top2;

        bool isMidGame = bfs.Round > 6 && bfs.Round < 16;
        bool isLateGame = bfs.Round > 15;

        var attackerMon = top1.Attacker.Pokemon; //--This unit should actually be the switch candidate, but i will get the candidate directly just in case. I will confirm this 100% soon. --04/22/26
        var opponentMon = top1.Opponent.Pokemon;
        var switchCandidate = action.SwitchPayload;

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"======================================================" );
        _ai.CurrentLog.Add( $"===[Evaluating Battlefield for Offensive Switching]===" );
        _ai.CurrentLog.Add( $"======================================================" );
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"TOP1 Attacker: {top1.Attacker.Name}" );
        _ai.CurrentLog.Add( $"Switch Candidate: {switchCandidate.NickName}" );

        if( bfs.Round <= 1 && top1.OpponentPTKO != PotentialToKO.OHKO && !top1.AttackerMovedFirst )
        {
            score -= 10;
            _ai.CurrentLog.Add( $"It's first round and we're not immediately threatened with death. Can we do something else other than switch? Score: {score}" );
        }
        
        if( bfs.IsEarlyGame )
        {
            if( top2.AttackerPTKO < PotentialToKO.Dangerous)
            {
                score -= 15;
                _ai.CurrentLog.Add( $"It's early game and we're not threatening powerful offense next turn. Should we try something else? Score: {score}" );
            }
            else if( top2.AttackerPTKO >= PotentialToKO.Dangerous )
            {
                score +=5;
                _ai.CurrentLog.Add( $"It's early game and we threaten powerful offense next turn. Giving a small nudge for early-game tempo grab/battlefield control. Score: {score}" );
            }
        }

        if( isMidGame && top2.AttackerPTKO >= PotentialToKO.Dangerous  )
        {
            score += 15;
            _ai.CurrentLog.Add( $"It's mid game and we threaten powerful offense next turn. Giving a slight boost for mid game phase tempo grab. Score: {score}" );
        }

        if( isLateGame && top2.AttackerPTKO != PotentialToKO.OHKO )
        {
            score -= 5;
            _ai.CurrentLog.Add( $"It's late game and we don't threaten a KO next turn. Tiny tiny penalty. Score: {score}" );
        }

        if( bfs.EntryHazardsOn_MySide > 0 )
        {
            score -= 15;
            _ai.CurrentLog.Add( $"Entry hazards detected on our side. Let's make sure it's worth switching into them. Score: {score}" );

            if( bfs.EntryHazardsOn_MySide > 1 )
                score -= 5;
        }

        int weatherContext = _ai.UnitSim.Get_WeatherContextScore( switchCandidate );
        int terrainContext = _ai.UnitSim.Get_TerrainContextScore( switchCandidate );
        int trickRoomContext = _ai.UnitSim.Get_TrickRoomContextScore( switchCandidate );
        int contextScore = weatherContext + terrainContext + trickRoomContext;

        score += contextScore;
        _ai.CurrentLog.Add( $"[Switch Candidate's Battlefield Context] Weather: {weatherContext}, Terrian: {terrainContext}, TRoom: {trickRoomContext}. Total Context Score: {contextScore}. Score: {score}" );

        bool switchSetsWeather = _ai.UnitSim.PokemonHasWeatherSetter_Ability( switchCandidate );
        bool switchChangesWeather = false;
        WeatherConditionID candidatesWeather = WeatherConditionID.None;

        if( switchSetsWeather )
        {
            switch( switchCandidate.AbilityID )
            {
                case AbilityID.Drought: candidatesWeather = WeatherConditionID.SUNNY; break;
                case AbilityID.Drizzle: candidatesWeather = WeatherConditionID.RAIN; break;
                case AbilityID.Sandstream: candidatesWeather = WeatherConditionID.SANDSTORM; break;
                case AbilityID.SnowWarning: candidatesWeather = WeatherConditionID.SNOW; break;
            }

            if( candidatesWeather != WeatherConditionID.None && candidatesWeather != bfs.Weather )
                switchChangesWeather = true;
        }

        if( !bfs.WeHave_WeatherControl && switchChangesWeather )
        {
            int myNewWeatherContext = _ai.UnitSim.Get_WeatherContextScore( switchCandidate, candidatesWeather );
            int theirNewWeatherContext = _ai.UnitSim.Get_WeatherContextScore( opponentMon, candidatesWeather );

            if( myNewWeatherContext > theirNewWeatherContext )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Switch candidate can swing the weather in our favor! Score: {score}" );
            }
            else
            {
                score -= 5;
                _ai.CurrentLog.Add( $"Switch candidate changes weather in our opponent's favor! Penalizing slightly. Score: {score}" );
            }
        }

        if( bfs.WeHave_Tailwind && bfs.OurTailwindDuration >= 2 )
        {
            score += 5;
            _ai.CurrentLog.Add( $"We may be able to take advantage of our tailwind. Score {score}" );
        }

        if( bfs.TheyHave_Tailwind && bfs.TheirTailwindDuration == 1 )
        {
            score += 5;
            _ai.CurrentLog.Add( $"Opponent's last turn of tailwind. Perhaps we can stall it out and gain offense next turn. Score: {score}" );
        }

        var oppMoveCat = top1.Opponent.MTR.Move.MoveSO.MoveCategory;
        if( bfs.WeHave_Reflect && bfs.OurReflectDuration >= 2 && oppMoveCat == MoveCategory.Physical )
        {
            score += 2;
            _ai.CurrentLog.Add( $"We're protected on incoming by Reflect. Score {score}" );
        }

        if( bfs.WeHave_LightScreen && bfs.OurLightScreenDuration >= 2 && oppMoveCat == MoveCategory.Special )
        {
            score += 2;
            _ai.CurrentLog.Add( $"We're protected on incoming by Light Screen. Score {score}" );
        }

        if( bfs.WeHave_AuroraVeil && bfs.OurAuroraVeilDuration >= 2 )
        {
            score += 5;
            _ai.CurrentLog.Add( $"We're protected on incoming by Aurora Veil. Score {score}" );
        }

        _ai.CurrentLog.Add( $"Final Battlefield State for Offensive Switching Score: {score}" );
        _ai.CurrentLog.Add( $"" );

        return score;
    }

    private int EvaluateBattlefieldFor_Setup( ActionEvaluation action, BoardContext boardContext )
    {
        int score = 0;

        var bfs = boardContext.BattlefieldState;
        var top1 = action.Top1;
        var top2 = action.Top2;

        bool isMidGame = bfs.Round > 6 && bfs.Round < 16;
        bool isLateGame = bfs.Round > 15;

        var attackerMon = top1.Attacker.Pokemon;
        var opponentMon = top1.Opponent.Pokemon;

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"======================================================" );
        _ai.CurrentLog.Add( $"===[Evaluating Battlefield for Setup]===" );
        _ai.CurrentLog.Add( $"======================================================" );
        _ai.CurrentLog.Add( $"" );

        if( bfs.IsEarlyGame && top1.OpponentPTKO < PotentialToKO.Dangerous )
        {
            score += 10;
            _ai.CurrentLog.Add( $"It's first round and we're not immediately threatened with death. Can we take this opportunity to setup? Score: {score}" );

            if( top2.AttackerMovedFirst )
                score += 5;
        }

        if( isMidGame )
        {
            if( top2.AttackerPTKO >= PotentialToKO.Dangerous )
                score += 10;
            else
                score += 5;

            _ai.CurrentLog.Add( $"Mid game setup. Score: {score}" );
        }

        if( isLateGame )
        {
            score -= 10;
            _ai.CurrentLog.Add( $"Late Game Setup Penalty. Score: {score}" );
        }

        int weatherContext = _ai.UnitSim.Get_WeatherContextScore( attackerMon );
        int terrainContext = _ai.UnitSim.Get_TerrainContextScore( attackerMon );
        int trickRoomContext = _ai.UnitSim.Get_TrickRoomContextScore( attackerMon );
        int contextScore = weatherContext + terrainContext + trickRoomContext;

        score += contextScore;
        _ai.CurrentLog.Add( $"[Attacker's Setup Battlefield Context] Weather: {weatherContext}, Terrian: {terrainContext}, TRoom: {trickRoomContext}. Total Context Score: {contextScore}. Score: {score}" );

        if( contextScore >= 10 )
        {
            score += 10;
            _ai.CurrentLog.Add( $"Field context greater than 10. Setting up scales with field advantage. Rewarding. Score: {score}" );
        }

        if( bfs.TheyHave_Reflect )
        {
            score -= bfs.TheirReflectDuration * 2;
            _ai.CurrentLog.Add( $"The opponent's behind a reflect. Let's stall and improve our chances of breaking through by setting up Score: {score}" );
        }

        if( bfs.TheyHave_LightScreen )
        {
            score -= bfs.TheirLightScreenDuration * 2;
            _ai.CurrentLog.Add( $"The opponent's behind a light. Let's stall and improve our chances of breaking through by setting up. Score: {score}" );
        }

        if( bfs.TheyHave_AuroraVeil )
        {
            score -= bfs.TheirAuroraVeilDuration * 2;
            _ai.CurrentLog.Add( $"The opponent's behind a aurora. Let's stall and improve our chances of breaking through by setting up. Score: {score}" );
        }

        if( bfs.WeHave_Reflect )
        {
            score += bfs.OurReflectDuration * 2;
            _ai.CurrentLog.Add( $"We're behind a reflect. We're likely protected enough to try setting up! Score: {score}" );
        }

        if( bfs.WeHave_LightScreen )
        {
            score += bfs.OurLightScreenDuration * 2;
            _ai.CurrentLog.Add( $"We're likely behind a light. We're likely protected enough to try setting up! Score: {score}" );
        }

        if( bfs.WeHave_AuroraVeil )
        {
            score += bfs.OurAuroraVeilDuration * 2;
            _ai.CurrentLog.Add( $"We're likely behind a aurora. We're likely protected enough to try setting up! Score: {score}" );
        }

        _ai.CurrentLog.Add( $"Final Battlefield State for Setting Up Score: {score}" );
        _ai.CurrentLog.Add( $"" );

        return score;
    }

    private int EvaluateBattlefieldFor_OffensiveStatus( ActionEvaluation action, BoardContext boardContext )
    {
        int score = 0;

        var bfs = boardContext.BattlefieldState;
        var top1 = action.Top1;
        var top2 = action.Top2;

        bool isMidGame = bfs.Round > 6 && bfs.Round < 16;
        bool isLateGame = bfs.Round > 15;

        var attackerMon = top1.Attacker.Pokemon;
        var opponentMon = top1.Opponent.Pokemon;

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"======================================================" );
        _ai.CurrentLog.Add( $"===[Evaluating Battlefield for Setup]===" );
        _ai.CurrentLog.Add( $"======================================================" );
        _ai.CurrentLog.Add( $"" );

        if( bfs.Round <= 1 && top1.OpponentPTKO < PotentialToKO.Dangerous )
        {
            if( top2.Attacker_EndOfTurnHP > 0 && top2.AttackerMovedFirst )
            {
                score += 35;
                _ai.CurrentLog.Add( $"It's first round and we're not immediately threatened with death, and we have tempo next turn. Can we take this opportunity to place hazards or cripple our opponent with status? Score: {score}" );
            }
            else
            {
                score += 15;
                _ai.CurrentLog.Add( $"It's first round and we're not immediately threatened with death. Can we take this opportunity to place hazards or cripple our opponent with status? Score: {score}" );
            }
        }
        else if( bfs.IsEarlyGame && top1.OpponentPTKO < PotentialToKO.Dangerous )
        {
            score += 10;
            _ai.CurrentLog.Add( $"It's early game and we're not immediately threatened with death. Can we take this opportunity to place hazards or cripple our opponent with status? Score: {score}" );

            if( top2.AttackerMovedFirst )
                score += 5;
        }

        if( isLateGame )
        {
            score -= 10;
            _ai.CurrentLog.Add( $"Late Game Offensive Status Penalty. Score: {score}" );
        }

        int weatherContext = _ai.UnitSim.Get_WeatherContextScore( attackerMon );
        int terrainContext = _ai.UnitSim.Get_TerrainContextScore( attackerMon );
        int trickRoomContext = _ai.UnitSim.Get_TrickRoomContextScore( attackerMon );
        int contextScore = weatherContext + terrainContext + trickRoomContext;

        score += contextScore;
        _ai.CurrentLog.Add( $"[Attacker's Offensive Status Battlefield Context] Weather: {weatherContext}, Terrian: {terrainContext}, TRoom: {trickRoomContext}. Total Context Score: {contextScore}. Score: {score}" );

        //--Hazard Value based on remaining opponents
        int remainingOpps = boardContext.OppRemainingPieces;
        int hazardValue = Mathf.Clamp( remainingOpps, 1, 4 );

        if( isLateGame )
        {
            hazardValue = Mathf.Max( 1, hazardValue - 2 );
            score += hazardValue;
        }
        else
            score += hazardValue * 5;

        _ai.CurrentLog.Add( $"Computed hazard value based on remaining opponents. Hazard Value * 5: {hazardValue * 5}. Score: {score}" );

        int hazardDelta = bfs.EntryHazardsOn_TheirSide - bfs.EntryHazardsOn_MySide;
        if( hazardDelta > 0 )
        {
            score += 5;
            _ai.CurrentLog.Add( $"Checked Hazard delta. Score: {score}" );
        }

        if( action.MovePayload.MoveSO.MoveEffects.CourtCondition == CourtConditionID.None )
        {
            if( !top1.OpponentCanAct || top2.AttackerPTKO >= PotentialToKO.Dangerous )
            {
                if( bfs.IsEarlyGame )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"Early game status effect prevents the opponent from acting this turn or rewards us with a chance to KO next round! Score: {score}" );
                }
                else if( isMidGame )
                {
                    score += 15;
                    _ai.CurrentLog.Add( $"Mid game status effect prevents the opponent from acting this turn or rewards us with a chance to KO next round! Score: {score}" );
                }
                else if( isLateGame )
                {
                    score += 5;
                    _ai.CurrentLog.Add( $"Late game status effect prevents the opponent from acting this turn or rewards us with a chance to KO next round! Score: {score}" );
                }
            }
        }

        if( bfs.TheyHave_FieldControl )
        {
            score += 10;
            _ai.CurrentLog.Add( $"They have control of the field. We should try disrupting or reclaiming field control via hazards. Score: {score}" );
        }

        _ai.CurrentLog.Add( $"Final Battlefield State for Offensive Status Score: {score}" );
        _ai.CurrentLog.Add( $"" );

        return score;
    }
}
