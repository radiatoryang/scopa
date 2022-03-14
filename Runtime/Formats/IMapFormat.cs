using System.IO;

namespace Scopa.Formats.Map.Formats
{
    /// <summary>
    /// A map format class
    /// </summary>
    public interface IMapFormat
    {
        /// <summary>
        /// A short English name for the format.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// A brief description of the format.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// The name of the primary (or earliest) application to use this format.
        /// </summary>
        string ApplicationName { get; }

        /// <summary>
        /// The most common extension (without leading dot) for this format.
        /// </summary>
        string Extension { get; }

        /// <summary>
        /// Common additional extensions (without leading dot) this format can be found in.
        /// </summary>
        string[] AdditionalExtensions { get; }

        /// <summary>
        /// A list of style hints supported by this format.
        /// </summary>
        string[] SupportedStyleHints { get; }

        /// <summary>
        /// Read a map from a stream. This method will not close or dispose the stream.
        /// </summary>
        /// <param name="stream">Seekable stream</param>
        /// <returns>Loaded map</returns>
        Objects.MapFile Read(Stream stream);

        /// <summary>
        /// Write a map to a stream. This method will not close or dispose the stream.
        /// </summary>
        /// <param name="stream">Stream to write to</param>
        /// <param name="map">The map to save</param>
        /// <param name="styleHint">The style hint to apply</param>
        void Write(Stream stream, Objects.MapFile map, string styleHint);
    }
}