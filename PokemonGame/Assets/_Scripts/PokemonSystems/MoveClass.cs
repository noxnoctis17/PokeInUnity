using System;

public class MoveClass
{
    public MoveBaseSO MoveSO { get; set; }
    public int PP { get; set; }

    public MoveClass( MoveBaseSO mBase ){
        MoveSO = mBase;
        PP = MoveSO.PP;
    }

    public MoveClass( MoveSaveData saveData ){
        MoveSO = MoveDB.GetMoveByName( saveData.MoveName );
        PP = saveData.PP;
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
