using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;
using RoadSafety.Player;
using RoadSafety.World;

// Behavior Designer tasks of every AI of the game (Tasks > Custom > ... in the Behavior Designer editor).
// Conditionals answer a question, Actions do one step of work; the Actions placed under a
// Utility Selector return their utility function (fonction d'utilité) in GetUtility().
// Every score goes through UtilityAI.Score and every action step through UtilityAI.Act:
// one central utility function for the whole game (US 5.3).
// The trees are ordinary Behavior Designer trees saved on each object (RoadSafety > AI >
// Attach Behavior Designer trees) and edited in Tools > Behavior Designer > Editor.
// Each tree is reactive: it runs once per tick and starts again (Restart When Complete),
// so higher branches (danger, red light...) are checked first every tick.
namespace RoadSafety.AI.BehaviorDesignerTasks
{
    internal static class Status
    {
        public static TaskStatus Of(bool ok) => ok ? TaskStatus.Success : TaskStatus.Failure;
        public static TaskStatus Of(NodeState s) => s == NodeState.Failure ? TaskStatus.Failure : TaskStatus.Success;
    }

    public abstract class AgentConditional<T> : Conditional where T : Component
    {
        protected T agent;
        public override void OnAwake() => agent = GetComponent<T>();
    }

    public abstract class AgentAction<T> : Action where T : Component
    {
        protected T agent;
        public override void OnAwake() => agent = GetComponent<T>();

        /// <summary>Runs the action step through the central utility gateway (US 5.3).</summary>
        protected TaskStatus Do(NodeState result) => Status.Of(UtilityAI.Act(agent, FriendlyName, result));

        /// <summary>Same, for actions that answer yes / no.</summary>
        protected TaskStatus Do(bool ok) => Status.Of(UtilityAI.Act(agent, FriendlyName, ok));

        /// <summary>Scores this option with the central utility function (US 5.3).</summary>
        protected float Utility(float raw) => UtilityAI.Score(agent, FriendlyName, raw);
    }

    // ===================================================================== traffic light

    [TaskCategory("Custom/Traffic light")]
    [TaskDescription("Success if the light is in this phase (0 main green, 1 main amber, 2 all red, 3 side green, 4 side amber, 5 all red).")]
    public class LightPhaseIs : AgentConditional<TrafficLight>
    {
        public SharedInt phase = 0;
        public override TaskStatus OnUpdate() => Status.Of(agent != null && agent.Phase == phase.Value);
    }

    [TaskCategory("Custom/Traffic light")]
    [TaskDescription("Keeps the colours of this phase; when its time is up, moves to the next phase.")]
    public class HoldPhase : AgentAction<TrafficLight>
    {
        public SharedInt phase = 0;
        public override TaskStatus OnUpdate() => Do(agent.Hold(phase.Value));
    }

    // ===================================================================== other cars

    [TaskCategory("Custom/Other cars")]
    [TaskDescription("Something in front (car, child, ball)?")]
    public class ObstacleAhead : AgentConditional<NpcCar>
    {
        public override TaskStatus OnUpdate() => Status.Of(agent.ObstacleAhead());
    }

    [TaskCategory("Custom/Other cars")]
    [TaskDescription("Red light or pedestrians at a stop line ahead?")]
    public class BlockedLineAhead : AgentConditional<NpcCar>
    {
        public override TaskStatus OnUpdate() => Status.Of(agent.BlockedLineAhead());
    }

    [TaskCategory("Custom/Other cars")]
    [TaskDescription("Brakes behind the obstacle.")]
    public class BrakeBehindObstacle : AgentAction<NpcCar>
    {
        public override TaskStatus OnUpdate() => Do(agent.Brake());
    }

    [TaskCategory("Custom/Other cars")]
    [TaskDescription("Stops at the stop line.")]
    public class StopAtTheLine : AgentAction<NpcCar>
    {
        public override TaskStatus OnUpdate() => Do(agent.StopAtLine());
    }

    [TaskCategory("Custom/Other cars")]
    [TaskDescription("Utility: constant. Drives at cruise speed.")]
    public class Cruise : AgentAction<NpcCar>
    {
        public SharedFloat utility = 0.6f;
        public override float GetUtility() => Utility(utility.Value);
        public override TaskStatus OnUpdate() => Do(agent.Cruise());
    }

    [TaskCategory("Custom/Other cars")]
    [TaskDescription("Utility: grows from 0 to 1 when a crossing or a light is 25 m → 0 m ahead. Slows down.")]
    public class SlowDown : AgentAction<NpcCar>
    {
        public override float GetUtility() => Utility(agent != null ? agent.SlowDownUtility() : 0f);
        public override TaskStatus OnUpdate() => Do(agent.SlowDown());
    }

