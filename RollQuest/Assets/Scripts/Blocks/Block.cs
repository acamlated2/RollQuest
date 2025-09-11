using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class Block
{
    public enum BlockTypes
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

    public BlockTypes BlockType;
    public Node.WalkableType WalkableType;

    public Vector3Int Position;
    
    public GameObject Owner;
    public GameObject RootOwner;
    public GameObject OwnedBlock;

    public Node Node;
    
    public Block(Vector3Int pos, BlockTypes type)
    {
        Position = pos;
        BlockType = type;
    }
}
