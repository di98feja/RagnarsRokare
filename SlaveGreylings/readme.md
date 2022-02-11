slavegreylings.readme

###############################

The Eikthyr forest are whispering about Greylings disappearing nearby Barg & Morg basecamp.

From the book of Barg:
Today the day of 38 in this paradise of a world of Odin we call Valheim, me and Morg came across a greyling 
that seemed to be interested in one of the silver necklaces we just dug up from an old grave.  
We tossed the necklace on the ground and it slowly approached us. After yet another necklace was tossed to 
the greyling it followed us back to the camp. Morg managed to train it to refill the kiln without burning 
itself to bad and fed it with resin. Tomorrow I will try to show it how to work the smelter.
Soon the enslavement can be perfected.

###############################

This mod enables the taming (enslavement) of certain creatures, allowing the player to drag them home to the village and set them to perform chores around the settlement.

Made and tested with Valheim v0.150.3

Installation:
Copy the contents of the RagnarsRokare_SlaveGreyling.zip file to your BepInEx plugin folder under Valheim

Usage:
In order to tame a creature you need to "feed" (bribe) it with the appropriate item until it's tame. Bribe and times are configurable.
When tamed the creature will need to be fed to keep working.

Tapping E will toggle between Follow and Performing tasks.
Holding E will enable name change.
Press CallHome key (Home by default) to call all slaves within earshot.

Some creatures will have to be shown and taught before beeing able to perform tasks, others have a more natural instinct.

When in performing task mode the creeature will search nearby for fitting tasks.
At the task it will identify what is missing and search ground and nearby chests for needed items.  After a certain time or when task is finished it will continue to next task.
Many distanses and timers are configurable for better adjustments, some of them require restart for effect to take place.

Tips:
The Greyling is not very smart (this is intended) and easily gets confused. It might get stuck in tight corners or behind walls. Avoid tight multi level designs for where you deploy enslaved greylings.
The Brute is tall! Don't expect it to pass through normal doors.

Please note that this is a work in progress, we will add new features/creatures and take disciplinary action against unruly or misbehaving Greylings.

The code can be found at https://github.com/di98feja/RagnarsRokare

