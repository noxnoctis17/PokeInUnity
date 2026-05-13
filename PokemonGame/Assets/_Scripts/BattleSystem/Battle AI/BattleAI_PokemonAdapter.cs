using System.Collections;
using System.Collections.Generic;
using Eflatun.SceneReference;
using UnityEngine;

public class BattleAI_PokemonAdapter : IBattleAIUnit
{
    private BattleAI _ai;
    public Pokemon Pokemon { get; set; }
    public string Name { get; set; }
    public string PID { get; set; }
    public float BeginningHPR { get; set; }
    public float CurrentHPR { get; set; }
    public ( PokemonType One, PokemonType Two ) Type { get; set; }
    public int Level { get; set; }
    public int HP { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int SpAttack { get; set; }
    public int SpDefense { get; set; }
    public int Speed { get; set; }
    public RoleProfile RoleProfile { get; set; }
    public StatSpread StatSpread { get; set; }
    public MoveThreatResult MTR { get; set; }
    public List<Move> ActiveMoves { get; set; }
    public bool HasPriority { get; set; }
    public bool IsUngrounded { get; set; }

    public float Expendability { get; set; }

    public AbilityID Ability { get; set; }
    public BattleItemEffectID Item { get; set; }

    public SevereConditionID SevereStatus { get; set; }
    public int SevereStatusTime { get; set; } //--For toxic, this increments.
    public List<VolatileConditionID> VolatileStatuses { get; set; }
    public List<BindingConditionID> Bindings { get; set; }

    public CourtLocation CourtLocation { get; set; }

    public Dictionary<Stat, int> StatStages { get; set; }
    public Dictionary<Stat, Dictionary<DirectModifierCause, float>> DirectStatModifiers{ get; set; }

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

        BeginningHPR = _ai.Get_HPRatio( pokemon );
        CurrentHPR = BeginningHPR;

        Type = ( pokemon.PokeSO.Type1, pokemon.PokeSO.Type2 );

        Level = pokemon.Level;

        ActiveMoves = new( pokemon.ActiveMoves );

        Ability = pokemon.AbilityID;
        Item = pokemon.BattleItemEffect != null ? pokemon.BattleItemEffect.ID : BattleItemEffectID.None;

        SevereStatus = pokemon.SevereStatus != null ? pokemon.SevereStatus.ID : SevereConditionID.None;
        SevereStatusTime = pokemon.SevereStatusTime;
        VolatileStatuses = new( pokemon.VolatileStatuses.Keys );
        Bindings = new( pokemon.BindingStatuses.Keys );

        CourtLocation = _ai.BattleSystem.Field.GetPokemonCourtLocationFromTrainer( pokemon );

        StatStages = pokemon.CloneStatStages();
        DirectStatModifiers = pokemon.CloneDirectModifiers();

        RoleProfile = _ai.RoleDetection.GetPokemonRole( this );
        StatSpread = _ai.StatSpreads.AssignStatSpread( this );

        CalculateStats();
    }

    public void SetExpendability()
    {
        Expendability = _ai.Projection.GetExpendability( this, BeginningHPR );
    }

    public void CalculateStats()
    {
        HP = _ai.GetCalculatedStat( this, Stat.HP );
        Attack = _ai.GetCalculatedStat( this, Stat.Attack );
        Defense = _ai.GetCalculatedStat( this, Stat.Defense );
        SpAttack = _ai.GetCalculatedStat( this, Stat.SpAttack );
        SpDefense = _ai.GetCalculatedStat( this, Stat.SpDefense );
        Speed = _ai.GetCalculatedStat( this, Stat.Speed );

        CustomLogSession log = new();
        log.Add( $"==============================================" );
        log.Add( $"=====[Calculating Adapter Stats ({Name})]=====" );
        log.Add( $"==============================================" );
        log.Add( $"" );
        log.Add( $"[HP]         Assumed: {HP}, Real: {Pokemon.Stats[Stat.HP]}" );
        log.Add( $"[Attack]     Assumed: {Attack}, Real: {Pokemon.Stats[Stat.Attack]}" );
        log.Add( $"[Defense]    Assumed: {Defense}, Real: {Pokemon.Stats[Stat.Defense]}" );
        log.Add( $"[SpAttack]   Assumed: {SpAttack}, Real: {Pokemon.Stats[Stat.SpAttack]}" );
        log.Add( $"[SpDefense]  Assumed: {SpDefense}, Real: {Pokemon.Stats[Stat.SpDefense]}" );
        log.Add( $"[Speed]      Assumed: {Speed}, Real: {Pokemon.Stats[Stat.Speed]}" );
        log.Add( $"" );
        log.Add( $"===[Adapter's Inferred Stats ({Name})]===" );

        HP = _ai.GetUnitInferredStat( this, Stat.HP );
        Attack = _ai.GetUnitInferredStat( this, Stat.Attack );
        Defense = _ai.GetUnitInferredStat( this, Stat.Defense );
        SpAttack = _ai.GetUnitInferredStat( this, Stat.SpAttack );
        SpDefense = _ai.GetUnitInferredStat( this, Stat.SpDefense );
        Speed = _ai.GetUnitInferredStat( this, Stat.Speed );

        log.Add( $"[HP]         Assumed: {HP}, Real: {Pokemon.MaxHP}" );
        log.Add( $"[Attack]     Assumed: {Attack}, Real: {Pokemon.Attack}" );
        log.Add( $"[Defense]    Assumed: {Defense}, Real: {Pokemon.Defense}" );
        log.Add( $"[SpAttack]   Assumed: {SpAttack}, Real: {Pokemon.SpAttack}" );
        log.Add( $"[SpDefense]  Assumed: {SpDefense}, Real: {Pokemon.SpDefense}" );
        log.Add( $"[Speed]      Assumed: {Speed}, Real: {Pokemon.Speed}" );
        log.Add( $"==============================================" );
        log.Add( $"" );

        Debug.Log( log.ToString() );
        log.Clear();
    }
}
