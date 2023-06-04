﻿// Mac EFI Toolkit
// https://github.com/MuertoGB/MacEfiToolkit

// BinaryUtils.cs - A collection of functions to handle binary data.
// This code uses the Knuth-Morris-Pratt algorithm for pattern matching and string searching, developed by Donald Knuth, Vaughan Pratt, and James Morris.
// Released under the GNU GLP v3.0

using System;
using System.IO;
using System.Linq;

namespace Mac_EFI_Toolkit.Utils
{
    internal class BinaryUtils
    {

        #region Binary Find
        /// <summary>
        /// Finds the offset of a byte pattern within a byte array.
        /// </summary>
        /// <param name="sourceBytes">The byte array to search in.</param>
        /// <param name="pattern">The byte pattern to search for.</param>
        /// <returns>The offset of the byte pattern within the byte array, or -1 if the pattern is not found.</returns>
        internal static long GetOffset(byte[] sourceBytes, byte[] pattern)
        {
            // Call the overload that takes a baseOffset parameter and sets it to 0.
            return GetOffset(sourceBytes, pattern, 0);
        }

        /// <summary>
        /// Finds the offset of a byte pattern within a byte array, starting at a specified base offset.
        /// </summary>
        /// <param name="sourceBytes">The byte array to search in.</param>
        /// <param name="pattern">The byte pattern to search for.</param>
        /// <param name="baseOffset">The base offset to start the search from.</param>
        /// <returns>The offset of the byte pattern within the byte array, or -1 if the pattern is not found.</returns>
        internal static long GetOffset(byte[] sourceBytes, byte[] pattern, long baseOffset)
        {
            // Call the overload that takes a baseOffset and maxSearchLength parameters and sets maxSearchLength to the remaining length of the sourceBytes array.
            return GetOffset(sourceBytes, pattern, baseOffset, sourceBytes.Length - baseOffset);
        }

        /// <summary>
        /// Finds the offset of a byte pattern within a byte array, starting at a specified base offset and limiting the search length.
        /// </summary>
        /// <param name="sourceBytes">The byte array to search in.</param>
        /// <param name="patternBytes">The byte pattern to search for.</param>
        /// <param name="baseOffset">The base offset to start the search from.</param>
        /// <param name="maxSearchLength">The maximum length of the search within the byte array.</param>
        /// <returns>The offset of the byte pattern within the byte array, or -1 if the pattern is not found.</returns>
        internal static long GetOffset(byte[] sourceBytes, byte[] patternBytes, long baseOffset, long maxSearchLength)
        {
            // Ensure that maxSearchLength is within the bounds of the sourceBytes array.
            maxSearchLength = Math.Min(maxSearchLength, sourceBytes.Length - baseOffset);

            // Build the partial match table for the pattern using the Knuth-Morris-Pratt algorithm.
            int[] partialMatchTable = BuildPartialMatchTable(patternBytes);

            // Initialize the source and pattern indices.
            int sourceIndex = (int)baseOffset;
            int patternIndex = 0;

            // Iterate over the source bytes until the end or until the pattern is found or the maximum search length is reached.
            while (sourceIndex < sourceBytes.Length && sourceIndex - baseOffset < maxSearchLength)
            {
                if (sourceBytes[sourceIndex] == patternBytes[patternIndex])
                {
                    // If the source byte matches the pattern byte, increment the indices.
                    sourceIndex++;
                    patternIndex++;

                    // If the pattern has been fully matched, return the offset.
                    if (patternIndex == patternBytes.Length)
                    {
                        return sourceIndex - patternIndex;
                    }
                }
                else if (patternIndex > 0)
                {
                    // If the source byte does not match and we have partially matched the pattern, backtrack the pattern index.
                    patternIndex = partialMatchTable[patternIndex - 1];
                }
                else
                {
                    // If the source byte does not match and we have not partially matched the pattern, increment the source index.
                    sourceIndex++;
                }
            }

            // If the pattern is not found within the maximum search length, return -1.
            return -1;
        }

        /// <summary>
        /// Builds the partial match table for a byte pattern using the Knuth-Morris-Pratt algorithm.
        /// </summary>
        /// <param name="patternBytes">The byte pattern to build the table for.</param>
        /// <returns>An array of integers representing the partial match table.</returns>
        private static int[] BuildPartialMatchTable(byte[] patternBytes)
        {
            int[] table = new int[patternBytes.Length];
            int i = 0;
            int j = 1;
            while (j < patternBytes.Length)
            {
                if (patternBytes[i] == patternBytes[j])
                {
                    i++;
                    table[j] = i;
                    j++;
                }
                else if (i > 0)
                {
                    i = table[i - 1];
                }
                else
                {
                    table[j] = 0;
                    j++;
                }
            }
            return table;
        }
        #endregion

