using System.Collections.Generic;

namespace Building_Blocks
{
    
    public class AgentAction
    {
        private AgentAction(string name)
        {
            Name = name;
        }
        
        public string Name { get; }
        public float Cost { get; private set; }

        public HashSet<AgentBelief> Preconditions { get; } = new();
        public HashSet<AgentBelief> Effects { get; } = new();

        /// <summary>
        /// Strategy Programing pattern to decouple the action from what is happening.
        /// </summary>
        private IActionStrategy _strategy;

        public bool Complete => _strategy.Complete;
        
        public void Start() => _strategy.Start();

        public void Update(float deltaTime)
        {
            // Check if the action can be performed and update the strategy
            if (_strategy.CanPerform) _strategy.Update(deltaTime);
            // Bail out if the strategy is still executing
            if (!_strategy.Complete) return;
            
            // Apply effects
            foreach (AgentBelief effects in Effects)
            {
                effects.Evaluate();
            }
            
            
        }
        
        public void Stop() => _strategy.Stop();

        public class Builder
        {
            readonly AgentAction _agentAction;

            public Builder(string name)
            {
                _agentAction = new AgentAction(name)
                {
                    Cost = 1
                };
            }
            public Builder WithCost(float cost)
            {
                _agentAction.Cost = cost;
                return this;
            }

            public Builder WithStrategy(IActionStrategy strategy)
            {
                _agentAction._strategy = strategy;
                return this;
            }

            public Builder AddPrecondition(AgentBelief precondition)
            {
                _agentAction.Preconditions.Add(precondition);
                return this;
            }

            public Builder AddEffect(AgentBelief effect)
            {
                _agentAction.Effects.Add(effect);
                return this;
            }

            public AgentAction Build()
            {
                return _agentAction;
            }
        }
    }


}