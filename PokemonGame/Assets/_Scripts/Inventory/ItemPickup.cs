using UnityEngine;

public class ItemPickup : MonoBehaviour, IInteractable, ISavable
{
    [SerializeField] private ItemSO _item;
    [SerializeField] private int _count = 1;
    [SerializeField] private bool _pickedUp;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private BoxCollider _boxCollider;
    public bool PickedUp => _pickedUp;
    
    private void OnEnable(){
        if( _pickedUp )
            ShowPickup( false );
    }

    public void Interact(){
        Debug.Log( "You interacted with an item!" );
        //--If we've picked this up, or if this is incorrectly set and somehow the object is still enabled, return and don't add, throw error
        if( _pickedUp ){
            Debug.LogError( "You've already picked up this item!" );
            gameObject.SetActive( false );
            return;
        }

        //--Create temp var for player inventory
        var inventory = PlayerReferences.Instance.PlayerInventory;

        //--Add Item to Player Inventory
        inventory.AddItem( _item );

        //--Play Dialogue
        inventory.OnItemGet?.Invoke( _item, _count );

        //--Set internal bool as true
        _pickedUp = true;

        //--For an object to save, the saving code itself must be on an ACTIVE gameobject. therefore we can't
        //--just set this object inactive unless i make a whole separate script JUST to save items
        //--...so i am forced to disable the sprite renderer and box collider...
        // gameObject.SetActive( false );
        ShowPickup( false );
    }

    private void ShowPickup( bool hide ){
        _spriteRenderer.enabled = hide;
        _boxCollider.enabled = hide;
    }

    public object CaptureState(){
        Debug.Log( $"item pickup CaptureState(), _pickedup is: {_pickedUp}" );
        return PickedUp;
    }

    public void RestoreState( object state ){
        Debug.Log( $"item pickup RestoreState(), _pickedup is: {_pickedUp}" );
        Debug.Log( $"item pickup RestoreState(), savedata bool is: {(bool)state}" );
        _pickedUp = (bool)state;
        Debug.Log( $"item pickup RestoreState(), _pickedup is: {_pickedUp}" );

        if( _pickedUp )
            ShowPickup( false );
    }
    
}
