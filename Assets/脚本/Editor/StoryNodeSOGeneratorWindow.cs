using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class StoryNodeSOGeneratorWindow : EditorWindow
{
    private const string DefaultFolderPath = "Assets/SOS/\u5267\u60c5so/\u4e3b\u7ebf\u5267\u60c5";
    private const string DefaultRootFolderPath = "Assets/SOS/\u5267\u60c5so";
    private const string DefaultStoryFolderName = "\u5267\u60c5so";
    private const string DefaultMainStoryFolderName = "\u4e3b\u7ebf\u5267\u60c5";
    private const string WindowTitle = "Story Node SO Generator";

    private DefaultAsset targetFolder;
    private string nodeID = "1001";
    private string speakerName = "\u65c1\u767d";
    private string dialogueText = string.Empty;
    private Sprite cgImage;
    private int characterImageIndex = 10;
    private string nextNodeID = string.Empty;
    private bool returnToNormalModeOnNext;
    private ClueSO rewardClue;
    private string flagOnEnter = string.Empty;
    private bool isEnding;
    private EndingType endingType = EndingType.None;
    private bool selectCreatedAsset = true;
    private readonly List<StoryChoiceDraft> choiceDrafts = new List<StoryChoiceDraft>();
    private Vector2 scrollPosition;

    [MenuItem("Tools/Story Node SO Generator")]
    public static void OpenWindow()
    {
        GetWindow<StoryNodeSOGeneratorWindow>(WindowTitle);
    }

    [MenuItem("Assets/Create Story Node SO In Selected Folder", priority = 21)]
    public static void CreateSingleStoryNodeSOInSelectedFolder()
    {
        string folderPath = GetSelectedFolderPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            folderPath = EnsureDefaultFolder();
        }

        string newNodeID = GetNextStoryNodeIdInFolder(folderPath);
        StoryNodeSO node = CreateStoryNodeAsset(
            folderPath,
            newNodeID,
            "\u65c1\u767d",
            string.Empty,
            null,
            -1,
            string.Empty,
            false,
            null,
            string.Empty,
            false,
            EndingType.None,
            new List<StoryChoiceDraft>(),
            true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = node;
        EditorGUIUtility.PingObject(node);
        Debug.Log($"Created StoryNodeSO: {AssetDatabase.GetAssetPath(node)}");
    }

    private void OnEnable()
    {
        string folderPath = EnsureDefaultFolder();
        targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
        nodeID = GetNextStoryNodeIdInFolder(folderPath);
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("Target Folder", targetFolder, typeof(DefaultAsset), false);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Selected Folder"))
            {
                string selectedFolder = GetSelectedFolderPath();
                if (!string.IsNullOrWhiteSpace(selectedFolder))
                {
                    targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(selectedFolder);
                    nodeID = GetNextStoryNodeIdInFolder(selectedFolder);
                }
                else
                {
                    Debug.LogWarning("No folder is selected in the Project panel.");
                }
            }

            if (GUILayout.Button("Use Default Folder"))
            {
                string folderPath = EnsureDefaultFolder();
                targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
                nodeID = GetNextStoryNodeIdInFolder(folderPath);
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Node Info", EditorStyles.boldLabel);
        nodeID = EditorGUILayout.TextField("Node ID", nodeID);
        speakerName = EditorGUILayout.TextField("Speaker Name", speakerName);
        EditorGUILayout.LabelField("Dialogue Text");
        dialogueText = EditorGUILayout.TextArea(dialogueText, GUILayout.MinHeight(90f));

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Visuals", EditorStyles.boldLabel);
        cgImage = (Sprite)EditorGUILayout.ObjectField("CG Image", cgImage, typeof(Sprite), false);
        characterImageIndex = EditorGUILayout.IntField("Character Image Index", characterImageIndex);
        EditorGUILayout.HelpBox("-1: none, 0: Xiao Ming, 1: Father, 2: Mother, 3: Chen Grandpa, 4: Chen Grandma, 5: Chen Chunbai, 6: History Teacher, 7: Doctor, 8: Taoist, 9: Villagers, 10: Narrator, 11: Tree God / Old God", MessageType.None);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Flow", EditorStyles.boldLabel);
        nextNodeID = EditorGUILayout.TextField("Next Node ID", nextNodeID);
        returnToNormalModeOnNext = EditorGUILayout.Toggle("Return To Normal On Next", returnToNormalModeOnNext);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Effects", EditorStyles.boldLabel);
        rewardClue = (ClueSO)EditorGUILayout.ObjectField("Reward Clue", rewardClue, typeof(ClueSO), false);
        flagOnEnter = EditorGUILayout.TextField("Flag On Enter", flagOnEnter);
        isEnding = EditorGUILayout.Toggle("Is Ending", isEnding);
        using (new EditorGUI.DisabledScope(!isEnding))
        {
            endingType = (EndingType)EditorGUILayout.EnumPopup("Ending Type", endingType);
        }

        DrawChoices();

        EditorGUILayout.Space(8f);
        selectCreatedAsset = EditorGUILayout.Toggle("Select Created Asset", selectCreatedAsset);

        using (new EditorGUI.DisabledScope(!CanCreateAsset()))
        {
            if (GUILayout.Button("Create Story Node SO", GUILayout.Height(34f)))
            {
                GenerateStoryNodeAsset();
            }
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "This creates one StoryNodeSO asset. Add the created asset to StoryManager.allStoryNodes before using nextNodeID or branch jumps.",
            MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    private void DrawChoices()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Choices", EditorStyles.boldLabel);

        for (int i = 0; i < choiceDrafts.Count; i++)
        {
            StoryChoiceDraft choice = choiceDrafts[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Choice {i + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(80f)))
                {
                    choiceDrafts.RemoveAt(i);
                    i--;
                    EditorGUILayout.EndVertical();
                    continue;
                }
            }

            choice.choiceText = EditorGUILayout.TextField("Choice Text", choice.choiceText);
            choice.nextNodeID = EditorGUILayout.TextField("Next Node ID", choice.nextNodeID);
            choice.requiredClue = (ClueSO)EditorGUILayout.ObjectField("Required Clue", choice.requiredClue, typeof(ClueSO), false);
            choice.requiredFlag = EditorGUILayout.TextField("Required Flag", choice.requiredFlag);
            choice.flagOnChoose = EditorGUILayout.TextField("Flag On Choose", choice.flagOnChoose);
            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("Add Choice"))
        {
            choiceDrafts.Add(new StoryChoiceDraft());
        }
    }

    private void GenerateStoryNodeAsset()
    {
        string folderPath = GetFolderPath(targetFolder);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            Debug.LogWarning("Invalid target folder. Cannot create StoryNodeSO asset.");
            return;
        }

        StoryNodeSO created = CreateStoryNodeAsset(
            folderPath,
            nodeID,
            speakerName,
            dialogueText,
            cgImage,
            characterImageIndex,
            nextNodeID,
            returnToNormalModeOnNext,
            rewardClue,
            flagOnEnter,
            isEnding,
            isEnding ? endingType : EndingType.None,
            choiceDrafts,
            false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (selectCreatedAsset && created != null)
        {
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
        }

        nodeID = GetNextStoryNodeId(nodeID);
        Debug.Log($"StoryNodeSO creation finished: {AssetDatabase.GetAssetPath(created)}");
    }

    private bool CanCreateAsset()
    {
        return !string.IsNullOrWhiteSpace(nodeID) &&
               targetFolder != null &&
               !string.IsNullOrWhiteSpace(GetFolderPath(targetFolder));
    }

    private static StoryNodeSO CreateStoryNodeAsset(
        string folderPath,
        string nodeID,
        string speakerName,
        string dialogueText,
        Sprite cgImage,
        int characterImageIndex,
        string nextNodeID,
        bool returnToNormalModeOnNext,
        ClueSO rewardClue,
        string flagOnEnter,
        bool isEnding,
        EndingType endingType,
        List<StoryChoiceDraft> choiceDrafts,
        bool saveImmediately)
    {
        StoryNodeSO node = CreateInstance<StoryNodeSO>();
        node.nodeID = nodeID;
        node.speakerName = speakerName;
        node.dialogueText = dialogueText;
        node.cgImage = cgImage;
        node.characterImageIndex = characterImageIndex;
        node.nextNodeID = nextNodeID;
        node.returnToNormalModeOnNext = returnToNormalModeOnNext;
        node.rewardClue = rewardClue;
        node.flagOnEnter = flagOnEnter;
        node.isEnding = isEnding;
        node.endingType = endingType;
        node.choices = new List<StoryChoice>();

        foreach (StoryChoiceDraft draft in choiceDrafts)
        {
            if (draft == null)
            {
                continue;
            }

            StoryChoice choice = new StoryChoice
            {
                choiceText = draft.choiceText,
                nextNodeID = draft.nextNodeID,
                requiredClue = draft.requiredClue,
                requiredFlag = draft.requiredFlag,
                flagOnChoose = draft.flagOnChoose
            };
            node.choices.Add(choice);
        }

        string fileName = MakeSafeFileName(string.IsNullOrWhiteSpace(nodeID) ? "StoryNode" : nodeID);
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{fileName}.asset");
        AssetDatabase.CreateAsset(node, assetPath);

        if (saveImmediately)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        return node;
    }

    private static string GetFolderPath(DefaultAsset folderAsset)
    {
        if (folderAsset == null)
        {
            return null;
        }

        string path = AssetDatabase.GetAssetPath(folderAsset);
        return AssetDatabase.IsValidFolder(path) ? path : null;
    }

    private static string GetSelectedFolderPath()
    {
        Object selected = Selection.activeObject;
        if (selected == null)
        {
            return null;
        }

        string path = AssetDatabase.GetAssetPath(selected);
        if (AssetDatabase.IsValidFolder(path))
        {
            return path;
        }

        string directory = Path.GetDirectoryName(path);
        return !string.IsNullOrWhiteSpace(directory) && AssetDatabase.IsValidFolder(directory) ? directory : null;
    }

    private static string EnsureDefaultFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/SOS"))
        {
            AssetDatabase.CreateFolder("Assets", "SOS");
        }

        if (!AssetDatabase.IsValidFolder(DefaultRootFolderPath))
        {
            AssetDatabase.CreateFolder("Assets/SOS", DefaultStoryFolderName);
        }

        if (!AssetDatabase.IsValidFolder(DefaultFolderPath))
        {
            AssetDatabase.CreateFolder(DefaultRootFolderPath, DefaultMainStoryFolderName);
        }

        return DefaultFolderPath;
    }

    private static string GetNextStoryNodeIdInFolder(string folderPath)
    {
        int maxId = 1000;
        string[] guids = AssetDatabase.FindAssets("t:StoryNodeSO", new[] { folderPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StoryNodeSO node = AssetDatabase.LoadAssetAtPath<StoryNodeSO>(path);
            if (node != null && int.TryParse(node.nodeID, out int parsedId))
            {
                maxId = Mathf.Max(maxId, parsedId);
            }
        }

        return (maxId + 1).ToString();
    }

    private static string GetNextStoryNodeId(string currentNodeID)
    {
        if (int.TryParse(currentNodeID, out int parsedId))
        {
            return (parsedId + 1).ToString();
        }

        return currentNodeID;
    }

    private static string MakeSafeFileName(string value)
    {
        string fileName = string.IsNullOrWhiteSpace(value) ? "StoryNode" : value.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return fileName;
    }

    private class StoryChoiceDraft
    {
        public string choiceText = string.Empty;
        public string nextNodeID = string.Empty;
        public ClueSO requiredClue;
        public string requiredFlag = string.Empty;
        public string flagOnChoose = string.Empty;
    }
}
