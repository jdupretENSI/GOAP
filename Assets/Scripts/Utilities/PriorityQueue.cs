using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Utilities
{
    public class PriorityQueue<T>
    {
        private readonly List<T> _items = new();
        private readonly List<float> _priorities = new();
        
        public int Count => _items.Count;
        
        public void Enqueue(T item, float priority)
        {
            _items.Add(item);
            _priorities.Add(priority);
        }

        public T Dequeue()
        {
            float lowestPriority = float.MaxValue;
            int lowestPriorityIndex = -1;

            for (int i = 0; i < _priorities.Count; i++)
            {
                if (_priorities[i] < lowestPriority)
                {
                    lowestPriority = _priorities[i];
                    lowestPriorityIndex = i;
                }
            }

            if (lowestPriorityIndex == -1)
            {
                Debug.LogWarning("Priority queue contains no items");
                return default;
            }
            T returnItem = _items[lowestPriorityIndex];
            _items.RemoveAt(lowestPriorityIndex);
            _priorities.RemoveAt(lowestPriorityIndex);
            
            return returnItem;
        }

        public T Peek()
        {
            float lowestPriority = float.MaxValue;
            int lowestPriorityIndex = -1;

            for (int i = 0; i < _priorities.Count; i++)
            {
                if (_priorities[i] < lowestPriority)
                {
                    lowestPriority = _priorities[i];
                    lowestPriorityIndex = i;
                }
            }

            if (lowestPriorityIndex == -1)
            {
                Debug.LogWarning("Priority queue contains no items");
                return default;
            }
            
            return _items[lowestPriorityIndex];
        }
        
    }
}