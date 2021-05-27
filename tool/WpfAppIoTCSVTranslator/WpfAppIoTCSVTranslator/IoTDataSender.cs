using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WpfAppIoTCSVTranslator
{
    public class IoTDataSender
    {
        DeviceClient deviceClient = null;
        IIoTLogger logger;

        public IoTDataSender(string connectionString, IIoTLogger logger, string modelId)
        {
            var options = new ClientOptions();
            if (!string.IsNullOrEmpty(modelId))
            {
                options.ModelId = modelId;
            }
            deviceClient = DeviceClient.CreateFromConnectionString(connectionString, options);
            this.logger = logger;
        }

        public event DataSenderStatusChangedHandler SendStatusChanged;
        public async Task Connect()
        {
            try
            {
                await deviceClient.OpenAsync();
                logger.ShowIoTLog("IoT Hub Connected.");
            }
            catch (Exception ex)
            {
                logger.ShowIoTLog(ex.Message);
            }
        }

        static readonly string AppPropKey_Format = "format";
        static readonly string AppPropKey_GZIP = "gzip";

        string sendingMsgJsonArray = "";
        string sendingMsgCSVMultilines = "";
        bool gzipSend = false;

        public Task SendCSVDataAsync(CancellationToken ct, FileInfo[] csvFiles, int currentCSVFileIndex, Collection<CSVColumnDefinition> columnDefs, bool jsonSend, int sendIntervalMSec,
            string timestampPropertyName, bool singleLineSending, string startTimeForTimestamp,int deltaMSecForTimestamp,
            string deviceIdProertyName, string deviceIdPropertyValue,
            int sendDataSizeMax, bool gzipsend)
        {
            csvColumns = columnDefs;
            this.singleLineSending = singleLineSending;
            this.timestampPropertyName = timestampPropertyName;
            this.startTimeForTimestamp = startTimeForTimestamp;
            this.sendDataSizeMax = sendDataSizeMax;
            this.deltaMSecForTimestamp = deltaMSecForTimestamp;
            this.gzipSend = gzipsend;
            this.deviceIdPropertyName = deviceIdProertyName;
            this.deviceIdPropertyValue = deviceIdPropertyValue;

            if (!string.IsNullOrEmpty(startTimeForTimestamp))
            {
                dataTime = DateTime.Parse(startTimeForTimestamp);
            }

            var task = Task.Run(async () =>
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
                    for (int index = 0; index < csvFiles.Length; index++)
                    {
                        using (var fs = File.OpenRead(csvFiles[index].FullName))
                        {
                            int currentLine = 0;
                            using (var currentCSVReader = new StreamReader(fs))
                            {
                                if (!singleLineSending)
                                {
                                    if (jsonSend)
                                    {
                                        sendingMsgJsonArray = "[";
                                    }
                                    else
                                    {
                                        sendingMsgCSVMultilines = "";
                                    }
                                }
                                while (!currentCSVReader.EndOfStream)
                                {
                                    var line = currentCSVReader.ReadLine();

                                    jsonMsg = "";
                                    (Message sendMsg, int msgSize) msg;
                                    if (jsonSend)
                                    {
                                        msg = CreateJsonMessage(line).GetAwaiter().GetResult();
                                    }
                                    else
                                    {
                                        msg = CreateCSVMessage(line).GetAwaiter().GetResult();
                                    }
                                    if (msg.sendMsg != null)
                                    {
                                        await SendMsgToIoTHub(jsonSend, gzipsend, msg, currentLine);
                                        await Task.Delay(sendIntervalMSec);
                                    }
                                    if (ct.IsCancellationRequested)
                                    {
                                        break;
                                    }
                                    currentLine++;
                                }
                                if (!singleLineSending)
                                {
                                    (Message sendMsg, int msgSize) restMsg;
                                    if (jsonSend)
                                    {
                                        restMsg = await CreateJsonRest();
                                    }
                                    else
                                    {
                                        restMsg = await CreateCSVRest();
                                    }
                                    if (restMsg.sendMsg != null)
                                    {
                                        await SendMsgToIoTHub(jsonSend, gzipsend, restMsg, currentLine);
                                        await Task.Delay(sendIntervalMSec);
                                    }
                                }
                            }
                        }
                        logger.ShowIoTLog($"{csvFiles[index].Name} - All Line Sent.");
                        SendStatusChanged(this, new DataSenderStatusChanged($"{csvFiles[index].Name} - finished.", false));
                    }
                    SendStatusChanged(this, new DataSenderStatusChanged($"All line sent.", true));
                }
                catch (Exception ex)
                {
                    logger.ShowIoTLog(ex.Message);
                }
            });
            return task;
        }

        private async Task SendMsgToIoTHub(bool jsonSend, bool gzipsend, (Message sendMsg, int msgSize) msg, int currentLine)
        {
            msg.sendMsg.Properties.Add("application", "csv-translator");
            var format = "csv";
            if (jsonSend)
            {
                format = "json";
            }
            if (gzipsend)
            {
                format += "-gzip";
            }
            msg.sendMsg.Properties.Add(AppPropKey_Format, format);
            await deviceClient.SendEventAsync(msg.sendMsg);
            logger.ShowIoTLog($"Send[{currentLine}] - {msg.msgSize} bytes");
        }

        Collection<CSVColumnDefinition> csvColumns;
        bool singleLineSending = true;
        string sendCSVTopLine = "";

        private async Task<(Message sendMsg, int msgSize)> CreateCSVMessage(string line)
        {
            if (string.IsNullOrEmpty(sendCSVTopLine))
            {
                foreach (var col in csvColumns)
                {
                    if (!string.IsNullOrEmpty(sendCSVTopLine))
                    {
                        sendCSVTopLine += ",";
                    }
                    sendCSVTopLine += col.Name;
                }
                if (!string.IsNullOrEmpty(timestampPropertyName))
                {
                    sendCSVTopLine += $",{timestampPropertyName}";
                }
                if (!string.IsNullOrEmpty(deviceIdPropertyName))
                {
                    sendCSVTopLine += $",{deviceIdPropertyName}";
                }
            }
            Message sendMsg = null;
            int msgSize = 0;
            if (!string.IsNullOrEmpty(timestampPropertyName))
            {
                var row = line.Substring(0, line.LastIndexOf(System.Environment.NewLine));
                string timestamp = CreateTimstampProperty();
                line = $"{row},{timestamp}" + System.Environment.NewLine;
            }
            if (!string.IsNullOrEmpty(deviceIdPropertyValue))
            {
                line += $",{deviceIdPropertyValue}";
            }
            if (singleLineSending)
            {
                var sb = new StringBuilder();
                var writer = new StringWriter(sb);
                writer.WriteLine(sendCSVTopLine);
                writer.WriteLine(line);
                var msgBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
                msgSize = msgBytes.Length;
                if (gzipSend)
                {
                    msgBytes = await GzipCompress(msgBytes);
                    msgSize = msgBytes.Length;
                }
                sendMsg = new Message(msgBytes);
            }
            else
            {
                var csvLinesSB = new StringBuilder(sendingMsgCSVMultilines);
                var csvLinesWriter = new StringWriter(csvLinesSB);
                if (string.IsNullOrEmpty(sendingMsgCSVMultilines))
                {
                    csvLinesWriter.WriteLine(sendCSVTopLine);
                }
                csvLinesWriter.WriteLine(line);
                bool larger = true;
                var msgBytes = System.Text.Encoding.UTF8.GetBytes(csvLinesSB.ToString());
                if (msgBytes.Length > sendDataSizeMax)
                {
                    if (gzipSend)
                    {
                        var tmpBytes = await GzipCompress(msgBytes);
                        if (tmpBytes.Length < sendDataSizeMax)
                        {
                            larger = false;
                        }
                    }
                    if (larger)
                    {
                        msgBytes = System.Text.Encoding.UTF8.GetBytes(sendingMsgCSVMultilines);
                        msgSize = msgBytes.Length;
                        if (gzipSend)
                        {
                            msgBytes = await GzipCompress(msgBytes);
                            msgSize = msgBytes.Length;
                        }
                        sendMsg = new Message(msgBytes);
                        csvLinesSB = new StringBuilder();
                        csvLinesWriter = new StringWriter(csvLinesSB);
                        csvLinesWriter.WriteLine(sendCSVTopLine);
                        csvLinesWriter.WriteLine(line);
                    }
                }
                sendingMsgCSVMultilines = csvLinesSB.ToString();
            }
            return (sendMsg, msgSize);
        }
        private async Task<(Message sendMsg, int dataSize)> CreateCSVRest()
        {
            Message sendMsg = null;
            int dataSize = 0;
            if (sendingMsgCSVMultilines.Split(System.Environment.NewLine).Length > 1)
            {
                var msgBytes = System.Text.Encoding.UTF8.GetBytes(sendingMsgCSVMultilines);
                if (gzipSend)
                {
                    msgBytes = await GzipCompress(msgBytes);
                }
                dataSize = msgBytes.Length;
                sendMsg = new Message(msgBytes);
            }
            return (sendMsg, dataSize);
        }

            string deviceIdPropertyName = "";
        string deviceIdPropertyValue = "";
        string timestampPropertyName = "";
        string startTimeForTimestamp = "";
        DateTime dataTime;
        int deltaMSecForTimestamp = 1000;
        int sendDataSizeMax = 256000;
        string jsonMsg = "";

        private async Task<(Message sendMsg, int msgSize)> CreateJsonMessage(string line)
        {
            var dataItems = line.Split(",");
            jsonMsg = "{";
            Message sendMsg = null;
            int msgSize = 0;
            var columnDefs = csvColumns.OrderBy(c => c.Order);
            for (int i = 0; i < columnDefs.Count(); i++)
            {
                if (i > 0)
                {
                    jsonMsg += ",";
                }
                var colDef = columnDefs.ElementAt(i);
                jsonMsg += $"\"{colDef.Name}\":";
                int tryIntVal;
                double tryDoubleVal;
                if (int.TryParse(dataItems[i], out tryIntVal) || double.TryParse(dataItems[i], out tryDoubleVal))
                {
                    jsonMsg += $"{dataItems[i]}";
                }
                else
                {
                    var dataValue = dataItems[i];
                    if (colDef.Formatter != null)
                    {
                        dataValue = colDef.Formatter.Format(dataValue);
                    }
                    jsonMsg += $"\"{dataValue}\"";
                }
            }
            if (!string.IsNullOrEmpty(timestampPropertyName))
            {
                string timestamp = CreateTimstampProperty();
                jsonMsg += $",\"{timestampPropertyName}\":\"{timestamp}\"";
            }
            if (!string.IsNullOrEmpty(deviceIdPropertyName))
            {
                jsonMsg += $",\"{deviceIdPropertyName}\":\"{deviceIdPropertyValue}\"";
            }
            jsonMsg += "}";
            if (singleLineSending)
            {
                var msgBytes = System.Text.Encoding.UTF8.GetBytes(jsonMsg);
                msgSize = msgBytes.Length;
                if (gzipSend)
                {
                    msgBytes = await GzipCompress(msgBytes);
                    msgSize = msgBytes.Length;
                }
                sendMsg = new Message(msgBytes);
            }
            else
            {
                var currentJsonMsg = sendingMsgJsonArray;
                if (!currentJsonMsg.EndsWith("["))
                {
                    currentJsonMsg += ",";
                }
                currentJsonMsg += jsonMsg;
                var msgBytes = System.Text.Encoding.UTF8.GetBytes(currentJsonMsg+"]");
                int currentMsgSize = msgBytes.Length;
                if (currentMsgSize > sendDataSizeMax)
                {
                    bool larger = true;
                    msgBytes = System.Text.Encoding.UTF8.GetBytes(sendingMsgJsonArray + "]");
                    msgSize = msgBytes.Length;
                    if (gzipSend)
                    {
                        var tmpBytes = await GzipCompress(msgBytes);
                        if (tmpBytes.Length < sendDataSizeMax)
                        {
                            larger = false;
                        } else
                        {
                            msgBytes = tmpBytes;
                        }
                    }
                    if (larger)
                    {
                        msgSize = msgBytes.Length;
                        sendMsg = new Message(msgBytes);
                        sendingMsgJsonArray = $"[{jsonMsg}";
                    }
                }
                else
                {
                    sendingMsgJsonArray = currentJsonMsg;
                }
            }
            return (sendMsg,msgSize);
        }
        private async Task<(Message sendMsg, int dataSize)> CreateJsonRest()
        {
            Message sendMsg = null;
            int dataSize = 0;
            if (!sendingMsgJsonArray.EndsWith("["))
            {
                sendingMsgJsonArray += "]";
                var msgBytes = System.Text.Encoding.UTF8.GetBytes(sendingMsgJsonArray);
                if (gzipSend)
                {
                    msgBytes = await GzipCompress(msgBytes);
                }
                dataSize = msgBytes.Length;
                sendMsg = new Message(msgBytes);
            }
            return (sendMsg, dataSize);
        }

        private string CreateTimstampProperty()
        {
            string timestamp = "";
            if (string.IsNullOrEmpty(startTimeForTimestamp))
            {
                timestamp = DateTime.Now.ToString("yyyy/MM/ddTHH:mm:ss.fff");
            }
            else
            {
                timestamp = dataTime.ToString("yyyy/MM/ddTHH:mm:ss.fff");
                dataTime = dataTime.Add(TimeSpan.FromMilliseconds(deltaMSecForTimestamp));
            }

            return timestamp;
        }

        private static async Task<byte[]> GzipCompress(byte[] msgBytes)
        {
            using (var gzipOutStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(gzipOutStream, CompressionMode.Compress))
                {
                    await gzipStream.WriteAsync(msgBytes);
                    await gzipStream.FlushAsync();
                    msgBytes = gzipOutStream.ToArray();
                }
            }

            return msgBytes;
        }

        public async Task Stop()
        {

        }

    }

    public class DataSenderStatusChanged: EventArgs
    {
        public string Status { get; set; }
        public bool Completed { get; set; }
        public DataSenderStatusChanged(string status, bool isCompleted)
        {
            Status = status;
            Completed = isCompleted;
        }
    }
    public delegate void DataSenderStatusChangedHandler(object sender, DataSenderStatusChanged e);
}
