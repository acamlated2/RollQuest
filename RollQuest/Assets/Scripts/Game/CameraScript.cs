using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraScript : MonoBehaviour
{
    public static CameraScript instance;
    
    private GameObject _player;
    private GameObject _rotatePoint;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        
        _player = GameObject.FindGameObjectWithTag("Player");
        _rotatePoint = _player.transform.GetChild(1).gameObject;
    }

    public void RotateCamera(InputAction.CallbackContext context)
    {
        Vector2 lookDirection = context.ReadValue<Vector2>();

        _rotatePoint.transform.Rotate(new Vector3(0, lookDirection.x * 10 * Time.deltaTime, 0));
        
        PlayerScript.instance.ChangeVirtualFront(GetCameraFacingDirection());
    }
    
    private Vector3 GetCameraFacingDirection()
    {
        Vector3 dir = transform.forward;
        dir.y = 0f;
        dir.Normalize();

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.z))
        {
            return dir.x > 0 ? Vector3.right : Vector3.left;
        }
        else
        {
            return dir.z > 0 ? Vector3.forward : Vector3.back;
        }
    }
}
