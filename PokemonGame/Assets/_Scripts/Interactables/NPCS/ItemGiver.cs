using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class ItemGiver
{
    [SerializeField] private ItemSO _item;
    [SerializeField] private int _count = 1;
    public bool ItemGiven { get; private set; }

    public IEnumerator GiveItem(){
        PlayerReferences.Instance.PlayerInventory.AddItem( _item );
        ItemGiven = true;
        yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"You received X  {_count} {_item.ItemName}!" );
    }

    public bool CanGiveItem(){
        return _item != null && !ItemGiven;
    }

    public void SetSaveData( bool itemGiven ){
        ItemGiven = itemGiven;
    }

}
