﻿using Serilog.Events;
using Serilog;
using XUIHelper.Core;
using System.Formats.Tar;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.IO;

namespace XUIHelper.Tests
{
    public class XUR12Tests
    {
        private ILogger _Log;

        [SetUp]
        public void Setup()
        {
            string logPath = Path.Combine(@"F:\Code Repos\XUIHelper\XUIHelper.Core\XUIHelper.Core\Debug", string.Format("Tests Log {0}.log", DateTime.Now.ToString("yyyy - MM - dd HHmmss")));
            var outputTemplate = "({Timestamp:HH:mm:ss.fff}) {Level}: [{LineNumber}]{SourceContext}::{MemberName} - {Message}{NewLine}";
            _Log = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.File(logPath, LogEventLevel.Verbose, outputTemplate)
            .CreateLogger();

            RegisterExtensions(_Log);
        }

        private void RegisterExtensions(ILogger? logger = null)
        {
            XMLExtensionsManager v5Extensions = new XMLExtensionsManager(logger);
            _ = v5Extensions.TryRegisterXMLExtensionsAsync(@"F:\Code Repos\XUIHelper\XUIHelper.Core\XUIHelper.Core\Assets\V5\XuiElements.xml");
            _ = v5Extensions.TryRegisterXMLExtensionsAsync(@"F:\Code Repos\XUIHelper\XUIHelper.Core\XUIHelper.Core\Assets\V5\9199DashElements.xml");
            _ = v5Extensions.TryRegisterXMLExtensionsAsync(@"F:\Code Repos\XUIHelper\XUIHelper.Core\XUIHelper.Core\Assets\V5\9199HUDElements.xml");
            XUIHelperCoreConstants.VersionedExtensions[0x5] = v5Extensions;
        }

        private async Task<XUI12?> GetReadXUI(string filePath, int extensionVersion, ILogger? logger = null)
        {
            XUI12 xui = new XUI12(filePath, logger);
            if (await xui.TryReadAsync(0x5))
            {
                return xui;
            }

            return null;
        }

        private bool AreFilesEqual(string filePathOne, string filePathTwo)
        {
            if (!File.Exists(filePathOne))
            {
                return false;
            }

            if (!File.Exists(filePathTwo))
            {
                return false;
            }

            int filePathOneLineCount = File.ReadAllLines(filePathOne).Length;
            int filePathTwoLineCount = File.ReadAllLines(filePathTwo).Length;
            int diff = filePathOneLineCount - filePathTwoLineCount;
            return diff >= -1 && diff <= 1;
        }

        [Test]
        public async Task CheckAllReadsSuccessful()
        {
            List<string> successfulXUIs = new List<string>();
            List<string> failedXUIs = new List<string>();

            int xuisCount = 0;
            foreach (string xuiFile in Directory.GetFiles(Path.Combine(TestContext.CurrentContext.TestDirectory, "Test Data/XUI/9199"), "*.xui", SearchOption.AllDirectories))
            {
                XUI12 xui = new XUI12(xuiFile, null);
                if (!await xui.TryReadAsync(0x5))
                {
                    failedXUIs.Add(xuiFile);
                }
                else
                {
                    successfulXUIs.Add(xuiFile);
                }

                xuisCount++;
            }

            int totalXUIsCount = successfulXUIs.Count + failedXUIs.Count;
            float successPercentage = (successfulXUIs.Count / (float)totalXUIsCount) * 100.0f;

            _Log.Information("==== XUI12 ALL READS ====");
            _Log.Information("Total: {0}, Successful: {1}, Failed: {2} ({3}%)", totalXUIsCount, successfulXUIs.Count, failedXUIs.Count, successPercentage);
            _Log.Information("");
            _Log.Information("==== SUCCESSFUL XUIS ====");
            _Log.Information(string.Join("\n", successfulXUIs));
            _Log.Information("");
            _Log.Information("==== FAILED XUIS ====");
            _Log.Information(string.Join("\n", failedXUIs));
            _Log.Information("");

            Assert.True(failedXUIs.Count == 0);
        }

        [Test]
        public async Task CheckSingleXUIReadSuccessful()
        {
            string xuiFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Test Data/XUI/9199/EditorSkin.xui");
            XUI12 xui = new XUI12(xuiFile, _Log);
            Assert.True(await xui.TryReadAsync(0x5));
        }

