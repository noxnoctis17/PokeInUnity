using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NPC_Base : MonoBehaviour, IInteractable, ISavable
{
    [SerializeField] private DialogueSO _dialogueSO;
    [SerializeField] private bool _isItemGiver;
    [SerializeField] private ItemGiver[] _itemGiver;
    public DialogueSO DialogueSO => _dialogueSO;
    
    public void Interact(){
        Debug.Log( $"You've Interacted With {this}" );

        foreach( DialogueResponseEvents responseEvents in GetComponents<DialogueResponseEvents>() ){
            if( responseEvents.DialogueSO == _dialogueSO ){
                DialogueManager.Instance.OnHasResponseEvents?.Invoke( responseEvents );
                break;
            }
        }

        DialogueManager.Instance.OnDialogueEvent?.Invoke( DialogueSO );

    }

    public void GiveItem(){
        if( !_isItemGiver )
            return;

        var itemGiver = _itemGiver.FirstOrDefault( item => item.CanGiveItem() );
        if( itemGiver != null )
            StartCoroutine( itemGiver.GiveItem() );
    }

    public IEnumerator GiveAllItems(){
        if( !_isItemGiver )
            yield break;

        foreach( var itemGiver in _itemGiver ){
            if( itemGiver.CanGiveItem() )
                yield return itemGiver.GiveItem();
        }
    }

    public void UpdateDialogueObject( DialogueSO dialogueSO ){
        _dialogueSO = dialogueSO;
    }

    public object CaptureState(){
        var canGiveItems = _itemGiver.Select( itemGiver => itemGiver.ItemGiven ).ToList();

        var saveData = new ItemGiverSaveData()
        {
            ItemGiven = canGiveItems,
        };

        return saveData;

    }

    public void RestoreState( object state ){
        var saveData = (ItemGiverSaveData)state;

        for( int i = 0; i < _itemGiver.Length; i++ ){
            _itemGiver[i].SetSaveData( saveData.ItemGiven[i] );
        }

    }
}

[Serializable]
public class ItemGiverSaveData
{
    public List<bool> ItemGiven;
}
