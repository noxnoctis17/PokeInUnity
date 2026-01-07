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

    public static ( Color color1, Color color2 ) GetColors( Pokemon pokemon )
    {
        Color color1 = TypeColors[pokemon.PokeSO.Type1].PrimaryColor;
        Color color2;

        if( pokemon.PokeSO.Type2 != PokemonType.None )
            color2 = TypeColors[pokemon.PokeSO.Type2].SecondaryColor;
        else
            color2 = TypeColors[pokemon.PokeSO.Type1].SecondaryColor;

        return ( color1, color2 );
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
             { PokemonType.Electric, ( new Color32( 250, 193, 0, 255), new Color32( 84, 76, 45, 255 ) ) },

             //--Grass
             { PokemonType.Grass, ( new Color32( 135, 185, 80, 255 ), new Color32( 233, 232, 101, 255 ) ) },

             //--Ice

             //--Fighting
             { PokemonType.Fighting, ( new Color32( 255, 128, 0, 255 ), new Color32( 206, 64, 105, 255 ) ) },

             //--Poison
             { PokemonType.Poison, ( new Color32( 167, 102, 183, 255 ), new Color32( 109, 56, 131, 255 ) ) },

             //--Ground

             //--Flying
             { PokemonType.Flying, ( new Color32( 129, 185, 239, 255 ), new Color32( 152, 216, 216, 255 ) ) },

             //--Psychic
             { PokemonType.Psychic, ( new Color32( 240, 83, 127, 255 ), new Color32( 184, 45, 84, 255 ) ) },

             //--Bug

             //--Rock
             { PokemonType.Rock, ( new Color32( 94, 76, 31, 255 ), new Color32( 58, 35, 29, 255 ) ) },

             //--Ghost

             //--Dragon

             //--Dark
             { PokemonType.Dark, ( new Color32( 80, 65, 63, 255 ), new Color32( 47, 42, 56, 255 ) ) },

             //--Steel
             { PokemonType.Steel, ( new Color32( 96, 161, 184, 255 ), new Color32( 191, 191, 224, 255 ) ) },

             //--Fairy
             { PokemonType.Fairy, ( new Color32( 252, 131, 216, 255 ), new Color32( 221, 155, 178, 255 ) ) }

         };
    }
}
