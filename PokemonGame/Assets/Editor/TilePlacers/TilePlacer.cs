using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class TilePlacer : EditorWindow
{
    const string UNDO_STR_SNAP  = "snap objects";

    [MenuItem("Tools/TilePlacer")]
    public static void OpenTheThing() => GetWindow<TilePlacer>("Tile Placer");

//-----------------------------------------------------------------------

    public GameObject GridObject;
    private GameObject[] MapObjects;
    public GameObject SelectedMapObject;
    public Material PreviewMaterial;
    public int GridCellSize;
    public float GridExtent;
    private RaycastHit placePoint;
    private Quaternion _selectedMapObjectRotation;
    public int HeightOffset;

    private SerializedObject _sobj;
    private SerializedProperty _propGridObject;
    private SerializedProperty _propSelectedMapObject;
    private SerializedProperty _propGridCellSize;
    private SerializedProperty _propGridExtent;
    private SerializedProperty _propHeightOffset;
    private SerializedProperty _propPreviewMaterial;

    private void OnEnable(){
        //--Get Grid Object and Set Material badly
        GridObject = FindObjectOfType<GridObject>().gameObject;
        PreviewMaterial = GridObject.GetComponent<GridObject>().PreviewMaterial;

        //--Serialize This and its Properties
        _sobj = new SerializedObject(this);
        _propGridCellSize = _sobj.FindProperty( "GridCellSize" );
        _propGridExtent = _sobj.FindProperty( "GridExtent" );
        _propGridObject = _sobj.FindProperty( "GridObject" );
        _propSelectedMapObject = _sobj.FindProperty( "SelectedMapObject" );
        _propPreviewMaterial = _sobj.FindProperty( "PreviewMaterial" );
        _propHeightOffset = _sobj.FindProperty( "HeightOffset" );

        //--Load Editor Prefs
        GridCellSize = EditorPrefs.GetInt( "MAP_OBJECT_PLACER_TOOL_GridCellSize", 8 );
        GridExtent = EditorPrefs.GetFloat( "MAP_OBJECT_PLACER_TOOL_GridExtent", 200 );

        //--
        SceneView.duringSceneGui += DuringSceneGUI;
        Selection.selectionChanged += Repaint;

        //--Load Map Object Prefabs
        string[] guids = AssetDatabase.FindAssets( "t:prefab", new[] { "Assets/_Game/_Prefabs/MapObjects" } );
        IEnumerable<string> paths = guids.Select( AssetDatabase.GUIDToAssetPath );
        MapObjects = paths.Select( AssetDatabase.LoadAssetAtPath<GameObject> ).ToArray();
        SelectedMapObject = MapObjects[0];
        _selectedMapObjectRotation = Quaternion.LookRotation( placePoint.normal );
        HeightOffset = 0;

    }

    private void OnDisable(){
        Selection.selectionChanged -= Repaint;
        SceneView.duringSceneGui -= DuringSceneGUI;

        //--Save Editor Prefs
        EditorPrefs.SetInt( "MAP_OBJECT_PLACER_TOOL_GridCellSize", GridCellSize );
        EditorPrefs.SetInt( "MAP_OBJECT_PLACER_TOOL_GridExtent", (int)GridExtent );
    }

    private void Start(){
        Vector3 gridObjPos = GridObject.transform.position;
        gridObjPos.y = GridObject.transform.position.y - 0.55f;
    }

    private void OnGUI(){
        _sobj.Update();
        if(Event.current.type == EventType.MouseDown && Event.current.button == 0 ){
            GUI.FocusControl( null );
            Repaint();
        }

        GUILayout.Label( "Grid Cellsize" );
        EditorGUILayout.PropertyField( _propGridExtent );
        EditorGUILayout.PropertyField( _propGridCellSize );
        EditorGUILayout.PropertyField( _propHeightOffset );
        
        GUILayout.Space(10f);

        GUILayout.Label( "Map Object Palette" );
        EditorGUILayout.PropertyField( _propPreviewMaterial );
        EditorGUILayout.PropertyField( _propGridObject );
        GUILayout.Space(5f);
        EditorGUILayout.PropertyField( _propSelectedMapObject );

        GUILayout.Space(10f);

        GUILayout.Label( "Snap Selected to World Grid" );
        using(new EditorGUI.DisabledScope( Selection.gameObjects.Length == 0 ) )
        if(GUILayout.Button("Snap Selection (Y: 0)")){
            SnapSelectedThings();
        }

        using(new EditorGUI.DisabledScope( Selection.gameObjects.Length == 0 ) )
        if(GUILayout.Button("Snap Selection (XZ)")){
            SnapSelectedThingsXZ();
        }

        using(new EditorGUI.DisabledScope( Selection.gameObjects.Length == 0 ) )
        if(GUILayout.Button("Rotate Selection +90")){
            foreach( GameObject selected in Selection.gameObjects ){
                Quaternion rot = selected.transform.rotation;
                rot = rot * Quaternion.Euler( 0f, 90f , 0f );
                selected.transform.rotation = rot;
            }
        }

        if( GUILayout.Button( "Reset Height Offset" ) ){
            HeightOffset = 0;
        }
        
        if( _sobj.ApplyModifiedProperties() ){
            SceneView.RepaintAll();
        }
    }

    private void DuringSceneGUI( SceneView sceneView ){
        Handles.zTest = CompareFunction.LessEqual;

        //--------------Prefab Select Buttons--------------------

        Handles.BeginGUI();

        Rect rect = new Rect( 4, 4, 200, 20 );

        foreach( GameObject mapObject in MapObjects ){
            if( GUI.Button(rect, mapObject.name) ){
                SelectedMapObject = mapObject;
                SelectedMapObject.transform.rotation = _selectedMapObjectRotation;
                Repaint();
            }

            rect.y += rect.height + 2;
        }

        Handles.EndGUI();

        //----------------------Grid----------------------------

        DrawGrid();

        //----------------------Cursor--------------------------

        if( Event.current.type == EventType.MouseMove ){
            sceneView.Repaint();
        }

        Ray ray = HandleUtility.GUIPointToWorldRay( Event.current.mousePosition );

        if( Physics.Raycast( ray, out RaycastHit hit ) ){
            placePoint = hit;
            Handles.color = Color.red;
            Handles.DrawAAPolyLine( 5f, hit.point, hit.point + hit.normal );
            Handles.DrawWireDisc( hit.point, hit.normal, ( GridCellSize / 2 ) );

            //--Draw SelectedMapObject Mesh in Place
            MeshFilter[] meshFilters = SelectedMapObject.GetComponentsInChildren<MeshFilter>();
            foreach(MeshFilter meshFilter in meshFilters){
                Mesh mesh = meshFilter.sharedMesh;
                // Material previewMat = meshFilter.GetComponent<MeshRenderer>().sharedMaterial;
                Material previewMat = PreviewMaterial;
                previewMat.SetPass( 0 );
                Vector3 hOffset = placePoint.point;
                hOffset.y = hOffset.y + HeightOffset;
                Graphics.DrawMeshNow( mesh, hOffset, _selectedMapObjectRotation * Quaternion.Euler( -90f, 0f, 0f ) );
            }
        }

        //--------------Place Map Object Functions---------------

            //--Alpha 1 to place object
        if( Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Alpha1 ){
            TryPlaceObject(placePoint);
        }

            //--Alpha 3 to rotate by +90 degrees
        if( Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Alpha3 ){
            _selectedMapObjectRotation = _selectedMapObjectRotation * Quaternion.Euler( 0f, 90f , 0f );
        }

            //--Alpha 4 or 5 to change height up or down
        if( Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Alpha4 ){
            HeightOffset = HeightOffset + 1;
            Repaint();
        }

        if( Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Alpha5 ){
            HeightOffset = HeightOffset - 1;
            Repaint();
        }

           
    }

    private void DrawGrid(){
        int lineCount = Mathf.RoundToInt( ( GridExtent * 2 ) / GridCellSize );
        if( lineCount % 2 == 0 )
            lineCount++;
        int halfLineCount = ( lineCount / 2 );

        for( int i = 0; i < lineCount; i++){
            int intOffset = ( i - halfLineCount );

            float xCoord = ( intOffset * GridCellSize );
            float zCoord0 = ( halfLineCount * GridCellSize );
            float zCoord1 = ( -halfLineCount * GridCellSize );
            float gridCellOffset = ( GridCellSize / 2 );

            Vector3 p0 = new Vector3( xCoord + gridCellOffset, GridObject.transform.position.y + 0.55f, zCoord0 + gridCellOffset );
            Vector3 p1 = new Vector3( xCoord + gridCellOffset, GridObject.transform.position.y + 0.55f, zCoord1 + gridCellOffset );
            Handles.DrawAAPolyLine(2f, p0, p1 );

            p0 = new Vector3( zCoord0 + gridCellOffset, GridObject.transform.position.y + 0.55f, xCoord + gridCellOffset );
            p1 = new Vector3( zCoord1 + gridCellOffset, GridObject.transform.position.y + 0.55f, xCoord + gridCellOffset );
            Handles.DrawAAPolyLine( 2f, p0, p1 );
        }
    }

    private void TryPlaceObject(RaycastHit placePoint){
        if(SelectedMapObject == null)
            return;

        GameObject SpawnedObject = (GameObject)PrefabUtility.InstantiatePrefab( SelectedMapObject );

        Undo.RegisterCreatedObjectUndo( SpawnedObject, "PlacedMapObject" );

        SpawnedObject.transform.position = placePoint.point;

        RotatePlacedObject(SpawnedObject);
        ApplyHeightOffset(SpawnedObject);
        SnapPlacedThings(SpawnedObject);
    }

    private void ApplyHeightOffset(GameObject spawnedObject){
        Vector3 hOff = spawnedObject.transform.position;
        hOff.y = hOff.y + HeightOffset;
        spawnedObject.transform.position = hOff;
    }

    private void RotatePlacedObject(GameObject spawnedObject){
        spawnedObject.transform.rotation = _selectedMapObjectRotation;
    }

    private void SnapSelectedThings(){
        foreach(GameObject gobj in Selection.gameObjects){
            Undo.RecordObject(gobj.transform, UNDO_STR_SNAP);
            gobj.transform.position = gobj.transform.position.Round( GridCellSize );
        }
    }
    private void SnapSelectedThingsXZ(){
        foreach(GameObject gobj in Selection.gameObjects){
            Undo.RecordObject(gobj.transform, UNDO_STR_SNAP);
            gobj.transform.position = gobj.transform.position.RoundXZ( GridCellSize );
        }
    }

    private void SnapPlacedThings(GameObject gobj){
        Undo.RecordObject(gobj.transform, UNDO_STR_SNAP);
        Vector3 oldPosition = gobj.transform.position;
        Vector3 snappedPosition = oldPosition.Round( GridCellSize );
        snappedPosition.y = oldPosition.y;
        gobj.transform.position = snappedPosition.RoundY();
    }
}
