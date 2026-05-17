using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 玩家操作和玩家 UI 的入口。
///
/// 这个脚本负责“玩家能不能做某件事”：消耗体力、获得线索、显示失败提示、刷新顶部 UI。
/// 一天流程本身放在 DayManager 中；这里保留 UpdateMoney 是为了兼容场景里尚未迁移的按钮事件。
/// </summary>
public class PlayerManager : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string LittleGameSceneName = "LittleGame";
    private const int EasyLittleGameConfigIndex = 0;
    private const int HardLittleGameConfigIndex = 1;

    [Header("玩家 UI")]
    [Tooltip("显示当前体力，例如：体力值: 2/3。")]
    public TextMeshProUGUI stepsText;

    [Tooltip("显示当前剧情天数。")]
    public TextMeshProUGUI dateText;

    [Tooltip("显示已获得线索数量。旧场景中原本可能绑定的是金钱文本，现在改为线索数量。")]
    public TextMeshProUGUI clueCountText;

    [Header("提示 UI")]
    [Tooltip("体力不足或操作失败时显示的提示弹窗。保留旧字段名以兼容场景绑定。")]
    public UIControl StepstipUI;

    [Header("小游戏")]
    [Tooltip("点击开始探索并成功消耗体力后打开的小游戏难度选择 Canvas。")]
    public UIControl littleGameSelectUI;

    [Header("流程管理")]
    [Tooltip("一天流程管理器。若未手动绑定，运行时会自动查找或添加。")]
    public DayManager dayManager;

    [Tooltip("线索管理器。读档后会尝试刷新线索数据。可不绑定，运行时自动查找。")]
    public ClueManager clueManager;

    [Header("存档")]
    [Tooltip("按钮保存/读档/删档使用的存档名。")]
    public string saveName = "save";

    /// <summary>
    /// 对提示 UI 的语义化访问。底层仍使用旧字段，避免 Unity 序列化绑定丢失。
    /// </summary>
    public UIControl operationTipUI => StepstipUI;

    private PlayerData Data => PlayerData.Instance;

    private void Awake()
    {
        // 优先复用场景中已经挂好的 DayManager。
        if (dayManager == null)
        {
            dayManager = FindObjectOfType<DayManager>();
        }

        // 如果场景还没手动挂 DayManager，就在当前对象上补一个运行时组件，保证旧按钮仍可用。
        if (dayManager == null)
        {
            dayManager = gameObject.AddComponent<DayManager>();
        }

        if (clueManager == null)
        {
            clueManager = FindObjectOfType<ClueManager>();
        }
    }

    private void Start()
    {
        UpdatePlayerUI();
    }

    /// <summary>
    /// 将 PlayerData 中的体力、天数和线索数量同步到界面文本。
    /// </summary>
    public void UpdatePlayerUI()
    {
        if (Data == null)
        {
            Debug.LogWarning("场景中缺少 PlayerData，无法刷新玩家 UI。", this);
            return;
        }

        SetText(stepsText, $"体力值: {Data.steps}/{Data.maxSteps}");
        SetText(dateText, $"日期: {Data.date}天");
        SetText(clueCountText, $"线索: {Data.unlockedClues.Count}");
    }

    /// <summary>
    /// 尝试执行一次需要体力的行动。
    /// </summary>
    /// <returns>成功消耗体力返回 true；体力不足或缺少数据源返回 false。</returns>
    public bool TryConsumeAction()
    {
        if (Data == null || !Data.TryConsumeStep())
        {
            ShowTip();
            UpdatePlayerUI();
            return false;
        }

        UpdatePlayerUI();
        return true;
    }

    /// <summary>
    /// 旧按钮兼容入口：单纯消耗一次体力。
    /// </summary>
    public void MovePlayer()
    {
        TryConsumeAction();
    }

    /// <summary>
    /// 旧按钮兼容入口：进入下一天。实际流程交给 DayManager。
    /// </summary>
    public void NextDay()
    {
        if (dayManager != null)
        {
            dayManager.GoToNextDay();
            return;
        }

        if (!TryConsumeAction() || Data == null)
        {
            return;
        }

        Data.AdvanceDay();
        UpdatePlayerUI();
    }

    /// <summary>
    /// 旧“开始经营/结算金钱”按钮兼容入口。
    /// 当前玩法中它等价于一次调查行动。
    /// </summary>
    public void UpdateMoney()
    {
        Investigate();
    }

    /// <summary>
    /// 执行一次调查行动。调查流程交给 DayManager 统一记录。
    /// </summary>
    public void Investigate()
    {
        if (dayManager != null)
        {
            dayManager.ProcessInvestigation();
            return;
        }

        TryConsumeAction();
    }

    /// <summary>
    /// 尝试获得一个线索。首次获得会消耗体力，已经获得过的线索直接返回成功。
    /// </summary>
    public bool TryCollectClue(ClueSO clue)
    {
        if (clue == null || Data == null)
        {
            return false;
        }

        if (Data.HasClue(clue))
        {
            return true;
        }

        if (!TryConsumeAction())
        {
            return false;
        }

        Data.CollectClue(clue);
        UpdatePlayerUI();
        return true;
    }

    /// <summary>
    /// 消耗 1 点体力后打开小游戏难度选择界面。体力不足时显示操作失败提示。
    /// </summary>
    public void EnterLittleGame()
    {
        if (!TryConsumeAction())
        {
            return;
        }

        if (littleGameSelectUI == null)
        {
            Debug.LogWarning("未绑定小游戏难度选择 Canvas，无法打开难度选择。", this);
            return;
        }

        littleGameSelectUI.Open();
        littleGameSelectUI.BringToFront();
    }

    /// <summary>
    /// 按钮入口：选择简单难度并进入小游戏。对应 WhackGameController.configs[0]。
    /// </summary>
    public void EnterEasyLittleGame()
    {
        EnterLittleGameWithConfig(EasyLittleGameConfigIndex);
    }

    /// <summary>
    /// 按钮入口：选择困难难度并进入小游戏。对应 WhackGameController.configs[1]。
    /// </summary>
    public void EnterHardLittleGame()
    {
        EnterLittleGameWithConfig(HardLittleGameConfigIndex);
    }

    public void EnterLittleGameWithConfig(int configIndex)
    {
        if (Data != null)
        {
            Data.selectedWhackGameConfigIndex = Mathf.Max(0, configIndex);
        }

        SceneManager.LoadScene(LittleGameSceneName);
    }

    /// <summary>
    /// 返回主菜单场景。
    /// </summary>
    public void BackToMainMenu()
    {
        SceneManager.LoadScene(MainMenuSceneName);
    }

    /// <summary>
    /// 按钮入口：保存当前玩家数据。
    /// </summary>
    public void SaveGame()
    {
        if (Data == null)
        {
            Debug.LogWarning("保存失败，PlayerData 不存在。", this);
            return;
        }

        int savedSlot = Data.SaveGameToNextSlot();
        saveName = PlayerData.GetSaveNameForSlot(savedSlot);
    }

    public void SaveGame(string targetSaveName)
    {
        if (Data == null)
        {
            Debug.LogWarning("保存失败，PlayerData 不存在。", this);
            return;
        }

        saveName = targetSaveName;
        Data.SaveGame(saveName);
    }

    /// <summary>
    /// 按钮入口：读取玩家数据，并刷新当前场景 UI。
    /// </summary>
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
        if (Data == null)
        {
            Debug.LogWarning("读档失败，PlayerData 不存在。", this);
            return;
        }

        saveName = targetSaveName;
        if (!Data.LoadGame(saveName))
        {
            return;
        }

        RefreshAfterLoad();
    }

    /// <summary>
    /// 按钮入口：删除本地存档文件。
    /// </summary>
    public void DeleteSave()
    {
        DeleteSaveSlot1();
    }

    public void DeleteSaveSlot1()
    {
        DeleteSave(PlayerData.GetSaveNameForSlot(1));
    }

    public void DeleteSaveSlot2()
    {
        DeleteSave(PlayerData.GetSaveNameForSlot(2));
    }

    public void DeleteSaveSlot3()
    {
        DeleteSave(PlayerData.GetSaveNameForSlot(3));
    }

    public void DeleteSave(string targetSaveName)
    {
        if (Data == null)
        {
            Debug.LogWarning("删档失败，PlayerData 不存在。", this);
            return;
        }

        saveName = targetSaveName;
        Data.DeleteSave(saveName);
    }

    public void SetSaveName(string targetSaveName)
    {
        if (!string.IsNullOrWhiteSpace(targetSaveName))
        {
            saveName = targetSaveName;
        }
    }

    private void RefreshAfterLoad()
    {
        if (clueManager == null)
        {
            clueManager = FindObjectOfType<ClueManager>();
        }

        clueManager?.RefreshData();
        UpdatePlayerUI();
    }

    /// <summary>
    /// 打开操作失败提示弹窗。
    /// </summary>
    public void ShowTip()
    {
        if (operationTipUI == null)
        {
            Debug.LogWarning("未绑定提示 UI，无法显示操作失败提示。", this);
            return;
        }

        operationTipUI.Open();
        operationTipUI.BringToFront();
    }

    /// <summary>
    /// 安全设置 TMP 文本。字段未绑定时静默跳过，避免 UI 原型阶段频繁空引用。
    /// </summary>
    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
        {
            label.text = value;
        }
    }
}

