# Goombata: Pavlov's Dogs

2D экшен-проект на Unity с акцентом на боевую систему, поведение врагов и стабильные переходы между сценами.

## О проекте

Проект демонстрирует полный цикл разработки игрового прототипа:
- управление персонажем через Unity Input System;
- боевые механики игрока и врагов;
- систему здоровья и состояний (урон, смерть);
- переходы между сценами со спавном в заданной точке;
- автоматическую перенастройку виртуальных камер после загрузки сцены.

## Ключевые механики

- Передвижение, бег, прыжок и атака игрока
- Патрулирование врагов, обнаружение игрока, преследование и атака
- Обработка урона и смерти у игрока и противников
- Переходы между уровнями с сохранением корректного игрового состояния

## Управление

### Клавиатура и мышь

| Действие | Клавиша |
|---|---|
| Движение | W / A / S / D |
| Прыжок | Space |
| Бег | Left Shift |
| Атака | Левая кнопка мыши |

### Геймпад

| Действие | Кнопка |
|---|---|
| Движение | Left Stick |
| Прыжок | Button South (A / Cross) |
| Бег | Button East (B / Circle) |
| Атака | Right Trigger |

## Структура проекта

```text
Goombata-Pavlov-s-Dogs/
├── Assets/
│   ├── Audio/
│   ├── Configs/
│   ├── Dialogue/
│   ├── Enemy/
│   ├── Inputs/
│   ├── Level/
│   ├── Menu/
│   ├── NPC/
│   ├── Player/
│   ├── Scene/
│   ├── Shop/
│   ├── UI/
│   └── Weapons/
│       ├── Bullet/
│       ├── Handlers/
│       └── ScriptableObject/
├── Packages/
├── ProjectSettings/
├── README.md
└── .gitignore
```

## Архитектура кода

```text
Assets/Scripts/
├── Audio/
├── Configs/
├── Dialogue/
├── Enemy/
├── Inputs/
├── Level/
├── Menu/
├── NPC/
├── Player/
├── Scene/
├── Shop/
├── UI/
└── Weapons/
```

## Ключевые скрипты

- [Assets/Scripts/Inputs/PlayerInputHandler.cs](Assets/Scripts/Inputs/PlayerInputHandler.cs) - обработка и трансляция команд ввода
- [Assets/Scripts/Player/PlayerMovement.cs](Assets/Scripts/Player/PlayerMovement.cs) - движение, прыжок, базовая кинематика игрока
- [Assets/Scripts/Player/PlayerAttackHandler.cs](Assets/Scripts/Player/PlayerAttackHandler.cs) - запуск атак через систему оружия
- [Assets/Scripts/Player/PlayerHealth.cs](Assets/Scripts/Player/PlayerHealth.cs) - здоровье игрока, урон, смерть
- [Assets/Scripts/Enemy/EnemyPatrolChase2D.cs](Assets/Scripts/Enemy/EnemyPatrolChase2D.cs) - патруль и преследование игрока
- [Assets/Scripts/Enemy/EnemyDamageDealer.cs](Assets/Scripts/Enemy/EnemyDamageDealer.cs) - зона атаки врага и нанесение урона
- [Assets/Scripts/Enemy/EnemyController.cs](Assets/Scripts/Enemy/EnemyController.cs) - общее состояние врага, смерть, награда
- [Assets/Scripts/Scene/SceneTransitionManager.cs](Assets/Scripts/Scene/SceneTransitionManager.cs) - переходы сцен, спавн, привязка камер
- [Assets/Scripts/Level/GameplayMechanicsBootstrapper.cs](Assets/Scripts/Level/GameplayMechanicsBootstrapper.cs) - инициализация игровых механик

## Как запустить

1. Открыть проект в Unity 2022 LTS или новее.
2. Дождаться импорта ассетов.
3. Убедиться, что нужные сцены добавлены в Build Settings.
4. Нажать Play для запуска в редакторе.

## Что проверить комиссии

- Корректность управления персонажем (движение, прыжок, бег, атака)
- Реакцию врагов на игрока (обнаружение, погоня, урон)
- Переходы между сценами и корректный спавн
- Стабильность камеры после загрузки новой сцены

## Технический стек

- Unity (C#)
- Unity Input System
- Cinemachine
- 2D Physics (Rigidbody2D, Collider2D)

## Репозиторий для QR-кода

Вставьте сюда ссылку на удаленный репозиторий после публикации:

 - GitHub: https://github.com/alice0709fox-afk/Goombata-Pavlov-s-Dogs

После этого ссылку можно закодировать в QR и поместить в пояснительную записку.
