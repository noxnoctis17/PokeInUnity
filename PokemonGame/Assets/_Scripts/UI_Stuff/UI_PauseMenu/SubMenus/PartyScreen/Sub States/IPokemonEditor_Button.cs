using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public interface IPokemonEditor_Button : ISelectHandler, ISubmitHandler, IDeselectHandler, ICancelHandler
{
    public Button ThisButton { get; set; }
    public void Setup( PartyScreen_PokemonEditor editor, Pokemon pokemon );
}
