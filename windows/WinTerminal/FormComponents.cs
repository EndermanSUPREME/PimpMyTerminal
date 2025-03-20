#nullable disable // Dont need null checking on parameters

namespace WinTerminal;

using System;
using System.Timers;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using System.Text.Json.Nodes;
using System.Drawing.Imaging;

public class FormComps: Form
{
    private Control.ControlCollection FormControls;

    private string sourceFolder = "";
    private Label folderLabel;

    private string sourceImage = "";
    private Label imageLabel;

    private TrackBar alphaSlider;
    private Label alphaLabel;

    private PictureBox imagePreview;
    private float sourceImageAlpha = 1f;

    private CheckBox toggleCheckBox;
    bool SlideShowMode = false;
    private System.Timers.Timer slideShowTimer;

    // Takes the forms controls to know where to add components to
    public void InitializeFormComponents(Control.ControlCollection formControls)
    {
        if (formControls == null)
        {
            Console.WriteLine("Form Controls Cannot be NULL!");
            return;
        }

        FormControls = formControls;

        CreateFormComponents();
        AddComponentsToForm();
    }

    private void CreateFormComponents() 
    {
        CreateNewButton("Set Source Folder", SelectFolderButton_Click, 0, 0);
        CreateNewButton("Get Random Image", GetRandImage, 0, 50);

        alphaLabel = new Label
        {
            Text = "Source Image Alpha: ",
            Location = new Point(0, 100),
            AutoSize = true
        };

        // Transparency Slider
        alphaSlider = new TrackBar
        {
            Minimum = 0,
            Maximum = 100, // We use 0-100 and scale it to 0-1
            Value = 100, // Fully opaque
            TickFrequency = 10,
            LargeChange = 10,
            SmallChange = 5,
            Location = new Point(0, 125),
            Width = 200
        };
        alphaSlider.Scroll += Alpha_Scroll;
        FormControls.Add(alphaSlider);


        // Initialize PictureBox
        imagePreview = new PictureBox
        {
            Size = new Size(300, 300), // Set size
            Location = new Point(250, 10),
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.StretchImage, // Adjust image size
            BackColor = Color.Black // Set background to black
        };

        CreateNewButton("Apply To Terminal", ApplyTerminal_Click, 300, 0);

        // Initialize Labels
        folderLabel = new Label
        {
            Text = "Terminal Background Images Folder: ",
            Location = new Point(50, 370),
            AutoSize = true
        };

        imageLabel = new Label
        {
            Text = "Terminal Background Image: ",
            Location = new Point(50, 400),
            AutoSize = true
        };

        // Initialize the checkbox
        toggleCheckBox = new CheckBox
        {
            Text = "Enable Function",
            Location = new System.Drawing.Point(20, 200),
            Checked = false // Initially unchecked
        };

        // Subscribe to the CheckedChanged event
        toggleCheckBox.CheckedChanged += TogglePresentation;

        // Add the checkbox to the form
        FormControls.Add(toggleCheckBox);
    }

    private void AddComponentsToForm()
    {
        FormControls.Add(folderLabel);
        FormControls.Add(imageLabel);
        FormControls.Add(alphaLabel);
        FormControls.Add(imagePreview);
    }

//=====================================================================================
//=====================================================================================

    // Create Buttons with give button text and On-Click function target
    private void CreateNewButton(string ButtonText, EventHandler ClickTarget, int PosX = 0, int PosY = 0)
    {
        // Create a button
        Button myButton = new Button();

        if (myButton != null)
        {
            // Set properties
            myButton.Text = ButtonText;
            myButton.Size = new System.Drawing.Size(ButtonText.Length + 200, ButtonText.Length + 25);
            myButton.Location = new System.Drawing.Point(PosX, PosY);

            // Attach Click event handler
            myButton.Click += ClickTarget;

            // Add the button to the form
            FormControls.Add(myButton);
        }
    }

    // Function to adjust image transparency
    private Bitmap AdjustImageOpacity(Image image, float opacity)
    {
        Bitmap bmp = new Bitmap(image.Width, image.Height);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            ColorMatrix matrix = new ColorMatrix
            {
                Matrix33 = opacity // Set transparency level
            };

            ImageAttributes attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

            g.DrawImage(image, new Rectangle(0, 0, bmp.Width, bmp.Height), 
                0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
        }
        return bmp;
    }

    // With a source folder pull a random image from it
    private void GetRandImage(object sender, EventArgs e)
    {
        if (!System.IO.Directory.Exists(sourceFolder)) return;

        var rand = new Random();
        string[] imageFileTypes = { ".png", ".jpg", ".jpeg" };

        // Enum Files works well for large collection of images
        var files = Directory.EnumerateFiles(sourceFolder).Where(file => imageFileTypes.Contains(Path.GetExtension(file).ToLower())).ToList();

        sourceImage = files[rand.Next(files.Count)];
        imageLabel.Text = "Terminal Background Image: " + sourceImage;

        using (Image originalImage = Image.FromFile(sourceImage))
        {
            imagePreview.Image = AdjustImageOpacity(originalImage, sourceImageAlpha);
        }
    }
    private void GetRandImage()
    {
        if (!System.IO.Directory.Exists(sourceFolder)) return;

        var rand = new Random();
        string[] imageFileTypes = { ".png", ".jpg", ".jpeg" };

        // Enum Files works well for large collection of images
        var files = Directory.EnumerateFiles(sourceFolder).Where(file => imageFileTypes.Contains(Path.GetExtension(file).ToLower())).ToList();

        sourceImage = files[rand.Next(files.Count)];
        imageLabel.Text = "Terminal Background Image: " + sourceImage;

        using (Image originalImage = Image.FromFile(sourceImage))
        {
            imagePreview.Image = AdjustImageOpacity(originalImage, sourceImageAlpha);
        }
    }

