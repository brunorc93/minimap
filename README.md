# Island generator — Minimap preview

Terrain Procedural Generation — Minimap preview

Created in Unity3D using C#, unity's Texture2D and 3D Quads.

---------------------------------------------------------------------------
This is one module of a series used on Unity3D to generate island meshes. Other modules adapted for C#.net can be seen in the following links:
* [Island Shape](https://github.com/brunorc93/islandShapeGen.net)  
* [Biome Growth](https://github.com/brunorc93/BiomeGrowth.net)  
* [Noise](https://github.com/brunorc93/noise)  
* [HQ2nxNoAA — previous](https://github.com/brunorc93/HQnx-noAA.net)  

The following modules use Unity  
* [empty]()

> (more links will be added as soon as the modules are ported onto C#.net or made presentable in Unity).  

The full Unity Project can be followed [here](https://github.com/brunorc93/procgen)  

---------------------------------------------------------------------------

This module runs some of the previous modules to generate and island shape followed by separating it into biomes and giving those biomes generic names. It then creates a visualization of this generated location with buttons to either **SAVE** when finished the generated texture or **RESET** the generation back to its beginning, generating a new drawing.

This visualization shows the user the **HOVER**ed region's name below the map, alongside an identifier. Coastal regions are written as (main_region)(main_ID)-(coastal_subregion)(coastal_ID)

A time counter is shown on the bottom left of the screen.

Images of the screen for different parts of the generation process can be seen below:
