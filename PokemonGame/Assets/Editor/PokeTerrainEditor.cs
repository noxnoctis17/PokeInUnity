using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

public enum CurrentTool{ GridSettings, TilePlacer, RoadPainter, PrefabPlacer, }
public class PokeTerrainEditor : EditorWindow
{
    const string UNDO_STR_SNAP  = "snap objects";

    [MenuItem( "Tools/Poke Terrain Editor" )]
    public static void OpenPokeTerrain() => GetWindow<PokeTerrainEditor>( "Poke Terrain Editor" );

//-----------------------------------------------------------------------

    public CurrentTool CurrentTool;
    public GameObject GridObject;
    public List<MapObjects> MapObjects;
    public GameObject SelectedMapObject;
    public Material PreviewMaterial;
    public int GridCellSize;
    public int GridExtent;
    private RaycastHit _cursor;
    private RaycastHit _previousHit;
    private Quaternion _selectedMapObjectRotation;
    public int HeightOffset;
    public float BrushOpacity = 1;
    public int BrushSize = 1;
    public float TileHeight = 1;
    public TileDirection TileDirection;
    public float TileSlopeStrength = 1f;
    public float TileSlopeWidth = 0.6f;
    public int PaintTextureLayer;
    public Texture2D SelectedRoadMask;
    public Texture2D[] RoadMasks;
    public Terrain ActiveTerrain;
    public TerrainLayer[] TerrainLayers;
    private GameObject _ledgeCollider_Side;
    private GameObject _cliffCollider_Side;
    private GameObject _ledgeCollider_Corner;
    private GameObject _cliffCollider_Corner;
    private Vector2 _terrainLayerScrollPos;
    private Vector2 _roadMasksScrollPos;
    private bool _editMode;
    private bool _showGrid;
    private bool _isLedge;
    private bool _isCliff;

    private SerializedObject _sobj;
    private SerializedProperty _propGridObject;
    private SerializedProperty _propSelectedMapObject;
    private SerializedProperty _propGridCellSize;
    private SerializedProperty _propGridExtent;
    private SerializedProperty _propHeightOffset;
    private SerializedProperty _propPreviewMaterial;
    private SerializedProperty _brushOpacity;
    private SerializedProperty _brushSize;
    private SerializedProperty _tileHeight;
    private SerializedProperty _tileDirection;
    private SerializedProperty _tileSlopeStrength;
    private SerializedProperty _tileSlopeWidth;
    private SerializedProperty _paintTextureLayer;
    private SerializedProperty _roadMask;

    private void OnEnable(){
        //--Get Grid Object and Set Material badly
        GridObject = FindObjectOfType<GridObject>().gameObject;
        PreviewMaterial = GridObject.GetComponent<GridObject>().PreviewMaterial;

        //--Serialize This and its Properties
        _sobj = new SerializedObject(this);
        _propGridObject                     = _sobj.FindProperty( "GridObject" );
        _propGridCellSize                   = _sobj.FindProperty( "GridCellSize" );
        _propGridExtent                     = _sobj.FindProperty( "GridExtent" );
        _propGridObject                     = _sobj.FindProperty( "GridObject" );
        _propSelectedMapObject              = _sobj.FindProperty( "SelectedMapObject" );
        _propPreviewMaterial                = _sobj.FindProperty( "PreviewMaterial" );
        _propHeightOffset                   = _sobj.FindProperty( "HeightOffset" );
        _brushOpacity                       = _sobj.FindProperty( "BrushOpacity" );
        _brushSize                          = _sobj.FindProperty( "BrushSize" );
        _tileHeight                         = _sobj.FindProperty( "TileHeight" );
        _tileDirection                      = _sobj.FindProperty( "TileDirection" );
        _tileSlopeStrength                  = _sobj.FindProperty( "TileSlopeStrength" );
        _tileSlopeWidth                     = _sobj.FindProperty( "TileSlopeWidth" );
        _paintTextureLayer                  = _sobj.FindProperty( "PaintTextureLayer" );
        _roadMask                           = _sobj.FindProperty( "SelectedRoadMask" );

        //--Load Editor Prefs
        GridCellSize = EditorPrefs.GetInt( "MAP_OBJECT_PLACER_TOOL_GridCellSize", 1 );
        GridExtent = EditorPrefs.GetInt( "MAP_OBJECT_PLACER_TOOL_GridExtent", 200 );

        //--
        SceneView.duringSceneGui    += DuringSceneGUI;
        Selection.selectionChanged  += Repaint;

        //--Load Map Object Prefabs
        string[] guids = AssetDatabase.FindAssets( "t:prefab", new[] { "Assets/_Prefabs/MapObjects" } );
        IEnumerable<string> paths = guids.Select( AssetDatabase.GUIDToAssetPath );
        GameObject[] objs = paths.Select( AssetDatabase.LoadAssetAtPath<GameObject> ).ToArray();
        MapObjects = objs.Select( go => go.GetComponent<MapObjects>() ).Where( c => c != null ).ToList();
        objs = null;

        for( int i = 0; i < MapObjects.Count; i++ ){
            if( MapObjects[i].PrimaryType != MapObject_PrimaryType.Foliage ){
                MapObjects.Remove( MapObjects[i] );
            }
        }

        if( MapObjects != null || MapObjects.Count > 0 ){
            SelectedMapObject = MapObjects[0].gameObject;
            _selectedMapObjectRotation = Quaternion.LookRotation( _cursor.normal );
            HeightOffset = 0;
        }

        //--Load all available Road Masks from Assets
        guids = AssetDatabase.FindAssets( "t:Texture2D", new[] { "Assets/Resources/Textures/RoadMasks" } );
        paths = guids.Select( AssetDatabase.GUIDToAssetPath );
        RoadMasks = paths.Select( AssetDatabase.LoadAssetAtPath<Texture2D> ).ToArray();
        
        if( RoadMasks.Length > 0 ){
            SelectedRoadMask = RoadMasks[0];
        }

        //--Load Ledge and Cliff colliders
        _ledgeCollider_Side = AssetDatabase.LoadAssetAtPath<GameObject>( "Assets/_Prefabs/MapObjects/Foliage/LedgeCollider.prefab" );
        _ledgeCollider_Corner = AssetDatabase.LoadAssetAtPath<GameObject>( "Assets/_Prefabs/MapObjects/Foliage/LedgeCollider_Corner.prefab" );
        // _cliffCollider = AssetDatabase.LoadAssetAtPath<GameObject>( "Assets/_Prefabs/MapObjects/Foliage/CliffCollider.prefab" );
        

    }

    private void OnDisable(){
        Selection.selectionChanged  -= Repaint;
        SceneView.duringSceneGui    -= DuringSceneGUI;

        //--Save Editor Prefs
        EditorPrefs.SetInt( "MAP_OBJECT_PLACER_TOOL_GridCellSize", GridCellSize );
        EditorPrefs.SetInt( "MAP_OBJECT_PLACER_TOOL_GridExtent", GridExtent );
    }

    private void OnGUI(){
        _sobj.Update();
        if( Event.current.type == EventType.MouseDown && Event.current.button == 0 ){
            GUI.FocusControl( null );
            Repaint();
        }

        GUILayout.Space( 10f );

        GUILayout.BeginHorizontal();
        GUILayout.Label( "Show Grid" );
        _showGrid = EditorGUILayout.Toggle( _showGrid );

        GUILayout.Label( "Enable Editing" );
        _editMode = EditorGUILayout.Toggle( _editMode );
        GUILayout.EndHorizontal();

        GUILayout.Space( 10f );

        CurrentTool = (CurrentTool)EditorGUILayout.EnumPopup( "Current Tool", CurrentTool );

        switch( CurrentTool )
        {   
            case CurrentTool.GridSettings:
                GUILayout.Label( "Grid Cellsize" );
                EditorGUILayout.PropertyField( _propGridExtent );
                EditorGUILayout.PropertyField( _propGridCellSize );
                EditorGUILayout.PropertyField( _propHeightOffset );

                GUILayout.Space( 10f );

                GUILayout.Label( "Snap Selected to World Grid" );
                using(new EditorGUI.DisabledScope( Selection.gameObjects.Length == 0 ) )
                if( GUILayout.Button( "Snap Selection (Y: 0)" ) ){
                    SnapSelectedThings();
                }

                using(new EditorGUI.DisabledScope( Selection.gameObjects.Length == 0 ) )
                if( GUILayout.Button( "Snap Selection (XZ)" ) ){
                    SnapSelectedThingsXZ();
                }
            break;

            case CurrentTool.TilePlacer:
                GUILayout.Label( "Brush" );
                EditorGUILayout.PropertyField( _brushOpacity );
                EditorGUILayout.PropertyField( _brushSize );
                EditorGUILayout.PropertyField( _tileHeight );
                EditorGUILayout.PropertyField( _tileSlopeStrength );
                EditorGUILayout.PropertyField( _tileSlopeWidth );
                GUILayout.Space( 10f );
                DrawTileHeightPresets();
                GUILayout.Space( 10f );
                TileDirection = (TileDirection)EditorGUILayout.EnumPopup( "Tile Direction", TileDirection );
                DrawTileButtons( CurrentTool.TilePlacer, -50 );
            break;

            case CurrentTool.RoadPainter:
                GUILayout.Label( "Brush" );
                EditorGUILayout.PropertyField( _brushOpacity );
                EditorGUILayout.PropertyField( _brushSize );
                EditorGUILayout.PropertyField( _paintTextureLayer );
                GUILayout.Space( 10f );
                TileDirection = (TileDirection)EditorGUILayout.EnumPopup( "Tile Direction", TileDirection );
                GUILayout.Space( 20f );
                DrawRoadMaskButtons();
                DrawTerrainLayerButtons();
                DrawTileButtons( CurrentTool.RoadPainter, 55 );
            break;

            case CurrentTool.PrefabPlacer:
                //--------------Prefab Select Buttons--------------------
                Handles.BeginGUI();
                Rect rect = new( 5, 200, 200, 30 );

                foreach( MapObjects mapObject in MapObjects )
                {
                    if( GUI.Button( rect, mapObject.gameObject.name ) ){
                        SelectedMapObject = mapObject.gameObject;
                        SelectedMapObject.transform.rotation = _selectedMapObjectRotation;
                        Repaint();
                    }

                    rect.y += rect.height + 2;
                }

                Handles.EndGUI();
            break;
        }
        
        GUILayout.Space( 20f );

        if( _sobj.ApplyModifiedProperties() ){
            SceneView.RepaintAll();
        }

    }

