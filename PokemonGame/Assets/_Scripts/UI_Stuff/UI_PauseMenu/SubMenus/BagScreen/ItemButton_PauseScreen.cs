using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemButton_PauseScreen : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler, ICancelHandler
{
    [SerializeField] private TextMeshProUGUI _itemName;
    [SerializeField] private TextMeshProUGUI _itemCountText;
    [SerializeField] private Image _itemIcon;
    private Bag_PauseScreen _bagScreen;
    private RectTransform _rectTransform;
    public RectTransform RectTransform => _rectTransform;
    public ItemSlot ItemSlot { get; private set; }
    public float RectHeight { get; private set; }
    public Button ThisButton { get; private set; }

    public void Init( Bag_PauseScreen bagScreen, ItemSlot itemSlot ){
        //--Set Bag Screen
        _bagScreen = bagScreen;
        ItemSlot = itemSlot;
        _rectTransform = GetComponent<RectTransform>();
        RectHeight = _rectTransform.rect.height;
        ThisButton = GetComponent<Button>();

        if( ItemSlot != null ){
            UpdateInfo();
        }
    }

    public void UpdateInfo(){
        //--Set Button Text
        _itemName.text      = ItemSlot.ItemSO.ItemName;
        _itemCountText.text = $"{ItemSlot.ItemCount}";
        _itemIcon.sprite = ItemSlot.ItemSO.Icon;
    }

    public void OnSelect( BaseEventData eventData ){
        if( ItemSlot == null )
            return;

        _bagScreen.OnButtonSelected?.Invoke( this );
        _bagScreen.ItemName.text            = ItemSlot.ItemSO.ItemName;
        _bagScreen.ItemDescription.text     = ItemSlot.ItemSO.ItemDescription;
    }

    public void OnDeselect( BaseEventData eventData ){

    }

    public void OnSubmit( BaseEventData eventData ){
        if( ItemSlot == null ){
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
        
        _bagScreen.UseItem( ItemSlot );
    }

    public void OnCancel( BaseEventData eventData ){
        _bagScreen.PauseMenuStateMachine.CloseCurrentMenu();
    }
}
