using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单个线索格子的 UI 与交互脚本。
///
/// 它负责显示一个 ClueSO 的图标、名称、获得状态；点击已获得线索时会广播 OnClueClick，详情面板可以监听该事件。
/// 线索是否已经获得不由格子自己决定，而是读取 PlayerData，保证所有 UI 显示一致。
/// </summary>
public class ClueSlot : MonoBehaviour
{
    [Header("线索信息")]
    [Tooltip("当前格子绑定的线索数据。通常由 ClueManager 自动分配。")]
    public ClueSO clue;

    [Tooltip("显示线索图标的 Image。")]
    public Image clueImage;

    [Tooltip("显示线索名称的 TMP 文本。")]
    public TextMeshProUGUI clueName;

    [Tooltip("显示未获得/提示状态的 TMP 文本。")]
    public TextMeshProUGUI clueTip;

    [Header("收集表现")]
    [Tooltip("当前线索是否已获得。运行时会从 PlayerData 刷新。")]
    public bool isCollected = false;

    [Tooltip("未获得线索时的图标颜色。")]
    public Color unknownColor = Color.gray;

    [Tooltip("已获得线索时的图标颜色。")]
    public Color collectedColor = Color.white;

    [Tooltip("未获得线索时显示的提示文字。")]
    public string unknownTip = "未获得";

    [Header("管理器引用")]
    [Tooltip("线索管理器，用于获得线索后刷新整体统计。")]
    public ClueManager clueManager;

    [Tooltip("玩家管理器，用于消耗体力和刷新玩家 UI。")]
    public PlayerManager playerManager;

    [Header("按钮")]
    [Tooltip("动态生成的 slot 会自动把 Button 点击绑定到 OnClick。若 prefab 已在 Inspector 手动绑定，可关闭它避免重复触发。")]
    public bool autoRegisterButtonClick = true;

    /// <summary>
    /// 已获得线索被点击时广播。ClueInfoUI 监听该事件显示详情。
    /// </summary>
    public static event Action<ClueSO> OnClueClick;

    private Button slotButton;

    private void Awake()
    {
        slotButton = GetComponent<Button>();
        if (autoRegisterButtonClick && slotButton != null)
        {
            slotButton.onClick.AddListener(OnClick);
        }
    }

    private void OnDestroy()
    {
        if (autoRegisterButtonClick && slotButton != null)
        {
            slotButton.onClick.RemoveListener(OnClick);
        }
    }

    private void Start()
    {
        RefreshView();
    }

    /// <summary>
    /// 由 ClueManager 调用，把指定线索数据绑定到当前格子。
    /// </summary>
    public void Bind(ClueSO newClue, ClueManager manager, PlayerManager player)
    {
        clue = newClue;
        clueManager = manager;
        playerManager = player;
        RefreshCollectState();
        RefreshView();
    }

    /// <summary>
    /// 从 PlayerData 读取当前线索是否已经获得。
    /// </summary>
    public void RefreshCollectState()
    {
        isCollected = PlayerData.Instance != null && PlayerData.Instance.HasClue(clue);
    }

    /// <summary>
    /// 根据线索数据和获得状态刷新图标、文本、按钮可交互状态。
    /// </summary>
    public void RefreshView()
    {
        if (clue == null)
        {
            isCollected = false;
            SetText(clueName, "空线索");
            SetText(clueTip, string.Empty);
            SetImage(null, unknownColor);
            SetInteractable(false);
            return;
        }

        SetText(clueName, clue.clueName);
        SetImage(clue.clueImage, isCollected ? collectedColor : unknownColor);
        SetText(clueTip, isCollected ? string.Empty : unknownTip);
        SetInteractable(isCollected);
    }

    /// <summary>
    /// 尝试获得当前格子的线索。
    /// 首次获得会通过 PlayerManager 消耗体力，并写入收集 flag。
    /// </summary>
    public void Collect()
    {
        if (isCollected || clue == null)
        {
            return;
        }

        if (playerManager == null)
        {
            Debug.LogWarning($"线索 {clue.clueName} 未绑定 PlayerManager，无法获得。", this);
            return;
        }

        if (!playerManager.TryCollectClue(clue))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(clue.storyFlagOnCollect) && PlayerData.Instance != null)
        {
            PlayerData.Instance.AddStoryFlag(clue.storyFlagOnCollect);
        }

        RefreshCollectState();
        RefreshView();
        clueManager?.RefreshData();
    }

    /// <summary>
    /// Unity Button 点击入口。只有已获得线索才允许打开详情。
    /// </summary>
    public void OnClick()
    {
        RefreshCollectState();

        if (!isCollected || clue == null)
        {
            return;
        }

        Debug.Log($"点击线索: {clue.clueName}", this);
        OnClueClick?.Invoke(clue);
    }

    private void SetInteractable(bool interactable)
    {
        if (slotButton != null)
        {
            slotButton.interactable = interactable;
        }
    }

    private void SetImage(Sprite sprite, Color color)
    {
        if (clueImage == null)
        {
            return;
        }

        clueImage.sprite = sprite;
        clueImage.color = color;
        clueImage.enabled = sprite != null;
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
        {
            label.text = value;
        }
    }
}
