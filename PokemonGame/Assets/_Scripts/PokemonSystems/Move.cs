using System;
using UnityEngine;

public class MoveClass
{
    public MoveSO MoveSO { get; set; }
    public int PP { get; set; }

    public MoveClass( MoveSO mBase ){
        MoveSO = mBase;
        PP = MoveSO.PP;
    }

    public MoveClass( MoveSaveData saveData ){
        MoveSO = MoveDB.GetMoveByName( saveData.MoveName );
        PP = saveData.PP;
    }

    public void RestorePP( int amount ){
        PP = Mathf.Clamp( PP + amount, 0, MoveSO.PP );
    }

    public MoveSaveData CreateSaveData(){
        var saveData = new MoveSaveData(){
            MoveName = MoveSO.MoveName,
            PP = PP,
        };

        return saveData;
    }

}

[Serializable]
public class MoveSaveData
{
    public string MoveName;
    public int PP;
}
