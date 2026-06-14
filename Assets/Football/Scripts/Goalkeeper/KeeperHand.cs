using UnityEngine;

/// <summary>
/// Attach to each goalkeeper hand cube. When the ball physically contacts the hand it
/// reports a save to the GameManager. The collision itself (handled by the Rigidbody +
/// Collider) is what deflects the ball; this script only reports the event.
/// </summary>
[RequireComponent(typeof(Collider))]
public class KeeperHand : MonoBehaviour
{
    [SerializeField] private string ballTag = "Ball";

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(ballTag) && GameManager.Instance != null)
            GameManager.Instance.OnBallSaved();
    }
}
