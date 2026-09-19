using UnityEngine;

public class TestAttack : MonoBehaviour
{
    [SerializeField] private float damageAmount = 10f;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerHealth playerHealth = PlayerHealth.Instance;
            playerHealth.Health.TakeDamage(damageAmount);
            Debug.Log($"Player took damage! Current Health: {playerHealth.Health.CurrentHealth}");
        }
        else if(collision.CompareTag("Enemy"))
        {
            EnemyHealth enemyHealth = collision.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.Health.TakeDamage(damageAmount);
                Debug.Log($"Enemy took damage! Current Health: {enemyHealth.Health.CurrentHealth}");
            }
        }
    }
}