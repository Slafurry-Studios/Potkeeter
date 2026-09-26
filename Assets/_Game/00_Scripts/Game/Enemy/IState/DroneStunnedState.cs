/// STUNNED: hasil dari parry. Drone lumpuh sebentar dan tidak menyentuh
/// velocity sama sekali, supaya impuls knockback dari BayonetController bebas
/// meredup sendiri lewat drag. Kalau velocity ditulis di sini, knockback itu
/// akan langsung ditimpa frame berikutnya dan parry terasa tidak mengubah apa-apa.
public class DroneStunnedState : IState
{
    private readonly DroneController drone;

    public DroneStunnedState(DroneController drone)
    {
        this.drone = drone;
    }

    public void Enter()
    {
    }

    public void Update()
    {
        drone.UpdateStun();
        if (drone.IsStunned) return;

        // Setelah pulih, langsung dikejar lagi kalau player masih di area.
        // kalau tidak sah ya sudah tidak ada yang dikejar.
        drone.StateMachine.ChangeState(drone.TargetInDetectRange ? drone.ChaseState : drone.PatrolState);
    }

    // Sengaja kosong: biarkan Rigidbody2D yang menggerakkan drone ke mana saja
    // selama knockback mereda.
    public void FixedUpdate()
    {
    }

    public void Exit()
    {
    }
}
