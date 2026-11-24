using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;
using RoadSafety.World;

// Behavior Designer tasks of the evaluation pedestrians (PNJ behaviours 1, 2 and 4).
// They appear in the Behavior Designer editor under Tasks > Custom > Pedestrians.
// Conditionals answer a question, Actions do something; the Actions used under a
// Utility Selector return their "fonction d'utilité" in GetUtility().
namespace RoadSafety.AI.Pedestrians
{
    // ======================================================================== bases

    public abstract class PedestrianConditional : Conditional
    {
        protected PedestrianAgent agent;
        public override void OnAwake() => agent = GetComponent<PedestrianAgent>();
    }

    public abstract class PedestrianAction : Action
    {
        protected PedestrianAgent agent;
        public override void OnAwake() => agent = GetComponent<PedestrianAgent>();

        public override void OnStart()
        {
            if (agent == null) return;
            agent.currentTask = FriendlyName;
            UtilityAI.Act(agent, FriendlyName, NodeState.Running); // central action gateway (US 5.3)
        }

        /// <summary>
        /// Scores this option with the game's central utility function (UtilityAI.Score, US 5.3):
        /// clamped to 0..1 and shown above the pedestrian's head.
        /// </summary>
        protected float Report(float u)
        {
            u = UtilityAI.Score(agent, FriendlyName, u);
            if (agent != null) agent.ReportUtility(FriendlyName, u);
            return u;
        }
    }

    // ======================================================================== conditionals

    [TaskCategory("Custom/Pedestrians")]
    [TaskDescription("Success while the pedestrian is on the road (crossing).")]
    public class IsOnRoad : PedestrianConditional
    {
        public override TaskStatus OnUpdate() => agent != null && agent.onRoad ? TaskStatus.Success : TaskStatus.Failure;
    }

    [TaskCategory("Custom/Pedestrians")]
    [TaskDescription("Success once the pedestrian has crossed and left (behind the bus).")]
    public class IsGone : PedestrianConditional
    {
        public override TaskStatus OnUpdate() => agent != null && agent.gone ? TaskStatus.Success : TaskStatus.Failure;
    }

    [TaskCategory("Custom/Pedestrians")]
    [TaskDescription("Success if the player's car is about to run into the pedestrian.")]
    public class CarAboutToHitMe : PedestrianConditional
    {
        public override TaskStatus OnUpdate() => agent != null && agent.CarAboutToHitMe() ? TaskStatus.Success : TaskStatus.Failure;
    }

    [TaskCategory("Custom/Pedestrians")]
    [TaskDescription("Success if the main-road traffic light (the cars' light) shows this colour.")]
    public class LightIs : PedestrianConditional
    {
        public LightColor color = LightColor.Red;
        public override TaskStatus OnUpdate() => agent != null && agent.LightIs(color) ? TaskStatus.Success : TaskStatus.Failure;
    }

    [TaskCategory("Custom/Pedestrians")]
    [TaskDescription("Success during the first seconds after the cars' light went from red to green.")]
    public class LightJustTurnedGreen : PedestrianConditional
    {
        public SharedFloat window = 1f;
        public override TaskStatus OnUpdate() =>
            agent != null && Time.time - agent.greenAt < window.Value ? TaskStatus.Success : TaskStatus.Failure;
    }

    [TaskCategory("Custom/Pedestrians")]
    [TaskDescription("Success once the pedestrian has waited on the kerb at least this long (temps écoulé).")]
    public class WaitedAtLeast : PedestrianConditional
    {
        public SharedFloat seconds = 1.5f;
        public override TaskStatus OnUpdate() => agent != null && agent.waited >= seconds.Value ? TaskStatus.Success : TaskStatus.Failure;
    }

    // ---------- conditions of the evaluation slide (in blue), one node each

