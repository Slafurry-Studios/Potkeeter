using UnityEngine;

public class BayonetParryingState : IState
{
    private readonly BayonetController controller;
    private float stateTimer;

    public BayonetParryingState(BayonetController controller)
    {
        this.controller = controller;
    }

    public void Enter()
    {
        stateTimer = 0f;
        controller.ExecuteParryLogic();
    }

    public void Update()
    {
        stateTimer += Time.deltaTime;
        if (stateTimer >= controller.EffectiveParryTransitionTime)
        {
            controller.OnParryWindowEnded();
        }
    }

    /// <summary>
    /// Memulai ulang siklus parry tanpa lewat IdleState. StateMachine.ChangeState
    /// menolak berpindah ke state yang sedang aktif, jadi parry beruntun tidak
    /// bisa lewat ChangeState dan harus masuk Exit/Enter langsung.
    /// </summary>
    public void Restart()
    {
        Exit();
        Enter();
    }

    public void FixedUpdate()
    {
        // Tetap memperbarui aim visual saat parry
        controller.ProcessAimingAndPositioning();
    }

    public void Exit()
    {
        controller.UpdateLastActionTime();
    }
}