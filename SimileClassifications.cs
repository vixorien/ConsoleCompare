using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace ConsoleCompare
{
	internal static class SimileClassifications
	{
		// Color settings
		internal static Color NumericTagBackgroundColor = Colors.Green;
		internal static Color InputTagBackgroundColor = Colors.CornflowerBlue;
		internal static Color ErrorBackgroundColor = Colors.Firebrick;

		// Strings that refer to the possible classifications
		internal const string SimileErrorClassifier = "SimileError";
		internal const string SimileInputTagClassifier = "SimileInputTag";
		internal const string SimileNumericTagClassifier = "SimileNumericTag";

		// Exports for the types for Visual Studio
		// Note: Might need to disable warning 169
		[Export(typeof(ClassificationTypeDefinition))]
		[Name(SimileErrorClassifier)]
		private static ClassificationTypeDefinition SimileErrorType = null;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(SimileInputTagClassifier)]
		private static ClassificationTypeDefinition SimileInputTagType = null;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(SimileNumericTagClassifier)]
		private static ClassificationTypeDefinition SimileNumericTagType = null;


		/// <summary>
		/// Defines an editor format for numeric tags in a simile
		/// </summary>
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = SimileNumericTagClassifier)]
		[Name(SimileNumericTagClassifier)]
		[UserVisible(true)] // This should be visible to the end user
		[Order(Before = Priority.Default)] // Set the priority to be after the default classifiers
		internal sealed class SimileNumericTagFormatDefinition : ClassificationFormatDefinition
		{
			public SimileNumericTagFormatDefinition()
			{
				this.DisplayName = "Simile Numeric Tag"; // Human readable version of the name
				this.BackgroundColor = NumericTagBackgroundColor;
				this.TextDecorations = System.Windows.TextDecorations.Underline;
			}
		}

		/// <summary>
		/// Defines an editor format for input tags in a simile
		/// </summary>
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = SimileInputTagClassifier)]
		[Name(SimileInputTagClassifier)]
		[UserVisible(true)] // This should be visible to the end user
		[Order(Before = Priority.Default)] // Set the priority to be after the default classifiers
		internal sealed class SimileInputTagFormatDefinition : ClassificationFormatDefinition
		{
			public SimileInputTagFormatDefinition()
			{
				this.DisplayName = "Simile Input Tag"; // Human readable version of the name
				this.BackgroundColor = InputTagBackgroundColor;
				this.TextDecorations = System.Windows.TextDecorations.Underline;
			}
		}

		/// <summary>
		/// Defines an editor format for errors in a simile
		/// </summary>
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = SimileErrorClassifier)]
		[Name(SimileErrorClassifier)]
		[UserVisible(true)] // This should be visible to the end user
		[Order(Before = Priority.Default)] // Set the priority to be after the default classifiers
		internal sealed class SimileErrorFormatDefinition : ClassificationFormatDefinition
		{
			public SimileErrorFormatDefinition()
			{
				this.DisplayName = "Simile Error"; // Human readable version of the name
				this.BackgroundColor = ErrorBackgroundColor;
				this.TextDecorations = System.Windows.TextDecorations.Underline;
			}
		}
	}
}
