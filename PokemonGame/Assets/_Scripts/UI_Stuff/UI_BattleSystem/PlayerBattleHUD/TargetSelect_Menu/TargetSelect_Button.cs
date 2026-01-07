using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TargetSelect_Button : MonoBehaviour, ISelectHandler, IDeselectHandler, ICancelHandler, ISubmitHandler
{
    private BattleSystem _battleSystem;
    [SerializeField] private BattleMenu_TargetSelectState _targetSelectState;
    [SerializeField] private Image _portrait;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _hpText;
    private BattleUnit _assignedUnit;
    private BattleUnit _attacker;
    private Button _thisButton;
    private Move _moveToBeUsed;
    public Button ThisButton => _thisButton;

    public void Setup( BattleUnit assignedUnit, BattleUnit attacker, Move move )
    {
        _thisButton = GetComponent<Button>();
        _assignedUnit = assignedUnit;
        _attacker = attacker;
        _moveToBeUsed = move;
        _battleSystem = _targetSelectState.PlayerBattleMenu.BattleSystem;

        if( _assignedUnit.Pokemon != null )
        {
            _portrait.sprite = _assignedUnit.PokeSO.CardPortrait;
            _nameText.text = _assignedUnit.Pokemon.NickName;
            _levelText.text = $"Lv. {_assignedUnit.Pokemon.Level}";
            _hpText.text = $"{_assignedUnit.Pokemon.CurrentHP}/{_assignedUnit.Pokemon.MaxHP}";
        }
    }

    public void OnSelect( BaseEventData eventData )
    {
        AudioController.Instance.PlaySFX( SoundEffect.ButtonSelect );
    }

    public void OnDeselect( BaseEventData eventData )
    {

    }

    public void OnSubmit( BaseEventData eventData )
    {   
        _targetSelectState.PlayerBattleMenu.PopState();
        _battleSystem.SetMoveCommand( _attacker, _assignedUnit, _moveToBeUsed );
    }

    public void OnCancel( BaseEventData eventData )
    {
        _targetSelectState.PlayerBattleMenu.PopState();
    }

}
