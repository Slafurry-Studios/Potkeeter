using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(EnemyHealth))]
public class DummyEnemyTest : MonoBehaviour, IParryable
{
    [Header("Attack Timing Settings")]
    [SerializeField] private float attackInterval = 3f;
    [SerializeField] private float windUpDuration = 0.4f;
    [SerializeField] private float activeHitboxDuration = 0.2f;
    [Tooltip("Berapa lama lumpuh setelah diparry, hanya untuk feedback visual dummy ini.")]
    [SerializeField] private float parryStunVisualDuration = 1.5f;

    [Header("Attack Hitbox Settings")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 0.8f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Visual Feedback")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color warningColor = Color.yellow;   // Wind-up awal
    [SerializeField] private Color attackColor = Color.red;       // Active hit
    [SerializeField] private Color parriedColor = Color.gray;     // Sedang dicounter parry

    public StateMachine StateMachine { get; private set; }
    public IdleState idleState { get; private set; }
    public AttackState attackState { get; private set; }
    public ParriedState parriedState { get; private set; }

    public bool IsHurtboxActive { get; private set; }

    private EnemyHealth enemyHealth;
    private Rigidbody2D rb;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        // Inisialisasi State Machine & States
        StateMachine = new StateMachine();
        idleState = new IdleState(this);
        attackState = new AttackState(this);
        parriedState = new ParriedState(this);
    }

    private void Start()
    {
        StateMachine.Initialize(idleState);
    }

    private void Update()
    {

        StateMachine.Update();
    }

    private void FixedUpdate()
    {
        StateMachine.FixedUpdate();
    }

    /// <summary>
    /// Dipanggil saat dummy ini berada di area parry. Tidak ada syarat apa
    /// pun: area parry yang menentukan, bukan state dummy.
    /// </summary>
    public void OnParried() => StateMachine.ChangeState(parriedState);

    public void SetColor(Color color)
    {
        if (spriteRenderer != null) spriteRenderer.color = color;
    }

    public void PerformDamageCheck()
    {
        Vector2 point = attackPoint != null ? attackPoint.position : transform.position;
        Collider2D playerCollider = Physics2D.OverlapCircle(point, attackRadius, playerLayer);

        if (playerCollider != null)
        {
            PlayerHealth playerHealth = playerCollider.GetComponentInParent<PlayerHealth>();
            playerHealth?.Health.TakeDamage(attackDamage);
            Debug.Log("<color=red>[Dummy Enemy] Attack Hit Player!</color>");
        }
    }

    public void SetHurtboxActiveState(bool state) => IsHurtboxActive = state;

    private void OnDrawGizmos()
    {
        Vector2 point = attackPoint != null ? attackPoint.position : transform.position;

        if (IsHurtboxActive)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(point, attackRadius);
        }
        else
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(point, attackRadius);
        }
    }

    #region State Implementations

    public class IdleState : IState
    {
        private readonly DummyEnemyTest enemy;
        private float timer;

        public IdleState(DummyEnemyTest enemy) => this.enemy = enemy;

        public void Enter()
        {
            enemy.SetColor(enemy.idleColor);
            enemy.SetHurtboxActiveState(false);
            timer = 0f;
        }

        public void Update()
        {
            timer += Time.deltaTime;
            if (timer >= enemy.attackInterval)
            {
                enemy.StateMachine.ChangeState(enemy.attackState);
            }
        }

        public void FixedUpdate() { }
        public void Exit() { }
    }

    public class AttackState : IState
    {
        private readonly DummyEnemyTest enemy;
        private Coroutine attackRoutine;

        public AttackState(DummyEnemyTest enemy) => this.enemy = enemy;

        public void Enter()
        {
            attackRoutine = enemy.StartCoroutine(AttackSequence());
        }

        private IEnumerator AttackSequence()
        {
            // 1. Wind-Up (kuning) - memberi tanda serangan mau masuk
            enemy.SetColor(enemy.warningColor);
            yield return new WaitForSeconds(enemy.windUpDuration);

            // 2. Active Hitbox (merah) - damage masuk. Selama durasi ini
            // dummy tetap bisa diparry, karena yang menentukan cuma apakah
            // dia masih berada di area parry.
            enemy.SetHurtboxActiveState(true);
            enemy.SetColor(enemy.attackColor);

            enemy.PerformDamageCheck();
            yield return new WaitForSeconds(enemy.activeHitboxDuration);

            // Kembali ke Idle setelah serangan selesai
            enemy.SetHurtboxActiveState(false);
            enemy.StateMachine.ChangeState(enemy.idleState);
        }

        public void Update() { }
        public void FixedUpdate() { }

        public void Exit()
        {
            if (attackRoutine != null) enemy.StopCoroutine(attackRoutine);
            enemy.SetHurtboxActiveState(false);
        }
    }

    public class ParriedState : IState
    {
        private readonly DummyEnemyTest enemy;
        private float timer;

        public ParriedState(DummyEnemyTest enemy) => this.enemy = enemy;

        public void Enter()
        {
            enemy.SetColor(enemy.parriedColor);
            enemy.SetHurtboxActiveState(false);
            timer = 0f;
            Debug.Log("<color=cyan>[Dummy Enemy] PARRIED!</color>");
        }

        public void Update()
        {
            timer += Time.deltaTime;
            if (timer >= enemy.parryStunVisualDuration)
            {
                enemy.StateMachine.ChangeState(enemy.idleState);
            }
        }

        public void FixedUpdate() { }
        public void Exit() { }
    }

    #endregion
}