In order to keep build the creatures behaviour, we have been using Stateless state machine (found at https://github.com/dotnet-state-machine/stateless). Thank you stateless team!

We hope you find this little mod fun!  
// Barg and Morg  

0.8.5 Patch Notes:  
Added support for Basic Farming Behaviour  

0.8.4 Patch Notes:
Rebuilt with Valheim v0.206.5

0.8.3 Patch Notes:
Rebuilt with new (Hearth and Home) dll's.

0.8.2 Patch Notes:  
Bugfix: Minimap and CharacterDamaged

0.8.1 Patch Notes:  
Bugfix: Missing method exception fixed

0.8.0 Patch Notes:  
* This update requires MobAILib version 0.3.*  

From the book of Barg:  
##################################  
Morg came by this afternoon and thanked me for harvesting and replating his garden.  
I was much confused because I had done no such thing. The mystery was solved later when we  
watched one of the Greydwarves pick and plant crops like it was no extraordinary thing at all.
It did look pretty pleased with itself though, these critters is starting to scare me.

From the book of Barg:  
##################################   
Today 100 days has passed since Odin put us on this faschinating world.
There has been hardship and strife but also much wonder.
The last couple of weeks we have been busy trying to figure out how to tame the Greydwarf.
After watching them nearly be stung to death when trying to get at a bee nest it became clear to us.
Loaded with honey we managed to tame not one but three Greydwarfs.
The creature really has a sweet tooth, after it got into our food chest it will not eat anything but Morgs best jam,
good thing that Morg managed to teach it to pick berries.
Speaking of teaching, the greydwarf is a remarkably intelligent and orderly creature.
They remember the content of chests and will pickup things from the ground and put them in matching chests.
Morg even managed to upset it by making a mess in one of the chests so now it will take whatever is in that chest and 
sort in others.
But my greatest supprise was when it watched me noting the content of a chest on a sign.
From that day on it would only put the things noted on the sign inside the chest, it had learned to read!
This world never cease to amaze me, now I just got to teach Morg to read aswell.  
##################################  



0.7.2 Patch Notes:  
From the book of Barg:  
##################################  
Today the day 67 in this paradise world of Odin we call Valheim, we managed to teach the slaves to not only look at their feet's when searching for assignments and chests.  
This makes them wandering longer distances in search of assignments making it easy for them to get lost. The solution for this was to teach them to return to the "home" position where we last set them to stay (not follow).   
##################################  
* This update is adjusted for the use fight behavior in MobAILib 0.2 or later (found at https://www.nexusmods.com/valheim/mods/1188?tab=description).
* Making use of Feedingbehaviour in MobAILib.
* Making use of new Attributes in BaseIA rather then configuring everything separately.
* Added random movement in idle and a return point to last stay position to return to after stuck without tasks.




0.7.1 Patch Notes:
From the book of Barg:  
##################################  
Today the day 65 in this paradise world of Odin we call Valheim, we found a way of training the slaves to defend themselves.  
After teaching the brutes to attack back if they are attacked, the greylings soon copied their behavior and now also tries to attack.   
They still fall back on fleeing as soon as hurt, so I am afraid they wont help out much in the way of defense.   
It seems like this defensive behavior have enraged Eikthyr so that Eikthyr's beasts now attacks slaves. 
##################################  
* This update is adjusted for the use fight behavior in MobAILib 0.1.3 or later (found at https://www.nexusmods.com/valheim/mods/1188?tab=description).

0.7 Patch Notes:  
* MobAI separated out to MobAILib that can be used separatley in any mod (found at https://www.nexusmods.com/valheim/mods/1188?tab=description).
* This makes the SlaveGreylings dependent on MobAILib from rev 0.7 and forward.
* In addition some small bugs are fixed to make the game run smother with less debug comments.

0.6.2 patch note:  
* Barg released a fix on the empty task list on load bug. 

0.6.1 patch notes:  
* Error on wakeup and by building/removing structures fixed.  
* Error when moving to removed item/container fixed.


0.6 patchnotes:  
From the book of Barg:  
##################################  
Today the day 63 in this paradise world of Odin we call Valheim, Me and Morg decided to stop the education of all tamed Greylings.  
Any Viking who want the greyling to perform a task will have to show the greyling the task itself.  
As you will notice they wont learn any task, but you can at least set them on separate tasks. 

##################################  
From The book of Barg:
Today the day 65 in this paradise world of Odin we call Valheim, Hugin must have whispered in his masters ear about my earlier thoughts.  
I was foraging in the dark forest when I came upon a tower ruin that was in pretty good condition. But what was inside was the greater supprise.  
An enourmous greydwarf was trapped inside the tower, to large to fit its arched doorway. It must have been in there since it was a mere sapling.  
This was the perfect opportunity to find a way to make the creature docile enough to use as labor around the settlement.  
Many hours and many attempts later Morg brought what would be they key to success, Dandelions.  
This pretty little weed seemed to bring peace to the mind of the giant creature and soon enough it had settled down. I named it Brutus and we had to tear down a wall in the tower to get it out.  
Almost giddy with excitement we soon discovered what I had hoped to be true, it could use its rock-fist as a hammer and showed great skill when it came to repairing worn down structures.  
It is truly a remarkable world that Odin has put us upon!  
##################################  
* Added the need to educate Greylings.  Have them in follow and interact with assignment.  After several interactions with Greyling nearby It will learn how to operate.  
* Added Greydwarf Brutes as tamable creatures. Start picking dandelions..


0.5 patchnotes:  
##################################  
From the book of Barg:  
Today the day 62 in this paradise world of Odin we call Valheim, I managed to teach the Greylings to answer to a whistle call.  
We soon realized that the greylings are spreading things we teach them between each other, because Brynäs and Slagpåse both came wondering when we were training.  
Morg will move out into the forest and try the call tomorrow. Maybe Berith and Jackass are still out there, If we did not kill them. All greylings and names are somewhat mixed in my memory.  
##################################  
* Rewritten the AI logic using a finite state machine  
* Refactored most of the app and split it into several files  
* Generalized the implementation to enable other types of creatues  
* Added CallHome command that will sett Follow state to all enslaved mobs in range. Default key is Home (pun intended)  
* Greylings now cause chest animations and don't interact with already chests open  

0.4 patchnotes:  
* Bug (Unity Log error spam) is fixed (we hope).  
* Hearth added in assign list.  
* Stop movement to avoid strange overshooting at target.  
* Minor interact distance tweaks  

0.3 patchnotes:  
##################################  
From the book of Barg:  
Today the day 45 in this paradise world of Odin we call Valheim. Today I realized that the Greylings are far more adaptable then expected.   
Already after a short period among us they tend to look in chests and houses for food rather then wondering of into the forest. By encouraging this behavior the need for fencing might be limited in the future.  
Another discovery of this adaptation was when Morg, enraged of Sig's overenthusiastic farting, throw his axe on Stig. To our surprise Stig looked a bit afraid from being hurt, but showed almost no sign of damage.  
We thought the rich amount of resin in their food made them harder, but pitching Stig against a skeleton killed him as easy as any other greyling. This must be an adaptation to the environment we did not count on.  
More experiments might help us understand this behavior better, but now we will hunt some Boar for this evening barbeque.  
##################################  
* Decreased dmg from player implemented on slaves.  
* Slaves will look in chests for food rather then wonder of in the forrest if there is no food on the ground.  
* Minor distance changes  

0.2 patchnotes:  
###############################  
From the book of Barg:  
Today the day 42 in this paradise world of Odin we call Valheim, me and Morg gave the two greylings the names Berith and Dumbass.   
We think one is female but cannot really tell them apart, except from a sense of identity we can feel when moving up close to them.   
The naming caused some misunderstanding when me and Morg was trying to use different names, but an improvement in our sense of identity sorted this problem just fine.  
Another improvement in the Greyling training is that we have trained them to not hast into a task without thinking it through.   
We noted that this improved the quality of their work, even if it results in a slower progress.  
###############################  
* Renaming is improved so the risk of players seeing different names is limited.  
* Task timeout is limiting the minimum time on a state (such as search, load item etc.) to 1s. This is creating a smoother more natural behavior of the slaves.  
* Minor adjustments to interact distance to make moving up to assignments/objects look better.  
