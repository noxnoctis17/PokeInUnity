using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System;

public enum PortalDestinationID { A, B, C, D, E, }

public class Portal : MonoBehaviour
{
    [SerializeField] private int _sceneToLoad = -1; //--Default -1 because indexed scenes start at 0. This will return an error so we know we fucked up
    [SerializeField] private GameObject _spawnPoint;
    [SerializeField] private PortalDestinationID _destinationID;
    [SerializeField] private bool _loadScene; //--to test the portal within the same scene/additively loaded scenes
    private GameObject _player;
    public GameObject SpawnPoint => _spawnPoint;
    public PortalDestinationID DestinationID => _destinationID;
    public static Action OnSceneChanged;

    private IEnumerator SwitchScene(){
        DontDestroyOnLoad( gameObject );
        yield return SceneManager.LoadSceneAsync( _sceneToLoad );
        OnSceneChanged?.Invoke();

        yield return TeleportPlayer();

        Destroy( gameObject );
    }

    private IEnumerator TeleportPlayer(){
        var destination = FindObjectsOfType<Portal>().First( x => x != this && x.DestinationID == DestinationID );
        yield return _player.GetComponent<PlayerMovement>().MovePlayerToSceneSpawnPoint( destination.SpawnPoint.transform );
    }

    private void OnTriggerEnter( Collider collider ){
        if( collider.CompareTag("Player") ){
            _player = collider.gameObject;

            if( _loadScene )
                StartCoroutine( SwitchScene() );
            else
                StartCoroutine( TeleportPlayer() );
        }
    }
}
