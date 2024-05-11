using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PokemonButton : MonoBehaviour, ISelectHandler, IDeselectHandler, ICancelHandler, ISubmitHandler
{
    [SerializeField] private GameObject _selectedOutline;
    [SerializeField] private PokemonButton_Battle _battleContext;
    [SerializeField] private PokemonButton_PauseScreen _pauseContext;
    [SerializeField] private PokemonButton_UseItem _itemContext;
    private PartyDisplay _partyDisplay;
    private IPartyScreen _parentMenu;
    private PartyScreenContext _partyScreenContext;
    private IPokemonButtonContext _buttonContext;
    public Pokemon Pokemon;
    public Button ThisButton { get; private set; }

    public void Setup( PartyDisplay partyScreen, PartyScreenContext partyScreenContext, IPartyScreen parentMenu ){
        ThisButton = GetComponent<Button>();
        _partyScreenContext = partyScreenContext;
        _partyDisplay = partyScreen;
        _parentMenu = parentMenu;

        switch( _partyScreenContext )
        {
            case PartyScreenContext.Battle:
                _buttonContext = _battleContext;
                _buttonContext.Init( partyScreen, this, _parentMenu );
            break;

            case PartyScreenContext.PauseMenu:
                _buttonContext = _pauseContext;
                _buttonContext.Init( partyScreen, this, _parentMenu );
            break;

            case PartyScreenContext.ItemUse:
                _buttonContext = _itemContext;
                _buttonContext.Init( partyScreen, this, _parentMenu );
            break;
        }
    }

    public void OnSelect( BaseEventData eventData ){
        _selectedOutline.SetActive( true );
        _buttonContext.ContextSelected();
    }

    public void OnDeselect( BaseEventData eventData ){
        _selectedOutline.SetActive( false );
        _buttonContext.ContextDeSelected();
    }

    public void OnSubmit( BaseEventData eventData ){
        _buttonContext.ContextSubmit();
        _partyDisplay.OnSubmittedButton?.Invoke( ThisButton );
    }

    public void OnCancel( BaseEventData baseEventData ){
        _buttonContext.ContextCancel();
    }

    public IEnumerator WaitForCloseAnims(){
        yield return new WaitForSeconds( 0.1f );
        _buttonContext.CloseContextMenu();
    }

}
