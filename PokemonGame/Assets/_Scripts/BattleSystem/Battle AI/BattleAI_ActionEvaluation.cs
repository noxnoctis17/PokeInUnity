using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ActionType { None, Any, Attack, OffensiveSwitch, DefensiveSwitch, Setup, OffensiveStatus, SupportiveStatus, Protect }
public class BattleAI_ActionEvaluation
{
    private BattleAI _ai;

    public BattleAI_ActionEvaluation( BattleAI ai )
    {
        _ai = ai;
    }

    public ActionEvaluation BuildActionEvaluation( ActionType type, IActionResult actionResult, List<IBattleAIUnit> targets, List<BattleUnit> targetBattleUnits, object payload, TurnOutcomeProjection top1, TurnOutcomeProjection top2, ExchangePack exchangePack )
    {
        ActionEvaluation eval = new()
        {
            Type = type,
            ActionResult = actionResult,
            Score = 0,
            Top1 = top1,
            Top2 = top2,
            ExchangePack = exchangePack,
            Actor = top1.Attacker.Pokemon,
        };

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"================================================================" );
        _ai.CurrentLog.Add( $"Building Action Evaluation for {eval.Type}..." );
        _ai.CurrentLog.Add( $"" );

        List<BattleUnit> finalTargets = null;
        if( targets != null )
        {
            // targetUnit = _ai.GetBattleUnit( target.Pokemon ); //--It's possible that targets are coming back wrong here for attacks? -- yes, yes they are. we're somehow getting targets passed into this function that aren't even on the field... --5/2/26 @ 2:20am
            finalTargets = targetBattleUnits;
            foreach( var target in targets )
                _ai.CurrentLog.Add( $"Intended Target: {target.Name}" );

            foreach( var target in finalTargets )
                _ai.CurrentLog.Add( $"Battle Unit Pokemon: {target.Pokemon.NickName}" );
        }
        else if( type != ActionType.OffensiveSwitch && type != ActionType.DefensiveSwitch )
            Debug.LogError( $"Target is null for a move action!" );

        switch( type )
        {
            case ActionType.Attack: //--and--//
            case ActionType.Setup:
            case ActionType.OffensiveStatus:
            case ActionType.SupportiveStatus:
            
                eval.Targets = finalTargets;
                _ai.CurrentLog.Add( $"" );

                foreach( var target in targets )
                    _ai.CurrentLog.Add( $"Attack's Target: (passed) {target.Name}" );
                
                eval.MovePayload = (Move)payload;
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
        var top2 = eval.Top2;

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
            bool opponentChipsSelf = ( _ai.UnitSim.CheckHasRecoilMove( top.Opponent.ActiveMoves ) || top.Opponent.Item == ItemBattleEffectID.LifeOrb ) && !top.AttackerMovedFirst;

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

        bool weKOThem = top2.Opponent.EndHPR <= 0f;
        bool weDie = top2.Attacker.EndHPR <= 0f;

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

        bool weMaintainPressure = top2.AttackerPTKO >= PotentialToKO.TwoHKO;
        bool theyThreatenUs = top2.OpponentPTKO >= PotentialToKO.Dangerous && !top2.AttackerMovedFirst;

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
        float damageTakenRaw = top.Attacker.EndHPR - top2.Attacker_EndOfTurnHP;
        float damageTaken = NormalizeDamage( damageTakenRaw, top.Attacker.EndHPR );
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
        float oppHPLossRaw = top.Opponent_EndOfTurnHP - top2.Opponent_EndOfTurnHP;
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
        float weAreForcedOutProb = _ai.UnitSim.PredictSwitchProbability( top2.Attacker.Pokemon, top2.OpponentPTKO, top2.AttackerPTKO, top2.AttackerMovedFirst, top.Opponent_EndOfTurnHP, top.Attacker_EndOfTurnHP, top2.Attacker.Expendability );
        float theyAreForcedOutProb = _ai.UnitSim.PredictSwitchProbability( top2.Opponent.Pokemon, top2.AttackerPTKO, top2.OpponentPTKO, top2.AttackerMovedFirst, top.Attacker_EndOfTurnHP, top.Opponent_EndOfTurnHP, top2.Opponent.Expendability );

        score += Mathf.FloorToInt( 25f * weAreForcedOutProb );
        _ai.CurrentLog.Add( $"We switch probability: {weAreForcedOutProb}. Score: {score}" );

        score -= Mathf.FloorToInt( 30f * theyAreForcedOutProb );
        _ai.CurrentLog.Add( $"They switch probability: {theyAreForcedOutProb}. Score: {score}" );

        eval.NextTurn_WeAreForcedOut = weAreForcedOutProb >= 0.7f;
        eval.NextTurn_TheyAreForcedOut = theyAreForcedOutProb >= 0.7f;

        eval.Score += score;
        _ai.CurrentLog.Add( $"Evaluate Attack Simulation Score: {score}" );
        _ai.CurrentLog.Add( $"Current Attack Decision Score: {eval.Score}" );
        return eval;
    }

