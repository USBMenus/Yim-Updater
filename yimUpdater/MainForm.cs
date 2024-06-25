using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Hosting;
using System.Windows.Forms;

namespace yimUpdater
{
    public partial class MainForm : Form
    {
        private bool fileExplorerOpened = false;
        private string selectedDLLPath = "";
        private const string placeholderText = "Enter process name WITHOUT .exe (Ex: GTA5 or RDR2)...";
        private Dictionary<string, string> knownProcesses;

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttribute, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("urlmon.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int URLDownloadToFile(IntPtr pCaller, string szURL, string szFileName, int dwReserved, IntPtr lpfnCB);

        private const int MAX_LOADSTRING = 100;

        public MainForm()
        {
            InitializeComponent();
            InitializeKnownProcesses();
            ToolTip tt = new ToolTip();
            tt.SetToolTip(downloadAddonButton, "Download The Extras Addon To %AppData%\\YimMenu\\scripts");
            tt.SetToolTip(downloadYimButton, "Download YimMenu.dll to your desktop");
            tt.SetToolTip(downloadHorseMenu, "Download HorseMenu.dll to your desktop");
            tt.SetToolTip(howToUseButton, "Shows a message explaining how to use YimMenu");
            tt.SetToolTip(extrasGuideButton, "Shows a message explaining how to add Extras Addon to YimMenu");
            tt.SetToolTip(animGuideButton, "Shows a message explaining how to add Animations to YimMenu");
            tt.SetToolTip(xmlGuideButton, "Shows a message explaining how to add XMLs to YimMenu");
            tt.SetToolTip(deleteCacheButton, "Deletes the Cache Folder from %AppData%\\YimMenu");
            tt.SetToolTip(injectYimButton, "Inject the selected DLL into the selected process");
            tt.SetToolTip(removeYimMenuButton, "Deletes YimMenu from %AppData%");
            tt.SetToolTip(downloadAnimationsButton, "Downloads animDictsCompact to %AppData%\\YimMenu");
            tt.SetToolTip(downloadXMLsButton, "Brings you to the XML's mega drive");
            tt.SetToolTip(processNameTextBox, "Enter a Process Name WITHOUT .exe at the end (Example: GTA5)");

            processNameTextBox.ForeColor = SystemColors.GrayText;
            processNameTextBox.Text = placeholderText;
            processNameTextBox.Enter += RemovePlaceholder;
            processNameTextBox.Leave += SetPlaceholder;
        }

        private void InitializeKnownProcesses()
        {
            knownProcesses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "GTA5", "PlayGTAV.exe" },
                { "RDR2", "RDR2.exe" },
                { "MINECRAFT", "minecraft.windows.exe" }
                // Add other known processes and their executables here
            };
        }

