#nullable disable // Dont need null checking on parameters

namespace WinTerminal;

using System;
using System.Timers;
using System.Drawing;
using System.Threading;
using System.Text.Json;
using System.Drawing.Text;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

// Create Custom Class for JSON Serialization
class FormData
{
    public string TargetProfile { get; set; }
    public float BackgroundAlpha { get; set; }
    public string SourceDirPath { get; set; }
    public string SourceImgPath { get; set; }
    public string TerminalFont { get; set; }
    public bool SlideShowMode { get; set; }
    public int Minutes { get; set; }
    public int Seconds { get; set; }
}

public class FormComps: Form
{
    private Control.ControlCollection FormControls;
    private bool shuttingDown = false;

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

    private CheckBox slideShowToggle;
    bool SlideShowMode = false;
    
    private CheckBox filterFontToggle;
    bool filterFonts = false;

    private Thread ImageFadeThread;
    Thread SlideShowMain;
    private float changeTime = 0f;

    private NumericUpDown numMinutes;
    private NumericUpDown numSeconds;

    private ComboBox profileDropDown;
    private string SelectedTerminalProfile = "";

    private ComboBox fontDropDown;
    private string SelectedFontName = "";

    private Label SaveStateLabel;

    protected override void OnFormClosing(System.Windows.Forms.FormClosingEventArgs e)
    {
        shuttingDown = true;

        // ensure all threads have ended so there is no background process after exit
        // we want the program to completely exit
        if (ImageFadeThread != null)
        {
            ImageFadeThread.Join();
        }
        if (SlideShowMain != null)
        {
            SlideShowMain.Join();
        }

        Application.Exit();
    }

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
        LoadFormState(); // Attempts to locate local config settings to import saved state
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

        CreateToggle(ref slideShowToggle, "Enable Slide-Show", TogglePresentation, 20, 200);

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

        SaveStateLabel = new Label
        {
            Text = "",
            Location = new Point(20, 230),
            AutoSize = true
        };
        FormControls.Add(SaveStateLabel);
        CreateNewButton("Save State", SaveFormState_Click, 20, 275);
        CreateNewButton("Load State", LoadFormState_Click, 20, 320);

        // Create Dropdown for selecting desired font for terminal
        CreateToggle(ref filterFontToggle, "Filter For Mono Fonts", FilterFonts, 570, 225);

        // Set properties for the font select drop-down
        Label SelectedFontLabel = new Label
        {
            Text = "Selected Font:",
            Location = new System.Drawing.Point(570, 255),
            AutoSize = true
        };
        FormControls.Add(SelectedFontLabel);

        fontDropDown = new ComboBox();

        fontDropDown.Location = new Point(570, 285);
        fontDropDown.Width = 200;

        // Add items to the fontDropDown
        foreach (string fontName in GetInstalledFonts())
        {
            fontDropDown.Items.Add(fontName);
        }

        // Handle selection event
        fontDropDown.SelectedIndexChanged += (s, e) =>
        {
            // Have a file locking mechanism to ensure all file write operations
            // are performed successfully, currently some writes get overwriten
            // before being finalized
            SelectedFontName = fontDropDown.SelectedItem.ToString();
            UpdateTerminalFont(SelectedFontName);
        };

        // Add to form
        FormControls.Add(fontDropDown);

