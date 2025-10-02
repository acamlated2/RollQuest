using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

public class GridControllerScr : MonoBehaviour
{
    public static GridControllerScr instance;

    private const float NoiseScale = 0.02f;
    private const int MaxBlockHeight = 64;
    private const float Sharpness = 2.5f;
    
    private const int ChunkBlockSize = 16;
    private const float LoadRadius = 100;
    private const int ChunkWorldSize = (int)(ChunkBlockSize * Globals.BlockSize);
    
    private Dictionary<Vector3Int, Chunk> _savedChunks = new Dictionary<Vector3Int, Chunk>();
    
    private const int MaxCachedChunks = 100;
    
    private ConcurrentQueue<Chunk> _generatedChunks = new ConcurrentQueue<Chunk>();
    
    private Queue<Chunk> _chunksToLoad = new Queue<Chunk>();
    private bool _isLoadingChunks;
    
    private Queue<Chunk> _chunksToUnload = new Queue<Chunk>();
    private bool _isUnloadingChunks;
    
    private GameObject _player;

    private Vector3Int _lastPlayerChunkPos;
    
    private readonly List<Chunk> _tempChunksToLoad = new List<Chunk>();
    
    private HashSet<Chunk> _newChunksToLoadSet = new HashSet<Chunk>();
    private HashSet<Chunk> _chunksToLoadSet = new HashSet<Chunk>();

    private Chunk loadingChunk;
    
    private Vector3Int[] _horizontalDirs =
    {
        Vector3Int.left, 
        Vector3Int.right, 
        Vector3Int.forward, 
        Vector3Int.back
    };

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
        
        GameplayControllerScr.instance.SetPlayerPosition(GetClosestTopMostBlock(_player.transform.position));
        
        LoadChunks();
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

        if (Input.GetKeyDown("z"))
        {
            Debug.Log(_savedChunks.Count);
        }

        if (Input.GetKeyDown("x"))
        {
            Debug.Log(_isLoadingChunks);
            Debug.Log(loadingChunk.Position);
            Debug.Log(_chunksToLoad.Count);
        }
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
            Vector3Int center = new Vector3Int(
                chunkX * ChunkWorldSize,
                0,
                chunkZ * ChunkWorldSize
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
        foreach (KeyValuePair<Vector3Int, Chunk> kvp in _savedChunks)
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
        Vector3Int playerChunk = GetChunkPos(playerPos);

        return playerChunk;
    }

    private Vector3Int GetChunkPos(Vector3 worldPos)
    {
        float halfSize = ChunkWorldSize / 2;
        
        Vector3Int chunk = new Vector3Int(
            Mathf.FloorToInt((worldPos.x + halfSize) / ChunkWorldSize) * ChunkWorldSize,
            0,
            Mathf.FloorToInt((worldPos.z + halfSize) / ChunkWorldSize) * ChunkWorldSize
        );

        return chunk;
    }

    private void GenerateChunkAsync(Chunk chunk)
    {
        Task.Run((() =>
        {
            GenerateChunk(chunk);
            _generatedChunks.Enqueue(chunk);
        }));
    }

    private void GenerateChunkImmediate(Chunk chunk)
    {
        GenerateChunk(chunk);
        
        _savedChunks[chunk.Position] = chunk;
    }

