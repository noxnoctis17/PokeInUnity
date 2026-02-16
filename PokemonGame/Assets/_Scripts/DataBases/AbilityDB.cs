using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AbilityDB
{
    public static Dictionary<AbilityID, Ability> Abilities = new()
    {
        //--1/4 HP ATK Boost-------------------------------------------------------------------------------------
        {
            AbilityID.Blaze, new()
            {
                Name = "Blaze",
                Description = "Powers up Fire-type moves by 1.5x when the Pokemon's HP is 33% or less",

                OnModify_ATK = ( float atk, Pokemon attacker, Pokemon target, Move move ) =>
                {
                    if( move.MoveSO.Type == PokemonType.Fire  && attacker.CurrentHP <= Mathf.Floor( attacker.MaxHP / 3 ) )
                    {
                        Debug.Log( $"Blaze is Active!" );
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        atk = atk * 1.5f;
                    }

                    return atk;
                },

                OnModify_SpATK = ( float spAtk, Pokemon attacker, Pokemon target, Move move ) =>
                {
                    if( move.MoveSO.Type == PokemonType.Fire  && attacker.CurrentHP <= Mathf.Floor( attacker.MaxHP / 3 ) )
                    {
                        Debug.Log( $"Blaze is Active!" );
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        spAtk = spAtk * 1.5f;
                    }

                    return spAtk;
                }
            }
        },
        {
            AbilityID.Torrent, new()
            {
                Name = "Torrent",
                Description = "Powers up Water-type moves by 1.5x when the Pokemon's HP is 33% or less",

                OnModify_ATK = ( float atk, Pokemon attacker, Pokemon target, Move move ) =>
                {
                    if( move.MoveSO.Type == PokemonType.Water  && attacker.CurrentHP <= Mathf.Floor( attacker.MaxHP / 3 ) )
                    {
                        Debug.Log( $"Torrent is Active!" );
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        atk = atk * 1.5f;
                    }

                    return atk;
                },

                OnModify_SpATK = ( float spAtk, Pokemon attacker, Pokemon target, Move move ) =>
                {
                    if( move.MoveSO.Type == PokemonType.Water  && attacker.CurrentHP <= Mathf.Floor( attacker.MaxHP / 3 ) )
                    {
                        Debug.Log( $"Torrent is Active!" );
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        spAtk = spAtk * 1.5f;
                    }

                    return spAtk;
                }
            }
        },
        {
            AbilityID.Overgrow, new()
            {
                Name = "Overgrow",
                Description = "Powers up Grass-type moves by 1.5x when the Pokemon's HP is 33% or less",

                OnModify_ATK = ( float atk, Pokemon attacker, Pokemon target, Move move ) =>
                {
                    if( move.MoveSO.Type == PokemonType.Grass  && attacker.CurrentHP <= Mathf.Floor( attacker.MaxHP / 3 ) )
                    {
                        Debug.Log( $"Overgrow is Active!" );
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        atk = atk * 1.5f;
                    }

                    return atk;
                },

                OnModify_SpATK = ( float spAtk, Pokemon attacker, Pokemon target, Move move ) =>
                {
                    if( move.MoveSO.Type == PokemonType.Grass  && attacker.CurrentHP <= Mathf.Floor( attacker.MaxHP / 3 ) )
                    {
                        Debug.Log( $"Overgrow is Active!" );
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        spAtk = spAtk * 1.5f;
                    }

                    return spAtk;
                }
            }
        },
        {
            AbilityID.Swarm, new()
            {
                Name = "Swarm",
                Description = "Powers up Bug-type moves by 1.5x when the Pokemon's HP is 33% or less",

                OnModify_ATK = ( float atk, Pokemon attacker, Pokemon target, Move move ) =>
                {
                    if( move.MoveSO.Type == PokemonType.Bug  && attacker.CurrentHP <= Mathf.Floor( attacker.MaxHP / 3 ) )
                    {
                        Debug.Log( $"Swarm is Active!" );
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        atk = atk * 1.5f;
                    }

                    return atk;
                },

                OnModify_SpATK = ( float spAtk, Pokemon attacker, Pokemon target, Move move ) =>
                {
                    if( move.MoveSO.Type == PokemonType.Bug  && attacker.CurrentHP <= Mathf.Floor( attacker.MaxHP / 3 ) )
                    {
                        Debug.Log( $"Swarm is Active!" );
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        spAtk = spAtk * 1.5f;
                    }

                    return spAtk;
                }
            }
        },

        //---------------STATUS-BASED STAT CHANGE----------------------------------------------------------------------------------
        {
            AbilityID.Guts, new()
            {
                Name = "Guts",
                Description = "If this Pokemon has a non-volatile status condition, its Attack is multiplied by 1.5. This Pokemon's physical attacks ignore the burn effect of halving damage.",

                OnSetSevereStatus = ( SevereConditionID id, Pokemon pokemon, StatusEffectSource source ) =>
                {
                    if( id == SevereConditionID.BRN )
                    {
                        pokemon.ApplyDirectStatModifier( Stat.Attack, DirectModifierCause.BRN, 1.5f );
                    }
                },

                OnAbilityEnter = ( Pokemon pokemon, List<BattleUnit> opps, Battlefield field ) =>
                {
                    if( pokemon.SevereStatus != null && pokemon.SevereStatus.ID == SevereConditionID.BRN )
                    {
                        pokemon.ApplyDirectStatModifier( Stat.Attack, DirectModifierCause.BRN, 1.5f );
                    }
                },

                OnAbilityExit = ( Pokemon pokemon, List<BattleUnit> opps, Battlefield field ) =>
                {
                    if( pokemon.SevereStatus != null && pokemon.SevereStatus.ID == SevereConditionID.BRN )
                    {
                        pokemon.RemoveDirectStatModifier( Stat.Attack, DirectModifierCause.BRN );
                    }
                }
            }
        },
        {
            AbilityID.MarvelScale, new()
            {
                Name = "Marvel Scale",
                Description = "If this Pokemon has a non-volatile status condition, its Defense is multiplied by 1.5.",

                OnModify_DEF = ( float def, Pokemon attacker, Pokemon target, Move move ) =>
                {
                    if( target.SevereStatus != null )
                    {
                        Debug.Log( "Marvel Scale is active!" );
                        BattleSystem.Instance.TriggerAbilityCutIn( target );
                        def = def * 1.5f;
                    }

                    return def;
                }
            }
        },
        {
            AbilityID.QuickFeet, new()
            {
                Name = "Quick Feet",
                Description = "If this Pokemon has a non-volatile status condition, its Speed is multiplied by 1.5. This Pokemon ignores the Speed reduction effect of Paralysis.",

                OnModify_SPD = ( float spd, Pokemon attacker, Pokemon target, Move move ) =>
                {
                    if( attacker.SevereStatus != null )
                    {
                        Debug.Log( "Quick Feet is active!" );
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        spd = spd * 1.5f;
                    }

                    return spd;
                }
            }
        },

        //-------------------COMPOUND EYES LOL------------------------------------------------------------------
        {
            AbilityID.CompoundEyes, new()
            {
                Name = "Compound Eyes",
                Description = "This Pokemon's moves have their accuracy multiplied by 1.3.",

                OnModify_ACC = ( float acc, Pokemon attacker, Pokemon target, Move move ) =>
                {
                        Debug.Log( "Quick Feet is active!" );
                        return acc * 1.3f;
                }
            }
        },
        //----------Prevent stat stage changes (ie from intimidate)
        {
            AbilityID.KeenEye, new()
            {
                Name = "Keen Eye",
                Description = "Prevents other Pokemon from lowering this Pokemon's accuracy stat stage. This Pokemon ignores a target's evasiveness stat stage.",

                OnStatStageChange = ( Dictionary<Stat, int> stages, Pokemon attacker, Pokemon target ) =>
                {
                    //--If Self-Boost, return
                    if( attacker != null && attacker == target )
                        return;

                    if( stages.ContainsKey( Stat.Accuracy ) && stages[Stat.Accuracy] < 0 )
                    {
                        Debug.Log( "Keen Eye prevented a stat stage from being lowered!" );
                        stages.Remove( Stat.Accuracy );
                        BattleSystem.Instance.TriggerAbilityCutIn( target );
                        target.AddStatusEvent( $"Keen Eye prevents {target.NickName}'s accuracy from being lowered!" );
                    }
                }
            }
        },
        {
            AbilityID.HyperCutter, new()
            {
                Name = "Hyper Cutter",
                Description = "Prevents other Pokemon from lowering this Pokemon's Attack stat stage.",

                OnStatStageChange = ( Dictionary<Stat, int> stages, Pokemon attacker, Pokemon target ) =>
                {
                    Debug.Log( $"Hyper Cutter reached! Attacker: {attacker.NickName}, Target: {target.NickName}" );
                    //--If Self-Boost, return
                    if( attacker != null && attacker == target )
                    {
                        Debug.Log( "Self-boost triggered!" );
                        return;
                    }

                    if( stages.ContainsKey( Stat.Attack ) && stages[Stat.Attack] < 0 )
                    {
                        Debug.Log( "Hyper Cutter prevented a stat stage from being lowered!" );
                        stages.Remove( Stat.Attack );
                        BattleSystem.Instance.TriggerAbilityCutIn( target );
                        target.AddStatusEvent( $"Hyper Cutter prevents {target.NickName}'s Attack from being lowered!" );
                    }
                }
            }
        },
        {
            AbilityID.BigPecks, new()
            {
                Name = "Big Pecks",
                Description = "Prevents other Pokemon from lowering this Pokemon's Defense stat stage.",

                OnStatStageChange = ( Dictionary<Stat, int> stages, Pokemon attacker, Pokemon target ) =>
                {
                    //--If Self-Boost, return
                    if( attacker != null && attacker == target )
                        return;

                    if( stages.ContainsKey( Stat.Defense ) && stages[Stat.Defense] < 0 )
                    {
                        Debug.Log( "Big Pecks prevented a stat stage from being lowered!" );
                        stages.Remove( Stat.Defense );
                        BattleSystem.Instance.TriggerAbilityCutIn( target );
                        target.AddStatusEvent( $"Big Pecks prevents {target.NickName}'s Defense from being lowered!" );
                    }
                }
            }
        },
        {
            AbilityID.ClearBody, new()
            {
                Name = "Clear Body",
                Description = "Prevents other Pokemon from lowering any of this Pokemon's stat stages.",

                OnStatStageChange = ( Dictionary<Stat, int> stages, Pokemon attacker, Pokemon target ) =>
                {
                    Debug.Log( $"Clear Body reached! Attacker: {attacker.NickName}, Target: {target.NickName}" );
                    //--If Self-Boost, return
                    if( attacker != null && attacker == target )
                    {
                        Debug.Log( "Self-boost triggered!" );
                        return;
                    }

                    var statstoRemove = new List<Stat>();
                    bool stageChangePrevented = false;
                    foreach( var kvp in stages )
                    {
                        if( kvp.Value < 0 )
                            statstoRemove.Add( kvp.Key );
                    }

                    foreach( var stat in statstoRemove )
                    {
                        Debug.Log( "Clear Body prevented a stat stage from being lowered!" );
                        stages.Remove( stat );
                        stageChangePrevented = true;
                    }

                    if( stageChangePrevented )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( target );
                        target.AddStatusEvent( $"Clear Body prevents {target.NickName}'s Stats from being lowered!" );
                    }
                }
            }
        },
        {
            AbilityID.WhiteSmoke, new()
            {
                Name = "White Smoke",
                Description = "Prevents other Pokemon from lowering any of this Pokemon's stat stages.",

                OnStatStageChange = ( Dictionary<Stat, int> stages, Pokemon attacker, Pokemon target ) =>
                {
                    Debug.Log( $"White Smoke reached! Attacker: {attacker.NickName}, Target: {target.NickName}" );
                    //--If Self-Boost, return
                    if( attacker != null && attacker == target )
                    {
                        Debug.Log( "Self-boost triggered!" );
                        return;
                    }

                    var statstoRemove = new List<Stat>();
                    bool stageChangePrevented = false;
                    foreach( var kvp in stages )
                    {
                        if( kvp.Value < 0 )
                            statstoRemove.Add( kvp.Key );
                    }

                    foreach( var stat in statstoRemove )
                    {
                        Debug.Log( "White Smoke prevented a stat stage from being lowered!" );
                        stages.Remove( stat );
                        stageChangePrevented = true;
                    }

                    if( stageChangePrevented )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( target );
                        target.AddStatusEvent( $"White Smoke prevents {target.NickName}'s Stats from being lowered!" );
                    }
                }
            }
        },
        //-----------Intimidate!!!!!!!!!!!!!!!!!!!!!-----------------------------------------------------------------
        {
            AbilityID.Intimidate, new()
            {
                Name = "Intimidate",
                Description = "On switch-in, this Pokemon lowers the Attack of opposing Pokemon by 1 stage.",
                ID = AbilityID.Intimidate,

                OnAbilityEnter = ( Pokemon attacker, List<BattleUnit> targets, Battlefield field ) =>
                {
                    List<StatStage> statStages = new();
                    StatStage stage = new();
                    stage.Stat = Stat.Attack;
                    stage.Change = -1;
                    statStages.Add( stage );

                    BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                    for( int i = 0; i < targets.Count; i++ )
                    {
                        var target = targets[i];

                        if( target.Pokemon.AbilityID == AbilityID.Oblivious )
                        {
                            BattleSystem.Instance.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{target.Pokemon.NickName} is oblivious to the intimidation!" ) );
                            continue;
                        }

                        if( target.Pokemon.AbilityID == AbilityID.InnerFocus )
                        {
                            BattleSystem.Instance.AddDialogue( $"{target.Pokemon.NickName}'s focus prevented it from being intimidated!" );
                            continue;
                        }

                        StageChangeSource source = new()
                        {
                            Pokemon = attacker,
                            MoveName = string.Empty,
                            Source = StageChangeSourceType.Ability,
                        };

                        Debug.Log( $"Intimidate Fired! {target.Pokemon.NickName}'s ATK before intimidate: {target.Pokemon.Attack}" );
                        target.Pokemon.ApplyStatStageChange( statStages, source );
                        Debug.Log( $"Intimidate Fired! {target.Pokemon.NickName}'s ATK after intimidate: {target.Pokemon.Attack}" );
                    }
                }
            }
        },
        {
            AbilityID.Demoralize, new()
            {
                Name = "Demoralize",
                Description = "On switch-in, this Pokemon lowers the Special Attack of opposing Pokemon by 1 stage.",

                OnAbilityEnter = ( Pokemon attacker, List<BattleUnit> targets, Battlefield field ) =>
                {
                    List<StatStage> statStages = new();
                    StatStage stage = new();
                    stage.Stat = Stat.SpAttack; 
                    stage.Change = -1;
                    statStages.Add( stage );

                    BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                    for( int i = 0; i < targets.Count; i++ )
                    {
                        var target = targets[i];
                        
                        if( target.Pokemon.AbilityID == AbilityID.Oblivious )
                        {
                            BattleSystem.Instance.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{target.Pokemon.NickName} is oblivious to being demoralized!" ) );
                            continue;
                        }

                        if( target.Pokemon.AbilityID == AbilityID.InnerFocus )
                        {
                            BattleSystem.Instance.AddDialogue( $"{target.Pokemon.NickName}'s focus prevented it from being demoralized!" );
                            continue;
                        }

                        StageChangeSource source = new()
                        {
                            Pokemon = attacker,
                            MoveName = string.Empty,
                            Source = StageChangeSourceType.Ability,
                        };

                        Debug.Log( $"Demoralize Fired! {target.Pokemon.NickName}'s Sp.ATK before Demoralize: {target.Pokemon.SpAttack}" );
                        target.Pokemon.ApplyStatStageChange( statStages, source );
                        Debug.Log( $"Demoralize Fired! {target.Pokemon.NickName}'s Sp.ATK after Demoralize: {target.Pokemon.SpAttack}" );
                    }
                }
            }
        },
