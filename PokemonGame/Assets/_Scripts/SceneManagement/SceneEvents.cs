using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneEvents : MonoBehaviour
{
    [SerializeField] private SceneDetails _sceneDetails;
    public static Action OnActiveSceneChanged;
    public static Action<SceneDetails> OnNewActiveScene;
    public static Action OnLeavingScene;

    private void OnTriggerEnter( Collider col ){
        if( col.CompareTag( "Player" ) ){
            Debug.Log( $"Entered {_sceneDetails.SceneName}" );
            SetActiveScene();
        }
    }

    private void OnTriggerExit( Collider col ){
        if( col.CompareTag( "Player" ) ){
            Debug.Log( $"Exited {_sceneDetails.SceneName}" );
        }
    }

    private void SetActiveScene(){
        OnLeavingScene?.Invoke();
        Scene targetScene = SceneManager.GetSceneByName( _sceneDetails.SceneName );
        SceneManager.SetActiveScene( targetScene );

        //--If for some reason (maybe during testing) the active scene isn't marked as being loaded, we should do so
        if( _sceneDetails.IsLoaded == false )
            _sceneDetails.SetIsLoaded( true );

        OnNewActiveScene?.Invoke( _sceneDetails );
        OnActiveSceneChanged?.Invoke();
    }
}
