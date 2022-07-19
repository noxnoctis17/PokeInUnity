using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActiveBattlePokemon : MonoBehaviour
{
    [SerializeField] private OnFieldUnit[] _activeUnits;
    private int _unitAmount;
    
    public IEnumerator EnableUnits( int unitAmount ){
        _unitAmount = unitAmount;
        
        for( int i = 0; i < unitAmount; i++ ){
            Debug.Log( i );
            if( i < unitAmount ){
                _activeUnits[i].gameObject.SetActive( true );
            } else{
                yield break;
            }
        }
        
        yield return null;
    }

    public void SetUnits( PokemonParty pokemonParty ){
        for( int i = 0; i < _unitAmount; i++ ){
            Debug.Log( i );
            _activeUnits[i].Setup( pokemonParty.PartyPokemon[i] );
        }
    }
    
}
