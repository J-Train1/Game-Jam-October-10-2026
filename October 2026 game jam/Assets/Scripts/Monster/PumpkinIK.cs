using UnityEngine;

// Sits on the pumpkin's Animator object and forwards the IK pass to PumpkinMonster
// (OnAnimatorIK only fires on the GameObject that has the Animator). Added automatically at runtime.
[RequireComponent(typeof(Animator))]
public class PumpkinIK : MonoBehaviour
{
    [HideInInspector] public PumpkinMonster owner;
    Animator anim;

    void Awake() => anim = GetComponent<Animator>();

    void OnAnimatorIK(int layerIndex)
    {
        if (owner != null && anim != null) owner.ApplyIK(anim);
    }
}
