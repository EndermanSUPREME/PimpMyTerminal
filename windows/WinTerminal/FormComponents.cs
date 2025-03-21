#nullable disable // Dont need null checking on parameters

namespace WinTerminal;

using System;
using System.Timers;
using System.Drawing;
using System.Threading;
using System.Text.Json;
using System.Windows.Forms;
using System.Text.Json.Nodes;
using System.Drawing.Imaging;

public class FormComps: Form
{
    private Control.ControlCollection FormControls;

    private string sourceFolder = "";
    private Label folderLabel;

    private string oldSourceImage = "", sourceImage = "";
    private Label imageLabel;

    private TrackBar alphaSlider;
    private Label alphaLabel;

    private PictureBox imagePreview;
    private Image oldImg, newImg;
    private float sourceImageAlpha = 1f;
    private float fadeAlpha = 1f;
    private int fadeStatus = 0;
    bool isTransitioning = false;

    private CheckBox toggleCheckBox;
    bool SlideShowMode = false;
    private System.Timers.Timer slideShowTimer;
    private Thread ImageFadeThread;
    private float changeTime = 0f;

    private NumericUpDown numMinutes;
    private NumericUpDown numSeconds;

    private ComboBox profileDropDown;
    private string SelectedTerminalProfile = "";

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
        SetSlideShowTimer(); // Sets Default Timer
    }

    private void CreateFormComponents() 
    {
        CreateNewButton("Set Source Folder", SelectFolderButton_Click, 0, 0);
        CreateNewButton("Get Random Image", GetRandImage_Click, 0, 50);

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

        // Add Controls for slide show functionality
        Label TimerMinutesLabel = new Label
        {
            Text = "Set Timer (minutes):",
            Location = new System.Drawing.Point(600, 0),
            AutoSize = true
        };
        numMinutes = new NumericUpDown()
        {
            Minimum = 0,
            Maximum = 10,
            Value = 1,
            Location = new System.Drawing.Point(600, 25),
            Width = 60
        };

        Label TimerSecondsLabel = new Label
        {
            Text = "Set Timer (seconds):",
            Location = new System.Drawing.Point(600, 60),
            AutoSize = true
        };
        numSeconds = new NumericUpDown()
        {
            Minimum = 0,
            Maximum = 60,
            Value = 0,
            Location = new System.Drawing.Point(600, 85),
            Width = 60
        };

        // Ensures both NumericUpDown components are defined before being accessed
        numMinutes.ValueChanged += SetSlideShowTimer;
        numSeconds.ValueChanged += SetSlideShowTimer;

        FormControls.Add(TimerSecondsLabel);
        FormControls.Add(numSeconds);
        FormControls.Add(TimerMinutesLabel);
        FormControls.Add(numMinutes);

        // Set properties for the profile select drop-down
        Label SelectedProfileLabel = new Label
        {
            Text = "Selected Profile:",
            Location = new System.Drawing.Point(570, 150),
            AutoSize = true
        };
        FormControls.Add(SelectedProfileLabel);

        profileDropDown = new ComboBox();

        profileDropDown.Location = new Point(570, 175);
        profileDropDown.Width = 200;

        // Add items
        foreach (string profile in GetProfiles())
        {
            profileDropDown.Items.Add(profile);
        }

        // Handle selection event
        profileDropDown.SelectedIndexChanged += (s, e) =>
        {
            SelectedTerminalProfile = profileDropDown.SelectedItem.ToString();
        };

        // Add to form
        FormControls.Add(profileDropDown);
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
// Form Component Target Functions

    // Uses the TrackBar to adjust the transparency of the source image
    private void Alpha_Scroll(object sender, EventArgs e)
    {
        sourceImageAlpha = alphaSlider.Value / 100f; // Scale 0-100 to 0-1
        alphaLabel.Text = $"Transparency: {sourceImageAlpha:F2}";

        // ensure the image file exists
        if (System.IO.File.Exists(sourceImage))
        {
            // Dont apply anything while in transition mode
            if (isTransitioning) return;

            // redraw the image on the preview section when alpha is change
            using (Image originalImage = Image.FromFile(sourceImage))
            {
                imagePreview.Image = AdjustImageOpacity(originalImage, sourceImageAlpha);
            }
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

    // With a source folder pull a random image from it
    private void GetRandImage_Click(object sender, EventArgs e)
    {
        // we dont want to switch images while transitioning
        if (!isTransitioning) GetRandImage_Click();
    }
    private void GetRandImage_Click()
    {
        if (!System.IO.Directory.Exists(sourceFolder)) return;

        var rand = new Random();
        string[] imageFileTypes = { ".png", ".jpg", ".jpeg" };

        // Enum Files works well for large collection of images
        var files = Directory.EnumerateFiles(sourceFolder).Where(file => imageFileTypes.Contains(Path.GetExtension(file).ToLower())).ToList();
        // remove the current image so when we fetch a random image we dont get the same image
        if (files.Count() > 1) files.Remove(sourceImage);

        // store the old image
        oldImg = newImg;
        oldSourceImage = sourceImage;

        sourceImage = files[rand.Next(files.Count)];
        imageLabel.Text = "Terminal Background Image: " + sourceImage;

        // store reference to the desired background image
        newImg = Image.FromFile(sourceImage);

        StartFade();
    }

//=====================================================================================
//=====================================================================================
// Form Component Helper Functions

    private string FindMicrosoftTerminalPath()
    {
        // Get the AppData\Local path
        string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        
        // Build the full path to the settings.json
        string WindowsTerminalSettingsFile = Path.Combine(LocalAppData, @"Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json");

        return WindowsTerminalSettingsFile;
    }

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

    // Adjust Desired Image Alpha with a desired Alpha Value
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
                    MessageBox.Show("Error Fetching Profiles!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return new List<string>();
                }
        } else
            {
                ShowInformationBox();
                return new List<string>();
            }
    }

//=========================================================================
//=========================================================================
// Edit Microsoft Terminal Settings JSON File

    private void ModifyTerminalSettings(string settingsFile)
    {
        // need a valid profile selected
        if (!GetProfiles().Contains(SelectedTerminalProfile)) return;

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
                if (profile?["name"]?.ToString() == SelectedTerminalProfile) // Target specific profile
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
                MessageBox.Show("Error occured in ModifyTerminalSettings", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        } catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
    }
    
//=================================================================================
//=================================================================================
// Slide Show Functionality

    // Event handler for when the checkbox is checked or unchecked
    private void TogglePresentation(object sender, EventArgs e)
    {
        if (toggleCheckBox != null)
        {
            if (toggleCheckBox.Checked)
            {
                // executes once
                SlideShowMode = true;

                if (imagePreview.Image == null) return;
                if (!SlideShowMode) return;

                // Timer allows us to run a function periodically every n number of miliseconds
                slideShowTimer = new System.Timers.Timer(changeTime);
                slideShowTimer.Elapsed += ChangeImage; // 
                slideShowTimer.AutoReset = true; // Loop Execution
                slideShowTimer.Enabled = true; // Enable the timer
            } else
                {
                    // executes once
                    SlideShowMode = false;
                    if (slideShowTimer != null) slideShowTimer.Enabled = false; // disable the timer
                }
        }
    }

    // Gets a random image and displays it as the background of the Terminal
    private void ChangeImage(object sender, EventArgs e)
    {
        if (isTransitioning) return;
        if (!System.IO.Directory.Exists(sourceFolder)) return;

        GetRandImage_Click();

        // Check for Windows Terminal settings.json
        if (System.IO.File.Exists(FindMicrosoftTerminalPath()))
        {
            // Potentially start a new thread to handle the Image Fade Transition
            StartFade();
        } else
            {
                ShowInformationBox();
                if (toggleCheckBox != null) toggleCheckBox.Checked = false;
                SlideShowMode = false;
                if (slideShowTimer != null) slideShowTimer.Enabled = false;
            }
    }

    // Event handler for when the checkbox is checked or unchecked
    private void SetSlideShowTimer(object sender, EventArgs e)
    {
        SetSlideShowTimer();
    }
    private void SetSlideShowTimer()
    {
        // 1s = 1000ms
        float minutes = (float)numMinutes.Value * (1000 * 60);
        float seconds = (float)numSeconds.Value * 1000;
        changeTime = minutes + seconds;
    }

//=================================================================================
//=================================================================================
// Image Fade Transition

    private void StartFade()
    {
        if (isTransitioning) return;

        isTransitioning = true;
        // Run the FadeTransition in its own thread
        ImageFadeThread = new Thread(FadeTransition);
        ImageFadeThread.Start();
    }

    private void FadeTransition()
    {
        while (isTransitioning) {
            if (fadeStatus == 0) // decrement alpha to 0
            {
                if (fadeAlpha > 0)
                {
                    fadeAlpha -= 0.01f;
                    
                    if (fadeAlpha < 0)
                    {
                        fadeAlpha = 0;
                        fadeStatus = 1;

                        ModifyBackgroundAlpha(oldImg, fadeAlpha, oldSourceImage);

                        // ensure the image file exists
                        if (System.IO.File.Exists(sourceImage))
                        {
                            // redraw the new image we selected
                            imagePreview.Image = AdjustImageOpacity(newImg, sourceImageAlpha);
                        }
                    } else
                        {
                            ModifyBackgroundAlpha(oldImg, fadeAlpha, oldSourceImage);
                        }
                }
            } else if (fadeStatus == 1) // increment alpha to original
                {
                    if (fadeAlpha < sourceImageAlpha)
                    {
                        fadeAlpha += 0.01f;
                        
                        if (fadeAlpha > sourceImageAlpha)
                        {
                            // Set the new image to display

                            fadeAlpha = sourceImageAlpha;
                            fadeStatus = 2;
                        }
                        
                        ModifyBackgroundAlpha(newImg, fadeAlpha, sourceImage);
                    }
                } else // reset transition state
                    {
                        fadeStatus = 0;
                        fadeAlpha = sourceImageAlpha;
                        ModifyBackgroundAlpha(newImg, fadeAlpha, sourceImage);
                        isTransitioning = false;
                    }
        }

        // Thread sleeps for 1 second before being able to execute again
        Thread.Sleep(1000);
    }
    
    // Used during the FadeTransition to adjust the Alpha Value
    // Adjusts the alpha within Settings JSON file
    private void ModifyBackgroundAlpha(Image targetImage, float alphaValue, string imagePath)
    {
        // need a valid profile selected
        if (!GetProfiles().Contains(SelectedTerminalProfile)) return;

        if (targetImage == null) return;

        imagePreview.Image = AdjustImageOpacity(targetImage, alphaValue);

        try
        {
            string settingsFile = FindMicrosoftTerminalPath();

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
                if (profile?["name"]?.ToString() == SelectedTerminalProfile) // Target specific profile
                {
                    profile["backgroundImage"] = imagePath;
                    profile["backgroundImageOpacity"] = alphaValue;
                    profile["backgroundImageStretchMode"] = "fill";
                }
            }

            // Write back to file
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(settingsFile, json.ToJsonString(options));
        } catch (System.Exception) // if the settings file is being used this catch will trigger
            {
                return;
            }
    }
}
