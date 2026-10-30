using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using XRMultiplayer;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCCompetitorAI : NetworkBehaviour
{
    private enum State
    {
        Roaming,
        Chasing,
        Fleeing,
        Eliminated
    }

    [SerializeField, Min(0.1f)] private float roamRadius = 12f;
    [SerializeField, Min(0.1f)] private float targetRefreshInterval = 0.75f;
    [SerializeField, Min(0.1f)] private float attackRange = 7f;
    [SerializeField, Min(0.1f)] private float moveSpeed = 2.5f;
    [SerializeField] private LayerMask obstacleMask = ~0;

    private NavMeshAgent agent;
    private Animator[] animators;
    private NPCCompetitor competitor;
    private MinigameManager minigameManager;
    private Vector3 arenaOrigin;
    private Transform target;
    private CompetitorMeltTarget targetMelt;
    private State state;
    private float nextTargetRefresh;
    private float nextRoamTime;
    private Vector3 roamDestination;
    private bool warnedNoNavMesh;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        agent = GetComponent<NavMeshAgent>();
        animators = GetComponentsInChildren<Animator>(true);
        competitor = GetComponent<NPCCompetitor>();
        minigameManager = FindFirstObjectByType<MinigameManager>();
        arenaOrigin = transform.position;

        if (!HasAuthority())
        {
            agent.enabled = false;
            return;
        }

        agent.speed = moveSpeed;
        agent.updateRotation = false;
        agent.stoppingDistance = attackRange * 0.8f;
        PickRoamDestination();
    }

    private void Update()
    {
        if (!HasAuthority() || agent == null || !agent.enabled)
        {
            return;
        }

        if (competitor != null && competitor.IsEliminated)
        {
            state = State.Eliminated;
            StopMoving();
            return;
        }

        int minigame = minigameManager != null ? minigameManager.currentMinigame.Value : 0;
        if (Time.time >= nextTargetRefresh)
        {
            target = FindNearestOpponent();
            targetMelt = target != null
                ? target.GetComponentInParent<CompetitorMeltTarget>()
                : null;
            nextTargetRefresh = Time.time + targetRefreshInterval;
        }

        switch (minigame)
        {
            case 1:
            case 2:
                state = target != null ? State.Chasing : State.Roaming;
                if (state == State.Chasing)
                {
                    MoveToward(target.position);
                    if (minigame == 1 && targetMelt != null &&
                        Vector3.Distance(transform.position, target.position) <= attackRange &&
                        HasLineOfSightTo(targetMelt))
                    {
                        targetMelt.ApplyNpcMelt();
                    }
                }
                else
                {
                    Roam();
                }
                break;

            case 3:
                state = target != null ? State.Fleeing : State.Roaming;
                if (state == State.Fleeing)
                {
                    Vector3 away = transform.position - target.position;
                    if (away.sqrMagnitude > 0.001f)
                    {
                        MoveToward(transform.position + away.normalized * roamRadius);
                    }
                }
                else
                {
                    Roam();
                }
                break;

            case 4:
                state = target != null ? State.Chasing : State.Roaming;
                if (state == State.Chasing)
                {
                    MoveToward(target.position);
                }
                else
                {
                    Roam();
                }
                break;

            default:
                state = State.Roaming;
                Roam();
                break;
        }

        UpdateFacing();
        UpdateWalkAnimation();
    }

    private Transform FindNearestOpponent()
    {
        Transform nearest = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (XRINetworkPlayer player in
                 FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None))
        {
            if (player == null || !player.gameObject.activeInHierarchy ||
                player.transform.position.y > GetEliminationHeightThreshold())
            {
                continue;
            }

            CompetitorMeltTarget playerMeltTarget =
                player.GetComponent<CompetitorMeltTarget>();
            if (playerMeltTarget != null && playerMeltTarget.IsEliminated)
            {
                continue;
            }

            float distance = (player.transform.position - transform.position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = player.transform;
                nearestDistance = distance;
            }
        }

        foreach (NPCCompetitor other in
                 FindObjectsByType<NPCCompetitor>(FindObjectsSortMode.None))
        {
            if (other == null || other == competitor || other.IsEliminated ||
                other.NetworkObject == null || !other.NetworkObject.IsSpawned ||
                other.transform.position.y > GetEliminationHeightThreshold())
            {
                continue;
            }

            float distance = (other.transform.position - transform.position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = other.transform;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void Roam()
    {
        if (Time.time >= nextRoamTime ||
            (agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance))
        {
            PickRoamDestination();
        }

        MoveToward(roamDestination);
    }

    private void PickRoamDestination()
    {
        Vector2 offset = Random.insideUnitCircle * roamRadius;
        roamDestination = arenaOrigin + new Vector3(offset.x, 0f, offset.y);
        nextRoamTime = Time.time + Random.Range(2f, 5f);
    }

    private void MoveToward(Vector3 destination)
    {
        Vector3 arenaOffset = destination - arenaOrigin;
        arenaOffset.y = 0f;
        if (arenaOffset.sqrMagnitude > roamRadius * roamRadius)
        {
            destination = arenaOrigin + arenaOffset.normalized * roamRadius;
        }

        destination.y = transform.position.y;

        if (agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(destination, out NavMeshHit navHit, 2f, agent.areaMask) &&
                agent.SetDestination(navHit.position))
            {
                return;
            }
        }

        if (!warnedNoNavMesh)
        {
            Debug.LogWarning(
                "NPC NavMeshAgent has no baked NavMesh; using obstacle-checked fallback steering.",
                this
            );
            warnedNoNavMesh = true;
        }

        Vector3 direction = destination - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f)
        {
            return;
        }

        direction.Normalize();
        float step = moveSpeed * Time.deltaTime;
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        if (Physics.SphereCast(
                origin,
                0.3f,
                direction,
                out RaycastHit hit,
                step + 0.3f,
                obstacleMask,
                QueryTriggerInteraction.Ignore))
        {
            Vector3 left = Vector3.Cross(Vector3.up, hit.normal).normalized;
            Vector3 right = -left;
            direction = Vector3.Dot(left, destination - transform.position) >
                        Vector3.Dot(right, destination - transform.position)
                ? left
                : right;

            if (Physics.SphereCast(
                    origin,
                    0.3f,
                    direction,
                    out _,
                    step + 0.3f,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore))
            {
                return;
            }
        }

        transform.position += direction * step;
    }

    private void UpdateFacing()
    {
        Vector3 direction = agent.isOnNavMesh ? agent.velocity : roamDestination - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    private void UpdateWalkAnimation()
    {
        float speed = agent.isOnNavMesh ? agent.velocity.magnitude : moveSpeed;
        foreach (Animator animator in animators)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Float &&
                    (parameter.name == "Speed" ||
                     parameter.name == "MoveSpeed" ||
                     parameter.name == "Velocity"))
                {
                    animator.SetFloat(parameter.name, speed);
                }
            }
        }
    }

    private bool HasLineOfSightTo(CompetitorMeltTarget targetCompetitor)
    {
        Vector3 origin = transform.position + Vector3.up * 1.2f;
        Vector3 targetPoint = targetCompetitor.transform.position + Vector3.up * 1.8f;
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;
        if (distance <= 0.01f)
        {
            return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(
                     origin,
                     direction / distance,
                     distance,
                     Physics.DefaultRaycastLayers,
                     QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            return hit.collider.GetComponentInParent<CompetitorMeltTarget>() ==
                   targetCompetitor;
        }

        return false;
    }

    private void StopMoving()
    {
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }

        agent.enabled = false;
        Collider bodyCollider = GetComponent<Collider>();
        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }
    }

    private bool HasAuthority()
    {
        return IsServer ||
               (NetworkManager != null &&
                NetworkManager.DistributedAuthorityMode &&
                NetworkManager.IsListening &&
                NetworkManager.LocalClient.IsSessionOwner);
    }

    private float GetEliminationHeightThreshold()
    {
        return minigameManager != null
            ? minigameManager.EliminationHeightThreshold
            : 100f;
    }
}
