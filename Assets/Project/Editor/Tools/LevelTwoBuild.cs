using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using Cinemachine;

public static partial class LevelTwoWorkshop
{
    private const string DataRoot="Assets/Project/Data/Level2";
    private static Font Font => AssetDatabase.LoadAssetAtPath<Font>("Assets/Project/Fonts/SettingsYaHei.ttf");
    static partial void Dispatch(string action)
    {
        if (action=="build") Build();
        if (action=="validate") Validate();
        if (action=="test") BeginTest();
        if (action=="restaurant-test") BeginRestaurantTest();
    }
    static partial void BeginTest();
    static partial void BeginRestaurantTest();

    [MenuItem("尘封砖语/第二关/构建庖厨关卡")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("请退出运行模式后构建第二关");
        var configured = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (configured != null && configured.GetComponent<CookingRestaurant>() != null)
            throw new Exception("第二关已配置饭店内外场。原始重建会覆盖手工布景，请直接编辑 Level2 预制体；验证请使用“验证配置”。");
        Directory.CreateDirectory(Output); Directory.CreateDirectory(DataRoot); AssetDatabase.Refresh();
        // 明确从第一关复制，之后仅编辑副本。
        if (!File.Exists(PrefabPath) && !AssetDatabase.CopyAsset("Assets/Project/Prefabs/Level1.prefab",PrefabPath)) throw new Exception("复制 Level1 失败");
        MakeData(); ConfigureVideo();
        GameObject root=PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            root.name="Level2";
            FarmingPuzzle old=root.GetComponent<FarmingPuzzle>();
            PlayerMovement player=old!=null ? old.player : root.GetComponentInChildren<PlayerMovement>(true);
            PlayerTaskManager tasks=player.GetComponent<PlayerTaskManager>();
            var taskSettings=new SerializedObject(tasks); taskSettings.FindProperty("enablePersistence").boolValue=true; taskSettings.ApplyModifiedPropertiesWithoutUndo();
            NextLevelButton next=root.GetComponentInChildren<NextLevelButton>(true);
            NPCInteraction chef=root.transform.Find("NPC")?.GetComponent<NPCInteraction>();
            if (chef==null) chef=root.transform.Find("庖长").GetComponent<NPCInteraction>();
            NPCInteraction guest=root.transform.Find("切菜")?.GetComponent<NPCInteraction>();
            if (guest==null) guest=root.transform.Find("宴饮宾客").GetComponent<NPCInteraction>();
            if (old!=null) UnityEngine.Object.DestroyImmediate(old);
            foreach (Transform t in root.transform.Cast<Transform>().ToArray())
                if (new[]{"Ground","Building","FarmingGameplay","whitecow","whitecow (1)","whitecow (2)","blackcow","耙子","耕种","GameObject","CookingEnvironment"}.Contains(t.name)) UnityEngine.Object.DestroyImmediate(t.gameObject);
            foreach (TaskCompleteTrigger trigger in root.GetComponentsInChildren<TaskCompleteTrigger>(true)) UnityEngine.Object.DestroyImmediate(trigger);
            foreach (CowEscape2D cow in root.GetComponentsInChildren<CowEscape2D>(true)) UnityEngine.Object.DestroyImmediate(cow.gameObject);
            CookingPuzzle puzzle=root.GetComponent<CookingPuzzle>(); if (puzzle==null) puzzle=root.AddComponent<CookingPuzzle>();
            puzzle.player=player; puzzle.tasks=tasks; puzzle.font=Font;
            puzzle.storage=AssetDatabase.LoadAssetAtPath<TaskDataStorage>(DataRoot+"/CookingTasks.asset");
            RecordWindow catalog=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/RecordUI.prefab").GetComponent<RecordWindow>();
            if (!catalog.TryGetBrick("paochu",out Sprite restored,out string title) || restored==null) throw new Exception("画像砖录缺少庖厨图");
            puzzle.restoredBrick=restored; puzzle.nextLevel=next; puzzle.chef=chef; puzzle.guest=guest;
            puzzle.chefDialogues=Enumerable.Range(0,6).Select(i=>AssetDatabase.LoadAssetAtPath<DialogueData>(DataRoot+"/Chef"+i+".asset")).ToArray();
            chef.name="庖长"; chef.dialogueData=puzzle.chefDialogues[0]; chef.transform.position=new Vector3(-12,0,0);
            guest.name="宴饮宾客"; guest.dialogueData=AssetDatabase.LoadAssetAtPath<DialogueData>(DataRoot+"/Guest.asset"); guest.transform.position=new Vector3(14,0,0);
            // 宾客复用 NPC 外观，去掉第一关切菜动作与额外碰撞。
            Animator guestAnim=guest.GetComponent<Animator>(); if (guestAnim!=null) UnityEngine.Object.DestroyImmediate(guestAnim);
            foreach (Collider2D collider in guest.GetComponents<Collider2D>()) { collider.isTrigger=true; }
            player.transform.position=new Vector3(-12,-4,0);
            var nextData=new SerializedObject(next); nextData.FindProperty("nextLevelId").intValue=3;
            var rewards=nextData.FindProperty("rewardBrickIds"); rewards.arraySize=1; rewards.GetArrayElementAtIndex(0).stringValue="paochu"; nextData.ApplyModifiedPropertiesWithoutUndo();
            Transform env=new GameObject("CookingEnvironment").transform; env.SetParent(root.transform,false);
            Sprite brick=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Art/Environments/Backgrounds/pictorial-brick-clean-seamless.png");
            Sprite plain=ShapeSprite("Panel");
            WorldSprite(env,"宅院砖面底图",brick,Vector2.zero,new Vector2(48,20),-20,new Color(.79f,.65f,.48f));
            string[] zones={"庖厨作坊","传菜廊道","主宴厅堂"};
            for(int i=0;i<3;i++)
            {
                float x=-14+i*14;
                WorldSprite(env,zones[i]+"地面",brick,new Vector2(x,0),new Vector2(14,16),-15,new Color(.79f,.65f,.48f));
                WorldSprite(env,zones[i]+"墙面",plain,new Vector2(x,5.8f),new Vector2(13.7f,3.2f),-13,new Color(.39f,.24f,.18f));
                WorldText(env,zones[i],new Vector2(x,5.5f),.09f,new Color(.95f,.88f,.73f));
            }
            Sprite building=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Art/Environments/Buildings/饭店.jpg");
            WorldSprite(env,"宅院建筑_可替换",building,new Vector2(0,3),new Vector2(8,5),-11,Color.white);
            WorldSprite(env,"后厨储物案",plain,new Vector2(-17,2),new Vector2(4,1),-2,new Color(.35f,.22f,.16f));
            WorldSprite(env,"厅堂案几",plain,new Vector2(12,-2),new Vector2(6,1.5f),-2,new Color(.35f,.22f,.16f));
            WorldText(env,"置酒案     食案     炉台",new Vector2(12,-1.5f),.045f,new Color(.93f,.84f,.65f));
            string[] names={"陶耳杯","食盘","烤肉炉"}; string[] shapes={"Cup","Plate","Oven"};
            Vector2[] positions={new Vector2(-17,-2),new Vector2(-4,1),new Vector2(2,-3)};
            puzzle.utensils=new Transform[3];
            for(int i=0;i<3;i++) puzzle.utensils[i]=WorldSprite(env,names[i],ShapeSprite(shapes[i]),positions[i],new Vector2(1.7f,1.4f),2,Color.white).transform;
            puzzle.stove=WorldSprite(env,"灶台",ShapeSprite("Oven"),new Vector2(-8,3),new Vector2(3.2f,2.4f),0,Color.white).transform;
            puzzle.stoveDebris=new GameObject[3];
            for(int i=0;i<3;i++) puzzle.stoveDebris[i]=WorldSprite(env,"炉口杂物"+i,plain,new Vector2(-8.7f+i*.65f,2.6f),new Vector2(.7f,.5f),3,new Color(.21f,.17f,.13f));
            puzzle.banquet=WorldSprite(env,"宴席配对交互点",ShapeSprite("Plate"),new Vector2(12,1.5f),new Vector2(2,1),1,Color.white).transform;
            ConfigureBounds(root,player);
            DialogueManager manager=root.GetComponentInChildren<DialogueManager>(true); manager.taskInfoPanel.taskStorage=puzzle.storage;
            // 副本里的旧布局组件会覆盖 LevelBase 的任务文字定位。
            foreach(LayoutGroup layout in manager.taskInfoPanel.GetComponentsInChildren<LayoutGroup>(true)) UnityEngine.Object.DestroyImmediate(layout);
            foreach(ContentSizeFitter fitter in manager.taskInfoPanel.GetComponentsInChildren<ContentSizeFitter>(true)) UnityEngine.Object.DestroyImmediate(fitter);
            foreach(Text text in manager.taskInfoPanel.taskText) if(text!=null) text.transform.SetParent(manager.taskInfoPanel.transform,false);
            manager.dialogueUI.gameObject.SetActive(true); manager.dialogueUI.HidePanel(); manager.dialogueUI.HideChoices();
            foreach (Text text in manager.dialogueUI.GetComponentsInChildren<Text>(true)) if (text.font==null) text.font=Font;
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        ConfigureSelect(); CreateTestScene(); AssetDatabase.SaveAssets(); Validate();
        File.WriteAllText(Output+"/build.txt","完成：Level1 复制为 Level2，移除农耕元素，独立对话/任务/拼图/奖励，Select 第二砖视频入口。\n");
    }

