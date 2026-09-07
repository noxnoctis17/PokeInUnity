using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class BattleAI_ThreatIntent
{
    private readonly BattleAI _ai;
    private CustomLogSession _tirLog;
    // private CustomLogSession _switchPredLog;

    public BattleAI_ThreatIntent( BattleAI ai )
    {
        _ai = ai;
    }

    public ThreatBrain ReadThreatBrain( IBattleAIUnit threat, IBattleAIUnit us )
    {
        us ??= _ai.CurrentUnitAdapter;

        var theirEE = _ai.Projection.EvaluateExchange( threat, us );
        var theirBC = _ai.Projection.GetBoardContext( threat, us, theirEE );
        var ourTP = _ai.GetThreatProfile( theirEE, theirBC, us, true ); //--this is our profile as a threat to them, from their perspective
        var theirGP = _ai.Blackboard.GetOpponentGamePlan( _ai.Blackboard.GamePlan );
        var theirCP = _ai.Projection.EvaluateCurrentPlan( theirEE, theirBC, ourTP, theirGP, _ai.Blackboard.TheirCurrentPlan, true );

        _ai.Blackboard.SetEnemyCurrentPlan( theirCP );

        return new()
        {
            TheirEE = theirEE,
            TheirBC = theirBC,
            OurTP = ourTP,
            TheirGP = theirGP,
            TheirCP = theirCP,
        };
    }

    public ThreatIntentCandidates GetThreatCandidates( IBattleAIUnit threat, IBattleAIUnit us, ThreatBrain tb )
    {
        var defensiveSwitchCandidates =_ai.GetLikely_DefensiveSwitches( threat, 0, true );
        var offensiveSwitchCandidates = _ai.GetLikely_OffensiveSwitches( threat, 0, true );

        bool defensiveSwitches = defensiveSwitchCandidates != null && defensiveSwitchCandidates.Count > 0;
        bool offensiveSwitches = offensiveSwitchCandidates != null && offensiveSwitchCandidates.Count > 0;

        bool aiBattle = _ai.BattleSystem.BattleType == BattleType.AI_Singles || _ai.BattleSystem.BattleType == BattleType.AI_Doubles;

        SwitchCandidateResult defSCR = new();
        SwitchCandidateResult offSCR = new();

        SwitchPredictionResult spr = new()
        {
            DefensiveSwitchesAvailable = defensiveSwitches,
            OffensiveSwitchesAvailable = offensiveSwitches,
            NoSwitchCandidatesAvailable = !defensiveSwitches && !offensiveSwitches,

            DefensiveCandidates = defensiveSwitchCandidates,
            OffensiveCandidates = offensiveSwitchCandidates,

            DefensiveScores = new(),
            OffensiveScores = new(),
        };

        // _switchPredLog = new();

        // _switchPredLog.Add( $"" );
        // _switchPredLog.Add( $"====================================================" );
        // _switchPredLog.Add( $"=====[Predicting Most Likely Switch Candidates]=====" );
        // _switchPredLog.Add( $"====================================================" );
        // _switchPredLog.Add( $"" );

        if( defensiveSwitches )
        {
            if( aiBattle )
                defSCR = defensiveSwitchCandidates.First().Value;
            else
            {
                var likely = SelectLikely_DefensiveSwitch( defensiveSwitchCandidates.Values.ToList(), ref spr, tb, threat );
                var mon = likely.PrimaryDefensive.Candidate;
                
                if( defensiveSwitchCandidates.TryGetValue( mon, out var scr ) )
                    defSCR = scr;
                else
                    defSCR = defensiveSwitchCandidates.First().Value;
            }
        }

        if( offensiveSwitches )
        {
            if( aiBattle )
                offSCR = offensiveSwitchCandidates.First().Value;
            else
            {
                var likely = SelectLikely_OffensiveSwitch( offensiveSwitchCandidates.Values.ToList(), ref spr, tb, threat );
                var mon = likely.PrimaryOffensive.Candidate;

                if( offensiveSwitchCandidates.TryGetValue( mon, out var scr ) )
                    offSCR = scr;
                else
                    offSCR = offensiveSwitchCandidates.First().Value;
            }
        }

        // Debug.Log( _switchPredLog.ToString() );
        // string path = Application.persistentDataPath + "/SwitchPrediction_Log.txt";
        // System.IO.File.AppendAllText( path, _switchPredLog.ToString() + "\n" + "\n" + "\n" + "\n" + "\n" );
        // _switchPredLog.Clear();

        ThreatIntentCandidates tic = new()
        {
            Threat = threat,
            MoveThreatResult = _ai.CandidateSelect.GetMove_BestAttack( threat, us ),
            DefensiveSwitchCandidateResult = defSCR,
            OffensiveSwitchCandidateResult = offSCR,
            SetupThreatResult = _ai.CandidateSelect.GetMove_Setup( threat, us ),
            OffensiveStatusThreatResult = _ai.CandidateSelect.GetMove_OffensiveStatus( threat, us ),
            SupportiveStatusThreatResult = _ai.CandidateSelect.GetMove_SupportiveStatus( threat, us ),
            // ProtectThreatResult
        };

        return tic;
    }

    private SwitchPredictionResult SelectLikely_DefensiveSwitch( List<SwitchCandidateResult> candidates, ref SwitchPredictionResult spr, ThreatBrain tb, IBattleAIUnit threat )
    {
        // _switchPredLog.Add( $"" );
        // _switchPredLog.Add( $"=========================================" );
        // _switchPredLog.Add( $"===[Selecting Likely Defensive Switch]===" );
        // _switchPredLog.Add( $"=========================================" );
        // _switchPredLog.Add( $"" );

        foreach( var cand in candidates )
        {
            int safety = 0;
            int pressure = 0;
            int resource = 0;
            int plan = 0;

            var top = cand.Top;
            var candidateAdapter = _ai.GetPokemonAs_Adapter( cand.Pokemon );
            // _switchPredLog.Add( $"=[{cand.Pokemon.NickName}]=" );

            //--Safety
            //--Candidate survives comfortably
            if( top.OpponentPTKO <= PotentialToKO.Safe )
            {
                safety += 2;
                // _switchPredLog.Add( $"Candidate survives comfortably. Safety: {safety}" );
            }
            //--Candidate survives but isn't amazing
            else if( top.OpponentPTKO < PotentialToKO.Risky )
            {
                safety += 1;
                // _switchPredLog.Add( $"Candidate survives somewhat. Safety: {safety}" );
            }
            //--Candidate still gets blown up
            else if( top.OpponentPTKO == PotentialToKO.Dangerous )
            {
                safety -= 2;
                // _switchPredLog.Add( $"Candidate takes huge damage. Safety: {safety}" );
            }
            //--OHKO
            else if( top.OpponentPTKO == PotentialToKO.OHKO )
            {
                safety -= 3;
                // _switchPredLog.Add( $"Candidate dies. Safety: {safety}" );
            }

            //--Pressure
            //--Candidate threatens current opponent
            if( top.AttackerPTKO <= PotentialToKO.Safe )
            {
                pressure -= 1;
                // _switchPredLog.Add( $"Candidate does not threaten us. Pressure: {pressure}" );
            }
            //--Threatens immediate KO
            else if( top.AttackerPTKO >= PotentialToKO.Dangerous )
            {
                pressure += 2;
                // _switchPredLog.Add( $"Candidate really threatens us. Pressure: {pressure}" );
            }
            //--Can enter safely but does nothing
            else if( top.AttackerPTKO >= PotentialToKO.TwoHKO )
            {
                pressure += 1;
                // _switchPredLog.Add( $"Candidate kinda threatens us. Pressure: {pressure}" );
            }

            //--Resource
            //--Low expendability
            if( threat.Expendability <= 0.3f && candidateAdapter.Expendability >= 0.3f )
            {
                resource += 1;
                // _switchPredLog.Add( $"Their current unit is not very expendible, and their tank is more expendible. Resource: {resource}" );
            }

            //--Hazard removal
            var theirCourt = candidateAdapter.CourtLocation == CourtLocation.TopCourt ? _ai.Blackboard.CurrentFieldSnapshot.TopCourtConditions : _ai.Blackboard.CurrentFieldSnapshot.BottomCourtConditions;
            bool hazardsExist = theirCourt.ContainsKey( CourtConditionID.LeechSeed ) || theirCourt.ContainsKey( CourtConditionID.Spikes ) || theirCourt.ContainsKey( CourtConditionID.StealthRock ) || theirCourt.ContainsKey( CourtConditionID.StickyWeb ) || theirCourt.ContainsKey( CourtConditionID.ToxicSpikes );
            bool weAreHazardSetter = _ai.CurrentUnitAdapter.RoleProfile.Traits.Contains( RoleTrait.HazardSetter );

            if( candidateAdapter.RoleProfile.Traits.Contains( RoleTrait.HazardRemover ) && ( hazardsExist || weAreHazardSetter ) )
            {
                resource += 1;
                // _switchPredLog.Add( $"Their candidate is a hazard remover and we have either set hazards on their side or we are a hazard setter. Resource: {resource}" );
            }

            //--Weather setter
            bool switchSetsWeather = _ai.UnitSim.PokemonHasWeatherSetter_Ability( cand.Pokemon );
            bool switchChangesWeather = false;
            WeatherConditionID candidatesWeather = WeatherConditionID.None;

            if( switchSetsWeather )
            {
                switch( cand.Pokemon.AbilityID )
                {
                    case AbilityID.Drought: candidatesWeather = WeatherConditionID.Sun; break;
                    case AbilityID.Drizzle: candidatesWeather = WeatherConditionID.Rain; break;
                    case AbilityID.Sandstream: candidatesWeather = WeatherConditionID.Sand; break;
                    case AbilityID.SnowWarning: candidatesWeather = WeatherConditionID.Snow; break;
                }

                if( candidatesWeather != WeatherConditionID.None && candidatesWeather != _ai.Blackboard.CurrentFieldSnapshot.Weather )
                    switchChangesWeather = true;
            }

            if( switchChangesWeather )
            {
                resource += 1;
                // _switchPredLog.Add( $"Their candidate changes the weather likely in their favor. Resource: {resource}" );
            }

            //--Future FocusMon support
            var cp = tb.TheirCP;
            if( cand.Pokemon == cp.FocusMon )
            {
                resource += 2;
                // _switchPredLog.Add( $"Their candidate is their current plan's FocusMon. Resource: {resource}" );
            }

            //--Plan
            //--Wall switching into threat
            var ourPR = _ai.CurrentUnitAdapter.RoleProfile.PrimaryRole;
            bool weAreOffensive = ourPR == RoleClass.BulkyAttacker || ourPR == RoleClass.RevengeKiller || ourPR == RoleClass.SetupSweeper || ourPR == RoleClass.Sweeper || ourPR == RoleClass.TrickRoomAbuser;
            if( candidateAdapter.RoleProfile.PrimaryRole == RoleClass.Wall && weAreOffensive )
            {
                plan += 1;
                // _switchPredLog.Add( $"Their candidate is a wall and we are an offensive unit. Plan: {plan}" );

                if( ourPR == RoleClass.WallBreaker )
                {
                    plan -= 1;
                    // _switchPredLog.Add( $"However, we are a wallbreaker. Nullifying previous reward. Plan: {plan}" );
                }
            }

            //--Pivot
            if( candidateAdapter.RoleProfile.PrimaryRole == RoleClass.Pivot )
            {
                plan += 1;
                // _switchPredLog.Add( $"Their candidate is a pivot. Plan: {plan}" );
            }

            //--Weather Abuser
            bool benefitsCurrentWeather = _ai.UnitSim.Get_WeatherContextScore( cand.Pokemon ) > 0;
            if( benefitsCurrentWeather )
            {
                plan += 1;
                // _switchPredLog.Add( $"Their candidate benefits from the current weather. Plan: {plan}" );

                WeatherConditionID threatsWeather = WeatherConditionID.None;

                switch( threat.Ability )
                {
                    case AbilityID.Drought: threatsWeather = WeatherConditionID.Sun; break;
                    case AbilityID.Drizzle: threatsWeather = WeatherConditionID.Rain; break;
                    case AbilityID.Sandstream: threatsWeather = WeatherConditionID.Sand; break;
                    case AbilityID.SnowWarning: threatsWeather = WeatherConditionID.Snow; break;
                }

                if( threatsWeather != WeatherConditionID.None && threatsWeather == _ai.Blackboard.CurrentFieldSnapshot.Weather )
                {
                    plan += 1;
                    // _switchPredLog.Add( $"Their current pokemon likely set the weather their candidate benefits from. Plan: {plan}" );
                }
            }

            //--Final Score
            float tieBreaker = ( safety * 0.01f ) + ( plan * 0.01f );
            float final = safety + pressure + resource + plan;
            final += tieBreaker;

            // _switchPredLog.Add( $"" );
            // _switchPredLog.Add( $"Safety: {safety}" );
            // _switchPredLog.Add( $"Pressure: {pressure}" );
            // _switchPredLog.Add( $"Resource: {resource}" );
            // _switchPredLog.Add( $"Plan: {plan}" );
            // _switchPredLog.Add( $"Tie Breaker Value: {tieBreaker}. Final Score: {final}" );
            // _switchPredLog.Add( $"" );

            SwitchPredictionScore sps = new()
            {
                Candidate = cand.Pokemon,
                Score = final,

                SafetyScore = safety,
                PressureScore = pressure,
                ResourceScore = resource,
                PlanScore = plan,
            };

            spr.DefensiveScores.Add( sps );
        }
        
        var sorted = spr.DefensiveScores.OrderByDescending( s => s.Score ).ToList();
        spr.DefensiveScores = sorted;

        spr.PrimaryDefensive = sorted.First();

        if( sorted.Count > 1 )
            spr.SecondaryDefensive = sorted.Skip( 1 ).First();
        else
            spr.SecondaryDefensive = sorted.First();;

        float best = sorted[0].Score;
        float second = sorted.Count > 1 ? sorted[1].Score : best;

        spr.DefensiveConfidence = Mathf.Clamp01( (float)Mathf.Max( 1, best ) / ( best + second ) );

        // _switchPredLog.Add( $"Primary Defensive Candidate: {spr.PrimaryDefensive.Candidate.NickName}" );
        // _switchPredLog.Add( $"Secondary Defensive Candidate: {spr.SecondaryDefensive.Candidate.NickName}" );
        // _switchPredLog.Add( $"Confidence: {spr.DefensiveConfidence}" );
        // _switchPredLog.Add( $"" );

        return spr;
    }

    private SwitchPredictionResult SelectLikely_OffensiveSwitch( List<SwitchCandidateResult> candidates, ref SwitchPredictionResult spr, ThreatBrain tb, IBattleAIUnit threat )
    {
        // _switchPredLog.Add( $"" );
        // _switchPredLog.Add( $"=========================================" );
        // _switchPredLog.Add( $"===[Selecting Likely Offensive Switch]===" );
        // _switchPredLog.Add( $"=========================================" );
        // _switchPredLog.Add( $"" );

        foreach( var cand in candidates )
        {
            int safety = 0;
            int pressure = 0;
            int resource = 0;
            int plan = 0;

            var top = cand.Top;
            var candidateAdapter = _ai.GetPokemonAs_Adapter( cand.Pokemon );

            // _switchPredLog.Add( $"=[{cand.Pokemon.NickName}]=" );

            //--Safety
            //--Candidate survives comfortably
            if( top.OpponentPTKO <= PotentialToKO.Safe )
            {
                safety += 2;
                // _switchPredLog.Add( $"Their candidate survives comfortably. Safety: {safety}" );
            }
            //--Candidate survives but isn't amazing
            else if( top.OpponentPTKO < PotentialToKO.Risky )
            {
                safety += 1;
                // _switchPredLog.Add( $"Their candidate barely survives. Safety: {safety}" );
            }
            //--Candidate still gets blown up
            else if( top.OpponentPTKO == PotentialToKO.Dangerous )
            {
                safety -= 2;
                // _switchPredLog.Add( $"Their candidate takes huge damage. Safety: {safety}" );
            }
            //--OHKO
            else if( top.OpponentPTKO == PotentialToKO.OHKO )
            {
                safety -= 3;
                // _switchPredLog.Add( $"Their candidate dies. Safety: {safety}" );
            }

            //--Pressure
            //--Threatens Immediate KO
            if( top.AttackerPTKO >= PotentialToKO.Dangerous)
            {
                pressure += 2;
                // _switchPredLog.Add( $"Their candidate threatens an immediate KO on us. Pressure: {pressure}" );
            }
            else if( top.AttackerPTKO >= PotentialToKO.TwoHKO )
            {
                pressure += 1;
                // _switchPredLog.Add( $"Their candidate threatens reasonable damage on us. Pressure: {pressure}" );
            }

            if( top.Attacker.Speed > top.Opponent.Speed )
            {
                pressure += 1;
                // _switchPredLog.Add( $"Their candidate outspeeds our current pokemon. Pressure: {pressure}" );
            }

            if( candidateAdapter.RoleProfile.PrimaryRole == RoleClass.RevengeKiller || candidateAdapter.RoleProfile.PrimaryRole == RoleClass.Pivot || candidateAdapter.RoleProfile.PrimaryRole == RoleClass.WallBreaker )
            {
                pressure += 1;
                // _switchPredLog.Add( $"Their candidate is a tempo-based offensive unit. Pressure: {pressure}" );
            }

            if( top.AttackerPTKO >= PotentialToKO.Risky && top.OpponentPTKO <= PotentialToKO.Safe )
            {
                pressure += 2;
                // _switchPredLog.Add( $"Their candidate has offensive and defensive control over the board. Pressure: {pressure}" );
            }
            else if( top.AttackerPTKO > top.OpponentPTKO )
            {
                pressure += 2;
                // _switchPredLog.Add( $"Their candidate has an offensive advantage over us. Pressure: {pressure}" );
            }

            //--Resource
            if( cand.Pokemon == tb.TheirCP.FocusMon )
            {
                resource += 2;
                // _switchPredLog.Add( $"Their candidate is their current plan's FocusMon. Resource: {resource}" );
            }

            float switchThreatCount = _ai.Blackboard.TheirTeamPieceValues.TryGetValue( top.Attacker.Pokemon, out var pieceValue ) ? pieceValue.ThreatCount : 0;
            float threatPercentage = switchThreatCount / _ai.Blackboard.TheirTeamAdapters.Count; //--this needs to be changed to be active threat count / remaining, not the team preview/full team percentage.
            if( threatPercentage >= 0.5f )
            {
                resource += 2;
                // _switchPredLog.Add( $"Their candidate threatens at least half of our team. Resource: {resource}" );
            }
            else if( threatPercentage >= 0.25f )
            {
                resource += 1;
                // _switchPredLog.Add( $"Their candidate threantes at least a quarter of our team. Resource: {resource}" );
            }

            if( candidateAdapter.Expendability >= 0.6f )
            {
                resource += 1;
                // _switchPredLog.Add( $"Their candidate is pretty expendible. Resource: {resource}" );
            }

            //--Plan
            if( tb.TheirCP.Type == PlanType.EnableSweep )
            {
                if( cand.Pokemon == tb.TheirCP.FocusMon )
                {
                    plan += 1;
                    // _switchPredLog.Add( $"They are looking to enable a sweep and their candidate is their current plan's FocusMon. Plan: {plan}" );
                }
            }

            bool gainsControl = _ai.CurrentUnitAdapter.Speed > threat.Speed && top.Attacker.Speed > top.Opponent.Speed;
            if( gainsControl )
            {
                plan += 1;
                // _switchPredLog.Add( $"Their candidate gains the speed advantage over us. Plan: {plan}" );
            }
            
            //--Weather Abuser
            bool benefitsCurrentWeather = _ai.UnitSim.Get_WeatherContextScore( cand.Pokemon ) > 0;
            if( benefitsCurrentWeather )
            {
                plan += 2;
                // _switchPredLog.Add( $"Their candidate benefits from the current weather. Plan: {plan}" );

                WeatherConditionID threatsWeather = WeatherConditionID.None;

                switch( threat.Ability )
                {
                    case AbilityID.Drought: threatsWeather = WeatherConditionID.Sun; break;
                    case AbilityID.Drizzle: threatsWeather = WeatherConditionID.Rain; break;
                    case AbilityID.Sandstream: threatsWeather = WeatherConditionID.Sand; break;
                    case AbilityID.SnowWarning: threatsWeather = WeatherConditionID.Snow; break;
                }

                if( threatsWeather != WeatherConditionID.None && threatsWeather == _ai.Blackboard.CurrentFieldSnapshot.Weather )
                {
                    plan += 1;
                    // _switchPredLog.Add( $"Their current pokemon likely set the weather the candidate benefits from. Plan: {plan}" );
                }
            }

            //--Final Score
            float tieBreaker = ( safety * 0.01f ) + ( plan * 0.01f );
            float final = safety + pressure + resource + plan;
            final += tieBreaker;

            // _switchPredLog.Add( $"" );
            // _switchPredLog.Add( $"Safety: {safety}" );
            // _switchPredLog.Add( $"Pressure: {pressure}" );
            // _switchPredLog.Add( $"Resource: {resource}" );
            // _switchPredLog.Add( $"Plan: {plan}" );
            // _switchPredLog.Add( $"Tie Breaker Value: {tieBreaker}. Final Score: {final}" );
            // _switchPredLog.Add( $"" );

            SwitchPredictionScore sps = new()
            {
                Candidate = cand.Pokemon,
                Score = final,

                SafetyScore = safety,
                PressureScore = pressure,
                ResourceScore = resource,
                PlanScore = plan,
            };

            spr.OffensiveScores.Add( sps );
        }

        var sorted = spr.OffensiveScores.OrderByDescending( s => s.Score ).ToList();
        spr.OffensiveScores = sorted;

        spr.PrimaryOffensive = sorted.First();

        if( sorted.Count > 1 )
            spr.SecondaryOffensive = sorted.Skip( 1 ).First();

        float best = sorted[0].Score;
        float second = sorted.Count > 1 ? sorted[1].Score : 0;

        spr.OffensiveConfidence = Mathf.Clamp01( ( best - second + 5f ) / 10f );
        spr.OffensiveConfidence = Mathf.Clamp01( (float)Mathf.Max( 1, best ) / ( best + second ) );

        // _switchPredLog.Add( $"Primary Offensive Candidate: {spr.PrimaryOffensive.Candidate?.NickName}" );
        // _switchPredLog.Add( $"Secondary Offensive Candidate: {spr.SecondaryOffensive.Candidate?.NickName}" );
        // _switchPredLog.Add( $"Confidence: {spr.OffensiveConfidence}" );
        // _switchPredLog.Add( $"" );

        return spr;
    }

    public ThreatIntentResult GetThreatIntentResult( ThreatIntentCandidates tic, ThreatBrain tb )
    {
        ThreatIntentResult tir = new()
        {
            Threat = tic.Threat,
        };

        _tirLog = new();

        _tirLog.Add( $"" );
        _tirLog.Add( $"================================" );
        _tirLog.Add( $"=====[Threat Intent Result]=====" );
        _tirLog.Add( $"================================" );
        _tirLog.Add( $"" );

        int attack_evidence = 0;
        int defSwitch_evidence = 0;
        int offSwitch_evidence = 0;
        int setup_Evidence = 0;
        int offStatus_Evidence = 0;
        int suppStatus_Evidence = 0;

        var theirEE = tb.TheirEE;
        var theirBC = tb.TheirBC;
        var theirBFS = theirBC.BattlefieldState;
        var ourTP =  tb.OurTP; //--this is our profile as a threat to them, from their perspective
        var theirGP = tb.TheirGP;
        var theirCP = tb.TheirCP;

        var ourEE = _ai.Projection.EvaluateExchange( _ai.CurrentUnitAdapter, tic.Threat );
        var ourBC = _ai.Projection.GetBoardContext( _ai.CurrentUnitAdapter, tic.Threat, ourEE );
        var theirTP = _ai.GetThreatProfile( ourEE, ourBC, tic.Threat ); //--this is their profile as a threat to us, from our perspective

        attack_evidence = AttackEvidence( ref tic, theirEE, theirBFS, theirTP, ourTP, theirCP );
        defSwitch_evidence = DefensiveSwitchEvidence( tic, theirEE, theirBFS, theirTP, ourTP, theirCP );
        offSwitch_evidence = OffensiveSwitchEvidence( tic, theirEE, theirBFS, theirTP, ourTP, theirCP );
        setup_Evidence = SetupEvidence( tic, theirEE, theirBFS, theirTP, ourTP, theirCP );
        offStatus_Evidence = OffensiveStatusEvidence( tic, theirEE, theirBFS, theirTP, ourTP, theirCP );
        suppStatus_Evidence = SupportiveStatusEvidence( tic, theirEE, theirBFS, theirTP, ourTP, theirCP );

        Dictionary<ActionType, int> evidenceGathered = new()
        {
            { ActionType.Attack, attack_evidence }, //--There's always an attack. Eventually this will include struggle.
        };

        if( defSwitch_evidence != -99 )
            evidenceGathered.Add( ActionType.DefensiveSwitch, defSwitch_evidence );

        if( offSwitch_evidence != -99 )
            evidenceGathered.Add( ActionType.OffensiveSwitch, offSwitch_evidence );

        if( setup_Evidence != -99 )
            evidenceGathered.Add( ActionType.Setup, setup_Evidence );

        if( offStatus_Evidence != -99 )
            evidenceGathered.Add( ActionType.OffensiveStatus, offStatus_Evidence );

        if( suppStatus_Evidence != -99 )
            evidenceGathered.Add( ActionType.SupportiveStatus, suppStatus_Evidence );

        // evidenceGathered = evidenceGathered.OrderByDescending( kvp => kvp.Value ).ToDictionary( kvp => kvp.Key, kvp => kvp.Value );
        var sorted = evidenceGathered.OrderByDescending( kvp => kvp.Value ).ToList();
        int activeIntents = sorted.Count( kvp => kvp.Value > 0 );

        float totalEvidence = 0f;
        float entropy = 0f;

        _tirLog.Add( $"===[Final Evidence Values]===" );
        foreach( var kvp in sorted )
        {
            totalEvidence += kvp.Value;
            _tirLog.Add( $"{kvp.Key}: {kvp.Value}" );
        }

        foreach( var kvp in sorted )
        {
            if( kvp.Value <= 0 )
                continue;

            float v = kvp.Value;
            float p = v / totalEvidence;
            entropy -= p * Mathf.Log( p, 2f );
        }

        float confidence = 0f;
        if( activeIntents <= 1 )
        {
            confidence = 1f;
        }
        else
        {
            entropy /= Mathf.Log( activeIntents, 2f );
            confidence = Mathf.Clamp01( 1f - entropy );
        }

        _tirLog.Add( $"" );

        var primaryIntent = sorted[0].Key;
        var primaryEvidence = sorted[0].Value;

        ActionType secondaryIntent = ActionType.Any;
        int secondaryEvidence = -99;

        if( sorted.Count > 1 )
        {
            secondaryIntent = sorted[1].Key;
            secondaryEvidence = sorted[1].Value;
        }

        tir.PrimaryIntent = new(){ ActionType = primaryIntent, Evidence = primaryEvidence };
        tir.SecondaryIntent = new(){ ActionType = secondaryIntent, Evidence = secondaryEvidence };

        AssignIntentResult( ref tir.PrimaryIntent );
        AssignIntentResult( ref tir.SecondaryIntent );

        tir.TotalEvidence = (int)totalEvidence;
        tir.IntentEvidence = sorted.ToDictionary( kvp => kvp.Key, kvp => kvp.Value );

        tir.Confidence = confidence;

        if( tir.Confidence < 0.5f )
            tir.CheckSecondaryIntent = true;
        else
            tir.CheckSecondaryIntent = false; 

        _tirLog.Add( $"Best Evidence: {primaryIntent}, {primaryEvidence}" );
        _tirLog.Add( $"Second Evidence: {secondaryIntent}, {secondaryEvidence}" );
        _tirLog.Add( $"Confidence: {tir.Confidence}" );

        _tirLog.Add( $"" );
        _tirLog.Add( $"Actual Prediction for Threat {tic.Threat.Name}: {primaryIntent}" );

        void AssignIntentResult( ref Intent intent )
        {
            switch( intent.ActionType )
            {
                case ActionType.Attack:
                    intent.IntentResult = tic.MoveThreatResult;
                    var attackCand = (MoveThreatResult)intent.IntentResult;
                    _tirLog.Add( $"With move: {attackCand.Move.MoveSO.Name} (Attacker: {attackCand.Top.Attacker?.Name}, Opponent: {attackCand.Top.Opponent?.Name}, Attacker Ally: {attackCand.Top.AttackerAlly?.Name}, Opponent Ally: {attackCand.Top.OpponentAlly?.Name})" );
                break;

                case ActionType.DefensiveSwitch:
                    intent.IntentResult = tic.DefensiveSwitchCandidateResult;
                    var defCand = (SwitchCandidateResult)intent.IntentResult;
                    _tirLog.Add( $"With defensive switch candidate: {defCand.Pokemon.NickName} (Attacker: {defCand.Top.Attacker?.Name}, Opponent: {defCand.Top.Opponent?.Name}, Attacker Ally: {defCand.Top.AttackerAlly?.Name}, Opponent Ally: {defCand.Top.OpponentAlly?.Name})" );
                break;

                case ActionType.OffensiveSwitch:
                    intent.IntentResult = tic.OffensiveSwitchCandidateResult;
                    var offCand = (SwitchCandidateResult)intent.IntentResult;
                    _tirLog.Add( $"With offensive switch candidate: {offCand.Pokemon.NickName} (Attacker: {offCand.Top.Attacker?.Name}, Opponent: {offCand.Top.Opponent?.Name}, Attacker Ally: {offCand.Top.AttackerAlly?.Name}, Opponent Ally: {offCand.Top.OpponentAlly?.Name})" );
                break;

                case ActionType.Setup:
                    intent.IntentResult = tic.SetupThreatResult;
                    var setupCand = (SetupThreatResult)intent.IntentResult;
                    _tirLog.Add( $"With move: {setupCand.Move.MoveSO.Name} (Attacker: {setupCand.Top.Attacker?.Name}, Opponent: {setupCand.Top.Opponent?.Name}, Attacker Ally: {setupCand.Top.AttackerAlly?.Name}, Opponent Ally: {setupCand.Top.OpponentAlly?.Name})" );
                break;

                case ActionType.OffensiveStatus:
                    intent.IntentResult = tic.OffensiveStatusThreatResult;
                    var offStatusCand = (StatusThreatResult)intent.IntentResult;
                    _tirLog.Add( $"With move: {offStatusCand.Move.MoveSO.Name} (Attacker: {offStatusCand.Top.Attacker?.Name}, Opponent: {offStatusCand.Top.Opponent?.Name}, Attacker Ally: {offStatusCand.Top.AttackerAlly?.Name}, Opponent Ally: {offStatusCand.Top.OpponentAlly?.Name})" );
                break;

                case ActionType.SupportiveStatus:
                    intent.IntentResult = tic.SupportiveStatusThreatResult;
                    var suppStatusCand = (StatusThreatResult)intent.IntentResult;
                    _tirLog.Add( $"With move: {suppStatusCand.Move.MoveSO.Name} (Attacker: {suppStatusCand.Top.Attacker?.Name}, Opponent: {suppStatusCand.Top.Opponent?.Name}, Attacker Ally: {suppStatusCand.Top.AttackerAlly?.Name}, Opponent Ally: {suppStatusCand.Top.OpponentAlly?.Name})" );
                break;
                
                default:
                    intent.IntentResult = tic.MoveThreatResult;
                break;
            };
        }

        _tirLog.Add( $"================================" );

        // Debug.Log( _tirLog.ToString() );
        string path = Application.persistentDataPath + "/ThreatIntentResult_Log.txt";
        System.IO.File.AppendAllText( path, _tirLog.ToString() + "\n" + "\n" + "\n" + "\n" + "\n" );
        _tirLog.Clear();

        return tir;
    }

    private int AttackEvidence( ref ThreatIntentCandidates tic, ExchangeEvaluation ee, BattlefieldState bfs, ThreatProfile theirTP, ThreatProfile ourTP, CurrentPlan theirCP )
    {
        int evidence = 0;

        var attackTOP = tic.MoveThreatResult.Top;
        var attackExchangePack = _ai.Projection.GetExchangePack( attackTOP.Attacker, attackTOP.Opponent );

        //---------------------------------------------------------------------------------
        //--Attack Evidence Gathering------------------------------------------------------
        //---------------------------------------------------------------------------------
        _tirLog.Add( $"===[Attack Evidence]===" );
        var ourRP = attackTOP.Opponent.RoleProfile;
        var ourBiases = ourRP.Biases;
        var ourTraits = ourRP.Traits;
        var ourCourt = attackTOP.Opponent.CourtLocation  == CourtLocation.TopCourt ? _ai.Blackboard.CurrentFieldSnapshot.TopCourtConditions : _ai.Blackboard.CurrentFieldSnapshot.BottomCourtConditions;

        bool weHaveOffensiveSetup = ourTraits.Contains( RoleTrait.PhysicallyOffensiveSetup ) || ourTraits.Contains( RoleTrait.SpeciallyOffensiveSetup );
        bool weHaveDefensiveSetup = ourTraits.Contains( RoleTrait.PhysicallyDefensiveSetup ) || ourTraits.Contains( RoleTrait.SpeciallyDefensiveSetup );
        bool weHaveSetup = weHaveOffensiveSetup || weHaveDefensiveSetup;
        bool weHaveTailwindAvailable = ourTraits.Contains( RoleTrait.TailwindSetter );
        bool weHaveTailwindActive = ourCourt.ContainsKey( CourtConditionID.Tailwind );
        bool weHaveFollowMe = ourTraits.Contains( RoleTrait.RedirectionMove );
        bool weHaveRecoveryMove = ourTraits.Contains( RoleTrait.RecoveryMove );

        bool weCanUseFakeOut = _ai.CanUseFakeOut( attackTOP.Opponent, attackTOP.Attacker );

        bool weHaveSash = attackTOP.Opponent.Item == ItemBattleEffectID.FocusSash && attackTOP.Opponent.BeginningHPR == 1f;
        bool weHaveSturdy = attackTOP.Opponent.Ability == AbilityID.Sturdy && attackTOP.Opponent.BeginningHPR == 1f;
        bool weHaveMultiscale = attackTOP.Opponent.Ability == AbilityID.Multiscale && attackTOP.Opponent.BeginningHPR == 1f;
        bool weHaveDisguise = attackTOP.Opponent.Ability == AbilityID.Disguise && attackTOP.Opponent.BeginningHPR == 1f;
        bool weHaveFullHPSave = weHaveSash || weHaveSturdy || weHaveMultiscale || weHaveDisguise;

        bool weHavePriority = ee.OpponentHasPriorityMove;

        bool weCanProtect = _ai.UnitSim.CheckHasMove( _ai.CurrentUnitAdapter, "Protect" ) && _ai.GetBattleUnit( _ai.CurrentUnitAdapter.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;

        bool weHave_HelpingHand   = _ai.UnitSim.CheckHasMove( attackTOP.Opponent, "Helping Hand" );
        bool weHave_Coaching      = _ai.UnitSim.CheckHasMove( attackTOP.Opponent, "Coaching" );
        bool weHave_Tailwind      = ourTraits.Contains( RoleTrait.TailwindSetter ) && !ourCourt.ContainsKey( CourtConditionID.Tailwind );
        bool weHave_TrickRoom     = ourTraits.Contains( RoleTrait.TrickRoomSetter );
        bool weHave_SpeedControl  = ourTraits.Contains( RoleTrait.SpeedControl );
        bool weHave_FakeOut       = _ai.CanUseFakeOut( attackTOP.Opponent, attackTOP.Attacker );
        bool weHave_FollowMe      = _ai.UnitSim.CheckHasMove( attackTOP.Opponent, "Follow Me" );
        bool weHave_RagePowder    = _ai.UnitSim.CheckHasMove( attackTOP.Opponent, "Rage Powder" );
        bool weHave_Feint         = _ai.UnitSim.CheckHasMove( attackTOP.Opponent, "Feint" );
        bool weHave_WideGuard     = _ai.UnitSim.CheckHasMove( attackTOP.Opponent, "Wide Guard" );
        bool weHave_QuickGuard    = _ai.UnitSim.CheckHasMove( attackTOP.Opponent, "Quick Guard" );

        var theirRP = attackTOP.Attacker.RoleProfile;
        var theirBiases = theirRP.Biases;
        var theirTraits = theirRP.Traits;
        var theirCourt = attackTOP.Attacker.CourtLocation  == CourtLocation.TopCourt ? _ai.Blackboard.CurrentFieldSnapshot.TopCourtConditions : _ai.Blackboard.CurrentFieldSnapshot.BottomCourtConditions;
        
        bool theyAreOffensive = theirRP.PrimaryRole == RoleClass.BulkyAttacker || theirRP.PrimaryRole == RoleClass.RevengeKiller || theirRP.PrimaryRole == RoleClass.SetupSweeper ||
            theirRP.PrimaryRole == RoleClass.Sweeper || theirRP.PrimaryRole == RoleClass.TrickRoomAbuser || theirRP.PrimaryRole == RoleClass.WallBreaker;

        bool theyCanUseFakeOut = _ai.CanUseFakeOut( attackTOP.Attacker, attackTOP.Opponent );
        bool theyHavePriority = ee.AttackerHasPriorityMove;
        bool theyAreChoiceLocked = attackTOP.Attacker.Item == ItemBattleEffectID.ChoiceBand || attackTOP.Attacker.Item == ItemBattleEffectID.ChoiceSpecs || attackTOP.Attacker.Item == ItemBattleEffectID.ChoiceScarf;

        bool theyHave_HelpingHand   = _ai.UnitSim.CheckHasMove( attackTOP.Attacker, "Helping Hand" );
        bool theyHave_Coaching      = _ai.UnitSim.CheckHasMove( attackTOP.Attacker, "Coaching" );
        bool theyHave_Tailwind      = ourTraits.Contains( RoleTrait.TailwindSetter ) && !ourCourt.ContainsKey( CourtConditionID.Tailwind );
        bool theyHave_TrickRoom     = ourTraits.Contains( RoleTrait.TrickRoomSetter );
        bool theyHave_SpeedControl  = ourTraits.Contains( RoleTrait.SpeedControl );
        bool theyHave_FakeOut       = _ai.CanUseFakeOut( attackTOP.Attacker, attackTOP.Opponent );
        bool theyHave_FollowMe      = _ai.UnitSim.CheckHasMove( attackTOP.Attacker, "Follow Me" );
        bool theyHave_RagePowder    = _ai.UnitSim.CheckHasMove( attackTOP.Attacker, "Rage Powder" );
        bool theyHave_Feint         = _ai.UnitSim.CheckHasMove( attackTOP.Attacker, "Feint" );
        bool theyHave_WideGuard     = _ai.UnitSim.CheckHasMove( attackTOP.Attacker, "Wide Guard" );
        bool theyHave_QuickGuard    = _ai.UnitSim.CheckHasMove( attackTOP.Attacker, "Quick Guard" );

        int ourRemainingCount = _ai.Blackboard.OurTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList().Count;
        int theirRemainingCount = _ai.Blackboard.TheirTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList().Count;
        int materialDelta = theirRemainingCount - ourRemainingCount;

        if( attackTOP.Opponent_DiesBeforeActing )
        {
            evidence += 2;
            _tirLog.Add( $"We die before acting. Attack Evidence: {evidence}" );
        }
        else if( attackTOP.Opponent_EndOfTurnHP <= 0 || attackTOP.AttackerPTKO >= PotentialToKO.Dangerous )
        {
            evidence += 1;

            _tirLog.Add( $"We're likely to die if they attack. Attack Evidence: {evidence}" );

            if( attackTOP.AttackerMovedFirst )
            {
                evidence += 1;
                _tirLog.Add( $"They're also faster than us. Attack Evidence: {evidence}" );
            }
        }

        if( theyAreOffensive && attackTOP.Attacker.BeginningHPR >= 0.8f && attackTOP.AttackerPTKO >= PotentialToKO.Risky )
        {
            evidence += 1;
            _tirLog.Add( $"Their hp is >= 80%, and they are an offensive unit who can do big damage. Attack Evidence: {evidence}" );
        }

        if( theyAreOffensive )
        {
            evidence += 1;
            _tirLog.Add( $"They are an offensively oriented role. Attack Evidence: {evidence}" );
        }

        if( attackTOP.OpponentPTKO <= PotentialToKO.TwoHKO || ( attackTOP.Opponent.BeginningHPR <= 0.25f && attackTOP.AttackerMovedFirst ) )
        {
            evidence += 1;
            _tirLog.Add( $"We don't do much damage to them, or we have <= 25% HP and they move first. Attack Evidence: {evidence}" );
        }

        if( attackTOP.AttackerPTKO > attackTOP.OpponentPTKO )
        {
            evidence += 1;
            _tirLog.Add( $"Their PTKO is higher than ours. Attack Evidence: {evidence}" );
        }

        if( attackTOP.Attacker.BeginningHPR <= 0.6f && attackTOP.Attacker.BeginningHPR >= 0.33f )
        {
            evidence -= 1;
            _tirLog.Add( $"Their their hp is middling, they may try to preserve a mon. Attack Evidence: {evidence}" );
        }
        else if( attackTOP.Attacker.BeginningHPR < 0.33f && attackTOP.Attacker.Expendability >= 0.4f )
        {
            evidence += 1;
            _tirLog.Add( $"Their their hp is low and their pokemon is fairly expendabile, they may just sac it. Attack Evidence: {evidence}" );
        }

        //--BattlefieldState
        if( ee.ExchangeState == ExchangeState.Pressure )
        {
            evidence += 1;
            _tirLog.Add( $"The exchange state is pressure!. Attack Evidence: {evidence}" );
        }

        if( ee.OpponentSwitchProbability >= 0.75f )
        {
            evidence += 1;
            _tirLog.Add( $"They think we may switch, they may try to punish the switch in with an attack. Attack Evidence: {evidence}" );
        }

        if( bfs.IsLateGame )
        {
            evidence += 1;
            _tirLog.Add( $"It's late game, they may just attack. Attack Evidence: {evidence}" );
        }

        if( attackTOP.AttackerPTKO != PotentialToKO.OHKO && attackTOP.AttackerPTKO >= PotentialToKO.TwoHKO )
        {
            int comebackPotential = 0;
            bool opponentSelfDebuffs = _ai.UnitSim.CheckHasSelfDebuffMove( attackTOP.Attacker.ActiveMoves ) && attackTOP.AttackerMovedFirst;
            bool opponentChipsSelf = ( _ai.UnitSim.CheckHasRecoilMove( attackTOP.Attacker.ActiveMoves ) || attackTOP.Attacker.Item == ItemBattleEffectID.LifeOrb ) && attackTOP.AttackerMovedFirst;

            if( ee.AttackerThreatensKO )
                comebackPotential++;

            if( opponentSelfDebuffs )
                comebackPotential++;

            if( opponentChipsSelf )
                comebackPotential++;

            if( comebackPotential >= 2 )
            {
                evidence += 1;
                _tirLog.Add( $"They have a bit of comeback potential through strong moves with drawback or increased damage from an item. Attack Evidence: {evidence}" );
            }
        }

        if( attackTOP.Opponent.Ability == AbilityID.MagicBounce || attackTOP.Opponent.Ability == AbilityID.MagicGuard )
        {
            evidence += 1;
            _tirLog.Add( $"Our current unit has magic bounce or magic guard, they cannot use a status move if we stay. Attack Evidence: {evidence}" );
        }

        //--Pokemon dies to hazards check
        float hpAfterHazards = _ai.Get_HPRatio_AfterEntryHazards( attackTOP.Attacker );
        if( attackTOP.Attacker.BeginningHPR != hpAfterHazards && hpAfterHazards <= 0f )
        {
            evidence += 1;
            _tirLog.Add( $"If they recall their unit, it will faint to hazards when they switch it in later if they don't clear them. Attack Evidence: {evidence}" );

            int removers = 0;
            foreach( var mon in _ai.Blackboard.TheirTeamAdapters.Values )
            {
                if( mon.RoleProfile.Traits.Contains( RoleTrait.HazardRemover ) )
                    removers++;
            }

            if( removers == 0 )
            {
                evidence += 1;
                _tirLog.Add( $"They also have no hazard removers on their team. Attack Evidence: {evidence}" );
            }
        }

        //--Threat Profile
        if( theirTP.SweepPotential )
        {
            evidence += 1;
            _tirLog.Add( $"They are about to start sweeping, attacking is attractive. Attack Evidence: {evidence}" );
        }

        if( theirTP.Type == ThreatType.Immediate && theirTP.Urgency >= ThreatUrgency.High )
        {
            evidence += 1;
            _tirLog.Add( $"They are a >= high urgency, immediate threat. Attack Evidence: {evidence}" );
        }

        //--PBS
        bool attackImprovesPressure = attackTOP.AttackerPTKO > attackTOP.OpponentPTKO && attackTOP.AttackerPTKO >= PotentialToKO.Risky;
        if( attackImprovesPressure )
        {
            evidence += 1;
            _tirLog.Add( $"They improve their board's offensive pressure if they attack now. Attack Evidence: {evidence}" );
        }

        bool attackMaintainsStability = attackTOP.Attacker_EndOfTurnHP > 0 && attackTOP.OpponentPTKO < PotentialToKO.Dangerous;
        if( attackMaintainsStability )
        {
            evidence += 1;
            _tirLog.Add( $"Attacking keeps the board stable for them. Attack Evidence: {evidence}" );
        }

        //--Current Plan & GPA
        if( theirCP.Type == PlanType.EnableSweep )
        {
            //--Primary Sweeper Intent. Add best offensive units to GamePlan!!!
            if( theirCP.FocusMon == attackTOP.Attacker.Pokemon )
            {
                evidence += 1;
                _tirLog.Add( $"They are looking to sweep and we are their intended sweeper. Attack Evidence: {evidence}" );
            }

            //--Blocker Removal
            if( _ai.Blackboard.GamePlan.OurBlockers.Contains( attackTOP.Opponent.Pokemon ) )
            {
                evidence += 1;
                _tirLog.Add( $"They are looking to sweep and we are a game plan blocker. Attack Evidence: {evidence}" );
            }
        }

        if( theirCP.FocusMon == attackTOP.Opponent.Pokemon && theirCP.Type == PlanType.Aggress )
        {
            evidence += 1;
            _tirLog.Add( $"They are looking to aggress our current pokemon. Attack Evidence: {evidence}" );
        }

        if( theirCP.Type == PlanType.Trade )
        {
            if( materialDelta > 0 )
            {
                evidence += 1;
                _tirLog.Add( $"They hold a material advantage, simply trading benefits them. Attack Evidence: {evidence}" );
            }
        }

        if( theirCP.AllowSacrifice && attackTOP.Attacker.Expendability >= 0.7f )
        {
            evidence += 1;
            _tirLog.Add( $"Their plan allows sacrificing, the current Pokemon is expendible. Attack Evidence: {evidence}" );
        }

        if( theirCP.Type == PlanType.PreventSweep && attackTOP.Opponent.Pokemon == theirCP.FocusMon )
        {
            evidence += 1;
            _tirLog.Add( $"Their plan is to prevent us from sweeping and we are the focus mon. Attack Evidence: {evidence}" );

            if( attackTOP.AttackerPTKO >= PotentialToKO.Dangerous && ( attackTOP.AttackerMovedFirst || attackTOP.OpponentPTKO <= PotentialToKO.Risky ) )
            {
                evidence += 1;
                _tirLog.Add( $"And they have a good chance of KOing us. Attack Evidence: {evidence}" );
            }
        }

        if( theirCP.Type == PlanType.Stabilize && attackTOP.AttackerPTKO >= PotentialToKO.Dangerous && ( attackTOP.AttackerMovedFirst || attackTOP.OpponentPTKO <= PotentialToKO.Risky ) )
        {
            evidence += 1;
            _tirLog.Add( $"Their plan is to stabilize and they are very likely to get a KO on us, stabilizing the board. Attack Evidence: {evidence}" );
        }

        //--Momentum Denial
        bool weHaveMomentumOpportunity = weHaveSetup || weHaveTailwindAvailable || weHaveTailwindActive || weHaveFollowMe || weHaveRecoveryMove;
        if( attackTOP.OpponentPTKO >= PotentialToKO.Risky && weHaveMomentumOpportunity )
        {
            evidence += 1;
            _tirLog.Add( $"We have the ability to seize momentum if they let us live. Attack Evidence: {evidence}" );

            if( attackTOP.OpponentPTKO == PotentialToKO.OHKO )
            {
                evidence += 1;
                _tirLog.Add( $"They mostlikely have an OHKO on us in this situation. Attack Evidence: {evidence}" );
            }

            if( attackTOP.OpponentPTKO >= PotentialToKO.Dangerous && weHaveTailwindActive && ( attackTOP.OpponentMovedFirst || attackTOP.AttackerPTKO <= PotentialToKO.Risky ) )
            {
                evidence += 1;
                _tirLog.Add( $"We have tailwind up and they are likely to KO us with little risk to themselves. Attack Evidence: {evidence}" );
            }
        }

        //--Resource Denial
        if( weHaveFullHPSave )
        {
            evidence += 1;
            _tirLog.Add( $"We have a full hp saving resource (focus sash, multiscale, etc.), breaking it benefits them regardless of PTKO. Attack Evidence: {evidence}" );
        }

        //--Priority Removal
        if( weHavePriority && attackTOP.Opponent.BeginningHPR <= 0.3f )
        {
            evidence += 1;
            _tirLog.Add( $"We have priority moves and low hp, which we will be more likely to click for chip or reverse sweep potential, so they may want to remove us now. Attack Evidence: {evidence}" );

            if( weHaveOffensiveSetup || ( _ai.UnitSim.PokemonIsIronDefenseBodyPress( attackTOP.Opponent.Pokemon ) && ourTraits.Contains( RoleTrait.RecoveryItem ) ) )
            {
                evidence += 1;
                _tirLog.Add( $"We also have offensive setup or Iron Defense + Body Press and passive recovery, which means they absolutely cannot give us a free turn and must pressure us. Attack Evidence: {evidence}" );
            }
        }

        //--Priority Opportunity
        if( theyHavePriority && attackTOP.Opponent.BeginningHPR <= 0.3f )
        {
            evidence += 1;
            _tirLog.Add( $"We have >= 30% hp left and they have access to priority attacks, they may want to chip us out. Attack Evidence: {evidence}" );

            if( attackTOP.AttackerPTKO >= PotentialToKO.Dangerous )
            {
                evidence += 1;
                _tirLog.Add( $"They also have a reasonable PTKO chance. Attack Evidence: {evidence}" );
            }
        }

        //--Recovery Prevention
        if( weHaveRecoveryMove && attackTOP.Opponent.BeginningHPR <= 0.55f )
        {
            if( attackTOP.AttackerPTKO >= PotentialToKO.OHKO )
            {
                evidence += 1;
                _tirLog.Add( $"We have a recovery move and less than 55% hp, and they likely OHKO us now, they may try to limit our healing. Attack Evidence: {evidence}" );
            }
            else if( attackTOP.AttackerPTKO >= PotentialToKO.Risky )
            {
                evidence += 1;
                _tirLog.Add( $"We have a recovery move and less than 55% hp, and they do big damage, they may try to limit our healing. Attack Evidence: {evidence}" );
            }

            if( attackTOP.AttackerMovedFirst )
            {
                evidence += 1;
                _tirLog.Add( $"They also move first, giving them the advantage on our potential healing attempt. Attack Evidence: {evidence}" );
            }
        }

        //--Existing Momentum
        if( theyAreChoiceLocked && ( attackTOP.AttackerPTKO >= PotentialToKO.Risky || attackTOP.OpponentPTKO <= PotentialToKO.TwoHKO ) )
        {
            evidence += 1;
            _tirLog.Add( $"They are choice locked and deal reasonable damage or are not threatened by us, so they may want to attack. Attack Evidence: {evidence}" );
        }

        foreach( var sc in attackTOP.Attacker.StatStages )
        {
            if( ( sc.Key == Stat.Attack && theirBiases.Contains( RoleBias.Physical ) ) || ( sc.Key == Stat.SpAttack && theirBiases.Contains( RoleBias.Special ) ) )
            {
                if( sc.Value > 1 )
                {
                    evidence += 2;
                    _tirLog.Add( $"They have multiple offensively aligned stat boosts and likely don't want to waste them. Attack Evidence: {evidence}" );
                }
                else if( sc.Value > 0 )
                {
                    evidence += 1;
                    _tirLog.Add( $"They have an offensively aligned stat boost and likely don't want to waste it. Attack Evidence: {evidence}" );
                }
            }

            if( sc.Key == Stat.Speed && sc.Value > 0 )
            {
                evidence += 1;
                _tirLog.Add( $"They have speed boosts and will likely not want to waste the tempo opportunity. Attack Evidence: {evidence}" );
            }
        }

        if( attackTOP.Attacker.BeginningHPR <= 0.25f )
        {
            if( attackTOP.AttackerMovedFirst && attackTOP.AttackerPTKO >= PotentialToKO.Risky )
            {
                evidence += 1;
                _tirLog.Add( $"They have very low health and can still move first and do big damage, they may attack and sack, or get lucky with a crit. Attack Evidence: {evidence}" );
            }
        }

        if( bfs.EntryHazardsOn_MySide > 0 && attackTOP.AttackerPTKO >= PotentialToKO.TwoHKO && attackTOP.OpponentPTKO <= PotentialToKO.TwoHKO )
        {
            evidence += 1;
            _tirLog.Add( $"There's hazards on our side, so they may attack knowing we are less likely to switch, or to further punish a switch. Attack Evidence: {evidence}" );
        }

        int fakeOutScore = 0;
        var theirAlly = _ai.GetActiveAllyAs_Adapter( tic.Threat.Pokemon );
        if( _ai.IsDoubleBattle )
        {
            _tirLog.Add( $"" );
            _tirLog.Add( $"This is a double battle! Checking attack intent evidence in the context of doubles..." );
            _tirLog.Add( $"Also checking fake out intent for doubles..." );
            
            bool theyHaveAlly = theirAlly != null;
            if( !theyHaveAlly )
                _tirLog.Add( $"They don't have an ally on the field!" );

            _tirLog.Add( $"Their ({tic.Threat.Name}) ally is: {theirAlly.Name} ({theirAlly.RoleProfile.PrimaryRole})" );
            _tirLog.Add( $"" );

            //--Our Ally Information
            var ourAlly = _ai.GetActiveAllyAs_Adapter( _ai.CurrentUnitAdapter.Pokemon );
            bool weHaveAlly = ourAlly != null;
            if( weHaveAlly )
            {
                _tirLog.Add( $"Our ({_ai.CurrentUnitAdapter.Name}) ally is: {ourAlly.Name} ({ourAlly.RoleProfile.PrimaryRole})" );
                _tirLog.Add( $"" );
            }

            bool ourAllyCanProtect = weHaveAlly && _ai.UnitSim.CheckHasMove( ourAlly, "Protect" ) && _ai.GetBattleUnit( ourAlly.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;

            //--Their Ally Information
            RoleProfile theirAllyRP = theyHaveAlly ? theirAlly.RoleProfile : default;
            HashSet<RoleTrait> theirAllyTraits = theirAllyRP.Traits;

            bool theirAllyIsOffensive = theyHaveAlly && ( theirAllyRP.PrimaryRole == RoleClass.BulkyAttacker || theirAllyRP.PrimaryRole == RoleClass.RevengeKiller || theirAllyRP.PrimaryRole == RoleClass.SetupSweeper ||
            theirAllyRP.PrimaryRole == RoleClass.Sweeper || theirAllyRP.PrimaryRole == RoleClass.TrickRoomAbuser || theirAllyRP.PrimaryRole == RoleClass.WallBreaker );

            bool theirAllyIsDefensive = theyHaveAlly && ( theirAllyRP.PrimaryRole == RoleClass.Wall || theirAllyRP.PrimaryRole == RoleClass.DefensiveSetup || theirAllyRP.PrimaryRole == RoleClass.BulkyAttacker && theirAllyRP.SecondaryRoles.Contains( RoleClass.Wall ) );
            bool theirAllyIsSupport = theyHaveAlly && ( theirAllyRP.PrimaryRole == RoleClass.UtilitySupport || theirAllyRP.PrimaryRole == RoleClass.Disrupter );
            bool theirAllyIsUtility = theyHaveAlly && ( !theirAllyIsOffensive && !theirAllyIsDefensive );

            bool theirAllyHas_HelpingHand   = theyHaveAlly && _ai.UnitSim.CheckHasMove( theirAlly, "Helping Hand" );
            bool theirAllyHas_Coaching      = theyHaveAlly && _ai.UnitSim.CheckHasMove( theirAlly, "Coaching" );
            bool theirAllyHas_Tailwind      = theyHaveAlly && theirAllyTraits.Contains( RoleTrait.TailwindSetter );
            bool theirAllyHas_TrickRoom     = theyHaveAlly && theirAllyTraits.Contains( RoleTrait.TrickRoomSetter );
            bool theirAllyHas_SpeedControl  = theyHaveAlly && theirAllyTraits.Contains( RoleTrait.SpeedControl );
            bool theirAllyHas_FakeOut       = theyHaveAlly && _ai.CanUseFakeOut( theirAlly, _ai.CurrentUnitAdapter );
            bool theirAllyHas_FollowMe      = theyHaveAlly && _ai.UnitSim.CheckHasMove( theirAlly, "Follow Me" );
            bool theirAllyHas_RagePowder    = theyHaveAlly && _ai.UnitSim.CheckHasMove( theirAlly, "Rage Powder" );
            bool theirAllyHas_Feint         = theyHaveAlly && _ai.UnitSim.CheckHasMove( theirAlly, "Feint" );
            bool theirAllyHas_WideGuard     = theyHaveAlly && _ai.UnitSim.CheckHasMove( theirAlly, "Wide Guard" );
            bool theirAllyHas_QuickGuard    = theyHaveAlly && _ai.UnitSim.CheckHasMove( theirAlly, "Quick Guard" );

            if( theirAllyHas_HelpingHand )
            {
                evidence += 2;
                _tirLog.Add( $"Their Ally has Helping Hand. Attack Evidence: {evidence}" );
            }

            if( theirAllyHas_Coaching )
            {
                evidence += 1;
                fakeOutScore += 1;
                _tirLog.Add( $"Their Ally has Coaching. Attack Evidence: {evidence}, Fake Out Score: {fakeOutScore}" );
            }

            if( theirAllyHas_Tailwind && !theirCourt.ContainsKey( CourtConditionID.Tailwind ) )
            {
                evidence += 1;
                fakeOutScore += 1;
                _tirLog.Add( $"Their Ally can set Tailwind. Attack Evidence: {evidence}, Fake Out Score: {fakeOutScore}" );

                if( theirAlly.Speed > attackTOP.Attacker.Speed )
                {
                    evidence += 1;
                    fakeOutScore += 1;
                    _tirLog.Add( $"Their Ally outspeeds them, meaning their ally can set tailwind before they attack. Attack Evidence: {evidence}, Fake Out Score: {fakeOutScore}" );
                }

                if( weHaveAlly && theirAlly.Speed > ourAlly.Speed )
                {
                    evidence += 1;
                    fakeOutScore += 1;
                    _tirLog.Add( $"Their Ally outspeeds our ally, meaning their ally can set tailwind before our ally can act, assuming our ally does not use priority. Attack Evidence: {evidence}, Fake Out Score: {fakeOutScore}" );
                }
            }

            if( theirAllyHas_TrickRoom && !_ai.Blackboard.CurrentFieldSnapshot.FieldConditions.ContainsKey( FieldConditionID.TrickRoom ) && theyCanUseFakeOut )
            {
                evidence += 3;
                fakeOutScore += 2;
                _tirLog.Add( $"Their Ally can set Trick Room and they can currently use fake out. Attack Evidence: {evidence}, Fake Out Score: {fakeOutScore}" );
            }

            if( theirAllyHas_SpeedControl )
            {
                evidence += 1;
                fakeOutScore += 1;
                _tirLog.Add( $"Their Ally has Speed Control. Attack Evidence: {evidence}, Fake Out Score: {fakeOutScore}" );
            }

            if( theirAllyHas_FakeOut )
            {
                evidence += 2;
                fakeOutScore += 1;
                _tirLog.Add( $"Their Ally has Fake Out available. Attack Evidence: {evidence}, Fake Out Score: {fakeOutScore}" );
            }

            if( theirAllyHas_FollowMe )
            {
                evidence += 1;
                fakeOutScore += 1;
                _tirLog.Add( $"Their Ally has Follow Me. Attack Evidence: {evidence}, Fake Out Score: {fakeOutScore}" );
            }

            if( theirAllyHas_RagePowder )
            {
                evidence += 1;
                fakeOutScore += 1;
                _tirLog.Add( $"Their Ally has Rage Powder. Attack Evidence: {evidence}, Fake Out Score: {fakeOutScore}" );
            }

            if( theirAllyHas_Feint )
            {
                evidence += 1;
                fakeOutScore += 1;
                _tirLog.Add( $"Their Ally has Feint. Attack Evidence: {evidence}, Fake Out Score: {fakeOutScore}" );
            }

            if( theirAllyHas_WideGuard )
            {
                evidence += 1;
                fakeOutScore += 1;
                _tirLog.Add( $"Their Ally has WideGuard. Attack Evidence: {evidence}, Fake Out Score: {fakeOutScore}" );
            }

            if( theirAllyHas_QuickGuard )
            {
                evidence += 1;
                fakeOutScore += 1;
                _tirLog.Add( $"Their Ally has QuickGuard. Attack Evidence: {evidence}, Fake Out Score: {fakeOutScore}" );
            }

            if( weCanProtect || ourAllyCanProtect )
            {
                evidence -= 1;
                fakeOutScore += 1;
                _tirLog.Add( $"We or our ally have protect on the table. Attack Evidence: {evidence}, Fake Out Score: {fakeOutScore}" );
            }
        }

        //--Opposing Intents
        //--Setup vs Attack
        bool theyHaveOffensiveSetup = theirTraits.Contains( RoleTrait.PhysicallyOffensiveSetup ) || theirTraits.Contains( RoleTrait.SpeciallyOffensiveSetup );
        if( tic.SetupThreatResult.Move != null && theyHaveOffensiveSetup && attackTOP.AttackerPTKO <= PotentialToKO.TwoHKO && attackTOP.OpponentPTKO <= PotentialToKO.Risky )
        {
            foreach( var sc in attackTOP.Attacker.StatStages )
            {
                if( sc.Value < 1 && ( sc.Key == Stat.Attack && theirRP.Biases.Contains( RoleBias.Physical ) || sc.Key == Stat.SpAttack && theirRP.Biases.Contains( RoleBias.Special ) ) )
                {
                    evidence -= 1;
                    _tirLog.Add( $"They are a bit behind on offensive setup and have a safe opportunity to do so, they may go for it. Attack Evidence: {evidence}" );
                    break;
                }
            }
        }

        //--Offensive Status vs Attack
        if( tic.OffensiveStatusThreatResult.Move != null && tic.OffensiveStatusThreatResult.OffensiveStatusType != OffensiveStatusType.EntryHazard )
        {
            if( theirTraits.Contains( RoleTrait.StatusSpreader ) && attackTOP.AttackerPTKO <= PotentialToKO.Risky && attackTOP.OpponentPTKO <= PotentialToKO.Risky )
            {
                evidence -= 1;
                _tirLog.Add( $"They are a status spreader and the situation is safe enough that they may attempt status instead. Attack Evidence: {evidence}" );
            }
        }

        //--Supportive Status vs Attack in Doubles
        if( tic.SupportiveStatusThreatResult.Move != null && theirAlly != null )
        {
            var theirAllyRP = theirAlly.RoleProfile;
            bool theyHaveAlly = theirAlly != null;
            bool theirAllyIsOffensive = theyHaveAlly && ( theirAllyRP.PrimaryRole == RoleClass.BulkyAttacker || theirAllyRP.PrimaryRole == RoleClass.RevengeKiller || theirAllyRP.PrimaryRole == RoleClass.SetupSweeper ||
                theirAllyRP.PrimaryRole == RoleClass.Sweeper || theirAllyRP.PrimaryRole == RoleClass.TrickRoomAbuser || theirAllyRP.PrimaryRole == RoleClass.WallBreaker );

            bool theyAreSupport = theirRP.PrimaryRole == RoleClass.UtilitySupport || theirRP.PrimaryRole == RoleClass.Disrupter || theirRP.SecondaryRoles.Contains( RoleClass.UtilitySupport );

            if( theyAreSupport && theirAllyIsOffensive && attackTOP.AttackerPTKO <= PotentialToKO.TwoHKO )
            {
                evidence -= 1;
                _tirLog.Add( $"They are a support mon with a weak PTKO and their ally is an offensive role, they may be more likely to support than attack. Attack Evidence: {evidence}" );
            }
        }

        //--Defensive Switch losing exchange
        if( tic.DefensiveSwitchCandidateResult.Pokemon != null && attackTOP.AttackerPTKO <= PotentialToKO.TwoHKO && attackTOP.OpponentPTKO >= PotentialToKO.Dangerous )
        {
            evidence -= 1;
            _tirLog.Add( $"They are currently sitting in a losing exchange that benefits defensively switching. Attack Evidence: {evidence}" );
        }

        //--Offensive Switch vs Attack
        if( tic.OffensiveSwitchCandidateResult.Pokemon != null )
        {
            var offSwitchTOP = tic.OffensiveSwitchCandidateResult.Top;
            if( attackTOP.OpponentPTKO <= PotentialToKO.TwoHKO && attackTOP.AttackerPTKO >= PotentialToKO.Dangerous && ( ( offSwitchTOP.OpponentPTKO > attackTOP.OpponentPTKO && offSwitchTOP.OpponentMovedFirst ) || offSwitchTOP.OpponentPTKO >= PotentialToKO.Dangerous ) )
            {
                evidence -= 1;
                _tirLog.Add( $"They have an offensive candidate that is more suited to the match up offensively than their current mon. Attack Evidence: {evidence}" );
            }
        }

        //--Fake Out Evaluation, assuming the chosen move is not already fake out
        if( theyCanUseFakeOut && tic.MoveThreatResult.Move.MoveSO.Name != "Fake Out" )
        {
            _tirLog.Add( $"" );
            _tirLog.Add( $"=[Fake Out Evaluation]=" );
            _tirLog.Add( $"" );

            //--Our Ally Information
            var ourAlly = _ai.GetActiveAllyAs_Adapter( _ai.CurrentUnitAdapter.Pokemon );
            bool weHaveAlly = ourAlly != null;
            if( weHaveAlly )
            {
                _tirLog.Add( $"Our ({_ai.CurrentUnitAdapter.Name}) ally is: {ourAlly.Name} ({ourAlly.RoleProfile.PrimaryRole})" );
                _tirLog.Add( $"" );
            }

            RoleProfile ourAllyRP = ourAlly != null ? ourAlly.RoleProfile : default;
            HashSet<RoleTrait> ourAllyTraits = ourAllyRP.Traits;

            bool ourAllyIsOffensive = weHaveAlly && ( ourAllyRP.PrimaryRole == RoleClass.BulkyAttacker || ourAllyRP.PrimaryRole == RoleClass.RevengeKiller || ourAllyRP.PrimaryRole == RoleClass.SetupSweeper ||
                ourAllyRP.PrimaryRole == RoleClass.Sweeper || ourAllyRP.PrimaryRole == RoleClass.TrickRoomAbuser || ourAllyRP.PrimaryRole == RoleClass.WallBreaker );

            bool ourAllyCanProtect = weHaveAlly && _ai.UnitSim.CheckHasMove( ourAlly, "Protect" ) && _ai.GetBattleUnit( ourAlly.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;
            
            bool ourAllyHas_HelpingHand   = weHaveAlly && _ai.UnitSim.CheckHasMove( ourAlly, "Helping Hand" );
            bool ourAllyHas_Coaching      = weHaveAlly && _ai.UnitSim.CheckHasMove( ourAlly, "Coaching" );
            bool ourAllyHas_Tailwind      = weHaveAlly && ourAllyTraits.Contains( RoleTrait.TailwindSetter ) && !ourCourt.ContainsKey( CourtConditionID.Tailwind );
            bool ourAllyHas_TrickRoom     = weHaveAlly && ourAllyTraits.Contains( RoleTrait.TrickRoomSetter );
            bool ourAllyHas_SpeedControl  = weHaveAlly && ourAllyTraits.Contains( RoleTrait.SpeedControl );
            bool ourAllyHas_FakeOut       = weHaveAlly && _ai.CanUseFakeOut( ourAlly, _ai.CurrentUnitAdapter );
            bool ourAllyHas_FollowMe      = weHaveAlly && _ai.UnitSim.CheckHasMove( ourAlly, "Follow Me" );
            bool ourAllyHas_RagePowder    = weHaveAlly && _ai.UnitSim.CheckHasMove( ourAlly, "Rage Powder" );
            bool ourAllyHas_Feint         = weHaveAlly && _ai.UnitSim.CheckHasMove( ourAlly, "Feint" );
            bool ourAllyHas_WideGuard     = weHaveAlly && _ai.UnitSim.CheckHasMove( ourAlly, "Wide Guard" );
            bool ourAllyHas_QuickGuard    = weHaveAlly && _ai.UnitSim.CheckHasMove( ourAlly, "Quick Guard" );

            RoleProfile theirAllyRP = theirAlly != null ? theirAlly.RoleProfile : default;
            HashSet<RoleTrait> theirAllyTraits = theirAllyRP.Traits;

            bool theyHaveAlly = theirAlly != null;

            bool theirAllyIsOffensive = theyHaveAlly && ( theirAllyRP.PrimaryRole == RoleClass.BulkyAttacker || theirAllyRP.PrimaryRole == RoleClass.RevengeKiller || theirAllyRP.PrimaryRole == RoleClass.SetupSweeper ||
                theirAllyRP.PrimaryRole == RoleClass.Sweeper || theirAllyRP.PrimaryRole == RoleClass.TrickRoomAbuser || theirAllyRP.PrimaryRole == RoleClass.WallBreaker );

            if( bfs.IsEarlyGame )
            {
                fakeOutScore += 2;
                _tirLog.Add( $"It's early game. Score: {fakeOutScore}" );
            }

            if( weCanProtect )
            {
                fakeOutScore -= 3;
                _tirLog.Add( $"We can protect, they probably won't fake out into us. Score: {fakeOutScore}" );
            }
            else if( !weCanProtect && ourAllyCanProtect )
            {
                fakeOutScore += 2;
                _tirLog.Add( $"We can't protect, but our ally can, they may fake out into us. Score: {fakeOutScore}" );
            }

            if( weHave_Coaching && ourAlly != null )
            {
                fakeOutScore += 1;
                _tirLog.Add( $"We have coaching and an ally. Score: {fakeOutScore}" );
            }

            if( weHave_Tailwind )
            {
                fakeOutScore += 2;
                _tirLog.Add( $"We can set tailwind. Score: {fakeOutScore}" );
            }

            if( weHave_TrickRoom )
            {
                fakeOutScore += 2;
                _tirLog.Add( $"We can set or reverse trick room. Score: {fakeOutScore}" );
            }

            if( weHave_SpeedControl )
            {
                fakeOutScore += 1;
                _tirLog.Add( $"We have speed control. Score: {fakeOutScore}" );
            }

            if( weHave_FakeOut )
            {
                fakeOutScore += 2;
                _tirLog.Add( $"We can also fake out, they may want to trade fake outs or beat ours. Score: {fakeOutScore}" );

                if( attackTOP.AttackerMovedFirst )
                {
                    fakeOutScore += 1;
                    _tirLog.Add( $"They have a faster fake out. Score: {fakeOutScore}" );
                }
                else if( attackTOP.OpponentMovedFirst )
                {
                    fakeOutScore -= 1;
                    _tirLog.Add( $"We have a faster fake out. Score: {fakeOutScore}" );
                }
            }

            if( weHave_FollowMe || weHave_RagePowder )
            {
                fakeOutScore += 2;
                _tirLog.Add( $"We have follow me or rage powder and fake out moves before it in priority. Score: {fakeOutScore}" );
            }

            if( weHave_Feint )
            {
                fakeOutScore += 1;
                _tirLog.Add( $"We have feint and fake out moves before it in priority. Score: {fakeOutScore}" );
            }

            if( weHave_WideGuard )
            {
                if( attackTOP.AttackerMovedFirst && theirAlly != null && theirAlly.RoleProfile.Traits.Contains( RoleTrait.SpreadAttack ) )
                {
                    fakeOutScore += 1;
                    _tirLog.Add( $"We have wide guard and they move before we do, which will enable their ally to use an available spread move. Score: {fakeOutScore}" );
                }
                else if( attackTOP.OpponentMovedFirst && theirAlly != null && theirAlly.RoleProfile.Traits.Contains( RoleTrait.SpreadAttack ) )
                {
                    fakeOutScore -= 1;
                    _tirLog.Add( $"We have wide guard and we move before they do, which will prevent their ally from using an available spread move. Score: {fakeOutScore}" );
                }
            }

            if( weHave_QuickGuard )
            {
                if( attackTOP.AttackerMovedFirst && theirAlly != null && _ai.UnitSim.PokemonHasMove_OffensivePriority( theirAlly.Pokemon ) )
                {
                    fakeOutScore += 1;
                    _tirLog.Add( $"We have quick guard and they move before we do, which will enable their ally to use an available priority attack. Score: {fakeOutScore}" );
                }
                else if( attackTOP.OpponentMovedFirst && theirAlly != null && _ai.UnitSim.PokemonHasMove_OffensivePriority( theirAlly.Pokemon ) )
                {
                    fakeOutScore -= 8;
                    _tirLog.Add( $"We have quick guard and we move before thy do, which will prevent their ally from using an available priority attack, including fake out. Score: {fakeOutScore}" );
                }
            }

            if( weHaveAlly )
            {
                if( ourAllyHas_Coaching )
                {
                    fakeOutScore += 1;
                    _tirLog.Add( $"Our ally has coaching, they may want to use fake out on our ally. Score: {fakeOutScore}" );
                }

                if( ourAllyHas_Tailwind )
                {
                    fakeOutScore += 1;
                    _tirLog.Add( $"Our ally can set tailwind, they may want to use fake out on our ally. Score: {fakeOutScore}" );
                }

                if( ourAllyHas_TrickRoom )
                {
                    fakeOutScore += 1;
                    _tirLog.Add( $"Our can set or reverse trick room, they may want to use fake out on our ally. Score: {fakeOutScore}" );
                }

                if( ourAllyHas_SpeedControl )
                {
                    fakeOutScore += 1;
                    _tirLog.Add( $"Our ally has speed control, they may want to use fake out on our ally. Score: {fakeOutScore}" );
                }

                if( ourAllyHas_FakeOut )
                {
                    fakeOutScore += 1;
                    _tirLog.Add( $"Our ally also has fake out, they may want to use fake out on our ally or trade fake outs. Score: {fakeOutScore}" );
                }

                if( ourAllyHas_FollowMe || ourAllyHas_RagePowder )
                {
                    fakeOutScore += 1;
                    _tirLog.Add( $"Our ally has follow me or rage powder, they may want to use fake out on our ally because fake out goes before redirection. Score: {fakeOutScore}" );
                }

                if( ourAllyHas_Feint )
                {
                    fakeOutScore += 1;
                    _tirLog.Add( $"Our ally has feint, they may want to use fake out on our ally. Score: {fakeOutScore}" );
                }

                if( ourAllyHas_WideGuard )
                {
                    fakeOutScore += 1;
                    _tirLog.Add( $"Our ally has wide guard, they may want to use fake out on our ally. Score: {fakeOutScore}" );
                }

                if( ourAllyHas_QuickGuard )
                {
                    fakeOutScore -= 1;
                    _tirLog.Add( $"Our ally has quick guard, they may not want to use fake out on our ally. Score: {fakeOutScore}" );
                }

                if( attackTOP.OpponentPTKO >= PotentialToKO.Dangerous )
                {
                    fakeOutScore += 1;
                    _tirLog.Add( $"We have a likely KO on them, using fake out prevents that. Score: {fakeOutScore}" );

                    if( attackExchangePack.AllyVS_Threat.AttackerPTKO >= PotentialToKO.Dangerous )
                    {
                        fakeOutScore += 1;
                        _tirLog.Add( $"Their ally has a likely KO on us as well, using fake out may secure that KO and prevent ours from going off. Score: {fakeOutScore}" );
                    }
                }

                if( attackExchangePack.OurAllyExists )
                {
                    if( attackExchangePack.AllyVS_Threat.OpponentPTKO >= PotentialToKO.Dangerous )
                    {
                        fakeOutScore += 1;
                        _tirLog.Add( $"Their ally has a KO, using fake out may secure that KO. Score: {fakeOutScore}" );
                    }
                }

                if( attackExchangePack.OurAllyExists && attackExchangePack.ThreatAllyExists )
                {
                    if( attackExchangePack.AllyVS_ThreatAlly.OpponentPTKO >= PotentialToKO.Dangerous )
                    {
                        fakeOutScore += 1;
                        _tirLog.Add( $"Their ally has a likely KO on our ally, fake out may be incoming to our ally instead of us. Score: {fakeOutScore}" );
                    }
                }

                if( weHaveFullHPSave )
                {
                    fakeOutScore += 2;
                    _tirLog.Add( $"We have a full hp-based damage reduction/ko saving resource, fake out removes that from us immediately. Score: {fakeOutScore}" );
                }
            }

            if( theyHaveAlly )
            {
                //--Their Ally wants to be freely offensive
                if( theirAllyIsOffensive )
                {
                    if( theirAlly.Item == ItemBattleEffectID.ChoiceBand || theirAlly.Item == ItemBattleEffectID.ChoiceSpecs || theirAlly.Item == ItemBattleEffectID.ChoiceScarf || theirAlly.Item == ItemBattleEffectID.LifeOrb || ( theirAlly.Ability == AbilityID.Guts && theirAlly.SevereStatus != SevereConditionID.None ) )
                    {
                        fakeOutScore += 2;
                        _tirLog.Add( $"their ally is an offensive unit with high offensive item investment, fake out provides support. Score: {fakeOutScore}" );
                    }

                    if( theirAllyTraits.Contains( RoleTrait.SpreadAttack ) )
                    {
                        fakeOutScore += 1;
                        _tirLog.Add( $"their ally is an offensive unit with spread pressure, fake out provides support. Score: {fakeOutScore}" );
                    }
                }
            }

        }

        if( fakeOutScore >= 8 )
        {
            _tirLog.Add( $"" );
            _tirLog.Add( $"Fake Out score passed the threshold! Replacing their intended attack with Fake Out! Final Fake Out Score: {fakeOutScore}" );

            Move fakeOut = null;
            foreach( var move in attackTOP.Attacker.ActiveMoves )
            {
                if( move.MoveSO.Name == "Fake Out" )
                {
                    fakeOut = move;
                    break;
                }
            }

            if( fakeOut != null )
            {
                var attacker = attackTOP.Attacker;
                var target = attackTOP.Opponent;

                float modifier = _ai.UnitSim.Get_MoveEffectiveness( target, fakeOut ) * _ai.UnitSim.Get_MoveModifier( attacker, target, fakeOut );
                MoveThreatResult fakeOutMTR = new()
                {
                    Score = 0,
                    Modifier = modifier,
                    Move = fakeOut,
                    TargetCount = 1,
                    Targets = new() { attackTOP.Opponent },
                    Type = ActionResultType.Move,
                    ActionType = ActionType.Attack,
                };

                var attackerSimUnit         = _ai.UnitSim.BuildSimUnit( attacker, attacker.BeginningHPR, fakeOutMTR, _ai.Blackboard.CurrentFieldSnapshot );
                var targetSimUnit           = _ai.UnitSim.BuildSimUnit( target, target.BeginningHPR, target.MTR, _ai.Blackboard.CurrentFieldSnapshot );

                SimulatedUnit allySimUnit = null;
                SimulatedUnit targetAllySimUnit = null;

                MoveThreatResult allyMTR = null;
                MoveThreatResult targetAllyMTR = null;

                // if( ally != null )
                // {
                //     var allyTarget = targetAlly ?? target;
                //     allyMTR = GetMove_BestAttack( ally, allyTarget, false, "Ally best attack on current target" ); //--Will be replaced by CIR
                //     allySimUnit = _ai.UnitSim.BuildSimUnit( ally, ally.BeginningHPR, allyMTR, fieldSim );
                // }

                // if( targetAlly != null )
                // {
                //     var targetAllyTarget = ally ?? attacker;
                //     targetAllyMTR = GetMove_BestAttack( targetAlly, targetAllyTarget, false, "Target's Ally best attack on current target" ); //--Will be replaced by PIR
                //     targetAllySimUnit = _ai.UnitSim.BuildSimUnit( targetAlly, targetAlly.BeginningHPR, targetAllyMTR, fieldSim );
                // }

                List<SimulatedUnit> attackerTargets = _ai.BattleSim.GetTOPTargets( attackerSimUnit, targetSimUnit, allySimUnit, targetAllySimUnit, fakeOutMTR );
                List<SimulatedUnit> opponentTargets = _ai.BattleSim.GetTOPTargets( attackerSimUnit, targetSimUnit, allySimUnit, targetAllySimUnit, targetSimUnit.MTR );
                List<SimulatedUnit> allyTargets = allySimUnit != null ? _ai.BattleSim.GetTOPTargets( attackerSimUnit, targetSimUnit, allySimUnit, targetAllySimUnit, allyMTR ) : new();
                List<SimulatedUnit> opponentAllyTargets = targetAllySimUnit != null ? _ai.BattleSim.GetTOPTargets( attackerSimUnit, targetSimUnit, allySimUnit, targetAllySimUnit, targetAllyMTR ) : new();

                SimulationPackage attackerPack      = _ai.BattleSim.BuildSimPackage( attackerSimUnit, null, attackerTargets, SimModuleType.Attack );
                SimulationPackage targetPack        = _ai.BattleSim.BuildSimPackage( targetSimUnit, null, opponentTargets, SimModuleType.Attack );

                SimulationPackage attackerAllyPack  = allySimUnit != null ? _ai.BattleSim.BuildSimPackage( allySimUnit, null, allyTargets, SimModuleType.Attack ) : default;
                SimulationPackage targetAllyPack    = targetAllySimUnit != null ? _ai.BattleSim.BuildSimPackage( targetAllySimUnit, null, opponentAllyTargets, SimModuleType.Attack ) : default;

                var roundPack = _ai.BattleSim.BuildRoundPackage( attackerPack, attackerAllyPack, targetPack, targetAllyPack );
                var bse = _ai.BattleSim.BuildBattleSimEvent( roundPack, _ai.Blackboard.CurrentFieldSnapshot );

                fakeOutMTR.Top = _ai.BattleSim.RunSimulation( bse );                

                tic.MoveThreatResult = fakeOutMTR;
            }
        }

        _tirLog.Add( $"" );

        return evidence;
    }

    private int DefensiveSwitchEvidence( ThreatIntentCandidates tic, ExchangeEvaluation ee, BattlefieldState bfs, ThreatProfile theirTP, ThreatProfile ourTP, CurrentPlan theirCP )
    {
        int evidence = 0;

        //---------------------------------------------------------------------------------
        //--Defensive Switch Evidence Gathering--------------------------------------------
        //---------------------------------------------------------------------------------
        //--Switch simulations don't provide the current pokemon. "Attacker" in defSwitchTOP is the switch in candidate, since simulation only looks at
        //--the potential incoming damage on the candidate, it doesn't actually perform a "switch". in order to know whether we threaten the current mon enough to cause it
        //--to want to switch in either defensive or offensive switch evidence, we will use the attackTOP.
        _tirLog.Add( $"===[Defensive Switch Evidence]===" );
        if( tic.DefensiveSwitchCandidateResult.Pokemon != null )
        {
            var attackTOP = tic.MoveThreatResult.Top;
            var defSwitchTOP = tic.DefensiveSwitchCandidateResult.Top;

            var ourRP = attackTOP.Opponent.RoleProfile;
            var theirCurrentRP = attackTOP.Attacker.RoleProfile;

            var ourCourt = attackTOP.Opponent.CourtLocation == CourtLocation.TopCourt ? _ai.Blackboard.CurrentFieldSnapshot.TopCourtConditions : _ai.Blackboard.CurrentFieldSnapshot.BottomCourtConditions;
            var theirCourt = attackTOP.Attacker.CourtLocation == CourtLocation.TopCourt ? _ai.Blackboard.CurrentFieldSnapshot.TopCourtConditions : _ai.Blackboard.CurrentFieldSnapshot.BottomCourtConditions;

            var switchEE = _ai.Projection.EvaluateExchange( defSwitchTOP.Attacker, defSwitchTOP.Opponent );

            bool switchIsThreatenedByKO = defSwitchTOP.OpponentPTKO >= PotentialToKO.Dangerous;
            bool switchTakesBigDamage = defSwitchTOP.OpponentPTKO >= PotentialToKO.TwoHKO;

            bool currentPositionCollapsed = attackTOP.OpponentPTKO >= PotentialToKO.Dangerous && attackTOP.AttackerPTKO <= PotentialToKO.TwoHKO;

            int ourRemainingCount = _ai.Blackboard.OurTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList().Count;
            int theirRemainingCount = _ai.Blackboard.TheirTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList().Count;
            int materialDelta = theirRemainingCount - ourRemainingCount;

            bool weSetHazards               = ourRP.Traits.Contains( RoleTrait.HazardSetter );
            bool weHaveRecoveryMove         = ourRP.Traits.Contains( RoleTrait.RecoveryMove );
            bool weCanSetTailwind           = ourRP.Traits.Contains( RoleTrait.TailwindSetter );
            bool weHaveOffensiveSetup       = ourRP.Traits.Contains( RoleTrait.PhysicallyOffensiveSetup ) || ourRP.Traits.Contains( RoleTrait.SpeciallyOffensiveSetup );
            bool weHaveDefensiveSetup       = ourRP.Traits.Contains( RoleTrait.PhysicallyDefensiveSetup ) || ourRP.Traits.Contains( RoleTrait.SpeciallyDefensiveSetup );
            bool weHaveSetup                = weHaveOffensiveSetup || weHaveDefensiveSetup;

            bool ourRocks = attackTOP.Opponent.Pokemon.CheckHasActiveMove( "Stealth Rock" ) && ourCourt.ContainsKey( CourtConditionID.StealthRock );
            bool ourSpikes = attackTOP.Attacker.Pokemon.CheckHasActiveMove( "Spikes" ) && theirCourt.ContainsKey( CourtConditionID.Spikes );
            bool ourToxicSpikes = attackTOP.Attacker.Pokemon.CheckHasActiveMove( "Toxic Spikes" ) && theirCourt.ContainsKey( CourtConditionID.ToxicSpikes );
            bool ourStickyWeb = attackTOP.Opponent.Pokemon.CheckHasActiveMove( "Sticky Web" ) && ourCourt.ContainsKey( CourtConditionID.StickyWeb );
            bool ourLeechSeed = attackTOP.Opponent.Pokemon.CheckHasActiveMove( "Leech Seed" ) && ourCourt.ContainsKey( CourtConditionID.LeechSeed );
            bool ourHazardsExist = ourRocks || ourStickyWeb || ourLeechSeed;

            bool theyRemoveHazards          = theirCurrentRP.Traits.Contains( RoleTrait.HazardRemover );
            bool theySetHazards             = theirCurrentRP.Traits.Contains( RoleTrait.HazardSetter );
            bool theyAreACleric             = theirCurrentRP.Traits.Contains( RoleTrait.Cleric );
            bool theyAreAWeatherSetter      = theirCurrentRP.Traits.Contains( RoleTrait.WeatherSetter );
            bool theyAreATrickRoomSetter    = theirCurrentRP.Traits.Contains( RoleTrait.TrickRoomSetter );
            bool theyAreATailwindSetter     = theirCurrentRP.Traits.Contains( RoleTrait.TailwindSetter );
            bool theyAreScreensSetter       = theirCurrentRP.Traits.Contains( RoleTrait.ScreenSetter );
            bool theyAreAPivot              = theirCurrentRP.Traits.Contains( RoleTrait.FastPivot ) || theirCurrentRP.Traits.Contains( RoleTrait.SlowPivot );
            bool theyHaveInnateValue = theyRemoveHazards || theySetHazards || theyAreACleric || theyAreAWeatherSetter || theyAreATrickRoomSetter || theyAreATailwindSetter || theyAreAPivot;

            bool theirRocks = attackTOP.Attacker.Pokemon.CheckHasActiveMove( "Stealth Rock" ) && theirCourt.ContainsKey( CourtConditionID.StealthRock );
            bool theirSpikes = attackTOP.Attacker.Pokemon.CheckHasActiveMove( "Spikes" ) && theirCourt.ContainsKey( CourtConditionID.Spikes );
            bool theirToxicSpikes = attackTOP.Attacker.Pokemon.CheckHasActiveMove( "Toxic Spikes" ) && theirCourt.ContainsKey( CourtConditionID.ToxicSpikes );
            bool theirStickyWeb = attackTOP.Attacker.Pokemon.CheckHasActiveMove( "Sticky Web" ) && theirCourt.ContainsKey( CourtConditionID.StickyWeb );
            bool theirLeechSeed = attackTOP.Attacker.Pokemon.CheckHasActiveMove( "Leech Seed" ) && theirCourt.ContainsKey( CourtConditionID.LeechSeed );
            bool theirHazardsExist = theirRocks || theirSpikes || theirToxicSpikes || theirStickyWeb || theirLeechSeed;

            if( attackTOP.Attacker_DiesBeforeActing )
            {
                evidence += 2;
                _tirLog.Add( $"They die before acting. Defensive Switch Evidence: {evidence}" );
            }

            if( attackTOP.AttackerPTKO >= PotentialToKO.Dangerous && attackTOP.AttackerMovedFirst )
            {
                evidence -= 2;
                _tirLog.Add( $"They threaten us with a fast KO. Defensive Switch Evidence: {evidence}" );
            }

            if( attackTOP.Opponent.EndHPR <= 0.25f )
            {
                evidence -= 2;
                _tirLog.Add( $"We have pretty low HP, they might stay in against us. Defensive Switch Evidence: {evidence}" );
            }

            if( currentPositionCollapsed )
            {
                evidence += 2;
                _tirLog.Add( $"They lose the current exchange. Defensive Switch Evidence: {evidence}" );
            }

            if( attackTOP.OpponentPTKO > switchEE.OpponentPTKOR.PTKO + 1 )
            {
                evidence += 2;
                _tirLog.Add( $"Our PTKO on the switch candidate is much lower than their current mon. Defensive Switch Evidence: {evidence}" );
            }
            else if( attackTOP.OpponentPTKO > switchEE.OpponentPTKOR.PTKO )
            {
                evidence += 1;
                _tirLog.Add( $"Our PTKO on the switch candidate is lower than their current mon. Defensive Switch Evidence: {evidence}" );
            }

            if( attackTOP.Attacker.Expendability < 0.25f )
            {
                evidence += 1;
                _tirLog.Add( $"Their unit has low expendability ({attackTOP.Attacker.Expendability}). Defensive Switch Evidence: {evidence}" );
            }

            if( defSwitchTOP.OpponentPTKO == PotentialToKO.OHKO )
            {
                evidence -= 3;
                _tirLog.Add( $"Their candidate gets OHKOd on entry. Defensive Switch Evidence: {evidence}" );
            }
            else if( switchIsThreatenedByKO )
            {
                evidence -= 2;
                _tirLog.Add( $"Their candidate is likely to get KOd on entry. Defensive Switch Evidence: {evidence}" );
            }
            else if( switchTakesBigDamage )
            {
                evidence -= 1;
                _tirLog.Add( $"Their candidate takes a lot of damage on entry. Defensive Switch Evidence: {evidence}" );
            }
            else if( defSwitchTOP.OpponentPTKO <= PotentialToKO.Safe || defSwitchTOP.Attacker.RoleProfile.PrimaryRole == RoleClass.Wall )
            {
                evidence += 1;
                _tirLog.Add( $"We don't do much damage, or their candidate is a wall. Defensive Switch Evidence: {evidence}" );
            }

            var switchCandidate = defSwitchTOP.Attacker.Pokemon;
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
                int theirNewWeatherContext = _ai.UnitSim.Get_WeatherContextScore( defSwitchTOP.Opponent.Pokemon, candidatesWeather );

                if( myNewWeatherContext > theirNewWeatherContext )
                {
                    evidence += 1;
                    _tirLog.Add( $"Switch candidate can swing the weather in our favor! Defensive Switch Evidence: {evidence}" );
                }
                else
                {
                    evidence -= 1;
                    _tirLog.Add( $"Switch candidate changes weather in our opponent's favor!. Defensive Switch Evidence: {evidence}" );
                }
            }

            if( theirTP.ConstrainingPressure >= 4f )
            {
                evidence -= 2;
                _tirLog.Add( $"They have high constraining pressure on us, so they are likely to continue to stay in. Defensive Switch Evidence: {evidence}" );
            }

            if( theirTP.ForcesSwitch )
            {
                evidence -= 1;
                _tirLog.Add( $"They believe they force us to switch, so they are unlikely to switch as a result. Defensive Switch Evidence: {evidence}" );
            }

            if( theirTP.Type == ThreatType.Constraining && ( theirCurrentRP.Traits.Contains( RoleTrait.TrappingAbility ) || attackTOP.Opponent.Bindings.Count > 0 ) )
            {
                evidence -= 3;
                _tirLog.Add( $"They're constraining us with trapping pressure, switching removes the trap. Defensive Switch Evidence: {evidence}" );
            }

            if( theirTP.IsDecaying )
            {
                evidence -= 1;
                _tirLog.Add( $"Their unit naturally decays, they may be unlikely to preserve it. Defensive Switch Evidence: {evidence}" );
            }

            float hpAfterHazards = _ai.Get_HPRatio_AfterEntryHazards( attackTOP.Attacker );
            if( attackTOP.Attacker.BeginningHPR != hpAfterHazards && hpAfterHazards <= 0f )
            {
                evidence -= 1;
                _tirLog.Add( $"If they recall their unit, it will faint to hazards when they switch it in later if they don't clear them. Defensive Switch Evidence: {evidence}" );

                if( defSwitchTOP.Opponent.RoleProfile.Traits.Contains( RoleTrait.HazardRemover ) )
                {
                    evidence += 1;
                    _tirLog.Add( $"Our predicted switch candidate has hazard removal. Nulifying previous penalty. Defensive Switch Evidence: {evidence}" );
                }
                else
                {
                    evidence -= 1;
                    _tirLog.Add( $"Our predicted switch candidate doesn't have hazard removal either. Defensive Switch Evidence: {evidence}" );
                }
            }

            //--PBS
            bool switchRestoresStability = attackTOP.OpponentPTKO >= PotentialToKO.Dangerous && defSwitchTOP.OpponentPTKO < PotentialToKO.Risky;
            if( switchRestoresStability )
            {
                evidence += 1;
                _tirLog.Add( $"Switching restores stability to our board. Defensive Switch Evidence: {evidence}" );
            }

            if( attackTOP.OpponentPTKO > defSwitchTOP.OpponentPTKO )
            {
                evidence += 1;
                _tirLog.Add( $"Switching relieves pressure. Defensive Switch Evidence: {evidence}" );
            }
            else
            {
                evidence -= 1;
            }

            bool switchIsStableNextTurn = defSwitchTOP.Attacker_EndOfTurnHP > 0 && defSwitchTOP.OpponentPTKO < PotentialToKO.Risky;
            if( switchIsStableNextTurn )
            {
                evidence += 1;
                _tirLog.Add( $"Our switch candidate is likely to be stable next turn. Defensive Switch Evidence: {evidence}" );
            }

            //--Current Plan & GPA
            if( theirCP.Type == PlanType.Trade )
            {
                if( materialDelta > 0 )
                {
                    evidence -= 1;
                    _tirLog.Add( $"They hold a material advantage, switching prevents favorable trading. Defensive Switch Evidence: {evidence}" );
                }
            }

            //--Preservation Value
            if( theyHaveInnateValue )
            {
                evidence += 2;
                _tirLog.Add( $"Their unit has innate strategical value. Defensive Switch Evidence: {evidence}" );
            }

            if( theySetHazards && theirHazardsExist )
            {
                evidence += 2;
                _tirLog.Add( $"Their unit has already done its job, so it may look to evacuate. Defensive Switch Evidence: {evidence}" );
            }

            if( theyAreScreensSetter )
            {
                bool reflect = attackTOP.Attacker.Pokemon.CheckHasActiveMove( "Reflect" ) && theirCourt.ContainsKey( CourtConditionID.Reflect );
                bool lightScreen = attackTOP.Attacker.Pokemon.CheckHasActiveMove( "Light Screen" ) && theirCourt.ContainsKey( CourtConditionID.LightScreen );
                bool auroraVeil = attackTOP.Attacker.Pokemon.CheckHasActiveMove( "AuroraVeil" ) && theirCourt.ContainsKey( CourtConditionID.AuroraVeil );

                if( reflect || lightScreen || auroraVeil )
                {
                    evidence += 2;
                    _tirLog.Add( $"Their unit has already done its job, so it may look to evacuate. Defensive Switch Evidence: {evidence}" );
                }
            }

            if( theyAreAPivot )
            {
                evidence += 1;
                _tirLog.Add( $"Their unit is a pivot, switching may be its goal. Defensive Switch Evidence: {evidence}" );
            }

            if( attackTOP.Attacker.SevereStatus != SevereConditionID.None )
            {
                evidence += 1;
                _tirLog.Add( $"Their unit has a severe status, they may want to evacuate to preserve hp or prevent missed turns due to sleep and paralysis. Defensive Switch Evidence: {evidence}" );
            }

            if( attackTOP.Attacker.VolatileStatuses is var vs )
            {
                if( vs.Contains( VolatileConditionID.Taunt ) || vs.Contains( VolatileConditionID.Encore ) || vs.Contains( VolatileConditionID.ChoiceLocked ) || vs.Contains( VolatileConditionID.Disabled ) || vs.Contains( VolatileConditionID.Yawn ) || ( vs.Contains( VolatileConditionID.Perish ) && attackTOP.Attacker.Bindings?.Count <= 0 ) )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their unit is likely crippled by volatile statuses and probably wants to evacuate to remove them. Defensive Switch Evidence: {evidence}" );
                }
            }

            if( switchCandidate.AbilityID == AbilityID.Intimidate || switchCandidate.AbilityID == AbilityID.Demoralize || ( attackTOP.Attacker.Ability == AbilityID.Regenerator && attackTOP.Attacker.BeginningHPR <= 0.7f ) || switchCandidate.CheckHasActiveMove( "Fake Out" ) )
            {
                evidence += 1;
                _tirLog.Add( $"Their likely switch candidate has switch-in utility, or they have regenerator and want to regain some hp. Defensive Switch Evidence: {evidence}" );
            }

            if( ( weSetHazards && !ourHazardsExist ) || weHaveRecoveryMove && attackTOP.Opponent.BeginningHPR <= 0.6f || ( weCanSetTailwind && !ourCourt.ContainsKey( CourtConditionID.Tailwind ) ) || weHaveSetup )
            {
                evidence -= 2;
                _tirLog.Add( $"They may give us a free setup/field control turn if they switch now. Defensive Switch Evidence: {evidence}" );
            }

            //--Double Battles
            if( _ai.IsDoubleBattle )
            {
                _tirLog.Add( $"" );
                _tirLog.Add( $"This is a double battle! Checking defensive switch intent evidence in the context of doubles..." );

                var theirAlly = _ai.GetActiveAllyAs_Adapter( tic.Threat.Pokemon );
                bool theyHaveAlly = true;
                if( theirAlly == null )
                {
                    _tirLog.Add( $"They don't have an ally on the field, skipping!" );
                    _tirLog.Add( $"" );
                    theyHaveAlly = false;
                }

                _tirLog.Add( $"Their ({tic.Threat.Name}) ally is: {theirAlly.Name} ({theirAlly.RoleProfile.PrimaryRole})" );
                _tirLog.Add( $"" );

                //--Our Ally Information
                var ourAlly = _ai.GetActiveAllyAs_Adapter( _ai.CurrentUnitAdapter.Pokemon );
                bool weHaveAlly = ourAlly != null;
                if( weHaveAlly )
                {
                    _tirLog.Add( $"Our ({_ai.CurrentUnitAdapter.Name}) ally is: {ourAlly.Name} ({ourAlly.RoleProfile.PrimaryRole})" );
                    _tirLog.Add( $"" );
                }

                bool weCanProtect = _ai.UnitSim.CheckHasMove( _ai.CurrentUnitAdapter, "Protect" ) && _ai.GetBattleUnit( _ai.CurrentUnitAdapter.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;
                bool ourAllyCanProtect = weHaveAlly && _ai.UnitSim.CheckHasMove( ourAlly, "Protect" ) && _ai.GetBattleUnit( ourAlly.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;

                //--Their Ally Information
                RoleProfile theirAllyRP = theyHaveAlly ? theirAlly.RoleProfile : default;
                HashSet<RoleTrait> theirAllyTraits = theirAllyRP.Traits;

                bool theirAllyIsOffensive = theyHaveAlly && ( theirAllyRP.PrimaryRole == RoleClass.BulkyAttacker || theirAllyRP.PrimaryRole == RoleClass.RevengeKiller || theirAllyRP.PrimaryRole == RoleClass.SetupSweeper ||
                theirAllyRP.PrimaryRole == RoleClass.Sweeper || theirAllyRP.PrimaryRole == RoleClass.TrickRoomAbuser || theirAllyRP.PrimaryRole == RoleClass.WallBreaker );

                bool theirAllyIsDefensive = theyHaveAlly && ( theirAllyRP.PrimaryRole == RoleClass.Wall || theirAllyRP.PrimaryRole == RoleClass.DefensiveSetup || theirAllyRP.PrimaryRole == RoleClass.BulkyAttacker && theirAllyRP.SecondaryRoles.Contains( RoleClass.Wall ) );
                bool theirAllyIsSupport = theyHaveAlly && ( theirAllyRP.PrimaryRole == RoleClass.UtilitySupport || theirAllyRP.PrimaryRole == RoleClass.Disrupter );
                bool theirAllyIsUtility = theyHaveAlly && ( !theirAllyIsOffensive && !theirAllyIsDefensive );

                bool theyCanProtect = _ai.UnitSim.CheckHasMove( tic.Threat, "Protect" ) && _ai.GetBattleUnit( tic.Threat.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;
                bool theirAllyCanProtect = theyHaveAlly && _ai.UnitSim.CheckHasMove( theirAlly, "Protect" ) && _ai.GetBattleUnit( theirAlly.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;

                if( theirAllyCanProtect )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their ally can protect themselves while they switch - switch + protect is a common strategy. Defensive Switch Evidence: {evidence}" );
                }

                if( switchChangesWeather )
                {
                    bool theirAllyBenefitsWeather = _ai.UnitSim.Get_WeatherContextScore( theirAlly.Pokemon, candidatesWeather ) > 0;
                    evidence += 1;
                    _tirLog.Add( $"Their Defensive switch candidate changes weather to one their ally benefits from. Defensive Switch Evidence: {evidence}" );
                }

                if( theyHaveAlly && theirAllyTraits.Contains( RoleTrait.IntimidateSupport ) && ( ourRP.Biases.Contains( RoleBias.Physical) || weHaveAlly && ourAlly.RoleProfile.Biases.Contains( RoleBias.Physical ) ) )
                {
                    evidence += 1;
                }

                if( theyHaveAlly && theirAllyTraits.Contains( RoleTrait.DemoralizeSupport ) && ( ourRP.Biases.Contains( RoleBias.Special) || weHaveAlly && ourAlly.RoleProfile.Biases.Contains( RoleBias.Special ) ) )
                {
                    evidence += 1;
                }

                //--Their ally covers switch

                //--Their ally protects switch

                //--Their ally covers current mon

                //--Switch Candidate has pair synergy with their ally
            }

            //--Opposing Intents
            //--Attack inversion
            if( attackTOP.AttackerPTKO >= PotentialToKO.Dangerous && attackTOP.OpponentPTKO <= PotentialToKO.Risky )
            {
                evidence -= 1;
                _tirLog.Add( $"They still strongly threaten us while taking little return damage, so attacking may be better than switching. Defensive Switch Evidence: {evidence}" );
            }

            //--Setup pull, but this is still attack investment pulling from abandoning it
            int offensiveBoosts = 0;
            foreach( var sc in attackTOP.Attacker.StatStages )
            {
                if( ( sc.Key == Stat.Attack && theirCurrentRP.Biases.Contains( RoleBias.Physical ) ) || ( sc.Key == Stat.SpAttack && theirCurrentRP.Biases.Contains( RoleBias.Special ) ) )
                {
                    offensiveBoosts += sc.Value;
                }
            }

            if( offensiveBoosts >= 2 )
            {
                evidence -= 1;
                _tirLog.Add( $"They already possess offensive setup momentum and may prefer to capitalize on it instead of switching. Defensive Switch Evidence: {evidence}" );
            }

            //--Recovery vs Evacuating
            if( theirCurrentRP.Traits.Contains( RoleTrait.RecoveryMove ) && attackTOP.Attacker.BeginningHPR <= 0.55f && attackTOP.OpponentPTKO < PotentialToKO.Dangerous )
            {
                evidence -= 1;
                _tirLog.Add( $"They can likely stabilize with recovery instead of abandoning the position. Defensive Switch Evidence: {evidence}" );
            }

            //--Another attack pull
            if (theirCP.Type == PlanType.Aggress && theirCP.FocusMon == attackTOP.Opponent.Pokemon )
            {
                evidence -= 1;
                _tirLog.Add( $"Their current plan is aggressive toward us, making a defensive switch less appealing. Defensive Switch Evidence: {evidence}" );
            }
        }
        else
        {
            evidence = -99;
            _tirLog.Add( $"Defensive switch candidate not found! Nullifying evidence. Defensive Switch Evidence: {evidence}" );
        }

        _tirLog.Add( $"" );

        return evidence;
    }

    private int OffensiveSwitchEvidence( ThreatIntentCandidates tic, ExchangeEvaluation ee, BattlefieldState bfs, ThreatProfile theirTP, ThreatProfile ourTP, CurrentPlan theirCP )
    {
        int evidence = 0;

        //---------------------------------------------------------------------------------
        //--Offensive Switch Evidence Gathering--------------------------------------------
        //---------------------------------------------------------------------------------
        //--Switch simulations don't provide the current pokemon. "Attacker" in defSwitchTOP is the switch in candidate, since simulation only looks at
        //--the potential incoming damage on the candidate, it doesn't actually perform a "switch". in order to know whether we threaten the current mon enough to cause it
        //--to want to switch in either defensive or offensive switch evidence, we will use the attackTOP.
        _tirLog.Add( $"===[Offensive Switch Evidence]===" );
        if( tic.OffensiveSwitchCandidateResult.Pokemon != null )
        {
            var attackTOP = tic.MoveThreatResult.Top;
            var offSwitchTOP = tic.OffensiveSwitchCandidateResult.Top;
            var switchEE = _ai.Projection.EvaluateExchange( offSwitchTOP.Attacker, offSwitchTOP.Opponent );

            var ourRP = attackTOP.Opponent.RoleProfile;
            var theirCurrentRP = attackTOP.Attacker.RoleProfile;

            bool switchIsThreatenedByKO = offSwitchTOP.OpponentPTKO >= PotentialToKO.Dangerous;
            bool switchTakesBigDamage = offSwitchTOP.OpponentPTKO >= PotentialToKO.TwoHKO;

            bool switchThreatensKO = offSwitchTOP.AttackerPTKO >= PotentialToKO.Dangerous;
            bool switchThreatensBigDamage = offSwitchTOP.AttackerPTKO >= PotentialToKO.TwoHKO;

            bool currentExchangeLost = attackTOP.OpponentPTKO >= PotentialToKO.Dangerous && attackTOP.AttackerPTKO <= PotentialToKO.TwoHKO;
            bool losingOffensiveExchange = ee.OpponentThreatensKO && !ee.AttackerThreatensKO;
            int currentThreatcount = _ai.Blackboard.TheirTeamPieceValues.TryGetValue( tic.Threat.Pokemon, out var currentPieceValue ) ? currentPieceValue.ThreatCount : 0;
            int switchThreatCount = _ai.Blackboard.TheirTeamPieceValues.TryGetValue( offSwitchTOP.Attacker.Pokemon, out var switchPieceValue ) ? switchPieceValue.ThreatCount : 0;

            bool theyRemoveHazards          = theirCurrentRP.Traits.Contains( RoleTrait.HazardRemover );
            bool theySetHazards             = theirCurrentRP.Traits.Contains( RoleTrait.HazardSetter );
            bool theyAreACleric             = theirCurrentRP.Traits.Contains( RoleTrait.Cleric );
            bool theyAreAWeatherSetter      = theirCurrentRP.Traits.Contains( RoleTrait.WeatherSetter );
            bool theyAreATrickRoomSetter    = theirCurrentRP.Traits.Contains( RoleTrait.TrickRoomSetter );
            bool theyAreATailwindSetter     = theirCurrentRP.Traits.Contains( RoleTrait.TailwindSetter );
            bool theyAreAPivot              = theirCurrentRP.Traits.Contains( RoleTrait.FastPivot ) || theirCurrentRP.Traits.Contains( RoleTrait.SlowPivot );
            bool theyHaveInnateValue = theyRemoveHazards || theySetHazards || theyAreACleric || theyAreAWeatherSetter || theyAreATrickRoomSetter || theyAreATailwindSetter || theyAreAPivot;

            if( currentExchangeLost )
            {
                evidence += 1;
                _tirLog.Add( $"They lose the current exchange. Offensive Switch Evidence: {evidence}" );
            }

            if( losingOffensiveExchange )
            {
                evidence += 1;
                _tirLog.Add( $"They're behind in the current offensive exchange. Offensive Switch Evidence: {evidence}" );
            }

            if( switchThreatCount > currentThreatcount )
            {
                evidence += 1;
                _tirLog.Add( $"Their offensive switch candidate threatens more units than their current mon. Offensive Switch Evidence: {evidence}" );
            }

            if( attackTOP.AttackerPTKO <= PotentialToKO.Safe || attackTOP.AttackerPTKO < attackTOP.OpponentPTKO )
            {
                evidence += 1;
                _tirLog.Add( $"Their PTKO is very low, or their PTKO is lower than ours. Offensive Switch Evidence: {evidence}" );
            }

            if( switchThreatensKO && !switchIsThreatenedByKO )
            {
                evidence += 1;
                _tirLog.Add( $"Their switch candidate threatens a KO and they are not threatened by one on incoming. Offensive Switch Evidence: {evidence}" );

                if( offSwitchTOP.Attacker.Speed > offSwitchTOP.Opponent.Speed )
                {
                    evidence += 1;
                    _tirLog.Add( $"They're also faster in speed. Offensive Switch Evidence: {evidence}" );
                }
            }
            else if( switchThreatensBigDamage && !switchIsThreatenedByKO && !switchTakesBigDamage )
            {
                evidence += 1;
                _tirLog.Add( $"Their switch candidate threatens big damage and they are not threatened by one nor do they take a lot of damage on incoming. Offensive Switch Evidence: {evidence}" );

                if( offSwitchTOP.Attacker.Speed > offSwitchTOP.Opponent.Speed )
                {
                    evidence += 1;
                    _tirLog.Add( $"They're also faster in speed. Offensive Switch Evidence: {evidence}" );
                }
            }

            if( offSwitchTOP.OpponentPTKO == PotentialToKO.OHKO )
            {
                evidence -= 3;
                _tirLog.Add( $"Their switch candidate gets OHKOd on incoming. Offensive Switch Evidence: {evidence}" );
            }
            else if( switchIsThreatenedByKO )
            {
                evidence -= 2;
                _tirLog.Add( $"Their switch candidate is likely to get KOd on incoming. Offensive Switch Evidence: {evidence}" );
            }
            else if( switchTakesBigDamage )
            {
                evidence -= 1;
                _tirLog.Add( $"Their switch candidate takes a lot of damage on incoming. Offensive Switch Evidence: {evidence}" );
            }
            else if( offSwitchTOP.OpponentPTKO <= PotentialToKO.Safe || offSwitchTOP.Attacker.RoleProfile.PrimaryRole == RoleClass.WallBreaker )
            {
                evidence += 1;
                _tirLog.Add( $"Our PTKO is low or they are a wall breaker. Offensive Switch Evidence: {evidence}" );
            }
            else if( offSwitchTOP.Attacker.RoleProfile.PrimaryRole == RoleClass.Pivot || offSwitchTOP.Attacker.RoleProfile.PrimaryRole == RoleClass.RevengeKiller )
            {
                evidence += 1;
                _tirLog.Add( $"They are either a pivot or a revenge killer. Offensive Switch Evidence: {evidence}" );
            }

            if( offSwitchTOP.AttackerPTKO < ee.AttackerPTKOR.PTKO )
            {
                evidence -= 1;
                _tirLog.Add( $"Their current mon has a better PTKO on us than their switch candidate, attacking may be better for them. Offensive Switch Evidence: {evidence}" );

                if( offSwitchTOP.AttackerPTKO < ee.AttackerPTKO - 1 )
                {
                    evidence -= 1;
                    _tirLog.Add( $"Their current mon has a much better PTKO on us than their switch candidate, attacking may be better for them. Offensive Switch Evidence: {evidence}" );
                }
            }

            if( offSwitchTOP.AttackerPTKO >= PotentialToKO.Dangerous && switchEE.AttackerMovesFirst && offSwitchTOP.OpponentPTKO <= PotentialToKO.Risky )
            {
                evidence += 2;
                _tirLog.Add( $"Their switch candidate can come in safely AND has a likely ohko AND moves before our current mon does, effectively revenging. Offensive Switch Evidence: {evidence}" );
            }

            var switchCandidate = offSwitchTOP.Attacker.Pokemon;
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
                int theirNewWeatherContext = _ai.UnitSim.Get_WeatherContextScore( offSwitchTOP.Opponent.Pokemon, candidatesWeather );

                if( myNewWeatherContext > theirNewWeatherContext )
                {
                    evidence += 1;
                    _tirLog.Add( $"Switch candidate can swing the weather in our favor! Offensive Switch Evidence: {evidence}" );
                }
                else
                {
                    evidence -= 1;
                    _tirLog.Add( $"Switch candidate changes weather in our opponent's favor!. Offensive Switch Evidence: {evidence}" );
                }
            }

            if( theirTP.ConstrainingPressure >= 4f )
            {
                evidence -= 2;
                _tirLog.Add( $"They have high constraining pressure on us, so they are likely to continue to stay in. Offensive Switch Evidence: {evidence}" );
            }

            if( theirTP.ForcesSwitch )
            {
                evidence -= 1;
                _tirLog.Add( $"They believe they force us to switch, so they are unlikely to switch as a result. Offensive Switch Evidence: {evidence}" );
            }

            if( theirTP.Type == ThreatType.Constraining && ( theirCurrentRP.Traits.Contains( RoleTrait.TrappingAbility ) || attackTOP.Opponent.Bindings.Count > 0 ) )
            {
                evidence -= 3;
                _tirLog.Add( $"They're constraining us with trapping pressure, switching removes the trap. Offensive Switch Evidence: {evidence}" );
            }

            if( theirTP.EscalatingPressure >= 4f && theirTP.SweepPotential == false )
            {
                evidence += 1;
                _tirLog.Add( $"They have high escalating pressure but their current unit does not have sweep potential. Offensive Switch Evidence: {evidence}" );
            }

            if( theirTP.IsDecaying )
            {
                evidence -= 1;
                _tirLog.Add( $"Their unit naturally decays, they may be unlikely to preserve it. Offensive Switch Evidence: {evidence}" );
            }

            //--Pokemon dies to hazards check
            float hpAfterHazards = _ai.Get_HPRatio_AfterEntryHazards( attackTOP.Opponent );
            if( attackTOP.Opponent.BeginningHPR != hpAfterHazards && hpAfterHazards <= 0f )
            {
                evidence -= 2;
                _tirLog.Add( $"If they recall their unit, it will faint to hazards when they switch it in later if they don't clear them. Defensive Switch Evidence: {evidence}" );

                if( offSwitchTOP.Opponent.RoleProfile.Traits.Contains( RoleTrait.HazardRemover ) )
                {
                    evidence += 1;
                    _tirLog.Add( $"The switch candidate we predict they will switch into has hazard removal. Reducing previous penalty. Offensive Switch Evidence: {evidence}" );
                }
                else
                {
                    evidence -= 1;
                    _tirLog.Add( $"The switch candidate we predict they will switch into doesn't have hazard removal either. Offensive Switch Evidence: {evidence}" );
                }
            }

            //--PBS
            bool gainsControl = !attackTOP.AttackerMovedFirst && switchEE.OpponentMovesFirst;
            if( gainsControl )
            {
                evidence += 1;
                _tirLog.Add( $"Our switch candidate gains control next turn. Offensive Switch Evidence: {evidence}" );
            }

            if( offSwitchTOP.OpponentPTKO > attackTOP.OpponentPTKO )
            {
                evidence += 1;
                _tirLog.Add( $"Our switch candidate is more threatening than my current mon. Offensive Switch Evidence: {evidence}" );
            }

            //--Current Plan & GPA
            if( theirCP.Type == PlanType.EnableSweep )
            {
                if( offSwitchTOP.Attacker.Pokemon == theirCP.FocusMon )
                {
                    evidence += 1;
                    _tirLog.Add( $"They are looking to sweep and their offensive switch candidate is their intended sweeper. Offensive Switch Evidence: {evidence}" );
                }
            }

            if( _ai.Blackboard.GamePlan.OurBlockers.Contains( attackTOP.Opponent.Pokemon ) && switchEE.AttackerPTKO >= PotentialToKO.Risky && ( switchEE.AttackerMovesFirst || switchEE.OpponentPTKO < PotentialToKO.Risky ) )
            {
                evidence += 1;
                _tirLog.Add( $"Their switch candidate beats our current mon which is a gameplan blocker. Offensive Switch Evidence: {evidence}" );
            }

            //--Preservation Value
            if( theyHaveInnateValue )
            {
                evidence += 1;
                _tirLog.Add( $"Their unit has innate strategical value. Offensive Switch Evidence: {evidence}" );
            }

            //--Momentum Recovery
            foreach( var sc in tic.Threat.StatStages )
            {
                if( sc.Key == Stat.Attack && theirCurrentRP.Biases.Contains( RoleBias.Physical ) || sc.Key == Stat.SpAttack && theirCurrentRP.Biases.Contains( RoleBias.Special ) )
                {
                    if( sc.Value < -1 )
                    {
                        evidence += 2;
                        _tirLog.Add( $"Their current unit has lost offensive power due to several lowered offensively aligned stat stages. Offensive Switch Evidence: {evidence}" );
                    }
                    else if( sc.Value < 0 )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their current unit has lost offensive power due to a lowered offensively aligned stat stage. Offensive Switch Evidence: {evidence}" );
                    }
                }

                if( sc.Key == Stat.Defense || sc.Key == Stat.SpDefense )
                {
                    if( sc.Value < -1 )
                    {
                        evidence += 2;
                        _tirLog.Add( $"Their current unit has lost survivability due to several lowered defensive stat stages. Offensive Switch Evidence: {evidence}" );
                    }
                    else if( sc.Value < 0 )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their current unit has lost survivability due to a lowered defensive stat stage. Offensive Switch Evidence: {evidence}" );
                    }
                }
            }

            if( attackTOP.Attacker.VolatileStatuses is var vs )
            {
                if( vs.Contains( VolatileConditionID.Encore ) || vs.Contains( VolatileConditionID.ChoiceLocked ) || vs.Contains( VolatileConditionID.Disabled ) || vs.Contains( VolatileConditionID.Yawn ) || ( vs.Contains( VolatileConditionID.Perish ) && attackTOP.Attacker.Bindings?.Count <= 0 ) )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their unit is likely crippled by volatile statuses and probably wants to evacuate to remove them. Offensive Switch Evidence: {evidence}" );
                }
            }

            //--Match up Evaluation stuff can go here!
            //--type match ups, role match ups, item match us, etc. etc.

            //--Double Battles
            if( _ai.IsDoubleBattle )
            {
                _tirLog.Add( $"" );
                _tirLog.Add( $"This is a double battle! Checking Offensive Switch intent evidence in the context of doubles..." );

                var theirAlly = _ai.GetActiveAllyAs_Adapter( tic.Threat.Pokemon );
                bool theyHaveAlly = true;
                if( theirAlly == null )
                {
                    _tirLog.Add( $"They don't have an ally on the field, skipping!" );
                    _tirLog.Add( $"" );
                    theyHaveAlly = false;
                    return evidence;
                }

                _tirLog.Add( $"Their ({tic.Threat.Name}) ally is: {theirAlly.Name} ({theirAlly.RoleProfile.PrimaryRole})" );
                _tirLog.Add( $"" );

                //--Our Ally Information
                var ourAlly = _ai.GetActiveAllyAs_Adapter( _ai.CurrentUnitAdapter.Pokemon );
                bool weHaveAlly = ourAlly != null;
                if( weHaveAlly )
                {
                    _tirLog.Add( $"Our ({_ai.CurrentUnitAdapter.Name}) ally is: {ourAlly.Name} ({ourAlly.RoleProfile.PrimaryRole})" );
                    _tirLog.Add( $"" );
                }

                bool weCanProtect = _ai.UnitSim.CheckHasMove( _ai.CurrentUnitAdapter, "Protect" ) && _ai.GetBattleUnit( _ai.CurrentUnitAdapter.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;
                bool ourAllyCanProtect = weHaveAlly && _ai.UnitSim.CheckHasMove( ourAlly, "Protect" ) && _ai.GetBattleUnit( ourAlly.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;

                //--Their Ally Information
                var theirAllyRP = theirAlly.RoleProfile;
                var theirAllyTraits = theirAllyRP.Traits;

                bool theirAllyIsOffensive = theirAllyRP.PrimaryRole == RoleClass.BulkyAttacker || theirAllyRP.PrimaryRole == RoleClass.RevengeKiller || theirAllyRP.PrimaryRole == RoleClass.SetupSweeper ||
                theirAllyRP.PrimaryRole == RoleClass.Sweeper || theirAllyRP.PrimaryRole == RoleClass.TrickRoomAbuser || theirAllyRP.PrimaryRole == RoleClass.WallBreaker;

                bool theirAllyIsDefensive = theirAllyRP.PrimaryRole == RoleClass.Wall || theirAllyRP.PrimaryRole == RoleClass.DefensiveSetup || theirAllyRP.PrimaryRole == RoleClass.BulkyAttacker && theirAllyRP.SecondaryRoles.Contains( RoleClass.Wall );
                bool theirAllyIsSupport = theirAllyRP.PrimaryRole == RoleClass.UtilitySupport || theirAllyRP.PrimaryRole == RoleClass.Disrupter;
                bool theirAllyIsUtility = !theirAllyIsOffensive && !theirAllyIsDefensive;

                bool theyCanProtect = _ai.UnitSim.CheckHasMove( tic.Threat, "Protect" ) && _ai.GetBattleUnit( tic.Threat.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;
                bool theirAllyCanProtect = theyHaveAlly && _ai.UnitSim.CheckHasMove( theirAlly, "Protect" ) && _ai.GetBattleUnit( theirAlly.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;

                if( theirAllyCanProtect )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their ally can protect themselves while they switch - switch + protect is a common strategy. Offensive Switch Evidence: {evidence}" );
                }

                if( switchChangesWeather )
                {
                    bool theirAllyBenefitsWeather = _ai.UnitSim.Get_WeatherContextScore( theirAlly.Pokemon, candidatesWeather ) > 0;

                    if( theirAlly != null && theirAllyBenefitsWeather )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their Offensive Switch candidate changes weather to one their ally benefits from. Offensive Switch Evidence: {evidence}" );
                    }
                }

                if( theirAllyTraits.Contains( RoleTrait.IntimidateSupport ) && ( ourRP.Biases.Contains( RoleBias.Physical) || weHaveAlly && ourAlly.RoleProfile.Biases.Contains( RoleBias.Physical ) ) )
                {
                    evidence += 1;
                }

                if( theirAllyTraits.Contains( RoleTrait.DemoralizeSupport ) && ( ourRP.Biases.Contains( RoleBias.Special) || weHaveAlly && ourAlly.RoleProfile.Biases.Contains( RoleBias.Special ) ) )
                {
                    evidence += 1;
                }
            }

            //--Opposing Intents
            //--Attack because our current unit is actually sufficient.
            if( attackTOP.AttackerPTKO >= PotentialToKO.Dangerous && attackTOP.OpponentPTKO <= PotentialToKO.Risky )
            {
                evidence -= 2;
                _tirLog.Add( $"Their current Pokemon already has a strong offensive position, they likely don't need to improve it with an offensive switch. Offensive Switch Evidence: {evidence}" );
            }

            //--Setup pull, but this is still attack investment pulling from abandoning it
            int offensiveBoosts = 0;
            foreach( var sc in attackTOP.Attacker.StatStages )
            {
                if( ( sc.Key == Stat.Attack && theirCurrentRP.Biases.Contains( RoleBias.Physical ) ) || ( sc.Key == Stat.SpAttack && theirCurrentRP.Biases.Contains( RoleBias.Special ) ) )
                {
                    offensiveBoosts += sc.Value;
                }
            }

            if( offensiveBoosts >= 2 )
            {
                evidence -= 1;
                _tirLog.Add( $"They already possess offensive setup momentum and may prefer to capitalize on it instead of switching. Offensive Switch Evidence: {evidence}" );
            }

            //--Offensive Status vs Offensive Switch
            if( tic.OffensiveStatusThreatResult.Move != null && ( theirCurrentRP.PrimaryRole == RoleClass.Disrupter || theirCurrentRP.PrimaryRole == RoleClass.UtilitySupport ) )
            {
                if( attackTOP.OpponentPTKO < PotentialToKO.Dangerous )
                {
                    evidence -= 1;
                    _tirLog.Add( $"Their current Pokemon still has disruptive utility available, they may want to offensive alter the board with status instead of switching. Offensive Switch Evidence: {evidence}" );
                }
            }
        }
        else
        {
            evidence = -99;
            _tirLog.Add( $"Offensive switch candidate not found! Nullifying evidence. Offensive Switch Evidence: {evidence}" );
        }
        
        _tirLog.Add( $"" );

        return evidence;
    }

    private int SetupEvidence( ThreatIntentCandidates tic, ExchangeEvaluation ee, BattlefieldState bfs, ThreatProfile theirTP, ThreatProfile ourTP, CurrentPlan theirCP )
    {
        int evidence = 0;

        //---------------------------------------------------------------------------------
        //--Setup Evidence Gathering-------------------------------------------------------
        //---------------------------------------------------------------------------------
        _tirLog.Add( $"===[Setup Evidence]===" );
        if( tic.SetupThreatResult.Move != null )
        {
            var setup = tic.SetupThreatResult;
            var setupTOP = tic.SetupThreatResult.Top;
            var ourRP = setupTOP.Opponent.RoleProfile;
            var ourTraits = ourRP.Traits;
            var setupRP = setupTOP.Attacker.RoleProfile;
            var theirTraits = setupRP.Traits;
            var setupEE = _ai.Projection.EvaluateExchange( setupTOP.Attacker, setupTOP.Opponent );

            bool offensiveSetup = setupRP.Traits.Contains( RoleTrait.SpeciallyOffensiveSetup ) || setupRP.Traits.Contains( RoleTrait.PhysicallyOffensiveSetup );
            bool defensiveSetup = setupRP.Traits.Contains( RoleTrait.SpeciallyDefensiveSetup ) || setupRP.Traits.Contains( RoleTrait.PhysicallyDefensiveSetup );
            bool hasSetup = offensiveSetup || defensiveSetup;

            bool freeTurn = setupTOP.OpponentPTKO <= PotentialToKO.Safe;

            int ourRemainingCount = _ai.Blackboard.OurTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList().Count;
            int theirRemainingCount = _ai.Blackboard.TheirTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList().Count;
            int materialDelta = theirRemainingCount - ourRemainingCount;

            bool weHaveTaunt = ourTraits.Contains( RoleTrait.Taunt );
            bool weHaveEncore = ourTraits.Contains( RoleTrait.Encore );
            bool weHaveReset = ourTraits.Contains( RoleTrait.Haze );
            bool weHavePhaze = ourTraits.Contains( RoleTrait.Phazes );

            if( hasSetup && freeTurn )
            {
                evidence += 2;
                _tirLog.Add( $"They have a setup move and a free turn. Setup Evidence: {evidence}" );
            }

            if( setupRP.PrimaryRole == RoleClass.SetupSweeper || setupRP.PrimaryRole == RoleClass.DefensiveSetup )
            {
                evidence += 1;
                _tirLog.Add( $"They are an offensive or defensive setup role. Setup Evidence: {evidence}" );
            }

            if( setupTOP.OpponentPTKO <= PotentialToKO.Risky && tic.SetupThreatResult.AfterPTKOR.PTKO > PotentialToKO.Risky )
            {
                evidence += 1;
                _tirLog.Add( $"We don't threaten them with a KO and they threaten us with one after setting up. Setup Evidence: {evidence}" );
            }

            if( setupTOP.AttackerPTKO >= PotentialToKO.Dangerous )
            {
                evidence -= 3;
                _tirLog.Add( $"We threaten to KO them if they decide to setup this turn. Setup Evidence: {evidence}" );
            }
            else
            {
                bool setupSweeperRole = setupRP.PrimaryRole == RoleClass.SetupSweeper || setupRP.SecondaryRoles.Contains( RoleClass.SetupSweeper );
                if( ( offensiveSetup && setupSweeperRole ) || ( defensiveSetup && setupTOP.OpponentPTKO >= PotentialToKO.TwoHKO ) )
                {
                    evidence += 1;
                    _tirLog.Add( $"They have an offensive setup move and a low PTKO, or a defensive setup move and we have a medicore or better PTKO on them. Setup Evidence: {evidence}" );
                }
            }

            if( ee.AttackerThreatensKO && ee.AttackerMovesFirst )
            {
                evidence -= 3;
                _tirLog.Add( $"They threaten a KO on us and move first. Setup Evidence: {evidence}" );
            }
            else if( ee.AttackerThreatensKO )
            {
                evidence -= 2;
                _tirLog.Add( $"They threaten a KO on us. Setup Evidence: {evidence}" );
            }

            if( ee.OpponentSwitchProbability >= 0.75f )
            {
                evidence += 1;
                _tirLog.Add( $"We have a high switch probability ({ee.OpponentSwitchProbability}). Setup Evidence: {evidence}" );
            }

            var theirCourt = setupTOP.Attacker.CourtLocation == CourtLocation.TopCourt ? setupTOP.Field.TopCourtConditions : setupTOP.Field.BottomCourtConditions;
            bool reflect = theirCourt.ContainsKey( CourtConditionID.Reflect );
            bool lightScreen = theirCourt.ContainsKey( CourtConditionID.LightScreen );
            bool aurora = theirCourt.ContainsKey( CourtConditionID.AuroraVeil );
            bool tailwind = theirCourt.TryGetValue( CourtConditionID.Tailwind, out var duration ) && duration >= 2;

            if( reflect || lightScreen )
            {
                evidence += 1;
                _tirLog.Add( $"They have reflect or light screen up. Setup Evidence: {evidence}" );
            }

            if( aurora )
            {
                evidence += 2;
                _tirLog.Add( $"They have aurora veil up. Setup Evidence: {evidence}" );
            }

            if( tailwind )
            {
                evidence += 1;
                _tirLog.Add( $"They have tailwind up. Setup Evidence: {evidence}" );
            }

            if( weHaveTaunt )
            {
                evidence -= 2;
                _tirLog.Add( $"We have Taunt, which should disincentivize them from setting up. Setup Evidence: {evidence}" );

                if( setupTOP.OpponentMovedFirst )
                {   
                    evidence += 1;
                    _tirLog.Add( $"We also move first. Setup Evidence: {evidence}" );
                }
            }

            if( weHaveEncore )
            {
                evidence -= 2;
                _tirLog.Add( $"We have Encore, which should disincentivize them from setting up. Setup Evidence: {evidence}" );

                if( setupTOP.OpponentMovedFirst )
                {   
                    evidence += 1;
                    _tirLog.Add( $"We also move first. Setup Evidence: {evidence}" );
                }
            }

            if( weHaveReset )
            {
                evidence -= 2;
                _tirLog.Add( $"We have a stat reset move, which should disincentivize them from setting up. Setup Evidence: {evidence}" );

                if( setupTOP.OpponentMovedFirst )
                {   
                    evidence += 1;
                    _tirLog.Add( $"We also move first. Setup Evidence: {evidence}" );
                }
            }

            if( weHavePhaze )
            {
                evidence -= 2;
                _tirLog.Add( $"We have a phazing move, which should disincentivize them from setting up. Setup Evidence: {evidence}" );

                if( setupTOP.OpponentMovedFirst )
                {   
                    evidence += 1;
                    _tirLog.Add( $"We also move first. Setup Evidence: {evidence}" );
                }
            }

            //--Replace lum berry check with a new "status prevention berry" + add specific status prevention berries traits
            if( setupTOP.Attacker.Item != ItemBattleEffectID.LumBerry && ourTraits.Contains( RoleTrait.BurnPressure ) && !theirTraits.Contains( RoleTrait.BurnImmune ) && setupTOP.Attacker.SevereStatus == SevereConditionID.None && ( setupRP.Biases.Contains( RoleBias.Physical ) || theirTraits.Contains( RoleTrait.PhysicallyOffensiveSetup ) ) )
            {
                evidence -= 1;
                _tirLog.Add( $"We have the potential to burn them and they are a physical setup mon. Setup Evidence: {evidence}" );
            }

            if( setupTOP.Attacker.Item != ItemBattleEffectID.LumBerry && ourTraits.Contains( RoleTrait.FrostbitePressure ) && !theirTraits.Contains( RoleTrait.FrostImmune ) && setupTOP.Attacker.SevereStatus == SevereConditionID.None && ( setupRP.Biases.Contains( RoleBias.Special ) || theirTraits.Contains( RoleTrait.SpeciallyOffensiveSetup ) ) )
            {
                evidence -= 1;
                _tirLog.Add( $"We have the potential to frostbite them and they are a special setup mon. Setup Evidence: {evidence}" );
            }


            var statStages = setupTOP.Attacker.StatStages;
            bool setupOffensivelyAlready = false;
            bool setupDefensivelyAlready = false;
            int offensiveBoosts = 0;
            int defensiveBoosts = 0;
            int speedBoosts = 0;
            foreach( var sc in statStages )
            {
                if( sc.Value > 0 )
                {
                    if( ( sc.Key == Stat.Attack || sc.Key == Stat.SpAttack ) && offensiveSetup )
                    {
                        setupOffensivelyAlready = true;
                        offensiveBoosts += sc.Value;
                    }

                    if( ( sc.Key == Stat.Defense || sc.Key == Stat.SpDefense ) && defensiveSetup  )
                    {
                        setupDefensivelyAlready = true;
                        defensiveBoosts += sc.Value;
                    }

                    if( sc.Key == Stat.Speed )
                    {
                        speedBoosts += sc.Value;
                    }
                }
            }

            if( setupOffensivelyAlready || setupDefensivelyAlready )
            {
                evidence -= 2;
                _tirLog.Add( $"They've already set up, so they may not do so again. Setup Evidence: {evidence}" );
            }

            if( offensiveBoosts >= 2 || defensiveBoosts >= 2 )
            {
                evidence -= 5;

                if( speedBoosts >= 1 )
                {
                    evidence -= 2;
                }
            }

            var ourRemaining = _ai.Blackboard.OurTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList();
            int fasterBonus = 0;
            bool theyKO = setupEE.AttackerPTKOR.PTKO >= PotentialToKO.Dangerous && ( setupEE.AttackerMovesFirst || setupEE.OpponentPTKOR.PTKO <= PotentialToKO.Risky );
            bool theyForceSwitchNextTurn = setupEE.OpponentSwitchProbability >= 0.85f;
            bool sweepBeginning = theyKO || theyForceSwitchNextTurn;

            if( sweepBeginning )
            {
                foreach( var opp in ourRemaining )
                {
                    if( setupEE.AttackerMovesFirst )
                        fasterBonus++;
                }

                if( fasterBonus > ourRemaining.Count / 2 )
                {
                    evidence += 2;
                    _tirLog.Add( $"Setting up gives them a sweep horizon where they're faster than at least half of our remaining pokemon. Setup Evidence: {evidence}" );
                }
                else
                {
                    evidence += 1;
                    _tirLog.Add( $"Setting up gives them a sweep horizon. Setup Evidence: {evidence}" );
                }
            }

            if( setup.ImprovedPTKOs >= 2 )
            {
                evidence += 1;
                _tirLog.Add( $"Setting up gives them multiple improved PTKOs across our team. Setup Evidence: {evidence}" );

                if( setup.SweepCount > 2 )
                {
                    evidence += 1;
                    _tirLog.Add( $"Setting up gives them a sweep count of more than 2 units. Setup Evidence: {evidence}" );
                }
            }

            if( theirTP.Type == ThreatType.Escalating && ( ( offensiveSetup && !setupOffensivelyAlready ) || ( defensiveSetup && !setupDefensivelyAlready ) ) )
            {
                evidence += 1;
                _tirLog.Add( $"They are an escalating threat. Setup Evidence: {evidence}" );
            }

            if( ourTP.ImmediatePressure >= 4f )
            {
                evidence -= 2;
                _tirLog.Add( $"We exert high immediate pressure on them, which may be too volatile for them to try setting up. Setup Evidence: {evidence}" );
            }

            if( theirTP.ConstrainingPressure >= 5f )
            {
                evidence += 1;
                _tirLog.Add( $"They have very high constraining pressure, they may feel safe setting up. Setup Evidence: {evidence}" );

                if( ( offensiveSetup && !setupOffensivelyAlready ) || ( defensiveSetup && !setupDefensivelyAlready ) )
                {
                    evidence += 1;
                    _tirLog.Add( $"They also are a setup oriented mon and haven't done so yet. Setup Evidence: {evidence}" );
                }
            }

            //--PBS
            if( ( setupEE.OpponentPTKOR.PTKO > setupTOP.OpponentPTKO ) || ( setupEE.AttackerPTKOR.PTKO < setupTOP.AttackerPTKO) )
            {
                evidence += 2;
                _tirLog.Add( $"Setting up directly improves our PTKO on them or reduces their PTKO on us. Setup Evidence: {evidence}" );
            }

            if( ee.AttackerMovesFirst && setupEE.OpponentMovesFirst )
            {
                evidence += 1;
                _tirLog.Add( $"Setting up allows us to move first next turn. Setup Evidence: {evidence}" );
            }

            if( setupTOP.Opponent_EndOfTurnHP > 0.55f )
            {
                evidence += 1;
                _tirLog.Add( $"We comfortably survive setting up next turn. Setup Evidence: {evidence}" );
            }

            if( setupTOP.Attacker.BeginningHPR <= 0.6f && setupTOP.Attacker.RoleProfile.Traits.Contains( RoleTrait.RecoveryMove ) )
            {
                if( setupTOP.OpponentPTKO >= PotentialToKO.Risky )
                {
                    evidence -= 3;
                    _tirLog.Add( $"They're below 60% hp and have a recovery move available, they may want to click that instead. Setup Evidence: {evidence}" );
                }
                else
                {
                    evidence += 1;
                    _tirLog.Add( $"They're a little low on hp and they have a recovery move available, but we don't really threaten them at the moment. Setup Evidence: {evidence}" );
                }
            }

            //--Current Plan & GPA
            if( theirCP.Type == PlanType.Trade )
            {
                if( materialDelta > 0 )
                {
                    evidence -= 1;
                    _tirLog.Add( $"They hold a material advantage, setting up introduces potentially unnecessary risk. Setup Evidence: {evidence}" );
                }
            }

            if( theirCP.Type == PlanType.EnableSweep )
            {
                evidence += 1;
                _tirLog.Add( $"Their plan is to enable a sweep. Setup Evidence: {evidence}" );

                if( setupTOP.Attacker.Pokemon == _ai.Blackboard.GamePlan.TheirPrimaryWinCon )
                {
                    evidence += 2;
                    _tirLog.Add( $"The current Pokemon is their primary win con. Setup Evidence: {evidence}" );
                }

                if( setupTOP.Attacker.Pokemon == theirCP.FocusMon )
                {
                    evidence += 1;
                    _tirLog.Add( $"The current Pokemon is their plan's focus mon. Setup Evidence: {evidence}" );
                }
            }

            if( _ai.IsDoubleBattle )
            {
                _tirLog.Add( $"" );
                _tirLog.Add( $"This is a double battle! Checking Setup intent evidence in the context of doubles..." );

                var theirAlly = _ai.GetActiveAllyAs_Adapter( tic.Threat.Pokemon );
                bool theyHaveAlly = true;
                if( theirAlly == null )
                {
                    _tirLog.Add( $"They don't have an ally on the field, skipping!" );
                    _tirLog.Add( $"" );
                    theyHaveAlly = false;
                    return evidence;
                }

                _tirLog.Add( $"Their ({tic.Threat.Name}) ally is: {theirAlly.Name} ({theirAlly.RoleProfile.PrimaryRole})" );
                _tirLog.Add( $"" );

                //--Our Ally Information
                var ourAlly = _ai.GetActiveAllyAs_Adapter( _ai.CurrentUnitAdapter.Pokemon );
                bool weHaveAlly = ourAlly != null;
                if( weHaveAlly )
                {
                    _tirLog.Add( $"Our ({_ai.CurrentUnitAdapter.Name}) ally is: {ourAlly.Name} ({ourAlly.RoleProfile.PrimaryRole})" );
                    _tirLog.Add( $"" );
                }

                bool weCanProtect = _ai.UnitSim.CheckHasMove( _ai.CurrentUnitAdapter, "Protect" ) && _ai.GetBattleUnit( _ai.CurrentUnitAdapter.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;
                bool ourAllyCanProtect = weHaveAlly && _ai.UnitSim.CheckHasMove( ourAlly, "Protect" ) && _ai.GetBattleUnit( ourAlly.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;

                //--Their Ally Information
                var theirAllyRP = theirAlly.RoleProfile;
                var theirAllyTraits = theirAllyRP.Traits;

                bool theirAllyIsOffensive = theirAllyRP.PrimaryRole == RoleClass.BulkyAttacker || theirAllyRP.PrimaryRole == RoleClass.RevengeKiller || theirAllyRP.PrimaryRole == RoleClass.SetupSweeper ||
                theirAllyRP.PrimaryRole == RoleClass.Sweeper || theirAllyRP.PrimaryRole == RoleClass.TrickRoomAbuser || theirAllyRP.PrimaryRole == RoleClass.WallBreaker;

                bool theirAllyIsDefensive = theirAllyRP.PrimaryRole == RoleClass.Wall || theirAllyRP.PrimaryRole == RoleClass.DefensiveSetup || theirAllyRP.PrimaryRole == RoleClass.BulkyAttacker && theirAllyRP.SecondaryRoles.Contains( RoleClass.Wall );
                bool theirAllyIsSupport = theirAllyRP.PrimaryRole == RoleClass.UtilitySupport || theirAllyRP.PrimaryRole == RoleClass.Disrupter;
                bool theirAllyIsUtility = !theirAllyIsOffensive && !theirAllyIsDefensive;

                bool theyCanProtect = _ai.UnitSim.CheckHasMove( tic.Threat, "Protect" ) && _ai.GetBattleUnit( tic.Threat.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;
                bool theirAllyCanProtect = theyHaveAlly && _ai.UnitSim.CheckHasMove( theirAlly, "Protect" ) && _ai.GetBattleUnit( theirAlly.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;

                bool theirAllyHas_FakeOut       = _ai.CanUseFakeOut( theirAlly, _ai.CurrentUnitAdapter );
                bool theirAllyHas_FollowMe      = _ai.UnitSim.CheckHasMove( theirAlly, "Follow Me" );
                bool theirAllyHas_RagePowder    = _ai.UnitSim.CheckHasMove( theirAlly, "Rage Powder" );
                bool theirAllyHas_WideGuard     = _ai.UnitSim.CheckHasMove( theirAlly, "Wide Guard" );
                bool theirAllyHas_QuickGuard    = _ai.UnitSim.CheckHasMove( theirAlly, "Quick Guard" );
                bool theirAllyHas_Taunt         = theirAllyTraits.Contains( RoleTrait.Taunt );
                bool theirAllyHas_Encore        = theirAllyTraits.Contains( RoleTrait.Encore );

                if( theirAllyHas_FakeOut )
                {
                    evidence += 2;
                    _tirLog.Add( $"Their ally has FakeOut. Setup Evidence: {evidence}" );
                }

                if( theirAllyHas_FollowMe )
                {
                    evidence += 2;
                    _tirLog.Add( $"Their ally has FollowMe. Setup Evidence: {evidence}" );
                }

                if( theirAllyHas_RagePowder )
                {
                    evidence += 2;
                    _tirLog.Add( $"Their ally has RagePowder. Setup Evidence: {evidence}" );
                }

                if( theirAllyHas_WideGuard )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their ally has WideGuard. Setup Evidence: {evidence}" );
                }

                if( theirAllyHas_QuickGuard )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their ally has QuickGuard. Setup Evidence: {evidence}" );
                }

            }

            //--Opposing Intents
            //--Recovery looks good!
            bool recoveryLooksGood = setupTOP.Attacker.BeginningHPR <= 0.45f && !ee.AttackerThreatensKO && !ee.OpponentThreatensKO && setupTOP.Attacker.RoleProfile.Traits.Contains( RoleTrait.RecoveryMove );
            if( recoveryLooksGood )
            {
                evidence -= 2;
                _tirLog.Add( $"Recovery is looking like a better alternative to setting up right now. Setup Evidence: {evidence}" );
            }
        }
        else
        {
            evidence = -99;
            _tirLog.Add( $"Setup candidate not found! Nullifying evidence. Setup Evidence: {evidence}" );
        }

        _tirLog.Add( $"" );

        return evidence;
    }

    private int OffensiveStatusEvidence( ThreatIntentCandidates tic, ExchangeEvaluation ee, BattlefieldState bfs, ThreatProfile theirTP, ThreatProfile ourTP, CurrentPlan theirCP )
    {
        int evidence = 0;

        //---------------------------------------------------------------------------------
        //--Offensive Status Evidence Gathering--------------------------------------------
        //---------------------------------------------------------------------------------
        _tirLog.Add( $"===[Offensive Status Evidence]===" );
        if( tic.OffensiveStatusThreatResult.Move != null )
        {
            var offStatusTOP = tic.OffensiveStatusThreatResult.Top;
            var statusEE = _ai.Projection.EvaluateExchange( offStatusTOP.Attacker, offStatusTOP.Opponent );
            var theirRP = offStatusTOP.Attacker.RoleProfile;
            
            var ourBiases = offStatusTOP.Opponent.RoleProfile.Biases;

            var ourTraits = offStatusTOP.Opponent.RoleProfile.Traits;
            var theirTraits = offStatusTOP.Attacker.RoleProfile.Traits;

            var ourRemaining = _ai.Blackboard.OurTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList();
            var theirRemaining = _ai.Blackboard.TheirTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList();
            float ourRemainingCount = ourRemaining.Count;
            float theirRemainingCount = theirRemaining.Count;
            float ourRemainingPercent = ourRemainingCount / _ai.Blackboard.OurTeamAdapters.Count;
            float theirRemainingPercent = theirRemainingCount / _ai.Blackboard.TheirTeamAdapters.Count;

            var status = tic.OffensiveStatusThreatResult;

            bool isRound1 = _ai.Round <= 1;
            bool isEarlyGame = bfs.IsEarlyGame;

            bool weGainSpeedControl = !ee.AttackerMovesFirst && statusEE.AttackerMovesFirst;

            var moveEffects = tic.OffensiveStatusThreatResult.Move.MoveSO.MoveEffects;

            bool theyCreatePressureState = false;
            bool theyCreateDecisiveState = false;

            //--Status Effect Evidence. Toxic, Taunt, etc.
            if( status.OffensiveStatusType == OffensiveStatusType.StatusEffect || status.OffensiveStatusType == OffensiveStatusType.Disruption )
            {
                _tirLog.Add( $"They are looking to use a move with a status effect." );

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

                bool weAreTauntWeak = ourTraits.Contains( RoleTrait.TauntWeak );
                bool weAreEncoreWeak = ourTraits.Contains( RoleTrait.EncoreWeak );
                bool weAreHealBlockWeak = ourTraits.Contains( RoleTrait.RecoveryMove );
                bool weAreDisableWeak = offStatusTOP.Opponent.VolatileStatuses.Contains( VolatileConditionID.ChoiceLocked ) || offStatusTOP.Opponent.RoleProfile.Signals.PhysicalAttackCount < 2 || offStatusTOP.Opponent.RoleProfile.Signals.SpecialAttackCount < 2;

                theyCreatePressureState = theyBurn || theyFrost || theyToxic;
                theyCreateDecisiveState = theySleep || theyParalyze || theyEncore || theyTaunt || theirTP.ForcesSwitch;

                if( theirRP.PrimaryRole == RoleClass.UtilitySupport || theirRP.PrimaryRole == RoleClass.Disrupter || ( ( theirRP.PrimaryRole == RoleClass.Wall || theirRP.PrimaryRole == RoleClass.BulkyAttacker ) && theirRP.Traits.Contains( RoleTrait.StatusSpreader ) ) )
                {
                    evidence += 1;

                    _tirLog.Add( $"They are a utility, disrupter, or bulky role. Offensive Status Evidence: {evidence}" );

                    if( theirRP.Traits.Contains( RoleTrait.StatusSpreader ) )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Who also spreads status. Offensive Status Evidence: {evidence}" );

                        if( ( theirRP.PrimaryRole == RoleClass.Wall || theirRP.PrimaryRole == RoleClass.BulkyAttacker ) && !ee.OpponentThreatensKO )
                        {
                            evidence += 1;
                            _tirLog.Add( $"And we're a wall/bulky aligned pokemon who isn't immediately threatened. Offensive Status Evidence: {evidence}" );
                        }
                    }
                }

                if( offStatusTOP.Opponent.SevereStatus == SevereConditionID.None && status.OffensiveStatusType == OffensiveStatusType.StatusEffect )
                {
                    if( theyBurn && weAreBurnWeak )
                    {
                        evidence += 1;
                        _tirLog.Add( $"They have a burn opportunity. Offensive Status Evidence: {evidence}" );
                    }
                    else if( theyFrost && weAreFrostWeak )
                    {
                        evidence += 1;
                        _tirLog.Add( $"They have a frostbite opportunity. Offensive Status Evidence: {evidence}" );
                    }
                    else if( theyPoison && wePassiveRecover )
                    {
                        evidence += 1;
                        _tirLog.Add( $"They have a poison opportunity. Offensive Status Evidence: {evidence}" );
                    }
                    else if( theyToxic && weAreToxicWeak )
                    {
                        evidence += 1;
                        _tirLog.Add( $"They have a toxic opportunity. Offensive Status Evidence: {evidence}" );
                    }
                    else if( theyParalyze && weAreParalysisWeak )
                    {
                        evidence += 1;
                        _tirLog.Add( $"They have a paralysis opportunity. Offensive Status Evidence: {evidence}" );
                    }
                    else if( theySleep && ( offStatusTOP.AttackerMovedFirst || offStatusTOP.OpponentPTKO <= PotentialToKO.TwoHKO ) )
                    {
                        evidence += 1;
                        _tirLog.Add( $"They have a sleep opportunity. Offensive Status Evidence: {evidence}" );
                    }
                    else
                    {
                        evidence -= 3;
                        _tirLog.Add( $"They want to status but it doesn't seem to be effective. Offensive Status Evidence: {evidence}" );
                    }
                }
                else
                {
                    evidence -= 5;
                    _tirLog.Add( $"They want to status but we are already severely statused. Offensive Status Evidence: {evidence}" );
                }

                if( status.OffensiveStatusType == OffensiveStatusType.Disruption )
                {
                    if( theyTaunt && weAreTauntWeak && !offStatusTOP.Opponent.VolatileStatuses.Contains( VolatileConditionID.Taunt ) )
                    {
                        evidence += 1;
                        _tirLog.Add( $"They have a taunt opportunity. Offensive Status Evidence: {evidence}" );
                    }
                    else if( theyEncore && weAreEncoreWeak && !offStatusTOP.Opponent.VolatileStatuses.Contains( VolatileConditionID.Encore ) )
                    {
                        evidence += 1;
                        _tirLog.Add( $"They have an encore opportunity. Offensive Status Evidence: {evidence}" );
                    }
                    else if( theyHealBlock && weAreHealBlockWeak && !offStatusTOP.Opponent.VolatileStatuses.Contains( VolatileConditionID.HealBlocked ) )
                    {
                        evidence += 1;
                        _tirLog.Add( $"They have a heal block opportunity. Offensive Status Evidence: {evidence}" );
                    }
                    else if( theyDisable && weAreDisableWeak && !offStatusTOP.Opponent.VolatileStatuses.Contains( VolatileConditionID.Disabled ) )
                    {
                        evidence += 1;
                        _tirLog.Add( $"They have a disable opportunity. Offensive Status Evidence: {evidence}" );
                    }
                    else
                    {
                        evidence -= 3;
                        _tirLog.Add( $"They want to disrupt but it doesn't seem to be effective. Offensive Status Evidence: {evidence}" );
                    }
                }
            }

            //--Entry Hazard Evidence
            if( status.OffensiveStatusType == OffensiveStatusType.EntryHazard )
            {
                _tirLog.Add( $"They are looking to set entry hazards" );
                var ourCourt = offStatusTOP.Opponent.CourtLocation == CourtLocation.TopCourt ? offStatusTOP.Field.TopCourtConditions : offStatusTOP.Field.BottomCourtConditions;
                theyCreatePressureState = ourCourt.ContainsKey( CourtConditionID.LeechSeed );

                if( theirRP.PrimaryRole == RoleClass.UtilitySupport || theirRP.PrimaryRole == RoleClass.Disrupter || ( ( theirRP.PrimaryRole == RoleClass.Wall || theirRP.PrimaryRole == RoleClass.BulkyAttacker ) && theirRP.Traits.Contains( RoleTrait.HazardSetter ) ) )
                {
                    evidence += 1;

                    _tirLog.Add( $"They are a utility, disrupter, or bulky role. Offensive Status Evidence: {evidence}" );

                    if( theirRP.Traits.Contains( RoleTrait.HazardSetter ) )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Who also sets hazards. Offensive Status Evidence: {evidence}" );

                        if( ( theirRP.PrimaryRole == RoleClass.Wall || theirRP.PrimaryRole == RoleClass.BulkyAttacker ) && !ee.OpponentThreatensKO )
                        {
                            evidence += 1;
                            _tirLog.Add( $"And we're a wall/bulky aligned pokemon who isn't immediately threatened. Offensive Status Evidence: {evidence}" );
                        }
                    }
                }

                if( isRound1 && !ee.OpponentThreatensKO )
                {
                    evidence += 3;
                    _tirLog.Add( $"It's the first round of the match and we don't threaten them with a KO. Offensive Status Evidence: {evidence}" );
                }
                else if( isEarlyGame )
                {
                    evidence += 2;
                    _tirLog.Add( $"It's still early game. Offensive Status Evidence: {evidence}" );
                }

                if( ourRemainingPercent >= 0.5f && ourRemainingCount > 2 )
                {
                    evidence += 1;
                    _tirLog.Add( $"We still have plenty of pokemon left for them to get value out of hazards. Offensive Status Evidence: {evidence}" );
                }

                if( bfs.EntryHazardsOn_MySide <= 0 )
                {
                    evidence += 1;
                    _tirLog.Add( $"They haven't put hazards on our side yet. Offensive Status Evidence: {evidence}" );
                }

                int hazardDelta = bfs.EntryHazardsOn_MySide - bfs.EntryHazardsOn_TheirSide;
                if( hazardDelta < 0 )
                {
                    evidence += 1;
                    _tirLog.Add( $"They're behind in hazard pressure. Offensive Status Evidence: {evidence}" );
                }

                if( _ai.IsDoubleBattle )
                {
                    evidence -= 3;
                    _tirLog.Add( $"Hazards have exponentionally less value in a double battle. Offensive Status Evidence: {evidence}" );
                }
            }

            //--Stat Debuff Evidence
            if( status.OffensiveStatusType == OffensiveStatusType.StatDebuff )
            {
                _tirLog.Add( $"They are looking to use a stat debuff move." );
                var move = status.Move;
                var statChanges = move.MoveSO.MoveEffects.StatChangeList;

                bool moveDropsAttack = false;
                bool moveDropsDefense = false;
                bool moveDropsSpAttack = false;
                bool moveDropsSpDefense = false;
                bool moveDropsSpeed = false;

                foreach( var sc in statChanges )
                {
                    if( sc.Change < 0 )
                    {
                        if( sc.Stat == Stat.Attack )
                            moveDropsAttack = true;

                        if( sc.Stat == Stat.Defense )
                            moveDropsDefense = true;

                        if( sc.Stat == Stat.SpAttack )
                            moveDropsSpAttack = true;

                        if( sc.Stat == Stat.SpDefense )
                            moveDropsSpDefense = true;

                        if( sc.Stat == Stat.Speed )
                            moveDropsSpeed = true;

                        theyCreatePressureState = true;
                    }
                }

                bool atkDrop = moveDropsAttack && ourBiases.Contains( RoleBias.Physical );
                bool defDrop = moveDropsDefense && ourBiases.Contains( RoleBias.PhysicallyBulky );
                bool spAtkDrop = moveDropsSpAttack && ourBiases.Contains( RoleBias.Special );
                bool spDefDrop = moveDropsSpDefense && ourBiases.Contains( RoleBias.SpeciallyBulky );
                bool speDrop = moveDropsSpeed && ourTraits.Contains( RoleTrait.ParalysisWeak ); //--Paralysis weak really only means weak to losing speed + the moves that induce paralysis

                if( theirRP.PrimaryRole == RoleClass.UtilitySupport || theirRP.PrimaryRole == RoleClass.Disrupter || ( ( theirRP.PrimaryRole == RoleClass.Wall || theirRP.PrimaryRole == RoleClass.BulkyAttacker ) && theirRP.Traits.Contains( RoleTrait.StatDebuffer ) ) )
                {
                    evidence += 1;

                    _tirLog.Add( $"They are a utility, disrupter, or bulky role. Offensive Status Evidence: {evidence}" );

                    if( theirRP.Traits.Contains( RoleTrait.StatDebuffer ) )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Who is also a stat debuffer. Offensive Status Evidence: {evidence}" );

                        if( ( theirRP.PrimaryRole == RoleClass.Wall || theirRP.PrimaryRole == RoleClass.BulkyAttacker ) && !ee.OpponentThreatensKO )
                        {
                            evidence += 1;
                            _tirLog.Add( $"And we're a wall/bulky aligned pokemon who isn't immediately threatened. Offensive Status Evidence: {evidence}" );
                        }
                    }
                }

                if( atkDrop )
                {
                    evidence += 1;
                    _tirLog.Add( $"They have an attack drop opportunity. Offensive Status Evidence: {evidence}" );
                }

                if( defDrop )
                {
                    evidence += 1;
                    _tirLog.Add( $"They have a defense drop opportunity. Offensive Status Evidence: {evidence}" );
                }

                if( spAtkDrop )
                {
                    evidence += 1;
                    _tirLog.Add( $"They have a special attack drop opportunity. Offensive Status Evidence: {evidence}" );
                }

                if( spDefDrop )
                {
                    evidence += 1;
                    _tirLog.Add( $"They have a special defense drop opportunity. Offensive Status Evidence: {evidence}" );
                }

                if( speDrop )
                {
                    evidence += 1;
                    _tirLog.Add( $"They have a speed drop opportunity. Offensive Status Evidence: {evidence}" );
                }
            }

            //--Phazing Evidence
            if( status.OffensiveStatusType == OffensiveStatusType.Phaze )
            {
                _tirLog.Add( $"They are looking to phaze us out." );

                bool weAreSetup = ourTraits.Contains( RoleTrait.SpeciallyOffensiveSetup ) || ourTraits.Contains( RoleTrait.PhysicallyOffensiveSetup ) || ourTraits.Contains( RoleTrait.SpeciallyDefensiveSetup ) || ourTraits.Contains( RoleTrait.PhysicallyDefensiveSetup );

                var ourStatChanges = offStatusTOP.Opponent.StatStages;
                int statBoosts = 0;

                theyCreateDecisiveState = offStatusTOP.Attacker_EndOfTurnHP > 0;

                foreach( var sc in ourStatChanges )
                {
                    if( sc.Value > 0 )
                        statBoosts++;
                }

                if( theirRP.PrimaryRole == RoleClass.UtilitySupport || theirRP.PrimaryRole == RoleClass.Disrupter || ( ( theirRP.PrimaryRole == RoleClass.Wall || theirRP.PrimaryRole == RoleClass.BulkyAttacker ) && theirRP.Traits.Contains( RoleTrait.Phazes ) ) )
                {
                    evidence += 1;

                    _tirLog.Add( $"They are a utility, disrupter, or bulky role. Offensive Status Evidence: {evidence}" );

                    if( ( theirRP.PrimaryRole == RoleClass.Wall || theirRP.PrimaryRole == RoleClass.BulkyAttacker ) && !ee.OpponentThreatensKO )
                    {
                        evidence += 1;
                        _tirLog.Add( $"And we're a wall/bulky aligned pokemon who isn't immediately threatened. Offensive Status Evidence: {evidence}" );
                    }
                }

                if( statBoosts > 0 )
                {
                    if( statBoosts >= 3 )
                    {
                        evidence += 3;
                        _tirLog.Add( $"We've setup quite a few times. Offensive Status Evidence: {evidence}" );
                    }
                    else if( statBoosts >= 2 )
                    {
                        evidence += 2;
                        _tirLog.Add( $"We've setup a few times. Offensive Status Evidence: {evidence}" );
                    }
                    else
                    {
                        evidence += 1;
                        _tirLog.Add( $"We've setup once. Offensive Status Evidence: {evidence}" );
                    }

                    if( weAreSetup )
                    {
                        evidence += 1;
                        _tirLog.Add( $"We are a setup role. Offensive Status Evidence: {evidence}" );
                    }
                }

                if( ee.AttackerPTKOR.PTKO <= PotentialToKO.Safe || !ee.OpponentThreatensKO )
                {
                    evidence += 1;
                    _tirLog.Add( $"They don't threaten us right now, and/or we don't threaten them in return. Offensive Status Evidence: {evidence}" );
                }
                else if( ( theirRP.PrimaryRole == RoleClass.Wall || theirRP.PrimaryRole == RoleClass.BulkyAttacker ) && ee.AttackerPTKOR.PTKO <= PotentialToKO.TwoHKO )
                {
                    evidence += 1;
                    _tirLog.Add( $"They are a wall/bulky mon and we don't threaten them too much. Offensive Status Evidence: {evidence}" );
                }
                else if( ee.OpponentThreatensKO && ee.OpponentMovesFirst )
                {
                    evidence -= 2;
                    _tirLog.Add( $"We threaten to KO them and we move first. Offensive Status Evidence: {evidence}" );

                    if( offStatusTOP.OpponentPTKO == PotentialToKO.OHKO )
                    {
                        evidence -= 1;
                        _tirLog.Add( $"We're very likely to OHKO before they can phaze us. Offensive Status Evidence: {evidence}" );
                    }
                }

                if( bfs.EntryHazardsOn_MySide > 0 )
                {
                    evidence += 1;
                    _tirLog.Add( $"They've setup hazards, which gives phazing us value. Offensive Status Evidence: {evidence}" );
                }

                //--Once threat profile is integrated, use sweep potential in place of this:
                if( ourRemainingPercent > 0.5f && offStatusTOP.AttackerPTKO >= PotentialToKO.Dangerous && ( statBoosts > 0 || offStatusTOP.Attacker.BeginningHPR >= 0.7f ) )
                {
                    evidence += 1;
                    _tirLog.Add( $"We're likely to begin sweeping, they may want to get rid of us. Offensive Status Evidence: {evidence}" );
                }

                if( ee.OpponentSwitchProbability <= 0.2f )
                {
                    evidence += 1;
                    _tirLog.Add( $"We're not likely to switch ({ee.OpponentSwitchProbability}), reliably staying in is a reason to get us out. Offensive Status Evidence: {evidence}" );
                }
                else
                {
                    evidence -= 3;
                    _tirLog.Add( $"They're not certain we'll switch, phazing may be a bad idea (phazing our switch candidate back out isn't a terrible play). Offensive Status Evidence: {evidence}" );
                }
            }

            //--Universal Evidence
            if( ee.OpponentThreatensKO )
            {
                evidence -= 2;
                _tirLog.Add( $"We threaten a KO on them. Offensive Status Evidence: {evidence}" );

                if( ee.OpponentMovesFirst )
                {
                    evidence -= 1;
                    _tirLog.Add( $"We also move first. Offensive Status Evidence: {evidence}" );
                }
            }
            else if( ee.OpponentPTKOR.PTKO >= PotentialToKO.Risky )
            {
                evidence -= 1;
                _tirLog.Add( $"We threaten big damage on them if they choose to use a status move. Offensive Status Evidence: {evidence}" );
            }

            if( ee.OpponentSwitchProbability >= 0.75f )
            {
                evidence += 1;
                _tirLog.Add( $"Our switch probability ({ee.OpponentSwitchProbability}) >= 0.75f, so we're likely to switch. Offensive Status Evidence: {evidence}" );
            }

            if( !offStatusTOP.OpponentCanAct )
            {
                evidence += 1;
                _tirLog.Add( $"We immediately can't act this turn if they land their intended status move (such as a faster sleep powder). Offensive Status Evidence: {evidence}" );
            }
            else if( weGainSpeedControl )
            {
                evidence += 1;
                _tirLog.Add( $"Their status move gives them speed control next turn. Offensive Status Evidence: {evidence}" );
            }

            if( ( theirTP.Type == ThreatType.Disruptive || theirTP.DisruptivePressure >= 5f ) && status.OffensiveStatusType != OffensiveStatusType.EntryHazard )
            {
                evidence += 1;
                _tirLog.Add( $"They are a disruptive threat or have high disruptive pressure and are looking to use a status move that isn't an entry hazard. Offensive Status Evidence: {evidence}" );
            }

            if( ( theirRP.PrimaryRole == RoleClass.Wall || theirRP.PrimaryRole == RoleClass.BulkyAttacker ) && ( theirTP.Type == ThreatType.Persistent || theirTP.PersistentPressure >= 5f ) )
            {
                evidence += 1;
                _tirLog.Add( $"They have high persistent pressure or a persistent threat, so they may be less likely to attack and more likely to click a status-category move. Offensive Status Evidence: {evidence}" );
            }

            _tirLog.Add( $"Offensive Status TOP Units: {offStatusTOP.Attacker.Name} ({offStatusTOP.Attacker.Ability}), {offStatusTOP.Opponent.Name} ({offStatusTOP.Opponent.Ability})" );
            if( offStatusTOP.Opponent.Ability == AbilityID.MagicBounce || offStatusTOP.Opponent.Ability == AbilityID.MagicGuard )
            {
                evidence -= 3;
                _tirLog.Add( $"Our current unit has magic bounce or magic guard, they cannot use a status move if we stay. Offensive Status Evidence: {evidence}" );
            }

            if( tic.MoveThreatResult?.Top.OpponentPTKO <= PotentialToKO.Safe && statusEE.OpponentPTKOR.PTKO > tic.MoveThreatResult?.Top.OpponentPTKO )
            {
                evidence += 2;
                _tirLog.Add( $"Status advances the board more than simply attacking. Offensive Status Evidence: {evidence}");
            }

            //--PBS
            if( theyCreatePressureState )
            {
                evidence += 1;
                _tirLog.Add( $"Their offensive status creates a pressure state. Offensive Status Evidence: {evidence}" );
            }

            if( theyCreateDecisiveState )
            {
                evidence += 1;
                _tirLog.Add( $"Their offensive status creates a decisive state. Offensive Status Evidence: {evidence}" );
            }

            if( statusEE.OpponentPTKOR.PTKO < offStatusTOP.OpponentPTKO )
            {
                evidence += 1;
                _tirLog.Add( $"Their status stabilizes the board for them. Offensive Status Evidence: {evidence}" );
            }

            //--Current Plan & GPA
            if( status.OffensiveStatusType != OffensiveStatusType.EntryHazard && _ai.Blackboard.GamePlan.OurBlockers.Contains( offStatusTOP.Opponent.Pokemon ) )
            {
                evidence += 1;
                _tirLog.Add( $"We are a game plan blocker and they are looking to cripple us with status. Offensive Status Evidence: {evidence}" );

                if( status.OffensiveStatusType == OffensiveStatusType.StatusEffect || status.OffensiveStatusType == OffensiveStatusType.Disruption )
                {
                    evidence += 1;
                    _tirLog.Add( $"That status is also a status effect such as toxic or taunt. Offensive Status Evidence: {evidence}" );
                }
            }

            if( theirCP.FocusMon == offStatusTOP.Opponent.Pokemon && theirCP.Type == PlanType.Aggress && status.OffensiveStatusType != OffensiveStatusType.EntryHazard )
            {
                evidence += 1;
                _tirLog.Add( $"They are looking to aggress our current pokemon. Offensive Status Evidence: {evidence}" );
            }

            if( theirCP.AllowSacrifice && statusEE.OpponentMovesFirst )
            {
                evidence += 1;
                _tirLog.Add( $"Their plan allows sacrificing, statusing before fainting can create value. Offensive Status Evidence: {evidence}" );
            }

            //--Doubles
            if( _ai.IsDoubleBattle )
            {
                _tirLog.Add( $"" );
                _tirLog.Add( $"This is a double battle! Checking Offensive Status intent evidence in the context of doubles..." );

                var theirAlly = _ai.GetActiveAllyAs_Adapter( tic.Threat.Pokemon );
                bool theyHaveAlly = true;
                if( theirAlly == null )
                {
                    _tirLog.Add( $"They don't have an ally on the field, skipping!" );
                    _tirLog.Add( $"" );
                    theyHaveAlly = false;
                    return evidence;
                }

                _tirLog.Add( $"Their ({tic.Threat.Name}) ally is: {theirAlly.Name} ({theirAlly.RoleProfile.PrimaryRole})" );
                _tirLog.Add( $"" );

                //--Our Ally Information
                var ourAlly = _ai.GetActiveAllyAs_Adapter( _ai.CurrentUnitAdapter.Pokemon );
                bool weHaveAlly = ourAlly != null;
                if( weHaveAlly )
                {
                    _tirLog.Add( $"Our ({_ai.CurrentUnitAdapter.Name}) ally is: {ourAlly.Name} ({ourAlly.RoleProfile.PrimaryRole})" );
                    _tirLog.Add( $"" );
                }

                bool weCanProtect = _ai.UnitSim.CheckHasMove( _ai.CurrentUnitAdapter, "Protect" ) && _ai.GetBattleUnit( _ai.CurrentUnitAdapter.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;
                bool ourAllyCanProtect = weHaveAlly && _ai.UnitSim.CheckHasMove( ourAlly, "Protect" ) && _ai.GetBattleUnit( ourAlly.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;

                //--Their Ally Information
                var theirAllyRP = theirAlly.RoleProfile;
                var theirAllyTraits = theirAllyRP.Traits;

                bool theirAllyIsOffensive = theirAllyRP.PrimaryRole == RoleClass.BulkyAttacker || theirAllyRP.PrimaryRole == RoleClass.RevengeKiller || theirAllyRP.PrimaryRole == RoleClass.SetupSweeper ||
                theirAllyRP.PrimaryRole == RoleClass.Sweeper || theirAllyRP.PrimaryRole == RoleClass.TrickRoomAbuser || theirAllyRP.PrimaryRole == RoleClass.WallBreaker;

                bool theirAllyIsDefensive = theirAllyRP.PrimaryRole == RoleClass.Wall || theirAllyRP.PrimaryRole == RoleClass.DefensiveSetup || theirAllyRP.PrimaryRole == RoleClass.BulkyAttacker && theirAllyRP.SecondaryRoles.Contains( RoleClass.Wall );
                bool theirAllyIsSupport = theirAllyRP.PrimaryRole == RoleClass.UtilitySupport || theirAllyRP.PrimaryRole == RoleClass.Disrupter;
                bool theirAllyIsUtility = !theirAllyIsOffensive && !theirAllyIsDefensive;

                bool theyCanProtect = _ai.UnitSim.CheckHasMove( tic.Threat, "Protect" ) && _ai.GetBattleUnit( tic.Threat.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;
                bool theirAllyCanProtect = theyHaveAlly && _ai.UnitSim.CheckHasMove( theirAlly, "Protect" ) && _ai.GetBattleUnit( theirAlly.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;

                bool theirAllyHas_FakeOut       = _ai.CanUseFakeOut( theirAlly, _ai.CurrentUnitAdapter );
                bool theirAllyHas_FollowMe      = _ai.UnitSim.CheckHasMove( theirAlly, "Follow Me" );
                bool theirAllyHas_RagePowder    = _ai.UnitSim.CheckHasMove( theirAlly, "Rage Powder" );
                bool theirAllyHas_SpeedControl  = theirAllyTraits.Contains( RoleTrait.SpeedControl );

                if( theirAllyHas_FakeOut )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their ally has Fake Out. Offensive Status Evidence: {evidence}" );
                }

                if( theirAllyHas_FollowMe )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their ally has Follow Me. Offensive Status Evidence: {evidence}" );
                }

                if( theirAllyHas_RagePowder )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their ally has Rage Powder. Offensive Status Evidence: {evidence}" );
                }

                if( theirAllyHas_SpeedControl )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their ally has Speed Control. Offensive Status Evidence: {evidence}" );
                }

            }

            //--Opposing Intents
            //--Setup steals status opportunities
            bool setupOpportunity = tic.SetupThreatResult.Move != null && tic.SetupThreatResult.AfterPTKOR.PTKO > tic.SetupThreatResult.Top.AttackerPTKO && !ee.OpponentThreatensKO;

            if( setupOpportunity )
            {
                evidence -= 2;
                _tirLog.Add( $"A strong setup opportunity exists. Setup is likely preferable to spreading status. Offensive Status Evidence: {evidence}" );
            }

            //--Attacking is better pull
            if( offStatusTOP.AttackerPTKO >= PotentialToKO.Dangerous )
            {
                evidence -= 2;
                _tirLog.Add( $"They have a strong KO chance on us, status may be less likely. Offensive Status Evidence: {evidence}" );

                if( offStatusTOP.AttackerMovedFirst )
                {
                    evidence -= 1;
                    _tirLog.Add( $"They're also faster than us, which increases their ability to just take a KO. Offensive Status Evidence: {evidence}" );
                }
            }

            //--Switch provides better situational improvement
            if( tic.DefensiveSwitchCandidateResult.Pokemon != null && status.OffensiveStatusType != OffensiveStatusType.EntryHazard && tic.DefensiveSwitchCandidateResult.Top.OpponentPTKO < offStatusTOP.OpponentPTKO )
            {
                evidence -= 1;
                _tirLog.Add( $"Switching may provide a better situational improvement than an offensive status move. Offensive Status Evidence: {evidence}" );
            }

            //--Tactical Diminishing Returns
            if( status.OffensiveStatusType == OffensiveStatusType.StatusEffect )
            {
                int statusedMons = 0;
                foreach( var mon in theirRemaining )
                {
                    if( mon.Value.SevereStatus != SevereConditionID.None )
                        statusedMons++;
                }

                float statusedPercent = (float)statusedMons / theirRemainingCount;

                if( statusedPercent > 0.25f )
                {
                    evidence -= 3;
                    _tirLog.Add( $"They've already statused {statusedPercent} of our remaining Pokemon, perhaps they will do something more useful. Offensive Status Evidence: {evidence}" );
                }
            }
        }
        else
        {
            evidence = -99;
            _tirLog.Add( $"Offensive Status candidate not found! Nullifying evidence. Offensive Status Evidence: {evidence}" );
        }

        _tirLog.Add( $"" );

        return evidence;
    }

    private int SupportiveStatusEvidence( ThreatIntentCandidates tic, ExchangeEvaluation ee, BattlefieldState bfs, ThreatProfile theirTP, ThreatProfile ourTP, CurrentPlan theirCP )
    {
        int evidence = 0;

        //---------------------------------------------------------------------------------
        //--Supportive Status Evidence Gathering-------------------------------------------
        //---------------------------------------------------------------------------------
        _tirLog.Add( $"===[Supportive Status Evidence]===" );
        if( tic.SupportiveStatusThreatResult.Move != null )
        {
            var suppStatusTOP = tic.SupportiveStatusThreatResult.Top;
            var statusEEPack = _ai.Projection.GetExchangePack( ee.Attacker, ee.Opponent );
            var threatVS_Us = statusEEPack.UsVS_Threat;
            var threatAllyVS_Us = statusEEPack.AllyVS_Threat;
            var threatVS_OurAlly = statusEEPack.UsVS_ThreatAlly;
            var threatAllyVS_OurAlly = statusEEPack.AllyVS_ThreatAlly;

            var theirAlly = statusEEPack.OurAllyExists ? threatAllyVS_Us.Attacker : null;
            var ourAlly = statusEEPack.ThreatAllyExists ? threatAllyVS_OurAlly.Opponent : null;

            var theirRP = suppStatusTOP.Attacker.RoleProfile;
            var theirCourt = tic.Threat.CourtLocation == CourtLocation.TopCourt ? _ai.Blackboard.CurrentFieldSnapshot.TopCourtConditions : _ai.Blackboard.CurrentFieldSnapshot.BottomCourtConditions;

            var ourBiases = suppStatusTOP.Opponent.RoleProfile.Biases;
            var ourTraits = suppStatusTOP.Opponent.RoleProfile.Traits;
            var theirTraits = suppStatusTOP.Attacker.RoleProfile.Traits;

            var move = tic.SupportiveStatusThreatResult.Move;
            var moveTarget = move.MoveSO.MoveTarget;
            var effects = move.MoveSO.MoveEffects;
            var effectsTarget = effects.Target;

            //--After Buff Exchange Evaluations
            ExchangeEvaluation threatAllyVS_UsAfter = default;
            if( statusEEPack.OurAllyExists )
            {
                var tempField = _ai.UnitSim.BuildSimField();
                IBattleAIUnit allyAfter = _ai.UnitSim.CopySimUnit( theirAlly, tempField );
                _ai.UnitSim.ApplySupportEffect( allyAfter, move, tempField );

                threatAllyVS_UsAfter = _ai.Projection.EvaluateExchange( allyAfter, threatAllyVS_Us.Opponent );
            }

            var us = ee.Opponent;
            var threat = ee.Attacker;
            var ourTeam = _ai.GetRemainingOpposingPokemon( threat.Pokemon );
            var threatTeam = _ai.GetRemainingAllyPokemon( threat.Pokemon );
            var teamAnalBefore = _ai.Projection.Get_TeamVSTeamAnalysis( threatTeam, ourTeam );

            var ourRemaining = _ai.GetRemainingPartyAs_IBattleAIUnits( us.Pokemon );
            var threatRemaining = _ai.GetRemainingPartyAs_IBattleAIUnits( threat.Pokemon );
            float ourRemainingPercentage = ourRemaining.Count / (float)_ai.Blackboard.OurTeamPokemon.Count;
            float threatRemainingPercentage = threatRemaining.Count / (float)_ai.Blackboard.TheirTeamPokemon.Count;

            var status = tic.SupportiveStatusThreatResult;
            var statusType = status.SupportiveStatusType;

            //--Material Opportunity Values
            if( statusType == SupportiveStatusType.BattlefieldControl )
            {
                if( threatRemainingPercentage > 0.5f )
                {
                    evidence += 1;
                }
                else if( threatRemainingPercentage < 0.5f && ourRemainingPercentage > 0.5f )
                {
                    evidence += 1;
                }
                else if( threatRemainingPercentage < 0.25f )
                {
                    evidence -= 5;
                }
                else if( threatRemainingPercentage < 0.5f )
                {
                    evidence -= 3;
                }
            }

            if( statusType == SupportiveStatusType.Recovery )
            {
                _tirLog.Add( $"They are looking to use a self-recovery move." );
                if( threatVS_Us.OpponentPTKO != PotentialToKO.OHKO && threatVS_Us.Attacker.BeginningHPR < 0.6f )
                {
                    evidence += 1;
                    _tirLog.Add( $"We don't have a guaranteed OHKO on them and their health is below 60%, they may want to try recovering. Supportive Status Evidence: {evidence}" );
                }
                else if( threatVS_Us.OpponentPTKO >= PotentialToKO.Dangerous && threatVS_Us.AttackerMovesFirst )
                {
                    evidence += 2;
                    _tirLog.Add( $"We have a likely KO on them but they move first with recovery. Supportive Status Evidence: {evidence}" );
                }
                else if( threatVS_Us.OpponentPTKO >= PotentialToKO.Dangerous && threatVS_Us.OpponentMovesFirst )
                {
                    evidence -= 2;
                    _tirLog.Add( $"We have a likely KO on them but we move first with recovery. Supportive Status Evidence: {evidence}" );
                }

                if( threatVS_Us.AttackerPTKO <= PotentialToKO.Risky )
                {
                    if( threatVS_Us.OpponentPTKO >= PotentialToKO.Dangerous && ( threatVS_Us.OpponentMovesFirst || threatVS_Us.Attacker.BeginningHPR > 0.5f ) )
                    {
                        evidence -= 2;
                        _tirLog.Add( $"We are likely to get a KO on them and they do not move first, trying to recover would throw the turn. Supportive Status Evidence: {evidence}" );
                    }

                    if( threatVS_Us.Attacker.BeginningHPR > 0.6f )
                    {
                        evidence -= 2;
                        _tirLog.Add( $"Their hp is reasonable. Supportive Status Evidence: {evidence}" );
                    }
                    else
                    {
                        float hpr = threatVS_Us.Attacker.BeginningHPR;
                        if( hpr >= 0.4f )
                        {
                            evidence += 2;
                            _tirLog.Add( $"Their hp is moderately low. Supportive Status Evidence: {evidence}" );
                        }
                        else if( hpr >= 0.2f )
                        {
                            evidence += 3;
                            _tirLog.Add( $"Their hp is very low. Supportive Status Evidence: {evidence}" );
                        }
                        else if( hpr < 0.2f && threatVS_Us.AttackerMovesFirst )
                        {
                            evidence += 4;
                            _tirLog.Add( $"Their hp is dangerously low. Supportive Status Evidence: {evidence}" );
                        }
                    }
                }

                if( threatVS_Us.AttackerPTKO >= PotentialToKO.Risky && threatVS_Us.Attacker.BeginningHPR >= 0.4f )
                {
                    evidence += 1;
                    _tirLog.Add( $"They have good damage on us currenty and around 40% HP. They may recover so they can threaten us better nex turn. Supportive Status Evidence: {evidence}" );
                }

                bool theyHaveOffensiveSetup = theirTraits.Contains( RoleTrait.PhysicallyOffensiveSetup ) || theirTraits.Contains( RoleTrait.SpeciallyOffensiveSetup );
                bool recoveryLooksGood = suppStatusTOP.Attacker.BeginningHPR <= 0.45f && !ee.AttackerThreatensKO && !ee.OpponentThreatensKO && theyHaveOffensiveSetup;
                if( recoveryLooksGood )
                {
                    evidence += 1;
                    _tirLog.Add( $"Recovery is looking reasonable compared to setting up. Setup Evidence: {evidence}" );
                }
            }

            if( statusType == SupportiveStatusType.ForceMultiplier || statusType == SupportiveStatusType.BattlefieldControl )
            {
                bool isAllySetup = _ai.UnitSim.MoveIsSetup( move ) && effects.Target == EffectTarget.AllySide;
                bool isAfterYou = move.MoveSO.Name == "After You";
                bool isHelpingHand = effects.VolatileStatus == VolatileConditionID.HelpingHand;
                bool isTailwind = effects.CourtCondition == CourtConditionID.Tailwind;
                bool isReflect = effects.CourtCondition == CourtConditionID.Reflect;
                bool isLightScreen = effects.CourtCondition == CourtConditionID.LightScreen;
                bool isAuroraVeil = effects.CourtCondition == CourtConditionID.AuroraVeil;
                bool isScreens = isReflect || isLightScreen || isAuroraVeil;
                bool isTrickRoom = effects.FieldCondition == FieldConditionID.TrickRoom;
                bool isWeather = effects.Weather != WeatherConditionID.None;
                bool isTerrain = effects.Terrain != TerrainID.None;

                var currentField = _ai.Blackboard.CurrentFieldSnapshot;

                //--Support already exists
                if( isTailwind && theirCourt.ContainsKey( CourtConditionID.Tailwind ) )
                {
                    evidence -= 5;
                    _tirLog.Add( $"Their move is likely tailwind but it's already up. Setup Evidence: {evidence}" );
                }

                if( isReflect && theirCourt.ContainsKey( CourtConditionID.Reflect ) )
                {
                    evidence -= 5;
                    _tirLog.Add( $"Their move is likely reflect but it's already up. Setup Evidence: {evidence}" );
                }

                if( isLightScreen && theirCourt.ContainsKey( CourtConditionID.LightScreen ) )
                {
                    evidence -= 5;
                    _tirLog.Add( $"Their move is likely light screen but it's already up. Setup Evidence: {evidence}" );
                }

                if( isAuroraVeil && theirCourt.ContainsKey( CourtConditionID.AuroraVeil ) )
                {
                    evidence -= 5;
                    _tirLog.Add( $"Their move is likely aurora veil but it's already up. Setup Evidence: {evidence}" );
                }

                if( isWeather && currentField.Weather == effects.Weather )
                {
                    evidence -= 5;
                    _tirLog.Add( $"Their move is likely a weather move but that weather is already up. Setup Evidence: {evidence}" );
                }

                if( isTerrain && currentField.Terrain == effects.Terrain )
                {
                    evidence -= 5;
                    _tirLog.Add( $"Their move is likely a terrain move but that terrain is already up. Setup Evidence: {evidence}" );
                }

                //--Team vs Team Comparisons
                List<IBattleAIUnit> threatTeamAfter = new();
                foreach( var mon in threatRemaining )
                {
                    var tempField = _ai.UnitSim.BuildSimField();
                    var sim = _ai.UnitSim.GetSimUnit( mon, us, tempField );

                    if( isTailwind || isScreens )
                        _ai.UnitSim.ApplySupportEffect( sim, move, tempField );

                    threatTeamAfter.Add( sim );
                }

                var teamAnalAfter = _ai.Projection.Get_TeamVSTeamAnalysis( threatTeamAfter, ourRemaining );

                //--Compare Team vs Team before and after. Naming here is reversed, "Our" refers to the threat's team, and "their" refers to our team.
                if( teamAnalAfter.Our_AveragePTKO > teamAnalBefore.Our_AveragePTKO )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their team's average PTKO improves from support. Supportive Status Evidence: {evidence}" );
                }

                if( teamAnalAfter.Our_BestPTKO > teamAnalBefore.Our_BestPTKO )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their team's best PTKO improves from support. Supportive Status Evidence: {evidence}" );
                }

                if( teamAnalAfter.Their_AveragePTKO < teamAnalBefore.Their_AveragePTKO )
                {
                    evidence += 1;
                    _tirLog.Add( $"Our team's average PTKO worsens from support. Supportive Status Evidence: {evidence}" );
                }

                if( teamAnalAfter.Their_BestPTKO < teamAnalBefore.Their_BestPTKO )
                {
                    evidence += 1;
                    _tirLog.Add( $"Our team's best PTKO worsens from support. Supportive Status Evidence: {evidence}" );
                }

                if( teamAnalAfter.Our_Outspeeds > teamAnalBefore.Our_Outspeeds )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their team's total out-speeds improve from support. Supportive Status Evidence: {evidence}" );
                }
                else if( teamAnalAfter.Our_Outspeeds <= teamAnalBefore.Our_Outspeeds && isTrickRoom )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their team's total out-speeds improve due to trick room. Supportive Status Evidence: {evidence}" );
                }

                if( teamAnalBefore.Our_Outspeeds <= teamAnalBefore.Their_Outspeeds && teamAnalAfter.Our_Outspeeds > teamAnalBefore.Their_Outspeeds )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their team's total out-speeds are higher after support. Supportive Status Evidence: {evidence}" );
                }
                else if( teamAnalBefore.Our_Outspeeds <= teamAnalBefore.Their_Outspeeds && isTrickRoom )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their team's total out-speeds are higher after trick room. Supportive Status Evidence: {evidence}" );
                }

                //--Check value of setting up ally
                if( isAllySetup )
                {
                    foreach( var sc in effects.StatChangeList )
                    {
                        var stat = sc.Stat;
                        var change = sc.Change;

                        if( change > 0 )
                        {
                            if( stat == Stat.Attack && theirAlly.RoleProfile.Biases.Contains( RoleBias.Physical ) )
                            {
                                evidence += threat.Speed > theirAlly.Speed ? 2 : 1;
                                _tirLog.Add( $"Their ally is Physical and they want to boost their ally's Physical Attack stages. Supportive Status Evidence: {evidence}" );
                            }

                            if( stat == Stat.SpAttack && theirAlly.RoleProfile.Biases.Contains( RoleBias.Special ) )
                            {
                                evidence += threat.Speed > theirAlly.Speed ? 2 : 1;
                                _tirLog.Add( $"Their ally is Special and they want to boost their ally's Special Attack stages. Supportive Status Evidence: {evidence}" );
                            }

                            if( stat == Stat.Defense )
                            {
                                evidence += threat.Speed > theirAlly.Speed ? 2 : 1;
                                evidence += theirAlly.RoleProfile.Biases.Contains( RoleBias.PhysicallyBulky ) ? 2 : 1;
                                _tirLog.Add( $"They want to boost their ally's Physical Defense stages. Supportive Status Evidence: {evidence}" );
                            }

                            if( stat == Stat.SpDefense )
                            {
                                evidence += threat.Speed > theirAlly.Speed ? 2 : 1;
                                evidence += theirAlly.RoleProfile.Biases.Contains( RoleBias.SpeciallyBulky ) ? 2 : 1;
                                _tirLog.Add( $"They want to boost their ally's Special Defense stages. Supportive Status Evidence: {evidence}" );
                            }

                            if( stat == Stat.Speed )
                            {
                                evidence += threat.Speed > theirAlly.Speed ? 2 : 1;
                                evidence += !theirAlly.RoleProfile.Traits.Contains( RoleTrait.ParalysisWeak ) ? 2 : 1;
                                _tirLog.Add( $"They want to boost their ally's Speed stages. Supportive Status Evidence: {evidence}" );
                            }
                        }
                        else if( change <= 0 && move.MoveSO.Name == "Acupressure" )
                        {
                            evidence += 1;
                            _tirLog.Add( $"Their ally has missing stat stage boosts or has stat stage reductions in the face of Acupressure. Supportive Status Evidence: {evidence}" );
                        }
                    }
                }

                if( isTrickRoom )
                {
                    _tirLog.Add( $"They may want to set Trick Room." );

                    int threatTRCS = 0;
                    threatTRCS += _ai.UnitSim.Get_TrickRoomContextScore( threatVS_Us.Attacker.Pokemon, true );
                    threatTRCS += statusEEPack.OurAllyExists ? _ai.UnitSim.Get_TrickRoomContextScore( threatAllyVS_Us.Attacker.Pokemon, true ) : 0;

                    int ourTRCS = 0;
                    ourTRCS += _ai.UnitSim.Get_TrickRoomContextScore( threatVS_Us.Opponent.Pokemon, true );
                    ourTRCS += statusEEPack.ThreatAllyExists ? _ai.UnitSim.Get_TrickRoomContextScore( threatAllyVS_Us.Opponent.Pokemon, true ) : 0;

                    int trDelta = threatTRCS - ourTRCS;

                    if( trDelta > 0 )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their side of the field benefits from Trick Room ({threatTRCS} - {ourTRCS} = {trDelta}). Supportive Status Evidence: {evidence}" );

                        if( trDelta > 1 )
                        {
                            evidence += 1;
                            _tirLog.Add( $"Trick Room is likely very good for them ({threatTRCS} - {ourTRCS} = {trDelta}). Supportive Status Evidence: {evidence}" );
                        }
                    }
                    else
                    {
                        evidence -= 2;
                        _tirLog.Add( $"Their side of the field does not benefit from Trick Room ({threatTRCS} - {ourTRCS} = {trDelta}). Supportive Status Evidence: {evidence}" );
                        
                        if( trDelta < 1)
                        {
                            evidence -= 1;
                            _tirLog.Add( $"Trick Room is especially bad for them ({threatTRCS} - {ourTRCS} = {trDelta}). Supportive Status Evidence: {evidence}" );
                        }
                    }

                    if( theirRP.PrimaryRole == RoleClass.TrickRoomAbuser || theirRP.SecondaryRoles.Contains( RoleClass.TrickRoomAbuser ) || theirAlly.RoleProfile.PrimaryRole == RoleClass.TrickRoomAbuser || theirAlly.RoleProfile.SecondaryRoles.Contains( RoleClass.TrickRoomAbuser ) )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their ally or they are trick room abusers. Supportive Status Evidence: {evidence}" );
                    }

                    if( theirAlly != null && _ai.CanUseFakeOut( theirAlly, us ) )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their ally can use fake out to protect setting up trick room. Supportive Status Evidence: {evidence}" );
                    }

                    if( theirAlly != null )
                    {
                        if( threatAllyVS_Us.AttackerPTKO >= PotentialToKO.Dangerous && ( threatAllyVS_Us.AttackerMovesFirst || threatAllyVS_UsAfter.AttackerMovesFirst ) ) //--trick room isn't reflected in speed order from exchange evaluations, so we have to manually make reverse order assumptions
                        {
                            evidence += 1;
                            _tirLog.Add( $"Their ally can take advantage of the inversed speed order to get a KO. Supportive Status Evidence: {evidence}" );
                        }
                    }

                    if( theirAlly.RoleProfile.Biases.Contains( RoleBias.TrickRoomSpeed ) || theirAlly.RoleProfile.Biases.Contains( RoleBias.SlowSpeed ) )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their ally is very slow speed which benefits trick room. Supportive Status Evidence: {evidence}" );
                    }
                    else
                    {
                        evidence -= 3;
                        _tirLog.Add( $"Their ally's speed is likely incompatible with trick room. Supportive Status Evidence: {evidence}" );
                    }
                }

                //--Their ally taking advantage of immediate turn speed advantage to get a KO
                if( statusEEPack.OurAllyExists ) //--Our is again them, as this ee pack is from their perspective
                {
                    if( threatAllyVS_Us.AttackerPTKO >= PotentialToKO.Dangerous && threatAllyVS_Us.OpponentMovesFirst && threatAllyVS_UsAfter.AttackerMovesFirst )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their ally can take advantage of an immediate speed change to get a KO. Supportive Status Evidence: {evidence}" );
                    }
                }

                //--Overall Team Battlefield Context Comparisons
                int ourTotalTeamContext = 0;
                foreach( var mon in ourTeam )
                {
                    if( isWeather )
                        ourTotalTeamContext += _ai.UnitSim.Get_WeatherContextScore( mon, effects.Weather );

                    if( isTerrain )
                        ourTotalTeamContext += _ai.UnitSim.Get_TerrainContextScore( mon, effects.Terrain );

                    if( isTrickRoom && effects.FieldCondition == FieldConditionID.TrickRoom )
                        ourTotalTeamContext += _ai.UnitSim.Get_TrickRoomContextScore( mon, true );
                }

                int theirTotalTeamContext = 0;
                foreach( var mon in threatTeam )
                {
                    if( isWeather )
                        theirTotalTeamContext += _ai.UnitSim.Get_WeatherContextScore( mon, effects.Weather );

                    if( isTerrain )
                        theirTotalTeamContext += _ai.UnitSim.Get_TerrainContextScore( mon, effects.Terrain );

                    if( isTrickRoom && effects.FieldCondition == FieldConditionID.TrickRoom )
                        theirTotalTeamContext += _ai.UnitSim.Get_TrickRoomContextScore( mon, true );
                }

                int battlefieldDelta = theirTotalTeamContext - ourTotalTeamContext;

                if( battlefieldDelta > 0 )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their team benefits from the battlefield context changes. Supportive Status Evidence: {evidence}" );
                }
                else
                {
                    evidence -= 1;
                    _tirLog.Add( $"Their team does not benefit from the battlefield context changes. Supportive Status Evidence: {evidence}" );
                }

                if( isHelpingHand && theirAlly != null )
                {
                    const float helpinghand = 1.5f;
                    //--Both of these need their own unique evidence contributions! todo immediately!
                    _tirLog.Add( $"They may want to use Helping Hand." );
                    var theirAllyEDR_Us = threatAllyVS_Us.AttackerMTR.EDR;
                    var theirAllyPTKO_Us = threatAllyVS_Us.AttackerPTKO;

                    var theirAllyMove_Us = threatAllyVS_Us.AttackerMTR.Move;
                    bool theirAllyMoveVsUsIsSpread = theirAllyMove_Us.MoveSO.MoveTarget == MoveTarget.AllAdjacent || theirAllyMove_Us.MoveSO.MoveTarget == MoveTarget.OpposingSide;

                    var adjustedPTKO_Us = _ai.Projection.Get_InteractionModifiedPTKO( theirAllyEDR_Us, theirAlly, us, addModifier: helpinghand );

                    if( adjustedPTKO_Us > theirAllyPTKO_Us )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their ally's PTKO is improved by helping hand. Supportive Status Evidence: {evidence}" );
                    }

                    if( adjustedPTKO_Us - theirAllyPTKO_Us > 2 )
                    {
                        evidence += 2;
                        _tirLog.Add( $"Their ally's PTKO is improved by 2 levels by helping hand. Supportive Status Evidence: {evidence}" );
                    }

                    if( theirAllyPTKO_Us != PotentialToKO.OHKO && adjustedPTKO_Us >= PotentialToKO.OHKO )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their PTKO is OHKO with helping hand and it isn't OHKO without it. Supportive Status Evidence: {evidence}" );
                    }

                    if( theirAllyMoveVsUsIsSpread )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their ally's move with the best PTKO is a spread move. Supportive Status Evidence: {evidence}" );
                    }

                    if( theirAlly.Speed > us.Speed || theirAllyMove_Us.Priority > MovePriority.Zero )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their ally outspeeds us. Supportive Status Evidence: {evidence}" );
                    }

                    if( ourAlly != null )
                    {
                        var theirAllyEDR_OurAlly = threatAllyVS_OurAlly.AttackerMTR.EDR;
                        var theirAllyPTKO_OurAlly = threatAllyVS_OurAlly.AttackerPTKO;

                        var theirAllyMove_OurAlly = threatAllyVS_OurAlly.AttackerMTR.Move;
                        bool theirAllyMoveVsOurAllyIsSpread = theirAllyMove_OurAlly.MoveSO.MoveTarget == MoveTarget.AllAdjacent || theirAllyMove_OurAlly.MoveSO.MoveTarget == MoveTarget.OpposingSide;

                        var adjustedPTKO_OurAlly = _ai.Projection.Get_InteractionModifiedPTKO( theirAllyEDR_OurAlly, theirAlly, ourAlly, addModifier: helpinghand );

                        if( adjustedPTKO_OurAlly > theirAllyPTKO_OurAlly )
                        {
                            evidence += 1;
                            _tirLog.Add( $"Their ally's PTKO against our ally improves with helping hand. Supportive Status Evidence: {evidence}" );
                        }

                        if( adjustedPTKO_OurAlly - theirAllyPTKO_OurAlly > 2 )
                        {
                            evidence += 2;
                            _tirLog.Add( $"Their ally's PTKO against our ally improves by 2 levels by helping hand. Supportive Status Evidence: {evidence}" );
                        }

                        if( theirAllyPTKO_OurAlly != PotentialToKO.OHKO && adjustedPTKO_OurAlly >= PotentialToKO.OHKO )
                        {
                            evidence += 1;
                            _tirLog.Add( $"Their ally PTKO on our ally is not OHKO, but it is with helping hand. Supportive Status Evidence: {evidence}" );
                        }

                        if( theirAllyMoveVsOurAllyIsSpread )
                        {
                            evidence += 1;
                            _tirLog.Add( $"Their ally's move is a spread move. Supportive Status Evidence: {evidence}" );
                        }

                        if( theirAlly.Speed > ourAlly.Speed || theirAllyMove_OurAlly.Priority > MovePriority.Zero )
                        {
                            evidence += 1;
                            _tirLog.Add( $"Their ally outspeeds our ally. Supportive Status Evidence: {evidence}" );
                        }

                        if( theirAllyMoveVsUsIsSpread && theirAllyMoveVsOurAllyIsSpread )
                        {
                            if( theirAllyPTKO_Us >= PotentialToKO.Risky && theirAllyPTKO_OurAlly >= PotentialToKO.Risky )
                            {
                                evidence += 1;
                                _tirLog.Add( $"Their ally's spread move PTKOs without helping hand are Risky or more. Supportive Status Evidence: {evidence}" );
                            }
                        }
                    }
                }

                if( isAfterYou && theirAlly != null )
                {
                    _tirLog.Add( $"They may want to use After You on their ally." );
                    //--Both of these need their own unique evidence contributions! todo immediately!
                    List<IBattleAIUnit> speedOrder = new()
                    {
                        us,
                        threat,
                    };

                    if( theirAlly != null )
                        speedOrder.Add( theirAlly );

                    if( ourAlly != null )
                        speedOrder.Add( ourAlly );

                    var afterYouUser = threat;
                    var attacker = theirAlly;

                    var afterYouUserSP = _ai.Projection.GetStrategicProfile( afterYouUser );
                    var attackerSP = _ai.Projection.GetStrategicProfile( attacker );

                    var attackerPTKO_Us = threatAllyVS_Us.AttackerPTKO;
                    var attackerPTKO_OurAlly = ourAlly != null ? threatAllyVS_OurAlly.AttackerPTKO : PotentialToKO.Untouchable;

                    speedOrder = speedOrder.OrderByDescending( u => u.Speed ).ThenByDescending( u => u.Pokemon.PokeSO.Speed ).ThenByDescending( u => u.Pokemon == afterYouUser.Pokemon || u.Pokemon == attacker.Pokemon ).ToList();

                    int order = 0;
                    foreach( var unit in speedOrder )
                    {
                        order++;

                        if( unit.Pokemon == attacker.Pokemon )
                            break;
                    }

                    bool theirAttackerIsSlowest = false;
                    bool theirAllyBenefitsAfterYou = false;

                    if( order == speedOrder.Count )
                        theirAttackerIsSlowest = true;
                    else if( order == speedOrder.Count - 1 )
                        theirAllyBenefitsAfterYou = true;

                    bool afterYouUserGoesFirst = speedOrder[0].Pokemon == afterYouUser.Pokemon;
                    bool afterYouUserGoesSecond = speedOrder[1].Pokemon == afterYouUser.Pokemon;

                    bool attackerDoesGoodDamage = attackerPTKO_Us >= PotentialToKO.Dangerous || attackerPTKO_OurAlly >= PotentialToKO.Dangerous;

                    bool attackerUsesSpreadMove_Us = threatAllyVS_Us.AttackerMTR.Move.MoveSO.MoveTarget == MoveTarget.AllAdjacent || threatAllyVS_Us.AttackerMTR.Move.MoveSO.MoveTarget == MoveTarget.OpposingSide;
                    bool attackerUsesSpreadMove_OurAlly = threatAllyVS_Us.AttackerMTR.Move.MoveSO.MoveTarget == MoveTarget.AllAdjacent || threatAllyVS_Us.AttackerMTR.Move.MoveSO.MoveTarget == MoveTarget.OpposingSide;
                    bool attackerUsesSpread = attackerUsesSpreadMove_Us || attackerUsesSpreadMove_OurAlly;
                    bool attackerUsesSpreadBoth = attackerUsesSpreadMove_Us && attackerUsesSpreadMove_OurAlly;
                    
                    bool attackerSpreadHitsHard = attackerUsesSpread && ( ( attackerPTKO_Us >= PotentialToKO.Dangerous && attackerPTKO_OurAlly >= PotentialToKO.TwoHKO ) || ( attackerPTKO_Us >= PotentialToKO.TwoHKO && attackerPTKO_OurAlly >= PotentialToKO.Dangerous ) || ( attackerPTKO_Us >= PotentialToKO.Risky && attackerPTKO_OurAlly >= PotentialToKO.Risky ) );
                    bool attackerSpreadThreatensSevereDamage = attackerUsesSpreadBoth && attackerPTKO_Us >= PotentialToKO.Dangerous && attackerPTKO_OurAlly >= PotentialToKO.Dangerous;
                    
                    bool attackerIsNaturallySlow = attacker.RoleProfile.Biases.Contains( RoleBias.SlowSpeed ) || attacker.RoleProfile.Biases.Contains( RoleBias.TrickRoomSpeed );
                    bool attackerDependsOnAlly = attackerSP.DependsOnFastAlly;

                    bool afterYouUserIsFast = afterYouUser.RoleProfile.Biases.Contains( RoleBias.MiddlingSpeed ) || afterYouUser.RoleProfile.Biases.Contains( RoleBias.FastSpeed );
                    bool afterYouUsersSpeedIsCurrentlyBoosted = _ai.UnitSim.PokemonHas_MatchingWeatherSpeedAbility( afterYouUser.Pokemon, currentField.Weather ) || theirCourt.ContainsKey( CourtConditionID.Tailwind ) || afterYouUser.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.Unburden ) || afterYouUser.StatStages[Stat.Speed] > 0;

                    if( theirAttackerIsSlowest )
                    {
                        evidence += 3;
                        _tirLog.Add( $"Their ally is the slowest unit on the field. Supportive Status Evidence: {evidence}" );
                    }
                    else if( theirAllyBenefitsAfterYou )
                    {
                        evidence += 2;
                        _tirLog.Add( $"Their ally directly benefits from After You. Supportive Status Evidence: {evidence}" );
                    }
                    else
                    {
                        evidence -= 5;
                        _tirLog.Add( $"Their ally doesn't benefit from After You at all. Supportive Status Evidence: {evidence}" );
                    }

                    if( attackerIsNaturallySlow )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their ally is a naturally slow speed tier. Supportive Status Evidence: {evidence}" );
                    }

                    if( attackerDependsOnAlly )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their ally depends on them being fast and/or using After You. Supportive Status Evidence: {evidence}" );
                    }

                    if( afterYouUserIsFast )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their after you is fast. Supportive Status Evidence: {evidence}" );
                    }

                    if( afterYouUsersSpeedIsCurrentlyBoosted )
                    {
                        evidence += 2;
                        _tirLog.Add( $"Their after you user's speed is currently boosted (by one of tailwind, weather, or stat boost). Supportive Status Evidence: {evidence}" );
                    }

                    if( afterYouUserGoesFirst )
                    {
                        evidence += 2;
                        _tirLog.Add( $"Their after you user goes first. Supportive Status Evidence: {evidence}" );
                    }
                    else if( afterYouUserGoesSecond )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their after you user goes second. Supportive Status Evidence: {evidence}" );
                    }

                    if( attackerDoesGoodDamage )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their ally does good damage. Supportive Status Evidence: {evidence}" );
                    }

                    if( attackerUsesSpread )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their ally uses a spread move. Supportive Status Evidence: {evidence}" );
                    }

                    if( attackerSpreadThreatensSevereDamage )
                    {
                        evidence += 2;
                        _tirLog.Add( $"Their ally's spread move threatens severe damage. Supportive Status Evidence: {evidence}" );
                    }
                    else if( attackerSpreadHitsHard )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Their ally's spread move does good damage. Supportive Status Evidence: {evidence}" );
                    }
                }
            }

            if( statusType == SupportiveStatusType.AllyProtection )
            {
                _tirLog.Add( $"They are looking to protect their ally." );

                if( threatAllyVS_Us.OpponentPTKO >= PotentialToKO.Risky && threatAllyVS_Us.OpponentMovesFirst )
                {
                    evidence += 1;
                    _tirLog.Add( $"We can do big damage to their ally and we move first, so they will want to redirect our attack. Supportive Status Evidence: {evidence}" );

                    if( threatVS_Us.OpponentPTKO >= PotentialToKO.Risky )
                    {
                        evidence += 1;
                        _tirLog.Add( $"We do big damage to them if they redirect. Supportive Status Evidence: {evidence}" );
                    }
                    else if( threatVS_Us.OpponentPTKO < PotentialToKO.Safe )
                    {
                        evidence += 3;
                        _tirLog.Add( $"They shrug off our attack with ease if they redirect. Supportive Status Evidence: {evidence}" );
                    }
                    else if( threatVS_Us.OpponentPTKO < PotentialToKO.Risky )
                    {
                        evidence += 2;
                        _tirLog.Add( $"They take our attack well if they redirect. Supportive Status Evidence: {evidence}" );
                    }
                }

                if( threatAllyVS_Us.AttackerPTKO >= PotentialToKO.Risky && threatAllyVS_Us.OpponentMovesFirst )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their ally is capable of doing big damage but we move first. Redirecting enables their ally to do that damage safely. Supportive Status Evidence: {evidence}" );

                    if( threatVS_Us.OpponentPTKO >= PotentialToKO.Risky )
                    {
                        evidence += 1;
                        _tirLog.Add( $"We do big damage to them if they redirect. Supportive Status Evidence: {evidence}" );
                    }
                    else if( threatVS_Us.OpponentPTKO < PotentialToKO.Safe )
                    {
                        evidence += 3;
                        _tirLog.Add( $"They shrug off our attack with ease if they redirect. Supportive Status Evidence: {evidence}" );
                    }
                    else if( threatVS_Us.OpponentPTKO < PotentialToKO.Risky )
                    {
                        evidence += 2;
                        _tirLog.Add( $"They take our attack well if they redirect. Supportive Status Evidence: {evidence}" );
                    }
                }
            }

            //--Doubles Specific Checks
            if( statusEEPack.OurAllyExists )
            {
                _tirLog.Add( $"" );
                _tirLog.Add( $"This is a double battle! Checking Supportive Status intent evidence in the context of doubles..." );

                _tirLog.Add( $"Their ({tic.Threat.Name}) ally is: {theirAlly.Name} ({theirAlly.RoleProfile.PrimaryRole})" );
                _tirLog.Add( $"" );

                //--Our Ally Information
                bool weHaveAlly = ourAlly != null;
                if( weHaveAlly )
                {
                    _tirLog.Add( $"Our ({_ai.CurrentUnitAdapter.Name}) ally is: {ourAlly.Name} ({ourAlly.RoleProfile.PrimaryRole})" );
                    _tirLog.Add( $"" );
                }

                bool weCanProtect = _ai.UnitSim.CheckHasMove( _ai.CurrentUnitAdapter, "Protect" ) && _ai.GetBattleUnit( _ai.CurrentUnitAdapter.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;
                bool ourAllyCanProtect = weHaveAlly && _ai.UnitSim.CheckHasMove( ourAlly, "Protect" ) && _ai.GetBattleUnit( ourAlly.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;
                bool ourAllyCanFakeOut = weHaveAlly && _ai.CanUseFakeOut( ee.Opponent, tic.Threat );
                bool ourAllyCanTaunt = weHaveAlly && ourAlly.RoleProfile.Traits.Contains( RoleTrait.Taunt );
                bool ourAllyCanEncore = weHaveAlly && ourAlly.RoleProfile.Traits.Contains( RoleTrait.Encore );

                //--Their Ally Information
                var theirAllyRP = theirAlly.RoleProfile;
                var theirAllyTraits = theirAllyRP.Traits;

                bool theirAllyIsOffensive = theirAllyRP.PrimaryRole == RoleClass.BulkyAttacker || theirAllyRP.PrimaryRole == RoleClass.RevengeKiller || theirAllyRP.PrimaryRole == RoleClass.SetupSweeper ||
                theirAllyRP.PrimaryRole == RoleClass.Sweeper || theirAllyRP.PrimaryRole == RoleClass.TrickRoomAbuser || theirAllyRP.PrimaryRole == RoleClass.WallBreaker;

                bool theirAllyIsDefensive = theirAllyRP.PrimaryRole == RoleClass.Wall || theirAllyRP.PrimaryRole == RoleClass.DefensiveSetup || theirAllyRP.PrimaryRole == RoleClass.BulkyAttacker && theirAllyRP.SecondaryRoles.Contains( RoleClass.Wall );
                bool theirAllyIsSupport = theirAllyRP.PrimaryRole == RoleClass.UtilitySupport || theirAllyRP.PrimaryRole == RoleClass.Disrupter;
                bool theirAllyIsUtility = !theirAllyIsOffensive && !theirAllyIsDefensive;

                bool theyCanProtect = _ai.UnitSim.CheckHasMove( tic.Threat, "Protect" ) && _ai.GetBattleUnit( tic.Threat.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;
                bool theirAllyCanProtect = _ai.UnitSim.CheckHasMove( theirAlly, "Protect" ) && _ai.GetBattleUnit( theirAlly.Pokemon ).Flags[UnitFlags.SuccessiveProtectUses].Count <= 0;

                bool theirAllyHas_FakeOut       = _ai.CanUseFakeOut( theirAlly, _ai.CurrentUnitAdapter );
                bool theirAllyHas_FollowMe      = _ai.UnitSim.CheckHasMove( theirAlly, "Follow Me" );
                bool theirAllyHas_RagePowder    = _ai.UnitSim.CheckHasMove( theirAlly, "Rage Powder" );
                bool theirAllyHas_SpeedControl  = theirAllyTraits.Contains( RoleTrait.SpeedControl );

                if( theirAllyHas_FakeOut )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their ally has Fake Out. Supportive Status Evidence: {evidence}" );
                }

                if( theirAllyHas_FollowMe )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their ally has Follow Me. Supportive Status Evidence: {evidence}" );
                }

                if( theirAllyHas_RagePowder )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their ally has Rage Powder. Supportive Status Evidence: {evidence}" );
                }

                if( theirAllyHas_SpeedControl )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their ally has Speed Control. Supportive Status Evidence: {evidence}" );
                }

                if( ourAllyCanFakeOut )
                {
                    evidence -= 2;
                    _tirLog.Add( $"Our ally can use Fake Out. Supportive Status Evidence: {evidence}" );
                }

                if( ourAllyCanTaunt )
                {
                    evidence -= 2;
                    _tirLog.Add( $"Our ally can use Taunt. Supportive Status Evidence: {evidence}" );
                }

                if( ourAllyCanEncore )
                {
                    evidence -= 2;
                    _tirLog.Add( $"Our ally can use Encore. Supportive Status Evidence: {evidence}" );
                }

            }

            //--Universal Signals
            //--Compare Their Before and After
            if( ee.AttackerPTKO < threatVS_Us.AttackerPTKO )
            {
                evidence += 1;
                _tirLog.Add( $"Their PTKO improves from support. Supportive Status Evidence: {evidence}" );
            }

            if( ee.OpponentPTKO > threatVS_Us.OpponentPTKO )
            {
                evidence += 1;
                _tirLog.Add( $"Their survivability improves from support. Supportive Status Evidence: {evidence}" );
            }

            if( ee.OpponentMovesFirst && threatVS_Us.AttackerMovesFirst )
            {
                evidence += 1;
                _tirLog.Add( $"Their speed turn order improves from support. Supportive Status Evidence: {evidence}" );
            }

            //--Compare Their Ally's Before and After
            if( threatAllyVS_Us.AttackerPTKO < threatAllyVS_UsAfter.AttackerPTKO )
            {
                evidence += 1;
                _tirLog.Add( $"Their ally's PTKO improves from support. Supportive Status Evidence: {evidence}" );
            }

            if( threatAllyVS_Us.OpponentPTKO > threatAllyVS_UsAfter.OpponentPTKO )
            {
                evidence += 1;
                _tirLog.Add( $"Their ally's survivability improves from support. Supportive Status Evidence: {evidence}" );
            }

            if( threatAllyVS_Us.OpponentMovesFirst && threatAllyVS_UsAfter.AttackerMovesFirst )
            {
                evidence += 1;
                _tirLog.Add( $"Their ally's speed turn order improves from support. Supportive Status Evidence: {evidence}" );
            }

            //--Opposing Intents
            //--Setup vs Support
            bool offensiveSetup = theirTraits.Contains( RoleTrait.SpeciallyOffensiveSetup ) || theirTraits.Contains( RoleTrait.PhysicallyOffensiveSetup );
            bool defensiveSetup = theirTraits.Contains( RoleTrait.SpeciallyDefensiveSetup ) || theirTraits.Contains( RoleTrait.PhysicallyDefensiveSetup );
            bool hasSetup = offensiveSetup || defensiveSetup;

            bool freeTurn = suppStatusTOP.OpponentPTKO <= PotentialToKO.Safe;
            if( hasSetup && freeTurn )
            {
                evidence -= 2;
                _tirLog.Add( $"They have a setup move and a free turn, setting up is likely more valuable right now. Supportive Status Evidence: {evidence}" );
            }

            //--Offensive vs Supportive Status investment
            if( tic.OffensiveStatusThreatResult.Move != null )
            {
                var offStatusTOP = tic.OffensiveStatusThreatResult.Top;
                var moveEffects = tic.OffensiveStatusThreatResult.Move.MoveSO.MoveEffects;

                bool theyCreatePressureState = false;
                bool theyCreateDecisiveState = false;

                //--Status Effect Evidence. Toxic, Taunt, etc.
                if( status.OffensiveStatusType == OffensiveStatusType.StatusEffect || status.OffensiveStatusType == OffensiveStatusType.Disruption )
                {
                    _tirLog.Add( $"They are looking to use a move with a status effect." );

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

                    bool weAreTauntWeak = ourTraits.Contains( RoleTrait.TauntWeak );
                    bool weAreEncoreWeak = ourTraits.Contains( RoleTrait.EncoreWeak );
                    bool weAreHealBlockWeak = ourTraits.Contains( RoleTrait.RecoveryMove );
                    bool weAreDisableWeak = offStatusTOP.Opponent.VolatileStatuses.Contains( VolatileConditionID.ChoiceLocked ) || offStatusTOP.Opponent.RoleProfile.Signals.PhysicalAttackCount < 2 || offStatusTOP.Opponent.RoleProfile.Signals.SpecialAttackCount < 2;

                    theyCreatePressureState = theyBurn || theyFrost || theyToxic;
                    theyCreateDecisiveState = theySleep || theyParalyze || theyEncore || theyTaunt || theirTP.ForcesSwitch;

                    if( theyCreatePressureState )
                    {
                        evidence -= 1;
                        _tirLog.Add( $"Their offensive status creates a pressure state. Supportive Status Evidence: {evidence}" );
                    }

                    if( theyCreateDecisiveState )
                    {
                        evidence -= 2;
                        _tirLog.Add( $"Their offensive status creates a decisive state. Supportive Status Evidence: {evidence}" );
                    }
                }

                //--Attacking is better pull
                if( ee.AttackerPTKO == PotentialToKO.OHKO )
                {
                    evidence -= 1;
                    _tirLog.Add( $"Their support unit can actually gain a KO on us, which may be better than using a support move. Supportive Status Evidence: {evidence}" );
                }

                //--We're thinking about switching pull
                if( ee.AttackerSwitchProbability > 0.8f )
                {
                    evidence -= 1;
                    _tirLog.Add( $"They have a high switch probability, they might not support. Supportive Status Evidence: {evidence}" );
                }

                //--Protect over Support
                //--goes here once the protect decision line and protect intents are implemented
            }
        }
        else
        {
            evidence = -99;
            _tirLog.Add( $"Supportive Status candidate not found! Nullifying evidence. Supportive Status Evidence: {evidence}" );
        }

        _tirLog.Add( $"" );

        return evidence;
    }

}

