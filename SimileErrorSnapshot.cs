
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
	// TODO: Look into just making the error an ITableEntry which can
	// directly be given to a table sink (rather than making a snapshot)

	internal class SimileError
	{
		// Add error details here
		public string Text { get; set; }
		public string DocumentName { get; set; }
		public int LineNumber { get; set; }

		public static bool operator ==(SimileError thisError, SimileError otherError)
		{
			return
				thisError.Text == otherError.Text &&
				thisError.DocumentName == otherError.DocumentName &&
				thisError.LineNumber == otherError.LineNumber;
		}

		public static bool operator !=(SimileError thisError, SimileError otherError)
		{
			return !(thisError == otherError);
		}

		public override bool Equals(object obj)
		{
			// If it's another error, compare
			if (obj is SimileError otherError)
				return this == otherError;

			// Not an error, so cannot be equivalent
			return false;
		}

		public override int GetHashCode()
		{
			return ToString().GetHashCode();
		}

		public override string ToString()
		{
			return DocumentName + ":" + LineNumber + ":" + Text;
		}
	}

	internal class SimileErrorSnapshot : WpfTableEntriesSnapshotBase
	{
		private List<SimileError> errors;

		public override int Count => errors.Count;

		public override int VersionNumber => 1;

		public SimileErrorSnapshot()
		{
			errors = new List<SimileError>();
		}

		public void AddError(SimileError error)
		{
			if(!errors.Contains(error))
				errors.Add(error);
		}

		public void ClearErrors()
		{
			errors.Clear();
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
					content = error.LineNumber;
					return true;

				default:
					content = null;
					return false;
			}
		}


	}
}
