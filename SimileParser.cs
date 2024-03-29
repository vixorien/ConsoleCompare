﻿using System;
using System.Collections.Generic;
using System.IO;

namespace ConsoleCompare
{
	/// <summary>
	/// Helper class for parsing a ConsoleSimile from an array of lines
	/// </summary>
	internal static class SimileParser
	{
		// Parsing details
		public const string PrefaceComment = "#";
		public const string NumericTagStart = "[[";
		public const string NumericTagEnd = "]]";
		public const string NumericOptionDelimiter = ";";
		public const string NumericOptionEquals = "=";
		public const string NumericValueSetStart = "{";
		public const string NumericValueSetEnd = "}";
		public const string NumericValueSetDelimiter = ",";
		public const string InputTagStart = "{{";
		public const string InputTagEnd = "}}";

		public const string SimileSyntaxDetails =
@"== Simile File Syntax ==

 - Lines beginning with # are comments and are ignored.
 - All other lines are processed as expected program output, including blank lines.
 
 == Input Tags ==
   - User input fed into the program is represented by a string between {{ }}'s.
   - Examples: {{Hello there}} or {{12345}} or {{Jimmy}}.
   - Input tags MUST appear at the end of a line or on a line by themselves.
   - Input tags CANNOT appear on a line with a numeric tag (see below),
      as text before an input tag must have a finite length.
 
 == Numeric Tags ==
   - Numeric data that needs to be parsed and checked for validity can be
      denoted inside [[ ]]'s.
   - Several options exist for data validation (see below).
   - Multiple options can be combined in a single [[ ]] with semicolons.
   - Multiple numeric tags may appear on the same line.
   - Numeric tags appearing before the end of a line must be followed by a space.
   - Numeric tags CANNOT appear on a line with an input tag, 
      as text before an input tag must have a finite length.
   
   == Numeric Tag Syntax ==
     - type (required)
       - Supported data types: byte, sbyte, short, ushort, int, uint, long, ulong, float, double, char
       - Short names: b, sb, s, us, i, ui, l, ul, f, d, c
       - Examples: [[t=s]] or [[type=s]] or [[t=short]] would be parsed as a short
     
     - min / max (optional)
       - Inclusive minimum and/or inclusive maximum for parsed numeric value
       - Values will be parsed in accordance with the type option
       - Examples: [[t=int;min=-10]] or [[t=double;min=3.14159]] or [[t=double;max=99]] or [[t=sbyte;min=-5;max=5]]
     
     - value set (optional)
       - A set of one or more expected values
       - Values will be parsed in accordance with the type option
       - Examples: [[t=int;v={1,2,3,4}]] or [[t=int;values={5,10,15,20}]] or [[t=short;v={88}]]
     
     - precision (optional)
       - Rounds the results to the given precision for checking
       - Value must be an integer between 0-15 (inclusive)
       - Only used if overall data type is float or double
       - examples: [[t=double;precision=3]] or [[t=d;p=5]]

   == Complex Numeric Tag Examples ==
     - The circle has a radius of [[t=double;min=0;max=100;p=3]] inches.
     - Player [[t=int;v={1,2,3,4}]] has a score of [[t=int;min=0]].";


		/// <summary>
		/// Parses a file into a ConsoleSimile object if possible
		/// </summary>
		/// <param name="filePath">Path to the file</param>
		/// <returns>A new ConsoleSimile object containing the simile</returns>
		public static ConsoleSimile ParseFromFile(string filePath)
		{
			string[] lines = File.ReadAllLines(filePath);
			return Parse(lines);
		}

		/// <summary>
		/// Parses an array of lines into a ConsoleSimile object
		/// </summary>
		/// <param name="lines">Array of lines to parse</param>
		/// <returns>A new ConsoleSimile object containing the simile</returns>
		public static ConsoleSimile Parse(string[] lines)
		{
			if (lines == null)
				throw new ArgumentNullException("lines", "Array of lines to parse was null");

			if (lines.Length == 0)
				throw new SimileParseException("No lines to parse; was the file empty?");

			// Loop through lines and process each
			ConsoleSimile result = new ConsoleSimile();
			int lineNumber = 1;
			foreach (string line in lines)
			{
				// If the parse fails, throw an exception to pass failure details up
				// - TODO: Capture more details on the failure reason
				if (!ParseLine(line, lineNumber, result))
					throw new SimileParseException("Error parsing line " + lineNumber, line, lineNumber);

				lineNumber++;
			}

			return result;
		}

