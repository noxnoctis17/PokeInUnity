using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System;
using DG.Tweening;

public enum PortalDestinationID { A, B, C, D, E, }
public enum PortalType{ Local, NewScene, }

public class Portal : MonoBehaviour
{
    private SceneManagerTWO _sceneManager;
    [SerializeField] private SceneDetails _sceneToLoad; //--If null, this will return an error so we know we fucked up
    [SerializeField] private GameObject _spawnPoint;
    [SerializeField] private PortalDestinationID _destinationID;
    [SerializeField] private PortalType _portalType; //--to test the portal within the same scene/additively loaded scenes
    private GameObject _player;
    public GameObject SpawnPoint => _spawnPoint;
    public PortalDestinationID DestinationID => _destinationID;
    public static Action OnSceneChanged;

    private void Awake(){
        _sceneManager = SceneManagerTWO.Instance;
    }

    private IEnumerator SwitchScene( SceneDetails indoorScene = null ){
        DontDestroyOnLoad( gameObject );
        
        switch( _sceneToLoad.SceneType ){
            case SceneType.Indoors:
            Debug.Log( "loading indoor scene" );
                yield return _sceneManager.LoadIndoorScene( _sceneToLoad.name );

            break;

            case SceneType.Outdoors:
            Debug.Log( "loading overworld" );
                yield return _sceneManager.LoadOverworld( indoorScene );

            break;
        }

        OnSceneChanged?.Invoke();

        yield return TeleportPlayer();

        Destroy( gameObject );
    }

    private IEnumerator TeleportPlayer(){
        yield return null;

        var destination = FindObjectsOfType<Portal>().First( x => x != this && x.DestinationID == DestinationID );
        yield return _player.GetComponent<PlayerMovement>().MovePlayerToSceneSpawnPoint( destination.SpawnPoint.transform );
    }

    private void OnTriggerEnter( Collider collider ){
        if( collider.CompareTag("Player") ){
            _player = collider.gameObject;

            //--If the portal leads to a new scene, we proceed with loading it. this excludes outdoor to outdoor, that's local despite cross-scene?
            //--Else we simply teleport the player to the local location
            if( _portalType == PortalType.NewScene ){
                var indoorScene = SceneManagerTWO.Instance.ActiveScene;
                StartCoroutine( SwitchScene( indoorScene ) );
            }
            else
                StartCoroutine( TeleportPlayer() );
        }
    }
}
