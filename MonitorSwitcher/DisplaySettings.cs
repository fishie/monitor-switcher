using System.Xml.Serialization;
using System.Xml;

namespace MonitorSwitcher;

public static class DisplaySettings
{
    public static bool debug;
    public static bool noIDMatch;

    public static void DebugOutput(string text)
    {
        if (debug)
        {
            Console.WriteLine(text);
        }
    }

    public static bool GetDisplaySettings(ref CcdWrapper.DisplayConfigPathInfo[] pathInfoArray, ref CcdWrapper.DisplayConfigModeInfo[] modeInfoArray, ref CcdWrapper.MonitorAdditionalInfo[] additionalInfo, bool ActiveOnly)
    {
        uint numPathArrayElements;
        uint numModeInfoArrayElements;

        // query active paths from the current computer.
        DebugOutput("Getting display settings");
        CcdWrapper.QueryDisplayFlags queryFlags = CcdWrapper.QueryDisplayFlags.AllPaths;
        if (ActiveOnly)
        {
            queryFlags = CcdWrapper.QueryDisplayFlags.OnlyActivePaths;
        }

        DebugOutput("Getting buffer size");
        var status = CcdWrapper.GetDisplayConfigBufferSizes(queryFlags, out numPathArrayElements, out numModeInfoArrayElements);
        if (status == 0)
        {
            pathInfoArray = new CcdWrapper.DisplayConfigPathInfo[numPathArrayElements];
            modeInfoArray = new CcdWrapper.DisplayConfigModeInfo[numModeInfoArrayElements];
            additionalInfo = new CcdWrapper.MonitorAdditionalInfo[numModeInfoArrayElements];

            DebugOutput("Querying display config");
            status = CcdWrapper.QueryDisplayConfig(queryFlags,
                                                   ref numPathArrayElements, pathInfoArray, ref numModeInfoArrayElements,
                                                   modeInfoArray, IntPtr.Zero);

            if (status == 0)
            {
                // cleanup of modeInfo bad elements 
                int validCount = 0;
                foreach (CcdWrapper.DisplayConfigModeInfo modeInfo in modeInfoArray)
                {
                    if (modeInfo.infoType != CcdWrapper.DisplayConfigModeInfoType.Zero)
                    {   // count number of valid mode Infos
                        validCount++;
                    }
                }
                if (validCount > 0)
                {   // only cleanup if there is at least one valid element found
                    CcdWrapper.DisplayConfigModeInfo[] tempInfoArray = new CcdWrapper.DisplayConfigModeInfo[modeInfoArray.Count()];
                    modeInfoArray.CopyTo(tempInfoArray, 0);
                    modeInfoArray = new CcdWrapper.DisplayConfigModeInfo[validCount];
                    int index = 0;
                    foreach (CcdWrapper.DisplayConfigModeInfo modeInfo in tempInfoArray)
                    {
                        if (modeInfo.infoType != CcdWrapper.DisplayConfigModeInfoType.Zero)
                        {
                            modeInfoArray[index] = modeInfo;
                            index++;
                        }
                    }
                }

                // cleanup of currently not available pathInfo elements
                validCount = 0;
                foreach (CcdWrapper.DisplayConfigPathInfo pathInfo in pathInfoArray)
                {
                    if (pathInfo.targetInfo.targetAvailable)
                    {
                        validCount++;
                    }
                }
                if (validCount > 0)
                {   // only cleanup if there is at least one valid element found
                    CcdWrapper.DisplayConfigPathInfo[] tempInfoArray = new CcdWrapper.DisplayConfigPathInfo[pathInfoArray.Count()];
                    pathInfoArray.CopyTo(tempInfoArray, 0);
                    pathInfoArray = new CcdWrapper.DisplayConfigPathInfo[validCount];
                    int index = 0;
                    foreach (CcdWrapper.DisplayConfigPathInfo pathInfo in tempInfoArray)
                    {
                        if (pathInfo.targetInfo.targetAvailable)
                        {
                            pathInfoArray[index] = pathInfo;
                            index++;
                        }
                    }
                }

                // get the display names for all modes
                for (var iMode = 0; iMode < modeInfoArray.Count(); iMode++)
                {
                    if (modeInfoArray[iMode].infoType == CcdWrapper.DisplayConfigModeInfoType.Target)
                    {
                        try
                        {
                            additionalInfo[iMode] = CcdWrapper.GetMonitorAdditionalInfo(modeInfoArray[iMode].adapterId, modeInfoArray[iMode].id);
                        }
                        catch
                        {
                            additionalInfo[iMode].valid = false;
                        }
                    }
                }
                return true;
            }
            else
            {
                DebugOutput("Querying display config failed");
            }
        }
        else
        {
            DebugOutput("Getting Buffer Size Failed");
        }

        return false;
    }

