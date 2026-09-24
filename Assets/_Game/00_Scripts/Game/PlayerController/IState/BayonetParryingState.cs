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
        if (stateTimer >= controller.ParryTransitionTime)
        {
            controller.StateMachine.ChangeState(controller.IdleState);
        }
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