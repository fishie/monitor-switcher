using System.ComponentModel;
using System.Runtime.InteropServices;

// Based on https://bloggablea.wordpress.com/2007/05/01/global-hotkeys-with-net/
namespace MonitorSwitcherGui;

public class HotkeyCtrl : IMessageFilter
{
    #region Interop

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, Keys vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int UnregisterHotKey(IntPtr hWnd, int id);

    private const uint WM_HOTKEY = 0x312;
    private const uint MOD_ALT = 0x1;
    private const uint MOD_CONTROL = 0x2;
    private const uint MOD_SHIFT = 0x4;
    private const uint ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

    #endregion

    private static int _currentId;
    private const int MaximumId = 0xBFFF;

    private Keys _keyCode;
    private bool _shift;
    private bool _control;
    private bool _alt;
    private int _id;
    private Control? _windowControl;

    public event HandledEventHandler? Pressed;

    public HotkeyCtrl() : this(Keys.None, false, false, false)
    {
        // No work done here!
    }

    private HotkeyCtrl(Keys keyCode, bool shift, bool control, bool alt)
    {
        // Assign properties
        KeyCode = keyCode;
        Shift = shift;
        Control = control;
        Alt = alt;

        // Register us as a message filter
        Application.AddMessageFilter(this);
    }

    ~HotkeyCtrl()
    {
        // Unregister the hotkey if necessary
        if (Registered)
        {
            Unregister();
        }
    }

    public bool GetCanRegister(Control windowControl)
    {
        // Handle any exceptions: they mean "no, you can't register" :)
        try
        {
            // Attempt to register
            if (!Register(windowControl))
            {
                return false;
            }

            // Unregister and say we managed it
            Unregister();
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public bool Register(Control windowControl)
    {
        // Check that we have not registered
        if (Registered)
        {
            throw new NotSupportedException("You cannot register a hotkey that is already registered");
        }

        // We can't register an empty hotkey
        if (Empty)
        {
            throw new NotSupportedException("You cannot register an empty hotkey");
        }

        // Get an ID for the hotkey and increase current ID
        _id = _currentId;
        _currentId += 1 % MaximumId;

        // Translate modifier keys into unmanaged version
        uint modifiers = (_alt ? MOD_ALT : 0) |
                         (_control ? MOD_CONTROL : 0) |
                         (_shift ? MOD_SHIFT : 0);

        // Register the hotkey
        if (RegisterHotKey(windowControl.Handle, _id, modifiers, _keyCode) == 0)
        {
            // Is the error that the hotkey is registered?
            if (Marshal.GetLastWin32Error() == ERROR_HOTKEY_ALREADY_REGISTERED)
            {
                return false;
            }

            throw new Win32Exception();
        }

        // Save the control reference and register state
        Registered = true;
        _windowControl = windowControl;

        // We successfully registered
        return true;
    }

    public void Unregister()
    {
        // Check that we have registered
        if (!Registered)
        {
            throw new NotSupportedException("You cannot unregister a hotkey that is not registered");
        }

        // It's possible that the control itself has died: in that case, no need to unregister!
        if (_windowControl != null && !_windowControl.IsDisposed)
        {
            // Clean up after ourselves
            if (UnregisterHotKey(_windowControl.Handle, _id) == 0)
            {
                //throw new Win32Exception();
            }
        }

        // Clear the control reference and register state
        Registered = false;
        _windowControl = null;
    }

    private void Reregister()
    {
        // Only do something if the key is already registered
        if (!Registered)
        {
            return;
        }

        if (_windowControl == null)
        {
            return;
        }

        // Save control reference
        var savedWindowControl = _windowControl;

        // Unregister and then reregister again
        Unregister();
        Register(savedWindowControl);
    }

    public bool PreFilterMessage(ref Message message)
    {
        // Only process WM_HOTKEY messages
        if (message.Msg != WM_HOTKEY)
        {
            return false;
        }

        // Check that the ID is our key and we are registered
        if (Registered && message.WParam.ToInt32() == _id)
        {
            // Fire the event and pass on the event if our handlers didn't handle it
            return OnPressed();
        }

        return false;
    }

    private bool OnPressed()
    {
        // Fire the event if we can
        var handledEventArgs = new HandledEventArgs(false);
        Pressed?.Invoke(this, handledEventArgs);

        // Return whether we handled the event or not
        return handledEventArgs.Handled;
    }

    public override string ToString()
    {
        // We can be empty
        if (Empty)
        {
            return "(none)";
        }

        // Build key name
        var keyName = Enum.GetName(_keyCode);
        Keys[] keysToStripFirstCharacter =
            [Keys.D0, Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9];
        if (keysToStripFirstCharacter.Contains(_keyCode))
        {
            keyName = keyName?[1..];
        }

        // Build modifiers
        string modifiers = "";
        if (_shift)   modifiers += "Shift+";
        if (_control) modifiers += "Control+";
        if (_alt)     modifiers += "Alt+";

        return modifiers + keyName;
    }

    private bool Empty => _keyCode == Keys.None;

    public bool Registered { get; private set; }

    public Keys KeyCode
    {
        set
        {
            // Save and reregister
            _keyCode = value;
            Reregister();
        }
    }

    public bool Shift
    {
        set
        {
            // Save and reregister
            _shift = value;
            Reregister();
        }
    }

    public bool Control
    {
        set
        {
            // Save and reregister
            _control = value;
            Reregister();
        }
    }

    public bool Alt
    {
        set
        {
            // Save and reregister
            _alt = value;
            Reregister();
        }
    }
}
