using UnityEngine;

[CreateAssetMenu( menuName = "Items/Medicine/PP Recovery Item" )]
public class PPItemSO : ItemSO
{
    [Header( "PP" )]
    [SerializeField] private int _ppHealAmnt;
    [SerializeField] private bool _restoreMaxPP;


    public override bool Use( Pokemon pokemon ){
        if( pokemon.SevereStatus != null && pokemon.SevereStatus.ID == ConditionID.FNT )
            return false;

        int fullPPmoves = 0;
        foreach( var move in pokemon.ActiveMoves ){
            Debug.Log( $"{move.MoveSO.Name} PP: {move.PP}/{move.MoveSO.PP}" );
            if( move.PP == move.MoveSO.PP ){
                Debug.Log( $"moves with full PP: {fullPPmoves}" );
                fullPPmoves++;
                Debug.Log( $"moves with full PP: {fullPPmoves}" );
            }
        }

        if( fullPPmoves >= pokemon.ActiveMoves.Count )
            return false;

        if( _restoreMaxPP ){
            pokemon.ActiveMoves.ForEach( m => m.RestorePP( m.MoveSO.PP ) );
        }
        else if( _ppHealAmnt > 0 ){
            pokemon.ActiveMoves.ForEach( m => m.RestorePP( _ppHealAmnt ) );
        }

        return true;
    }

    public override bool CheckIfUsable( Pokemon pokemon ){
        if( pokemon.SevereStatus != null && pokemon.SevereStatus.ID == ConditionID.FNT )
            return false;

        //--PP Restore
        if( _restoreMaxPP || _ppHealAmnt > 0 ){
            int fullPPmoves = 0;

            foreach( var move in pokemon.ActiveMoves )
                if( move.PP == move.MoveSO.PP )
                    fullPPmoves++;

            if( fullPPmoves == 4 )
                return false;
        }      
        
        return true;
    }

    public override string UseText( Pokemon pokemon ){
        return $"You used a {ItemName}! All of {pokemon.PokeSO.Name}'s moves recovered {_ppHealAmnt} PP!";
    }

}
