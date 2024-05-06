using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class PKMNMenu_Battle : PKMNMenu_Base<PlayerBattleMenu>
{
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private PartyScreen _partyScreen;
    [SerializeField] private PartyScreenContext _partyScreenContext;
    private Button _initialButton;
    public BattleSystem BattleSystem => _battleSystem;
    public PartyScreen PartyScreen => _partyScreen;
    public PlayerBattleMenu BattleMenu { get; private set; }
    public Button LastButton { get; private set; }

    public override void EnterState( PlayerBattleMenu owner ){
        BattleMenu = owner;

        PKMNMenu_Events.OnPopPartyScreenState += PopScreenState;

        _partyScreen.Init( _partyScreenContext );
        _partyScreen.SetParty( _battleSystem.PlayerParty.PartyPokemon );

        gameObject.SetActive( true );
        // Debug.Log( "EnterState: " + this );

        _initialButton = _partyScreen.PartyButton1;
        StartCoroutine( SetInitialButton() );
    }

    public override void ExitState(){
        PKMNMenu_Events.OnPopPartyScreenState -= PopScreenState;
        gameObject.SetActive( false );
    }

    private void PopScreenState(){
        BattleMenu.BattleMenuStateMachine.Pop();
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
