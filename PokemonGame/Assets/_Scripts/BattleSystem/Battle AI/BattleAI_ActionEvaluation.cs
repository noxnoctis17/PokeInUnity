using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ActionType { Attack, OffensiveSwitch, DefensiveSwitch, Setup, OffensiveStatus, SupportiveStatus }
public class BattleAI_ActionEvaluation
{
    private BattleAI _ai;

    public BattleAI_ActionEvaluation( BattleAI ai )
    {
        _ai = ai;
    }

    public ActionEvaluation BuildActionEvaluation( ActionType type, IActionResult actionResult, IBattleAIUnit target, BattleUnit targetBattleUnit, object payload, TurnOutcomeProjection top, ExchangePack exchangePack )
    {
        ActionEvaluation eval = new()
        {
            Type = type,
            ActionResult = actionResult,
            Score = 0,
            Top1 = top,
            ExchangePack = exchangePack,
            Actor = top.Attacker.Pokemon,
        };

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"================================================================" );
        _ai.CurrentLog.Add( $"Building Action Evaluation for {eval.Type}..." );
        _ai.CurrentLog.Add( $"" );

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

        switch( type )
        {
            case ActionType.Attack: //--and--//
            case ActionType.Setup:
            case ActionType.OffensiveStatus:
            case ActionType.SupportiveStatus:
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

    public ActionEvaluation EvaluateSimulation( ActionEvaluation eval )
    {
        return eval.Type switch
        {
            ActionType.Attack           => EvaluateAttackSim( eval ),
            ActionType.DefensiveSwitch  => EvaluateDefensiveSwitchSim( eval ),
            ActionType.OffensiveSwitch  => EvaluateOffensiveSwitchSim( eval ),
            ActionType.Setup            => EvaluateSetupSim( eval ),
            ActionType.OffensiveStatus  => EvaluateOffensiveStatusSim( eval ),
            ActionType.SupportiveStatus => EvaluateSupportiveStatusSim( eval ),
            _ => eval,
        };
    }

    private float NormalizeDamage( float rawDamage, float currentHPR )
    {
        return rawDamage / Mathf.Max( currentHPR, 0.001f );
    }

    private ActionEvaluation EvaluateAttackSim( ActionEvaluation eval )
    {
        int score = 0;
        var top = eval.Top1;

        _ai.CurrentLog.Add( $"====================================" );
        _ai.CurrentLog.Add( $"===[Evaluating Attack Simulation]===" );
        _ai.CurrentLog.Add( $"====================================" );
        _ai.CurrentLog.Add( $"Our PTKO {top.AttackerPTKO} with Move: {top.Attacker.MTR?.Move?.MoveSO.Name}" );
        _ai.CurrentLog.Add( $"Their PTKO {top.OpponentPTKO} with Move: {top.Opponent.MTR?.Move?.MoveSO.Name}" );

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
        float theySwitchProbability = eval.ExchangePack.UsVS_Threat.OpponentSwitchProbability;
        score += Mathf.FloorToInt( 25f * theySwitchProbability );
        _ai.CurrentLog.Add( $"Probability the opponent switches: {theySwitchProbability}. Score: {score}" );

        //--Risky survival push
        var ee = eval.ExchangePack.UsVS_Threat;
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
        
        var ourActiveAdapters = _ai.GetActiveAllyUnits_AsBattleAIUnits( _ai.CurrentUnitDeciding.Pokemon );
        
        var offensiveSwitch = _ai.CandidateSelect.GetSwitch_Revenge( ourActiveAdapters ).Pokemon;
        var defensiveSwitch = _ai.CandidateSelect.GetSwitch_Defensive( top.Opponent ).Top.Attacker;

        SimulatedUnit nextOpponent;
        MoveThreatResult nextOpponentMTR;

        if( top.Opponent_EndOfTurnHP <= 0f && offensiveSwitch != null )
        {
            BattleAI_PokemonAdapter opponentOffensiveSwitchAdapter = _ai.GetPokemonAs_Adapter( offensiveSwitch );
            nextOpponentMTR = _ai.CandidateSelect.GetMove_BestAttack( opponentOffensiveSwitchAdapter, top.Attacker );
            nextOpponent = _ai.UnitSim.BuildSimUnit( opponentOffensiveSwitchAdapter, opponentOffensiveSwitchAdapter.BeginningHPR, nextOpponentMTR, top.Field );
        }
        else if( weForceSwitch && defensiveSwitch != null )
        {
            SimulatedUnit opponentDefensiveSwitchAdapter = defensiveSwitch;
            nextOpponentMTR = _ai.CandidateSelect.GetMove_BestAttack( opponentDefensiveSwitchAdapter, top.Attacker );
            nextOpponent = _ai.UnitSim.BuildSimUnit( opponentDefensiveSwitchAdapter, opponentDefensiveSwitchAdapter.CurrentHPR, nextOpponentMTR, top.Field );
        }
        else
        {
            nextOpponentMTR = _ai.CandidateSelect.GetMove_BestAttack( top.Opponent, top.Attacker );
            nextOpponent = _ai.UnitSim.BuildSimUnit( top.Opponent, top.Opponent_EndOfTurnHP, nextOpponentMTR, top.Field );
        }

        var next = _ai.CandidateSelect.GetMove_BestAttack( top.Attacker, nextOpponent ).Top;

        bool weKOThem = next.Opponent_DiesBeforeActing || next.Opponent_EndOfTurnHP <= 0f;
        bool weDie = next.Attacker_DiesBeforeActing || next.Attacker_EndOfTurnHP <= 0f;

        if( weKOThem )
        {
            score += 50;
            _ai.CurrentLog.Add( $"We KO them in the look ahead round! Score: {score}" );
        }

        if( weDie )
        {
            score -= 70;
            _ai.CurrentLog.Add( $"They KO us in the look ahead round! Score: {score}" );
        }

        bool weMaintainPressure = next.AttackerPTKO >= PotentialToKO.TwoHKO;
        bool theyThreatenUs = next.OpponentPTKO >= PotentialToKO.Dangerous && !next.AttackerMovedFirst;

        if( weMaintainPressure )
        {
            score += 25;
            _ai.CurrentLog.Add( $"We maintain pressure in the look ahead round! Score: {score}" );
        }

        if( theyThreatenUs )
        {
            score -= 30;
            _ai.CurrentLog.Add( $"They threaten us in the look ahead round! Score: {score}" );
        }

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
        float weAreForcedOutProb = _ai.UnitSim.PredictSwitchProbability( next.Attacker.Pokemon, next.OpponentPTKO, next.AttackerPTKO, next.AttackerMovedFirst, top.Opponent_EndOfTurnHP, top.Attacker_EndOfTurnHP, next.Attacker.Expendability );
        float theyAreForcedOutProb = _ai.UnitSim.PredictSwitchProbability( next.Opponent.Pokemon, next.AttackerPTKO, next.OpponentPTKO, next.AttackerMovedFirst, top.Attacker_EndOfTurnHP, top.Opponent_EndOfTurnHP, next.Opponent.Expendability );

        score += Mathf.FloorToInt( 25f * weAreForcedOutProb );
        _ai.CurrentLog.Add( $"We switch probability: {weAreForcedOutProb}. Score: {score}" );

        score -= Mathf.FloorToInt( 30f * theyAreForcedOutProb );
        _ai.CurrentLog.Add( $"They switch probability: {theyAreForcedOutProb}. Score: {score}" );

        eval.NextTurn_WeAreForcedOut = weAreForcedOutProb >= 0.7f;
        eval.NextTurn_TheyAreForcedOut = theyAreForcedOutProb >= 0.7f;

        eval.Top2 = next;
        eval.Score += score;
        _ai.CurrentLog.Add( $"Evaluate Attack Simulation Score: {score}" );
        _ai.CurrentLog.Add( $"Current Attack Decision Score: {eval.Score}" );
        return eval;
    }

    private ActionEvaluation EvaluateDefensiveSwitchSim( ActionEvaluation eval )
    {
        var top = eval.Top1;
        int score = 0;

        _ai.CurrentLog.Add( $"==============================================" );
        _ai.CurrentLog.Add( $"===[Evaluating Defensive Switch Simulation]===" );
        _ai.CurrentLog.Add( $"==============================================" );
        _ai.CurrentLog.Add( $"Our PTKO {top.AttackerPTKO} with Move: {top.Attacker.MTR?.Move?.MoveSO.Name}" );
        _ai.CurrentLog.Add( $"Their PTKO {top.OpponentPTKO} with Move: {top.Opponent.MTR?.Move?.MoveSO.Name}" );

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
        var ee = eval.ExchangePack.UsVS_Threat;
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
        var next = _ai.CandidateSelect.GetMove_BestAttack( top.Attacker, top.Opponent ).Top;

        //--First we compare threat
        bool weDie = next.Attacker_DiesBeforeActing || next.Attacker_EndOfTurnHP <= 0f;
        bool weKOThem = next.Opponent_DiesBeforeActing || next.Opponent_EndOfTurnHP <= 0f;

        bool theyThreatenUs = next.OpponentPTKO >= PotentialToKO.Dangerous && !next.AttackerMovedFirst;
        bool weThreatenThem = next.AttackerPTKO >= PotentialToKO.TwoHKO && next.AttackerMovedFirst;

        bool weCantThreatenBack = next.AttackerPTKO >= PotentialToKO.TwoHKO && !next.AttackerMovedFirst;

        float weAreForcedOut = _ai.UnitSim.PredictSwitchProbability( next.Attacker.Pokemon, next.OpponentPTKO, next.AttackerPTKO, next.AttackerMovedFirst, top.Opponent_EndOfTurnHP, top.Attacker_EndOfTurnHP, next.Attacker.Expendability );
        float theyAreForcedOut = _ai.UnitSim.PredictSwitchProbability( next.Opponent.Pokemon, next.AttackerPTKO, next.OpponentPTKO, next.AttackerMovedFirst, top.Attacker_EndOfTurnHP, top.Opponent_EndOfTurnHP, next.Opponent.Expendability );

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
        eval.Score += score;
        _ai.CurrentLog.Add( $"Evaluate Defensive Switch Simulation Score: {score}" );
        _ai.CurrentLog.Add( $"Current Defensive Switch Decision Score: {eval.Score}" );
        return eval;
    }

    private ActionEvaluation EvaluateOffensiveSwitchSim( ActionEvaluation eval )
    {
        int score = 0;
        var top = eval.Top1;

        _ai.CurrentLog.Add( $"==============================================" );
        _ai.CurrentLog.Add( $"===[Evaluating Offensive Switch Simulation]===" );
        _ai.CurrentLog.Add( $"==============================================" );
        _ai.CurrentLog.Add( $"Our PTKO {top.AttackerPTKO} with Move: {top.Attacker.MTR?.Move?.MoveSO.Name}" );
        _ai.CurrentLog.Add( $"Their PTKO {top.OpponentPTKO} with Move: {top.Opponent.MTR?.Move?.MoveSO.Name}" );

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
        var next = _ai.CandidateSelect.GetMove_BestAttack( top.Attacker, top.Opponent ).Top;

        bool weKOThem = next.Opponent_DiesBeforeActing || next.Opponent_EndOfTurnHP <= 0f;
        if( weKOThem )
            score += 60;

        bool weThreaten = next.AttackerPTKO >= PotentialToKO.Dangerous;
        if( weThreaten )
            score += 35;

        float theyAreForcedOut = _ai.UnitSim.PredictSwitchProbability( next.Attacker.Pokemon, next.AttackerPTKO, next.OpponentPTKO, next.AttackerMovedFirst, top.Attacker_EndOfTurnHP, top.Opponent_EndOfTurnHP, next.Opponent.Expendability );
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

        float weAreForcedOut = _ai.UnitSim.PredictSwitchProbability( next.Opponent.Pokemon, next.OpponentPTKO, next.AttackerPTKO, next.AttackerMovedFirst, top.Opponent_EndOfTurnHP, top.Attacker_EndOfTurnHP, next.Attacker.Expendability );
        score -= Mathf.FloorToInt( 75f * weAreForcedOut );

        float damageTakenRaw = top.Attacker.CurrentHPR - next.Attacker_EndOfTurnHP;
        float damageTaken = NormalizeDamage( damageTakenRaw, top.Attacker.CurrentHPR );
        bool noPressure = next.AttackerPTKO < PotentialToKO.TwoHKO;

        if( noPressure && ( damageTaken >= 0.4f || oppHPLoss < 0.2f && damageTaken >= 0.3f ) )
            score -= 50;

        eval.Top2 = next;
        eval.Score += score;
        _ai.CurrentLog.Add( $"Evaluate Offensive Switch Simulation Score: {score}" );
        _ai.CurrentLog.Add( $"Current Offensive Switch Decision Score: {eval.Score}" );
        return eval;
    }

    private ActionEvaluation EvaluateSetupSim( ActionEvaluation eval )
    {
        const int DIE_BEFORE_ACTING_PENALTY         = 150;
        const int SETUP_DIES_AFTER_ACTING_PENALTY   = 175;
        const int HEAVY_SETUP_DAMAGE_PENALTY        = 50;
        const int SETUP_THREATEN_KO_NEXT_TURN       = 30;
        const int OPPONENT_SWITCH_WEIGHT            = 50;
        const int WE_SWITCH_WEIGHT                  = 75;

        int score = 0;
        var top = eval.Top1;

        _ai.CurrentLog.Add( $"===================================" );
        _ai.CurrentLog.Add( $"===[Evaluating Setup Simulation]===" );
        _ai.CurrentLog.Add( $"===================================" );
        _ai.CurrentLog.Add( $"Our PTKO {top.AttackerPTKO} with Move: {top.Attacker.MTR?.Move?.MoveSO.Name}" );
        _ai.CurrentLog.Add( $"Their PTKO {top.OpponentPTKO} with Move: {top.Opponent.MTR?.Move?.MoveSO.Name}" );

        float oppSwitchProb = eval.ExchangePack.UsVS_Threat.OpponentSwitchProbability;
        score += Mathf.FloorToInt( OPPONENT_SWITCH_WEIGHT * oppSwitchProb );

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

        var ourNextAttacker = top.Attacker_EndOfTurnHP > 0f ? top.Attacker : _ai.CandidateSelect.GetSwitch_Revenge( _ai.Blackboard.TheirActiveBattleAIUnits ).Candidate;
        ourNextAttacker ??= top.Attacker;
        var next = _ai.CandidateSelect.GetMove_BestAttack( ourNextAttacker, top.Opponent, false, "Evaluate Setup Action (Look Ahead)" ).Top;

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

        float weForceSwitchNextTurnProbability = _ai.UnitSim.PredictSwitchProbability( next.Attacker.Pokemon, next.AttackerPTKO, next.OpponentPTKO, movesFirst, next.Attacker.CurrentHPR, next.Opponent.CurrentHPR, next.Opponent.Expendability, true, $"{next.Opponent.Name} (Setup Look Ahead)" );
        float theyForceUsToSwitchNextTurnProbability = _ai.UnitSim.PredictSwitchProbability( next.Opponent.Pokemon, next.OpponentPTKO, next.AttackerPTKO, movesFirst, next.Opponent.CurrentHPR, next.Attacker.CurrentHPR, next.Attacker.Expendability, true, $"{next.Attacker.Name} (Setup Look Ahead)" );

        float dangerWeight =
            next.OpponentPTKO >= PotentialToKO.OHKO ? 1.5f :
            next.OpponentPTKO >= PotentialToKO.Dangerous ? 1.25f :
            next.OpponentPTKO >= PotentialToKO.Risky ? 1f :
            next.OpponentPTKO >= PotentialToKO.TwoHKO ? 0.5f : 0.25f;

        float penalty = WE_SWITCH_WEIGHT * dangerWeight;

        score += Mathf.FloorToInt( OPPONENT_SWITCH_WEIGHT * weForceSwitchNextTurnProbability );
        score -= Mathf.FloorToInt( ( 1f - theyForceUsToSwitchNextTurnProbability ) * penalty );

        var oppTeam = _ai.GetRemainingOpposingPokemon( next.Attacker.Pokemon );
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
        eval.Score += score;
        _ai.CurrentLog.Add( $"Evaluate Setup Simulation Score: {score}" );
        _ai.CurrentLog.Add( $"Current Setup Decision Score: {eval.Score}" );
        return eval;
    }

    private ActionEvaluation EvaluateOffensiveStatusSim( ActionEvaluation eval )
    {
        int score = 0;
        var top = eval.Top1;

        _ai.CurrentLog.Add( $"==============================================" );
        _ai.CurrentLog.Add( $"===[Evaluating Offensive Status Simulation]===" );
        _ai.CurrentLog.Add( $"==============================================" );
        _ai.CurrentLog.Add( $"Our PTKO {top.AttackerPTKO} with Move: {top.Attacker.MTR?.Move?.MoveSO.Name}" );
        _ai.CurrentLog.Add( $"Their PTKO {top.OpponentPTKO} with Move: {top.Opponent.MTR?.Move?.MoveSO.Name}" );

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

        //--------------------------------
        //----------Look Ahead------------
        //--------------------------------

        var ourNextAttacker = top.Attacker_EndOfTurnHP > 0f ? top.Attacker : _ai.CandidateSelect.GetSwitch_Revenge( _ai.Blackboard.TheirActiveBattleAIUnits ).Candidate;
        var next = _ai.CandidateSelect.GetMove_BestAttack( ourNextAttacker, top.Opponent, false, "Evaluate Offensive Status Sim (Look Ahead)" ).Top;

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

        float weForceSwitchNextTurnProb = _ai.UnitSim.PredictSwitchProbability( next.Opponent.Pokemon, next.AttackerPTKO, next.OpponentPTKO, next.AttackerMovedFirst, next.Attacker.BeginningHPR, next.Opponent.BeginningHPR, top.Opponent.Expendability );
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
        eval.Score += score;
        _ai.CurrentLog.Add( $"Evaluate Offensive Status Simulation Score: {score}" );
        _ai.CurrentLog.Add( $"Current Offensive Status Decision Score: {eval.Score}" );
        return eval;
    }

    private ActionEvaluation EvaluateSupportiveStatusSim( ActionEvaluation eval )
    {
        int score = 0;
        var top1 = eval.Top1;

        //--ExchangeEvaluation is a PRE EVERYTHING attack exchange.
        //--This means any status effects or battlefield effects
        //--that would get applied this turn are not in effect,
        //--being a reliable source for before supportive status is applied!
        var ee1 = eval.ExchangePack.UsVS_Threat;
        var eeAttackerPTKO = ee1.AttackerPTKO;
        var eeOpponentPTKO = ee1.OpponentPTKO;

        _ai.CurrentLog.Add( $"===============================================" );
        _ai.CurrentLog.Add( $"===[Evaluating Supportive Status Simulation]===" );
        _ai.CurrentLog.Add( $"===============================================" );
        _ai.CurrentLog.Add( $"Our PTKO {top1.AttackerPTKO} with Move: {top1.Attacker.MTR?.Move?.MoveSO.Name}" );
        _ai.CurrentLog.Add( $"Their PTKO {top1.OpponentPTKO} with Move: {top1.Opponent.MTR?.Move?.MoveSO.Name}" );

        float oppSwitchProb = eval.ExchangePack.UsVS_Threat.OpponentSwitchProbability;
        score += Mathf.FloorToInt( 50f * oppSwitchProb );

        if( top1.Attacker_DiesBeforeActing )
        {
            score -= 150;
            _ai.CurrentLog.Add( $"Attacker dies before support can be used! Score: {score}" );
        }

        //--We get KOd after executing our support move
        if( top1.Attacker_EndOfTurnHP <= 0 )
        {
            score -= 50;
            _ai.CurrentLog.Add( $"Attacker faints after using support! May be a reasonable sacrifice.... Not penalizing too heavily until we have better contextual information available! Score: {score}" );
        }

        //--Immediate Results of Support Move
        //--I think i'd like to be able to compare an exchange of before and after support takes effect. i actually really would like a beforeTOP directly from
        //--candidate selection for this particular evaluator. intentTOP will be the "after", with the "true" action from the opponent. we can use ExchangeEvaluation for an attack-case after
        //--PTKO in the event that intentTOP doesn't come back with the opponent attacking us, giving us an inaccurate PTKO.
        //--For now, i will just make some simple checks that essentially mirror the EvaluateBattlefield ones, and then again for the look ahead, and later when i have access to doubles
        //--architecture inside of TOP, and i add a beforeStatusTOP to StatusThreatResult, i can come back and expand checks for us, ally, opponent, opponent ally interactions and cross-turn ptko and speed changes.

        //--Opponent's ability to KO us
        if( top1.OpponentPTKO <= eeOpponentPTKO )
        {
            score += 30;
            _ai.CurrentLog.Add( $"This action reduces the opponent's potential to KO us this turn. Score: {score}" );
        }
        else if( eeOpponentPTKO <= top1.OpponentPTKO )
        {
            score -= 45;
            _ai.CurrentLog.Add( $"This action doesn't change the opponent's potential to KO us this turn, or makes it worse. Score: {score}" );
        }

        if( top1.OpponentPTKO <= PotentialToKO.Safe )
        {
            score += 45;
            _ai.CurrentLog.Add( $"The opponent has a very low PTKO on us next turn. Score: {score}" );
        }
        else if( top1.OpponentPTKO <= PotentialToKO.Risky )
        {
            score += 35;
            _ai.CurrentLog.Add( $"The opponent has a survivable PTKO on us next turn. Score: {score}" );
        }
        else if( top1.OpponentPTKO >= PotentialToKO.Dangerous )
        {
            score -= 55;
            _ai.CurrentLog.Add( $"The opponent has a reasonable chance to KO us next turn. Score: {score}" );
        }

        //--Our ability to KO opponent
        if( eeAttackerPTKO < top1.AttackerPTKO )
        {
            score += 45;
            _ai.CurrentLog.Add( $"This action improves our potential to KO the opponent this turn. Score: {score}" );
        }

        if( top1.AttackerPTKO >= PotentialToKO.OHKO )
        {
            score += 40;
            _ai.CurrentLog.Add( $"We have an OHKO available on our opponent next turn. Score: {score}" );
        }
        else if( top1.AttackerPTKO >= PotentialToKO.Dangerous )
        {
            score += 30;
            _ai.CurrentLog.Add( $"We have a good chance to KO our opponent next turn. Score: {score}" );
        }
        else if( top1.AttackerPTKO >= PotentialToKO.Risky )
        {
            score += 20;
            _ai.CurrentLog.Add( $"We do good damage to our opponent next turn. Score: {score}" );
        }

        //--Speed gain
        if( !ee1.AttackerMovesFirst && top1.AttackerMovedFirst )
        {
            score += 50;
            _ai.CurrentLog.Add( $"This action causes us to outspeed the opponent this turn, when we didn't without this action's effect. Score: {score}" );
        }
        else if( top1.AttackerMovedFirst )
        {
            score += 10;
            _ai.CurrentLog.Add( $"We move first when using our support move this turn. Score: {score}" );
        }
        
        //--------------------------------
        //----------Look Ahead------------
        //--------------------------------

        var ourNextAttacker = top1.Attacker_EndOfTurnHP > 0f ? top1.Attacker : _ai.CandidateSelect.GetSwitch_Revenge( _ai.Blackboard.TheirActiveBattleAIUnits ).Candidate;
        var top2 = _ai.CandidateSelect.GetMove_BestAttack( ourNextAttacker, top1.Opponent, false, "Evaluate Supportive Status Sim (Look Ahead)" ).Top;

        float weSwitchNextProb = _ai.UnitSim.PredictSwitchProbability( top2.Attacker.Pokemon, top2.OpponentPTKO, top2.AttackerPTKO, top2.AttackerMovedFirst, top2.Opponent.BeginningHPR, top2.Attacker.BeginningHPR, top2.Attacker.Expendability );
        score -= Mathf.FloorToInt( 35f * weSwitchNextProb );
        _ai.CurrentLog.Add( $"We switch next turn probability: {weSwitchNextProb}. Score: {score}" );

        float oppSwitchNextProb = _ai.UnitSim.PredictSwitchProbability( top2.Opponent.Pokemon, top2.AttackerPTKO, top2.OpponentPTKO, top2.AttackerMovedFirst, top2.Attacker.BeginningHPR, top2.Opponent.BeginningHPR, top2.Opponent.Expendability );
        score += Mathf.FloorToInt( 30f * oppSwitchNextProb );
        _ai.CurrentLog.Add( $"They switch next turn probability: {oppSwitchNextProb}. Score: {score}" );

        //--Opponent's ability to KO us
        if( top1.OpponentPTKO > top2.OpponentPTKO )
        {
            score += 20;
            _ai.CurrentLog.Add( $"This action reduces the opponent's potential to KO us next turn. Score: {score}" );
        }
        else if( top1.OpponentPTKO <= top2.OpponentPTKO )
        {
            score -= 30;
            _ai.CurrentLog.Add( $"This action doesn't change the opponent's potential to KO us next turn, or makes it worse. Score: {score}" );
        }
        
        if( top2.OpponentPTKO <= PotentialToKO.Safe )
        {
            score += 35;
            _ai.CurrentLog.Add( $"The opponent has a very low PTKO on us next turn. Score: {score}" );
        }
        else if( top2.OpponentPTKO <= PotentialToKO.Risky )
        {
            score += 25;
            _ai.CurrentLog.Add( $"The opponent has a survivable PTKO on us next turn. Score: {score}" );
        }
        else if( top2.OpponentPTKO >= PotentialToKO.Dangerous )
        {
            score -= 45;
            _ai.CurrentLog.Add( $"The opponent has a reasonable chance to KO us next turn. Score: {score}" );
        }

        //--Our ability to KO opponent
        if( top1.AttackerPTKO < top2.AttackerPTKO )
        {
            score += 25;
            _ai.CurrentLog.Add( $"This action improves our potential to KO the opponent next turn. Score: {score}" );
        }

        if( top2.AttackerPTKO >= PotentialToKO.OHKO )
        {
            score += 30;
            _ai.CurrentLog.Add( $"We have an OHKO available on our opponent next turn. Score: {score}" );
        }
        else if( top2.AttackerPTKO >= PotentialToKO.Dangerous )
        {
            score += 20;
            _ai.CurrentLog.Add( $"We have a good chance to KO our opponent next turn. Score: {score}" );
        }
        else if( top2.AttackerPTKO >= PotentialToKO.Risky )
        {
            score += 10;
            _ai.CurrentLog.Add( $"We do good damage to our opponent next turn. Score: {score}" );
        }

        //--Speed gain
        if( !top1.AttackerMovedFirst && top2.AttackerMovedFirst )
        {
            score += 35;
            _ai.CurrentLog.Add( $"This action causes us to outspeed the opponent next turn, when we didn't this turn. Score: {score}" );
        }
        else if( top2.AttackerMovedFirst )
        {
            score += 20;
            _ai.CurrentLog.Add( $"We move first next turn after executing a support action. Score: {score}" );
        }

        eval.Top2 = top2;
        eval.Score += score;
        _ai.CurrentLog.Add( $"Evaluate Supportive Status Simulation Score: {score}" );
        _ai.CurrentLog.Add( $"Current Supportive Status Decision Score: {eval.Score}" );
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
            var switchCandidate = _ai.CandidateSelect.GetSwitch_Revenge( _ai.Blackboard.TheirActiveBattleAIUnits ).Pokemon;
            if( switchCandidate != null )
                revengeCandidate = _ai.GetPokemonAs_Adapter( switchCandidate );
        }
        else if( eval.Top1.AttackerPTKO <= PotentialToKO.Safe && eval.Top1.OpponentPTKO >= PotentialToKO.TwoHKO )
        {
            var switchCandidate = _ai.CandidateSelect.GetSwitch_Revenge( _ai.Blackboard.TheirActiveBattleAIUnits ).Pokemon;
            if( switchCandidate != null )
                revengeCandidate = _ai.GetPokemonAs_Adapter( switchCandidate );
        }

        IBattleAIUnit nextPokemon;
        if( revengeCandidate != null )
            nextPokemon = revengeCandidate;
        else
            nextPokemon = eval.Top1.Attacker;

        //--Look ahead at the next round
        var followUp = _ai.CandidateSelect.GetMove_BestAttack( nextPokemon, eval.Top1.Opponent ).Top;

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
        float weForceSwitchNextTurnProb = _ai.UnitSim.PredictSwitchProbability( followUp.Opponent.Pokemon, followUp.AttackerPTKO, followUp.OpponentPTKO, followUp.AttackerMovedFirst, nextPokemon.CurrentHPR, eval.Top1.Opponent_EndOfTurnHP, followUp.Opponent.Expendability );
        score += Mathf.FloorToInt( 30f * weForceSwitchNextTurnProb );
        _ai.CurrentLog.Add( $"Opponent's switch probability {weForceSwitchNextTurnProb} * 30f. Score: {score}" );

        //--Dead end penalty to punish bad sacrifice lines
        if( ( followUp.Attacker_EndOfTurnHP <= 0 || followUp.Attacker_DiesBeforeActing ) && followUp.Opponent_EndOfTurnHP >= 0.5f )
        {
            score -= 100;
            _ai.CurrentLog.Add( $"Dead end detected for {eval.Type} decision line! Penalizing... Score: {score}" );
        }

        //--Piece Value death penalty
        if( _ai.Blackboard.OurTeamPieceValues.TryGetValue( eval.Top1.Attacker.Pokemon, out var pieceValue ) )
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

        if( nextPokemon != null && _ai.Blackboard.OurTeamPieceValues.TryGetValue( nextPokemon.Pokemon, out var nextValue ) )
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

        //--This action's switch probability.
        float theySwitchProbability = _ai.UnitSim.PredictSwitchProbability( top1.Opponent.Pokemon, top1.AttackerPTKO, top1.OpponentPTKO, top1.AttackerMovedFirst, top1.Attacker.BeginningHPR, top1.Opponent.BeginningHPR, top1.Opponent.Expendability );
        score += Mathf.FloorToInt( 50f * theySwitchProbability );
        _ai.CurrentLog.Add( $"Switch Probability: {theySwitchProbability}. Score: {score}" );

        switch( threat.Type )
        {
            case ThreatType.Immediate:

                if( top1.Attacker_EndOfTurnHP <= 0f )
                {
                    score -= Mathf.RoundToInt( 40 * sackModifier );
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

                if( !top1.AttackerMovedFirst && top2.AttackerMovedFirst )
                {
                    score += 30;
                    _ai.CurrentLog.Add( $"This action flips the speed dynamic against the immediate threat. Score: {score}" );
                }

                if( top1.Attacker_EndOfTurnHP > 0 && ( top1.AttackerPTKO >= PotentialToKO.Risky && top1.AttackerMovedFirst || top1.AttackerPTKO >= PotentialToKO.Dangerous ) )
                {
                    score += 40;
                    _ai.CurrentLog.Add( $"Attacker survives and threatens big damage on burst damage threat opponent. Score: {score}" );
                }

                if( top1.Opponent_EndOfTurnHP <= 0f )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"Opponent is KO'd this round! Score: {score}" );
                }

                if( top1.OpponentPTKO >= PotentialToKO.Risky && top2.OpponentPTKO < PotentialToKO.Risky )
                {
                    score += 40;
                    _ai.CurrentLog.Add( $"Opponent's PTKO {top1.OpponentPTKO} during this round is lessened to {top2.OpponentPTKO} next round! Score: {score}" );
                }

                if( threat.ThreatensImmediateKO && action.Type == ActionType.DefensiveSwitch && top2.OpponentPTKO < PotentialToKO.Risky )
                {
                    score += 25;
                    _ai.CurrentLog.Add( $"Opponent threatens an immediate KO, and this defensive switch absorbs the damage meaningfully. Score: {score}" );

                    if( action.Top2.Attacker_EndOfTurnHP > 0 )
                    {
                        score += 20;
                        _ai.CurrentLog.Add( $"Defensive switch candidate survives next turn as well. Score: {score}" );
                    }
                }

                if( action.Type == ActionType.OffensiveSwitch )
                {
                    if( action.Top2.Attacker_EndOfTurnHP > 0 )
                    {
                        if( action.Top2.AttackerPTKO >= PotentialToKO.Dangerous )
                        {
                            score += 30;
                            _ai.CurrentLog.Add( $"Offensive switch candidate survives next round and threatens big damage! Score: {score}" );
                        }

                        if( action.Top2.AttackerMovedFirst )
                        {
                            score += 30;
                            _ai.CurrentLog.Add( $"Offensive switch candidate outspeeds next turn! Score: {score}" );
                        }
                    }
                }

                //--Force out potential
                score += Mathf.FloorToInt( 25f * theySwitchProbability );
                bool phazer = action.Top1.Attacker.RoleProfile.Traits.Contains( RoleTrait.Phazes );
                if( phazer )
                {
                    if( action.Type == ActionType.OffensiveStatus && action.Top1.Attacker_EndOfTurnHP > 0 )
                    {
                        score += 25;
                        _ai.CurrentLog.Add( $"Phazer survives phaze attemp this turn. Score: {score}" );
                    }

                    if( ( action.Type == ActionType.OffensiveSwitch || action.Type == ActionType.DefensiveSwitch ) && action.Top2.Attacker_EndOfTurnHP > 0 )
                    {
                        score += 25;
                        _ai.CurrentLog.Add( $"Switch has phaze potential and survives next turn, forcing immediate damage threat out by phazing is possible. Score: {score}" );
                    }
                }

                //--Penalize Passive Actions
                if( action.Type == ActionType.Setup && ( action.Top1.Attacker_EndOfTurnHP <= 0f || top2.Opponent_EndOfTurnHP > 0f ) )
                {
                    score -= 15;
                    _ai.CurrentLog.Add( $"Setting up this turn results in either us dying or us not getting a KO next turn, which is passive vs an immediate damage threat. Reducing score slightly, as this type of check exists in many other places. Score: {score}" );
                }

                //--Role Considerations
                if( threat.ThreatUnit.RoleProfile.PrimaryRole == RoleClass.RevengeKiller && ( action.Top2.AttackerMovedFirst || !action.Top2.OpponentCanAct ) )
                {
                    score += 20;
                    _ai.CurrentLog.Add( $"This action shuts down a revenge killer, reversing tempo on their attempted tempo grab. Score: {score}" );
                }

                if( threat.ThreatUnit.RoleProfile.PrimaryRole == RoleClass.Sweeper || threat.ThreatUnit.RoleProfile.PrimaryRole == RoleClass.SetupSweeper )
                {
                    if( damageDealt >= 0.5f )
                    {
                        score += 15;
                        _ai.CurrentLog.Add( $"Chunked a sweep-threat passed a damage threshold, rewarding. Score: {score}" );
                    }

                    if( !top1.OpponentCanAct || !top2.OpponentCanAct )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"This action prevents a sweeper type from acting either this turn or next turn, rewarding. Score: {score}" );
                    }
                }

                if( threat.ThreatUnit.RoleProfile.Traits.Contains( RoleTrait.Frail ) || threat.ThreatUnit.RoleProfile.Traits.Contains( RoleTrait.FocusSash ) || threat.ThreatUnit.RoleProfile.Biases.Contains( RoleBias.GlassCannon ) )
                {
                    if( damageDealt >= 0.25f )
                    {
                        score += 20;
                        _ai.CurrentLog.Add( $"Did chip damage to a frail or focus sash mon, rewarding. Score: {score}" );
                    }
                    else if( damageDealt >= 0.20f )
                    {
                        score += 15;
                        _ai.CurrentLog.Add( $"Did chip damage to a frail or focus sash mon, rewarding. Score: {score}" );
                    }
                    else if( damageDealt >= 0.15f )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Did chip damage to a frail or focus sash mon, rewarding. Score: {score}" );
                    }

                    if( action.Top2.Attacker.Ability == AbilityID.Sandstream && action.Top1.Field.Weather != WeatherConditionID.SANDSTORM )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"This action sets sandstorm next turn, which will chip away at a frail/focus sash mon. Score: {score}" );
                    }

                    if( action.Type == ActionType.OffensiveStatus && _ai.UnitSim.MoveIsEntryHazard( action.MovePayload) )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Current threat is frail or holding a sash - setting hazards will apply pressure to them. Score: {score}" );

                        if( theySwitchProbability >= 0.75f )
                        {
                            score += 15;
                            _ai.CurrentLog.Add( $"They have a good likelyhood of switching next turn. Applying hazards now punishes the switch and causes good chip to a frail/sashed mon. Score: {score}" );
                        }
                    }
                }

                bool offenseDependent =
                    threat.ThreatUnit.RoleProfile.PrimaryRole == RoleClass.Sweeper ||
                    threat.ThreatUnit.RoleProfile.PrimaryRole == RoleClass.RevengeKiller ||
                    threat.ThreatUnit.RoleProfile.PrimaryRole == RoleClass.SetupSweeper;

                if( offenseDependent && action.Type == ActionType.OffensiveStatus )
                {
                    if( top1.Opponent.SevereStatus == SevereConditionID.None && top2.Opponent.SevereStatus != SevereConditionID.None )
                    {
                        score += 10;
                        var status = top2.Opponent.SevereStatus;
                        var biases = top1.Opponent.RoleProfile.Biases;
                        var traits = top1.Opponent.RoleProfile.Traits;

                        if( biases.Contains( RoleBias.Physical ) && status == SevereConditionID.BRN )
                        {
                            score += 15;
                        }

                        if( biases.Contains( RoleBias.Special ) && status == SevereConditionID.FBT )
                        {
                            score += 15;
                        }

                        if( ( biases.Contains( RoleBias.MiddlingSpeed ) || biases.Contains( RoleBias.FastSpeed ) ) && status == SevereConditionID.PAR )
                        {
                            score += 10;
                        }

                        if( status == SevereConditionID.SLP )
                        {
                            score += 10;
                        }
                    }
                }

            break;

            case ThreatType.Constraining:

                score += Mathf.FloorToInt( damageDealt * 60 );
                _ai.CurrentLog.Add( $"Flat damage dealt reward for general pressure, * 60({damageDealt * 60}). Score: {score}" );

                bool stabilized = top2.Attacker_EndOfTurnHP > 0f && top2.OpponentPTKO < PotentialToKO.Risky;
                bool failedStability = ( action.Type == ActionType.DefensiveSwitch || action.Type == ActionType.OffensiveSwitch ) && top2.OpponentPTKO >= PotentialToKO.Dangerous;

                if( stabilized )
                {
                    score += 20;
                    _ai.CurrentLog.Add( $"This action restores a relatively safe board state against the constraining threat. Score: {score}" );
                }

                if( failedStability )
                {
                    score -= Mathf.FloorToInt( 35 * sackModifier );
                    _ai.CurrentLog.Add( $"Switching results in failed stability or a potential sacrifice. Score: {score}" );
                }

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

                if( action.Type == ActionType.DefensiveSwitch && stabilized )
                {
                    score += 20;
                    _ai.CurrentLog.Add( $"Defensive switch fully stabilizes against constraining offensive pressure. Score: {score}" );
                }

                //--Trap/Forced sequence escape
                //--Speed
                if( !top1.AttackerMovedFirst && top2.AttackerMovedFirst )
                {
                    score += 20;
                    _ai.CurrentLog.Add( $"This action restores speed control against the constraining threat. Score: {score}" );
                }

                //--Pivot moves
                if( top1.Attacker.RoleProfile.Traits.Contains( RoleTrait.PivotMove ) )
                {
                    bool highConstraint = threat.ConstrainingPressure >= 4f;

                    score += highConstraint ? 20 : 10;
                    _ai.CurrentLog.Add( $"Attacker has a pivot move it can use to escape constraining pressure. Score: {score}" );

                    if( top1.Opponent.RoleProfile.Traits.Contains( RoleTrait.TrappingMove ) || top1.Opponent.RoleProfile.Traits.Contains( RoleTrait.ShadowTag ) )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Threat can trap and we can escape via pivot move. Score: {score}" );

                        if( action.Type == ActionType.Attack && _ai.UnitSim.MoveIsPivot( action.MovePayload ) && top1.Attacker.Bindings.Count > 0 )
                        {
                            score += 10;
                            _ai.CurrentLog.Add( $"We're considering a pivot move and we're actively trapped, we should push toward using it. Score: {score}" );
                        }
                    }
                }

                //--Phaze
                if( top1.Attacker.RoleProfile.Traits.Contains( RoleTrait.Phazes ) )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"We can potentially phaze this unit out. Score: {score}" );

                    if( action.Type == ActionType.OffensiveStatus && _ai.UnitSim.MoveIsPhaze( action.MovePayload ) && top1.Attacker_EndOfTurnHP > 0 )
                    {
                        score += 25;
                        _ai.CurrentLog.Add( $"We're actively considering phazing the target. This removes the current constriant pressure on us entirely, and we survive. Score: {score}" );
                    }
                }

                //--Forcing a Switch
                score += Mathf.FloorToInt( 35f * theySwitchProbability );

                //--Hazard factor
                if( action.Type == ActionType.OffensiveStatus && _ai.UnitSim.MoveIsEntryHazard( action.MovePayload ) && top1.Attacker_EndOfTurnHP > 0 )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"Setting hazards could increase general pressure against a constraining target. Score: {score}" );

                    if( theySwitchProbability >= 0.75f )
                    {
                        score += 20;
                        _ai.CurrentLog.Add( $"Constraining threat likely to switch ({theySwitchProbability}), setting hazards punishes the switch and provides chip damage down the line. Score: {score}" );
                    }
                }

                //--Severe Statuses
                if( action.Type == ActionType.OffensiveStatus )
                {
                    if( top1.Opponent.SevereStatus == SevereConditionID.None && top2.Opponent.SevereStatus != SevereConditionID.None )
                    {
                        score += 10;
                        var status = top2.Opponent.SevereStatus;
                        var biases = top1.Opponent.RoleProfile.Biases;
                        var traits = top1.Opponent.RoleProfile.Traits;

                        if( ( traits.Contains( RoleTrait.RecoveryMove ) || traits.Contains( RoleTrait.RecoveryItem ) ) && ( status == SevereConditionID.BRN || status == SevereConditionID.FBT || status == SevereConditionID.PSN || status == SevereConditionID.TOX ) )
                        {
                            score += 20;
                            _ai.CurrentLog.Add( $"Applying a damage over time severe status puts a constraining threat on a timer. Score: {score}" );

                            if( ( _ai.BattleSystem.BattleType == BattleType.TrainerSingles || _ai.BattleSystem.BattleType == BattleType.AI_Singles ) && status == SevereConditionID.TOX )
                            {
                                score += 10;
                                _ai.CurrentLog.Add( $"Toxic during singles is extremely effective and so it gets a bigger reward. Score: {score}" );
                            }
                        }

                        if( ( biases.Contains( RoleBias.MiddlingSpeed ) || biases.Contains( RoleBias.FastSpeed ) ) && status == SevereConditionID.PAR )
                        {
                            score += 10;
                            _ai.CurrentLog.Add( $"We paralyze a middling speed or fast speed tier mon, crippling their offensive presence and giving us speed control over them. Score: {score}" );
                        }

                        if( status == SevereConditionID.SLP )
                        {
                            score += 15;
                        }
                    }
                }

                //--Role Profile considerations
                if( top1.Opponent.RoleProfile.Traits.Contains( RoleTrait.WideMoveCoverage ) )
                {
                    if( action.Type == ActionType.DefensiveSwitch && top2.OpponentPTKO > PotentialToKO.Risky )
                    {
                        score += 15;
                        _ai.CurrentLog.Add( $"Defensively switching against a target with wide move coverage that we survive comfortably might be good. Score: {score}" );
                    }

                    if( action.Type == ActionType.OffensiveStatus && _ai.UnitSim.MoveIsPhaze( action.MovePayload ) )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Phazing a wide-coverage constraining threat is good, we reward phazing a little again here. Score: {score}" );
                    }

                    if( !top1.AttackerMovedFirst && top2.AttackerMovedFirst )
                    {
                        score += 15;
                        _ai.CurrentLog.Add( $"This action gains us speed control over the current constraining target. Score: {score}" );
                    }
                }

                if( top1.Opponent.RoleProfile.Biases.Contains( RoleBias.AttritionFocused ) )
                {
                    if( action.Type == ActionType.OffensiveStatus && _ai.UnitSim.MoveIsEntryHazard( action.MovePayload ) && top1.Attacker_EndOfTurnHP > 0 )
                    {
                        score += 10;
                    }

                    if( action.Type == ActionType.Setup && top1.Attacker_EndOfTurnHP > 0 )
                    {
                        score += 15;
                    }

                    if( action.Type == ActionType.OffensiveStatus )
                    {
                        score += 10;
                    }

                    if( action.Type == ActionType.Attack && _ai.UnitSim.MoveIsPivot( action.MovePayload ) )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Using a pivot move to switch against an attrition-focused constraint threat provides unique control over it. Score: {score}" );

                        if( top1.Attacker.RoleProfile.Traits.Contains( RoleTrait.FastPivot ) || top1.Attacker.RoleProfile.Traits.Contains( RoleTrait.SlowPivot ) )
                        {
                            score += 5;

                            if( top2.Attacker_EndOfTurnHP > 0 )
                            {
                                if( top2.AttackerMovedFirst || top2.AttackerPTKO >= PotentialToKO.Risky )
                                {
                                    score += 15;
                                }

                                if( !top1.AttackerMovedFirst && top1.Attacker.RoleProfile.Traits.Contains( RoleTrait.SlowPivot ) && top2.AttackerMovedFirst )
                                {
                                    score += 10;
                                }
                            }
                        }
                    }
                }

            break;

            case ThreatType.Escalating:

                score += Mathf.FloorToInt( damageDealt * 75 );
                _ai.CurrentLog.Add( $"Flat damage dealt reward on a threat that might setup, * 75( {damageDealt * 75}). Score: {score}" );

                if( top1.AttackerPTKO >= PotentialToKO.Risky )
                {
                    score += 20;
                    _ai.CurrentLog.Add( $"We threaten decent damage to the setup mon. Score: {score}" );

                    if( action.Type == ActionType.Attack )
                    {
                        if( threat.ThreatUnit.RoleProfile.PrimaryRole == RoleClass.SetupSweeper )
                        {
                            score += 10;
                            _ai.CurrentLog.Add( $"Target role is setup sweeper, pushing slightly to attack it. Score: {score}" );
                        }
                    }
                }

                if( top1.AttackerMovedFirst )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"We're faster than the setup threat. Score: {score}" );

                    if( action.Type == ActionType.Attack )
                    {
                        if( threat.ThreatUnit.RoleProfile.PrimaryRole == RoleClass.SetupSweeper )
                        {
                            score += 10;
                            _ai.CurrentLog.Add( $"Target role is setup sweeper, pushing slightly to attack it. Score: {score}" );
                        }
                    }
                }

                bool recoveryMove = threat.ThreatUnit.RoleProfile.Traits.Contains( RoleTrait.RecoveryMove );
                bool physicallyOffensiveSetup = threat.ThreatUnit.RoleProfile.Traits.Contains( RoleTrait.PhysicallyOffensiveSetup );
                bool speciallyOffensiveSetup = threat.ThreatUnit.RoleProfile.Traits.Contains( RoleTrait.SpeciallyOffensiveSetup );

                if( recoveryMove && ( physicallyOffensiveSetup || speciallyOffensiveSetup ) && action.Type == ActionType.Attack )
                {
                    score += 25;
                    _ai.CurrentLog.Add( $"Target is an escalating threat with recovery and setup moves, pushing slightly to attack it. Score: {score}" );
                    
                    if( top1.AttackerPTKO >= PotentialToKO.Risky )
                    {
                        score += 5;
                    }

                    if( top1.AttackerMovedFirst )
                    {
                        score += 5;
                    }

                    if( top2.AttackerPTKO >= PotentialToKO.Dangerous && top2.Attacker_EndOfTurnHP > 0 )
                    {
                        score += 10;
                    }
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

                //--Setup safety threshold
                if( damageDealt >= 0.5f )
                {
                    score += 25;
                }

                bool forcedRespect = top1.AttackerPTKO >= PotentialToKO.Risky || top2.AttackerPTKO >= PotentialToKO.Dangerous;
                if( forcedRespect )
                {
                    score += 20;
                    _ai.CurrentLog.Add( $"This action prevents the setup threat from freely escalating by forcing immediate respect. Score: {score}" );

                    if( top1.OpponentPTKO >= PotentialToKO.Risky && top1.Attacker_EndOfTurnHP > 0 )
                    {
                        score += 10;
                    }
                }

                if( action.Type == ActionType.OffensiveStatus )
                {
                    if( threat.ThreatUnit.RoleProfile.PrimaryRole == RoleClass.SetupSweeper )
                    {
                        score += 15;
                        _ai.CurrentLog.Add( $"Offensive status likely good against a setup sweeper. Score: {score}" );
                    }

                    if( action.MovePayload.MoveSO.Name == "Taunt" )
                    {
                        score += 25;
                        _ai.CurrentLog.Add( $"Taunt immediately shuts down setup users. Score: {score}" );

                        if( top1.AttackerMovedFirst )
                        {
                            score += 10;
                            _ai.CurrentLog.Add( $"We're a faster Taunt, pushing with small bonus. Score: {score}" );
                        }
                    }

                    if( action.MovePayload.MoveSO.Name == "Encore" )
                    {
                        score += 30;
                        _ai.CurrentLog.Add( $"Encore prevents setup users from utilizing their setup freely. Score: {score}" );

                        if( top2.AttackerMovedFirst )
                        {
                            score += 10;
                            _ai.CurrentLog.Add( $"We're a faster Encore next turn, pushing with small bonus so we can lock them into their setup move. Score: {score}" );
                        }
                    }

                    if( _ai.UnitSim.MoveIsPhaze( action.MovePayload ) && top1.Attacker_EndOfTurnHP > 0 )
                    {
                        score += 30;
                        _ai.CurrentLog.Add( $"Phazing moves hard-reset a setup mon. Score: {score}" );

                        if( threat.EscalatingPressure >= 4f )
                        {
                            score += 10;
                            _ai.CurrentLog.Add( $"Escalating pressure is high, pushing with a small bonus. Score: {score}" );
                        }
                    }

                    //--Severe Statuses
                    if( top1.Opponent.SevereStatus == SevereConditionID.None && top2.Opponent.SevereStatus != SevereConditionID.None )
                    {
                        score += 10;
                        var status = top2.Opponent.SevereStatus;
                        var biases = top1.Opponent.RoleProfile.Biases;

                        if( biases.Contains( RoleBias.Physical ) && status == SevereConditionID.BRN )
                        {
                            score += 25;
                        }

                        if( biases.Contains( RoleBias.Special ) && status == SevereConditionID.FBT )
                        {
                            score += 25;
                        }

                        if( status == SevereConditionID.PAR )
                        {
                            score += 20;
                        }

                        if( status == SevereConditionID.SLP )
                        {
                            score += 30;
                        }

                        if( threat.ThreatUnit.RoleProfile.PrimaryRole == RoleClass.SetupSweeper )
                        {
                            score += 10;
                            _ai.CurrentLog.Add( $"Target role is setup sweeper, increasing reward for applying offensive status to cripple it. Score: {score}" );
                        }
                    }
                }

                //--Handle Setup Races
                if( action.Type == ActionType.Setup && top1.OpponentCanAct )
                {
                    var ourProfile = top1.Attacker.RoleProfile;
                    var threatProfile = threat.ThreatUnit.RoleProfile;

                    bool weSetup_PhysicallyOffensive = ourProfile.Traits.Contains( RoleTrait.PhysicallyOffensiveSetup );
                    bool weSetup_SpeciallyOffensive = ourProfile.Traits.Contains( RoleTrait.SpeciallyOffensiveSetup );
                    bool weSetup_PhysicallyDefensive = ourProfile.Traits.Contains( RoleTrait.PhysicallyDefensiveSetup );
                    bool weSetup_SpeciallyDefensive = ourProfile.Traits.Contains( RoleTrait.SpeciallyDefensiveSetup );

                    bool theySetup_PhysicallyOffensive = threatProfile.Traits.Contains( RoleTrait.PhysicallyOffensiveSetup );
                    bool theySetup_SpeciallyOffensive = threatProfile.Traits.Contains( RoleTrait.SpeciallyOffensiveSetup );
                    bool theySetup_PhysicallyDefensive = threatProfile.Traits.Contains( RoleTrait.PhysicallyDefensiveSetup );
                    bool theySetup_SpeciallyDefensive = threatProfile.Traits.Contains( RoleTrait.SpeciallyDefensiveSetup );

                    bool weMovefirstNext = top2.AttackerMovedFirst;

                    bool ourMoveIsOffensivePlus2 = _ai.UnitSim.MoveIsOffensiveSetupPlus2( action.MovePayload );
                    bool weAreIronDefenseBodyPress = _ai.UnitSim.PokemonIsIronDefenseBodyPress( top1.Attacker.Pokemon );

                    if( ourMoveIsOffensivePlus2 && _ai.UnitSim.PokemonHasMove_OffensivePriority( top1.Attacker.Pokemon ) )
                    {
                        score += 15;

                        if( top2.AttackerMovedFirst )
                        {
                            score += 5;
                        }
                    }
                    else if( weAreIronDefenseBodyPress )
                    {
                        score += 15;

                        if( top2.AttackerMovedFirst )
                        {
                            score += 5;
                        }
                    }
                    else if( weSetup_PhysicallyOffensive && theySetup_SpeciallyDefensive || weSetup_SpeciallyOffensive && theySetup_PhysicallyDefensive )
                    {
                        score += 5;

                        if( weMovefirstNext )
                        {
                            score += 15;
                        }
                    }
                    else if( weSetup_PhysicallyDefensive && theySetup_PhysicallyOffensive || weSetup_SpeciallyDefensive && theySetup_SpeciallyOffensive )
                    {
                        score += 5;

                        if( weMovefirstNext )
                        {
                            score += 15;
                        }
                    }
                    else
                    {
                        score -= 20;
                        _ai.CurrentLog.Add( $"Disincentivizing setting up when the opponent also wants to setup. Score: {score}" );

                        if( threat.ThreatUnit.RoleProfile.PrimaryRole == RoleClass.SetupSweeper )
                        {
                            score -= 10;
                            _ai.CurrentLog.Add( $"Target role is setup sweeper, increasing penalty for setting up. Score: {score}" );
                        }
                    }
                }

                //--Reward Tempo Preservation!
                if( action.Type == ActionType.DefensiveSwitch && top2.OpponentCanAct )
                {
                    score -= 15;
                    _ai.CurrentLog.Add( $"Disincentivizing a passive, possibly read defensive switch against a mon that wants to setup. Score: {score}" );

                    if( threat.ThreatUnit.RoleProfile.PrimaryRole == RoleClass.SetupSweeper )
                    {
                        score -= 10;
                        _ai.CurrentLog.Add( $"Target role is setup sweeper, increasing penalty for defensive switching while threat is in escalation. Score: {score}" );
                    }
                }

                //--Delayed Failure against a setup mon. If choosing this action causes us to faint next turn, meaning they likely setup this turn, it may not be the right choice
                if( top1.Attacker_EndOfTurnHP > 0 && top2.Attacker_EndOfTurnHP <= 0 && top2.Opponent_EndOfTurnHP > 0 )
                {
                    score -= 25;
                    _ai.CurrentLog.Add( $"If choosing this action causes us to faint next turn, meaning they likely setup this turn, it may not be the right choice. Score: {score}" );
                }

                if( !top1.OpponentCanAct || !top2.OpponentCanAct )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"Flat reward for preventing the escalating threat from acting this turn or next turn. Score: {score}" );
                }

            break;

            case ThreatType.Persistent:

                bool recoveryTank = threat.ThreatUnit.RoleProfile.Traits.Contains( RoleTrait.RecoveryItem ) || threat.ThreatUnit.RoleProfile.Traits.Contains( RoleTrait.RecoveryMove ) || threat.ThreatUnit.RoleProfile.Traits.Contains( RoleTrait.RecoveryAbility );
                bool isAttritionFocused = threat.ThreatUnit.RoleProfile.Biases.Contains( RoleBias.AttritionFocused );
                bool passivePressure = threat.ThreatUnit.RoleProfile.Biases.Contains( RoleBias.PassivePressure );

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

                bool forcesRecovery = top1.Opponent_EndOfTurnHP <= 0.5f && top2.OpponentPTKO >= PotentialToKO.Risky && recoveryTank;
                if( forcesRecovery )
                {
                    score += 20;
                    _ai.CurrentLog.Add( $"We're likely to force the tank into an hp threshold that forces it to use a recovery move or switch. Score: {score}" );
                }

                bool recoveryLocked = forcesRecovery && top2.AttackerMovedFirst;
                if( recoveryLocked )
                {
                    score += 5;
                    _ai.CurrentLog.Add( $"Tiny flat global bonus for recovery locking the recovery tank. Score: {score}" );
                }

                if( top2.AttackerPTKO >= PotentialToKO.Risky )
                {
                    score += 15; //--Future breaking potential
                    _ai.CurrentLog.Add( $"We threaten good damage next round, or we improve our PTKO from current round into next round. This is good break potential. Score: {score}" );
                }

                if( top2.AttackerPTKO > top1.AttackerPTKO )
                {
                    score += 25;
                }

                if( action.Type == ActionType.Setup )
                {
                    if( isAttritionFocused )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"An attrition focused tank is worth setting up on. Score: {score}" );
                    }

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
                        _ai.CurrentLog.Add( $"Setting up on tanks is usually good. We may not survive or threaten significant damage, but still giving a small reward for the scenario. Score: {score}" );
                    }

                    var threatProfile = threat.ThreatUnit.RoleProfile;
                    bool tankHasSetupDisruptionMove = threatProfile.Traits.Contains( RoleTrait.Haze ) || threatProfile.Traits.Contains( RoleTrait.Encore ) || threatProfile.Traits.Contains( RoleTrait.Taunt ) || threatProfile.Traits.Contains( RoleTrait.Phazes );
                    bool tankIgnoresSetup = threat.ThreatUnit.Ability == AbilityID.Unaware;
                    bool tankCanStatus = threatProfile.Traits.Contains( RoleTrait.StatusSpreader );

                    if( tankIgnoresSetup )
                    {
                        score -= 10;
                    }

                    if( tankHasSetupDisruptionMove )
                    {
                        score -= 10;
                    }

                    if( tankCanStatus )
                    {
                        score -= 10;
                    }

                    if( recoveryLocked )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Threat is likely to be recovery locked, setting up should be safer than usual. Rewarding. Score: {score}" );
                    }
                }

                if( action.Type == ActionType.OffensiveStatus )
                {
                    if( passivePressure )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Flat reward for using an offensive status move on a passive tank. Score: {score}" );
                    }

                    if( !top1.OpponentCanAct || !top2.OpponentCanAct && top2.Attacker_EndOfTurnHP > 0 )
                    {
                        score += 25;
                        _ai.CurrentLog.Add( $"We prevent the tank from acting this round, or next round and we survive next round. Rewarding. Score: {score}" );
                    }

                    if( top1.Opponent.SevereStatus == SevereConditionID.None && top2.Opponent.SevereStatus != SevereConditionID.None )
                    {
                        score += 25;
                        _ai.CurrentLog.Add( $"We apply a status effect to the tank, likely crippling it or allowing for guaranteed residual chip damage. Score: {score}" );

                        bool appliedResidualStatus = top2.Opponent.SevereStatus != SevereConditionID.PAR && top2.Opponent.SevereStatus != SevereConditionID.SLP;
                        if( recoveryTank && appliedResidualStatus )
                        {
                            score += 20;
                            _ai.CurrentLog.Add( $"Applied a residual status to a recovery tank. Score: {score}" );

                            bool isToxic = top2.Opponent.SevereStatus == SevereConditionID.TOX;

                            if( isAttritionFocused )
                            {
                                score += 10;
                                _ai.CurrentLog.Add( $"Giving further residual damage bonus to an attrition focused tank. Score: {score}" );

                                if( isToxic )
                                {
                                    score += 10;
                                }
                            }

                            if( isToxic )
                            {
                                score += 5;
                            }
                        }

                        if( recoveryLocked )
                        {
                            score += 10;
                            _ai.CurrentLog.Add( $"Threat is likely to be recovery locked, taking advantage with severe status should be rewarded. Score: {score}" );
                        }

                        string moveName = action.MovePayload.MoveSO.Name;
                        bool recoveryItem = threat.ThreatUnit.Item == BattleItemEffectID.Leftovers || threat.ThreatUnit.Item == BattleItemEffectID.SitrusBerry;
                        if( recoveryTank && ( moveName == "Taunt" || moveName == "Encore" || moveName == "Heal Block" || moveName == "Knock Off" && recoveryItem || top2.Opponent.Bindings.Count > 0 && top2.Opponent.SevereStatus == SevereConditionID.TOX ) )
                        {
                            score += 10;
                            _ai.CurrentLog.Add( $"This action can shut down the tank's recovery line. Score: {score}" );

                            if( recoveryLocked )
                            {
                                score += 10;
                                _ai.CurrentLog.Add( $"Threat is likely to be recovery locked next turn, preventing that now is strong. Rewarding. Score: {score}" );
                            }
                        }
                    }

                    if( bc.BattlefieldState.EntryHazardsOn_TheirSide <= 0 && _ai.UnitSim.MoveIsEntryHazard( action.MovePayload ) && top1.Attacker_EndOfTurnHP > 0f )
                    {
                        score += 25;
                        _ai.CurrentLog.Add( $"We don't have hazards setup yet, and we survive the turn. We should take advantage of the tank and seize some field control. Score: {score}" );

                        if( recoveryTank )
                        {
                            score += 15;
                            _ai.CurrentLog.Add( $"Setting hazards when the other side has a recovery tank reduces the efficacy of that recovery down the line. Score: {score}" );
                        }
                    }
                }

                int defensiveSwitchChecks = 0;
                if( action.Type == ActionType.DefensiveSwitch )
                {    
                    var threatProfile = threat.ThreatUnit.RoleProfile;
                    var candidateAdapter = _ai.GetPokemonAs_Adapter( action.SwitchPayload );
                    var candidateIsWallBreaker = candidateAdapter.RoleProfile.PrimaryRole == RoleClass.WallBreaker || candidateAdapter.RoleProfile.SecondaryRoles.Contains( RoleClass.WallBreaker );

                    if( candidateIsWallBreaker || top2.AttackerPTKO >= PotentialToKO.Risky )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Defensively switching in a wall breaker into a wall is good. Score: {score}" );

                        if( ( threatProfile.Biases.Contains( RoleBias.PhysicallyBulky ) && candidateAdapter.RoleProfile.Biases.Contains( RoleBias.Special ) ) || ( threatProfile.Biases.Contains( RoleBias.SpeciallyBulky ) && candidateAdapter.RoleProfile.Biases.Contains( RoleBias.Physical ) ) )
                        {
                            score += 10;
                            _ai.CurrentLog.Add( $"Wall breaker is offensively aligned with the tank's weaker defensive stat. Score: {score}" );
                        }

                        defensiveSwitchChecks++;
                    }

                    if( top2.AttackerMovedFirst )
                    {
                        score += 5;
                        _ai.CurrentLog.Add( $"Defensive candidate moves first next turn. Score: {score}" );

                        defensiveSwitchChecks++;
                    }

                    if( candidateAdapter.RoleProfile.Traits.Contains( RoleTrait.HazardSetter ) || candidateAdapter.RoleProfile.Traits.Contains( RoleTrait.HazardRemover ) )
                    {
                        score += 5;
                        _ai.CurrentLog.Add( $"Defensive candidate can set or remove hazards. Score: {score}" );

                        defensiveSwitchChecks++;
                    }

                    if( candidateAdapter.RoleProfile.Traits.Contains( RoleTrait.Phazes ) || candidateAdapter.RoleProfile.Traits.Contains( RoleTrait.Taunt ) || candidateAdapter.RoleProfile.Traits.Contains( RoleTrait.Encore ) )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Defensive candidate can phaze or lock down via taunt or encore. Score: {score}" );

                        defensiveSwitchChecks++;
                    }

                    if( defensiveSwitchChecks <= 0 )
                    {
                        score -= 25;
                        _ai.CurrentLog.Add( $"Defensive switch candidate provides 0 anti-tank checks. Penalizing. Score: {score}" );
                    }
                    else
                    {
                        if( recoveryLocked )
                        {
                            score += 5;
                            _ai.CurrentLog.Add( $"Threat is likely to be recovery locked, switching should be safer than usual. Very small nudge. Score: {score}" );
                        }
                    }

                    if( defensiveSwitchChecks > 0 && passivePressure )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Defensive candidate has defensive checks and the target is providing passive pressure. Rewarding. Score: {score}" );
                    }

                }

                int offensiveSwitchChecks = 0;
                if( action.Type == ActionType.OffensiveSwitch )
                {                    
                    if( action.Top2.Attacker_EndOfTurnHP > 0 && action.Top2.AttackerPTKO >= PotentialToKO.Dangerous )
                    {
                        score += 25;
                        _ai.CurrentLog.Add( $"We survive switching in, survive next turn, and threaten big damage next turn. Score: {score}" );
                        offensiveSwitchChecks++;
                    }

                    if( passivePressure )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Offensive candidate may be likely to counter passive pressure. Rewarding. Score: {score}" );
                        offensiveSwitchChecks++;
                    }

                    if( offensiveSwitchChecks <= 0 )
                    {
                        score -= 20;
                        _ai.CurrentLog.Add( $"Offensively switching provides no real checks, penalizing. Score: {score}" );
                    }
                    else
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Offensively switching against a tank is likely a safe tempo grab. Score: {score}" );

                        if( recoveryLocked )
                        {
                            score += 5;
                            _ai.CurrentLog.Add( $"Threat is likely to be recovery locked, switching should be safer than usual. Very small nudge. Score: {score}" );
                        }
                    }
                }

                bool lockedDownPressure = threat.ConstrainingPressure >= 4f || threat.PersistentPressure >= 4f;

                if( lockedDownPressure )
                {
                    score -= 20;
                    _ai.CurrentLog.Add( $"Constraint Pressure {threat.ConstrainingPressure}, Persistent Pressure {threat.PersistentPressure}. Pressure locks us down. Score: {score}" );
                }

                //--No progress detection
                bool futureBreakProgress = top2.AttackerPTKO > top1.AttackerPTKO || top1.AttackerPTKO >= PotentialToKO.Risky || damageDealt >= 0.45f;
                bool statusApplied = top1.Opponent.SevereStatus == SevereConditionID.None && top2.Opponent.SevereStatus != SevereConditionID.None;
                bool hazardsSet = action.Type == ActionType.OffensiveStatus && _ai.UnitSim.MoveIsEntryHazard( action.MovePayload );
                bool settingUp = action.Type == ActionType.Setup;
                bool viableSwitch = offensiveSwitchChecks > 0 || defensiveSwitchChecks > 0;

                bool progressMade = futureBreakProgress || statusApplied || hazardsSet || settingUp || viableSwitch;

                if( !progressMade )
                {
                    score -= 20;
                    _ai.CurrentLog.Add( $"No progress is made against a persistent tank with this action. Penalizing. Score: {score}" );

                    if( lockedDownPressure )
                    {
                        score -= 10;
                        _ai.CurrentLog.Add( $"We're also locked down, further penalizing this no-progress action. Score: {score}" );
                    }
                }

            break;

            case ThreatType.Disruptive:

                var threatRP = threat.ThreatUnit.RoleProfile;
                var ourRP = top1.Attacker.RoleProfile;
                var us = top1.Attacker;
                var them = threat.ThreatUnit;
                var bfs = bc.BattlefieldState;

                //--Check their disruption information
                bool statusSpreader = threatRP.Traits.Contains( RoleTrait.StatusSpreader );
                bool hazardSetter = threatRP.Traits.Contains( RoleTrait.HazardSetter );
                bool phazerDisruptive = threatRP.Traits.Contains( RoleTrait.Phazes );
                bool pivoter = threatRP.Traits.Contains( RoleTrait.FastPivot ) || threatRP.Traits.Contains( RoleTrait.SlowPivot );
                bool disruptive = threatRP.Traits.Contains( RoleTrait.Taunt ) || threatRP.Traits.Contains( RoleTrait.Encore ) || phazerDisruptive;
                bool weForceReactivePlay = damageDealt >= 0.4f || top2.AttackerPTKO >= PotentialToKO.Risky;
                bool theyHaveRecoveryMove = threatRP.Traits.Contains( RoleTrait.RecoveryMove );
                bool theyAreSashed = threat.ThreatUnit.Item == BattleItemEffectID.FocusSash;
                bool activeDisruption = statusSpreader || disruptive || hazardSetter;

                //--Guaranteed Severe Status Application moves
                bool burner = _ai.UnitSim.CheckHasMove( them, "Will-O-Wisp" );
                bool froster = _ai.UnitSim.CheckHasMove( them, "Hoarfrost Spirit" );
                bool paralizer = _ai.UnitSim.CheckHasMove( them, "Thunder Wave" ) || _ai.UnitSim.CheckHasMove( them, "Nuzzle" ) || _ai.UnitSim.CheckHasMove( them, "Stun Spore" );
                bool sleeper = _ai.UnitSim.CheckHasMove( them, "Sleep Powder" ) || _ai.UnitSim.CheckHasMove( them, "Spore" ) || _ai.UnitSim.CheckHasMove( them, "Hypnosis" );
                bool poisoner = _ai.UnitSim.CheckHasMove( them, "Poison Powder" ) || _ai.UnitSim.CheckHasMove( them, "Mortal Spin" ) || _ai.UnitSim.CheckHasMove( them, "Poison Gas" ) || _ai.UnitSim.CheckHasMove( them, "Toxic Thread" );
                bool toxicer = _ai.UnitSim.CheckHasMove( them, "Toxic" );
                bool prankster = them.Ability == AbilityID.Prankster;
                bool powderer = _ai.UnitSim.CheckHasMove( them, "Sleep Powder" ) || _ai.UnitSim.CheckHasMove( them, "Spore" ) || _ai.UnitSim.CheckHasMove( them, "Poison Powder" ) || _ai.UnitSim.CheckHasMove( them, "Stun Spore" );
                bool taunter = threatRP.Traits.Contains( RoleTrait.Taunt );
                bool encorer = threatRP.Traits.Contains( RoleTrait.Encore );
                bool knockOff = _ai.UnitSim.CheckHasMove( them, "Knock Off" );

                //--Detect if we have disruption protection
                bool sub = us.VolatileStatuses.Contains( VolatileConditionID.Substitute );
                bool lum = us.Item == BattleItemEffectID.LumBerry;
                bool theyAreTaunted = them.VolatileStatuses.Contains( VolatileConditionID.Taunt );
                bool theyAreEncored = them.VolatileStatuses.Contains( VolatileConditionID.Encore );
                bool weHaveAPriorityAttack = _ai.UnitSim.PokemonHasMove_Priority( us.Pokemon );
                bool weAreFasterThisTurn = top1.AttackerMovedFirst;
                bool weAreFasterNextTurn = top2.AttackerMovedFirst;
                bool weForceRecovery = damageDealt >= 0.5f && theyHaveRecoveryMove;
                bool weForceRespect = top1.AttackerPTKO >= PotentialToKO.Risky || top2.AttackerPTKO >= PotentialToKO.Dangerous;
                bool weForceAnAttack = theyAreTaunted || weForceReactivePlay && !theyHaveRecoveryMove || weForceRespect && weAreFasterNextTurn;

                if( weForceReactivePlay || ( weForceRecovery && weForceRespect ) )
                {
                    score += 15;
                    _ai.CurrentLog.Add( $"We force a disruptive threat to have to make a reactive play. Score: {score}" );
                }

                if( weForceAnAttack )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"We force a disruptive threat to have to attack. Score: {score}" );
                }

                if( pivoter && weForceReactivePlay )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"We force a disruptive threat to potentially pivot this turn. Score: {score}" );
                }

                if( damageDealt > 0 && theyAreSashed )
                {
                    score += 5;
                    _ai.CurrentLog.Add( $"Breaking sash deserves a small reward. Score: {score}" );
                }

                if( top1.Opponent_EndOfTurnHP <= 0f )
                {
                    score += 25;
                    _ai.CurrentLog.Add( $"This action results in the disruptive threat fainting this turn. Big reward. Score: {score}" );
                }
                else if( top2.Attacker_EndOfTurnHP > 0 && top2.Opponent_EndOfTurnHP < 0 )
                {
                    score += 15;
                    _ai.CurrentLog.Add( $"This action results in the disruptive threat fainting next turn. moderate reward. Score: {score}" );
                }

                if( action.Type == ActionType.Attack && action.MovePayload.MoveSO.Name == "Fake Out" && _ai.CanUseFakeOut( us, them ) )
                {
                    score += 15;
                    _ai.CurrentLog.Add( $"Fake out is extremely useful against disruptive threats. Delaying them even one turn is worth the effort. Score: {score}" );

                    if( theyAreSashed )
                    {
                        score += 5;
                        _ai.CurrentLog.Add( $"Extra stacking bonus for using fake out to break a focus sash. Score: {score}" );
                    }
                }

                if( action.Type == ActionType.Setup )
                {
                    if( activeDisruption && !weForceRespect )
                    {
                        score -= 30;
                        _ai.CurrentLog.Add( $"Setting up against a disruptive threat with active disruption could cripple us. Score: {score}" );
                    }

                    if( sub )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"We're behind a sub, setting up is naturally safer. Score: {score}" );
                    }

                    if( statusSpreader && lum )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Our lum berry may cause them to waste a turn, letting us set up. Score: {score}" );
                    }

                    if( theyAreTaunted || theyAreEncored )
                    {
                        score += 15;
                        _ai.CurrentLog.Add( $"They are either taunted or unable to select a different move, likely forcing them to switch or otherwise allow us to setup on them safely. Score: {score}" );
                    }

                    if( weAreFasterThisTurn )
                    {
                        score += 5;
                        _ai.CurrentLog.Add( $"We're faster and so we're more likely to setup. Score: {score}" );
                    }

                    if( ( weHaveAPriorityAttack || weAreFasterNextTurn ) && weForceRespect )
                    {
                        score += 5;
                    }

                    if( _ai.UnitSim.MoveIsOffensiveSetupPlus2( action.MovePayload ) && weHaveAPriorityAttack && weAreFasterNextTurn )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"We're going for a +2 attack stat with priority, and we outspeed next turn. Score: {score}" );
                    }

                    if( weForceRecovery && ( weAreFasterNextTurn || weHaveAPriorityAttack ) )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"We may force them to use a recovery move and we're likely to outspeed them next turn. Score: {score}" );
                    }
                }

                //--Status Immunity Checks (Current mon & Defensive and Offensive switch candidates)
                //--Current Mon
                bool current_AbilityUsesStatus = us.Ability == AbilityID.Guts || us.Ability == AbilityID.MarvelScale;
                bool current_GroundVSTwave = _ai.UnitSim.CheckTypes( PokemonType.Ground, us ) && _ai.UnitSim.CheckHasMove( them, "Thunder Wave" );
                bool current_GrassVSStunSpore = _ai.UnitSim.CheckTypes( PokemonType.Grass, us ) && _ai.UnitSim.CheckHasMove( them, "Stun Spore" );
                bool current_GrassVSSporePowder = _ai.UnitSim.CheckTypes( PokemonType.Grass, us ) && ( _ai.UnitSim.CheckHasMove( them, "Sleep Powder" ) || _ai.UnitSim.CheckHasMove( them, "Spore" ) );

                bool current_PowderImmunity = _ai.UnitSim.CheckTypes( PokemonType.Grass, us );
                bool current_BrnImmunity = _ai.UnitSim.CheckTypes( PokemonType.Fire, us ) || current_AbilityUsesStatus || us.Ability == AbilityID.FlashFire || lum || sub;
                bool current_FbtImmunity = _ai.UnitSim.CheckTypes( PokemonType.Ice, us ) || current_AbilityUsesStatus || lum || sub;
                bool current_PsnToxImmunity = _ai.UnitSim.CheckTypes( PokemonType.Poison, us ) || _ai.UnitSim.CheckTypes( PokemonType.Steel, us ) || current_AbilityUsesStatus || us.Ability == AbilityID.PoisonHeal || lum || sub;
                bool current_ParImmunity = _ai.UnitSim.CheckTypes( PokemonType.Electric, us ) || current_GroundVSTwave || current_GrassVSStunSpore || sub;
                bool current_SlpImmunity = current_GrassVSSporePowder || us.Ability == AbilityID.Insomnia || us.Ability == AbilityID.VitalSpirit || sub;
                bool current_PhazeImmunity = sub;
                bool current_PranksterImmunity = _ai.UnitSim.CheckTypes( PokemonType.Dark, us );

                //--Switch Candidate Disruption Immunities
                int switchDisruptionChecks = 0;
                int currentMonDisruptionChecks = 0;
                if( action.Type == ActionType.DefensiveSwitch || action.Type == ActionType.OffensiveSwitch )
                {
                    //--Switch Candidate
                    var candidate = action.SwitchPayload;
                    var candidateAdapter = _ai.GetPokemonAs_Adapter( candidate );
                    var candidateRP = candidateAdapter.RoleProfile;
                    bool switchSub = candidateAdapter.VolatileStatuses.Contains( VolatileConditionID.Substitute );

                    bool switch_AbilityUsesStatus = candidateAdapter.Ability == AbilityID.Guts || candidateAdapter.Ability == AbilityID.MarvelScale;
                    bool switch_GroundVSTwave = _ai.UnitSim.CheckTypes( PokemonType.Ground, candidateAdapter ) && _ai.UnitSim.CheckHasMove( them, "Thunder Wave" );
                    bool switch_GrassVSStunSpore = _ai.UnitSim.CheckTypes( PokemonType.Grass, candidateAdapter ) && _ai.UnitSim.CheckHasMove( them, "Stun Spore" );
                    bool switch_GrassVSSporePowder = _ai.UnitSim.CheckTypes( PokemonType.Grass, candidateAdapter ) && ( _ai.UnitSim.CheckHasMove( them, "Sleep Powder" ) || _ai.UnitSim.CheckHasMove( them, "Spore" ) );

                    bool switch_PowderImmunity = _ai.UnitSim.CheckTypes( PokemonType.Grass, candidateAdapter );
                    bool switch_BrnImmunity = _ai.UnitSim.CheckTypes( PokemonType.Fire, candidateAdapter ) || switch_AbilityUsesStatus || candidateAdapter.Ability == AbilityID.FlashFire || lum || switchSub;
                    bool switch_FbtImmunity = _ai.UnitSim.CheckTypes( PokemonType.Ice, candidateAdapter ) || switch_AbilityUsesStatus || lum || switchSub;
                    bool switch_PsnToxImmunity = _ai.UnitSim.CheckTypes( PokemonType.Poison, candidateAdapter ) || _ai.UnitSim.CheckTypes( PokemonType.Steel, candidateAdapter ) || switch_AbilityUsesStatus || candidateAdapter.Ability == AbilityID.PoisonHeal || lum || switchSub;
                    bool switch_ParImmunity = _ai.UnitSim.CheckTypes( PokemonType.Electric, candidateAdapter ) || switch_GroundVSTwave || switch_GrassVSStunSpore || switchSub;
                    bool switch_SlpImmunity = switch_GrassVSSporePowder || candidateAdapter.Ability == AbilityID.Insomnia || candidateAdapter.Ability == AbilityID.VitalSpirit || switchSub;
                    bool switch_PranksterImmunity = _ai.UnitSim.CheckTypes( PokemonType.Dark, candidateAdapter );

                    if( burner && switch_BrnImmunity )
                        switchDisruptionChecks++;

                    if( froster && switch_FbtImmunity )
                        switchDisruptionChecks++;

                    if( poisoner && switch_PsnToxImmunity )
                        switchDisruptionChecks++;

                    if( toxicer && switch_PsnToxImmunity )
                        switchDisruptionChecks++;

                    if( paralizer && switch_ParImmunity )
                        switchDisruptionChecks++;

                    if( sleeper && switch_SlpImmunity )
                        switchDisruptionChecks++;

                    if( prankster && switch_PranksterImmunity )
                        switchDisruptionChecks++;

                    if( powderer && switch_PowderImmunity )
                        switchDisruptionChecks++;

                    if( hazardSetter && candidateAdapter.Ability == AbilityID.MagicBounce )
                    {
                        switchDisruptionChecks++;

                        if( bfs.EntryHazardsOn_MySide <= 0 && ( bfs.IsEarlyGame || bfs.Round < 7 ) )
                            score += 10;

                        if( bfs.EntryHazardsOn_TheirSide <= 0 && ( bfs.IsEarlyGame || bfs.Round < 7 ) )
                            score += 10;
                    }
                    else if( hazardSetter && candidateAdapter.RoleProfile.Traits.Contains( RoleTrait.HazardRemover ) )
                    {
                        switchDisruptionChecks++;

                        if( bfs.EntryHazardsOn_MySide > 0 )
                            score += 10;
                    }

                    if( candidateAdapter.RoleProfile.Traits.Contains( RoleTrait.Taunt ) || candidateAdapter.RoleProfile.Traits.Contains( RoleTrait.Encore ) || candidateAdapter.RoleProfile.Traits.Contains( RoleTrait.Phazes ) )
                        switchDisruptionChecks++;

                    if( switchDisruptionChecks >= 1 )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Switching provides 1 or more checks against incoming disruption, giving an extra bonus. Score: {score}" );
                    }

                    if( switchDisruptionChecks >= 3 )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Switching provides 3 or more checks against incoming disruption, giving an extra bonus. Score: {score}" );
                    }

                    if( switchDisruptionChecks >= 5 )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Switching provides 5 or more checks against incoming disruption, giving an extra bonus. Score: {score}" );
                    }

                    if( switchDisruptionChecks <= 0 )
                    {
                        score -= 30;
                        _ai.CurrentLog.Add( $"Switching provides no checks against disruption, flat penalty. Score: {score}" );
                    }

                    if( action.Top2.Attacker_EndOfTurnHP > 0 && action.Top2.AttackerPTKO >= PotentialToKO.Dangerous )
                    {
                        score += 25;
                        _ai.CurrentLog.Add( $"We survive next round and threaten big damage. Score: {score}" );

                        if( top2.AttackerMovedFirst )
                        {
                            score += 10;
                            _ai.CurrentLog.Add( $"Switch candidate also moves first next round. Score: {score}" );
                        }
                    }

                    //--Disruption Vulnerabilities
                    bool catastrophicBurn = burner && candidateRP.Biases.Contains( RoleBias.Physical );
                    bool catastrophicFrost = froster && candidateRP.Biases.Contains( RoleBias.Special );
                    bool catastrophicParalysis = paralizer && candidateRP.Traits.Contains( RoleTrait.FastPivot );
                    
                    bool hasCoreStatusMoves = candidateRP.Traits.Contains( RoleTrait.RecoveryMove ) || candidateRP.Traits.Contains( RoleTrait.HazardSetter ) || candidateRP.Traits.Contains( RoleTrait.StatusSpreader ) || candidateRP.PrimaryRole == RoleClass.SetupSweeper;

                    bool catastrophicTaunt = threatRP.Traits.Contains( RoleTrait.Taunt ) && hasCoreStatusMoves;
                    bool catastrophicEncore = threatRP.Traits.Contains( RoleTrait.Encore ) && hasCoreStatusMoves;

                    int vulnerabilities = 0;

                    if( catastrophicBurn )
                        vulnerabilities++;

                    if( catastrophicFrost )
                        vulnerabilities++;

                    if( catastrophicParalysis )
                        vulnerabilities++;

                    if( catastrophicTaunt )
                        vulnerabilities++;

                    if( catastrophicEncore )
                        vulnerabilities++;
                    
                    if( vulnerabilities >= 1 )
                    {
                        score -= 15;

                        if( sleeper || taunter || encorer || prankster || knockOff )
                            score -= 10;

                        if( vulnerabilities >= 3 )
                        score -= 15;
                    }
                }
                else
                {
                    //--Current Mon Disruption Immunities/Checks

                    if( burner && current_BrnImmunity )
                        currentMonDisruptionChecks++;

                    if( froster && current_FbtImmunity )
                        currentMonDisruptionChecks++;

                    if( poisoner && current_PsnToxImmunity )
                        currentMonDisruptionChecks++;

                    if( toxicer && current_PsnToxImmunity )
                        currentMonDisruptionChecks++;

                    if( paralizer && current_ParImmunity )
                        currentMonDisruptionChecks++;

                    if( sleeper && current_SlpImmunity )
                        currentMonDisruptionChecks++;

                    if( prankster && current_PranksterImmunity )
                        currentMonDisruptionChecks++;

                    if( powderer && current_PowderImmunity )
                        currentMonDisruptionChecks++;

                    if( hazardSetter && us.Ability == AbilityID.MagicBounce )
                    {
                        currentMonDisruptionChecks++;

                        if( bfs.EntryHazardsOn_MySide <= 0 && ( bfs.IsEarlyGame || bfs.Round < 7 ) )
                            score += 10;

                        if( bfs.EntryHazardsOn_TheirSide <= 0 && ( bfs.IsEarlyGame || bfs.Round < 7 ) )
                            score += 10;
                    }
                    else if( hazardSetter && us.RoleProfile.Traits.Contains( RoleTrait.HazardRemover ) )
                    {
                        currentMonDisruptionChecks++;

                        if( bfs.EntryHazardsOn_MySide > 0 )
                            score += 10;
                    }

                    if( phazerDisruptive && current_PhazeImmunity )
                        currentMonDisruptionChecks++;

                    if( us.RoleProfile.Traits.Contains( RoleTrait.Taunt ) || us.RoleProfile.Traits.Contains( RoleTrait.Encore ) || us.RoleProfile.Traits.Contains( RoleTrait.Phazes ) )
                    {
                        currentMonDisruptionChecks++;
                        score += 10;
                    }

                    if( currentMonDisruptionChecks >= 1 )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Current mon provides 1 or more checks against incoming disruption, giving an extra bonus. Score: {score}" );
                    }

                    if( currentMonDisruptionChecks >= 3 )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Current mon provides 3 or more checks against incoming disruption, giving an extra bonus. Score: {score}" );
                    }

                    if( currentMonDisruptionChecks >= 5 )
                    {
                        score += 10;
                        _ai.CurrentLog.Add( $"Current mon provides 5 or more checks against incoming disruption, giving an extra bonus. Score: {score}" );
                    }

                    if( currentMonDisruptionChecks <= 0 )
                    {
                        score -= 10;
                        _ai.CurrentLog.Add( $"Current mon provides no checks against disruption, flat penalty. Score: {score}" );
                    }

                    //--Disruption Vulnerabilities
                    bool catastrophicBurn = burner && ourRP.Biases.Contains( RoleBias.Physical );
                    bool catastrophicFrost = froster && ourRP.Biases.Contains( RoleBias.Special );
                    bool catastrophicParalysis = paralizer && ourRP.Traits.Contains( RoleTrait.FastPivot );
                    
                    bool tauntHurts = ourRP.Traits.Contains( RoleTrait.RecoveryMove ) || ourRP.Traits.Contains( RoleTrait.HazardSetter ) || ourRP.Traits.Contains( RoleTrait.StatusSpreader ) || ourRP.PrimaryRole == RoleClass.SetupSweeper;
                    bool actionIsStatusMove = action.Type == ActionType.OffensiveStatus && action.MovePayload.MoveSO.MoveCategory == MoveCategory.Status;

                    bool catastrophicTaunt = threatRP.Traits.Contains( RoleTrait.Taunt ) && ( tauntHurts || actionIsStatusMove );
                    bool catastrophicEncore = threatRP.Traits.Contains( RoleTrait.Encore ) && actionIsStatusMove;

                    int vulnerabilities = 0;

                    if( catastrophicBurn )
                        vulnerabilities++;

                    if( catastrophicFrost )
                        vulnerabilities++;

                    if( catastrophicParalysis )
                        vulnerabilities++;

                    if( catastrophicTaunt )
                        vulnerabilities++;

                    if( catastrophicEncore )
                        vulnerabilities++;
                    
                    if( vulnerabilities >= 1 )
                    {
                        score -= 15;
                        
                        if( sleeper || taunter || encorer || prankster || knockOff )
                            score -= 10;

                        if( vulnerabilities >= 3 )
                        score -= 15;
                    }
                }

                if( action.Type == ActionType.OffensiveStatus && !_ai.UnitSim.MoveIsEntryHazard( action.MovePayload ) )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"We could potentially cripple the utility threat. Score: {score}" );

                    string statusMoveName = action.MovePayload.MoveSO.Name;
                    bool moveIsPhaze = _ai.UnitSim.MoveIsPhaze( action.MovePayload );

                    if( statusMoveName == "Taunt" || statusMoveName == "Encore" || statusMoveName == "Disable" || moveIsPhaze )
                    {
                        score += 15;
                        _ai.CurrentLog.Add( $"We are looking to lock down or phaze out the disruptive threat. Score: {score}" );
                    }
                }

                //--General Pressure Amount
                if( threat.ConstrainingPressure >= 4f )
                {
                    score -= 30;
                    _ai.CurrentLog.Add( $"Constraint Pressure: {threat.ConstrainingPressure} > 2. Score: {score}" );
                }

                //--Dead Turn Check
                bool noProgress = damageDealt < 0.2f && !weForceReactivePlay && currentMonDisruptionChecks <= 0 && switchDisruptionChecks <= 0;
                if( noProgress )
                {
                    score -= 30;
                    _ai.CurrentLog.Add( $"This action makes no progress against a disruptive threat. Score: {score}" );
                }

            break;
        }

        //--------------------
        //--Universal Scores--
        //--------------------

        if( top1.Opponent_DiesBeforeActing )
        {
            score += 25; //--Outright removes threat
            _ai.CurrentLog.Add( $"Current simulation detects we out-right remove the threat. Score: {score}" );
        }

        if( action.Top2.AttackerMovedFirst && threat.OutspeedsCurrent )
        {
            score += 15;
            _ai.CurrentLog.Add( $"This action changes speed dynamic in our favor. Score: {score}" );
        }

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
                score += 25;
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

        //--Forced Line Check
        bool forcedLine = top1.OpponentCanAct && ( top2.OpponentPTKO < top1.OpponentPTKO || top2.Opponent_EndOfTurnHP < 0.5f || !top2.OpponentCanAct );
        if( forcedLine )
        {
            score += 10;
            _ai.CurrentLog.Add( $"This action creates a forced line for the opponent. Score: {score}" );
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
            ActionType.SupportiveStatus     => EvaluateBattlefieldFor_SupportiveStatus( action, boardContext ),
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

        bool isMidGame = bfs.IsMidGame;
        bool isLateGame = bfs.IsLateGame;

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
        _ai.CurrentLog.Add( $"[Attacker's Battlefield Context] Weather: {weatherContext}, Terrain: {terrainContext}, Trick Room: {trickRoomContext}. Total Context Score: {contextScore}. Score: {score}" );

        int oppWeatherContext = _ai.UnitSim.Get_WeatherContextScore( opponentMon );
        int oppTerrainContext = _ai.UnitSim.Get_TerrainContextScore( opponentMon );
        int oppTrickRoomContext = _ai.UnitSim.Get_TrickRoomContextScore( opponentMon );
        int oppContextScore = oppWeatherContext + oppTerrainContext + oppTrickRoomContext;

        score -= oppContextScore;
        _ai.CurrentLog.Add( $"[Opponent's Battlefield Context] Weather: {oppWeatherContext}, Terrain: {oppTerrainContext}, Trick Room: {oppTrickRoomContext}. Total Context Score: {oppContextScore}. Score: {score}" );

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
        _ai.CurrentLog.Add( $"[Switch Candidate's Battlefield Context] Weather: {weatherContext}, Terrain: {terrainContext}, Trick Room: {trickRoomContext}. Total Context Score: {contextScore}. Score: {score}" );

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

        MoveCategory oppMoveCat = top1.Opponent.MTR?.Move != null ? top1.Opponent.MTR.Move.MoveSO.MoveCategory : MoveCategory.Other;
        if( bfs.WeHave_Reflect && bfs.OurReflectDuration >= 2 && ( oppMoveCat == MoveCategory.Physical || oppMoveCat == MoveCategory.Other ) )
        {
            score += 5;
            _ai.CurrentLog.Add( $"We're protected on incoming by Reflect. Score {score}" );
        }

        if( bfs.WeHave_LightScreen && bfs.OurLightScreenDuration >= 2 && ( oppMoveCat == MoveCategory.Special || oppMoveCat == MoveCategory.Other ) )
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

        if( bfs.IsMidGame && top2.AttackerPTKO >= PotentialToKO.Dangerous  )
        {
            score += 15;
            _ai.CurrentLog.Add( $"It's mid game and we threaten powerful offense next turn. Giving a slight boost for mid game phase tempo grab. Score: {score}" );
        }

        if( bfs.IsLateGame && top2.AttackerPTKO != PotentialToKO.OHKO )
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
        _ai.CurrentLog.Add( $"[Switch Candidate's Battlefield Context] Weather: {weatherContext}, Terrain: {terrainContext}, Trick Room: {trickRoomContext}. Total Context Score: {contextScore}. Score: {score}" );

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

        MoveCategory oppMoveCat = top1.Opponent.MTR?.Move != null ? top1.Opponent.MTR.Move.MoveSO.MoveCategory : MoveCategory.Other;
        if( bfs.WeHave_Reflect && bfs.OurReflectDuration >= 2 && ( oppMoveCat == MoveCategory.Physical || oppMoveCat == MoveCategory.Other ) )
        {
            score += 5;
            _ai.CurrentLog.Add( $"We're protected on incoming by Reflect. Score {score}" );
        }

        if( bfs.WeHave_LightScreen && bfs.OurLightScreenDuration >= 2 && ( oppMoveCat == MoveCategory.Special || oppMoveCat == MoveCategory.Other ) )
        {
            score += 5;
            _ai.CurrentLog.Add( $"We're protected on incoming by Light Screen. Score {score}" );
        }

        if( bfs.WeHave_AuroraVeil && bfs.OurAuroraVeilDuration >= 2 )
        {
            score += 10;
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
        _ai.CurrentLog.Add( $"==========[Evaluating Battlefield for Setup]==========" );
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
        _ai.CurrentLog.Add( $"[Attacker's Setup Battlefield Context] Weather: {weatherContext}, Terrain: {terrainContext}, Trick Room: {trickRoomContext}. Total Context Score: {contextScore}. Score: {score}" );

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

        bool isMidGame = bfs.IsMidGame;
        bool isLateGame = bfs.IsLateGame;

        var attackerMon = top1.Attacker.Pokemon;
        var opponentMon = top1.Opponent.Pokemon;

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"======================================================" );
        _ai.CurrentLog.Add( $"====[Evaluating Battlefield for Offensive Status]=====" );
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
        _ai.CurrentLog.Add( $"[Attacker's Offensive Status Battlefield Context] Weather: {weatherContext}, Terrain: {terrainContext}, Trick Room: {trickRoomContext}. Total Context Score: {contextScore}. Score: {score}" );

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

    private int EvaluateBattlefieldFor_SupportiveStatus( ActionEvaluation action, BoardContext boardContext )
    {
        int score = 0;

        var bfs = boardContext.BattlefieldState;
        var top1 = action.Top1;
        var top2 = action.Top2;

        bool isEarlyGame = bfs.IsEarlyGame;
        bool isMidGame = bfs.IsMidGame;
        bool isLateGame = bfs.IsLateGame;

        var attackerMon = top1.Attacker.Pokemon;
        var opponentMon = top1.Opponent.Pokemon;

        StatusThreatResult str = (StatusThreatResult)action.ActionResult; //--How have i not done it like this before now lol --07/06/26

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"======================================================" );
        _ai.CurrentLog.Add( $"====[Evaluating Battlefield for Supportive Status]====" );
        _ai.CurrentLog.Add( $"======================================================" );
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"Supportive Status Type: {str.SupportiveStatusType}" );
        _ai.CurrentLog.Add( $"" );

        int weatherContext = _ai.UnitSim.Get_WeatherContextScore( attackerMon );
        int terrainContext = _ai.UnitSim.Get_TerrainContextScore( attackerMon );
        int trickRoomContext = _ai.UnitSim.Get_TrickRoomContextScore( attackerMon );
        int contextScore = weatherContext + terrainContext + trickRoomContext;

        int oppWeatherContext = _ai.UnitSim.Get_WeatherContextScore( opponentMon );
        int oppTerrainContext = _ai.UnitSim.Get_TerrainContextScore( opponentMon );
        int oppTrickRoomContext = _ai.UnitSim.Get_TrickRoomContextScore( opponentMon );
        int oppContextScore = oppWeatherContext + oppTerrainContext + oppTrickRoomContext;

        if( str.SupportiveStatusType == SupportiveStatusType.Recovery )
        {
            //--Field Control Nuance
            if( bfs.WeHave_FieldControl )
            {
                score += 15;
                _ai.CurrentLog.Add( $"We have field control, recovering is reasonable. Score: {score}" );
            }
            else if( bfs.TheyHave_FieldControl )
            {
                score -= 10;
                _ai.CurrentLog.Add( $"They have field control, recovering could be scary. Score: {score}" );
            }

            //--Game Phase Nuance
            if( isEarlyGame )
            {
                score += 5;
                _ai.CurrentLog.Add( $"It's early game, recovery could provide an early tempo swing. Score: {score}" );
            }
            else if( isMidGame )
            {
                score += 10;
                _ai.CurrentLog.Add( $"It's mid game, recovery is likely necessary and could swing the mid game in our favor. Score: {score}" );
            }
            else if( isLateGame )
            {
                score -= 5;
                _ai.CurrentLog.Add( $"It's late game, we may not benefit from recovering anymore. Score: {score}" );

            }

            //--Battlefield Context
            score += contextScore;
            _ai.CurrentLog.Add( $"[Attacker's Battlefield Context] Weather: {weatherContext}, Terrain: {terrainContext}, Trick Room: {trickRoomContext}. Total Context Score: {contextScore}. Score: {score}" );

            score -= oppContextScore / 2;
            _ai.CurrentLog.Add( $"[Opponent's Battlefield Context] Weather: {oppWeatherContext}, Terrain: {oppTerrainContext}, Trick Room: {oppTrickRoomContext}. Total Context Score: {oppContextScore}. Score: {score}" );
        }
        else if( str.SupportiveStatusType == SupportiveStatusType.ForceMultiplier )
        {
            //--Field Control Nuance
            if( bfs.WeHave_FieldControl )
            {
                score += 20;
                _ai.CurrentLog.Add( $"We have field control, we have an advantage in forcing more multipliers for ourselves. Score: {score}" );
            }
            else if( bfs.TheyHave_FieldControl )
            {
                score += 5;
                _ai.CurrentLog.Add( $"They have field control, forcing a multiplier could swing control in our favor. Score: {score}" );
            }

            //--Game Phase Nuance
            if( isEarlyGame )
            {
                score += 10;
                _ai.CurrentLog.Add( $"It's early game, forcing a multiplier will gives a strong start. Score: {score}" );
            }
            else if( isMidGame )
            {
                score += 5;
                _ai.CurrentLog.Add( $"It's mid game, forcing a multiplier could swing the mid game in our favor. Score: {score}" );
            }
            else if( isLateGame )
            {
                score -= 10;
                _ai.CurrentLog.Add( $"It's late game, we may not benefit from multipliers anymore. Score: {score}" );

            }

            //--Battlefield Context
            score += contextScore * 2;
            _ai.CurrentLog.Add( $"[Attacker's Battlefield Context] Weather: {weatherContext}, Terrain: {terrainContext}, Trick Room: {trickRoomContext}. Total Context Score: {contextScore}. Score: {score}" );

            if( oppContextScore > contextScore )
            {
                score += 10;
                _ai.CurrentLog.Add( $"We're behind in battlefield context, so adding multipliers will help balance us out. Score: {score}" );
            }
            else
            {
                score += 5;
                _ai.CurrentLog.Add( $"We're ahead in battlefield context, so adding multipliers will give us an extra edge. Score: {score}" );
            }
        }
        else if( str.SupportiveStatusType == SupportiveStatusType.BattlefieldControl )
        {
            //--Field Control Nuance
            if( bfs.WeHave_FieldControl )
            {
                score += 5;
                _ai.CurrentLog.Add( $"We have field control, more battlefield control may not be necessary. Score: {score}" );
            }
            else if( bfs.TheyHave_FieldControl )
            {
                score += 15;
                _ai.CurrentLog.Add( $"They have field control, battlefield control could match theirs or swing it in our favor. Score: {score}" );
            }

            //--Game Phase Nuance
            if( isEarlyGame )
            {
                score += 20;
                _ai.CurrentLog.Add( $"It's early game, taking control of the battlefield will gives a strong start. Score: {score}" );
            }
            else if( isMidGame )
            {
                score += 15;
                _ai.CurrentLog.Add( $"It's mid game, trying to take control of the battlefield may swing the mid game in our favor. Score: {score}" );
            }
            else if( isLateGame )
            {
                score += 10;
                _ai.CurrentLog.Add( $"It's late game, a last minute battlefield control grab may be exactly what we need to win. Score: {score}" );

            }

            //--Battlefield Context
            score += contextScore;
            _ai.CurrentLog.Add( $"[Attacker's Battlefield Context] Weather: {weatherContext}, Terrain: {terrainContext}, Trick Room: {trickRoomContext}. Total Context Score: {contextScore}. Score: {score}" );

            if( oppContextScore > contextScore )
            {
                score += 10;
                _ai.CurrentLog.Add( $"We're behind in battlefield context, so we should absolutely try to change that in our favor. Score: {score}" );
            }
            else
            {
                score += 5;
                _ai.CurrentLog.Add( $"We're ahead in battlefield context, so we should absolutely try to change that in our favor. Score: {score}" );
            }
        }
        else if( str.SupportiveStatusType == SupportiveStatusType.AllyProtection )
        {
            //--Field Control Nuance
            if( bfs.WeHave_FieldControl )
            {
                score += 15;
                _ai.CurrentLog.Add( $"We have field control, protecting our ally keeps their advantage in it. Score: {score}" );
            }
            else if( bfs.TheyHave_FieldControl )
            {
                score += 5;
                _ai.CurrentLog.Add( $"They have field control, protecting our ally should limit our opponent's field advantage. Score: {score}" );
            }

            //--Game Phase Nuance
            score += 5;
            _ai.CurrentLog.Add( $"Protecting our ally is always valuable regardless of game phase. Score: {score}" );

            //--Battlefield Context
            score += contextScore;
            _ai.CurrentLog.Add( $"[Attacker's Battlefield Context] Weather: {weatherContext}, Terrain: {terrainContext}, Trick Room: {trickRoomContext}. Total Context Score: {contextScore}. Score: {score}" );

            if( oppContextScore > contextScore )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Being behind in battlefield context means we should definitely protect our ally. Score: {score}" );
            }
            else
            {
                score += 5;
                _ai.CurrentLog.Add( $"We're ahead in battlefield context, protecting our ally simply continues their advantage. Score: {score}" );
            }
        }

        //--Battlefield Effects providing contextual advantage or disadvantages
        if( bfs.WeHave_Reflect )
        {
            score += 5;
            _ai.CurrentLog.Add( $"We have reflect. Score: {score}" );

            if( top1.Opponent.MTR?.Move.MoveSO.MoveCategory == MoveCategory.Physical )
            {
                score += 5;
                _ai.CurrentLog.Add( $"And we think our opponent is looking to use a physical move. Score: {score}" );
            }
        }

        if( bfs.WeHave_LightScreen )
        {
            score += 5;
            _ai.CurrentLog.Add( $"We have light screen. Score: {score}" );

            if( top1.Opponent.MTR?.Move.MoveSO.MoveCategory == MoveCategory.Special )
            {
                score += 5;
                _ai.CurrentLog.Add( $"And we think our opponent is looking to use a special move. Score: {score}" );
            }
        }

        if( bfs.WeHave_AuroraVeil )
        {
            score += 10;
            _ai.CurrentLog.Add( $"We have aurora veil. Score: {score}" );
        }

        if( bfs.WeHave_Tailwind && !bfs.TheyHave_Tailwind )
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
            if( bfs.TheirTailwindDuration <= 1 )
            {
                score += 5;
                _ai.CurrentLog.Add( $"They have tailwind up and it's about to expire!. Score: {score}" );

                if( bfs.WeHave_Tailwind && bfs.OurTailwindDuration >= 2 )
                {
                    score += 5;
                    _ai.CurrentLog.Add( $"We also have tailwind up and it was staggered, giving us a speed advantage after the opponent's tailwind ends!. Score: {score}" );
                }
            }
            else if( !bfs.WeHave_Tailwind )
            {
                score -= bfs.TheirTailwindDuration * 2;
                _ai.CurrentLog.Add( $"They have tailwind up. Score: {score}" );
            }
        }

        if( bfs.TrickRoomActive && trickRoomContext <= 0 && bfs.TrickRoomDuration <= 1 )
        {
            score += 5;
            _ai.CurrentLog.Add( $"Trick Room is up and we don't benefit, and it's about to go down next turn!. Score: {score}" );
        }
        else if( bfs.TrickRoomActive && trickRoomContext > 0 )
        {
            score += bfs.TrickRoomDuration * 2;
            _ai.CurrentLog.Add( $"Trick Room is up and we benefit from it. Score: {score}" );
        }

        //--Simulation Context
        //--Opponent's ability to KO us
        if( top1.OpponentPTKO > top2.OpponentPTKO )
        {
            score += 5;
            _ai.CurrentLog.Add( $"This action reduces the opponent's potential to KO us next turn. Score: {score}" );
        }
        else if( top1.OpponentPTKO <= top2.OpponentPTKO )
        {
            score -= 10;
            _ai.CurrentLog.Add( $"This action doesn't change the opponent's potential to KO us next turn, or makes it worse. Score: {score}" );
        }

        //--Our ability to KO opponent
        if( top1.AttackerPTKO < top2.AttackerPTKO )
        {
            score += 5;
        }

        //--Speed gain
        if( !top1.AttackerMovedFirst && top2.AttackerMovedFirst )
        {
            score += 10;
        }

        //--Checks against target's ally will go here
        //--potential to be ko'd
        //--potential to ko
        //--speed changes

        //--Ally checks will go here
        //--Ally's potential to be KO'd
        //--Ally's potential to KO
        //--Ally's speed changes

        //--Ally's potential to be KO'd by target ally
        //--Ally's potential to KO target ally
        //--Ally's speed changes compared to target ally

        return score;
    }
}
