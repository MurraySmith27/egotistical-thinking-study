using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private AudioSource m_mouseClickSFX;
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

        if (ClientManager.m_ipAddress != null)
        {
            addressText.value = ClientManager.m_ipAddress;
        }

        if (ClientManager.m_port != 0)
        {
            portText.value = ClientManager.m_port.ToString();
        }

        invalidConnectionDataLabel = root.Q<Label>("error-label");

        if (ServerManager.m_reset)
        {
            OnStartAsServerButtonClicked();
        }
        
        startAsServerButton.clicked += OnStartAsServerButtonClicked;
        startAsClientButton.clicked += OnStartAsClientButtonClicked;
    }

    private string GetIpAddress()
    {
        // var addresses = Dns.GetHostAddresses(Dns.GetHostName());
        // foreach (var ip in addresses) {
        //     if (ip.AddressFamily == AddressFamily.InterNetwork) {
        //         return ip.ToString();
        //     }
        // }
        // throw new System.Exception("No network adapters with an IPv4 address in the system!");

        StringBuilder sb = new StringBuilder(); 

        // Get a list of all network interfaces (usually one per network card, dialup, and VPN connection) 
        NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces(); 

        foreach (NetworkInterface network in networkInterfaces) 
        { 
            // Read the IP configuration for each network 
            IPInterfaceProperties properties = network.GetIPProperties(); 

            // Each network interface may have multiple IP addresses 
            foreach (IPAddressInformation address in properties.UnicastAddresses) 
            { 
                // We're only interested in IPv4 addresses for now 
                if (address.Address.AddressFamily != AddressFamily.InterNetwork) 
                    continue; 

                // Ignore loopback addresses (e.g., 127.0.0.1) 
                if (IPAddress.IsLoopback(address.Address)) 
                    continue;

                return address.Address.ToString();
            } 
        }

        return sb.ToString();
        
        // GetPublicIpAddressAsync();
    }

    private string _ip;
    private async Task<string> GetPublicIpAddressAsync()
    {
        using (HttpClient client = new HttpClient())
        {
            try
            {
                string ip = await client.GetStringAsync("https://api.ipify.org");
                Debug.LogError($"my ip: {ip}");
                _ip = ip;
                return ip.Trim();
            }
            catch (Exception ex)
            {
                Debug.LogError($"cannot find ip: {ex.Message}");
                return "";
            }
        }
    }

    private int GetFreePort()
    {
        TcpListener l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    void OnStartAsServerButtonClicked()
    {
        m_mouseClickSFX.Play();

        if (!_isStartServerCoroutineRunning)
            StartCoroutine(StartServer());
    }

    private bool _isStartServerCoroutineRunning = false;
    private IEnumerator StartServer()
    {
        _isStartServerCoroutineRunning = true;
        string address = ServerManager.m_ipAddress;

        address = GetIpAddress();

        int portNum = ServerManager.m_port;
        if (!ServerManager.m_reset)
        {
            portNum = GetFreePort();
        }
        
        IPAddress ip;
        // if (portNum < 1024) {
        //     invalidConnectionDataLabel.text = "Invalid Port!";
        //     return;
        // }
        if (!IPAddress.TryParse(address, out ip)) {
            invalidConnectionDataLabel.text = "Invalid Address!";
        }
        else
        {
            Debug.LogError($"address: {address}, portnum: {portNum}");
            serverManager.StartServer(address, portNum);
        }

        _isStartServerCoroutineRunning = false;
        yield return null;
    }

    void OnStartAsClientButtonClicked() {
        m_mouseClickSFX.Play();
        string address = addressText.text;
        int portNum = Int32.Parse(portText.text);

        ClientManager.m_port = portNum;
        ClientManager.m_ipAddress = address;
        
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
