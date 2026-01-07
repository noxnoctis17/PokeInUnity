using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PokemonParty : MonoBehaviour
{
    [SerializeField] private bool _isPlayerParty;
    [SerializeField] private bool _isEnemyParty;
    [SerializeField] private List<Pokemon> _partyPokemon;
    public List<Pokemon> Party { get { return _partyPokemon; } set { PartySetter( value ); } }
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
        _partyPokemon = party;
        OnPartyUpdated?.Invoke();
    }

    public Pokemon GetHealthyPokemon( List<Pokemon> dontInclude = null ){
        var healthyPokemon = _partyPokemon.Where( x => x.CurrentHP > 0 ).ToList();
        
        if( dontInclude != null )
            healthyPokemon = healthyPokemon.Where( p => !dontInclude.Contains( p ) ).ToList();

        return healthyPokemon.FirstOrDefault();
    }

    public List<Pokemon> GetHealthyPokemon( int unitCount ){
        return _partyPokemon.Where( x => x.CurrentHP > 0 ).Take( unitCount ).ToList();
    }

    public void AddPokemon( Pokemon pokemon, PokeBallType ball ){
        Pokemon copyPokemon = new ( pokemon.PokeSO, pokemon.Level );
        copyPokemon.Init();
        copyPokemon.CurrentHP = pokemon.CurrentHP;
        copyPokemon.ChangeCurrentBall( ball );

        if( _partyPokemon.Count < 6 ){
            Party.Add( copyPokemon );
            copyPokemon.SetAsPlayerUnit();
            OnPartyUpdated?.Invoke();

            if( pokemon.SevereStatus != null )
                copyPokemon.SetSevereStatus( pokemon.SevereStatus.ID );
        }
        else{
            Debug.Log( "Your Party is Full" );
            //--Add to PC
        }
    }

    public void UpdateParty()
    {
        OnPartyUpdated?.Invoke();
    }

    public void GiveParty( List<Pokemon> givenParty )
    {
        Party = givenParty;
    }

    public void RestoreSavedParty( List<Pokemon> restoredParty ){
        Party = restoredParty;
    }

    public void SwitchPokemonPosition( Pokemon chosenMon, Pokemon swapTo )
    {
        int chosen = 0;
        int swap = 0;

        for( int i = 0; i < _partyPokemon.Count; i++ )
        {
            if( _partyPokemon[i] == chosenMon )
                chosen = i;

            if( _partyPokemon[i] == swapTo )
                swap = i;
        }
        
        Debug.Log( $"Swapping indices {swap} & {chosenMon}" );
        _partyPokemon[swap] = chosenMon;
        _partyPokemon[chosen] = swapTo;
        OnPartyUpdated?.Invoke();
    }
    
}
