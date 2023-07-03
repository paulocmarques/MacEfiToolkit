﻿// Mac EFI Toolkit
// https://github.com/MuertoGB/MacEfiToolkit

// Descriptor.cs
// Released under the GNU GLP v3.0
// This code is a work in progress.

using Mac_EFI_Toolkit.Utils;
using System.Linq;
using System.Runtime.InteropServices;

namespace Mac_EFI_Toolkit.Common
{

    #region Structs
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct FlashDescriptor
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x10)]
        internal byte[] ReservedVector;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x4)]
        internal byte[] Signature;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x2C)]
        internal byte[] Unknown;  // We do not need this chunk of data
        internal ushort DescriptorBase;
        internal ushort DescriptorLimit;
        internal ushort BiosBase;
        internal ushort BiosLimit;
        internal ushort MeBase;
        internal ushort MeLimit;
        internal ushort GbeBase;
        internal ushort GbeLimit;
        internal ushort PdrBase;
        internal ushort PdrLimit;
    }
    #endregion

    class Descriptor
    {

        #region Internal Members
        internal const uint DESCRIPTOR_BASE = 0x0;
        internal const uint DESCRIPTOR_LENGTH = 0x1000;

        internal static uint BiosBase = 0;
        internal static uint BiosLimit = 0;
        internal static uint BiosSize = 0;
        internal static uint MeBase = 0;
        internal static uint MeLimit = 0;
        internal static uint MeSize = 0;
        internal static uint PdrBase = 0;
        internal static uint PdrLimit = 0;
        internal static uint PdrSize = 0;

        internal static bool IsValid = false;
        #endregion

        internal static uint CalculateRegionBase(ushort basePosition)
        {
            // For example:
            // BIOS Base:  LE: 3701h > 137h * 1000h = A bios base of 137000h
            return basePosition * DESCRIPTOR_LENGTH;
        }

        internal static uint CalculateRegionSize(ushort basePosition, ushort limitPosition)
        {
            if (limitPosition != 0)
                return (uint)(limitPosition + 0x1 - basePosition) * DESCRIPTOR_LENGTH;

            // For example:
            // BIOS Size: LE: FF07h > (7FFh + 1) = 800h * 1000h - LE: 3701h > 137h * 1000h = 6C9000h
            return 0;
        }

        internal static void Parse(byte[] sourceBytes)
        {
            // Read in the flash descriptor
            byte[] fdRegionBytes = BinaryUtils.GetBytesBaseLength(sourceBytes, (int)DESCRIPTOR_BASE, (int)DESCRIPTOR_LENGTH);
            // Deserialize the descriptor
            FlashDescriptor descriptor = Helper.DeserializeHeader<FlashDescriptor>(fdRegionBytes);

            // Match flash descriptor tag (5AA5F00F)
            IsValid = (descriptor.Signature.SequenceEqual(FLASH_DESC_SIGNATURE)) ? true : false;

            if (IsValid)
            {
                // BIOS base, size, limit
                BiosBase = CalculateRegionBase(descriptor.BiosBase);
                if (BiosBase > sourceBytes.Length) BiosBase = 0;
                BiosSize = CalculateRegionSize(descriptor.BiosBase, descriptor.BiosLimit);
                BiosLimit = BiosBase + BiosSize;

                // Management Engine base, size, limit
                MeBase = CalculateRegionBase(descriptor.MeBase);
                if (MeBase > sourceBytes.Length) MeBase = 0;
                MeSize = CalculateRegionSize(descriptor.MeBase, descriptor.MeLimit);
                MeLimit = MeBase + MeSize;

                // Platform Data Region base, size, limit
                PdrBase = CalculateRegionBase(descriptor.PdrBase);
                if (PdrBase > sourceBytes.Length) PdrBase = 0;
                PdrSize = CalculateRegionSize(descriptor.PdrBase, descriptor.PdrLimit);
                PdrLimit = PdrBase + PdrSize;
            }
        }

        internal static void ResetValues()
        {
            BiosBase = 0;
            BiosLimit = 0;
            BiosSize = 0;
            MeBase = 0;
            MeLimit = 0;
            MeSize = 0;
            PdrBase = 0;
            PdrLimit = 0;
            PdrSize = 0;

            IsValid = false;
        }

        internal static readonly byte[] FLASH_DESC_SIGNATURE =
        {
            0x5A, 0xA5, 0xF0, 0x0F
        };

    }
}