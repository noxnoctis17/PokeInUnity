using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BattleAI_UnitSim
{
    private readonly BattleAI _ai;
    private readonly BattleSystem _bs;
    private readonly Battlefield _field;
    public Dictionary<WeatherConditionID, Func<Move, float>> WeatherDMGModifiers { get; private set; }
    public Dictionary<TerrainID, Func<Move, float>> TerrainDMGModifiers { get; private set; }
    public Dictionary<ItemBattleEffectID, Func<IBattleAIUnit, IBattleAIUnit, Move, float>> ItemDMGModifiers { get; private set; }
    public Dictionary<string, Func<IBattleAIUnit, IBattleAIUnit, Move, int>> MovePowerConditions { get; private set; }
    public Dictionary<SevereConditionID, Action<IBattleAIUnit>> SevereConditions { get; private set; }
    public CustomLogSession TurnSimLog { get; private set; }

    public BattleAI_UnitSim( BattleAI ai )
    {
        _ai = ai;
        _bs = _ai.BattleSystem;
        _field = _bs.Field;

        TurnSimLog = new();

        DicsInit();
    }

    private void DicsInit()
    {
        WeatherDicInit();
        TerrainDicInit();
        ItemDicInit();
        MovePowerChangesDicInit();
        SevereConditionsDicInit();
    }

    // public void LogSimUnit( SimulatedUnit unit )
    // {
    //     TurnSimLog.Add( $"===[Simulated Unit: (Lv.{unit.Level}) {unit.Name}]===" );
    //     TurnSimLog.Add( $"Current HPR: {unit.CurrentHPR}" );
    //     TurnSimLog.Add( $"Types: {unit.Type.One} / {unit.Type.Two}" );
    //     TurnSimLog.Add( $"" );
    //     TurnSimLog.Add( $"MaxHP: {unit.HP}" );
    //     TurnSimLog.Add( $"Attack: {unit.Attack}" );
    //     TurnSimLog.Add( $"Defense: {unit.Defense}" );
    //     TurnSimLog.Add( $"SpAttack: {unit.SpAttack}" );
    //     TurnSimLog.Add( $"SpDefense: {unit.SpDefense}" );
    //     TurnSimLog.Add( $"Speed: {unit.Speed}" );
    //     TurnSimLog.Add( $"" );
    //     TurnSimLog.Add( $"Move: {unit.MTR.Move.MoveSO.Name}" );
    //     TurnSimLog.Add( $"Ungrounded: {unit.IsUngrounded}" );
    //     TurnSimLog.Add( $"Ability: {unit.Ability}" );
    //     TurnSimLog.Add( $"Item: {unit.Item}" );
    //     TurnSimLog.Add( $"" );
    //     TurnSimLog.Add( $"Severe Status: {unit.SevereStatus}" );
    //     TurnSimLog.Add( $"Toxic Counter: {unit.SevereStatusTime}" );
    //     TurnSimLog.Add( $"Volatile Status Count: {unit.VolatileStatuses.Count}" );
    //     TurnSimLog.Add( $"Binding Condition Count: {unit.Bindings.Count}" );
    //     TurnSimLog.Add( $"" );
    // }

    public void LogSimField( SimulatedField field )
    {
        TurnSimLog.Add( $"===[Simulated Field]===" );
        TurnSimLog.Add( $"Weather: {field.Weather}, Duration: {field.WeatherDuration}" );
        TurnSimLog.Add( $"Terrain: {field.Terrain}, Duration: {field.TerrainDuration}" );
        TurnSimLog.Add( $"" );
        TurnSimLog.Add( $"Top Court Condition Count: {field.TopCourtConditions.Count}" );
        foreach( var kvp in field.TopCourtConditions )
            TurnSimLog.Add( $"Condition: {kvp.Key}, Duration: {kvp.Value}" );
        TurnSimLog.Add( $"" );
        TurnSimLog.Add( $"Bottom Court Condition Count: {field.BottomCourtConditions.Count}" );
        foreach( var kvp in field.BottomCourtConditions )
            TurnSimLog.Add( $"Condition: {kvp.Key}, Duration: {kvp.Value}" );
        TurnSimLog.Add( $"" );
        TurnSimLog.Add( $"Field Conditions Count: {field.FieldConditions.Count}" );
        foreach( var kvp in field.FieldConditions )
            TurnSimLog.Add( $"Condition: {kvp.Key}, Duration: {kvp.Value}" );
        TurnSimLog.Add( $"" );
        TurnSimLog.Add( $"" );
    }

    public void LogTop( TurnOutcomeProjection top )
    {
        TurnSimLog.Add( $"Attacker End HP: {top.Attacker_EndOfTurnHP}" );
        TurnSimLog.Add( $"Opponent End HP: {top.Opponent_EndOfTurnHP}" );
        TurnSimLog.Add( $"Attacker Ally End HP: {top.AttackerAlly?.EndHPR}" );
        TurnSimLog.Add( $"Opponent Ally End HP: {top.OpponentAlly?.EndHPR}" );
        TurnSimLog.Add( $"Attacker Dies Before Acting: {top.Attacker_DiesBeforeActing}" );
        TurnSimLog.Add( $"Opponent Dies Before Acting: {top.Opponent_DiesBeforeActing}" );
        TurnSimLog.Add( $"Mutual KO: {top.MutualKO}" );
        TurnSimLog.Add( $"" );
    }

    //--Create Simple Sim Unit directly from Pokemon
    public SimulatedUnit BuildSimUnit( Pokemon pokemon, float hpr, MoveThreatResult mtr, SimulatedField field )
    {
        BattleAI_PokemonAdapter mon = _ai.GetPokemonAs_Adapter( pokemon );
        return BuildSimUnit( mon, hpr, mtr, field );
    }

    public SimulatedUnit BuildSimUnit_WithStatus( IBattleAIUnit pokemon, float hpr, MoveThreatResult mtr, SimulatedField field )
    {
        var moveEffects = mtr.Move.MoveSO.MoveEffects;

        if( moveEffects.SevereStatus != SevereConditionID.None )
            pokemon.SevereStatus = moveEffects.SevereStatus;

        if( moveEffects.VolatileStatus != VolatileConditionID.None )
            pokemon.VolatileStatuses.Add( moveEffects.VolatileStatus );

        return BuildSimUnit( pokemon, hpr, mtr, field );
    }

    //--Create a Sim Unit with stat stage changes created from an extracted Stat Stage Delta (from either a pokemon or a move)
    public SimulatedUnit BuildSimUnit_WithStageDelta( IBattleAIUnit pokemon, float hpr, MoveThreatResult mtr, SimulatedField field, StatStageDelta stageDelta )
    {
        var unit = BuildSimUnit( pokemon, hpr, mtr, field );
        ApplyStatStages( unit, stageDelta );

        return unit;
    }

    //--Create Simple Sim Unit from IBattleAIUnit
    public SimulatedUnit BuildSimUnit( IBattleAIUnit pokemon, float hpr, MoveThreatResult mtr, SimulatedField field )
    {
        ItemBattleEffectID item = pokemon.Item;
        SevereConditionID severe =  pokemon.SevereStatus;
        int toxic = pokemon.SevereStatusTime;

        float expendability = _ai.Projection.GetExpendability( pokemon, pokemon.BeginningHPR );

        List<VolatileConditionID> vol = new();
        foreach( var id in pokemon.VolatileStatuses )
            vol.Add( id );

        List<BindingConditionID> binds = new();
        foreach( var id in pokemon.Bindings )
            binds.Add( id );

        var courtLocation = pokemon.CourtLocation;

        //--Copy active moves
        List<Move> activeMoves = new();
        for( int i = 0; i < pokemon.ActiveMoves.Count; i++ )
            activeMoves.Add( pokemon.ActiveMoves[i] );

        //--Copy Stat Stages
        var statStages = pokemon.StatStages.ToDictionary( kvp => kvp.Key, kvp => kvp.Value );

        //--Copy Direct Stat Modifiers
        var directModifiers = pokemon.DirectStatModifiers.ToDictionary( kvp => kvp.Key, kvp => new Dictionary<DirectModifierCause, float>( kvp.Value ) );

        SimulatedUnit unit = new()
        {
            Pokemon = pokemon.Pokemon,
            Name = pokemon.Name,
            PID = pokemon.PID,
            BeginningHPR = hpr,
            EndHPR = hpr,
            Type = ( pokemon.Type.One, pokemon.Type.Two ),

            Level = pokemon.Level,
            HP = pokemon.HP,
            Attack = pokemon.Attack,
            Defense = pokemon.Defense,
            SpAttack = pokemon.SpAttack,
            SpDefense = pokemon.SpDefense,
            Speed = _ai.GetUnitContextualSpeed( pokemon ),

            RoleProfile = pokemon.RoleProfile,
            StatSpread = pokemon.StatSpread,

            ActiveMoves = activeMoves,
            MTR = mtr,

            IsUngrounded = IsUngrounded( pokemon, field ),

            Expendability = expendability,

            Ability = pokemon.Ability,
            Item = item,

            SevereStatus = severe,
            SevereStatusTime = toxic,
            VolatileStatuses = vol,
            Bindings = binds,

            CourtLocation = courtLocation,

            StatStages = statStages,
            DirectStatModifiers = directModifiers,
        };

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

    public void ApplyStatStages( IBattleAIUnit unit, StatStageDelta stageDelta )
    {
        var pokemon = unit.Pokemon;

        List<StatStage> statStages = new()
        {
            new(){ Stat = Stat.Attack,      Change = stageDelta.Attack      + pokemon.StatStages[Stat.Attack] }, //--We do + existing stages because we need to actually consider existing stages lol
            new(){ Stat = Stat.Defense,     Change = stageDelta.Defense     + pokemon.StatStages[Stat.Defense] },
            new(){ Stat = Stat.SpAttack,    Change = stageDelta.SpAttack    + pokemon.StatStages[Stat.SpAttack] },
            new(){ Stat = Stat.SpDefense,   Change = stageDelta.SpDefense   + pokemon.StatStages[Stat.SpDefense] },
            new(){ Stat = Stat.Speed,       Change = stageDelta.Speed       + pokemon.StatStages[Stat.Speed] },
        };

        for( int i = 0; i < statStages.Count; i++ )
        {
            var stages = statStages[i];
            unit.StatStages[stages.Stat] = stages.Change;
        }
    }

    public void ClearStatStages( IBattleAIUnit unit )
    {
        foreach( var sc in unit.StatStages )
        {
            unit.StatStages[sc.Key] = 0;
        }
    }

    public void UndoStageDelta( IBattleAIUnit unit, StatStageDelta delta )
    {
        //--Positive Delta Changes
        if( delta.Attack > 0 )
            unit.StatStages[Stat.Attack] -= delta.Attack;

        if( delta.Defense > 0 )
            unit.StatStages[Stat.Defense] -= delta.Defense;

        if( delta.SpAttack > 0 )
            unit.StatStages[Stat.SpAttack] -= delta.SpAttack;

        if( delta.SpDefense > 0 )
            unit.StatStages[Stat.SpDefense] -= delta.SpDefense;

        if( delta.Speed > 0 )
            unit.StatStages[Stat.Speed] -= delta.Speed;

        //--Negative Delta Changes
        if( delta.Attack < 0 )
            unit.StatStages[Stat.Attack] += delta.Attack;

        if( delta.Defense < 0 )
            unit.StatStages[Stat.Defense] += delta.Defense;

        if( delta.SpAttack < 0 )
            unit.StatStages[Stat.SpAttack] += delta.SpAttack;

        if( delta.SpDefense < 0 )
            unit.StatStages[Stat.SpDefense] += delta.SpDefense;

        if( delta.Speed < 0 )
            unit.StatStages[Stat.Speed] += delta.Speed;
    }

    public SimulatedUnit GetSimUnit( IBattleAIUnit attacker, IBattleAIUnit target, SimulatedField field )
    {
        //--HP Ratios
        float attackerHPR = attacker.BeginningHPR;

        //--Move Threat Result
        var attackerMTR = attacker.MTR ?? _ai.CandidateSelect.GetMove_BestAttack( attacker, target );

        field ??= _ai.Blackboard.CurrentFieldSnapshot;

        return BuildSimUnit( attacker, attackerHPR, attackerMTR, field );
    }

    public SimulatedUnit CopySimUnit( IBattleAIUnit unit, SimulatedField field )
    {
        float hpr = unit.BeginningHPR;
        var mtr = unit.MTR;
        
        field ??= _ai.Blackboard.CurrentFieldSnapshot;

        return BuildSimUnit( unit, hpr, mtr, field );
    }

    public void UpdateUnitForLookAhead( ref IBattleAIUnit unit )
    {
        unit.BeginningHPR = unit.EndHPR;
    }

    public SimulatedField BuildSimField()
    {
        WeatherConditionID weather = _field.Weather != null ? _field.Weather.ID : WeatherConditionID.None;
        TerrainID terrain = _field.Terrain != null ? _field.Terrain.ID : TerrainID.None;
        int weatherDuration = _field.Weather != null ? (int)_field.WeatherDuration : 0;
        int terrainDuration = _field.Terrain != null ? (int)_field.TerrainDuration : 0;

        Dictionary<CourtConditionID, int> topCourtConditions = new();
        Dictionary<CourtConditionID, int> bottomCourtConditions = new();

        int topSpikesLayers = 0;
        int bottomSpikesLayers = 0;

        foreach( var kvp in _field.ActiveCourts[CourtLocation.TopCourt].Conditions )
        {
            topCourtConditions.Add( kvp.Key, kvp.Value.TimeLeft );
            
            if( kvp.Key == CourtConditionID.Spikes )
            {
                topSpikesLayers = _field.ActiveCourts[CourtLocation.TopCourt].Conditions[CourtConditionID.Spikes].Layers;
            }
        }

        foreach( var kvp in _field.ActiveCourts[CourtLocation.BottomCourt].Conditions )
        {
            bottomCourtConditions.Add( kvp.Key, kvp.Value.TimeLeft );
            
            if( kvp.Key == CourtConditionID.Spikes )
            {
                bottomSpikesLayers = _field.ActiveCourts[CourtLocation.BottomCourt].Conditions[CourtConditionID.Spikes].Layers;
            }
        }

        Dictionary<FieldConditionID, int> fieldConditions = new();

        foreach( var kvp in _field.FieldConditions )
        {
            fieldConditions.Add( kvp.Key, kvp.Value.TimeLeft );
        }

        SimulatedField field = new()
        {
            Weather = weather,
            WeatherDuration = weatherDuration,

            Terrain = terrain,
            TerrainDuration = terrainDuration,

            TopCourtConditions = topCourtConditions,
            BottomCourtConditions = bottomCourtConditions,

            TopSpikesLayers = topSpikesLayers,
            BottomSpikesLayers = bottomSpikesLayers,

            FieldConditions = fieldConditions,
            TrickRoomActive = fieldConditions.ContainsKey( FieldConditionID.TrickRoom ),
            TrickRoomDuration = fieldConditions.TryGetValue( FieldConditionID.TrickRoom, out int duration ) ? duration : -1,
        };

        // LogSimField( field );

        return field;
    }

    public SimulatedField CopySimField( SimulatedField field )
    {
        SimulatedField copy = new()
        {
            Weather = field.Weather,
            WeatherDuration = field.WeatherDuration,
            
            Terrain = field.Terrain,
            TerrainDuration = field.TerrainDuration,

            TopCourtConditions = field.TopCourtConditions,
            BottomCourtConditions = field.BottomCourtConditions,
            FieldConditions = field.FieldConditions,

            TopSpikesLayers = field.TopSpikesLayers,
            BottomSpikesLayers = field.BottomSpikesLayers,
            TrickRoomActive = field.TrickRoomActive,
            TrickRoomDuration = field.TrickRoomDuration,
        };

        return copy;
    }

    public Move GetRandomMove( IBattleAIUnit pokemon )
    {
        int r = UnityEngine.Random.Range( 0, pokemon.ActiveMoves.Count );
        var unit = _ai.GetBattleUnit( pokemon.Pokemon );

        if( unit != null && unit.Flags[UnitFlags.ChoiceItem].IsActive && unit.LastUsedMove != null )
            return unit.LastUsedMove;
        else
            return pokemon.ActiveMoves[r];
    }

    public bool CheckTypes( PokemonType type, IBattleAIUnit unit )
    {
        if( type == unit.Type.One || type == unit.Type.Two )
            return true;
        else
            return false;
    }

    public bool IsFainted( IBattleAIUnit unit )
    {
        if( unit.EndHPR <= 0 )
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

    public bool IsUngrounded( IBattleAIUnit pokemon, SimulatedField field )
    {
        if( CheckTypes( PokemonType.Flying, pokemon ) || pokemon.Ability == AbilityID.Levitate )
            return true;
        else
            return false;
    }

    public bool CanActOnTurn( IBattleAIUnit pokemon )
    {
        if( pokemon.SevereStatus == SevereConditionID.PAR && pokemon.SevereStatusTime > 0 )
            return false;

        if( pokemon.SevereStatus == SevereConditionID.SLP && pokemon.SevereStatusTime > 0 )
            return false;

        return true;
    }

    public bool AttackerMovesFirst( IBattleAIUnit attacker, IBattleAIUnit target, Move attackerMove, Move targetMove )
    {
        var attMovePrio = attackerMove.Priority;
        var tarMovePrio = targetMove.Priority;

        bool attackerMovesFirst = false;

        if( attMovePrio != tarMovePrio )
            attackerMovesFirst = attMovePrio > tarMovePrio;
        else
            attackerMovesFirst = attacker.Speed > target.Speed;

        return attackerMovesFirst;
    }

    public WeatherConditionID GetWeatherFrom_Ability( Pokemon pokemon )
    {
        var ability = pokemon.AbilityID;

        if( ability == AbilityID.Drought )
            return WeatherConditionID.SUNNY;

        if( ability == AbilityID.Drizzle )
            return WeatherConditionID.RAIN;

        if( ability == AbilityID.Sandstream )
            return WeatherConditionID.SANDSTORM;

        if( ability == AbilityID.SnowWarning )
            return WeatherConditionID.SNOW;
        
        return WeatherConditionID.None;
    }

    public WeatherConditionID GetWeatherFrom_Moveset( Pokemon pokemon )
    {
        var moves = pokemon.ActiveMoves;

        foreach( var move in moves )
        {
            var weather = move.MoveSO.MoveEffects.Weather;

            if( weather == WeatherConditionID.SUNNY )
                return WeatherConditionID.SUNNY;

            if( weather == WeatherConditionID.RAIN )
                return WeatherConditionID.RAIN;

            if( weather == WeatherConditionID.SANDSTORM )
                return WeatherConditionID.SANDSTORM;

            if( weather == WeatherConditionID.SNOW )
                return WeatherConditionID.SNOW;
        }

        return WeatherConditionID.None;
    }

    public WeatherConditionID GetWeatherFrom_Move( Move move )
    {
        var weather = move.MoveSO.MoveEffects.Weather;

        if( weather == WeatherConditionID.SUNNY )
            return WeatherConditionID.SUNNY;

        if( weather == WeatherConditionID.RAIN )
            return WeatherConditionID.RAIN;

        if( weather == WeatherConditionID.SANDSTORM )
            return WeatherConditionID.SANDSTORM;

        if( weather == WeatherConditionID.SNOW )
            return WeatherConditionID.SNOW;
        
        return WeatherConditionID.None;
    }

    public TerrainID GetTerrainFromAbility( Pokemon pokemon )
    {
        var ability = pokemon.AbilityID;

        if( ability == AbilityID.GrassySurge )
            return TerrainID.Grassy;

        if( ability == AbilityID.PsychicSurge )
            return TerrainID.Psychic;

        if( ability == AbilityID.BlightSurge )
            return TerrainID.Blighted;

        return TerrainID.None;
    }

    //--TODO: fill this out with all relevant abilities
    public bool PokemonAbilityMatchesWeather( Pokemon pokemon, WeatherConditionID weather )
    {
        var ability = pokemon.AbilityID;
        return weather switch
        {
            WeatherConditionID.None => false,
            WeatherConditionID.SUNNY => ability == AbilityID.Chlorophyll,
            WeatherConditionID.RAIN => ability == AbilityID.SwiftSwim || ability == AbilityID.Hydration,
            WeatherConditionID.SANDSTORM => ability == AbilityID.SandRush || ability == AbilityID.SandForce || ability == AbilityID.SandVeil,
            WeatherConditionID.SNOW => ability == AbilityID.SlushRush || ability == AbilityID.IceBody,
            _ => false,
        };
    }

    public bool PokemonHasMove_AbusesWeather( Pokemon pokemon, WeatherConditionID weather )
    {
        var moves = pokemon.ActiveMoves;
        foreach( var move in moves )
        {
            var name = move.MoveSO.Name;

            if( weather == WeatherConditionID.SUNNY )
            {
                if( name == "Heat Wave" || name == "Eruption" || name == "Solar Beam" )
                    return true;
            }

            if( weather == WeatherConditionID.RAIN )
            {
                if( name == "Thunder" || name == "Muddy Water" || name == "Water Spout" || name == "Hurricane" )
                    return true;
            }

            if( weather == WeatherConditionID.SANDSTORM )
            {
                if( name == "Rock Slide" )
                    return true;
            }

            if( weather == WeatherConditionID.SNOW )
            {
                if( name == "Blizzard" )
                    return true;
            }
        }

        return false;
    }

    public bool PokemonBenefitsFromSevereStatus( Pokemon pokemon )
    {
        var ability = pokemon.AbilityID;
        bool hasFacade = pokemon.CheckHasActiveMove( "Facade" );
        
        if( ability == AbilityID.Guts || ability == AbilityID.MarvelScale || ability == AbilityID.QuickFeet || hasFacade )
            return true;
        else
            return false;
    }

    public bool PokemonHasLoweredStats( Pokemon pokemon )
    {
        foreach( var kvp in pokemon.StatStages )
        {
            var change = kvp.Value;
            if( change < 0 )
                return true;
        }

        return false;
    }

    public bool PokemonHasAbility_OffensiveAmplification( Pokemon pokemon )
    {
        var ability = pokemon.AbilityID;

        if( ability == AbilityID.Blaze || ability == AbilityID.Torrent || ability == AbilityID.Overgrow || ability == AbilityID.Swarm )
            return true;

        if( ability == AbilityID.Adaptability || ability == AbilityID.SheerForce || ability == AbilityID.Technician || ability == AbilityID.Hustle || ability == AbilityID.Sniper || ability == AbilityID.Superluck )
            return true;

        if( ability == AbilityID.Analytic || ability == AbilityID.SolarPower || ability == AbilityID.SandForce || ability == AbilityID.Illuminate || ability == AbilityID.CompoundEyes )
            return true;

        if(  ability == AbilityID.Pixilate || ability == AbilityID.LiquidVoice || ability == AbilityID.Burninate || ability == AbilityID.Electrize || ability == AbilityID.Liquidize )
            return true;

        return false;
    }

    public bool PokemonHasAbility_WeatherSpeed( Pokemon pokemon )
    {
        var ability = pokemon.AbilityID;

        if( ability == AbilityID.SwiftSwim || ability == AbilityID.Chlorophyll || ability == AbilityID.SandRush || ability == AbilityID.SlushRush )
            return true;

        return false;
    }

    public bool PokemonHasAbility_SpeedManipulation( Pokemon pokemon )
    {
        var ability = pokemon.AbilityID;

        if( PokemonHasAbility_WeatherSpeed( pokemon ) || ability == AbilityID.QuickFeet || ability == AbilityID.Steadfast )
            return true;

        return false;
    }

    public bool PokemonHasAbility_DefensiveSustain( Pokemon pokemon )
    {
        var ability = pokemon.AbilityID;

        if( ability == AbilityID.MarvelScale || ability == AbilityID.ThickFat || ability == AbilityID.WaterAbsorb || ability == AbilityID.NaturalCure || ability == AbilityID.Hydration || ability == AbilityID.Levitate || ability == AbilityID.LightMetal )
            return true;

        return false;
    }

    public bool PokemonHasAbility_UtilityOrDisruption( Pokemon pokemon )
    {
        var ability = pokemon.AbilityID;

        if( ability == AbilityID.Intimidate || ability == AbilityID.Demoralize || ability == AbilityID.Prankster || ability == AbilityID.MagicBounce )
            return true;

        if( ability == AbilityID.ArenaTrap || ability == AbilityID.StormDrain || ability == AbilityID.LightningRod || ability == AbilityID.Pressure || ability == AbilityID.SereneGrace )
            return true;

        if( ability == AbilityID.Triage || ability == AbilityID.LeafGuard || ability == AbilityID.Infiltrator || ability == AbilityID.Healer || ability == AbilityID.FriendGuard )
            return true;

        return false;
    }

    public bool PokemonHasAbility_TeamArchetype( Pokemon pokemon )
    {
        bool weather = PokemonHasWeatherSetter_Ability( pokemon );
        bool terrain = PokemonHasTerrainSetter_Ability( pokemon );

        if( weather || terrain )
            return true;

        return false;
    }

    public bool PokemonHasAbility_Punishment( Pokemon pokemon )
    {
        var ability = pokemon.AbilityID;

        if( ability == AbilityID.CursedBody || ability == AbilityID.RoughSkin || ability == AbilityID.FlameBody || ability == AbilityID.Static || ability == AbilityID.CuteCharm || ability == AbilityID.PoisonPoint || ability == AbilityID.Pressure )
            return true;

        return false;
    }

    public bool PokemonHasAbility_CounterPlay( Pokemon pokemon )
    {
        var ability = pokemon.AbilityID;

        if( ability == AbilityID.Competitive || ability == AbilityID.Defiant || ability == AbilityID.MirrorArmor || ability == AbilityID.Justified )
            return true;

        if( ability == AbilityID.StickyHold || ability == AbilityID.Oblivious || ability == AbilityID.InnerFocus || ability == AbilityID.Soundproof )
            return true;

        if( ability == AbilityID.WhiteSmoke || ability == AbilityID.ClearBody || ability == AbilityID.HyperCutter )
            return true;

        return false;
    }

    public bool PokemonHasAbility_Recovery( Pokemon pokemon )
    {
        var ability = pokemon.AbilityID;

        if( ability == AbilityID.Healer || ability == AbilityID.NaturalCure || ability == AbilityID.Regenerator )
            return true;

        if( ability == AbilityID.RainDish || ability == AbilityID.DrySkin || ability == AbilityID.WaterAbsorb )
            return true;

        return false;
    }

    public bool PokemonHas_SpeedControl( Pokemon pokemon )
    {
        var moves = pokemon.ActiveMoves;

        if( pokemon.CheckHasActiveMove( "Tailwind" ) )
            return true;

        foreach( var move in moves )
        {
            var effects = move.MoveEffects;
            var statChanges = effects.StatChangeList;
            var target = effects.Target;
            if( ( target == EffectTarget.Enemy || target == EffectTarget.OpposingSide ) && statChanges != null && statChanges.Count > 0 )
            {
                foreach( var sc in statChanges )
                {
                    if( sc.Stat == Stat.Speed && sc.Change < 0 )
                        return true;
                }
            }
        }

        return false;
    }

    public bool PokemonHasMove_Trapping( Pokemon pokemon )
    {
        var moves = pokemon.ActiveMoves;

        foreach( var move in moves )
        {
            var trap = move.MoveEffects.BindingStatus;
            if( trap != BindingConditionID.None )
                return true;
        }

        return false;
    }

    public bool PokemonHasMove_AllyHealing( Pokemon pokemon )
    {
        var moves = pokemon.ActiveMoves;

        foreach( var move in moves )
        {
            var heal = move.MoveSO.HealType;
            var moveTarget = move.MoveSO.MoveTarget;
            var name = move.MoveSO.Name;
            if( ( moveTarget == MoveTarget.Ally || moveTarget == MoveTarget.AllySide ) && heal != HealType.None )
                return true;

            if( name == "Healing Wish" || name == "Wish" || name == "Aromatherapy" )
                return true;
        }

        return false;
    }

    public bool PokemonHasWeatherSetter_Ability( Pokemon pokemon )
    {
        if( pokemon.AbilityID == AbilityID.Drought || pokemon.AbilityID == AbilityID.Drizzle || pokemon.AbilityID == AbilityID.Sandstream || pokemon.AbilityID == AbilityID.SnowWarning )
            return true;
        else
            return false;
    }

    public bool PokemonHasWeatherSetter_Move( Pokemon pokemon )
    {
        if( pokemon.CheckHasActiveMove( "Sunny Day" ) || pokemon.CheckHasActiveMove( "Rain Dance" ) || pokemon.CheckHasActiveMove( "Sandstorm" ) || pokemon.CheckHasActiveMove( "Snowscape" ) )
            return true;
        else
            return false;
    }

    public bool PokemonHas_MatchingWeatherSpeedAbility( Pokemon pokemon, WeatherConditionID weather )
    {
        return weather switch
        {
            WeatherConditionID.None => false,
            WeatherConditionID.SUNNY => pokemon.AbilityID == AbilityID.Chlorophyll,
            WeatherConditionID.RAIN => pokemon.AbilityID == AbilityID.SwiftSwim,
            WeatherConditionID.SANDSTORM => pokemon.AbilityID == AbilityID.SandRush,
            WeatherConditionID.SNOW => pokemon.AbilityID == AbilityID.SlushRush,
            _ => false,
        };
    }

    public bool Pokemon_ChangesWeather( Pokemon pokemon, SimulatedField field = null )
    {
        field ??= _ai.Blackboard.CurrentFieldSnapshot;
        bool pokemonSetsWeather = PokemonHasWeatherSetter_Ability( pokemon );
        bool pokemonChangesWeather = false;
        WeatherConditionID candidatesWeather = WeatherConditionID.None;

        if( pokemonSetsWeather )
        {
            switch( pokemon.AbilityID )
            {
                case AbilityID.Drought: candidatesWeather = WeatherConditionID.SUNNY; break;
                case AbilityID.Drizzle: candidatesWeather = WeatherConditionID.RAIN; break;
                case AbilityID.Sandstream: candidatesWeather = WeatherConditionID.SANDSTORM; break;
                case AbilityID.SnowWarning: candidatesWeather = WeatherConditionID.SNOW; break;
            }

            if( candidatesWeather != WeatherConditionID.None && candidatesWeather != field.Weather )
                pokemonChangesWeather = true;
        }

        return pokemonChangesWeather;
    }

    public bool Switch_ChangesWeather( SwitchCandidateResult scr, SimulatedField field = null )
    {
        field ??= _ai.Blackboard.CurrentFieldSnapshot;
        var top = scr.Top;

        var switchCandidate = top.Attacker.Pokemon;
        bool switchSetsWeather = PokemonHasWeatherSetter_Ability( switchCandidate );
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

            if( candidatesWeather != WeatherConditionID.None && candidatesWeather != field.Weather )
                switchChangesWeather = true;
        }

        return switchChangesWeather;
    }

    public bool Move_ChangesWeather( Move move, SimulatedField field = null )
    {
        field ??= _ai.Blackboard.CurrentFieldSnapshot;
        var weather = move.MoveSO.MoveEffects.Weather;

        bool moveSetsWeather = weather != WeatherConditionID.None;
        bool moveChangesWeather = false;

        if( moveSetsWeather )
        {
            if( weather != WeatherConditionID.None && weather != field.Weather )
                moveChangesWeather = true;
        }

        return moveChangesWeather;
    }

    public bool PokemonHasTerrainSetter_Ability( Pokemon pokemon )
    {
        if( pokemon.AbilityID == AbilityID.GrassySurge || pokemon.AbilityID == AbilityID.PsychicSurge || pokemon.AbilityID == AbilityID.BlightSurge )
            return true;
        else
            return false;
    }

    public bool PokemonHasTerrainSetter_Move( Pokemon pokemon )
    {
        if( pokemon.CheckHasActiveMove( "Grassy Terrain" ) || pokemon.CheckHasActiveMove( "Psychic Terrain" ) || pokemon.CheckHasActiveMove( "Misty Terrain" ) || pokemon.CheckHasActiveMove( "Electric Terrain" ) )
            return true;
        else
            return false;
    }

    public bool Pokemon_ChangesTerrain( Pokemon pokemon, SimulatedField field = null )
    {
        field ??= _ai.Blackboard.CurrentFieldSnapshot;
        bool pokemonSetsTerrain = PokemonHasTerrainSetter_Ability( pokemon ) || PokemonHasTerrainSetter_Move( pokemon );
        bool pokemonChangesTerrain = false;
        TerrainID candidatesTerrain = TerrainID.None;

        if( pokemonSetsTerrain )
        {
            switch( pokemon.AbilityID )
            {
                case AbilityID.PsychicSurge: candidatesTerrain = TerrainID.Psychic; break;
                case AbilityID.GrassySurge: candidatesTerrain = TerrainID.Grassy; break;
                case AbilityID.BlightSurge: candidatesTerrain = TerrainID.Blighted; break;
            }

            if( candidatesTerrain != TerrainID.None && candidatesTerrain != field.Terrain )
                pokemonChangesTerrain = true;
        }

        return pokemonChangesTerrain;
    }

    public bool TeamHasWeatherSetter_Ability( List<Pokemon> team )
    {
        for( int i = 0; i < team.Count; i++ )
        {
            var mon = team[i];
            if( PokemonHasWeatherSetter_Ability( mon ) )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool TeamHasWeatherSetter_Move( List<Pokemon> team )
    {
        for( int i = 0; i < team.Count; i++ )
        {
            var mon = team[i];
            if( PokemonHasWeatherSetter_Move( mon ) )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool TeamHasTerrainSetter_Ability( List<Pokemon> team )
    {
        for( int i = 0; i < team.Count; i++ )
        {
            var mon = team[i];
            if( PokemonHasTerrainSetter_Ability( mon ) )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool TeamHasTerrainSetter_Move( List<Pokemon> team )
    {
        for( int i = 0; i < team.Count; i++ )
        {
            var mon = team[i];
            if( PokemonHasTerrainSetter_Move( mon ) )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool TeamHasTailwindSetter( List<Pokemon> team )
    {
        for( int i = 0; i < team.Count; i++ )
        {
            var mon = team[i];
            if( mon.CheckHasActiveMove( "Tailwind" ) )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool TeamHasReflectSetter( List<Pokemon> team )
    {
        for( int i = 0; i < team.Count; i++ )
        {
            var mon = team[i];
            if( mon.CheckHasActiveMove( "Reflect" ) )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool TeamHasLightScreenSetter( List<Pokemon> team )
    {
        for( int i = 0; i < team.Count; i++ )
        {
            var mon = team[i];
            if( mon.CheckHasActiveMove( "Light Screen" ) )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool TeamHasAuroraSetter( List<Pokemon> team )
    {
        for( int i = 0; i < team.Count; i++ )
        {
            var mon = team[i];
            if( mon.CheckHasActiveMove( "Aurora Veil" ) )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool TeamHasTrickRoomSetter( List<Pokemon> team )
    {
        for( int i = 0; i < team.Count; i++ )
        {
            var mon = team[i];
            if( mon.CheckHasActiveMove( "Trick Room" ) )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool TeamHasHazardSetter( List<Pokemon> team )
    {
        for( int i = 0; i < team.Count; i++ )
        {
            var mon = team[i];
            if( PokemonHasMove_HazardSet( mon ) )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool PokemonHasMove_HazardSet( Pokemon pokemon )
    {
        if( pokemon.CheckHasActiveMove( "Stealth Rock" ) || pokemon.CheckHasActiveMove( "Sticky Web" ) || pokemon.CheckHasActiveMove( "Leech Seed" ) || pokemon.CheckHasActiveMove( "Spikes" ) || pokemon.CheckHasActiveMove( "Toxic Spikes" ) )
                return true;

        return false;
    }

    public bool PokemonHasMove_HazardRemoval( Pokemon pokemon )
    {
        if( pokemon.CheckHasActiveMove( "Rapid Spin" ) || pokemon.CheckHasActiveMove( "Defog" ) || pokemon.CheckHasActiveMove( "Mortal Spin" ) )
            return true;

        return false;
    }

    public bool PokemonHasMove_Screens( Pokemon pokemon )
    {
        if( pokemon.CheckHasActiveMove( "Reflect" ) || pokemon.CheckHasActiveMove( "Light Screen" ) || pokemon.CheckHasActiveMove( "Aurora Veil" ) )
            return true;

        return false;
    }

    public bool PokemonIsOffensiveRole( IBattleAIUnit unit )
    {
        var primary = unit.RoleProfile.PrimaryRole;

        return primary == RoleClass.BulkyAttacker || primary == RoleClass.RevengeKiller || primary == RoleClass.SetupSweeper || primary == RoleClass.Sweeper || primary == RoleClass.TrickRoomAbuser || primary == RoleClass.WallBreaker;
    }

    public bool MoveIsEntryHazard( Move move )
    {
        string name = move.MoveSO.Name;
        if( name == "Stealth Rock" || name == "Sticky Web" || name == "Leech Seed" || name == "Spikes" || name == "Toxic Spikes" )
            return true;
        else
            return false;
    }

    public bool MoveIsSetup( Move move )
    {
        if( move.MoveSO.MoveCategory != MoveCategory.Status )
            return false;

        bool isSetupMove = move.MoveSO.MoveEffects.StatChangeList?.Count > 0 && ( move.MoveSO.MoveEffects.Target == EffectTarget.Self || move.MoveSO.MoveEffects.Target == EffectTarget.AllySide );
        return isSetupMove;
    }

    public bool MoveIsOffensiveSetupPlus2( Move move )
    {
        if( move.MoveSO.MoveCategory != MoveCategory.Status )
            return false;

        var statChanges = move.MoveSO.MoveEffects.StatChangeList;

        foreach( var sc in statChanges )
        {
            if( ( sc.Stat == Stat.Attack || sc.Stat == Stat.SpAttack ) && sc.Change > 1 )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool MoveIsDefensiveSetupPlus2( Move move )
    {
        if( move.MoveSO.MoveCategory != MoveCategory.Status )
            return false;

        var statChanges = move.MoveSO.MoveEffects.StatChangeList;

        foreach( var sc in statChanges )
        {
            if( ( sc.Stat == Stat.Defense || sc.Stat == Stat.SpDefense ) && sc.Change > 1 )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool PokemonIsIronDefenseBodyPress( Pokemon pokemon )
    {
        var moves = pokemon.ActiveMoves;
        int count = 0;

        foreach( var move in moves )
        {
            if( move.MoveSO.Name == "Iron Defense" )
                count++;

            if( move.MoveSO.Name == "Body Press" )
                count++;

            if( count >= 2 )
                break;
        }

        if( count >= 2 )
            return true;
        else
            return false;
    }

    public bool MoveIsDebuff( Move move )
    {
        var statChanges = move.MoveSO.MoveEffects.StatChangeList;
        var target = move.MoveSO.MoveEffects.Target;
        var cat = move.MoveSO.MoveCategory;

        if( statChanges == null || statChanges.Count <= 0 || target == EffectTarget.Self || target == EffectTarget.AllySide || cat != MoveCategory.Status )
            return false;

        foreach( var sc in statChanges )
        {
            if( sc.Change < 0 )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool MoveAppliesStatus( Move move )
    {
        var effects = move.MoveSO.MoveEffects;
        var target = move.MoveSO.MoveEffects.Target;
        var cat = move.MoveSO.MoveCategory;
        var vs = effects.VolatileStatus;

        if( target == EffectTarget.Self || target == EffectTarget.AllySide || cat != MoveCategory.Status )
            return false;

        if( effects.SevereStatus != SevereConditionID.None )
            return true;

        if( vs == VolatileConditionID.Confusion || vs == VolatileConditionID.Cursed || vs == VolatileConditionID.Infatuation || vs == VolatileConditionID.Yawn )
            return true;

        if( effects.TransientStatus == TransientConditionID.Flinch )
            return true;

        return false;
    }

    public bool MoveIsSupport( Move move )
    {
        var cat = move.MoveSO.MoveCategory;
        if( cat != MoveCategory.Status )
            return false;

        var moveSO = move.MoveSO;
        var effects = move.MoveSO.MoveEffects;
        var moveTarget = move.MoveTarget;
        var effectTarget = effects.Target;
        var statChanges = effects.StatChangeList;
        var courtCon = effects.CourtCondition;

        if( effectTarget == EffectTarget.AllySide || moveTarget == MoveTarget.Ally || moveTarget == MoveTarget.AllySide )
        {
            if( statChanges != null && statChanges.Count > 0 )
            {
                foreach( var sc in statChanges )
                {
                    if( sc.Change > 0 )
                        return true;
                }
            }

            if( moveSO.HealType != HealType.None )
                return true;

            //--Screens check
            if( courtCon == CourtConditionID.AuroraVeil || courtCon == CourtConditionID.Reflect || courtCon == CourtConditionID.LightScreen )
                return true;

            //--Side-Guard checks
            if( courtCon == CourtConditionID.QuickGuard || courtCon == CourtConditionID.WideGuard || courtCon == CourtConditionID.SafeGuard )
                return true;

            //--Helping Hand check
            if( effects.VolatileStatus == VolatileConditionID.HelpingHand )
                return true;
        }

        //--Redirection check
        if( effects.TransientStatus == TransientConditionID.CenterOfAttention )
            return true;

        return false;
    }

    public bool MoveIsRedirection( Move move )
    {
        var cat = move.MoveSO.MoveCategory;
        if( cat != MoveCategory.Status )
            return false;

        var effects = move.MoveSO.MoveEffects;

        //--Redirection check
        if( effects.TransientStatus == TransientConditionID.CenterOfAttention )
            return true;
        else
            return false;
    }

    public bool MoveIsBattlefieldControl( Move move )
    {
        var cat = move.MoveSO.MoveCategory;
        if( cat != MoveCategory.Status )
            return false;

        var effects = move.MoveSO.MoveEffects;

        if( effects.Weather != WeatherConditionID.None )
            return true;

        if( effects.Terrain != TerrainID.None )
            return true;

        if( effects.CourtCondition == CourtConditionID.Tailwind )
            return true;

        if( effects.FieldCondition != FieldConditionID.None )
            return true;

        return false;
    }

    public bool MoveIsPivot( Move move )
    {
        var effects = move.MoveSO.MoveEffects;

        if( effects.SwitchType == SwitchEffectType.SelfPivot )
            return true;
        else
            return false;
    }

    public bool PokemonHasMove_Pivot( Pokemon pokemon )
    {
        var moves = pokemon.ActiveMoves;

        foreach( var move in moves )
        {
            if( MoveIsPivot( move ) )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool PokemonHasMove_Phaze( Pokemon pokemon )
    {
        var moves = pokemon.ActiveMoves;

        foreach( var move in moves )
        {
            if( MoveIsPhaze( move ) )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool PokemonHasMove_Priority( Pokemon pokemon )
    {
        var moves = pokemon.ActiveMoves;

        foreach( var move in moves )
        {
            var prio = move.Priority;
            var cat = move.MoveSO.MoveCategory;

            if( cat == MoveCategory.Status && pokemon.AbilityID == AbilityID.Prankster )
                return true;

            if( prio > MovePriority.Zero )
                return true;
        }

        return false;
    }

    public bool PokemonHasMove_OffensivePriority( Pokemon pokemon )
    {
        var moves = pokemon.ActiveMoves;

        foreach( var move in moves )
        {
            var prio = move.Priority;

            if( prio > MovePriority.Zero )
                return true;
        }

        return false;
    }

    public bool PokemonHasMove_Spread( Pokemon pokemon )
    {
        var moves = pokemon.ActiveMoves;

        foreach( var move in moves )
        {
            var target = move.MoveSO.MoveTarget;

            if( target == MoveTarget.AllAdjacent || target == MoveTarget.OpposingSide )
                return true;
        }

        return false;
    }

    public bool MoveIsPhaze( Move move )
    {
        var effects = move.MoveSO.MoveEffects;

        if( effects.SwitchType == SwitchEffectType.Phaze )
            return true;
        else
            return false;
    }

    public bool MoveIsSelfHeal( Move move )
    {
        var moveSO = move.MoveSO;
        var moveTarget = move.MoveTarget;

        if( moveSO.MoveCategory != MoveCategory.Status )
            return false;

        if( moveSO.HealType != HealType.None && moveTarget == MoveTarget.Self )
            return true;

        if( moveSO.Name == "Life Dew" )
            return true;

        return false;
    }

    public bool MoveHasDrawback( Move move )
    {
        var moveSO = move.MoveSO;
        var effects = moveSO.MoveEffects;
        var statChanges = effects.StatChangeList;

        if( moveSO.Recoil.RecoilType != RecoilType.None )
            return true;

        if( statChanges != null && statChanges.Count > 0 )
        {
            foreach( var sc in statChanges )
            {
                if( sc.Change < 0 )
                    return true;
                else
                    continue;
            }
        }

        if( moveSO.Flags.Contains( MoveFlags.Recharge ) )
            return true;

        return false;
    }

    public List<Move> GetSetupMoves( List<Move> moves )
    {
        List<Move> setupMoves = new();

        foreach( var move in moves )
        {
            bool isSetupMove = MoveIsSetup( move );

            if( isSetupMove )
                setupMoves.Add( move );
            else
                continue;
        }

        return setupMoves;
    }

    public bool CheckHasPhazeMove( Pokemon pokemon )
    {
        for( int i = 0; i < pokemon.ActiveMoves.Count; i++ )
        {
            var move = pokemon.ActiveMoves[i];
            if( move.MoveSO.MoveEffects.SwitchType == SwitchEffectType.Phaze )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool CheckHasMove( IBattleAIUnit pokemon, string move )
    {
        for( int i = 0; i < pokemon.ActiveMoves.Count; i++ )
        {
            if( pokemon.ActiveMoves[i].MoveSO.Name == move )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool PokemonHasMove_Recovery( Pokemon pokemon )
    {
        var moves = pokemon.ActiveMoves;

        foreach( var move in moves )
        {
            if( MoveIsSelfHeal( move ) )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool PokemonHasMove_SideRecovery( Pokemon pokemon )
    {
        var moves = pokemon.ActiveMoves;

        foreach( var move in moves )
        {
            var moveTarget = move.MoveSO.MoveTarget;
            var effectTarget = move.MoveSO.MoveEffects.Target;

            if( move.MoveSO.HealType != HealType.None && ( moveTarget == MoveTarget.Ally || moveTarget == MoveTarget.AllySide || effectTarget == EffectTarget.AllySide ) )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool CheckCurseIsVolatile( IBattleAIUnit pokemon )
    {        
        if( CheckTypes( PokemonType.Ghost, pokemon ) )
            return true;
        else
            return false;
    }

    public Move GetCurseFromActiveMoves( List<Move> moves )
    {
        for( int i = 0; i < moves.Count; i++ )
        {
            if( moves[i].MoveSO.Name == "Curse" )
                return moves[i];
            else
                continue;
        }

        return null;
    }

    public List<Move> GetOffensiveStatusMoves( List<Move> moves )
    {
        List<Move> statusMoves = new();

        foreach( var move in moves )
        {
            bool isSetupMove = move.MoveSO.MoveEffects.StatChangeList?.Count > 0 && ( move.MoveSO.MoveEffects.Target == EffectTarget.Self || move.MoveSO.MoveEffects.Target == EffectTarget.AllySide );
            bool isOffensiveStatus = move.MoveSO.MoveEffects.Target == EffectTarget.Enemy || move.MoveSO.MoveEffects.Target == EffectTarget.OpposingSide;
            var category = move.MoveSO.MoveCategory;

            if( category != MoveCategory.Status )
                continue;
            else
            {
                if( !isSetupMove && isOffensiveStatus )
                    statusMoves.Add( move );
                else
                    continue;
            }
        }

        return statusMoves;
    }

    public List<Move> GetSupportiveStatusMoves( List<Move> moves )
    {
        List<Move> statusMoves = new();

        foreach( var move in moves )
        {
            var category = move.MoveSO.MoveCategory;
            var effects = move.MoveSO.MoveEffects;
            var target = move.MoveSO.MoveTarget;
            bool isSetupMove = effects.StatChangeList?.Count > 0 && effects.Target == EffectTarget.Self;
            bool moveEffectsTargetUs = move.MoveSO.MoveEffects.Target == EffectTarget.Self || effects.Target == EffectTarget.AllySide;
            bool moveTargetsUs = target == MoveTarget.AllField || target == MoveTarget.Ally || target == MoveTarget.AllySide || target == MoveTarget.Self;
            bool moveIsProtection = effects.TransientStatus == TransientConditionID.Protect || effects.CourtCondition == CourtConditionID.QuickGuard || effects.CourtCondition == CourtConditionID.WideGuard;
            bool isSupportiveStatus =  category == MoveCategory.Status && !isSetupMove && ( moveEffectsTargetUs || moveTargetsUs ) && !moveIsProtection;
            
            if( isSupportiveStatus )
                statusMoves.Add( move );
            else
                continue;
        }

        return statusMoves;
    }

    public bool CheckHasSelfDebuffMove( List<Move> moves )
    {
        for( int i = 0; i < moves.Count; i++ )
        {
            var move = moves[i];
            var cat = move.MoveSO.MoveCategory;
            var statChanges = move.MoveSO.MoveEffects.StatChangeList;

            if( cat == MoveCategory.Status )
                continue;

            if( statChanges != null && statChanges.Count > 0 )
            {
                foreach( var sc in statChanges )
                {
                    if( sc.Change < 0 )
                        return true;
                    else
                        continue;
                }
            }
        }

        return false;
    }

    public bool CheckHasRecoilMove( List<Move> moves )
    {
        for( int i = 0; i < moves.Count; i++ )
        {
            var move = moves[i];
            var recoil = move.MoveSO.Recoil;

            if( recoil != null && recoil.RecoilType != RecoilType.None )
                return true;
            else
                continue;
        }

        return false;
    }

    public void ApplySupportEffect( IBattleAIUnit unit, Move move, SimulatedField field )
    {
        var moveTarget = move.MoveSO.MoveTarget;
        var effects = move.MoveSO.MoveEffects;
        var court = unit.CourtLocation == CourtLocation.TopCourt ? field.TopCourtConditions : field.BottomCourtConditions;
        var battleField = _ai.BattleSystem.Field;
        var realCourt = battleField.ActiveCourts[unit.CourtLocation];

        bool isAllySetup = _ai.UnitSim.MoveIsSetup( move ) && effects.Target == EffectTarget.AllySide;
        bool isHelpingHand = effects.VolatileStatus == VolatileConditionID.HelpingHand;

        bool isWeather = effects.Weather != WeatherConditionID.None;
        bool isTerrain = effects.Terrain != TerrainID.None;
        bool isField = effects.FieldCondition != FieldConditionID.None;

        bool isTailwind = effects.CourtCondition == CourtConditionID.Tailwind;
        bool isScreens = effects.CourtCondition == CourtConditionID.Reflect || effects.CourtCondition == CourtConditionID.LightScreen || effects.CourtCondition == CourtConditionID.AuroraVeil;
        bool isSafeguard = effects.CourtCondition == CourtConditionID.SafeGuard;

        bool isAllyHeal = move.MoveSO.HealType != HealType.None && moveTarget == MoveTarget.Ally;
        bool isSideHeal = move.MoveSO.HealType != HealType.None && moveTarget == MoveTarget.AllySide;

        if( isAllySetup )
        {
            var stages = _ai.UnitSim.BuildStatStageDelta( move );
            _ai.UnitSim.ApplyStatStages( unit, stages );
        }

        if( isHelpingHand && _ai.IsDoubleBattle )
        {
            unit.VolatileStatuses.Add( VolatileConditionID.HelpingHand );
        }

        if( isWeather )
        {
            field.Weather = effects.Weather;
        }

        if( isTerrain )
        {
            field.Terrain = effects.Terrain;
        }

        if( isField )
        {
            int duration = FieldConditionDB.Conditions[effects.FieldCondition].Duration;
            field.FieldConditions.Add( effects.FieldCondition, duration );
        }

        if( isTailwind || isScreens || isSafeguard )
        {
            int duration = CourtConditionDB.Conditions[effects.CourtCondition].Duration;
            court.Add( effects.CourtCondition, duration );
        }

        if( isAllyHeal || isSideHeal )
        {
            float healAmount = (float)move.MoveSO.HealAmount / 100f;

            unit.BeginningHPR += Mathf.Clamp01( healAmount );
            unit.EndHPR += Mathf.Clamp01( healAmount );
        }
    }

    public int ComputeOffensiveSetupValue( PotentialToKOResult before, PotentialToKOResult after, StatStageDelta delta )
    {
        int value = 0;

        int beforeScore = before.Score;
        int afterScore = after.Score;

        value += ( afterScore - beforeScore ) * 2;

        if( delta.Speed > 0 )
            value += 25;

        if( delta.Attack > 1 || delta.SpAttack > 1 )
            value += 40;
        
        if( delta.Attack == 1 || delta.SpAttack == 1 )
            value += 15;

        return value;
    }

    public int ComputeDefensiveSetupValue( PotentialToKOResult before, PotentialToKOResult after, StatStageDelta delta )
    {
        int value = 0;

        int beforeScore = before.Score;
        int afterScore = after.Score;

        value += ( beforeScore - afterScore ) * 2;

        if( delta.Speed > 0 )
            value += 5;

        if( delta.Defense == 1 || delta.SpDefense == 1 )
            value += 15;

        if( delta.Defense > 1 || delta.SpDefense > 1 )
            value += 25;

        return value;
    }

    public float PredictSwitchProbability( Pokemon switcher, PotentialToKO offensePTKO, PotentialToKO defensePTKO, bool weAreFaster, float attacker, float opponentHPR, float opponentExpendability, bool log = false, string targetName = "no name" )
    {
        int theirRemaining = _ai.GetRemainingAllyPokemon( switcher ).Count;
        if( theirRemaining == 1 )
            return 0f;

        CustomLogSession probabilityLog = new();

        float score = 0f;
        float bias = -0.5f;

        //--Features
        const float THREAT_CHECK = 1f;
        const float GUARANTEED_OHKO = 2f;
        const float PRESERVE_RANGE = 1f;
        const float SACK_RANGE = 1f;
        const float BENCH_COUNT = 1.5f;

        bool weThreatenOHKO = offensePTKO >= PotentialToKO.Risky;
        bool theyThreatenOHKO = defensePTKO >= PotentialToKO.Risky;

        if( log ) probabilityLog.Add( $"" );
        if( log ) probabilityLog.Add( $"===[Predicting Opponent {targetName} Switch Probability...]===" );

        //--Our Threat Check
        //--We have a guaranteed KO scenario and we outspeed them
        if( offensePTKO == PotentialToKO.OHKO && weAreFaster )
        {
            score += 1.25f * GUARANTEED_OHKO;
            if( log ) probabilityLog.Add( $"We have a guaranteed KO scenario and we outspeed them. Score: {score}" );
        }
        else if( offensePTKO == PotentialToKO.OHKO )
        {
            score += 0.85f * GUARANTEED_OHKO;
            if( log ) probabilityLog.Add( $"We have an OHKO on them. Score: {score}" );
        }
        else if( weThreatenOHKO && weAreFaster && !theyThreatenOHKO )
        {
            score += 1.5f * THREAT_CHECK;
            if( log ) probabilityLog.Add( $"We threaten a KO and they don't! Score: {score}" );
        }
        else if( weThreatenOHKO && weAreFaster )
        {
            score += 1.25f * THREAT_CHECK;
            if( log ) probabilityLog.Add( $"We threaten a KO and we're faster! Score: {score}" );
        }
        else if( weAreFaster && !theyThreatenOHKO )
        {
            score += 1f * THREAT_CHECK;
            if( log ) probabilityLog.Add( $"We're faster and they don't threaten a KO! Score: {score}" );
        }
        else if( weAreFaster )
        {
            score += 0.85f * THREAT_CHECK;
            if( log ) probabilityLog.Add( $"We're faster. Score: {score}" );
        }
        //--If we do not threaten them, they will probably stay in
        else if( offensePTKO <= PotentialToKO.TwoHKO )
        {
            score -= 1f * THREAT_CHECK;
            if( log ) probabilityLog.Add( $"We don't threaten them at all Score: {score}" );
        }

        //--Their threat against us check
        //--They have a guaranteed KO scenario and they outspeed us.
        if( defensePTKO == PotentialToKO.OHKO && !weAreFaster )
        {
            score -= 1.25f * GUARANTEED_OHKO;
            if( log ) probabilityLog.Add( $"They have a guaranteed KO scenario and they outspeed us. Score: {score}" );
        }
        if( defensePTKO == PotentialToKO.OHKO )
        {
            score -= 0.85f * GUARANTEED_OHKO;
            if( log ) probabilityLog.Add( $"They have an OHKO on us. Score: {score}" );
        }
        else if( theyThreatenOHKO && !weAreFaster && !weThreatenOHKO )
        {
            score -= 1.5f * THREAT_CHECK;
            if( log ) probabilityLog.Add( $"They threaten a KO and we don't! Score: {score}" );
        }
        else if( theyThreatenOHKO && !weAreFaster )
        {
            score -= 1.25f * THREAT_CHECK;
            if( log ) probabilityLog.Add( $"They threaten an OHKO and we are slower! Score: {score}" );
        }
        else if( !weAreFaster && !weThreatenOHKO )
        {
            score -= 1f * THREAT_CHECK;
            if( log ) probabilityLog.Add( $"We're slower and do not threaten them. Score: {score}" );
        }

        //--HP Range check. Are they in a preservable but dangerous hp range?
        if( opponentHPR > 0.3f && opponentHPR <= 0.55f && offensePTKO >= PotentialToKO.Risky )
        {
            score += 1f * PRESERVE_RANGE;
            if( log ) probabilityLog.Add( $"Opponent current HP <= 30%! Score: {score}" );
        }

        //--Bench count check. If they have fewer pokemon, switching is less likely.
        if( theirRemaining <= 2 )
        {
            score -= 1f * BENCH_COUNT;
            if( log ) probabilityLog.Add( $"They have 2 or less Pokemon! Score: {score}" );
        }

        //--If we threaten a KO, the unit's potential to be sacrificed instead of switched out should negatively influence probability.
        //--We get a hp gradient and utilize their expendability as a weight for the feature
        if( weThreatenOHKO )
        {
            float lowHP = Mathf.Clamp01( ( 0.35f - opponentHPR ) / 0.35f );
            score -= lowHP * opponentExpendability * SACK_RANGE;
        }

        //--Calculate total feature score with a slight weight + intercept/bias that leans toward not switching.
        float total = score /** 0.75f*/ + bias;
        if( log ) probabilityLog.Add( $"Total probability: {total} (Score {score} + {bias})" );

        float prob = 1f / ( 1f + Mathf.Exp( -total ) );
        prob = Mathf.Round( prob * 100f ) / 100f;

        if( log ) probabilityLog.Add( $"Final Probability: {prob}" );
        if( log ) probabilityLog.Add( $"===" );
        if( log ) probabilityLog.Add( $"" );

        if( log ) Debug.Log( probabilityLog.ToString() );
        if( log ) probabilityLog.Clear();

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

    public float Get_MoveModifier( IBattleAIUnit attacker, IBattleAIUnit target, Move move, SimulatedField field = null )
    {
        const float SCREENS_MODIFIER = 0.66796875f;
        const float AURORA_VEIL_MODIFIER = 0.6669921875f;

        float modifier = 1f;
        field ??= _ai.Blackboard.CurrentFieldSnapshot;
        var targetCourt = target.CourtLocation == CourtLocation.TopCourt ? field.TopCourtConditions : field.BottomCourtConditions;

        float stab      = CheckTypes( move.MoveType, attacker ) ? 1.5f : 1f;
        float weather   = 1f;
        float terrain   = 1f;
        float item      = 1f;
        float helping   = 1f;
        float screens   = 1f;
        float aurora    = 1f;

        if( field.Weather != WeatherConditionID.None )
        {
            if( _ai.UnitSim.WeatherDMGModifiers.TryGetValue( field.Weather, out var mod ) )
                weather = mod( move );
        }

        if( field.Terrain != TerrainID.None )
        {
            if( _ai.UnitSim.TerrainDMGModifiers.TryGetValue( field.Terrain, out var mod ) )
                terrain = mod( move );
        }

        if( attacker.Item != ItemBattleEffectID.None )
        {
            if( _ai.UnitSim.ItemDMGModifiers.TryGetValue( attacker.Item, out var mod ) )
                item = mod( attacker, target, move );
        }

        if( attacker.VolatileStatuses.Contains( VolatileConditionID.HelpingHand ) )
        {
            helping = 1.5f;
        }

        if( targetCourt.ContainsKey( CourtConditionID.Reflect ) && move.MoveSO.MoveCategory == MoveCategory.Physical )
        {
            screens = SCREENS_MODIFIER;
        }

        if( targetCourt.ContainsKey( CourtConditionID.LightScreen ) && move.MoveSO.MoveCategory == MoveCategory.Special )
        {
            screens = SCREENS_MODIFIER;
        }

        if( targetCourt.ContainsKey( CourtConditionID.AuroraVeil ) )
        {
            aurora = AURORA_VEIL_MODIFIER;
        }

        modifier = stab * weather * terrain * item * helping * screens * aurora;

        return modifier;
    }

    public float Get_MoveEffectiveness( IBattleAIUnit target, Move move )
    {
        float effectiveness = 1f;

        if( move.MoveSO.MoveCategory == MoveCategory.Status )
            return 0f;

        //--Base Effectiveness
        effectiveness = TypeChart.GetTotalMoveEffectiveness( target.Type, move );

        //--Contextual Effectiveness. Typically ability-based effectiveness changes, such as levitate or water absorb. growing list as of 04/11/26
        if( ( target.Ability == AbilityID.Levitate || target.IsUngrounded ) && move.MoveType == PokemonType.Ground )
            effectiveness = 0;

        if( target.Ability == AbilityID.WaterAbsorb && move.MoveType == PokemonType.Water )
            effectiveness = 0;

        if( target.Ability == AbilityID.LightningRod && move.MoveType == PokemonType.Electric )
            effectiveness = 0;

        if( move.MoveSO.Flags.Contains( MoveFlags.Powder ) && CheckTypes( PokemonType.Grass, target ) )
            effectiveness = 0;

        if( move.MoveSO.Flags.Contains( MoveFlags.Sound ) && target.Ability == AbilityID.Soundproof )
            effectiveness = 0;

        return effectiveness;
    }

    public int Get_WeatherContextScore( Pokemon pokemon, WeatherConditionID checkWeather = WeatherConditionID.None )
    {
        int score = 0;
        var weather = checkWeather != WeatherConditionID.None ? checkWeather : _ai.BattleSystem.Field.Weather?.ID;
        
        if( _ai.BattleSystem.Field.Weather == null )
            weather = WeatherConditionID.None;

        if( weather == WeatherConditionID.None )
            return 0;

        if( weather == WeatherConditionID.SUNNY )
        {
            if( pokemon.CheckTypes( PokemonType.Fire ) )
                score += 5;

            if( pokemon.CheckTypes( PokemonType.Water ) || pokemon.CheckHasAttackingMoveOfType( PokemonType.Water ) )
                score -= 5;

            if( TypeChart.GetTotalEffectiveness( PokemonType.Water, pokemon.PokeSO.Type1, pokemon.PokeSO.Type2 ) > 1 )
                score += 5;

            if( pokemon.AbilityID == AbilityID.Chlorophyll || pokemon.AbilityID == AbilityID.SolarPower /*|| sun ability */ )
                score += 10;

            if( pokemon.CheckHasAttackingMoveOfType( PokemonType.Fire ) )
                score += 5;

            if( pokemon.CheckHasActiveMove( "Solar Beam" ) )
                score += 3;

            if( pokemon.CheckHasActiveMove( "Solar Blade" ) )
                score += 3;

            return score;
        }

        if( weather == WeatherConditionID.RAIN )
        {
            if( pokemon.CheckTypes( PokemonType.Water ) )
                score += 5;

            if( pokemon.CheckTypes( PokemonType.Fire ) || pokemon.CheckHasAttackingMoveOfType( PokemonType.Fire ) )
                score -= 5;

            if( TypeChart.GetTotalEffectiveness( PokemonType.Fire, pokemon.PokeSO.Type1, pokemon.PokeSO.Type2 ) > 1 )
                score += 5;

            if( pokemon.AbilityID == AbilityID.SwiftSwim /*|| water ability */ )
                score += 10;

            if( pokemon.CheckHasAttackingMoveOfType( PokemonType.Water ) )
                score += 5;

            if( pokemon.CheckHasActiveMove( "Thunder" ) )
                score += 2;

            if( pokemon.CheckHasActiveMove( "Hurricane" ) )
                score += 2;

            return score;
        }

        if( weather == WeatherConditionID.SANDSTORM )
        {
            if( pokemon.CheckTypes( PokemonType.Rock ) || pokemon.CheckTypes( PokemonType.Ground ) || pokemon.CheckTypes( PokemonType.Steel ) )
                score += 10;

            if( pokemon.AbilityID == AbilityID.SandRush || pokemon.AbilityID == AbilityID.SandForce /*|| sand ability*/ )
                score += 10;

            return score;
        }

        if( weather == WeatherConditionID.SNOW )
        {
            if( pokemon.CheckTypes( PokemonType.Ice ) )
                score += 10;

            if( pokemon.CheckTypes( PokemonType.Fighting ) || pokemon.CheckHasAttackingMoveOfType( PokemonType.Fighting ) )
                score -= 5;

            if( TypeChart.GetTotalEffectiveness( PokemonType.Ice, pokemon.PokeSO.Type1, pokemon.PokeSO.Type2 ) > 1 )
                score -= 5;

            if( pokemon.AbilityID == AbilityID.SlushRush || pokemon.AbilityID == AbilityID.SnowCloak /*|| snow ability */ )
                score += 10;

            if( pokemon.CheckHasAttackingMoveOfType( PokemonType.Ice ) )
                score += 5;

            if( pokemon.CheckHasActiveMove( "Blizzard" ) )
                score += 5;

            return score;
        }

        return score;
    }

    public int Get_TerrainContextScore( Pokemon pokemon, TerrainID checkTerrain = TerrainID.None )
    {
        int score = 0;
        var terrain = _ai.BattleSystem.Field.Terrain;
        
        var id = terrain?.ID;
        
        if( checkTerrain != TerrainID.None )
            id = checkTerrain;

        if( terrain == null || id == TerrainID.None )
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

        if( terrain.ID == TerrainID.Psychic )
        {
            if( pokemon.CheckHasAttackingMoveOfType( PokemonType.Psychic ) )
                score += 5;

            if( pokemon.CheckHasActiveMove( "Expanding Force" ) )
                score += 15;

            if( PokemonHasMove_OffensivePriority( pokemon ) )
                score -= 5;

            if( pokemon.CheckHasActiveMove( "Fake Out" ) )
            {
                score -= 10;
            }
        }

        return score;
    }

    public int Get_TrickRoomContextScore( Pokemon pokemon, bool checkValue = false )
    {
        if( !checkValue && !_ai.BattleSystem.BattleFlags[BattleFlag.TrickRoom] )
            return 0;

        var adapter = _ai.GetPokemonAs_Adapter( pokemon );
        var rp = adapter.RoleProfile;
        var secondaries = rp.SecondaryRoles;
        var signals = rp.Signals;
        var biases = rp.Biases;
        var traits = rp.Traits;

        bool offensive = rp.PrimaryRole == RoleClass.BulkyAttacker || rp.PrimaryRole == RoleClass.RevengeKiller || rp.PrimaryRole == RoleClass.SetupSweeper ||
            rp.PrimaryRole == RoleClass.Sweeper || rp.PrimaryRole == RoleClass.TrickRoomAbuser || rp.PrimaryRole == RoleClass.WallBreaker;

        bool defensive = rp.PrimaryRole == RoleClass.Wall || rp.PrimaryRole == RoleClass.DefensiveSetup;
        bool utility = !offensive && !defensive;

        int speed = _ai.GetUnitContextualSpeed( pokemon );

        int score = Mathf.Clamp( ( 150 - speed ) / 10, -15, 15 );

        if( offensive )
        {
            if( rp.PrimaryRole == RoleClass.TrickRoomAbuser || secondaries.Contains( RoleClass.TrickRoomAbuser ) )
            {
                score += 5;
            }

            if( biases.Contains( RoleBias.FastSpeed ) )
            {
                score -= 15;
            }
            else if( biases.Contains( RoleBias.MiddlingSpeed ) )
            {
                score -= 10;
            }
            else if( biases.Contains( RoleBias.AwkwardSpeed ) )
            {
                score -= 5;
            }
            else if( biases.Contains( RoleBias.SlowSpeed ) )
            {
                score += 10;

                if( ( rp.PrimaryRole == RoleClass.SetupSweeper || secondaries.Contains( RoleClass.SetupSweeper ) ) && ( biases.Contains( RoleBias.PhysicallyBulky ) || biases.Contains( RoleBias.SpeciallyBulky ) ) )
                {
                    score += 5;
                }
            }
            else if( biases.Contains( RoleBias.TrickRoomSpeed ) )
            {
                score += 15;

                if( ( rp.PrimaryRole == RoleClass.SetupSweeper || secondaries.Contains( RoleClass.SetupSweeper ) ) && ( biases.Contains( RoleBias.PhysicallyBulky ) || biases.Contains( RoleBias.SpeciallyBulky ) ) )
                {
                    score += 5;
                }
            }
        }
        else if( defensive )
        {
            if( secondaries.Contains( RoleClass.TrickRoomAbuser ) )
            {
                score += 5;
            }

            if( biases.Contains( RoleBias.Disruptive ) )
            {
                score += 5;
            }

            if( biases.Contains( RoleBias.PassivePressure ) )
            {
                score += 5;
            }

            if( pokemon.CheckHasActiveMove( "Follow Me" ) || pokemon.CheckHasActiveMove( "Rage Powder" ) )
            {
                score += 5;
            }
        }
        else if( utility )
        {
            if( secondaries.Contains( RoleClass.TrickRoomAbuser ) )
            {
                score += 5;
            }

            if( pokemon.CheckHasActiveMove( "Follow Me" ) || pokemon.CheckHasActiveMove( "Rage Powder" ) )
            {
                score += 5;
            }

            if( ( traits.Contains( RoleTrait.Cleric ) || traits.Contains( RoleTrait.StatusSpreader ) || biases.Contains( RoleBias.Disruptive ) ) && ( biases.Contains( RoleBias.SlowSpeed ) || biases.Contains( RoleBias.TrickRoomSpeed ) ) )
            {
                score += 10;
            }
        }

        if( PokemonHasMove_OffensivePriority( pokemon ) )
        {
            score += 5;
        }

        if( pokemon.BattleItemEffect?.ID == ItemBattleEffectID.ChoiceScarf )
        {
            score -= 10;
        }

        if( pokemon.StatStages?.Count > 0 )
        {
            foreach( var sc in pokemon.StatStages )
            {
                if( sc.Key == Stat.Speed )
                {
                    if( sc.Value > 0 )
                        score -= 5;
                    else if( sc.Value < 0 )
                        score += 5;
                }
            }
        }

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
                WeatherConditionID.SANDSTORM, ( move ) =>
                {
                    bool boosts = move.MoveType == PokemonType.Rock || move.MoveType == PokemonType.Ground || move.MoveType == PokemonType.Steel;

                    if( boosts )
                        return 1.3f;
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
                ItemBattleEffectID.LifeOrb, ( attacker, target, move ) =>
                {
                    return 1.3f;
                }
            },
            {
                ItemBattleEffectID.MysticWater, ( attacker, target, move ) =>
                {
                    if( move.MoveType == PokemonType.Water )
                        return 1.2f;
                    else
                        return 1f;
                }
            },
            {
                ItemBattleEffectID.Charcoal, ( attacker, target, move ) =>
                {
                    if( move.MoveType == PokemonType.Fire )
                        return 1.2f;
                    else
                        return 1f;
                }
            },
            {
                ItemBattleEffectID.ExpertBelt, ( attacker, target, move ) =>
                {
                    var effectiveness = TypeChart.GetEffectiveness( move.MoveType, target.Type.One ) * TypeChart.GetEffectiveness( move.MoveType, target.Type.Two );
                    if( effectiveness > 1 )
                        return 4915f/4096f;
                    else
                        return 1f;
                }
            },
        };
    }

    private void MovePowerChangesDicInit()
    {
        MovePowerConditions = new()
        {
            {
                "Knock Off", ( attacker, target, move ) =>
                {
                    if( target.Item != ItemBattleEffectID.None )
                        return Mathf.FloorToInt( move.MovePower * 1.5f );
                    else
                        return move.MovePower;
                }
            },
            {
                "Eruption", ( attacker, target, move ) =>
                {
                    int hp = attacker.Pokemon.CurrentHP;
                    int maxHP = attacker.Pokemon.MaxHP;
                    int power = ( 150 * hp ) / maxHP;

                    return Mathf.Max( power, 1 );
                }
            },
            {
                "Water Spout", ( attacker, target, move ) =>
                {
                    int hp = attacker.Pokemon.CurrentHP;
                    int maxHP = attacker.Pokemon.MaxHP;
                    int power = ( 150 * hp ) / maxHP;

                    return Mathf.Max( power, 1 );
                }
            },
        };
    }

    private void AbilityDicInit()
    {
        
    }

    private void SevereConditionsDicInit()
    {
        SevereConditions = new()
        {
            {
                SevereConditionID.PSN, ( attacker ) =>
                {
                    attacker.SevereStatus = SevereConditionID.PSN;
                } 
            },
            {
                SevereConditionID.TOX, ( attacker ) =>
                {
                    attacker.SevereStatus = SevereConditionID.TOX;
                    attacker.SevereStatusTime = 1;
                } 
            },
            {
                SevereConditionID.BRN, ( attacker ) =>
                {
                    attacker.SevereStatus = SevereConditionID.BRN;
                } 
            },
            {
                SevereConditionID.FBT, ( attacker ) =>
                {
                    attacker.SevereStatus = SevereConditionID.FBT;
                } 
            },
            {
                SevereConditionID.PAR, ( attacker ) =>
                {
                    attacker.SevereStatus = SevereConditionID.PAR;
                    attacker.SevereStatusTime = 1;
                } 
            },
            {
                SevereConditionID.SLP, ( attacker ) =>
                {
                    attacker.SevereStatus = SevereConditionID.SLP;
                    attacker.SevereStatusTime = 2;
                } 
            },
        };
    }

}

public class SimulatedUnit : IBattleAIUnit
{
    public Pokemon Pokemon { get; set; }
    public string Name { get; set; }
    public string PID { get; set; }
    public float BeginningHPR { get; set; }
    public float EndHPR { get; set; }
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

    public bool Phazed { get; set; }

    public AbilityID Ability { get; set; }
    public ItemBattleEffectID Item { get; set; }

    public SevereConditionID SevereStatus { get; set; }
    public int SevereStatusTime { get; set; }
    public List<VolatileConditionID> VolatileStatuses { get; set; }
    public List<BindingConditionID> Bindings { get; set; }

    public CourtLocation CourtLocation { get; set; }
    
    public Dictionary<Stat, int> StatStages { get; set; }
    public Dictionary<Stat, Dictionary<DirectModifierCause, float>> DirectStatModifiers{ get; set; }
}

public class SimulatedField
{
    public WeatherConditionID Weather;
    public WeatherConditionID LastWeather;
    public int WeatherDuration;
    public TerrainID Terrain;
    public TerrainID LastTerrain;
    public int TerrainDuration;
    public Dictionary<CourtConditionID, int> TopCourtConditions;
    public Dictionary<CourtConditionID, int> BottomCourtConditions;
    public Dictionary<FieldConditionID, int> FieldConditions;
    public int TopSpikesLayers;
    public int BottomSpikesLayers;
    public bool TrickRoomActive;
    public int TrickRoomDuration;
}