    private ActionEvaluation EvaluateDefensiveSwitchSim( ActionEvaluation eval )
    {
        int score = 0;
        var top = eval.Top1;
        var top2 = eval.Top2;

        _ai.CurrentLog.Add( $"==============================================" );
        _ai.CurrentLog.Add( $"===[Evaluating Defensive Switch Simulation]===" );
        _ai.CurrentLog.Add( $"==============================================" );
        _ai.CurrentLog.Add( $"Our PTKO {top.AttackerPTKO} with Move: {top.Attacker.MTR?.Move?.MoveSO.Name}" );
        _ai.CurrentLog.Add( $"Their PTKO {top.OpponentPTKO} with Move: {top.Opponent.MTR?.Move?.MoveSO.Name}" );
        _ai.CurrentLog.Add( $"" );
        // _ai.CurrentLog.Add( $"" );
        // _ai.CurrentLog.Add( $"" );
        // _ai.CurrentLog.Add( $"==[TOP 1 Sim Log]===" );
        // _ai.CurrentLog.Add( top.SimulationLog );
        // _ai.CurrentLog.Add( $"" );
        // _ai.CurrentLog.Add( $"" );
        // _ai.CurrentLog.Add( $"" );
        // _ai.CurrentLog.Add( $"==[TOP 2 Sim Log]===" );
        // _ai.CurrentLog.Add( top2.SimulationLog );

        //--Switched mon dies on entry
        if( top.Attacker_EndOfTurnHP <= 0f )
        {
            score = -999;
            // eval.Score = score;
            _ai.CurrentLog.Add( $"Switch in ({top.Attacker.Name}) faints on switch in (B:{top.Attacker.BeginningHPR}, E:{top.Attacker.EndHPR}, EoT:{top.Attacker_EndOfTurnHP})! Score: {score}" );
            // return eval;
        }
        //--Critically low after entry. Will have to be careful here, end game switching might be more heavily penalized, which is somewhat reasonable.
        else if( top.Attacker_EndOfTurnHP <= 0.2f )
        {
            score -= 30;
            _ai.CurrentLog.Add( $"Switch in ({top.Attacker.Name}) (B:{top.Attacker.BeginningHPR}, E:{top.Attacker.EndHPR}, EoT:{top.Attacker_EndOfTurnHP}) takes big damage on entry, leaving it at {top.Attacker_EndOfTurnHP} HP on switch in! Score: {score}" );
        }

        //--Risky survival push
        var ee = eval.ExchangePack.UsVS_Threat;
        bool weMightSurvive = ee.OpponentPTKOR.PTKO != PotentialToKO.OHKO && ee.OpponentPTKOR.PTKO >= PotentialToKO.Risky;
        bool weFaintInEval = !ee.AttackerSurvives;

        if( weMightSurvive && weFaintInEval )
        {
            int comebackPotential = 0;

            bool opponentSelfDebuffs = _ai.UnitSim.CheckHasSelfDebuffMove( top.Opponent.ActiveMoves ) && !top.AttackerMovedFirst;
            bool opponentChipsSelf = ( _ai.UnitSim.CheckHasRecoilMove( top.Opponent.ActiveMoves ) || top.Opponent.Item == ItemBattleEffectID.LifeOrb ) && !top.AttackerMovedFirst;

            if( ee.AttackerThreatensKO )
                comebackPotential += 2;

            if( opponentSelfDebuffs )
                comebackPotential += 2;

            if( opponentChipsSelf )
                comebackPotential += 2;

            score -= comebackPotential * 10;
        }

        //--------------
        //--Look Ahead--
        //--------------

        //--First we compare threat
        bool weDie = top2.Attacker_DiesBeforeActing || top2.Attacker_EndOfTurnHP <= 0f;
        bool weKOThem = top2.Opponent_DiesBeforeActing || top2.Opponent_EndOfTurnHP <= 0f;

        bool theyThreatenUs = top2.OpponentPTKO >= PotentialToKO.Dangerous && !top2.AttackerMovedFirst;
        bool weThreatenThem = top2.AttackerPTKO >= PotentialToKO.TwoHKO && top2.AttackerMovedFirst;

        bool weCantThreatenBack = top2.AttackerPTKO >= PotentialToKO.TwoHKO && !top2.AttackerMovedFirst;

        float weAreForcedOut = _ai.UnitSim.PredictSwitchProbability( top2.Attacker.Pokemon, top2.OpponentPTKO, top2.AttackerPTKO, top2.AttackerMovedFirst, top.Opponent_EndOfTurnHP, top.Attacker_EndOfTurnHP, top2.Attacker.Expendability );
        float theyAreForcedOut = _ai.UnitSim.PredictSwitchProbability( top2.Opponent.Pokemon, top2.AttackerPTKO, top2.OpponentPTKO, top2.AttackerMovedFirst, top.Attacker_EndOfTurnHP, top.Opponent_EndOfTurnHP, top2.Opponent.Expendability );

        if( weDie )
        {
            score -= 60;
        }
        else if( theyThreatenUs )
        {
            score -= 45;
        }

        //--Reward tanks for taking very little damage the turn after switching in.
        float damageTakenRaw = top.Attacker.EndHPR - top2.Attacker_EndOfTurnHP;
        float damageTaken = NormalizeDamage( damageTakenRaw, top.Attacker.EndHPR );
        if( damageTaken >= 0.6f )           score -= 30;
        else if( damageTaken >= 0.4f )      score -= 15;
        else if( damageTaken <= 0.15f )     score += 50;
        else if( damageTaken <= 0.3f )      score += 25;

        //--Reward doing acceptable chip.
        float oppHPLossRaw = top.Opponent_EndOfTurnHP - top2.Opponent_EndOfTurnHP;
        float oppHPLoss = NormalizeDamage( oppHPLossRaw, top.Opponent_EndOfTurnHP );
        if( oppHPLoss >= 0.3f )             score += 25;
        else if( oppHPLoss >= 0.15f )       score += 10;

        // if( weCantThreatenBack )
        // {
        //     score -= Mathf.FloorToInt( 60f * weAreForcedOut);
        //     _ai.CurrentLog.Add( $"Switch creates unstable position (forced out next turn)! Score: {score}" );
        // }

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

        eval.Score += score;
        _ai.CurrentLog.Add( $"Evaluate Defensive Switch Simulation Score: {score}" );
        _ai.CurrentLog.Add( $"Current Defensive Switch Decision Score: {eval.Score}" );
        return eval;
    }

