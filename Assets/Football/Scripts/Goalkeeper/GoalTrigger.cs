using UnityEngine;

/// <summary>
/// Attach to a trigger volume (Box Collider, Is Trigger = ON) placed just behind the goal
/// line, filling the goal mouth. When the ball enters, it reports a goal to the GameManager.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GoalTrigger : MonoBehaviour
{
    [SerializeField] private string ballTag = "Ball";

    private void Reset()
    {
        // Convenience: ensure the collider is a trigger when first added.
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(ballTag) && GameManager.Instance != null)
            GameManager.Instance.OnBallEnteredGoal();
    }
}
