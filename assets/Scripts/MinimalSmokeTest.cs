using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class MinimalSmokeTest : MonoBehaviour
{
    public ArcadeCarController car;
    public ChaseCamera chaseCamera;
    private float timer;
    private float logTimer;
    private float fpsSum;
    private float minFps = 999f;
    private readonly StringBuilder samples = new StringBuilder();
    private int frames;
    private bool shotA, shotB;
    private string output;
    private void Start()
    {
        if (!Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "-autotest")) { enabled = false; return; }
        output = Path.GetFullPath("tests/minimal");
        Directory.CreateDirectory(output);
        car.aiControlled = true;
        samples.AppendLine("time,speed_kph,grounded,nitro,fps,x,y,z");
        Application.targetFrameRate = 60;
    }
    private void Update()
    {
        timer += Time.deltaTime;
        float fps = 1f / Mathf.Max(Time.deltaTime, 0.0001f);
        if (timer > 2f)
        {
            fpsSum += fps;
            frames++;
            minFps = Mathf.Min(minFps, fps);
        }
        car.SetTestInput(timer > 1f ? 1f : 0f, timer > 8f && timer < 10.5f ? 0.35f : 0f, timer > 10.5f && timer < 11.5f, timer > 5f && timer < 8f);
        logTimer += Time.deltaTime;
        if (logTimer >= 1f)
        {
            logTimer = 0f;
            Vector3 p = car.transform.position;
            samples.AppendLine($"{timer:F1},{car.SpeedKph:F2},{car.GroundedWheels},{car.nitro:F2},{fps:F1},{p.x:F2},{p.y:F2},{p.z:F2}");
        }
        if (timer > 4f && !shotA) { shotA = true; Capture("01_acceleration.png"); }
        if (timer > 12.5f && !shotB) { shotB = true; Capture("02_drift.png"); }
        if (timer > 15f)
        {
            enabled = false;
            car.SetTestInput(0f, 0f, false, false);
            StartCoroutine(FinishTest());
        }
    }
    private IEnumerator FinishTest()
    {
        string telemetry = $"device={SystemInfo.graphicsDeviceName}\nversion={SystemInfo.graphicsDeviceVersion}\nresolution={Screen.width}x{Screen.height}\nspeed_kph={car.SpeedKph:F2}\ngrounded_wheels={car.GroundedWheels}\nnitro={car.nitro:F2}\nposition={car.transform.position}\nrotation={car.transform.eulerAngles}\naverage_fps={(frames > 0 ? fpsSum / frames : 0f):F2}\nminimum_fps={(frames > 0 ? minFps : 0f):F2}\n";
        File.WriteAllText(Path.Combine(output, "telemetry.txt"), telemetry);
        File.WriteAllText(Path.Combine(output, "driving_samples.csv"), samples.ToString());
        yield return new WaitForEndOfFrame();
        yield return new WaitForSecondsRealtime(0.4f);
        Application.Quit(0);
    }
    private void Capture(string name)
    {
        ScreenCapture.CaptureScreenshot(Path.Combine(output, name));
        Debug.Log($"SMOKE_CAPTURE {name} speed={car.SpeedKph:F1} grounded={car.GroundedWheels} position={car.transform.position}");
    }
}