    [TaskCategory("Custom/Pedestrians/Conditions")]
    [TaskDescription("Piéton dans zone: Success while the pedestrian stands in the crossing zone (on the kerb at the crosswalk).")]
    public class PedestrianInZone : PedestrianConditional
    {
        public SharedFloat radius = 3f;
        public override TaskStatus OnUpdate() =>
            agent != null && agent.startKerb != null && (agent.transform.position - agent.startKerb.position).sqrMagnitude < radius.Value * radius.Value
                ? TaskStatus.Success : TaskStatus.Failure;
    }

    [TaskCategory("Custom/Pedestrians/Conditions")]
    [TaskDescription("Aléatoire: Success when the pedestrian's random roll (re-rolled while waiting) is below the chance.")]
    public class RandomChance : PedestrianConditional
    {
        public SharedFloat chance = 0.5f;
        public override TaskStatus OnUpdate() => agent != null && agent.roll < chance.Value ? TaskStatus.Success : TaskStatus.Failure;
    }

    [TaskCategory("Custom/Pedestrians/Conditions")]
    [TaskDescription("Vitesse player > 50: Success when the player's car goes faster than this (in % of its top speed).")]
    public class PlayerSpeedAbove : PedestrianConditional
    {
        public SharedFloat percent = 50f;
        public override TaskStatus OnUpdate() => PedestrianAgent.PlayerSpeedPercent > percent.Value ? TaskStatus.Success : TaskStatus.Failure;
    }

