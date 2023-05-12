﻿// Mac EFI Toolkit
// https://github.com/MuertoGB/MacEfiToolkit

// Core Components
// Filesystem.cs - Contains GUIDs and signature bytes
// Updated 03.05.23 - Edit sigs
// Released under the GNU GLP v3.0

namespace Mac_EFI_Toolkit.Core
{
    class Filesystem
    {
        // GUIDs
        internal static readonly byte[] LZMA_DXE_OLD_GUID =
        {
            0xDB, 0x7F, 0xAD, 0x77,
            0x2A, 0xDF, 0x02, 0x43,
            0x88, 0x98, 0xC7, 0x2E,
            0x4C, 0xDB, 0xD0, 0xF4
        };
        internal static readonly byte[] LZMA_DXE_NEW_GUID =
        {
            0x79, 0xDE, 0xD3, 0x2A,
            0xE9, 0x63, 0x4F, 0x9B,
            0xB6, 0x4F, 0xE7, 0xC6,
            0x18, 0x1B, 0x0C, 0xEC
        };
        internal static readonly byte[] APFS_DXE_GUID =
        {
            0xF4, 0x32, 0xFB, 0xCF,
            0xA8, 0xC2, 0xBB, 0x48,
            0xA0, 0xEB, 0x6C, 0x3C,
            0xCA, 0x3F, 0xE8, 0x47
        };
        internal static readonly byte[] ROM_INFO_GUID =
        {
            0xF6, 0xAB, 0x35, 0xB5,
            0x7D, 0x96, 0xF2, 0x43,
            0xB4, 0x94, 0xA1, 0xEB,
            0x8E, 0x21, 0xA2, 0x8E
        };
        internal static readonly byte[] NV_DATA_GUID =
        {
            0x8D, 0x2B, 0xF1, 0xFF,
            0x96, 0x76, 0x8B, 0x4C,
            0xA9, 0x85, 0x27, 0x47,
            0x07, 0x5B, 0x4F, 0x50
        };

        // Signatures
        internal static readonly byte[] FSYS_SIG =
        {
            0x46, 0x73, 0x79, 0x73,
            0x01
        };
        internal static readonly byte[] SSN_LOWER_SIG =
        {
            0x73, 0x73, 0x6E
        };
        internal static readonly byte[] SSN_UPPER_SIG =
        {
            0x53, 0x53, 0x4E
        };
        internal static readonly byte[] SON_SIG =
        {
            0x03, 0x73, 0x6F, 0x6E
        };
        internal static readonly byte[] HWC_SIG =
        {
            0x03, 0x68, 0x77,  0x63
        };
        internal static readonly byte[] SVS_SIG =
        {
            0x24, 0x53, 0x56, 0x53
        };
        internal static readonly byte[] VSS_SIG =
        {
            0x24, 0x56, 0x53, 0x53
        };
        internal static readonly byte[] BID_SIG =
        {
            0xF8, 0x7C, 0x00, 0x00,
            0x19
        };
        internal static readonly byte[] ABIOS_SIG =
        {
            0x41, 0x70, 0x70, 0x6C,
            0x65, 0x20, 0x52, 0x4F,
            0x4D
        };
        internal static readonly byte[] IBIOS_SIG =
        {
            0x24, 0x49, 0x42, 0x49,
            0x4F, 0x53, 0x49, 0x24
        };
        internal static readonly byte[] EFIVER_SIG =
        {
            0x45, 0x46, 0x49, 0x20,
            0x56, 0x65, 0x72, 0x73,
            0x69, 0x6F, 0x6E, 0x3A
        };
        internal static readonly byte[] ROMVER_SIG =
        {
            0x52, 0x4F, 0x4D, 0x20,
            0x56, 0x65, 0x72, 0x73,
            0x69, 0x6F, 0x6E, 0x3A
        };
        internal static readonly byte[] FPT_HEADER_SIG =
{
            0x24, 0x46, 0x50, 0x54
        };
        internal static readonly byte[] MN2_SIG =
        {
            0x24, 0x4D, 0x4E, 0x32
        };
        internal static readonly byte[] FLASH_DESC_SIG =
        {
            0x5A, 0xA5, 0xF0, 0x0F
        };
    }
}