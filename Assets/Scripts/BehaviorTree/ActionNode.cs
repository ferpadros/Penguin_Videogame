using System;

namespace BehaviorTree
{
    // Leaf: runs a callback. The callback can return its own state,
    // or be a plain void method (then the node always reports SUCCESS).
    public class ActionNode : Node
    {
        private readonly Func<NodeState> _run;

        public ActionNode(Func<NodeState> run) : base()
        {
            _run = run;
        }

        public ActionNode(System.Action run) : base()
        {
            _run = () => { run(); return NodeState.SUCCESS; };
        }

        public override NodeState Evaluate()
        {
            state = _run();
            return state;
        }
    }
}
