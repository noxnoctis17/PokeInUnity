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
    public MoveClass AssignedMove {get; set;}


    private void OnEnable(){
        gameObject.GetComponent<Outline>().enabled = false;
    }

    public void OnSelect(BaseEventData baseEventData){
        IEnumerator SelectedOutlineDelay(){
            yield return new WaitForSeconds(0.1f);
        }
        gameObject.GetComponent<Outline>().enabled = true;
        _moveDescription.text = AssignedMove.moveBase.Description;
    }

    public void OnDeselect(BaseEventData baseEventData){
        gameObject.GetComponent<Outline>().enabled = false;
    }

    public void OnSubmit(BaseEventData baseEventData){
        BattleUIActions.OnSubMenuClosed?.Invoke();
        _battleSystem.SetPlayerMoveCommand(_fightMenu.playerUnit, AssignedMove);
        BattleUIActions.OnCommandUsed?.Invoke();
    }

    public void OnCancel(BaseEventData baseEventData){
        BattleUIActions.OnSubMenuClosed?.Invoke();
        BattleUIActions.OnFightMenuClosed?.Invoke();
        StartCoroutine(WaitForCloseAnims());
    }

    private IEnumerator WaitForCloseAnims(){
        yield return new WaitForSeconds(0.1f);
        gameObject.GetComponent<Outline>().enabled = false;
        _fightMenu.gameObject.SetActive(false);
    }

    private IEnumerator WaitForMoveDialogue(){
        yield return new WaitForSeconds(2f);
    }
}
