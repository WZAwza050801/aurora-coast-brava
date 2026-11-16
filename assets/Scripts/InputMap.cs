using UnityEngine;

/// <summary>
/// Every gamepad / keyboard intent in the game is decided here, in pure
/// functions that take already-sampled booleans. The game code asks Unity for
/// the raw input and passes it in; this file decides what the player meant.
///
/// Keeping the decisions side-effect free means the mapping can be verified
/// with synthetic signals (-padmaptest) on a machine with no controller
/// attached. The physical feel of a real stick still needs a human pass.
/// </summary>
public static class InputMap
{
    /// <summary>Stick drift below this magnitude is treated as centred.</summary>
    public const float StickDeadzone = 0.10f;

    // Unity's default joystick button indices.
    public const int PadA = 0;          // Cross / A
    public const int PadB = 1;          // Circle / B
    public const int PadX = 2;          // Square / X
    public const int PadLB = 4;         // Left shoulder
    public const int PadRB = 5;         // Right shoulder
    public const int PadStart = 9;      // Start / Menu
    public const int PadStartAlt = 7;   // Alternate start (kept for older rigs)

    /// <summary>
    /// Legacy "Vertical" / "Horizontal" axes already carry joystick axes 0/1
    /// (with the inverted Y baked into ProjectSettings), so this only trims
    /// the residual drift around centre.
    /// </summary>
    public static float Stick(float raw)
    {
        float magnitude = Mathf.Abs(raw);
        if (magnitude <= StickDeadzone) return 0f;
        return Mathf.Sign(raw) * (magnitude - StickDeadzone) / Mathf.Max(0.0001f, 1f - StickDeadzone);
    }

    public static bool Handbrake(bool space, bool padA)
    {
        return space || padA;
    }

    public static bool Nitro(bool shiftLeft, bool shiftRight, bool padX, bool padRB)
    {
        return shiftLeft || shiftRight || padX || padRB;
    }

    public static bool Reset(bool keyR, bool padB)
    {
        return keyR || padB;
    }

    public static bool Pause(bool escape, bool padStart, bool padStartAlt)
    {
        return escape || padStart || padStartAlt;
    }

    public static bool Confirm(bool enter, bool keypadEnter, bool padA)
    {
        return enter || keypadEnter || padA;
    }

    public static bool PreviousCar(bool keyQ, bool leftBracket, bool padLB)
    {
        return keyQ || leftBracket || padLB;
    }

    public static bool NextCar(bool keyE, bool rightBracket, bool padRB)
    {
        return keyE || rightBracket || padRB;
    }
}
