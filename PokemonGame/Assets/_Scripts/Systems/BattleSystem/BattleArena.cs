using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Cinemachine;

public class BattleArena : MonoBehaviour
{
    private BattleSystem _battleSystem;
    private BattleType _battleType;
    [SerializeField] private CinemachineVirtualCamera _singleTargetCamera;
    [SerializeField] private CinemachineFreeLook _mainTargetGroupCamera;
    private WildPokemon _wildEncounter;
    private GameObject _enemyTrainer1, _enemyTrainer2;
    [SerializeField] private GameObject _arenaContainer; //--parent object of the entire Battle Arena. Should always be at the same world position as the _stagePivot, which gets placed according to BattleType
    [SerializeField] private GameObject _arenaPivot; //--pivot will be placed according to encounter ex: same position as wild pokemon
    [SerializeField] private float _singlesArenaSize; //--wire disc radius for singles
    [SerializeField] private float _doublesArenaSize; //--wire disc radius for doubles
    [SerializeField] private GameObject _arenaGizmoCenter; //--wire disc center
    [SerializeField] private GameObject _singlesTrainer1, _singlesTrainer2; //--well, whatever
    [SerializeField] private GameObject _doublesTrainer1, _doublesTrainer2, _doublesTrainer3, _doublesTrainer4; //--player side odds vs evens
    [SerializeField] private GameObject _singlesUnit1, _singlesUnit2; //--again, whatever
    [SerializeField] private GameObject _doublesUnit1, _doublesUnit2, _doublesUnit3, _doublesUnit4; //--player side odds vs evens
    private List<GameObject> _activePositionsList = new List<GameObject>();
    public List<GameObject> ActivePositionsList => _activePositionsList;

    public IEnumerator PrepareArena( BattleSystem battleSystem ){
        _battleSystem = battleSystem;
        _battleType = _battleSystem.BattleType;

        switch( _battleType ){

            case BattleType.WildBattle_1v1 :

                yield return WildBattle_1v1(); //--Wild Battle, 1v1

            break;

            case BattleType.WildBattle_2v2 :

                //--I'm not sure i'll ever have one of these but who knows. i think the Arcanine Guardian encounters will be the only ones

            break;

            case BattleType.TrainerSingles :

                yield return TrainerSingles(); //--Trainer Battle, Singles

            break;

            case BattleType.TrainerDoubles_1v1 :

                yield return TrainerDoubles_1v1(); //--Trainer Battle, Doubles 1v1

            break;

            case BattleType.TrainerDoubles_2v1 :

            break;

            case BattleType.TrainerDoubles_2v2 :

            break;

            
        }

        yield return null;
    }

    //--make a function that can automate setting up each BattleUnit in the arena, by accepting a game object
    //--probably make a separate function for the player, and possibly wild mons and trainers, since their in-world objects will be used instead

    private IEnumerator SetPivot( Vector3 targetPosition, GameObject pivotObj ){
        yield return null;
        Vector3 pivotOffset = _arenaContainer.transform.position - pivotObj.transform.position;
        _arenaPivot.transform.position = targetPosition;
        _arenaContainer.transform.position = _arenaPivot.transform.position + pivotOffset;
    }

    private IEnumerator SetCameras( GameObject singleCameraInitialTarget ){
        _singleTargetCamera.LookAt = singleCameraInitialTarget.transform;
        _singleTargetCamera.gameObject.SetActive( true );
        yield return new WaitForSeconds( 1f );

        _mainTargetGroupCamera.gameObject.SetActive( true );
        _singleTargetCamera.gameObject.SetActive( false );
    }

    private void ClearCameras(){
        _singleTargetCamera.LookAt = null;
        _singleTargetCamera.gameObject.SetActive( false );
        _mainTargetGroupCamera.gameObject.SetActive( false );
    }

    private IEnumerator MovePlayerIntoPosition( Vector3 position ){
        yield return new WaitForSeconds( 0.25f );
        Debug.Log( "move player into position from battle arena" );
        PlayerReferences.Instance.PlayerMovement.MovePlayerIntoBattlePosition( position );
    }

    private GameObject GrabWildEncounter(){
        _wildEncounter = _battleSystem.EncounteredPokemon;
        return _wildEncounter.gameObject;
    }

    private GameObject GrabEnemyTrainer1(){
        _enemyTrainer1 = _battleSystem.EnemyTrainerParty.gameObject;
        return _enemyTrainer1;
    }

    private void ClearActivePositionsList(){
        for( int i = _activePositionsList.Count - 1; i >= 0; i-- ){
            _activePositionsList[i].SetActive( false );
            _activePositionsList.RemoveAt( i );
        }
    }

    public void AfterBattleCleanup(){
        ClearActivePositionsList();
        ClearCameras();

        _wildEncounter = null;
        _enemyTrainer1 = null;
        _enemyTrainer2 = null;
    }

