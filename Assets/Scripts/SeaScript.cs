using UnityEngine;
using TMPro;

public class SeaScript : MonoBehaviour
{
    public TextMeshProUGUI uiText; 

    public PlayerScript playerController;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
         uiText.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if(playerController != null && playerController.IsHunting()){
            uiText.gameObject.SetActive(false);
            uiText.text = "";
        }
        if(playerController != null && !playerController.IsHunting()){
            uiText.gameObject.SetActive(true);
            uiText.text = "Find food";        
        }
        
    }

    void OnTriggerEnter(Collider other){
        if (other.gameObject.name == "Player"){
            playerController = other.gameObject.GetComponent<PlayerScript>();
            uiText.gameObject.SetActive(true);
            uiText.text = "Find food";
        }
    }

    void OnTriggerExit(Collider other){
        if (other.gameObject.name == "Player"){
            uiText.gameObject.SetActive(false);
            uiText.text = "";
            playerController = null;
        }
    }
}
