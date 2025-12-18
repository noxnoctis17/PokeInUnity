using UnityEngine;
using System;
using UnityEditor;

[System.Serializable]
public class GridClass<TGridObject>
{
    [SerializeField] private bool _showDebug;
    public event EventHandler<OnGridValueChangedEventArgs> OnGridObjectChanged;
    public class OnGridValueChangedEventArgs : EventArgs
    {
        public int x, z;
    }

    private int _width;
    private int _height;
    private float _cellSize;
    public float CellSize => _cellSize;
    public int MaxSize { get => _width * _height; }
    private Vector3 _originPosition;
    private TGridObject[,] _gridArray;
    private TextMesh[,] _debugTextArray;

    public GridClass(int width, int height, float cellSize, Vector3 originPosition, Func<GridClass<TGridObject>, int, int, TGridObject> createGridObject){
        _width = width;
        _height = height;
        _cellSize = cellSize;
        _originPosition = originPosition;

        _gridArray = new TGridObject[_width, _height];

        for(int x = 0; x < _gridArray.GetLength(0); x++){
            for(int z = 0; z < _gridArray.GetLength(1); z++){
                _gridArray[x, z] = createGridObject(this, x, z);
            }
        }

        _showDebug = true;
        if(_showDebug){
            // _debugTextArray = new TextMesh[_width, _height];

            for(int x = 0; x < _gridArray.GetLength(0); x++){
                for(int z = 0; z < _gridArray.GetLength(1); z++){
                    Debug.DrawLine(GetWorldPosition(x, z), GetWorldPosition(x, z + 1), Color.cyan, 100f);
                    Debug.DrawLine(GetWorldPosition(x, z), GetWorldPosition(x + 1, z), Color.cyan, 100f);
                    // Handles.color = Color.cyan;
                    // Handles.DrawAAPolyLine( 2f, GetWorldPosition(x, z), GetWorldPosition(x, z + 1) );
                    // Handles.DrawAAPolyLine( 2f, GetWorldPosition(x, z), GetWorldPosition(x + 1, z) );
                }
            }
            // Handles.DrawAAPolyLine( 2f, GetWorldPosition(0, _height), GetWorldPosition(_width, _height) );
            // Handles.DrawAAPolyLine( 2f, GetWorldPosition(_width, 0), GetWorldPosition(_width, _height) );
            Debug.DrawLine(GetWorldPosition(0, _height), GetWorldPosition(_width, _height), Color.cyan, 100f);
            Debug.DrawLine(GetWorldPosition(_width, 0), GetWorldPosition(_width, _height), Color.cyan, 100f);
        }
    }

    public void TriggerGridObjectChanged(int x, int z){
        if(OnGridObjectChanged != null) OnGridObjectChanged(this, new OnGridValueChangedEventArgs { x = x, z = z} );
    }

    public Vector3 GetWorldPosition(int x, int z){
        return new Vector3(x, 0, z) * _cellSize + _originPosition;
    }

    public void GetXY(Vector3 worldPosition, out int x, out int z){
        x = Mathf.FloorToInt((worldPosition - _originPosition).x / _cellSize);
        z = Mathf.FloorToInt((worldPosition - _originPosition).z / _cellSize);
    }

    public int GetWidth(){
        return _width;
    }

    public int GetHeight(){
        return _height;
    }

    public void SetGridObject(int x, int z, TGridObject value){
        if(x >= 0 && z >= 0 && x < _width && z < _height){
            _gridArray[x, z] = value;
            _debugTextArray[x, z].text = _gridArray[x, z].ToString();
        }
    }

    public void SetGridObject(Vector3 worldPosition, TGridObject value){
        int x, z;
        GetXY(worldPosition, out x, out z);
        SetGridObject(x, z, value);
    }

    public TGridObject GetGridObject(int x, int z){
        if(x >= 0 && z >= 0 && x < _width && z < _height){
            return _gridArray[x, z];
        } else {
            return default(TGridObject);
        }
    }

    public TGridObject GetGridObject(Vector3 worldPosition){
        int x, z;
        GetXY(worldPosition, out x, out z);
        return GetGridObject(x, z);
    }

}
