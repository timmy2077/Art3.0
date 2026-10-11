using UnityEngine;
using UnityEngine.UI;

public class UIPageSwitch : MonoBehaviour
{
    [Header("按顺序拖入5个界面面板")]
    public GameObject[] uiPanels;
    [Header("按钮")]
    public Button btnLast;
    public Button btnNext;

    private int currentIndex = 0;

    void Start()
    {
        btnLast.onClick.AddListener(LastPage);
        btnNext.onClick.AddListener(NextPage);
        RefreshPanel();
    }

    void NextPage()
    {
        currentIndex++;
        if (currentIndex >= uiPanels.Length)
        {
            currentIndex = 0;
        }
        RefreshPanel();
    }

    void LastPage()
    {
        currentIndex--;
        if (currentIndex < 0)
        {
            currentIndex = uiPanels.Length - 1;
        }
        RefreshPanel();
    }

    public void SwitchToPage(int index)
    {
        if (index >= 0 && index < uiPanels.Length)
        {
            currentIndex = index;
            RefreshPanel();
        }
    }

    void RefreshPanel()
    {
        for (int i = 0; i < uiPanels.Length; i++)
        {
            uiPanels[i].SetActive(i == currentIndex);
        }
    }
}