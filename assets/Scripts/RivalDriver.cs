using UnityEngine;

[RequireComponent(typeof(ArcadeCarController))]
public sealed class RivalDriver : MonoBehaviour
{
    public TrackPath track;
    public RaceManager race;
    public float skill = 0.65f;
    public float preferredLane;
    /// <summary>Hold the boost for as long as the car is moving forward.
    /// The normal AI taps the boost in short bursts, so its nitro streak never
    /// reaches the 1.5 s overdrive threshold; this flag lets the automated
    /// nitro test prove the overdrive branch at runtime.</summary>
    public bool sustainedBoost;
    public int NearestIndex { get; private set; }
    public float Lane { get { return lane; } }
    public bool drivingEnabled = true;
    private ArcadeCarController car;
    private float lane;
    private float thinkTimer;
    private float stuckTimer;
    private float targetSpeed;
    private float desiredSteer;
    private float throttle;
    private bool handbrake;
    private bool boost;

    private void Awake() { car = GetComponent<ArcadeCarController>(); lane = preferredLane; }
    private void Start() { car.aiControlled = true; NearestIndex = track.NearestIndex(transform.position); }

    private void Update()
    {
        if (!track || !car || !drivingEnabled || car.controlsLocked) { if (car) car.SetTestInput(0f, 0f, false, false); return; }
        thinkTimer -= Time.deltaTime;
        if (thinkTimer <= 0f)
        {
            thinkTimer = 0.08f;
            NearestIndex = track.NearestIndex(transform.position, NearestIndex);
            float speed = car.Body.linearVelocity.magnitude;
            int aheadIndex = track.IndexAhead(NearestIndex, 9f + speed * 0.42f);
            float targetLane = preferredLane;
            var ahead = race ? race.FindCarAhead(this, 28f) : null;
            if (ahead != null)
            {
                float side = race.OvertakeSide(this, ahead);
                targetLane = Mathf.Clamp(ahead.Lane + side * 3.1f, -3.8f, 3.8f);
            }
            lane = Mathf.Lerp(lane, targetLane, 0.24f);
            Vector3 target = track.PointAt(aheadIndex, lane) + Vector3.up * 0.4f;
            Vector3 local = transform.InverseTransformPoint(target);
            desiredSteer = Mathf.Clamp(local.x / Mathf.Max(2.5f, Mathf.Abs(local.z)) * 1.65f, -1f, 1f);
            targetSpeed = track.TargetSpeed(NearestIndex, skill);
            float currentForward = Vector3.Dot(car.Body.linearVelocity, transform.forward);
            throttle = currentForward < targetSpeed ? 1f : -Mathf.Clamp((currentForward - targetSpeed) / 7f, 0.2f, 1f);
            float angle = Vector3.Angle(transform.forward, track.At(aheadIndex).tangent);
            handbrake = targetSpeed < 21f && currentForward > 25f && angle > 20f;
            boost = targetSpeed > 46f && angle < 14f && car.nitro > 32f && currentForward > 18f;
            if (sustainedBoost)
            {
                // Keep the tank topped up and hold the boost while moving, so the
                // 1.5 s overdrive threshold is reachable. Only the automated
                // nitro test enables this; normal AI racing is untouched.
                car.nitro = Mathf.Max(car.nitro, 60f);
                boost = currentForward > 12f && angle < 26f && car.nitro > 2f;
            }
            if (speed < 2f && race && race.State == RacePhase.Racing) stuckTimer += 0.08f; else stuckTimer = 0f;
            if (stuckTimer > 4f)
            {
                car.ResetToTrack(NearestIndex);
                if (race) race.NotifyCarReset(car);
                stuckTimer = 0f;
            }
        }
        car.SetTestInput(throttle, desiredSteer, handbrake, boost);
    }
}