    private void GenerateChunk(Chunk chunk)
    {
        Vector2 origin = new Vector2(
            chunk.Position.x - ChunkWorldSize / 2,
            chunk.Position.z - ChunkWorldSize / 2);
        
        HashSet<Vector3Int> chunkBlockPositions = new HashSet<Vector3Int>(ChunkBlockSize * ChunkBlockSize);
            
        // generate top surface
        for (int x = 0; x < ChunkBlockSize; x++) 
        {
            for (int z = 0; z < ChunkBlockSize; z++)
            {
                Vector2 worldXZ = origin + new Vector2(x * Globals.BlockSize + Globals.BlockSize / 2,
                    z * Globals.BlockSize + Globals.BlockSize / 2);

                // get height and biome data
                (int height, Block.BlockTypes blockType) = CalculateHeightAndBiome(worldXZ);
                
                Vector3Int gridPos = new Vector3Int(x, height, z);
                
                chunkBlockPositions.Add(gridPos);
                chunk.Blocks[gridPos] = new Block(gridPos, blockType);
                chunk.GroundBlocks[gridPos] = new Block(gridPos, blockType);
            }
        }
        
        // generate structures
        foreach (var kvp in chunk.GroundBlocks.ToList())
        {
            Vector3Int pos = kvp.Key;
            Block block = kvp.Value;
            
            int worldX = chunk.Position.x + pos.x;
            int worldZ = chunk.Position.z + pos.z;
            
            float treeNoise = GetNoiseValue(worldX, worldZ, 0.8f) * 0.9f;
            if (treeNoise > 0.8f && block.BlockType == Block.BlockTypes.Grass)
            {
                // place tree
                StructureScr.instance.PlaceStructure(StructureScr.StructureTypes.Tree, chunk, pos,
                    ref chunkBlockPositions);
            }
            
            float rockNoise = GetNoiseValue(worldX, worldZ, 0.2f) * 1.1f;
            if (rockNoise > 0.95f && block.BlockType != Block.BlockTypes.Sand)
            {
                // place rock
                StructureScr.instance.PlaceStructure(StructureScr.StructureTypes.Rock, chunk, pos,
                    ref chunkBlockPositions);
            }
        }

        // generate extra blocks to fill in the gaps
        foreach (var kvp in chunk.GroundBlocks.ToList())
        {
            Vector3Int gridPos = kvp.Key;
            Block block = kvp.Value;
            
            int blockY = gridPos.y;
            int finalBlockCount = 0;
        
            foreach (Vector3Int dir in _horizontalDirs)
            {
                Block neighbour = GetNeighbour(chunk, gridPos, dir);
                if (neighbour != null)
                {
                    int neighbourY = neighbour.Position.y;
                    
                    if (neighbourY >= blockY - 1) continue;
                    
                    int blockCount = blockY - neighbourY;

                    if (blockCount > finalBlockCount) finalBlockCount = blockCount;
                }
            }
        
            if (finalBlockCount > 0)
            {
                Block.BlockTypes blockType = block.BlockType;
                
                for (int i = 0; i < finalBlockCount; i++)
                {
                    Vector3Int newBlockPos = new Vector3Int(gridPos.x, blockY - i, gridPos.z);
                    
                    chunkBlockPositions.Add(newBlockPos);
                    chunk.Blocks[newBlockPos] = new Block(newBlockPos, blockType);
                    chunk.FillerBlocks[newBlockPos] = new Block(newBlockPos, blockType);
                }
            }
        }

        foreach (var position in chunkBlockPositions)
        {
            Vector3Int pos = position;
            pos.y = 0;
        }
        
        int halfSize = ChunkWorldSize / 2;

        chunk.BlockPositions = chunkBlockPositions
            .GroupBy(pos => new Vector2Int(pos.x - halfSize, pos.z - halfSize))
            .SelectMany(g =>
            {
                var ordered = g.OrderByDescending(p => p.y).ToList();

                return ordered.Select((p, i) =>
                    new KeyValuePair<Vector3Int, Vector3Int>(new Vector3Int(p.x - halfSize, p.z - halfSize, i), p));
            }).ToDictionary(kv => kv.Key, kv => kv.Value);
        
        chunk.IsGenerated = true;
    }

    private Block GetNeighbour(Chunk chunk, Vector3Int blockPos, Vector3Int dir)
    {
        int nx = blockPos.x + dir.x;
        int nz = blockPos.z + dir.z;

        foreach (var kvp in chunk.GroundBlocks)
        {
            if (kvp.Key.x == nx && kvp.Key.z == nz)
            {
                return kvp.Value;
            }
        }

        return null;
    }
    
