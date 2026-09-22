using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class CollideTrigger : MonoBehaviour
{
    
    [SerializeField] private UnityEvent onTriggerEnter;
    [SerializeField] private UnityEvent onTriggerExit;


    private void OnTriggerEnter2D(Collider2D other)
    {
        onTriggerEnter?.Invoke();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        onTriggerExit?.Invoke();
    }
}