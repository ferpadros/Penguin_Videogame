using UnityEngine;

public class FollowPlayer : MonoBehaviour
{
    public Transform player;
    // Set offset in Inspector, e.g., (X: 0, Y: 3, Z: -5) to sit behind and slightly above the player
    public Vector3 offset = new Vector3(0f, 3f, -5f); 
    public float smoothSpeed = 10f;
    
    void LateUpdate()
    {
        if (player == null) return;

        // Calculate position relative to player's current facing direction
        Vector3 targetPosition = player.position + (player.rotation * offset);

        // Smoothly move camera toward target position
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);

        // Keep looking at the player (slightly above their pivot for a better view angle)
        Vector3 lookAtTarget = player.position + Vector3.up * 1.5f;
        transform.LookAt(lookAtTarget);
    }
}