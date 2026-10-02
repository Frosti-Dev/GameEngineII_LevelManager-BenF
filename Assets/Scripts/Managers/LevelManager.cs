using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private LoadingOverlay LoadingOverlay;
    private Dictionary<string, string> loadedSceneBySlot = new();
    private bool isBusy = false;
    
    public static LevelManager manager;

    void Awake()
    {
        if (manager == null)
        {
            DontDestroyOnLoad(gameObject);
            manager = this;
        }

        else if (manager != null)
        {
            Destroy(gameObject);
        }
    }

    #region buttons
    public void ToMainMenu()
    {
        manager
            .NewTransition()
            .Load(SceneDataBase.Slots.MainMenu, SceneDataBase.Scenes.MainMenu, setActive: true)
            .Unload(SceneDataBase.Slots.Session)
            .Unload(SceneDataBase.Slots.SessionContent)
            .WithClearUnusedAssets()
            .WithOverlay()
            .Perform();
    }

    public void ToStart()
    {
        manager
            .NewTransition()
            .Load(SceneDataBase.Slots.Session, SceneDataBase.Scenes.Session)
            .Load(SceneDataBase.Slots.SessionContent, SceneDataBase.Scenes.Gameplay, setActive: true)
            .Unload(SceneDataBase.Slots.MainMenu)
            .WithOverlay()
            .Perform();
    }

    public void ToGameplay()
    {
        manager
            .NewTransition()
            .Load(SceneDataBase.Slots.SessionContent, SceneDataBase.Scenes.Gameplay, setActive: true)
            .WithOverlay()
            .Perform();
    }

    public void ToGameOver()
    {
        manager
            .NewTransition()
            .Load(SceneDataBase.Slots.SessionContent, SceneDataBase.Scenes.GameOver, setActive: true)
            .WithOverlay()
            .Perform();

    }

    public void Quit()
    {
        Application.Quit();
    }
    #endregion

    public SceneTransitionPlan NewTransition()
    {
        return new SceneTransitionPlan();
    }

    private Coroutine ExecutePlan(SceneTransitionPlan plan)
    {
        if (isBusy)
        {
            Debug.LogWarning("Scene Change already in progress!");
            return null;
        }

        isBusy = true;
        return StartCoroutine(ChangeSceneRoutine(plan));
    }

    private IEnumerator ChangeSceneRoutine(SceneTransitionPlan plan)
    {
        if (plan.Overlay)
        {
            yield return LoadingOverlay.FadeInBlack();
            yield return new WaitForSeconds(0.5f);
        }

        foreach (var slotKey in plan.ScenesToUnload)
        {
            yield return UnloadSceneRoutine(slotKey);
        }

        if (plan.ClearUnusedAssets) yield return CleanupUnusedAssetsRoutine();

        foreach(var kvp in plan.ScenesToLoad)
        {
            if (loadedSceneBySlot.ContainsKey(kvp.Key))
            {
                yield return UnloadSceneRoutine(kvp.Key);
            }

            yield return LoadAdditiveRoutine(kvp.Key, kvp.Value, plan.ActiveSceneName == kvp.Value);
        }

        if (plan.Overlay)
        {
            yield return LoadingOverlay.FadeOutBlack();
        }

        isBusy = false;
    }

    private IEnumerator LoadAdditiveRoutine(string slotKey, string sceneName, bool setActive)
    {
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

        if (loadOp == null) yield break;
        loadOp.allowSceneActivation = false;
        
        while(loadOp.progress < 0.9f)
        {
            yield return null;
        }

        loadOp.allowSceneActivation = true;

        while(!loadOp.isDone)
        {
            yield return null;
        }

        if (setActive)
        {
            Scene newScene = SceneManager.GetSceneByName(sceneName);

            if(newScene.IsValid() && newScene.isLoaded)
            {
                SceneManager.SetActiveScene(newScene);
            }
        }

        loadedSceneBySlot[slotKey] = sceneName;
    }

    private IEnumerator UnloadSceneRoutine(string slotKey)
    {
        if (!loadedSceneBySlot.TryGetValue(slotKey, out string sceneName)) yield break;
        if (string.IsNullOrEmpty(sceneName)) yield break;

        AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(sceneName);

        if(unloadOp != null)
        {
            while (!unloadOp.isDone)
            {
                yield return null;
            }
        }

        loadedSceneBySlot.Remove(slotKey);
    }

    private IEnumerator CleanupUnusedAssetsRoutine()
    {
        AsyncOperation cleanupOp = Resources.UnloadUnusedAssets();

        while (!cleanupOp.isDone)
        {
            yield return null;
        }
    }

    public class SceneTransitionPlan
    {
        public Dictionary<string, string> ScenesToLoad { get; } = new();
        public List<string> ScenesToUnload { get; } = new();
        public string ActiveSceneName { get; private set; } = "";
        public bool ClearUnusedAssets { get; private set; } = false;
        public bool Overlay { get; private set; } = false;

        public SceneTransitionPlan Load(string slotKey, string sceneName, bool setActive = false)
        {
            ScenesToLoad[slotKey] = sceneName;
            if (setActive) ActiveSceneName = sceneName;
            return this;
        }

        public SceneTransitionPlan Unload(string slotKey)
        {
            ScenesToUnload.Add(slotKey);
            return this;
        }

        public SceneTransitionPlan WithOverlay()
        {
            Overlay = true;
            return this;
        }

        public SceneTransitionPlan WithClearUnusedAssets()
        {
            ClearUnusedAssets = true;
            return this;
        }

        public Coroutine Perform()
        {
            return LevelManager.manager.ExecutePlan(this);
        }
    }
}
