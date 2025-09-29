using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerScr : MonoBehaviour
{
    public static PlayerScr instance;
    
    private Coroutine _moveCoroutine;

    private GameObject _playerModel;
    
    private GameObject _directionIndicator;

    private Vector3 _virtualFront = new Vector3(0, 0, 1);

    public Vector3Int currentPosition = new Vector3Int();
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        
        _playerModel = transform.GetChild(0).gameObject;
        _directionIndicator = transform.GetChild(2).gameObject;
    }
    
    public void ChangeVirtualFront(Vector3 direction)
    {
        _virtualFront = direction;
        
        _directionIndicator.transform.position = transform.position + _virtualFront * 1f;
    }

    public void MoveFB(InputAction.CallbackContext context)
    {
        float value = context.ReadValue<float>();
        
        if (Mathf.Approximately(value, 0)) return;
        
        Vector3 direction = _virtualFront.normalized;
        direction.y = 0;
        direction.Normalize();
        
        TryMove(direction * Mathf.Sign(value));
    }
    
    public void MoveLR(InputAction.CallbackContext context)
    {
        float value = context.ReadValue<float>();
        
        if (Mathf.Approximately(value, 0)) return;

        Vector3 direction = Vector3.Cross(Vector3.up, _virtualFront).normalized;
        
        TryMove(direction * Mathf.Sign(value));
    }
    
    private void TryMove(Vector3 direction)
    {
        if (_moveCoroutine != null)
            return;
        
        Vector3Int targetPosition =
            GridControllerScr.instance.GetClosestTopMostBlock(currentPosition + Vector3Int.RoundToInt(direction));

        targetPosition.y += 1;
        
        _moveCoroutine = StartCoroutine(MoveToPosition(direction, targetPosition, (() => { })));
    }
    
    private IEnumerator MoveToPosition(Vector3 direction, Vector3Int targetPos, Action onComplete)
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
        
        float halfBlockSize = Globals.BlockSize * 0.5f;
        
        Vector3 rotationAxis = Vector3.Cross(Vector3.up, direction.normalized);
        Vector3 pivot = transform.position + (direction.normalized * halfBlockSize) + (Vector3.down * halfBlockSize);
    
        Quaternion totalRotation = Quaternion.AngleAxis(90f, rotationAxis);
        Quaternion startRotation = _playerModel.transform.rotation;
        
        Vector3 startPosition = transform.position;
        
        float finalYOffset = targetPos.y;
        
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
        
        currentPosition = targetPos;

        _moveCoroutine = null;
        
        onComplete?.Invoke();
    }
}
