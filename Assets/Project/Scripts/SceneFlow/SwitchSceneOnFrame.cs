using UnityEngine;
using UnityEngine.SceneManagement;

public class SwitchSceneOnFrame : MonoBehaviour
{
    public string targetSceneName;

    public void OnAnimationFrame()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("Target scene name is empty!");
            return;
        }

        if (gameObject.scene.name == "Start" && targetSceneName == "Index") IndexPrologue.Request();
        SceneManager.LoadScene(targetSceneName);
    }
}
