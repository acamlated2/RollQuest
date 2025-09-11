using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class GameplayControllerScr : MonoBehaviour
{
    public static GameplayControllerScr instance;
    
    private bool _gameStarted;
    
    public int seed;
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;

        if (seed == 0)
        {
            seed = Random.Range(int.MinValue, int.MaxValue);
        }
        Random.InitState(seed);
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    // public void SetPlayerPosition(BlockScr centerBlock)
    // {
    //     Vector3 spawnPosition = new Vector3(0, centerBlock.transform.position.y + 2, 0);
    //
    //     GameObject player = GameObject.FindGameObjectWithTag("Player");
    //     player.transform.position = spawnPosition;
    //     player.GetComponent<PlayerScr>().currentBlockScr = centerBlock;
    // }
}
