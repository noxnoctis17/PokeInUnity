using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class RentalTeamEditor : EditorWindow
{
    [MenuItem( "Tools/RentalTeamSO Editor" )]
    public static void OpenRentalTeamEditor() => GetWindow<RentalTeamEditor>( "RentalTeamSO Editor", typeof(PokemonEditor), typeof(MoveEditor), typeof(TrainerEditor), typeof(RentalTeamEditor) );
    public static RentalTeamSO OpenedTeam;
    private const int MOVE_COUNT = 4;

    //--Editor
    private VisualTreeAsset _uxml;
    private ListView _rentalTeamListView;
    private Label _currentTrainerLabel;
    private List<RentalTeamSO> _rentalTeamList;
    private RentalTeamSO _currentTeam;
    private SerializedObject _trainerObject;
    private SerializedProperty _rentalTeamProperty;
    private Button _createNewTrainerButton;
    private VisualElement _pokemonTeamPanel;
    private Button _addPokemonButton;
    private Dictionary<PokemonType, ( Color PrimaryColor, Color SecondaryColor )> TypeColors { get; set; }

    //--Trainer Info
    private TextField _nameField;
    private EnumField _trainerClassField;

    public static void OpenRentalTeamEditor( RentalTeamSO team )
    {
        OpenedTeam = team;
        GetWindow<RentalTeamEditor>( "RentalTeamSO Editor" );
    }

    public void CreateGUI()
    {
        _uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>( "Assets/Editor/Rental Team Editor/RentalTeamEditor.uxml" );
        VisualElement ui = _uxml.CloneTree();
        rootVisualElement.Add( ui );
        SetTypeColorsDictionary();

        //--Build Editor
        _rentalTeamListView = rootVisualElement.Q<ListView>( "RentalTeamList" );
        _currentTrainerLabel = rootVisualElement.Q<Label>( "CurrentRentalTeamLabel" );
        CreateRentalTeamList();
        _createNewTrainerButton = rootVisualElement.Q<Button>( "CreateNewRentalTeamButton" );
        _pokemonTeamPanel = rootVisualElement.Q<VisualElement>( "PokemonTeam" );
        _addPokemonButton = rootVisualElement.Q<Button>( "AddPokemonButton" );

        RegisterFieldCallbacks();

        if( OpenedTeam != null )
            SelectRentalTeam( OpenedTeam );
        else
            SelectRentalTeam( _rentalTeamList[0] );
    }

    private void CreateAndBindPokemonSlot( int index )
    {
        var slotUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>( "Assets/Editor/Trainer Editor/TrainerPokemonSlot.uxml" );
        VisualElement slot = slotUxml.CloneTree();
        slot.style.marginTop = 5;
        slot.style.marginBottom = 5;
        slot.style.marginLeft = 5;
        slot.style.marginRight = 5;

        SerializedProperty pokemonProp = _rentalTeamProperty.GetArrayElementAtIndex( index );

        BindPokemon( pokemonProp, slot );
        RegisterSlotCallbacks( slot, index );
        RefreshSlotHeader( slot, index );
        BindMoves( slot, index );

        _pokemonTeamPanel.Add(slot);
    }

    private void BindPokemon( SerializedProperty pokemon, VisualElement slot )
    {
        //--Bind Properties
        string rootPath = pokemon.propertyPath;

        var pokemonField = slot.Q<ObjectField>( "PokemonField" );
        pokemonField.objectType = typeof(PokemonSO);
        
        var heldItemField = slot.Q<ObjectField>( "HeldItemField" );
        heldItemField.objectType = typeof(ItemSO);

        pokemonField.bindingPath                                = $"{rootPath}._pokemon";
        slot.Q<TextField>( "NameField" ).bindingPath            = $"{rootPath}._nickName";
        slot.Q<IntegerField>( "LevelField" ).bindingPath        = $"{rootPath}._level";
        slot.Q<EnumField>( "NatureField" ).bindingPath          = $"{rootPath}._nature";
        slot.Q<EnumField>( "AbilityField" ).bindingPath         = $"{rootPath}._ability";
        heldItemField.bindingPath                               = $"{rootPath}._heldItem";

        slot.Q<IntegerField>( "HPField" ).bindingPath           = $"{rootPath}._hpEVs";
        slot.Q<IntegerField>( "AtkField" ).bindingPath          = $"{rootPath}._attackEVs";
        slot.Q<IntegerField>( "DefField" ).bindingPath          = $"{rootPath}._defenseEVs";
        slot.Q<IntegerField>( "SpAtkField" ).bindingPath        = $"{rootPath}._spattackEVs";
        slot.Q<IntegerField>( "SpDefField" ).bindingPath        = $"{rootPath}._spdefenseEVs";
        slot.Q<IntegerField>( "SpeField" ).bindingPath          = $"{rootPath}._speedEVs";

        slot.Q<EnumField>( "PokeballField" ).bindingPath        = $"{rootPath}._ball";
        // slot.Q<ListView>( "MoveListField" ).bindingPath      = $"{rootPath}._moves";

        //--Actually Bind
        slot.Bind( pokemon.serializedObject );
    }

    private void RegisterSlotCallbacks( VisualElement slot, int index )
    {
        //--Get Serialized Slot
        SerializedProperty pokemon = _rentalTeamProperty.GetArrayElementAtIndex( index );

        if( pokemon == null )
            return;

        //--Get properties
        SerializedProperty pokemonSOprop = pokemon.FindPropertyRelative( "_pokemon" );
        var pokeSO = pokemonSOprop.objectReferenceValue as PokemonSO;

        //--Slot Cache
        var pokemonField = slot.Q<ObjectField>( "PokemonField" );
        var nameField = slot.Q<TextField>( "NameField" );
        var levelField = slot.Q<IntegerField>( "LevelField" );
        var editButton = slot.Q<Button>( "OpenButton" );
        var upButton = slot.Q<Button>( "UpButton" );
        var downButton = slot.Q<Button>( "DownButton" );
        var clearButton = slot.Q<Button>( "ClearButton" );
        var removeButton = slot.Q<Button>( "RemoveButton" );

        //--Register Callbacks
        pokemonField.RegisterValueChangedCallback( evt =>
        {
            if( _currentTeam == null )
                return;

            RefreshSlotHeader( slot, index );
            BindMoves( slot, index );
        });

        nameField.RegisterValueChangedCallback( evt =>
        {
            if( _currentTeam == null )
                return;

            RefreshSlotHeader( slot, index );
        });

        levelField.RegisterValueChangedCallback( evt =>
        {
            if( _currentTeam == null )
                return;

            RefreshSlotHeader( slot, index );
        });

        editButton.RegisterCallback<ClickEvent>( evt =>
        {
            PokemonEditor.OpenPokemonEditor( pokeSO );
        });

        upButton.RegisterCallback<ClickEvent>( evt =>
        {
            if( _currentTeam == null )
                return;

            if( index == 0 )
                return;

            if( index > 0 )
                SwapPokemon( index, index - 1 );
        });

        downButton.RegisterCallback<ClickEvent>( evt =>
        {
            if( _currentTeam == null )
                return;

            if( index == _rentalTeamProperty.arraySize - 1 )
                return;

            if( index < 5 )
                SwapPokemon( index, index + 1 );
        });

        clearButton.RegisterCallback<ClickEvent>( evt =>
        {
            if( _currentTeam == null )
                return;

            SerializedProperty pokemon = _rentalTeamProperty.GetArrayElementAtIndex( index );

            pokemon.FindPropertyRelative( "_pokemon" ).objectReferenceValue = null;
            pokemon.FindPropertyRelative( "_nickName" ).stringValue = "";
            pokemon.FindPropertyRelative( "_level" ).intValue = 0;
            pokemon.FindPropertyRelative( "_nature" ).enumValueIndex = 0;
            pokemon.FindPropertyRelative( "_ability" ).enumValueIndex = 0;
            pokemon.FindPropertyRelative( "_heldItem" ).objectReferenceValue = null;

            pokemon.FindPropertyRelative( "_hpEVs" ).intValue = 0;
            pokemon.FindPropertyRelative( "_attackEVs" ).intValue = 0;
            pokemon.FindPropertyRelative( "_defenseEVs" ).intValue = 0;
            pokemon.FindPropertyRelative( "_spattackEVs" ).intValue = 0;
            pokemon.FindPropertyRelative( "_spdefenseEVs" ).intValue = 0;
            pokemon.FindPropertyRelative( "_speedEVs" ).intValue = 0;
            pokemon.FindPropertyRelative( "_ball" ).enumValueIndex = 0;

            ClearMoves( slot, index );

            pokemon.serializedObject.ApplyModifiedProperties();

            RefreshSlotHeader( slot, index );
            RefreshPokemonTeam();
        });

        removeButton.RegisterCallback<ClickEvent>( evt =>
        {
            if( _currentTeam == null )
                return;

            _rentalTeamProperty.DeleteArrayElementAtIndex( index );
            _trainerObject.ApplyModifiedProperties();

            RefreshPokemonTeam();
        });
    }

    private void RefreshSlotHeader( VisualElement slot, int index )
    {
        //--Get Serialized Slot
        SerializedProperty pokemon = _rentalTeamProperty.GetArrayElementAtIndex( index );

        if( pokemon == null )
            return;

        //--Get properties
        SerializedProperty pokemonSOprop = pokemon.FindPropertyRelative( "_pokemon" );
        SerializedProperty nickNameProp = pokemon.FindPropertyRelative( "_nickName" );
        SerializedProperty levelProp = pokemon.FindPropertyRelative( "_level" );

        var pokeSO = pokemonSOprop.objectReferenceValue as PokemonSO;

        //--Header Cache
        var pokemonSlotContainer = slot.Q<VisualElement>( "PokemonSlot" );
        // var headerBox = slot.Q<GroupBox>( "TopBar" );
        var portrait = slot.Q<VisualElement>( "Portrait" );
        var pokemonLabel = slot.Q<Label>( "PokemonLabel" );

        //--Label
        string pokemonName = !string.IsNullOrEmpty( nickNameProp.stringValue ) ? nickNameProp.stringValue : pokeSO != null ? pokeSO.Species : string.Empty;
        pokemonLabel.text = $"Slot {index + 1} - {pokemonName} (Lv. {levelProp.intValue})";

        //--Portrait
        if( pokeSO != null && pokeSO.Portrait_Normal != null )
            portrait.style.backgroundImage = new( pokeSO.Portrait_Normal );
        else
            portrait.style.backgroundImage = null;

        //--Top bar colors
        if( pokeSO != null )
        {
            pokemonSlotContainer.style.backgroundColor = GetColors( pokeSO ).color1;
            pokemonSlotContainer.style.borderTopColor = GetColors( pokeSO ).color2;
            pokemonSlotContainer.style.borderBottomColor = GetColors( pokeSO ).color2;
            pokemonSlotContainer.style.borderLeftColor = GetColors( pokeSO ).color2;
            pokemonSlotContainer.style.borderRightColor = GetColors( pokeSO ).color2;
        }
    }

    private void BindMoves( VisualElement slot, int index )
    {
        //--Get Serialized Slot
        SerializedProperty pokemon = _rentalTeamProperty.GetArrayElementAtIndex( index );

        //--Get PokemonSO
        SerializedProperty pokemonSOprop = pokemon.FindPropertyRelative( "_pokemon" );
        var pokeSO = pokemonSOprop.objectReferenceValue as PokemonSO;

        //--Get Moves List
        SerializedProperty movesProp = pokemon.FindPropertyRelative( "_moves" );

        for( int i = 0; i < MOVE_COUNT; i++ )
        {
            int moveIndex = i;

            var moveField = slot.Q<ObjectField>( $"Move{moveIndex + 1}Field" );
            var moveInfo = slot.Q<Label>( $"Move{moveIndex + 1}Info" );

            moveField.objectType = typeof(MoveSO);

            //-- Ensure list is large enough
            if( movesProp.arraySize <= moveIndex )
            {
                movesProp.arraySize = moveIndex + 1;
                pokemon.serializedObject.ApplyModifiedProperties();
            }

            SerializedProperty moveProp = movesProp.GetArrayElementAtIndex( moveIndex );

            //--Bind
            moveField.SetValueWithoutNotify( moveProp.objectReferenceValue );

            RefreshMoveSlot( moveProp.objectReferenceValue as MoveSO, pokeSO, moveField, moveInfo );

            //--Callback
            moveField.RegisterValueChangedCallback( evt =>
            {
                if( _currentTeam == null )
                    return;

                var currentPokemon = pokemon.FindPropertyRelative( "_pokemon" ).objectReferenceValue as PokemonSO;

                var move = evt.newValue as MoveSO;
                if( move != null && !currentPokemon.CanLearn( move ) )
                {
                    moveField.style.backgroundColor = Color.black;
                    moveInfo.text = "Invalid Move!";
                    return;
                }
                
                moveProp.objectReferenceValue = move;
                pokemon.serializedObject.ApplyModifiedProperties();
                RefreshMoveSlot( moveProp.objectReferenceValue as MoveSO, currentPokemon, moveField, moveInfo );
            });
        }
    }

    private void ClearMoves( VisualElement slot, int index )
    {
        //--Get Serialized Slot
        SerializedProperty pokemon = _rentalTeamProperty.GetArrayElementAtIndex( index );

        //--Get PokemonSO
        SerializedProperty pokemonSOprop = pokemon.FindPropertyRelative( "_pokemon" );
        var pokeSO = pokemonSOprop.objectReferenceValue as PokemonSO;

        //--Get Moves List
        SerializedProperty movesProp = pokemon.FindPropertyRelative( "_moves" );

        for( int i = 0; i < MOVE_COUNT; i++ )
        {
            int moveIndex = i;

            var moveField = slot.Q<ObjectField>( $"Move{moveIndex + 1}Field" );
            var moveInfo = slot.Q<Label>( $"Move{moveIndex + 1}Info" );

            moveField.objectType = typeof(MoveSO);

            //-- Ensure list is large enough
            if( movesProp.arraySize <= moveIndex )
            {
                movesProp.arraySize = moveIndex + 1;
                pokemon.serializedObject.ApplyModifiedProperties();
            }

            SerializedProperty moveProp = movesProp.GetArrayElementAtIndex( moveIndex );

            //--Clear
            moveProp.objectReferenceValue = null;
            RefreshMoveSlot( moveProp.objectReferenceValue as MoveSO, pokeSO, moveField, moveInfo );
        }
    }

    private void RefreshMoveSlot( MoveSO move, PokemonSO pokeSO, ObjectField moveField, Label moveInfo )
    {
        if( move == null )
        {
            moveField.style.backgroundColor = Color.grey;
            moveInfo.text = "";
            return;
        }

        if( !pokeSO.CanLearn( move ) )
        {
            moveField.style.backgroundColor = Color.black;
            moveInfo.text = "Invalid Move!";
            return;
        }

        moveField.style.backgroundColor = TypeColors[move.Type].PrimaryColor;
        moveInfo.text = GetMoveInfo( move );
    }

    private string GetMoveInfo( MoveSO move )
    {
        string category;
        if( move.MoveCategory == MoveCategory.Physical )
            category = "PH";
        else if( move.MoveCategory == MoveCategory.Special )
            category = "SP";
        else
            category = "ST";

        string acc = $"A {move.Accuracy:D3}";
        string pp = $"PP {move.PP}";
        string bp = $"BP {move.Power:D3}";

        string targets;
        if( move.MoveTarget == MoveTarget.Self )
            targets = "Self";
        else if( move.MoveTarget == MoveTarget.Enemy )
            targets = "Enmy";
        else if( move.MoveTarget == MoveTarget.Ally )
            targets = "Ally";
        else if( move.MoveTarget == MoveTarget.AllySide )
            targets = "Us";
        else if( move.MoveTarget == MoveTarget.OpposingSide )
            targets = "Them";
        else if( move.MoveTarget == MoveTarget.AllAdjacent )
            targets = "AAdj";
        else if( move.MoveTarget == MoveTarget.AllField )
            targets = "AllF";
        else
            targets = "All";

        return $"{category} | {acc} | {pp} | {bp} | {targets}";
    }

    private void AddPokemon()
    {
        if( _currentTeam.RentalTeam == null )
        {
            _currentTeam.InitTeam();
        }
        
        if( _currentTeam.RentalTeam.Count >= 6 )
            return;
            
        Undo.RecordObject( _currentTeam, "Add Trainer Pokemon" );

        _trainerObject.Update();

        int newIndex = _rentalTeamProperty.arraySize;
        _rentalTeamProperty.InsertArrayElementAtIndex( newIndex );

        SerializedProperty newPokemon = _rentalTeamProperty.GetArrayElementAtIndex( newIndex );

        _trainerObject.ApplyModifiedProperties();

        RefreshPokemonTeam();
    }

    private void SwapPokemon( int a, int b )
    {
        _rentalTeamProperty.MoveArrayElement( a, b );
        _trainerObject.ApplyModifiedProperties();
        RefreshPokemonTeam();
    }

    private void RefreshPokemonTeam()
    {
        _pokemonTeamPanel.Clear();
        _pokemonTeamPanel.Add( _addPokemonButton );

        if( _currentTeam == null )
            return;

        _trainerObject = new( _currentTeam );
        _rentalTeamProperty = _trainerObject.FindProperty( "_rentalTeam" );

        for( int i = 0; i < _rentalTeamProperty.arraySize; i++ )
        {
            CreateAndBindPokemonSlot( i );
        }
    }

    private void LoadRentalTeams()
    {
        _rentalTeamList = new();

        string[] guids = AssetDatabase.FindAssets( "t:RentalTeamSO" );

        foreach( string guid in guids )
        {
            string path = AssetDatabase.GUIDToAssetPath( guid );
            var team = AssetDatabase.LoadAssetAtPath<RentalTeamSO>( path );

            if( team != null )
            {
                Debug.Log( $"Loading {team.name}" );
                _rentalTeamList.Add( team );
            }
        }

        //--Initially Sort by Trainer Name
        _rentalTeamList.Sort( ( a, b ) => a.name.CompareTo( b.name ) );
    }

    private void CreateRentalTeamList()
    {
        LoadRentalTeams();

        _rentalTeamListView.itemsSource = _rentalTeamList;

        _rentalTeamListView.makeItem = () =>
        {
            return new Label();
        };

        _rentalTeamListView.bindItem = ( element, index ) =>
        {
            var label = (Label)element;
            var team = _rentalTeamList[index];

            label.text = $"{team.name}";
            Debug.Log( $"Label Text: {label.text}" );
        };

        _rentalTeamListView.selectionType = SelectionType.Single;
        _rentalTeamListView.selectionChanged += OnRentalTeamSelected;
        _rentalTeamListView.Rebuild();
    }

    private void OnRentalTeamSelected( IEnumerable<object> selection )
    {
        RentalTeamSO selectedTeam = null;

        foreach( var item in selection )
        {
            selectedTeam = item as RentalTeamSO;
            break;
        }

        _currentTeam = selectedTeam;

        RefreshDetailPanel();
    }

    private void RefreshDetailPanel()
    {
        if( _currentTeam == null )
        {
            Debug.LogError( $"Current Trainer is Null!" );
            _trainerClassField.SetValueWithoutNotify( TrainerClasses.Trainer );
            _nameField.SetValueWithoutNotify( string.Empty );
            return;
        }

        //--Trainer Pokemon
        RefreshPokemonTeam();
    }

    private void RefreshTrainerList()
    {
        _rentalTeamList.Sort( ( a, b ) => a.name.CompareTo( b.name ) );
        _rentalTeamListView.Rebuild();
    }

    private void RegisterFieldCallbacks()
    {
        _createNewTrainerButton.RegisterCallback<ClickEvent>( evt =>
        {
            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>( "Assets/Resources/Trainers" );
            if (folder != null)
                Selection.activeObject = folder;

            string path = EditorUtility.SaveFilePanelInProject( "Create New RentalTeamSO", "NewTrainer", "asset", "Choose save location" );

            if( string.IsNullOrEmpty( path ) )
                return;

            CreateNewRentalTeamAsset( path );
            RefreshTrainerList();
        });

        _addPokemonButton.RegisterCallback<ClickEvent>( evt =>
        {
            if( _currentTeam == null )
                return;

            AddPokemon();
        });
    }

    private void SelectRentalTeam( RentalTeamSO team )
    {
        _currentTeam = team;
        RefreshDetailPanel();

        int index = _rentalTeamList.IndexOf( team ) ;
        if( index >= 0 )
            _rentalTeamListView.SetSelection( index );
    }

    private void CreateNewRentalTeamAsset( string path )
    {
        var team = CreateInstance<RentalTeamSO>();

        AssetDatabase.CreateAsset( team, path );
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        CreateRentalTeamList();
        RefreshTrainerList();

        SelectRentalTeam( team );
    }

    private ( Color color1, Color color2 ) GetColors( PokemonSO pokemon )
    {
        Color color1 = TypeColors[pokemon.Type1].PrimaryColor;
        Color color2;

        if( pokemon.Type2 != PokemonType.None )
            color2 = TypeColors[pokemon.Type2].SecondaryColor;
        else
            color2 = TypeColors[pokemon.Type1].SecondaryColor;

        return ( color1, color2 );
    }

    private void SetTypeColorsDictionary()
    {
         TypeColors = new()
         {
            //--None
            { PokemonType.None, ( new Color32( 255, 255, 255, 255 ), new Color32( 0, 0, 0, 255 ) ) },

            //--Normal
            { PokemonType.Normal, ( new Color32( 159, 161, 159, 255 ), new Color32( 172, 156, 134, 255 ) ) },

            //--Fire
            { PokemonType.Fire, ( new Color32( 229, 97, 62, 255 ), new Color32( 230, 40, 41, 255 ) ) },

            //--Water
            { PokemonType.Water, ( new Color32( 41, 128, 239, 255 ), new Color32( 105, 146, 243, 255 ) ) },

            //--Electric
            { PokemonType.Electric, ( new Color32( 250, 193, 0, 255), new Color32( 84, 76, 45, 255 ) ) },

            //--Grass
            { PokemonType.Grass, ( new Color32( 135, 185, 80, 255 ), new Color32( 233, 232, 101, 255 ) ) },

            //--Ice
            { PokemonType.Ice, ( new Color32( 48, 216, 208, 255 ), new Color32( 10, 142, 162, 255 ) ) },

            //--Fighting
            { PokemonType.Fighting, ( new Color32( 255, 128, 0, 255 ), new Color32( 206, 64, 105, 255 ) ) },

            //--Poison
            { PokemonType.Poison, ( new Color32( 167, 102, 183, 255 ), new Color32( 109, 56, 131, 255 ) ) },

            //--Ground
            { PokemonType.Ground, ( new Color32( 224, 202, 52, 255 ), new Color32( 171, 121, 57, 255 ) ) },

            //--Flying
            { PokemonType.Flying, ( new Color32( 129, 185, 239, 255 ), new Color32( 152, 216, 216, 255 ) ) },

            //--Psychic
            { PokemonType.Psychic, ( new Color32( 240, 83, 127, 255 ), new Color32( 184, 45, 84, 255 ) ) },

            //--Bug
            { PokemonType.Bug, ( new Color32( 201, 232, 49, 255 ), new Color32( 89, 97, 52, 255 ) ) },

            //--Rock
            { PokemonType.Rock, ( new Color32( 94, 76, 31, 255 ), new Color32( 58, 35, 29, 255 ) ) },

            //--Ghost
            { PokemonType.Ghost, ( new Color32( 135, 111, 186, 255 ), new Color32( 89, 41, 122, 255 ) ) },

            //--Dragon
            { PokemonType.Dragon, ( new Color32( 104, 152, 248, 255 ), new Color32( 208, 64, 64, 255 ) ) },

            //--Dark
            { PokemonType.Dark, ( new Color32( 80, 65, 63, 255 ), new Color32( 47, 42, 56, 255 ) ) },

            //--Steel
            { PokemonType.Steel, ( new Color32( 96, 161, 184, 255 ), new Color32( 191, 191, 224, 255 ) ) },

            //--Fairy
            { PokemonType.Fairy, ( new Color32( 252, 131, 216, 255 ), new Color32( 221, 155, 178, 255 ) ) },
         };
    }
}
