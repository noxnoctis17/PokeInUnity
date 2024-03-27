using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

public class PartyScreen : MonoBehaviour
{
    [SerializeField] private PartyMember_UI[] _memberSlots;
    private PKMN_Button[] _pkmnButton;
    [SerializeField] private Button _partyButton1, _partyButton2, _partyButton3, _partyButton4, _partyButton5, _partyButton6;
    public Button LastPKMNButton { get; private set; }
    public Action<Button> OnSubmittedButton;

    private void OnEnable(){
        OnSubmittedButton += SetLastButton;
        StartCoroutine( SetInitialButton() );
    }

    private void OnDisable(){
        OnSubmittedButton -= SetLastButton;
    }

    public void Init(){
        // ClearPartyScreen();
        // _memberSlots = GetComponentsInChildren<PartyMember_UI>();
        _pkmnButton = GetComponentsInChildren<PKMN_Button>();
    }

    public void SetParty( List<PokemonClass> pokemon ){
        Debug.Log( "SetParty Party Amount: " + pokemon.Count );
        Debug.Log( "SetParty _memberSlots Amount: " + _memberSlots.Length );
        for( int i = 0; i < _memberSlots.Length; i++ ){
            if( i < pokemon.Count )
                _memberSlots[i].SetData( pokemon[i] );
            else
                _memberSlots[i].gameObject.SetActive( false );
        }

        AssignPokemonToButtons();
    }

    public void ClearParty(){
        Debug.Log( "Member Slots in ClearParty: " + _memberSlots.Length );
        foreach( PartyMember_UI member in _memberSlots ){
            member.gameObject.SetActive( true );
        }

        ClearPartyScreen();
    }

    private void ClearPartyScreen(){
        // _memberSlots = null;
        _pkmnButton = null;
    }

    private void AssignPokemonToButtons(){
        for( int i = 0; i < _pkmnButton.Length; i++ ){
            _pkmnButton[i].Pokemon = _memberSlots[i].Pokemon;
        }
    }

    private IEnumerator SetInitialButton(){
        if( LastPKMNButton == null )
            LastPKMNButton = _partyButton1;
        
        yield return new WaitForSeconds( 0.1f );
        Debug.Log( "Set Initial Button: " + LastPKMNButton.gameObject.name );
        LastPKMNButton.Select();
    }

    private void SetLastButton( Button button ){
        Debug.Log( "SetLastButton: " + LastPKMNButton.gameObject.name );
        LastPKMNButton = button;
    }

}
