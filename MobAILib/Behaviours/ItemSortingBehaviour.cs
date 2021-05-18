using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    class ItemSortingBehaviour : IBehaviour
    {
        private const string Prefix = "RR_ISB";
        private class State
        {
            public const string Main = Prefix + "Main";
            public const string SearchForRandomContainer = Prefix + "SearchForRandomContainer";
            public const string OpenContainer = Prefix + "OpenContainer";
            public const string AddContainerItemsToItemDictionary = Prefix + "OpenContainer";
            public const string SearchItemsOnGround = Prefix + "SearchItemsOnGround";
            public const string MoveToGroundItem = Prefix + "MoveToGroundItem";
            public const string PickUpItemFromGround = Prefix + "PickUpItemFromGround";
            public const string MoveToDumpContainer = Prefix + "MoveToDumpContainer";
            public const string MoveToContainer = Prefix + "MoveToContainer";
        }

        private class Trigger
        {
            public const string ItemFound = Prefix + "ItemFound";
            public const string ContainerFound = Prefix + "ContainerFound";
            public const string ContainerNotFound = Prefix + "ContainerNotFound";
            public const string ContainerIsClose = Prefix + "ContainerIsClose";
            public const string Failed = Prefix + "Failed";
            public const string ContainerOpened = Prefix + "ContainerOpened";
            public const string Timeout = Prefix + "Timeout";
            public const string GroundItemIsClose = Prefix + "GroundItemIsClose";
            public const string FoundGroundItem = Prefix + "FoundGroundItem";
        }

        // Input
        public string[] AcceptedContainerNames { get; set; }

        // Output

        // Settings
        public float MaxSearchTime { get; set; } = 60;
        public string StartState { get { return State.Main; } }
        public string SuccessState { get; set; }
        public string FailState { get; set; }

        private ItemDrop m_item;
        private Container m_container;
        private MobAIBase m_aiBase;
        private float m_openChestTimer;
        private float m_currentSearchTime;
        private int m_searchRadius;
        private MaxStack<Container> m_knownContainers;
        private Vector3 m_startPosition;

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_aiBase = aiBase;
            m_searchRadius = aiBase.Awareness * 5;

            brain.Configure(State.Main)
                .InitialTransition(State.SearchItemsOnGround)
                .SubstateOf(parentState)
                .PermitDynamic(Trigger.Failed, () => FailState)
                .OnEntry(t =>
                {
                    Common.Dbgl("Entered SearchForItemsBehaviour");
                    m_startPosition = aiBase.Character.transform.position;
                })
                .OnExit(t =>
                {
                });

            brain.Configure(State.SearchForRandomContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerFound, State.MoveToContainer)
                .OnEntry(t =>
                {
                    m_currentSearchTime = 0;
                });

            brain.Configure(State.MoveToContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerIsClose, State.OpenContainer)
                .Permit(Trigger.ContainerNotFound, State.SearchForRandomContainer)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus($"Heading to that a bin");
                    m_currentSearchTime = 0;
                });

            brain.Configure(State.OpenContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerOpened, State.SearchForItem)
                .Permit(Trigger.ContainerNotFound, State.SearchForRandomContainer)
                .OnEntry(t =>
                {
                    if (m_knownContainers.Peek() == null)
                    {
                        m_knownContainers.Pop();
                        brain.Fire(Trigger.ContainerNotFound);
                    }
                    else
                    {
                        m_knownContainers.Peek().SetInUse(inUse: true);
                        m_openChestTimer = 0f;
                    }
                });

        }

        public void Update(MobAIBase aiBase, float dt)
        {
            if (aiBase.Brain.IsInState(State.SearchForRandomContainer))
            {
                m_currentSearchTime += dt;
                if (m_currentSearchTime > MaxSearchTime)
                {
                    aiBase.Brain.Fire(Trigger.Failed);
                    return;
                }
                Container newContainer = Common.FindRandomNearbyContainer(aiBase.Instance, m_knownContainers, AcceptedContainerNames, m_searchRadius);
                if (newContainer != null)
                {
                    m_container = newContainer;
                    aiBase.Brain.Fire(Trigger.ContainerFound);
                }
                Common.Invoke<BaseAI>(aiBase.Instance, "RandomMovement", dt, m_startPosition);
                return;
            }

            if (aiBase.Brain.IsInState(State.MoveToContainer))
            {
                m_currentSearchTime += dt;
                if (m_currentSearchTime > MaxSearchTime)
                {
                    aiBase.Brain.Fire(Trigger.ContainerNotFound);
                    return;
                }
                //Common.Dbgl($"State MoveToContainer: {KnownContainers.Peek().name}");
                if (m_knownContainers.Peek() == null)
                {
                    aiBase.StopMoving();
                    m_knownContainers.Pop();
                    aiBase.Brain.Fire(Trigger.ContainerNotFound);
                    //Common.Dbgl("Container = null");
                    return;
                }
                aiBase.MoveAndAvoidFire(m_knownContainers.Peek().transform.position, dt, 0.5f);
                if (Vector3.Distance(aiBase.Instance.transform.position, m_knownContainers.Peek().transform.position) < 2)
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.ContainerIsClose);
                    //Debug.Log($"{KnownContainers.Peek().name} is close");
                }
                return;
            }
        }
    }
}
