using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 线索数据和线索展示的统一控制脚本。
///
/// allClues 保存项目里的全部线索 SO；PlayerData.unlockedClues 保存玩家已经解锁的线索。
/// 打开线索展示界面时，本脚本会根据已解锁线索动态生成 ClueSlot。
/// </summary>
public class ClueManager : MonoBehaviour
{
    [Header("全部线索数据")]
    [Tooltip("项目中全部线索 SO 都放在这里，作为统一线索目录。")]
    [FormerlySerializedAs("clueCatalog")]
    public List<ClueSO> allClues = new List<ClueSO>();

    [Header("线索展示界面")]
    [Tooltip("线索展示界面的 UIControl。按钮打开线索界面时可以调用 OpenCluePanel。")]
    public UIControl cluePanelUI;

    [Tooltip("动态生成已解锁线索 slot 的父物体。")]
    public Transform slotParent;

    [Tooltip("动态生成已解锁线索 slot 使用的预制体。")]
    public ClueSlot slotPrefab;

    [Tooltip("Start 时是否立刻生成一次已解锁线索列表。")]
    public bool generateOnStart = false;

    [Header("Clue Pages")]
    [Tooltip("每页最多显示的线索数量。")]
    public int slotsPerPage = 10;

    [Tooltip("上一页按钮。可不绑定；绑定后会自动刷新是否可点击。")]
    public Button previousPageButton;

    [Tooltip("下一页按钮。可不绑定；绑定后会自动刷新是否可点击。")]
    public Button nextPageButton;

    [Header("场景预放线索格子")]
    [Tooltip("兼容旧场景中已经摆好的线索格子。新展示界面建议使用 slotParent + slotPrefab 动态生成。")]
    [FormerlySerializedAs("clueSlots")]
    public List<ClueSlot> preplacedSlots = new List<ClueSlot>();

    [Header("管理器引用")]
    [Tooltip("玩家管理器，用于传给每个线索格子处理体力和 UI。")]
    public PlayerManager playerManager;

    [Header("UI 文本")]
    [Tooltip("显示已解锁线索数量。")]
    public TextMeshProUGUI collectedCountText;

    [Tooltip("显示已解锁关键线索数量。")]
    public TextMeshProUGUI keyClueCountText;

    [Tooltip("运行时统计：已解锁线索数量。")]
    public int collectedCount;

    [Tooltip("运行时统计：已解锁关键线索数量。")]
    public int keyClueCount;

    private readonly List<ClueSlot> generatedSlots = new List<ClueSlot>();
    private int currentPageIndex = 0;
    private bool wasCluePanelOpen = false;

    private void Awake()
    {
        if (playerManager == null)
        {
            playerManager = FindObjectOfType<PlayerManager>();
        }

        if (cluePanelUI == null)
        {
            cluePanelUI = GetComponent<UIControl>();
        }
    }

    private void Start()
    {
        SyncPlayerData();
        BindPreplacedSlots();
        RefreshData();
        wasCluePanelOpen = cluePanelUI != null && cluePanelUI.isOpen;

        if (generateOnStart)
        {
            GenerateUnlockedClueSlots();
        }
    }

    private void LateUpdate()
    {
        if (cluePanelUI == null)
        {
            return;
        }

        bool isCluePanelOpen = cluePanelUI.isOpen;
        if (isCluePanelOpen && !wasCluePanelOpen)
        {
            RefreshOpenedCluePanel(false);
        }

        wasCluePanelOpen = isCluePanelOpen;
    }

    /// <summary>
    /// 按 allClues 的顺序把线索数据绑定到场景预放格子，兼容旧场景。
    /// </summary>
    public void BindPreplacedSlots()
    {
        for (int i = 0; i < preplacedSlots.Count; i++)
        {
            ClueSO clue = i < allClues.Count ? allClues[i] : null;

            if (preplacedSlots[i] != null)
            {
                preplacedSlots[i].Bind(clue, this, playerManager);
            }
        }
    }

    /// <summary>
    /// 打开线索展示界面，并动态生成已解锁线索 slot。
    /// </summary>
    public void OpenCluePanel()
    {
        RefreshOpenedCluePanel(true);

        if (cluePanelUI != null)
        {
            cluePanelUI.Open();
            cluePanelUI.BringToFront();
            wasCluePanelOpen = true;
        }
    }

    /// <summary>
    /// 关闭线索展示界面。
    /// </summary>
    public void CloseCluePanel()
    {
        if (cluePanelUI != null)
        {
            cluePanelUI.Close();
            wasCluePanelOpen = false;
        }
    }

    private void RefreshOpenedCluePanel(bool resetPage)
    {
        SyncPlayerData();
        BindPreplacedSlots();

        if (resetPage)
        {
            currentPageIndex = 0;
        }

        GenerateUnlockedClueSlots();
        RefreshData();
    }

