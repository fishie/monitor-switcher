namespace MonitorSwitcherGui;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Parse command line
        string customSettingsDirectory = "";
        foreach (var arg in args)
        {
            var argElements = arg.Split(':', 2);

            if (argElements[0].ToLower() == "-settings")
            {
                customSettingsDirectory = argElements[1];
            }
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MonitorSwitcherGui(customSettingsDirectory));
    }
}