    private ActionEvaluation EvaluateOffensiveSwitchSim( ActionEvaluation eval )
    {
        int score = 0;
        var top = eval.Top1;
        var top2 = eval.Top2;

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

        //----------------------
        //--Look Ahead Section--
        //----------------------

        bool weKOThem = top2.Opponent_DiesBeforeActing || top2.Opponent_EndOfTurnHP <= 0f;
        if( weKOThem )
            score += 60;

        bool weThreaten = top2.AttackerPTKO >= PotentialToKO.Dangerous;
        if( weThreaten )
            score += 35;

        float theyAreForcedOut = _ai.UnitSim.PredictSwitchProbability( top2.Attacker.Pokemon, top2.AttackerPTKO, top2.OpponentPTKO, top2.AttackerMovedFirst, top.Attacker_EndOfTurnHP, top.Opponent_EndOfTurnHP, top2.Opponent.Expendability );
        score += Mathf.FloorToInt( 40f * theyAreForcedOut );

        float oppHPLossRaw = top.Opponent_EndOfTurnHP - top2.Opponent_EndOfTurnHP;
        float oppHPLoss = NormalizeDamage( oppHPLossRaw, top.Opponent_EndOfTurnHP );
        if( oppHPLoss >= 0.4f )
            score += 30;
        else if( oppHPLoss >= 0.25f )
            score += 20;

        bool weDie = top2.Attacker_DiesBeforeActing || top2.Attacker_EndOfTurnHP <= 0f;
        if( weDie )
            score -= 100;

        float weAreForcedOut = _ai.UnitSim.PredictSwitchProbability( top2.Opponent.Pokemon, top2.OpponentPTKO, top2.AttackerPTKO, top2.AttackerMovedFirst, top.Opponent_EndOfTurnHP, top.Attacker_EndOfTurnHP, top2.Attacker.Expendability );
        score -= Mathf.FloorToInt( 75f * weAreForcedOut );

        float damageTakenRaw = top.Attacker.EndHPR - top2.Attacker_EndOfTurnHP;
        float damageTaken = NormalizeDamage( damageTakenRaw, top.Attacker.EndHPR );
        bool noPressure = top2.AttackerPTKO < PotentialToKO.TwoHKO;

        if( noPressure && ( damageTaken >= 0.4f || oppHPLoss < 0.2f && damageTaken >= 0.3f ) )
            score -= 50;

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
        var top2 = eval.Top2;

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

        if( top2.Attacker_DiesBeforeActing )
        {
            score -= DIE_BEFORE_ACTING_PENALTY;
            // eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker dies before setup completes! Score: {score}" );
            // return eval;
        }

        if( top2.Attacker_EndOfTurnHP <= 0f )
        {
            score -= SETUP_DIES_AFTER_ACTING_PENALTY;
            // eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker faints after setting up! Score: {score}" );
            // return eval;
        }

        if( top2.Opponent_DiesBeforeActing )
        {
            score += SETUP_THREATEN_KO_NEXT_TURN + 15;
            _ai.CurrentLog.Add( $"Setup likely KO without taking damage next turn! Score: {score}" );
        }
        else if( top2.Opponent_EndOfTurnHP <= 0f )
        {
            score += SETUP_THREATEN_KO_NEXT_TURN;
            _ai.CurrentLog.Add( $"Setup likely KO next turn! Score: {score}" );
        }

        if( top2.OpponentPTKO < top.OpponentPTKO )
        {
            score += 15;
            _ai.CurrentLog.Add( $"Setup is more defensive next turn! Score: {score}" );
        }
        
        if( (int)top2.OpponentPTKO - 1 < (int)top.OpponentPTKO )
        {
            score += 10;
            _ai.CurrentLog.Add( $"Setup walls hard next turn! Score: {score}" );
        }

        float damageTakenRaw = top2.Attacker.EndHPR - top2.Attacker_EndOfTurnHP;
        float damageTaken = NormalizeDamage( damageTakenRaw, top2.Attacker.EndHPR );
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
        bool movesFirst = top2.AttackerMovedFirst;

        float weForceSwitchNextTurnProbability = _ai.UnitSim.PredictSwitchProbability( top2.Attacker.Pokemon, top2.AttackerPTKO, top2.OpponentPTKO, movesFirst, top2.Attacker.EndHPR, top2.Opponent.EndHPR, top2.Opponent.Expendability, true, $"{top2.Opponent.Name} (Setup Look Ahead)" );
        float theyForceUsToSwitchNextTurnProbability = _ai.UnitSim.PredictSwitchProbability( top2.Opponent.Pokemon, top2.OpponentPTKO, top2.AttackerPTKO, movesFirst, top2.Opponent.EndHPR, top2.Attacker.EndHPR, top2.Attacker.Expendability, true, $"{top2.Attacker.Name} (Setup Look Ahead)" );

        float dangerWeight =
            top2.OpponentPTKO >= PotentialToKO.OHKO ? 1.5f :
            top2.OpponentPTKO >= PotentialToKO.Dangerous ? 1.25f :
            top2.OpponentPTKO >= PotentialToKO.Risky ? 1f :
            top2.OpponentPTKO >= PotentialToKO.TwoHKO ? 0.5f : 0.25f;

        float penalty = WE_SWITCH_WEIGHT * dangerWeight;

        score += Mathf.FloorToInt( OPPONENT_SWITCH_WEIGHT * weForceSwitchNextTurnProbability );
        score -= Mathf.FloorToInt( ( 1f - theyForceUsToSwitchNextTurnProbability ) * penalty );

        var oppTeam = _ai.GetRemainingOpposingPokemon( top2.Attacker.Pokemon );
        int fasterBonus = 0;
        bool weKO = top2.Opponent_DiesBeforeActing || top2.Opponent_EndOfTurnHP <= 0f;
        bool weForceSwitchNextTurn = weForceSwitchNextTurnProbability >= 0.7f;
        bool sweepBeginning = weKO || weForceSwitchNextTurn;

        if( sweepBeginning )
        {
            foreach( var opp in oppTeam )
            {
                int oppSpeed = _ai.GetUnitContextualSpeed( opp );

                if( top2.Attacker.Speed > oppSpeed )
                    fasterBonus += 5;
            }

            score += fasterBonus;
            _ai.CurrentLog.Add( $"Outspeeds {fasterBonus / 5} opposing Pokémon after setup! {score}" );
        }

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
        var top2 = eval.Top2;

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

        bool weNowMoveFirst = top2.AttackerMovedFirst;
        if( !top.AttackerMovedFirst && weNowMoveFirst )
        {
            score += 40;
            _ai.CurrentLog.Add( $"We outspeed next turn when we don't currently! Score: {score}" );
        }

        if( top2.OpponentPTKO < top.OpponentPTKO || top2.AttackerPTKO > top.AttackerPTKO )
        {
            score += 25;
            _ai.CurrentLog.Add( $"Survival or Offense improves next turn! Score: {score}" );
        }

        if( (int)top2.OpponentPTKO < (int)top.OpponentPTKO - 1 || (int)top2.AttackerPTKO > (int)top.AttackerPTKO + 1 )
        {
            score += 15;
            _ai.CurrentLog.Add( $"Survival or Offense improves next turn dramatically! Score: {score}" );
        }

        if( top2.Opponent_DiesBeforeActing )
        {
            score += 45;
            _ai.CurrentLog.Add( $"Opponent dies before acting next turn! Score: {score}" );
        }
        else if( top2.Opponent_EndOfTurnHP <= 0f )
        {
            score += 30;
            _ai.CurrentLog.Add( $"Opponent dies next turn! Score: {score}" );
        }

        if( top2.AttackerPTKO >= PotentialToKO.TwoHKO && weNowMoveFirst )
        {
            score += 20;
            _ai.CurrentLog.Add( $"We maintain pressure with speed advantage! Score: {score}" );
        }
        else if( top2.AttackerPTKO >= PotentialToKO.Risky )
        {
            score += 10;
            _ai.CurrentLog.Add( $"We maintain decent ko pressure! Score: {score}" );
        }

        if( top2.Attacker_DiesBeforeActing )
        {
            score -= 60;
            // eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker dies before acting! Score: {score}" );
            // return eval;
        }

        if( top2.Attacker_EndOfTurnHP <= 0f )
        {
            score -= 50;
            // eval.Score = score;
            _ai.CurrentLog.Add( $"Attacker dies! Score: {score}" );
            // return eval;
        }

        float weForceSwitchNextTurnProb = _ai.UnitSim.PredictSwitchProbability( top2.Opponent.Pokemon, top2.AttackerPTKO, top2.OpponentPTKO, top2.AttackerMovedFirst, top2.Attacker.BeginningHPR, top2.Opponent.BeginningHPR, top.Opponent.Expendability );
        score += Mathf.FloorToInt( 50f * weForceSwitchNextTurnProb );
        _ai.CurrentLog.Add( $"We force a switch next turn probability: {weForceSwitchNextTurnProb} * 50f. Score: {score}" );

        float damageTakenRaw = top2.Attacker.EndHPR - top2.Attacker_EndOfTurnHP;
        float damageTaken = NormalizeDamage( damageTakenRaw, top2.Attacker.EndHPR );
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

        eval.Score += score;
        _ai.CurrentLog.Add( $"Evaluate Offensive Status Simulation Score: {score}" );
        _ai.CurrentLog.Add( $"Current Offensive Status Decision Score: {eval.Score}" );
        return eval;
    }