    // ===================================================================== school kid (zebra crossing)

    [TaskCategory("Custom/School kid")]
    [TaskDescription("Success if the kid is in this state.")]
    public class KidStateIs : AgentConditional<Pedestrian>
    {
        public PedestrianState state = PedestrianState.Crossing;
        public override TaskStatus OnUpdate() => Status.Of(agent.InState(state));
    }

    [TaskCategory("Custom/School kid")]
    [TaskDescription("Crossing and a car rushes at the crossing (less than half way)?")]
    public class CarRushesWhileCrossing : AgentConditional<Pedestrian>
    {
        public override TaskStatus OnUpdate() => Status.Of(agent.CarRushesWhileCrossing());
    }

    [TaskCategory("Custom/School kid")]
    [TaskDescription("Waited long enough and every car near the crossing has stopped?")]
    public class ReadyToCross : AgentConditional<Pedestrian>
    {
        public override TaskStatus OnUpdate() => Status.Of(agent.ReadyToCross());
    }

    [TaskCategory("Custom/School kid")]
    [TaskDescription("Reached the other side (thanking or leaving)?")]
    public class HasCrossed : AgentConditional<Pedestrian>
    {
        public override TaskStatus OnUpdate() => Status.Of(agent.HasCrossed());
    }

    [TaskCategory("Custom/School kid")]
    [TaskDescription("Starts jumping back to the kerb.")]
    public class StartSteppingBack : AgentAction<Pedestrian>
    {
        public override TaskStatus OnUpdate() => Do(agent.BeginStepBack());
    }

    [TaskCategory("Custom/School kid")]
    [TaskDescription("Runs back to the kerb.")]
    public class RunBackToKerb : AgentAction<Pedestrian>
    {
        public override TaskStatus OnUpdate() => Do(agent.StepBack());
    }

    [TaskCategory("Custom/School kid")]
    [TaskDescription("Walks across the zebra crossing.")]
    public class CrossTheRoad : AgentAction<Pedestrian>
    {
        public override TaskStatus OnUpdate() => Do(agent.Cross());
    }

    [TaskCategory("Custom/School kid")]
    [TaskDescription("Starts crossing.")]
    public class StartCrossing : AgentAction<Pedestrian>
    {
        public override TaskStatus OnUpdate() => Do(agent.BeginCrossing());
    }

    [TaskCategory("Custom/School kid")]
    [TaskDescription("Walks to the kerb.")]
    public class WalkToTheKerb : AgentAction<Pedestrian>
    {
        public override TaskStatus OnUpdate() => Do(agent.WalkToKerb());
    }

    [TaskCategory("Custom/School kid")]
    [TaskDescription("Waves \"thank you\" to the driver, then walks to the school gate.")]
    public class ThankAndLeave : AgentAction<Pedestrian>
    {
        public override TaskStatus OnUpdate() => Do(agent.ThankAndLeave());
    }

    [TaskCategory("Custom/School kid")]
    [TaskDescription("Waits on the kerb with a hand up.")]
    public class WaitHandUp : AgentAction<Pedestrian>
    {
        public override TaskStatus OnUpdate() => Do(agent.Wait());
    }

    // ===================================================================== townspeople

    [TaskCategory("Custom/Townspeople")]
    [TaskDescription("The player's car honked nearby a moment ago?")]
    public class HornHeard : AgentConditional<Townsperson>
    {
        public override TaskStatus OnUpdate() => Status.Of(agent.HornHeard());
    }

    [TaskCategory("Custom/Townspeople")]
    [TaskDescription("Has a chair (cafe)?")]
    public class IsSeated : AgentConditional<Townsperson>
    {
        public override TaskStatus OnUpdate() => Status.Of(agent.IsSeated());
    }

    [TaskCategory("Custom/Townspeople")]
    [TaskDescription("Stands in a group of friends?")]
    public class IsGroupTalker : AgentConditional<Townsperson>
    {
        public override TaskStatus OnUpdate() => Status.Of(agent.IsGroupTalker());
    }

    [TaskCategory("Custom/Townspeople")]
    [TaskDescription("Is chatting with a neighbour?")]
    public class HasChatPartner : AgentConditional<Townsperson>
    {
        public override TaskStatus OnUpdate() => Status.Of(agent.HasPartner());
    }

