using UnityEngine;

/// <summary>
/// 一天流程管理器。
///
/// 当前玩法中“一天”由多次调查行动组成：调查消耗体力，进入下一天会消耗一次行动并恢复体力。
/// 后续如果要加入随机事件、NPC 刷新、日报结算，都应该优先放到这里，而不是塞进 PlayerManager。
/// </summary>
public class DayManager : MonoBehaviour
{
    [Header("管理器引用")]
    [Tooltip("玩家操作和 UI 管理器。")]
    public PlayerManager playerManager;

    [Tooltip("线索管理器。当前预留给每日刷新线索或统计使用。")]
    public ClueManager clueManager;

    [Tooltip("剧情管理器。当前预留给每日触发剧情节点使用。")]
    public StoryManager storyManager;

    [Tooltip("勾选后，下一天按钮在普通模式且存在可进入的剧情进度时，会先打开剧情 UI，再执行进入下一天。")]
    public bool enterStoryModeBeforeNextDay = true;

    [Header("当天记录")]
    [Tooltip("当天已经调查的次数。进入下一天后会清零。")]
    public int investigatedTimes;

    [Tooltip("当天摘要文本，方便之后接日报 UI。")]
    public string currentDaySummary;

    private PlayerData Data => PlayerData.Instance;

    private void Awake()
    {
        // 尽量自动查找依赖，减少 Unity Inspector 初期配置成本。
        if (playerManager == null)
        {
            playerManager = FindObjectOfType<PlayerManager>();
        }

        if (clueManager == null)
        {
            clueManager = FindObjectOfType<ClueManager>();
        }

        if (storyManager == null)
        {
            storyManager = FindObjectOfType<StoryManager>();
        }
    }

    private void Start()
    {
        StartDay();
    }

    /// <summary>
    /// 开始一天：清空当天临时记录，并刷新玩家 UI。
    /// </summary>
    public void StartDay()
    {
        investigatedTimes = 0;
        currentDaySummary = string.Empty;
        RefreshPlayerUI();
    }

    /// <summary>
    /// 执行一次调查行动。
    /// 调查会消耗 1 点体力，并更新当天调查次数。
    /// </summary>
    public void ProcessInvestigation()
    {
        if (Data == null || playerManager == null)
        {
            return;
        }

        if (!playerManager.TryConsumeAction())
        {
            return;
        }

        investigatedTimes++;
        currentDaySummary = $"今日已调查 {investigatedTimes} 次";
        RefreshPlayerUI();
    }

    /// <summary>
    /// 旧经营流程兼容入口。当前等价于一次调查行动。
    /// </summary>
    public void ProcessBusiness()
    {
        ProcessInvestigation();
    }

    /// <summary>
    /// 结束当天并推进到下一天。
    /// 这里保留“进入下一天也消耗体力”的规则，和旧按钮行为一致。
    /// </summary>
    public void EndDay()
    {
        if (Data == null || playerManager == null)
        {
            return;
        }

        if (!playerManager.TryConsumeAction())
        {
            return;
        }

        Data.AdvanceDay();
        StartDay();
    }

    /// <summary>
    /// 给 UI 按钮使用的语义化入口。
    /// </summary>
    public void GoToNextDay()
    {
        if (enterStoryModeBeforeNextDay &&
            storyManager != null &&
            storyManager.CanEnterStoryModeFromNormalMode())
        {
            storyManager.EnterStoryMode();
        }

        EndDay();
    }

    /// <summary>
    /// 安全刷新玩家 UI。
    /// </summary>
    private void RefreshPlayerUI()
    {
        if (playerManager != null)
        {
            playerManager.UpdatePlayerUI();
        }
    }
}
