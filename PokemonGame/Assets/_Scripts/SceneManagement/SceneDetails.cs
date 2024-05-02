using System.Collections;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneDetails : MonoBehaviour
{
    public bool IsLoaded { get; private set; }
    public static Action OnActiveSceneChanged;
    public static Action OnLeavingScene;

    public void LoadScene(){
        if( !IsLoaded ){
            SceneManager.LoadSceneAsync( gameObject.name, LoadSceneMode.Additive );
            IsLoaded = true;
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
        OnActiveSceneChanged?.Invoke();
    }
    
}
