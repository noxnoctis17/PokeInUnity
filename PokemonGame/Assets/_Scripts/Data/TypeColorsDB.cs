using System.Collections.Generic;
using UnityEngine;

public class TypeColorsDB
{
    public static Dictionary<PokemonType, ( Color PrimaryColor, Color SecondaryColor )> TypeColors { get; set; }

    public static void Init(){
        SetDictionary();
    }

    public static void Clear(){
        TypeColors = null;
    }

    private static void SetDictionary(){
         TypeColors = new()
         {
             //--Normal
             { PokemonType.Normal, ( new Color32( 159, 161, 159, 255 ), new Color32( 172, 156, 134, 255 ) ) },

             //--Fire
             { PokemonType.Fire, ( new Color32( 229, 97, 62, 255 ), new Color32( 230, 40, 41, 255 ) ) },

             //--Water
             { PokemonType.Water, ( new Color32( 41, 128, 239, 255 ), new Color32( 105, 146, 243, 255 ) ) },

             //--Electric

             //--Grass
             { PokemonType.Grass, ( new Color32( 135, 185, 80, 255 ), new Color32( 233, 232, 101, 255 ) ) },

             //--Ice

             //--Fighting
             { PokemonType.Fighting, ( new Color32( 255, 128, 0, 255 ), new Color32( 206, 64, 105, 255 ) ) },

             //--Poison

             //--Ground

             //--Flying
             { PokemonType.Flying, ( new Color32( 129, 185, 239, 255 ), new Color32( 152, 216, 216, 255 ) ) },

             //--Psychic

             //--Bug

             //--Rock

             //--Ghost

             //--Dragon

             //--Dark

             //--Steel
             { PokemonType.Steel, ( new Color32( 96, 161, 184, 255 ), new Color32( 191, 191, 224, 255 ) ) },

             //--Fairy
             { PokemonType.Fairy, ( new Color32( 252, 131, 216, 255 ), new Color32( 221, 155, 178, 255 ) ) }

         };
    }
}
