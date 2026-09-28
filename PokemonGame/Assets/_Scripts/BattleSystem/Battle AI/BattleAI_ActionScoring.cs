using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class BattleAI_ActionScoring
{
    private readonly BattleAI _ai;
    private readonly BattleAI_Projection _proj;

    public BattleAI_ActionScoring( BattleAI ai )
    {
        _ai = ai;
        _proj = _ai.Projection;
    }

    public int ActionScore( ActionEvaluation action, TempoStateResult tempo, ExchangePack pack, BoardContext context, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        int score = 0;

        switch( action.Type )
        {
            case ActionType.Attack:
                var attack = (MoveThreatResult)action.ActionResult;
                score += AttackScore( tempo, pack, context, attack, intentTOP, tir );
            break;

            case ActionType.DefensiveSwitch:
                var defensiveSwitch = (SwitchCandidateResult)action.ActionResult;
                score += DefensiveSwitchScore( tempo, pack, context, defensiveSwitch, intentTOP, tir );
            break;

            case ActionType.OffensiveSwitch:
                var offensiveSwitch = (SwitchCandidateResult)action.ActionResult;
                score += OffensiveSwitchScore( tempo, pack, context, offensiveSwitch, intentTOP, tir );
            break;

            case ActionType.Setup:
                var setup = (SetupThreatResult)action.ActionResult;
                score += SetupScore( tempo, pack, context, setup, intentTOP, tir );
            break;

            case ActionType.OffensiveStatus:
                var offensiveStatus = (StatusThreatResult)action.ActionResult;
                score += OffensiveStatusScore( tempo, pack, context, offensiveStatus, intentTOP, tir );
            break;

            case ActionType.SupportiveStatus:
                var supportiveStatus = (StatusThreatResult)action.ActionResult;
                score += SupportiveStatusScore( tempo, pack, context, supportiveStatus, intentTOP, tir );
            break;
        }

        return score;
    }

//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================
//=======================================================================================[ATTACK SCORE]=============================================================================================================
//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================

    public int AttackScore( TempoStateResult tempo, ExchangePack pack, BoardContext context, MoveThreatResult mtr, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"===========================" );
        _ai.CurrentLog.Add( $"===[Attack Action Score]===" );
        _ai.CurrentLog.Add( $"===========================" );
        _ai.CurrentLog.Add( $"" );

        int score = 0;

        // ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        // ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        // ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        // ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        string attackerName = intentTOP.Attacker?.Name;
        string targetName = intentTOP.Opponent?.Name;

        PotentialToKO ourPTKO = intentTOP.AttackerPTKO;
        PotentialToKO theirPTKO = intentTOP.OpponentPTKO;

        int ourPTKOScore = _proj.Get_PotentialToKOScoreFromEnum( ourPTKO );
        int theirPTKOScore = _proj.Get_PotentialToKOScoreFromEnum( theirPTKO );

        string moveName = "NONE";

        if( mtr.Move != null )
            moveName = mtr.Move.MoveSO.Name;
        else
        {
            _ai.CurrentLog.Add( $"({attackerName}) Had no viable attacking move! Tanking Score!" );
            return -999;
        }

        _ai.CurrentLog.Add( $"===[Beginning Attack Scoring for {attackerName} vs {targetName}. Tempo: {tempo.TempoState}, Our ({moveName}) PTKO: {ourPTKO}, Their ({intentTOP.Opponent?.MTR?.Move?.MoveSO.Name}) PTKO: {theirPTKO}]===" );
        _ai.CurrentLog.Add( $"IntentTOP Information. Opponent mismatch occurs if we read a switch from the opponent. Attacker: {intentTOP.Attacker?.Name}, Opponent: {intentTOP.Opponent?.Name}, Threat: {tir.Threat?.Name}" );

        //--KO Class Advantage
        score += _proj.Get_OffensivePTKOScore( ourPTKOScore );
        _ai.CurrentLog.Add( $"My ({attackerName}) PTKO Score {ourPTKOScore}. Score: {score}" );

        score += theirPTKOScore;
        _ai.CurrentLog.Add( $"Their ({targetName}) PTKO Score {theirPTKOScore}. Score: {score}" );

        bool iMoveFirst = intentTOP.TurnOrderHistory[intentTOP.Attacker] < intentTOP.TurnOrderHistory[intentTOP.Opponent];
        bool iThreatenKO = ourPTKO >= PotentialToKO.Dangerous;
        bool theyThreatenKO = theirPTKO >= PotentialToKO.Dangerous;

        bool theyCantAct = !intentTOP.Opponent.CouldAct;
        bool weUsedFakeOut = mtr?.Move.MoveSO.Name == "Fake Out";

        _ai.CurrentLog.Add( $"I Threaten a KO: {iThreatenKO}." );
        _ai.CurrentLog.Add( $"They Threaten a KO: {theyThreatenKO}." );
        _ai.CurrentLog.Add( $"I am faster: {iMoveFirst}." );

        if( iThreatenKO )
        {
            if( iMoveFirst )
                score += 135; //--Commit hard
            else
                score += 75; //--Probably commit
        }

        _ai.CurrentLog.Add( $"Score: {score}." );

        if( theyCantAct )
        {
            score += 75;
            _ai.CurrentLog.Add( $"Opponent cannot act on their turn! Score: {score}" );
        }

        if( weUsedFakeOut )
        {
            score += 75;

            _ai.CurrentLog.Add( $"We used Fake Out! Score: {score}" );

            if( iThreatenKO )
            {
                score += 25;
                _ai.CurrentLog.Add( $"And it might chip them to a KO. Score: {score}" );
            }

            if( theyCantAct )
            {
                if( theirPTKO >= PotentialToKO.Dangerous )
                {
                    score += 50;
                    _ai.CurrentLog.Add( $"We prevent them from doing dangerous damage! Score: {score}" );
                }
                else if( theirPTKO >= PotentialToKO.TwoHKO )
                {
                    score += 25;
                    _ai.CurrentLog.Add( $"We prevent them from doing good damage! Score: {score}" );
                }

                if( intentTOP.Opponent?.Speed > intentTOP.Attacker?.Speed )
                {
                    score += 25;
                    _ai.CurrentLog.Add( $"They're naturally faster than us, preventing them from acting first is good. Score: {score}" );
                }
                
                if( intentTOP.OpponentAlly?.Speed > intentTOP.Attacker?.Speed )
                {
                    score += 25;
                    _ai.CurrentLog.Add( $"They're naturally faster than our ally, preventing them from acting first is good. Score: {score}" );
                }
            }
        }

        float myHPR = intentTOP.Attacker.BeginningHPR;
        if( theyThreatenKO && ( !iThreatenKO || !iMoveFirst || !theyCantAct ) )
        {
            _ai.CurrentLog.Add( $"They Threaten a KO and we probably can't prevent it." );

            int deathPenalty;

            if( myHPR > 0.6f )
                deathPenalty = 120;
            else if( myHPR > 0.3f )
                deathPenalty = 80;
            else
                deathPenalty = 40;

            score -= deathPenalty;

            _ai.CurrentLog.Add( $"Death Penalty: {deathPenalty}. Score: {score}" );
        }
        else
        {
            if( myHPR > 0.8f && iMoveFirst )
                score += 20;
        }

        if( myHPR < 0.2f )
            score -= 20;
        else if( myHPR < 0.4f )
            score -= 10;
        else if( myHPR >= 0.8f && !theyThreatenKO )
            score += 15;

        _ai.CurrentLog.Add( $"HP Ratio Check {myHPR}. Score: {score}" );

        score += _ai.Attack_TempoModifier( tempo );

        _ai.CurrentLog.Add( $"Tempo check. Score: {score}" );

        if( context.IsBehind )
            score += 20;

        _ai.CurrentLog.Add( $"Is Behind: {context.IsBehind}. Score: {score}" );

        //--Attacking Based on Switch Pressure
        if( intentTOP.OpponentSwitched )
        {
            score += 25;
            _ai.CurrentLog.Add( $"Opponent modeling thinks the opponent will switch. Score: {score}" );
        }

        if( context.IsForcedTrade )
        {
            if( iThreatenKO )
                score += 25;
            else
                score += 15;

            _ai.CurrentLog.Add( $"Forced Trade: {context.IsForcedTrade}. Score: {score}" );
        }

        //--Flat KO flag checks
        //--Guaranteed TwoHKO
        if( ourPTKO >= PotentialToKO.TwoHKO && ourPTKO <= PotentialToKO.OHKO && iMoveFirst && theirPTKO <= PotentialToKO.TwoHKO )
        {
            score += 45;
            _ai.CurrentLog.Add( $"We have a very likely guaranteed 2HKO on this opponent. Score: {score}" );
        }

        //--Likely KO
        if( ourPTKO == PotentialToKO.Dangerous && ( !theyThreatenKO || iMoveFirst ) )
            score += 40;
        else if( ourPTKO == PotentialToKO.OHKO && ( !theyThreatenKO || iMoveFirst ) )
            score += 50;

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"Final Attack Score: {score}" );

        return score;
    }

