using System;

namespace BehaviorTree
{
    // Leaf: SUCCESS if the check returns true, FAILURE otherwise.
    public class ConditionNode : Node
    {
        private readonly Func<bool> _check;

        public ConditionNode(Func<bool> check) : base()
        {
            _check = check;
        }

        public override NodeState Evaluate()
        {
            state = _check() ? NodeState.SUCCESS : NodeState.FAILURE;
            return state;
        }
    }
}
