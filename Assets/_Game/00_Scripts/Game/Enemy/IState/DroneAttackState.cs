/// ATTACK: drone berhenti di standoffRange, menghadap target, lalu meminta
/// semua larasnya ticked. Tiap EnemyShooter punya cooldown sendiri, jadi
/// drone dengan dua laras akan menembak dari keduanya dengan ritme terpisah.
public class DroneAttackState : IState
{
    // Sedikit lebih longgar dari fireRange, jadi drone tidak keluar-masuk
    // Attack setiap kali player bergerak sedikit di ujung jangkauan.
    private const float ReengageSlack = 1.15f;

    private readonly DroneController drone;

    public DroneAttackState(DroneController drone)
    {
        this.drone = drone;
    }

    public void Enter()
    {
    }

    public void Update()
    {
        if (drone.TargetBeyondEngageRange)
        {
            drone.StateMachine.ChangeState(drone.PatrolState);
            return;
        }

        if (drone.DistanceToTarget > drone.FireRange * ReengageSlack)
        {
            drone.StateMachine.ChangeState(drone.ChaseState);
            return;
        }

        drone.TickShooters();
    }

    public void FixedUpdate() => drone.AttackStep();

    public void Exit()
    {
    }
}
