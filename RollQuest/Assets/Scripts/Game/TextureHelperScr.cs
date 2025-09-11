using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

public class TextureHelperScr : MonoBehaviour
{
    public static TextureHelperScr instance;
    
    [SerializeField] private SpriteAtlas atlas;

    public Material blockMaterial;

    private static readonly (Vector3Int dir, string suffix)[] _faceMap =
    {
        (Vector3Int.up, "Top"),
        (Vector3Int.down, "Bottom"),
        (Vector3Int.left, "Side 0"),
        (Vector3Int.back, "Side 1"),
        (Vector3Int.right, "Side 2"),
        (Vector3Int.forward, "Side 3")
    };

    public Dictionary<(Block.BlockTypes, Vector3Int), string> BlockTextures =
        new Dictionary<(Block.BlockTypes, Vector3Int), string>();
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
        }

        instance = this;
        
        Sprite s = atlas.GetSprite("Grass Bottom");
        if (s != null)
        {
            blockMaterial.mainTexture = s.texture;
        }
        
        InitialiseBlockTextures();
    }
    
    public Rect GetUVRect(string spriteName)
    {
        Sprite s = atlas.GetSprite(spriteName);

        if (s == null)
        {
            //Debug.LogError($"Sprite {spriteName} not found in atlas!");
            return Rect.zero;
        }

        Texture2D tex = s.texture;
        Rect rect = s.textureRect;
        
        // normalised UV (0-1)
        return new Rect(
            rect.x / tex.width,
            rect.y / tex.height,
            rect.width / tex.width,
            rect.height / tex.height
        );
    }

    private void InitialiseBlockTextures()
    {
        BlockTextures = new Dictionary<(Block.BlockTypes, Vector3Int), string>();
        
        AddBlockTextures(Block.BlockTypes.Grass, "Grass");
        AddBlockTextures(Block.BlockTypes.Sand, "Sand");
        AddBlockTextures(Block.BlockTypes.Stone, "Stone");
    }

    private void AddBlockTextures(Block.BlockTypes blockType, string blockName)
    {
        foreach (var (dir, suffix) in _faceMap)
        {
            BlockTextures[(blockType, dir)] = $"{blockName} {suffix}";
        }
    }
}
