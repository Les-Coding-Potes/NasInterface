using NasInterface.Python;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.Forms.MessageBox;

namespace NasInterface
{
    public partial class Main : Form
    {
        private FileManager fileManager;
        private DiskManager diskManager;
        private PyRunner pyRunner;
        private const string ipAddress = "192.168.63.209";
        private const string port = "5000";
        public Main()
        {
            InitializeComponent();
            Load += Main_Load;
        }
        private async void Main_Load(object sender, EventArgs e)
        {
            fileManager = new FileManager();
            diskManager = new DiskManager();
            pyRunner = new PyRunner();

            pyRunner.ProcessCompleted += async (s, args) =>
            {
                if (!string.IsNullOrEmpty(args.Errors))
                    MessageBox.Show(args.Errors);
            };

            await InitializeAsync();
            await UpdateDiskSpace();
        }
        private async Task UpdateDiskSpace()
        {
            await diskManager.GetDiskSpace(ipAddress, port);

            lbDiskSpace.Text = $"{ diskManager.UsedSpace} free of {diskManager.TotalSpace}";
            double usedSpace = ExtractNumericValue(diskManager.UsedSpace);
            double totalSpace = ExtractNumericValue(diskManager.TotalSpace);

            if (usedSpace >= 0 && totalSpace > 0)
            {
                int progressValue = (int)((usedSpace / totalSpace) * 100);
                pbDiskSpace.Value = progressValue;
            }

        }
        private double ExtractNumericValue(string valueWithUnit)
        {
            string numericPart = string.Empty;

            foreach (char c in valueWithUnit)
            {
                if (char.IsDigit(c) || c == '.' || c == ',')
                {
                    numericPart += c;
                }
                else if (numericPart.Length > 0)
                {
                    break;
                }
            }

            numericPart = numericPart.Replace(',', '.');

            if (!string.IsNullOrEmpty(numericPart) && double.TryParse(numericPart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double numericValue))
            {
                return numericValue;
            }

            return -1; 
        }

        private async Task InitializeAsync()
        {
            var rootFile = await fileManager.GetFiles(ipAddress, port);

            if (rootFile != null)
            {
                LoadTreeViewIcons();

                TreeNode rootNode = new TreeNode(rootFile.Name);
                rootNode.Tag = "directory";

                PopulateTreeView(rootFile, rootNode);

                tv_NasFiles.Nodes.Clear();
                tv_NasFiles.Nodes.Add(rootNode);
            }
        }


        private void PopulateTreeView(NasFile nasFile, TreeNode parentNode)
        {
            PopulateNodes(nasFile.Directories, "folderIcon", parentNode);
            PopulateNodes(nasFile.Files, "fileIcon", parentNode);
        }

        private void PopulateNodes(List<NasFile> items, string imageKey, TreeNode parentNode)
        {
            if (items != null)
            {
                foreach (var item in items)
                {
                    TreeNode node = new TreeNode(item.Name);
                    node.ImageKey = imageKey;
                    node.SelectedImageKey = imageKey;
                    node.Tag = item.Type;
                    parentNode.Nodes.Add(node);

                    PopulateNodes(item.Directories, "folderIcon", node);
                    PopulateNodes(item.Files, "fileIcon", node);
                }
            }
        }


        private void LoadTreeViewIcons()
        {
            ImageList imageList = new ImageList();
            imageList.Images.Add("folderIcon", Properties.Resources.folder);
            imageList.Images.Add("fileIcon", Properties.Resources.file);

            tv_NasFiles.ImageList = imageList;
        }

        private void tv_NasFiles_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                tv_NasFiles.SelectedNode = e.Node;

                ContextMenu menu = new ContextMenu();


                MenuItem downloadItem = new MenuItem("Download");
                downloadItem.Click += DownloadItem_Click;
                menu.MenuItems.Add(downloadItem);

                MenuItem deleteItem = new MenuItem("Delete");
                deleteItem.Click += DeleteItem_Click;
                menu.MenuItems.Add(deleteItem);

                if (tv_NasFiles.SelectedNode.Tag.ToString() == "directory")
                {
                    MenuItem uploadItem = new MenuItem("Upload");
                    uploadItem.Click += UploadItem_Click;
                    menu.MenuItems.Add(uploadItem);
                }

                menu.Show(tv_NasFiles, e.Location);
            }
        }

        private async void DeleteItem_Click(object sender, EventArgs e)
        {
            TreeNode selectedNode = tv_NasFiles.SelectedNode;

            if (selectedNode != null)
            {
                if (await fileManager.Delete(ipAddress, port, selectedNode.FullPath))
                {
                    tv_NasFiles.Nodes.Remove(selectedNode);
                }
            }
        }


        private async void DownloadItem_Click(object sender, EventArgs e)
        {
            TreeNode selectedNode = tv_NasFiles.SelectedNode;
            if (selectedNode != null)
            {
                using (var folderDialog = new FolderBrowserDialog())
                {
                    DialogResult result = folderDialog.ShowDialog();
                    if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
                    {
                        string downloadPath = folderDialog.SelectedPath;

                        try
                        {
                            string downloadedFilePath = await fileManager.DownloadAndReturnPath(ipAddress, port, selectedNode.FullPath, downloadPath);
                            string decodedFilePath = Path.Combine(downloadPath, Path.GetFileNameWithoutExtension(downloadedFilePath));
                            await pyRunner.RunAsync(RunnerType.Decoder, decodedFilePath + ".png");

                            MessageBox.Show("Download and decoding successful.");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error downloading: {ex.Message}");
                        }
                    }
                    else
                    {
                        MessageBox.Show("No folder selected.");
                    }
                }
            }
        }

        private async void UploadItem_Click(object sender, EventArgs e)
        {
            TreeNode selectedNode = tv_NasFiles.SelectedNode;
            if (selectedNode != null && selectedNode.Tag.ToString() == "directory")
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Title = "Sélectionner un fichier à téléverser";
                    openFileDialog.Filter = "Tous les fichiers (*.*)|*.*";
                    openFileDialog.CheckFileExists = true;

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        string filePath = openFileDialog.FileName;
                        string projectDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
                        string tempFilePath = Path.Combine(projectDirectory, "Temp\\")+ Path.GetFileName(filePath);
                        await pyRunner.RunAsync(RunnerType.Encoder, @filePath);
                        if (await fileManager.Upload(ipAddress, port, tempFilePath+".png", selectedNode.FullPath))
                        {
                            string newFileName = Path.GetFileName(filePath)+".png";
                            TreeNode newNode = new TreeNode(newFileName);
                            newNode.ImageKey = "fileIcon";
                            newNode.SelectedImageKey = "fileIcon";
                            newNode.Tag = "file";
                            selectedNode.Nodes.Add(newNode);
                            await UpdateDiskSpace();
                        }
                    }
                }
            }
        }
    }
}
