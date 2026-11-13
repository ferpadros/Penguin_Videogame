using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public float ENEMY_COOLDOWN = 3f;
    public float SPAWN_MIN_RANGE = 5f;
    public float SPAWN_MAX_RANGE = 10f;
    private float cooldown = 0;
    public GameObject Enemy;


    public void SpawnEnemy()
    {
        Vector2 randomCircle = Random.insideUnitCircle.normalized;
        float distance = Random.Range(SPAWN_MIN_RANGE, SPAWN_MAX_RANGE);
        Vector3 spawnOffset = new Vector3(randomCircle.x, 0f, randomCircle.y) * distance;
        Vector3 spawnPosition = transform.position + spawnOffset + new Vector3(0f, 50f, 0f);
        GameObject newEnemy = Instantiate(Enemy, spawnPosition, Quaternion.identity);
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cooldown = ENEMY_COOLDOWN + Random.Range(0f,ENEMY_COOLDOWN*0.5f);
    }

    // Update is called once per frame
    void Update()
    {
        if(cooldown <=0){
            SpawnEnemy();
            cooldown = ENEMY_COOLDOWN + Random.Range(0f,ENEMY_COOLDOWN*0.5f);
        }else{
            cooldown -= Time.deltaTime;
        }
    }
}
