using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class FightMenu : MonoBehaviour
{
    [SerializeField] PlayerBattleMenu _battleMenu;
    [SerializeField] Button move1button, move2button, move3button, move4button;
    private Button _intialButton;
    public Button LastButton;
    [SerializeField] BattleUnit _playerUnit;
    public BattleUnit playerUnit => _playerUnit;
    [SerializeField] List<MoveButton> _moveButtons;
    [SerializeField] List<TextMeshProUGUI> _moveNameText, _ppText;
    // [SerializeField] private Image _pokemonType_Image1, _pokemonType_Image2;

    private void OnEnable(){
        _intialButton = move1button;
        StartCoroutine(SetInitialButton());
        BattleUIActions.OnFightMenuOpened?.Invoke();
    }

    private void OnDisable(){
        BattleUIActions.OnSubMenuClosed?.Invoke();
        BattleUIActions.OnFightMenuClosed?.Invoke();
    }

    public void SetUpMoves(List<MoveClass> moves){
        SetMoveNames(moves);
        SetMoveButtons(moves);
    }

    private void SetMoveNames(List<MoveClass> moves){
        for(int i = 0; i < moves.Count; i++){
            for(int moveTexti = 0; moveTexti < _moveNameText.Count; moveTexti++){
                if(i < _moveNameText.Count){
                    _moveNameText[i].text = moves[i].moveBase.MoveName;
                    
                    if(moveTexti > i)
                        _moveNameText[moveTexti].text = "-";

                } else {
                        _moveNameText[i].text = "-";
                }
            }

            for(int moveTexti = 0; moveTexti < _ppText.Count; moveTexti++){
                if(i < _moveNameText.Count){
                    _ppText[i].text = $"PP: {moves[i].moveBase.PP.ToString()}";
                    
                    if(moveTexti > i)
                        _ppText[moveTexti].text = "PP: -";

                } else {
                    _ppText[i].text = "PP: -";
                }
            }
        }
    }

    private void SetMoveButtons(List<MoveClass> moves){
        for(int i = 0; i < moves.Count; i++){
            _moveButtons[i].AssignedMove = moves[i];
        }
    }

    private IEnumerator SetInitialButton(){
        yield return new WaitForSeconds(0.15f);
        _intialButton.Select();
    }

}
