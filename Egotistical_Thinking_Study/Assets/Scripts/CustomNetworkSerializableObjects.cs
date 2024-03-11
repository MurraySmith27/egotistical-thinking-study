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
    public int[] mapDestination;
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
    
    public string textDescription;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsWriter)
        {
            serializer.GetFastBufferWriter().WriteValueSafe(receivingPlayer);
            serializer.GetFastBufferWriter().WriteValueSafe(mapDestination);
            serializer.GetFastBufferWriter().WriteValueSafe(requiredItemsKeys);
            serializer.GetFastBufferWriter().WriteValueSafe(requiredItemsValues);
            serializer.GetFastBufferWriter().WriteValueSafe(textDescription);
        }
        else
        {
            serializer.GetFastBufferReader().ReadValueSafe(out textDescription);
            serializer.GetFastBufferReader().ReadValueSafe(out requiredItemsValues);
            serializer.GetFastBufferReader().ReadValueSafe(out requiredItemsKeys);
            serializer.GetFastBufferReader().ReadValueSafe(out mapDestination);
            serializer.GetFastBufferReader().ReadValueSafe(out receivingPlayer);
        }
    }
}

public class NetworkSerializableOrderArray : INetworkSerializable
{
    public NetworkSerializableOrder[] arr;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsWriter)
        {
            foreach (NetworkSerializableOrder order in arr)
            {
                order.NetworkSerialize<T>(serializer);
            }
        }
        else
        {
            if (arr != null)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    arr[i].NetworkSerialize<T>(serializer);
                }
            }
        }
    }
}
