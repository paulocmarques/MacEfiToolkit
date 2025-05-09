﻿// Mac EFI Toolkit
// https://github.com/MuertoGB/MacEfiToolkit

// Windows Forms
// frmEfiRom.cs
// Released under the GNU GLP v3.0

using Mac_EFI_Toolkit.Common;
using Mac_EFI_Toolkit.Common.Constants;
using Mac_EFI_Toolkit.Firmware;
using Mac_EFI_Toolkit.Firmware.EFIROM;
using Mac_EFI_Toolkit.Tools;
using Mac_EFI_Toolkit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Mac_EFI_Toolkit.Forms
{
    public partial class frmEfiRom : FormEx
    {
        #region Private Members
        private string _strInitialDirectory = ApplicationPaths.WorkingDirectory;
        private Thread _tLoadFirmware = null;
        private CancellationTokenSource _cancellationToken;
        private Button[] _menuButtons;
        private ContextMenuStrip[] _contextMenus;
        #endregion

        #region Constructor
        public frmEfiRom()
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

            // Get and disable menu items.
            GetAndDisableMenuItems();
        }

        private void WireEventHandlers()
        {
            Load += frmEfiRom_Load;
            FormClosing += frmEfiRom_FormClosing;
            FormClosed += frmEfiRom_FormClosed;
            KeyDown += frmEfiRom_KeyDown;
            DragEnter += frmEfiRom_DragEnter;
            DragDrop += frmEfiRom_DragDrop;
            Deactivate += frmEfiRom_Deactivate;
            Activated += frmEfiRom_Activated;
            lblFmmEmail.MouseEnter += lblFmmEmail_MouseEnter;
        }

        private void GetAndDisableMenuItems()
        {
            _menuButtons = new Button[]
            {
                cmdMenuCopy,
                cmdMenuExport,
                cmdMenuPatch,
                cmdMenuTools,
                cmdOpenInExplorer,
            };

            _contextMenus = new ContextMenuStrip[]
            {
                cmsCopy, cmsExport, cmsPatch, cmsTools
            };

            EnableButtons(false, _menuButtons);
            EnableMenus(false, _contextMenus);
        }
        #endregion

        #region Window Events
        private void frmEfiRom_Load(object sender, EventArgs e)
        {
            // Get and set the primary file dialog initial directory.
            SetInitialDirectory();

            _cancellationToken = new CancellationTokenSource();

            if (!string.IsNullOrEmpty(Program.MainWindow.LoadedFirmware))
            {
                OpenBinary(Program.MainWindow.LoadedFirmware);
                Program.MainWindow.LoadedFirmware = null;
            }

            MemoryTracker.Instance.OnMemoryUsageUpdated += MemoryTracker_OnMemoryUsageUpdated;
        }

        private void MemoryTracker_OnMemoryUsageUpdated(object sender, ulong pagefileUsage)
        {
            Invoke(new Action(() =>
            {
                if (!lblPagefile.Visible)
                {
                    lblPagefile.Visible = true;
                }

                lblPagefile.Text = FileTools.FormatBytesToReadableUnit(pagefileUsage);
            }));
        }

        private void frmEfiRom_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_cancellationToken != null && !_cancellationToken.IsCancellationRequested)
            {
                _cancellationToken.Cancel();
            }

            MemoryTracker.Instance.OnMemoryUsageUpdated -= MemoryTracker_OnMemoryUsageUpdated;

            FlashDescriptor.ClearRegionData();
            EFIROM.ResetFirmwareBaseData();
        }

        private void frmEfiRom_FormClosed(object sender, FormClosedEventArgs e) => _cancellationToken?.Dispose();

        private void frmEfiRom_DragEnter(object sender, DragEventArgs e) => Program.HandleDragEnter(sender, e);

        private void frmEfiRom_DragDrop(object sender, DragEventArgs e)
        {
            // Get the path of the dragged file.
            string[] arrDraggedFiles = (string[])e.Data.GetData(DataFormats.FileDrop);
            string strFileName = arrDraggedFiles[0];

            // Open the binary file.
            OpenBinary(strFileName);
        }

        private void frmEfiRom_Deactivate(object sender, EventArgs e) => SetControlForeColor(tlpTitle, Colours.ClrInactiveFormText);

        private void frmEfiRom_Activated(object sender, EventArgs e) => SetControlForeColor(tlpTitle, Colours.ClrActiveFormText);
        #endregion

        #region KeyDown Events
        private void frmEfiRom_KeyDown(object sender, KeyEventArgs e)
        {
            // Check if the Alt key is pressed
            if (e.Alt && e.KeyCode == Keys.F4)
            {
                // Let the system handle Alt+F4 to close the window
                e.Handled = false;
                return;
            }

            // Handle individual keys (F12, ESC) without modifiers.
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    Close();
                    break;
                case Keys.F1:
                    manualToolStripMenuItem.PerformClick();
                    break;
                case Keys.F4:
                    settingsToolStripMenuItem.PerformClick();
                    break;
                case Keys.F5:
                    reloadFileFromDiskToolStripMenuItem.PerformClick();
                    break;
                case Keys.F12:
                    viewApplicationLogToolStripMenuItem.PerformClick();
                    break;
            }

            // Handle control key + other combinations.
            if (e.Modifiers == Keys.Control)
            {
                switch (e.KeyCode)
                {
                    // Main menu.
                    case Keys.O:
                        cmdMenuOpen.PerformClick();
                        break;
                    case Keys.L:
                        cmdMenuFolders.PerformClick();
                        break;
                    case Keys.C:
                        cmdMenuCopy.PerformClick();
                        break;
                    case Keys.E:
                        cmdMenuExport.PerformClick();
                        break;
                    case Keys.P:
                        cmdMenuPatch.PerformClick();
                        break;
                    case Keys.T:
                        cmdMenuTools.PerformClick();
                        break;
                    case Keys.H:
                        cmdMenuHelp.PerformClick();
                        break;

                    // Tools menu.
                    case Keys.I:
                        viewRomInformationToolStripMenuItem.PerformClick();
                        break;
                    case Keys.R:
                        resetWindowToolStripMenuItem.PerformClick();
                        break;
                }
            }
            // Handle control key + shift key + other combinations.
            else if ((e.Modifiers & Keys.Control) == Keys.Control && (e.Modifiers & Keys.Shift) == Keys.Shift)
            {
                switch (e.KeyCode)
                {
                    case Keys.L:
                        cmdOpenInExplorer.PerformClick();
                        break;
                    case Keys.N:
                        cbxCensor.Checked = !cbxCensor.Checked;
                        break;
                }
            }
        }
        #endregion

        #region Button Events
        private void cmdClose_Click(object sender, EventArgs e) => Close();

        private void cmdMinimize_Click(object sender, EventArgs e) => WindowState = FormWindowState.Minimized;

        private void cmdOpen_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog
            {
                InitialDirectory = _strInitialDirectory,
                Filter = APPSTRINGS.FILTER_EFI_SUPPORTED_FIRMWARE
            })
            {
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    OpenBinary(openFileDialog.FileName);
                }
            }
        }

        private void cmdCopyMenu_Click(object sender, EventArgs e) =>
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

                using (Form form = new frmTerms())
                {
                    form.FormClosed += ChildWindowClosed;
                    DialogResult dlgResult = form.ShowDialog();
                    bOpenEditor = (dlgResult != DialogResult.No);
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

        private void cmdMenuTools_Click(object sender, EventArgs e) =>
            UITools.ShowContextMenuAtControlPoint(
                sender,
                cmsTools,
                MenuPosition.BottomLeft);

        private void cmdMenuHelp_Click(object sender, EventArgs e) =>
            UITools.ShowContextMenuAtControlPoint(
                sender,
                cmsHelp,
                MenuPosition.BottomLeft);


        private void cmdOpenInExplorer_Click(object sender, EventArgs e) => UITools.HighlightPathInExplorer(EFIROM.LoadedBinaryPath, this);
        #endregion

        #region Switch Events
        private void cbxCensor_CheckedChanged(object sender, EventArgs e) => UpdateSerialNumberControls();
        #endregion

        #region Label Events
        private void lblFmmEmail_MouseEnter(object sender, EventArgs e)
        {
            Label label = (Label)sender;

            if (EFIROM.FmmEmail == null)
            {
                label.Cursor = Cursors.Default;
                return;
            }

            label.Cursor = Cursors.Hand;
        }

        private void lblFmmEmail_Click(object sender, EventArgs e)
        {
            if (EFIROM.FmmEmail != null)
            {
                METPrompt.Show(this, EFIROM.FmmEmail, METPromptType.Information, METPromptButtons.Okay);
                return;
            }
        }
        #endregion

        #region Copy Toolstrip Events
        // File Clipboard Menu.
        private void filenameToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFilename(true);

        private void sizeBytesToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFileSize();

        private void crc32ToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFileCrc32();

        private void createdDateToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFileCreationTime();

        private void modifiedDateToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFileModifiedTime();

        // Firmware Clipboard Menu.
        private void modelToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFirmwareModel();

        private void configurationToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFirmwareConfigCode();

        private void fsysBaseAddressToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFsysBaseAddress();

        private void fsysCRC32ToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFirmwareFsysCrc32();

        private void serialToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string strSerial = EFIROM.FsysStoreData.Serial;

            if (string.IsNullOrEmpty(strSerial))
            {
                return;
            }

            Clipboard.SetText(strSerial);

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

        private void hwcToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFirmwareHwc();

        private void orderNoToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFirmwareOrderNumber();

        private void efiVersionToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFirmwareVersion();

        private void vSSBaseAddressToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetVssBaseAddress();

        private void sVSBaseAddressToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetSvsBaseAddress();

        private void boardIDToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFirmwareBoardId();

        private void fitVersionToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFirmwareFitVersion();

        private void meVersionToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetFirmwareMeVersion();

        private void pdrBaseToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetPdrRegionOffsets();

        private void meBaseToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetMeRegionOffsets();

        private void biosBaseToolStripMenuItem_Click(object sender, EventArgs e) =>
            ClipboardSetBiosRegionOffsets();
        #endregion

        #region Folders Context Menu Events
        private void openBackupsFolderToolStripMenuItem_Click(object sender, EventArgs e) =>
            UITools.OpenFolderInExplorer(ApplicationPaths.BackupsDirectory, this);

        private void openBuildsFolderToolStripMenuItem_Click(object sender, EventArgs e) =>
            UITools.OpenFolderInExplorer(ApplicationPaths.BuildsDirectory, this);

        private void openFsysStoresFolderToolStripMenuItem_Click(object sender, EventArgs e) =>
            UITools.OpenFolderInExplorer(ApplicationPaths.FsysDirectory, this);

        private void openIntelMEFolderToolStripMenuItem_Click(object sender, EventArgs e) =>
            UITools.OpenFolderInExplorer(ApplicationPaths.IntelMeDirectory, this);

        private void openNVRAMFolderToolStripMenuItem_Click(object sender, EventArgs e) =>
            UITools.OpenFolderInExplorer(ApplicationPaths.NvramDirectory, this);

        private void openLZMADXEFolderToolStripMenuItem_Click(object sender, EventArgs e) =>
            UITools.OpenFolderInExplorer(ApplicationPaths.LzmaDirectory, this);

        private void openWorkingDirectoryToolStripMenuItem_Click(object sender, EventArgs e) =>
            UITools.OpenFolderInExplorer(ApplicationPaths.WorkingDirectory, this);
        #endregion

        #region Export Toolstrip Events
        private void exportFsysStoreToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.EnsureDirectoriesExist();

            using (SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = APPSTRINGS.FILTER_BIN,
                FileName = $"{EFIROM.FileInfoData.FileName}_{EFISTRINGS.FSYS_REGION}",
                OverwritePrompt = true,
                InitialDirectory = ApplicationPaths.FsysDirectory
            })
            {
                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                // Save the Fsys stores bytes to disk.
                if (FileTools.WriteAllBytesEx(saveFileDialog.FileName, EFIROM.FsysStoreData.FsysBytes) && File.Exists(saveFileDialog.FileName))
                {
                    UITools.ShowExplorerFileHighlightPrompt(this, saveFileDialog.FileName);
                    return;
                }

                METPrompt.Show(
                    this,
                    DIALOGSTRINGS.FSYS_EXPORT_FAIL,
                    METPromptType.Error,
                    METPromptButtons.Okay);
            }
        }

        private void exportIntelMERegionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (FlashDescriptor.MeBase == 0 || FlashDescriptor.MeLimit == 0)
            {
                METPrompt.Show(
                    this,
                    DIALOGSTRINGS.IME_BASE_LIM_NOT_FOUND,
                    METPromptType.Error,
                    METPromptButtons.Okay);

                return;
            }

            Program.EnsureDirectoriesExist();

            // Set SaveFileDialog params.
            using (SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = APPSTRINGS.FILTER_BIN,
                FileName = $"{EFIROM.FileInfoData.FileName}_{EFISTRINGS.ME_REGION}",
                OverwritePrompt = true,
                InitialDirectory = ApplicationPaths.IntelMeDirectory
            })
            {
                // Action was cancelled.
                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                byte[] bMeBuffer = BinaryTools.GetBytesBaseLength(EFIROM.LoadedBinaryBuffer, (int)FlashDescriptor.MeBase, (int)FlashDescriptor.MeSize);

                if (FileTools.WriteAllBytesEx(saveFileDialog.FileName, bMeBuffer) && File.Exists(saveFileDialog.FileName))
                {
                    UITools.ShowExplorerFileHighlightPrompt(this, saveFileDialog.FileName);
                    return;
                }

                METPrompt.Show(
                    this,
                    DIALOGSTRINGS.IME_EXPORT_FAIL,
                    METPromptType.Error,
                    METPromptButtons.Okay);
            }
        }

        private void exportNVRAMVSSStoresToolStripMenuItem_Click(object sender, EventArgs e) =>
            ExportNVRAM(EFIROM.VssPrimary, EFIROM.VssSecondary, NvramStoreType.Variable, this);

        private void exportNVRAMSVSStoresToolStripMenuItem_Click(object sender, EventArgs e) =>
            ExportNVRAM(EFIROM.SvsPrimary, EFIROM.SvsSecondary, NvramStoreType.Secure, this);

        internal static void ExportNVRAM(NvramStore nvramprimary, NvramStore nvramsecondary, NvramStoreType nvramstoretype, Form owner)
        {
            Program.EnsureDirectoriesExist();

            switch (nvramstoretype)
            {
                case NvramStoreType.Variable:
                    SaveFilesInFolder(nvramprimary.StoreBuffer, nvramsecondary.StoreBuffer, "VSS", owner);
                    break;
                case NvramStoreType.Secure:
                    SaveFilesInFolder(nvramprimary.StoreBuffer, nvramsecondary.StoreBuffer, "SVS", owner);
                    break;
            }
        }

        private static void SaveFilesInFolder(byte[] primarystore, byte[] secondarystore, string nvramstoretype, Form owner)
        {
            using (FolderBrowserDialog fbDialog = new FolderBrowserDialog
            {
                Description = APPSTRINGS.SELECT_FOLDER,
                SelectedPath = ApplicationPaths.NvramDirectory
            })
            {
                if (fbDialog.ShowDialog() == DialogResult.OK)
                {
                    string folderPath = Path.Combine(fbDialog.SelectedPath, $"{nvramstoretype}_{EFIROM.FileInfoData.FileName}");

                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    if (primarystore != null) SaveFile(Path.Combine(folderPath, $"{nvramstoretype}_{EFISTRINGS.PRIMARY_REGION}_{EFIROM.FileInfoData.FileName}.bin"), primarystore);
                    if (secondarystore != null) SaveFile(Path.Combine(folderPath, $"{nvramstoretype}_{EFISTRINGS.BACKUP_REGION}_{EFIROM.FileInfoData.FileName}.bin"), secondarystore);

                    UITools.ShowOpenFolderInExplorerPrompt(owner, folderPath);
                }
            }
        }

        private static void SaveFile(string filepath, byte[] buffer) => File.WriteAllBytes(filepath, buffer);

        private void exportLZMADXEArchiveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.EnsureDirectoriesExist();

            using (SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = APPSTRINGS.FILTER_LZMA,
                FileName = $"{EFIROM.FileInfoData.FileName}_{EFISTRINGS.DXE_ARCHIVE}",
                OverwritePrompt = true,
                InitialDirectory = ApplicationPaths.LzmaDirectory
            })
            {
                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                if (FileTools.WriteAllBytesEx(saveFileDialog.FileName, EFIROM.LzmaDecompressedBuffer) && File.Exists(saveFileDialog.FileName))
                {
                    UITools.ShowExplorerFileHighlightPrompt(this, saveFileDialog.FileName);
                    return;
                }

                METPrompt.Show(
                    this,
                    DIALOGSTRINGS.ARCHIVE_EXPORT_FAIL,
                    METPromptType.Error,
                    METPromptButtons.Okay);
            }
        }

        private void backupFirmwareZIPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.EnsureDirectoriesExist();

            using (SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = ApplicationPaths.BackupsDirectory,
                Filter = APPSTRINGS.FILTER_ZIP,
                FileName = $"{EFIROM.FileInfoData.FileName}_{APPSTRINGS.EFIROM}_{APPSTRINGS.BACKUP}",
                OverwritePrompt = true
            })
            {
                // Action was cancelled
                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                FileTools.BackupFileToZip(EFIROM.LoadedBinaryBuffer, EFIROM.FileInfoData.FileNameExt, saveFileDialog.FileName);

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
                FileName = $"{EFIROM.FileInfoData.FileName}_{APPSTRINGS.FIRMWARE_INFO}",
                OverwritePrompt = true,
                InitialDirectory = ApplicationPaths.WorkingDirectory
            })
            {
                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                StringBuilder sbFirmware = new StringBuilder();

                sbFirmware.AppendLine("File");
                sbFirmware.AppendLine("----------------------------------");
                sbFirmware.AppendLine($"Filename:        {EFIROM.FileInfoData.FileNameExt}");
                sbFirmware.AppendLine($"Size (Bytes):    {FileTools.FormatBytesWithCommas(EFIROM.FileInfoData.Length)} bytes");
                sbFirmware.AppendLine($"Size (MB):       {FileTools.FormatBytesToReadableUnit((ulong)EFIROM.FileInfoData.Length)}");
                sbFirmware.AppendLine($"Size (Hex):      {EFIROM.FileInfoData.Length:X}h");
                sbFirmware.AppendLine($"CRC32:           {EFIROM.FileInfoData.CRC32:X}");
                sbFirmware.AppendLine($"Created:         {EFIROM.FileInfoData.CreationTime}");
                sbFirmware.AppendLine($"Modified:        {EFIROM.FileInfoData.LastWriteTime}\r\n");

                sbFirmware.AppendLine("Descriptor");
                sbFirmware.AppendLine("----------------------------------");
                if (FlashDescriptor.IsDescriptorMode)
                {
                    sbFirmware.AppendLine(
                        $"PDR Region:      Base: {FlashDescriptor.PdrBase:X}h, " +
                        $"Limit: {FlashDescriptor.PdrLimit:X}h, " +
                        $"Size: {FlashDescriptor.PdrSize:X}h");
                    sbFirmware.AppendLine(
                        $"ME Region:       Base: {FlashDescriptor.MeBase:X}h, " +
                        $"Limit: {FlashDescriptor.MeLimit:X}h, " +
                        $"Size: {FlashDescriptor.MeSize:X}h");
                    sbFirmware.AppendLine(
                        $"BIOS Region:     Base: {FlashDescriptor.BiosBase:X}h, " +
                        $"Limit: {FlashDescriptor.BiosLimit:X}h, " +
                        $"Size: {FlashDescriptor.BiosSize:X}h\r\n");
                }
                else
                {
                    sbFirmware.AppendLine("Descriptor mode is disabled.\r\n");
                }

                sbFirmware.AppendLine("Model");
                sbFirmware.AppendLine("----------------------------------");
                sbFirmware.AppendLine($"Identifier:      {EFIROM.EfiBiosIdSectionData.ModelPart ?? "N/A"}");
                sbFirmware.AppendLine($"Model:           {MacTools.ConvertEfiModelCode(EFIROM.EfiBiosIdSectionData.ModelPart) ?? "N/A"}");
                sbFirmware.AppendLine($"Configuration:   {EFIROM.ConfigCode ?? "N/A"}");
                sbFirmware.AppendLine($"Board ID:        {EFIROM.PdrSectionData.BoardId ?? "N/A"}\r\n");

                sbFirmware.AppendLine("Fsys");
                sbFirmware.AppendLine("----------------------------------");
                if (EFIROM.FsysStoreData.FsysBytes != null)
                {
                    sbFirmware.AppendLine($"Base:            {EFIROM.FsysStoreData.FsysBase:X}h");
                    sbFirmware.AppendLine($"Size:            {EFIROM.FsysRegionSize:X}h");
                    sbFirmware.AppendLine($"CRC32:           {EFIROM.FsysStoreData.CrcString ?? "N/A"}");
                    sbFirmware.AppendLine($"Serial:          {EFIROM.FsysStoreData.Serial ?? "N/A"}");
                    sbFirmware.AppendLine($"HWC:             {EFIROM.FsysStoreData.HWC ?? "N/A"}");
                    sbFirmware.AppendLine($"SON:             {EFIROM.FsysStoreData.SON ?? "N/A"}\r\n");
                }
                else
                {
                    sbFirmware.AppendLine("Fsys Store was not found.\r\n");
                }

                sbFirmware.AppendLine("Firmware");
                sbFirmware.AppendLine("----------------------------------");
                sbFirmware.AppendLine($"EFI Version:     {EFIROM.FirmwareVersion ?? "N/A"}");
                sbFirmware.AppendLine($"EFI Lock:        {EFIROM.EfiPrimaryLockData.LockType.ToString() ?? "N/A"}");
                sbFirmware.AppendLine($"APFS Capable:    {EFIROM.IsApfsCapable.ToString() ?? "N/A"}\r\n");

                File.WriteAllText(saveFileDialog.FileName, sbFirmware.ToString());

                sbFirmware.Clear();

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

        private void exportFmmmobilemeEmailTextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = APPSTRINGS.FILTER_TEXT,
                FileName = $"{EFIROM.FileInfoData.FileName}_{EFISTRINGS.FMM_EMAIL}",
                OverwritePrompt = true,
                InitialDirectory = ApplicationPaths.WorkingDirectory
            })
            {
                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                StringBuilder sbEmailData = new StringBuilder();

                sbEmailData.AppendLine("Find My Mac Email:");
                sbEmailData.AppendLine("----------------------------------");
                sbEmailData.AppendLine(EFIROM.FmmEmail);

                File.WriteAllText(saveFileDialog.FileName, sbEmailData.ToString());

                sbEmailData.Clear();

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

            using (frmSerialSelect form = new frmSerialSelect())
            {
                form.Tag = SerialSenderTag.EFIROMWindow;
                form.FormClosed += ChildWindowClosed;
                form.ShowDialog();

                if (form.DialogResult == DialogResult.OK)
                {
                    WriteEfiromSerialNumber(EFIROM.NewSerial);
                }
            }
        }

        private void eraseNvramToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BlurHelper.ApplyBlur(this);

            using (frmNvramSelect form = new frmNvramSelect())
            {
                form.FormClosed += ChildWindowClosed;
                form.ShowDialog();

                if (form.DialogResult == DialogResult.OK)
                {
                    EraseNvram(EFIROM.ResetVss, EFIROM.ResetSvs);
                }
            }
        }

        private void replaceFsysStoreToolStripMenuItem_Click(object sender, EventArgs e) => WriteFsysStore();

        private void replaceIntelMERegionToolStripMenuItem_Click(object sender, EventArgs e) => WriteIntelMeRegion();

        private void fixFsysChecksumToolStripMenuItem_Click(object sender, EventArgs e) => MaskFsysChecksum();

        private void invalidateEFILockToolStripMenuItem_Click(object sender, EventArgs e) => RemoveEfiLock();
        #endregion

        #region Tools Toolstrip Events
        private void generateFilenameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string strEfiModel = EFIROM.EfiBiosIdSectionData.ModelPart ?? EFISTRINGS.NOMODEL;
            string strEfiVersion = EFIROM.FirmwareVersion ?? EFISTRINGS.NOFWVER;
            string strSerial = EFIROM.FsysStoreData.Serial ?? EFISTRINGS.NOSERIAL;

            SetClipboardText($"{strEfiModel}_{strEfiVersion}_{strSerial}");
        }

        private void lookupSerialNumberToolStripMenuItem_Click(object sender, EventArgs e) =>
            MacTools.LookupSerialOnEveryMac(EFIROM.FsysStoreData.Serial);

        private void viewRomInformationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BlurHelper.ApplyBlur(this);

            using (Form form = new frmRominfo())
            {
                form.FormClosed += ChildWindowClosed;
                form.ShowDialog();
            }
        }

        private void resetWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Settings.ReadBool(SettingsBoolType.DisableConfDiag))
            {
                ToggleControlEnable(false);
                ResetWindow();
                return;
            }

            DialogResult dlgResult =
                METPrompt.Show(
                    this,
                    DIALOGSTRINGS.UNLOAD_FIRMWARE_RESET,
                    METPromptType.Warning,
                    METPromptButtons.YesNo);

            if (dlgResult == DialogResult.Yes)
            {
                ToggleControlEnable(false);
                ResetWindow();
            }
        }
        private void reloadFileFromDiskToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!File.Exists(EFIROM.LoadedBinaryPath))
            {
                METPrompt.Show(
                    this,
                    DIALOGSTRINGS.COULD_NOT_RELOAD,
                    METPromptType.Error,
                    METPromptButtons.Okay);

                return;
            }

            // Load bytes from loaded binary file.
            byte[] bBinaryBuffer = File.ReadAllBytes(EFIROM.LoadedBinaryPath);

            // Check if the binaries match in size and data.
            if (BinaryTools.ByteArraysMatch(bBinaryBuffer, EFIROM.LoadedBinaryBuffer))
            {
                // Loaded binaries match.
                METPrompt.Show(
                    this,
                    DIALOGSTRINGS.WARN_DATA_MATCHES_BUFF,
                    METPromptType.Warning,
                    METPromptButtons.Okay);

                return;
            }

            OpenBinary(EFIROM.LoadedBinaryPath);
        }
        #endregion

        #region Help Toolstrip Events
        private void manualToolStripMenuItem_Click(object sender, EventArgs e) => Process.Start(ApplicationUrls.GithubManual);

        private void viewApplicationLogToolStripMenuItem_Click(object sender, EventArgs e) => Logger.OpenLogFile(this);

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BlurHelper.ApplyBlur(this);

            using (Form form = new frmAbout())
            {
                form.Tag = StartupSenderTag.Other;
                form.FormClosed += ChildWindowClosed;
                form.ShowDialog();
            }
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BlurHelper.ApplyBlur(this);

            using (Form form = new frmSettings())
            {
                form.Tag = StartupSenderTag.Other;
                form.FormClosed += ChildWindowClosed;
                form.ShowDialog();
            }
        }
        #endregion

        #region Open binary
        private void OpenBinary(string filepath)
        {
            // Check the filesize
            if (!FileTools.IsValidMinMaxSize(filepath, this, FirmwareVars.MIN_IMAGE_SIZE, FirmwareVars.MAX_IMAGE_SIZE))
            {
                return;
            }

            // Check if the image is what we're looking for.
            if (!EFIROM.IsValidImage(File.ReadAllBytes(filepath)))
            {
                METPrompt.Show(
                    this,
                    DIALOGSTRINGS.NOT_VALID_EFIROM,
                    METPromptType.Warning,
                    METPromptButtons.Okay);

                return;
            }

            // If a firmware is loaded, reset all data.
            if (EFIROM.FirmwareLoaded)
            {
                ResetWindow();
            }

            ToggleControlEnable(false);

            // Set the binary path and load the bytes.
            EFIROM.LoadedBinaryPath = filepath;
            EFIROM.LoadedBinaryBuffer = File.ReadAllBytes(filepath);

            // Set the current directory.
            _strInitialDirectory = Path.GetDirectoryName(filepath);

            // Show loading bar.
            pbxLoad.Image = Properties.Resources.loading;

            // Load the firmware base in a separate thread.
            _tLoadFirmware = new Thread(() => LoadFirmwareBase(filepath, _cancellationToken.Token)) { IsBackground = true };
            _tLoadFirmware.Start();
        }

        private void LoadFirmwareBase(string filepath, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            EFIROM.LoadFirmwareBaseData(EFIROM.LoadedBinaryBuffer, filepath);

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (this.IsHandleCreated && !token.IsCancellationRequested)
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

            if (!token.IsCancellationRequested)
            {
                EFIROM.FirmwareLoaded = true;
            }
        }
        #endregion

        #region Update Window
        internal void UpdateUI()
        {
            // Parse time.
            UpdateParseTimeControls();

            // File information.
            UpdateFileNameControls();
            UpdateFileSizeControls();
            UpdateFileCrc32Controls();
            UpdateFileCreationDateControls();
            UpdateFileModifiedDateControls();

            // Firmware Data.
            UpdateModelControls();
            UpdateConfigCodeControls();
            UpdateFsysControls();
            UpdateSerialNumberControls();
            UpdateHardwareConfigControls();
            UpdateOrderNumberControls();
            UpdateEfiVersionControls();
            UpdateBoardIdControls();
            UpdateNvramControls();
            UpdateEfiLockControls();
            UpdateApfsCapableControls();
            UpdateIntelFitControls();
            UpdateDescriptorModeControls();
            UpdateIntelMeControls();
            UpdateLzmaArchiveControls();
            UpdateFmmEmailControls();

            // Apply DISABLED_TEXT to N/A labels.
            UITools.ApplyNestedPanelLabelForeColor(tlpFirmware, Colours.ClrDisabledText);

            // Check which descriptor copy menu items should be enabled.
            pdrBaseToolStripMenuItem.Enabled = FlashDescriptor.PdrBase != 0;
            meBaseToolStripMenuItem.Enabled = FlashDescriptor.MeBase != 0;
            biosBaseToolStripMenuItem.Enabled = FlashDescriptor.BiosBase != 0;

            // Update window title.
            UpdateWindowTitle();

            // Hide loading image.
            pbxLoad.Image = null;

            // Check and set control enable.
            ToggleControlEnable(true);
        }

        private void UpdateParseTimeControls() => lblParseTime.Text = $"{EFIROM.ParseTime.TotalSeconds:F2}s";

        private void UpdateFileNameControls() => lblFilename.Text = $"{APPSTRINGS.FILE}: '{EFIROM.FileInfoData.FileNameExt}'";

        private void UpdateFileSizeControls()
        {
            long lSize = EFIROM.FileInfoData.Length;
            bool bValidSize = FileTools.GetIsValidBinSize(lSize);
            lblFilesize.Text = $"{FileTools.FormatBytesWithCommas(lSize)} {APPSTRINGS.BYTES} ({lSize:X}h)";

            if (!bValidSize)
            {
                lblFilesize.ForeColor = Colours.ClrError;
                lblFilesize.Text += bValidSize ? string.Empty : $" ({FileTools.GetSizeDifference(lSize)})";
            }
        }

        private void UpdateFileCrc32Controls() => lblFileCrc32.Text = $"{EFIROM.FileInfoData.CRC32:X8}";

        private void UpdateFileCreationDateControls() => lblFileCreatedDate.Text = EFIROM.FileInfoData.CreationTime;

        private void UpdateFileModifiedDateControls() => lblFileModifiedDate.Text = EFIROM.FileInfoData.LastWriteTime;

        private void UpdateModelControls()
        {
            string strIdentifier = EFIROM.EfiBiosIdSectionData.ModelPart;

            if (!string.IsNullOrEmpty(strIdentifier))
            {
                string strModel = MacTools.ConvertEfiModelCode(EFIROM.EfiBiosIdSectionData.ModelPart);
                lblModel.Text = $"{strModel} ({strIdentifier})" ?? APPSTRINGS.NA;
                modelToolStripMenuItem.Enabled = true;
                return;
            }

            lblModel.Text = APPSTRINGS.NA;
            modelToolStripMenuItem.Enabled = false;
        }

        private void UpdateConfigCodeControls()
        {
            if (!string.IsNullOrEmpty(EFIROM.ConfigCode))
            {
                lblConfigCode.Text = EFIROM.ConfigCode;
                configurationToolStripMenuItem.Enabled = true;
                return;
            }

            lblConfigCode.Text = APPSTRINGS.CONTACT_SERVER;
            lblConfigCode.ForeColor = Colours.ClrInfo;
            GetConfigCodeAsync(EFIROM.FsysStoreData.HWC);
        }

        internal async void GetConfigCodeAsync(string hwc)
        {
            string configCode = await MacTools.GetDeviceConfigCodeSupportRemote(hwc);

            if (!string.IsNullOrEmpty(configCode))
            {
                EFIROM.ConfigCode = configCode;
                lblConfigCode.Text = configCode;
                lblConfigCode.ForeColor = Colours.ClrNormalText;
                configurationToolStripMenuItem.Enabled = true;
                return;
            }

            lblConfigCode.Text = APPSTRINGS.NA;
            lblConfigCode.ForeColor = Colours.ClrDisabledText;
            configurationToolStripMenuItem.Enabled = false;
        }

        private void UpdateFsysControls()
        {
            if (EFIROM.FsysStoreData.FsysBase != -1)
            {
                lblFsysStore.Text = $"{EFIROM.FsysStoreData.FsysBase:X2}h";
                bool doesCrcMatch = string.Equals(EFIROM.FsysStoreData.CrcString, EFIROM.FsysStoreData.CrcActualString);

                if (!string.IsNullOrEmpty(EFIROM.FsysStoreData.CrcString))
                {
                    lblFsysStore.Text += doesCrcMatch ? $" ({EFISTRINGS.CRC_VALID})" : $" ({EFISTRINGS.CRC_INVALID})";
                    lblFsysStore.ForeColor = doesCrcMatch ? lblFsysStore.ForeColor : Colours.ClrWarn;
                }

                if (EFIROM.ForceFoundFsys)
                {
                    lblFsysStore.Text += " [F]";
                }

                fsysBaseAddressToolStripMenuItem.Enabled = true;
                fsysCRC32ToolStripMenuItem.Enabled = true;

                return;
            }

            fsysBaseAddressToolStripMenuItem.Enabled = false;
            fsysCRC32ToolStripMenuItem.Enabled = false;
            lblFsysStore.Text = APPSTRINGS.NA;
        }

        private void UpdateSerialNumberControls()
        {
            string strSerial = EFIROM.FsysStoreData.Serial;

            if (!string.IsNullOrEmpty(strSerial))
            {
                if (cbxCensor.Checked)
                {
                    lblSerialNumber.Text = strSerial;
                }
                else
                {
                    if (strSerial.Length == 11)
                    {
                        lblSerialNumber.Text = $"{strSerial.Substring(0, 2)}******{strSerial.Substring(8, 3)}";
                    }
                    else if (strSerial.Length == 12)
                    {
                        lblSerialNumber.Text = $"{strSerial.Substring(0, 2)}******{strSerial.Substring(8, 4)}";
                    }
                }

                if (!Serial.IsValid(strSerial))
                {
                    lblSerialNumber.ForeColor = Colours.ClrWarn;
                }

                cbxCensor.Enabled = true;
                serialToolStripMenuItem.Enabled = true;
            }
            else
            {
                lblSerialNumber.Text = APPSTRINGS.NA;
                cbxCensor.Enabled = false;
                serialToolStripMenuItem.Enabled = false;
            }
        }

        private void UpdateHardwareConfigControls()
        {
            string StrHwc = EFIROM.FsysStoreData.HWC;
            lblHwc.Text = StrHwc ?? APPSTRINGS.NA;

            if (!string.IsNullOrEmpty(StrHwc))
            {
                hwcToolStripMenuItem.Enabled = true;

                return;
            }

            hwcToolStripMenuItem.Enabled = false;
        }

        private void UpdateOrderNumberControls()
        {
            string strSon = EFIROM.FsysStoreData.SON;
            lblOrderNumber.Text = strSon ?? APPSTRINGS.NA;

            if (!string.IsNullOrEmpty(strSon))
            {
                orderNoToolStripMenuItem.Enabled = true;
                return;
            }

            orderNoToolStripMenuItem.Enabled = false;
        }

        private void UpdateEfiVersionControls()
        {
            string strEfiVersion = EFIROM.FirmwareVersion;
            lblEfiVersion.Text = strEfiVersion ?? APPSTRINGS.NA;

            if (!string.IsNullOrEmpty(strEfiVersion))
            {
                efiVersionToolStripMenuItem.Enabled = true;
                return;
            }

            efiVersionToolStripMenuItem.Enabled = false;
        }

        private void UpdateBoardIdControls()
        {
            string strBoardId = EFIROM.PdrSectionData.BoardId;
            lblBoardId.Text = strBoardId ?? APPSTRINGS.NA;

            if (!string.IsNullOrEmpty(strBoardId))
            {
                boardIDToolStripMenuItem.Enabled = true;
                return;
            }

            boardIDToolStripMenuItem.Enabled = false;
        }

        private void UpdateNvramControls()
        {
            // Retrieve base addresses and empty states
            int iVssBase = EFIROM.VssPrimary.StoreBase;
            int iSvsBase = EFIROM.SvsPrimary.StoreBase;
            bool isVssEmpty = EFIROM.VssPrimary.IsStoreEmpty;
            bool isSvsEmpty = EFIROM.SvsPrimary.IsStoreEmpty;

            // Update labels and menu items
            UpdateStoreDisplay(lblVss, vSSBaseAddressToolStripMenuItem, iVssBase, isVssEmpty);
            UpdateStoreDisplay(lblSvs, sVSBaseAddressToolStripMenuItem, iSvsBase, isSvsEmpty);
        }

        private static void UpdateStoreDisplay(Label label, ToolStripMenuItem menuitem, int baseposition, bool isstoreempty)
        {
            if (baseposition != -1)
            {
                label.Text = $"{baseposition:X2}h {(isstoreempty ? $"[{EFISTRINGS.EMPTY}]" : $"[{EFISTRINGS.ACTIVE}]")}";
                menuitem.Enabled = true;
            }
            else
            {
                label.Text = APPSTRINGS.NA;
                menuitem.Enabled = false;
            }
        }

        private void UpdateEfiLockControls()
        {
            switch (EFIROM.EfiPrimaryLockData.LockType)
            {
                case EfiLockType.Locked:
                    lblEfiLock.Text = EFISTRINGS.LOCKED.ToUpper();
                    lblEfiLock.ForeColor = Colours.ClrWarn;
                    break;
                case EfiLockType.Unlocked:
                    lblEfiLock.Text = EFISTRINGS.UNLOCKED.ToUpper();
                    break;
                case EfiLockType.Unknown:
                default:
                    lblEfiLock.Text = APPSTRINGS.UNKNOWN.ToUpper();
                    lblEfiLock.ForeColor = Colours.ClrWarn;
                    break;
            }
        }

        private void UpdateApfsCapableControls()
        {
            switch (EFIROM.IsApfsCapable)
            {
                case ApfsCapable.Yes:
                    lblApfsCapable.Text = EFISTRINGS.APFS_DRIVER_FOUND;
                    break;
                case ApfsCapable.No:
                    lblApfsCapable.Text = EFISTRINGS.APFS_DRIVER_NOT_FOUND;
                    lblApfsCapable.ForeColor = Colours.ClrWarn;
                    break;
                case ApfsCapable.Unknown:
                    lblApfsCapable.Text = APPSTRINGS.UNKNOWN.ToUpper();
                    lblApfsCapable.ForeColor = Colours.ClrWarn;
                    break;
            }
        }

        private void UpdateDescriptorModeControls() => lblDescriptorMode.Text = $"{FlashDescriptor.IsDescriptorMode}";

        private void UpdateIntelMeControls()
        {
            string strIntelMeVersion = EFIROM.MeVersion;

            lblMeVersion.Text = strIntelMeVersion ?? APPSTRINGS.NA;

            if (!string.IsNullOrEmpty(strIntelMeVersion))
            {
                if (FlashDescriptor.MeBase != 0)
                {
                    if (!string.IsNullOrEmpty(strIntelMeVersion))
                    {
                        lblMeVersion.Text += $" ({FlashDescriptor.MeBase:X}h)";
                    }
                }

                meVersionToolStripMenuItem.Enabled = true;
                return;
            }

            meVersionToolStripMenuItem.Enabled = false;
        }

        private void UpdateIntelFitControls() =>
            fitVersionToolStripMenuItem.Enabled = !string.IsNullOrEmpty(EFIROM.FitVersion);

        private void UpdateLzmaArchiveControls() =>
            lblLzma.ForeColor = EFIROM.LzmaDecompressedBuffer != null ? Colours.ClrGlyphActive : Colours.ClrGlyphDefault;

        private void UpdateFmmEmailControls() =>
            lblFmmEmail.ForeColor = EFIROM.FmmEmail != null ? Colours.ClrGlyphActive : Colours.ClrGlyphDefault;
        #endregion

        #region UI Events
        private void ChildWindowClosed(object sender, EventArgs e) => BlurHelper.RemoveBlur(this);

        private void EnableButtons(bool enable, params Button[] buttons)
        {
            foreach (Button button in buttons)
            {
                button.Enabled = enable;
            }
        }

        private void EnableMenus(bool enable, params ContextMenuStrip[] contextmenus)
        {
            foreach (ContextMenuStrip contextMenuStrip in contextmenus)
            {
                contextMenuStrip.Enabled = enable;
            }
        }

        private void ToggleControlEnable(bool enable)
        {
            EnableButtons(enable, _menuButtons);
            EnableMenus(enable, _contextMenus);

            if (enable)
            {
                // Logic for enabling menus when controls are enabled
                bool isFsysBufferNull = EFIROM.FsysStoreData.FsysBytes != null;
                bool doesCrcMatch = isFsysBufferNull && !string.Equals(EFIROM.FsysStoreData.CrcActualString, EFIROM.FsysStoreData.CrcString);
                bool areNvramStoresEmpty =
                    EFIROM.VssPrimary.IsStoreEmpty &&
                    EFIROM.VssSecondary.IsStoreEmpty &&
                    EFIROM.SvsPrimary.IsStoreEmpty &&
                    EFIROM.SvsSecondary.IsStoreEmpty;

                // Export Menu
                exportFsysStoreToolStripMenuItem.Enabled = isFsysBufferNull;
                exportIntelMERegionToolStripMenuItem.Enabled = FlashDescriptor.IsDescriptorMode && FlashDescriptor.MeBase != 0 && FlashDescriptor.MeLimit != 0;
                exportNVRAMVSSStoresToolStripMenuItem.Enabled = !EFIROM.VssPrimary.IsStoreEmpty || !EFIROM.VssSecondary.IsStoreEmpty;
                exportNVRAMSVSStoresToolStripMenuItem.Enabled = !EFIROM.SvsPrimary.IsStoreEmpty || !EFIROM.SvsSecondary.IsStoreEmpty;
                exportLZMADXEArchiveToolStripMenuItem.Enabled = EFIROM.LzmaDecompressedBuffer != null;
                exportFmmmobilemeEmailTextToolStripMenuItem.Enabled = EFIROM.FmmEmail != null;

                // Patch Menu
                changeSerialNumberToolStripMenuItem.Enabled = EFIROM.FsysStoreData.FsysBase != -1 && EFIROM.FsysStoreData.SerialBase != -1;
                replaceIntelMERegionToolStripMenuItem.Enabled = FlashDescriptor.IsDescriptorMode && FlashDescriptor.MeBase != 0 && FlashDescriptor.MeLimit != 0;
                replaceFsysStoreToolStripMenuItem.Enabled = EFIROM.FsysStoreData.FsysBase != -1;
                eraseNVRAMToolStripMenuItem.Enabled = !areNvramStoresEmpty;
                fixFsysChecksumToolStripMenuItem.Enabled = doesCrcMatch;
                invalidateEFILockToolStripMenuItem.Enabled = EFIROM.EfiPrimaryLockData.LockType == EfiLockType.Locked;

                // Options Menu
                viewRomInformationToolStripMenuItem.Enabled = EFIROM.AppleRomInfoSectionData.SectionExists;
                lookupSerialNumberToolStripMenuItem.Enabled = !string.IsNullOrEmpty(EFIROM.FsysStoreData.Serial);
            }

            // Always enable/disable these controls
            tlpFilename.Enabled = enable;
            tlpFirmware.Enabled = enable;
        }

        private void SetButtonFontAndGlyph()
        {
            var buttons = new[]
            {
                new { Button = cmdClose, Font = Program.FontSegMdl2Regular12, Text = Program.MDL2_EXIT_CROSS },
                new { Button = cmdOpenInExplorer, Font = Program.FontSegMdl2Regular12, Text = Program.MDL2_FILE_EXPLORER },
            };

            foreach (var buttonData in buttons)
            {
                buttonData.Button.Font = buttonData.Font;
                buttonData.Button.Text = buttonData.Text;
            }
        }

        private void SetLabelFontAndGlyph()
        {
            lblLzma.Font = Program.FontSegMdl2Regular10;
            lblLzma.Text = Program.MDL2_REPORT;

            lblFmmEmail.Font = Program.FontSegMdl2Regular10;
            lblFmmEmail.Text = Program.MDL2_ACCOUNT;
        }

        private void SetTipHandlers()
        {
            Button[] buttons =
            {
                cmdMenuOpen,
                cmdMenuCopy,
                cmdMenuFolders,
                cmdOpenInExplorer,
                cmdMenuExport,
                cmdMenuPatch,
                cmdMenuTools,
                cmdMenuHelp
            };

            Label[] labels =
            {
                lblParseTime,
                lblLzma,
                lblFmmEmail
            };

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
                    { cmdMenuOpen, $"{EFISTRINGS.MENU_TIP_OPEN} (CTRL + O)" },
                    { cmdMenuCopy, $"{EFISTRINGS.MENU_TIP_COPY} (CTRL + C)" },
                    { cmdMenuFolders, $"{EFISTRINGS.MENU_TIP_FOLDERS} (CTRL + L)" },
                    { cmdMenuExport, $"{EFISTRINGS.MENU_TIP_EXPORT} (CTRL + E)"},
                    { cmdMenuPatch, $"{EFISTRINGS.MENU_TIP_PATCH} (CTRL + P)"},
                    { cmdMenuTools, $"{EFISTRINGS.MENU_TIP_TOOLS} (CTRL + T)"},
                    { cmdMenuHelp, $"{EFISTRINGS.MENU_TIP_HELP} (CTRL + H)" },
                    { cmdOpenInExplorer, $"{EFISTRINGS.MENU_TIP_OPENFILELOCATION} (CTRL + SHIFT + L)" },
                    { lblParseTime, APPSTRINGS.FW_PARSE_TIME},
                    { cbxCensor, censorString() },
                    { lblLzma, lzmaString() },
                    { lblFmmEmail, emailString() }
                };

                if (tooltips.TryGetValue(sender, out string value))
                {
                    lblStatusBarTip.Text = value;
                }

                tooltips.Clear();
            }
        }

        private string censorString() =>
            $"{(cbxCensor.Checked ? APPSTRINGS.HIDE : APPSTRINGS.SHOW)} {APPSTRINGS.SERIAL_NUMBER} (CTRL + SHIFT + N)";

        private string lzmaString() =>
            EFIROM.LzmaDecompressedBuffer != null ? EFISTRINGS.LZMA_VOL_FOUND : string.Empty;

        private string emailString() =>
            EFIROM.FmmEmail != null ? EFISTRINGS.FMM_EMAIL_FOUND : string.Empty;

        private void HandleCheckBoxChanged(object sender, EventArgs e)
        {
            if (sender == cbxCensor && cbxCensor.ClientRectangle.Contains(cbxCensor.PointToClient(Cursor.Position)))
            {
                if (!Settings.ReadBool(SettingsBoolType.DisableTips))
                {
                    lblStatusBarTip.Text = censorString();
                }
            }
        }

        private void HandleMouseLeaveTip(object sender, EventArgs e) => lblStatusBarTip.Text = string.Empty;

        private static void SetControlForeColor(Control parentControl, Color foreColor)
        {
            foreach (Control control in parentControl.Controls)
            {
                control.ForeColor = foreColor;
            }
        }

        private void NotifyPatchingFailure()
        {
            if (Prompts.ShowPatchFailedPrompt(this) == DialogResult.Yes)
            {
                Logger.OpenLogFile(this);
            }
        }

        private void UpdateWindowTitle()
        {
            this.Text = EFIROM.FileInfoData.FileNameExt;
            lblTitle.Text = $"{APPSTRINGS.EFIROM} {Program.MDL2_RIGHT_ARROW} {EFIROM.FileInfoData.FileNameExt}";
        }
        #endregion

        #region Misc Events
        internal void SetInitialDirectory()
        {
            // Get the initial directory from settings.
            string directory = Settings.ReadString(SettingsStringType.EfiInitialDirectory);

            // If the path is not empty check if it exists and set it as the initial directory.
            if (!string.IsNullOrEmpty(directory))
            {
                _strInitialDirectory = Directory.Exists(directory) ? directory : ApplicationPaths.WorkingDirectory;
            }
        }

        private void ResetWindow()
        {
            // Reset censor switch.
            cbxCensor.Checked = false;
            cbxCensor.Enabled = false;

            // Clear label data, and reset color properties.
            Label[] labels =
            {
                lblFilename,
                lblFilesize,
                lblFileCrc32,
                lblFileCreatedDate,
                lblFileModifiedDate,
                lblModel,
                lblConfigCode,
                lblSerialNumber,
                lblHwc,
                lblFsysStore,
                lblDescriptorMode,
                lblOrderNumber,
                lblEfiVersion,
                lblVss,
                lblSvs,
                lblEfiLock,
                lblBoardId,
                lblApfsCapable,
                lblMeVersion
            };

            foreach (Label label in labels)
            {
                label.Text = string.Empty;
                label.ForeColor = Colours.ClrNormalText;
            }

            // Reset parse time.
            lblParseTime.Text = "0.00s";
            lblLzma.ForeColor = Colours.ClrGlyphDefault;
            lblFmmEmail.ForeColor = Colours.ClrGlyphDefault;

            // Reset window text.
            Text = APPSTRINGS.EFIROM;
            lblTitle.Text = APPSTRINGS.EFIROM;

            // Reset initial directory.
            SetInitialDirectory();

            // Reset Intel FD.
            FlashDescriptor.ClearRegionData();

            // Reset EFIROM.
            EFIROM.ResetFirmwareBaseData();
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

        private void ClipboardSetFilename(bool showExtension) =>
            SetClipboardText(
                showExtension
                ? EFIROM.FileInfoData.FileNameExt
                : EFIROM.FileInfoData.FileName);

        private void ClipboardSetFileSize() =>
            SetClipboardText(
                $"{FileTools.FormatBytesWithCommas(EFIROM.FileInfoData.Length)} " +
                $"{APPSTRINGS.BYTES} ({EFIROM.FileInfoData.Length:X}h)");

        private void ClipboardSetFileCrc32() => SetClipboardText($"{EFIROM.FileInfoData.CRC32:X8}");

        private void ClipboardSetFileCreationTime() => SetClipboardText(EFIROM.FileInfoData.CreationTime);

        private void ClipboardSetFileModifiedTime() => SetClipboardText(EFIROM.FileInfoData.LastWriteTime);

        private void ClipboardSetFirmwareModel() =>
            SetClipboardText(MacTools.ConvertEfiModelCode(EFIROM.EfiBiosIdSectionData.ModelPart));

        private void ClipboardSetFirmwareConfigCode() => SetClipboardText(EFIROM.ConfigCode);

        private void ClipboardSetFsysBaseAddress() => SetClipboardText($"{EFIROM.FsysStoreData.FsysBase:X2}");

        private void ClipboardSetFirmwareFsysCrc32() => SetClipboardText(EFIROM.FsysStoreData.CrcString);

        private void ClipboardSetFirmwareHwc() => SetClipboardText(EFIROM.FsysStoreData.HWC);

        private void ClipboardSetFirmwareOrderNumber() => SetClipboardText(EFIROM.FsysStoreData.SON);

        private void ClipboardSetFirmwareVersion() => SetClipboardText(EFIROM.FirmwareVersion);

        private void ClipboardSetVssBaseAddress() => SetClipboardText($"{EFIROM.VssPrimary.StoreBase:X2}");

        private void ClipboardSetSvsBaseAddress() => SetClipboardText($"{EFIROM.SvsPrimary.StoreBase:X2}");

        private void ClipboardSetFirmwareBoardId() => SetClipboardText(EFIROM.PdrSectionData.BoardId);

        private void ClipboardSetFirmwareFitVersion() => SetClipboardText(EFIROM.FitVersion);

        private void ClipboardSetFirmwareMeVersion() => SetClipboardText(EFIROM.MeVersion);

        private void ClipboardSetPdrRegionOffsets() =>
            SetClipboardText(
                $"{APPSTRINGS.BASE} {FlashDescriptor.PdrBase:X}h, " +
                $"{APPSTRINGS.LIMIT} {FlashDescriptor.PdrLimit:X}h, " +
                $"{APPSTRINGS.SIZE} {FlashDescriptor.PdrSize:X}h");

        private void ClipboardSetMeRegionOffsets() =>
            SetClipboardText(
                $"{APPSTRINGS.BASE} {FlashDescriptor.MeBase:X}h, " +
                $"{APPSTRINGS.LIMIT} {FlashDescriptor.MeLimit:X}h, " +
                $"{APPSTRINGS.SIZE} {FlashDescriptor.MeSize:X}h");

        private void ClipboardSetBiosRegionOffsets() =>
            SetClipboardText(
                $"{APPSTRINGS.BASE} {FlashDescriptor.BiosBase:X}h, " +
                $"{APPSTRINGS.LIMIT} {FlashDescriptor.BiosLimit:X}h, " +
                $"{APPSTRINGS.SIZE} {FlashDescriptor.BiosSize:X}h");
        #endregion

        #region Write Serial
        private void WriteEfiromSerialNumber(string serial)
        {
            Logger.WriteCallerLine(LOGSTRINGS.PATCH_START);

            // Check serial length.
            if (serial.Length != 11 && serial.Length != 12)
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.SERIAL_LEN_INVALID} ({serial.Length})");
                NotifyPatchingFailure();
                return;
            }

            // Check if the SerialBase exists.
            if (EFIROM.FsysStoreData.SerialBase == -1)
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.SSN_BASE_NOT_FOUND}");
                NotifyPatchingFailure();
                return;
            }

            // Create buffers.
            Logger.WriteCallerLine(LOGSTRINGS.CREATING_BUFFERS);

            byte[] binaryBuffer = BinaryTools.CloneBuffer(EFIROM.LoadedBinaryBuffer);
            byte[] newSerialBytes = Encoding.UTF8.GetBytes(serial);

            // Overwrite serial in the binary buffer.
            Logger.WriteCallerLine(LOGSTRINGS.SSN_WTB);

            BinaryTools.OverwriteBytesAtBase(binaryBuffer, EFIROM.FsysStoreData.SerialBase, newSerialBytes);

            // Check HWC base and write new HWC.
            bool isHwcBasePresent = EFIROM.FsysStoreData.HWCBase != -1;
            string newHwc = null;

            if (isHwcBasePresent)
            {
                newHwc = serial.Substring(8, EFIROM.FsysStoreData.Serial.Length == 11 ? 3 : 4);
                byte[] newHwcBytes = Encoding.UTF8.GetBytes(newHwc);

                Logger.WriteCallerLine(LOGSTRINGS.HWC_WTB);

                // Write new HWC.
                BinaryTools.OverwriteBytesAtBase(binaryBuffer, EFIROM.FsysStoreData.HWCBase, newHwcBytes);
            }

            Logger.WriteCallerLine(LOGSTRINGS.FSYS_LFB);

            // Load patched fsys from the binary buffer.
            FsysStore fsysStoreFromBuffer = EFIROM.GetFsysStoreData(binaryBuffer, false);

            // Verify the serial was written correctly.
            if (!string.Equals(serial, fsysStoreFromBuffer.Serial))
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.SSN_NOT_WRITTEN}");
                NotifyPatchingFailure();
                return;
            }

            Logger.WriteCallerLine(LOGSTRINGS.SSN_WRITE_SUCCESS);

            // Verify the HWC was written correctly, if applicable.
            if (isHwcBasePresent && fsysStoreFromBuffer.HWCBase != -1 && !string.Equals(newHwc, fsysStoreFromBuffer.HWC))
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.HWC_NOT_WRITTEN}");
                NotifyPatchingFailure();
                return;
            }

            Logger.WriteCallerLine(LOGSTRINGS.HWC_WRITE_SUCCESS);

            // Patch fsys checksum in the binary buffer.
            binaryBuffer = EFIROM.MakeFsysCrcPatchedBinary(binaryBuffer, fsysStoreFromBuffer.FsysBase, fsysStoreFromBuffer.FsysBytes, fsysStoreFromBuffer.CrcActual);

            // Reload fsys store from the binary buffer and verify CRC masking success.
            fsysStoreFromBuffer = EFIROM.GetFsysStoreData(binaryBuffer, false);

            if (!string.Equals(fsysStoreFromBuffer.CrcString, fsysStoreFromBuffer.CrcActualString))
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.FSYS_SUM_MASK_FAIL}");
                NotifyPatchingFailure();
                return;
            }

            // Log success and prompt for saving the patched firmware.
            Logger.WriteCallerLine(LOGSTRINGS.PATCH_SUCCESS);

            if (Prompts.ShowPathSuccessPrompt(this) == DialogResult.Yes)
            {
                SaveOutputFirmwareEfirom(binaryBuffer);
                return;
            }

            Logger.WriteCallerLine(LOGSTRINGS.FILE_EXPORT_CANCELLED);
        }
        #endregion

        #region Erase NVRAM
        private void EraseNvram(bool resetVss, bool resetSvs)
        {
            Logger.WriteCallerLine(LOGSTRINGS.PATCH_START);

            // Load current firmware into buffer.
            byte[] binaryBuffer = BinaryTools.CloneBuffer(EFIROM.LoadedBinaryBuffer);

            // Erase NVRAM sections if required
            if (resetVss)
            {
                Logger.WriteCallerLine(LOGSTRINGS.NVRAM_VSS_ERASE);
                CheckEraseStore(nameof(EFIROM.VssPrimary), EFIROM.VssPrimary, binaryBuffer);
                CheckEraseStore(nameof(EFIROM.VssSecondary), EFIROM.VssSecondary, binaryBuffer);
            }

            if (resetSvs)
            {
                Logger.WriteCallerLine(LOGSTRINGS.NVRAM_SVS_ERASE);
                CheckEraseStore(nameof(EFIROM.SvsPrimary), EFIROM.SvsPrimary, binaryBuffer);
                CheckEraseStore(nameof(EFIROM.SvsSecondary), EFIROM.SvsSecondary, binaryBuffer);
            }

            Logger.WriteCallerLine(LOGSTRINGS.PATCH_SUCCESS);

            if (Prompts.ShowPathSuccessPrompt(this) == DialogResult.Yes)
            {
                SaveOutputFirmwareEfirom(binaryBuffer);
                return;
            }

            Logger.WriteCallerLine(LOGSTRINGS.FILE_EXPORT_CANCELLED);
        }

        private void CheckEraseStore(string storeName, NvramStore store, byte[] buffer)
        {
            if (store.StoreBase == -1)
            {
                Logger.WriteCallerLine($"{storeName} {LOGSTRINGS.NVR_BASE_NOT_FOUND}");
            }
            else if (store.IsStoreEmpty)
            {
                Logger.WriteCallerLine($"{storeName} {LOGSTRINGS.NVR_IS_EMPTY}");
            }
            else if (!GetAndEraseStore(storeName, store, buffer))
            {
                return;
            }
        }

        private bool GetAndEraseStore(string storeName, NvramStore nvramStore, byte[] binaryBuffer)
        {
            Logger.WriteCallerLine($"{storeName} {LOGSTRINGS.AT} {nvramStore.StoreBase:X}h {LOGSTRINGS.NVR_HAS_BODY_ERASING}");

            byte[] erasedBuffer = EraseNvramStore(NvramStoreType.Variable, nvramStore);

            if (erasedBuffer == null)
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.NVR_FAIL_ERASE_BODY} ({storeName})");
                NotifyPatchingFailure();
                return false;
            }

            BinaryTools.OverwriteBytesAtBase(binaryBuffer, nvramStore.StoreBase, erasedBuffer);

            byte[] tempBuffer = BinaryTools.GetBytesBaseLength(binaryBuffer, nvramStore.StoreBase, nvramStore.StoreLength);

            if (!BinaryTools.ByteArraysMatch(tempBuffer, erasedBuffer))
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.NVR_FAIL_WRITE_VERIFY} ({storeName})");
                NotifyPatchingFailure();
                return false;
            }

            return true;
        }

        private byte[] EraseNvramStore(NvramStoreType storeType, NvramStore store)
        {
            try
            {
                byte[] storeBuffer = BinaryTools.CloneBuffer(store.StoreBuffer);
                int bodyStart = EFIROM.HDR_SIZE;
                int bodyEnd = store.StoreBuffer.Length - EFIROM.HDR_SIZE;

                // Initialize header.
                Logger.WriteCallerLine(LOGSTRINGS.NVRAM_INIT_HDR);
                for (int i = 0x4; i <= 0x7; i++)
                {
                    storeBuffer[i] = 0xFF;
                }

                if (storeType == NvramStoreType.Variable)
                {
                    Logger.WriteCallerLine(LOGSTRINGS.NVRAM_INIT_HDR_VSS);
                    for (int i = 0x9; i <= 0xA; i++)
                    {
                        storeBuffer[i] = 0xFF;
                    }
                }

                // Verify that the relevant header bytes have been set to 0xFF.
                if (!VerifyErasedHeader(storeBuffer, storeType))
                {
                    Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.NVRAM_INIT_HDR_FAIL}");
                    return null;
                }

                Logger.WriteCallerLine(LOGSTRINGS.NVRAM_INIT_HDR_SUCCESS);

                // Pull the store body from the buffer.
                byte[] erasedStoreBodyBuffer = BinaryTools.GetBytesBaseLength(storeBuffer, bodyStart, bodyEnd);

                Logger.WriteCallerLine(LOGSTRINGS.NVR_ERASE_BODY);

                // Erase the store body.
                BinaryTools.EraseByteArray(erasedStoreBodyBuffer);

                Logger.WriteCallerLine(LOGSTRINGS.NVR_WRITE_ERASED_BODY);

                // Write the erased store back to the nvram store buffer.
                BinaryTools.OverwriteBytesAtBase(storeBuffer, bodyStart, erasedStoreBodyBuffer);

                // Check the body was erased.
                erasedStoreBodyBuffer = BinaryTools.GetBytesBaseLength(storeBuffer, bodyStart, bodyEnd);

                if (!BinaryTools.IsByteBlockEmpty(erasedStoreBodyBuffer))
                {
                    MessageBox.Show(LOGSTRINGS.NVR_BODY_WRITE_FAIL);
                    return null;
                }

                Logger.WriteCallerLine(LOGSTRINGS.NVR_STORE_ERASE_SUCESS);

                return storeBuffer;
            }
            catch (Exception e)
            {
                Logger.WriteErrorLine(nameof(EraseNvramStore), e.GetType(), e.Message);
                return null;
            }
        }

        private static bool VerifyErasedHeader(byte[] storeBuffer, NvramStoreType storeType)
        {
            for (int i = 0x4; i <= 0x7; i++)
            {
                if (storeBuffer[i] != 0xFF)
                    return false;
            }

            if (storeType == NvramStoreType.Variable)
            {
                for (int i = 0x9; i <= 0xA; i++)
                {
                    if (storeBuffer[i] != 0xFF)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        #endregion

        #region Write Fsys Store
        private void WriteFsysStore()
        {
            Logger.WriteCallerLine(LOGSTRINGS.PATCH_START);

            using (OpenFileDialog openFileDialog = CreateFsysOpenFileDialog())
            {
                if (openFileDialog.ShowDialog() != DialogResult.OK)
                {
                    Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.FSYS_IMPORT_CANCELLED}");
                    return;
                }

                Logger.WriteCallerLine(LOGSTRINGS.CREATING_BUFFERS);

                byte[] newFsysBuffer = File.ReadAllBytes(openFileDialog.FileName);

                if (!ValidateFsysStore(newFsysBuffer))
                {
                    return;
                }

                FsysStore fsysTempStore = EFIROM.GetFsysStoreData(newFsysBuffer, true);

                if (!ValidateFsysCrc(fsysTempStore, ref newFsysBuffer))
                {
                    return;
                }

                byte[] binaryBuffer = BinaryTools.CloneBuffer(EFIROM.LoadedBinaryBuffer);

                if (!WriteNewFsysStore(binaryBuffer, newFsysBuffer))
                {
                    return;
                }

                Logger.WriteCallerLine(LOGSTRINGS.PATCH_SUCCESS);

                if (Prompts.ShowPathSuccessPrompt(this) == DialogResult.Yes)
                {
                    SaveOutputFirmwareEfirom(binaryBuffer);
                    return;
                }

                Logger.WriteCallerLine(LOGSTRINGS.FILE_EXPORT_CANCELLED);
            }
        }

        private static OpenFileDialog CreateFsysOpenFileDialog()
        {
            return new OpenFileDialog
            {
                InitialDirectory = ApplicationPaths.FsysDirectory,
                Filter = APPSTRINGS.FILTER_BIN
            };
        }

        private bool ValidateFsysStore(byte[] fsysBuffer)
        {
            int fsysBase = BinaryTools.GetBaseAddress(fsysBuffer, EFISigs.FsysMarker);

            // Fsys store length should equal 800h, 2048 bytes.
            if (fsysBuffer.Length != EFIROM.FsysRegionSize)
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.EXPECTED_STORE_SIZE_NOT} {EFIROM.FsysRegionSize:X}h ({fsysBuffer.Length:X}h)");
                NotifyPatchingFailure();
                return false;
            }

            // Expect Fsys signature at 0h.
            if (fsysBase != 0)
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.STORE_SIG_MISALIGNED}");
                NotifyPatchingFailure();
                return false;
            }

            Logger.WriteCallerLine(LOGSTRINGS.VALIDATION_PASS);

            return true;
        }

        private bool ValidateFsysCrc(FsysStore tempFsysBuffer, ref byte[] newFsysBuffer)
        {
            if (!string.Equals(tempFsysBuffer.CrcString, tempFsysBuffer.CrcActualString))
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.FSYS_SUM_INVALID} ({LOGSTRINGS.FOUND} {tempFsysBuffer.CrcString}, {LOGSTRINGS.CALCULATED} {tempFsysBuffer.CrcActualString})");

                Logger.WriteCallerLine(LOGSTRINGS.MASKING_SUM);

                newFsysBuffer = EFIROM.PatchFsysCrc(tempFsysBuffer.FsysBytes, tempFsysBuffer.CrcActual);
                tempFsysBuffer = EFIROM.GetFsysStoreData(newFsysBuffer, true);

                if (!string.Equals(tempFsysBuffer.CrcString, tempFsysBuffer.CrcActualString))
                {
                    Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.SUM_MASKING_FAIL}");
                    NotifyPatchingFailure();
                    return false;
                }

                Logger.WriteCallerLine(LOGSTRINGS.FSYS_SUM_MASK_SUCCESS);
            }

            return true;
        }

        private bool WriteNewFsysStore(byte[] binaryBuffer, byte[] newFsysBuffer)
        {
            Logger.WriteCallerLine(LOGSTRINGS.WRITE_NEW_DATA);

            BinaryTools.OverwriteBytesAtBase(binaryBuffer, EFIROM.FsysStoreData.FsysBase, newFsysBuffer);
            FsysStore fsysTempBuffer = EFIROM.GetFsysStoreData(binaryBuffer, false);

            if (!BinaryTools.ByteArraysMatch(fsysTempBuffer.FsysBytes, newFsysBuffer))
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.STORE_COMP_FAILED}");
                NotifyPatchingFailure();
                return false;
            }

            return true;
        }
        #endregion

        #region Mask Fsys Checksum
        private void MaskFsysChecksum()
        {
            Logger.WriteCallerLine(LOGSTRINGS.PATCH_START);

            // Make binary with patched Fsys crc.
            byte[] binaryBuffer =
                EFIROM.MakeFsysCrcPatchedBinary(
                    BinaryTools.CloneBuffer(EFIROM.LoadedBinaryBuffer),
                    EFIROM.FsysStoreData.FsysBase,
                    EFIROM.FsysStoreData.FsysBytes,
                    EFIROM.FsysStoreData.CrcActual);

            if (binaryBuffer == null)
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.CRC_BUFFER_EMPTY}");
                NotifyPatchingFailure();
                return;
            }

            Logger.WriteCallerLine(LOGSTRINGS.PATCH_SUCCESS);

            if (Prompts.ShowPathSuccessPrompt(this) == DialogResult.Yes)
            {
                SaveOutputFirmwareEfirom(binaryBuffer);
                return;
            }

            Logger.WriteCallerLine(LOGSTRINGS.FILE_EXPORT_CANCELLED);
        }
        #endregion

        #region Remove EFI Lock
        private void RemoveEfiLock()
        {
            Logger.WriteCallerLine(LOGSTRINGS.PATCH_START);
            Logger.WriteCallerLine(LOGSTRINGS.CREATING_BUFFERS);

            // Initialize buffers.
            byte[] binaryBuffer = BinaryTools.CloneBuffer(EFIROM.LoadedBinaryBuffer);

            // Patch the primary store.
            byte[] unlockedPrimaryStore = PatchPrimaryStore(binaryBuffer);

            // Patch the backup store, if necessary.
            byte[] unlockedBackupStore = PatchBackupStore(binaryBuffer);

            // Verify patched stores.
            if (!VerifyPatchedStores(binaryBuffer, unlockedPrimaryStore, unlockedBackupStore))
            {
                NotifyPatchingFailure();
                return;
            }

            Logger.WriteCallerLine(LOGSTRINGS.PATCH_SUCCESS);

            // Prompt for saving and export if confirmed.
            if (Prompts.ShowPathSuccessPrompt(this) == DialogResult.Yes)
            {
                SaveOutputFirmwareEfirom(binaryBuffer);
                return;
            }

            Logger.WriteCallerLine(LOGSTRINGS.FILE_EXPORT_CANCELLED);
        }

        private byte[] PatchPrimaryStore(byte[] binaryBuffer)
        {
            Logger.WriteCallerLine(LOGSTRINGS.LOCK_PRIMARY_MAC);
            byte[] unlockedPrimaryStore = EFIROM.PatchSvsStoreMac(EFIROM.SvsPrimary.StoreBuffer, EFIROM.EfiPrimaryLockData.LockBase);

            Logger.WriteCallerLine(LOGSTRINGS.WRITE_NEW_DATA);
            BinaryTools.OverwriteBytesAtBase(binaryBuffer, EFIROM.SvsPrimary.StoreBase, unlockedPrimaryStore);

            return unlockedPrimaryStore;
        }

        private byte[] PatchBackupStore(byte[] binaryBuffer)
        {
            byte[] unlockedBackupStore = null;

            if (EFIROM.EfiBackupLockData.LockBase != -1)
            {
                Logger.WriteCallerLine(LOGSTRINGS.LOCK_BACKUP_MAC);
                unlockedBackupStore = EFIROM.PatchSvsStoreMac(EFIROM.SvsSecondary.StoreBuffer, EFIROM.EfiBackupLockData.LockBase);

                Logger.WriteCallerLine(LOGSTRINGS.WRITE_NEW_DATA);
                BinaryTools.OverwriteBytesAtBase(binaryBuffer, EFIROM.SvsSecondary.StoreBase, unlockedBackupStore);
            }

            return unlockedBackupStore;
        }

        private bool VerifyPatchedStores(byte[] binaryBuffer, byte[] unlockedPrimaryStore, byte[] unlockedBackupStore)
        {
            Logger.WriteCallerLine(LOGSTRINGS.LOCK_LOAD_SVS);

            int svsPrimaryBase = BinaryTools.GetBaseAddressUpToLimit(binaryBuffer, EFISigs.SvsStoreMarker, EFIROM.NvramBase, EFIROM.NvramLimit);
            NvramStore svsPrimary = EFIROM.ParseNvramStore(binaryBuffer, svsPrimaryBase, NvramStoreType.Secure);

            if (!BinaryTools.ByteArraysMatch(svsPrimary.StoreBuffer, unlockedPrimaryStore))
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.LOCK_PRIM_VERIF_FAIL}");
                return false;
            }

            if (unlockedBackupStore != null)
            {
                int svsBackupBase = BinaryTools.GetBaseAddressUpToLimit(binaryBuffer, EFISigs.SvsStoreMarker, svsPrimaryBase + EFIROM.HDR_SIZE, EFIROM.NvramLimit);
                NvramStore svsBackup = EFIROM.ParseNvramStore(binaryBuffer, svsBackupBase, NvramStoreType.Secure);

                if (!BinaryTools.ByteArraysMatch(svsBackup.StoreBuffer, unlockedBackupStore))
                {
                    Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.LOCK_BACK_VERIF_FAIL}");
                    return false;
                }
            }

            return true;
        }
        #endregion

        #region Write Intel ME
        private void WriteIntelMeRegion()
        {
            Logger.WriteCallerLine(LOGSTRINGS.PATCH_START);

            using (OpenFileDialog openFileDialog = CreateImeOpenFileDialog())
            {
                if (openFileDialog.ShowDialog() != DialogResult.OK)
                {
                    Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.IME_IMPORT_CANCELLED}");
                    return;
                }

                Logger.WriteCallerLine(LOGSTRINGS.CREATING_BUFFERS);

                byte[] imeBuffer = File.ReadAllBytes(openFileDialog.FileName);

                if (!ValidateMeRegion(imeBuffer))
                {
                    return;
                }

                string imeVersion = IntelME.GetVersionData(imeBuffer, ImeVersionType.ManagementEngine);

                Logger.WriteCallerLine($"{LOGSTRINGS.IME_VERSION} {imeVersion ?? APPSTRINGS.NOT_FOUND}");

                byte[] binaryBuffer = BinaryTools.CloneBuffer(EFIROM.LoadedBinaryBuffer);

                if (!WriteMeRegion(imeBuffer, binaryBuffer))
                {
                    return;
                }

                Logger.WriteCallerLine(LOGSTRINGS.PATCH_SUCCESS);

                if (Prompts.ShowPathSuccessPrompt(this) == DialogResult.Yes)
                {
                    SaveOutputFirmwareEfirom(binaryBuffer);
                    return;
                }

                Logger.WriteCallerLine(LOGSTRINGS.FILE_EXPORT_CANCELLED);
            }
        }

        private static OpenFileDialog CreateImeOpenFileDialog()
        {
            return new OpenFileDialog
            {
                InitialDirectory = ApplicationPaths.IntelMeDirectory,
                Filter = APPSTRINGS.FILTER_BIN
            };
        }

        private bool ValidateMeRegion(byte[] newImeBuffer)
        {
            int fptBase = BinaryTools.GetBaseAddress(newImeBuffer, IntelME.FPTMarker);

            if (fptBase == -1)
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.IME_FPT_NOT_FOUND}");
                NotifyPatchingFailure();
                return false;
            }

            if (newImeBuffer.Length > FlashDescriptor.MeSize)
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.IME_TOO_LARGE} {newImeBuffer.Length:X}h > {FlashDescriptor.MeSize:X}h");
                NotifyPatchingFailure();
                return false;
            }

            if (newImeBuffer.Length < FlashDescriptor.MeSize)
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.IME_TOO_SMALL} {newImeBuffer.Length:X}h > {FlashDescriptor.MeSize:X}h");
            }

            Logger.WriteCallerLine(LOGSTRINGS.VALIDATION_PASS);

            return true;
        }

        private bool WriteMeRegion(byte[] newImeBuffer, byte[] binaryBuffer)
        {
            byte[] ffBuffer = new byte[FlashDescriptor.MeSize];
            BinaryTools.EraseByteArray(ffBuffer);

            Array.Copy(newImeBuffer, 0, ffBuffer, 0, newImeBuffer.Length);
            Array.Copy(ffBuffer, 0, binaryBuffer, FlashDescriptor.MeBase, ffBuffer.Length);

            byte[] patchedMeBuffer = BinaryTools.GetBytesBaseLimit(binaryBuffer, (int)FlashDescriptor.MeBase, (int)FlashDescriptor.MeLimit);

            if (!BinaryTools.ByteArraysMatch(patchedMeBuffer, ffBuffer))
            {
                Logger.WriteCallerLine($"{LOGSTRINGS.PATCH_FAIL} {LOGSTRINGS.IME_BUFFER_MISMATCH}");
                NotifyPatchingFailure();
                return false;
            }

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
                FileName = EFIROM.FileInfoData.FileName,
                OverwritePrompt = true,
                InitialDirectory = ApplicationPaths.BuildsDirectory
            };
        }

        private void SaveOutputFirmwareEfirom(byte[] binaryBuffer)
        {
            using (SaveFileDialog saveFileDialog = CreateFirmwareSaveFileDialog())
            {
                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                {
                    Logger.WriteCallerLine(LOGSTRINGS.FILE_EXPORT_CANCELLED);
                    return;
                }

                if (FileTools.WriteAllBytesEx(saveFileDialog.FileName, binaryBuffer) && File.Exists(saveFileDialog.FileName))
                {
                    Logger.WriteCallerLine($"{LOGSTRINGS.FILE_SAVE_SUCCESS} {saveFileDialog.FileName}");

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