    /// <summary>
    /// 根据 PlayerData.unlockedClues 动态生成线索 slot，并给每个 slot 赋值对应 SO。
    /// </summary>
    public void GenerateUnlockedClueSlots()
    {
        ClearGeneratedSlots();

        if (PlayerData.Instance == null || slotParent == null || slotPrefab == null)
        {
            return;
        }

        List<ClueSO> unlockedClues = GetUnlockedClues();
        ClampCurrentPage(unlockedClues.Count);

        int startIndex = currentPageIndex * GetSafeSlotsPerPage();
        int endIndex = Mathf.Min(startIndex + GetSafeSlotsPerPage(), unlockedClues.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            ClueSO clue = unlockedClues[i];
            if (clue == null)
            {
                continue;
            }

            ClueSlot slot = Instantiate(slotPrefab, slotParent);
            slot.Bind(clue, this, playerManager);
            generatedSlots.Add(slot);
        }

        RefreshPageButtons(unlockedClues.Count);
    }

    /// <summary>
    /// 给“上一页”按钮绑定的入口。
    /// </summary>
    public void PreviousCluePage()
    {
        SyncPlayerData();

        if (currentPageIndex <= 0)
        {
            RefreshPageButtons(GetUnlockedClues().Count);
            return;
        }

        currentPageIndex--;
        GenerateUnlockedClueSlots();
        RefreshData();
    }

    /// <summary>
    /// 给“下一页”按钮绑定的入口。
    /// </summary>
    public void NextCluePage()
    {
        SyncPlayerData();

        List<ClueSO> unlockedClues = GetUnlockedClues();
        int maxPageIndex = GetMaxPageIndex(unlockedClues.Count);
        if (currentPageIndex >= maxPageIndex)
        {
            RefreshPageButtons(unlockedClues.Count);
            return;
        }

        currentPageIndex++;
        GenerateUnlockedClueSlots();
        RefreshData();
    }

    /// <summary>
    /// 刷新线索统计和所有线索格子的显示状态。
    /// 获得新线索后应调用该方法。
    /// </summary>
    public void RefreshData()
    {
        SyncPlayerData();

        collectedCount = 0;
        keyClueCount = 0;

        if (PlayerData.Instance != null)
        {
            foreach (ClueSO clue in PlayerData.Instance.unlockedClues)
            {
                if (clue == null)
                {
                    continue;
                }

                collectedCount++;
                if (clue.isKeyClue)
                {
                    keyClueCount++;
                }
            }
        }

        RefreshSlots(preplacedSlots);
        RefreshSlots(generatedSlots);
        RefreshSummaryUI();
        RefreshPageButtons(collectedCount);
    }

    private void SyncPlayerData()
    {
        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.SyncUnlockedCluesFromIds(allClues);
        }
    }

    private void ClearGeneratedSlots()
    {
        foreach (ClueSlot slot in generatedSlots)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }

        generatedSlots.Clear();
    }

    private List<ClueSO> GetUnlockedClues()
    {
        List<ClueSO> result = new List<ClueSO>();
        if (PlayerData.Instance == null)
        {
            return result;
        }

        foreach (ClueSO clue in PlayerData.Instance.unlockedClues)
        {
            if (clue != null)
            {
                result.Add(clue);
            }
        }

        return result;
    }

    private int GetSafeSlotsPerPage()
    {
        return Mathf.Max(1, slotsPerPage);
    }

    private int GetMaxPageIndex(int itemCount)
    {
        if (itemCount <= 0)
        {
            return 0;
        }

        return Mathf.CeilToInt(itemCount / (float)GetSafeSlotsPerPage()) - 1;
    }

    private void ClampCurrentPage(int itemCount)
    {
        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, GetMaxPageIndex(itemCount));
    }

    private void RefreshPageButtons(int itemCount)
    {
        ClampCurrentPage(itemCount);

        if (previousPageButton != null)
        {
            previousPageButton.interactable = currentPageIndex > 0;
        }

        if (nextPageButton != null)
        {
            nextPageButton.interactable = currentPageIndex < GetMaxPageIndex(itemCount);
        }
    }

    private static void RefreshSlots(List<ClueSlot> slots)
    {
        foreach (ClueSlot slot in slots)
        {
            if (slot == null)
            {
                continue;
            }

            slot.RefreshCollectState();
            slot.RefreshView();
        }
    }

    private void RefreshSummaryUI()
    {
        SetText(collectedCountText, $"已解锁线索: {collectedCount}");
        SetText(keyClueCountText, $"关键线索: {keyClueCount}");
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
        {
            label.text = value;
        }
    }
}
