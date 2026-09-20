using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(EnemyHealth))]
public class DummyEnemyTest : MonoBehaviour, IDamageableParryable
{
    [Header("Attack Timing Settings")]
    [SerializeField] private float attackInterval = 3f;
    [SerializeField] private float windUpDuration = 0.4f;
    [SerializeField] private float parryWindowDuration = 0.2f; // Jendela waktu emas parry (sebelum hit)
    [SerializeField] private float activeHitboxDuration = 0.2f;
    [SerializeField] private float stunDuration = 1.5f;

    [Header("Attack Hitbox Settings")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 0.8f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Visual Feedback")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color warningColor = Color.yellow;   // Wind-up awal
    [SerializeField] private Color parryableColor = Color.cyan;   // Parry window aktif!
    [SerializeField] private Color attackColor = Color.red;       // Active hit (Sudah terlambat diparry)
    [SerializeField] private Color stunColor = Color.gray;        // Stun saat ter-parry

    public StateMachine StateMachine { get; private set; }
    public IdleState idleState { get; private set; }
    public AttackState attackState { get; private set; }
    public StunnedState stunnedState { get; private set; }

    public bool IsParryable { get; private set; }
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
        stunnedState = new StunnedState(this);
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

    public bool TryParry()
    {
        if (IsParryable)
        {
            StateMachine.ChangeState(stunnedState);
            return true;
        }
        return false;
    }

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

    public void SetParryableState(bool state) => IsParryable = state;
    public void SetHurtboxActiveState(bool state) => IsHurtboxActive = state;

    private void OnDrawGizmos()
    {
        Vector2 point = attackPoint != null ? attackPoint.position : transform.position;

        if (IsHurtboxActive)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(point, attackRadius);
        }
        else if (IsParryable)
        {
            Gizmos.color = Color.cyan;
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
            enemy.SetParryableState(false);
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
            // 1. Wind-Up Phase (Warning - Belum bisa diparry)
            enemy.SetColor(enemy.warningColor);
            yield return new WaitForSeconds(enemy.windUpDuration);

            // 2. Parry Window Phase (Cyan - Window Emas untuk Player Parry!)
            enemy.SetColor(enemy.parryableColor);
            enemy.SetParryableState(true);
            yield return new WaitForSeconds(enemy.parryWindowDuration);

            // 3. Active Hitbox Phase (Merah - Serangan Aktif & Damage Masuk)
            enemy.SetParryableState(false);
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
            enemy.SetParryableState(false);
            enemy.SetHurtboxActiveState(false);
        }
    }

    public class StunnedState : IState
    {
        private readonly DummyEnemyTest enemy;
        private float timer;

        public StunnedState(DummyEnemyTest enemy) => this.enemy = enemy;

        public void Enter()
        {
            enemy.SetColor(enemy.stunColor);
            enemy.SetParryableState(false);
            enemy.SetHurtboxActiveState(false);
            timer = 0f;
            Debug.Log("<color=cyan>[Dummy Enemy] STUNNED / PARRIED!</color>");
        }

        public void Update()
        {
            timer += Time.deltaTime;
            if (timer >= enemy.stunDuration)
            {
                enemy.StateMachine.ChangeState(enemy.idleState);
            }
        }

        public void FixedUpdate() { }
        public void Exit() { }
    }

    #endregion
}