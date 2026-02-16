using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Move
{
    [SerializeField] private MoveSO _moveSO;
    public MoveSO MoveSO { get => _moveSO; set => _moveSO = value; }
    public int PP { get; set; }
    public PokemonType MoveType { get; private set; }
    public int MovePower { get; private set; }
    public int Accuracy { get; private set; }
    public AccuracyType AccuracyType { get; private set; }
    public MovePriority Priority { get; private set; }
    public MoveTarget MoveTarget { get; private set; }
    public int HealAmount { get; private set; }
    public MoveEffects MoveEffects { get; private set; }

    public Move( MoveSO mBase )
    {
        MoveSO = mBase;
        PP = MoveSO.PP;
        MoveType = MoveSO.Type;
        MovePower = MoveSO.Power;
        Accuracy = MoveSO.Accuracy;
        AccuracyType = MoveSO.AccuracyType;
        Priority = MoveSO.MovePriority;
        MoveTarget = MoveSO.MoveTarget;
        HealAmount = MoveSO.HealAmount;
        MoveEffects = MoveSO.MoveEffects;
    }

    public Move( MoveSaveData saveData )
    {
        MoveSO = MoveDB.GetMoveByName( saveData.MoveName );
        PP = saveData.PP;
        MoveType = saveData.MoveType;
        MovePower = saveData.MovePower;
        Accuracy = MoveSO.Accuracy;
        AccuracyType = MoveSO.AccuracyType;
        MoveTarget = saveData.MoveTarget;
        HealAmount = saveData.HealAmount;
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

    public MovePriority OverridePriority( MovePriority priority )
    {
        return priority;
    }

    public MoveTarget OverrideMoveTarget( MoveTarget target )
    {
        return target;
    }

    public void OverrideAccuracyType( AccuracyType value )
    {
        AccuracyType = value;
    }

    public void OverrideHealing( int healing )
    {
        HealAmount = healing;
    }

    public void OverrideMoveEffects( MoveEffects effects )
    {
        MoveEffects = effects;
    }

    public void RestoreMoveEffects()
    {
        MoveEffects = MoveSO.MoveEffects;
    }

    public void RestorePP( int amount )
    {
        PP = Mathf.Clamp( PP + amount, 0, MoveSO.PP );
    }

    public MoveSaveData CreateSaveData()
    {
        var saveData = new MoveSaveData()
        {
            MoveName = MoveSO.Name,
            PP = PP,
            MoveType = MoveType,
            MovePower = MovePower,
            Accuracy = Accuracy,
            AccuracyType = AccuracyType,
            MoveTarget = MoveTarget,
            HealAmount = HealAmount,
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
    public int Accuracy;
    public AccuracyType AccuracyType;
    public MoveTarget MoveTarget;
    public int HealAmount;
}
