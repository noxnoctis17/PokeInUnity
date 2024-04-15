using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PokemonParty : MonoBehaviour
{
    [SerializeField] private bool _isPlayerParty;
    [SerializeField] private bool _isEnemyParty;
    [SerializeField] private List<PokemonClass> _partyPokemon;
    public List<PokemonClass> PartyPokemon => _partyPokemon;

    private void Start(){
        Init();
    }

    public void Init(){
        // Debug.Log( "Amount of Pokemon in Player Party: " + _partyPokemon.Count );
        foreach( PokemonClass pokemon in _partyPokemon ){
            pokemon.Init();
            
            if( _isPlayerParty ){
                pokemon.SetAsPlayerUnit();
            }
            else if( _isEnemyParty ){
                pokemon.SetAsEnemyUnit();
            }
        }
    }

    public PokemonClass GetHealthyPokemon(){
        return _partyPokemon.Where( x => x.CurrentHP > 0 ).FirstOrDefault();
    }

    public void AddPokemon( PokemonClass pokemon ){
        PokemonClass copyPokemon = new ( pokemon.PokeSO, pokemon.Level );

        if( _partyPokemon.Count < 6 )
            _partyPokemon.Add( copyPokemon );
        else{
            //--Add to PC
        }
    }
    
}
