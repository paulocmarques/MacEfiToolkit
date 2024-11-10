﻿// Mac EFI Toolkit
// https://github.com/MuertoGB/MacEfiToolkit

// Windows Forms
// frmSocRom.cs
// Released under the GNU GLP v3.0

using Mac_EFI_Toolkit.Firmware;
using Mac_EFI_Toolkit.Firmware.SOCROM;
using Mac_EFI_Toolkit.Tools;
using Mac_EFI_Toolkit.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Mac_EFI_Toolkit.Forms
{
    public partial class frmSocRom : METForm
    {
        #region Private Members
        private string _strInitialDirectory = METPath.WORKING_DIR;
        private Thread _tLoadFirmware = null;
        private CancellationTokenSource _cancellationToken;
        #endregion

        #region Constructor
        public frmSocRom()
        {
            InitializeComponent();

            // Attach event handlers.
            WireEventHandlers();

            // Enable drag.
            UITools.EnableFormDrag(this, tlpTitle, lblTitle);

            // Set tip handlers for controls.
            SetTipHandlers();

            // Set button properties.
            SetButtonFontAndGlyph();

            // Set label properties.
            SetLabelFontAndGlyph();
        }

        private void WireEventHandlers()
        {
            Load += frmSocRom_Load;
            FormClosing += frmSocRom_FormClosing;
            FormClosed += frmSocRom_FormClosed;
            KeyDown += frmSocRom_KeyDown;
            DragEnter += frmSocRom_DragEnter;
            DragDrop += frmSocRom_DragDrop;
            Deactivate += frmSocRom_Deactivate;
            Activated += frmSocRom_Activated;
        }
        #endregion

        #region Window Events
        private void frmSocRom_Load(object sender, EventArgs e)
        {
            SetInitialDirectory();

            _cancellationToken = new CancellationTokenSource();

            if (!string.IsNullOrEmpty(Program.MAIN_WINDOW.loadedFile))
            {
                OpenBinary(Program.MAIN_WINDOW.loadedFile);
                Program.MAIN_WINDOW.loadedFile = null;
            }
        }

        private void frmSocRom_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_cancellationToken != null && !_cancellationToken.IsCancellationRequested)
            {
                _cancellationToken.Cancel();
            }
        }

        private void frmSocRom_FormClosed(object sender, FormClosedEventArgs e) => _cancellationToken?.Dispose();

        private void frmSocRom_DragEnter(object sender, DragEventArgs e) => Program.HandleDragEnter(sender, e);

        private void frmSocRom_DragDrop(object sender, DragEventArgs e)
        {
            // Get the path of the dragged file.
            string[] draggedFiles = (string[])e.Data.GetData(DataFormats.FileDrop);
            string draggedFilename = draggedFiles[0];

            // Open the binary file.
            OpenBinary(draggedFilename);
        }

        private void frmSocRom_Deactivate(object sender, EventArgs e) => SetControlForeColor(tlpTitle, AppColours.DEACTIVATED_TEXT);

        private void frmSocRom_Activated(object sender, EventArgs e) => SetControlForeColor(tlpTitle, AppColours.WHITE_TEXT);
        #endregion

        #region KeyDown Events
        private void frmSocRom_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }

            if (e.Modifiers == Keys.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.O:
                        cmdMenuOpen.PerformClick();
                        break;
                    case Keys.R:
                        cmdMenuReset.PerformClick();
                        break;
                    case Keys.C:
                        cmdMenuCopy.PerformClick();
                        break;
                    case Keys.L:
                        cmdMenuFolders.PerformClick();
                        break;
                    case Keys.E:
                        cmdMenuExport.PerformClick();
                        break;
                    case Keys.P:
                        cmdMenuPatch.PerformClick();
                        break;
                    case Keys.T:
                        cmdMenuOptions.PerformClick();
                        break;
                    case Keys.S:
                        cbxCensor.Checked = !cbxCensor.Checked;
                        break;
                }
            }
            else if ((e.Modifiers & Keys.Control) == Keys.Control && (e.Modifiers & Keys.Shift) == Keys.Shift)
            {
                switch (e.KeyCode)
                {
                    case Keys.L:
                        cmdOpenInExplorer.PerformClick();
                        break;
                }
            }
        }
        #endregion

        #region Button Events
        private void cmdClose_Click(object sender, EventArgs e) => Close();

        private void cmdMinimize_Click(object sender, EventArgs e) => WindowState = FormWindowState.Minimized;

        private void cmdMenuOpen_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog
            {
                InitialDirectory = _strInitialDirectory,
                Filter = APPSTRINGS.FILTER_SOCROM_SUPPORTED_FIRMWARE
            })
            {
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    OpenBinary(openFileDialog.FileName);
                }
            }
        }

        private void cmdMenuReset_Click(object sender, EventArgs e)
        {
            if (Settings.ReadBool(SettingsBoolType.DisableConfDiag))
            {
                ToggleControlEnable(false);
                ResetWindow();
                return;
            }

            DialogResult result =
                METPrompt.Show(
                    this,
                    DIALOGSTRINGS.UNLOAD_FIRMWARE_RESET,
                    METPromptType.Warning,
                    METPromptButtons.YesNo);

            if (result == DialogResult.Yes)
            {
                ToggleControlEnable(false);
                ResetWindow();
            }
        }

        private void cmdMenuCopy_Click(object sender, EventArgs e) =>
            UITools.ShowContextMenuAtControlPoint(
                sender,
                cmsCopy,
                MenuPosition.BottomLeft);

        private void cmdMenuFolders_Click(object sender, EventArgs e) =>
            UITools.ShowContextMenuAtControlPoint(
                sender,
                cmsFolders,
                MenuPosition.BottomLeft);

        private void cmdMenuExport_Click(object sender, EventArgs e) =>
            UITools.ShowContextMenuAtControlPoint(
                sender,
                cmsExport,
                MenuPosition.BottomLeft);

        private void cmdMenuPatch_Click(object sender, EventArgs e)
        {
            bool bOpenEditor = Settings.ReadBool(SettingsBoolType.AcceptedEditingTerms);

            if (!bOpenEditor)
            {
                BlurHelper.ApplyBlur(this);

                using (Form frm = new frmTerms())
                {
                    frm.FormClosed += ChildWindowClosed;
                    DialogResult result = frm.ShowDialog();
                    bOpenEditor = (result != DialogResult.No);
                }
            }

            if (bOpenEditor)
            {
                UITools.ShowContextMenuAtControlPoint(
                    sender,
                    cmsPatch,
                    MenuPosition.BottomLeft);
            }
        }

        private void cmdMenuOptions_Click(object sender, EventArgs e) =>
            UITools.ShowContextMenuAtControlPoint(
                sender,
                cmsOptions,
                MenuPosition.BottomLeft);

        private void cmdOpenInExplorer_Click(object sender, EventArgs e) => UITools.HighlightPathInExplorer(SOCROM.LoadedBinaryPath, this);
        #endregion

        #region Switch Events
        private void cbxCensor_CheckedChanged(object sender, EventArgs e) => UpdateSerialControls();
        #endregion

        #region Copy Toolstrip Events
        private void filenameToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFilename(true);

        private void sizeToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFileSize();

        private void cRC32ToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFileCrc32();

        private void creationDateToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFileCreationTime();

        private void modifiedDateToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFileModifiedTime();

        private void iBootVersionToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetIbootVersion();

        private void scfgBaseAddressToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetScfgBaseAddress();

        private void scfgSizeDecimalToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetScfgSizeDecimal();

        private void scfgSizeHexToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetScfgSizeHex();

        private void scfgCRC32ToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetScfgCrc32();

        private void serialToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string serial = SOCROM.ScfgSectionData.Serial;

            if (string.IsNullOrEmpty(serial))
            {
                return;
            }

            Clipboard.SetText(serial);

            if (!Settings.ReadBool(SettingsBoolType.DisableConfDiag))
            {
                METPrompt.Show(
                    this,
                    $"{APPSTRINGS.SERIAL_NUMBER} " +
                    $"{EFISTRINGS.COPIED_TO_CB_LC}",
                    METPromptType.Information,
                    METPromptButtons.Okay);
            }
        }

        private void configToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetScfgConfig();

        private void orderNoToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetScfgOrderNo();
        #endregion

        #region Folders Toolstrip Events
        private void openBackupsFolderToolStripMenuItem_Click(object sender, EventArgs e) =>
            UITools.OpenFolderInExplorer(METPath.BACKUPS_DIR, this);

        private void openBuildsFolderToolStripMenuItem_Click(object sender, EventArgs e) =>
            UITools.OpenFolderInExplorer(METPath.BUILDS_DIR, this);

        private void openSCFGFolderToolStripMenuItem_Click(object sender, EventArgs e) =>
            UITools.OpenFolderInExplorer(METPath.SCFG_DIR, this);

        private void openWorkingDirectoryToolStripMenuItem_Click(object sender, EventArgs e) =>
            UITools.OpenFolderInExplorer(METPath.WORKING_DIR, this);
        #endregion

        #region Export Toolstrip Events
        private void exportScfgStoreToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.EnsureDirectoriesExist();

            using (SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = APPSTRINGS.FILTER_BIN,
                FileName = $"{SOCSTRINGS.SCFG}_{SOCROM.FileInfoData.FileName}",
                OverwritePrompt = true,
                InitialDirectory = METPath.SCFG_DIR
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                // Save the Scfg stores bytes to disk.
                if (FileTools.WriteAllBytesEx(dialog.FileName, SOCROM.ScfgSectionData.ScfgBytes))
                {
                    UITools.ShowExplorerFileHighlightPrompt(this, dialog.FileName);
                    return;
                }

                METPrompt.Show(
                    this,
                    DIALOGSTRINGS.SCFG_EXPORT_FAIL,
                    METPromptType.Error,
                    METPromptButtons.Okay);
            }
        }

        private void backupFirmwareZIPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.EnsureDirectoriesExist();

            using (SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = METPath.BACKUPS_DIR,
                Filter = APPSTRINGS.FILTER_ZIP,
                FileName = $"{SOCROM.FileInfoData.FileName}_{APPSTRINGS.SOCROM}_{APPSTRINGS.BACKUP}",
                OverwritePrompt = true
            })
            {
                // Action was cancelled
                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                FileTools.BackupFileToZip(SOCROM.LoadedBinaryBuffer, SOCROM.FileInfoData.FileNameExt, saveFileDialog.FileName);

                if (File.Exists(saveFileDialog.FileName))
                {
                    UITools.ShowExplorerFileHighlightPrompt(this, saveFileDialog.FileName);
                    return;
                }

                METPrompt.Show(
                    this,
                    DIALOGSTRINGS.ARCHIVE_CREATE_FAILED,
                    METPromptType.Error,
                    METPromptButtons.Okay);
            }
        }

        private void exportFirmwareInformationTextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = APPSTRINGS.FILTER_TEXT,
                FileName = $"{APPSTRINGS.FIRMWARE_INFO}_{SOCROM.FileInfoData.FileName}",
                OverwritePrompt = true,
                InitialDirectory = METPath.WORKING_DIR
            })
            {
                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                StringBuilder stringBuilder = new StringBuilder();

                stringBuilder.AppendLine("File");
                stringBuilder.AppendLine("----------------------------------");
                stringBuilder.AppendLine($"Filename:        {SOCROM.FileInfoData.FileNameExt}");
                stringBuilder.AppendLine($"Size (Bytes):    {FileTools.FormatFileSize(SOCROM.FileInfoData.Length)} bytes");
                stringBuilder.AppendLine($"Size (MB):       {Helper.GetBytesReadableSize(SOCROM.FileInfoData.Length)}");
                stringBuilder.AppendLine($"Size (Hex):      {SOCROM.FileInfoData.Length:X}h");
                stringBuilder.AppendLine($"CRC32:           {SOCROM.FileInfoData.CRC32:X}");
                stringBuilder.AppendLine($"Created:         {SOCROM.FileInfoData.CreationTime}");
                stringBuilder.AppendLine($"Modified:        {SOCROM.FileInfoData.LastWriteTime}\r\n");

                stringBuilder.AppendLine("Scfg");
                stringBuilder.AppendLine("----------------------------------");
                stringBuilder.AppendLine($"Base:            {SOCROM.ScfgSectionData.StoreBase:X}h");
                stringBuilder.AppendLine($"Size (Bytes):    {SOCROM.ScfgSectionData.StoreSize} bytes");
                stringBuilder.AppendLine($"Size (Hex):      {SOCROM.ScfgSectionData.StoreSize:X}h");
                stringBuilder.AppendLine($"CRC32:           {SOCROM.ScfgSectionData.ScfgCrc ?? APPSTRINGS.NA}");
                stringBuilder.AppendLine($"Serial:          {SOCROM.ScfgSectionData.Serial ?? APPSTRINGS.NA}\r\n");

                stringBuilder.AppendLine("Model");
                stringBuilder.AppendLine("----------------------------------");
                stringBuilder.AppendLine($"Config:          {SOCROM.ConfigCode ?? APPSTRINGS.NA}");
                stringBuilder.AppendLine($"Order No:        {SOCROM.ScfgSectionData.SON ?? APPSTRINGS.NA}");
                stringBuilder.AppendLine($"Reg No:          {SOCROM.ScfgSectionData.RegNumText ?? APPSTRINGS.NA}\r\n");

                stringBuilder.AppendLine("Firmware");
                stringBuilder.AppendLine("----------------------------------");
                stringBuilder.AppendLine($"iBoot Version:   {SOCROM.iBootVersion ?? APPSTRINGS.NA}");

                File.WriteAllText(saveFileDialog.FileName, stringBuilder.ToString());

                stringBuilder.Clear();

                if (!File.Exists(saveFileDialog.FileName))
                {
                    METPrompt.Show(
                        this,
                        DIALOGSTRINGS.DATA_EXPORT_FAILED,
                        METPromptType.Error,
                        METPromptButtons.Okay);

                    return;
                }

                UITools.ShowExplorerFileHighlightPrompt(this, saveFileDialog.FileName);
            }
        }
        #endregion

        #region Patch Toolstrip Events
        private void changeSerialNumberToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BlurHelper.ApplyBlur(this);

            using (frmSerialSelect child = new frmSerialSelect())
            {
                child.Tag = SerialSenderTag.SOCROM;
                child.FormClosed += ChildWindowClosed;
                child.ShowDialog();

                if (child.DialogResult == DialogResult.OK)
                {
                    WriteSocromSerialNumber(SOCROM.NewSerial);
                }
            }
        }

        private void replaceScfgStoreToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WriteScfgStore();
        }
        #endregion

        #region Options Toolstrip Events
        private void reloadFileFromDiskToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!File.Exists(SOCROM.LoadedBinaryPath))
            {
                METPrompt.Show(
                    this,
                    DIALOGSTRINGS.COULD_NOT_RELOAD,
                    METPromptType.Error,
                    METPromptButtons.Okay);

                return;
            }

            // Load bytes from loaded binary file.
            byte[] fileBytes = File.ReadAllBytes(SOCROM.LoadedBinaryPath);

            // Check if the binaries match in size and data.
            if (BinaryTools.ByteArraysMatch(fileBytes, SOCROM.LoadedBinaryBuffer))
            {
                // Loaded binaries match.
                METPrompt.Show(
                    this,
                    DIALOGSTRINGS.WARN_DATA_MATCHES_BUFF,
                    METPromptType.Warning,
                    METPromptButtons.Okay);

                return;
            }

            OpenBinary(SOCROM.LoadedBinaryPath);
        }

        private void viewApplicationLogToolStripMenuItem_Click(object sender, EventArgs e) =>
            Logger.OpenLogFile(this);

        private void lookupSerialNumberEveryMacToolStripMenuItem_Click(object sender, EventArgs e) =>
            MacTools.LookupSerialOnEveryMac(SOCROM.ScfgSectionData.Serial);
        #endregion

        #region Open Binary
        private void OpenBinary(string filePath)
        {
            ToggleControlEnable(false);

            if (SOCROM.FirmwareLoaded)
            {
                ResetWindow();
            }

            // Check filesize
            if (!FileTools.IsValidMinMaxSize(filePath, this))
            {
                return;
            }

            // Set the binary path and load the bytes.
            SOCROM.LoadedBinaryPath = filePath;
            SOCROM.LoadedBinaryBuffer = File.ReadAllBytes(filePath);

            // Check if the image is what we're looking for.
            if (!SOCROM.IsValidImage(SOCROM.LoadedBinaryBuffer))
            {
                METPrompt.Show(
                    this,
                    DIALOGSTRINGS.NOT_VALID_SOCROM,
                    METPromptType.Warning,
                    METPromptButtons.Okay);

                return;
            }

            // Set the current directory.
            _strInitialDirectory = Path.GetDirectoryName(filePath);

            // Show loading bar.
            pbxLoad.Image = Properties.Resources.loading;

            _tLoadFirmware = new Thread(() => LoadFirmwareBase(filePath, _cancellationToken.Token)) { IsBackground = true };
            _tLoadFirmware.Start();
        }

        private void LoadFirmwareBase(string filePath, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            SOCROM.LoadFirmwareBaseData(SOCROM.LoadedBinaryBuffer, filePath);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (IsHandleCreated && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        UpdateUI();
                    });
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                SOCROM.FirmwareLoaded = true;
            }
        }
        #endregion

        #region UI Events
        private void ChildWindowClosed(object sender, EventArgs e) => BlurHelper.RemoveBlur(this);

        private void UpdateWindowTitle()
        {
            this.Text = SOCROM.FileInfoData.FileNameExt;
            lblTitle.Text = $"{APPSTRINGS.SOCROM} {Program.GLYPH_RIGHT_ARROW} {SOCROM.FileInfoData.FileNameExt}";
        }

        private void SetTipHandlers()
        {
            Button[] buttons =
            {
                cmdMenuOpen,
                cmdMenuReset,
                cmdMenuCopy,
                cmdMenuFolders,
                cmdMenuExport,
                cmdMenuOptions,
                cmdMenuPatch,
                cmdOpenInExplorer
            };

            Label[] labels = { lblParseTime };

            CheckBox[] checkBoxes = { cbxCensor };

            foreach (Button button in buttons)
            {
                button.MouseEnter += HandleMouseEnterTip;
                button.MouseLeave += HandleMouseLeaveTip;
            }

            foreach (Label label in labels)
            {
                label.MouseEnter += HandleMouseEnterTip;
                label.MouseLeave += HandleMouseLeaveTip;
            }

            foreach (CheckBox checkBox in checkBoxes)
            {
                checkBox.MouseEnter += HandleMouseEnterTip;
                checkBox.MouseLeave += HandleMouseLeaveTip;
                checkBox.CheckedChanged += HandleCheckBoxChanged;
            }
        }

        private void HandleMouseEnterTip(object sender, EventArgs e)
        {
            if (!Settings.ReadBool(SettingsBoolType.DisableTips))
            {
                Dictionary<object, string> tooltips = new Dictionary<object, string>
                {
                    { cmdMenuOpen, $"{SOCSTRINGS.MENU_TIP_OPEN} (CTRL + O)" },
                    { cmdMenuReset, $"{SOCSTRINGS.MENU_TIP_RESET} (CTRL + R)"},
                    { cmdMenuCopy, $"{SOCSTRINGS.MENU_TIP_COPY} (CTRL + C)" },
                    { cmdMenuFolders, $"{SOCSTRINGS.MENU_TIP_FOLDERS} (CTRL + L)" },
                    { cmdMenuExport, $"{SOCSTRINGS.MENU_TIP_EXPORT} (CTRL + E)"},
                    { cmdMenuPatch, $"{SOCSTRINGS.MENU_TIP_PATCH} (CTRL + P)"},
                    { cmdMenuOptions, $"{SOCSTRINGS.MENU_TIP_OPTIONS} (CTRL + T)"},
                    { cmdOpenInExplorer, $"{SOCSTRINGS.MENU_TIP_OPENFILELOCATION} (CTRL + SHIFT + L)"},
                    { lblParseTime, APPSTRINGS.FW_PARSE_TIME},
                    { cbxCensor, cbxCensorTipString() }
                };

                if (tooltips.TryGetValue(sender, out string value))
                {
                    lblStatusBarTip.Text = value;
                }
            }
        }

        private string cbxCensorTipString() =>
            $"{(cbxCensor.Checked ? APPSTRINGS.HIDE : APPSTRINGS.SHOW)} {APPSTRINGS.SERIAL_NUMBER} (CTRL + S)";

        private void HandleCheckBoxChanged(object sender, EventArgs e)
        {
            if (sender == cbxCensor && cbxCensor.ClientRectangle.Contains(cbxCensor.PointToClient(Cursor.Position)))
            {
                lblStatusBarTip.Text = cbxCensorTipString();
            }
        }

        private void HandleMouseLeaveTip(object sender, EventArgs e) => lblStatusBarTip.Text = string.Empty;

        private void SetButtonFontAndGlyph()
        {
            var buttons = new[]
            {
                new { Button = cmdClose, Font = Program.FONT_MDL2_REG_12, Text = Program.GLYPH_EXIT_CROSS },
                new { Button = cmdOpenInExplorer, Font = Program.FONT_MDL2_REG_10, Text = Program.GLYPH_FILE_EXPLORER },
            };

            foreach (var buttonData in buttons)
            {
                buttonData.Button.Font = buttonData.Font;
                buttonData.Button.Text = buttonData.Text;
            }
        }

        private void SetLabelFontAndGlyph()
        {
            lblView.Font = Program.FONT_MDL2_REG_10;
            lblView.Text = Program.GLYPH_VIEW;
        }

        private static void SetControlForeColor(Control parentControl, Color foreColor)
        {
            foreach (Control ctrl in parentControl.Controls)
            {
                ctrl.ForeColor = foreColor;
            }
        }

        private void NotifyPatchingFailure()
        {
            if (Prompts.ShowPatchFailedPrompt(this) == DialogResult.Yes)
            {
                Logger.OpenLogFile(this);
            }
        }
        #endregion

        #region Misc Events
        internal void SetInitialDirectory()
        {
            // Get the initial directory from settings.
            string directory = Settings.ReadString(SettingsStringType.SocInitialDirectory);

            // If the path is not empty check if it exists and set it as the initial directory.
            if (!string.IsNullOrEmpty(directory))
            {
                _strInitialDirectory = Directory.Exists(directory) ? directory : METPath.WORKING_DIR;
            }
        }

        private void ResetWindow()
        {
            // Reset censor switch.
            cbxCensor.Checked = false;
            cbxCensor.Enabled = false;

            // Reset labels.
            Label[] labels =
            {
                lblFilename,
                lblFilesize,
                lblCrc,
                lblCreated,
                lblModified,
                lbliBoot,
                lblScfg,
                lblSerial,
                lblConfigCode,
                lblSon
            };

            foreach (Label label in labels)
            {
                label.Text = string.Empty;
                label.ForeColor = Color.White;
            }

            // Reset parse time.
            lblParseTime.Text = "0.00s";

            // Reset initial directory.
            SetInitialDirectory();

            // Reset window text.
            Text = APPSTRINGS.SOCROM;
            lblTitle.Text = APPSTRINGS.SOCROM;

            SOCROM.ResetFirmwareBaseData();
        }

        private void ToggleControlEnable(bool enable)
        {
            Button[] standardButtons =
            {
                cmdMenuReset,
                cmdMenuCopy,
                cmdMenuFolders,
                cmdMenuExport,
                cmdMenuPatch,
                cmdMenuOptions,
                cmdOpenInExplorer
            };

            void EnableButtons(params Button[] buttons)
            {
                foreach (var button in buttons)
                {
                    button.Enabled = enable;
                }
            }

            if (!enable)
            {
                EnableButtons(standardButtons);
            }
            else
            {
                EnableButtons(standardButtons);

                exportScfgStoreToolStripMenuItem.Enabled = SOCROM.ScfgSectionData.StoreBase != -1;
                lookupSerialNumberEveryMacToolStripMenuItem.Enabled = !string.IsNullOrEmpty(SOCROM.ScfgSectionData.Serial);

                changeSerialNumberToolStripMenuItem.Enabled = SOCROM.ScfgSectionData.StoreBase != -1;
            }

            tlpFirmware.Enabled = enable;
        }
        #endregion

        #region Update Window
        internal void UpdateUI()
        {
            // Parse time.
            UpdateParseTimeControls();

            // File information
            UpdateFilenameControls();
            UpdateFileSizeControls();
            UpdateFileCrc32Controls();
            UpdateFileCreationDateControls();
            UpdateFileModifiedDateControls();

            // Firmware data
            UpdateIbootControls();
            UpdateScfgControls();
            UpdateSerialControls();
            UpdateConfigCodeControls();
            UpdateModelControls();

            // Apply DISABLED_TEXT to N/A labels.
            UITools.ApplyNestedPanelLabelForeColor(tlpFirmware, AppColours.DISABLED_TEXT);

            // Update window title.
            UpdateWindowTitle();

            // Unload image.
            pbxLoad.Image = null;

            // Check and set control enable
            ToggleControlEnable(true);
        }

        private void UpdateParseTimeControls() => lblParseTime.Text = $"{SOCROM.tsParseTime.TotalSeconds:F2}s";

        private void UpdateFilenameControls() => lblFilename.Text = $"{APPSTRINGS.FILE}: '{SOCROM.FileInfoData.FileNameExt}'";

        private void UpdateFileSizeControls()
        {
            int fsDecimal = SOCROM.FileInfoData.Length;

            bool isValidSize = FileTools.GetIsValidBinSize(fsDecimal);

            lblFilesize.Text = $"{FileTools.FormatFileSize(fsDecimal)} {APPSTRINGS.BYTES} ({fsDecimal:X}h)";

            if (!isValidSize)
            {
                lblFilesize.ForeColor = AppColours.ERROR;
                lblFilesize.Text += $" ({FileTools.GetSizeDifference(fsDecimal)})";
            }
        }

        private void UpdateFileCrc32Controls() => lblCrc.Text = $"{SOCROM.FileInfoData.CRC32:X8}";

        private void UpdateFileCreationDateControls() => lblCreated.Text = SOCROM.FileInfoData.CreationTime;

        private void UpdateFileModifiedDateControls() => lblModified.Text = SOCROM.FileInfoData.LastWriteTime;

        private void UpdateIbootControls()
        {
            if (!string.IsNullOrEmpty(SOCROM.iBootVersion))
            {
                lbliBoot.Text = SOCROM.iBootVersion;

                iBootVersionToolStripMenuItem.Enabled = true;

                return;
            }

            iBootVersionToolStripMenuItem.Enabled = false;
            lbliBoot.Text = APPSTRINGS.NA;
        }

        private void UpdateScfgControls()
        {
            if (SOCROM.ScfgSectionData.StoreBase == -1)
            {
                DisableScfgMenuItems();
                lblScfg.Text = APPSTRINGS.NA;
                return;
            }

            string scfgBase = $"{SOCROM.ScfgSectionData.StoreBase:X}h";
            string crc = SOCROM.ScfgSectionData.ScfgCrc;
            int scfgSize = SOCROM.ScfgSectionData.StoreSize;

            lblScfg.Text = $"{scfgBase}, {scfgSize:X}h ({scfgSize} bytes), {crc}";

            EnableScfgMenuItems();
        }

        private void DisableScfgMenuItems()
        {
            scfgBaseAddressToolStripMenuItem.Enabled = false;
            scfgSizeDecimalToolStripMenuItem.Enabled = false;
            scfgSizeHexToolStripMenuItem.Enabled = false;
            scfgCRC32ToolStripMenuItem.Enabled = false;
        }

        private void EnableScfgMenuItems()
        {
            scfgBaseAddressToolStripMenuItem.Enabled = true;
            scfgSizeDecimalToolStripMenuItem.Enabled = true;
            scfgSizeHexToolStripMenuItem.Enabled = true;
            scfgCRC32ToolStripMenuItem.Enabled = true;
        }

        private void UpdateSerialControls()
        {
            string serialNumber = SOCROM.ScfgSectionData.Serial;

            if (!string.IsNullOrEmpty(serialNumber))
            {
                if (cbxCensor.Checked)
                {
                    lblSerial.Text = serialNumber;
                }
                else
                {
                    lblSerial.Text = $"{serialNumber.Substring(0, 2)}******{serialNumber.Substring(8, 4)}";
                }

                // Prototype in testing
                if (!Serial.IsValid(serialNumber))
                {
                    lblSerial.ForeColor = AppColours.WARNING;
                }

                cbxCensor.Enabled = true;
                serialToolStripMenuItem.Enabled = true;
            }
            else
            {
                lblSerial.Text = APPSTRINGS.NA;
                cbxCensor.Enabled = false;
                serialToolStripMenuItem.Enabled = false;
            }
        }

        private void UpdateConfigCodeControls()
        {

            if (!string.IsNullOrEmpty(SOCROM.ConfigCode))
            {
                lblConfigCode.Text = SOCROM.ConfigCode;

                configToolStripMenuItem.Enabled = true;

                return;
            }

            lblConfigCode.Text = APPSTRINGS.CONTACT_SERVER;
            lblConfigCode.ForeColor = Colours.INFO_BOX;

            GetConfigCodeAsync(SOCROM.ConfigCode);
        }

        internal async void GetConfigCodeAsync(string hwc)
        {
            string configcode = await MacTools.GetDeviceConfigCodeSupportRemote(hwc);

            if (!string.IsNullOrEmpty(configcode))
            {
                SOCROM.ConfigCode = configcode;

                lblConfigCode.Text = configcode;
                lblConfigCode.ForeColor = AppColours.NORMAL_INFO_TEXT;

                configToolStripMenuItem.Enabled = true;

                return;
            }

            configToolStripMenuItem.Enabled = false;

            lblConfigCode.Text = APPSTRINGS.NA;
            lblConfigCode.ForeColor = Colours.CONTROL_DISABLED_TEXT;
        }

        private void UpdateModelControls()
        {
            if (string.IsNullOrEmpty(SOCROM.ScfgSectionData.SON))
            {
                lblSon.Text = APPSTRINGS.NA;

                orderNoToolStripMenuItem.Enabled = false;

                return;
            }

            orderNoToolStripMenuItem.Enabled = true;

            lblSon.Text = SOCROM.ScfgSectionData.SON;

            if (!string.IsNullOrEmpty(SOCROM.ScfgSectionData.RegNumText))
            {
                lblSon.Text += SOCROM.ScfgSectionData.RegNumText;
            }
        }
        #endregion

        #region Clipboard
        internal void SetClipboardText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Clipboard.SetText(text);

            if (!Settings.ReadBool(SettingsBoolType.DisableConfDiag))
            {
                METPrompt.Show(
                    this,
                    $"'{text}' {EFISTRINGS.COPIED_TO_CB_LC}",
                    METPromptType.Information,
                    METPromptButtons.Okay);
            }
        }

        private void ClipboardSetFilename(bool showExtention) =>
            SetClipboardText(showExtention ? SOCROM.FileInfoData.FileNameExt : SOCROM.FileInfoData.FileName);

        private void ClipboardSetFileSize() =>
            SetClipboardText($"{FileTools.FormatFileSize(SOCROM.FileInfoData.Length)} {APPSTRINGS.BYTES} ({SOCROM.FileInfoData.Length:X}h)");

        private void ClipboardSetFileCrc32() => SetClipboardText($"{SOCROM.FileInfoData.CRC32:X8}");

        private void ClipboardSetFileCreationTime() => SetClipboardText(SOCROM.FileInfoData.CreationTime);

        private void ClipboardSetFileModifiedTime() => SetClipboardText(SOCROM.FileInfoData.LastWriteTime);

        private void ClipboardSetIbootVersion() => SetClipboardText(SOCROM.iBootVersion);

        private void ClipboardSetScfgBaseAddress() => SetClipboardText($"{SOCROM.ScfgSectionData.StoreBase:X}");

        private void ClipboardSetScfgSizeDecimal() => SetClipboardText($"{SOCROM.ScfgSectionData.StoreSize} {APPSTRINGS.BYTES}");

        private void ClipboardSetScfgSizeHex() => SetClipboardText($"{SOCROM.ScfgSectionData.StoreSize:X}h");

        private void ClipboardSetScfgCrc32() => SetClipboardText(SOCROM.ScfgSectionData.ScfgCrc);

        private void ClipboardSetScfgConfig() => SetClipboardText(SOCROM.ConfigCode);

        private void ClipboardSetScfgOrderNo() => SetClipboardText($"{SOCROM.ScfgSectionData.SON}{SOCROM.ScfgSectionData.RegNumText ?? string.Empty}");
        #endregion

        #region Write Serial
        private void WriteSocromSerialNumber(string serial)
        {
            Logger.WritePatchLine(LOGSTRINGS.PATCH_START);

            // Check serial length.
            if (serial.Length != SOCROM.SERIAL_LEN)
            {
                Logger.WritePatchLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.SERIAL_LEN_INVALID} ({serial.Length})");
                NotifyPatchingFailure();
                return;
            }

            // Check if the SerialBase exists.
            if (SOCROM.ScfgSectionData.SerialBase == -1)
            {
                Logger.WritePatchLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.SSN_BASE_NOT_FOUND}");
                NotifyPatchingFailure();
                return;
            }

            // Create buffers.
            Logger.WritePatchLine(LOGSTRINGS.CREATING_BUFFERS);

            byte[] binaryBuffer = SOCROM.LoadedBinaryBuffer;
            byte[] newSerialBytes = Encoding.UTF8.GetBytes(serial);

            // Overwrite serial in the binary buffer.
            Logger.WritePatchLine(LOGSTRINGS.SSN_WTB);

            BinaryTools.OverwriteBytesAtBase(binaryBuffer, SOCROM.ScfgSectionData.SerialBase, newSerialBytes);

            Logger.WritePatchLine(LOGSTRINGS.SCFG_LFB);

            // Load patched scfg from the binary buffer.
            ScfgStore scfgStoreFromBuffer = SOCROM.GetSCfgData(binaryBuffer, false);

            // Verify the serial was written correctly.
            if (!string.Equals(serial, scfgStoreFromBuffer.Serial))
            {
                Logger.WritePatchLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.SSN_NOT_WRITTEN}");
                NotifyPatchingFailure();
                return;
            }

            Logger.WritePatchLine(LOGSTRINGS.SSN_WRITE_SUCCESS);

            // Log success and prompt for saving the patched firmware.
            Logger.WritePatchLine(LOGSTRINGS.PATCH_SUCCESS);

            if (Prompts.ShowPathSuccessPrompt(this) == DialogResult.Yes)
            {
                SaveOutputFirmwareSocrom(binaryBuffer);
                return;
            }

            Logger.WritePatchLine(LOGSTRINGS.FILE_EXPORT_CANCELLED);
        }
        #endregion

        #region Write Scfg Store
        private void WriteScfgStore()
        {
            Logger.WritePatchLine(LOGSTRINGS.PATCH_START);

            using (OpenFileDialog openFileDialog = CreateScfgOpenFileDialog())
            {
                if (openFileDialog.ShowDialog() != DialogResult.OK)
                {
                    Logger.WritePatchLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.SCFG_IMPORT_CANCELLED}");
                    return;
                }
                // Check the SOCROM contains a store, otherwise set the base address.
                int scfgBase = SOCROM.ScfgSectionData.StoreBase;

                bool bScfgFound = true;

                // Set the Scfg base manually.
                if (scfgBase == -1)
                {
                    bScfgFound = false;
                    Logger.WritePatchLine($"{LOGSTRINGS.SCFG_BASE_ADJUST} {SOCROM.SCFG_EXPECTED_BASE:X}h");
                    scfgBase = SOCROM.SCFG_EXPECTED_BASE;
                }

                Logger.WritePatchLine(LOGSTRINGS.CREATING_BUFFERS);

                byte[] scfgBuffer = File.ReadAllBytes(openFileDialog.FileName);
                byte[] binaryBuffer = SOCROM.LoadedBinaryBuffer;

                if (!ValidateScfgStore(scfgBuffer))
                {
                    return;
                }

                // Check were not writing over data we shouldn't be.
                if (!bScfgFound)
                {
                    byte[] writeBuffer = BinaryTools.GetBytesBaseLength(binaryBuffer, SOCROM.SCFG_EXPECTED_BASE, SOCROM.SCFG_EXPECTED_LEN);

                    for (int i = 0; i < writeBuffer.Length; i++)
                    {
                        if (writeBuffer[i] != 0xFF)
                        {
                            Logger.WritePatchLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.SCFG_POS_INITIALIZED}");
                            NotifyPatchingFailure();
                            return;
                        }
                    }
                }

                Logger.Write(LOGSTRINGS.WRITE_NEW_DATA, LogType.Application);

                // Overwrite Scfg store in the binary buffer.
                BinaryTools.OverwriteBytesAtBase(binaryBuffer, scfgBase, scfgBuffer);

                // Load Scfg store from the binary buffer.
                ScfgStore scfgTempStore = SOCROM.GetSCfgData(binaryBuffer, false);

                // Check store was written successfully.
                if (!BinaryTools.ByteArraysMatch(scfgTempStore.ScfgBytes, scfgBuffer))
                {
                    Logger.WritePatchLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.STORE_COMP_FAILED}");
                    NotifyPatchingFailure();
                    return;
                }

                Logger.WritePatchLine(LOGSTRINGS.PATCH_SUCCESS);

                if (Prompts.ShowPathSuccessPrompt(this) == DialogResult.Yes)
                {
                    SaveOutputFirmwareSocrom(binaryBuffer);
                    return;
                }

                Logger.WritePatchLine(LOGSTRINGS.FILE_EXPORT_CANCELLED);
            }
        }

        private static OpenFileDialog CreateScfgOpenFileDialog()
        {
            return new OpenFileDialog
            {
                InitialDirectory = METPath.SCFG_DIR,
                Filter = APPSTRINGS.FILTER_BIN
            };
        }

        private bool ValidateScfgStore(byte[] scfgBuffer)
        {
            int scfgBase = BinaryTools.GetBaseAddress(scfgBuffer, SOCROM.SCFG_HEADER_SIG);

            // A serialized Scfg store should be B8h, 184 bytes length.
            if (scfgBuffer.Length != SOCROM.SCFG_EXPECTED_LEN)
            {
                Logger.WritePatchLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.EXPECTED_STORE_SIZE_NOT} {SOCROM.SCFG_EXPECTED_LEN:X}h ({scfgBuffer.Length:X}h)");

                NotifyPatchingFailure();
                return false;
            }

            // Expect scfg signature at address 0h.
            if (scfgBase != 0)
            {
                Logger.WritePatchLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.STORE_SIG_MISALIGNED}");
                NotifyPatchingFailure();
                return false;
            }

            Logger.WritePatchLine(LOGSTRINGS.VALIDATION_PASS);

            return true;
        }
        #endregion

        #region Patching IO
        private static SaveFileDialog CreateFirmwareSaveFileDialog()
        {
            Program.EnsureDirectoriesExist();

            return new SaveFileDialog
            {
                Filter = APPSTRINGS.FILTER_BIN,
                FileName = SOCROM.FileInfoData.FileName,
                OverwritePrompt = true,
                InitialDirectory = METPath.BUILDS_DIR
            };
        }

        private void SaveOutputFirmwareSocrom(byte[] binaryBuffer)
        {
            using (SaveFileDialog saveFileDialog = CreateFirmwareSaveFileDialog())
            {
                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                {
                    Logger.WritePatchLine(LOGSTRINGS.FILE_EXPORT_CANCELLED);
                    return;
                }

                if (FileTools.WriteAllBytesEx(saveFileDialog.FileName, binaryBuffer) && File.Exists(saveFileDialog.FileName))
                {
                    Logger.WritePatchLine($"{LOGSTRINGS.FILE_SAVE_SUCCESS} {saveFileDialog.FileName}");

                    DialogResult result =
                        METPrompt.Show(
                            this,
                            DIALOGSTRINGS.FW_SAVED_SUCCESS_LOAD,
                            METPromptType.Question,
                            METPromptButtons.YesNo);

                    if (result == DialogResult.Yes)
                    {
                        OpenBinary(saveFileDialog.FileName);
                    }
                }
            }
        }
        #endregion
    }
}