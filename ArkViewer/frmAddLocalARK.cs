﻿using ArkViewer.Configuration;
using ARKViewer.Models;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ARKViewer
{
    public partial class frmAddLocalARK : Form
    {

        public string Filename { get; set; } = "";
        public string OfflineName { get; set; } = "";

        public string ClusterFolder { get; set; } = string.Empty;

        private void LoadWindowSettings()
        {
            var savedWindow = ARKViewer.Program.ProgramConfig.Windows.FirstOrDefault(w => w.Name == this.Name);

            if (savedWindow != null)
            {
                var targetScreen = Screen.FromPoint(new Point(savedWindow.Left, savedWindow.Top));
                if (targetScreen == null) return;

                if (targetScreen.DeviceName == null || targetScreen.DeviceName == savedWindow.Monitor)
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.Left = savedWindow.Left;
                    this.Top = savedWindow.Top;
                    this.Width = savedWindow.Width;
                    this.Height = savedWindow.Height;
                }
            }
        }

        private void UpdateWindowSettings()
        {
            //only save location if normal window, do not save location/size if minimized/maximized
            if (this.WindowState == FormWindowState.Normal)
            {
                var savedWindow = ARKViewer.Program.ProgramConfig.Windows.FirstOrDefault(w => w.Name == this.Name);
                if (savedWindow == null)
                {
                    savedWindow = new ViewerWindow();
                    savedWindow.Name = this.Name;
                    ARKViewer.Program.ProgramConfig.Windows.Add(savedWindow);
                }

                if (savedWindow != null)
                {
                    var restoreScreen = Screen.FromHandle(this.Handle);

                    savedWindow.Left = this.Left;
                    savedWindow.Top = this.Top;
                    savedWindow.Width = this.Width;
                    savedWindow.Height = this.Height;
                    savedWindow.Monitor = restoreScreen.DeviceName;

                }
            }
        }


        public frmAddLocalARK(OfflineFileConfiguration selectedValue): this()
        {
            txtFilename.Text = selectedValue.Filename;
            txtName.Text = selectedValue.Name;
            txtClusterFolder.Text = selectedValue.ClusterFolder ?? "";
            btnSave.Enabled = true;
        }

        public frmAddLocalARK()
        {
            InitializeComponent();
            LoadWindowSettings();
        }

        private void frmAddLocalARK_FormClosed(object sender, FormClosedEventArgs e)
        {
            UpdateWindowSettings();
        }

        private void btnAddARK_Click(object sender, EventArgs e)
        {
            using(OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "ARK Save Games (*.ark)|*.ark";
                dialog.InitialDirectory = AppContext.BaseDirectory;
                dialog.Title = "Select ARK Save File";
                if(dialog.ShowDialog() == DialogResult.OK)
                {
                    if (File.Exists(dialog.FileName))
                    {
                        txtFilename.Text = dialog.FileName;
                    }

                }

                btnSave.Enabled = txtFilename.TextLength > 0;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            Filename = txtFilename.Text.Trim();
            OfflineName = txtName.Text.Trim();
            ClusterFolder = txtClusterFolder.Text.Trim();

            if(txtName.Text.Trim().Length == 0)
            {
                MessageBox.Show("Please enter a name for this offline file selection.", "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnClusterFolder_Click(object sender, EventArgs e)
        {
            using(var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select shared cluster folder location.";
                if(dialog.ShowDialog() == DialogResult.OK)
                {
                    txtClusterFolder.Text = dialog.SelectedPath;
                    btnSave.Enabled = true;
                }
                else
                {
                    txtClusterFolder.Text = String.Empty;
                    btnSave.Enabled = true;
                }
            }
        }
    }
}
