using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public enum PartyScreenContext { Battle, Pause, UseItemPaused, UseItemBattle, Trade,  }

public class PartyDisplay : MonoBehaviour, IInitializeMeDaddy
{
    [SerializeField] private PartyScreenContext _partyScreenContext;
    [SerializeField] private Button _partyButton1;
    private IPartyScreen _parentMenu;
    private PartyMember_UI[] _memberSlots;
    private PokemonButton[] _pkmnButtons;
    public PlayerTrainer PlayerTrainer { get; private set; }
    public Button PartyButton1 => _partyButton1;
    public PartyMember_UI[] MemberSlots => _memberSlots;
    public PokemonButton[] PKMNButtons => _pkmnButtons;
    public Action<Button> OnSubmittedButton;
    public Action<bool> OnHPPocketEntered;
    public Action<Item, bool> OnEvolutionItemSelected;
    public Action<Item, bool> OnTMSelected;

    private void OnEnable()
    {
        if( _partyScreenContext == PartyScreenContext.UseItemPaused )
        {
            OnHPPocketEntered           += SetHPBarActive;
            OnEvolutionItemSelected     += SetStatusText_EvoItem;
            OnTMSelected                += SetStatusText_TM;
        }
    }

    private void OnDisable()
    {
        if( _partyScreenContext == PartyScreenContext.UseItemPaused )
        {
            OnHPPocketEntered           -= SetHPBarActive;
            OnEvolutionItemSelected     -= SetStatusText_EvoItem;
            OnTMSelected                -= SetStatusText_TM;
        }
    }

    public void Init()
    {
        _memberSlots = GetComponentsInChildren<PartyMember_UI>( true );
        _pkmnButtons = GetComponentsInChildren<PokemonButton>();
        PlayerTrainer = PlayerReferences.Instance.PlayerTrainer;
        _parentMenu = GetComponentInParent<IPartyScreen>( true );

        PlayerTrainer.OnPartyUpdated += SetParty;
        BattleSystem.OnBattlePartyUpdated += SetParty;
        

        SetParty( PlayerTrainer.ActiveParty );
    }

    public void SetParty( List<Pokemon> party )
    {
        for( int i = 0; i < _memberSlots.Length; i++ )
        {
            if( i < party.Count )
            {
                Debug.Log( $"[Party Display] Button index {i} has Pokemon {party[i].NickName}" );
                _memberSlots[i].gameObject.SetActive( true );
                _memberSlots[i].Init( party[i] );
            }
            else
                _memberSlots[i].gameObject.SetActive( false );
        }

        AssignPokemonToButtons();
    }

    public void ClearParty()
    {
        foreach( PartyMember_UI member in _memberSlots )
        {
            member.gameObject.SetActive( true );
        }

        ClearPartyScreen();
    }

    private void ClearPartyScreen()
    {
        _memberSlots = null;
        _pkmnButtons = null;
    }

    private void AssignPokemonToButtons()
    {
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

    private void SetHPBarActive( bool show )
    {
        Debug.Log( "SetHPBarActive" );
        foreach( PartyMember_UI icon in _memberSlots ){
            icon.ShowHPBar( show );
        }
    }

    private void SetStatusText_EvoItem( Item item, bool show = false )
    {
        Debug.Log( "SetStatusText_TM" );
        foreach( PartyMember_UI icon in _memberSlots ){
            icon.UpdateStatusText_EvoItem( item, show );
        }
    }

    private void SetStatusText_TM( Item item, bool show = false )
    {
        Debug.Log( "SetStatusText_TM" );
        foreach( PartyMember_UI icon in _memberSlots ){
            icon.UpdateStatusText_TM( item, show );
        }
    }

    public int GetIndex( PokemonButton button )
    {
        for( int i = 0; i < _memberSlots.Length; i++ )
        {
            if( _pkmnButtons[i] == button )
                return i;
        }

        return default;
    }
}
