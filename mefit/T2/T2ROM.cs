﻿// Mac EFI Toolkit
// https://github.com/MuertoGB/MacEfiToolkit

// T2ROM.cs - Handles parsing of T2 SOCROM data
// Released under the GNU GLP v3.0

using Mac_EFI_Toolkit.T2.Structs;
using Mac_EFI_Toolkit.Utils;
using Mac_EFI_Toolkit.Utils.Structs;
using System.Text;

namespace Mac_EFI_Toolkit.T2
{
    internal class T2ROM
    {

        #region Internal Members
        internal static string LoadedBinaryPath = null;
        internal static byte[] LoadedBinaryBytes = null;

        internal static string iBootVersion = null;
        internal static string ConfigCode = null;
        internal static Binary FileInfoData;
        internal static SCfgData ScfgSectionData;
        #endregion

        #region Private Members
        const int _serialLength = 12;
        private static readonly byte[] _limitChars = new byte[] { 0x00, 0x00, 0x00 };
        private static readonly Encoding _utf8 = Encoding.UTF8;
        #endregion

        #region Parse Fimware
        internal static void LoadFirmwareBaseData(byte[] sourceBytes, string fileName)
        {
            // Parse file info
            FileInfoData =
                FileUtils.GetBinaryFileInfo
                (fileName);

            // Parse iBoot version
            iBootVersion = GetIbootVersion(sourceBytes);

            // Parse Scfg Store data
            ScfgSectionData = GetSCfgData(sourceBytes);

            // Fetch the Config Code
            ConfigCode = ScfgSectionData.HWC != null
                ? MacUtils.GetDeviceConfigCodeLocalLocal(ScfgSectionData.HWC)
                : null;
        }

        internal static void ResetFirmwareBaseData()
        {
            FileInfoData = default;
            iBootVersion = null;
            ConfigCode = null;
            ScfgSectionData = default;
        }

        internal static bool IsValidImage(byte[] source)
        {
            int ibootSig =
                BinaryUtils.GetBaseAddress(
                    source,
                    ROMSigs.IBOOT_VER_SIG,
                    0);

            return (ibootSig != -1);
        }
        #endregion

        #region IBoot
        internal static string GetIbootVersion(byte[] source)
        {
            int ibootSig =
                BinaryUtils.GetBaseAddress(
                    source,
                    ROMSigs.IBOOT_VER_SIG, 0);

            if (ibootSig != -1) // Signature found
            {
                // Get byte containing data length
                byte[] lByte =
                    BinaryUtils.GetBytesBaseLength(
                    source,
                    ibootSig + ROMSigs.IBOOT_VER_SIG.Length + 1,
                    1);

                // Convert data length to unsigned int8
                byte dataSize = (byte)lByte[0];

                // Get iboot version data bytes
                byte[] stringData =
                    BinaryUtils.GetBytesBaseLength(
                        source,
                        ibootSig + 0x6,
                        dataSize);

                return _utf8.GetString(stringData);
            }

            return AppStrings.AS_UNKNOWN;
        }
        #endregion

        #region SCfg
        internal static SCfgData GetSCfgData(byte[] source)
        {
            string serial = string.Empty;
            string hwc = string.Empty;
            string son = string.Empty;
            string regno = string.Empty;

            int scfgBase =
                BinaryUtils.GetBaseAddress(
                    source,
                    ROMSigs.SCFG_HEADER_SIG);

            if (scfgBase == -1)
                return DefaultScfgData();

            // Get byte containing scfg store length
            byte[] lByte =
                BinaryUtils.GetBytesBaseLength(
                    source,
                    scfgBase + ROMSigs.SCFG_HEADER_SIG.Length,
                    1);

            // Convert data length to unsigned int8
            byte dataSize = (byte)lByte[0];

            if (dataSize == 0)
                return DefaultScfgData();

            // Extract Scfg store into buffer
            byte[] scfgBytes =
                BinaryUtils.GetBytesBaseLength(
                    source,
                    scfgBase,
                    dataSize);

            // Get the serial number
            int serialBase = BinaryUtils.GetBaseAddress(scfgBytes, ROMSigs.SCFG_SSN_SIG);

            if (serialBase == -1)
            {
                serial = null;
            }
            else
            {
                byte[] serialBytes = BinaryUtils.GetBytesBaseLength(scfgBytes, serialBase + ROMSigs.SCFG_SSN_SIG.Length, _serialLength);

                if (serialBytes?.Length == _serialLength)
                {
                    serial = _utf8.GetString(serialBytes);
                    hwc = serial.Length >= 4 ? serial.Substring(serial.Length - 4, 4) : null;
                }
                else
                {
                    serial = null;
                }
            }

            // Get the system order number
            int sonBase = BinaryUtils.GetBaseAddress(scfgBytes, ROMSigs.SCFG_SON_SIG);

            if (sonBase == -1)
            {
                son = null;
            }
            else
            {
                sonBase += ROMSigs.SCFG_SON_SIG.Length;
                int sonLimit = BinaryUtils.GetBaseAddress(scfgBytes, _limitChars, sonBase);

                if (sonLimit == -1)
                {
                    son = null;
                }
                else
                {
                    byte[] sonBytes = BinaryUtils.GetBytesBaseLimit(scfgBytes, sonBase, sonLimit);
                    son = _utf8.GetString(sonBytes);
                }
            }

            // Get the registration? number
            int regnBase = BinaryUtils.GetBaseAddress(scfgBytes, ROMSigs.SCFG_SON_REGN);

            if (regnBase == -1)
            {
                regno = null;
            }
            else
            {
                regnBase += ROMSigs.SCFG_SON_REGN.Length;
                int regnLimit = BinaryUtils.GetBaseAddress(scfgBytes, _limitChars, regnBase);

                if (regnLimit == -1)
                {
                    son = null;
                }
                else
                {
                    byte[] regnBytes = BinaryUtils.GetBytesBaseLimit(scfgBytes, regnBase, regnLimit);
                    regno = _utf8.GetString(regnBytes);
                }
            }

            return new SCfgData
            {
                StoreBase = scfgBase,
                StoreSize = dataSize,
                StoreBytes = scfgBytes,
                SerialText = serial,
                HWC = hwc,
                SonText = son,
                MdlC = null,
                RegNumText = regno
            };
        }

        private static SCfgData DefaultScfgData()
        {
            return new SCfgData
            {
                StoreBase = -1,
                StoreSize = 0,
                StoreBytes = null,
                SerialText = null,
                HWC = null,
                SonText = null,
                MdlC = null,
                RegNumText = null
            };
        }
        #endregion

    }
}