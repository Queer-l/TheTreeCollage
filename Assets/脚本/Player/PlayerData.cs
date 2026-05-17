using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// 玩家运行时数据的唯一来源。
///
/// 当前项目保存体力、天数、已解锁线索和剧情分支状态。
/// 其他脚本应通过 PlayerData.Instance 读取或修改这些状态。
/// </summary>
public class PlayerData : MonoBehaviour
{
    private const string DefaultSaveName = "save";
    private const string SaveFileExtension = ".json";
    private const int MaxSaveSlot = 3;
    private const string NextSaveSlotPrefsKey = "PlayerData.NextSaveSlot";
    public static bool loadSaveOnNextAwake = false;
    public static string pendingLoadSaveName = DefaultSaveName;
    public static bool resetDataOnNextAwake = false;

    /// <summary>
    /// 场景内玩家数据单例。当前项目只有一个 GameScene 运行数据源。
    /// </summary>
    public static PlayerData Instance;

    [Header("玩家数据")]
    [Tooltip("玩家当前剩余体力。调查、获得线索、进入下一天都会消耗体力。")]
    public int steps = 3;

    [Tooltip("当前剧情推进天数。进入下一天时递增。")]
    public int date = 0;

    [Tooltip("每天可用的最大体力。进入下一天后体力会恢复到该值。")]
    public int maxSteps = 3;

    [Tooltip("玩家当前分数。小游戏、剧情奖励或结算流程可以通过方法修改。")]
    public int score = 0;

    [Tooltip("即将进入小游戏时选择的难度配置索引。WhackGameController 会用它选择 configs。")]
    public int selectedWhackGameConfigIndex = 0;

    [Tooltip("从小游戏返回 GameScene 时置为 true，用于避免重新播放初始剧情。")]
    public bool returningFromLittleGame = false;

    [Header("线索状态")]
    [Tooltip("已经解锁的线索 SO。线索展示界面会根据这个列表动态生成 slot。")]
    [FormerlySerializedAs("collectedClues")]
    public List<ClueSO> unlockedClues = new List<ClueSO>();

    [Tooltip("旧版已获得线索 ID，仅用于从旧数据迁移到 unlockedClues。")]
    [SerializeField]
    private List<int> collectedClueIds = new List<int>();

    [Header("剧情状态")]
    [Tooltip("当前剧情节点 ID。用于记录玩家推进到哪一段剧情。")]
    public string currentStoryNodeId;

    [Tooltip("关键抉择记录，用于控制后续分支和结局判定。")]
    public List<string> storyFlags = new List<string>();

    [Header("剧情历史")]
    [Tooltip("玩家走过的剧情节点 ID 记录。上一对话/下一对话会优先读取这里。")]
    public List<string> visitedStoryNodeIds = new List<string>();

    [Tooltip("当前剧情历史索引，指向 visitedStoryNodeIds 中当前正在显示的节点。")]
    public int storyHistoryIndex = -1;

    /// <summary>
    /// 当前是否还可以执行需要体力的行动。
    /// </summary>
    public bool HasSteps => steps > 0;

    public static string SaveFilePath => GetSaveFilePath(DefaultSaveName);

    private void Awake()
    {
        // 保证运行时只有一个 PlayerData。重复组件只销毁自身，不销毁所在物体；
        // 避免回到 GameScene 时把新场景里的 PlayerManager/DayManager/UI 引用一起销毁。
        if (Instance != null && Instance != this)
        {
            if (loadSaveOnNextAwake)
            {
                Instance.LoadGame(pendingLoadSaveName);
                loadSaveOnNextAwake = false;
            }
            else if (resetDataOnNextAwake)
            {
                Instance.ResetRuntimeData();
                resetDataOnNextAwake = false;
            }

            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ClampValues();

        if (loadSaveOnNextAwake)
        {
            LoadGame(pendingLoadSaveName);
            loadSaveOnNextAwake = false;
        }
        else if (resetDataOnNextAwake)
        {
            ResetRuntimeData();
            resetDataOnNextAwake = false;
        }
    }

    private void OnEnable()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RemoveSceneOnlyComponentsFromPersistentObject();
    }

    private void RemoveSceneOnlyComponentsFromPersistentObject()
    {
        PlayerManager playerManager = GetComponent<PlayerManager>();
        if (playerManager != null)
        {
            Destroy(playerManager);
        }

        DayManager dayManager = GetComponent<DayManager>();
        if (dayManager != null)
        {
            Destroy(dayManager);
        }
    }

    /// <summary>
    /// 尝试消耗 1 点体力。
    /// </summary>
    /// <returns>体力足够并成功扣除时返回 true，否则返回 false。</returns>
    public bool TryConsumeStep()
    {
        if (!HasSteps)
        {
            return false;
        }

        steps--;
        return true;
    }

