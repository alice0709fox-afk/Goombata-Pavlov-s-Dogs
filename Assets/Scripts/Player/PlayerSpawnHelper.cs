using UnityEngine;

public static class PlayerSpawnHelper
{
    public static void StabilizeAfterSpawn(Transform playerRoot)
    {
        if (playerRoot == null)
        {
            return;
        }

        var rigidbody2D = playerRoot.GetComponent<Rigidbody2D>();
        if (rigidbody2D == null)
        {
            rigidbody2D = playerRoot.GetComponentInChildren<Rigidbody2D>();
        }

        if (rigidbody2D != null)
        {
            rigidbody2D.linearVelocity = Vector2.zero;
            rigidbody2D.angularVelocity = 0f;
        }

        var groundChecker = playerRoot.GetComponentInChildren<GroundChecker>();
        if (groundChecker == null)
        {
            Physics2D.SyncTransforms();
            return;
        }

        Physics2D.SyncTransforms();
        if (!groundChecker.CheckGround())
        {
            groundChecker.TrySnapToGround(playerRoot);
        }

        groundChecker.CheckGround();
    }
}