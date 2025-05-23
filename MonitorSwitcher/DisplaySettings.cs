﻿using System.Xml.Serialization;
using System.Xml;
using System.Diagnostics.CodeAnalysis;
using Serilog;

namespace MonitorSwitcher;

public static class DisplaySettings
{
    public static bool GetDisplaySettings(
        [NotNullWhen(true)] out CcdWrapper.DisplayConfigPathInfo[]? pathInfoArray,
        [NotNullWhen(true)] out CcdWrapper.DisplayConfigModeInfo[]? modeInfoArray,
        [NotNullWhen(true)] out CcdWrapper.MonitorAdditionalInfo[]? additionalInfo,
        bool activeOnly)
    {
        // query active paths from the current computer.
        Log.Debug("Getting display settings");
        var queryFlags = activeOnly
            ? CcdWrapper.QueryDisplayFlags.OnlyActivePaths
            : CcdWrapper.QueryDisplayFlags.AllPaths;

        Log.Debug("Querying display config");
        if (!CcdWrapper.QueryDisplayConfig(queryFlags, out pathInfoArray, out modeInfoArray))
        {
            Log.Debug("Querying display config failed");
            additionalInfo = null;
            return false;
        }

        additionalInfo = new CcdWrapper.MonitorAdditionalInfo[modeInfoArray.Length];

        // cleanup of modeInfo bad elements
        var validModeInfos = modeInfoArray
            .Where(modeInfo => modeInfo.infoType != CcdWrapper.DisplayConfigModeInfoType.Zero)
            .ToList();
        if (validModeInfos.Count > 0)
        {
            // only cleanup if there is at least one valid element found
            modeInfoArray = validModeInfos.ToArray();
        }

        // cleanup of currently not available pathInfo elements
        var availablePathInfos = pathInfoArray.Where(pathInfo => pathInfo.targetInfo.targetAvailable).ToList();
        if (availablePathInfos.Count > 0)
        {
            // only cleanup if there is at least one valid element found
            pathInfoArray = availablePathInfos.ToArray();
        }

        // get the display names for all modes
        for (var i = 0; i < modeInfoArray.Length; i++)
        {
            if (modeInfoArray[i].infoType != CcdWrapper.DisplayConfigModeInfoType.Target)
            {
                continue;
            }

            try
            {
                additionalInfo[i] = CcdWrapper.GetMonitorAdditionalInfo(
                    modeInfoArray[i].adapterId, modeInfoArray[i].id);
            }
            catch
            {
                additionalInfo[i].valid = false;
            }
        }

        return true;
    }

    public static bool SaveDisplaySettings(string fileName)
    {
        Log.Debug("Getting display config");
        if (!GetDisplaySettings(out var pathInfoArray, out var modeInfoArray, out var additionalInfo, true))
        {
            Log.Error("Failed to get display settings");
            return false;
        }

        // debug output complete display settings
        Log.Debug("Display settings to write:");
        Log.Debug(PrintDisplaySettings(pathInfoArray, modeInfoArray));

        Log.Debug("Initializing objects for Serialization");
        var writerAdditionalInfo = new XmlSerializer(typeof(CcdWrapper.MonitorAdditionalInfo));
        var writerPath = new XmlSerializer(typeof(CcdWrapper.DisplayConfigPathInfo));
        var writerModeTarget = new XmlSerializer(typeof(CcdWrapper.DisplayConfigTargetMode));
        var writerModeSource = new XmlSerializer(typeof(CcdWrapper.DisplayConfigSourceMode));
        var writerModeInfoType = new XmlSerializer(typeof(CcdWrapper.DisplayConfigModeInfoType));
        var writerModeAdapterId = new XmlSerializer(typeof(CcdWrapper.LUID));
        var xmlWriter = XmlWriter.Create(fileName);

        xmlWriter.WriteStartDocument();
        xmlWriter.WriteStartElement("displaySettings");
        xmlWriter.WriteStartElement("pathInfoArray");
        foreach (var pathInfo in pathInfoArray)
        {
            writerPath.Serialize(xmlWriter, pathInfo);
        }

        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("modeInfoArray");
        foreach (var modeInfo in modeInfoArray)
        {
            xmlWriter.WriteStartElement("modeInfo");
            xmlWriter.WriteElementString("id", modeInfo.id.ToString());
            writerModeAdapterId.Serialize(xmlWriter, modeInfo.adapterId);
            writerModeInfoType.Serialize(xmlWriter, modeInfo.infoType);
            if (modeInfo.infoType == CcdWrapper.DisplayConfigModeInfoType.Target)
            {
                writerModeTarget.Serialize(xmlWriter, modeInfo.targetMode);
            }
            else
            {
                writerModeSource.Serialize(xmlWriter, modeInfo.sourceMode);
            }

            xmlWriter.WriteEndElement();
        }

        xmlWriter.WriteEndElement();

        xmlWriter.WriteStartElement("additionalInfo");
        foreach (var info in additionalInfo)
        {
            writerAdditionalInfo.Serialize(xmlWriter, info);
        }

        xmlWriter.WriteEndElement();
        xmlWriter.WriteEndDocument();
        xmlWriter.Flush();
        xmlWriter.Close();

        return true;
    }

