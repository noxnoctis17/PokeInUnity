using System.Collections;
using UnityEngine;

public class InactivesInitializer : MonoBehaviour
{
    private IEnumerator Start(){
        yield return new WaitForEndOfFrame();
        var partyDisplays = FindObjectsOfType<PartyDisplay>( true );
        var bagDisplays = FindObjectsOfType<BagDisplay>( true );

        //--Party Displays
        foreach( var obj in partyDisplays )
            obj.Init();

        //--Bag Displays
        foreach( var obj in bagDisplays )
            obj.Init();
    }
}
