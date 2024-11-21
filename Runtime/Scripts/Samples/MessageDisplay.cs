using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MessageDisplay : MonoBehaviour
{
    public TMP_Text Text => _textRenderer;
    [SerializeField] TMP_Text _textRenderer;

    const string _startColorOpen = "<color=";
    const string _startColorClose = ">";
    const string _stopColor = "</color>";
    const string _startBold = "<b>";
    const string _stopBold = "</b>";

    public void SetMessage(string timestamp, string name, string message, Color userColor)
    {
        timestamp = $"<color=grey>{timestamp}</color>";

        string colorString = ColorUtility.ToHtmlStringRGB(userColor);

        string nameOpenTag = $"{timestamp}  {_startColorOpen}#{colorString}{_startColorClose}{_startBold}";
        string nameCloseTag = $"{_stopColor}{_stopBold}";
        string nameString = $"{nameOpenTag}{name}{nameCloseTag}";
        string text = $"{nameString}: {message}";
        _textRenderer.text = text;
    }
}
