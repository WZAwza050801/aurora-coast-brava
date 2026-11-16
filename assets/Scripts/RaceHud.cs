using UnityEngine;

public sealed class RaceHud : MonoBehaviour
{
    public RaceManager race;
    public TrackPath track;
    private GUIStyle title, subtitle, big, label, small, center, button, rowButton;
    private Texture2D minimap;
    private VehicleGarage garage;
    private Vector2 mapMin, mapMax;
    private float fps = 60f;
    private int pauseIndex;
    private float lastNav, gamepadNextCheck;
    private string gamepadLabel = "GAMEPAD: SCANNING";
    private static readonly string[] PauseOptions =
    {
        "RESUME  [ESC / START]", "RESTART RACE", "MAIN MENU", "CHANGE CAR",
        "QUALITY", "SHAKE / FX", "CAMERA FOV", "MASTER VOLUME", "QUIT"
    };

    private void Start()
    {
        if (track) { track.Initialize(); BuildMinimap(); }
    }

    private void Update()
    {
        if (Time.unscaledTime >= gamepadNextCheck)
        {
            gamepadNextCheck = Time.unscaledTime + 3f;
            string[] names = Input.GetJoystickNames();
            gamepadLabel = names == null || names.Length == 0 || string.IsNullOrEmpty(names[0])
                ? "GAMEPAD: NONE DETECTED" : "GAMEPAD: " + names[0];
        }
        if (race && race.State == RacePhase.Menu)
        {
            if (InputMap.PreviousCar(
                    Input.GetKeyDown(KeyCode.Q),
                    Input.GetKeyDown(KeyCode.LeftBracket),
                    Input.GetKeyDown(KeyCode.JoystickButton4)))
                CycleGarage(-1);
            if (InputMap.NextCar(
                    Input.GetKeyDown(KeyCode.E),
                    Input.GetKeyDown(KeyCode.RightBracket),
                    Input.GetKeyDown(KeyCode.JoystickButton5)))
                CycleGarage(1);
        }
        if (!race || race.State != RacePhase.Paused) { pauseIndex = 0; return; }
        // IMGUI has no gamepad focus navigation, so drive the pause menu from Update.
        float v = Input.GetAxisRaw("Vertical"), h = Input.GetAxisRaw("Horizontal");
        if (Time.unscaledTime - lastNav > 0.22f)
        {
            if (Mathf.Abs(v) > 0.55f)
            {
                lastNav = Time.unscaledTime;
                pauseIndex = (pauseIndex + (v > 0f ? -1 : 1) + PauseOptions.Length) % PauseOptions.Length;
            }
            else if (Mathf.Abs(h) > 0.55f)
            {
                lastNav = Time.unscaledTime;
                AdjustPauseOption(Mathf.Sign(h) * 0.1f);
            }
        }
        if (InputMap.Confirm(
                Input.GetKeyDown(KeyCode.Return),
                Input.GetKeyDown(KeyCode.KeypadEnter),
                Input.GetKeyDown(KeyCode.JoystickButton0)))
            ActivatePauseOption();
    }