public struct ThreatIntentResult
{
    public IBattleAIUnit Threat;
    public Intent PrimaryIntent;
    public Intent SecondaryIntent;
    public int TotalEvidence;
    public float Confidence;

    public bool CheckSecondaryIntent;

    public Dictionary<ActionType, int> IntentEvidence;
}

public struct Intent
{
    public ActionType ActionType;
    public IActionResult IntentResult;
    public int Evidence;
}

public struct ThreatIntentCandidates
{
    public IBattleAIUnit Threat;
    public MoveThreatResult MoveThreatResult;
    public SwitchCandidateResult DefensiveSwitchCandidateResult;
    public SwitchCandidateResult OffensiveSwitchCandidateResult;
    public SwitchPredictionResult SwitchPredictions;
    public SetupThreatResult SetupThreatResult;
    public StatusThreatResult OffensiveStatusThreatResult;
    public StatusThreatResult SupportiveStatusThreatResult;
    public StatusThreatResult ProtectThreatResult;
}

public struct ThreatBrain
{
    public ExchangeEvaluation TheirEE;
    public BoardContext TheirBC;
    public ThreatProfile OurTP;
    public GamePlan TheirGP;
    public CurrentPlan TheirCP;
}

public struct SwitchPredictionScore
{
    public Pokemon Candidate;

    public float Score;

    public int SafetyScore;
    public int PressureScore;
    public int ResourceScore;
    public int PlanScore;
}

public struct SwitchPredictionResult
{
    public bool DefensiveSwitchesAvailable;
    public bool OffensiveSwitchesAvailable;
    public bool NoSwitchCandidatesAvailable;

    public Dictionary<Pokemon, SwitchCandidateResult> DefensiveCandidates;
    public Dictionary<Pokemon, SwitchCandidateResult> OffensiveCandidates;

    public SwitchPredictionScore PrimaryDefensive;
    public SwitchPredictionScore SecondaryDefensive;

    public SwitchPredictionScore PrimaryOffensive;
    public SwitchPredictionScore SecondaryOffensive;

    public float DefensiveConfidence;
    public float OffensiveConfidence;

    public List<SwitchPredictionScore> DefensiveScores;
    public List<SwitchPredictionScore> OffensiveScores;
}
