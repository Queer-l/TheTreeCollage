using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    private const string GameSceneName = "GameScene";

    [Header("关于我们 UI")]
    [Tooltip("关于我们弹窗的控制脚本。")]
    public UIControl aboutUsUI;

    [Header("存档")]
    [Tooltip("主菜单读档按钮使用的存档名。")]
    public string saveName = "save";

    public void StartGame()
    {
        PlayerData.loadSaveOnNextAwake = false;
        PlayerData.resetDataOnNextAwake = true;

        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.ResetRuntimeData();
            PlayerData.resetDataOnNextAwake = false;
        }

        SceneManager.LoadScene(GameSceneName);
    }

    public void LoadGame()
    {
        LoadSaveSlot1();
    }

    public void LoadSaveSlot1()
    {
        LoadSaveSlot(1);
    }

    public void LoadSaveSlot2()
    {
        LoadSaveSlot(2);
    }

    public void LoadSaveSlot3()
    {
        LoadSaveSlot(3);
    }

    public void LoadSaveSlot(int slot)
    {
        LoadGame(PlayerData.GetSaveNameForSlot(slot));
    }

    public void LoadGame(string targetSaveName)
    {
        saveName = targetSaveName;
        if (!PlayerData.HasSaveFile(saveName))
        {
            Debug.LogWarning("读档失败，没有找到存档文件。", this);
            return;
        }

        PlayerData.loadSaveOnNextAwake = true;
        PlayerData.pendingLoadSaveName = saveName;
        SceneManager.LoadScene(GameSceneName);
    }

    public void SetSaveName(string targetSaveName)
    {
        if (!string.IsNullOrWhiteSpace(targetSaveName))
        {
            saveName = targetSaveName;
        }
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void AboutUs()
    {
        if (aboutUsUI == null)
        {
            Debug.LogWarning("未绑定关于我们 UI，无法打开弹窗。", this);
            return;
        }

        aboutUsUI.Open();
        aboutUsUI.BringToFront();
    }
}
