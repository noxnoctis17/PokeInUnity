using UnityEngine;

public class PlayerReferences : MonoBehaviour
{
//=======================[INSTANCE]=============================
   private static PlayerReferences _instance;
   public static PlayerReferences Instance => _instance;

//====================[PLAYER OBJECT]===========================
   [SerializeField] private Transform _playerCenter;
   public PlayerSaving PlayerSaving { get; private set; }
   public Transform PlayerSpriteTransform { get; private set; }
   public PlayerMovement PlayerMovement { get; private set; }
   public PlayerController PlayerController { get; private set; }
   public PokemonParty PlayerParty { get; private set; }
   public Inventory PlayerInventory { get; private set; }
   public Transform PlayerTransform { get; private set; }
   public Transform PlayerCenter { get; private set; }
   public PlayerInput PlayerInput { get; private set; }

//========================[CAMERA]==============================
   [SerializeField] private Transform _mainCameraTransform;
   public static Transform MainCameraTransform { get; private set; }

//=======================[ACTIONS]==============================
   

//==============================================================

   private void OnEnable() {
      //--Instance
      _instance = this;

      //--Player
      PlayerSaving            = GetComponentInChildren<PlayerSaving>();
      PlayerSpriteTransform   = GetComponentInChildren<SpriteRenderer>().gameObject.transform;
      PlayerMovement          = GetComponent<PlayerMovement>();
      PlayerController        = GetComponent<PlayerController>();
      PlayerParty             = GetComponent<PokemonParty>();
      PlayerInventory         = GetComponent<Inventory>();
      PlayerTransform         = transform;
      PlayerCenter            = _playerCenter;
      PlayerInput             = PlayerMovement.PlayerInput;
      //--PlayerFlags         = GetComponent<PlayerFlags>();

      //--Current Camera
      MainCameraTransform = _mainCameraTransform;
      
   }

}
