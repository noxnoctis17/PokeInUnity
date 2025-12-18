using System.Collections;
using NoxNoctisDev.StateMachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RunMenu : State<PlayerBattleMenu>
{
    [SerializeField] BattleSystem _battleSystem;
    private PlayerBattleMenu _battleMenu;
    [SerializeField] private Button _confirmEscape;

    public override void EnterState( PlayerBattleMenu owner ){
        gameObject.SetActive( true );
        Debug.Log( "EnterState: " + this );

        _battleMenu = owner;
        SetupButton();
        StartCoroutine( SelectInitialButton() );
    }

    public override void ExitState(){
        BattleUIActions.OnSubMenuClosed?.Invoke();
        gameObject.SetActive( false );
    }

    private void SetupButton(){
        _confirmEscape.gameObject.GetComponent<RunButton>().Setup( _battleSystem, _battleMenu );
    }

    private IEnumerator SelectInitialButton(){
        yield return new WaitForSeconds( 0.25f );
        _confirmEscape.Select();
    }

}
