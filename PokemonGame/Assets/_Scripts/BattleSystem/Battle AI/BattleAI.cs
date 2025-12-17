using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public enum AIDecisionType { Attack, RandomMove, StrongestMove, OffensiveSwitch, DefensiveSwitch, SpeedControl, Weather, FakeOut, Protect, }
public enum KO_Class { HardWall, Sturdy, Safe, Neutral2HK0, Risky, Dangerous, LikelyOHKO }

public class BattleAI : MonoBehaviour
{
    private BattleAI_MoveCommand _moveCommand;
    private BattleAI_SwitchCommand _switchCommand;
    public BattleSystem BattleSystem { get; private set; }
    public BattleUnit Unit { get; private set; }
    public Pokemon Pokemon { get; private set; }
    public float TrainerSkillModifier { get; private set; }
    public const int HARD_SWITCH_THRESHOLD = 50;

    public void SetupAI( BattleSystem battleSystem, BattleUnit battleUnit ){
        BattleSystem = battleSystem;
        Unit = battleUnit;
        Pokemon = Unit.Pokemon;
        TrainerSkillModifier = Mathf.Clamp01( battleSystem.EnemyTrainerParty.GetComponent<Trainer>().TrainerSkillLevel / 100f );
        _moveCommand = new( this );
        _switchCommand = new( this );
    }

    public void CleanupAI()
    {
        _moveCommand = null;
        _switchCommand = null;
    }

    public void ChooseCommand(){
        Debug.Log( $"AI {Unit.Pokemon.NickName} ChooseCommand()" );
        if( Unit.Pokemon.SevereStatus?.ID == StatusConditionID.FNT || Unit.Pokemon.CurrentHP == 0 )
            return;

        var damageThreat = GetThreat_ImmediateDamage( BattleSystem.PlayerUnits, Unit.Pokemon );

        if( _switchCommand.ShouldSwitch( damageThreat ) )
        {
            Debug.Log( $"AI {Unit.Pokemon.NickName} is thinking about switching out!" );
            var incomingPokemon = _switchCommand.GetSwitch_Defensive( BattleSystem.PlayerUnits ).Pokemon;
            if( incomingPokemon != null )
            {
                Debug.Log( $"AI {Unit.Pokemon.NickName} is switching with {incomingPokemon.NickName}!" );
                _switchCommand.SubmitSwitchCommand( incomingPokemon );
            }
            else
                _moveCommand.SubmitMoveCommand( damageThreat.Unit );
        }
        else
        {
            Debug.Log( $"AI {Unit.Pokemon.NickName} chose to attack target: {damageThreat.Unit.Pokemon.NickName}" );
            _moveCommand.SubmitMoveCommand( damageThreat.Unit );
        }
    }

    public Pokemon RequestFaintedSwitch()
    {
        return _switchCommand.GetSwitch_Defensive( BattleSystem.PlayerUnits ).Pokemon;
    }