//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================
//======================================================================================[DEFENSIVE SWITCH SCORE]====================================================================================================
//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================

    public int DefensiveSwitchScore( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult scr, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"=====================================" );
        _ai.CurrentLog.Add( $"===[Defensive Switch Action Score]===" );
        _ai.CurrentLog.Add( $"=====================================" );
        _ai.CurrentLog.Add( $"" );

        // ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        // ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        // ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        // ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var returnVs_Threat = _proj.MakeUnitComparison( scr.CurrentActor, tir.Threat );

        //--Tank score if unable to switch
        if( scr.Pokemon == null || _ai.BattleSystem.BattleType == BattleType.WildBattle_1v1 || _ai.Check_IsLastPokemon( _ai.CurrentUnitAdapter.Pokemon ) )
        {
            _ai.CurrentLog.Add( $"No switch available (null, wild battle, or last pokemon). Tanking Score!" );
            return -999;
        }

        int score = 0;

        string attackerName = intentTOP.Attacker?.Name;
        string targetName = intentTOP.Opponent?.Name;

        var switchName = "no switch available!";
        if( scr.Pokemon != null )
            switchName = scr.Pokemon.NickName;

        _ai.CurrentLog.Add( $"===[Beginning Defensive Switch Scoring for {attackerName} vs {targetName}. Switch Candidate: {switchName}. Tempo: {tempo.TempoState}]===" );
        _ai.CurrentLog.Add( $"IntentTOP Information. Attacker: {intentTOP.Attacker?.Name}, Opponent: {intentTOP.Opponent?.Name}, TIR Threat: {tir.Threat?.Name}" );

        if( intentTOP.OpponentPTKO == PotentialToKO.OHKO )
        {
            _ai.CurrentLog.Add( $"Switch candidate {intentTOP.Attacker.Name}'s potential to be KO'd on switch in is OHKO in the intent simulation! Tanking Score!" );
            return -999;
        }
        // else if( str.SwitchDefensePTKO == PotentialToKO.OHKO )
        // {
        //     score -= 70;
        // }
        
        var opponentPTKO_ReturnMon = scr.OriginalPTKO;
        var opponentPTKO_ReturnMonScore = _proj.Get_PotentialToKOScoreFromEnum( scr.OriginalPTKO );

        var opponentPTKO_Switch = scr.SwitchDefensePTKO;
        var opponentPTKO_SwitchScore = _ai.Projection.Get_PotentialToKOScoreFromEnum( scr.SwitchDefensePTKO );

        _ai.CurrentLog.Add( $"{targetName}'s Current PTKO me ({attackerName}): {opponentPTKO_ReturnMon}. {targetName}'s PTKO on Switch Candidate ({switchName}): {scr.SwitchDefensePTKO}. {switchName}'s PTKO {targetName}: {scr.SwitchOffensePTKO}" );

        if( context.IsTerminal && context.IsForcedTrade && !scr.IsLegitimate )
        {
            _ai.CurrentLog.Add( $"Terminal board and no KO class improvement/Switch is illegitimate. Tanking Score!" );
            return -999;
        }

        if( !context.IsTerminal && opponentPTKO_ReturnMon >= PotentialToKO.Dangerous && opponentPTKO_Switch >= PotentialToKO.Dangerous )
            score -= 45;

        int improvement = Mathf.Clamp( opponentPTKO_SwitchScore - opponentPTKO_ReturnMonScore, -60, 60 );
        score += improvement;

        _ai.CurrentLog.Add( $"Improvement: {improvement}, Score: {score}" );

        bool returnMonUnlikelyToAct = !returnVs_Threat.AttackerMovesFirst && opponentPTKO_ReturnMon >= PotentialToKO.Dangerous;

        if( returnMonUnlikelyToAct )
            score += 40;

        _ai.CurrentLog.Add( $"Returning Pokemon unlikely to act due to being KOd: {returnMonUnlikelyToAct}, Score: {score}" );

        bool losingExchange = returnVs_Threat.Target.BestCurrentPTKO >= PotentialToKO.Dangerous && returnVs_Threat.Attacker.BestCurrentPTKO <= PotentialToKO.Risky && !returnVs_Threat.AttackerMovesFirst;

        if( losingExchange )
            score += 30;

        _ai.CurrentLog.Add( $"Losing Exchange: {losingExchange}, Score: {score}" );

        if( !scr.IsLegitimate )
            score -= 70;

        _ai.CurrentLog.Add( $"Legit Switch: {scr.IsLegitimate}, Score: {score}" );

        bool switchIsThreatenedByKO = scr.SwitchDefensePTKO >= PotentialToKO.Dangerous;
        bool switchTakesBigDamage = scr.SwitchDefensePTKO >= PotentialToKO.TwoHKO;

        if( switchIsThreatenedByKO )
            score -= 50;
        else if( switchTakesBigDamage )
            score -= 35;

        _ai.CurrentLog.Add( $"Switch is threatened: {switchIsThreatenedByKO}, Switch takes big damage: {switchTakesBigDamage}, Score: {score}" );

        //--Piece Value Modifier
        if( losingExchange && returnMonUnlikelyToAct )
        {
            _ai.CurrentLog.Add( $"Trying to get Piece Value for {_ai.CurrentUnitDeciding.Pokemon.NickName}." );

            if( _ai.Blackboard.OurTeamPieceValues.TryGetValue( _ai.CurrentUnitAdapter.Pokemon, out var pieceValue )  )
            {
                int preservationBias = Mathf.FloorToInt( pieceValue.OffensiveValue * 0.25f );
                preservationBias = Mathf.FloorToInt( preservationBias * ( 1 - context.MyExpendability ) ); //--

                score += preservationBias;

                _ai.CurrentLog.Add( $"Piece Value Preservation Bias: {preservationBias}, Score: {score}" );
            }

            if( scr.CurrentActor.BeginningHPR <= 0.25f && pieceValue.SpeedScore == 0 )
                score -= 15;
        }

        // score += _ai.Get_ConsecutiveSwitchPenalty();
        // _ai.CurrentLog.Add( $"Consecutive switch penalty: Score: {score}" );

        score += _ai.DefensiveSwitch_TempoModifier( tempo );

        _ai.CurrentLog.Add( $"Tempo Switch Modifier: Score: {score}" );

        if( context.IsForcedTrade )
        {
            if( improvement <= 0 )
                score -= context.IsTerminal ? 25 : 40;
        }

        _ai.CurrentLog.Add( $"Is Forced Trade: {context.IsForcedTrade}. Is Terminal: {context.IsTerminal} Score: {score}" );

        //--Penalty for likely undoing a pivot
        if( _ai.LastSentInPokemon != null )
        {
            if( scr.Pokemon == _ai.LastSentInPokemon )
            {
                score -= 50;
            
                bool lastMonStillOnField = false;
                for( int i = 0; i < _ai.LastOpposingPokemon.Count; i++ )
                {
                    var lastOpp = _ai.LastOpposingPokemon[i];
                    if( lastOpp.PID == _ai.Blackboard.TheirActiveBattleAIUnits[i]?.PID )
                    {
                        lastMonStillOnField = true;
                        _ai.CurrentLog.Add( "Defensive Switch's Candidate's Last Opponent is still on the field! Skipping!");
                        break;
                    }
                    else
                        continue;
                }

                if( lastMonStillOnField )
                    score -= 70;
            }
        }

        //--Opponent Switches Predictions
        // float opponentSwitchProb = usVS_Threat.OpponentSwitchProbability;
        // score -= Mathf.FloorToInt( 75f * opponentSwitchProb );

        if( intentTOP.OpponentSwitched )
        {
            score -= 75;
            _ai.CurrentLog.Add( $"Opponent modeling thinks the opponent switches. Score: {score}" );
        }

        //--HP Check
        if( intentTOP.Opponent.BeginningHPR <= 0.25f )
        {
            score -= 35; // don't switch if opponent is about to die
        }
        else if( intentTOP.Opponent.BeginningHPR <= 0.45f && returnVs_Threat.Attacker.BestCurrentPTKO > PotentialToKO.TwoHKO )
            score -= 15;

        //--Attacking is better penalty
        if( returnVs_Threat.Attacker.BestCurrentPTKO == PotentialToKO.OHKO && returnVs_Threat.AttackerMovesFirst )
            score -= 50;
        else if( returnVs_Threat.Attacker.BestCurrentPTKO == PotentialToKO.Dangerous && returnVs_Threat.AttackerMovesFirst )
            score -= 35;

        //--Switch tax
        score -= 10;

        _ai.CurrentLog.Add( $"===[Final Switch Score after Tax: {score}]===" );
        return score;
    }

