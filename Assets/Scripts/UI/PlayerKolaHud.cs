using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerKolaHud : MonoBehaviour
{
    [SerializeField] private PlayerConsumables playerConsumables;
    [SerializeField] private string hudRootName = "HpPlayer";
    [SerializeField] private string firstKolaName = "Kola";
    [SerializeField] private string secondKolaName = "Kola (1)";
    [SerializeField] [Range(0f, 1f)] private float activeAlpha = 1f;
    [SerializeField] [Range(0f, 1f)] private float inactiveAlpha = 0.2f;

    private Image _firstKolaImage;
    private Image _secondKolaImage;
    private float _nextRebindAttemptTime;

    private void Awake()
    {
        if (playerConsumables == null)
        {
            playerConsumables = GetComponent<PlayerConsumables>() ?? GetComponentInParent<PlayerConsumables>();
        }

        RebindHud();
        Refresh(playerConsumables != null ? playerConsumables.CurrentKolaCount : 0, playerConsumables != null ? playerConsumables.MaxKolaCount : 2);
    }

    private void OnEnable()
    {
        if (playerConsumables != null)
        {
            playerConsumables.KolaCountChanged += Refresh;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (playerConsumables != null)
        {
            playerConsumables.KolaCountChanged -= Refresh;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if ((_firstKolaImage != null && _secondKolaImage != null) || Time.unscaledTime < _nextRebindAttemptTime)
        {
            return;
        }

        _nextRebindAttemptTime = Time.unscaledTime + 0.4f;
        RebindHud();
        Refresh(playerConsumables != null ? playerConsumables.CurrentKolaCount : 0, playerConsumables != null ? playerConsumables.MaxKolaCount : 2);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindHud();
        Refresh(playerConsumables != null ? playerConsumables.CurrentKolaCount : 0, playerConsumables != null ? playerConsumables.MaxKolaCount : 2);
    }

    private void RebindHud()
    {
        var hudRoot = FindSceneObject(SceneManager.GetActiveScene(), hudRootName);
        _firstKolaImage = FindImage(hudRoot, firstKolaName);
        _secondKolaImage = FindImage(hudRoot, secondKolaName);
    }

    private void Refresh(int currentKolaCount, int maxKolaCount)
    {
        ApplyState(_firstKolaImage, currentKolaCount >= 1);
        ApplyState(_secondKolaImage, currentKolaCount >= 2);
    }

    private void ApplyState(Image image, bool isAvailable)
    {
        if (image == null)
        {
            return;
        }

        var color = image.color;
        color.a = isAvailable ? activeAlpha : inactiveAlpha;
        image.color = color;
    }

    private static Image FindImage(GameObject root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        var transforms = root.GetComponentsInChildren<Transform>(true);
        for (var index = 0; index < transforms.Length; index++)
        {
            var current = transforms[index];
            if (current != null && current.name == objectName)
            {
                return current.GetComponent<Image>();
            }
        }

        return null;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        var roots = scene.GetRootGameObjects();
        for (var index = 0; index < roots.Length; index++)
        {
            var root = roots[index];
            if (root == null)
            {
                continue;
            }

            if (root.name == objectName)
            {
                return root;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                var current = transforms[transformIndex];
                if (current != null && current.name == objectName)
                {
                    return current.gameObject;
                }
            }
        }

        return null;
    }
}