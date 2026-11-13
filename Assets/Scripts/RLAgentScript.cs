using UnityEngine;
using UnityEngine.AI;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class RLAgentScript : Agent, IPenguin
{
    public enum AgentState { Attacking = 0, Incubating = 1, Hunting = 2 }
    private AgentState currentState = AgentState.Hunting; // overwritten by the first decision

    [Header("Movement / needs")]
    public float moveSpeed = 8f;
    public float turnSpeed = 180f;
    public float hungerSpeed = 2f;
    private const float MAX_HUNGER_LEVEL = 100f;
    public float hungerLevel = MAX_HUNGER_LEVEL;

    [Header("Scene references")]
    public Transform huntingTarget;
    public Transform incubationTarget;
    public EggScript eggController;
    public GameObject player;
    public SphereCollider attackArea;

    [Header("Observation distance")]
    public float maxDistance = 50f;

    [Header("Decisions")]
    [Tooltip("Minimum delay before requesting the next policy decision. Replaces the Decision Requester component (remove it).")]
    public float decisionInterval = 1f;
    private float decisionTimer;
    private bool actionInProgress;

    [Header("Enemy detection")]
    public float enemyDetectionRange = 50f;

    // Scale: terminal outcomes are ±1 and every shaping term is a fraction of that,
    // so no single shaping signal can outweigh actually winning or losing.
    [Header("Reward weights")]
    public float winReward = 1f;        // added once when the survival time is reached
    public float gameOverPenalty = 1f;  // subtracted once when LogicScript reports game over
    public float aliveRewardPerSecond = 0.001f;      // ~0.3 over a 300 s episode
    public float incubationRewardWeight = 0.05f;     // start incubating: weight × (1 − egg warmth)
    public float hungerRewardWeight = 0.05f;         // start hunting: weight × (1 − hunger)
    public float earlyLeavePenaltyWeight = 0.1f;     // leaving with the bar empty; ≥ start rewards so enter/leave loops don't pay
    public float noIncubationPenaltyWeight = 0.01f;  // max penalty per second (egg fully cold, not incubating)
    public float lowHungerPenaltyWeight = 0.01f;     // max penalty per second (hunger at 0, not hunting)

    // Scaring an enemy is worth as much as the best possible hunting / incubation start
    private float ScaredEnemyReward => Mathf.Max(hungerRewardWeight, incubationRewardWeight);

    private NavMeshAgent agent;
    private GameObject body;

    private bool isHunting;
    private bool isIncubating;
    private bool isHuntingAvailable;
    private bool isIncubationAvailable;

    private const float ENEMY_SCAN_INTERVAL = 0.25f;
    private float enemyScanTimer;

    private const float ATTACK_TIME = 1f;
    private float attackTimer;

    private EnemyScript currentEnemy;   // non-scared enemy currently targeted

    // Time without incubating. X = time for hunger to drop from full to half.
    private float timeSinceIncubated;
    private const float EggPenaltyThreshold = 0.9f; // no-incubation penalty starts below this egg warmth (0..1)
    private float noIncubationTime => (MAX_HUNGER_LEVEL * 0.5f) / hungerSpeed;

    public override void Initialize()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.angularSpeed = turnSpeed;
        }

        body = transform.GetChild(0).gameObject;

        if (attackArea == null) attackArea = GetComponent<SphereCollider>();
        if (attackArea != null) attackArea.enabled = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnemyScript.Scared += HandleEnemyScared;
    }

    protected override void OnDisable()
    {
        EnemyScript.Scared -= HandleEnemyScared;
        base.OnDisable();
    }

    // Reward only enemies scared by this agent's own colliders (not the player's or another penguin's)
    private void HandleEnemyScared(EnemyScript enemy, GameObject scarer)
    {
        if (scarer != null && scarer.GetComponentInParent<RLAgentScript>() == this)
            AddReward(ScaredEnemyReward);
    }

    // No manual reset needed: LogicScript reloads the scene when an episode ends,
    // so every episode starts from the exact state of a fresh scene load.
    // Keep "Max Step" = 0 in Behavior Parameters / Agent, otherwise the agent would
    // end an episode on its own without reloading the scene.

    public override void CollectObservations(VectorSensor sensor)
    {
        // In the sea the penguin can't see anything above the surface: those observations are -1.

        // Own hunger (0..1)
        sensor.AddObservation(hungerLevel / MAX_HUNGER_LEVEL);

        // Distance to the egg (0..1)
        float eggDist = eggController != null
            ? Vector3.Distance(transform.position, eggController.transform.position)
            : maxDistance;
        sensor.AddObservation(Mathf.Clamp01(eggDist / maxDistance));

        // Distance to the sea (0..1)
        float seaDist = huntingTarget != null
            ? Vector3.Distance(transform.position, huntingTarget.position)
            : maxDistance;
        sensor.AddObservation(Mathf.Clamp01(seaDist / maxDistance));

        // Distance to the player (-1 = unknown while hunting)
        if (isHunting || player == null)
            sensor.AddObservation(-1f);
        else
            sensor.AddObservation(Mathf.Clamp01(Vector3.Distance(transform.position, player.transform.position) / maxDistance));

        // Enemy detected (0/1, -1 = unknown while hunting)
        // Distance to that enemy (0..1, -1 = no enemy detected / unknown while hunting)
        if (isHunting)
        {
            sensor.AddObservation(-1f);
            sensor.AddObservation(-1f);
        }
        else if (currentEnemy != null)
        {
            sensor.AddObservation(1f);
            float enemyDist = Vector3.Distance(transform.position, currentEnemy.transform.position);
            sensor.AddObservation(Mathf.Clamp01(enemyDist / maxDistance));
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(-1f);
        }

        // Egg being incubated (0/1, -1 = unknown while hunting)
        if (isHunting || eggController == null)
            sensor.AddObservation(-1f);
        else
            sensor.AddObservation(eggController.IsAlone() ? 0f : 1f);

        // Time since the egg was last known to be warmed (0..1 over the penalty ramp)
        sensor.AddObservation(Mathf.Clamp01(timeSinceIncubated / (2f * noIncubationTime)));
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        // Can't attack what isn't there. Underwater the agent can't see enemies,
        // so the mask must not leak that information.
        if (currentEnemy == null && !isHunting)
            actionMask.SetActionEnabled(0, (int)AgentState.Attacking, false);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var requested = (AgentState)actions.DiscreteActions[0];

        // A high-level action remains active until it reaches its destination.
        // This gives the policy a meaningful outcome for each decision instead
        // of allowing a new action to replace it during navigation.
        if (actionInProgress && requested != AgentState.Attacking) return;

        changeState(requested);
        actionInProgress = true;
    }

    // Time-based penalties, applied every frame scaled by Time.deltaTime (weights are per second),
    // so they don't depend on how often decisions happen or whether a decision was ignored.
    private void applyStatePenalties(float dt)
    {
        float hungerNorm = hungerLevel / MAX_HUNGER_LEVEL;

        // No-incubation penalty: 0..1 as the egg's warmth drops from 90% to 0
        // (paused once Incubating is the chosen state, even before arriving)
        if (currentState != AgentState.Incubating && eggController != null)
        {
            float eggNorm = Mathf.Clamp01(eggController.GetColdLevel() / EggScript.MAX_COLD_LEVEL);
            if (eggNorm < EggPenaltyThreshold)
            {
                float p = Mathf.Clamp01((EggPenaltyThreshold - eggNorm) / EggPenaltyThreshold);
                AddReward(-noIncubationPenaltyWeight * p * dt);
            }
        }

        // Low-hunger penalty: 0..1 as hunger drops from 50 to 0
        // (paused once Hunting is the chosen state, even before arriving)
        if (currentState != AgentState.Hunting && hungerNorm < 0.5f)
        {
            float p = Mathf.Clamp01((0.5f - hungerNorm) / 0.5f);
            AddReward(-lowHungerPenaltyWeight * p * dt);
        }
    }

    /// <summary>Called by LogicScript when the game is over (egg lost, player or penguin starved).</summary>
    public void OnGameOver()
    {
        AddReward(-gameOverPenalty);
        LogEpisodeReward("GAME OVER");
        EndEpisode();
    }

    /// <summary>Called by LogicScript when the survival time is reached.</summary>
    public void OnWin()
    {
        AddReward(winReward);
        LogEpisodeReward("WIN");
        EndEpisode();
    }

    // Must be called before EndEpisode(), which resets the cumulative reward.
    private void LogEpisodeReward(string reason)
    {
        Debug.Log($"[RLAgent] EPISODE END ({reason}) t={Time.time:F1}s total reward = {GetCumulativeReward():F3}", this);
    }

    void Update()
    {
        AddReward(aliveRewardPerSecond * Time.deltaTime);
        applyStatePenalties(Time.deltaTime);

        decisionTimer -= Time.deltaTime;
        if (!actionInProgress && decisionTimer <= 0f)
        {
            decisionTimer = decisionInterval;
            RequestDecision();
        }

        enemyScanTimer -= Time.deltaTime;
        if (enemyScanTimer <= 0f)
        {
            enemyScanTimer = ENEMY_SCAN_INTERVAL;
            detectEnemies();
        }
        if(!isHunting)handleHunger();
        handleAttack();

        // Reset when incubating, or when the penguin sees the player incubating the egg
        // (not possible underwater). While incubating, IsAlone() is false too.
        bool eggTended = isIncubating || (!isHunting && eggController != null && !eggController.IsAlone());
        if (eggTended) timeSinceIncubated = 0f;
        else timeSinceIncubated += Time.deltaTime;

        switch (currentState)
        {
            case AgentState.Attacking:  performAttackAction();     break;
            case AgentState.Incubating: performIncubationAction(); break;
            case AgentState.Hunting:    performHuntingAction();    break;
        }
    }

    private void changeState(AgentState newState, bool penalizeEarlyLeave = true)
    {
        if (currentState == newState) return;

        // Penalize leaving hunting / incubating early, proportional to how much of the bar is still unfilled
        if (penalizeEarlyLeave && isHunting)
        {
            float remaining = 1f - Mathf.Clamp01(hungerLevel / MAX_HUNGER_LEVEL);
            AddReward(-earlyLeavePenaltyWeight * remaining);
        }
        if (penalizeEarlyLeave && isIncubating && currentEnemy == null && eggController != null)
        {
            float remaining = 1f - Mathf.Clamp01(eggController.GetColdLevel() / EggScript.MAX_COLD_LEVEL);
            AddReward(-earlyLeavePenaltyWeight * remaining);
        }

        if (isIncubating) stopIncubating();
        if (isHunting) stopHunting();

        if (attackArea != null && attackArea.enabled)
        {
            attackArea.enabled = false;
            attackTimer = 0f;
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.ResetPath();
        }
        if (body != null && !body.activeSelf) body.SetActive(true);

        currentState = newState;
    }

    void handleHunger()
    {
        hungerLevel = Mathf.Max(0f, hungerLevel - Time.deltaTime * hungerSpeed);
    }

    void startHunting()
    {
        isHunting = true;
        body.SetActive(false);
        if (agent != null) agent.isStopped = true;
    }

    void stopHunting()
    {
        isHunting = false;
        body.SetActive(true);
        if (agent != null) agent.isStopped = false;
    }

    void startIncubating()
    {
        isIncubating = true;
        body.SetActive(false);
        if (agent != null) agent.isStopped = true;
        if (eggController != null) eggController.StartIncubation();
    }

    void stopIncubating()
    {
        isIncubating = false;
        body.SetActive(true);
        if (agent != null) agent.isStopped = false;
        if (eggController != null) eggController.StopIncubation();
    }

    void handleHunting()
    {
        hungerLevel = Mathf.Clamp(hungerLevel + Time.deltaTime * hungerSpeed * 5, 0, MAX_HUNGER_LEVEL);
    }

    void handleAttack()
    {
        if (attackArea != null && attackArea.enabled)
        {
            attackTimer += Time.deltaTime;
            if (attackTimer >= ATTACK_TIME)
            {
                attackArea.enabled = false;
                attackTimer = 0f;
            }
        }
    }

    void performIncubationAction()
    {
        if (isIncubating)
        {
            completeAction();
            return;
        }

        if (isIncubationAvailable && eggController != null && eggController.IsAlone())
        {
            startIncubating();
            AddReward(incubationRewardWeight *
                (1f - Mathf.Clamp01(eggController.GetColdLevel() / EggScript.MAX_COLD_LEVEL)));
            completeAction();
        }
        else if (incubationTarget != null && agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(incubationTarget.position);
        }
    }

    void performHuntingAction()
    {
        if (isHuntingAvailable)
        {
            if (!isHunting)
            {
                startHunting();
                AddReward(hungerRewardWeight *
                    (1f - Mathf.Clamp01(hungerLevel / MAX_HUNGER_LEVEL)));
            }
            completeAction();
        }
        else if (huntingTarget != null && agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(huntingTarget.position);
        }

        if (isHunting) handleHunting();
    }

    void attack()
    {
        if (attackArea != null && !attackArea.enabled)
        {
            attackArea.enabled = true;
            attackTimer = 0f;
        }
    }

    void performAttackAction()
    {
        if (currentEnemy == null || attackArea == null)
        {
            completeAction();
            return;
        }
        float distanceToEnemy = Vector3.Distance(transform.position, currentEnemy.transform.position);

        if (distanceToEnemy < attackArea.radius - 1f)
        {
            if (agent != null && agent.hasPath) agent.ResetPath();
            attack();
            completeAction();
        }
        else if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(currentEnemy.transform.position);
        }
    }

    private void completeAction()
    {
        if (!actionInProgress) return;

        actionInProgress = false;
        decisionTimer = decisionInterval;
        if (agent != null && agent.isOnNavMesh)
            agent.ResetPath();
    }

    void detectEnemies()
    {
        EnemyScript previousEnemy = currentEnemy;
        currentEnemy = null;
        // Closest non-scared enemy within detection range
        float closest = enemyDetectionRange;
        foreach (var enemyObj in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            var enemy = enemyObj.GetComponent<EnemyScript>();
            if (enemy == null || enemy.IsScared()) continue;

            float dist = Vector3.Distance(transform.position, enemyObj.transform.position);
            if (dist < closest)
            {
                closest = dist;
                currentEnemy = enemy;
            }
        }

        // An enemy is an important observation change, so let the policy
        // respond immediately instead of waiting for the normal interval.
        // The policy still decides whether to interrupt the current action.
        if (currentEnemy != null && currentEnemy != previousEnemy)
            RequestDecision();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == "Sea") isHuntingAvailable = true;
        // Only track proximity; performIncubationAction checks IsAlone() when it acts
        if (other.gameObject.name == "Egg") isIncubationAvailable = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.name == "Sea") isHuntingAvailable = false;
        if (other.gameObject.name == "Egg") isIncubationAvailable = false;
    }

    public bool IsHunting() => isHunting;
    public float GetHungerLevel() => hungerLevel;
}