    private ActionEvaluation EvaluateSupportiveStatusSim( ActionEvaluation eval )
    {
        int score = 0;
        var top1 = eval.Top1;
        var top2 = eval.Top2;

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

        StatusThreatResult str = (StatusThreatResult)eval.ActionResult;

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

        //--Resisting Opponent PTKO Improvement checks
        if( top1.OpponentPTKO < eeOpponentPTKO )
        {
            score += 30;
            _ai.CurrentLog.Add( $"This action reduces the opponent's potential to KO us this turn. Score: {score}" );
        }
        else if( eeOpponentPTKO >= PotentialToKO.Dangerous && eeOpponentPTKO >= top1.OpponentPTKO ) //--replace with ptko severity ladder, same for our ptko
        {
            score -= 45;
            _ai.CurrentLog.Add( $"This action doesn't change the opponent's potential to KO us this turn, or makes it worse. Score: {score}" );
        }
        else if( eeOpponentPTKO >= PotentialToKO.Risky && eeOpponentPTKO >= top1.OpponentPTKO ) //--replace with ptko severity ladder, same for our ptko
        {
            score -= 30;
            _ai.CurrentLog.Add( $"This action doesn't change the opponent's potential to KO us this turn, or makes it worse. Score: {score}" );
        }
        else if( eeOpponentPTKO >= PotentialToKO.TwoHKO && eeOpponentPTKO >= top1.OpponentPTKO ) //--replace with ptko severity ladder, same for our ptko
        {
            score -= 10;
            _ai.CurrentLog.Add( $"This action doesn't change the opponent's potential to KO us this turn, or makes it worse. Score: {score}" );
        }

        //--Opponent PTKOs
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
        else if( top1.OpponentPTKO >= PotentialToKO.Dangerous && top1.AttackerPTKO <= ee1.AttackerPTKO )
        {
            score -= 55;
            _ai.CurrentLog.Add( $"The opponent has a reasonable chance to KO us next turn. Score: {score}" );
        }

        //--Our PTKO improvement checks
        if( eeAttackerPTKO >= PotentialToKO.Risky && eeAttackerPTKO < top1.AttackerPTKO )
        {
            score += 45;
            _ai.CurrentLog.Add( $"This action improves our potential to KO the opponent this turn. Score: {score}" );
            if( eeAttackerPTKO + 1 < top1.AttackerPTKO )
            {
                score += 10;
                _ai.CurrentLog.Add( $"This action dramatically improves our potential to KO the opponent this turn. Score: {score}" );
            }
        }
        else if( eeAttackerPTKO >= PotentialToKO.Safe && eeAttackerPTKO < top1.AttackerPTKO )
        {
            score += 30;
            _ai.CurrentLog.Add( $"This action improves our potential to KO the opponent this turn. Score: {score}" );
            if( eeAttackerPTKO + 1 < top1.AttackerPTKO )
            {
                score += 20;
                _ai.CurrentLog.Add( $"This action dramatically improves our potential to KO the opponent this turn. Score: {score}" );
            }
        }

        //--Our PTKOs
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

        //--Speed
        if( top1.AttackerMovedFirst )
        {
            score += 10;
            _ai.CurrentLog.Add( $"We move first when using our support move this turn. Score: {score}" );
        }

        //--Our Ally Block
        if( eval.ExchangePack.OurAllyExists && str.SupportiveStatusType != SupportiveStatusType.Recovery )
        {
            var allyVS_Threat1 = eval.ExchangePack.AllyVS_Threat;
            var allyVS_ThreatAfter1 = _ai.Projection.EvaluateExchange( top1.AttackerAlly, ee1.Opponent ); //--this evaluates a post top1 ally against a pre top1 opponent to infer inter top1 exchange results

            //--Ally death checks
            if( top1.AttackerAlly_DiesBeforeActing )
            {
                score -= 50;
                _ai.CurrentLog.Add( $"Our ally dies before it can act! Score: {score}" );
            }
            else if( top1.AttackerAlly_EndOfTurnHP <= 0 )
            {
                score -= 30;
                _ai.CurrentLog.Add( $"Attacker dies after support! Score: {score}" );
            }

            //--Ally resisting opponent PTKO improvement checks
            if( allyVS_ThreatAfter1.OpponentPTKO < allyVS_Threat1.OpponentPTKO )
            {
                score += 25;
                _ai.CurrentLog.Add( $"The opponent has a worse PTKO on our ally if we use this move. Score: {score}" );
            }
            else if( allyVS_ThreatAfter1.OpponentPTKO >= PotentialToKO.Dangerous && allyVS_Threat1.OpponentPTKO >= allyVS_ThreatAfter1.OpponentPTKO )
            {
                score -= 20;
                _ai.CurrentLog.Add( $"The opponent has a likely chance to OHKO our ally and support did not improve it! Score: {score}" );
            }
            else if( allyVS_ThreatAfter1.OpponentPTKO >= PotentialToKO.Risky && allyVS_Threat1.OpponentPTKO >= allyVS_ThreatAfter1.OpponentPTKO )
            {
                score -= 15;
                _ai.CurrentLog.Add( $"The opponent has a chance to OHKO our ally if they get lucky and support did not improve it! Score: {score}" );
            }

            //--Opponent's PTKOs on ally
            if( allyVS_ThreatAfter1.OpponentPTKO <= PotentialToKO.Safe )
            {
                score += 25;
                _ai.CurrentLog.Add( $"The opponent has a very safe PTKO on us. Score: {score}" );
            }
            else if( allyVS_ThreatAfter1.OpponentPTKO <= PotentialToKO.Risky )
            {
                score += 15;
                _ai.CurrentLog.Add( $"The opponent has a survivable PTKO on our ally. Score: {score}" );
            }
            else if( allyVS_ThreatAfter1.OpponentPTKO >= PotentialToKO.Dangerous && top1.AttackerAllyPTKO <= allyVS_Threat1.AttackerPTKO )
            {
                score -= 35;
                _ai.CurrentLog.Add( $"The opponent has a very likely PTKO on us and our ally's PTKO on them did not improve due to support. Score: {score}" );
            }

            //--Ally's PTKO improvement checks
            if( allyVS_Threat1.AttackerPTKO >= PotentialToKO.Risky && allyVS_Threat1.AttackerPTKO < top1.AttackerAllyPTKO )
            {
                score += 25;
                _ai.CurrentLog.Add( $"Our ally had a possible chance to KO before support, and support improved their PTKO. Score: {score}" );

                if( allyVS_Threat1.AttackerPTKO + 1 < top1.AttackerAllyPTKO )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"Support improved our ally's PTKO dramatically. Score: {score}" );
                }
            }
            else if( allyVS_Threat1.AttackerPTKO >= PotentialToKO.Safe && allyVS_Threat1.AttackerPTKO < top1.AttackerAllyPTKO )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally had no chance to KO before support, but support improved their PTKO. Score: {score}" );

