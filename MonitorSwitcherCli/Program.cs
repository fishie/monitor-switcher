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
    string[] argElements = iArg.Split(':', 2);

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
            if (DisplaySettings.GetDisplaySettings(out var pathInfoArray, out var modeInfoArray, out _, true))
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
    Console.WriteLine("""
        Monitor Profile Switcher command line utlility (version 0.9.0.0):
        
        Parameters to MonitorSwitcher.exe:
            -save:{xmlfile}    save the current monitor configuration to file (full path)
            -load:{xmlfile}    load and apply monitor configuration from file (full path)
            -debug             enable debug output (parameter must come before -load or -save)
            -noidmatch         disable matching of adapter IDs
            -print             print current monitor configuration to console
    
        Examples:
            MonitorSwitcher.exe -save:MyProfile.xml
            MonitorSwitcher.exe -load:MyProfile.xml
            MonitorSwitcher.exe -debug -load:MyProfile.xml
        """);
    Console.ReadKey();
}