    [TaskCategory("Custom/Townspeople")]
    [TaskDescription("Waves at the car.")]
    public class WaveAtTheCar : AgentAction<Townsperson>
    {
        public override TaskStatus OnUpdate() => Do(agent.Wave());
    }

    [TaskCategory("Custom/Townspeople")]
    [TaskDescription("Sits, talks and drinks tea.")]
    public class SitTalkDrinkTea : AgentAction<Townsperson>
    {
        public override TaskStatus OnUpdate() => Do(agent.SitAndTalk());
    }

    [TaskCategory("Custom/Townspeople")]
    [TaskDescription("Talks in turn with the group.")]
    public class TalkInTurn : AgentAction<Townsperson>
    {
        public override TaskStatus OnUpdate() => Do(agent.GroupTalk());
    }

    [TaskCategory("Custom/Townspeople")]
    [TaskDescription("Chats with the neighbour.")]
    public class ChatWithNeighbour : AgentAction<Townsperson>
    {
        public override TaskStatus OnUpdate() => Do(agent.Chat());
    }

    [TaskCategory("Custom/Townspeople")]
    [TaskDescription("Utility: constant. Walks on along the pavement.")]
    public class WalkOn : AgentAction<Townsperson>
    {
        public SharedFloat utility = 0.35f;
        public override float GetUtility() => Utility(utility.Value);
        public override TaskStatus OnUpdate() => Do(agent.Walk());
    }

    [TaskCategory("Custom/Townspeople")]
    [TaskDescription("Utility: how sociable they are, if a neighbour is near. Greets the neighbour.")]
    public class GreetNeighbour : AgentAction<Townsperson>
    {
        public override float GetUtility() => Utility(agent != null ? agent.GreetUtility() : 0f);
        public override TaskStatus OnUpdate() => Do(agent.StartChat());
    }

    [TaskCategory("Custom/Townspeople")]
    [TaskDescription("Utility: how tired they are (1 while resting). Rests a moment.")]
    public class RestAMoment : AgentAction<Townsperson>
    {
        public override float GetUtility() => Utility(agent != null ? agent.RestUtility() : 0f);
        public override TaskStatus OnUpdate() => Do(agent.Rest());
    }

    // ===================================================================== player's actions (HUD hint)

    [TaskCategory("Custom/Player")]
    [TaskDescription("The car's health is low?")]
    public class HealthLow : AgentConditional<PlayerActions>
    {
        public override TaskStatus OnUpdate() => Status.Of(agent.HealthLow());
    }

    [TaskCategory("Custom/Player")]
    [TaskDescription("The driver's stamina is low?")]
    public class StaminaLow : AgentConditional<PlayerActions>
    {
        public override TaskStatus OnUpdate() => Status.Of(agent.StaminaLow());
    }

    [TaskCategory("Custom/Player")]
    [TaskDescription("Repairs the car.")]
    public class RepairCar : AgentAction<PlayerActions>
    {
        public override TaskStatus OnUpdate() => Do(agent.Repair());
    }

    [TaskCategory("Custom/Player")]
    [TaskDescription("Rests.")]
    public class RestDriver : AgentAction<PlayerActions>
    {
        public override TaskStatus OnUpdate() => Do(agent.Rest());
    }

    public abstract class PlayerUtilityAction : AgentAction<PlayerActions>
    {
        protected abstract float Score();
        public override float GetUtility() => Utility(agent != null ? Score() : 0f);
        public override void OnStart()
        {
            if (agent != null) agent.NoteChoice(FriendlyName, GetUtility());
        }
    }

    [TaskCategory("Custom/Player")]
    [TaskDescription("Utility: seatbelt off = 1 when stopped, 0.7 while driving (dangers win). Hints the belt; while driving unbelted the voice reminds the child every 8 s.")]
    public class AdviseSeatbelt : PlayerUtilityAction
    {
        protected override float Score() => agent.ScoreSeatbelt();
        public override TaskStatus OnUpdate() => Do(agent.RecommendSeatbelt());
    }

    [TaskCategory("Custom/Player")]
    [TaskDescription("Utility: danger ahead 0.9, red light ahead 0.85, pedestrian waiting 0.8. Hints the Stop button.")]
    public class AdviseBrake : PlayerUtilityAction
    {
        protected override float Score() => agent.ScoreBrake();
        public override TaskStatus OnUpdate() => Do(agent.RecommendBrake());
    }

