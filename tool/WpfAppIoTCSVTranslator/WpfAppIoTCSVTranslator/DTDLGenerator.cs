using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace WpfAppIoTCSVTranslator
{
    public class DTDLGenerator
    {
        public static readonly string DTDLMark_DeviceId = "for DeviceId";
        public static readonly string DTDLMark_Timestamp = "for Timestamp";
        public static readonly string DTDLMark_Timestamp_AddKey = "Ticks";

        public static readonly string DTDLMark_ColmnOrder = "column order=";
        public string ModelId { get; set; }
        public string ModelDisplayName { get; set; }
        public string DeviceIdId { get; set; }
        public string DeviceIdName { get; set; }
        public string DeviceIdDisplayName { get; set; }
        public string TimestampName { get; set; }
        public string SourceCSVFileName { get; set; }
        public ObservableCollection<CSVColumnDefinition> CSVColums { get; set; }

        public string GeneratedDataModel { get; set; }

        public void GenerateDTDL()
        {
            var deviceIdColumns = CSVColums.Where(c => { return c.IsDeviceId; });
            bool deviceIdColumnExisted = false;
            if (deviceIdColumns.Count() > 0)
            {
                deviceIdColumnExisted = true;
            }
            var timestampColumns = CSVColums.Where(c => { return c.IsTimestamp; });
            bool timestampCoumnExisted = false;
            if (timestampColumns.Count() > 0)
            {
                timestampCoumnExisted = true;
            }
            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            {
                writer.WriteLine("{");
                IncrementIndent();
                writer.WriteLine($"{currentIndent}\"@context\": \"dtmi:dtdl:context; 2\",");
                writer.WriteLine($"{currentIndent}\"@id\": \"{ModelId}\",");
                writer.WriteLine($"{currentIndent}\"@type\": \"Interface\",");
                writer.WriteLine($"{currentIndent}\"displayName\": \"{ModelDisplayName}\",");
                var fi = new FileInfo(SourceCSVFileName);
                writer.WriteLine($"{currentIndent}\"description\": \"source file - '{fi.Name}'\",");
                writer.WriteLine($"{currentIndent}\"contents\": [");
                IncrementIndent();
                if (!deviceIdColumnExisted && !string.IsNullOrEmpty(DeviceIdName))
                {
                    var description = $"Additional Property {DTDLMark_DeviceId}";
                    AddPropertyDefinition(writer,DeviceIdId, DeviceIdName, DeviceIdDisplayName, "string", description,",");
                }
                if (!timestampCoumnExisted && !string.IsNullOrEmpty(TimestampName))
                {
                    var description = $"Additional Property {DTDLMark_Timestamp}";
                    AddPropertyDefinition(writer, null, TimestampName, TimestampName, "dateTime", description, ",");
                    description = $"Additional Property {DTDLMark_Timestamp}{DTDLMark_Timestamp_AddKey}";
                    AddPropertyDefinition(writer, null, $"{TimestampName}{DTDLMark_Timestamp_AddKey}", $"{TimestampName}{DTDLMark_Timestamp_AddKey}", "long", description, ",");
                }
                for (int i = 0; i < CSVColums.Count; i++)
                {
                    string id = null;
                    var name = CSVColums[i].Name;
                    var displayName = CSVColums[i].DisplayName;
                    //                    var schemaTypeName = CSVColums[i].Schema.Substring(CSVColums[i].Schema.LastIndexOf(" ") + 1);
                    var schemaTypeName = CSVColums[i].SchemaName;
                    var schema = schemaTypeName.Substring(0, 1).ToLower() + schemaTypeName.Substring(1);
                    var description = $"{DTDLMark_ColmnOrder}{CSVColums[i].Order}";
                    if (CSVColums[i].IsDeviceId)
                    {
                        id = DeviceIdId;
                        description += $",specified {DTDLMark_DeviceId}";
                    }
                    if (CSVColums[i].IsTimestamp)
                    {
                        description += $",specified {DTDLMark_Timestamp}";
                    }
                    string closing = "";
                    if (i < CSVColums.Count - 1)
                    {
                        closing = ",";
                    }
                    AddPropertyDefinition(writer, id, name, displayName, schema, description, closing);
                }
                DecrementIndent();
                writer.WriteLine($"{currentIndent}]");
                DecrementIndent();
                writer.WriteLine("}");
            }
            GeneratedDataModel = sb.ToString();
        }

        private void AddPropertyDefinition(StringWriter writer, string id, string name, string displayName, string schema, string description, string closing)
        {
            var opening = currentIndent + "{";
            IncrementIndent();
            writer.WriteLine($"{opening}");
            writer.WriteLine($"{currentIndent}\"@type\": \"Telemetry\",");
            if (!string.IsNullOrEmpty(id))
            {
                writer.WriteLine($"{currentIndent}\"@id\": \"{id}\",");
            }
            writer.WriteLine($"{currentIndent}\"name\": \"{name}\",");
            if (!string.IsNullOrEmpty(displayName))
                writer.WriteLine($"{currentIndent}\"displayName\": \"{displayName}\",");
            if (!string.IsNullOrEmpty(description)) 
                writer.WriteLine($"{currentIndent}\"description\": \"{description}\",");
            writer.WriteLine($"{currentIndent}\"schema\": \"{schema}\"");
            DecrementIndent();
            closing = "}"+closing;
            writer.WriteLine($"{currentIndent}{closing}");
        }

        int indentLevel = 0;
        string currentIndent = "";

        public void AddLine(string content, StringWriter writer)
        {
            writer.WriteLine($"{currentIndent}{content}");
        }

        public void IncrementIndent()
        {
            indentLevel++;
            currentIndent += "  ";
        }
        public void DecrementIndent()
        {
            indentLevel--;
            currentIndent = currentIndent.Substring(2);
        }
    }
}
