using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.AI; 
using System.Collections;
using System.Collections.Generic;
using BehaviorTree;

public class AgentScript : MonoBehaviour, IPenguin
{
    public enum AgentState { Idle, Attacking, Incubating, Hunting }
    public AgentState currentState = AgentState.Idle;

    public float moveSpeed = 8f; 
    public float turnSpeed = 180f;
    public float hungerSpeed = 2f;
    private float MAX_HUNGER_LEVEL = 100f;
    public float hungerLevel = 100f;

    private LogicScript logic;
    private NavMeshAgent agent;

    private bool isHunting = false;
    private bool isIncubating = false;
    private bool isHuntingAvailable = false;
    private bool isIncubationAvailable = false;

    private GameObject body;

    public Transform huntingTarget;
    public Transform incubationTarget;
    public EggScript eggController;
    public GameObject player;

    public SphereCollider attackArea;

    private float ATTACK_TIME = 1f;
    private float attackTimer = 0f;

    private GameObject currentEnemy;
    private Node behaviorTree;
    


    void Start() 
    { 
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.angularSpeed = turnSpeed;
        }
        
        GameObject logicObj = GameObject.FindGameObjectWithTag("Logic");
        body = transform.GetChild(0).gameObject;

        if (logicObj != null)
        {
            logic = logicObj.GetComponent<LogicScript>();
        }

        hungerLevel = MAX_HUNGER_LEVEL;

        if (attackArea == null)
        {
            attackArea = GetComponent<SphereCollider>();
        }
        if (attackArea != null)
        {
            attackArea.enabled = false;
        }

        behaviorTree = BuildTree();
    }

    private void changeState(AgentState newState)
    {
        if (currentState == newState) return; // Already in target state

        if (isIncubating)
        {
            stopIncubating();
        }

        if (isHunting)
        {
            stopHunting();
        }

        if (attackArea != null && attackArea.enabled)
        {
            attackArea.enabled = false;
            attackTimer = 0f;
        }

        // Reset movement path and ensure body is visible
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.ResetPath();
        }
        if (body != null && !body.activeSelf)
        {
            body.SetActive(true);
        }

        currentState = newState;
    }

    void handleHunger()
    {
        hungerLevel -= Time.deltaTime * hungerSpeed;
    }

    void startHunting(){
        isHunting = true;
        body.SetActive(false);
        if (agent != null) agent.isStopped = true;
    }

    void stopHunting(){
        isHunting = false;
        body.SetActive(true);
        if (agent != null) agent.isStopped = false;
    }

    void startIncubating(){
        isIncubating = true;
        body.SetActive(false);
        if (agent != null) agent.isStopped = true;
        if (eggController != null) eggController.StartIncubation();
    }

    void stopIncubating(){
        isIncubating = false;
        body.SetActive(true);
        if (agent != null) agent.isStopped = false;
        if (eggController != null) eggController.StopIncubation();
    }

    void handleHunting(){
        hungerLevel = Mathf.Clamp(hungerLevel + Time.deltaTime * hungerSpeed * 5, 0, MAX_HUNGER_LEVEL);
        // if(hungerLevel >= MAX_HUNGER_LEVEL){
        //     stopHunting();
        //     changeState(AgentState.Idle);
        // }
    }

    void handleAttack(){
        if(attackArea != null && attackArea.enabled){
            attackTimer += Time.deltaTime;
            if(attackTimer >= ATTACK_TIME){
                attackArea.enabled = false;
                attackTimer = 0f;
            }
        }
    }

    void attack(){
        if(attackArea != null && !attackArea.enabled)
        {
            attackArea.enabled = true;
            attackTimer = 0f;
        }
    }

    // Behaviour tree
    private Node BuildTree()
    {
        // Hunt: go to the sea, start hunting once there, eat while hunting.
        Node Hunt() => new Sequence(new List<Node> {
            new ActionNode(() => changeState(AgentState.Hunting)),
            new Selector(new List<Node> {
                new Sequence(new List<Node> {
                    new ConditionNode(() => isHuntingAvailable),
                    new ActionNode(() => { if (!isHunting) startHunting(); })
                }),
                new ActionNode(() => {
                    if (huntingTarget != null && agent != null)
                        agent.SetDestination(huntingTarget.position);
                })
            }),
            new ActionNode(() => { if (isHunting) handleHunting(); })
        });

        // Attack: go close to the enemy and scare them
        Node Attack() => new Sequence(new List<Node> {
            new ActionNode(() => changeState(AgentState.Attacking)),
            new Selector(new List<Node> {
                new Sequence(new List<Node> {
                    new ConditionNode(() => Vector3.Distance(transform.position, currentEnemy.transform.position) < attackArea.radius - 1f),
                    new ActionNode(() => {
                        if (agent != null && agent.hasPath) agent.ResetPath();
                        attack();
                    })
                }),
                new ActionNode(() => {
                    if (agent != null) agent.SetDestination(currentEnemy.transform.position);
                })
            })
        });

        // Incubate: go to the egg and sit on it as soon as it is free.
        Node Incubate() => new Sequence(new List<Node> {
            new ActionNode(() => changeState(AgentState.Incubating)),
            new Selector(new List<Node> {
                new Sequence(new List<Node> {
                    new ConditionNode(() => isIncubationAvailable),
                    new ActionNode(() => {
                        if (!isIncubating && eggController != null && eggController.IsAlone())
                            startIncubating();
                    })
                }),
                new ActionNode(() => {
                    if (incubationTarget != null && agent != null)
                        agent.SetDestination(incubationTarget.position);
                })
            })
        });

        return new Selector(new List<Node> {
            new Sequence(new List<Node> { new ConditionNode(() => isHunting && hungerLevel < MAX_HUNGER_LEVEL), Hunt() }),
            new Sequence(new List<Node> { new ConditionNode(() => currentEnemy != null), Attack() }),
            new Sequence(new List<Node> { new ConditionNode(() => hungerLevel <= MAX_HUNGER_LEVEL * 0.25f), Hunt() }),
            new Sequence(new List<Node> {
                new ConditionNode(() => hungerLevel <= MAX_HUNGER_LEVEL * 0.5f),
                new ConditionNode(canLeaveEgg),
                Hunt()
            }),
            new Sequence(new List<Node> { new ConditionNode(() => !isIncubating && !eggController.IsAlone()), Hunt() }),
            Incubate()
        });
    }

    void detectEnemies(){
        currentEnemy = GameObject.FindGameObjectWithTag("Enemy");
        if(currentEnemy != null){
            EnemyScript enemyManager = currentEnemy.GetComponent<EnemyScript>();
            if(enemyManager.IsScared()){
                currentEnemy = null;
            }
        }
    }

    bool canLeaveEgg(){
        if(!isIncubating && !eggController.IsAlone()) return true;
        float maxPlayerDistance = 10f;
        float eggToPlayerDistance = Vector3.Distance(eggController.gameObject.transform.position, player.transform.position);
        return eggToPlayerDistance < maxPlayerDistance;
    }

    void Update() 
    {
        if(!isHunting){
            handleHunger();
            detectEnemies();
        } 
        handleAttack();

        behaviorTree.Evaluate();
    }

    void OnTriggerEnter(Collider collider){
        if(collider.gameObject.name == "Sea"){
            isHuntingAvailable = true;
        }
        if(collider.gameObject.name == "Egg"){
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