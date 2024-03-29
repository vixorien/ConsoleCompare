﻿using EnvDTE;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;

namespace ConsoleCompare
{
	/// <summary>
	/// Holds results of a comment check of the current solution
	/// </summary>
	internal class CommentCheckResults
	{
		/// <summary>
		/// Gets or sets the total number of projects
		/// </summary>
		public int ProjectCount { get; set; }
		
		public int ClassCount { get; set; }
		public int ClassXMLCommentCount { get; set; }
		public int ClassRegularCommentCount { get; set; }

		public int MethodCount { get; set; }
		public int MethodXMLCommentCount { get; set; }
		public int MethodRegularCommentCount { get; set; }

		public int PropertyCount { get; set; }
		public int PropertyXMLCommentCount { get; set; }
		public int PropertyRegularCommentCount { get; set; }


		/// <summary>
		/// Gets the total number of classes, methods and properties
		/// </summary>
		public int ExpectedXMLCommentTotal => 
			ClassCount + 
			MethodCount + 
			PropertyCount;

		/// <summary>
		/// Gets the total number of XML comments on classes, methods and properties
		/// </summary>
		public int XMLCommentTotal => 
			ClassXMLCommentCount + 
			MethodXMLCommentCount +
			PropertyXMLCommentCount;
		
		/// <summary>
		/// Gets the total number of regular (non-XML) comments on classes, methods and properties
		/// </summary>
		public int RegularCommentTotal =>
			ClassRegularCommentCount +
			MethodRegularCommentCount +
			PropertyRegularCommentCount;

		/// <summary>
		/// Gets whether or not all classes, methods and properties have XML comments
		/// </summary>
		public bool HasAllXMLComments =>
			ClassCount == ClassXMLCommentCount &&
			MethodCount == MethodXMLCommentCount &&
			PropertyCount == PropertyXMLCommentCount;

		/// <summary>
		/// Gets a summary of XML comment results
		/// </summary>
		/// <returns>A string with XML comment count summary</returns>
		public string GetSummary()
		{
			return $"{XMLCommentTotal}/{ExpectedXMLCommentTotal} XML comment headers found";
		}

		/// <summary>
		/// Gets a detailed string of XML comments and, if necessary, regular
		/// (non-XML) comments found
		/// </summary>
		/// <returns>A string with XML and non-XML details</returns>
		public string GetDetails()
		{
			string results = "XML comment headers found:\n" +
				$" Classes: {ClassXMLCommentCount}/{ClassCount}\n" +
				$" Methods: {MethodXMLCommentCount}/{MethodCount}\n" +
				$" Properties: {PropertyXMLCommentCount}/{PropertyCount}";

			// Any regular comments that should be XML?
			if (RegularCommentTotal > 0)
			{
				results += "\n\n" +
					"Regular (non-XML) comment headers found:\n" +
					$" Classes: {ClassRegularCommentCount}\n" +
					$" Methods: {MethodRegularCommentCount}\n" +
					$" Properties: {PropertyRegularCommentCount}";
			}

			return results;
		}


		/// <summary>
		/// Returns a string detailing the count of XML comments on classes, methods and properties
		/// </summary>
		/// <returns>A string with XML comment count details</returns>
		public override string ToString()
		{
			return GetDetails();
		}
	}

	/// <summary>
	/// Static class helper for checking a solution's comments
	/// </summary>
	internal static class CommentChecker
	{
		/// <summary>
		/// Scans the current solution for code elements and the
		/// status of their comments (XML, regular or none)
		/// </summary>
		/// <param name="window">The window for reporting results, if any</param>
		/// <returns>Object containing info on code elements and comment counts</returns>
		public static CommentCheckResults ScanForComments(ResultsWindow window)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Grab the global DTE
			DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;

			// Create the overall result object
			CommentCheckResults results = new CommentCheckResults();
			results.ProjectCount = dte.Solution.Projects.Count;

			// Scan each project
			foreach (Project proj in dte.Solution.Projects)
			{
				// Go through all project items looking for code files
				foreach (ProjectItem item in proj.ProjectItems)
				{
					// Skip files that are not code
					if (item.FileCodeModel == null)
						continue;

					// Check each code element in the file - at this level
					// these are probably namespaces, which have classes as children
					// so this becomes a recursive walk of a tree
					foreach (CodeElement element in item.FileCodeModel.CodeElements)
					{
						WalkCodeTree(element, results);
					}
				}
			}

			// Are we reporting results?
			if (window != null)
			{
				window.SetCommentStatus(
					results.GetSummary(),
					results.HasAllXMLComments ? KnownMonikers.StatusOK : KnownMonikers.Uncomment,
					results.GetDetails());
			}

			return results;
		}

		/// <summary>
		/// Recursively checks an element and its children for code elements
		/// and their comment states
		/// </summary>
		/// <param name="element">The element to check</param>
		/// <param name="results">The result object to collect data</param>
		private static void WalkCodeTree(CodeElement element, CommentCheckResults results)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Verify the element is actually from the project (and not external)
			if (element.InfoLocation != vsCMInfoLocation.vsCMInfoLocationProject)
				return;

			// Determine the type of element
			switch (element.Kind)
			{
				case vsCMElement.vsCMElementClass:
	
					// Cast as class element to get details
					if (element is CodeClass cl)
					{
						results.ClassCount++;
						if (!string.IsNullOrEmpty(cl.DocComment)) results.ClassXMLCommentCount++;
						if (!string.IsNullOrEmpty(cl.Comment)) results.ClassRegularCommentCount++;
					}

					break;

				case vsCMElement.vsCMElementFunction:

					// Cast as function element to get details
					if (element is CodeFunction method)
					{
						results.MethodCount++;
						if (!string.IsNullOrEmpty(method.DocComment)) results.MethodXMLCommentCount++;
						if (!string.IsNullOrEmpty(method.Comment)) results.MethodRegularCommentCount++;
					}

					break;

				case vsCMElement.vsCMElementProperty:

					// Cast as property element to get details
					if (element is CodeProperty prop)
					{
						results.PropertyCount++;
						if (!string.IsNullOrEmpty(prop.DocComment)) results.PropertyXMLCommentCount++;
						if (!string.IsNullOrEmpty(prop.Comment)) results.PropertyRegularCommentCount++;
					}

					break;
			}

			// Recursively check children
			foreach (CodeElement child in element.Children)
				WalkCodeTree(child, results);
		}
	}
}
