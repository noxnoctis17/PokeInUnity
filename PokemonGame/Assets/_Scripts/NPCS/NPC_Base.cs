using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPC_Base : MonoBehaviour, IInteractable
{
    public void Interact(){
        Debug.Log( "You've Interacted With an NPC" );
    }
}