        private void StartProcessButton_Click(object sender, EventArgs e)
        {
            string processName = processNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(processName) || processName == placeholderText)
            {
                MessageBox.Show("Please enter a process name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (knownProcesses.TryGetValue(processName, out string executableName))
            {
                string executablePath = FindExecutable(executableName);
                if (!string.IsNullOrEmpty(executablePath))
                {
                    try
                    {
                        Process.Start(executablePath);
                        MessageBox.Show($"{processName} started successfully.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to start {processName}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show($"Executable for {processName} not found on any local drive.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show($"Process path for {processName} not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string FindExecutable(string executableName)
        {
            var excludedDirectories = new List<string> { "GTAV_Old", "SomeOtherFolderToExclude" }; // Add other folders to exclude

            foreach (char driveLetter in Enumerable.Range('A', 26).Select(i => (char)i))
            {
                string[] programFilesPaths = {
                $"{driveLetter}:\\Program Files",
                $"{driveLetter}:\\Program Files (x86)"
            };

                foreach (string programFilesPath in programFilesPaths)
                {
                    if (Directory.Exists(programFilesPath))
                    {
                        try
                        {
                            var files = Directory.GetFiles(programFilesPath, executableName, SearchOption.AllDirectories)
                                                 .Where(file => !excludedDirectories.Any(dir => file.IndexOf(dir, StringComparison.OrdinalIgnoreCase) >= 0));
                            if (files.Any())
                            {
                                return files.First(); // Return the first found executable path
                            }
                        }
                        catch (Exception ex)
                        {
                            // Handle any exceptions that occur during the search
                            Console.WriteLine($"Error searching directory {programFilesPath}: {ex.Message}");
                        }
                    }
                }
            }
            return null; // Executable not found
        }

        private void downloadAddonButton_Click(object sender, EventArgs e)
        {
            string baseUrl = "https://raw.githubusercontent.com/Deadlineem/Extras-Addon-for-YimMenu/main/";

            string folderPath = Environment.ExpandEnvironmentVariables("%APPDATA%\\YimMenu\\scripts");
            string[] fileNames = {
                "Extras-Addon.lua",
                "Extras-data.lua",
                "json.lua"
            };

            bool allDownloadsCompleted = true; // Flag to track if all downloads were successful

            for (int i = 0; i < fileNames.Length; i++)
            {
                if (!DownloadFile(baseUrl + fileNames[i], folderPath, fileNames[i]))
                {
                    allDownloadsCompleted = false; // Set flag to false if any download fails
                }
            }

            if (allDownloadsCompleted)
            {
                MessageBox.Show("Downloaded Extras Addon files to " + folderPath, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                allDownloadsCompleted = true;
            }
            else
            {
                MessageBox.Show("Failed to download all files. Please try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void downloadYimButton_Click(object sender, EventArgs e)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string filePath = Path.Combine(desktopPath, "");
                string url = "https://github.com/YimMenu/YimMenu/releases/download/nightly/YimMenu.dll";

                DownloadFile(url, filePath, "YimMenu.dll");

                MessageBox.Show("YimMenu.dll downloaded to your desktop.", "Download Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void downloadHorseMenu_Click(object sender, EventArgs e)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string filePath = Path.Combine(desktopPath, "");
                string url = "https://github.com/YimMenu/HorseMenu/releases/download/nightly/HorseMenu.dll";

                DownloadFile(url, filePath, "HorseMenu.dll");

                MessageBox.Show("HorseMenu.dll downloaded to your desktop.", "Download Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void howToUseButton_Click(object sender, EventArgs e)
        {
            string guideMessage = "How to Install/Use YimMenu:\n\n" +
                "1. Click Download YimMenu\n\n" +
                "2. Start GTA V \n\n" +
                "3. Inject YimMenu\n\n" +
                "4. Once in-game, press the INSERT key to open the YimMenu.\n\n" +
                "5. Enjoy using YimMenu to enhance your gameplay!\n\n";

            MessageBox.Show(guideMessage, "How to Install/Use YimMenu", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void gitHub_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Specify that the link was visited.
            this.gitHub.LinkVisited = true;

            // Navigate to the URL in the default web browser.
            System.Diagnostics.Process.Start(this.gitHub.Text);
        }

        private void extrasGuideButton_Click(object sender, EventArgs e)
        {
            string guideMessage = "How to Install/Use Extras Addon:\n\n" +
                "1. Download Extras Addon\n\n" +
                "2. Start GTA V \n\n" +
                "3. Inject YimMenu\n\n" +
                "4. Once in-game, press the INSERT key to open the YimMenu.\n\n" +
                "5. In YimMenu, Click Settings -> Lua Scripts\n\n" +
                "7. Enable the 'Auto Reload Changed Scripts' checkbox & press 'Reload All'\n\n" +
                "8. the 'Extras Addon' tab should appear at the bottom of YimMenu (right above the player list)\n\n";

            MessageBox.Show(guideMessage, "How to Install/Use Extras Addon", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void animGuideButton_Click(object sender, EventArgs e)
        {
            string guideMessage = "How to Install/Use Animations:\n\n" +
                "1. Click Download Animations\n" +
                "2. Start GTA V & Load into Story Mode COMPLETELY\n" +
                "3. Inject YimMenu\n" +
                "4. Once in-game, press the INSERT key to open YimMenu.\n" +
                "5. Click the 'Self' tab and underneath it, click Animations" +
                "7. Click 'List From Debug' on the right side and then click 'Fetch All Anims'\n" +
                "8. Use the searchbox or manually select an animation in the list and click Play";

            MessageBox.Show(guideMessage, "How to Install/Use Animations", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void xmlGuideButton_Click(object sender, EventArgs e)
        {
            string guideMessage = "How to Install/Use XML Maps/Vehicles:\n\n" +
            "1. Click Download XML's and your browser window should open to the mega url\n" +
            "2. After downloading, unzip the folder to your desktop and open it\n" +
            "3. Once it is opened, rename both folders (XML Maps and XML Vehicles) to the following\n\n" +
            "XML Maps -> xml_maps\n" +
            "XML Vehicles -> xml_vehicles\n\n" +
            "4. Once you are done, open a new File Explorer Window/Tab and type %APPDATA%\\YimMenu in the PATH box\n" +
            "5. You should now see a list of folders, one being xml_maps and the other being xml_vehicles\n" +
            "6. Simply drag and drop the folders you renamed from your desktop into the YimMenu folder & overwrite them\n" +
            "7. Once done, Load into GTA and go to the 'World' Tab for XML Maps OR 'Vehicle -> Vehicle Spawner' tab for XML Vehicles\n" +
            "(the spawner has a bullet that says XML)";

            DialogResult result = MessageBox.Show(guideMessage, "How to Install/Use XML's", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void deleteCacheButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to delete YimMenu's cache folder?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                string folderPath = Environment.ExpandEnvironmentVariables("%APPDATA%\\YimMenu\\cache");
                try
                {
                    Directory.Delete(folderPath, true);
                    MessageBox.Show("YimMenu's cache folder deleted successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error deleting cache folder: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void RemovePlaceholder(object sender, EventArgs e)
        {
            if (processNameTextBox.Text == placeholderText)
            {
                processNameTextBox.Text = "";
                processNameTextBox.ForeColor = SystemColors.WindowText;
            }
        }

        private void SetPlaceholder(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(processNameTextBox.Text))
            {
                processNameTextBox.Text = placeholderText;
                processNameTextBox.ForeColor = SystemColors.GrayText;
            }
        }

        private void injectYimButton_Click(object sender, EventArgs e)
        {
            if (!fileExplorerOpened)
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "DLL files (*.dll)|*.dll";
                    ofd.Title = "Select A DLL";
                    ofd.RestoreDirectory = false;

                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        selectedDLLPath = ofd.FileName;
                        fileExplorerOpened = false;
                    }
                    else
                    {
                        return; // Cancelled, do nothing
                    }
                }
            }

            string processName = processNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(processName) || processName == placeholderText)
            {
                MessageBox.Show("Please enter a process name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                MessageBox.Show($"{processName}.exe is not running. Please start the application before injecting", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (File.Exists(selectedDLLPath))
            {
                // Now you can inject the DLL into the process
                foreach (Process process in processes)
                {
                    InjectDLL(process, selectedDLLPath);
                }
            }
            else
            {
                MessageBox.Show("DLL not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void removeYimMenuButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to uninstall YimMenu?\nThis is only needed if you do not plan on playing GTA 5 again or you do not want to get caught.\nThis will remove all settings, scripts and saved data for YimMenu.", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                string yimMenuFolderPath = Environment.ExpandEnvironmentVariables("%APPDATA%\\YimMenu");
                try
                {
                    Directory.Delete(yimMenuFolderPath, true);
                    MessageBox.Show("YimMenu uninstalled successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error uninstalling YimMenu: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void downloadAnimationsButton_Click(object sender, EventArgs e)
        {
            string animationsUrl = "https://raw.githubusercontent.com/DurtyFree/gta-v-data-dumps/master/animDictsCompact.json";
            string yimMenuFolderPath = Environment.ExpandEnvironmentVariables("%APPDATA%\\YimMenu");
            string animationsFilePath = Path.Combine(yimMenuFolderPath, "animDictsCompact.json");

            if (DownloadFile(animationsUrl, yimMenuFolderPath, "animDictsCompact.json"))
            {
                MessageBox.Show("Animations file downloaded successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to download animations file. Please try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void downloadXMLsButton_Click(object sender, EventArgs e)
        {
            Process.Start("https://mega.nz/folder/BnM2jQoT#Lb6MG4m24nGv0GkNGsD3sQ/folder/sy9VWSDC");
        }

        private bool DownloadFile(string url, string folderPath, string fileName)
        {
            try
            {
                string filePath = Path.Combine(folderPath, fileName);

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                WebClient client = new WebClient();
                client.DownloadFile(url, filePath);

                return true; // Return true if download succeeds
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading file: {ex.Message}");
                return false; // Return false if download fails
            }
        }
        private void InjectDLL(Process process, string dllPath)
        {
            string dllName = Path.GetFileName(dllPath);
            foreach (ProcessModule mod in process.Modules)
            {
                if (mod.ModuleName.Equals(dllName, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(dllName + " is already injected.\n\nIf you unloaded with the menu, you need to restart the game to inject again.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            IntPtr processHandle = OpenProcess(0x1F0FFF, false, process.Id);

            if (processHandle == IntPtr.Zero)
            {
                MessageBox.Show("Failed to open process for injection", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            IntPtr loadLibraryAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");

            if (loadLibraryAddr == IntPtr.Zero)
            {
                MessageBox.Show("Failed to get address of LoadLibraryA", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            IntPtr addr = VirtualAllocEx(processHandle, IntPtr.Zero, (uint)((dllPath.Length + 1) * Marshal.SizeOf(typeof(char))), 0x1000, 0x40);

            if (addr == IntPtr.Zero)
            {
                MessageBox.Show("Failed to allocate memory in target process", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            IntPtr outSize;
            bool result = WriteProcessMemory(processHandle, addr, System.Text.Encoding.Default.GetBytes(dllPath), (uint)((dllPath.Length + 1) * Marshal.SizeOf(typeof(char))), out outSize);

            if (!result)
            {
                MessageBox.Show("Failed to write to process memory", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            IntPtr hThread = CreateRemoteThread(processHandle, IntPtr.Zero, 0, loadLibraryAddr, addr, 0, IntPtr.Zero);

            if (hThread == IntPtr.Zero)
            {
                MessageBox.Show("Failed to create remote thread", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            CloseHandle(hThread);
            CloseHandle(processHandle);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label1_Click_1(object sender, EventArgs e)
        {

        }
    }
}