    public float GetOutgoingPressure( Pokemon me, BattleUnit target )
    {
        int score = 50;
        float bestMoveThreat = float.MinValue;
        float hpRatio = me.CurrentHP / me.MaxHP;
        float defense;

        if( target.Pokemon.PokeSO.Attack > target.Pokemon.PokeSO.SpAttack )
            defense = me.PokeSO.Defense;
        else
            defense = me.PokeSO.SpDefense;

        Debug.Log( $"[Outgoing Pressure Check] Starting Outgoing Pressure check on {me.NickName}. Starting Score: {score}" );

        //--Move Threat
        foreach( var move in me.ActiveMoves )
        {
            if( move.MoveSO.MoveCategory == MoveCategory.Status )
                continue;

            //--Assign a bonus for effectiveness
            float effectiveness = TypeChart.GetEffectiveness( move.MoveType, target.Pokemon.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, target.Pokemon.PokeSO.Type2 );

            //--Assign a bonus for stab
            float stab = move.MoveSO.Type == Pokemon.PokeSO.Type1 || move.MoveSO.Type == Pokemon.PokeSO.Type2 ? 1.5f : 1f;

            //--Assign a bonus for a weather damage boost
            float weather = BattleSystem.Field.Weather?.OnDamageModify?.Invoke( Unit.Pokemon, target.Pokemon, move ) ?? 1f;

            Debug.Log( $"[Outgoing Pressure Check] Score-ing {me.NickName}'s move {move.MoveSO.Name}. Effectiveness Modifier: {effectiveness}, STAB Modifier: {stab}, Weather Modifier: {weather}" );

            float currentMoveThreat = effectiveness * stab * weather;
            bestMoveThreat = Mathf.Max( bestMoveThreat, currentMoveThreat );

            Debug.Log( $"[Outgoing Pressure Check] {me.NickName}'s move {move.MoveSO.Name} checked. Move's Score: {bestMoveThreat}" );
        }

             if( bestMoveThreat >= 9f )             score += 90; //--Upper bounds, this move is 4x effective, has STAB, and benefits from weather.
        else if( bestMoveThreat >= 6f )             score += 60; //--This move is 4x effective, and either has STAB OR benefits from weather.
        else if( bestMoveThreat >= 4f )             score += 40; //--This move is 4x effective, or has some combination of 2x effective, stab, and weather.
        else if( bestMoveThreat >= 2f )             score += 20;
        else if( bestMoveThreat >= 1.5f )           score += 15;
        else if( bestMoveThreat >= 1f )             score += 0;
        else if( bestMoveThreat >= 0.5f )           score -= 15;
        else if( bestMoveThreat >= 0.25f )          score -= 25;
        else if( bestMoveThreat == 0f )             score = 0;

        Debug.Log( $"[Outgoing Pressure Check] {me.NickName}'s Moves have all been checked. Score: {score}" );

        //--Speed comparison
        if( me.Speed > target.Pokemon.Speed )
            score += 20;
        else if( me.Speed < target.Pokemon.Speed )
            score -= 20;

        Debug.Log( $"[Outgoing Pressure Check] {me.NickName}'s Speed comparison checked. Score: {score}" );

        Debug.Log( $"[Outgoing Pressure Check] {me.NickName}'s Defense is: {defense}" );

        if ( defense >= 150f )              score += 40;
        else if( defense >= 125f )          score += 25;
        else if( defense >= 100f )          score += 10;
        else if( defense >= 80f )           score += 0;
        else if( defense >= 65f )           score -= 10;
        else if( defense >= 50f )           score -= 25;
        else if( defense < 50f )            score -= 40;

        Debug.Log( $"[Outgoing Pressure Check] {me.NickName}'s Defense checked. Score: {score}" );

        int bulk = me.PokeSO.MaxHP + me.PokeSO.Defense + me.PokeSO.SpDefense;

        if( bulk >= 400 )           score += 25;
        else if( bulk >= 300 )      score += 10;
        else if( bulk >= 200 )      score += 0;
        else if( bulk >= 150 )      score -= 10;
        else if( bulk <= 100 )      score -= 20;


        Debug.Log( $"[Outgoing Pressure Check] {me.NickName}'s Overall Bulk checked. Score: {score}" );

        Debug.Log( $"[Outgoing Pressure Check] {me.NickName}'s HP Ratio is: {hpRatio}" );
        if( hpRatio < 0.25f )           score -= 30;
        else if( hpRatio < 0.5f )       score -= 15;

        Debug.Log( $"[Outgoing Pressure Check] {me.NickName}'s HP checked for low percentage. Score: {score}" );

        Debug.Log( $"[Outgoing Pressure Check] {me.NickName}'s Final Score: {score}" );
        
        return Mathf.Clamp( score, 0, 250 );
    }

