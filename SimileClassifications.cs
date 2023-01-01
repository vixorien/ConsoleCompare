using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Media;

namespace ConsoleCompare
{
	internal static class SimileClassifications
	{
		// Color settings
		// Reference for colors: https://learn.microsoft.com/en-us/dotnet/media/art-color-table.png
		internal static Color ErrorBackgroundColor = Colors.Firebrick;
		internal static Color CommentForegroundColor = Colors.ForestGreen;
		internal static Color InputTagBackgroundColor = Colors.DodgerBlue;
		internal static Color NumericTagBackgroundColor = Colors.DarkGreen;

		// Strings that refer to the possible classifications
		internal const string SimileErrorClassifier = "SimileError";
		internal const string SimileCommentClassifier = "SimileComment";
		internal const string SimileInputTagClassifier = "SimileInputTag";
		internal const string SimileNumericTagClassifier = "SimileNumericTag";

		// Exports for the types for Visual Studio
		// Note: Disabling warning for never being assigned as they're assigned by the extensibility framework (MEF)
#pragma warning disable 169
		[Export(typeof(ClassificationTypeDefinition))]
		[Name(SimileErrorClassifier)]
		private static ClassificationTypeDefinition SimileErrorType;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(SimileCommentClassifier)]
		private static ClassificationTypeDefinition SimileCommentType;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(SimileInputTagClassifier)]
		private static ClassificationTypeDefinition SimileInputTagType;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(SimileNumericTagClassifier)]
		private static ClassificationTypeDefinition SimileNumericTagType;
#pragma warning restore 169

		/// <summary>
		/// Defines an editor format for errors in a simile
		/// </summary>
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = SimileErrorClassifier)]
		[Name(SimileErrorClassifier)]
		[UserVisible(true)] // This should be visible to the end user
		[Order(Before = Priority.Default)]
		internal sealed class SimileErrorFormatDefinition : ClassificationFormatDefinition
		{
			public SimileErrorFormatDefinition()
			{
				this.DisplayName = "Simile Error"; // Human readable version of the name
				this.BackgroundColor = ErrorBackgroundColor;

				// Red dashed underline example
				//TextDecoration redUnderline = new TextDecoration();
				//redUnderline.Pen = new Pen(Brushes.Red, 1);
				//redUnderline.Pen.DashStyle = DashStyles.Dash;
				//redUnderline.PenThicknessUnit = TextDecorationUnit.FontRecommended;
				//this.TextDecorations = new TextDecorationCollection();
				//this.TextDecorations.Add(redUnderline);
			}
		}


		/// <summary>
		/// Defines an editor format for comments in a simile
		/// </summary>
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = SimileCommentClassifier)]
		[Name(SimileCommentClassifier)]
		[UserVisible(true)] // This should be visible to the end user
		[Order(Before = Priority.Default)] 
		internal sealed class SimileCommentFormatDefinition : ClassificationFormatDefinition
		{
			public SimileCommentFormatDefinition()
			{
				this.DisplayName = "Simile Comment"; // Human readable version of the name
				this.ForegroundColor = CommentForegroundColor;
			}
		}


		/// <summary>
		/// Defines an editor format for numeric tags in a simile
		/// </summary>
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = SimileNumericTagClassifier)]
		[Name(SimileNumericTagClassifier)]
		[UserVisible(true)] // This should be visible to the end user
		[Order(Before = Priority.Default)]
		internal sealed class SimileNumericTagFormatDefinition : ClassificationFormatDefinition
		{
			public SimileNumericTagFormatDefinition()
			{
				this.DisplayName = "Simile Numeric Tag"; // Human readable version of the name
				this.BackgroundColor = NumericTagBackgroundColor;
			}
		}

		/// <summary>
		/// Defines an editor format for input tags in a simile
		/// </summary>
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = SimileInputTagClassifier)]
		[Name(SimileInputTagClassifier)]
		[UserVisible(true)] // This should be visible to the end user
		[Order(Before = Priority.Default)]
		internal sealed class SimileInputTagFormatDefinition : ClassificationFormatDefinition
		{
			public SimileInputTagFormatDefinition()
			{
				this.DisplayName = "Simile Input Tag"; // Human readable version of the name
				this.BackgroundColor = InputTagBackgroundColor;
				this.IsItalic = true;
			}
		}

	
	}
}
