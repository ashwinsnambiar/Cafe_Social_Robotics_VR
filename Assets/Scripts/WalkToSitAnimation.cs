using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class WalkToSitAnimation : MonoBehaviour
{
    public Transform waypoint_stand;
    public Transform waypoint_seat;
    public bool IsSitting = false;
    private NavMeshAgent agent;
    private Animator m_Animator;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        m_Animator = GetComponent<Animator>();
        if (waypoint_stand != null)
        {
            agent.SetDestination(waypoint_stand.position);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (agent.velocity.magnitude != 0f)
        {
            m_Animator.SetBool("Walking", true);
        }
        else
        {
            m_Animator.SetBool("Walking", false);
        }

        if (agent.enabled && !IsSitting && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            Sit();
        }
    }

    private void Sit()
    {
        agent.isStopped = true;
        agent.enabled = false;
        
        // Start a coroutine to move smoothly
        StartCoroutine(SmoothSlideToSeat(1.5f)); // 1.0 seconds duration
    }

    private IEnumerator SmoothSlideToSeat(float duration)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        // Trigger the animation first so they start the "transition" motion
        m_Animator.SetBool("Sitting", true);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / duration;

            // Smoothly interpolate position and rotation
            transform.position = Vector3.Lerp(startPos, waypoint_seat.position, percent);
            transform.rotation = Quaternion.Slerp(startRot, waypoint_seat.rotation, percent);
            
            yield return null; // Wait for next frame
        }

        // Ensure final position is exact
        transform.position = waypoint_seat.position;
        transform.rotation = waypoint_seat.rotation;
        IsSitting = true;
    }

    private void OnAnimatorMove()
    {
        if (m_Animator.GetBool("Walking"))
        {
            agent.speed = (m_Animator.deltaPosition / Time.deltaTime).magnitude;
        }
    }
}