//=================================================================================================================================
//=================================================[WEATHER]=======================================================================
//=================================================================================================================================
        {
            AbilityID.Drought, new()
            {
                Name = "Drought",
                Description = "On switch-in, this Pokemon summons harsh sunlight.",

                OnAbilityEnter = ( Pokemon attacker, List<BattleUnit> targets, Battlefield battleField ) =>
                {
                    if( battleField.Weather?.ID != WeatherConditionID.SUNNY )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        Debug.Log( "Setting weather: Harsh Sunlight" );
                        battleField.SetWeather( WeatherConditionID.SUNNY );
                    }
                }
            }
        },
        {
            AbilityID.Drizzle, new()
            {
                Name = "Drizzle",
                Description = "On switch-in, this Pokemon summons rainfall.",

                OnAbilityEnter = ( Pokemon attacker, List<BattleUnit> targets, Battlefield battleField ) =>
                {
                    if( battleField.Weather?.ID != WeatherConditionID.RAIN )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        Debug.Log( "Setting weather: Rainfall" );
                        battleField.SetWeather( WeatherConditionID.RAIN );
                    }
                }
            }
        },
        {
            AbilityID.Sandstream, new()
            {
                Name = "Sand Stream",
                Description = "On switch-in, this Pokemon summons a raging sandstorm.",

                OnAbilityEnter = ( Pokemon attacker, List<BattleUnit> targets, Battlefield battleField ) =>
                {
                    if( battleField.Weather?.ID != WeatherConditionID.SANDSTORM )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        Debug.Log( "Setting weather: Raging Sandstorm" );
                        battleField.SetWeather( WeatherConditionID.SANDSTORM );
                    }
                }
            }
        },
        {
            AbilityID.SnowWarning, new()
            {
                Name = "Snow Warning",
                Description = "On switch-in, this Pokemon summons snowfall.",

                OnAbilityEnter = ( Pokemon attacker, List<BattleUnit> targets, Battlefield battleField ) =>
                {
                    if( battleField.Weather?.ID != WeatherConditionID.SNOW )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        Debug.Log( "Setting weather: Snowfall" );
                        battleField.SetWeather( WeatherConditionID.SNOW );
                    }
                }
            }
        },
        {
            AbilityID.Chlorophyll, new()
            {
                Name = "Chlorophyll",
                Description = "This Pokemon's Speed is doubled in harsh sunlight.",

                //--The effects of these abilities are triggered by weather conditions,
                //--therefore the effects are applied or removed in their respective WeatherConditionsDB entry
                OnAbilityTriggered = ( Pokemon pokemon ) =>
                {
                    // BattleSystem.Instance.SetBattleFlag( BattleFlag.SpeedChange, true );
                    // BattleSystem.Instance.TriggerAbilityCutIn( pokemon ); //--This shit pops up every turn the weather is active, now i know why cart doesn't do this lol. i'll use it for the hud ability display if i implement that.
                },

                OnAbilityEnter = ( Pokemon pokemon, List<BattleUnit> units, Battlefield field ) =>
                {
                    if( field.Weather?.ID == WeatherConditionID.SUNNY )
                    {
                        Debug.Log( $"{pokemon.NickName}'s Chlorophyll is active!" );
                        Debug.Log( $"{pokemon.NickName}'s SPD Stat before is: {pokemon.Speed}" );
                        pokemon.ApplyDirectStatModifier( Stat.Speed, DirectModifierCause.WeatherSPD, 2f );
                        Debug.Log( $"{pokemon.NickName}'s SPD Stat after is: {pokemon.Speed}" );
                    }
                },

                OnAbilityExit = ( Pokemon pokemon, List<BattleUnit> units, Battlefield field ) =>
                {
                    Debug.Log( $"{pokemon.NickName}'s Chlorophyll is no longer active!" );
                    Debug.Log( $"{pokemon.NickName}'s SPD Stat before is: {pokemon.Speed}" );
                    pokemon.RemoveDirectStatModifier( Stat.Speed, DirectModifierCause.WeatherSPD );
                    Debug.Log( $"{pokemon.NickName}'s SPD Stat after is: {pokemon.Speed}" );
                },
            }
        },
        {
            AbilityID.SwiftSwim, new()
            {
                Name = "Swift Swim",
                Description = "This Pokemon's Speed is doubled in rainfall.",

                //--The effects of these abilities are triggered by weather conditions,
                //--therefore the effects are applied or removed in their respective WeatherConditionsDB entry
                OnAbilityTriggered = ( Pokemon pokemon ) =>
                {
                    // BattleSystem.Instance.SetBattleFlag( BattleFlag.SpeedChange, true );
                    // BattleSystem.Instance.TriggerAbilityCutIn( pokemon ); //--This shit pops up every turn the weather is active, now i know why cart doesn't do this lol. i'll use it for the hud ability display if i implement that.
                },

                OnAbilityEnter = ( Pokemon pokemon, List<BattleUnit> units, Battlefield field ) =>
                {
                    if( field.Weather?.ID == WeatherConditionID.RAIN )
                    {
                        Debug.Log( $"{pokemon.NickName}'s Swift Swim is active!" );
                        Debug.Log( $"{pokemon.NickName}'s SPD Stat before is: {pokemon.Speed}" );
                        pokemon.ApplyDirectStatModifier( Stat.Speed, DirectModifierCause.WeatherSPD, 2f );
                        Debug.Log( $"{pokemon.NickName}'s SPD Stat after is: {pokemon.Speed}" );
                    }
                },

                OnAbilityExit = ( Pokemon pokemon, List<BattleUnit> units, Battlefield field ) =>
                {
                    Debug.Log( $"{pokemon.NickName}'s Swift Swim is no longer active!" );
                    Debug.Log( $"{pokemon.NickName}'s SPD Stat before is: {pokemon.Speed}" );
                    pokemon.RemoveDirectStatModifier( Stat.Speed, DirectModifierCause.WeatherSPD );
                    Debug.Log( $"{pokemon.NickName}'s SPD Stat after is: {pokemon.Speed}" );
                },
            }
        },
        {
            AbilityID.SandRush, new()
            {
                Name = "Sand Rush",
                Description = "This Pokemon's Speed is doubled in a raging sandstorm.",

                //--The effects of these abilities are triggered by weather conditions,
                //--therefore the effects are applied or removed in their respective WeatherConditionsDB entry
                OnAbilityTriggered = ( Pokemon pokemon ) =>
                {
                    // BattleSystem.Instance.SetBattleFlag( BattleFlag.SpeedChange, true );
                    // BattleSystem.Instance.TriggerAbilityCutIn( pokemon ); //--This shit pops up every turn the weather is active, now i know why cart doesn't do this lol. i'll use it for the hud ability display if i implement that.
                },

                OnAbilityEnter = ( Pokemon pokemon, List<BattleUnit> units, Battlefield field ) =>
                {
                    if( field.Weather?.ID == WeatherConditionID.SANDSTORM )
                    {
                        Debug.Log( $"{pokemon.NickName}'s Sand Rush is active!" );
                        Debug.Log( $"{pokemon.NickName}'s SPD Stat before is: {pokemon.Speed}" );
                        pokemon.ApplyDirectStatModifier( Stat.Speed, DirectModifierCause.WeatherSPD, 2f );
                        Debug.Log( $"{pokemon.NickName}'s SPD Stat after is: {pokemon.Speed}" );
                    }
                },

                OnAbilityExit = ( Pokemon pokemon, List<BattleUnit> units, Battlefield field ) =>
                {
                    Debug.Log( $"{pokemon.NickName}'s Sand Rush is no longer active!" );
                    Debug.Log( $"{pokemon.NickName}'s SPD Stat before is: {pokemon.Speed}" );
                    pokemon.RemoveDirectStatModifier( Stat.Speed, DirectModifierCause.WeatherSPD );
                    Debug.Log( $"{pokemon.NickName}'s SPD Stat after is: {pokemon.Speed}" );
                },
            }
        },
        {
            AbilityID.SlushRush, new()
            {
                Name = "Slush Rush",
                Description = "This Pokemon's Speed is doubled in snowfall.",

                //--The effects of these abilities are triggered by weather conditions,
                //--therefore the effects are applied or removed in their respective WeatherConditionsDB entry
                OnAbilityTriggered = ( Pokemon pokemon ) =>
                {
                    // BattleSystem.Instance.SetBattleFlag( BattleFlag.SpeedChange, true );
                    // BattleSystem.Instance.TriggerAbilityCutIn( pokemon ); //--This shit pops up every turn the weather is active, now i know why cart doesn't do this lol. i'll use it for the hud ability display if i implement that.
                },

                OnAbilityEnter = ( Pokemon pokemon, List<BattleUnit> units, Battlefield field ) =>
                {
                    if( field.Weather?.ID == WeatherConditionID.SNOW )
                    {
                        Debug.Log( $"{pokemon.NickName}'s Slush Rush is active!" );
                        Debug.Log( $"{pokemon.NickName}'s SPD Stat before is: {pokemon.Speed}" );
                        pokemon.ApplyDirectStatModifier( Stat.Speed, DirectModifierCause.WeatherSPD, 2f );
                        Debug.Log( $"{pokemon.NickName}'s SPD Stat after is: {pokemon.Speed}" );
                    }
                },

                OnAbilityExit = ( Pokemon pokemon, List<BattleUnit> units, Battlefield field ) =>
                {
                    Debug.Log( $"{pokemon.NickName}'s Slush Rush is no longer active!" );
                    Debug.Log( $"{pokemon.NickName}'s SPD Stat before is: {pokemon.Speed}" );
                    pokemon.RemoveDirectStatModifier( Stat.Speed, DirectModifierCause.WeatherSPD );
                    Debug.Log( $"{pokemon.NickName}'s SPD Stat after is: {pokemon.Speed}" );
                },
            }
        },
        {
            AbilityID.Insomnia, new()
            {
                Name = "Insomnia",
                Description = "Prevents the Pokemon from falling asleep.",

                OnTrySetSevereStatus = ( SevereConditionID id, Pokemon pokemon, StatusEffectSource StatusEffectSource ) =>
                {
                    if( id == SevereConditionID.SLP )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( pokemon );
                        pokemon.AddStatusEvent( $"{pokemon.NickName}'s Insomnia prevents it from falling asleep!" );

                        return false;
                    }
                    else
                        return true;
                },

                OnAbilityEnter = ( Pokemon pokemon, List<BattleUnit> opponents, Battlefield field ) =>
                {
                    if( pokemon.SevereStatus?.ID == SevereConditionID.SLP )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( pokemon );
                        pokemon.AddStatusEvent( $"{pokemon.NickName}'s Vital Spirit wakes it from sleep!" );
                        pokemon.CureSevereStatus();
                    }
                },
            }  
        },
        {
            AbilityID.VitalSpirit, new()
            {
                Name = "Vital Spirit",
                Description = "Prevents the Pokemon from falling asleep.",

                OnTrySetSevereStatus = ( SevereConditionID id, Pokemon pokemon, StatusEffectSource StatusEffectSource ) =>
                {
                    if( id == SevereConditionID.SLP )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( pokemon );
                        pokemon.AddStatusEvent( $"{pokemon.NickName}'s Vital Spirit prevents it from falling asleep!" );

                        return false;
                    }
                    else
                        return true;
                },

                OnAbilityEnter = ( Pokemon pokemon, List<BattleUnit> opponents, Battlefield field ) =>
                {
                    if( pokemon.SevereStatus?.ID == SevereConditionID.SLP )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( pokemon );
                        pokemon.AddStatusEvent( $"{pokemon.NickName}'s Vital Spirit wakes it from sleep!" );
                        pokemon.CureSevereStatus();
                    }
                },
            }  
        },
        {
            AbilityID.Immunity, new()
            {
                Name = "Immunity",
                Description = "Prevents the Pokemon from being poisoned.",

                OnTrySetSevereStatus = ( SevereConditionID id, Pokemon pokemon, StatusEffectSource StatusEffectSource ) =>
                {
                    if( id == SevereConditionID.PSN )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( pokemon );
                        pokemon.AddStatusEvent( $"{pokemon.NickName}'s Immunity prevents it from being poisoned!" );

                        return false;
                    }
                    else
                        return true;
                },

                OnAbilityEnter = ( Pokemon pokemon, List<BattleUnit> opponents, Battlefield field ) =>
                {
                    if( pokemon.SevereStatus?.ID == SevereConditionID.PSN )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( pokemon );
                        pokemon.AddStatusEvent( $"{pokemon.NickName}'s Immunity cures its poisoning!" );
                        pokemon.CureSevereStatus();
                    }
                },
            }  
        },
        {
            AbilityID.Limber, new()
            {
                Name = "Limber",
                Description = "Prevents the Pokemon from being paralyzed.",

                OnTrySetSevereStatus = ( SevereConditionID id, Pokemon pokemon, StatusEffectSource StatusEffectSource ) =>
                {
                    if( id == SevereConditionID.PAR )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( pokemon );
                        pokemon.AddStatusEvent( $"{pokemon.NickName}'s Limber prevents it from being paralyzed!" );

                        return false;
                    }
                    else
                        return true;
                },

                OnAbilityEnter = ( Pokemon pokemon, List<BattleUnit> opponents, Battlefield field ) =>
                {
                    if( pokemon.SevereStatus?.ID == SevereConditionID.PAR )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( pokemon );
                        pokemon.AddStatusEvent( $"{pokemon.NickName}'s Limber cures its paralysis!" );
                        pokemon.CureSevereStatus();
                    }
                },
            }  
        },
        {
            AbilityID.WaterVeil, new()
            {
                Name = "Water Veil",
                Description = "Prevents the Pokemon from being burned.",

                OnTrySetSevereStatus = ( SevereConditionID id, Pokemon pokemon, StatusEffectSource StatusEffectSource ) =>
                {
                    if( id == SevereConditionID.BRN )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( pokemon );
                        pokemon.AddStatusEvent( $"{pokemon.NickName}'s Water Veil prevents it from being burned!" );

                        return false;
                    }
                    else
                        return true;
                },

                OnAbilityEnter = ( Pokemon pokemon, List<BattleUnit> opponents, Battlefield field ) =>
                {
                    if( pokemon.SevereStatus?.ID == SevereConditionID.BRN )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( pokemon );
                        pokemon.AddStatusEvent( $"{pokemon.NickName}'s Water Veil cures its Burn!" );
                        pokemon.CureSevereStatus();
                    }
                },
            }  
        },
        {
            AbilityID.OwnTempo, new()
            {
                Name = "Own Tempo",
                Description = "Prevents the Pokemon from being confused.",

                OnTrySetVolatileStatus = ( VolatileConditionID id, Pokemon pokemon, StatusEffectSource StatusEffectSource ) =>
                {
                    if( id == VolatileConditionID.Confusion )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( pokemon );
                        pokemon.AddStatusEvent( $"{pokemon.NickName}'s Own Tempo prevents it from being confused!" );

                        return false;
                    }
                    else
                        return true;
                },

                OnAbilityEnter = ( Pokemon pokemon, List<BattleUnit> opponents, Battlefield field ) =>
                {
                    if( pokemon.VolatileStatuses.ContainsKey( VolatileConditionID.Confusion ) )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( pokemon );
                        pokemon.AddStatusEvent( $"{pokemon.NickName}'s Own Tempo cures its confusion!" );
                        pokemon.CureSevereStatus();
                    }
                },
            }  
        },
        {
            AbilityID.RoughSkin, new()
            {
                Name = "Rough Skin",
                Description = "The Pokemon's rough skin damages attackers that make direct contact with it.",

                OnMoveContact = ( BattleUnit attacker, BattleUnit target, Move move ) =>
                {
                    if( move.MoveSO.HasFlag( MoveFlags.Contact ) )
                    {
                        int damage = Mathf.FloorToInt( attacker.Pokemon.MaxHP / 8 );
                        attacker.Pokemon.DecreaseHP( damage );
                        attacker.Pokemon.AddStatusEvent( StatusEventType.Damage, $"{attacker.Pokemon.NickName} is hurt by {target.Pokemon.NickName}'s Rough Skin" );
                    }
                },
            }
        },
        {
            AbilityID.FlameBody, new()
            {
                Name = "Flame Body",
                Description = "Contact with the Pokemon may burn the attacker.",

                OnMoveContact = ( BattleUnit attacker, BattleUnit target, Move move ) =>
                {
                    if( move.MoveSO.HasFlag( MoveFlags.Contact ) && Random.Range( 1, 101 ) <= 30 )
                    {
                        StatusEffectSource source = new()
                        {
                            Pokemon = target.Pokemon,
                            Source = EffectSource.Ability,
                        };

                        attacker.Pokemon.SetSevereStatus( SevereConditionID.BRN, source );
                    }
                },
            }
        },
        {
            AbilityID.PoisonPoint, new()
            {
                Name = "Poison Point",
                Description = "Contact with the Pokemon may poison the attacker.",

                OnMoveContact = ( BattleUnit attacker, BattleUnit target, Move move ) =>
                {
                    if( move.MoveSO.HasFlag( MoveFlags.Contact ) && Random.Range( 1, 101 ) <= 30 )
                    {
                        StatusEffectSource source = new()
                        {
                            Pokemon = target.Pokemon,
                            Source = EffectSource.Ability,
                        };

                        attacker.Pokemon.SetSevereStatus( SevereConditionID.PSN, source );
                    }
                },
            }
        },
        {
            AbilityID.Static, new()
            {
                Name = "Static",
                Description = "The Pokemon is charged with static electricity and may paralyze attackers that make direct contact with it.",

                OnMoveContact = ( BattleUnit attacker, BattleUnit target, Move move ) =>
                {
                    if( move.MoveSO.HasFlag( MoveFlags.Contact ) && Random.Range( 1, 101 ) <= 30 )
                    {
                        StatusEffectSource source = new()
                        {
                            Pokemon = target.Pokemon,
                            Source = EffectSource.Ability,
                        };

                        attacker.Pokemon.SetSevereStatus( SevereConditionID.PAR, source );
                    }
                },
            }
        },
        {
            AbilityID.Adaptability, new()
            {
                Name = "Adaptability",
                Description = "Powers up moves of the same type as the Pokemon.",

                OnSTABModify = ( Pokemon pokemon, Move move ) =>
                {
                    if( pokemon.CheckTypes( move.MoveType ) )
                        return 2f;
                    else
                        return 1f;
                },
            }
        },
        {
            AbilityID.Pixilate, new()
            {
                Name = "Pixilate",
                Description = "Normal-type moves become Fairy-type moves. The power of those moves is boosted by 1.2x.",

                OnAbilityEnter = ( Pokemon pokemon, List<BattleUnit> targets, Battlefield field ) =>
                {
                    foreach( var move in pokemon.ActiveMoves )
                    {
                        if( move.MoveSO.Type == PokemonType.Normal )
                        {
                            move.OverrideMoveType( PokemonType.Fairy );
                            move.OverrideMovePower( Mathf.FloorToInt( move.MoveSO.Power * 1.2f ) );
                        }
                    }
                },

                OnAbilityExit = ( Pokemon pokemon, List<BattleUnit> targets, Battlefield field ) =>
                {
                    foreach( var move in pokemon.ActiveMoves )
                    {
                        if( move.MoveSO.Type == PokemonType.Normal && move.MoveType == PokemonType.Fairy)
                        {
                            move.OverrideMoveType( PokemonType.Normal );
                            move.OverrideMovePower( Mathf.FloorToInt( move.MoveSO.Power ) );
                        }
                    }
                },
            }
        },
        {
            AbilityID.Burninate, new()
            {
                Name = "Burninate",
                Description = "Normal-type moves become Fire-type moves. The power of those moves is boosted by 1.2x.",

                OnAbilityEnter = ( Pokemon pokemon, List<BattleUnit> targets, Battlefield field ) =>
                {
                    foreach( var move in pokemon.ActiveMoves )
                    {
                        if( move.MoveSO.Type == PokemonType.Normal )
                        {
                            move.OverrideMoveType( PokemonType.Fire );
                            move.OverrideMovePower( Mathf.FloorToInt( move.MoveSO.Power * 1.2f ) );
                        }
                    }
                },

                OnAbilityExit = ( Pokemon pokemon, List<BattleUnit> targets, Battlefield field ) =>
                {
                    foreach( var move in pokemon.ActiveMoves )
                    {
                        if( move.MoveSO.Type == PokemonType.Normal && move.MoveType == PokemonType.Fire)
                        {
                            move.OverrideMoveType( PokemonType.Normal );
                            move.OverrideMovePower( Mathf.FloorToInt( move.MoveSO.Power ) );
                        }
                    }
                },
            }
        },
        {
            AbilityID.Electrify, new()
            {
                Name = "Electrify",
                Description = "Normal-type moves become Electric-type moves. The power of those moves is boosted by 1.2x.",

                OnAbilityEnter = ( Pokemon pokemon, List<BattleUnit> targets, Battlefield field ) =>
                {
                    foreach( var move in pokemon.ActiveMoves )
                    {
                        if( move.MoveSO.Type == PokemonType.Normal )
                        {
                            move.OverrideMoveType( PokemonType.Electric );
                            move.OverrideMovePower( Mathf.FloorToInt( move.MoveSO.Power * 1.2f ) );
                        }
                    }
                },

                OnAbilityExit = ( Pokemon pokemon, List<BattleUnit> targets, Battlefield field ) =>
                {
                    foreach( var move in pokemon.ActiveMoves )
                    {
                        if( move.MoveSO.Type == PokemonType.Normal && move.MoveType == PokemonType.Fire)
                        {
                            move.OverrideMoveType( PokemonType.Normal );
                            move.OverrideMovePower( Mathf.FloorToInt( move.MoveSO.Power ) );
                        }
                    }
                },
            }
        },
        {
            AbilityID.SereneGrace, new()
            {
                Name = "Serene Grace",
                Description = "Raises the likelihood of additional effects occurring when the Pokemon uses its moves.",

                OnSecondaryEffectChanceModify = () =>
                {
                    return 2f;
                }
            }
        },
        {
            AbilityID.SolarPower, new()
            {
                Name = "Solar Power",
                Description = "In harsh sunlight, the Pokemon's Sp. Atk stat is boosted, but its HP decreases every turn.",

                //--Stat Modifiers applied in WeatherConditionDB

                OnAbilityAfterTurn = ( BattleUnit attacker, Battlefield field ) =>
                {
                    if( field.Weather?.ID == WeatherConditionID.SUNNY && field.WeatherDuration > 1 )
                    {
                        attacker.Pokemon.DecreaseHP( Mathf.FloorToInt( attacker.Pokemon.MaxHP / 8 ) );
                    }
                },
            }
        },
        {
            AbilityID.MagicBounce, new()
            {
                Name = "Magic Bounce",
                Description = "The Pokemon reflects status moves instead of getting hit by them.",

                //--Ability's effect is handled for in PerformMoveCommand() in the Battle System.
                //--If a status move targets a Pokemon with this ability, it switches the target to the attacker.
            }
        },
        {
            AbilityID.ThickFat, new()
            {
                Name = "Thick Fat",
                Description = "The Pokemon is protected by a layer of thick fat, which halves the damage taken from Fire- and Ice-type moves.",

                OnModifyTakeDamage = ( float atk, Pokemon attacker, Pokemon target, Move move ) =>
                {
                    if( move.MoveType == PokemonType.Fire || move.MoveType == PokemonType.Ice )
                    {
                        Debug.Log( "Thick Fat is active!" );
                        BattleSystem.Instance.TriggerAbilityCutIn( target );
                        if( move.MoveSO.MoveCategory == MoveCategory.Physical )
                            atk = attacker.Attack * 0.5f;
                        else if( move.MoveSO.MoveCategory == MoveCategory.Special )
                            atk = attacker.SpAttack * 0.5f;
                    }

                    return atk;
                }
            }
        },
        {
            AbilityID.Prankster, new()
            {
                Name = "Prankster",
                ID = AbilityID.Prankster,
                Description = "Gives priority to the Pokemon's status moves.",
                //--Ability's Effect is handled in UseMoveCommand
            }
        },
        {
            AbilityID.Triage, new()
            {
                Name = "Triage",
                ID = AbilityID.Triage,
                Description = "Gives priority to the Pokemon's healing moves. ",
                //--Ability's Effect is handled in UseMoveCommand
            }
        },
        {
            AbilityID.Levitate, new()
            {
                Name = "Levitate",
                ID = AbilityID.Levitate,
                Description = "By floating in the air, the Pokemon receives full immunity to all Ground-type moves.",
                //--Ability's Effect is handled in MoveSuccess & BattleFlags for Grounded
            }
        },
        {
            AbilityID.GrassySurge, new()
            {
                Name = "Grassy Surge",
                ID = AbilityID.GrassySurge,
                Description = "Turns the ground into Grassy Terrain when the Pokemon enters a battle.",
                
                OnAbilityEnter = ( Pokemon attacker, List<BattleUnit> targets, Battlefield battleField ) =>
                {
                    if( battleField.Terrain?.ID != TerrainID.Grassy )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        Debug.Log( "Setting Terrain: Grassy Terrain" );
                        battleField.SetTerrain( TerrainID.Grassy );
                    }
                }
            }
        },
        {
            AbilityID.PsychicSurge, new()
            {
                Name = "Psychic Surge",
                ID = AbilityID.PsychicSurge,
                Description = "Turns the ground into Psychic Terrain when the Pokemon enters a battle.",
                
                OnAbilityEnter = ( Pokemon attacker, List<BattleUnit> targets, Battlefield battleField ) =>
                {
                    if( battleField.Terrain?.ID != TerrainID.Psychic )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        Debug.Log( "Setting Terrain: Psychic Terrain" );
                        battleField.SetTerrain( TerrainID.Psychic );
                    }
                }
            }
        },
        {
            AbilityID.SandVeil, new()
            {
                Name = "Sand Veil",
                ID = AbilityID.SandVeil,
                Description = "Boosts the Pokemon's evasiveness in a sandstorm.",
                //--This ability is handled in Sandstorm's WeatherDB entry.
            }
        },
        {
            AbilityID.Oblivious, new()
            {
                Name = "Oblivious",
                ID = AbilityID.Oblivious,
                Description = "The Pokemon is oblivious, keeping it from being infatuated, falling for taunts, or being affected by either Demoralize or Intimidate.",
                //--This ability will be handled in the code for infatuation, taunt, demoralize, and intimidate.
            }
        },
        {
            AbilityID.Competitive, new()
            {
                Name = "Competitive",
                ID = AbilityID.Competitive,
                Description = "Boosts the Pokemon's Sp. Atk stat sharply when its stats are lowered by an opposing Pokemon.",

                OnAfterStatStageChange = ( Dictionary<Stat, int> stages, Pokemon attacker, Pokemon target ) =>
                {
                    if( attacker != null && attacker == target )
                    {
                        Debug.Log( "Self stat stage change triggered!" );
                        return;
                    }

                    List<StatStage> competitiveBoost = new() { new(){ Stat = Stat.SpAttack, Change = 2 } };
                    Debug.Log( $"[Ability] Competitive Triggered! " );
                    foreach( var kvp in stages )
                    {
                        var stat = kvp.Key;
                        var change = kvp.Value;

                        if( change < 0 )
                        {
                            StageChangeSource source = new()
                            {
                                Pokemon = attacker,
                                MoveName = string.Empty,
                                Source = StageChangeSourceType.Ability,
                            };

                            target.ApplyStatStageChange( competitiveBoost, source );
                        }
                    }
                },
            }
        },
        {
            AbilityID.Defiant, new()
            {
                Name = "Defiant",
                ID = AbilityID.Defiant,
                Description = "Boosts the Pokemon's Atk stat sharply when its stats are lowered by an opposing Pokemon.",

                OnAfterStatStageChange = ( Dictionary<Stat, int> stages, Pokemon attacker, Pokemon target ) =>
                {
                    if( attacker != null && attacker == target )
                    {
                        Debug.Log( "Self stat stage change triggered!" );
                        return;
                    }

                    List<StatStage> defiantBoost = new() { new(){ Stat = Stat.Attack, Change = 2 } };
                    Debug.Log( $"[Ability] Defiant Triggered! " );
                    foreach( var kvp in stages )
                    {
                        var stat = kvp.Key;
                        var change = kvp.Value;

                        if( change < 0 )
                        {
                            StageChangeSource source = new()
                            {
                                Pokemon = attacker,
                                MoveName = string.Empty,
                                Source = StageChangeSourceType.Ability,
                            };

                            target.ApplyStatStageChange( defiantBoost, source );
                        }
                    }
                },
            }
        },
        {
            AbilityID.MirrorArmor, new()
            {
                // If multiple Pokemon, including the Pokemon with this Ability, would have their stats lowered by the same effect,
                // only the stat drop that would have applied to the Pokemon with this Ability is reflected. If the Pokemon with this Ability is affected by Sticky Web,
                // the effect is reflected back to the Pokemon which set it up. If Pokemon which set up Sticky Web is not on the field, no Pokemon have their Speed lowered. 
                //--Guess i need to add a signature to sticky web once it's implemented or something lol. a simple public Pokemon Setter { get; set; } in CourtEffects is probably fine. 02/06/26

                Name = "MirrorArmor",
                ID = AbilityID.MirrorArmor,
                Description = "Bounces back only the stat-lowering effects that the Pokemon receives. ",

                OnStatStageChange = ( Dictionary<Stat, int> stages, Pokemon attacker, Pokemon target ) =>
                {
                    Debug.Log( $"Mirror Armor triggered! Attacker: {attacker.NickName}, Target: {target.NickName}" );
                    //--If Self-Boost, return
                    if( attacker != null && attacker == target )
                    {
                        Debug.Log( "Self-boost triggered!" );
                        return;
                    }

                    bool stageReflected = false;
                    var statstoRemove = new List<Stat>();
                    List<StatStage> reflect = new();
                    foreach( var kvp in stages )
                    {
                        if( kvp.Value < 0 )
                        {
                            statstoRemove.Add( kvp.Key );
                            StatStage change = new(){ Stat = kvp.Key, Change = kvp.Value };
                            reflect.Add( change );
                        }
                    }

                    foreach( var stat in statstoRemove )
                    {
                        stages.Remove( stat );
                        stageReflected = true;
                    }

                    if( stageReflected )
                    {
                        StageChangeSource source = new()
                        {
                            Pokemon = target,
                            MoveName = string.Empty,
                            Source = StageChangeSourceType.Ability,
                        };

                        BattleSystem.Instance.TriggerAbilityCutIn( target );
                        attacker.ApplyStatStageChange( reflect, source );
                    }

                },
            }
        },
        {
            AbilityID.CuteCharm, new()
            {
                Name = "Cute Charm",
                Description = "The Pokemon may infatuate attackers that make direct contact with it.",
                ID = AbilityID.CuteCharm,

                OnMoveContact = ( BattleUnit attacker, BattleUnit target, Move move ) =>
                {
                    if( move.MoveSO.HasFlag( MoveFlags.Contact ) && Random.Range( 1, 101 ) <= 30 ) //--And if the genders are not the same!
                    {
                        // int duration = -1; //-- -1 will be for infinite duration. I need to make sure i implement it so that we only decrease/increase timer on a counter > 0
                        StatusEffectSource source = new()
                        {
                            Pokemon = target.Pokemon,
                            Source = EffectSource.Ability,
                        };

                        attacker.Pokemon.SetVolatileStatus( VolatileConditionID.Infatuation, source );
                        var bs = BattleSystem.Instance;
                        var field = bs.Field;
                        var infatuation = target.Pokemon.VolatileStatuses[VolatileConditionID.Infatuation].Condition;

                        infatuation?.OnApplyStatus?.Invoke( attacker, target, bs );
                    }
                },
            }
        },
        {
            AbilityID.NaturalCure, new()
            {
                Name = "Natural Cure",
                Description = "The Pokemon's severe status conditions are cured when it switches out.",
                ID = AbilityID.NaturalCure,

                OnAbilityExit = ( Pokemon pokemon, List<BattleUnit> opponents, Battlefield field ) =>
                {
                    pokemon.CureSevereStatus();
                },
            }
        },
        {
            AbilityID.LeafGuard, new()
            {
                Name = "Leaf Guard",
                Description = "Prevents all status conditions in harsh sunlight.",
                ID = AbilityID.LeafGuard,

                OnTrySetSevereStatus = ( SevereConditionID status, Pokemon pokemon, StatusEffectSource source ) =>
                {
                    if( BattleSystem.Instance.Field.Weather?.ID == WeatherConditionID.SUNNY )
                        return false;
                    else
                        return true;
                },

                OnTrySetVolatileStatus = ( VolatileConditionID status, Pokemon pokemon, StatusEffectSource source ) =>
                {
                    if( BattleSystem.Instance.Field.Weather?.ID == WeatherConditionID.SUNNY && source.Pokemon != pokemon )
                        return false;
                    else
                        return true;
                },

                OnTrySetTransientStatus = ( TransientConditionID status, Pokemon pokemon, StatusEffectSource source ) =>
                {
                    if( BattleSystem.Instance.Field.Weather?.ID == WeatherConditionID.SUNNY && source.Pokemon != pokemon )
                        return false;
                    else
                        return true;
                },

                OnTrySetBindingStatus = ( BindingConditionID status, Pokemon pokemon, StatusEffectSource source ) =>
                {
                    if( BattleSystem.Instance.Field.Weather?.ID == WeatherConditionID.SUNNY )
                        return false;
                    else
                        return true;
                },

            }
        },
        {
            AbilityID.Technician, new()
            {
                Name = "Technician",
                Description = "Powers up weak moves so the Pokémon can deal more damage with them.",
                ID = AbilityID.Technician,

                OnMoveUsed = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                {
                    if( move.MovePower > 60 )
                        return;
                    
                    int power = Mathf.FloorToInt( move.MovePower * 1.5f );
                    move.OverrideMovePower( power );
                },

                OnMoveCompleted = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                {
                    if( move.MovePower != move.MoveSO.Power )
                        move.OverrideMovePower( move.MoveSO.Power );
                },
            }
        },
        {
            AbilityID.Pressure, new()
            {
                Name = "Pressure",
                Description = "Puts other Pokémon under pressure, causing them to expend more PP to use their moves.",
                ID = AbilityID.Pressure,

                //--Code for this ability is handled when pp is reduced in BattleCommandCenter.PerformMoveCommand();
                OnAbilityEnter = ( Pokemon pokemon, List<BattleUnit> opps, Battlefield field ) =>
                {
                    BattleSystem.Instance.AddDialogue( $"{pokemon.NickName} is exerting its Pressure!" );
                }
            }
        },
        {
            AbilityID.Infiltrator, new()
            {
                Name = "Infiltrator",
                Description = "The Pokémon's moves are unaffected by the target's barriers, substitutes, and the like.",
                ID = AbilityID.Infiltrator,

                //--Code for this ability is handled in BattleUnit.TakeDamage();
            }
        },
        {
            AbilityID.DesecratedGround, new()
            {
                Name = "Desecrated Ground",
                Description = "Turns the ground into Blighted Terrain when the Pokemon enters a battle.",
                
                OnAbilityEnter = ( Pokemon attacker, List<BattleUnit> targets, Battlefield battleField ) =>
                {
                    if( battleField.Terrain?.ID != TerrainID.Blighted )
                    {
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        Debug.Log( "Setting Terrain: Blighted Terrain" );
                        battleField.SetTerrain( TerrainID.Blighted );
                    }
                }
            }
        },
        {
            AbilityID.Steadfast, new()
            {
                Name = "Steadfast",
                Description = "The Pokemon's determination boosts its Speed stat every time it flinches. ",

                //--This ability will be handled in Flinch's OnBeforeTurn
            }
        },
        {
            AbilityID.InnerFocus, new()
            {
                Name = "Inner Focus",
                Description = "The Pokemon's intense focus prevents it from flinching or being affected by both Intimidate and Demoralize.",

                //--This ability is handled in canapply for flinch, and in both Intimidate and Demoralize
            }
        },
        {
            AbilityID.Justified, new()
            {
                Name = "Justified",
                Description = "When the Pokemon is hit by a Dark-type attack, its Attack stat is boosted by its sense of justice.",

                OnTakeDamage = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                {
                    if( move.MoveType == PokemonType.Dark )
                    {
                        List<StatStage> attackBoost = new() { new() { Stat = Stat.Attack, Change = 1 } };
                        StageChangeSource source = new() { Pokemon = target.Pokemon, MoveName = move.MoveSO.Name, Source = StageChangeSourceType.Ability };

                        target.Pokemon.ApplyStatStageChange( attackBoost, source );
                    }
                }
            }
        },
        {
            AbilityID.Hustle, new()
            {
                Name = "Hustle",
                Description = "Boosts the Pokemon's Attack stat but lowers its accuracy.", // accuracy multiplier 3277/4096

                OnAbilityEnter = ( attacker, opponents, field ) =>
                {
                    attacker.ApplyDirectStatModifier( Stat.Attack, DirectModifierCause.Hustle, 1.5f );
                },

                OnAbilityExit = ( attacker, opponents, field ) =>
                {
                    attacker.RemoveDirectStatModifier( Stat.Attack, DirectModifierCause.Hustle );
                },

                OnModify_ACC = ( acc, attacker, target, move ) =>
                {

                    if( move.MoveSO.MoveCategory == MoveCategory.Physical )
                        return acc * ( 3277/4096 );
                    else
                        return acc;
                },
            }
        },
        {
            AbilityID.Superluck, new()
            {
                Name = "Superluck",
                Description = "The Pokemon is so lucky that the critical-hit ratios of its moves are boosted.",

                //--Crit Ratio stuff not implemented at all. Gotta figure this out soon...
            }
        }

    };
}

