using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private ServerManager serverManager;
    [SerializeField] private ClientManager clientManager;

    private Button startAsServerButton;
    private Button startAsClientButton;
    private TextField addressText;
    private TextField portText;
    private Label invalidConnectionDataLabel;


    void Start() {
        var root = GetComponent<UIDocument>().rootVisualElement; 
        
        startAsServerButton = root.Q<Button>("server-button");
        startAsClientButton = root.Q<Button>("client-button");

        addressText = root.Q<TextField>("address-field");
        portText = root.Q<TextField>("port-field");

        invalidConnectionDataLabel = root.Q<Label>("error-label");

        startAsServerButton.clicked += OnStartAsServerButtonClicked;
        startAsClientButton.clicked += OnStartAsClientButtonClicked;
    }

    void OnStartAsServerButtonClicked() {
        string address = addressText.text;
        int portNum = Int32.Parse(portText.text);
        IPAddress ip;
        if (portNum < 1024) {
            invalidConnectionDataLabel.text = "Invalid Port!";
            return;
        }
        else if (!IPAddress.TryParse(address, out ip)) {
            invalidConnectionDataLabel.text = "Invalid Address!";
        }
        else {
            serverManager.StartServer(address, portNum);
        }
    }

    void OnStartAsClientButtonClicked() {
          string address = addressText.text;
        int portNum = Int32.Parse(portText.text);
        IPAddress ip;
        if (portNum < 1024) {
            invalidConnectionDataLabel.text = "Invalid Port!";
            return;
        }
        else if (!IPAddress.TryParse(address, out ip)) {
            invalidConnectionDataLabel.text = "Invalid Address!";
        }
        else {
            clientManager.StartClient(address, portNum);
        }
    }
}
