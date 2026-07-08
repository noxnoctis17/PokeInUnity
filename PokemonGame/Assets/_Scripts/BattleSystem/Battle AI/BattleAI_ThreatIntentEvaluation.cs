using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleAI_ThreatIntentEvaluation
{
    private readonly BattleAI _ai;

    public BattleAI_ThreatIntentEvaluation( BattleAI ai )
    {
        _ai = ai;
    }

    private float ApplyIntentAdjustment( float adjustment, float evidence, float confidence )
    {
        _ai.CurrentLog.Add( $"Final Adjustment: {adjustment}, Evidence: {evidence}, Confidence: {confidence} (Formula: (Adjustment * Evidence) * Confidence, to be clamped within -75f, 75f)" );

        adjustment = Mathf.Clamp( adjustment, -75f, 75f );
        float applied = ( adjustment * evidence ) * confidence;
        
        return applied;
    }

    public int ActionVS_ThreatIntent( ActionEvaluation action, TempoStateResult tempo, ExchangePack pack, BoardContext context, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        int score = 0;

        switch( action.Type )
        {
            case ActionType.Attack:
                var attack = (MoveThreatResult)action.ActionResult;
                score += AttackVS_ThreatIntent( tempo, pack, context, attack, intentTOP, tir );
            break;

            case ActionType.DefensiveSwitch:
                var defensiveSwitch = (SwitchCandidateResult)action.ActionResult;
                score += DefensiveSwitchVS_ThreatIntent( tempo, pack, context, defensiveSwitch, intentTOP, tir );
            break;

            case ActionType.OffensiveSwitch:
                var offensiveSwitch = (SwitchCandidateResult)action.ActionResult;
                score += OffensiveSwitchVS_ThreatIntent( tempo, pack, context, offensiveSwitch, intentTOP, tir );
            break;

            case ActionType.Setup:
                var setup = (SetupThreatResult)action.ActionResult;
                score += SetupVS_ThreatIntent( tempo, pack, context, setup, intentTOP, tir );
            break;

            case ActionType.OffensiveStatus:
                // var offensiveStatus = (StatusThreatResult)action.ActionResult;
                // score += OFfensiveStatusVS_ThreatIntent( tempo, pack, context, offensiveStatus, intentTOP, tir );
            break;

            case ActionType.SupportiveStatus:
                // var supportiveStatus = (StatusThreatResult)action.ActionResult;
                // score += SupportiveStatusVS_ThreatIntent( tempo, pack, context, supportiveStatus, intentTOP, tir );
            break;
        }

        return score;
    }

//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================
//=====================================================================================[ATTACK THREAT VS INTENT]====================================================================================================
//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================

    public int AttackVS_ThreatIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, MoveThreatResult mtr, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        float score = 0f;
        int final = 0;

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"======================================" );
        _ai.CurrentLog.Add( $"=====[Attack Threat Intent Check]=====" );
        _ai.CurrentLog.Add( $"======================================" );
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"Evaluating our Attack line against their {tir.PrimaryIntent.IntentType} (Confidence: {tir.Confidence}, Evidence: {tir.PrimaryIntent.Evidence})" );

        score += tir.PrimaryIntent.IntentType switch
        {
            IntentType.Attack =>            AttackVS_AttackIntent( tempo, pack, context, mtr, intentTOP, tir ),
            IntentType.DefensiveSwitch =>   AttackVS_DefensiveSwitchIntent( tempo, pack, context, mtr, intentTOP, tir ),
            IntentType.OffensiveSwitch =>   AttackVS_OffensiveSwitchIntent( tempo, pack, context, mtr, intentTOP, tir ),
            IntentType.Setup =>             AttackVS_SetupIntent( tempo, pack, context, mtr, intentTOP, tir ),
            IntentType.OffensiveStatus =>   AttackVS_OffensiveStatusIntent( tempo, pack, context, mtr, intentTOP, tir ),
            IntentType.SupportiveStatus =>  AttackVS_SupportiveStatusIntent( tempo, pack, context, mtr, intentTOP, tir ),
            IntentType.Protect =>           AttackVS_ProtectIntent( tempo, pack, context, mtr, intentTOP, tir ),
            _ => 0f,
        };

        final = Mathf.RoundToInt( score );
        _ai.CurrentLog.Add( $"Final Score: {final}" );
        _ai.CurrentLog.Add( $"" );

        return final;
    }

    private float AttackVS_AttackIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, MoveThreatResult mtr, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Attack vs Attack Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var ourRP = _ai.CurrentUnitAdapter.RoleProfile;

        var move = mtr.Move;

        bool iAmFaster = usVS_Threat.AttackerMovesFirst;
        bool iThreatenKO = usVS_Threat.AttackerThreatensKO;
        bool theyThreatenKO = usVS_Threat.OpponentThreatensKO;

        bool weAreOffensive = ourRP.PrimaryRole == RoleClass.BulkyAttacker || ourRP.PrimaryRole == RoleClass.RevengeKiller || ourRP.PrimaryRole == RoleClass.SetupSweeper ||
            ourRP.PrimaryRole == RoleClass.Sweeper || ourRP.PrimaryRole == RoleClass.TrickRoomAbuser || ourRP.PrimaryRole == RoleClass.WallBreaker;

        var theirAttack = (MoveThreatResult)tir.PrimaryIntent.IntentResult;
        var theirMove = theirAttack.Move;

        if( weAreOffensive )
        {
            if( iAmFaster && iThreatenKO )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"We are an offensive unit. We are faster and threaten a KO. Adjustment: {adjustment}" );
            }
            else if( _ai.CurrentUnitAdapter.CurrentHPR <= 0.45 && usVS_Threat.OpponentThreatensKO && !iAmFaster )
            {
                adjustment -= 25f;
                _ai.CurrentLog.Add( $"We are an offensive unit. We have less than 45% hp, the opponent threatens a KO, and we are slower than them. Adjustment: {adjustment}" );
            }
        }

        if( theirMove.MoveSO.DrainPercentage > 0f )
        {
            adjustment += 15f;

            _ai.CurrentLog.Add( $"They are looking to use a drain move, we should try to offset it. Adjustment: {adjustment}" );

            if( iAmFaster )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"We're faster, so we get damage in before they receive healing. Adjustment: {adjustment}" );
            }
        }

        if( iAmFaster && iThreatenKO && theyThreatenKO )
        {
            adjustment += 25f;
            _ai.CurrentLog.Add( $"We both threaten KOs on each other but we're faster, we should remove them. Adjustment: {adjustment}" );
        }

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float AttackVS_DefensiveSwitchIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, MoveThreatResult mtr, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Attack vs Defensive Switch Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var ourRP = _ai.CurrentUnitAdapter.RoleProfile;
        var move = mtr.Move;

        bool iAmFaster = usVS_Threat.AttackerMovesFirst;
        bool iThreatenKO = usVS_Threat.AttackerThreatensKO;
        bool theyThreatenKO = usVS_Threat.OpponentThreatensKO;

        bool weAreOffensive = ourRP.PrimaryRole == RoleClass.BulkyAttacker || ourRP.PrimaryRole == RoleClass.RevengeKiller || ourRP.PrimaryRole == RoleClass.SetupSweeper ||
            ourRP.PrimaryRole == RoleClass.Sweeper || ourRP.PrimaryRole == RoleClass.TrickRoomAbuser || ourRP.PrimaryRole == RoleClass.WallBreaker;

        if( weAreOffensive )
        {
            var theirDefSwitch = (SwitchCandidateResult)tir.PrimaryIntent.IntentResult;
            var theirDefCandidate = _ai.GetPokemonAs_Adapter( theirDefSwitch.Pokemon );

            adjustment += 15f;
            _ai.CurrentLog.Add( $"We are an offensive unit. We should pressure their defensive candidate with chip. Final Reasoning may even allow a coverage move downstream. Adjustment: {adjustment}" );

            float ourModDefSwitch = TypeChart.GetTotalMoveEffectiveness( theirDefCandidate.Type, move );
            if( ourModDefSwitch >= 1.5f )
            {
                adjustment += 20f;
                _ai.CurrentLog.Add( $"We have a 1.5x damage modifier on their defensive candidate somehow, attacking may be highly rewarding. Adjustment: {adjustment}" );
            }
        }

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float AttackVS_OffensiveSwitchIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, MoveThreatResult mtr, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Attack vs Offensive Switch Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var ourRP = _ai.CurrentUnitAdapter.RoleProfile;
        var move = mtr.Move;

        bool iAmFaster = usVS_Threat.AttackerMovesFirst;
        bool iThreatenKO = usVS_Threat.AttackerThreatensKO;
        bool theyThreatenKO = usVS_Threat.OpponentThreatensKO;

        bool weAreOffensive = ourRP.PrimaryRole == RoleClass.BulkyAttacker || ourRP.PrimaryRole == RoleClass.RevengeKiller || ourRP.PrimaryRole == RoleClass.SetupSweeper ||
            ourRP.PrimaryRole == RoleClass.Sweeper || ourRP.PrimaryRole == RoleClass.TrickRoomAbuser || ourRP.PrimaryRole == RoleClass.WallBreaker;

        var theirOffSwitch = (SwitchCandidateResult)tir.PrimaryIntent.IntentResult;
        var theirOffCandidate = _ai.GetPokemonAs_Adapter( theirOffSwitch.Pokemon );
        var offSwitchEE = _ai.Projection.EvaluateExchange( _ai.CurrentUnitAdapter, theirOffCandidate );

        if( weAreOffensive )
        {
            adjustment += 15f;
            _ai.CurrentLog.Add( $"We are an offensive unit. We should pressure their offensive candidate with chip. Adjustment: {adjustment}" );
        }
        
        float ourModOffSwitch = TypeChart.GetTotalMoveEffectiveness( theirOffCandidate.Type, move );

        if( ourModOffSwitch <= 0f )
        {
            adjustment -= 40f;
            _ai.CurrentLog.Add( $"We have a modifier of 0 on their offensive candidate, so they are immune to our attack. Perhaps we can do something else? Adjustment: {adjustment}" );
        }
        else if( ourModOffSwitch >= 4f )
        {
            adjustment += 30f;
            _ai.CurrentLog.Add( $"We have >= 4x multiplier on their offensive candidate. They risk massive damage to bring it in. Adjustment: {adjustment}" );
        }
        else if( ourModOffSwitch >= 3f )
        {
            adjustment += 25f;
            _ai.CurrentLog.Add( $"We have >= 3x multiplier on their offensive candidate. They risk big damage to bring it in. Adjustment: {adjustment}" );
        }
        else if( ourModOffSwitch >= 1.5f )
        {
            adjustment += 20f;
            _ai.CurrentLog.Add( $"We have >= 1.5x multiplier on their offensive candidate. They risk heavy damage to bring it in. Adjustment: {adjustment}" );
        }

        if( offSwitchEE.AttackerThreatensKO )
        {
            adjustment += 10f;
            _ai.CurrentLog.Add( $"If we attack now, we'll threaten their offensive candidate with a KO next turn. Adjustment: {adjustment}" );
        }

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float AttackVS_SetupIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, MoveThreatResult mtr, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Attack vs Setup Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var ourRP = _ai.CurrentUnitAdapter.RoleProfile;
        var move = mtr.Move;

        bool iAmFaster = usVS_Threat.AttackerMovesFirst;
        bool iThreatenKO = usVS_Threat.AttackerThreatensKO;
        bool theyThreatenKO = usVS_Threat.OpponentThreatensKO;

        bool weAreOffensive = ourRP.PrimaryRole == RoleClass.BulkyAttacker || ourRP.PrimaryRole == RoleClass.RevengeKiller || ourRP.PrimaryRole == RoleClass.SetupSweeper ||
            ourRP.PrimaryRole == RoleClass.Sweeper || ourRP.PrimaryRole == RoleClass.TrickRoomAbuser || ourRP.PrimaryRole == RoleClass.WallBreaker;

        var theirSetup = (SetupThreatResult)tir.PrimaryIntent.IntentResult;
        var delta = theirSetup.StageDelta;

        bool theyGainedSpeedControl = delta.Speed > 0 && tir.Threat.Speed < intentTOP.Opponent.Speed;

        //--Flat push
        if( iAmFaster && usVS_Threat.AttackerPTKOR.PTKO >= PotentialToKO.Risky )
        {
            adjustment += 20f;
            _ai.CurrentLog.Add( $"We're faster than them, getting chip in while they waste a turn setting up is appropriate. Adjustment: {adjustment}" );
        }
        else if( usVS_Threat.AttackerPTKOR.PTKO >= PotentialToKO.Risky )
        {
            adjustment += 10f;
            _ai.CurrentLog.Add( $"Getting chip in while they waste a turn setting up is appropriate. Adjustment: {adjustment}" );
        }

        if( ourRP.PrimaryRole == RoleClass.Wall )
        {
            adjustment += 15f;
            _ai.CurrentLog.Add( $"We're a wall, and so they think they can set up in our face. We should get chip in. Adjustment: {adjustment}" );

            if( theirSetup.AfterPTKOR.PTKO > usVS_Threat.AttackerPTKOR.PTKO )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"If they setup, their PTKO on us is greater than ours on them. Adjustment: {adjustment}" );
            }

            if( theirSetup.BeforePTKOR.PTKO < theirSetup.AfterPTKOR.PTKO )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"Their current PTKO on us is less than their PTKO after they set up. Adjustment: {adjustment}" );
            }

            if( theyGainedSpeedControl )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"They gain reasonable speed over us. Adjustment: {adjustment}" );
            }

            if( ourRP.Biases.Contains( RoleBias.PhysicallyBulky ) )
            {
                if( delta.SpAttack > 1 )
                {
                    adjustment += 20f;
                    _ai.CurrentLog.Add( $"We're Physically Bulky and they are boosting their special attack by 2, which is our weaker axis. Adjustment: {adjustment}" );
                }
                else if( delta.SpAttack > 0 )
                {
                    adjustment += 10f;
                    _ai.CurrentLog.Add( $"We're Physically Bulky and they are boosting their special attack by 1, which is our weaker axis. Adjustment: {adjustment}" );
                }
            }

            if( ourRP.Biases.Contains( RoleBias.SpeciallyBulky ) )
            {
                if( delta.Attack > 1 )
                {
                    adjustment += 20f;
                    _ai.CurrentLog.Add( $"We're Physically Bulky and they are boosting their attack by 2, which is our weaker axis. Adjustment: {adjustment}" );
                }
                else if( delta.Attack > 0 )
                {
                    adjustment += 10f;
                    _ai.CurrentLog.Add( $"We're Physically Bulky and they are boosting their attack by 1, which is our weaker axis. Adjustment: {adjustment}" );
                }
            }
        }

        if( weAreOffensive )
        {
            if( ourRP.Biases.Contains( RoleBias.Physical ) )
            {
                if( delta.Defense > 1 )
                {
                    adjustment += 20f;
                    _ai.CurrentLog.Add( $"We're Physically Offensive and they are boosting their defense by 2, which offsets our strength. Adjustment: {adjustment}" );
                }
                else if( delta.Defense > 0 )
                {
                    adjustment += 10f;
                    _ai.CurrentLog.Add( $"We're Physically Offensive and they are boosting their defense by 1, which offsets our strength. Adjustment: {adjustment}" );
                }
            }

            if( ourRP.Biases.Contains( RoleBias.Special ) )
            {
                if( delta.SpDefense > 1 )
                {
                    adjustment += 20f;
                    _ai.CurrentLog.Add( $"We're Specially Offensive and they are boosting their special defense by 2, which offsets our strength. Adjustment: {adjustment}" );
                }
                else if( delta.SpDefense > 0 )
                {
                    adjustment += 10f;
                    _ai.CurrentLog.Add( $"We're Specially Offensive and they are boosting their special defense by 1, which offsets our strength. Adjustment: {adjustment}" );
                }
            }
        }

        if( theirSetup.SweepCount >= 3 )
        {
            adjustment += 15;
            _ai.CurrentLog.Add( $"Their sweep count is {theirSetup.SweepCount}. Adjustment: {adjustment}" );

            if( iThreatenKO )
            {
                adjustment += 15;
                _ai.CurrentLog.Add( $"We threaten a KO on them this turn. Adjustment: {adjustment}" );

                if( iAmFaster )
                {
                    adjustment += 15;
                    _ai.CurrentLog.Add( $"We're also faster. Adjustment: {adjustment}" );
                }
            }
        }

        if( theirSetup.ImprovedPTKOs >= 2 )
        {
            adjustment += 15;
            _ai.CurrentLog.Add( $"They improve their PTKO delta on us by {theirSetup.ImprovedPTKOs}. Adjustment: {adjustment}" );

            if( iThreatenKO )
            {
                adjustment += 15;
                _ai.CurrentLog.Add( $"We threaten a KO on them this turn. Adjustment: {adjustment}" );

                if( iAmFaster )
                {
                    adjustment += 15;
                    _ai.CurrentLog.Add( $"We're also faster. Adjustment: {adjustment}" );
                }
            }
        }

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float AttackVS_OffensiveStatusIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, MoveThreatResult mtr, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Attack vs Offensive Status Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var ourRP = _ai.CurrentUnitAdapter.RoleProfile;
        var move = mtr.Move;

        bool iAmFaster = usVS_Threat.AttackerMovesFirst;
        bool iThreatenKO = usVS_Threat.AttackerThreatensKO;
        bool theyThreatenKO = usVS_Threat.OpponentThreatensKO;

        bool weAreOffensive = ourRP.PrimaryRole == RoleClass.BulkyAttacker || ourRP.PrimaryRole == RoleClass.RevengeKiller || ourRP.PrimaryRole == RoleClass.SetupSweeper ||
            ourRP.PrimaryRole == RoleClass.Sweeper || ourRP.PrimaryRole == RoleClass.TrickRoomAbuser || ourRP.PrimaryRole == RoleClass.WallBreaker;

        var theirOffStatus = (StatusThreatResult)tir.PrimaryIntent.IntentResult;
        var offStatusMove = theirOffStatus.Move;
        var offStatusMoveEffects = offStatusMove.MoveSO.MoveEffects;
        var subType = theirOffStatus.OffensiveStatusType;
        var ourTraits = ourRP.Traits;
        var us = _ai.CurrentUnitAdapter;

        if( subType == OffensiveStatusType.StatusEffect || subType == OffensiveStatusType.Disruption )
        {
            _ai.CurrentLog.Add( $"They are going to use an offensive status move that causes a status effect." );

            var moveEffects = theirOffStatus.Move.MoveSO.MoveEffects;
            
            bool theyBurn = moveEffects.SevereStatus == SevereConditionID.BRN;
            bool theyFrost = moveEffects.SevereStatus == SevereConditionID.FBT;
            bool theyPoison = moveEffects.SevereStatus == SevereConditionID.PSN;
            bool theyToxic = moveEffects.SevereStatus == SevereConditionID.TOX;
            bool theyParalyze = moveEffects.SevereStatus == SevereConditionID.PAR;
            bool theySleep = moveEffects.SevereStatus == SevereConditionID.SLP;

            bool theyTaunt = moveEffects.VolatileStatus == VolatileConditionID.Taunt;
            bool theyEncore = moveEffects.VolatileStatus == VolatileConditionID.Encore;
            bool theyHealBlock = moveEffects.VolatileStatus == VolatileConditionID.HealBlocked;
            bool theyDisable = moveEffects.VolatileStatus == VolatileConditionID.Disabled;

            bool weAreBurnWeak = ourTraits.Contains( RoleTrait.BurnWeak );
            bool weAreFrostWeak = ourTraits.Contains( RoleTrait.FrostWeak );
            bool weAreToxicWeak = ourTraits.Contains( RoleTrait.ToxicWeak );
            bool weAreParalysisWeak = ourTraits.Contains( RoleTrait.ParalysisWeak  );

            bool wePassiveRecover = ourTraits.Contains( RoleTrait.RecoveryItem ) || ourTraits.Contains( RoleTrait.RecoveryAbility );
            bool weHaveSetup = ourRP.Traits.Contains( RoleTrait.PhysicallyOffensiveSetup ) || ourRP.Traits.Contains( RoleTrait.SpeciallyOffensiveSetup ) || ourRP.Traits.Contains( RoleTrait.PhysicallyDefensiveSetup ) || ourRP.Traits.Contains( RoleTrait.SpeciallyDefensiveSetup );

            bool weAreTauntWeak = ourTraits.Contains( RoleTrait.TauntWeak );
            bool weAreEncoreWeak = ourTraits.Contains( RoleTrait.EncoreWeak );
            bool weAreHealBlockWeak = ourTraits.Contains( RoleTrait.RecoveryMove );
            bool weAreDisableWeak = us.VolatileStatuses.Contains( VolatileConditionID.ChoiceLocked ) || ourRP.Signals.PhysicalAttackCount < 2 || ourRP.Signals.SpecialAttackCount < 2;

            if( us.SevereStatus == SevereConditionID.None )
            {
                if( theyBurn && weAreBurnWeak )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"They burn and we are burn weak. Adjustment: {adjustment}" );

                    if( iAmFaster && iThreatenKO )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"But we are faster than them and threaten a KO. Adjustment: {adjustment}" );
                    }
                }

                if( theyFrost && weAreFrostWeak )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"They frostbite and we are frostbite weak. Adjustment: {adjustment}" );
                    
                    if( iAmFaster && iThreatenKO )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"But we are faster and threaten a KO. Adjustment: {adjustment}" );
                    }
                }

                if( theyPoison && wePassiveRecover )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"They poison and we have passive recovery, which negates each other. Adjustment: {adjustment}" );

                    if( iAmFaster && iThreatenKO )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"But we are faster and threaten a KO. Adjustment: {adjustment}" );
                    }
                }

                if( theyToxic && weAreToxicWeak )
                {
                    if( us.CurrentHPR >= 0.6f )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"They toxic and we are burn toxic weak with more than 60% hp remaining. Adjustment: {adjustment}" );
                    }
                    else
                    {
                        adjustment += 10f;
                        _ai.CurrentLog.Add( $"They toxic and we are toxic weak with less than 60% hp remaining. Adjustment: {adjustment}" );
                    }

                    if( iAmFaster && iThreatenKO )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"But we are faster and threaten a KO. Adjustment: {adjustment}" );
                    }
                }

                if( theyParalyze && weAreParalysisWeak )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"They paralyze and we are paralyze weak. Adjustment: {adjustment}" );

                    if( iAmFaster && iThreatenKO )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"But we are faster and threaten a KO. Adjustment: {adjustment}" );
                    }
                }

                if( theySleep && ( iAmFaster || usVS_Threat.AttackerPTKOR.PTKO <= PotentialToKO.TwoHKO ) )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"They sleep and we are either faster or do not threaten much. Adjustment: {adjustment}" );

                    if( iAmFaster && iThreatenKO )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"But we are faster and threaten a KO. Adjustment: {adjustment}" );
                    }
                }
            }

            if( theyTaunt && weAreTauntWeak && !us.VolatileStatuses.Contains( VolatileConditionID.Taunt ) )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"They taunt and we are taunt weak. Adjustment: {adjustment}" );

                if( ourRP.PrimaryRole == RoleClass.SetupSweeper || weHaveSetup )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"We are a setup sweeper which presents extra danger. We should try to remove their taunter. Adjustment: {adjustment}" );

                    if( iThreatenKO )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"But we are faster and threaten a KO. Adjustment: {adjustment}" );
                    }
                }
            }

            if( theyEncore && weAreEncoreWeak && !us.VolatileStatuses.Contains( VolatileConditionID.Encore ) )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"They encore and we are encore weak. Adjustment: {adjustment}" );

                if( iAmFaster && iThreatenKO )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"But we are faster and threaten a KO. Adjustment: {adjustment}" );
                    }
            }

            if( theyHealBlock && weAreHealBlockWeak && !us.VolatileStatuses.Contains( VolatileConditionID.HealBlocked ) )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"They heal block and we are heal block weak. Adjustment: {adjustment}" );

                if( iAmFaster && iThreatenKO )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"But we are faster and threaten a KO. Adjustment: {adjustment}" );
                    }
            }

            if( theyDisable && weAreDisableWeak && !us.VolatileStatuses.Contains( VolatileConditionID.Disabled ) )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"They disable and we are disable weak. Adjustment: {adjustment}" );

                if( iAmFaster && iThreatenKO )
                {
                    adjustment += 10f;
                    _ai.CurrentLog.Add( $"But we are faster and threaten a KO. Adjustment: {adjustment}" );
                }
                else
                {
                    adjustment -= 25f;
                    _ai.CurrentLog.Add( $"And since we're not faster they will disable our choice-locked move forcing us to struggle, discouraging attack. Adjustment: {adjustment}" );
                }
            }
        }

        if( subType == OffensiveStatusType.EntryHazard )
        {
            _ai.CurrentLog.Add( $"They are going to use an offensive status move that sets an entry hazard." );

            bool rocks = offStatusMoveEffects.CourtCondition == CourtConditionID.StealthRock;
            bool spikes = offStatusMoveEffects.CourtCondition == CourtConditionID.Spikes;
            bool toxicSpikes = offStatusMoveEffects.CourtCondition == CourtConditionID.ToxicSpikes;
            bool web = offStatusMoveEffects.CourtCondition == CourtConditionID.StickyWeb;
            bool seeds = offStatusMoveEffects.CourtCondition == CourtConditionID.LeechSeed;

            var winconRP = _ai.GetPokemonAs_Adapter( _ai.Blackboard.GamePlan.OurPrimaryWinCon ).RoleProfile;

            PokemonType one = _ai.Blackboard.GamePlan.OurPrimaryWinCon.PokeSO.Type1;
            PokemonType two = _ai.Blackboard.GamePlan.OurPrimaryWinCon.PokeSO.Type2;
            float winconRocksMod = TypeChart.GetTotalEffectiveness( PokemonType.Rock, one, two );

            adjustment += 10f;

            if( iThreatenKO )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"We threaten a KO. Adjustment: {adjustment}" );

                if( seeds )
                {
                    adjustment += 15;
                    _ai.CurrentLog.Add( $"They are trying to apply leech seed, which creates long term draining pressure and should be dealt with. Adjustment: {adjustment}" );
                }

                if( iAmFaster )
                {
                    adjustment += 25f;
                    _ai.CurrentLog.Add( $"We are faster. Adjustment: {adjustment}" );
                }
            }
            
            if( rocks && winconRocksMod > 1f )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"They are trying to set stealth rock and our wincon is weak to it. Adjustment: {adjustment}" );

                if( winconRocksMod > 2 )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"Our wincon is extra weak to rocks. Adjustment: {adjustment}" );
                }

                if( iThreatenKO )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"We threaten a KO. Adjustment: {adjustment}" );

                    if( iAmFaster )
                    {
                        adjustment += 25f;
                        _ai.CurrentLog.Add( $"And we are faster. Adjustment: {adjustment}" );
                    }
                }
            }

            if( web && winconRP.Traits.Contains( RoleTrait.ParalysisWeak ) )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"They are trying to set sticky web and our wincon is weakened by it. Adjustment: {adjustment}" );
                

                if( iThreatenKO )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"We threaten a KO. Adjustment: {adjustment}" );

                    if( iAmFaster )
                    {
                        adjustment += 25f;
                        _ai.CurrentLog.Add( $"And we are faster. Adjustment: {adjustment}" );
                    }
                }
            }
        }

        if( subType == OffensiveStatusType.Phaze )
        {
            _ai.CurrentLog.Add( $"They are looking to phaze us." );

            if( iAmFaster )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"But we are faster. Adjustment: {adjustment}" );

                if( iThreatenKO )
                {
                    adjustment += 25f;
                    _ai.CurrentLog.Add( $"And we threaten a KO. Adjustment: {adjustment}" );
                }
            }
        }

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float AttackVS_SupportiveStatusIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, MoveThreatResult mtr, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Attack vs Supportive Status Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var ourRP = _ai.CurrentUnitAdapter.RoleProfile;
        var move = mtr.Move;

        bool iAmFaster = usVS_Threat.AttackerMovesFirst;
        bool iThreatenKO = usVS_Threat.AttackerThreatensKO;
        bool theyThreatenKO = usVS_Threat.OpponentThreatensKO;

        bool weAreOffensive = ourRP.PrimaryRole == RoleClass.BulkyAttacker || ourRP.PrimaryRole == RoleClass.RevengeKiller || ourRP.PrimaryRole == RoleClass.SetupSweeper ||
            ourRP.PrimaryRole == RoleClass.Sweeper || ourRP.PrimaryRole == RoleClass.TrickRoomAbuser || ourRP.PrimaryRole == RoleClass.WallBreaker;

        adjustment += 30f;

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float AttackVS_ProtectIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, MoveThreatResult mtr, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Attack vs Protect Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var ourRP = _ai.CurrentUnitAdapter.RoleProfile;
        var move = mtr.Move;

        bool iAmFaster = usVS_Threat.AttackerMovesFirst;
        bool iThreatenKO = usVS_Threat.AttackerThreatensKO;
        bool theyThreatenKO = usVS_Threat.OpponentThreatensKO;

        bool weAreOffensive = ourRP.PrimaryRole == RoleClass.BulkyAttacker || ourRP.PrimaryRole == RoleClass.RevengeKiller || ourRP.PrimaryRole == RoleClass.SetupSweeper ||
            ourRP.PrimaryRole == RoleClass.Sweeper || ourRP.PrimaryRole == RoleClass.TrickRoomAbuser || ourRP.PrimaryRole == RoleClass.WallBreaker;

        if( mtr.Move.MoveSO.Name == "Feint" )
        {
            adjustment += 35f;
        }
        else
        {
            adjustment -= 50f;
        }

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================
//===================================================================================[DEFENSIVE SWITCH VS THREAT INTENT]============================================================================================
//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================

    public int DefensiveSwitchVS_ThreatIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        float score = 0f;
        int final = 0;

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"================================================" );
        _ai.CurrentLog.Add( $"=====[Defensive Switch Threat Intent Check]=====" );
        _ai.CurrentLog.Add( $"================================================" );
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"Evaluating our Defensive Switch line against their {tir.PrimaryIntent.IntentType} (Confidence: {tir.Confidence}, Evidence: {tir.PrimaryIntent.Evidence})" );

        score += tir.PrimaryIntent.IntentType switch
        {
            IntentType.Attack =>            DefensiveSwitchVS_AttackIntent( tempo, pack, context, switchCandidate, intentTOP, tir ),
            IntentType.DefensiveSwitch =>   DefensiveSwitchVS_DefensiveSwitchIntent( tempo, pack, context, switchCandidate, intentTOP, tir ),
            IntentType.OffensiveSwitch =>   DefensiveSwitchVS_OffensiveSwitchIntent( tempo, pack, context, switchCandidate, intentTOP, tir ),
            IntentType.Setup =>             DefensiveSwitchVS_SetupIntent( tempo, pack, context, switchCandidate, intentTOP, tir ),
            IntentType.OffensiveStatus =>   DefensiveSwitchVS_OffensiveStatusIntent( tempo, pack, context, switchCandidate, intentTOP, tir ),
            IntentType.SupportiveStatus =>  DefensiveSwitchVS_SupportiveStatusIntent( tempo, pack, context, switchCandidate, intentTOP, tir ),
            IntentType.Protect =>           DefensiveSwitchVS_ProtectIntent( tempo, pack, context, switchCandidate, intentTOP, tir ),
            _ => 0f,
        };

        final = Mathf.RoundToInt( score );
        _ai.CurrentLog.Add( $"Final Score: {final}" );
        _ai.CurrentLog.Add( $"" );

        return Mathf.RoundToInt( score );
    }

    private float DefensiveSwitchVS_AttackIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Defensive Switch vs Attack Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var theirMTR = (MoveThreatResult)tir.PrimaryIntent.IntentResult;
        var theirMove = theirMTR.Move;
        var theirRP = tir.Threat.RoleProfile;
        var theirBiases = theirRP.Biases;
        var theirTraits = theirRP.Traits;

        bool theyAreOffensive = theirRP.PrimaryRole == RoleClass.BulkyAttacker || theirRP.PrimaryRole == RoleClass.RevengeKiller || theirRP.PrimaryRole == RoleClass.SetupSweeper ||
            theirRP.PrimaryRole == RoleClass.Sweeper || theirRP.PrimaryRole == RoleClass.TrickRoomAbuser || theirRP.PrimaryRole == RoleClass.WallBreaker;

        bool theyArePhysical = theirBiases.Contains( RoleBias.Physical );
        bool theyAreSpecial = theirBiases.Contains( RoleBias.Special );

        var ourCand = _ai.GetPokemonAs_Adapter( switchCandidate.Pokemon );
        var candRP = ourCand.RoleProfile;
        var candTraits = candRP.Traits;

        bool knockOff = theirMove.MoveSO.Name == "Knock Off";

        if( knockOff )
        {
            adjustment -= 15f;
            _ai.CurrentLog.Add( $"Switching into an incoming knock off is bad lol. Adjustment: {adjustment}" );

            if( candRP.PrimaryRole == RoleClass.Wall && candTraits.Contains( RoleTrait.RecoveryItem ) )
            {
                adjustment -= 15f;
                _ai.CurrentLog.Add( $"And we lose a critical item to our role. Adjustment: {adjustment}" );
            }

            bool offensiveItem = ourCand.Item == BattleItemEffectID.ChoiceBand || ourCand.Item == BattleItemEffectID.ChoiceSpecs || ourCand.Item == BattleItemEffectID.LifeOrb; //--make item class a role trait.
            if( offensiveItem )
            {
                adjustment -= 15f;
                _ai.CurrentLog.Add( $"And we lose a critical item to our role. Adjustment: {adjustment}" );
            }
        }

        if( theyArePhysical && candTraits.Contains( RoleTrait.IntimidateSupport ) )
        {
            adjustment += 15f;
            _ai.CurrentLog.Add( $"They are physically oriented and our candidate has Intimidate. Adjustment: {adjustment}" );

            if( theyAreOffensive )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"And they are offensive, crippling their role as well. Adjustment: {adjustment}" );
            }
        }

        if( theyAreSpecial && candTraits.Contains( RoleTrait.DemoralizeSupport ) )
        {
            adjustment += 15;
            _ai.CurrentLog.Add( $"They are specially oriented and our candidate has Demoralize. Adjustment: {adjustment}" );

            if( theyAreOffensive )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"And they are offensive, crippling their role as well. Adjustment: {adjustment}" );
            }
        }

        //--Ability checks for immunities to assumed attack will go here

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float DefensiveSwitchVS_DefensiveSwitchIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Defensive Switch vs Defensive Switch Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var switchEE = _ai.Projection.EvaluateExchange( intentTOP.Attacker, intentTOP.Opponent );

        //--Discourage defensively switching against a defensive switch. this creates a random match up. lightly evaluating that match up.
        if( switchEE.AttackerPTKOR.PTKO > switchEE.OpponentPTKOR.PTKO || switchEE.OpponentPTKOR.PTKO < usVS_Threat.OpponentPTKOR.PTKO )
        {
            adjustment += 25f;
            _ai.CurrentLog.Add( $"Our Defensive candidate has a better PTKO than their defensive candidate, or, their defensive candidate has a worse ptko than their current pokemon. Adjustment: {adjustment}" );
        }

        if( switchEE.AttackerThreatensKO && switchEE.AttackerMovesFirst )
        {
            adjustment += 15f;
            _ai.CurrentLog.Add( $"Our defensive candidate threatens a KO and moves first. Adjustment: {adjustment}" );
        }

        if( switchEE.OpponentThreatensKO && switchEE.OpponentMovesFirst )
        {
            adjustment -= 35f;
            _ai.CurrentLog.Add( $"Their defensive candidate threatens a KO and moves first. Adjustment: {adjustment}" );
        }

        if( switchEE.OpponentSwitches && !switchEE.AttackerSwitches )
        {
            adjustment += 30f * switchEE.OpponentSwitchProbability;
            _ai.CurrentLog.Add( $"Our defensive candidate may force them to switch. Adjustment: {adjustment}" );
        }
        else if( switchEE.AttackerSwitches )
        {
            adjustment -= 50f * switchEE.AttackerSwitchProbability;
            _ai.CurrentLog.Add( $"Their defensive candidate may force us to switch. Adjustment: {adjustment}" );
        }

        _ai.CurrentLog.Add( $"Further match up specific checks will be added here in the future. Ability interactions, weather wars, role comparisons, etc. Adjustment: {adjustment}" );

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float DefensiveSwitchVS_OffensiveSwitchIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Defensive Switch vs Offensive Switch Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var cand = _ai.GetPokemonAs_Adapter( switchCandidate.Pokemon );
        var candRP = cand.RoleProfile;

        //--We use the opponent from intent top because this should include the results of our opponent setting up, properly evaluating an attack exchange next turn.
        var switchEE = _ai.Projection.EvaluateExchange( cand, intentTOP.Opponent );

        if( switchEE.OpponentThreatensKO && switchEE.OpponentMovesFirst )
        {
            adjustment -= 30f;
            _ai.CurrentLog.Add( $"Their offensive candidate threatens a KO on our defensive candidate, and they move first. Adjustment: {adjustment}" );
        }

        if( switchEE.AttackerThreatensKO && switchEE.AttackerMovesFirst )
        {
            adjustment += 25f;
            _ai.CurrentLog.Add( $"Our defensive candidate threatens a KO on their offensive candidate, and we move first. Adjustment: {adjustment}" );
        }

        if( switchEE.OpponentSwitches && !switchEE.AttackerSwitches )
        {
            adjustment += 15;
            _ai.CurrentLog.Add( $"We might force them to switch. Adjustment: {adjustment}" );
        }
        else if( switchEE.AttackerSwitches )
        {
            adjustment -= 25;
            _ai.CurrentLog.Add( $"They might force us to switch Adjustment: {adjustment}" );
        }

        if( switchEE.OpponentPTKOR.PTKO <= PotentialToKO.Safe )
        {
            adjustment += 15f;
            _ai.CurrentLog.Add( $"Our defensive candidate walls their offensive candidate very well. Adjustment: {adjustment}" );

            if( switchEE.AttackerMovesFirst )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"And our candidate moves first. Adjustment: {adjustment}" );
            }
        }
        if( switchEE.OpponentPTKOR.PTKO <= PotentialToKO.Risky )
        {
            adjustment += 15f;
            _ai.CurrentLog.Add( $"Our defensive candidate walls their offensive candidate decently. Adjustment: {adjustment}" );

            if( switchEE.AttackerMovesFirst )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"And our candidate moves first. Adjustment: {adjustment}" );
            }
        }

        _ai.CurrentLog.Add( $"Further match up specific checks will be added here in the future. Ability interactions, weather wars, role comparisons, etc. Adjustment: {adjustment}" );

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float DefensiveSwitchVS_SetupIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Defensive Switch vs Setup Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var cand = _ai.GetPokemonAs_Adapter( switchCandidate.Pokemon );
        var candRP = cand.RoleProfile;

        //--We use the opponent from intent top because this should include the results of our opponent setting up, properly evaluating an attack exchange next turn.
        var switchEE = _ai.Projection.EvaluateExchange( cand, intentTOP.Opponent );

        bool weAreOffensive = candRP.PrimaryRole == RoleClass.BulkyAttacker || candRP.PrimaryRole == RoleClass.RevengeKiller || candRP.PrimaryRole == RoleClass.SetupSweeper ||
            candRP.PrimaryRole == RoleClass.Sweeper || candRP.PrimaryRole == RoleClass.TrickRoomAbuser || candRP.PrimaryRole == RoleClass.WallBreaker;

        var theirSetup = (SetupThreatResult)tir.PrimaryIntent.IntentResult;
        var delta = theirSetup.StageDelta;

        bool theyGainedSpeed = delta.Speed > 0 && tir.Threat.Speed < intentTOP.Opponent.Speed;

        if( switchEE.OpponentThreatensKO && switchEE.OpponentMovesFirst )
        {
            adjustment -= 40f;
            _ai.CurrentLog.Add( $"They threaten a KO on our defensive candidate and move first after setting up. Adjustment: {adjustment}" );
        }

        if( switchEE.AttackerThreatensKO && switchEE.AttackerMovesFirst )
        {
            adjustment += 20f;
            _ai.CurrentLog.Add( $"Our candidate threatens a KO on them after they set up and we move first. Adjustment: {adjustment}" );

            if( weAreOffensive )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"Our candidate is also offensively oriented. Adjustment: {adjustment}" );
            }
        }

        if( theirSetup.AfterPTKOR.PTKO >= PotentialToKO.Dangerous && !switchEE.OpponentThreatensKO )
        {
            adjustment += 20f;
            _ai.CurrentLog.Add( $"They threaten a KO on our current mon after setting up, but they do not threaten a KO on our defensive candidate. Adjustment: {adjustment}" );

            if( weAreOffensive )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"Our defensive candidate is also offensively oriented. Adjustment: {adjustment}" );
            }
        }

        if( switchEE.OpponentSwitches && !switchEE.AttackerSwitches )
        {
            adjustment += 20;
            _ai.CurrentLog.Add( $"Our candidate may force them to switch next turn, invalidating their setup. Adjustment: {adjustment}" );

            if( weAreOffensive )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"Our defensive candidate is also offensively oriented. Adjustment: {adjustment}" );
            }
        }
        else if( switchEE.AttackerSwitches )
        {
            adjustment -= 25;
            _ai.CurrentLog.Add( $"They might force our defensive candidate to switch next turn. Adjustment: {adjustment}" );
        }

        if( candRP.Traits.Contains( RoleTrait.Phazes ) )
        {
            adjustment += 25f;
            _ai.CurrentLog.Add( $"Our defensive candidate has a phazing move. Adjustment: {adjustment}" );

            if( switchEE.OpponentPTKOR.PTKO <= PotentialToKO.Risky )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"We are also likely to survive the round and pull it off. Adjustment: {adjustment}" );
            }

            if( switchEE.AttackerMovesFirst )
            {
                adjustment += 25f;
                _ai.CurrentLog.Add( $"We're also faster and will get to use it before they can attack next turn. Adjustment: {adjustment}" );
            }
        }

        if( candRP.PrimaryRole == RoleClass.Wall )
        {
            adjustment += 15f;
            _ai.CurrentLog.Add( $"Our candidate is a wall. Adjustment: {adjustment}" );

            if( theirSetup.AfterPTKOR.PTKO < switchEE.OpponentPTKOR.PTKO )
            {
                adjustment += 25f;
                _ai.CurrentLog.Add( $"Our defensive candidate lowers their after-setup PTKO. Adjustment: {adjustment}" );
            }

            if( candRP.Biases.Contains( RoleBias.PhysicallyBulky ) )
            {
                if( delta.Attack > 1 )
                {
                    adjustment += 20f;
                    _ai.CurrentLog.Add( $"Our defensive candidate is physically bulky, and they are boosting their attack by more than 1 stage, we should be able to take it. Adjustment: {adjustment}" );
                }
                else if( delta.Attack > 0 )
                {
                    adjustment += 10f;
                    _ai.CurrentLog.Add( $"Our defensive candidate is physically bulky, and they are boosting their attack by 1 stage, we can probably take it. Adjustment: {adjustment}" );
                }
            }

            if( candRP.Biases.Contains( RoleBias.SpeciallyBulky ) )
            {
                if( delta.SpAttack > 1 )
                {
                    adjustment += 20f;
                    _ai.CurrentLog.Add( $"Our defensive candidate is specially bulky, and they are boosting their special attack by more than 1 stage, we should be able to take it. Adjustment: {adjustment}" );
                }
                else if( delta.SpAttack > 0 )
                {
                    adjustment += 10f;
                    _ai.CurrentLog.Add( $"Our defensive candidate is specially bulky, and they are boosting their special attack by 1 stage, we can probably take it. Adjustment: {adjustment}" );
                }
            }
        }

        if( theyGainedSpeed && candRP.Traits.Contains( RoleTrait.ParalysisPressure ) )
        {
            adjustment += 15f;
            _ai.CurrentLog.Add( $"Our defensive candidate has paralysis pressure and they gain significant speed from setting up. Adjustment: {adjustment}" );

            if( switchEE.AttackerMovesFirst || switchEE.OpponentPTKOR.PTKO <= PotentialToKO.Risky )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"Our defensive candidate also either moves first or has a risky or safer PTKO. Adjustment: {adjustment}" );
            }
        }

        if( candRP.Traits.Contains( RoleTrait.Taunt ) || candRP.Traits.Contains( RoleTrait.Encore ) )
        {
            adjustment += 15f;
            _ai.CurrentLog.Add( $"Our defensive candidate has taunt or encore pressure, which could prevent further setup or lock them down. Adjustment: {adjustment}" );

            if( switchEE.AttackerMovesFirst || switchEE.OpponentPTKOR.PTKO <= PotentialToKO.Risky )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"Our defensive candidate also either moves first or has a risky or safer PTKO. Adjustment: {adjustment}" ); 
            }
        }

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float DefensiveSwitchVS_OffensiveStatusIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Defensive Switch vs Offensive Status Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var theirOffStatus = (StatusThreatResult)tir.PrimaryIntent.IntentResult;
        var them = tir.Threat;
        var theirMove = theirOffStatus.Move;
        var subType = theirOffStatus.OffensiveStatusType;
        var offStatusMoveEffects = theirMove.MoveSO.MoveEffects;
        var theirRP = them.RoleProfile;

        var ourCand = _ai.GetPokemonAs_Adapter( switchCandidate.Pokemon );
        var candRP = ourCand.RoleProfile;
        var candTraits = candRP.Traits;

        bool ourCandIsOffensive = candRP.PrimaryRole == RoleClass.BulkyAttacker || candRP.PrimaryRole == RoleClass.RevengeKiller || candRP.PrimaryRole == RoleClass.SetupSweeper ||
            candRP.PrimaryRole == RoleClass.Sweeper || candRP.PrimaryRole == RoleClass.TrickRoomAbuser || candRP.PrimaryRole == RoleClass.WallBreaker;

        bool ourCandIsDefensive = candRP.PrimaryRole == RoleClass.Wall || candRP.PrimaryRole == RoleClass.DefensiveSetup && candRP.SecondaryRoles.Contains( RoleClass.Wall );
        bool ourCandIsUtility = !ourCandIsOffensive && !ourCandIsDefensive;

        if( subType == OffensiveStatusType.StatusEffect|| subType == OffensiveStatusType.Disruption )
        {
            _ai.CurrentLog.Add( $"They are looking to use a move that causes a status effect." );

            bool theyBurn = offStatusMoveEffects.SevereStatus == SevereConditionID.BRN;
            bool theyFrost = offStatusMoveEffects.SevereStatus == SevereConditionID.FBT;
            bool theyPoison = offStatusMoveEffects.SevereStatus == SevereConditionID.PSN;
            bool theyToxic = offStatusMoveEffects.SevereStatus == SevereConditionID.TOX;
            bool theyParalyze = offStatusMoveEffects.SevereStatus == SevereConditionID.PAR;
            bool theySleep = offStatusMoveEffects.SevereStatus == SevereConditionID.SLP;
            bool theySevere = offStatusMoveEffects.SevereStatus != SevereConditionID.None;

            bool prankster = them.Ability == AbilityID.Prankster;
            bool powderer = _ai.UnitSim.CheckHasMove( them, "Sleep Powder" ) || _ai.UnitSim.CheckHasMove( them, "Spore" ) || _ai.UnitSim.CheckHasMove( them, "Poison Powder" ) || _ai.UnitSim.CheckHasMove( them, "Stun Spore" );
            
            bool twaver = _ai.UnitSim.CheckHasMove( them, "Thunder Wave" );

            bool theyTaunt = offStatusMoveEffects.VolatileStatus == VolatileConditionID.Taunt;
            bool theyEncore = offStatusMoveEffects.VolatileStatus == VolatileConditionID.Encore;
            bool theyHealBlock = offStatusMoveEffects.VolatileStatus == VolatileConditionID.HealBlocked;
            bool theyDisable = offStatusMoveEffects.VolatileStatus == VolatileConditionID.Disabled;

            bool weAreBurnWeak = candTraits.Contains( RoleTrait.BurnWeak );
            bool weAreFrostWeak = candTraits.Contains( RoleTrait.FrostWeak );
            bool weAreToxicWeak = candTraits.Contains( RoleTrait.ToxicWeak );
            bool weAreParalysisWeak = candTraits.Contains( RoleTrait.ParalysisWeak  );

            bool candPassiveRecovers = candTraits.Contains( RoleTrait.RecoveryItem ) || candTraits.Contains( RoleTrait.RecoveryAbility );
            bool weHaveSetup = candRP.Traits.Contains( RoleTrait.PhysicallyOffensiveSetup ) || candRP.Traits.Contains( RoleTrait.SpeciallyOffensiveSetup ) || candRP.Traits.Contains( RoleTrait.PhysicallyDefensiveSetup ) || candRP.Traits.Contains( RoleTrait.SpeciallyDefensiveSetup );

            bool weAreTauntWeak = candTraits.Contains( RoleTrait.TauntWeak );
            bool weAreEncoreWeak = candTraits.Contains( RoleTrait.EncoreWeak );
            bool weAreHealBlockWeak = candTraits.Contains( RoleTrait.RecoveryMove );
            bool weAreDisableWeak = ourCand.VolatileStatuses.Contains( VolatileConditionID.ChoiceLocked ) || candRP.Signals.PhysicalAttackCount < 2 || candRP.Signals.SpecialAttackCount < 2;

            //--Prankster Immunity
            if( prankster && _ai.UnitSim.CheckTypes( PokemonType.Dark, ourCand ) )
            {
                adjustment += 30f;
                _ai.CurrentLog.Add( $"Their status move is going to activate their Prankster ability! Our defensive candidate is a dark type, which will prevent the move from going off! Adjustment: {adjustment}" );

                if( theirRP.PrimaryRole == RoleClass.UtilitySupport || theirRP.PrimaryRole == RoleClass.Disrupter )
                {
                    adjustment += 20f;
                    _ai.CurrentLog.Add( $"They are a utility/disruption class mon, shutting down prankster likely locks down their set. Adjustment: {adjustment}" );
                }
            }

            if( theySevere && ourCand.SevereStatus != SevereConditionID.None )
            {
                adjustment += 25f;
                _ai.CurrentLog.Add( $"Our defensive candidate is already effected by a severe status, which will nullify their attempt to place a severe status on us. Adjustment: {adjustment}" );
            }
            else if( theySevere )
            {
                //--Severe Status immunity/absorption checks
                if( theyBurn && candTraits.Contains( RoleTrait.BurnImmune ) )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"They burn and our defensive candidate is burn immune. Adjustment: {adjustment}" );

                    if( ourCand.Ability == AbilityID.FlashFire )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"Our defensive candidate's ability is also flash fire. Adjustment: {adjustment}" );
                    }
                }

                if( theyFrost && candTraits.Contains( RoleTrait.FrostImmune ) )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"They frostbite and our defensive candidate is frostbite immune. Adjustment: {adjustment}" );

                    if( ourCand.Ability == AbilityID.IceBody )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"Our defensive candidate's ability is also ice body. Adjustment: {adjustment}" );
                    }
                }

                if( ( theyPoison || theyToxic ) && candTraits.Contains( RoleTrait.PoisonToxImmune ) )
                {
                    adjustment += 20f;
                    _ai.CurrentLog.Add( $"They poison/toxic and our defensive candidate is poison/toxic immune. Adjustment: {adjustment}" );
                }

                if( ( theyParalyze && _ai.UnitSim.CheckTypes( PokemonType.Electric, ourCand ) ) || ( twaver && _ai.UnitSim.CheckTypes( PokemonType.Ground, ourCand ) ) )
                {
                    adjustment += 25f;
                    _ai.CurrentLog.Add( $"They paralyze and our defensive candidate is paralyze immune. Adjustment: {adjustment}" );
                }

                if( theySleep && candTraits.Contains( RoleTrait.SleepImmune ) )
                {
                    adjustment += 30f;
                    _ai.CurrentLog.Add( $"They sleep and our defensive candidate is sleep immune. Adjustment: {adjustment}" );
                }

                if( powderer && candTraits.Contains( RoleTrait.PowderImmune ) )
                {
                    adjustment += 20f;
                    _ai.CurrentLog.Add( $"Their status move has the powder flag and our defensive candidate is powder immune. Adjustment: {adjustment}" );
                }

                if( theyEncore )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"If they encore, switching will cause the move to fail. Adjustment: {adjustment}" );
                }

                if( theyDisable )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"If they disable, switching will cause the move to fail. Adjustment: {adjustment}" );
                }

                //--Severe Status Weakness Checks
                if( theyBurn && weAreBurnWeak )
                {
                    adjustment -= 25f;
                    _ai.CurrentLog.Add( $"They burn and our defensive candidate is burn weak. Adjustment: {adjustment}" );

                    if( ourCandIsOffensive )
                    {
                        adjustment -= 20f;
                        _ai.CurrentLog.Add( $"Our defensive candidate is also offensive, making this catastrophic. Adjustment: {adjustment}" );
                    }
                }

                if( theyFrost && weAreFrostWeak )
                {
                    adjustment -= 25f;
                    _ai.CurrentLog.Add( $"They frostbite and our defensive candidate is frostbite weak. Adjustment: {adjustment}" );

                    if( ourCandIsOffensive )
                    {
                        adjustment -= 20f;
                        _ai.CurrentLog.Add( $"Our defensive candidate is also offensive, making this catastrophic. Adjustment: {adjustment}" );
                    }
                }

                if( theyPoison && candPassiveRecovers )
                {
                    adjustment -= 20f;
                    _ai.CurrentLog.Add( $"Our defensive candidate has passive recovery, which gets negated by being poisoned. Adjustment: {adjustment}" );
                }

                if( theyToxic && weAreToxicWeak )
                {
                    adjustment -= 20f;
                    _ai.CurrentLog.Add( $"They toxic and our defensive candidate is toxic weak. Adjustment: {adjustment}" );

                    if( ourCandIsDefensive )
                    {
                        adjustment -= 20f;
                        _ai.CurrentLog.Add( $"Our defensive candidate is also defensive. Adjustment: {adjustment}" );

                        if( candRP.Biases.Contains( RoleBias.PassivePressure ) )
                        {
                            adjustment -= 20f;
                            _ai.CurrentLog.Add( $"And it relies on passive pressure, which toxic directly checks and destroys. Adjustment: {adjustment}" );
                        }
                    }
                }

                if( ( theyParalyze && weAreParalysisWeak ) || ( theyParalyze && powderer && weAreParalysisWeak && !candTraits.Contains( RoleTrait.PowderImmune ) ) )
                {
                    adjustment -= 20f;
                    _ai.CurrentLog.Add( $"They paralyze and our defensive candidate is paralyze weak. Adjustment: {adjustment}" );

                    if( ourCandIsOffensive )
                    {
                        adjustment -= 15f;
                        _ai.CurrentLog.Add( $"Our defensive candidate is also offensive, paralysis removes a turn of action + near guarantees we will move second for the rest of the match, limiting our offensive options. Adjustment: {adjustment}" );
                    }
                }
                
                if( ( theySleep && !candTraits.Contains( RoleTrait.SleepImmune ) ) || ( theySleep && powderer && !candTraits.Contains( RoleTrait.PowderImmune ) ) )
                {
                    adjustment -= 30f;
                    _ai.CurrentLog.Add( $"They sleep and our defensive candidate is sleep weak. Adjustment: {adjustment}" );
                }
            }

            if( theyTaunt && weAreTauntWeak )
            {
                adjustment -= 20f;
                _ai.CurrentLog.Add( $"They taunt and our defensive candidate is taunt weak. Adjustment: {adjustment}" );

                if( weHaveSetup )
                {
                    adjustment -= 20f;
                    _ai.CurrentLog.Add( $"Our defensive candidate also has setup, switching into a taunt immediately removes one of our strategic options. Adjustment: {adjustment}" );
                }

                if( ourCandIsUtility )
                {
                    adjustment -= 20f;
                    _ai.CurrentLog.Add( $"Our defensive candidate is a utility-class pokemon, getting taunted likely shuts it down entirely. Adjustment: {adjustment}" );
                }
            }

            if( theyHealBlock && weAreHealBlockWeak )
            {
                adjustment -= 30f;
                _ai.CurrentLog.Add( $"Our defensive candidate relies on healing and they are trying to heal block. Adjustment: {adjustment}" );
            }
        }

        if( subType == OffensiveStatusType.EntryHazard )
        {
            _ai.CurrentLog.Add( $"They are looking to set an entry hazard." );

            bool rocks = offStatusMoveEffects.CourtCondition == CourtConditionID.StealthRock;
            bool spikes = offStatusMoveEffects.CourtCondition == CourtConditionID.Spikes;
            bool toxicSpikes = offStatusMoveEffects.CourtCondition == CourtConditionID.ToxicSpikes;
            bool web = offStatusMoveEffects.CourtCondition == CourtConditionID.StickyWeb;
            bool seeds = offStatusMoveEffects.CourtCondition == CourtConditionID.LeechSeed;

            var winconRP = _ai.GetPokemonAs_Adapter( _ai.Blackboard.GamePlan.OurPrimaryWinCon ).RoleProfile;

            PokemonType one = _ai.Blackboard.GamePlan.OurPrimaryWinCon.PokeSO.Type1;
            PokemonType two = _ai.Blackboard.GamePlan.OurPrimaryWinCon.PokeSO.Type2;
            float winconRocksMod = TypeChart.GetTotalEffectiveness( PokemonType.Rock, one, two );

            if( candTraits.Contains( RoleTrait.HazardRemover ) )
            {
                adjustment += 30;
                _ai.CurrentLog.Add( $"Our defensive candidate has hazard removal. Adjustment: {adjustment}" );

                if( rocks && winconRocksMod > 1f )
                {
                    adjustment += 20f;
                    _ai.CurrentLog.Add( $"They're attempting to set rocks. Removing them will protect our wincon. Adjustment: {adjustment}" );
                }

                if( web && winconRP.Traits.Contains( RoleTrait.ParalysisWeak ) )
                {
                    adjustment += 20f;
                    _ai.CurrentLog.Add( $"They're attempting to set stick web. Removing it will protect our wincon. Adjustment: {adjustment}" );
                }
            }

            if( _ai.CurrentUnitAdapter.Ability == AbilityID.MagicBounce || _ai.CurrentUnitAdapter.Ability == AbilityID.MagicGuard )
            {
                adjustment -= 70f;
                _ai.CurrentLog.Add( $"Our current unit has magic bounce or magic guard, they cannot use hazards if we stay in. Adjustment: {adjustment}" );
            }

            if( ourCand.Ability  == AbilityID.MagicBounce || ourCand.Ability == AbilityID.MagicGuard )
            {
                adjustment += 30f;
                _ai.CurrentLog.Add( $"Our defensive candidate has magic bounce or magic guard, they cannot use hazards if we switch. Adjustment: {adjustment}" );
            }
        }

        if( subType == OffensiveStatusType.Phaze )
        {
            adjustment -= 30f;
            _ai.CurrentLog.Add( $"They're looking to phaze. Switching just gives them a free turn to do so. Adjustment: {adjustment}" );
        }

        if( subType == OffensiveStatusType.StatDebuff )
        {
            adjustment -= 15f;

            var delta = _ai.UnitSim.BuildStatStageDelta( theirMove );

            if( ourCandIsDefensive )
            {
                if( candRP.Biases.Contains( RoleBias.PhysicallyBulky ) )
                {
                    if( delta.Defense < -1 )
                    {
                        adjustment -= 30f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces defense by more than 1 stage, and our defensive candidate is Physically Bulky. Adjustment: {adjustment}" );
                    }
                    else if( delta.Defense < 0 )
                    {
                        adjustment -= 15f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces defense by 1 stage, and our defensive candidate is Physically Bulky. Adjustment: {adjustment}" );
                    }
                }

                if( candRP.Biases.Contains( RoleBias.SpeciallyBulky ) )
                {
                    if( delta.SpDefense < -1 )
                    {
                        adjustment -= 30f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces special defense by more than 1 stage, and our defensive candidate is Specially Bulky. Adjustment: {adjustment}" );
                    }
                    else if( delta.SpDefense < 0 )
                    {
                        adjustment -= 15f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces special defense by 1 stage, and our defensive candidate is Specially Bulky. Adjustment: {adjustment}" );
                    }
                }

                if( delta.Defense < 0 || delta.SpDefense < 0 )
                {
                    adjustment -= 25f;
                    _ai.CurrentLog.Add( $"Our defensive-class defensive switch candidate having either of its defensive stats lowered is not good in general regardless of alignment. Adjustment: {adjustment}" );
                }
            }

            if( ourCandIsOffensive )
            {
                if( candRP.Biases.Contains( RoleBias.Physical ) )
                {
                    if( delta.Attack < -1 )
                    {
                        adjustment -= 25f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces attack by more than 1 stage, and our defensive candidate is Physically aligned offensively. Adjustment: {adjustment}" );
                    }
                    else if( delta.Attack < 0 )
                    {
                        adjustment -= 15f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces attack by 1 stage, and our defensive candidate is Physically aligned offensively. Adjustment: {adjustment}" );
                    }

                    if( delta.SpAttack < -1 )
                    {
                        adjustment += 25f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces special attack by more than 1 stage, and our defensive candidate is Specially aligned offensively. Adjustment: {adjustment}" );
                    }
                    else if( delta.SpAttack < 0 )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces special attack by 1 stage, and our defensive candidate is Specially aligned offensively. Adjustment: {adjustment}" );
                    }
                }

                if( candRP.Biases.Contains( RoleBias.Special ) )
                {
                    if( delta.SpAttack < -1 )
                    {
                        adjustment -= 25f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces special attack by more than 1 stage, and our defensive candidate is Specially aligned offensively. Adjustment: {adjustment}" );
                    }
                    else if( delta.SpAttack < 0 )
                    {
                        adjustment -= 15f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces special attack by 1 stage, and our defensive candidate is Specially aligned offensively. Adjustment: {adjustment}" );
                    }

                    if( delta.Attack < -1 )
                    {
                        adjustment += 25f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces attack by more than 1 stage, and our defensive candidate is Physically aligned offensively. Adjustment: {adjustment}" );
                    }
                    else if( delta.Attack < 0 )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces attack by 1 stage, and our defensive candidate is Physically aligned offensively. Adjustment: {adjustment}" );
                    }
                }
            }

            if( delta.Speed < -1 )
            {
                adjustment -= 25f;
                 _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces speed by more than 1 stage. Losing speed in general is not good. Adjustment: {adjustment}" );
            }
            else if( delta.Speed < 0 )
            {
                adjustment -= 15f;
                _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces speed by 1 stage. Losing speed in general is not good. Adjustment: {adjustment}" );
            }
        }

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float DefensiveSwitchVS_SupportiveStatusIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Defensive Switch vs Supportive Status Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        adjustment -= 25f;

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float DefensiveSwitchVS_ProtectIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Defensive Switch vs Protect Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        adjustment += 25f;

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================
//===================================================================================[OFFENSIVE SWITCH VS THREAT INTENT]============================================================================================
//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================

    public int OffensiveSwitchVS_ThreatIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        float score = 0f;
        int final = 0;

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"================================================" );
        _ai.CurrentLog.Add( $"=====[Offensive Switch Threat Intent Check]=====" );
        _ai.CurrentLog.Add( $"================================================" );
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"Evaluating our Offensive Switch line against their {tir.PrimaryIntent.IntentType} (Confidence: {tir.Confidence}, Evidence: {tir.PrimaryIntent.Evidence})" );

        score += tir.PrimaryIntent.IntentType switch
        {
            IntentType.Attack =>            OffensiveSwitchVS_AttackIntent( tempo, pack, context, switchCandidate, intentTOP, tir ),
            IntentType.DefensiveSwitch =>   OffensiveSwitchVS_DefensiveSwitchIntent( tempo, pack, context, switchCandidate, intentTOP, tir ),
            IntentType.OffensiveSwitch =>   OffensiveSwitchVS_OffensiveSwitchIntent( tempo, pack, context, switchCandidate, intentTOP, tir ),
            IntentType.Setup =>             OffensiveSwitchVS_SetupIntent( tempo, pack, context, switchCandidate, intentTOP, tir ),
            IntentType.OffensiveStatus =>   OffensiveSwitchVS_OffensiveStatusIntent( tempo, pack, context, switchCandidate, intentTOP, tir ),
            IntentType.SupportiveStatus =>  OffensiveSwitchVS_SupportiveStatusIntent( tempo, pack, context, switchCandidate, intentTOP, tir ),
            IntentType.Protect =>           OffensiveSwitchVS_ProtectIntent( tempo, pack, context, switchCandidate, intentTOP, tir ),
            _ => 0f,
        };

        final = Mathf.RoundToInt( score );
        _ai.CurrentLog.Add( $"Final Score: {final}" );
        _ai.CurrentLog.Add( $"" );

        return Mathf.RoundToInt( score );
    }

    private float OffensiveSwitchVS_AttackIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Offensive Switch vs Attack Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var theirMTR = (MoveThreatResult)tir.PrimaryIntent.IntentResult;
        var theirMove = theirMTR.Move;
        var theirRP = tir.Threat.RoleProfile;
        var theirBiases = theirRP.Biases;
        var theirTraits = theirRP.Traits;

        bool theyAreOffensive = theirRP.PrimaryRole == RoleClass.BulkyAttacker || theirRP.PrimaryRole == RoleClass.RevengeKiller || theirRP.PrimaryRole == RoleClass.SetupSweeper ||
            theirRP.PrimaryRole == RoleClass.Sweeper || theirRP.PrimaryRole == RoleClass.TrickRoomAbuser || theirRP.PrimaryRole == RoleClass.WallBreaker;

        bool theyArePhysical = theirBiases.Contains( RoleBias.Physical );
        bool theyAreSpecial = theirBiases.Contains( RoleBias.Special );

        var ourCand = _ai.GetPokemonAs_Adapter( switchCandidate.Pokemon );
        var candRP = ourCand.RoleProfile;
        var candTraits = candRP.Traits;

        bool knockOff = theirMove.MoveSO.Name == "Knock Off";

        if( knockOff )
        {
            adjustment -= 10f;
            _ai.CurrentLog.Add( $"Switching into an incoming knock off is bad lol. Adjustment: {adjustment}" );

            if( candRP.PrimaryRole == RoleClass.Wall && candTraits.Contains( RoleTrait.RecoveryItem ) )
            {
                adjustment -= 20f;
                _ai.CurrentLog.Add( $"And we lose a critical item to our role. Adjustment: {adjustment}" );
            }

            bool offensiveItem = ourCand.Item == BattleItemEffectID.ChoiceBand || ourCand.Item == BattleItemEffectID.ChoiceSpecs || ourCand.Item == BattleItemEffectID.LifeOrb; //--make item class a role trait.
            if( offensiveItem )
            {
                adjustment -= 20f;
                _ai.CurrentLog.Add( $"And we lose a critical item to our role. Adjustment: {adjustment}" );
            }
        }

        if( theyArePhysical && candTraits.Contains( RoleTrait.IntimidateSupport ) )
        {
            adjustment += 15f;
            _ai.CurrentLog.Add( $"They are physically oriented and our candidate has Intimidate. Adjustment: {adjustment}" );

            if( theyAreOffensive )
            {
                adjustment += 10f;
                _ai.CurrentLog.Add( $"And they are offensive, crippling their role as well. Adjustment: {adjustment}" );
            }
        }

        if( theyAreSpecial && candTraits.Contains( RoleTrait.DemoralizeSupport ) )
        {
            adjustment += 15f;
            _ai.CurrentLog.Add( $"They are specially oriented and our candidate has Demoralize. Adjustment: {adjustment}" );

            if( theyAreOffensive )
            {
                adjustment += 10f;
                _ai.CurrentLog.Add( $"And they are offensive, crippling their role as well. Adjustment: {adjustment}" );
            }
        }

        //--Ability checks for immunities to assumed attack will go here

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float OffensiveSwitchVS_DefensiveSwitchIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Defensive Switch vs Defensive Switch Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var switchEE = _ai.Projection.EvaluateExchange( intentTOP.Attacker, intentTOP.Opponent );

        //--Next round light look ahead PTKO/Switch stuff framed in a way that isn't handled by top2 in action evaluation
        //--I will expand on the nuance here and check things like ability or role counters, both within role profile and game plan
        if( switchEE.AttackerPTKOR.PTKO > switchEE.OpponentPTKOR.PTKO )
        {
            adjustment += 15f;
        }

        if( switchEE.AttackerThreatensKO && !switchEE.OpponentThreatensKO )
        {
            adjustment += 15;
        }

        if( switchEE.AttackerThreatensKO && switchEE.AttackerMovesFirst )
        {
            adjustment += 15;
        }

        if( switchEE.OpponentThreatensKO && switchEE.OpponentMovesFirst )
        {
            adjustment -= 30f;
        }

        if( switchEE.OpponentSwitches && !switchEE.AttackerSwitches )
        {
            adjustment += 15f;
        }
        else if( switchEE.AttackerSwitches )
        {
            adjustment -= 30f;
        }

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float OffensiveSwitchVS_OffensiveSwitchIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our offensive Switch vs Offensive Switch Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var cand = _ai.GetPokemonAs_Adapter( switchCandidate.Pokemon );
        var candRP = cand.RoleProfile;

        //--We use the opponent from intent top because this should include the results of our opponent setting up, properly evaluating an attack exchange next turn.
        var switchEE = _ai.Projection.EvaluateExchange( cand, intentTOP.Opponent );

        if( switchEE.OpponentThreatensKO && switchEE.OpponentMovesFirst )
        {
            adjustment -= 30f;
            _ai.CurrentLog.Add( $"Their offensive candidate threatens a KO on our offensive candidate, and they move first. Adjustment: {adjustment}" );
        }

        if( switchEE.AttackerThreatensKO && switchEE.AttackerMovesFirst )
        {
            adjustment += 25f;
            _ai.CurrentLog.Add( $"Our offensive candidate threatens a KO on their offensive candidate, and we move first. Adjustment: {adjustment}" );
        }

        if( switchEE.OpponentSwitches && !switchEE.AttackerSwitches )
        {
            adjustment += 20;
            _ai.CurrentLog.Add( $"We might force them to switch. Adjustment: {adjustment}" );
        }
        else if( switchEE.AttackerSwitches )
        {
            adjustment -= 35;
            _ai.CurrentLog.Add( $"They might force us to switch Adjustment: {adjustment}" );
        }

        if( switchEE.OpponentPTKOR.PTKO <= PotentialToKO.Safe )
        {
            adjustment += 15f;
            _ai.CurrentLog.Add( $"Our offensive candidate walls their offensive candidate very well. Adjustment: {adjustment}" );

            if( switchEE.AttackerMovesFirst )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"And our candidate moves first. Adjustment: {adjustment}" );
            }
        }
        else if( switchEE.OpponentPTKOR.PTKO <= PotentialToKO.Risky )
        {
            adjustment += 10f;
            _ai.CurrentLog.Add( $"Our offensive candidate walls their offensive candidate decently. Adjustment: {adjustment}" );

            if( switchEE.AttackerMovesFirst )
            {
                adjustment += 10f;
                _ai.CurrentLog.Add( $"And our candidate moves first. Adjustment: {adjustment}" );
            }
        }

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float OffensiveSwitchVS_SetupIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our offensive Switch vs Setup Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var cand = _ai.GetPokemonAs_Adapter( switchCandidate.Pokemon );
        var candRP = cand.RoleProfile;

        //--We use the opponent from intent top because this should include the results of our opponent setting up, properly evaluating an attack exchange next turn.
        var switchEE = _ai.Projection.EvaluateExchange( cand, intentTOP.Opponent );

        bool weAreOffensive = candRP.PrimaryRole == RoleClass.BulkyAttacker || candRP.PrimaryRole == RoleClass.RevengeKiller || candRP.PrimaryRole == RoleClass.SetupSweeper ||
            candRP.PrimaryRole == RoleClass.Sweeper || candRP.PrimaryRole == RoleClass.TrickRoomAbuser || candRP.PrimaryRole == RoleClass.WallBreaker;

        var theirSetup = (SetupThreatResult)tir.PrimaryIntent.IntentResult;
        var delta = theirSetup.StageDelta;

        bool theyGainedSpeed = delta.Speed > 0 && tir.Threat.Speed < intentTOP.Opponent.Speed;

        if( switchEE.OpponentThreatensKO && switchEE.OpponentMovesFirst )
        {
            adjustment -= 30f;
            _ai.CurrentLog.Add( $"They threaten a KO on our offensive candidate and move first after setting up. Adjustment: {adjustment}" );
        }

        if( switchEE.AttackerThreatensKO && switchEE.AttackerMovesFirst )
        {
            adjustment += 50f;
            _ai.CurrentLog.Add( $"Our candidate threatens a KO on them after they set up and we move first. Adjustment: {adjustment}" );

            if( weAreOffensive )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"Our candidate is also offensively oriented. Adjustment: {adjustment}" );
            }
        }

        if( theirSetup.AfterPTKOR.PTKO >= PotentialToKO.Dangerous && !switchEE.OpponentThreatensKO )
        {
            adjustment += 25f;
            _ai.CurrentLog.Add( $"They threaten a KO on our current mon after setting up, but they do not threaten a KO on our offensive candidate. Adjustment: {adjustment}" );

            if( weAreOffensive )
            {
                adjustment += 20f;
                _ai.CurrentLog.Add( $"Our offensive candidate is also offensively oriented. Adjustment: {adjustment}" );
            }
        }

        if( switchEE.OpponentSwitches && !switchEE.AttackerSwitches )
        {
            adjustment += 25;
            _ai.CurrentLog.Add( $"Our candidate may force them to switch next turn, invalidating their setup. Adjustment: {adjustment}" );

            if( weAreOffensive )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"Our offensive candidate is also offensively oriented. Adjustment: {adjustment}" );
            }
        }
        else if( switchEE.AttackerSwitches )
        {
            adjustment -= 35;
            _ai.CurrentLog.Add( $"They might force our offensive candidate to switch next turn. Adjustment: {adjustment}" );
        }

        if( candRP.Traits.Contains( RoleTrait.Phazes ) )
        {
            adjustment += 10f;
            _ai.CurrentLog.Add( $"Our offensive candidate has a phazing move. Adjustment: {adjustment}" );

            if( switchEE.OpponentPTKOR.PTKO <= PotentialToKO.Risky )
            {
                adjustment += 5f;
                _ai.CurrentLog.Add( $"We are also likely to survive the round and pull it off. Adjustment: {adjustment}" );
            }

            if( switchEE.AttackerMovesFirst )
            {
                adjustment += 25f;
                _ai.CurrentLog.Add( $"We're also faster and will get to use it before they can attack next turn. Adjustment: {adjustment}" );
            }
        }

        if( candRP.PrimaryRole == RoleClass.RevengeKiller || candRP.PrimaryRole == RoleClass.Sweeper || ( candRP.PrimaryRole == RoleClass.WallBreaker && intentTOP.Opponent.RoleProfile.PrimaryRole == RoleClass.Wall ) )
        {
            adjustment += 15f;
            _ai.CurrentLog.Add( $"Our candidate is a a tempo-based offensive class, or a wall breaker coming in against an incoming wall read. Adjustment: {adjustment}" );

            if( theirSetup.AfterPTKOR.PTKO < switchEE.OpponentPTKOR.PTKO )
            {
                adjustment += 20f;
                _ai.CurrentLog.Add( $"Our offensive candidate lowers their after-setup PTKO. Adjustment: {adjustment}" );
            }

            if( candRP.Biases.Contains( RoleBias.Physical ) )
            {
                if( delta.SpDefense > 1 )
                {
                    adjustment += 20f;
                    _ai.CurrentLog.Add( $"Our offensive candidate is physically offensive, and they are boosting their special defense by more than 1 stage, we are a good counter. Adjustment: {adjustment}" );
                }
                else if( delta.SpDefense > 0 )
                {
                    adjustment += 10f;
                    _ai.CurrentLog.Add( $"Our offensive candidate is physically offensive, and they are boosting their special defense by 1 stage, we are a good counter. Adjustment: {adjustment}" );
                }
            }

            if( candRP.Biases.Contains( RoleBias.Special ) )
            {
                if( delta.Defense > 1 )
                {
                    adjustment += 20f;
                    _ai.CurrentLog.Add( $"Our offensive candidate is specially offensive, and they are boosting their defense by more than 1 stage, we are a good counter. Adjustment: {adjustment}" );
                }
                else if( delta.Defense > 0 )
                {
                    adjustment += 10f;
                    _ai.CurrentLog.Add( $"Our offensive candidate is specially offensive, and they are boosting their defense by 1 stage, we are a good counter. Adjustment: {adjustment}" );
                }
            }
        }

        if( theyGainedSpeed && candRP.Traits.Contains( RoleTrait.ParalysisPressure ) )
        {
            adjustment += 15f;
            _ai.CurrentLog.Add( $"Our offensive candidate has paralysis pressure and their pokemon gains significant speed from setting up. Adjustment: {adjustment}" );
        }

        if( switchEE.AttackerMovesFirst || switchEE.OpponentPTKOR.PTKO <= PotentialToKO.Risky )
        {
            adjustment += 15f;
            _ai.CurrentLog.Add( $"Our offensive candidate either moves first or has a risky or safer PTKO next turn. Adjustment: {adjustment}" );
        }

        if( candRP.Traits.Contains( RoleTrait.Taunt ) || candRP.Traits.Contains( RoleTrait.Encore ) )
        {
            adjustment += 15f;
            _ai.CurrentLog.Add( $"Our offensive candidate has taunt or encore pressure, which could prevent further setup or lock them down. Adjustment: {adjustment}" );
        }

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float OffensiveSwitchVS_OffensiveStatusIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Offensive Switch vs Offensive Status Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var theirOffStatus = (StatusThreatResult)tir.PrimaryIntent.IntentResult;
        var them = tir.Threat;
        var theirMove = theirOffStatus.Move;
        var subType = theirOffStatus.OffensiveStatusType;
        var offStatusMoveEffects = theirMove.MoveSO.MoveEffects;
        var theirRP = them.RoleProfile;

        var ourCand = _ai.GetPokemonAs_Adapter( switchCandidate.Pokemon );
        var candRP = ourCand.RoleProfile;
        var candTraits = candRP.Traits;

        bool ourCandIsOffensive = candRP.PrimaryRole == RoleClass.BulkyAttacker || candRP.PrimaryRole == RoleClass.RevengeKiller || candRP.PrimaryRole == RoleClass.SetupSweeper ||
            candRP.PrimaryRole == RoleClass.Sweeper || candRP.PrimaryRole == RoleClass.TrickRoomAbuser || candRP.PrimaryRole == RoleClass.WallBreaker;

        bool ourCandIsDefensive = candRP.PrimaryRole == RoleClass.Wall || candRP.PrimaryRole == RoleClass.DefensiveSetup && candRP.SecondaryRoles.Contains( RoleClass.Wall );
        bool ourCandIsUtility = !ourCandIsOffensive && !ourCandIsDefensive;

        if( subType == OffensiveStatusType.StatusEffect|| subType == OffensiveStatusType.Disruption )
        {
            _ai.CurrentLog.Add( $"They are looking to use a move that causes a status effect." );

            bool theyBurn = offStatusMoveEffects.SevereStatus == SevereConditionID.BRN;
            bool theyFrost = offStatusMoveEffects.SevereStatus == SevereConditionID.FBT;
            bool theyPoison = offStatusMoveEffects.SevereStatus == SevereConditionID.PSN;
            bool theyToxic = offStatusMoveEffects.SevereStatus == SevereConditionID.TOX;
            bool theyParalyze = offStatusMoveEffects.SevereStatus == SevereConditionID.PAR;
            bool theySleep = offStatusMoveEffects.SevereStatus == SevereConditionID.SLP;
            bool theySevere = offStatusMoveEffects.SevereStatus != SevereConditionID.None;

            bool prankster = them.Ability == AbilityID.Prankster;
            bool powderer = _ai.UnitSim.CheckHasMove( them, "Sleep Powder" ) || _ai.UnitSim.CheckHasMove( them, "Spore" ) || _ai.UnitSim.CheckHasMove( them, "Poison Powder" ) || _ai.UnitSim.CheckHasMove( them, "Stun Spore" );
            
            bool twaver = _ai.UnitSim.CheckHasMove( them, "Thunder Wave" );

            bool theyTaunt = offStatusMoveEffects.VolatileStatus == VolatileConditionID.Taunt;
            bool theyEncore = offStatusMoveEffects.VolatileStatus == VolatileConditionID.Encore;
            bool theyHealBlock = offStatusMoveEffects.VolatileStatus == VolatileConditionID.HealBlocked;
            bool theyDisable = offStatusMoveEffects.VolatileStatus == VolatileConditionID.Disabled;

            bool weAreBurnWeak = candTraits.Contains( RoleTrait.BurnWeak );
            bool weAreFrostWeak = candTraits.Contains( RoleTrait.FrostWeak );
            bool weAreToxicWeak = candTraits.Contains( RoleTrait.ToxicWeak );
            bool weAreParalysisWeak = candTraits.Contains( RoleTrait.ParalysisWeak  );

            bool candPassiveRecovers = candTraits.Contains( RoleTrait.RecoveryItem ) || candTraits.Contains( RoleTrait.RecoveryAbility );
            bool weHaveSetup = candRP.Traits.Contains( RoleTrait.PhysicallyOffensiveSetup ) || candRP.Traits.Contains( RoleTrait.SpeciallyOffensiveSetup ) || candRP.Traits.Contains( RoleTrait.PhysicallyDefensiveSetup ) || candRP.Traits.Contains( RoleTrait.SpeciallyDefensiveSetup );

            bool weAreTauntWeak = candTraits.Contains( RoleTrait.TauntWeak );
            bool weAreEncoreWeak = candTraits.Contains( RoleTrait.EncoreWeak );
            bool weAreHealBlockWeak = candTraits.Contains( RoleTrait.RecoveryMove );
            bool weAreDisableWeak = ourCand.VolatileStatuses.Contains( VolatileConditionID.ChoiceLocked ) || candRP.Signals.PhysicalAttackCount < 2 || candRP.Signals.SpecialAttackCount < 2;

            //--Prankster Immunity
            if( prankster && _ai.UnitSim.CheckTypes( PokemonType.Dark, ourCand ) )
            {
                adjustment += 10f;
                _ai.CurrentLog.Add( $"Their status move is going to activate their Prankster ability! Our Offensive candidate is a dark type, which will prevent the move from going off! Adjustment: {adjustment}" );

                if( theirRP.PrimaryRole == RoleClass.UtilitySupport || theirRP.PrimaryRole == RoleClass.Disrupter )
                {
                    adjustment += 10f;
                    _ai.CurrentLog.Add( $"They are a utility/disruption class mon, shutting down prankster likely locks down their set. Adjustment: {adjustment}" );
                }
            }

            if( theySevere && ourCand.SevereStatus != SevereConditionID.None )
            {
                adjustment += 15f;
                _ai.CurrentLog.Add( $"Our Offensive candidate is already effected by a severe status, which will nullify their attempt to place a severe status on us. Adjustment: {adjustment}" );
            }
            else if( theySevere )
            {
                //--Severe Status immunity/absorption checks
                if( theyBurn && candTraits.Contains( RoleTrait.BurnImmune ) )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"They burn and our Offensive candidate is burn immune. Adjustment: {adjustment}" );

                    if( ourCand.Ability == AbilityID.FlashFire )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"Our Offensive candidate's ability is also flash fire. Adjustment: {adjustment}" );
                    }
                }

                if( theyFrost && candTraits.Contains( RoleTrait.FrostImmune ) )
                {
                    adjustment += 10f;
                    _ai.CurrentLog.Add( $"They frostbite and our Offensive candidate is frostbite immune. Adjustment: {adjustment}" );

                    if( ourCand.Ability == AbilityID.IceBody )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"Our Offensive candidate's ability is also ice body. Adjustment: {adjustment}" );
                    }
                }

                if( ( theyPoison || theyToxic ) && candTraits.Contains( RoleTrait.PoisonToxImmune ) )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"They poison/toxic and our Offensive candidate is poison/toxic immune. Adjustment: {adjustment}" );
                }

                if( ( theyParalyze && _ai.UnitSim.CheckTypes( PokemonType.Electric, ourCand ) ) || ( twaver && _ai.UnitSim.CheckTypes( PokemonType.Ground, ourCand ) ) )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"They paralyze and our Offensive candidate is paralyze immune. Adjustment: {adjustment}" );
                }

                if( theySleep && candTraits.Contains( RoleTrait.SleepImmune ) )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"They sleep and our Offensive candidate is sleep immune. Adjustment: {adjustment}" );
                }

                if( powderer && candTraits.Contains( RoleTrait.PowderImmune ) )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"Their status move has the powder flag and our Offensive candidate is powder immune. Adjustment: {adjustment}" );
                }

                if( theyEncore )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"If they encore, switching will cause the move to fail. Adjustment: {adjustment}" );
                }

                if( theyDisable )
                {
                    adjustment += 15f;
                    _ai.CurrentLog.Add( $"If they disable, switching will cause the move to fail. Adjustment: {adjustment}" );
                }

                //--Severe Status Weakness Checks
                if( theyBurn && weAreBurnWeak )
                {
                    adjustment -= 15f;
                    _ai.CurrentLog.Add( $"They burn and our Offensive candidate is burn weak. Adjustment: {adjustment}" );

                    if( ourCandIsOffensive )
                    {
                        adjustment -= 25f;
                        _ai.CurrentLog.Add( $"Our Offensive candidate is also offensive, making this catastrophic. Adjustment: {adjustment}" );
                    }
                }

                if( theyFrost && weAreFrostWeak )
                {
                    adjustment -= 15f;
                    _ai.CurrentLog.Add( $"They frostbite and our Offensive candidate is frostbite weak. Adjustment: {adjustment}" );

                    if( ourCandIsOffensive )
                    {
                        adjustment -= 25f;
                        _ai.CurrentLog.Add( $"Our Offensive candidate is also offensive, making this catastrophic. Adjustment: {adjustment}" );
                    }
                }

                if( theyPoison && candPassiveRecovers )
                {
                    adjustment -= 25f;
                    _ai.CurrentLog.Add( $"Our Offensive candidate has passive recovery, which gets negated by being poisoned. Adjustment: {adjustment}" );
                }

                if( theyToxic && weAreToxicWeak )
                {
                    adjustment -= 15f;
                    _ai.CurrentLog.Add( $"They toxic and our Offensive candidate is toxic weak. Adjustment: {adjustment}" );

                    if( ourCandIsDefensive )
                    {
                        adjustment -= 15f;
                        _ai.CurrentLog.Add( $"Our Offensive candidate is also Offensive. Adjustment: {adjustment}" );

                        if( candRP.Biases.Contains( RoleBias.PassivePressure ) )
                        {
                            adjustment -= 15f;
                            _ai.CurrentLog.Add( $"And it relies on passive pressure, which toxic directly checks and destroys. Adjustment: {adjustment}" );
                        }
                    }
                }

                if( ( theyParalyze && weAreParalysisWeak ) || ( theyParalyze && powderer && weAreParalysisWeak && !candTraits.Contains( RoleTrait.PowderImmune ) ) )
                {
                    adjustment -= 15f;
                    _ai.CurrentLog.Add( $"They paralyze and our Offensive candidate is paralyze weak. Adjustment: {adjustment}" );

                    if( ourCandIsOffensive )
                    {
                        adjustment -= 15f;
                        _ai.CurrentLog.Add( $"Our Offensive candidate is also offensive, paralysis removes a turn of action + near guarantees we will move second for the rest of the match, limiting our offensive options. Adjustment: {adjustment}" );
                    }
                }
                
                if( ( theySleep && !candTraits.Contains( RoleTrait.SleepImmune ) ) || ( theySleep && powderer && !candTraits.Contains( RoleTrait.PowderImmune ) ) )
                {
                    adjustment -= 15f;
                    _ai.CurrentLog.Add( $"They sleep and our Offensive candidate is sleep weak. Adjustment: {adjustment}" );
                }
            }

            if( theyTaunt && weAreTauntWeak )
            {
                adjustment -= 15f;
                _ai.CurrentLog.Add( $"They taunt and our Offensive candidate is taunt weak. Adjustment: {adjustment}" );

                if( weHaveSetup )
                {
                    adjustment -= 15f;
                    _ai.CurrentLog.Add( $"Our Offensive candidate also has setup, switching into a taunt immediately removes one of our strategic options. Adjustment: {adjustment}" );
                }

                if( ourCandIsUtility )
                {
                    adjustment -= 15f;
                    _ai.CurrentLog.Add( $"Our Offensive candidate is a utility-class pokemon, getting taunted likely shuts it down entirely. Adjustment: {adjustment}" );
                }
            }

            if( theyHealBlock && weAreHealBlockWeak )
            {
                adjustment -= 15f;
                _ai.CurrentLog.Add( $"Our Offensive candidate relies on healing and they are trying to heal block. Adjustment: {adjustment}" );
            }
        }

        if( subType == OffensiveStatusType.EntryHazard )
        {
            _ai.CurrentLog.Add( $"They are looking to set an entry hazard." );

            bool rocks = offStatusMoveEffects.CourtCondition == CourtConditionID.StealthRock;
            bool spikes = offStatusMoveEffects.CourtCondition == CourtConditionID.Spikes;
            bool toxicSpikes = offStatusMoveEffects.CourtCondition == CourtConditionID.ToxicSpikes;
            bool web = offStatusMoveEffects.CourtCondition == CourtConditionID.StickyWeb;
            bool seeds = offStatusMoveEffects.CourtCondition == CourtConditionID.LeechSeed;

            var winconRP = _ai.GetPokemonAs_Adapter( _ai.Blackboard.GamePlan.OurPrimaryWinCon ).RoleProfile;

            PokemonType one = _ai.Blackboard.GamePlan.OurPrimaryWinCon.PokeSO.Type1;
            PokemonType two = _ai.Blackboard.GamePlan.OurPrimaryWinCon.PokeSO.Type2;
            float winconRocksMod = TypeChart.GetTotalEffectiveness( PokemonType.Rock, one, two );

            if( candTraits.Contains( RoleTrait.HazardRemover ) )
            {
                adjustment += 15;
                _ai.CurrentLog.Add( $"Our Offensive candidate has hazard removal. Adjustment: {adjustment}" );

                if( rocks && winconRocksMod > 1f )
                {
                    adjustment += 10f;
                    _ai.CurrentLog.Add( $"They're attempting to set rocks. Removing them will protect our wincon. Adjustment: {adjustment}" );
                }

                if( web && winconRP.Traits.Contains( RoleTrait.ParalysisWeak ) )
                {
                    adjustment += 10f;
                    _ai.CurrentLog.Add( $"They're attempting to set stick web. Removing it will protect our wincon. Adjustment: {adjustment}" );
                }
            }

            if( _ai.CurrentUnitAdapter.Ability == AbilityID.MagicBounce || _ai.CurrentUnitAdapter.Ability == AbilityID.MagicGuard )
            {
                adjustment -= 70f;
                _ai.CurrentLog.Add( $"Our current unit has magic bounce or magic guard, they cannot use hazards if we stay in. Adjustment: {adjustment}" );
            }

            if( ourCand.Ability  == AbilityID.MagicBounce || ourCand.Ability == AbilityID.MagicGuard )
            {
                adjustment += 30f;
                _ai.CurrentLog.Add( $"Our offensive candidate has magic bounce or magic guard, they cannot use hazards if we switch. Adjustment: {adjustment}" );
            }
        }

        if( subType == OffensiveStatusType.Phaze )
        {
            adjustment -= 15f;
            _ai.CurrentLog.Add( $"They're looking to phaze. Switching just gives them a free turn to do so. Adjustment: {adjustment}" );
        }

        if( subType == OffensiveStatusType.StatDebuff )
        {
            adjustment -= 15f;

            var delta = _ai.UnitSim.BuildStatStageDelta( theirMove );

            if( ourCandIsDefensive )
            {
                if( candRP.Biases.Contains( RoleBias.PhysicallyBulky ) )
                {
                    if( delta.Defense < -1 )
                    {
                        adjustment -= 25f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces defense by more than 1 stage, and our Offensive candidate is Physically Bulky. Adjustment: {adjustment}" );
                    }
                    else if( delta.Defense < 0 )
                    {
                        adjustment -= 15f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces defense by 1 stage, and our Offensive candidate is Physically Bulky. Adjustment: {adjustment}" );
                    }
                }

                if( candRP.Biases.Contains( RoleBias.SpeciallyBulky ) )
                {
                    if( delta.SpDefense < -1 )
                    {
                        adjustment -= 25f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces special defense by more than 1 stage, and our Offensive candidate is Specially Bulky. Adjustment: {adjustment}" );
                    }
                    else if( delta.SpDefense < 0 )
                    {
                        adjustment -= 15f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces special defense by 1 stage, and our Offensive candidate is Specially Bulky. Adjustment: {adjustment}" );
                    }
                }

                if( delta.Defense < 0 || delta.SpDefense < 0 )
                {
                    adjustment -= 25f;
                    _ai.CurrentLog.Add( $"Our defensive-class Offensive switch candidate having either of its defensive stats lowered is not good in general regardless of alignment. Adjustment: {adjustment}" );
                }
            }

            if( ourCandIsOffensive )
            {
                if( candRP.Biases.Contains( RoleBias.Physical ) )
                {
                    if( delta.Attack < -1 )
                    {
                        adjustment -= 25f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces attack by more than 1 stage, and our Offensive candidate is Physically aligned offensively. Adjustment: {adjustment}" );
                    }
                    else if( delta.Attack < 0 )
                    {
                        adjustment -= 15f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces attack by 1 stage, and our Offensive candidate is Physically aligned offensively. Adjustment: {adjustment}" );
                    }

                    if( delta.SpAttack < -1 )
                    {
                        adjustment += 25f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces special attack by more than 1 stage, and our Offensive candidate is Specially aligned offensively. Adjustment: {adjustment}" );
                    }
                    else if( delta.SpAttack < 0 )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces special attack by 1 stage, and our Offensive candidate is Specially aligned offensively. Adjustment: {adjustment}" );
                    }
                }

                if( candRP.Biases.Contains( RoleBias.Special ) )
                {
                    if( delta.SpAttack < -1 )
                    {
                        adjustment -= 25f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces special attack by more than 1 stage, and our Offensive candidate is Specially aligned offensively. Adjustment: {adjustment}" );
                    }
                    else if( delta.SpAttack < 0 )
                    {
                        adjustment -= 15f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces special attack by 1 stage, and our Offensive candidate is Specially aligned offensively. Adjustment: {adjustment}" );
                    }

                    if( delta.Attack < -1 )
                    {
                        adjustment += 25f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces attack by more than 1 stage, and our Offensive candidate is Physically aligned offensively. Adjustment: {adjustment}" );
                    }
                    else if( delta.Attack < 0 )
                    {
                        adjustment += 15f;
                        _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces attack by 1 stage, and our Offensive candidate is Physically aligned offensively. Adjustment: {adjustment}" );
                    }
                }
            }

            if( delta.Speed < -1 )
            {
                adjustment -= 30f;
                 _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces speed by more than 1 stage. Losing speed in general is not good. Adjustment: {adjustment}" );
            }
            else if( delta.Speed < 0 )
            {
                adjustment -= 15f;
                _ai.CurrentLog.Add( $"They're looking to use a stat debuff move that reduces speed by 1 stage. Losing speed in general is not good. Adjustment: {adjustment}" );
            }
        }

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float OffensiveSwitchVS_SupportiveStatusIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Offensive Switch vs Supportive Status Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        adjustment -= 5f;

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

    private float OffensiveSwitchVS_ProtectIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Our Defensive Switch vs Protect Intent]===" );
        _ai.CurrentLog.Add( $"" );

        float score = 0f;
        float adjustment = 0f;
        float evidence = tir.PrimaryIntent.Evidence;
        float confidence = tir.Confidence;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        adjustment += 30f;

        score += ApplyIntentAdjustment( adjustment, evidence, confidence );

        return score;
    }

