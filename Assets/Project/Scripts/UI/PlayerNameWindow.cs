using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerNameWindow : MonoBehaviour
{
    [SerializeField] private Button playerButton;
    [SerializeField] private GameObject nameWindow;
    [SerializeField] private InputField nameInput;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Text playerNameText;
    [SerializeField] private Text errorText;
    [Min(0f)] [SerializeField] private float errorDuration = 2f;

    private Coroutine hideErrorRoutine;

    private void Awake()
    {
        if (playerButton != null) playerButton.onClick.AddListener(Open);
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (nameWindow != null) nameWindow.SetActive(false);
        if (errorText != null) errorText.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (playerNameText != null && DataSystem.instance != null)
            playerNameText.text = DataSystem.instance.GetPlayerName();
    }

    private void OnDestroy()
    {
        if (playerButton != null) playerButton.onClick.RemoveListener(Open);
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
    }

    private void Open()
    {
        if (nameWindow == null || nameInput == null) return;

        HideError();
        nameInput.text = DataSystem.instance != null && DataSystem.instance.HasSavedPlayerName()
            ? DataSystem.instance.GetPlayerName() : "";
        nameWindow.SetActive(true);
        nameWindow.transform.SetAsLastSibling();
        nameInput.Select();
        nameInput.ActivateInputField();
    }

    private void Confirm()
    {
        if (nameInput == null) return;

        string newName = nameInput.text.Trim();
        if (newName.Length == 0)
        {
            ShowError("请输入名字");
            return;
        }

        foreach (char character in newName)
        {
            if (char.IsLetterOrDigit(character)) continue;
            ShowError("名字不能包含特殊字符");
            return;
        }

        if (DataSystem.instance == null)
        {
            ShowError("保存失败，请重试");
            return;
        }

        DataSystem.instance.SavePlayerName(newName);
        if (playerNameText != null) playerNameText.text = newName;
        HideError();
        if (nameWindow != null) nameWindow.SetActive(false);
    }

    private void ShowError(string message)
    {
        if (errorText == null) return;
        HideError();
        errorText.text = message;
        errorText.gameObject.SetActive(true);
        hideErrorRoutine = StartCoroutine(HideErrorAfterDelay());
    }

    private IEnumerator HideErrorAfterDelay()
    {
        yield return new WaitForSecondsRealtime(errorDuration);
        hideErrorRoutine = null;
        if (errorText != null) errorText.gameObject.SetActive(false);
    }

    private void HideError()
    {
        if (hideErrorRoutine != null)
        {
            StopCoroutine(hideErrorRoutine);
            hideErrorRoutine = null;
        }
        if (errorText != null) errorText.gameObject.SetActive(false);
    }
}
