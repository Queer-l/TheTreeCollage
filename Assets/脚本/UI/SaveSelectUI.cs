using TMPro;
using UnityEngine;

/// <summary>
/// 固定 3 槽位存档面板。
/// 存档自动按 1 -> 2 -> 3 -> 1 循环覆盖；读档按钮按编号读取对应槽位。
/// </summary>
public class SaveSelectUI : MonoBehaviour
{
    [Header("显示控制")]
    public UIControl panelUI;

    [Header("槽位文本")]
    public TextMeshProUGUI slot1Text;
    public TextMeshProUGUI slot2Text;
    public TextMeshProUGUI slot3Text;
    public TextMeshProUGUI nextSaveSlotText;

    [Header("管理器")]
    public PlayerManager playerManager;
    public MainMenuManager mainMenuManager;

    private void Awake()
    {
        if (panelUI == null)
        {
            panelUI = GetComponent<UIControl>();
        }

        if (playerManager == null)
        {
            playerManager = FindObjectOfType<PlayerManager>();
        }

        if (mainMenuManager == null)
        {
            mainMenuManager = FindObjectOfType<MainMenuManager>();
        }
    }

    public void Open()
    {
        RefreshSlotTexts();

        if (panelUI != null)
        {
            panelUI.Open();
            panelUI.BringToFront();
        }
    }

    public void Close()
    {
        if (panelUI != null)
        {
            panelUI.Close();
        }
    }

    /// <summary>
    /// 按钮入口：自动保存到下一个槽位，1/2/3 循环覆盖。
    /// </summary>
    public void SaveNextSlot()
    {
        if (playerManager == null)
        {
            Debug.LogWarning("保存失败，未绑定 PlayerManager。", this);
            return;
        }

        playerManager.SaveGame();
        RefreshSlotTexts();
    }

    public void LoadSlot1()
    {
        LoadSlot(1);
    }

    public void LoadSlot2()
    {
        LoadSlot(2);
    }

    public void LoadSlot3()
    {
        LoadSlot(3);
    }

    public void DeleteSlot1()
    {
        DeleteSlot(1);
    }

    public void DeleteSlot2()
    {
        DeleteSlot(2);
    }

    public void DeleteSlot3()
    {
        DeleteSlot(3);
    }

    public void RefreshSlotTexts()
    {
        SetSlotText(slot1Text, 1);
        SetSlotText(slot2Text, 2);
        SetSlotText(slot3Text, 3);

        if (nextSaveSlotText != null)
        {
            nextSaveSlotText.text = $"下次保存槽位: {PlayerData.GetNextSaveSlot()}";
        }
    }

    private void LoadSlot(int slot)
    {
        if (!PlayerData.HasSaveSlotFile(slot))
        {
            Debug.LogWarning($"读档失败，槽位 {slot} 没有存档。", this);
            return;
        }

        if (playerManager != null)
        {
            playerManager.LoadSaveSlot(slot);
            return;
        }

        if (mainMenuManager != null)
        {
            mainMenuManager.LoadSaveSlot(slot);
            return;
        }

        Debug.LogWarning("读档失败，未绑定 PlayerManager 或 MainMenuManager。", this);
    }

    private void DeleteSlot(int slot)
    {
        if (playerManager != null)
        {
            switch (slot)
            {
                case 1:
                    playerManager.DeleteSaveSlot1();
                    break;
                case 2:
                    playerManager.DeleteSaveSlot2();
                    break;
                case 3:
                    playerManager.DeleteSaveSlot3();
                    break;
            }
        }
        else if (PlayerData.HasSaveSlotFile(slot))
        {
            string savePath = PlayerData.GetSaveFilePath(PlayerData.GetSaveNameForSlot(slot));
            System.IO.File.Delete(savePath);
            Debug.Log($"删档成功: {savePath}", this);
        }

        RefreshSlotTexts();
    }

    private static void SetSlotText(TextMeshProUGUI label, int slot)
    {
        if (label == null)
        {
            return;
        }

        label.text = PlayerData.HasSaveSlotFile(slot) ? $"存档 {slot}" : $"存档 {slot}（空）";
    }
}
