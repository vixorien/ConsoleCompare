using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Media;

namespace ConsoleCompare
{
	/// <summary>
	/// Defines all possible classifications of text within a simile file
	/// </summary>
	internal static class SimileClassifications
	{
		// Color settings
		// Reference for colors: https://learn.microsoft.com/en-us/dotnet/media/art-color-table.png
		internal static Color ErrorTagBackgroundColor = Colors.Firebrick;
		internal static Color InputTagBackgroundColor = Colors.DodgerBlue;
		internal static Color NumericTagBackgroundColor = Colors.DarkGreen;

		internal static Color CommentForegroundColor = Colors.ForestGreen;

		// Details for fancy underline error effect
		internal static SolidColorBrush ErrorLineBrush = Brushes.Firebrick;
		internal static double ErrorLineExtraOffset = 5.0;
		internal static double ErrorLineThickness = 2.0;

		internal static TextDecoration ErrorLineDecoration = new TextDecoration()
		{
			Location = TextDecorationLocation.Underline,
			PenOffset = ErrorLineExtraOffset,
			Pen = new Pen()
			{
				Brush = ErrorLineBrush,
				DashStyle = DashStyles.Dash,
				Thickness = ErrorLineThickness
			}
		};


		// Strings that refer to the possible classifications
		internal const string SimileCommentClassifier = "SimileComment"; 
		internal const string SimileTagErrorClassifier = "SimileTagError";
		internal const string SimileLineErrorClassifier = "SimileLineError";
		internal const string SimileInputTagClassifier = "SimileInputTag";
		internal const string SimileNumericTagClassifier = "SimileNumericTag";

		// Exports for the types for Visual Studio
		// Note: Disabling warning for never being assigned as they're assigned by the extensibility framework (MEF)
#pragma warning disable 169
		[Export(typeof(ClassificationTypeDefinition))]
		[Name(SimileCommentClassifier)]
		private static ClassificationTypeDefinition SimileCommentType;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(SimileTagErrorClassifier)]
		private static ClassificationTypeDefinition SimileTagErrorType;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(SimileLineErrorClassifier)]
		private static ClassificationTypeDefinition SimileLineErrorType;
		
		[Export(typeof(ClassificationTypeDefinition))]
		[Name(SimileInputTagClassifier)]
		private static ClassificationTypeDefinition SimileInputTagType;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(SimileNumericTagClassifier)]
		private static ClassificationTypeDefinition SimileNumericTagType;
#pragma warning restore 169


		/// <summary>
		/// Defines an editor format for comments in a simile
		/// </summary>
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = SimileCommentClassifier)]
		[Name(SimileCommentClassifier)]
		[UserVisible(true)]
		[Order(Before = Priority.Default)]
		internal sealed class SimileCommentFormatDefinition : ClassificationFormatDefinition
		{
			public SimileCommentFormatDefinition()
			{
				this.DisplayName = "Simile Comment";
				this.ForegroundColor = CommentForegroundColor;
			}
		}


		/// <summary>
		/// Defines an editor format for errors on an entire simile line
		/// </summary>
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = SimileLineErrorClassifier)]
		[Name(SimileLineErrorClassifier)]
		[UserVisible(true)]
		[Order(Before = Priority.Default)]
		internal sealed class SimileLineErrorFormatDefinition : ClassificationFormatDefinition
		{
			public SimileLineErrorFormatDefinition()
			{
				this.DisplayName = "Simile Line Error";

				// Set up the decoration for a fancy underline effect
				this.TextDecorations = new TextDecorationCollection() { ErrorLineDecoration };
			}
		}


		/// <summary>
		/// Defines an editor format for errors in a tag
		/// </summary>
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = SimileTagErrorClassifier)]
		[Name(SimileTagErrorClassifier)]
		[UserVisible(true)]
		[Order(Before = Priority.Default)]
		internal sealed class SimileTagErrorFormatDefinition : ClassificationFormatDefinition
		{
			public SimileTagErrorFormatDefinition()
			{
				this.DisplayName = "Simile Tag Error";
				this.BackgroundColor = ErrorTagBackgroundColor;
			}
		}


		/// <summary>
		/// Defines an editor format for numeric tags in a simile
		/// </summary>
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = SimileNumericTagClassifier)]
		[Name(SimileNumericTagClassifier)]
		[UserVisible(true)]
		[Order(Before = Priority.Default)]
		internal sealed class SimileNumericTagFormatDefinition : ClassificationFormatDefinition
		{
			public SimileNumericTagFormatDefinition()
			{
				this.DisplayName = "Simile Numeric Tag";
				this.BackgroundColor = NumericTagBackgroundColor;
			}
		}


		/// <summary>
		/// Defines an editor format for input tags in a simile
		/// </summary>
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = SimileInputTagClassifier)]
		[Name(SimileInputTagClassifier)]
		[UserVisible(true)]
		[Order(Before = Priority.Default)]
		internal sealed class SimileInputTagFormatDefinition : ClassificationFormatDefinition
		{
			public SimileInputTagFormatDefinition()
			{
				this.DisplayName = "Simile Input Tag";
				this.BackgroundColor = InputTagBackgroundColor;
				this.IsItalic = true;
			}
		}

	
	}
}