    private void DuringSceneGUI( SceneView sceneView ){
        Handles.zTest = CompareFunction.LessEqual;

        //--Grid
        if( _showGrid )
            DrawGrid();
        
        //--Only allow editing if edit mode is checked
        if( !_editMode )
            return;
        

        //----------------------Cursor--------------------------

        if( Event.current.type == EventType.MouseMove )
            sceneView.Repaint();
        

        Ray ray = HandleUtility.GUIPointToWorldRay( Event.current.mousePosition );

        if( Physics.Raycast( ray, out RaycastHit hit ) ){
            _cursor = hit;
        }

        Vector3 wireCube = _cursor.point;
        wireCube.x = _cursor.point.x;
        wireCube.z = _cursor.point.z;

        Handles.color = Color.red;
        Handles.DrawWireCube( wireCube, new Vector3( BrushSize, BrushSize, BrushSize ) );

        //--------------Paint Map Functions---------------

            //--Left Click to Raise Height, hold control while clicking to lower it.
        if( Event.current.type == EventType.MouseDown && Event.current.button == 0 ){
            //--Register Undo
            if( ActiveTerrain != null)
                Undo.RegisterCompleteObjectUndo( ActiveTerrain.terrainData, "Modify Terrain" );

            switch( CurrentTool )
            {
                case CurrentTool.TilePlacer:
                    //--ERASE------------------------------------------
                    if( Event.current.control )
                        //--ERASE OVER DISTANCE------------------------
                        if( Event.current.shift ){
                            Vector3 start = _previousHit.point;
                            Vector3 end = _cursor.point;
                            Vector3 direction = (end - start).normalized;
                            float distance = Vector3.Distance( start, end );

                            for( float i = BrushSize; i <= distance; i += BrushSize ){
                                Vector3 pos = start + direction * i;
                                if( Physics.Raycast( pos + Vector3.up * 100, Vector3.down, out RaycastHit tile ) ){
                                    TryLowerTerrain( tile );
                                }
                            }
                        }
                        else //--ERASE JUST ONE----------------
                            TryLowerTerrain( _cursor );
                    else{
                    //--PAINT--------------------------------
                        //--PAINT OVER DISTANCE----------------
                        if( Event.current.shift ){
                            Vector3 start = _previousHit.point;
                            Vector3 end = _cursor.point;
                            Vector3 direction = (end - start).normalized;
                            float distance = Vector3.Distance( start, end );
                            float step = 1;

                            for( float i = step; i <= distance; i += step ){
                                Vector3 pos = start + direction * i;
                                if( Physics.Raycast( pos + Vector3.up * 2, Vector3.down, out RaycastHit tile ) ){
                                    TryRaiseTerrain( tile );
                                }
                            }
                        }
                        else{
                            //--PLACE LEDGE/CLIFF COLLIDER
                            if( _isLedge ){
                                if( TileDirection == TileDirection.TopLeft || TileDirection == TileDirection.TopRight || TileDirection == TileDirection.BottomLeft || TileDirection == TileDirection.BottomRight ){
                                    SelectedMapObject = _ledgeCollider_Corner;
                                    Debug.Log( $"Cockin a ledge: {TileDirection}" );
                                }
                                else
                                    SelectedMapObject = _ledgeCollider_Side;

                                TryPlaceObject( _cursor );
                            }
                            else if( _isCliff ){
                                SelectedMapObject = _cliffCollider_Side;
                            }
                        //--JUST RAISE THE DAMN TERRAIN ONCE, SHEESH
                            TryRaiseTerrain( _cursor );
                        }
                    }
                break;

                case CurrentTool.RoadPainter:
                    if( Event.current.control )
                        Debug.Log( "Clearing a road" );
                    else
                        TryPaintRoad( _cursor );
                break;

                case CurrentTool.PrefabPlacer:
                    if( Event.current.control )
                        Debug.Log( "Deleting a prefab" );
                    else
                        TryPlaceObject( _cursor );
                break;
            }
            _previousHit = _cursor;
        }
           
    }

    private void OnSelectionChange(){
        //--Get Active Terrain and cache it
        var terrain = Selection.activeGameObject?.GetComponent<Terrain>();
        if( terrain != null){
            ActiveTerrain = terrain;
            TerrainLayers = terrain.terrainData.terrainLayers;
            Repaint();
        }
    }

