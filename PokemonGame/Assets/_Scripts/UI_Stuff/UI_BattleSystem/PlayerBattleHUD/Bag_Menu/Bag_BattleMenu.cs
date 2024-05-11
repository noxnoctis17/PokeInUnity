using System.Collections;
using UnityEngine;
using NoxNoctisDev.StateMachine;
using UnityEngine.UI;

public class Bag_BattleMenu : State<PlayerBattleMenu>
{
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private PlayerBattleMenu _battleMenu;
    [SerializeField] private Button _throwBall; //--temporary for catch pokemon testing
    public PlayerBattleMenu BattleMenu => _battleMenu;
    private Button _initialButton;

    public override void EnterState(PlayerBattleMenu owner){
        gameObject.SetActive( true );

        Debug.Log( "EnterState: " + this );
        _battleMenu = owner;

        _initialButton = _throwBall;
        StartCoroutine( SetInitialButton() );
    }

    public override void ExitState(){
        BattleUIActions.OnSubMenuClosed?.Invoke();
        gameObject.SetActive( false );
    }

    private IEnumerator SetInitialButton(){
        yield return new WaitForSeconds( 0.15f );
        _initialButton.Select();
    }
}
