using System;
using System.Collections.Generic;
using Building_Blocks;
using UnityEngine;

[Serializable]
public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
{
    [SerializeField] private List<TKey> keys = new List<TKey>();
    [SerializeField] private List<TValue> values = new List<TValue>();

    // Save dictionary to lists
    public void OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();
        
        foreach (KeyValuePair<TKey, TValue> pair in this)
        {
            keys.Add(pair.Key);
            values.Add(pair.Value);
        }
    }

    // Load dictionary from lists
    public void OnAfterDeserialize()
    {
        Clear();

        if (keys.Count != values.Count)
        {
            Debug.LogError($"Key count ({keys.Count}) doesn't match value count ({values.Count})");
            return;
        }

        for (int i = 0; i < keys.Count; i++)
        {
            Add(keys[i], values[i]);
        }
    }
}

[Serializable] public class StringAgentBelief : SerializableDictionary<string, AgentBelief> { }