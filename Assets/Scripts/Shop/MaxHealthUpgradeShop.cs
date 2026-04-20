using UnityEngine;

[DisallowMultipleComponent]
public class MaxHealthUpgradeShop : MonoBehaviour
{
    [SerializeField] private int price = 50;
    [SerializeField] private float maxHealthBonus = 20f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryBuy(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null)
            return;

        TryBuy(collision.collider);
    }

    private void TryBuy(Collider2D target)
    {
        if (target == null)
            return;

        var playerHealth = target.GetComponent<PlayerHealth>() ?? target.GetComponentInParent<PlayerHealth>();
        var playerSouls = target.GetComponent<PlayerSouls>() ?? target.GetComponentInParent<PlayerSouls>();

        if (playerHealth == null || playerSouls == null)
            return;

        if (!playerSouls.SpendSouls(price))
            return;

        playerHealth.IncreaseMaxHealth(maxHealthBonus);
    }
}