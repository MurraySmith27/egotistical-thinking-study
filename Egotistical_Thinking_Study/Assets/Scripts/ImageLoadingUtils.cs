
using System.IO;
using UnityEngine;

public static class ImageLoadingUtils
{
    public static bool LoadImageAsSprite(string filePath, out Sprite sprite)
    {
        sprite = null;
        
        if (File.Exists(filePath))
        {
            byte[] bytes = File.ReadAllBytes(filePath);

            Texture2D tex = new Texture2D(2, 2);

            if (tex.LoadImage(bytes))
            {
                sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }
}
