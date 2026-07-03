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

//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================
//=======================================================================================[ATTACK SCORE]=============================================================================================================
//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================

    public int AttackScore( TempoStateResult tempo, ExchangeEvaluation eval, BoardContext context, MoveThreatResult move, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        int score = 0;

        var attackerName = eval.AttackerName;
        var targetName = eval.OpponentName;

        var myPTKO_onTarget = eval.AttackerPTKOR;
        var theirPTKO_onMe = eval.OpponentPTKOR;

        string moveName = "NONE";

        if( move.Move != null )
            moveName = move.Move.MoveSO.Name;
        else
        {
            _ai.CurrentLog.Add( $"({attackerName}) Had no viable attacking move! Tanking Score!" );
            return -999;
        }

        _ai.CurrentLog.Add( $"===[Beginning Attack Scoring for {attackerName} ({moveName}) vs {targetName}. Tempo: {tempo.TempoState}, My PTKO Them: {myPTKO_onTarget.PTKO}, their PTKO on me: {theirPTKO_onMe.PTKO} ({eval.OpponentMoveName})]===" );
        _ai.CurrentLog.Add( $"IntentTOP Information. Opponent mismatch occurs if we read a switch from the opponent. Attacker: {intentTOP.Attacker.Name}, Opponent: {intentTOP.Opponent.Name}, Threat: {tir.Threat.Name}" );

        //--KO Class Advantage
        score += _proj.Get_OffensivePTKOScore( myPTKO_onTarget.Score );
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

        _ai.CurrentLog.Add( $"I Threaten a KO: {iThreatenKO}. I am faster: {iAmFaster}. Score: {score}" );

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

        score += _ai.Attack_TempoModifier( tempo );

        _ai.CurrentLog.Add( $"Tempo check. Score: {score}" );

        if( context.IsBehind )
            score += 20;

        _ai.CurrentLog.Add( $"===[Is Behind: {context.IsBehind}. Final Attack Score: {score}]===" );

        //--Attacking Based on Switch Pressure
        score += Mathf.FloorToInt( 10f * eval.OpponentSwitchProbability );
        _ai.CurrentLog.Add( $"Opponent likey forced to switch. Score: {score}" );
        
        if( eval.ExchangeState == ExchangeState.Pressure )
        {
            score += 10;
            _ai.CurrentLog.Add( $"The pressure is on! Score: {score}" );
        }

        if( context.IsForcedTrade )
        {
            if( eval.AttackerThreatensKO )
                score += 25;
            else if( eval.AttackerPTKOR.PTKO > PotentialToKO.Safe )
                score += 15;
        }

        _ai.CurrentLog.Add( $"Forced Trade: {context.IsForcedTrade}. Score: {score}" );

        //--Flat KO flag check
        if( eval.AttackerPTKOR.PTKO == PotentialToKO.Dangerous && ( !theyThreatenKO || iAmFaster ) )
            score += 10;
        else if( eval.AttackerPTKOR.PTKO == PotentialToKO.OHKO && ( !theyThreatenKO || iAmFaster ) )
            score += 20;

        score += 10; //--default attack incentive

        return score;
    }