    public static bool LoadDisplaySettings(string filename, bool matchAdapterIds = true)
    {
        Log.Debug("Loading display settings from file: {Filename}", filename);

        if (!File.Exists(filename))
        {
            Log.Error("Failed to load display settings because file does not exist: {Filename}", filename);
            return false;
        }

        ParseXmlFile(filename, out var pathInfoList, out var modeInfoList, out var additionalInfoList);

        // Convert C# lists to simple arrays
        Log.Debug("Converting to simple arrays for API compatibility");
        var pathInfoArray = pathInfoList.ToArray();
        var modeInfoArray = modeInfoList.ToArray();

        Log.Debug("Getting current display settings");
        if (!GetDisplaySettings(out var pathInfoArrayCurrent, out var modeInfoArrayCurrent, out var additionalInfoCurrent, false))
        {
            Log.Debug("Failed to get current display settings");
            return false;
        }

        if (matchAdapterIds)
        {
            MatchAdapterIds(pathInfoArray, pathInfoArrayCurrent, modeInfoArray);
        }

        // Set loaded display settings
        Log.Debug("Setting up final display settings to load");

        // debug output complete display settings
        Log.Debug("Display settings to be loaded: ");
        Log.Debug(PrintDisplaySettings(pathInfoArray, modeInfoArray));

        const CcdWrapper.SdcFlags noAllowChanges = CcdWrapper.SdcFlags.Apply |
            CcdWrapper.SdcFlags.UseSuppliedDisplayConfig |
            CcdWrapper.SdcFlags.NoOptimization |
            CcdWrapper.SdcFlags.SaveToDatabase;
        const CcdWrapper.SdcFlags withAllowChanges = noAllowChanges | CcdWrapper.SdcFlags.AllowChanges;

        // First let's try without SdcFlags.AllowChanges
        if (CcdWrapper.SetDisplayConfig(pathInfoArray, modeInfoArray, noAllowChanges))
        {
            return true;
        }

        // try again with SdcFlags.AllowChanges
        Log.Error("Failed to set display settings without SdcFlags.AllowChanges");
        Log.Information("Trying again with additional SdcFlags.AllowChanges flag");

        if (CcdWrapper.SetDisplayConfig(pathInfoArray, modeInfoArray, withAllowChanges))
        {
            return true;
        }

        Log.Error("Failed to set display settings using default method");

        if (additionalInfoCurrent.Length > 0 && additionalInfoList.Count > 0) // only if present, e.g. new profile
        {
            Log.Information("Trying alternative method");
            // Restore original settings and adapter IDs
            Log.Debug("Converting again to simple arrays for API compatibility");
            pathInfoArray = pathInfoList.ToArray();
            modeInfoArray = modeInfoList.ToArray();

            Log.Debug("Alternative matching mode");
            // For each modeInfo iterate over the current additional information, i.e. monitor names and paths, and find the one matching in the current setup
            for (int iModeInfo = 0; iModeInfo < modeInfoArray.Length; iModeInfo++)
            {
                for (int iAdditionalInfoCurrent = 0; iAdditionalInfoCurrent < additionalInfoCurrent.Length; iAdditionalInfoCurrent++)
                {
                    if (additionalInfoCurrent[iAdditionalInfoCurrent].monitorFriendlyDevice != null && additionalInfoList[iModeInfo].monitorFriendlyDevice != null)
                    {
                        if (additionalInfoCurrent[iAdditionalInfoCurrent].monitorFriendlyDevice == additionalInfoList[iModeInfo].monitorFriendlyDevice)
                        {
                            CcdWrapper.LUID originalId = modeInfoArray[iModeInfo].adapterId;
                            // now also find all other matching pathInfo modeInfos with that ID and change it
                            for (int iPathInfo = 0; iPathInfo < pathInfoArray.Length; iPathInfo++)
                            {
                                if (pathInfoArray[iPathInfo].targetInfo.adapterId.LowPart == originalId.LowPart &&
                                    pathInfoArray[iPathInfo].targetInfo.adapterId.HighPart == originalId.HighPart)
                                {
                                    pathInfoArray[iPathInfo].targetInfo.adapterId = modeInfoArrayCurrent[iAdditionalInfoCurrent].adapterId;
                                    pathInfoArray[iPathInfo].sourceInfo.adapterId = modeInfoArrayCurrent[iAdditionalInfoCurrent].adapterId;
                                    pathInfoArray[iPathInfo].targetInfo.id = modeInfoArrayCurrent[iAdditionalInfoCurrent].id;
                                }
                            }
                            for (int iModeInfoFix = 0; iModeInfoFix < modeInfoArray.Length; iModeInfoFix++)
                            {
                                if (modeInfoArray[iModeInfoFix].adapterId.LowPart == originalId.LowPart &&
                                    modeInfoArray[iModeInfoFix].adapterId.HighPart == originalId.HighPart)
                                {
                                    modeInfoArray[iModeInfoFix].adapterId = modeInfoArrayCurrent[iAdditionalInfoCurrent].adapterId;
                                }
                            }
                            modeInfoArray[iModeInfo].adapterId = modeInfoArrayCurrent[iAdditionalInfoCurrent].adapterId;
                            modeInfoArray[iModeInfo].id = modeInfoArrayCurrent[iAdditionalInfoCurrent].id;

                            break;
                        }
                    }
                }
            }

            // debug output complete display settings
            Log.Debug("\nDisplay settings to be loaded: ");
            Log.Debug(PrintDisplaySettings(pathInfoArray, modeInfoArray));

            // First let's try without SdcFlags.AllowChanges
            if (CcdWrapper.SetDisplayConfig(pathInfoArray, modeInfoArray, noAllowChanges))
            {
                return true;
            }

            // again with SdcFlags.AllowChanges
            if (CcdWrapper.SetDisplayConfig(pathInfoArray, modeInfoArray, withAllowChanges))
            {
                return true;
            }

            Log.Error("Failed to set display settings using alternative method");
        }

        Log.Information("Trying yet another method for adapter ID matching:");

        // Restore original settings and adapter IDs
        Log.Debug("Converting again to simple arrays for API compatibility");
        pathInfoArray = pathInfoList.ToArray();
        modeInfoArray = modeInfoList.ToArray();

        // The next method is identical to the first one but uses a more radical adapter ID assignment
        for (int iPathInfo = 0; iPathInfo < pathInfoArray.Length; iPathInfo++)
        {
            for (int iPathInfoCurrent = 0; iPathInfoCurrent < pathInfoArrayCurrent.Length; iPathInfoCurrent++)
            {
                if (pathInfoArray[iPathInfo].sourceInfo.id ==
                    pathInfoArrayCurrent[iPathInfoCurrent].sourceInfo.id &&
                    pathInfoArray[iPathInfo].targetInfo.id ==
                    pathInfoArrayCurrent[iPathInfoCurrent].targetInfo.id)
                {
                    Log.Debug("\t!!! Both IDs are a match, getting new Adapter ID and replacing all other IDs !!!");
                    uint oldId = pathInfoArray[iPathInfo].sourceInfo.adapterId.LowPart;
                    uint newId = pathInfoArrayCurrent[iPathInfoCurrent].sourceInfo.adapterId.LowPart;
                    for (int iPathInfoReplace = 0; iPathInfoReplace < pathInfoArray.Length; iPathInfoReplace++)
                    {
                        if (pathInfoArray[iPathInfoReplace].sourceInfo.adapterId.LowPart == oldId)
                            pathInfoArray[iPathInfoReplace].sourceInfo.adapterId.LowPart = newId;
                        if (pathInfoArray[iPathInfoReplace].targetInfo.adapterId.LowPart == oldId)
                            pathInfoArray[iPathInfoReplace].targetInfo.adapterId.LowPart = newId;
                    }

                    for (int iModeInfoReplace = 0; iModeInfoReplace < modeInfoArray.Length; iModeInfoReplace++)
                    {
                        if (modeInfoArray[iModeInfoReplace].adapterId.LowPart == oldId)
                        {
                            modeInfoArray[iModeInfoReplace].adapterId.LowPart = newId;
                        }
                    }

                    break;
                }

                Log.Debug("\t---");
            }
        }

        // Set loaded display settings
        Log.Debug("Setting up final display settings to load");

        // debug output complete display settings
        Log.Debug("\nDisplay settings to be loaded: ");
        Log.Debug(PrintDisplaySettings(pathInfoArray, modeInfoArray));

        // First let's try without SdcFlags.AllowChanges
        if (CcdWrapper.SetDisplayConfig(pathInfoArray, modeInfoArray, noAllowChanges))
        {
            return true;
        }

        // again with SdcFlags.AllowChanges
        if (CcdWrapper.SetDisplayConfig(pathInfoArray, modeInfoArray, withAllowChanges))
        {
            return true;
        }

        Log.Error("Failed to set display settings using the other alternative method");
        return false;
    }

