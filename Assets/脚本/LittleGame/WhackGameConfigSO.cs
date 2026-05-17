using UnityEngine;

/// <summary>
/// 打地鼠类小游戏的一套难度配置。
/// </summary>
[CreateAssetMenu(fileName = "New Whack Game Config", menuName = "Game/Little Game/Whack Game Config")]
public class WhackGameConfigSO : ScriptableObject
{
    [Header("难度信息")]
    [Tooltip("难度显示名称，例如：简单、普通、困难。")]
    public string difficultyName = "普通";

    [Header("生成设置")]
    [Tooltip("该难度使用的被点击物品预制体。预制体上建议挂 WhackTarget。")]
    public WhackTarget targetPrefab;

    [Tooltip("目标生成区域的左下角世界坐标。")]
    public Vector2 spawnAreaBottomLeft = new Vector2(-4f, -3f);

    [Tooltip("目标生成区域的右上角世界坐标。")]
    public Vector2 spawnAreaTopRight = new Vector2(4f, 3f);

    [Tooltip("生成目标使用的 Z 坐标。2D 游戏通常保持为 0。")]
    public float spawnZ = 0f;

    [Tooltip("每隔多少秒生成一个目标。")]
    public float spawnInterval = 1f;

    [Tooltip("每个目标如果没被点击，最多存在多少秒。")]
    public float targetLifetime = 1.5f;

    [Header("当局设置")]
    [Tooltip("每局总时间，单位秒。")]
    public float roundDuration = 30f;

    [Header("奖励设置")]
    [Tooltip("当局分数大于等于该值时，可以获得随机线索奖励。")]
    public int rewardScoreThreshold = 10;

    [Tooltip("达到奖励分数后，从该线索池随机获得一条线索。")]
    public ClueSO[] rewardCluePool = new ClueSO[0];
}
