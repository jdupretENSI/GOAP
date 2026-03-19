using System;
using System.Collections.Generic;
using System.Linq;
using Agents;
using UnityEngine;

namespace Building_Blocks
{
    public interface IGoapPlanner
    {
        ActionPlan Plan(GoapAgent agent, HashSet<AgentGoal> goals, AgentGoal mostRecentGoal = null);
    }

    public class GoapPlanner : IGoapPlanner
    {
        public ActionPlan Plan(GoapAgent agent, HashSet<AgentGoal> goals, AgentGoal mostRecentGoal = null)
        {
            // Order goals by priority, descending
            List<AgentGoal> orderedGoals = goals
                // Don't include goals where the effects are already complete
                .Where(g => g.DesiredEffects.Any(b => !b.Evaluate()))
                // Operator to not grab the same top goal over and over
                .OrderByDescending(g => g == mostRecentGoal ? g.Priority -0.01 : g.Priority)
                .ToList();

            //TODO A* implementation
            // Currently this is a BFS
            foreach (AgentGoal goal in orderedGoals)
            {
                Node goalNode = new Node(null, null, goal.DesiredEffects, 0);

                if (FindPath(goalNode, agent.Actions))
                {
                    // If the goalNode has no leaves and no action to perform try a different goal
                    if (goalNode.IsLeafDead) continue;
                
                    Stack<AgentAction> actionStack = new Stack<AgentAction>();
                    while (goalNode.Leaves.Count > 0)
                    {
                        Node cheapestLeaf = goalNode.Leaves.OrderBy(leaf => leaf.Cost).First();
                        goalNode = cheapestLeaf;
                        actionStack.Push(cheapestLeaf.Action);
                    }

                    return new ActionPlan(goal, actionStack, goalNode.Cost);
                }
            }
            Debug.LogWarning("No plan found");
            return null;
        }

        bool FindPath(Node parent, HashSet<AgentAction> actions) {
            // Order actions by cost, ascending
            IOrderedEnumerable<AgentAction> orderedActions = actions.OrderBy(a => a.Cost);
        
            foreach (AgentAction action in orderedActions) {
                HashSet<AgentBelief> requiredEffects = parent.RequiredEffects;
            
                // Remove any effects that evaluate to true, there is no action to take
                requiredEffects.RemoveWhere(b => b.Evaluate());
            
                // If there are no required effects to fulfill, we have a plan
                if (requiredEffects.Count == 0) {
                    return true;
                }

                if (action.Effects.Any(requiredEffects.Contains)) {
                    HashSet<AgentBelief> newRequiredEffects = new HashSet<AgentBelief>(requiredEffects);
                    newRequiredEffects.ExceptWith(action.Effects);
                    newRequiredEffects.UnionWith(action.Preconditions);
                
                    HashSet<AgentAction> newAvailableActions = new HashSet<AgentAction>(actions);
                    newAvailableActions.Remove(action);
                
                    Node newNode = new Node(parent, action, newRequiredEffects, parent.Cost + action.Cost);
                
                    // Explore the new node recursively
                    if (FindPath(newNode, newAvailableActions)) {
                        parent.Leaves.Add(newNode);
                        newRequiredEffects.ExceptWith(newNode.Action.Preconditions);
                    }
                
                    // If all effects at this depth have been satisfied, return true
                    if (newRequiredEffects.Count == 0) {
                        return true;
                    }
                }
            }
        
            return parent.Leaves.Count > 0;
        }
    }

    /// <summary>
    /// Node graph of actions
    /// </summary>
    public class Node
    {
        public Node Parent { get; }
        public AgentAction Action { get; }
        public HashSet<AgentBelief> RequiredEffects { get; }
        public List<Node> Leaves { get; }
        public float Cost { get; }
    
        public bool IsLeafDead => Leaves.Count == 0 && Action == null;

        public Node(Node parent, AgentAction action, HashSet<AgentBelief> effects, float cost)
        {
            Parent = parent;
            Action = action;
            RequiredEffects = new HashSet<AgentBelief>(effects);
            Leaves = new List<Node>();
            Cost = cost;
        }
    }

    /// <summary>
    /// Struct for plan storage
    /// </summary>
    [Serializable]
    public class ActionPlan
    {
        public AgentGoal AgentGoal { get; }
        public Stack<AgentAction> Actions { get; }
        public float TotalCost {get; set;}
    
        public ActionPlan(AgentGoal agentGoal,  Stack<AgentAction> actions,  float totalCost)
        {
            AgentGoal = agentGoal;
            Actions = actions;
            TotalCost = totalCost;
        }
    }
}