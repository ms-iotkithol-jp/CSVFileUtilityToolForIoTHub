using Microsoft.Azure.DigitalTwins.Parser;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WpfAppIoTCSVTranslator
{
    /// <summary>
    /// TSITypeGenerator.xaml の相互作用ロジック
    /// </summary>
    public partial class TSITypeGenerator : Window
    {
        public TSITypeGenerator()
        {
            InitializeComponent();
            this.Loaded += TSITypeGenerator_Loaded;
        }

        private void TSITypeGenerator_Loaded(object sender, RoutedEventArgs e)
        {
            lbHeirachieInstances.ItemsSource = generator.Hiearchies;
            lbDTDLFiles.ItemsSource = dtdlFiles;
            lbInstances.ItemsSource = instanceTypeDefs;
        }

        TSIGenerator generator = new TSIGenerator();

        private void buttonCreateHierachieId_Click(object sender, RoutedEventArgs e)
        {
            tbHierachieId.Text = Guid.NewGuid().ToString();
        }

        private void buttonAddHierachieInstance_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbHierachieInstanceName.Text))
            {
                MessageBox.Show("Please input name.");
                return;
            }
            generator.Hiearchies.Add(tbHierachieInstanceName.Text);
            SetHierachieInstanceName();
            tbHierachieInstanceName.Text = "";
        }

        void SetHierachieInstanceName()
        {
            tbInstanceHiearchies.Text = "";
            var candidateH = $"{generator.Hiearchies.First()}=name";
            for (int i = 1; i < generator.Hiearchies.Count; i++)
            {
                candidateH += $":{generator.Hiearchies[i]}=name";
            }
            tbInstanceHiearchies.Text = candidateH;
        }

        private void buttonRemoveHierachieInstance_Click(object sender, RoutedEventArgs e)
        {
            if (lbHeirachieInstances.SelectedItem == null)
            {
                MessageBox.Show("Please select hierarch instance.");
                return;
            }
            generator.Hiearchies.Remove(lbHeirachieInstances.SelectedItem as string);
            SetHierachieInstanceName();
        }

        private void buttonGenerateHeirachieDef_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbHierachieId.Text) || string.IsNullOrEmpty(tbHierachieName.Name))
            {
                MessageBox.Show("Please input id and model.");
                return;
            }
            tbHeirachesDef.Text = generator.GenerateHierarchies(tbHierachieId.Text, tbHierachieName.Text);
        }

        ObservableCollection<string> dtdlFiles = new ObservableCollection<string>();
        private async void buttonAddDTDLFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "DTDL Files|*.json";
            if (dialog.ShowDialog().Value)
            {
                if (!dtdlFiles.Contains(dialog.FileName))
                {
                    dtdlFiles.Add(dialog.FileName);
                    var modelJson = new List<string>();
                    using (var fs = File.OpenRead(dialog.FileName))
                    {
                        using (var reader = new StreamReader(fs))
                        {
                            modelJson.Add(reader.ReadToEnd());
                            try
                            {
                                var parser = new ModelParser();
                                var parseResult = await parser.ParseAsync(modelJson);
                                var dtdlInterfaces = parseResult.Where((d) => { return d.Value.EntityKind == DTEntityKind.Interface; });
                                foreach (var dif in dtdlInterfaces)
                                {
                                    dtdlInterfaceIds.Add(dif.Value.Id.ToString(), new TypeDTDLInfo()
                                    {
                                        DTDLFileName = new FileInfo(dialog.FileName).Name
                                    });
                                }
                            }
                            catch (ParsingException pex)
                            {
                                MessageBox.Show(pex.Message);
                                foreach (var err in pex.Errors)
                                {
                                    MessageBox.Show($"Error:{err.PrimaryID} - {err.Message}");
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.Message);
                            }
                        }
                    }
                }
            }        
        }

        Dictionary<string, TypeDTDLInfo> dtdlInterfaceIds = new Dictionary<string, TypeDTDLInfo>();
        private async void buttonGenerateTypeDef_Click(object sender, RoutedEventArgs e)
        {
            if (dtdlFiles.Count == 0)
            {
                MessageBox.Show("Please add DTDL files!");
                return;
            }
            var dtdls = new List<KeyValuePair<Dtmi, DTEntityInfo>>();
            var modelJson = new List<string>();
            foreach (var dtdlFile in dtdlFiles)
            {
                using (var fs = File.OpenRead(dtdlFile))
                {
                    using (var reader = new StreamReader(fs))
                    {
                        modelJson.Add(reader.ReadToEnd());
                        try
                        {
                            var parser = new ModelParser();
                            var parseResult = await parser.ParseAsync(modelJson);
                            var dtdlInterfaces = parseResult.Where((d) => { return d.Value.EntityKind == DTEntityKind.Interface; });
                            foreach (var dif in dtdlInterfaces)
                            {
                                dtdls.Add(dif);
                            }
                            tbTypeDef.Text = generator.GenerateTypes(dtdls, dtdlInterfaceIds);
                        }
                        catch (ParsingException pex)
                        {
                            MessageBox.Show(pex.Message);
                            foreach (var err in pex.Errors)
                            {
                                MessageBox.Show($"Error:{err.PrimaryID} - {err.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                }
            }
        }

        private async void buttonSaveHeirachies_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog();
            dialog.Filter = "TSI Hiearchies Files|hierarchies.json";
            if (dialog.ShowDialog().Value)
            {
                using (var fs = File.Open(dialog.FileName, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    await fs.WriteAsync(System.Text.Encoding.UTF8.GetBytes(tbHeirachesDef.Text));
                }
                tbHeirachesFileName.Text = dialog.FileName;
            }
        }

        private async void buttonSaveTypeDef_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog();
            dialog.Filter = "TSI Types Files|types.json";
            if (dialog.ShowDialog().Value)
            {
                using (var fs = File.Open(dialog.FileName, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    await fs.WriteAsync(System.Text.Encoding.UTF8.GetBytes(tbTypeDef.Text));
                }
                tbTypeDefFileName.Text = dialog.FileName;
            }
        }

        private async void buttonSaveInstanceFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog();
            dialog.Filter = "TSI Instances Files|instances.json";
            if (dialog.ShowDialog().Value)
            {
                using (var fs = File.Open(dialog.FileName, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    await fs.WriteAsync(System.Text.Encoding.UTF8.GetBytes(tbInstanceDef.Text));
                }
                tbInstanceFileName.Text = dialog.FileName;
            }

        }

        ObservableCollection<InstanceTypeDef> instanceTypeDefs = new ObservableCollection<InstanceTypeDef>();
        private async void buttonAddDeviceId_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbDeviceId.Text))
            {
                MessageBox.Show("Please input device id.");
                return;
            }
            if (lbDTDLFiles.SelectedItem == null)
            {
                MessageBox.Show("Please select DTDL file.");
                return;
            }
            if (generator.Hiearchies.Count == 0)
            {
                MessageBox.Show("Hiearchies definition doesn't exist.");
                return;
            }
            if (string.IsNullOrEmpty(tbInstanceHiearchies.Text) || (!string.IsNullOrEmpty(tbInstanceHiearchies.Text) && tbInstanceHiearchies.Text.Contains("=:")))
            {
                MessageBox.Show("Please input correct hiearchy statement");
                return;
            }
            var dtdlFileName = lbDTDLFiles.SelectedItem as string;
            var modelJson = new List<string>();
            using (var fs = File.OpenRead(dtdlFileName))
            {
                using (var reader = new StreamReader(fs))
                {
                    modelJson.Add(reader.ReadToEnd());
                }
            }
            try
            {
                var parser = new ModelParser();
                var parseResult = await parser.ParseAsync(modelJson);
                var dtdlInterfaces = parseResult.Where((d) => { return d.Value.EntityKind == DTEntityKind.Interface; });
                var dtdlIF = dtdlInterfaces.First();
                var instHiearch = tbInstanceHiearchies.Text;
                instHiearch = instHiearch.Substring(0, instHiearch.LastIndexOf("="));
                tbInstanceHiearchies.Text = $"{instHiearch}={tbDeviceId.Text}";
                instanceTypeDefs.Add(new InstanceTypeDef()
                {
                    dtdlInterface = (DTInterfaceInfo)dtdlIF.Value,
                    InstanceName = tbDeviceId.Text,
                    Hiearchy = tbInstanceHiearchies.Text
                });
            }
            catch (ParsingException pex)
            {
                MessageBox.Show(pex.Message);
                foreach (var err in pex.Errors)
                {
                    MessageBox.Show($"Error:{err.PrimaryID} - {err.Message}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void buttonGenerateInstanceDef_Click(object sender, RoutedEventArgs e)
        {
            tbInstanceDef.Text = generator.GenerateInstanceDef(tbHierachieId.Text, instanceTypeDefs.ToList(), dtdlInterfaceIds);
        }

        private void buttonOpenTypeFile_Click(object sender, RoutedEventArgs e)
        {
            if (lbDTDLFiles.SelectedItem == null)
            {
                MessageBox.Show("Select DTDL File.");
                return;
            }
            var dialog = new OpenFileDialog();
            dialog.Filter = "TSI Types Files|*.json";
            if (dialog.ShowDialog().Value)
            {
                tbTypeDefFileName.Text = dialog.FileName;
                using (var fs = File.OpenRead(dialog.FileName))
                {
                    using (var reader = new StreamReader(fs))
                    {
                        var content = reader.ReadToEnd();
                        dynamic typedef = Newtonsoft.Json.JsonConvert.DeserializeObject(content);
                        foreach(dynamic item in typedef.put)
                        {
                            string typeid = item.id;
                            string name = item.name;
                            if (dtdlInterfaceIds.ContainsKey(name))
                            {
                                dtdlInterfaceIds[name].TypeId = typeid;
                            }
                        }
                        tbTypeDef.Text = content;
                    }
                }
            }
        }

        private void buttonOpenHeiracheFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "TSI Hiearchies Files|*.json";
            if (dialog.ShowDialog().Value)
            {
                try
                {
                    tbHeirachesFileName.Text = dialog.FileName;
                    using (var fs = File.OpenRead(dialog.FileName))
                    {
                        using (var reader = new StreamReader(fs))
                        {
                            var def = reader.ReadToEnd();
                            dynamic content = Newtonsoft.Json.JsonConvert.DeserializeObject(def);
                            generator.Hiearchies.Clear();
                            foreach (var item in content.put)
                            {
                                tbHierachieId.Text = item.id;
                                tbHierachieName.Text = item.name;
                                dynamic source = item.source;
                                foreach (var instanceFieldName in source.instanceFieldNames)
                                {
                                    string name = instanceFieldName;
                                    generator.Hiearchies.Add(name);
                                }
                            }
                            SetHierachieInstanceName();
                            tbHeirachesDef.Text = def;
                        }
                    }
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

        }

        private void tbDeviceId_TextChanged(object sender, TextChangedEventArgs e)
        {
            var instH = tbInstanceHiearchies.Text;
            instH = instH.Substring(0, instH.LastIndexOf("=")+1) + tbDeviceId.Text;
            tbInstanceHiearchies.Text = instH;
        }
    }
}
