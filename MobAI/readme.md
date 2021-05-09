﻿## RagnarsRökare MobAILib ##
MobAILib is a library that mods can use to replace the built in AI of Characters inside Valheim.

### Usage ###
Start by adding a reference to MobAILib inside your mod-project.

The RagnarsRokare.MobAI.MobManager is used to register and keep track of controlled mobs.
It has a list of available MobAIs to use as the "brain" of the mob and controls its actions.  
Each MobAI has its own config file with settings: WorkerAIConfig and FixerAIConfig.

- **WorkerAI**  
Makes the character walk around and refill Smelters, Kilns, Fireplaces and Torches. It will search for items on the ground and in chests.
Does not fight, only flee when getting damaged. The FeedDuration setting only applies if there is a Tameable component attached to the gameObject.  
**WorkerAIConfig**  
    public int FeedDuration = 1000;  
    public int AssignmentSearchRadius = 30;  
    public int ItemSearchRadius = 10;  
    public int ContainerSearchRadius = 10;  
    public string[] IncludedContainers = new string[] { "piece_chest_wood" };  
    public int MaxContainersInMemory = 3;  
    public int TimeBeforeAssignmentCanBeRepeated = 120;  
    public int TimeLimitOnAssignment = 60;  

- **FixerAI**  
Makes the character go and repair any damaged structures it finds. Does not fight, only flee when taking damage.  
The FeedDuration setting only applies if there is a Tameable component attached to the gameObject.  
**FixerAIConfig**  
These are the default values  
    public int PostTameFeedDuration = 1000;  
    public int AssignmentSearchRadius = 10;  
    public int ItemSearchRadius = 10;  
    public int ContainerSearchRadius = 10;  
    public int MaxContainersInMemory = 5;  
    public int TimeLimitOnAssignment = 30;  
    public string[] IncludedContainers = new string[] { "piece_chest_wood" };  

**``RegisterMob(Character character, string uniqueId, string mobAIName, string configAsJson)``**  
Used to register a mob to use a MobAI.
- character is the Character component of the mob
- uniqueId is a string that is used to uniquely identify the mob among all other mobs
- mobAIName is the type of MobAI to be used. Available types can be found via **``MobManager.GetRegisteredMobAIs()``**
- configAsJson is the MobAI specific config serialized as JSON

**``UnregisterMob(string uniqueId)``**  
Used to stop controlling a mob.

**``IsRegisteredMob(string uniqueId)``**  
Check if there is a registered mob by the given uniqueId.

**``IsAliveMob(string uniqueId)``**  
Check if there is an active mob by the given uniqueId.
A mob is Alive when its MobAI has been instanciated and assigned.

**``Dictionary<string, MobAIBase> AliveMobs``**  
All Alive mobs is kept in a dictionary with their UniqueId as key.
Value is the baseclass of its MobAI that give access to its CurrentAIState


The code can be found at [GitHub](https://github.com/di98feja/RagnarsRokare)  
In that repository is also the SlaveGreylings mod which uses this library.

This is a work in progress and as all hobby projects there is limit to how much time we have to spend on this project, no matter how fun we think it is.
So even if we love feedback we will not be able to suit everybodys wishes.  

### Future plans ###
- Ability to register custom MobAI-classes
- Add a general Fighting-behaviour that can be used by all MobAI-classes
- More built-in MobAI classes

### Changelog ###
- Version 0.1.0  
First release
