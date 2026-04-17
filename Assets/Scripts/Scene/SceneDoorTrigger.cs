using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SceneDoorTrigger : MonoBehaviour
{
    public enum TriggerMode
    {
        Enter,
        Exit
    }

    [SerializeField] private string targetSceneName = "Level_1";
    [SerializeField] private string targetSpawnPointName = "TargetSpawnPointName";
    [SerializeField] private TriggerMode triggerMode = TriggerMode.Exit;
    [SerializeField] private float activationDelay = 0.75f;

    private bool _isTriggered;
    private float _canTriggerAfterTime;

    private void OnEnable()
    {
        _isTriggered = false;
        _canTriggerAfterTime = Time.timeSinceLevelLoad + Mathf.Max(0f, activationDelay);
    }

    private void Reset()
    {
        var collider2D = GetComponent<Collider2D>();
        collider2D.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggerMode == TriggerMode.Enter)
        {
            TryLoadScene(other);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (triggerMode == TriggerMode.Exit)
        {
            TryLoadScene(other);
        }
    }

    private void TryLoadScene(Collider2D other)
    {
        if (_isTriggered)
        {
            return;
        }

        if (Time.timeSinceLevelLoad < _canTriggerAfterTime)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerMovement>() == null)
        {
            return;
        }

        _isTriggered = true;
        SceneTransitionManager.Instance.LoadScene(targetSceneName, targetSpawnPointName);
    }
}