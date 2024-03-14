using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class PartyScreen : MonoBehaviour
{
    private PartyMember_UI[] _memberSlots;
    private PKMN_Button[] _pkmnButton;
    [SerializeField] private Button _partyButton1, _partyButton2, _partyButton3, _partyButton4, _partyButton5, _partyButton6;
    public Button LastPKMNButton;
    [SerializeField] private EventSystem _eventSystem;

    private void OnEnable(){
        StartCoroutine( SetInitialButton() );
    }

    public void Init(){
        _memberSlots = GetComponentsInChildren<PartyMember_UI>();
        _pkmnButton = GetComponentsInChildren<PKMN_Button>();

        if( LastPKMNButton != null )
        LastPKMNButton.Select();
    }

    public void SetParty( List<PokemonClass> pokemon ){
        for( int i = 0; i < _memberSlots.Length; i++ ){
            if(i < pokemon.Count)
                _memberSlots[i].SetData(pokemon[i]);
            else
                _memberSlots[i].gameObject.SetActive( false );
        }

        AssignPokemonToButtons();
    }

    private void AssignPokemonToButtons(){
        for( int i = 0; i < _pkmnButton.Length; i++ ){
            _pkmnButton[i].Pokemon = _memberSlots[i].Pokemon;
        }
    }

    private IEnumerator SetInitialButton(){
        LastPKMNButton = _partyButton1;
        yield return new WaitForSeconds( 0.1f );
        LastPKMNButton.Select();
    }

}