//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================
//===================================================================================[OFFENSIVE SWITCH VS THREAT INTENT]============================================================================================
//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================

    public int SetupVS_ThreatIntent( TempoStateResult tempo, ExchangePack pack, BoardContext context, SetupThreatResult setup, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        float score = 0f;
        int final = 0;

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"================================================" );
        _ai.CurrentLog.Add( $"=====[Offensive Switch Threat Intent Check]=====" );
        _ai.CurrentLog.Add( $"================================================" );
        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"Evaluating our Offensive Switch line against their {tir.PrimaryIntent.IntentType} (Confidence: {tir.Confidence}, Evidence: {tir.PrimaryIntent.Evidence})" );

        // score += tir.PrimaryIntent.IntentType switch
        // {
        //     IntentType.Attack =>            OffensiveSwitchVS_AttackIntent( tempo, eval, context, setup, intentTOP, tir ),
        //     IntentType.DefensiveSwitch =>   OffensiveSwitchVS_DefensiveSwitchIntent( tempo, eval, context, setup, intentTOP, tir ),
        //     IntentType.OffensiveSwitch =>   OffensiveSwitchVS_OffensiveSwitchIntent( tempo, eval, context, setup, intentTOP, tir ),
        //     IntentType.Setup =>             OffensiveSwitchVS_SetupIntent( tempo, eval, context, setup, intentTOP, tir ),
        //     IntentType.OffensiveStatus =>   OffensiveSwitchVS_OffensiveStatusIntent( tempo, eval, context, setup, intentTOP, tir ),
        //     IntentType.SupportiveStatus =>  OffensiveSwitchVS_SupportiveStatusIntent( tempo, eval, context, setup, intentTOP, tir ),
        //     IntentType.Protect =>           OffensiveSwitchVS_ProtectIntent( tempo, eval, context, setup, intentTOP, tir ),
        //     _ => 0f,
        // };

        final = Mathf.RoundToInt( score );
        _ai.CurrentLog.Add( $"Final Score: {final}" );
        _ai.CurrentLog.Add( $"" );

        return Mathf.RoundToInt( score );
    }
}