    private void Styles()
    {
        if (title != null) return;
        title = Style(54, new Color(0.92f, 0.97f, 1f), FontStyle.Bold);
        subtitle = Style(22, new Color(1f, 0.65f, 0.25f), FontStyle.Bold);
        big = Style(62, Color.white, FontStyle.Bold);
        label = Style(19, new Color(0.9f, 0.95f, 1f), FontStyle.Bold);
        small = Style(14, new Color(0.7f, 0.82f, 0.9f), FontStyle.Normal);
        center = Style(30, Color.white, FontStyle.Bold);
        center.alignment = TextAnchor.MiddleCenter;
        big.alignment = TextAnchor.MiddleRight;
        button = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold, fixedHeight = 46f };
        rowButton = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        rowButton.padding.left = 16;
    }
    private ChaseCamera Chase() { return Camera.main ? Camera.main.GetComponent<ChaseCamera>() : null; }

    private VehicleGarage Garage()
    {
        if (!garage && race && race.player) garage = race.player.GetComponent<VehicleGarage>();
        if (!garage && !garageMissingWarned && race && race.player)
        {
            // A missing component here means the scene holds a broken script
            // reference, and the garage would silently do nothing. Say so.
            garageMissingWarned = true;
            Debug.LogError("GARAGE_MISSING: VehicleGarage component not found on the player car.");
        }
        return garage;
    }

    private bool garageMissingWarned;

    /// <summary>Only the visible body changes: the shared physics rig stays fixed.</summary>
    private void CycleGarage(int delta)
    {
        var g = Garage();
        if (g == null || g.Count == 0) return;
        if (delta > 0) g.Next(); else g.Previous();
        var p = race.Player;
        if (p != null) p.name = "YOU / " + g.Label();
        Debug.Log("GARAGE_SELECT index=" + g.selected + " id=" + g.Current.id
                  + " label=" + g.Label() + " aero=" + g.Current.aeroKit
                  + " wheel_visuals=" + (race.player ? race.player.wheelVisuals.Length : 0));
    }
    private void ApplyQuality()
    {
        QualitySettings.shadowDistance = race.Quality == 0 ? 80f : race.Quality == 1 ? 160f : 260f;
    }
    private void ApplyEffects()
    {
        var chase = Chase();
        if (chase) chase.effects = race.EffectStrength;
    }
    private string PauseValue(int index)
    {
        var chase = Chase();
        var g = Garage();
        switch (index)
        {
            case 3: return g && g.Count > 0 ? (g.selected + 1) + "/" + g.Count + "  " + g.Label() : "N/A";
            case 4: return race.Quality == 0 ? "LOW" : race.Quality == 1 ? "HIGH" : "ULTRA";
            case 5: return Mathf.RoundToInt(race.EffectStrength * 100) + "%";
            case 6: return chase ? Mathf.RoundToInt(chase.baseFov) + " DEG" : "--";
            case 7: return Mathf.RoundToInt(AudioListener.volume * 100) + "%";
            default: return "";
        }
    }
    private void AdjustPauseOption(float delta)
    {
        var chase = Chase();
        switch (pauseIndex)
        {
            case 3: CycleGarage(delta > 0f ? 1 : -1); break;
            case 4: race.Quality = (race.Quality + (delta > 0f ? 1 : 2)) % 3; ApplyQuality(); break;
            case 5: race.EffectStrength = Mathf.Clamp01(race.EffectStrength + delta); ApplyEffects(); break;
            case 6: if (chase) chase.baseFov = Mathf.Clamp(chase.baseFov + delta * 20f, 52f, 72f); break;
            case 7: AudioListener.volume = Mathf.Clamp01(AudioListener.volume + delta); break;
        }
    }
    private void ActivatePauseOption()
    {
        switch (pauseIndex)
        {
            case 0: race.Resume(); break;
            case 1: race.Restart(); break;
            case 2: race.ReturnToMenu(); break;
            case 3: CycleGarage(1); break;
            case 4: race.Quality = (race.Quality + 1) % 3; ApplyQuality(); break;
            case 5: race.EffectStrength = race.EffectStrength >= 0.95f ? 0f : Mathf.Clamp01(race.EffectStrength + 0.2f); ApplyEffects(); break;
            case 6: AdjustPauseOption(1f); break;
            case 7: AudioListener.volume = AudioListener.volume >= 0.95f ? 0f : Mathf.Clamp01(AudioListener.volume + 0.1f); break;
            case 8: race.Quit(); break;
        }
    }
    private GUIStyle Style(int size, Color color, FontStyle weight)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight };
        s.normal.textColor = color;
        return s;
    }
    private void Panel(Rect rect, float alpha = 0.72f)
    {
        Color old = GUI.color;
        GUI.color = new Color(0.018f, 0.035f, 0.06f, alpha);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }
    private void Bar(Rect rect, float fill, Color color)
    {
        Color old = GUI.color;
        GUI.color = new Color(0.03f, 0.07f, 0.1f, 0.85f); GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = color; GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, (rect.width - 4) * Mathf.Clamp01(fill), rect.height - 4), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private void OnGUI()
    {
        if (!race || race.Player == null) return;
        Styles();
        fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.03f);
        if (race.State == RacePhase.Menu) { DrawMenu(); return; }
        DrawRaceHud();
        if (race.State == RacePhase.Countdown) DrawCountdown();
        if (race.State == RacePhase.Paused) DrawPause();
        if (race.State == RacePhase.Finished) DrawResults();
    }

    private void DrawMenu()
    {
        // The menu is split into a text column on the left and a showcase
        // window on the right, so the car on the turntable stays bright and
        // unobstructed instead of sitting behind a full-screen panel.
        Panel(new Rect(0, 0, Screen.width, Screen.height), 0.30f);
        Panel(new Rect(Screen.width * 0.045f, Screen.height * 0.09f, Screen.width * 0.47f, Screen.height * 0.83f), 0.88f);
        float x = Screen.width * 0.085f, y = Screen.height * 0.13f;
        GUI.Label(new Rect(x, y, 560, 66), "A U R O R A", title);
        GUI.Label(new Rect(x + 3, y + 66, 560, 32), "COSTA BRAVA / GOLDEN HOUR", subtitle);
        GUI.Label(new Rect(x + 3, y + 104, 570, 64), "One coastal road. Two laps. Four drivers.\nDrift the hairpins, earn nitro, own the straight.", label);
        GUI.Label(new Rect(x + 3, y + 176, 570, 118),
            "W / S or LEFT STICK    Throttle & brake\nA / D or LEFT STICK    Steering\n"
            + "SPACE / A    Handbrake drift\nSHIFT / X / RB    Nitro      R / B    Reset\n"
            + "ESC / START    Pause      ENTER / A    Confirm\n"
            + "Gamepad menus: stick or D-pad to select / adjust", small);
        var g = Garage();
        if (g != null && g.Count > 0)
        {
            GUI.Label(new Rect(x, y + 300, 300, 22), "GARAGE   Q / E   or   LB / RB", small);
            GUI.Label(new Rect(x, y + 322, 268, 30), (g.selected + 1) + "/" + g.Count + "  " + g.Label(), label);
            if (GUI.Button(new Rect(x + 274, y + 322, 52, 30), "<", button)) CycleGarage(-1);
            if (GUI.Button(new Rect(x + 330, y + 322, 52, 30), ">", button)) CycleGarage(1);
            GUI.Label(new Rect(x, y + 354, 570, 22), g.Current.era + "  |  " + g.Credit(), small);
        }
        if (GUI.Button(new Rect(x, y + 382, 262, 48), "START  [ENTER / A]", button)) race.StartRace();
        if (GUI.Button(new Rect(x + 272, y + 382, 162, 48), "QUALITY: " + (race.Quality == 0 ? "LOW" : race.Quality == 1 ? "HIGH" : "ULTRA"), button))
        {
            race.Quality = (race.Quality + 1) % 3;
            ApplyQuality();
        }
        GUI.Label(new Rect(x, y + 438, 400, 24), "SHAKE / MOTION BLUR / NITRO FOV", small);
        race.EffectStrength = GUI.HorizontalSlider(new Rect(x, y + 462, 434, 24), race.EffectStrength, 0f, 1f);
        ChaseCamera chase = Camera.main ? Camera.main.GetComponent<ChaseCamera>() : null;
        if (chase) chase.effects = race.EffectStrength;
        // Right column: route facts only, kept narrow so the car stays visible.
        float right = Screen.width * 0.545f;
        float rw = Screen.width * 0.40f;
        GUI.Label(new Rect(right, y + 4, rw, 32), "ROUTE 01", subtitle);
        GUI.Label(new Rect(right, y + 40, rw, 34), (track.TotalLength / 1000f).ToString("0.0") + " KM / 2 LAPS", label);
        Panel(new Rect(right - 12f, y + 72f, rw + 24f, 128f), 0.62f);
        GUI.Label(new Rect(right, y + 80, rw, 92), "Mediterranean coast\nCliff esses & hairpin\nStone tunnel & seaside village", small);
        if (chase)
        {
            GUI.Label(new Rect(right, Screen.height - 118f, 300, 24), "BASE FOV  " + Mathf.RoundToInt(chase.baseFov), small);
            chase.baseFov = GUI.HorizontalSlider(new Rect(right, Screen.height - 94f, 300, 22), chase.baseFov, 52f, 72f);
            GUI.Label(new Rect(right, Screen.height - 68f, 300, 24), "FOLLOW DISTANCE  " + chase.distance.ToString("0.0") + " M", small);
            chase.distance = GUI.HorizontalSlider(new Rect(right, Screen.height - 44f, 300, 22), chase.distance, 6f, 10f);
        }
        // Minimap sits in the left column, under the garage block.
        if (minimap) GUI.DrawTexture(new Rect(x + 398f, y + 452f, 128, 128), minimap);
        float footerY = Screen.height - 84f;
        GUI.Label(new Rect(x, footerY, 520, 22), "Ferrari 458: vicent091036 CC BY 4.0", small);
        GUI.Label(new Rect(x, footerY + 19f, 520, 22), "3DAssets.dev fleet + BMW: CC0 1.0", small);
        GUI.Label(new Rect(x, footerY + 38f, 520, 22), gamepadLabel, small);
        GUI.Label(new Rect(right, y + 176f, 400, 22), "TURNTABLE  /  change car with Q / E or LB / RB", small);
    }

    private void DrawRaceHud()
    {
        var p = race.Player;
        Panel(new Rect(20, 18, 285, 88));
        GUI.Label(new Rect(36, 24, 120, 32), "POS  " + race.PlayerPosition() + " / 4", subtitle);
        GUI.Label(new Rect(36, 61, 170, 30), "LAP  " + Mathf.Min(p.lap + 1, 2) + " / 2", label);
        Panel(new Rect(Screen.width * 0.5f - 150, 18, 300, 70));
        GUI.Label(new Rect(Screen.width * 0.5f - 138, 24, 276, 34), RaceManager.FormatTime(race.Elapsed), center);
        GUI.Label(new Rect(Screen.width * 0.5f - 130, 59, 260, 22), "TOTAL RACE TIME", small);
        Panel(new Rect(Screen.width - 300, 18, 280, 88));
        GUI.Label(new Rect(Screen.width - 284, 24, 250, 30), "BEST  " + RaceManager.FormatTime(p.bestLap), label);
        GUI.Label(new Rect(Screen.width - 284, 61, 250, 24), "LAST  " + RaceManager.FormatTime(p.lastLap), small);

        if (minimap)
        {
            Panel(new Rect(20, Screen.height - 238, 210, 210), 0.56f);
            GUI.DrawTexture(new Rect(28, Screen.height - 230, 194, 194), minimap);
            DrawCarDots(new Rect(28, Screen.height - 230, 194, 194));
        }
        float speedX = Screen.width - 315f, speedY = Screen.height - 190f;
        Panel(new Rect(speedX - 15, speedY - 12, 295, 166));
        GUI.Label(new Rect(speedX, speedY - 5, 220, 72), Mathf.RoundToInt(race.player.SpeedKph).ToString("000"), big);
        GUI.Label(new Rect(speedX + 170, speedY + 50, 85, 26), "KM/H", label);
        GUI.Label(new Rect(speedX, speedY + 83, 200, 26),
            race.player.NitroOverdrive ? "NITRO OVERDRIVE" : race.player.NitroActive ? "NITRO ACTIVE" : "NITRO", subtitle);
        Bar(new Rect(speedX, speedY + 119, 250, 18), race.player.nitro / 100f,
            race.player.NitroOverdrive ? new Color(0.78f, 0.38f, 1f) : race.player.NitroActive ? new Color(0.15f, 0.8f, 1f) : new Color(1f, 0.55f, 0.12f));
        if (race.player.DriftAmount > 0.22f) GUI.Label(new Rect(Screen.width * 0.5f - 170, Screen.height - 110, 340, 38), "DRIFT  +" + Mathf.RoundToInt(race.player.DriftAmount * 100), center);
        if (!string.IsNullOrEmpty(race.Notice)) { Panel(new Rect(Screen.width * 0.5f - 260, 120, 520, 54), 0.65f); GUI.Label(new Rect(Screen.width * 0.5f - 250, 128, 500, 40), race.Notice, center); }
        if (p.wrongWay) GUI.Label(new Rect(Screen.width * 0.5f - 170, 188, 340, 44), "WRONG WAY", center);
        else if (p.offroad) GUI.Label(new Rect(Screen.width * 0.5f - 170, 188, 340, 44), "OFF ROAD — R TO RESET", center);
        GUI.Label(new Rect(Screen.width - 115, Screen.height - 24, 100, 20), Mathf.RoundToInt(fps) + " FPS", small);
    }

    private void DrawCountdown()
    {
        string text = race.Countdown > 3f ? "READY" : race.Countdown > 1f ? Mathf.CeilToInt(race.Countdown - 1f).ToString() : "GO";
        Panel(new Rect(Screen.width * 0.5f - 120, Screen.height * 0.32f, 240, 125), 0.45f);
        var style = new GUIStyle(center) { fontSize = text.Length > 2 ? 48 : 72 };
        GUI.Label(new Rect(Screen.width * 0.5f - 120, Screen.height * 0.32f + 12, 240, 105), text, style);
    }

    private void DrawPause()
    {
        Panel(new Rect(0, 0, Screen.width, Screen.height), 0.68f);
        float w = 560f, h = 525f, x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f;
        Panel(new Rect(x, y, w, h), 0.93f);
        GUI.Label(new Rect(x + 30, y + 16, w - 60, 50), "RACE PAUSED", center);
        var p = race.Player;
        GUI.Label(new Rect(x + 30, y + 68, w - 60, 28),
            "POS " + race.PlayerPosition() + "/4    LAP " + Mathf.Min(p.lap + 1, 2) + "/2    TIME "
            + RaceManager.FormatTime(race.Elapsed) + "    BEST " + RaceManager.FormatTime(p.bestLap), small);
        for (int i = 0; i < PauseOptions.Length; i++)
        {
            float rowY = y + 106 + i * 39;
            if (i == pauseIndex)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 0.62f, 0.20f, 0.30f);
                GUI.DrawTexture(new Rect(x + 26, rowY - 2, w - 52, 36), Texture2D.whiteTexture);
                GUI.color = old;
            }
            string value = PauseValue(i);
            string text = PauseOptions[i] + (string.IsNullOrEmpty(value) ? "" : "     " + value);
            if (GUI.Button(new Rect(x + 30, rowY, w - 60, 32), text, rowButton)) { pauseIndex = i; ActivatePauseOption(); }
        }
        GUI.Label(new Rect(x + 30, y + h - 52, w - 60, 22), gamepadLabel, small);
        GUI.Label(new Rect(x + 30, y + h - 30, w - 60, 22),
            "UP/DOWN SELECT   LEFT/RIGHT ADJUST   A CONFIRM", small);
    }

    private void DrawResults()
    {
        Panel(new Rect(0, 0, Screen.width, Screen.height), 0.74f);
        float x = Screen.width * 0.5f - 390f, y = Screen.height * 0.16f;
        Panel(new Rect(x, y, 780, 570));
        GUI.Label(new Rect(x + 45, y + 32, 690, 70), "COAST RUN COMPLETE", title);
        GUI.Label(new Rect(x + 48, y + 120, 690, 38), "POSITION  " + race.PlayerPosition() + " / 4     TOTAL  " + RaceManager.FormatTime(race.Player.finishTime), subtitle);
        for (int i = 0; i < race.racers.Count; i++)
        {
            var r = race.racers[i];
            GUI.Label(new Rect(x + 48, y + 190 + i * 48, 70, 30), (i + 1).ToString("00"), label);
            GUI.Label(new Rect(x + 130, y + 190 + i * 48, 330, 30), r.name, label);
            GUI.Label(new Rect(x + 490, y + 190 + i * 48, 240, 30), r.finished ? RaceManager.FormatTime(r.finishTime) : "ON COURSE", label);
        }
        GUI.Label(new Rect(x + 48, y + 405, 680, 32), "BEST LAP  " + RaceManager.FormatTime(race.Player.bestLap) + "     RESETS  " + race.Player.resets, label);
        if (GUI.Button(new Rect(x + 48, y + 480, 330, 50), "RACE AGAIN  [ENTER]", button)) race.Restart();
        if (GUI.Button(new Rect(x + 400, y + 480, 330, 50), "QUIT", button)) race.Quit();
    }

    private void BuildMinimap()
    {
        const int size = 256;
        minimap = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
        mapMin = new Vector2(float.MaxValue, float.MaxValue); mapMax = new Vector2(float.MinValue, float.MinValue);
        foreach (var s in track.Samples)
        {
            mapMin = Vector2.Min(mapMin, new Vector2(s.position.x, s.position.z));
            mapMax = Vector2.Max(mapMax, new Vector2(s.position.x, s.position.z));
        }
        for (int i = 0; i < track.Count; i++)
        {
            Vector2 a = MapPoint(track.At(i).position, new Rect(0, 0, size, size));
            Vector2 b = MapPoint(track.At(i + 1).position, new Rect(0, 0, size, size));
            DrawLine(pixels, size, a, b, new Color(0.62f, 0.76f, 0.84f, 0.85f));
        }
        minimap.SetPixels(pixels); minimap.Apply();
    }
    private Vector2 MapPoint(Vector3 p, Rect rect)
    {
        Vector2 n = new Vector2(Mathf.InverseLerp(mapMin.x, mapMax.x, p.x), Mathf.InverseLerp(mapMin.y, mapMax.y, p.z));
        float scale = Mathf.Min(rect.width, rect.height) * 0.88f;
        Vector2 center = rect.center + new Vector2((n.x - 0.5f) * scale, (n.y - 0.5f) * scale);
        return center;
    }
    private void DrawLine(Color[] pixels, int size, Vector2 a, Vector2 b, Color color)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 1.5f) + 1;
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
            for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
            {
                int px = Mathf.RoundToInt(p.x) + x, py = Mathf.RoundToInt(p.y) + y;
                if (px >= 0 && px < size && py >= 0 && py < size) pixels[py * size + px] = color;
            }
        }
    }
    private void DrawCarDots(Rect rect)
    {
        Color old = GUI.color;
        foreach (var r in race.racers)
        {
            Vector2 p = MapPoint(r.car.transform.position, rect);
            GUI.color = r.car == race.player ? new Color(0.1f, 0.85f, 1f) : new Color(1f, 0.55f, 0.18f);
            GUI.DrawTexture(new Rect(p.x - 3, rect.yMax - (p.y - rect.y) - 3, 7, 7), Texture2D.whiteTexture);
        }
        GUI.color = old;
    }
}