    private (int height, Block.BlockTypes blockType) CalculateHeightAndBiome(Vector2 blockPos)
    {
        float xCoord = blockPos.x * NoiseScale;
        float zCoord = blockPos.y * NoiseScale;
        
        // biome
        float biomeNoise = GetNoiseValue(xCoord, zCoord, 0.5f);
        biomeNoise = Mathf.Pow(biomeNoise, 0.5f);
        
        float desertWeight   = Mathf.Clamp01(1 - Mathf.Abs(biomeNoise - 0.4f) / 0.4f);
        float plainsWeight   = Mathf.Clamp01(1 - Mathf.Abs(biomeNoise - 0.5f) / 0.33f);
        float mountainWeight = Mathf.Clamp01(1 - Mathf.Abs(biomeNoise - 1.0f) / 0.33f);

        float total = desertWeight + plainsWeight + mountainWeight;
        desertWeight   /= total;
        plainsWeight   /= total;
        mountainWeight /= total;

        // base terrain
        float baseNoise = GetNoiseValue(xCoord, zCoord, 1) * 0.6f +
                          GetNoiseValue(xCoord, zCoord, 2) * 0.3f +
                          GetNoiseValue(xCoord, zCoord, 4) * 0.1f;

        float heightCurve = Mathf.Pow(baseNoise, Sharpness);

        // large scale terrain variation
        float continentNoise = GetNoiseValue(xCoord, zCoord, 0.002f);
        float terrainScale = Mathf.Lerp(0.1f, 1.3f, continentNoise);

        // biome rules
        terrainScale *= desertWeight * 0.5f + plainsWeight * 0.5f + mountainWeight * 3f;
        heightCurve  *= desertWeight * 1f + plainsWeight * 1f + mountainWeight * 1f;

        // final height
        int height = Mathf.RoundToInt(heightCurve * MaxBlockHeight * terrainScale);

        // biome → block type
        Block.BlockTypes blockType = Block.BlockTypes.Grass;
        float maxWeight = Mathf.Max(desertWeight, Mathf.Max(plainsWeight, mountainWeight));

        if (maxWeight == desertWeight) blockType = Block.BlockTypes.Sand;
        else if (maxWeight == plainsWeight) blockType = Block.BlockTypes.Grass;
        else if (maxWeight == mountainWeight) blockType = Block.BlockTypes.Stone;

        return (height, blockType);
    }

    private float GetNoiseValue(float worldX, float worldZ, float scale = 0.1f)
    {
        return Mathf.PerlinNoise((worldX + Globals.seedOffsetX) * scale, (worldZ + Globals.seedOffsetZ) * scale);
    }

    private void LoadChunk(Chunk chunk)
    {
        if (!_savedChunks.ContainsValue(chunk))
            return;
        
        if (chunk.ChunkObject == null)
        {
            chunk.ChunkObject = new GameObject($"Chunk {chunk.Position}");
            chunk.ChunkObject.transform.position = chunk.Position;
            
            MeshFilter mf = chunk.ChunkObject.AddComponent<MeshFilter>();
            MeshRenderer mr = chunk.ChunkObject.AddComponent<MeshRenderer>();
            
            // assign a material
            mr.sharedMaterial = TextureHelperScr.instance.blockMaterial;
            
            // build mesh
            Mesh mesh = GenerateChunkMesh(chunk);
            mesh.name = $"ChunkMesh_{chunk.Position.x}_{chunk.Position.y}_{chunk.Position.z}";
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
                //Debug.Log("not generated yet");
                yield return null; // wait a frame and check again, but don't dequeue
            }

            else
            {
                _chunksToLoad.Dequeue();
                loadingChunk = chunk;
                LoadChunk(chunk);
                yield return null; // wait a frame and run again
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

            UnloadChunk(chunk);
            yield return null; // wait a frame and run again
        }
        
        CleanupCache();
        
        _isUnloadingChunks = false;
    }

    private void UnloadChunk(Chunk chunk)
    {
        if (chunk.ChunkObject != null)
        {
            Destroy(chunk.ChunkObject);
            chunk.ChunkObject = null;
        }

        chunk.IsLoaded = false;
    }

