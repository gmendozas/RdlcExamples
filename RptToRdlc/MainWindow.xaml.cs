using Microsoft.Reporting.WinForms;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
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
using ZXing;

namespace RptToRdlc
{
    public delegate void RefToFunction();

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private bool _isReportViewerLoaded;
        private string ReportName { get; set; }
        private Dictionary<string, string> templates = new Dictionary<string, string>(4);

        public MainWindow()
        {
            InitializeComponent();

            _reportViewer.Load += ReportViewer_Load;
            FillComboBox();
            cmbTemplates.ItemsSource = templates.Keys;
        }

        private void ReportViewer_Load(object sender, EventArgs e)
        {
            if (!_isReportViewerLoaded)
            {
                if (!string.IsNullOrEmpty(ReportName))
                    GenerateReport();
            }
        }

        private void cmbTemplates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedReport = string.Empty;
            templates.TryGetValue(cmbTemplates.SelectedItem.ToString(), out selectedReport);
            ReportName = selectedReport;
        }

        private void GenerateReport()
        {
            List<string> confReport = ConfigReport();
            try
            {
                using (SqlConnection sqlConn = new SqlConnection(ConfigurationManager.ConnectionStrings["RptToRdlc.Properties.Settings.DemosBDConnectionString"].ConnectionString))
                using (SqlDataAdapter da = new SqlDataAdapter(confReport[0], sqlConn))
                {
                    DataSet ds = new DataSet();
                    da.Fill(ds);
                    DataTable dt = ds.Tables[0];

                    if(dt.Rows.Count == 0)
                    {
                        MessageBox.Show(this, "No information was found.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    this._reportViewer.Reset();
                    this._reportViewer.ProcessingMode = ProcessingMode.Local;
                   
                    ReportDataSource reportDataSource = new ReportDataSource();                    
                    reportDataSource.Name = confReport[1];
                    reportDataSource.Value = ds.Tables[0];

                    this._reportViewer.LocalReport.ReportPath = string.Format(@"rdls\{0}.rdlc", ReportName);
                    this._reportViewer.LocalReport.DataSources.Add(reportDataSource);
                    //this._reportViewer.b

                    ReportParameter[] parameters = new ReportParameter[2];
                    parameters[0] = new ReportParameter("ESIntegration", chbEsIntegration.IsChecked.Value ? Boolean.TrueString : Boolean.FalseString, false); 
                    if (DoesNeedBarCode())
                    {
                        GenerateBarCode(string.Format("{0}{1}"
                                , dt.AsEnumerable().Select(s => s.Field<string>("TransactionNo")).FirstOrDefault()
                                , dt.AsEnumerable().Select(s => s.Field<string>("LineNum")).FirstOrDefault())
                            , 100, 30);                        
                        parameters[1] = new ReportParameter("BarCodeImage", "file:\\" + System.IO.Directory.GetCurrentDirectory() + @"\imageBarCode.jpg", true);
                    } else
                        parameters[1] = new ReportParameter("BarCodeImage", string.Empty, true);

                    this._reportViewer.LocalReport.EnableExternalImages = true;
                    this._reportViewer.LocalReport.SetParameters(parameters);

                    _reportViewer.RefreshReport();
                    _isReportViewerLoaded = true;
                }
            } catch(Exception ex)
            {
                MessageBox.Show(this, string.Format("An error has occurred: {0}.", ex.Message), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            } finally
            {
                pgbWorking.Visibility = Visibility.Hidden;
                btnGenerate.IsEnabled = true;
            }
        }

        private void OnReponse()
        {
            pgbWorking.IsIndeterminate = false;
            pgbWorking.Value = 100;
        }

        private async void btnGenerate_ClickAsync(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ReportName))
            {
                MessageBox.Show(this, "Please, select a template before generate a report.", "Atention", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            else
            {
                pgbWorking.IsIndeterminate = true;
                pgbWorking.Visibility = Visibility.Visible;
                btnGenerate.IsEnabled = false;
                RefToFunction toFunction = new RefToFunction(OnReponse);

                await PerformLengthyTaskAsync(toFunction);
                GenerateReport();
            }
        }

        private void FillComboBox()
        {
            this.templates.Add("Send Med", "SendMedLabel");
            this.templates.Add("Sell to Pharmacy", "SellToPharmacyLabel");
            this.templates.Add("Fill a prescription", "FillAPrescriptionLabel");
            this.templates.Add("Non Standard Compounding", "NonStandardCompounding");
        }

        private async Task PerformLengthyTaskAsync(RefToFunction toFunction)
        {
            for (int i = 0; i < 2; i++) // 2 sec task
            {
                await Task.Delay(1000); // wait for 1 sec
            }
            toFunction.Invoke();
        }

        private void GenerateBarCode(string value, int width, int height)
        {
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.CODE_39,
                Options = new ZXing.Common.EncodingOptions
                {
                    Height = height,
                    Width = width,
                    Margin = 1,
                    PureBarcode = true
                }
            };
            var bitmap = writer.Write(value);
            var fileImage = System.IO.Directory.GetCurrentDirectory() + @"\imageBarCode.jpg";
            bitmap.Save(fileImage);
        }

        private bool DoesNeedBarCode()
        {
            var itDoes = false;
            if (string.Equals("FillAPrescriptionLabel", ReportName))
                itDoes = false;
            else if (string.Equals("NonStandardCompounding", ReportName))
                itDoes = false;
            else
                itDoes = true;

            return itDoes;
        }

        private List<string> ConfigReport()
        {
            List<string> configs = new List<string>();

            if (string.Equals("SendMedLabel", ReportName))
            {
                configs.Add("SELECT * FROM TblSentMedication");
                configs.Add("DsSentMedication");
            }
            else if (string.Equals("SellToPharmacyLabel", ReportName))
            {
                configs.Add("SELECT * FROM TblSelltoPharmacy");
                configs.Add("DsSellToPharmacy");
            }
            else if (string.Equals("FillAPrescriptionLabel", ReportName))
            {
                configs.Add("SELECT * FROM TblPrescription");
                configs.Add("DsPrescription");
            }
            else if (string.Equals("NonStandardCompounding", ReportName))
            {
                configs.Add("SELECT * FROM TblCompoundMeds");
                configs.Add("DsNonStandardCompounding");
            }

            return configs;
        }
    }
}
