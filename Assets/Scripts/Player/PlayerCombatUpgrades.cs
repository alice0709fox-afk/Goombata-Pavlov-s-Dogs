using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCombatUpgrades : MonoBehaviour
{
    [SerializeField, Min(0f)] private float startFlatDamageBonus;

    public float FlatDamageBonus { get; private set; }

    private void Awake()
    {
        FlatDamageBonus = Mathf.Max(0f, startFlatDamageBonus);
    }

    public void AddFlatDamageBonus(float value)
    {
        if (value <= 0f)
        {
            return;
        }

        FlatDamageBonus = Mathf.Max(0f, FlatDamageBonus + value);
    }
}