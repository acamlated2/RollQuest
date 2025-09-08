using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

public class GridControllerScr : MonoBehaviour
{
    public static GridControllerScr instance;

    private Dictionary<Vector2Int, BlockScr> _blockLookup = new Dictionary<Vector2Int, BlockScr>();
    private Dictionary<Vector2Int, Node> _nodeLookup = new Dictionary<Vector2Int, Node>();

    private const float NoiseScale = 0.008f;
    private const int MaxBlockHeight = 64;
    private const float Sharpness = 2.5f;
    
    private const float BlockSize = 2;
    private const int ChunkBlockSize = 8;
    private const float LoadRadius = 100;
    private const int ChunkWorldSize = (int)(ChunkBlockSize * BlockSize);
    
    private Dictionary<Vector3, Chunk> _savedChunks = new Dictionary<Vector3, Chunk>();
    
    private const int MaxCachedChunks = 100;
    
    private ConcurrentQueue<Chunk> _generatedChunks = new ConcurrentQueue<Chunk>();
    
    private Queue<Chunk> _chunksToLoad = new Queue<Chunk>();
    private bool _isLoadingChunks = false;
    
    private Queue<Chunk> _chunksToUnload = new Queue<Chunk>();
    private bool _isUnloadingChunks = false;

    private const int BlockBatchCount = 5;
    
    [SerializeField] private ObjectPoolScr grassObjectPool;
    [SerializeField] private ObjectPoolScr dirtObjectPool;
    
    private GameObject _player;

    private Vector3Int _lastPlayerChunkPos;
    
    private readonly List<Chunk> _tempChunksToLoad = new List<Chunk>();
    