    private static void ParseXmlFile(string filename,
        out List<CcdWrapper.DisplayConfigPathInfo> pathInfoList,
        out List<CcdWrapper.DisplayConfigModeInfo> modeInfoList,
        out List<CcdWrapper.MonitorAdditionalInfo> additionalInfoList)
    {
        Log.Debug("Parsing XML file");
        var xmlReader = XmlReader.Create(filename);
        var readerAdditionalInfo = new XmlSerializer(typeof(CcdWrapper.MonitorAdditionalInfo));
        var readerPath = new XmlSerializer(typeof(CcdWrapper.DisplayConfigPathInfo));
        var readerModeTarget = new XmlSerializer(typeof(CcdWrapper.DisplayConfigTargetMode));
        var readerModeSource = new XmlSerializer(typeof(CcdWrapper.DisplayConfigSourceMode));
        var readerModeInfoType = new XmlSerializer(typeof(CcdWrapper.DisplayConfigModeInfoType));
        var readerModeAdapterId = new XmlSerializer(typeof(CcdWrapper.LUID));

        pathInfoList = [];
        modeInfoList = [];
        additionalInfoList = [];

        while (true)
        {
            Log.Debug("\tXML Element: " + xmlReader.Name);
            if (xmlReader.Name == "DisplayConfigPathInfo" && xmlReader.IsStartElement())
            {
                Log.Debug("\t\tReading pathInfo");
                var pathInfo = readerPath.Deserialize<CcdWrapper.DisplayConfigPathInfo>(xmlReader);
                pathInfoList.Add(pathInfo);
            }
            else if (xmlReader.Name == "modeInfo" && xmlReader.IsStartElement())
            {
                Log.Debug("\t\tReading modeInfo");

                xmlReader.Read(); // Read id start tag
                xmlReader.Read(); // Read id value

                var modeInfo = new CcdWrapper.DisplayConfigModeInfo
                {
                    id = Convert.ToUInt32(xmlReader.Value)
                };

                xmlReader.Read(); // Read id end tag
                xmlReader.Read(); // Read LUID start tag

                modeInfo.adapterId = readerModeAdapterId.Deserialize<CcdWrapper.LUID>(xmlReader);
                modeInfo.infoType = readerModeInfoType.Deserialize<CcdWrapper.DisplayConfigModeInfoType>(xmlReader);
                if (modeInfo.infoType == CcdWrapper.DisplayConfigModeInfoType.Target)
                {
                    modeInfo.targetMode = readerModeTarget.Deserialize<CcdWrapper.DisplayConfigTargetMode>(xmlReader);
                }
                else
                {
                    modeInfo.sourceMode = readerModeSource.Deserialize<CcdWrapper.DisplayConfigSourceMode>(xmlReader);
                }
                Log.Debug("\t\t\tmodeInfo.id = " + modeInfo.id);
                Log.Debug("\t\t\tmodeInfo.adapterId (High Part) = " + modeInfo.adapterId.HighPart);
                Log.Debug("\t\t\tmodeInfo.adapterId (Low Part) = " + modeInfo.adapterId.LowPart);
                Log.Debug("\t\t\tmodeInfo.infoType = " + modeInfo.infoType);

                modeInfoList.Add(modeInfo);
            }
            else if (xmlReader.Name == "MonitorAdditionalInfo" && xmlReader.IsStartElement())
            {
                Log.Debug("\t\tReading additional information");
                var additionalInfo = readerAdditionalInfo.Deserialize<CcdWrapper.MonitorAdditionalInfo>(xmlReader);
                additionalInfoList.Add(additionalInfo);
            }
            else if (!xmlReader.Read())
            {
                break;
            }
        }

        xmlReader.Close();
        Log.Debug("Parsing of XML file successful");
    }

