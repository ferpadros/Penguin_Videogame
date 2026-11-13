using UnityEngine;

public class EnemyScript : MonoBehaviour
{
    public float moveSpeed = 8f; 
    public float flySpeed = 10f;
    public float gravity = -9.81f;
    private Vector3 velocity; 
    private CharacterController controller; 
    private EggScript eggController;
    private bool falling;
    public bool scared;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        
        GameObject eggObject = GameObject.FindWithTag("Egg");
        if (eggObject != null)
        {
            eggController = eggObject.GetComponent<EggScript>();
        }
        else
        {
            Debug.LogError("EggScript component missing or no object tagged 'Egg' found!");
        }
        falling = true;
    }

    // Update is called once per frame
    void Update()
    {   
        if(scared){
            velocity.y += flySpeed * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            if(transform.position.y >= 50f){
                Destroy(gameObject);
            }
            return;
        }

        if(falling){
            // Apply gravity
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }


        // Move towards the egg target when not incubating
        if (!falling && eggController != null && eggController.IsAlone())
        {
            Vector3 targetPosition = eggController.transform.position;
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f; 

            if (direction.sqrMagnitude > 0.001f)
            {
                Vector3 moveDirection = direction.normalized;
                controller.Move(moveDirection * moveSpeed * Time.deltaTime);

                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }
    }

    void OnControllerColliderHit(ControllerColliderHit hit){
        // Only count surfaces facing mostly upward as ground; ignore walls
        if (falling && hit.normal.y > 0.7f){
            falling = false;
            velocity.y = 0f;
        }
    }


    // Who scared this enemy (the collider's GameObject), null while not scared
    public GameObject ScaredBy { get; private set; }

    // Raised once, the moment an enemy becomes scared: (enemy, scarer)
    public static event System.Action<EnemyScript, GameObject> Scared;

    void OnTriggerEnter(Collider other){
        if (scared) return;
        if (other.gameObject.tag == "Penguin"){
            scared = true;
            ScaredBy = other.gameObject;
            Scared?.Invoke(this, other.gameObject);
        }
    }

    public bool IsScared(){
        return scared;
    }
}