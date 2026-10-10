using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Stable, exclusive local device ownership, shared by selection, driving and pause.</summary>
public static class RallyLocalDevices
{
    static readonly Gamepad[] pads = new Gamepad[2];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        pads[0] = pads[1] = null;
    }

    static bool Available(Gamepad pad) => pad != null && pad.added && pad.enabled;

    static void Refresh()
    {
        // Keep an already paired controller even when Gamepad.current changes.
        // A missing slot may take a newly connected device, never the other player's.
        for (int player = 0; player < 2; player++)
        {
            if (Available(pads[player])) continue;
            foreach (Gamepad pad in Gamepad.all)
            {
                if (!Available(pad) || pad == pads[1 - player]) continue;
                pads[player] = pad;
                break;
            }
        }
    }

    public static Gamepad GamepadFor(int player)
    {
        Refresh();
        return player >= 0 && player < 2 && Available(pads[player]) ? pads[player] : null;
    }

    public static int KeyboardPlayer
    {
        get
        {
            Refresh();
            if (!Available(pads[0])) return 0;
            return !Available(pads[1]) ? 1 : -1;
        }
    }

    public static bool UsesKeyboard(int player) => player >= 0 && player < 2 && GamepadFor(player) == null;
    public static string Label(int player) => "J" + (player + 1) + " · " +
        (GamepadFor(player) != null ? "MANDO" : player == 0 ? "WASD + ESPACIO" : "FLECHAS + ENTER");
}
