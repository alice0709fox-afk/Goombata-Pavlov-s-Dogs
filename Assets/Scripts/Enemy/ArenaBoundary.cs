using UnityEngine;

/// <summary>
/// Управляет невидимыми коллайдерами-стенами арены.
/// Активируется через BossRoomTrigger.Activate() после кат-сцены.
/// 
/// Настройка: создай дочерние GameObject с BoxCollider2D (Is Trigger = false)
/// на левой и правой границах арены и назначь их в leftWall / rightWall.
/// Или оставь поля пустыми — скрипт создаст стены автоматически
/// по заданным координатам leftX / rightX.
/// </summary>
[DisallowMultipleComponent]
public class ArenaBoundary : MonoBehaviour
{
    [Header("Walls (опционально, создаются автоматически если пусто)")]
    [SerializeField] private GameObject leftWall;
    [SerializeField] private GameObject rightWall;

    [Header("Авто-создание стен")]
    [SerializeField] private float leftX = -10f;
    [SerializeField] private float rightX = 10f;
    [SerializeField, Min(0.5f)] private float minimumArenaWidth = 8f;
    [SerializeField] private float wallHeight = 20f;
    [SerializeField] private float wallThickness = 0.5f;
    [SerializeField] private float wallCenterY = 0f;

    [Header("Failsafe удержание игрока")]
    [SerializeField] private bool enforcePlayerBounds = true;
    [SerializeField, Min(0f)] private float clampPadding = 0.25f;
    [SerializeField] private bool validateAssignedWalls = true;
    [SerializeField, Min(0f)] private float maxAutoWallDeviation = 2f;

    private bool _active;

    private void Awake()
    {
        // Стены по умолчанию выключены — включатся через Activate()
        if (leftWall != null)
            leftWall.SetActive(false);
        if (rightWall != null)
            rightWall.SetActive(false);

        _active = false;
    }

    /// <summary>Вызывается BossRoomTrigger после окончания кат-сцены.</summary>
    public void Activate()
    {
        ValidateOrResetAssignedWalls();

        if (leftWall == null && rightWall == null)
        {
            leftWall = CreateWall("ArenaWall_Left", leftX);
            rightWall = CreateWall("ArenaWall_Right", rightX);
        }

        EnsureWallCollider(leftWall);
        EnsureWallCollider(rightWall);

        if (leftWall != null)
            leftWall.SetActive(true);
        if (rightWall != null)
            rightWall.SetActive(true);

        _active = true;
    }

    /// <summary>
    /// Активирует границы и фиксирует ближайшую к точке входа сторону арены по X входа.
    /// Это не даёт игроку выйти назад за линию входа после старта боя,
    /// но не переворачивает арену, если вход расположен справа от босса.
    /// </summary>
    public void ActivateWithEntranceX(float entranceX)
    {
        var currentLeft = GetLeftBoundaryX();
        var currentRight = GetRightBoundaryX();

        // Фиксируем ту сторону, которая ближе к точке входа.
        // Для входа справа это будет правая стена, для входа слева — левая.
        var lockLeftSide = Mathf.Abs(entranceX - currentLeft) <= Mathf.Abs(entranceX - currentRight);

        if (lockLeftSide)
        {
            leftX = entranceX;
            if (leftWall != null)
                leftWall.transform.position = new Vector3(entranceX, leftWall.transform.position.y, leftWall.transform.position.z);
        }
        else
        {
            rightX = entranceX;
            if (rightWall != null)
                rightWall.transform.position = new Vector3(entranceX, rightWall.transform.position.y, rightWall.transform.position.z);
        }

        if (rightX - leftX < minimumArenaWidth)
        {
            if (lockLeftSide)
                rightX = leftX + minimumArenaWidth;
            else
                leftX = rightX - minimumArenaWidth;
        }

        Activate();
    }

    public void Deactivate()
    {
        if (leftWall != null)
            leftWall.SetActive(false);
        if (rightWall != null)
            rightWall.SetActive(false);

        _active = false;
    }

    private void LateUpdate()
    {
        if (!_active || !enforcePlayerBounds)
            return;

        var minX = GetLeftBoundaryX() + clampPadding;
        var maxX = GetRightBoundaryX() - clampPadding;
        if (maxX <= minX)
            return;

        var players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            var player = players[i];
            if (player == null || player.IsDead)
                continue;

            var playerTransform = player.transform;
            var pos = playerTransform.position;
            var clampedX = Mathf.Clamp(pos.x, minX, maxX);
            if (Mathf.Abs(clampedX - pos.x) <= 0.0001f)
                continue;

            var rb = player.GetComponent<Rigidbody2D>() ?? player.GetComponentInParent<Rigidbody2D>();
            if (rb != null)
            {
                var rbPos = rb.position;
                rb.position = new Vector2(clampedX, rbPos.y);
                continue;
            }

            playerTransform.position = new Vector3(clampedX, pos.y, pos.z);
        }
    }

    private GameObject CreateWall(string wallName, float x)
    {
        var wall = new GameObject(wallName);
        wall.transform.SetParent(transform, false);
        wall.transform.position = new Vector3(x, wallCenterY, 0f);
        wall.layer = gameObject.layer;

        var col = wall.AddComponent<BoxCollider2D>();
        col.size = new Vector2(wallThickness, wallHeight);
        col.isTrigger = false;

        return wall;
    }

    private void EnsureWallCollider(GameObject wall)
    {
        if (wall == null)
            return;

        var collider = wall.GetComponent<BoxCollider2D>();
        if (collider == null)
            collider = wall.AddComponent<BoxCollider2D>();

        collider.isTrigger = false;
        collider.size = new Vector2(wallThickness, wallHeight);
    }

    private float GetLeftBoundaryX()
    {
        if (leftWall != null)
            return leftWall.transform.position.x;

        return leftX;
    }

    private float GetRightBoundaryX()
    {
        if (rightWall != null)
            return rightWall.transform.position.x;

        return rightX;
    }

    private void ValidateOrResetAssignedWalls()
    {
        if (!validateAssignedWalls)
            return;

        leftWall = ValidateWall(leftWall, leftX);
        rightWall = ValidateWall(rightWall, rightX);
    }

    private GameObject ValidateWall(GameObject wall, float expectedX)
    {
        if (wall == null)
            return null;

        var col = wall.GetComponent<Collider2D>();
        if (col == null)
            return null;

        if (col.isTrigger)
            return null;

        var xDelta = Mathf.Abs(wall.transform.position.x - expectedX);
        if (xDelta > maxAutoWallDeviation)
            return null;

        return wall;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
        Gizmos.DrawCube(new Vector3(leftX, wallCenterY, 0f), new Vector3(wallThickness, wallHeight, 0.1f));
        Gizmos.DrawCube(new Vector3(rightX, wallCenterY, 0f), new Vector3(wallThickness, wallHeight, 0.1f));
    }
}
