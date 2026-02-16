using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor( typeof(RentalTeamSO) )]
public class RentalTeamSOInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var team = (RentalTeamSO)target;

        if( GUILayout.Button("Open In Editor", GUILayout.Height( 40 ) ) )
        {
            RentalTeamEditor.OpenRentalTeamEditor( team );
        }

        base.OnInspectorGUI();
    }
}
