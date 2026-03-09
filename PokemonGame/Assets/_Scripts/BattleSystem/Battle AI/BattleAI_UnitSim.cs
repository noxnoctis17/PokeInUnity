using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleAI_UnitSim
{
    private readonly BattleAI _ai;
    private readonly BattleSystem _bs;
    private readonly Battlefield _field;
    public Dictionary<WeatherConditionID, Func<Move, float>> WeatherDMGModifiers { get; private set; }
    public Dictionary<TerrainID, Func<Move, float>> TerrainDMGModifiers { get; private set; }
    public Dictionary<BattleItemEffectID, Func<Pokemon, Pokemon, Move, float>> ItemDMGModifiers { get; private set; }
    // public CustomLogSession TurnSimLog { get; private set; }

    public BattleAI_UnitSim( BattleAI ai )
    {
        _ai = ai;
        _bs = _ai.BattleSystem;
        _field = _bs.Field;

        // TurnSimLog = new();

        DicsInit();
    }

    private void DicsInit()
    {
        WeatherDicInit();
        TerrainDicInit();
        ItemDicInit();
    }

    // private void LogSimUnit( SimulatedUnit unit )
    // {
    //     TurnSimLog.Add( $"===[Logging Sim Unit: {unit.Name}]===" );
    //     TurnSimLog.Add( $"Name: {unit.Name}" );
    //     TurnSimLog.Add( $"HPR: {unit.CurrentHPR}" );
    //     TurnSimLog.Add( $"Types: {unit.Type.One} / {unit.Type.Two}" );
    //     TurnSimLog.Add( $"Speed: {unit.Speed}" );
    //     TurnSimLog.Add( $"Move: {unit.MTR.Move.MoveSO.Name}" );
    //     TurnSimLog.Add( $"Ungrounded: {unit.IsUngrounded}" );
    //     TurnSimLog.Add( $"Ability: {unit.Ability}" );
    //     TurnSimLog.Add( $"Item: {unit.Item}" );
    //     TurnSimLog.Add( $"Severe Status: {unit.SevereStatus}" );
    //     TurnSimLog.Add( $"Toxic Counter: {unit.ToxicCounter}" );
    //     TurnSimLog.Add( $"Volatile Status Count: {unit.VolatileStatuses.Count}" );
    //     TurnSimLog.Add( $"Court Seeded: {unit.CourtSeeded}" );
    //     TurnSimLog.Add( $"Binding Condition Count: {unit.Bindings.Count}" );
    // }

    // private void LogSimField( SimulatedField field )
    // {
    //     TurnSimLog.Add( $"===[Turn Simulation][Beginning Turn Simulation. Getting Sim Field]===" );
    //     TurnSimLog.Add( $"Weather: {field.Weather}" );
    //     TurnSimLog.Add( $"Terrain: {field.Terrain}" );
    //     TurnSimLog.Add( $"Top Court Condition Count: {field.TopCourtConditions.Count}" );
    //     TurnSimLog.Add( $"Bottom Court Condition Count: {field.BottomCourtConditions.Count}" );
    // }

    // public void LogTop( TurnOutcomeProjection top )
    // {
    //     TurnSimLog.Add( $"Attacker End HP: {top.Attacker_EndOfTurnHP}" );
    //     TurnSimLog.Add( $"Opponent End HP: {top.Opponent_EndOfTurnHP}" );
    //     TurnSimLog.Add( $"Attacker Dies Before Acting: {top.Attacker_DiesBeforeActing}" );
    //     TurnSimLog.Add( $"Opponent Dies Before Acting: {top.Opponent_DiesBeforeActing}" );
    //     TurnSimLog.Add( $"Mutual KO: {top.MutualKO}" );
    // }

    public SimulatedUnit BuildSimUnit( Pokemon pokemon, float hpr, MoveThreatResult mtr, SimulatedField field, StatStageDelta statStages )
    {
        var unit = BuildSimUnit( pokemon, hpr, mtr, field );
        unit.StatStages = statStages;

        return unit;
    }

    public SimulatedUnit BuildSimUnit( Pokemon pokemon, float hpr, MoveThreatResult mtr, SimulatedField field )
    {
        BattleItemEffectID item = pokemon.BattleItemEffect != null ? pokemon.BattleItemEffect.ID : BattleItemEffectID.None;
        SevereConditionID severe =  pokemon.SevereStatus != null ? pokemon.SevereStatus.ID : SevereConditionID.None;
        int toxic = severe == SevereConditionID.TOX ? pokemon.SevereStatusTime : 0;

        List<VolatileConditionID> vol = new();
        foreach( var kvp in pokemon.VolatileStatuses )
            vol.Add( kvp.Key );

        List<BindingConditionID> binds = new();
        foreach( var kvp in pokemon.BindingStatuses )
            binds.Add( kvp.Key );

        var courtLocation = _ai.BattleSystem.Field.GetPokemonCourtLocationFromTrainer( pokemon );
        var court = _ai.BattleSystem.Field.GetPokemonCourtFromTrainer( pokemon );
        bool leechseed = court.Conditions.ContainsKey( CourtConditionID.LeechSeed );

        var statStages = BuildStatStageDelta( pokemon );

        SimulatedUnit unit = new()
        {
            Name = pokemon.NickName,
            CurrentHPR = hpr,
            Type = ( pokemon.PokeSO.Type1, pokemon.PokeSO.Type2 ),
            Speed = _ai.GetUnitContextualSpeed( pokemon ),
            MTR = mtr,
            IsUngrounded = IsUngrounded( pokemon, field ),
            Ability = pokemon.AbilityID,
            Item = item,
            SevereStatus = severe,
            SevereStatusDuration = toxic,
            VolatileStatuses = vol,
            Bindings = binds,
            CourtLocation = courtLocation,
            Court = court,
            CourtSeeded = leechseed,
            StatStages = statStages,
        };

        // LogSimUnit( unit );

        return unit;
    }

    public StatStageDelta BuildStatStageDelta( Pokemon pokemon )
    {
        var changes = pokemon.GetStatStages();
        int attack = 0;
        int defense = 0;
        int spAttack = 0;
        int spDefense = 0;
        int speed = 0;

        foreach( var change in changes )
        {
            switch( change.Stat )
            {
                case Stat.Attack:       attack = change.Change;
                    break;
                case Stat.Defense:      defense = change.Change;
                    break;
                case Stat.SpAttack:     spAttack = change.Change;
                    break;
                case Stat.SpDefense:    spDefense = change.Change;
                    break;
                case Stat.Speed:        speed = change.Change;
                    break;
            };
        }

        return new()
        {
            Attack = attack,
            Defense = defense,
            SpAttack = spAttack,
            SpDefense = spDefense,
            Speed = speed,
        };
    }

    public StatStageDelta BuildStatStageDelta( Move move )
    {
        if( move.MoveSO.MoveEffects.StatChangeList == null || move.MoveSO.MoveEffects.StatChangeList.Count <= 0 )
            return default;

        var changes = move.MoveSO.MoveEffects.StatChangeList;
        int attack = 0;
        int defense = 0;
        int spAttack = 0;
        int spDefense = 0;
        int speed = 0;

        foreach( var change in changes )
        {
            switch( change.Stat )
            {
                case Stat.Attack:       attack = change.Change;
                    break;
                case Stat.Defense:      defense = change.Change;
                    break;
                case Stat.SpAttack:     spAttack = change.Change;
                    break;
                case Stat.SpDefense:    spDefense = change.Change;
                    break;
                case Stat.Speed:        speed = change.Change;
                    break;
            };
        }

        return new()
        {
            Attack = attack,
            Defense = defense,
            SpAttack = spAttack,
            SpDefense = spDefense,
            Speed = speed,
        };
    }

    public SimulatedField BuildSimField()
    {
        WeatherConditionID weather = _field.Weather != null ? _field.Weather.ID : WeatherConditionID.None;
        TerrainID terrain = _field.Terrain != null ? _field.Terrain.ID : TerrainID.None;

        List<CourtConditionID> topCourtConditions = new();
        List<CourtConditionID> bottomCourtConditions = new();

        foreach( var kvp in _field.ActiveCourts[CourtLocation.TopCourt].Conditions )
            topCourtConditions.Add( kvp.Key );

        foreach( var kvp in _field.ActiveCourts[CourtLocation.BottomCourt].Conditions )
            bottomCourtConditions.Add( kvp.Key );

        SimulatedField field = new()
        {
            Weather = weather,
            Terrain = terrain,
            TopCourtConditions = topCourtConditions,
            BottomCourtConditions = bottomCourtConditions,
        };

        // LogSimField( field );

        return field;
    }

    public bool CheckTypes( PokemonType type, SimulatedUnit unit )
    {
        if( type == unit.Type.One || type == unit.Type.Two )
            return true;
        else
            return false;
    }

    public bool IsFainted( SimulatedUnit unit )
    {
        if( unit.CurrentHPR <= 0 )
            return true;
        else
            return false;
    }

    public bool IsUngrounded( Pokemon pokemon, SimulatedField field )
    {
        if( pokemon.CheckTypes( PokemonType.Flying ) || pokemon.AbilityID == AbilityID.Levitate )
            return true;
        else
            return false;
    }

    public bool PredictForcedSwitch( PotentialToKO offensePTKO, PotentialToKO defensePTKO, bool weAreFaster )
    {
        bool weThreatenKO = offensePTKO >= PotentialToKO.Risky;
        bool theyThreatenKO = defensePTKO >= PotentialToKO.Risky;

        if( weThreatenKO && !theyThreatenKO )
            return true;

        if( weThreatenKO && weAreFaster && defensePTKO > PotentialToKO.Dangerous )
            return true;

        return false;
    }

    public List<Move> GetSetupMoves( List<Move> moves )
    {
        List<Move> setupMoves = new();

        foreach( var move in moves )
        {
            if( move.MoveSO.MoveCategory != MoveCategory.Status )
                continue;
            else
            {
                if( move.MoveSO.MoveEffects.StatChangeList?.Count > 0 )
                    setupMoves.Add( move );
                else
                    continue;
            }
        }

        return setupMoves;
    }

    public int ComputeSetupValue( PotentialToKOResult before, PotentialToKOResult after, StatStageDelta delta )
    {
        int value = 0;

        int beforeScore = before.Score;
        int afterScore = after.Score;

        value += ( afterScore - beforeScore ) * 2;

        if( delta.Speed > 0 )
            value += 25;

        if( delta.Defense > 0 || delta.SpDefense > 0 )
            value += 15;

        if( delta.Attack > 1 || delta.SpAttack > 1 )
            value += 40;
        
        if( delta.Attack == 1 || delta.SpAttack == 1 )
            value += 15;

        return value;
    }

    public float PredictSwitchProbability( PotentialToKO offensePTKO, PotentialToKO defensePTKO, bool weAreFaster, float attackerHPR, float opponentHPR )
    {
        float prob = 0.0f;
        bool weThreaten_OHKO = offensePTKO >= PotentialToKO.Dangerous;
        bool theyDoNotThreaten = defensePTKO < PotentialToKO.TwoHKO;
        int theirRemaining = _ai.GetRemainingOpposingPokemon( _ai.Unit.Pokemon ).Count;

        if( PredictForcedSwitch( offensePTKO, defensePTKO, weAreFaster ) )      prob += 0.6f;
        if( weThreaten_OHKO )                                                   prob += 0.2f;
        if( theyDoNotThreaten )                                                 prob += 0.1f;
        if( opponentHPR < 0.35f )                                               prob += 0.1f;
        if( theirRemaining > 2 )                                                prob += 0.1f;
        if( theirRemaining == 1 )                                               prob -= 0.4f;

        prob = Mathf.Clamp01( prob );

        return prob;
    }

    public int Get_ExpectedMoveHits( Move move )
    {
        int expectedHits = 1;

        if( move.MoveSO.HitRange.x >= 2 && move.MoveSO.HitRange.y != 0 )
        {
            int minHits = move.MoveSO.HitRange.x;
            int maxHits = move.MoveSO.HitRange.y;

            expectedHits = Mathf.FloorToInt( ( minHits + maxHits ) * 0.5f );
        }
        else if( move.MoveSO.HitRange.x >= 2 && move.MoveSO.HitRange.y == 0 )
        {
            expectedHits = move.MoveSO.HitRange.x;
        }

        return expectedHits;
    }

    public float Get_MoveModifier( Pokemon attacker, Pokemon target, Move move )
    {
        float modifier = 1f;
        var field = _ai.BattleSystem.Field;

        float stab      = attacker.CheckTypes( move.MoveType ) ? 1.5f : 1f;
        float weather   = 1f;
        float terrain   = 1f;
        float item      = 1f;

        if( field.Weather != null )
        {
            if( _ai.UnitSim.WeatherDMGModifiers.TryGetValue( field.Weather.ID, out var mod ) )
                weather = mod( move );
        }

        if( field.Terrain != null )
        {
            if( _ai.UnitSim.TerrainDMGModifiers.TryGetValue( field.Terrain.ID, out var mod ) )
                terrain = mod( move );
        }

        if( attacker.BattleItemEffect != null )
        {
            if( _ai.UnitSim.ItemDMGModifiers.TryGetValue( attacker.BattleItemEffect.ID, out var mod ) )
                item = mod( attacker, target, move );
        }

        modifier = stab * weather * terrain * item;

        return modifier;
    }

    public int Get_WeatherContextScore( Pokemon pokemon )
    {
        int score = 0;
        var weather = _ai.BattleSystem.Field.Weather;

        if( weather == null )
            return 0;

        if( weather.ID == WeatherConditionID.RAIN )
        {
            if( pokemon.CheckTypes( PokemonType.Water ) )
                score += 5;

            if( pokemon.CheckTypes( PokemonType.Fire ) )
                score -= 5;

            if( pokemon.AbilityID == AbilityID.SwiftSwim /*|| water ability */ )
                score += 10;

            if( pokemon.CheckHasAttackingMoveOfType( PokemonType.Water ) )
                score += 5;

            if( pokemon.CheckHasMove( "Thunder" ) )
                score += 2;

            if( pokemon.CheckHasMove( "Hurricane" ) )
                score += 2;

            return score;
        }

        if( weather.ID == WeatherConditionID.SUNNY )
        {
            if( pokemon.CheckTypes( PokemonType.Fire ) )
                score += 5;

            if( pokemon.CheckTypes( PokemonType.Water ) )
                score -= 5;

            if( pokemon.AbilityID == AbilityID.Chlorophyll || pokemon.AbilityID == AbilityID.SolarPower /*|| sun ability */ )
                score += 10;

            if( pokemon.CheckHasAttackingMoveOfType( PokemonType.Fire ) )
                score += 5;

            if( pokemon.CheckHasMove( "Solar Beam" ) )
                score += 3;

            if( pokemon.CheckHasMove( "Solar Blade" ) )
                score += 3;

            return score;
        }

        if( weather.ID == WeatherConditionID.SANDSTORM )
        {
            if( pokemon.CheckTypes( PokemonType.Rock ) || pokemon.CheckTypes( PokemonType.Ground ) || pokemon.CheckTypes( PokemonType.Steel ) )
                score += 5;

            if( pokemon.AbilityID == AbilityID.SandRush /*|| sand ability*/ )
                score += 10;

            return score;
        }

        if( weather.ID == WeatherConditionID.SNOW )
        {
            if( pokemon.CheckTypes( PokemonType.Ice ) )
                score += 5;

            if( pokemon.CheckTypes( PokemonType.Fighting ) && pokemon.CheckHasAttackingMoveOfType( PokemonType.Fighting ) )
                score -= 5;

            if( pokemon.AbilityID == AbilityID.SlushRush /*|| snow ability */ )
                score += 10;

            if( pokemon.CheckHasAttackingMoveOfType( PokemonType.Ice ) )
                score += 5;

            if( pokemon.CheckHasMove( "Blizzard" ) )
                score += 5;

            return score;
        }

        return score;
    }

    public int Get_TerrainContextScore( Pokemon pokemon )
    {
        int score = 0;
        var terrain = _ai.BattleSystem.Field.Terrain;

        if( terrain == null )
            return 0;

        if( terrain.ID == TerrainID.Blighted )
        {
            if( pokemon.CheckTypes( PokemonType.Ghost ) || pokemon.CheckTypes( PokemonType.Dark ) )
                score += 5;

            if( pokemon.CheckHasAttackingMoveOfType( PokemonType.Ghost ) )
                score += 5;

            if( pokemon.CheckHasAttackingMoveOfType( PokemonType.Dark ) )
                score += 5;

            return score;
        }

        if( terrain.ID == TerrainID.Grassy )
        {
            if( pokemon.CheckTypes( PokemonType.Grass ) )
                score += 5;

            if( pokemon.CheckHasAttackingMoveOfType( PokemonType.Grass ) )
                score += 5;

            if( _ai.Get_HPRatio_AfterEntryHazards( pokemon ) < 0.9f )
                score += 2;

            return score;
        }

        return score;
    }

    public int Get_TrickRoomContextScore( Pokemon pokemon )
    {
        if( !_ai.BattleSystem.BattleFlags[BattleFlag.TrickRoom] )
            return 0;

        int speed = _ai.GetUnitContextualSpeed( pokemon );

        int score = Mathf.Clamp( ( 150 - speed ) / 10, -15, 15 );

        return score;
    }

    private void WeatherDicInit()
    {
        WeatherDMGModifiers = new()
        {
            {
                WeatherConditionID.SUNNY, ( move ) =>
                {
                    if( move.MoveType == PokemonType.Fire )
                        return 1.5f;
                    else if( move.MoveType == PokemonType.Water )
                        return 0.5f;
                    else
                        return 1f;
                }
            },
            {
                WeatherConditionID.RAIN, ( move ) =>
                {
                    if( move.MoveType == PokemonType.Water )
                        return 1.5f;
                    else if( move.MoveType == PokemonType.Fire )
                        return 0.5f;
                    else
                        return 1f;
                }
            },
            {
                WeatherConditionID.SNOW, ( move ) =>
                {
                    if( move.MoveType == PokemonType.Ice )
                        return 1.5f;
                    else if( move.MoveType == PokemonType.Fighting )
                        return 0.5f;
                    else
                        return 1f;
                }
            },
        };
    }

    private void TerrainDicInit()
    {
        TerrainDMGModifiers = new()
        {
            {
                TerrainID.Grassy, ( move ) =>
                {
                    if( move.MoveType == PokemonType.Grass )
                        return 1.3f;
                    else
                        return 1f;
                }
            },
            {
                TerrainID.Psychic, ( move ) =>
                {
                    if( move.MoveType == PokemonType.Psychic )
                        return 1.3f;
                    else
                        return 1f;
                }
            },
            {
                TerrainID.Blighted, ( move ) =>
                {
                    if( move.MoveType == PokemonType.Dark || move.MoveType == PokemonType.Ghost )
                        return 1.3f;
                    else
                        return 1f;
                }
            },
        };
    }

    private void ItemDicInit()
    {
        ItemDMGModifiers = new()
        {
            {
                BattleItemEffectID.LifeOrb, ( attacker, target, move ) =>
                {
                    return 1.3f;
                }
            },
            {
                BattleItemEffectID.MysticWater, ( attacker, target, move ) =>
                {
                    if( move.MoveType == PokemonType.Water )
                        return 1.2f;
                    else
                        return 1f;
                }
            },
            {
                BattleItemEffectID.Charcoal, ( attacker, target, move ) =>
                {
                    if( move.MoveType == PokemonType.Fire )
                        return 1.2f;
                    else
                        return 1f;
                }
            },
            {
                BattleItemEffectID.ExpertBelt, ( attacker, target, move ) =>
                {
                    var effectiveness = TypeChart.GetEffectiveness( move.MoveType, target.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, target.PokeSO.Type2 );
                    if( effectiveness > 1 )
                        return 4915f/4096f;
                    else
                        return 1f;
                }
            },
        };
    }
}

public class SimulatedUnit : IBattleAIUnit
{
    public string Name { get; set; }
    public float CurrentHPR { get; set; }
    public ( PokemonType One, PokemonType Two ) Type { get; set; }
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
    public int SevereStatusDuration { get; set; }
    public List<VolatileConditionID> VolatileStatuses { get; set; }
    public List<BindingConditionID> Bindings { get; set; }

    public CourtLocation CourtLocation { get; set; }
    public Court Court { get; set; }
    public bool CourtSeeded { get; set; }

    public StatStageDelta StatStages { get; set; }
}

public class SimulatedField
{
    public WeatherConditionID Weather;
    public TerrainID Terrain;
    public List<CourtConditionID> TopCourtConditions;
    public List<CourtConditionID> BottomCourtConditions;
}
