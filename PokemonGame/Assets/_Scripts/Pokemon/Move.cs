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

    public float OverrideAttackingStat( Pokemon pokemon, Move move, float defaultStat )
    {
        Debug.Log( $"[Override Attacking Stat] {pokemon.NickName}'s {move.MoveSO.Name} is causing the attacking stat to be overridden!" );

        if( move.MoveSO.Name == "Body Press" )
        {
            Debug.Log( $"[Override Attacking Stat] {pokemon.NickName}'s {move.MoveSO.Name} made the attack with its Defense instead!" );
            return pokemon.Defense;
        }

        return defaultStat;
    }

    public Move( MoveSaveData saveData ){
        MoveSO = MoveDB.GetMoveByName( saveData.MoveName );
        PP = saveData.PP;
        MoveType = saveData.MoveType;
        MovePower = saveData.MovePower;
    }

    public void RestorePP( int amount ){
        PP = Mathf.Clamp( PP + amount, 0, MoveSO.PP );
    }

    public MoveSaveData CreateSaveData(){
        var saveData = new MoveSaveData(){
            MoveName = MoveSO.Name,
            PP = PP,
            MoveType = MoveType,
            MovePower = MovePower,
        };

        return saveData;
    }

}

[Serializable]
public class MoveSaveData
{
    public string MoveName;
    public int PP;
    public PokemonType MoveType;
    public int MovePower;
}
