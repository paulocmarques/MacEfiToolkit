﻿// Mac EFI Toolkit
// https://github.com/MuertoGB/MacEfiToolkit

// EFIROM.cs - Handles parsing of firmware data
// Released under the GNU GLP v3.0

using Mac_EFI_Toolkit.Tools;
using Mac_EFI_Toolkit.Tools.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Mac_EFI_Toolkit.Firmware.EFI
{
    class EFIROM
    {
        #region Internal Members
        internal static string LoadedBinaryPath = null;
        internal static byte[] LoadedBinaryBytes = null;
        internal static bool FirmwareLoaded = false;
        internal static string FirmwareVersion = null;
        internal static string ConfigCode = null;
        internal static bool ForceFoundFsys = false;
        internal static string FitVersion = null;
        internal static string MeVersion = null;

        internal static bool bResetVss = false;
        internal static bool bResetSvs = false;

        internal static Binary FileInfoData;
        internal static PdrSection PdrSectionData;
        internal static NvramStore VssStoreData;
        internal static NvramStore SvsStoreData;
        internal static EFILock EfiPrimaryLockData;
        internal static EFILock EfiBackupLockData;
        internal static FsysStore FsysStoreData;
        internal static AppleRomInformationSection AppleRomInfoSectionData;
        internal static EfiBiosIdSection EfiBiosIdSectionData;

        internal static ApfsCapable IsApfsCapable = ApfsCapable.Unknown;

        internal static TimeSpan tsParseTime { get; private set; }

        internal static int FSYS_RGN_SIZE = 0;
        internal static int NVRAM_BASE = -1;
        internal static int NVRAM_SIZE = -1;

        internal const int CRC32_SIZE = 4; // 4h
        #endregion

        #region Private Members
        private const int GUID_SIZE = 16;          // 10h
        private const int ZERO_VECTOR_SIZE = 16;   // 10h
        private const int LITERAL_POS = 2;         // 2h
        private static readonly Encoding _utf8 = Encoding.UTF8;
        #endregion

        #region Parse Firmware
        internal static void LoadFirmwareBaseData(byte[] sourceBytes, string fileName)
        {
            // Start bench.
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Try processing flash descriptor.
            IFD.ParseRegionData(sourceBytes);

            // Parse file info.
            FileInfoData = FileTools.GetBinaryFileInfo(fileName);

            // Parse Platform Data Region.
            PdrSectionData = GetPdrData(sourceBytes);

            // Find the NVRAM base address.
            NVRAM_BASE = BinaryTools.GetBaseAddress(
                sourceBytes, Guids.NVRAM_SECTION_GUID, (int)IFD.BIOS_REGION_BASE, (int)IFD.BIOS_REGION_LIMIT) - ZERO_VECTOR_SIZE;

            if (NVRAM_BASE < 0 || NVRAM_BASE > FileInfoData.Length)
            {
                Logger.Write($"Invalid NVRAM base address: {NVRAM_BASE}", LogType.Application);
                NVRAM_BASE = -1;
            }

            // Determine size of the NVRAM section.
            // Int32 value is stored at NVRAM_BASE + 0x20 (32 decimal).
            NVRAM_SIZE = BitConverter.ToInt32(BinaryTools.GetBytesBaseLength(sourceBytes, NVRAM_BASE + (ZERO_VECTOR_SIZE + GUID_SIZE), 4), 0);

            if (NVRAM_SIZE < 0 || NVRAM_SIZE > FileInfoData.Length)
            {
                Logger.Write($"Invalid NVRAM size: {NVRAM_SIZE}", LogType.Application);
                NVRAM_SIZE = -1;
            }

            // Parse NVRAM VSS Store data.
            VssStoreData = GetNvramStoreData(sourceBytes, NvramStoreType.VSS);

            // Parse NVRAM SVS Store data.
            SvsStoreData = GetNvramStoreData(sourceBytes, NvramStoreType.SVS);

            // Search both NVRAM SVS stores for a Message Authentication Code.
            EfiPrimaryLockData = GetIsEfiLocked(SvsStoreData.PrimaryStoreBytes);
            EfiBackupLockData = GetIsEfiLocked(SvsStoreData.BackupStoreBytes);

            // Parse Fsys Store data.
            FsysStoreData = GetFsysStoreData(sourceBytes, false);

            // Try to force find the Fsys store if it wasn't found in the first pass.
            if (FsysStoreData.FsysBytes == null)
            {
                FsysStoreData = GetFsysStoreData(sourceBytes, false, true);

                if (FsysStoreData.FsysBytes != null)
                {
                    ForceFoundFsys = true;

                    Logger.Write($"Force found Fsys Store at {FsysStoreData.FsysBase:X}h. " +
                        $"The image may be misaligned or corrupt ({FileInfoData.FileNameExt}).", LogType.Application
                    );
                }
            }

            // Fetch the Config Code
            ConfigCode = FsysStoreData.HWC != null ? MacTools.GetDeviceConfigCodeLocal(FsysStoreData.HWC) : null;

            // Parse AppleRomSectionInformation region data.
            AppleRomInfoSectionData = GetRomInformationData(sourceBytes);

            // Parse EfiBiosId section data.
            EfiBiosIdSectionData = GetEfiBiosIdSectionData(sourceBytes);

            // Check if the firmware is APFS capable.
            IsApfsCapable = GetIsApfsCapable(LoadedBinaryBytes);

            // Generate a proper EFI version string.
            FirmwareVersion = MacTools.GetFirmwareVersion();

            // Get the Intel ME Flash Image Tool version.
            FitVersion = IME.GetVersionData(LoadedBinaryBytes, VersionType.FlashImageTool);

            // Get the Intel ME version.
            MeVersion = IME.GetVersionData(LoadedBinaryBytes, VersionType.ManagementEngine);

            stopwatch.Stop();
            tsParseTime = stopwatch.Elapsed;
        }

        internal static void ResetFirmwareBaseData()
        {
            LoadedBinaryPath = null;
            LoadedBinaryBytes = null;
            FirmwareLoaded = false;
            FirmwareVersion = null;
            ConfigCode = null;
            ForceFoundFsys = false;
            FitVersion = null;
            MeVersion = null;

            bResetVss = false;
            bResetSvs = false;

            FileInfoData = default;
            PdrSectionData = default;
            VssStoreData = default;
            SvsStoreData = default;
            EfiPrimaryLockData = default;
            EfiBackupLockData = default;
            FsysStoreData = default;
            AppleRomInfoSectionData = default;
            EfiBiosIdSectionData = default;
            IsApfsCapable = ApfsCapable.Unknown;

            Serial.NewValue = string.Empty;

            FSYS_RGN_SIZE = 0;
            NVRAM_BASE = -1;
            NVRAM_SIZE = -1;
        }

        internal static bool IsValidImage(byte[] sourceBytes)
        {
            byte[][] appleGuids = new[]
            {
                Guids.APPLE_IMMUTABLE_FV_GUID,
                Guids.APPLE_AUTH_FV_GUID,
                Guids.APPLE_IMC_GUID
            };

            // Check if any of the Apple GUIDs are found within the firmware.
            return appleGuids.Any(guid => BinaryTools.GetBaseAddress(sourceBytes, guid, 0, sourceBytes.Length) != -1);
        }
        #endregion 

        #region Platform Data Region
        internal static PdrSection GetPdrData(byte[] sourceBytes)
        {
            // Descriptor mode not set.
            if (!IFD.IsDescriptorMode)
            {
                return DefaultPdrSection();
            }

            // Platform data region is not present.
            if (IFD.PDR_REGION_BASE == 0)
            {
                return DefaultPdrSection();
            }

            // Look for the board id signature bytes.
            int baseAddress = BinaryTools.GetBaseAddress(sourceBytes, PDR_BOARD_ID_SIGNATURE, (int)IFD.PDR_REGION_BASE, (int)IFD.PDR_REGION_SIZE);

            // Board id signature not found.
            if (baseAddress == -1)
            {
                return DefaultPdrSection();
            }

            int boardIdLength = 8;
            int dataStartPosition = 5;

            byte[] bidBytes = BinaryTools.GetBytesBaseLength(sourceBytes, baseAddress + dataStartPosition, boardIdLength);

            if (bidBytes == null)
            {
                return DefaultPdrSection();
            }

            // Return the board id.
            return new PdrSection
            {
                BoardIdBase = baseAddress,
                BoardId = $"Mac-{BitConverter.ToString(bidBytes).Replace("-", "")}"
            };
        }

        private static PdrSection DefaultPdrSection()
        {
            return new PdrSection
            {
                BoardId = null
            };
        }

        internal static readonly byte[] PDR_BOARD_ID_SIGNATURE =
        {
            0xF8, 0x7C, 0x00, 0x00, 0x19
        };
        #endregion

        #region NVRAM / EFI Lock
        internal static NvramStore GetNvramStoreData(byte[] sourceBytes, NvramStoreType storeType)
        {
            byte[] storeTypeSignature = GetNvramStoreSignature(storeType);

            if (NVRAM_BASE == -1)
            {
                return DefaultNvramStoreData();
            }

            // Retrieve primary store details.
            (int primaryStoreSize, int primaryStoreBase, byte[] primaryStoreData, bool isPrimaryStoreEmpty) =
                ParseStoreData(sourceBytes, storeTypeSignature, NVRAM_BASE, GUID_SIZE);

            // Return immediately if the primary store base is not found.
            if (primaryStoreBase == -1)
            {
                return DefaultNvramStoreData();
            }

            // If primary store is empty, retain the base address and empty status but no size/data.
            if (primaryStoreSize == -1)
            {
                return new NvramStore
                {
                    StoreType = storeType,
                    PrimaryStoreSize = primaryStoreSize,      // -1 indicates no size
                    PrimaryStoreBase = primaryStoreBase,      // Base address of found empty store
                    PrimaryStoreBytes = primaryStoreData,     // Null data for empty store
                    IsPrimaryStoreEmpty = true,
                    BackupStoreSize = -1,                     // No backup store since primary is empty
                    BackupStoreBase = -1,
                    BackupStoreBytes = null,
                    IsBackupStoreEmpty = true
                };
            }

            // Calculate padding size only if primary store has valid data.
            int paddingSize = 0;
            for (int i = primaryStoreBase + primaryStoreSize; i < sourceBytes.Length && sourceBytes[i] == 0xFF; i++)
            {
                paddingSize++;
            }

            // Fetch backup store data.
            (int backupStoreSize, int backupStoreBase, byte[] backupStoreData, bool isBackupStoreEmpty) =
                ParseStoreData(sourceBytes, storeTypeSignature, primaryStoreBase + primaryStoreSize + paddingSize, GUID_SIZE);

            return new NvramStore
            {
                StoreType = storeType,
                PrimaryStoreSize = primaryStoreSize,
                PrimaryStoreBase = primaryStoreBase,
                PrimaryStoreBytes = primaryStoreData,
                IsPrimaryStoreEmpty = isPrimaryStoreEmpty,
                BackupStoreSize = backupStoreSize,
                BackupStoreBase = backupStoreBase,
                BackupStoreBytes = backupStoreData,
                IsBackupStoreEmpty = isBackupStoreEmpty
            };
        }

        private static (int Size, int BaseAddress, byte[] Data, bool IsEmpty) ParseStoreData(byte[] sourceBytes, byte[] storeSignatureType, int baseAddress, int headerSize)
        {
            int storeSize = -1;
            byte[] storeData = null;
            bool isStoreEmpty = true;

            int storeBaseAddress = BinaryTools.GetBaseAddress(sourceBytes, storeSignatureType, baseAddress);

            if (storeBaseAddress != -1 && BinaryTools.GetBytesBaseLength(sourceBytes, storeBaseAddress, 6) is byte[] bytesStoreHeader)
            {
                NvramStoreHeader storeHeader = Helper.DeserializeHeader<NvramStoreHeader>(bytesStoreHeader);

                if (storeHeader.SizeOfData != 0xFFFF && storeHeader.SizeOfData != 0)
                {
                    storeSize = storeHeader.SizeOfData;

                    storeData = BinaryTools.GetBytesBaseLength(sourceBytes, storeBaseAddress, storeSize);

                    if (storeData != null)
                    {
                        byte[] storeBodyData = BinaryTools.GetBytesBaseLength(sourceBytes, storeBaseAddress + headerSize, storeSize - headerSize);

                        isStoreEmpty = BinaryTools.IsByteBlockEmpty(storeBodyData);
                    }
                }
            }

            return (storeSize, storeBaseAddress, storeData, isStoreEmpty);
        }

        private static NvramStore DefaultNvramStoreData()
        {
            return new NvramStore
            {
                PrimaryStoreSize = -1,
                PrimaryStoreBase = -1,
                PrimaryStoreBytes = null,
                IsPrimaryStoreEmpty = true,
                BackupStoreSize = -1,
                BackupStoreBase = -1,
                BackupStoreBytes = null,
                IsBackupStoreEmpty = true
            };
        }

        private static byte[] GetNvramStoreSignature(NvramStoreType storeType)
        {
            switch (storeType)
            {
                case NvramStoreType.SVS:
                    return SVS_STORE_SIG;
                case NvramStoreType.VSS:
                    return VSS_STORE_SIG;
                default:
                    throw new ArgumentException("Invalid NVRAM header type.");
            }
        }

        internal static EFILock GetIsEfiLocked(byte[] nvramStoreBytes)
        {
            // NVRAM store is empty.
            if (nvramStoreBytes == null)
            {
                return DefaultEfiLockStatus();
            }

            // Search store for Message Authentication Code.
            int crcBaseAddress = BinaryTools.GetBaseAddress(nvramStoreBytes, EFI_LOCK_MAC_SIG);

            // No Message Authentication Code was found.
            if (crcBaseAddress == -1)
            {
                return DefaultEfiLockStatus();
            }

            // MAC present
            return new EFILock
            {
                LockType = EfiLockType.Locked,
                LockCrcBase = crcBaseAddress
            };
        }

        private static EFILock DefaultEfiLockStatus()
        {
            return new EFILock
            {
                LockType = EfiLockType.Unlocked,
                LockCrcBase = -1,
            };
        }

        internal static readonly byte[] EFI_LOCK_MAC_SIG =
        {
            0x43, 0x00, 0x42, 0x00,
            0x46, 0x00, 0x32, 0x00,
            0x43, 0x00, 0x43, 0x00,
            0x33, 0x00, 0x32
        };

        internal static readonly byte[] VSS_STORE_SIG =
        {
            0x24, 0x56, 0x53, 0x53
        };

        internal static readonly byte[] SVS_STORE_SIG =
        {
            0x24, 0x53, 0x56, 0x53
        };
        #endregion

        #region Fsys Store
        // Fsys resides in the NVRAM at either base: 0x20000h, or 0x22000h.
        internal static FsysStore GetFsysStoreData(byte[] sourceBytes, bool isFsysStoreOnly, bool forceFindFsysStore = false)
        {
            // Find the base position of Fsys Store.
            int fsysBaseAddress = FindFsysBaseAddress(sourceBytes, isFsysStoreOnly, forceFindFsysStore);

            // If Fsys Store base is not found, return default data.
            if (fsysBaseAddress == -1)
            {
                return DefaultFsysRegion();
            }

            // Retrieve FsysStore bytes.
            byte[] fsysStoreBytes = GetFsysStoreBytes(sourceBytes, fsysBaseAddress);

            // If FsysStore is invalid, return default data.
            if (!IsValidFsysStore(fsysStoreBytes))
            {
                return DefaultFsysRegion();
            }


            // Retrieve CRC bytes and calculate CRC values.
            byte[] crcBytes = GetCrcBytes(sourceBytes, fsysBaseAddress);

            string crcString = GetFlippedCrcString(crcBytes);

            uint uiCrcCalc = CalculateFsysCrc(fsysStoreBytes);

            string crcCalcString = $"{uiCrcCalc:X8}";

            // Find and parse various signatures within FsysStore.
            int serialDataBaseAddress = FindSignatureAddress(sourceBytes, fsysBaseAddress, FSYS_RGN_SIZE, SSN_LOWER_SIG, SSN_UPPER_SIG, SSNP_LOWER_SIG);
            string serialString = ParseFsysString(sourceBytes, serialDataBaseAddress);

            int hwcDataBaseAddress = FindSignatureAddress(sourceBytes, fsysBaseAddress, FSYS_RGN_SIZE, HWC_LOWER_SIG, HWC_UPPER_SIG);
            string hwcString = ParseFsysString(sourceBytes, hwcDataBaseAddress);

            int sonDataBaseAddress = FindSignatureAddress(sourceBytes, fsysBaseAddress, FSYS_RGN_SIZE, SON_LOWER_SIG, SON_UPPER_SIG);
            string sonString = ParseFsysString(sourceBytes, sonDataBaseAddress);

            // Trim trailing '/' from SON string if present.
            if (sonString != null && sonString.EndsWith("/"))
            {
                sonString = sonString.TrimEnd('/');
            }

            // Create and return FsysStore object.
            return new FsysStore
            {
                FsysBytes = fsysStoreBytes,
                FsysBase = fsysBaseAddress,
                Serial = serialString,
                SerialBase = serialDataBaseAddress != -1 ? serialDataBaseAddress + LITERAL_POS : -1,
                HWC = hwcString,
                HWCBase = hwcDataBaseAddress != -1 ? hwcDataBaseAddress + LITERAL_POS : -1,
                SON = sonString,
                CrcString = crcString,
                CrcCalcString = crcCalcString,
                CRC32CalcInt = uiCrcCalc
            };
        }

        private static string ParseFsysString(byte[] sourceBytes, int baseAddress)
        {
            // Return null if base position is invalid or data size is zero.
            if (baseAddress == -1 || sourceBytes[baseAddress] == 0)
            {
                return null;
            }

            // Read size of the indicated variable.
            int dataSizeByte = sourceBytes[baseAddress];

            // Return null if data size is invalid.
            if (dataSizeByte == 0)
            {
                return null;
            }

            // Read the variable bytes.
            byte[] stringBytes = BinaryTools.GetBytesBaseLength(sourceBytes, baseAddress + LITERAL_POS, dataSizeByte);

            // Return null if bytes are invalid or exceed the data size.
            if (stringBytes == null || stringBytes.Length != dataSizeByte)
            {
                return null;
            }

            // Return string data.
            return _utf8.GetString(stringBytes);
        }

        private static int FindFsysBaseAddress(byte[] sourceBytes, bool isFsysStoreOnly, bool forceFindFsysStore)
        {
            if (isFsysStoreOnly)
            {
                return 0;
            }

            if (forceFindFsysStore)
            {
                return BinaryTools.GetBaseAddress(sourceBytes, FSYS_SIG, (int)IFD.BIOS_REGION_BASE, (int)IFD.BIOS_REGION_LIMIT);
            }

            if (NVRAM_BASE == -1)
            {
                return -1;
            }

            if (NVRAM_SIZE == -1)
            {
                return -1;
            }

            return BinaryTools.GetBaseAddress(sourceBytes, FSYS_SIG, NVRAM_BASE, NVRAM_SIZE);
        }

        private static bool IsValidFsysStore(byte[] fsysStoreBytes)
        {
            return fsysStoreBytes != null && fsysStoreBytes.Length == FSYS_RGN_SIZE;
        }

        private static string GetFlippedCrcString(byte[] crcBytes)
        {
            byte[] crcEndianBytes = crcBytes.Reverse().ToArray();

            return BitConverter.ToString(crcEndianBytes).Replace("-", "");
        }

        private static uint CalculateFsysCrc(byte[] fsysStoreBytes)
        {
            return MacTools.GetUintFsysCrc32(fsysStoreBytes);
        }

        private static int FindSignatureAddress(byte[] sourceBytes, int baseAddress, int maxSearchLength, params byte[][] signatures)
        {
            foreach (byte[] signature in signatures)
            {
                int signatureBaseAddress = BinaryTools.GetBaseAddress(sourceBytes, signature, baseAddress, maxSearchLength);

                if (signatureBaseAddress != -1)
                {
                    return signatureBaseAddress + signature.Length;
                }
            }

            return -1;
        }

        private static byte[] GetFsysStoreBytes(byte[] sourceBytes, int fsysBaseAddress)
        {
            // Get Fsys Store size bytes - fsys base + 0x09, length 2 bytes (int16).
            byte[] fsysSizeBytes = BinaryTools.GetBytesBaseLength(sourceBytes, fsysBaseAddress + 9, 2);

            // Convert size to int16 value.
            FSYS_RGN_SIZE = BitConverter.ToInt16(fsysSizeBytes, 0);

            // Min Fsys rgn size.
            if (FSYS_RGN_SIZE < 2048)
            {
                Logger.Write($"Fsys Store size was less than the min expected size: {FSYS_RGN_SIZE}", LogType.Application);

                return null;
            }

            return BinaryTools.GetBytesBaseLength(sourceBytes, fsysBaseAddress, FSYS_RGN_SIZE);
        }

        private static byte[] GetCrcBytes(byte[] sourceBytes, int fsysBaseAddress)
        {
            return BinaryTools.GetBytesBaseLength(sourceBytes, fsysBaseAddress + (FSYS_RGN_SIZE - CRC32_SIZE), CRC32_SIZE);
        }

        private static FsysStore DefaultFsysRegion()
        {
            return new FsysStore
            {
                FsysBytes = null,
                FsysBase = -1,
                Serial = null,
                SerialBase = -1,
                HWC = null,
                HWCBase = -1,
                SON = null,
                CrcString = null,
                CrcCalcString = null,
                CRC32CalcInt = 0xFFFFFFF
            };
        }

        internal static readonly byte[] FSYS_SIG =
        {
            0x46, 0x73, 0x79, 0x73, 0x01
        };

        internal static readonly byte[] SSN_LOWER_SIG =
        {
            0x03, 0x73, 0x73, 0x6E
        };

        internal static readonly byte[] SSN_UPPER_SIG =
        {
            0x03, 0x53, 0x53, 0x4E
        };

        internal static readonly byte[] SSNP_LOWER_SIG =
        {
             0x04, 0x73, 0x73, 0x6E, 0x70
        };

        internal static readonly byte[] HWC_LOWER_SIG =
        {
            0x03, 0x68, 0x77, 0x63
        };

        internal static readonly byte[] HWC_UPPER_SIG =
        {
            0x03, 0x53, 0x53, 0x4E
        };

        internal static readonly byte[] SON_LOWER_SIG =
        {
            0x03, 0x73, 0x6F, 0x6E
        };

        internal static readonly byte[] SON_UPPER_SIG =
        {
            0x03, 0x53, 0x4F, 0x4E
        };
        #endregion

        #region Apple ROM Information Section
        internal static AppleRomInformationSection GetRomInformationData(byte[] sourceBytes)
        {
            // Define constants for index and termination bytes.
            const byte indexByte = 0x20;
            const byte terminationByte = 0x0A;

            // Initialize signature-data dictionary.
            Dictionary<byte[], string> romInfoData = new Dictionary<byte[], string>
            {
                { BIOS_ID_SIGNATURE, null },
                { MODEL_SIGNATURE, null },
                { EFI_VERSION_SIGNATURE, null },
                { BUILT_BY_SIGNATURE, null },
                { DATE_SIGNATURE, null },
                { REVISION_SIGNATURE, null },
                { ROM_VERSION_SIGNATURE, null },
                { BUILDCAVE_ID_SIGNATURE, null },
                { BUILD_TYPE_SIGNATURE, null },
                { COMPILER_SIGNATURE, null }
            };

            // Find all GUID locations.
            List<int> romSectionBaseAddresses = GetRomSectionBaseAddresses(sourceBytes);

            // Return default if no AppleRomInformation GUID was found.
            if (romSectionBaseAddresses.Count == 0)
            {
                return DefaultRomInformationBase();
            }

            // Process each AppleRomInformation section.
            foreach (int sectionBaseAddress in romSectionBaseAddresses)
            {
                byte[] romSectionBytes = ExtractSectionBytes(sourceBytes, sectionBaseAddress);

                if (romSectionBytes == null)
                {
                    return DefaultRomInformationBase();
                }

                // Extract information based on each signature.
                foreach (byte[] signature in romInfoData.Keys.ToList())
                {
                    int dataBaseAddress = BinaryTools.GetBaseAddress(romSectionBytes, signature);

                    if (dataBaseAddress != -1)
                    {
                        int keyLength = signature.Length;

                        byte[] infoBytes = BinaryTools.GetBytesDelimited(romSectionBytes, dataBaseAddress + keyLength, indexByte, terminationByte);

                        if (infoBytes != null)
                        {
                            romInfoData[signature] = _utf8.GetString(infoBytes);
                        }
                    }
                }

                return new AppleRomInformationSection
                {
                    SectionExists = true,
                    SectionBytes = romSectionBytes,
                    SectionBase = sectionBaseAddress,
                    BiosId = romInfoData[BIOS_ID_SIGNATURE],
                    Model = romInfoData[MODEL_SIGNATURE],
                    EfiVersion = romInfoData[EFI_VERSION_SIGNATURE],
                    BuiltBy = romInfoData[BUILT_BY_SIGNATURE],
                    DateStamp = romInfoData[DATE_SIGNATURE],
                    Revision = romInfoData[REVISION_SIGNATURE],
                    RomVersion = romInfoData[ROM_VERSION_SIGNATURE],
                    BuildcaveId = romInfoData[BUILDCAVE_ID_SIGNATURE],
                    BuildType = romInfoData[BUILD_TYPE_SIGNATURE],
                    Compiler = romInfoData[COMPILER_SIGNATURE]
                };
            }

            // Return default if no valid section was processed.
            return DefaultRomInformationBase();

            // Local function to locate all AppleRomInformation section GUIDs.
            List<int> GetRomSectionBaseAddresses(byte[] bytes)
            {
                List<int> addresses = new List<int>();
                int address = BinaryTools.GetBaseAddress(bytes, Guids.APPLE_ROM_INFO_GUID, (int)IFD.BIOS_REGION_BASE, (int)IFD.BIOS_REGION_LIMIT);

                while (address != -1)
                {
                    addresses.Add(address);
                    address = BinaryTools.GetBaseAddress(bytes, Guids.APPLE_ROM_INFO_GUID, address + 1, (int)IFD.BIOS_REGION_LIMIT);
                }

                return addresses;
            }

            // Local function to extract the section bytes.
            byte[] ExtractSectionBytes(byte[] bytes, int baseAddress)
            {
                const int headerLength = 24; // 18h
                const int dataLength = 2; // 2h

                byte[] sectionSizeBytes = BinaryTools.GetBytesBaseLength(bytes, baseAddress + headerLength, dataLength);

                int sectionSize = BitConverter.ToInt16(sectionSizeBytes, 0);

                return sectionSize > 6 ? BinaryTools.GetBytesBaseLength(bytes, baseAddress + headerLength, sectionSize) : null;
            }
        }

        internal static AppleRomInformationSection DefaultRomInformationBase()
        {
            return new AppleRomInformationSection
            {
                SectionExists = false,
                SectionBytes = null,
                SectionBase = -1,
                BiosId = null,
                Model = null,
                EfiVersion = null,
                BuiltBy = null,
                DateStamp = null,
                Revision = null,
                RomVersion = null,
                BuildcaveId = null,
                BuildType = null,
                Compiler = null
            };
        }

        internal static readonly byte[] BIOS_ID_SIGNATURE =
        {
            0x42, 0x49, 0x4F, 0x53,
            0x20, 0x49, 0x44, 0x3A
        };

        internal static readonly byte[] MODEL_SIGNATURE =
        {
            0x4D, 0x6F, 0x64, 0x65,
            0x6C, 0x3A
        };

        internal static readonly byte[] EFI_VERSION_SIGNATURE =
        {
            0x45, 0x46, 0x49, 0x20,
            0x56, 0x65, 0x72, 0x73,
            0x69, 0x6F, 0x6E, 0x3A
        };

        internal static readonly byte[] BUILT_BY_SIGNATURE =
        {
            0x42, 0x75, 0x69, 0x6C,
            0x74, 0x20, 0x62, 0x79,
            0x3A
        };

        internal static readonly byte[] DATE_SIGNATURE =
        {
            0x44, 0x61, 0x74, 0x65,
            0x3A
        };

        internal static readonly byte[] REVISION_SIGNATURE =
        {
            0x52, 0x65, 0x76, 0x69,
            0x73, 0x69, 0x6F, 0x6E,
            0x3A
        };

        internal static readonly byte[] ROM_VERSION_SIGNATURE =
        {
            0x52, 0x4F, 0x4D, 0x20,
            0x56, 0x65, 0x72, 0x73,
            0x69, 0x6F, 0x6E, 0x3A
        };

        internal static readonly byte[] BUILDCAVE_ID_SIGNATURE =
        {
            0x42, 0x75, 0x69, 0x6C,
            0x64, 0x63, 0x61, 0x76,
            0x65, 0x20, 0x49, 0x44,
            0x3A
        };

        internal static readonly byte[] BUILD_TYPE_SIGNATURE =
        {
            0x42, 0x75, 0x69, 0x6C,
            0x64, 0x20, 0x54, 0x79,
            0x70, 0x65, 0x3A
        };

        internal static readonly byte[] COMPILER_SIGNATURE =
        {
            0x43, 0x6F, 0x6D, 0x70,
            0x69, 0x6C, 0x65, 0x72,
            0x3A
        };
        #endregion

        #region EFI BIOS ID Section
        internal static EfiBiosIdSection GetEfiBiosIdSectionData(byte[] sourceBytes)
        {
            int guidBaseAddress = BinaryTools.GetBaseAddress(sourceBytes, Guids.EFI_BIOS_ID_GUID, (int)IFD.BIOS_REGION_BASE, (int)IFD.BIOS_REGION_LIMIT);

            if (guidBaseAddress == -1)
            {
                return DefaultEfiBiosIdSection();
            }


            int efiBiosIdBaseAddress = BinaryTools.GetBaseAddress(sourceBytes, EFI_BIOS_ID_SIGNATURE, guidBaseAddress);

            if (efiBiosIdBaseAddress == -1)
            {
                return DefaultEfiBiosIdSection();
            }

            int efiBiosIdLimitAddress = BinaryTools.GetBaseAddress(sourceBytes, new byte[] { 0x00, 0x00, 0x00 }, efiBiosIdBaseAddress);

            byte[] efiBiosIdBytes = BinaryTools.GetBytesBaseLimit(sourceBytes, efiBiosIdBaseAddress + EFI_BIOS_ID_SIGNATURE.Length, efiBiosIdLimitAddress);

            if (efiBiosIdBytes == null)
            {
                return DefaultEfiBiosIdSection();
            }

            efiBiosIdBytes = efiBiosIdBytes.Where(b => b != 0x00 && b != 0x20).ToArray();
            string efiBiosId = _utf8.GetString(efiBiosIdBytes);
            string[] parts = efiBiosId.Split((char)0x2E);

            if (parts.Length != 5)
            {
                return DefaultEfiBiosIdSection();
            }

            return new EfiBiosIdSection
            {
                ModelPart = parts[0],
                zzPart = parts[1],
                MajorPart = parts[2],
                MinorPart = parts[3],
                DatePart = parts[4],
            };
        }

        private static EfiBiosIdSection DefaultEfiBiosIdSection()
        {
            return new EfiBiosIdSection
            {
                ModelPart = null,
                zzPart = null,
                MajorPart = null,
                MinorPart = null,
                DatePart = null,
            };
        }

        internal static readonly byte[] EFI_BIOS_ID_SIGNATURE =
        {
            0x24, 0x49, 0x42, 0x49,
            0x4F, 0x53, 0x49, 0x24
        };
        #endregion

        #region APFSJumpStart
        internal static ApfsCapable GetIsApfsCapable(byte[] sourceBytes)
        {
            // APFS DXE GUID found.
            if (BinaryTools.GetBaseAddress(sourceBytes, Guids.APFS_DXE_GUID, (int)IFD.BIOS_REGION_BASE, (int)IFD.BIOS_REGION_LIMIT) != -1)
            {
                return ApfsCapable.Yes;
            }

            // Look for a compressed volume GUID.
            int lzmaDxeBaseAddress = BinaryTools.GetBaseAddress(
                sourceBytes, Guids.LZMA_DXE_VOLUME_IMAGE_1_GUID, (int)IFD.BIOS_REGION_BASE, (int)IFD.BIOS_REGION_LIMIT);

            if (lzmaDxeBaseAddress == -1)
            {
                lzmaDxeBaseAddress = BinaryTools.GetBaseAddress(
                    sourceBytes, Guids.LZMA_DXE_VOLUME_IMAGE_0_GUID, (int)IFD.BIOS_REGION_BASE, (int)IFD.BIOS_REGION_LIMIT);
            }

            // No compressed DXE volume was found.
            if (lzmaDxeBaseAddress == -1)
            {
                return ApfsCapable.No;
            }

            // Get bytes containing section length (0x3).
            byte[] dataLengthBytes = BinaryTools.GetBytesBaseLength(sourceBytes, lzmaDxeBaseAddress + 20, 3);

            // Convert section length bytes to int24.
            int sectionSize = Helper.ToInt24(dataLengthBytes);

            // Determine the end of the lzma guid section.
            int lzmaDxeLimitAddress = lzmaDxeBaseAddress + sectionSize;

            // Search for the LZMA signature byte.
            lzmaDxeBaseAddress = BinaryTools.GetBaseAddress(sourceBytes, new byte[] { 0x5D }, lzmaDxeBaseAddress + GUID_SIZE);

            // Decompress the LZMA volume.
            byte[] decompressedBytes = LzmaCoder.DecompressBytes(BinaryTools.GetBytesBaseLimit(sourceBytes, lzmaDxeBaseAddress, lzmaDxeLimitAddress));

            // There was an issue decompressing the volume (Error saved to './mefit.log').
            if (decompressedBytes == null)
            {
                return ApfsCapable.Unknown;
            }

            // Search the decompressed volume for the APFS DXE GUID.
            if (BinaryTools.GetBaseAddress(decompressedBytes, Guids.APFS_DXE_GUID) == -1)
            {
                // The APFS DXE GUID was not found in the compressed volume.
                return ApfsCapable.No;
            }

            // The APFS DXE GUID was present in the compressed volume.
            return ApfsCapable.Yes;
        }
        #endregion

        #region Functions
        /// <summary>
        /// Patches the given Fsys store byte array with a new CRC value.
        /// </summary>
        /// <param name="fsysStore">The byte array representing the Fsys store.</param>
        /// <param name="newCrc">The new CRC value to be patched.</param>
        /// <returns>The patched Fsys store byte array.</returns>
        internal static byte[] PatchFsysCrc(byte[] fsysStore, uint newCrc)
        {
            int storeLength = fsysStore.Length;

            // Check if the size of the byte array is valid
            if (storeLength < EFIROM.FSYS_RGN_SIZE)
            {
                throw new ArgumentException(nameof(PatchFsysCrc), $"Given byes are too small: {storeLength:X2}h");
            }

            if (storeLength > EFIROM.FSYS_RGN_SIZE)
            {
                throw new ArgumentException(nameof(PatchFsysCrc), $"Given bytes are too large: {storeLength:X2}h");
            }

            // Convert the new CRC value to bytes
            byte[] newCrcBytes = BitConverter.GetBytes(newCrc);

            // Write the new bytes back to the Fsys store at the appropriate base
            BinaryTools.OverwriteBytesAtBase(fsysStore, EFIROM.FSYS_RGN_SIZE - EFIROM.CRC32_SIZE, newCrcBytes);

            // Return the patched data
            return fsysStore;
        }

        /// <summary>
        /// Patches a binaries Fsys store with the correct crc value.
        /// </summary>
        /// <param name="sourceBytes">The byte array representing the source binary file.</param>
        /// <param name="fsysBase">The base of the Fsys store within the binary file.</param>
        /// <param name="fsysStore">The byte array representing the Fsys store.</param>
        /// <param name="uiNewCrc">The new CRC value to be patched in the Fsys store.</param>
        /// <returns>The patched file byte array, or null if the new calculated crc does not match the crc in the Fsys store.</returns>
        internal static byte[] MakeFsysCrcPatchedBinary(byte[] sourceBytes, int fsysBase, byte[] fsysStore, uint uiNewCrc)
        {
            Logger.Write($"{nameof(MakeFsysCrcPatchedBinary)}: {LOGSTRINGS.CREATING_BUFFERS}", LogType.Application);

            // Create a new byte array to hold the patched binary.
            byte[] patchedBytes = new byte[sourceBytes.Length];

            Array.Copy(sourceBytes, patchedBytes, sourceBytes.Length);

            Logger.Write($"{nameof(MakeFsysCrcPatchedBinary)}: " + $"{LOGSTRINGS.CRC_PATCH}", LogType.Application);

            // Patch the Fsys store crc.
            byte[] patchedStore = PatchFsysCrc(fsysStore, uiNewCrc);

            Logger.Write($"{nameof(MakeFsysCrcPatchedBinary)}: {LOGSTRINGS.CRC_WRITE_TO_FW}", LogType.Application);

            // Overwrite the loaded Fsys crc32 with the newly calculated crc32.
            BinaryTools.OverwriteBytesAtBase(patchedBytes, fsysBase, patchedStore);

            // Load the Fsys store from the new binary.
            FsysStore newBinaryFsys = EFIROM.GetFsysStoreData(patchedBytes, false);

            // Compare the new checksums.
            if (newBinaryFsys.CrcString != newBinaryFsys.CrcCalcString)
            {
                Logger.Write($"{nameof(MakeFsysCrcPatchedBinary)}: {LOGSTRINGS.CRC_WRITE_FAIL}", LogType.Application);

                return null;
            }

            Logger.Write($"{nameof(MakeFsysCrcPatchedBinary)}: {LOGSTRINGS.CRC_WRITE_SUCCESS}", LogType.Application);

            return patchedBytes;
        }

        /// <summary>
        /// Invalidates an SVS store Message Authentication Code, removing EFI Lock.
        /// </summary>
        /// <param name="storeData">The SVS store to unlock.</param>
        /// <param name="lockCrcbase">The Message Authentication Code CRC base</param>
        internal static byte[] PatchSvsStoreMac(byte[] storeData, int lockCrcbase)
        {
            // Some sanity checks.
            if (storeData == null || storeData.Length < 16)
            {
                return null;
            }

            // Write 0xFh length zeros over the MAC CRC from lockBase
            BinaryTools.OverwriteBytesAtBase(storeData, lockCrcbase, new byte[0x0F]);

            // Returned the unlocked store
            return storeData;
        }
        #endregion
    }
}