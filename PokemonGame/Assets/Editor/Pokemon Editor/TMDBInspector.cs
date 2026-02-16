using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor( typeof(TMDB) )]
public class TMDBInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var tmdb = (TMDB)target;

        if( GUILayout.Button("Rebuild TM Database", GUILayout.Height( 40 ) ) )
        {
            tmdb.Rebuild();
        }

        base.OnInspectorGUI();
    }
}
