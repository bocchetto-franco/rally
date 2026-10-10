using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Non-overlapping local keyboard bindings, shared by driving, navigation and pause.</summary>
public static class RallyLocalKeyboard
{
    public static bool Split => RallyGameSession.LocalPlayerCount == 2;
    public static bool Second(int player) => Split && player == 1;
    public static bool Held(Key key) => (Keyboard.current != null && Keyboard.current[key].isPressed) || Input.GetKey(Legacy(key));
    public static bool Pressed(Key key) => (Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame) || Input.GetKeyDown(Legacy(key));
    static KeyCode Legacy(Key key)
    {
        switch (key)
        {
            case Key.UpArrow: return KeyCode.UpArrow;
            case Key.DownArrow: return KeyCode.DownArrow;
            case Key.LeftArrow: return KeyCode.LeftArrow;
            case Key.RightArrow: return KeyCode.RightArrow;
            case Key.Enter: return KeyCode.Return;
            case Key.Space: return KeyCode.Space;
            case Key.RightCtrl: return KeyCode.RightControl;
            case Key.RightShift: return KeyCode.RightShift;
            case Key.Backspace: return KeyCode.Backspace;
            case Key.Escape: return KeyCode.Escape;
            case Key.W: return KeyCode.W;
            case Key.S: return KeyCode.S;
            case Key.A: return KeyCode.A;
            case Key.D: return KeyCode.D;
            case Key.R: return KeyCode.R;
            case Key.T: return KeyCode.T;
            case Key.M: return KeyCode.M;
            case Key.P: return KeyCode.P;
            default: return KeyCode.None;
        }
    }
    public static bool Accelerate(int p) => Second(p) ? Held(Key.UpArrow) : Held(Key.W) || (!Split && Held(Key.UpArrow));
    public static bool Brake(int p) => Second(p) ? Held(Key.DownArrow) : Held(Key.S) || (!Split && Held(Key.DownArrow));
    public static bool Left(int p) => Second(p) ? Held(Key.LeftArrow) : Held(Key.A) || (!Split && Held(Key.LeftArrow));
    public static bool Right(int p) => Second(p) ? Held(Key.RightArrow) : Held(Key.D) || (!Split && Held(Key.RightArrow));
    public static bool Handbrake(int p) => Held(Second(p) ? Key.Enter : Key.Space);
    public static bool Reset(int p) => Held(Second(p) ? Key.Backspace : Key.R);
    public static bool Recover(int p) => Second(p) ? Held(Key.RightShift) : Held(Key.T) || Held(Key.M);
    public static bool Pause(int p) => Pressed(Second(p) ? Key.P : Key.Escape);
    public static string RecoveryLabel(int p) => Second(p) ? "SHIFT DER." : "M / T";
    public static bool MenuUp(int p) => Pressed(Split && p == 0 ? Key.W : Key.UpArrow);
    public static bool MenuDown(int p) => Pressed(Split && p == 0 ? Key.S : Key.DownArrow);
    public static bool MenuLeft(int p) => Pressed(Split && p == 0 ? Key.A : Key.LeftArrow);
    public static bool MenuRight(int p) => Pressed(Split && p == 0 ? Key.D : Key.RightArrow);
    public static bool Confirm(int p) => Pressed(Split && p == 0 ? Key.Space : Key.Enter);
}
