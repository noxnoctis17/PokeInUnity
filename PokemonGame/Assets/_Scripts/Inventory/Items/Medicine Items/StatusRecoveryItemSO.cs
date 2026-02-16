using UnityEngine;

[CreateAssetMenu( menuName = "Items/Medicine/Status Recovery Item" )]
public class StatusRecoveryItemSO : ItemSO
{
    [Header( "Status" )]
    [SerializeField] private SevereConditionID _severeStatus;
    [SerializeField] private VolatileConditionID _volatileStatus;

    [TextArea(3, 10)]
    [SerializeField] private string _recoverText;
    [SerializeField] private bool _restoreAllStatus; //--Excluding FNT
    [SerializeField] private bool _revive; //--Cure FNT status + heal max hp
    [SerializeField] private bool _maxRevive; //--Cure FNT status + heal max hp

    public override bool Use( Pokemon pokemon ){
        //--Revive
        if( pokemon.SevereStatus != null && pokemon.SevereStatus.ID == SevereConditionID.FNT ){
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
        if( _restoreAllStatus || _severeStatus != SevereConditionID.None || _volatileStatus != VolatileConditionID.None )
        {
            if( pokemon.SevereStatus == null && pokemon.VolatileStatuses != null )
                return false;

            if( _restoreAllStatus )
            {
                pokemon.CureSevereStatus();
                pokemon.ClearAllVolatileStatus();
            }
            else
            {
                if( pokemon.SevereStatus != null && pokemon.SevereStatus.ID == _severeStatus )
                {
                    pokemon.CureSevereStatus();
                }
                else if( pokemon.VolatileStatuses != null )
                {
                    for( int i = 0; i < pokemon.VolatileStatuses.Count; i++ )
                    {
                        if( pokemon.VolatileStatuses.ContainsKey( _volatileStatus ) )
                            pokemon.CureVolatileStatus( _volatileStatus );
                    }
                }
                else
                    return false;

            }
        }

        return true;
    }

    public override bool CheckIfUsable( Pokemon pokemon ){
        if( pokemon.SevereStatus == null )
            return false;

        //--Revive Item
        if( _revive && pokemon.SevereStatus.ID == SevereConditionID.FNT || _maxRevive && pokemon.SevereStatus.ID == SevereConditionID.FNT )
            return true;

        //--Status Item
        if( _restoreAllStatus )
            return true;

        if( _severeStatus != pokemon.SevereStatus.ID )
            return false;

        return true;
    }

    public override string UseText( Pokemon pokemon ){
        return $"You used {ItemName}! {pokemon.NickName} {_recoverText}";
    }

}
