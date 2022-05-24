using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleDialogueBox : MonoBehaviour
{
    [SerializeField] RectTransform _dialogueBoxParent;
    [SerializeField] TextMeshProUGUI _dialogueText;

    private void AnimateDialogueBox()
    {
        LeanTween.moveLocalX(gameObject, -370, 0.2f);
        StartCoroutine(BounceIn());

    }

    public IEnumerator TypeDialogue(string dialogue)
    {
        AnimateDialogueBox();
        _dialogueText.text = "";
        foreach(var letter in dialogue.ToCharArray())
        {
            _dialogueText.text += letter;
            yield return new WaitForSeconds(0.5f/60);
        }

        yield return new WaitForSeconds(1f);
        
    }


    //---------------------------ANIMATION SWIPE IN-------------------------------

    private IEnumerator BounceIn()
    {
        yield return new WaitForSeconds(0.2f);
        LeanTween.moveLocalX(gameObject, -360, 0.2f);
        StartCoroutine(HideBoxDelay());

    }

    private IEnumerator HideBoxDelay()
    {
        yield return new WaitForSeconds(1f);
        LeanTween.moveLocalX(gameObject, -1600f, 0.2f);
        yield return new WaitForSeconds(1f);

    }
}
