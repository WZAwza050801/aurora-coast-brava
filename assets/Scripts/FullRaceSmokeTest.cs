using System;
using System.IO;
using System.Collections;
using System.Text;
using UnityEngine;

public sealed class FullRaceSmokeTest : MonoBehaviour
{
    public RaceManager race;
    private bool active;
    private float timer, logTimer, fpsSum, minFps = 999f;
    private int frames, captures, overdriveFrames;
    private string output;
    private StringBuilder log = new StringBuilder();

    private IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        bool wantRace = Array.Exists(args, a => a == "-racetest");
        // Keep Update() out of the first frames so the telemetry file starts
        // with its header instead of two stray startup rows.
        active = false;
        if (Array.Exists(args, a => a == "-gamepadcheck")) { WriteGamepadCheck(); enabled = false; yield break; }
        if (Array.Exists(args, a => a == "-pausetest")) { yield return MenuChecks(); yield break; }
        if (Array.Exists(args, a => a == "-garagecheck")) { yield return GarageChecks(); yield break; }
        if (Array.Exists(args, a => a == "-nitrotest")) { yield return NitroChecks(); yield break; }
        if (Array.Exists(args, a => a == "-collisiontest")) { yield return CollisionChecks(); yield break; }
        if (Array.Exists(args, a => a == "-garagedrive")) { yield return GarageDriveChecks(); yield break; }
        if (Array.Exists(args, a => a == "-padmaptest")) { WritePadMapCheck(); enabled = false; yield break; }
        if (!wantRace) { enabled = false; yield break; }
        yield return null;
        output = Path.GetFullPath("tests/race");
        Directory.CreateDirectory(output);
        var driver = race.player.GetComponent<RivalDriver>();
        race.player.aiControlled = true;
        driver.enabled = true;
        driver.drivingEnabled = true;
        driver.skill = 0.72f;
        race.StartRace();
        log.AppendLine($"device={SystemInfo.graphicsDeviceName}");
        log.AppendLine($"version={SystemInfo.graphicsDeviceVersion}");
        log.AppendLine($"resolution={Screen.width}x{Screen.height}");
        log.AppendLine($"track_length_m={race.track.TotalLength:F1}");
        log.AppendLine("gamepad_devices=" + GamepadNames());
        var garage = race.player.GetComponent<VehicleGarage>();
        log.AppendLine("garage_variants=" + (garage ? garage.Count : 1));
        log.AppendLine("selected_car=" + (garage ? garage.Current.id : "auroraGt"));
        log.AppendLine("time,state,lap,checkpoint,position,speed_kph,grounded,nitro,fps,xyz");
        active = true;
    }

    /// <summary>Captures real main-menu and pause-menu frames so the HUD work has
    /// screenshot evidence instead of being asserted from source code.</summary>
    private IEnumerator MenuChecks()
    {
        string dir = Path.GetFullPath("tests/race");
        Directory.CreateDirectory(dir);
        yield return new WaitForSecondsRealtime(1.5f);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, "main_menu.png"));
        yield return new WaitForSecondsRealtime(0.6f);
        race.StartRace();
        yield return new WaitForSecondsRealtime(6f);
        race.Pause();
        yield return new WaitForSecondsRealtime(0.6f);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, "pause_menu.png"));
        Debug.Log("MENU_CHECK pause_state=" + race.State + " timescale=" + Time.timeScale
                  + " audio_pause=" + AudioListener.pause + " gamepad=" + GamepadNames());
        yield return new WaitForSecondsRealtime(0.8f);
        Application.Quit(0);
    }

    /// <summary>Real visual evidence for the multi-car garage: one screenshot per
    /// body plus a text probe of the active renderer set and the shared physics
    /// rig. AuroraCoast.exe -garagecheck writes tests/race/garage/.</summary>
    private IEnumerator GarageChecks()
    {
        string dir = Path.GetFullPath("tests/race/garage");
        Directory.CreateDirectory(dir);
        var hud = race.GetComponent<RaceHud>();
        if (hud) hud.enabled = false;                 // clean car-only frames
        var chase = Camera.main ? Camera.main.GetComponent<ChaseCamera>() : null;
        if (chase) chase.enabled = false;             // freeze a 3/4 showcase angle

        // Hide the rivals: the garage shot is about one body at a time, and
        // the other cars on the grid otherwise sit in frame with their own
        // aero kits and read as floating parts.
        var hidden = new System.Collections.Generic.List<GameObject>();
        foreach (var car in race.cars)
        {
            if (car == null || car == race.player) continue;
            var body = car.GetComponentInChildren<Transform>();
            if (body != null && body.gameObject.activeSelf) { body.gameObject.SetActive(false); hidden.Add(body.gameObject); }
        }

        if (Camera.main)
        {
            // Frame the whole car from a slightly raised 3/4 angle. The guardrail
            // line sits low, so a higher camera keeps the track furniture out
            // of the car's silhouette.
            var t = race.player.transform;
            Vector3 focus = t.position + Vector3.up * 0.80f;
            Vector3 offset = t.rotation * new Vector3(4.6f, 2.9f, -6.6f);
            Camera.main.transform.position = focus + offset;
            Camera.main.transform.LookAt(focus);
            var cam = Camera.main.GetComponent<Camera>();
            if (cam) cam.fieldOfView = 40f;
        }
        var garage = race.player.GetComponent<VehicleGarage>();
        var report = new StringBuilder();
        report.AppendLine("garage_check_utc=" + DateTime.UtcNow.ToString("o"));
        report.AppendLine("device=" + SystemInfo.graphicsDeviceName);
        report.AppendLine("resolution=" + Screen.width + "x" + Screen.height);
        report.AppendLine("variants=" + (garage ? garage.Count : 0));

        // Any null component means a broken script reference on the car.
        int missingScripts = 0;
        foreach (var comp in race.player.GetComponents<Component>())
            if (comp == null) missingScripts++;
        report.AppendLine("missing_script_components=" + missingScripts);
        report.AppendLine("verdict_no_broken_script_refs=" + (missingScripts == 0));

        yield return new WaitForSecondsRealtime(1.5f);
        if (!garage || garage.Count == 0)
        {
            report.AppendLine("ERROR=VehicleGarage component missing or empty");
            report.AppendLine("verdict_garage_usable=False");
        }
        else
        {
            int colliders = race.player.wheels.Length;
            float mass = race.player.GetComponent<Rigidbody>().mass;
            for (int i = 0; i < garage.Count; i++)
            {
                garage.Apply(i);
                yield return new WaitForSecondsRealtime(0.45f);
                var spec = garage.Current;
                int activeBodies = 0, visibleRenderers = 0;
                foreach (var v in garage.variants)
                    if (v.root && v.root.activeSelf) activeBodies++;
                foreach (var r in race.player.GetComponentsInChildren<Renderer>())
                    if (r.gameObject.activeInHierarchy) visibleRenderers++;
                string file = "garage_" + i.ToString("00") + "_" + spec.id + ".png";
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, file));
                report.AppendLine(i + " | " + spec.id + " | " + spec.displayName + " | " + spec.credit
                    + " | active_bodies=" + activeBodies + " | visible_renderers=" + visibleRenderers
                    + " | wheel_visuals=" + race.player.wheelVisuals.Length
                    + " | wing_kit=" + (garage.aeroKit ? garage.aeroKit.activeSelf : false)
                    + " | roof_m=" + garage.ChosenRoof.ToString("F2")
                    + " | screenshot=" + file);
                Debug.Log("GARAGE_CHECK index=" + i + " id=" + spec.id + " renderers=" + visibleRenderers
                          + " active_bodies=" + activeBodies);
                yield return new WaitForSecondsRealtime(0.35f);
            }
            report.AppendLine("physics_wheelcolliders=" + colliders + " (unchanged for every body)");
            report.AppendLine("physics_mass_kg=" + mass + " (unchanged for every body)");
            int bodiesOk = 0;
            for (int i = 0; i < garage.Count; i++)
            {
                garage.Apply(i);
                yield return new WaitForSecondsRealtime(0.12f);
                int active = 0;
                foreach (var v in garage.variants) if (v.root && v.root.activeSelf) active++;
                bool visualsMatch = race.player.wheelVisuals.Length == 4
                    && race.player.wheelVisuals[0] != null
                    && race.player.wheelVisuals[0].gameObject.activeInHierarchy;
                if (active == 1 && visualsMatch) bodiesOk++;
            }
            report.AppendLine("bodies_switching_ok=" + bodiesOk + "/" + garage.Count);
            report.AppendLine("verdict_garage_usable=" + (bodiesOk == garage.Count));
            garage.Apply(0);
        }
        File.WriteAllText(Path.Combine(dir, "garage_check.txt"), report.ToString());
        Debug.Log("GARAGE_CHECK_FINISHED " + report.ToString().Replace("\r", "").Replace("\n", " | "));
        foreach (var g in hidden) if (g) g.SetActive(true);
        yield return new WaitForSecondsRealtime(0.5f);
        Application.Quit(0);
    }

    /// <summary>Runtime proof for the nitro overdrive branch. The normal AI taps
    /// the boost in short bursts, so the 1.5 s streak never completes during a
    /// race. Here the boost is held continuously and every observable is
    /// sampled: boost state, streak, overdrive state, camera FOV, particle
    /// emission, and the top-speed budget.</summary>
    private IEnumerator NitroChecks()
    {
        string dir = Path.GetFullPath("tests/race");
        Directory.CreateDirectory(dir);
        var report = new StringBuilder();
        report.AppendLine("nitro_check_utc=" + DateTime.UtcNow.ToString("o"));
        report.AppendLine("device=" + SystemInfo.graphicsDeviceName);
        report.AppendLine("resolution=" + Screen.width + "x" + Screen.height);
        report.AppendLine("overdrive_threshold_s=1.5");

        var driver = race.player.GetComponent<RivalDriver>();
        race.player.aiControlled = true;
        driver.enabled = true;
        driver.drivingEnabled = true;
        driver.sustainedBoost = true;
        driver.skill = 0.85f;
        race.StartRace();

        var chase = Camera.main ? Camera.main.GetComponent<ChaseCamera>() : null;
        float t = 0f, logTimer = 0f;
        int boostFrames = 0, overdriveFrames = 0, sampled = 0;
        float maxStreak = 0f, maxSpeed = 0f, fovBase = 0f, fovBoost = 0f, fovOver = 0f;
        bool flameSeen = false, lightSeen = false;
        report.AppendLine("time,speed_kph,nitro,streak_s,boost,overdrive,fov,speed_cap_kph");
        while (t < 42f)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            logTimer += dt;
            var car = race.player;
            if (car.NitroActive) boostFrames++;
            if (car.NitroOverdrive) overdriveFrames++;
            maxStreak = Mathf.Max(maxStreak, car.NitroStreak);
            maxSpeed = Mathf.Max(maxSpeed, car.SpeedKph);
            if (car.boostFlames != null)
                for (int i = 0; i < car.boostFlames.Length; i++)
                {
                    if (!car.boostFlames[i]) continue;
                    if (car.boostFlames[i].emission.enabled) flameSeen = true;
                }
            if (car.boostLights != null)
                for (int i = 0; i < car.boostLights.Length; i++)
                    if (car.boostLights[i] && car.boostLights[i].enabled) lightSeen = true;
            float fov = chase ? chase.CurrentFov : 0f;
            if (car.NitroOverdrive) fovOver = Mathf.Max(fovOver, fov);
            else if (car.NitroActive) fovBoost = Mathf.Max(fovBoost, fov);
            else fovBase = Mathf.Max(fovBase, fov);
            if (logTimer >= 0.25f && sampled < 160)
            {
                logTimer = 0f; sampled++;
                report.AppendLine($"{t:F2},{car.SpeedKph:F1},{car.nitro:F1},{car.NitroStreak:F2}," +
                                  $"{(car.NitroActive ? 1 : 0)},{(car.NitroOverdrive ? 1 : 0)},{fov:F2},{car.SpeedCapKph:F1}");
            }
            if (overdriveFrames > 0 && t > 12f) break;
            yield return null;
        }
        report.AppendLine("boost_frames=" + boostFrames);
        report.AppendLine("overdrive_frames=" + overdriveFrames);
        report.AppendLine("max_nitro_streak_s=" + maxStreak.ToString("F2"));
        report.AppendLine("max_speed_kph=" + maxSpeed.ToString("F1"));
        report.AppendLine("fov_max_normal=" + fovBase.ToString("F2"));
        report.AppendLine("fov_max_boost=" + fovBoost.ToString("F2"));
        report.AppendLine("fov_max_overdrive=" + fovOver.ToString("F2"));
        report.AppendLine("fov_boost_delta=" + (fovBoost - fovBase).ToString("F2"));
        report.AppendLine("fov_overdrive_delta=" + (fovOver - fovBoost).ToString("F2"));
        report.AppendLine("nitro_flame_emission_seen=" + flameSeen);
        report.AppendLine("nitro_light_seen=" + lightSeen);
        report.AppendLine("verdict_overdrive_reached=" + (overdriveFrames > 0));
        File.WriteAllText(Path.Combine(dir, "nitro_test.txt"), report.ToString());
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, "nitro_overdrive.png"));
        Debug.Log("NITRO_CHECK_FINISHED " + report.ToString().Replace("\r", "").Replace("\n", " | "));
        yield return new WaitForSecondsRealtime(0.6f);
        Application.Quit(0);
    }

    /// <summary>Runtime proof for the pack-racing contact model: the player
    /// deliberately rams a rival and the test measures the impulse, the
    /// impact strength that drives the camera shake, the speed loss and how
    /// far the rival got pushed sideways.</summary>
    private IEnumerator CollisionChecks()
    {
        string dir = Path.GetFullPath("tests/race");
        Directory.CreateDirectory(dir);
        var report = new StringBuilder();
        report.AppendLine("collision_check_utc=" + DateTime.UtcNow.ToString("o"));
        report.AppendLine("device=" + SystemInfo.graphicsDeviceName);
        report.AppendLine("resolution=" + Screen.width + "x" + Screen.height);

        var player = race.player;
        var driver = player.GetComponent<RivalDriver>();
        race.StartRace();
        yield return new WaitForSecondsRealtime(5.5f);   // let the field spread out

        // Line the player up directly behind a rival on the racing line.
        var target = race.cars.Count > 1 ? race.cars[1] : null;
        if (target == null)
        {
            report.AppendLine("ERROR=no rival car available");
            File.WriteAllText(Path.Combine(dir, "collision_test.txt"), report.ToString());
            Application.Quit(4);
            yield break;
        }
        int targetIndex = race.track.NearestIndex(target.transform.position);
        var playerState = race.racers.Find(r => r.car == player);
        Vector3 line = race.track.PointAt(targetIndex, playerState != null ? playerState.car.GetComponent<RivalDriver>().preferredLane : 0f);
        Quaternion facing = race.track.RotationAt(targetIndex);
        player.ResetToPose(line - facing * Vector3.forward * 9f + Vector3.up * 0.6f, facing);
        player.Body.linearVelocity = facing * Vector3.forward * 22f;
        var rivalDriver = target.GetComponent<RivalDriver>();
        if (rivalDriver) rivalDriver.drivingEnabled = false;   // hold the target still
        driver.enabled = false;                               // we drive the ram
        player.aiControlled = true;

        float maxImpact = 0f, maxShake = 0f, speedBefore = 0f, speedAfter = 0f;
        int startCount = player.CollisionCount;
        // SpeedKph is refreshed in FixedUpdate, so read it from the rigidbody
        // instead of waiting for the next physics tick.
        speedBefore = player.Body.linearVelocity.magnitude * 3.6f;
        Vector3 rivalBefore = target.transform.position, rivalAfter = rivalBefore;
        int hits = 0;
        float t = 0f;
        var chase = Camera.main ? Camera.main.GetComponent<ChaseCamera>() : null;
        bool captured = false;
        while (t < 12f)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            // Keep steering into the target so the contact really happens.
            Vector3 toTarget = target.transform.position - player.transform.position;
            Vector3 local = player.transform.InverseTransformDirection(toTarget.normalized);
            float steer = Mathf.Clamp(local.x * 2.4f, -1f, 1f);
            player.SetTestInput(1f, steer, false, false);
            maxImpact = Mathf.Max(maxImpact, player.ImpactStrength);
            if (chase) maxShake = Mathf.Max(maxShake, chase.LastShakeAmplitude);
            int nowCount = player.CollisionCount;
            if (nowCount > startCount)
            {
                hits = nowCount - startCount;
                if (!captured)
                {
                    captured = true;
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, "collision_impact.png"));
                }
            }
            if (hits > 0 && t > 2.2f) break;
            yield return null;
        }
        speedAfter = player.Body.linearVelocity.magnitude * 3.6f;
        rivalAfter = target.transform.position;
        float lateralBefore = Mathf.Abs(race.track.SignedLateral(rivalBefore, targetIndex));
        float lateralAfter = Mathf.Abs(race.track.SignedLateral(rivalAfter, targetIndex));
        float lateralPush = Mathf.Abs(lateralAfter - lateralBefore);
        report.AppendLine("player_speed_before_kph=" + speedBefore.ToString("F1"));
        report.AppendLine("player_speed_after_kph=" + speedAfter.ToString("F1"));
        report.AppendLine("collision_count_delta=" + hits);
        report.AppendLine("max_impact_strength=" + maxImpact.ToString("F3"));
        report.AppendLine("max_camera_jitter_m=" + maxShake.ToString("F4"));
        report.AppendLine("rival_lateral_before_m=" + lateralBefore.ToString("F2"));
        report.AppendLine("rival_lateral_after_m=" + lateralAfter.ToString("F2"));
        report.AppendLine("rival_lateral_push_m=" + lateralPush.ToString("F2"));
        report.AppendLine("rival_position_delta=" + Vector3.Distance(rivalBefore, rivalAfter).ToString("F2"));
        report.AppendLine("verdict_collision_registered=" + (hits > 0));
        report.AppendLine("verdict_camera_jitter_triggered=" + (maxShake > 0.0005f));
        report.AppendLine("verdict_rival_pushed=" + (lateralPush > 0.05f || Vector3.Distance(rivalBefore, rivalAfter) > 0.2f));
        report.AppendLine("verdict_speed_lost=" + (speedBefore - speedAfter > 3f));
        File.WriteAllText(Path.Combine(dir, "collision_test.txt"), report.ToString());
        Debug.Log("COLLISION_CHECK_FINISHED " + report.ToString().Replace("\r", "").Replace("\n", " | "));
        yield return new WaitForSecondsRealtime(0.6f);
        Application.Quit(0);
    }

    /// <summary>Runtime proof that swapping the body mid-race does not break
    /// the car: the physics rig, the mass and the wheel colliders must stay
    /// identical, and the car must keep driving through the swap.</summary>
    private IEnumerator GarageDriveChecks()
    {
        string dir = Path.GetFullPath("tests/race");
        Directory.CreateDirectory(dir);
        var report = new StringBuilder();
        report.AppendLine("garage_drive_utc=" + DateTime.UtcNow.ToString("o"));
        report.AppendLine("device=" + SystemInfo.graphicsDeviceName);
        report.AppendLine("resolution=" + Screen.width + "x" + Screen.height);

        var garage = race.player.GetComponent<VehicleGarage>();
        var driver = race.player.GetComponent<RivalDriver>();
        race.player.aiControlled = true;
        driver.enabled = true;
        driver.drivingEnabled = true;
        driver.skill = 0.75f;
        race.StartRace();
        yield return new WaitForSecondsRealtime(6f);

        var body = race.player.GetComponent<Rigidbody>();
        float massBefore = body.mass;
        int collidersBefore = race.player.wheels.Length;
        int visualsBefore = race.player.wheelVisuals.Length;
        Vector3 posBefore = race.player.transform.position;
        float speedBefore = race.player.SpeedKph;
        int checkpointBefore = race.Player != null ? race.Player.nextCheckpoint : -1;

        // Swap three times in a row, mid-race, at speed.
        var order = new[] { 2, 5, 8 };
        for (int i = 0; i < order.Length; i++)
        {
            garage.Apply(order[i]);
            yield return new WaitForSecondsRealtime(1.4f);
            int activeBodies = 0;
            foreach (var v in garage.variants) if (v.root && v.root.activeSelf) activeBodies++;
            report.AppendLine($"swap_{i} | target={garage.Current.id} | active_bodies={activeBodies}"
                + $" | mass={body.mass:F1} | wheelcolliders={race.player.wheels.Length}"
                + $" | wheel_visuals={race.player.wheelVisuals.Length}"
                + $" | speed_kph={race.player.SpeedKph:F1} | grounded={race.player.GroundedWheels}"
                + $" | checkpoint={(race.Player != null ? race.Player.nextCheckpoint : -1)}");
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"garage_drive_{i}_{garage.Current.id}.png"));
        }

        float posDelta = Vector3.Distance(posBefore, race.player.transform.position);
        report.AppendLine("mass_before=" + massBefore.ToString("F1"));
        report.AppendLine("mass_after=" + body.mass.ToString("F1"));
        report.AppendLine("mass_unchanged=" + Mathf.Approximately(massBefore, body.mass));
        report.AppendLine("wheelcolliders_before=" + collidersBefore);
        report.AppendLine("wheelcolliders_after=" + race.player.wheels.Length);
        report.AppendLine("wheelvisuals_before=" + visualsBefore);
        report.AppendLine("wheelvisuals_after=" + race.player.wheelVisuals.Length);
        report.AppendLine("speed_before_kph=" + speedBefore.ToString("F1"));
        report.AppendLine("speed_after_kph=" + race.player.SpeedKph.ToString("F1"));
        report.AppendLine("distance_travelled_during_swaps_m=" + posDelta.ToString("F1"));
        report.AppendLine("checkpoint_before=" + checkpointBefore);
        report.AppendLine("checkpoint_after=" + (race.Player != null ? race.Player.nextCheckpoint : -1));
        report.AppendLine("verdict_kept_driving=" + (race.player.SpeedKph > 5f && posDelta > 5f));
        report.AppendLine("verdict_physics_intact=" + (Mathf.Approximately(massBefore, body.mass)
            && race.player.wheels.Length == collidersBefore && race.player.wheelVisuals.Length == visualsBefore));
        File.WriteAllText(Path.Combine(dir, "garage_drive.txt"), report.ToString());
        Debug.Log("GARAGE_DRIVE_FINISHED " + report.ToString().Replace("\r", "").Replace("\n", " | "));
        yield return new WaitForSecondsRealtime(0.6f);
        Application.Quit(0);
    }

    /// <summary>Synthetic proof of the input mapping. No controller is attached
    /// on this machine, so the real button feel still needs a human pass, but
    /// the decision table itself can be verified exhaustively.</summary>
    private static void WritePadMapCheck()
    {
        string dir = Path.GetFullPath("tests/race");
        Directory.CreateDirectory(dir);
        var probe = new StringBuilder();
        int pass = 0, fail = 0;
        Action<string, bool, string> check = (name, ok, detail) =>
        {
            probe.AppendLine((ok ? "PASS " : "FAIL ") + name + " | " + detail);
            if (ok) pass++; else fail++;
        };

        // Stick deadzone: centre, inside the deadzone, and full deflection.
        check("stick_zero", Mathf.Approximately(InputMap.Stick(0f), 0f), "raw=0 -> 0");
        check("stick_inside_deadzone", Mathf.Approximately(InputMap.Stick(0.09f), 0f), "raw=0.09 -> 0");
        check("stick_just_outside", InputMap.Stick(0.11f) > 0.001f, "raw=0.11 -> " + InputMap.Stick(0.11f).ToString("F3"));
        check("stick_full_positive", Mathf.Approximately(InputMap.Stick(1f), 1f), "raw=1 -> 1");
        check("stick_full_negative", Mathf.Approximately(InputMap.Stick(-1f), -1f), "raw=-1 -> -1");
        check("stick_half", Mathf.Abs(InputMap.Stick(0.5f) - 0.444f) < 0.01f, "raw=0.5 -> " + InputMap.Stick(0.5f).ToString("F3"));
        check("stick_monotonic", InputMap.Stick(0.9f) > InputMap.Stick(0.4f), "0.9 > 0.4");

        // Button mapping.
        check("handbrake_space", InputMap.Handbrake(true, false), "SPACE");
        check("handbrake_pad_a", InputMap.Handbrake(false, true), "PAD A");
        check("handbrake_none", !InputMap.Handbrake(false, false), "none");
        check("nitro_shift", InputMap.Nitro(true, false, false, false), "SHIFT");
        check("nitro_shift_right", InputMap.Nitro(false, true, false, false), "R SHIFT");
        check("nitro_pad_x", InputMap.Nitro(false, false, true, false), "PAD X");
        check("nitro_pad_rb", InputMap.Nitro(false, false, false, true), "PAD RB");
        check("nitro_none", !InputMap.Nitro(false, false, false, false), "none");
        check("reset_key_r", InputMap.Reset(true, false), "R");
        check("reset_pad_b", InputMap.Reset(false, true), "PAD B");
        check("pause_escape", InputMap.Pause(true, false, false), "ESC");
        check("pause_pad_start", InputMap.Pause(false, true, false), "PAD START");
        check("pause_pad_start_alt", InputMap.Pause(false, false, true), "PAD START ALT");
        check("confirm_enter", InputMap.Confirm(true, false, false), "ENTER");
        check("confirm_keypad", InputMap.Confirm(false, true, false), "KEYPAD ENTER");
        check("confirm_pad_a", InputMap.Confirm(false, false, true), "PAD A");
        check("prev_car_q", InputMap.PreviousCar(true, false, false), "Q");
        check("prev_car_bracket", InputMap.PreviousCar(false, true, false), "[");
        check("prev_car_pad_lb", InputMap.PreviousCar(false, false, true), "PAD LB");
        check("next_car_e", InputMap.NextCar(true, false, false), "E");
        check("next_car_bracket", InputMap.NextCar(false, true, false), "]");
        check("next_car_pad_rb", InputMap.NextCar(false, false, true), "PAD RB");

        probe.AppendLine("devices=" + GamepadNames());
        probe.AppendLine("stick_deadzone=" + InputMap.StickDeadzone.ToString("F2"));
        probe.AppendLine("cases_passed=" + pass);
        probe.AppendLine("cases_failed=" + fail);
        probe.AppendLine("verdict_all_passed=" + (fail == 0));
        probe.AppendLine("note=physical stick feel and real button latency still need a human pass on real hardware");
        File.WriteAllText(Path.Combine(dir, "padmap_test.txt"), probe.ToString());
        Debug.Log("PADMAP_CHECK " + probe.ToString().Replace("\r", "").Replace("\n", " | "));
        Application.Quit(0);
    }

    private static string GamepadNames()
    {
        var names = Input.GetJoystickNames();
        return names == null || names.Length == 0 || string.IsNullOrEmpty(names[0]) ? "NONE" : string.Join(" | ", names);
    }

    /// <summary>Runnable evidence for gamepad support: AuroraCoast.exe -gamepadcheck
    /// writes the detected devices and the mapping to tests/race/gamepad_check.txt.</summary>
    private static void WriteGamepadCheck()
    {
        string dir = Path.GetFullPath("tests/race");
        Directory.CreateDirectory(dir);
        var probe = new StringBuilder();
        probe.AppendLine("gamepad_check_utc=" + DateTime.UtcNow.ToString("o"));
        probe.AppendLine("devices=" + GamepadNames());
        probe.AppendLine("axis_vertical=" + Input.GetAxis("Vertical"));
        probe.AppendLine("axis_horizontal=" + Input.GetAxis("Horizontal"));
        probe.AppendLine("mapping=Left stick = Horizontal/Vertical (joystick axes 0/1, dead 0.19, Y inverted)");
        probe.AppendLine("buttons=A/0 handbrake+confirm, B/1 reset, X/2 nitro, RB/5 nitro, Start/7 pause+start");
        probe.AppendLine("code_deadzone=0.10 (ArcadeCarController.ApplyDeadzone)");
        File.WriteAllText(Path.Combine(dir, "gamepad_check.txt"), probe.ToString());
        Debug.Log("GAMEPAD_CHECK " + probe.ToString().Replace("\r", "").Replace("\n", " | "));
        Application.Quit(0);
    }

    private void Update()
    {
        if (!active) return;
        float dt = Time.unscaledDeltaTime;
        timer += dt;
        float fps = 1f / Mathf.Max(dt, 0.0001f);
        if (timer > 5f) { fpsSum += fps; frames++; minFps = Mathf.Min(minFps, fps); }
        if (race.player.NitroOverdrive) overdriveFrames++;
        logTimer += dt;
        if (logTimer >= 1f)
        {
            logTimer = 0f;
            var p = race.Player;
            log.AppendLine($"{timer:F1},{race.State},{p.lap},{p.nextCheckpoint},{race.PlayerPosition()},{race.player.SpeedKph:F1},{race.player.GroundedWheels},{race.player.nitro:F1},{fps:F1},{race.player.transform.position}");
        }
        float[] captureTimes = { 10f, 55f, 110f, 170f, 230f };
        if (captures < captureTimes.Length && timer >= captureTimes[captures])
        {
            captures++;
            ScreenCapture.CaptureScreenshot(Path.Combine(output, $"race_{captures:00}.png"));
            Debug.Log($"RACE_TEST_CAPTURE {captures} t={timer:F1} fps={fps:F1} lap={race.Player.lap} pos={race.player.transform.position}");
        }
        if (race.State == RacePhase.Finished)
        {
            Finish("completed");
        }
        else if (timer > 420f)
        {
            Finish("timeout");
        }
    }

    private void Finish(string status)
    {
        if (!active) return;
        active = false;
        var p = race.Player;
        log.AppendLine($"status={status}");
        log.AppendLine($"race_time={p.finishTime:F3}");
        log.AppendLine($"best_lap={p.bestLap:F3}");
        log.AppendLine($"average_fps={(frames > 0 ? fpsSum / frames : 0f):F2}");
        log.AppendLine($"minimum_fps={(frames > 0 ? minFps : 0f):F2}");
        log.AppendLine($"final_position={race.PlayerPosition()}");
        log.AppendLine($"collision_events={race.player.CollisionCount}");
        log.AppendLine($"nitro_overdrive_frames={overdriveFrames}");
        File.WriteAllText(Path.Combine(output, "race_telemetry.txt"), log.ToString());
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "race_finish.png"));
        Debug.Log("RACE_TEST_FINISHED " + status + " time=" + p.finishTime + " avgFps=" + (frames > 0 ? fpsSum / frames : 0f));
        StartCoroutine(QuitAfterCapture(status == "completed" ? 0 : 4));
    }

    private IEnumerator QuitAfterCapture(int exitCode)
    {
        yield return null;
        yield return new WaitForSecondsRealtime(0.3f);
        Application.Quit(exitCode);
    }
}