    private IEnumerator WildBattle_1v1(){
        //--Activate relevant positions
        _singlesTrainer1.SetActive( true ); //--Player
        _singlesUnit1.SetActive( true ); //--Player Unit
        _singlesUnit2.SetActive( true ); //--Wild Pokemon
        yield return null;

        //--Set BattleUnit component appropriately
        _singlesUnit1.GetComponent<BattleUnit>().enabled = false;
        _singlesUnit2.GetComponent<BattleUnit>().enabled = false;
        yield return null;

        //--Clear relevant sprites that will be replaced by an in-world sprite instead
        ClearSprites( _singlesTrainer1 ); //--trainer 1 will almost always be the player, who will almost always be on-map
        ClearSprites( _singlesUnit2 ); //--clear unit2's sprite because the on-map encounter will take unit2's position
        _singlesUnit2.GetComponentInChildren<PokemonAnimator>().enabled = false; //--disable the animator for the battle unit, we use the wild pokemon's animator instead
        _singlesUnit2.GetComponentInChildren<PokemonShadow>().enabled = false; //--disable the animator for the battle unit, we use the wild pokemon's animator instead
        yield return null;

        //--Add relevant positions to the active positions list
        _activePositionsList.Add( _singlesTrainer1 );
        _activePositionsList.Add( _singlesUnit1 );
        _activePositionsList.Add( _singlesUnit2 );
        yield return null;

        //--Setup relevant Battle Units
        var playerUnit = _singlesUnit1.GetComponent<BattleUnit>();
        playerUnit.Setup( _battleSystem.PlayerParty.GetHealthyPokemon(), _battleSystem.PlayerHUD, _battleSystem );

        var enemyUnit = _battleSystem.EncounteredPokemon.gameObject.GetComponent<BattleUnit>();
        enemyUnit.OnIsAI?.Invoke(); //--enable AI for this unit
        enemyUnit.Setup( _battleSystem.WildPokemon, _battleSystem.EnemyHUD, _battleSystem ); //--REMEMBER!!!!! _singlesUnit2 is not actually being assigned to, it's only here for its position!!
        yield return null;

        //--Assign relevant Battle Units
        _battleSystem.AssignUnits_1v1( playerUnit, enemyUnit );
        yield return null;

        //--Eventually make this get added to the message queue that prints messages on the smaller "alert box" you have planned
        StartCoroutine( _battleSystem.DialogueBox.TypeDialogue( $"A wild {_battleSystem.EnemyUnit.Pokemon.PokeSO.pName} appeared!" ) );

        //--Attempt! to set the pivot of the arena to the wild encounter
        var targetPosition = GrabWildEncounter().transform.position;
        yield return SetPivot( targetPosition, _singlesUnit2 );

        //--Make everyone face the arena center
        LookAtArenaCenter( _singlesTrainer1 );
        LookAtArenaCenter( _singlesUnit1 );
        LookAtArenaCenter( _singlesUnit2 );

        //--Handle Cameras by passing the initial single target camera's target unit
        StartCoroutine( SetCameras( _singlesUnit2 ) );

        //--Move Player into position
        yield return MovePlayerIntoPosition( _singlesTrainer1.transform.position );

        yield return null;

    }

    //--Trainer Battle, Singles
    private IEnumerator TrainerSingles(){
        //--Activate Relevant Positions
        _singlesTrainer1.SetActive( true ); //--Player
        _singlesTrainer2.SetActive( true ); //--Enemy Trainer
        _singlesUnit1.SetActive( true ); //--Player Unit
        _singlesUnit2.SetActive( true ); //--Enemy Trainer Unit
        yield return null;

        //--Set BattleUnit component appropriately ( This is for cases where the unit is on-map )
        _singlesUnit1.GetComponent<BattleUnit>().enabled = true;
        _singlesUnit2.GetComponent<BattleUnit>().enabled = true;
        yield return null;

        //--Clear relevant sprites that will be replaced by an in-world sprite instead
        ClearSprites( _singlesTrainer1 );
        ClearSprites( _singlesTrainer2 );
        yield return null;

        //--Add relevant positions to the active positions list
        _activePositionsList.Add( _singlesTrainer1 );
        _activePositionsList.Add( _singlesTrainer2 );
        _activePositionsList.Add( _singlesUnit1 );
        _activePositionsList.Add( _singlesUnit2 );
        yield return null;

        //--Setup relevant Battle Units
        var playerUnit = _singlesUnit1.GetComponent<BattleUnit>();
        var enemyUnit  = _singlesUnit2.GetComponent<BattleUnit>();
        
        playerUnit.Setup( _battleSystem.PlayerParty.GetHealthyPokemon(), _battleSystem.PlayerHUD, _battleSystem );
        
        enemyUnit.OnIsAI?.Invoke(); //--enable AI for this unit
        enemyUnit.Setup( _battleSystem.EnemyTrainerParty.GetHealthyPokemon(), _battleSystem.EnemyHUD, _battleSystem );

        //--Assign relevant Battle Units
        _battleSystem.AssignUnits_1v1( playerUnit, enemyUnit );
        yield return null;

        //--Attempt! to set the pivot of the arena to the Enemy Trainer's location
        var targetPosition = GrabEnemyTrainer1().transform.position;
        yield return SetPivot( targetPosition, _singlesTrainer2 );

        //--Make everyone face the arena center
        LookAtArenaCenter( _singlesTrainer1 );
        LookAtArenaCenter( _singlesTrainer2 );
        LookAtArenaCenter( _singlesUnit1 );
        LookAtArenaCenter( _singlesUnit2 );

        //--Handle Cameras by passing the initial single target camera's target unit
        StartCoroutine( SetCameras( _singlesUnit2 ) );
        yield return null;

        //--Move Player into position
        yield return MovePlayerIntoPosition( _singlesTrainer1.transform.position );

    }

