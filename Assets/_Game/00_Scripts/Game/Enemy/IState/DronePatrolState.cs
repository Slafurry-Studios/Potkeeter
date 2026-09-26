/// <summary>
/// PATROL: drone menyusuri titik-titik patrol sampai player masuk detectRange.
///
/// Kalau patrolPoints kosong, drone hanya diam di tempat - ini fallback yang
/// aman, bukan bug, supaya prefab yang belum dikasih waypoint tetap jalan.
/// </summary>
public class DronePatrolState : IState
{
    private readonly DroneController drone;

    public DronePatrolState(DroneController drone)
    {
        this.drone = drone;
    }

    public void Enter() => drone.OnEnterPatrol();

    public void Update()
    {
        if (drone.TargetInDetectRange) drone.StateMachine.ChangeState(drone.ChaseState);
    }

    public void FixedUpdate() => drone.PatrolStep();

    public void Exit()
    {
    }
}
