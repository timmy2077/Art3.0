using UnityEngine;

public class AudioCueTrigger : MonoBehaviour
{
    [SerializeField] private string soundId;

    // Can be called by a Button OnClick or an Animator event.
    public void Play()
    {
        if (GameAudio.Instance != null)
            GameAudio.Instance.PlaySound(soundId);
    }

    public void Play(string id)
    {
        if (GameAudio.Instance != null)
            GameAudio.Instance.PlaySound(id);
    }
}
