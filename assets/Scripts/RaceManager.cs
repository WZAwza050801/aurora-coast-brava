using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum RacePhase { Menu, Countdown, Racing, Paused, Finished }

[Serializable]
public sealed class RacerState
{
    public string name;
    public ArcadeCarController car;
    public RivalDriver driver;
    public int nearest;
    public int previousNearest;
    public int lap;
    public int nextCheckpoint = 1;
    public float continuousIndex;
    public float progress;
    public float lapStarted;
    public float bestLap = float.MaxValue;
    public float lastLap;
    public float finishTime;
    public bool finished;
    public bool wrongWay;
    public bool offroad;
    public int resets;
    public Vector3 previousPosition;
    public Vector3 gridPosition;
    public Quaternion gridRotation;
    public int gridIndex;
}

public sealed class RaceManager : MonoBehaviour
{
    public TrackPath track;
    public ArcadeCarController player;
    public List<ArcadeCarController> cars = new List<ArcadeCarController>();
    public List<RacerState> racers = new List<RacerState>();
    public RacePhase State { get; private set; } = RacePhase.Menu;
    public float Elapsed { get; private set; }
    public float Countdown { get; private set; }
    public float EffectStrength = 0.65f;
    public int Quality = 1;
    public string Notice { get; private set; } = "";
    public RacerState Player { get { return racers.Find(r => r.car == player); } }
    private float countdown;
    private float noticeTimer;
    private float finishTimer;
    private static bool autoStartOnLoad;

    private void Start()
    {
        track.Initialize();
        racers.Clear();
        var garage = player.GetComponent<VehicleGarage>();
        if (garage == null) Debug.LogError("GARAGE_MISSING: VehicleGarage component not found on the player car; the car switcher will not work.");
        string playerLabel = garage && garage.Count > 0 ? "YOU / " + garage.Label() : "YOU / AURORA GT";
        for (int i = 0; i < cars.Count; i++)
        {
            var car = cars[i];
            car.controlsLocked = true;
            car.track = track;
            car.race = this;
            car.trackIndex = track.NearestIndex(car.transform.position);
            float continuous = RelativeToStart(car.trackIndex);
            var state = new RacerState
            {
                name = i == 0 ? playerLabel : "RIVAL 0" + i,
                car = car,
                driver = car.GetComponent<RivalDriver>(),
                nearest = car.trackIndex,
                previousNearest = car.trackIndex,
                continuousIndex = continuous,
                progress = continuous,
                previousPosition = car.transform.position,
                gridPosition = car.transform.position,
                gridRotation = car.transform.rotation,
                gridIndex = car.trackIndex
            };
            if (state.driver) state.driver.race = this;
            racers.Add(state);
        }
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        if (autoStartOnLoad) { autoStartOnLoad = false; StartRace(); }
    }

    private void Update()
    {
        // The garage turntable is on whenever the race is not running and the
        // chase camera is back behind the car as soon as the countdown starts.
        var chase = Camera.main ? Camera.main.GetComponent<ChaseCamera>() : null;
        if (chase && chase.showroom != (State == RacePhase.Menu))
        {
            if (State == RacePhase.Menu) chase.showroom = true;
            else chase.ExitShowroom();
        }
        bool pausePressed = InputMap.Pause(
            Input.GetKeyDown(KeyCode.Escape),
            Input.GetKeyDown(KeyCode.JoystickButton9),
            Input.GetKeyDown(KeyCode.JoystickButton7));
        bool confirm = InputMap.Confirm(
            Input.GetKeyDown(KeyCode.Return),
            Input.GetKeyDown(KeyCode.KeypadEnter),
            Input.GetKeyDown(KeyCode.JoystickButton0));
        if (pausePressed)
        {
            if (State == RacePhase.Racing) Pause();
            else if (State == RacePhase.Paused) Resume();
        }
        if (State == RacePhase.Menu)
        {
            if (confirm) StartRace();
            return;
        }
        if (State == RacePhase.Paused) return;
        if (State == RacePhase.Finished)
        {
            if (confirm) Restart();
            return;
        }
        if (State == RacePhase.Countdown)
        {
            countdown -= Time.deltaTime;
            Countdown = countdown;
            if (countdown <= 0f)
            {
                State = RacePhase.Racing;
                foreach (var r in racers) r.car.controlsLocked = false;
                ShowNotice("GO — MAKE THE COAST YOURS", 1.2f);
            }
        }
        if (State != RacePhase.Racing) return;
        Elapsed += Time.deltaTime;
        noticeTimer -= Time.deltaTime;
        if (noticeTimer <= 0f) Notice = "";
        for (int i = 0; i < racers.Count; i++) UpdateRacer(racers[i]);
        racers.Sort((a, b) => b.progress.CompareTo(a.progress));
        if (Player.finished)
        {
            finishTimer += Time.deltaTime;
            if (finishTimer > 0.8f)
            {
                State = RacePhase.Finished;
                foreach (var r in racers) r.car.controlsLocked = true;
                SaveBestLap();
            }
        }
    }

