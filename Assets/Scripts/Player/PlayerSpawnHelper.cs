using UnityEngine;

public static class PlayerSpawnHelper
{
    public static void PrepareForSceneTransition(Transform playerRoot)
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
            rigidbody2D.simulated = false;
        }

        var animators = playerRoot.GetComponentsInChildren<Animator>(true);
        for (var index = 0; index < animators.Length; index++)
        {
            if (animators[index] != null)
            {
                animators[index].enabled = false;
            }
        }

        var renderers = playerRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (var index = 0; index < renderers.Length; index++)
        {
            if (renderers[index] != null)
            {
                renderers[index].enabled = false;
            }
        }
    }

    public static void RestoreAfterSceneTransition(Transform playerRoot)
    {
        if (playerRoot == null)
        {
            return;
        }

        var renderers = playerRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (var index = 0; index < renderers.Length; index++)
        {
            if (renderers[index] != null)
            {
                renderers[index].enabled = true;
            }
        }

        var animators = playerRoot.GetComponentsInChildren<Animator>(true);
        for (var index = 0; index < animators.Length; index++)
        {
            if (animators[index] != null)
            {
                animators[index].enabled = true;
                animators[index].Update(0f);
            }
        }

        var rigidbody2D = playerRoot.GetComponent<Rigidbody2D>();
        if (rigidbody2D == null)
        {
            rigidbody2D = playerRoot.GetComponentInChildren<Rigidbody2D>();
        }

        if (rigidbody2D != null)
        {
            rigidbody2D.simulated = true;
            rigidbody2D.linearVelocity = Vector2.zero;
            rigidbody2D.angularVelocity = 0f;
        }
    }

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