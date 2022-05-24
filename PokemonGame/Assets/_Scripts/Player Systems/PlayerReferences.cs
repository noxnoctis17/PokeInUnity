using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

public class PlayerReferences : MonoBehaviour
{
   public static Transform PlayerTransform {get; private set;}
   public static AIPath AIPath {get; private set;}

   private void Awake(){
       PlayerTransform = transform;
       AIPath = GetComponent<AIPath>();
   }

}
