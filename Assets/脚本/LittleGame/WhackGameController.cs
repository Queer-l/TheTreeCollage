using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 打地鼠类小游戏的数据和流程控制。
/// 负责记录生成间隔、目标存在时间、当局得分和当局时间，并按配置生成目标物。
/// </summary>
public class WhackGameController : MonoBehaviour
{
    [Header("难度配置")]
    [Tooltip("可选难度配置列表。通过 currentConfigIndex 选择当前难度。")]
    public WhackGameConfigSO[] configs = new WhackGameConfigSO[0];

    [Tooltip("当前使用的难度配置索引。")]
    public int currentConfigIndex = 0;

    [Tooltip("当前使用的难度配置。")]
    public WhackGameConfigSO currentConfig;

    [Header("生成设置")]
    [Tooltip("被点击物品预制体。预制体上建议挂 WhackTarget。")]
    public WhackTarget targetPrefab;

    [Tooltip("目标生成区域的左下角世界坐标。")]
    public Vector2 spawnAreaBottomLeft = new Vector2(-4f, -3f);

    [Tooltip("目标生成区域的右上角世界坐标。")]
    public Vector2 spawnAreaTopRight = new Vector2(4f, 3f);

    [Tooltip("生成目标使用的 Z 坐标。2D 游戏通常保持为 0。")]
    public float spawnZ = 0f;

    [Tooltip("生成目标的父物体。为空时生成在场景根节点。")]
    public Transform targetParent;

    [Tooltip("每隔多少秒生成一个目标。")]
    public float spawnInterval = 1f;

    [Tooltip("每个目标如果没被点击，最多存在多少秒。")]
    public float targetLifetime = 1.5f;

    [Header("当局数据")]
    [Tooltip("每局总时间，单位秒。")]
    public float roundDuration = 30f;

    [Tooltip("当局剩余时间，单位秒。")]
    public float remainingTime;

    [Tooltip("当局得分。")]
    public int roundScore;

    [Tooltip("当前最高分。开局时从 PlayerData.score 读取，结算时破纪录才写回。")]
    public int highScore;

    [Tooltip("开始时是否自动开局。")]
    public bool playOnStart = false;

