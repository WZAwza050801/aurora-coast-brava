using UnityEngine;

public sealed class VehicleAudio : MonoBehaviour
{
    public ArcadeCarController car;
    public AudioClip[] engineLoops;
    public AudioClip skidClip, windClip, boostStart, boostLoop, boostEnd, impactClip;
    private AudioSource engineA, engineB, skid, wind, boostSource, oneShot;
    private bool wasBoosting;
    private float gearTimer;

    private void Start()
    {
        if (!car) car = GetComponent<ArcadeCarController>();
        engineA = MakeSource("Engine low", engineLoops.Length > 1 ? engineLoops[1] : null, true, 0.5f);
        engineB = MakeSource("Engine high", engineLoops.Length > 4 ? engineLoops[4] : engineA.clip, true, 0.35f);
        skid = MakeSource("Tyre slip", skidClip, true, 0f);
        wind = MakeSource("Wind load", windClip, true, 0f);
        boostSource = MakeSource("Nitro loop", boostLoop, true, 0f);
        oneShot = MakeSource("One shots", null, false, 0.8f);
    }

    private AudioSource MakeSource(string name, AudioClip clip, bool loop, float volume)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        var source = obj.AddComponent<AudioSource>();
        source.clip = clip; source.loop = loop; source.volume = volume; source.spatialBlend = 0.35f;
        source.dopplerLevel = 0.15f; source.playOnAwake = false;
        if (clip && loop) source.Play();
        return source;
    }

    private void Update()
    {
        if (!car || !engineA) return;
        float speed = car.Body.linearVelocity.magnitude;
        float normalized = Mathf.Clamp01(speed / 76f);
        float gearFloat = normalized * 5.7f;
        int gear = Mathf.Clamp(Mathf.FloorToInt(gearFloat), 0, 5);
        float rpm = Mathf.Lerp(0.22f, 1f, Mathf.Repeat(gearFloat, 1f));
        if (speed < 2f) rpm = 0.18f + Mathf.Abs(car.Throttle) * 0.3f;
        float load = Mathf.Clamp01(Mathf.Abs(car.Throttle) + (car.NitroActive ? 0.6f : 0f));
        engineA.pitch = Mathf.Lerp(engineA.pitch, 0.72f + rpm * 1.05f + gear * 0.025f, Time.deltaTime * 8f);
        engineB.pitch = Mathf.Lerp(engineB.pitch, 0.66f + rpm * 1.35f, Time.deltaTime * 8f);
        engineA.volume = Mathf.Lerp(0.28f, 0.62f, load) * (1f - rpm * 0.45f);
        engineB.volume = Mathf.Lerp(0.08f, 0.48f, load) * (0.25f + rpm * 0.75f);
        skid.volume = Mathf.Lerp(skid.volume, car.DriftAmount * (car.Handbrake ? 0.85f : 0.42f), Time.deltaTime * 10f);
        skid.pitch = 0.9f + normalized * 0.25f;
        wind.volume = Mathf.Lerp(wind.volume, normalized * normalized * 0.55f, Time.deltaTime * 4f);
        wind.pitch = 0.8f + normalized * 0.6f;
        boostSource.volume = Mathf.Lerp(boostSource.volume, car.NitroActive ? 0.7f : 0f, Time.deltaTime * 10f);
        if (car.NitroActive && !wasBoosting && boostStart) oneShot.PlayOneShot(boostStart, 0.9f);
        if (!car.NitroActive && wasBoosting && boostEnd) oneShot.PlayOneShot(boostEnd, 0.65f);
        wasBoosting = car.NitroActive;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (oneShot && impactClip && collision.relativeVelocity.magnitude > 3f)
            oneShot.PlayOneShot(impactClip, Mathf.Clamp01(collision.relativeVelocity.magnitude / 20f));
    }
}
