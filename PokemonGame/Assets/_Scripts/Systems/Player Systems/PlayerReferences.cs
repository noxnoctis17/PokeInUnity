using UnityEngine;
using Pathfinding;

public class PlayerReferences : MonoBehaviour
{
//-----------TRANSFORM, CAMERA, AI PATH---------------------------------
   public static Transform PlayerTransform { get; private set; }
   public static Transform MainCameraTransform { get; private set; }
   [SerializeField] private Transform _mainCameraTransform;
   public static AIPath AIPath {get; private set;}
//--------------------------------------------------------------

//---------POKEMON BATTLE POSITIONS-----------------------------
   [SerializeField] private Transform ActivePokePositions;
   [SerializeField] private Transform _poke1;
   [SerializeField] private Transform _poke2;
   [SerializeField] private Transform _poke3;
   [SerializeField] private Transform _poke4;
   public static Transform Poke1;
   public static Transform Poke2;
   public static Transform Poke3;
   public static Transform Poke4;
//--------------------------------------------------------------   


   private void OnEnable() {
      PlayerTransform = transform;
      MainCameraTransform = _mainCameraTransform;
      AIPath = GetComponent<AIPath>();

      Poke1 = _poke1;
      Poke2 = _poke2;
      Poke3 = _poke3;
      Poke4 = _poke4;
   }

}