    /// <summary>
    /// 进入下一天：天数递增，并把体力恢复到每天上限。
    /// </summary>
    public void AdvanceDay()
    {
        date++;
        steps = maxSteps;
    }

    /// <summary>
    /// 设置玩家分数。分数不会低于 0。
    /// </summary>
    public void SetScore(int value)
    {
        score = Mathf.Max(0, value);
    }

    /// <summary>
    /// 增加或减少玩家分数。传入负数可以扣分，最终分数不会低于 0。
    /// </summary>
    public void AddScore(int amount)
    {
        SetScore(score + amount);
    }

    public void ResetRuntimeData()
    {
        steps = maxSteps;
        date = 0;
        score = 0;
        selectedWhackGameConfigIndex = 0;
        returningFromLittleGame = false;
        currentStoryNodeId = string.Empty;
        storyHistoryIndex = -1;
        unlockedClues.Clear();
        collectedClueIds.Clear();
        storyFlags.Clear();
        visitedStoryNodeIds.Clear();
        ClampValues();
    }

    /// <summary>
    /// 尝试消耗指定分数。分数足够时扣除并返回 true，否则返回 false。
    /// </summary>
    public bool TrySpendScore(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (score < amount)
        {
            return false;
        }

        score -= amount;
        return true;
    }

    /// <summary>
    /// 记录玩家进入过的剧情节点 ID。
    /// 如果玩家先回看旧剧情，再进入一段新剧情，会丢弃当前位置之后的旧历史。
    /// </summary>
    public void RecordStoryNodeVisit(string nodeID)
    {
        if (string.IsNullOrWhiteSpace(nodeID))
        {
            return;
        }

        currentStoryNodeId = nodeID;

        if (storyHistoryIndex >= 0 &&
            storyHistoryIndex < visitedStoryNodeIds.Count &&
            visitedStoryNodeIds[storyHistoryIndex] == nodeID)
        {
            return;
        }

        if (storyHistoryIndex < visitedStoryNodeIds.Count - 1)
        {
            int removeStartIndex = storyHistoryIndex + 1;
            visitedStoryNodeIds.RemoveRange(removeStartIndex, visitedStoryNodeIds.Count - removeStartIndex);
        }

        visitedStoryNodeIds.Add(nodeID);
        storyHistoryIndex = visitedStoryNodeIds.Count - 1;
    }

    /// <summary>
    /// 查看上一条剧情历史 ID，但不移动历史索引。
    /// </summary>
    public bool TryPeekPreviousStoryNodeId(out string nodeID)
    {
        nodeID = null;

        if (storyHistoryIndex <= 0 || storyHistoryIndex > visitedStoryNodeIds.Count - 1)
        {
            return false;
        }

        nodeID = visitedStoryNodeIds[storyHistoryIndex - 1];
        return !string.IsNullOrWhiteSpace(nodeID);
    }

    /// <summary>
    /// 查看下一条剧情历史 ID，但不移动历史索引。
    /// </summary>
    public bool TryPeekNextStoryNodeId(out string nodeID)
    {
        nodeID = null;

        if (storyHistoryIndex < 0 || storyHistoryIndex >= visitedStoryNodeIds.Count - 1)
        {
            return false;
        }

        nodeID = visitedStoryNodeIds[storyHistoryIndex + 1];
        return !string.IsNullOrWhiteSpace(nodeID);
    }

    /// <summary>
    /// 移动到上一条剧情历史，并返回目标节点 ID。
    /// </summary>
    public bool TryMoveToPreviousStoryNode(out string nodeID)
    {
        if (!TryPeekPreviousStoryNodeId(out nodeID))
        {
            return false;
        }

        storyHistoryIndex--;
        currentStoryNodeId = nodeID;
        return true;
    }

    /// <summary>
    /// 移动到下一条剧情历史，并返回目标节点 ID。
    /// </summary>
    public bool TryMoveToNextStoryNode(out string nodeID)
    {
        if (!TryPeekNextStoryNodeId(out nodeID))
        {
            return false;
        }

        storyHistoryIndex++;
        currentStoryNodeId = nodeID;
        return true;
    }

    /// <summary>
    /// 判断指定线索资产是否已经解锁。
    /// </summary>
    public bool HasClue(ClueSO clue)
    {
        return clue != null && (unlockedClues.Contains(clue) || collectedClueIds.Contains(clue.clueId));
    }

