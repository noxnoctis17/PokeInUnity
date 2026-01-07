using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MoveButton_Summary : MonoBehaviour, ISelectHandler, IDeselectHandler, ICancelHandler, ISubmitHandler
{
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private PartyScreen_Battle _partyScreen;
    [SerializeField] private TextMeshProUGUI _moveName;
    [SerializeField] private TextMeshProUGUI _ppText;
    [SerializeField] private Image _typeIcon;
    [SerializeField] private TextMeshProUGUI _moveDescription;
    public Move AssignedMove { get; set; }
    private Button _thisButton;


    private void OnEnable()
    {
        
    }

    public void OnSelect( BaseEventData baseEventData )
    {
        AudioController.Instance.PlaySFX( SoundEffect.ButtonSelect );
    }

    public void OnDeselect( BaseEventData baseEventData )
    {
        
    }

    public void OnSubmit( BaseEventData baseEventData )
    {
        
    }

    public void OnCancel( BaseEventData baseEventData )
    {

    }

    public void Setup( Move move )
    {
        AssignedMove = move;
        _moveName.text = move.MoveSO.Name;
        _ppText.text = $"{move.PP}/{move.MoveSO.PP}";
        _typeIcon.sprite = TypeIconAtlas.TypeIcons[move.MoveType];
        _moveDescription.text = move.MoveSO.Description;
    }
}