    private void SelectFolderButton_Click(object sender, EventArgs e)
    {
        using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
        {
            folderDialog.Description = "Select a folder";

            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                sourceFolder = folderDialog.SelectedPath;
                folderLabel.Text = "Terminal Background Images Folder: " + sourceFolder;
            }
        }
    }

    private string FindMicrosoftTerminalPath()
    {
        // Get the AppData\Local path
        string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        
        // Build the full path to the settings.json
        string WindowsTerminalSettingsFile = Path.Combine(LocalAppData, @"Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json");

        return WindowsTerminalSettingsFile;
    }

    private void ApplyTerminal_Click(object sender, EventArgs e)
    {
        // Check for Windows Terminal settings.json
        if (System.IO.File.Exists(FindMicrosoftTerminalPath()))
        {
            ModifyTerminalSettings(FindMicrosoftTerminalPath());
        } else
            {
                ShowInformationBox();
            }
    }

    private List<string> GetProfiles()
    {
        // Check for Windows Terminal settings.json
        if (System.IO.File.Exists(FindMicrosoftTerminalPath()))
        {
            try
            {
                List<string> Profiles = new List<string>();

                // Read JSON
                string jsonText = File.ReadAllText(FindMicrosoftTerminalPath());
                JsonNode json = JsonNode.Parse(jsonText);

                // Get the profiles list
                JsonArray profiles = json["profiles"]?["list"]?.AsArray();
                if (profiles == null)
                {
                    Console.WriteLine("Profiles section not found.");
                    return new List<string>();
                }

                foreach (JsonNode profile in profiles)
                {
                    Profiles.Add(profile?["name"]?.ToString());
                }
                
                return Profiles;
            } catch (System.Exception)
                {
                    MessageBox.Show("An error occurred reading settings!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return new List<string>();
                }
        } else
            {
                ShowInformationBox();
                return new List<string>();
            }
    }

    private void ModifyTerminalSettings(string settingsFile)
    {
        try
        {
            // Read JSON
            string jsonText = File.ReadAllText(settingsFile);
            JsonNode json = JsonNode.Parse(jsonText);

            // Get the profiles list
            JsonArray profiles = json["profiles"]?["list"]?.AsArray();
            if (profiles == null)
            {
                Console.WriteLine("Profiles section not found.");
                return;
            }

            foreach (JsonNode profile in profiles)
            {
                if (profile?["name"]?.ToString() == "Windows PowerShell") // Target specific profile
                {
                    profile["backgroundImage"] = sourceImage;
                    profile["backgroundImageOpacity"] = sourceImageAlpha;
                    profile["backgroundImageStretchMode"] = "fill";
                }
            }

            // Write back to file
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(settingsFile, json.ToJsonString(options));
        } catch (System.Exception)
            {
                MessageBox.Show("An error occurred when applying new settings!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
    }

    private void ShowInformationBox()
    {
        MessageBox.Show("Windows Terminal could not be located!\nClick the Install Button\nTo Install Windows Terminal."); 
        // create a new button within the form that takes the user to the microsoft store
        CreateNewButton("Install Microsoft Terminal", OpenMicrosoftStore_Click, 300, 310);
    }

    private void OpenMicrosoftStore_Click(object sender, EventArgs e)
    {
        // Link to Microsoft Terminal
        string storeUrl = "https://www.microsoft.com/store/productId/9N0DX20HK701?ocid=libraryshare";
            
        try
        {
            // Open Microsoft Store URL
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(storeUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error: " + ex.Message);
        }
    }

    // Uses the TrackBar to adjust the transparency of the source image
    private void Alpha_Scroll(object sender, EventArgs e)
    {
        sourceImageAlpha = alphaSlider.Value / 100f; // Scale 0-100 to 0-1
        alphaLabel.Text = $"Transparency: {sourceImageAlpha:F2}";

        // ensure the image file exists
        if (System.IO.File.Exists(sourceImage))
        {
            // redraw the image on the preview section when alpha is change
            using (Image originalImage = Image.FromFile(sourceImage))
            {
                imagePreview.Image = AdjustImageOpacity(originalImage, sourceImageAlpha);
            }
        }
    }

    // Event handler for when the checkbox is checked or unchecked
    private void TogglePresentation(object sender, EventArgs e)
    {
        if (toggleCheckBox.Checked)
        {
            // executes once
            SlideShowMode = true;

            if (imagePreview.Image == null) return;
            if (!SlideShowMode) return;

            // Set up the Timer to call the MyPeriodicFunction every 2 seconds (2000 milliseconds)
            slideShowTimer = new System.Timers.Timer(2000); // 2000 ms = 2 seconds
            slideShowTimer.Elapsed += ChangeImage; // Event handler to execute on each interval
            slideShowTimer.AutoReset = true; // Keep executing every 2 seconds
            slideShowTimer.Enabled = true; // Enable the timer
        } else
            {
                // executes once
                SlideShowMode = false;
                slideShowTimer.Enabled = false; // disable the timer
            }
    }

    private void ChangeImage(object sender, EventArgs e)
    {
        if (!System.IO.Directory.Exists(sourceFolder)) return;

        GetRandImage();

        // Check for Windows Terminal settings.json
        if (System.IO.File.Exists(FindMicrosoftTerminalPath()))
        {
            ModifyTerminalSettings(FindMicrosoftTerminalPath());
        } else
            {
                ShowInformationBox();
                if (toggleCheckBox != null) toggleCheckBox.Checked = false;
                SlideShowMode = false;
                slideShowTimer.Enabled = false;
            }
    }
}
