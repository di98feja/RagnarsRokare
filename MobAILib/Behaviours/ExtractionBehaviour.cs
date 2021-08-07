using Stateless;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public class ExtractionBehaviour : IBehaviour
    {
        private const string Prefix = "RR_EXTR";

        private class State
        {
            public const string Main = Prefix + "Main";
            public const string FindRandomTask = Prefix + "FindRandomTask";
            public const string MoveToTask = Prefix + "MoveToTask";
            public const string Extracting = Prefix + "Extracting";
        }
        private class Trigger
        {
            public const string ItemFound = Prefix + "ItemFound";
            public const string Failed = Prefix + "Failed";
            public const string TaskFound = Prefix + "TaskFound";
            public const string TaskIsClose = Prefix + "TaskIsClose";
            public const string TaskNotFound = Prefix + "TaskNotFound";
            public const string ExtractionSucceeded = Prefix + "ExtractionSucceeded";
            public const string ExtractionFailed = Prefix + "ExtractionFailed";
        }

        // Settings
        public float MaxSearchTime { get; set; } = 60f;
        public string StartState => State.Main;
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public AssignmentType[] AcceptedAssignments { get; set; }
        public float CloseEnoughTimeout { get; private set; } = 30;

        // Timers
        private float m_currentSearchTimeLimit;
        private float m_closeEnoughTimer;

        private MobAIBase m_aiBase;
        private int m_searchRadius;
        private LinkedList<Assignment> m_taskList;

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_aiBase = aiBase;
            m_searchRadius = aiBase.Awareness * 5;
            m_taskList = new LinkedList<Assignment>();

            brain.Configure(State.Main)
                .InitialTransition(State.FindRandomTask)
                .SubstateOf(parentState)
                .PermitDynamic(Trigger.Failed, () => FailState)
                .OnEntry(t =>
                {
                    Common.Dbgl("Entered ExtractionBehaviour", "Extraction");
                });

            brain.Configure(State.FindRandomTask)
                .SubstateOf(State.Main)
                .Permit(Trigger.TaskFound, State.MoveToTask)
                .OnEntry(t =>
                {
                    Common.Dbgl("Entered SearchForTask", "Extraction");
                    m_currentSearchTimeLimit = 0f;
                });

            brain.Configure(State.MoveToTask)
                .SubstateOf(State.Main)
                .Permit(Trigger.TaskIsClose, State.Extracting)
                .Permit(Trigger.TaskNotFound, State.FindRandomTask)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus(State.MoveToTask);
                    m_currentSearchTimeLimit = Time.time + MaxSearchTime;
                });

            brain.Configure(State.Extracting)
                .SubstateOf(State.Main)
                .Permit(Trigger.ExtractionSucceeded, SuccessState)
                .Permit(Trigger.ExtractionFailed, State.FindRandomTask)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus(State.Extracting, m_taskList.First.ToString());
                });
        }

        public void Update(MobAIBase aiBase, float dt)
        {
            if (aiBase.Brain.IsInState(State.FindRandomTask))
            {
                bool noAssignmentFound = !StartNewAssignment(aiBase, ref m_taskList, AcceptedAssignments);
                if (noAssignmentFound)
                {
                    Common.Dbgl($"{aiBase.Character.GetHoverName()}:No suitable extraction assignments found", "Extraction");
                    aiBase.Brain.Fire(Trigger.Failed);
                    return;
                }
                aiBase.Brain.Fire(Trigger.TaskFound);
                return;
            }

            if (aiBase.Brain.IsInState(State.MoveToTask))
            {
                bool isCloseToTask = MoveToAssignment(aiBase, dt);
                if (isCloseToTask)
                {
                    Common.Dbgl($"{aiBase.Character.GetHoverName()}:Reached task position", "Extraction");
                    aiBase.Brain.Fire(Trigger.TaskIsClose);
                }
                if (Time.time > m_currentSearchTimeLimit)
                {
                    Common.Dbgl($"{aiBase.Character.GetHoverName()}:Failed to reach task in time", "Extraction");
                    m_taskList.First().AssignmentTimeout = Time.time + m_taskList.First().TypeOfAssignment.TimeBeforeAssignmentCanBeRepeated;
                    aiBase.Brain.Fire(Trigger.TaskNotFound);
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.Extracting))
            {
                bool successful = PerformExtraction(aiBase, m_taskList.First());
                if (successful)
                {
                    Common.Dbgl($"{aiBase.Character.GetHoverName()}: Extraction successful", "Extraction");
                    aiBase.Brain.Fire(Trigger.ExtractionSucceeded);
                }
                else
                {
                    Common.Dbgl($"{aiBase.Character.GetHoverName()}: Extraction failed", "Extraction");
                    aiBase.Brain.Fire(Trigger.ExtractionFailed);
                }
                return;
            }

        }

        private bool PerformExtraction(MobAIBase aiBase, Assignment assignment)
        {
            Interactable extObject = assignment.AssignmentObject.GetComponent(assignment.TypeOfAssignment.ComponentType) as Interactable;
            if (null == extObject) return false;
            assignment.AssignmentTimeout = Time.time + assignment.TypeOfAssignment.TimeBeforeAssignmentCanBeRepeated;
            return extObject.Interact(aiBase.Character as Humanoid, false);
        }

        public bool StartNewAssignment(MobAIBase aiBase, ref LinkedList<Assignment> KnownAssignments, AssignmentType[] acceptedAssignmentTypes)
        {
            var newassignment = Common.FindRandomNearbyAssignment(aiBase.Instance, aiBase.m_trainedAssignments, KnownAssignments, m_searchRadius, acceptedAssignmentTypes);
            if (newassignment != null && newassignment.IsExtractable())
            {
                Common.Dbgl($"{aiBase.Character.GetHoverName()}:Found new assignment:{newassignment.TypeOfAssignment.Name}", "Extraction");
                KnownAssignments.AddFirst(newassignment);
                return true;
            }
            else if (KnownAssignments.Any())
            {
                KnownAssignments.OrderBy(a => a.AssignmentTimeout);
                if (KnownAssignments.First().AssignmentTimeout <= Time.time)
                {
                    Common.Dbgl($"{aiBase.Character.GetHoverName()}:No new assignment found, checking old one:{KnownAssignments.First().TypeOfAssignment.Name}", "Extraction");
                    return true;
                }
            }
            return false;
        }

        public bool MoveToAssignment(MobAIBase aiBase, float dt)
        {
            if (!m_taskList.Any())
            {
                Common.Dbgl($"{aiBase.Character.GetHoverName()}:No assignments to move to", "Extraction");
                aiBase.Brain.Fire(Trigger.TaskNotFound);
                return true;
            }
            if (!(bool)m_taskList.First().AssignmentObject)
            {
                Common.Dbgl($"{aiBase.Character.GetHoverName()}:AssignmentObject is null", "Extraction");
                m_taskList.RemoveFirst();
                aiBase.Brain.Fire(Trigger.TaskNotFound);
                return true;
            }
            bool assignmentIsInvalid = m_taskList.First().AssignmentObject?.GetComponent<ZNetView>()?.IsValid() == false;
            if (assignmentIsInvalid)
            {
                Common.Dbgl($"{aiBase.Character.GetHoverName()}:AssignmentObject is invalid", "Extraction");
                m_taskList.RemoveFirst();
                aiBase.Brain.Fire(Trigger.TaskNotFound);
                return true;
            }
            float distance = (m_closeEnoughTimer += dt) > CloseEnoughTimeout ? m_taskList.First().TypeOfAssignment.InteractDist : m_taskList.First().TypeOfAssignment.InteractDist + 1;
            return aiBase.MoveAndAvoidFire(m_taskList.First().Position, dt, distance);
        }

    }
}
