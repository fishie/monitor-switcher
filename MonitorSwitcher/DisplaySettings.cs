using System.Xml.Serialization;
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
        CcdWrapper.QueryDisplayFlags queryFlags = CcdWrapper.QueryDisplayFlags.AllPaths;
        if (activeOnly)
        {
            queryFlags = CcdWrapper.QueryDisplayFlags.OnlyActivePaths;
        }

        Log.Debug("Getting buffer size");
        var status = CcdWrapper.GetDisplayConfigBufferSizes(queryFlags,
            out var numPathArrayElements, out var numModeInfoArrayElements);
        if (status == 0)
        {
            pathInfoArray = new CcdWrapper.DisplayConfigPathInfo[numPathArrayElements];
            modeInfoArray = new CcdWrapper.DisplayConfigModeInfo[numModeInfoArrayElements];
            additionalInfo = new CcdWrapper.MonitorAdditionalInfo[numModeInfoArrayElements];

            Log.Debug("Querying display config");
            status = CcdWrapper.QueryDisplayConfig(queryFlags, ref numPathArrayElements, pathInfoArray,
                ref numModeInfoArrayElements, modeInfoArray, IntPtr.Zero);

            if (status == 0)
            {
                // cleanup of modeInfo bad elements
                int validCount = 0;
                foreach (var modeInfo in modeInfoArray)
                {
                    if (modeInfo.infoType != CcdWrapper.DisplayConfigModeInfoType.Zero)
                    {   // count number of valid mode Infos
                        validCount++;
                    }
                }
                if (validCount > 0)
                {   // only cleanup if there is at least one valid element found
                    var tempInfoArray = new CcdWrapper.DisplayConfigModeInfo[modeInfoArray.Length];
                    modeInfoArray.CopyTo(tempInfoArray, 0);
                    modeInfoArray = new CcdWrapper.DisplayConfigModeInfo[validCount];
                    int index = 0;
                    foreach (var modeInfo in tempInfoArray)
                    {
                        if (modeInfo.infoType != CcdWrapper.DisplayConfigModeInfoType.Zero)
                        {
                            modeInfoArray[index] = modeInfo;
                            index++;
                        }
                    }
                }

                // cleanup of currently not available pathInfo elements
                validCount = pathInfoArray.Count(pathInfo => pathInfo.targetInfo.targetAvailable);

                if (validCount > 0)
                {   // only cleanup if there is at least one valid element found
                    var tempInfoArray = new CcdWrapper.DisplayConfigPathInfo[pathInfoArray.Length];
                    pathInfoArray.CopyTo(tempInfoArray, 0);
                    pathInfoArray = new CcdWrapper.DisplayConfigPathInfo[validCount];
                    int index = 0;
                    foreach (var pathInfo in tempInfoArray)
                    {
                        if (pathInfo.targetInfo.targetAvailable)
                        {
                            pathInfoArray[index] = pathInfo;
                            index++;
                        }
                    }
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
                        additionalInfo[i] = CcdWrapper.GetMonitorAdditionalInfo(modeInfoArray[i].adapterId, modeInfoArray[i].id);
                    }
                    catch
                    {
                        additionalInfo[i].valid = false;
                    }
                }
                return true;
            }
            else
            {
                Log.Debug("Querying display config failed");
            }
        }
        else
        {
            Log.Debug("Getting Buffer Size Failed");
        }

        pathInfoArray = null;
        modeInfoArray = null;
        additionalInfo = null;
        return false;
    }

    public static bool SaveDisplaySettings(string fileName)
    {
        Log.Debug("Getting display config");
        if (GetDisplaySettings(out var pathInfoArray, out var modeInfoArray, out var additionalInfo, true))
        {
            // debug output complete display settings
            Log.Debug("Display settings to write:");
            Log.Debug(PrintDisplaySettings(pathInfoArray, modeInfoArray));

            Log.Debug("Initializing objects for Serialization");
            var writerAdditionalInfo = new XmlSerializer(typeof(CcdWrapper.MonitorAdditionalInfo));
            var writerPath = new XmlSerializer(typeof(CcdWrapper.DisplayConfigPathInfo));
            var writerModeTarget = new XmlSerializer(typeof(CcdWrapper.DisplayConfigTargetMode));
            var writerModeSource = new XmlSerializer(typeof(CcdWrapper.DisplayConfigSourceMode));
            var writerModeInfoType = new XmlSerializer(typeof(CcdWrapper.DisplayConfigModeInfoType));
            var writerModeAdapterID = new XmlSerializer(typeof(CcdWrapper.LUID));
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
                writerModeAdapterID.Serialize(xmlWriter, modeInfo.adapterId);
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
        else
        {
            Log.Error("Failed to get display settings");
        }

        return false;
    }

    public static bool LoadDisplaySettings(string filename, bool matchAdapterIds = true)
    {
        Log.Debug("Loading display settings from file: {Filename}", filename);
        if (!File.Exists(filename))
        {
            Log.Error("Failed to load display settings because file does not exist: {Filename}", filename);

            return false;
        }

        // Objects for DeSerialization of pathInfo and modeInfo classes
        Log.Debug("Initializing objects for Serialization");
        var readerAdditionalInfo = new XmlSerializer(typeof(CcdWrapper.MonitorAdditionalInfo));
        var readerPath = new XmlSerializer(typeof(CcdWrapper.DisplayConfigPathInfo));
        var readerModeTarget = new XmlSerializer(typeof(CcdWrapper.DisplayConfigTargetMode));
        var readerModeSource = new XmlSerializer(typeof(CcdWrapper.DisplayConfigSourceMode));
        var readerModeInfoType = new XmlSerializer(typeof(CcdWrapper.DisplayConfigModeInfoType));
        var readerModeAdapterID = new XmlSerializer(typeof(CcdWrapper.LUID));

        // Lists for storing the results
        var pathInfoList = new List<CcdWrapper.DisplayConfigPathInfo>();
        var modeInfoList = new List<CcdWrapper.DisplayConfigModeInfo>();
        var additionalInfoList = new List<CcdWrapper.MonitorAdditionalInfo>();

        // Loading the xml file
        Log.Debug("Parsing XML file");
        XmlReader xmlReader = XmlReader.Create(filename);
        xmlReader.Read();
        while (true)
        {
            Log.Debug("\tXML Element: " + xmlReader.Name);
            if ((xmlReader.Name.CompareTo("DisplayConfigPathInfo") == 0) && (xmlReader.IsStartElement()))
            {
                var pathInfo = readerPath.Deserialize<CcdWrapper.DisplayConfigPathInfo>(xmlReader);
                pathInfoList.Add(pathInfo);
                continue;
            }
            else if ((xmlReader.Name.CompareTo("modeInfo") == 0) && (xmlReader.IsStartElement()))
            {
                Log.Debug("\t\tReading modeInfo");
                var modeInfo = new CcdWrapper.DisplayConfigModeInfo();
                xmlReader.Read();
                xmlReader.Read();
                modeInfo.id = Convert.ToUInt32(xmlReader.Value);
                xmlReader.Read();
                xmlReader.Read();
                modeInfo.adapterId = readerModeAdapterID.Deserialize<CcdWrapper.LUID>(xmlReader);
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
                continue;
            }
            else if ((xmlReader.Name.CompareTo("MonitorAdditionalInfo") == 0) && (xmlReader.IsStartElement()))
            {
                Log.Debug("\t\tReading additional information");
                var additionalInfo = readerAdditionalInfo.Deserialize<CcdWrapper.MonitorAdditionalInfo>(xmlReader);
                additionalInfoList.Add(additionalInfo);
                continue;
            }

            if (!xmlReader.Read())
            {
                break;
            }
        }
        xmlReader.Close();
        Log.Debug("Parsing of XML file successful");

        // Convert C# lists to simply array
        Log.Debug("Converting to simple arrays for API compatibility");
        var pathInfoArray = new CcdWrapper.DisplayConfigPathInfo[pathInfoList.Count];
        for (int iPathInfo = 0; iPathInfo < pathInfoList.Count; iPathInfo++)
        {
            pathInfoArray[iPathInfo] = pathInfoList[iPathInfo];
        }

        var modeInfoArray = new CcdWrapper.DisplayConfigModeInfo[modeInfoList.Count];
        for (int iModeInfo = 0; iModeInfo < modeInfoList.Count; iModeInfo++)
        {
            modeInfoArray[iModeInfo] = modeInfoList[iModeInfo];
        }

        // Get current display settings
        Log.Debug("Getting current display settings");
        if (GetDisplaySettings(out var pathInfoArrayCurrent, out var modeInfoArrayCurrent, out var additionalInfoCurrent, false))
        {
            if (matchAdapterIds)
            {
                // For some reason the adapterID parameter changes upon system restart, all other parameters however, especially the ID remain constant.
                // We check the loaded settings against the current settings replacing the adapterID with the other parameters
                Log.Debug("Matching of adapter IDs for pathInfo");
                for (int iPathInfo = 0; iPathInfo < pathInfoArray.Length; iPathInfo++)
                {
                    for (int iPathInfoCurrent = 0; iPathInfoCurrent < pathInfoArrayCurrent.Length; iPathInfoCurrent++)
                    {
                        Log.Debug("\t---");
                        Log.Debug("\tIndex XML = " + iPathInfo);
                        Log.Debug("\tIndex Current = " + iPathInfoCurrent);
                        Log.Debug("\tsourceInfo.id XML = " + pathInfoArray[iPathInfo].sourceInfo.id);
                        Log.Debug("\tsourceInfo.id Current = " + pathInfoArrayCurrent[iPathInfoCurrent].sourceInfo.id);
                        Log.Debug("\ttargetInfo.id XML = " + pathInfoArray[iPathInfo].targetInfo.id);
                        Log.Debug("\ttargetInfo.id Current = " + pathInfoArrayCurrent[iPathInfoCurrent].targetInfo.id);
                        Log.Debug("\tsourceInfo.adapterId XML = " + pathInfoArray[iPathInfo].sourceInfo.adapterId.LowPart);
                        Log.Debug("\tsourceInfo.adapterId Current = " + pathInfoArrayCurrent[iPathInfoCurrent].sourceInfo.adapterId.LowPart);
                        Log.Debug("\ttargetInfo.adapterId XML = " + pathInfoArray[iPathInfo].targetInfo.adapterId.LowPart);
                        Log.Debug("\ttargetInfo.adapterId Current = " + pathInfoArrayCurrent[iPathInfoCurrent].targetInfo.adapterId.LowPart);
                        if ((pathInfoArray[iPathInfo].sourceInfo.id == pathInfoArrayCurrent[iPathInfoCurrent].sourceInfo.id) &&
                            (pathInfoArray[iPathInfo].targetInfo.id == pathInfoArrayCurrent[iPathInfoCurrent].targetInfo.id))
                        {
                            Log.Debug("\t!!! Both IDs are a match, assigning current adapter ID !!!");
                            pathInfoArray[iPathInfo].sourceInfo.adapterId.LowPart = pathInfoArrayCurrent[iPathInfoCurrent].sourceInfo.adapterId.LowPart;
                            pathInfoArray[iPathInfo].targetInfo.adapterId.LowPart = pathInfoArrayCurrent[iPathInfoCurrent].targetInfo.adapterId.LowPart;
                            break;
                        }
                        Log.Debug("\t---");
                    }
                }

                // Same again for modeInfo, however we get the required adapterId information from the pathInfoArray
                Log.Debug("Matching of adapter IDs for modeInfo");
                for (int iModeInfo = 0; iModeInfo < modeInfoArray.Length; iModeInfo++)
                {
                    for (int iPathInfo = 0; iPathInfo < pathInfoArray.Length; iPathInfo++)
                    {
                        Log.Debug("\t---");
                        Log.Debug("\tIndex Mode = " + iModeInfo);
                        Log.Debug("\tIndex Path = " + iPathInfo);
                        Log.Debug("\tmodeInfo.id = " + modeInfoArray[iModeInfo].id);
                        Log.Debug("\tpathInfo.id = " + pathInfoArray[iPathInfo].targetInfo.id);
                        Log.Debug("\tmodeInfo.infoType = " + modeInfoArray[iModeInfo].infoType);
                        if ((modeInfoArray[iModeInfo].id == pathInfoArray[iPathInfo].targetInfo.id) &&
                            (modeInfoArray[iModeInfo].infoType == CcdWrapper.DisplayConfigModeInfoType.Target))
                        {
                            Log.Debug("\t\tTarget adapter id found, checking for source modeInfo and adapterID");
                            // We found target adapter id, now lets look for the source modeInfo and adapterID
                            for (int iModeInfoSource = 0; iModeInfoSource < modeInfoArray.Length; iModeInfoSource++)
                            {
                                Log.Debug("\t\t---");
                                Log.Debug("\t\tIndex = " + iModeInfoSource);
                                Log.Debug("\t\tmodeInfo.id Source = " + modeInfoArray[iModeInfoSource].id);
                                Log.Debug("\t\tpathInfo.sourceInfo.id = " + pathInfoArray[iPathInfo].sourceInfo.id);
                                Log.Debug("\t\tmodeInfo.adapterId = " + modeInfoArray[iModeInfo].adapterId.LowPart);
                                Log.Debug("\t\tmodeInfo.adapterId Source = " + modeInfoArray[iModeInfoSource].adapterId.LowPart);
                                Log.Debug("\t\tmodeInfo.infoType Source = " + modeInfoArray[iModeInfoSource].infoType);
                                if ((modeInfoArray[iModeInfoSource].id == pathInfoArray[iPathInfo].sourceInfo.id) &&
                                    (modeInfoArray[iModeInfoSource].adapterId.LowPart == modeInfoArray[iModeInfo].adapterId.LowPart) &&
                                    (modeInfoArray[iModeInfoSource].infoType == CcdWrapper.DisplayConfigModeInfoType.Source))
                                {
                                    Log.Debug("\t\t!!! IDs are a match, taking adapter id from pathInfo !!!");
                                    modeInfoArray[iModeInfoSource].adapterId.LowPart = pathInfoArray[iPathInfo].sourceInfo.adapterId.LowPart;
                                    break;
                                }
                                Log.Debug("\t\t---");
                            }
                            modeInfoArray[iModeInfo].adapterId.LowPart = pathInfoArray[iPathInfo].targetInfo.adapterId.LowPart;
                            break;
                        }
                        Log.Debug("\t---");
                    }
                }
                Log.Debug("Done matching of adapter IDs");
            }

            // Set loaded display settings
            Log.Debug("Setting up final display settings to load");

            // debug output complete display settings
            Log.Debug("\nDisplay settings to be loaded: ");
            Log.Debug(PrintDisplaySettings(pathInfoArray, modeInfoArray));

            uint numPathArrayElements = (uint)pathInfoArray.Length;
            uint numModeInfoArrayElements = (uint)modeInfoArray.Length;

            // First let's try without SdcFlags.AllowChanges
            long status = CcdWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                      CcdWrapper.SdcFlags.Apply | CcdWrapper.SdcFlags.UseSuppliedDisplayConfig | CcdWrapper.SdcFlags.SaveToDatabase | CcdWrapper.SdcFlags.NoOptimization);

            if (status != 0)
            {// try again with SdcFlags.AllowChanges
                Log.Error("Failed to set display settings without SdcFlags.AllowChanges, ERROR: {Status}", status);
                Log.Information("Trying again with additional SdcFlags.AllowChanges flag");
                status = CcdWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                      CcdWrapper.SdcFlags.Apply | CcdWrapper.SdcFlags.UseSuppliedDisplayConfig | CcdWrapper.SdcFlags.SaveToDatabase | CcdWrapper.SdcFlags.NoOptimization | CcdWrapper.SdcFlags.AllowChanges);
            }

            if (status != 0)
            {
                Log.Error("Failed to set display settings using default method, ERROR: {Status}", status);

                if ((additionalInfoCurrent.Length > 0) && (additionalInfoList.Count > 0)) // only if present, e.g. new profile
                {
                    Log.Information("Trying alternative method");
                    // Restore original settings and adapter IDs
                    Log.Debug("Converting again to simple arrays for API compatibility");
                    for (int iPathInfo = 0; iPathInfo < pathInfoList.Count; iPathInfo++)
                    {
                        pathInfoArray[iPathInfo] = pathInfoList[iPathInfo];
                    }

                    for (int iModeInfo = 0; iModeInfo < modeInfoList.Count; iModeInfo++)
                    {
                        modeInfoArray[iModeInfo] = modeInfoList[iModeInfo];
                    }

                    Log.Debug("Alternative matching mode");
                    // For each modeInfo iterate over the current additional information, i.e. monitor names and paths, and find the one matching in the current setup
                    for (int iModeInfo = 0; iModeInfo < modeInfoArray.Length; iModeInfo++)
                    {
                        for (int iAdditionalInfoCurrent = 0; iAdditionalInfoCurrent < additionalInfoCurrent.Length; iAdditionalInfoCurrent++)
                        {
                            if ((additionalInfoCurrent[iAdditionalInfoCurrent].monitorFriendlyDevice != null) && (additionalInfoList[iModeInfo].monitorFriendlyDevice != null))
                            {
                                if (additionalInfoCurrent[iAdditionalInfoCurrent].monitorFriendlyDevice == additionalInfoList[iModeInfo].monitorFriendlyDevice)
                                {
                                    CcdWrapper.LUID originalID = modeInfoArray[iModeInfo].adapterId;
                                    // now also find all other matching pathInfo modeInfos with that ID and change it
                                    for (int iPathInfo = 0; iPathInfo < pathInfoArray.Length; iPathInfo++)
                                    {
                                        if ((pathInfoArray[iPathInfo].targetInfo.adapterId.LowPart == originalID.LowPart) &&
                                           (pathInfoArray[iPathInfo].targetInfo.adapterId.HighPart == originalID.HighPart))
                                        {
                                            pathInfoArray[iPathInfo].targetInfo.adapterId = modeInfoArrayCurrent[iAdditionalInfoCurrent].adapterId;
                                            pathInfoArray[iPathInfo].sourceInfo.adapterId = modeInfoArrayCurrent[iAdditionalInfoCurrent].adapterId;
                                            pathInfoArray[iPathInfo].targetInfo.id = modeInfoArrayCurrent[iAdditionalInfoCurrent].id;
                                        }
                                    }
                                    for (int iModeInfoFix = 0; iModeInfoFix < modeInfoArray.Length; iModeInfoFix++)
                                    {
                                        if ((modeInfoArray[iModeInfoFix].adapterId.LowPart == originalID.LowPart) &&
                                            (modeInfoArray[iModeInfoFix].adapterId.HighPart == originalID.HighPart))
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
                    status = CcdWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                         CcdWrapper.SdcFlags.Apply | CcdWrapper.SdcFlags.UseSuppliedDisplayConfig | CcdWrapper.SdcFlags.NoOptimization | CcdWrapper.SdcFlags.SaveToDatabase);

                    if (status != 0)
                    {   // again with SdcFlags.AllowChanges
                        status = CcdWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                             CcdWrapper.SdcFlags.Apply | CcdWrapper.SdcFlags.UseSuppliedDisplayConfig | CcdWrapper.SdcFlags.NoOptimization | CcdWrapper.SdcFlags.SaveToDatabase | CcdWrapper.SdcFlags.AllowChanges);
                    }
                }

                if (status != 0)
                {
                    Log.Error("Failed to set display settings using alternative method, ERROR: {Status}", status);
                    Log.Information("Trying yet another method for adapter ID matching:");

                    // Restore original settings and adapter IDs
                    Log.Debug("Converting again to simple arrays for API compatibility");
                    for (int iPathInfo = 0; iPathInfo < pathInfoList.Count; iPathInfo++)
                    {
                        pathInfoArray[iPathInfo] = pathInfoList[iPathInfo];
                    }

                    for (int iModeInfo = 0; iModeInfo < modeInfoList.Count; iModeInfo++)
                    {
                        modeInfoArray[iModeInfo] = modeInfoList[iModeInfo];
                    }

                    // The next method is identical to the first one but uses a more radical adapter ID assignment
                    for (int iPathInfo = 0; iPathInfo < pathInfoArray.Length; iPathInfo++)
                    {
                        for (int iPathInfoCurrent = 0; iPathInfoCurrent < pathInfoArrayCurrent.Length; iPathInfoCurrent++)
                        {
                            if ((pathInfoArray[iPathInfo].sourceInfo.id == pathInfoArrayCurrent[iPathInfoCurrent].sourceInfo.id) &&
                                (pathInfoArray[iPathInfo].targetInfo.id == pathInfoArrayCurrent[iPathInfoCurrent].targetInfo.id))
                            {
                                Log.Debug("\t!!! Both IDs are a match, getting new Adapter ID and replacing all other IDs !!!");
                                uint oldID = pathInfoArray[iPathInfo].sourceInfo.adapterId.LowPart;
                                uint newID = pathInfoArrayCurrent[iPathInfoCurrent].sourceInfo.adapterId.LowPart;
                                for (int iPathInfoReplace = 0; iPathInfoReplace < pathInfoArray.Length; iPathInfoReplace++)
                                {
                                    if (pathInfoArray[iPathInfoReplace].sourceInfo.adapterId.LowPart == oldID)
                                        pathInfoArray[iPathInfoReplace].sourceInfo.adapterId.LowPart = newID;
                                    if (pathInfoArray[iPathInfoReplace].targetInfo.adapterId.LowPart == oldID)
                                        pathInfoArray[iPathInfoReplace].targetInfo.adapterId.LowPart = newID;
                                }

                                for (int iModeInfoReplace = 0; iModeInfoReplace < modeInfoArray.Length; iModeInfoReplace++)
                                {
                                    if (modeInfoArray[iModeInfoReplace].adapterId.LowPart == oldID)
                                    {
                                        modeInfoArray[iModeInfoReplace].adapterId.LowPart = newID;
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
                    status = CcdWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                            CcdWrapper.SdcFlags.Apply | CcdWrapper.SdcFlags.UseSuppliedDisplayConfig | CcdWrapper.SdcFlags.SaveToDatabase | CcdWrapper.SdcFlags.NoOptimization | CcdWrapper.SdcFlags.AllowChanges);

                    if (status != 0)
                    {   // again with SdcFlags.AllowChanges
                        status = CcdWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                                                    CcdWrapper.SdcFlags.Apply | CcdWrapper.SdcFlags.UseSuppliedDisplayConfig | CcdWrapper.SdcFlags.SaveToDatabase | CcdWrapper.SdcFlags.NoOptimization | CcdWrapper.SdcFlags.AllowChanges);
                    }
                }

                if (status != 0)
                {
                    Log.Error("Failed to set display settings using the other alternative method, ERROR: {Status}", status);
                    return false;
                }
            }

            return true;
        }

        Log.Debug("Failed to get current display settings");
        return false;
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
        var writerModeAdapterID = new XmlSerializer(typeof(CcdWrapper.LUID));

        // write content to string
        stringWriter.WriteLine("<displaySettings>");
        stringWriter.WriteLine("<pathInfoArray>");
        foreach (CcdWrapper.DisplayConfigPathInfo pathInfo in pathInfoArray)
        {
            writerPath.Serialize(stringWriter, pathInfo);
        }
        stringWriter.WriteLine("</pathInfoArray>");

        stringWriter.WriteLine("<modeInfoArray>");
        foreach (var modeInfo in modeInfoArray)
        {
            stringWriter.WriteLine("<modeInfo>");
            stringWriter.WriteLine("<id>" + modeInfo.id.ToString() + "</id>");
            writerModeAdapterID.Serialize(stringWriter, modeInfo.adapterId);
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
