using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class BlockScr : MonoBehaviour
{
    public enum BlockType
    {
        Grass, 
        Sand, 
        Wood, 
        Stone, 
        Tree, 
        Platform, 
        Tower, 
        Water, 
        Lava, 
    }

    public BlockType blockType;
    public Node.WalkableType walkableType;

    public Vector3Int gridPos = new Vector3Int();
    
    public GameObject owner;
    public GameObject rootOwner;

    public Node Node;
    
    public GameObject ownedBlock;

    [SerializeField] private bool shouldntRotate;

    protected virtual void Start()
    {
        EventManagerScr.Instance.OnBlocksInitialise += InitialiseBlock;
    }
    
    protected virtual void OnDestroy()
    {
        EventManagerScr.Instance.OnBlocksInitialise -= InitialiseBlock;
    }

    public void InitialiseBlock()
    {
        if (shouldntRotate)
        {
            return;
        }
        
        // randomise rotation
        int randRotationInt = Random.Range(0, 4);
        Transform model = transform.GetChild(0).transform;

        Quaternion newRotation =
            Quaternion.Euler(new Vector3(model.transform.rotation.x, 90 * randRotationInt,
                                         model.transform.rotation.z));
        model.transform.rotation = newRotation;
    }

    public virtual void Interact()
    {
        
    }

    public GameObject SpawnBlock(GameObject prefab)
    {
        Vector3 spawnPosition = transform.position + new Vector3(0, 2, 0);
        
        GameObject newBlockObject = Instantiate(prefab, spawnPosition, Quaternion.identity);
        newBlockObject.name = prefab.name + " " + gridPos.x + " " + gridPos.z;

        newBlockObject.GetComponent<BlockScr>().owner = gameObject;
        newBlockObject.GetComponent<BlockScr>().rootOwner = gameObject;
        ownedBlock = newBlockObject;

        return newBlockObject;
    }
}
