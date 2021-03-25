using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Minimap : MonoBehaviour
{
    private Camera cam;
    private bool minimapIsActive;
    public UIManager UIMan;

    private List<Color> colors = new List<Color>();
    private List<string> locations = new List<string>();
    private List<GameObject> biomes = new List<GameObject>();
    
    void Awake()
    {
      minimapIsActive = false;
      cam = transform.GetComponent<Camera>();
    }
    void Update()
    {
        if (minimapIsActive)
        {
            Vector3 direction = Vector3.forward;

            RaycastHit hit;

            if (Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition) , out hit)) 
            {
                if (hit.collider.tag != "Minimap") 
                {
                    UIMan.SetSDebugText("");
                    for (int i=0; i< biomes.Count; i++)
                    {
                      biomes[i].SetActive(false);
                    }
                    return;
                }

                Renderer renderer = hit.collider.GetComponent<MeshRenderer>();
                Texture2D texture2D = renderer.material.mainTexture as Texture2D;
                Vector2 pCoord = hit.textureCoord;
                pCoord.x *= texture2D.width;
                pCoord.y *= texture2D.height;

                Vector2 tiling = renderer.material.mainTextureScale;
                Color color = texture2D.GetPixel(Mathf.FloorToInt(pCoord.x * tiling.x) , Mathf.FloorToInt(pCoord.y * tiling.y));
                color.a = 1f;

                if (colors.Contains(color))
                {
                  UIMan.SetSDebugText(locations[colors.IndexOf(color)]);
                  for (int i=0; i< biomes.Count; i++)
                  {
                    if (i == colors.IndexOf(color)) { biomes[i].SetActive(true); }
                    else { biomes[i].SetActive(false); }
                  }
                }
                else 
                { 
                  UIMan.SetSDebugText(""); 
                  for (int i=0; i< biomes.Count; i++)
                  {
                    biomes[i].SetActive(false);
                  }
                }
            }
            else
            {
              UIMan.SetSDebugText("");
              for (int i=0; i< biomes.Count; i++)
              {
                biomes[i].SetActive(false);
              }
            }
        }
    }

    public void Activate(bool activate) 
    { 
      minimapIsActive = activate; 
      UIMan.AlingSDTCenterBottom();
    }
    public void AddLocation(string location, Color color, GameObject biome)
    {
      locations.Add(location);
      colors.Add(color);
      biomes.Add(biome);
    }

    public void Clear()
    {
      colors = new List<Color>();
      locations = new List<string>();
      biomes = new List<GameObject>();
    }

    public List<string> GetLocations() { return locations; }

}