    private IEnumerator TrainerDoubles_1v1(){
        //--Activate Relevant Positions
        _doublesTrainer1.SetActive( true ); //--Player
        _doublesTrainer2.SetActive( true );
        _doublesUnit1.SetActive( true ); //--Player Unit
        _doublesUnit2.SetActive( true );
        _doublesUnit3.SetActive( true ); //--Player Unit
        _doublesUnit4.SetActive( true );
        yield return null;

        //--Set BattleUnit component appropriately ( This is for cases where the unit is on-map )
        _doublesUnit1.GetComponent<BattleUnit>().enabled = true;
        _doublesUnit2.GetComponent<BattleUnit>().enabled = true;
        _doublesUnit3.GetComponent<BattleUnit>().enabled = true;
        _doublesUnit4.GetComponent<BattleUnit>().enabled = true;
        yield return null;

        //--Clear relevant sprites that will be replaced by an in-world sprite instead
        ClearSprites( _doublesTrainer1 );
        ClearSprites( _doublesTrainer2 );

        //--Make everyone face the arena center
        LookAtArenaCenter( _doublesTrainer1 ); //--Player
        LookAtArenaCenter( _doublesTrainer2 );
        LookAtArenaCenter( _doublesUnit1 ); //--Player Unit
        LookAtArenaCenter( _doublesUnit2 );
        LookAtArenaCenter( _doublesUnit3 ); //--Player Unit
        LookAtArenaCenter( _doublesUnit4 );

    }

    private void LookAtArenaCenter( GameObject obj ){
        obj.transform.rotation.SetLookRotation( _arenaGizmoCenter.transform.position );
    }

    private void ClearSprites( GameObject obj ){
        obj.GetComponentsInChildren<SpriteRenderer>()[0].sprite = null;
    }





#if UNITY_EDITOR

    public void OnDrawGizmos(){
        //--Arena Rings
        Handles.DrawWireDisc( _arenaGizmoCenter.transform.position, Vector3.up , _singlesArenaSize ); //--singles
        Handles.DrawWireDisc( _arenaGizmoCenter.transform.position, Vector3.up , _doublesArenaSize ); //--doubles

    //--Singles Positions

        //--Trainer 1
        Handles.color = Color.cyan;
        if( _singlesTrainer1 != null )
        Handles.DrawWireDisc( _singlesTrainer1.transform.position, Vector3.up, 1f );

        //--Trainer 2
        Handles.color = Color.magenta;
        if( _singlesTrainer2 != null )
        Handles.DrawWireDisc( _singlesTrainer2.transform.position, Vector3.up, 1f );

        //--Unit 1
        Handles.color = Color.cyan;
        if( _singlesUnit1 != null )
        Handles.DrawWireDisc( _singlesUnit1.transform.position, Vector3.up, 1f );

        //--Unit 2
        Handles.color = Color.magenta;
        if( _singlesUnit2 != null )
        Handles.DrawWireDisc( _singlesUnit2.transform.position, Vector3.up, 1f );

    //--Doubles Positions

        //--Trainer 1
        Handles.color = Color.red;
        if( _doublesTrainer1 != null )
        Handles.DrawWireDisc( _doublesTrainer1.transform.position, Vector3.up, 1f );

        //--Trainer 2
        Handles.color = Color.blue;
        if( _doublesTrainer2 != null )
        Handles.DrawWireDisc( _doublesTrainer2.transform.position, Vector3.up, 1f );

        //--Trainer 3
        Handles.color = Color.yellow;
        if( _doublesTrainer3 != null )
        Handles.DrawWireDisc( _doublesTrainer3.transform.position, Vector3.up, 1f );

        //--Trainer 4
        Handles.color = Color.green;
        if( _doublesTrainer4 != null )
        Handles.DrawWireDisc( _doublesTrainer4.transform.position, Vector3.up, 1f );

        //--Unit 1
        Handles.color = Color.red;
        if( _doublesUnit1 != null )
        Handles.DrawWireDisc( _doublesUnit1.transform.position, Vector3.up, 1f );

        //--Unit 2
        Handles.color = Color.blue;
        if( _doublesUnit2 != null )
        Handles.DrawWireDisc( _doublesUnit2.transform.position, Vector3.up, 1f );

        //--Unit 3
        Handles.color = Color.red;
        if( _doublesUnit3 != null )
        Handles.DrawWireDisc( _doublesUnit3.transform.position, Vector3.up, 1f );

        //--Unit 4
        Handles.color = Color.blue;
        if( _doublesUnit4 != null )
        Handles.DrawWireDisc( _doublesUnit4.transform.position, Vector3.up, 1f );

    }


#endif

}
