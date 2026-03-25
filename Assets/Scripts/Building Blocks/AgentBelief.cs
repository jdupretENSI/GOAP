using System;
using System.Collections.Generic;
using Agents;
using UnityEngine;

namespace Building_Blocks
{
    public class BeliefFactory
    {
        private readonly GoapAgent _goapAgent;
        private readonly Dictionary<string, AgentBelief> _beliefs;

        public BeliefFactory(GoapAgent agent, Dictionary<string, AgentBelief> beliefs)
        {
            _goapAgent = agent;
            _beliefs = beliefs;
        }

        public void AddBelief(string key, Func<bool> condition)
        {
            _beliefs.Add(key, new AgentBelief.Builder(key)
                .WithCondition(condition)
                .Build());
        }

        public void AddSensorBelief(string key, Sensor sensor)
        {
            _beliefs.Add(key, new AgentBelief.Builder(key)
                .WithCondition(() => sensor.IsTargetInRange)
                .WithLocation(() => sensor.TargetPosition)
                .Build());
        }

        public void AddLocationBelief(string key, float distance, Transform locationCondition)
        {
            // Instead of polling, we'll use a flag set by the station.
            // The flag is stored in the agent, so we just create a belief that reads it.
            _beliefs.Add(key, new AgentBelief.Builder(key)
                .WithCondition(() => _goapAgent.GetLocationFlag(key))
                .Build());
        }

        public void AddLocationBelief(string key, float distance, Vector3 locationCondition)
        {
            // For static locations not tied to a station, polling might still be needed,
            // but in our case we only use station locations. For simplicity, we keep this overload.
            _beliefs.Add(key, new AgentBelief.Builder(key)
                .WithCondition(() => _goapAgent.GetLocationFlag(key))
                .Build());
        }
    }

    public class AgentBelief
    {
        private AgentBelief(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public event Action<AgentBelief> OnValueChanged;

        private Func<bool> _condition = () => false;
        private Func<Vector3> _observedLocation = () => Vector3.zero;
        private bool _currentValue;

        public Vector3 Location => _observedLocation();

        public bool Evaluate() => _currentValue;

        // Called externally when the underlying data may have changed.
        public void Refresh()
        {
            bool newValue = _condition();
            if (newValue != _currentValue)
            {
                _currentValue = newValue;
                OnValueChanged?.Invoke(this);
            }
        }

        public class Builder
        {
            private readonly AgentBelief _agentBelief;

            public Builder(string name)
            {
                _agentBelief = new AgentBelief(name);
            }

            public Builder WithCondition(Func<bool> condition)
            {
                _agentBelief._condition = condition;
                _agentBelief._currentValue = condition(); // initial evaluation
                return this;
            }

            public Builder WithLocation(Func<Vector3> location)
            {
                _agentBelief._observedLocation = location;
                return this;
            }

            public AgentBelief Build() => _agentBelief;
        }
    }
}