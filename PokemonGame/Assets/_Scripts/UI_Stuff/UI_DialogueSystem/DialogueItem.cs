using UnityEngine;
using System;

[Serializable] 
public class DialogueItem
{
    [SerializeField] private string _speakerName;
    [SerializeField] private DialogueSpeaker _dialogueSpeaker;
    [SerializeField] private DialogueColorSO _dialogueColor;
    [SerializeField] private Sprite _leftPortrait_1, _leftPortrait_2, _rightPortrait_1, _rightPortrait_2;
    [SerializeField] [TextArea(5, 5)] private string _dialogue;
    public string SpeakerName => _speakerName;
    public DialogueSpeaker DialogueSpeaker => _dialogueSpeaker;
    public DialogueColorSO DialogueColor => _dialogueColor;
    public Sprite LeftPortrait_1 => _leftPortrait_1;
    public Sprite LeftPortrait_2 => _leftPortrait_2;
    public Sprite RightPortrait_1 => _rightPortrait_1;
    public Sprite RightPortrait_2 => _rightPortrait_2;
    public string Dialogue => _dialogue;
}
