using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using UnityEngine;

public class BattleAI_ThreatIntent
{
    private readonly BattleAI _ai;
    private CustomLogSession _tirLog;
    private CustomLogSession _switchPredLog;

    public BattleAI_ThreatIntent( BattleAI ai )
    {
        _ai = ai;
    }

    public ThreatBrain ReadThreatBrain( IBattleAIUnit threat )
    {
        var theirEE = _ai.Projection.EvaluateExchange( threat, _ai.CurrentUnitAdapter );
        var theirBC = _ai.Projection.GetBoardContext( threat, _ai.CurrentUnitAdapter, theirEE );
        var ourTP = _ai.GetThreatProfile( theirEE, theirBC, _ai.CurrentUnitAdapter ); //--this is our profile as a threat to them, from their perspective
        var theirGP = _ai.GetOpponentGamePlan( _ai.GamePlan );
        var theirCP = _ai.Projection.EvaluateCurrentPlan( theirEE, theirBC, ourTP, theirGP, null );

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

        _switchPredLog = new();

        _switchPredLog.Add( $"" );
        _switchPredLog.Add( $"====================================================" );
        _switchPredLog.Add( $"=====[Predicting Most Likely Switch Candidates]=====" );
        _switchPredLog.Add( $"====================================================" );
        _switchPredLog.Add( $"" );

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

        Debug.Log( _switchPredLog.ToString() );
        string path = Application.persistentDataPath + "/SwitchPrediction_Log.txt";
        System.IO.File.AppendAllText( path, _switchPredLog.ToString() + "\n" + "\n" + "\n" + "\n" + "\n" );
        _switchPredLog.Clear();

        ThreatIntentCandidates tic = new()
        {
            Threat = threat,
            MoveThreatResult = _ai.MoveCommand.GetMove_BestAttack( threat, us ),
            DefensiveSwitchCandidateResult = defSCR,
            OffensiveSwitchCandidateResult = offSCR,
            SetupThreatResult = _ai.MoveCommand.GetMove_Setup( threat, us ),
            OffensiveStatusThreatResult = _ai.MoveCommand.GetMove_OffensiveStatus( threat, us ),
            // SupportiveStatusThreatResult
            // ProtectThreatResult
        };

        return tic;
    }

