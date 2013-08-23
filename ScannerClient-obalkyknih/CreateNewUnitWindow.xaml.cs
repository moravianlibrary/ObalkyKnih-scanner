using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;

namespace ScannerClient_obalkyknih
{
    /// <summary>
    /// Interaction logic for CreateNewUnitWindow.xaml
    /// </summary>
    public partial class CreateNewUnitWindow : Window
    {
        // close on escape
        public static RoutedCommand closeCommand = new RoutedCommand();

        // parent element of TabsControl element
        private DockPanel parentOfTabsControl;

        /// <summary>
        /// Constructor, creates new popup window for inserting of barcode,
        /// after confirmation new tabsControl wil be inserted to main window
        /// </summary>
        /// <param name="parentOfTabsControl">parent element,
        /// where should be inserted tabsControl</param>
        public CreateNewUnitWindow(DockPanel parentOfTabsControl)
        {
            this.parentOfTabsControl = parentOfTabsControl;
            InitializeComponent();
            this.barcodeTextBox.Focus();

            // close on Esc
            CommandBinding cb = new CommandBinding(closeCommand, CloseExecuted, CloseCanExecute);
            this.CommandBindings.Add(cb);
            KeyGesture kg = new KeyGesture(Key.Escape);
            InputBinding ib = new InputBinding(closeCommand, kg);
            this.InputBindings.Add(ib);
        }

        // Handles clicking on button, executes ShowUnitTabsControl
        private void NewUnitButton_Click(object sender, RoutedEventArgs e)
        {
            ShowNewUnitTabsControl(this.barcodeTextBox.Text);
        }

        // Handles KeyDown event, so it can execute ShowUnitTabsControl by pressing Enter key in textbox
        private void BarcodeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ShowNewUnitTabsControl(this.barcodeTextBox.Text);
            }
        }

        // shows tabsControl for the unit with entered barcode
        private void ShowNewUnitTabsControl(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                //show error message, if barcode was not entered
                MessageBox.Show("Zadejte čárový kód.", "Chyba!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            List<UIElement> childrenList = new List<UIElement>();
            //get all children of parent of remote window
            foreach (UIElement item in parentOfTabsControl.Children)
            {
                //don't add Menu item
                if (item is Menu || item is StatusBar)
                {
                    continue;
                }
                childrenList.Add(item);
            }
            //remove all these children from remote window
            foreach (UIElement item in childrenList)
            {
                parentOfTabsControl.Children.Remove(item);
            }
            //put there new tabsControl with appropriate barcode
            parentOfTabsControl.Children.Add(new TabsControl(barcode));

            //close the window, if it was called from new window
            Window parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.Close();
            }
            (Window.GetWindow(parentOfTabsControl)as MainWindow).AddMessageToStatusBar("Stahuji metadata.");
        }

        // Close on Esc
        private void CloseCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        // Close on Esc
        private void CloseExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            this.Close();
        }
    }
}
