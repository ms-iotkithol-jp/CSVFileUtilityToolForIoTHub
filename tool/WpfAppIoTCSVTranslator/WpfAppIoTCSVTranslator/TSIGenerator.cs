using Microsoft.Azure.DigitalTwins.Parser;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace WpfAppIoTCSVTranslator
{
    public class TSIGenerator
    {
        ObservableCollection<string> hiearchs = new ObservableCollection<string>();
        public ObservableCollection<string> Hiearchies { get { return hiearchs; } }

        string indent = "";
        int indentLevel = 0;
        void IncIndent()
        {
            indentLevel++;
        }
        void DecIndent()
        {
            indentLevel--;
        }

        public string GenerateHierarchies(string id, string name)
        {
            var formatter = new IndentFormatter();
            formatter.WriteLine("{", incrementAfter:true);
            formatter.WriteLine("\"put\": [", incrementAfter:true);
            formatter.WriteLine("{", incrementAfter:true);
            formatter.WriteLine($"\"id\": \"{id}\",");
            formatter.WriteLine($"\"name\": \"{name.Replace(" ","_")}\",");
            formatter.WriteLine("\"source\": {", incrementAfter:true);
            formatter.WriteLine("\"instanceFieldNames\": [", incrementAfter:true);
            for (var i=0;i<hiearchs.Count;i++)
            {
                string end = "";
                if (i<hiearchs.Count-1)
                {
                    end = ",";
                }
                formatter.WriteLine($"\"{hiearchs[i]}\"{end}");
            }
            formatter.WriteLine("]", declementBefore: true);
            formatter.WriteLine("}", declementBefore: true);
            formatter.WriteLine("}", declementBefore: true);
            formatter.WriteLine("]", declementBefore: true);
            formatter.WriteLine("}", declementBefore: true);
            return formatter.ToString();
        }

        public string GenerateTypes(List<KeyValuePair<Dtmi, DTEntityInfo>> dtInterfaces, Dictionary<string, TypeDTDLInfo> interfaceIds)
        {
            var f = new IndentFormatter();

            f.WriteLine("{", incrementAfter: true);
            f.WriteLine("\"put\": [", incrementAfter: true);
            f.WriteLine("{", incrementAfter: true);
            f.WriteLine("\"id\": \"1be09af9-f089-4d6b-9f0b-48018b5f7393\",");
            f.WriteLine("\"name\": \"DefaultType\",");
            f.WriteLine("\"description\": \"Default type\",");
            f.WriteLine("\"variables\": {", incrementAfter: true);
            f.WriteLine("\"EventCount\": {", incrementAfter: true);
            f.WriteLine("\"kind\": \"aggregate\",");
            f.WriteLine("\"aggregation\": {", incrementAfter: true);
            f.WriteLine("\"tsx\": \"count()\"");
            f.WriteLine("}", declementBefore: true);
            f.WriteLine("}", declementBefore: true);
            f.WriteLine("}", declementBefore: true);
            f.WriteLine("},", declementBefore: true);
            int iCount = 0;
            var end = "";
            foreach (var dtIF in dtInterfaces)
            {
                var dtdl = dtIF.Value;
                if (dtdl.EntityKind == DTEntityKind.Interface)
                {
                    var dtdlIF = (DTInterfaceInfo)dtdl;
                    f.WriteLine("{", incrementAfter: true);
                    var id = Guid.NewGuid().ToString();
                    interfaceIds[dtdlIF.Id.ToString()].TypeId = id;
                    f.WriteLine($"\"id\":\"{id}\",");
                    f.WriteLine($"\"name\": \"{dtdlIF.Id}\",");
                    f.WriteLine($"\"description\": \"Generate from {interfaceIds[dtdlIF.Id.ToString()].DTDLFileName}\",");
                    f.WriteLine("\"variables\": {", incrementAfter: true);
                    var tCount = 0;
                    foreach (var m in dtdlIF.Contents)
                    {
                        if (m.Value.EntityKind == DTEntityKind.Telemetry)
                        {
                            var t = (DTTelemetryInfo)m.Value;
                            var descrip = t.Description["en"];
                            if (descrip.EndsWith(DTDLGenerator.DTDLMark_DeviceId) || descrip.EndsWith(DTDLGenerator.DTDLMark_Timestamp))
                            {
                                tCount++;
                                continue;
                            }

                            f.WriteLine($"\"{t.Name}\": " + "{", incrementAfter: true);
                            var kind = "numeric";
                            var kindVal = "Double";
                            var schema = t.Schema.DisplayName["en"];
                            if (schema != "long" && schema != "integer" && schema != "double")
                            {
                                kind = "Categorical";
                                if (schema.ToLower() == "datetime")
                                {
                                    kindVal = "DateTime";
                                }
                                else
                                {
                                    kindVal = "String";
                                }
                            }
                            else
                            {
                                if (schema != "double")
                                {
                                    kindVal = "Long";
                                }
                            }
                            f.WriteLine($"\"kind\": \"{kind}\", ");
                            f.WriteLine("\"value\": {", incrementAfter: true);
                            f.WriteLine($"\"tsx\": \"$event.[{t.Name}].{kindVal}\"");
                            f.WriteLine("},", declementBefore: true);
                            f.WriteLine("\"aggregation\": {", incrementAfter: true);
                            if (kind == "numeric")
                            {
                                f.WriteLine("\"tsx\": \"avg($value)\"");
                            }
                            else
                            {
                                f.WriteLine("\"tsx\": \"$value\"");

                            }
                            f.WriteLine("}", declementBefore: true);
                            tCount++;
                            end = "";
                            if (tCount < dtdlIF.Contents.Count)
                            {
                                end = ",";
                            }
                            f.WriteLine("}" + end, declementBefore: true);
                        }
                    }
                    f.WriteLine("}", declementBefore: true); // "Variables" {
                }
                end = "";
                iCount++;
                if (iCount < dtInterfaces.Count)
                {
                    end = ",";
                }
                f.WriteLine("}" + end, declementBefore: true);
            }
            f.WriteLine("]", declementBefore: true);
            f.WriteLine("}", declementBefore: true);

            return f.ToString();
        }

        public string GenerateInstanceDef(string heiarchieId, List<InstanceTypeDef> instances, Dictionary<string,TypeDTDLInfo> dtdlInterfaceIds)
        {
            var f = new IndentFormatter();

            f.WriteLine("{", incrementAfter: true);
            f.WriteLine("\"put\": [", incrementAfter: true);
            int iIndex = 0;
            var end = "";
            foreach (var instanceDef in instances)
            {
                f.WriteLine("{", incrementAfter: true);

                var modelId = instanceDef.dtdlInterface.Id.ToString();
                f.WriteLine($"\"typeId\": \"{dtdlInterfaceIds[modelId].TypeId}\",");
                var timeSeriesName = $"{instanceDef.dtdlInterface.Id.Labels[instanceDef.dtdlInterface.Id.Labels.Length - 1]}-{instanceDef.InstanceName}";
                var descrip = $"{instanceDef.InstanceName} of {instanceDef.dtdlInterface.Id.ToString()}";
                f.WriteLine("\"timeSeriesId\": [", incrementAfter: true);
                f.WriteLine($"\"{instanceDef.InstanceName}\"");
                f.WriteLine("],", declementBefore: true);
                f.WriteLine($"\"name\": \"{timeSeriesName}\",");
                f.WriteLine($"\"description\": \"{descrip}\",");
                f.WriteLine("\"hierarchyIds\": [", incrementAfter:true);
                f.WriteLine($"\"{heiarchieId}\"");
                f.WriteLine("],", declementBefore: true);
                f.WriteLine("\"instanceFields\": {", incrementAfter:true);
                var hls = instanceDef.Hiearchy.Split(":");
                for(var hli =0;hli< hls.Length;hli++)
                {
                    var hl = hls[hli];
                    var hvs = hl.Split("=");
                    end = "";
                    if (hli < hls.Length - 1)
                    {
                        end = ",";
                    }
                    f.WriteLine($"\"{hvs[0]}\": \"{hvs[1]}\"" + end);
                }
                f.WriteLine("}", declementBefore: true);
                iIndex++;
                if (iIndex < instances.Count)
                {
                    end = ",";
                }
                f.WriteLine("}" + end, declementBefore:true);
            }

            f.WriteLine("]", declementBefore: true);
            f.WriteLine("}", declementBefore: true);

            return f.ToString();
        }
    }

    public class InstanceTypeDef
    {
        public DTInterfaceInfo dtdlInterface { get; set; }
        public string InstanceName { get; set; }
        public string Hiearchy { get; set; }
    }

    public class TypeDTDLInfo
    {
        public string DTDLFileName { get; set; }
        public string TypeId { get; set; }
    }
}
