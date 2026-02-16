using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class OpenPokemonSuite
{
    [MenuItem( "Tools/Pokemon Suite" )]
    public static void OpenPokemonEditorSuite()
    {
        PokemonEditor.OpenPokemonEditor();
        MoveEditor.OpenMoveEditor();
    }
}
