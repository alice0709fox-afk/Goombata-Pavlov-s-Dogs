using UnityEngine;

public class BalconyPlatformSetup : MonoBehaviour
{
    [SerializeField] private string platformLayerName = "Platform";
    [SerializeField] private bool addBoxColliders = true;
    [SerializeField] private string[] platformNameKeywords = { "Square", "Platform" };
    [SerializeField, Range(0.05f, 1f)] private float topSurfaceHeightRatio = 0.22f;

    private void Awake()
    {
        ApplySetup();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        ApplySetup();
    }

    private void ApplySetup()
    {
        var layer = LayerMask.NameToLayer(platformLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"BalconyPlatformSetup: Layer '{platformLayerName}' was not found.", this);
            return;
        }

        var spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                continue;
            }

            if (!ShouldBePlatform(spriteRenderer.gameObject.name))
            {
                continue;
            }

            spriteRenderer.gameObject.layer = layer;

            if (!addBoxColliders)
            {
                continue;
            }

            var boxCollider = spriteRenderer.GetComponent<BoxCollider2D>();
            if (boxCollider == null)
            {
                boxCollider = spriteRenderer.gameObject.AddComponent<BoxCollider2D>();
            }

            var spriteBounds = spriteRenderer.sprite.bounds;
            var topHeight = Mathf.Max(0.05f, spriteBounds.size.y * topSurfaceHeightRatio);
            boxCollider.isTrigger = false;
            boxCollider.size = new Vector2(spriteBounds.size.x, topHeight);
            boxCollider.offset = new Vector2(
                spriteBounds.center.x,
                spriteBounds.center.y + (spriteBounds.size.y - topHeight) * 0.5f);
        }
    }

    private bool ShouldBePlatform(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName) || platformNameKeywords == null || platformNameKeywords.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < platformNameKeywords.Length; index++)
        {
            var keyword = platformNameKeywords[index];
            if (string.IsNullOrWhiteSpace(keyword))
            {
                continue;
            }

            if (objectName.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}