//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================
//======================================================================================[OFFENSIVE SWITCH SCORE]====================================================================================================
//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================

    public int OffensiveSwitchScore( TempoStateResult tempo, ExchangePack pack, BoardContext context, SwitchCandidateResult scr, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        _ai.CurrentLog.Add( $"=====================================" );
        _ai.CurrentLog.Add( $"===[Offensive Switch Action Score]===" );
        _ai.CurrentLog.Add( $"=====================================" );
        _ai.CurrentLog.Add( $"" );

        int score = 0;
        string switchName = "none";

        var usVS_Threat = _proj.MakeUnitComparison( scr.CurrentActor, tir.Threat );
        // ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        // ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        // ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        //--Tank score if unable to switch
        if( scr.Pokemon == null || _ai.BattleSystem.BattleType == BattleType.WildBattle_1v1 || _ai.Check_IsLastPokemon( _ai.CurrentUnitAdapter.Pokemon ) )
        {
            _ai.CurrentLog.Add( $"No switch available (null, wild battle, or last pokemon). Tanking Score!" );
            return -999;
        }

        switchName = scr.Pokemon.NickName;

        _ai.CurrentLog.Add( $"===[Beginning Offensive Switch Scoring for Candidate {switchName}]===" );
        _ai.CurrentLog.Add( $"IntentTOP Information. Attacker: {intentTOP.Attacker?.Name}, Opponent: {intentTOP.Opponent?.Name}, Threat: {tir.Threat?.Name}" );

        if( intentTOP.OpponentPTKO == PotentialToKO.OHKO )
        {
            _ai.CurrentLog.Add( $"Switch candidate {intentTOP.Opponent.Name}'s potential to be KO'd on switch in is OHKO! Tanking Score!" );
            return -999;
        }
        else if( intentTOP.OpponentPTKO >= PotentialToKO.Dangerous )
        {
            score -= 250;
        }
        // else if( scr.SwitchDefensePTKO == PotentialToKO.OHKO )
        // {
        //     // if( tir.Confidence < 0.75f )
        //         // score -= 150;
        //     // else
        //         score -= 75;
        // }

        int offensiveDelta = _proj.Get_PotentialToKOScoreFromEnum( scr.SwitchOffensePTKO ) - _proj.Get_PotentialToKOScoreFromEnum( scr.SwitchDefensePTKO ); //--should be offensive ptko score minus defensive ptko score.
        score += Mathf.Clamp( Mathf.FloorToInt( offensiveDelta * 0.5f ), 0, 40 );

        _ai.CurrentLog.Add( $"Offensive PTKOR Score: {_proj.Get_PotentialToKOScoreFromEnum( scr.SwitchOffensePTKO )}, Defensive PTKOR Score: {_proj.Get_PotentialToKOScoreFromEnum( scr.SwitchDefensePTKO )}, Delta: {offensiveDelta}." );

        BattleAI_PokemonAdapter candidateAdapter = _ai.GetPokemonAs_Adapter( scr.Pokemon );
        if( scr.Pokemon != null && _ai.Blackboard.OurTeamPieceValues.TryGetValue( candidateAdapter.Pokemon, out var pieceValue ) )
        {
            int switchThreatCount = pieceValue.ThreatCount;

            if( switchThreatCount == 2 )
                score += 10;
            else if( switchThreatCount >= 3 )
                score += 20;

            _ai.CurrentLog.Add( $"Threat Count: {switchThreatCount}. Score: {score}" );
        }

        bool switchThreatensKO          = scr.SwitchOffensePTKO >= PotentialToKO.Dangerous;
        bool switchIsThreatenedByKO     = scr.SwitchDefensePTKO >= PotentialToKO.Dangerous;
        bool switchDoesBigDamage        = scr.SwitchOffensePTKO >= PotentialToKO.TwoHKO;
        bool switchTakesBigDamage       = scr.SwitchDefensePTKO >= PotentialToKO.TwoHKO;
        bool switchMovesFirst           = scr.MovesFirst;

        if( !switchMovesFirst )
            score -= 10;

        if( ( switchThreatensKO || switchDoesBigDamage ) && switchMovesFirst && !switchIsThreatenedByKO )
            score += 75;
        else if( switchIsThreatenedByKO && !switchThreatensKO )
            score -= 45;

        if( !switchThreatensKO && !switchDoesBigDamage && ( switchTakesBigDamage || switchIsThreatenedByKO ) )
            score -= 45;

        _ai.CurrentLog.Add( $"SwitchThreatensKO {switchThreatensKO}, SwitchMovesFirst {switchMovesFirst}, !switchIsThreatenedByKO {!switchIsThreatenedByKO}. Score: {score}" );

        var defensePTKO = scr.SwitchDefensePTKO;
        float incomingDamage = _proj.Get_PTKODamagePercent( defensePTKO );

        if( incomingDamage >= 0.75f )
            score -= 75;
        else if( incomingDamage >= 0.5f )
            score -= 50;
        else if( incomingDamage >= 0.25f )
            score -= 25;

        _ai.CurrentLog.Add( $"Switch's DefensePTKO (opponent's potential to ko us): {defensePTKO}. Switch's Likely damage taken: {incomingDamage}. Score: {score}" );

        //--Tempo
        score += _ai.OffensiveSwitch_TempoModifier( tempo );
        _ai.CurrentLog.Add( $"Applied tempo modifier. Score: {score}" );

        //--Attacking is better penalty
        if( usVS_Threat.Attacker.BestCurrentPTKO == PotentialToKO.OHKO && usVS_Threat.AttackerMovesFirst )
            score -= 150;
        else if( usVS_Threat.Attacker.BestCurrentPTKO == PotentialToKO.Dangerous && usVS_Threat.AttackerMovesFirst )
            score -= 125;
        else if( usVS_Threat.Attacker.BestCurrentPTKO >= PotentialToKO.Dangerous && usVS_Threat.Target.BestCurrentPTKO <= PotentialToKO.TwoHKO )
            score -= 100;

        _ai.CurrentLog.Add( $"Applied attacking is better penalty. Score: {score}" );

        //--Switch Tax
        score += _ai.Get_ConsecutiveSwitchPenalty();
        score -= 15;

        _ai.CurrentLog.Add( $"Applied consecutive switch pentalty and switch tax. Final Score: {score}" );

        return score;
    }

