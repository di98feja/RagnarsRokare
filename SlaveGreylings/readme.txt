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

Made and tested with Valheim v0.148.7

Installation:
Copy the RagnarsRokare_SlaveGreyling.dll file to your BepInEx plugin folder under Valheim

Usage:
In order to tame a Greyling you need to "feed" (bribe) it with silver necklaces until it's tame (typically two is needed). Bribe and times are configurable.
When tamed the greyling will need Resin to keep working (also this feedtime is configurable).

Tapping E will toggle between Follow and Performing tasks.
Holding E will enable name change.

When in performing task mode the Greyling will search nearby radius for enabled tasks. It's possible to dissable tasks in the config.
At the task it will identify what is missing and search ground and nearby chests for needed items.  After a certain time or when task is finished it will continue to next task.
Many distanses and timers are configurable for better adjustments, some of them require restart for effect to take place.

Tips:
The Greyling is not very smart (this is intended) and easily gets confused. It might get stuck in tight corners or behind walls. Avoid tight multi level designs for where you deploy enslaved greylings.

Please note that this is a work in progress, we will add new features/creatures and take disciplinary action against unruly or misbehaving Greylings.

The code can be found at https://github.com/di98feja/RagnarsRokare

We hope you find this little mod useful!
// Barg and Morg



0.2 patchnotes:
###############################
From the book of Barg:
Today the day of 42 in this paradise of a world of Odin we call Valheim, me and Morg gave the two greylings the names Berith and Dumbass. We think one is female but cannot really tell them apart, 
except from a sense of identity we can feel when moving up close to them. The naming caused some misunderstanding when me and Morg was trying to use different names, but an improvement in our sense of identity 
sorted this problem just fine.
Another improvement in the Greyling training is that we have trained them to not hast into a task without thinking it through. We noted that this improved the quality of their work, even if it results in a slower progress.
###############################
* Renaming is improved so the risk of players seeing different names is limited.
* Task timeout is limiting the minimum time on a state (such as search, load item etc.) to 1s. This is creating a smoother more natural behavior of the slaves.
* Minor adjustments to interact distance to make moving up to assignments/objects look better.