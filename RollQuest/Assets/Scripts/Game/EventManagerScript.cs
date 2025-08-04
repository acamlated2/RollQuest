using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventManagerScript : MonoBehaviour
{
    public static EventManagerScript Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }

        Instance = this;
    }

    public event Action<int> OnLevelStart;

    public void LevelStart(int level)
    {
        if (OnLevelStart != null)
        {
            OnLevelStart(level);
        }
    }

    public event Action OnBlocksInitialise;

    public void InitialiseBlocks()
    {
        if (OnBlocksInitialise != null)
        {
            OnBlocksInitialise();
        }
    }

    public event Action OnInitiativeRequest;

    public void RequestInitiative()
    {
        if (OnInitiativeRequest != null)
        {
            OnInitiativeRequest();
        }
    }

    // public event Action<CharacterScript> OnTurnStart;
    //
    // public void StartTurn(CharacterScript character)
    // {
    //     if (OnTurnStart != null)
    //     {
    //         OnTurnStart(character);
    //     }
    // }
    //
    // public event Action<CharacterScript> OnTurnEnd;
    //
    // public void EndTurn(CharacterScript character)
    // {
    //     if (OnTurnEnd != null)
    //     {
    //         OnTurnEnd(character);
    //     }
    // }

    public event Action OnObstacleDestroy;

    public void DestroyObstacle()
    {
        if (OnObstacleDestroy != null)
        {
            OnObstacleDestroy();
        }
    }

    // public event Action<GameStateControllerScript.GameState> OnGameStateChange;
    //
    // public void ChangeGameState(GameStateControllerScript.GameState gameState)
    // {
    //     if (OnGameStateChange != null)
    //     {
    //         OnGameStateChange(gameState);
    //     }
    // }

    public event Action OnNewWave;

    public void NewWave()
    {
        if (OnNewWave != null)
        {
            OnNewWave();
        }
    }
    
    public event Action OnDataLoaded;

    public void DataLoaded()
    {
        if (OnDataLoaded != null)
        {
            OnDataLoaded();
        }
    }

    public event Action<GameObject> OnSelectedBlock;

    public void SelectBlock(GameObject block)
    {
        if (OnSelectedBlock != null)
        {
            OnSelectedBlock(block);
        }
    }
    
    public event Action OnUIUpdate;
    
    public void UIUpdate()
    {
        if (OnUIUpdate != null)
        {
            OnUIUpdate();
        }
    }
}
