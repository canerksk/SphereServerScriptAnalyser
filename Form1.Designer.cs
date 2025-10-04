using System.Drawing;
using System.Windows.Forms;

namespace SphereServerScriptAnalyser
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        private TextBox txtFolder;
        private FolderBrowserDialog folderBrowserDialog1;
        private Button btnSelectFolder;

        // yeni eklenenler:
        private ListView lvProblems;
        private ColumnHeader chFile;
        private ColumnHeader chIssueCount;
        private Label lblStatus;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            txtFolder = new TextBox();
            folderBrowserDialog1 = new FolderBrowserDialog();
            btnSelectFolder = new Button();
            lvProblems = new ListView();
            chFile = new ColumnHeader();
            chIssueCount = new ColumnHeader();
            lblStatus = new Label();
            btnScanAgain = new Button();
            menuStrip1 = new MenuStrip();
            languageToolStripMenuItem = new ToolStripMenuItem();
            englishToolStripMenuItem = new ToolStripMenuItem();
            turkishToolStripMenuItem = new ToolStripMenuItem();
            frCAToolStripMenuItem = new ToolStripMenuItem();
            toolTip1 = new ToolTip(components);
            settingsToolStripMenuItem = new ToolStripMenuItem();
            setDefaultEditorToolStripMenuItem = new ToolStripMenuItem();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // txtFolder
            // 
            txtFolder.Location = new Point(12, 43);
            txtFolder.Name = "txtFolder";
            txtFolder.Size = new Size(520, 23);
            txtFolder.TabIndex = 0;
            // 
            // btnSelectFolder
            // 
            btnSelectFolder.Location = new Point(538, 43);
            btnSelectFolder.Name = "btnSelectFolder";
            btnSelectFolder.Size = new Size(96, 23);
            btnSelectFolder.TabIndex = 1;
            btnSelectFolder.Text = Properties.Resources.SelectFolder;
            btnSelectFolder.UseVisualStyleBackColor = true;
            btnSelectFolder.Click += btnSelectFolder_Click;
            // 
            // lvProblems
            // 
            lvProblems.Columns.AddRange(new ColumnHeader[] { chFile, chIssueCount });
            lvProblems.FullRowSelect = true;
            lvProblems.Location = new Point(12, 108);
            lvProblems.MultiSelect = false;
            lvProblems.Name = "lvProblems";
            lvProblems.Size = new Size(622, 360);
            lvProblems.TabIndex = 2;
            lvProblems.UseCompatibleStateImageBehavior = false;
            lvProblems.View = View.Details;
            lvProblems.ItemActivate += lvProblems_ItemActivate;
            // 
            // chFile
            // 
            chFile.Text = Properties.Resources.File;
            chFile.Width = 480;
            // 
            // chIssueCount
            // 
            chIssueCount.Text = Properties.Resources.Issue;
            chIssueCount.Width = 100;
            // 
            // lblStatus
            // 
            lblStatus.Location = new Point(12, 78);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(622, 17);
            lblStatus.TabIndex = 0;
            lblStatus.Text = "Ready";
            // 
            // btnScanAgain
            // 
            btnScanAgain.Location = new Point(12, 474);
            btnScanAgain.Name = "btnScanAgain";
            btnScanAgain.Size = new Size(121, 23);
            btnScanAgain.TabIndex = 3;
            btnScanAgain.Text = Properties.Resources.Rescan;
            btnScanAgain.UseVisualStyleBackColor = true;
            btnScanAgain.Click += btnScanAgain_Click;
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { languageToolStripMenuItem, settingsToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(646, 24);
            menuStrip1.TabIndex = 4;
            menuStrip1.Text = "menuStrip1";
            // 
            // languageToolStripMenuItem
            // 
            languageToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { englishToolStripMenuItem, turkishToolStripMenuItem, frCAToolStripMenuItem });
            languageToolStripMenuItem.Name = "languageToolStripMenuItem";
            languageToolStripMenuItem.Size = new Size(71, 20);
            languageToolStripMenuItem.Text = Properties.Resources.Language;
            // 
            // englishToolStripMenuItem
            // 
            englishToolStripMenuItem.Name = "englishToolStripMenuItem";
            englishToolStripMenuItem.Size = new Size(106, 22);
            englishToolStripMenuItem.Text = "en-US";
            englishToolStripMenuItem.Click += mienUS_Click;
            // 
            // turkishToolStripMenuItem
            // 
            turkishToolStripMenuItem.Name = "turkishToolStripMenuItem";
            turkishToolStripMenuItem.Size = new Size(106, 22);
            turkishToolStripMenuItem.Text = "tr-TR";
            turkishToolStripMenuItem.Click += mitrTR_Click;
            // 
            // frCAToolStripMenuItem
            // 
            frCAToolStripMenuItem.Name = "frCAToolStripMenuItem";
            frCAToolStripMenuItem.Size = new Size(106, 22);
            frCAToolStripMenuItem.Text = "fr-CA";
            frCAToolStripMenuItem.Click += mifrCA_Click;
            // 
            // settingsToolStripMenuItem
            // 
            settingsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { setDefaultEditorToolStripMenuItem });
            settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            settingsToolStripMenuItem.Size = new Size(61, 20);
            settingsToolStripMenuItem.Text = Properties.Resources.Settings;
            // 
            // setDefaultEditorToolStripMenuItem
            // 
            setDefaultEditorToolStripMenuItem.Name = "setDefaultEditorToolStripMenuItem";
            setDefaultEditorToolStripMenuItem.Size = new Size(180, 22);
            setDefaultEditorToolStripMenuItem.Text = Properties.Resources.SetDefaultEditor;
            setDefaultEditorToolStripMenuItem.Click += setDefaultEditorToolStripMenuItem_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(646, 505);
            Controls.Add(btnScanAgain);
            Controls.Add(lblStatus);
            Controls.Add(lvProblems);
            Controls.Add(btnSelectFolder);
            Controls.Add(txtFolder);
            Controls.Add(menuStrip1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MainMenuStrip = menuStrip1;
            MaximizeBox = false;
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Sphere Server Script Analyser";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }
        private Button btnScanAgain;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem languageToolStripMenuItem;
        private ToolStripMenuItem englishToolStripMenuItem;
        private ToolStripMenuItem turkishToolStripMenuItem;
        private ToolStripMenuItem frCAToolStripMenuItem;
        private ToolTip toolTip1;
        private ToolStripMenuItem settingsToolStripMenuItem;
        private ToolStripMenuItem setDefaultEditorToolStripMenuItem;
    }
}