    private HashSet<Chunk> _newChunksToLoadSet = new HashSet<Chunk>();
    private HashSet<Chunk> _chunksToLoadSet = new HashSet<Chunk>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
        }

        instance = this;

        _player = GameObject.FindGameObjectWithTag("Player");
    }

    private void Start()
    {
        LoadPlayerChunk();
        LoadChunks();
        
        GameplayControllerScr.instance.SetPlayerPosition(GetGridBlock(0, 0));
    }

    private void Update()
    {
        while (_generatedChunks.TryDequeue(out Chunk data))
        {
            _savedChunks[data.Position] = data;
        }
        
        if (!_isUnloadingChunks && _chunksToUnload.Count > 0)
            StartCoroutine(ProcessChunksUnloading());
        
        if (!_isLoadingChunks && _chunksToLoad.Count > 0)
            StartCoroutine(ProcessChunksLoading());
    }

    public void UpdatePlayerPosition()
    {
        Vector3Int playerChunk = GetPlayerChunkPos();

        if (playerChunk != _lastPlayerChunkPos)
        {
            LoadChunks();
            _lastPlayerChunkPos = playerChunk;
        }
    }

    public BlockScr GetGridBlock(int x, int z)
    {
        _blockLookup.TryGetValue(new Vector2Int(x, z), out var block);
        return block;
    }

    public Node GetNode(int x, int z)
    {
        _nodeLookup.TryGetValue(new Vector2Int(x, z), out var node);
        return node;
    }

    private void LoadChunks()
    {
        int searchRadius = Mathf.RoundToInt(LoadRadius / ChunkWorldSize);
        
        _tempChunksToLoad.Clear();
        
        Vector3Int playerChunk = GetPlayerChunkPos();
        float sqrRadius = LoadRadius * LoadRadius;

        foreach (Vector2Int offset in GetSpiralOffsets(searchRadius))
        {
            int chunkX = playerChunk.x + offset.x;
            int chunkZ = playerChunk.z + offset.y;

            // world position of center of chunk
            Vector3 center = new Vector3(
                chunkX * ChunkWorldSize + ChunkWorldSize / 2,
                0f,
                chunkZ * ChunkWorldSize + ChunkWorldSize / 2
            );
                
            if ((_player.transform.position - center).sqrMagnitude <= sqrRadius)
            {
                if (!_savedChunks.TryGetValue(center, out Chunk chunk))
                {
                    chunk = new Chunk(center, false);
                    _savedChunks[center] = chunk;
                    GenerateChunkAsync(chunk);
                }
                    
                _tempChunksToLoad.Add(chunk);
            }
        }
        
        _newChunksToLoadSet = new HashSet<Chunk>(_tempChunksToLoad);
        _chunksToLoadSet = new HashSet<Chunk>(_chunksToLoad);
        
        // remove chunks that are no longer needed
        foreach (KeyValuePair<Vector3, Chunk> kvp in _savedChunks)
        {
            if (!_newChunksToLoadSet.Contains(kvp.Value))
            {
                _chunksToUnload.Enqueue(kvp.Value);
            }
        }
        
        // add new chunks to load
        foreach (Chunk chunk in _newChunksToLoadSet)
        {
            if (chunk.IsLoaded)
                continue;
            
            if (!_chunksToLoadSet.Contains(chunk))
            {
                _chunksToLoad.Enqueue(chunk);
            }
        }
    }

    private Vector3Int GetPlayerChunkPos()
    {
        Vector3 playerPos = _player.transform.position;
        Vector3Int playerChunk = new Vector3Int(
            Mathf.FloorToInt(playerPos.x / ChunkWorldSize),
            0,
            Mathf.FloorToInt(playerPos.z / ChunkWorldSize)
        );

        return playerChunk;
    }

    private void GenerateChunkAsync(Chunk chunk)
    {
        Task.Run((() =>
        {
            GenerateChunkBlockPositions(chunk);
            _generatedChunks.Enqueue(chunk);
        }));
    }

    private void GenerateChunk(Chunk chunk)
    {
        GenerateChunkBlockPositions(chunk);
        
        _savedChunks[chunk.Position] = chunk;
    }

    private void GenerateChunkBlockPositions(Chunk chunk)
    {
        Vector2 origin = new Vector2(
            chunk.Position.x - ChunkWorldSize / 2,
            chunk.Position.z - ChunkWorldSize / 2);

        int offset = GameplayControllerScr.instance.seed % 1000;
        List<Vector3Int> chunkBlockPositions = new List<Vector3Int>(ChunkBlockSize * ChunkBlockSize);
            
        for (int x = 0; x < ChunkBlockSize; x++) 
        {
            for (int z = 0; z < ChunkBlockSize; z++) 
            {
                Vector2 blockPos = origin + new Vector2(x * BlockSize, z * BlockSize);

                float xCoord = (blockPos.x + offset) * NoiseScale;
                float zCoord = (blockPos.y + offset) * NoiseScale;

                // base
                float baseNoise = Mathf.PerlinNoise(xCoord, zCoord) * 0.6f +
                                  Mathf.PerlinNoise(xCoord * 2, zCoord * 2) * 0.3f +
                                  Mathf.PerlinNoise(xCoord * 4, zCoord * 4) * 0.1f;

                float heightCurve = Mathf.Pow(baseNoise, Sharpness);

                // Large scale terrain variation
                float continentNoise = Mathf.PerlinNoise(xCoord * 0.002f, zCoord * 0.002f);
                float terrainScale = Mathf.Lerp(0.5f, 1.3f, continentNoise);

                int height = Mathf.RoundToInt(heightCurve * MaxBlockHeight * terrainScale);
                
                float worldY = height * BlockSize;

                chunkBlockPositions.Add(new Vector3Int((int)blockPos.x, (int)worldY, (int)blockPos.y));
            }
        }
        
        chunk.BlockPositions = chunkBlockPositions;
        chunk.IsGenerated = true;
    }

    private IEnumerator LoadChunkAsync(Chunk chunk)
    {
        if (!_savedChunks.ContainsValue(chunk))
            yield break;

        if (chunk.ChunkObject == null)
        {
            chunk.ChunkObject = new GameObject($"Chunk {chunk.Position}");
            chunk.ChunkObject.transform.position = Vector3.zero;
            
            MeshFilter mf = chunk.ChunkObject.AddComponent<MeshFilter>();
            MeshRenderer mr = chunk.ChunkObject.AddComponent<MeshRenderer>();
            
            // assign a material
            mr.material = AtlasHelperScr.instance.blockMaterial;
            
            // build mesh
            Mesh mesh = GenerateChunkMesh(chunk);
            mf.mesh = mesh;
            chunk.Mesh = mesh;
        }
        
        chunk.LastUsedTime = Time.time;
        chunk.IsLoaded = true;
    }

    private void LoadChunk(Chunk chunk)
    {
        if (!_savedChunks.ContainsValue(chunk))
            return;
        
        if (chunk.ChunkObject == null)
        {
            chunk.ChunkObject = new GameObject($"Chunk {chunk.Position}");
            chunk.ChunkObject.transform.position = Vector3.zero;
            
            MeshFilter mf = chunk.ChunkObject.AddComponent<MeshFilter>();
            MeshRenderer mr = chunk.ChunkObject.AddComponent<MeshRenderer>();
            
            // assign a material
            mr.material = AtlasHelperScr.instance.blockMaterial;
            
            // build mesh
            Mesh mesh = GenerateChunkMesh(chunk);
            mf.mesh = mesh;
            chunk.Mesh = mesh;
        }
        
        chunk.LastUsedTime = Time.time;
        chunk.IsLoaded = true;
    }

    private IEnumerator ProcessChunksLoading()
    {
        _isLoadingChunks = true;

        while (_chunksToLoad.Count > 0) 
        {
            Chunk chunk = _chunksToLoad.Peek(); // look at the first item

            if (chunk.IsLoaded)
            {
                _chunksToLoad.Dequeue();
                continue;
            }

            if (!chunk.IsGenerated)
            {
                yield return null; // wait a frame and check again, but don't dequeue
            }

            else
            {
                _chunksToLoad.Dequeue();
                yield return LoadChunkAsync(chunk);
            }
        }
        
        _isLoadingChunks = false;
    }
    
    private IEnumerator ProcessChunksUnloading()
    {
        _isUnloadingChunks = true;

        while (_chunksToUnload.Count > 0) 
        {
            Chunk chunk = _chunksToUnload.Dequeue();

            yield return UnloadChunkAsync(chunk); // wait until finished before next chunk
        }
        
        CleanupCache();
        
        _isUnloadingChunks = false;
    }

    private IEnumerator UnloadChunkAsync(Chunk chunk)
    {
        if (chunk.ChunkObject != null)
        {
            GameObject.Destroy(chunk.ChunkObject);
            chunk.ChunkObject = null;
        }

        chunk.IsLoaded = false;
        yield break;
    }

    private void SpawnBlock(Vector3 blockPos)
    {
        // spawn block
        GameObject newBlock = grassObjectPool.GetObject();
        newBlock.transform.position = blockPos;
        newBlock.transform.name = "Grass " + blockPos.x + " " + blockPos.z;
        
        // add node
        Vector3Int virtualBlockPos = new Vector3Int(
            (int)(newBlock.transform.position.x / BlockSize),
            (int)(newBlock.transform.position.y / BlockSize),
            (int)(newBlock.transform.position.z / BlockSize));
        
        BlockScr blockScr = newBlock.GetComponent<BlockScr>();
            
        blockScr.gridPos = virtualBlockPos;
            
        Node newNode = new Node(virtualBlockPos.x, virtualBlockPos.y, virtualBlockPos.z, newBlock);
        blockScr.Node = newNode;
        
        Vector2Int gridKey = new Vector2Int(virtualBlockPos.x, virtualBlockPos.z);
        
        _blockLookup[gridKey] = blockScr;
        _nodeLookup[gridKey] = newNode;
        
        // generate dirt below
        GenerateDirtBelow(newBlock);
    }

    private void GenerateDirtBelow(GameObject block)
    {
        BlockScr blockScr = block.GetComponent<BlockScr>();
        
        int finalDirtCount = 0;
        List<Node> neighbourList = GetNeighbourList(blockScr.Node);
            
        int blockGridHeight = blockScr.gridPos.y;
        
        foreach (Node node in neighbourList)
        {
            int neighbourGridHeight = node.Block.GetComponent<BlockScr>().gridPos.y;;
        
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
        
            GameObject newBlock = dirtObjectPool.GetObject();
            newBlock.transform.position = spawnPosition;
            newBlock.transform.name = "Dirt " + spawnPosition.x + " " + spawnPosition.y + " " + spawnPosition.z;
        }
    }

    private void LoadPlayerChunk()
    {
        Vector3 playerChunk = GetPlayerChunkPos();
        
        Vector3 center = new Vector3(
            playerChunk.x * ChunkWorldSize + ChunkWorldSize / 2,
            0f,
            playerChunk.z * ChunkWorldSize + ChunkWorldSize / 2
        );
        
        if (!_savedChunks.TryGetValue(center, out Chunk chunk))
        {
            chunk = new Chunk(center, false);
            _savedChunks[center] = chunk;
            GenerateChunk(chunk);
            LoadChunk(chunk);
        }
    }
    
    private void CleanupCache()
    {
        if (_savedChunks.Count <= MaxCachedChunks)
            return;

        List<Chunk> chunks = new List<Chunk>(_savedChunks.Values);
        chunks.Sort((a, b) => a.LastUsedTime.CompareTo(b.LastUsedTime));

        int toRemove = _savedChunks.Count - MaxCachedChunks;

        for (int i = 0; i < toRemove; i++)
        {
            Chunk chunk = chunks[i];

            if (!chunk.IsLoaded)
            {
                _savedChunks.Remove(chunk.Position);
                
                chunk.BlockPositions.Clear();
            }
        }
    }

    private IEnumerable<Vector2Int> GetSpiralOffsets(int radius)
    {
        int x = 0;
        int z = 0;
        int dx = 0;
        int dz = -1;
        
        int max = (radius * 2 + 1) * (radius * 2 + 1);

        for (int i = 0; i < max; i++)
        {
            // inside circle radius
            if (Mathf.Abs(x) <= radius && Mathf.Abs(z) <= radius)
            {
                yield return new Vector2Int(x, z);
            }
            
            // spiral step

            if (x == z || (x < 0 && x == -z) || (x > 0 && x == 1 - z))
            {
                int temp = dx;
                dx = -dz;
                dz = temp;
            }
            
            x += dx;
            z += dz;
        }
    }

    private Mesh GenerateChunkMesh(Chunk chunk)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        int vertIndex = 0;
        float s = BlockSize;

        HashSet<Vector3Int> blockSet = new HashSet<Vector3Int>(chunk.BlockPositions);
        
        BlockTextures tex = new BlockTextures {
            Top = "Grass Top",
            Bottom = "Grass Bottom",
            Side0 = "Grass Side 0",
            Side1 = "Grass Side 1",
            Side2 = "Grass Side 2",
            Side3 = "Grass Side 3"
        };

        foreach (Vector3Int b in chunk.BlockPositions)
        {
            Vector3 pos = new Vector3(b.x, b.y, b.z);

            foreach (var dir in Directions)
            {
                Vector3Int neighbour = b + dir.Offset;
                if (blockSet.Contains(neighbour))
                    continue; // hidden face

                // add face vertices (already ordered so triangles (0,1,2) & (2,3,0) give outward normal)
                vertices.Add(pos + dir.Verts[0] * s);
                vertices.Add(pos + dir.Verts[1] * s);
                vertices.Add(pos + dir.Verts[2] * s);
                vertices.Add(pos + dir.Verts[3] * s);

                // triangles (two tris per quad)
                triangles.Add(vertIndex + 0);
                triangles.Add(vertIndex + 1);
                triangles.Add(vertIndex + 2);

                triangles.Add(vertIndex + 2);
                triangles.Add(vertIndex + 3);
                triangles.Add(vertIndex + 0);

                // pick uv rect based on face direction
                string spriteName = tex.Bottom;
                if (dir.Offset == Vector3Int.up) spriteName = tex.Top;
                else if (dir.Offset == Vector3Int.down) spriteName = tex.Bottom;
                else if (dir.Offset == Vector3Int.left) spriteName = tex.Side0;
                else if (dir.Offset == Vector3Int.back) spriteName = tex.Side1;
                else if (dir.Offset == Vector3Int.right) spriteName = tex.Side2;
                else if (dir.Offset == Vector3Int.forward) spriteName = tex.Side3;

                Rect uvRect = AtlasHelperScr.instance.GetUVRect(spriteName);
                
                // quad uvs in rect
                uvs.Add(new Vector2(uvRect.xMin, uvRect.yMin));
                uvs.Add(new Vector2(uvRect.xMax, uvRect.yMin));
                uvs.Add(new Vector2(uvRect.xMax, uvRect.yMax));
                uvs.Add(new Vector2(uvRect.xMin, uvRect.yMax));

                vertIndex += 4;
            }
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
    
    private struct FaceDir
    {
        public Vector3Int Offset;
        public Vector3[] Verts;
    }

    private static readonly FaceDir[] Directions = new FaceDir[]
    {
        // Top (+Y) -> verts ordered so normal points +Y
        new FaceDir {
            Offset = new Vector3Int(0, 1, 0),
            Verts = new [] {
                new Vector3(0,1,0), new Vector3(0,1,1),
                new Vector3(1,1,1), new Vector3(1,1,0)
            }
        },

        // Bottom (-Y) -> verts ordered so normal points -Y
        new FaceDir {
            Offset = new Vector3Int(0, -1, 0),
            Verts = new [] {
                new Vector3(0,0,0), new Vector3(1,0,0),
                new Vector3(1,0,1), new Vector3(0,0,1)
            }
        },

        // Front (+Z)
        new FaceDir {
            Offset = new Vector3Int(0, 0, 1),
            Verts = new [] {
                new Vector3(0,0,1), new Vector3(1,0,1),
                new Vector3(1,1,1), new Vector3(0,1,1)
            }
        },

        // Back (-Z)
        new FaceDir {
            Offset = new Vector3Int(0, 0, -1),
            Verts = new [] {
                new Vector3(1,0,0), new Vector3(0,0,0),
                new Vector3(0,1,0), new Vector3(1,1,0)
            }
        },

        // Right (+X)
        new FaceDir {
            Offset = new Vector3Int(1, 0, 0),
            Verts = new [] {
                new Vector3(1,0,1), new Vector3(1,0,0),
                new Vector3(1,1,0), new Vector3(1,1,1)
            }
        },

        // Left (-X)
        new FaceDir {
            Offset = new Vector3Int(-1, 0, 0),
            Verts = new [] {
                new Vector3(0,0,0), new Vector3(0,0,1),
                new Vector3(0,1,1), new Vector3(0,1,0)
            }
        }
    };

    private struct BlockTextures
    {
        public string Top;
        public string Bottom;
        public string Side0;
        public string Side1;
        public string Side2;
        public string Side3;
    }

    // public List<Node> GetNodeCrossDeepNeighbourList(Node node, int depth)
    // {
    //     List<Node> neighbourList = new List<Node>() { node };
    //     List<Node> checkList = new List<Node>() { node };
    //
    //     for (int i = 0; i < depth; i++)
    //     {
    //         List<Node> neighboursInDepth = new List<Node>();
    //         foreach (Node nodeToCheck in checkList)
    //         {
    //             List<Node> newNeighbourList = GetNeighbourList(nodeToCheck);
    //
    //
    //             foreach (Node newNeighbour in newNeighbourList)
    //             {
    //                 if (neighbourList.Contains(newNeighbour))
    //                 {
    //                     continue;
    //                 }
    //
    //                 neighboursInDepth.Add(newNeighbour);
    //                 neighbourList.Add(newNeighbour);
    //             }
    //         }
    //
    //         foreach (Node newNeighbour in neighboursInDepth)
    //         {
    //             checkList.Add(newNeighbour);
    //         }
    //     }
    //
    //     return neighbourList;
    // }
    //
    // public List<Node> GetNodeSquareDeepNeighbourList(Node node, int depth)
    // {
    //     List<Node> neighbourList = new List<Node>();
    //
    //     int centreX = node.GridPos.x;
    //     int centreZ = node.GridPos.z;
    //
    //     for (int dx = -depth; dx <= depth; dx++)
    //     {
    //         for (int dy = -depth; dy <= depth; dy++)
    //         {
    //             int newX = centreX + dx;
    //             int newZ = centreZ + dy;
    //
    //             // Ensure the new coordinates are within the grid bounds
    //             Node newNeighbour = GetNode(newX, newZ);
    //             if (newNeighbour != null)
    //             {
    //                 neighbourList.Add(newNeighbour);
    //             }
    //         }
    //     }
    //
    //     return neighbourList;
    // }

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

        Block.GetComponent<BlockScr>().gridPos.x = xPos;
        Block.GetComponent<BlockScr>().gridPos.y = yPos;
        Block.GetComponent<BlockScr>().gridPos.z = zPos;
        
        Block.GetComponent<BlockScr>().Node = this;
    }

    public void CalculateFCost()
    {
        PathfindingGFH.z = PathfindingGFH.x + PathfindingGFH.y;
    }
}

public class Chunk
{
    public Vector3 Position;
    public List<Vector3Int> BlockPositions;
    public bool IsGenerated;
    public bool IsLoaded;
    public float LastUsedTime;
    
    public GameObject ChunkObject;
    public Mesh Mesh;

    public Chunk(Vector3 chunkPos, bool isGenerated)
    {
        Position = chunkPos;
        IsGenerated = isGenerated;
        LastUsedTime = Time.time;
    }
}