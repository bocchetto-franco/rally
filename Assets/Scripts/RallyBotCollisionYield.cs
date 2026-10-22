using UnityEngine;

/// <summary>Collision messages belong on the Rigidbody, which can be below the bot controller.</summary>
[DisallowMultipleComponent, AddComponentMenu("")]
public sealed class RallyBotCollisionYield : MonoBehaviour
{
    RallyBotController owner;
    Rigidbody player;
    float blockedContactTime;
    public void Bind(RallyBotController bot) => owner = bot;
    void OnCollisionEnter(Collision collision)
    {
        var other = collision.rigidbody;
        if (other == null || other.GetComponent<JrsVehicleController>() == null || other.GetComponentInParent<RallyBotController>() != null) return;
        player = other; blockedContactTime = 0;
    }
    void OnCollisionStay(Collision collision)
    {
        if (owner == null || player == null || collision.rigidbody != player) return;
        blockedContactTime += Time.fixedDeltaTime;
        if (blockedContactTime >= .12f) owner.ReportBlockedPlayer(player);
    }
    void OnCollisionExit(Collision collision)
    {
        if (collision.rigidbody == player) { player = null; blockedContactTime = 0; }
    }
    void OnDisable() { player = null; blockedContactTime = 0; }
}
