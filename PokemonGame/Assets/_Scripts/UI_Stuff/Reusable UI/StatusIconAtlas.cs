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
    public static Dictionary<ConditionID, Sprite> StatusIcons;

    private void OnEnable(){
        InitializeDictionary();
    }

    private void InitializeDictionary(){
        StatusIcons = new()
        {
            { ConditionID.PSN, _psn },
            { ConditionID.TOX, _tox },
            { ConditionID.BRN, _brn },
            { ConditionID.PAR, _par },
            { ConditionID.SLP, _slp },
            { ConditionID.FBT, _fbt },
            { ConditionID.FNT, _fnt },

        };
    }
}
