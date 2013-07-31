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
        RoutedCommand flipVerticalCommand = new RoutedCommand();
        RoutedCommand flipHorizontalCommand = new RoutedCommand();
        RoutedCommand cropCommand = new RoutedCommand();
        RoutedCommand scanImageCommand = new RoutedCommand();
        #endregion

        // Barcode of the unit
        private string barcode;

        // Object responsible for cropping of cover images
        private CroppingAdorner coverCropper;

        // Object responsible for cropping of toc images
        private CroppingAdorner tocCropper;

        // Chosen scanner device
        private Device activeScanner;

        // TOC image that is currently edited
        private Image selectedTocImageThumbnail;

        // Flag indicating that some image (other than default) was set as selectedCoverImage
        private bool isSelectedCoverImage = false;

        // Flag indicating that som image (other than default) was set as selectedTocImage
        private bool isSelectedTocImage = false;

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

        /// <summary>
        /// constructor, creates new TabsControl based on given barcode
        /// </summary>
        /// <param name="barcode">barcode of the unit, that will be processed</param>
        public TabsControl(string barcode)
        {
            this.barcode = barcode;
            InitializeComponent();
            InitializeBackgroundWorkers();
            metadataReceiverBackgroundWorker.RunWorkerAsync();

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

            //flipVertical
            cb = new CommandBinding(this.flipVerticalCommand, FlipVerticalCommandExecuted, FlipVerticalCommandCanExecute);
            this.CommandBindings.Add(cb);
            kg = new KeyGesture(Key.Up, ModifierKeys.Control);
            ib = new InputBinding(this.flipVerticalCommand, kg);
            this.InputBindings.Add(ib);
            kg = new KeyGesture(Key.Down, ModifierKeys.Control);
            ib = new InputBinding(this.flipVerticalCommand, kg);
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

            //scanImage
            cb = new CommandBinding(this.scanImageCommand, ScanImageCommandExecuted, ScanImageCommandCanExecute);
            this.CommandBindings.Add(cb);
            kg = new KeyGesture(Key.S, ModifierKeys.Control);
            ib = new InputBinding(this.scanImageCommand, kg);
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
            if (currentTab.Equals(this.coverTabItem) && isSelectedCoverImage)
            {
                ImageTools.RotateImage(this.selectedCoverImage, -90);
                this.coverImageThumbnail.Source = this.selectedCoverImage.Source; 
                this.confirmCoverImage.Source = this.selectedCoverImage.Source;
            }
            else if (currentTab.Equals(this.tocTabItem) && isSelectedTocImage)
            {
                ImageSource oldSource = this.selectedTocImage.Source;
                ImageTools.RotateImage(this.selectedTocImage, -90);
                ImageSource newSource = this.selectedTocImage.Source;
                this.selectedTocImageThumbnail.Source = newSource;
                SetConfirmationTocNewImageSource(oldSource, newSource);
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
            if (currentTab.Equals(this.coverTabItem) && isSelectedCoverImage)
            {
                ImageTools.RotateImage(this.selectedCoverImage, 90);
                this.coverImageThumbnail.Source = this.selectedCoverImage.Source;
                this.confirmCoverImage.Source = this.selectedCoverImage.Source;
            }
            else if (currentTab.Equals(this.tocTabItem) && isSelectedTocImage)
            {
                ImageSource oldSource = this.selectedTocImage.Source;
                ImageTools.RotateImage(this.selectedTocImage, 90);
                ImageSource newSource = this.selectedTocImage.Source;
                this.selectedTocImageThumbnail.Source = newSource;
                SetConfirmationTocNewImageSource(oldSource, newSource);
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
            if (currentTab.Equals(this.coverTabItem) && isSelectedCoverImage)
            {
                ImageTools.RotateImage(this.selectedCoverImage, 180);
                this.coverImageThumbnail.Source = this.selectedCoverImage.Source;
                this.confirmCoverImage.Source = this.selectedCoverImage.Source;
            }
            else if (currentTab.Equals(this.tocTabItem) && isSelectedTocImage)
            {
                ImageSource oldSource = this.selectedTocImage.Source;
                ImageTools.RotateImage(this.selectedTocImage, 180);
                ImageSource newSource = this.selectedTocImage.Source;
                this.selectedTocImageThumbnail.Source = newSource;
                SetConfirmationTocNewImageSource(oldSource, newSource);
            }
        }

        //flipVertical
        private void FlipVerticalCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void FlipVerticalCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            TabItem currentTab = tabControl.SelectedItem as TabItem;
            if (currentTab.Equals(this.coverTabItem) && isSelectedCoverImage)
            {
                ImageTools.FlipVerticalImage(this.selectedCoverImage);
                this.coverImageThumbnail.Source = this.selectedCoverImage.Source;
                this.confirmCoverImage.Source = this.selectedCoverImage.Source;
            }
            else if (currentTab.Equals(this.tocTabItem) && isSelectedTocImage)
            {
                ImageSource oldSource = this.selectedTocImage.Source;
                ImageTools.FlipVerticalImage(this.selectedTocImage);
                ImageSource newSource = this.selectedTocImage.Source;
                this.selectedTocImageThumbnail.Source = newSource;
                SetConfirmationTocNewImageSource(oldSource, newSource);
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
            if (currentTab.Equals(this.coverTabItem) && isSelectedCoverImage)
            {
                ImageTools.FlipHorizontalImage(this.selectedCoverImage);
                this.coverImageThumbnail.Source = this.selectedCoverImage.Source;
                this.confirmCoverImage.Source = this.selectedCoverImage.Source;
            }
            else if (currentTab.Equals(this.tocTabItem) && isSelectedTocImage)
            {
                ImageSource oldSource = this.selectedTocImage.Source;
                ImageTools.FlipHorizontalImage(this.selectedTocImage);
                ImageSource newSource = this.selectedTocImage.Source;
                this.selectedTocImageThumbnail.Source = newSource;
                SetConfirmationTocNewImageSource(oldSource, newSource);
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
            if (currentTab.Equals(this.coverTabItem) && isSelectedCoverImage)
            {
                ImageTools.CropImage(this.selectedCoverImage, ref this.coverCropper);
                this.coverImageThumbnail.Source = this.selectedCoverImage.Source;
                this.confirmCoverImage.Source = this.selectedCoverImage.Source;
            }
            else if (currentTab.Equals(this.tocTabItem) && isSelectedTocImage)
            {
                ImageSource oldSource = this.selectedTocImage.Source;
                ImageTools.CropImage(this.selectedTocImage, ref this.tocCropper);
                ImageSource newSource = this.selectedTocImage.Source;
                this.selectedTocImageThumbnail.Source = newSource;
                SetConfirmationTocNewImageSource(oldSource, newSource);
            }
        }

        //scanImage
        private void ScanImageCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void ScanImageCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            TabItem currentTab = tabControl.SelectedItem as TabItem;
            if (currentTab.Equals(this.coverTabItem))
            {
                ScanImage(true);
            }
            else if (currentTab.Equals(this.tocTabItem))
            {
                ScanImage(false);
            }
        }
        #endregion

        #region metadata tab controls

        // Shows all available metadata in new MetadataWindow
        private void showCompleteMetadataButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.metadataRetriever.Metadata != null)
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
            this.metadataReceiverBackgroundWorker.RunWorkerAsync();
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
            metadataRetriever = new MetadataRetriever(this.barcode);
            e.Result = metadataRetriever;
        }

        // Called after worker ended job, shows status with which worker ended
        private void MetadataReceiverBW_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            (Window.GetWindow(this) as MainWindow).RemoveMessageFromStatusBar("Stahuji metadata.");
            this.downloadMetadataButton.IsEnabled = true;
            if (e.Error != null)
            {
                System.Windows.MessageBox.Show(e.Error.Message, "Chyba při stahování metadat",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else if (!e.Cancelled)
            {
                this.metadataRetriever = e.Result as MetadataRetriever;
                if (this.metadataRetriever.Metadata != null)
                {
                    FillMetadata(this.metadataRetriever.Metadata);
                    this.showCompleteMetadataButton.IsEnabled = true;
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
                this.originalCoverImage.Source = imgsrc;
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
                this.originalTocImage.Source = imgsrc;
                this.originalTocImage.Cursor = Cursors.Hand;
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
            //copy basic info
            this.confirmTitle.Content = this.titleTextBox.Text;
            this.confirmAuthor.Content = this.authorTextBox.Text;
            this.confirmYear.Content = this.yearTextBox.Text;
            this.confirmIdentifiers.Content = "";

            string error;
            //ISBN
            if (!string.IsNullOrEmpty(this.isbnTextBox.Text))
            {

                error = ValidateIsbn(this.isbnTextBox.Text);
                if (error != null)
                {
                    this.isbnErrorLabel.Content = 'x';
                    this.isbnErrorLabel.ToolTip = this.isbnTextBox.ToolTip = error;
                }
                else
                {
                    this.isbnErrorLabel.Content = "";
                    this.isbnErrorLabel.ToolTip = this.isbnTextBox.ToolTip = null;
                    this.confirmIdentifiers.Content += "ISBN: " + this.isbnTextBox.Text + Environment.NewLine;
                }
            }
            // ISSN 7 numbers + checksum
            if (!string.IsNullOrEmpty(this.issnTextBox.Text))
            {

                error = ValidateIssn(this.issnTextBox.Text);
                if (error != null)
                {
                    this.issnErrorLabel.Content = 'x';
                    this.issnErrorLabel.ToolTip = this.issnTextBox.ToolTip = error;
                }
                else
                {
                    this.issnErrorLabel.Content = "";
                    this.issnErrorLabel.ToolTip = this.issnTextBox.ToolTip = null;
                    this.confirmIdentifiers.Content += "ISSN: " + this.issnTextBox.Text + Environment.NewLine;
                }
            }
            // EAN - 12 numbers + checksum
            if (!string.IsNullOrEmpty(this.eanTextBox.Text))
            {

                error = ValidateEan(this.eanTextBox.Text);
                if (error != null)
                {
                    this.eanErrorLabel.Content = 'x';
                    this.eanErrorLabel.ToolTip = this.eanTextBox.ToolTip = error;
                }
                else
                {
                    this.eanErrorLabel.Content = "";
                    this.eanErrorLabel.ToolTip = this.eanTextBox.ToolTip = null;
                    this.confirmIdentifiers.Content += "EAN: " + this.eanTextBox.Text + Environment.NewLine;
                }
            }
            // CNB - cnb + 9 numbers
            if (!string.IsNullOrEmpty(this.cnbTextBox.Text))
            {

                error = ValidateCnb(this.cnbTextBox.Text);
                if (error != null)
                {
                    this.cnbErrorLabel.Content = 'x';
                    this.cnbErrorLabel.ToolTip = this.cnbTextBox.ToolTip = error;
                }
                else
                {
                    this.cnbErrorLabel.Content = "";
                    this.cnbErrorLabel.ToolTip = this.cnbTextBox.ToolTip = null;
                    this.confirmIdentifiers.Content += "ČNB: " + this.cnbTextBox.Text + Environment.NewLine;
                }
            }
            //OCLC - variable-length numeric string
            if (!string.IsNullOrEmpty(this.oclcTextBox.Text))
            {

                error = ValidateOclc(this.oclcTextBox.Text);
                if (error != null)
                {
                    this.oclcErrorLabel.Content = 'x';
                    this.oclcErrorLabel.ToolTip = this.oclcTextBox.ToolTip = error;
                }
                else
                {
                    this.oclcErrorLabel.Content = "";
                    this.oclcErrorLabel.ToolTip = this.oclcTextBox.ToolTip = null;
                    this.confirmIdentifiers.Content += "OCLC: " + this.oclcTextBox.Text + Environment.NewLine;
                }
            }
            //URN
            if (!string.IsNullOrEmpty(this.urnNbnTextBox.Text))
            {
                this.confirmIdentifiers.Content += "URN:NBN: " + this.urnNbnTextBox.Text + Environment.NewLine;

            }
            //Custom
            if (!string.IsNullOrEmpty(this.siglaTextBox.Text))
            {
                this.confirmIdentifiers.Content += "Vlastní: " + Settings.Sigla
                    + "-" + this.siglaTextBox.Text + Environment.NewLine;

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
                    errorText = "ISBN má " + isbn.Length + " cifer";
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
                errorText = "ISSN má " + issn.Length + "cifer";
            }

            return errorText;
        }

        // Validates given oclc, returns error message or null
        private string ValidateOclc(string oclc)
        {
            string errorText = null;

            int tmp;
            if (!int.TryParse(oclc, out tmp))
            {
                errorText = "OCLC obsahuje nečíselné znaky";
            }

            return errorText;
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
                        errorText = "ČNB obsahuje víc než 9 číslic";
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
                errorText = "EAN má " + ean.Length + "cifer";
            }

            return errorText;
        }
        #endregion
        #endregion

        #region cover tab controls

        // Scan button on cover tab clicked - Scans image and sets it to selectedCoverImage
        private void CoverScanButton_Click(object sender, RoutedEventArgs e)
        {
            bool isCover = true;
            ScanImage(isCover);
        }

        // Saves image to file and opens it in chosen graphical editor
        private void CoverEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Settings.ExternalImageEditor))
            {
                MessageBox.Show("V nastaveních nebyla zadána cesta k externímu editoru",
                    "Chybí cesta k editoru", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string fileUriString = @"temp-cover.tif";
            //save temporary tiff file for edit purposes
            FileStream stream = new FileStream(fileUriString, FileMode.Create);
            TiffBitmapEncoder encoderTiff = new TiffBitmapEncoder();
            encoderTiff.Compression = TiffCompressOption.Lzw;
            encoderTiff.Frames.Add(BitmapFrame.Create(selectedCoverImage.Source as BitmapSource));
            encoderTiff.Save(stream);
            stream.Close();

            Process graphicalEditorProcess = new Process();
            graphicalEditorProcess.StartInfo.FileName = Settings.ExternalImageEditor;
            graphicalEditorProcess.StartInfo.Arguments = fileUriString;
            graphicalEditorProcess.StartInfo.UseShellExecute = false;
            graphicalEditorProcess.Start();
        }

        // Rotates cover 90 degrees left
        private void CoverRotateLeft_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (!isSelectedCoverImage)
            {
                return;
            }
            ImageTools.RotateImage(this.selectedCoverImage, -90);
            this.coverImageThumbnail.Source = this.selectedCoverImage.Source;
            this.confirmCoverImage.Source = this.coverImageThumbnail.Source;
        }

        // Rotates cover 90 degrees right
        private void CoverRotateRight_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (!isSelectedCoverImage)
            {
                return;
            }
            ImageTools.RotateImage(this.selectedCoverImage, 90);
            this.coverImageThumbnail.Source = this.selectedCoverImage.Source;
            this.confirmCoverImage.Source = this.coverImageThumbnail.Source;
        }

        // Rotates cover by 180 degrees
        private void CoverRotate180_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (!isSelectedCoverImage)
            {
                return;
            }
            ImageTools.RotateImage(this.selectedCoverImage, 180);
            this.coverImageThumbnail.Source = this.selectedCoverImage.Source;
            this.confirmCoverImage.Source = this.coverImageThumbnail.Source;
        }

        // Vertically flips cover
        private void CoverVerticalFlip_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (!isSelectedCoverImage)
            {
                return;
            }
            ImageTools.FlipVerticalImage(this.selectedCoverImage);
            this.coverImageThumbnail.Source = this.selectedCoverImage.Source;
            this.confirmCoverImage.Source = this.coverImageThumbnail.Source;
        }

        // Vertically flips cover
        private void CoverHorizontalFlip_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (!isSelectedCoverImage)
            {
                return;
            }
            ImageTools.FlipHorizontalImage(this.selectedCoverImage);
            this.coverImageThumbnail.Source = this.selectedCoverImage.Source;
            this.confirmCoverImage.Source = this.coverImageThumbnail.Source;
        }

        // Changes brightness of cover - irreversible process
        private void CoverBrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int diff = (int)(e.NewValue - e.OldValue);
            TransformedBitmap bi = new TransformedBitmap();
            bi.BeginInit();
            bi.Source = ImageTools.ApplyBrightness(this.selectedCoverImage.Source as BitmapSource, diff);
            bi.EndInit();
            this.selectedCoverImage.Source = bi;
            this.coverImageThumbnail.Source = this.selectedCoverImage.Source;
            this.confirmCoverImage.Source = this.coverImageThumbnail.Source;
        }

        // Changes contrast of cover - irreversible process
        private void CoverContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int diff = (int)(e.NewValue - e.OldValue);
            TransformedBitmap bi = new TransformedBitmap();
            bi.BeginInit();
            bi.Source = ImageTools.ApplyContrast(this.selectedCoverImage.Source as BitmapSource, diff);
            bi.EndInit();
            this.selectedCoverImage.Source = bi;
            this.coverImageThumbnail.Source = this.selectedCoverImage.Source;
            this.confirmCoverImage.Source = this.coverImageThumbnail.Source;
        }

        // Crops cover image - irreversible process
        private void CoverCropButton_Click(object sender, RoutedEventArgs e)
        {
            ImageTools.CropImage(this.selectedCoverImage, ref this.coverCropper);
            this.coverImageThumbnail.Source = this.selectedCoverImage.Source;
            this.confirmCoverImage.Source = this.coverImageThumbnail.Source;
        }

        // Loads cover image from file (bmp, png, jpeg, wmp, gif, tiff)
        private void CoverLoadImageButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "image files (bmp;png;jpeg;wmp;gif;tiff)|*.png;*.bmp;*.jpeg;*.jpg;*.wmp;*.gif;*.tiff;*.tif";
            dlg.FilterIndex = 2;
            bool? result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                string fileName = dlg.FileName;
                bool isCover = true;
                LoadExternalImage(isCover, fileName);
            }
            coverEditButton.IsEnabled = true;
        }

        // Assign cover image to selectedCoverImage and add crop to it
        private void CoverImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Image image = sender as Image;
            this.selectedCoverImage.Source = image.Source;
            this.coverImageThumbnail = image;
            ImageTools.AddCropToElement(this.selectedCoverImage, ref coverCropper);
        }
        #endregion

        #region toc tab controls

        // Rotates TOC image 90 degrees left
        private void TocRotateLeft_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (!isSelectedTocImage)
            {
                return;
            }
            ImageSource oldSource = this.selectedTocImage.Source;
            ImageTools.RotateImage(selectedTocImage, -90);
            ImageSource newSource = this.selectedTocImage.Source;
            this.selectedTocImageThumbnail.Source = newSource;
            SetConfirmationTocNewImageSource(oldSource, newSource);
        }

        // Rotates TOC image 90 degrees right
        private void TocRotateRight_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (!isSelectedTocImage)
            {
                return;
            }
            ImageSource oldSource = this.selectedTocImage.Source;
            ImageTools.RotateImage(selectedTocImage, 90);
            ImageSource newSource = this.selectedTocImage.Source;
            this.selectedTocImageThumbnail.Source = newSource;
            SetConfirmationTocNewImageSource(oldSource, newSource);
        }

        // Rotates TOC image by 180 degrees
        private void TocRotate180_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (!isSelectedTocImage)
            {
                return;
            }
            ImageSource oldSource = this.selectedTocImage.Source;
            ImageTools.RotateImage(selectedTocImage, 180);
            ImageSource newSource = this.selectedTocImage.Source;
            this.selectedTocImageThumbnail.Source = newSource;
            SetConfirmationTocNewImageSource(oldSource, newSource);
        }

        // Vertically flips TOC image
        private void TocVerticalFlip_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (!isSelectedTocImage)
            {
                return;
            }
            ImageSource oldSource = this.selectedTocImage.Source;
            ImageTools.FlipVerticalImage(selectedTocImage);
            ImageSource newSource = this.selectedTocImage.Source;
            this.selectedTocImageThumbnail.Source = newSource;
            SetConfirmationTocNewImageSource(oldSource, newSource);
        }

        //Horizontally flips TOC image
        private void TocHorizontalFlip_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (!isSelectedTocImage)
            {
                return;
            }
            ImageSource oldSource = this.selectedTocImage.Source;
            ImageTools.FlipHorizontalImage(selectedTocImage);
            ImageSource newSource = this.selectedTocImage.Source;
            this.selectedTocImageThumbnail.Source = newSource;
            SetConfirmationTocNewImageSource(oldSource, newSource);
        }

        // Scans new TOC image
        private void tocScanButton_Click(object sender, RoutedEventArgs e)
        {
            bool isCover = false;
            ScanImage(isCover);
        }

        // Crops TOC image
        private void tocCropButton_Click(object sender, RoutedEventArgs e)
        {
            ImageSource oldSource = this.selectedTocImage.Source;
            ImageTools.CropImage(this.selectedTocImage, ref this.tocCropper);
            ImageSource newSource = this.selectedTocImage.Source;
            this.selectedTocImageThumbnail.Source = newSource;
            SetConfirmationTocNewImageSource(oldSource, newSource);
        }

        // Saves TOC image to file and opens it in external editor
        private void tocEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Settings.ExternalImageEditor))
            {
                MessageBox.Show("V nastaveních nebyla zadána cesta k externímu editoru",
                    "Chybí cesta k editoru", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            string fileUriString = @"temp-toc.tif";
            //save temporary tiff file for edit purposes
            FileStream stream = new FileStream(fileUriString, FileMode.Create);
            TiffBitmapEncoder encoderTiff = new TiffBitmapEncoder();
            encoderTiff.Compression = TiffCompressOption.Lzw;
            encoderTiff.Frames.Add(BitmapFrame.Create(selectedTocImage.Source as BitmapSource));
            encoderTiff.Save(stream);
            stream.Close();

            Process graphicalEditorProcess = new Process();
            graphicalEditorProcess.StartInfo.FileName = Settings.ExternalImageEditor;
            graphicalEditorProcess.StartInfo.Arguments = fileUriString;
            graphicalEditorProcess.StartInfo.UseShellExecute = false;
            graphicalEditorProcess.Start();
        }

        // Sets brighness of TOC image
        private void tocBrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ImageSource oldSource = this.selectedTocImage.Source;
            int diff = (int)(e.NewValue - e.OldValue);
            TransformedBitmap bi = new TransformedBitmap();
            bi.BeginInit();
            bi.Source = ImageTools.ApplyBrightness(selectedTocImage.Source as BitmapSource, diff);
            bi.EndInit();
            selectedTocImage.Source = bi;
            ImageSource newSource = this.selectedTocImage.Source;
            this.selectedTocImageThumbnail.Source = newSource;
            SetConfirmationTocNewImageSource(oldSource, newSource);
        }

        // Sets contrast of TOC image
        private void tocContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ImageSource oldSource = this.selectedTocImage.Source;
            int diff = (int)(e.NewValue - e.OldValue);
            TransformedBitmap bi = new TransformedBitmap();
            bi.BeginInit();
            bi.Source = ImageTools.ApplyContrast(selectedTocImage.Source as BitmapSource, diff);
            bi.EndInit();
            selectedTocImage.Source = bi;
            ImageSource newSource = this.selectedTocImage.Source;
            this.selectedTocImageThumbnail.Source = newSource;
            SetConfirmationTocNewImageSource(oldSource, newSource);
        }

        // Sets selected TOC image from list of all TOC images
        private void tocImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.tocBrightnessSlider.Value = 0;
            this.tocContrastSlider.Value = 0;
            Image image = sender as Image;
            this.selectedTocImage.Source = image.Source;
            this.selectedTocImageThumbnail = image;
            ImageTools.AddCropToElement(selectedTocImage, ref tocCropper);

            //enable controlers for image manipulation
            this.isSelectedTocImage = true;
            this.tocCropButton.IsEnabled = true;
            this.tocBrightnessSlider.IsEnabled = true;
            this.tocContrastSlider.IsEnabled = true;
            this.tocEditButton.IsEnabled = true;
        }

        // Loads new TOC image from file
        private void tocLoadImageButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "image files (bmp;png;jpeg;wmp;gif;tiff)|*.png;*.bmp;*.jpeg;*.jpg;*.wmp;*.gif;*.tiff;*.tif";
            dlg.FilterIndex = 2;
            bool? result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                string fileName = dlg.FileName;
                bool isCover = false;
                LoadExternalImage(isCover, fileName);
            }
        }
        #endregion

        #region confirmation tab controls

        // Sends to ObalkyKnih
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendToObalkyKnih();
        }

        // Checks everything and calls uploadWorker to upload to obalkyknih
        private void SendToObalkyKnih()
        {
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

            Stream coverStream = null;
            Stream tocStream = null;
            Stream metaStream = new MemoryStream();

            //cover tiff
            if (this.isSelectedCoverImage)
            {
                try
                {
                    //convert from Bgra32 into RGB24
                    BitmapSource sourceBmp = selectedCoverImage.Source as BitmapSource;
                    PixelFormat pixelFormat = sourceBmp.Format;
                    ColorContext sourceContext = new ColorContext(pixelFormat);
                    ColorContext destContext = new ColorContext(PixelFormats.Rgb24);
                    ColorConvertedBitmap convertedBmp = new ColorConvertedBitmap(
                        sourceBmp, sourceContext, destContext, PixelFormats.Rgb24);

                    TiffBitmapEncoder encoderTiff = new TiffBitmapEncoder();
                    encoderTiff.Compression = TiffCompressOption.Lzw;
                    encoderTiff.Frames.Add(BitmapFrame.Create(convertedBmp));
                    coverStream = new MemoryStream();
                    encoderTiff.Save(coverStream);
                }
                catch(Exception)
                {
                    MessageBox.Show("Obálku se nepovedlo zkonvertovat, pravděpodobně má špatný formát.",
                            "Chybná obálka.", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                var result = MessageBox.Show("Chybí obálka. Opravdu chcete odeslat data bez obálky?",
                "Chybí obálka.", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            //toc multipage tiff
            if (this.confirmTocImagesList.Items.Count > 0)
            {
                try
                {
                    tocStream = new MemoryStream();
                    TiffBitmapEncoder encoderTiff = new TiffBitmapEncoder();
                    encoderTiff.Compression = TiffCompressOption.Lzw;
                    for (int i = 0; i < this.confirmTocImagesList.Items.Count; i++)
                    {
                        if (this.confirmTocImagesList.Items[i] is Image)
                        {
                            //convert from Bgra32 into RGB24
                            BitmapSource sourceBmp = (this.confirmTocImagesList.Items[i] as Image)
                                .Source as BitmapSource;
                            PixelFormat pixelFormat = sourceBmp.Format;
                            ColorContext sourceContext = new ColorContext(pixelFormat);
                            ColorContext destContext = new ColorContext(PixelFormats.Rgb24);
                            ColorConvertedBitmap convertedBmp = new ColorConvertedBitmap(
                                sourceBmp, sourceContext, destContext, PixelFormats.Rgb24);

                            //add frame to multi-frame tiff
                            encoderTiff.Frames.Add(BitmapFrame.Create(convertedBmp));
                        }
                    }
                    encoderTiff.Save(tocStream);
                }
                catch (Exception)
                {
                    MessageBox.Show("Obsah se nepovedlo zkonvertovat, pravděpodobně má špatný formát.",
                            "Chybný obsah", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                //compute SHA1 hash of toc
                SHA1Managed sha = new SHA1Managed();
                byte[] checksumBytes = sha.ComputeHash(tocStream);
                string tocChecksum = BitConverter.ToString(checksumBytes).Replace("-", String.Empty);
                nvc.Add("toc_sha1hex", tocChecksum);
            }
            else
            {
                var result = MessageBox.Show("Opravdu chcete odeslat data bez obsahu?",
                "Chybí obsah", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                {
                    if (coverStream != null)
                    {
                        coverStream.Close();
                        coverStream = null;
                    }
                    return;
                }
            }

            //metastream
            try
            {
                XElement userElement = new XElement("user", Settings.UserName);
                XElement coverDpiElement = new XElement("cover-dpi", Settings.CoverDPI);
                XElement tocDpiElement = new XElement("toc-dpi", Settings.TocDPI);
                XElement coverColorElement = new XElement("cover-color", Settings.CoverScanType.ToString());
                XElement tocColorElement = new XElement("toc-color", Settings.CoverScanType.ToString());
                XElement siglaElement = new XElement("sigla", Settings.Sigla);

                XElement rootElement = new XElement("meta");
                rootElement.Add(siglaElement);
                rootElement.Add(userElement);
                XElement scannerElement = new XElement("scanner");
                scannerElement.Add(coverDpiElement);
                scannerElement.Add(coverColorElement);
                scannerElement.Add(tocDpiElement);
                scannerElement.Add(tocColorElement);
                rootElement.Add(scannerElement);
                XDocument xmlDoc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"), rootElement);
                xmlDoc.Save(metaStream);
            }
            catch (Exception)
            {
                MessageBox.Show("Nastala chyba při tvorbě metasouboru, oznamte to prosím autorovi programu.",
                        "Chybný metasoubor", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            UploadParameters param = new UploadParameters();
            param.Url = Settings.ImportLink;
            param.CoverStream = coverStream;
            param.TocStream = tocStream;
            param.MetaStream = metaStream;
            param.Nvc = nvc;
            this.uploaderBackgroundWorker.RunWorkerAsync(param);
            
            this.uploadWindow = new UploadWindow();
            this.uploadWindow.ShowDialog();
        }

        // Method for uploading multipart/form-data
        // url where will be data posted, login, password
        private void UploadFilesToRemoteUrl(string url, Stream coverStream, Stream tocStream,
            Stream metaStream, NameValueCollection nvc, DoWorkEventArgs e)
        {
            // Checks
            UpdateChecker updateChecker = new UpdateChecker();
            updateChecker.RetrieveUpdateInfo();
            if (!updateChecker.IsSupportedVersion)
            {
                throw new WebException("Používáte nepodporovanou verzi programu. Aktualizujte ho.",
                    WebExceptionStatus.ProtocolError);
            }

            string boundary = "----------------------------" + DateTime.Now.Ticks.ToString("x");
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.ContentType = "multipart/form-data; boundary=" + boundary;
            httpWebRequest.Method = "POST";
            httpWebRequest.ServicePoint.Expect100Continue = false;
            httpWebRequest.KeepAlive = false;

            boundary = "--" + boundary;

            Stream memStream = new System.IO.MemoryStream();

            var boundarybytes = Encoding.ASCII.GetBytes(boundary + Environment.NewLine);
            string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"" + Environment.NewLine + Environment.NewLine + "{1}" + Environment.NewLine;

            //write non-file parameters
            foreach (string key in nvc.Keys)
            {
                string formitem = string.Format(formdataTemplate, key, nvc[key]);
                byte[] formitembytes = System.Text.Encoding.UTF8.GetBytes(formitem);
                memStream.Write(boundarybytes, 0, boundarybytes.Length);
                memStream.Write(formitembytes, 0, formitembytes.Length);
            }

            string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"" + Environment.NewLine + "Content-Type: image/tif" + Environment.NewLine + Environment.NewLine;
            string headerTemplate2 = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"" + Environment.NewLine + "Content-Type: text/plain" + Environment.NewLine + Environment.NewLine;
            
            string header;
            byte[] newLineBytes = Encoding.ASCII.GetBytes(Environment.NewLine);
            byte[] headerbytes;
            string name;
            string fileName;

            //cover
            if (coverStream != null)
            {
                fileName = "cover.tif";
                name = "cover";

                header = string.Format(headerTemplate, name, fileName);

                headerbytes = Encoding.UTF8.GetBytes(header);
                

                memStream.Write(boundarybytes, 0, boundarybytes.Length);
                memStream.Write(headerbytes, 0, headerbytes.Length);
                coverStream.Position = 0;
                coverStream.CopyTo(memStream);
                memStream.Write(newLineBytes, 0, newLineBytes.Length);
                coverStream.Close();
            }
            //toc
            if (tocStream != null)
            {
                fileName = "toc_page_1.tif";
                name = "toc_page_1";

                header = string.Format(headerTemplate, name, fileName);
                headerbytes = Encoding.UTF8.GetBytes(header);

                memStream.Write(boundarybytes, 0, boundarybytes.Length);
                memStream.Write(headerbytes, 0, headerbytes.Length);
                tocStream.Position = 0;
                tocStream.CopyTo(memStream);
                memStream.Write(newLineBytes, 0, newLineBytes.Length);
                tocStream.Close();
            }

            //meta
            if (metaStream != null)
            {
                fileName = "meta.xml";
                name = "meta";

                header = string.Format(headerTemplate2, name, fileName);

                headerbytes = Encoding.UTF8.GetBytes(header);

                memStream.Write(boundarybytes, 0, boundarybytes.Length);
                memStream.Write(headerbytes, 0, headerbytes.Length);
                metaStream.Position = 0;
                metaStream.CopyTo(memStream);
                memStream.Write(newLineBytes, 0, newLineBytes.Length);
                metaStream.Close();
            }

            //end request
            var boundaryBuffer = Encoding.ASCII.GetBytes(boundary + "--");
            memStream.Write(boundaryBuffer, 0, boundaryBuffer.Length);

            httpWebRequest.ContentLength = memStream.Length;

            Stream requestStream = httpWebRequest.GetRequestStream();

            memStream.Position = 0;
            byte[] tempBuffer = new byte[memStream.Length];
            memStream.Read(tempBuffer, 0, tempBuffer.Length);
            memStream.Close();
            requestStream.Write(tempBuffer, 0, tempBuffer.Length);
            requestStream.Close();

            WebResponse webResponse = null;
            try
            {
                //timeout 10 minutes
                httpWebRequest.Timeout = 600000;
                webResponse = httpWebRequest.GetResponse();
            }
            catch (Exception)
            {
                if (webResponse != null)
                {
                    webResponse.Close();
                }
                requestStream.Close();
                throw;
            }
            Stream stream2 = webResponse.GetResponseStream();
            StreamReader reader2 = new StreamReader(stream2);
            e.Result = reader2.ReadToEnd();
            stream2.Close();
            webResponse.Close();
            httpWebRequest = null;
            webResponse = null;
        }

        // Uploads files to obalkyknih in new thread
        private void UploaderBW_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            UploadParameters up = e.Argument as UploadParameters;
            UploadFilesToRemoteUrl(up.Url, up.CoverStream, up.TocStream, up.MetaStream, up.Nvc, e);
        }

        // Shows result of uploading process (OK or error message)
        private void UploaderBW_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
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
        #endregion

        #region scanning functionality

        // Scans image
        private void ScanImage(bool isCover)
        {
            ICommonDialog dialog = new CommonDialog();

            //try to set active scanner
            if (!setActiveScanner())
            {
                return;
            }

            int dpi = isCover ? Settings.CoverDPI : Settings.TocDPI;

            Item item = activeScanner.Items[1];

            //Setting configuration of scanner (dpi, color)
            Object value;
            foreach (IProperty property in item.Properties)
            {
                switch (property.PropertyID)
                {
                    case 6146: //4 is Black-white,gray is 2, color 1
                        value = isCover ? Settings.CoverScanType : Settings.TocScanType;
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

            //We will work with in-memory images
            MemoryStream ms = new MemoryStream((byte[])image.FileData.get_BinaryData());
            BitmapImage bmpImg = new BitmapImage();
            bmpImg.BeginInit();
            bmpImg.CacheOption = BitmapCacheOption.OnLoad;
            bmpImg.StreamSource = new MemoryStream(ms.ToArray());
            bmpImg.EndInit();

            
            //insert to GUI
            if (isCover)
            {
                AddCoverImage(bmpImg);
            }
            else
            {
                AddTocImage(bmpImg);
            }
        }

        // Sets active scanner device automatically, show selection dialog, if more scanners
        // if no scanner device found, shows error window and returns false 
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

        // Loads new cover image
        private void AddCoverImage(BitmapSource bitmapSource)
        {
            // enable controlers for image manipulation
            this.isSelectedCoverImage = true;

            this.coverRotateLeft.Cursor = Cursors.Hand;
            this.coverRotateRight.Cursor = Cursors.Hand;
            this.coverRotate180.Cursor = Cursors.Hand;
            this.coverVerticalFlip.Cursor = Cursors.Hand;
            this.coverHorizontalFlip.Cursor = Cursors.Hand;

            this.coverCropButton.IsEnabled = true;
            this.coverBrightnessSlider.IsEnabled = true;
            this.coverContrastSlider.IsEnabled = true;
            this.coverEditButton.IsEnabled = true;
            this.coverBrightnessSlider.Value = 0;
            this.coverContrastSlider.Value = 0;

            // add bitmapSource to images
            this.coverImageThumbnail.Source = bitmapSource;
            this.selectedCoverImage.Source = bitmapSource;
            this.confirmCoverImage.Source = bitmapSource;

            // set crop
            ImageTools.AddCropToElement(this.selectedCoverImage, ref this.coverCropper);
        }

        // Adds new TOC image to list of TOC images
        private void AddTocImage(BitmapSource bitmapSource)
        {
            //enable controlers for image manipulation
            this.isSelectedTocImage = true;

            this.tocRotateLeft.Cursor = Cursors.Hand;
            this.tocRotateRight.Cursor = Cursors.Hand;
            this.tocRotate180.Cursor = Cursors.Hand;
            this.tocVerticalFlip.Cursor = Cursors.Hand;
            this.tocHorizontalFlip.Cursor = Cursors.Hand;

            this.tocCropButton.IsEnabled = true;
            this.tocBrightnessSlider.IsEnabled = true;
            this.tocContrastSlider.IsEnabled = true;
            this.tocEditButton.IsEnabled = true;
            this.tocBrightnessSlider.Value = 0;
            this.tocContrastSlider.Value = 0;

            Image img = new Image();
            img.MouseLeftButtonDown += tocImage_MouseLeftButtonDown;
            img.Margin = new System.Windows.Thickness(15, 5, 0, 5);
            img.Source = bitmapSource;
            img.Cursor = Cursors.Hand;
            CheckBox checkBox = new CheckBox();
            Grid gridWrapper = new Grid();
            gridWrapper.Children.Add(checkBox);
            gridWrapper.Children.Add(img);
            checkBox.Checked += new RoutedEventHandler(TocCheckBox_Checked);
            checkBox.Unchecked += new RoutedEventHandler(TocCheckBox_Unchecked);
            checkBox.VerticalAlignment = VerticalAlignment.Center;
            checkBox.IsChecked = true;
            this.tocImagesList.Items.Add(gridWrapper);

            this.selectedTocImage.Source = img.Source;
            this.selectedTocImageThumbnail = img;
            ImageTools.AddCropToElement(this.selectedTocImage, ref this.tocCropper);
        }

        // Includes particular TOC image in result
        private void TocCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = e.Source as CheckBox;
            Grid grid = cb.Parent as Grid;
            foreach (var element in grid.Children)
            {
                if (element is Image)
                {
                    Image img = new Image();
                    img.Source = (element as Image).Source;
                    img.Margin = new Thickness(0,5,0,0);
                    this.confirmTocImagesList.Items.Add(img);
                    this.ocrCheckBox.IsChecked = true;
                }
            }
        }

        // Excludes particular TOC image from result
        private void TocCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            Grid grid = ((e.Source as CheckBox).Parent as Grid);
            foreach (var element in grid.Children)
            {
                if (element is Image)
                {
                    ImageSource src = (element as Image).Source;
                    object itemToRemove = null;
                    foreach (var item in this.confirmTocImagesList.Items)
                    {
                        if (item is Image && ((Image)item).Source.Equals(src))
                        {
                            itemToRemove = item;
                        }
                    }
                    if (itemToRemove != null)
                    {
                        this.confirmTocImagesList.Items.Remove(itemToRemove);
                    }
                }
            }
            if(this.confirmTocImagesList.Items.Count == 0)
            {
                this.ocrCheckBox.IsChecked = false;
            }
        }

        // Sets new BitmapSource for toc image in confirmation page with same oldSource BitmapSource
        private void SetConfirmationTocNewImageSource(ImageSource oldSource, ImageSource newSource)
        {
            foreach (var item in this.confirmTocImagesList.Items)
            {
                if (item is Image && ((Image)item).Source.Equals(oldSource))
                {
                    (item as Image).Source = newSource;
                }
            }
        }

        // Loads image from external file
        private void LoadExternalImage(bool isCover, string fileName)
        {
            BitmapSource loadedBmpFrame = null;
            string extension = fileName.Substring(fileName.LastIndexOf('.')+1).ToLower();
            BitmapDecoder decoder = null;
            try
            {
                switch (extension)
                {
                    case "jpg":
                    case "jpeg":
                        decoder = new JpegBitmapDecoder(new Uri(fileName), BitmapCreateOptions.PreservePixelFormat,
                            BitmapCacheOption.Default);
                        break;
                    case "png":
                        decoder = new PngBitmapDecoder(new Uri(fileName), BitmapCreateOptions.PreservePixelFormat,
                            BitmapCacheOption.Default);
                        break;
                    case "bmp":
                        decoder = new BmpBitmapDecoder(new Uri(fileName), BitmapCreateOptions.PreservePixelFormat,
                            BitmapCacheOption.Default);
                        break;
                    case "tif":
                    case "tiff":
                        decoder = new TiffBitmapDecoder(new Uri(fileName), BitmapCreateOptions.PreservePixelFormat,
                            BitmapCacheOption.Default);
                        break;
                    case "gif":
                        decoder = new GifBitmapDecoder(new Uri(fileName), BitmapCreateOptions.PreservePixelFormat,
                            BitmapCacheOption.Default);
                        break;
                    case "wmp":
                        decoder = new WmpBitmapDecoder(new Uri(fileName), BitmapCreateOptions.PreservePixelFormat,
                            BitmapCacheOption.Default);
                        break;
                }
            }
            catch (System.IO.FileFormatException)
            {
                MessageBox.Show("Zvolený obrázek není možné načíst", "Chyba načítání obrázku", MessageBoxButton.OK,MessageBoxImage.Error);
                return;
            }
            if (decoder != null)
            {
                loadedBmpFrame = decoder.Frames[0];
                if (isCover)
                {
                    AddCoverImage(loadedBmpFrame);

                }
                else
                {
                    AddTocImage(loadedBmpFrame);
                }
            }
        }        
        #endregion
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
}