    /// <summary>
    /// 判断指定线索 ID 是否已经解锁。用于剧情条件和旧数据兼容。
    /// </summary>
    public bool HasClue(int clueID)
    {
        foreach (ClueSO clue in unlockedClues)
        {
            if (clue != null && clue.clueId == clueID)
            {
                return true;
            }
        }

        return collectedClueIds.Contains(clueID);
    }

    /// <summary>
    /// 记录解锁线索。重复解锁同一 SO 不会重复加入列表。
    /// </summary>
    public void CollectClue(ClueSO clue)
    {
        if (clue == null || unlockedClues.Contains(clue))
        {
            return;
        }

        unlockedClues.Add(clue);
        if (!collectedClueIds.Contains(clue.clueId))
        {
            collectedClueIds.Add(clue.clueId);
        }
    }

    /// <summary>
    /// 把旧版 ID 数据迁移为 SO 引用。ClueManager 会在启动时传入完整线索表。
    /// </summary>
    public void SyncUnlockedCluesFromIds(IEnumerable<ClueSO> allClues)
    {
        if (allClues == null || collectedClueIds.Count == 0)
        {
            return;
        }

        foreach (ClueSO clue in allClues)
        {
            if (clue != null && collectedClueIds.Contains(clue.clueId) && !unlockedClues.Contains(clue))
            {
                unlockedClues.Add(clue);
            }
        }
    }

    /// <summary>
    /// 判断是否拥有某个剧情 flag。
    /// flag 适合记录“是否隐瞒父亲”“是否把信给 NPC 看过”等关键分支行为。
    /// </summary>
    public bool HasStoryFlag(string flag)
    {
        return !string.IsNullOrWhiteSpace(flag) && storyFlags.Contains(flag);
    }

    /// <summary>
    /// 添加剧情 flag。空字符串和重复 flag 会被忽略。
    /// </summary>
    public void AddStoryFlag(string flag)
    {
        if (string.IsNullOrWhiteSpace(flag) || storyFlags.Contains(flag))
        {
            return;
        }

        storyFlags.Add(flag);
    }

    public void SaveGame()
    {
        SaveGameToNextSlot();
    }

    public int SaveGameToNextSlot()
    {
        int slot = GetNextSaveSlot();
        SaveGameSlot(slot);
        SetNextSaveSlot(slot % MaxSaveSlot + 1);
        return slot;
    }

    public void SaveGameSlot(int slot)
    {
        SaveGame(GetSaveNameForSlot(slot));
    }

    public void SaveGame(string saveName)
    {
        SaveData saveData = CreateSaveData();
        string json = JsonUtility.ToJson(saveData, true);
        string savePath = GetSaveFilePath(saveName);
        File.WriteAllText(savePath, json);
        Debug.Log($"存档成功: {savePath}", this);
    }

    public bool LoadGame()
    {
        return LoadGameSlot(1);
    }

    public bool LoadGameSlot(int slot)
    {
        return LoadGame(GetSaveNameForSlot(slot));
    }

    public bool LoadGame(string saveName)
    {
        string savePath = GetSaveFilePath(saveName);
        if (!File.Exists(savePath))
        {
            Debug.LogWarning($"读档失败，找不到存档文件: {savePath}", this);
            return false;
        }

        string json = File.ReadAllText(savePath);
        SaveData saveData = JsonUtility.FromJson<SaveData>(json);
        if (saveData == null)
        {
            Debug.LogWarning($"读档失败，存档内容无效: {savePath}", this);
            return false;
        }

        ApplySaveData(saveData);
        Debug.Log($"读档成功: {savePath}", this);
        return true;
    }

    public bool HasSave()
    {
        return HasSaveSlot(1);
    }

    public bool HasSaveSlot(int slot)
    {
        return HasSave(GetSaveNameForSlot(slot));
    }

    public bool HasSave(string saveName)
    {
        return File.Exists(GetSaveFilePath(saveName));
    }

    public static bool HasSaveFile()
    {
        return HasSaveSlotFile(1);
    }

    public static bool HasSaveSlotFile(int slot)
    {
        return HasSaveFile(GetSaveNameForSlot(slot));
    }

    public static bool HasSaveFile(string saveName)
    {
        return File.Exists(GetSaveFilePath(saveName));
    }

    public static List<string> GetAllSaveNames()
    {
        List<string> saveNames = new List<string>();
        if (!Directory.Exists(Application.persistentDataPath))
        {
            return saveNames;
        }

        string[] saveFiles = Directory.GetFiles(Application.persistentDataPath, $"*{SaveFileExtension}");
        foreach (string saveFile in saveFiles)
        {
            string saveName = Path.GetFileNameWithoutExtension(saveFile);
            if (!string.IsNullOrWhiteSpace(saveName) && !saveNames.Contains(saveName))
            {
                saveNames.Add(saveName);
            }
        }

        saveNames.Sort();
        return saveNames;
    }

