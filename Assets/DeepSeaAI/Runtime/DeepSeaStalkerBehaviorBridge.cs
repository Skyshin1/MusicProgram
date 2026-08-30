using System;
using System.Collections;
using System.Reflection;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

namespace DeepSeaAI
{
    [DisallowMultipleComponent]
    public sealed class DeepSeaStalkerBehaviorBridge : MonoBehaviour
    {
        private BehaviorGraphAgent behaviorAgent;
        private DeepSeaStalkerController controller;
        private GameObject player;
        private GameObject patrolPoints;
        private float nextBlackboardPushTime;
        private const float BlackboardPushInterval = 0.2f;

        public void Configure(
            BehaviorGraphAgent agent,
            DeepSeaStalkerController stalker,
            GameObject playerObject,
            GameObject patrolRoute)
        {
            behaviorAgent = agent;
            controller = stalker;
            player = playerObject;
            patrolPoints = patrolRoute;
            PushBlackboard();
        }

        private void Awake()
        {
            if (behaviorAgent == null)
                behaviorAgent = GetComponent<BehaviorGraphAgent>();
            if (controller == null)
                controller = GetComponent<DeepSeaStalkerController>();
            RepairBehaviorGraphCollections();
        }

        private void OnDisable()
        {
            RepairBehaviorGraphCollections();
        }

        private void RepairBehaviorGraphCollections()
        {
            if (behaviorAgent == null || behaviorAgent.Graph == null)
                return;

            FieldInfo graphsField = typeof(BehaviorGraph).GetField(
                "Graphs",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (graphsField?.GetValue(behaviorAgent.Graph) is not IEnumerable modules)
                return;

            string[] collectionFields =
            {
                "m_ActiveNodes",
                "m_NodesToTick",
                "m_NodesToEnd",
                "m_EndedNodes"
            };
            foreach (object module in modules)
            {
                if (module == null)
                    continue;
                Type moduleType = module.GetType();
                foreach (string fieldName in collectionFields)
                {
                    FieldInfo field = moduleType.GetField(
                        fieldName,
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null && field.GetValue(module) == null)
                        field.SetValue(module, Activator.CreateInstance(field.FieldType));
                }
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextBlackboardPushTime)
                return;

            nextBlackboardPushTime = Time.unscaledTime + BlackboardPushInterval;
            PushBlackboard();
        }

        private void PushBlackboard()
        {
            if (behaviorAgent == null || controller == null || behaviorAgent.Graph == null)
                return;

            behaviorAgent.SetVariableValue("Self", gameObject);
            behaviorAgent.SetVariableValue("Player", player);
            behaviorAgent.SetVariableValue("PatrolPoints", patrolPoints);
            behaviorAgent.SetVariableValue("CanSeePlayer", controller.CanSeePlayer);
            behaviorAgent.SetVariableValue("LastSeenPosition", controller.LastSeenPosition);
            behaviorAgent.SetVariableValue("HasNoise", controller.HasNoise);
            behaviorAgent.SetVariableValue("LastNoisePosition", controller.LastNoisePosition);
            behaviorAgent.SetVariableValue("NoiseScore", controller.NoiseScore);
            behaviorAgent.SetVariableValue("ActiveState", controller.State.ToString());
        }
    }

    internal static class DeepSeaBehaviorBranchUtility
    {
        internal static DeepSeaStalkerController Resolve(BlackboardVariable<GameObject> agent)
        {
            return agent != null && agent.Value != null
                ? agent.Value.GetComponent<DeepSeaStalkerController>()
                : null;
        }
    }

    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Chase Player Priority",
        story: "If the stalker sees the player, run chase and attack as highest priority",
        category: "Deep Sea AI",
        id: "7f6cfc74078d4b2aa4de4791f199105a")]
    public partial class DeepSeaChasePriorityAction : Unity.Behavior.Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        protected override Status OnStart()
        {
            return Evaluate();
        }

        protected override Status OnUpdate()
        {
            return Evaluate();
        }

        private Status Evaluate()
        {
            DeepSeaStalkerController stalker = DeepSeaBehaviorBranchUtility.Resolve(Agent);
            if (stalker == null)
                return Status.Failure;
            return stalker.State == DeepSeaStalkerController.StalkerState.Chase ||
                   stalker.State == DeepSeaStalkerController.StalkerState.Attack
                ? Status.Running
                : Status.Failure;
        }
    }

    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Investigate Sound Priority",
        story: "If the stalker heard a sound, investigate, search, then return to route",
        category: "Deep Sea AI",
        id: "e2a3dd394a9e4c17af746e0713349e2c")]
    public partial class DeepSeaInvestigatePriorityAction : Unity.Behavior.Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        protected override Status OnStart()
        {
            return Evaluate();
        }

        protected override Status OnUpdate()
        {
            return Evaluate();
        }

        private Status Evaluate()
        {
            DeepSeaStalkerController stalker = DeepSeaBehaviorBranchUtility.Resolve(Agent);
            if (stalker == null)
                return Status.Failure;
            return stalker.State == DeepSeaStalkerController.StalkerState.Investigate ||
                   stalker.State == DeepSeaStalkerController.StalkerState.Search ||
                   stalker.State == DeepSeaStalkerController.StalkerState.ReturnToPatrol
                ? Status.Running
                : Status.Failure;
        }
    }

    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Fixed Route Patrol",
        story: "Otherwise patrol a newly shuffled route of the configured patrol points",
        category: "Deep Sea AI",
        id: "d689408ead084e15b7869edcc52452b0")]
    public partial class DeepSeaPatrolPriorityAction : Unity.Behavior.Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        protected override Status OnStart()
        {
            return Evaluate();
        }

        protected override Status OnUpdate()
        {
            return Evaluate();
        }

        private Status Evaluate()
        {
            DeepSeaStalkerController stalker = DeepSeaBehaviorBranchUtility.Resolve(Agent);
            return stalker != null &&
                   stalker.State == DeepSeaStalkerController.StalkerState.Patrol
                ? Status.Running
                : Status.Failure;
        }
    }
}
