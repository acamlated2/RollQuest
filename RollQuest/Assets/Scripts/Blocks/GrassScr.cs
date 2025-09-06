using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class GrassScr : BlockScr
{
    public void CreateObject()
    {
        if (ownedBlock)
        {
            Destroy(ownedBlock);
        }

        walkableType = Node.WalkableType.Walkable;
        
        int randBlockInt = Random.Range(0, 11);

        if (randBlockInt <= 1)
        {
            walkableType = Node.WalkableType.NonWalkable;

            GameObject newOwnedBlock = null;
            GameObject prefab = PrefabsScr.instance.woodPrefab;

            if (randBlockInt == 0)
            {
                newOwnedBlock = SpawnBlock(PrefabsScr.instance.woodPrefab);
                prefab = PrefabsScr.instance.woodPrefab;
            }

            if (randBlockInt == 1)
            {
                newOwnedBlock = SpawnBlock(PrefabsScr.instance.stonePrefab);
                prefab = PrefabsScr.instance.stonePrefab;
            }
            
            newOwnedBlock.GetComponent<BlockScr>().InitialiseBlock();
            newOwnedBlock.transform.parent =
                GameObject.FindGameObjectWithTag("Environment Blocks Parent").transform;
            newOwnedBlock.name = prefab.name + " " + gridPos.x + " " + gridPos.y;
        }
    }

    public void RemoveObject()
    {
        if (ownedBlock)
        {
            Destroy(ownedBlock);
            ownedBlock = null;
        }

        walkableType = Node.WalkableType.Walkable;
    }
}