    public void DeleteSave()
    {
        DeleteSaveSlot(1);
    }

    public void DeleteSaveSlot(int slot)
    {
        DeleteSave(GetSaveNameForSlot(slot));
    }

    public void DeleteSave(string saveName)
    {
        string savePath = GetSaveFilePath(saveName);
        if (!File.Exists(savePath))
        {
            return;
        }

        File.Delete(savePath);
        Debug.Log($"删档成功: {savePath}", this);
    }

    public static string GetSaveFilePath(string saveName)
    {
        return Path.Combine(Application.persistentDataPath, $"{SanitizeSaveName(saveName)}{SaveFileExtension}");
    }

    public static string GetSaveNameForSlot(int slot)
    {
        return NormalizeSaveSlot(slot).ToString();
    }

    public static int GetNextSaveSlot()
    {
        return NormalizeSaveSlot(PlayerPrefs.GetInt(NextSaveSlotPrefsKey, 1));
    }

    private static void SetNextSaveSlot(int slot)
    {
        PlayerPrefs.SetInt(NextSaveSlotPrefsKey, NormalizeSaveSlot(slot));
        PlayerPrefs.Save();
    }

    private static int NormalizeSaveSlot(int slot)
    {
        return Mathf.Clamp(slot, 1, MaxSaveSlot);
    }

    private static string SanitizeSaveName(string saveName)
    {
        string safeName = string.IsNullOrWhiteSpace(saveName) ? DefaultSaveName : saveName.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(invalidChar, '_');
        }

        return string.IsNullOrWhiteSpace(safeName) ? DefaultSaveName : safeName;
    }

    private SaveData CreateSaveData()
    {
        SaveData saveData = new SaveData
        {
            steps = steps,
            date = date,
            maxSteps = maxSteps,
            score = score,
            selectedWhackGameConfigIndex = selectedWhackGameConfigIndex,
            currentStoryNodeId = currentStoryNodeId,
            storyHistoryIndex = storyHistoryIndex,
            storyFlags = new List<string>(storyFlags),
            visitedStoryNodeIds = new List<string>(visitedStoryNodeIds),
            unlockedClueIds = GetUnlockedClueIds()
        };

        return saveData;
    }

    private void ApplySaveData(SaveData saveData)
    {
        steps = saveData.steps;
        date = saveData.date;
        maxSteps = saveData.maxSteps;
        score = saveData.score;
        selectedWhackGameConfigIndex = saveData.selectedWhackGameConfigIndex;
        returningFromLittleGame = false;
        currentStoryNodeId = saveData.currentStoryNodeId;
        storyHistoryIndex = saveData.storyHistoryIndex;
        storyFlags = saveData.storyFlags != null ? new List<string>(saveData.storyFlags) : new List<string>();
        visitedStoryNodeIds = saveData.visitedStoryNodeIds != null ? new List<string>(saveData.visitedStoryNodeIds) : new List<string>();
        collectedClueIds = saveData.unlockedClueIds != null ? new List<int>(saveData.unlockedClueIds) : new List<int>();
        unlockedClues.Clear();
        ClampValues();
        storyHistoryIndex = Mathf.Clamp(storyHistoryIndex, -1, visitedStoryNodeIds.Count - 1);
    }

    private List<int> GetUnlockedClueIds()
    {
        List<int> clueIds = new List<int>();

        foreach (int clueId in collectedClueIds)
        {
            if (!clueIds.Contains(clueId))
            {
                clueIds.Add(clueId);
            }
        }

        foreach (ClueSO clue in unlockedClues)
        {
            if (clue != null && !clueIds.Contains(clue.clueId))
            {
                clueIds.Add(clue.clueId);
            }
        }

        return clueIds;
    }

    /// <summary>
    /// 修正 Inspector 中可能填错的初始数值，避免体力上限为 0 或日期为负数。
    /// </summary>
    private void ClampValues()
    {
        maxSteps = Mathf.Max(1, maxSteps);
        steps = Mathf.Clamp(steps, 0, maxSteps);
        date = Mathf.Max(0, date);
        score = Mathf.Max(0, score);
        selectedWhackGameConfigIndex = Mathf.Max(0, selectedWhackGameConfigIndex);
    }

    [System.Serializable]
    private class SaveData
    {
        public int steps;
        public int date;
        public int maxSteps;
        public int score;
        public int selectedWhackGameConfigIndex;
        public string currentStoryNodeId;
        public List<string> storyFlags = new List<string>();
        public List<string> visitedStoryNodeIds = new List<string>();
        public int storyHistoryIndex = -1;
        public List<int> unlockedClueIds = new List<int>();
    }
}