//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================
//=========================================================================================[SETUP SCORE]============================================================================================================
//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================

    public int SetupScore( TempoStateResult tempo, ExchangePack pack, BoardContext context, SetupThreatResult setup, TurnOutcomeProjection intentTOP, ThreatIntentResult tir  )
    {
        _ai.CurrentLog.Add( $"==========================" );
        _ai.CurrentLog.Add( $"===[Setup Action Score]===" );
        _ai.CurrentLog.Add( $"==========================" );
        _ai.CurrentLog.Add( $"" );

        int score = 0;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var attackerName = usVS_Threat.AttackerName;
        var targetName = usVS_Threat.OpponentName;

        var myPTKO_AfterSetup = setup.AfterPTKOR;
        var theirPTKO = usVS_Threat.OpponentPTKOR.PTKO;
        var theirIntentPTKO = intentTOP.OpponentPTKO;

        string moveName = "NONE";

        if( setup.Move != null )
            moveName = setup.Move.MoveSO.Name;
        else
        {
            _ai.CurrentLog.Add( $"({attackerName}) Had no viable setup move! Tanking Score!" );
            return -999;
        }

        _ai.CurrentLog.Add( $"===[Beginning Setup Scoring for {attackerName} ({moveName}) vs {targetName}. Tempo: {tempo.TempoState}, My PTKO Them after setup: {myPTKO_AfterSetup.PTKO}, their PTKO on me now: (eval){theirPTKO} (intent){theirIntentPTKO}]===" );
        _ai.CurrentLog.Add( $"IntentTOP Information. Attacker: {intentTOP.Attacker?.Name}, Opponent: {intentTOP.Opponent?.Name}, Threat: tir.Threat.Name //not implemented yet" );

        //--These are much tighter/more risky because setting up can drastically change an outcome. defensive setup can swing ptko chances, while offensive setup can threaten KOs across entire teams, making your current hp potentially irrelevant
        if( theirIntentPTKO >= PotentialToKO.Dangerous && !usVS_Threat.AttackerMovesFirst )
        {
            _ai.CurrentLog.Add( $"The intentTOP says we're likely to die if we setup now! Tanking Score!" );
            return -999;
        }

        if( theirIntentPTKO == PotentialToKO.OHKO )
        {
            _ai.CurrentLog.Add( $"The intentTOP says we're likely to die if we setup now! Tanking Score!" );
            return -999;
        }

        if( theirPTKO >= PotentialToKO.Dangerous && !usVS_Threat.AttackerMovesFirst )
        {
            _ai.CurrentLog.Add( $"We're likely to die if we setup now!" );
            score -= 70;
        }

        if( theirPTKO == PotentialToKO.OHKO )
        {
            _ai.CurrentLog.Add( $"We're likely to die if we setup now!" );
            score -= 80;
        }

        //--Setup Value base
        score += setup.SetupValue;
        _ai.CurrentLog.Add( $"Added setup value. Score: {score}" );

        //--Discourage setup if we can already KO AND we aren't very tanky vs our current opponent. We DO want to setup if we can take some hits, especially if we're defensively setting up or going for iron defense body press.
        if( usVS_Threat.AttackerThreatensKO && theirPTKO < PotentialToKO.TwoHKO )
        {
            if( usVS_Threat.AttackerMovesFirst )
                score -= 60;
            else
                score -= 30;
        }

        //--If we are likely to KO next turn
        if( myPTKO_AfterSetup.PTKO >= PotentialToKO.Dangerous && usVS_Threat.AttackerMovesFirst )
            score += 30;
        else if( myPTKO_AfterSetup.PTKO >= PotentialToKO.Risky )
            score += 20;
        else if( myPTKO_AfterSetup.PTKO <= PotentialToKO.Risky && !usVS_Threat.AttackerMovesFirst )
            score -= 45;
        else if( myPTKO_AfterSetup.PTKO <= PotentialToKO.Risky )
            score -= 35;

        _ai.CurrentLog.Add( $"Checked current PTKO. Score: {score}" );

        //--Sweep Count
        if( setup.SweepCount > 3 )
            score += 40;
        else if( setup.SweepCount > 0 )
            score += setup.SweepCount * 10;

        _ai.CurrentLog.Add( $"Checked Sweep Count. Score: {score}" );

        //--Improved survivability across opponent's entire remaining pieces
        score += setup.ImprovedPTKOs * 10;

        _ai.CurrentLog.Add( $"Checked Sweep Count. Score: {score}" );

        //--If the opponent is likely to switch, we should consider setting up. If they don't, maybe it's not the best idea even if we survive.
        float switchProb = usVS_Threat.OpponentSwitchProbability;

        float dangerWeight =
            theirPTKO >= PotentialToKO.OHKO ? 1.25f :
            theirPTKO >= PotentialToKO.Dangerous ? 1.0f :
            theirPTKO >= PotentialToKO.Risky ? 0.75f :
            theirPTKO >= PotentialToKO.TwoHKO ? 0.5f : 0.25f;

        if( usVS_Threat.OpponentMovesFirst )
            dangerWeight *= 1.5f;

        float penalty = 60f * dangerWeight;

        score += Mathf.FloorToInt( switchProb * 25f );
        score -= Mathf.FloorToInt( ( 1f - switchProb ) * penalty );

        _ai.CurrentLog.Add( $"Opponent Switch Probability: {switchProb}. Score: {score}" );

        score += _ai.Setup_TempoModifier( tempo );
        _ai.CurrentLog.Add( $"Checked Tempo Modifier. Score: {score}" );

        //--Multiple setup attempt penalty.
        score -= _ai.SetupAmount * 20;

        return score;
    }

