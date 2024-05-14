using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ItemButton_PauseScreen : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler, ICancelHandler
{
    [SerializeField] private TextMeshProUGUI _itemName;
    [SerializeField] private TextMeshProUGUI _itemCountText;
    [SerializeField] private Image _itemIcon;
    private BagScreenContext _bagScreenContext;
    private BagScreen_Battle _bagScreenBattle;
    private BagScreen_Pause _bagScreenPause;
    private RectTransform _rectTransform;
    public RectTransform RectTransform => _rectTransform;
    public Item Item { get; private set; }
    public float RectHeight { get; private set; }
    public Button ThisButton { get; private set; }

    public void Init( IBagScreen bagScreen, Item itemSlot, BagScreenContext context ){
        //--Set Bag Screen Context
        _bagScreenContext = context;
        SetBagScreen( bagScreen );

        Item = itemSlot;
        _rectTransform = GetComponent<RectTransform>();
        RectHeight = _rectTransform.rect.height;
        ThisButton = GetComponent<Button>();

        if( Item != null ){
            UpdateInfo();
        }
    }

    private void SetBagScreen( IBagScreen bagScreen ){
        switch( _bagScreenContext )
        {
            case BagScreenContext.Battle:
                _bagScreenBattle = (BagScreen_Battle)bagScreen;
            break;

            case BagScreenContext.Pause:
                _bagScreenPause = (BagScreen_Pause)bagScreen;
            break;
        }
    }

    public void UpdateInfo(){
        //--Set Button Text
        _itemName.text      = Item.ItemSO.ItemName;
        _itemCountText.text = $"{Item.ItemCount}";
        _itemIcon.sprite = Item.ItemSO.Icon;
    }

    public void OnSubmit( BaseEventData eventData ){
        if( Item == null ){
            Debug.Log( "You have no items!" );
            return;
        }
        //--just a reminder, if you want to get the actual class of the items
        //--You make a new variable of System.Type itemType = _itemSlot.GetType()
        //--and then use typeof( class ) to compare in an if statement
        //--i was originally going to do this to test recovery items, but
        //--i realized that it's super easy to let the player change the ball a pokemon is in
        //--through selecting the ball item from the bag and having an option to do so and use
        //--it on a pokemon in the party field of the bag screen
        
        switch( _bagScreenContext )
        {
            case BagScreenContext.Battle:
                _bagScreenBattle.UseItem( Item );
            break;

            case BagScreenContext.Pause:
                _bagScreenPause.UseItem( Item );
            break;
        }
    }

    public void OnSelect( BaseEventData eventData ){
        if( Item == null )
            return;

        switch( _bagScreenContext )
        {
            case BagScreenContext.Battle:
                _bagScreenBattle.BagDisplay.SetSelectedItemInfoField( Item.ItemSO.ItemName, Item.ItemSO.ItemDescription ); 
                _bagScreenBattle.BagDisplay.OnButtonSelected?.Invoke( this );
            break;

            case BagScreenContext.Pause:
                _bagScreenPause.BagDisplay.SetSelectedItemInfoField( Item.ItemSO.ItemName, Item.ItemSO.ItemDescription ); 
                _bagScreenPause.BagDisplay.OnButtonSelected?.Invoke( this );
            break;
        }
    }

    public void OnDeselect( BaseEventData eventData ){

    }

    public void OnCancel( BaseEventData eventData ){
        switch( _bagScreenContext )
        {
            case BagScreenContext.Battle:
                _bagScreenBattle.BattleMenu.PopState();
            break;

            case BagScreenContext.Pause:
                _bagScreenPause.PauseMenuStateMachine.PopState();
            break;
        }
        
    }
}
