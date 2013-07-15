using System;
using System.Windows;

namespace ScannerClient_obalkyknih
{
    /// <summary>
    /// Interaction logic for MetadataWindow.xaml
    /// </summary>
    public partial class MetadataWindow : Window
    {
        /// <summary>
        /// Initializes MetadataWindow with given data
        /// </summary>
        /// <param name="metadata">data to show in window</param>
        public MetadataWindow(Metadata metadata)
        {
            InitializeComponent();

            if (metadata == null)
            {
                return;
            }

            string textContent = "";

            if (metadata.Sysno != null)
            {
                textContent += "SYSNO\t" + metadata.Sysno + "\n";
            }

            foreach (var fixfield in metadata.FixedFields)
            {
                textContent += fixfield.Key + "\t" + fixfield.Value + "\n";
            }
            foreach (var varfield in metadata.VariableFields)
            {
                textContent += varfield.TagName + varfield.Indicator1 + varfield.Indicator2 + "\t";
                foreach (var sf in varfield.Subfields)
                {
                    textContent += " |" + sf.Key + " " + sf.Value;
                }
                textContent += "\n";
            }
            this.metadataLabel.Text = textContent;
        }
    }
}
