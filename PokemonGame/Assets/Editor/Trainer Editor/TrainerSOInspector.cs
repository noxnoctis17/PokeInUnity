using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor( typeof(TrainerSO) )]
public class TrainerSOInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var trainer = (TrainerSO)target;

        if( GUILayout.Button("Open In Editor", GUILayout.Height( 40 ) ) )
        {
            TrainerEditor.OpenTrainerEditor( trainer );
        }

        base.OnInspectorGUI();
    }
}