//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================
//======================================================================================[OFFENSIVE STATUS SCORE]====================================================================================================
//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================

    public int OffensiveStatusScore( TempoStateResult tempo, ExchangePack pack, BoardContext context, StatusThreatResult status, TurnOutcomeProjection intentTOP, ThreatIntentResult tir  )
    {
        _ai.CurrentLog.Add( $"=====================================" );
        _ai.CurrentLog.Add( $"===[Offensive Status Action Score]===" );
        _ai.CurrentLog.Add( $"=====================================" );
        _ai.CurrentLog.Add( $"" );

        int score = 0;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var attackerName = usVS_Threat.AttackerName;
        var targetName = usVS_Threat.OpponentName;

        var ourPTKO = status.AttackerPTKOR;
        var theirPTKO = usVS_Threat.OpponentPTKOR;

        var theirIntentPTKO = intentTOP.OpponentPTKO;

        string moveName = "NONE";

        if( status.Move == null )
        {
            _ai.CurrentLog.Add( $"({attackerName}) Had no viable offensive status move! Tanking Score!" );
            return -999;
        }
        else
            moveName = status.Move.MoveSO.Name;

        //--Survival check
        if( theirIntentPTKO >= PotentialToKO.Dangerous && !intentTOP.AttackerMovedFirst )
        {
            _ai.CurrentLog.Add( $"The intentTOP says we're likely to die with no progress from it if we use an offensive status move now! Tanking Score!" );
            return -999;
        }
        else if( theirIntentPTKO == PotentialToKO.Risky && !intentTOP.AttackerMovedFirst )
        {
            _ai.CurrentLog.Add( $"The intentTOP says we're likely to die with no progress from it if we use an offensive status move now! Tanking Score!" );
            return -999;
        }

        if( theirPTKO.PTKO >= PotentialToKO.Dangerous && !usVS_Threat.AttackerMovesFirst )
        {
            _ai.CurrentLog.Add( $"We're likely to die with no progress from it if we use an offensive status move now!" );
            score -= 80;
        }
        else if( theirPTKO.PTKO >= PotentialToKO.Risky && !usVS_Threat.AttackerMovesFirst )
        {
            _ai.CurrentLog.Add( $"We're likely to die with no progress from it if we use an offensive status move now!" );
            score -= 70;
        }

        //--Base value
        _ai.CurrentLog.Add( $"===[Beginning Offensive Status Scoring for {attackerName} ({moveName}) vs {targetName}. Tempo: {tempo.TempoState}, My PTKO Them: {ourPTKO.PTKO}, their PTKO on me: (eval){theirPTKO.PTKO} (intent){theirIntentPTKO}]===" );

        if( status.OffensiveStatusType == OffensiveStatusType.EntryHazard )
        {
            score += status.Coverage
                - Mathf.FloorToInt( status.Impact * 0.5f )
                + Mathf.FloorToInt( status.Ambiguity * 0.5f )
                + Mathf.FloorToInt( status.Reliability * 0.4f );

            _ai.CurrentLog.Add( $"Entry Hazard detected! Team Coverage: {status.Coverage}, Impact (50%): {Mathf.FloorToInt( status.Impact * 0.5f )}, Ambiguity (50%): {status.Ambiguity}. Base Score: {score}" );

            if( _ai.Round == 1 )
                score += 65;
            else if( _ai.Round <= 3 )
                score += 30;
            else if( _ai.Round < 6 )
                score -= 15;
            else if( _ai.Round > 6 )
                score -= 50;

            int remainingOpponents = _ai.GetRemainingOpposingPokemon( _ai.CurrentUnitAdapter.Pokemon ).Count;
            score += remainingOpponents * 5;

            _ai.CurrentLog.Add( $"Assessed current round ({_ai.Round}), intent (are we lead? probably if current round is < 3), and remaining opponents ({remainingOpponents}). Score: {score}" );
        }

        if( status.OffensiveStatusType == OffensiveStatusType.StatusEffect || status.OffensiveStatusType == OffensiveStatusType.StatDebuff )
        {
            score += status.Impact
                - Mathf.FloorToInt( status.Coverage * 0.5f )
                + Mathf.FloorToInt( status.Reliability * 0.5f );

            _ai.CurrentLog.Add( $"Status Effect or Status Debuff detected! Impact: {status.Impact}, Coverage (50%) {Mathf.FloorToInt( status.Coverage * 0.5f )}, Reliability (50%): {Mathf.FloorToInt( status.Reliability * 0.5f )}. Base Score: {score}" );

            //--Disruption bonus
            if( !status.Top.Opponent_ExpectedToAct )
                score += 60;

            _ai.CurrentLog.Add( $"Opponent Can Act: {status.Top.Opponent_ExpectedToAct}. Score: {score}" );

            int attackEquivalent = usVS_Threat.AttackerPTKOR.Score - usVS_Threat.OpponentPTKOR.Score;
            if( status.Impact < attackEquivalent )
                score -= 40;

            _ai.CurrentLog.Add( $"Does attack equivalent ({attackEquivalent}) outweigh immediate impact ({status.Impact})?. Score: {score}" );

            //--Bonus for punishing a switch with a status effect move like sleep powder or thunder wave
            score += Mathf.FloorToInt( 25f * usVS_Threat.OpponentSwitchProbability );
        }

        if( status.OffensiveStatusType == OffensiveStatusType.Disruption )
        {
            score += status.Impact
                + Mathf.FloorToInt( status.Reliability * 0.6f )
                + Mathf.FloorToInt( status.Ambiguity * 0.5f )
                - Mathf.FloorToInt( status.Coverage * 0.25f );

            _ai.CurrentLog.Add( $"Disruption detected! Impact: {status.Impact}, Reliability (60%): {Mathf.FloorToInt( status.Reliability * 0.6f )}, Ambiguity (50%): {Mathf.FloorToInt( status.Ambiguity * 0.5f )}, Coverage (25%): {Mathf.FloorToInt( status.Coverage * 0.25f )}. Score: {score}" );

            if( !status.Top.Opponent_ExpectedToAct )
                score += 60;

            int attackEquivalent = usVS_Threat.AttackerPTKOR.Score - usVS_Threat.OpponentPTKOR.Score;

            if( status.Impact < attackEquivalent )
                score -= 30;

            score += Mathf.FloorToInt( 20f * usVS_Threat.OpponentSwitchProbability );
        }

        if( status.OffensiveStatusType == OffensiveStatusType.Phaze )
        {
            score += status.Impact
                + status.Coverage
                + Mathf.FloorToInt( status.Ambiguity * 0.5f )
                + Mathf.FloorToInt( status.Reliability * 0.3f );

            _ai.CurrentLog.Add( $"Phazing detected! Impact: {status.Impact}, Coverage: {status.Coverage}, Ambiguity (50%): {Mathf.FloorToInt( status.Ambiguity * 0.5f )}, Reliability (30%): {Mathf.FloorToInt( status.Reliability * 0.3f )}. Score: {score}" );

            if( usVS_Threat.OpponentSwitchProbability < 0.4f )
                score += 20;

            if( status.Top.Opponent_ExpectedToAct )
                score -= 15;
        }

        if( usVS_Threat.ExchangeState == ExchangeState.Pressure )
            score += 15;

        float switchProb = usVS_Threat.OpponentSwitchProbability;

        float dangerWeight =
            theirPTKO.PTKO >= PotentialToKO.OHKO ? 1.25f :
            theirPTKO.PTKO >= PotentialToKO.Dangerous ? 1.0f :
            theirPTKO.PTKO >= PotentialToKO.Risky ? 0.75f :
            theirPTKO.PTKO >= PotentialToKO.TwoHKO ? 0.5f : 0.25f;

        if( usVS_Threat.OpponentMovesFirst )
            dangerWeight *= 1.5f;

        float penalty = 50f * dangerWeight;

        score += Mathf.FloorToInt( switchProb * 75f );
        score -= Mathf.FloorToInt( ( 1f - switchProb ) * penalty );

        _ai.CurrentLog.Add( $"Opponent Switch Probability: {switchProb}. Score: {score}" );

        //--Don’t overuse if attack is better
        if( usVS_Threat.AttackerThreatensKO )
        {
            if( usVS_Threat.AttackerMovesFirst )
                score -= 80;
            else
                score -= 40;
        }

        _ai.CurrentLog.Add( $"Checked if attacking may be better. Attacker Threatens KO: {usVS_Threat.AttackerThreatensKO}. Attacker Moves First: {usVS_Threat.AttackerMovesFirst} Score: {score}" );

        //--Status for survival incentive
        if( usVS_Threat.OpponentThreatensKO )
        {
            if( usVS_Threat.AttackerMovesFirst && ( status.OffensiveStatusType == OffensiveStatusType.StatusEffect || status.OffensiveStatusType == OffensiveStatusType.Disruption || status.OffensiveStatusType == OffensiveStatusType.Phaze ) )
                score += 25;
            else if( status.Top.Opponent_ExpectedToAct )
                score -= 150;
        }
        else if( usVS_Threat.OpponentPTKOR.PTKO >= PotentialToKO.Risky )
            score -= 75;

        _ai.CurrentLog.Add( $"Checked Survival. Opponent Threatens KO: {usVS_Threat.OpponentThreatensKO}. Opponent acts in simulation: {status.Top.Opponent_ExpectedToAct} Score: {score}" );

        //--HP context
        float hp = usVS_Threat.AttackerHPR;

        if( hp <= 0.25f )
            score -= 30;
        else if( hp <= 0.45f && usVS_Threat.OpponentThreatensKO )
            score -= 20;
        else if( hp >= 0.7f && !usVS_Threat.OpponentThreatensKO )
            score += 10;

        _ai.CurrentLog.Add( $"HP: {hp}. Score: {score}" );

        //--Tempo
        score += _ai.Setup_TempoModifier( tempo );

        _ai.CurrentLog.Add( $"Checked Tempo. Score: {score}" );

        return score;
    }