    private SwitchPredictionResult SelectLikely_DefensiveSwitch( List<SwitchCandidateResult> candidates, ref SwitchPredictionResult spr, ThreatBrain tb, IBattleAIUnit threat )
    {
        _switchPredLog.Add( $"" );
        _switchPredLog.Add( $"=========================================" );
        _switchPredLog.Add( $"===[Selecting Likely Defensive Switch]===" );
        _switchPredLog.Add( $"=========================================" );
        _switchPredLog.Add( $"" );

        foreach( var cand in candidates )
        {
            int safety = 0;
            int pressure = 0;
            int resource = 0;
            int plan = 0;

            var top = cand.Top;
            var candidateAdapter = _ai.GetPokemonAs_Adapter( cand.Pokemon );
            _switchPredLog.Add( $"=[{cand.Pokemon.NickName}]=" );

            //--Safety
            //--Candidate survives comfortably
            if( top.OpponentPTKO <= PotentialToKO.Safe )
            {
                safety += 2;
                _switchPredLog.Add( $"Candidate survives comfortably. Safety: {safety}" );
            }
            //--Candidate survives but isn't amazing
            else if( top.OpponentPTKO < PotentialToKO.Risky )
            {
                safety += 1;
                _switchPredLog.Add( $"Candidate survives somewhat. Safety: {safety}" );
            }
            //--Candidate still gets blown up
            else if( top.OpponentPTKO == PotentialToKO.Dangerous )
            {
                safety -= 2;
                _switchPredLog.Add( $"Candidate takes huge damage. Safety: {safety}" );
            }
            //--OHKO
            else if( top.OpponentPTKO == PotentialToKO.OHKO )
            {
                safety -= 3;
                _switchPredLog.Add( $"Candidate dies. Safety: {safety}" );
            }

            //--Pressure
            //--Candidate threatens current opponent
            if( top.AttackerPTKO <= PotentialToKO.Safe )
            {
                pressure -= 1;
                _switchPredLog.Add( $"Candidate does not threaten us. Pressure: {pressure}" );
            }
            //--Threatens immediate KO
            else if( top.AttackerPTKO >= PotentialToKO.Dangerous )
            {
                pressure += 2;
                _switchPredLog.Add( $"Candidate really threatens us. Pressure: {pressure}" );
            }
            //--Can enter safely but does nothing
            else if( top.AttackerPTKO >= PotentialToKO.TwoHKO )
            {
                pressure += 1;
                _switchPredLog.Add( $"Candidate kinda threatens us. Pressure: {pressure}" );
            }

            //--Resource
            //--Low expendability
            if( threat.Expendability <= 0.3f && candidateAdapter.Expendability >= 0.3f )
            {
                resource += 1;
                _switchPredLog.Add( $"Their current unit is not very expendible, and their tank is more expendible. Resource: {resource}" );
            }

            //--Hazard removal
            var theirCourt = candidateAdapter.CourtLocation == CourtLocation.TopCourt ? _ai.CurrentFieldSnapshot.TopCourtConditions : _ai.CurrentFieldSnapshot.BottomCourtConditions;
            bool hazardsExist = theirCourt.ContainsKey( CourtConditionID.LeechSeed ) || theirCourt.ContainsKey( CourtConditionID.Spikes ) || theirCourt.ContainsKey( CourtConditionID.StealthRock ) || theirCourt.ContainsKey( CourtConditionID.StickyWeb ) || theirCourt.ContainsKey( CourtConditionID.ToxicSpikes );
            bool weAreHazardSetter = _ai.CurrentUnitAdapter.RoleProfile.Traits.Contains( RoleTrait.HazardSetter );

            if( candidateAdapter.RoleProfile.Traits.Contains( RoleTrait.HazardRemover ) && ( hazardsExist || weAreHazardSetter ) )
            {
                resource += 1;
                _switchPredLog.Add( $"Their candidate is a hazard remover and we have either set hazards on their side or we are a hazard setter. Resource: {resource}" );
            }

            //--Weather setter
            bool switchSetsWeather = _ai.UnitSim.PokemonHasWeatherSetter_Ability( cand.Pokemon );
            bool switchChangesWeather = false;
            WeatherConditionID candidatesWeather = WeatherConditionID.None;

            if( switchSetsWeather )
            {
                switch( cand.Pokemon.AbilityID )
                {
                    case AbilityID.Drought: candidatesWeather = WeatherConditionID.SUNNY; break;
                    case AbilityID.Drizzle: candidatesWeather = WeatherConditionID.RAIN; break;
                    case AbilityID.Sandstream: candidatesWeather = WeatherConditionID.SANDSTORM; break;
                    case AbilityID.SnowWarning: candidatesWeather = WeatherConditionID.SNOW; break;
                }

                if( candidatesWeather != WeatherConditionID.None && candidatesWeather != _ai.CurrentFieldSnapshot.Weather )
                    switchChangesWeather = true;
            }

            if( switchChangesWeather )
            {
                resource += 1;
                _switchPredLog.Add( $"Their candidate changes the weather likely in their favor. Resource: {resource}" );
            }

            //--Future FocusMon support
            var cp = tb.TheirCP;
            if( cand.Pokemon == cp.FocusMon )
            {
                resource += 2;
                _switchPredLog.Add( $"Their candidate is their current plan's FocusMon. Resource: {resource}" );
            }

            //--Plan
            //--Wall switching into threat
            var ourPR = _ai.CurrentUnitAdapter.RoleProfile.PrimaryRole;
            bool weAreOffensive = ourPR == RoleClass.BulkyAttacker || ourPR == RoleClass.RevengeKiller || ourPR == RoleClass.SetupSweeper || ourPR == RoleClass.Sweeper || ourPR == RoleClass.TrickRoomAbuser;
            if( candidateAdapter.RoleProfile.PrimaryRole == RoleClass.Wall && weAreOffensive )
            {
                plan += 1;
                _switchPredLog.Add( $"Their candidate is a wall and we are an offensive unit. Plan: {plan}" );

                if( ourPR == RoleClass.WallBreaker )
                {
                    plan -= 1;
                    _switchPredLog.Add( $"However, we are a wallbreaker. Nullifying previous reward. Plan: {plan}" );
                }
            }

            //--Pivot
            if( candidateAdapter.RoleProfile.PrimaryRole == RoleClass.Pivot )
            {
                plan += 1;
                _switchPredLog.Add( $"Their candidate is a pivot. Plan: {plan}" );
            }

            //--Weather Abuser
            bool benefitsCurrentWeather = _ai.UnitSim.Get_WeatherContextScore( cand.Pokemon ) > 0;
            if( benefitsCurrentWeather )
            {
                plan += 1;
                _switchPredLog.Add( $"Their candidate benefits from the current weather. Plan: {plan}" );

                WeatherConditionID threatsWeather = WeatherConditionID.None;

                switch( threat.Ability )
                {
                    case AbilityID.Drought: threatsWeather = WeatherConditionID.SUNNY; break;
                    case AbilityID.Drizzle: threatsWeather = WeatherConditionID.RAIN; break;
                    case AbilityID.Sandstream: threatsWeather = WeatherConditionID.SANDSTORM; break;
                    case AbilityID.SnowWarning: threatsWeather = WeatherConditionID.SNOW; break;
                }

                if( threatsWeather != WeatherConditionID.None && threatsWeather == _ai.CurrentFieldSnapshot.Weather )
                {
                    plan += 1;
                    _switchPredLog.Add( $"Their current pokemon likely set the weather their candidate benefits from. Plan: {plan}" );
                }
            }

            //--Final Score
            float tieBreaker = ( safety * 0.01f ) + ( plan * 0.01f );
            float final = safety + pressure + resource + plan;
            final += tieBreaker;

            _switchPredLog.Add( $"" );
            _switchPredLog.Add( $"Safety: {safety}" );
            _switchPredLog.Add( $"Pressure: {pressure}" );
            _switchPredLog.Add( $"Resource: {resource}" );
            _switchPredLog.Add( $"Plan: {plan}" );
            _switchPredLog.Add( $"Tie Breaker Value: {tieBreaker}. Final Score: {final}" );
            _switchPredLog.Add( $"" );

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

        _switchPredLog.Add( $"Primary Defensive Candidate: {spr.PrimaryDefensive.Candidate.NickName}" );
        _switchPredLog.Add( $"Secondary Defensive Candidate: {spr.SecondaryDefensive.Candidate.NickName}" );
        _switchPredLog.Add( $"Confidence: {spr.DefensiveConfidence}" );
        _switchPredLog.Add( $"" );

        return spr;
    }

    private SwitchPredictionResult SelectLikely_OffensiveSwitch( List<SwitchCandidateResult> candidates, ref SwitchPredictionResult spr, ThreatBrain tb, IBattleAIUnit threat )
    {
        _switchPredLog.Add( $"" );
        _switchPredLog.Add( $"=========================================" );
        _switchPredLog.Add( $"===[Selecting Likely Offensive Switch]===" );
        _switchPredLog.Add( $"=========================================" );
        _switchPredLog.Add( $"" );

        foreach( var cand in candidates )
        {
            int safety = 0;
            int pressure = 0;
            int resource = 0;
            int plan = 0;

            var top = cand.Top;
            var candidateAdapter = _ai.GetPokemonAs_Adapter( cand.Pokemon );

            _switchPredLog.Add( $"=[{cand.Pokemon.NickName}]=" );

            //--Safety
            //--Candidate survives comfortably
            if( top.OpponentPTKO <= PotentialToKO.Safe )
            {
                safety += 2;
                _switchPredLog.Add( $"Their candidate survives comfortably. Safety: {safety}" );
            }
            //--Candidate survives but isn't amazing
            else if( top.OpponentPTKO < PotentialToKO.Risky )
            {
                safety += 1;
                _switchPredLog.Add( $"Their candidate barely survives. Safety: {safety}" );
            }
            //--Candidate still gets blown up
            else if( top.OpponentPTKO == PotentialToKO.Dangerous )
            {
                safety -= 2;
                _switchPredLog.Add( $"Their candidate takes huge damage. Safety: {safety}" );
            }
            //--OHKO
            else if( top.OpponentPTKO == PotentialToKO.OHKO )
            {
                safety -= 3;
                _switchPredLog.Add( $"Their candidate dies. Safety: {safety}" );
            }

            //--Pressure
            //--Threatens Immediate KO
            if( top.AttackerPTKO >= PotentialToKO.Dangerous)
            {
                pressure += 2;
                _switchPredLog.Add( $"Their candidate threatens an immediate KO on us. Pressure: {pressure}" );
            }
            else if( top.AttackerPTKO >= PotentialToKO.TwoHKO )
            {
                pressure += 1;
                _switchPredLog.Add( $"Their candidate threatens reasonable damage on us. Pressure: {pressure}" );
            }

            if( top.Attacker.Speed > top.Opponent.Speed )
            {
                pressure += 1;
                _switchPredLog.Add( $"Their candidate outspeeds our current pokemon. Pressure: {pressure}" );
            }

            if( candidateAdapter.RoleProfile.PrimaryRole == RoleClass.RevengeKiller || candidateAdapter.RoleProfile.PrimaryRole == RoleClass.Pivot || candidateAdapter.RoleProfile.PrimaryRole == RoleClass.WallBreaker )
            {
                pressure += 1;
                _switchPredLog.Add( $"Their candidate is a tempo-based offensive unit. Pressure: {pressure}" );
            }

            if( top.AttackerPTKO >= PotentialToKO.Risky && top.OpponentPTKO <= PotentialToKO.Safe )
            {
                pressure += 2;
                _switchPredLog.Add( $"Their candidate has offensive and defensive control over the board. Pressure: {pressure}" );
            }
            else if( top.AttackerPTKO > top.OpponentPTKO )
            {
                pressure += 2;
                _switchPredLog.Add( $"Their candidate has an offensive advantage over us. Pressure: {pressure}" );
            }

            //--Resource
            if( cand.Pokemon == tb.TheirCP.FocusMon )
            {
                resource += 2;
                _switchPredLog.Add( $"Their candidate is their current plan's FocusMon. Resource: {resource}" );
            }

            float switchThreatCount = _ai.TheirTeamPieceValues.TryGetValue( top.Attacker.Pokemon, out var pieceValue ) ? pieceValue.ThreatCount : 0;
            float threatPercentage = switchThreatCount / _ai.TheirTeamAdapters.Count; //--this needs to be changed to be active threat count / remaining, not the team preview/full team percentage.
            if( threatPercentage >= 0.5f )
            {
                resource += 2;
                _switchPredLog.Add( $"Their candidate threatens at least half of our team. Resource: {resource}" );
            }
            else if( threatPercentage >= 0.25f )
            {
                resource += 1;
                _switchPredLog.Add( $"Their candidate threantes at least a quarter of our team. Resource: {resource}" );
            }

            if( candidateAdapter.Expendability >= 0.6f )
            {
                resource += 1;
                _switchPredLog.Add( $"Their candidate is pretty expendible. Resource: {resource}" );
            }

            //--Plan
            if( tb.TheirCP.Type == PlanType.EnableSweep )
            {
                if( cand.Pokemon == tb.TheirCP.FocusMon )
                {
                    plan += 1;
                    _switchPredLog.Add( $"They are looking to enable a sweep and their candidate is their current plan's FocusMon. Plan: {plan}" );
                }
            }

            bool gainsControl = _ai.CurrentUnitAdapter.Speed > threat.Speed && top.Attacker.Speed > top.Opponent.Speed;
            if( gainsControl )
            {
                plan += 1;
                _switchPredLog.Add( $"Their candidate gains the speed advantage over us. Plan: {plan}" );
            }
            
            //--Weather Abuser
            bool benefitsCurrentWeather = _ai.UnitSim.Get_WeatherContextScore( cand.Pokemon ) > 0;
            if( benefitsCurrentWeather )
            {
                plan += 2;
                _switchPredLog.Add( $"Their candidate benefits from the current weather. Plan: {plan}" );

                WeatherConditionID threatsWeather = WeatherConditionID.None;

                switch( threat.Ability )
                {
                    case AbilityID.Drought: threatsWeather = WeatherConditionID.SUNNY; break;
                    case AbilityID.Drizzle: threatsWeather = WeatherConditionID.RAIN; break;
                    case AbilityID.Sandstream: threatsWeather = WeatherConditionID.SANDSTORM; break;
                    case AbilityID.SnowWarning: threatsWeather = WeatherConditionID.SNOW; break;
                }

                if( threatsWeather != WeatherConditionID.None && threatsWeather == _ai.CurrentFieldSnapshot.Weather )
                {
                    plan += 1;
                    _switchPredLog.Add( $"Their current pokemon likely set the weather the candidate benefits from. Plan: {plan}" );
                }
            }

            //--Final Score
            float tieBreaker = ( safety * 0.01f ) + ( plan * 0.01f );
            float final = safety + pressure + resource + plan;
            final += tieBreaker;

            _switchPredLog.Add( $"" );
            _switchPredLog.Add( $"Safety: {safety}" );
            _switchPredLog.Add( $"Pressure: {pressure}" );
            _switchPredLog.Add( $"Resource: {resource}" );
            _switchPredLog.Add( $"Plan: {plan}" );
            _switchPredLog.Add( $"Tie Breaker Value: {tieBreaker}. Final Score: {final}" );
            _switchPredLog.Add( $"" );

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

        _switchPredLog.Add( $"Primary Offensive Candidate: {spr.PrimaryOffensive.Candidate?.NickName}" );
        _switchPredLog.Add( $"Secondary Offensive Candidate: {spr.SecondaryOffensive.Candidate?.NickName}" );
        _switchPredLog.Add( $"Confidence: {spr.OffensiveConfidence}" );
        _switchPredLog.Add( $"" );

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

        var theirEE = tb.TheirEE;
        var theirBC = tb.TheirBC;
        var theirBFS = theirBC.BattlefieldState;
        var ourTP =  tb.OurTP; //--this is our profile as a threat to them, from their perspective
        var theirGP = tb.TheirGP;
        var theirCP = tb.TheirCP;

        var ourEE = _ai.Projection.EvaluateExchange( _ai.CurrentUnitAdapter, tic.Threat );
        var ourBC = _ai.Projection.GetBoardContext( _ai.CurrentUnitAdapter, tic.Threat, ourEE );
        var theirTP = _ai.GetThreatProfile( ourEE, ourBC, tic.Threat ); //--this is their profile as a threat to us, from our perspective

        attack_evidence = AttackEvidence( tic, theirEE, theirBFS, theirTP, ourTP, theirCP );
        defSwitch_evidence = DefensiveSwitchEvidence( tic, theirEE, theirBFS, theirTP, ourTP, theirCP );
        offSwitch_evidence = OffensiveSwitchEvidence( tic, theirEE, theirBFS, theirTP, ourTP, theirCP );
        setup_Evidence = SetupEvidence( tic, theirEE, theirBFS, theirTP, ourTP, theirCP );
        offStatus_Evidence = OffensiveStatusEvidence( tic, theirEE, theirBFS, theirTP, ourTP, theirCP );

        Dictionary<IntentType, int> evidenceGathered = new()
        {
            { IntentType.Attack, attack_evidence }, //--There's always an attack. Eventually this will include struggle.
        };

        if( defSwitch_evidence != -99 )
            evidenceGathered.Add( IntentType.DefensiveSwitch, defSwitch_evidence );

        if( offSwitch_evidence != -99 )
            evidenceGathered.Add( IntentType.OffensiveSwitch, offSwitch_evidence );

        if( setup_Evidence != -99 )
            evidenceGathered.Add( IntentType.Setup, setup_Evidence );

        if( offStatus_Evidence != -99 )
            evidenceGathered.Add( IntentType.OffensiveStatus, offStatus_Evidence );

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

        IntentType secondaryIntent = IntentType.None;
        int secondaryEvidence = -99;

        if( sorted.Count > 1 )
        {
            secondaryIntent = sorted[1].Key;
            secondaryEvidence = sorted[1].Value;
        }

        tir.PrimaryIntent = new(){ IntentType = primaryIntent, Evidence = primaryEvidence };
        tir.SecondaryIntent = new(){ IntentType = secondaryIntent, Evidence = secondaryEvidence };

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
            switch( intent.IntentType )
            {
                case IntentType.Attack:
                    intent.IntentResult = tic.MoveThreatResult;
                    var attackCand = (MoveThreatResult)intent.IntentResult;
                    _tirLog.Add( $"With move: {attackCand.Move.MoveSO.Name}" );
                break;

                case IntentType.DefensiveSwitch:
                    intent.IntentResult = tic.DefensiveSwitchCandidateResult;
                    var defCand = (SwitchCandidateResult)intent.IntentResult;
                    _tirLog.Add( $"With defensive switch candidate: {defCand.Pokemon.NickName}" );
                break;

                case IntentType.OffensiveSwitch:
                    intent.IntentResult = tic.OffensiveSwitchCandidateResult;
                    var offCand = (SwitchCandidateResult)intent.IntentResult;
                    _tirLog.Add( $"With offensive switch candidate: {offCand.Pokemon.NickName}" );
                break;

                case IntentType.Setup:
                    intent.IntentResult = tic.SetupThreatResult;
                    var setupCand = (SetupThreatResult)intent.IntentResult;
                    _tirLog.Add( $"With move: {setupCand.Move.MoveSO.Name}" );
                break;

                case IntentType.OffensiveStatus:
                    intent.IntentResult = tic.OffensiveStatusThreatResult;
                    var offStatusCand = (StatusThreatResult)intent.IntentResult;
                    _tirLog.Add( $"With move: {offStatusCand.Move.MoveSO.Name}" );
                break;
                
                default:
                    intent.IntentResult = tic.MoveThreatResult;
                break;
            };
        }

        _tirLog.Add( $"================================" );

        Debug.Log( _tirLog.ToString() );
        string path = Application.persistentDataPath + "/ThreatIntentResult_Log.txt";
        System.IO.File.AppendAllText( path, _tirLog.ToString() + "\n" + "\n" + "\n" + "\n" + "\n" );
        _tirLog.Clear();

        return tir;
    }

    private int AttackEvidence( ThreatIntentCandidates tic, ExchangeEvaluation ee, BattlefieldState bfs, ThreatProfile theirTP, ThreatProfile ourTP, CurrentPlan theirCP )
    {
        int evidence = 0;

        var attackTOP = tic.MoveThreatResult.Top;

        //---------------------------------------------------------------------------------
        //--Attack Evidence Gathering------------------------------------------------------
        //---------------------------------------------------------------------------------
        _tirLog.Add( $"===[Attack Evidence]===" );
        var theirRP = attackTOP.Attacker.RoleProfile;
        bool theyAreOffensive = theirRP.PrimaryRole == RoleClass.BulkyAttacker || theirRP.PrimaryRole == RoleClass.RevengeKiller || theirRP.PrimaryRole == RoleClass.SetupSweeper ||
            theirRP.PrimaryRole == RoleClass.Sweeper || theirRP.PrimaryRole == RoleClass.TrickRoomAbuser || theirRP.PrimaryRole == RoleClass.WallBreaker;

        var theirCourt = attackTOP.Attacker.CourtLocation  == CourtLocation.TopCourt ? _ai.CurrentFieldSnapshot.TopCourtConditions : _ai.CurrentFieldSnapshot.BottomCourtConditions;

        bool theyCanUseFakeOut = _ai.CanUseFakeOut( attackTOP.Attacker, attackTOP.Opponent );

        int ourRemainingCount = _ai.OurTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList().Count;
        int theirRemainingCount = _ai.TheirTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList().Count;
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

        if( attackTOP.Attacker.BeginningHPR >= 0.8f )
        {
            evidence += 1;
            _tirLog.Add( $"Their hp is >= 80%. Attack Evidence: {evidence}" );
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
            bool opponentChipsSelf = ( _ai.UnitSim.CheckHasRecoilMove( attackTOP.Attacker.ActiveMoves ) || attackTOP.Attacker.Item == BattleItemEffectID.LifeOrb ) && attackTOP.AttackerMovedFirst;

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
        if( hpAfterHazards <= 0f )
        {
            evidence += 1;
            _tirLog.Add( $"If they recall their unit, it will faint to hazards when they switch it in later if they don't clear them. Attack Evidence: {evidence}" );
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
            if( _ai.GamePlan.OurBlockers.Contains( attackTOP.Opponent.Pokemon ) )
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

        if( _ai.IsDoubleBattle )
        {
            _tirLog.Add( $"" );
            _tirLog.Add( $"This is a double battle! Checking attack intent evidence in the context of doubles..." );

            var theirAlly = _ai.GetActiveAlly( tic.Threat.Pokemon );
            if( ( theirAlly == null && theirAlly.Pokemon == null ) || ( theirAlly != null && theirAlly.Pokemon == null ) )
            {
                _tirLog.Add( $"They don't have an ally on the field, skipping!" );
                _tirLog.Add( $"" );
                return evidence;
            }

            _tirLog.Add( $"Their ({tic.Threat.Name}) ally is: {theirAlly.Name} ({theirAlly.RoleProfile.PrimaryRole})" );
            _tirLog.Add( $"" );

            //--Our Ally Information
            var ourAlly = _ai.GetActiveAlly( _ai.CurrentUnitAdapter.Pokemon );
            bool weHaveAlly = ourAlly != null && ourAlly.Pokemon != null;
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

            bool theirAllyHas_HelpingHand   = _ai.UnitSim.CheckHasMove( theirAlly, "Helping Hand" );
            bool theirAllyHas_Coaching      = _ai.UnitSim.CheckHasMove( theirAlly, "Coaching" );
            bool theirAllyHas_Tailwind      = theirAllyTraits.Contains( RoleTrait.TailwindSetter );
            bool theirAllyHas_TrickRoom     = theirAllyTraits.Contains( RoleTrait.TrickRoomSetter );
            bool theirAllyHas_SpeedControl  = theirAllyTraits.Contains( RoleTrait.SpeedControl );
            bool theirAllyHas_FakeOut       = _ai.CanUseFakeOut( theirAlly, _ai.CurrentUnitAdapter );
            bool theirAllyHas_FollowMe      = _ai.UnitSim.CheckHasMove( theirAlly, "Follow Me" );
            bool theirAllyHas_RagePowder    = _ai.UnitSim.CheckHasMove( theirAlly, "Rage Powder" );
            bool theirAllyHas_Feint         = _ai.UnitSim.CheckHasMove( theirAlly, "Feint" );
            bool theirAllyHas_WideGuard     = _ai.UnitSim.CheckHasMove( theirAlly, "Wide Guard" );
            bool theirAllyHas_QuickGuard    = _ai.UnitSim.CheckHasMove( theirAlly, "Quick Guard" );

            if( theirAllyHas_HelpingHand )
            {
                evidence += 2;
                _tirLog.Add( $"Their Ally has Helping Hand. Attack Evidence: {evidence}" );
            }

            if( theirAllyHas_Coaching )
            {
                evidence += 1;
                _tirLog.Add( $"Their Ally has Coaching. Attack Evidence: {evidence}" );
            }

            if( theirAllyHas_Tailwind && !theirCourt.ContainsKey( CourtConditionID.Tailwind ) )
            {
                evidence += 1;
                _tirLog.Add( $"Their Ally can set Tailwind. Attack Evidence: {evidence}" );

                if( theirAlly.Speed > attackTOP.Attacker.Speed )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their Ally outspeeds them, meaning their ally can set tailwind before they attack. Attack Evidence: {evidence}" );
                }

                if( weHaveAlly && theirAlly.Speed > ourAlly.Speed )
                {
                    evidence += 1;
                    _tirLog.Add( $"Their Ally outspeeds our ally, meaning their ally can set tailwind before our ally can act, assuming our ally does not use priority. Attack Evidence: {evidence}" );
                }
            }

            if( theirAllyHas_TrickRoom && !_ai.CurrentFieldSnapshot.FieldConditions.ContainsKey( FieldConditionID.TrickRoom ) && theyCanUseFakeOut )
            {
                evidence += 3;
                _tirLog.Add( $"Their Ally can set Trick Room and they can currently use fake out. Attack Evidence: {evidence}" );
            }

            if( theirAllyHas_SpeedControl )
            {
                evidence += 1;
                _tirLog.Add( $"Their Ally has Speed Control. Attack Evidence: {evidence}" );
            }

            if( theirAllyHas_FakeOut )
            {
                evidence += 2;
                _tirLog.Add( $"Their Ally has Fake Out available. Attack Evidence: {evidence}" );
            }

            if( theirAllyHas_FollowMe )
            {
                evidence += 1;
                _tirLog.Add( $"Their Ally has Follow Me. Attack Evidence: {evidence}" );
            }

            if( theirAllyHas_RagePowder )
            {
                evidence += 1;
                _tirLog.Add( $"Their Ally has Rage Powder. Attack Evidence: {evidence}" );
            }

            if( theirAllyHas_Feint )
            {
                evidence += 1;
                _tirLog.Add( $"Their Ally has Feint. Attack Evidence: {evidence}" );
            }

            if( theirAllyHas_WideGuard )
            {
                evidence += 1;
                _tirLog.Add( $"Their Ally has WideGuard. Attack Evidence: {evidence}" );
            }

            if( theirAllyHas_QuickGuard )
            {
                evidence += 1;
                _tirLog.Add( $"Their Ally has QuickGuard. Attack Evidence: {evidence}" );
            }

            if( weCanProtect || ourAllyCanProtect )
            {
                evidence -= 1;
                _tirLog.Add( $"We or our ally have protect on the table. Attack Evidence: {evidence}" );
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

            var switchEE = _ai.Projection.EvaluateExchange( defSwitchTOP.Attacker, defSwitchTOP.Opponent );

            bool switchIsThreatenedByKO = defSwitchTOP.OpponentPTKO >= PotentialToKO.Dangerous;
            bool switchTakesBigDamage = defSwitchTOP.OpponentPTKO >= PotentialToKO.TwoHKO;

            bool currentExchangeLost = attackTOP.OpponentPTKO >= PotentialToKO.Dangerous && attackTOP.AttackerPTKO <= PotentialToKO.TwoHKO;

            int ourRemainingCount = _ai.OurTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList().Count;
            int theirRemainingCount = _ai.TheirTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList().Count;
            int materialDelta = theirRemainingCount - ourRemainingCount;

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

            if( attackTOP.Opponent.CurrentHPR <= 0.25f )
            {
                evidence -= 2;
                _tirLog.Add( $"We have pretty low HP, they might stay in against us. Defensive Switch Evidence: {evidence}" );
            }

            if( currentExchangeLost )
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

            if( theirTP.Type == ThreatType.Constraining && ( theirCurrentRP.Traits.Contains( RoleTrait.ShadowTag ) || attackTOP.Opponent.Bindings.Count > 0 ) )
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
            if( hpAfterHazards <= 0f )
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

            if( _ai.IsDoubleBattle )
            {
                _tirLog.Add( $"" );
                _tirLog.Add( $"This is a double battle! Checking defensive switch intent evidence in the context of doubles..." );

                var theirAlly = _ai.GetActiveAlly( tic.Threat.Pokemon );
                bool theyHaveAlly = true;
                if( ( theirAlly == null && theirAlly.Pokemon == null ) || ( theirAlly != null && theirAlly.Pokemon == null ) )
                {
                    _tirLog.Add( $"They don't have an ally on the field, skipping!" );
                    _tirLog.Add( $"" );
                    theyHaveAlly = false;
                    return evidence;
                }

                _tirLog.Add( $"Their ({tic.Threat.Name}) ally is: {theirAlly.Name} ({theirAlly.RoleProfile.PrimaryRole})" );
                _tirLog.Add( $"" );

                //--Our Ally Information
                var ourAlly = _ai.GetActiveAlly( _ai.CurrentUnitAdapter.Pokemon );
                bool weHaveAlly = ourAlly != null && ourAlly.Pokemon != null;
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
                    _tirLog.Add( $"Their ally can protect themselves while they switch - switch + protect is a common strategy. Defensive Switch Evidence: {evidence}" );
                }

                if( switchChangesWeather )
                {
                    bool theirAllyBenefitsWeather = _ai.UnitSim.Get_WeatherContextScore( theirAlly.Pokemon, candidatesWeather ) > 0;
                    evidence += 1;
                    _tirLog.Add( $"Their Defensive switch candidate changes weather to one their ally benefits from. Defensive Switch Evidence: {evidence}" );
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
            int switchThreatCount = _ai.TheirTeamPieceValues.TryGetValue( offSwitchTOP.Attacker.Pokemon, out var pieceValue ) ? pieceValue.ThreatCount : 0;

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

            if( switchThreatCount >= 3 )
            {
                evidence += 1;
                _tirLog.Add( $"Their offensive switch candidate threatens 3 or units. Offensive Switch Evidence: {evidence}" );
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
            }

            var switchCandidate = offSwitchTOP.Attacker.Pokemon;
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

            if( theirTP.Type == ThreatType.Constraining && ( theirCurrentRP.Traits.Contains( RoleTrait.ShadowTag ) || attackTOP.Opponent.Bindings.Count > 0 ) )
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
            float hpAfterHazards = _ai.Get_HPRatio_AfterEntryHazards( attackTOP.Attacker );
            if( hpAfterHazards <= 0f )
            {
                evidence -= 1;
                _tirLog.Add( $"If they recall their unit, it will faint to hazards when they switch it in later if they don't clear them. Defensive Switch Evidence: {evidence}" );

                if( offSwitchTOP.Opponent.RoleProfile.Traits.Contains( RoleTrait.HazardRemover ) )
                {
                    evidence += 1;
                    _tirLog.Add( $"Our predicted switch candidate has hazard removal. Nulifying previous penalty. Offensive Switch Evidence: {evidence}" );
                }
                else
                {
                    evidence -= 1;
                    _tirLog.Add( $"Our predicted switch candidate doesn't have hazard removal either. Offensive Switch Evidence: {evidence}" );
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

            if( _ai.IsDoubleBattle )
            {
                _tirLog.Add( $"" );
                _tirLog.Add( $"This is a double battle! Checking Offensive Switch intent evidence in the context of doubles..." );

                var theirAlly = _ai.GetActiveAlly( tic.Threat.Pokemon );
                bool theyHaveAlly = true;
                if( ( theirAlly == null && theirAlly.Pokemon == null ) || ( theirAlly != null && theirAlly.Pokemon == null ) )
                {
                    _tirLog.Add( $"They don't have an ally on the field, skipping!" );
                    _tirLog.Add( $"" );
                    theyHaveAlly = false;
                    return evidence;
                }

                _tirLog.Add( $"Their ({tic.Threat.Name}) ally is: {theirAlly.Name} ({theirAlly.RoleProfile.PrimaryRole})" );
                _tirLog.Add( $"" );

                //--Our Ally Information
                var ourAlly = _ai.GetActiveAlly( _ai.CurrentUnitAdapter.Pokemon );
                bool weHaveAlly = ourAlly != null && ourAlly.Pokemon != null;
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
                    evidence += 1;
                    _tirLog.Add( $"Their Offensive Switch candidate changes weather to one their ally benefits from. Offensive Switch Evidence: {evidence}" );
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
            var setupTOP = tic.SetupThreatResult.Top;
            var setupRP = setupTOP.Attacker.RoleProfile;
            var setupEE = _ai.Projection.EvaluateExchange( setupTOP.Attacker, setupTOP.Opponent );

            bool offensiveSetup = setupRP.Traits.Contains( RoleTrait.SpeciallyOffensiveSetup ) || setupRP.Traits.Contains( RoleTrait.PhysicallyOffensiveSetup );
            bool defensiveSetup = setupRP.Traits.Contains( RoleTrait.SpeciallyDefensiveSetup ) || setupRP.Traits.Contains( RoleTrait.PhysicallyDefensiveSetup );
            bool hasSetup = offensiveSetup || defensiveSetup;

            bool freeTurn = setupTOP.OpponentPTKO <= PotentialToKO.Safe;

            int ourRemainingCount = _ai.OurTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList().Count;
            int theirRemainingCount = _ai.TheirTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList().Count;
            int materialDelta = theirRemainingCount - ourRemainingCount;

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

            var statStages = setupTOP.Attacker.StatStages;
            bool setupOffensivelyAlready = false;
            bool setupDefensivelyAlready = false;

            foreach( var sc in statStages )
            {
                if( sc.Value > 0 )
                {
                    if( ( sc.Key == Stat.Attack || sc.Key == Stat.SpAttack ) && offensiveSetup )
                    {
                        setupOffensivelyAlready = true;
                    }

                    if( ( sc.Key == Stat.Defense || sc.Key == Stat.SpDefense ) && defensiveSetup  )
                    {
                        setupDefensivelyAlready = true;
                    }
                }
            }

            if( setupOffensivelyAlready || setupDefensivelyAlready )
            {
                evidence -= 2;
                _tirLog.Add( $"They've already set up, so they may not do so again. Setup Evidence: {evidence}" );
            }

            var ourRemaining = _ai.OurTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList();
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

            //--Current Plan & GPA
            if( theirCP.Type == PlanType.Trade )
            {
                if( materialDelta > 0 )
                {
                    evidence -= 1;
                    _tirLog.Add( $"They hold a material advantage, setting up introduces potentially unnecessary risk. Setup Evidence: {evidence}" );
                }
            }

            if( _ai.IsDoubleBattle )
            {
                _tirLog.Add( $"" );
                _tirLog.Add( $"This is a double battle! Checking Setup intent evidence in the context of doubles..." );

                var theirAlly = _ai.GetActiveAlly( tic.Threat.Pokemon );
                bool theyHaveAlly = true;
                if( ( theirAlly == null && theirAlly.Pokemon == null ) || ( theirAlly != null && theirAlly.Pokemon == null ) )
                {
                    _tirLog.Add( $"They don't have an ally on the field, skipping!" );
                    _tirLog.Add( $"" );
                    theyHaveAlly = false;
                    return evidence;
                }

                _tirLog.Add( $"Their ({tic.Threat.Name}) ally is: {theirAlly.Name} ({theirAlly.RoleProfile.PrimaryRole})" );
                _tirLog.Add( $"" );

                //--Our Ally Information
                var ourAlly = _ai.GetActiveAlly( _ai.CurrentUnitAdapter.Pokemon );
                bool weHaveAlly = ourAlly != null && ourAlly.Pokemon != null;
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

            float ourRemainingCount = _ai.OurTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList().Count;
            float theirRemainingCount = _ai.TheirTeamAdapters.Where( kvp => kvp.Key.CurrentHP > 0 ).ToList().Count;
            float ourRemainingPercent = ourRemainingCount / _ai.OurTeamAdapters.Count;
            float theirRemainingPercent = theirRemainingCount / _ai.TheirTeamAdapters.Count;

            var status = tic.OffensiveStatusThreatResult;

            bool isRound1 = _ai.Round <= 1;
            bool isEarlyGame = bfs.IsEarlyGame;

            bool weGainSpeedControl = !ee.AttackerMovesFirst && statusEE.AttackerMovesFirst;

            var moveEffects = tic.OffensiveStatusThreatResult.Move.MoveSO.MoveEffects;

            bool theyCreatePressureState = false;
            bool theyCreateDecisiveState = false;

            //--Status Effect Evidence. Toxic, Taunt, etc.
            if( status.StatusType == OffensiveStatusType.StatusEffect || status.StatusType == OffensiveStatusType.Disruption )
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

                if( offStatusTOP.Opponent.SevereStatus == SevereConditionID.None )
                {
                    if( theyBurn && weAreBurnWeak )
                    {
                        evidence += 1;
                        _tirLog.Add( $"They have a burn opportunity. Offensive Status Evidence: {evidence}" );
                    }

                    if( theyFrost && weAreFrostWeak )
                    {
                        evidence += 1;
                        _tirLog.Add( $"They have a frostbite opportunity. Offensive Status Evidence: {evidence}" );
                    }

                    if( theyPoison && wePassiveRecover )
                    {
                        evidence += 1;
                        _tirLog.Add( $"They have a poison opportunity. Offensive Status Evidence: {evidence}" );
                    }

                    if( theyToxic && weAreToxicWeak )
                    {
                        evidence += 1;
                        _tirLog.Add( $"They have a toxic opportunity. Offensive Status Evidence: {evidence}" );
                    }

                    if( theyParalyze && weAreParalysisWeak )
                    {
                        evidence += 1;
                        _tirLog.Add( $"They have a paralysis opportunity. Offensive Status Evidence: {evidence}" );
                    }

                    if( theySleep && ( offStatusTOP.AttackerMovedFirst || offStatusTOP.OpponentPTKO <= PotentialToKO.TwoHKO ) )
                    {
                        evidence += 1;
                        _tirLog.Add( $"They have a sleep opportunity. Offensive Status Evidence: {evidence}" );
                    }
                }

                if( theyTaunt && weAreTauntWeak && !offStatusTOP.Opponent.VolatileStatuses.Contains( VolatileConditionID.Taunt ) )
                {
                    evidence += 1;
                    _tirLog.Add( $"They have a taunt opportunity. Offensive Status Evidence: {evidence}" );
                }

                if( theyEncore && weAreEncoreWeak && !offStatusTOP.Opponent.VolatileStatuses.Contains( VolatileConditionID.Encore ) )
                {
                    evidence += 1;
                    _tirLog.Add( $"They have an encore opportunity. Offensive Status Evidence: {evidence}" );
                }

                if( theyHealBlock && weAreHealBlockWeak && !offStatusTOP.Opponent.VolatileStatuses.Contains( VolatileConditionID.HealBlocked ) )
                {
                    evidence += 1;
                    _tirLog.Add( $"They have a heal block opportunity. Offensive Status Evidence: {evidence}" );
                }

                if( theyDisable && weAreDisableWeak && !offStatusTOP.Opponent.VolatileStatuses.Contains( VolatileConditionID.Disabled ) )
                {
                    evidence += 1;
                    _tirLog.Add( $"They have a disable opportunity. Offensive Status Evidence: {evidence}" );
                }
            }

            //--Entry Hazard Evidence
            if( status.StatusType == OffensiveStatusType.EntryHazard )
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
            if( status.StatusType == OffensiveStatusType.StatDebuff )
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
            if( status.StatusType == OffensiveStatusType.Phaze )
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

                    if( theirRP.Traits.Contains( RoleTrait.Phazes ) )
                    {
                        evidence += 1;
                        _tirLog.Add( $"Who is also a phazer. Offensive Status Evidence: {evidence}" );

                        if( ( theirRP.PrimaryRole == RoleClass.Wall || theirRP.PrimaryRole == RoleClass.BulkyAttacker ) && !ee.OpponentThreatensKO )
                        {
                            evidence += 1;
                            _tirLog.Add( $"And we're a wall/bulky aligned pokemon who isn't immediately threatened. Offensive Status Evidence: {evidence}" );
                        }
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

            if( ( theirTP.Type == ThreatType.Disruptive || theirTP.DisruptivePressure >= 5f ) && status.StatusType != OffensiveStatusType.EntryHazard )
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

            //--PBS
            if( theyCreatePressureState )
            {
                evidence += 1;
                _tirLog.Add( $"Their offensive status creates a pressure state. Offensive Status Evidence: {evidence}" );
            }

            if( theyCreateDecisiveState )
            {
                evidence += 2;
                _tirLog.Add( $"Their offensive status creates a decisive state. Offensive Status Evidence: {evidence}" );
            }

            if( statusEE.OpponentPTKOR.PTKO < offStatusTOP.OpponentPTKO )
            {
                evidence += 1;
                _tirLog.Add( $"Their status stabilizes the board for them. Offensive Status Evidence: {evidence}" );
            }

            //--Current Plan & GPA
            if( status.StatusType != OffensiveStatusType.EntryHazard && _ai.GamePlan.OurBlockers.Contains( offStatusTOP.Opponent.Pokemon ) )
            {
                evidence += 1;
                _tirLog.Add( $"We are a game plan blocker and they are looking to cripple us with status. Offensive Status Evidence: {evidence}" );

                if( status.StatusType == OffensiveStatusType.StatusEffect || status.StatusType == OffensiveStatusType.Disruption )
                {
                    evidence += 1;
                    _tirLog.Add( $"That status is also a status effect such as toxic or taunt. Offensive Status Evidence: {evidence}" );
                }
            }

            if( theirCP.FocusMon == offStatusTOP.Opponent.Pokemon && theirCP.Type == PlanType.Aggress && status.StatusType != OffensiveStatusType.EntryHazard )
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

                var theirAlly = _ai.GetActiveAlly( tic.Threat.Pokemon );
                bool theyHaveAlly = true;
                if( ( theirAlly == null && theirAlly.Pokemon == null ) || ( theirAlly != null && theirAlly.Pokemon == null ) )
                {
                    _tirLog.Add( $"They don't have an ally on the field, skipping!" );
                    _tirLog.Add( $"" );
                    theyHaveAlly = false;
                    return evidence;
                }

                _tirLog.Add( $"Their ({tic.Threat.Name}) ally is: {theirAlly.Name} ({theirAlly.RoleProfile.PrimaryRole})" );
                _tirLog.Add( $"" );

                //--Our Ally Information
                var ourAlly = _ai.GetActiveAlly( _ai.CurrentUnitAdapter.Pokemon );
                bool weHaveAlly = ourAlly != null && ourAlly.Pokemon != null;
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
        }
        else
        {
            evidence = -99;
            _tirLog.Add( $"Offensive Status candidate not found! Nullifying evidence. Offensive Status Evidence: {evidence}" );
        }

        _tirLog.Add( $"" );

        return evidence;
    }

    private int SupportiveStatusEvidence()
    {
        int evidence = 0;

        // Trick Room
        // Increase if ally
        // Fake Out
        // Follow Me
        // Rage Powder
        // Wide Guard
        // Decrease if ally is already extremely fast and benefits from normal Speed order.

        // Tailwind
        // Increase if ally
        // Fast offensive role
        // Choice item
        // Glass cannon

        return evidence;
    }

}

public enum IntentType{ None, Attack, DefensiveSwitch, OffensiveSwitch, Setup, OffensiveStatus, SupportiveStatus, Protect }
public struct ThreatIntentResult
{
    public IBattleAIUnit Threat;
    public Intent PrimaryIntent;
    public Intent SecondaryIntent;
    public int TotalEvidence;
    public float Confidence;

    public bool CheckSecondaryIntent;

    public Dictionary<IntentType, int> IntentEvidence;
}

public struct Intent
{
    public IntentType IntentType;
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
