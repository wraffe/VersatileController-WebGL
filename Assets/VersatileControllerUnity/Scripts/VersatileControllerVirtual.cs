using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using Photon.Pun;

// This is the application side of the versatile controller. Use the public functions provided to subscribe
// to events from the controller (or if appropriate) to poll the current state of controls.
public class VersatileControllerVirtual : MonoBehaviour
{
    [System.Serializable]
    public class Skins
    {
        public string name;
        public VersatileControllerPhysical.Handedness whichHand;
        public GameObject[] parts;
    }

    public TextMeshProUGUI debug;

    public Vector3 positionReset = new Vector3(0.0f, 1.0f, -1.0f);
    public Vector3 rotationReset = Vector3.zero;
    private Quaternion androidResetOffset = Quaternion.identity;

    public bool positionActive = true;
    public bool rotationActive = true;

    private Rigidbody rb;

    public Skins[] skins;

    // Event for tracking when new controllers are added.
    private static UnityEvent<GameObject> newControllers;
    private static List<GameObject> knownControllers;
    private static Dictionary<GameObject, string> controllerObjects;

    private static bool initialized = false;
    private static void initialize()
    {
        if (!initialized)
        {
            newControllers = new UnityEvent<GameObject>();
            knownControllers = new List<GameObject>();
            controllerObjects = new Dictionary<GameObject, string>();
            initialized = true;
        }
    }

    // Register to receive a callback whenever a new controller connects. The callback
    // will be provided with the gameobject representing the controller. This gameobject
    // will have a VersatileControllerVirtual component. This method is static, so you
    // don't need any existing controller before you can register to learn about new
    // controllers.
    public static void subscribeNewControllers(UnityAction<GameObject> call)
    {
        initialize();
        newControllers.AddListener(call);

        // Inform of all controllers that have already connected.
        foreach (GameObject g in knownControllers)
        {
            newControllers.Invoke(g);
        }
    }

    // Set the skin and handedness for a given controller representation. The 
    // appropriate skin needs to be part of the virtual controller prefab.
    private void setSkin(string skinName, bool isLeftHanded)
    {
        // Switch off all skins
        foreach (Skins s in skins)
        {
            foreach (GameObject g in s.parts)
            {
                g.SetActive(false);
            }
        }

        // Enable the active skin.
        foreach (Skins s in skins)
        {
            if ((s.name == skinName) &&
                ((s.whichHand == VersatileControllerPhysical.Handedness.BothHands) ||
                ((s.whichHand == VersatileControllerPhysical.Handedness.LeftHanded) == isLeftHanded)))
            {
                foreach (GameObject g in s.parts)
                {
                    g.SetActive(true);
                }
            }
        }
    }

    // This function is called (remotely) by the physical controller whenever the controller
    // application starts.
    [PunRPC]
    public void ControllerStarted(string name, bool isLeftHanded, string skinName)
    {
        initialize();
        classInitialize();

        setSkin(skinName, isLeftHanded);

        this.gameObject.name = name;
        if (!knownControllers.Contains(this.gameObject))
        {
            // A new controller has been started. Unlikely to get duplicates, but checking anyway.
            knownControllers.Add(this.gameObject);
            controllerObjects[this.gameObject] = name;
            newControllers.Invoke(this.gameObject);

            ResetPose(true, Quaternion.identity);
            rb = GetComponent<Rigidbody>();
        }
        nameUpdates.Invoke(name, isLeftHanded, skinName);
    }

    // Event tracking for button presses.

    private bool classInitialized = false;

    // This function ensures that all data structures are initialized. Each internal
    // function calls this, so that initialization state is guaranteed, regardless of 
    // the Unity initialization sequence.
    private void classInitialize()
    {
        if (!classInitialized)
        {
            buttonDownEvents = new Dictionary<string, UnityEvent<string, VersatileControllerVirtual>>();
            buttonUpEvents = new Dictionary<string, UnityEvent<string, VersatileControllerVirtual>>();
            sliderEvents = new Dictionary<string, UnityEvent<string, float, VersatileControllerVirtual>>();

            allButtonDownEvents = new UnityEvent<string, VersatileControllerVirtual>();
            allButtonUpEvents = new UnityEvent<string, VersatileControllerVirtual>();
            allSliderEvents = new UnityEvent<string, float, VersatileControllerVirtual>();

            buttonState = new Dictionary<string, bool>();
            sliderState = new Dictionary<string, float>();

            poseEvents = new UnityEvent<GameObject, Quaternion, Vector3>();
            nameUpdates = new UnityEvent<string, bool, string>();
            classInitialized = true;
        }
    }

