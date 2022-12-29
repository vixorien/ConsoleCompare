using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace ConsoleCompare
{
	/// <summary>
	/// Defines an editor format for the SimileClassifier type that has a purple background
	/// and is underlined.
	/// </summary>
	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = "SimileClassifier")]
	[Name("SimileClassifier")]
	[UserVisible(true)] // This should be visible to the end user
	[Order(Before = Priority.Default)] // Set the priority to be after the default classifiers
	internal sealed class SimileClassifierFormat : ClassificationFormatDefinition
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SimileClassifierFormat"/> class.
		/// </summary>
		public SimileClassifierFormat()
		{
			this.DisplayName = "SimileClassifier"; // Human readable version of the name
			this.BackgroundColor = Colors.Green;
			this.TextDecorations = System.Windows.TextDecorations.Underline;
		}
	}
}
