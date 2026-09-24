using UnityEngine;

public class BayonetIdleState : IState
{
    private readonly BayonetController controller;

    public BayonetIdleState(BayonetController controller)
    {
        this.controller = controller;
    }

    public void Enter() { }

    public void Update() { }

    public void FixedUpdate()
    {
        controller.ProcessAimingAndPositioning();
    }

    public void Exit() { }
}