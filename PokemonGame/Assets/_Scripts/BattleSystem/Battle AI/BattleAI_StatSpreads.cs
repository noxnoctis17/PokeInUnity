using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class BattleAI_StatSpreads
{
    private readonly BattleAI _ai;
    private Dictionary<StatSpreadType, StatSpread> _statSpreads;
    private Dictionary<NatureID, Nature> _natures;

    public BattleAI_StatSpreads( BattleAI ai )
    {
        _ai = ai;
        InitStatSpreads();
        InitNatureDictionary();
    }

    public StatSpread AssignStatSpread( BattleAI_PokemonAdapter adapter )
    {
        StatSpreadType spreadType = default;
        StatSpread spread = new() { Spread = new() };
        NatureID nature = NatureID.Neutral;
        // CustomLogSession log = new();

        // log.Add( $"====================================================" );
        // log.Add( $"=====[Assigning Stat Spread for {adapter.Name}]=====" );
        // log.Add( $"====================================================" );
        // log.Add( $"" );

        bool ourMon = _ai.Blackboard.OurTeamPokemon.Contains( adapter.Pokemon );

        if( ourMon )
        {
            // log.Add( $"{adapter.Name} belongs to our team! Reading true EV and Nature values..." );
            // log.Add( $"" );

            spread.Spread = adapter.Pokemon.EffortValues;
            spread.Nature = adapter.Pokemon.CurrentNature;

            // log.Add( $"===[Effort Values]===" );
            // log.Add( $"HP: {spread.Spread[Stat.HP]}" );
            // log.Add( $"Attack: {spread.Spread[Stat.Attack]}" );
            // log.Add( $"Defense: {spread.Spread[Stat.Defense]}" );
            // log.Add( $"SpAttack: {spread.Spread[Stat.SpAttack]}" );
            // log.Add( $"SpDefense: {spread.Spread[Stat.SpDefense]}" );
            // log.Add( $"Speed: {spread.Spread[Stat.Speed]}" );
            // log.Add( $"" );
            // log.Add( $"Nature: {spread.Nature}" );
            // log.Add( $"=====================" );
            // log.Add( $"" );
        }
        else
        {
            // log.Add( $"{adapter.Name} belongs to the opposing team! Estimating EV and Nature values based on Role Profile (Parimary Role: {adapter.RoleProfile.PrimaryRole})..." );
            // log.Add( $"" );

            var battleType = _ai.BattleSystem.BattleType;
            if( battleType == BattleType.TrainerDoubles || battleType == BattleType.AI_Doubles )
                spreadType = GetStatSpreadType_Doubles( adapter );
            else
                spreadType = GetStatSpreadType_Singles( adapter );

            spread = _statSpreads[spreadType];
            nature = GetNatureID( adapter, spreadType );
            spread.Nature = nature;

            // log.Add( $"===[Effort Values (Battle Mode: {battleType}, Spread Type: {spreadType})]===" );
            // log.Add( $"HP: {spread.Spread[Stat.HP]}" );
            // log.Add( $"Attack: {spread.Spread[Stat.Attack]}" );
            // log.Add( $"Defense: {spread.Spread[Stat.Defense]}" );
            // log.Add( $"SpAttack: {spread.Spread[Stat.SpAttack]}" );
            // log.Add( $"SpDefense: {spread.Spread[Stat.SpDefense]}" );
            // log.Add( $"Speed: {spread.Spread[Stat.Speed]}" );
            // log.Add( $"" );
            // log.Add( $"Nature: {spread.Nature}" );
            // log.Add( $"=====================" );
            // log.Add( $"" );
        }

        // // Debug.Log( log.ToString() );
        // log.Clear();

        return spread;
    }

    private StatSpreadType GetStatSpreadType_Singles( BattleAI_PokemonAdapter adapter )
    {
        var rp = adapter.RoleProfile;

        switch( rp.PrimaryRole )
        {
            case RoleClass.Sweeper:
            case RoleClass.SetupSweeper:
            case RoleClass.RevengeKiller:
            case RoleClass.WallBreaker:
                if( rp.Biases.Contains( RoleBias.Physical ) )
                    if( rp.Biases.Contains( RoleBias.Mixed ) )
                        return StatSpreadType.FastMixedSpecial_PhysicalAttacker;
                    else
                        return StatSpreadType.FastPhysicalAttacker;

                else if( rp.Biases.Contains( RoleBias.Special ) )
                    if( rp.Biases.Contains( RoleBias.Mixed ) )
                        return StatSpreadType.FastMixedPhysical_SpecialAttacker;
                    else
                        return StatSpreadType.FastSpecialAttacker;
            break;

            case RoleClass.Wall:
            case RoleClass.DefensiveSetup:
                if( rp.Biases.Contains( RoleBias.PhysicallyBulky ) )
                    return StatSpreadType.PhysicallyDefensive;
                else if( rp.Biases.Contains( RoleBias.SpeciallyBulky ) )
                    return StatSpreadType.SpeciallyDefensive;
            break;

            case RoleClass.BulkyAttacker:
                if( rp.Biases.Contains( RoleBias.PhysicallyBulky ) && rp.Biases.Contains( RoleBias.Physical ) )
                        return StatSpreadType.BulkyPhysical_PhysicalAttacker;
                else if( rp.Biases.Contains( RoleBias.PhysicallyBulky ) && rp.Biases.Contains( RoleBias.Special ) )
                        return StatSpreadType.BulkyPhysical_SpecialAttacker;
                else if( rp.Biases.Contains( RoleBias.SpeciallyBulky ) && rp.Biases.Contains( RoleBias.Physical ) )
                        return StatSpreadType.BulkySpecial_PhysicalAttacker;
                else if( rp.Biases.Contains( RoleBias.SpeciallyBulky ) && rp.Biases.Contains( RoleBias.Special ) )
                        return StatSpreadType.BulkySpecial_SpecialAttacker;
                else if( rp.Biases.Contains( RoleBias.Physical ) )
                    return StatSpreadType.BulkyPhysical_PhysicalAttacker;
                else if( rp.Biases.Contains( RoleBias.Special ) )
                    return StatSpreadType.BulkyPhysical_SpecialAttacker;
            break;

            case RoleClass.UtilitySupport:
            case RoleClass.Disrupter:
            case RoleClass.Pivot:
            break;

            case RoleClass.TrickRoomAbuser:
            break;

            case RoleClass.AllRounder:
                if( rp.Biases.Contains( RoleBias.Physical ) )
                    return StatSpreadType.AllRounder_Atk;
                else if( rp.Biases.Contains( RoleBias.Special ) )
                    return StatSpreadType.AllRounder_SpAtk;
            break;
        };

        return default;
    }

    private StatSpreadType GetStatSpreadType_Doubles( BattleAI_PokemonAdapter adapter )
    {
        return GetStatSpreadType_Singles( adapter );
    }

    private NatureID GetNatureID( BattleAI_PokemonAdapter adapter, StatSpreadType spreadType )
    {
        var archetype = GetNatureArchetype( adapter, spreadType );
        var boostedStat = DetermineBoostedStat( adapter, spreadType, archetype );
        var dumpStat = DetermineDumpStat( adapter, spreadType, archetype, boostedStat );
        var natureID = FindNatureID( boostedStat, dumpStat );

        return natureID;
    }

    private NatureArchetype GetNatureArchetype( BattleAI_PokemonAdapter adapter, StatSpreadType spreadType )
    {
        //--Examine EV Spread, Base Speed Stat, and Item to determine the nature archetype.
        Dictionary<NatureArchetype, int> scores = new()
        {
            { NatureArchetype.Neutral, 0 },
            { NatureArchetype.OffensiveFast, 0 },
            { NatureArchetype.OffensiveSlow, 0 },
            { NatureArchetype.BulkyPhysical, 0 },
            { NatureArchetype.BulkySpecial, 0 },
            { NatureArchetype.MixedFast, 0 },
            { NatureArchetype.MixedSlow, 0 },
        };

        //--Base Speed
        int baseSpeed = adapter.Pokemon.PokeSO.Speed;

        //--Role Signals
        var primaryRole = adapter.RoleProfile.PrimaryRole;

        //--Item Signals
        bool speedItem = adapter.Item == ItemBattleEffectID.ChoiceScarf;
        bool recoveryItem = adapter.Item == ItemBattleEffectID.SitrusBerry || adapter.Item == ItemBattleEffectID.Leftovers;

        bool hyperOffensiveItem =
        adapter.Item == ItemBattleEffectID.LifeOrb || adapter.Item == ItemBattleEffectID.ExpertBelt ||
        adapter.Item == ItemBattleEffectID.ChoiceBand || adapter.Item == ItemBattleEffectID.ChoiceSpecs ||
        ( adapter.Ability == AbilityID.Guts && ( adapter.Item == ItemBattleEffectID.FlameOrb || adapter.Item == ItemBattleEffectID.ToxicOrb || adapter.Item == ItemBattleEffectID.StaticOrb ) );

        //--Gather Information from EV Spread
        var statSpread = _statSpreads[spreadType];
        int hp = statSpread.Spread[Stat.HP];
        int atk = statSpread.Spread[Stat.Attack];
        int def = statSpread.Spread[Stat.Defense];
        int spatk = statSpread.Spread[Stat.SpAttack];
        int spdef = statSpread.Spread[Stat.SpDefense];
        int spe = statSpread.Spread[Stat.Speed];

        //--HP Spread Signals
        bool investedHP = hp >= 200;
        bool maxHP = hp == 252;

        //--Speed Spread Signal
        bool maxSpe = spe >= 252;
        SpeedCommitment speedCommit;

        if( spe >= 200 )
        {
            speedCommit = SpeedCommitment.Comitted;
        }
        else if( spe >= 40 && spe < 200 )
        {
            speedCommit = SpeedCommitment.Creeping;
        }
        else
        {
            speedCommit = SpeedCommitment.Dumped;
        }

        //--Offensive Spread Signals
        bool investedAtk = atk >= 200;
        bool investedSpAtk = spatk >= 200;
        bool maxAtk = atk >= 252;
        bool maxSpAtk = spatk >= 252;
        
        bool physical = atk > spatk + 40;
        bool special = spatk > atk + 40;
        bool mixed = !physical && !special;

        bool fastAtk = ( atk + spe >= 350 ) || ( investedAtk && speedCommit == SpeedCommitment.Comitted ) || ( maxAtk && maxSpe ); 
        bool fastSpAtk = ( spatk + spe >= 350 ) || ( investedSpAtk && speedCommit == SpeedCommitment.Comitted ) || ( maxSpAtk && maxSpe ); 

        //--Defensive Spread Signals
        bool investedDef = def >= 200;
        bool investedSpDef = spdef >= 200;
        bool maxDef = def >= 252;
        bool maxSpDef = spdef >= 252;

        bool bulkyPhysical = ( hp + def >= 350 ) || ( investedHP && investedDef ) || ( maxHP && maxDef );
        bool bulkySpecial = ( hp + spdef >= 350 ) || ( investedHP && investedSpDef ) || ( maxHP && maxSpDef );

        //--Begin Scoring
        //--Offensive Fast
        if( speedCommit == SpeedCommitment.Comitted )
            scores[NatureArchetype.OffensiveFast] += 2;

        if( baseSpeed >= 100 )
            scores[NatureArchetype.OffensiveFast] += 1;

        if( adapter.Item == ItemBattleEffectID.ChoiceScarf )
            scores[NatureArchetype.OffensiveFast] += 1;
        
        if( hyperOffensiveItem && speedCommit == SpeedCommitment.Comitted )
            scores[NatureArchetype.OffensiveFast] += 1;

        if( primaryRole == RoleClass.RevengeKiller || primaryRole == RoleClass.Sweeper || adapter.RoleProfile.Traits.Contains( RoleTrait.WideMoveCoverage ) )
            scores[NatureArchetype.OffensiveFast] += 1;

        //--Offensive Slow
        if( spe <= 4 )
            scores[NatureArchetype.OffensiveSlow] += 2;
        else if( speedCommit == SpeedCommitment.Dumped )
            scores[NatureArchetype.OffensiveSlow] += 1;

        if( primaryRole == RoleClass.TrickRoomAbuser || adapter.RoleProfile.SecondaryRoles.Contains( RoleClass.TrickRoomAbuser ) )
            scores[NatureArchetype.OffensiveSlow] += 3;

        if( ( adapter.RoleProfile.Signals.BurstDamage >= 40 || adapter.RoleProfile.Traits.Contains( RoleTrait.WideMoveCoverage ) ) && speedCommit != SpeedCommitment.Comitted )
            scores[NatureArchetype.OffensiveSlow] += 1;

        if( adapter.RoleProfile.Signals.SetupPressure >= 20 && speedCommit != SpeedCommitment.Comitted )
            scores[NatureArchetype.OffensiveSlow] += 1;

        //--Bulky Physical
        if( investedDef )
        {
            scores[NatureArchetype.BulkyPhysical] += 2;

            if( investedHP )
                scores[NatureArchetype.BulkyPhysical] += 2;

            if( recoveryItem )
                scores[NatureArchetype.BulkyPhysical] += 1;
        }
        else if( investedSpDef )
        {
            scores[NatureArchetype.BulkySpecial] += 2;

            if( investedHP )
                scores[NatureArchetype.BulkySpecial] += 2;

            if( recoveryItem )
                scores[NatureArchetype.BulkySpecial] += 1;
        }
        else
        {
            if( investedHP )
            {
                scores[NatureArchetype.BulkyPhysical] += 1;
                scores[NatureArchetype.BulkySpecial] += 1;
            }

            if( primaryRole == RoleClass.Wall )
            {
                if( adapter.RoleProfile.Biases.Contains( RoleBias.PhysicallyBulky ) )
                    scores[NatureArchetype.BulkyPhysical] += 1;
                else if( adapter.RoleProfile.Biases.Contains( RoleBias.SpeciallyBulky ) )
                    scores[NatureArchetype.BulkySpecial] += 1;

                if( investedAtk || investedSpAtk )
                    scores[NatureArchetype.OffensiveSlow] += 1;
            }
        }

        if( adapter.Item == ItemBattleEffectID.RockyHelmet )
            scores[NatureArchetype.BulkyPhysical] += 1;

        if( adapter.Item == ItemBattleEffectID.AssaultVest )
            scores[NatureArchetype.BulkySpecial] += 1;

        //--Mixed Fast
        if( speedCommit != SpeedCommitment.Dumped && ( ( investedAtk && investedSpAtk ) || mixed ) )
        {
            scores[NatureArchetype.MixedFast] += 2;

            if( adapter.RoleProfile.Traits.Contains( RoleTrait.WideMoveCoverage ) )
                scores[NatureArchetype.MixedFast] += 1;

            if( hyperOffensiveItem )
                scores[NatureArchetype.MixedFast] += 1;

            if( baseSpeed >= 100 )
                scores[NatureArchetype.MixedFast] += 1;

            if( primaryRole == RoleClass.RevengeKiller || primaryRole == RoleClass.Sweeper )
            scores[NatureArchetype.MixedFast] += 2;
        }

        //--Mixed Slow
        if( speedCommit == SpeedCommitment.Dumped && ( ( investedAtk && investedSpAtk ) || mixed ) )
        {
            scores[NatureArchetype.MixedSlow] += 2;

            if( adapter.RoleProfile.Traits.Contains( RoleTrait.WideMoveCoverage ) )
                scores[NatureArchetype.MixedSlow] += 1;

            if( hyperOffensiveItem )
                scores[NatureArchetype.MixedSlow] += 1;

            if( baseSpeed >= 100 )
                scores[NatureArchetype.MixedSlow] += 1;

            if( primaryRole == RoleClass.TrickRoomAbuser || adapter.RoleProfile.SecondaryRoles.Contains( RoleClass.TrickRoomAbuser ) )
                scores[NatureArchetype.MixedSlow] += 2;
        }

        var scoresSorted = scores.OrderByDescending( s => s.Value );
        if( scoresSorted.First().Value < 3 )
            return NatureArchetype.Neutral;
        else
            return scoresSorted.First().Key;
    }

    private Stat DetermineBoostedStat( BattleAI_PokemonAdapter adapter, StatSpreadType spreadType, NatureArchetype archetype )
    {
        //--Based on archetype, spread, and item, determine the boosted stat.
        //--attackers will typically lean toward boosting their attack or speed regardless of whether they are naturally fast or slow, and we should always weight toward speed.
        //--unless of course there is no speed investment. 0 speed evs, or < 200 speed evs almost certainly means no speed nature unless something ultra specific is going on
        //--in which case the ai won't know that anyway, and will have to learn it instead over the course of the battle.

        //--Determination will be check based. each passed check in favor of a direction, based on archetype, will reward a singular point in favor of that direction.
        //--at the end, another reasoning check and some gates will be the final resolution. archetype's goal should be to limit the range we need to make comparisons on
        //--as there are a lot of different combinations. OffensiveFast likely doesn't care about defensive stat boosts, and likely doesn't want to dump a defensive stat, for example.
        
        Dictionary<Stat, int> scores = new()
        {   //--HP natures do not exist
            { Stat.Attack, 0 },
            { Stat.Defense, 0 },
            { Stat.SpAttack, 0 },
            { Stat.SpDefense, 0 },
            { Stat.Speed, 0 },
        };

        //--Base Stats
        int baseAttack = adapter.Pokemon.PokeSO.Attack;
        int baseSpAttack = adapter.Pokemon.PokeSO.SpAttack;
        int baseSpeed = adapter.Pokemon.PokeSO.Speed;

        //--Role Signals
        var primaryRole = adapter.RoleProfile.PrimaryRole;

        //--Item Signals
        bool speedItem = adapter.Item == ItemBattleEffectID.ChoiceScarf;
        bool recoveryItem = adapter.Item == ItemBattleEffectID.SitrusBerry || adapter.Item == ItemBattleEffectID.Leftovers;

        bool hyperOffensiveItem =
        adapter.Item == ItemBattleEffectID.LifeOrb || adapter.Item == ItemBattleEffectID.ExpertBelt ||
        adapter.Item == ItemBattleEffectID.ChoiceBand || adapter.Item == ItemBattleEffectID.ChoiceSpecs ||
        ( adapter.Ability == AbilityID.Guts && ( adapter.Item == ItemBattleEffectID.FlameOrb || adapter.Item == ItemBattleEffectID.ToxicOrb || adapter.Item == ItemBattleEffectID.StaticOrb ) );

        //--Gather Information from EV Spread
        var statSpread = _statSpreads[spreadType];
        int hp = statSpread.Spread[Stat.HP];
        int atk = statSpread.Spread[Stat.Attack];
        int def = statSpread.Spread[Stat.Defense];
        int spatk = statSpread.Spread[Stat.SpAttack];
        int spdef = statSpread.Spread[Stat.SpDefense];
        int spe = statSpread.Spread[Stat.Speed];

        //--HP Spread Signals
        bool investedHP = hp >= 200;
        bool maxHP = hp == 252;

        //--Speed Spread Signal
        bool maxSpe = spe >= 252;
        SpeedCommitment speedCommit;

        if( spe >= 200 )
            speedCommit = SpeedCommitment.Comitted;
        else if( spe >= 40 && spe < 200 )
            speedCommit = SpeedCommitment.Creeping;
        else
            speedCommit = SpeedCommitment.Dumped;

        //--Offensive Spread Signals
        bool investedAtk = atk >= 200;
        bool investedSpAtk = spatk >= 200;
        bool maxAtk = atk >= 252;
        bool maxSpAtk = spatk >= 252;
        
        bool physical = atk > spatk + 40;
        bool special = spatk > atk + 40;
        bool mixed = !physical && !special;

        bool fastAtk = ( atk + spe >= 350 ) || ( investedAtk && speedCommit == SpeedCommitment.Comitted ) || ( maxAtk && maxSpe );
        bool fastSpAtk = ( spatk + spe >= 350 ) || ( investedSpAtk && speedCommit == SpeedCommitment.Comitted ) || ( maxSpAtk && maxSpe );

        Stat mainOffensiveStat;
        if( physical )
            mainOffensiveStat = Stat.Attack;
        else if( special )
            mainOffensiveStat = Stat.SpAttack;
        else if( atk > spatk )
            mainOffensiveStat = Stat.Attack;
        else if( spatk > atk )
            mainOffensiveStat = Stat.SpAttack;
        else if( baseAttack > baseSpAttack )
            mainOffensiveStat = Stat.Attack;
        else if( baseSpAttack > baseAttack )
            mainOffensiveStat = Stat.SpAttack;
        else
            mainOffensiveStat = Stat.Attack;

        Stat mixedPrimaryAxis;
        if( adapter.RoleProfile.Signals.SpecialAttackCount >= 3 && adapter.RoleProfile.Signals.PhysicalAttackCount <= 2 )
            mixedPrimaryAxis = Stat.SpAttack;
        else
            mixedPrimaryAxis = Stat.Attack;

        //--Defensive Spread Signals
        bool investedDef = def >= 200;
        bool investedSpDef = spdef >= 200;
        bool maxDef = def >= 252;
        bool maxSpDef = spdef >= 252;

        bool bulkyPhysical = ( hp + def >= 350 ) || ( investedHP && investedDef ) || ( maxHP && maxDef );
        bool bulkySpecial = ( hp + spdef >= 350 ) || ( investedHP && investedSpDef ) || ( maxHP && maxSpDef );

        //--Moveset Setup Signals
        int atkBoostCount = 0;
        int spatkBoostCount = 0;
        int defBoostCount = 0;
        int spdefBoostCount = 0;
        int speBoostCount = 0;
        int priorityCount = 0;

        for( int i = 0; i < adapter.ActiveMoves.Count; i++ )
        {
            var move = adapter.ActiveMoves[i];
            var statChanges = move.MoveSO.MoveEffects.StatChangeList;

            //--Setup/Stat Boosting Attacks (Trailblaze, Scale Shot)
            if( statChanges != null && statChanges.Count > 0 )
            {
                foreach( var sc in statChanges )
                {
                    if( sc.Stat == Stat.Attack )
                    {
                        if( sc.Change > 1 )
                            atkBoostCount += 2;
                        else
                            atkBoostCount++;
                    }

                    if( sc.Stat == Stat.SpAttack )
                    {
                        if( sc.Change > 1 )
                            spatkBoostCount += 2;
                        else
                            spatkBoostCount++;
                    }

                    if( sc.Stat == Stat.Defense )
                    {
                        if( sc.Change > 1 )
                            defBoostCount += 2;
                        else
                            defBoostCount++;
                    }

                    if( sc.Stat == Stat.SpDefense )
                    {
                        if( sc.Change > 1 )
                            spdefBoostCount += 2;
                        else
                            spdefBoostCount++;
                    }

                    if( sc.Stat == Stat.Speed )
                    {
                        if( sc.Change > 1 )
                            speBoostCount += 2;
                        else
                            speBoostCount++;
                    }
                }
            }

            //--Priority
            if( move.MoveSO.MoveCategory != MoveCategory.Status && move.Priority > MovePriority.Zero )
                priorityCount++;
        }

        switch( archetype )
        {
            case NatureArchetype.OffensiveFast:
            //--Offensive Fast is typically going to be deciding between Adamant/Modest and Jolly/Timid. There's a couple of others, but we won't focus on them for now.

                //--Speed Votes
                //--We want to always weight toward speed. Speed is the most important stat in the game,
                //--and a pokemon that wants to be fast offense will often prioritize speed even if they are already fast.
                scores[Stat.Speed] += 1;

                if( speedCommit == SpeedCommitment.Comitted )
                    scores[Stat.Speed] += 2;

                if( baseSpeed >= 90 && baseSpeed <= 110 )
                    scores[Stat.Speed] += 2;

                if( primaryRole == RoleClass.RevengeKiller )
                    scores[Stat.Speed] += 3;

                if( adapter.Item == ItemBattleEffectID.ChoiceScarf )
                    scores[Stat.Speed] -= 2;

                //--Offense Votes

                if( investedAtk )
                    scores[Stat.Attack] += 2;

                if( investedSpAtk )
                    scores[Stat.SpAttack] += 2;

                if( hyperOffensiveItem && adapter.Item != ItemBattleEffectID.ChoiceBand && adapter.Item != ItemBattleEffectID.ChoiceScarf )
                    scores[mainOffensiveStat] += 1;

                if( adapter.RoleProfile.Signals.BurstDamage >= 40 )
                    scores[mainOffensiveStat] += 2;

                if( adapter.Item == ItemBattleEffectID.ChoiceBand || adapter.Item == ItemBattleEffectID.ChoiceScarf )
                    scores[mainOffensiveStat] -= 2;

                //--Speed Tier overall contribution
                if( baseSpeed >= 115 )
                    scores[mainOffensiveStat] += 2;
                else if( baseSpeed >= 85 && baseSpeed <= 105 )
                    scores[Stat.Speed] += 2;

                if( baseSpeed < 80 )
                    scores[mainOffensiveStat] += 3;

                //--Stat Boost Check
                if( atkBoostCount > speBoostCount || spatkBoostCount > speBoostCount )
                {
                    if( hyperOffensiveItem )
                        scores[mainOffensiveStat] -= 1;
                    else
                        scores[mainOffensiveStat] += 2;
                }
                else if( speBoostCount > atkBoostCount || speBoostCount > spatkBoostCount )
                {
                    if( adapter.Item == ItemBattleEffectID.ChoiceScarf )
                        scores[Stat.Speed] -= 1;
                    else
                        scores[Stat.Speed] += 2;
                }

                //--Priority Check
                if( priorityCount > 0 )
                    scores[mainOffensiveStat] += 1;
            break;

            case NatureArchetype.OffensiveSlow:
                //--Slow offensive pokemon typically do not care about speed (ursaluna, hariyama), so we will weight toward offense here.
                scores[mainOffensiveStat] += 2;

                //--Offensive Pressure
                if( hyperOffensiveItem )
                    scores[mainOffensiveStat] += 2;

                if( adapter.RoleProfile.Signals.BurstDamage >= 40 )
                    scores[mainOffensiveStat] += 3;

                //--Trick Room Pressure
                if( adapter.RoleProfile.PrimaryRole == RoleClass.TrickRoomAbuser || adapter.RoleProfile.SecondaryRoles.Contains( RoleClass.TrickRoomAbuser ) )
                    scores[mainOffensiveStat] += 3;

                //--Bulk Pressure
                if( investedHP )
                {
                    scores[Stat.Defense] += 1;
                    scores[Stat.SpDefense] += 1;
                }

                if( recoveryItem )
                {
                    scores[Stat.Defense] += 1;
                    scores[Stat.SpDefense] += 1;
                }

                //--Stat boost check
                if( atkBoostCount > 0 || spatkBoostCount > 0 )
                {
                    if( investedDef )
                        scores[Stat.Defense] += 1;

                    if( investedSpDef )
                        scores[Stat.SpDefense] += 1;
                }

                if( defBoostCount > 0 || spdefBoostCount > 0 )
                {
                    scores[mainOffensiveStat] += 1;
                }

                //--Priority Check
                if( priorityCount > 0 )
                    scores[mainOffensiveStat] += 1;

            break;

            case NatureArchetype.BulkyPhysical:
                //--Weight Toward Defense
                scores[Stat.Defense] += 3;

                //--Defensive Pressure
                if( investedDef )
                    scores[Stat.Defense] += 2;

                if( investedSpDef )
                    scores[Stat.SpDefense] += 1;

                if( investedHP )
                    scores[Stat.Defense] += 1;

                if( adapter.Item == ItemBattleEffectID.RockyHelmet )
                    scores[Stat.Defense] += 2;

                if( recoveryItem )
                    scores[Stat.Defense] += 1;

                //--Offensive Presence
                if( investedAtk )
                {
                    scores[Stat.Attack] += 2;
                    scores[Stat.SpDefense] -= 1;
                }
                else if( investedSpAtk )
                {
                    scores[Stat.SpAttack] += 2;
                    scores[Stat.SpDefense] -= 1;
                }

                if( adapter.RoleProfile.Signals.BurstDamage >= 35 )
                    scores[mainOffensiveStat] += 1;

                //--Assault Vest can be held by physically bulky pokemon to patch their spdef. in some cases, this may be paired with a spdef boosting nature for mixed tanks. 
                //--AV + SpDef investment can actually heavily imply a spdef boosting nature
                if( adapter.Item == ItemBattleEffectID.AssaultVest )
                {
                    scores[Stat.Defense] += 1;
                    scores[Stat.SpDefense] += 3;
                }
            break;

            case NatureArchetype.BulkySpecial:
                //--Weight Toward SpDefense
                scores[Stat.SpDefense] += 3;

                //--SpDefensive Pressure
                if( investedSpDef )
                    scores[Stat.SpDefense] += 2;

                if( investedDef )
                    scores[Stat.Defense] += 1;

                if( investedHP )
                    scores[Stat.SpDefense] += 1;

                if( adapter.Item == ItemBattleEffectID.RockyHelmet )
                    scores[Stat.SpDefense] += 1;

                if( recoveryItem )
                    scores[Stat.SpDefense] += 1;

                //--Offensive Presence
                if( investedAtk )
                {
                    scores[Stat.Attack] += 2;
                    scores[Stat.Defense] -= 1;
                }
                else if( investedSpAtk )
                {
                    scores[Stat.SpAttack] += 2;
                    scores[Stat.Defense] -= 1;
                }

                if( adapter.RoleProfile.Signals.BurstDamage >= 35 )
                    scores[mainOffensiveStat] += 1;

                //--Assault Vest could be used to replace a spdef boosting nature in favor of a def boosting nature. this isn't as weighted as physically defensive pokemon.
                if( adapter.Item == ItemBattleEffectID.AssaultVest )
                {
                    scores[Stat.Defense] += 2;
                    scores[Stat.SpDefense] += 2;
                }
            break;

            case NatureArchetype.MixedFast:
                //--Fast attacker - weight toward speed
                scores[Stat.Speed] += 2;

                //--Speed Commitment
                if( speedCommit == SpeedCommitment.Comitted )
                    scores[Stat.Speed] += 2;

                if( adapter.RoleProfile.Traits.Contains( RoleTrait.WideMoveCoverage ) )
                    scores[Stat.Speed] += 1;

                if( adapter.Item == ItemBattleEffectID.ChoiceScarf )
                {
                    scores[mixedPrimaryAxis] += 3;
                    scores[Stat.Speed] -= 1;
                }

                if( hyperOffensiveItem && adapter.Item != ItemBattleEffectID.ChoiceBand && adapter.Item != ItemBattleEffectID.ChoiceSpecs )
                    scores[Stat.Speed] += 1;

                if( adapter.Item == ItemBattleEffectID.ChoiceBand && investedAtk )
                    scores[Stat.Attack] += 1;

                if( adapter.Item == ItemBattleEffectID.ChoiceSpecs && investedSpAtk )
                    scores[Stat.SpAttack] += 1;
            break;

            case NatureArchetype.MixedSlow:
                //--Weighted toward everything but speed
                scores[Stat.Speed] -= 2;

                //--Offense weight
                scores[mixedPrimaryAxis] += 2;

                //--Trick Room Weight
                if( adapter.RoleProfile.PrimaryRole == RoleClass.TrickRoomAbuser || adapter.RoleProfile.SecondaryRoles.Contains( RoleClass.TrickRoomAbuser ) )
                    scores[mixedPrimaryAxis] += 2;

                //--Bulk
                if( investedHP )
                {
                    scores[Stat.Defense] += 1;
                    scores[Stat.SpDefense] += 1;

                    if( investedDef )
                        scores[Stat.Defense] += 2;

                    if( investedSpDef )
                        scores[Stat.SpDefense] += 2;
                }

                //--Setup Check
                if( atkBoostCount > spatkBoostCount )
                    scores[Stat.Attack] += 1;

                if( spatkBoostCount > atkBoostCount )
                    scores[Stat.SpAttack] += 1;
            break;
        };

        var first = scores.OrderByDescending( s => s.Value ).First();
        var second = scores.OrderByDescending( s => s.Value ).Skip( 1 ).FirstOrDefault();

        if( second.Value > 0 && first.Value - second.Value <= 3 )
        {
            int rand = UnityEngine.Random.Range( first.Value, second.Value + 1 );
            if( rand <= second.Value )
                return second.Key;
            else
                return first.Key;
        }
        else
            return first.Key;
    }

    private Stat DetermineDumpStat( BattleAI_PokemonAdapter adapter, StatSpreadType spreadType, NatureArchetype archetype, Stat boostedStat )
    {
        //--Based on archetype, spread, item, and the determined boosted stat, determine the dumped stat.

        //--Determination will be check based. each passed check in favor of a direction, based on archetype, will reward a singular point in favor of that direction.
        //--at the end, another reasoning check and some gates will be the final resolution. archetype's goal should be to limit the range we need to make comparisons on
        //--as there are a lot of different combinations. OffensiveFast likely doesn't care about defensive stat boosts, and likely doesn't want to dump a defensive stat, for example.

        Dictionary<Stat, int> scores = new()
        {   //--HP natures do not exist
            { Stat.Attack, 0 },
            { Stat.Defense, 0 },
            { Stat.SpAttack, 0 },
            { Stat.SpDefense, 0 },
            { Stat.Speed, 0 },
        };

        //--Base Stats
        int baseAttack = adapter.Pokemon.PokeSO.Attack;
        int baseDefense = adapter.Pokemon.PokeSO.Defense;
        int baseSpAttack = adapter.Pokemon.PokeSO.SpAttack;
        int baseSpDefense = adapter.Pokemon.PokeSO.SpDefense;
        int baseSpeed = adapter.Pokemon.PokeSO.Speed;

        //--Role Signals
        var primaryRole = adapter.RoleProfile.PrimaryRole;

        //--Item Signals
        bool speedItem = adapter.Item == ItemBattleEffectID.ChoiceScarf;
        bool recoveryItem = adapter.Item == ItemBattleEffectID.SitrusBerry || adapter.Item == ItemBattleEffectID.Leftovers;

        bool hyperOffensiveItem =
        adapter.Item == ItemBattleEffectID.LifeOrb || adapter.Item == ItemBattleEffectID.ExpertBelt ||
        adapter.Item == ItemBattleEffectID.ChoiceBand || adapter.Item == ItemBattleEffectID.ChoiceSpecs ||
        ( adapter.Ability == AbilityID.Guts && ( adapter.Item == ItemBattleEffectID.FlameOrb || adapter.Item == ItemBattleEffectID.ToxicOrb || adapter.Item == ItemBattleEffectID.StaticOrb ) );

        //--Gather Information from EV Spread
        var statSpread = _statSpreads[spreadType];
        int hp = statSpread.Spread[Stat.HP];
        int atk = statSpread.Spread[Stat.Attack];
        int def = statSpread.Spread[Stat.Defense];
        int spatk = statSpread.Spread[Stat.SpAttack];
        int spdef = statSpread.Spread[Stat.SpDefense];
        int spe = statSpread.Spread[Stat.Speed];

        //--HP Spread Signals
        bool investedHP = hp >= 200;
        bool maxHP = hp == 252;

        //--Speed Spread Signal
        bool maxSpe = spe >= 252;
        SpeedCommitment speedCommit;

        if( spe >= 200 )
            speedCommit = SpeedCommitment.Comitted;
        else if( spe >= 40 && spe < 200 )
            speedCommit = SpeedCommitment.Creeping;
        else
            speedCommit = SpeedCommitment.Dumped;

        //--Offensive Spread Signals
        bool investedAtk = atk >= 200;
        bool investedSpAtk = spatk >= 200;
        bool maxAtk = atk >= 252;
        bool maxSpAtk = spatk >= 252;
        
        bool physical = atk > spatk + 40;
        bool special = spatk > atk + 40;
        bool mixed = !physical && !special;

        bool fastAtk = ( atk + spe >= 350 ) || ( investedAtk && speedCommit == SpeedCommitment.Comitted ) || ( maxAtk && maxSpe );
        bool fastSpAtk = ( spatk + spe >= 350 ) || ( investedSpAtk && speedCommit == SpeedCommitment.Comitted ) || ( maxSpAtk && maxSpe );

        Stat mainOffensiveStat;
        if( physical )
            mainOffensiveStat = Stat.Attack;
        else if( special )
            mainOffensiveStat = Stat.SpAttack;
        else if( atk > spatk )
            mainOffensiveStat = Stat.Attack;
        else if( spatk > atk )
            mainOffensiveStat = Stat.SpAttack;
        else if( baseAttack > baseSpAttack )
            mainOffensiveStat = Stat.Attack;
        else if( baseSpAttack > baseAttack )
            mainOffensiveStat = Stat.SpAttack;
        else
            mainOffensiveStat = Stat.Attack;

        Stat mixedPrimaryAxis;
        if( adapter.RoleProfile.Signals.SpecialAttackCount >= 3 && adapter.RoleProfile.Signals.PhysicalAttackCount <= 2 )
            mixedPrimaryAxis = Stat.SpAttack;
        else
            mixedPrimaryAxis = Stat.Attack;

        //--Defensive Spread Signals
        bool investedDef = def >= 200;
        bool investedSpDef = spdef >= 200;
        bool maxDef = def >= 252;
        bool maxSpDef = spdef >= 252;

        bool bulkyPhysical = ( hp + def >= 350 ) || ( investedHP && investedDef ) || ( maxHP && maxDef );
        bool bulkySpecial = ( hp + spdef >= 350 ) || ( investedHP && investedSpDef ) || ( maxHP && maxSpDef );

        //--Moveset Setup Signals
        int atkBoostCount = 0;
        int spatkBoostCount = 0;
        int defBoostCount = 0;
        int spdefBoostCount = 0;
        int speBoostCount = 0;
        int priorityCount = 0;

        for( int i = 0; i < adapter.ActiveMoves.Count; i++ )
        {
            var move = adapter.ActiveMoves[i];
            var statChanges = move.MoveSO.MoveEffects.StatChangeList;

            //--Setup/Stat Boosting Attacks (Trailblaze, Scale Shot)
            if( statChanges != null && statChanges.Count > 0 )
            {
                foreach( var sc in statChanges )
                {
                    if( sc.Stat == Stat.Attack )
                    {
                        if( sc.Change > 1 )
                            atkBoostCount += 2;
                        else
                            atkBoostCount++;
                    }

                    if( sc.Stat == Stat.SpAttack )
                    {
                        if( sc.Change > 1 )
                            spatkBoostCount += 2;
                        else
                            spatkBoostCount++;
                    }

                    if( sc.Stat == Stat.Defense )
                    {
                        if( sc.Change > 1 )
                            defBoostCount += 2;
                        else
                            defBoostCount++;
                    }

                    if( sc.Stat == Stat.SpDefense )
                    {
                        if( sc.Change > 1 )
                            spdefBoostCount += 2;
                        else
                            spdefBoostCount++;
                    }

                    if( sc.Stat == Stat.Speed )
                    {
                        if( sc.Change > 1 )
                            speBoostCount += 2;
                        else
                            speBoostCount++;
                    }
                }
            }

            //--Priority
            if( move.MoveSO.MoveCategory != MoveCategory.Status && move.Priority > MovePriority.Zero )
                priorityCount++;
        }

        switch( archetype )
        {
            case NatureArchetype.OffensiveFast:
                //--We never dump the boosted stat in a clear cut offensive role
                scores[boostedStat] -= 999;

                if( boostedStat == Stat.Attack )
                {
                    scores[Stat.SpAttack] += 3;

                    if( speedCommit == SpeedCommitment.Comitted )
                        scores[Stat.Speed] -= 999;
                }

                if( boostedStat == Stat.SpAttack )
                {
                    scores[Stat.Attack] += 3;

                    if( speedCommit == SpeedCommitment.Comitted )
                        scores[Stat.Speed] -= 999;
                }

                if( boostedStat == Stat.Speed )
                {
                    if( investedAtk )
                        scores[Stat.SpAttack] += 5;

                    if( investedSpAtk )
                        scores[Stat.Attack] += 5;
                }
            break;

            case NatureArchetype.OffensiveSlow:
                //--We never dump the boosted stat in a clear cut offensive role
                scores[boostedStat] -= 999;

                if( boostedStat == Stat.Attack )
                {
                    scores[Stat.SpAttack] += 3;

                    if( speedCommit != SpeedCommitment.Comitted )
                        scores[Stat.Speed] += 2;
                }

                if( boostedStat == Stat.SpAttack )
                {
                    scores[Stat.Attack] += 3;

                    if( speedCommit != SpeedCommitment.Comitted )
                        scores[Stat.Speed] += 2;
                }

                if( boostedStat == Stat.Speed )
                {
                    if( investedAtk )
                        scores[Stat.SpAttack] += 5;

                    if( investedSpAtk )
                        scores[Stat.Attack] += 5;
                }

                if( adapter.RoleProfile.PrimaryRole == RoleClass.TrickRoomAbuser || adapter.RoleProfile.SecondaryRoles.Contains( RoleClass.TrickRoomAbuser ) )
                {
                    if( adapter.RoleProfile.Biases.Contains( RoleBias.TrickRoomSpeed ) )
                    {
                        if( investedAtk )
                            scores[Stat.SpAttack] += 2;

                        if( investedSpAtk )
                            scores[Stat.Attack] += 2;

                        if( speedCommit != SpeedCommitment.Comitted )
                            scores[Stat.Speed] += 1;
                    }
                    else if( adapter.RoleProfile.Biases.Contains( RoleBias.AwkwardSpeed ) )
                    {
                        if( investedAtk )
                            scores[Stat.SpAttack] += 1;

                        if( investedSpAtk )
                            scores[Stat.Attack] += 1;

                        if( speedCommit != SpeedCommitment.Comitted )
                            scores[Stat.Speed] += 2;
                    }
                }

                if( investedHP || investedDef || investedSpDef )
                {
                    if( investedAtk )
                        scores[Stat.SpAttack] += 1;
                    else if( investedSpAtk )
                        scores[Stat.Attack] += 1;
                    else if( speedCommit != SpeedCommitment.Comitted )
                        scores[Stat.Speed] += 1;
                }
            break;

            case NatureArchetype.BulkyPhysical:
            case NatureArchetype.BulkySpecial:
                //--We never dump the boosted stat in a clear cut defensive role either
                scores[boostedStat] -= 999;

                if( bulkyPhysical )
                    scores[Stat.Defense] -= 2;

                if( bulkySpecial )
                    scores[Stat.SpDefense] -= 2;

                if( boostedStat == Stat.Attack )
                {
                    scores[Stat.SpAttack] += 3;

                    if( speedCommit != SpeedCommitment.Comitted )
                        scores[Stat.Speed] += 2;
                }

                if( boostedStat == Stat.SpAttack )
                {
                    scores[Stat.Attack] += 3;

                    if( speedCommit != SpeedCommitment.Comitted )
                        scores[Stat.Speed] += 2;
                }

                if( boostedStat == Stat.Speed )
                {
                    if( investedAtk )
                        scores[Stat.SpAttack] += 5;

                    if( investedSpAtk )
                        scores[Stat.Attack] += 5;
                }

                if( boostedStat == Stat.Defense || boostedStat == Stat.SpDefense )
                {
                    if( investedAtk )
                        scores[Stat.SpAttack] += 5;
                    else if( investedSpAtk )
                        scores[Stat.Attack] += 5;
                    else if( speedCommit != SpeedCommitment.Comitted )
                        scores[Stat.Speed] += 2;
                }

                if( recoveryItem )
                {
                    if( investedAtk )
                        scores[Stat.SpAttack] += 2;
                    
                    if( investedSpAtk )
                        scores[Stat.Attack] += 2;
                }

                if( hyperOffensiveItem )
                {
                    if( investedAtk )
                        scores[Stat.SpAttack] += 2;
                    
                    if( investedSpAtk )
                        scores[Stat.Attack] += 2;

                    if( speedCommit != SpeedCommitment.Comitted )
                        scores[Stat.Speed] += 2;
                }
            break;

            case NatureArchetype.MixedFast:
                //--Fast mixed attackers likely don't drop their offenses ever.
                scores[mixedPrimaryAxis] -= 5;

                if( mixedPrimaryAxis == Stat.Attack )
                    scores[Stat.SpAttack] -= 3;
                else
                    scores[Stat.Attack] -= 3;

                scores[Stat.Speed] -= 3;

                if( baseDefense > baseSpDefense )
                    scores[Stat.SpDefense] += 1;
                else if( baseSpDefense > baseDefense )
                    scores[Stat.Defense] += 1;

                if( hyperOffensiveItem )
                {
                    if( baseDefense > baseSpDefense )
                        scores[Stat.SpDefense] += 1;
                    else if( baseSpDefense > baseDefense )
                        scores[Stat.Defense] += 1;
                }

                if( recoveryItem || adapter.Item == ItemBattleEffectID.FocusSash )
                    scores[Stat.Speed] += 2;

                if( adapter.RoleProfile.Traits.Contains( RoleTrait.WideMoveCoverage ) )
                {
                    if( baseDefense > baseSpDefense )
                        scores[Stat.SpDefense] += 1;
                    else if( baseSpDefense > baseDefense )
                        scores[Stat.Defense] += 1;
                }
            break;

            case NatureArchetype.MixedSlow:
                //--Slow mixed attacker definitely doesn't care about speed
                scores[Stat.Speed] += 5;

                scores[mixedPrimaryAxis] -= 5;

                if( mixedPrimaryAxis == Stat.Attack )
                    scores[Stat.SpAttack] -= 3;
                else
                    scores[Stat.Attack] -= 3;

                if( baseDefense > baseSpDefense )
                    scores[Stat.SpDefense] += 1;
                else if( baseSpDefense > baseDefense )
                    scores[Stat.Defense] += 1;

                if( hyperOffensiveItem )
                {
                    if( speedCommit == SpeedCommitment.Dumped )
                        scores[Stat.Speed] += 2;
                    else if( baseDefense > baseSpDefense )
                        scores[Stat.SpDefense] += 1;
                    else if( baseSpDefense > baseDefense )
                        scores[Stat.Defense] += 1;
                }

                if( recoveryItem || adapter.Item == ItemBattleEffectID.FocusSash )
                    scores[Stat.Speed] += 2;

                if( adapter.RoleProfile.Traits.Contains( RoleTrait.WideMoveCoverage ) )
                {
                    if( speedCommit == SpeedCommitment.Dumped )
                        scores[Stat.Speed] += 2;
                    else if( baseDefense > baseSpDefense )
                        scores[Stat.SpDefense] += 1;
                    else if( baseSpDefense > baseDefense )
                        scores[Stat.Defense] += 1;
                }
            break;
        };

        var first = scores.OrderByDescending( s => s.Value ).First();
        var second = scores.OrderByDescending( s => s.Value ).Skip( 1 ).FirstOrDefault();

        if( second.Value > 0 && first.Value - second.Value <= 4 )
        {
            int rand = UnityEngine.Random.Range( first.Value, second.Value + 1 );
            if( rand <= second.Value )
                return second.Key;
            else
                return first.Key;
        }
        else
            return first.Key;
    }

    private NatureID FindNatureID( Stat boostedStat, Stat dumpedStat )
    {
        //--Reverse look up nature from the dictionary
        var nature = _natures.FirstOrDefault( n => n.Value.PositiveStat == boostedStat && n.Value.NegativeStat == dumpedStat );

        if( EqualityComparer<KeyValuePair<NatureID, Nature>>.Default.Equals( nature, default ) )
            return NatureID.Neutral;

        return nature.Key;
    }

    private void InitNatureDictionary()
    {
        _natures = new()
        {
            { NatureID.Neutral, new() },
            { NatureID.Lonely,      new(){ PositiveStat = Stat.Attack,      NegativeStat = Stat.Defense } },
            { NatureID.Brave,       new(){ PositiveStat = Stat.Attack,      NegativeStat = Stat.Speed } },
            { NatureID.Adamant,     new(){ PositiveStat = Stat.Attack,      NegativeStat = Stat.SpAttack } },
            { NatureID.Naughty,     new(){ PositiveStat = Stat.Attack,      NegativeStat = Stat.SpDefense } },
            { NatureID.Bold,        new(){ PositiveStat = Stat.Defense,     NegativeStat = Stat.Attack } },
            { NatureID.Relaxed,     new(){ PositiveStat = Stat.Defense,     NegativeStat = Stat.Speed } },
            { NatureID.Impish,      new(){ PositiveStat = Stat.Defense,     NegativeStat = Stat.SpAttack } },
            { NatureID.Lax,         new(){ PositiveStat = Stat.Defense,     NegativeStat = Stat.SpDefense } },
            { NatureID.Timid,       new(){ PositiveStat = Stat.Speed,       NegativeStat = Stat.Attack } },
            { NatureID.Hasty,       new(){ PositiveStat = Stat.Speed,       NegativeStat = Stat.Defense } },
            { NatureID.Jolly,       new(){ PositiveStat = Stat.Speed,       NegativeStat = Stat.SpAttack } },
            { NatureID.Naive,       new(){ PositiveStat = Stat.Speed,       NegativeStat = Stat.SpDefense } },
            { NatureID.Modest,      new(){ PositiveStat = Stat.SpAttack,    NegativeStat = Stat.Attack } },
            { NatureID.Mild,        new(){ PositiveStat = Stat.SpAttack,    NegativeStat = Stat.Defense } },
            { NatureID.Quiet,       new(){ PositiveStat = Stat.SpAttack,    NegativeStat = Stat.Speed } },
            { NatureID.Rash,        new(){ PositiveStat = Stat.SpAttack,    NegativeStat = Stat.SpDefense } },
            { NatureID.Calm,        new(){ PositiveStat = Stat.SpDefense,   NegativeStat = Stat.Attack } },
            { NatureID.Gentle,      new(){ PositiveStat = Stat.SpDefense,   NegativeStat = Stat.Defense } },
            { NatureID.Sassy,       new(){ PositiveStat = Stat.SpDefense,   NegativeStat = Stat.Speed } },
            { NatureID.Careful,     new(){ PositiveStat = Stat.SpDefense,   NegativeStat = Stat.SpAttack } },
        };
    }

    private void InitStatSpreads()
    {
        _statSpreads = new()
        {
            {
                StatSpreadType.FastPhysicalAttacker, new()
                {
                    Spread = new()
                    {
                        { Stat.HP, 4},
                        { Stat.Attack, 252},
                        { Stat.Defense, 0},
                        { Stat.SpAttack, 0},
                        { Stat.SpDefense, 0},
                        { Stat.Speed, 252},
                    }
                }
            },
            {
                StatSpreadType.FastSpecialAttacker, new()
                {
                    Spread = new()
                    {
                        { Stat.HP, 4},
                        { Stat.Attack, 0},
                        { Stat.Defense, 0},
                        { Stat.SpAttack, 252},
                        { Stat.SpDefense, 0},
                        { Stat.Speed, 252},
                    }
                }
            },
            {
                StatSpreadType.FastMixedSpecial_PhysicalAttacker, new()
                {
                    Spread = new()
                    {
                        { Stat.HP, 4},
                        { Stat.Attack, 172},
                        { Stat.Defense, 0},
                        { Stat.SpAttack, 80},
                        { Stat.SpDefense, 0},
                        { Stat.Speed, 252},
                    }
                }
            },
            {
                StatSpreadType.FastMixedPhysical_SpecialAttacker, new()
                {
                    Spread = new()
                    {
                        { Stat.HP, 4},
                        { Stat.Attack, 80},
                        { Stat.Defense, 0},
                        { Stat.SpAttack, 172},
                        { Stat.SpDefense, 0},
                        { Stat.Speed, 252},
                    }
                }
            },
            {
                StatSpreadType.PhysicallyDefensive, new()
                {
                    Spread = new()
                    {
                        { Stat.HP, 252},
                        { Stat.Attack, 0},
                        { Stat.Defense, 252},
                        { Stat.SpAttack, 0},
                        { Stat.SpDefense, 4},
                        { Stat.Speed, 0},
                    }
                }
            },
            {
                StatSpreadType.SpeciallyDefensive, new()
                {
                    Spread = new()
                    {
                        { Stat.HP, 252},
                        { Stat.Attack, 0},
                        { Stat.Defense, 4},
                        { Stat.SpAttack, 0},
                        { Stat.SpDefense, 252},
                        { Stat.Speed, 0},
                    }
                }
            },
            {
                StatSpreadType.BulkyPhysical_PhysicalAttacker, new()
                {
                    Spread = new()
                    {
                        { Stat.HP, 0},
                        { Stat.Attack, 252},
                        { Stat.Defense, 212},
                        { Stat.SpAttack, 0},
                        { Stat.SpDefense, 0},
                        { Stat.Speed, 44},
                    }
                }
            },
            {
                StatSpreadType.BulkyPhysical_SpecialAttacker, new()
                {
                    Spread = new()
                    {
                        { Stat.HP, 0},
                        { Stat.Attack, 0},
                        { Stat.Defense, 212},
                        { Stat.SpAttack, 252},
                        { Stat.SpDefense, 0},
                        { Stat.Speed, 44},
                    }
                }
            },
            {
                StatSpreadType.BulkySpecial_PhysicalAttacker, new()
                {
                    Spread = new()
                    {
                        { Stat.HP, 0},
                        { Stat.Attack, 252},
                        { Stat.Defense, 0},
                        { Stat.SpAttack, 0},
                        { Stat.SpDefense, 212},
                        { Stat.Speed, 44},
                    }
                }
            },
            {
                StatSpreadType.BulkySpecial_SpecialAttacker, new()
                {
                    Spread = new()
                    {
                        { Stat.HP, 0},
                        { Stat.Attack, 0},
                        { Stat.Defense, 0},
                        { Stat.SpAttack, 252},
                        { Stat.SpDefense, 212},
                        { Stat.Speed, 44},
                    }
                }
            },
            {
                StatSpreadType.AllRounder_Atk, new()
                {
                    Spread = new()
                    {
                        { Stat.HP, 48},
                        { Stat.Attack, 144},
                        { Stat.Defense, 0},
                        { Stat.SpAttack, 52},
                        { Stat.SpDefense, 184},
                        { Stat.Speed, 80},
                    }
                }
            },
            {
                StatSpreadType.AllRounder_SpAtk, new()
                {
                    Spread = new()
                    {
                        { Stat.HP, 48 },
                        { Stat.Attack, 0 },
                        { Stat.Defense, 52 },
                        { Stat.SpAttack, 144 },
                        { Stat.SpDefense, 184 },
                        { Stat.Speed, 80 },
                    },
                }
            },
        };
    }

}

public enum StatSpreadType
{
    FastPhysicalAttacker,
    FastSpecialAttacker,
    FastMixedSpecial_PhysicalAttacker,
    FastMixedPhysical_SpecialAttacker,
    PhysicallyDefensive,
    SpeciallyDefensive,
    BulkyPhysical_PhysicalAttacker,
    BulkyPhysical_SpecialAttacker,
    BulkySpecial_PhysicalAttacker,
    BulkySpecial_SpecialAttacker,
    UtilityTank_Physical,
    UtilityTank_Special,
    ArcanineSpread,
    AllRounder_Atk,
    AllRounder_SpAtk,
}

public class StatSpread
{
    public Dictionary<Stat, int> Spread;
    public NatureID Nature;
}

public enum NatureArchetype
{
    Neutral,
    OffensiveFast,
    OffensiveSlow,
    BulkyPhysical,
    BulkySpecial,
    MixedFast,
    MixedSlow,
}

public enum SpeedCommitment
{
    Comitted,
    Creeping,
    Dumped,
}
