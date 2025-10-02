using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public static class Globals
{
    public const float BlockSize = 1;
    public static int seed = 0;
    public static float seedOffsetX = 0;
    public static float seedOffsetZ = 0;
}

public class GameplayControllerScr : MonoBehaviour
{
    public static GameplayControllerScr instance;
    
    private bool _gameStarted;
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;

        if (Globals.seed == 0)
        {
            Globals.seed = Random.Range(int.MinValue, int.MaxValue);
            
            int seedX = Globals.seed & 0xFFFF;
            int seedZ = (Globals.seed >> 16) & 0xFFFF;

            Globals.seedOffsetX = seedX * 0.01f;
            Globals.seedOffsetZ = seedZ * 0.01f;
            
            Random.InitState(Globals.seed);
        }
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void SetPlayerPosition(Vector3Int position)
    {
        Vector3 spawnPosition = new Vector3(0, position.y + 1, 0);
    
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        player.transform.position = spawnPosition;
        player.GetComponent<PlayerScr>().currentPosition = position;
    }
}
