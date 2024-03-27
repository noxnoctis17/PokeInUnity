using UnityEngine;
using NoxNoctisDev.StateMachine;

public class PKMNMenu : State<PlayerBattleMenu>
{
    [SerializeField] private BattleSystem _battleSystem;
    public PlayerBattleMenu BattleMenu { get; private set; }
    public BattleSystem BattleSystem => _battleSystem;
    [SerializeField] private PartyScreen _partyScreen;
    public PartyScreen PartyScreen => _partyScreen;

    public override void EnterState( PlayerBattleMenu owner ){
        // _partyScreen.Init();
        // _partyScreen.SetParty( _battleSystem.PlayerParty.PartyPokemon );

        gameObject.SetActive( true );
        Debug.Log( "EnterState: " + this );
        BattleMenu = owner;

        BattleUIActions.OnPkmnMenuOpened?.Invoke();
    }

    public override void ExitState(){
        BattleUIActions.OnPkmnMenuClosed?.Invoke();
        gameObject.SetActive( false );
    }

}
