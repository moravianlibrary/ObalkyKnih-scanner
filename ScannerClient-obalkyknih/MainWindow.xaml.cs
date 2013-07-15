using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;


namespace ScannerClient_obalkyknih
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region key bindings commands
        public static RoutedCommand newUnitCommand = new RoutedCommand();
        public static RoutedCommand closeCommand = new RoutedCommand();
        #endregion

        //downloads info about update on background
        private BackgroundWorker updateInfoBackgroundWorker = new BackgroundWorker();
        //checks info about update and supported versions
        private UpdateChecker updateChecker = new UpdateChecker();
        private bool showIsLatestVersionPopup = false;
        //signals is this version is supported
        private bool? isAllowedVersion = null;
        //client for downloading of update
        private WebClient webClient = new WebClient();
        //force shutdown - do not show confirmation message
        private bool forceShutDown = false;
       
        /// <summary>
        /// constructor - initializes components and key bindings
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            InitializeBackgroundWorkers();
            //only here are settings loaded from file
            Settings.ReloadSettings();
            this.webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadFileCompleted);
            this.webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressChanged);
            
            this.programVersionLabel.Content = this.updateChecker.Version.Major.ToString()
                + "." + this.updateChecker.Version.Minor.ToString();

            this.versionStateLabel.Foreground = Brushes.Black;
            this.versionStateLabel.Content = "Získávání informací";
            this.updateInfoBackgroundWorker.RunWorkerAsync();
            
            #region commandBindings

            //new - Ctrl + N
            CommandBinding cb = new CommandBinding(newUnitCommand, NewUnitExecuted, NewUnitCanExecute);
            this.CommandBindings.Add(cb);
            KeyGesture kg = new KeyGesture(Key.N, ModifierKeys.Control);
            InputBinding ib = new InputBinding(newUnitCommand, kg);
            this.InputBindings.Add(ib);

            //close - Ctrl + X
            cb = new CommandBinding(closeCommand, CloseExecuted, CloseCanExecute);
            this.CommandBindings.Add(cb);
            kg = new KeyGesture(Key.X, ModifierKeys.Control);
            ib = new InputBinding(closeCommand, kg);
            this.InputBindings.Add(ib);
            #endregion
        }

        // Downloads update
        private void DownloadUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadUpdateFile();
        }

        #region menu items
        
        // Shows CreateNewUnitWindows
        private void NewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowNewUnitWindow();
        }
        
        // Download update-info from update server
        private void UpdateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (this.updateInfoBackgroundWorker.IsBusy)
            {
                return;
            }
            this.showIsLatestVersionPopup = true;
            this.updateInfoBackgroundWorker.RunWorkerAsync();
        }

        // Shows confirmation dialog and exits application
        private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Shows CredentialsWindow
        private void CredentialsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Window credentialsWindow = new CredentialsWindow();
            credentialsWindow.ShowDialog();
        }

        // Shows SettingsWindow
        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Window settingsWindows = new SettingsWindow();
            settingsWindows.ShowDialog();
        }

        //Shows AboutWindow
        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Window aboutWindow = new AboutWindow();
            aboutWindow.ShowDialog();
        }

        // Shows HelpWindow
        private void HelpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Window helpWindow = new HelpWindow();
            helpWindow.Show();
        }
        #endregion

        #region command bindings methods

        //Ctrl+N  - Shows CreateNewUnitWindow
        private void NewUnitCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        //Ctrl+N  - Shows CreateNewUnitWindow
        private void NewUnitExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            ShowNewUnitWindow();
        }


        //Ctrl+X - Shows confirmation messageBox and exits application
        private void CloseCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        //Ctrl+X - Shows confirmation messageBox and exits application
        private void CloseExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            this.Close();
        }
        #endregion

        #region functionality

        /// <summary>
        /// Adds message to StatusBar
        /// </summary>
        /// <param name="text">Message that will be added to StatusBar</param>
        public void AddMessageToStatusBar(string text)
        {
            this.metadataDownloadTextBox.Text += text + " ";
            if (!string.IsNullOrWhiteSpace(this.metadataDownloadTextBox.Text))
            {
                this.metadataDownloadTextBox.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Removes message from StatusBar if it contains this message
        /// </summary>
        /// <param name="text">message that will be removed from StatusBar</param>
        public void RemoveMessageFromStatusBar(string text)
        {
            if (!string.IsNullOrEmpty(this.metadataDownloadTextBox.Text))
            {
                this.metadataDownloadTextBox.Text =
                    this.metadataDownloadTextBox.Text.Replace(text + " ", "");
            }
            // check if new string is empty
            if (string.IsNullOrWhiteSpace(this.metadataDownloadTextBox.Text))
            {
                this.metadataDownloadTextBox.Visibility = Visibility.Collapsed;
            }
        }

        // Shows confirmation message and shutdown application
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (forceShutDown)
            {
                return;
            }

            if (MessageBox.Show("Opravdu chcete ukončit program?",
                "Potvrzení", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
            else
            {
                e.Cancel = true;
            }
        }


        // Opens CreateNewUnitWindow
        private void ShowNewUnitWindow()
        {
            if (isAllowedVersion == null)
            {
                MessageBox.Show("Kontrola verze zatím neskončila, počkejte prosím.", "Kontrola verze", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (isAllowedVersion == true)
            {
                Window newWindow = new CreateNewUnitWindow(this.dockPanel);
                newWindow.ShowDialog();
            }
            else
            {
                MessageBox.Show("Tato verze programu není podporována, prosím nainstalujte aktualizaci.", "Nepodporovaná verze", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Action after clicking on progress bar or label in StatusBar, starts update if downloaded
        private void UpdateDownload_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ShutDownAndStartUpdate();
            }
        }

        //Shows confirmation dialog, starts update and exits application
        private void ShutDownAndStartUpdate()
        {
            if (this.updateDownloadProgressBar.Value == 100)
            {
                var result = MessageBox.Show("Aktualizace byla stažena, přejete si nyní ukončit program a aktualizovat?",
                "Aktualizace stažena", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        //start update in new process
                        Process updateProcess = new Process();
                        updateProcess.StartInfo.FileName = this.updateChecker.FilePath;
                        updateProcess.StartInfo.UseShellExecute = false;
                        updateProcess.Start();

                        //close application
                        this.forceShutDown = true;
                        Application.Current.Shutdown();
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Nepodařilo se spustit aktualizaci, ukončete program a spusťte ji ručně.",
                            "Aktualizace stažena", MessageBoxButton.YesNo, MessageBoxImage.Information);
                    }
                }
            }
            else
            {
                MessageBox.Show("Aktualizace zatím nebyla stažena.", "Stahování nekompletní", MessageBoxButton.YesNo, MessageBoxImage.Question);
            }
        }

        #region background downloads

        // Initializes updateInfoBackgroundWorker 
        private void InitializeBackgroundWorkers()
        {
            this.updateInfoBackgroundWorker.WorkerSupportsCancellation = false;
            this.updateInfoBackgroundWorker.WorkerReportsProgress = false;
            this.updateInfoBackgroundWorker.DoWork += new DoWorkEventHandler(UpdateInfoBackgroundWorker_DoWork);
            this.updateInfoBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(UpdateInfoBackgroundWorker_RunWorkerCompleted);
        }

        // Starts RetrieveInfo method of UpdateChecker in new thread
        private void UpdateInfoBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            updateChecker.RetrieveUpdateInfo();
        }

        // Complex actions after update-info was retrieved
        private void UpdateInfoBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                this.versionStateLabel.Content = "";
                if (e.Error.InnerException is FormatException)
                {
                    MessageBox.Show("Informace o aktualizaci mají nesprávný formát. Kontaktujte prosím administrátory servru obalkyknih.cz.",
                        "Chybné informace o aktualizaci", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    var result = MessageBox.Show("Při stahování informací o aktualizaci se vyskytla chyba. Chcete to zkusit znovu?",
                        "Chyba stahování informací o aktualizaci", MessageBoxButton.YesNo, MessageBoxImage.Error);
                    if (result == MessageBoxResult.Yes)
                    {
                        updateInfoBackgroundWorker.RunWorkerAsync();
                    }
                }
            }
            else if (!e.Cancelled)
            {
                this.versionStateLabel.Foreground = Brushes.Red;
                this.versionStateLabel.Content = "Verze není aktuální";
                this.latestVersionLabel.Content = this.updateChecker.AvailableVersion.Major.ToString()
                    + "." + this.updateChecker.AvailableVersion.Minor.ToString();
                this.latestDateLabel.Content = this.updateChecker.AvailableVersionDate;

                if (!updateChecker.IsSupportedVersion)
                {
                    this.programSupportLabel.Foreground = Brushes.Red;
                    this.programSupportLabel.Content = "Nepodporováno";
                    isAllowedVersion = false;
                    this.downloadUpdateButton.Visibility = Visibility.Visible;
                    var result = MessageBox.Show("Vaše verze programu je v seznamu nepodporovaných verzí a nelze s ní pracovat, musíte stáhnout aktualizaci. Chcete ji stáhnout nyní?",
                            "Nepodporovaná verze", MessageBoxButton.YesNo, MessageBoxImage.Error);
                    if (result == MessageBoxResult.Yes)
                    {
                        DownloadUpdateFile();
                    }
                }
                else
                {
                    this.programSupportLabel.Foreground = Brushes.Green;
                    this.programSupportLabel.Content = "Podporováno";
                    isAllowedVersion = true;
                    if (this.updateChecker.IsUpdateAvailable)
                    {
                        this.downloadUpdateButton.Visibility = Visibility.Visible;
                        var result = MessageBox.Show("Aktualizace je k dispozici, chcete ji stáhnout?",
                            "Aktualizace", MessageBoxButton.YesNo, MessageBoxImage.Error);
                        if (result == MessageBoxResult.Yes)
                        {
                            DownloadUpdateFile();
                        }
                    }
                    else
                    {
                        this.versionStateLabel.Foreground = Brushes.Green;
                        this.versionStateLabel.Content = "Verze je aktuální";
                        if (this.showIsLatestVersionPopup)
                        {
                            MessageBox.Show("Verze je aktuální",
                                "Aktualizace", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
        }

        // Downloads update file from link in DownloadLink property of UpdateChecker
        private void DownloadUpdateFile()
        {
            this.downloadUpdateButton.IsEnabled = false;
            if (this.webClient.IsBusy)
            {
                webClient.CancelAsync();
            }
            string downloadLink = this.updateChecker.DownloadLink;
            if (downloadLink != null)
            {
                this.updateDownloadTextBox.Visibility = Visibility.Visible;
                this.updateDownloadProgressBar.Value = 0;
                this.updateDownloadProgressBar.Visibility = Visibility.Visible;

                this.webClient.DownloadFileAsync(new Uri(downloadLink), this.updateChecker.FilePath);
            }
        }

        // Shows progress of downloading of update file
        private void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            this.updateDownloadProgressBar.Value = e.ProgressPercentage;
        }

        // Complex actions after update file was downloaded
        private void DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            this.downloadUpdateButton.IsEnabled = true;
            if (e.Error != null)
            {
                MessageBox.Show("Počas stahování aktualizace došlo k chybě. Zkuste spustit stahování znovu.",
                    "Chyba při stahování aktualizace", MessageBoxButton.OK, MessageBoxImage.Error);
                this.updateDownloadTextBox.Visibility = Visibility.Collapsed;
                this.updateDownloadProgressBar.Visibility = Visibility.Collapsed;
            }
            else if (e.Cancelled)
            {
                MessageBox.Show("Stahování bylo přerušeno.",
                    "Stahování přerušeno", MessageBoxButton.OK, MessageBoxImage.Information);
                this.updateDownloadTextBox.Visibility = Visibility.Collapsed;
                this.updateDownloadProgressBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                //check checksum
                try
                {
                    string checksum;
                    using (FileStream stream = File.OpenRead(this.updateChecker.FilePath))
                    {
                        SHA256Managed sha = new SHA256Managed();
                        byte[] checksumBytes = sha.ComputeHash(stream);
                        checksum = BitConverter.ToString(checksumBytes).Replace("-", String.Empty);
                    }

                    if (!this.updateChecker.Checksum.ToLower().Equals(checksum.ToLower()))
                    {
                        File.Delete(this.updateChecker.FilePath);
                        MessageBox.Show("Kontrolní součet stažené aktualizace se nezhoduje, soubor byl zmazán, stáhněte aktualizaci znovu.",
                        "Aktualizace poškozena", MessageBoxButton.OK, MessageBoxImage.Error);
                        this.updateDownloadProgressBar.Value = 0;
                        this.updateDownloadProgressBar.Visibility = Visibility.Collapsed;
                        this.updateDownloadTextBox.Visibility = Visibility.Collapsed;
                        return;
                    }
                }
                catch (Exception)
                {
                    MessageBox.Show("Při kontrole integrity aktualizace došlo k chybě, stáhněte aktualizaci znovu.",
                        "Chyba kontroly aktualizace", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.updateDownloadProgressBar.Value = 0;
                    this.updateDownloadProgressBar.Visibility = Visibility.Collapsed;
                    this.updateDownloadTextBox.Visibility = Visibility.Collapsed;
                    return;
                }

                ShutDownAndStartUpdate();
            }
        }
        #endregion
        #endregion
    }
}