		/// <summary>
		/// Parses a single line and adds the result, if any, to the simile
		/// </summary>
		/// <param name="line">Line to parse</param>
		/// <param name="lineNumber">The number of the line (1-based)</param>
		/// <param name="simile">Simile to add to</param>
		/// <returns>True if the line is valid or blank, false if invalid</returns>
		private static bool ParseLine(string line, int lineNumber, ConsoleSimile simile)
		{
			// A null line is invalid
			if (line == null)
				return false;

			// Check for empty lines
			if (line.Length == 0)
			{
				// Add an expected empty line
				simile.AddOutput(new SimileLineOutput("", LineEndingType.NewLine));
				return true;
			}

			// Comment lines are valid but do not add to the simile
			if (line.StartsWith(PrefaceComment))
				return true;

			// Split the line into various line elements
			List<string> lineElements = SplitLineElements(line, lineNumber);

			// Count tag types
			int numericElementCount = 0;
			int inputElementCount = 0;
			int textElementCount = 0;
			int lastInputIndex = -1;
			for (int i = 0; i < lineElements.Count; i++)
			{
				if (lineElements[i].StartsWith(InputTagStart))
				{
					inputElementCount++;
					lastInputIndex = i;
				}
				else if (lineElements[i].StartsWith(NumericTagStart))
				{
					numericElementCount++;
				}
				else
				{
					textElementCount++;
				}
			}

			// Handle input tag first

			// Maximum of 1
			if (inputElementCount > 1)
				throw new SimileParseException("Lines may have a maximum of one input tag", line, lineNumber);

			// Cannot mix tags
			if (inputElementCount == 1 && numericElementCount > 0)
				throw new SimileParseException("Input tags may not appear on the same line as a numeric tag", line, lineNumber);

			// Must be last element on a line
			if (lastInputIndex >= 0 && lastInputIndex != lineElements.Count - 1)
				throw new SimileParseException("Input tags may only appear at the end of a line", line, lineNumber);

			// We know that if there is an input element, it's alone or at the end of a line
			if (inputElementCount == 1)
			{
				// Alone?
				if (lineElements.Count == 0)
				{
					// By itself, so add to the simile and we're done
					simile.AddInput(StripTag(lineElements[0]));
					return true;
				}
				else
				{
					// With one other text element beforehand, so add both
					// and we're done.  Note that the output is on the same line!
					simile.AddOutput(lineElements[0], LineEndingType.SameLine);
					simile.AddInput(StripTag(lineElements[1]));
					return true;
				}
			}

			// Only thing left should be text and numeric elements
			SimileLineOutput output = new SimileLineOutput(line, LineEndingType.NewLine);
			if (!ParseOutputElements(line, output))
				return false;

			// Success
			simile.AddOutput(output);
			return true;
		}

		/// <summary>
		/// Splits a line into its various elements: input tags, numeric tags and plain text
		/// </summary>
		/// <param name="line">The line of text to split</param>
		/// <param name="lineNumber">The line number for exception reporting</param>
		/// <returns>A List of line element strings</returns>
		private static List<string> SplitLineElements(string line, int lineNumber)
		{
			List<string> elements = new List<string>();
			string originalLine = line; // Save a copy for possible exceptions

			// Keep going while there are characters left
			while (line.Length > 0)
			{
				// Look for the next start tag
				int numericTagStartIndex = line.IndexOf(NumericTagStart);
				int inputTagStartIndex = line.IndexOf(InputTagStart);

				if ( // Only a numeric tag, or both exist and the numeric is first
					(numericTagStartIndex >= 0 && inputTagStartIndex == -1) ||
					(numericTagStartIndex >= 0 && numericTagStartIndex < inputTagStartIndex))
				{
					// Anything before tag?
					if (numericTagStartIndex > 0)
					{
						// Add the beginning of the line and remove
						elements.Add(line.Substring(0, numericTagStartIndex));
						line = line.Substring(numericTagStartIndex);
					}

					// String starts with tag now, search for end
					int numericTagEndIndex = line.IndexOf(NumericTagEnd);
					if (numericTagEndIndex == -1)
						throw new SimileParseException("Numeric tag not ended properly", originalLine, lineNumber);

					// Full tag exists, so add to elements and remove
					elements.Add(line.Substring(0, numericTagEndIndex + NumericTagEnd.Length));
					line = line.Substring(numericTagEndIndex + NumericTagEnd.Length);
				}
				else if ( // Only an input tag, or both exist and the input tag is first
					(inputTagStartIndex >= 0 && numericTagStartIndex == -1) ||
					(inputTagStartIndex >= 0 && inputTagStartIndex < numericTagStartIndex))
				{
					// Anything before tag?
					if (inputTagStartIndex > 0)
					{
						// Add the beginning of the line and remove
						elements.Add(line.Substring(0, inputTagStartIndex));
						line = line.Substring(inputTagStartIndex);
					}

					// String starts with tag now, search for end
					int inputTagEndIndex = line.IndexOf(InputTagEnd);
					if (inputTagEndIndex == -1)
						throw new SimileParseException("Input tag not ended properly", originalLine, lineNumber);

					// Full tag exists, so add to elements and remove
					elements.Add(line.Substring(0, inputTagEndIndex + InputTagEnd.Length));
					line = line.Substring(inputTagEndIndex + InputTagEnd.Length);
				}
				else // No tags, add the remainder to the elements
				{
					elements.Add(line);
					line = ""; // Nothing left
				}
			}

			return elements;
		}

