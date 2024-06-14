using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DebugLogger : MonoBehaviour
{
    
    [SerializeField] private TMP_Text m_logElement;
    [SerializeField] private ScrollRect m_scrollRect;
    [SerializeField] private float m_scrollSpeed = 10f;
    [SerializeField] private float m_scrollCatchupSpeed = 1f;

    private float m_currentNormalizedVerticalScrollPosition = 0f;
    
    void Start()
    {
        Application.logMessageReceived += PrintLogMessage;
        m_logElement.text = "";
        DontDestroyOnLoad(this.transform.parent.gameObject);
    }

    void Update()
    {
        float mouseScroll = Input.GetAxis("Mouse ScrollWheel");
        if (!Application.isFocused)
        {
            mouseScroll = 0;
        }
        m_currentNormalizedVerticalScrollPosition =
            Mathf.Clamp(m_currentNormalizedVerticalScrollPosition + m_scrollSpeed * mouseScroll, 0, 1);

        m_scrollRect.verticalNormalizedPosition = Mathf.Lerp(m_scrollRect.verticalNormalizedPosition,
            m_currentNormalizedVerticalScrollPosition, m_scrollCatchupSpeed * Time.deltaTime);
    }

    private void PrintLogMessage(string logString, string stackTrace, LogType type)
    {
        string textColor = "white";
        if (type == LogType.Error)
        {
            textColor = "red";
        }
        else if (type == LogType.Warning)
        {
            textColor = "yellow";
        }

        m_logElement.text += $"<color={textColor}>{logString}<br>";
        if (stackTrace.Length > 0)
        {
            m_logElement.text += $"Stack Trace: ({stackTrace}) <br>";
        }

    } 
}
