using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WIA;
using System.ComponentModel;
using System.IO;
using System.Diagnostics;
using DAP.Adorners;
using System.Windows.Controls.Primitives;
using System.Reflection;
using System.Net;
using System.Collections.Specialized;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Net.Sockets;

namespace ScannerClient_obalkyknih
{

    /// <summary>
    /// Interaction logic for TabsControl.xaml
    /// </summary>
    public partial class TabsControl : UserControl
    {
        #region attributes
        #region key binding commands
        RoutedCommand rotateLeftCommand = new RoutedCommand();
        RoutedCommand rotateRightCommand = new RoutedCommand();
        RoutedCommand rotate180Command = new RoutedCommand();
        RoutedCommand flipHorizontalCommand = new RoutedCommand();
        RoutedCommand cropCommand = new RoutedCommand();
        RoutedCommand deskewCommand = new RoutedCommand();
        #endregion

        // Only for debugging purposes
        //public static StringBuilder DEBUGLOG = new StringBuilder();

        // Barcode of the unit
        private string barcode;

        // Backup image for Ctrl+Z (Undo)
        private KeyValuePair<string, BitmapSource> backupImage = new KeyValuePair<string,BitmapSource>();

        private KeyValuePair<string, BitmapSource> redoImage = new KeyValuePair<string, BitmapSource>();

        private KeyValuePair<Guid, BitmapSource> workingImage = new KeyValuePair<Guid, BitmapSource>();

        // Used by sliders, because changing contrast or brightness is irreversible process
        private KeyValuePair<Guid, BitmapSource> sliderOriginalImage = new KeyValuePair<Guid, BitmapSource>();

        // GUID of Image that is currently selected
        private Guid selectedImageGuid;

        // GUID that corresponds to cover image
        private Guid coverGuid;

        // Dictionary containing GUID and file path of all loaded images 
        private Dictionary<Guid, string> imagesFilePaths = new Dictionary<Guid, string>();

        // Dictionary containing GUID and dimensions of originals of all loaded images
        private Dictionary<Guid, Size> imagesOriginalSizes = new Dictionary<Guid, Size>();

        // Dictionary containing Guid of TOC image and its thumbnail with wrapping Grid
        private Dictionary<Guid, Grid> tocThumbnailGridsDictionary = new Dictionary<Guid, Grid>();

        // Object responsible for cropping of images
        private CroppingAdorner cropper;

        // Chosen scanner device
        private Device activeScanner;

        // Background worker for downloading of metadata and cover and toc images
        private BackgroundWorker metadataReceiverBackgroundWorker = new BackgroundWorker();

        // Background worker for downloading of metadata and cover and toc images
        private BackgroundWorker uploaderBackgroundWorker = new BackgroundWorker();

        // Window that informs that upload is in progress
        private UploadWindow uploadWindow = null;

        // WebClient for downloading of pdf version of toc
        private WebClient tocPdfWebClient = new WebClient();

        // Object responsible for retrieval of metadata and finding links for toc and cover images
        private MetadataRetriever metadataRetriever = null;
        #endregion

        /// <summary>Constructor, creates new TabsControl based on given barcode</summary>
        /// <param name="barcode">barcode of the unit, that will be processed</param>
        public TabsControl(string barcode)
        {
            this.barcode = barcode;
            InitializeComponent();
            InitializeBackgroundWorkers();
            metadataReceiverBackgroundWorker.RunWorkerAsync(null);

            #region key binding commands initialization
            //rotateLeft
            CommandBinding cb = new CommandBinding(this.rotateLeftCommand, RotateLeftCommandExecuted, RotateLeftCommandCanExecute);
            this.CommandBindings.Add(cb);
            KeyGesture kg = new KeyGesture(Key.Left, ModifierKeys.Control);
            InputBinding ib = new InputBinding(this.rotateLeftCommand, kg);
            this.InputBindings.Add(ib);

            //rotateRight
            cb = new CommandBinding(this.rotateRightCommand, RotateRightCommandExecuted, RotateRightCommandCanExecute);
            this.CommandBindings.Add(cb);
            kg = new KeyGesture(Key.Right, ModifierKeys.Control);
            ib = new InputBinding(this.rotateRightCommand, kg);
            this.InputBindings.Add(ib);

            //rotate180
            cb = new CommandBinding(this.rotate180Command, Rotate180CommandExecuted, Rotate180CommandCanExecute);
            this.CommandBindings.Add(cb);
            kg = new KeyGesture(Key.R, ModifierKeys.Control);
            ib = new InputBinding(this.rotate180Command, kg);
            this.InputBindings.Add(ib);

            //flipHorizontal
            cb = new CommandBinding(this.flipHorizontalCommand, FlipHorizontalCommandExecuted, FlipHorizontalCommandCanExecute);
            this.CommandBindings.Add(cb);
            kg = new KeyGesture(Key.H, ModifierKeys.Control);
            ib = new InputBinding(this.flipHorizontalCommand, kg);
            this.InputBindings.Add(ib);
            
            //crop
            cb = new CommandBinding(this.cropCommand, CropCommandExecuted, CropCommandCanExecute);
            this.CommandBindings.Add(cb);
            kg = new KeyGesture(Key.C, ModifierKeys.Control);
            ib = new InputBinding(this.cropCommand, kg);
            this.InputBindings.Add(ib);

            //deskew
            cb = new CommandBinding(this.deskewCommand, DeskewCommandExecuted, DeskewCommandCanExecute);
            this.CommandBindings.Add(cb);
            kg = new KeyGesture(Key.D, ModifierKeys.Control);
            ib = new InputBinding(this.deskewCommand, kg);
            this.InputBindings.Add(ib);
            #endregion
        }

        #region key bindings

        //rotateLeft
        private void RotateLeftCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void RotateLeftCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            TabItem currentTab = tabControl.SelectedItem as TabItem;
            if (currentTab.Equals(this.scanningTabItem))
            {
                RotateLeft_Clicked(null, null);
            }
        }