		// CURRENTLY UNUSED
		private static string SearchForTag(string line, string tagStart, string tagEnd, int startIndex = 0)
		{
			// Empty lines obviously have no tags
			if (string.IsNullOrEmpty(line))
				return null;

			// Search for input tags
			int tagStartIndex = line.IndexOf(InputTagStart, startIndex);
			int tagEndIndex = line.IndexOf(InputTagEnd, startIndex);

			// If the end is first, or both are not found, no tag!
			if (tagEndIndex <= tagStartIndex)
				return null;

			// If one or the other, but not both, exist, no tag!
			if (tagStartIndex == -1 ^ tagEndIndex == -1) // XOR
				return null;

			// Both exist, so return the tag
			return line.Substring(tagStartIndex, tagEndIndex - tagStartIndex + tagEnd.Length);
		}

		/// <summary>
		/// Strip the start and end characters from a tag string if both are present
		/// </summary>
		/// <param name="tag">The tag to strip</param>
		/// <returns>The stripped tag if it begins and ends properly, otherwise the original string</returns>
		private static string StripTag(string tag)
		{
			// Check input tag
			if (tag.StartsWith(InputTagStart) && tag.EndsWith(InputTagEnd))
				return tag.Substring(InputTagStart.Length, tag.Length - InputTagStart.Length - InputTagEnd.Length);

			// Check numeric tag
			if (tag.StartsWith(NumericTagStart) && tag.EndsWith(NumericTagEnd))
				return tag.Substring(NumericTagStart.Length, tag.Length - NumericTagStart.Length - NumericTagEnd.Length);

			// Does not contain valid tag characters so just return the original
			return tag;
		}



		/// <summary>
		/// Recursive helper to parse the elements of a single line of output
		/// </summary>
		/// <param name="line">The string of output to parse</param>
		/// <param name="output">The output line to add elements to</param>
		/// <returns>True if the parse is successful, false otherwise</returns>
		private static bool ParseOutputElements(string line, SimileLineOutput output)
		{
			// Empty lines are valid
			if (line.Length == 0)
				return true;

			// Search for numeric tags
			int startIndex = line.IndexOf(NumericTagStart);
			int endIndex = line.IndexOf(NumericTagEnd);

			// If the end tag is first, we're invalid
			if (endIndex < startIndex)
				return false;

			// If one or the other but not both exist, this is an invalid line
			if (startIndex == -1 ^ endIndex == -1) // XOR
				return false;

			// Check line contents
			if (startIndex == -1)
			{
				// No numeric data, so this is one big text string
				output.AddTextElement(line);
				return true;
			}

			// Only regular lines (ending with a newline character) can have numeric tags
			if (output.LineEnding == LineEndingType.SameLine)
				return false;

			// Is there anything before the numeric element?
			if (startIndex > 0)
			{
				// Add the starting text to the output
				string lineStartText = line.Substring(0, startIndex);
				output.AddTextElement(lineStartText);

				// Strip the start text from the line
				line = line.Substring(startIndex);

				// Adjust indices accordingly
				endIndex -= startIndex;
				startIndex = 0;
			}

			// Grab the tag and the remainder		
			string tag = line.Substring(startIndex + NumericTagStart.Length, endIndex - startIndex - NumericTagStart.Length);
			string remainder = line.Substring(endIndex + NumericTagEnd.Length);

			// Verify that the character after the tag is indeed a space (see below for reasoning)
			if (remainder.Length > 0 && remainder[0] != ' ')
				return false;

			// Note: The above requirement is to simplify comparisons, but might
			//       be relaxed in the future after more testing and feedback!
			//       The main worry is: What happens if a numeric tag is followed
			//       by a number or other numeric-friendly character?
			//
			//       Example: [[t=int]]0011[[t=int]] 
			//
			//       In the above example, comparisons will probably never work
			//       as expected, because the "0011" will be parsed as part of the
			//       data that the numeric tag represents.  Seems problematic!
			//
			//       Or, do we say "that's on the creator of the simile"?  Or have
			//       a list of "valid" / "invalid" post-tag characters?  No idea yet.


			// Parse the tag
			string errorDetails;
			if (!ParseNumericTag(tag, output, out errorDetails))
				return false;

			// Recursively handle the remainder
			return ParseOutputElements(remainder, output);
		}