        #region Binary Read
        /// <summary>
        /// Reads a specified number of bytes from a byte array at a given offset.
        /// </summary>
        /// <param name="sourceBytes">The byte array to read from.</param>
        /// <param name="baseOffset">The offset in the byte array to read from.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <returns>The bytes read from the byte array.</returns>
        internal static byte[] GetBytesAtOffset(byte[] sourceBytes, long baseOffset, int length)
        {
            if (baseOffset < 0 || baseOffset + length > sourceBytes.Length) return null;

            byte[] buffer = new byte[length];
            Buffer.BlockCopy(sourceBytes, (int)baseOffset, buffer, 0, length);
            return buffer;
        }

        /// <summary>
        /// Reads a specified number of bytes from a byte array at a given offset.
        /// </summary>
        /// <param name="sourceBytes">The byte array to read from.</param>
        /// <param name="baseOffset">The starting offset in the byte array to read from.</param>
        /// <param name="endOffset">The ending offset in the byte array to read from.</param>
        /// <returns>The bytes read from the byte array.</returns>
        internal static byte[] GetBytesBetweenOffsets(byte[] sourceBytes, long baseOffset, long endOffset)
        {
            if (endOffset <= baseOffset) return new byte[0]; // Nothing to read

            long length = endOffset - baseOffset;
            var segment = new ArraySegment<byte>(sourceBytes, (int)baseOffset, (int)length);
            return segment.ToArray();
        }

        /// <summary>
        /// Reads bytes from a byte array starting from a specified offset and up to a specified terminating byte.
        /// </summary>
        /// <param name="sourceBytes">The byte array to read from.</param>
        /// <param name="baseOffset">The offset in the byte array to start reading from.</param>
        /// <param name="startByte">The starting byte to read from.</param>
        /// <param name="terminationBytes">The terminating byte params to stop reading at.</param>
        /// <returns>The bytes read from the byte array up to the terminating byte.</returns>
        internal static byte[] GetBytesAtOffsetByteDelimited(byte[] sourceBytes, long baseOffset, byte startByte, params byte[] terminationBytes)
        {
            int startIndex = Array.IndexOf(sourceBytes, startByte, (int)baseOffset);
            if (startIndex < 0 || startIndex == sourceBytes.Length - 1)
                return null;

            startIndex++;
            while (startIndex < sourceBytes.Length && sourceBytes[startIndex] == startByte)
            {
                startIndex++;
            }

            using (MemoryStream ms = new MemoryStream())
            {
                while (startIndex < sourceBytes.Length && !terminationBytes.Contains(sourceBytes[startIndex]))
                {
                    ms.WriteByte(sourceBytes[startIndex]);
                    startIndex++;
                }

                return ms.ToArray();
            }
        }
        #endregion

        #region Binary Edit
        /// <summary>
        /// Overwrites a sequence of bytes in a byte array at a given offset with new bytes.
        /// </summary>
        /// <param name="sourceBytes">The byte array to overwrite.</param>
        /// <param name="baseOffset">The offset in the byte array to overwrite at.</param>
        /// <param name="newBytes">The new bytes to write.</param>
        internal static void OverwriteBytesAtOffset(byte[] sourceBytes, long baseOffset, byte[] newBytes)
        {
            if (baseOffset < 0 || baseOffset + newBytes.Length > sourceBytes.Length)
                throw new ArgumentOutOfRangeException(nameof(baseOffset), "Offset is out of range.");

            Buffer.BlockCopy(newBytes, 0, sourceBytes, (int)baseOffset, newBytes.Length);
        }

        /// <summary>
        /// Removes any trailing 0xFF bytes from a byte array.
        /// </summary>
        /// <param name="sourceBytes">The byte array to remove the trailing 0xFF bytes from.</param>
        /// <returns>The byte array with any trailing 0xFF bytes removed.</returns>
        internal static byte[] RemoveTrailingFFPadding(byte[] sourceBytes)
        {
            int end = sourceBytes.Length - 1;
            while (end >= 0 && sourceBytes[end] == 0xFF)
            {
                end--;
            }
            if (end < 0)
            {
                return new byte[0];
            }

            byte[] result = new byte[end + 1];
            Array.Copy(sourceBytes, result, end + 1);
            return result;
        }
        #endregion

    }
}