//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================
//======================================================================================[DEFENSIVE SWITCH SCORE]====================================================================================================
//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================

    public int DefensiveSwitchScore( TempoStateResult tempo, ExchangeEvaluation eval, BoardContext context, SwitchCandidateResult switchCandidate, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        //--Tank score if unable to switch
        if( switchCandidate.Pokemon == null || _ai.BattleSystem.BattleType == BattleType.WildBattle_1v1 || _ai.Check_IsLastPokemon( _ai.CurrentUnitAdapter.Pokemon ) )
        {
            _ai.CurrentLog.Add( $"No switch available (null, wild battle, or last pokemon). Tanking Score!" );
            return -999;
        }

        int score = 0;

        var attackerName = eval.AttackerName;
        var targetName = eval.OpponentName;
        var switchName = "no switch available!";
        if( switchCandidate.Pokemon != null )
            switchName = switchCandidate.Pokemon.NickName;

        _ai.CurrentLog.Add( $"===[Beginning Defensive Switch Scoring for {attackerName} vs {targetName}. Switch Candidate: {switchName}. Tempo: {tempo.TempoState}]===" );
        _ai.CurrentLog.Add( $"IntentTOP Information. Attacker: {intentTOP.Attacker.Name}, Opponent: {intentTOP.Opponent.Name}, Threat: {tir.Threat.Name}" );

        if( intentTOP.OpponentPTKO == PotentialToKO.OHKO )
        {
            _ai.CurrentLog.Add( $"Switch candidate {intentTOP.Opponent.Name}'s potential to be KO'd on switch in is OHKO! Tanking Score!" );
            return -999;
        }
        else if( switchCandidate.SwitchDefensePTKOR.PTKO == PotentialToKO.OHKO )
        {
            score -= 70;
        }
        
        var currentPTKO = eval.OpponentPTKOR.PTKO;
        var currentScore = eval.OpponentPTKOR.Score;
        var switchPTKOR = switchCandidate.SwitchDefensePTKOR;

        _ai.CurrentLog.Add( $"{targetName}'s Current PTKO me ({attackerName}): {currentPTKO}. {targetName}'s PTKO on Switch Candidate ({switchName}): {switchCandidate.SwitchDefensePTKOR.PTKO}. {switchName}'s PTKO {targetName}: {switchCandidate.SwitchOffensePTKOR.PTKO}" );

        if( context.IsTerminal && context.IsForcedTrade && !switchCandidate.IsLegitimate )
        {
            _ai.CurrentLog.Add( $"Terminal board and no KO class improvement/Switch is illegitimate. Tanking Score!" );
            return -999;
        }

        if( !context.IsTerminal && currentPTKO >= PotentialToKO.Dangerous && switchPTKOR.PTKO >= PotentialToKO.Dangerous )
            score -= 45;

        int improvement = Mathf.Clamp( switchPTKOR.Score - currentScore, -60, 60 );
        score += improvement;

        _ai.CurrentLog.Add( $"Improvement: {improvement}, Score: {score}" );

        bool iDieBeforeActing = !eval.AttackerMovesFirst && eval.OpponentThreatensKO;

        if( iDieBeforeActing )
            score += 40;

        _ai.CurrentLog.Add( $"Die before Act: {iDieBeforeActing}, Score: {score}" );

        bool losingExchange = eval.OpponentThreatensKO && !eval.AttackerThreatensKO;

        if( losingExchange )
            score += 30;

        _ai.CurrentLog.Add( $"Losing Exchange: {losingExchange}, Score: {score}" );

        if( !switchCandidate.IsLegitimate )
            score -= 70;

        _ai.CurrentLog.Add( $"Legit Switch: {switchCandidate.IsLegitimate}, Score: {score}" );

        bool switchIsThreatenedByKO = switchCandidate.SwitchDefensePTKOR.PTKO >= PotentialToKO.Dangerous;
        bool switchTakesBigDamage = switchCandidate.SwitchDefensePTKOR.PTKO >= PotentialToKO.TwoHKO;

        if( switchIsThreatenedByKO || switchTakesBigDamage )
            score -= 35;

        _ai.CurrentLog.Add( $"Switch is threatened: {switchIsThreatenedByKO}, Switch takes big damage: {switchTakesBigDamage}, Score: {score}" );

        //--Piece Value Modifier
        if( losingExchange && iDieBeforeActing )
        {
            _ai.CurrentLog.Add( $"Trying to get Piece Value for {_ai.Unit.Pokemon.NickName}." );

            if( _ai.OurTeamPieceValues.TryGetValue( _ai.CurrentUnitAdapter.Pokemon, out var pieceValue )  )
            {
                int preservationBias = Mathf.FloorToInt( pieceValue.OffensiveValue * 0.25f );
                preservationBias = Mathf.FloorToInt( preservationBias * ( 1 - context.MyExpendability ) ); //--

                score += preservationBias;

                _ai.CurrentLog.Add( $"Piece Value Preservation Bias: {preservationBias}, Score: {score}" );
            }

            if( eval.AttackerHPR <= 0.1f && pieceValue.SpeedScore == 0 )
                score -= 15;
        }

        score += _ai.Get_ConsecutiveSwitchPenalty();

        _ai.CurrentLog.Add( $"Consecutive switch penalty: Score: {score}" );

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
            if( switchCandidate.Pokemon == _ai.LastSentInPokemon )
            {
                score -= 50;
            
                bool lastMonStillOnField = false;
                for( int i = 0; i < _ai.LastOpposingPokemon.Count; i++ )
                {
                    var lastOpp = _ai.LastOpposingPokemon[i];
                    if( lastOpp.PID == _ai.TheirActiveBattleAIUnits[i]?.PID )
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
        float opponentSwitchProb = eval.OpponentSwitchProbability;
        score -= Mathf.FloorToInt( 75f * opponentSwitchProb );

        //--HP Check
        if( eval.OpponentHPR <= 0.25f )
        {
            score -= 35; // don't switch if opponent is about to die
        }
        else if( eval.OpponentHPR <= 0.45f && eval.AttackerPTKOR.PTKO > PotentialToKO.TwoHKO )
            score -= 15;

        //--Attacking is better penalty
        if( eval.AttackerPTKOR.PTKO == PotentialToKO.OHKO && eval.AttackerMovesFirst )
            score -= 50;
        else if( eval.AttackerPTKOR.PTKO == PotentialToKO.Dangerous && eval.AttackerMovesFirst )
            score -= 35;

        //--Switch tax
        score -= 25;

        _ai.CurrentLog.Add( $"===[Final Switch Score after Tax: {score}]===" );
        return score;
    }

//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================
//======================================================================================[OFFENSIVE SWITCH SCORE]====================================================================================================
//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================

    public int OffensiveSwitchScore( TempoStateResult tempo, ExchangeEvaluation eval, SwitchCandidateResult switchCandidate, BoardContext context, TurnOutcomeProjection intentTOP, ThreatIntentResult tir )
    {
        int score = 0;
        string switchName = "none";

        //--Tank score if unable to switch
        if( switchCandidate.Pokemon == null || _ai.BattleSystem.BattleType == BattleType.WildBattle_1v1 || _ai.Check_IsLastPokemon( _ai.CurrentUnitAdapter.Pokemon ) )
        {
            _ai.CurrentLog.Add( $"No switch available (null, wild battle, or last pokemon). Tanking Score!" );
            return -999;
        }

        switchName = switchCandidate.Pokemon.NickName;

        _ai.CurrentLog.Add( $"===[Beginning Offensive Switch Scoring for Candidate {switchName}]===" );
        _ai.CurrentLog.Add( $"IntentTOP Information. Attacker: {intentTOP.Attacker.Name}, Opponent: {intentTOP.Opponent.Name}, Threat: {tir.Threat.Name}" );

        if( intentTOP.OpponentPTKO == PotentialToKO.OHKO )
        {
            _ai.CurrentLog.Add( $"Switch candidate {intentTOP.Opponent.Name}'s potential to be KO'd on switch in is OHKO! Tanking Score!" );
            return -999;
        }
        else if( switchCandidate.SwitchDefensePTKOR.PTKO == PotentialToKO.OHKO )
        {
            score -= 70;
        }

        int offensiveDelta = switchCandidate.SwitchOffensePTKOR.Score - switchCandidate.SwitchDefensePTKOR.Score; //--should be offensive ptko score minus defensive ptko score.
        score += Mathf.Clamp( Mathf.FloorToInt( offensiveDelta * 0.5f ), 0, 40 );

        _ai.CurrentLog.Add( $"Offensive PTKOR Score: {switchCandidate.SwitchOffensePTKOR.Score}, Defensive PTKOR Score: {switchCandidate.SwitchDefensePTKOR.Score}, Delta: {offensiveDelta}." );

        BattleAI_PokemonAdapter candidateAdapter = _ai.GetPokemonAs_Adapter( switchCandidate.Pokemon );
        if( switchCandidate.Pokemon != null && _ai.OurTeamPieceValues.TryGetValue( candidateAdapter.Pokemon, out var pieceValue ) )
        {
            int switchThreatCount = pieceValue.ThreatCount;

            if( switchThreatCount == 2 )
                score += 10;
            else if( switchThreatCount >= 3 )
                score += 20;

            _ai.CurrentLog.Add( $"Threat Count: {switchThreatCount}. Score: {score}" );
        }

        bool switchThreatensKO          = switchCandidate.SwitchOffensePTKOR.PTKO >= PotentialToKO.Dangerous;
        bool switchIsThreatenedByKO     = switchCandidate.SwitchDefensePTKOR.PTKO >= PotentialToKO.Dangerous;
        bool switchDoesBigDamage        = switchCandidate.SwitchOffensePTKOR.PTKO >= PotentialToKO.TwoHKO;
        bool switchTakesBigDamage       = switchCandidate.SwitchDefensePTKOR.PTKO >= PotentialToKO.TwoHKO;
        bool switchMovesFirst           = switchCandidate.MovesFirst;

        if( !switchMovesFirst )
            score -= 10;

        if( ( switchThreatensKO || switchDoesBigDamage ) && switchMovesFirst && !switchIsThreatenedByKO )
            score += 75;
        else if( switchIsThreatenedByKO && !switchThreatensKO )
            score -= 45;

        if( !switchThreatensKO && !switchDoesBigDamage && ( switchTakesBigDamage || switchIsThreatenedByKO ) )
            score -= 45;

        _ai.CurrentLog.Add( $"SwitchThreatensKO {switchThreatensKO}, SwitchMovesFirst {switchMovesFirst}, !switchIsThreatenedByKO {!switchIsThreatenedByKO}. Score: {score}" );

        var defensePTKO = switchCandidate.SwitchDefensePTKOR.PTKO;
        float incomingDamage = _proj.Get_PTKODamagePercent( defensePTKO );

        if( incomingDamage >= 0.75f )
            score -= 75;
        else if( incomingDamage >= 0.5f )
            score -= 50;
        else if( incomingDamage >= 0.25f )
            score -= 25;

        _ai.CurrentLog.Add( $"Switch's DefensePTKO (opponent's potential to ko us): {defensePTKO}. Switch's Likely damage taken: {incomingDamage}. Score: {score}]=" );

        //--Tempo
        score += _ai.OffensiveSwitch_TempoModifier( tempo );

        //--Attacking is better penalty
        if( eval.AttackerPTKOR.PTKO == PotentialToKO.OHKO && eval.AttackerMovesFirst )
            score -= 150;
        else if( eval.AttackerPTKOR.PTKO == PotentialToKO.Dangerous && eval.AttackerMovesFirst )
            score -= 125;
        else if( eval.AttackerPTKOR.PTKO >= PotentialToKO.Dangerous && eval.AttackerSurvives )
            score -= 100;

        //--Switch Tax
        score += _ai.Get_ConsecutiveSwitchPenalty();
        score -= 15;

        _ai.CurrentLog.Add( $"=[Final Offensive Switch Score after tempo modifier, consecutive switch pentalty, and switch tax: {score}" );

        return score;
    }

//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================
//=========================================================================================[SETUP SCORE]============================================================================================================
//==================================================================================================================================================================================================================
//==================================================================================================================================================================================================================

    public int SetupScore( TempoStateResult tempo, ExchangeEvaluation eval, BoardContext context, SetupThreatResult setup, TurnOutcomeProjection intentTOP, ThreatIntentResult tir  )
    {
        int score = 0;

        var attackerName = eval.AttackerName;
        var targetName = eval.OpponentName;

        var myPTKO_AfterSetup = setup.AfterPTKOR;
        var theirPTKO = eval.OpponentPTKOR.PTKO;
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
        _ai.CurrentLog.Add( $"IntentTOP Information. Attacker: {intentTOP.Attacker.Name}, Opponent: {intentTOP.Opponent.Name}, Threat: tir.Threat.Name //not implemented yet" );

        //--These are much tighter/more risky because setting up can drastically change an outcome. defensive setup can swing ptko chances, while offensive setup can threaten KOs across entire teams, making your current hp potentially irrelevant
        if( theirIntentPTKO >= PotentialToKO.Dangerous && !eval.AttackerMovesFirst )
        {
            _ai.CurrentLog.Add( $"The intentTOP says we're likely to die if we setup now! Tanking Score!" );
            return -999;
        }

        if( theirIntentPTKO == PotentialToKO.OHKO )
        {
            _ai.CurrentLog.Add( $"The intentTOP says we're likely to die if we setup now! Tanking Score!" );
            return -999;
        }

        if( theirPTKO >= PotentialToKO.Dangerous && !eval.AttackerMovesFirst )
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
        if( eval.AttackerThreatensKO && theirPTKO < PotentialToKO.TwoHKO )
        {
            if( eval.AttackerMovesFirst )
                score -= 60;
            else
                score -= 30;
        }

        //--If we are likely to KO next turn
        if( myPTKO_AfterSetup.PTKO >= PotentialToKO.Dangerous && eval.AttackerMovesFirst )
            score += 30;
        else if( myPTKO_AfterSetup.PTKO >= PotentialToKO.Risky )
            score += 20;
        else if( myPTKO_AfterSetup.PTKO <= PotentialToKO.Risky && !eval.AttackerMovesFirst )
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
        float switchProb = eval.OpponentSwitchProbability;

        float dangerWeight =
            theirPTKO >= PotentialToKO.OHKO ? 1.25f :
            theirPTKO >= PotentialToKO.Dangerous ? 1.0f :
            theirPTKO >= PotentialToKO.Risky ? 0.75f :
            theirPTKO >= PotentialToKO.TwoHKO ? 0.5f : 0.25f;

        if( eval.OpponentMovesFirst )
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

    public int OffensiveStatusScore( TempoStateResult tempo, ExchangeEvaluation eval, BoardContext context, StatusThreatResult status, TurnOutcomeProjection intentTOP, ThreatIntentResult tir  )
    {
        int score = 0;

        var attackerName = eval.AttackerName;
        var targetName = eval.OpponentName;

        var ourPTKO = status.AttackerPTKOR;
        var theirPTKO = eval.OpponentPTKOR;

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

        if( theirPTKO.PTKO >= PotentialToKO.Dangerous && !eval.AttackerMovesFirst )
        {
            _ai.CurrentLog.Add( $"We're likely to die with no progress from it if we use an offensive status move now!" );
            score -= 80;
        }
        else if( theirPTKO.PTKO >= PotentialToKO.Risky && !eval.AttackerMovesFirst )
        {
            _ai.CurrentLog.Add( $"We're likely to die with no progress from it if we use an offensive status move now!" );
            score -= 70;
        }

        //--Base value
        _ai.CurrentLog.Add( $"===[Beginning Offensive Status Scoring for {attackerName} ({moveName}) vs {targetName}. Tempo: {tempo.TempoState}, My PTKO Them: {ourPTKO.PTKO}, their PTKO on me: (eval){theirPTKO.PTKO} (intent){theirIntentPTKO}]===" );

        if( status.StatusType == OffensiveStatusType.EntryHazard )
        {
            score += status.TeamCoverage
                - Mathf.FloorToInt( status.ImmediateImpact * 0.5f )
                + Mathf.FloorToInt( status.BoardAmbiguity * 0.5f )
                + Mathf.FloorToInt( status.Reliability * 0.4f );

            _ai.CurrentLog.Add( $"Entry Hazard detected! Team Coverage: {status.TeamCoverage}, Impact (50%): {Mathf.FloorToInt( status.ImmediateImpact * 0.5f )}, Ambiguity (50%): {status.BoardAmbiguity}. Base Score: {score}" );

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

        if( status.StatusType == OffensiveStatusType.StatusEffect || status.StatusType == OffensiveStatusType.StatDebuff )
        {
            score += status.ImmediateImpact
                - Mathf.FloorToInt( status.TeamCoverage * 0.5f )
                + Mathf.FloorToInt( status.Reliability * 0.5f );

            _ai.CurrentLog.Add( $"Status Effect or Status Debuff detected! Impact: {status.ImmediateImpact}, Coverage (50%) {Mathf.FloorToInt( status.TeamCoverage * 0.5f )}, Reliability (50%): {Mathf.FloorToInt( status.Reliability * 0.5f )}. Base Score: {score}" );

            //--Disruption bonus
            if( !status.Top.OpponentCanAct )
                score += 60;

            _ai.CurrentLog.Add( $"Opponent Can Act: {status.Top.OpponentCanAct}. Score: {score}" );

            int attackEquivalent = eval.AttackerPTKOR.Score - eval.OpponentPTKOR.Score;
            if( status.ImmediateImpact < attackEquivalent )
                score -= 40;

            _ai.CurrentLog.Add( $"Does attack equivalent ({attackEquivalent}) outweigh immediate impact ({status.ImmediateImpact})?. Score: {score}" );

            //--Bonus for punishing a switch with a status effect move like sleep powder or thunder wave
            score += Mathf.FloorToInt( 25f * eval.OpponentSwitchProbability );
        }

        if( status.StatusType == OffensiveStatusType.Disruption )
        {
            score += status.ImmediateImpact
                + Mathf.FloorToInt( status.Reliability * 0.6f )
                + Mathf.FloorToInt( status.BoardAmbiguity * 0.5f )
                - Mathf.FloorToInt( status.TeamCoverage * 0.25f );

            _ai.CurrentLog.Add( $"Disruption detected! Impact: {status.ImmediateImpact}, Reliability (60%): {Mathf.FloorToInt( status.Reliability * 0.6f )}, Ambiguity (50%): {Mathf.FloorToInt( status.BoardAmbiguity * 0.5f )}, Coverage (25%): {Mathf.FloorToInt( status.TeamCoverage * 0.25f )}. Score: {score}" );

            if( !status.Top.OpponentCanAct )
                score += 60;

            int attackEquivalent = eval.AttackerPTKOR.Score - eval.OpponentPTKOR.Score;

            if( status.ImmediateImpact < attackEquivalent )
                score -= 30;

            score += Mathf.FloorToInt( 20f * eval.OpponentSwitchProbability );
        }

        if( status.StatusType == OffensiveStatusType.Phaze )
        {
            score += status.ImmediateImpact
                + status.TeamCoverage
                + Mathf.FloorToInt( status.BoardAmbiguity * 0.5f )
                + Mathf.FloorToInt( status.Reliability * 0.3f );

            _ai.CurrentLog.Add( $"Phazing detected! Impact: {status.ImmediateImpact}, Coverage: {status.TeamCoverage}, Ambiguity (50%): {Mathf.FloorToInt( status.BoardAmbiguity * 0.5f )}, Reliability (30%): {Mathf.FloorToInt( status.Reliability * 0.3f )}. Score: {score}" );

            if( eval.OpponentSwitchProbability < 0.4f )
                score += 20;

            if( status.Top.OpponentCanAct )
                score -= 15;
        }

        if( eval.ExchangeState == ExchangeState.Pressure )
            score += 15;

        float switchProb = eval.OpponentSwitchProbability;

        float dangerWeight =
            theirPTKO.PTKO >= PotentialToKO.OHKO ? 1.25f :
            theirPTKO.PTKO >= PotentialToKO.Dangerous ? 1.0f :
            theirPTKO.PTKO >= PotentialToKO.Risky ? 0.75f :
            theirPTKO.PTKO >= PotentialToKO.TwoHKO ? 0.5f : 0.25f;

        if( eval.OpponentMovesFirst )
            dangerWeight *= 1.5f;

        float penalty = 50f * dangerWeight;

        score += Mathf.FloorToInt( switchProb * 75f );
        score -= Mathf.FloorToInt( ( 1f - switchProb ) * penalty );

        _ai.CurrentLog.Add( $"Opponent Switch Probability: {switchProb}. Score: {score}" );

        //--Don’t overuse if attack is better
        if( eval.AttackerThreatensKO )
        {
            if( eval.AttackerMovesFirst )
                score -= 80;
            else
                score -= 40;
        }

        _ai.CurrentLog.Add( $"Checked if attacking may be better. Attacker Threatens KO: {eval.AttackerThreatensKO}. Attacker Moves First: {eval.AttackerMovesFirst} Score: {score}" );

        //--Status for survival incentive
        if( eval.OpponentThreatensKO )
        {
            if( eval.AttackerMovesFirst && ( status.StatusType == OffensiveStatusType.StatusEffect || status.StatusType == OffensiveStatusType.Disruption || status.StatusType == OffensiveStatusType.Phaze ) )
                score += 25;
            else if( status.Top.OpponentCanAct )
                score -= 150;
        }
        else if( eval.OpponentPTKOR.PTKO >= PotentialToKO.Risky )
            score -= 75;

        _ai.CurrentLog.Add( $"Checked Survival. Opponent Threatens KO: {eval.OpponentThreatensKO}. Opponent acts in simulation: {status.Top.OpponentCanAct} Score: {score}" );

        //--HP context
        float hp = eval.AttackerHPR;

        if( hp <= 0.25f )
            score -= 30;
        else if( hp <= 0.45f && eval.OpponentThreatensKO )
            score -= 20;
        else if( hp >= 0.7f && !eval.OpponentThreatensKO )
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


}
