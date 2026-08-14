using UnityEngine;

public class AnimStateMachine : StateMachineBehaviour
{
    public int index = 0;
    [Header("是否使用根节点位移")]
    public bool useCustomPositionMotion = true;
    [Header("是否使用根节点旋转")]
    public bool useCustomRotationMotion = true;
    [Header("是否根节点")]
    public bool useMotion = true;

    private CharacterController _controller;
    private Vector3 _deltaPosition;
    private Quaternion _deltaRotation;
    private Transform _root;

    // OnStateEnter is called before OnStateEnter is called on any state inside this state machine
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        _root = animator.transform.parent;
        _controller = _root.GetComponent<CharacterController>();
        animator.applyRootMotion = useMotion;
    }

    // OnStateUpdate is called before OnStateUpdate is called on any state inside this state machine
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {

    }

    // OnStateExit is called before OnStateExit is called on any state inside this state machine
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {

    }

    // OnStateMove is called before OnStateMove is called on any state inside this state machine
    override public void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (useCustomPositionMotion == true && _controller != null && _controller.enabled)
        {
            _deltaPosition = animator.deltaPosition;
            _deltaPosition.y = 0;
            _controller.Move(_deltaPosition);
        }

        if (useCustomRotationMotion == true )
        {
            _deltaRotation = animator.deltaRotation;
            _root.rotation = _deltaRotation;
        }
    }

    // OnStateIK is called before OnStateIK is called on any state inside this state machine
    //override public void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    
    //}

    // OnStateMachineEnter is called when entering a state machine via its Entry Node
    override public void OnStateMachineEnter(Animator animator, int stateMachinePathHash)
    {
        Debug.Log($"yns  OnStateMachineEnter");
    }

    // OnStateMachineExit is called when exiting a state machine via its Exit Node
    override public void OnStateMachineExit(Animator animator, int stateMachinePathHash)
    {
        Debug.Log($"yns OnStateMachineExit ");
    }
}
