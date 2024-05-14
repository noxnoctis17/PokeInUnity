using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using NoxNoctisDev.StateMachine;

public class PartyScreen_Battle : State<PlayerBattleMenu>, IPartyScreen
{
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private PartyDisplay _partyDisplay;
    [SerializeField] private PartyScreenContext _partyScreenContext;
    private Button _initialButton;
    public BattleSystem BattleSystem => _battleSystem;
    public PartyDisplay PartyDisplay => _partyDisplay;
    public PlayerBattleMenu BattleMenu { get; private set; }
    public Button LastButton { get; private set; }

    public override void EnterState( PlayerBattleMenu owner ){
        //--State Machinee
        BattleMenu = owner;
        

        //--Open Menu
        gameObject.SetActive( true );

        //--Select Initial Button
        _initialButton = _partyDisplay.PartyButton1;
        StartCoroutine( SetInitialButton() );
    }

    public override void ExitState(){
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
