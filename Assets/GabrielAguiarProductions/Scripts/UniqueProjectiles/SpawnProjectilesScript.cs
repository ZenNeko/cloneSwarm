//
//NOTES:
//This script is used for DEMONSTRATION porpuses of the Projectiles. I recommend everyone to create their own code for their own projects.
//This is just a basic example.
//

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Input = InputWrapper;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SpawnProjectilesScript : MonoBehaviour {

    public bool useTarget;
	public bool use2D;
	public bool cameraShake;
	public Text effectName;
	public RotateToMouseScript rotateToMouse;
	public GameObject firePoint;
	public GameObject cameras;
    public GameObject target;
    public List<GameObject> VFXs = new List<GameObject> ();

	private int count = 0;
	private float timeToFire = 0f;
	private GameObject effectToSpawn;
	private List<Camera> camerasList = new List<Camera> ();
	private Camera singleCamera;

	void Start () {

		if (cameras.transform.childCount > 0) {
			for (int i = 0; i < cameras.transform.childCount; i++) {
				camerasList.Add (cameras.transform.GetChild (i).gameObject.GetComponent<Camera> ());
			}
			if(camerasList.Count == 0){
				Debug.Log ("Please assign one or more Cameras in inspector");
			}
		} else {
			singleCamera = cameras.GetComponent<Camera> ();
			if (singleCamera != null)
				camerasList.Add (singleCamera);
			else
				Debug.Log ("Please assign one or more Cameras in inspector");
		}

		if(VFXs.Count>0)
			effectToSpawn = VFXs[0];
		else
			Debug.Log ("Please assign one or more VFXs in inspector");
		
		if (effectName != null) effectName.text = effectToSpawn.name;

		if (camerasList.Count > 0) {
			rotateToMouse.SetCamera (camerasList [camerasList.Count - 1]);
			if(use2D)
				rotateToMouse.Set2D (true);
			rotateToMouse.StartUpdateRay ();
		}
		else
			Debug.Log ("Please assign one or more Cameras in inspector");

        if (useTarget && target != null)
        {
            var collider = target.GetComponent<BoxCollider>();
            if (!collider)
            {
                target.AddComponent<BoxCollider>();
            }
        }
    }

	void Update () {
		if (Input.GetKey (KeyCode.Space) && Time.time >= timeToFire || Input.GetMouseButton (0) && Time.time >= timeToFire) {
			timeToFire = Time.time + 1f / effectToSpawn.GetComponent<ProjectileMoveScript>().fireRate;
			SpawnVFX ();	
		}

		if (Input.GetKeyDown (KeyCode.D))
			Next ();
		if (Input.GetKeyDown (KeyCode.A)) 
			Previous ();	
		if (Input.GetKeyDown (KeyCode.C))
			SwitchCamera ();	
		if (Input.GetKeyDown (KeyCode.Alpha1))
			CameraShake ();
		if (Input.GetKeyDown (KeyCode.X))
			ZoomIn ();
		if (Input.GetKeyDown (KeyCode.Z))
			ZoomOut ();
	}

	public void SpawnVFX () {
		GameObject vfx;

		var cameraShakeScript = cameras.GetComponent<CameraShakeSimpleScript> ();

		if (cameraShake && cameraShakeScript != null)
			cameraShakeScript.ShakeCamera ();

		if (firePoint != null) {
			vfx = Instantiate (effectToSpawn, firePoint.transform.position, Quaternion.identity);
            if (!useTarget)
            {
                if (rotateToMouse != null)
                {
                    vfx.transform.localRotation = rotateToMouse.GetRotation();
                }
                else Debug.Log("No RotateToMouseScript found on firePoint.");
            }
            else
            {
                if (target != null)
                {                    
                    vfx.GetComponent<ProjectileMoveScript>().SetTarget(target, rotateToMouse);
                    rotateToMouse.RotateToMouse(vfx, target.transform.position);                    
                }
                else
                {
                    Destroy(vfx);
                    Debug.Log("No target assigned.");
                }
            }
		}
		else
			vfx = Instantiate (effectToSpawn);		
	}

	public void Next () {
		count++;

		if (count > VFXs.Count)
			count = 0;

		for(int i = 0; i < VFXs.Count; i++){
			if (count == i)	effectToSpawn = VFXs [i];
			if (effectName != null)	effectName.text = effectToSpawn.name;
		}
	}

	public void Previous () {
		count--;

		if (count < 0)
			count = VFXs.Count;

		for (int i = 0; i < VFXs.Count; i++) {
			if (count == i) effectToSpawn = VFXs [i];
			if (effectName != null)	effectName.text = effectToSpawn.name;
		}
	}

	public void CameraShake () {
		cameraShake = !cameraShake;
	}

	public void ZoomIn () {
		if (camerasList.Count > 0) {
			if (!camerasList [0].orthographic) {
				if (camerasList [0].fieldOfView < 101) {
					for (int i = 0; i < camerasList.Count; i++) {
						camerasList [i].fieldOfView += 5;
					}
				}
			} else {
				if (camerasList [0].orthographicSize < 10) {
					for (int i = 0; i < camerasList.Count; i++) {
						camerasList [i].orthographicSize += 0.5f;
					}
				}
			}
		}
	}

	public void ZoomOut () {
		if (camerasList.Count > 0) {
			if (!camerasList [0].orthographic) {
				if (camerasList [0].fieldOfView > 20) {
					for (int i = 0; i < camerasList.Count; i++) {
						camerasList [i].fieldOfView -= 5;
					}
				}
			} else {
				if (camerasList [0].orthographicSize > 4) {
					for (int i = 0; i < camerasList.Count; i++) {
						camerasList [i].orthographicSize -= 0.5f;
					}
				}
			}
		}
	}

	public void SwitchCamera () {
		if (camerasList.Count > 0) {
			for (int i = 0; i < camerasList.Count; i++) {
				if (camerasList [i].gameObject.activeSelf) {
					camerasList [i].gameObject.SetActive (false);
					if ((i + 1) == camerasList.Count) {
						camerasList [0].gameObject.SetActive (true);
						rotateToMouse.SetCamera (camerasList [0]);
						break;
					} else {
						camerasList [i + 1].gameObject.SetActive (true);
						rotateToMouse.SetCamera (camerasList [i + 1]);
						break;
					}
				}
			}
		}
	}
}

