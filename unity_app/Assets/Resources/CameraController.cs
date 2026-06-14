using System.Diagnostics;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class SimpleCameraController : MonoBehaviour
{

    public float mainSpeed = 10.0f; //regular speed
    public float shiftAdd = 250.0f; //multiplied by how long shift is held.  Basically running
    public float maxShift = 250.0f; //Maximum speed when holdin gshift
    public float camSens = 0.5f; //How sensitive it with mouse
    public float mouseVerticalSens = 0.1f;
    public float mouseDragStepPixels = 50f;

    public float scrollWheelSens = 1f;

    //private Vector3 lastMouse = new Vector3(255, 255, 255); //kind of in the middle of the screen, rather than at the top (play)
    private bool leftDragActive;
    private Vector2 leftDragStartPosition;

    private void Start()
    {
        UnityEngine.Debug.Log($"CameraController Start() method");

        transform.position = new Vector3(0.545f, 0.9f, -0.580f);
        transform.rotation = Quaternion.Euler(5.714f, -62.139f, 0f);
    }

    void Update()
    {
        var overUi = IsPointerOverUi();
        var cameraInputBlocked = overUi || UiInputCaptureState.IsTextInputFocused;

        if (cameraInputBlocked)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else if (Input.GetMouseButton(1))
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        else if (Input.GetMouseButton(0))
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        //Mouse  camera angle done.  

        //Keyboard commands
        //float f = 0.0f;
        Vector3 p = GetBaseInput(cameraInputBlocked);
        if (p.sqrMagnitude > 0)
        { // only move while a direction key is pressed

            if (p[0] != 0)
            {
                var angle = 33;
                ApplyOrbit(angle * p[0] * Time.deltaTime);
            }

            //Up arrow or Down arrow is in-and-out movement
            //Q and E are up-and-down movement.
            if (p[2] != 0 || p[1] != 0)
            {
                var translation = new Vector3(0f, p[1], p[2]);
                translation = translation * mainSpeed * Time.deltaTime;
                transform.Translate(translation);
            }
            /*
                        UnityEngine.Debug.Log($"Camera position is {transform.position}, maybe looking at {uiEvents.CameraLookAtPos}");
                        UnityEngine.Debug.Log($"looking at {newLookAtPos}");
                        transform.LookAt(newLookAtPos);
            */
        }

        var scroll = Input.GetAxis("Mouse ScrollWheel");
        //mainSpeed += scroll * scrollWheelSens;
    }

    private static bool IsPointerOverUi()
    {
        return UiInputCaptureState.IsPointerOverTabView;
    }

    private void ApplyOrbit(float angleDelta)
    {
        var orbitPivot = new Vector3(uiEvents.CameraLookAtPos[0], transform.position[1], uiEvents.CameraLookAtPos[2]);
        transform.RotateAround(orbitPivot, Vector3.up, angleDelta);
    }

    private void BeginLeftDrag()
    {
        leftDragActive = true;
        leftDragStartPosition = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
    }

    private void EndLeftDrag()
    {
        leftDragActive = false;
    }

    private Vector3 GetMouseDragInput(bool cameraInputBlocked)
    {

        if (cameraInputBlocked || !Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            if (leftDragActive)
            {
                EndLeftDrag();
            }

            return Vector3.zero;
        }

        if (Input.GetMouseButtonDown(0) || !leftDragActive)
        {
            BeginLeftDrag();
        }

        var dragDelta = new Vector2(Input.mousePosition.x, Input.mousePosition.y) - leftDragStartPosition;
        var pVelocity = Vector3.zero;
        var stepSize = Mathf.Max(mouseDragStepPixels, 0.0001f);

        if (Mathf.Abs(dragDelta.x) >= stepSize && Mathf.Abs(dragDelta.x) >= Mathf.Abs(dragDelta.y))
        {
            pVelocity += new Vector3(dragDelta.x > 0f ? 1f : -1f, 0f, 0f);
        }
        else if (Mathf.Abs(dragDelta.y) >= stepSize)
        {
            pVelocity += new Vector3(0f, dragDelta.y < 0f ? 1f : -1f, 0f);
        }

        return pVelocity;
    }

    private Vector3 GetBaseInput(bool cameraInputBlocked)

    { //returns the basic values, if it's 0 than it's not active.
        Vector3 p_Velocity = GetMouseDragInput(cameraInputBlocked);
        if (cameraInputBlocked)
        {
            return p_Velocity;
        }

        var scrollDirection = IsPointerOverUi() ? 0 : Input.GetAxis("Mouse ScrollWheel");
        //UnityEngine.Debug.Log($"Scroll axis is {scrollDirection}");

        var scrollMultiplier = 20;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || scrollDirection > 0)
        {
            p_Velocity += new Vector3(0, 0, 1 * (scrollDirection != 0 ? scrollMultiplier : 1));
        }
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || scrollDirection < 0)
        {
            p_Velocity += new Vector3(0, 0, -1 * (scrollDirection != 0 ? scrollMultiplier : 1));
        }
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            p_Velocity += new Vector3(1, 0, 0);
        }
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            p_Velocity += new Vector3(-1, 0, 0);
        }
        else if (Input.GetKey(KeyCode.E))
        {
            p_Velocity += new Vector3(0, 1, 0);
        }
        else if (Input.GetKey(KeyCode.Q))
        {
            p_Velocity += new Vector3(0, -1, 0);
        }
        return p_Velocity;
    }
}
