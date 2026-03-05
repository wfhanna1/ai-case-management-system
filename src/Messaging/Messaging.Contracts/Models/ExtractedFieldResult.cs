namespace Messaging.Contracts.Models;

/// <summary>
/// Represents a single field extracted from a document during OCR processing.
/// </summary>
/// <param name="FieldName">The name of the template field.</param>
/// <param name="Value">The extracted text value.</param>
/// <param name="Confidence">Model confidence score in the range [0.0, 1.0].</param>
public record ExtractedFieldResult(
    string FieldName,
    string Value,
    double Confidence);