    // Dictionaries to map button/slider names to various callbacks and state data.
    private Dictionary<string, UnityEvent<string, VersatileControllerVirtual>> buttonDownEvents;
    private UnityEvent<string, VersatileControllerVirtual> allButtonDownEvents;
    private Dictionary<string, UnityEvent<string, VersatileControllerVirtual>> buttonUpEvents;
    private UnityEvent<string, VersatileControllerVirtual> allButtonUpEvents;
    private Dictionary<string, bool> buttonState;

    private Dictionary<string, UnityEvent<string, float, VersatileControllerVirtual>> sliderEvents;
    private UnityEvent<string, float, VersatileControllerVirtual> allSliderEvents;
    private Dictionary<string, float> sliderState;

    private UnityEvent<GameObject, Quaternion, Vector3> poseEvents;
    private UnityEvent<string, bool, string> nameUpdates;

    // Register to receive a callback whenever the name of the controller is updated.
    public void subscribeNameUpdates(UnityAction<string, bool, string> call)
    {
        classInitialize();
        nameUpdates.AddListener(call);
    }

    // Use this to receive call backs whenever the named button is pressed. 
    // The callback provides the name of the button, so that the callback
    // can be used to subscribe to multiple buttons.
    // If button is null, then subscribe to all button down events.
    public void subscribeButtonDown(string button, UnityAction<string, VersatileControllerVirtual> call)
    {
        classInitialize();
        if ((button != null) && (!buttonDownEvents.ContainsKey(button)))
        {
            buttonDownEvents[button] = new UnityEvent<string, VersatileControllerVirtual>();
            buttonState[button] = false;
        }

        if (button == null)
        {
            allButtonDownEvents.AddListener(call);
        }
        else
        {
            buttonDownEvents[button].AddListener(call);
        }
    }

    // Use this to receive call backs whenever the named button is released.
    public void subscribeButtonUp(string button, UnityAction<string, VersatileControllerVirtual> call)
    {
        classInitialize();
        if ((button != null) && (!buttonUpEvents.ContainsKey(button)))
        {
            buttonUpEvents[button] = new UnityEvent<string, VersatileControllerVirtual>();
            buttonState[button] = false;
        }

        if (button == null)
        {
            allButtonUpEvents.AddListener(call);
        }
        else
        {
            buttonUpEvents[button].AddListener(call);
        }
    }

    // Subscribe to controller events.
    public void subscribeSlider(string slider, UnityAction<string, float, VersatileControllerVirtual> call)
    {
        classInitialize();
        if ((slider != null) && (!sliderEvents.ContainsKey(slider)))
        {
            sliderEvents[slider] = new UnityEvent<string, float, VersatileControllerVirtual>();
            sliderState[slider] = 0.0f;
        }

        if (slider == null)
        {
            allSliderEvents.AddListener(call);
        }
        else
        {
            sliderEvents[slider].AddListener(call);
        }
    }

    // Event tracking for pose updates
    // Subscribe to updates whenever the physical controller pose changes (i.e. it is moved).
    public void subscribePose(UnityAction<GameObject, Quaternion, Vector3> call)
    {
        classInitialize();
        poseEvents.AddListener(call);
    }

    // State checking.

    // Returns the state of the given button. Returns false if the button has
    // never provided a state update, or doesn't exist.
    public bool getButtonState(string button)
    {
        if (buttonState.ContainsKey(button))
        {
            return buttonState[button];
        }
        return false;
    }

    // Returns the last known value of the given slider. Returns 0 if the slider
    // doesn't exist or has never provided any value updates.
    public float getSliderState(string slider)
    {
        if (sliderState.ContainsKey(slider))
        {
            return sliderState[slider];
        }
        return 0.0f;
    }

    // Called from the physical controller to indicate a button has been pressed.
    [PunRPC]
    public void SendButtonDown(string button, string systemID, string controllerID, PhotonMessageInfo info)
    {
        classInitialize();
        if (buttonDownEvents.ContainsKey(button))
        {
            buttonState[button] = true;
            buttonDownEvents[button].Invoke(button, this);
        }
        allButtonDownEvents.Invoke(button, this);
    }

    // Called from the physical controller to indicate a button has been released.
    [PunRPC]
    public void SendButtonUp(string button, string systemID, string controllerID, PhotonMessageInfo info)
    {
        classInitialize();
        if (buttonUpEvents.ContainsKey(button))
        {
            buttonState[button] = false;
            buttonUpEvents[button].Invoke(button, this);
        }
        allButtonUpEvents.Invoke(button, this);
    }

