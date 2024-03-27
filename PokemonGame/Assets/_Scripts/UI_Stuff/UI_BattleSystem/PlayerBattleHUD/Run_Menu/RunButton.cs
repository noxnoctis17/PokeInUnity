using System.Collections;
using NoxNoctisDev.StateMachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RunButton : State<PlayerBattleMenu>, ISubmitHandler, ICancelHandler
{
    [SerializeField] BattleSystem _battleSystem;
    private PlayerBattleMenu _battleMenu;
    private Button _confirmEscape;

    public override void EnterState( PlayerBattleMenu owner ){
        gameObject.SetActive( true );
        Debug.Log( "EnterState: " + this );

        _battleMenu = owner;

        _confirmEscape = GetComponent<Button>();
        _confirmEscape.Select();
    }

    public override void ExitState(){
        BattleUIActions.OnSubMenuClosed?.Invoke();
        gameObject.SetActive( false );
    }

    public void OnSubmit( BaseEventData eventData ){
        _battleSystem.SetRunFromBattleCommand();
        _battleMenu.BattleMenuStateMachine.Pop();
        BattleUIActions.OnCommandUsed?.Invoke();
        BattleUIActions.OnSubMenuClosed?.Invoke();
    }

    public void OnCancel( BaseEventData baseEventData ){
        BattleUIActions.OnSubMenuClosed?.Invoke();
        StartCoroutine( WaitForCloseAnims() );
    }

    private IEnumerator WaitForCloseAnims(){
        yield return new WaitForSeconds( 0.1f );
        gameObject.GetComponent<Outline>().enabled = false;
        _battleMenu.BattleMenuStateMachine.Pop();
    }
}
