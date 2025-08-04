using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class GameControllerScript : MonoBehaviour
{
    public static GameControllerScript instance;
    
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
    
    private void Update()
    {
        if (!_gameStarted)
        {
            _gameStarted = true;

            BlockScript centreBlock = GridControllerScript.instance.GetGridBlock(0, 0);
            Vector3 spawnPosition = new Vector3(0, centreBlock.transform.position.y + 2, 0);

            GameObject player = Instantiate(PrefabsScript.instance.playerPrefab, spawnPosition, Quaternion.identity);
            player.GetComponent<PlayerScript>().currentBlock = centreBlock;
        }
    }
}
