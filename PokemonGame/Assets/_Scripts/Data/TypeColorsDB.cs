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

             //--Fire

             //--Water

             //--Electric

             //--Grass
             { PokemonType.Grass, ( new Color32( 135, 185, 80, 255 ), new Color32( 233, 232, 101, 255 ) ) },

             //--Ice

             //--Fighting

             //--Poison

             //--Ground

             //--Flying

             //--Psychic

             //--Bug

             //--Rock

             //--Ghost

             //--Dragon

             //--Dark

             //--Steel

             //--Fairy
             { PokemonType.Fairy, ( new Color32( 252, 131, 216, 255 ), new Color32( 221, 155, 178, 255 ) ) }
         };
    }
}
