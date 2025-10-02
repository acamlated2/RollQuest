using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class StructureScr : MonoBehaviour
{
    public enum StructureTypes
    {
        Tree, 
        Rock, 
    }
    
    public static StructureScr instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private int GetRandomIntFromPos(Vector3Int pos)
    {
        int hash = (pos.x * 7385693) ^ (pos.y * 19349663) ^ (pos.z * 83492791) ^ Globals.seed;
        hash = (hash ^ (hash >> 16)) & int.MaxValue;

        return hash;
    }

    public void PlaceStructure(StructureTypes type, Chunk chunk, Vector3Int basePos, ref HashSet<Vector3Int> newBlockPos)
    {
        switch (type)
        {
            case StructureTypes.Tree:
                PlaceTree(chunk, basePos, ref newBlockPos);
                break;
            case StructureTypes.Rock:
                PlaceRock(chunk, basePos, ref newBlockPos);
                break;
            default:
                break;
        }
    }
    
    private void PlaceTree(Chunk chunk, Vector3Int basePos, ref HashSet<Vector3Int> newBlockPos)
    {
        int trunkHeight = 4 + GetRandomIntFromPos(basePos) % 3;
    
        // --- Trunk ---
        for (int i = 0; i <= trunkHeight; i++)
        {
            Vector3Int trunkPos = new Vector3Int(basePos.x, basePos.y + i, basePos.z);
            if (!chunk.ContainsBlock(trunkPos))
            {
                newBlockPos.Add(trunkPos);
                chunk.Blocks[trunkPos] = new Block(trunkPos, Block.BlockTypes.Wood);
                chunk.StructureBlocks[trunkPos] = new Block(trunkPos, Block.BlockTypes.Wood);
            }
        }

        // --- Leaves ---
        int leafStart = trunkHeight - 1;
        int leafHeight = 3;
        int maxRadius = 2;

        for (int y = 0; y < leafHeight; y++)
        {
            for (int x = -maxRadius; x <= maxRadius; x++)
            {
                for (int z = -maxRadius; z <= maxRadius; z++)
                {
                    Vector3Int leafPos = new Vector3Int(basePos.x + x, basePos.y + leafStart + y, basePos.z + z);
                    if (!chunk.ContainsBlock(leafPos))
                    {
                        newBlockPos.Add(leafPos);
                        chunk.Blocks[leafPos] = new Block(leafPos, Block.BlockTypes.Leaf);
                        chunk.StructureBlocks[leafPos] = new Block(leafPos, Block.BlockTypes.Leaf);
                    }
                }
            }

            maxRadius -= 1;
        }
    }

    private void PlaceRock(Chunk chunk, Vector3Int basePos, ref HashSet<Vector3Int> newBlockPos)
    {
        int randInt = 1 + GetRandomIntFromPos(basePos) % 4;
        int height = (randInt == 4) ? 2 : 1;

        for (int y = 0; y <= height; y++)
        {
            Vector3Int rockPos = new Vector3Int(basePos.x, basePos.y + y, basePos.z);
            if (!chunk.ContainsBlock(rockPos))
            {
                newBlockPos.Add(rockPos);
                chunk.Blocks[rockPos] = new Block(rockPos, Block.BlockTypes.Rock);
                chunk.StructureBlocks[rockPos] = new Block(rockPos, Block.BlockTypes.Rock);
            }
        }
    }
}
