using System;
using System.Drawing;
using System.Windows.Forms;

class TrayMenuActions
{
    public Action OpenPanel = delegate { };
    public Action Refresh = delegate { };
    public Action OpenSettings = delegate { };
    public Action Quit = delegate { };
}

// The right-click menu of the taskbar icons. Rebuilt on each opening to follow the language and theme.
class TrayMenu : ContextMenuStrip
{
    const float TextPixels = 13;
    const float SmallIconAt100Percent = 16;

    readonly TrayMenuActions actions;

    public TrayMenu(TrayMenuActions actions)
    {
        this.actions = actions;
        ShowImageMargin = false;
        Font = EmbeddedFonts.New(TextPixels * SystemInformation.SmallIconSize.Width / SmallIconAt100Percent, false);
        Items.Add(new ToolStripSeparator()); // A menu with no items does not open.
    }

    protected override void OnOpening(System.ComponentModel.CancelEventArgs e)
    {
        Items.Clear();
        BackColor = Theme.Background;
        Renderer = new ToolStripProfessionalRenderer(new MenuColors());
        AddItem("openPanel", actions.OpenPanel);
        AddItem("refreshNow", actions.Refresh);
        AddItem("settings", actions.OpenSettings);
        Items.Add(new ToolStripSeparator());
        AddItem("closeWidget", actions.Quit);
        base.OnOpening(e);
        e.Cancel = false;
    }

    void AddItem(string labelKey, Action run)
    {
        ToolStripMenuItem item = new ToolStripMenuItem(Localization.T(labelKey));
        item.ForeColor = Theme.Text;
        item.Click += delegate { run(); };
        Items.Add(item);
    }

    class MenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Background; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.Hover; } }
        public override Color MenuItemSelected { get { return Theme.Hover; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Background; } }
    }
}
