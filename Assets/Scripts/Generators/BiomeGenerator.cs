using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

public class BiomeGenerator : MonoBehaviour
{
//----------------- vars ------------------
    public bool useSeed = false;
    public int randomSeed = 42;
    [HideInInspector]
    public UIManager UIMan;
    [HideInInspector]
    public bool destroyOnEnd = true;
    [HideInInspector]
    public bool finished = false;
    [HideInInspector]
    public int auxValue = 0;
    [HideInInspector]
    public int generation_phase = 0;
    [HideInInspector]
    public int roundNumber = 0;
    [HideInInspector]
    public int size = 0;
    [HideInInspector]
    public int updateCount = 0;
    [HideInInspector]
    public Texture2D shape_bmp;
    private bool nodesChosen = false;
    private List<string> inlandNodeNames = new List<string>();
    private List<Vector2Int> inlandV2 = new List<Vector2Int>();
    private int[] zonesRed = null;
    private int[] zonesArea = null;
    private List<List<Vector2Int>> zonesV2 = new List<List<Vector2Int>>();
    private Vector2[] perlinPoint = null;
    private float[][] gradients = null;
    private bool perlinDone = true;
    private List<string> coastalNodeNames = new List<string>();
    private List<int> coastalBlue = new List<int>();
    private List<Vector2Int> coastalV2 = new List<Vector2Int>();
    private Vector2Int startingCoastalWalkPoint = new Vector2Int(0,0);
    private int coastSize;
    private List<Color> fullZonesColors = new List<Color>();
    private List<string> fullZonesNames = new List<string>();
    public TextAsset BGNodes;
    private bool organized = false;

// main ---------------------------------------

    void Start()
    {
      BGNodes = Resources.Load<TextAsset>("Data/BGNodes");
    }
    void _Start()
    {
      if (useSeed) { Random.InitState(randomSeed); }
    }

