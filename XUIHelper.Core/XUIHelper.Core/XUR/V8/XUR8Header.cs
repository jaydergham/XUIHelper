﻿using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XUIHelper.Core.Extensions;

namespace XUIHelper.Core
{
    public class XUR8Header : IXURHeader
    {
        public const int ExpectedVersion = 0x00000008;

        public int Magic { get; private set; }
        public int Version { get; private set; }
        public int Flags { get; private set; }
        public short ToolVersion { get; private set; }
        public int FileSize { get; private set; }
        public short SectionsCount { get; private set; }

        public async Task<bool> TryReadAsync(IXUR xur, BinaryReader reader)
        {
            try
            {
                xur.Logger = xur.Logger?.ForContext(typeof(XUR8Header));

                xur.Logger?.Here().Verbose("Reading XUR8 header.");
                Magic = reader.ReadInt32BE();
                if (Magic != IXURHeader.ExpectedMagic)
                {
                    xur.Logger?.Here().Error("Read magic was not the expected value, returning false. Expected: {0}, Actual: {1}", IXURHeader.ExpectedMagic, Magic);
                    return false;
                }

                Version = reader.ReadInt32BE();
                if (Version != ExpectedVersion)
                {
                    xur.Logger?.Here().Error("Read version was not the expected value, returning false. Expected: {0}, Actual: {1}", ExpectedVersion, Version);
                    return false;
                }

                Flags = reader.ReadInt32BE();
                xur.Logger?.Here().Verbose("Flags is {0:X8}", Flags);

                ToolVersion = reader.ReadInt16BE();
                xur.Logger?.Here().Verbose("ToolVersion is {0:X8}", ToolVersion);

                FileSize = reader.ReadInt32BE();
                if (FileSize != reader.BaseStream.Length)
                {
                    xur.Logger?.Here().Error("Read file size didn't match, returning false. Expected: {0}, Actual: {1}", FileSize, reader.BaseStream.Length);
                    return false;
                }

                SectionsCount = reader.ReadInt16BE();
                xur.Logger?.Here().Verbose("Sections count is {0:X8}", SectionsCount);

                xur.Logger?.Here().Verbose("XUR8 header read successful!");
                return true;

            }
            catch (Exception ex)
            {
                xur.Logger?.Here().Error("Caught an exception when reading XUR8 header, returning false. The exception is: {0}", ex);
                return false;
            }
        }
    }
}