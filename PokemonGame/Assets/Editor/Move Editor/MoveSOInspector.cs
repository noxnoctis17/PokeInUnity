using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor( typeof(MoveSO) )]
public class MoveSOInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var move = (MoveSO)target;

        if( GUILayout.Button("Open In Editor", GUILayout.Height( 40 ) ) )
        {
            MoveEditor.OpenMoveEditor( move );
        }

        base.OnInspectorGUI();
    }
}
