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

    private const float NoiseScale = 0.01f;
    private const int MaxBlockHeight = 64;
    private const float Sharpness = 2.5f;
    
    private const float BlockSize = 2;
    private const int ChunkBlockSize = 16;
    private const float LoadRadius = 500;
    private const int ChunkWorldSize = (int)(ChunkBlockSize * BlockSize);
    
    private Dictionary<Vector3, Chunk> _savedChunks = new Dictionary<Vector3, Chunk>();
    
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
        
        // GameplayControllerScr.instance.SetPlayerPosition();
        Debug.LogError("set player position first");
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

        int offset = GameplayControllerScr.instance.seed % 1000;
        List<Vector3Int> chunkBlockPositions = new List<Vector3Int>(ChunkBlockSize * ChunkBlockSize);
        
        Vector3Int currentBlockPos;
            
        for (int x = 0; x < ChunkBlockSize; x++) 
        {
            for (int z = 0; z < ChunkBlockSize; z++) 
            {
                Vector2 blockPos = origin + new Vector2(x * BlockSize, z * BlockSize);

                float xCoord = (blockPos.x + offset) * NoiseScale;
                float zCoord = (blockPos.y + offset) * NoiseScale;
                
                // biome
                float biomeNoise = Mathf.PerlinNoise(xCoord * 0.5f, zCoord * 0.5f);
                biomeNoise = Mathf.Pow(biomeNoise, 0.5f);

                float desertWeight = Mathf.Clamp01(1 - Mathf.Abs(biomeNoise - 0.4f) / 0.4f);
                float plainsWeight = Mathf.Clamp01(1 - Mathf.Abs(biomeNoise - 0.5f) / 0.33f);
                float mountainWeight = Mathf.Clamp01(1 - Mathf.Abs(biomeNoise - 1.0f) / 0.33f);
                
                float total = desertWeight + plainsWeight + mountainWeight;
                desertWeight /= total;
                plainsWeight /= total;
                mountainWeight /= total;

                // base
                float baseNoise = Mathf.PerlinNoise(xCoord, zCoord) * 0.6f +
                                  Mathf.PerlinNoise(xCoord * 2, zCoord * 2) * 0.3f +
                                  Mathf.PerlinNoise(xCoord * 4, zCoord * 4) * 0.1f;

                float heightCurve = Mathf.Pow(baseNoise, Sharpness);

                // Large scale terrain variation
                float continentNoise = Mathf.PerlinNoise(xCoord * 0.002f, zCoord * 0.002f);
                float terrainScale = Mathf.Lerp(0.1f, 1.3f, continentNoise);
                
                // apply biome rules
                terrainScale *= desertWeight * 0.5f + plainsWeight * 0.5f + mountainWeight * 3f;
                heightCurve *= desertWeight * 1 + plainsWeight * 1 + mountainWeight * 1f;

                // apply noise
                int height = Mathf.RoundToInt(heightCurve * MaxBlockHeight * terrainScale);
                float worldY = height * BlockSize;

                currentBlockPos = new Vector3Int((int)blockPos.x, (int)worldY, (int)blockPos.y);
                chunkBlockPositions.Add(currentBlockPos);

                Block.BlockTypes blockType = Block.BlockTypes.Grass;
                float maxWeight = Mathf.Max(desertWeight, Mathf.Max(plainsWeight, mountainWeight));

                if (maxWeight == desertWeight) blockType = Block.BlockTypes.Sand;
                else if (maxWeight == plainsWeight) blockType = Block.BlockTypes.Grass;
                else if (maxWeight == mountainWeight) blockType = Block.BlockTypes.Stone;

                chunk.Blocks[currentBlockPos] = new Block(currentBlockPos, blockType);
            }
        }
        
        chunk.BlockPositions = chunkBlockPositions;
        chunk.IsGenerated = true;
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
        float s = BlockSize;

        HashSet<Vector3Int> blockSet = new HashSet<Vector3Int>(chunk.BlockPositions);
        
        BlockTextures tex = new BlockTextures {
            GrassTop = "Grass Top",
            GrassBottom = "Grass Bottom",
            GrassSide = "Grass Side 0",
            SandTop = "Sand Top",
            SandBottom = "Sand Bottom",
            SandSide = "Sand Side0",
            StoneTop = "Stone Top",
            StoneBottom = "Stone Bottom",
            StoneSide = "Stone Side0"
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
                string spriteName = tex.GrassBottom;

                if (chunk.Blocks[b].BlockType == Block.BlockTypes.Grass)
                {
                    if (dir.Offset == Vector3Int.up) spriteName = tex.GrassTop;
                    else if (dir.Offset == Vector3Int.down) spriteName = tex.GrassBottom;
                    else if (dir.Offset == Vector3Int.left) spriteName = tex.GrassSide;
                    else if (dir.Offset == Vector3Int.back) spriteName = tex.GrassSide;
                    else if (dir.Offset == Vector3Int.right) spriteName = tex.GrassSide;
                    else if (dir.Offset == Vector3Int.forward) spriteName = tex.GrassSide;
                }
                else if (chunk.Blocks[b].BlockType == Block.BlockTypes.Sand)
                {
                    if (dir.Offset == Vector3Int.up) spriteName = tex.SandTop;
                    else if (dir.Offset == Vector3Int.down) spriteName = tex.SandBottom;
                    else if (dir.Offset == Vector3Int.left) spriteName = tex.SandSide;
                    else if (dir.Offset == Vector3Int.back) spriteName = tex.SandSide;
                    else if (dir.Offset == Vector3Int.right) spriteName = tex.SandSide;
                    else if (dir.Offset == Vector3Int.forward) spriteName = tex.SandSide;
                }
                else if (chunk.Blocks[b].BlockType == Block.BlockTypes.Stone)
                {
                    if (dir.Offset == Vector3Int.up) spriteName = tex.StoneTop;
                    else if (dir.Offset == Vector3Int.down) spriteName = tex.StoneBottom;
                    else if (dir.Offset == Vector3Int.left) spriteName = tex.StoneSide;
                    else if (dir.Offset == Vector3Int.back) spriteName = tex.StoneSide;
                    else if (dir.Offset == Vector3Int.right) spriteName = tex.StoneSide;
                    else if (dir.Offset == Vector3Int.forward) spriteName = tex.StoneSide;
                }

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
        public string GrassTop;
        public string GrassBottom;
        public string GrassSide;
        public string SandTop;
        public string SandBottom;
        public string SandSide;
        public string StoneTop;
        public string StoneBottom;
        public string StoneSide;
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
    public Vector3 Position;
    public List<Vector3Int> BlockPositions;
    public bool IsGenerated;
    public bool IsLoaded;
    public float LastUsedTime;
    
    public GameObject ChunkObject;
    public Mesh Mesh;
    
    public Dictionary<Vector3Int, Block> Blocks = new Dictionary<Vector3Int, Block>();

    public Chunk(Vector3 chunkPos, bool isGenerated)
    {
        Position = chunkPos;
        IsGenerated = isGenerated;
        LastUsedTime = Time.time;
    }
}

public enum BiomeTypes
{
    Plains, 
    Desert, 
    Mountain, 
}