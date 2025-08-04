using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class GrassScript : BlockScript
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
            GameObject prefab = PrefabsScript.instance.woodPrefab;

            if (randBlockInt == 0)
            {
                newOwnedBlock = SpawnBlock(PrefabsScript.instance.woodPrefab);
                prefab = PrefabsScript.instance.woodPrefab;
            }

            if (randBlockInt == 1)
            {
                newOwnedBlock = SpawnBlock(PrefabsScript.instance.stonePrefab);
                prefab = PrefabsScript.instance.stonePrefab;
            }
            
            newOwnedBlock.GetComponent<BlockScript>().InitialiseBlock();
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
