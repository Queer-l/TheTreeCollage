using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 小游戏结算 Canvas 控制脚本。
/// 显示当局分数和奖励线索，并提供返回 GameScene 的按钮入口。
/// </summary>
public class WhackSettlementUI : MonoBehaviour
{
    private const string GameSceneName = "GameScene";

    [Header("显示控制")]
    public UIControl settlementPanelUI;

    [Header("文本")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI rewardText;
    public TextMeshProUGUI highScoreText;

    [Header("奖励图片")]
    public Image rewardImage;

    [Header("调试")]
    [Tooltip("开启后输出结算 UI 打开和返回场景日志。")]
    public bool enableDebugLog = true;

    private void Awake()
    {
        if (settlementPanelUI == null)
        {
            settlementPanelUI = GetComponent<UIControl>();
        }
    }

    public void OpenSettlement(int score, int highScore, ClueSO rewardClue, bool rewardUnlocked)
    {
        LogDebug($"OpenSettlement：打开结算。score={score}，highScore={highScore}，rewardUnlocked={rewardUnlocked}，reward={(rewardClue != null ? rewardClue.clueName : "无")}，panel={(settlementPanelUI != null ? settlementPanelUI.name : "未绑定")}。");
        SetText(scoreText, $"本局分数: {score}");
        SetText(highScoreText, $"最高分: {highScore}");

        if (rewardUnlocked && rewardClue != null)
        {
            SetText(rewardText, $"获得线索: {rewardClue.clueName}");
            SetRewardImage(rewardClue.clueImage);
        }
        else
        {
            SetText(rewardText, "未获得奖励");
            SetRewardImage(null);
        }

        if (settlementPanelUI != null)
        {
            settlementPanelUI.Open();
            settlementPanelUI.BringToFront();
            LogDebug("OpenSettlement：结算面板已 Open 并 BringToFront。");
        }
        else
        {
            LogDebug("OpenSettlement：settlementPanelUI 未绑定，无法打开结算面板。");
        }
    }

    public void CloseSettlement()
    {
        LogDebug("CloseSettlement：关闭结算面板。");
        if (settlementPanelUI != null)
        {
            settlementPanelUI.Close();
        }
    }

    /// <summary>
    /// 按钮入口：返回主游戏场景。
    /// </summary>
    public void BackToGameScene()
    {
        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.returningFromLittleGame = true;
        }

        LogDebug($"BackToGameScene：加载场景 {GameSceneName}。");
        SceneManager.LoadScene(GameSceneName);
    }

    private void SetRewardImage(Sprite sprite)
    {
        if (rewardImage == null)
        {
            return;
        }

        rewardImage.sprite = sprite;
        rewardImage.enabled = sprite != null;
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
        {
            label.text = value;
        }
    }

    private void LogDebug(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[WhackSettlementUI] {message}", this);
        }
    }
}
