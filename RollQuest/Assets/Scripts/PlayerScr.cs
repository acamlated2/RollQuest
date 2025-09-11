using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerScr : MonoBehaviour
{
    public static PlayerScr instance;
    
    public Block CurrentBlock;
    
    private Coroutine _moveCoroutine;

    private GameObject _playerModel;
    
    private GameObject _directionIndicator;

    private Vector3 _virtualFront = new Vector3(0, 0, 1);
    
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
        
        _directionIndicator.transform.position = transform.position + _virtualFront * 2f;
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
        Debug.LogError("try move");
        // if (_moveCoroutine != null)
        //     return;
        //
        // BlockScr nextBlockScr = GridControllerScr.instance.GetGridBlock((int)(currentBlockScr.gridPos.x + direction.x),
        //     (int)(currentBlockScr.gridPos.z + direction.z));
        //
        // _moveCoroutine = StartCoroutine(MoveToPosition(direction, nextBlockScr, (() =>
        // {
        //     GridControllerScr.instance.UpdatePlayerPosition();
        // })));
    }
    
    // private IEnumerator MoveToPosition(Vector3 direction, BlockScr targetBlockScr, Action onComplete)
    // {
    //     float EaseIn(float t)
    //     {
    //         return t * t * t * t;
    //     }
    //     
    //     Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 axis, float angle)
    //     {
    //         return Quaternion.AngleAxis(angle, axis) * (point - pivot) + pivot;
    //     }
    //     
    //     float duration = 0.3f;
    //     float elapsed = 0f;
    //     
    //     Vector3 rotationAxis = Vector3.Cross(Vector3.up, direction.normalized);
    //     Vector3 pivot = transform.position + (direction.normalized * 1f) + (Vector3.down * 1f);
    //
    //     Quaternion totalRotation = Quaternion.AngleAxis(90f, rotationAxis);
    //     Quaternion startRotation = _playerModel.transform.rotation;
    //     
    //     Vector3 startPosition = transform.position;
    //     
    //     float finalYOffset = targetBlockScr.transform.position.y + 2f;
    //     
    //     Vector3 finalRotatedPosition = RotatePointAroundPivot(startPosition, pivot, rotationAxis, 90f);
    //     Vector3 finalPosition = new Vector3(finalRotatedPosition.x, finalYOffset, finalRotatedPosition.z);
    //     
    //     float height = Mathf.Max(Mathf.Abs(finalPosition.y - startPosition.y) * 0.5f, 0f) + 0.5f;
    //
    //     while (elapsed < duration)
    //     {
    //         elapsed += Time.deltaTime;
    //         float t = Mathf.Clamp01(elapsed / duration);
    //         
    //         t = EaseIn(t);
    //         
    //         float currentAngle = 90f * t;
    //         Quaternion incrementalRotation = Quaternion.AngleAxis(currentAngle, rotationAxis);
    //         
    //         Vector3 rotatedPosition = RotatePointAroundPivot(startPosition, pivot, rotationAxis, currentAngle);
    //
    //         if (height != 0)
    //         {
    //             float arc = Mathf.Sin(t * Mathf.PI) * height;
    //             float baseY = Mathf.Lerp(startPosition.y, finalYOffset, t);
    //             rotatedPosition.y = baseY + arc;
    //         }
    //         
    //         transform.position = rotatedPosition;
    //         _playerModel.transform.rotation = incrementalRotation * startRotation;
    //         
    //         yield return null;
    //     }
    //
    //     transform.position = finalPosition;
    //     _playerModel.transform.rotation = totalRotation * startRotation;
    //     
    //     currentBlockScr = targetBlockScr;
    //     
    //     _moveCoroutine = null;
    //     
    //     onComplete?.Invoke();
    // }
}
