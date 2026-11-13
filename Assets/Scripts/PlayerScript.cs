using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

public class PlayerScript : MonoBehaviour, IPenguin
{
    public float moveSpeed = 8f; 
    public float turnSpeed = 180f;
    public float gravity = -9.81f; // Added gravity force strength
    public float hungerSpeed = 2f;
    private float MAX_HUNGER_LEVEL = 100f;
    private float hungerLevel = 100f;

    private LogicScript logic;
    
    private CharacterController controller; 
    private Vector3 velocity; 

    private bool isHunting = false;
    private bool isIncubating = false;
    private bool isHuntingAvailable = false;
    private bool isIncubationAvailable = false;

    private GameObject body;

    public EggScript eggController;

    public Slider hungerSlider;
    public SphereCollider attackArea;

    private float ATTACK_TIME = 1f;
    private float attackTimer = 0f;

    void Start() 
    { 
        controller = GetComponent<CharacterController>();
        
        GameObject logicObj = GameObject.FindGameObjectWithTag("Logic");

        body = transform.GetChild(0).gameObject;

        if (logicObj != null)
        {
            logic = logicObj.GetComponent<LogicScript>();
        }

        hungerLevel = MAX_HUNGER_LEVEL;
        if (hungerSlider != null)
        {
            hungerSlider.maxValue = hungerLevel;
            hungerSlider.value = hungerLevel;
        }

        // Only assign via GetComponent if it wasn't assigned in the Inspector
        if (attackArea == null)
        {
            attackArea = GetComponent<SphereCollider>();
            if (attackArea != null)
            {
                attackArea.enabled = false;
            }
        }
    }

    void handleHunger()
    {
        hungerLevel -= Time.deltaTime * hungerSpeed;

        if (hungerSlider != null)
        {
            hungerSlider.value = hungerLevel;
        }
    }

    void startHunting(){
        isHunting = true;
        body.SetActive(false);
    }

    void stopHunting(){
        isHunting = false;
        body.SetActive(true);
    }

    void startIncubating(){
        isIncubating = true;
        body.SetActive(false);
        if (eggController != null)
        {
            eggController.StartIncubation(true);
        }
    }

    void stopIncubating(){
        isIncubating = false;
        body.SetActive(true);
    }

    void handleHunting(){
        hungerLevel = Mathf.Clamp(hungerLevel + Time.deltaTime * hungerSpeed * 5, 0, MAX_HUNGER_LEVEL);
        if (hungerSlider != null)
        {
            hungerSlider.value = hungerLevel;
        }

        if(hungerLevel >= MAX_HUNGER_LEVEL || Keyboard.current.escapeKey.isPressed){
            stopHunting();
        }
    }

    void handleAttack(){
        // Check if attackArea exists and is enabled
        if(attackArea != null && attackArea.enabled){
            attackTimer += Time.deltaTime;
            if(attackTimer >= ATTACK_TIME){
                attackArea.enabled = false;
                attackTimer = 0f;
            }
        }
    }

    void Update() 
    {
        if(isHunting){
            handleHunting();
            return;
        }else{
            handleHunger();
        }

        handleAttack();

        if(isIncubating){
            if(Keyboard.current.escapeKey.isPressed){
                if (eggController != null) eggController.StopIncubation();
                stopIncubating();
            }
            return;
        }

        // 1. Ground check reset
        if (controller.isGrounded && velocity.y < 0)
        { 
            velocity.y = -2f; // Slight downward force to stay stuck to slopes/ground
        }

        if(Keyboard.current.spaceKey.isPressed){
            if(attackArea != null)
            {
                attackArea.enabled = true; 
                attackTimer = 0f;        
            }
        }

        if(Keyboard.current.enterKey.isPressed){
            if(isHuntingAvailable){
                startHunting();
            }
            else if(isIncubationAvailable && eggController != null && eggController.IsAlone()){
                startIncubating();
            }
        }

        // Rotation Controls
        float turnInput = 0f;
        if (Keyboard.current.leftArrowKey.isPressed) turnInput = -1f;
        else if (Keyboard.current.rightArrowKey.isPressed) turnInput = 1f;

        transform.Rotate(0f, turnInput * turnSpeed * Time.deltaTime, 0f);

        // Forward movement 
        Vector3 move = Vector3.zero;
        if (Keyboard.current.upArrowKey.isPressed) move = transform.forward;
        if (Keyboard.current.downArrowKey.isPressed) move = -transform.forward;

        // 2. Accumulate downward acceleration over time
        velocity.y += gravity * Time.deltaTime;

        // 3. Combine horizontal movement and vertical gravity velocity into a single Move call
        Vector3 finalMove = (move * moveSpeed) + velocity;
        controller.Move(finalMove * Time.deltaTime);
    }

    void OnTriggerEnter(Collider collider){
        if(collider.gameObject.name == "Sea"){
            isHuntingAvailable = true;
        }
        if(collider.gameObject.name == "Egg"){
            // Only track proximity; IsAlone() is checked when the player presses Enter
            isIncubationAvailable = true;
        }
    }

    void OnTriggerExit(Collider collider){
        if(collider.gameObject.name == "Sea"){
            isHuntingAvailable = false;
        }
        if(collider.gameObject.name == "Egg"){
            isIncubationAvailable = false;
        }
    }

    public bool IsHunting(){
        return isHunting;
    }

    public float GetHungerLevel(){
        return hungerLevel;
    }
}