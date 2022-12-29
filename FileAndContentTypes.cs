using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace ConsoleCompare
{
	/// <summary>
	/// Defines content type and file extension for .simile files
	/// See: https://learn.microsoft.com/en-us/visualstudio/extensibility/walkthrough-linking-a-content-type-to-a-file-name-extension?view=vs-2022
	/// </summary>
	internal static class FileAndContentTypes
	{
		// Disabling "field never assigned to" warning since it is actually used by the project
#pragma warning disable 649
		[Export]
		[Name("simile")]
		[BaseDefinition("text")]
		internal static ContentTypeDefinition SimileContentTypeDefinition;

		[Export]
		[FileExtension(".simile")]
		[ContentType("simile")] // Must match definition above
		internal static FileExtensionToContentTypeDefinition SimileFileExtensionDefinition;
#pragma warning restore 649
	}
}
