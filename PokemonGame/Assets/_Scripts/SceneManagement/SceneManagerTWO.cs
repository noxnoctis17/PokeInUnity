using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManagerTWO : MonoBehaviour
{
    public static SceneManagerTWO Instance;
    [SerializeField] private SceneDetails _gameplayScene;
    [SerializeField] private List<SceneDetails> _overworldScenes;
    public SceneDetails ActiveScene { get; private set; }

    private void Awake(){
        Instance = this;
        SceneDetails.OnNewActiveScene += SetActiveScene;
    }

    private void OnDisable(){
        SceneDetails.OnNewActiveScene -= SetActiveScene;
    }

    private void SetActiveScene( SceneDetails scene ){
        ActiveScene = scene;
    }

    public IEnumerator LoadOverworld( SceneDetails indoorScene = null ){
        if( indoorScene != null )
            yield return SceneManager.UnloadSceneAsync( indoorScene.gameObject.name );

        foreach( var scene in _overworldScenes ){
            Debug.Log( scene.gameObject.name);
            scene.LoadSceneAdditively();
            yield return null;
        }
    }

    public IEnumerator LoadIndoorScene( string scene ){
        foreach( var overworldScene in _overworldScenes ){
            overworldScene.UnloadScene();
        }

        yield return SceneManager.LoadSceneAsync( scene, LoadSceneMode.Additive );
    }
}
