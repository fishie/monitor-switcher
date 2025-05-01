using System.ComponentModel;
using System.Xml;
using System.Reflection;
using System.Xml.Serialization;
using System.Diagnostics;
using MonitorSwitcher;

namespace MonitorSwitcherGui;

public class MonitorSwitcherGui : Form
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Parse command line
        string customSettingsDirectory = "";
        foreach (string iArg in args)
        {
            string[] argElements = iArg.Split(':', 2);

            switch (argElements[0].ToLower())
            {
                case "-settings":
                    customSettingsDirectory = argElements[1];
                    break;
            }
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MonitorSwitcherGui(customSettingsDirectory));
    }

    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _trayMenu;
    private readonly string _settingsDirectory;
    private readonly string _settingsDirectoryProfiles;
    private readonly List<Hotkey> _hotkeys;

    public MonitorSwitcherGui(string customSettingsDirectory)
    {
        // Initialize settings directory
        _settingsDirectory = DisplaySettings.GetSettingsDirectory(customSettingsDirectory);
        _settingsDirectoryProfiles = DisplaySettings.GetSettingsProfileDirectory(_settingsDirectory);

        if (!Directory.Exists(_settingsDirectory))
            Directory.CreateDirectory(_settingsDirectory);
        if (!Directory.Exists(_settingsDirectoryProfiles))
            Directory.CreateDirectory(_settingsDirectoryProfiles);

        // Initialize Hotkey list before loading settings
        _hotkeys = new List<Hotkey>();

        // Load all settings
        LoadSettings();

        // Refresh Hotkey Hooks
        KeyHooksRefresh();

        // Build up context menu
        _trayMenu = new ContextMenuStrip
        {
            ImageList = new ImageList
            {
                Images =
                {
                    new Icon(GetType(), "Icons.MainIcon.ico"),
                    new Icon(GetType(), "Icons.DeleteProfile.ico"),
                    new Icon(GetType(), "Icons.Exit.ico"),
                    new Icon(GetType(), "Icons.Profile.ico"),
                    new Icon(GetType(), "Icons.SaveProfile.ico"),
                    new Icon(GetType(), "Icons.NewProfile.ico"),
                    new Icon(GetType(), "Icons.About.ico"),
                    new Icon(GetType(), "Icons.Hotkey.ico"),
                }
            }
        };

        // add paypal png logo
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("MonitorSwitcherGui.Icons.PayPal.png");
        if (stream == null)
        {
            throw new Exception(
                "No resources were specified during compilation or the resource is not visible to the caller");
        }
        _trayMenu.ImageList.Images.Add(Image.FromStream(stream));

        // finally build tray menu
        BuildTrayMenu();

        // Create tray icon
        _trayIcon = new NotifyIcon
        {
            Text = "Monitor Profile Switcher",
            Icon = new Icon(GetType(), "Icons.MainIcon.ico"),
            ContextMenuStrip = _trayMenu,
            Visible = true,
        };
        _trayIcon.MouseUp += OnTrayClick;
    }

    private void KeyHooksRefresh()
    {
        var removeList = new List<Hotkey>();
        // check which hooks are still valid
        foreach (var hotkey in _hotkeys)
        {
            if (!File.Exists(ProfileFileFromName(hotkey.profileName)))
            {
                hotkey.UnregisterHotkey();
                removeList.Add(hotkey);
            }
        }
        if (removeList.Count > 0)
        {
            foreach (var hotkey in removeList)
            {
                _hotkeys.Remove(hotkey);
            }
            removeList.Clear();
            SaveSettings();
        }

        // register the valid hooks
        foreach (var hotkey in _hotkeys)
        {
            hotkey.UnregisterHotkey();
            hotkey.RegisterHotkey(this);
        }
    }

    public void KeyHook_KeyUp(object? sender, HandledEventArgs e)
    {
        var hotkeyCtrl = sender as HotkeyCtrl;
        var hotkey = FindHotkey(hotkeyCtrl);
        if (hotkey == null)
        {
            throw new Exception("Hotkey could not be found");
        }
        LoadProfile(hotkey.profileName);
        e.Handled = true;
    }

    private void LoadSettings()
    {
        // Unregister and clear all existing hotkeys
        foreach (var hotkey in _hotkeys) {
            hotkey.UnregisterHotkey();
        }
        _hotkeys.Clear();

        // Loading the xml file
        if (!File.Exists(SettingsFileFromName("Hotkeys")))
            return;

        var readerHotkey = new XmlSerializer(typeof(Hotkey));

        try
        {
            var xmlReader = XmlReader.Create(SettingsFileFromName("Hotkeys"));
            xmlReader.Read();
            while (true)
            {
                if (xmlReader.Name == "Hotkey" && xmlReader.IsStartElement())
                {
                    var hotkey = readerHotkey.Deserialize<Hotkey>(xmlReader);
                    _hotkeys.Add(hotkey);
                    continue;
                }

                if (!xmlReader.Read())
                {
                    break;
                }
            }
            xmlReader.Close();
        }
        catch
        {
            // TODO: why do we ignore all exceptions?
        }
    }

    private void SaveSettings()
    {
        var writerHotkey = new XmlSerializer(typeof(Hotkey));
        var settings = new XmlWriterSettings { CloseOutput = true };

        try
        {
            using var fileStream = new FileStream(SettingsFileFromName("Hotkeys"), FileMode.Create);
            var xmlWriter = XmlWriter.Create(fileStream, settings);
            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement("hotkeys");
            foreach (var hotkey in _hotkeys)
            {
                writerHotkey.Serialize(xmlWriter, hotkey);
            }
            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndDocument();
            xmlWriter.Flush();
            xmlWriter.Close();
            fileStream.Close();
        }
        catch
        {
            // TODO: why do we ignore all exceptions?
        }
    }

    private Hotkey? FindHotkey(HotkeyCtrl? ctrl)
    {
        foreach (var hotkey in _hotkeys)
        {
            if (hotkey.hotkeyCtrl == ctrl)
            {
                return hotkey;
            }
        }

        return null;
    }

    private Hotkey? FindHotkey(string name)
    {
        foreach (var hotkey in _hotkeys)
        {
            if (hotkey.profileName == name)
            {
                return hotkey;
            }
        }

        return null;
    }

    private void BuildTrayMenu()
    {
        ToolStripItem newMenuItem;

        _trayMenu.Items.Clear();

        _trayMenu.Items.Add("Load Profile").Enabled = false;
        _trayMenu.Items.Add("-");

        // Find all profile files
        string[] profiles = Directory.GetFiles(_settingsDirectoryProfiles, "*.xml")
            .Select(filename =>
            {
                var profile = Path.GetFileNameWithoutExtension(filename);
                return profile ?? throw new NullReferenceException(
                    $"Could not get file name without extension for file name '{filename}'");
            })
            .ToArray();

        // Add to load menu
        foreach (string profile in profiles)
        {
            newMenuItem = _trayMenu.Items.Add(profile);
            newMenuItem.Tag = profile;
            newMenuItem.Click += OnMenuLoad;
            newMenuItem.ImageIndex = 3;
        }

        // Menu for saving items
        _trayMenu.Items.Add("-");
        var saveMenu = new ToolStripMenuItem("Save Profile")
        {
            ImageIndex = 4,
            DropDown = new ToolStripDropDownMenu()
        };
        saveMenu.DropDown.ImageList = _trayMenu.ImageList;
        _trayMenu.Items.Add(saveMenu);

        newMenuItem = saveMenu.DropDownItems.Add("New Profile...");
        newMenuItem.Click += OnMenuSaveAs;
        newMenuItem.ImageIndex = 5;
        saveMenu.DropDownItems.Add("-");

        // Menu for deleting items
        var deleteMenu = new ToolStripMenuItem("Delete Profile")
        {
            ImageIndex = 1,
            DropDown = new ToolStripDropDownMenu()
        };
        deleteMenu.DropDown.ImageList = _trayMenu.ImageList;
        _trayMenu.Items.Add(deleteMenu);

        // Menu for hotkeys
        var hotkeyMenu = new ToolStripMenuItem("Set Hotkeys")
        {
            ImageIndex = 7,
            DropDown = new ToolStripDropDownMenu()
        };
        hotkeyMenu.DropDown.ImageList = _trayMenu.ImageList;
        _trayMenu.Items.Add(hotkeyMenu);

        // Add to delete, save and hotkey menus
        foreach (string profile in profiles)
        {
            newMenuItem = saveMenu.DropDownItems.Add(profile);
            newMenuItem.Tag = profile;
            newMenuItem.Click += OnMenuSave;
            newMenuItem.ImageIndex = 3;

            newMenuItem = deleteMenu.DropDownItems.Add(profile);
            newMenuItem.Tag = profile;
            newMenuItem.Click += OnMenuDelete;
            newMenuItem.ImageIndex = 3;

            string hotkeyString = "(No Hotkey)";
            // check if a hotkey is assigned
            var hotkey = FindHotkey(profile);
            if (hotkey != null)
            {
                hotkeyString = $"({hotkey})";
            }

            newMenuItem = hotkeyMenu.DropDownItems.Add($"{profile} {hotkeyString}");
            newMenuItem.Tag = profile;
            newMenuItem.Click += OnHotkeySet;
            newMenuItem.ImageIndex = 3;
        }

        _trayMenu.Items.Add("-");
        newMenuItem = _trayMenu.Items.Add("Turn Off All Monitors");
        newMenuItem.Click += OnEnergySaving;
        newMenuItem.ImageIndex = 0;

        _trayMenu.Items.Add("-");
        newMenuItem = _trayMenu.Items.Add("About");
        newMenuItem.Click += OnMenuAbout;
        newMenuItem.ImageIndex = 6;

        newMenuItem = _trayMenu.Items.Add("Donate");
        newMenuItem.Click += OnMenuDonate;
        newMenuItem.ImageIndex = 8;

        newMenuItem = _trayMenu.Items.Add("Exit");
        newMenuItem.Click += OnMenuExit;
        newMenuItem.ImageIndex = 2;
    }

    private string ProfileFileFromName(string? profileName)
    {
        if (profileName == null)
        {
            throw new NullReferenceException("Profile name is null");
        }
        return Path.Combine(_settingsDirectoryProfiles, profileName + ".xml");
    }

    private string SettingsFileFromName(string name)
    {
        return Path.Combine(_settingsDirectory, name + ".xml");
    }

    private void OnEnergySaving(object? sender, EventArgs e)
    {
        Thread.Sleep(500); // wait for 500 milliseconds to give the user the chance to leave the mouse alone
        SendMessageApi.PostMessage(new IntPtr(SendMessageApi.HWND_BROADCAST), SendMessageApi.WM_SYSCOMMAND, new IntPtr(SendMessageApi.SC_MONITORPOWER), new IntPtr(SendMessageApi.MONITOR_OFF));
    }

    private void OnMenuAbout(object? sender, EventArgs e)
    {
        MessageBox.Show("Monitor Profile Switcher by Martin Krämer \n(MartinKraemer84@gmail.com)\nVersion 0.9.0.0\nCopyright 2013-2017 \n\nhttps://sourceforge.net/projects/monitorswitcher/", "About Monitor Profile Switcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnMenuDonate(object? sender, EventArgs e)
    {
        Process.Start("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=Y329BPYNKDTLC");
    }

    private void OnMenuSaveAs(object? sender, EventArgs e)
    {
        string profileName = "New Profile";
        if (InputBox("Save as new profile", "Enter name of new profile", ref profileName) == DialogResult.OK)
        {
            string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            foreach (char invalidChar in invalidChars)
            {
                profileName = profileName.Replace(invalidChar.ToString(), "");
            }

            if (profileName.Trim().Length > 0)
            {
                if (!DisplaySettings.SaveDisplaySettings(ProfileFileFromName(profileName)))
                {
                    _trayIcon.BalloonTipTitle = "Failed to save Multi Monitor profile";
                    _trayIcon.BalloonTipText = "MonitorSwitcher was unable to save the current profile to a new profile with name\"" + profileName + "\"";
                    _trayIcon.BalloonTipIcon = ToolTipIcon.Error;
                    _trayIcon.ShowBalloonTip(5000);
                }
            }
        }
    }

    private void OnHotkeySet(object? sender, EventArgs e)
    {
        var profileName = GetProfileName(sender);
        var hotkey = FindHotkey(profileName);
        bool isNewHotkey = hotkey == null;
        if (HotkeySetting("Set Hotkey for Monitor Profile '" + profileName + "'", ref hotkey) == DialogResult.OK)
        {
            if (isNewHotkey && hotkey != null)
            {
                if (!hotkey.RemoveKey)
                {
                    hotkey.profileName = profileName;
                    _hotkeys.Add(hotkey);
                }
            }
            else if (hotkey != null && hotkey.RemoveKey)
            {
                _hotkeys.Remove(hotkey);
            }

            KeyHooksRefresh();
            SaveSettings();
        }
    }

    private void LoadProfile(string? name)
    {
        if (!DisplaySettings.LoadDisplaySettings(ProfileFileFromName(name)))
        {
            _trayIcon.BalloonTipTitle = "Failed to load Multi Monitor profile";
            _trayIcon.BalloonTipText = "MonitorSwitcher was unable to load the previously saved profile \"" + name + "\"";
            _trayIcon.BalloonTipIcon = ToolTipIcon.Error;
            _trayIcon.ShowBalloonTip(5000);
        }
    }

    private void OnMenuLoad(object? sender, EventArgs e)
    {
        var profileName = GetProfileName(sender);
        LoadProfile(profileName);
    }

    private void OnMenuSave(object? sender, EventArgs e)
    {
        var profileName = GetProfileName(sender);
        var filename = ProfileFileFromName(profileName);
        if (!DisplaySettings.SaveDisplaySettings(filename))
        {
            _trayIcon.BalloonTipTitle = "Failed to save Multi Monitor profile";
            _trayIcon.BalloonTipText = $"MonitorSwitcher was unable to save the current profile to name \"{profileName}\"";
            _trayIcon.BalloonTipIcon = ToolTipIcon.Error;
            _trayIcon.ShowBalloonTip(5000);
        }
    }

    private void OnMenuDelete(object? sender, EventArgs e)
    {
        var profileName = GetProfileName(sender);
        var filename = ProfileFileFromName(profileName);
        File.Delete(filename);
    }

    private static string GetProfileName(object? sender)
    {
        return ((ToolStripMenuItem?)sender)?.Tag as string
            ?? throw new NullReferenceException("Profile name is null");
    }

    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        BuildTrayMenu();

        if (e.Button == MouseButtons.Left)
        {
            var methodInfo = typeof(NotifyIcon).GetMethod(
                "ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            if (methodInfo == null)
            {
                throw new NullReferenceException("ShowContextMenu method not found on type NotifyIcon");
            }
            methodInfo.Invoke(_trayIcon, null);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        KeyHooksRefresh();
    }

    protected override void OnLoad(EventArgs e)
    {
        Visible = false; // Hide form window.
        ShowInTaskbar = false; // Remove from taskbar.

        base.OnLoad(e);
    }

    private void OnMenuExit(object? sender, EventArgs e)
    {
        Application.Exit();
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            // Release the icon resource.
            _trayIcon.Dispose();
        }

        base.Dispose(isDisposing);
    }

    private static DialogResult HotkeySetting(string title, ref Hotkey? value)
    {
        Form form = new Form();
        Label label = new Label();
        TextBox textBox = new TextBox();
        Button buttonOk = new Button();
        Button buttonCancel = new Button();
        Button buttonClear = new Button();

        form.Text = title;
        label.Text = "Press hotkey combination or click 'Clear Hotkey' to remove the current hotkey";
        if (value != null)
            textBox.Text = value.ToString();
        textBox.Tag = value;

        buttonClear.Text = "Clear Hotkey";
        buttonOk.Text = "OK";
        buttonCancel.Text = "Cancel";
        buttonOk.DialogResult = DialogResult.OK;
        buttonCancel.DialogResult = DialogResult.Cancel;

        label.SetBounds(9, 10, 372, 13);
        textBox.SetBounds(12, 36, 372 - 75 -8, 20);
        buttonOk.SetBounds(228, 72, 75, 23);
        buttonCancel.SetBounds(309, 72, 75, 23);
        buttonClear.SetBounds(309, 36 - 1, 75, 23);

        buttonClear.Tag = textBox;
        buttonClear.Click += buttonClear_Click;
        textBox.KeyDown += textBox_KeyDown;
        textBox.KeyUp += textBox_KeyUp;

        label.AutoSize = true;
        textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
        buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        buttonClear.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

        form.ClientSize = new Size(396, 107);
        form.Controls.AddRange([label, textBox, buttonOk, buttonCancel, buttonClear]);
        form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.StartPosition = FormStartPosition.CenterScreen;
        form.MinimizeBox = false;
        form.MaximizeBox = false;
        form.AcceptButton = buttonOk;
        form.CancelButton = buttonCancel;

        DialogResult dialogResult = form.ShowDialog();
        value = textBox.Tag as Hotkey;
        return dialogResult;
    }

    private static void textBox_KeyUp(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox { Tag: Hotkey hotkey } textBox)
        {
            // check if any additional key was pressed, if not don't accept hotkey
            if (hotkey.Key < Keys.D0 || !hotkey.Alt && !hotkey.Ctrl && !hotkey.Shift)
                textBox.Text = "";
        }
    }

    private static void textBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            var hotkey = textBox.Tag as Hotkey ?? new Hotkey();
            hotkey.AssignFromKeyEventArgs(e);

            e.Handled = true;
            e.SuppressKeyPress = true; // don't add user input to text box, just use custom display

            textBox.Text = hotkey.ToString();
            textBox.Tag = hotkey; // store the current key combination in the textbox tag (for later use)
        }
    }

    private static void buttonClear_Click(object? sender, EventArgs e)
    {
        if (sender is Button { Tag: TextBox { Tag: Hotkey hotkey} textBox })
        {
            hotkey.RemoveKey = true;
            textBox.Clear();
        }
    }

    private static DialogResult InputBox(string title, string promptText, ref string value)
    {
        Form form = new Form();
        Label label = new Label();
        TextBox textBox = new TextBox();
        Button buttonOk = new Button();
        Button buttonCancel = new Button();

        form.Text = title;
        label.Text = promptText;
        textBox.Text = value;

        buttonOk.Text = "OK";
        buttonCancel.Text = "Cancel";
        buttonOk.DialogResult = DialogResult.OK;
        buttonCancel.DialogResult = DialogResult.Cancel;

        label.SetBounds(9, 10, 372, 13);
        textBox.SetBounds(12, 36, 372, 20);
        buttonOk.SetBounds(228, 72, 75, 23);
        buttonCancel.SetBounds(309, 72, 75, 23);

        label.AutoSize = true;
        textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
        buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

        form.ClientSize = new Size(396, 107);
        form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
        form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.StartPosition = FormStartPosition.CenterScreen;
        form.MinimizeBox = false;
        form.MaximizeBox = false;
        form.AcceptButton = buttonOk;
        form.CancelButton = buttonCancel;

        DialogResult dialogResult = form.ShowDialog();
        value = textBox.Text;
        return dialogResult;
    }
}
