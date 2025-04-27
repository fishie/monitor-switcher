/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using MonitorSwitcher;

static void DebugOutput(string text)
{
    if (DisplaySettings.debug)
    {
        Console.WriteLine(text);
    }
}

bool validCommand = false;
foreach (string iArg in args)
{
    string[] argElements = iArg.Split(new char[] { ':' }, 2);

    switch (argElements[0].ToLower())
    {
        case "-debug":
            DisplaySettings.debug = true;
            DebugOutput("\nDebug output enabled");
            break;
        case "-noidmatch":
            DisplaySettings.noIDMatch = true;
            DebugOutput("\nDisabled matching of adapter IDs");
            break;
        case "-save":
            DisplaySettings.SaveDisplaySettings(argElements[1]);
            validCommand = true;
            break;
        case "-load":
            DisplaySettings.LoadDisplaySettings(argElements[1]);
            validCommand = true;
            break;
        case "-print":
            var pathInfoArray = new CCDWrapper.DisplayConfigPathInfo[0];
            var modeInfoArray = new CCDWrapper.DisplayConfigModeInfo[0];
            var additionalInfo = new CCDWrapper.MonitorAdditionalInfo[0];

            bool status = DisplaySettings.GetDisplaySettings(ref pathInfoArray, ref modeInfoArray, ref additionalInfo, true);
            if (status)
            {
                Console.WriteLine(DisplaySettings.PrintDisplaySettings(pathInfoArray, modeInfoArray));
            }
            else
            {
                Console.WriteLine("Failed to get display settings");
            }
            validCommand = true;
            break;
    }
}

if (!validCommand)
{
    Console.WriteLine("Monitor Profile Switcher command line utlility (version 0.9.0.0):\n");
    Console.WriteLine("Paremeters to MonitorSwitcher.exe:");
    Console.WriteLine("\t -save:{xmlfile} \t save the current monitor configuration to file (full path)");
    Console.WriteLine("\t -load:{xmlfile} \t load and apply monitor configuration from file (full path)");
    Console.WriteLine("\t -debug \t\t enable debug output (parameter must come before -load or -save)");
    Console.WriteLine("\t -noidmatch \t\t disable matching of adapter IDs");
    Console.WriteLine("\t -print \t\t print current monitor configuration to console");
    Console.WriteLine("");
    Console.WriteLine("Examples:");
    Console.WriteLine("\tMonitorSwitcher.exe -save:MyProfile.xml");
    Console.WriteLine("\tMonitorSwitcher.exe -load:MyProfile.xml");
    Console.WriteLine("\tMonitorSwitcher.exe -debug -load:MyProfile.xml");
    Console.ReadKey();
}
