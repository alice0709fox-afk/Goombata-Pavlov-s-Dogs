using UnityEngine;

[DisallowMultipleComponent]
public class PlayerSouls : MonoBehaviour
{
    [SerializeField] private int startSouls;

    public event System.Action<int> SoulsChanged;

    public int CurrentSouls { get; private set; }

    private void Awake()
    {
        CurrentSouls = Mathf.Max(0, startSouls);
        SoulsChanged?.Invoke(CurrentSouls);
    }

    public void AddSouls(int amount)
    {
        if (amount <= 0)
            return;

        CurrentSouls += amount;
        SoulsChanged?.Invoke(CurrentSouls);
    }

    public bool SpendSouls(int amount)
    {
        if (amount <= 0)
            return true;

        if (CurrentSouls < amount)
            return false;

        CurrentSouls -= amount;
        SoulsChanged?.Invoke(CurrentSouls);
        return true;
    }
}