using UnityEngine;
using UnityEngine.Video;

public class DestroyOnVideoEnd : MonoBehaviour
{
    private VideoPlayer videoPlayer;

    void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoEnd;
        }
        else
        {
            Debug.LogError("DestroyOnVideoEnd requires a VideoPlayer component on the same GameObject!");
        }
    }

    private void OnVideoEnd(VideoPlayer vp)
    {
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd;
        }
    }
}