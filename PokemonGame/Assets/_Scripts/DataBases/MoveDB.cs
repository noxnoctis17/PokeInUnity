using System.Collections.Generic;
using UnityEngine;

public class MoveDB
{
    private static Dictionary<string, MoveSO> _moveDB;

    public static void Init(){
        _moveDB = new();

        var dbArray = Resources.LoadAll<MoveSO>( "" );
        foreach( var moveSO in dbArray ){
            if( _moveDB.ContainsKey( moveSO.Name ) ){
                Debug.LogError( $"Duplicate Move: {moveSO.Name}" );
                continue;
            }

            _moveDB[moveSO.Name] = moveSO;

        }
    }

    public static MoveSO GetMoveByName( string moveName ){
        if( !_moveDB.ContainsKey( moveName ) ){
            Debug.LogError( "Move not found in Move Database!" );
            return null;
        }
        
        return _moveDB[moveName];
    }

}