    /// <summary>
    /// For some reason the adapterID parameter changes upon system restart, all other parameters however, especially the ID remain constant.
    /// We check the loaded settings against the current settings replacing the adapterID with the other parameters
    /// </summary>
    private static void MatchAdapterIds(
        CcdWrapper.DisplayConfigPathInfo[] xmlPathInfos,
        CcdWrapper.DisplayConfigPathInfo[] currentPathInfos,
        CcdWrapper.DisplayConfigModeInfo[] modeInfos)
    {
        Log.Debug("Matching of adapter IDs for pathInfo");
        for (var i = 0; i < xmlPathInfos.Length; i++)
        {
            var matches = currentPathInfos
                .Where(current => current.sourceInfo.id == xmlPathInfos[i].sourceInfo.id &&
                    current.targetInfo.id == xmlPathInfos[i].targetInfo.id)
                .ToList();
            if (matches.Count == 0)
            {
                continue;
            }

            var current = matches.Single();
            xmlPathInfos[i].sourceInfo.adapterId.LowPart = current.sourceInfo.adapterId.LowPart;
            xmlPathInfos[i].targetInfo.adapterId.LowPart = current.targetInfo.adapterId.LowPart;
        }

        // Same again for modeInfo, however we get the required adapterId information from the pathInfoArray
        Log.Debug("Matching of adapter IDs for modeInfo");
        for (var i = 0; i < modeInfos.Length; i++)
        {
            if (modeInfos[i].infoType != CcdWrapper.DisplayConfigModeInfoType.Target)
            {
                continue;
            }

            var pathInfos = xmlPathInfos.Where(pathInfo => pathInfo.targetInfo.id == modeInfos[i].id).ToList();
            if (pathInfos.Count == 0)
            {
                continue;
            }
            var pathInfo = pathInfos.Single();
            modeInfos[i].adapterId.LowPart = pathInfo.targetInfo.adapterId.LowPart;

            for (var j = 0; j < modeInfos.Length; j++)
            {
                if (modeInfos[j].id == pathInfo.sourceInfo.id &&
                    modeInfos[j].adapterId.LowPart == modeInfos[i].adapterId.LowPart &&
                    modeInfos[j].infoType == CcdWrapper.DisplayConfigModeInfoType.Source)
                {
                    Log.Debug("\t\t!!! IDs are a match, taking adapter id from pathInfo !!!");
                    modeInfos[j].adapterId.LowPart = pathInfo.sourceInfo.adapterId.LowPart;
                    break;
                }
            }
        }

        Log.Debug("Done matching of adapter IDs");
    }

