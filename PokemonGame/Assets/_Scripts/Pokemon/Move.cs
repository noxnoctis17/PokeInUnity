using System;
using UnityEngine;

[Serializable]
public class Move
{
    [SerializeField] private MoveSO _moveSO;
    public MoveSO MoveSO { get => _moveSO; set => _moveSO = value; }
    public int PP { get; set; }
    public PokemonType MoveType { get; private set; }
    public int MovePower { get; private set; }

    public Move( MoveSO mBase ){
        MoveSO = mBase;
        PP = MoveSO.PP;
        MoveType = MoveSO.Type;
        MovePower = MoveSO.Power;
    }

    //--Mostly for shit like Pixilate, Liquid Voice, etc.
    public void OverrideMoveType( PokemonType type )
    {
        MoveType = type;
    }

    public void OverrideMovePower( int power )
    {
        MovePower = power;
    }

    public Move( MoveSaveData saveData ){
        MoveSO = MoveDB.GetMoveByName( saveData.MoveName );
        PP = saveData.PP;
    }

    public void RestorePP( int amount ){
        PP = Mathf.Clamp( PP + amount, 0, MoveSO.PP );
    }

    public MoveSaveData CreateSaveData(){
        var saveData = new MoveSaveData(){
            MoveName = MoveSO.Name,
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
