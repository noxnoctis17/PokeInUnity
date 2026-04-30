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

    public void LogSimUnit( SimulatedUnit unit )
    {
        TurnSimLog.Add( $"===[Simulated Unit: (Lv.{unit.Level}) {unit.Name}]===" );
        TurnSimLog.Add( $"Current HPR: {unit.CurrentHPR}" );
        TurnSimLog.Add( $"Types: {unit.Type.One} / {unit.Type.Two}" );
        TurnSimLog.Add( $"" );
        TurnSimLog.Add( $"MaxHP: {unit.HPBaseStat}" );
        TurnSimLog.Add( $"Attack: {unit.Attack}" );
        TurnSimLog.Add( $"Defense: {unit.Defense}" );
        TurnSimLog.Add( $"SpAttack: {unit.SpAttack}" );
        TurnSimLog.Add( $"SpDefense: {unit.SpDefense}" );
        TurnSimLog.Add( $"Speed: {unit.Speed}" );
        TurnSimLog.Add( $"" );
        TurnSimLog.Add( $"Move: {unit.MTR.Move.MoveSO.Name}" );
        TurnSimLog.Add( $"Ungrounded: {unit.IsUngrounded}" );
        TurnSimLog.Add( $"Ability: {unit.Ability}" );
        TurnSimLog.Add( $"Item: {unit.Item}" );
        TurnSimLog.Add( $"" );
        TurnSimLog.Add( $"Severe Status: {unit.SevereStatus}" );
        TurnSimLog.Add( $"Toxic Counter: {unit.SevereStatusTime}" );
        TurnSimLog.Add( $"Volatile Status Count: {unit.VolatileStatuses.Count}" );
        TurnSimLog.Add( $"Binding Condition Count: {unit.Bindings.Count}" );
        TurnSimLog.Add( $"" );
    }

    private void LogSimField( SimulatedField field )
    {
        TurnSimLog.Add( $"===[Simulated Field]===" );
        TurnSimLog.Add( $"Weather: {field.Weather}" );
        TurnSimLog.Add( $"Terrain: {field.Terrain}" );
        TurnSimLog.Add( $"Top Court Condition Count: {field.TopCourtConditions.Count}" );
        TurnSimLog.Add( $"Bottom Court Condition Count: {field.BottomCourtConditions.Count}" );
        TurnSimLog.Add( $"" );
    }

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
        BattleAI_PokemonAdapter mon = new( pokemon, _ai );
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

        return BuildSimUnit_WithStatStageList( unit, hpr, mtr, field, statStages );
    }

    //--Create a Sim Unit with stat stage changes.
    private SimulatedUnit BuildSimUnit_WithStatStageList( IBattleAIUnit pokemon, float hpr, MoveThreatResult mtr, SimulatedField field, List<StatStage> statStages )
    {
        var unit = BuildSimUnit( pokemon, hpr, mtr, field );
        unit.StatStages = new();

        for( int i = 0; i < statStages.Count; i++ )
        {
            var stages = statStages[i];
            unit.StatStages.Add( stages.Stat, stages.Change );
        }

        return unit;
    }

    //--Create Simple Sim Unit from IBattleAIUnit
    public SimulatedUnit BuildSimUnit( IBattleAIUnit pokemon, float hpr, MoveThreatResult mtr, SimulatedField field )
    {
        BattleItemEffectID item = pokemon.Item;
        SevereConditionID severe =  pokemon.SevereStatus;
        int toxic = pokemon.SevereStatusTime;

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
            HPBaseStat = _ai.GetBaseStat( pokemon, Stat.HP ),
            Attack = _ai.GetBaseStat( pokemon, Stat.Attack ),
            Defense = _ai.GetBaseStat( pokemon, Stat.Defense ),
            SpAttack = _ai.GetBaseStat( pokemon, Stat.SpAttack ),
            SpDefense = _ai.GetBaseStat( pokemon, Stat.SpDefense ),
            Speed = _ai.GetUnitContextualSpeed( pokemon ),

            ActiveMoves = activeMoves,
            MTR = mtr,

            IsUngrounded = IsUngrounded( pokemon, field ),

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
        var unit = _ai.GetBattleUnit( pokemon.PID );

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

    public bool PokemonHasWeatherAbility( Pokemon pokemon )
    {
        if( pokemon.AbilityID == AbilityID.Drought || pokemon.AbilityID == AbilityID.Drizzle || pokemon.AbilityID == AbilityID.Sandstream || pokemon.AbilityID == AbilityID.SnowWarning )
            return true;
        else
            return false;
    }

    public bool PokemonHasWeatherMove( Pokemon pokemon )
    {
        if( pokemon.CheckHasActiveMove( "Sunny Day" ) || pokemon.CheckHasActiveMove( "Rain Dance" ) || pokemon.CheckHasActiveMove( "Sandstorm" ) || pokemon.CheckHasActiveMove( "Snowscape" ) )
            return true;
        else
            return false;
    }

    public bool PokemonHasTerrainAbility( Pokemon pokemon )
    {
        if( pokemon.AbilityID == AbilityID.GrassySurge || pokemon.AbilityID == AbilityID.PsychicSurge || pokemon.AbilityID == AbilityID.DesecratedGround )
            return true;
        else
            return false;
    }

    public bool PokemonHasTerrainMove( Pokemon pokemon )
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
            if( PokemonHasWeatherAbility( mon ) )
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
            if( PokemonHasWeatherMove( mon ) )
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
            if( PokemonHasTerrainAbility( mon ) )
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
            if( PokemonHasTerrainMove( mon ) )
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
            if( mon.CheckHasActiveMove( "Stealth Rock" ) || mon.CheckHasActiveMove( "Sticky Web" ) || mon.CheckHasActiveMove( "Leech Seed" ) || mon.CheckHasActiveMove( "Spikes" ) || mon.CheckHasActiveMove( "Toxic Spikes" ) )
                return true;
            else
                continue;
        }

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

    public List<Move> GetSetupMoves( List<Move> moves )
    {
        List<Move> setupMoves = new();

        foreach( var move in moves )
        {
            bool isSetupMove = move.MoveSO.MoveEffects.StatChangeList?.Count > 0 && ( move.MoveSO.MoveEffects.Target == EffectTarget.Self || move.MoveSO.MoveEffects.Target == EffectTarget.AllySide );

            if( move.MoveSO.MoveCategory != MoveCategory.Status )
                continue;
            else
            {
                if( isSetupMove )
                    setupMoves.Add( move );
                else
                    continue;
            }
        }

        return setupMoves;
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

    public bool PredictForcedSwitch( PotentialToKO offensePTKO, PotentialToKO defensePTKO, bool weAreFaster )
    {
        bool weThreatenKO = offensePTKO >= PotentialToKO.Risky;
        bool theyThreatenKO = defensePTKO >= PotentialToKO.Risky;

        if( weThreatenKO && !theyThreatenKO )
            return true;

        if( weThreatenKO && weAreFaster && defensePTKO < PotentialToKO.Risky )
            return true;

        return false;
    }

    // public float PredictSwitchProbability( PotentialToKO offensePTKO, PotentialToKO defensePTKO, bool weAreFaster, float attackerHPR, float opponentHPR, CustomLogSession log = null )
    // {
    //     float prob = 0f;

    //     bool weThreatenOHKO = offensePTKO >= PotentialToKO.Dangerous;
    //     bool theyThreatenOHKO = defensePTKO >= PotentialToKO.Dangerous;
    //     bool theyThreaten2HKO = defensePTKO >= PotentialToKO.Risky;

    //     int theirRemaining = _ai.GetRemainingOpposingPokemon( _ai.Unit.Pokemon ).Count;

    //     //--Strong signals
    //     if( weThreatenOHKO && !theyThreatenOHKO )
    //         prob += 0.65f;
    //     else if( weThreatenOHKO && weAreFaster && defensePTKO < PotentialToKO.Risky )
    //         prob += 0.65f;
        
    //     if ( weAreFaster && !theyThreatenOHKO )
    //         prob += 0.2f;

    //     //--Moderate signals
    //     if( opponentHPR <= 0.2f )
    //         prob += 0.35f;
    //     else if( opponentHPR <= 0.3f )
    //         prob += 0.25f;
    //     else if( opponentHPR <= 0.5f )
    //         prob += 0.15f;
    //     else if( opponentHPR <= 0.7f && weThreatenOHKO )
    //         prob += 0.2f;

    //     //---Negative signals (VERY IMPORTANT)
    //     if( theyThreatenOHKO )
    //         prob -= 0.4f;
    //     else if( theyThreaten2HKO )
    //         prob -= 0.2f;

    //     //--If they are faster and threaten, even less likely to switch
    //     if( !weAreFaster && theyThreaten2HKO )
    //         prob -= 0.15f;

    //     //--Endgame: less switching
    //     if( theirRemaining == 1 )
    //         prob = 0f;
    //     else if( theirRemaining == 2 )
    //         prob *= 0.6f;

    //     // Clamp
    //     prob = Mathf.Clamp01( prob );

    //     return prob;
    // }

    public float PredictSwitchProbability( PotentialToKO offensePTKO, PotentialToKO defensePTKO, bool weAreFaster, float attackerHPR, float opponentHPR, CustomLogSession log = null )
    {
        float prob = 0f;
        float positive = 0;
        float negative = 0;

        bool weThreatenOHKO = offensePTKO >= PotentialToKO.Dangerous;
        bool theyThreatenOHKO = defensePTKO >= PotentialToKO.Dangerous;
        bool theyThreaten2HKO = defensePTKO >= PotentialToKO.Risky;

        int theirRemaining = _ai.GetRemainingOpposingPokemon( _ai.Unit.Pokemon ).Count;
        if( theirRemaining == 1 )
            return 0f;

        // _ai.CurrentLog.Add( $"" );
        // _ai.CurrentLog.Add( $"===[Predicting Opponent Switch Probability...]===" );

        //--Positive Signals
        if( weThreatenOHKO && !theyThreatenOHKO )
        {
            positive += 2;
            // _ai.CurrentLog.Add( $"We threaten a KO and they don't! Positive: {positive}, Negative: {negative}" );
        }
        
        if( weThreatenOHKO && weAreFaster )
        {
            positive++;
            // _ai.CurrentLog.Add( $"We threaten a KO and we're faster! Positive: {positive}, Negative: {negative}" );
        }
        
        if ( weAreFaster && !theyThreatenOHKO )
        {
            positive++;
            // _ai.CurrentLog.Add( $"We're faster and they don't threaten a KO! Positive: {positive}, Negative: {negative}" );
        }

        if( opponentHPR <= 0.3f )
        {
            positive += 2;
            // _ai.CurrentLog.Add( $"Opponent current HP <= 30%! Positive: {positive}, Negative: {negative}" );
        }
        else if( opponentHPR <= 0.5f )
        {
            positive += 1;
            // _ai.CurrentLog.Add( $"Opponent current HP <= 50%! Positive: {positive}, Negative: {negative}" );
        }

        //--Negative Signals
        if( theyThreatenOHKO && !weThreatenOHKO )
        {
            negative += 2;
            // _ai.CurrentLog.Add( $"They threaten a KO and we don't! Positive: {positive}, Negative: {negative}" );
        }

        if( theyThreaten2HKO && !weAreFaster )
        {
            negative++;
            // _ai.CurrentLog.Add( $"They threaten a 2HKO and we are slower! Positive: {positive}, Negative: {negative}" );
        }

        if( theirRemaining <= 2 )
        {
            negative++;
            // _ai.CurrentLog.Add( $"They have 2 or less Pokemon! Positive: {positive}, Negative: {negative}" );
        }

        if( opponentHPR <= 0.2f )
        {
            negative++;
            // _ai.CurrentLog.Add( $"They're likely going to faint anyway from being <= 20% HP! Positive: {positive}, Negative: {negative}" );
        }

        if( defensePTKO == PotentialToKO.OHKO && !weAreFaster )
        {
            negative++;
            // _ai.CurrentLog.Add( $"They almost certainly KO us and we're slower! {positive}, Negative: {negative}" );
        }

        //--Calculate
        float total = positive - negative;
        // _ai.CurrentLog.Add( $"Total probability: {total} ({positive} + Negative: {negative})" );

        if( total == 0 )
            return 0.5f;

        // prob = positive / total;
        prob = 1f / ( 1f + Mathf.Exp( -total ) );

        // _ai.CurrentLog.Add( $"Final Probability: {prob} (Positive: {positive} / Total: {total})" );
        // _ai.CurrentLog.Add( $"===" );
        // _ai.CurrentLog.Add( $"" );

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

    public int Get_TerrainContextScore( Pokemon pokemon )
    {
        int score = 0;
        var terrain = _ai.BattleSystem.Field.Terrain;

        if( terrain == null || terrain?.ID == TerrainID.None )
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
    public int HPBaseStat { get; set; }
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
    public bool TrickRoomActive;
    public int TrickRoomDuration;
}