        CreateNewButton("Can't Find Font?", FontInformation, 570, 315);
    } // End Of Component Creation Func

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

    private void FontInformation(object sender, EventArgs e)
    {
        MessageBox.Show("If you cannot find the Font you're looking for, you may need to install your font (.ttf, .odt)"+
        "into your Windows System.\n\nIt is Highly Suggested you use a MONO Font in the Terminal, mono-font ensures all " +
        "characters are equally spaced apart, non-mono fonts may result in hard to read text.\n\n" +
        "To ensure the font is found with the mono-filter, ensure the font name includes the word 'mono'",
        "Font Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // With a source folder pull a random image from it
    private void GetRandImage_Click(object sender, EventArgs e)
    {
        // we dont want to switch images while transitioning
        if (!isTransitioning)
        {
            GetRandImage();
        }
    }
    private void GetRandImage()
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

    private void FilterFonts(object sender, EventArgs e)
    {
        FilterFonts();
    }
    private void FilterFonts()
    {
        if (filterFontToggle == null || fontDropDown == null) return;
        filterFonts = filterFontToggle.Checked;

        // erase the current items
        fontDropDown.Items.Clear();

        // Add items to the fontDropDown
        foreach (string fontName in GetInstalledFonts())
        {
            fontDropDown.Items.Add(fontName);
        }
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

    // Creates a CheckBox object for a referenced CheckBox variable
    private void CreateToggle(ref CheckBox toggleComp, string toggleText, EventHandler ToggleTarget, int PosX = 0, int PosY = 0)
    {
        // Initialize the checkbox
        toggleComp = new CheckBox
        {
            Text = toggleText,
            AutoSize = true,
            Location = new System.Drawing.Point(PosX, PosY),
            Checked = false // Initially unchecked
        };

        // execute a function when the toggle is interacted with
        toggleComp.CheckedChanged += ToggleTarget;

        // Add the checkbox to the form
        FormControls.Add(toggleComp);
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

    // Returns a list of Font Names that are installed on the device
    private List<string> GetInstalledFonts()
    {
        List<string> fontList = new List<string>();
    
        InstalledFontCollection fontsCollection = new InstalledFontCollection();
        var fonts = fontsCollection.Families.Select(f => f.Name);

        foreach (string font in fonts)
        {
            if (filterFonts) {
                if ( font.ToLower().Contains("mono") )
                    fontList.Add(font);
            } else
                {
                    fontList.Add(font);
                }
        }

        return fontList;
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

    private void UpdateTerminalFont(string fontName)
    {
        // need a valid profile selected
        if (!GetProfiles().Contains(SelectedTerminalProfile)) return;

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
                // find the profile that the fonts being assigned to
                if (profile?["name"]?.ToString() == SelectedTerminalProfile) // Target specific profile
                {
                    // ensure the font object is present
                    if (profile?["font"] != null)
                    {
                        // set the font family (face)
                        profile["font"]["face"] = fontName;
                    } else
                        {
                            // add the font json within the profile json node
                            JsonObject terminalFontObject = new JsonObject
                            {
                                ["face"] = fontName,
                                ["size"] = 12
                            };

                            profile["font"] = terminalFontObject;
                        }
                    // escape the loop as we found the profile we want to edit
                    break;
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
    
//=================================================================================
//=================================================================================
// Slide Show Functionality

    // Event handler for when the checkbox is checked or unchecked
    private void TogglePresentation(object sender, EventArgs e)
    {
        if (slideShowToggle != null)
        {
            if (slideShowToggle.Checked)
            {
                // executes once
                ReadySlideShow();
            } else
                {
                    // executes once
                    SlideShowMode = false;
                }
        }
    }

    // Trigger a piece of logic that can periodically execute
    private void ReadySlideShow()
    {
        SlideShowMode = true;
        if (imagePreview.Image == null) return;
        if (!SlideShowMode) return;
        if (shuttingDown) return;

        if (SlideShowMain == null || !SlideShowMain.IsAlive)
        {
            SlideShowMain = new Thread(SlideShowThread);
            SlideShowMain.IsBackground = true;
            SlideShowMain.Start();
        }
    }

    private void SlideShowThread()
    {
        while (true)
        {
            // if any important components are not ready do not
            // Find a new image or start the transition process
            if (imagePreview.Image == null) continue;
            if (isTransitioning) continue;
            if (!SlideShowMode) return;

            // Fetch the new image
            ChangeImage();

            // Start the Fade Transition
            TransitionImage();
        }
    }

    // Gets a random image and displays it as the background of the Terminal
    private void ChangeImage(object sender, EventArgs e)
    {
        ChangeImage();
    }
    private void ChangeImage()
    {
        if (isTransitioning) return;
        if (!System.IO.Directory.Exists(sourceFolder)) return;

        GetRandImage();
    }

    private void TransitionImage()
    {
        // Check for Windows Terminal settings.json
        if (System.IO.File.Exists(FindMicrosoftTerminalPath()))
        {
            // Potentially start a new thread to handle the Image Fade Transition
            StartFade();
        } else
            {
                ShowInformationBox();
                if (slideShowToggle != null) slideShowToggle.Checked = false;
                SlideShowMode = false;
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
        if (shuttingDown || isTransitioning) return;

        isTransitioning = true;
        if (ImageFadeThread == null)
        {
            // Run the FadeTransition in its own thread
            ImageFadeThread = new Thread(FadeTransition);
            ImageFadeThread.IsBackground = true;
            ImageFadeThread.Start();
        } else if (!ImageFadeThread.IsAlive)
            {
                // Ensure previous thread finished before making a new one
                ImageFadeThread = new Thread(FadeTransition);
                ImageFadeThread.IsBackground = true;
                ImageFadeThread.Start();
            }
    }

    private void FadeTransition()
    {
        while (true) {
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
                        break;
                    }
        }

        // Thread sleeps for desired amount of second before being able to execute again
        Thread.Sleep((int)changeTime);
        isTransitioning = false;
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
    
//=================================================================================
//=================================================================================
// WinForm Settings Saving and Loading

    private string PimpMyTerminalSettingsPath()
    {
        // Get the AppData\Local path
        string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        
        // Build the full path to the config file for this application
        string settingsPath = Path.Combine(LocalAppData, @"Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\pimpmyterminal.json");
        return settingsPath;
    }

    private void SaveFormState_Click(object sender, EventArgs e)
    {
        SaveFormState();
    }
    private void SaveFormState()
    {
        if (SaveStateLabel == null || isTransitioning) return;

        try
        {
            // Create an object to serialize
            var data = new FormData
            {
                TargetProfile = SelectedTerminalProfile,
                BackgroundAlpha = sourceImageAlpha,
                SourceDirPath = sourceFolder,
                SourceImgPath = sourceImage,
                TerminalFont = SelectedFontName,
                SlideShowMode = SlideShowMode,
                Minutes = (int)numMinutes.Value,
                Seconds = (int)numSeconds.Value
            };

            // Convert object to JSON string
            string jsonString = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

            // Write JSON to a file
            File.WriteAllText(PimpMyTerminalSettingsPath(), jsonString);

            SaveStateLabel.Text = "State Saved Successfully!";
        } catch (System.Exception)
            {
                SaveStateLabel.Text = "Error Occured when Saving State!";
            }
    }

    private void LoadFormState_Click(object sender, EventArgs e)
    {
        LoadFormState();
    }
    private void LoadFormState()
    {
        if (SaveStateLabel == null || isTransitioning) return;

        string settingsFile = PimpMyTerminalSettingsPath();
        if (System.IO.File.Exists(settingsFile))
        {
            try
            {
                // Read JSON
                string jsonText = File.ReadAllText(settingsFile);
                JsonNode json = JsonNode.Parse(jsonText);

                // Set the local variables related to various GUI options
                sourceFolder = json["SourceDirPath"].ToString();
                sourceImage = json["SourceImgPath"].ToString();
                sourceImageAlpha = (float)json["BackgroundAlpha"];
                SlideShowMode = (bool)json["SlideShowMode"];
                SelectedTerminalProfile = json["TargetProfile"].ToString();
                SelectedFontName = json["SelectedFontName"].ToString();

                // set corresponding GUI
                if (profileDropDown != null) profileDropDown.SelectedItem = SelectedTerminalProfile;
                if (alphaSlider != null) alphaSlider.Value = (int)(sourceImageAlpha * 100f);
                if (folderLabel != null) folderLabel.Text = "Terminal Background Images Folder: " + sourceFolder;
                if (imageLabel != null) imageLabel.Text = "Terminal Background Image: " + sourceImage;
                if (numMinutes != null) numMinutes.Value = (int)json["Minutes"];
                if (numSeconds != null) numSeconds.Value = (int)json["Seconds"];
                if (imagePreview != null)
                {
                    if (System.IO.File.Exists(sourceImage))
                    {
                        imagePreview.Image = Image.FromFile(sourceImage);
                        newImg = Image.FromFile(sourceImage);
                    }
                }
                if (slideShowToggle != null) 
                {
                    slideShowToggle.Checked = SlideShowMode;
                    if (SlideShowMode)
                    {
                        ReadySlideShow();
                    }
                }
                if (fontDropDown != null) fontDropDown.SelectedItem = SelectedFontName;

                SaveStateLabel.Text = "State Loaded Successfully!";
            } catch (System.Exception)
                {
                    SaveStateLabel.Text = "Error Occured when\nLoading State!";
                }
        }
    }
}// EndScript