using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MoveButton : MonoBehaviour, ISelectHandler, IDeselectHandler, ICancelHandler, ISubmitHandler
{
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private FightMenu _fightMenu;
    [SerializeField] private BattleDialogueBox _dialogueBox;
    [SerializeField] private TextMeshProUGUI _moveDescription;
    public MoveClass AssignedMove { get; set; }
    private Button _thisButton;


    private void OnEnable(){
        gameObject.GetComponent<Outline>().enabled = false;
        _thisButton = gameObject.GetComponent<Button>();
    }

    public void OnSelect( BaseEventData baseEventData ){
        #pragma warning disable CS8321
        IEnumerator SelectedOutlineDelay(){
            yield return new WaitForSeconds( 0.1f );
        }
        #pragma warning restore CS8321
        gameObject.GetComponent<Outline>().enabled = true;
        _moveDescription.text = AssignedMove.MoveSO.Description;
    }

    public void OnDeselect( BaseEventData baseEventData ){
        gameObject.GetComponent<Outline>().enabled = false;
    }

    public void OnSubmit( BaseEventData baseEventData ){
        if( AssignedMove.PP > 0){
            BattleUIActions.OnSubMenuClosed?.Invoke();
            _fightMenu.SetMemoryButton( _thisButton );
            BattleUIActions.OnCommandUsed?.Invoke();
            Debug.Log( "MoveButton OnSubmit, Popping Menu State" );
            _fightMenu.BattleMenu.BattleMenuStateMachine.Pop();
            _battleSystem.SetPlayerMoveCommand( _fightMenu.ActiveUnit, AssignedMove );
        }
        else{
            StartCoroutine( _battleSystem.DialogueBox.TypeDialogue( "There's no PP left!" ) );
        }
    }

    public void OnCancel( BaseEventData baseEventData ){
        _fightMenu.ClearMemoryButton();
        BattleUIActions.OnSubMenuClosed?.Invoke();
        BattleUIActions.OnFightMenuClosed?.Invoke();
        StartCoroutine( WaitForCloseAnims() );
    }

    private IEnumerator WaitForCloseAnims(){
        yield return new WaitForSeconds( 0.1f );
        gameObject.GetComponent<Outline>().enabled = false;
        _fightMenu.BattleMenu.BattleMenuStateMachine.Pop();
    }

    private IEnumerator WaitForMoveDialogue(){
        yield return new WaitForSeconds( 2f );
    }
}
