using UnityEngine;

public sealed class MinimalHud : MonoBehaviour
{
    public ArcadeCarController car;
    private GUIStyle speedStyle, infoStyle, titleStyle;
    private void EnsureStyles()
    {
        if (speedStyle != null) return;
        speedStyle = new GUIStyle(GUI.skin.label) { fontSize = 52, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        speedStyle.normal.textColor = Color.white;
        infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        infoStyle.normal.textColor = new Color(0.85f, 0.93f, 1f);
        titleStyle = new GUIStyle(infoStyle) { fontSize = 24 };
        titleStyle.normal.textColor = new Color(1f, 0.67f, 0.24f);
    }
    private void OnGUI()
    {
        if (!car) return;
        EnsureStyles();
        GUI.Box(new Rect(20, 20, 350, 92), GUIContent.none);
        GUI.Label(new Rect(36, 24, 500, 36), "AURORA / DRIVING LAB", titleStyle);
        GUI.Label(new Rect(36, 62, 650, 32), "WASD drive   SPACE drift   SHIFT nitro   R reset", infoStyle);
        GUI.Box(new Rect(Screen.width - 280, Screen.height - 150, 260, 130), GUIContent.none);
        GUI.Label(new Rect(Screen.width - 265, Screen.height - 145, 220, 65), Mathf.RoundToInt(car.SpeedKph).ToString("000"), speedStyle);
        GUI.Label(new Rect(Screen.width - 110, Screen.height - 104, 80, 28), "KM/H", infoStyle);
        GUI.Label(new Rect(Screen.width - 255, Screen.height - 65, 100, 28), "NITRO", infoStyle);
        GUI.Box(new Rect(Screen.width - 150, Screen.height - 57, 110, 14), GUIContent.none);
        GUI.color = car.NitroActive ? new Color(0.2f, 0.85f, 1f) : new Color(1f, 0.55f, 0.15f);
        GUI.DrawTexture(new Rect(Screen.width - 150, Screen.height - 57, 110 * car.nitro / 100f, 14), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
