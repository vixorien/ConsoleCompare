using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace ConsoleCompare
{
	/// <summary>
	/// Classifier provider. It adds the classifier to the set of classifiers.
	/// </summary>
	[Export(typeof(IClassifierProvider))]
	[ContentType("simile")] // This classifier applies to all text files.
	internal class SimileClassifierProvider : IClassifierProvider
	{

		internal readonly ITableManager errorTableManager;

		[ImportingConstructor]
		internal SimileClassifierProvider([Import]ITableManagerProvider tableManagerProvider)
		{
			errorTableManager = tableManagerProvider.GetTableManager(StandardTables.ErrorsTable);

			// Ref: https://github.com/madskristensen/WebAccessibilityChecker/tree/master/src/ErrorList
			// Before we can add a source, this class needs to implement ITableDataSource - This class can hold all of the "error snapshots"
			// Then we need a sinkmanager?  Which will hold (but not implement) an ITableDataSink
			// Then we need something that implements WpfTableEntriesSnapshotBase to act as our "error snapshots"
			// And then...?

			//errorTableManager.AddSource(
			//	this,
			//	StandardTableColumnDefinitions.Line,
			//	StandardTableColumnDefinitions.Text,
			//	StandardTableColumnDefinitions.DocumentName);

		}

		// Disable "Field is never assigned to..." compiler's warning. Justification: the field is assigned by MEF.
#pragma warning disable 649

		/// <summary>
		/// Classification registry to be used for getting a reference
		/// to the custom classification type later.
		/// </summary>
		[Import]
		private IClassificationTypeRegistryService classificationRegistry;

#pragma warning restore 649

		#region IClassifierProvider

		/// <summary>
		/// Gets a classifier for the given text buffer.
		/// </summary>
		/// <param name="buffer">The <see cref="ITextBuffer"/> to classify.</param>
		/// <returns>A classifier for the text buffer, or null if the provider cannot do so in its current state.</returns>
		public IClassifier GetClassifier(ITextBuffer buffer)
		{
			return buffer.Properties.GetOrCreateSingletonProperty<SimileClassifier>(creator: () => new SimileClassifier(this.classificationRegistry, this.errorTableManager));
		}

		#endregion
	}
}
