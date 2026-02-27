## OpenNFS1AI
This is an AI enhanced port of OpenNFS1 (https://github.com/jeff-1amstudios/OpenNFS1)

## Changes made by AI
  * Ported from Monogame 3.2 to Monogame 3.8 using .net6.0 and SDL2 instead of OpenTK which builds in VS2022 and VS2026
  * Added real analog controller support (Xbox360 USB and PS5 USB controllers tested)

## Requires
  * OpenAL
  * Monogame >= 3.8 (Visual Studio should handle this so i guess no extra installation needed)
  * .NET 6.0 installed in Visual Studio (You get a notice if its missing when opening the project)
  * The CD_DATA Package from http://www.1amstudios.com/download/NFSSE_cd_data.zip (CD_DATA has to be in the same Folder as the OpenNFS1.exe)
  * vcredist 2013

### Build from source
```
git clone https://github.com/mrweedster/OpenNFS1AI.git
```
Install mgcb beforehand: 
```
dotnet tool install --global dotnet-mgcb
```
Open OpenNFS1.sln in Visual Studio >=2022 (Community Edition is fine)

### Installer
None

### Options
This port has added the possibility to change the resolutions. Previously this was made in the code but now there's an added resolution menu when starting the game 

### Issues
- The warrior wheels have a black overlay
- The speedometer and tachometer needles overlap the steering wheel

### Fixes
- Lambo wheels are now rendered properly
- AI drivers improved
- Added sub-seconds to the lap times
- Added proper controller support for Xbox360 USB and PS5 USB controller
- Added menu controls for controllers
- Improved handbrake
- Added a resolution selection screen when the game starts
- Fixed the too wide wheels on the driver cars
- Added debug output which is disabled by default
- Fixed tacho/speedometer needles
- Menu graphics now scale with the resolution
- Dashboards scale with the resolution
- Incar perspective changed so the street is better visible
- Stages/Races now end when the players car is stopped
- Races are now class races. There are three different classes and opponent cars get the same class as the player car
- Gamma corrects now depending on the detected GPU
- Added rear view mirrors for the in car perspective
- Proper digital speedometers for the Warrior and ZR1

## Legal:
Models, textures, tracks, cars by Pioneer Productions / EA Seattle (C) 1995.
OpenNFS1 or OpenNFS1AI is not affiated in any way with EA or Pioneer Productions
