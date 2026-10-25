# Zombie Survival — кооп-выживание в стиле 7 Days to Die

Низкополигональная графика в духе POLYGON / Synty. Днём — лут, крафт патронов, стройка базы.
Ночью — орда зомби, которая растёт каждый день. Движок Unity 6 LTS, мультиплеер — Netcode for GameObjects.

## Быстрый старт (Windows)

1. Скачай `setup-windows.ps1` из этой папки, нажми правой кнопкой → **Выполнить с помощью PowerShell**.
   Скрипт сам:
   - установит **Git**, **Unity Hub**, **VS Code**;
   - создаст папку **`ZombieSurvival` на рабочем столе** и скачает туда проект;
   - поставит **Unity 6000.3.25f1 (6.3 LTS)**.
2. Открой Unity Hub → **Add → Add project from disk** → выбери `Рабочий стол\ZombieSurvival`.
   Первое открытие займёт несколько минут (Unity скачает пакеты и импортирует модели).
3. В Unity в верхнем меню: **Zombie Survival → 2. Собрать прототип сцены**.
   Скрипт сам настроит графику (URP), создаст префабы игрока, зомби, ящиков, блоков и соберёт карту.
4. Нажми **Play** → **Создать игру (хост)**.

> Если Unity предложит включить новую систему ввода (Input System) и перезапуститься — соглашайся.

### Играть вдвоём

- **По локальной сети / Radmin VPN / Hamachi:** один жмёт «Создать игру», второй вводит его IP и жмёт «Подключиться» (порт 7777).
- **Через интернет без VPN** — следующий шаг: подключить Unity Relay (бесплатно до 50 игроков онлайн). Пока в списке задач.
- Для теста на одном ПК: **Window → Multiplayer → Multiplayer Play Mode**, включи второго виртуального игрока.

## Управление

| Клавиша | Действие |
|---|---|
| WASD / Shift / Space | ходьба / бег / прыжок |
| ЛКМ / R | стрельба / перезарядка |
| E | обыскать ящик, бочку, срубить дерево, добыть камень |
| Tab | меню крафта (патроны, порох, аптечки) |
| H | использовать аптечку |
| B | режим стройки, 1/2/3 — дерево / камень / металл |
| ЛКМ / ПКМ (в стройке) | поставить / разобрать блок |
| Esc | освободить курсор |

## Что уже сделано

| Система | Файл |
|---|---|
| Смена дня и ночи, свет, туман | `Scripts/Core/DayNightCycle.cs` |
| Здоровье, урон, респаун | `Scripts/Core/Health.cs` |
| Движение от первого лица | `Scripts/Player/PlayerController.cs` |
| Инвентарь | `Scripts/Player/PlayerInventory.cs` |
| Оружие (хитскан, магазин, перезарядка) | `Scripts/Weapons/Weapon.cs` |
| Крафт | `Scripts/Crafting/CraftingSystem.cs` |
| Стройка по сетке + разбор | `Scripts/Building/BuildingSystem.cs` |
| ИИ зомби (днём бродят, ночью бегут, ломают стены) | `Scripts/Zombies/ZombieAI.cs` |
| Орды по ночам | `Scripts/Zombies/ZombieSpawner.cs` |
| Лут и ресурсы | `Scripts/Loot/LootContainer.cs` |
| Меню хост / подключение | `Scripts/Networking/NetworkMenu.cs` |
| Низкополигональная земля | `Scripts/World/LowPolyTerrain.cs` |
| Автосборка сцены | `Scripts/Editor/PrototypeBuilder.cs` |

Все параметры (скорость зомби, длина дня, размер орды, рецепты, лут) настраиваются в инспекторе Unity на префабах в `Assets/ZombieSurvival/Generated/Prefabs` и на объекте `GameSystems` в сцене.

## Бесплатные модели

В проект уже скачаны модели **Kenney** (лицензия CC0 — можно использовать бесплатно, даже в коммерческой игре):

| Пакет | Что внутри | Папка |
|---|---|---|
| Blocky Characters | 18 персонажей (игроки и зомби) | `Assets/ThirdParty/Kenney/BlockyCharacters` |
| Blaster Kit | оружие, магазины, прицелы, гранаты | `Assets/ThirdParty/Kenney/BlasterKit` |
| Survival Kit | ящики, бочки, деревья, камни, инструменты, верстаки, костёр, палатки | `Assets/ThirdParty/Kenney/SurvivalKit` |
| City Kit Suburban | дома, заборы, дорожки | `Assets/ThirdParty/Kenney/CitySuburban` |

### Чтобы было ещё ближе к стилю POLYGON

Скачай и импортируй (Unity → Window → Package Manager → My Assets, или перетащи .unitypackage / .fbx в окно Project):

| Что | Где | Лицензия |
|---|---|---|
| **POLYGON Starter Pack** (Synty) — тот самый стиль со скриншота | Unity Asset Store, бесплатно | Asset Store EULA |
| **Quaternius: Zombie Apocalypse Kit, Ultimate Zombies, Ultimate Guns, Modular Buildings** | quaternius.com | CC0 |
| **KayKit: City Builder, Adventurers, Prototype Bits** | kaylousberg.itch.io | CC0 |
| **Анимации** (бег, удар, смерть зомби) | mixamo.com (нужен Adobe-аккаунт, бесплатно) | бесплатно для игр |
| Звуки выстрелов и зомби | freesound.org, kenney.nl/assets (Impact/Interface sounds) | CC0 / CC-BY |

Модели Synty и Asset Store нельзя выкладывать в публичный репозиторий — храни их только локально (папку можно добавить в `.gitignore`).

## Если что-то пошло не так

- **Всё розовое** → меню **Zombie Survival → 1. Настроить графику (URP)**.
- **Персонажи или оружие повёрнуты не той стороной** → в `PrototypeBuilder.cs` поменяй `CharacterYaw` / `WeaponYaw` (0 или 180) и пересобери прототип.
- **Unity ругается на версии пакетов** → Window → Package Manager → обнови пакет до предложенной версии.
- **Ошибки компиляции** → скопируй текст из Console и отправь мне.

## Дальше по плану

1. Анимации персонажей и зомби (Mixamo + Animator).
2. Unity Relay + Lobby — игра с другом через интернет по коду комнаты.
3. Сохранение мира и построек.
4. Больше оружия (дробовик, винтовка, лук), ближний бой.
5. Двери, ловушки, шипы, турели.
6. Голод / жажда, фермерство.
7. Настоящий UI (инвентарь с ячейками, крафт с иконками).
8. Звуки и музыка.
