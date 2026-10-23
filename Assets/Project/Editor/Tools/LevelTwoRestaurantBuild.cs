using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class LevelTwoRestaurantWorkshop
{
    private const string PrefabPath = "Assets/Project/Prefabs/Level2.prefab";
    static partial void Dispatch(string action)
    {
        if (action == "restore-cook") RestoreCook();
        if (action == "fix-door") FixDoor();
        if (action == "configure") Configure();
        if (action == "validate") Validate();
    }
    private static void RestoreCook()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("请先退出运行模式。");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var first = PrefabUtility.LoadPrefabContents("Assets/Project/Prefabs/Level1.prefab");
        try
        {
            var puzzle = root.GetComponent<CookingPuzzle>();
            if (puzzle.guest != null) UnityEngine.Object.DestroyImmediate(puzzle.guest.gameObject);
            var original = first.transform.Find("切菜");
            if (original == null) throw new Exception("找不到原来的切菜庖厨。");
            var cook = UnityEngine.Object.Instantiate(original.gameObject, puzzle.restaurant.interior.transform);
            cook.name = "庖厨";
            cook.transform.position = new Vector3(14, 0, 0);
            puzzle.guest = cook.GetComponent<NPCInteraction>();
            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.npcName = "庖厨"; data.isCoreNPC = false;
            data.minorNPCLines = new[] { "我在这里切配食材，准备今日的宴席。", "庖厨砖画记录了古人备餐、烹饪的劳作，让我们得以看见千年前的生活。" };
            const string path = "Assets/Project/Data/Level2/Cook.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (existing == null) { AssetDatabase.CreateAsset(data, path); existing = data; }
            else { EditorUtility.CopySerialized(data, existing); UnityEngine.Object.DestroyImmediate(data); EditorUtility.SetDirty(existing); }
            puzzle.guest.dialogueData = existing;
            puzzle.guest.automaticKeyboardInteraction = false;
            foreach (var collider in cook.GetComponents<Collider2D>()) collider.isTrigger = true;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Output + "/cook-fix.txt", "PASS 恢复原切菜庖厨外观与 Animator；室内显示；独立庖厨对话。");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); PrefabUtility.UnloadPrefabContents(first); }
        Validate();
    }
    private static void FixDoor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("请先退出运行模式。");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var area = root.GetComponent<CookingRestaurant>();
            var building = area.exterior.transform.Find("饭店");
            var sprite = building.GetComponent<SpriteRenderer>();
            var collider = building.GetComponent<BoxCollider2D>();
            if (collider == null) collider = building.gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = false;
            var bounds = sprite.sprite.bounds;
            collider.size = new Vector2(bounds.size.x * .88f, bounds.size.y * .28f);
            collider.offset = new Vector2(bounds.center.x, bounds.min.y + bounds.size.y * .18f);
            var puzzle = area.puzzle;
            if (puzzle.guest == null)
            {
                var guest = UnityEngine.Object.Instantiate(puzzle.chef.gameObject, area.interior.transform);
                guest.name = "宴饮宾客";
                guest.transform.position = new Vector3(14, 0, 0);
                puzzle.guest = guest.GetComponent<NPCInteraction>();
                puzzle.guest.dialogueData = AssetDatabase.LoadAssetAtPath<DialogueData>("Assets/Project/Data/Level2/Guest.asset");
                puzzle.guest.automaticKeyboardInteraction = false;
                foreach (var c in guest.GetComponents<Collider2D>()) c.isTrigger = true;
            }
            area.buildingCollider = collider;
            // 入口跟随建筑移动；保持当前世界位置。
            area.entrance.SetParent(building, true);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            File.WriteAllText(Output + "/door-fix.txt", "PASS 饭店实体 BoxCollider2D；建筑距离交互；入口随建筑移动。\n" + collider.bounds);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Validate();
    }
    private static string Hash(string path)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));
    }
    private static string[] Layout(Transform environment)
    {
        return environment.GetComponentsInChildren<Transform>(true).Select(t =>
        {
            var renderer = t.GetComponent<SpriteRenderer>();
            return AnimationUtility.CalculateTransformPath(t, environment) + "|" + t.localPosition.ToString("R") + "|" + t.localRotation.ToString("R") + "|" + t.localScale.ToString("R") + "|" + t.gameObject.activeSelf +
                (renderer != null ? "|" + AssetDatabase.GetAssetPath(renderer.sprite) + "|" + renderer.color.ToString("R") + "|" + renderer.sortingLayerID + "|" + renderer.sortingOrder : "");
        }).ToArray();
    }
    [MenuItem("尘封砖语/第二关/配置饭店内外场")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("请先退出运行模式。");
        foreach (int i in Enumerable.Range(0, SceneManager.sceneCount))
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isDirty && scene.path.EndsWith("Level2.unity")) throw new Exception("Level2 场景有未保存修改，请先保存。");
        }
        Directory.CreateDirectory(Output);
        string firstHash = Hash("Assets/Project/Prefabs/Level1.prefab");
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        GameObject first = PrefabUtility.LoadPrefabContents("Assets/Project/Prefabs/Level1.prefab");
        try
        {
            var puzzle = root.GetComponent<CookingPuzzle>();
            if (root.GetComponent<CookingRestaurant>() != null) throw new Exception("饭店内外场已配置；请直接编辑现有节点，避免覆盖后续布景。");
            Transform environment = root.transform.Find("CookingEnvironment");
            if (puzzle == null || environment == null || puzzle.chef == null || puzzle.player == null) throw new Exception("第二关基础配置缺失。");
            File.Copy(PrefabPath, Output + "/Level2-before-restaurant.prefab", true);
            string[] layout = Layout(environment);
            File.WriteAllLines(Output + "/interior-before.txt", layout);
            Vector3 insidePlayer = puzzle.player.transform.position, insideChef = puzzle.chef.transform.position;
            var area = root.AddComponent<CookingRestaurant>();
            puzzle.restaurant = area; area.puzzle = puzzle;
            area.interior = environment.gameObject;
            area.interiorBounds = root.transform.Find("Confiner").GetComponent<PolygonCollider2D>();
            area.interiorCameraSize = root.GetComponentInChildren<CinemachineVirtualCamera>(true).m_Lens.OrthographicSize;
            area.exterior = new GameObject("RestaurantExterior"); area.exterior.transform.SetParent(root.transform, false);
            // 只复制第一关地面与边界，不复制牛、围栏、农耕、房屋、NPC 或任务。
            var ground = UnityEngine.Object.Instantiate(first.transform.Find("Ground").gameObject, area.exterior.transform);
            ground.name = "Ground";
            var bounds = UnityEngine.Object.Instantiate(first.transform.Find("Confiner").gameObject, root.transform);
            bounds.name = "ExteriorBounds"; area.exteriorBounds = bounds.GetComponent<PolygonCollider2D>();
            area.exteriorCameraSize = first.GetComponentInChildren<CinemachineVirtualCamera>(true).m_Lens.OrthographicSize;
            var building = new GameObject("饭店", typeof(SpriteRenderer)); building.transform.SetParent(area.exterior.transform, false);
            building.transform.localPosition = new Vector3(8, 1.5f, 0);
            var renderer = building.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Art/Environments/Buildings/饭店.jpg");
            if (renderer.sprite == null) throw new Exception("缺少饭店图片。");
            renderer.sortingLayerID = puzzle.chef.GetComponent<SpriteRenderer>().sortingLayerID;
            renderer.sortingOrder = 1;
            building.transform.localScale = new Vector3(8 / renderer.sprite.bounds.size.x, 5 / renderer.sprite.bounds.size.y, 1);
            area.entrance = Marker(area.exterior.transform, "饭店入口", new Vector3(8, -1.6f, 0));
            area.restaurantName = Marker(area.exterior.transform, "饭店名称锚点", new Vector3(8, 2.5f, 0));
            // 只补不可见的出口锚点，不改变室内既有地面、案几和道具布局。
            var markers = new GameObject("RestaurantMarkers"); markers.transform.SetParent(root.transform, false);
            area.exit = Marker(markers.transform, "饭店出口", new Vector3(-19, -5.2f, 0));
            // 出口随室内显示，但不插入 CookingEnvironment，便于核对原布局完全不变。
            area.exit.gameObject.SetActive(false);
            area.exteriorSpawn = Marker(markers.transform, "外场玩家出生点", first.GetComponent<FarmingPuzzle>().player.transform.position);
            area.interiorSpawn = Marker(markers.transform, "室内玩家出生点", insidePlayer);
            area.exteriorChef = Marker(markers.transform, "外场庖长位置", first.transform.Find("NPC").position);
            area.interiorChef = Marker(markers.transform, "室内庖长位置", insideChef);
            area.virtualCamera = root.GetComponentInChildren<CinemachineVirtualCamera>(true);
            area.fieldCamera = root.GetComponentInChildren<Camera>(true);
            puzzle.player.transform.position = area.exteriorSpawn.position;
            puzzle.chef.transform.position = area.exteriorChef.position;
            puzzle.chef.automaticKeyboardInteraction = false;
            if (puzzle.guest != null) UnityEngine.Object.DestroyImmediate(puzzle.guest.gameObject);
            puzzle.guest = null;
            area.interior.SetActive(false); area.interiorBounds.gameObject.SetActive(false);
            area.exterior.SetActive(true); area.exteriorBounds.gameObject.SetActive(true);
            area.virtualCamera.Follow = puzzle.player.transform;
            area.virtualCamera.m_Lens.OrthographicSize = area.exteriorCameraSize;
            area.virtualCamera.GetComponent<CinemachineConfiner2D>().m_BoundingShape2D = area.exteriorBounds;
            area.fieldCamera.transform.position = puzzle.player.transform.position + Vector3.back * 10;
            if (!Layout(environment).Skip(1).SequenceEqual(layout.Skip(1))) throw new Exception("室内布局发生意外变化，未保存。");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            File.WriteAllLines(Output + "/interior-after.txt", Layout(environment));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); PrefabUtility.UnloadPrefabContents(first); }
        if (firstHash != Hash("Assets/Project/Prefabs/Level1.prefab")) throw new Exception("第一关文件发生意外变动。");
        File.WriteAllText(Output + "/configuration.txt", "PASS 第一关保持原样；Level2 外场复制原 Ground/边界；室内原布局保持；共享玩家和庖长；移除宾客。\nLevel1 SHA256=" + firstHash);
        AssetDatabase.SaveAssets();
        Validate();
    }
    private static Transform Marker(Transform parent, string name, Vector3 position)
    {
        var marker = new GameObject(name).transform; marker.SetParent(parent, false); marker.position = position;
        return marker;
    }
    [MenuItem("尘封砖语/第二关/验证饭店内外场配置")]
    public static void Validate()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var area = root.GetComponent<CookingRestaurant>(); var puzzle = root.GetComponent<CookingPuzzle>();
            if (area == null || puzzle.restaurant != area || area.puzzle != puzzle || area.exterior == null || area.interior == null || area.entrance == null || area.exit == null) throw new Exception("区域引用缺失");
            if (root.GetComponentsInChildren<PlayerMovement>(true).Length != 1 || root.GetComponentsInChildren<NPCInteraction>(true).Length != 2 || puzzle.guest == null) throw new Exception("玩家/NPC数量错误");
            if (area.interior.activeSelf || area.interiorBounds.gameObject.activeSelf || !area.exterior.activeSelf || !area.exteriorBounds.gameObject.activeSelf) throw new Exception("默认外场显示状态错误");
            if (root.GetComponentsInChildren<CowEscape2D>(true).Length != 0 || root.GetComponentsInChildren<CowPen>(true).Length != 0) throw new Exception("存在农耕元素");
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) throw new Exception("Missing: " + t.name);
            File.WriteAllText(Output + "/validation.txt", "PASS 内外场引用；初始外场；单一玩家、庖长与室内宾客；无牛/围栏；无 Missing Script。");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
