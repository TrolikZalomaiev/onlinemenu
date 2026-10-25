using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZombieSurvival.EditorTools
{
    /// <summary>
    /// Меню «Zombie Survival» в редакторе: одной кнопкой настраивает URP и собирает
    /// играбельную сцену-прототип из бесплатных моделей Kenney (CC0).
    /// Повторный запуск пересоздаёт сцену и префабы.
    /// </summary>
    public static class PrototypeBuilder
    {
        const string Gen = "Assets/ZombieSurvival/Generated";
        const string Kenney = "Assets/ThirdParty/Kenney";
        const string ScenePath = "Assets/ZombieSurvival/Scenes/Prototype.unity";
        const float GridSize = 2f;
        const float WorldSize = 240f;
        const float TownRadius = 45f;

        // Если модели смотрят не туда — поправь эти углы и пересобери прототип.
        const float CharacterYaw = 0f;
        const float WeaponYaw = 180f;
        const float HouseFrontYaw = 0f;

        struct Materials
        {
            public Material grass, road, wood, stone, metal, preview;
        }

        // ------------------------------------------------------------------ URP

        [MenuItem("Zombie Survival/1. Настроить графику (URP)", priority = 1)]
        public static void SetupGraphics()
        {
            EnsureFolder(Gen);

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>($"{Gen}/URP_Asset.asset");
            if (asset == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, $"{Gen}/URP_Renderer.asset");

                asset = UniversalRenderPipelineAsset.Create(rendererData);
                asset.shadowDistance = 90f;
                asset.msaaSampleCount = 4;
                asset.supportsHDR = true;
                AssetDatabase.CreateAsset(asset, $"{Gen}/URP_Asset.asset");
            }

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);

            AssetDatabase.SaveAssets();

            // Модели импортировались до включения URP — переимпортируем, чтобы материалы стали URP/Lit.
            AssetDatabase.ImportAsset(Kenney, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Debug.Log("[ZombieSurvival] URP настроен.");
        }

        // ------------------------------------------------------------ Прототип

        [MenuItem("Zombie Survival/2. Собрать прототип сцены", priority = 2)]
        public static void BuildPrototype()
        {
            if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset)) SetupGraphics();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder($"{Gen}/Materials");
            EnsureFolder($"{Gen}/Prefabs");
            EnsureFolder("Assets/ZombieSurvival/Scenes");

            var mats = CreateMaterials();

            // Постройки
            var woodBlock = CreateBlockPrefab("Block_Wood", mats.wood, 250f);
            var stoneBlock = CreateBlockPrefab("Block_Stone", mats.stone, 600f);
            var metalBlock = CreateBlockPrefab("Block_Metal", mats.metal, 1000f);

            // Зомби — три разных персонажа, окрашенных в «мертвецкий» зелёный.
            var zombies = new[]
            {
                CreateZombiePrefab("Zombie_A", "BlockyCharacters/character-c", new Color(0.55f, 0.85f, 0.45f)),
                CreateZombiePrefab("Zombie_B", "BlockyCharacters/character-f", new Color(0.6f, 0.8f, 0.55f)),
                CreateZombiePrefab("Zombie_C", "BlockyCharacters/character-k", new Color(0.5f, 0.75f, 0.5f)),
            };

            // Лут и ресурсы
            var crate = CreateLootPrefab("Loot_Crate", "SurvivalKit/box-large", "Ящик", 1.1f, false, 600f, null);
            var chest = CreateLootPrefab("Loot_Chest", "SurvivalKit/chest", "Сундук", 0.9f, false, 900f, new[]
            {
                Loot(ItemType.Ammo, 10, 25, 0.8f), Loot(ItemType.Gunpowder, 2, 4, 0.7f),
                Loot(ItemType.Scrap, 2, 5, 0.7f), Loot(ItemType.Medkit, 1, 2, 0.4f),
            });
            var barrel = CreateLootPrefab("Loot_Barrel", "SurvivalKit/barrel", "Бочка", 1.2f, false, 600f, new[]
            {
                Loot(ItemType.Gunpowder, 1, 4, 0.9f), Loot(ItemType.Scrap, 1, 2, 0.5f),
            });
            var scrap = CreateLootPrefab("Loot_Scrap", "SurvivalKit/metal-panel-screws", "Металлолом", 1.2f, true, 240f, new[]
            {
                Loot(ItemType.Scrap, 2, 4, 1f),
            });
            var tree = CreateLootPrefab("Resource_Tree", "SurvivalKit/tree-tall", "Дерево", 7f, true, 120f, new[]
            {
                Loot(ItemType.Wood, 3, 6, 1f),
            }, trunkCollider: true);
            var rock = CreateLootPrefab("Resource_Rock", "SurvivalKit/rock-a", "Камень", 1.3f, true, 180f, new[]
            {
                Loot(ItemType.Stone, 2, 5, 1f), Loot(ItemType.Scrap, 1, 1, 0.2f),
            });

            var player = CreatePlayerPrefab(mats.preview, new[]
            {
                ("Деревянный блок", woodBlock, ItemType.Wood, 4),
                ("Каменный блок", stoneBlock, ItemType.Stone, 6),
                ("Металлический блок", metalBlock, ItemType.Scrap, 5),
            });

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = CreateLighting();
            CreatePostProcessing();
            var world = CreateWorld(mats, new WorldPrefabs
            {
                crate = crate, chest = chest, barrel = barrel, scrap = scrap, tree = tree, rock = rock
            });

            CreateGameSystems(sun, zombies);
            CreateNetworkManager(player, zombies, new[] { woodBlock, stoneBlock, metalBlock });
            CreateMenuCamera();

            var spawn = new GameObject("PlayerSpawnPoint");
            spawn.transform.position = new Vector3(5f, 0f, 5f);
            spawn.AddComponent<PlayerSpawnPoint>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = world;
            Debug.Log("[ZombieSurvival] Прототип собран. Нажми Play и выбери «Создать игру (хост)».");
        }

        // ---------------------------------------------------------- Материалы

        static Materials CreateMaterials()
        {
            var preview = Mat("BuildPreview", new Color(0.3f, 1f, 0.4f, 0.4f));
            MakeTransparent(preview);

            return new Materials
            {
                grass = Mat("Ground_Grass", new Color(0.42f, 0.62f, 0.3f)),
                road = Mat("Road_Asphalt", new Color(0.24f, 0.24f, 0.26f)),
                wood = Mat("Block_Wood", new Color(0.62f, 0.43f, 0.26f)),
                stone = Mat("Block_Stone", new Color(0.55f, 0.55f, 0.58f)),
                metal = Mat("Block_Metal", new Color(0.42f, 0.47f, 0.52f), 0.45f, 0.7f),
                preview = preview,
            };
        }

        static Shader LitShader() => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        static Material Mat(string name, Color color, float smoothness = 0.1f, float metallic = 0f)
        {
            string path = $"{Gen}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(LitShader());
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = LitShader();
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void MakeTransparent(Material mat)
        {
            mat.SetOverrideTag("RenderType", "Transparent");
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
        }

        static Material TintedCopy(Material source, string name, Color tint)
        {
            string path = $"{Gen}/Materials/{name}.mat";
            AssetDatabase.DeleteAsset(path);
            var mat = new Material(source) { name = name };
            mat.color = source.color * tint;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ------------------------------------------------------------ Префабы

        static GameObject CreateBlockPrefab(string name, Material material, float hp)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            root.transform.localScale = Vector3.one * GridSize;
            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            root.AddComponent<NetworkObject>();
            var health = root.AddComponent<Health>();
            SetFields(health, ("maxHealth", hp), ("onDeath", (int)DeathBehavior.Despawn));
            var block = root.AddComponent<BuildBlock>();
            SetObjectArray(block, "damageTintRenderers", new Object[] { renderer });

            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            return SavePrefab(root, name);
        }

        static GameObject CreateZombiePrefab(string name, string model, Color tint)
        {
            var root = new GameObject(name);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.speed = 2f;
            agent.acceleration = 14f;
            agent.angularSpeed = 400f;
            agent.stoppingDistance = 1.2f;
            agent.autoBraking = false;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.35f;

            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.SyncRotAngleX = nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;

            var health = root.AddComponent<Health>();
            SetFields(health, ("maxHealth", 100f), ("onDeath", (int)DeathBehavior.Despawn));
            root.AddComponent<ZombieAI>();

            var visual = InstantiateModel(model, root.transform);
            visual.name = "Model";
            visual.transform.localRotation = Quaternion.Euler(0f, CharacterYaw, 0f);
            FitToHeight(visual, root.transform, 1.85f);

            foreach (var r in visual.GetComponentsInChildren<Renderer>())
            {
                var src = r.sharedMaterials;
                var dst = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                    dst[i] = src[i] != null ? TintedCopy(src[i], $"{name}_Skin{i}", tint) : null;
                r.sharedMaterials = dst;
            }

            var zombieVisual = root.AddComponent<ZombieVisual>();
            SetFields(zombieVisual, ("model", visual.transform));

            return SavePrefab(root, name);
        }

        static LootContainer.LootEntry Loot(ItemType item, int min, int max, float chance) =>
            new LootContainer.LootEntry { item = item, min = min, max = max, chance = chance };

        static GameObject CreateLootPrefab(string name, string model, string displayName, float height, bool hideWhenLooted,
            float respawn, LootContainer.LootEntry[] loot, bool trunkCollider = false)
        {
            var root = new GameObject(name);
            var visual = InstantiateModel(model, root.transform);
            visual.name = "Model";
            visual.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            FitToHeight(visual, root.transform, height);

            if (trunkCollider)
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = 0.45f;
                capsule.height = 4f;
                capsule.center = new Vector3(0f, 2f, 0f);
            }
            else
            {
                var b = RendererBounds(visual);
                var box = root.AddComponent<BoxCollider>();
                box.center = b.center - root.transform.position;
                box.size = b.size;
            }

            root.AddComponent<NetworkObject>();
            var container = root.AddComponent<LootContainer>();
            SetFields(container, ("displayName", displayName), ("respawnSeconds", respawn));
            if (hideWhenLooted) SetFields(container, ("hideWhenLooted", visual));
            if (loot != null) SetLootEntries(container, loot);

            return SavePrefab(root, name);
        }

        static GameObject CreatePlayerPrefab(Material previewMaterial, (string name, GameObject prefab, ItemType item, int cost)[] blocks)
        {
            var root = new GameObject("Player");

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;

            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<OwnerNetworkTransform>();
            nt.SyncRotAngleX = nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;

            // Тело
            var body = InstantiateModel("BlockyCharacters/character-a", root.transform);
            body.name = "Body";
            body.transform.localRotation = Quaternion.Euler(0f, CharacterYaw, 0f);
            FitToHeight(body, root.transform, 1.8f);

            // Голова с камерой
            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            head.tag = "MainCamera";
            var cam = head.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 400f;
            cam.fieldOfView = 75f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.enabled = false;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var listener = head.AddComponent<AudioListener>();
            listener.enabled = false;

            // Оружие в руках
            var holder = new GameObject("WeaponHolder");
            holder.transform.SetParent(head.transform, false);
            holder.transform.localPosition = new Vector3(0.22f, -0.2f, 0.42f);
            var gun = InstantiateModel("BlasterKit/blaster-b", holder.transform);
            gun.name = "Gun";
            gun.transform.localRotation = Quaternion.Euler(0f, WeaponYaw, 0f);
            FitToMaxSize(gun, 0.55f);
            CenterOn(gun, holder.transform);
            foreach (var r in gun.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(holder.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.04f, 0.3f);

            var controller = root.AddComponent<PlayerController>();
            SetFields(controller, ("head", head.transform), ("playerCamera", cam), ("bodyVisual", body.transform));

            root.AddComponent<PlayerInventory>();
            var health = root.AddComponent<Health>();
            SetFields(health, ("maxHealth", 100f), ("onDeath", (int)DeathBehavior.Respawn));

            var weapon = root.AddComponent<Weapon>();
            SetFields(weapon, ("muzzle", muzzle.transform), ("model", holder.transform));

            var building = root.AddComponent<BuildingSystem>();
            SetFields(building, ("previewMaterial", previewMaterial), ("gridSize", GridSize));
            var so = new SerializedObject(building);
            var arr = so.FindProperty("blocks");
            arr.arraySize = blocks.Length;
            for (int i = 0; i < blocks.Length; i++)
            {
                var el = arr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("name").stringValue = blocks[i].name;
                el.FindPropertyRelative("prefab").objectReferenceValue = blocks[i].prefab.GetComponent<NetworkObject>();
                el.FindPropertyRelative("costItem").enumValueIndex = (int)blocks[i].item;
                el.FindPropertyRelative("cost").intValue = blocks[i].cost;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<CraftingSystem>();
            root.AddComponent<PlayerInteractor>();
            root.AddComponent<HUD>();

            return SavePrefab(root, "Player");
        }

        // ---------------------------------------------------------------- Сцена

        static Light CreateLighting()
        {
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.intensity = 1.2f;
            sunGo.transform.rotation = Quaternion.Euler(50f, 160f, 0f);

            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.64f, 0.7f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 150f;
            RenderSettings.fogColor = new Color(0.66f, 0.79f, 0.88f);
            return sun;
        }

        static void CreatePostProcessing()
        {
            string path = $"{Gen}/PostProcessing.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.5f);
            bloom.threshold.Override(1f);
            bloom.scatter.Override(0.6f);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(18f);
            color.contrast.Override(12f);
            color.postExposure.Override(0.15f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.25f);

            foreach (var component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);

            var go = new GameObject("PostProcessing");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        struct WorldPrefabs
        {
            public GameObject crate, chest, barrel, scrap, tree, rock;
        }

        static GameObject CreateWorld(Materials mats, WorldPrefabs prefabs)
        {
            var world = new GameObject("World");
            var surface = world.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            world.AddComponent<RuntimeNavMeshBaker>();

            // Низкополигональная земля
            string meshPath = $"{Gen}/Ground.asset";
            AssetDatabase.DeleteAsset(meshPath);
            var mesh = LowPolyTerrain.Generate(WorldSize, 4f, 7f, TownRadius, 1234);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var ground = new GameObject("Ground");
            ground.transform.SetParent(world.transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            ground.AddComponent<MeshRenderer>().sharedMaterial = mats.grass;
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;
            ground.isStatic = true;
            groundCollider = ground.GetComponent<MeshCollider>();

            // Дороги-перекрёсток
            CreateRoad(world.transform, mats.road, new Vector3(0f, 0.02f, 0f), new Vector3(8f, 0.04f, TownRadius * 2f));
            CreateRoad(world.transform, mats.road, new Vector3(0f, 0.021f, 0f), new Vector3(TownRadius * 2f, 0.04f, 8f));

            Physics.SyncTransforms();
            var rng = new System.Random(42);
            var occupied = new List<(Vector3 pos, float radius)>();

            // Дома вдоль дорог, рядом ящики и бочки
            var houseParent = new GameObject("Houses").transform;
            houseParent.SetParent(world.transform, false);
            var lootParent = new GameObject("Loot").transform;
            lootParent.SetParent(world.transform, false);

            char houseType = 'a';
            float[] along = { -36f, -20f, 20f, 36f };
            foreach (float a in along)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    // Вдоль дороги по Z и вдоль дороги по X
                    PlaceHouse(new Vector3(side * 15f, 0f, a), side > 0 ? -90f : 90f);
                    PlaceHouse(new Vector3(a, 0f, side * 15f), side > 0 ? 180f : 0f);
                }
            }

            void PlaceHouse(Vector3 position, float yawToRoad)
            {
                string model = $"CitySuburban/building-type-{houseType}";
                houseType = houseType == 'u' ? 'a' : (char)(houseType + 1);

                var house = InstantiateModel(model, houseParent);
                house.transform.position = position;
                house.transform.rotation = Quaternion.Euler(0f, yawToRoad + HouseFrontYaw, 0f);
                FitToMaxFootprint(house, 10f);
                CenterOnGround(house, position);
                AddMeshColliders(house);
                house.isStatic = true;
                occupied.Add((position, 7f));

                // Лут во дворе, между домом и дорогой
                Vector3 toRoad = Quaternion.Euler(0f, yawToRoad, 0f) * Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, toRoad);
                PlaceOnGround(rng.NextDouble() < 0.25 ? prefabs.chest : prefabs.crate, lootParent, position + toRoad * 6.5f + right * 2.5f, rng);
                if (rng.NextDouble() < 0.7)
                    PlaceOnGround(prefabs.barrel, lootParent, position + toRoad * 6.5f - right * 2.5f, rng);
            }

            // Металлолом у дорог
            for (int i = 0; i < 14; i++)
            {
                Vector3 p = rng.NextDouble() < 0.5
                    ? new Vector3(RandSign(rng) * 5.5f, 0f, RandRange(rng, -TownRadius + 4f, TownRadius - 4f))
                    : new Vector3(RandRange(rng, -TownRadius + 4f, TownRadius - 4f), 0f, RandSign(rng) * 5.5f);
                if (p.magnitude < 9f) continue;
                PlaceOnGround(prefabs.scrap, lootParent, p, rng);
            }

            // Лес и камни за посёлком
            var natureParent = new GameObject("Nature").transform;
            natureParent.SetParent(world.transform, false);
            ScatterOutsideTown(prefabs.tree, natureParent, 90, TownRadius - 5f, WorldSize * 0.45f, 0.8f, 1.3f);
            ScatterOutsideTown(prefabs.rock, natureParent, 40, TownRadius - 10f, WorldSize * 0.45f, 0.7f, 1.6f);

            // Декоративная трава (без коллайдеров и без сети)
            var grassModel = LoadModel("SurvivalKit/grass-large");
            if (grassModel != null)
            {
                var grassParent = new GameObject("Grass").transform;
                grassParent.SetParent(world.transform, false);
                for (int i = 0; i < 220; i++)
                {
                    Vector3 p = new Vector3(RandRange(rng, -100f, 100f), 0f, RandRange(rng, -100f, 100f));
                    if (Mathf.Abs(p.x) < 5f || Mathf.Abs(p.z) < 5f) continue;
                    var g = (GameObject)PrefabUtility.InstantiatePrefab(grassModel, grassParent);
                    g.transform.rotation = Quaternion.Euler(0f, RandRange(rng, 0f, 360f), 0f);
                    FitToHeight(g, null, RandRange(rng, 0.4f, 0.8f));
                    CenterOnGround(g, p);
                    foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            return world;

            void ScatterOutsideTown(GameObject prefab, Transform parent, int count, float minR, float maxR, float minScale, float maxScale)
            {
                int placed = 0;
                for (int attempt = 0; attempt < count * 5 && placed < count; attempt++)
                {
                    float angle = RandRange(rng, 0f, Mathf.PI * 2f);
                    float r = Mathf.Sqrt(RandRange(rng, minR * minR, maxR * maxR));
                    var p = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                    if (Mathf.Abs(p.x) < 6f || Mathf.Abs(p.z) < 6f) continue;
                    bool blocked = false;
                    foreach (var o in occupied)
                        if (Vector3.Distance(o.pos, p) < o.radius + 2f) { blocked = true; break; }
                    if (blocked) continue;

                    var go = PlaceOnGround(prefab, parent, p, rng);
                    go.transform.localScale = Vector3.one * RandRange(rng, minScale, maxScale);
                    occupied.Add((p, 2f));
                    placed++;
                }
            }
        }

        static void CreateRoad(Transform parent, Material material, Vector3 center, Vector3 size)
        {
            var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = "Road";
            road.transform.SetParent(parent, false);
            road.transform.position = center;
            road.transform.localScale = size;
            road.GetComponent<MeshRenderer>().sharedMaterial = material;
            road.isStatic = true;
        }

        static GameObject PlaceOnGround(GameObject prefab, Transform parent, Vector3 position, System.Random rng)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = new Vector3(position.x, GroundHeight(position), position.z);
            go.transform.rotation = Quaternion.Euler(0f, RandRange(rng, 0f, 360f), 0f);
            return go;
        }

        static MeshCollider groundCollider;

        /// <summary>Высота земли (только меш земли, без домов и деревьев).</summary>
        static float GroundHeight(Vector3 p)
        {
            if (groundCollider == null) return 0f;
            var ray = new Ray(new Vector3(p.x, 100f, p.z), Vector3.down);
            return groundCollider.Raycast(ray, out RaycastHit hit, 300f) ? hit.point.y : 0f;
        }

        static void CreateGameSystems(Light sun, GameObject[] zombies)
        {
            var go = new GameObject("GameSystems");
            go.AddComponent<NetworkObject>();
            var cycle = go.AddComponent<DayNightCycle>();
            SetFields(cycle, ("sun", sun));
            var spawner = go.AddComponent<ZombieSpawner>();
            var refs = new Object[zombies.Length];
            for (int i = 0; i < zombies.Length; i++) refs[i] = zombies[i].GetComponent<NetworkObject>();
            SetObjectArray(spawner, "zombiePrefabs", refs);
        }

        static void CreateNetworkManager(GameObject player, GameObject[] zombies, GameObject[] blocks)
        {
            var go = new GameObject("NetworkManager");
            var nm = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            nm.NetworkConfig ??= new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = player;
            nm.NetworkConfig.EnableSceneManagement = true;

            var menu = go.AddComponent<NetworkMenu>();
            var all = new List<Object>();
            all.AddRange(zombies);
            all.AddRange(blocks);
            SetObjectArray(menu, "networkPrefabs", all.ToArray());
            EditorUtility.SetDirty(nm);
        }

        static void CreateMenuCamera()
        {
            var go = new GameObject("MenuCamera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(55f, 32f, -55f);
            go.transform.LookAt(Vector3.zero);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.farClipPlane = 500f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            go.AddComponent<AudioListener>();
            go.AddComponent<MenuCamera>();
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------ Утилиты

        static GameObject LoadModel(string relativePath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Kenney}/{relativePath}.fbx");
            if (model == null) Debug.LogWarning($"[ZombieSurvival] Не найдена модель {relativePath}.fbx — будет куб.");
            return model;
        }

        static GameObject InstantiateModel(string relativePath, Transform parent)
        {
            var model = LoadModel(relativePath);
            GameObject go;
            if (model != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
            }
            go.transform.localPosition = Vector3.zero;
            return go;
        }

        static Bounds RendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Масштабирует до нужной высоты и ставит ногами на pivot родителя.</summary>
        static void FitToHeight(GameObject go, Transform root, float height)
        {
            var b = RendererBounds(go);
            if (b.size.y > 0.0001f) go.transform.localScale *= height / b.size.y;
            if (root != null) CenterOnGround(go, root.position, false);
        }

        static void FitToMaxSize(GameObject go, float size)
        {
            var b = RendererBounds(go);
            float max = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (max > 0.0001f) go.transform.localScale *= size / max;
        }

        static void FitToMaxFootprint(GameObject go, float size)
        {
            var b = RendererBounds(go);
            float max = Mathf.Max(b.size.x, b.size.z);
            if (max > 0.0001f) go.transform.localScale *= size / max;
        }

        static void CenterOn(GameObject go, Transform target)
        {
            var b = RendererBounds(go);
            go.transform.position += target.position - b.center;
        }

        /// <summary>Центрирует по XZ на точке и ставит низом модели на землю.</summary>
        static void CenterOnGround(GameObject go, Vector3 point, bool snapToGround = true)
        {
            var b = RendererBounds(go);
            float groundY = snapToGround ? GroundHeight(point) : point.y;
            go.transform.position += new Vector3(point.x - b.center.x, groundY - b.min.y, point.z - b.center.z);
        }

        static void AddMeshColliders(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Gen}/Prefabs/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void SetFields(Object target, params (string name, object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var (name, value) in fields)
            {
                var p = so.FindProperty(name);
                if (p == null)
                {
                    Debug.LogError($"[ZombieSurvival] Поле {name} не найдено в {target.GetType().Name}");
                    continue;
                }
                switch (value)
                {
                    case float f: p.floatValue = f; break;
                    case int i when p.propertyType == SerializedPropertyType.Enum: p.enumValueIndex = i; break;
                    case int i: p.intValue = i; break;
                    case string s: p.stringValue = s; break;
                    case Object o: p.objectReferenceValue = o; break;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetObjectArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetLootEntries(LootContainer container, LootContainer.LootEntry[] loot)
        {
            var so = new SerializedObject(container);
            var p = so.FindProperty("loot");
            p.arraySize = loot.Length;
            for (int i = 0; i < loot.Length; i++)
            {
                var el = p.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("item").enumValueIndex = (int)loot[i].item;
                el.FindPropertyRelative("min").intValue = loot[i].min;
                el.FindPropertyRelative("max").intValue = loot[i].max;
                el.FindPropertyRelative("chance").floatValue = loot[i].chance;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        static float RandRange(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
        static float RandSign(System.Random rng) => rng.NextDouble() < 0.5 ? -1f : 1f;
    }
}
