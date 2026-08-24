using EGamePlay.Unity;
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
        // Token 驱动的 RootMotionDriver 接管时，不再由 SMB 改 applyRootMotion
        if (IsDrivenByRootMotionDriver(animator))
            return;

        _root = animator.transform.parent;
        _controller = _root != null ? _root.GetComponent<CharacterController>() : null;
        animator.applyRootMotion = useMotion;
    }

    // OnStateMove is called before OnStateMove is called on any state inside this state machine
    override public void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 有 Driver 时一律交给 Token 门闩，避免与 OnAnimatorMove 双推
        if (IsDrivenByRootMotionDriver(animator))
            return;

        if (_root == null)
        {
            _root = animator.transform.parent;
            _controller = _root != null ? _root.GetComponent<CharacterController>() : null;
        }

        if (useCustomPositionMotion && _controller != null && _controller.enabled)
        {
            _deltaPosition = animator.deltaPosition;
            _deltaPosition.y = 0;
            _controller.Move(_deltaPosition);
        }

        if (useCustomRotationMotion)
        {
            _deltaRotation = animator.deltaRotation;
            if (_root != null)
                _root.rotation = _deltaRotation * _root.rotation;
        }
    }

    static bool IsDrivenByRootMotionDriver(Animator animator)
    {
        var driver = animator.GetComponent<RootMotionDriver>();
        return driver != null && driver.ConsumesRootMotion;
    }
}