    public static bool SaveDisplaySettings(string fileName)
    {
        var pathInfoArray = new CcdWrapper.DisplayConfigPathInfo[0];
        var modeInfoArray = new CcdWrapper.DisplayConfigModeInfo[0];
        var additionalInfo = new CcdWrapper.MonitorAdditionalInfo[0];

        DebugOutput("Getting display config");
        bool status = GetDisplaySettings(ref pathInfoArray, ref modeInfoArray, ref additionalInfo, true);
        if (status)
        {
            if (debug)
            {
                // debug output complete display settings
                DebugOutput("Display settings to write:");
                Console.WriteLine(PrintDisplaySettings(pathInfoArray, modeInfoArray));
            }

            DebugOutput("Initializing objects for Serialization");
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
            foreach (CcdWrapper.DisplayConfigPathInfo pathInfo in pathInfoArray)
            {
                writerPath.Serialize(xmlWriter, pathInfo);
            }
            xmlWriter.WriteEndElement();

            xmlWriter.WriteStartElement("modeInfoArray");
            for (int iModeInfo = 0; iModeInfo < modeInfoArray.Length; iModeInfo++)
            {
                xmlWriter.WriteStartElement("modeInfo");
                CcdWrapper.DisplayConfigModeInfo modeInfo = modeInfoArray[iModeInfo];
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
            for (int iAdditionalInfo = 0; iAdditionalInfo < additionalInfo.Length; iAdditionalInfo++)
            {
                writerAdditionalInfo.Serialize(xmlWriter, additionalInfo[iAdditionalInfo]);
            }
            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndDocument();
            xmlWriter.Flush();
            xmlWriter.Close();

            return true;
        }
        else
        {
            Console.WriteLine("Failed to get display settings, ERROR: " + status.ToString());
        }

        return false;
    }

    public static bool LoadDisplaySettings(string fileName)
    {
        DebugOutput("Loading display settings from file: " + fileName);
        if (!File.Exists(fileName))
        {
            Console.WriteLine("Failed to load display settings because file does not exist: " + fileName);

            return false;
        }

        // Objects for DeSerialization of pathInfo and modeInfo classes
        DebugOutput("Initializing objects for Serialization");
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
        DebugOutput("Parsing XML file");
        XmlReader xml = XmlReader.Create(fileName);
        xml.Read();
        while (true)
        {
            DebugOutput("\tXML Element: " + xml.Name);
            if ((xml.Name.CompareTo("DisplayConfigPathInfo") == 0) && (xml.IsStartElement()))
            {
                CcdWrapper.DisplayConfigPathInfo pathInfo = (CcdWrapper.DisplayConfigPathInfo)readerPath.Deserialize(xml);
                pathInfoList.Add(pathInfo);
                continue;
            }
            else if ((xml.Name.CompareTo("modeInfo") == 0) && (xml.IsStartElement()))
            {
                DebugOutput("\t\tReading modeInfo");
                CcdWrapper.DisplayConfigModeInfo modeInfo = new CcdWrapper.DisplayConfigModeInfo();
                xml.Read();
                xml.Read();
                modeInfo.id = Convert.ToUInt32(xml.Value);
                xml.Read();
                xml.Read();
                modeInfo.adapterId = (CcdWrapper.LUID)readerModeAdapterID.Deserialize(xml);
                modeInfo.infoType = (CcdWrapper.DisplayConfigModeInfoType)readerModeInfoType.Deserialize(xml);
                if (modeInfo.infoType == CcdWrapper.DisplayConfigModeInfoType.Target)
                {
                    modeInfo.targetMode = (CcdWrapper.DisplayConfigTargetMode)readerModeTarget.Deserialize(xml);
                }
                else
                {
                    modeInfo.sourceMode = (CcdWrapper.DisplayConfigSourceMode)readerModeSource.Deserialize(xml);
                }
                DebugOutput("\t\t\tmodeInfo.id = " + modeInfo.id);
                DebugOutput("\t\t\tmodeInfo.adapterId (High Part) = " + modeInfo.adapterId.HighPart);
                DebugOutput("\t\t\tmodeInfo.adapterId (Low Part) = " + modeInfo.adapterId.LowPart);
                DebugOutput("\t\t\tmodeInfo.infoType = " + modeInfo.infoType);

                modeInfoList.Add(modeInfo);
                continue;
            }
            else if ((xml.Name.CompareTo("MonitorAdditionalInfo") == 0) && (xml.IsStartElement()))
            {
                DebugOutput("\t\tReading additional informations");
                CcdWrapper.MonitorAdditionalInfo additionalInfo = (CcdWrapper.MonitorAdditionalInfo)readerAdditionalInfo.Deserialize(xml);
                additionalInfoList.Add(additionalInfo);
                continue;
            }

            if (!xml.Read())
            {
                break;
            }
        }
        xml.Close();
        DebugOutput("Parsing of XML file successful");

        // Convert C# lists to simply array
        DebugOutput("Converting to simple arrays for API compatibility");
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
        DebugOutput("Getting current display settings");
        var pathInfoArrayCurrent = new CcdWrapper.DisplayConfigPathInfo[0];
        var modeInfoArrayCurrent = new CcdWrapper.DisplayConfigModeInfo[0];
        var additionalInfoCurrent = new CcdWrapper.MonitorAdditionalInfo[0];

        bool statusCurrent = GetDisplaySettings(ref pathInfoArrayCurrent, ref modeInfoArrayCurrent, ref additionalInfoCurrent, false);
        if (statusCurrent)
        {
            if (!noIDMatch)
            {
                // For some reason the adapterID parameter changes upon system restart, all other parameters however, especially the ID remain constant.
                // We check the loaded settings against the current settings replacing the adapaterID with the other parameters
                DebugOutput("Matching of adapter IDs for pathInfo");
                for (int iPathInfo = 0; iPathInfo < pathInfoArray.Length; iPathInfo++)
                {
                    for (int iPathInfoCurrent = 0; iPathInfoCurrent < pathInfoArrayCurrent.Length; iPathInfoCurrent++)
                    {
                        DebugOutput("\t---");
                        DebugOutput("\tIndex XML = " + iPathInfo);
                        DebugOutput("\tIndex Current = " + iPathInfoCurrent);
                        DebugOutput("\tsourceInfo.id XML = " + pathInfoArray[iPathInfo].sourceInfo.id);
                        DebugOutput("\tsourceInfo.id Current = " + pathInfoArrayCurrent[iPathInfoCurrent].sourceInfo.id);
                        DebugOutput("\ttargetInfo.id XML = " + pathInfoArray[iPathInfo].targetInfo.id);
                        DebugOutput("\ttargetInfo.id Current = " + pathInfoArrayCurrent[iPathInfoCurrent].targetInfo.id);
                        DebugOutput("\tsourceInfo.adapterId XML = " + pathInfoArray[iPathInfo].sourceInfo.adapterId.LowPart);
                        DebugOutput("\tsourceInfo.adapterId Current = " + pathInfoArrayCurrent[iPathInfoCurrent].sourceInfo.adapterId.LowPart);
                        DebugOutput("\ttargetInfo.adapterId XML = " + pathInfoArray[iPathInfo].targetInfo.adapterId.LowPart);
                        DebugOutput("\ttargetInfo.adapterId Current = " + pathInfoArrayCurrent[iPathInfoCurrent].targetInfo.adapterId.LowPart);
                        if ((pathInfoArray[iPathInfo].sourceInfo.id == pathInfoArrayCurrent[iPathInfoCurrent].sourceInfo.id) &&
                            (pathInfoArray[iPathInfo].targetInfo.id == pathInfoArrayCurrent[iPathInfoCurrent].targetInfo.id))
                        {
                            DebugOutput("\t!!! Both IDs are a match, assigning current adapter ID !!!");
                            pathInfoArray[iPathInfo].sourceInfo.adapterId.LowPart = pathInfoArrayCurrent[iPathInfoCurrent].sourceInfo.adapterId.LowPart;
                            pathInfoArray[iPathInfo].targetInfo.adapterId.LowPart = pathInfoArrayCurrent[iPathInfoCurrent].targetInfo.adapterId.LowPart;
                            break;
                        }
                        DebugOutput("\t---");
                    }
                }

                // Same again for modeInfo, however we get the required adapterId information from the pathInfoArray
                DebugOutput("Matching of adapter IDs for modeInfo");
                for (int iModeInfo = 0; iModeInfo < modeInfoArray.Length; iModeInfo++)
                {
                    for (int iPathInfo = 0; iPathInfo < pathInfoArray.Length; iPathInfo++)
                    {
                        DebugOutput("\t---");
                        DebugOutput("\tIndex Mode = " + iModeInfo);
                        DebugOutput("\tIndex Path = " + iPathInfo);
                        DebugOutput("\tmodeInfo.id = " + modeInfoArray[iModeInfo].id);
                        DebugOutput("\tpathInfo.id = " + pathInfoArray[iPathInfo].targetInfo.id);
                        DebugOutput("\tmodeInfo.infoType = " + modeInfoArray[iModeInfo].infoType);
                        if ((modeInfoArray[iModeInfo].id == pathInfoArray[iPathInfo].targetInfo.id) &&
                            (modeInfoArray[iModeInfo].infoType == CcdWrapper.DisplayConfigModeInfoType.Target))
                        {
                            DebugOutput("\t\tTarget adapter id found, checking for source modeInfo and adpaterID");
                            // We found target adapter id, now lets look for the source modeInfo and adapterID
                            for (int iModeInfoSource = 0; iModeInfoSource < modeInfoArray.Length; iModeInfoSource++)
                            {
                                DebugOutput("\t\t---");
                                DebugOutput("\t\tIndex = " + iModeInfoSource);
                                DebugOutput("\t\tmodeInfo.id Source = " + modeInfoArray[iModeInfoSource].id);
                                DebugOutput("\t\tpathInfo.sourceInfo.id = " + pathInfoArray[iPathInfo].sourceInfo.id);
                                DebugOutput("\t\tmodeInfo.adapterId = " + modeInfoArray[iModeInfo].adapterId.LowPart);
                                DebugOutput("\t\tmodeInfo.adapterId Source = " + modeInfoArray[iModeInfoSource].adapterId.LowPart);
                                DebugOutput("\t\tmodeInfo.infoType Source = " + modeInfoArray[iModeInfoSource].infoType);
                                if ((modeInfoArray[iModeInfoSource].id == pathInfoArray[iPathInfo].sourceInfo.id) &&
                                    (modeInfoArray[iModeInfoSource].adapterId.LowPart == modeInfoArray[iModeInfo].adapterId.LowPart) &&
                                    (modeInfoArray[iModeInfoSource].infoType == CcdWrapper.DisplayConfigModeInfoType.Source))
                                {
                                    DebugOutput("\t\t!!! IDs are a match, taking adpater id from pathInfo !!!");
                                    modeInfoArray[iModeInfoSource].adapterId.LowPart = pathInfoArray[iPathInfo].sourceInfo.adapterId.LowPart;
                                    break;
                                }
                                DebugOutput("\t\t---");
                            }
                            modeInfoArray[iModeInfo].adapterId.LowPart = pathInfoArray[iPathInfo].targetInfo.adapterId.LowPart;
                            break;
                        }
                        DebugOutput("\t---");
                    }
                }
                DebugOutput("Done matching of adapter IDs");
            }

            // Set loaded display settings
            DebugOutput("Setting up final display settings to load");
            if (debug)
            {
                // debug output complete display settings
                Console.WriteLine("\nDisplay settings to be loaded: ");
                Console.WriteLine(PrintDisplaySettings(pathInfoArray, modeInfoArray));
            }
            uint numPathArrayElements = (uint)pathInfoArray.Length;
            uint numModeInfoArrayElements = (uint)modeInfoArray.Length;

            // First let's try without SdcFlags.AllowChanges
            long status = CcdWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                      CcdWrapper.SdcFlags.Apply | CcdWrapper.SdcFlags.UseSuppliedDisplayConfig | CcdWrapper.SdcFlags.SaveToDatabase | CcdWrapper.SdcFlags.NoOptimization);

            if (status != 0)
            {// try again with SdcFlags.AllowChanges
                Console.WriteLine("Failed to set display settings without SdcFlags.AllowChanges, ERROR: " + status.ToString());
                Console.WriteLine("Trying again with additional SdcFlags.AllowChanges flag");
                status = CcdWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                      CcdWrapper.SdcFlags.Apply | CcdWrapper.SdcFlags.UseSuppliedDisplayConfig | CcdWrapper.SdcFlags.SaveToDatabase | CcdWrapper.SdcFlags.NoOptimization | CcdWrapper.SdcFlags.AllowChanges);
            }

            if (status != 0)
            {
                Console.WriteLine("Failed to set display settings using default method, ERROR: " + status.ToString());

                if ((additionalInfoCurrent.Length > 0) && (additionalInfoList.Count > 0)) // only if present, e.g. new profile
                {
                    Console.WriteLine("Trying alternative method");
                    // Restore original settings and adapter IDs
                    DebugOutput("Converting again to simple arrays for API compatibility");
                    for (int iPathInfo = 0; iPathInfo < pathInfoList.Count; iPathInfo++)
                    {
                        pathInfoArray[iPathInfo] = pathInfoList[iPathInfo];
                    }

                    for (int iModeInfo = 0; iModeInfo < modeInfoList.Count; iModeInfo++)
                    {
                        modeInfoArray[iModeInfo] = modeInfoList[iModeInfo];
                    }

                    DebugOutput("Alternative matching mode");
                    // For each modeInfo iterate over the current additional informations, i.e. monitor names and paths, and find the one matching in the current setup
                    for (int iModeInfo = 0; iModeInfo < modeInfoArray.Length; iModeInfo++)
                    {
                        for (int iAdditionalInfoCurrent = 0; iAdditionalInfoCurrent < additionalInfoCurrent.Length; iAdditionalInfoCurrent++)
                        {
                            if ((additionalInfoCurrent[iAdditionalInfoCurrent].monitorFriendlyDevice != null) && (additionalInfoList[iModeInfo].monitorFriendlyDevice != null))
                            {
                                if (additionalInfoCurrent[iAdditionalInfoCurrent].monitorFriendlyDevice.Equals(additionalInfoList[iModeInfo].monitorFriendlyDevice))
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

                    if (debug)
                    {
                        // debug output complete display settings
                        Console.WriteLine("\nDisplay settings to be loaded: ");
                        Console.WriteLine(PrintDisplaySettings(pathInfoArray, modeInfoArray));
                    }


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
                    Console.WriteLine("Failed to set display settings using alternative method, ERROR: " + status.ToString());


                    Console.WriteLine("\nTrying yet another method for arapter ID maching:");

                    // Restore original settings and adapter IDs
                    DebugOutput("Converting again to simple arrays for API compatibility");
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
                                DebugOutput("\t!!! Both IDs are a match, getting new Adapter ID and replacing all other IDs !!!");
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
                            DebugOutput("\t---");
                        }
                    }

                    // Set loaded display settings
                    DebugOutput("Setting up final display settings to load");
                    if (debug)
                    {
                        // debug output complete display settings
                        Console.WriteLine("\nDisplay settings to be loaded: ");
                        Console.WriteLine(PrintDisplaySettings(pathInfoArray, modeInfoArray));
                    }

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
                    Console.WriteLine("Failed to set display settings using the other alternative method, ERROR: " + status.ToString());
                    return false;
                }
            }

            return true;
        }

        DebugOutput("Failed to get current display settings");
        return false;
    }

