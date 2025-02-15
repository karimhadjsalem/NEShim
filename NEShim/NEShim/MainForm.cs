namespace NEShim;

public partial class MainForm : Form
{
    private Button btnStartGame;
    private Button btnOptions;
    private Button btnExit;
    private Panel pnlMenu;

    public MainForm()
    {
        InitializeComponent();
        SetupForm();
    }

    private void SetupForm()
    {
        // Set the form to full screen
        this.WindowState = FormWindowState.Maximized;
        this.FormBorderStyle = FormBorderStyle.None; // No window borders
        this.TopMost = true; // Keep the form on top of other applications

        // Optional: Set background image (or custom graphics for the game)
        // this.BackgroundImage = Image.FromFile("background.png"); // Your custom background image
        // this.BackgroundImageLayout = ImageLayout.Center;
        // this.BackColor = Color.Aqua;
        
        // Create and configure the on-screen menu
        pnlMenu = new Panel();
        pnlMenu.Dock = DockStyle.Fill;
        pnlMenu.BackgroundImage = Image.FromFile("background.png"); // Your custom background image
        pnlMenu.BackgroundImageLayout = ImageLayout.Zoom;
        pnlMenu.BackColor = Color.Aqua;
        
        // Create Start Game Button
        btnStartGame = new Button();
        btnStartGame.Text = "Start Game";
        btnStartGame.ForeColor = Color.White;
        btnStartGame.BackColor = Color.DarkBlue;
        btnStartGame.FlatStyle = FlatStyle.Flat;
        btnStartGame.Size = new Size(200, 50);
        btnStartGame.Location = new Point(100, 30);
        btnStartGame.Click += BtnStartGame_Click;

        // Create Options Button
        btnOptions = new Button();
        btnOptions.Text = "Options";
        btnOptions.ForeColor = Color.White;
        btnOptions.BackColor = Color.DarkGreen;
        btnOptions.FlatStyle = FlatStyle.Flat;
        btnOptions.Size = new Size(200, 50);
        btnOptions.Location = new Point(100, 90);
        btnOptions.Click += BtnOptions_Click;

        // Create Exit Button
        btnExit = new Button();
        btnExit.Text = "Exit";
        btnExit.ForeColor = Color.White;
        btnExit.BackColor = Color.DarkRed;
        btnExit.FlatStyle = FlatStyle.Flat;
        btnExit.Size = new Size(200, 50);
        btnExit.Location = new Point(100, 150);
        btnExit.Click += BtnExit_Click;

        // Add buttons to the panel
        pnlMenu.Controls.Add(btnStartGame);
        pnlMenu.Controls.Add(btnOptions);
        pnlMenu.Controls.Add(btnExit);
        this.Controls.Add(pnlMenu);

        // Optional: Add other controls or animations to the form
    }

    private void BtnStartGame_Click(object sender, EventArgs e)
    {
        // Transition to the game or load a new form to start the game
        MessageBox.Show("Starting Game...");
    }

    private void BtnOptions_Click(object sender, EventArgs e)
    {
        // Open options menu (this could open another form or show a panel)
        MessageBox.Show("Opening Options...");
    }

    private void BtnExit_Click(object sender, EventArgs e)
    {
        // Close the application
        Application.Exit();
    }

    // Override OnKeyDown to allow navigation via keyboard (e.g., pressing 'Esc' to quit)
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            this.Close(); // Close the application when Escape is pressed
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }
}

