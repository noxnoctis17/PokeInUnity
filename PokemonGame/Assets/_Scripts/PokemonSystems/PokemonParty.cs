using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PokemonParty : MonoBehaviour
{
    [SerializeField] private bool _isPlayerParty;
    [SerializeField] private bool _isEnemyParty;
    [SerializeField] private List<Pokemon> _partyPokemon;
    public List<Pokemon> PartyPokemon => _partyPokemon;

    private void Start(){
        Init();
    }

    public void Init(){
        // Debug.Log( "Amount of Pokemon in Player Party: " + _partyPokemon.Count );
        foreach( Pokemon pokemon in _partyPokemon ){
            pokemon.Init();
            
            if( _isPlayerParty ){
                pokemon.SetAsPlayerUnit();
            }
            else if( _isEnemyParty ){
                pokemon.SetAsEnemyUnit();
            }
        }
    }

    public Pokemon GetHealthyPokemon(){
        return _partyPokemon.Where( x => x.CurrentHP > 0 ).FirstOrDefault();
    }

    public void AddPokemon( Pokemon pokemon ){
        Pokemon copyPokemon = new ( pokemon.PokeSO, pokemon.Level );

        if( _partyPokemon.Count < 6 )
            _partyPokemon.Add( copyPokemon );
        else{
            //--Add to PC
        }
    }

    public void RestoreSavedParty( List<Pokemon> restoredParty ){
        _partyPokemon = restoredParty;
    }
    
}
