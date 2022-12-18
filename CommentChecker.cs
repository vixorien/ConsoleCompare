using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleCompare
{
	/// <summary>
	/// Holds results of a comment check of the current solution
	/// </summary>
	internal class CommentCheckResults
	{
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


		public int ExpectedXMLCommentTotal => 
			ClassCount + 
			MethodCount + 
			PropertyCount;
		
		public int XMLCommentTotal => 
			ClassXMLCommentCount + 
			MethodXMLCommentCount +
			PropertyXMLCommentCount;
		
		public int RegularCommentTotal =>
			ClassRegularCommentCount +
			MethodRegularCommentCount +
			PropertyRegularCommentCount;


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
		/// <param name="dte">DTE object</param>
		/// <returns>Object containing info on code elements and comment counts</returns>
		public static CommentCheckResults ScanForComments(DTE dte)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

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
					CodeClass cl = element as CodeClass;
					if (cl != null)
					{
						results.ClassCount++;
						if (!string.IsNullOrEmpty(cl.DocComment)) results.ClassXMLCommentCount++;
						if (!string.IsNullOrEmpty(cl.Comment)) results.ClassRegularCommentCount++;
					}

					break;

				case vsCMElement.vsCMElementFunction:

					// Cast as function element to get details
					CodeFunction method = element as CodeFunction;
					if (method != null)
					{
						results.MethodCount++;
						if (!string.IsNullOrEmpty(method.DocComment)) results.MethodXMLCommentCount++;
						if (!string.IsNullOrEmpty(method.Comment)) results.MethodRegularCommentCount++;
					}

					break;

				case vsCMElement.vsCMElementProperty:

					// Cast as property element to get details
					CodeProperty prop = element as CodeProperty;
					if (prop != null)
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
