using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleAI_EvaluateThreatResponse
{
    private readonly BattleAI _ai;

    public BattleAI_EvaluateThreatResponse( BattleAI ai )
    {
        _ai = ai;
    }

    public int EvaluateThreatResponse( ActionEvaluation action, ThreatProfile threat, DoomedOutcome doomed, BoardContext bc )
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

        score += threat.Type switch
        {
            ThreatType.Immediate    => EvaluateImmediateThreat( action, threat, doomed, bc ),
            ThreatType.Escalating   => EvaluateEscalatingThreat( action, threat, doomed, bc ),
            ThreatType.Persistent   => EvaluatePersistentThreat( action, threat, doomed, bc ),
            ThreatType.Disruptive   => EvaluateDisruptiveThreat( action, threat, doomed, bc ),
            ThreatType.Constraining => EvaluateConstrainingThreat( action, threat, doomed, bc ),
            _ => 0,
        };

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

        //--Strategic Sacrifice to regain control
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

        if( action.ExchangePack.OurAllyExists && top1.AttackerAlly != null )
        {
            var allyVS_Threat = action.ExchangePack.AllyVS_Threat;
            var allyVS_ThreatAfter1 = _ai.Projection.EvaluateExchange( top1.AttackerAlly, allyVS_Threat.Opponent );

            if( top1.AttackerAllyPTKO >= PotentialToKO.Dangerous )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally is likely to land a KO on our chosen threat. Score: {score}" );
            }

            if( !allyVS_Threat.AttackerMovesFirst && allyVS_ThreatAfter1.AttackerMovesFirst )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally's speed improves from our support this round. Score: {score}" );
            }

            if( allyVS_Threat.AttackerPTKO < PotentialToKO.Dangerous && top1.AttackerAllyPTKO >= PotentialToKO.Dangerous )
            {
                score += 25;
                _ai.CurrentLog.Add( $"Our ally's ptko improves from our support this round. Score: {score}" );
            }

            bool allyForcesLine = top1.OpponentCanAct && ( allyVS_ThreatAfter1.OpponentPTKO < allyVS_Threat.OpponentPTKO || top2.Opponent_EndOfTurnHP < 0.5f || !top2.OpponentCanAct );
            if( allyForcesLine )
            {
                score += 10;
                _ai.CurrentLog.Add( $"This action allows our ally to create a forced line for the opponent. Score: {score}" );

                if( !forcedLine )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"Our own action doesn't create a forced line, but our ally's attack does. Score: {score}" );
                }
            }
        }

        _ai.CurrentLog.Add( $"{action.Type}'s Final Threat Response Score: {score}" );
        _ai.CurrentLog.Add( $"===================================================" );
        _ai.CurrentLog.Add( $"" );

        return score;
    }

    private int EvaluateImmediateThreat( ActionEvaluation action, ThreatProfile threat, DoomedOutcome doomed, BoardContext bc )
    {
        int score = 0;

        float sackScalar = 0.7f;
        var expendability = bc.MyExpendability;
        float sackModifier = ( 1 - expendability * sackScalar );

        var top1 = action.Top1;
        var top2 = action.Top2;

        float damageDealt = top1.Opponent.BeginningHPR - top1.Opponent_EndOfTurnHP;
        _ai.CurrentLog.Add( $"Damage Dealt to threat: {damageDealt}. Score: {score}" );

        //--This action's switch probability.
        float theySwitchProbability = _ai.UnitSim.PredictSwitchProbability( top1.Opponent.Pokemon, top1.AttackerPTKO, top1.OpponentPTKO, top1.AttackerMovedFirst, top1.Attacker.BeginningHPR, top1.Opponent.BeginningHPR, top1.Opponent.Expendability );
        score += Mathf.FloorToInt( 50f * theySwitchProbability );
        _ai.CurrentLog.Add( $"Switch Probability: {theySwitchProbability}. Score: {score}" );

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

            if( action.Top2.Attacker.Ability == AbilityID.Sandstream && action.Top1.Field.Weather != WeatherConditionID.Sand )
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
                    _ai.CurrentLog.Add( $"The opposing immediate threat is physical and we burn them. Score: {score}" );
                }

                if( biases.Contains( RoleBias.Special ) && status == SevereConditionID.FBT )
                {
                    score += 15;
                    _ai.CurrentLog.Add( $"The opposing immediate threat is special and we frostbite them. Score: {score}" );
                }

                if( ( biases.Contains( RoleBias.MiddlingSpeed ) || biases.Contains( RoleBias.FastSpeed ) ) && status == SevereConditionID.PAR )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"The opposing immediate threat is fast and we paralyze them. Score: {score}" );
                }

                if( status == SevereConditionID.SLP )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"We sleep the immediate threat. Score: {score}" );
                }
            }
        }

        if( action.ExchangePack.OurAllyExists && top1.AttackerAlly != null )
        {
            var allyVS_Threat = action.ExchangePack.AllyVS_Threat;

            if( allyVS_Threat.AttackerPTKO < top1.AttackerAllyPTKO )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally's ptko against the immediate threat improves from our support. Score: {score}" );
            }

            if( top1.AttackerAlly.EndHPR > 0 )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally survives the round (07/12/26 this will always be true in simulations currently). Score: {score}" );
            }

            if( !allyVS_Threat.AttackerMovesFirst && top1.AttackerAllyMovedFirst )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally's speed compared to the immediate threat improves from our support. Score: {score}" );
            }

            if( allyVS_Threat.AttackerPTKO < PotentialToKO.Dangerous && top1.AttackerAllyPTKO >= PotentialToKO.Dangerous )
            {
                score += 25;
                _ai.CurrentLog.Add( $"Our ally's ptko against the immediate threat goes from not being likely to KO to being very likely to KO from our support. Score: {score}" );
            }

            //--Add redirection support here
            if( action.ActionResult.ActionType == ActionType.SupportiveStatus )
            {
                var str = (StatusThreatResult)action.ActionResult;

                //--Did Follow Me let breaker attack freely?
                if( str.Move.MoveSO.MoveEffects.TransientStatus == TransientConditionID.CenterOfAttention && top1.AttackerAllyPTKO >= PotentialToKO.Risky )
                {
                    score += 25;
                    _ai.CurrentLog.Add( $"Our ally is likely to do a lot of damage to the immediate threat after we redirect attacks away from them. Score: {score}" );
                }
            }
        }

        return score;
    }

    private int EvaluateEscalatingThreat( ActionEvaluation action, ThreatProfile threat, DoomedOutcome doomed, BoardContext bc )
    {
        int score = 0;

        float sackScalar = 0.7f;
        var expendability = bc.MyExpendability;
        float sackModifier = ( 1 - expendability * sackScalar );

        var top1 = action.Top1;
        var top2 = action.Top2;

        float damageDealt = top1.Opponent.BeginningHPR - top1.Opponent_EndOfTurnHP;
        _ai.CurrentLog.Add( $"Damage Dealt to threat: {damageDealt}. Score: {score}" );

        //--This action's switch probability.
        float theySwitchProbability = _ai.UnitSim.PredictSwitchProbability( top1.Opponent.Pokemon, top1.AttackerPTKO, top1.OpponentPTKO, top1.AttackerMovedFirst, top1.Attacker.BeginningHPR, top1.Opponent.BeginningHPR, top1.Opponent.Expendability );
        score += Mathf.FloorToInt( 50f * theySwitchProbability );
        _ai.CurrentLog.Add( $"Switch Probability: {theySwitchProbability}. Score: {score}" );

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
                _ai.CurrentLog.Add( $"We do big damage to the escalating recovery threat. Score: {score}" );
            }

            if( top1.AttackerMovedFirst )
            {
                score += 5;
                _ai.CurrentLog.Add( $"We move before the escalating recovery threat. Score: {score}" );
            }

            if( top2.AttackerPTKO >= PotentialToKO.Dangerous && top2.Attacker_EndOfTurnHP > 0 )
            {
                score += 10;
                _ai.CurrentLog.Add( $"We're likely to KO the escalating recovery threat next turn and survive. Score: {score}" );
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
            _ai.CurrentLog.Add( $"We do more damage than they want to take if they try to setup. Score: {score}" );
        }

        bool forcedRespect = top1.AttackerPTKO >= PotentialToKO.Risky || top2.AttackerPTKO >= PotentialToKO.Dangerous;
        if( forcedRespect )
        {
            score += 20;
            _ai.CurrentLog.Add( $"This action prevents the setup threat from freely escalating by forcing immediate respect. Score: {score}" );

            if( top1.OpponentPTKO >= PotentialToKO.Risky && top1.Attacker_EndOfTurnHP > 0 )
            {
                score += 10;
                _ai.CurrentLog.Add( $"We also survive their likely big damage. Score: {score}" );
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
                    _ai.CurrentLog.Add( $"Escalating threat is physical and we burn them. Score: {score}" );
                }

                if( biases.Contains( RoleBias.Special ) && status == SevereConditionID.FBT )
                {
                    score += 25;
                    _ai.CurrentLog.Add( $"Escalating threat is special and we frostbite them. Score: {score}" );
                }

                if( status == SevereConditionID.PAR )
                {
                    score += 20;
                    _ai.CurrentLog.Add( $"Paralyzing an escalating threat robs them of their next turn and permanently cripples their speed. Score: {score}" );
                }

                if( status == SevereConditionID.SLP )
                {
                    score += 30;
                    _ai.CurrentLog.Add( $"Sleeping an escalating threat robs them of their next two turns. Score: {score}" );
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

            _ai.CurrentLog.Add( $"Checking the value of enganging in a setup race...." );

            if( ourMoveIsOffensivePlus2 && _ai.UnitSim.PokemonHasMove_OffensivePriority( top1.Attacker.Pokemon ) )
            {
                score += 15;
                _ai.CurrentLog.Add( $"We are +2 in an attacking stat and have a priority move. Score: {score}" );

                if( top2.AttackerMovedFirst )
                {
                    score += 5;
                    _ai.CurrentLog.Add( $"And we move first next turn. Score: {score}" );
                }
            }
            else if( weAreIronDefenseBodyPress )
            {
                score += 15;

                _ai.CurrentLog.Add( $"We are Iron Defense Body Press. Score: {score}" );

                if( top2.AttackerMovedFirst )
                {
                    score += 5;
                    _ai.CurrentLog.Add( $"And we move first next turn. Score: {score}" );
                }
            }
            else if( weSetup_PhysicallyOffensive && theySetup_SpeciallyDefensive || weSetup_SpeciallyOffensive && theySetup_PhysicallyDefensive )
            {
                score += 5;
                _ai.CurrentLog.Add( $"We offensively setup against the defensive stat they do not setup with. Score: {score}" );

                if( weMovefirstNext )
                {
                    score += 15;
                    _ai.CurrentLog.Add( $"And we move first next turn. Score: {score}" );
                }
            }
            else if( weSetup_PhysicallyDefensive && theySetup_PhysicallyOffensive || weSetup_SpeciallyDefensive && theySetup_SpeciallyOffensive )
            {
                score += 5;

                _ai.CurrentLog.Add( $"We setup with the defensive stat that aligns with their offensive setup. Score: {score}" );

                if( weMovefirstNext )
                {
                    score += 15;
                    _ai.CurrentLog.Add( $"And we move first next turn. Score: {score}" );
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

        if( action.ExchangePack.OurAllyExists && top1.AttackerAlly != null )
        {
            var allyVS_Threat = action.ExchangePack.AllyVS_Threat;
            var allyVS_ThreatAfter1 = _ai.Projection.EvaluateExchange( top1.AttackerAlly, allyVS_Threat.Opponent ); //--this evaluates a post top1 ally against a pre top1 opponent to infer inter top1 exchange results

            //--Helping Hand immediately lets ally KO
            if( allyVS_Threat.AttackerPTKO < PotentialToKO.Dangerous && top1.AttackerAllyPTKO >= PotentialToKO.Dangerous )
            {
                score += 20;
                _ai.CurrentLog.Add( $"Our ally was not likely to get a KO, but is now very likely after our support. Score: {score}" );
            }

            if( top1.AttackerAlly.EndHPR > 0 )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally survives the round (07/12/26 always true as of right now). Score: {score}" );
            }

            if( !allyVS_Threat.AttackerMovesFirst && allyVS_ThreatAfter1.AttackerMovesFirst )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally was not likely to move first, but is now very likely to move first after our support. Score: {score}" );
            }

            //--Tailwind lets ally revenge next turn
            if( ( top1.AttackerAlly.Speed < top2.AttackerAlly.Speed && top2.AttackerAlly.Speed > top2.Opponent.Speed ) || ( !top1.AttackerAllyMovedFirst && top2.AttackerAllyMovedFirst ) )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally had their speed directly improved by our support. Score: {score}" );

                if( top2.AttackerAllyPTKO >= PotentialToKO.Dangerous )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"And they are likely to get a revenge KO on the escalating threat next turn. Score: {score}" );
                }
            }

            //--Screens let ally survive the boosted hit
            if( allyVS_Threat.OpponentPTKO >= PotentialToKO.Dangerous && allyVS_ThreatAfter1.OpponentPTKO < PotentialToKO.Dangerous )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally had their survivability directly improved by our support. Score: {score}" );

                if( allyVS_ThreatAfter1.OpponentPTKO < PotentialToKO.TwoHKO )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"We've made our ally incredibly safe. Score: {score}" );
                }
            }

            if( action.ActionResult.ActionType == ActionType.SupportiveStatus )
            {
                var str = (StatusThreatResult)action.ActionResult;

                //--Did Follow Me let breaker attack freely?
                if( str.Move.MoveSO.MoveEffects.TransientStatus == TransientConditionID.CenterOfAttention && top1.AttackerAllyPTKO >= PotentialToKO.Dangerous )
                {
                    score += 25;
                    _ai.CurrentLog.Add( $"Our ally has a very likely chance to KO the escalating threat if we redirect the threat's move away. Score: {score}" );
                }
            }
            
            //--Protection Decision line
            //--Wide Guard blocks boosted spread move. this also requires pair intent and threat intent
        }

        return score;
    }

    private int EvaluatePersistentThreat( ActionEvaluation action, ThreatProfile threat, DoomedOutcome doomed, BoardContext bc )
    {
        int score = 0;

        float sackScalar = 0.7f;
        var expendability = bc.MyExpendability;
        float sackModifier = ( 1 - expendability * sackScalar );

        var top1 = action.Top1;
        var top2 = action.Top2;

        float damageDealt = top1.Opponent.BeginningHPR - top1.Opponent_EndOfTurnHP;
        _ai.CurrentLog.Add( $"Damage Dealt to threat: {damageDealt}. Score: {score}" );

        //--This action's switch probability.
        float theySwitchProbability = _ai.UnitSim.PredictSwitchProbability( top1.Opponent.Pokemon, top1.AttackerPTKO, top1.OpponentPTKO, top1.AttackerMovedFirst, top1.Attacker.BeginningHPR, top1.Opponent.BeginningHPR, top1.Opponent.Expendability );
        score += Mathf.FloorToInt( 50f * theySwitchProbability );
        _ai.CurrentLog.Add( $"Switch Probability: {theySwitchProbability}. Score: {score}" );

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
            _ai.CurrentLog.Add( $"Our PTKO is better next turn than it is this turn. Score: {score}" );
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
                _ai.CurrentLog.Add( $"The opposing tank ignores setup. Score: {score}" );
            }

            if( tankHasSetupDisruptionMove )
            {
                score -= 10;
                _ai.CurrentLog.Add( $"The opposing tank ignores disruption. Score: {score}" );
            }

            if( tankCanStatus )
            {
                score -= 10;
                _ai.CurrentLog.Add( $"The opposing tank can status. Score: {score}" );
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
                            _ai.CurrentLog.Add( $"We have used toxic the persistent threat, likely engaging in toxic stall. Score: {score}" );
                        }
                    }

                    if( isToxic )
                    {
                        score += 5;
                        _ai.CurrentLog.Add( $"Extra toxic the persistent threat bonus, because toxic is a direct answer to this type of threat. Score: {score}" );
                    }
                }

                if( recoveryLocked )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"Threat is likely to be recovery locked, taking advantage with severe status should be rewarded. Score: {score}" );
                }

                string moveName = action.MovePayload.MoveSO.Name;
                bool recoveryItem = threat.ThreatUnit.Item == ItemBattleEffectID.Leftovers || threat.ThreatUnit.Item == ItemBattleEffectID.SitrusBerry;
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

        if( action.ExchangePack.OurAllyExists && top1.AttackerAlly != null )
        {
            var allyVS_Threat = action.ExchangePack.AllyVS_Threat;
            var allyVS_ThreatAfter1 = _ai.Projection.EvaluateExchange( top1.AttackerAlly, allyVS_Threat.Opponent ); //--this evaluates a post top1 ally against a pre top1 opponent to infer inter top1 exchange results

            float allyDamageBefore = Mathf.Round( ( allyVS_Threat.AttackerMTR.EstimatedDamage / allyVS_Threat.Opponent.BeginningHPR ) * 100f ) / 100f;
            float allyDamageAfter = Mathf.Round( ( allyVS_ThreatAfter1.AttackerMTR.EstimatedDamage / allyVS_Threat.Opponent.BeginningHPR ) * 100f ) / 100f;

            //--Did Helping Hand make ally break the wall?
            if( allyDamageAfter > allyDamageBefore )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally does more damage after our support to the persistent threat. Score: {score}" );

                if( allyDamageBefore < 0.45f && allyDamageAfter > 0.55f )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"They do more and more significant damage to the persistent threat after our support. Score: {score}" );
                }
            }

            //--Did Tailwind let ally pressure it?
            if( !allyVS_Threat.AttackerMovesFirst && allyVS_ThreatAfter1.AttackerMovesFirst )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally likely had their speed improved by our support, allowing them to pressure the persistent threat this turn. Score: {score}" );
            }

            if( action.ActionResult.ActionType == ActionType.SupportiveStatus )
            {
                var str = (StatusThreatResult)action.ActionResult;

                //--Did Follow Me let breaker attack freely?
                if( str.Move.MoveSO.MoveEffects.TransientStatus == TransientConditionID.CenterOfAttention && top1.AttackerAllyPTKO >= PotentialToKO.Risky )
                {
                    score += 25;
                }

                //--Did Safeguard remove passive pressure?
                if( str.Move.MoveSO.MoveEffects.CourtCondition == CourtConditionID.SafeGuard && top1.Opponent.RoleProfile.Traits.Contains( RoleTrait.StatusSpreader ) )
                {
                    score += 10;

                    if( top1.AttackerMovedFirst )
                    {
                        score += 10;
                    }
                }
            }

            //--Tailwind lets ally revenge next turn
            if( ( top1.AttackerAlly.Speed < top2.AttackerAlly.Speed && top2.AttackerAlly.Speed > top2.Opponent.Speed ) || ( !top1.AttackerAllyMovedFirst && top2.AttackerAllyMovedFirst ) )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally had their speed directly improved by our support. Score: {score}" );

                if( top2.AttackerAllyPTKO >= PotentialToKO.Dangerous )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"Our ally also has a very likely KO next turn after we improved their speed going into next turn. Score: {score}" );
                }
            }

            //--Screens let ally survive the boosted hit
            if( allyVS_Threat.OpponentPTKO >= PotentialToKO.Dangerous && allyVS_ThreatAfter1.OpponentPTKO < PotentialToKO.Dangerous )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Our ally likely had their survivability improved by our support. Score: {score}" );

                if( allyVS_ThreatAfter1.OpponentPTKO < PotentialToKO.Risky )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"And they are likely to do big damage this turn with improved survivability. Score: {score}" );
                }
            }
            
            //--Did Wide Guard invalidate the tank's spread attack?
            //--Protection decision line. this also requires pair intent and/or threat intent
        }

        return score;
    }

    private int EvaluateDisruptiveThreat( ActionEvaluation action, ThreatProfile threat, DoomedOutcome doomed, BoardContext bc )
    {
        int score = 0;

        float sackScalar = 0.7f;
        var expendability = bc.MyExpendability;
        float sackModifier = ( 1 - expendability * sackScalar );

        var top1 = action.Top1;
        var top2 = action.Top2;

        float damageDealt = top1.Opponent.BeginningHPR - top1.Opponent_EndOfTurnHP;
        _ai.CurrentLog.Add( $"Damage Dealt to threat: {damageDealt}. Score: {score}" );

        //--This action's switch probability.
        float theySwitchProbability = _ai.UnitSim.PredictSwitchProbability( top1.Opponent.Pokemon, top1.AttackerPTKO, top1.OpponentPTKO, top1.AttackerMovedFirst, top1.Attacker.BeginningHPR, top1.Opponent.BeginningHPR, top1.Opponent.Expendability );
        score += Mathf.FloorToInt( 50f * theySwitchProbability );
        _ai.CurrentLog.Add( $"Switch Probability: {theySwitchProbability}. Score: {score}" );

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
        bool theyAreSashed = threat.ThreatUnit.Item == ItemBattleEffectID.FocusSash;
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
        bool lum = us.Item == ItemBattleEffectID.LumBerry;
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
                _ai.CurrentLog.Add( $"We move first next turn and force respect. Score: {score}" );
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
            _ai.CurrentLog.Add( $"We're looking to switch while facing down a disruptive threat." );
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
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"We don't have hazards on our side and it's early game, and we have magic bounce. Score: {score}" );
                }

                if( bfs.EntryHazardsOn_TheirSide <= 0 && ( bfs.IsEarlyGame || bfs.Round < 7 ) )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"They don't have hazards on their side and it's early game, and we have magic bounce, which will place the hazards on their side. Score: {score}" );
                }
            }
            else if( hazardSetter && candidateAdapter.RoleProfile.Traits.Contains( RoleTrait.HazardRemover ) )
            {
                switchDisruptionChecks++;

                if( bfs.EntryHazardsOn_MySide > 0 )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"Our candidate has hazard removal and we have hazards on our side of the field. Score: {score}" );
                }
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
                _ai.CurrentLog.Add( $"Our candidate has potential vulnerabilities to moves the disruptive threat posses. Score: {score}" );

                if( sleeper || taunter || encorer || prankster || knockOff )
                {
                    score -= 10;
                    _ai.CurrentLog.Add( $"These vulnerabilities are a bit extra scary. Score: {score}" );
                }

                if( vulnerabilities >= 3 )
                {
                    score -= 15;
                    _ai.CurrentLog.Add( $"And our candidate has 3 or more vulnerabilities. Score: {score}" );
                }
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
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"We have magic bounce, it's early game, and we don't have hazards on our side. Score: {score}" );
                }

                if( bfs.EntryHazardsOn_TheirSide <= 0 && ( bfs.IsEarlyGame || bfs.Round < 7 ) )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"We have magic bounce, it's early game, and they don't have hazards on their side, allowing magic bounce to provide that for us. Score: {score}" );
                }
            }
            else if( hazardSetter && us.RoleProfile.Traits.Contains( RoleTrait.HazardRemover ) )
            {
                currentMonDisruptionChecks++;

                if( bfs.EntryHazardsOn_MySide > 0 )
                {
                    score += 10;
                    _ai.CurrentLog.Add( $"We have hazard removal and hazards on our side. Score: {score}" );
                }
            }

            if( phazerDisruptive && current_PhazeImmunity )
                currentMonDisruptionChecks++;

            if( us.RoleProfile.Traits.Contains( RoleTrait.Taunt ) || us.RoleProfile.Traits.Contains( RoleTrait.Encore ) || us.RoleProfile.Traits.Contains( RoleTrait.Phazes ) )
            {
                currentMonDisruptionChecks++;
                score += 10;
                _ai.CurrentLog.Add( $"We have ways to disrupt their disruptive threat. Score: {score}" );
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
                _ai.CurrentLog.Add( $"The disruptive threat has disruptive moves we are vulnerable to. Score: {score}" );
                
                if( sleeper || taunter || encorer || prankster || knockOff )
                {
                    score -= 10;
                    _ai.CurrentLog.Add( $"These vulnerabilities are a bit extra scary. Score: {score}" );
                }

                if( vulnerabilities >= 3 )
                {
                    score -= 15;
                    _ai.CurrentLog.Add( $"And we have 3 or more of these vulnerabilities. Score: {score}" );
                }
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

        if( action.ExchangePack.OurAllyExists && top1.AttackerAlly != null )
        {
            var allyVS_Threat = action.ExchangePack.AllyVS_Threat;
            var allyVS_ThreatAfter1 = _ai.Projection.EvaluateExchange( top1.AttackerAlly, allyVS_Threat.Opponent ); //--this evaluates a post top1 ally against a pre top1 opponent to infer inter top1 exchange results

            float allyDamageBefore = Mathf.Round( ( allyVS_Threat.AttackerMTR.EstimatedDamage / allyVS_Threat.Opponent.BeginningHPR ) * 100f ) / 100f;
            float allyDamageAfter = Mathf.Round( ( allyVS_ThreatAfter1.AttackerMTR.EstimatedDamage / allyVS_Threat.Opponent.BeginningHPR ) * 100f ) / 100f;

            //--All of these checks require pair intent and threat intent
            //--Safeguard vs status spam
            //--Follow Me redirecting Taunt
            //--Follow Me redirecting Encore
            //--Ally Switch
            //--Wide Guard vs Snarl/Icy Wind/etc.
        }

        return score;
    }

    private int EvaluateConstrainingThreat( ActionEvaluation action, ThreatProfile threat, DoomedOutcome doomed, BoardContext bc )
    {
        int score = 0;

        float sackScalar = 0.7f;
        var expendability = bc.MyExpendability;
        float sackModifier = ( 1 - expendability * sackScalar );

        var top1 = action.Top1;
        var top2 = action.Top2;

        float damageDealt = top1.Opponent.BeginningHPR - top1.Opponent_EndOfTurnHP;
        _ai.CurrentLog.Add( $"Damage Dealt to threat: {damageDealt}. Score: {score}" );

        //--This action's switch probability.
        float theySwitchProbability = _ai.UnitSim.PredictSwitchProbability( top1.Opponent.Pokemon, top1.AttackerPTKO, top1.OpponentPTKO, top1.AttackerMovedFirst, top1.Attacker.BeginningHPR, top1.Opponent.BeginningHPR, top1.Opponent.Expendability );
        score += Mathf.FloorToInt( 50f * theySwitchProbability );
        _ai.CurrentLog.Add( $"Switch Probability: {theySwitchProbability}. Score: {score}" );

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

            if( top1.Opponent.RoleProfile.Traits.Contains( RoleTrait.TrappingMove ) || top1.Opponent.RoleProfile.Traits.Contains( RoleTrait.TrappingAbility ) )
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
                    score += 35;
                    _ai.CurrentLog.Add( $"Putting a constraining threat to sleep gives us freedom to handle them. Score: {score}" );
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
                _ai.CurrentLog.Add( $"We're an attrition focused unit and want to place hazards and we survive. Score: {score}" );
            }

            if( action.Type == ActionType.Setup && top1.Attacker_EndOfTurnHP > 0 )
            {
                score += 15;
                _ai.CurrentLog.Add( $"We're an attrition focused unit and want to setup and we survive. Score: {score}" );
            }

            if( action.Type == ActionType.OffensiveStatus )
            {
                score += 10;
                _ai.CurrentLog.Add( $"We're an attrition focused unit and want to use an offensive status move. Score: {score}" );
            }

            if( action.Type == ActionType.Attack && _ai.UnitSim.MoveIsPivot( action.MovePayload ) )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Using a pivot move to switch against an attrition-focused constraint threat provides unique control over it. Score: {score}" );

                if( top1.Attacker.RoleProfile.Traits.Contains( RoleTrait.FastPivot ) || top1.Attacker.RoleProfile.Traits.Contains( RoleTrait.SlowPivot ) )
                {
                    score += 5;
                    _ai.CurrentLog.Add( $"We're looking to use a damaging pivot move on a constraining threat, chipping them and escaping to better handle the situation with another Pokemon. Score: {score}" );

                    if( top2.Attacker_EndOfTurnHP > 0 )
                    {
                        if( top2.AttackerMovedFirst || top2.AttackerPTKO >= PotentialToKO.Risky )
                        {
                            score += 15;
                            _ai.CurrentLog.Add( $"Our likely pivot in survives next round and either moves first or does big damage. Score: {score}" );
                        }

                        if( !top1.AttackerMovedFirst && top1.Attacker.RoleProfile.Traits.Contains( RoleTrait.SlowPivot ) )
                        {
                            score += 10;
                            _ai.CurrentLog.Add( $"We're also slow, which allows us to safely pivot our new Pokemon in and they move first next round. Score: {score}" );

                            if( top2.AttackerMovedFirst )
                            {
                                score += 10;
                                _ai.CurrentLog.Add( $"And they move first next round. Score: {score}" );
                            }
                        }
                    }
                }
            }
        }

        return score;
    }
}
