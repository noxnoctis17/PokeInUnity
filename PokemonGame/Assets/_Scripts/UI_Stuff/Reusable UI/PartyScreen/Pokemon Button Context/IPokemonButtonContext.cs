using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPokemonButtonContext
{
    public void Init( PartyDisplay partyScreen, PokemonButton button, IPartyScreen parentMenu );
    public void ContextSubmit();
    public void ContextSelected();
    public void ContextDeSelected();
    public void ContextCancel();
    public void CloseContextMenu();
}