    private void DrawRoadMaskButtons(){
        if( RoadMasks == null | RoadMasks.Length == 0 ){
            GUILayout.Label( "No Road Masks were found!", EditorStyles.helpBox );
            return;
        }

        GUILayout.Label( "Road Masks", EditorStyles.boldLabel );
        _roadMasksScrollPos = EditorGUILayout.BeginScrollView( _roadMasksScrollPos, GUILayout.Height( 120 ) );
        GUILayout.BeginHorizontal();

        for( int i = 0; i < RoadMasks.Length; i++ ){
            Texture2D mask = RoadMasks[i];
            if( mask == null )
                return;

            Rect buttonRect = GUILayoutUtility.GetRect( 100, 100, GUILayout.ExpandWidth( false ) );

            //--Draw preview texture
            Texture2D preview = AssetPreview.GetAssetPreview( mask );
            if( preview != null)
                GUI.DrawTexture( buttonRect, preview, ScaleMode.ScaleToFit );

            //--Label
            GUI.Label( new Rect( buttonRect.x, buttonRect.yMax - 16, buttonRect.width, 16 ), mask.name, EditorStyles.whiteBoldLabel );

            //--Detect Selection
            if( Event.current.type == EventType.MouseDown && buttonRect.Contains( Event.current.mousePosition ) ){
                SelectedRoadMask = mask;
                Repaint();
            }

            //--Draw Highlight
            if( mask == SelectedRoadMask ){
                Handles.BeginGUI();
                Handles.DrawSolidRectangleWithOutline( buttonRect, new Color( 0, 0, 0, 0 ), Color.yellow );
                Handles.EndGUI();
            }
        }

        GUILayout.EndHorizontal();
        GUILayout.EndScrollView();

    }

    private void DrawTerrainLayerButtons(){
        if( TerrainLayers == null || TerrainLayers.Length == 0 ){
            GUILayout.Label( "No terrain layers found or no terrain selected.", EditorStyles.helpBox );
            return;
        }

        GUILayout.Label( "Paint Terrain Layers", EditorStyles.boldLabel );

        _terrainLayerScrollPos = EditorGUILayout.BeginScrollView( _terrainLayerScrollPos, GUILayout.Height( 120 ) );
        GUILayout.BeginHorizontal();

        for( int i = 0; i < TerrainLayers.Length; i++ ){
            TerrainLayer layer = TerrainLayers[i];
            if( layer == null )
                return;

            Rect buttonRect = GUILayoutUtility.GetRect( 100, 100, GUILayout.ExpandWidth( false ) );

            //--Draw preview texture
            Texture2D preview = AssetPreview.GetAssetPreview( layer.diffuseTexture );
            if( preview != null )
                GUI.DrawTexture( buttonRect, preview, ScaleMode.ScaleToFit );

            //--Label
            GUI.Label( new Rect( buttonRect.x, buttonRect.yMax - 16, buttonRect.width, 16 ), layer.diffuseTexture.name, EditorStyles.whiteMiniLabel );

            //--Detect Selection
            if( Event.current.type == EventType.MouseDown && buttonRect.Contains( Event.current.mousePosition ) ){
                PaintTextureLayer = i;
                Repaint();
            }

            //--Draw Highlight
            if( i == PaintTextureLayer){
                Handles.BeginGUI();
                Handles.DrawSolidRectangleWithOutline( buttonRect, new Color( 0, 0, 0, 0 ), Color.yellow );
                Handles.EndGUI();
            }
        }

        GUILayout.EndHorizontal();
        GUILayout.EndScrollView();

    }

    private void DrawTileHeightPresets(){
        GUILayout.BeginHorizontal();

        if( GUILayout.Button( "Cliff" ) ){
            TileHeight = 1f;
            TileSlopeStrength = 1f;
            TileSlopeWidth = 0.6f;
        }

        if( GUILayout.Button( "Ledge" ) ){
            TileHeight = 0.5f;
            TileSlopeStrength = 0.25f;
            TileSlopeWidth = 0.25f;
        }

        if( GUILayout.Button( "Cliff Ramp" ) ){
            TileHeight = 1f;
            TileSlopeStrength = 1f;
            TileSlopeWidth = 1f;
        }

        if( GUILayout.Button( "Ledge Ramp" ) ){
            TileHeight = 0.5f;
            TileSlopeStrength = 1f;
            TileSlopeWidth = 1f;
        }

        GUILayout.EndHorizontal();

        GUILayout.Space( 10f );

        GUILayout.BeginHorizontal();

        GUILayout.Label( "Place Cliff Collider" );
        _isCliff = EditorGUILayout.Toggle( _isCliff );

        GUILayout.Label( "Place Ledge Collider" );
        _isLedge = EditorGUILayout.Toggle( _isLedge );

        GUILayout.EndHorizontal();
    }

