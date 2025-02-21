using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNavigator : MonoBehaviour
{
    List<string> allSceneNames = new();
    string currentSceneName = "";

    float sceneButtonHeight = 20f;
    float sceneButtonWidth = 300f;

    public static SceneNavigator instance;


    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.activeSceneChanged += (a, b) => {
                currentSceneName = EditorBuildSettings.scenes[SceneManager.GetActiveScene().buildIndex].path.Split(".unity")[0];
            };
        }
    }

    private void Start()
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            allSceneNames.Add(scene.path.Split(".unity")[0]);
        }
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(10f, Screen.height - (allSceneNames.Count * (sceneButtonHeight + 5f)) - 40f, sceneButtonWidth + 20f, (allSceneNames.Count * (sceneButtonHeight + 5f)) + 30f), "Scene Navigator");
        
        for (int i = 0; i < allSceneNames.Count; i++)
        {
            string sceneName = allSceneNames[i];
            Rect sceneButtonRect = new Rect(20f, Screen.height - (allSceneNames.Count * (sceneButtonHeight + 5f)) + (i * (sceneButtonHeight + 5f)) - 15f, sceneButtonWidth, sceneButtonHeight);

            if (sceneName == currentSceneName)
            {
                GUI.Label(sceneButtonRect, "(Active) " + sceneName);
            }
            else
            {
                if (GUI.Button(sceneButtonRect, sceneName))
                {
                    SceneManager.LoadScene(sceneName + ".unity", LoadSceneMode.Single);
                }
            }
        }
    }
}
