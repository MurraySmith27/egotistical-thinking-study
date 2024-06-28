using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class ClientConnectionIntermediateController : MonoBehaviour
{

    [SerializeField] private AudioSource m_mouseClickSFX;
    
    private VisualElement m_root;

    private Button m_backButton;

    void Start()
    {
        m_root = GetComponent<UIDocument>().rootVisualElement;

        m_backButton = m_root.Q<Button>("back-button");

        m_backButton.clicked += OnBackButtonClicked;
    }

    private void OnBackButtonClicked()
    {
        m_mouseClickSFX.Play();
        NetworkManager.Singleton.Shutdown();
        ((ClientManager)Object.FindObjectOfType(typeof(ClientManager)))?.OnDisconnected(0);
    }
}