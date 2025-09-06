using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class DirtScr : MonoBehaviour
{
    private void Awake()
    {
        // randomise rotation
        int randRotationInt = Random.Range(0, 4);
        Transform model = transform.GetChild(0).transform;

        Quaternion newRotation =
            Quaternion.Euler(new Vector3(model.transform.rotation.x, 90 * randRotationInt,
                model.transform.rotation.z));
        model.transform.rotation = newRotation;
    }
}