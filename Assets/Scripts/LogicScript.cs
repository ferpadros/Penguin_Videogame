using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class LogicScript : MonoBehaviour
{
    private GameObject gameOverPanel;
    private bool restarting;

    [Header("Win condition")]
    public float gameDurationSeconds = 300f; // 5 minutes
    private float elapsed;
    public EggScript eggController;

    // Every object tagged "Penguin" (player, scripted agent or RL agent)
    private readonly List<IPenguin> penguins = new List<IPenguin>();

    void Awake()
    {
        // Undo the pause from a previous game over, but never while training:
        // ML-Agents sets Time.timeScale (e.g. 20x) and this would reset it to real time
        // on every scene reload.
        bool training = Unity.MLAgents.Academy.IsInitialized && Unity.MLAgents.Academy.Instance.IsCommunicatorOn;
        if (!training) Time.timeScale = 1f;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
          
            Transform panelTransform = canvas.transform.Find("GameOver");

            if (panelTransform != null)
            {
                gameOverPanel = panelTransform.gameObject;
            }
            else
            {
                Debug.LogError("LogicScript: Could not find child object named 'GameOver' under Canvas!");
            }

        }
    }

    void Start()
    {
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Penguin"))
        {
            IPenguin penguin = obj.GetComponentInParent<IPenguin>();
            if (penguin != null && !penguins.Contains(penguin))
            {
                penguins.Add(penguin);
            }
        }

        if (penguins.Count == 0)
        {
            Debug.LogWarning("LogicScript: no objects tagged 'Penguin' with a penguin script were found.");
        }
    }

    public void Update(){
        if (restarting) return;

        elapsed += Time.deltaTime;
        if (elapsed >= gameDurationSeconds)
        {
            Debug.Log("WIN");
            Win();
            return;
        }

        if(!eggController.IsEggFine()){
            Debug.Log("Termination reason: EGG");
            GameOver(true);
            return;
        }

        foreach (IPenguin penguin in penguins)
        {
            if(penguin.GetHungerLevel() <= 0){
                Debug.Log("Termination reason: HUNGER");
                GameOver(penguin is RLAgentScript);
                return;
            }
        }
    }


    public void GameOver(bool penalizeAgent = false)
    {
        // While training: penalise the agent, end the episode and reload the scene
        // so the next episode starts from a fresh scene state.
        if (Unity.MLAgents.Academy.IsInitialized && Unity.MLAgents.Academy.Instance.IsCommunicatorOn)
        {
            if (restarting) return;
            restarting = true;
            if(penalizeAgent){
                foreach (IPenguin penguin in penguins)
                {
                    if (penguin is RLAgentScript rlAgent) rlAgent.OnGameOver();
                }
            }
            PlayAgain();
            return;
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
        Time.timeScale = 0f;
    }

    public void Win()
    {
        if (restarting) return;
        restarting = true;
        foreach (IPenguin penguin in penguins)
        {
            if (penguin is RLAgentScript rlAgent) rlAgent.OnWin();
        }
        PlayAgain();
    }

    public void PlayAgain()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

}