    // Called from the physical controller to indicate a slider value has changed.
    [PunRPC]
    public void SendSliderChanged(string slider, float value, string systemID, string controllerID, PhotonMessageInfo info)
    {
        classInitialize();
        if (sliderEvents.ContainsKey(slider))
        {
            sliderState[slider] = value;
            sliderEvents[slider].Invoke(slider, value, this);
        }
        allSliderEvents.Invoke(slider, value, this);
    }

    // For Android builds using ARTrackable. Called from the physical controller to communicate pose updates.
    [PunRPC]
    void SendControlInfo(float x, float y, float z, float w, float px, float py, float pz, PhotonMessageInfo info)
    {
        classInitialize();

        Quaternion o = new Quaternion(x, y, z, w);
        Vector3 p = new Vector3(px, py, pz);

        poseEvents.Invoke(this.gameObject, o, p);

        if (rotationActive)
            transform.localRotation = o;
        if (positionActive) 
            transform.localPosition = p;
        
    }

    // For using WebGL. More variables needed. 
    [PunRPC]
    void SendControlInfoWebGL(bool reset, bool isIOS, float x, float y, float z, float w, float gx, float gy, float gz, float px, float py, float pz)
    {
        classInitialize();

        Quaternion attitude = new Quaternion(-x, -z, -y, w); //Only for Android
        Vector3 grav = new Vector3(gx, gy, gz);
        Vector3 accel = new Vector3(px, pz, -py);

        // If the reset flag is set, then recenter the avatar and stop
        if (reset)
            ResetPose(isIOS, attitude);
           

        if (rotationActive)
        {
            if (isIOS)
            {
                Debug.LogWarning("iOS");
                transform.Rotate(new Vector3(-x, -z, -y), Space.Self);

                // Correct for rotational drift in iOS. This comes from slight differences in the gyro.updateInterval and the phone's frame rate.
                Recentre(grav);
            }
            else
            {
                /* Attitude sensor works in Android WebGL and is more accurate, so use that instead.
                 * Attitude includes the compass direction, so use the recentre button to create a rotational offset. */
                transform.rotation = androidResetOffset * attitude;
            }
        }

        if (positionActive)
        {
            // Position tracking is not working yet in WebGL.
            // However, you can access the users input accelaration on the phone from px, py, and pz or the accel variable. 
            // Note these values may be different between iOS and Android
            Debug.Log("User Velocity: " + accel);            
        }

        poseEvents.Invoke(this.gameObject, transform.rotation, transform.position);
    }


    private void Recentre(Vector3 grav)
    {
        // Values either side of 0 go negative and positive
        // Values either side of 1 or negative one go back to zero

        /* Apple
         * Portrait up (standard hold) - (0, -1, 0) 
         * Portrait down (upside down hold) - (0, 1, 0) 
         * Flat on table - (0, 0, -1) 
         * Upside down on table - (0, 0, 1) 
         * Right side lean - (1, 0, 0) 
         * Left side lean - (-1, 0, 0) 
         */

        // Portrait up
        if (VecApprox(grav, new Vector3(0, -1, 0)))
            transform.rotation = Quaternion.Euler(-90, transform.eulerAngles.y, transform.eulerAngles.z);
        // Portrait down
        if (VecApprox(grav, new Vector3(0, 1, 0)))
            transform.rotation = Quaternion.Euler(90, transform.eulerAngles.y, transform.eulerAngles.z);
        // Flat on table
        if (VecApprox(grav, new Vector3(0, 0, -1)))
            transform.rotation = Quaternion.Euler(0.0f, transform.eulerAngles.y, 0.0f);
        // Upside down on table
        if (VecApprox(grav, new Vector3(0, 0, 1)))
            transform.rotation = Quaternion.Euler(0.0f, transform.eulerAngles.y, 180.0f);
        // Right side lean
        if (VecApprox(grav, new Vector3(1, 0, 0)))
            transform.rotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, -90.0f);
        // Left side lean
        if (VecApprox(grav, new Vector3(-1, 0, 0)))
            transform.rotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, 90.0f);

    }

    private void ResetPose(bool isIOS, Quaternion attitude)
    {
        transform.position = positionReset;
        transform.rotation = Quaternion.Euler(rotationReset);
        if (!isIOS)
        {
            //transform.parent.rotation = Quaternion.FromToRotation(transform.parent.rotation.eulerAngles, transform.localRotation.eulerAngles);
            androidResetOffset = transform.rotation * Quaternion.Inverse(attitude);
        }

        poseEvents.Invoke(this.gameObject, transform.rotation, transform.position);
    }


    private bool VecApprox(Vector3 val, Vector3 target)
    {
        // Dot product to check alignement  between gravity vector and target vector
        return (Vector3.Dot(val, target) > 0.995);
    }
}
