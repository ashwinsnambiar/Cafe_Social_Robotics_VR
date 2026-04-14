using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public class RobotNavigator : MonoBehaviour
{
    [Header("Core")]
    public NavMeshAgent agent;
    public bool debug = false;

    [Header("Events")]
    public UnityEvent onArrived;

    public Transform currentLookTarget { get; private set; }
    public Vector3 Destination { get; private set; }
    public bool IsMoving => agent != null && (agent.pathPending || agent.remainingDistance > agent.stoppingDistance);

    Coroutine activeMove;

    void Reset()
    {
        // Try to auto-assign a NavMeshAgent when the component is added in the editor
        if (agent == null) agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            // Navigator takes care of rotation
            agent.updateRotation = false;
        }
    }

    public void MoveTo(Vector3 destination)
    {
        MoveTo(destination, null);
    }

    public void MoveTo(Vector3 destination, Transform lookTarget)
    {
        if (agent == null)
        {
            Debug.LogWarning("RobotNavigator: No NavMeshAgent assigned.");
            return;
        }

        Destination = destination;
        currentLookTarget = lookTarget;

        if (activeMove != null) StopCoroutine(activeMove);
        activeMove = StartCoroutine(MoveRoutine(destination, lookTarget));
    }

    public IEnumerator MoveToAsync(Vector3 destination, Transform lookTarget = null)
    {
        MoveTo(destination, lookTarget);
        while (activeMove != null)
            yield return null;
    }

    public void Cancel()
    {
        if (activeMove != null)
        {
            StopCoroutine(activeMove);
            activeMove = null;
        }

        if (agent != null && agent.hasPath)
            agent.ResetPath();
    }

    IEnumerator MoveRoutine(Vector3 destination, Transform lookTarget)
    {
        agent.SetDestination(destination);

        // wait for path to be computed
        yield return new WaitUntil(() => agent.pathPending == false);

        while (agent.remainingDistance > agent.stoppingDistance)
        {
            // rotate to face movement direction using desiredVelocity (stable)
            var desired = agent.desiredVelocity;
            var horiz = new Vector3(desired.x, 0f, desired.z);
            if (horiz.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(horiz.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, agent.angularSpeed * Time.deltaTime);
            }

            yield return null;
        }

        // if a look target is supplied, rotate to match it on arrival
        if (lookTarget != null)
        {
            float timeout = 1f;
            while (timeout > 0f && Quaternion.Angle(transform.rotation, lookTarget.rotation) > 0.5f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, lookTarget.rotation, agent.angularSpeed * Time.deltaTime);
                timeout -= Time.deltaTime;
                yield return null;
            }
        }

        activeMove = null;
        onArrived?.Invoke();

        if (debug)
            Debug.Log($"RobotNavigator: Arrived at {destination} (lookTarget={(lookTarget!=null?lookTarget.name:"null")}).");
    }
}
