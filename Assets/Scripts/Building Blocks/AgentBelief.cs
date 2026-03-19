using System;
using System.Collections.Generic;
using Agents;
using UnityEngine;

namespace Building_Blocks
{
    /// <summary>
    /// Helper class that will enable us to create a dictionary of Beliefs
    /// </summary>
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

        /// <summary>
        /// Overload if passing a transform, will get the transform position
        /// </summary>
        public void AddLocationBelief(string key, float distance, Transform locationCondition)
        {
            AddLocationBelief(key, distance, locationCondition.position);
        }

        /// <summary>
        /// Are we in range of this thing? This method will create a belief to verify this statement
        /// </summary>
        /// <param name="key"></param>
        /// <param name="distance">Distance to location</param>
        /// <param name="locationCondition">Location itself</param>
        public void AddLocationBelief(string key, float distance, Vector3 locationCondition)
        {
            _beliefs.Add(key, new AgentBelief.Builder(key)
                .WithCondition(() => InRangeOf(locationCondition, distance))
                .WithLocation(() => locationCondition)
                .Build());
        }

        private bool InRangeOf(Vector3 pos, float range) => Vector3.Distance(_goapAgent.transform.position, pos) < range;
    }

    [Serializable]
    public class AgentBelief
    {
        private AgentBelief(string name)
        {
            Name = name;
        }
    
        /// <summary>
        /// For debugging
        /// </summary>
        public string Name { get; }
    
        [SerializeField] private Func<bool> _condition = () => false;
        private Func<Vector3> _observedLocation =  () => Vector3.zero;
    
        /// <summary>
        /// When we want to find the location of this belief it will be reevaluated
        /// </summary>
        public Vector3 Location => _observedLocation();
    
        public bool Evaluate() => _condition();

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
                return this;
            }

            public Builder WithLocation(Func<Vector3> location)
            {
                _agentBelief._observedLocation =  location;
                return this;
            }

            public AgentBelief Build()
            {
                return _agentBelief;
            }
        }
    
    
    }
}