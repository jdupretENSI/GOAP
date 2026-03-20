using System;
using System.Collections.Generic;

namespace Building_Blocks
{
    // What do we want to achieve
    public class AgentGoal
    {
        public string Name {get;}
        public float Priority {get; private set;}
        public HashSet<AgentBelief> DesiredEffects { get; } = new();

        AgentGoal(string name)
        {
            Name = name;
        }
        
        public class Builder
        {
            readonly AgentGoal _agentGoal;

            public Builder(string name)
            {
                _agentGoal = new AgentGoal(name);
            }

            public Builder WithPriority(float priority)
            {
                _agentGoal.Priority = priority;
                return this;
            }

            public Builder WithDesiredEffect(AgentBelief desiredEffect)
            {
                _agentGoal.DesiredEffects.Add(desiredEffect);
                return this;
            }

            public AgentGoal Build()
            {
                return _agentGoal;
            }
        }

    }
}