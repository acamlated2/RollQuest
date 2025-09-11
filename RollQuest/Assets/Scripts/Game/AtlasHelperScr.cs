using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

public class AtlasHelperScr : MonoBehaviour
{
    public static AtlasHelperScr instance;
    
    [SerializeField] private SpriteAtlas atlas;

    public Material blockMaterial;
    
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
}
