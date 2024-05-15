using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Rendering;
using UnityEngine;

public class PokemonParty : MonoBehaviour
{
    [SerializeField] private bool _isPlayerParty;
    [SerializeField] private bool _isEnemyParty;
    [SerializeField] private List<Pokemon> _partyPokemon;
    public List<Pokemon> PartyPokemon { get { return _partyPokemon; } set { PartySetter( value ); } }
    public event Action OnPartyUpdated;

    private void Start(){
        Init();
    }

    public void Init(){
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

    //--Reminder that when you need to set the Party, you HAVE to use the property so that the Setter is called
    //--The purpose of a setter is to i guess also execute some type of code "on set" of a property, as opposed to just like
    //--updating the direct reference value. like in this case, where we not only update the party list, we also need to fire
    //--the event so that observers can be notified of the change to the property they're using.
    private void PartySetter( List<Pokemon> party ){
        Debug.Log( "PartySetter();" );
        _partyPokemon = party;
        OnPartyUpdated?.Invoke();
        Init();
    }

    public Pokemon GetHealthyPokemon(){
        return _partyPokemon.Where( x => x.CurrentHP > 0 ).FirstOrDefault();
    }

    public void AddPokemon( Pokemon pokemon ){
        Pokemon copyPokemon = new ( pokemon.PokeSO, pokemon.Level );

        if( _partyPokemon.Count < 6 ){
            PartyPokemon.Add( copyPokemon );
            OnPartyUpdated?.Invoke();
        }
        else{
            //--Add to PC
        }
    }

    public void RestoreSavedParty( List<Pokemon> restoredParty ){
        PartyPokemon = restoredParty;
    }
    
}
