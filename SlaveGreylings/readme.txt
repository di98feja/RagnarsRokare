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

This mod is enabeling the taming (enslavement) of greylings, allowing the player to drag them home to the village and set them to perform simple tasks such as refilling kiln, fireplaces and smelters.

Made and tested with Valheim v0.150.3

Installation:
Copy the contents of the RagnarsRokare_SlaveGreyling.zip file to your BepInEx plugin folder under Valheim

Usage:
In order to tame a Greyling you need to "feed" (bribe) it with silver necklaces until it's tame (typically two is needed). Bribe and times are configurable.
When tamed the greyling will need Resin to keep working (also this feedtime is configurable).

Tapping E will toggle between Follow and Performing tasks.
Holding E will enable name change.
Press CallHome key (Home by default) to call all slaves within earshot

When in performing task mode the Greyling will search nearby radius for enabled tasks. It's possible to dissable tasks in the config.
At the task it will identify what is missing and search ground and nearby chests for needed items.  After a certain time or when task is finished it will continue to next task.
Many distanses and timers are configurable for better adjustments, some of them require restart for effect to take place.

Tips:
The Greyling is not very smart (this is intended) and easily gets confused. It might get stuck in tight corners or behind walls. Avoid tight multi level designs for where you deploy enslaved greylings.

Please note that this is a work in progress, we will add new features/creatures and take disciplinary action against unruly or misbehaving Greylings.

The code can be found at https://github.com/di98feja/RagnarsRokare

In order to keep track on the Greyling Behaviour, we have been using Stateless state machine (found at https://github.com/dotnet-state-machine/stateless). Thank you stateless team!

We hope you find this little mod fun!
// Barg and Morg

0.6 patchnotes:
##################################
From the book of Barg:
Today the day 62 in this paradise world of Odin we call Valheim, Me and Morg decided to stop the education of all tamed Greylings. 
Any Wiking who want the greyling to perform a task will have to show the greyling the task itself. As you will notice they wont learn any task, but you can at least set them on separate tasks. 
##################################
Added the need to educate Greylings.  Have them in follow and interact with assignment.  After several interactions with Greyling nearby It will learn how to operate.

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
*Bug (Unity Log error spam) is fixed (we hope).
*Hearth added in assign list.
*Stop movement to avoid strange overshooting at target.
*Minor interact distance tweaks

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
