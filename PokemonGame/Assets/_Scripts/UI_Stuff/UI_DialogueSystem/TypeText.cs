using System.Collections;
using UnityEngine;
using TMPro;

public class TypeText : MonoBehaviour
{
    [SerializeField] private float _typeSpeed;
    
    public Coroutine RunDialogue( string textToType, TMP_Text dialogueText ){
        return StartCoroutine( TypeDialogue( textToType, dialogueText) );
    }
    
    private IEnumerator TypeDialogue( string textToType, TMP_Text dialogueText ){
        dialogueText.text = string.Empty;
        
        float t = 0;
        int charIndex = 0;
        
        while( charIndex < textToType.Length ){
            t += Time.deltaTime * _typeSpeed;
            charIndex = Mathf.FloorToInt( t );
            charIndex = Mathf.Clamp( charIndex, 0, textToType.Length );
            
            dialogueText.text = textToType.Substring( 0, charIndex );
            
            yield return null;
        }
    }
}