    void Update()
    {
      if (!finished)
      {
        updateCount++;
        switch (generation_phase)
        {
          case 0: // Creating TG
            if (auxValue == 0) { UIMan = GameObject.Find("/UI/Canvas").GetComponent<UIManager>(); }
            CallDebug("Starting Biome Generator");
            _Start();
            generation_phase++;
            break;
          case 1:  // Setting up renderer
            CallDebug("Setting renderer");
            SetupRenderer();
            generation_phase++;
            break;
          case 2:  // Clear shape_bmp
            CallDebug("Clearing shape_bmp colors");
            shape_bmp = shape_bmp.ResetColors();
            generation_phase++;
            break;
          case 3:  // Choosing Nodes
            CallDebug("Choosing Biome Nodes from list of biomes");
            if (!nodesChosen)
            {
              ChooseNodes();
              nodesChosen = true;
            }
            generation_phase++;
            break;
          case 4:  // Placing Coastal Nodes in island
            CallDebug("Placing Coastline Nodes");
            SetCoastToStartPlacing_part1();
            SetStartingCoastalWalkPoint();
            SetCoastToStartPlacing_part2();
            CleanCoastalLeftovers();
            PlaceCoastalNodes();
            generation_phase++;
            break;
          case 5:  // Expanding Coastal Nodes
            CallDebug("Expanding Coastline Nodes"); // nodes grow until alpha value turns 253/255 -> growth similar to celular automata
            if (!ExpandedCoastalNodes()) { auxValue++; }
            else
            {
              generation_phase++;
              auxValue = 0;
            }
            
            break;
          case 6:  // Total Alpha Clean Up  
            CallDebug("Transforming color.a!=0f & 1f into color.a=1f");
            shape_bmp = shape_bmp.Non0AlphaTo1();
            generation_phase++;
            auxValue = 0;
            break;
          case 7:  // Placing Inland Nodes in island
            CallDebug("Placing Inland Nodes");
            PlaceInlandNodes();
            generation_phase++;
            break;
          case 8:  // Expanding Inland Nodes
            CallDebug("Expanding Inland Nodes");
            if (auxValue%350 == 0 && perlinDone)
            {
              perlinDone = false;
              auxValue++;
              StartCoroutine(CalculatePerlinNoise());
            }
            if (perlinDone)
            {
              if (!ExpandedInlandNodes()) { auxValue++; }
              else 
              {
                generation_phase++;
                auxValue = 0;
            } }
            break;
          case 9:  // Checking area values and calling for a reset if any biome has less than 9k pixels
            CallDebug("Checking Biomes' area values");
            bool needsReset = false;
            for (int i=0; i<inlandNodeNames.Count; i++) { if(zonesArea[i]<9000) { needsReset = true; break; } }
            if (needsReset)
            {
              GenerationReset();
              UIMan.SetSDebugText("Reseting generation due to a really small inland biome");
              StartCoroutine(UIMan.SetSDebugTextafterT("",2));
              generation_phase = 2;
            } else { generation_phase++; }
            break;
          case 10: // If Village area is bellow average: switch with average area biome
            CallDebug("checking Village Biome size");
            CheckVillageArea();
            generation_phase++;
            break;
          case 11: // Organizing Zone Data
            CallDebug("Organizing Zone Data");
            if (auxValue == 0)
            {
              StartCoroutine(OrganizeFullZones(true));
            }
            if (!organized)
            {
              auxValue++;
            }
            else
            {
              organized = false;
              UIMan.ActivateMinimap(true);
              auxValue = 0;
              generation_phase++;
            }
            // OrganizeFullZones(); // this could have been done in the inlandNodes expansion phase
            // UIMan.ActivateMinimap(true);
            // generation_phase++;
            break;
          case 12: // Creating Outlines and Highlighted areas
            CallDebug("Creating Outlines");
            GenerateOutline();
            generation_phase++;
            break;
          case 13: // Finished
            CallDebug("Finished BiomeGenerator.cs", false);
            UIMan.save = true;
            UIMan.saveData = shape_bmp;
            finished = true;
            break;
        }
        Showcase();
      } else {
        UIMan.SetDone(true);
        if (destroyOnEnd)
        {
          destroyOnEnd = false;
          StartCoroutine(finalCountdown());
    } } }

// methods ------------------------------------
    IEnumerator CalculatePerlinNoise()
    {
      System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
      stopwatch.Start();
      int yieldCount = 1;
      perlinPoint = new Vector2[inlandNodeNames.Count];
      gradients = new float[inlandNodeNames.Count][];
      for (int a=0; a<inlandNodeNames.Count; a++)
      {
        perlinPoint[a] = new Vector2(Random.Range(-500f,500f),Random.Range(-500f,500f));
        gradients [a] = new float[size*size];
        float maxValue = float.MinValue;
        float minValue = float.MaxValue;
        for (int i=0; i<size; i++)
        {
          for (int j=0; j<size; j++)
          {
            if (stopwatch.ElapsedMilliseconds-yieldCount*20>0)
            {
              yieldCount++;
              yield return null;
            }
            float x = (float)i*0.39f;
            float y = (float)j*0.39f;
            float newValue = Mathf.PerlinNoise(x+perlinPoint[a].x,y+perlinPoint[a].y);
            float m = 1f;
            for (int k=0; k<5; k++)
            {
              x = x/2;
              y = y/2;
              m = m*2.1f;
              newValue+=Mathf.PerlinNoise(x+perlinPoint[a].x,y+perlinPoint[a].y)*m;
            }
            gradients[a][i*size+j]=newValue;
            if (newValue>maxValue) { maxValue = newValue; }
            if (newValue<minValue) { minValue = newValue; }
        } }
        for (int i=0; i<size; i++)
        {
          for (int j=0; j<size; j++)
          {
            float result = (gradients[a][i*size+j]-minValue)/(maxValue-minValue);
            result = result - result%0.05f;
            if (result >= 1-7*0.05f) { result = 0.99f; }
            if (result <= 7*0.05f) { result = 0f; }
            gradients[a][i*size+j] = result;
      } } }
      stopwatch.Stop();
      perlinDone = true;
    }
    void CleanCoastalLeftovers()
    {
      List<Vector2Int> auxList = new List<Vector2Int>();
      foreach(Vector2Int point in coastalV2)
      {
        Color newColor = shape_bmp.GetPixel(point.x,point.y);
        if (Mathf.RoundToInt(newColor.a*255)==253) { auxList.Add(point); } 
        else 
        {
          newColor.a = 1f;
          shape_bmp.SetPixel(point.x,point.y,newColor);
      } }
      coastalV2 = auxList;
    }
    void CheckVillageArea()
    {
      int villageIndex = 0;
      int villageArea = 0;
      int averageArea = 0;

      bool aboveAverage = false;

      int maxArea = 0;
      int maxIndex = 0;

      for (int i=0; i<inlandNodeNames.Count; i++)
      {
        if (inlandNodeNames[i] == "Village")
        {
          villageIndex = i;
          villageArea = zonesArea[i];
        }
        averageArea+=zonesArea[i];
        if (zonesArea[i]>maxArea)
        {
          maxArea = zonesArea[i];
          maxIndex = i;
      } }
      averageArea /= inlandNodeNames.Count;
      if (villageArea>=averageArea) { aboveAverage = true; }
      if (!aboveAverage)
      {
        string temp = inlandNodeNames[maxIndex];
        inlandNodeNames[maxIndex] = inlandNodeNames[villageIndex];
        inlandNodeNames[villageIndex] = temp;
    } }
    void ChooseCoastalNodes(int n)
    {
      List<string> coastalData = GatherCoastalData();
      List<string> auxList = new List<string>();
      foreach(string nodeName in coastalData)
      {
        if (nodeName.Contains("MustHave"))
        {
          n--;
          string newNodeName = nodeName.Replace("MustHave","");
          coastalNodeNames.Add(newNodeName);
        } else { auxList.Add(nodeName); }
      }
      coastalData = auxList;
      for (int i=0; i<n; i++)
      {
        int rnd = Random.Range(0,coastalData.Count-1);
        string nodeName = coastalData[rnd];
        if (nodeName.Contains("Unlimited"))
        {
          string newNodeName = nodeName.Replace("Unlimited","");
          coastalNodeNames.Add(newNodeName);
        } else 
        {
          coastalNodeNames.Add(nodeName);
          coastalData.RemoveAt(rnd);
    } } }
    void ChooseInlandNodes(int n)
    {
      List<string> inlandData = GatherInlandData();
      List<string> auxList = new List<string>();
      foreach(string nodeName in inlandData)
      {
        if (nodeName.Contains("MustHave"))
        {
          n--;
          string newNodeName = nodeName.Replace("MustHave","");
          inlandNodeNames.Add(newNodeName);
        } else { auxList.Add(nodeName); }
      }
      inlandData = auxList;
      for (int i=0; i<n; i++)
      {
        int rnd = Random.Range(0,inlandData.Count-1);
        string nodeName = inlandData[rnd];
        if (nodeName.Contains("Unlimited"))
        {
          string newNodeName = nodeName.Replace("Unlimited","");
          inlandNodeNames.Add(newNodeName);
        } else 
        {
          inlandNodeNames.Add(nodeName);
          inlandData.RemoveAt(rnd);
    } } }
    void ChooseNodes()
    {
      int numberOfCoastalNodes = 0;
      int minCN = 9;
      int maxCN = 14;
      int perimeterSize = shape_bmp.MeasurePerimeter();
      int minPer = 2000;
      int maxPer = 6000;
      numberOfCoastalNodes = Mathf.RoundToInt(((float)(perimeterSize-minPer)*(float)(maxCN-minCN)/(float)(maxPer-minPer))+minCN);

      int numberOfInlandNodes = 0;
      int minIN = 7;  //8
      int maxIN = 10;//12
      int islandArea = shape_bmp.MeasureArea();
      int minArea = 120000;
      int maxArea = 260000;
      numberOfInlandNodes = Mathf.RoundToInt(((float)(islandArea-minArea)*(float)(maxIN-minIN)/(float)(maxArea-minArea))+minIN);
      ChooseCoastalNodes(numberOfCoastalNodes);
      ChooseInlandNodes(numberOfInlandNodes);
    }
    bool ExpandedCoastalNodes()
    {
      bool done = false;
      if (coastalV2.Count>0)
      {
        List<Vector2Int> auxList = new List<Vector2Int>();
        foreach(Vector2Int point in coastalV2)
        {
          Color newColor = shape_bmp.GetPixel(point.x,point.y);
          if (Mathf.RoundToInt(newColor.a*255)<254)
          {
            float rnd = Random.value;
            auxList.Add(point);
            if (rnd>0.2f) { newColor.a = (float)(Mathf.RoundToInt(newColor.a*255)+1)/255f; } // there is a chance it gets a free grow!
            shape_bmp.SetPixel(point.x,point.y,newColor);
            float rnd2 = Random.value;
            if (rnd2>0.3f)
            { // there is a chance it doesn't grow! at all
              foreach (Vector2Int neighbour in point.Neighbours(size))
              {
                float rnd3 = Random.value;
                if (rnd3>0.4f)
                { // there is a chance its growth isn't received by some neighbour
                  if (shape_bmp.GetPixel(neighbour.x,neighbour.y).a==1f)
                  {
                    shape_bmp.SetPixel(neighbour.x,neighbour.y,newColor);
                    auxList.Add(neighbour);
        } } } } } }
        coastalV2 = auxList;
      } else { done = true; }
      return done;
    }
    bool ExpandedInlandNodes()
    {
      bool done = false;
      List<Vector2Int> auxList = new List<Vector2Int>();
      int minArea = int.MaxValue;
      int maxArea = int.MinValue;
      for (int i=0; i<zonesArea.Length; i++)
      {
        if (zonesArea[i]<minArea) { minArea = zonesArea[i]; }
        if (zonesArea[i]>maxArea) { maxArea = zonesArea[i]; }
      }
      foreach (Vector2Int point in inlandV2)
      {
        Vector2Int[] neighbours = point.Neighbours(size);
        Color nodeColor = shape_bmp.GetPixel(point.x,point.y);
        int index = System.Array.IndexOf(zonesRed,Mathf.RoundToInt(nodeColor.r*255f));
        bool freeNeighboursExist = false;
        float minRnd = 0.4f;
        foreach(Vector2Int neighbour in neighbours)
        {
          Color neighbourColor = shape_bmp.GetPixel(neighbour.x,neighbour.y);
          if (Mathf.RoundToInt(neighbourColor.a*255)==254) { minRnd /=6f; } 
        }
        int failCount = 0;
        foreach(Vector2Int neighbour in neighbours)
        {
          Color newColor = shape_bmp.GetPixel(neighbour.x,neighbour.y);
          if (Mathf.RoundToInt(newColor.a*255)>253)
          {
            freeNeighboursExist = true;
            if (zonesArea[index]<maxArea+25)
            {
              float rnd = Random.value;
              int gradIndex = System.Array.IndexOf(zonesRed,Mathf.RoundToInt(nodeColor.r*255f));
              if (rnd>1-minRnd-gradients[gradIndex][neighbour.x*size+neighbour.y]*0.5f)
              {
                newColor.a = 253f/255f;
                newColor.r = nodeColor.r;
                newColor.g = nodeColor.g;
                int insertIndex = Random.Range(0,auxList.Count-1);
                auxList.Insert(insertIndex,neighbour);
                shape_bmp.SetPixel(neighbour.x,neighbour.y,newColor);
                zonesArea[index]++;
                zonesV2[index].Add(neighbour);
              } else 
              {
                newColor.a = 254f/255f;
                failCount++;
                shape_bmp.SetPixel(neighbour.x,neighbour.y,newColor);
        } } } }
        if (failCount>4)
        {
          minRnd = 0.9f;
          foreach (Vector2Int neighbour in neighbours)
          {
            float rnd = Random.value;
            if (rnd > 1-minRnd)
            {
              Color neighbourColor = shape_bmp.GetPixel(neighbour.x,neighbour.y);
              if (Mathf.RoundToInt(neighbourColor.a*255)==254)
              {
                neighbourColor.a = 1f;
                minRnd-=0.15f;
                shape_bmp.SetPixel(neighbour.x,neighbour.y,neighbourColor);
        } } } }
        if (freeNeighboursExist)
        {
          int insertIndex = Random.Range(0,auxList.Count-1);
          auxList.Insert(insertIndex,point);
      } }
      inlandV2 = auxList;
      if (inlandV2.Count == 0) { done = true; }
      return done;
    }
    List<string> GatherCoastalData()
    {
      List<string> auxList = new List<string>();

      string dataRead = BGNodes.text;

      string[] dataLines = dataRead.Split('\n');
      foreach(string line in dataLines)
      {
        string[] lContent = line.Split(',');
        int enabled = int.Parse(lContent[5]);
        if (enabled == 1)
        {
          if (lContent[4] == "2")
          { //if it is a coastal node
            if (lContent[3] != "0")
            { //if MustHaveQtt != 0
              string newString = "MustHave"+lContent[0];
              int mhQuantity = int.Parse(lContent[3]);
              for (int i=0; i<mhQuantity; i++) { auxList.Add(newString); } 
              if (lContent[2]!="0")
              { //if it can only appear a limited ammount
                int totalQuantity = int.Parse(lContent[2]);
                if (totalQuantity>mhQuantity)
                {
                  int remainingQuantity = totalQuantity-mhQuantity;
                  for (int i_2=0; i_2<remainingQuantity; i_2++) { auxList.Add(lContent[0]); } 
                } 
              } else 
              { //if it can appear any number of times
                string newString_2 = "Unlimited"+lContent[0];
                auxList.Add(newString_2);
              } 
            } else 
            {  //if MustHaveQtt == 0
              if (lContent[2]!="0")
              { //if it can only appear a limited ammount
                int quantity = int.Parse(lContent[2]);
                for (int i_3=0; i_3<quantity; i_3++) { auxList.Add(lContent[0]); }
              } else 
              { //if it can appear any number of times
                string newString_3 = "Unlimited"+lContent[0];
                auxList.Add(newString_3);
      } } } } }
      return auxList;
    }
    List<string> GatherInlandData()
    {
      List<string> auxList = new List<string>();

      string dataRead = BGNodes.text;

      string[] dataLines = dataRead.Split('\n');
      foreach(string line in dataLines)
      {
        string[] lContent = line.Split(',');
        int enabled = int.Parse(lContent[5]);
        if (enabled == 1)
        { //if it is enabled
          if (lContent[4] != "2")
          { //if it isn't a coastal node
            if (lContent[4] == "0")
            { //if it is a strict inland node
              string newlContentZero = "Strict"+lContent[0];
              lContent[0] = newlContentZero;
            }
            if (lContent[3] != "0")
            { //if MustHaveQtt != 0
              string newString = "MustHave"+lContent[0];
              int mhQuantity = int.Parse(lContent[3]);
              for (int i=0; i<mhQuantity; i++) { auxList.Add(newString); } 
              if (lContent[2]!="0")
              { //if it can only appear a limited ammount
                int totalQuantity = int.Parse(lContent[2]);
                if (totalQuantity>mhQuantity)
                {
                  int remainingQuantity = totalQuantity-mhQuantity;
                  for (int i_2=0; i_2<remainingQuantity; i_2++) { auxList.Add(lContent[0]); } 
                } 
              } else 
              { //if it can appear any number of times
                string newString_2 = "Unlimited"+lContent[0];
                auxList.Add(newString_2);
              } 
            } else 
            {  //if MustHaveQtt == 0
              if (lContent[2]!="0")
              { //if it can only appear a limited ammount
                int quantity = int.Parse(lContent[2]);
                for (int i_3=0; i_3<quantity; i_3++) { auxList.Add(lContent[0]); }
              } else 
              { //if it can appear any number of times
                string newString_3 = "Unlimited"+lContent[0];
                auxList.Add(newString_3);
      } } } } }
      return auxList;
    }
    void GenerateOutline()
    {
      Texture2D outline_bmp = new Texture2D(size,size, TextureFormat.ARGB32, false);
      for(int i=0;i<size;i++) 
      { 
        for(int j=0;j<size;j++) 
        { 
          
          bool isOutline = false;
          Color v2color = shape_bmp.GetPixel(i,j);
          Vector2Int[] neighbours = new Vector2Int(i,j).Neighbours(size);
          foreach(Vector2Int neighbour in neighbours)
          {
            if (shape_bmp.GetPixel(neighbour.x,neighbour.y) != v2color)
            {
              isOutline = true;
              break;
            }
          }

          if (isOutline) 
          {
            Color shadedv2color = v2color;
            shadedv2color.r /= 1.5f;
            shadedv2color.g /= 1.5f;
            shadedv2color.b /= 1.5f;

            outline_bmp.SetPixel(i,j,shadedv2color);
          }
          else
          {
            outline_bmp.SetPixel(i,j,Color.clear); 
          }
        } 
      }
      Transform outlineTr = transform.Find("Outline");
      outlineTr.GetComponent<Renderer>().material.mainTexture = outline_bmp;
      outline_bmp.Apply();
      outlineTr.gameObject.SetActive(true);
    }
    void GenerationReset()
    {
      generation_phase = 2;
      nodesChosen = false;
      auxValue = 0;
      inlandNodeNames = new List<string>();
      inlandV2 = new List<Vector2Int>();
      zonesRed = null;
      zonesArea = null;
      zonesV2 = new List<List<Vector2Int>>();
      perlinPoint = null;
      gradients = null;
      perlinDone = true;
      coastalNodeNames = new List<string>();
      coastalBlue = new List<int>();
      coastalV2 = new List<Vector2Int>();
      startingCoastalWalkPoint = new Vector2Int(0,0);
      coastSize = 0;
      fullZonesColors = new List<Color>();
      fullZonesNames = new List<string>();
      roundNumber++;
    }
    void OrganizeFullZones()
    {
      for (int i=0; i<size; i++)
      {
        for (int j=0; j<size; j++)
        {
          Color nodeColor = shape_bmp.GetPixel(i,j);
          if (nodeColor.a!=0)
          {
            Vector2Int v2 = new Vector2Int();
            v2.x = Mathf.RoundToInt(nodeColor.r*255f);
            v2.y = Mathf.RoundToInt(nodeColor.b*255f);
            nodeColor.a = 1f;
            if (!fullZonesColors.Contains(nodeColor))
            {
              fullZonesColors.Add(nodeColor);
              string name = inlandNodeNames[System.Array.IndexOf(zonesRed,v2.x)]+v2.x.ToString("D3");
              if (v2.y!=0) { name+="-"+coastalNodeNames[coastalBlue.IndexOf(v2.y)]+v2.y.ToString("D3"); }
              fullZonesNames.Add(name);

              GenerateUI(name,nodeColor);
            }
    } } } }
    IEnumerator OrganizeFullZones(bool asd)
    {
      System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
      stopwatch.Start();
      long lastYield = 0;
      
      for (int i=0; i<size; i++)
      {
        for (int j=0; j<size; j++)
        {
          Color nodeColor = shape_bmp.GetPixel(i,j);
          if (nodeColor.a!=0)
          {

            if (stopwatch.ElapsedMilliseconds-lastYield>30)
            {
              lastYield = stopwatch.ElapsedMilliseconds;
              yield return null;
            }
            
            Vector2Int v2 = new Vector2Int();
            v2.x = Mathf.RoundToInt(nodeColor.r*255f);
            v2.y = Mathf.RoundToInt(nodeColor.b*255f);
            nodeColor.a = 1f;
            if (!fullZonesColors.Contains(nodeColor))
            {
              fullZonesColors.Add(nodeColor);
              string name = inlandNodeNames[System.Array.IndexOf(zonesRed,v2.x)]+v2.x.ToString("D3");
              if (v2.y!=0) { name+="-"+coastalNodeNames[coastalBlue.IndexOf(v2.y)]+v2.y.ToString("D3"); }
              fullZonesNames.Add(name);

              GenerateUI(name,nodeColor);
            }
          }
        }
      }

      organized = true; 

      stopwatch.Stop();
    }
    void PlaceCoastalNodes()
    {
      int[] blueValue = new int[coastalNodeNames.Count];      
      int minusValue = Mathf.FloorToInt(0.4f*255f/(coastalNodeNames.Count+1f));
      for (int i=0; i<coastalNodeNames.Count; i++) { blueValue[i]=255-minusValue*i; }
      blueValue = blueValue.Shuffle();
      int turn = 1;
      int totalTurns = coastalNodeNames.Count;
      int walkSize = Mathf.RoundToInt(Random.Range(0.95f,0.99f)*(float)coastSize/(float)totalTurns);
      int walkLeft = coastSize - walkSize;
      Vector2Int point = startingCoastalWalkPoint;
      int bluePointer = 0;
      float greenValue = 0f;
      float redValue = 0f;
      Vector2Int lastMove = new Vector2Int(0,0);
      for (int k=0; k<coastSize; k++)
      {
        int i = point.x;
        int j = point.y;
        int i_p = Mathf.Min(point.x+1,size-1);
        int i_m = Mathf.Max(0,point.x-1);
        int j_p = Mathf.Min(point.y+1,size-1);
        int j_m = Mathf.Max(0,point.y-1);
        int initialMove = 0;
        Vector2Int[] moves = new Vector2Int[7]
        {
          new Vector2Int(i_m,j),  // 0; - 0
          new Vector2Int(i,j_m),  // 1; 0 +
          new Vector2Int(i_p,j),  // 2; + 0
          new Vector2Int(i,j_p),  // 3; 0 -
          new Vector2Int(i_m,j),  // 4; - 0
          new Vector2Int(i,j_m),  // 5; 0 +
          new Vector2Int(i_p,j)   // 6; + 0
        };
        Vector2Int[] neighbours = new Vector2Int[4];
        bool found = false;
        if (lastMove == new Vector2Int(0,1)) { initialMove = 2; } 
        else
        {
          if (lastMove == new Vector2Int(-1,0)) { initialMove = 3; } 
          else { if (lastMove == new Vector2Int(1,0)) { initialMove = 1; } }
        }
        neighbours[0] = moves[initialMove];
        neighbours[1] = moves[initialMove+1];
        neighbours[2] = moves[initialMove+2];
        neighbours[3] = moves[initialMove+3];   
        foreach(Vector2Int neighbour in neighbours)
        { // walk
          if (Mathf.RoundToInt(shape_bmp.GetPixel(neighbour.x,neighbour.y).a*255) == 253)
          {
            lastMove = new Vector2Int(neighbour.x-point.x,neighbour.y-point.y);
            point.x = neighbour.x;
            point.y = neighbour.y;
            found = true;
            break;
          }
        }
        if (!found)
        { //try the diagonal neighbours
          neighbours[0] = new Vector2Int(i_p,j_p);  //+1,+1
          neighbours[1] = new Vector2Int(i_p,j_m);  //+1,-1
          neighbours[2] = new Vector2Int(i_m,j_p);  //-1,+1
          neighbours[3] = new Vector2Int(i_m,j_m);  //-1,-1
          foreach(Vector2Int neighbour in neighbours)
          {
            if (Mathf.RoundToInt(shape_bmp.GetPixel(neighbour.x,neighbour.y).a*255) == 253)
            {
              lastMove = new Vector2Int(neighbour.x-point.x,neighbour.y-point.y);
              point.x = neighbour.x;
              point.y = neighbour.y;
              found = true;
              break;
        } } }
        Color newColor = shape_bmp.GetPixel(point.x,point.y); 
        newColor.b = (float)blueValue[bluePointer]/255f;
        newColor.g = greenValue;
        newColor.r = redValue;
        int growth = 14; // make it random later if necessary
        newColor.a = (252f-(float)growth)/255f;
        shape_bmp.SetPixel(point.x,point.y,newColor);
        walkSize--;
        if (walkSize==0)
        {
          turn++;
          if (turn==totalTurns) { walkSize = walkLeft; }
          else 
          {
            walkSize = Mathf.RoundToInt(Random.Range(0.95f,0.99f)*(float)coastSize/(float)totalTurns);
            walkLeft -= walkSize;
          }
          redValue=Random.value;
          greenValue=Random.value;
          coastalBlue.Add(blueValue[bluePointer]);
          bluePointer++;
    } } }
    void PlaceInlandNodes()
    {
      int minusValue = Mathf.FloorToInt(255f/(inlandNodeNames.Count+1f));
      float iterN = 0;
      if (zonesArea == null) { zonesArea = new int[inlandNodeNames.Count]; }
      if (zonesRed == null) { zonesRed = new int[inlandNodeNames.Count]; }
      int redValue = 255;
      int greenValue = Random.Range(50,255);
      for (int i=0; i<inlandNodeNames.Count; i++)
      {
        float minDistance = (float)(size/2f);
        bool found = false;
        Vector2Int point = new Vector2Int(0,0);
        while (!found)
        {
          iterN++;
          point = new Vector2Int(Random.Range(0,size-1),Random.Range(0,size-1));
          Color nodeColor = shape_bmp.GetPixel(point.x,point.y);
          if (nodeColor.a == 1f && nodeColor.b == 0f)
          {
            float cDistance = point.GetClosestDistance(inlandV2);
            if (cDistance>minDistance) { found = true; iterN = 0; }
          }
          if (iterN>500) { iterN = 0; minDistance--; }
        }
        inlandV2.Add(point);
        zonesArea[i]++;
        zonesRed[i]+=redValue;
        zonesV2.Add(new List<Vector2Int>());
        zonesV2[i].Add(point);
        Color newColor = new Color((float)redValue/255f,(float)greenValue/255f,0f,253f/255f);
        shape_bmp.SetPixel(point.x,point.y,newColor);
        redValue-=minusValue;
        greenValue = Random.Range(50,255);
    } }
    void SetCoastToStartPlacing_part1()
    { // This perimeter includes all internal holes
      for (int i=0; i<size; i++)
      {
        for (int j=0; j<size; j++)
        {
          if (shape_bmp.GetPixel(i,j).a==1f)
          {
            int neighboursCount = 0;
            foreach (Vector2Int neighbour in new Vector2Int(i,j).Neighbours(size)) { neighboursCount+=Mathf.RoundToInt(shape_bmp.GetPixel(neighbour.x,neighbour.y).a); }
            if (neighboursCount<8)
            {
              Color newColor = shape_bmp.GetPixel(i,j);
              newColor.a = 254f/255f;
              shape_bmp.SetPixel(i,j,newColor);
              coastalV2.Add(new Vector2Int(i,j));
              if (startingCoastalWalkPoint == new Vector2Int(0,0)) { startingCoastalWalkPoint = new Vector2Int(i,j); }
    } } } } }
    void SetCoastToStartPlacing_part2()
    { // walk until we can't anymore and measure coastSize
      int perimeterSize = 0;
      bool done = false;
      Vector2Int point = startingCoastalWalkPoint;
      Vector2Int lastMove = new Vector2Int(0,0);
      while(!done)
      {
        int i = point.x;
        int j = point.y;
        int i_p = Mathf.Min(point.x+1,size-1);
        int i_m = Mathf.Max(0,point.x-1);
        int j_p = Mathf.Min(point.y+1,size-1);
        int j_m = Mathf.Max(0,point.y-1);
        int initialMove = 0;
        Vector2Int[] moves = new Vector2Int[7]
        {
          new Vector2Int(i_m,j),  // 0; - 0
          new Vector2Int(i,j_m),  // 1; 0 +
          new Vector2Int(i_p,j),  // 2; + 0
          new Vector2Int(i,j_p),  // 3; 0 -
          new Vector2Int(i_m,j),  // 4; - 0
          new Vector2Int(i,j_m),  // 5; 0 +
          new Vector2Int(i_p,j)   // 6; + 0
        };
        Vector2Int[] neighbours = new Vector2Int[4];
        bool found = false;
        if (lastMove == new Vector2Int(0,1)) { initialMove = 2; } 
        else 
        {  
          if (lastMove == new Vector2Int(-1,0)) { initialMove = 3; } 
          else { if (lastMove == new Vector2Int(1,0)) { initialMove = 1; } }
        }     
        neighbours[0] = moves[initialMove];
        neighbours[1] = moves[initialMove+1];
        neighbours[2] = moves[initialMove+2];
        neighbours[3] = moves[initialMove+3];   
        foreach(Vector2Int neighbour in neighbours)
        { // walk
          if (Mathf.RoundToInt(shape_bmp.GetPixel(neighbour.x,neighbour.y).a*255) == 254)
          {
            lastMove = new Vector2Int(neighbour.x-point.x,neighbour.y-point.y);
            point.x = neighbour.x;
            point.y = neighbour.y;
            found = true;
            break;
        } }
        if (!found)
        { //try the diagonal neighbours
          neighbours[0] = new Vector2Int(i_p,j_p);  //+1,+1
          neighbours[1] = new Vector2Int(i_p,j_m);  //+1,-1
          neighbours[2] = new Vector2Int(i_m,j_p);  //-1,+1
          neighbours[3] = new Vector2Int(i_m,j_m);  //-1,-1
          foreach(Vector2Int neighbour in neighbours)
          {
            if (Mathf.RoundToInt(shape_bmp.GetPixel(neighbour.x,neighbour.y).a*255) == 254)
            {
              lastMove = new Vector2Int(neighbour.x-point.x,neighbour.y-point.y);
              point.x = neighbour.x;
              point.y = neighbour.y;
              found = true;
              break;
        } } }
        if (!found) { done = true; } 
        else 
        {
          Color newColor = shape_bmp.GetPixel(point.x,point.y); 
          newColor.a = 253f/255f;
          shape_bmp.SetPixel(point.x,point.y,newColor); // set point
          perimeterSize++;
      } }
      coastSize = perimeterSize;
    }
    void SetStartingCoastalWalkPoint()
    {
      for (int k=startingCoastalWalkPoint.y; k>=0; k--)
      {
        if (Mathf.RoundToInt(shape_bmp.GetPixel(startingCoastalWalkPoint.x,k).a*255)==254)
        {
          startingCoastalWalkPoint.y = k;
    } } }
    void CallDebug(string text)
    {
      UIMan.SetPDebugText(updateCount.ToString("D5")+" BG"+generation_phase.ToString("D3")+"."+auxValue.ToString("D4")+"."+roundNumber.ToString("D2")+", "+text);
    }
    void CallDebug(string text, bool primary)
    {
      if (primary) { UIMan.SetPDebugText(updateCount.ToString("D5")+" BG"+generation_phase.ToString("D3")+"."+auxValue.ToString("D4")+"."+roundNumber.ToString("D2")+", "+text); } 
      else { UIMan.SetSDebugText(updateCount.ToString("D5")+" BG"+generation_phase.ToString("D3")+"."+auxValue.ToString("D4")+"."+roundNumber.ToString("D2")+", "+text); }
    }
    void SetupRenderer()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend!=null) { rend.material.mainTexture = shape_bmp; } 
        else { Debug.LogError("NO Renderer IN THIS GAME OBJECT"); }
    }
    void Showcase() { shape_bmp.Apply(); }
    IEnumerator finalCountdown()
    {
      UIMan.SetSDebugText(updateCount.ToString("D5")+" BG"+" is going to be destroyed in 5");
      yield return new WaitForSeconds(1);
      UIMan.SetSDebugText(updateCount.ToString("D5")+" BG"+" is going to be destroyed in 4");
      yield return new WaitForSeconds(1);
      UIMan.SetSDebugText(updateCount.ToString("D5")+" BG"+" is going to be destroyed in 3");
      yield return new WaitForSeconds(1);
      UIMan.SetSDebugText(updateCount.ToString("D5")+" BG"+" is going to be destroyed in 2");
      yield return new WaitForSeconds(1);
      UIMan.SetSDebugText(updateCount.ToString("D5")+" BG"+" is going to be destroyed in 1");
      yield return new WaitForSeconds(1);
      UIMan.SetPDebugText("Minimap");
      UIMan.AlingPDTCenter();
      UIMan.SetSDebugText("");
      BiomeGenerator script = GetComponent<BiomeGenerator>();
      Destroy(script);
    }
    void GenerateUI(string location, Color color)
    {
        Texture2D biome_bmp = new Texture2D(size,size,TextureFormat.ARGB32, false);
        Color shadedColor = color;
        shadedColor.r /= 1.5f;
        shadedColor.g /= 1.5f;
        shadedColor.b /= 1.5f;
        for(int i=0;i<size;i++) 
        { 
          for(int j=0;j<size;j++) 
          {
            if (shape_bmp.GetPixel(i,j).r == color.r && shape_bmp.GetPixel(i,j).b == color.b)
            {
              biome_bmp.SetPixel(i,j,shadedColor);
            }
            else 
            {
              biome_bmp.SetPixel(i,j,Color.clear);
            }
          }
        }
        biome_bmp.Apply();
        Transform outline = transform.Find("Outline");
        GameObject biome = Instantiate(outline.gameObject);
        biome.transform.localScale *=9f;
        biome.GetComponent<Renderer>().material.mainTexture = biome_bmp;
      
        biome.transform.name = location;
        biome_bmp.Apply();
        biome.SetActive(false);
        biome.GetComponent<MeshCollider>().enabled = false;
        biome.transform.parent = this.transform;
        biome.transform.localPosition = new Vector3(0,0,-1);
        
        UIMan.AddLocationToMinimap(location, color, biome);
    }
}