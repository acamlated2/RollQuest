using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerScript : MonoBehaviour
{
    public static PlayerScript instance;
    
    public BlockScript currentBlock;
    
    private Coroutine _moveCoroutine;

    private GameObject _playerModel;
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        
        _playerModel = transform.GetChild(0).gameObject;
    }

    public void MoveFB(InputAction.CallbackContext context)
    {
        if (context.ReadValue<float>() < 0)
        {
            TryMove(new Vector3(0, 0, -1));
        }
        if (context.ReadValue<float>() > 0)
        {
            TryMove(new Vector3(0, 0, 1));
        }
    }
    
    public void MoveLR(InputAction.CallbackContext context)
    {
        if (context.ReadValue<float>() < 0)
        {
            TryMove(new Vector3(-1, 0, 0));
        }
        if (context.ReadValue<float>() > 0)
        {
            TryMove(new Vector3(1, 0, 0));
        }
    }
    
    private void TryMove(Vector3 direction)
    {
        if (_moveCoroutine != null)
            return;

        BlockScript nextBlock = GridControllerScript.instance.GetGridBlock((int)(currentBlock.gridPos.x + direction.x),
            (int)(currentBlock.gridPos.z + direction.z));

        _moveCoroutine = StartCoroutine(MoveToPosition(direction, nextBlock));
    }
    
    private IEnumerator MoveToPosition(Vector3 direction, BlockScript targetBlock)
    {
        float EaseIn(float t)
        {
            return t * t * t * t;
        }
        
        Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 axis, float angle)
        {
            return Quaternion.AngleAxis(angle, axis) * (point - pivot) + pivot;
        }
        
        float duration = 0.3f;
        float elapsed = 0f;
        
        Vector3 rotationAxis = Vector3.Cross(Vector3.up, direction.normalized);
        Vector3 pivot = transform.position + (direction.normalized * 1f) + (Vector3.down * 1f);

        Quaternion totalRotation = Quaternion.AngleAxis(90f, rotationAxis);
        Quaternion startRotation = _playerModel.transform.rotation;
        
        Vector3 startPosition = transform.position;
        
        float finalYOffset = targetBlock.transform.position.y + 2f;
        
        Vector3 finalRotatedPosition = RotatePointAroundPivot(startPosition, pivot, rotationAxis, 90f);
        Vector3 finalPosition = new Vector3(finalRotatedPosition.x, finalYOffset, finalRotatedPosition.z);
        
        float height = Mathf.Max(Mathf.Abs(finalPosition.y - startPosition.y) * 0.5f, 0f) + 0.5f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            
            t = EaseIn(t);
            
            float currentAngle = 90f * t;
            Quaternion incrementalRotation = Quaternion.AngleAxis(currentAngle, rotationAxis);
            
            Vector3 rotatedPosition = RotatePointAroundPivot(startPosition, pivot, rotationAxis, currentAngle);

            if (height != 0)
            {
                float arc = Mathf.Sin(t * Mathf.PI) * height;
                float baseY = Mathf.Lerp(startPosition.y, finalYOffset, t);
                rotatedPosition.y = baseY + arc;
            }
            
            transform.position = rotatedPosition;
            _playerModel.transform.rotation = incrementalRotation * startRotation;
            
            yield return null;
        }

        transform.position = finalPosition;
        _playerModel.transform.rotation = totalRotation * startRotation;
        
        currentBlock = targetBlock;
        
        _moveCoroutine = null;
    }
}