		/// <summary>
		/// Parses the contents of a numeric tag.  This method will strip the tag characters if they exist.
		/// </summary>
		/// <param name="tag">The tag itself</param>
		/// <param name="output">The output to add the numeric element to, or null if just checking validity</param>
		/// <param name="errorDetails">String holding resulting error details, if any</param>
		/// <returns>True if the tag is parsed successfully, false otherwise</returns>
		public static bool ParseNumericTag(string tag, SimileLineOutput output, out string errorDetails)
		{
			// No errors necessarily
			errorDetails = null;

			// Strip the tag characters if necessary
			tag = StripTag(tag);

			// Split into options
			string[] allOptions = tag.Split(new string[] { NumericOptionDelimiter }, StringSplitOptions.RemoveEmptyEntries);

			// Process each option
			SimileNumericType type = SimileNumericType.Unknown;
			string min = null;
			string max = null;
			string[] values = null;
			string precision = null;

			foreach (string op in allOptions)
			{
				// Split across the equals and check
				string[] pieces = op.Split(new string[] { NumericOptionEquals }, StringSplitOptions.None);
				if (pieces.Length < 2)
				{
					errorDetails = "Incomplete tag option";
					return false;
				}
				else if (pieces.Length > 2)
				{
					errorDetails = "Invalid tag option";
					return false;
				}

				// Trim both halves before checking
				pieces[0] = pieces[0].Trim();
				pieces[1] = pieces[1].Trim();
				switch (pieces[0])
				{
					case "t":
					case "type":
						type = ParseType(pieces[1]);
						if (type == SimileNumericType.Unknown)
						{
							errorDetails = "Unknown data type specified";
							return false;
						}
						break;

					case "min":
						min = pieces[1];
						break;

					case "max":
						max = pieces[1];
						break;

					case "v":
					case "values":
						// Verify set has { }'s around the values
						if (!pieces[1].StartsWith(NumericValueSetStart) ||
							!pieces[1].EndsWith(NumericValueSetEnd))
						{
							errorDetails = $"Value set values not properly contained within {NumericValueSetStart} {NumericValueSetEnd}'s";
							return false;
						}

						// Strip the start and end pieces, then split
						pieces[1] = pieces[1].Substring(NumericValueSetStart.Length, pieces[1].Length - NumericValueSetStart.Length - NumericValueSetEnd.Length);
						values = pieces[1].Split(new string[] { NumericValueSetDelimiter }, StringSplitOptions.RemoveEmptyEntries);
						if (values.Length == 0)
						{
							errorDetails = "Value set is empty";
							return false;
						}
						break;

					case "p":
					case "precision":
						precision = pieces[1];
						break;

					// Unknown option
					default:
						errorDetails = "Unknown tag option specified";
						return false;
				}
			}

			// Check the type and create the corresponding numeric element
			switch (type)
			{
				case SimileNumericType.Byte: return CreateNumericElement<byte>(type, min, max, values, precision, output, out errorDetails);
				case SimileNumericType.SignedByte: return CreateNumericElement<sbyte>(type, min, max, values, precision, output, out errorDetails);
				case SimileNumericType.Char: return CreateNumericElement<char>(type, min, max, values, precision, output, out errorDetails);
				case SimileNumericType.Short: return CreateNumericElement<short>(type, min, max, values, precision, output, out errorDetails);
				case SimileNumericType.UnsignedShort: return CreateNumericElement<ushort>(type, min, max, values, precision, output, out errorDetails);
				case SimileNumericType.Int: return CreateNumericElement<int>(type, min, max, values, precision, output, out errorDetails);
				case SimileNumericType.UnsignedInt: return CreateNumericElement<uint>(type, min, max, values, precision, output, out errorDetails);
				case SimileNumericType.Long: return CreateNumericElement<long>(type, min, max, values, precision, output, out errorDetails);
				case SimileNumericType.UnsignedLong: return CreateNumericElement<ulong>(type, min, max, values, precision, output, out errorDetails);
				case SimileNumericType.Float: return CreateNumericElement<float>(type, min, max, values, precision, output, out errorDetails);
				case SimileNumericType.Double: return CreateNumericElement<double>(type, min, max, values, precision, output, out errorDetails);

				// Invalid type found
				case SimileNumericType.Unknown:
				default:
					errorDetails = "Unknown data type specified";
					return false;
			}
		}

