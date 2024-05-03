using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SceneType { Outdoors, Indoors, Cave, Core }

public class SceneDetails : MonoBehaviour
{
    [SerializeField] private SceneType _sceneType;
    public SceneType SceneType => _sceneType;
    public Scene ActiveScene;
    public bool IsLoaded { get; private set; }
    public static Action OnActiveSceneChanged;
    public static Action<SceneDetails> OnNewActiveScene;
    public static Action OnLeavingScene;

    //--TODO: REMOVE THIS WHEN YOU EVENTUALLY MAKE A BOOTSTRAP SCENE OR SOMETHING, THESE ARE MY CURRENTLY LOADED OVERWORLD TEST AREAS
    private void Awake(){
        if( gameObject.name == "InitiaTown" || gameObject.name == "RouteTest01" )
            IsLoaded = true;
    }

    public void LoadSceneAdditively(){
        if( !IsLoaded ){
            SceneManager.LoadSceneAsync( gameObject.name, LoadSceneMode.Additive );
            IsLoaded = true;
        }
    }

    public void UnloadScene(){
        if( IsLoaded ){
            SceneManager.UnloadSceneAsync( gameObject.name );
            IsLoaded = false;
        }
    }

    private void OnTriggerEnter( Collider col ){
        if( col.CompareTag( "Player" ) ){
            Debug.Log( $"Entered {gameObject.name}" );
            // LoadScene();
            SetActiveScene();
        }
    }

    private void OnTriggerExit( Collider col ){
        if( col.CompareTag( "Player" ) ){
            Debug.Log( $"Exited {gameObject.name}" );
        }
    }

    private void SetActiveScene(){
        OnLeavingScene?.Invoke();
        Scene targetScene = SceneManager.GetSceneByName( gameObject.name );
        SceneManager.SetActiveScene( targetScene );
        OnNewActiveScene?.Invoke( this );
        OnActiveSceneChanged?.Invoke();
    }
    
}
