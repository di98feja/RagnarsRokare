## RagnarsRökare MobAILib ##
MobAILib is a library that mods can use to replace the built in AI of Characters inside Valheim.

### Usage ###
Start by adding a reference to MobAILib inside your mod-project.

The RagnarsRokare.MobAI.MobManager is used to register and keep track of controlled mobs.
It has a list of available MobAIs to use as the "brain" of the mob and controls its actions.
- WorkerAI
Makes the character walk around and refill Smelters, Kilns, Fireplaces and Torches. It will search for items on the ground and in chests.
- FixerAI
Makes the character go and repair any damaged structures it finds.

Each MobAI has its own config file with settings: WorkerAIConfig and FixerAIConfig.

``RegisterMob(Character character, string uniqueId, string mobAIName, string configAsJson)``  
To register a mob use MobManager.RegisterMob method.
- character is the Character component of the mob
- uniqueId is a string that is used to uniquely identify the mob among all other mobs
- mobAIName is the type of MobAI to be used. Available types can be found via MobManager.GetRegisteredMobAIs()
- configAsJson is the MobAI specific config serialized as JSON

``UnregisterMob(string uniqueId)``  
Used to stop controlling a mob.

``IsRegisteredMob(string uniqueId)``  
Check if there is a registered mob by the given uniqueId.

``IsAliveMob(string uniqueId)``  
Check if there is an active mob by the given uniqueId.
A mob is Alive when its MobAI has been instanciated and assigned.

``Dictionary<string, MobAIBase> AliveMobs``  
All Alive mobs is kept in a dictionary with their UniqueId as key.
Value is the baseclass of its MobAI that give access to its CurrentAIState


