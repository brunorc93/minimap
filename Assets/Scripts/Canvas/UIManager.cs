using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using TMPro;

// save button
// reset button
// positions and sizes according to screen width & height for biome labels -> anchors! -- currently set for screen of 1920x1920 and no other sizes
// recalculate size per letter (20 was enough for a 36 sized font, but now it is size 47)
// control camera orthographicSize if height > width so that the island fully shows on the screen

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI PdebugText;
    public TextMeshProUGUI SdebugText;
    public TextMeshProUGUI Timer;
    public Button SaveButton;
    private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
    private bool done = false;
    public Minimap minimap;
    public bool save;
    public Texture2D saveData;

    void Start()
    {
        SetPDebugText("");
        SetSDebugText("");
        StartTimer();

        save = false;
        saveData = null;
    }
    void Update()
    {
        if (!done)
        {
            System.TimeSpan ts = stopwatch.Elapsed;
            Timer.SetText(ts.ToString(@"mm\:ss\.fff"));
        }

        if (SaveButton.interactable != save)
        {
          SaveButton.interactable = save;
        }
    }
  public void SetPDebugText(string text) { PdebugText.SetText(text); }
  public void AlingPDTCenter() { PdebugText.alignment = TextAlignmentOptions.Center; }
  public void AlingSDTCenterBottom() 
  { 
    SdebugText.alignment = TextAlignmentOptions.Center;
    SdebugText.rectTransform.anchorMin = new Vector2(0,0);
    SdebugText.rectTransform.anchorMax = new Vector2(1,0);
    Vector2 anchPos = SdebugText.rectTransform.anchoredPosition;
    anchPos.y = 110;
    SdebugText.rectTransform.anchoredPosition = anchPos;
  }
  public void SetSDebugText(string text) { SdebugText.SetText(text); }
  void StartTimer()
  {
      Timer.SetText("00:00.000");
      stopwatch.Start();
  }
  public void SetDone(bool newBool)
  {
      done = newBool;
      stopwatch.Stop();
  }
  public void AddLocationToMinimap(string location, Color color, GameObject biome) { minimap.AddLocation(location, color, biome); }
  public void ActivateMinimap(bool activate) { minimap.Activate(activate); }
  public void ResetGen()
  {
    save = false;
    saveData = null;
    done = false;

    ActivateMinimap(false);

    GameObject gen = GameObject.Find("Generator");
    gen.TryGetComponent<BiomeGenerator>(out BiomeGenerator BG);
    gen.TryGetComponent<TerrainGenerator>(out TerrainGenerator TG);

    if (TG != null) { Destroy(TG); }
    if (BG != null) { Destroy(BG); }

    gen.AddComponent<TerrainGenerator>();
    foreach (Transform child in gen.transform)
    {
      if (child.name !="Outline")
      {
        if (child.gameObject != null) { Destroy(child.gameObject); }
      }
      else {
        child.gameObject.SetActive(false);
      }
    }

    minimap.Clear();

    PdebugText.alignment = TextAlignmentOptions.Left;
    PdebugText.SetText("");
    SdebugText.alignment = TextAlignmentOptions.Left;
    SdebugText.rectTransform.anchorMin = new Vector2(0,1);
    SdebugText.rectTransform.anchorMax = new Vector2(1,1);
    Vector2 anchPos = SdebugText.rectTransform.anchoredPosition;
    anchPos.y = -130;
    SdebugText.rectTransform.anchoredPosition = anchPos;
    SdebugText.SetText("");

    stopwatch.Stop();
    stopwatch = new System.Diagnostics.Stopwatch();
    Timer.SetText("00:00.000");
    stopwatch.Start();
  }
  public void Save()
  {
    save = false;

    int n = 0;
    string path_1 = Application.dataPath+"/Data/Saved/BG/";
    Directory.CreateDirectory(@path_1);
    path_1 += "_locations_info.txt";
    if (!File.Exists(path_1)) { File.WriteAllText(path_1,""); } 
    else 
    {
      string dataRead = File.ReadAllText(path_1);
      string[] dataLines = dataRead.Split('\n');
      n = dataLines.Length-1;
    }
    byte[] bytes = saveData.EncodeToPNG();
    string path_2 = Application.dataPath + "/Data/Saved/BG/";
    Directory.CreateDirectory(@path_2);
    path_2 += n.ToString("D3")+".png";
    File.WriteAllBytes(path_2, bytes);

    string text = "n = "+n.ToString("D3")+";";
    foreach(string name in minimap.GetLocations()) { text+=name; text+=";"; }
    text+="\n";
    File.AppendAllText(path_1,text);

    PdebugText.SetText("File saved at "+'"'+path_2+'"');
    PdebugText.alignment = TextAlignmentOptions.Left;

    StartCoroutine(SetPDebugTextafterT("Minimap",5));
  }

  public IEnumerator SetPDebugTextafterT(string text, int time)
  {
    yield return new WaitForSeconds(time);
    PdebugText.SetText(text);
    PdebugText.alignment = TextAlignmentOptions.Center;
  }
  public IEnumerator SetSDebugTextafterT(string text, int time)
  {
    yield return new WaitForSeconds(time);
    SdebugText.SetText(text);
  }
}