    private void UpdateRacer(RacerState r)
    {
        if (r.finished) return;
        Vector3 currentPosition = r.car.transform.position;
        r.nearest = track.NearestIndex(currentPosition, r.nearest);
        r.car.trackIndex = r.nearest;
        int delta = r.nearest - r.previousNearest;
        if (delta > track.Count / 2) delta -= track.Count;
        else if (delta < -track.Count / 2) delta += track.Count;
        r.continuousIndex += delta;
        r.previousNearest = r.nearest;

        float lateral = track.LateralDistance(currentPosition, r.nearest);
        r.offroad = lateral > 8.5f;
        float direction = Vector3.Dot(r.car.Body.linearVelocity, track.At(r.nearest).tangent);
        r.wrongWay = direction < -3f && r.car.SpeedKph > 12f;

        int target = track.Wrap(r.nextCheckpoint * track.Count / TrackPath.CheckpointCount);
        if (CrossedCheckpoint(r.previousPosition, currentPosition, target))
        {
            if (r.nextCheckpoint == 0)
            {
                float lapTime = Elapsed - r.lapStarted;
                r.lastLap = lapTime;
                r.bestLap = Mathf.Min(r.bestLap, lapTime);
                r.lap++;
                r.lapStarted = Elapsed;
                r.nextCheckpoint = 1;
                if (r.car == player) ShowNotice(r.lap >= 2 ? "FINISH" : "LAP 2 — PUSH CLEANLY", 2f);
                if (r.lap >= 2)
                {
                    r.finished = true;
                    r.finishTime = Elapsed;
                    r.car.controlsLocked = true;
                    r.progress = 100000f - r.finishTime;
                    r.previousPosition = currentPosition;
                    return;
                }
            }
            else r.nextCheckpoint = (r.nextCheckpoint + 1) % TrackPath.CheckpointCount;
        }

        float expected = r.lap * track.Count + r.nextCheckpoint * track.Count / TrackPath.CheckpointCount;
        if (r.nextCheckpoint == 0) expected = (r.lap + 1) * track.Count;
        if (r.continuousIndex > expected + 12f && r.car == player && Notice == "")
            ShowNotice("CHECKPOINT MISSED — TURN BACK OR RESET", 2.5f);
        r.progress = Mathf.Min(r.continuousIndex, expected - 0.25f);
        r.previousPosition = currentPosition;
    }

    private bool CrossedCheckpoint(Vector3 previous, Vector3 current, int index)
    {
        TrackSample sample = track.At(index);
        float before = Vector3.Dot(previous - sample.position, sample.tangent);
        float after = Vector3.Dot(current - sample.position, sample.tangent);
        if (before >= 0f || after < 0f || after <= before) return false;
        float t = Mathf.Clamp01(-before / (after - before));
        Vector3 crossing = Vector3.Lerp(previous, current, t);
        return Mathf.Abs(Vector3.Dot(crossing - sample.position, sample.side)) < 8.5f
            && Mathf.Abs(crossing.y - sample.position.y) < 6f;
    }

