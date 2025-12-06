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

                OnModify_ATK = ( float atk, Pokemon attacker, Pokemon target, Move move ) =>
                {
                    if( attacker.SevereStatus != null )
                    {
                        Debug.Log( "Guts is active!" );
                        BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                        atk = atk * 1.5f;
                    }

                    return atk;
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
                    //--If Self-Boost, return
                    if( attacker != null && attacker == target )
                        return;

                    bool stageChangePrevented = false;
                    foreach( var stat in stages.Keys )
                    {
                        if( stages[stat] < 0 )
                        {
                            Debug.Log( "White Smoke prevented a stat stage from being lowered!" );
                            stages.Remove( stat );
                            stageChangePrevented = true;
                        }
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

                OnAbilityEnter = ( Pokemon attacker, List<BattleUnit> targets, Battlefield field ) =>
                {
                    List<StatStage> statStages = new();
                    StatStage stage = new();
                    stage.Stat = Stat.Attack;
                    stage.Change = -1;
                    statStages.Add( stage );

                    BattleSystem.Instance.TriggerAbilityCutIn( attacker );
                    foreach( var target in targets )
                    {
                        Debug.Log( $"Intimidate Fired! {target.Pokemon.NickName}'s ATK before intimidate: {target.Pokemon.Attack}" );
                        target.Pokemon.ApplyStatStageChange( statStages, attacker );
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
                    foreach( var target in targets )
                    {
                        Debug.Log( $"Demoralize Fired! {target.Pokemon.NickName}'s Sp.ATK before Demoralize: {target.Pokemon.SpAttack}" );
                        target.Pokemon.ApplyStatStageChange( statStages, attacker );
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
                    // BattleSystem.Instance.TriggerAbilityCutIn( pokemon ); //--This shit pops up every turn the weather is active, now i know why cart doesn't do this lol. i'll use it for the hud ability display if i implement that.
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
                    // BattleSystem.Instance.TriggerAbilityCutIn( pokemon ); //--This shit pops up every turn the weather is active, now i know why cart doesn't do this lol. i'll use it for the hud ability display if i implement that.
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
                    // BattleSystem.Instance.TriggerAbilityCutIn( pokemon ); //--This shit pops up every turn the weather is active, now i know why cart doesn't do this lol. i'll use it for the hud ability display if i implement that.
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
                    // BattleSystem.Instance.TriggerAbilityCutIn( pokemon ); //--This shit pops up every turn the weather is active, now i know why cart doesn't do this lol. i'll use it for the hud ability display if i implement that.
                },
            }
        },

    };







    //----------End Database
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


}
