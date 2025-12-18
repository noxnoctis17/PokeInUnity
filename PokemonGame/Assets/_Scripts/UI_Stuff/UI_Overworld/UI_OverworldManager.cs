using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UI_OverworldManager : MonoBehaviour
{
    public static UI_OverworldManager Instance { get; private set;}
    [SerializeField] private TextMeshProUGUI _timeText;
    public TextMeshProUGUI TimeText => _timeText;

    private void OnEnable(){
        Instance = this;
    }

}
