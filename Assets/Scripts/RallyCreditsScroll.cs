using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Read long credits with keyboard/D-pad while the sole menu button remains Back.</summary>
public sealed class RallyCreditsScroll : MonoBehaviour
{
    ScrollRect scroll;
    public void Configure(ScrollRect target) => scroll = target;
    void Update()
    {
        if (scroll == null || !scroll.gameObject.activeInHierarchy) return;
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = RallyGameSession.LocalPlayerCount == 2 ? RallyLocalDevices.GamepadFor(0) : Gamepad.current;
        float direction = 0f;
        if (Input.GetKey(KeyCode.DownArrow) || (keyboard != null && keyboard.downArrowKey.isPressed) ||
            (pad != null && pad.dpad.down.isPressed)) direction -= 1f;
        if (Input.GetKey(KeyCode.UpArrow) || (keyboard != null && keyboard.upArrowKey.isPressed) ||
            (pad != null && pad.dpad.up.isPressed)) direction += 1f;
        float range = Mathf.Max(1f, scroll.content.rect.height - scroll.viewport.rect.height);
        if (direction != 0f)
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + direction * 500f * Time.unscaledDeltaTime / range);
        bool pageUp = Input.GetKeyDown(KeyCode.PageUp) || (keyboard != null && keyboard.pageUpKey.wasPressedThisFrame);
        bool pageDown = Input.GetKeyDown(KeyCode.PageDown) || (keyboard != null && keyboard.pageDownKey.wasPressedThisFrame);
        if (pageUp || pageDown)
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + (pageUp ? 1f : -1f) * scroll.viewport.rect.height * .9f / range);
        if (Input.GetKeyDown(KeyCode.Home) || (keyboard != null && keyboard.homeKey.wasPressedThisFrame)) scroll.verticalNormalizedPosition = 1f;
        if (Input.GetKeyDown(KeyCode.End) || (keyboard != null && keyboard.endKey.wasPressedThisFrame)) scroll.verticalNormalizedPosition = 0f;
    }
}