    private static void MakeData()
    {
        TaskDataStorage storage=Asset<TaskDataStorage>(DataRoot+"/CookingTasks.asset"); storage.questId="chapter2_cooking";
        storage.tasks=new List<TaskDataStorage.TaskEntry> {
            new TaskDataStorage.TaskEntry { taskId=1,taskText="找齐三件散落的食器" },
            new TaskDataStorage.TaskEntry { taskId=2,taskText="与庖长交谈并清理灶台" },
            new TaskDataStorage.TaskEntry { taskId=3,taskText="在主宴厅堂配对食器" } };
        storage.finalTaskId=4; storage.finalTaskText="向庖长领取缺片，复原庖厨图"; EditorUtility.SetDirty(storage);
        DialogueData intro=Asset<DialogueData>(DataRoot+"/Chef0.asset"); intro.npcName="庖长"; intro.isCoreNPC=true; intro.endAfterScience=true;
        intro.greetingLines=new[]{"后厨接连遭遇变故，宴席的器具散落各处，灶台也被堵死。","宾客还在厅堂等候，实在令人焦急。"};
        intro.taskChoiceText="我来帮忙整理备餐"; intro.scienceChoiceText="魏晋的宴席有哪些习俗？";
        intro.taskLines=new[]{"多谢你的援手！陶耳杯、食盘、烤肉炉这些宴饮器具散落在后厨各个角落。","还请你逐一找寻收集，找齐后回来告诉我。"};
        intro.scienceLines=new[]{"我们这一带盛行分餐制，每位宾客都会单独配备案几与食器，不会多人共用一桌饮食。","河西地区农牧兼具，宴席之上既有面食、谷物，也有新鲜肉食，饮食种类十分丰富。","墓葬之中绘制庖厨与宴饮画面，是古人的美好期许，希望离世之后，依旧能享用美食、欢聚一堂。","了解完这些风俗，若是有空，还请帮我们一同完成备餐工作。"}; EditorUtility.SetDirty(intro);
        string[][] stages={
            new[]{"陶耳杯、食盘与烤肉炉还没有找齐。请到后厨作坊和传菜廊道看看，找到后再回来。"},
            new[]{"器具总算全部找回来了。长年的风化堆积了不少杂物，把灶台的炉口堵得严严实实。","麻烦你前去清理灶台，疏通炉道，我们才能继续制作宴席菜品。"},
            new[]{"灶台恢复通畅，现在可以正常烹饪了。","接下来，请你把所有食器送到宴饮厅堂，摆放到对应的案几之上。"},
            new[]{"宴席布置完毕，最后我们一同修补砖面上缺损的人物与器物纹样。","这块缺失的砖画碎片交给你，让这幅庖厨古砖恢复原貌吧。"},
            new[]{"器物与灶台已恢复，缺片也已交给你。请打开拼图，完成庖厨图的修复。"} };
        for(int i=0;i<5;i++) { DialogueData data=Asset<DialogueData>(DataRoot+"/Chef"+(i+1)+".asset"); data.npcName="庖长"; data.isCoreNPC=false; data.minorNPCLines=stages[i]; EditorUtility.SetDirty(data); }
        DialogueData guest=Asset<DialogueData>(DataRoot+"/Guest.asset"); guest.npcName="宴饮宾客"; guest.isCoreNPC=false;
        guest.minorNPCLines=new[]{"赴宴需遵守相应礼仪，座次排布、器具摆放都有严格的规矩，这也是魏晋社交文化的一部分。","从这些砖画中，后人也能直观看到千年前人们的饮食风貌与生活情趣。"}; EditorUtility.SetDirty(guest);
        // 与已有文字加载方式一致，后续修改 JSON 即可调整第二关拼图文案。
        var scratch=new GameObject("CookingText");
        try { CookingPuzzle p=scratch.AddComponent<CookingPuzzle>(); var method=typeof(CookingPuzzle).GetMethod("BuildDefaultText",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            File.WriteAllText("Assets/Project/Resources/LevelText/cooking.json",JsonUtility.ToJson(method.Invoke(p,null),true)); }
        finally { UnityEngine.Object.DestroyImmediate(scratch); }
        // 第二关人物志沿用原系统，只替换第二份档案内容。
        CompanionProfile profile=AssetDatabase.LoadAssetAtPath<CompanionProfile>("Assets/Project/Data/Companions/Companion2.asset");
        if (profile!=null)
        {
            profile.displayName="庖长";
            profile.identity="负责宅院备餐与食器整理的庖长，人物参考《庖厨图》中的庖厨劳作者。";
            profile.sharedExperience="寻砖人寻回食器、疏通炉道，并将器物送至厅堂配对归位。庖长交出缺失碎片，帮助寻砖人复原庖厨图。";
            profile.relatedDialogues=Enumerable.Range(0,6).Select(i=>AssetDatabase.LoadAssetAtPath<DialogueData>(DataRoot+"/Chef"+i+".asset")).ToArray();
            EditorUtility.SetDirty(profile);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
    }
    private static T Asset<T>(string path) where T:ScriptableObject { T value=AssetDatabase.LoadAssetAtPath<T>(path); if(value==null) { value=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value,path); } return value; }
    private static void ConfigureVideo()
    {
        VideoClipImporter importer=(VideoClipImporter)AssetImporter.GetAtPath(VideoPath);
        var settings=importer.defaultTargetSettings; settings.enableTranscoding=true; settings.codec=VideoCodec.H264;
        settings.bitrateMode=VideoBitrateMode.Medium;
        importer.defaultTargetSettings=settings;
        importer.SetTargetSettings("Android",settings); importer.SetTargetSettings("iPhone",settings);
        importer.importAudio=true; importer.SaveAndReimport();
    }
    private static void ConfigureBounds(GameObject root,PlayerMovement player)
    {
        Transform bounds=root.transform.Find("Confiner"); foreach(Transform child in bounds.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
        bounds.position=Vector3.zero; bounds.localScale=Vector3.one;
        PolygonCollider2D polygon=bounds.GetComponent<PolygonCollider2D>(); polygon.isTrigger=true; polygon.points=new[]{new Vector2(-22,-8),new Vector2(-22,8),new Vector2(22,8),new Vector2(22,-8)};
        for(int i=0;i<4;i++) { GameObject edge=new GameObject("宅院边界"+i,typeof(BoxCollider2D)); edge.transform.SetParent(bounds,false); edge.transform.localPosition=i<2 ? new Vector3(0,i==0 ? -7.5f:7.5f,0) : new Vector3(i==2?-21.5f:21.5f,0,0); edge.GetComponent<BoxCollider2D>().size=i<2 ? new Vector2(44,1):new Vector2(1,16); }
        foreach(CinemachineVirtualCamera camera in root.GetComponentsInChildren<CinemachineVirtualCamera>(true)) { camera.Follow=player.transform; camera.m_Lens.OrthographicSize=6;
            camera.transform.position=player.transform.position+Vector3.back*10; CinemachineConfiner2D confiner=camera.GetComponent<CinemachineConfiner2D>(); if(confiner!=null) confiner.m_BoundingShape2D=polygon; ConfinerFinder finder=camera.GetComponent<ConfinerFinder>(); if(finder!=null) UnityEngine.Object.DestroyImmediate(finder); }
    }
    private static void ConfigureSelect()
    {
        const string path="Assets/Project/Scenes/Select.unity";
        Scene scene=SceneManager.GetSceneByPath(path); bool opened=!scene.isLoaded;
        if (!opened && scene.isDirty) throw new Exception("Select 有未保存编辑，请保存后重新构建第二关，防止覆盖。");
        if (opened) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            Transform entrance=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="刨厨图动画" || t.name=="第二关视频入场");
            HideAndInstantiate legacy=entrance.GetComponent<HideAndInstantiate>();
            GameObject selection=legacy!=null ? legacy.uiToHide : entrance.GetComponent<LevelVideoEntrance>().selectionUI;
            if(legacy!=null) UnityEngine.Object.DestroyImmediate(legacy);
            Animator animator=entrance.GetComponent<Animator>(); if(animator!=null) UnityEngine.Object.DestroyImmediate(animator);
            Image image=entrance.GetComponent<Image>(); if(image!=null) image.enabled=false;
            LevelVideoEntrance video=entrance.GetComponent<LevelVideoEntrance>(); if(video==null) video=entrance.gameObject.AddComponent<LevelVideoEntrance>();
            entrance.name="第二关视频入场"; video.clip=AssetDatabase.LoadAssetAtPath<VideoClip>(VideoPath); video.levelPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); video.selectionUI=selection; video.font=Font;
            entrance.gameObject.SetActive(false);
            var clicks=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UIHoverShow>(true)).Where(c=>c.targetUI==entrance.gameObject).ToArray();
            if(clicks.Length==0) throw new Exception("第二砖点击入口没有连到视频对象");
            foreach(UIHoverShow click in clicks) { click.requiredLevelId=2; EditorUtility.SetDirty(click); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally { if(opened) EditorSceneManager.CloseScene(scene,true); }
    }
    private static void CreateTestScene()
    {
        const string path="Assets/Project/Scenes/Level2.unity";
        Scene previous=SceneManager.GetActiveScene(); Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try { SceneManager.SetActiveScene(scene); PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),scene); EditorSceneManager.SaveScene(scene,path); }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene,true); }
        var scenes=EditorBuildSettings.scenes.ToList(); if(!scenes.Any(s=>s.path==path)) { scenes.Add(new EditorBuildSettingsScene(path,true)); EditorBuildSettings.scenes=scenes.ToArray(); }
    }
    private static GameObject WorldSprite(Transform parent,string name,Sprite sprite,Vector2 position,Vector2 size,int order,Color color)
    {
        if(sprite==null) throw new Exception("缺少素材："+name);
        GameObject go=new GameObject(name,typeof(SpriteRenderer)); go.transform.SetParent(parent,false); go.transform.position=position;
        SpriteRenderer r=go.GetComponent<SpriteRenderer>(); r.sprite=sprite; r.sortingOrder=order; r.color=color;
        go.transform.localScale=new Vector3(size.x/sprite.bounds.size.x,size.y/sprite.bounds.size.y,1);
        return go;
    }
    private static void WorldText(Transform parent,string name,Vector2 position,float scale,Color color)
    {
        GameObject go=new GameObject(name,typeof(TextMesh)); go.transform.SetParent(parent,false); go.transform.position=position; go.transform.localScale=Vector3.one*scale;
        TextMesh text=go.GetComponent<TextMesh>(); text.text=name; text.font=Font; text.fontSize=80; text.anchor=TextAnchor.MiddleCenter; text.color=color;
        MeshRenderer renderer=go.GetComponent<MeshRenderer>(); renderer.sharedMaterial=Font.material; renderer.sortingOrder=5;
    }
    private static Sprite ShapeSprite(string shape)
    {
        string path=DataRoot+"/"+shape+".png";
        if(!File.Exists(path))
        {
            Texture2D texture=new Texture2D(192,192,TextureFormat.RGBA32,false);
            Color ink=new Color(.22f,.15f,.11f), clay=new Color(.66f,.39f,.24f), light=new Color(.86f,.71f,.48f);
            for(int y=0;y<192;y++) for(int x=0;x<192;x++)
            {
                Color c=Color.clear; bool Ellipse(float cx,float cy,float rx,float ry)=>Mathf.Pow((x-cx)/rx,2)+Mathf.Pow((y-cy)/ry,2)<=1;
                if(shape=="Panel") c=Color.white;
                if(shape=="Plate") { if(Ellipse(96,96,87,44)) c=ink; if(Ellipse(96,96,80,37)) c=light; if(Ellipse(96,96,60,23)) c=clay; }
                if(shape=="Cup") { if(Ellipse(22,107,20,28)||Ellipse(170,107,20,28)) c=ink; if(Ellipse(22,107,13,19)||Ellipse(170,107,13,19)) c=light;
                    if(Ellipse(96,100,67,48)) c=ink; if(Ellipse(96,105,60,41)) c=clay; if(Ellipse(96,123,62,25)) c=ink; if(Ellipse(96,126,54,18)) c=light; }
                if(shape=="Oven") { if(x>24&&x<168&&y>30&&y<146) c=ink; if(x>32&&x<160&&y>38&&y<138)c=clay; if(x>58&&x<134&&y>38&&y<90)c=ink;
                    if(Ellipse(96,148,72,24))c=ink; if(Ellipse(96,151,63,16))c=light; }
                texture.SetPixel(x,y,c);
            }
            texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
            TextureImporter importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite; importer.spritePixelsPerUnit=100; importer.alphaIsTransparency=true; importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    [MenuItem("尘封砖语/第二关/验证配置")]
    public static void Validate()
    {
        GameObject root=PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            CookingPuzzle p=root.GetComponent<CookingPuzzle>();
            if(p==null || p.chef==null || (p.guest==null && p.restaurant==null) || p.restoredBrick==null || p.storage.questId!="chapter2_cooking" || p.chefDialogues.Length!=6) throw new Exception("第二关配置缺失");
            if(root.GetComponentsInChildren<CowPen>(true).Length>0 || root.GetComponentsInChildren<CowEscape2D>(true).Length>0 || root.GetComponent<FarmingPuzzle>()!=null) throw new Exception("残留第一关赶牛组件");
            if(root.GetComponentsInChildren<Transform>(true).Any(t=>t.name.ToLower().Contains("cow") || t.name.Contains("Fence") || t.name.Contains("耕具") || t.name=="耙子")) throw new Exception("残留牛、围栏或耕具节点");
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true)) if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0) throw new Exception("Missing script: "+t.name);
            if(p.nextLevel.RewardBrickIds.Length!=1 || p.nextLevel.RewardBrickIds[0]!="paochu") throw new Exception("奖励 ID 错误");
            File.WriteAllText(Output+"/validation.txt","PASS: Level2 references; independent quest; six chef dialogue stages; paochu puzzle/reward; no cow/fence/farming components; no missing scripts.\n");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
