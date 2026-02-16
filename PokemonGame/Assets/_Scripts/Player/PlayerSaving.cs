using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerSaving : MonoBehaviour, ISavable
{
    public object CaptureState(){
        var playerPosition = PlayerReferences.Instance.PlayerTransform.position;
        var playerTrainer = PlayerReferences.Instance.PlayerTrainer;

        var saveData = new PlayerSaveData(){
            SavedPosition = new float[] { playerPosition.x, playerPosition.y, playerPosition.z },
            PlayerParty = playerTrainer.ActiveParty.Select( p => p.CreateSaveData() ).ToList(),
        };

        return saveData;
    }

    public void RestoreState( object state ){
        //--Nice, cute local variables
        var saveData = (PlayerSaveData)state;
        var savedPosition = saveData.SavedPosition;
        var playerParty = PlayerReferences.Instance.PlayerTrainer;
        var restoredParty = saveData.PlayerParty.Select( p => new Pokemon( p ) ).ToList();

        //--Restore Player Position
        Vector3 position = new( savedPosition[0], savedPosition[1], savedPosition[2] );
        StartCoroutine( PlayerReferences.Instance.PlayerMovement.MovePlayerPosition( position ) );

        //--Restore Player Party
        playerParty.RestoreSavedParty( restoredParty );

    }
    
}

[System.Serializable]
public class PlayerSaveData
{
    public float[] SavedPosition;
    public List<PokemonSaveData> PlayerParty;
}
