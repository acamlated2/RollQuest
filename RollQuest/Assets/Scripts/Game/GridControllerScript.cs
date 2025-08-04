using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class GridControllerScript : MonoBehaviour
{
    public static GridControllerScript instance;

    public List<GameObject> blockGrid = new List<GameObject>();
    private List<Node> _nodeGrid = new List<Node>();
    
    private int _gridSize = 100;

    private float _noiseScale = 0.008f;
    private int _maxBlockHeight = 48;
    private float _sharpness = 2.5f;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
        }

        instance = this;
    }

    private void Start()
    {
        InitialiseBlocks();
    }

    public BlockScript GetGridBlock(int x, int z)
    {
        foreach (GameObject block in blockGrid)
        {
            if (block.GetComponent<BlockScript>().gridPos.x == x)
            {
                if (block.GetComponent<BlockScript>().gridPos.z == z)
                {
                    return block.GetComponent<BlockScript>();
                }
            }
        }

        return null;
    }

    public Node GetNode(int x, int z)
    {
        foreach (Node node in _nodeGrid)
        {
            if (node.GridPos.x == x)
            {
                if (node.GridPos.z == z)
                {
                    return node;
                }
            }
        }

        return null;
    }

    private void InitialiseBlocks()
    {
        GameObject prefab = PrefabsScript.instance.grassPrefab;
        
        for (int i = 0; i < _gridSize; i++)
        {
            for (int j = 0; j < _gridSize; j++)
            {
                Vector3 position = new Vector3(i * 2 - _gridSize, 0, j * 2 - _gridSize);;
                
                GameObject newBlock = Instantiate(prefab, position, Quaternion.identity);
                newBlock.transform.name = "Grass " + position.x + " " + position.z;
                newBlock.transform.SetParent(GameObject.FindGameObjectWithTag("Environment Blocks Parent").transform);
                
                blockGrid.Add(newBlock);
            }
        }
        
        // noise
        int offset = GameControllerScript.instance.seed % 1000;

        foreach (GameObject block in blockGrid)
        {
            if (block.GetComponent<BlockScript>().blockType != BlockScript.BlockType.Grass)
            {
                continue;
            }
            
            // generate noise position
            float xCoord = (block.transform.position.x + offset) * _noiseScale;
            float zCoord = (block.transform.position.z + offset) * _noiseScale;

            float noiseValue = Mathf.PerlinNoise(xCoord, zCoord) * 0.6f + 
                               Mathf.PerlinNoise(xCoord * 2, zCoord * 2) * 0.3f + 
                               Mathf.PerlinNoise(xCoord * 4, zCoord * 4) * 0.1f;

            float heightCurve = Mathf.Pow(noiseValue, _sharpness);
            int height = Mathf.RoundToInt(heightCurve * _maxBlockHeight);
            
            // apply noise to block
            block.transform.position =
                new Vector3(block.transform.position.x, height * 2, block.transform.position.z);
            
            Vector3Int virtualBlockPos = new Vector3Int((int)(block.transform.position.x / 2),
                (int)(block.transform.position.y / 2),
                (int)(block.transform.position.z / 2));
            
            block.GetComponent<BlockScript>().gridPos = virtualBlockPos;
            
            Node newNode = new Node(virtualBlockPos.x, virtualBlockPos.y, virtualBlockPos.z, block);
            _nodeGrid.Add(block.GetComponent<BlockScript>().Node);
            block.GetComponent<BlockScript>().Node = newNode;
        }

        // generate dirt blocks
        foreach (GameObject block in blockGrid)
        {
            int finalDirtCount = 0;
            List<Node> neighbourList = GetNeighbourList(block.GetComponent<BlockScript>().Node);
            
            int blockGridHeight = block.GetComponent<BlockScript>().gridPos.y;

            foreach (Node node in neighbourList)
            {
                int neighbourGridHeight = node.Block.GetComponent<BlockScript>().gridPos.y;;

                if (neighbourGridHeight >= blockGridHeight - 1)
                {
                    continue;
                }
                
                int dirtCount = blockGridHeight - neighbourGridHeight;

                if (dirtCount > finalDirtCount)
                {
                    finalDirtCount = dirtCount;
                }
            }

            for (int i = 0; i < finalDirtCount; i++)
            {
                Vector3 spawnPosition = new Vector3(block.transform.position.x, block.transform.position.y - i * 2 - 2,
                    block.transform.position.z);
                
                GameObject newBlock = Instantiate(PrefabsScript.instance.dirtPrefab, spawnPosition, Quaternion.identity);
                newBlock.transform.SetParent(GameObject.FindGameObjectWithTag("Environment Blocks Parent").transform);
                newBlock.transform.name = "Dirt " + spawnPosition.x + " " + spawnPosition.y + " " + spawnPosition.z;
            }
        }
    }

    public List<Node> GetNodeCrossDeepNeighbourList(Node node, int depth)
    {
        List<Node> neighbourList = new List<Node>() { node };
        List<Node> checkList = new List<Node>() { node };
    
        for (int i = 0; i < depth; i++)
        {
            List<Node> neighboursInDepth = new List<Node>();
            foreach (Node nodeToCheck in checkList)
            {
                List<Node> newNeighbourList = GetNeighbourList(nodeToCheck);
    
    
                foreach (Node newNeighbour in newNeighbourList)
                {
                    if (neighbourList.Contains(newNeighbour))
                    {
                        continue;
                    }
    
                    neighboursInDepth.Add(newNeighbour);
                    neighbourList.Add(newNeighbour);
                }
            }
    
            foreach (Node newNeighbour in neighboursInDepth)
            {
                checkList.Add(newNeighbour);
            }
        }
    
        return neighbourList;
    }
    
    public List<Node> GetNodeSquareDeepNeighbourList(Node node, int depth)
    {
        List<Node> neighbourList = new List<Node>();
    
        int centreX = node.GridPos.x;
        int centreZ = node.GridPos.z;
    
        for (int dx = -depth; dx <= depth; dx++)
        {
            for (int dy = -depth; dy <= depth; dy++)
            {
                int newX = centreX + dx;
                int newZ = centreZ + dy;
    
                // Ensure the new coordinates are within the grid bounds
                Node newNeighbour = GetNode(newX, newZ);
                if (newNeighbour != null)
                {
                    neighbourList.Add(newNeighbour);
                }
            }
        }
    
        return neighbourList;
    }

    public List<Node> GetNeighbourList(Node node)
    {
        List<Node> neighbourList = new List<Node>();

        // left
        Node leftNeighbour = GetNode(node.GridPos.x - 1, node.GridPos.z);
        if (leftNeighbour != null)
        {
            neighbourList.Add(leftNeighbour);
        }

        // right
        Node rightNeighbour = GetNode(node.GridPos.x + 1, node.GridPos.z);
        if (rightNeighbour != null)
        {
            neighbourList.Add(rightNeighbour);
        }

        // top 
        Node topNeighbour = GetNode(node.GridPos.x, node.GridPos.z + 1);
        if (topNeighbour != null)
        {
            neighbourList.Add(topNeighbour);
        }

        // bottom
        Node bottomNeighbour = GetNode(node.GridPos.x, node.GridPos.z - 1);
        if (bottomNeighbour != null)
        {
            neighbourList.Add(bottomNeighbour);
        }

        return neighbourList;
    }

    // private GameObject ReplaceBlock(GameObject targetBlock, BlockScript.BlockType newBlockType)
    // {
    //     GameObject newGridBlock = Instantiate(GetBlockPrefab(newBlockType),
    //                                           targetBlock.transform.position,
    //                                           Quaternion.identity);
    //     BlockScript gridBlock = GetGridBlock(targetBlock.GetComponent<BlockScript>().gridPos.x,
    //                                          targetBlock.GetComponent<BlockScript>().gridPos.z);
    //
    //     newGridBlock.transform.name = newBlockType +
    //                                   " " +
    //                                   targetBlock.GetComponent<BlockScript>().gridPos.x +
    //                                   " " +
    //                                   targetBlock.GetComponent<BlockScript>().gridPos.z;
    //     
    //     newGridBlock.transform.parent = GameObject.FindGameObjectWithTag("Ground Parent").transform;
    //     newGridBlock.GetComponent<BlockScript>().Node =
    //         targetBlock.GetComponent<BlockScript>().Node;
    //     newGridBlock.GetComponent<BlockScript>().Node.Block = newGridBlock;
    //     
    //     newGridBlock.GetComponent<BlockScript>().gridPos.x = gridBlock.gridPos.x;
    //     newGridBlock.GetComponent<BlockScript>().gridPos.y = gridBlock.gridPos.y;
    //     newGridBlock.GetComponent<BlockScript>().gridPos.z = gridBlock.gridPos.z;
    //     
    //     blockGrid.Add(newGridBlock);
    //     blockGrid.Remove(targetBlock);
    //
    //     if (targetBlock.GetComponent<BlockScript>().blockType == BlockScript.BlockType.Sand)
    //     {
    //         foreach (GameObject enemy in targetBlock.GetComponent<SandScript>().enemies)
    //         {
    //             enemy.GetComponent<EnemyScript>().FindCurrentBlock();
    //             enemy.GetComponent<EnemyScript>().MoveToCurrentBlock(() => { });
    //         }
    //         targetBlock.GetComponent<SandScript>().enemies.Clear();
    //     }
    //     
    //     Destroy(targetBlock);
    //
    //     return newGridBlock;
    // }
    //
    // private GameObject GetBlockPrefab(BlockScript.BlockType blockType)
    // {
    //     switch (blockType)
    //     {
    //
    //         case BlockScript.BlockType.Grass:
    //             return PrefabsScript.instance.grassPrefab;
    //         case BlockScript.BlockType.Sand:
    //             return PrefabsScript.instance.sandPrefab;
    //         case BlockScript.BlockType.Wood:
    //             break;
    //         case BlockScript.BlockType.Stone:
    //             break;
    //         case BlockScript.BlockType.Tree:
    //             break;
    //         case BlockScript.BlockType.Platform:
    //             break;
    //         case BlockScript.BlockType.Tower:
    //             break;
    //         case BlockScript.BlockType.Water:
    //             return PrefabsScript.instance.waterPrefab;
    //         default:
    //             throw new ArgumentOutOfRangeException(nameof(blockType), blockType, null);
    //     }
    //     return null;
    // }
}


public class Node
{
    public enum WalkableType
    {
        Walkable,
        NonWalkable
    }

    public Vector3Int GridPos = new Vector3Int();

    public Vector3Int PathfindingGFH = new Vector3Int();

    public Node CameFromNode;

    public GameObject Block;

    public Node(int xPos, int yPos, int zPos, GameObject block)
    {
        GridPos.x = Mathf.RoundToInt(xPos);
        GridPos.y = Mathf.RoundToInt(yPos);
        GridPos.z = Mathf.RoundToInt(zPos);
        Block = block;

        Block.GetComponent<BlockScript>().gridPos.x = xPos;
        Block.GetComponent<BlockScript>().gridPos.y = yPos;
        Block.GetComponent<BlockScript>().gridPos.z = zPos;
        
        Block.GetComponent<BlockScript>().Node = this;
    }

    public void CalculateFCost()
    {
        PathfindingGFH.z = PathfindingGFH.x + PathfindingGFH.y;
    }
}

