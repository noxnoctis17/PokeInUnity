using UnityEngine;
using NoxNoctisDev.StateMachine;
using System.Collections;
using UnityEngine.UI;

public class PKMNMenu : State<PlayerBattleMenu>
{
    [SerializeField] private BattleSystem _battleSystem;
    public PlayerBattleMenu BattleMenu { get; private set; }
    public BattleSystem BattleSystem => _battleSystem;
    [SerializeField] private PartyScreen _partyScreen;
    public PartyScreen PartyScreen => _partyScreen;
    private Button _initialButton;
    public Button LastButton { get; private set; }

    public override void EnterState( PlayerBattleMenu owner ){
        // _partyScreen.Init();
        // _partyScreen.SetParty( _battleSystem.PlayerParty.PartyPokemon );

        gameObject.SetActive( true );
        Debug.Log( "EnterState: " + this );
        BattleMenu = owner;

        _initialButton = _partyScreen.PartyButton1;

        StartCoroutine( SetInitialButton() );
        BattleUIActions.OnPkmnMenuOpened?.Invoke();
    }

    public override void ExitState(){
        BattleUIActions.OnPkmnMenuClosed?.Invoke();
        gameObject.SetActive( false );
    }

    private IEnumerator SetInitialButton(){
        yield return new WaitForSeconds( 0.15f );

        if( LastButton != null )
            SelectMemoryButton();
        else{
            SetMemoryButton( _initialButton );
        }
    }

    public void SetMemoryButton( Button lastButton ){
        LastButton = lastButton;
        SelectMemoryButton();
    }

    private void SelectMemoryButton(){
        LastButton.Select();
    }

    public void ClearMemoryButton(){
        LastButton = null;
        _initialButton.Select();
    }

}
