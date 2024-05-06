using System.Collections;
using EasyButtons;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SceneTestingLoader : MonoBehaviour
{
    [SerializeField] private SceneManagerTWO _sceneManager;
    [SerializeField] private bool _standardTesting; //--Check this to simply start in InitiaTown with adjacent routes loaded

    private void OnEnable(){
        if( _standardTesting )
            StartCoroutine( LoadOverworld() );
    }

    //--Open refers to in-editor. Load refers to in-game.
    [Button]
    private void OpenOverworld(){
        foreach( var scene in _sceneManager.OverworldScenes ){
            EditorSceneManager.OpenScene( scene.SceneReference.Path, OpenSceneMode.Additive );
        }
    }

    [Button]
    private void CloseOverworld(){
        foreach( var scene in _sceneManager.OverworldScenes ){
            EditorSceneManager.CloseScene( scene.SceneReference.LoadedScene, true );
        }
    }

    private IEnumerator LoadOverworld(){
        yield return _sceneManager.LoadOverworld();
    }
}
