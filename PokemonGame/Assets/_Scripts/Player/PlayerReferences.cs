using UnityEngine;
using Pathfinding;
using System;
using UnityEngine.EventSystems;

public class PlayerReferences : MonoBehaviour
{
//=======================[INSTANCE]=============================
   private static PlayerReferences _instance;
   public static PlayerReferences Instance => _instance;

//====================[PLAYER OBJECT]===========================
   [SerializeField] private Transform _playerCenter;
   public Transform PlayerTransform { get; private set; }
   public Transform PlayerCenter { get; private set; }
   public Transform PlayerSpriteTransform { get; private set; }
   public PlayerInput PlayerInput { get; private set; }
   public PlayerMovement PlayerMovement { get; private set; }
   public PlayerController PlayerController { get; private set; }
   public PokemonParty PlayerParty { get; private set; }

//========================[CAMERA]==============================
   [SerializeField] private Transform _mainCameraTransform;
   public static Transform MainCameraTransform { get; private set; }

//==================[DIRECTIONAL LIGHTS]========================
   [SerializeField] private Transform _sunTransform;
   [SerializeField] private Transform _moonTransform;
   public Transform SunTransform { get; private set; }
   public Transform MoonTransform { get; private set; }

//=======================[A* AI]==============================
   public AIPath AIPath {get; private set;}

//=======================[ACTIONS]==============================
   

//==============================================================

   private void OnEnable() {
      //--Instance
      _instance = this;

      //--Player
      PlayerTransform = transform;
      PlayerCenter = _playerCenter;
      PlayerSpriteTransform = GetComponentInChildren<SpriteRenderer>().gameObject.transform;
      PlayerMovement = GetComponent<PlayerMovement>();
      PlayerController = GetComponent<PlayerController>();
      PlayerParty = GetComponent<PokemonParty>();
      PlayerInput = PlayerMovement.PlayerInput;

      //--Current Camera
      MainCameraTransform = _mainCameraTransform;

      //--Current Directional Lights
      SunTransform = _sunTransform;
      MoonTransform = _moonTransform;
      
      //--A* AI Path, will likely not use
      AIPath = GetComponent<AIPath>();
   }

}
