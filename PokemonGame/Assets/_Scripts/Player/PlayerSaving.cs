using System;
using UnityEngine;

public class PlayerSaving : MonoBehaviour, ISavable
{
    //--Local Variables to cache references to necessary player data?
    private Vector3 _playerPosition;

    public object CaptureState(){
        _playerPosition = PlayerReferences.Instance.PlayerTransform.position;
        float[] savedPosition = new float[] { _playerPosition.x, _playerPosition.y, _playerPosition.z };
        // Debug.Log(  savedPosition[0] + ", " + savedPosition[1] + ", " + savedPosition[2]  );
        return savedPosition;
    }

    public void RestoreState( object state ){
        float[] savedPosition = (float[])state;
        Vector3 position = new( savedPosition[0], savedPosition[1], savedPosition[2] );
        // Debug.Log( position );s

        StartCoroutine( PlayerReferences.Instance.PlayerMovement.MovePlayerPosition( position ) );
    }
    
}
