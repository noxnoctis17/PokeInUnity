using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
using UnityEngine;

public class BattleAI_UnitSim
{
    private readonly BattleAI _ai;
    private readonly BattleSystem _bs;
    private readonly Battlefield _field;
    public Dictionary<WeatherConditionID, Func<Move, float>> WeatherDMGModifiers { get; private set; }
    public Dictionary<TerrainID, Func<Move, float>> TerrainDMGModifiers { get; private set; }
    public Dictionary<BattleItemEffectID, Func<IBattleAIUnit, IBattleAIUnit, Move, float>> ItemDMGModifiers { get; private set; }
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

    // private void LogSimField( SimulatedField field )
    // {
    //     TurnSimLog.Add( $"===[Simulated Field]===" );
    //     TurnSimLog.Add( $"Weather: {field.Weather}" );
    //     TurnSimLog.Add( $"Terrain: {field.Terrain}" );
    //     TurnSimLog.Add( $"Top Court Condition Count: {field.TopCourtConditions.Count}" );
    //     TurnSimLog.Add( $"Bottom Court Condition Count: {field.BottomCourtConditions.Count}" );
    //     TurnSimLog.Add( $"" );
    // }

    public void LogTop( TurnOutcomeProjection top )
    {
        TurnSimLog.Add( $"Attacker End HP: {top.Attacker_EndOfTurnHP}" );
        TurnSimLog.Add( $"Opponent End HP: {top.Opponent_EndOfTurnHP}" );
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

        return unit;
    }

    //--Create Simple Sim Unit from IBattleAIUnit
    public SimulatedUnit BuildSimUnit( IBattleAIUnit pokemon, float hpr, MoveThreatResult mtr, SimulatedField field )
    {
        BattleItemEffectID item = pokemon.Item;
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
            CurrentHPR = hpr,
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

    public Dictionary<Stat, int> BuildStatStagesDictionary( StatStageDelta delta )
    {
        return new()
        {
            { Stat.Attack,      delta.Attack },
            { Stat.Defense,     delta.Defense },
            { Stat.SpAttack,    delta.SpAttack },
            { Stat.SpDefense,   delta.SpDefense },
            { Stat.Speed,       delta.Speed },
        };
    }

    public SimulatedField BuildSimField()
    {
        WeatherConditionID weather = _field.Weather != null ? _field.Weather.ID : WeatherConditionID.None;
        TerrainID terrain = _field.Terrain != null ? _field.Terrain.ID : TerrainID.None;
        int weatherDuration = _field.Weather != null ? (int)_field.WeatherDuration : 0;
        int terrainDuration = _field.Terrain != null ? (int)_field.TerrainDuration : 0;

        Dictionary<CourtConditionID, int> topCourtConditions = new();
        Dictionary<CourtConditionID, int> bottomCourtConditions = new();

        foreach( var kvp in _field.ActiveCourts[CourtLocation.TopCourt].Conditions )
            topCourtConditions.Add( kvp.Key, kvp.Value.TimeLeft );

        foreach( var kvp in _field.ActiveCourts[CourtLocation.BottomCourt].Conditions )
            bottomCourtConditions.Add( kvp.Key, kvp.Value.TimeLeft );

        SimulatedField field = new()
        {
            Weather = weather,
            Terrain = terrain,
            WeatherDuration = weatherDuration,
            TerrainDuration = terrainDuration,
            TopCourtConditions = topCourtConditions,
            BottomCourtConditions = bottomCourtConditions,
            TrickRoomActive = _ai.BattleSystem.BattleFlags[BattleFlag.TrickRoom],
            TrickRoomDuration = 4, //--We have to move TR out of CourtConditionDB ASAP!
        };

        // LogSimField( field );

        return field;
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

    public bool PokemonBenefitsFromSevereStatus( Pokemon pokemon )
    {
        var ability = pokemon.AbilityID;
        if( ability == AbilityID.Guts || ability == AbilityID.MarvelScale || ability == AbilityID.QuickFeet )
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

    public bool PokemonHasAbility_SpeedManipulation( Pokemon pokemon )
    {
        var ability = pokemon.AbilityID;

        if( ability == AbilityID.SwiftSwim || ability == AbilityID.Chlorophyll || ability == AbilityID.SandRush || ability == AbilityID.SlushRush || ability == AbilityID.QuickFeet || ability == AbilityID.Steadfast )
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

    public bool PokemonHasTerrainSetter_Ability( Pokemon pokemon )
    {
        if( pokemon.AbilityID == AbilityID.GrassySurge || pokemon.AbilityID == AbilityID.PsychicSurge || pokemon.AbilityID == AbilityID.DesecratedGround )
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

    public bool MoveIsPhaze( Move move )
    {
        var effects = move.MoveSO.MoveEffects;

        if( effects.SwitchType == SwitchEffectType.ForceOpponentOut )
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
            if( move.MoveSO.MoveEffects.SwitchType == SwitchEffectType.ForceOpponentOut )
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

    public float PredictSwitchProbability( PotentialToKO offensePTKO, PotentialToKO defensePTKO, bool weAreFaster, float attacker, float opponentHPR, float opponentExpendability, bool log = false, string targetName = "no name" )
    {
        int theirRemaining = _ai.GetRemainingOpposingPokemon( _ai.Unit.Pokemon ).Count;
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

    public float Get_MoveModifier( IBattleAIUnit attacker, IBattleAIUnit target, Move move )
    {
        float modifier = 1f;
        var field = _ai.BattleSystem.Field;

        float stab      = CheckTypes( move.MoveType, attacker ) ? 1.5f : 1f;
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

        if( attacker.Item != BattleItemEffectID.None )
        {
            if( _ai.UnitSim.ItemDMGModifiers.TryGetValue( attacker.Item, out var mod ) )
                item = mod( attacker, target, move );
        }

        modifier = stab * weather * terrain * item;

        return modifier;
    }

    public float Get_MoveEffectiveness( IBattleAIUnit target, Move move )
    {
        float effectiveness = 1f;

        //--Base Effectiveness
        effectiveness = TypeChart.GetTotalEffectiveness( target.Type, move );

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

            if( pokemon.CheckTypes( PokemonType.Water ) )
                score -= 5;

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

            if( pokemon.CheckTypes( PokemonType.Fire ) )
                score -= 5;

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
                score += 5;

            if( pokemon.AbilityID == AbilityID.SandRush || pokemon.AbilityID == AbilityID.SandForce /*|| sand ability*/ )
                score += 10;

            return score;
        }

        if( weather == WeatherConditionID.SNOW )
        {
            if( pokemon.CheckTypes( PokemonType.Ice ) )
                score += 5;

            if( pokemon.CheckTypes( PokemonType.Fighting ) || pokemon.CheckHasAttackingMoveOfType( PokemonType.Fighting ) )
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
                    if( target.Item != BattleItemEffectID.None )
                        return Mathf.FloorToInt( move.MovePower * 1.5f );
                    else
                        return move.MovePower;
                }
            }
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
    public int WeatherDuration;
    public TerrainID Terrain;
    public int TerrainDuration;
    public Dictionary<CourtConditionID, int> TopCourtConditions;
    public Dictionary<CourtConditionID, int> BottomCourtConditions;
    public Dictionary<FieldConditionID, int> FieldConditions;
    public bool TrickRoomActive;
    public int TrickRoomDuration;
}
