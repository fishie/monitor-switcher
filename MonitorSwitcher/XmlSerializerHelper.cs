using System.Xml;
using System.Xml.Serialization;

namespace MonitorSwitcher;

public static class XmlSerializerHelper
{
    public static T Deserialize<T>(this XmlSerializer xmlSerializer, XmlReader xmlReader) =>
        (T)(xmlSerializer.Deserialize(xmlReader)
            ?? throw new NullReferenceException("Failed to deserialize XML"));
}
