using System;
using UnityEngine;

public class DialogueResponseEvents : MonoBehaviour
{
    [SerializeField] private DialogueSO _dialogueSO;
    public DialogueSO DialogueSO => _dialogueSO;
    [SerializeField] private ResponseEvent[] _responseEvents;
    public ResponseEvent[] ResponseEvents => _responseEvents;

    public void OnValidate()
    {
        if( _dialogueSO == null ) return;
        if( _dialogueSO.Responses == null ) return;
        if( _responseEvents != null && _responseEvents.Length == _dialogueSO.Responses.Length ) return;

        if( _responseEvents == null ){
            _responseEvents = new ResponseEvent[ _dialogueSO.Responses.Length ];
        }
        else{
            Array.Resize(ref _responseEvents, _dialogueSO.Responses.Length );
        }

        for( int i = 0; i < _dialogueSO.Responses.Length; i++ ){
            Response response = _dialogueSO.Responses[i];

            if( _responseEvents[i] != null ){
                _responseEvents[i].name = response.ResponseText;
                continue;
            }

            _responseEvents[i] = new ResponseEvent(){ name = response.ResponseText };
        }
    }
}