public enum AbilityID
{
    None,

//--1/4 HP ATK Boost
    Blaze,
    Torrent,
    Overgrow,
    Swarm,

//--Status-based Stat Change
    Guts,
    MarvelScale,
    QuickFeet,
    CompoundEyes,

//--Prevents stat stage from being lowered (not direct stat changes)
    KeenEye,
    HyperCutter,
    BigPecks,
    ClearBody,
    WhiteSmoke,

//--Causes a Stat Stage to be lowered
    Intimidate,
    Demoralize,

//--Causes a Direct Stat Change in Weather
    Chlorophyll,
    SwiftSwim,
    SandRush,
    SlushRush,

//--Sets Weather---------------------------
    Drought,
    Drizzle,
    SnowWarning,
    Sandstream,

    SheerForce,

    Insomnia,
    VitalSpirit,
    Immunity,
    Limber,
    WaterVeil,
    OwnTempo,

//--Chance to cause a status on Contact
    FlameBody,
    PoisonPoint,
    Static,

//--Move Type Changing Abilities
    Pixilate,
    LiquidVoice,
    Burninate,
    Electrify,

//--Adaptability
    Adaptability,
    SereneGrace,
    SolarPower,
    MagicBounce,
    ThickFat,
    Prankster,
    Triage,
    Levitate,
    GrassySurge,
    PsychicSurge,
    RoughSkin,
    SandVeil,
    EarthPower,
    Oblivious, //--Immune to taunt, infatuation, and intimidate (and demoralize)
    Competitive,
    Defiant,
    CuteCharm,
    MirrorArmor,
    NaturalCure,
    Technician,
    LeafGuard,
    Pressure,
    Infiltrator,
    DesecratedGround,
    StickyHold,
    Steadfast,
    InnerFocus,
    Justified,
    Soundproof,
    Hustle,
    Superluck,

}
