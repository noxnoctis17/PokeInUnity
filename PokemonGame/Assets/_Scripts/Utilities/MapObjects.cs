using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MapObject_PrimaryType { Building, Foliage, Decoration, Prop, Traversal };
public enum MapObject_SecondaryType { None, Tree, Bush, Flower, Fence, Lamp, Stairs };
public class MapObjects : MonoBehaviour
{
    [SerializeField] private MapObject_PrimaryType _primaryType;
    [SerializeField] private MapObject_SecondaryType _secondaryType;

    public MapObject_PrimaryType PrimaryType => _primaryType;
    public MapObject_SecondaryType SecondaryType => _secondaryType;
}