    private void DrawTileButtons( CurrentTool tool, int spacer ){
        //--Change button icon assignment based on current tool
        // switch( tool )
        // {
        //     case CurrentTool.TilePlacer:
        //     break;

        //     case CurrentTool.RoadPainter:
        //     break;
        // }

        if( GUI.Button( new Rect( 50, 395 + spacer, 45, 45 ), new GUIContent( "TL" ) ) ){
            TileDirection = TileDirection.TopLeft;
        }

        if( GUI.Button( new Rect( 100, 395 + spacer, 150, 45 ), new GUIContent( "UP" ) ) ){
            TileDirection = TileDirection.Up;
        }

        if( GUI.Button( new Rect( 255, 395 + spacer, 45, 45 ), new GUIContent( "TR" ) ) ){
            TileDirection = TileDirection.TopRight;
        }

        if( GUI.Button( new Rect( 50, 445 + spacer, 45, 150 ), new GUIContent( "LEFT" ) ) ){
            TileDirection = TileDirection.Left;
        }

        if( GUI.Button( new Rect( 100, 445 + spacer, 45, 45 ), new GUIContent( "InTL" ) ) ){
            TileDirection = TileDirection.InnerTopLeft;
        }

        if( GUI.Button( new Rect( 100, 495 + spacer, 150, 45 ), new GUIContent( "CENTER" ) ) ){
            TileDirection = TileDirection.Center;
        }

        if( GUI.Button( new Rect( 205, 445 + spacer, 45, 45 ), new GUIContent( "InTR" ) ) ){
            TileDirection = TileDirection.InnerTopRight;
        }

        if( GUI.Button( new Rect( 255, 445 + spacer, 45, 150 ), new GUIContent( "RGHT" ) ) ){
            TileDirection = TileDirection.Right;
        }

        if( GUI.Button( new Rect( 100, 545 + spacer, 45, 45 ), new GUIContent( "InBL" ) ) ){
            TileDirection = TileDirection.InnerBottomLeft;
        }

        if( GUI.Button( new Rect( 205, 545 + spacer, 45, 45 ), new GUIContent( "InBR" ) ) ){
            TileDirection = TileDirection.InnerBottomRight;
        }

        if( GUI.Button( new Rect( 50, 600 + spacer, 45, 45 ), new GUIContent( "BL" ) ) ){
            TileDirection = TileDirection.BottomLeft;
        }

        if( GUI.Button( new Rect( 100, 600 + spacer, 150, 45 ), new GUIContent( "DOWN" ) ) ){
            TileDirection = TileDirection.Down;
        }

        if( GUI.Button( new Rect( 255, 600 + spacer, 45, 45 ), new GUIContent( "BR" ) ) ){
            TileDirection = TileDirection.BottomRight;
        }
    }

    private void DrawGrid(){
        int lineCount = Mathf.RoundToInt( ( GridExtent * 2 ) / GridCellSize );
        if( lineCount % 2 == 0 )
            lineCount++;
        int halfLineCount = ( lineCount / 2 );

        for( int i = 0; i < lineCount; i++ ){
            int intOffset = ( i - halfLineCount );

            float xCoord = ( intOffset * GridCellSize );
            float zCoord0 = ( halfLineCount * GridCellSize );
            float zCoord1 = ( -halfLineCount * GridCellSize );
            float gridCellOffset = ( GridCellSize / 2 );

            Vector3 p0 = new Vector3( xCoord + gridCellOffset, GridObject.transform.position.y + 0.55f, zCoord0 + gridCellOffset );
            Vector3 p1 = new Vector3( xCoord + gridCellOffset, GridObject.transform.position.y + 0.55f, zCoord1 + gridCellOffset );
            Handles.DrawAAPolyLine( 2f, p0, p1 );

            p0 = new Vector3( zCoord0 + gridCellOffset, GridObject.transform.position.y + 0.55f, xCoord + gridCellOffset );
            p1 = new Vector3( zCoord1 + gridCellOffset, GridObject.transform.position.y + 0.55f, xCoord + gridCellOffset );
            Handles.DrawAAPolyLine( 2f, p0, p1 );
        }
    }

    private int GetRoadMaskIndex(){
        if( TileDirection == TileDirection.TopLeft )
            return 0;

        if( TileDirection == TileDirection.Up )
            return 1;

        if( TileDirection == TileDirection.TopRight )
            return 2;

        if( TileDirection == TileDirection.Left )
            return 3;

        if( TileDirection == TileDirection.Center )
            return 4;

        if( TileDirection == TileDirection.Right )
            return 5;

        if( TileDirection == TileDirection.BottomLeft )
            return 6;

        if( TileDirection == TileDirection.Down )
            return 7;

        if( TileDirection == TileDirection.BottomRight )
            return 8;

        //--if no direction, return center tile
        return 4;
    }

    private float GetRoadMask( Texture2D atlas, int direction, float u, float v ){
        //--3x3 grid
        const int tilesetSize = 3;

        //--Tile size
        float subWidth = 1f / tilesetSize;
        float subHeight = 1f / tilesetSize;

        //--Row & Column
        int row = ( tilesetSize - 1 ) - direction / tilesetSize;
        int col = direction % tilesetSize;

        //--Pixel Position
        float x = ( col + u ) * subWidth;
        float y = ( row + v ) * subHeight;

        return atlas.GetPixelBilinear( x, y ).grayscale;
    }
    
    private void TryPlaceObject( RaycastHit placePoint ){
        if( SelectedMapObject == null )
            return;

        float x = Mathf.FloorToInt( placePoint.point.x );
        float z = Mathf.FloorToInt( placePoint.point.z );
        x += 0.5f;
        z += 0.5f;
        Vector3 pos = new( x, placePoint.point.y, z );

        GameObject SpawnedObject = (GameObject)PrefabUtility.InstantiatePrefab( SelectedMapObject, ActiveTerrain.transform );

        Undo.RegisterCreatedObjectUndo( SpawnedObject, "PlacedMapObject" );

        SpawnedObject.transform.position = pos;
        if( SelectedMapObject == _ledgeCollider_Side || SelectedMapObject == _ledgeCollider_Corner )
            SpawnedObject.GetComponentInChildren<LedgeHop>().Init( TileDirection );
    }