    [Header("UI")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timeText;
    public WhackSettlementUI settlementUI;

    [Header("事件")]
    public UnityEvent onRoundStarted;
    public UnityEvent onRoundPaused;
    public UnityEvent onRoundResumed;
    public UnityEvent onRoundEnded;
    public UnityEvent<int> onScoreChanged;
    public UnityEvent<float> onTimeChanged;

    [Header("调试")]
    [Tooltip("开启后输出小游戏按钮、结束和结算流程日志。")]
    public bool enableDebugLog = true;

    private readonly List<WhackTarget> activeTargets = new List<WhackTarget>();
    private Coroutine roundRoutine;
    private Coroutine spawnRoutine;
    private bool isPlaying;
    private bool isPaused;

    public bool IsPlaying => isPlaying;
    public bool IsPaused => isPaused;

    private void Awake()
    {
        if (PlayerData.Instance != null)
        {
            currentConfigIndex = PlayerData.Instance.selectedWhackGameConfigIndex;
        }

        ApplyCurrentConfig();
    }

    private void OnEnable()
    {
        WhackTarget.TargetHit += HandleTargetHit;
    }

    private void OnDisable()
    {
        WhackTarget.TargetHit -= HandleTargetHit;
    }

    private void Start()
    {
        ResetRoundData();

        if (playOnStart)
        {
            StartRound();
        }
    }

    public void StartRound()
    {
        LogDebug("StartRound：开始新一局。");
        StopRoundCoroutines();
        ClearTargets();
        ApplyCurrentConfig();
        ResetRoundData();
        SyncHighScoreFromPlayerData();

        isPlaying = true;
        isPaused = false;
        LogDebug($"StartRound：状态已初始化。roundDuration={roundDuration}，remainingTime={remainingTime}，spawnInterval={spawnInterval}，targetLifetime={targetLifetime}，targetPrefab={(targetPrefab != null ? targetPrefab.name : "未绑定")}，settlementUI={(settlementUI != null ? settlementUI.name : "未绑定")}。");
        onRoundStarted?.Invoke();

        roundRoutine = StartCoroutine(RoundTimerRoutine());
        spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    public void SelectConfig(int configIndex)
    {
        if (configs == null || configs.Length == 0)
        {
            currentConfigIndex = 0;
            currentConfig = null;
            ApplyCurrentConfig();
            return;
        }

        currentConfigIndex = Mathf.Clamp(configIndex, 0, Mathf.Max(0, configs.Length - 1));
        ApplyCurrentConfig();
    }

    public void SelectNextConfig()
    {
        if (configs == null || configs.Length == 0)
        {
            return;
        }

        SelectConfig((currentConfigIndex + 1) % configs.Length);
    }

    public void SelectPreviousConfig()
    {
        if (configs == null || configs.Length == 0)
        {
            return;
        }

        SelectConfig((currentConfigIndex - 1 + configs.Length) % configs.Length);
    }

    /// <summary>
    /// 按钮入口：开始新一局。
    /// </summary>
    public void StartGame()
    {
        LogDebug("StartGame：按钮入口被调用。");
        StartRound();
    }

    /// <summary>
    /// 按钮入口：暂停当前局。暂停后不再生成目标，倒计时和目标存在时间也会停止。
    /// </summary>
    public void PauseGame()
    {
        LogDebug($"PauseGame：按钮入口被调用。isPlaying={isPlaying}，isPaused={isPaused}。");
        if (!isPlaying || isPaused)
        {
            return;
        }

        isPaused = true;
        onRoundPaused?.Invoke();
    }

    /// <summary>
    /// 按钮入口：继续当前局。
    /// </summary>
    public void ResumeGame()
    {
        LogDebug($"ResumeGame：按钮入口被调用。isPlaying={isPlaying}，isPaused={isPaused}。");
        if (!isPlaying || !isPaused)
        {
            return;
        }

        isPaused = false;
        onRoundResumed?.Invoke();
    }

    /// <summary>
    /// 按钮入口：取消暂停。等价于 ResumeGame，方便在 Button OnClick 中按语义查找。
    /// </summary>
    public void CancelPauseGame()
    {
        LogDebug("CancelPauseGame：按钮入口被调用。");
        ResumeGame();
    }

    /// <summary>
    /// 按钮入口：暂停和继续之间切换。
    /// </summary>
    public void TogglePauseGame()
    {
        LogDebug($"TogglePauseGame：按钮入口被调用。isPlaying={isPlaying}，isPaused={isPaused}。");
        if (isPaused)
        {
            ResumeGame();
            return;
        }

        PauseGame();
    }

    /// <summary>
    /// 按钮入口：结束当前局并进行最高分结算。
    /// </summary>
    public void FinishGame()
    {
        LogDebug("FinishGame：按钮入口被调用。");
        EndRound();
    }

    public void EndRound()
    {
        LogDebug($"EndRound：尝试结束当前局。isPlaying={isPlaying}，isPaused={isPaused}，roundScore={roundScore}，remainingTime={remainingTime}。");
        if (!isPlaying)
        {
            LogDebug("EndRound：当前没有正在进行的局，结束请求被忽略。");
            return;
        }

        isPlaying = false;
        isPaused = false;
        StopRoundCoroutines();
        ClearTargets();
        SaveHighScoreIfNeeded();
        OpenSettlementIfReady();
        onRoundEnded?.Invoke();
        LogDebug("EndRound：结束流程完成。");
    }

    public void AddRoundScore(int amount)
    {
        roundScore = Mathf.Max(0, roundScore + amount);
        RefreshScoreUI();
        onScoreChanged?.Invoke(roundScore);
    }

    public void SetRoundScore(int value)
    {
        roundScore = Mathf.Max(0, value);
        RefreshScoreUI();
        onScoreChanged?.Invoke(roundScore);
    }

    public void ResetRoundData()
    {
        roundScore = 0;
        remainingTime = Mathf.Max(0f, roundDuration);
        SyncHighScoreFromPlayerData();
        RefreshScoreUI();
        RefreshTimeUI();
    }

    public void ApplyCurrentConfig()
    {
        if (configs == null || configs.Length == 0)
        {
            currentConfig = null;
            ClampRuntimeSettings();
            return;
        }

        currentConfigIndex = Mathf.Clamp(currentConfigIndex, 0, configs.Length - 1);
        currentConfig = configs[currentConfigIndex];
        if (currentConfig == null)
        {
            ClampRuntimeSettings();
            return;
        }

        targetPrefab = currentConfig.targetPrefab;
        spawnAreaBottomLeft = currentConfig.spawnAreaBottomLeft;
        spawnAreaTopRight = currentConfig.spawnAreaTopRight;
        spawnZ = currentConfig.spawnZ;
        spawnInterval = currentConfig.spawnInterval;
        targetLifetime = currentConfig.targetLifetime;
        roundDuration = currentConfig.roundDuration;
        ClampRuntimeSettings();
    }

    private IEnumerator RoundTimerRoutine()
    {
        while (remainingTime > 0f)
        {
            if (isPaused)
            {
                yield return null;
                continue;
            }

            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            RefreshTimeUI();
            onTimeChanged?.Invoke(remainingTime);
            yield return null;
        }

        EndRound();
    }

    private IEnumerator SpawnRoutine()
    {
        while (isPlaying)
        {
            if (isPaused)
            {
                yield return null;
                continue;
            }

            SpawnTarget();
            yield return new WaitForSeconds(Mathf.Max(0.05f, spawnInterval));
        }
    }

    private void ClampRuntimeSettings()
    {
        float minX = Mathf.Min(spawnAreaBottomLeft.x, spawnAreaTopRight.x);
        float maxX = Mathf.Max(spawnAreaBottomLeft.x, spawnAreaTopRight.x);
        float minY = Mathf.Min(spawnAreaBottomLeft.y, spawnAreaTopRight.y);
        float maxY = Mathf.Max(spawnAreaBottomLeft.y, spawnAreaTopRight.y);
        spawnAreaBottomLeft = new Vector2(minX, minY);
        spawnAreaTopRight = new Vector2(maxX, maxY);
        spawnInterval = Mathf.Max(0.05f, spawnInterval);
        targetLifetime = Mathf.Max(0f, targetLifetime);
        roundDuration = Mathf.Max(0f, roundDuration);
    }

    public WhackTarget SpawnTarget()
    {
        if (!isPlaying || targetPrefab == null)
        {
            return null;
        }

        Vector3 spawnPosition = GetRandomSpawnPosition();
        WhackTarget target = Instantiate(targetPrefab, spawnPosition, Quaternion.identity, targetParent);
        target.ResetTarget();
        activeTargets.Add(target);
        StartCoroutine(RemoveTargetAfterLifetime(target));
        return target;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        float x = Random.Range(spawnAreaBottomLeft.x, spawnAreaTopRight.x);
        float y = Random.Range(spawnAreaBottomLeft.y, spawnAreaTopRight.y);
        return new Vector3(x, y, spawnZ);
    }

    private IEnumerator RemoveTargetAfterLifetime(WhackTarget target)
    {
        float lifetime = Mathf.Max(0f, targetLifetime);
        float elapsedTime = 0f;

        while (elapsedTime < lifetime)
        {
            if (target == null || !activeTargets.Contains(target))
            {
                yield break;
            }

            if (!isPaused)
            {
                elapsedTime += Time.deltaTime;
            }

            yield return null;
        }

        if (target == null || !activeTargets.Contains(target))
        {
            yield break;
        }

        ReleaseTarget(target);
        Destroy(target.gameObject);
    }

    private void HandleTargetHit(WhackTarget target)
    {
        if (!isPlaying || isPaused || target == null || !activeTargets.Contains(target))
        {
            return;
        }

        ReleaseTarget(target);
        AddRoundScore(target.scoreValue);
    }

    private void ReleaseTarget(WhackTarget target)
    {
        activeTargets.Remove(target);
    }

    private void SyncHighScoreFromPlayerData()
    {
        highScore = PlayerData.Instance != null ? PlayerData.Instance.score : 0;
    }

    private void SaveHighScoreIfNeeded()
    {
        LogDebug($"SaveHighScore：检查最高分。playerData={(PlayerData.Instance != null ? "存在" : "不存在")}，roundScore={roundScore}，oldHighScore={(PlayerData.Instance != null ? PlayerData.Instance.score.ToString() : "无")}。");
        if (PlayerData.Instance == null || roundScore <= PlayerData.Instance.score)
        {
            LogDebug("SaveHighScore：未刷新最高分。");
            return;
        }

        PlayerData.Instance.SetScore(roundScore);
        highScore = roundScore;
        LogDebug($"SaveHighScore：刷新最高分为 {highScore}。");
    }

    private void OpenSettlementIfReady()
    {
        LogDebug($"Settlement：准备打开结算。settlementUI={(settlementUI != null ? settlementUI.name : "未绑定")}。");
        ClueSO rewardClue = TryGrantRewardClue();
        settlementUI?.OpenSettlement(roundScore, highScore, rewardClue, rewardClue != null);
        LogDebug($"Settlement：结算调用完成。reward={(rewardClue != null ? rewardClue.clueName : "无")}。");
    }

    private ClueSO TryGrantRewardClue()
    {
        LogDebug($"Reward：检查奖励。config={(currentConfig != null ? currentConfig.name : "未绑定")}，playerData={(PlayerData.Instance != null ? "存在" : "不存在")}，roundScore={roundScore}，threshold={(currentConfig != null ? currentConfig.rewardScoreThreshold.ToString() : "无")}。");
        if (currentConfig == null ||
            PlayerData.Instance == null ||
            roundScore <= currentConfig.rewardScoreThreshold ||
            currentConfig.rewardCluePool == null ||
            currentConfig.rewardCluePool.Length == 0)
        {
            LogDebug("Reward：不满足奖励条件或线索池为空。");
            return null;
        }

        ClueSO rewardClue = GetRandomRewardClue();
        if (rewardClue == null)
        {
            LogDebug("Reward：线索池里没有可发放的新线索。");
            return null;
        }

        PlayerData.Instance.CollectClue(rewardClue);
        LogDebug($"Reward：发放线索 {rewardClue.clueName}。");
        return rewardClue;
    }

    private ClueSO GetRandomRewardClue()
    {
        if (currentConfig == null || currentConfig.rewardCluePool == null)
        {
            return null;
        }

        List<ClueSO> availableClues = new List<ClueSO>();
        foreach (ClueSO clue in currentConfig.rewardCluePool)
        {
            if (clue == null)
            {
                continue;
            }

            if (PlayerData.Instance == null || !PlayerData.Instance.HasClue(clue))
            {
                availableClues.Add(clue);
            }
        }

        if (availableClues.Count == 0)
        {
            return null;
        }

        return availableClues[Random.Range(0, availableClues.Count)];
    }

    private void LogDebug(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[WhackGameController] {message}", this);
        }
    }

    private void ClearTargets()
    {
        for (int i = activeTargets.Count - 1; i >= 0; i--)
        {
            WhackTarget target = activeTargets[i];
            if (target != null)
            {
                Destroy(target.gameObject);
            }
        }

        activeTargets.Clear();
    }

    private void StopRoundCoroutines()
    {
        isPaused = false;

        if (roundRoutine != null)
        {
            StopCoroutine(roundRoutine);
            roundRoutine = null;
        }

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private void RefreshScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"分数: {roundScore}";
        }
    }

    private void RefreshTimeUI()
    {
        if (timeText != null)
        {
            timeText.text = $"时间: {Mathf.CeilToInt(remainingTime)}";
        }
    }
}
