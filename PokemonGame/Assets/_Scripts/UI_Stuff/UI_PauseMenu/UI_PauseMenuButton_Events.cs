using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

public class UI_PauseMenuButton_Events : MonoBehaviour
{
    public Action<Button> OnButtonSelected;
    public Action<Button> OnButtonDeselected;
    public Action<Button> OnButtonSubmitted;
}