    private void TryRaiseTerrain( RaycastHit hit ){
        //--Setup some local variables
        float xBase = Mathf.FloorToInt( hit.point.x );
        float yBase = Mathf.FloorToInt( hit.point.z );
        int width = BrushSize;
        int height = BrushSize;
        var col = hit.collider as TerrainCollider;

        //--Check if raycast hit a terrain collider. if not, return
        if( col == null ){
            Debug.LogError( "Terrain Collider not found!" );
            return;
        }

        //--Get terrain data from collider
        TerrainData terrainData = col.terrainData;
        Transform terrain = hit.collider.GetComponent<Transform>();

        //--Add grid offset
        xBase += 0.5f;
        yBase += 0.5f;

        //--If brush size is even, offset by half a cell
        if( BrushSize % 2 == 0 ) xBase += 0.5f;
        if( BrushSize % 2 == 0 ) yBase += 0.5f;

        //--Convert world position to terrain positions
        xBase = Mathf.Clamp( Mathf.FloorToInt( ( ( xBase - terrain.position.x )  / terrainData.size.x ) * terrainData.heightmapResolution ), 0, terrainData.heightmapResolution - 1 );
        yBase = Mathf.Clamp( Mathf.FloorToInt( ( ( yBase - terrain.position.z )  / terrainData.size.z ) * terrainData.heightmapResolution ), 0, terrainData.heightmapResolution - 1 );
        width = Mathf.FloorToInt( ( width / terrainData.size.x ) * terrainData.heightmapResolution );
        height = Mathf.FloorToInt( ( height / terrainData.size.z ) * terrainData.heightmapResolution );
        
        //--Round positions to brush size?
        xBase -= width / 2;
        yBase -= height / 2;

        //--Setup float arrays for Get & Set Heights
        float[,] currentHeight = terrainData.GetHeights( (int)xBase, (int)yBase, width, height );
        float[,] newHeight = currentHeight;
        float baseHeightRaise = TileHeight / terrainData.size.y;

        //--Cache current height X & Y separately
        int heightCacheY = currentHeight.GetLength(0);
        int heightCacheX = currentHeight.GetLength(1);
        
        //--Use current height + desired slope starting point (from ground) to determine slopeBand
        int slopeBandY = Mathf.RoundToInt( heightCacheY * TileSlopeWidth );
        int slopeBandX = Mathf.RoundToInt( heightCacheX * TileSlopeWidth );

        //--Loop through the 2D array from our cached currentHeight, from GetHeights
        for( int y = 0; y < heightCacheY; y++ ){
            for( int x = 0; x < heightCacheX; x++){
                float slopeMultiplier = 1f;
                float t;
                
                //--Complicated 4D math to determine shape of brush, if brush isn't placing a center tile
                if( TileDirection != TileDirection.Center ){
                    //===================================================================================================================================
                    //===================================================================================================================================
                    //===================================================================================================================================
                    //--Up-------------------------------------------------------------------------------------
                    if( TileDirection == TileDirection.Up && y >= heightCacheY - slopeBandY ){
                        t = (float)(y - ( heightCacheY - slopeBandY ) ) / slopeBandY;
                        slopeMultiplier = 1f - ( t * TileSlopeStrength );
                    }
                    //--Down-------------------------------------------------------------------------------------
                    else if( TileDirection == TileDirection.Down && y < slopeBandY ){
                        t = 1f - (float)y / slopeBandY;
                        slopeMultiplier = 1f - ( t * TileSlopeStrength );
                    }
                    //--Left-------------------------------------------------------------------------------------
                    else if( TileDirection == TileDirection.Left && x < slopeBandX ){
                        t = 1f - (float)x / slopeBandX;
                        slopeMultiplier = 1f - ( t * TileSlopeStrength );
                    }
                    //--Right-------------------------------------------------------------------------------------
                    else if( TileDirection == TileDirection.Right && x >= heightCacheX - slopeBandX ){
                        t = (float)( x - ( heightCacheX - slopeBandX ) ) / slopeBandX;
                        slopeMultiplier = 1f - ( t * TileSlopeStrength );
                    }
                    //===================================================================================================================================
                    //===================================================================================================================================
                    //===================================================================================================================================
                    //--Top Left-------------------------------------------------------------------------------------
                    else if ( TileDirection == TileDirection.TopLeft ){
                        float tX = 0f;
                        if( x < slopeBandX )
                            tX = 1f - (float)x / slopeBandX;
                        
                        float tY = 0f;
                        if( y >= heightCacheY - slopeBandY )
                            tY = (float)( y - ( heightCacheY - slopeBandY ) ) / slopeBandY;

                        t = tX + tY;

                        slopeMultiplier = 1f - ( t * TileSlopeStrength );

                        slopeMultiplier = Mathf.Clamp01( slopeMultiplier );
                    }
                    //--Top Right-------------------------------------------------------------------------------------
                    else if ( TileDirection == TileDirection.TopRight )
                    {
                        
                        
                        float tX = 0f;
                        if( x >= heightCacheX - slopeBandX )
                            tX = (float)( x - ( heightCacheX - slopeBandX ) ) / slopeBandX;
                        
                        float tY = 0f;
                        if( y >= heightCacheY - slopeBandY )
                            tY = (float)( y - ( heightCacheY - slopeBandY ) ) / slopeBandY;

                        t = tX + tY;

                        slopeMultiplier = 1f - ( t * TileSlopeStrength );

                        slopeMultiplier = Mathf.Clamp01( slopeMultiplier );
                    }
                    //--Bottom Left-------------------------------------------------------------------------------------
                    else if ( TileDirection == TileDirection.BottomLeft )
                    {
                        float tX = 0f;
                        if( x < slopeBandX )
                            tX = 1f - ( (float)x / slopeBandX );
                        
                        float tY = 0f;
                        if( y < slopeBandY )
                            tY = 1f - ( (float)y / slopeBandY );

                        t = tX + tY;

                        slopeMultiplier = 1f - ( t * TileSlopeStrength );

                        slopeMultiplier = Mathf.Clamp01( slopeMultiplier );
                    }
                    //--Bottom Right-------------------------------------------------------------------------------------
                    else if ( TileDirection == TileDirection.BottomRight )
                    {
                        float tX = 0f;
                        if( x >= heightCacheX - slopeBandX )
                            tX = (float)( x - ( heightCacheX - slopeBandX ) ) / slopeBandX;
                        
                        float tY = 0f;
                        if( y < slopeBandY )
                            tY = 1f - ( (float)y / slopeBandY );

                        t = tX + tY;

                        slopeMultiplier = 1f - ( t * TileSlopeStrength );

                        slopeMultiplier = Mathf.Clamp01( slopeMultiplier );
                    }
                    //===================================================================================================================================
                    //===================================================================================================================================
                    //===================================================================================================================================
                    //--Inner Top Left-------------------------------------------------------------------------------------
                    else if ( TileDirection == TileDirection.InnerBottomRight && x < slopeBandX && y >= heightCacheY - slopeBandY ){
                        float tX = 1f - (float)x / slopeBandX;
                        float tY = (float)( y - ( heightCacheY - slopeBandY ) ) / slopeBandY;

                        t = tX * tY;
                        t = Mathf.Clamp01( t );

                        slopeMultiplier = 1f - ( t * TileSlopeStrength );

                        slopeMultiplier = Mathf.Clamp01( slopeMultiplier );
                    }
                    //--Inner Top Right-------------------------------------------------------------------------------------
                    else if ( TileDirection == TileDirection.InnerBottomLeft && x >= heightCacheX - slopeBandX && y >= heightCacheY - slopeBandY )
                    {
                        float tX = (float)( x - ( heightCacheX - slopeBandX ) ) / slopeBandX;
                        float tY = (float)( y - ( heightCacheY - slopeBandY ) ) / slopeBandY;
                        t = tX * tY;
                        t = Mathf.Clamp01( t );

                        slopeMultiplier = 1f - (t * TileSlopeStrength);
                    }
                    //--Inner Bottom Left-------------------------------------------------------------------------------------
                    else if ( TileDirection == TileDirection.InnerTopRight && x < slopeBandX && y < slopeBandY )
                    {
                        float tX = 1f - ( (float)x / slopeBandX );
                        float tY = 1f - ( (float)y / slopeBandY );
                        t = tX * tY;
                        t *= 0.7071f;
                        t = Mathf.Clamp01( t );

                        float stepCount = 4f;
                        t = Mathf.Floor( t * stepCount ) / stepCount;

                        slopeMultiplier = 1f - ( t * TileSlopeStrength );
                    }
                    //--Inner Bottom Right-------------------------------------------------------------------------------------
                    else if ( TileDirection == TileDirection.InnerTopLeft && x >= heightCacheX - slopeBandX && y < slopeBandY )
                    {
                        float tX = (float)( x - ( heightCacheX - slopeBandX ) ) / slopeBandX;
                        float tY = 1f - ( (float)y / slopeBandY );
                        t = tX * tY;
                        t = Mathf.Clamp01( t );

                        float stepCount = 4f;
                        t = Mathf.Floor( t * stepCount ) / stepCount;

                        slopeMultiplier = 1f - ( t * TileSlopeStrength );
                    }
                }

                newHeight[y, x] = currentHeight[y, x] + baseHeightRaise * slopeMultiplier;
            }
        }

        xBase = Mathf.FloorToInt( xBase );
        yBase = Mathf.FloorToInt( yBase );
        terrainData.SetHeights( (int)xBase, (int)yBase, newHeight );
    }

