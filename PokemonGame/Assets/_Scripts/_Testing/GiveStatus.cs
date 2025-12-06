using UnityEngine;

public class GiveStatus : MonoBehaviour, IInteractable
{
    public void Interact(){
        // ConditionsDB.Init();
        var firstMon = PlayerReferences.Instance.PlayerParty.PartyPokemon[0];
        var secondMon = PlayerReferences.Instance.PlayerParty.PartyPokemon[1];
        var thirdMon = PlayerReferences.Instance.PlayerParty.PartyPokemon[2];
        firstMon.SetSevereStatus( StatusConditionID.PSN );
        secondMon.SetSevereStatus( StatusConditionID.BRN );
        thirdMon.SetSevereStatus( StatusConditionID.FBT );

        // Debug.Log( $"{firstMon.PokeSO.pName}'s status condition is: {firstMon.SevereStatus.ID}" );
        // Debug.Log( $"{secondMon.PokeSO.pName}'s status condition is: {secondMon.SevereStatus.ID}" );
        // Debug.Log( $"{thirdMon.PokeSO.pName}'s status condition is: {thirdMon.SevereStatus.ID}" );
    }
}