        [Test]
        public async Task CheckAllWritesSuccessful()
        {
            List<XUI12> readXUIs = new List<XUI12>();

            int xuisCount = 0;
            foreach (string xuiFile in Directory.GetFiles(Path.Combine(TestContext.CurrentContext.TestDirectory, "Test Data/XUI/9199"), "*.xui", SearchOption.AllDirectories))
            {
                XUI12 xur = new XUI12(xuiFile, null);
                if (await xur.TryReadAsync(0x5))
                {
                    xuisCount++;
                    readXUIs.Add(xur);
                }
            }

            List<string> successfulXUIs = new List<string>();
            List<string> failedXUIs = new List<string>();
            foreach (XUI12 readXUI in readXUIs)
            {
                if (readXUI.RootObject == null)
                {
                    _Log.Information("Failure: Null root object for {0}", readXUI.FilePath);
                    failedXUIs.Add(readXUI.FilePath);
                    continue;
                }

                string thisWriteXUIPath = Path.GetTempFileName();
                XUI12 writeXUI = new XUI12(thisWriteXUIPath, null);
                if (!await writeXUI.TryWriteAsync(0x5, readXUI.RootObject))
                {
                    _Log.Information("Failure: Write failed for {0}", readXUI.FilePath);
                    failedXUIs.Add(readXUI.FilePath);
                }
                else if (!AreFilesEqual(readXUI.FilePath, thisWriteXUIPath))
                {
                    _Log.Information("Failure: Non-equal files for {0}.", readXUI.FilePath);
                    failedXUIs.Add(readXUI.FilePath);
                }
                else
                {
                    successfulXUIs.Add(readXUI.FilePath);
                }

                File.Delete(thisWriteXUIPath);
            }

            float successPercentage = (successfulXUIs.Count / (float)readXUIs.Count) * 100.0f;

            _Log.Information("==== XUI12 ALL WRITES ====");
            _Log.Information("Total: {0}, Successful: {1}, Failed: {2} ({3}%)", readXUIs.Count, successfulXUIs.Count, failedXUIs.Count, successPercentage);
            _Log.Information("");
            _Log.Information("==== SUCCESSFUL XUIS ====");
            _Log.Information(string.Join("\n", successfulXUIs));
            _Log.Information("");
            _Log.Information("==== FAILED XUIS ====");
            _Log.Information(string.Join("\n", failedXUIs));
            _Log.Information("");

            Assert.True(failedXUIs.Count == 0);
        }

        [Test]
        public async Task CheckSingleXUIWriteSuccessful()
        {
            string xuiFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Test Data/XUI/9199/EditorSkin.xui");
            XUI12 readXUI = new XUI12(xuiFile, null);
            Assert.True(await readXUI.TryReadAsync(0x5));
            Assert.NotNull(readXUI.RootObject);

            string thisWriteXUIPath = @"F:\Code Repos\XUIHelper\XUIHelper.Core\XUIHelper.Core\Debug\written.xui";
            XUI12 writeXUI = new XUI12(thisWriteXUIPath, _Log);
            Assert.True(await writeXUI.TryWriteAsync(0x5, readXUI.RootObject));
            Assert.True(AreFilesEqual(readXUI.FilePath, writeXUI.FilePath));
        }

        [Test]
        public async Task CheckPhotoCaptureWriteSuccessful()
        {
            string xurFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Test Data/XUR/9199/PhotoCapture.xur");
            XUR5 readXUR = new XUR5(xurFile, null);
            Assert.True(await readXUR.TryReadAsync());
            IDATASection? data = ((IXUR)readXUR).TryFindXURSectionByMagic<IDATASection>(IDATASection.ExpectedMagic);
            Assert.NotNull(data);
            Assert.NotNull(data.RootObject);
            string thisWriteXUIPath = @"F:\Code Repos\XUIHelper\XUIHelper.Core\XUIHelper.Core\Debug\written.xui";
            string photoCaptureXUIPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Test Data/XUI/9199/PhotoCapture.xui");

            XUI12 writeXUI = new XUI12(thisWriteXUIPath, _Log);
            Assert.True(await writeXUI.TryWriteAsync(0x5, data.RootObject));
            Assert.True(AreFilesEqual(photoCaptureXUIPath, writeXUI.FilePath));
        }
    }
}