    private void TryLowerTerrain( RaycastHit placePoint ){
        int xBase = Mathf.FloorToInt( placePoint.point.x );
        int yBase = Mathf.FloorToInt( placePoint.point.z );
        int width = BrushSize;
        int height = BrushSize;
        var col = placePoint.collider as TerrainCollider;

        if( col == null ){
            Debug.LogError( "Terrain Collider not found!" );
            return;
        }

        TerrainData terrainData = col.terrainData;
        Transform terrain = placePoint.collider.GetComponent<Transform>();

        xBase = Mathf.Clamp( Mathf.FloorToInt( ( ( xBase - terrain.position.x )  / terrainData.size.x ) * terrainData.heightmapResolution ), 0, terrainData.heightmapResolution - 1 );
        yBase = Mathf.Clamp( Mathf.FloorToInt( ( ( yBase - terrain.position.z )  / terrainData.size.z ) * terrainData.heightmapResolution ), 0, terrainData.heightmapResolution - 1 );
        width = Mathf.FloorToInt( ( width / terrainData.size.x ) * terrainData.heightmapResolution );
        height = Mathf.FloorToInt( ( height / terrainData.size.z ) * terrainData.heightmapResolution );
        
        
        float[,] currentHeight = terrainData.GetHeights( xBase, yBase, width, height );
        float[,] newHeight = currentHeight;
        float highestPoint = float.MinValue;

        for( int y = 0; y < currentHeight.GetLength( 0 ); y++ )
            for( int x = 0; x < currentHeight.GetLength( 1 ); x++){
                if( currentHeight[y, x] > highestPoint )
                    highestPoint = currentHeight[y, x];
        }

        float targetHeight = highestPoint - ( TileHeight / terrainData.size.y );

        for( int y = 0; y < currentHeight.GetLength( 0 ); y++ )
            for( int x = 0; x < currentHeight.GetLength( 1 ); x++){
                newHeight[y, x] = targetHeight;
            }   

        terrainData.SetHeights( xBase, yBase, newHeight );
    }

