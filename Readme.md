# Vintage Engineering
A Tech mod for Vintage Story

## Support?
If you want to support me on this or any of my projects, please consider being a patron.

This project represents hundreds of hours of work, please consider supporting the project here https://www.patreon.com/flexiblegames

## Description
A mod for Vintage Story inspired by Immersive Engineering. A massive project that combines all the lessons learned making all my other mods. Combining Immersive Engineering and Petroleum, with a dash of Mekanismm, will massively expand the game past the steel process that marks the vanilla end-game.

## Future Goals
The mod will expand late-game play as steel is the primary material used for its machines. Players will be expected to produce steel via the vanilla mechanics several times before they have enough for the alloy process using coke and iron.

- Crushing, smelting, grinding, refining, distilling, etc via multiblock machines.
- Fluid movement, pumping, tanks.
- Electrical system, power generation using Coal and oil based combustion, then expanding into more modern power options (nuclear?).
- Pipez-style system for moving goods and fluids around. At least I hope this won't crush FPS.

## Building
Set the VINTAGE_STORY environmental variable before loading or trying to build the project. The [wiki](https://wiki.vintagestory.at/index.php/Modding:Preparing_For_Code_Mods#Creating_an_Environment_Variable) has directions for setting the variable on Windows.

The mod can be built with either Visual Studio, Visual Studio Code, or directly with msbuild. It has been tested on Windows and Linux.

### Visual Studio or Visual Studio Code
Load the solution in the code\VintageEngineering directory. Press F5 to launch Vintage Story in the debugger with the mod loaded.

Press Ctrl + Shift + B to build the mod without starting the debugger.

### MSBuild
Open a developer command prompt (one that has msbuild in the path). From the code\VintageEngineering directory, either run `msbuild` or `dotnet build`, depending on which is installed on your system.

### Packaging
After a successful build, the output will be placed in the mod directory. In order to distribute the mod, the contents of the mod directory must be manually zipped. Specifically the zip file should contain modinfo.json at the top level, rather than mod\modinfo.json.

## Debugging in VS Code

To allow the F11 key in the VS Code debugger to step into the base game libraries, create a source folder under the vintagestory folder, and checkout the game source there. For example, on Linux do:
```
mkdir "${VINTAGE_STORY}/source"
cd "${VINTAGE_STORY}/source"
git clone https://github.com/anegostudios/vssurvivalmod.git
git clone https://github.com/anegostudios/vsessentialsmod.git
git clone https://github.com/anegostudios/vsapi.git
```

## Help Is Welcome
Immersive Engineering has had over 80 contributers, I know a project like this is just overwhelming for one person. So I will be more then welcoming of other contributions.

# Current Contributers
Quentin (QPTech), Mister Andy Dandy(MAD), Rinly, DeathxxRenegade

# Code Wizard Contributers
bluelightning32

**What can you help with?**
- Models
- Textures
- Animations
- Code
- GUIs
- Translations
- Recipes
- Bug Fixing

Ideas are not on that list right now as I have a LOT of work to do before I'll be open to MORE to do. If you want to contribute please reach out to me (Buggi) on the VS discord, I'm in the "Modder" group. Or just submit a pull request. I'll try to post 'Issues' on specific items needed.

## Pull Request Rules
Please keep pull request changes simple and single-issue. Please include a description outlining the change. The faster I can merge the faster the project gets awesome.
Always include code comments outlining the change.

## Most Needed
The areas I'll need the most help and will be reaching out to people in hopes of gaining some assistance.
- Models and textures with animations.
- Multiblock models for swapping (if this is possible in the game).
- Translations, though wait for a bit on this to prevent redoing work in my constantly expanding language file.
- Particles or rendering optimizations.

## Realism Isn't The Goal
While I know some people love the gritty grindy slow process that chews up real life hours like candy, not everyone has the time. The default balance of this mod will try to give players plenty to accomplish without taking months out of their lives.

Of course as is the way of all my mods, you will be able to customize some base settings to make things harder or easier. Some things will have to be simplified to save update tick rate, like fluid and electrical system updates. I want to ensure players are able to build big without creating 4 FPS experiences. As usual, all the JSON the game uses for recipes, machine settings, etc will be fully patchable with the base-game mechanics so you can customize the experience all you want.
