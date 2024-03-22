using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;

public class NetworkSerializableUlongArray : INetworkSerializable
{
    public ulong[] arr;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsWriter)
        {
            serializer.GetFastBufferWriter().WriteValueSafe(arr);
        }
        else
        {
            serializer.GetFastBufferReader().ReadValueSafe(out arr);
        }
    }
    
}

public class NetworkSerializableIntArray : INetworkSerializable
{
    public int[] arr;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsWriter)
        {
            serializer.GetFastBufferWriter().WriteValueSafe(arr);
        }
        else
        {
            serializer.GetFastBufferReader().ReadValueSafe(out arr);
        }
    }
}

public class NetworkSerializableOrder : INetworkSerializable
{
    public int receivingPlayer;
    public int destinationWarehouse;
    private FixedString64Bytes[] requiredItemsKeys;
    private int[] requiredItemsValues;
    public Dictionary<string, int> requiredItems {
        get
        {
            Dictionary<string, int> dict = new();
            for (int i = 0; i < requiredItemsKeys.Length; i++)
            {
                dict.Add(requiredItemsKeys[i].ToString(), requiredItemsValues[i]);
            }
    
            return dict;
        }
    
        set
        {
            requiredItemsKeys = new FixedString64Bytes[value.Keys.Count];
            requiredItemsValues = new int[value.Keys.Count];
            int i = 0;
            foreach (string key in value.Keys)
            {
                requiredItemsKeys[i] = (FixedString64Bytes)key;
                requiredItemsValues[i] = value[key];
                i++;
            }
        }
    }
    
    public FixedString64Bytes textDescription;

    public NetworkSerializableOrder()
    {
        receivingPlayer = -1;
        destinationWarehouse = -1;
        requiredItemsKeys = new FixedString64Bytes[0];
        requiredItemsValues = new int[0];
        textDescription = "";
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsWriter)
        {
            var writer = serializer.GetFastBufferWriter();
            writer.WriteValueSafe(receivingPlayer);
            writer.WriteValueSafe(destinationWarehouse); 
            writer.WriteValueSafe(requiredItemsKeys);
            writer.WriteValueSafe(requiredItemsValues);
            writer.WriteValueSafe(textDescription);
        }
        else
        {
            var reader = serializer.GetFastBufferReader();
            reader.ReadValueSafe(out receivingPlayer);
            reader.ReadValueSafe(out destinationWarehouse);
            reader.ReadValueSafe(out requiredItemsKeys);
            reader.ReadValueSafe(out requiredItemsValues);
            reader.ReadValueSafe(out textDescription);
        }
    }
}

public class NetworkSerializableOrderArray : INetworkSerializable
{
    // public NetworkSerializableOrder[] arr;
    public NetworkSerializableOrder[] orders;

    public NetworkSerializableOrderArray()
    {
        orders = new NetworkSerializableOrder[0];
    }
    
    public NetworkSerializableOrderArray(NetworkSerializableOrder[] _orders)
    {
        orders = _orders;
    }
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsWriter)
        {
            serializer.SerializeValue(ref orders);
        }
        else
        {
            if (orders != null)
            {
                serializer.SerializeValue(ref orders);
            }
        }
    }
}