        //rotateRight
        private void RotateRightCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void RotateRightCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            TabItem currentTab = tabControl.SelectedItem as TabItem;
            if (currentTab.Equals(this.scanningTabItem))
            {
                RotateRight_Clicked(null, null);
            }
        }

        //rotate180
        private void Rotate180CommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void Rotate180CommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            TabItem currentTab = tabControl.SelectedItem as TabItem;
            if (currentTab.Equals(this.scanningTabItem))
            {
                Rotate180_Clicked(null, null);
            }
        }

        //flipHorizontal
        private void FlipHorizontalCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void FlipHorizontalCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            TabItem currentTab = tabControl.SelectedItem as TabItem;
            if (currentTab.Equals(this.scanningTabItem))
            {
                Flip_Clicked(null, null);
            }
        }

        //crop
        private void CropCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void CropCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            TabItem currentTab = tabControl.SelectedItem as TabItem;
            if (currentTab.Equals(this.scanningTabItem))
            {
                Crop_Clicked(null, null);
            }
        }
        
        //deskew
        private void DeskewCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void DeskewCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            TabItem currentTab = tabControl.SelectedItem as TabItem;
            if (currentTab.Equals(this.scanningTabItem))
            {
                Deskew_Clicked(null, null);
            }
        }
        #endregion

        #region metadata tab controls

        // Shows all available metadata in new MetadataWindow
        private void showCompleteMetadataButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.metadataRetriever != null && this.metadataRetriever.Metadata != null)
            {
                Window metadataWindow = new MetadataWindow(this.metadataRetriever.Metadata);
                metadataWindow.Show();
            }
        }
        
        // Downloads metadata and cover and toc images
        private void DownloadMetadataButton_Click(object sender, RoutedEventArgs e)
        {
            this.downloadMetadataButton.IsEnabled = false;
            (Window.GetWindow(this) as MainWindow).AddMessageToStatusBar("Stahuji metadata.");
            if (this.metadataRetriever != null && this.metadataRetriever.Warnings != null
                && this.metadataRetriever.Warnings.Count > 0)
            {
                Metadata m = GetMetadataFromTextBoxes();
                this.metadataReceiverBackgroundWorker.RunWorkerAsync(m);
            }
            else
            {
                this.metadataReceiverBackgroundWorker.RunWorkerAsync(null);
            }
        }
        
        // On doubleclick, downloads pdf with toc and opens it in default viewer
        private void OriginalTocImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (!this.tocPdfWebClient.IsBusy && this.metadataRetriever.OriginalTocPdfLink != null)
                {
                    MainWindow mainWindow = Window.GetWindow(this) as MainWindow;
                    mainWindow.AddMessageToStatusBar("Stahuji pdf obsahu.");
                    using (tocPdfWebClient)
                    {
                        tocPdfWebClient.DownloadFileCompleted += new AsyncCompletedEventHandler(TocPdfDownloadCompleted);
                        tocPdfWebClient.DownloadFileAsync(new Uri(metadataRetriever.OriginalTocPdfLink), Settings.TemporaryFolder.TrimEnd('\\')
                            + "\\" + "orig_toc.pdf");
                    }
                }
            }
        }

        #region AsyncMethods

        // Sets background worker for receiving of metadata
        private void InitializeBackgroundWorkers()
        {
            uploaderBackgroundWorker.WorkerReportsProgress = false;
            uploaderBackgroundWorker.WorkerSupportsCancellation = true;
            uploaderBackgroundWorker.DoWork += new DoWorkEventHandler(UploaderBW_DoWork);
            uploaderBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(UploaderBW_RunWorkerCompleted);
            
            metadataReceiverBackgroundWorker.WorkerSupportsCancellation = true;
            metadataReceiverBackgroundWorker.WorkerReportsProgress = false;
            metadataReceiverBackgroundWorker.DoWork += new DoWorkEventHandler(MetadataReceiverBW_DoWork);
            metadataReceiverBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(MetadataReceiverBW_RunWorkerCompleted);
        }

        // Starts retrieving of metadata on background
        private void MetadataReceiverBW_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            Metadata metadata = e.Argument as Metadata;
            MetadataRetriever mr = null;
            if (metadata == null)
            {
                mr = new MetadataRetriever(this.barcode);
            }
            else
            {
                mr = new MetadataRetriever(this.barcode, metadata);
                mr.RetrieveOriginalCoverAndTocInformation();
            }
            e.Result = mr;
        }

        // Called after worker ended job, shows status with which worker ended
        private void MetadataReceiverBW_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            (Window.GetWindow(this) as MainWindow).RemoveMessageFromStatusBar("Stahuji metadata.");
            this.downloadMetadataButton.IsEnabled = true;
            if (e.Error != null)
            {
                MessageBox.Show(e.Error.Message, "Chyba při stahování metadat",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else if (!e.Cancelled)
            {
                this.metadataRetriever = e.Result as MetadataRetriever;
                if (this.metadataRetriever.Metadata != null)
                {
                    FillMetadata(this.metadataRetriever.Metadata);
                    this.showCompleteMetadataButton.IsEnabled = true;
                    if (this.metadataRetriever.Warnings.Count > 0)
                    {
                        string message = string.Join(Environment.NewLine, this.metadataRetriever.Warnings);
                        MessageBox.Show(message, "Duplicitní identifikátor", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                DownloadCoverAndToc();
            }
        }

        // Downloads cover and toc images
        private void DownloadCoverAndToc()
        {
            if (this.metadataRetriever.OriginalCoverImageLink != null)
            {
                (Window.GetWindow(this) as MainWindow).AddMessageToStatusBar("Stahuji obálku.");
                using (WebClient coverWc = new WebClient())
                {
                    coverWc.OpenReadCompleted += new OpenReadCompletedEventHandler(CoverDownloadCompleted);
                    coverWc.OpenReadAsync(new Uri(this.metadataRetriever.OriginalCoverImageLink));
                }
            }
            if (this.metadataRetriever.OriginalTocThumbnailLink != null)
            {
                (Window.GetWindow(this) as MainWindow).AddMessageToStatusBar("Stahuji obsah.");
                using (WebClient tocWc = new WebClient())
                {
                    tocWc.OpenReadCompleted += new OpenReadCompletedEventHandler(TocDownloadCompleted);
                    tocWc.OpenReadAsync(new Uri(this.metadataRetriever.OriginalTocThumbnailLink));
                }
            }
        }

        // Actions after cover image was downloaded - shows image
        void CoverDownloadCompleted(object sender, OpenReadCompletedEventArgs e)
        {
            (Window.GetWindow(this) as MainWindow).RemoveMessageFromStatusBar("Stahuji obálku.");
            if (e.Error == null && !e.Cancelled)
            {
                BitmapImage imgsrc = new BitmapImage();
                imgsrc.BeginInit();
                imgsrc.StreamSource = e.Result;
                imgsrc.EndInit();
                if (this.tabControl.SelectedItem == this.controlTabItem)
                {
                    this.controlCoverImage.Source = imgsrc;
                }
                else
                {
                    this.originalCoverImage.Source = imgsrc;
                }
            }
        }

        // Actions after toc image was downloaded - shows image
        void TocDownloadCompleted(object sender, OpenReadCompletedEventArgs e)
        {
            (Window.GetWindow(this) as MainWindow).RemoveMessageFromStatusBar("Stahuji obsah.");
            if (e.Error == null && !e.Cancelled)
            {
                BitmapImage imgsrc = new BitmapImage();
                imgsrc.BeginInit();
                imgsrc.StreamSource = e.Result;
                imgsrc.EndInit();
                if (this.tabControl.SelectedItem == this.controlTabItem)
                {
                    this.controlTocImage.Source = imgsrc;
                    this.controlTocImage.IsEnabled = true;
                }
                else
                {
                    this.originalTocImage.Source = imgsrc;
                    this.originalTocImage.IsEnabled = true; ;
                }
            }
        }

        // Actions after pdf file was downloaded - opens it in default viewer
        void TocPdfDownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            (Window.GetWindow(this) as MainWindow).RemoveMessageFromStatusBar("Stahuji pdf obsahu.");
            if (e.Error == null && !e.Cancelled)
            {
                System.Diagnostics.Process.Start(Settings.TemporaryFolder + @"orig_toc.pdf");
            }
        }
        #endregion

        #region Metadata validation

        // Sets metadata from metadata object to textBoxes and starts validation
        private void FillMetadata(Metadata metadata)
        {
            this.titleTextBox.Text = metadata.Title;
            this.authorTextBox.Text = metadata.Authors;
            this.yearTextBox.Text = metadata.Year.ToString();
            this.isbnTextBox.Text = metadata.ISBN;
            this.issnTextBox.Text = metadata.ISSN;
            this.cnbTextBox.Text = metadata.CNB;
            this.oclcTextBox.Text = metadata.OCLC;
            this.eanTextBox.Text = metadata.EAN;
            this.urnNbnTextBox.Text = metadata.URN;
            this.siglaTextBox.Text = metadata.Custom;

            ValidateIdentifiers(null, null);
        }

        // Validates identifiers, highlights errors
        private void ValidateIdentifiers(object sender, TextChangedEventArgs e)
        {
            // set title to scanning tab
            this.ThumbnailsTitleLabel.Content = this.titleTextBox.Text;

            string error;
            //ISBN
            if (!string.IsNullOrEmpty(this.isbnTextBox.Text))
            {

                error = ValidateIsbn(this.isbnTextBox.Text);
                if (error != null)
                {
                    this.isbnWarning.Visibility = Visibility.Visible;
                    this.isbnWarning.ToolTip = this.isbnTextBox.ToolTip = error;
                    this.isbnTextBox.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#A50100"));
                }
                else
                {
                    this.isbnWarning.Visibility = Visibility.Hidden;
                    this.isbnWarning.ToolTip = this.isbnTextBox.ToolTip = null;
                    this.isbnTextBox.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#111111"));
                }
            }
            // ISSN 7 numbers + checksum
            if (!string.IsNullOrEmpty(this.issnTextBox.Text))
            {

                error = ValidateIssn(this.issnTextBox.Text);
                if (error != null)
                {
                    this.issnWarning.Visibility = Visibility.Visible;
                    this.issnWarning.ToolTip = this.issnTextBox.ToolTip = error;
                    this.issnTextBox.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#A50100"));
                }
                else
                {
                    this.issnWarning.Visibility = Visibility.Hidden;
                    this.issnWarning.ToolTip = this.issnTextBox.ToolTip = null;
                    this.issnTextBox.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#111111"));
                }
            }
            // EAN - 12 numbers + checksum
            if (!string.IsNullOrEmpty(this.eanTextBox.Text))
            {

                error = ValidateEan(this.eanTextBox.Text);
                if (error != null)
                {
                    this.eanWarning.Visibility = Visibility.Visible;
                    this.eanWarning.ToolTip = this.eanTextBox.ToolTip = error;
                    this.eanTextBox.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#A50100"));
                }
                else
                {
                    this.eanWarning.Visibility = Visibility.Hidden;
                    this.eanWarning.ToolTip = this.eanTextBox.ToolTip = null;
                    this.eanTextBox.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#111111"));
                }
            }
            // CNB - cnb + 9 numbers
            if (!string.IsNullOrEmpty(this.cnbTextBox.Text))
            {

                error = ValidateCnb(this.cnbTextBox.Text);
                if (error != null)
                {
                    this.cnbWarning.Visibility = Visibility.Visible;
                    this.cnbWarning.ToolTip = this.cnbTextBox.ToolTip = error;
                    this.cnbTextBox.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#A50100"));
                }
                else
                {
                    this.cnbWarning.Visibility = Visibility.Hidden;
                    this.cnbWarning.ToolTip = this.cnbTextBox.ToolTip = null;
                    this.cnbTextBox.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#111111"));
                }
            }
            //OCLC - variable-length numeric string
            if (!string.IsNullOrEmpty(this.oclcTextBox.Text))
            {

                error = ValidateOclc(this.oclcTextBox.Text);
                if (error != null)
                {
                    this.oclcWarning.Visibility = Visibility.Visible;
                    this.oclcWarning.ToolTip = this.oclcTextBox.ToolTip = error;
                    this.oclcTextBox.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#A50100"));
                }
                else
                {
                    this.oclcWarning.Visibility = Visibility.Hidden;
                    this.oclcWarning.ToolTip = this.oclcTextBox.ToolTip = null;
                    this.oclcTextBox.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#111111"));
                }
            }
        }

        // Validates given isbn, returns error message if invalid or null if valid
        private string ValidateIsbn(string isbn)
        {
            string errorText = null;
            isbn = isbn.Replace("-", "");
            char[] isbnArray = isbn.ToCharArray();
            switch (isbn.Length)
            {
                case 10:
                    int sumIsbn = 0;
                    for (int i = 0; i < 9; i++)
                    {
                        if (char.IsDigit(isbnArray[i]))
                        {
                            int multiplier = 10 - i;
                            sumIsbn += multiplier * ((int)char.GetNumericValue(isbnArray[i]));
                        }
                        else
                        {
                            errorText = "ISBN obsahuje nečíselný znak";
                            break;
                        }
                    }
                    int checksumIsbn = (char.ToLower(isbnArray[9]) == 'x') ? 10 : (int)char.GetNumericValue(isbnArray[9]);
                    sumIsbn += checksumIsbn;
                    if ((sumIsbn % 11) != 0)
                    {
                        errorText = "Nesedí kontrolní znak";
                    }
                    break;
                case 13:
                    sumIsbn = 0;
                    for (int i = 0; i < 13; i += 2)
                    {
                        if (char.IsDigit(isbnArray[i]))
                        {
                            sumIsbn += (int)char.GetNumericValue(isbnArray[i]);
                        }
                        else
                        {
                            errorText = "ISBN obsahuje nečíselný znak";
                            break;
                        }
                    }
                    for (int i = 1; i < 12; i += 2)
                    {
                        if (char.IsDigit(isbnArray[i]))
                        {
                            sumIsbn += (int)char.GetNumericValue(isbnArray[i]) * 3;
                        }
                        else
                        {
                            errorText = "ISBN obsahuje nečíselný znak";
                            break;
                        }
                    }
                    if ((sumIsbn % 10) != 0)
                    {
                        errorText = "Nesedí kontrolní znak";
                    }
                    break;
                default:
                    errorText = "ISBN musí obsahovat 10 nebo 13 čísel";
                    break;
            }
            return errorText;
        }

        // Validates given issn, returns error message if invalid or null if valid
        private string ValidateIssn(string issn)
        {
            string errorText = null;

            issn = issn.Replace("-", "").Trim();
            char[] issnArray = issn.ToCharArray();
            if (issn.Length == 8)
            {
                int sumIssn = 0;
                for (int i = 0; i < issnArray.Length - 1; i++)
                {
                    if (char.IsDigit(issnArray[i]))
                    {
                        int multiplier = 8 - i;
                        sumIssn += (int)char.GetNumericValue(issnArray[i]) * multiplier;
                    }
                    else
                    {
                        errorText = "ISSN obsahuje nečíselný znak";
                        break;
                    }
                }
                int checksumIssn = (char.ToLower(issnArray[7]) == 'x') ? 10 : (int)char.GetNumericValue(issnArray[7]);
                sumIssn += checksumIssn;
                if ((sumIssn % 11) != 0)
                {
                    errorText = "Nesedí kontrolní znak";
                }
            }
            else
            {
                errorText = "ISSN musí obsahovat 8 čísel";
            }

            return errorText;
        }

        // Validates given oclc, returns error message or null
        private string ValidateOclc(string oclc)
        {
            if (!oclc.StartsWith("(OCoLC)"))
            {
                return "OCLC nezačíná znaky (OCoLC)";
            }

            oclc = oclc.Substring(7);
            long tmp;
            if (!long.TryParse(oclc, out tmp))
            {
                return "OCLC obsahuje za znaky (OCoLC) další nečíselné znaky";
            }

            return null;
        }

        // Validates given cnb, returns error message or null
        private string ValidateCnb(string cnb)
        {
            string errorText = null;

            if (cnb.StartsWith("cnb"))
            {
                string cnbTmp = cnb.Substring(3);
                int tmp;
                if (int.TryParse(cnbTmp, out tmp))
                {
                    if (cnbTmp.Length != 9)
                    {
                        errorText = "ČNB musí obsahovat za znaky cnb přesně 9 číslic";
                    }
                }
                else
                {
                    errorText = "ČNB obsahuje za znaky cnb nečíselné znaky";
                }
            }
            else
            {
                errorText = "ČNB nezačíná znaky cnb";
            }

            return errorText;
        }

        // Validates given ean, returns error message or null
        private string ValidateEan(string ean)
        {
            string errorText = null;

            ean = ean.Replace("-", "").Trim();
            char[] eanArray = ean.ToCharArray();
            if (ean.Length == 13)
            {
                long tmp;
                if (!long.TryParse(ean, out tmp))
                {
                    errorText = "EAN obsahuje nečíselný znak";
                }
                else
                {
                    int sumEan = 0;
                    for (int i = 0; i < 13; i += 2)
                    {
                        sumEan += (int)char.GetNumericValue(eanArray[i]);
                    }
                    for (int i = 1; i < 12; i += 2)
                    {
                        sumEan += (int)char.GetNumericValue(eanArray[i]) * 3;
                    }
                    if ((sumEan % 10) != 0)
                    {
                        errorText = "Nesedí kontrolní znak";
                    }
                }
            }
            else
            {
                errorText = "EAN musí mít 13 číslic.";
            }

            return errorText;
        }
        #endregion
        #endregion

        #region sending to ObalkyKnih

        // Checks everything and calls uploadWorker to upload to obalkyknih
        private void SendToObalkyKnih()
        {
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            if (string.IsNullOrWhiteSpace(Settings.UserName))
            {
                MessageBox.Show("Nastavte přihlašovací údaje.", "Žádné přihlašovací údaje",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            //validate
            string error = null;
            string isbn = this.isbnTextBox.Text;
            string issn = this.issnTextBox.Text;
            string oclc = this.oclcTextBox.Text;
            string ean = this.eanTextBox.Text;
            string cnb = this.cnbTextBox.Text;
            string urn = this.urnNbnTextBox.Text;
            string custom = this.siglaTextBox.Text;

            NameValueCollection nvc = new NameValueCollection();
            nvc.Add("login", Settings.UserName);
            nvc.Add("password", Settings.Password);
            if (!string.IsNullOrEmpty(isbn))
            {
                nvc.Add("isbn", isbn);
                error = ValidateIsbn(isbn);
            }
            if (!string.IsNullOrEmpty(issn))
            {
                nvc.Add("issn", issn);
                error = ValidateIssn(issn);
            }
            if (!string.IsNullOrEmpty(oclc))
            {
                nvc.Add("oclc", oclc);
                error = ValidateOclc(oclc);
            }
            if (!string.IsNullOrEmpty(ean))
            {
                nvc.Add("ean", ean);
                error = ValidateEan(ean);
            }
            if (!string.IsNullOrEmpty(cnb))
            {
                nvc.Add("nbn", cnb);
                error = ValidateCnb(cnb);
            }
            if (!string.IsNullOrEmpty(urn))
            {
                if (nvc.Get("nbn") == null)
                {
                    nvc.Add("nbn", urn);
                }
            }
            if (!string.IsNullOrEmpty(custom))
            {
                if (nvc.Get("nbn") == null)
                {
                    nvc.Add("nbn", Settings.Sigla + "-" + custom);
                }
            }

            if (error != null)
            {
                MessageBox.Show("Některý z identifikátorů obsahuje chybu." + Environment.NewLine + error,
                "Chybný identifikátor", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrEmpty(isbn) && string.IsNullOrEmpty(issn) && string.IsNullOrEmpty(cnb)
                && string.IsNullOrEmpty(oclc) && string.IsNullOrEmpty(ean) && string.IsNullOrEmpty(urn)
                && string.IsNullOrEmpty(custom))
            {
                MessageBox.Show("Vyplňte alespoň jeden identifikátor." + Environment.NewLine + error,
                "Žádný identifikátor", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(this.titleTextBox.Text))
            {
                MessageBox.Show("Název musí být vyplněn." + Environment.NewLine + error,
               "Žádný název", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(this.authorTextBox.Text)
                || string.IsNullOrWhiteSpace(this.yearTextBox.Text))
            {
                var result = MessageBox.Show("Chybí autor nebo rok vydání. Opravdu chcete odeslat obálku bez toho?"
                    + Environment.NewLine + error,
                "Chybí základní informace.", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            nvc.Add("title", this.titleTextBox.Text);
            nvc.Add("author", this.authorTextBox.Text ?? "");
            nvc.Add("year", this.yearTextBox.Text ?? "");
            nvc.Add("ocr", (this.ocrCheckBox.IsChecked == true) ? "yes" : "no");

            string metaXml = null;
            string coverFileName = (this.coverGuid == Guid.Empty) ? null : this.imagesFilePaths[this.coverGuid];
            List<string> tocFileNames = new List<string>();

            //cover
            if (this.coverGuid == Guid.Empty)
            {
                var result = MessageBox.Show("Opravdu chcete odeslat data bez obálky?",
                "Chybí obálka.", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            //toc
            if (!this.tocImagesList.HasItems)
            {
                var result = MessageBox.Show("Opravdu chcete odeslat data bez obsahu?",
                "Chybí obsah", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }
            else
            {
                foreach (var grid in tocImagesList.Items)
                {
                    Guid guid = Guid.Empty;
                    foreach (var record in this.tocThumbnailGridsDictionary)
                    {
                        if (record.Value.Equals(grid))
                        {
                            tocFileNames.Add(this.imagesFilePaths[record.Key]);
                        }
                    }
                }
            }

            //metastream
            try
            {
                XElement userElement = new XElement("user", Settings.UserName);
                XElement siglaElement = new XElement("sigla", Settings.Sigla);

                XElement clientElement = new XElement("client");
                XElement clientNameElement = new XElement("name", "ObalkyKnih-scanner");
                XElement clientVersionElement = new XElement("version", Assembly.GetEntryAssembly().GetName().Version);
                IPHostEntry host;
                string localIPv4 = "?";
                string localIPv6 = "?";
                host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        localIPv4 = ip.ToString();
                    }
                    if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        localIPv6 = ip.ToString();
                    }
                }
                XElement clientIpv4Address = new XElement("local-IPv4-address", localIPv4);
                XElement clientIpv6Address = new XElement("local-IPv6-address", localIPv6);
                clientElement.Add(clientNameElement);
                clientElement.Add(clientVersionElement);
                clientElement.Add(clientIpv4Address);
                clientElement.Add(clientIpv6Address);

                XElement rootElement = new XElement("meta");
                rootElement.Add(siglaElement);
                rootElement.Add(userElement);
                rootElement.Add(clientElement);
                if (this.coverGuid != Guid.Empty)
                {
                    XElement coverElement = new XElement("cover");
                    rootElement.Add(coverElement);
                }
                if (this.tocImagesList.Items.Count > 0)
                {
                    XElement tocElement = new XElement("toc");
                    tocElement.Add(new XElement("pages", this.tocImagesList.Items.Count));
                    rootElement.Add(tocElement);
                }
                XDocument xmlDoc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"), rootElement);
                metaXml = xmlDoc.ToString();
            }
            catch (Exception)
            {
                MessageBox.Show("Nastala chyba při tvorbě metasouboru, oznamte to prosím autorovi programu.",
                        "Chybný metasoubor", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            UploadParameters param = new UploadParameters();
            param.Url = Settings.ImportLink;
            param.TocFilePaths = tocFileNames;
            param.CoverFilePath = coverFileName;
            param.MetaXml = metaXml;
            param.Nvc = nvc;

            // Save working image in memory to file
            if (workingImage.Key != Guid.Empty)
            {
                this.sendButton.IsEnabled = false;
                try
                {
                    ImageTools.SaveToFile(this.workingImage.Value, this.imagesFilePaths[this.workingImage.Key]);
                }
                catch (Exception)
                {
                    MessageBox.Show("Nastal problém při ukládání obrázku do souboru.", "Chyba!", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    this.sendButton.IsEnabled = true;
                    return;
                }
                this.sendButton.IsEnabled = true;
            }

            //DEBUGLOG.AppendLine("SendToObalkyKnih part1: Total time: " + sw.ElapsedMilliseconds);
            this.uploaderBackgroundWorker.RunWorkerAsync(param);

            this.uploadWindow = new UploadWindow();
            this.uploadWindow.ShowDialog();
        }

        // Method for uploading multipart/form-data
        // url where will be data posted, login, password
        private void UploadFilesToRemoteUrl(string url, string coverFileName, List<string> tocFileNames,
            string metaXml, NameValueCollection nvc, DoWorkEventArgs e)
        {
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            // Check version
            UpdateChecker updateChecker = new UpdateChecker();
            updateChecker.RetrieveUpdateInfo();
            if (!updateChecker.IsSupportedVersion)
            {
                throw new WebException("Používáte nepodporovanou verzi programu. Aktualizujte ho.",
                    WebExceptionStatus.ProtocolError);
            }

            HttpWebRequest requestToServer = (HttpWebRequest)WebRequest.Create(url);
            requestToServer.Timeout = 600000;

            // Define a boundary string
            string boundaryString = "----ObalkyKnih" + DateTime.Now.Ticks.ToString("x");

            // Turn off the buffering of data to be written, to prevent OutOfMemoryException when sending data
            requestToServer.AllowWriteStreamBuffering = false;
            // Specify that request is a HTTP post
            requestToServer.Method = WebRequestMethods.Http.Post;
            // Specify that the content type is a multipart request
            requestToServer.ContentType = "multipart/form-data; boundary=" + boundaryString;
            // Turn off keep alive
            requestToServer.KeepAlive = false;




            UTF8Encoding utf8 = new UTF8Encoding();
            string boundaryStringLine = "\r\n--" + boundaryString + "\r\n";
            
            string lastBoundaryStringLine = "\r\n--" + boundaryString + "--\r\n";
            byte[] lastBoundaryStringLineBytes = utf8.GetBytes(lastBoundaryStringLine);


            // TEXT PARAMETERS
            string formDataString = "";
            foreach (string key in nvc.Keys)
            {
                formDataString += boundaryStringLine 
                    + String.Format(
                "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}",
                key,
                nvc[key]);
            }
            byte[] formDataBytes = utf8.GetBytes(formDataString);


            // COVER PARAMETER
            long coverSize = 0;
            string coverDescriptionString = boundaryStringLine
                + String.Format(
                "Content-Disposition: form-data; name=\"{0}\"; "
                 + "filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n",
                "cover", "cover.tif", "image/tif");
            byte[] coverDescriptionBytes = utf8.GetBytes(coverDescriptionString);
            
            if (coverFileName != null)
            {
                FileInfo fileInfo = new FileInfo(coverFileName);
                coverSize = fileInfo.Length + coverDescriptionBytes.Length;
            }
            

            // TOC PARAMETERS
            int counter = 1;
            Dictionary<string, byte[]> tocDescriptionsDictionary = new Dictionary<string, byte[]>();
            long tocSize = 0;
            foreach (var fileName in tocFileNames)
            {
                string tocDescription = boundaryStringLine
                    + String.Format(
                "Content-Disposition: form-data; name=\"{0}\"; "
                 + "filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n",
                "toc_page_" + counter, "toc_page_" + counter + ".tif", "image/tif");
                byte[] tocDescriptionBytes = utf8.GetBytes(tocDescription);

                FileInfo fi = new FileInfo(fileName);
                tocSize += fi.Length + tocDescriptionBytes.Length;

                tocDescriptionsDictionary.Add(fileName, tocDescriptionBytes);
                counter++;
            }

            // META PARAMETER
            string metaDataString = boundaryStringLine
                + String.Format(
                "Content-Disposition: form-data; name=\"{0}\"; "
                + "filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n",
                "meta", "meta.xml", "text/xml")
                + metaXml;
            byte[] metaDataBytes = utf8.GetBytes(metaDataString);

            // Calculate the total size of the HTTP request
            long totalRequestBodySize = 
                + lastBoundaryStringLineBytes.Length
                + formDataBytes.Length
                + coverSize
                + tocSize
                +metaDataBytes.Length;

            // And indicate the value as the HTTP request content length
            requestToServer.ContentLength = totalRequestBodySize;


            // Write the http request body directly to the server
            using (Stream s = requestToServer.GetRequestStream())
            {
                // Send text parameters
                s.Write(formDataBytes, 0,
                    formDataBytes.Length);

                // Send cover
                if (coverFileName != null)
                {
                    s.Write(coverDescriptionBytes, 0,
                        coverDescriptionBytes.Length);

                    byte[] buffer = File.ReadAllBytes(coverFileName);
                    s.Write(buffer, 0, buffer.Length);
                }

                // Send toc
                foreach (var tocRecord in tocDescriptionsDictionary)
                {
                    GC.Collect();
                    byte[] buffer = File.ReadAllBytes(tocRecord.Key);
                    s.Write(tocRecord.Value, 0, tocRecord.Value.Length);
                    s.Write(buffer, 0, buffer.Length);
                }

                // Send meta
                s.Write(metaDataBytes, 0, metaDataBytes.Length);

                // Send the last part of the HTTP request body
                s.Write(lastBoundaryStringLineBytes, 0, lastBoundaryStringLineBytes.Length);
            }



            //DEBUGLOG.AppendLine("UploadFilesToRemoteUrl (upload data): Total time: " + sw.ElapsedMilliseconds);

            // Grab the response from the server. WebException will be thrown
            // when a HTTP OK status is not returned
            WebResponse response = requestToServer.GetResponse();
            StreamReader responseReader = new StreamReader(response.GetResponseStream());
            e.Result = responseReader.ReadToEnd();
            //DEBUGLOG.AppendLine("UploadFilesToRemoteUrl: Total time: " + sw.ElapsedMilliseconds);
        }

        // Uploads files to obalkyknih in new thread
        private void UploaderBW_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            UploadParameters up = e.Argument as UploadParameters;
            UploadFilesToRemoteUrl(up.Url, up.CoverFilePath, up.TocFilePaths, up.MetaXml, up.Nvc, e);
        }

        // Shows result of uploading process (OK or error message)
        private void UploaderBW_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //using (StreamWriter sw = new StreamWriter(Settings.TemporaryFolder + "DEBUG.LOG",true))
            //{
                //DEBUGLOG.AppendLine("-----------------------------------------------------------------");
                //sw.Write(DEBUGLOG.ToString());
                //DEBUGLOG.Clear();
            //}

            this.uploadWindow.isClosable = true;
            this.uploadWindow.Close();
            if (e.Error != null)
            {
                if (e.Error is WebException)
                {
                    string message = "";
                    if ((e.Error as WebException).Response != null)
                    {
                        HttpWebResponse response = (e.Error as WebException).Response as HttpWebResponse;
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            message = "Chyba autorizace: Přihlašovací údaje nejsou správné.";
                        }
                        else if (response.StatusCode == HttpStatusCode.InternalServerError)
                        {
                            message = "Chyba na straně serveru: " + response.StatusDescription;
                        }
                        else
                        {
                            message = response.StatusCode + ": " + response.StatusDescription;
                        }
                    }
                    else
                    {
                        message = (e.Error as WebException).Status + ": " + e.Error.Message;
                    }
                    MessageBox.Show(message, "Odesílání neúspěšné", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show("Počas odesílání nastala neznámá výjimka, je možné, že data nebyli odeslány.",
                        "Chyba odesílání", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (!e.Cancelled)
            {
                string response = (e.Result as string) ?? "";
                if ("OK".Equals(response))
                {
                    FillControlMetadata();
                    this.controlTabItem.IsEnabled = true;
                    this.tabControl.SelectedItem = this.controlTabItem;
                    Metadata m = GetMetadataFromTextBoxes();
                    metadataReceiverBackgroundWorker.RunWorkerAsync(m);

                    MessageBox.Show("Odesílání úspěšné.",
                        "Odesláno", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Server nepotvrdil zpracování dat. Je možné, že data nebyly zpracovány správně."
                        + response, "Zpracování nepotvrzené", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        // Extracts metadata from textboxes and from metadataRetriever
        private Metadata GetMetadataFromTextBoxes()
        {
            Metadata metadata = new Metadata();
            if (!string.IsNullOrWhiteSpace(this.titleTextBox.Text))
            {
                metadata.Title = this.titleTextBox.Text;
            }
            if (!string.IsNullOrWhiteSpace(this.authorTextBox.Text))
            {
                metadata.Authors = this.authorTextBox.Text;
            }
            if (!string.IsNullOrWhiteSpace(this.yearTextBox.Text))
            {
                metadata.Year = this.yearTextBox.Text;
            }
            if (!string.IsNullOrWhiteSpace(this.isbnTextBox.Text))
            {
                metadata.ISBN = this.isbnTextBox.Text;
            }
            if (!string.IsNullOrWhiteSpace(this.issnTextBox.Text))
            {
                metadata.ISSN = this.issnTextBox.Text;
            }
            if (!string.IsNullOrWhiteSpace(this.cnbTextBox.Text))
            {
                metadata.CNB = this.cnbTextBox.Text;
            }
            if (!string.IsNullOrWhiteSpace(this.oclcTextBox.Text))
            {
                metadata.OCLC = this.oclcTextBox.Text;
            }
            if (!string.IsNullOrWhiteSpace(this.eanTextBox.Text))
            {
                metadata.EAN = this.eanTextBox.Text;
            }
            if (!string.IsNullOrWhiteSpace(this.urnNbnTextBox.Text))
            {
                metadata.URN= this.urnNbnTextBox.Text;
            }
            if (!string.IsNullOrWhiteSpace(this.siglaTextBox.Text))
            {
                metadata.Custom = this.siglaTextBox.Text;
            }

            if (metadataRetriever != null && metadataRetriever.Metadata != null)
            {
                metadata.FixedFields = this.metadataRetriever.Metadata.FixedFields;
                metadata.VariableFields = this.metadataRetriever.Metadata.VariableFields;
                metadata.Sysno = this.metadataRetriever.Metadata.Sysno;
            }

            return metadata;
        }
        #endregion

        #region scanning functionality

        // Scans image
        private void ScanImage(DocumentType documentType)
        {
            //Stopwatch totalTime = new Stopwatch();
            //Stopwatch partialTime = new Stopwatch();
            //totalTime.Start();
            //partialTime.Start();
            ICommonDialog dialog = new CommonDialog();

            //try to set active scanner
            if (!setActiveScanner())
            {
                return;
            }

            int dpi = (documentType == DocumentType.Cover) ? Settings.CoverDPI : Settings.TocDPI;

            Item item = activeScanner.Items[1];

            //Setting configuration of scanner (dpi, color)
            Object value;
            foreach (IProperty property in item.Properties)
            {
                switch (property.PropertyID)
                {
                    case 6146: //4 is Black-white,gray is 2, color 1
                        value = (documentType == DocumentType.Cover) ? Settings.CoverScanType : Settings.TocScanType;
                        property.set_Value(ref value);
                        break;
                    case 6147: //dots per inch/horizontal
                        value = dpi;
                        property.set_Value(ref value);
                        break;
                    case 6148: //dots per inch/vertical
                        value = dpi;
                        property.set_Value(ref value);
                        break;
                }
            }

            ImageFile image = null;
            try
            {
                image = (ImageFile)dialog.ShowTransfer(item, FormatID.wiaFormatBMP, true);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                MessageBox.Show("Skenování nebylo úspěšné.", "Chyba!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            //long scanTime = partialTime.ElapsedMilliseconds;
            //partialTime.Restart();

            BitmapSource originalSizeImage = null;
            BitmapSource smallerSizeImage = null;
            using (MemoryStream ms = new MemoryStream((byte[])image.FileData.get_BinaryData()))
            {
                originalSizeImage = ImageTools.LoadFullSize(ms);
                originalSizeImage = ImageTools.ApplyAutoColorCorrections(originalSizeImage);
                smallerSizeImage = ImageTools.LoadGivenSizeFromBitmapSource(originalSizeImage, 800);
            }

            //long conversionTime = partialTime.ElapsedMilliseconds;
            //partialTime.Restart();

            // create unique identifier for image
            Guid guid = Guid.NewGuid();
            while (this.imagesFilePaths.ContainsKey(guid))
            {
                guid = Guid.NewGuid();
            }

            string newFileName = Settings.TemporaryFolder +
                ((documentType == DocumentType.Cover) ? "obalkyknih-cover_" : "obalkyknih-toc_")
                + barcode + "_" + guid + ".tif";

            Size originalSize = new Size(originalSizeImage.PixelWidth, originalSizeImage.PixelHeight);

            this.imagesFilePaths.Add(guid, newFileName);
            this.imagesOriginalSizes.Add(guid, originalSize);

            if (documentType == DocumentType.Cover)
            {
                AddCoverImage(smallerSizeImage, guid);
            }
            else
            {
                AddTocImage(smallerSizeImage, guid);
            }

            //set workingImage and save previous to file
            if (this.workingImage.Key != Guid.Empty && this.imagesFilePaths.ContainsKey(this.workingImage.Key))
            {
                try
                {
                    ImageTools.SaveToFile(this.workingImage.Value, this.imagesFilePaths[this.workingImage.Key]);
                }
                catch (Exception)
                {
                    MessageBox.Show("Nastal problém při ukládání obrázku do souboru.", "Chyba!", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
            }
            this.workingImage = new KeyValuePair<Guid, BitmapSource>(guid, originalSizeImage);

            image = null;
            GC.Collect();
            //DEBUGLOG.AppendLine("ScanImage: Total time: " + totalTime.ElapsedMilliseconds + "; scanning time: " + scanTime + "; conversion time:" + conversionTime + "; rest: " + partialTime.ElapsedMilliseconds);
        }

        // Sets active scanner device automatically, show selection dialog, if more scanners, if no scanner device found, shows error window and returns false 
        private bool setActiveScanner()
        {
            ICommonDialog dialog = new CommonDialog();
            if (activeScanner != null)
            {
                return true;
            }
            List<DeviceInfo> foundDevices = GetDevices();
            if (foundDevices.Count == 1 && foundDevices[0].Type == WiaDeviceType.ScannerDeviceType)
            {
                activeScanner = foundDevices[0].Connect();
                return true;
            }
            try
            {
                activeScanner = dialog.ShowSelectDevice(WiaDeviceType.ScannerDeviceType, true, true);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                //Show error and return
                MessageBox.Show("Nenalezen skener.", "Chyba!", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        // Gets the list of available WIA devices.
        private static List<DeviceInfo> GetDevices()
        {
            List<DeviceInfo> devices = new List<DeviceInfo>();
            WIA.DeviceManager manager = new WIA.DeviceManager();

            foreach (WIA.DeviceInfo info in manager.DeviceInfos)
            {
                devices.Add(info);
            }

            return devices;
        }
        #endregion

        #region scanning tab controls

        #region Scanning controllers
        
        // Scan cover image
        private void ScanCoverButton_Click(object sender, RoutedEventArgs e)
        {
            ScanButtonClicked(DocumentType.Cover);
        }

        // Scan toc image
        private void ScanTocButton_Click(object sender, RoutedEventArgs e)
        {
            ScanButtonClicked(DocumentType.Toc);
        }

        // Unified scan function
        private void ScanButtonClicked(DocumentType documentType)
        {
            DisableImageControllers();

            // backup old cover
            if (documentType == DocumentType.Cover && this.coverGuid != Guid.Empty)
            {
                string filePath = this.imagesFilePaths[this.coverGuid];
                if (this.workingImage.Key == this.coverGuid)
                {
                    backupImage = new KeyValuePair<string,BitmapSource>(filePath,
                        this.workingImage.Value);
                }
                else
                {
                    backupImage = new KeyValuePair<string, BitmapSource>(filePath,
                    ImageTools.LoadFullSize(filePath));
                }
                SignalLoadedBackup();
            }

            ScanImage(documentType);

            EnableImageControllers();
        }
        #endregion

        #region Load Image controllers

        // Shows ExernalImageLoadWindow
        private void LoadFromFile_Clicked(object sender, MouseButtonEventArgs e)
        {
            ExternalImageLoadWindow window = new ExternalImageLoadWindow();
            window.Image_Clicked += new MouseButtonEventHandler(LoadButtonClicked);
            window.ShowDialog();
        }

        // Shows dialog for loading image
        private void LoadButtonClicked(object sender, MouseButtonEventArgs e)
        {
            DocumentType documentType;
            if ((sender as Image).Name.Equals("coverImage"))
            {
                documentType = DocumentType.Cover;
            }
            else
            {
                documentType = DocumentType.Toc;
            }

            // backup old cover
            if (documentType == DocumentType.Cover && this.coverGuid != Guid.Empty)
            {
                string filePath = this.imagesFilePaths[this.coverGuid];
                if (this.workingImage.Key == this.coverGuid)
                {
                    backupImage = new KeyValuePair<string, BitmapSource>(filePath,
                        this.workingImage.Value);
                }
                else
                {
                    backupImage = new KeyValuePair<string, BitmapSource>(filePath,
                    ImageTools.LoadFullSize(filePath));
                }
                SignalLoadedBackup();
            }

            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Title = (documentType == DocumentType.Cover) ? "Načíst obálku" : "Načíst obsah";
            dlg.Filter = "image files (bmp;png;jpeg;wmp;gif;tiff)|*.png;*.bmp;*.jpeg;*.jpg;*.wmp;*.gif;*.tiff;*.tif";
            dlg.FilterIndex = 2;
            bool? result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                string fileName = dlg.FileName;
                Guid guid = Guid.NewGuid();
                while (this.imagesFilePaths.ContainsKey(guid))
                {
                    guid = Guid.NewGuid();
                }

                DisableImageControllers();
                LoadExternalImage(documentType, fileName, guid);
                EnableImageControllers();
                this.contrastSlider.IsEnabled = true;
            }
        }

        // Loads image from external file
        private void LoadExternalImage(DocumentType documentType, string fileName, Guid guid)
        {
            //Stopwatch totalSW = new Stopwatch();
            //Stopwatch partialSW = new Stopwatch();
            //long preparationTime;
            //long loadingTime;
            //long colorCorrectionTime;
            //long imageSaveTime;
            //long thumbCreateTime;
            //long saveWorkingImageTime = 0;
            //totalSW.Start();
            //partialSW.Start();

            string newFileName = Settings.TemporaryFolder +
                ((documentType == DocumentType.Cover) ? "obalkyknih-cover_" : "obalkyknih-toc_")
                + barcode + "_" + guid + ".tif";
            BitmapSource originalSizeImage = null;
            BitmapSource smallerSizeImage = null;
            Size originalSize;
            try
            {
                //partialSW.Stop();
                //preparationTime = partialSW.ElapsedMilliseconds;
                //partialSW.Restart();
                originalSizeImage = ImageTools.LoadFullSize(fileName);

                //partialSW.Stop();
                //loadingTime = partialSW.ElapsedMilliseconds;
                //partialSW.Restart();

                originalSizeImage = ImageTools.ApplyAutoColorCorrections(originalSizeImage);

                //partialSW.Stop();
                //colorCorrectionTime = partialSW.ElapsedMilliseconds;
                //partialSW.Restart();

                originalSize = new Size(originalSizeImage.PixelWidth, originalSizeImage.PixelHeight);

                ImageTools.SaveToFile(originalSizeImage, newFileName);

                //partialSW.Stop();
                //imageSaveTime = partialSW.ElapsedMilliseconds;
                //partialSW.Restart();

                smallerSizeImage = ImageTools.LoadGivenSizeFromBitmapSource((BitmapSource)originalSizeImage, 800);

                //partialSW.Stop();
                //thumbCreateTime = partialSW.ElapsedMilliseconds;
                //partialSW.Restart();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nastala chyba během načítání souboru. Důvod: " + ex.Message, "Chyba načítání obrázku",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.imagesFilePaths.Add(guid, newFileName);
            this.imagesOriginalSizes.Add(guid, originalSize);

            if (documentType == DocumentType.Cover)
            {
                AddCoverImage(smallerSizeImage, guid);
            }
            else
            {
                AddTocImage(smallerSizeImage, guid);
            }

            if (this.workingImage.Key != Guid.Empty && this.imagesFilePaths.ContainsKey(this.workingImage.Key))
            {
                try
                {
                    ImageTools.SaveToFile(this.workingImage.Value, this.imagesFilePaths[this.workingImage.Key]);
                    //partialSW.Stop();
                    //saveWorkingImageTime = partialSW.ElapsedMilliseconds;
                    //partialSW.Restart();
                }
                catch (Exception)
                {
                    MessageBox.Show("Nastal problém při ukládání obrázku do souboru.", "Chyba!", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
            }
            this.workingImage = new KeyValuePair<Guid, BitmapSource>(guid, originalSizeImage);

            //partialSW.Stop();
            //long remainingTime = partialSW.ElapsedMilliseconds;
            //partialSW.Restart();
            //totalSW.Stop();
            //DEBUGLOG.AppendLine("LoadExternalImage: Total time: " + totalSW.ElapsedMilliseconds + "; Preparation: " + preparationTime
            //    + "; Load: " + loadingTime + "; Color correction: " + colorCorrectionTime
            //    + "; Save: " + imageSaveTime + "; Thumbnail: " + thumbCreateTime + "; Remaining: " + remainingTime
            //    + "; SaveWI: " + saveWorkingImageTime);

            GC.Collect();
        }
        #endregion

        #region Main transformation controllers (rotation, deskew, crop, flip)

        // Rotates selected image by 90 degrees left
        private void RotateLeft_Clicked(object sender, MouseButtonEventArgs e)
        {
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            TransformImage(ImageTransforms.RotateLeft);
            //DEBUGLOG.AppendLine("Rotate: Total time: " + sw.ElapsedMilliseconds);
        }

        // Rotates selected image by 90 degrees right
        private void RotateRight_Clicked(object sender, MouseButtonEventArgs e)
        {
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            TransformImage(ImageTransforms.RotateRight);
            //DEBUGLOG.AppendLine("Rotate: Total time: " + sw.ElapsedMilliseconds);
        }

        // Rotates selected image by 180 degrees
        private void Rotate180_Clicked(object sender, MouseButtonEventArgs e)
        {
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            TransformImage(ImageTransforms.Rotate180);
            //DEBUGLOG.AppendLine("Rotate: Total time: " + sw.ElapsedMilliseconds);
        }

        // Flips selected image horizontally
        private void Flip_Clicked(object sender, MouseButtonEventArgs e)
        {
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            TransformImage(ImageTransforms.FlipHorizontal);
            //DEBUGLOG.AppendLine("Flip: Total time: " + sw.ElapsedMilliseconds);
        }

        // Crops selected image
        private void Crop_Clicked(object sender, MouseButtonEventArgs e)
        {
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            TransformImage(ImageTransforms.Crop);
            //DEBUGLOG.AppendLine("Crop: Total time: " + sw.ElapsedMilliseconds);
        }

        // Deskews selected image
        private void Deskew_Clicked(object sender, MouseButtonEventArgs e)
        {
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            TransformImage(ImageTransforms.Deskew);
            //DEBUGLOG.AppendLine("Deskew: Total time: " + sw.ElapsedMilliseconds);
        }

        // Applies contrast and brightness changes to original image
        private void SliderConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            TransformImage(ImageTransforms.CorrectColors);
            //DEBUGLOG.AppendLine("Color correction: Total time: " + sw.ElapsedMilliseconds);
        }

        // Applies given transformation to selected image
        private void TransformImage(ImageTransforms transformation)
        {
            Guid guid = this.selectedImageGuid;
            if (guid == Guid.Empty)
            {
                return;
            }

            DisableImageControllers();

            string filePath = this.imagesFilePaths[guid];

            // if working image is not selected image, save old working image and load new
            if (guid != this.workingImage.Key)
            {
                if (this.workingImage.Key != Guid.Empty && this.imagesFilePaths.ContainsKey(this.workingImage.Key))
                {
                    try
                    {
                        ImageTools.SaveToFile(this.workingImage.Value, this.imagesFilePaths[this.workingImage.Key]);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Nastal problém při ukládání obrázku do souboru.", "Chyba!", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        EnableImageControllers();
                        return;
                    }
                }
                // freeze previous workingImage because it somehow decreases memory footprint
                if (this.workingImage.Value != null && this.workingImage.Value.CanFreeze)
                {
                    this.workingImage.Value.Freeze();
                }
                this.workingImage = new KeyValuePair<Guid, BitmapSource>(guid, ImageTools.LoadFullSize(filePath));
            }

            // freeze previous workingImage because it somehow decreases memory footprint
            if (this.workingImage.Value != null && this.workingImage.Value.CanFreeze)
            {
                this.workingImage.Value.Freeze();
            }
            // freeze previous backupImage because it somehow decreases memory footprint
            if (this.backupImage.Value != null && this.backupImage.Value.CanFreeze)
            {
                this.backupImage.Value.Freeze();
            }
            // backup old image
            backupImage = new KeyValuePair<string, BitmapSource>(filePath, this.workingImage.Value);
            SignalLoadedBackup();

            // do transformation to working image
            switch (transformation)
            {
                case ImageTransforms.RotateLeft:
                    this.workingImage = new KeyValuePair<Guid, BitmapSource>(guid,
                        ImageTools.RotateImage(this.workingImage.Value, -90));
                    break;
                case ImageTransforms.RotateRight:
                    this.workingImage = new KeyValuePair<Guid, BitmapSource>(guid,
                        ImageTools.RotateImage(this.workingImage.Value, 90));
                    break;
                case ImageTransforms.Rotate180:
                    this.workingImage = new KeyValuePair<Guid, BitmapSource>(guid,
                        ImageTools.RotateImage(this.workingImage.Value, 180));
                    break;
                case ImageTransforms.FlipHorizontal:
                    this.workingImage = new KeyValuePair<Guid, BitmapSource>(guid,
                        ImageTools.FlipHorizontalImage(this.workingImage.Value));
                    break;
                case ImageTransforms.Deskew:
                    double skewAngle = ImageTools.GetDeskewAngle(this.selectedImage.Source as BitmapSource);
                    this.workingImage = new KeyValuePair<Guid,BitmapSource>(guid,
                        ImageTools.DeskewImage(this.workingImage.Value, skewAngle));
                    break;
                case ImageTransforms.Crop:
                    this.workingImage = new KeyValuePair<Guid, BitmapSource>(guid,
                        ImageTools.CropImage(this.workingImage.Value, this.cropper));
                    break;
                case ImageTransforms.CorrectColors:
                    BitmapSource tmp = ImageTools.AdjustContrast(this.workingImage.Value, (int)this.contrastSlider.Value);
                    tmp = ImageTools.AdjustGamma(tmp, (int)this.gammaSlider.Value);
                    tmp = ImageTools.AdjustBrightness(tmp, (int)this.gammaSlider.Value);
                    this.workingImage = new KeyValuePair<Guid, BitmapSource>(guid, tmp);
                    break;
            }

            // renew selected image
            this.selectedImage.Source = ImageTools.LoadGivenSizeFromBitmapSource(this.workingImage.Value, 800);
            this.sliderOriginalImage = new KeyValuePair<Guid, BitmapSource>(Guid.Empty, null);

            // renew thumbnail image
            if (this.selectedImageGuid == this.coverGuid)
            {
                this.coverThumbnail.Source = this.selectedImage.Source;
            }
            else
            {
                (LogicalTreeHelper.FindLogicalNode(this.tocThumbnailGridsDictionary[this.selectedImageGuid],
                    "tocThumbnail") as Image).Source = this.selectedImage.Source;
            }

            // set new width and height
            this.imagesOriginalSizes[this.selectedImageGuid] = new Size(this.workingImage.Value.PixelWidth,
                this.workingImage.Value.PixelHeight);

            EnableImageControllers();

            // reset cropZone
            SetAppropriateCrop(Size.Empty, this.selectedImage.RenderSize, true);
            GC.Collect();
        }

        // Changes brightness of cover - only preview
        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.selectedImageGuid == Guid.Empty)
            {
                return;
            }
            if (this.sliderOriginalImage.Key != this.selectedImageGuid)
            {
                this.sliderOriginalImage = new KeyValuePair<Guid, BitmapSource>(
                    this.selectedImageGuid, this.selectedImage.Source as BitmapSource);
            }
            BitmapSource tmp = ImageTools.AdjustContrast(this.sliderOriginalImage.Value, (int)this.contrastSlider.Value);
            tmp = ImageTools.AdjustGamma(tmp, this.gammaSlider.Value);
            this.selectedImage.Source = ImageTools.AdjustBrightness(tmp, (int)e.NewValue);
        }

        // Changes contrast of cover - only preview
        private void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.selectedImageGuid == Guid.Empty)
            {
                return;
            }
            if (this.sliderOriginalImage.Key != this.selectedImageGuid)
            {
                this.sliderOriginalImage = new KeyValuePair<Guid, BitmapSource>(
                    this.selectedImageGuid, this.selectedImage.Source as BitmapSource);
            }
            BitmapSource tmp = ImageTools.AdjustContrast(this.sliderOriginalImage.Value, (int)e.NewValue);
            tmp = ImageTools.AdjustGamma(tmp, this.gammaSlider.Value);
            this.selectedImage.Source = ImageTools.AdjustBrightness(tmp, (int)this.brightnessSlider.Value);
        }

        // Changes contrast of cover - only preview
        private void GammaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.selectedImageGuid == Guid.Empty)
            {
                return;
            }
            if (this.sliderOriginalImage.Key != this.selectedImageGuid)
            {
                this.sliderOriginalImage = new KeyValuePair<Guid, BitmapSource>(
                    this.selectedImageGuid, this.selectedImage.Source as BitmapSource);
            }

            BitmapSource tmp = ImageTools.AdjustContrast(this.sliderOriginalImage.Value, (int)this.contrastSlider.Value);
            tmp = ImageTools.AdjustGamma(tmp, e.NewValue);
            this.selectedImage.Source = ImageTools.AdjustBrightness(tmp, (int)this.brightnessSlider.Value);
        }
        #endregion

        #region Thumbnail controllers

        // Adds new cover image
        private void AddCoverImage(BitmapSource bitmapSource, Guid guid)
        {
            this.imagesFilePaths.Remove(this.coverGuid);
            this.imagesOriginalSizes.Remove(this.coverGuid);
            this.coverGuid = guid;
            this.selectedImageGuid = guid;
            // add bitmapSource to images
            this.coverThumbnail.IsEnabled = true;
            this.coverThumbnail.Source = bitmapSource;
            this.selectedImage.Source = bitmapSource;
            // set border
            RemoveAllBorders();
            HideAllThumbnailControls();
            (this.coverThumbnail.Parent as Border).BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#6D8527"));
            this.coverThumbnail.IsEnabled = true;
            this.deleteCoverIcon.Visibility = Visibility.Visible;

            // set crop
            SetAppropriateCrop(Size.Empty, this.selectedImage.RenderSize, true);

            EnableImageControllers();
        }

        // Adds new TOC image to list of TOC images
        private void AddTocImage(BitmapSource bitmapSource, Guid guid)
        {
            #region construction of ListItem
            // create thumbnail with following structure
            //<ItemsControl>
            //    <Grid>
            //        <Image HorizontalAlignment="Left" Margin="0,-40,0,0" Stretch="Uniform" VerticalAlignment="Center" Source="/ObalkyKnih-scanner;component/Images/arrows/arrow_up.gif" Width="23"/>
            //        <Image HorizontalAlignment="Left" Margin="0,45,0,0" Stretch="Uniform" VerticalAlignment="Center" Source="/ObalkyKnih-scanner;component/Images/delete_24.png" Width="23"/>
            //        <Image HorizontalAlignment="Left" Margin="0,0,0,0" Stretch="Uniform" VerticalAlignment="Center" Source="/ObalkyKnih-scanner;component/Images/arrows/arrow_down.gif" Width="23"/>
            //        <Border>
            //            <Image HorizontalAlignment="Left" Margin="25,0,0,0" Stretch="Uniform" VerticalAlignment="Top" Source="/ObalkyKnih-scanner;component/Images/default-icon.png" />
            //        </Border>
            //    </Grid>
            //</ItemsControl>
            Image tocImage = new Image();
            tocImage.Name = "tocThumbnail";
            tocImage.MouseLeftButtonDown += Thumbnail_Clicked;
            tocImage.Source = bitmapSource;
            tocImage.Cursor = Cursors.Hand;
            tocImage.MouseEnter += Icon_MouseEnter;
            tocImage.MouseLeave += Icon_MouseLeave;

            Border tocImageBorder = new Border();
            tocImageBorder.BorderThickness = new Thickness(4);
            //green border
            tocImageBorder.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#6D8527"));
            tocImageBorder.Margin = new Thickness(50, 0, 50, 0);

            Image deleteImage = new Image();
            deleteImage.Name = "deleteThumbnail";
            deleteImage.VerticalAlignment = VerticalAlignment.Top;
            deleteImage.HorizontalAlignment = HorizontalAlignment.Right;
            deleteImage.Source = new BitmapImage(new Uri("/ObalkyKnih-scanner;component/Images/ok-icon-delete.png", UriKind.Relative));
            deleteImage.Margin = new Thickness(0, 0, 26, 0);
            deleteImage.Width = 18;
            deleteImage.Stretch = Stretch.None;
            deleteImage.Cursor = Cursors.Hand;
            deleteImage.MouseLeftButtonDown += TocThumbnail_Delete;

            Image moveUpImage = new Image();
            moveUpImage.Name = "moveUpThumbnail";
            moveUpImage.VerticalAlignment = VerticalAlignment.Top;
            moveUpImage.HorizontalAlignment = HorizontalAlignment.Right;
            moveUpImage.Source = new BitmapImage(new Uri("/ObalkyKnih-scanner;component/Images/ok-icon-up.png", UriKind.Relative));
            moveUpImage.Margin = new Thickness(0, 25, 26, 0);
            moveUpImage.Stretch = Stretch.None;
            moveUpImage.Width = 18;
            moveUpImage.Cursor = Cursors.Hand;
            moveUpImage.MouseLeftButtonDown += TocThumbnail_MoveUp;
            if (!this.tocImagesList.HasItems)
            {
                moveUpImage.Visibility = Visibility.Hidden;
            }
            

            Image moveDownImage = new Image();
            moveDownImage.Name = "moveDownThumbnail";
            moveDownImage.VerticalAlignment = VerticalAlignment.Top;
            moveDownImage.HorizontalAlignment = HorizontalAlignment.Right;
            moveDownImage.Source = new BitmapImage(new Uri("/ObalkyKnih-scanner;component/Images/ok-icon-down.png", UriKind.Relative));
            moveDownImage.Margin = new Thickness(0, 50, 26, 0);
            moveDownImage.Width = 18;
            moveDownImage.Stretch = Stretch.None;
            moveDownImage.Cursor = Cursors.Hand;
            moveDownImage.MouseLeftButtonDown += TocThumbnail_MoveDown;
            moveDownImage.Visibility = Visibility.Hidden;

            Grid gridWrapper = new Grid();
            gridWrapper.Margin = new Thickness(0, 10, 0, 10);
            gridWrapper.Name = "guid_" + guid.ToString().Replace("-", "");
            tocImageBorder.Child = tocImage;
            gridWrapper.Children.Add(tocImageBorder);
            gridWrapper.Children.Add(moveUpImage);
            gridWrapper.Children.Add(moveDownImage);
            gridWrapper.Children.Add(deleteImage);
            #endregion

            // edit previously last item - enable moveDown arrow
            if (this.tocImagesList.HasItems)
            {
                var lastItem = this.tocImagesList.Items.OfType<Grid>().LastOrDefault();
                foreach (Image item in lastItem.Children.OfType<Image>())
                {
                    if (item.Name.Contains("moveDownThumbnail"))
                    {
                        item.IsEnabled = true;
                    }
                }
            }

            RemoveAllBorders();
            HideAllThumbnailControls();

            // add to list
            this.tocImagesList.Items.Add(gridWrapper);
            this.tocImagesList.SelectedItem = gridWrapper;

            // assign "pointers" to these elements into dictionaries
            this.selectedImageGuid = guid;
            this.selectedImage.Source = bitmapSource;
            this.tocThumbnailGridsDictionary.Add(guid, gridWrapper);
            SetAppropriateCrop(Size.Empty, this.selectedImage.RenderSize, true);

            string pages = "";
            int pagesNumber = this.tocImagesList.Items.Count;
            switch (pagesNumber)
            {
                case 1:
                    pages = "strana";
                    break;
                case 2:
                case 3:
                case 4:
                    pages = "strany";
                    break;
                default:
                    pages = "stran";
                    break;
            }
            this.tocPagesNumber.Content = pagesNumber + " " + pages;
            EnableImageControllers();
            this.ocrCheckBox.IsChecked = true;
        }

        // Removes colored border from all thumbnails
        private void RemoveAllBorders()
        {
            (this.coverThumbnail.Parent as Border).BorderBrush = Brushes.Transparent;
            foreach (var grid in this.tocThumbnailGridsDictionary.Values)
            {
                grid.Children.OfType<Border>().First().BorderBrush = Brushes.Transparent;
            }
        }

        // Hides all thumbnail controls (arrows and delete icon)
        private void HideAllThumbnailControls()
        {
            this.deleteCoverIcon.Visibility = Visibility.Hidden;
            foreach (var grid in this.tocThumbnailGridsDictionary.Values)
            {
                foreach (var imageControl in grid.Children.OfType<Image>())
                {
                    imageControl.Visibility = Visibility.Hidden;
                }
            }
        }

        // Makes appropriate controls of toc thumbnail visible
        private void SetThumbnailControls(Guid guid)
        {
            Grid grid = this.tocThumbnailGridsDictionary[guid];
            // set delete icon visible
            (LogicalTreeHelper.FindLogicalNode(grid, "deleteThumbnail") as Image).Visibility = Visibility.Visible;
            Image moveUp = LogicalTreeHelper.FindLogicalNode(grid, "moveUpThumbnail") as Image;
            Image moveDown = LogicalTreeHelper.FindLogicalNode(grid, "moveDownThumbnail") as Image;

            if(!grid.Equals(this.tocImagesList.Items.GetItemAt(0)))
            {
                moveUp.Visibility = Visibility.Visible;
            }

            if (!grid.Equals(this.tocImagesList.Items.GetItemAt(this.tocImagesList.Items.Count - 1)))
            {
                moveDown.Visibility = Visibility.Visible;
            }

        }

        // Sets the selectedImage
        private void Thumbnail_Clicked(object sender, MouseButtonEventArgs e)
        {
            Image image = sender as Image;
            this.selectedImage.Source = image.Source;
            SetAppropriateCrop(Size.Empty, this.selectedImage.RenderSize, true);

            RemoveAllBorders();
            HideAllThumbnailControls();

            // find out if new image is toc or cover and color the border
            if (image.Name.Equals("coverThumbnail"))
            {
                this.selectedImageGuid = this.coverGuid;
                (image.Parent as Border).BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#6D8527"));
                this.deleteCoverIcon.Visibility = Visibility.Visible;
            }
            else
            {
                Border border = (sender as Image).Parent as Border;
                string guidName = (border.Parent as Grid).Name;
                foreach (var key in this.imagesFilePaths.Keys)
                {
                    if (guidName.Contains(key.ToString().Replace("-", "")))
                    {
                        this.selectedImageGuid = key;
                    }
                }
                border.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#6D8527"));
                SetThumbnailControls(this.selectedImageGuid);
            }

            EnableImageControllers();
            SetAppropriateCrop(Size.Empty, this.selectedImage.RenderSize, true);
        }        

        // Sets selected TOC image from list of all TOC images
        private void TocThumbnail_MoveUp(object sender, MouseButtonEventArgs e)
        {
            int selectedIndex = this.tocImagesList.SelectedIndex;

            // check sanity of moving up
            if (selectedIndex <= 0 || selectedIndex > this.tocImagesList.Items.Count - 1)
            {
                return;
            }

            // get the grid
            var tmp = this.tocImagesList.Items.GetItemAt(selectedIndex);
            // move it
            this.tocImagesList.Items.RemoveAt(selectedIndex);
            this.tocImagesList.Items.Insert(selectedIndex - 1, tmp);

            this.tocImagesList.SelectedIndex = selectedIndex - 1;

            HideAllThumbnailControls();
            SetThumbnailControls(this.selectedImageGuid);
        }

        // Sets selected TOC image from list of all TOC images
        private void TocThumbnail_MoveDown(object sender, MouseButtonEventArgs e)
        {
            int selectedIndex = this.tocImagesList.SelectedIndex;

            // check sanity of moving down
            if (selectedIndex < 0 || selectedIndex >= this.tocImagesList.Items.Count - 1)
            {
                return;
            }

            // get the grid
            var tmp = this.tocImagesList.Items.GetItemAt(selectedIndex);
            // move it
            this.tocImagesList.Items.RemoveAt(selectedIndex);
            this.tocImagesList.Items.Insert(selectedIndex + 1, tmp);

            this.tocImagesList.SelectedIndex = selectedIndex + 1;

            HideAllThumbnailControls();
            SetThumbnailControls(this.selectedImageGuid);
        }

        //Removes image from thumbnails
        private void TocThumbnail_Delete(object sender, MouseButtonEventArgs e)
        {
            int selectedIndex = this.tocImagesList.SelectedIndex;
            // sanity check
            if (selectedIndex < 0 || selectedIndex >= this.tocImagesList.Items.Count)
            {
                return;
            }

            var result = MessageBox.Show("Opravdu chcete odstranit vybraný obsah?", "Potvrzení odstranění",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                DisableImageControllers();

                Guid guid = (from record in tocThumbnailGridsDictionary.ToList()
                             where record.Value.Equals(this.tocImagesList.Items.GetItemAt(selectedIndex))
                             select record.Key).First();

                if (guid != Guid.Empty)
                {
                    BitmapSource tmpImage = ImageTools.LoadFullSize(this.imagesFilePaths[guid]);
                    backupImage = new KeyValuePair<string, BitmapSource>(this.imagesFilePaths[guid], tmpImage);
                    SignalLoadedBackup();
                }

                try
                {
                    File.Delete(this.imagesFilePaths[guid]);
                }
                catch (Exception)
                {
                    MessageBox.Show("Nebylo možné zmazat soubor z disku.", "Chyba mazání souboru.", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                this.tocImagesList.Items.RemoveAt(selectedIndex);
                this.tocThumbnailGridsDictionary.Remove(guid);
                this.imagesFilePaths.Remove(guid);
                this.imagesOriginalSizes.Remove(guid);

                HideAllThumbnailControls();

                if (!this.tocImagesList.HasItems)
                {
                    if (this.coverGuid == Guid.Empty)
                    {
                        // set default image
                        this.selectedImageGuid = Guid.Empty;
                        this.selectedImage.Source = new BitmapImage(
                            new Uri("/ObalkyKnih-scanner;component/Images/default-icon.png", UriKind.Relative));

                        Mouse.OverrideCursor = null;
                    }
                    else
                    {
                        this.selectedImageGuid = this.coverGuid;
                        (this.coverThumbnail.Parent as Border).BorderBrush = (SolidColorBrush)(new BrushConverter()
                            .ConvertFrom("#6D8527"));
                        this.deleteCoverIcon.Visibility = Visibility.Visible;
                        this.selectedImage.Source = coverThumbnail.Source;

                        EnableImageControllers();
                    }
                    this.ocrCheckBox.IsChecked = false;
                }
                else
                {

                    Grid grid = this.tocImagesList.Items.GetItemAt(tocImagesList.Items.Count - 1) as Grid;
                    Image thumbnail = LogicalTreeHelper.FindLogicalNode(grid, "tocThumbnail") as Image;
                    this.selectedImage.Source = thumbnail.Source;
                    foreach (var guidKey in this.imagesFilePaths.Keys)
                    {
                        if (grid.Name.Contains(guidKey.ToString().Replace("-", "")))
                        {
                            this.selectedImageGuid = guidKey;
                        }
                    }

                    SetThumbnailControls(this.selectedImageGuid);
                    this.tocThumbnailGridsDictionary[this.selectedImageGuid].Children.OfType<Border>().First()
                        .BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#6D8527"));

                    this.tocImagesList.SelectedItem = this.tocThumbnailGridsDictionary[this.selectedImageGuid];
                    EnableImageControllers();
                }

                // set numer of pages
                string pages = "";
                int pagesNumber = this.tocImagesList.Items.Count;
                switch (pagesNumber)
                {
                    case 1:
                        pages = "strana";
                        break;
                    case 2:
                    case 3:
                    case 4:
                        pages = "strany";
                        break;
                    default:
                        pages = "stran";
                        break;
                }
                this.tocPagesNumber.Content = pagesNumber + " " + pages;
            }
        }

        private void CoverThumbnail_Delete(object sender, MouseButtonEventArgs e)
        {
            var result = MessageBox.Show("Opravdu chcete odstranit vybraný obsah?", "Potvrzení odstranění",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                DisableImageControllers();
                this.imagesFilePaths.Remove(this.coverGuid);
                this.imagesOriginalSizes.Remove(this.coverGuid);
                this.coverGuid = Guid.Empty;
                this.coverThumbnail.IsEnabled = false;
                this.deleteCoverIcon.Visibility = Visibility.Hidden;
                this.coverThumbnail.Source = new BitmapImage(
                        new Uri("/ObalkyKnih-scanner;component/Images/default-icon.png", UriKind.Relative));

                if (this.tocThumbnailGridsDictionary.Keys.Count > 0)
                {
                    this.selectedImageGuid = this.tocThumbnailGridsDictionary.Keys.Last();
                    this.selectedImage.Source = (LogicalTreeHelper.FindLogicalNode(
                        this.tocThumbnailGridsDictionary.Values.Last(), "tocThumbnail") as Image).Source;

                    this.tocImagesList.SelectedItem = this.tocThumbnailGridsDictionary[this.selectedImageGuid];
                    this.tocThumbnailGridsDictionary[this.selectedImageGuid].Children.OfType<Border>().First()
                        .BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#6D8527"));
                    SetThumbnailControls(this.selectedImageGuid);

                    EnableImageControllers();
                }
                else
                {
                    // set default image
                    this.selectedImageGuid = Guid.Empty;
                    this.selectedImage.Source = new BitmapImage(
                        new Uri("/ObalkyKnih-scanner;component/Images/default-icon.png", UriKind.Relative));
                }
            }
        }
        #endregion

        #region Undo/Redo
        internal void UndoLastStep()
        {
            Guid guid = (from record in this.imagesFilePaths
                         where record.Value.Equals(this.backupImage.Key)
                         select record.Key).SingleOrDefault();
            bool isCover = this.backupImage.Key.Contains("cover");
            bool isChanged = this.imagesFilePaths.ContainsValue(this.backupImage.Key);

            // copy current image to redoImage and backupImage to current image
            if (this.workingImage.Key == guid)
            {
                this.redoImage = new KeyValuePair<string, BitmapSource>(
                    this.backupImage.Key, this.workingImage.Value);
            }
            else
            {
                try
                {
                    ImageTools.SaveToFile(this.workingImage.Value, this.imagesFilePaths[this.workingImage.Key]);
                    this.redoImage = new KeyValuePair<string, BitmapSource>(
                        this.backupImage.Key, ImageTools.LoadFullSize(this.backupImage.Key));
                }
                catch (Exception)
                {
                    MessageBox.Show("Nastal problém při ukládání obrázku do souboru.", "Chyba!", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
            }
            this.workingImage = new KeyValuePair<Guid, BitmapSource>(guid, this.backupImage.Value);
            BitmapSource newImage = ImageTools.LoadGivenSizeFromBitmapSource(this.workingImage.Value, 800);

            // if image was changed, else image was deleted
            if (isChanged)
            {
                // image was changed
                this.imagesOriginalSizes[guid] = new Size(this.workingImage.Value.PixelWidth, this.workingImage.Value.PixelHeight);

                if (this.selectedImageGuid == guid)
                {
                    this.selectedImage.Source = newImage;
                }

                if (isCover)
                {
                    this.coverThumbnail.Source = newImage;
                }
                else
                {
                    (LogicalTreeHelper.FindLogicalNode(this.tocThumbnailGridsDictionary[guid],
                    "tocThumbnail") as Image).Source = newImage;
                }
            }
            else
            {
                // image was deleted
                Guid newGuid = new Guid();
                while (this.imagesFilePaths.ContainsKey(newGuid))
                {
                    newGuid = new Guid();
                }
                string newFileName = Settings.TemporaryFolder + ((isCover) ? "obalkyknih-cover_" : "obalkyknih-toc_")
                    + barcode + "_" + newGuid + ".tif";

                Size originalSize = new Size(this.backupImage.Value.PixelWidth, this.backupImage.Value.PixelHeight);


                this.imagesFilePaths.Add(newGuid, newFileName);
                this.imagesOriginalSizes.Add(newGuid, originalSize);

                if (isCover)
                {
                    AddCoverImage(newImage, newGuid);
                }
                else
                {
                    AddTocImage(newImage, newGuid);
                }
            }

            this.backupImage = new KeyValuePair<string, BitmapSource>(null, null);
            (Window.GetWindow(this) as MainWindow).DeactivateUndo();
            if (isChanged || isCover)
            {
                (Window.GetWindow(this) as MainWindow).ActivateRedo();
            }
            else
            {
                this.redoImage = new KeyValuePair<string, BitmapSource>(null, null);
            }
            GC.Collect();
        }

        internal void RedoLastStep()
        {
            bool isCover = this.redoImage.Key.Contains("cover");
            String backupFilePath = this.backupImage.Key;
            Guid backupGuid = (from record in this.imagesFilePaths
                               where record.Value.Equals(backupFilePath)
                               select record.Key).SingleOrDefault();

            String redoFilePath = this.redoImage.Key;
            Guid redoGuid = (from record in this.imagesFilePaths
                             where record.Value.Equals(redoFilePath)
                             select record.Key).SingleOrDefault();

            // save current to backup
            if (this.workingImage.Key == redoGuid)
            {
                this.backupImage = new KeyValuePair<string, BitmapSource>(this.redoImage.Key,
                    this.workingImage.Value);
            }
            else
            {
                this.backupImage = new KeyValuePair<string, BitmapSource>(this.redoImage.Key,
                    ImageTools.LoadFullSize(this.redoImage.Key));
                if (this.workingImage.Key != Guid.Empty && this.imagesFilePaths.ContainsKey(this.workingImage.Key))
                {
                    try
                    {
                        ImageTools.SaveToFile(this.workingImage.Value, this.imagesFilePaths[this.workingImage.Key]);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Nastal problém při ukládání obrázku do souboru.", "Chyba!", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }
                }
            }
            this.workingImage = new KeyValuePair<Guid, BitmapSource>(redoGuid, this.redoImage.Value);
            BitmapSource newImage = ImageTools.LoadGivenSizeFromBitmapSource(this.redoImage.Value, 800);

            this.imagesOriginalSizes.Remove(redoGuid);
            this.imagesOriginalSizes.Add(redoGuid, new Size(this.redoImage.Value.PixelWidth, this.redoImage.Value.PixelHeight));

            if (this.selectedImageGuid == redoGuid)
            {
                this.selectedImage.Source = newImage;
            }

            if (isCover)
            {
                this.coverThumbnail.Source = newImage;
            }
            else
            {
                (LogicalTreeHelper.FindLogicalNode(this.tocThumbnailGridsDictionary[redoGuid],
                "tocThumbnail") as Image).Source = newImage;
            }

            this.redoImage = new KeyValuePair<string, BitmapSource>(null, null);
            (Window.GetWindow(this) as MainWindow).DeactivateRedo();
            (Window.GetWindow(this) as MainWindow).ActivateUndo();
        }
        #endregion

        // Sends to ObalkyKnih
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            this.controlTabItem.IsEnabled = false;
            SendToObalkyKnih();
        }

        // Creates hover effect for transormation controllers
        private void Icon_MouseEnter(object sender, EventArgs e)
        {
            (sender as UIElement).Opacity = 0.7;
        }

        // Creates hover effect for transormation controllers
        private void Icon_MouseLeave(object sender, EventArgs e)
        {
            (sender as UIElement).Opacity = 1;
        }

        // Disables image editing controllers
        private void DisableImageControllers()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            this.rotateRightIcon.IsEnabled = false;
            this.rotate180Icon.IsEnabled = false;
            this.deskewIcon.IsEnabled = false;
            this.flipIcon.IsEnabled = false;
            this.cropIcon.IsEnabled = false;
            this.brightnessSlider.IsEnabled = false;
            this.contrastSlider.IsEnabled = false;
            this.gammaSlider.IsEnabled = false;
            this.sliderConfirmButton.IsEnabled = false;
        }

        // Enables image editing controllers
        private void EnableImageControllers()
        {
            Mouse.OverrideCursor = null;
            this.rotateLeftIcon.IsEnabled = true;
            this.rotateRightIcon.IsEnabled = true;
            this.rotate180Icon.IsEnabled = true;
            this.deskewIcon.IsEnabled = true;
            this.flipIcon.IsEnabled = true;
            this.cropIcon.IsEnabled = true;
            this.brightnessSlider.IsEnabled = true;
            this.contrastSlider.IsEnabled = true;
            this.gammaSlider.IsEnabled = true;
            this.sliderConfirmButton.IsEnabled = true;
            this.brightnessSlider.Value = 0;
            this.contrastSlider.Value = 0;
            this.gammaSlider.Value = 1;
        }

        private void SelectedImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetAppropriateCrop(e.PreviousSize, e.NewSize, false);
        }

        private void SetAppropriateCrop(Size previousSize, Size newSize, bool calculateCropZone)
        {
            if (this.selectedImageGuid == Guid.Empty)
            {
                return;
            }
            BitmapSource source = this.selectedImage.Source as BitmapSource;
            double ratioNewSizeToThumbX = newSize.Width / source.PixelWidth;
            double ratioNewSizeToThumbY = newSize.Height / source.PixelHeight;

            // Coordinates where started previous cropZone
            Point previousCropZoneFrom = (this.cropper == null) ? new Point() :
                new Point(this.cropper.ClippingRectangle.X, this.cropper.ClippingRectangle.Y);
            // Size of previous cropZone
            Size previousCropZoneSize = (this.cropper == null) ? Size.Empty :
                new Size(this.cropper.ClippingRectangle.Width, this.cropper.ClippingRectangle.Height);
            // size of cropZone in full (not scaled) image
            Size originalSizeCropZone = (this.cropper == null) ? Size.Empty : this.cropper.CropZone;
            // real size of image (pixel size of this image on disk)
            Size originalSourceSize = this.imagesOriginalSizes[this.selectedImageGuid];
            // display size of cropZone, (pixel size of cropZone as displayed on screen) 

            Size newCropZoneSize = Size.Empty;
            Point newCropZoneFrom = new Point(0, 0);

            if (this.cropper == null) //adding first image
            {
                Rect calculatedCrop = ImageTools.FindCropZone(source);
                newCropZoneFrom = new Point(ratioNewSizeToThumbX * calculatedCrop.X, ratioNewSizeToThumbY * calculatedCrop.Y);
                newCropZoneSize = new Size(ratioNewSizeToThumbX * calculatedCrop.Width, ratioNewSizeToThumbY * calculatedCrop.Height);
            }
            else // adding image other than first
            {
                if (calculateCropZone) // try to automatically determine crop zone
                {
                    Rect calculatedCrop = ImageTools.FindCropZone(source);
                    if (originalSizeCropZone.Equals(Size.Empty) || originalSizeCropZone.Equals(new Size(0, 0)))
                    {
                        newCropZoneFrom = new Point(ratioNewSizeToThumbX * calculatedCrop.X, ratioNewSizeToThumbY * calculatedCrop.Y);
                        newCropZoneSize = new Size(ratioNewSizeToThumbX * calculatedCrop.Width, ratioNewSizeToThumbY * calculatedCrop.Height);

                    }
                    else
                    {

                        // count new X and Y coordinates for start of crop zone
                        double ratioOrigX = (double)originalSourceSize.Width / source.PixelWidth;
                        double ratioOrigY = (double)originalSourceSize.Height / source.PixelHeight;

                        double newFromX = ((ratioOrigX * calculatedCrop.X + originalSizeCropZone.Width) > originalSourceSize.Width) ?
                            (originalSourceSize.Width - originalSizeCropZone.Width) * (newSize.Width / originalSourceSize.Width)
                            : ratioNewSizeToThumbX * calculatedCrop.X;
                        double newFromY = ((ratioOrigY * calculatedCrop.Y + originalSizeCropZone.Height) > originalSourceSize.Height) ?
                            (originalSourceSize.Height - originalSizeCropZone.Height) * (newSize.Height / originalSourceSize.Height)
                            : ratioNewSizeToThumbY * calculatedCrop.Y;
                        newCropZoneFrom = new Point(newFromX, newFromY);
                        // set width and height of crop
                        newCropZoneSize = new Size((newSize.Width / originalSourceSize.Width) * originalSizeCropZone.Width,
                            (newSize.Height / originalSourceSize.Height) * originalSizeCropZone.Height);
                    }
                }
                else //image was resized, don't change crop zone, only resize it
                {
                    // set X and Y of crop
                    newCropZoneFrom = new Point((newSize.Width / previousSize.Width) * previousCropZoneFrom.X,
                        (newSize.Height / previousSize.Height) * previousCropZoneFrom.Y);
                    // set width and height of crop
                    newCropZoneSize = new Size((newSize.Width / previousSize.Width) * previousCropZoneSize.Width,
                        (newSize.Height / previousSize.Height) * previousCropZoneSize.Height);
                }
            }

            // limit to size of image
            if (newCropZoneSize.Height + newCropZoneFrom.Y > newSize.Height)
            {
                newCropZoneSize.Height = newSize.Height - newCropZoneFrom.Y;
            }
            if (newCropZoneSize.Width + newCropZoneFrom.X > newSize.Width)
            {
                newCropZoneSize.Width = newSize.Width - newCropZoneFrom.X;
            }

            ImageTools.AddCropToElement(this.selectedImage, ref this.cropper,
                    new Rect(newCropZoneFrom, newCropZoneSize));
        }

        private void SignalLoadedBackup()
        {
            this.redoImage = new KeyValuePair<string,BitmapSource>(null, null);
            (Window.GetWindow(this) as MainWindow).ActivateUndo();
            (Window.GetWindow(this) as MainWindow).DeactivateRedo();
        }
        #endregion

        #region control tab controls
        
        // Shows windows for barcode
        private void controlNewUnitButton_Click(object sender, RoutedEventArgs e)
        {
            (Window.GetWindow(this) as MainWindow).ShowNewUnitWindow();
        }

        private void FillControlMetadata()
        {
            int counter = 0;

            this.controlTitle.Content = this.titleTextBox.Text;
            this.controlAuthors.Content = this.authorTextBox.Text;
            this.controlYear.Content = this.yearTextBox.Text;

            #region identifiers
            if (!string.IsNullOrWhiteSpace(this.isbnTextBox.Text))
            {
                CreateIdentifierLabel("ISBN", this.isbnTextBox.Text, counter);
                counter++;
            }

            if (!string.IsNullOrWhiteSpace(this.issnTextBox.Text))
            {
                CreateIdentifierLabel("ISSN", this.issnTextBox.Text, counter);
                counter++;
            }

            if (!string.IsNullOrWhiteSpace(this.cnbTextBox.Text))
            {
                CreateIdentifierLabel("ČNB", this.cnbTextBox.Text, counter);
                counter++;
            }

            if (!string.IsNullOrWhiteSpace(this.oclcTextBox.Text))
            {
                CreateIdentifierLabel("OCLC", this.oclcTextBox.Text, counter);
                counter++;
            }
            if (!string.IsNullOrWhiteSpace(this.eanTextBox.Text))
            {
                CreateIdentifierLabel("EAN", this.eanTextBox.Text, counter);
                counter++;
            }

            if (!string.IsNullOrWhiteSpace(this.urnNbnTextBox.Text))
            {
                CreateIdentifierLabel("URN:NBN", this.urnNbnTextBox.Text, counter);
                counter++;
            }

            if (!string.IsNullOrWhiteSpace(this.siglaTextBox.Text))
            {
                CreateIdentifierLabel("Vlastní", Settings.Sigla + "-" + this.siglaTextBox.Text, counter);
                counter++;
            }            
            #endregion
        }

        private void CreateIdentifierLabel(string identifierName, string identifierValue, int counter)
        {
            Label label = new Label();
            label.FontFamily = (FontFamily)(new FontFamilyConverter()).ConvertFromInvariantString("Arial");
            label.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#858585"));
            label.Margin = new Thickness(0, counter * 20, 0, 0);
            label.Content = identifierName;

            Label label2 = new Label();
            label2.FontFamily = (FontFamily)(new FontFamilyConverter()).ConvertFromInvariantString("Arial");
            label2.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#cecece"));
            label2.Margin = new Thickness(70, counter * 20, 0, 0);
            label2.Content = identifierValue;

            this.controlIdentifiersGrid.Children.Add(label);
            this.controlIdentifiersGrid.Children.Add(label2);
        }
        #endregion

        private void tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Keyboard.Focus(this.tabControl.SelectedItem as TabItem);
            if (this.tabControl.SelectedItem != null &&
                "scanningTabItem".Equals((this.tabControl.SelectedItem as TabItem).Name))
            {
                if (this.backupImage.Key != null)
                {
                    (Window.GetWindow(this) as MainWindow).ActivateUndo();
                }

                if (this.redoImage.Key != null)
                {
                    (Window.GetWindow(this) as MainWindow).ActivateRedo();
                }
            }
            else
            {
                (Window.GetWindow(this) as MainWindow).DeactivateUndo();
                (Window.GetWindow(this) as MainWindow).DeactivateRedo();
            }
        }
    }

    #region Custom WPF controls

    /// <summary>
    /// Custom ListView - changed not to catch events with arrow keys and scrolling,
    /// because of rotation key bindings
    /// </summary>
    public class MyListView : ListView
    {
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            return;
        }
        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            e.Handled = true;

            var e2 = new MouseWheelEventArgs(e.MouseDevice,e.Timestamp,e.Delta);
            e2.RoutedEvent = UIElement.MouseWheelEvent;
            this.RaiseEvent(e2);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Left || e.Key == Key.Right
                    || e.Key == Key.Up || e.Key == Key.Down)
            {
                return;
            }
            base.OnKeyDown(e);
        }
    }
    /// <summary>
    /// Custom ScrollViewer - changed not to catch events with arrow keys,
    /// because of rotation key bindings
    /// </summary>
    public class MyScrollViewer : ScrollViewer
    {
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.Left || e.Key == Key.Right
                    || e.Key == Key.Up || e.Key == Key.Down)
                    return;
            }
            base.OnKeyDown(e);
        }
    }

    /// <summary>
    /// Custom GridSplitter - changed not to catch events with arrow keys,
    /// because of rotation key bindings
    /// </summary>
    public class MyGridSplitter : GridSplitter
    {
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.Left || e.Key == Key.Right
                    || e.Key == Key.Up || e.Key == Key.Down)
                    return;
            }
            base.OnKeyDown(e);
        }
    }
    #endregion
}