    public static string PrintDisplaySettings(CcdWrapper.DisplayConfigPathInfo[] pathInfoArray, CcdWrapper.DisplayConfigModeInfo[] modeInfoArray)
    {
        // initialize result
        string output = "";

        // initialize text writer
        StringWriter textWriter = new StringWriter();


        // initialize xml serializer
        var writerPath = new XmlSerializer(typeof(CcdWrapper.DisplayConfigPathInfo));
        var writerModeTarget = new XmlSerializer(typeof(CcdWrapper.DisplayConfigTargetMode));
        var writerModeSource = new XmlSerializer(typeof(CcdWrapper.DisplayConfigSourceMode));
        var writerModeInfoType = new XmlSerializer(typeof(CcdWrapper.DisplayConfigModeInfoType));
        var writerModeAdapterID = new XmlSerializer(typeof(CcdWrapper.LUID));

        // write content to string
        textWriter.WriteLine("<displaySettings>");
        textWriter.WriteLine("<pathInfoArray>");
        foreach (CcdWrapper.DisplayConfigPathInfo pathInfo in pathInfoArray)
        {
            writerPath.Serialize(textWriter, pathInfo);
        }
        textWriter.WriteLine("</pathInfoArray>");

        textWriter.WriteLine("<modeInfoArray>");
        for (int iModeInfo = 0; iModeInfo < modeInfoArray.Length; iModeInfo++)
        {
            textWriter.WriteLine("<modeInfo>");
            CcdWrapper.DisplayConfigModeInfo modeInfo = modeInfoArray[iModeInfo];
            textWriter.WriteLine("<id>" + modeInfo.id.ToString() + "</id>");
            writerModeAdapterID.Serialize(textWriter, modeInfo.adapterId);
            writerModeInfoType.Serialize(textWriter, modeInfo.infoType);
            if (modeInfo.infoType == CcdWrapper.DisplayConfigModeInfoType.Target)
            {
                writerModeTarget.Serialize(textWriter, modeInfo.targetMode);
            }
            else
            {
                writerModeSource.Serialize(textWriter, modeInfo.sourceMode);
            }
            textWriter.WriteLine("</modeInfo>");
        }
        textWriter.WriteLine("</modeInfoArray>");

        output = textWriter.ToString();
        return output;
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
