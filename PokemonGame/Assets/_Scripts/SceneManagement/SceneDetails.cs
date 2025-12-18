using System;
using System.Collections.Generic;
using EasyButtons;
using Eflatun.SceneReference;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SceneType { Outdoors, Indoors, Cave, Core }

[Serializable]
public class SceneDetails
{
    [SerializeField] private SceneType _sceneType;
    [SerializeField] private SceneReference _sceneReference;
    public SceneType SceneType => _sceneType;
    public SceneReference SceneReference => _sceneReference;
    public string SceneName => _sceneReference.Name;
    public bool IsLoaded { get; private set; }
    private List<SavableEntity> _savableEntities;

    public void SetIsLoaded( bool isLoaded ){
        Debug.Log( $"SetIsLoaded: {isLoaded} in scene: {SceneName}" );
        IsLoaded = isLoaded;
    }

    public void LoadSceneAdditively(){
        //--Load scene state here
        if( !IsLoaded ){
            var asyncOP = SceneManager.LoadSceneAsync( SceneName, LoadSceneMode.Additive );
            IsLoaded = true;

            asyncOP.completed += ( AsyncOperation op ) =>
            {   //--Async Operation callback "completed". once the loadscene async operation is completed, this event is raised. we subscribe to it here
                //--So that we don't try to get and potentially restore savable entities before they scene fully loads, in which some objects might not
                //--have been fully loaded in yet.
                _savableEntities = SceneManagerTWO.Instance.GetSceneSavables( this );
                SavingSystem.Instance.RestoreEntityStates( _savableEntities );
            };
        }
    }

    public void UnloadScene(){
        //--Save scene state here
        if( IsLoaded ){
            SavingSystem.Instance.CaptureEntityStates( _savableEntities );

            SceneManager.UnloadSceneAsync( SceneName );
            IsLoaded = false;
        }
    }
    
}
