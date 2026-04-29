using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ShopLevelUpgradePanel : MonoBehaviour
{
    [System.Serializable]
    private class UpgradeRow
    {
        public string rowName;
        [Min(0)] public int price = 50;
        [Min(1)] public int maxLevel = 10;
        [Min(0)] public int startLevel;
        [Min(0f)] public float valuePerLevel = 10f;
    }

    [Header("Upgrade Rows")]
    [SerializeField] private UpgradeRow life = new UpgradeRow { rowName = "Life", price = 50, maxLevel = 10, startLevel = 0, valuePerLevel = 10f };
    [SerializeField] private UpgradeRow damage = new UpgradeRow { rowName = "Damage", price = 60, maxLevel = 10, startLevel = 0, valuePerLevel = 1f };
    [SerializeField] private UpgradeRow endurance = new UpgradeRow { rowName = "Endurance", price = 55, maxLevel = 10, startLevel = 0, valuePerLevel = 1f };
    [SerializeField] private UpgradeRow criticalDamage = new UpgradeRow { rowName = "CriticalDamage", price = 70, maxLevel = 10, startLevel = 0, valuePerLevel = 1f };

    [Header("UI")]
    [SerializeField] private string upgradeRootName = "upgrade";
    [SerializeField] private Color emptyCircleColor = new Color(1f, 1f, 1f, 0.22f);
    [SerializeField] private Color filledCircleColor = new Color(1f, 0.85f, 0.25f, 1f);
    [SerializeField] private Color selectedRowColor = new Color(1f, 0.96f, 0.5f, 1f);
    [SerializeField] private Color unselectedRowColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField, Min(1f)] private float selectedRowScale = 1.12f;
    [SerializeField] private float selectedRowOffsetX = 24f;

    public int SelectedIndex => _selectedIndex;

    private class RowRuntime
    {
        public UpgradeRow Config;
        public List<Image> Circles = new List<Image>();
        public Image RowImage;
        public TMP_Text LevelText;
        public RectTransform RowRect;
        public Vector2 BaseAnchoredPosition;
        public Vector3 BaseScale;
        public int CurrentLevel;
    }

    private readonly List<RowRuntime> _rows = new List<RowRuntime>();
    private PlayerSouls _playerSouls;
    private PlayerHealth _playerHealth;
    private PlayerCombatUpgrades _playerCombatUpgrades;
    private bool _initialized;
    private int _selectedIndex;

    private void Awake()
    {
        BuildRows();
        CacheRowVisuals();
        RefreshCircleVisuals();
        RefreshSelectionVisuals();
    }

    public void Initialize(PlayerSouls playerSouls, PlayerHealth playerHealth)
    {
        _playerSouls = playerSouls;
        _playerHealth = playerHealth;
        _playerCombatUpgrades = (_playerHealth != null ? _playerHealth.GetComponent<PlayerCombatUpgrades>() : null)
                              ?? FindFirstObjectByType<PlayerCombatUpgrades>();
        _initialized = true;
    }

    public void HandleSelectionInput()
    {
        if (_rows.Count == 0)
        {
            return;
        }

        if (TrySelectRowByMouse())
        {
            return;
        }

        if (_rows.Count <= 1)
        {
            return;
        }

        var move = 0;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                move = 1;
            }
            else if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                move = -1;
            }
        }

        if (move == 0 && GamepadUiNavigation.TryGetNavigationMove(out var gamepadMove))
        {
            if (gamepadMove.y < 0)
            {
                move = 1;
            }
            else if (gamepadMove.y > 0)
            {
                move = -1;
            }
        }

        if (move == 0)
        {
            return;
        }

        _selectedIndex = (_selectedIndex + move + _rows.Count) % _rows.Count;
        RefreshSelectionVisuals();
    }

    private bool TrySelectRowByMouse()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return false;
        }

        var pointerPosition = Mouse.current.position.ReadValue();
        var eventCamera = ResolveEventCamera();

        for (var i = 0; i < _rows.Count; i++)
        {
            var runtime = _rows[i];
            if (runtime == null || runtime.RowRect == null)
            {
                continue;
            }

            if (!RectTransformUtility.RectangleContainsScreenPoint(runtime.RowRect, pointerPosition, eventCamera))
            {
                continue;
            }

            if (_selectedIndex != i)
            {
                _selectedIndex = i;
                RefreshSelectionVisuals();
            }

            return true;
        }

        return false;
    }

    private Camera ResolveEventCamera()
    {
        var parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return parentCanvas.worldCamera;
    }

    public bool TryPurchaseSelectedUpgrade()
    {
        if (!_initialized)
        {
            ResolvePlayerDependencies();
        }

        if (_rows.Count == 0)
        {
            return false;
        }

        var row = _rows[Mathf.Clamp(_selectedIndex, 0, _rows.Count - 1)];
        if (row == null || row.Config == null)
        {
            return false;
        }

        var rowMaxLevel = Mathf.Max(1, row.Config.maxLevel);
        if (row.CurrentLevel >= rowMaxLevel)
        {
            return false;
        }

        if (_playerSouls == null)
        {
            return false;
        }

        if (!_playerSouls.SpendSouls(Mathf.Max(0, row.Config.price)))
        {
            return false;
        }

        row.CurrentLevel++;

        if (_playerHealth != null &&
            row.Config.rowName.Equals("Life", System.StringComparison.OrdinalIgnoreCase) &&
            row.Config.valuePerLevel > 0f)
        {
            _playerHealth.IncreaseMaxHealth(row.Config.valuePerLevel, true);
        }

        if (_playerCombatUpgrades != null &&
            row.Config.rowName.Equals("Damage", System.StringComparison.OrdinalIgnoreCase) &&
            row.Config.valuePerLevel > 0f)
        {
            _playerCombatUpgrades.AddFlatDamageBonus(row.Config.valuePerLevel);
        }

        RefreshCircleVisuals();
        RefreshSelectionVisuals();
        return true;
    }

    private void ResolvePlayerDependencies()
    {
        if (_playerSouls == null)
        {
            _playerSouls = FindFirstObjectByType<PlayerSouls>();
        }

        if (_playerHealth == null)
        {
            _playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (_playerCombatUpgrades == null)
        {
            _playerCombatUpgrades = (_playerHealth != null ? _playerHealth.GetComponent<PlayerCombatUpgrades>() : null)
                                  ?? FindFirstObjectByType<PlayerCombatUpgrades>();
        }

        _initialized = true;
    }

    private void BuildRows()
    {
        _rows.Clear();

        AddRow(life);
        AddRow(damage);
        AddRow(endurance);
        AddRow(criticalDamage);

        if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
        {
            _selectedIndex = 0;
        }
    }

    private void AddRow(UpgradeRow row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.rowName))
        {
            return;
        }

        var runtime = new RowRuntime
        {
            Config = row,
            CurrentLevel = Mathf.Clamp(row.startLevel, 0, Mathf.Max(1, row.maxLevel))
        };

        _rows.Add(runtime);
    }

    private void CacheRowVisuals()
    {
        var root = FindUpgradeRoot();
        if (root == null)
        {
            return;
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            var runtime = _rows[i];
            if (runtime == null || runtime.Config == null)
            {
                continue;
            }

            var row = FindChildByName(root, runtime.Config.rowName);
            if (row == null)
            {
                continue;
            }

            runtime.Circles = CollectRowCircles(row, runtime.Config.maxLevel);
            runtime.RowImage = row.GetComponent<Image>();
            runtime.LevelText = ResolveLevelText(row);
            runtime.RowRect = row as RectTransform;
            if (runtime.RowRect != null)
            {
                runtime.BaseAnchoredPosition = runtime.RowRect.anchoredPosition;
                runtime.BaseScale = runtime.RowRect.localScale;
            }
        }
    }

    private Transform FindUpgradeRoot()
    {
        if (string.IsNullOrWhiteSpace(upgradeRootName))
        {
            return transform;
        }

        var found = FindChildByName(transform, upgradeRootName);
        return found != null ? found : transform;
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        if (root.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        for (var i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            var nested = FindChildByName(child, targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private List<Image> CollectRowCircles(Transform row, int maxLevelForRow)
    {
        var candidates = new List<Image>();
        for (var i = 0; i < row.childCount; i++)
        {
            var child = row.GetChild(i);
            var image = child.GetComponent<Image>();
            if (image == null)
            {
                continue;
            }

            candidates.Add(image);
        }

        candidates.Sort((a, b) =>
        {
            var aPos = ((RectTransform)a.transform).anchoredPosition.x;
            var bPos = ((RectTransform)b.transform).anchoredPosition.x;
            return aPos.CompareTo(bPos);
        });

        var rowMaxLevel = Mathf.Max(1, maxLevelForRow);
        if (candidates.Count > rowMaxLevel)
        {
            var circlesOnly = new List<Image>(rowMaxLevel);
            for (var i = candidates.Count - rowMaxLevel; i < candidates.Count; i++)
            {
                circlesOnly.Add(candidates[i]);
            }

            return circlesOnly;
        }

        return candidates;
    }

    private void RefreshCircleVisuals()
    {
        for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
        {
            var runtime = _rows[rowIndex];
            if (runtime == null)
            {
                continue;
            }

            var circles = runtime.Circles;
            for (var circleIndex = 0; circleIndex < circles.Count; circleIndex++)
            {
                var image = circles[circleIndex];
                if (image == null)
                {
                    continue;
                }

                image.color = circleIndex < runtime.CurrentLevel ? filledCircleColor : emptyCircleColor;
            }

            if (runtime.LevelText != null)
            {
                runtime.LevelText.text = $"Level {runtime.CurrentLevel}";
            }
        }
    }

    private static TMP_Text ResolveLevelText(Transform row)
    {
        var texts = row.GetComponentsInChildren<TMP_Text>(true);
        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (text.text != null && text.text.ToLowerInvariant().Contains("level"))
            {
                return text;
            }
        }

        return texts.Length > 0 ? texts[0] : null;
    }

    private void RefreshSelectionVisuals()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            var runtime = _rows[i];
            if (runtime == null || runtime.RowImage == null)
            {
                if (runtime != null && runtime.RowRect != null)
                {
                    var rowIsSelected = i == _selectedIndex;
                    runtime.RowRect.anchoredPosition = runtime.BaseAnchoredPosition + new Vector2(rowIsSelected ? selectedRowOffsetX : 0f, 0f);
                    runtime.RowRect.localScale = runtime.BaseScale * (rowIsSelected ? selectedRowScale : 1f);
                }

                continue;
            }

            var isSelected = i == _selectedIndex;
            runtime.RowImage.color = isSelected ? selectedRowColor : unselectedRowColor;

            if (runtime.RowRect != null)
            {
                runtime.RowRect.anchoredPosition = runtime.BaseAnchoredPosition + new Vector2(isSelected ? selectedRowOffsetX : 0f, 0f);
                runtime.RowRect.localScale = runtime.BaseScale * (isSelected ? selectedRowScale : 1f);
            }
        }
    }
}