    public static string PrintDisplaySettings(CcdWrapper.DisplayConfigPathInfo[] pathInfoArray, CcdWrapper.DisplayConfigModeInfo[] modeInfoArray)
    {
        // initialize text writer
        var stringWriter = new StringWriter();

        // initialize xml serializer
        var writerPath = new XmlSerializer(typeof(CcdWrapper.DisplayConfigPathInfo));
        var writerModeTarget = new XmlSerializer(typeof(CcdWrapper.DisplayConfigTargetMode));
        var writerModeSource = new XmlSerializer(typeof(CcdWrapper.DisplayConfigSourceMode));
        var writerModeInfoType = new XmlSerializer(typeof(CcdWrapper.DisplayConfigModeInfoType));
        var writerModeAdapterId = new XmlSerializer(typeof(CcdWrapper.LUID));

        // write content to string
        stringWriter.WriteLine("<displaySettings>");
        stringWriter.WriteLine("<pathInfoArray>");
        foreach (var pathInfo in pathInfoArray)
        {
            writerPath.Serialize(stringWriter, pathInfo);
        }
        stringWriter.WriteLine("</pathInfoArray>");

        stringWriter.WriteLine("<modeInfoArray>");
        foreach (var modeInfo in modeInfoArray)
        {
            stringWriter.WriteLine("<modeInfo>");
            stringWriter.WriteLine("<id>" + modeInfo.id.ToString() + "</id>");
            writerModeAdapterId.Serialize(stringWriter, modeInfo.adapterId);
            writerModeInfoType.Serialize(stringWriter, modeInfo.infoType);
            if (modeInfo.infoType == CcdWrapper.DisplayConfigModeInfoType.Target)
            {
                writerModeTarget.Serialize(stringWriter, modeInfo.targetMode);
            }
            else
            {
                writerModeSource.Serialize(stringWriter, modeInfo.sourceMode);
            }
            stringWriter.WriteLine("</modeInfo>");
        }
        stringWriter.WriteLine("</modeInfoArray>");

        return stringWriter.ToString();
    }

    public static string GetSettingsDirectory(string customSettingsDirectory)
    {
        if (string.IsNullOrEmpty(customSettingsDirectory))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MonitorSwitcher");
        }
        else
        {
            return customSettingsDirectory;
        }
    }

    public static string GetSettingsProfileDirectory(string settingsDirectory)
    {
        return Path.Combine(settingsDirectory, "Profiles");
    }
}
