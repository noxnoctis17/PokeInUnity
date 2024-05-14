using UnityEngine;
using UnityEngine.UI;
using System;

public enum PartyScreenContext { Battle, Pause, UseItemPaused, UseItemBattle, Trade,  }

public class PartyDisplay : MonoBehaviour
{
    [SerializeField] private PartyScreenContext _partyScreenContext;
    [SerializeField] private Button _partyButton1;
    private IPartyScreen _parentMenu;
    private PokemonParty _playerParty;
    private PartyMember_UI[] _memberSlots;
    private PokemonButton[] _pkmnButtons;
    public Button PartyButton1 => _partyButton1;
    public PokemonButton[] PKMNButtons => _pkmnButtons;
    public Action<Button> OnSubmittedButton;

    private void Awake(){
        Init();

        _playerParty.OnPartyUpdated += SetParty;
    }

    private void Init(){
        _memberSlots = GetComponentsInChildren<PartyMember_UI>( true );
        _pkmnButtons = GetComponentsInChildren<PokemonButton>();
        _playerParty = PlayerReferences.Instance.PlayerParty;
        _parentMenu = GetComponentInParent<IPartyScreen>();
        SetParty();
    }

    public void SetParty(){
        var pokemon = _playerParty.PartyPokemon;

        for( int i = 0; i < _memberSlots.Length; i++ ){
            if( i < pokemon.Count ){
                _memberSlots[i].gameObject.SetActive( true );
                _memberSlots[i].Init( pokemon[i] );
            }
            else
                _memberSlots[i].gameObject.SetActive( false );
        }

        AssignPokemonToButtons();
    }

    public void ClearParty(){
        foreach( PartyMember_UI member in _memberSlots ){
            member.gameObject.SetActive( true );
        }

        ClearPartyScreen();
    }

    private void ClearPartyScreen(){
        _memberSlots = null;
        _pkmnButtons = null;
    }

    private void AssignPokemonToButtons(){
        for( int i = 0; i < _pkmnButtons.Length; i++ ){
            _pkmnButtons[i].Pokemon = _memberSlots[i].Pokemon;
            _pkmnButtons[i].Setup( this, _partyScreenContext, _parentMenu );
        }
    }

    public void SetPartyButtons_Interactable( bool isInteractable ){
        Debug.Log( $"SetPartyButtons_Interactable: {isInteractable}" );
        foreach( PokemonButton button in _pkmnButtons ){
            button.ThisButton.interactable = isInteractable;
        }
    }

}