    [TaskCategory("Custom/Player")]
    [TaskDescription("Utility: 0.4 when stopped and a child is crossing in front. A friendly honk.")]
    public class AdviseHonk : PlayerUtilityAction
    {
        protected override float Score() => agent.ScoreHonk();
        public override TaskStatus OnUpdate() => Do(agent.RecommendHonk());
    }

    [TaskCategory("Custom/Player")]
    [TaskDescription("Utility: constant (the default). Drive on.")]
    public class AdviseDriveOn : PlayerUtilityAction
    {
        public SharedFloat utility = 0.35f;
        protected override float Score() => utility.Value;
        public override TaskStatus OnUpdate() => Do(agent.Drive());
    }

    // ===================================================================== café visitors

    [TaskCategory("Custom/Cafe visitors")]
    [TaskDescription("The player's car honked nearby (and not inside)?")]
    public class VisitorHornHeard : AgentConditional<CafeVisitor> { public override TaskStatus OnUpdate() => Status.Of(agent.HornHeard()); }

    [TaskCategory("Custom/Cafe visitors")]
    [TaskDescription("Inside the café?")]
    public class IsInsideCafe : AgentConditional<CafeVisitor> { public override TaskStatus OnUpdate() => Status.Of(agent.IsInside()); }

    [TaskCategory("Custom/Cafe visitors")]
    [TaskDescription("Just had a coffee and leaving?")]
    public class IsLeavingCafe : AgentConditional<CafeVisitor> { public override TaskStatus OnUpdate() => Status.Of(agent.IsLeaving()); }

    [TaskCategory("Custom/Cafe visitors")]
    [TaskDescription("Wants a coffee (thirst full)?")]
    public class WantsCoffee : AgentConditional<CafeVisitor> { public override TaskStatus OnUpdate() => Status.Of(agent.WantsCoffee()); }

    [TaskCategory("Custom/Cafe visitors")]
    [TaskDescription("Waves at the car.")]
    public class VisitorWave : AgentAction<CafeVisitor> { public override TaskStatus OnUpdate() => Do(agent.Wave()); }

    [TaskCategory("Custom/Cafe visitors")]
    [TaskDescription("Has a coffee inside.")]
    public class HaveCoffeeInside : AgentAction<CafeVisitor> { public override TaskStatus OnUpdate() => Do(agent.Inside()); }

    [TaskCategory("Custom/Cafe visitors")]
    [TaskDescription("Walks out and away.")]
    public class WalkOutAndAway : AgentAction<CafeVisitor> { public override TaskStatus OnUpdate() => Do(agent.Leave()); }

    [TaskCategory("Custom/Cafe visitors")]
    [TaskDescription("Walks in through the door.")]
    public class WalkInThroughDoor : AgentAction<CafeVisitor> { public override TaskStatus OnUpdate() => Do(agent.GoIn()); }

    [TaskCategory("Custom/Cafe visitors")]
    [TaskDescription("Utility: constant. Strolls on the pavement.")]
    public class VisitorStroll : AgentAction<CafeVisitor>
    {
        public SharedFloat utility = 0.35f;
        public override float GetUtility() => Utility(utility.Value);
        public override TaskStatus OnUpdate() => Do(agent.Stroll());
    }

    [TaskCategory("Custom/Cafe visitors")]
    [TaskDescription("Utility: curiosity (1 while looking). Looks at the shops.")]
    public class LookAtShops : AgentAction<CafeVisitor>
    {
        public override float GetUtility() => Utility(agent != null ? agent.LookUtility() : 0f);
        public override TaskStatus OnUpdate() => Do(agent.LookAround());
    }

    // ===================================================================== pigeons

    [TaskCategory("Custom/Street life")]
    [TaskDescription("A car is within 9 m (and the pigeons are on the ground)?")]
    public class PigeonsCarComing : AgentConditional<PigeonFlock> { public override TaskStatus OnUpdate() => Status.Of(agent.CarComing()); }

    [TaskCategory("Custom/Street life")]
    [TaskDescription("The pigeons are flying?")]
    public class PigeonsFlying : AgentConditional<PigeonFlock> { public override TaskStatus OnUpdate() => Status.Of(agent.IsFlying()); }

    [TaskCategory("Custom/Street life")]
    [TaskDescription("The flock flies off.")]
    public class PigeonsTakeOff : AgentAction<PigeonFlock> { public override TaskStatus OnUpdate() => Do(agent.TakeOff()); }

    [TaskCategory("Custom/Street life")]
    [TaskDescription("Flies and lands further on.")]
    public class PigeonsFly : AgentAction<PigeonFlock> { public override TaskStatus OnUpdate() => Do(agent.Fly()); }

