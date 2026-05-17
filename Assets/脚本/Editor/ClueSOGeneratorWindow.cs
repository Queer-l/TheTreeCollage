using System.IO;
using UnityEditor;
using UnityEngine;

public class ClueSOGeneratorWindow : EditorWindow
{
    private const string DefaultFolderPath = "Assets/SOS/\u7ebf\u7d22SO";
    private const string WindowTitle = "Clue SO Generator";
    private const string DefaultClueName = "\u65b0\u9053\u5177";
    private const string DefaultClueFolderName = "\u7ebf\u7d22SO";

    private DefaultAsset targetFolder;
    private string clueName = DefaultClueName;
    private string clueIntro = string.Empty;
    private Sprite clueImage;
    private int clueId = 1;
    private bool isKeyClue;
    private string storyFlagOnCollect = string.Empty;
    private bool selectLastCreatedAsset = true;

    [MenuItem("Tools/Clue SO Generator")]
    public static void OpenWindow()
    {
        GetWindow<ClueSOGeneratorWindow>(WindowTitle);
    }

    [MenuItem("Assets/Create Clue SO In Selected Folder", priority = 20)]
    public static void CreateSingleClueSOInSelectedFolder()
    {
        string folderPath = GetSelectedFolderPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            folderPath = EnsureDefaultFolder();
        }

        ClueSO clue = CreateClueAsset(folderPath, DefaultClueName, string.Empty, null, GetNextClueIdInFolder(folderPath), false, string.Empty, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = clue;
        EditorGUIUtility.PingObject(clue);
        Debug.Log($"Created ClueSO: {AssetDatabase.GetAssetPath(clue)}");
    }

    private void OnEnable()
    {
        string folderPath = EnsureDefaultFolder();
        targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
        clueId = GetNextClueIdInFolder(folderPath);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Generator Settings", EditorStyles.boldLabel);

        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("Target Folder", targetFolder, typeof(DefaultAsset), false);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Selected Folder"))
            {
                string selectedFolder = GetSelectedFolderPath();
                if (!string.IsNullOrWhiteSpace(selectedFolder))
                {
                    targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(selectedFolder);
                }
                else
                {
                    Debug.LogWarning("No folder is selected in the Project panel.");
                }
            }

            if (GUILayout.Button("Use Default Folder"))
            {
                targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(EnsureDefaultFolder());
            }
        }

        clueName = EditorGUILayout.TextField("Clue Name", clueName);
        clueId = EditorGUILayout.IntField("Clue ID", clueId);
        clueImage = (Sprite)EditorGUILayout.ObjectField("Clue Image", clueImage, typeof(Sprite), false);
        isKeyClue = EditorGUILayout.Toggle("Is Key Clue", isKeyClue);
        storyFlagOnCollect = EditorGUILayout.TextField("Story Flag On Collect", storyFlagOnCollect);

        EditorGUILayout.LabelField("Clue Intro");
        clueIntro = EditorGUILayout.TextArea(clueIntro, GUILayout.MinHeight(70f));
        selectLastCreatedAsset = EditorGUILayout.Toggle("Select Last Created", selectLastCreatedAsset);

        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(!CanCreateAssets()))
        {
            if (GUILayout.Button("Create Clue SO", GUILayout.Height(32f)))
            {
                GenerateClueAsset();
            }
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "This creates one complete ClueSO asset with the values filled in this window.",
            MessageType.Info);
    }

    private void GenerateClueAsset()
    {
        string folderPath = GetFolderPath(targetFolder);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            Debug.LogWarning("Invalid target folder. Cannot create ClueSO assets.");
            return;
        }

        ClueSO created = CreateClueAsset(
            folderPath,
            clueName,
            clueIntro,
            clueImage,
            clueId,
            isKeyClue,
            storyFlagOnCollect,
            false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (selectLastCreatedAsset && created != null)
        {
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
        }

        clueId++;
        Debug.Log($"ClueSO creation finished: {AssetDatabase.GetAssetPath(created)}");
    }

    private bool CanCreateAssets()
    {
        return !string.IsNullOrWhiteSpace(clueName) &&
               targetFolder != null &&
               !string.IsNullOrWhiteSpace(GetFolderPath(targetFolder));
    }

    private static ClueSO CreateClueAsset(
        string folderPath,
        string clueName,
        string clueIntro,
        Sprite clueImage,
        int clueId,
        bool isKeyClue,
        string storyFlagOnCollect,
        bool saveImmediately)
    {
        ClueSO clue = CreateInstance<ClueSO>();
        clue.clueName = clueName;
        clue.clueIntro = clueIntro;
        clue.clueImage = clueImage;
        clue.clueId = clueId;
        clue.isKeyClue = isKeyClue;
        clue.storyFlagOnCollect = storyFlagOnCollect;

        string safeFileName = MakeSafeFileName(clueName);
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{safeFileName}.asset");
        AssetDatabase.CreateAsset(clue, assetPath);

        if (saveImmediately)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        return clue;
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
        if (AssetDatabase.IsValidFolder(DefaultFolderPath))
        {
            return DefaultFolderPath;
        }

        if (!AssetDatabase.IsValidFolder("Assets/SOS"))
        {
            AssetDatabase.CreateFolder("Assets", "SOS");
        }

        AssetDatabase.CreateFolder("Assets/SOS", DefaultClueFolderName);
        return DefaultFolderPath;
    }

    private static int GetNextClueIdInFolder(string folderPath)
    {
        int maxId = 0;
        string[] guids = AssetDatabase.FindAssets("t:ClueSO", new[] { folderPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ClueSO clue = AssetDatabase.LoadAssetAtPath<ClueSO>(path);
            if (clue != null)
            {
                maxId = Mathf.Max(maxId, clue.clueId);
            }
        }

        return maxId + 1;
    }

    private static string MakeSafeFileName(string value)
    {
        string fileName = string.IsNullOrWhiteSpace(value) ? DefaultClueName : value.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return fileName;
    }
}
