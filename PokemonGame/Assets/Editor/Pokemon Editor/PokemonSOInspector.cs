using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor( typeof(PokemonSO) )]
public class PokemonSOInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var pokemon = (PokemonSO)target;

        if( GUILayout.Button("Open In Editor", GUILayout.Height( 40 ) ) )
        {
            PokemonEditor.OpenPokemonEditor( pokemon );
        }

        base.OnInspectorGUI();
    }
}
