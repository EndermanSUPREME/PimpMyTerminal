#nullable disable // Dont need null checking on parameters

namespace WinTerminal;

using System.Windows.Forms;

public partial class WinTerminalForm : Form
{
    FormComps formComps = new FormComps();

    public WinTerminalForm()
    {
        InitializeComponent();
        formComps.InitializeFormComponents(this.Controls);
    }
}
