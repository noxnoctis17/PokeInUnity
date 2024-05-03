using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class PartyScreen : MonoBehaviour
{
    private PartyMember_UI[] _memberSlots;
    private PKMN_Button[] _pkmnButton;
    [SerializeField] private Button _partyButton1;
    public Button PartyButton1 => _partyButton1;
    public Action<Button> OnSubmittedButton;

    public void Init(){
        _memberSlots = GetComponentsInChildren<PartyMember_UI>( true );
        _pkmnButton = GetComponentsInChildren<PKMN_Button>();
    }

    public void SetParty( List<Pokemon> pokemon ){
        // Debug.Log( "SetParty Party Amount: " + pokemon.Count );
        // Debug.Log( "SetParty _memberSlots Amount: " + _memberSlots.Length );
        for( int i = 0; i < _memberSlots.Length; i++ ){
            if( i < pokemon.Count ){
                _memberSlots[i].gameObject.SetActive( true );
                _memberSlots[i].SetData( pokemon[i] );
            }
            else
                _memberSlots[i].gameObject.SetActive( false );
        }

        AssignPokemonToButtons();
    }

    public void ClearParty(){
        // Debug.Log( "Member Slots in ClearParty: " + _memberSlots.Length );
        foreach( PartyMember_UI member in _memberSlots ){
            member.gameObject.SetActive( true );
        }

        ClearPartyScreen();
    }

    private void ClearPartyScreen(){
        _memberSlots = null;
        _pkmnButton = null;
    }

    private void AssignPokemonToButtons(){
        for( int i = 0; i < _pkmnButton.Length; i++ ){
            _pkmnButton[i].Pokemon = _memberSlots[i].Pokemon;
        }
    }

}
