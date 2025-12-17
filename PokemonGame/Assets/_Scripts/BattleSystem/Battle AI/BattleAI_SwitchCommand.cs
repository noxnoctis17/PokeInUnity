using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum SwitchType { Offensive, Defensive, Pivot, }

public class BattleAI_SwitchCommand
{
    private readonly BattleAI _ai;
    public SwitchType LastSwitchType { get; private set; }

    public BattleAI_SwitchCommand( BattleAI ai )
    {
        _ai = ai;
    }

    public void SubmitSwitchCommand( Pokemon incomingPokemon )
    {
        _ai.BattleSystem.SetSwitchPokemonCommand( incomingPokemon, _ai.Unit, true );
    }

    public bool ShouldSwitch( ThreatResult damageThreat )
    {
        var outgoingPressure_Vs_damageThreat = _ai.GetOutgoingPressure( _ai.Unit.Pokemon, damageThreat.Unit );
        int dangerGapScore = DangerGapScore( damageThreat, outgoingPressure_Vs_damageThreat );

        Debug.Log( $"[Should Switch] Threat and Switch scores for AI {_ai.Unit.Pokemon.NickName}! Incoming Damage: {damageThreat.Score} from {damageThreat.Unit.Pokemon.NickName}, Outgoing Pressure: {outgoingPressure_Vs_damageThreat}, Danger Gap Score: {dangerGapScore}" );

        if( dangerGapScore >= BattleAI.HARD_SWITCH_THRESHOLD )
            return true;
        else
            return false;
    }

    public int DangerGapScore( ThreatResult incomingThreat, float outgoingPressure )
    {
        int score = Mathf.FloorToInt( incomingThreat.Score - ( outgoingPressure * 1f ) );

        return score;
    }

    public SwitchCandidateResult GetSwitch_Defensive( List<BattleUnit> opponents )
    {
        int bestScore = int.MinValue;
        Pokemon bestSwitch = null;
        KO_Class bestKOClass = KO_Class.Neutral2HK0;

        var ourParty = BattleSystem.Instance.EnemyTrainerParty.PartyPokemon;
        var bench = ourParty.Where( p => !BattleSystem.Instance.EnemyUnits.Any( u => u.Pokemon == p ) ).ToList();

        foreach( var pokemon in bench )
        {
            if( pokemon.IsFainted() )
                continue;

            if( _ai.BattleSystem.IsPokemonSelectedToShift( pokemon ) )
                continue;

            var threat = _ai.GetThreat_ImmediateDamage( opponents, pokemon );

            int score = 100;

            float hpRatio = pokemon.CurrentHP / pokemon.MaxHP;
            int threatsOffense;
            int ourDefense;
            int bulk = pokemon.PokeSO.MaxHP + pokemon.PokeSO.Defense + pokemon.PokeSO.SpDefense;

            if( threat.Unit.Pokemon.PokeSO.Attack > threat.Unit.Pokemon.PokeSO.SpAttack )
            {
                ourDefense = pokemon.PokeSO.Defense;
                threatsOffense = threat.Unit.Pokemon.PokeSO.Attack;
            }
            else
            {
                ourDefense = pokemon.PokeSO.SpDefense;
                threatsOffense = threat.Unit.Pokemon.PokeSO.SpAttack;
            }

            bool isInDanger = hpRatio < 0.4f && ourDefense < 80f || hpRatio < 0.25f;

            //--Speed check. Not as important in a defensive switch-in.
            if( threat.Unit != null && pokemon.PokeSO.Speed < threat.Unit.Pokemon.PokeSO.Speed )
                score -= 5;
            Debug.Log( $"[Defensive Switch Candidate] {pokemon.NickName}'s Speed Comparison checked. Score: {score}" );

            //--Tier of the targeted defensive stat's Base Stat
            if ( ourDefense >= 150f )              score += 25;
            else if( ourDefense >= 125f )          score += 15;
            else if( ourDefense >= 100f )          score += 10;
            else if( ourDefense >= 80f )           score += 0;
            else if( ourDefense >= 65f )           score -= 10;
            else if( ourDefense >= 50f )           score -= 15;
            else if( ourDefense < 50f )            score -= 25;
            Debug.Log( $"[Defensive Switch Candidate] {pokemon.NickName}'s Defense checked. Score: {score}" );

            //--Aggregate bulk based on hp, def, and spdef base stats
            if( bulk >= 400 )           score += 15;
            else if( bulk >= 300 )      score += 10;
            else if( bulk >= 200 )      score += 0;
            else if( bulk >= 150 )      score -= 10;
            else if( bulk <= 100 )      score -= 15;
            Debug.Log( $"[Defensive Switch Candidate] {pokemon.NickName}'s Overall Bulk checked. Score: {score}" );

            //--Walling capability vs the threat's highest attacking stat
            int wallingScore = ourDefense - threatsOffense;
            wallingScore += 30; //--"Normalizes the values, since -30 is the neutral starting point, which lets us start at 0 and go up or down from there.
            KO_Class koClass;

            if( wallingScore >= 45 )                koClass = KO_Class.HardWall;        //--Hard Wall, Shuts down pressure
            else if( wallingScore >= 25 )           koClass = KO_Class.Sturdy;          //--Sturdy, can take a couple hits
            else if( wallingScore >= 10 )           koClass = KO_Class.Safe;            //--Safe, can take an extra hit
            else if( wallingScore >= -9 )           koClass = KO_Class.Neutral2HK0;     //--Neutral, possible 2HKO
            else if( wallingScore >= -29 )          koClass = KO_Class.Risky;           //--Getting Risky, almost guaranteed 2HK0
            else if( wallingScore >= -59 )          koClass = KO_Class.Dangerous;       //--Danger, high damage expected, crit or unexpected damage might OHKO
            else                                    koClass = KO_Class.LikelyOHKO;      //--Fatal, Likely OHKO

            int koscore = _ai.Get_KOClassScore( koClass );
            score += _ai.Check_KOvsHP( koClass, hpRatio, koscore );
            Debug.Log( $"[Defensive Switch Candidate] {pokemon.NickName}'s Walling Score vs HP Ratio checked. Score: {score}" );

            //--Immediate danger override
            if( isInDanger )
            {
                Debug.Log( $"[Defensive Switch Candidate] {pokemon.NickName}'s In Danger!" );
                score -= 50;
            }

            if( score > bestScore )
            {
                bestScore = score;
                bestSwitch = pokemon;
                bestKOClass = koClass;
            }

            Debug.Log( $"[Defensive Switch Candidate] Current Switch Candidate: {pokemon.NickName}, Score: {score}" );
        }

        Debug.Log( $"[Defensive Switch Candidate] Chosen Candidate: {bestSwitch.NickName}, Score: {bestScore}, KO_Class: {bestKOClass}" );
        return new(){ Score = bestScore, Pokemon = bestSwitch, KOClass = bestKOClass };
    }

    private SwitchCandidateResult GetSwitch_Offensive( List<BattleUnit> opponents )
    {
        return new();
    }

}
