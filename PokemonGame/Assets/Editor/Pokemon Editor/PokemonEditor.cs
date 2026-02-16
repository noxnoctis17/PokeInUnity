using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class PokemonEditor : EditorWindow
{
    [MenuItem( "Tools/Pokemon SO Editor" )]
    public static void OpenPokemonEditor() => GetWindow<PokemonEditor>( "PokemonSO Editor", typeof(PokemonEditor), typeof(MoveEditor), typeof(TrainerEditor), typeof(RentalTeamEditor) );
    public static PokemonSO OpenedPokemon;
    
    //--Basics
    private VisualTreeAsset _uxml;
    private ListView _pokemonListView;
    private VisualElement _detailsPanel;
    private Label _currentPokemonLabel;
    private List<PokemonSO> _pokemonList;
    private PokemonSO _currentPokemon;
    private Texture2D _loadedSpriteSheet;
    private Dictionary<PokemonType, ( Color PrimaryColor, Color SecondaryColor )> TypeColors { get; set; }
    private Button _refreshListButton;
    private Button _createNewPokemonButton;
    private SpritePreviewPlayer _previewPlayer;
    private VisualElement _previewImage;
    private VisualElement _normalPortrait;
    private VisualElement _happyPortrait;
    private VisualElement _angryPortrait;
    private VisualElement _hurtPortrait;
    private EnumField _previewAnimationTypeField;
    private EnumField _previewDirectionTypeField;
    private PokemonAnimationType _previewAnimationType = PokemonAnimationType.Idle;
    private FacingDirection _previewDirection = FacingDirection.Down;
    [SerializeField] private Sprite _missingNoSprite;
    private List<Sprite> _noSpriteSheetAvailable;


    //--Species info
    private IntegerField _dexNoField;
    private TextField _speciesField;
    private IntegerField _formIndexField;
    private EnumField _wildTypeField;
    private TextField _dexEntryField;
    private Label _type1Label;
    private Label _type2Label;
    private EnumField _type1Field;
    private EnumField _type2Field;
    private ListView _abilitiesList;
    private Button _addAbilityButton;
    private ListView _evolutionList;
    private Button _addEvolutionButton;
    private Button _passLearnSetButton;
    private ListView _levelUpMovesList;
    private Button _addMoveButton;
    private Button _sortLevelUpMovesButton;
    [SerializeField] private TMDB _tmDB;
    private ListView _teachableMovesList;
    private List<MoveSO> _tmKeys;
    private Button _bulkSyncTMsButton;
    private Button _singleSyncTMsButton;

    //--Base Stats
    private IntegerField _hpField;
    private IntegerField _atkField;
    private IntegerField _defField;
    private IntegerField _spatkField;
    private IntegerField _spdefField;
    private IntegerField _speField;
    private IntegerField _catchRateField;
    private IntegerField _expField;
    private IntegerField _epField;
    private EnumField _growthRateField;
    private IntegerField _friendshipField;
    private IntegerField _heightField;
    private IntegerField _weightField;
    private IntegerField _maleField;
    private IntegerField _femaleField;

    //--Sprites
    // default portrait
    // happy portrait
    // hurt portrait
    // angry portrait
    private Button _assignSpriteSheetButton;
    private ObjectField _spriteSheetField;

    public static void OpenPokemonEditor( PokemonSO pokemon )
    {
        OpenedPokemon = pokemon;
        GetWindow<PokemonEditor>( "PokemonSO Editor", typeof(PokemonEditor), typeof(MoveEditor), typeof(TrainerEditor), typeof(RentalTeamEditor) );
    }

    private void OnEnable()
    {
        _previewPlayer = new();
        _noSpriteSheetAvailable = new(){ _missingNoSprite };
        Undo.undoRedoPerformed += OnUndoRedo;
        EditorApplication.update += UpdateSpritePreview;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.update -= UpdateSpritePreview;
    }

    private void OnUndoRedo()
    {
        RefreshDetailPanel();
        RefreshPokemonList();
    }

    public void CreateGUI()
    {
        _uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>( "Assets/Editor/Pokemon Editor/PokemonEditor.uxml" );
        VisualElement ui = _uxml.CloneTree();
        rootVisualElement.Add( ui );

        SetDictionary();

        //--Build Editor
        _pokemonListView = rootVisualElement.Q<ListView>( "pokemonList" );
        _detailsPanel = rootVisualElement.Q<VisualElement>( "pokemonDetailsPanel" );
        _currentPokemonLabel = rootVisualElement.Q<Label>( "CurrentPokemonLabel" );
        CreatePokemonList();
        _refreshListButton = rootVisualElement.Q<Button>( "RefreshListButton" );
        _createNewPokemonButton = rootVisualElement.Q<Button>( "CreateNewPokemonButton" );
        _previewImage = rootVisualElement.Q<VisualElement>( "PreviewImage" );
        _normalPortrait = rootVisualElement.Q<VisualElement>( "NormalPortrait" );
        _happyPortrait = rootVisualElement.Q<VisualElement>( "HappyPortrait" );
        _angryPortrait = rootVisualElement.Q<VisualElement>( "AngryPortrait" );
        _hurtPortrait = rootVisualElement.Q<VisualElement>( "HurtPortrait" );
        _previewAnimationTypeField = rootVisualElement.Q<EnumField>( "PreviewAnimationTypeField" );
        _previewDirectionTypeField = rootVisualElement.Q<EnumField>( "PreviewDirectionTypeField" );
        _previewAnimationTypeField.Init( PokemonAnimationType.Idle );
        _previewDirectionTypeField.Init( FacingDirection.Down );

        //--Species Info
        _dexNoField = rootVisualElement.Q<IntegerField>( "DexNOField" );
        _speciesField = rootVisualElement.Q<TextField>( "SpeciesField" );
        _formIndexField = rootVisualElement.Q<IntegerField>( "FormIndexField" );
        _wildTypeField = rootVisualElement.Q<EnumField>( "WildTypeField" );
        _wildTypeField.Init( WildType.Uninterested );
        _dexEntryField = rootVisualElement.Q<TextField>( "DexEntryField" );
        _type1Label = rootVisualElement.Q<Label>( "Type1Label" );
        _type2Label = rootVisualElement.Q<Label>( "Type2Label" );
        _type1Field = rootVisualElement.Q<EnumField>( "Type1Field" );
        _type2Field = rootVisualElement.Q<EnumField>( "Type2Field" );
        _type1Field.Init( PokemonType.None );
        _type2Field.Init( PokemonType.None );

        //--Evolutions
        _evolutionList = rootVisualElement.Q<ListView>( "EvolutionList" );
        _addEvolutionButton = rootVisualElement.Q<Button>( "AddEvolutionButton" );
        _passLearnSetButton = rootVisualElement.Q<Button>( "PassLearnSetButton" );
        CreateEvolutionsList();

        //--Base Stats
        _hpField = rootVisualElement.Q<IntegerField>( "HPField" );
        _atkField = rootVisualElement.Q<IntegerField>( "AttackField" );
        _defField = rootVisualElement.Q<IntegerField>( "DefenseField" );
        _spatkField = rootVisualElement.Q<IntegerField>( "SpAttackField" );
        _spdefField = rootVisualElement.Q<IntegerField>( "SpDefenseField" );
        _speField = rootVisualElement.Q<IntegerField>( "SpeedField" );
        _catchRateField = rootVisualElement.Q<IntegerField>( "CatchRateField" );
        _expField = rootVisualElement.Q<IntegerField>( "ExpYieldField" );
        _epField = rootVisualElement.Q<IntegerField>( "EPYieldField" );
        _growthRateField = rootVisualElement.Q<EnumField>( "GrowthRateField" );
        _growthRateField.Init( GrowthRate.MediumFast );
        _friendshipField = rootVisualElement.Q<IntegerField>( "FriendshipField" );
        _heightField = rootVisualElement.Q<IntegerField>( "HeightField" );
        _weightField = rootVisualElement.Q<IntegerField>( "WeightField" );
        _maleField = rootVisualElement.Q<IntegerField>( "MaleField" );
        _femaleField = rootVisualElement.Q<IntegerField>( "FemaleField" );

        //--Abilities
        _abilitiesList = rootVisualElement.Q<ListView>( "AbilityList" );
        _addAbilityButton = rootVisualElement.Q<Button>( "AddAbilityButton" );
        CreateAbilityList();

        //--Moves
        _levelUpMovesList = rootVisualElement.Q<ListView>( "LevelUpMovesList" );
        _addMoveButton = rootVisualElement.Q<Button>( "AddMoveButton" );
        _sortLevelUpMovesButton = rootVisualElement.Q<Button>( "SortLevelUpMovesButton" );
        CreateLevelUpMovesList();

        //--TM Learn Set
        _teachableMovesList = rootVisualElement.Q<ListView>( "TeachableMovesList" );
        _bulkSyncTMsButton = rootVisualElement.Q<Button>( "BulkSyncTMsButton" );
        _singleSyncTMsButton = rootVisualElement.Q<Button>( "SingleSyncTMsButton" );
        _tmKeys = new();
        CreateTeachableMovesList();

        //--Sprite Sheet Setup
        _assignSpriteSheetButton = rootVisualElement.Q<Button>( "AssignSpriteSheetButton" );
        _spriteSheetField = rootVisualElement.Q<ObjectField>( "SpriteSheetField" );
        _spriteSheetField.objectType = typeof(Texture2D);

        RegisterFieldCallbacks();

        if( OpenedPokemon != null )
            SelectPokemon( OpenedPokemon );
        else
            SelectPokemon( _pokemonList[0] );
    }

    private void LoadPokemon()
    {
        _pokemonList = new();

        string[] guids = AssetDatabase.FindAssets( "t:PokemonSO" );

        foreach( string guid in guids )
        {
            string path = AssetDatabase.GUIDToAssetPath( guid );
            var pokemon = AssetDatabase.LoadAssetAtPath<PokemonSO>( path );

            if( pokemon != null )
            {
                Debug.Log( $"Loading {pokemon.Species}" );
                _pokemonList.Add( pokemon );
            }
        }

        //--Initially Sort By Dex Number
        _pokemonList.Sort( ( a, b ) => a.DexNO.CompareTo( b.DexNO ) );
    }

    private void CreatePokemonList()
    {
        LoadPokemon();

        _pokemonListView.itemsSource = _pokemonList;

        _pokemonListView.makeItem = () =>
        {
            return new Label();
        };

        _pokemonListView.bindItem = ( element, index ) =>
        {
            var label = (Label)element;
            var pokemon = _pokemonList[index];

            label.text = $"{pokemon.DexNO:D3} - {pokemon.Species}";
            // Debug.Log( $"Label Text: {label.text}" );
        };

        _pokemonListView.selectionType = SelectionType.Single;
        _pokemonListView.selectionChanged += OnPokemonSelected;
        _pokemonListView.Rebuild();
    }

    private void CreateAbilityList()
    {
        _abilitiesList.fixedItemHeight = 40;

        _abilitiesList.makeItem = () =>
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.height = 40;
            row.style.marginTop = 0;
            row.style.marginLeft = 0;
            row.style.marginRight = 0;
            row.style.marginBottom = 0;
            row.style.paddingTop = 2;
            row.style.paddingLeft = 2;
            row.style.paddingRight = 2;
            row.style.paddingBottom = 2;

            Button abilityButton = new() { text = "Select Ability" };
            abilityButton.style.flexGrow = 1;

            Button removeButton = new() { text = "Remove Ability" };

            row.Add( abilityButton );
            row.Add( removeButton );

            row.userData = ( abilityButton, removeButton );
            // removeButton.userData = row;

            return row;
        };

        _abilitiesList.bindItem = ( element, index ) =>
        {
            if( _currentPokemon == null )
                return;

            var ( abilityButton, removeButton ) = (( Button, Button ))element.userData;
            var ability = _currentPokemon.Abilities[index];

            abilityButton.userData = index;
            removeButton.userData = index;

            // element.userData = index;

            //--Update Button Label
            if( ability == AbilityID.None )
                abilityButton.text = "Select Ability";
            else
                abilityButton.text = $"{ability}";

            //--Clear clicked
            abilityButton.clicked -= abilityButton.userData as Action;
            removeButton.clicked -= removeButton.userData as Action;
            // abilityButton.userData = null;

            Action abilityClick = () =>
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();

                AbilityPickerWindow.Show( ability, mousePos, selected =>
                {
                    Undo.RecordObject( _currentPokemon, "Changed Ability" );
                    _currentPokemon.Abilities[index] = selected;
                    EditorUtility.SetDirty( _currentPokemon );

                    //--Update Button Label
                    abilityButton.text = selected.ToString();
                });
            };

            abilityButton.userData = index;
            abilityButton.clicked += abilityClick;

            Action removeClick = () =>
            {
                int removeIndex = (int)removeButton.userData;

                if( removeIndex < 0 || removeIndex >= _currentPokemon.Abilities.Count )
                    return;

                Undo.RecordObject( _currentPokemon, "Remove Ability" );
                _currentPokemon.RemoveAbility( index );
                EditorUtility.SetDirty( _currentPokemon );
                RefreshAbilityList();
            };

            removeButton.userData = index;
            removeButton.clicked += removeClick;
        };

        _addAbilityButton.clicked += () =>
        {
            if( _currentPokemon == null )
                return;

            if( _currentPokemon.Abilities.Count >= 3 )
                return;

            Undo.RecordObject( _currentPokemon, "Add Ability" );
            _currentPokemon.AddAbility( AbilityID.None );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshAbilityList();
        };
    }

    private void CreateEvolutionsList()
    {
        _evolutionList.fixedItemHeight = 40;

        _evolutionList.makeItem = () =>
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.height        = 40;
            row.style.paddingTop    = 2;
            row.style.paddingLeft   = 2;
            row.style.paddingRight  = 2;
            row.style.paddingBottom = 2;

            ObjectField pokeSOField = new();
            pokeSOField.objectType = typeof( PokemonSO );
            pokeSOField.style.flexGrow = 1;

            IntegerField levelField = new();
            levelField.style.width = 50;

            ObjectField itemField = new();
            itemField.objectType = typeof( EvolutionItemsSO );
            itemField.style.width = 150;

            IntegerField friendshipField = new();
            friendshipField.style.width = 50;

            EnumField timeField = new();
            timeField.Init( TimeOfDay.None );
            timeField.style.width = 75;

            Button passLearnsetButton = new() { text = "Pass Learnset" };
            passLearnsetButton.clicked += () =>
            {
                if( _currentPokemon == null )
                    return;
                    
                PassLearnset( (PokemonSO)pokeSOField.value );
            };

            var removeButton = new Button { text = "X" };
            removeButton.clicked += () =>
            {
                int i = (int)removeButton.userData;
                if( _currentPokemon == null || i < 0 || i >= _currentPokemon.Evolutions.Count )
                    return;

                Undo.RecordObject( _currentPokemon, "X" );
                _currentPokemon.RemoveEvolution( i );
                EditorUtility.SetDirty( _currentPokemon );
                RefreshEvolutionsList();
            };

            row.Add( pokeSOField );
            row.Add( levelField );
            row.Add( itemField );
            row.Add( friendshipField );
            row.Add( timeField );
            row.Add( passLearnsetButton );
            row.Add( removeButton );

            row.userData = ( pokeSOField, levelField, itemField, friendshipField, timeField, passLearnsetButton, removeButton );
            removeButton.userData = row;

            return row;
        };

        _evolutionList.bindItem = ( element, index ) =>
        {
            if( _currentPokemon == null )
                return;

            var evolution = _currentPokemon.Evolutions[index];
            var ( pokeSOField, levelField, itemField, friendshipField, timeField, passLearnsetButton, removeButton ) = ( ( ObjectField, IntegerField, ObjectField, IntegerField, EnumField, Button, Button ) )element.userData;

            //--Set Values
            pokeSOField.SetValueWithoutNotify( evolution.Pokemon );
            levelField.SetValueWithoutNotify( evolution.EvolutionLevel );
            itemField.SetValueWithoutNotify( evolution.EvolutionItem );
            friendshipField.SetValueWithoutNotify( evolution.Friendship );
            timeField.SetValueWithoutNotify( evolution.TimeOfDay );

            //--Clear Old Callbacks
            pokeSOField.UnregisterValueChangedCallback( OnEvolutionPokemonChanged );
            levelField.UnregisterValueChangedCallback( OnEvolutionLevelChanged );
            itemField.UnregisterValueChangedCallback( OnEvolutionItemChanged );
            friendshipField.UnregisterValueChangedCallback( OnEvolutionFriendshipChanged );
            timeField.UnregisterValueChangedCallback( OnEvolutionTimeChanged );

            //--Assign the index?
            pokeSOField.userData = index;
            levelField.userData = index;
            itemField.userData = index;
            friendshipField.userData = index;
            timeField.userData = index;
            passLearnsetButton.userData = index;
            removeButton.userData = index;

            //--Register Callbacks
            pokeSOField.RegisterValueChangedCallback( OnEvolutionPokemonChanged );
            levelField.RegisterValueChangedCallback( OnEvolutionLevelChanged );
            itemField.RegisterValueChangedCallback( OnEvolutionItemChanged );
            friendshipField.RegisterValueChangedCallback( OnEvolutionFriendshipChanged );
            timeField.RegisterValueChangedCallback( OnEvolutionTimeChanged );
            
        };

        _addEvolutionButton.clicked += () =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Add Evolution" );
            _currentPokemon.AddEvolution();
            EditorUtility.SetDirty( _currentPokemon );
            RefreshEvolutionsList();
        };

        _passLearnSetButton.clicked += () =>
        {
            if( _currentPokemon == null )
                return;

            var evo = _currentPokemon.Evolutions[0].Pokemon;
            PassLearnset( evo );
        };
    }

    private void CreateLevelUpMovesList()
    {
        _levelUpMovesList.fixedItemHeight = 35;

        _levelUpMovesList.makeItem = () =>
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.height        = 35;
            row.style.paddingTop    = 2;
            row.style.paddingLeft   = 2;
            row.style.paddingRight  = 2;
            row.style.paddingBottom = 2;

            ObjectField moveSOField = new();
            moveSOField.objectType = typeof( MoveSO );
            moveSOField.style.flexGrow = 1;

            IntegerField levelField = new();
            levelField.style.width = 50;

            Button openInEditor = new() { text = "Open in Editor" };
            Button removeButton = new() { text = "X" };

            openInEditor.clicked += () =>
            {
                MoveEditor.OpenMoveEditor( (MoveSO)moveSOField.value );
            };

            removeButton.clicked += () =>
            {
                int i = (int)removeButton.userData;
                if( _currentPokemon == null || i < 0 || i >= _currentPokemon.LearnableMoves.Count )
                    return;

                Undo.RecordObject( _currentPokemon, "Remove Level Up Move" );
                _currentPokemon.RemoveLevelUpMove( i );
                EditorUtility.SetDirty( _currentPokemon );
                RefreshLevelUpMovesList();
            };

            row.Add( moveSOField );
            row.Add( levelField );
            row.Add( openInEditor );
            row.Add( removeButton );

            row.userData = ( moveSOField, levelField, openInEditor, removeButton );
            removeButton.userData = row;

            return row;
        };

        _levelUpMovesList.bindItem = ( element, index ) =>
        {
            if( _currentPokemon == null )
                return;

            var levelUpMove = _currentPokemon.LearnableMoves[index];
            var ( moveSOField, levelField, openInEditor, removeButton ) = ( ( ObjectField, IntegerField, Button, Button ) )element.userData;

            //--Set Values
            moveSOField.SetValueWithoutNotify( levelUpMove.MoveSO );
            levelField.SetValueWithoutNotify( levelUpMove.LevelLearned );

            //--Clear Old Callbacks
            moveSOField.UnregisterValueChangedCallback( OnLevelUpMoveChanged );
            levelField.UnregisterValueChangedCallback( OnLevelUpMoveLevelChanged );

            //--Register Callbacks
            moveSOField.RegisterValueChangedCallback( OnLevelUpMoveChanged );
            levelField.RegisterValueChangedCallback( OnLevelUpMoveLevelChanged );

            //--Assign the index?
            moveSOField.userData = index;
            levelField.userData = index;
            openInEditor.userData = index;
            removeButton.userData = index;
        };

        _addMoveButton.clicked += () =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Add Level Up Move" );
            _currentPokemon.AddLevelUpMove();
            EditorUtility.SetDirty( _currentPokemon );
            RefreshLevelUpMovesList();
        };

        _sortLevelUpMovesButton.clicked += () =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Sort Level Up Move List" );
            _currentPokemon.SortLevelUpMovesByLevel();
            EditorUtility.SetDirty( _currentPokemon );
            RefreshLevelUpMovesList();
        };
    }

    private void CreateTeachableMovesList()
    {
        _teachableMovesList.fixedItemHeight = 35;

        _teachableMovesList.makeItem = () =>
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.height        = 35;
            row.style.paddingTop    = 2;
            row.style.paddingLeft   = 2;
            row.style.paddingRight  = 2;
            row.style.paddingBottom = 2;

            Toggle leftToggle = new();
            Toggle rightToggle = new();

            leftToggle.style.flexGrow = 1;
            rightToggle.style.flexGrow = 1;

            row.Add( leftToggle );
            row.Add( rightToggle );

            return row;
        };

        _teachableMovesList.bindItem = ( element, rowIndex ) =>
        {
            if( _currentPokemon == null )
                return;

            var leftToggle  = element[0] as Toggle;
            var rightToggle = element[1] as Toggle;

            int leftKeyIndex  = rowIndex * 2;
            int rightKeyIndex = leftKeyIndex + 1;

            BindToggle( leftToggle, leftKeyIndex );
            BindToggle( rightToggle, rightKeyIndex );
        };
    }

    private void BindToggle( Toggle toggle, int keyIndex )
    {
        if ( keyIndex >= _tmKeys.Count )
        {
            toggle.visible = false;
            return;
        }

        toggle.visible = true;

        MoveSO tmKey = _tmKeys[keyIndex];

        toggle.SetValueWithoutNotify( _currentPokemon.TeachableMoves[tmKey] );

        toggle.text = tmKey.Name;

        toggle.userData = tmKey;

        toggle.RegisterValueChangedCallback( evt =>
        {
            Undo.RecordObject( _currentPokemon, "Toggle TM" );
            _currentPokemon.SetTM( (MoveSO)toggle.userData, evt.newValue );
            EditorUtility.SetDirty(_currentPokemon);
            RefreshTeachableMovesList();
        });
    }

    private void BulkSyncTMs()
    {
        bool destructive = EditorUtility.DisplayDialog( "Sync TM Lists", "This will add missing TMs to all Pokemon.\n\n" + "Do you want to also REMOVE obsolete TMs?\n\n" + "Removing is destructive and cannot be undone safely.", "Add & Remove", "Add Only" );
        var tmList = _tmDB.TMList;

        foreach( var pokemon in _pokemonList )
        {
            Undo.RecordObject( pokemon, "Sync TM List" );
            pokemon.SyncTMs( tmList, destructive );
            EditorUtility.SetDirty( pokemon );
        }

        RefreshTeachableMovesList();
    }

    private void SingleSyncTMs()
    {
        bool destructive = EditorUtility.DisplayDialog( "Sync TM Lists", "This will add missing TMs to all Pokemon.\n\n" + "Do you want to also REMOVE obsolete TMs?\n\n" + "Removing is destructive and cannot be undone safely.", "Add & Remove", "Add Only" );
        var tmList = _tmDB.TMList;

        Undo.RecordObject( _currentPokemon, "Sync TM List" );
        _currentPokemon.SyncTMs( tmList, destructive );
        EditorUtility.SetDirty( _currentPokemon );
        RefreshTeachableMovesList();
    }

    private void PassLearnset( PokemonSO evolution )
    {
        bool passLearnset = EditorUtility.DisplayDialog( "Sync Learnsets", "Do you want to transfer the full learnset?\n\n" + "This will pass all current level up moves and all TM flags to the evolution's PokemonSO.\n\n", "Pass Learnset", "No thanks lol" );
        if( passLearnset )
        {
            Undo.RecordObject( evolution, "Passed prevo Learnset" );

            for( int i = 0; i < _currentPokemon.LearnableMoves.Count; i++ )
            {
                LearnableMoves move = new( _currentPokemon.LearnableMoves[i] );
                evolution.AddLevelUpMove( move );
            }

            EditorUtility.SetDirty( evolution );
        }
    }

    private void OnAbilityChanged( ChangeEvent<Enum> evt )
    {
        if( _currentPokemon == null )
            return;

        var enumField = (EnumField)evt.target;
        int index = (int)enumField.userData;

        Undo.RecordObject( _currentPokemon, "Change Ability" );
        _currentPokemon.SetAbility( index, (AbilityID)evt.newValue );
        EditorUtility.SetDirty( _currentPokemon );
        RefreshAbilityList();
    }

    private void OnEvolutionPokemonChanged( ChangeEvent<UnityEngine.Object> evt )
    {
        if( _currentPokemon == null )
            return;

        bool passLearnset = EditorUtility.DisplayDialog( "Sync Learnsets", "Do you want to transfer the full learnset?\n\n" + "This will pass all current level up moves and all TM flags to the evolution's PokemonSO.\n\n", "Pass Learnset", "No thanks lol" );

        int index = (int)( (VisualElement)evt.target ).userData;
        var evolution = (PokemonSO)evt.newValue;

        Undo.RecordObject( _currentPokemon, "Change Evolution PokemonSO" );
        _currentPokemon.SetEvolutionPokemon( index, evolution );

        if( passLearnset )
        {
            Undo.RecordObject( evolution, "Passed prevo Learnset" );

            for( int i = 0; i < _currentPokemon.LearnableMoves.Count; i++ )
            {
                LearnableMoves move = new( _currentPokemon.LearnableMoves[i] );
                evolution.AddLevelUpMove( move );
            }

            EditorUtility.SetDirty( evolution );
        }
    }

    private void OnEvolutionLevelChanged( ChangeEvent<int> evt )
    {
        if( _currentPokemon == null )
            return;

        int index = (int)( (VisualElement)evt.target ).userData;
        int level = evt.newValue;

        Undo.RecordObject( _currentPokemon, "Change Evolution Level" );
        _currentPokemon.SetEvolutionLevel( index, level );
    }

    private void OnEvolutionItemChanged( ChangeEvent<UnityEngine.Object> evt )
    {
        if( _currentPokemon == null )
            return;

            int index = (int)( (VisualElement)evt.target ).userData;
            var item = (EvolutionItemsSO)evt.newValue;

            Undo.RecordObject( _currentPokemon, "Change Evolution Item" );
            _currentPokemon.SetEvolutionItem( index, item );
    }

    private void OnEvolutionFriendshipChanged( ChangeEvent<int> evt )
    {
        if( _currentPokemon == null )
            return;

        int index = (int)( (VisualElement)evt.target ).userData;
        int friendship = evt.newValue;

        Undo.RecordObject( _currentPokemon, "Change Evolution Friendship" );
        _currentPokemon.SetEvolutionFriendship( index, friendship );
    }

    private void OnEvolutionTimeChanged( ChangeEvent<Enum> evt )
    {
        if( _currentPokemon == null )
            return;

        int index = (int)( (VisualElement)evt.target ).userData;
        TimeOfDay time = (TimeOfDay)evt.newValue;

        Undo.RecordObject( _currentPokemon, "Change Evolution Time" );
        _currentPokemon.SetEvolutionTime( index, time );
    }

    private void OnLevelUpMoveChanged( ChangeEvent<UnityEngine.Object> evt )
    {
        if( _currentPokemon == null )
            return;

            int index = (int)( (VisualElement)evt.target ).userData;
            var move = (MoveSO)evt.newValue;

            Undo.RecordObject( _currentPokemon, "Change Evolution PokemonSO" );
            _currentPokemon.SetLevelUpMove( index, move );
    }

    private void OnLevelUpMoveLevelChanged( ChangeEvent<int> evt )
    {
        if( _currentPokemon == null )
            return;

            int index = (int)( (VisualElement)evt.target ).userData;
            int level = evt.newValue;

            Undo.RecordObject( _currentPokemon, "Change Evolution Level" );
            _currentPokemon.SetLevelUpMoveLevel( index, level );
    }

    private void RefreshPokemonList()
    {
        _pokemonList.Sort( ( a, b ) => a.DexNO.CompareTo( b.DexNO ) );
        _pokemonListView.Rebuild();
    }

    private void RefreshAbilityList()
    {
        _abilitiesList.itemsSource = _currentPokemon.Abilities;
        _abilitiesList.Rebuild();
    }

    private void RefreshEvolutionsList()
    {
        _evolutionList.itemsSource = _currentPokemon.Evolutions;
        _evolutionList.Rebuild();
    }

    private void RefreshLevelUpMovesList()
    {
        _levelUpMovesList.itemsSource = _currentPokemon.LearnableMoves;
        _levelUpMovesList.Rebuild();
    }

    private void RefreshTeachableMovesList()
    {
        _tmKeys = _currentPokemon.TeachableMoves.Keys.OrderBy( k => k.Name ).ToList();
        _teachableMovesList.itemsSource = _tmKeys;
        _teachableMovesList.Rebuild();
    }

    private void OnPokemonSelected( IEnumerable<object> selection )
    {
        _previewPlayer.Pause();
        EditorApplication.update -= UpdateSpritePreview;
        PokemonSO selectedPokemon = null;

        foreach( var item in selection )
        {
            selectedPokemon = item as PokemonSO;
            break;
        }

        _currentPokemon = selectedPokemon;

        RefreshDetailPanel();

        EditorApplication.update += UpdateSpritePreview;
        _previewPlayer.Play();
    }

    private void RefreshDetailPanel()
    {
        if( _currentPokemon == null )
        {
            Debug.LogError( $"Current Pokemon is Null!" );
            _dexNoField.SetValueWithoutNotify( 0 );
            _speciesField.SetValueWithoutNotify( string.Empty );
            return;
        }

        //--Label
        var typeColors = GetColors( _currentPokemon );
        _currentPokemonLabel.text = $"({_currentPokemon.name}) {_currentPokemon.DexNO:D3} - {_currentPokemon.Species}";
        _currentPokemonLabel.style.backgroundColor      = typeColors.color1;
        _currentPokemonLabel.style.borderTopColor       = typeColors.color2;
        _currentPokemonLabel.style.borderBottomColor    = typeColors.color2;
        _currentPokemonLabel.style.borderLeftColor      = typeColors.color2;
        _currentPokemonLabel.style.borderRightColor     = typeColors.color2;

        //--Basic Info
        _dexNoField.SetValueWithoutNotify( _currentPokemon.DexNO );
        _speciesField.SetValueWithoutNotify( _currentPokemon.Species );
        _formIndexField.SetValueWithoutNotify( _currentPokemon.Form );
        _wildTypeField.SetValueWithoutNotify( _currentPokemon.WildType );
        _dexEntryField.SetValueWithoutNotify( _currentPokemon.Description );
        _type1Field.SetValueWithoutNotify( _currentPokemon.Type1 );
        _type2Field.SetValueWithoutNotify( _currentPokemon.Type2 );
        typeColors = GetColors( _currentPokemon );
        _type1Label.style.backgroundColor = typeColors.color1;
        _type2Label.style.backgroundColor = typeColors.color2;

        //--Abilities
        RefreshAbilityList();

        //--Base Stats
        _hpField.SetValueWithoutNotify( _currentPokemon.MaxHP );
        _atkField.SetValueWithoutNotify( _currentPokemon.Attack );
        _defField.SetValueWithoutNotify( _currentPokemon.Defense );
        _spatkField.SetValueWithoutNotify( _currentPokemon.SpAttack );
        _spdefField.SetValueWithoutNotify( _currentPokemon.SpDefense );
        _speField.SetValueWithoutNotify( _currentPokemon.Speed );
        _catchRateField.SetValueWithoutNotify( _currentPokemon.CatchRate );
        _expField.SetValueWithoutNotify( _currentPokemon.ExpYield );
        _epField.SetValueWithoutNotify( _currentPokemon.EffortYield );
        _growthRateField.SetValueWithoutNotify( _currentPokemon.GrowthRate );
        _friendshipField.SetValueWithoutNotify( _currentPokemon.BaseFriendship );
        _heightField.SetValueWithoutNotify( _currentPokemon.Height );
        _weightField.SetValueWithoutNotify( _currentPokemon.Weight );
        _maleField.SetValueWithoutNotify( _currentPokemon.MaleRatio );
        _femaleField.SetValueWithoutNotify( _currentPokemon.FemaleRatio );

        //--Evolutions
        RefreshEvolutionsList();

        //--Learnable Moves
        RefreshLevelUpMovesList();

        //--Teachable Moves
        RefreshTeachableMovesList();

        //--Sprite Sheet Preview
        RefreshAnimationPreview();
        _normalPortrait.style.backgroundImage = new StyleBackground( _currentPokemon.Portrait_Normal );
        _happyPortrait.style.backgroundImage = new StyleBackground( _currentPokemon.Portrait_Happy );
        _angryPortrait.style.backgroundImage = new StyleBackground( _currentPokemon.Portrait_Angry );
        _hurtPortrait.style.backgroundImage = new StyleBackground( _currentPokemon.Portrait_Hurt );
    }

    private void RegisterFieldCallbacks()
    {
        _refreshListButton.RegisterCallback<ClickEvent>( evt =>
        {
            CreatePokemonList();
        });

        _createNewPokemonButton.RegisterCallback<ClickEvent>( evt =>
        {
            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>( "Assets/Resources/Pokemon" );
            if (folder != null)
                Selection.activeObject = folder;

            string path = EditorUtility.SaveFilePanelInProject( "Create New Pokemon", "NewPokemon", "asset", "Choose location for the new Pokemon", "Assets/Resources/Pokemon" );

            if( string.IsNullOrEmpty( path ) )
                return;

            CreateNewPokemonAsset( path );
        });

        _previewAnimationTypeField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            _previewAnimationType = (PokemonAnimationType)evt.newValue;
            RefreshAnimationPreview();
        });

        _previewDirectionTypeField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            _previewDirection = (FacingDirection)evt.newValue;
            RefreshAnimationPreview();
        });

        _dexNoField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Dex Number" );
            _currentPokemon.SetDexNO( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
            RefreshPokemonList();
        });

        _speciesField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Species" );
            _currentPokemon.SetSpecies( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
            RefreshPokemonList();
        });

        _formIndexField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Form Index" );
            _currentPokemon.SetFormIndex( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _dexEntryField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Dex Description" );
            _currentPokemon.SetDexDescription( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _wildTypeField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Wild Type" );
            _currentPokemon.SetWildType( (WildType)evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _type1Field.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Type 1" );
            _currentPokemon.SetType1( (PokemonType)evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _type2Field.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Type 2" );
            _currentPokemon.SetType2( (PokemonType)evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _spriteSheetField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            _loadedSpriteSheet = evt.newValue as Texture2D;
        });

        _assignSpriteSheetButton.RegisterCallback<ClickEvent>( evt =>
        {
            if( _loadedSpriteSheet == null )
                return;

            GetSpriteSheet( _loadedSpriteSheet );
        });

        _bulkSyncTMsButton.RegisterCallback<ClickEvent>( evt =>
        {
            if( _pokemonList == null || _tmKeys == null )
                return;

            BulkSyncTMs();
        });

        _singleSyncTMsButton.RegisterCallback<ClickEvent>( evt =>
        {
            if( _currentPokemon == null || _tmKeys == null )
                return;

            SingleSyncTMs();
        });

        _hpField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Form Index" );
            _currentPokemon.SetHP( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _atkField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Form Index" );
            _currentPokemon.SetAttack( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _defField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Form Index" );
            _currentPokemon.SetDefense( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _spatkField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Form Index" );
            _currentPokemon.SetSpAttack( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _spdefField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Form Index" );
            _currentPokemon.SetSpDefense( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _speField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Form Index" );
            _currentPokemon.SetSpeed( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _catchRateField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Form Index" );
            _currentPokemon.SetCatchRate( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _expField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Form Index" );
            _currentPokemon.SetEXPYield( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _epField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Form Index" );
            _currentPokemon.SetEffortYield( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _growthRateField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Form Index" );
            _currentPokemon.SetGrowthRate( (GrowthRate)evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _friendshipField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Base Friendship" );
            _currentPokemon.SetBaseFriendship( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _heightField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Height" );
            _currentPokemon.SetHeight( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _weightField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Weight" );
            _currentPokemon.SetWeight( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _maleField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Male Ratio" );
            _currentPokemon.SetMaleRatio( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

        _femaleField.RegisterValueChangedCallback( evt =>
        {
            if( _currentPokemon == null )
                return;

            Undo.RecordObject( _currentPokemon, "Edit Female Ratio" );
            _currentPokemon.SetFemaleRatio( evt.newValue );
            EditorUtility.SetDirty( _currentPokemon );
            RefreshDetailPanel();
        });

    }

    private void CreateNewPokemonAsset( string path )
    {
        var pokemon = CreateInstance<PokemonSO>();

        AssetDatabase.CreateAsset( pokemon, path );
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        pokemon.InitFromEditor();
        pokemon.SyncTMs( _tmDB.TMList );

        // Reload list + select
        CreatePokemonList();
        SelectPokemon( pokemon );
    }

    private void SelectPokemon( PokemonSO pokemon )
    {
        _currentPokemon = pokemon;
        RefreshDetailPanel();

        int index = _pokemonList.IndexOf( pokemon );
        if( index >= 0 )
            _pokemonListView.SetSelection( index );
    }

    private void UpdateSpritePreview()
    {
        if( _previewPlayer.CurrentSprite == null )
            return;

        if( _previewPlayer.LastSprite != _previewPlayer.CurrentSprite )
        {
            _previewPlayer.Update();
            _previewImage.style.backgroundImage = new StyleBackground( _previewPlayer.CurrentSprite );
        }
    }

    private void RefreshAnimationPreview()
    {
        _previewPlayer.Pause();
        var sheet = GetPreviewSpriteSheet( _previewAnimationType, _previewDirection );

        if( sheet != null && sheet.Count > 0 )
        {
            _previewPlayer.SetCurrentSpriteSheet( new( sheet ) );
        }
        else
        {
            _previewPlayer.SetCurrentSpriteSheet( new( _noSpriteSheetAvailable ) );
        }

        _previewPlayer.Play();
    }

    private void GetSpriteSheet( Texture2D sheet )
    {
        string path = AssetDatabase.GetAssetPath( sheet );
        List<UnityEngine.Object> assets = AssetDatabase.LoadAllAssetsAtPath( path ).ToList();

        List<Sprite> sprites = new();

        foreach( var asset in assets )
        {
            if( asset is Sprite sprite )
                sprites.Add( sprite );
        }

        if( sprites.Count == 0 )
        {
            Debug.LogWarning( "No sprites found in sprite sheet!" );
            return;
        }

        sprites.Sort( ( a, b ) => EditorUtility.NaturalCompare( a.name, b.name ) );
        AssignSpriteSheet( sheet.name, sprites );
    }

    private const int DIRECTION_COUNT = 8;
    //--This shit cray
    private List<List<Sprite>> SliceSpriteSheet( List<Sprite> sprites )
    {
        int framesPerDirection = sprites.Count / DIRECTION_COUNT;

        var result = new List<List<Sprite>>();

        for( int d = 0; d < DIRECTION_COUNT; d++ )
        {
            List<Sprite> list = new();

            for( int f = 0; f < framesPerDirection; f++ )
            {
                list.Add( sprites[d * framesPerDirection + f ]);
            }

            result.Add( list );
        }

        return result;
    }

    private void AssignSpriteSheet( string name, List<Sprite> sprites )
    {
        if( _currentPokemon == null )
            return;

        if( name.Contains( "Portraits", StringComparison.OrdinalIgnoreCase ) )
        {
            Debug.Log( $"Sprite Sheet is a Portrait Sprite Sheet! The current Pokemon is: {_currentPokemon.Species}, Form: {_currentPokemon.Form}" );
            Undo.RecordObject( _currentPokemon, $"Assign Portrait Sprites" );
            
            if( sprites.Count > 0 )
                _currentPokemon.SetNormalPortrait( sprites[0] );
            if( sprites.Count > 1 )
                _currentPokemon.SetHappyPortrait( sprites[1] );
            if( sprites.Count > 3 )
                _currentPokemon.SetAngryPortrait( sprites[3] );
            if( sprites.Count > 5 )
                _currentPokemon.SetHurtPortrait( sprites[6] );
        }
        else 
        {
            if( !TryGetAnimationType( name, out var type ) )
            {
                Debug.LogWarning( $"Could not determine animation type from {name}!" );
                return;
            }
            

            var directionalSprites = SliceSpriteSheet( sprites );
            if( directionalSprites == null )
                return;

            Undo.RecordObject( _currentPokemon, $"Assign {type} Sprites" );

            switch( type )
            {
                case PokemonAnimationType.Idle:
                    _currentPokemon.SetIdleSprites( directionalSprites );
                    break;

                // case PokemonAnimationType.Walk:
                //     _currentPokemon.SetWalkSprites( directionalSprites );
                //     break;

                // case PokemonAnimationType.Strike:
                // _currentPokemon.SetStrikeSprites(directionalSprites);
                //     break;

                // case PokemonAnimationType.Shoot:
                //     _currentPokemon.SetShootSprites(directionalSprites);
                //     break;

                // case PokemonAnimationType.Faint:
                //     _currentPokemon.SetFaintSprites(directionalSprites);
                //     break;
            }

        }

        EditorUtility.SetDirty( _currentPokemon );
        RefreshDetailPanel();
    }

    private bool TryGetAnimationType( string sheetName, out PokemonAnimationType type )
    {
        type = default;

        foreach( PokemonAnimationType t in Enum.GetValues( typeof(PokemonAnimationType) ) )
        {
            if( sheetName.Contains( t.ToString(), StringComparison.OrdinalIgnoreCase ) )
            {
                type = t;
                return true;
            }
        }

        return false;
    }

    private List<Sprite> GetPreviewSpriteSheet( PokemonAnimationType animationType, FacingDirection direction )
    {
        switch( animationType )
        {
            case PokemonAnimationType.Idle:

                if( direction == FacingDirection.Up && _currentPokemon.IdleUpSprites != null && _currentPokemon.IdleUpSprites.Count > 0 )
                    return _currentPokemon.IdleUpSprites;

                else if( direction == FacingDirection.Down && _currentPokemon.IdleDownSprites != null && _currentPokemon.IdleDownSprites.Count > 0 )
                    return _currentPokemon.IdleDownSprites;

                else if( direction == FacingDirection.Left && _currentPokemon.IdleLeftSprites != null && _currentPokemon.IdleLeftSprites.Count > 0 )
                    return _currentPokemon.IdleLeftSprites;

                else if( direction == FacingDirection.Right && _currentPokemon.IdleRightSprites != null && _currentPokemon.IdleRightSprites.Count > 0 )
                    return _currentPokemon.IdleRightSprites;

                else if( direction == FacingDirection.UpLeft && _currentPokemon.IdleUpLeftSprites != null && _currentPokemon.IdleUpLeftSprites.Count > 0 )
                    return _currentPokemon.IdleUpLeftSprites;

                else if( direction == FacingDirection.UpRight && _currentPokemon.IdleUpRightSprites != null && _currentPokemon.IdleUpRightSprites.Count > 0 )
                    return _currentPokemon.IdleUpRightSprites;

                else if( direction == FacingDirection.DownLeft && _currentPokemon.IdleDownLeftSprites != null && _currentPokemon.IdleDownLeftSprites.Count > 0 )
                    return _currentPokemon.IdleDownLeftSprites;

                else if( direction == FacingDirection.DownRight && _currentPokemon.IdleDownRightSprites != null && _currentPokemon.IdleDownRightSprites.Count > 0 )
                    return _currentPokemon.IdleDownRightSprites;
    
                break;

            default: return _currentPokemon.IdleDownSprites;
        }

        return null;
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

    private void SetDictionary(){
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
