using System.Collections.Generic;
using UnityEngine;

public class MoveDB
{
    private static Dictionary<string, MoveBaseSO> _moveDB;

    public static void Init(){
        _moveDB = new();

        var dbArray = Resources.LoadAll<MoveBaseSO>( "" );
        foreach( var moveSO in dbArray ){
            if( _moveDB.ContainsKey( moveSO.MoveName ) ){
                Debug.LogError( $"Duplicate Move: {moveSO.MoveName}" );
                continue;
            }

            _moveDB[moveSO.MoveName] = moveSO;

        }
    }

    public static MoveBaseSO GetMoveByName( string moveName ){
        if( !_moveDB.ContainsKey( moveName ) ){
            Debug.LogError( "Move not found in Move Database!" );
            return null;
        }
        
        return _moveDB[moveName];
    }

}