public static class InputWrapper
{
    public static Vector3 mousePosition
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
            return Vector3.zero;
#else
            return UnityEngine.Input.mousePosition;
#endif
        }
    }

    public static bool GetMouseButton(int button)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            if (button == 0) return Mouse.current.leftButton.isPressed;
            if (button == 1) return Mouse.current.rightButton.isPressed;
            if (button == 2) return Mouse.current.middleButton.isPressed;
        }
        return false;
#else
        return UnityEngine.Input.GetMouseButton(button);
#endif
    }

    public static bool GetKey(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (key == KeyCode.Space) return Keyboard.current.spaceKey.isPressed;
        }
        return false;
#else
        return UnityEngine.Input.GetKey(key);
#endif
    }

    public static bool GetKeyDown(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            switch (key)
            {
                case KeyCode.D: return Keyboard.current.dKey.wasPressedThisFrame;
                case KeyCode.A: return Keyboard.current.aKey.wasPressedThisFrame;
                case KeyCode.C: return Keyboard.current.cKey.wasPressedThisFrame;
                case KeyCode.Alpha1: return Keyboard.current.digit1Key.wasPressedThisFrame;
                case KeyCode.X: return Keyboard.current.xKey.wasPressedThisFrame;
                case KeyCode.Z: return Keyboard.current.zKey.wasPressedThisFrame;
                case KeyCode.Space: return Keyboard.current.spaceKey.wasPressedThisFrame;
                case KeyCode.RightArrow: return Keyboard.current.rightArrowKey.wasPressedThisFrame;
                case KeyCode.LeftArrow: return Keyboard.current.leftArrowKey.wasPressedThisFrame;
            }
        }
        return false;
#else
        return UnityEngine.Input.GetKeyDown(key);
#endif
    }

    public static float GetAxis(string axisName)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Mouse.current != null)
        {
            if (axisName == "Horizontal")
            {
                float val = 0;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) val += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) val -= 1f;
                return val;
            }
            if (axisName == "Vertical")
            {
                float val = 0;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) val += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) val -= 1f;
                return val;
            }
            if (axisName == "Mouse X")
            {
                return Mouse.current.delta.x.ReadValue() * 0.05f;
            }
            if (axisName == "Mouse Y")
            {
                return Mouse.current.delta.y.ReadValue() * 0.05f;
            }
        }
        return 0f;
#else
        return UnityEngine.Input.GetAxis(axisName);
#endif
    }

    public static bool GetButton(string buttonName)
    {
#if ENABLE_INPUT_SYSTEM
        if (buttonName == "Fire1") return GetMouseButton(0);
        if (buttonName == "Jump")
        {
            if (Keyboard.current != null) return Keyboard.current.spaceKey.isPressed;
        }
        return false;
#else
        return UnityEngine.Input.GetButton(buttonName);
#endif
    }

    public static bool GetButtonDown(string buttonName)
    {
#if ENABLE_INPUT_SYSTEM
        if (buttonName == "Fire1")
        {
            if (Mouse.current != null) return Mouse.current.leftButton.wasPressedThisFrame;
        }
        return false;
#else
        return UnityEngine.Input.GetButtonDown(buttonName);
#endif
    }
}
