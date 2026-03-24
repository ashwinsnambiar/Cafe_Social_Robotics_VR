using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator), typeof(NavMeshAgent))]
public class TalkingOnPhonePacing : MonoBehaviour
{
    private Animator anim;
    private NavMeshAgent agent;
    [Tooltip("Multiply the root-motion step distance. Use values < 1 to shorten steps.")]
    public float stepMultiplier = 1f;

    void Start()
    {
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        anim.applyRootMotion = true;
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.Warp(transform.position);
    }

    void OnAnimatorMove()
    {
        if (agent == null || anim == null)
        {
            return;
        }

        // 1. Drive NavMeshAgent using animation delta so logical agent actually moves.
        Vector3 rawDelta = anim.deltaPosition;
        Vector3 delta = rawDelta * stepMultiplier;
        agent.Move(delta);

        // 2. Keep visual model glued to the moved logical agent.
        transform.position = agent.nextPosition;
        transform.rotation *= anim.deltaRotation;

        // 3. Drive internal velocity so other agents can avoid this NPC.
        if (Time.deltaTime > 0f)
        {
            agent.velocity = delta / Time.deltaTime;
        }
    }
}




