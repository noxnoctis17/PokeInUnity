using UnityEngine;

[CreateAssetMenu( menuName = "Items/Medicine/Status Recovery Item" )]
public class StatusRecoveryItemSO : ItemSO
{
    [Header( "Status" )]
    [SerializeField] private ConditionID _status;

    [TextArea(3, 10)]
    [SerializeField] private string _recoverText;
    [SerializeField] private bool _restoreAllStatus; //--Excluding FNT
    [SerializeField] private bool _revive; //--Cure FNT status + heal max hp
    [SerializeField] private bool _maxRevive; //--Cure FNT status + heal max hp

    public override bool Use( Pokemon pokemon ){
        //--Revive
        if( pokemon.SevereStatus != null && pokemon.SevereStatus.ID == ConditionID.FNT ){
            if( _revive ){
                pokemon.CureSevereStatus();
                pokemon.IncreaseHP( pokemon.MaxHP/2 );
                return true;
            }

            if( _maxRevive ){
                pokemon.CureSevereStatus();
                pokemon.IncreaseHP( pokemon.MaxHP );
                return true;
            }

            return false;
        }

        //--Status Item
        if( _restoreAllStatus || _status != ConditionID.NONE ){
            if( pokemon.SevereStatus == null && pokemon.VolatileStatus != null )
                return false;

            if( _restoreAllStatus ){
                pokemon.CureSevereStatus();
                pokemon.CureVolatileStatus();
            }
            else{
                if( pokemon.SevereStatus != null && pokemon.SevereStatus.ID == _status )
                    pokemon.CureSevereStatus();
                else if( pokemon.VolatileStatus != null && pokemon.VolatileStatus.ID == _status )
                    pokemon.CureVolatileStatus();
                else
                    return false;

            }
        }

        return true;
    }

    public override bool CheckIfUsable( Pokemon pokemon ){
        if( pokemon.SevereStatus.ID == ConditionID.FNT )
            return false;

        //--Status Item
        if( _restoreAllStatus || _status != ConditionID.NONE )
            if( pokemon.SevereStatus == null && pokemon.VolatileStatus == null )
                return false;

        if( _status != pokemon.SevereStatus.ID )
            return false;

        if( _status != pokemon.VolatileStatus.ID )
            return false;

        return true;
    }

    public override string UseText( Pokemon pokemon ){
        return $"You used a {ItemName}! {pokemon.PokeSO.pName} {_recoverText}";
    }

}