    private void LoadPlayerChunk()
    {
        Vector3Int playerChunk = GetPlayerChunkPos();
        
        if (!_savedChunks.TryGetValue(playerChunk, out Chunk chunk))
        {
            chunk = new Chunk(playerChunk, false);
            _savedChunks[playerChunk] = chunk;
            GenerateChunkImmediate(chunk);
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
        float s = Globals.BlockSize;

        HashSet<Vector3Int> blockSet = chunk.GroundBlocks.Keys.ToHashSet();
        
        Vector3Int minBlock = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
        Vector3Int maxBlock = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

        foreach (var b in blockSet)
        {
            if (b.x < minBlock.x) minBlock.x = b.x;
            if (b.z < minBlock.z) minBlock.z = b.z;

            if (b.x > maxBlock.x) maxBlock.x = b.x;
            if (b.z > maxBlock.z) maxBlock.z = b.z;
        }

        Vector3 chunkCenter = (Vector3)(minBlock + maxBlock) * 0.5f;
        chunkCenter.y = 0;
        chunkCenter.x += s / 2;
        chunkCenter.z += s / 2;

        blockSet.UnionWith(chunk.FillerBlocks.Keys);
        blockSet.UnionWith(chunk.StructureBlocks.Keys);

        foreach (Vector3Int blockPos in blockSet)
        {
            Vector3 pos = (Vector3)blockPos * s - chunkCenter;

            foreach (var dir in Directions)
            {
                Vector3Int neighbour = blockPos + dir.Offset;
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
                Vector3Int faceDir = dir.Offset;
                if (faceDir == Vector3Int.left || faceDir == Vector3Int.right || faceDir == Vector3Int.back ||
                    faceDir == Vector3Int.forward)
                {
                    int faceIndex = faceDir == Vector3Int.left ? 0 : 
                                    faceDir == Vector3Int.right ? 1 : 
                                    faceDir == Vector3Int.back ? 2 : 3;
                    
                    int rand = Mathf.Abs(Hash(blockPos.x, blockPos.y, blockPos.z, faceIndex)) % 4;

                    if (rand == 0) faceDir = Vector3Int.left;
                    else if (rand == 1) faceDir = Vector3Int.right;
                    else if (rand == 2) faceDir = Vector3Int.back;
                    else faceDir = Vector3Int.forward;
                }

                string spriteName =
                    TextureHelperScr.instance.BlockTextures[(chunk.GetBlockInAny(blockPos).BlockType, faceDir)];

                Rect uvRect = TextureHelperScr.instance.GetUVRect(spriteName);
                
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
        
        // // --- recentre pivot ---
        // Vector3 min = mesh.bounds.min;
        // Vector3 max = mesh.bounds.max;
        // Vector3 center = (min + max) * 0.5f;
        // center.y = 0;
        //
        // // add offset so the grid lines up with the world grid
        // center.z += Globals.BlockSize / 2;
        // center.x += Globals.BlockSize / 2;
        //
        // // offset verts
        // for (int i = 0; i < vertices.Count; i++)
        //     vertices[i] -= center;
        //
        // mesh.SetVertices(vertices);
        // mesh.RecalculateBounds();

        return mesh;
    }
    
    private static int Hash(int x, int y, int z, int face)
    {
        int h = x * 374761393 + y * 668265263 + z * 2147483647 + face * 1274126177;
        h = (h ^ (h >> 13)) * 1274126177;
        h = h ^ (h >> 16);
        return h;
    }
    
    private struct FaceDir
    {
        public Vector3Int Offset;
        public Vector3[] Verts;
    }

    private static readonly FaceDir[] Directions = new FaceDir[]
    {
        // Top (+Y)
        new FaceDir {
            Offset = new Vector3Int(0, 1, 0),
            Verts = new [] {
                new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),  new Vector3(0.5f, 0.5f, -0.5f)
            }
        },

        // Bottom (-Y)
        new FaceDir {
            Offset = new Vector3Int(0, -1, 0),
            Verts = new [] {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),   new Vector3(-0.5f, -0.5f, 0.5f)
            }
        },

        // Front (+Z)
        new FaceDir {
            Offset = new Vector3Int(0, 0, 1),
            Verts = new [] {
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),   new Vector3(-0.5f, 0.5f, 0.5f)
            }
        },

        // Back (-Z)
        new FaceDir {
            Offset = new Vector3Int(0, 0, -1),
            Verts = new [] {
                new Vector3(0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f)
            }
        },

        // Right (+X)
        new FaceDir {
            Offset = new Vector3Int(1, 0, 0),
            Verts = new [] {
                new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f)
            }
        },

