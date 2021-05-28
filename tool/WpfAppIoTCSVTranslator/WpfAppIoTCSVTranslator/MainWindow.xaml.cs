using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.DigitalTwins.Parser;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfAppIoTCSVTranslator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IIoTLogger
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false, reloadOnChange: false).Build();
            tbIoTHubCS.Text = config["iothub-connection-string"];
            lbColumnDefnition.ItemsSource = csvColumns;
        }

        string currentCSVFile = "";
        FileInfo[] selectedCSVFiles = null;
        
        ObservableCollection<CSVColumnDefinition> csvColumns = new ObservableCollection<CSVColumnDefinition>();
        private void buttonSelectCSVFile_Click(object sender, RoutedEventArgs e)
        {
            if (cbMultiCSVFilesSelection.IsChecked.Value)
            {
                var dialog = new CommonOpenFileDialog();
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    tbCSVFile.Text = dialog.FileName;
                    var dir = new DirectoryInfo(tbCSVFile.Text);
                    selectedCSVFiles = dir.GetFiles("*.csv").OrderBy(f => f.Name).ToArray();
                }
            }
            else
            {
                var dialog = new OpenFileDialog();
                dialog.Filter = "CSV Files|*.csv";
                if (dialog.ShowDialog().Value)
                {
                    tbCSVFile.Text = dialog.FileName;
                    currentCSVFile = tbCSVFile.Text;
                }
            }
            if (!string.IsNullOrEmpty( tbCSVFile.Text) )
            {
                bool loadAndPredictDefinition = true;
                if (hasLoadedDTDLFile)
                {
                    if (MessageBox.Show("DTDL File has been loaded. Do you clear current definitions?", "Alert", MessageBoxButton.YesNo) == MessageBoxResult.No)
                    {
                        loadAndPredictDefinition = false;
                    }
                } 
                if (loadAndPredictDefinition)
                {
//                    var fileName = tbCSVFile.Text;
                    if (!tbCSVFile.Text.EndsWith(".csv"))
                    {
                        if (selectedCSVFiles.Count() == 0)
                        {
                            MessageBox.Show("CSV file doesn't exist!");
                            return;
                        }
                        currentCSVFile = selectedCSVFiles[0].FullName;
                        
                    }
                    try
                    {
                        using (var fs = File.OpenRead(currentCSVFile))
                        {
                            csvColumns.Clear();
                            var reader = new StreamReader(fs);
                            var topLine = reader.ReadLine();
                            int index = 0;
                            foreach (var column in topLine.Split(","))
                            {
                                csvColumns.Add(new CSVColumnDefinition()
                                {
                                    Name = column,
                                    Order = index,
                                    Description = column
                                });
                                index++;
                            }
                            var lines = 1;
                            while (!reader.EndOfStream)
                            {
                                var line = reader.ReadLine();
                                lines++;
                            }
                        }
                        buttonParseCSVDefinition.IsEnabled = true;
                    }
                    catch(Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
            }
            
            buttonConnectToIoTHub.IsEnabled = true;
            hasLoadedCSVFile = true;
            hasLoadedDTDLFile = false;
        }

        public void ShowIoTLog(string msg)
        {
            this.Dispatcher.Invoke(() =>
            {
                var content = tbIoTHubLog.Text;
                var sb = new StringBuilder();
                using (var writer = new StringWriter(sb))
                {
                    writer.WriteLine($"{DateTime.Now.ToString("yyyyMMddHHmmss")} {msg}");
                    writer.Write(content);
                }
                string currentText = sb.ToString();

                if (cbLogAutoRemove.IsChecked.Value)
                {
                    if (currentText.Split(System.Environment.NewLine).Length > logLineMax)
                    {
                        currentText = currentText.Substring(0, currentText.LastIndexOf(System.Environment.NewLine));
                        currentText = currentText.Substring(0, currentText.LastIndexOf(System.Environment.NewLine));
                        currentText += System.Environment.NewLine;
                    }
                }
                tbIoTHubLog.Text = currentText;
            });
        }
        int logLineMax = 100;

        int currentCursor = 0;

        IoTDataSender iotDataSender = null;

        private async void buttonConnectToIoTHub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var modelId = "";
                if (cbUseModelId.IsChecked.Value)
                {
                    modelId = tbModelId.Text;
                }
                iotDataSender = new IoTDataSender(tbIoTHubCS.Text, this, modelId);
                await iotDataSender.Connect();
                var csBuilder = IotHubConnectionStringBuilder.Create(tbIoTHubCS.Text);
                tbDeviceIdPropertyValue.Text = csBuilder.DeviceId;
                buttonSendStart.IsEnabled = true;
                cbAddDeviceId.IsEnabled = true;
                tbDeviceIdPropertyName.IsEnabled = true;
                tbDeviceIdPropertyValue.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowIoTLog(ex.Message);
            }
        }

        int currentSendIntervalMSec = 0;
        string currentTimestampPropertyName = "";
        string startTimeForTimestamp = "";
        int deltaMSecForTimestamp = 0;
        bool singleLineSending = false;

        bool hasLoadedCSVFile = false;
        bool hasLoadedDTDLFile = false;

        int currentCSVFileIndex = 0;

        private void buttonSendStart_Click(object sender, RoutedEventArgs e)
        {
            var singleOrMultiline = ((ComboBoxItem)cbSigleOrMultiSending.SelectedItem).Content as string;
            if (singleOrMultiline.ToLower().StartsWith("single"))
            {
                singleLineSending = true;
            }
            else
            {
                singleLineSending = false;
                int tmp;
                if (!int.TryParse(tbSendDataSizeMax.Text, out tmp))
                {
                    MessageBox.Show("data size max should be integer!");
                    return;
                }
                if (tmp > 256000)
                {
                    MessageBox.Show("data size max should be less equal than 256000");
                    return;
                }
                sendDataSizeMax = tmp;
            }
            if (selectedCSVFiles == null)
            {
                selectedCSVFiles = new FileInfo[] { new FileInfo(currentCSVFile) };
            }
            currentCursor = 0;
          //  IncrementCurrentCursor();
            currentTimestampPropertyName = "";
            if (cbAddTimestamp.IsChecked.Value)
            {
                currentTimestampPropertyName = tbTimestampName.Text;
            }
            else
            {
                currentTimestampPropertyName = "";
            }
            if (cbUseMeasuredTime.IsChecked.Value)
            {
                startTimeForTimestamp = tbTimestampStartTime.Text;
                deltaMSecForTimestamp = int.Parse(tbDeltaMSec.Text);
            }
            else
            {
                startTimeForTimestamp = "";
                deltaMSecForTimestamp = 0;
            }
            bool gzipSend = false;
            if (cbZGIP.IsChecked.Value)
            {
                gzipSend = true;
            }
            var jsonSend = false;
    //        var topLine = currentCSVReader.ReadLine();
            var selectedSendFormat = ((ComboBoxItem)cbMessageFormat.SelectedItem).Content as string;
            if (selectedSendFormat=="As JSON")
            {
                jsonSend = true;
            }
            var deviceIdPropertyName = "";
            var deviceIdPropertyValue = "";
            if (cbAddDeviceId.IsChecked.Value)
            {
                deviceIdPropertyName = tbDeviceIdPropertyName.Text;
                deviceIdPropertyValue = tbDeviceIdPropertyValue.Text;
            }
            currentSendIntervalMSec = int.Parse(tbSendIntervalMSec.Text);
            iotDataSender.SendStatusChanged += (s,e) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    if (e.Completed)
                    {
                        tbSendEndTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        buttonSendStart.IsEnabled = true;
                        buttonSendStop.IsEnabled = false;
                    }
                });
            };
            sendTaskCancel = new CancellationTokenSource();
            currentSendingTask = iotDataSender.SendCSVDataAsync(sendTaskCancel.Token,
                selectedCSVFiles, currentCSVFileIndex, csvColumns, jsonSend,
                currentSendIntervalMSec, currentTimestampPropertyName, singleLineSending, startTimeForTimestamp, deltaMSecForTimestamp,
                deviceIdPropertyName,deviceIdPropertyValue,
                sendDataSizeMax, gzipSend);
            tbSendingStartTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            buttonSendStart.IsEnabled = false;
            buttonSendStop.IsEnabled = true;
        }

        CancellationTokenSource sendTaskCancel = null;
        Task currentSendingTask = null;
        int sendDataSizeMax = 4096;

        private void buttonSendStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                sendTaskCancel.Cancel();
                currentSendingTask.Wait();
            }
            catch (Exception ex)
            {
                ShowIoTLog(ex.Message);
            }
            buttonSendStop.IsEnabled = false;
            buttonSendStart.IsEnabled = true;
        }

        bool loadingDTDL = false;

        private void cbAddTimestamp_Checked(object sender, RoutedEventArgs e)
        {
            if (!loadingDTDL)
            {
                MessageBox.Show("This mode is that each timestamp will be set as the time of sending.");
            }
        }

        private void cbUseMeasuredTime_Checked(object sender, RoutedEventArgs e)
        {
            if (cbUseMeasuredTime.IsChecked.Value)
            {
                tbDeltaMSec.IsEnabled = true;
                tbTimestampStartTime.Text = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                tbTimestampStartTime.IsEnabled = true;
            }
            else
            {
                tbDeltaMSec.IsEnabled = false;
                tbTimestampStartTime.Text = "";
                tbTimestampStartTime.IsEnabled = false;
            }
        }

        private void cbAddDeviceId_Checked(object sender, RoutedEventArgs e)
        {
            if (!loadingDTDL)
            {
                tbDeviceIdPropertyValue.IsEnabled = true;
                tbDeviceIdPropertyName.IsEnabled = true;
                MessageBox.Show("Current property value is used as IoT Hub device connection string's.");
            }
        }

        bool isParsingCSVFile = false;
        private void buttonParseCSVDefinition_Click(object sender, RoutedEventArgs e)
        {
            isParsingCSVFile = true;
            try
            {
                int parseLine = 10;
                var columnStatus = new int[csvColumns.Count()];
                // status 0->unknown, 1->maybe, 2->confirmed
                using (var fs = File.OpenRead(currentCSVFile))
                {
                    using (var reader = new StreamReader(fs))
                    {
                        int index = 0;
                        var line = reader.ReadLine();
                        while (!reader.EndOfStream && index++ < parseLine)
                        {
                            line = reader.ReadLine();
                            var cols = line.Split(",");
                            for (var i = 0; i < csvColumns.Count(); i++)
                            {
                                if (columnStatus[i] < 2)
                                {
                                    DateTime dtv;
                                    double dv;
                                    int iv;
                                    if (DateTime.TryParse(cols[i], out dtv) && !double.TryParse(cols[i], out dv))
                                    {
                                        csvColumns[i].Schema = nameof(DateTime);
                                        csvColumns[i].SchemaName = nameof(DateTime);
                                        columnStatus[i] = 2;
                                    }
                                    else if (columnStatus[i] < 2 && double.TryParse(cols[i], out dv) && !int.TryParse(cols[i], out iv))
                                    {
                                        csvColumns[i].Schema = nameof(Double);
                                        csvColumns[i].SchemaName = nameof(Double);
                                        columnStatus[i] = 2;
                                    }

                                    if (columnStatus[i]==0 && int.TryParse(cols[i],out iv))
                                    {
                                        csvColumns[i].Schema = "Integer";
                                        csvColumns[i].SchemaName = "Integer";
                                        columnStatus[i] = 1;
                                    }
                                }
                            }
                            var unConfirmed = columnStatus.Where((c) => { return c < 2; });
                            if (unConfirmed.Count() == 0)
                            {
                                break;
                            }
                        }
                        for (var i = 0; i < columnStatus.Length; i++)
                        {
                            if (columnStatus[i] == 0)
                            {
                                csvColumns[i].Schema = nameof(String);
                                csvColumns[i].SchemaName = nameof(String);
                            }
                        }
                    }
                }
                buttonTranslateColumnName.IsEnabled = true;
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            isParsingCSVFile = false;
        }

        private void buttonTranslateColumnName_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                for (int i = 0; i < csvColumns.Count(); i++)
                {
                    if (string.IsNullOrEmpty(csvColumns[i].DisplayName))
                    {
                        var name = csvColumns[i].Name;
                        csvColumns[i].DisplayName = name;
                        if (name.IndexOf(" ") >= 0)
                        {
                            name = name.Trim();
                            name = name.Replace(" ", "_");
                        }
                        csvColumns[i].Name = name;
                    }
                }
                buttonGenerateDTDL.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void buttonGenerateDTDL_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentCSVFile))
            {
                MessageBox.Show("Please select CSV file!");
                return;
            }
            try
            {
                var generator = new DTDLGenerator()
                {
                    ModelId = tbModelId.Text,
                    ModelDisplayName = tbModelDisplayName.Text,
                    DeviceIdId = tbDeviceIdId.Text,
                    DeviceIdName = tbDeviceIdName.Text,
                    DeviceIdDisplayName = tbDeviceIdDisplayName.Text,
                    TimestampName = tbTDTDLTimestampName.Text,
                    CSVColums = csvColumns,
                    SourceCSVFileName = currentCSVFile
                };
                generator.GenerateDTDL();
                tbDTDL.Text = generator.GeneratedDataModel;
                buttonSaveDTDLFile.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void buttonSaveDTDLFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog();
            dialog.Filter = "DTDL Files|*.json";
            if (dialog.ShowDialog().Value)
            {
                using (var fs = File.Open(dialog.FileName, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    await fs.WriteAsync(System.Text.Encoding.UTF8.GetBytes(tbDTDL.Text));
                }
            }
        }
        private (
            DTTelemetryInfo deviceIdDT, string devieIdId, string deviceIdName, string deviceIdDisplayName, string deviceIdDescription,
            DTTelemetryInfo timestampDT, string timestampName, string timestampDisplayName, string timestampDescription,
            Dictionary<int,DTTelemetryInfo> colDTDLs) ResolveInterface(DTInterfaceInfo dtInterface)
        {
            string deviceIdId = "";
            string deviceIdName = "";
            string deviceIdDisplayName = "";
            string deviceIdDescription = "";
            string timestampName = "";
            string timestampDisplayName = "";
            string timestampDescription = "";
            DTTelemetryInfo deviceIdDT = null;
            DTTelemetryInfo timestampDT = null;
            Dictionary<int, DTTelemetryInfo> colDTDLs = new Dictionary<int, DTTelemetryInfo>();
            foreach(var content in dtInterface.Contents)
            {
                var dtdlKey = content.Key;
                var dtdlValue = content.Value;
                if (dtdlValue.EntityKind == DTEntityKind.Telemetry)
                {
                    var dtdlTelemetry = (DTTelemetryInfo)dtdlValue;
                    foreach (var descrip in dtdlValue.Description)
                    {
                        if (deviceIdDT==null && descrip.Value.EndsWith(DTDLGenerator.DTDLMark_DeviceId))
                        {
                            deviceIdDT = dtdlTelemetry;
                            deviceIdId = dtdlValue.Id.ToString();
                            deviceIdDisplayName = dtdlValue.DisplayName.First().Value;
                            deviceIdName = dtdlTelemetry.Name;
                            deviceIdDescription = dtdlTelemetry.Description.First().Value;
                        }
                        else if (timestampDT==null&& descrip.Value.EndsWith(DTDLGenerator.DTDLMark_Timestamp))
                        {
                            timestampDT = dtdlTelemetry;
                            timestampDisplayName = dtdlValue.DisplayName.First().Value;
                            timestampName = dtdlTelemetry.Name;
                            timestampDescription = dtdlTelemetry.Description.First().Value;
                        }
                        if (descrip.Value.StartsWith(DTDLGenerator.DTDLMark_ColmnOrder))
                        {
                            var orderPart = descrip.Value.Split(",")[0];
                            var order = int.Parse(orderPart.Split("=")[1]);
                            if (!colDTDLs.ContainsKey(order))
                            {
                                colDTDLs.Add(order, dtdlTelemetry);
                            }
                        }
                    }
                }
            }
            return (deviceIdDT, deviceIdId, deviceIdName, deviceIdDisplayName, deviceIdDescription,
                timestampDT, timestampName, timestampDisplayName, timestampDescription, colDTDLs);
        }
        private async void buttonSelectDTDLFile_Click(object sender, RoutedEventArgs e)
        {
            if (hasLoadedCSVFile)
            {
                if(MessageBox.Show("CSV File has been loaded. Do you clear current setting?","Alert", MessageBoxButton.OKCancel)== MessageBoxResult.Cancel)
                {
                    return;
                }
            }
            loadingDTDL = true;
            var dialog = new OpenFileDialog();
            dialog.Filter = "DTDL Files|*.json";
            if (dialog.ShowDialog().Value)
            {
                tbDTDLFileName.Text = dialog.FileName;
                try
                {
                    var modelJson = new List<string>();
                    using (var fs = File.OpenRead(tbDTDLFileName.Text))
                    {
                        using (var reader = new StreamReader(fs))
                        {
                            modelJson.Add(reader.ReadToEnd());
                        }
                    }
                    var parser = new ModelParser();
                    var parseResult = await parser.ParseAsync(modelJson);
                    string modelId = "";
                    string modelDescription = "";
                    string modelDisplayName = "";
                    var dtdlInterfaces = parseResult.Where((d) => { return d.Value.EntityKind == DTEntityKind.Interface; });
                    if (dtdlInterfaces.Count() > 0)
                    {
                        var contents = ResolveInterface(((DTInterfaceInfo)dtdlInterfaces.First().Value));
                    }
                    foreach (var dtdlDef in parseResult)
                    {
                        var dtdlKey= dtdlDef.Key;
                        var dtdlValue = dtdlDef.Value;
                        if (dtdlValue.EntityKind == DTEntityKind.Interface)
                        {
                            modelId = dtdlValue.Id.ToString();
                            modelDescription = dtdlValue.Description.First().Value;
                            modelDisplayName = dtdlValue.DisplayName.First().Value;
                            var contents = ResolveInterface((DTInterfaceInfo)dtdlValue);
                            tbDeviceIdId.Text = contents.devieIdId;
                            tbDeviceIdName.Text = contents.deviceIdName;
                            tbDeviceIdDisplayName.Text = contents.deviceIdDisplayName;
                            tbTDTDLTimestampName.Text = contents.timestampName;
                            tbModelId.Text = modelId;
                            tbModelDisplayName.Text = modelDisplayName;

                            bool hasDeviceIdinCol = false;
                            bool hasTimestampInCol = false;

                            csvColumns.Clear();
                            foreach(var propIndex in contents.colDTDLs.Keys)
                            {
                                var prop = contents.colDTDLs[propIndex];
                                var csvCol = new CSVColumnDefinition()
                                {
                                    IsDeviceId = false,
                                    IsTimestamp = false,
                                    Name = prop.Name,
                                    Order = propIndex
                                };
                                if (prop.DisplayName.Count > 0)
                                {
                                    csvCol.DisplayName = prop.DisplayName.First().Value;
                                }
                                if (prop.Description.Count > 0)
                                {
                                    csvCol.Description = prop.Description.First().Value;
                                }
                                if (prop == contents.deviceIdDT)
                                {
                                    csvCol.IsDeviceId = true;
                                    hasDeviceIdinCol = true;
                                }
                                if (prop == contents.timestampDT)
                                {
                                    csvCol.IsTimestamp = true;
                                    hasTimestampInCol = true;
                                }
                                csvColumns.Add(csvCol);
                                csvCol.Schema = prop.Schema.EntityKind.ToString();

                            }
                            if (contents.deviceIdDT != null && !hasDeviceIdinCol)
                            {
                                cbAddDeviceId.IsChecked = true;
                                tbDeviceIdPropertyName.Text = contents.deviceIdName;
                            }
                            if (contents.timestampDT != null && !hasTimestampInCol)
                            {
                                cbAddTimestamp.IsChecked = true;
                                tbTimestampName.Text = contents.timestampName;
                            }

                            break;
                        }
                        switch (dtdlValue.EntityKind)
                        {
                            case DTEntityKind.Interface:
                                break;
                            case DTEntityKind.Telemetry:
                                break;
                            case DTEntityKind.Property:
                                break;
                            case DTEntityKind.Relationship:
                                break;
                            case DTEntityKind.Array:
                                break;
                            case DTEntityKind.Boolean:
                                break;
                            case DTEntityKind.Date:
                                break;
                            case DTEntityKind.DateTime:
                                break;
                            case DTEntityKind.Double:
                                break;
                            case DTEntityKind.Duration:
                                break;
                            case DTEntityKind.Enum:
                                break;
                            case DTEntityKind.EnumValue:
                                break;
                            case DTEntityKind.Field:
                                break;
                            case DTEntityKind.Float:
                                break;
                            case DTEntityKind.Integer:
                                break;
                            case DTEntityKind.Long:
                                break;
                            case DTEntityKind.Map:
                                break;
                            case DTEntityKind.MapKey:
                                break;
                            case DTEntityKind.MapValue:
                                break;
                            case DTEntityKind.Object:
                                break;
                            case DTEntityKind.Reference:
                                break;
                            case DTEntityKind.String:
                                break;
                            case DTEntityKind.Time:
                                break;
                            case DTEntityKind.Unit:
                                break;
                            case DTEntityKind.UnitAttribute:
                                break;
                            case DTEntityKind.Command:
                                break;
                            case DTEntityKind.CommandPayload:
                                break;
                            case DTEntityKind.CommandType:
                                break;
                            case DTEntityKind.Component:
                                break;
                            default:
                                break;

                        }
                    }
                    hasLoadedCSVFile = false;
                    hasLoadedDTDLFile = true;
                }
                catch (ParsingException pex)
                {
                    MessageBox.Show(pex.Message);
                    foreach (var err in pex.Errors)
                    {
                        MessageBox.Show($"Error:{err.PrimaryID} - {err.Message}");
                    }
                }
                loadingDTDL = false;
            }
        }

        private void cbSigleOrMultiSending_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var singleOrMultiline = ((ComboBoxItem)cbSigleOrMultiSending.SelectedItem).Content as string;
            if (!string.IsNullOrEmpty(singleOrMultiline) && singleOrMultiline.ToLower().StartsWith("multi"))
            {
                tbSendDataSizeMax.IsEnabled = true;
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isParsingCSVFile)
            {
                var target = (CSVColumnDefinition)((ComboBox)sender).DataContext;
                target.SchemaName = target.Schema;
                if (target.SchemaName.IndexOf(" ") > 0)
                {
                    target.SchemaName = target.Schema.Substring(target.Schema.LastIndexOf(" ") + 1);
                }
            }
        }

        private void tbModelId_TextChanged(object sender, TextChangedEventArgs e)
        {
            var lastModelId = ((TextBox)sender).Text;
            if (tbDeviceIdId != null)
            {
                var deviceIdId = tbDeviceIdId.Text;
                foreach (var c in e.Changes)
                {
                    Debug.WriteLine($"AddedLength{c.AddedLength},RemoveLength{c.RemovedLength},Offset{c.Offset}");
                    var addedString = lastModelId.Substring(c.Offset, c.AddedLength);
                    var deviceIdIdS = deviceIdId.Substring(0, c.Offset);
                    var deviceIdIdE = deviceIdId.Substring(c.Offset+c.RemovedLength);
                    deviceIdId = deviceIdIdS + addedString + deviceIdIdE;
                    Debug.WriteLine($"New DeviceIdId:{deviceIdId}");
                }
                tbDeviceIdId.Text = deviceIdId;
            }
        }

        private void buttonGenerateTypeFileForTSI_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Under construction");
        }
    }

    public class StringIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return $"{value}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return int.Parse((string)value);
        }
    }
}