using System;
using UnityEngine;

public enum PokeBallType { None, PokeBall, GreatBall, UltraBall, }

[CreateAssetMenu( menuName = "Items/Pokeball Item" )]
public class PokeballItemSO : ItemSO
{
    [SerializeField] private PokeBallType _ballType;
    [SerializeField] private float _catchRate;
    public float CatchRate => _catchRate;
    public PokeBallType BallType => _ballType;

    public override bool Use( Pokemon pokemon )
    {
        //--Battle Use, to catch a wild pokemon
        if( BattleSystem.Instance != null )
        {
            if( GameStateController.Instance.CurrentStateEnum == GameStateController.GameStateEnum.BattleState )
                if( BattleSystem.Instance.BattleType != BattleType.TrainerSingles || BattleSystem.Instance.BattleType != BattleType.TrainerDoubles )
                    return true;
                else
                    return false;
        }

        //--Overworld Use to change Pokemon's current Ball
        if( pokemon.SevereStatus != null && pokemon.SevereStatus.ID == SevereConditionID.FNT )
            return false;
        
        if( pokemon.CurrentBallType == _ballType )
            return false;

        pokemon.ChangeCurrentBall( _ballType );
        return true;
    }

    public override bool CheckIfUsable( Pokemon pokemon ){
        //--Battle Use, to catch a wild pokemon
        if( BattleSystem.Instance != null )
        {
            if( GameStateController.Instance.CurrentStateEnum == GameStateController.GameStateEnum.BattleState )
            {
                if( BattleSystem.Instance.BattleType != BattleType.TrainerSingles || BattleSystem.Instance.BattleType != BattleType.TrainerDoubles )
                    return true;
            }
        }

        return false;
    }

    public override string UseText( Pokemon pokemon )
    {
        return $"{pokemon.NickName} was placed inside your extra {ItemName}!";
    }
}