    public void StartRace()
    {
        if (State != RacePhase.Menu) return;
        State = RacePhase.Countdown;
        countdown = 4f;
        Countdown = countdown;
        Elapsed = 0f;
        finishTimer = 0f;
        noticeTimer = 0f;
        Notice = "";
        foreach (var r in racers)
        {
            r.car.ResetToPose(r.gridPosition, r.gridRotation);
            r.car.controlsLocked = true;
            r.car.trackIndex = r.gridIndex;
            r.nearest = r.gridIndex;
            r.previousNearest = r.gridIndex;
            r.continuousIndex = RelativeToStart(r.gridIndex);
            r.progress = r.continuousIndex;
            r.previousPosition = r.gridPosition;
            r.lap = 0;
            r.nextCheckpoint = 1;
            r.lapStarted = 0f;
            r.bestLap = float.MaxValue;
            r.lastLap = 0f;
            r.finishTime = 0f;
            r.finished = false;
            r.wrongWay = false;
            r.offroad = false;
            r.resets = 0;
        }
        racers.Sort((a, b) => b.progress.CompareTo(a.progress));
    }

    public void Pause()
    {
        if (State != RacePhase.Racing) return;
        State = RacePhase.Paused;
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }
    public void Resume()
    {
        if (State != RacePhase.Paused) return;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        State = RacePhase.Racing;
    }
    public void Restart()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        autoStartOnLoad = true; // "RACE AGAIN" / "RESTART" goes straight into countdown
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        autoStartOnLoad = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    public void Quit()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Application.Quit();
    }

    public void ResetPlayer()
    {
        if (Player == null || (State != RacePhase.Racing && State != RacePhase.Countdown)) return;
        Player.car.ResetToTrack(Player.nearest);
        SynchronizeAfterReset(Player, true);
    }

    public void NotifyCarReset(ArcadeCarController car)
    {
        RacerState state = racers.Find(r => r.car == car);
        if (state != null) SynchronizeAfterReset(state, true);
    }

    private void SynchronizeAfterReset(RacerState r, bool countReset)
    {
        int nearest = track.NearestIndex(r.car.transform.position);
        int delta = nearest - r.previousNearest;
        if (delta > track.Count / 2) delta -= track.Count;
        else if (delta < -track.Count / 2) delta += track.Count;
        r.continuousIndex += delta;
        r.nearest = nearest;
        r.previousNearest = nearest;
        r.car.trackIndex = nearest;
        r.previousPosition = r.car.transform.position;
        r.wrongWay = false;
        if (countReset) r.resets++;
    }

    private float RelativeToStart(int index)
    {
        return index > track.Count / 2 ? index - track.Count : index;
    }

    public int PlayerPosition()
    {
        if (Player == null) return 1;
        for (int i = 0; i < racers.Count; i++) if (racers[i] == Player) return i + 1;
        return 1;
    }

    public RivalDriver FindCarAhead(RivalDriver driver, float range)
    {
        RacerState self = null, best = null;
        float bestGap = float.MaxValue;
        foreach (var r in racers) if (r.driver == driver) self = r;
        if (self == null) return null;
        foreach (var r in racers)
        {
            if (r == self || r.finished) continue;
            float gap = (r.progress - self.progress) / track.Count * track.TotalLength;
            if (gap > 0f && gap < range && gap < bestGap) { best = r; bestGap = gap; }
        }
        return best != null ? best.driver : null;
    }

    public float OvertakeSide(RivalDriver driver, RivalDriver ahead)
    {
        float lane = driver.preferredLane;
        if (Mathf.Abs(lane) < 0.3f) lane = Mathf.Sin(driver.GetInstanceID() * 0.13f) > 0f ? 1f : -1f;
        return Mathf.Sign(lane);
    }

    public void ShowNotice(string text, float seconds) { Notice = text; noticeTimer = seconds; }

    private void SaveBestLap()
    {
        if (Player == null || Player.bestLap == float.MaxValue) return;
        float saved = PlayerPrefs.GetFloat("AuroraCoast.BestLap", float.MaxValue);
        if (Player.bestLap < saved) PlayerPrefs.SetFloat("AuroraCoast.BestLap", Player.bestLap);
        PlayerPrefs.Save();
    }

    public static string FormatTime(float seconds)
    {
        if (seconds < 0f || seconds == float.MaxValue) return "--:--.---";
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float rest = seconds - minutes * 60f;
        return string.Format("{0:00}:{1:00.000}", minutes, rest);
    }
}
