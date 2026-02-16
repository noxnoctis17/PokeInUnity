using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class MoveEditor : EditorWindow
{
    [MenuItem( "Tools/Move SO Editor" )]
    public static void OpenMoveEditor() => GetWindow<MoveEditor>( "MoveSO Editor", typeof(PokemonEditor), typeof(MoveEditor), typeof(TrainerEditor), typeof(RentalTeamEditor) );
    public static MoveSO OpenedMove;

    //--Editor
    private VisualTreeAsset _uxml;
    private Label _currentMoveLabel;
    private ListView _moveListView;
    private List<MoveSO> _moveList;
    private MoveSO _currentMove;
    private Button _createNewMoveButton;
    private Dictionary<PokemonType, ( Color PrimaryColor, Color SecondaryColor )> TypeColors { get; set; }
    private SerializedObject _moveObject;
    private SerializedProperty _primaryEffectsProp;
    private SerializedProperty _secondaryEffectsProp;
    private MoveEffectsSlot _primaryEffectsRow;

    //--Move Fields
    private TextField _nameField;
    private IntegerField _ppField;
    private IntegerField _powerField;
    private IntegerField _accuracyField;
    private EnumField _accuracyTypeField;
    private Toggle _hasTMToggle;
    private Toggle _statOverrideToggle;
    private EnumField _targetField;
    private EnumField _typeField;
    private EnumField _categoryField;
    private EnumField _criticalsField;
    private EnumField _priorityField;
    private EnumField _animationTypeField;
    private EnumField _recoilTypeField;
    private IntegerField _recoilDamageField;
    private IntegerField _drainAmountField;
    private EnumField _healTypeField;
    private IntegerField _healAmount;
    private Vector2IntField _hitRangeField;
    private TextField _descriptionField;
    private Button _addFlagButton;
    private ListView _moveFlagsList;
    private VisualElement _primaryEffectsContainer;
    private VisualElement _secondaryEffectsContainer;
    private VisualElement _primaryEffectsGoHere;
    private ListView _secondaryEffectsListView;
    private Button _addSecondaryEffectsButton;

    public static void OpenMoveEditor( MoveSO move )
    {
        OpenedMove = move;
        GetWindow<MoveEditor>( "MoveSO Editor", typeof(PokemonEditor), typeof(MoveEditor), typeof(TrainerEditor), typeof(RentalTeamEditor) );
    }

    private void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    private void OnUndoRedo()
    {
        RefreshDetailPanel();
        RefreshMoveList();
    }

    public void CreateGUI()
    {
        _uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>( "Assets/Editor/Move Editor/MoveEditor.uxml" );
        VisualElement ui = _uxml.CloneTree();
        rootVisualElement.Add( ui );
        SetTypeColorsDictionary();

        //--Build Editor
        _moveListView = rootVisualElement.Q<ListView>( "MoveList" );
        _currentMoveLabel = rootVisualElement.Q<Label>( "CurrentMoveLabel" );
        CreateMoveList();
        _createNewMoveButton = rootVisualElement.Q<Button>( "CreateNewMoveButton" );

        //--Move Info
        _nameField = rootVisualElement.Q<TextField>( "NameField" );
        _ppField = rootVisualElement.Q<IntegerField>( "PPField" );
        _powerField = rootVisualElement.Q<IntegerField>( "PowerField" );
        _accuracyField = rootVisualElement.Q<IntegerField>( "AccuracyField" );

        _accuracyTypeField = rootVisualElement.Q<EnumField>( "AccuracyTypeField" );
        _hasTMToggle = rootVisualElement.Q<Toggle>( "HasTMToggle" );
        _statOverrideToggle = rootVisualElement.Q<Toggle>( "StatOverrideToggle" );

        _targetField = rootVisualElement.Q<EnumField>( "TargetField" );
        _typeField = rootVisualElement.Q<EnumField>( "TypeField" );
        _categoryField = rootVisualElement.Q<EnumField>( "CategoryField" );
        _criticalsField = rootVisualElement.Q<EnumField>( "CriticalsField" );
        _priorityField = rootVisualElement.Q<EnumField>( "PriorityField" );
        _animationTypeField = rootVisualElement.Q<EnumField>( "AnimationTypeField" );
        _recoilTypeField = rootVisualElement.Q<EnumField>( "RecoilTypeField" );

        _recoilDamageField = rootVisualElement.Q<IntegerField>( "RecoilDamageField" );
        _drainAmountField = rootVisualElement.Q<IntegerField>( "DrainAmountField" );
        _healTypeField = rootVisualElement.Q<EnumField>( "HealTypeField" );
        _healAmount = rootVisualElement.Q<IntegerField>( "HealAmountField" );
        _hitRangeField = rootVisualElement.Q<Vector2IntField>( "HitRangeField" );
        _descriptionField = rootVisualElement.Q<TextField>( "DescriptionField" );

        _accuracyTypeField.Init( AccuracyType.Once );
        _targetField.Init( MoveTarget.Enemy );
        _typeField.Init( PokemonType.None );
        _categoryField.Init( MoveCategory.Physical );
        _criticalsField.Init( CritBehavior.None );
        _priorityField.Init( MovePriority.Zero );
        _animationTypeField.Init( MoveAnimationType.None );
        _recoilTypeField.Init( RecoilType.None );
        _healTypeField.Init( HealType.None );

        //--Move Flags
        _addFlagButton = rootVisualElement.Q<Button>( "AddFlagButton" );
        _moveFlagsList = rootVisualElement.Q<ListView>( "MoveFlagsList" );
        CreateMoveFlagsList();

        //--Move Effects
        _primaryEffectsContainer = rootVisualElement.Q<VisualElement>( "PrimaryEffectsContainer" );
        _secondaryEffectsContainer = rootVisualElement.Q<VisualElement>( "SecondaryEffectsContainer" );
        _primaryEffectsGoHere = rootVisualElement.Q<VisualElement>( "PrimaryEffectsGoHere" );
        _secondaryEffectsListView = rootVisualElement.Q<ListView>( "SecondaryEffectsListView" );
        _addSecondaryEffectsButton = rootVisualElement.Q<Button>( "AddSecondaryEffectsButton" );

        RegisterFieldCallbacks();

        if( OpenedMove != null )
            SelectMove( OpenedMove );
        else if( _currentMove != null )
            SelectMove( _currentMove );
        else
            SelectMove( _moveList[0] );
    }

    private void LoadMoves()
    {
        _moveList = new();
        
        string[] guids = AssetDatabase.FindAssets( "t:MoveSO" );

        for( int i = 0; i < guids.Length; i++ )
        {
            string path = AssetDatabase.GUIDToAssetPath( guids[i] );
            var move = AssetDatabase.LoadAssetAtPath<MoveSO>( path );

            if( move != null )
            {
                _moveList.Add( move );
            }
        }

        _moveList.Sort( ( a, b ) => a.Name.CompareTo( b.Name ) );
    }

    private void CreateMoveList()
    {
        LoadMoves();

        _moveListView.itemsSource = _moveList;

        _moveListView.makeItem = () =>
        {
            return new Label();
        };

        _moveListView.bindItem = ( element, index ) =>
        {
            var label = (Label)element;
            var move = _moveList[index];

            label.text = $"{move.Name}";
        };

        _moveListView.selectionType = SelectionType.Single;
        _moveListView.selectionChanged += OnMoveSelected;
        _moveListView.Rebuild();
    }

    private void CreateMoveFlagsList()
    {
        _moveFlagsList.fixedItemHeight = 40;

        _moveFlagsList.makeItem = () =>
        {
            VisualElement row = new();
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

            EnumField enumField = new( MoveFlags.Authentic );
            enumField.style.flexGrow = 1;

            Button removeButton = new(){ text = "Remove Flag" };

            row.Add( enumField );
            row.Add( removeButton );

            row.userData = enumField;
            removeButton.userData = row;

            return row;
        };

        _moveFlagsList.bindItem = ( element, index ) =>
        {
            if( _currentMove == null )
                return;

            var enumField = (EnumField)element.userData;
            enumField.SetValueWithoutNotify( _currentMove.Flags[index] );

            enumField.UnregisterValueChangedCallback( OnMoveFlagAdded );

            enumField.RegisterValueChangedCallback( OnMoveFlagAdded );

            enumField.userData = index;

            var removeButton = element.Q<Button>();
            removeButton.clicked += () =>
            {
                Undo.RecordObject( _currentMove, "Remove Move Flag" );
                _currentMove.RemoveFlag( index );
                EditorUtility.SetDirty( _currentMove );
                RefreshMoveEffects();
            };
        };

        _addFlagButton.clicked += () =>
        {
            if( _currentMove == null )
                return;

            Debug.Log( $"Current Move is: {_currentMove.Name}" );
            Undo.RecordObject( _currentMove, "Add Move Flag" );
            _currentMove.AddFlag( MoveFlags.Authentic );
            EditorUtility.SetDirty( _currentMove );
            RefreshMoveFlagList();
        };
    }

    private void OnMoveFlagAdded( ChangeEvent<Enum> evt )
    {
        if( _currentMove == null )
            return;

        var enumField = (EnumField)evt.target;
        int index = (int)enumField.userData;

        Undo.RecordObject( _currentMove, "Change Ability" );
        _currentMove.SetFlag( index, (MoveFlags)evt.newValue );
        EditorUtility.SetDirty( _currentMove );
        RefreshMoveFlagList();
    }

    private VisualElement GetNewEffectsRow()
    {
        var row = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>( "Assets/Editor/Move Editor/MoveEffects.uxml" );
        VisualElement slot = row.CloneTree();
        slot.style.marginTop = 5;
        slot.style.marginBottom = 5;
        slot.style.marginLeft = 5;
        slot.style.marginRight = 5;

        return slot;
    }

    private void CreateAndBindPrimaryEffects()
    {
        var row = GetNewEffectsRow();

        _primaryEffectsRow = new( row, _primaryEffectsProp, index: 0 );
        _primaryEffectsRow.Bind();

        _primaryEffectsGoHere.Add( row );
    }

    private void RefreshMoveEffects()
    {
        _primaryEffectsGoHere.Clear();
        
        if( _currentMove == null )
            return;

        _moveObject = new( _currentMove );
        _primaryEffectsProp = _moveObject.FindProperty( "_moveEffects" );
        CreateAndBindPrimaryEffects();

        _secondaryEffectsProp = _moveObject.FindProperty( "_secondaryMoveEffects" );
        CreateSecondaryEffectsList();
    }

    private void CreateSecondaryEffectsList()
    {
        _secondaryEffectsListView.itemsSource = new int[_secondaryEffectsProp.arraySize];

        _secondaryEffectsListView.makeItem = () =>
        {
            var row = GetNewEffectsRow();
            row.style.flexGrow = 0;
            row.style.flexShrink = 0;
            row.style.height = StyleKeyword.Auto;

            return row;
        };

        _secondaryEffectsListView.bindItem = ( element, index ) =>
        {
            element.userData = index;

            SerializedProperty effectProp = _secondaryEffectsProp.GetArrayElementAtIndex( index );

            MoveEffectsSlot slot = new( element, effectProp, index );
            slot.Bind( true );

            var foldout = element.Q<Foldout>();

            foldout.RegisterValueChangedCallback( _ =>
            {
                _secondaryEffectsListView.RefreshItems();
            });

            var removeButton = element.Q<Button>( "RemoveButton" );

            removeButton.RegisterCallback<ClickEvent>( evt =>
            {
                int removeIndex = (int)element.userData;

                if( removeIndex < 0 || removeIndex >= _secondaryEffectsProp.arraySize )
                    return;

                _secondaryEffectsProp.DeleteArrayElementAtIndex( removeIndex );
                _moveObject.ApplyModifiedProperties();

                RefreshMoveEffects();
            });
        };

        _secondaryEffectsListView.reorderable = true;
        _secondaryEffectsListView.showBorder = true;
        _secondaryEffectsListView.selectionType = SelectionType.None;

        _secondaryEffectsListView.Rebuild();
    }

    private void RegisterFieldCallbacks()
    {
        _createNewMoveButton.RegisterCallback<ClickEvent>( evt =>
        {
            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>( "Assets/Resources/Moves" );
            if (folder != null)
                Selection.activeObject = folder;

            string path = EditorUtility.SaveFilePanelInProject( "Create New MoveSO", "NewMove", "asset", "Choose save location", "Assets/Resources/Moves" );

            if( string.IsNullOrEmpty( path ) )
                return;

            CreateNewMoveAsset( path );
            RefreshMoveList();
        });

        _nameField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move Name" );
            _currentMove.SetName( evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _ppField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move PP" );
            _currentMove.SetPP( evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _powerField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move Power" );
            _currentMove.SetPower( evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _accuracyField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move Accuracy" );
            _currentMove.SetAccuracy( evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _accuracyTypeField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move AlwaysHits" );
            _currentMove.SetAccuracyType( (AccuracyType)evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _hasTMToggle.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move HasTM" );
            _currentMove.SetHasTM( evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _statOverrideToggle.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move Stat Override" );
            _currentMove.SetStatOverride( evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _targetField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move Target" );
            _currentMove.SetTarget( (MoveTarget)evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _typeField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move Type" );
            _currentMove.SetType( (PokemonType)evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _categoryField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move Category" );
            _currentMove.SetCateogry( (MoveCategory)evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _criticalsField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move Critical Behavior" );
            _currentMove.SetCriticals( (CritBehavior)evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _priorityField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move Priority" );
            _currentMove.SetPriority( (MovePriority)evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _animationTypeField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move Animation Type" );
            _currentMove.SetAnimationType( (MoveAnimationType)evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _recoilTypeField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move RecoilType" );
            _currentMove.SetRecoilType( (RecoilType)evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _recoilDamageField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move Recoil Damage" );
            _currentMove.SetRecoilDamage( evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _drainAmountField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move Drain Amount" );
            _currentMove.SetDrainAmount( evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _healTypeField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move Heal Type" );
            _currentMove.SetHealType( (HealType)evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _healAmount.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move Heal Amount" );
            _currentMove.SetHealAmount( evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _hitRangeField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move Hit Range" );
            _currentMove.SetHitRange( evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _descriptionField.RegisterValueChangedCallback( evt =>
        {
            if( _currentMove == null )
                return;

            Undo.RecordObject( _currentMove, "Change Move Description" );
            _currentMove.SetDescription( evt.newValue );
            EditorUtility.SetDirty( _currentMove );
            RefreshDetailPanel();
        });

        _addSecondaryEffectsButton.RegisterCallback<ClickEvent>( evt =>
        {
            _secondaryEffectsProp.arraySize++;
            _moveObject.ApplyModifiedProperties();
            _secondaryEffectsListView.RefreshItems();
            RefreshDetailPanel();
        });
    }

    private void CreateNewMoveAsset( string path )
    {
        var move = CreateInstance<MoveSO>();

        AssetDatabase.CreateAsset( move, path );
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        move.SetName( move.name );

        CreateMoveList();
        RefreshMoveList();

        SelectMove( move );
    }

    private void RefreshMoveList()
    {
        _moveList.Sort( ( a, b ) => a.Name.CompareTo( b.Name ) );
        _moveListView.Rebuild();
    }

    private void SelectMove( MoveSO move )
    {
        _currentMove = move;
        RefreshDetailPanel();

        int index = _moveList.IndexOf( move );
        if( index >= 0 )
            _moveListView.SetSelection( index );
    }

    private void OnMoveSelected( IEnumerable<object> selection )
    {
        MoveSO selectedMove = null;

        foreach( var item in selection )
        {
            selectedMove = item as MoveSO;
            break;
        }

        _currentMove = selectedMove;

        RefreshDetailPanel();
    }

    private void RefreshDetailPanel()
    {
        if( _currentMove == null )
        {
            _nameField.SetValueWithoutNotify( string.Empty );
            return;
        }

        //--Label
        var typeColor = TypeColors[_currentMove.Type];
        _currentMoveLabel.text = $"({_currentMove.name}) - {_currentMove.Name} | {_currentMove.Type} | Power: {_currentMove.Power} | {_currentMove.MoveCategory} | Accuracy: {_currentMove.Accuracy} | PP: {_currentMove.PP} | {_currentMove.MoveTarget}";
        _currentMoveLabel.style.backgroundColor      = typeColor.PrimaryColor;
        _currentMoveLabel.style.borderTopColor       = typeColor.SecondaryColor;
        _currentMoveLabel.style.borderBottomColor    = typeColor.SecondaryColor;
        _currentMoveLabel.style.borderLeftColor      = typeColor.SecondaryColor;
        _currentMoveLabel.style.borderRightColor     = typeColor.SecondaryColor;

        //--Move Info
        _nameField.SetValueWithoutNotify( _currentMove.Name );
        _ppField.SetValueWithoutNotify( _currentMove.PP );
        _powerField.SetValueWithoutNotify( _currentMove.Power );
        _accuracyField.SetValueWithoutNotify( _currentMove.Accuracy );
        _accuracyTypeField.SetValueWithoutNotify( _currentMove.AccuracyType );
        _hasTMToggle.SetValueWithoutNotify( _currentMove.HasTM );
        _statOverrideToggle.SetValueWithoutNotify( _currentMove.OverrideAttackStat );
        _targetField.SetValueWithoutNotify( _currentMove.MoveTarget );
        _typeField.SetValueWithoutNotify( _currentMove.Type );
        _categoryField.SetValueWithoutNotify( _currentMove.MoveCategory );
        _criticalsField.SetValueWithoutNotify( _currentMove.CritBehavior );
        _priorityField.SetValueWithoutNotify( _currentMove.MovePriority );
        _animationTypeField.SetValueWithoutNotify( _currentMove.AnimationType );
        _recoilTypeField.SetValueWithoutNotify( _currentMove.Recoil.RecoilType );
        _recoilDamageField.SetValueWithoutNotify( _currentMove.Recoil.RecoilDamage );
        _drainAmountField.SetValueWithoutNotify( _currentMove.DrainPercentage );
        _healTypeField.SetValueWithoutNotify( _currentMove.HealType );
        _healAmount.SetValueWithoutNotify( _currentMove.HealAmount );
        _hitRangeField.SetValueWithoutNotify( _currentMove.HitRange );
        _descriptionField.SetValueWithoutNotify( _currentMove.Description );

        //--Move Flags
        RefreshMoveFlagList();

        //--Move Effects
        RefreshMoveEffects();
    }

    private void RefreshMoveFlagList()
    {
        _moveFlagsList.itemsSource = _currentMove.Flags;
        _moveFlagsList.Rebuild();
    }

    private void SetTypeColorsDictionary(){
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

public class MoveEffectsSlot
{
    private int _index;
    private VisualElement _moveEffectsRow;
    private SerializedProperty _moveEffectsProp;
    private ListView _statStageListView;
    private SerializedProperty _statStageListProp;
    public int Index => _index;
    public SerializedProperty MoveEffectsProp => _moveEffectsProp;

    public MoveEffectsSlot( VisualElement row, SerializedProperty prop, int index )
    {
        _moveEffectsRow = row;
        _moveEffectsProp = prop;
        _index = index;
    }

    public void Bind( bool secondary = false )
    {
        //--Cache Fields
        var chance                      = _moveEffectsRow.Q<IntegerField>( "ChanceField" );
        var removeButton                = _moveEffectsRow.Q<Button>( "RemoveButton" );
        var addStatStageChangeButton    = _moveEffectsRow.Q<Button>( "AddStatStageChangeButton" );
        var target                      = _moveEffectsRow.Q<EnumField>( "TargetField" );
        var trigger                     = _moveEffectsRow.Q<EnumField>( "TriggerField" );
        var severeStatus                = _moveEffectsRow.Q<EnumField>( "SevereStatusField" );
        var volatileStatus              = _moveEffectsRow.Q<EnumField>( "VolatileStatusField" );
        var transientStatus             = _moveEffectsRow.Q<EnumField>( "TransientStatusField" );
        var bindingStatus               = _moveEffectsRow.Q<EnumField>( "BindingStatusField" );
        var weather                     = _moveEffectsRow.Q<EnumField>( "WeatherField" );
        var terrain                     = _moveEffectsRow.Q<EnumField>( "TerrainField" );
        var courtCondition              = _moveEffectsRow.Q<EnumField>( "CourtConditionField" );
        var switchType                  = _moveEffectsRow.Q<EnumField>( "SwitchEffectTypeField" );

        _statStageListView              = _moveEffectsRow.Q<ListView>( "StatStageChangeList" );
        _statStageListProp              = _moveEffectsProp.FindPropertyRelative( "_statChangeList" );

        SerializedProperty chanceProp               = _moveEffectsProp.FindPropertyRelative( "_chance" );
        SerializedProperty targetProp               = _moveEffectsProp.FindPropertyRelative( "_target" );
        SerializedProperty triggerProp              = _moveEffectsProp.FindPropertyRelative( "_trigger" );
        SerializedProperty severeStatusProp         = _moveEffectsProp.FindPropertyRelative( "_severeStatus" );
        SerializedProperty volatileStatusProp       = _moveEffectsProp.FindPropertyRelative( "_volatileStatus" );
        SerializedProperty transientStatusProp      = _moveEffectsProp.FindPropertyRelative( "_transientStatus" );
        SerializedProperty bindingStatusProp        = _moveEffectsProp.FindPropertyRelative( "_bindingStatus" );
        SerializedProperty weatherProp              = _moveEffectsProp.FindPropertyRelative( "_weather" );
        SerializedProperty terrainProp              = _moveEffectsProp.FindPropertyRelative( "_terrain" );
        SerializedProperty courtConditionProp       = _moveEffectsProp.FindPropertyRelative( "_courtCondition" );
        SerializedProperty switchTypeProp           = _moveEffectsProp.FindPropertyRelative( "_switchType" );

        chance.SetEnabled( false );
        removeButton.SetEnabled( false );

        //--Bind Enum Helper. Initializes and does SetValueWithoutNotify.
        BindAndRegisterEnum<EffectTarget>( target, targetProp );
        BindAndRegisterEnum<MoveEffectTrigger>( trigger, triggerProp );
        BindAndRegisterEnum<SevereConditionID>( severeStatus, severeStatusProp );
        BindAndRegisterEnum<VolatileConditionID>( volatileStatus, volatileStatusProp );
        BindAndRegisterEnum<TransientConditionID>( transientStatus, transientStatusProp );
        BindAndRegisterEnum<BindingConditionID>( bindingStatus, bindingStatusProp );
        BindAndRegisterEnum<WeatherConditionID>( weather, weatherProp );
        BindAndRegisterEnum<TerrainID>( terrain, terrainProp );
        BindAndRegisterEnum<CourtConditionID>( courtCondition, courtConditionProp );
        BindAndRegisterEnum<SwitchEffectType>( switchType, switchTypeProp );

        // //--Set Values
        if( secondary )
        {
            chance.SetEnabled( true );
            removeButton.SetEnabled( true );
            chance.SetValueWithoutNotify( chanceProp.intValue );
        }

        chance.RegisterValueChangedCallback( evt =>
        {
            chanceProp.intValue = evt.newValue;
            _moveEffectsProp.serializedObject.ApplyModifiedProperties();
        });

        addStatStageChangeButton.clicked += () =>
        {
            _statStageListProp.arraySize++;
            _moveEffectsProp.serializedObject.ApplyModifiedProperties();
            _statStageListView.RefreshItems();
            RefreshStatStageChangeList();
        };

        CreateStatStageChangeList();
    }

    private void CreateStatStageChangeList()
    {
        _statStageListView.itemsSource = new int[_statStageListProp.arraySize];

        _statStageListView.makeItem = () =>
        {
            var row = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>( "Assets/Editor/Move Editor/StatStageChangeRow.uxml" );
            return row.CloneTree();
        };

        _statStageListView.bindItem = ( element, index ) =>
        {
            element.userData = index;

            SerializedProperty statStageChangeProp  = _statStageListProp.GetArrayElementAtIndex( index );
            SerializedProperty statProp             = statStageChangeProp.FindPropertyRelative( "Stat" );
            SerializedProperty changeProp           = statStageChangeProp.FindPropertyRelative( "Change" );

            var statStageChangeLabel    = element.Q<Label>( "StatStageChangeLabel" );
            var statField               = element.Q<EnumField>( "StatField" );
            var changeSlider            = element.Q<SliderInt>( "ChangeSlider" );
            var changeValueLabel        = element.Q<Label>( "ChangeValueLabel" );
            var removeButton            = element.Q<Button>( "RemoveButton" );

            //--Init & Set Value
            statField.Init( Stat.Attack );
            statField.SetValueWithoutNotify( (Stat)statProp.enumValueIndex );
            changeSlider.SetValueWithoutNotify( changeProp.intValue );
            changeValueLabel.text = $"{changeProp.intValue}";
            statStageChangeLabel.text = $"{(Stat)statProp.enumValueIndex} {changeProp.intValue}";

            statField.RegisterValueChangedCallback( evt =>
            {
                statProp.enumValueIndex = Convert.ToInt32( evt.newValue );
                statStageChangeLabel.text = $"{(Stat)statProp.enumValueIndex} {changeProp.intValue}";

                _moveEffectsProp.serializedObject.ApplyModifiedProperties();
            });

            changeSlider.RegisterValueChangedCallback( evt =>
            {
                changeProp.intValue = evt.newValue;
                changeValueLabel.text = evt.newValue.ToString();
                statStageChangeLabel.text = $"{(Stat)statProp.enumValueIndex} {changeProp.intValue}";

                _moveEffectsProp.serializedObject.ApplyModifiedProperties();
            });

            removeButton.clicked += () =>
            {
                int removeIndex = (int)element.userData;

                if( removeIndex < 0 || removeIndex >= _statStageListProp.arraySize )
                    return;

                _statStageListProp.DeleteArrayElementAtIndex( removeIndex );
                _moveEffectsProp.serializedObject.ApplyModifiedProperties();

                RefreshStatStageChangeList();
            };
        };

        _statStageListView.Rebuild();
    }

    private void BindAndRegisterEnum<T>( EnumField field, SerializedProperty prop ) where T : Enum
    {
        T value = (T)Enum.ToObject( typeof(T), prop.enumValueIndex );
        field.Init( value );
        field.SetValueWithoutNotify( value );

        field.RegisterValueChangedCallback( evt =>
        {
            prop.enumValueIndex = Convert.ToInt32( evt.newValue );
            _moveEffectsProp.serializedObject.ApplyModifiedProperties();
        });
    }

    private void RefreshStatStageChangeList()
    {
        _statStageListView.itemsSource = new int[_statStageListProp.arraySize];
        _statStageListView.Rebuild();
    }
}