		/// <summary>
		/// Creates a numeric element of the specified type from the given strings
		/// </summary>
		/// <typeparam name="T">The data type of the numeric element</typeparam>
		/// <param name="type">The type</param>
		/// <param name="min">Minimum value, or null for none</param>
		/// <param name="max">Maximum value, or null for none</param>
		/// <param name="values">Array of values, or null for none</param>
		/// <param name="precision">Precision value, or null for none</param>
		/// <param name="output">The output to add the element to, or null if just checking validity</param>
		/// <param name="errorDetails">String holding resulting error details, if any</param>
		/// <returns>True if all strings are parsed correctly, false otherwise</returns>
		private static bool CreateNumericElement<T>(SimileNumericType type, string min, string max, string[] values, string precision, SimileLineOutput output, out string errorDetails)
			where T : struct, IComparable
		{
			// Assume no error at first
			errorDetails = null;

			try
			{
				SimileOutputNumeric<T> num = new SimileOutputNumeric<T>(type);
				if (min != null) num.Minimum = (T)Convert.ChangeType(min, typeof(T));
				if (max != null) num.Maximum = (T)Convert.ChangeType(max, typeof(T));
				if (values != null) foreach (string v in values) num.ValueSet.Add((T)Convert.ChangeType(v.Trim(), typeof(T)));
				if (precision != null)
				{
					num.Precision = int.Parse(precision);

					// Precision is limited to what Math.Round() accepts
					if (num.Precision < 0 || num.Precision > 15)
					{
						errorDetails = "Precision option value must be an integer between 0 and 15 (inclusive)";
						return false;
					}
				}

				// Add the element to the output line if non-null
				output?.AddNumericElement(num);
				return true;
			}
			catch
			{
				// One of the casts failed, so the numeric element is invalid
				errorDetails = "One or more tag option values do not match specified tag data type";
				return false;
			}
		}


		/// <summary>
		/// Parses a numeric type from the given string.  The following are valid type strings:
		/// 
		/// b, byte, sb, sbyte
		/// us, ushort, ui, uint, ul, ulong
		/// s, short, i, int, l, long
		/// f, float, d, double
		/// c, char
		/// </summary>
		/// <param name="type">The type as a string</param>
		/// <returns>The numeric type.  If the parse fails, Unknown is returned</returns>
		private static SimileNumericType ParseType(string type)
		{
			// Verify we have a string
			if (type == null)
				return SimileNumericType.Unknown;

			// Check for known values
			switch (type.ToLower())
			{
				case "b": case "byte": return SimileNumericType.Byte;
				case "sb": case "sbyte": return SimileNumericType.SignedByte;
				case "s": case "short": return SimileNumericType.Short;
				case "us": case "ushort": return SimileNumericType.UnsignedShort;
				case "i": case "int": return SimileNumericType.Int;
				case "ui": case "uint": return SimileNumericType.UnsignedInt;
				case "l": case "long": return SimileNumericType.Long;
				case "ul": case "ulong": return SimileNumericType.UnsignedLong;
				case "f": case "float": return SimileNumericType.Float;
				case "d": case "double": return SimileNumericType.Double;
				case "c": case "char": return SimileNumericType.Char;
				default: return SimileNumericType.Unknown;
			}
		}
	}

	/// <summary>
	/// Represents a problem during a simile parse
	/// </summary>
	public class SimileParseException : Exception
	{
		/// <summary>
		/// Gets the number of the line on which the error occured
		/// </summary>
		public int LineNumber { get; }

		/// <summary>
		/// Gets the line of text that caused the error
		/// </summary>
		public string LineText { get; }

		/// <summary>
		/// Creates an exception object to represent an error during a simile parse
		/// </summary>
		/// <param name="message">The overall error message</param>
		/// <param name="line">The line from the simile that caused the error</param>
		/// <param name="lineNumber">The number of the line that caused the error</param>
		public SimileParseException(string message, string lineText = null, int lineNumber = -1)
			: base(message)
		{
			LineText = lineText;
			LineNumber = lineNumber;
		}
	}
}
