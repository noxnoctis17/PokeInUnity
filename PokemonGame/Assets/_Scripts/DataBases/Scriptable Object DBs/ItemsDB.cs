using System.Collections.Generic;
using UnityEngine;

public class ItemsDB : MonoBehaviour
{
    private static Dictionary<string, ItemSO> _itemDB;

    public static void Init(){
        _itemDB = new();

        var dbArray = Resources.LoadAll<ItemSO>( "" );
        foreach( var itemSO in dbArray ){
            if( _itemDB.ContainsKey( itemSO.ItemName ) ){
                Debug.LogError( $"Duplicate Item: {itemSO.ItemName}" );
                continue;
            }

            _itemDB[itemSO.ItemName] = itemSO;

        }
    }

    public static ItemSO GetItemByName( string itemName ){
        if( !_itemDB.ContainsKey( itemName ) ){
            Debug.LogError( "Move not found in Move Database!" );
            return null;
        }
        
        return _itemDB[itemName];
    }
}
