using System;
using UnityEngine;
using UnityEngine.AI;

namespace DeepSeaAI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(CapsuleCollider))]
    public sealed class DeepSeaStalkerController : MonoBehaviour
    {
        public enum StalkerState
        {
            Patrol,
            Investigate,
            Search,
            Chase,
            Attack,
            ReturnToPatrol
        }

        [SerializeField] private DeepSeaStalkerConfig config;
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerRespawnController playerRespawn;
        [Header("Sonar Diagnostics")]
        [Tooltip("Writes one Console message whenever this enemy directly receives a player or impact sonar pulse.")]
        [SerializeField] private bool logDirectSonarReception = true;

        private readonly RaycastHit[] sightHits = new RaycastHit[16];
        private NavMeshAgent agent;
        private Camera playerCamera;
        private StalkerState state;
        private int patrolIndex;
        private readonly System.Collections.Generic.List<int> patrolOrder = new();
        private int patrolOrderCursor;
        private int lastPatrolPoint = -1;
        private float stateTimer;
        private float nextSightCheck;
        private bool canSeePlayer;
        private bool hadSightLastCheck;
        private Vector3 lastSeenPosition;
        private Vector3 investigationOrigin;
        private Vector3 currentSearchPoint;
        private int searchPointsVisited;
        private float currentNoiseScore;
        private float lastNoiseTime = -100f;
        private Vector3 previousPosition;
        private bool configured;

        public StalkerState State => state;
        public bool CanSeePlayer => canSeePlayer;
        public Vector3 LastSeenPosition => lastSeenPosition;
        public bool HasNoise => Time.time - lastNoiseTime <= CurrentConfig.noiseMemorySeconds;
        public Vector3 LastNoisePosition => investigationOrigin;
        public float NoiseScore => currentNoiseScore;

        private DeepSeaStalkerConfig CurrentConfig
        {
            get
            {
                if (config == null)
                    config = DeepSeaStalkerConfig.CreateRuntimeDefaults();
                return config;
            }
        }

        public void Configure(
            DeepSeaStalkerConfig newConfig,
            Transform[] route,
            Transform player,
            PlayerRespawnController respawn,
            Animator modelAnimator)
        {
            config = newConfig != null ? newConfig : DeepSeaStalkerConfig.CreateRuntimeDefaults();
            patrolPoints = route;
            playerRoot = player;
            playerRespawn = respawn;
            animator = modelAnimator;
            configured = true;
            ResolvePlayerReferences();
            ConfigureAgent();
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            ConfigureAgent();
            previousPosition = transform.position;
            if (GetComponent<DeepSeaStalkerAlertIndicator>() == null)
                gameObject.AddComponent<DeepSeaStalkerAlertIndicator>();
        }

        private void OnEnable()
        {
            NoiseSystem.NoiseEmitted += OnNoise;
            // Listen to the actual pulse source as well as the generic noise bus.
            // This deliberately bypasses water-volume rendering and NoiseSystem
            // initialization order, so underwater sonar always reaches the AI.
            VolumetricFogPulseEmitter.PulseStarted += OnSonarPulseStarted;
            BindRespawn();
        }

        private void OnDisable()
        {
            NoiseSystem.NoiseEmitted -= OnNoise;
            VolumetricFogPulseEmitter.PulseStarted -= OnSonarPulseStarted;
            if (playerRespawn != null)
                playerRespawn.Respawned -= OnPlayerRespawned;
        }

        private void Start()
        {
            ResolvePlayerReferences();
            BindRespawn();
            if (!configured)
                configured = patrolPoints != null && patrolPoints.Length > 0;
            ResetToPatrol(false);
        }

        private void Update()
        {
            ResolvePlayerReferences();
            if (patrolPoints == null || patrolPoints.Length == 0)
                return;

            if (Time.time >= nextSightCheck)
            {
                nextSightCheck = Time.time + CurrentConfig.sightInterval;
                hadSightLastCheck = canSeePlayer;
                canSeePlayer = EvaluateSight();

                if (canSeePlayer)
                {
                    lastSeenPosition = PlayerBodyPosition();
                    if (state != StalkerState.Attack)
                        EnterChase();
                }
                else if (hadSightLastCheck && state == StalkerState.Chase)
                {
                    BeginInvestigation(lastSeenPosition, 0f);
                }
            }

            switch (state)
            {
                case StalkerState.Patrol:
                    TickPatrol();
                    break;
                case StalkerState.Investigate:
                    TickInvestigate();
                    break;
                case StalkerState.Search:
                    TickSearch();
                    break;
                case StalkerState.Chase:
                    TickChase();
                    break;
                case StalkerState.Attack:
                    TickAttack();
                    break;
                case StalkerState.ReturnToPatrol:
                    TickReturn();
                    break;
            }

            UpdateAnimator();
        }

        private void ConfigureAgent()
        {
            if (agent == null)
                agent = GetComponent<NavMeshAgent>();
            if (agent == null)
                return;

            agent.angularSpeed = 420f;
            agent.acceleration = 9f;
            agent.stoppingDistance = CurrentConfig.stoppingDistance;
            agent.autoBraking = true;
        }

        private void ResolvePlayerReferences()
        {
            if (playerRoot == null)
            {
                Unity.XR.CoreUtils.XROrigin origin =
                    FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (origin != null)
                {
                    playerRoot = origin.transform;
                    playerCamera = origin.Camera;
                }
            }

            if (playerCamera == null)
                playerCamera = Camera.main;

            if (playerRoot == null && playerCamera != null)
                playerRoot = playerCamera.transform.root;

            if (playerRespawn == null && playerRoot != null)
                playerRespawn = playerRoot.GetComponentInParent<PlayerRespawnController>();
        }

        private void BindRespawn()
        {
            if (playerRespawn == null)
                ResolvePlayerReferences();
            if (playerRespawn == null)
                return;

            playerRespawn.Respawned -= OnPlayerRespawned;
            playerRespawn.Respawned += OnPlayerRespawned;
        }

        private void OnNoise(NoiseStimulus noise)
        {
            if (!isActiveAndEnabled || state == StalkerState.Attack || state == StalkerState.Chase)
                return;
            if (noise.Source != null &&
                (noise.Source == transform ||
                 noise.Source.IsChildOf(transform) ||
                 transform.IsChildOf(noise.Source)))
            {
                return;
            }

            Vector3 ear = transform.position + Vector3.up * CurrentConfig.eyeHeight;
            float distance = Vector3.Distance(ear, noise.Position);
            // Sonar range is a per-enemy gameplay value. Impacts retain the
            // strength calculated from their collision speed.
            float effectiveRadius = noise.Kind == NoiseKind.Sonar
                ? CurrentConfig.sonarHearingRadius
                : noise.Radius;
            Vector3 direction = noise.Position - ear;
            if (CurrentConfig.soundsCanBeOccluded &&
                direction.sqrMagnitude > 0.01f &&
                Physics.Raycast(
                    ear,
                    direction.normalized,
                    out RaycastHit hit,
                    distance,
                    CurrentConfig.sightBlockers,
                    QueryTriggerInteraction.Ignore) &&
                (noise.Source == null ||
                 (hit.transform != noise.Source &&
                  !hit.transform.IsChildOf(noise.Source))))
            {
                effectiveRadius *= CurrentConfig.occludedRadiusMultiplier;
            }

            if (distance > effectiveRadius)
                return;

            float score = effectiveRadius / Mathf.Max(0.25f, distance);
            bool hasActiveNoise = state == StalkerState.Investigate || state == StalkerState.Search;
            if (hasActiveNoise &&
                score < currentNoiseScore * (1f + CurrentConfig.noiseRetargetAdvantage))
            {
                return;
            }

            lastNoiseTime = Time.time;
            BeginInvestigation(noise.Position, score);
        }

        private void OnSonarPulseStarted(VolumetricFogPulseEmitter.PulseState pulse)
        {
            if (!isActiveAndEnabled || state == StalkerState.Attack || state == StalkerState.Chase)
                return;

            Vector3 ear = transform.position + Vector3.up * CurrentConfig.eyeHeight;
            float distance = Vector3.Distance(ear, pulse.Origin);
            float effectiveRadius = CurrentConfig.sonarHearingRadius * Mathf.Clamp01(pulse.Strength);
            if (distance > effectiveRadius)
                return;

            // Do not use line-of-sight, water depth, or renderer visibility here.
            // Sonar is gameplay sound: an active pulse within range always creates
            // an investigation target, including while both objects are underwater.
            float score = effectiveRadius / Mathf.Max(0.25f, distance);
            bool hasActiveNoise = state == StalkerState.Investigate || state == StalkerState.Search;
            if (hasActiveNoise &&
                score < currentNoiseScore * (1f + CurrentConfig.noiseRetargetAdvantage))
            {
                return;
            }

            lastNoiseTime = Time.time;
            BeginInvestigation(pulse.Origin, score);

            if (logDirectSonarReception)
            {
                Debug.Log($"[DeepSeaAI] {name} received sonar at {pulse.Origin} (distance {distance:F1}m) and is investigating.", this);
            }
        }

        private bool EvaluateSight()
        {
            if (playerRoot == null)
                return false;

            Vector3 eye = transform.position + Vector3.up * CurrentConfig.eyeHeight;
            Vector3 target = PlayerBodyPosition();
            Vector3 toPlayer = target - eye;
            float distance = toPlayer.magnitude;
            if (distance > CurrentConfig.sightRange || distance < 0.001f)
                return false;

            Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 planarDirection = Vector3.ProjectOnPlane(toPlayer, Vector3.up).normalized;
            if (planarForward.sqrMagnitude < 0.001f)
                planarForward = transform.forward;
            if (Vector3.Angle(planarForward, planarDirection) > CurrentConfig.sightAngle * 0.5f)
                return false;

            if (!CurrentConfig.requireLineOfSight)
                return true;

            int count = Physics.RaycastNonAlloc(
                eye,
                toPlayer / distance,
                sightHits,
                distance,
                CurrentConfig.sightBlockers,
                QueryTriggerInteraction.Ignore);
            float closestBlocker = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider collider = sightHits[i].collider;
                if (collider == null || collider.transform.IsChildOf(transform))
                    continue;
                if (IsPlayerCollider(collider))
                    continue;
                closestBlocker = Mathf.Min(closestBlocker, sightHits[i].distance);
            }

            return float.IsPositiveInfinity(closestBlocker);
        }

        private bool IsPlayerCollider(Collider collider)
        {
            if (playerRoot == null || collider == null)
                return false;
            Transform target = collider.transform;
            return target == playerRoot ||
                   target.IsChildOf(playerRoot) ||
                   playerRoot.IsChildOf(target);
        }

        private Vector3 PlayerBodyPosition()
        {
            if (playerRoot != null)
                return playerRoot.position + Vector3.up * CurrentConfig.playerBodyOffset;
            if (playerCamera != null)
                return playerCamera.transform.position - Vector3.up * 0.35f;
            return transform.position;
        }

        private void TickPatrol()
        {
            EnsurePatrolOrder();
            Transform point = patrolPoints[Mathf.Clamp(patrolIndex, 0, patrolPoints.Length - 1)];
            if (!MoveTo(point.position, CurrentConfig.patrolSpeed))
                return;
            if (!HasArrived(point.position))
                return;

            stateTimer += Time.deltaTime;
            if (stateTimer < CurrentConfig.patrolWaitSeconds)
                return;

            stateTimer = 0f;
            lastPatrolPoint = patrolIndex;
            patrolOrderCursor++;
            if (patrolOrderCursor >= patrolOrder.Count)
                BuildPatrolOrder();
            patrolIndex = patrolOrder[Mathf.Clamp(patrolOrderCursor, 0, patrolOrder.Count - 1)];
        }

        private void BeginInvestigation(Vector3 position, float score)
        {
            investigationOrigin = ProjectToNavigationPlane(position);
            currentNoiseScore = Mathf.Max(0f, score);
            state = StalkerState.Investigate;
            stateTimer = 0f;
            searchPointsVisited = 0;
            SetAgentStopped(false);
        }

        private void TickInvestigate()
        {
            if (!MoveTo(investigationOrigin, InvestigationSpeed))
            {
                BeginSearch();
                return;
            }

            if (HasArrived(investigationOrigin))
                BeginSearch();
        }

        private void BeginSearch()
        {
            state = StalkerState.Search;
            stateTimer = 0f;
            searchPointsVisited = 0;
            ChooseNextSearchPoint();
        }

        private void TickSearch()
        {
            stateTimer += Time.deltaTime;
            if (stateTimer >= CurrentConfig.searchDuration ||
                searchPointsVisited >= CurrentConfig.searchPointCount)
            {
                EnterReturnToPatrol();
                return;
            }

            if (!MoveTo(currentSearchPoint, InvestigationSpeed) ||
                HasArrived(currentSearchPoint))
            {
                float pause = Mathf.Repeat(stateTimer, CurrentConfig.searchPointWait + 0.001f);
                if (pause <= Time.deltaTime + 0.001f)
                {
                    searchPointsVisited++;
                    ChooseNextSearchPoint();
                }
                else
                {
                    SetAgentStopped(true);
                    transform.Rotate(
                        Vector3.up,
                        CurrentConfig.searchTurnSpeed * Time.deltaTime,
                        Space.World);
                }
            }
        }

        private void ChooseNextSearchPoint()
        {
            SetAgentStopped(false);
            Vector2 circle = UnityEngine.Random.insideUnitCircle * CurrentConfig.searchRadius;
            Vector3 candidate = investigationOrigin + new Vector3(circle.x, 0f, circle.y);
            if (NavMesh.SamplePosition(
                    candidate,
                    out NavMeshHit hit,
                    CurrentConfig.searchRadius,
                    NavMesh.AllAreas))
            {
                currentSearchPoint = hit.position;
            }
            else
            {
                currentSearchPoint = candidate;
            }
        }

        private void EnterChase()
        {
            state = StalkerState.Chase;
            stateTimer = 0f;
            currentNoiseScore = 0f;
            SetAgentStopped(false);
        }

        private void TickChase()
        {
            Vector3 target = ProjectToNavigationPlane(PlayerBodyPosition());
            lastSeenPosition = target;
            MoveTo(target, CurrentConfig.chaseSpeed);

            float planarDistance = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(target.x, target.z));
            if (canSeePlayer && planarDistance <= CurrentConfig.killDistance)
                BeginAttack();
        }

        private void BeginAttack()
        {
            state = StalkerState.Attack;
            stateTimer = 0f;
            SetAgentStopped(true);
            if (animator != null)
                animator.SetTrigger("Attack");
        }

        private void TickAttack()
        {
            stateTimer += Time.deltaTime;
            Face(PlayerBodyPosition(), 720f);
            if (stateTimer < CurrentConfig.attackWindup)
                return;

            stateTimer = float.NegativeInfinity;
            if (playerRespawn != null)
                playerRespawn.Kill(transform);
            else
                ResetToPatrol(true);
        }

        private void EnterReturnToPatrol()
        {
            patrolIndex = FindNearestPatrolPoint();
            state = StalkerState.ReturnToPatrol;
            stateTimer = 0f;
            currentNoiseScore = 0f;
            SetAgentStopped(false);
        }

        private void TickReturn()
        {
            Vector3 target = patrolPoints[patrolIndex].position;
            if (!MoveTo(target, CurrentConfig.patrolSpeed))
            {
                state = StalkerState.Patrol;
                return;
            }

            if (HasArrived(target))
            {
                lastPatrolPoint = patrolIndex;
                BuildPatrolOrder();
                state = StalkerState.Patrol;
                stateTimer = 0f;
            }
        }

        private bool MoveTo(Vector3 target, float speed)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = speed;
                if (!agent.SetDestination(target))
                    return false;
                return agent.pathStatus != NavMeshPathStatus.PathInvalid;
            }

            Vector3 planarTarget = new Vector3(target.x, transform.position.y, target.z);
            Vector3 next = Vector3.MoveTowards(
                transform.position,
                planarTarget,
                speed * Time.deltaTime);
            Vector3 direction = next - transform.position;
            transform.position = next;
            if (direction.sqrMagnitude > 0.0001f)
                Face(next, 420f);
            return true;
        }

        private bool HasArrived(Vector3 target)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (agent.pathPending)
                    return false;
                return agent.remainingDistance <=
                    Mathf.Max(CurrentConfig.stoppingDistance, agent.stoppingDistance) + 0.05f;
            }

            Vector2 here = new Vector2(transform.position.x, transform.position.z);
            Vector2 there = new Vector2(target.x, target.z);
            return Vector2.Distance(here, there) <= CurrentConfig.stoppingDistance + 0.05f;
        }

        private Vector3 ProjectToNavigationPlane(Vector3 position)
        {
            // A sonar can originate at the player's underwater head height, while
            // the navmesh lives on the seabed. Use a generous vertical tolerance.
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 100f, NavMesh.AllAreas))
                return hit.position;
            return new Vector3(position.x, transform.position.y, position.z);
        }

        private int FindNearestPatrolPoint()
        {
            int nearest = 0;
            float best = float.PositiveInfinity;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null)
                    continue;
                float distance = (patrolPoints[i].position - transform.position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }
            return nearest;
        }

        private float InvestigationSpeed =>
            CurrentConfig.patrolSpeed * Mathf.Max(1f, CurrentConfig.investigateSpeedMultiplier);

        /// <summary>
        /// Every patrol round visits each configured point once, in a fresh
        /// random order. The first destination is never the point just visited,
        /// so the enemy cannot immediately turn around and repeat itself.
        /// </summary>
        private void EnsurePatrolOrder()
        {
            if (patrolOrder.Count == 0 || patrolOrderCursor >= patrolOrder.Count)
                BuildPatrolOrder();
            if (patrolOrder.Count > 0)
                patrolIndex = patrolOrder[patrolOrderCursor];
        }

        private void BuildPatrolOrder()
        {
            patrolOrder.Clear();
            if (patrolPoints == null)
                return;

            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] != null)
                    patrolOrder.Add(i);
            }

            for (int i = patrolOrder.Count - 1; i > 0; i--)
            {
                int other = UnityEngine.Random.Range(0, i + 1);
                (patrolOrder[i], patrolOrder[other]) = (patrolOrder[other], patrolOrder[i]);
            }

            if (patrolOrder.Count > 1 && patrolOrder[0] == lastPatrolPoint)
                (patrolOrder[0], patrolOrder[1]) = (patrolOrder[1], patrolOrder[0]);
            patrolOrderCursor = 0;
        }

        private void SetAgentStopped(bool stopped)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = stopped;
        }

        private void Face(Vector3 target, float degreesPerSecond)
        {
            Vector3 direction = Vector3.ProjectOnPlane(target - transform.position, Vector3.up);
            if (direction.sqrMagnitude < 0.0001f)
                return;
            Quaternion desired = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                desired,
                degreesPerSecond * Time.deltaTime);
        }

        private void UpdateAnimator()
        {
            Vector3 delta = transform.position - previousPosition;
            previousPosition = transform.position;
            float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                speed = agent.velocity.magnitude;

            if (animator == null)
                return;

            animator.SetFloat("Speed", speed, 0.12f, Time.deltaTime);
            animator.SetBool("IsSearching", state == StalkerState.Search);

            if (state == StalkerState.Attack || animator.IsInTransition(0))
                return;

            string expectedLoopState = speed > 0.05f
                ? "Base Layer.Walk"
                : "Base Layer.Idle";
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (!stateInfo.IsName(expectedLoopState) || stateInfo.normalizedTime < 0.99f)
                return;

            animator.Play(stateInfo.fullPathHash, 0, 0f);
            animator.Update(0f);
        }

        private void OnPlayerRespawned()
        {
            ResetToPatrol(true);
        }

        public void ResetToPatrol(bool teleportToStart)
        {
            canSeePlayer = false;
            hadSightLastCheck = false;
            currentNoiseScore = 0f;
            lastNoiseTime = -100f;
            lastPatrolPoint = -1;
            BuildPatrolOrder();
            patrolIndex = patrolOrder.Count > 0 ? patrolOrder[0] : 0;
            state = StalkerState.Patrol;
            stateTimer = 0f;

            if (teleportToStart && patrolPoints != null && patrolPoints.Length > 0 &&
                patrolPoints[Mathf.Clamp(patrolIndex, 0, patrolPoints.Length - 1)] != null)
            {
                Vector3 start = patrolPoints[patrolIndex].position;
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                    agent.Warp(start);
                else
                    transform.position = start;
            }

            SetAgentStopped(false);
        }

        private void OnDrawGizmosSelected()
        {
            DeepSeaStalkerConfig active = config;
            if (active == null)
                return;

            Vector3 eye = transform.position + Vector3.up * active.eyeHeight;
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.65f);
            Gizmos.DrawWireSphere(eye, active.sightRange);
            Vector3 left = Quaternion.AngleAxis(-active.sightAngle * 0.5f, Vector3.up) * transform.forward;
            Vector3 right = Quaternion.AngleAxis(active.sightAngle * 0.5f, Vector3.up) * transform.forward;
            Gizmos.DrawRay(eye, left * active.sightRange);
            Gizmos.DrawRay(eye, right * active.sightRange);
        }
    }
}
