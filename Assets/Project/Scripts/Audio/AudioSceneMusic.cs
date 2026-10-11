using UnityEngine;

public class AudioSceneMusic : MonoBehaviour
{
    [SerializeField] private string musicId;

    private void Start()
    {
        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayMusic(musicId);
    }
}
