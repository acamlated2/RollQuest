using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrefabsScript : MonoBehaviour
{
    // blocks
    public GameObject stonePrefab;
    public GameObject woodPrefab;
    public GameObject treePrefab;
    public GameObject waterPrefab;
    public GameObject lavaPrefab;
    public GameObject dirtPrefab;
    public GameObject grassPrefab;
    public GameObject sandPrefab;
    public GameObject orangePortalPrefab;
    public GameObject bluePortalPrefab;
    
    public GameObject platformPrefab;
    
    // towers
    public GameObject vanguardTowerPrefab;
    public GameObject sentinelTowerPrefab;
    public GameObject boomerangTowerPrefab;
    public GameObject mortarTowerPrefab;
    public GameObject mirrorTowerPrefab;
    
    public static PrefabsScript instance;
    
    // characters
    public GameObject playerPrefab;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }
}