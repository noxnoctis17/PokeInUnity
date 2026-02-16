using System.Collections.Generic;
using UnityEngine;

public class StatusIconAtlas : MonoBehaviour
{
    [Header( "Poison" )]
    [SerializeField] private Sprite _psn;
    [SerializeField] private Color _psnColor;
    [SerializeField] private GameObject _psnVFX;

    [Header( "Toxic" )]
    [SerializeField] private Sprite _tox;
    [SerializeField] private Color _toxColor;
    [SerializeField] private GameObject _toxVFX;

    [Header( "Burn" )]
    [SerializeField] private Sprite _brn;
    [SerializeField] private Color _brnColor;
    [SerializeField] private GameObject _brnVFX;

     [Header( "Frostbite" )]
    [SerializeField] private Sprite _fbt;
    [SerializeField] private Color _fbtColor;
    [SerializeField] private GameObject _fbtVFX;   

    [Header( "Paralysis" )]
    [SerializeField] private Sprite _par;
    [SerializeField] private Color _parColor;
    [SerializeField] private GameObject _parVFX;

    [Header( "Sleep" )]
    [SerializeField] private Sprite _slp;
    [SerializeField] private Color _slpColor;
    [SerializeField] private GameObject _slpVFX;

    [Header( "Faint" )]
    [SerializeField] private Sprite _fnt;
    [SerializeField] private Color _fntColor;
    [SerializeField] private GameObject _fntVFX;

    public static Dictionary<SevereConditionID, StatusObject> StatusIcons;

    private void OnEnable(){
        InitializeDictionary();
    }

    private void InitializeDictionary(){
        StatusIcons = new()
        {
            { SevereConditionID.PSN, new( _psn, _psnColor, _psnVFX ) },
            { SevereConditionID.TOX, new( _tox, _toxColor, _toxVFX ) },
            { SevereConditionID.BRN, new( _brn, _brnColor, _brnVFX ) },
            { SevereConditionID.FBT, new( _fbt, _fbtColor, _fbtVFX ) },
            { SevereConditionID.PAR, new( _par, _parColor, _parVFX ) },
            { SevereConditionID.SLP, new( _slp, _slpColor, _slpVFX ) },
            { SevereConditionID.FNT, new( _fnt, _fntColor, _fntVFX ) },

        };
    }
}

public class StatusObject
{
    public Sprite Icon { get; private set; }
    public Color Color { get; private set; }
    public GameObject VFX { get; private set; }

    public StatusObject( Sprite icon, Color color, GameObject vfx )
    {
        Icon = icon;
        Color = color;
        VFX = vfx;
    }
}