                if( allyVS_Threat1.AttackerPTKO + 1 < top1.AttackerAllyPTKO )
                {
                    score += 20;
                    _ai.CurrentLog.Add( $"Support improved our ally's PTKO dramatically. Score: {score}" );
                }
            }

            //--Ally's PTKOs
            if( top1.AttackerAllyPTKO == PotentialToKO.OHKO )
            {
                score += 20;
                _ai.CurrentLog.Add( $"Our ally is likely to KO the threat. Score: {score}" );
            }
            else if( top1.AttackerAllyPTKO == PotentialToKO.Dangerous )
            {
                score += 15;
                _ai.CurrentLog.Add( $"Our ally has a good chance to KO the threat. Score: {score}" );
            }
            else if( top1.AttackerAllyPTKO == PotentialToKO.Risky )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally can KO the threat if they get lucky. Score: {score}" );
            }

            //--Speed
            if( !allyVS_Threat1.AttackerMovesFirst && ( top1.AttackerAllyMovedFirst || allyVS_ThreatAfter1.AttackerMovesFirst ) )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally is likely to move before our opponents after we use this support move. Score: {score}" );
            }
        }
        
        //--------------------------------
        //----------Look Ahead------------
        //--------------------------------

        float weForceOppSwitchNextProb = _ai.UnitSim.PredictSwitchProbability( top2.Opponent.Pokemon, top2.AttackerPTKO, top2.OpponentPTKO, top2.AttackerMovedFirst, top2.Attacker.BeginningHPR, top2.Opponent.BeginningHPR, top2.Opponent.Expendability );
        score += Mathf.FloorToInt( 25f * weForceOppSwitchNextProb );
        _ai.CurrentLog.Add( $"They switch next turn probability: {weForceOppSwitchNextProb}. Score: {score}" );

        if( eval.ExchangePack.OurAllyExists )
        {
            float allyForceOppSwitchNextProb = _ai.UnitSim.PredictSwitchProbability( top2.Opponent.Pokemon, top2.AttackerAllyPTKO, top2.OpponentPTKO, top2.AttackerAllyMovedFirst, top2.AttackerAlly.BeginningHPR, top2.Opponent.BeginningHPR, top2.Opponent.Expendability );
            score += Mathf.FloorToInt( 15f * allyForceOppSwitchNextProb );
            _ai.CurrentLog.Add( $"They switch next turn because of our ally probability: {allyForceOppSwitchNextProb}. Score: {score}" );
        }

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

        //--Our Ally Block (Look ahead)
        if( eval.ExchangePack.OurAllyExists && str.SupportiveStatusType != SupportiveStatusType.Recovery )
        {
            var allyVS_ThreatAfter1 = _ai.Projection.EvaluateExchange( top1.AttackerAlly, ee1.Opponent ); //--this evaluates a post top1 ally against a pre top1 opponent to infer inter top1 exchange results
            var allyVS_ThreatAfter2 = _ai.Projection.EvaluateExchange( top2.AttackerAlly, top1.Opponent ); //--this evaluates a post top2 ally against a pre top2 opponent to infer inter top2 exchange results

            //--Ally death checks
            if( top2.AttackerAlly_DiesBeforeActing )
            {
                score -= 50;
                _ai.CurrentLog.Add( $"Our ally dies before it can act! Score: {score}" );
            }
            else if( top2.AttackerAlly_EndOfTurnHP <= 0 )
            {
                score -= 30;
                _ai.CurrentLog.Add( $"Attacker dies after support! Score: {score}" );
            }

            //--Ally resisting opponent PTKO improvement checks
            if( allyVS_ThreatAfter2.OpponentPTKO < allyVS_ThreatAfter1.OpponentPTKO )
            {
                score += 20;
                _ai.CurrentLog.Add( $"The opponent has a worse PTKO on our ally if we use this move. Score: {score}" );
            }
            else if( allyVS_ThreatAfter2.OpponentPTKO >= PotentialToKO.Dangerous && allyVS_ThreatAfter1.OpponentPTKO >= allyVS_ThreatAfter2.OpponentPTKO )
            {
                score -= 15;
                _ai.CurrentLog.Add( $"The opponent has a likely chance to OHKO our ally and support did not improve it! Score: {score}" );
            }
            else if( allyVS_ThreatAfter2.OpponentPTKO >= PotentialToKO.Risky && allyVS_ThreatAfter1.OpponentPTKO >= allyVS_ThreatAfter2.OpponentPTKO )
            {
                score -= 10;
                _ai.CurrentLog.Add( $"The opponent has a chance to OHKO our ally if they get lucky and support did not improve it! Score: {score}" );
            }

            //--Opponent's PTKOs on ally
            if( allyVS_ThreatAfter2.OpponentPTKO <= PotentialToKO.Safe )
            {
                score += 20;
                _ai.CurrentLog.Add( $"The opponent has a very safe PTKO on us. Score: {score}" );
            }
            else if( allyVS_ThreatAfter2.OpponentPTKO <= PotentialToKO.Risky )
            {
                score += 10;
                _ai.CurrentLog.Add( $"The opponent has a survivable PTKO on our ally. Score: {score}" );
            }
            else if( allyVS_ThreatAfter2.OpponentPTKO >= PotentialToKO.Dangerous && allyVS_ThreatAfter2.AttackerPTKO <= allyVS_ThreatAfter1.AttackerPTKO )
            {
                score -= 25;
                _ai.CurrentLog.Add( $"The opponent has a very likely PTKO on us and our ally's PTKO on them did not improve due to support. Score: {score}" );
            }

            //--Ally's PTKO improvement checks
            if( top1.AttackerAllyPTKO >= PotentialToKO.Risky && top1.AttackerAllyPTKO < top2.AttackerAllyPTKO )
            {
                score += 15;
                _ai.CurrentLog.Add( $"Our ally had a possible chance to KO before support, and support improved their PTKO. Score: {score}" );

                if( top1.AttackerAllyPTKO + 1 < top2.AttackerAllyPTKO )
                {
                    score += 5;
                    _ai.CurrentLog.Add( $"Support improved our ally's PTKO dramatically. Score: {score}" );
                }
            }
            else if( top1.AttackerAllyPTKO >= PotentialToKO.Safe && top1.AttackerAllyPTKO < top2.AttackerAllyPTKO )
            {
                score += 5;
                _ai.CurrentLog.Add( $"Our ally had no chance to KO before support, but support improved their PTKO. Score: {score}" );

                if( top1.AttackerAllyPTKO + 1 < top2.AttackerAllyPTKO )
                {
                    score += 15;
                    _ai.CurrentLog.Add( $"Support improved our ally's PTKO dramatically. Score: {score}" );
                }
            }

            //--Ally's PTKOs
            if( top2.AttackerAllyPTKO == PotentialToKO.OHKO )
            {
                score += 15;
                _ai.CurrentLog.Add( $"Our ally is likely to KO the threat. Score: {score}" );
            }
            else if( top2.AttackerAllyPTKO == PotentialToKO.Dangerous )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally has a good chance to KO the threat. Score: {score}" );
            }
            else if( top2.AttackerAllyPTKO == PotentialToKO.Risky )
            {
                score += 5;
                _ai.CurrentLog.Add( $"Our ally can KO the threat if they get lucky. Score: {score}" );
            }

            //--Speed
            if( !top1.AttackerAllyMovedFirst && ( top2.AttackerAllyMovedFirst || allyVS_ThreatAfter2.AttackerMovesFirst ) )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally is likely to move before our opponents after we use this support move. Score: {score}" );
            }
        }

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
        float weForceSwitchNextTurnProb = _ai.UnitSim.PredictSwitchProbability( followUp.Opponent.Pokemon, followUp.AttackerPTKO, followUp.OpponentPTKO, followUp.AttackerMovedFirst, nextPokemon.EndHPR, eval.Top1.Opponent_EndOfTurnHP, followUp.Opponent.Expendability );
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

        // if( bfs.Round <= 1 && top1.OpponentPTKO != PotentialToKO.OHKO && !top1.AttackerMovedFirst )
        // {
        //     score -= 30;
        //     _ai.CurrentLog.Add( $"It's first round and we're not immediately threatened with death. Can we do something else other than switch? Score: {score}" );
        // }
        // else if( bfs.IsEarlyGame && top1.OpponentPTKO < PotentialToKO.Dangerous )
        // {
        //     score -= 15;
        //     _ai.CurrentLog.Add( $"It's early game and we're not in immediate danger. Should we try something else? Score: {score}" );
        // }

        if( bfs.EntryHazardsOn_MySide > 0 )
        {
            if( bfs.IsEarlyGame || isMidGame )
            {
                score -= 10;
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
                case AbilityID.Drought: candidatesWeather = WeatherConditionID.Sun; break;
                case AbilityID.Drizzle: candidatesWeather = WeatherConditionID.Rain; break;
                case AbilityID.Sandstream: candidatesWeather = WeatherConditionID.Sand; break;
                case AbilityID.SnowWarning: candidatesWeather = WeatherConditionID.Snow; break;
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
            score += 10;
            _ai.CurrentLog.Add( $"We may be able to take advantage of our tailwind. Score {score}" );
        }

        MoveCategory oppMoveCat = top1.Opponent.MTR?.Move != null ? top1.Opponent.MTR.Move.MoveSO.MoveCategory : MoveCategory.Other;
        if( bfs.WeHave_Reflect && bfs.OurReflectDuration >= 2 && ( oppMoveCat == MoveCategory.Physical || oppMoveCat == MoveCategory.Other ) )
        {
            score += 10;
            _ai.CurrentLog.Add( $"We're protected on incoming by Reflect. Score {score}" );
        }

        if( bfs.WeHave_LightScreen && bfs.OurLightScreenDuration >= 2 && ( oppMoveCat == MoveCategory.Special || oppMoveCat == MoveCategory.Other ) )
        {
            score += 10;
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
            /*if( top2.AttackerPTKO < PotentialToKO.Dangerous)
            {
                score -= 15;
                _ai.CurrentLog.Add( $"It's early game and we're not threatening powerful offense next turn. Should we try something else? Score: {score}" );
            }
            else */if( top2.AttackerPTKO >= PotentialToKO.Dangerous )
            {
                score += 10;
                _ai.CurrentLog.Add( $"It's early game and we threaten powerful offense next turn. Giving a small nudge for early-game tempo grab/battlefield control. Score: {score}" );
            }
        }

        if( bfs.IsMidGame && top2.AttackerPTKO >= PotentialToKO.Dangerous  )
        {
            score += 15;
            _ai.CurrentLog.Add( $"It's mid game and we threaten powerful offense next turn. Giving a slight boost for mid game phase tempo grab. Score: {score}" );
        }

        // if( bfs.IsLateGame && top2.AttackerPTKO != PotentialToKO.OHKO )
        // {
        //     score -= 5;
        //     _ai.CurrentLog.Add( $"It's late game and we don't threaten a KO next turn. Tiny tiny penalty. Score: {score}" );
        // }

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
                case AbilityID.Drought: candidatesWeather = WeatherConditionID.Sun; break;
                case AbilityID.Drizzle: candidatesWeather = WeatherConditionID.Rain; break;
                case AbilityID.Sandstream: candidatesWeather = WeatherConditionID.Sand; break;
                case AbilityID.SnowWarning: candidatesWeather = WeatherConditionID.Snow; break;
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
