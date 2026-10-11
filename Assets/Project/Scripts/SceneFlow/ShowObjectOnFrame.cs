using UnityEngine;

public class ShowObjectOnFrame : MonoBehaviour
{
    public GameObject objectToShow;

    public void TriggerShowObject()
    {
        if (objectToShow != null)
        {
            objectToShow.SetActive(true);
        }
        Destroy(gameObject);
    }
}