    [TaskCategory("Custom/Street life")]
    [TaskDescription("Utility: constant. Pecks at crumbs.")]
    public class PigeonsPeck : AgentAction<PigeonFlock>
    {
        public SharedFloat utility = 0.5f;
        public override float GetUtility() => Utility(utility.Value);
        public override TaskStatus OnUpdate() => Do(agent.Peck());
    }

    [TaskCategory("Custom/Street life")]
    [TaskDescription("Utility: wish to hop. Hops around.")]
    public class PigeonsHop : AgentAction<PigeonFlock>
    {
        public override float GetUtility() => Utility(agent != null ? agent.HopWish : 0f);
        public override TaskStatus OnUpdate() => Do(agent.Hop());
    }

    [TaskCategory("Custom/Street life")]
    [TaskDescription("Utility: tiredness. Rests.")]
    public class PigeonsRest : AgentAction<PigeonFlock>
    {
        public override float GetUtility() => Utility(agent != null ? agent.Tired : 0f);
        public override TaskStatus OnUpdate() => Do(agent.Rest());
    }

    // ===================================================================== street cat

    [TaskCategory("Custom/Street life")]
    [TaskDescription("A car is within 7 m?")]
    public class CatCarClose : AgentConditional<StreetCat> { public override TaskStatus OnUpdate() => Status.Of(agent.CarClose()); }

    [TaskCategory("Custom/Street life")]
    [TaskDescription("Dashes to the wall.")]
    public class CatDash : AgentAction<StreetCat> { public override TaskStatus OnUpdate() => Do(agent.Dash()); }

    [TaskCategory("Custom/Street life")]
    [TaskDescription("Utility: constant. Strolls.")]
    public class CatStroll : AgentAction<StreetCat>
    {
        public SharedFloat utility = 0.4f;
        public override float GetUtility() => Utility(utility.Value);
        public override TaskStatus OnUpdate() => Do(agent.Stroll());
    }

    [TaskCategory("Custom/Street life")]
    [TaskDescription("Utility: boredom (1 while washing). Sits and washes.")]
    public class CatWash : AgentAction<StreetCat>
    {
        public override float GetUtility() => Utility(agent != null ? agent.WashUtility() : 0f);
        public override TaskStatus OnUpdate() => Do(agent.Wash());
    }

    [TaskCategory("Custom/Street life")]
    [TaskDescription("Utility: sleepiness. Naps in the sun.")]
    public class CatNap : AgentAction<StreetCat>
    {
        public override float GetUtility() => Utility(agent != null ? agent.NapUtility() : 0f);
        public override TaskStatus OnUpdate() => Do(agent.Nap());
    }

    // ===================================================================== seagull

    [TaskCategory("Custom/Street life")]
    [TaskDescription("Utility: constant. Glides in circles.")]
    public class GullGlide : AgentAction<Gull>
    {
        public SharedFloat utility = 0.5f;
        public override float GetUtility() => Utility(utility.Value);
        public override TaskStatus OnUpdate() => Do(agent.Glide());
    }

    [TaskCategory("Custom/Street life")]
    [TaskDescription("Utility: 0.9 now and then. Flaps to climb.")]
    public class GullFlap : AgentAction<Gull>
    {
        public override float GetUtility() => Utility(agent != null ? agent.FlapUtility() : 0f);
        public override TaskStatus OnUpdate() => Do(agent.Flap());
    }

    // ===================================================================== utility root

    [TaskCategory("Custom/Utility")]
    [TaskDescription("One branch under the Utility Selector at the top of a tree. Its utility is a fixed number " +
                     "(higher = more important), and 0 while the branch's first condition is false. Runs its child as is.")]
    public class UtilityBranch : Decorator
    {
        public SharedFloat utility = 0.5f;
        private TaskStatus status = TaskStatus.Inactive;

        public override float GetUtility()
        {
            float u = utility.Value;
            // the branch's first condition (e.g. "Something in front?") is false: not this branch
            if (Children != null && Children.Count > 0 && Children[0] is ParentTask branch &&
                branch.Children != null && branch.Children.Count > 0 && branch.Children[0] is Conditional condition &&
                condition.OnUpdate() == TaskStatus.Failure)
                u = 0f;
            return UtilityAI.Score(transform, FriendlyName, u);
        }

        public override bool CanExecute() => status == TaskStatus.Inactive || status == TaskStatus.Running;
        public override void OnChildExecuted(TaskStatus childStatus) => status = childStatus;
        public override void OnEnd() => status = TaskStatus.Inactive;
    }
}
