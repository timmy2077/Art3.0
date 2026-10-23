using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>本地 VideoClip 入场：准备与首帧就绪后显示；失败可重试或返回，不使用桌面绝对路径。</summary>
public sealed class LevelVideoEntrance : MonoBehaviour
{
    public VideoClip clip;
    public GameObject levelPrefab;
    public GameObject selectionUI;
    public Font font;
    [Min(3)] public float prepareTimeout = 20;
    [Min(3)] public float stallTimeout = 10;
    public bool IsPlaying { get; private set; }
    public bool FirstFrameReceived { get; private set; }
    public bool IsPictureVisible { get; private set; }
    public bool Completed { get; private set; }
    public string LastError { get; private set; }
    private VideoPlayer video;
    private RenderTexture texture;
    private RawImage screen;
    private Text message;
    private GameObject actions, overlay;
    private CanvasGroup menu;
    private bool menuInteractable, menuRaycasts;
    private float menuAlpha;
    private Coroutine playback;
    private bool ending, appPaused;
    private long lastFrame;
    private double lastProgress;
    private bool finished;

    private void OnEnable()
    {
        ending=false; Completed=false;
        menu=selectionUI != null ? selectionUI.GetComponentInParent<CanvasGroup>(true) : null;
        if (menu != null) { menuAlpha=menu.alpha; menuInteractable=menu.interactable; menuRaycasts=menu.blocksRaycasts; menu.interactable=false; menu.blocksRaycasts=false; }
        CreateOverlay(); Retry();
    }
    private void CreateOverlay()
    {
        overlay=new GameObject(name+"_视频画面",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(Image));
        // 必须是独立根 Canvas；嵌在选关 Canvas 下会继承缩放和 100×100 的默认尺寸。
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(overlay,gameObject.scene);
        Canvas canvas=overlay.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.overrideSorting=true; canvas.sortingOrder=32000;
        CanvasScaler scaler=overlay.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
        // 准备期间保留背后的选关砖图；透明 Image 仍然拦截输入。
        overlay.GetComponent<Image>().color=Color.clear;
        GameObject picture=new GameObject("视频画面",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter)); picture.transform.SetParent(overlay.transform,false);
        screen=picture.GetComponent<RawImage>(); screen.color=Color.clear; screen.raycastTarget=false;
        AspectRatioFitter fitter=picture.GetComponent<AspectRatioFitter>(); fitter.aspectMode=AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio=clip != null && clip.height>0 ? (float)clip.width/clip.height : 16f/9;
        message=Label(overlay.transform,"",new Vector2(0,-385),new Vector2(1550,150),30);
        actions=new GameObject("播放失败操作",typeof(RectTransform)); actions.transform.SetParent(overlay.transform,false);
        Button(actions.transform,"重试播放",-240,Retry); Button(actions.transform,"返回选关",240,Cancel);
        actions.SetActive(false);
    }
    private void CreateDecoder()
    {
        video=gameObject.AddComponent<VideoPlayer>(); video.playOnAwake=false; video.isLooping=false; video.waitForFirstFrame=false;
        video.skipOnDrop=true; video.renderMode=VideoRenderMode.RenderTexture; video.source=VideoSource.VideoClip;
        video.timeUpdateMode=VideoTimeUpdateMode.DSPTime;
        video.audioOutputMode=VideoAudioOutputMode.Direct; video.sendFrameReadyEvents=true;
        video.errorReceived+=Error; video.frameReady+=Frame; video.loopPointReached+=End;
    }
    // frameReady 回调时纹理可能还未完成提交，不在回调中立即显示。
    private void Frame(VideoPlayer sender,long frame) { FirstFrameReceived=true; }
    private void End(VideoPlayer sender) { finished=true; }
    private void Error(VideoPlayer sender,string error) { LastError=error; }
    public void Retry()
    {
        if (ending) return;
        if (playback != null) StopCoroutine(playback);
        if (video!=null) { video.Stop(); video.errorReceived-=Error; video.frameReady-=Frame; video.loopPointReached-=End; Destroy(video); }
        CleanupTexture();
        CreateDecoder(); LastError=null; finished=false; FirstFrameReceived=false; IsPictureVisible=false; IsPlaying=true; actions.SetActive(false); screen.color=Color.clear;
        overlay.GetComponent<Image>().color=Color.clear;
        message.text="";
        playback=StartCoroutine(PrepareAndPlay());
    }
    private IEnumerator PrepareAndPlay()
    {
        yield return null; // 等上一轮解码器释放，再启动新一轮原生解码。
        if (clip == null || levelPrefab == null) { Fail("视频或关卡预制体未配置。"); yield break; }
        video.clip=clip;
        int height=Mathf.Clamp((int)clip.height,2,720); int width=Mathf.Max(2,Mathf.RoundToInt(height*(float)clip.width/Mathf.Max(1,clip.height)));
        texture=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32); texture.Create();
        video.targetTexture=texture; screen.texture=texture;
        if (clip.audioTrackCount>0) { video.controlledAudioTrackCount=1; video.EnableAudioTrack(0,true); video.SetDirectAudioVolume(0,AudioListener.volume); }
        video.sendFrameReadyEvents=true; video.Prepare();
        double deadline=Time.realtimeSinceStartupAsDouble+prepareTimeout;
        while (!video.isPrepared && LastError==null && Time.realtimeSinceStartupAsDouble<deadline) yield return null;
        if (!video.isPrepared || LastError!=null) { Fail(LastError ?? "视频准备超时，请重试。"); yield break; }
        yield return null; // 部分平台在 Prepare 完成的同一帧仍会暂停于首帧。
        video.Play(); lastFrame=-1; lastProgress=Time.realtimeSinceStartupAsDouble;
        double nextResume=lastProgress+.25;
        int resumeAttempts=0;
        while (!finished && LastError==null)
        {
            if (FirstFrameReceived && !IsPictureVisible)
            {
                // 至少经过一轮渲染后再用视频覆盖原图，避免显示未写入的黑纹理。
                yield return null;
                if (LastError!=null) break;
                overlay.GetComponent<Image>().color=Color.black;
                screen.color=Color.white;
                message.text="";
                IsPictureVisible=true;
            }
#if UNITY_EDITOR
            // 编辑器失去焦点时原生视频可能自行暂停；此时不误报设备解码停滞。
            if (!Application.isFocused && video.isPaused) lastProgress=Time.realtimeSinceStartupAsDouble;
#endif
            if (!appPaused && video.isPaused && Time.realtimeSinceStartupAsDouble>nextResume && resumeAttempts<3)
            { video.Play(); resumeAttempts++; nextResume=Time.realtimeSinceStartupAsDouble+.5; }
            if (video.frame!=lastFrame) { lastFrame=video.frame; lastProgress=Time.realtimeSinceStartupAsDouble; }
            if (FirstFrameReceived && video.frameCount>1 && video.frame>=(long)video.frameCount-1) { finished=true; break; }
            if (Time.realtimeSinceStartupAsDouble-lastProgress>stallTimeout) { LastError=$"视频播放停滞，请重试。frame={video.frame}/{video.frameCount}, time={video.time}, playing={video.isPlaying}, paused={video.isPaused}, enabled={video.isActiveAndEnabled}, prepared={video.isPrepared}, clock={video.timeUpdateMode}"; break; }
            yield return null;
        }
        if (LastError!=null || !FirstFrameReceived) { Fail(LastError ?? "视频未能输出画面，请重试。"); yield break; }
        // 实例化成功后才收起选关，防止视频失败留下不可操作界面。
        ending=true; Completed=true; IsPlaying=false;
        GameObject instance=Instantiate(levelPrefab,Vector3.zero,Quaternion.identity);
        UiClickSoundInstaller.ScanGameObject(instance);
        if (menu!=null) { menu.alpha=0; menu.interactable=false; menu.blocksRaycasts=false; }
        else if (selectionUI!=null) selectionUI.SetActive(false);
        gameObject.SetActive(false);
    }
    private void Fail(string error)
    {
        LastError=error; IsPlaying=false; IsPictureVisible=false; video.Stop(); screen.color=Color.black;
        overlay.GetComponent<Image>().color=Color.black;
        message.text="入场视频暂时无法播放，请重试或返回选关。";
        actions.SetActive(true); Debug.LogWarning("[LevelVideoEntrance] "+error,this);
    }
    public void Cancel() { ending=false; gameObject.SetActive(false); }
    private void OnApplicationPause(bool value)
    {
        if (value==appPaused) return;
        appPaused=value;
        if (!isActiveAndEnabled || ending || !IsPlaying) return;
        if (value) { if (playback!=null) StopCoroutine(playback); video.Pause(); }
        else Retry();
    }
    private void CleanupTexture()
    {
        if (video!=null) video.targetTexture=null;
        if (screen!=null) screen.texture=null;
        if (texture!=null) { texture.Release(); Destroy(texture); texture=null; }
    }
    private void OnDisable()
    {
        if (playback!=null) StopCoroutine(playback); playback=null; IsPlaying=false; IsPictureVisible=false;
        if (video!=null) { video.Stop(); video.errorReceived-=Error; video.frameReady-=Frame; video.loopPointReached-=End; Destroy(video); }
        CleanupTexture();
        if (overlay!=null) Destroy(overlay);
        if (!ending && menu!=null) { menu.alpha=menuAlpha; menu.interactable=menuInteractable; menu.blocksRaycasts=menuRaycasts; }
    }
    private Text Label(Transform parent,string text,Vector2 position,Vector2 size,int fontSize)
    {
        GameObject go=new GameObject("Text",typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
        RectTransform rect=go.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=Vector2.one*.5f; rect.anchoredPosition=position; rect.sizeDelta=size;
        Text label=go.GetComponent<Text>(); label.font=font; label.text=text; label.fontSize=fontSize; label.color=new Color(.93f,.87f,.74f); label.alignment=TextAnchor.MiddleCenter; label.raycastTarget=false; return label;
    }
    private void Button(Transform parent,string text,float x,UnityEngine.Events.UnityAction callback)
    {
        GameObject go=new GameObject(text,typeof(RectTransform),typeof(Image),typeof(Button)); go.transform.SetParent(parent,false);
        RectTransform rect=go.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=Vector2.one*.5f; rect.anchoredPosition=new Vector2(x,0); rect.sizeDelta=new Vector2(380,90);
        go.GetComponent<Image>().color=new Color(.3f,.24f,.18f);
        Label(go.transform,text,Vector2.zero,new Vector2(370,85),30);
        go.GetComponent<Button>().onClick.AddListener(callback);
    }
}



