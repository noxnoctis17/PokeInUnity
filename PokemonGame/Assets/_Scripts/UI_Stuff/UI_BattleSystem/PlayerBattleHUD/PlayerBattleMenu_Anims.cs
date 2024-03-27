using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class PlayerBattleMenu_Anims : MonoBehaviour
{
    private Transform _battleMenuParent;
    private PlayerBattleMenu_AnimEvents _animEvents;
    [SerializeField] private Transform _fightButton, _pkmnButton, _bagButton, _runButton;
    [SerializeField] private RectTransform _fightText, _pkmnText, _bagText, _runText;

    private void OnEnable(){
        _battleMenuParent = transform;
        _animEvents = GetComponent<PlayerBattleMenu_AnimEvents>();

        _animEvents.OnBattleStart           += OnBattleStart;
        _animEvents.OnSwapActiveButton      += OnSwapActiveButton;
        _animEvents.OnNewActiveButton       += OnNewActiveButton;
        _animEvents.OnHideMenu              += OnHideMenu;
        _animEvents.OnRestoreMenu           += OnRestoreMenu;
    }

    private void OnDisable(){
        _animEvents.OnBattleStart           -= OnBattleStart;
        _animEvents.OnSwapActiveButton      -= OnSwapActiveButton;
        _animEvents.OnNewActiveButton       -= OnNewActiveButton;
        _animEvents.OnHideMenu              -= OnHideMenu;
        _animEvents.OnRestoreMenu           -= OnRestoreMenu;
    }

    private void OnBattleStart( Transform transform ){
        // _battleMenuParent needs to slide up
        // all cards need to be single file, from fight -> run
        // after, they'll fan out to the left. unsure if one at a time or all at the same time
        // maybe just offset times by a tiny amount, so neither at the same time nor one at a time
    }

    private void OnSwapActiveButton( Transform transform ){
        // when a card becomes the currently active button, it should animate into place
        // all cards under it should rotate their 15 degrees. the previous top card, or bottom card
        // depending on the direction the menu is being scrolled, should do SOME kind of animation
        // into place
    }

    private void OnNewActiveButton( Transform transform ){
        // the currently active button should be larger than the rest of the cards in the stack, as if
        // it's being held and looked at in a separate hand. this will animate it into place
        // i think i will raise this event in Leftcrease and Rightcrease, where we get the activebutton
    }

    private void OnHideMenu( Transform transform ){
        //--self explanatory
    }

    private void OnRestoreMenu( Transform transform ){
        //--self explanatory
    }

}