    private void TryPaintRoad( RaycastHit hit ){
        //--Hit points need to be floored to properly align to grid!
        int hitX = Mathf.FloorToInt( hit.point.x );
        int hitZ = Mathf.FloorToInt( hit.point.z );
        var col = hit.collider as TerrainCollider;

        if( col == null ){
            Debug.LogError( "Terrain Collider not found!" );
            return;
        }

        TerrainData terrainData = col.terrainData;
        Transform terrain = hit.collider.GetComponent<Transform>();

        //--Convert world position to splatmap position
        int posX = (int)((( hitX - terrain.transform.position.x ) / terrainData.size.x ) * terrainData.alphamapWidth );
        int posY = (int)((( hitZ - terrain.transform.position.z ) / terrainData.size.z ) * terrainData.alphamapHeight );
        int width = (int)(( BrushSize / terrainData.size.x ) * terrainData.alphamapWidth );
        int height = (int)(( BrushSize / terrainData.size.z ) * terrainData.alphamapHeight );

        //--Clamp clamp clamp
        posX = Mathf.Clamp( posX, 0, terrainData.alphamapWidth - BrushSize );
        posY = Mathf.Clamp( posY, 0, terrainData.alphamapHeight - BrushSize );
        
        Debug.Log( $"x:{posX}, y:{posY}" );

        float[,,] alphaMap = terrainData.GetAlphamaps( posX, posY, width, height );

        for( int y = 0; y < alphaMap.GetLength( 0 ); y++ )
            for( int x = 0; x < alphaMap.GetLength( 1 ); x++){      
                if( SelectedRoadMask == null){
                    Debug.LogError( "Road Mask texture not found!" );
                    return;
                }

                //--Mask "Blend"
                float u = (float)x / ( alphaMap.GetLength( 1 ) - 1 );
                float v = (float)y / ( alphaMap.GetLength( 0 ) - 1 );

                float scale = 1.25f;
                u = (u - 0.5f) / scale + 0.55f;
                v = (v - 0.5f) / scale + 0.55f;

                u = Mathf.Clamp01(u);
                v = Mathf.Clamp01(v);
                
                float maskValue = GetRoadMask( SelectedRoadMask, GetRoadMaskIndex(), u, v );

                //--Get pixels to remove from other layers
                float remainder = 1f - maskValue;

                //--Remove other layers
                for( int i = 0; i < terrainData.alphamapLayers; i++ ){
                    if ( i == PaintTextureLayer )
                        continue;

                    alphaMap[y, x, i] *= remainder;
                }

                alphaMap[y, x, PaintTextureLayer] = maskValue;
            }

        Debug.Log( $"Painting layer: {terrainData.terrainLayers[PaintTextureLayer].diffuseTexture.name}" );
        terrainData.SetAlphamaps( posX, posY, alphaMap );
    }

    private void ApplyHeightOffset( GameObject spawnedObject ){
        Vector3 hOff = spawnedObject.transform.position;
        hOff.y = hOff.y + HeightOffset;
        spawnedObject.transform.position = hOff;
    }

    private void RotatePlacedObject( GameObject spawnedObject ){
        spawnedObject.transform.rotation = _selectedMapObjectRotation;
    }

    private void SnapSelectedThings(){
        foreach(UnityEngine.GameObject gobj in Selection.gameObjects){
            Undo.RecordObject(gobj.transform, UNDO_STR_SNAP);
            gobj.transform.position = gobj.transform.position.Round(GridCellSize);
        }
    }
    private void SnapSelectedThingsXZ(){
        foreach(UnityEngine.GameObject gobj in Selection.gameObjects){
            Undo.RecordObject(gobj.transform, UNDO_STR_SNAP);
            gobj.transform.position = gobj.transform.position.RoundXZ(GridCellSize);
        }
    }

    private void SnapPlacedThings( GameObject gobj ){
        Undo.RecordObject( gobj.transform, UNDO_STR_SNAP );
        Vector3 oldPosition = gobj.transform.position;
        Vector3 snappedPosition = oldPosition.Round( GridCellSize );
        snappedPosition.y = oldPosition.y;
        gobj.transform.position = snappedPosition.RoundY();
    }
}
