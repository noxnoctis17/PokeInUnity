using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RunButton : MonoBehaviour, ISelectHandler, ICancelHandler
{
    [SerializeField] BattleSystem _battleSystem;
    private Button _confirmEscape;

    private void OnEnable()
    {
        _confirmEscape = GetComponent<Button>();
        _confirmEscape.Select();
    }

    public void OnSelect(BaseEventData eventData)
    {
        _battleSystem.SetRunFromBattleCommand();
        BattleUIActions.OnCommandUsed?.Invoke();
        BattleUIActions.OnSubMenuClosed?.Invoke();
    }

    public void OnCancel(BaseEventData eventData)
    {
        gameObject.SetActive(false);
    }
    public void OnDisable()
    {
        gameObject.SetActive(false);
    }
}
