﻿using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using XUIHelper.Core.Extensions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace XUIHelper.Core
{
    public class DATA5Section : IDATASection
    {
        public int Magic { get { return IDATASection.ExpectedMagic; } }

        public XMLExtensionsManager? ExtensionsManager { get; private set; }

        public XUObject? RootObject { get; private set; }

        public async Task<bool> TryReadAsync(IXUR xur, BinaryReader reader)
        {
            try
            {
                xur.Logger = xur.Logger?.ForContext(typeof(DATA5Section));
                xur.Logger?.Here().Verbose("Reading DATA5 section.");

                if(ExtensionsManager == null)
                {
                    xur.Logger?.Here().Error("Extensions manager was null, returning false.");
                    return false;
                }

                XURSectionTableEntry? entry = xur.TryGetXURSectionTableEntryForMagic(IDATASection.ExpectedMagic);
                if (entry == null)
                {
                    xur.Logger?.Here().Error("XUR section table entry was null, returning false.");
                    return false;
                }

                xur.Logger?.Here().Verbose("Reading data from offset {0:X8}.", entry.Offset);
                reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);

                XUObject dummyParent = new XUObject("");
                RootObject = TryReadObject(xur, reader, ref dummyParent);
                if (RootObject == null)
                {
                    xur.Logger?.Here().Error("Root object was null, read must have failed, returning false.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                xur.Logger?.Here().Error("Caught an exception when reading DATA5 section, returning false. The exception is: {0}", ex);
                return false;
            }
        }

        private XUObject? TryReadObject(IXUR xur, BinaryReader reader, ref XUObject parentObject)
        {
            try
            {
                xur.Logger?.Here().Verbose("Reading object.");

                ISTRNSection? strnSection = xur.TryFindXURSectionByMagic<ISTRNSection>(ISTRNSection.ExpectedMagic);
                if (strnSection == null)
                {
                    xur.Logger?.Here().Error("STRN section was null, returning null.");
                    return null;
                }

                short stringIndex = (short)(reader.ReadInt16BE() - 1);
                xur.Logger?.Here().Verbose("Read string index of {0:X8}.", stringIndex);

                byte flags = reader.ReadByte();
                xur.Logger?.Here().Verbose("Read flags of {0:X8}.", flags);

                if (stringIndex < 0 || stringIndex > strnSection.Strings.Count - 1)
                {
                    xur.Logger?.Here().Error("String index of {0:X8} is invalid, must be between 0 and {1}, returning null.", stringIndex, strnSection.Strings.Count - 1);
                    return null;
                }

                string className = strnSection.Strings[stringIndex];

                XUObject thisObject = new XUObject(className);
                xur.Logger?.Here().Verbose("Reading class {0}.", className);

                if ((flags & 0x1) == 0x1)
                {
                    xur.Logger?.Here().Verbose("Class has properties.");
                    List<XUProperty>? readProperties = TryReadProperties(xur, reader, className);
                    if (readProperties == null)
                    {
                        xur.Logger?.Here().Error("Failed to read properties, returning null.");
                        return null;
                    }

                    thisObject.Properties = readProperties;
                }

                if ((flags & 0x2) == 0x2)
                {
                    xur.Logger?.Here().Verbose("Class has children, reading count.");

                    int childrenCount = reader.ReadInt32BE();
                    xur.Logger?.Here().Verbose("Class has {0} children.", childrenCount);

                    for (int childIndex = 0; childIndex < childrenCount; childIndex++)
                    {
                        xur.Logger?.Here().Verbose("Reading child object index {0}.", childIndex);
                        XUObject? thisChild = TryReadObject(xur, reader, ref thisObject);
                        if (thisChild == null)
                        {
                            xur.Logger?.Here().Error("Failed to read child object index {0}, returning false.", childIndex);
                            return null;
                        }

                        thisObject.Children.Add(thisChild);
                    }
                }

                if((flags & 0x4) == 0x4) 
                {
                    xur.Logger?.Here().Verbose("Class has timeline data, reading named frames count.");

                    int namedFramesCount = reader.ReadInt32BE();
                    xur.Logger?.Here().Verbose("Class has {0} named frames.", namedFramesCount);

                    for (int namedFrameIndex = 0; namedFrameIndex < namedFramesCount; namedFrameIndex++)
                    {
                        xur.Logger?.Here().Verbose("Reading named frame index {0}.", namedFrameIndex);
                        XUNamedFrame? thisNamedFrame = ((XUR5)xur).TryReadNamedFrame(reader);
                        if (thisNamedFrame == null)
                        {
                            xur.Logger?.Here().Error("Failed to read named frame index {0}, returning false.", namedFrameIndex);
                            return null;
                        }

                        thisObject.NamedFrames.Add(thisNamedFrame);
                    }

                    if (thisObject.Children.Count == 0)
                    {
                        xur.Logger?.Here().Verbose("The parent object had no children, no need to load timeline data.");
                        return thisObject;
                    }

                    xur.Logger?.Here().Verbose("Reading timelines count.");
                    int timelinesCount = reader.ReadInt32BE();
                    xur.Logger?.Here().Verbose("Class has {0:X8} timelines.", timelinesCount);

                    if (timelinesCount == 0)
                    {
                        xur.Logger?.Here().Verbose("There are no timelines, no need to load timeline data, returning true.");
                        return thisObject;
                    }

                    for (int timelineIndex = 0; timelineIndex < timelinesCount; timelineIndex++)
                    {
                        xur.Logger?.Here().Verbose("Reading timeline index {0}.", timelineIndex);
                        XUTimeline? thisTimeline = xur.TryReadTimeline(reader, thisObject);
                        if (thisTimeline == null)
                        {
                            xur.Logger?.Here().Error("Failed to read timeline index {0}, returning false.", timelineIndex);
                            return null;
                        }

                        thisObject.Timelines.Add(thisTimeline);
                    }
                }

                return thisObject;
            }
            catch (Exception ex)
            {
                xur.Logger?.Here().Error("Caught an exception when reading object, returning null. The exception is: {0}", ex);
                return null;
            }
        }

        private List<XUProperty>? TryReadProperties(IXUR xur, BinaryReader reader, string className)
        {
            try
            {
                xur.Logger?.Here().Verbose("Reading properties for class {0}.", className);

                List<XUClass>? classList = ExtensionsManager?.TryGetClassHierarchy(className);
                if (classList == null)
                {
                    xur.Logger?.Here().Error("Failed to get class hierarchy for class {0}, returning null.", className);
                    return null;
                }

                short propertiesCount = reader.ReadInt16BE();
                xur.Logger?.Here().Verbose("Class {0} has {1:X8} properties.", className, propertiesCount);
                List<XUProperty> retProperties = new List<XUProperty>();
                foreach (XUClass xuClass in classList)
                {
                    xur.Logger?.Here().Verbose("Reading property data for hierarchy class {0}.", xuClass.Name);

                    byte hierarchicalPropertiesCount = reader.ReadByte();
                    xur.Logger?.Here().Verbose("Class {0} has a hierarchical properties count of {1:X8}.", className, hierarchicalPropertiesCount);
                    if(hierarchicalPropertiesCount == 0x00)
                    {
                        xur.Logger?.Here().Verbose("No hierarchical properties, continuing to next class...");
                        continue;
                    }

                    int propertyMasksCount = Math.Max((int)Math.Ceiling(xuClass.PropertyDefinitions.Count / 8.0f), 1);
                    xur.Logger?.Here().Verbose("Class has {0:X8} property definitions, will have {1:X8} mask(s).", xuClass.PropertyDefinitions.Count, propertyMasksCount);

                    byte[] propertyMasks = new byte[propertyMasksCount];
                    for (int i = 0; i < propertyMasksCount; i++)
                    {
                        byte readMask = reader.ReadByte();
                        propertyMasks[i] = readMask;
                        xur.Logger?.Here().Verbose("Read property mask {0:X8}.", readMask);
                    }
                    Array.Reverse(propertyMasks);

                    for (int i = 0; i < propertyMasksCount; i++)
                    {
                        byte thisPropertyMask = propertyMasks[i];
                        xur.Logger?.Here().Verbose("Handling property mask {0:X8} for hierarchy class {1}.", thisPropertyMask, xuClass.Name);

                        if (thisPropertyMask == 0x00)
                        {
                            xur.Logger?.Here().Verbose("Property mask is 0, continuing.");
                            continue;
                        }

                        int propertyIndex = 0;
                        List<XUPropertyDefinition> thisMaskPropertyDefinitions = xuClass.PropertyDefinitions.Skip(i * 8).Take(8).ToList();
                        foreach (XUPropertyDefinition propertyDefinition in thisMaskPropertyDefinitions)
                        {
                            int flag = 1 << propertyIndex;

                            if ((thisPropertyMask & flag) == flag)
                            {
                                xur.Logger?.Here().Verbose("Reading {0} property.", propertyDefinition.Name);
                                XUProperty? xuProperty = xur.TryReadProperty(reader, propertyDefinition);
                                if (xuProperty == null)
                                {
                                    xur.Logger?.Here().Error("Failed to read {0} property, returning null.", propertyDefinition.Name);
                                    return null;
                                }

                                retProperties.Add(xuProperty);
                            }

                            propertyIndex++;
                        }
                    }
                }

                if (retProperties.Count != propertiesCount)
                {
                    xur.Logger?.Here().Error("Mismatch of properties count, returning null. Expected: {0}, Actual: {1}", propertiesCount, retProperties.Count);
                    return null;
                }

                return retProperties;
            }
            catch (Exception ex)
            {
                xur.Logger?.Here().Error("Caught an exception when reading properties, returning null. The exception is: {0}", ex);
                return null;
            }
        }

        public async Task<bool> TryBuildAsync(IXUR xur, XUObject xuObject)
        {
            throw new NotImplementedException();
        }

        public async Task<int?> TryWriteAsync(IXUR xur, XUObject xuObject, BinaryWriter writer)
        {
            try
            {
                xur.Logger = xur.Logger?.ForContext(typeof(DATA5Section));
                xur.Logger?.Here().Verbose("Writing DATA5 section.");

                if (ExtensionsManager == null)
                {
                    xur.Logger?.Here().Error("Extensions manager was null, returning null.");
                    return null;
                }

                if (RootObject == null)
                {
                    xur.Logger?.Here().Error("Root object was null, returning null.");
                    return null;
                }

                int? bytesWritten = TryWriteObject(xur, writer, RootObject);
                if (bytesWritten == null)
                {
                    xur.Logger?.Here().Error("Bytes written was null, write must have failed, returning null.");
                    return null;
                }

                xur.Logger?.Here().Verbose("Wrote DATA5 section as {0:X8} bytes successfully!", bytesWritten);
                return bytesWritten;
            }
            catch (Exception ex)
            {
                xur.Logger?.Here().Error("Caught an exception when writing DATA5 section, returning null. The exception is: {0}", ex);
                return null;
            }
        }

        private int? TryWriteObject(IXUR xur, BinaryWriter writer, XUObject xuObject)
        {
            try
            {
                xur.Logger?.Here().Verbose("Writing object.");
                int bytesWritten = 0;

                ISTRNSection? strnSection = xur.TryFindXURSectionByMagic<ISTRNSection>(ISTRNSection.ExpectedMagic);
                if (strnSection == null)
                {
                    xur.Logger?.Here().Error("STRN section was null, returning null.");
                    return null;
                }

                short classNameIndex = (short)strnSection.Strings.IndexOf(xuObject.ClassName);
                if(classNameIndex == -1)
                {
                    xur.Logger?.Here().Error("Failed to find valid string index for class name {0}, returning null.", xuObject.ClassName);
                    return null;
                }

                classNameIndex++;   //Class name indexes are 1-based :/
                xur.Logger?.Here().Verbose("Writing object class name index of {0} for class name {1}.", classNameIndex, xuObject.ClassName);
                writer.WriteInt16BE(classNameIndex);
                bytesWritten += 2;

                byte flags = 0x00;
                if (xuObject.Properties.Count > 0)
                {
                    flags |= 0x1;
                    xur.Logger?.Here().Verbose("Object has properties, flags is now {0:X8}", flags);
                }

                if (xuObject.Children.Count > 0)
                {
                    flags |= 0x2;
                    xur.Logger?.Here().Verbose("Object has children, flags is now {0:X8}", flags);
                }

                if (xuObject.Timelines.Count > 0 || xuObject.NamedFrames.Count > 0)
                {
                    flags |= 0x4;
                    xur.Logger?.Here().Verbose("Object has timline data, flags is now {0:X8}", flags);
                }

                xur.Logger?.Here().Verbose("Writing flags of {0:X8}.", (byte)flags);
                writer.Write((byte)flags);
                bytesWritten++;

                if(xuObject.Properties.Count > 0)
                {
                    int? propertyBytesWritten = TryWriteProperties(xur, writer, xuObject.Properties);
                    if (propertyBytesWritten == null)
                    {
                        xur.Logger?.Here().Error("Property bytes written was null for {0}, an error must have occurred, returning null.", xuObject.ClassName);
                        return null;
                    }

                    bytesWritten += propertyBytesWritten.Value;
                }

                return null;
            }
            catch (Exception ex)
            {
                xur.Logger?.Here().Error("Caught an exception when writing object, returning null. The exception is: {0}", ex);
                return null;
            }
        }

        private int? TryWriteProperties(IXUR xur, BinaryWriter writer, List<XUProperty> properties)
        {
            try
            {
                xur.Logger?.Here().Verbose("Writing object properties.");

                if (properties.Count == 0)
                {
                    xur.Logger?.Here().Verbose("There were no properties, returning 0.");
                    return 0;
                }

                int bytesWritten = 0;

                xur.Logger?.Here().Verbose("Writing object properties count of {0:X8}.", properties.Count);
                writer.WriteInt16BE((short)properties.Count);
                bytesWritten += 2;

                return bytesWritten;
            }
            catch (Exception ex)
            {
                xur.Logger?.Here().Error("Caught an exception when writing object properties, returning null. The exception is: {0}", ex);
                return null;
            }
        }

        public DATA5Section()
        {
            ExtensionsManager = XUIHelperCoreConstants.VersionedExtensions.GetValueOrDefault(0x5);
        }

        public DATA5Section(XUObject rootObject)
        {
            RootObject = rootObject;
            ExtensionsManager = XUIHelperCoreConstants.VersionedExtensions.GetValueOrDefault(0x5);
        }
    }
}
