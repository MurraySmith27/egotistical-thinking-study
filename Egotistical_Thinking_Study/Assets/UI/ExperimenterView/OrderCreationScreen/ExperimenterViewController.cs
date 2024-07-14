using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class ExperimenterViewController : MonoBehaviour
{
    [SerializeField] private AudioSource m_mouseClickSFX;
    [SerializeField] private VisualTreeAsset m_orderElement;

    [SerializeField] private VisualTreeAsset m_gasRefillButtonElement;

    private VisualElement m_root;
    private VisualElement m_orderContainer;

    private List<VisualElement> m_orderElements;

    private Dictionary<int, VisualElement> m_gasRefillElementsPerPlayer = new Dictionary<int, VisualElement>();
    
    private string m_cachedConfigFilePath;
    private string m_cachedMapFilePath;

    private void Start()
    {
        m_root = GetComponent<UIDocument>().rootVisualElement;

        m_orderElements = new List<VisualElement>();

        m_orderContainer = m_root.Q<VisualElement>("order-container");

        Order[] orders = GameRoot.Instance.configData.Orders;
        for (int i = 0; i < orders.Length; i++)
        {
            VisualElement orderElement = m_orderElement.Instantiate();
            orderElement.AddToClassList("order");
            ProgressBar orderTimer = orderElement.Q<ProgressBar>("order-timer");
            if (orders[i].TimeLimitSeconds != -1)
            {
                orderTimer.style.visibility = Visibility.Visible;
                orderTimer.lowValue = 100;
                orderTimer.title = $"{orders[i].TimeLimitSeconds}s";
            }

            orderElement.Q<Label>("order-number-label").text = $"Order {i + 1}:";
            orderElement.Q<Label>("order-description").text = orders[i].TextDescription;
            orderElement.Q<Label>("send-to-player-label").text = $"Receiving Player: {orders[i].RecievingPlayer}";
            orderElement.Q<Label>("score-reward-label").text = $"Reward: {orders[i].ScoreReward}G";
            VisualElement itemsContainer = orderElement.Q<VisualElement>("order-items-container");
            foreach (string key in orders[i].RequiredItems.Keys)
            {
                int itemNum = Int32.Parse(key);
                InventorySlot slot = new InventorySlot(false);
                slot.HoldItem(InventorySystem.Instance.m_items[itemNum], orders[i].RequiredItems[key]);
                itemsContainer.Add(slot);
            }

            string mapDestinationText = $"Destination Warehouse:";
            
            //get map sprite
            ulong destiationNetworkObjectId = MapDataNetworkBehaviour.Instance.GetNetworkIdOfDestination(orders[i].DestinationWarehouse);

            Sprite destinationSprite = null;
            foreach (NetworkBehaviour networkBehaviour in FindObjectsOfType<NetworkBehaviour>())
            {
                if (networkBehaviour.NetworkObjectId == destiationNetworkObjectId)
                {
                    destinationSprite = networkBehaviour.GetComponentInChildren<SpriteRenderer>().sprite;
                    break;
                }
            }

            orderElement.Q<VisualElement>("map-destination-image").style.backgroundImage = destinationSprite.texture;
            
            orderElement.Q<Label>("map-destination-label").text = mapDestinationText;

            int x = i;
            orderElement.Q<Button>("send-order-button").clicked += () =>
            {
                m_mouseClickSFX.Play();
                orderElement.Q<Button>("send-order-button").style.visibility = Visibility.Hidden;
                OnOrderSent(x);
            };

            orderElement.MarkDirtyRepaint();
            m_orderContainer.Add(orderElement);
            m_orderElements.Add(orderElement);
            m_orderContainer.MarkDirtyRepaint();
        }

        ClientConnectionHandler.Instance.m_onClientConnected += OnClientConnected;

        m_root.Q<Label>("ip-label").text = $"IP: {ServerManager.m_ipAddress}";
        
        m_root.Q<Label>("port-label").text = $"Port: {ServerManager.m_port}";
        
        m_root.Q<Label>("score-label").text = $"Total Score: {OrderSystem.Instance.currentScorePerPlayer.Value.arr.Sum()}G";

        TimeSpan t = TimeSpan.FromSeconds(GameTimerSystem.Instance.timerSecondsRemaining.Value);
        m_root.Q<Label>("timer-label").text = t.ToString(@"mm\:ss");

        m_root.Q<Button>("pause-resume-button").clicked += OnPauseResumeButtonClicked;
        
        m_root.Q<Button>("reset-button").clicked += OnResetButtonClicked;
        
        GameTimerSystem.Instance.timerSecondsRemaining.OnValueChanged += OnTimerValueChanged;
        
        OrderSystem.Instance.onScoreChanged += OnScoreChanged;

        OrderSystem.Instance.onOrderChanged += OnOrderValueChanged;
        OrderSystem.Instance.onOrderComplete += OnOrderComplete;
        OrderSystem.Instance.onOrderIncomplete += OnOrderIncomplete;
        OrderSystem.Instance.onOrderRejected += OnOrderRejected;
    }

    private void OnTimerValueChanged(int oldTimerValueSeconds, int currentTimerValueSeconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(currentTimerValueSeconds);
        m_root.Q<Label>("timer-label").text = t.ToString(@"mm\:ss");

        if (currentTimerValueSeconds <= 0)
        {
            GameTimerSystem.Instance.isGamePaused.Value = true;

            Button pauseResumeButton = m_root.Q<Button>("pause-resume-button");
            pauseResumeButton.clicked -= OnPauseResumeButtonClicked;
            pauseResumeButton.style.opacity = 0.5f;
        }
        else if (currentTimerValueSeconds < 60)
        {
            m_root.Q<VisualElement>("timer-parent").style.backgroundColor = new Color(128, 0, 0);
        }
    }

    private void OnPauseResumeButtonClicked()
    {
        m_mouseClickSFX.Play();
        if (!GameTimerSystem.Instance.isGamePaused.Value)
        {
            m_root.Q<Button>("pause-resume-button").text = "Resume";
        }
        else
        {
            m_root.Q<Button>("pause-resume-button").text = "Pause";
        }

        GameTimerSystem.Instance.isGamePaused.Value = !GameTimerSystem.Instance.isGamePaused.Value;
    }
    
    private void OnResetButtonClicked()
    {
        m_mouseClickSFX.Play();
        ServerManager.m_reset = true;
        
        foreach (GameObject obj in FindObjectsOfType(typeof(GameObject)))
        {
            if (obj != null && obj.name != this.gameObject.name && obj.name != this.transform.parent.name && obj.name != NetworkManager.Singleton.gameObject.name)
            {
                DestroyImmediate(obj);
            }
        }
        
        NetworkManager.Singleton.Shutdown();
        DestroyImmediate(NetworkManager.Singleton.gameObject);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    private void OnScoreChanged(int[] scorePerPlayer)
    {
        m_root.Q<Label>("score-label").text = $"Total Score: {scorePerPlayer.Sum()}G";
        
        for (int i = 0; i < scorePerPlayer.Length; i++)
        {
            if (m_gasRefillElementsPerPlayer.ContainsKey(i))
            {
                m_gasRefillElementsPerPlayer[i].Q<Label>("score-label").text = $"{scorePerPlayer[i]}G";
            }
        }
    }
    
    private void OnClientConnected(ulong clientId)
    {
        foreach (Guid guid in ClientConnectionHandler.Instance.serverSideClientList.Keys)
        {
            if (ClientConnectionHandler.Instance.serverSideClientList[guid].clientId == clientId)
            {
                int playerNum = ClientConnectionHandler.Instance.serverSideClientList[guid].playerNum;

                GameObject playerGameObject = GameObject.FindGameObjectWithTag($"Player{playerNum}");

                Sprite playerIcon = playerGameObject.transform.GetChild(0).GetComponent<SpriteRenderer>().sprite;

                VisualElement gasRefillElement = m_gasRefillButtonElement.Instantiate();
                
                m_root.Q<VisualElement>("refill-buttons-parent").Add(gasRefillElement);

                Label scoreLabel = gasRefillElement.Q<Label>("score-label");
                
                if (!GameRoot.Instance.configData.IsScoreShared)
                {
                    scoreLabel.parent.Remove(scoreLabel);
                }
                else
                {
                    scoreLabel.text = $"{OrderSystem.Instance.currentScorePerPlayer.Value.arr[playerNum]}G";
                }
                
                m_gasRefillElementsPerPlayer.Add(playerNum, gasRefillElement);

                gasRefillElement.Q<VisualElement>("icon").style.backgroundImage = Background.FromSprite(playerIcon);

                gasRefillElement.Q<Button>("refill-button").clicked += () => OnGasRefillButtonClicked(guid);
            }
        }
    }

    private void OnGasRefillButtonClicked(Guid playerGuid)
    {
        m_mouseClickSFX.Play();
        int playerNum = ClientConnectionHandler.Instance.serverSideClientList[playerGuid].playerNum;
        GameObject playerGameObject = GameObject.FindGameObjectWithTag($"Player{playerNum}");

        playerGameObject.GetComponent<PlayerNetworkBehaviour>().RefillGas();
    }
    
    
    private void OnOrderValueChanged(int orderIndex)
    {
        VisualElement orderElement = m_orderElements[orderIndex];
        ProgressBar orderTimer = orderElement.Q<ProgressBar>("order-timer");
        NetworkSerializableOrder order = OrderSystem.Instance.orders.Value.orders[orderIndex];
        if (order.orderTimeLimit != -1)
        {
            orderTimer.lowValue = 100f * order.orderTimeRemaining /
                                  (float)order.orderTimeLimit;
            orderTimer.title = $"{order.orderTimeRemaining}s";
            orderTimer.MarkDirtyRepaint();
        }
    }

    private void OnOrderComplete(int orderIndex)
    {
        VisualElement orderElement = m_orderElements[orderIndex];
        
        if (OrderSystem.Instance.completeOrders.Value.arr[orderIndex] != 0)
        {
            orderElement.Q<VisualElement>("checkmark-overlay").style.visibility = Visibility.Visible;
        }
    }

    private void OnOrderIncomplete(int orderIndex)
    {
        VisualElement orderElement = m_orderElements[orderIndex];
        
        if (OrderSystem.Instance.incompleteOrders.Value.arr[orderIndex] != 0)
        {
            orderElement.Q <VisualElement>("x-overlay").style.visibility = Visibility.Visible;
        }
    }

    private void OnOrderRejected(int orderIndex)
    {
        VisualElement orderElement = m_orderElements[orderIndex];

        if (OrderSystem.Instance.acceptedOrders.Value.arr[orderIndex] == 1)
        {
            orderElement.Q<VisualElement>("x-overlay").style.visibility = Visibility.Visible;
        }
    }

    private void OnOrderSent(int orderIndex)
    {
        OrderSystem.Instance.SendOrder(orderIndex);
    }
}
