using UnityEngine;

[CreateAssetMenu( menuName = "Items/TM/TM Item" )]
public class TMItemSO : ItemSO
{
    [SerializeField] private MoveSO _moveSO;

    public MoveSO MoveSO => _moveSO;

    public override bool Use( Pokemon pokemon ){
        return pokemon.CheckHasMove( _moveSO );
    }

    public override bool CheckIfUsable( Pokemon pokemon ){
        if( !pokemon.CheckCanLearnMove( _moveSO ) )
            return false;

        return true;
    }
}
