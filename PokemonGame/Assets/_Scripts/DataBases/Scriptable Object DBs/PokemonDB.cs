using System;
using System.Collections.Generic;
using UnityEngine;

public class PokemonDB
{
    private static Dictionary<(string Species, int Form), PokemonSO> _pokemonSpeciesDB;

    public static void Init()
    {
        _pokemonSpeciesDB = new();

        var dbArray = Resources.LoadAll<PokemonSO>( "" );
        foreach( var pokeSO in dbArray )
        {
            var key = ( pokeSO.Species, pokeSO.Form );

            if( _pokemonSpeciesDB.ContainsKey( key ) )
            {
                Debug.LogError( "Duplicate Pokemon Species" );
                continue;
            }

            _pokemonSpeciesDB[key] = pokeSO;
        }
    }

    public static PokemonSO GetPokemonBySpecies( ( string species, int form ) key )
    {
        if( !_pokemonSpeciesDB.ContainsKey( key ) )
        {
            Debug.LogError( "Pokemon not found in Pokemon Database!" );
            return null;
        }
        
        return _pokemonSpeciesDB[key];
    }
}
