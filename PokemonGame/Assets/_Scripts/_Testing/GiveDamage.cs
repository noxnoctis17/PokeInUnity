using UnityEngine;

public class GiveDamage : MonoBehaviour, IInteractable
{
    public void Interact(){
        // ConditionsDB.Init();
        var firstMon = PlayerReferences.Instance.PlayerParty.PartyPokemon[0];
        var secondMon = PlayerReferences.Instance.PlayerParty.PartyPokemon[1];
        var thirdMon = PlayerReferences.Instance.PlayerParty.PartyPokemon[2];
        firstMon.DecreaseHP( firstMon.MaxHP );
        firstMon.SetSevereStatus( ConditionID.FNT );

        secondMon.DecreaseHP( secondMon.MaxHP/2 );

        foreach( var move in thirdMon.ActiveMoves ){
            move.PP -= 10;
            Debug.Log( $"{thirdMon.PokeSO.Name}'s {move.MoveSO.Name} had its PP reduced to {move.PP}" );
        }

        Debug.Log( $"{firstMon.PokeSO.Name}'s hp reduced to: {firstMon.CurrentHP}! Its status condition is: {firstMon.SevereStatus.ID}" );
        Debug.Log( $"{secondMon.PokeSO.Name}'s hp reduced to: {secondMon.CurrentHP}! Its status condition is: {secondMon.SevereStatus?.ID}" );
    }
}
