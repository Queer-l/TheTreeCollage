using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class StoryManager : MonoBehaviour
{
    private const string CharacterSpriteIndexNotesText =
        "-1：不显示立绘\n" +
        "0：小明\n" +
        "1：父亲\n" +
        "2：母亲\n" +
        "3：陈爷爷/陈工\n" +
        "4：陈奶奶\n" +
        "5：陈春柏\n" +
        "6：高中历史老师\n" +
        "7：卫生所年轻医生\n" +
        "8：疯疯癫癫的道士\n" +
        "9：村民群像";
    [Header("剧情数据")]
    [Tooltip("场景中可跳转的全部剧情节点。StoryManager 会用 nodeID 建立查找表。")]
    public List<StoryNodeSO> allStoryNodes = new List<StoryNodeSO>();

    public StoryNodeSO startNode;
    public StoryNodeSO currentNode;

    [Header("剧情 UI")]
    public UIControl storyUI;
    public Image cgImage;
    public Image characterImage;
    public TextMeshProUGUI speakerText;
    public TextMeshProUGUI dialogueText;
    public List<Button> choiceButtons = new List<Button>();
    public List<TextMeshProUGUI> choiceTexts = new List<TextMeshProUGUI>();

    [Header("立绘资源")]
    [Tooltip("StoryCanvas 上统一维护的人物立绘数组。剧情节点通过 characterImageIndex 从这里取立绘。")]
    public Sprite[] characterSprites = new Sprite[0];

    [Header("立绘索引注释")]
    [TextArea(4, 12)]
    [Tooltip("剧情节点 characterImageIndex 对应的人物立绘编号。")]
    public string characterSpriteIndexNotes = CharacterSpriteIndexNotesText;

    [Header("动态分支选项")]
    [Tooltip("动态生成分支按钮的父物体。只负责承载按钮，不要求它本身带 Image。")]
    public Transform choiceParent;

    [Tooltip("分支按钮预制体。预制体上需要有 Button，子物体中建议有 TextMeshProUGUI 用于显示选项文本。")]
    public Button choiceButtonPrefab;

    [Tooltip("控制选项区域整体显隐的 CanvasGroup。请绑定包含选项、滚动区域和滚动条的最外层对象。")]
    [FormerlySerializedAs("choicePanelImage")]
    [FormerlySerializedAs("choiceParentCanvasGroup")]
    public CanvasGroup choicePanelCanvasGroup;

    [Header("顺序对话按钮")]
    [Tooltip("上一对话按钮。可不绑定；绑定后会在刷新节点时自动设置是否可点击。")]
    public Button previousDialogueButton;

    [Tooltip("下一对话按钮。可不绑定；绑定后会在刷新节点时自动设置是否可点击。")]
    public Button nextDialogueButton;

    [Header("管理器引用")]
    public PlayerManager playerManager;
    public ClueManager clueManager;

    [Header("调试")]
    [Tooltip("开启后在 Console 输出按钮点击相关的剧情日志。")]
    public bool enableDebugLog = true;

    private readonly Dictionary<string, StoryNodeSO> storyNodeLookup = new Dictionary<string, StoryNodeSO>();
    private readonly List<Button> generatedChoiceButtons = new List<Button>();

    private void Awake()
    {
        LogStory("Awake：初始化 StoryManager。");

        if (playerManager == null)
        {
            playerManager = FindObjectOfType<PlayerManager>();
            LogStory($"Awake：自动查找 PlayerManager，结果={(playerManager != null ? "成功" : "失败")}。");
        }

        if (clueManager == null)
        {
            clueManager = FindObjectOfType<ClueManager>();
            LogStory($"Awake：自动查找 ClueManager，结果={(clueManager != null ? "成功" : "失败")}。");
        }

        PrepareChoiceParentCanvasGroup();
        SetChoiceParentVisible(false);
    }

    private void Start()
    {
        LogStory("Start：开始加载剧情节点表。");
        RegisterStoryNodeCatalog();
        LogStory($"Start：剧情节点表加载完成，已注册节点数量={storyNodeLookup.Count}。");

        if (PlayerData.Instance != null && PlayerData.Instance.returningFromLittleGame)
        {
            PlayerData.Instance.returningFromLittleGame = false;
            currentNode = GetStoryModeEntryNode(false);
            LogButtonClick($"Start：检测到从小游戏返回，跳过自动打开剧情。currentNode={(currentNode != null ? currentNode.name : "空")}。");
            return;
        }

        StoryNodeSO savedNode = GetSavedStoryNode();
        if (savedNode != null && currentNode == null)
        {
            LogStory($"Start：准备恢复玩家剧情进度。asset={savedNode.name}，nodeID={FormatLogValue(savedNode.nodeID)}。");
            ShowNode(savedNode, false);
            return;
        }

        if (startNode != null && currentNode == null)
        {
            LogStory($"Start：准备显示起始节点。asset={startNode.name}，nodeID={FormatLogValue(startNode.nodeID)}。");
            ShowNode(startNode);
        }
        else
        {
            LogStory($"Start：跳过自动显示起始节点。startNode={(startNode != null ? startNode.name : "空")}，currentNode={(currentNode != null ? currentNode.name : "空")}。");
        }
    }

    private StoryNodeSO GetSavedStoryNode()
    {
        if (PlayerData.Instance == null || string.IsNullOrWhiteSpace(PlayerData.Instance.currentStoryNodeId))
        {
            return null;
        }

        TryGetKnownStoryNode(PlayerData.Instance.currentStoryNodeId, out StoryNodeSO savedNode, "Start：读取玩家保存的剧情节点");
        return savedNode;
    }

    public void ShowNode(StoryNodeSO node)
    {
        ShowNode(node, true);
    }

    private void ShowNode(StoryNodeSO node, bool recordHistory)
    {
        if (node == null)
        {
            LogStory("ShowNode：传入节点为空，停止显示。");
            return;
        }

        LogStory($"ShowNode：显示节点。asset={node.name}，nodeID={FormatLogValue(node.nodeID)}，recordHistory={recordHistory}。");
        RegisterKnownNodes(node);
        currentNode = node;

        if (PlayerData.Instance != null)
        {
            if (recordHistory)
            {
                PlayerData.Instance.RecordStoryNodeVisit(node.nodeID);
                LogStory($"ShowNode：记录剧情历史。currentStoryNodeId={FormatLogValue(PlayerData.Instance.currentStoryNodeId)}，historyIndex={PlayerData.Instance.storyHistoryIndex}，historyCount={PlayerData.Instance.visitedStoryNodeIds.Count}。");
            }
            else
            {
                PlayerData.Instance.currentStoryNodeId = node.nodeID;
                LogStory($"ShowNode：历史回看显示，不新增历史。currentStoryNodeId={FormatLogValue(PlayerData.Instance.currentStoryNodeId)}。");
            }

            PlayerData.Instance.AddStoryFlag(node.flagOnEnter);
            LogStory($"ShowNode：尝试写入进入节点 flag={FormatLogValue(node.flagOnEnter)}。");

            if (node.rewardClue != null)
            {
                PlayerData.Instance.CollectClue(node.rewardClue);
                LogStory($"ShowNode：解锁节点奖励线索。clue={node.rewardClue.clueName}，clueId={node.rewardClue.clueId}。");
                clueManager?.RefreshData();
                playerManager?.UpdatePlayerUI();
            }
        }
        else
        {
            LogStory("ShowNode：PlayerData.Instance 为空，跳过剧情状态记录。");
        }

        SetText(speakerText, node.speakerName);
        SetText(dialogueText, node.dialogueText);
        SetImage(cgImage, node.cgImage);
        SetImage(characterImage, GetCharacterSprite(node.characterImageIndex));
        RefreshChoices(node);
        RefreshDialogueNavigation(node);

        if (storyUI != null)
        {
            storyUI.Open();
            storyUI.BringToFront();
            LogStory("ShowNode：剧情 UI 已打开并置顶。");
        }
    }

    public void Choose(int choiceIndex)
    {
        LogButtonClick($"Choose：收到选项选择请求。choiceIndex={choiceIndex}，currentNode={(currentNode != null ? currentNode.name : "空")}，currentNodeID={(currentNode != null ? FormatLogValue(currentNode.nodeID) : "空")}，choiceCount={(currentNode != null && currentNode.choices != null ? currentNode.choices.Count : 0)}。");

        if (currentNode == null)
        {
            LogButtonClick($"Choose：选择失败，currentNode 为空。choiceIndex={choiceIndex}。");
            return;
        }

        if (currentNode.choices == null)
        {
            LogButtonClick($"Choose：选择失败，当前节点 choices 为空。nodeID={FormatLogValue(currentNode.nodeID)}，choiceIndex={choiceIndex}。");
            return;
        }

        if (choiceIndex < 0 || choiceIndex >= currentNode.choices.Count)
        {
            LogButtonClick($"Choose：选择失败，choiceIndex 超出范围。choiceIndex={choiceIndex}，choiceCount={currentNode.choices.Count}。");
            return;
        }

        StoryChoice choice = currentNode.choices[choiceIndex];
        LogButtonChoiceState("Choose：点击选项对应配置", choiceIndex, choice);

        if (!CanChoose(choice))
        {
            LogButtonClick($"Choose：选择失败，选项条件不足。choiceIndex={choiceIndex}，requiredClue={FormatClueLogValue(choice.requiredClue)}，requiredFlag={FormatLogValue(choice.requiredFlag)}。");
            playerManager?.ShowTip();
            return;
        }

        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.AddStoryFlag(choice.flagOnChoose);
            LogButtonClick($"Choose：写入选择后 flag。flagOnChoose={FormatLogValue(choice.flagOnChoose)}。");
        }
        else
        {
            LogButtonClick("Choose：PlayerData.Instance 为空，跳过选择后 flag 写入。");
        }

        StoryNodeSO nextNode = ResolveStoryNode(choice.nextNodeID, $"Choose：分支选项 index={choiceIndex}");
        if (nextNode == null)
        {
            Debug.LogWarning($"分支“{choice.choiceText}”没有配置有效的跳转节点 ID。", this);
            LogButtonClick($"Choose：选择失败，目标剧情节点不存在。choiceIndex={choiceIndex}，choiceText={FormatLogValue(choice.choiceText)}，targetID={FormatLogValue(choice.nextNodeID)}。");
            playerManager?.ShowTip();
            return;
        }

        LogButtonClick($"Choose：分支跳转。choiceIndex={choiceIndex}，choiceText={FormatLogValue(choice.choiceText)}，targetID={FormatLogValue(choice.nextNodeID)}。");
        ShowNode(nextNode);
    }

    /// <summary>
    /// 给“上一对话”按钮绑定的入口。
    /// 优先读取 PlayerData 中记录的剧情历史。
    /// 如果上一条历史对应的是分支节点，则不跳转并输出“无法跳转”。
    /// </summary>
    public void ShowPreviousDialogue()
    {
        LogButtonClick("Previous：点击上一对话按钮。");

        if (IsBranchNode(currentNode))
        {
            LogButtonClick($"Previous：当前节点是分支节点，禁止上一对话。nodeID={FormatLogValue(currentNode.nodeID)}。");
            return;
        }

        if (PlayerData.Instance == null ||
            !PlayerData.Instance.TryPeekPreviousStoryNodeId(out string previousNodeID) ||
            !TryGetKnownStoryNode(previousNodeID, out StoryNodeSO previousNode, "Previous：查询上一条历史节点"))
        {
            LogButtonClick("Previous：没有上一条历史，或历史 ID 无法解析为节点。");
            return;
        }

        if (IsBranchNode(previousNode))
        {
            Debug.Log("无法跳转");
            LogButtonClick($"Previous：上一条历史是分支节点，禁止跳转。nodeID={FormatLogValue(previousNodeID)}。");
            return;
        }

        if (PlayerData.Instance.TryMoveToPreviousStoryNode(out _))
        {
            LogButtonClick($"Previous：跳转到上一条历史节点。nodeID={FormatLogValue(previousNodeID)}，historyIndex={PlayerData.Instance.storyHistoryIndex}。");
            ShowNode(previousNode, false);
        }
    }

    /// <summary>
    /// 给“下一对话”按钮绑定的入口。
    /// 如果当前处于回看历史状态，优先跳转到下一条历史；否则跳转到当前节点配置的 nextNodeID。
    /// </summary>
    public void ShowNextDialogue()
    {
        LogButtonClick("Next：点击下一对话按钮。");

        if (IsBranchNode(currentNode))
        {
            LogButtonClick($"Next：当前节点是分支节点，禁止下一对话。nodeID={FormatLogValue(currentNode.nodeID)}。");
            return;
        }

        if (PlayerData.Instance != null &&
            PlayerData.Instance.TryPeekNextStoryNodeId(out string nextHistoryNodeID) &&
            TryGetKnownStoryNode(nextHistoryNodeID, out StoryNodeSO nextHistoryNode, "Next：查询历史下一条节点"))
        {
            LogButtonClick($"Next：当前在历史回看中，优先跳转到历史下一条。nodeID={FormatLogValue(nextHistoryNodeID)}。");
            if (PlayerData.Instance.TryMoveToNextStoryNode(out _))
            {
                LogButtonClick($"Next：历史索引前进。historyIndex={PlayerData.Instance.storyHistoryIndex}。");
                ShowNode(nextHistoryNode, false);
            }

            return;
        }

        if (currentNode != null && currentNode.returnToNormalModeOnNext)
        {
            LogButtonClick($"Next：当前节点配置为幕末返回普通模式。nodeID={FormatLogValue(currentNode.nodeID)}。");
            MoveCurrentNodeToNextForNormalMode();
            ReturnToNormalMode();
            return;
        }

        LogButtonClick($"Next：解析顺序下一节点。currentNode={(currentNode != null ? currentNode.name : "空")}，nextNodeID={FormatLogValue(currentNode?.nextNodeID)}。");
        StoryNodeSO nextNode = ResolveStoryNode(currentNode?.nextNodeID, "Next：查询顺序下一节点");
        if (nextNode == null)
        {
            LogButtonClick("Next：没有找到顺序下一节点，停止跳转。");
            return;
        }

        LogButtonClick($"Next：跳转到顺序下一节点。nodeID={FormatLogValue(nextNode.nodeID)}。");
        ShowNode(nextNode);
    }

    /// <summary>
    /// ShowPreviousDialogue 的简短别名，方便在 Button OnClick 中查找。
    /// </summary>
    public void PreviousDialogue()
    {
        ShowPreviousDialogue();
    }

    /// <summary>
    /// ShowNextDialogue 的简短别名，方便在 Button OnClick 中查找。
    /// </summary>
    public void NextDialogue()
    {
        ShowNextDialogue();
    }

    /// <summary>
    /// 普通模式是否可以重新打开剧情界面。
    /// 供 DayManager 在“进入下一天”前判断当前是否有可显示的剧情进度。
    /// </summary>
    public bool CanEnterStoryModeFromNormalMode()
    {
        if (storyUI != null && storyUI.isOpen)
        {
            return false;
        }

        return GetStoryModeEntryNode(false) != null;
    }

    /// <summary>
    /// 从普通模式回到当前剧情进度。
    /// </summary>
    public void EnterStoryMode()
    {
        StoryNodeSO entryNode = GetStoryModeEntryNode(true);
        if (entryNode == null)
        {
            LogButtonClick("StoryMode：无法进入剧情模式，没有可显示的剧情节点。");
            return;
        }

        LogButtonClick($"StoryMode：从普通模式进入剧情。nodeID={FormatLogValue(entryNode.nodeID)}。");
        ShowNode(entryNode, false);
    }

    /// <summary>
    /// 按钮入口：刷新当前剧情 UI 内容，并打开剧情界面。
    /// </summary>
    public void RefreshAndOpenStoryUI()
    {
        StoryNodeSO entryNode = GetStoryModeEntryNode(true);
        if (entryNode == null)
        {
            LogButtonClick("StoryUI：无法刷新并打开，没有可显示的剧情节点。");
            return;
        }

        LogButtonClick($"StoryUI：刷新并打开剧情界面。nodeID={FormatLogValue(entryNode.nodeID)}。");
        ShowNode(entryNode, false);
    }

    /// <summary>
    /// 关闭剧情 UI，回到普通模式。
    /// 可用于每一幕最后一个剧情节点，或外部按钮直接绑定。
    /// </summary>
    public void ReturnToNormalMode()
    {
        ClearGeneratedChoices();
        SetChoiceParentVisible(false);

        if (storyUI != null)
        {
            storyUI.Close();
            LogButtonClick("StoryMode：已关闭 Story UI，返回普通模式。");
        }
        else
        {
            LogButtonClick("StoryMode：无法返回普通模式，storyUI 未绑定。");
        }
    }

    private void MoveCurrentNodeToNextForNormalMode()
    {
        StoryNodeSO nextNode = ResolveStoryNode(currentNode?.nextNodeID, "Next：幕末返回普通模式前推进当前节点");
        if (nextNode == null)
        {
            LogButtonClick("Next：幕末节点没有有效的下一节点，保持当前剧情进度后返回普通模式。");
            return;
        }

        RegisterKnownNodes(nextNode);
        currentNode = nextNode;

        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.currentStoryNodeId = nextNode.nodeID;
        }

        LogButtonClick($"Next：幕末返回普通模式前已推进当前节点。nodeID={FormatLogValue(nextNode.nodeID)}。");
    }

    private StoryNodeSO GetStoryModeEntryNode(bool logWarnings)
    {
        if (storyNodeLookup.Count == 0)
        {
            RegisterStoryNodeCatalog();
        }

        if (currentNode != null)
        {
            return currentNode;
        }

        if (PlayerData.Instance != null &&
            !string.IsNullOrWhiteSpace(PlayerData.Instance.currentStoryNodeId) &&
            TryGetKnownStoryNode(PlayerData.Instance.currentStoryNodeId, out StoryNodeSO savedNode, "StoryMode：读取玩家当前剧情进度"))
        {
            return savedNode;
        }

        if (startNode != null)
        {
            return startNode;
        }

        if (logWarnings)
        {
            Debug.LogWarning("无法进入剧情模式：未配置当前剧情节点或起始剧情节点。", this);
        }

        return null;
    }

    private void RefreshChoices(StoryNodeSO node)
    {
        int choiceCount = node != null && node.choices != null ? node.choices.Count : 0;
        bool hasChoices = choiceCount > 0;
        bool useDynamicChoices = hasChoices && choiceParent != null && choiceButtonPrefab != null;

        LogStory($"Choices：开始刷新当前节点选项。asset={(node != null ? node.name : "空")}，nodeID={(node != null ? FormatLogValue(node.nodeID) : "空")}，choiceCount={choiceCount}，useDynamicChoices={useDynamicChoices}，choiceParent={(choiceParent != null ? choiceParent.name : "未绑定")}，choiceButtonPrefab={(choiceButtonPrefab != null ? choiceButtonPrefab.name : "未绑定")}，preplacedButtonCount={choiceButtons.Count}。");

        if (node != null && node.choices != null)
        {
            for (int i = 0; i < node.choices.Count; i++)
            {
                LogChoiceState("Choices：读取当前节点选项配置", i, node.choices[i]);
            }
        }

        ClearGeneratedChoices();

        SetChoiceParentVisible(hasChoices);
        LogStory($"Choices：分支父物体显示状态刷新完成。hasChoices={hasChoices}。");

        if (!hasChoices)
        {
            LogStory("Choices：当前节点没有分支选项，切换到预放按钮刷新以隐藏旧按钮。");
            RefreshPreplacedChoices(node);
            return;
        }

        if (choiceParent != null && choiceButtonPrefab != null)
        {
            LogStory("Choices：检测到动态生成配置完整，开始使用 prefab 生成选项。");
            HidePreplacedChoices();
            GenerateChoiceButtons(node);
            return;
        }

        LogStory("Choices：动态生成配置不完整，使用预放按钮显示选项。");
        RefreshPreplacedChoices(node);
    }

    private void GenerateChoiceButtons(StoryNodeSO node)
    {
        if (node == null || node.choices == null)
        {
            LogStory("Choices：动态生成失败，节点或选项列表为空。");
            return;
        }

        if (choiceParent == null || choiceButtonPrefab == null)
        {
            LogStory($"Choices：动态生成失败。choiceParent={(choiceParent != null ? choiceParent.name : "未绑定")}，choiceButtonPrefab={(choiceButtonPrefab != null ? choiceButtonPrefab.name : "未绑定")}。");
            return;
        }

        LogStory($"Choices：开始动态生成按钮。nodeID={FormatLogValue(node.nodeID)}，choiceCount={node.choices.Count}，parent={choiceParent.name}，prefab={choiceButtonPrefab.name}。");

        for (int i = 0; i < node.choices.Count; i++)
        {
            Button button = Instantiate(choiceButtonPrefab, choiceParent);
            generatedChoiceButtons.Add(button);
            LogStory($"Choices：动态按钮实例化完成。index={i}，button={(button != null ? button.name : "空")}，generatedCount={generatedChoiceButtons.Count}。");
            ConfigureChoiceButton(button, node.choices[i], i);
        }

        LogStory($"Choices：动态生成结束。生成按钮数量={generatedChoiceButtons.Count}。");
    }

    private void RefreshPreplacedChoices(StoryNodeSO node)
    {
        int choiceCount = node != null && node.choices != null ? node.choices.Count : 0;
        LogStory($"Choices：开始刷新预放按钮。choiceCount={choiceCount}，buttonCount={choiceButtons.Count}，choiceTextCount={choiceTexts.Count}。");

        for (int i = 0; i < choiceButtons.Count; i++)
        {
            bool hasChoice = node != null && node.choices != null && i < node.choices.Count;
            Button button = choiceButtons[i];

            if (button == null)
            {
                LogStory($"Choices：预放按钮为空，跳过。index={i}，hasChoice={hasChoice}。");
                continue;
            }

            button.gameObject.SetActive(hasChoice);
            button.onClick.RemoveAllListeners();
            LogStory($"Choices：预放按钮状态刷新。index={i}，button={button.name}，active={hasChoice}。");

            if (!hasChoice)
            {
                continue;
            }

            TextMeshProUGUI label = i < choiceTexts.Count ? choiceTexts[i] : null;
            LogStory($"Choices：准备绑定预放按钮。index={i}，label={(label != null ? label.name : "未绑定，将自动查找")}。");
            ConfigureChoiceButton(button, node.choices[i], i, label);
        }

        LogStory("Choices：预放按钮刷新结束。");
    }

    private void HidePreplacedChoices()
    {
        LogStory($"Choices：隐藏预放按钮。buttonCount={choiceButtons.Count}。");

        for (int i = 0; i < choiceButtons.Count; i++)
        {
            Button button = choiceButtons[i];
            if (button != null)
            {
                button.gameObject.SetActive(false);
                LogStory($"Choices：已隐藏预放按钮。index={i}，button={button.name}。");
            }
            else
            {
                LogStory($"Choices：预放按钮为空，无法隐藏。index={i}。");
            }
        }
    }

    private void ConfigureChoiceButton(Button button, StoryChoice choice, int index, TextMeshProUGUI label = null)
    {
        if (button == null)
        {
            LogStory($"Choices：绑定按钮失败，Button 为空。index={index}。");
            return;
        }

        if (choice == null)
        {
            LogStory($"Choices：绑定按钮失败，StoryChoice 为空。index={index}，button={button.name}。");
            return;
        }

        bool canChoose = CanChoose(choice);
        bool hasTarget = HasStoryNodeTarget(choice.nextNodeID, $"Choices：检查按钮目标节点 index={index}");

        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();
        button.interactable = canChoose && hasTarget;
        button.onClick.AddListener(() => OnChoiceButtonClicked(index));
        LogStory($"Choices：点击事件绑定完成。index={index}，button={button.name}，targetMethod=OnChoiceButtonClicked。");

        if (label == null)
        {
            label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            LogStory($"Choices：按钮文本未手动传入，自动查找结果={(label != null ? label.name : "未找到")}。index={index}，button={button.name}。");
        }

        SetText(label, choice.choiceText);
        LogChoiceState("Choices：按钮绑定完成", index, choice);
        LogStory($"Choices：按钮最终状态。index={index}，button={button.name}，interactable={button.interactable}，canChoose={canChoose}，hasTarget={hasTarget}，label={(label != null ? label.name : "空")}，displayText={FormatLogValue(choice.choiceText)}。");
    }

    private void OnChoiceButtonClicked(int choiceIndex)
    {
        LogButtonClick($"Click：选项按钮被点击。choiceIndex={choiceIndex}，currentNode={(currentNode != null ? currentNode.name : "空")}，currentNodeID={(currentNode != null ? FormatLogValue(currentNode.nodeID) : "空")}。");
        Choose(choiceIndex);
    }

    private void ClearGeneratedChoices()
    {
        LogStory($"Choices：清理动态生成按钮。count={generatedChoiceButtons.Count}。");

        for (int i = 0; i < generatedChoiceButtons.Count; i++)
        {
            Button button = generatedChoiceButtons[i];
            if (button != null)
            {
                LogStory($"Choices：销毁动态按钮。index={i}，button={button.name}。");
                Destroy(button.gameObject);
            }
            else
            {
                LogStory($"Choices：动态按钮引用为空，跳过销毁。index={i}。");
            }
        }

        generatedChoiceButtons.Clear();
        LogStory("Choices：动态按钮清理完成。");
    }

    private void PrepareChoiceParentCanvasGroup()
    {
        if (choicePanelCanvasGroup != null)
        {
            LogStory($"Choices：选项 CanvasGroup 已绑定。canvasGroup={choicePanelCanvasGroup.name}。");
            return;
        }

        if (choiceParent == null)
        {
            LogStory("Choices：未绑定 choiceParent，无法自动获取选项 CanvasGroup。");
            return;
        }

        choicePanelCanvasGroup = choiceParent.GetComponent<CanvasGroup>();
        LogStory($"Choices：从 choiceParent 自动获取 CanvasGroup。choiceParent={choiceParent.name}，result={(choicePanelCanvasGroup != null ? choicePanelCanvasGroup.name : "未找到")}。");
    }

    private void SetChoiceParentVisible(bool visible)
    {
        PrepareChoiceParentCanvasGroup();

        if (choicePanelCanvasGroup == null)
        {
            LogStory($"Choices：设置选项区域显隐失败，CanvasGroup 未绑定。visible={visible}。");
            return;
        }

        choicePanelCanvasGroup.alpha = visible ? 1f : 0f;
        choicePanelCanvasGroup.interactable = visible;
        choicePanelCanvasGroup.blocksRaycasts = visible;
        LogStory($"Choices：设置选项区域显隐完成。canvasGroup={choicePanelCanvasGroup.name}，visible={visible}，alpha={choicePanelCanvasGroup.alpha}，interactable={choicePanelCanvasGroup.interactable}，blocksRaycasts={choicePanelCanvasGroup.blocksRaycasts}。");
    }

    private void RefreshDialogueNavigation(StoryNodeSO node)
    {
        bool isBranchNode = IsBranchNode(node);

        if (previousDialogueButton != null)
        {
            previousDialogueButton.interactable = !isBranchNode &&
                                                   PlayerData.Instance != null &&
                                                   PlayerData.Instance.TryPeekPreviousStoryNodeId(out _);
        }

        if (nextDialogueButton != null)
        {
            bool hasNextHistory = PlayerData.Instance != null && PlayerData.Instance.TryPeekNextStoryNodeId(out _);
            bool hasNextNode = HasStoryNodeTarget(node?.nextNodeID, "Navigation：检查下一对话按钮目标节点");
            bool canReturnToNormalMode = node != null && node.returnToNormalModeOnNext;
            nextDialogueButton.interactable = !isBranchNode && (hasNextHistory || hasNextNode || canReturnToNormalMode);
        }
    }

    private void RegisterStoryNodeCatalog()
    {
        storyNodeLookup.Clear();
        LogStory($"LoadNodes：清空旧节点表，开始加载 allStoryNodes。列表数量={allStoryNodes.Count}。");

        for (int i = 0; i < allStoryNodes.Count; i++)
        {
            StoryNodeSO node = allStoryNodes[i];
            LogStory($"LoadNodes：读取 allStoryNodes[{i}]，asset={(node != null ? node.name : "空")}，nodeID={(node != null ? FormatLogValue(node.nodeID) : "空")}。");
            RegisterKnownNodes(node);
        }

        LogStory($"LoadNodes：额外注册 startNode。asset={(startNode != null ? startNode.name : "空")}，nodeID={(startNode != null ? FormatLogValue(startNode.nodeID) : "空")}。");
        RegisterKnownNodes(startNode);

        LogStory($"LoadNodes：额外注册 currentNode。asset={(currentNode != null ? currentNode.name : "空")}，nodeID={(currentNode != null ? FormatLogValue(currentNode.nodeID) : "空")}。");
        RegisterKnownNodes(currentNode);

        LogStory($"LoadNodes：节点表加载结束。有效节点数量={storyNodeLookup.Count}。");
    }

    private void RegisterKnownNodes(StoryNodeSO node)
    {
        RegisterKnownNode(node);

        if (node == null)
        {
            return;
        }

        // 跳转目标全部通过 ID 从 allStoryNodes 查询，不再通过 SO 引用兜底注册。
    }

    private void RegisterKnownNode(StoryNodeSO node)
    {
        if (node == null)
        {
            LogStory("LoadNodes：跳过空节点。");
            return;
        }

        if (string.IsNullOrWhiteSpace(node.nodeID))
        {
            LogStory($"LoadNodes：节点 ID 为空，跳过注册。asset={node.name}。");
            return;
        }

        if (storyNodeLookup.TryGetValue(node.nodeID, out StoryNodeSO oldNode) && oldNode != node)
        {
            LogStory($"LoadNodes：发现重复 nodeID={node.nodeID}。旧节点={oldNode.name}，新节点={node.name}，将使用新节点覆盖。");
        }

        storyNodeLookup[node.nodeID] = node;
        LogStory($"LoadNodes：注册节点成功。nodeID={node.nodeID}，asset={node.name}。");
    }

    private StoryNodeSO ResolveStoryNode(string nodeID, string source = "未指定来源")
    {
        LogStory($"ResolveNode：开始按 ID 解析节点。source={source}，nodeID={FormatLogValue(nodeID)}，lookupCount={storyNodeLookup.Count}。");

        if (!string.IsNullOrWhiteSpace(nodeID))
        {
            if (TryGetKnownStoryNode(nodeID, out StoryNodeSO node, $"ResolveNode/{source}"))
            {
                LogStory($"ResolveNode：按 ID 解析成功。source={source}，nodeID={nodeID}，asset={node.name}。");
                return node;
            }

            Debug.LogWarning($"找不到剧情节点 ID: {nodeID}", this);
            LogStory($"ResolveNode：按 ID 解析失败。source={source}，nodeID={nodeID} 不在节点表中。");
        }
        else
        {
            LogStory($"ResolveNode：nodeID 为空，无法解析。source={source}。");
        }

        return null;
    }

    private bool HasStoryNodeTarget(string nodeID, string source = "未指定来源")
    {
        LogStory($"LookupNode：检查目标节点是否存在。source={source}，nodeID={FormatLogValue(nodeID)}。");

        if (string.IsNullOrWhiteSpace(nodeID))
        {
            LogStory($"LookupNode：目标节点检查失败，nodeID 为空。source={source}。");
            return false;
        }

        bool result = TryGetKnownStoryNode(nodeID, out StoryNodeSO node, $"HasStoryNodeTarget/{source}");
        LogStory($"LookupNode：目标节点检查结束。source={source}，nodeID={nodeID}，exists={result}，asset={(node != null ? node.name : "空")}。");
        return result;
    }

    private bool TryGetKnownStoryNode(string nodeID, out StoryNodeSO node, string source = "未指定来源")
    {
        node = null;
        LogStory($"LookupNode：开始查询节点表。source={source}，nodeID={FormatLogValue(nodeID)}，lookupCount={storyNodeLookup.Count}。");

        if (string.IsNullOrWhiteSpace(nodeID))
        {
            LogStory($"LookupNode：查询失败，nodeID 为空。source={source}。");
            return false;
        }

        bool hasKey = storyNodeLookup.TryGetValue(nodeID, out node);
        if (!hasKey)
        {
            LogStory($"LookupNode：查询失败，节点表中没有该 ID。source={source}，nodeID={nodeID}。");
            return false;
        }

        if (node == null)
        {
            LogStory($"LookupNode：查询失败，该 ID 对应的节点引用为空。source={source}，nodeID={nodeID}。");
            return false;
        }

        LogStory($"LookupNode：查询成功。source={source}，nodeID={nodeID}，asset={node.name}。");
        return true;
    }

    private void LogStory(string message)
    {
        // 普通剧情流程日志已关闭，只保留按钮点击相关日志。
    }

    private void LogButtonClick(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[StoryManager] {message}", this);
        }
    }

    private static string FormatLogValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "空" : value;
    }

    private void LogChoiceState(string prefix, int index, StoryChoice choice)
    {
        if (choice == null)
        {
            LogStory($"{prefix}。index={index}，choice=空。");
            return;
        }

        LogStory($"{prefix}。index={index}，text={FormatLogValue(choice.choiceText)}，nextNodeID={FormatLogValue(choice.nextNodeID)}，requiredClue={FormatClueLogValue(choice.requiredClue)}，requiredFlag={FormatLogValue(choice.requiredFlag)}，flagOnChoose={FormatLogValue(choice.flagOnChoose)}，canChoose={CanChoose(choice)}，hasTarget={HasStoryNodeTarget(choice.nextNodeID, $"{prefix} index={index}")}。");
    }

    private void LogButtonChoiceState(string prefix, int index, StoryChoice choice)
    {
        if (choice == null)
        {
            LogButtonClick($"{prefix}。index={index}，choice=空。");
            return;
        }

        LogButtonClick($"{prefix}。index={index}，text={FormatLogValue(choice.choiceText)}，nextNodeID={FormatLogValue(choice.nextNodeID)}，requiredClue={FormatClueLogValue(choice.requiredClue)}，requiredFlag={FormatLogValue(choice.requiredFlag)}，flagOnChoose={FormatLogValue(choice.flagOnChoose)}，canChoose={CanChoose(choice)}，hasTarget={HasStoryNodeTarget(choice.nextNodeID, $"{prefix} index={index}")}。");
    }

    private static string FormatClueLogValue(ClueSO clue)
    {
        return clue == null ? "无" : $"{clue.clueName}(id={clue.clueId})";
    }

    private static bool IsBranchNode(StoryNodeSO node)
    {
        return node != null && node.choices != null && node.choices.Count > 0;
    }

    private bool CanChoose(StoryChoice choice)
    {
        if (choice == null)
        {
            return false;
        }

        if (PlayerData.Instance == null)
        {
            return true;
        }

        bool hasRequiredClue = choice.requiredClue == null || PlayerData.Instance.HasClue(choice.requiredClue);
        bool hasRequiredFlag = string.IsNullOrWhiteSpace(choice.requiredFlag) || PlayerData.Instance.HasStoryFlag(choice.requiredFlag);
        return hasRequiredClue && hasRequiredFlag;
    }

    private Sprite GetCharacterSprite(int characterImageIndex)
    {
        if (characterImageIndex < 0)
        {
            return null;
        }

        if (characterSprites == null || characterImageIndex >= characterSprites.Length)
        {
            return null;
        }

        return characterSprites[characterImageIndex];
    }

    private static void SetImage(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
        {
            label.text = value;
        }
    }
}
