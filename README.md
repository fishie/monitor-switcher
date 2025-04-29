Forked from:
* Website: https://sourceforge.net/projects/monitorswitcher
* Author: Martin Krämer
* Email: MartinKraemer84@gmail.com

Updated the project to use C# 12 and .NET 9. Work in progress. Works on my machine ™.

Save multi monitor configurations of Windows and easily switch between them with a click in a popup menu of your taskbar.

The tool is designed for users with two or more monitors who in certain situations would like to quickly change their monitor setup
for performing a certain tasks (e.g. enable/disable a TV which is attached to the HDMI port, make an attached TV the primary display to get rid of tearing problems, switch which monitor is on the left/right...).

The program works by saving a current monitor setup as configured using the windows control panel to an XML file. Once saved the configuration can be quickly restored at any time.

The program is provided without any warranty. If you accidentally disable all your monitors boot into safe mode to fix the problem.

## CHANGELOG ##

### Version 0.5.3.0 ###
* Added "-debug" flag to console application to generate helpful console output for fixing problems
* Minor changes

### Version 0.5.2.0 ###
* Added menu option to turn off all monitors
* Fixed about window showing wrong version
* Fixed hotkeys not being set properly for profiles with dots in the profile name

### Version 0.5.0.0 ###
* Fixed a problem with hotkeys not working after some time or not working at all
* Fixed hotkey settings of old profiles not getting cleaned up
* Fixed errors when the program tried to access xml files which did not exist
* Fixed xml files not getting closed properly which could cause an error when accessed again

### Version 0.4.0.0 ###
* Fixed a problem with cloned displays using different connection types (e.g. DVI and HDMI)

### Version 0.3.0.0 ###
* Fixed monitor switching not working when multiple video cards were used
* Fixed a problem with hotkeys stopping to work after a some time

### Version 0.2.0.0 ###
* Added hotkey support
