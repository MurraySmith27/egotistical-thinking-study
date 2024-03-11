using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerInventoryMenuController : MonoBehaviour
{

    [SerializeField] private GameObject m_clientMenuGameObject;

    [SerializeField] private VisualTreeAsset m_inventoryElementAsset;

    private VisualElement m_inventoryElement;
    
    private VisualElement m_root;
    
    private Button m_backButton;

    private List<InventorySlot> m_inventoryItems = new List<InventorySlot>();
    
    void Start()
    {
        m_root = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("root");

        m_backButton = m_root.Q<Button>("back-button");
        
        VisualElement inventoryRoot = m_root.Q<VisualElement>("inventory-root");
        
        m_inventoryElement = m_inventoryElementAsset.Instantiate();
        inventoryRoot.Add(m_inventoryElement);

        m_backButton.clicked += BackButtonClicked;
        
        
        //get current player number
        int playerNum = ClientConnectionHandler.Instance.clientSideSessionInfo.playerNum;
        
        
        VisualElement inventoryContainer = m_inventoryElement.Q<VisualElement>("slot-container");
        
        //populate inventory
        for (int i = 0; i < InventorySystem.Instance.m_numInventorySlotsPerPlayer; i++)
        {
            InventorySlot itemSlot = new InventorySlot();
            m_inventoryItems.Add(itemSlot);
            inventoryContainer.Add(itemSlot);
        }
    }

    private void BackButtonClicked()
    {
        m_clientMenuGameObject.SetActive(true);
        this.gameObject.SetActive(false);
    }
    
    
}
