using UnityEngine;

public class CameraResolution : MonoBehaviour
{
    Vector2Int resolution;
    float original_camera_size;

    void Awake()
    {
      original_camera_size = this.GetComponent<Camera>().orthographicSize;

      SetOrthoSize();
    }

    void Update()
    {
        if (resolution.x != Screen.width || resolution.y != Screen.height)
        {
            SetOrthoSize();
        }
    }

    void SetOrthoSize()
    {
        resolution = new Vector2Int(Screen.width, Screen.height);

        if (resolution.y > resolution.x)
        {
            this.GetComponent<Camera>().orthographicSize = (original_camera_size*(float)resolution.y/(float)resolution.x);
        }
        else 
        {
            this.GetComponent<Camera>().orthographicSize = original_camera_size;
        }
    }

}