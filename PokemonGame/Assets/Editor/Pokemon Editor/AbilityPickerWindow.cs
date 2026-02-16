using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class AbilityPickerWindow : EditorWindow
{
    private string _search = "";
    private Vector2 _scroll;
    private AbilityID _current;
    private Action<AbilityID> _onSelected;
    private List<AbilityID> _filteredList;

    public static void Show( AbilityID current, Vector2 mousePos, Action<AbilityID> onSelected )
    {
        var window = CreateInstance<AbilityPickerWindow>();

        window._current = current;
        window._onSelected = onSelected;

        window.titleContent = new ( "Select Ability" );

        
        Rect rect = new( mousePos, Vector2.zero );

        window.ShowAsDropDown( rect, new( 300, 400 ) );
    }

    private void OnEnable()
    {
        _filteredList = AbilityIDUtility.Alphabetical.ToList();
    }

    private void OnGUI()
    {
        if( Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape )
        {
            Close();
            GUIUtility.ExitGUI();
        }

        DrawHeader();
        DrawSearchBar();
        DrawAbilityList();
    }

    private void DrawHeader()
    {
        using ( new GUILayout.HorizontalScope( EditorStyles.toolbar ) )
        {
            GUILayout.Label( "Select Ability", EditorStyles.boldLabel );
            GUILayout.FlexibleSpace();

            if( GUILayout.Button( "Close", EditorStyles.toolbarButton ) )
            {
                Close();
            }
        }
    }

    private void DrawSearchBar()
    {
        EditorGUI.BeginChangeCheck();
        _search = EditorGUILayout.TextField( "Search", _search );

        if( EditorGUI.EndChangeCheck() )
            ApplySearch();
    }

    private void ApplySearch()
    {
        if( string.IsNullOrEmpty( _search ) )
        {
            _filteredList = AbilityIDUtility.Alphabetical.ToList();
        }
        else
        {
            _filteredList = AbilityIDUtility.Alphabetical.Where( a => a.ToString().IndexOf( _search, StringComparison.OrdinalIgnoreCase ) >= 0 ).ToList();
        }
    }

    private void DrawAbilityList()
    {
        _scroll = EditorGUILayout.BeginScrollView( _scroll );

        for( int i = 0; i < _filteredList.Count; i++ )
        {
            var ability = _filteredList[i];
            bool isCurrent = ability == _current;

            GUIStyle style = new( EditorStyles.label );
            if( isCurrent )
                style.fontStyle = FontStyle.Bold;

            if( GUILayout.Button( ability.ToString(), style ) )
            {
                _onSelected?.Invoke( ability );
                Close();
            }
        }

        EditorGUILayout.EndScrollView();
    }
}

public class AbilityIDUtility
{
    public static readonly AbilityID[] Alphabetical;

    static AbilityIDUtility()
    {
        Alphabetical = Enum.GetValues( typeof(AbilityID) ).Cast<AbilityID>().OrderBy( a => a.ToString() ).ToArray();
    }
}
