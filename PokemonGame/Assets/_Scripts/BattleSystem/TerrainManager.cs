using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainManager : MonoBehaviour
{
    public static TerrainManager Instance;
    [SerializeField] private GameObject _terrain;
    [SerializeField] private Material _terrainMaterial;
    [SerializeField] private Color32 _grassyColor;
    [SerializeField] private Color32 _psychicColor;

    private void Start()
    {
        Instance = this;
    }

    public void DisplayTerrain( TerrainID id )
    {
        switch( id )
        {
            case TerrainID.None:
                _terrain.SetActive( false );
            break;

            case TerrainID.Grassy:
                _terrainMaterial.color = _grassyColor;
                _terrain.SetActive( true );
            break;

            case TerrainID.Psychic:
                _terrainMaterial.color = _psychicColor;
                _terrain.SetActive( true );
            break;
        }
    }

}
