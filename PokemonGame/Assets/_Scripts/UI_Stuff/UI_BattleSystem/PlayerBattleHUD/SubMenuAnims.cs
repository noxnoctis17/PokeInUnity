using System.Collections;
using UnityEngine;

public class SubMenuAnims : MonoBehaviour
{
    [SerializeField] private GameObject _menuOutline;

    private void OnEnable(){
        BattleUIActions.OnCommandUsed += HideMenuOnCommandUsed;
        _menuOutline.SetActive(true);
        LeanTween.moveLocalX(gameObject, 0f, 0.05f);
        LeanTween.moveLocalX(_menuOutline, 0f, 0.2f);
    }

    private void OnDisable(){
        BattleUIActions.OnCommandUsed -= HideMenuOnCommandUsed;
        LeanTween.moveLocalX(gameObject, -25f, 0f);
        LeanTween.moveLocalX(_menuOutline, -528f, 0.2f);

        //--PRIVATE FUNCTIONS ARE COOL
        #pragma warning disable CS8321
        IEnumerator HideOutlineDelay(){
            yield return new WaitForSeconds(0.2f);
            _menuOutline.SetActive(false);
        }
        #pragma warning restore CS8321
    }

    private void HideMenuOnCommandUsed(){
        gameObject.SetActive(false);
    }

}
