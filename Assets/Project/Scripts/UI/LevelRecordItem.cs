using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class LevelRecordItem : MonoBehaviour
{
    [SerializeField] private Image brickImage;
    [SerializeField] private Text levelNameText;

    public void SetOpenAction(UnityAction action)
    {
        Button button = GetComponent<Button>();
        if (button == null) button = gameObject.AddComponent<Button>();
        Image background = GetComponent<Image>();
        if (background != null)
        {
            background.raycastTarget = true;
            button.targetGraphic = background;
        }
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    public void SetData(string brickId, string brickName, Sprite brickSprite)
    {
        gameObject.name = "BrickRecord_" + brickId;
        if (brickImage != null)
            brickImage.sprite = brickSprite;
        if (levelNameText != null)
            levelNameText.text = "《" + brickName + "》";
    }
}
