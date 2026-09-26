/// CHASE: drone mendekat sampai standoffRange, lalu serah kalau target
/// menjauh melewati loseRange. Belum menembak - itu tugas state Attack.
public class DroneChaseState : IState
{
    private readonly DroneController drone;

    public DroneChaseState(DroneController drone)
    {
        this.drone = drone;
    }

    public void Enter()
    {
    }

    public void Update()
    {
        // loseRange diperiksa sebelum fireRange supaya saat player mundur,
        // drone tidak berhenti untuk menembak ke range yang sudah kosong.
        if (drone.TargetBeyondEngageRange)
        {
            drone.StateMachine.ChangeState(drone.PatrolState);
            return;
        }

        if (drone.TargetInFireRange) drone.StateMachine.ChangeState(drone.AttackState);
    }

    public void FixedUpdate() => drone.ChaseStep();

    public void Exit()
    {
    }
}
