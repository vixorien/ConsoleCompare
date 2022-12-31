
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleCompare
{
	internal class SimileError
	{
		// Add error details here
		public string Text { get; set; }
		public string DocumentName { get; set; }
		public int LineNumber { get; set; }
	}

	internal class SimileErrorSnapshot : WpfTableEntriesSnapshotBase
	{
		private List<SimileError> errors;

		public override int Count => errors.Count;

		public override int VersionNumber => 1;

		public SimileErrorSnapshot(SimileError error)
		{
			errors = new List<SimileError>();
			errors.Add(error);
		}

		public override bool TryGetValue(int index, string keyName, out object content)
		{
			// Initialize content and check index
			content = null;
			if (index < 0 || index >= errors.Count)
				return false;

			// Grab the requested error and check
			SimileError error = errors[index];
			switch (keyName)
			{
				case StandardTableKeyNames.DocumentName:
					content = error.DocumentName;
					return true;

				case StandardTableKeyNames.Text:
					content = error.Text;
					return true;

				case StandardTableKeyNames.Line:
					content = error.LineNumber.ToString();
					return true;

				// TODO: MORE CASES

				default:
					content = null;
					return false;
			}
		}


	}
}
