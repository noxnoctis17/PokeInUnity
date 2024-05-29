using System.Collections.Generic;
using UnityEngine;

public class StatusIconAtlas : MonoBehaviour
{
    [SerializeField] private Sprite _psn;
    [SerializeField] private Sprite _tox;
    [SerializeField] private Sprite _brn;
    [SerializeField] private Sprite _par;
    [SerializeField] private Sprite _slp;
    [SerializeField] private Sprite _fbt;
    [SerializeField] private Sprite _fnt;
    [SerializeField] private Color _psnColor;
    [SerializeField] private Color _toxColor;
    [SerializeField] private Color _brnColor;
    [SerializeField] private Color _parColor;
    [SerializeField] private Color _slpColor;
    [SerializeField] private Color _fbtColor;
    [SerializeField] private Color _fntColor;
    public static Dictionary<ConditionID, ( Sprite icon, Color color )> StatusIcons;

    private void OnEnable(){
        InitializeDictionary();
    }

    private void InitializeDictionary(){
        StatusIcons = new()
        {
            { ConditionID.PSN, ( _psn, _psnColor ) },
            { ConditionID.TOX, ( _tox, _toxColor ) },
            { ConditionID.BRN, ( _brn, _brnColor ) },
            { ConditionID.PAR, ( _par, _parColor ) },
            { ConditionID.SLP, ( _slp, _slpColor ) },
            { ConditionID.FBT, ( _fbt, _fbtColor ) },
            { ConditionID.FNT, ( _fnt, _fntColor ) },

        };
    }
}
