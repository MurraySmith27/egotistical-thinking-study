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
    public int orderTimeLimit;
    public int orderTimeRemaining;
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
    public int scoreReward;
    public int incompletePenalty;
    public FixedString64Bytes textDescription;

    public NetworkSerializableOrder()
    {
        orderTimeLimit = -1;
        orderTimeRemaining = -1;
        receivingPlayer = -1;
        destinationWarehouse = -1;
        requiredItemsKeys = new FixedString64Bytes[0];
        requiredItemsValues = new int[0];
        textDescription = "";
        scoreReward = 1000;
        incompletePenalty = 0;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsWriter)
        {
            var writer = serializer.GetFastBufferWriter();
            writer.WriteValueSafe(orderTimeLimit);
            writer.WriteValueSafe(orderTimeRemaining);
            writer.WriteValueSafe(receivingPlayer);
            writer.WriteValueSafe(destinationWarehouse); 
            writer.WriteValueSafe(requiredItemsKeys);
            writer.WriteValueSafe(requiredItemsValues);
            writer.WriteValueSafe(textDescription);
            writer.WriteValueSafe(scoreReward);
            writer.WriteValueSafe(incompletePenalty);
        }
        else
        {
            var reader = serializer.GetFastBufferReader();
            reader.ReadValueSafe(out orderTimeLimit);
            reader.ReadValueSafe(out orderTimeRemaining);
            reader.ReadValueSafe(out receivingPlayer);
            reader.ReadValueSafe(out destinationWarehouse);
            reader.ReadValueSafe(out requiredItemsKeys);
            reader.ReadValueSafe(out requiredItemsValues);
            reader.ReadValueSafe(out textDescription);
            reader.ReadValueSafe(out scoreReward);
            reader.ReadValueSafe(out incompletePenalty);
        }
    }
}

public class NetworkSerializableRoadblock : INetworkSerializable
{
    public int informedPlayer;
    public NetworkSerializableIntArray affectedTilesXPositions;
    public NetworkSerializableIntArray affectedTilesYPositions;

    public List<(int, int)> affectedTiles
    {
        get
        {
            List<(int, int)> list = new();
            for (int i = 0; i < affectedTilesXPositions.arr.Length; i++)
            {
                list.Add((affectedTilesXPositions.arr[i], affectedTilesYPositions.arr[i]));
            }

            return list;
        }
        set
        {
            List<int> x = new();
            List<int> y = new();
            foreach ((int, int) tile in value)
            {
                x.Add(tile.Item1);
                y.Add(tile.Item2);
            }

            affectedTilesXPositions.arr = x.ToArray();
            affectedTilesYPositions.arr = y.ToArray();
        }
    }

    public NetworkSerializableRoadblock()
    {
        informedPlayer = -1;
        affectedTilesXPositions = new NetworkSerializableIntArray();
        affectedTilesYPositions = new NetworkSerializableIntArray();
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsWriter)
        {
            serializer.SerializeValue(ref affectedTilesXPositions);
            serializer.SerializeValue(ref affectedTilesYPositions);
            
            var writer = serializer.GetFastBufferWriter();
            writer.WriteValueSafe(informedPlayer);
        }
        else
        {
            var reader = serializer.GetFastBufferReader();
            reader.ReadValueSafe(out informedPlayer);
            
            serializer.SerializeValue(ref affectedTilesXPositions);
            serializer.SerializeValue(ref affectedTilesYPositions);
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

public class NetworkSerializableRoadblockArray : INetworkSerializable
{
    public NetworkSerializableRoadblock[] roadblocks;

    public NetworkSerializableRoadblockArray()
    {
        roadblocks = new NetworkSerializableRoadblock[0];
    }
    
    public NetworkSerializableRoadblockArray(NetworkSerializableRoadblock[] _orders)
    {
        roadblocks = _orders;
    }
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsWriter)
        {
            serializer.SerializeValue(ref roadblocks);
        }
        else
        {
            if (roadblocks != null)
            {
                serializer.SerializeValue(ref roadblocks);
            }
        }
    }
}
