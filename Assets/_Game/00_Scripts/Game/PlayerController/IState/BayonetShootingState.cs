using UnityEngine;

public class BayonetShootingState : IState
{
    private readonly BayonetController controller;
    private float stateTimer;

    public BayonetShootingState(BayonetController controller)
    {
        this.controller = controller;
    }

    public void Enter()
    {
        stateTimer = 0f;
        controller.ExecuteShootAndKnockback();
    }

    public void Update()
    {
        stateTimer += Time.deltaTime;
        if (stateTimer >= controller.ShootTransitionTime)
        {
            controller.StateMachine.ChangeState(controller.IdleState);
        }
    }

    public void FixedUpdate()
    {
        // Tetap memperbarui aim visual saat menembak
        controller.ProcessAimingAndPositioning();
    }

    public void Exit()
    {
        controller.UpdateLastActionTime();
    }
}