    public ThreatResult GetThreat_ImmediateDamage( List<BattleUnit> opponents, Pokemon ourUnit )
    {
        int highestThreat = int.MinValue;
        BattleUnit highestUnit = null;

        foreach( var threat in opponents )
        {
            int threatScore = 100;
            float moveThreat = float.MinValue;

            Debug.Log( $"[Incoming Immediate Damage Check] Starting threat check on {threat.Pokemon.NickName}. Starting Score: {threatScore}" );

            //--Offensive Pressure
            float offensivePressure;
            if( threat.Pokemon.Attack > threat.Pokemon.SpAttack )
                offensivePressure = threat.Pokemon.Attack;
            else
                offensivePressure = threat.Pokemon.SpAttack;

            Debug.Log( $"[Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Offensive Pressure is: {offensivePressure}" );
            
            if( offensivePressure >= 150f )             threatScore += 40;
            else if( offensivePressure >= 125f )        threatScore += 25;
            else if( offensivePressure >= 100f )        threatScore += 10;
            else if( offensivePressure >= 80f )         threatScore += 0;
            else if( offensivePressure >= 65f )         threatScore -= 10;
            else if( offensivePressure >= 50f )         threatScore -= 25;
            else if( offensivePressure < 50f )          threatScore -= 40;

            Debug.Log( $"[Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Offensive Pressure checked. Score: {threatScore}" );

            //--Move Threat
            foreach( var move in threat.Pokemon.ActiveMoves )
            {
                if( move.MoveSO.Power <= 0 || move.MoveSO.MoveCategory == MoveCategory.Status )
                    continue;

                float effectiveness     = TypeChart.GetEffectiveness( move.MoveType, ourUnit.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, ourUnit.PokeSO.Type2 );
                float stab              = threat.Pokemon.CheckTypes( move.MoveType ) ? 1.5f : 1f;
                float weather           = BattleSystem.Field.Weather?.OnDamageModify?.Invoke( ourUnit, threat.Pokemon, move ) ?? 1f;

                Debug.Log( $"[Incoming Immediate Damage Check] Score-ing {threat.Pokemon.NickName}'s move {move.MoveSO.Name}. Effectiveness Modifier: {effectiveness}, STAB Modifier: {stab}, Weather Modifier: {weather}" );

                float currentMoveThreat = effectiveness * stab * weather;
                moveThreat = Mathf.Max( moveThreat, currentMoveThreat );

                Debug.Log( $"[Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s move {move.MoveSO.Name} checked. Move's Score: {moveThreat}" );
            }

                 if( moveThreat >= 9f )             threatScore += 90; //--Upper bounds, this move is 4x effective, has STAB, and benefits from weather.
            else if( moveThreat >= 6f )             threatScore += 60; //--This move is 4x effective, and either has STAB OR benefits from weather.
            else if( moveThreat >= 4f )             threatScore += 40; //--This move is 4x effective, or has some combination of 2x effective, stab, and weather.
            else if( moveThreat >= 2f )             threatScore += 20;
            else if( moveThreat >= 1.5f )           threatScore += 15;
            else if( moveThreat >= 1f )             threatScore += 0;
            else if( moveThreat >= 0.5f )           threatScore -= 15;
            else if( moveThreat >= 0.25f )          threatScore -= 25;
            else if( moveThreat == 0f )             threatScore = 0;

            Debug.Log( $"[Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Moves have all been checked. Score: {threatScore}" );

            //--Higher speed means the target is more threatening
            if( threat.Pokemon.Speed > ourUnit.Speed )
                threatScore += 20;
            else if( threat.Pokemon.Speed < ourUnit.Speed )
                threatScore -= 20;

            Debug.Log( $"[Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Speed comparison checked. Score: {threatScore}" );

            //--Current HP Ratio. Lower HP means we're more threatened
            float hpRatio = ourUnit.CurrentHP / ourUnit.MaxHP;

            Debug.Log( $"[Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Current HP Ratio is: {hpRatio}" );

            if( hpRatio < 0.25f )           threatScore += 30;
            else if( hpRatio < 0.5f )       threatScore += 15;
            else if( hpRatio < 0.75f )      threatScore += 5;

            Debug.Log( $"[Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Current HP Ratio checked. Score: {threatScore}" );

            threatScore = Mathf.Clamp( threatScore, 0, 300 );

            if( threatScore > highestThreat )
            {
                highestThreat = threatScore;
                highestUnit = threat;
            }

            Debug.Log( $"[Incoming Immediate Damage Check] The current most threatening Pokemon is: {highestUnit.Pokemon.NickName}, with a Score of: {highestThreat}" );

        }

        Debug.Log( $"[Incoming Immediate Damage Check] The most threatening Pokemon is: {highestUnit.Pokemon.NickName}, with a Score of: {highestThreat}" );

        return new(){ Score = highestThreat, Unit = highestUnit };
    }

    public int Get_KOClassScore( KO_Class ko )
    {
        //--This is a pretty ass switch, sheesh
        return ko switch
        {
            KO_Class.HardWall       => +60,
            KO_Class.Sturdy         => +35,
            KO_Class.Safe           => +15,
            KO_Class.Neutral2HK0    => 0,
            KO_Class.Risky          => -15,
            KO_Class.Dangerous      => -35,
            KO_Class.LikelyOHKO     => -60,
            _ => 0
        };
    }

    public int Check_KOvsHP( KO_Class ko, float hpRatio, int baseScore )
    {
        if( hpRatio < 0.25f )
        {
            if( ko >= KO_Class.Risky )
                return baseScore - 30;
        }
        else if( hpRatio < 0.4f )
        {
            if( ko >= KO_Class.Dangerous )
                return baseScore - 20;
        }

        return baseScore;
    }

}

public class ThreatResult
{
    public int Score { get; set; }
    public BattleUnit Unit { get; set; }
}

public class MoveThreatResult
{
    public float Score { get; set; }
    public Move Move { get; set; }
}

public class SwitchCandidateResult
{
    public int Score { get; set; }
    public Pokemon Pokemon { get; set; }
    public KO_Class KOClass { get; set; }
}
