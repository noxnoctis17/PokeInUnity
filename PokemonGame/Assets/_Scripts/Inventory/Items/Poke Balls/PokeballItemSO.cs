using UnityEngine;

[CreateAssetMenu( menuName = "Items/Pokeball Item" )]
public class PokeballItemSO : ItemSO
{
    [SerializeField] private float _catchRate;
    public float CatchRate => _catchRate;

    public override bool Use( Pokemon pokemon ){
        return true;
    }

    public override bool CheckIfUsable( Pokemon pokemon ){
        //--Battle Use, to catch a wild pokemon
        if( BattleSystem.Instance != null ){
            if( GameStateController.Instance.CurrentStateEnum == GameStateController.GameStateEnum.BattleState )
                if( BattleSystem.Instance.BattleType != BattleType.TrainerSingles || BattleSystem.Instance.BattleType != BattleType.TrainerDoubles )
                    return true;
        }

        //--Overworld Use to change Pokemon's current Ball
        if( pokemon.SevereStatus != null && pokemon.SevereStatus.ID != ConditionID.FNT )
                return true;

        return false;
    }
}
