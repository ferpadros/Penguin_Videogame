using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class EggScript : MonoBehaviour
{
    public TextMeshProUGUI uiText;
    public float coldSpeed = 2f;
    public float coldLevel = 100f; 
    public const float MAX_COLD_LEVEL = 100f;
    public Slider coldSlider;
    public GameObject penguin;
    public bool isAlone = true;
    public bool eaten = false;

    private bool isPlayerIncubating = false;
    private bool isPlayerNear = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        uiText.gameObject.SetActive(false);
        penguin.gameObject.SetActive(false);
        coldLevel = MAX_COLD_LEVEL;
        RefreshColdBar();
        if (coldSlider != null)
        {
            coldSlider.maxValue = coldLevel; 
            coldSlider.value = coldLevel;
            coldSlider.maxValue = coldLevel;
        }
    }

    // Cold bar is visible only while the player is in the trigger and either
    // nobody is incubating or the player is the one incubating.
    void RefreshColdBar(){
        if (coldSlider == null) return;
        coldSlider.gameObject.SetActive(isPlayerNear && (isAlone || isPlayerIncubating));
        coldSlider.value = coldLevel;
    }

    public void StartIncubation(bool isPlayer = false){
        isAlone = false;
        penguin.SetActive(true);
        // Egg is taken (by player or agent): hide the prompt
        uiText.gameObject.SetActive(false);
        uiText.text = "";
        isPlayerIncubating = isPlayer;
        RefreshColdBar();
    }

    public void StopIncubation(){
        isAlone = true;
        penguin.SetActive(false);
        isPlayerIncubating = false;
        RefreshColdBar();
        // Egg is free again: show the prompt if the player is standing in the trigger
        if(isPlayerNear){
            uiText.text = "Incubate";
            uiText.gameObject.SetActive(true);
        }
    }

    void handleIncubation(){
        coldLevel = Mathf.Clamp(coldLevel + Time.deltaTime * coldSpeed, 0, MAX_COLD_LEVEL);
        if (coldSlider != null)
        {
            coldSlider.value = coldLevel;
        }
    }


    void handleCold()
    {
        coldLevel -= Time.deltaTime * coldSpeed;

        if (coldSlider != null)
        {
            coldSlider.value = coldLevel;
        }
    }

    void OnTriggerEnter(Collider other){
        if (other.gameObject.name == "Player"){
            isPlayerNear = true;
            if (isAlone){
                uiText.gameObject.SetActive(true);
                uiText.text = "Incubate";
            }
            RefreshColdBar();
        }
        if (other.gameObject.tag == "Enemy"){
            eaten = true;
        }
    }

    void OnTriggerExit(Collider other){
        if (other.gameObject.name == "Player"){
            isPlayerNear = false;
            uiText.gameObject.SetActive(false);
            uiText.text = "";
            RefreshColdBar();
        }
    }


    // Update is called once per frame
    void Update()
    {
        if(isAlone){
            handleCold();
            return;
        }
        handleIncubation();
    }

    public bool IsAlone(){
        return isAlone;
    }

    public bool IsEggFine(){
        return !eaten && coldLevel >0;
    }

    public float GetColdLevel(){
        return coldLevel;
    }

    public bool IsLevelFull(){
        return coldLevel >= MAX_COLD_LEVEL;
    }
}
