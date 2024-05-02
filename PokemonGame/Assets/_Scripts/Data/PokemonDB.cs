using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class PokemonDB
{
    private static Dictionary<PokemonSpecies, PokemonSO> _pokemonSpeciesDB;

    public static void Init(){
        _pokemonSpeciesDB = new();

        var dbArray = Resources.LoadAll<PokemonSO>( "" );
        foreach( var pokeSO in dbArray ){
            if( _pokemonSpeciesDB.ContainsKey( pokeSO.Species ) ){
                Debug.LogError( "Duplicate Pokemon Species" );
                continue;
            }

            _pokemonSpeciesDB[pokeSO.Species] = pokeSO;

        }
    }

    public static PokemonSO GetPokemonBySpecies( PokemonSpecies species ){
        if( !_pokemonSpeciesDB.ContainsKey( species ) ){
            Debug.LogError( "Pokemon not found in Pokemon Database!" );
            return null;
        }
        
        return _pokemonSpeciesDB[species];
    }

    // public static PokemonSO GetPokemonByName(){

    // }

    // public static PokemonSO GetPokemonByType(){

    // }

}
