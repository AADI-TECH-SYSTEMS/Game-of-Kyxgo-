using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==========================================
// 1. MAIN SUPERHERO ROPE CONTROLLER
// ==========================================
[RequireComponent(typeof(CharacterController))]
public class RopeHeroUnity : MonoBehaviour
{
    [Header("Player Movement")]
    public float moveSpeed = 8.5f;
    public float sprintSpeed = 14f;
    public float jumpHeight = 2.5f;
    public float gravity = -20f;
    public Transform cameraMain;

    [Header("Rope / Grapple Hook")]
    public float maxGrappleDistance = 120f;
    public float grapplePullSpeed = 30f;
    public LayerMask grappleLayer = ~0; // Default all layers

    [Header("Combat & Superhero Skills")]
    public float slamRadius = 8f;
    public int slamDamage = 100;
    public float kickForce = 25f;

    [Header("Inventory & Stats")]
    public int totalCoins = 0;
    public int playerHealth = 100;

    private CharacterController controller;
    private LineRenderer ropeLine;
    private Vector3 playerVelocity;
    private bool isGrounded;
    private bool isGrappling = false;
    private Vector3 grappleTargetPoint;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        ropeLine = GetComponent<LineRenderer>();

        // Auto-configure LineRenderer if missing
        if (ropeLine == null)
        {
            ropeLine = gameObject.AddComponent<LineRenderer>();
            ropeLine.startWidth = 0.08f;
            ropeLine.endWidth = 0.08f;
            ropeLine.material = new Material(Shader.Find("Sprites/Default"));
            ropeLine.startColor = Color.cyan;
            ropeLine.endColor = Color.white;
            ropeLine.positionCount = 0;
        }

        if (cameraMain == null && Camera.main != null)
        {
            cameraMain = Camera.main.transform;
        }
    }

    void Update()
    {
        HandleGroundedCheck();
        HandleMovement();
        HandleGrappleInput();
        HandleSkills();
    }

    void HandleGroundedCheck()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && playerVelocity.y < 0)
        {
            playerVelocity.y = -2f;
        }
    }

    void HandleMovement()
    {
        if (isGrappling) return;

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;

        if (direction.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + (cameraMain != null ? cameraMain.eulerAngles.y : 0f);
            transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);

            Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            controller.Move(moveDirection.normalized * currentSpeed * Time.deltaTime);
        }

        // Jump
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Gravity Apply
        playerVelocity.y += gravity * Time.deltaTime;
        controller.Move(playerVelocity * Time.deltaTime);
    }

    void HandleGrappleInput()
    {
        // Right Mouse Button or 'E' key to Hook
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.E))
        {
            StartGrapple();
        }

        if (isGrappling)
        {
            ExecuteGrapplePull();
        }

        if (Input.GetMouseButtonUp(1) || Input.GetKeyUp(KeyCode.E))
        {
            StopGrapple();
        }
    }

    void StartGrapple()
    {
        if (cameraMain == null) return;

        RaycastHit hit;
        if (Physics.Raycast(cameraMain.position, cameraMain.forward, out hit, maxGrappleDistance, grappleLayer))
        {
            grappleTargetPoint = hit.point;
            isGrappling = true;
            ropeLine.positionCount = 2;
        }
    }

    void ExecuteGrapplePull()
    {
        ropeLine.SetPosition(0, transform.position + Vector3.up * 1.5f);
        ropeLine.SetPosition(1, grappleTargetPoint);

        Vector3 pullDir = (grappleTargetPoint - transform.position).normalized;
        controller.Move(pullDir * grapplePullSpeed * Time.deltaTime);

        // Auto release near target
        if (Vector3.Distance(transform.position, grappleTargetPoint) < 3.5f)
        {
            StopGrapple();
        }
    }

    void StopGrapple()
    {
        isGrappling = false;
        ropeLine.positionCount = 0;
    }

    void HandleSkills()
    {
        // Super Shockwave Attack ('Q' Key)
        if (Input.GetKeyDown(KeyCode.Q))
        {
            ExecuteSuperSlam();
        }
    }

    void ExecuteSuperSlam()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, slamRadius);
        foreach (var hit in hitColliders)
        {
            if (hit.gameObject == gameObject) continue;

            CivilianAI civilian = hit.GetComponent<CivilianAI>();
            if (civilian != null)
            {
                Vector3 blastDir = (hit.transform.position - transform.position).normalized * kickForce;
                civilian.TakeDamage(slamDamage, blastDir);
            }
        }
    }

    public void AddCoins(int amount)
    {
        totalCoins += amount;
        Debug.Log("[ROPE HERO] Coins Collected: " + totalCoins);
    }
}

// ==========================================
// 2. INDIAN CITY NPC CIVILIAN & TRAFFIC AI
// ==========================================
public class CivilianAI : MonoBehaviour
{
    public float walkSpeed = 2.5f;
    public int health = 50;
    public int coinDropAmount = 25;

    private Vector3 roamDirection;
    private float directionTimer = 0f;
    private bool isDead = false;

    void Start()
    {
        PickNewDirection();
    }

    void Update()
    {
        if (isDead) return;

        transform.Translate(roamDirection * walkSpeed * Time.deltaTime);
        directionTimer -= Time.deltaTime;
        if (directionTimer <= 0)
        {
            PickNewDirection();
        }
    }

    void PickNewDirection()
    {
        float angle = Random.Range(0, 360);
        transform.rotation = Quaternion.Euler(0, angle, 0);
        roamDirection = Vector3.forward;
        directionTimer = Random.Range(3f, 7f);
    }

    public void TakeDamage(int damage, Vector3 knockback)
    {
        if (isDead) return;

        health -= damage;
        SpawnBloodVFX();

        if (health <= 0)
        {
            Die(knockback);
        }
    }

    void SpawnBloodVFX()
    {
        // Creates a dynamic red blood particle without needing external assets
        GameObject bloodObj = new GameObject("BloodFX");
        bloodObj.transform.position = transform.position + Vector3.up;

        ParticleSystem ps = bloodObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = Color.red;
        main.startSize = 0.2f;
        main.startLifetime = 0.5f;
        main.startSpeed = 3f;
        main.duration = 0.2f;
        main.loop = false;

        ps.Play();
        Destroy(bloodObj, 1f);
    }

    void Die(Vector3 knockback)
    {
        isDead = true;
        SpawnCoinDrop();

        // Enable physics ragdoll knockback
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.AddForce(knockback, ForceMode.Impulse);

        Destroy(gameObject, 4f);
    }

    void SpawnCoinDrop()
    {
        GameObject coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        coin.name = "CoinReward";
        coin.transform.position = transform.position + Vector3.up * 0.5f;
        coin.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);

        // Gold Color
        Renderer rend = coin.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = Color.yellow;
        }

        CoinPickup pickup = coin.AddComponent<CoinPickup>();
        pickup.coinValue = coinDropAmount;
    }
}

// ==========================================
// 3. COIN PICKUP TRIGGER MECHANISM
// ==========================================
public class CoinPickup : MonoBehaviour
{
    public int coinValue = 10;
    public float rotationSpeed = 120f;

    void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        RopeHeroUnity hero = other.GetComponent<RopeHeroUnity>();
        if (hero != null)
        {
            hero.AddCoins(coinValue);
            Destroy(gameObject);
        }
    }
}
