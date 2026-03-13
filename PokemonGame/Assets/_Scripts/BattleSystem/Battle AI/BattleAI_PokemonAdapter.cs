using System.Collections;
using System.Collections.Generic;
using Eflatun.SceneReference;
using UnityEngine;

public class BattleAI_PokemonAdapter : IBattleAIUnit
{
    private BattleAI _ai;
    public Pokemon Pokemon { get; private set; }
    public string Name { get; set; }
    public string PID { get; set; }
    public int MaxHP { get; set; }
    public float CurrentHPR { get; set; }
    public ( PokemonType One, PokemonType Two ) Type { get; set; }
    public int Level { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int SpAttack { get; set; }
    public int SpDefense { get; set; }
    public int Speed { get; set; }
    public MoveThreatResult MTR { get; set; }
    public List<Move> ActiveMoves { get; set; }
    public bool HasPriority { get; set; }
    public bool IsUngrounded { get; set; }

    public AbilityID Ability { get; set; }
    public BattleItemEffectID Item { get; set; }

    public SevereConditionID SevereStatus { get; set; }
    public int SevereStatusTime { get; set; } //--For toxic, this increments.
    public List<VolatileConditionID> VolatileStatuses { get; set; }
    public List<BindingConditionID> Bindings { get; set; }

    public CourtLocation CourtLocation { get; set; }
    public Court Court { get; set; }
    public bool CourtSeeded { get; set; }

    public Dictionary<Stat, int> StatStages { get; set; }
    public Dictionary<Stat, Dictionary<DirectModifierCause, float>> DirectStatModifiers{ get; set; }

    private CustomLogSession _buildLog;

    public BattleAI_PokemonAdapter( Pokemon mon, BattleAI ai )
    {
        Pokemon = mon;
        _ai = ai;
        Build( mon );
    }

    private void Build( Pokemon pokemon )
    {
        Name = pokemon.NickName;
        PID = pokemon.PID;

        CurrentHPR = _ai.Get_HPRatio( pokemon );

        Type = ( pokemon.PokeSO.Type1, pokemon.PokeSO.Type2 );

        Level = pokemon.Level;
        MaxHP = _ai.GetBaseStat( pokemon, Stat.HP );
        Attack = _ai.GetBaseStat( pokemon, Stat.Attack );
        Defense = _ai.GetBaseStat( pokemon, Stat.Defense );
        SpAttack = _ai.GetBaseStat( pokemon, Stat.SpAttack );
        SpDefense = _ai.GetBaseStat( pokemon, Stat.SpDefense );
        Speed = _ai.GetBaseStat( pokemon, Stat.Speed );

        ActiveMoves = new( pokemon.ActiveMoves );

        Ability = pokemon.AbilityID;
        Item = pokemon.BattleItemEffect != null ? pokemon.BattleItemEffect.ID : BattleItemEffectID.None;

        SevereStatus = pokemon.SevereStatus != null ? pokemon.SevereStatus.ID : SevereConditionID.None;
        SevereStatusTime = pokemon.SevereStatusTime;
        VolatileStatuses = new( pokemon.VolatileStatuses.Keys );
        Bindings = new( pokemon.BindingStatuses.Keys );

        CourtLocation = _ai.BattleSystem.Field.GetPokemonCourtLocationFromTrainer( pokemon );
        Court = _ai.BattleSystem.Field.ActiveCourts[CourtLocation];
        CourtSeeded = Court.Conditions.ContainsKey( CourtConditionID.LeechSeed );

        StatStages = pokemon.CloneStatStages();
        DirectStatModifiers = pokemon.CloneDirectModifiers();

        _buildLog = new();
        _buildLog.Add( $"===[Built Adapter for (Lv. {Level}) {Name}]===" );
        _buildLog.Add( $"PID: {PID}" );
        _buildLog.Add( $"Current HPR: {CurrentHPR}" );
        _buildLog.Add( $"Type One: {Type.One}, Type Two: {Type.Two}" );
        _buildLog.Add( $"" );
        _buildLog.Add( $"HP: {MaxHP}" );
        _buildLog.Add( $"Attack: {Attack}" );
        _buildLog.Add( $"Defense: {Defense}" );
        _buildLog.Add( $"SpAttack: {SpAttack}" );
        _buildLog.Add( $"SpDefense: {SpDefense}" );
        _buildLog.Add( $"Speed: {Speed}" );

        _buildLog.Add( $"" );
        _buildLog.Add( $"=[Move List]=" );
        for( int i = 0; i < ActiveMoves.Count; i++ )
            _buildLog.Add( $"Move {i+1}: {ActiveMoves[i].MoveSO.Name}" );

        _buildLog.Add( $"" );
        _buildLog.Add( $"Ability: {Ability}" );
        _buildLog.Add( $"Item: {Item}" );
        _buildLog.Add( $"" );
        _buildLog.Add( $"Severe Status: {SevereStatus}" );
        _buildLog.Add( $"Volatile Statuses: {VolatileStatuses.Count}" );
        _buildLog.Add( $"Binding Statuses: {Bindings.Count}" );
        _buildLog.Add( $"" );
        _buildLog.Add( $"Court Location: {CourtLocation}" );
        _buildLog.Add( $"" );

        _buildLog.Add( $"=[Stat Stages]=" );
        foreach( var kvp in StatStages )
            _buildLog.Add( $"Stat: {kvp.Key}, Stage: {kvp.Value}" );

        _buildLog.Add( $"=[Direct Modifiers]=" );
        foreach( var stat in DirectStatModifiers )
        {
            foreach( var cause in stat.Value )
            {
                _buildLog.Add( $"Stat: {stat.Key}, Cause: {cause.Key}, Modifier: {cause.Value}" );
            }
        }

        Debug.Log( _buildLog.ToString() );
        _buildLog.Clear();
    }
}
