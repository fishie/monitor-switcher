using MonitorSwitcher;

namespace MonitorSwitcherGUIConfig;

public partial class MainForm : Form
{
    public MainForm()
    {
        InitializeComponent();
    }

    private void toolStripContainer1_TopToolStripPanel_Click(object sender, EventArgs e)
    {

    }

    private void Form1_Load(object sender, EventArgs e)
    {
        UpdateProfileList();
    }

    private void UpdateProfileList()
    {
        string settingsDirectory = DisplaySettings.GetSettingsDirectory(null);
        string settingsDirectoryProfiles = DisplaySettings.GetSettingsProfileDirectory(settingsDirectory);

        // get profiles
        string[] profiles = Directory.GetFiles(settingsDirectoryProfiles, "*.xml");
        foreach (string profile in profiles)
        {
            string itemCaption = Path.GetFileNameWithoutExtension(profile);
            lbProfiles.Items.Add(itemCaption);
        }

        UpdateGUIStatus();
    }

    private void UpdateGUIStatus()
    {
        tsbAdd.Enabled = true;

        tsbDelete.Enabled = (lbProfiles.SelectedItem != null);
        tsbExport.Enabled = (lbProfiles.SelectedItem != null);
    }

    private void toolStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
    {
        
    }

    private void lbProfiles_SelectedIndexChanged(object sender, EventArgs e)
    {
        UpdateGUIStatus();
    }
}
