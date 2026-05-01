using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class TimelineTriggerZone2D : MonoBehaviour
{
    [Header("Timeline")]
    [SerializeField] private PlayableDirector timelineDirector;
    [SerializeField] private bool playOnFirstEnterOnly = true;
    [SerializeField] private bool restartFromBeginning = true;
    [SerializeField] private bool requireEnterFromOutside = true;

    [Header("Player Filter")]
    [SerializeField] private string playerTag = "Player";

    private Collider2D _triggerCollider;
    private bool _triggered;
    private bool _armed = true;

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider2D>();
        _triggerCollider.isTrigger = true;

        if (timelineDirector != null)
        {
            timelineDirector.playOnAwake = false;
            timelineDirector.Stop();
            if (restartFromBeginning)
            {
                timelineDirector.time = 0d;
            }
        }
    }

    private void Start()
    {
        if (requireEnterFromOutside)
        {
            _armed = !IsAnyPlayerInsideTrigger();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        if (!_armed)
        {
            return;
        }

        if (timelineDirector == null)
        {
            return;
        }

        if (playOnFirstEnterOnly && _triggered)
        {
            return;
        }

        _triggered = true;
        timelineDirector.extrapolationMode = DirectorWrapMode.None;

        if (restartFromBeginning)
        {
            timelineDirector.time = 0d;
        }

        timelineDirector.Play();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!requireEnterFromOutside || _armed || !IsPlayer(other))
        {
            return;
        }

        _armed = !IsAnyPlayerInsideTrigger();
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(playerTag) && other.CompareTag(playerTag))
        {
            return true;
        }

        return other.GetComponentInParent<PlayerMovement>() != null
            || other.GetComponentInParent<PlayerHealth>() != null;
    }

    private bool IsAnyPlayerInsideTrigger()
    {
        if (_triggerCollider == null)
        {
            return false;
        }

        var filter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = false,
            useDepth = false
        };

        var overlaps = new Collider2D[16];
        var count = _triggerCollider.Overlap(filter, overlaps);
        for (var i = 0; i < count; i++)
        {
            if (IsPlayer(overlaps[i]))
            {
                return true;
            }
        }

        return false;
    }
}
