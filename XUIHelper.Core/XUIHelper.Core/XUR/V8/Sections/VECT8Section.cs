﻿using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XUIHelper.Core.Extensions;

namespace XUIHelper.Core
{
    public class VECT8Section : IVECTSection
    {
        public int Magic { get { return IVECTSection.ExpectedMagic; } }

        public List<XUVector> Vectors { get; private set; } = new List<XUVector>();

        public async Task<bool> TryReadAsync(IXUR xur, BinaryReader reader)
        {
            try
            {
                xur.Logger = xur.Logger?.ForContext(typeof(VECT8Section));
                xur.Logger?.Here().Verbose("Reading VECT8 section.");

                XURSectionTableEntry? entry = xur.TryGetXURSectionTableEntryForMagic(IVECTSection.ExpectedMagic);
                if (entry == null)
                {
                    xur.Logger?.Here().Error("XUR section table entry was null, returning false.");
                    return false;
                }

                xur.Logger?.Here().Verbose("Reading vectors from offset {0:X8}.", entry.Offset);
                reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);

                int bytesRead;
                int vectIndex = 0;
                for (bytesRead = 0; bytesRead < entry.Length;)
                {
                    xur.Logger?.Here().Verbose("Reading vector index {0} from offset {1:X8}.", vectIndex, reader.BaseStream.Position);
                    float thisX = reader.ReadSingleBE();
                    float thisY = reader.ReadSingleBE();
                    float thisZ = reader.ReadSingleBE();
                    vectIndex++;
                    bytesRead += 12;

                    XUVector thisVector = new XUVector(thisX, thisY, thisZ);
                    Vectors.Add(thisVector);
                    xur.Logger?.Here().Verbose("Read vector index {0} as {1}.", vectIndex, thisVector);
                }

                xur.Logger?.Here().Verbose("Read vectors successfully, read a total of {0} vectors, {1:X8} bytes.", Vectors.Count, bytesRead);
                return true;
            }
            catch (Exception ex)
            {
                xur.Logger?.Here().Error("Caught an exception when reading VECT8 section, returning false. The exception is: {0}", ex);
                return false;
            }
        }

        public async Task<bool> TryBuildAsync(IXUR xur, XUObject xuObject)
        {
            try
            {
                xur.Logger?.Here().Verbose("Building VECT8 vectors.");
                HashSet<XUVector> builtVectors = new HashSet<XUVector>();
                if (!TryBuildVectorsFromObject(xur, xuObject, ref builtVectors))
                {
                    xur.Logger?.Here().Error("Failed to build vectors, returning null.");
                    return false;
                }

                Vectors = builtVectors.ToList();
                xur.Logger?.Here().Verbose("Built a total of {0} VECT8 vectors successfully!", Vectors.Count);
                return true;
            }
            catch (Exception ex)
            {
                xur.Logger?.Here().Error("Caught an exception when trying to build VECT8 vectors, returning false. The exception is: {0}", ex);
                return false;
            }
        }

        private bool TryBuildVectorsFromObject(IXUR xur, XUObject xuObject, ref HashSet<XUVector> builtVectors)
        {
            try
            {
                if (!TryBuildVectorsFromProperties(xur, xuObject.Properties, ref builtVectors))
                {
                    xur.Logger?.Here().Error("Failed to build vectors from properties for {0}, returning false.", xuObject.ClassName);
                    return false;
                }

                foreach (XUObject childObject in xuObject.Children)
                {
                    if (!TryBuildVectorsFromObject(xur, childObject, ref builtVectors))
                    {
                        xur.Logger?.Here().Error("Failed to get vectors for child {0}, returning false.", childObject.ClassName);
                        return false;
                    }
                }

                foreach (XUTimeline childTimeline in xuObject.Timelines)
                {
                    foreach (XUKeyframe childKeyframe in childTimeline.Keyframes)
                    {
                        foreach (XUProperty animatedProperty in childKeyframe.Properties)
                        {
                            if (animatedProperty.PropertyDefinition.Type == XUPropertyDefinitionTypes.Vector)
                            {
                                if (animatedProperty.Value is not XUVector valueVector)
                                {
                                    xur.Logger?.Here().Error("Animated property {0} marked as vector had a non-vector value of {1}, returning false.", animatedProperty.PropertyDefinition.Name, animatedProperty.Value);
                                    return false;
                                }

                                if (builtVectors.Add(valueVector))
                                {
                                    xur.Logger?.Here().Verbose("Added {0} animated property value vector {1}.", animatedProperty.PropertyDefinition.Name, valueVector);
                                }
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                xur.Logger?.Here().Error("Caught an exception when trying to build VECT8 vectors for object {0}, returning false. The exception is: {1}", xuObject.ClassName, ex);
                return false;
            }
        }

        private bool TryBuildVectorsFromProperties(IXUR xur, List<XUProperty> properties, ref HashSet<XUVector> builtVectors)
        {
            try
            {
                foreach (XUProperty childProperty in properties)
                {
                    if (childProperty.PropertyDefinition.Type == XUPropertyDefinitionTypes.Vector)
                    {
                        if (childProperty.Value is not XUVector valueVector)
                        {
                            xur.Logger?.Here().Error("Child property {0} marked as vector had a non-vector value of {1}, returning false.", childProperty.PropertyDefinition.Name, childProperty.Value);
                            return false;
                        }

                        if (builtVectors.Add(valueVector))
                        {
                            xur.Logger?.Here().Verbose("Added {0} property value vector {1}.", childProperty.PropertyDefinition.Name, valueVector);
                        }
                    }
                    else if (childProperty.PropertyDefinition.Type == XUPropertyDefinitionTypes.Object)
                    {
                        if (!TryBuildVectorsFromProperties(xur, childProperty.Value as List<XUProperty>, ref builtVectors))
                        {
                            xur.Logger?.Here().Error("Failed to build vectors for child compound properties, returning false.");
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                xur.Logger?.Here().Error("Caught an exception when trying to build VECT8 vectors from properties, returning false. The exception is: {0}", ex);
                return false;
            }
        }


        public async Task<int?> TryWriteAsync(IXUR xur, XUObject xuObject, BinaryWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}
