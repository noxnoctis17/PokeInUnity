using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneGrid : MonoBehaviour
{
    private GridClass<Vector3> _grid;
    public GridClass<Vector3> Grid => _grid;
    [SerializeField] private int _gridWidth;
    [SerializeField] private int _gridHeight;

    private void Start(){
        _grid = new GridClass<Vector3>(_gridWidth, _gridHeight, 10f, new Vector3(0, 0, 0), (GridClass<Vector3> grid, int x, int z) => new Vector3());
    }

    public Vector3 ClickedLocation(int x, int z){
        Vector3 newLocation = new Vector3(x, 0, z);
        Debug.Log(newLocation);
        return newLocation;
    }

}
