using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Прикрепить на GameObject с Collider2D (Is Trigger = true).
/// Когда игрок входит в триггер — запускается кат-сцена Timeline.
/// После окончания кат-сцены включается Boss AI и границы арены.
/// </summary>
[DisallowMultipleComponent]
public class BossRoomTrigger : MonoBehaviour
{
    [Header("Timeline")]
    [SerializeField] private PlayableDirector cutsceneDirector;
    [SerializeField] private bool keepFinalStateAfterCutscene = true;

    [Header("Boss")]
    [SerializeField] private BossController bossController;

    [Header("Arena Boundaries")]
    [SerializeField] private ArenaBoundary arenaBoundary;

    [Header("Camera")]
    [Tooltip("Отключить основную камеру игрока во время кат-сцены")]
    [SerializeField] private GameObject playerCamera;

    private bool _triggered;
    private bool _bossFightStarted;

    private void Start()
    {
        if (bossController != null)
            bossController.enabled = false;

        if (cutsceneDirector != null)
        {
            cutsceneDirector.playOnAwake = false;
            cutsceneDirector.Stop();
            cutsceneDirector.stopped -= OnCutsceneStopped;
            cutsceneDirector.time = 0d;
            cutsceneDirector.Evaluate();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_triggered)
            return;

        if (!other.CompareTag("Player") && other.GetComponentInParent<PlayerHealth>() == null)
            return;

        _triggered = true;
        GetComponent<Collider2D>().enabled = false;

        if (cutsceneDirector != null)
        {
            cutsceneDirector.extrapolationMode = keepFinalStateAfterCutscene
                ? DirectorWrapMode.Hold
                : DirectorWrapMode.None;
            cutsceneDirector.stopped += OnCutsceneStopped;
            cutsceneDirector.time = 0d;
            cutsceneDirector.Play();
        }
        else
        {
            StartBossFight();
        }
    }

    private void OnCutsceneStopped(PlayableDirector director)
    {
        if (!_triggered)
            return;

        director.stopped -= OnCutsceneStopped;

        if (keepFinalStateAfterCutscene)
        {
            director.time = director.duration;
            director.Evaluate();
        }

        if (Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = 1f;

        AudioListener.pause = false;

        StartBossFight();
    }

    private void StartBossFight()
    {
        if (_bossFightStarted || !_triggered)
            return;

        _bossFightStarted = true;

        if (cutsceneDirector != null)
            cutsceneDirector.stopped -= OnCutsceneStopped;

        if (bossController != null)
            bossController.enabled = true;

        if (arenaBoundary != null)
            arenaBoundary.Activate();
    }

    private void OnDisable()
    {
        if (cutsceneDirector != null)
            cutsceneDirector.stopped -= OnCutsceneStopped;
    }
}
