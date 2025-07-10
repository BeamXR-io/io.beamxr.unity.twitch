using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MessageDisplay : MonoBehaviour
{
    public TMP_Text Text => _textRenderer;
    [SerializeField] TMP_Text _textRenderer;

    [SerializeField]
    private Color _timeColor = Color.gray, _messageColor = Color.white;
    private string _timeColorHex, _messageColorHex;

    const string _startColorOpen = "<color=";
    const string _startColorClose = ">";
    const string _stopColor = "</color>";
    const string _startBold = "<b>";
    const string _stopBold = "</b>";

    public void SetMessage(string timestamp, string name, string message, Color userColor)
    {
        if(_timeColorHex == null || _timeColorHex == "")
        {
            _timeColorHex = ColorUtility.ToHtmlStringRGBA(_timeColor);
        }

        if(_messageColorHex == null || _messageColorHex == "")
        {
            _messageColorHex = ColorUtility.ToHtmlStringRGBA(_messageColor);
        }

        timestamp = $"<color=#{_timeColorHex}>{timestamp}</color>";

        string colorString = ColorUtility.ToHtmlStringRGB(userColor);

        string nameOpenTag = $"{timestamp} {_startColorOpen}#{colorString}{_startColorClose}{_startBold}";
        string nameCloseTag = $"{_stopColor}{_stopBold}";
        string nameString = $"{nameOpenTag}{name}{nameCloseTag}";
        string text = $"{nameString}: <color=#{_messageColorHex}>{message}</color>";
        _textRenderer.text = text;
    }
}
