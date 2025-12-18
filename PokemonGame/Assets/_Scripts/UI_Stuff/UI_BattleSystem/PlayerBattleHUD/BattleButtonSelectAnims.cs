using UnityEngine;
using UnityEngine.EventSystems;

public class BattleButtonSelectAnims : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [SerializeField] private GameObject _button, _buttonOutline;
    private bool _submitted;

    private void Start(){
        BattleUIActions.OnSubMenuClosed += () => { _submitted = false; };
    }

    public void OnSelect(BaseEventData baseEventData){   
        if(_submitted) return;

        //--animate the button
        LeanTween.moveX(_button, transform.position.x - 5f, 0.1f);
        
        // //--enable the outline and animate it
        _buttonOutline.SetActive(true); 
        LeanTween.moveX(_buttonOutline, _buttonOutline.transform.position.x - 10f, 0.2f);
    }

    public void OnDeselect(BaseEventData baseEventData){
        if(_submitted) return;

        //--Animate the Button
        LeanTween.moveX(_button, transform.position.x + 5f, 0.2f);

        //--Animate the Outline and then Disable it
        LeanTween.moveX(_buttonOutline, _buttonOutline.transform.position.x + 10f, 0.1f);
        _buttonOutline.SetActive(false); 
    }

    public void OnSubmit(BaseEventData baseEventData){
        _submitted = true;
    }

}
