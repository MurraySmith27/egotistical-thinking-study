using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class InventorySlot : VisualElement
{
    public Image Icon;
    public string ItemGuid = "";

    public InventorySlot()
    {
        Icon = new Image();
        this.Add(Icon);
        
        Icon.AddToClassList("slot-icon");
        this.AddToClassList("slot");
    }
    
    public void HoldItem(ItemDetails itemDetails)
    {
        Icon.image = itemDetails.Icon.texture;
        ItemGuid = itemDetails.GUID;
    }

    public void DropItem()
    {
        ItemGuid = "";
        Icon.image = null;
    }
}
