using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private ServerManager serverManager;
    [SerializeField] private ClientManager clientManager;

    private Button startAsServerButton;
    private Button startAsClientButton;


    void Start() {
        var root = GetComponent<UIDocument>().rootVisualElement; 
        
        startAsServerButton = root.Q<Button>("server-button");
        startAsClientButton = root.Q<Button>("client-button");

        startAsServerButton.clicked += OnStartAsServerButtonClicked;
        startAsClientButton.clicked += OnStartAsClientButtonClicked;
    }

    void OnStartAsServerButtonClicked() {
        serverManager.StartServer();
    }

    void OnStartAsClientButtonClicked() {
        clientManager.StartClient();
    }
}
