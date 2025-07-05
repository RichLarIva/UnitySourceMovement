using UnityEngine;
using UnityEngine.InputSystem;

public class SourceGrabber : MonoBehaviour
{
    public Transform holdPoint;
    public float grabRange = 4f;
    public float grabForce = 500f;
    public LayerMask grabbableLayer;

    private Rigidbody heldObject;
    [SerializeField]private Camera cam;

    void Start() => cam = Camera.main;

    public void OnGrab(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (heldObject == null)
                TryGrab();
            else
                DropObject();
        }
    }

    public void OnThrow(InputAction.CallbackContext context)
    {
        if (heldObject != null && context.performed)
        {
            ThrowObject();
        }
    }

    void FixedUpdate()
    {
        if (heldObject != null)
        {
            Vector3 toHoldPoint = holdPoint.position - heldObject.position;
            heldObject.linearVelocity = toHoldPoint * grabForce * Time.fixedDeltaTime;
        }
    }

    void TryGrab()
    {
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, grabRange, grabbableLayer))
        {
            if (hit.rigidbody != null)
            {
                heldObject = hit.rigidbody;
                heldObject.useGravity = false;
                heldObject.linearDamping = 10f;  // linearDamping is now drag in Unity's Rigidbody
            }
        }
    }

    void DropObject()
    {
        if (heldObject != null)
        {
            heldObject.useGravity = true;
            heldObject.linearDamping = 0f;
            heldObject = null;
        }
    }

    void ThrowObject()
    {
        heldObject.useGravity = true;
        heldObject.linearDamping = 0f;
        heldObject.linearVelocity = cam.transform.forward * 10f;
        heldObject = null;
    }
}
