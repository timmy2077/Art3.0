using UnityEngine;
using UnityEngine.UI;

public class LevelRecordItem : MonoBehaviour
{
    [SerializeField] private Image brickImage;
    [SerializeField] private Text levelNameText;

    public void SetData(string brickId, string brickName, Sprite brickSprite)
    {
        gameObject.name = "BrickRecord_" + brickId;
        if (brickImage != null)
            brickImage.sprite = brickSprite;
        if (levelNameText != null)
            levelNameText.text = "《" + brickName + "》";
    }
}