    [TaskCategory("Custom/Pedestrians/Conditions")]
    [TaskDescription("Player éloigné: Success when the player's car is on the main road and between min and max metres before the pedestrian.")]
    public class PlayerIsFar : PedestrianConditional
    {
        public SharedFloat min = 22f;
        public SharedFloat max = 130f;
        public override TaskStatus OnUpdate()
        {
            if (agent == null) return TaskStatus.Failure;
            float d = agent.PlayerDistance;
            return PedestrianAgent.PlayerOnMainRoad && d > min.Value && d < max.Value ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    // ======================================================================== plain actions

    [TaskCategory("Custom/Pedestrians")]
    [TaskDescription("Walks / runs to a point. Running until there, then Success.")]
    public class MoveToPoint : PedestrianAction
    {
        public PedestrianPoint point = PedestrianPoint.FarKerb;
        public SharedFloat speed = 1.2f;
        public SharedBool startled = false;
        private Transform target;

        public override void OnStart()
        {
            base.OnStart();
            target = agent.Point(point);
            agent.startled = startled.Value;
            agent.stateTime = 0f;
        }

        public override TaskStatus OnUpdate()
        {
            if (target == null) return TaskStatus.Failure;
            return agent.MoveTo(target.position, speed.Value) ? TaskStatus.Success : TaskStatus.Running;
        }

        public override void OnEnd()
        {
            if (agent != null) agent.startled = false;
        }
    }

    [TaskCategory("Custom/Pedestrians")]
    [TaskDescription("Steps onto the road (starts crossing).")]
    public class StepOntoRoad : PedestrianAction
    {
        public override TaskStatus OnUpdate()
        {
            agent.onRoad = true;
            return TaskStatus.Success;
        }
    }

    [TaskCategory("Custom/Pedestrians")]
    [TaskDescription("Off the road again. swapKerbs = true if they reached the other side (it becomes home).")]
    public class LeaveRoad : PedestrianAction
    {
        public SharedBool swapKerbs = true;
        [UnityEngine.Tooltip("Instead: swap only if they ended up on the far kerb (after running to the nearest kerb).")]
        public SharedBool swapIfAtFarKerb = false;
        public override TaskStatus OnUpdate()
        {
            bool atFar = agent.farKerb != null && (agent.transform.position - agent.farKerb.position).sqrMagnitude < 0.25f;
            if (swapIfAtFarKerb.Value ? atFar : swapKerbs.Value) agent.SwapKerbs();
            agent.onRoad = false;
            agent.waited = 0f;
            return TaskStatus.Success;
        }
    }

    [TaskCategory("Custom/Pedestrians")]
    [TaskDescription("Stands on the kerb: counts the waiting time and rolls a new random number now and then.")]
    public class WaitOnKerb : PedestrianAction
    {
        public SharedFloat rerollEvery = 1.5f;
        public SharedFloat walkSpeed = 1.2f;
        private float timer;

        public override TaskStatus OnUpdate()
        {
            agent.waited += Time.deltaTime;
            timer += Time.deltaTime;
            if (timer >= rerollEvery.Value) { timer = 0f; agent.Reroll(); }
            if (agent.MoveTo(agent.startKerb.position, walkSpeed.Value)) agent.Face(agent.farKerb.position, 4f);
            return TaskStatus.Success;
        }
    }

    [TaskCategory("Custom/Pedestrians")]
    [TaskDescription("Marks the pedestrian as gone (crossed and leaving).")]
    public class MarkGone : PedestrianAction
    {
        public override TaskStatus OnUpdate()
        {
            agent.onRoad = false;
            agent.gone = true;
            agent.stateTime = 0f;
            return TaskStatus.Success;
        }
    }

    [TaskCategory("Custom/Pedestrians")]
    [TaskDescription("Waits out of sight, then comes back behind the bus once the player is far away. Running until then.")]
    public class RespawnBehindBus : PedestrianAction
    {
        public SharedFloat afterSeconds = 8f;
        public SharedFloat playerAwayDistance = 45f;

        public override TaskStatus OnUpdate()
        {
            bool away = Mathf.Abs(agent.PlayerDistance) > playerAwayDistance.Value;
            if (agent.stateTime < afterSeconds.Value || !away) return TaskStatus.Running;
            agent.transform.position = agent.hideSpot.position;
            agent.SnapToGround();
            agent.gone = false;
            agent.roll = 0f;
            return TaskStatus.Success;
        }
    }

    // ======================================================================== 1 - distracted pedestrian (utility options)

    [TaskCategory("Custom/Pedestrians/1 Distracted")]
    [TaskDescription("Utility: feu vert × piéton dans zone × vitesse player > 50 × temps écoulé × aléatoire. Steps onto the road WITHOUT looking.")]
    public class StepOutWithoutLooking : PedestrianAction
    {
        [UnityEngine.Tooltip("Player speed in % of the car's top speed (the car tops at ~32 km/h).")]
        public SharedFloat fastPercent = 50f;
        public SharedFloat minWaitSeconds = 4f;
        public SharedFloat zoneNear = 12f;
        public SharedFloat zoneFar = 55f;

        public override float GetUtility()
        {
            if (agent == null) return 0f;
            float green = agent.LightIs(LightColor.Green) ? 1f : 0f;
            float d = agent.PlayerDistance;
            float inZone = PedestrianAgent.PlayerOnMainRoad && d > zoneNear.Value && d < zoneFar.Value ? 1f : 0f;
            float fast = PedestrianAgent.PlayerSpeedPercent > fastPercent.Value ? 1f : 0f;
            float time = agent.waited >= minWaitSeconds.Value ? 1f : 0f;
            return Report(green * inZone * fast * time * agent.roll);
        }

        public override TaskStatus OnUpdate()
        {
            agent.onRoad = true;
            Debug.Log("[Distracted pedestrian] Steps out WITHOUT looking (green for cars, player fast and close).");
            return TaskStatus.Success;
        }
    }

    [TaskCategory("Custom/Pedestrians/1 Distracted")]
    [TaskDescription("Utility: high when the cars have a red light. Steps onto the road.")]
    public class CrossOnRedForCars : PedestrianAction
    {
        public SharedFloat utilityWhenRed = 0.8f;
        public SharedFloat minWaitSeconds = 1.5f;

        public override float GetUtility() =>
            agent == null ? 0f : Report(agent.LightIs(LightColor.Red) && agent.waited > minWaitSeconds.Value ? utilityWhenRed.Value : 0f);

        public override TaskStatus OnUpdate()
        {
            agent.onRoad = true;
            return TaskStatus.Success;
        }
    }

    [TaskCategory("Custom/Pedestrians/1 Distracted")]
    [TaskDescription("Utility: constant. Stays on the kerb scrolling the phone (counts the waiting time, rolls the dice).")]
    public class WaitScrollingPhone : WaitOnKerb
    {
        public SharedFloat utility = 0.4f;
        public override float GetUtility() => Report(utility.Value);
    }

    // ======================================================================== 2 - unpredictable child (utility options)

    [TaskCategory("Custom/Pedestrians/2 Child")]
    [TaskDescription("Utility: constant. Runs on to the far kerb. Running until there.")]
    public class HurryToFarKerb : PedestrianAction
    {
        public SharedFloat utility = 0.5f;
        public SharedFloat runSpeed = 2.6f;

        public override float GetUtility() => Report(utility.Value);

        public override TaskStatus OnUpdate()
        {
            if (!agent.MoveTo(agent.farKerb.position, runSpeed.Value)) return TaskStatus.Running;
            agent.SwapKerbs();
            agent.onRoad = false;
            agent.waited = 0f;
            return TaskStatus.Success;
        }
    }

    [TaskCategory("Custom/Pedestrians/2 Child")]
    [TaskDescription("Utility: le feu passe du rouge au vert × player éloigné × vitesse > 50 × aléatoire. DEMI-TOUR: runs back the way she came.")]
    public class TurnBack : PedestrianAction
    {
        public SharedFloat fastPercent = 50f;
        public SharedFloat farMin = 22f;
        public SharedFloat farMax = 130f;
        public SharedFloat runSpeed = 2.6f;

        public override float GetUtility()
        {
            if (agent == null) return 0f;
            float d = agent.PlayerDistance;
            float playerFar = PedestrianAgent.PlayerOnMainRoad && d > farMin.Value && d < farMax.Value ? 1f : 0f;
            float fast = PedestrianAgent.PlayerSpeedPercent > fastPercent.Value ? 1f : 0f;
            return Report(playerFar * fast * (0.35f + 0.65f * agent.roll));
        }

        public override void OnStart()
        {
            base.OnStart();
            Debug.Log("[Unpredictable child] DEMI-TOUR in the middle of the road!");
        }

        public override TaskStatus OnUpdate()
        {
            if (!agent.MoveTo(agent.startKerb.position, runSpeed.Value)) return TaskStatus.Running;
            agent.onRoad = false;
            agent.waited = 0f;
            return TaskStatus.Success;
        }
    }

    // ======================================================================== 4 - behind the bus (utility options)

    [TaskCategory("Custom/Pedestrians/4 Bus")]
    [TaskDescription("Utility: constant. Stays hidden in front of the bus.")]
    public class StayHiddenBehindBus : PedestrianAction
    {
        public SharedFloat utility = 0.35f;
        public SharedFloat rerollEvery = 0.5f;
        private float timer;

        public override float GetUtility() => Report(utility.Value);

        public override TaskStatus OnUpdate()
        {
            timer += Time.deltaTime;
            if (timer >= rerollEvery.Value) { timer = 0f; agent.Reroll(); }
            if (agent.MoveTo(agent.hideSpot.position, 1.9f)) agent.Face(agent.farKerb.position, 4f);
            return TaskStatus.Success;
        }
    }

    [TaskCategory("Custom/Pedestrians/4 Bus")]
    [TaskDescription("Utility: the player's car is coming and close × aléatoire. SURGES out from behind the bus.")]
    public class SurgeOutFromBus : PedestrianAction
    {
        public SharedFloat triggerNear = 12f;
        public SharedFloat triggerFar = 38f;

        public override float GetUtility()
        {
            if (agent == null) return 0f;
            float d = agent.PlayerDistance;
            float coming = PedestrianAgent.PlayerOnMainRoad && PedestrianAgent.Car.Speed > 1f && d > triggerNear.Value && d < triggerFar.Value ? 1f : 0f;
            return Report(coming * agent.roll);
        }

        public override TaskStatus OnUpdate()
        {
            agent.onRoad = true;
            Debug.Log("[Bus pedestrian] Surges out from behind the bus!");
            return TaskStatus.Success;
        }
    }

    // ======================================================================== 3 - old man (slow, horn → remark)

    [TaskCategory("Custom/Pedestrians/3 Old man")]
    [TaskDescription("Success if the player honked in the last moments while close to the old man.")]
    public class WasHonkedAt : PedestrianConditional
    {
        public SharedFloat within = 0.6f;
        public SharedFloat maxDistance = 30f;
        public override TaskStatus OnUpdate()
        {
            if (agent == null || PedestrianAgent.Car == null) return TaskStatus.Failure;
            bool recent = Time.time - agent.honkedAt < within.Value;
            bool close = Vector3.Distance(PedestrianAgent.Car.transform.position, agent.transform.position) < maxDistance.Value;
            return recent && close ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory("Custom/Pedestrians/3 Old man")]
    [TaskDescription("Stops, turns to the driver, wags his hand and makes a remark (voice only). Running while he speaks.")]
    public class MakeRemark : PedestrianAction
    {
        private float until;

        public override void OnStart()
        {
            base.OnStart();
            agent.StopMoving(); // stands still to tell the driver off
            agent.remarking = true;
            agent.honkedAt = -99f;
            until = Time.time + Mathf.Max(1.5f, agent.SayRemark());
            Debug.Log("[Old man] The driver honked: he makes a remark.");
        }

        public override TaskStatus OnUpdate()
        {
            if (PedestrianAgent.Car != null) agent.Face(PedestrianAgent.Car.transform.position, 6f);
            return Time.time < until ? TaskStatus.Running : TaskStatus.Success;
        }

        public override void OnEnd()
        {
            if (agent != null) agent.remarking = false;
        }
    }

    [TaskCategory("Custom/Pedestrians/3 Old man")]
    [TaskDescription("Utility: constant. The light is green but he is still crossing: he keeps shuffling on at his own pace. Running until the far kerb.")]
    public class ShuffleOnSlowly : PedestrianAction
    {
        public SharedFloat utility = 0.5f;
        public SharedFloat speed = 0.45f;

        public override float GetUtility() => Report(utility.Value);

        public override TaskStatus OnUpdate()
        {
            if (!agent.MoveTo(agent.farKerb.position, speed.Value)) return TaskStatus.Running;
            agent.SwapKerbs();
            agent.onRoad = false;
            agent.waited = 0f;
            return TaskStatus.Success;
        }
    }

    [TaskCategory("Custom/Pedestrians/3 Old man")]
    [TaskDescription("Utility: le feu passe au vert et toujours pas passé × the player's car comes fast. Raises his hand to the cars (\"wait!\") and keeps crossing slowly.")]
    public class RaiseHandToCars : PedestrianAction
    {
        public SharedFloat fastPercent = 50f;
        public SharedFloat nearMax = 70f;
        public SharedFloat speed = 0.45f;

        public override float GetUtility()
        {
            if (agent == null) return 0f;
            float d = agent.PlayerDistance;
            float coming = PedestrianAgent.PlayerOnMainRoad && d > 0f && d < nearMax.Value ? 1f : 0f;
            float fast = PedestrianAgent.PlayerSpeedPercent > fastPercent.Value ? 1f : 0.3f;
            return Report(coming * fast * 0.9f);
        }

        public override void OnStart()
        {
            base.OnStart();
            agent.StopMoving(); // stops to raise his hand first
            agent.raisingHand = true;
            agent.stateTime = 0f;
        }

        public override TaskStatus OnUpdate()
        {
            if (PedestrianAgent.Car != null && agent.stateTime < 1.2f)
                agent.Face(PedestrianAgent.Car.transform.position, 4f);
            else if (!agent.MoveTo(agent.farKerb.position, speed.Value))
                return TaskStatus.Running;
            else
            {
                agent.SwapKerbs();
                agent.onRoad = false;
                agent.waited = 0f;
                return TaskStatus.Success;
            }
            return TaskStatus.Running;
        }

        public override void OnEnd()
        {
            if (agent != null) agent.raisingHand = false;
        }
    }
}
