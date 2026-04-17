using UnityEngine;

public class GroundChecker : MonoBehaviour
{
    [SerializeField] private float radius;
    [SerializeField] private LayerMask groundLayer;

    public bool IsGrounded { get; private set; }
    
    public bool CheckGround()
    {
        IsGrounded = Physics2D.OverlapCircle(transform.position, radius, groundLayer);
        return IsGrounded;
    }

    public bool TrySnapToGround(Transform playerRoot, float maxDistance = 10f)
    {
        if (playerRoot == null)
        {
            return false;
        }

        var rayOrigin = new Vector2(transform.position.x, transform.position.y + maxDistance * 0.5f);
        var hit = Physics2D.Raycast(rayOrigin, Vector2.down, maxDistance, groundLayer);
        if (!hit)
        {
            return false;
        }

        // Keep GroundChecker circle just above ground while preserving local offset from player root.
        var checkerOffset = transform.position - playerRoot.position;
        var targetCheckerY = hit.point.y + radius + 0.01f;
        var targetRootY = targetCheckerY - checkerOffset.y;

        playerRoot.position = new Vector3(playerRoot.position.x, targetRootY, playerRoot.position.z);
        Physics2D.SyncTransforms();
        return CheckGround();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
