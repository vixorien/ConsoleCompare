using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace ConsoleCompare
{
	/// <summary>
	/// Classification type definition export for SimileClassifier
	/// </summary>
	internal static class SimileClassifierClassificationDefinition
	{
		// This disables "The field is never used" compiler's warning. Justification: the field is used by MEF.
#pragma warning disable 169

		/// <summary>
		/// Defines the "SimileClassifier" classification type.
		/// </summary>
		[Export(typeof(ClassificationTypeDefinition))]
		[Name("SimileClassifier")]
		private static ClassificationTypeDefinition typeDefinition;

#pragma warning restore 169
	}
}
