using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GameplayMechanicsBootstrapper : MonoBehaviour
{
    private static GameplayMechanicsBootstrapper _instance;

    private float _nextRefreshTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
        {
            return;
        }

        var bootstrapperObject = new GameObject(nameof(GameplayMechanicsBootstrapper));
        _instance = bootstrapperObject.AddComponent<GameplayMechanicsBootstrapper>();
        DontDestroyOnLoad(bootstrapperObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        EnsureSceneBindings(SceneManager.GetActiveScene());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefreshTime)
        {
            return;
        }

        _nextRefreshTime = Time.unscaledTime + 0.5f;
        EnsureSceneBindings(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _nextRefreshTime = 0f;
        EnsureSceneBindings(scene);
    }

    private void EnsureSceneBindings(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        EnsurePlayerComponents();
        EnsurePickupComponents(scene);
        EnsureEnemyComponents(scene);
    }

    private void EnsurePlayerComponents()
    {
        var players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        for (var index = 0; index < players.Length; index++)
        {
            var player = players[index];
            if (player == null)
            {
                continue;
            }

            if (player.GetComponent<PlayerConsumables>() == null)
            {
                player.gameObject.AddComponent<PlayerConsumables>();
            }

            if (player.GetComponent<PlayerKolaHud>() == null)
            {
                player.gameObject.AddComponent<PlayerKolaHud>();
            }
        }
    }

    private void EnsurePickupComponents(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var root = roots[rootIndex];
            if (root == null)
            {
                continue;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                var current = transforms[transformIndex];
                if (current == null)
                {
                    continue;
                }

                if (HasPickupName(current.name, "Money"))
                {
                    EnsurePickupSetup<MoneyPickup>(current.gameObject);
                }
                else if (HasPickupName(current.name, "KokaKola"))
                {
                    EnsurePickupSetup<KokaKolaPickup>(current.gameObject);
                }
            }
        }
    }

    private void EnsureEnemyComponents(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var root = roots[rootIndex];
            if (root == null)
                continue;

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                var current = transforms[transformIndex];
                if (current == null)
                    continue;

                var isRaider3 = HasEnemyName(current.name, "Raider3");
                var isRaider2 = HasEnemyName(current.name, "Raider2");
                if (!isRaider3 && !isRaider2)
                    continue;

                EnsureEnemySetup(current.gameObject, isRaider2);
            }
        }
    }

    private static bool HasPickupName(string objectName, string baseName)
    {
        if (string.IsNullOrEmpty(objectName) || string.IsNullOrEmpty(baseName))
        {
            return false;
        }

        if (string.Equals(objectName, baseName, System.StringComparison.Ordinal))
        {
            return true;
        }

        return objectName.StartsWith(baseName + " (", System.StringComparison.Ordinal);
    }

    private static bool HasEnemyName(string objectName, string baseName)
    {
        if (string.IsNullOrEmpty(objectName) || string.IsNullOrEmpty(baseName))
            return false;

        if (string.Equals(objectName, baseName, System.StringComparison.Ordinal))
            return true;

        return objectName.StartsWith(baseName + " (", System.StringComparison.Ordinal);
    }

    private static void EnsurePickupSetup<T>(GameObject pickupObject) where T : Component
    {
        if (pickupObject == null)
        {
            return;
        }

        if (pickupObject.GetComponent<T>() == null)
        {
            pickupObject.AddComponent<T>();
        }
    }

    private static void EnsureEnemySetup(GameObject enemyObject, bool includeRangedShooter)
    {
        if (enemyObject == null)
            return;

        if (enemyObject.GetComponent<Rigidbody2D>() == null)
            enemyObject.AddComponent<Rigidbody2D>();

        if (enemyObject.GetComponent<Collider2D>() == null)
        {
            var capsule = enemyObject.AddComponent<CapsuleCollider2D>();
            capsule.isTrigger = true;
        }

        if (enemyObject.GetComponent<EnemyController>() == null)
            enemyObject.AddComponent<EnemyController>();

        if (enemyObject.GetComponent<EnemyDamageDealer>() == null)
            enemyObject.AddComponent<EnemyDamageDealer>();

        if (enemyObject.GetComponent<EnemyPatrolChase2D>() == null)
            enemyObject.AddComponent<EnemyPatrolChase2D>();

        if (includeRangedShooter && enemyObject.GetComponent<EnemyRangedShooter2D>() == null)
            enemyObject.AddComponent<EnemyRangedShooter2D>();
    }
}