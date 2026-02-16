using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class BindingConditionsDB
{
    public static Dictionary<BindingConditionID, BindingCondition> Conditions { get; set; } 

    public static void Init()
    {
        SetDictionary();

        foreach( var kvp in Conditions )
        {
            var conditionID = kvp.Key;
            var condition = kvp.Value;

            condition.ID = conditionID;
        }
    }

    public static void Clear(){
        Conditions = null;
    }

    private static void BindingOnStart( Pokemon pokemon, BindingConditionID id )
    {
        int random = Random.Range( 4, 6 );
        var status = pokemon.BindingStatuses[id];
        status.Duration = random;

        pokemon.BindingStatuses[id] = status;

        var unit = BattleSystem.Instance.GetPokemonBattleUnit( pokemon );
        unit.SetUnitTrapped( true );
    }

    private static void BindingOnAfterTurn( Pokemon pokemon, BindingConditionID id, string freedText, string hurtText )
    {
        string statusName = Regex.Replace( id.ToString(), "(?<=[a-z])([A-Z])", " $1");
        Debug.Log( $"{pokemon.NickName}'s {statusName} Counter is: {pokemon.BindingStatuses[id].Duration}" );

        if( pokemon.BindingStatuses[id].Duration == 0 )
        {
            pokemon.CureBindingStatus();
            pokemon.AddStatusEvent( $"{freedText}" );
            var unit = BattleSystem.Instance.GetPokemonBattleUnit( pokemon );
            unit.SetUnitTrapped( false );
        }
        else
        {
            float damage = pokemon.MaxHP / 8;

            if( id == BindingConditionID.AcidTrap )
            {
                float effectiveness = TypeChart.GetEffectiveness( PokemonType.Poison, pokemon.PokeSO.Type1 ) * TypeChart.GetEffectiveness( PokemonType.Poison, pokemon.PokeSO.Type2 );
                damage *= effectiveness;
            }

            pokemon.DecreaseHP( Mathf.FloorToInt( damage ) );
            pokemon.AddStatusEvent( StatusEventType.Damage, $"{hurtText}" );

            var status = pokemon.BindingStatuses[id];
            status.Duration--;
            pokemon.BindingStatuses[id] = status;
        }
    }

    private static void SetDictionary(){
        Conditions = new Dictionary<BindingConditionID, BindingCondition>()
        {
            {
                BindingConditionID.FireSpin, new()
                {
                    Name = "Fire Spin",
                    StartMessage = "was trapped by Fire Spin!",

                    OnStart = ( Pokemon pokemon ) => BindingOnStart( pokemon, BindingConditionID.FireSpin ),

                    OnAfterTurn = ( Pokemon pokemon ) => BindingOnAfterTurn( pokemon, BindingConditionID.FireSpin, $"{pokemon.NickName} was freed from Fire Spin!", "was singed by the rolling flames of Fire Spin!" ),
                }
            },
            {
                BindingConditionID.Whirlpool, new()
                {
                    Name = "Whirlpool",
                    StartMessage = "was trapped by Whirlpool!",

                    OnStart = ( Pokemon pokemon ) => BindingOnStart( pokemon, BindingConditionID.Whirlpool ),

                    OnAfterTurn = ( Pokemon pokemon ) => BindingOnAfterTurn( pokemon, BindingConditionID.Whirlpool, $"{pokemon.NickName} was freed from Whirlpool!", "was pelted by the twisting waters of Whirlpool!" ),
                }
            },
            {
                BindingConditionID.SandTomb, new()
                {
                    Name = "Sand Tomb",
                    StartMessage = "was trapped by Sand Tomb!",

                    OnStart = ( Pokemon pokemon ) => BindingOnStart( pokemon, BindingConditionID.SandTomb ),

                    OnAfterTurn = ( Pokemon pokemon ) => BindingOnAfterTurn( pokemon, BindingConditionID.Whirlpool, $"{pokemon.NickName} was freed from Sand Tomb!", "was buffeted by the swirling sands of Sand Tomb!" ),
                }
            },
            {
                BindingConditionID.Bind, new()
                {
                    Name = "Bind",
                    StartMessage = "was trapped by Bind!",

                    OnStart = ( Pokemon pokemon ) => BindingOnStart( pokemon, BindingConditionID.Bind ),

                    OnAfterTurn = ( Pokemon pokemon ) => BindingOnAfterTurn( pokemon, BindingConditionID.Bind, $"{pokemon.NickName} was freed from Bind!", "was hurt from the Bind!" ),
                }
            },
            {
                BindingConditionID.Wrap, new()
                {
                    Name = "Wrap",
                    StartMessage = "was trapped by Wrap!",

                    OnStart = ( Pokemon pokemon ) => BindingOnStart( pokemon, BindingConditionID.Wrap ),

                    OnAfterTurn = ( Pokemon pokemon ) => BindingOnAfterTurn( pokemon, BindingConditionID.Wrap, $"{pokemon.NickName} was freed from Wrap!", "was squeezed by Wrap!" ),
                }
            },
            {
                BindingConditionID.AcidTrap, new()
                {
                    Name = "Acid Trap",
                    StartMessage = "was trapped by PoisonTrap!",

                    OnStart = ( Pokemon pokemon ) => BindingOnStart( pokemon, BindingConditionID.AcidTrap ),

                    OnAfterTurn = ( Pokemon pokemon ) => BindingOnAfterTurn( pokemon, BindingConditionID.AcidTrap, $"{pokemon.NickName} was freed from Acid Trap!", "was stung by the Acid Trap!" ),
                }
            },
        };
    }
}

public enum BindingConditionID
{
    None,
    FireSpin,
    Whirlpool,
    SandTomb,
    Bind,
    Wrap,
    AcidTrap,
}
