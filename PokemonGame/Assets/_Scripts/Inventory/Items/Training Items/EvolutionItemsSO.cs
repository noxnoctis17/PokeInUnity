using UnityEngine;

[CreateAssetMenu( menuName = "Items/Training Item/Evolution Item" )]
public class EvolutionItemsSO : ItemSO
{
    public override bool Use( Pokemon pokemon ){
        return true;
    }

    public override bool CheckIfUsable( Pokemon pokemon ){
        var canUse = pokemon.CheckForEvolution( this );

        if( canUse != null )
            return true;
        else
            return false;
    }
}