        // Left (-X)
        new FaceDir {
            Offset = new Vector3Int(-1, 0, 0),
            Verts = new [] {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),   new Vector3(-0.5f, 0.5f, -0.5f)
            }
        }
    };

    private Vector3Int GetWorldBlockPos(Vector3Int chunkPos, Vector3Int blockChunkPos)
    {
        int halfSize = (int)(ChunkWorldSize / 2f);
        
        Vector3Int worldPos = chunkPos - new Vector3Int(halfSize, 0, halfSize) + blockChunkPos; 

        return worldPos;
    }

    private (Vector3Int chunkPos, Vector3Int blockChunkPos) GetBlockChunkPos(Vector3 worldPos)
    {
        Vector3Int chunkPos = GetChunkPos(worldPos);
        
        Vector3Int inChunkPos = Vector3Int.FloorToInt(worldPos) - chunkPos;

        Vector3Int key = new Vector3Int(inChunkPos.x, inChunkPos.z, 0);

        if (_savedChunks.TryGetValue(chunkPos, out Chunk chunk))
        {
            if (chunk.BlockPositions.TryGetValue(key, out Vector3Int blockChunkPos))
            {
                return (chunkPos, blockChunkPos);
            }
            
            Debug.LogError("Block not found: " + key);
            return (Vector3Int.zero, Vector3Int.zero);
        }

        Debug.LogError("Chunk not found: " + chunkPos);
        return (Vector3Int.zero, Vector3Int.zero);
    }
    
    public Vector3Int GetClosestTopMostBlock(Vector3 worldPos)
    {
        (Vector3Int chunkPos, Vector3Int blockChunkPos) = GetBlockChunkPos(worldPos);

        return GetWorldBlockPos(chunkPos, blockChunkPos);
    }
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

        Block.GetComponent<Block>().Position.x = xPos;
        Block.GetComponent<Block>().Position.y = yPos;
        Block.GetComponent<Block>().Position.z = zPos;
        
        Block.GetComponent<Block>().Node = this;
    }

    public void CalculateFCost()
    {
        PathfindingGFH.z = PathfindingGFH.x + PathfindingGFH.y;
    }
}

public class Chunk
{
    public Vector3Int Position;
    public Dictionary<Vector3Int, Vector3Int> BlockPositions;
    public bool IsGenerated;
    public bool IsLoaded;
    public float LastUsedTime;
    
    public GameObject ChunkObject;
    public Mesh Mesh;
    
    public Dictionary<Vector3Int, Block> Blocks;
    public Dictionary<Vector3Int, Block> GroundBlocks;
    public Dictionary<Vector3Int, Block> FillerBlocks;
    public Dictionary<Vector3Int, Block> StructureBlocks;

    public Chunk(Vector3Int chunkPos, bool isGenerated)
    {
        Position = chunkPos;
        IsGenerated = isGenerated;
        LastUsedTime = Time.time;
        
        Blocks = new Dictionary<Vector3Int, Block>();
        GroundBlocks = new Dictionary<Vector3Int, Block>();
        FillerBlocks = new Dictionary<Vector3Int, Block>();
        StructureBlocks = new Dictionary<Vector3Int, Block>();
    }
    
    public bool ContainsBlock(Vector3Int blockPos)
    {
        bool result = Blocks.ContainsKey(blockPos);
        return result;
    }

    public Block GetBlockInAny(Vector3Int blockPos)
    {
        if (Blocks.ContainsKey(blockPos))
        {
            return Blocks[blockPos];
        }

        return null;
    }
}