//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================
//=====================================================================================[SUPPORTIVE STATUS SCORE]====================================================================================================
//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================

    public int SupportiveStatusScore( TempoStateResult tempo, ExchangePack pack, BoardContext context, StatusThreatResult status, TurnOutcomeProjection intentTOP, ThreatIntentResult tir  )
    {
        _ai.CurrentLog.Add( $"======================================" );
        _ai.CurrentLog.Add( $"===[Supportive Status Action Score]===" );
        _ai.CurrentLog.Add( $"======================================" );
        _ai.CurrentLog.Add( $"" );

        int score = 0;

        ExchangeEvaluation usVS_Threat = pack.UsVS_Threat;
        ExchangeEvaluation usVS_ThreatAlly = pack.UsVS_ThreatAlly;
        ExchangeEvaluation allyVS_Threat = pack.AllyVS_Threat;
        ExchangeEvaluation allyVS_ThreatAlly = pack.AllyVS_ThreatAlly;

        var attackerName = usVS_Threat.AttackerName;
        var targetName = usVS_Threat.OpponentName;

        var ourPTKO = status.AttackerPTKOR.PTKO;
        var theirPTKO = usVS_Threat.OpponentPTKOR.PTKO;

        var theirIntentPTKO = intentTOP.OpponentPTKO;

        string moveName = status.Move != null ? status.Move.MoveSO.Name : "NONE";

        _ai.CurrentLog.Add( $"===[Beginning Supportive Status Scoring for {attackerName} ({moveName}) vs {targetName}. Tempo: {tempo.TempoState}, My PTKO Them: {ourPTKO}, their PTKO on me: (eval){theirPTKO} (intent){theirIntentPTKO}]===" );
        _ai.CurrentLog.Add( $"We're looking to use a {status.SupportiveStatusType} move." );

        if( status.Move == null )
        {
            _ai.CurrentLog.Add( $"({attackerName}) Had no viable supportive status move! Tanking Score!" );
            return -999;
        }

        //--Survival check. This will need to become way more nuanced and potentially allowed to breathe.
        if( theirIntentPTKO >= PotentialToKO.Dangerous && !intentTOP.AttackerMovedFirst )
        {
            _ai.CurrentLog.Add( $"The intentTOP says we're likely to die with no progress from it if we use a supportive status move now! Tanking Score!" );
            return -999;
        }

        if( theirPTKO >= PotentialToKO.Dangerous && !usVS_Threat.AttackerMovesFirst )
        {
            score -= 80;
            _ai.CurrentLog.Add( $"We're likely to die with no progress from it if we use a supportive status move now! Score: {score}" );
        }
        else if( theirPTKO >= PotentialToKO.Risky && !usVS_Threat.AttackerMovesFirst )
        {
            score -= 70;
            _ai.CurrentLog.Add( $"We're likely to die with no progress from it if we use a supportive status move now! Score: {score}" );
        }

        BattleAI_PokemonAdapter ally = _ai.GetActiveAllyAs_Adapter( _ai.CurrentUnitAdapter.Pokemon );
        BattleAI_PokemonAdapter threatAlly = tir.Threat == null ? _ai.GetActiveAllyAs_Adapter( intentTOP.Opponent.Pokemon ) : _ai.GetActiveAllyAs_Adapter( tir.Threat.Pokemon );
        if( pack.IsDoubles )
        {
            if( status.Targets?.Count > 0 && status.Targets[0].Pokemon == _ai.CurrentUnitAdapter.Pokemon )
            {
                if( ally != null )
                {
                    var threat = tir.Threat ?? intentTOP.Opponent;
                    allyVS_Threat = _ai.Projection.EvaluateExchange( ally, threat );

                    if( threatAlly != null )
                    {
                        usVS_ThreatAlly = _ai.Projection.EvaluateExchange( _ai.CurrentUnitAdapter, threatAlly );
                        allyVS_ThreatAlly = _ai.Projection.EvaluateExchange( ally, threatAlly );
                    }
                }
            }
        }

        //--Base Score
        int baseScore = 0;
        switch( status.SupportiveStatusType )
        {
            case SupportiveStatusType.Recovery:

                baseScore += status.Stability;
                baseScore += status.Reliability;
                baseScore += status.Impact;

                baseScore += status.Unique / 4;
                baseScore += status.StrategicReach / 4;

                break;

            case SupportiveStatusType.ForceMultiplier:
            case SupportiveStatusType.BattlefieldControl:

                baseScore += status.StrategicReach;
                baseScore += status.Impact;
                baseScore += status.Reliability / 2;

                baseScore += status.Stability / 2;

                break;

            case SupportiveStatusType.AllyProtection:

                baseScore += status.Stability / 2;
                baseScore += status.Impact;
                baseScore += status.Unique / 2;
                baseScore += status.Reliability;

                break;
        }

        score += baseScore;
        _ai.CurrentLog.Add( $"Base Score: {baseScore}. Score: {score}" );

        if( ally != null && status.SupportiveStatusType != SupportiveStatusType.Recovery )
        {
            if( _ai.Blackboard.GamePlan.OurPrimaryWinCon == ally.Pokemon || _ai.Blackboard.GamePlan.OurBlockers.Contains( ally.Pokemon ) || _ai.Blackboard.GamePlan.OurEnablers.Contains( ally.Pokemon ) )
            {
                score += 25;
                _ai.CurrentLog.Add( $"We're attempting to use a support move that involves a game plan aligned ally. Score: {score}" );
            }
        }

        //--Opportunity Adjustments
        if( status.SupportiveStatusType == SupportiveStatusType.Recovery )
        {
            if( theirPTKO != PotentialToKO.OHKO )
            {
                score += 25;
                _ai.CurrentLog.Add( $"They don't OHKO us this turn. Score: {score}" );
            }
            else if( theirPTKO >= PotentialToKO.Dangerous && usVS_Threat.AttackerMovesFirst )
            {
                score += 25;
                _ai.CurrentLog.Add( $"They threaten a KO but we move first. Score: {score}" );
            }
            else if( theirPTKO >= PotentialToKO.Dangerous && usVS_Threat.OpponentMovesFirst )
            {
                score -= 20;
                _ai.CurrentLog.Add( $"They threaten a KO and they move first. Reinforcing likely death penalty due to this being a self healing move. Score: {score}" );
            }

            if( theirPTKO <= PotentialToKO.TwoHKO )
            {
                if( usVS_Threat.AttackerPTKOR.PTKO >= PotentialToKO.Dangerous && usVS_Threat.AttackerMovesFirst && _ai.CurrentUnitAdapter.BeginningHPR >= 0.5f  )
                {
                    score -= 50;
                    _ai.CurrentLog.Add( $"They don't threaten us much and we have a good KO chance on them. Score: {score}." );
                }

                if( _ai.CurrentUnitAdapter.BeginningHPR >= 0.55f )
                {
                    score -= 25;
                    _ai.CurrentLog.Add( $"They don't threaten us much and we have more than 55% hp remaining. Score: {score}" );
                }
                else
                {
                    score += 15;
                    _ai.CurrentLog.Add( $"They don't threaten us much but we have less than 55% hp remaining. Score: {score}" );
                    float hpr = _ai.CurrentUnitAdapter.BeginningHPR;
                    if( hpr >= 0.4f )
                        score += 10;
                    else if( hpr >= 0.3f )
                        score += 20;
                    else if( hpr >= 0.2f )
                        score += 30;
                    else if( hpr < 0.2f )
                        score += 45;
                }
            }

            // score += Mathf.RoundToInt( 10f * usVS_Threat.OpponentSwitchProbability );
            // _ai.CurrentLog.Add( $"Opponent switches because of us check. Score: {score}" );

            // score += ally != null ? Mathf.RoundToInt( 10f * allyVS_Threat.OpponentSwitchProbability ) : 0;
            // _ai.CurrentLog.Add( $"Opponent switches because of our ally check. Score: {score}" );

            // score += threatAlly != null ? Mathf.RoundToInt( 10f * usVS_ThreatAlly.OpponentSwitchProbability ) : 0;
            // _ai.CurrentLog.Add( $"Opponent's ally switches because of us check. Score: {score}" );

            // score += threatAlly != null && ally != null ? Mathf.RoundToInt( 10f * allyVS_ThreatAlly.OpponentSwitchProbability ) : 0;
            // _ai.CurrentLog.Add( $"Opponent's ally switches because of our ally check. Score: {score}" );

        }

        if( status.SupportiveStatusType == SupportiveStatusType.ForceMultiplier || status.SupportiveStatusType == SupportiveStatusType.BattlefieldControl )
        {
            _ai.CurrentLog.Add( $"No real way to currently check direct PTKO and speed change results between units in the case of a Force Multiplier or Battlefield Control move. Score: {score}" );
            if( ally != null )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Ally exists and we're trying to improve the overall field for us and them. Score: {score}" );
            }

            if( threatAlly != null )
            {
                score += 10;
                _ai.CurrentLog.Add( $"Threat's Ally exists and we're trying to improve the overall field for us and reduce it for them. Score: {score}" );
            }

            // score += Mathf.RoundToInt( 20f * usVS_Threat.OpponentSwitchProbability );
            // _ai.CurrentLog.Add( $"Opponent switches because of us check. Score: {score}" );

            // score += ally != null ? Mathf.RoundToInt( 20f * allyVS_Threat.OpponentSwitchProbability ) : 0;
            // _ai.CurrentLog.Add( $"Opponent switches because of our ally check. Score: {score}" );

            // score += threatAlly != null ? Mathf.RoundToInt( 20f * usVS_ThreatAlly.OpponentSwitchProbability ) : 0;
            // _ai.CurrentLog.Add( $"Opponent's ally switches because of us check. Score: {score}" );

            // score += threatAlly != null && ally != null ? Mathf.RoundToInt( 20f * allyVS_ThreatAlly.OpponentSwitchProbability ) : 0;
            // _ai.CurrentLog.Add( $"Opponent's ally switches because of our ally check. Score: {score}" );
        }

        if( status.SupportiveStatusType == SupportiveStatusType.AllyProtection )
        {
            if( status.Move.MoveSO.MoveEffects.TransientStatus == TransientConditionID.CenterOfAttention )
            {
                if( ally != null )
                {
                    var threatVSAllyPTKO = allyVS_Threat.OpponentPTKOR.PTKO;
                    if( threatVSAllyPTKO >= PotentialToKO.Risky )
                    {
                        if( allyVS_Threat.AttackerMovesFirst && allyVS_Threat.AttackerPTKOR.PTKO >= PotentialToKO.Dangerous )
                        {
                            score -= 30;
                            _ai.CurrentLog.Add( $"Redirecting the threat's attack when our ally is faster and is likely to KO it. Score: {score}" );
                        }
                        else
                        {
                            score += 10;
                            _ai.CurrentLog.Add( $"Redirecting the threat's attack when our ally is likely to take big damage. Score: {score}" );

                            if( threatVSAllyPTKO >= PotentialToKO.Dangerous )
                            {
                                score += 5;
                                _ai.CurrentLog.Add( $"Redirecting the threat's attack when our ally may likely be KO'd. Score: {score}" );
                            }

                            if( theirPTKO <= PotentialToKO.Risky ) //--this is the target's ptko on us, the redirector. likely living the redirection is good.
                            {
                                score += 10;
                                _ai.CurrentLog.Add( $"Redirecting the threat's attack when we handle it fairly well. Score: {score}" );
                            }
                        }

                        if( threatAlly != null )
                        {
                            var threatAllyVSAllyPTKO = allyVS_ThreatAlly.OpponentPTKOR.PTKO;
                            if( threatAllyVSAllyPTKO >= PotentialToKO.Risky )
                            {
                                if( allyVS_ThreatAlly.AttackerMovesFirst && allyVS_ThreatAlly.AttackerPTKOR.PTKO >= PotentialToKO.Dangerous )
                                {
                                    score -= 30;
                                    _ai.CurrentLog.Add( $"Redirecting the threat's ally's attack when our ally is faster and is likely to KO it. Score: {score}" );
                                }
                                else
                                {
                                    score += 10;
                                    _ai.CurrentLog.Add( $"Redirecting the threat's ally's attack when our ally is likely to take big damage. Score: {score}" );

                                    if( threatAllyVSAllyPTKO >= PotentialToKO.Dangerous )
                                    {
                                        score += 5;
                                        _ai.CurrentLog.Add( $"Redirecting the threat's ally's attack when our ally may likely be KO'd. Score: {score}" );
                                    }

                                    if( usVS_ThreatAlly.OpponentPTKOR.PTKO <= PotentialToKO.Risky ) //--this is the target's ptko on us, the redirector. likely living the redirection is good.
                                    {
                                        score += 10;
                                        _ai.CurrentLog.Add( $"Redirecting the threat's ally's attack when we handle it fairly well. Score: {score}" );
                                    }
                                }
                            }
                        }
                    }
                }

                // score -= Mathf.RoundToInt( 10f * usVS_Threat.OpponentSwitchProbability );
                // _ai.CurrentLog.Add( $"Opponent switches because of us check. Score: {score}" );

                // score -= ally != null ? Mathf.RoundToInt( 10f * allyVS_Threat.OpponentSwitchProbability ) : 0;
                // _ai.CurrentLog.Add( $"Opponent switches because of our ally check. Score: {score}" );

                // score -= threatAlly != null ? Mathf.RoundToInt( 10f * usVS_ThreatAlly.OpponentSwitchProbability ) : 0;
                // _ai.CurrentLog.Add( $"Opponent's ally switches because of us check. Score: {score}" );

                // score -= threatAlly != null && ally != null ? Mathf.RoundToInt( 10f * allyVS_ThreatAlly.OpponentSwitchProbability ) : 0;
                // _ai.CurrentLog.Add( $"Opponent's ally switches because of our ally check. Score: {score}" );
            }
        }

        if( context.IsBehind )
        {
            score += 20;
            _ai.CurrentLog.Add( $"We're currently behind, a support move may give us the edge we need. Score: {score}" );
        }

        // score += _ai.Setup_TempoModifier( tempo );
        // _ai.CurrentLog.Add( $"Applied Tempo Modifier. Score: {score}" );

        return score;
    }
}
