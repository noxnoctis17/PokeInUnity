using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(menuName = "Pokemon/Databases/TM Database")]
public class TMDB : ScriptableObject
{
    [SerializeField] private List<MoveSO> _tmList = new();

    public List<MoveSO> TMList => _tmList;


#if UNITY_EDITOR

    [ContextMenu( itemName: "Rebuild TM Database" )]
    public void Rebuild()
    {
        _tmList.Clear();

        string[] guids = AssetDatabase.FindAssets( "t:MoveSO" );
        foreach( string guid in guids )
        {
            var path = AssetDatabase.GUIDToAssetPath( guid );
            var move = AssetDatabase.LoadAssetAtPath<MoveSO>( path );

            if ( move != null && move.HasTM )
                _tmList.Add( move );
        }

        EditorUtility.SetDirty( this );
    }

#endif
}
