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
    }
}
