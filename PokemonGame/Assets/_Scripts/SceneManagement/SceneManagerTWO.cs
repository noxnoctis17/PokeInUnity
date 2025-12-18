using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Eflatun.SceneReference;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManagerTWO : MonoBehaviour
{
    public static SceneManagerTWO Instance;
    [SerializeField] private SceneDetails _gameplayScene;
    [SerializeField] private List<SceneDetails> _overworldScenes;
    [SerializeField] private List<SceneDetails> _indoorScenes;
    public SceneDetails ActiveScene { get; private set; }
    public SceneDetails GameplayScene => _gameplayScene;
    public List<SceneDetails> OverworldScenes => _overworldScenes;

    private void Awake(){
        Instance = this;
        SceneEvents.OnNewActiveScene += SetActiveScene;
    }

    private void OnDisable(){
        SceneEvents.OnNewActiveScene -= SetActiveScene;
    }

    private void SetActiveScene( SceneDetails scene ){
        ActiveScene = scene;
    }

    public IEnumerator LoadOverworld( SceneDetails indoorScene = null ){
        if( indoorScene != null )
            SceneManager.UnloadSceneAsync( indoorScene.SceneName );

        foreach( var scene in _overworldScenes ){
            // Debug.Log( scene.gameObject.name );
            scene.LoadSceneAdditively();
        }

        yield return null;
    }

    public IEnumerator LoadIndoorScene( SceneDetails scene ){

        foreach( var overworldScene in _overworldScenes ){
            overworldScene.UnloadScene();
        }

        scene.LoadSceneAdditively();

        yield return null;
    }

    public List<SavableEntity> GetSceneSavables( SceneDetails sceneDetails){
        var thisScene = SceneManager.GetSceneByName( sceneDetails.SceneName );
        var savableEntities = FindObjectsOfType<SavableEntity>().Where( s => s.gameObject.scene == thisScene ).ToList();

        return savableEntities;
    }
}
