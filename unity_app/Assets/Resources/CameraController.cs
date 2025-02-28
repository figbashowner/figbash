using System.Diagnostics;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class SimpleCameraController : MonoBehaviour
{

    public float mainSpeed = 10.0f; //regular speed
    public float shiftAdd = 250.0f; //multiplied by how long shift is held.  Basically running
    public float maxShift = 250.0f; //Maximum speed when holdin gshift
    public float camSens = 0.25f; //How sensitive it with mouse
    public bool invertY = true;

    public float scrollWheelSens = 1f;

    //private Vector3 lastMouse = new Vector3(255, 255, 255); //kind of in the middle of the screen, rather than at the top (play)

    private void Start()
    {
        UnityEngine.Debug.Log($"CameraController Start() method");

        transform.position = new Vector3(0.545f, 0.9f, -0.580f);
        transform.rotation = Quaternion.Euler(5.714f, -62.139f, 0f);
    }

    void Update()
    {
        if (Input.GetMouseButton(1))
        {
            var mouseMoveY = invertY ? -1 * Input.GetAxis("Mouse Y") : Input.GetAxis("Mouse Y");
            var mouseMoveX = Input.GetAxis("Mouse X");

            var mouseMove = new Vector3(mouseMoveY, mouseMoveX, 0) * camSens;
            transform.eulerAngles = transform.eulerAngles + mouseMove;
        }

        if (Input.GetMouseButtonDown(1))
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        if (Input.GetMouseButtonUp(1))
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        //Mouse  camera angle done.  

        //Keyboard commands
        //float f = 0.0f;
        Vector3 p = GetBaseInput();
        if (p.sqrMagnitude > 0)
        { // only move while a direction key is pressed

            //Up arrow or Down arrow is in-and-out movement
            //Q and E are up-and-down movement.
            if (p[2] != 0 || p[1] != 0)
            {
                p = p * mainSpeed;
                p = p * Time.deltaTime;
                transform.Translate(p);
            }
            else
            {
                var angle = 33;
                Vector3 newLookAtPos = new Vector3(uiEvents.CameraLookAtPos[0], transform.position[1], uiEvents.CameraLookAtPos[2]);
                transform.RotateAround(newLookAtPos, Vector3.up,  angle * p[0] *  Time.deltaTime);
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

    private Vector3 GetBaseInput()

    { //returns the basic values, if it's 0 than it's not active.
        var scrollDirection = Input.GetAxis("Mouse ScrollWheel");
        //UnityEngine.Debug.Log($"Scroll axis is {scrollDirection}");

        var scrollMultiplier = 20;
        Vector3 p_Velocity = new Vector3();
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